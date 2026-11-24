using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 지금까지 모은 것들을 모아보는 도감 — 카드(칵테일/차/하이브리드), 영구 강화 노드, 재료 누적 수집량.
    /// 직업 선택 화면에서 여는 버튼으로 연다. 전부 런이 끝나도 사라지지 않고 영구 저장된 값을 보여준다
    /// (PermanentProgress). 카드/영구 강화는 항목 수가 많아 세로 스크롤 가능한 영역 안에 넣는다.
    /// 08:95 "도감 탭4개, 카드 카테고리 필터 전체/칵테일/차/하이브리드" — 탭은 카드 3열(영구 강화 열은
    /// 카드가 아니라서 탭과 무관하게 항상 보임)의 표시 여부만 바꾼다(2026-09-28). 07:167 "하이브리드는
    /// 발견 전에도 조건 표시" — 하이브리드 카드는 일반 카드의 "???" 대신 미발견이어도 해금 조건 문구를
    /// 보여준다.
    /// </summary>
    public class CardCodexView : MonoBehaviour
    {
        enum Tab { All, Cocktail, Tea, Hybrid }
        class CardEntry
        {
            public CardData card;
            public Text nameText;
            public Text descText;
            public Text flavorText;
            /// <summary>도감 정리: 등급(왼쪽 flavorText)과 분리한 획득 상태(오른쪽 정렬, 발견한 카드만 "발견함").</summary>
            public Text statusText;
            public Image icon;
            public GameObject lockBadge;
        }

        class SkillEntry
        {
            public PermanentSkillData skill;
            public Text nameText;
            public Text descText;
            /// <summary>효과(descText)와 분리한 해금 상태 줄: "해금됨" / "미해금 · 비용 N" / "비활성(선행 확인 필요)".</summary>
            public Text statusText;
        }

        /// <summary>07:169 커피 노드 — 처음부터 이름/효과/선행 표시, "최초 획득" 기록만 구분(카드의 "???" 미발견, 영구의
        /// "미해금"과 다른 문구).</summary>
        class CoffeeEntry
        {
            public SkillData skill;
            public Text nameText;
            public Text descText;
            public Text prereqText;
            public Image icon;
            /// <summary>그림 칸 위 "해금 기록"/"기록 없음" 칩(그림이 없는 노드는 null — 그때는 설명 아래 줄에 상태를 붙인다).</summary>
            public GameObject recordChip;
            public Text recordChipText;
            public bool acquired;
        }

        public const string CoffeeAcquiredLabel = "최초 획득 기록 있음";
        public const string CoffeeNotAcquiredLabel = "아직 획득 기록 없음";

        class MaterialEntry
        {
            public string label;
            public IngredientType type;
            public Func<SaveData, int> getCount;
            public Text headerText;
            public Text varietyText;
            public Button button;
        }

        // QA 2026-10-05(720p): 목록 글자가 ~8px로 08:49 "720p에서도 본문 환산 최소16px(1080기준24)" 미달 —
        // 뷰포트를 안전 여백48 안 최대 폭(1824)으로 넓히고 열 440/간격 452, 본문 24·이름 26, 행 높이를 키웠다.
        // 설명은 최대 2줄, 풍미/선행은 1줄로 자르고(행끼리 겹치지 않음) 전문은 클릭 상세에서 보인다.
        public const int MinBodyFontSize = 24;
        public const float ViewportWidth = 1824f;
        // r7: 상자 높이를 폰트 실제 줄 높이로 계산(r6의 고정 34/64/32는 Noto Sans KR 줄 높이보다 낮아 이름·???·2번째 줄이 사라짐).
        public const int RowNameFontSize = 26;
        static float RowNameHeight => UiFactory.TruncatedBoxHeight(RowNameFontSize, 2);
        static float RowDescHeight => UiFactory.TruncatedBoxHeight(MinBodyFontSize, 2);
        static float RowFlavorHeight => UiFactory.TruncatedBoxHeight(MinBodyFontSize);
        static float RowStep => RowNameHeight + RowDescHeight + RowFlavorHeight + 6f;
        static float MaterialHeaderHeight => UiFactory.TruncatedBoxHeight(MinBodyFontSize);
        static float MaterialVarietyHeight => UiFactory.TruncatedBoxHeight(MinBodyFontSize, 2);
        static float MaterialSlotHeight => 2f + MaterialHeaderHeight + 2f + MaterialVarietyHeight + 2f;
        const float ColumnWidth = 440f;
        const float ColumnSpacing = 452f; // 4열 폭 3×452+440 = 1796 ≤ 뷰포트 1824
        const float ContentTopPadding = 40f; // 헤더 높이만큼
        const float ContentBottomPadding = 20f;

        readonly List<CardEntry> cardEntries = new List<CardEntry>();
        readonly List<SkillEntry> skillEntries = new List<SkillEntry>();
        readonly List<MaterialEntry> materialEntries = new List<MaterialEntry>();
        readonly List<CoffeeEntry> coffeeEntries = new List<CoffeeEntry>();

        /// <summary>41 히든 스킬 — 발견 전 "???" + 등급·첫 요소 힌트(사용자 결정 전 기본값), 발견 후 조건·효과.</summary>
        class HiddenEntry
        {
            public HiddenSkillDef def;
            public Text nameText, descText, effectText;
        }
        readonly List<HiddenEntry> hiddenEntries = new List<HiddenEntry>();
        public int HiddenFoundForTests => hiddenEntries.Count(e => PermanentProgress.Current.discoveredCardIds.Contains(e.def.id));
        public int HiddenRowsForTests => hiddenEntries.Count;
        public string HiddenNameTextForTests(int i) => hiddenEntries[i].nameText.text;
        readonly List<GameObject> cocktailColumnObjects = new List<GameObject>();
        readonly List<GameObject> teaColumnObjects = new List<GameObject>();
        readonly List<GameObject> hybridColumnObjects = new List<GameObject>();

        GameObject root;
        RectTransform content;
        Text countLabel;
        Action onClose;
        Tab tab = Tab.All;
        ScrollRect scrollRect;
        GameObject detailRoot;
        GameObject detailShield;
        Text detailTitle;
        Text detailBody;
        RectTransform detailContent;
        /// <summary>테스트용 — 상세에 보이는 출처 주소 복사 버튼(카드 상세만).</summary>
        public readonly List<Button> DetailSourceButtons = new List<Button>();
        public RectTransform DetailSourcesRect => detailContent != null ? detailContent.Find("Sources") as RectTransform : null;
        public RectTransform DetailBodyRect => detailBody != null ? detailBody.rectTransform : null;
        Button detailClose;
        ScrollRect detailScroll;
        GameObject detailReturnFocus;
        readonly Dictionary<Tab, Button> tabButtons = new Dictionary<Tab, Button>();

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트용 — Show() 이후에만 채워진다 (카드 항목 수).</summary>
        public int EntryCount => cardEntries.Count;
        /// <summary>테스트용 — Show() 이후에만 채워진다 (영구 강화 항목 수).</summary>
        public int SkillEntryCount => skillEntries.Count;
        /// <summary>테스트용 — Show() 이후에만 채워진다 (커피 노드 항목 수).</summary>
        public int CoffeeEntryCount => coffeeEntries.Count;
        /// <summary>테스트용 — 상단 집계 문구.</summary>
        public string CountText => countLabel.text;
        /// <summary>테스트용 — id로 커피 노드 항목의 이름/설명(상태 포함)/선행 문구를 읽는다.</summary>
        public (string name, string desc, string prereq) GetCoffeeText(string skillId)
        {
            var entry = coffeeEntries.Find(e => e.skill.id == skillId);
            if (entry == null) return (null, null, null);
            // 상태 칩이 따로 있으면 desc에는 효과만 있다 — 테스트·접근성용으로 상태 문구를 이어 돌려준다.
            string status = entry.acquired ? CoffeeAcquiredLabel : CoffeeNotAcquiredLabel;
            string desc = entry.recordChip != null ? entry.descText.text + "\n" + status : entry.descText.text;
            return (entry.nameText.text, desc, entry.prereqText.text);
        }
        /// <summary>테스트용 — 영구 강화 항목 설명(상태 포함).</summary>
        public string GetPermanentText(string skillId)
        {
            var e = skillEntries.Find(x => x.skill.id == skillId);
            return e == null ? null : e.descText.text + "\n" + e.statusText.text;
        }
        /// <summary>테스트용 — 카드 행의 등급(왼쪽)·획득 상태(오른쪽) 글.</summary>
        public (string rarity, string status) GetCardMeta(string cardId)
        {
            var e = cardEntries.Find(x => x.card.id == cardId);
            return e == null ? (null, null) : (e.flavorText.text, e.statusText.text);
        }
        /// <summary>테스트용 — 재료 칸 머리줄·이름 줄.</summary>
        public (string header, string names) GetMaterialText(IngredientType type)
        {
            var e = materialEntries.Find(x => x.type == type);
            return e == null ? (null, null) : (e.headerText.text, e.varietyText.text);
        }
        public bool OpenMaterialDetailForTests(IngredientType type)
        {
            var e = materialEntries.Find(x => x.type == type);
            if (e == null || e.button == null) return false;
            e.button.onClick.Invoke();
            return true;
        }
        /// <summary>테스트/버튼 공용 진입점 — 08:95 4탭.</summary>
        public void SelectAllTab() => SetTab(Tab.All);
        public void SelectCocktailTab() => SetTab(Tab.Cocktail);
        public void SelectTeaTab() => SetTab(Tab.Tea);
        public void SelectHybridTab() => SetTab(Tab.Hybrid);
        /// <summary>테스트용 — 현재 탭에서 해당 카테고리 카드 열(헤더 포함)이 보이는지.</summary>
        public bool IsCocktailColumnVisible => cocktailColumnObjects.Count > 0 && cocktailColumnObjects[0].activeSelf;
        public bool IsTeaColumnVisible => teaColumnObjects.Count > 0 && teaColumnObjects[0].activeSelf;
        public bool IsHybridColumnVisible => hybridColumnObjects.Count > 0 && hybridColumnObjects[0].activeSelf;
        /// <summary>테스트용 — id로 카드 항목의 현재 표시 텍스트를 읽는다.</summary>
        public (string name, string desc) GetCardText(string cardId)
        {
            var entry = cardEntries.Find(e => e.card.id == cardId);
            return entry == null ? (null, null) : (entry.nameText.text, entry.descText.text);
        }

        public static CardCodexView Create(Transform canvas, Action onClose)
        {
            var rootRt = UiFactory.Stretch("CardCodex", canvas);
            UiFactory.AddImage(rootRt, new Color(0.05f, 0.04f, 0.07f, 0.97f));
            UiFactory.AddCafeBackdrop(rootRt);

            var view = rootRt.gameObject.AddComponent<CardCodexView>();
            view.root = rootRt.gameObject;
            view.onClose = onClose;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(1400f, 60f));
            UiFactory.AddText(title, "도감", 44, TextAnchor.MiddleCenter, Color.white);

            var countRt = UiFactory.Place("Count", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(1200f, 36f));
            view.countLabel = UiFactory.AddText(countRt, "", 24, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.65f, 1f));

            view.BuildMaterialsRow(rootRt);
            view.BuildTabs(rootRt);

            // 스크롤 영역: 탭 줄 아래 ~ 뒤로 버튼 위 사이에만 배치, 안쪽은 RectMask2D로 잘라낸다.
            var scrollAreaRt = UiFactory.Place("ScrollArea", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -346f),
                new Vector2(ViewportWidth, 600f));
            var viewportRt = UiFactory.Stretch("Viewport", scrollAreaRt);
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 656f); // 실제 행 개수에 맞춰 BuildRows에서 다시 계산
            view.content = contentRt;

            var scrollRect = scrollAreaRt.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            // G31: 키보드로 행을 옮기면 선택한 행이 보이도록 스크롤을 따라간다.
            scrollAreaRt.gameObject.AddComponent<ScrollToSelected>().Init(scrollRect, contentRt, viewportRt);
            view.scrollRect = scrollRect;
            view.BuildDetailPanel(rootRt);

            var backRt = UiFactory.Place("BackButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 36f),
                new Vector2(320f, 66f));
            var backBg = UiFactory.AddImage(backRt, new Color(0.5f, 0.2f, 0.55f));
            var backButton = backRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(backButton);
            backButton.targetGraphic = backBg;
            UiFactory.ApplyHoverColors(backButton);
            backButton.onClick.AddListener(view.Close);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), UiFactory.InputLabel("뒤로", "Esc"), 30, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        /// <summary>08:95 "도감 탭4개, 카드 카테고리 필터 전체/칵테일/차/하이브리드". 영구 강화 열은
        /// 카드가 아니라서 탭 대상이 아니고 항상 보인다.</summary>
        void BuildTabs(RectTransform rootRt)
        {
            // 2026-10-04: 재료 행(-140..-270)과 겹치지 않게 -284로(QA 지적 — 이전 -224는 44px 겹침).
            var rowRt = UiFactory.Place("Tabs", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -284f),
                new Vector2(1000f, 50f));

            AddTab(rowRt, -345f, Tab.All, "전체");
            AddTab(rowRt, -115f, Tab.Cocktail, "칵테일");
            AddTab(rowRt, 115f, Tab.Tea, "차");
            AddTab(rowRt, 345f, Tab.Hybrid, "하이브리드");
        }

        void AddTab(RectTransform parent, float x, Tab value, string label)
        {
            var rt = UiFactory.Place($"Tab_{value}", parent, new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(210f, 50f));
            var bg = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f));
            UiFactory.SkinTab(bg);
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => SetTab(value));
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 24, TextAnchor.MiddleCenter, Color.white);
            tabButtons[value] = button;
        }

        void SetTab(Tab value)
        {
            AudioService.PlaySfx("ui_confirm");
            tab = value;
            ApplyTabFilter();
        }

        void ApplyTabFilter()
        {
            foreach (var kv in tabButtons)
                UiFactory.SetChoice(kv.Value.image, kv.Key == tab,
                    kv.Key == tab ? new Color(0.42f, 0.28f, 0.18f) : new Color(0.16f, 0.13f, 0.2f));

            SetColumnVisible(cocktailColumnObjects, tab == Tab.All || tab == Tab.Cocktail);
            SetColumnVisible(teaColumnObjects, tab == Tab.All || tab == Tab.Tea);
            SetColumnVisible(hybridColumnObjects, tab == Tab.All || tab == Tab.Hybrid);
        }

        static void SetColumnVisible(List<GameObject> objects, bool visible)
        {
            foreach (var go in objects) go.SetActive(visible);
        }

        void BuildMaterialsRow(RectTransform rootRt)
        {
            var rowRt = UiFactory.Place("Materials", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -136f),
                new Vector2(ViewportWidth, MaterialSlotHeight));

            AddMaterialSlot(rowRt, -608f, "Icons/cocktail_ingredient", "칵테일 재료", IngredientType.Cocktail,
                s => s.cocktailIngredientsCollected);
            AddMaterialSlot(rowRt, 0f, "Icons/tea", "찻잎", IngredientType.Tea, s => s.teaLeavesCollected);
            AddMaterialSlot(rowRt, 608f, "Icons/coffee_bean", "커피 원두", IngredientType.Coffee,
                s => s.coffeeBeansCollected);
        }

        void AddMaterialSlot(RectTransform parent, float x, string iconPath, string label, IngredientType type,
            Func<SaveData, int> getCount)
        {
            var slotRt = UiFactory.Place($"Material_{label}", parent, new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(590f, MaterialSlotHeight));
            // 종류 이름 전체는 눌러서(키보드 Enter 포함) 상세에서 본다 — 줄에는 수집 종류와 누적량을 우선 보인다.
            var slotBg = slotRt.gameObject.AddComponent<Image>();
            slotBg.sprite = SpriteFactory.Square;
            slotBg.color = new Color(1f, 1f, 1f, 0.03f);
            var slotButton = slotRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(slotButton);
            slotButton.targetGraphic = slotBg;
            var slotColors = slotButton.colors;
            slotColors.highlightedColor = new Color(1f, 1f, 1f, 4f);
            slotColors.selectedColor = new Color(0.4f, 1f, 1f, 6f);
            slotButton.colors = slotColors;
            slotButton.onClick.AddListener(() =>
            {
                detailReturnFocus = slotRt.gameObject;
                ShowMaterialDetail(label, type, getCount);
            });

            var icon = Resources.Load<Sprite>(iconPath);
            if (icon != null)
            {
                var iconRt = UiFactory.Place("Icon", slotRt, new Vector2(0f, 0.5f), new Vector2(10f, 0f),
                    new Vector2(56f, 56f));
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            var headerRt = UiFactory.Place("Header", slotRt, new Vector2(0f, 1f), new Vector2(76f, -2f),
                new Vector2(510f, MaterialHeaderHeight));
            var headerText = UiFactory.AddText(headerRt, "", MinBodyFontSize, TextAnchor.UpperLeft, new Color(0.92f, 0.92f, 0.92f));
            headerText.verticalOverflow = VerticalWrapMode.Truncate;

            var varietyRt = UiFactory.Place("Varieties", slotRt, new Vector2(0f, 1f), new Vector2(76f, -4f - MaterialHeaderHeight),
                new Vector2(510f, MaterialVarietyHeight));
            var varietyText = UiFactory.AddText(varietyRt, "", MinBodyFontSize, TextAnchor.UpperLeft, Color.white);
            varietyText.verticalOverflow = VerticalWrapMode.Truncate;
            varietyText.supportRichText = true;

            materialEntries.Add(new MaterialEntry
            {
                label = label, type = type, getCount = getCount, headerText = headerText, varietyText = varietyText, button = slotButton,
            });
        }

        void BuildRows()
        {
            var all = CardDatabase.LoadAll();
            int cocktailCount = BuildCardColumn(all.Where(c => c.category == CardCategory.Cocktail),
                -3f * ColumnSpacing / 2f, "칵테일", cocktailColumnObjects);
            int teaCount = BuildCardColumn(all.Where(c => c.category == CardCategory.Tea),
                -ColumnSpacing / 2f, "차", teaColumnObjects);
            int hybridCount = BuildCardColumn(all.Where(c => c.category == CardCategory.Hybrid),
                ColumnSpacing / 2f, "하이브리드", hybridColumnObjects);
            int skillCount = BuildSkillColumn(PermanentSkillDatabase.LoadAll(), 3f * ColumnSpacing / 2f, "영구 강화");
            // 커피 노드 15개는 카드가 아니라 탭 필터와 무관하게 항상 보인다 — 하이브리드 열 아래에 이어 둔다.
            int coffeeRows = BuildCoffeeSection(SkillDatabase.LoadAll(), ColumnSpacing / 2f, hybridCount + 1);

            int hiddenRows = BuildHiddenSection(3f * ColumnSpacing / 2f, skillCount + 1);
            int maxRows = Mathf.Max(Mathf.Max(cocktailCount, teaCount), Mathf.Max(hybridCount + 1 + coffeeRows, skillCount + 1 + hiddenRows));
            float height = ContentTopPadding + maxRows * RowStep + ContentBottomPadding;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
            ApplyTabFilter();
        }

        int BuildCardColumn(IEnumerable<CardData> cards, float x, string headerLabel, List<GameObject> trackInto)
        {
            var ordered = cards.OrderBy(c => c.rarity).ThenBy(c => c.displayName).ToList();
            trackInto.Add(BuildHeader(x, headerLabel));

            for (int i = 0; i < ordered.Count; i++)
            {
                var card = ordered[i];
                var rowRt = UiFactory.Place($"Row_{card.id}", content, new Vector2(0.5f, 1f),
                    new Vector2(x, -ContentTopPadding - i * RowStep), new Vector2(ColumnWidth, RowStep));

                var capturedCard = card;
                MakeRowSelectable(rowRt, () => ShowCardDetail(capturedCard));
                // 2026-11-15 피드백 2: 그림 → 이름 → 획득 상태. 이전 그림은 오른쪽 위 38(720p ≈25px, 미발견이면 숨김)이었다.
                var cardIcon = ArtCell(rowRt, CardEducationIcons.For(card));
                var lockBadge = LockBadge(rowRt);
                var nameText = RowText(rowRt, "Name", ArtTextLeft, 0f, ColumnWidth - ArtTextLeft - 8f, RowNameHeight, RowNameFontSize, TextAnchor.MiddleLeft, true);
                var descText = RowText(rowRt, "Desc", ArtTextLeft, -RowNameHeight, ColumnWidth - ArtTextLeft - 8f, RowDescHeight, MinBodyFontSize,
                    TextAnchor.UpperLeft, true);
                var flavorText = RowText(rowRt, "Flavor", ArtTextLeft, -RowNameHeight - RowDescHeight, ColumnWidth - ArtTextLeft - 8f, RowFlavorHeight,
                    MinBodyFontSize, TextAnchor.MiddleLeft, true);
                // 등급(왼쪽)과 획득 상태(오른쪽)를 서로 다른 글로 — 이전 "획득 · 일반"은 둘을 한 줄에 섞었다.
                var statusText = RowText(rowRt, "Status", ArtTextLeft, -RowNameHeight - RowDescHeight, ColumnWidth - ArtTextLeft - 8f, RowFlavorHeight,
                    MinBodyFontSize, TextAnchor.MiddleRight, true);

                cardEntries.Add(new CardEntry
                {
                    card = card, icon = cardIcon, nameText = nameText, descText = descText, flavorText = flavorText, statusText = statusText, lockBadge = lockBadge,
                });
                trackInto.Add(rowRt.gameObject);
            }

            return ordered.Count;
        }

        int BuildSkillColumn(IEnumerable<PermanentSkillData> skills, float x, string headerLabel)
        {
            var ordered = skills.OrderBy(s => s.branch).ThenBy(s => s.tier).ToList();
            BuildHeader(x, headerLabel);

            for (int i = 0; i < ordered.Count; i++)
            {
                var skill = ordered[i];
                var rowRt = UiFactory.Place($"SkillRow_{skill.id}", content, new Vector2(0.5f, 1f),
                    new Vector2(x, -ContentTopPadding - i * RowStep), new Vector2(ColumnWidth, RowStep));

                var capturedPerm = skill;
                MakeRowSelectable(rowRt, () => ShowPermanentDetail(capturedPerm));
                var icon = PermanentSkillView.Icon(skill);
                float textLeft = 0f;
                if (icon != null)
                {
                    var iconRt = UiFactory.Place("Icon", rowRt, new Vector2(0f, 1f), new Vector2(0f, -2f),
                        new Vector2(60f, 60f));
                    var iconImg = iconRt.gameObject.AddComponent<Image>();
                    iconImg.sprite = icon;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                    textLeft = 68f;
                }

                var nameText = RowText(rowRt, "Name", textLeft, 0f, ColumnWidth - textLeft, RowNameHeight, RowNameFontSize,
                    TextAnchor.MiddleLeft, true);
                var descText = RowText(rowRt, "Desc", textLeft, -RowNameHeight, ColumnWidth - textLeft,
                    RowDescHeight, MinBodyFontSize, TextAnchor.UpperLeft, true);
                // 효과(위)와 해금 상태(아래 줄)를 분리 — 이전에는 "효과  ·  미해금"이 한 글에 섞여 줄바꿈에서 상태가 묻혔다.
                var statusText = RowText(rowRt, "Status", textLeft, -RowNameHeight - RowDescHeight, ColumnWidth - textLeft,
                    RowFlavorHeight, MinBodyFontSize, TextAnchor.MiddleLeft, true);

                skillEntries.Add(new SkillEntry { skill = skill, nameText = nameText, descText = descText, statusText = statusText });
            }

            return ordered.Count;
        }

        /// <summary>커피 노드 섹션. startRow 행 위치에 머리글, 그 아래 노드 행. 사용한 행 수(머리글 제외)를 돌려준다.</summary>
        int BuildCoffeeSection(IEnumerable<SkillData> skills, float x, int startRow)
        {
            var ordered = skills.Where(s => s != null && !string.IsNullOrEmpty(s.id))
                .OrderBy(s => s.branch).ThenBy(s => s.tier).ToList();
            float top = -startRow * RowStep;
            BuildHeader(x, "직업 연구", top);

            for (int i = 0; i < ordered.Count; i++)
            {
                var skill = ordered[i];
                var rowRt = UiFactory.Place($"CoffeeRow_{skill.id}", content, new Vector2(0.5f, 1f),
                    new Vector2(x, top - ContentTopPadding - i * RowStep), new Vector2(ColumnWidth, RowStep));
                var capturedSkill = skill;
                MakeRowSelectable(rowRt, () => ShowCoffeeDetail(capturedSkill));
                // 2026-11-15 피드백 2: 연구 노드 그림(2026-11-12 승인 아트)을 왼쪽 칸에 — 그림이 없으면 기존 전체 폭 글.
                var art = ResearchIcons.Research(skill.id, Job.Bartender);
                Image icon = art != null ? ArtCell(rowRt, art) : null;
                float left = icon != null ? ArtTextLeft : 0f, width = icon != null ? ColumnWidth - ArtTextLeft - 8f : ColumnWidth;
                GameObject chip = null; Text chipText = null;
                if (icon != null) chip = RecordChip(rowRt, out chipText);
                var nameText = RowText(rowRt, "Name", left, 0f, width, RowNameHeight, RowNameFontSize, TextAnchor.MiddleLeft, icon != null);
                var descText = RowText(rowRt, "Desc", left, -RowNameHeight, width, RowDescHeight, MinBodyFontSize,
                    TextAnchor.UpperLeft, icon != null);
                var prereqText = RowText(rowRt, "Prereq", left, -RowNameHeight - RowDescHeight, width, RowFlavorHeight,
                    MinBodyFontSize, TextAnchor.MiddleLeft, icon != null);
                prereqText.color = new Color(0.7f, 0.68f, 0.75f);
                coffeeEntries.Add(new CoffeeEntry { skill = skill, nameText = nameText, descText = descText, prereqText = prereqText, icon = icon, recordChip = chip, recordChipText = chipText });
            }
            return ordered.Count;
        }

        /// <summary>41 히든 스킬 섹션(영구 강화 열 아래). 사용한 행 수(머리글 제외).</summary>
        int BuildHiddenSection(float x, int startRow)
        {
            float top = -startRow * RowStep;
            BuildHeader(x, "히든 스킬", top);
            var all = HiddenSkillDatabase.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                var rowRt = UiFactory.Place($"HiddenRow_{def.id}", content, new Vector2(0.5f, 1f),
                    new Vector2(x, top - ContentTopPadding - i * RowStep), new Vector2(ColumnWidth, RowStep));
                var captured = def;
                MakeRowSelectable(rowRt, () => ShowHiddenDetail(captured));
                var nameText = RowText(rowRt, "Name", 0f, 0f, ColumnWidth, RowNameHeight, RowNameFontSize, TextAnchor.MiddleLeft);
                var descText = RowText(rowRt, "Desc", 0f, -RowNameHeight, ColumnWidth, RowDescHeight, MinBodyFontSize, TextAnchor.UpperLeft);
                var effectText = RowText(rowRt, "Effect", 0f, -RowNameHeight - RowDescHeight, ColumnWidth, RowFlavorHeight,
                    MinBodyFontSize, TextAnchor.MiddleLeft);
                hiddenEntries.Add(new HiddenEntry { def = def, nameText = nameText, descText = descText, effectText = effectText });
            }
            return all.Count;
        }

        void ShowHiddenDetail(HiddenSkillDef def)
        {
            bool has = PermanentProgress.Current.discoveredCardIds.Contains(def.id);
            ShowDetail(has ? def.name : "???",
                has ? $"히든 스킬 · {HiddenSkillDatabase.TierLabel(def.tier)}\n바텐더: {def.ConditionFor(Job.Bartender)}\n티 소믈리에: {def.ConditionFor(Job.TeaSommelier)}\n효과: {def.effect}\n런마다 조건을 다시 채우면 그 런에서 발동"
                    : $"히든 스킬 · {HiddenSkillDatabase.TierLabel(def.tier)}\n힌트: 바텐더 {def.HintFor(Job.Bartender)} · 티 소믈리에 {def.HintFor(Job.TeaSommelier)}\n아직 발견하지 않았습니다");
        }

        /// <summary>2026-11-15 피드백 2: 행 왼쪽 그림 칸(1080 기준 112 = 720p ≈75px, 이전 38 = 25px). 연한 바탕 위에 그림 — 미발견은
        /// 같은 그림을 어두운 실루엣으로(피드백 3) 보여 바탕과 구분된다.</summary>
        public const float ArtCellSize = 112f, ArtCellLeft = 12f, ArtTextLeft = ArtCellLeft + ArtCellSize + 12f;
        public const string LockedLabel = "미발견";
        public const string FoundLabel = "발견함";

        /// <summary>도감 카드 효과 줄. 효과 이름·부호·단위가 읽히게 — 재료 게이지 카드는 "필요량"이 무엇의 필요량인지 풀어 쓴다
        /// (PlayerStats.GaugeNeedMultiplier는 레벨업 재료 게이지가 한 칸 차는 데 모아야 하는 재료 양에 곱해진다). 수치는 카드 데이터 값 그대로.</summary>
        public static string EffectText(CardData card)
        {
            if (card != null && card.effects != null && card.effects.Length == 1 && card.effects[0].type == CardEffectType.GaugeNeedPercent)
                return "게이지를 채우는 데 필요한 재료 " + StatStyle.Value(card.effects[0]);
            return CardEducationSummary.Effect(card);
        }
        public const string LockedHint = "플레이 중 획득하면 등록됩니다";

        static string CategoryLabel(CardCategory c) => c == CardCategory.Cocktail ? "칵테일" : c == CardCategory.Tea ? "차" : "하이브리드";

        static Image ArtCell(RectTransform row, Sprite sprite)
        {
            var cell = UiFactory.Place("ArtCell", row, new Vector2(0f, 0.5f), new Vector2(ArtCellLeft, 0f), new Vector2(ArtCellSize, ArtCellSize));
            var back = cell.gameObject.AddComponent<Image>();
            back.sprite = SpriteFactory.Square;
            back.color = new Color(0.93f, 0.86f, 0.72f, 0.16f);
            back.raycastTarget = false;
            var iconRt = UiFactory.Place("EducationIcon", cell, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ArtCellSize - 8f, ArtCellSize - 8f));
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = sprite != null;
            return img;
        }

        /// <summary>피드백 3 잠금 배지: 그림 칸 위쪽 "미발견" 글 칩(색만으로 잠금을 나타내지 않는다).</summary>
        static GameObject LockBadge(RectTransform row)
        {
            var rt = UiFactory.Place("LockBadge", row, new Vector2(0f, 0.5f), new Vector2(ArtCellLeft, ArtCellSize * 0.5f - 22f), new Vector2(ArtCellSize, 44f));
            rt.pivot = new Vector2(0f, 0.5f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Square;
            bg.color = new Color(0.12f, 0.1f, 0.14f, 0.92f);
            bg.raycastTarget = false;
            var labelRt = UiFactory.Stretch("Label", rt);
            var t = UiFactory.AddText(labelRt, LockedLabel, MinBodyFontSize, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f));
            t.raycastTarget = false;
            // 2026-11-16 최종 아트: 칩 왼쪽 자물쇠(28) + 글(자물쇠 오른쪽). 글자와 모양 둘 다로 잠금 표시.
            if (UiFactory.AddLockIcon(rt, new Vector2(4f, 0f), 28f) != null) labelRt.offsetMin = new Vector2(34f, labelRt.offsetMin.y); // 글 칸 78 ≥ "미발견" 24pt 약 72
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        public const string RecordChipHas = "해금 기록", RecordChipNone = "기록 없음";

        /// <summary>직업 연구 그림 칸 위 칩: 한 번이라도 해금한 기록이 있는지(비밀 아님 — 이름·효과는 항상 보임). 4글자 24pt가 칩 안에 들어간다.</summary>
        static GameObject RecordChip(RectTransform row, out Text label)
        {
            var rt = UiFactory.Place("RecordChip", row, new Vector2(0f, 0.5f), new Vector2(ArtCellLeft, ArtCellSize * 0.5f - 22f), new Vector2(ArtCellSize, 44f));
            rt.pivot = new Vector2(0f, 0.5f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Square;
            bg.color = new Color(0.12f, 0.1f, 0.14f, 0.92f);
            bg.raycastTarget = false;
            label = UiFactory.AddText(UiFactory.Stretch("Label", rt), "", MinBodyFontSize, TextAnchor.MiddleCenter, Color.white);
            label.raycastTarget = false;
            return rt.gameObject;
        }

        static readonly Color SilhouetteColor = new Color(0.02f, 0.02f, 0.03f, 0.75f);

        /// <summary>행 안 글줄: 상자 높이를 넘는 줄은 잘라 이웃 행을 덮지 않는다(전문은 상세). leftAnchored면 좌상단 기준.</summary>
        static Text RowText(RectTransform row, string name, float x, float y, float width, float height, int size,
            TextAnchor anchor, bool leftAnchored = false)
        {
            if (UiFactory.IsCafe(row.GetComponent<Image>(), "ui_cafe_card"))
            { width -= 32f; if (leftAnchored) x += 16f; }
            var rt = UiFactory.Place(name, row, leftAnchored ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f),
                new Vector2(x, y), new Vector2(width, height));
            var text = UiFactory.AddText(rt, "", size, anchor, Color.white);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        GameObject BuildHeader(float x, string headerLabel, float y = 0f)
        {
            var header = UiFactory.Place($"Header_{headerLabel}", content, new Vector2(0.5f, 1f),
                new Vector2(x, y), new Vector2(ColumnWidth, ContentTopPadding));
            UiFactory.AddText(header, headerLabel, 30, TextAnchor.MiddleCenter, new Color(0.9f, 0.7f, 0.5f));
            return header.gameObject;
        }

        public void Show()
        {
            if (cardEntries.Count == 0 && skillEntries.Count == 0 && coffeeEntries.Count == 0) BuildRows();
            Refresh();
            root.SetActive(true);
            // The inactive ScrollRect had cached its old content size. Reopen at the headers,
            // after layout knows the complete rows, instead of clamping to the bottom.
            Canvas.ForceUpdateCanvases();
            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(tabButtons[Tab.All].gameObject);
        }

        void Refresh()
        {
            var save = PermanentProgress.Current;
            var discovered = new HashSet<string>(save.discoveredCardIds);
            // 영구 강화 상태는 효과·비용과 같은 기준(유효/비활성 옛 노드)으로 — 알 수 없는 id는 화면에 없고 원본 목록은 그대로.
            var permanentTree = new PermanentSkillTree(skillEntries.Select(e => e.skill), save.unlockedPermanentSkillIds);

            int found = 0;
            foreach (var entry in cardEntries)
            {
                bool has = discovered.Contains(entry.card.id);
                if (has) found++;
                // 피드백 2·3: 그림은 항상 칸에 — 획득이면 원색, 미발견이면 어두운 실루엣 + "미발견" 배지(이름·효과는 숨김 유지).
                if (entry.icon != null) entry.icon.color = has ? Color.white : SilhouetteColor;
                if (entry.lockBadge != null) entry.lockBadge.SetActive(!has);
                string category = CategoryLabel(entry.card.category);

                if (entry.card.category == CardCategory.Hybrid)
                {
                    // 07:167 — 하이브리드는 미발견이어도 이름과 해금 조건을 보여준다("???" 추측 게임 금지).
                    entry.nameText.text = entry.card.displayName;
                    entry.nameText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 0.85f, 0.6f);
                    entry.descText.text = has
                        ? EffectText(entry.card)
                        : "해금: 레이피어 시작 · 칵테일 3 + 차 3 누적";
                    entry.descText.color = has ? new Color(0.92f, 0.9f, 0.84f) : new Color(0.8f, 0.78f, 0.74f);
                    // 등급과 획득 상태를 분리: 왼쪽 = 등급(발견 후) / 분류, 오른쪽 = 발견함(그림 위 "미발견" 칩과 중복해 쓰지 않는다).
                    entry.flavorText.text = has ? $"등급 {RarityTable.Label(entry.card.rarity)}" : $"{category} 카드";
                    entry.flavorText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 0.86f, 0.6f, 0.85f);
                    entry.statusText.text = has ? FoundLabel : "";
                    entry.statusText.color = new Color(0.62f, 0.86f, 0.66f);
                    continue;
                }

                entry.nameText.text = has ? entry.card.displayName : "???";
                entry.nameText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 1f, 1f, 0.55f);

                // 그리드에는 짧은 게임 효과만 — 긴 설명·음료 지식·출처는 클릭 상세(피드백 2).
                entry.descText.text = has ? EffectText(entry.card) : LockedHint;
                entry.descText.color = has ? new Color(0.92f, 0.9f, 0.84f) : new Color(0.75f, 0.75f, 0.78f, 0.8f);

                entry.flavorText.text = has ? $"등급 {RarityTable.Label(entry.card.rarity)}" : $"{category} 카드";
                entry.flavorText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 0.86f, 0.6f, 0.85f);
                entry.statusText.text = has ? FoundLabel : "";
                entry.statusText.color = new Color(0.62f, 0.86f, 0.66f);
            }
            // 커피 노드: 저장된 id 중 실제 노드와 일치하는 것만 센다(알 수 없는 id는 표시·집계 제외, 저장 목록은 보존).
            var acquiredSkills = new HashSet<string>(save.discoveredSkillIds ?? new List<string>());
            int coffeeFound = 0;
            foreach (var entry in coffeeEntries)
            {
                bool has = acquiredSkills.Contains(entry.skill.id);
                entry.acquired = has;
                if (has) coffeeFound++;
                entry.nameText.text = $"{entry.skill.displayName}  ({SkillData.BranchLabel(entry.skill.branch)} {entry.skill.tier}단계)";
                entry.nameText.color = has ? new Color(0.95f, 0.8f, 0.55f) : new Color(1f, 1f, 1f, 0.6f);
                if (entry.icon != null) entry.icon.color = has ? Color.white : new Color(1f, 1f, 1f, 0.55f); // 커피 노드는 비밀 아님 — 흐림만
                // 효과(설명)와 해금 기록을 분리: 그림이 있으면 그림 위 칩에, 없으면 설명 아래 줄에.
                if (entry.recordChip != null)
                {
                    entry.descText.text = entry.skill.description;
                    entry.recordChipText.text = has ? RecordChipHas : RecordChipNone;
                    entry.recordChipText.color = has ? new Color(0.62f, 0.86f, 0.66f) : new Color(1f, 0.86f, 0.6f);
                }
                else entry.descText.text = $"{entry.skill.description}\n{(has ? CoffeeAcquiredLabel : CoffeeNotAcquiredLabel)}";
                entry.descText.color = has ? new Color(0.85f, 0.85f, 0.85f) : new Color(1f, 1f, 1f, 0.7f);
                entry.prereqText.text = (entry.skill.prerequisite != null
                    ? $"선행: {entry.skill.prerequisite.displayName}"
                    : "선행: 없음") + $" · 티: {JobStudyTheme.Name(entry.skill, Job.TeaSommelier)}";
            }

            int hiddenFound = 0;
            foreach (var entry in hiddenEntries)
            {
                bool has = discovered.Contains(entry.def.id);
                if (has) hiddenFound++;
                entry.nameText.text = has ? $"{entry.def.name}  ({HiddenSkillDatabase.TierLabel(entry.def.tier)})"
                    : $"???  ({HiddenSkillDatabase.TierLabel(entry.def.tier)})";
                entry.nameText.color = has ? new Color(0.75f, 0.9f, 1f) : new Color(1f, 1f, 1f, 0.45f);
                // 47: 직업별 조건 — 찻집 런은 칵테일 카드가 없어 차 전용 조건 또는 바텐더 전용
                entry.descText.text = has ? $"바텐더: {entry.def.ConditionFor(Job.Bartender)}\n티: {entry.def.ConditionFor(Job.TeaSommelier)}"
                    : $"힌트: {entry.def.HintFor(Job.Bartender)} / 티: {entry.def.HintFor(Job.TeaSommelier)}";
                entry.descText.color = new Color(0.75f, 0.75f, 0.75f, has ? 1f : 0.6f);
                entry.effectText.text = has ? entry.def.effect : "";
                entry.effectText.color = new Color(0.6f, 0.85f, 0.95f);
            }


            // 영구 강화는 카드와 달리 비밀이 아님(강화 화면에서 항상 전체 정보가 보임) — "???" 대신
            // 이름/효과를 그대로 보여주고 해금 여부만 색과 상태 문구로 구분한다.
            int permanentUnlocked = 0;
            foreach (var entry in skillEntries)
            {
                bool has = permanentTree.IsUnlocked(entry.skill);
                bool inactive = permanentTree.IsInactiveLegacy(entry.skill);
                if (has) permanentUnlocked++;
                entry.nameText.text = entry.skill.displayName;
                entry.nameText.color = has ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.75f);
                // 효과와 해금 상태를 두 줄로 — 해금 전 효과도 읽히도록 흐리기만 하지 않고 대비를 유지한다.
                entry.descText.text = entry.skill.description;
                entry.descText.color = has ? new Color(0.85f, 0.85f, 0.85f) : new Color(1f, 1f, 1f, 0.7f);
                entry.statusText.text = has ? "해금됨" : inactive ? "비활성(선행 확인 필요)" : $"미해금 · 비용 {entry.skill.cost}";
                entry.statusText.color = has ? new Color(0.6f, 1f, 0.7f) : inactive ? new Color(1f, 0.7f, 0.5f) : new Color(1f, 0.86f, 0.6f, 0.95f);
            }

            // 상단 집계: "발견"(카드·히든)과 "해금"(영구 강화)·"해금 기록"(직업 연구)을 다른 말로 구분하고 항목마다 이름을 붙인다.
            countLabel.text = $"카드 발견 {found}/{cardEntries.Count}  ·  히든 스킬 발견 {hiddenFound}/{hiddenEntries.Count}  ·  직업 연구 해금 기록 {coffeeFound}/{coffeeEntries.Count}  ·  영구 강화 해금 {permanentUnlocked}/{skillEntries.Count}";

            var discoveredVarieties = new HashSet<string>(save.discoveredMaterialVarietyIds);
            foreach (var entry in materialEntries)
            {
                var varieties = MaterialVarieties.For(entry.type);
                int foundVarieties = varieties.Count(v => discoveredVarieties.Contains(v.id));
                // 종류 수(발견/전체)와 누적량을 앞에: "칵테일 재료 6/6종 · 누적 3038개". 이름은 일부만 미리 보이고 전체는 눌러서(상세).
                entry.headerText.text = $"{entry.label} {foundVarieties}/{varieties.Count}종 · 누적 {entry.getCount(save)}개";
                entry.varietyText.text = VarietyPreview(varieties, discoveredVarieties);
            }
        }

        /// <summary>미리보기 이름 글자 예산(공백 포함, 590폭 2줄 안에서 "외 N종"까지 들어가는 양).</summary>
        const int VarietyPreviewChars = 15;

        /// <summary>발견한 품종 이름을 예산 안에서만 나열하고 나머지는 "외 N종", 미발견은 이름 없이 "미발견 N종". 이름이 다 안 보이면 상세 안내.</summary>
        static string VarietyPreview(List<MaterialVariety> varieties, HashSet<string> discovered)
        {
            var shown = new List<string>();
            int used = 0, found = 0;
            bool full = false;
            foreach (var v in varieties)
            {
                if (!discovered.Contains(v.id)) continue;
                found++;
                int cost = v.displayName.Length + (shown.Count > 0 ? 2 : 0);
                if (full || (shown.Count > 0 && used + cost > VarietyPreviewChars)) { full = true; continue; } // 순서대로 앞에서부터만 보인다
                shown.Add(v.displayName);
                used += cost;
            }
            int hidden = found - shown.Count, locked = varieties.Count - found;
            var sb = new System.Text.StringBuilder();
            if (shown.Count > 0) sb.Append("<color=#E8E8E8>").Append(string.Join(", ", shown)).Append("</color>");
            if (hidden > 0) sb.Append(" <color=#E8E8E8>외 ").Append(hidden).Append("종</color>");
            if (locked > 0) sb.Append(shown.Count > 0 ? " · " : "").Append("<color=#A09AA8>미발견 ").Append(locked).Append("종</color>");
            if (shown.Count == 0 && locked == 0) return "";
            sb.Append("  <color=#A09AA8>(눌러서 보기)</color>");
            return sb.ToString();
        }

        void ShowMaterialDetail(string label, IngredientType type, Func<SaveData, int> getCount)
        {
            var save = PermanentProgress.Current;
            var discovered = new HashSet<string>(save.discoveredMaterialVarietyIds);
            var varieties = MaterialVarieties.For(type);
            int found = varieties.Count(v => discovered.Contains(v.id));
            var sb = new System.Text.StringBuilder();
            sb.Append($"종류 {found}/{varieties.Count}종 발견 · 지금까지 누적 수집 {getCount(save)}개");
            foreach (var v in varieties)
            {
                // 미발견 품종은 이름·설명을 숨긴다(발견 전에는 ??? 하나만).
                if (discovered.Contains(v.id)) sb.Append($"\n· {v.displayName} — {v.flavorText}");
                else sb.Append($"\n· ??? — {LockedLabel}");
            }
            sb.Append("\n런에서 이 재료를 주울 때마다 종류가 무작위로 정해지고, 처음 나온 종류는 영구히 기록됩니다.");
            ShowDetail($"{label} 종류", sb.ToString());
        }

        /// <summary>2026-11-21 데스크톱 Esc: 항목 상세가 열려 있으면 상세만, 아니면 뒤로 버튼과 같은 경로로 닫는다. 처리했으면 true.</summary>
        public bool HandleEscape()
        {
            if (!IsOpen) return false;
            if (IsDetailOpen) HideDetail(); else Close();
            return true;
        }

        void Close()
        {
            HideDetail();
            root.SetActive(false);
            onClose?.Invoke();
        }

        // ── 08:95 항목 상세 ─────────────────────────────────────────────

        public bool IsDetailOpen => detailRoot != null && detailRoot.activeSelf;
        public string DetailTitleText => detailTitle != null ? detailTitle.text : "";
        public string DetailBodyText => detailBody != null ? detailBody.text : "";

        /// <summary>테스트/키보드 공용 — id로 행의 상세를 연다(행 버튼 onClick과 같은 경로).</summary>
        public bool OpenDetailFor(string id)
        {
            var row = content != null ? FindDeep(content, id) : null;
            var button = row != null ? row.GetComponent<Button>() : null;
            if (button == null) return false;
            button.onClick.Invoke();
            return true;
        }

        static Transform FindDeep(Transform parent, string id)
        {
            foreach (Transform t in parent)
                if (t.name == $"Row_{id}" || t.name == $"SkillRow_{id}" || t.name == $"CoffeeRow_{id}") return t;
            return null;
        }

        void MakeRowSelectable(RectTransform rowRt, System.Action onClick)
        {
            var bg = rowRt.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Square; // 단색 하이라이트(버튼 공용 아트를 입히지 않음)
            bg.color = new Color(1f, 1f, 1f, 0.03f);
            UiFactory.SkinCard(bg);
            if (UiFactory.IsCafe(bg, "ui_cafe_card")) bg.color = Color.white;
            var button = rowRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 4f);
            colors.selectedColor = new Color(0.4f, 1f, 1f, 6f);
            button.colors = colors;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() =>
            {
                detailReturnFocus = rowRt.gameObject;
                onClick();
            });
        }

        void BuildDetailPanel(RectTransform rootRt)
        {
            var shield = UiFactory.Stretch("DetailShield", rootRt);
            UiFactory.AddImage(shield, new Color(0,0,0,.8f));
            detailShield=shield.gameObject; detailShield.SetActive(false);
            // QA 2026-10-05(720p): 긴 설명의 마지막 줄이 닫기 버튼과 겹쳤다 — 패널을 키우고 본문 상자를 버튼 위에서 끝내며,
            // 본문 24(08:49)를 유지하려고 패널을 넓히고 빈 줄을 없앴다. 그래도 넘치면 잘라서 버튼 영역을 침범하지 않는다.
            var rt = UiFactory.Place("Detail", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f),
                new Vector2(DetailPanelWidth, DetailPanelHeight));
            detailRoot = rt.gameObject;
            // 2026-10-09 실제720: 알파0.98은 선형 색공간 UI 합성에서 뒤 행 글자가 비쳐 보였다 — 모달은 완전 불투명.
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.1f, 0.08f, 0.12f, 1f)));
            detailTitle = UiFactory.AddText(UiFactory.Place("Title", rt, new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(DetailPanelWidth - 80f, 50f)), "", 34, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));
            detailBody = UiFactory.AddText(UiFactory.Place("Body", rt, new Vector2(0.5f, 1f), new Vector2(0f, -DetailBodyTop),
                new Vector2(DetailPanelWidth - 80f, DetailPanelHeight - DetailBodyTop - DetailCloseReserved)), "",
                MinBodyFontSize, TextAnchor.MiddleCenter,
                new Color(0.92f, 0.92f, 0.92f));
            var scrollArea = UiFactory.Place("DetailScroll", rt, new Vector2(.5f,1),new Vector2(0,-DetailBodyTop),new Vector2(DetailPanelWidth-80,DetailPanelHeight-DetailBodyTop-DetailCloseReserved));
            UiFactory.AddImage(scrollArea,new Color(0,0,0,.1f));
            var viewport = UiFactory.Stretch("Viewport",scrollArea); viewport.gameObject.AddComponent<RectMask2D>();
            // 2026-11-15 피드백 10: 스크롤 내용 = 본문 글 + (카드면) 출처 영역. 출처는 작은 보조 글꼴·사이트명·주소 복사(CardSourceList).
            detailContent = UiFactory.Place("Content", viewport, new Vector2(.5f,1), Vector2.zero, new Vector2(DetailPanelWidth-80, 580));
            detailBody.rectTransform.SetParent(detailContent,false);
            detailBody.rectTransform.anchorMin = detailBody.rectTransform.anchorMax = detailBody.rectTransform.pivot = new Vector2(.5f,1);
            detailBody.rectTransform.anchoredPosition = Vector2.zero;
            detailBody.alignment = TextAnchor.UpperLeft; detailBody.supportRichText=false;
            detailScroll = scrollArea.gameObject.AddComponent<ScrollRect>(); detailScroll.content=detailContent;detailScroll.viewport=viewport;detailScroll.horizontal=false;detailScroll.scrollSensitivity=40;detailScroll.movementType=ScrollRect.MovementType.Clamped;

            var closeRt = UiFactory.Place("Close", rt, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(260f, 70f));
            var closeBg = UiFactory.AddImage(closeRt, new Color(0.3f, 0.32f, 0.36f));
            detailClose = closeRt.gameObject.AddComponent<Button>();
            detailClose.targetGraphic = closeBg;
            UiFactory.ApplyHoverColors(detailClose);
            detailClose.onClick.AddListener(HideDetail);
            UiFactory.AddText(UiFactory.Stretch("Label", closeRt), "닫기", 28, TextAnchor.MiddleCenter, Color.white);
            detailRoot.SetActive(false);
        }

        const float DetailPanelWidth = 1500f;
        const float DetailPanelHeight = 780f;
        /// <summary>본문 상자 위쪽 여백(제목 아래)과, 아래쪽에 닫기 버튼(30+70)과 간격 20을 위해 비워 두는 높이.</summary>
        const float DetailBodyTop = 80f;
        const float DetailCloseReserved = 120f;

        void ShowDetail(string title, string body, CardData sourcesOf = null)
        {
            detailTitle.text = title;
            detailBody.text = body;
            detailRoot.SetActive(true);
            detailShield.SetActive(true);
            detailShield.transform.SetAsLastSibling();
            detailRoot.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            float w = DetailPanelWidth - 80;
            float bodyH = Mathf.Ceil(detailBody.preferredHeight) + 24;
            detailBody.rectTransform.sizeDelta = new Vector2(w, bodyH);
            var oldSources = detailContent.Find("Sources");
            if (oldSources != null) { oldSources.SetParent(null, false); if (Application.isPlaying) Destroy(oldSources.gameObject); else DestroyImmediate(oldSources.gameObject); }
            DetailSourceButtons.Clear();
            float srcH = sourcesOf != null ? CardSourceList.Build(detailContent, -bodyH - 8, w, sourcesOf, DetailSourceButtons) : 0f;
            detailContent.sizeDelta = new Vector2(w, Mathf.Max(580, bodyH + (srcH > 0 ? 8 + srcH + 24 : 0)));
            detailScroll.verticalNormalizedPosition=1;
            detailClose.navigation=new Navigation {mode=Navigation.Mode.None};
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(detailClose.gameObject);
        }

        public void HideDetail()
        {
            if (detailRoot == null || !detailRoot.activeSelf) return;
            detailRoot.SetActive(false);
            detailShield.SetActive(false);
            if (EventSystem.current != null && detailReturnFocus != null)
                EventSystem.current.SetSelectedGameObject(detailReturnFocus);
        }

        /// <summary>07:167 미발견 카드는 이름·효과를 숨기고 카테고리만, 하이브리드는 해금 조건을 보인다.</summary>
        void ShowCardDetail(CardData card)
        {
            bool has = PermanentProgress.Current.discoveredCardIds.Contains(card.id);
            string category = card.category == CardCategory.Cocktail ? "칵테일" : card.category == CardCategory.Tea ? "차" : "하이브리드";
            if (!has && card.category != CardCategory.Hybrid)
            {
                // 피드백 3: 미획득도 안내 상세 — 획득 경로(카드 창)만 알리고 이름·효과는 공개하지 않는다.
                ShowDetail("???", $"{category} 카드 · {LockedLabel}\n{LockedHint}.\n획득 방법: 런에서 {(card.category == CardCategory.Tea ? "찻잎" : "칵테일 재료")} 게이지가 차면 열리는 카드 창에서 고르기 (이름·효과·음료 지식이 기록됨)");
                return;
            }
            if (!has)
            {
                ShowDetail($"{card.displayName} (미발견)", "해금 조건: 레이피어 시작, 이번 런 칵테일 누적 3 + 차 누적 3");
                return;
            }
            ShowDetail($"{card.displayName}  ({RarityTable.Label(card.rarity)})",
                CardEducationData.Body(card), card);
        }

        void ShowPermanentDetail(PermanentSkillData skill)
        {
            var tree = new PermanentSkillTree(skillEntries.Select(e => e.skill), PermanentProgress.Current.unlockedPermanentSkillIds);
            string state = tree.IsUnlocked(skill) ? "해금됨" : tree.IsInactiveLegacy(skill) ? "비활성(선행 확인 필요)" : "미해금";
            ShowDetail(skill.displayName, $"영구 강화 · 상태: {state} · 비용 {skill.cost}\n효과: {skill.description}\n{PermanentSkillView.Conditions(skill)}\n{PermanentSkillView.Target(skill)}\n다음 런부터 적용 · 해금이 늘수록 더 많은 적이 등장합니다.");
        }

        void ShowCoffeeDetail(SkillData skill)
        {
            bool has = (PermanentProgress.Current.discoveredSkillIds ?? new List<string>()).Contains(skill.id);
            string prereq = skill.prerequisite != null ? skill.prerequisite.displayName : "없음";
            ShowDetail(skill.displayName + " / " + JobStudyTheme.Name(skill, Job.TeaSommelier),
                $"직업 연구 · {SkillData.BranchLabel(skill.branch)} {skill.tier}단계 · 선행 {prereq}\n{(has ? CoffeeAcquiredLabel : CoffeeNotAcquiredLabel)}\n효과: {skill.description}\n연구 효과는 이번 런 동안 적용됩니다.");
        }

        void Update()
        {
            if (!IsDetailOpen) return;
            var kb=UnityEngine.InputSystem.Keyboard.current;
            var pad=UnityEngine.InputSystem.Gamepad.current;
            float direction=kb!=null && (kb.downArrowKey.isPressed || kb.pageDownKey.isPressed)?-1:
                kb!=null && (kb.upArrowKey.isPressed || kb.pageUpKey.isPressed)?1:0;
            if(pad!=null)direction+=pad.rightStick.ReadValue().y;
            float overflow=detailScroll.content.rect.height-detailScroll.viewport.rect.height;
            if(overflow>0)detailScroll.verticalNormalizedPosition+=direction*600*Time.unscaledDeltaTime/overflow;
        }
    }
}
