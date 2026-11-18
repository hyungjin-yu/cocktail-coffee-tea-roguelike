using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 카페 입장형 첫 화면의 장면(외부 전경 + 실내). layout.json(정규화 좌상단 원점)을 기준 1920x1080 장면 좌표로 바꿔
    /// 이미지·클릭 영역·이름표·문을 한 컨테이너에 둔다 → 화면 맞춤(cover/contain)·카메라 이동/확대가 모두 같은 변환으로 적용된다.
    /// 클릭 영역은 기존 메뉴 버튼과 같은 이름의 Button(시험·투어가 이름으로 찾음).
    /// </summary>
    public sealed class CafeTitleScene : MonoBehaviour
    {
        public const float RefW = 1920f, RefH = 1080f;

        public static readonly Dictionary<string, string> ButtonNames = new Dictionary<string, string>
        {
            { "start", "StartButton" }, { "permanent", "PermanentShopButton" }, { "codex", "CardCodexButton" },
            { "skins", "SkinShopButton" }, { "history", "RunHistoryButton" }, { "settings", "SettingsButton" },
            { "credits", "CreditsButton" }, { "quit", "QuitButton" },
        };

        public CafeTitleLayout Layout { get; private set; }
        public RectTransform Exterior { get; private set; }
        public RectTransform Interior { get; private set; }
        public Image DoorImage { get; private set; }
        /// <summary>외부 간판 표면에 쓴 게임 이름(Unity 텍스트, 아트에 굽지 않음).</summary>
        public Text SignTitle { get; private set; }
        /// <summary>입장 bridge 뷰(같은 평면에서 그린 카메라 뷰 PNG들). 없으면 null.</summary>
        public RectTransform Bridge { get; private set; }
        public Image BridgeImage { get; private set; }
        public Image BridgeImageNext { get; private set; }
        public Sprite[] BridgeSprites { get; private set; }
        CanvasGroup bridgeGroup;
        Vector2 brCenter = new Vector2(0.5f, 0.5f);
        float brZoom = 1f;
        public float BridgeAlpha { get => bridgeGroup != null ? bridgeGroup.alpha : 0f; set { if (bridgeGroup != null) bridgeGroup.alpha = value; } }
        public Sprite[] DoorFrames { get; private set; }
        public readonly Dictionary<string, CafeHotspot> Hotspots = new Dictionary<string, CafeHotspot>();
        public readonly List<CafeHotspot> NavOrder = new List<CafeHotspot>();
        CanvasGroup interiorGroup, exteriorGroup;
        RectTransform rootRt;

        // 카메라 상태(장면별): 정규화 중심 + 배율(1 = 화면 맞춤)
        Vector2 exCenter = new Vector2(0.5f, 0.5f), inCenter = new Vector2(0.5f, 0.5f);
        float exZoom = 1f, inZoom = 1f;

        public float InteriorAlpha { get => interiorGroup.alpha; set => interiorGroup.alpha = value; }
        public float ExteriorAlpha { get => exteriorGroup.alpha; set => exteriorGroup.alpha = value; }
        /// <summary>외관 입장 대기 중에만 외부 장면(입장 버튼)이 입력을 받는다.</summary>
        public bool ExteriorInteractable
        {
            get => exteriorGroup.blocksRaycasts;
            set { exteriorGroup.blocksRaycasts = value; exteriorGroup.interactable = value; }
        }

        public bool InteriorInteractable
        {
            get => interiorGroup.blocksRaycasts;
            set { interiorGroup.blocksRaycasts = value; interiorGroup.interactable = value; }
        }

        public static CafeTitleScene Build(RectTransform root, CafeTitleLayout layout, IDictionary<string, System.Action> actions)
        {
            var go = new GameObject("CafeScene", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var scene = go.AddComponent<CafeTitleScene>();
            scene.rootRt = root;
            scene.Layout = layout;

            scene.Interior = scene.Container("CafeInterior", out scene.interiorGroup);
            foreach (var l in layout.InteriorLayers) scene.AddLayer(scene.Interior, l);
            foreach (var f in layout.Features)
            {
                if (!ButtonNames.TryGetValue(f.Id, out var bname)) continue;
                actions.TryGetValue(f.Id, out var act);
                var hs = CafeHotspot.Create(scene.Interior, bname, f, act);
                scene.Hotspots[f.Id] = hs;
            }
            var order = layout.NavOrder.Count > 0 ? layout.NavOrder : new List<string>(ButtonNames.Keys);
            foreach (var id in order) if (scene.Hotspots.TryGetValue(id, out var h)) scene.NavOrder.Add(h);
            foreach (var h in scene.Hotspots.Values) if (!scene.NavOrder.Contains(h)) scene.NavOrder.Add(h);
            scene.BuildNavigation();

            scene.Exterior = scene.Container("CafeExterior", out scene.exteriorGroup);
            scene.exteriorGroup.blocksRaycasts = false;
            var door = layout.EntranceDoor;
            bool doorPlaced = false;
            foreach (var l in layout.ExteriorLayers)
            {
                // 문 안쪽 실내 일부(glimpse)·문짝은 문 자리 레이어 순서에 끼운다: z가 문 z(=glimpse z+1)보다 크면 문틀 등 앞 레이어
                if (!doorPlaced && door.Glimpse != null && l.Z > door.Glimpse.Z) { scene.AddDoor(); doorPlaced = true; }
                scene.AddLayer(scene.Exterior, l);
            }
            if (!doorPlaced) scene.AddDoor();
            scene.AddSignTitle();
            if (layout.Bridge.Count > 0)
            {
                scene.Bridge = scene.Container("CafeBridge", out scene.bridgeGroup);
                scene.bridgeGroup.blocksRaycasts = false;
                scene.bridgeGroup.alpha = 0f;
                scene.BridgeImage = Place(scene.Bridge, "BridgeView", new Rect(0, 0, 1, 1)).gameObject.AddComponent<Image>();
                scene.BridgeImageNext = Place(scene.Bridge, "BridgeViewNext", new Rect(0, 0, 1, 1)).gameObject.AddComponent<Image>();
                scene.BridgeImage.raycastTarget = scene.BridgeImageNext.raycastTarget = false;
                var sprites = new List<Sprite>();
                foreach (var v in layout.Bridge) sprites.Add(CafeTitleLayout.LoadSprite(v.Png));
                scene.BridgeSprites = sprites.ToArray();
            }
            scene.ApplyCameras();
            return scene;
        }

        /// <summary>간판 판(exterior.sign.rect) 위에 게임 이름을 칠한 글자처럼 얹는다: 판 높이의 약 55% 글자, 판 폭 안에 맞춤,
        /// 따뜻한 금색 + 왼쪽 위 주광에 맞춘 오른쪽 아래 짧은 그림자. 정면 간판이라 기울임 없음. 외부 장면과 같은 변환.</summary>
        void AddSignTitle()
        {
            var r = Layout.SignRect;
            if (r.width <= 0f || r.height <= 0f || (r.width >= 0.999f && r.height >= 0.999f)) return; // 간판 정보 없음
            var rt = Place(Exterior, "SignTitle", r);
            SignTitle = UiFactory.AddText(rt, GameInfo.Title, Mathf.RoundToInt(r.height * RefH * 0.55f), TextAnchor.MiddleCenter, new Color(0.97f, 0.84f, 0.55f));
            SignTitle.color = new Color(0.97f, 0.84f, 0.55f);
            SignTitle.raycastTarget = false;
            SignTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            SignTitle.verticalOverflow = VerticalWrapMode.Truncate;
            SignTitle.resizeTextForBestFit = true;
            SignTitle.resizeTextMinSize = 12;
            SignTitle.resizeTextMaxSize = SignTitle.fontSize;
            // 판 가장자리 여백(글자가 판 테두리를 넘지 않게)
            rt.sizeDelta = new Vector2(r.width * RefW * 0.86f, r.height * RefH * 0.8f);
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.06f, 0.03f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -3f);
        }

        RectTransform Container(string name, out CanvasGroup group)
        {
            var c = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)c.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RefW, RefH);
            group = c.AddComponent<CanvasGroup>();
            return rt;
        }

        void AddDoor()
        {
            var door = Layout.EntranceDoor;
            if (door.Glimpse != null && (door.Glimpse.Png != null || door.Glimpse.Color.HasValue)) AddLayer(Exterior, door.Glimpse);
            var rt = Place(Exterior, "EntranceDoor", door.Rect);
            DoorImage = rt.gameObject.AddComponent<Image>();
            DoorImage.raycastTarget = false;
            var frames = new List<Sprite>();
            foreach (var f in door.Frames) { var s = CafeTitleLayout.LoadSprite(f); if (s != null) frames.Add(s); }
            DoorFrames = frames.ToArray();
            if (DoorFrames.Length > 0) DoorImage.sprite = DoorFrames[0];
            else DoorImage.color = door.Color ?? new Color(0.25f, 0.15f, 0.09f);
        }

        void AddLayer(RectTransform parent, CafeTitleLayout.Layer l)
        {
            var rt = Place(parent, string.IsNullOrEmpty(l.Id) ? "Layer" : l.Id, l.Rect);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            if (!string.IsNullOrEmpty(l.Png))
            {
                img.sprite = CafeTitleLayout.LoadSprite(l.Png);
                if (img.sprite == null) { img.color = new Color(1f, 0f, 1f, 0.3f); return; }
                if (l.Tile)
                {
                    img.type = Image.Type.Tiled;
                    // 타일 한 칸 = 아트 픽셀 x (1920/artPixels.x) x tile_scale (늘림 없이 같은 픽셀 밀도)
                    float artToRef = RefW / Mathf.Max(1, Layout.ArtPixels.x) * l.TileScale;
                    img.pixelsPerUnitMultiplier = (100f / img.sprite.pixelsPerUnit) / artToRef;
                }
                if (l.Color.HasValue) img.color = l.Color.Value;
            }
            else img.color = l.Color ?? Color.black;
        }

        /// <summary>정규화 rect(좌상단 원점) → 장면 컨테이너 안의 RectTransform(가운데 피벗).</summary>
        public static RectTransform Place(RectTransform parent, string name, Rect n)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(n.width * RefW, n.height * RefH);
            rt.anchoredPosition = NormToLocal(n.center);
            return rt;
        }

        /// <summary>정규화 점(좌상단 원점) → 장면 로컬 좌표(가운데 원점, 위가 +y).</summary>
        public static Vector2 NormToLocal(Vector2 n) => new Vector2((n.x - 0.5f) * RefW, (0.5f - n.y) * RefH);
        public static Vector2 LocalToNorm(Vector2 l) => new Vector2(l.x / RefW + 0.5f, 0.5f - l.y / RefH);

        // ───────── 카메라(화면 맞춤 + 이동/확대) ─────────

        /// <summary>화면 맞춤 배율: cover = 높이/너비 중 큰 쪽(좌우 또는 위아래 자름), contain = 작은 쪽(전체 보임·여백).</summary>
        public float FitScale()
        {
            var size = rootRt.rect.size;
            float sx = size.x / RefW, sy = size.y / RefH;
            return Layout.FitMode == "contain" ? Mathf.Min(sx, sy) : Mathf.Max(sx, sy);
        }

        public void SetExteriorCamera(Vector2 center, float zoom) { exCenter = center; exZoom = zoom; ApplyCameras(); }
        public void SetInteriorCamera(Vector2 center, float zoom) { inCenter = center; inZoom = zoom; ApplyCameras(); }
        public void SetBridgeCamera(Vector2 center, float zoom) { brCenter = center; brZoom = zoom; ApplyCameras(); }

        void ApplyCameras()
        {
            float fit = FitScale();
            Apply(Exterior, exCenter, exZoom, fit);
            Apply(Interior, inCenter, inZoom, fit);
            Apply(Bridge, brCenter, brZoom, fit);
        }

        static void Apply(RectTransform c, Vector2 center, float zoom, float fit)
        {
            if (c == null) return;
            float s = fit * zoom;
            c.localScale = new Vector3(s, s, 1f);
            c.anchoredPosition = -NormToLocal(center) * s;
        }

        void LateUpdate() => ApplyCameras(); // 해상도 변경·창 크기 변경에도 같은 변환 유지

        // ───────── 키보드 내비게이션(공간 기준 + Tab 순서) ─────────

        void BuildNavigation()
        {
            foreach (var h in Hotspots.Values)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = Nearest(h, Vector2.up);
                nav.selectOnDown = Nearest(h, Vector2.down);
                nav.selectOnLeft = Nearest(h, Vector2.left);
                nav.selectOnRight = Nearest(h, Vector2.right);
                h.Button.navigation = nav;
            }
        }

        Selectable Nearest(CafeHotspot from, Vector2 dir)
        {
            Selectable best = null;
            float bestScore = float.MaxValue;
            var a = NormToLocal(from.Feature.HotspotRect.center);
            foreach (var h in Hotspots.Values)
            {
                if (h == from) continue;
                var d = NormToLocal(h.Feature.HotspotRect.center) - a;
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float across = Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x)));
                float score = along + across * 2f; // 같은 줄/열을 우선
                if (score < bestScore) { bestScore = score; best = h.Button; }
            }
            return best;
        }

        public CafeHotspot NextInOrder(CafeHotspot current, int step)
        {
            if (NavOrder.Count == 0) return null;
            int i = current != null ? NavOrder.IndexOf(current) : -1;
            i = ((i + step) % NavOrder.Count + NavOrder.Count) % NavOrder.Count;
            return NavOrder[i];
        }

        /// <summary>검수·시험용: 클릭 영역의 화면 픽셀 사각형(현재 변환 적용).</summary>
        public Rect ScreenRectOf(string id)
        {
            if (!Hotspots.TryGetValue(id, out var h)) return Rect.zero;
            var c = new Vector3[4];
            ((RectTransform)h.transform).GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
    }

    /// <summary>한 가구의 클릭 영역 + 이름표 + 포커스 표시(평소엔 이름표만, hover/선택 시 얇은 테두리·짧은 설명).</summary>
    public sealed class CafeHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ICanvasRaycastFilter
    {
        public CafeTitleLayout.Feature Feature { get; private set; }
        public Button Button { get; private set; }
        public RectTransform Nameplate { get; private set; }
        public Text LabelText { get; private set; }
        GameObject focusRoot;
        bool hovered, selected;
        public bool FocusVisible => focusRoot != null && focusRoot.activeSelf;

        static readonly Color FocusColor = new Color(1f, 0.86f, 0.55f, 0.9f);

        public static CafeHotspot Create(RectTransform interior, string buttonName, CafeTitleLayout.Feature f, System.Action onClick)
        {
            var rt = CafeTitleScene.Place(interior, buttonName, f.HotspotRect);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // 보이지 않는 클릭 영역(가구 그림이 보이는 그대로)
            var hs = rt.gameObject.AddComponent<CafeHotspot>();
            hs.Feature = f;
            hs.Button = rt.gameObject.AddComponent<Button>();
            hs.Button.transition = Selectable.Transition.None; // 그림 위 색칠 없음 — 포커스는 테두리로만
            hs.Button.targetGraphic = hit;
            UiFactory.AddFocusSfx(hs.Button);
            hs.Button.onClick.AddListener(() => { AudioService.PlaySfx("ui_confirm"); onClick?.Invoke(); });

            // 포커스 테두리(3px, 가구 둘레)
            hs.focusRoot = new GameObject("Focus", typeof(RectTransform));
            var frt = (RectTransform)hs.focusRoot.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = new Vector2(-6f, -6f); frt.offsetMax = new Vector2(6f, 6f);
            Edge(frt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 3)); // 위
            Edge(frt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 3)); // 아래
            Edge(frt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0)); // 왼
            Edge(frt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(3, 0)); // 오른
            hs.focusRoot.SetActive(false);

            // 이름표: 가구 옆/아래 작은 명패(Unity 텍스트). 클릭 영역과 같은 장면 변환.
            var plate = new GameObject("Nameplate", typeof(RectTransform));
            var prt = (RectTransform)plate.transform;
            prt.SetParent(interior, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            var bg = plate.AddComponent<Image>();
            bg.color = new Color(0.13f, 0.085f, 0.06f, 0.88f);
            bg.raycastTarget = false;
            var label = UiFactory.AddText(UiFactory.Stretch("Label", prt), f.Label, 26, TextAnchor.MiddleCenter, new Color(0.98f, 0.92f, 0.8f));
            label.raycastTarget = false;
            float w = Mathf.Max(64f, label.preferredWidth + 28f), h = 40f;
            prt.sizeDelta = new Vector2(w, h);
            var anchor = CafeTitleScene.NormToLocal(f.LabelAnchor);
            switch (f.LabelSide)
            {
                case "above": prt.pivot = new Vector2(0.5f, 0f); break;
                case "left": prt.pivot = new Vector2(1f, 0.5f); break;
                case "right": prt.pivot = new Vector2(0f, 0.5f); break;
                default: prt.pivot = new Vector2(0.5f, 1f); break;
            }
            prt.anchoredPosition = anchor;
            hs.Nameplate = prt;
            hs.LabelText = label;

            // 짧은 목적 설명(포커스 때만, 이름표 아래)
            if (!string.IsNullOrEmpty(f.Desc))
            {
                var drt = UiFactory.Place("Desc", prt, new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(Mathf.Max(w, 300f), 30f));
                drt.pivot = new Vector2(0.5f, 1f);
                var dbg = drt.gameObject.AddComponent<Image>();
                dbg.color = new Color(0.08f, 0.05f, 0.04f, 0.85f);
                dbg.raycastTarget = false;
                var dt = UiFactory.AddText(UiFactory.Stretch("Text", drt), f.Desc, 20, TextAnchor.MiddleCenter, new Color(0.95f, 0.85f, 0.65f));
                dt.raycastTarget = false;
                drt.SetParent(hs.focusRoot.transform, true); // 포커스와 함께 켜고 끈다
            }
            return hs;
        }

        static void Edge(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 thick)
        {
            var rt = new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x == aMax.x ? aMin.x : 0.5f, aMin.y == aMax.y ? aMin.y : 0.5f);
            rt.sizeDelta = thick;
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = FocusColor;
            img.raycastTarget = false;
        }

        void Refresh() { if (focusRoot != null) focusRoot.SetActive((hovered || selected) && Button.interactable); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Refresh(); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
        void OnDisable() { hovered = selected = false; if (focusRoot != null) focusRoot.SetActive(false); }

        /// <summary>다각형 클릭 영역: 외접 사각형 안이라도 다각형 밖이면 이 가구를 누르지 않는다(이웃 가구 침범 방지).</summary>
        public bool IsRaycastLocationValid(Vector2 sp, Camera cam)
        {
            if (Feature == null || Feature.Polygon == null || Feature.Polygon.Length < 3) return true;
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rt.parent, sp, cam, out var local)) return false;
            var n = CafeTitleScene.LocalToNorm(local);
            return PointInPolygon(n, Feature.Polygon);
        }

        public static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
