using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 치장 스킨 상점(타이틀 → "스킨"). 스킨마다 미리보기(소유 전에도 스킨 아트 표시, 없으면 기본 아트 + "아트 준비 중"),
    /// 스토어 현지화 가격의 구매 버튼, 장착/해제, 하단 "구매 복원"·"뒤로". 가격은 코드에 없다 — 스토어 값이 없으면
    /// "가격 정보를 불러올 수 없음" + 구매 비활성. 결제 꺼짐/미지원 플랫폼이면 구매·복원 비활성 + 안내 문구.
    /// 버튼은 터치용 120px 높이, 마우스/키보드(EventSystem 자동 내비게이션) 공용. 닫기 버튼 이름은 BackButton(안드로이드 뒤로 키 라우터).
    /// </summary>
    public class SkinShopView : MonoBehaviour
    {
        class Card
        {
            public SkinDefinition def;
            public Image preview;
            public Image basePreview;
            public Text artNote;
            public Text state;
            public Button buy;
            public Text buyLabel;
            public Button equip;
            public Text equipLabel;
        }

        GameObject root;
        Action onClose;
        Func<SkinCommerce> commerceSource;
        SkinCommerce commerce;
        readonly List<Card> cards = new List<Card>();
        Button restoreButton;
        Button backButton;
        Text globalText;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트용.</summary>
        public Button BackButton => backButton;
        public Button RestoreButton => restoreButton;
        public Button BuyButton(string productId) => Find(productId)?.buy;
        public Button EquipButton(string productId) => Find(productId)?.equip;
        public string StateText(string productId) => Find(productId)?.state.text;
        public Sprite PreviewSprite(string productId) => Find(productId)?.preview.sprite;
        public bool ArtNoteVisible(string productId) => Find(productId)?.artNote.gameObject.activeSelf ?? false;
        public string GlobalText => globalText != null ? globalText.text : "";

        Card Find(string productId)
        {
            foreach (var c in cards) if (c.def.productId == productId) return c;
            return null;
        }

        /// <param name="commerceOverride">테스트에서 주입. null이면 SkinCommerce.Shared.</param>
        public static SkinShopView Create(Transform canvas, Action onClose, Func<SkinCommerce> commerceOverride = null)
        {
            var rootRt = UiFactory.Stretch("SkinShop", canvas);
            var view = rootRt.gameObject.AddComponent<SkinShopView>();
            view.root = rootRt.gameObject;
            view.onClose = onClose;
            view.commerceSource = commerceOverride ?? (() => SkinCommerce.Shared);
            view.Build(rootRt);
            view.root.SetActive(false);
            return view;
        }

        void Build(RectTransform rootRt)
        {
            UiFactory.AddImage(rootRt, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(rootRt);

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 90f));
            UiFactory.AddText(title, "스킨", 54, TextAnchor.MiddleCenter, Color.white);
            var sub = UiFactory.Place("Subtitle", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1400f, 44f));
            UiFactory.AddText(sub, "외형만 바뀌는 치장 아이템입니다. 능력치·밸런스에는 영향이 없습니다.", 26, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.85f, 0.9f));

            float x = -420f;
            foreach (var def in SkinCatalog.All)
            {
                cards.Add(BuildCard(rootRt, def, x));
                x += 840f;
            }

            globalText = UiFactory.AddText(UiFactory.Place("GlobalMessage", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 190f),
                new Vector2(1600f, 44f)), "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.7f));

            restoreButton = MakeButton(rootRt, "RestoreButton", "구매 복원", new Vector2(0.5f, 0f), new Vector2(-230f, 50f),
                new Vector2(420f, 120f), new Color(0.24f, 0.4f, 0.5f), out _, OnRestore);
            backButton = MakeButton(rootRt, "BackButton", "뒤로", new Vector2(0.5f, 0f), new Vector2(230f, 50f),
                new Vector2(420f, 120f), new Color(0.3f, 0.32f, 0.36f), out _, Hide, "ui_cancel");
        }

        Card BuildCard(RectTransform parent, SkinDefinition def, float x)
        {
            var card = new Card { def = def };
            var rt = UiFactory.Place("Skin_" + def.productId, parent, new Vector2(0.5f, 0.5f), new Vector2(x, 30f), new Vector2(780f, 640f));
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.12f, 0.1f, 0.13f, 0.96f)));

            UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(720f, 56f)),
                def.displayName, 38, TextAnchor.MiddleCenter, Color.white);

            // 미리보기: 기본(작게) → 스킨(크게). 소유 전에도 보인다.
            var baseRt = UiFactory.Place("BasePreview", rt, new Vector2(0.5f, 1f), new Vector2(-230f, -110f), new Vector2(180f, 180f));
            card.basePreview = UiFactory.AddImage(baseRt, Color.white);
            card.basePreview.preserveAspect = true;
            card.basePreview.raycastTarget = false;
            UiFactory.AddText(UiFactory.Place("BaseLabel", rt, new Vector2(0.5f, 1f), new Vector2(-230f, -292f), new Vector2(200f, 34f)),
                "기본", 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.8f));

            var prevRt = UiFactory.Place("Preview", rt, new Vector2(0.5f, 1f), new Vector2(70f, -90f), new Vector2(260f, 260f));
            card.preview = UiFactory.AddImage(prevRt, Color.white);
            card.preview.preserveAspect = true;
            card.preview.raycastTarget = false;
            card.artNote = UiFactory.AddText(UiFactory.Place("ArtNote", rt, new Vector2(0.5f, 1f), new Vector2(70f, -352f), new Vector2(300f, 34f)),
                "아트 준비 중", 22, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.5f));

            UiFactory.AddText(UiFactory.Place("Description", rt, new Vector2(0.5f, 1f), new Vector2(0f, -388f), new Vector2(720f, 60f)),
                def.description, 22, TextAnchor.UpperCenter, new Color(0.88f, 0.88f, 0.9f));

            card.state = UiFactory.AddText(UiFactory.Place("State", rt, new Vector2(0.5f, 0f), new Vector2(0f, 146f), new Vector2(720f, 40f)),
                "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.75f));

            card.buy = MakeButton(rt, "BuyButton", "구매", new Vector2(0.5f, 0f), new Vector2(-180f, 16f), new Vector2(340f, 120f),
                new Color(0.9f, 0.35f, 0.75f), out card.buyLabel, () => OnBuy(def.productId));
            card.equip = MakeButton(rt, "EquipButton", "장착", new Vector2(0.5f, 0f), new Vector2(180f, 16f), new Vector2(340f, 120f),
                new Color(0.4f, 0.24f, 0.5f), out card.equipLabel, () => OnEquip(def));
            return card;
        }

        static Button MakeButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color,
            out Text labelText, Action onClick, string sfx = "ui_confirm")
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            var bg = UiFactory.AddImage(rt, color);
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => { AudioService.PlaySfx(sfx); onClick(); });
            labelText = UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 30, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        public void Show()
        {
            root.SetActive(true);
            commerce = commerceSource();
            commerce.Changed -= Refresh;
            commerce.Changed += Refresh;
            commerce.RetryNow(); // 상점 열기 = 보류 구매 재시도 + 끊긴 스토어 재연결
            foreach (var c in cards)
            {
                var baseSprite = SkinArt.BaseIdleSprite(c.def.job);
                var skin = SkinArt.PreviewSprite(c.def);
                c.basePreview.sprite = baseSprite;
                c.basePreview.enabled = baseSprite != null;
                c.preview.sprite = skin != null ? skin : baseSprite;
                c.preview.enabled = c.preview.sprite != null;
                c.artNote.gameObject.SetActive(skin == null);
            }
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FirstSelectable());
        }

        GameObject FirstSelectable()
        {
            foreach (var c in cards)
            {
                if (c.buy.interactable) return c.buy.gameObject;
                if (c.equip.interactable) return c.equip.gameObject;
            }
            return backButton.gameObject;
        }

        public void Hide()
        {
            if (commerce != null) commerce.Changed -= Refresh;
            root.SetActive(false);
            onClose?.Invoke();
        }

        void OnDestroy()
        {
            if (commerce != null) commerce.Changed -= Refresh;
        }

        void OnBuy(string productId)
        {
            commerce?.Buy(productId);
            Refresh();
        }

        void OnEquip(SkinDefinition def)
        {
            if (commerce == null) return;
            if (commerce.IsEquipped(def.productId)) commerce.Unequip(def.job);
            else commerce.Equip(def.productId);
            Refresh();
        }

        void OnRestore()
        {
            commerce?.Restore();
            Refresh();
        }

        public void Refresh()
        {
            if (commerce == null || root == null) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var c in cards)
            {
                var id = c.def.productId;
                bool owned = commerce.IsOwned(id);
                bool equipped = commerce.IsEquipped(id);
                var price = commerce.PriceText(id);
                c.buy.interactable = commerce.CanBuy(id);
                c.buyLabel.text = owned ? "보유 중" : price != null ? "구매  " + price : "구매";
                c.equip.interactable = owned;
                c.equipLabel.text = equipped ? "장착 해제" : owned ? "장착" : "장착(미보유)";
                var msg = commerce.MessageFor(id);
                var stateLine = equipped ? "장착 중" : commerce.BuyStateText(id);
                if (!owned && !commerce.PurchasesEnabled) { stateLine = "미보유"; msg = null; }
                c.state.text = string.IsNullOrEmpty(msg) || msg == stateLine ? stateLine : stateLine + " · " + msg;
            }
            restoreButton.interactable = commerce.PurchasesEnabled;
            globalText.text = commerce.GlobalMessage ?? (commerce.PurchasesEnabled ? "" : commerce.BuyStateText(SkinCatalog.All[0].productId));
            // 선택된 버튼이 비활성으로 바뀌면 키보드 포커스를 잃지 않게 옮긴다.
            if (IsOpen && EventSystem.current != null && selected != null && selected.transform.IsChildOf(transform))
            {
                var sel = selected.GetComponent<Selectable>();
                if (sel != null && !sel.interactable) EventSystem.current.SetSelectedGameObject(FirstSelectable());
            }
        }
    }
}
