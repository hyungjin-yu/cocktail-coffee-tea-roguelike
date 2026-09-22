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
    /// </summary>
    public class CardCodexView : MonoBehaviour
    {
        class Entry
        {
            public CardData card;
            public Text text;
        }

        readonly List<Entry> entries = new List<Entry>();
        GameObject root;
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

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(1400f, 60f));
            UiFactory.AddText(title, "카드 도감", 48, TextAnchor.MiddleCenter, Color.white);

            var countRt = UiFactory.Place("Count", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -112f),
                new Vector2(900f, 40f));
            view.countLabel = UiFactory.AddText(countRt, "", 28, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.65f, 1f));

            var backRt = UiFactory.Place("BackButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(320f, 70f));
            var backBg = UiFactory.AddImage(backRt, new Color(0.5f, 0.2f, 0.55f));
            var backButton = backRt.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backBg;
            backButton.onClick.AddListener(view.Close);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 32, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        void BuildRows()
        {
            var all = CardDatabase.LoadAll();
            BuildColumn(all.Where(c => c.category == CardCategory.Cocktail), -260f, "칵테일");
            BuildColumn(all.Where(c => c.category == CardCategory.Tea), 260f, "차");
        }

        void BuildColumn(IEnumerable<CardData> cards, float x, string headerLabel)
        {
            var ordered = cards.OrderBy(c => c.rarity).ThenBy(c => c.displayName).ToList();

            var header = UiFactory.Place($"Header_{headerLabel}", root.transform, new Vector2(0.5f, 1f),
                new Vector2(x, -160f), new Vector2(440f, 40f));
            UiFactory.AddText(header, headerLabel, 32, TextAnchor.MiddleCenter, new Color(0.9f, 0.7f, 0.5f));

            for (int i = 0; i < ordered.Count; i++)
            {
                var rowRt = UiFactory.Place($"Row_{ordered[i].id}", root.transform, new Vector2(0.5f, 1f),
                    new Vector2(x, -206f - i * 40f), new Vector2(440f, 36f));
                var text = UiFactory.AddText(rowRt, "", 26, TextAnchor.MiddleLeft, Color.white);
                entries.Add(new Entry { card = ordered[i], text = text });
            }
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
                entry.text.text = has
                    ? $"{entry.card.displayName}  ({RarityTable.Label(entry.card.rarity)})"
                    : "???";
                entry.text.color = has ? RarityTable.Color(entry.card.rarity) : new Color(1f, 1f, 1f, 0.35f);
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
