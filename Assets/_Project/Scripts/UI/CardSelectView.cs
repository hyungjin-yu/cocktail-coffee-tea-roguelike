using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>레벨업 시 카드 3장 제시. 클릭 또는 1/2/3 키로 선택. 등급 확률을 카드에 표기한다.</summary>
    public class CardSelectView : MonoBehaviour
    {
        const int SlotCount = 3;

        class Slot
        {
            public GameObject root;
            public Image background;
            public Text key;
            public Text rarity;
            public Text title;
            public Text description;
            public Text stacks;
            public Text odds;
        }

        readonly Slot[] slots = new Slot[SlotCount];
        readonly List<CardOffer> offers = new List<CardOffer>();
        GameObject root;
        Action<CardOffer> onChosen;

        public bool IsOpen => root != null && root.activeSelf;

        public static CardSelectView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("CardSelect", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.72f));

            var view = rootRt.gameObject.AddComponent<CardSelectView>();
            view.root = rootRt.gameObject;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 80f));
            UiFactory.AddText(title, "레벨업!  카드를 선택하세요", 56, TextAnchor.MiddleCenter, Color.white);

            for (int i = 0; i < SlotCount; i++)
                view.slots[i] = view.BuildSlot(rootRt, i);

            view.root.SetActive(false);
            return view;
        }

        Slot BuildSlot(Transform parent, int index)
        {
            var slot = new Slot();
            float x = (index - 1) * 480f;
            var rt = UiFactory.Place($"Card{index + 1}", parent, new Vector2(0.5f, 0.5f), new Vector2(x, -30f),
                new Vector2(430f, 620f));
            slot.root = rt.gameObject;
            slot.background = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = slot.background;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            int captured = index;
            button.onClick.AddListener(() => Choose(captured));

            slot.key = UiFactory.AddText(UiFactory.Place("Key", rt, new Vector2(0f, 1f), new Vector2(18f, -12f),
                new Vector2(80f, 40f)), $"[{index + 1}]", 30, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.5f));
            slot.rarity = UiFactory.AddText(UiFactory.Place("Rarity", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(380f, 44f)), "", 32, TextAnchor.MiddleCenter, Color.white);
            slot.title = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(390f, 110f)), "", 44, TextAnchor.MiddleCenter, Color.white);
            slot.description = UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(360f, 220f)), "", 32, TextAnchor.UpperCenter,
                new Color(0.92f, 0.92f, 0.92f));
            slot.stacks = UiFactory.AddText(UiFactory.Place("Stacks", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(380f, 40f)), "", 28, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.85f, 1f));
            slot.odds = UiFactory.AddText(UiFactory.Place("Odds", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(380f, 44f)), "", 28, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.6f));
            return slot;
        }

        public void Show(List<CardOffer> newOffers, Action<CardOffer> callback)
        {
            offers.Clear();
            offers.AddRange(newOffers);
            onChosen = callback;

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = slots[i];
                bool used = i < offers.Count;
                slot.root.SetActive(used);
                if (!used) continue;

                var offer = offers[i];
                var card = offer.card;
                var rarityColor = RarityTable.Color(card.rarity);

                slot.rarity.text = RarityTable.Label(card.rarity);
                slot.rarity.color = rarityColor;
                slot.title.text = card.displayName;
                slot.description.text = card.description;
                slot.stacks.text = offer.currentStacks == 0
                    ? "신규"
                    : $"Lv {offer.currentStacks} → {offer.currentStacks + 1}  (최대 {card.maxStacks})";
                slot.odds.text = $"{RarityTable.Label(card.rarity)} 등급 확률 {offer.rarityPercent:0.#}%";
                slot.background.color = Color.Lerp(new Color(0.16f, 0.13f, 0.2f), rarityColor, 0.18f);
            }

            root.SetActive(true);
        }

        void Update()
        {
            if (!IsOpen) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        /// <summary>인덱스의 카드를 선택. 이미 닫혔거나 범위 밖이면 무시한다.</summary>
        public void Choose(int index)
        {
            if (!IsOpen || onChosen == null || index < 0 || index >= offers.Count) return;

            var callback = onChosen;
            var offer = offers[index];
            onChosen = null;
            root.SetActive(false);
            callback(offer);
        }
    }
}
