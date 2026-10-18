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
            public Image icon;
        }

        class SkillEntry
        {
            public PermanentSkillData skill;
            public Text nameText;
            public Text descText;
        }

        /// <summary>07:169 커피 노드 — 처음부터 이름/효과/선행 표시, "최초 획득" 기록만 구분(카드의 "???" 미발견, 영구의
        /// "미해금"과 다른 문구).</summary>
        class CoffeeEntry
        {
            public SkillData skill;
            public Text nameText;
            public Text descText;
            public Text prereqText;
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
            return entry == null ? (null, null, null) : (entry.nameText.text, entry.descText.text, entry.prereqText.text);
        }
        /// <summary>테스트용 — 영구 강화 항목 설명(상태 포함).</summary>
        public string GetPermanentText(string skillId) => skillEntries.Find(e => e.skill.id == skillId)?.descText.text;
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
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 30, TextAnchor.MiddleCenter, Color.white);

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
                label = label, type = type, getCount = getCount, headerText = headerText, varietyText = varietyText,
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

                var iconRt = UiFactory.Place("EducationIcon", rowRt, new Vector2(1,1), new Vector2(-8,-5), new Vector2(38,38));
                var cardIcon = iconRt.gameObject.AddComponent<Image>(); cardIcon.preserveAspect = true; cardIcon.raycastTarget = false;
                cardIcon.sprite = CardEducationIcons.For(card);
                var capturedCard = card;
                MakeRowSelectable(rowRt, () => ShowCardDetail(capturedCard));
                var nameText = RowText(rowRt, "Name", -24f, 0f, ColumnWidth - 48f, RowNameHeight, RowNameFontSize, TextAnchor.MiddleLeft);
                var descText = RowText(rowRt, "Desc", 0f, -RowNameHeight, ColumnWidth, RowDescHeight, MinBodyFontSize,
                    TextAnchor.UpperLeft);
                var flavorText = RowText(rowRt, "Flavor", 0f, -RowNameHeight - RowDescHeight, ColumnWidth, RowFlavorHeight,
                    MinBodyFontSize, TextAnchor.MiddleLeft);

                cardEntries.Add(new CardEntry
                {
                    card = card, icon = cardIcon, nameText = nameText, descText = descText, flavorText = flavorText,
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
                    RowDescHeight + RowFlavorHeight, MinBodyFontSize, TextAnchor.UpperLeft, true);

                skillEntries.Add(new SkillEntry { skill = skill, nameText = nameText, descText = descText });
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
                var nameText = RowText(rowRt, "Name", 0f, 0f, ColumnWidth, RowNameHeight, RowNameFontSize, TextAnchor.MiddleLeft);
                var descText = RowText(rowRt, "Desc", 0f, -RowNameHeight, ColumnWidth, RowDescHeight, MinBodyFontSize,
                    TextAnchor.UpperLeft);
                var prereqText = RowText(rowRt, "Prereq", 0f, -RowNameHeight - RowDescHeight, ColumnWidth, RowFlavorHeight,
                    MinBodyFontSize, TextAnchor.MiddleLeft);
                prereqText.color = new Color(0.7f, 0.68f, 0.75f);
                coffeeEntries.Add(new CoffeeEntry { skill = skill, nameText = nameText, descText = descText, prereqText = prereqText });
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
                if (entry.icon != null) entry.icon.gameObject.SetActive(has);
                if (has) found++;

                if (entry.card.category == CardCategory.Hybrid)
                {
                    // 07:167 — 하이브리드는 미발견이어도 이름과 해금 조건을 보여준다("???" 추측 게임 금지).
                    entry.nameText.text = $"{entry.card.displayName}  ({RarityTable.Label(entry.card.rarity)})";
                    entry.nameText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 0.85f, 0.6f);
                    entry.descText.text = has
                        ? entry.card.description
                        : "해금 조건: 레이피어 시작, 이번 런 칵테일 누적 3 + 차 누적 3";
                    entry.descText.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                    entry.flavorText.text = has ? entry.card.flavorText : "";
                    entry.flavorText.color = new Color(0.6f, 0.58f, 0.65f, has ? 1f : 0.35f);
                    continue;
                }

                entry.nameText.text = has
                    ? $"{entry.card.displayName}  ({RarityTable.Label(entry.card.rarity)})"
                    : "???";
                entry.nameText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 1f, 1f, 0.35f);

                entry.descText.text = has ? entry.card.description : "";
                entry.descText.color = new Color(0.75f, 0.75f, 0.75f, has ? 1f : 0.35f);

                entry.flavorText.text = has ? entry.card.flavorText : "";
                entry.flavorText.color = new Color(0.6f, 0.58f, 0.65f, has ? 1f : 0.35f);
            }
            // 커피 노드: 저장된 id 중 실제 노드와 일치하는 것만 센다(알 수 없는 id는 표시·집계 제외, 저장 목록은 보존).
            var acquiredSkills = new HashSet<string>(save.discoveredSkillIds ?? new List<string>());
            int coffeeFound = 0;
            foreach (var entry in coffeeEntries)
            {
                bool has = acquiredSkills.Contains(entry.skill.id);
                if (has) coffeeFound++;
                entry.nameText.text = $"{entry.skill.displayName}  ({SkillData.BranchLabel(entry.skill.branch)} {entry.skill.tier}단계)";
                entry.nameText.color = has ? new Color(0.95f, 0.8f, 0.55f) : new Color(1f, 1f, 1f, 0.6f);
                entry.descText.text = $"티 소믈리에: {JobStudyTheme.Name(entry.skill, Job.TeaSommelier)}\n{entry.skill.description}  ·  {(has ? CoffeeAcquiredLabel : CoffeeNotAcquiredLabel)}";
                entry.descText.color = has ? new Color(0.8f, 0.8f, 0.8f) : new Color(1f, 1f, 1f, 0.4f);
                entry.prereqText.text = entry.skill.prerequisite != null
                    ? $"선행: {entry.skill.prerequisite.displayName}"
                    : "선행: 없음";
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

            countLabel.text = $"발견한 카드: {found} / {cardEntries.Count}   ·   연구 최초 획득: {coffeeFound} / {coffeeEntries.Count}   ·   히든: {hiddenFound} / {hiddenEntries.Count}";

            // 영구 강화는 카드와 달리 비밀이 아님(강화 화면에서 항상 전체 정보가 보임) — "???" 대신
            // 이름/효과를 그대로 보여주고 해금 여부만 색과 상태 문구로 구분한다.
            foreach (var entry in skillEntries)
            {
                bool has = permanentTree.IsUnlocked(entry.skill);
                bool inactive = permanentTree.IsInactiveLegacy(entry.skill);
                entry.nameText.text = entry.skill.displayName;
                entry.nameText.color = has ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.6f);
                entry.descText.text = $"{entry.skill.description}  ·  {(has ? "해금됨" : inactive ? "비활성(선행 확인 필요)" : "미해금")}";
                entry.descText.color = has ? new Color(0.75f, 0.75f, 0.75f) : new Color(1f, 1f, 1f, 0.35f);
            }

            var discoveredVarieties = new HashSet<string>(save.discoveredMaterialVarietyIds);
            foreach (var entry in materialEntries)
            {
                var varieties = MaterialVarieties.For(entry.type);
                int foundVarieties = varieties.Count(v => discoveredVarieties.Contains(v.id));
                entry.headerText.text = $"{entry.label} — 총 {entry.getCount(save)}개 수집 ({foundVarieties}/{varieties.Count}종)";

                var names = varieties.Select(v => discoveredVarieties.Contains(v.id)
                    ? $"<color=#E8E8E8>{v.displayName}</color>"
                    : "<color=#6A6A6A>???</color>");
                entry.varietyText.text = string.Join(", ", names);
            }
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
            detailBody.rectTransform.SetParent(viewport,false);
            detailBody.rectTransform.anchorMin = detailBody.rectTransform.anchorMax = detailBody.rectTransform.pivot = new Vector2(.5f,1);
            detailBody.rectTransform.anchoredPosition = Vector2.zero;
            detailBody.alignment = TextAnchor.UpperLeft; detailBody.supportRichText=false;
            detailScroll = scrollArea.gameObject.AddComponent<ScrollRect>(); detailScroll.content=detailBody.rectTransform;detailScroll.viewport=viewport;detailScroll.horizontal=false;detailScroll.scrollSensitivity=40;detailScroll.movementType=ScrollRect.MovementType.Clamped;

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

        void ShowDetail(string title, string body)
        {
            detailTitle.text = title;
            detailBody.text = body;
            detailRoot.SetActive(true);
            detailShield.SetActive(true);
            detailShield.transform.SetAsLastSibling();
            detailRoot.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            detailBody.rectTransform.sizeDelta=new Vector2(DetailPanelWidth-80,Mathf.Max(580,detailBody.preferredHeight+24));
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
                ShowDetail("???", $"{category} 카드 · 미발견\n런에서 처음 획득하면 이름과 효과가 기록됩니다.");
                return;
            }
            if (!has)
            {
                ShowDetail($"{card.displayName} (미발견)", "해금 조건: 레이피어 시작, 이번 런 칵테일 누적 3 + 차 누적 3");
                return;
            }
            ShowDetail($"{card.displayName}  ({RarityTable.Label(card.rarity)})",
                CardEducationData.Detail(card));
        }

        void ShowPermanentDetail(PermanentSkillData skill)
        {
            var tree = new PermanentSkillTree(skillEntries.Select(e => e.skill), PermanentProgress.Current.unlockedPermanentSkillIds);
            string state = tree.IsUnlocked(skill) ? "해금됨" : tree.IsInactiveLegacy(skill) ? "비활성(선행 확인 필요)" : "미해금";
            ShowDetail(skill.displayName, $"영구 강화 · 비용 {skill.cost}\n{skill.description}\n{PermanentSkillView.Conditions(skill)}\n{PermanentSkillView.Target(skill)}\n다음 런부터 적용 · 해금이 늘수록 더 많은 적이 등장합니다.\n{state}");
        }

        void ShowCoffeeDetail(SkillData skill)
        {
            bool has = (PermanentProgress.Current.discoveredSkillIds ?? new List<string>()).Contains(skill.id);
            string prereq = skill.prerequisite != null ? skill.prerequisite.displayName : "없음";
            ShowDetail(skill.displayName + " / " + JobStudyTheme.Name(skill, Job.TeaSommelier),
                $"직업 연구 · {SkillData.BranchLabel(skill.branch)} {skill.tier}단계 · 선행 {prereq}\n{skill.description}\n연구 효과는 이번 런 동안 적용됩니다.\n{(has ? CoffeeAcquiredLabel : CoffeeNotAcquiredLabel)}");
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
