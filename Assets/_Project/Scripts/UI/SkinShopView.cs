using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 치장 스킨 상점(타이틀 → "스킨"). 탭 2개:
    /// - 캐릭터: 기본 외형과 스킨을 같은 크기로 나란히 확대 비교 + 실제 색·복장 차이 설명 + "외형 전용 · 능력치 동일".
    ///   스토어 현지화 가격의 구매 버튼, 장착/해제. 가격은 코드에 없다 — 스토어 값이 없으면 구매 비활성.
    ///   스킨 아트가 없으면 스킨 칸을 비우고 "아트 준비 중"(기본 외형을 스킨처럼 보여 주지 않는다, 49).
    /// - 무기(49): 레이피어·총·검·활 무료 외형 1종씩. 기본/스킨 미리보기, 이름·차이, 무기별 장착(weapon_skins.json, 몸체 스킨과 독립).
    /// 하단 "구매 복원"·"뒤로". 버튼은 터치용 120px 높이(무기 장착 110px), 마우스/키보드 공용. 닫기 버튼 이름은 BackButton.
    /// </summary>
    public class SkinShopView : MonoBehaviour
    {
        public const string StatsSameText = "외형 전용 · 능력치 동일";

        class Card
        {
            public SkinDefinition def;
            public Image preview;
            public Image basePreview;
            /// <summary>잘라 내기 전 원본(실제 몸체 PNG) — 시험·QA 확인용.</summary>
            public Sprite previewSource, baseSource;
            public Text artNote;
            public Text difference;
            public Text state;
            public Button buy;
            public Text buyLabel;
            public Button equip;
            public Text equipLabel;
        }

        class WeaponCard
        {
            public WeaponKind weapon;
            public WeaponSkinDefinition def;
            public Image basePreview;
            public Image skinPreview;
            public Text artNote;
            public Text state;
            public Button equip;
            public Text equipLabel;
        }

        GameObject root;
        GameObject characterPanel, weaponPanel;
        Image characterTabImage, weaponTabImage;
        Button characterTab, weaponTab;
        Action onClose;
        Func<SkinCommerce> commerceSource;
        SkinCommerce commerce;
        readonly List<Card> cards = new List<Card>();
        readonly List<WeaponCard> weaponCards = new List<WeaponCard>();
        Button restoreButton;
        Button backButton;
        Text globalText;

        public bool IsOpen => root != null && root.activeSelf;
        public bool WeaponTabOpen => weaponPanel != null && weaponPanel.activeSelf;
        /// <summary>테스트용.</summary>
        public Button BackButton => backButton;
        public Button RestoreButton => restoreButton;
        public Button CharacterTabButton => characterTab;
        public Button WeaponTabButton => weaponTab;
        public Button BuyButton(string productId) => Find(productId)?.buy;
        public Button EquipButton(string productId) => Find(productId)?.equip;
        public string StateText(string productId) => Find(productId)?.state.text;
        public Sprite PreviewSprite(string productId) { var c = Find(productId); return c != null && c.preview.enabled ? c.previewSource : null; }
        public Sprite BasePreviewSprite(string productId) => Find(productId)?.baseSource;
        public string DifferenceText(string productId) => Find(productId)?.difference.text;
        public bool ArtNoteVisible(string productId) => Find(productId)?.artNote.gameObject.activeSelf ?? false;
        public string GlobalText => globalText != null ? globalText.text : "";
        public Button WeaponEquipButton(WeaponKind w) => FindWeapon(w)?.equip;
        public string WeaponStateText(WeaponKind w) => FindWeapon(w)?.state.text;
        public Sprite WeaponSkinPreview(WeaponKind w) { var c = FindWeapon(w); return c != null && c.skinPreview.enabled ? c.skinPreview.sprite : null; }
        public Sprite WeaponBasePreview(WeaponKind w) => FindWeapon(w)?.basePreview.sprite;
        public bool WeaponArtNoteVisible(WeaponKind w) => FindWeapon(w)?.artNote.gameObject.activeSelf ?? false;

        Card Find(string productId)
        {
            foreach (var c in cards) if (c.def.productId == productId) return c;
            return null;
        }

        WeaponCard FindWeapon(WeaponKind w)
        {
            foreach (var c in weaponCards) if (c.weapon == w) return c;
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
            var sub = UiFactory.Place("Subtitle", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(1500f, 38f));
            UiFactory.AddText(sub, "모든 스킨은 " + StatsSameText + " — 공격·체력·직업 특성은 바뀌지 않습니다.", 26, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.85f, 0.9f));

            characterTab = MakeButton(rootRt, "CharacterTab", "캐릭터 스킨", new Vector2(0.5f, 1f), new Vector2(-190f, -156f),
                new Vector2(360f, 66f), new Color(0.32f, 0.24f, 0.4f), out _, () => ShowTab(false));
            weaponTab = MakeButton(rootRt, "WeaponTab", "무기 스킨 (무료)", new Vector2(0.5f, 1f), new Vector2(190f, -156f),
                new Vector2(360f, 66f), new Color(0.32f, 0.24f, 0.4f), out _, () => ShowTab(true));
            characterTabImage = characterTab.GetComponent<Image>();
            weaponTabImage = weaponTab.GetComponent<Image>();

            characterPanel = UiFactory.Stretch("CharacterPanel", rootRt).gameObject;
            weaponPanel = UiFactory.Stretch("WeaponPanel", rootRt).gameObject;

            float x = -420f;
            foreach (var def in SkinCatalog.All)
            {
                cards.Add(BuildCard((RectTransform)characterPanel.transform, def, x));
                x += 840f;
            }
            float wx = -705f;
            foreach (var w in WeaponSkinCatalog.Weapons)
            {
                weaponCards.Add(BuildWeaponCard((RectTransform)weaponPanel.transform, w, wx));
                wx += 470f;
            }

            globalText = UiFactory.AddText(UiFactory.Place("GlobalMessage", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 174f),
                new Vector2(1600f, 32f)), "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.7f));

            restoreButton = MakeButton(rootRt, "RestoreButton", "구매 복원", new Vector2(0.5f, 0f), new Vector2(-230f, 50f),
                new Vector2(420f, 120f), new Color(0.24f, 0.4f, 0.5f), out _, OnRestore);
            backButton = MakeButton(rootRt, "BackButton", "뒤로", new Vector2(0.5f, 0f), new Vector2(230f, 50f),
                new Vector2(420f, 120f), new Color(0.3f, 0.32f, 0.36f), out _, Hide, "ui_cancel");
        }

        static Image PreviewImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            // 미리보기 칸 배경(같은 크기) — 두 외형을 같은 조건에서 비교한다.
            var frame = UiFactory.Place(name + "Frame", parent, new Vector2(0.5f, 1f), pos, size);
            UiFactory.AddImage(frame, new Color(0.2f, 0.18f, 0.24f, 0.9f)).raycastTarget = false;
            var rt = UiFactory.Place(name, frame, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 16f));
            var img = UiFactory.AddImage(rt, Color.white);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // 좌표 메모: UiFactory.Place는 pivot = anchor라 위쪽 기준(0.5,1) 요소의 y는 "윗변", 아래쪽 기준(0.5,0)은 "아랫변"이다.
        Card BuildCard(RectTransform parent, SkinDefinition def, float x)
        {
            var card = new Card { def = def };
            var rt = UiFactory.Place("Skin_" + def.productId, parent, new Vector2(0.5f, 0.5f), new Vector2(x, -10f), new Vector2(780f, 640f));
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.12f, 0.1f, 0.13f, 0.96f)));

            UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(720f, 48f)),
                def.displayName, 36, TextAnchor.MiddleCenter, Color.white);

            // 49: 기본과 스킨을 같은 크기(280)로 나란히 — 몸체 그림 영역만 잘라 확대해 색·복장 차이를 볼 수 있게 한다.
            card.basePreview = PreviewImage(rt, "BasePreview", new Vector2(-185f, -64f), new Vector2(280f, 280f));
            card.preview = PreviewImage(rt, "Preview", new Vector2(185f, -64f), new Vector2(280f, 280f));
            UiFactory.AddText(UiFactory.Place("BaseLabel", rt, new Vector2(0.5f, 1f), new Vector2(-185f, -346f), new Vector2(280f, 28f)),
                "기본 외형", 22, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            UiFactory.AddText(UiFactory.Place("SkinLabel", rt, new Vector2(0.5f, 1f), new Vector2(185f, -346f), new Vector2(280f, 28f)),
                "스킨 외형", 22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.6f));
            card.artNote = UiFactory.AddText(UiFactory.Place("ArtNote", rt, new Vector2(0.5f, 1f), new Vector2(185f, -184f), new Vector2(260f, 40f)),
                "아트 준비 중", 26, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.5f));

            card.difference = UiFactory.AddText(UiFactory.Place("Difference", rt, new Vector2(0.5f, 1f), new Vector2(0f, -378f),
                new Vector2(750f, 58f)), def.difference ?? "", 21, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.92f));
            UiFactory.AddText(UiFactory.Place("StatsSame", rt, new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(740f, 28f)),
                StatsSameText + " (직업 특성과 무관)", 21, TextAnchor.MiddleCenter, new Color(0.65f, 0.9f, 0.7f));

            card.state = UiFactory.AddText(UiFactory.Place("State", rt, new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(720f, 32f)),
                "", 23, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.75f));

            card.buy = MakeButton(rt, "BuyButton", "구매", new Vector2(0.5f, 0f), new Vector2(-180f, 12f), new Vector2(340f, 120f),
                new Color(0.9f, 0.35f, 0.75f), out card.buyLabel, () => OnBuy(def.productId));
            card.equip = MakeButton(rt, "EquipButton", "장착", new Vector2(0.5f, 0f), new Vector2(180f, 12f), new Vector2(340f, 120f),
                new Color(0.4f, 0.24f, 0.5f), out card.equipLabel, () => OnEquip(def));
            return card;
        }

        WeaponCard BuildWeaponCard(RectTransform parent, WeaponKind weapon, float x)
        {
            var def = WeaponSkinCatalog.ForWeapon(weapon);
            var card = new WeaponCard { weapon = weapon, def = def };
            var rt = UiFactory.Place("WeaponSkin_" + weapon, parent, new Vector2(0.5f, 0.5f), new Vector2(x, -10f), new Vector2(440f, 640f));
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.12f, 0.1f, 0.13f, 0.96f)));
            UiFactory.AddText(UiFactory.Place("Weapon", rt, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(400f, 42f)),
                WeaponSkinCatalog.WeaponLabel(weapon), 32, TextAnchor.MiddleCenter, Color.white);

            UiFactory.AddText(UiFactory.Place("BaseLabel", rt, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(400f, 26f)),
                "기본 외형", 21, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            card.basePreview = PreviewImage(rt, "BasePreview", new Vector2(0f, -82f), new Vector2(400f, 120f));
            UiFactory.AddText(UiFactory.Place("SkinLabel", rt, new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(400f, 28f)),
                def != null ? def.displayName : "", 23, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.6f));
            card.skinPreview = PreviewImage(rt, "SkinPreview", new Vector2(0f, -236f), new Vector2(400f, 120f));
            card.artNote = UiFactory.AddText(UiFactory.Place("ArtNote", rt, new Vector2(0.5f, 1f), new Vector2(0f, -278f), new Vector2(380f, 36f)),
                "아트 준비 중", 24, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.5f));
            UiFactory.AddText(UiFactory.Place("Difference", rt, new Vector2(0.5f, 1f), new Vector2(0f, -362f), new Vector2(420f, 54f)),
                def != null ? def.difference : "", 20, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.92f));
            UiFactory.AddText(UiFactory.Place("StatsSame", rt, new Vector2(0.5f, 1f), new Vector2(0f, -418f), new Vector2(420f, 26f)),
                "무료 · " + StatsSameText, 20, TextAnchor.MiddleCenter, new Color(0.65f, 0.9f, 0.7f));
            card.state = UiFactory.AddText(UiFactory.Place("State", rt, new Vector2(0.5f, 0f), new Vector2(0f, 132f), new Vector2(420f, 30f)),
                "", 21, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.75f));
            card.equip = MakeButton(rt, "WeaponEquipButton", "장착", new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(400f, 110f),
                new Color(0.4f, 0.24f, 0.5f), out card.equipLabel, () => OnWeaponEquip(card));
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
                c.baseSource = baseSprite;
                c.previewSource = skin;
                c.basePreview.sprite = Crop(baseSprite, BodyBox);
                c.basePreview.enabled = baseSprite != null;
                // 49: 스킨 아트가 없으면 기본 외형을 스킨 칸에 그리지 않는다(오인 방지).
                c.preview.sprite = Crop(skin, BodyBox);
                c.preview.enabled = skin != null;
                c.artNote.gameObject.SetActive(skin == null);
            }
            foreach (var w in weaponCards)
            {
                var b = PreviewCrop(WeaponSkinCatalog.BaseHeldSprite(w.weapon), w.weapon);
                w.basePreview.sprite = b;
                w.basePreview.enabled = b != null;
                var s = PreviewCrop(WeaponSkinCatalog.HeldSprite(w.def), w.weapon);
                w.skinPreview.sprite = s;
                w.skinPreview.enabled = s != null;
                w.artNote.gameObject.SetActive(s == null);
            }
            ShowTab(false, false);
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FirstSelectable());
        }

        // 무기 PNG(256 캔버스)는 손 위치 맞춤용 투명 여백이 커서 미리보기가 작게 보인다 — 실제 그림 영역(기본·스킨 동일 실루엣)만 잘라 크게 보여 준다.
        // 좌상단 원점 px(x0,y0,x1,y1), 측정: 기본·스킨 모두 같은 경계. 표시 전용(전투 렌더·판정과 무관).
        static readonly Dictionary<WeaponKind, RectInt> ContentBox = new Dictionary<WeaponKind, RectInt>
        {
            { WeaponKind.Rapier, new RectInt(18, 116, 216, 32) }, { WeaponKind.Gun, new RectInt(34, 94, 204, 58) },
            { WeaponKind.Sword, new RectInt(18, 104, 220, 50) }, { WeaponKind.Bow, new RectInt(66, 32, 90, 200) },
        };
        static readonly Dictionary<Sprite, Sprite> cropCache = new Dictionary<Sprite, Sprite>();

        /// <summary>몸체 idle 그림 영역(기본·스킨 두 직업 모두 x80..176, y32..224) + 여백.</summary>
        static readonly RectInt BodyBox = new RectInt(80, 32, 96, 192);

        public static Sprite PreviewCrop(Sprite s, WeaponKind weapon) =>
            ContentBox.TryGetValue(weapon, out var box) ? Crop(s, box) : s;

        static Sprite Crop(Sprite s, RectInt box)
        {
            if (s == null || s.texture == null || s.rect.width < 256f || s.rect.height < 256f) return s;
            if (cropCache.TryGetValue(s, out var cached) && cached != null) return cached;
            const int pad = 6;
            var r = s.textureRect;
            float x = r.x + Mathf.Max(0, box.x - pad), top = Mathf.Max(0, box.y - pad);
            float w = Mathf.Min(r.width - (x - r.x), box.width + pad * 2), h = Mathf.Min(r.height - top, box.height + pad * 2);
            float y = r.y + r.height - top - h;
            var crop = Sprite.Create(s.texture, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            crop.name = s.name + "_preview";
            cropCache[s] = crop;
            return crop;
        }

        /// <summary>탭 전환(캐릭터/무기). 구매 복원은 캐릭터 탭에서만 의미가 있어 무기 탭에서는 숨긴다.</summary>
        public void ShowTab(bool weapons) => ShowTab(weapons, true);

        void ShowTab(bool weapons, bool focus)
        {
            characterPanel.SetActive(!weapons);
            weaponPanel.SetActive(weapons);
            restoreButton.gameObject.SetActive(!weapons);
            var on = new Color(0.62f, 0.42f, 0.22f);
            var off = new Color(0.26f, 0.22f, 0.3f);
            characterTabImage.color = weapons ? off : on;
            weaponTabImage.color = weapons ? on : off;
            Refresh();
            if (focus && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FirstSelectable());
        }

        GameObject FirstSelectable()
        {
            if (WeaponTabOpen)
            {
                foreach (var w in weaponCards) if (w.equip.interactable) return w.equip.gameObject;
                return weaponTab.gameObject;
            }
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

        void OnWeaponEquip(WeaponCard card)
        {
            if (card.def == null) return;
            bool equipped = WeaponSkinStore.EquippedFor(card.weapon) == card.def.id;
            bool saved = WeaponSkinStore.Equip(card.weapon, equipped ? null : card.def.id);
            Refresh();
            if (!saved && globalText != null) globalText.text = "무기 외형 선택을 저장하지 못했습니다(이번 실행에는 적용).";
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
            foreach (var w in weaponCards)
            {
                bool ready = WeaponSkinCatalog.ArtReady(w.def);
                bool equipped = w.def != null && WeaponSkinStore.EquippedFor(w.weapon) == w.def.id;
                // 저장된 선택이 있어도 아트가 없으면 실제 전투는 기본 외형 — 상태도 그대로 알린다.
                w.equip.interactable = ready || equipped;
                w.equipLabel.text = equipped ? "기본 외형으로" : ready ? "스킨 장착" : "준비 중";
                w.state.text = equipped ? (ready ? "장착: " + w.def.displayName : "선택됨 · 아트 준비 전까지 기본 외형") : "장착: 기본 외형";
            }
            restoreButton.interactable = commerce.PurchasesEnabled;
            globalText.text = WeaponTabOpen ? "무기 외형은 무료입니다. 무기마다 따로 저장되며 캐릭터 스킨과 함께 쓸 수 있습니다."
                : commerce.GlobalMessage ?? (commerce.PurchasesEnabled ? "" : commerce.BuyStateText(SkinCatalog.All[0].productId));
            // 선택된 버튼이 비활성으로 바뀌면 키보드 포커스를 잃지 않게 옮긴다.
            if (IsOpen && EventSystem.current != null && selected != null && selected.transform.IsChildOf(transform))
            {
                var sel = selected.GetComponent<Selectable>();
                if (sel != null && (!sel.interactable || !sel.gameObject.activeInHierarchy))
                    EventSystem.current.SetSelectedGameObject(FirstSelectable());
            }
        }
    }
}
