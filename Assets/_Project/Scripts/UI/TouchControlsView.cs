using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-10-12 Android 가로 화면 터치 조작. 터치 UI(MobilePlatform.IsTouchUI)일 때만 HUD 안(안전 영역)에 만든다.
    /// - 왼쪽 아래: 떠 있는 가상 조이스틱(누른 자리에 받침이 생기고 손잡이를 끈 방향·거리로 이동, 데드존 12%).
    /// - 오른쪽 아래: 연구 트리(K와 같은 경로 RequestCoffeeTree)·히든 효과(H와 같은 OpenHiddenDetails) 큰 버튼.
    /// - 일시정지는 HUD 기존 버튼(터치에서는 크게)을 쓴다. 공격은 자동이라 공격 버튼은 없다.
    /// 여러 손가락: 조이스틱은 처음 누른 포인터 id만 따라간다(버튼 탭과 동시 사용 가능).
    /// </summary>
    public class TouchControlsView : MonoBehaviour
    {
        public const float JoystickRadius = 130f;   // 캔버스 단위(1920×1080 기준)
        public const float DeadZone = 0.12f;

        /// <summary>플레이어 이동이 읽는 현재 조이스틱 값(크기 0..1). 터치 UI가 아니거나 손을 떼면 0.</summary>
        public static Vector2 Move { get; private set; }
        public static bool Held { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Move = Vector2.zero; Held = false; }

        GameManager game;
        CanvasGroup group;
        /// <summary>전투 중(보상·일시정지·히든 창이 없을 때)만 보인다.</summary>
        public bool Visible => group == null || group.alpha > 0.5f;
        RectTransform joystickBase, knob, zone;
        Text studyLabel;
        public Button StudyButton { get; private set; }
        public Button HiddenButton { get; private set; }

        /// <summary>끈 거리(캔버스 단위)를 조이스틱 값으로. 반지름에서 1로 포화, 데드존 아래는 0, 데드존 이후는 0..1로 다시 펼친다.</summary>
        public static Vector2 StickValue(Vector2 delta, float radius)
        {
            if (radius <= 0f) return Vector2.zero;
            float m = delta.magnitude / radius;
            if (m <= DeadZone) return Vector2.zero;
            float scaled = Mathf.Clamp01((m - DeadZone) / (1f - DeadZone));
            return delta.normalized * scaled;
        }

        public static TouchControlsView Create(RectTransform hudRoot, GameManager game)
        {
            var root = UiFactory.Stretch("TouchControls", hudRoot);
            root.SetAsFirstSibling(); // HUD 버튼(일시정지·히든 요약 등)이 먼저 터치를 받도록 맨 뒤에 그린다
            var view = root.gameObject.AddComponent<TouchControlsView>();
            view.game = game;
            view.group = root.gameObject.AddComponent<CanvasGroup>();

            // 조이스틱 영역: 왼쪽 45%, 아래 70%(상단 HUD 글자 영역은 피한다). 투명하지만 터치는 받는다.
            view.zone = UiFactory.Place("JoystickZone", root, Vector2.zero, Vector2.zero, Vector2.zero);
            view.zone.anchorMin = Vector2.zero; view.zone.anchorMax = new Vector2(0.45f, 0.7f);
            view.zone.offsetMin = Vector2.zero; view.zone.offsetMax = Vector2.zero;
            var zoneImg = UiFactory.AddImage(view.zone, new Color(0f, 0f, 0f, 0f));
            zoneImg.raycastTarget = true;
            var handler = view.zone.gameObject.AddComponent<JoystickZone>();
            handler.owner = view;

            // 받침·손잡이: 쉬는 위치는 왼쪽 아래(처음 보는 사람도 어디를 누를지 알 수 있게 흐리게 표시).
            view.joystickBase = UiFactory.Place("JoystickBase", view.zone, Vector2.zero, new Vector2(260f, 240f),
                Vector2.one * (JoystickRadius * 2f));
            view.joystickBase.pivot = new Vector2(0.5f, 0.5f);
            // 밝은 원목 바닥에서도 보이게: 금색 테두리 + 어두운 반투명 바탕(카페 메뉴판 팔레트).
            var rimImg = UiFactory.AddImage(view.joystickBase, new Color(0.95f, 0.75f, 0.35f, 0.75f));
            rimImg.sprite = SpriteFactory.Circle; rimImg.raycastTarget = false;
            var fill = UiFactory.Stretch("Fill", view.joystickBase);
            fill.offsetMin = new Vector2(6f, 6f); fill.offsetMax = new Vector2(-6f, -6f);
            var baseImg = UiFactory.AddImage(fill, new Color(0.10f, 0.07f, 0.09f, 0.55f));
            baseImg.sprite = SpriteFactory.Circle; baseImg.raycastTarget = false;
            view.knob = UiFactory.Place("JoystickKnob", view.joystickBase, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 110f);
            var knobImg = UiFactory.AddImage(view.knob, new Color(0.95f, 0.75f, 0.35f, 0.9f));
            knobImg.sprite = SpriteFactory.Circle; knobImg.raycastTarget = false;
            view.restPosition = view.joystickBase.anchoredPosition;

            // 오른쪽 아래 큰 버튼(최소 150 캔버스 단위 ≈ 1080p 폰에서 9mm 이상).
            view.StudyButton = view.AddRoundButton(root, "TouchStudyButton", new Vector2(-250f, 70f),
                game != null ? JobStudyTheme.TreeTitle(game.Job) : "커피 연구", () => { if (view.game != null) view.game.RequestCoffeeTree(); },
                out view.studyLabel);
            view.HiddenButton = view.AddRoundButton(root, "TouchHiddenButton", new Vector2(-70f, 230f), "히든",
                () => { if (view.game != null) view.game.OpenHiddenDetails(); }, out _);
            return view;
        }

        Vector2 restPosition;

        Button AddRoundButton(RectTransform root, string name, Vector2 pos, string label, System.Action onClick, out Text text)
        {
            var rt = UiFactory.Place(name, root, new Vector2(1f, 0f), pos, new Vector2(170f, 170f));
            var bg = UiFactory.AddImage(rt, new Color(0.12f, 0.09f, 0.12f, 0.78f));
            bg.sprite = SpriteFactory.Circle;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // 키보드 포커스 순환에 끼지 않는다
            UiFactory.ApplyHoverColors(button);
            UiFactory.AddFocusSfx(button);
            button.onClick.AddListener(() => onClick());
            var labelRt = UiFactory.Stretch("Label", rt);
            labelRt.offsetMin = new Vector2(14f, 14f); labelRt.offsetMax = new Vector2(-14f, -14f);
            text = UiFactory.AddText(labelRt, label, 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = 30;
            return button;
        }

        int pointerId = int.MinValue;

        internal void Press(PointerEventData e)
        {
            if (Held) return;
            pointerId = e.pointerId;
            Held = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, e.pressEventCamera, out var local);
            // 받침을 누른 자리로(영역 안 좌표, 영역 왼쪽 아래 기준).
            var size = zone.rect.size;
            joystickBase.anchoredPosition = local + new Vector2(size.x * zone.pivot.x, size.y * zone.pivot.y);
            knob.anchoredPosition = Vector2.zero;
            Move = Vector2.zero;
        }

        internal void Drag(PointerEventData e)
        {
            if (!Held || e.pointerId != pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickBase, e.position, e.pressEventCamera, out var local);
            var clamped = Vector2.ClampMagnitude(local, JoystickRadius);
            knob.anchoredPosition = clamped;
            Move = StickValue(local, JoystickRadius);
        }

        internal void Release(PointerEventData e)
        {
            if (e != null && e.pointerId != pointerId) return;
            ReleaseAll();
        }

        public void ReleaseAll()
        {
            pointerId = int.MinValue;
            Held = false;
            Move = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (joystickBase != null) joystickBase.anchoredPosition = restPosition;
        }

        void Update()
        {
            // 정지(일시정지·보상 창) 중에는 이동 입력을 쌓지 않는다. 앱이 백그라운드로 가면 OnApplicationPause에서 해제.
            bool active = game == null || (!game.IsPaused && game.State == GameState.Playing);
            if (!active && Held) ReleaseAll();
            // 2026-10-12 실기기: 카드 선택 등 보상 창 뒤로 조이스틱·버튼이 비쳤다 — 전투 중이 아니면 숨기고 입력도 받지 않는다.
            if (group != null && (group.alpha > 0.5f) != active)
            {
                group.alpha = active ? 1f : 0f;
                group.blocksRaycasts = active; group.interactable = active;
            }
            if (studyLabel != null && game != null && game.CoffeePoints > 0)
                studyLabel.text = $"{JobStudyTheme.TreeTitle(game.Job)}\n+{game.CoffeePoints}";
            else if (studyLabel != null && game != null)
                studyLabel.text = JobStudyTheme.TreeTitle(game.Job);
        }

        void OnApplicationPause(bool paused) { if (paused) ReleaseAll(); }
        void OnDisable() => ReleaseAll();
        void OnDestroy() { if (Held) { Held = false; Move = Vector2.zero; } }
    }

    /// <summary>조이스틱 영역의 포인터 이벤트를 TouchControlsView로 넘긴다(EventSystem·InputSystemUIInputModule 경로 — 마우스로도 시험 가능).</summary>
    public class JoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        internal TouchControlsView owner;
        public void OnPointerDown(PointerEventData e) { owner.Press(e); owner.Drag(e); }
        public void OnDrag(PointerEventData e) => owner.Drag(e);
        public void OnPointerUp(PointerEventData e) => owner.Release(e);
    }
}
