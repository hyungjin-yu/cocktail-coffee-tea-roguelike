using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 지금까지 한 번이라도 획득한 카드를 모아보는 도감. 직업 선택 화면에서 여는 버튼으로 연다.
    /// 발견 여부는 런이 끝나도 사라지지 않고 영구 저장된다 (PermanentProgress.discoveredCardIds).
    /// 카드 하나당 이름+등급 / 효과 / 제목이 뭘 뜻하는지(풍미) 3줄을 보여주는데, 항목 수가 많아
    /// 화면 안에 다 안 들어가서 세로 스크롤 가능한 영역 안에 넣는다.
    /// </summary>
    public class CardCodexView : MonoBehaviour
    {
        class Entry
        {
            public CardData card;
            public Text nameText;
            public Text descText;
            public Text flavorText;
        }

        const float RowStep = 68f;
        const float RowNameHeight = 24f;
        const float RowDescHeight = 20f;
        const float RowFlavorHeight = 20f;
        const float ColumnWidth = 440f;
        const float ContentTopPadding = 40f; // 헤더 높이만큼
        const float ContentBottomPadding = 20f;

        readonly List<Entry> entries = new List<Entry>();
        GameObject root;
        RectTransform content;
        Text countLabel;
        Action onClose;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트용 — Show() 이후에만 채워진다.</summary>
        public int EntryCount => entries.Count;

        public static CardCodexView Create(Transform canvas, Action onClose)
        {
            var rootRt = UiFactory.Stretch("CardCodex", canvas);
            UiFactory.AddImage(rootRt, new Color(0.05f, 0.04f, 0.07f, 0.97f));

            var view = rootRt.gameObject.AddComponent<CardCodexView>();
            view.root = rootRt.gameObject;
            view.onClose = onClose;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(1400f, 60f));
            UiFactory.AddText(title, "카드 도감", 44, TextAnchor.MiddleCenter, Color.white);

            var countRt = UiFactory.Place("Count", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(900f, 36f));
            view.countLabel = UiFactory.AddText(countRt, "", 26, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.65f, 1f));

            // 스크롤 영역: 헤더 아래 ~ 뒤로 버튼 위 사이에만 배치, 안쪽은 RectMask2D로 잘라낸다.
            var scrollAreaRt = UiFactory.Place("ScrollArea", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -132f),
                new Vector2(1050f, 800f));
            var viewportRt = UiFactory.Stretch("Viewport", scrollAreaRt);
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 800f); // 실제 행 개수에 맞춰 BuildRows에서 다시 계산
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

        void BuildRows()
        {
            var all = CardDatabase.LoadAll();
            int cocktailCount = BuildColumn(all.Where(c => c.category == CardCategory.Cocktail), -260f, "칵테일");
            int teaCount = BuildColumn(all.Where(c => c.category == CardCategory.Tea), 260f, "차");

            int maxRows = Mathf.Max(cocktailCount, teaCount);
            float height = ContentTopPadding + maxRows * RowStep + ContentBottomPadding;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        int BuildColumn(IEnumerable<CardData> cards, float x, string headerLabel)
        {
            var ordered = cards.OrderBy(c => c.rarity).ThenBy(c => c.displayName).ToList();

            var header = UiFactory.Place($"Header_{headerLabel}", content, new Vector2(0.5f, 1f),
                new Vector2(x, 0f), new Vector2(ColumnWidth, ContentTopPadding));
            UiFactory.AddText(header, headerLabel, 30, TextAnchor.MiddleCenter, new Color(0.9f, 0.7f, 0.5f));

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

                entries.Add(new Entry { card = card, nameText = nameText, descText = descText, flavorText = flavorText });
            }

            return ordered.Count;
        }

        public void Show()
        {
            if (entries.Count == 0) BuildRows();
            Refresh();
            root.SetActive(true);
        }

        void Refresh()
        {
            var discovered = new HashSet<string>(PermanentProgress.Current.discoveredCardIds);
            int found = 0;
            foreach (var entry in entries)
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
            countLabel.text = $"발견한 카드: {found} / {entries.Count}";
        }

        void Close()
        {
            root.SetActive(false);
            onClose?.Invoke();
        }
    }
}
