using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 치장 스킨 상점(타이틀 → "스킨"). 탭 2개:
    /// - 캐릭터(79): 왼쪽 스크롤 목록(12종, 직업별 묶음 — 행 = 이름·직업 전용·보유/가격) + 오른쪽 선택 상세.
    ///   상세: 기본 외형과 스킨을 같은 크기로 나란히 확대 비교 + 실제 색·복장 차이 설명 + "외형 전용 · 능력치 동일".
    ///   현금 스킨(기존 2종)은 스토어 현지화 가격의 구매 버튼(가격은 코드에 없다 — 스토어 값이 없으면 구매 비활성).
    ///   코인 스킨(신규 10종)은 판매 코인 1000으로 구매(DrinkLab.BuyCosmetic, 스토어 결제와 무관).
    ///   스킨 아트가 완성되지 않았으면 스킨 칸을 비우고 "아트 준비 중", 코인 판매도 막는다(기본 외형을 스킨처럼 보여 주지 않는다, 49).
    /// - 무기(49): 레이피어·총·검·활 무료 외형 1종씩. 기본/스킨 미리보기, 이름·차이, 무기별 장착(weapon_skins.json, 몸체 스킨과 독립).
    /// 하단 "구매 복원"·"뒤로". 버튼은 터치용 120px 높이(무기 장착 110px), 마우스/키보드 공용. 닫기 버튼 이름은 BackButton.
    /// </summary>
    /// <summary>79: 목록 행이 키보드/게임패드 포커스를 받으면 그 스킨을 상세에 보여 준다.</summary>
    public class SkinRowFocus : MonoBehaviour, ISelectHandler
    {
        public Action onSelect;
        public void OnSelect(BaseEventData eventData) => onSelect?.Invoke();
    }

    public class SkinShopView : MonoBehaviour
    {
        public const string StatsSameText = "외형 전용 · 능력치 동일";

        /// <summary>목록 1행(스킨 1종). 상세 칸은 선택된 행 하나만 보여 준다.</summary>
        class Card
        {
            public SkinDefinition def;
            public Button row;
            public Image rowImage;
            public Text rowStatus;
            /// <summary>잘라 내기 전 원본(실제 몸체 PNG) — 시험·QA 확인용. 스킨 아트가 완성되지 않았으면 null.</summary>
            public Sprite previewSource, baseSource;
        }

        // 선택 상세(하나)
        Image detailPreview, detailBasePreview;
        Text detailName, detailJob, detailArtNote, detailDifference, detailState, detailBuyLabel, detailEquipLabel;
        Button detailBuy, detailEquip;
        ScrollRect listScroll;
        RectTransform listContent;
        string selectedId;

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
        // 79: 상세 칸은 하나 — 스킨별 조회는 그 스킨을 선택한 뒤 상세 칸을 돌려준다(행 클릭과 같은 경로).
        public string SelectedId => selectedId;
        public int RowCount => cards.Count;
        public Button RowButton(string productId) => Find(productId)?.row;
        public string RowStatusText(string productId) => Find(productId)?.rowStatus.text;
        public ScrollRect ListScroll => listScroll;
        public RectTransform ListContent => listContent;
        public Button BuyButton(string productId) => Select(productId) ? detailBuy : null;
        public Button EquipButton(string productId) => Select(productId) ? detailEquip : null;
        public string BuyLabel(string productId) => Select(productId) ? detailBuyLabel.text : null;
        public string StateText(string productId) => Select(productId) ? detailState.text : null;
        public Sprite PreviewSprite(string productId) => Select(productId) && detailPreview.enabled ? Find(productId).previewSource : null;
        public Sprite BasePreviewSprite(string productId) => Find(productId)?.baseSource;
        public string DifferenceText(string productId) => Select(productId) ? detailDifference.text : null;
        public bool ArtNoteVisible(string productId) => Select(productId) && detailArtNote.gameObject.activeSelf;
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

            BuildList((RectTransform)characterPanel.transform);
            BuildDetail((RectTransform)characterPanel.transform);
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

        const float RowHeight = 96f, RowGap = 8f, HeaderHeight = 46f, ListWidth = 660f;

        static string JobLabel(Job job) => job == Job.Bartender ? "바텐더" : "티 소믈리에";

        /// <summary>79: 왼쪽 스크롤 목록 — 직업별 머리글 + 행(이름 / 직업 전용·보유·가격). 마우스 휠·끌기·방향키(선택 행을 따라 스크롤).</summary>
        void BuildList(RectTransform parent)
        {
            var area = UiFactory.Place("SkinList", parent, new Vector2(0.5f, 0.5f), new Vector2(-580f, -10f), new Vector2(ListWidth, 620f));
            UiFactory.AddImage(area, new Color(0.1f, 0.08f, 0.12f, 0.92f));
            var viewport = UiFactory.Place("Viewport", area, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(ListWidth - 28f, 620f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.AddImage(viewport, new Color(0f, 0f, 0f, 0.01f)); // 끌기 스크롤용 레이캐스트 면
            var contentGo = new GameObject("Content", typeof(RectTransform));
            listContent = (RectTransform)contentGo.transform;
            listContent.SetParent(viewport, false);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.anchoredPosition = Vector2.zero;

            float y = 8f;
            Job? lastJob = null;
            foreach (var def in SkinCatalog.All)
            {
                if (lastJob != def.job)
                {
                    lastJob = def.job;
                    UiFactory.AddText(UiFactory.Place("Header_" + def.job, listContent, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(ListWidth - 60f, HeaderHeight)),
                        JobLabel(def.job) + " 스킨", 26, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.55f));
                    y += HeaderHeight;
                }
                cards.Add(BuildRow(def, y));
                y += RowHeight + RowGap;
            }
            listContent.sizeDelta = new Vector2(0f, y + 8f);

            listScroll = area.gameObject.AddComponent<ScrollRect>();
            listScroll.content = listContent;
            listScroll.viewport = viewport;
            listScroll.horizontal = false;
            listScroll.vertical = true;
            listScroll.movementType = ScrollRect.MovementType.Clamped;
            listScroll.scrollSensitivity = 40f;
            listScroll.verticalScrollbar = BuildScrollbar(area);
            listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            area.gameObject.AddComponent<ScrollToSelected>().Init(listScroll, listContent, viewport);
        }

        static Scrollbar BuildScrollbar(RectTransform area)
        {
            var bar = UiFactory.Place("Scrollbar", area, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(20f, 600f));
            UiFactory.AddImage(bar, new Color(0.25f, 0.22f, 0.3f, 0.9f));
            var sliding = UiFactory.Stretch("SlidingArea", bar);
            var handle = UiFactory.Stretch("Handle", sliding);
            var handleImg = UiFactory.AddImage(handle, new Color(0.85f, 0.7f, 0.45f, 1f));
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = handleImg;
            sb.direction = Scrollbar.Direction.BottomToTop;
            var nav = sb.navigation;
            nav.mode = Navigation.Mode.None; // 키보드 포커스는 행·버튼만
            sb.navigation = nav;
            return sb;
        }

        Card BuildRow(SkinDefinition def, float y)
        {
            var card = new Card { def = def };
            var rt = UiFactory.Place("Row_" + def.productId, listContent, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(ListWidth - 60f, RowHeight));
            card.rowImage = UiFactory.AddImage(rt, new Color(0.22f, 0.18f, 0.26f, 1f));
            card.row = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(card.row);
            card.row.targetGraphic = card.rowImage;
            UiFactory.ApplyHoverColors(card.row);
            card.row.onClick.AddListener(() => { AudioService.PlaySfx("ui_confirm"); Select(def.productId); });
            rt.gameObject.AddComponent<SkinRowFocus>().onSelect = () => Select(def.productId); // 방향키로 행에 오면 바로 상세 갱신
            UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(ListWidth - 96f, 40f)),
                def.displayName, 28, TextAnchor.MiddleLeft, Color.white);
            card.rowStatus = UiFactory.AddText(UiFactory.Place("Status", rt, new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(ListWidth - 96f, 34f)),
                "", 21, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.9f));
            return card;
        }

        // 좌표 메모: UiFactory.Place는 pivot = anchor라 위쪽 기준(0.5,1) 요소의 y는 "윗변", 아래쪽 기준(0.5,0)은 "아랫변"이다.
        void BuildDetail(RectTransform parent)
        {
            var rt = UiFactory.Place("SkinDetail", parent, new Vector2(0.5f, 0.5f), new Vector2(350f, -10f), new Vector2(1100f, 620f));
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.12f, 0.1f, 0.13f, 0.96f)));

            detailName = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1040f, 48f)),
                "", 36, TextAnchor.MiddleCenter, Color.white);
            detailJob = UiFactory.AddText(UiFactory.Place("Job", rt, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1040f, 28f)),
                "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.55f));

            // 49: 기본과 스킨을 같은 크기(240)로 나란히 — 몸체 그림 영역만 잘라 확대해 색·복장 차이를 볼 수 있게 한다.
            detailBasePreview = PreviewImage(rt, "BasePreview", new Vector2(-170f, -92f), new Vector2(240f, 240f));
            detailPreview = PreviewImage(rt, "Preview", new Vector2(170f, -92f), new Vector2(240f, 240f));
            UiFactory.AddText(UiFactory.Place("BaseLabel", rt, new Vector2(0.5f, 1f), new Vector2(-170f, -334f), new Vector2(240f, 24f)),
                "기본 외형", 22, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            UiFactory.AddText(UiFactory.Place("SkinLabel", rt, new Vector2(0.5f, 1f), new Vector2(170f, -334f), new Vector2(240f, 24f)),
                "스킨 외형", 22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.6f));
            detailArtNote = UiFactory.AddText(UiFactory.Place("ArtNote", rt, new Vector2(0.5f, 1f), new Vector2(170f, -194f), new Vector2(230f, 40f)),
                "아트 준비 중", 26, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.5f));

            detailDifference = UiFactory.AddText(UiFactory.Place("Difference", rt, new Vector2(0.5f, 1f), new Vector2(0f, -360f),
                new Vector2(1040f, 56f)), "", 21, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.92f));
            UiFactory.AddText(UiFactory.Place("StatsSame", rt, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(1040f, 26f)),
                StatsSameText + " (직업 특성과 무관)", 21, TextAnchor.MiddleCenter, new Color(0.65f, 0.9f, 0.7f));

            detailState = UiFactory.AddText(UiFactory.Place("State", rt, new Vector2(0.5f, 0f), new Vector2(0f, 134f), new Vector2(1040f, 30f)),
                "", 23, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.75f));

            detailBuy = MakeButton(rt, "BuyButton", "구매", new Vector2(0.5f, 0f), new Vector2(-190f, 8f), new Vector2(360f, 120f),
                new Color(0.9f, 0.35f, 0.75f), out detailBuyLabel, () => OnBuy(selectedId));
            detailEquip = MakeButton(rt, "EquipButton", "장착", new Vector2(0.5f, 0f), new Vector2(190f, 8f), new Vector2(360f, 120f),
                new Color(0.4f, 0.24f, 0.5f), out detailEquipLabel, () => OnEquip(SkinCatalog.Find(selectedId)));
            detailBuyLabel.resizeTextForBestFit = true;
            detailBuyLabel.resizeTextMinSize = 20;
            detailBuyLabel.resizeTextMaxSize = 30;
        }

        /// <summary>목록에서 스킨 선택 → 상세 칸 갱신. 모르는 ID면 false.</summary>
        public bool Select(string productId)
        {
            var card = Find(productId);
            if (card == null) return false;
            if (selectedId != productId)
            {
                selectedId = productId;
                coinMessage = null;
                ApplyDetail();
            }
            return true;
        }

        void ApplyDetail()
        {
            var card = Find(selectedId);
            if (card == null) return;
            var def = card.def;
            detailName.text = def.displayName;
            detailJob.text = JobLabel(def.job) + " 전용 · " + (def.paid ? "기존 결제 스킨(스토어 가격)" : "판매 코인 " + SkinCatalog.CoinSkinPrice + " 전용(현금 결제 아님)");
            detailDifference.text = def.difference ?? "";
            detailBasePreview.sprite = Crop(card.baseSource, BodyBox);
            detailBasePreview.enabled = card.baseSource != null;
            // 49: 스킨 아트가 없으면 기본 외형을 스킨 칸에 그리지 않는다(오인 방지).
            detailPreview.sprite = Crop(card.previewSource, BodyBox);
            detailPreview.enabled = card.previewSource != null;
            detailArtNote.gameObject.SetActive(card.previewSource == null);
            Refresh();
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
                c.baseSource = SkinArt.BaseIdleSprite(c.def.job);
                // 79: 모든 실제 몸체 프레임이 갖춰진 스킨만 미리보기(일부 프레임만 있으면 "아트 준비 중").
                c.previewSource = SkinArt.Exists(c.def) ? SkinArt.PreviewSprite(c.def) : null;
            }
            var keep = selectedId;
            selectedId = null;
            Select(Find(keep) != null ? keep : cards[0].def.productId);
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

        /// <summary>몸체 idle 그림 영역 + 여백. 79: 신규 스킨의 높은 모자(별자리 y10)·등짐 바구니(산길 x67)까지 들어가도록 x66..178, y10..224
        /// (기존 x80..176, y32..224 포함). 기본·스킨 같은 상자라 비교 배율이 같다.</summary>
        static readonly RectInt BodyBox = new RectInt(66, 10, 112, 214);

        /// <summary>몸체 미리보기(그림 영역만 잘라 확대) — 판매 코인 목록(DrinkLabView)도 같은 기준을 쓴다.</summary>
        public static Sprite BodyPreview(Sprite s) => Crop(s, BodyBox);

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
            var current = Find(selectedId);
            if (current != null) return current.row.gameObject; // 목록의 선택 행 — 방향키로 행 이동, →로 구매/장착
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
            var def = SkinCatalog.Find(productId);
            if (def == null || commerce == null) return;
            if (def.paid) commerce.Buy(productId);
            else
            {
                // 79: 코인 스킨 — 판매 코인으로 구매(현금 스토어 경로 없음). 중복 클릭은 BuyCosmetic이 소유/잔액으로 거부한다.
                string error;
                bool ok;
                try { ok = DrinkLab.BuyCosmetic(productId, commerce.IsCashOwned, out error); }
                catch (Exception) { ok = false; error = "코인 구매를 처리하지 못했습니다."; }
                coinMessage = ok ? $"{def.displayName}을(를) 판매 코인 {SkinCatalog.CoinSkinPrice}으로 샀습니다. 장착할 수 있습니다." : error;
            }
            Refresh();
        }

        string coinMessage;

        /// <summary>79 키보드: 행 ↑↓(머리글 건너뜀, 맨 위 → 탭, 맨 아래 → 복원), 행 → 상세의 쓸 수 있는 버튼, 상세 ← 선택 행.
        /// 비활성 버튼으로는 보내지 않는다(Refresh마다 다시 연결).</summary>
        void WireNavigation()
        {
            if (cards.Count == 0 || detailBuy == null) return;
            var current = Find(selectedId);
            Selectable rowTarget = current != null ? current.row : cards[0].row;
            Selectable detailTarget = detailBuy.interactable ? detailBuy : detailEquip.interactable ? (Selectable)detailEquip : null;
            Selectable bottom = restoreButton != null && restoreButton.interactable ? restoreButton : (Selectable)backButton;
            for (int i = 0; i < cards.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? cards[i - 1].row : characterTab,
                    selectOnDown = i < cards.Count - 1 ? cards[i + 1].row : bottom,
                    selectOnRight = detailTarget,
                    selectOnLeft = null,
                };
                cards[i].row.navigation = nav;
            }
            detailBuy.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnLeft = rowTarget, selectOnRight = detailEquip.interactable ? detailEquip : null,
                selectOnUp = characterTab, selectOnDown = bottom,
            };
            detailEquip.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnLeft = detailBuy.interactable ? (Selectable)detailBuy : rowTarget,
                selectOnUp = characterTab, selectOnDown = backButton,
            };
        }

        static int SafeCoins() { try { return DrinkLab.Coins; } catch (Exception) { return 0; } }

        static bool SafeOnSale(SkinDefinition def)
        {
            try { return DrinkSales.OnSale(DrinkSales.Cosmetic(def.productId)); }
            catch (Exception) { return false; }
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
            int coins = SafeCoins();
            foreach (var c in cards)
            {
                var id = c.def.productId;
                bool owned = commerce.IsOwned(id);
                bool equipped = commerce.IsEquipped(id);
                bool isSelected = id == selectedId;
                bool onSale = !c.def.paid && SafeOnSale(c.def);
                string rowState = equipped ? "장착 중" : owned ? "보유"
                    : c.def.paid ? (commerce.PurchasesEnabled ? commerce.BuyStateText(id) : "결제 스킨 · 미보유")
                    : onSale ? $"판매 코인 {SkinCatalog.CoinSkinPrice}" : "아트 준비 중";
                c.rowStatus.text = JobLabel(c.def.job) + " 전용 · " + rowState;
                c.rowImage.color = isSelected ? new Color(0.62f, 0.42f, 0.22f, 1f) : new Color(0.22f, 0.18f, 0.26f, 1f);
                if (!isSelected) continue;

                string stateLine, msg;
                if (c.def.paid)
                {
                    var price = commerce.PriceText(id);
                    detailBuy.interactable = commerce.CanBuy(id);
                    detailBuyLabel.text = owned ? "보유 중" : price != null ? "구매  " + price : "구매";
                    msg = commerce.MessageFor(id);
                    stateLine = equipped ? "장착 중" : commerce.BuyStateText(id);
                    if (!owned && !commerce.PurchasesEnabled) { stateLine = "미보유"; msg = null; }
                }
                else
                {
                    detailBuy.interactable = !owned && onSale && coins >= SkinCatalog.CoinSkinPrice;
                    detailBuyLabel.text = owned ? "보유 중" : onSale ? $"구매  {SkinCatalog.CoinSkinPrice}코인" : "준비 중";
                    msg = coinMessage;
                    stateLine = equipped ? "장착 중" : owned ? "보유 중(판매 코인 구매)"
                        : !onSale ? "아트 준비 중 · 아직 판매하지 않음"
                        : $"판매 코인 {SkinCatalog.CoinSkinPrice} · 보유 {coins}" + (coins < SkinCatalog.CoinSkinPrice ? $" ({SkinCatalog.CoinSkinPrice - coins} 부족)" : "");
                }
                detailEquip.interactable = owned;
                detailEquipLabel.text = equipped ? "장착 해제" : owned ? "장착" : "장착(미보유)";
                detailState.text = string.IsNullOrEmpty(msg) || msg == stateLine ? stateLine : stateLine + " · " + msg;
            }
            WireNavigation();
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
                : commerce.GlobalMessage ?? (commerce.PurchasesEnabled ? ""
                    : SkinCatalog.Find(selectedId) is SkinDefinition selDef && !selDef.paid ? "판매 코인 스킨은 현금 결제 없이 이 화면에서 바로 살 수 있습니다." // 79
                    : commerce.BuyStateText(SkinCatalog.All[0].productId));
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
