using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 지금까지 모은 것들을 모아보는 도감 — 카드(칵테일/차), 영구 강화 노드, 재료 누적 수집량.
    /// 직업 선택 화면에서 여는 버튼으로 연다. 전부 런이 끝나도 사라지지 않고 영구 저장된 값을 보여준다
    /// (PermanentProgress). 카드/영구 강화는 항목 수가 많아 세로 스크롤 가능한 영역 안에 넣는다.
    /// </summary>
    public class CardCodexView : MonoBehaviour
    {
        class CardEntry
        {
            public CardData card;
            public Text nameText;
            public Text descText;
            public Text flavorText;
        }

        class SkillEntry
        {
            public PermanentSkillData skill;
            public Text nameText;
            public Text descText;
        }

        class MaterialEntry
        {
            public string label;
            public Func<SaveData, int> getCount;
            public Text text;
        }

        const float RowStep = 68f;
        const float RowNameHeight = 24f;
        const float RowDescHeight = 20f;
        const float RowFlavorHeight = 20f;
        const float ColumnWidth = 420f;
        const float ColumnSpacing = 460f;
        const float ContentTopPadding = 40f; // 헤더 높이만큼
        const float ContentBottomPadding = 20f;

        readonly List<CardEntry> cardEntries = new List<CardEntry>();
        readonly List<SkillEntry> skillEntries = new List<SkillEntry>();
        readonly List<MaterialEntry> materialEntries = new List<MaterialEntry>();

        GameObject root;
        RectTransform content;
        Text countLabel;
        Action onClose;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트용 — Show() 이후에만 채워진다 (카드 항목 수).</summary>
        public int EntryCount => cardEntries.Count;
        /// <summary>테스트용 — Show() 이후에만 채워진다 (영구 강화 항목 수).</summary>
        public int SkillEntryCount => skillEntries.Count;

        public static CardCodexView Create(Transform canvas, Action onClose)
        {
            var rootRt = UiFactory.Stretch("CardCodex", canvas);
            UiFactory.AddImage(rootRt, new Color(0.05f, 0.04f, 0.07f, 0.97f));

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

            // 스크롤 영역: 재료 줄 아래 ~ 뒤로 버튼 위 사이에만 배치, 안쪽은 RectMask2D로 잘라낸다.
            var scrollAreaRt = UiFactory.Place("ScrollArea", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -222f),
                new Vector2(1450f, 710f));
            var viewportRt = UiFactory.Stretch("Viewport", scrollAreaRt);
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 710f); // 실제 행 개수에 맞춰 BuildRows에서 다시 계산
            view.content = contentRt;

            var scrollRect = scrollAreaRt.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            var backRt = UiFactory.Place("BackButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 36f),
                new Vector2(320f, 66f));
            var backBg = UiFactory.AddImage(backRt, new Color(0.5f, 0.2f, 0.55f));
            var backButton = backRt.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backBg;
            backButton.onClick.AddListener(view.Close);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 30, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        void BuildMaterialsRow(RectTransform rootRt)
        {
            var rowRt = UiFactory.Place("Materials", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                new Vector2(1450f, 76f));

            AddMaterialSlot(rowRt, -480f, "Icons/cocktail_ingredient", "칵테일 재료", s => s.cocktailIngredientsCollected);
            AddMaterialSlot(rowRt, 0f, "Icons/tea", "찻잎", s => s.teaLeavesCollected);
            AddMaterialSlot(rowRt, 480f, "Icons/coffee_bean", "커피 원두", s => s.coffeeBeansCollected);
        }

        void AddMaterialSlot(RectTransform parent, float x, string iconPath, string label, Func<SaveData, int> getCount)
        {
            var slotRt = UiFactory.Place($"Material_{label}", parent, new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(420f, 72f));

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

            var textRt = UiFactory.Place("Text", slotRt, new Vector2(0f, 0.5f), new Vector2(80f, 0f),
                new Vector2(330f, 64f));
            var text = UiFactory.AddText(textRt, "", 22, TextAnchor.MiddleLeft, new Color(0.92f, 0.92f, 0.92f));

            materialEntries.Add(new MaterialEntry { label = label, getCount = getCount, text = text });
        }

        void BuildRows()
        {
            var all = CardDatabase.LoadAll();
            int cocktailCount = BuildCardColumn(all.Where(c => c.category == CardCategory.Cocktail), -ColumnSpacing, "칵테일");
            int teaCount = BuildCardColumn(all.Where(c => c.category == CardCategory.Tea), 0f, "차");
            int skillCount = BuildSkillColumn(PermanentSkillDatabase.LoadAll(), ColumnSpacing, "영구 강화");

            int maxRows = Mathf.Max(cocktailCount, Mathf.Max(teaCount, skillCount));
            float height = ContentTopPadding + maxRows * RowStep + ContentBottomPadding;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        int BuildCardColumn(IEnumerable<CardData> cards, float x, string headerLabel)
        {
            var ordered = cards.OrderBy(c => c.rarity).ThenBy(c => c.displayName).ToList();
            BuildHeader(x, headerLabel);

            for (int i = 0; i < ordered.Count; i++)
            {
                var card = ordered[i];
                var rowRt = UiFactory.Place($"Row_{card.id}", content, new Vector2(0.5f, 1f),
                    new Vector2(x, -ContentTopPadding - i * RowStep), new Vector2(ColumnWidth, RowStep));

                var nameText = UiFactory.AddText(UiFactory.Place("Name", rowRt, new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(ColumnWidth, RowNameHeight)), "", 22, TextAnchor.MiddleLeft,
                    Color.white);
                var descText = UiFactory.AddText(UiFactory.Place("Desc", rowRt, new Vector2(0.5f, 1f),
                    new Vector2(0f, -RowNameHeight), new Vector2(ColumnWidth, RowDescHeight)), "", 17,
                    TextAnchor.MiddleLeft, Color.white);
                var flavorText = UiFactory.AddText(UiFactory.Place("Flavor", rowRt, new Vector2(0.5f, 1f),
                    new Vector2(0f, -RowNameHeight - RowDescHeight), new Vector2(ColumnWidth, RowFlavorHeight)),
                    "", 14, TextAnchor.MiddleLeft, Color.white);

                cardEntries.Add(new CardEntry { card = card, nameText = nameText, descText = descText, flavorText = flavorText });
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

                var icon = string.IsNullOrEmpty(skill.iconResourcePath)
                    ? null : Resources.Load<Sprite>(skill.iconResourcePath);
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

                var nameText = UiFactory.AddText(UiFactory.Place("Name", rowRt, new Vector2(0f, 1f),
                    new Vector2(textLeft, 0f), new Vector2(ColumnWidth - textLeft, RowNameHeight)), "", 20,
                    TextAnchor.MiddleLeft, Color.white);
                var descText = UiFactory.AddText(UiFactory.Place("Desc", rowRt, new Vector2(0f, 1f),
                    new Vector2(textLeft, -RowNameHeight - RowDescHeight * 0.5f),
                    new Vector2(ColumnWidth - textLeft, RowDescHeight)), "", 16, TextAnchor.MiddleLeft, Color.white);

                skillEntries.Add(new SkillEntry { skill = skill, nameText = nameText, descText = descText });
            }

            return ordered.Count;
        }

        void BuildHeader(float x, string headerLabel)
        {
            var header = UiFactory.Place($"Header_{headerLabel}", content, new Vector2(0.5f, 1f),
                new Vector2(x, 0f), new Vector2(ColumnWidth, ContentTopPadding));
            UiFactory.AddText(header, headerLabel, 30, TextAnchor.MiddleCenter, new Color(0.9f, 0.7f, 0.5f));
        }

        public void Show()
        {
            if (cardEntries.Count == 0 && skillEntries.Count == 0) BuildRows();
            Refresh();
            root.SetActive(true);
        }

        void Refresh()
        {
            var save = PermanentProgress.Current;
            var discovered = new HashSet<string>(save.discoveredCardIds);
            var unlocked = new HashSet<string>(save.unlockedPermanentSkillIds);

            int found = 0;
            foreach (var entry in cardEntries)
            {
                bool has = discovered.Contains(entry.card.id);
                if (has) found++;

                entry.nameText.text = has
                    ? $"{entry.card.displayName}  ({RarityTable.Label(entry.card.rarity)})"
                    : "???";
                entry.nameText.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 1f, 1f, 0.35f);

                entry.descText.text = has ? entry.card.description : "";
                entry.descText.color = new Color(0.75f, 0.75f, 0.75f, has ? 1f : 0.35f);

                entry.flavorText.text = has ? entry.card.flavorText : "";
                entry.flavorText.color = new Color(0.6f, 0.58f, 0.65f, has ? 1f : 0.35f);
            }
            countLabel.text = $"발견한 카드: {found} / {cardEntries.Count}";

            // 영구 강화는 카드와 달리 비밀이 아님(강화 화면에서 항상 전체 정보가 보임) — "???" 대신
            // 이름/효과를 그대로 보여주고 해금 여부만 색과 상태 문구로 구분한다.
            foreach (var entry in skillEntries)
            {
                bool has = unlocked.Contains(entry.skill.id);
                entry.nameText.text = entry.skill.displayName;
                entry.nameText.color = has ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.6f);
                entry.descText.text = $"{entry.skill.description}  ·  {(has ? "해금됨" : "미해금")}";
                entry.descText.color = has ? new Color(0.75f, 0.75f, 0.75f) : new Color(1f, 1f, 1f, 0.35f);
            }

            foreach (var entry in materialEntries)
                entry.text.text = $"{entry.label}\n총 {entry.getCount(save)}개 수집";
        }

        void Close()
        {
            root.SetActive(false);
            onClose?.Invoke();
        }
    }
}
