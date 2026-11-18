using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-18 카페 입장 연출(기존 2026-11-15 조명·소품 연출 대체). 같은 카페의 외부 전경 → 문짝이 경첩 기준으로 열리는 프레임(아트) →
    /// 카메라가 문턱 쪽으로 다가감(문 안쪽에 실내 일부) → 문턱을 넘는 순간 실내 장면으로 이어짐(도착 구도 → 기본 구도).
    /// - 연출 중에는 실내 클릭 영역이 눌리지 않는다. 아무 키·클릭이면 즉시 끝 상태로 가고, 그 입력(누르고 있는 동안 포함)은 메뉴를 누르지 않는다.
    /// - 같은 실행에서 타이틀로 돌아오면(재진입) 문 연출 없이 실내 0.25초 페이드. 저이펙트는 확대 없이 열린 문 → 0.3초 전환.
    /// - 판정·저장과 무관한 표시 전용, 무음.
    /// </summary>
    public class TitleIntro : MonoBehaviour
    {
        public const float FullSeconds = 2.6f, ShortSeconds = 0.25f, CalmSeconds = 0.7f;
        // 전체 연출 구간(초). bridge 뷰가 있으면 문턱 뒤에 bridge 구간이 끼고 전체 길이가 늘어난다.
        // 아트 layout.timing이 있으면 그 값(문 열림 프레임 길이·bridge 길이·cut 시각을 아트와 일치), 없으면 기본 구간.
        float HoldEnd = 0.45f, DoorEnd = 1.25f, PushEnd = 2.0f, CrossSeconds = 0.2f, SettleSeconds = 0.4f;
        float bridgeSeconds;
        float CrossEnd => PushEnd + bridgeSeconds + CrossSeconds;
        float FullTotal => CrossEnd + SettleSeconds;

        static bool playedThisSession;
        /// <summary>시험 전용: 세션 플래그 초기화.</summary>
        public static void ResetSessionForTests() => playedThisSession = false;

        CafeTitleScene scene;
        Text title;
        Color titleColor;
        float t, duration;
        bool calm, full;
        bool guard; // 건너뛴 입력이 아직 눌려 있는 동안 메뉴 입력 차단
        int guardFrames;
        public bool Finished { get; private set; }
        /// <summary>외관에서 입장 입력을 기다리는 중(자동 입장 없음). 문/입장하기 클릭·터치 또는 새 키 입력으로 입장.</summary>
        public bool IsWaiting { get; private set; }
        bool armed;      // 대기 시작(실행·포커스 복귀) 때 눌려 있던 키가 모두 떼어져야 새 입력으로 인정
        bool skipArmed;  // 입장을 시작한 입력이 떼어진 뒤의 새 입력만 건너뛰기
        GameObject enterUi;
        public Button EnterButton { get; private set; }
        public bool PlayedFull => full;
        public bool Calm => calm;
        public float Duration => duration;
        /// <summary>연출 중이거나 건너뛴 입력이 아직 떼어지지 않았다 — 실내 메뉴 입력을 막는다.</summary>
        public bool BlockingInput => IsWaiting || !Finished || guard;
        /// <summary>검수용: 현재 구간 이름.</summary>
        public string Phase { get; private set; } = "";
        public event System.Action Completed;

        public static TitleIntro Attach(CafeTitleScene scene, Text titleText)
        {
            var v = scene.gameObject.AddComponent<TitleIntro>();
            v.scene = scene;
            v.title = titleText;
            v.titleColor = titleText != null ? titleText.color : Color.white;
            v.calm = SettingsService.Current.effectsLevel == SettingsData.EffectsLow;
            v.full = !playedThisSession && !v.calm;
            bool calmFirst = !playedThisSession && v.calm;
            playedThisSession = true;
            v.bridgeSeconds = scene.Layout.Bridge.Count > 0 && scene.BridgeSprites != null ? scene.Layout.BridgeSeconds : 0f;
            var lay = scene.Layout;
            if (lay.HasTiming)
            {
                v.HoldEnd = lay.TimingHold;
                v.DoorEnd = v.HoldEnd + lay.TimingDoor;
                v.PushEnd = v.DoorEnd + lay.TimingApproach;
            }
            if (lay.BridgeBlend == "cut") v.CrossSeconds = 0f; // 명시적 쇼트 편집: 교차 없음
            bool arrivalIdentity = Mathf.Approximately(lay.ArrivalZoom, 1f) && (lay.ArrivalCenter - new Vector2(0.5f, 0.5f)).sqrMagnitude < 1e-6f;
            if (arrivalIdentity) v.SettleSeconds = 0f; // 고정 최종 구도로 바로 도착(카메라 이동 없음)
            v.duration = v.full ? v.FullTotal : calmFirst ? CalmSeconds : ShortSeconds;
            if (!v.full && !calmFirst) v.calm = false; // 재진입: 짧은 실내 페이드
            v.calmFirst = calmFirst;
            v.BuildEnterUi();
            if (v.full || calmFirst) v.EnterWaiting(); // 첫 진입: 간판·닫힌 문 외관에서 입장 입력 대기(영구)
            else v.Apply(0f);                          // 재진입: 실내 짧은 페이드
            return v;
        }

        /// <summary>외관 입장 안내: 문 위 투명 클릭 영역 + "입장하기" 명패 + "아무 키나 누르면 입장" 보조 안내(Unity 텍스트).</summary>
        void BuildEnterUi()
        {
            var door = scene.Layout.EntranceDoor;
            var rt = CafeTitleScene.Place(scene.Exterior, "EnterButton", door.Rect);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            EnterButton = rt.gameObject.AddComponent<Button>();
            EnterButton.transition = Selectable.Transition.None;
            EnterButton.targetGraphic = hit;
            EnterButton.onClick.AddListener(() => { if (IsWaiting && armed) StartEntering(); });
            enterUi = rt.gameObject;
            var plateAt = new Vector2(door.Rect.center.x, Mathf.Lerp(door.Rect.yMin, door.Rect.yMax, 0.58f));
            var plate = new GameObject("EnterPlate", typeof(RectTransform)).GetComponent<RectTransform>();
            plate.SetParent(rt, false);
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = CafeTitleScene.NormToLocal(plateAt) - rt.anchoredPosition;
            var bg = plate.gameObject.AddComponent<Image>();
            bg.color = new Color(0.13f, 0.085f, 0.06f, 0.92f);
            bg.raycastTarget = false;
            var label = UiFactory.AddText(UiFactory.Stretch("Label", plate), "입장하기", 34, TextAnchor.MiddleCenter, new Color(0.99f, 0.9f, 0.72f));
            label.raycastTarget = false;
            plate.sizeDelta = new Vector2(label.preferredWidth + 48f, 58f);
            var hintRt = UiFactory.Place("EnterHint", plate, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(520f, 34f));
            hintRt.pivot = new Vector2(0.5f, 1f);
            var hint = UiFactory.AddText(hintRt, "아무 키나 누르거나 문을 눌러 입장", 22, TextAnchor.MiddleCenter, new Color(0.98f, 0.92f, 0.8f));
            hint.raycastTarget = false;
            var o = hint.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            enterUi.SetActive(false);
        }

        void EnterWaiting()
        {
            IsWaiting = true; armed = false; Finished = false; guard = false;
            t = 0f;
            Apply(0f);
            Phase = "exterior_waiting";
            TitleTrace.Log("intro waiting");
            enterUi.SetActive(true);
            scene.ExteriorInteractable = true;
        }

        /// <summary>입장 시작(문/입장하기 클릭·터치·새 키). 이 입력이 떼어질 때까지 건너뛰기·메뉴로 이어지지 않는다.</summary>
        public void StartEntering()
        {
            if (!IsWaiting) return;
            TitleTrace.Log("intro start-entering", true);
            IsWaiting = false;
            skipArmed = false;
            enterUi.SetActive(false);
            scene.ExteriorInteractable = false;
            t = full ? HoldEnd : 0f; // 대기 자체가 외관 정지 구간 — 입력 즉시 문이 열리기 시작
            Apply(t);
        }

        bool calmFirst;

        /// <summary>QA 캡처 전용: 같은 화면에서 첫 진입 연출을 처음부터 다시(저이펙트면 저이펙트 첫 진입).</summary>
        public void ReplayForQa()
        {
            full = !calm; calmFirst = calm; duration = full ? FullTotal : CalmSeconds;
            QaFrozen = false;
            scene.Exterior.gameObject.SetActive(true);
            EnterWaiting();
        }

        /// <summary>QA 캡처 전용: 연출을 특정 시각으로 고정해 그린다(연속 프레임 캡처).</summary>
        public void SeekForQa(float seconds) { QaFrozen = true; t = Mathf.Clamp(seconds, 0f, duration); Apply(t); }
        /// <summary>QA 캡처 전용: true면 시간이 흐르지 않는다(SeekForQa 프레임 고정).</summary>
        public bool QaFrozen;

        public void Skip()
        {
            if (Finished) return;
            TitleTrace.Log("intro skip", true);
            if (IsWaiting) { IsWaiting = false; enterUi.SetActive(false); scene.ExteriorInteractable = false; }
            t = duration;
            Apply(duration);
            guard = true; guardFrames = 0;
        }

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool anyHeld = (kb != null && kb.anyKey.isPressed) || (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed));
            if (guard)
            {
                // 건너뛴 입력이 떼어지고 한 프레임이 더 지나야 메뉴 입력을 연다
                if (anyHeld) guardFrames = 0; else if (++guardFrames >= 2) guard = false;
                return;
            }
            if (Finished || QaFrozen) return;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            bool touching = touch != null && touch.primaryTouch.press.isPressed;
            anyHeld |= touching;
            bool keyPressed = kb != null && kb.anyKey.wasPressedThisFrame;
            bool pointerPressed = (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) ||
                                  (touch != null && touch.primaryTouch.press.wasPressedThisFrame);
            if (IsWaiting)
            {
                // 실행·포커스 복귀 때 이미 눌려 있던 키는 무시: 모두 떼어진 뒤의 새 키만. 클릭·터치는 문/입장하기 버튼(onClick)으로만.
                if (!armed) { if (!anyHeld) armed = true; return; }
                if (keyPressed) StartEntering();
                return;
            }
            // 입장 중: 시작한 입력이 떼어진 뒤의 새 입력만 건너뛰기
            if (!skipArmed) { if (!anyHeld) skipArmed = true; }
            else if (keyPressed || pointerPressed) { Skip(); return; }
            t += Mathf.Min(Time.unscaledDeltaTime, 0.1f); // 느린 프레임에서도 단계를 건너뛰지 않게 한 프레임 최대 0.1초
            Apply(t);
        }

        void Apply(float time)
        {
            if (scene == null) return;
            if (full) ApplyFull(time);
            else if (calmFirst) ApplyCalm(time);
            else ApplyShort(time);
            if (time >= duration && !Finished && !IsWaiting)
            {
                Finished = true;
                TitleTrace.Log($"intro finished t={time:0.000}");
                guard = true; guardFrames = 0; // 완료 직후 메뉴 입력 1프레임 이상 보호(눌린 입력이 있으면 떼어질 때까지)
                Phase = "interior";
                scene.ExteriorAlpha = 0f;
                scene.Exterior.gameObject.SetActive(false);
                if (scene.Bridge != null) scene.Bridge.gameObject.SetActive(false);
                scene.InteriorAlpha = 1f;
                scene.SetInteriorCamera(new Vector2(0.5f, 0.5f), 1f);
                if (title != null) title.color = titleColor;
                Completed?.Invoke();
            }
        }

        void ApplyFull(float time)
        {
            var layout = scene.Layout;
            var door = layout.EntranceDoor;
            scene.Exterior.gameObject.SetActive(time < CrossEnd);
            // 1) 외부 전경 → 2) 문 열림(경첩 기준 프레임) → 3) 문턱으로 다가감 → 4) 문턱 통과(실내로 이어짐) → 5) 도착 구도 정착
            float doorK = Mathf.Clamp01((time - HoldEnd) / (DoorEnd - HoldEnd));
            SetDoorFrame(doorK);
            float camK = PushEnd > HoldEnd ? Mathf.Clamp01((time - HoldEnd) / (PushEnd - HoldEnd)) : 1f;
            EvalExteriorCamera(Smooth(camK), out var c, out var z);
            scene.SetExteriorCamera(c, z);
            float cross;
            // blend=cut(명시적 쇼트 편집): 외부→bridge→실내를 교차 없이 끊어 바꾼다(다른 그림을 섞어 연속 공간처럼 꾸미지 않음)
            bool cut = layout.BridgeBlend == "cut";
            if (bridgeSeconds > 0f)
            {
                // 문턱 → 같은 평면에서 그린 bridge 뷰들을 시간 키대로 재생 → 마지막 뷰에서 실내 도착 구도로
                float bk = Mathf.Clamp01((time - PushEnd) / bridgeSeconds);
                bool inBridge = time >= PushEnd && time < PushEnd + bridgeSeconds;
                scene.Bridge.gameObject.SetActive(time >= PushEnd - 0.05f && time < CrossEnd);
                float inFade = cut ? (time >= PushEnd ? 1f : 0f) : Mathf.Clamp01((time - PushEnd) / 0.12f);
                scene.ExteriorAlpha = 1f - inFade;
                ApplyBridge(bk);
                cross = cut ? (time >= PushEnd + bridgeSeconds ? 1f : 0f) : Mathf.Clamp01((time - PushEnd - bridgeSeconds) / CrossSeconds);
                scene.BridgeAlpha = inBridge || time < PushEnd + bridgeSeconds ? inFade : 1f - cross;
                if (inBridge) Phase = "bridge";
            }
            else
            {
                cross = cut ? (time >= PushEnd ? 1f : 0f) : Mathf.Clamp01((time - PushEnd) / CrossSeconds);
                scene.ExteriorAlpha = 1f - cross;
            }
            scene.InteriorAlpha = cross;
            float settle = SettleSeconds > 0f ? Mathf.Clamp01((time - CrossEnd) / SettleSeconds) : (time >= CrossEnd ? 1f : 0f);
            float e = Smooth(settle);
            scene.SetInteriorCamera(Vector2.Lerp(layout.ArrivalCenter, new Vector2(0.5f, 0.5f), e), Mathf.Lerp(layout.ArrivalZoom, 1f, e));
            if (Phase != "bridge" || time >= PushEnd + bridgeSeconds)
                Phase = time < HoldEnd ? "exterior" : time < DoorEnd ? "door_opening" : time < PushEnd ? "approach_threshold" : time < CrossEnd ? "threshold" : time < FullTotal ? "arrival" : "interior";
            if (title != null) { var col = titleColor; col.a = titleColor.a * cross; title.color = col; }
        }

        void ApplyBridge(float k)
        {
            var views = scene.Layout.Bridge;
            if (views.Count == 0 || scene.BridgeSprites == null) return;
            int i = 0;
            while (i + 1 < views.Count && k >= views[i + 1].T) i++;
            int n = Mathf.Min(i + 1, views.Count - 1);
            float span = n > i ? views[n].T - views[i].T : 1f;
            float u = n > i ? Mathf.Clamp01((k - views[i].T) / Mathf.Max(0.0001f, span)) : 0f;
            scene.BridgeImage.sprite = scene.BridgeSprites[i];
            scene.BridgeImage.enabled = scene.BridgeSprites[i] != null;
            bool fade = scene.Layout.BridgeBlend == "crossfade" && n > i;
            scene.BridgeImageNext.enabled = fade && scene.BridgeSprites[n] != null;
            if (fade) { scene.BridgeImageNext.sprite = scene.BridgeSprites[n]; scene.BridgeImageNext.color = new Color(1f, 1f, 1f, u); }
            // 각 뷰에 적은 카메라만 보간(그려지지 않은 회전을 중심/확대로 꾸미지 않음)
            scene.SetBridgeCamera(Vector2.Lerp(views[i].Center, views[n].Center, fade ? u : 0f), Mathf.Lerp(views[i].Zoom, views[n].Zoom, fade ? u : 0f));
        }

        void ApplyCalm(float time)
        {
            // 저이펙트 첫 진입: 확대·점멸 없이 열린 문 전경 0.4초 → 0.3초 교차 전환
            scene.Exterior.gameObject.SetActive(time < duration);
            SetDoorFrame(IsWaiting ? 0f : 1f); // 대기 중엔 닫힌 문, 입장하면 확대 없이 열린 문
            scene.SetExteriorCamera(new Vector2(0.5f, 0.5f), 1f);
            scene.SetInteriorCamera(new Vector2(0.5f, 0.5f), 1f);
            float cross = Mathf.Clamp01((time - 0.4f) / 0.3f);
            scene.ExteriorAlpha = 1f - cross;
            scene.InteriorAlpha = cross;
            Phase = cross <= 0f ? "exterior_open" : "crossfade";
            if (title != null) { var col = titleColor; col.a = titleColor.a * cross; title.color = col; }
        }

        void ApplyShort(float time)
        {
            scene.Exterior.gameObject.SetActive(false);
            scene.SetInteriorCamera(new Vector2(0.5f, 0.5f), 1f);
            scene.InteriorAlpha = Mathf.Clamp01(time / ShortSeconds);
            Phase = "reentry_fade";
        }

        void SetDoorFrame(float k)
        {
            var frames = scene.DoorFrames;
            if (frames != null && frames.Length > 0)
            {
                int i = Mathf.Clamp(Mathf.FloorToInt(k * frames.Length), 0, frames.Length - 1);
                scene.DoorImage.sprite = frames[i];
                scene.DoorImage.color = Color.white;
            }
            else if (scene.DoorImage != null)
            {
                // 문 프레임 아트가 없을 때(임시 배치): 늘이기 대신 문짝을 투명하게 — 최종 연출로 보고하지 않는다
                var c = scene.DoorImage.color; c.a = 1f - k; scene.DoorImage.color = c;
            }
        }

        void EvalExteriorCamera(float k, out Vector2 center, out float zoom)
        {
            var keys = scene.Layout.ExteriorCamera;
            if (keys.Count == 0)
            {
                var d = scene.Layout.EntranceDoor.Rect;
                float endZoom = Mathf.Min(1f / Mathf.Max(0.05f, d.width), 1f / Mathf.Max(0.05f, d.height)) * 0.95f;
                center = Vector2.Lerp(new Vector2(0.5f, 0.5f), d.center, k);
                zoom = Mathf.Lerp(1f, endZoom, k * k);
                return;
            }
            center = keys[0].Center; zoom = keys[0].Zoom;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (k < keys[i].T || k > keys[i + 1].T) continue;
                float u = Mathf.InverseLerp(keys[i].T, keys[i + 1].T, k);
                center = Vector2.Lerp(keys[i].Center, keys[i + 1].Center, u);
                zoom = Mathf.Lerp(keys[i].Zoom, keys[i + 1].Zoom, u);
                return;
            }
            if (k >= keys[keys.Count - 1].T) { center = keys[keys.Count - 1].Center; zoom = keys[keys.Count - 1].Zoom; }
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
