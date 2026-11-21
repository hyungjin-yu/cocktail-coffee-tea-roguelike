using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-20 카페 메뉴 접근 연출: 물체 클릭/Enter → 그 물체로 다가가는 시점 이동 → 목적에 맞는 조작 프레임(아트 계약 layout feature.approach) →
    /// 기존 메뉴 열기. 메뉴를 닫으면 같은 경로로 원위치(조작 프레임 역재생) 후 입력을 다시 받는다.
    /// - 진행 중에는 실내 물체·Tab/방향키가 막혀 중복 입력·다른 메뉴 선택이 없다.
    /// - 시작한 입력이 떼어진 뒤의 새 입력 = 건너뛰기(끝 상태로), Esc = 취소(메뉴를 열지 않고 원위치). 메뉴는 모든 입력이 떼어진 다음 프레임에 열린다(입력 누출 없음).
    /// - 저이펙트: 카메라 이동 없이 조작 마지막 프레임만 짧게 보이고 연다.
    /// - approach 계약이 없는 물체는 기존처럼 바로 연다(임의 확대/페이드로 대체하지 않음).
    /// 판정·저장과 무관한 표시 전용, 무음.
    /// </summary>
    public sealed class CafeMenuApproach : MonoBehaviour
    {
        public enum State { Idle, Approaching, Acting, Opening, Opened, Returning }

        CafeTitleScene scene;
        Func<bool> lowEffects;
        public State Current { get; private set; } = State.Idle;
        public string ActiveId { get; private set; }
        /// <summary>실내 입력을 막아야 하는지(Idle이 아니면 막음; 메뉴가 열린 동안은 메뉴 화면이 입력을 받는다).</summary>
        public bool Busy => Current != State.Idle;
        /// <summary>검수용: 마지막 연출에서 실제로 쓴 접근 데이터 유무·건너뜀·취소.</summary>
        public bool LastUsedApproach { get; private set; }
        public bool LastSkipped { get; private set; }
        public bool LastCancelled { get; private set; }
        public event Action<string> Returned;

        Coroutine running;
        Image actionImage;
        Sprite actionOriginal;
        Sprite[] actionFrames;
        bool skipRequested, cancelRequested;

        public static CafeMenuApproach Attach(GameObject host, CafeTitleScene scene, Func<bool> lowEffects)
        {
            var a = host.AddComponent<CafeMenuApproach>();
            a.scene = scene;
            a.lowEffects = lowEffects;
            return a;
        }

        /// <summary>물체 확정(클릭·Enter). 진행 중이면 무시. open은 기존 메뉴 열기(같은 동작). 접근 계약이 없으면 바로 open.</summary>
        public bool Begin(string id, Action open)
        {
            if (Current != State.Idle || scene == null) return false;
            var f = scene.Layout.FindFeature(id);
            var ap = f?.Approach;
            ActiveId = id; LastSkipped = LastCancelled = false;
            LastUsedApproach = ap != null;
            if (ap == null) { open?.Invoke(); return true; }
            TitleTrace.Log($"approach begin {id}", true);
            running = StartCoroutine(Run(f, ap, open));
            return true;
        }

        /// <summary>메뉴를 닫고 카페로 돌아왔을 때(또는 열기가 취소됐을 때): 같은 경로로 원위치.</summary>
        public void Return()
        {
            if (Current != State.Opened && Current != State.Opening) return;
            var f = scene.Layout.FindFeature(ActiveId);
            if (f?.Approach == null) { Finish(); return; }
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(RunReturn(f.Approach));
        }

        static bool AnyHeld()
        {
            var kb = Keyboard.current; var m = Mouse.current; var t = Touchscreen.current;
            return (kb != null && kb.anyKey.isPressed) || (m != null && (m.leftButton.isPressed || m.rightButton.isPressed)) ||
                   (t != null && t.primaryTouch.press.isPressed);
        }

        static bool NewPress(out bool escape)
        {
            var kb = Keyboard.current; var m = Mouse.current; var t = Touchscreen.current;
            escape = kb != null && kb.escapeKey.wasPressedThisFrame;
            return (kb != null && kb.anyKey.wasPressedThisFrame) || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) ||
                   (t != null && t.primaryTouch.press.wasPressedThisFrame);
        }

        IEnumerator Run(CafeTitleLayout.Feature f, CafeTitleLayout.Approach ap, Action open)
        {
            Current = State.Approaching;
            skipRequested = cancelRequested = false;
            EventSystem.current?.SetSelectedGameObject(null);
            scene.SetNameplatesVisible(false);
            if (scene.Hotspots.TryGetValue(f.Id, out var hs)) hs.SuppressLight(true);
            BindAction(ap);
            bool low = lowEffects != null && lowEffects();
            bool armed = false; // 확정한 입력이 떼어진 뒤에만 건너뛰기·취소를 받는다

            // 1) 시점 이동(저이펙트: 이동 없음)
            float dur = low ? 0f : Mathf.Max(0f, ap.Seconds);
            for (float t = 0f; t < dur && !skipRequested && !cancelRequested; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                PollSkip(ref armed);
                float k = Smooth(t / dur);
                scene.SetInteriorCamera(Vector2.Lerp(Overview, ap.Center, k), Mathf.Lerp(1f, ap.Zoom, k));
                yield return null;
            }
            if (cancelRequested) { yield return Cancel(ap); yield break; }
            if (!low) scene.SetInteriorCamera(ap.Center, ap.Zoom);

            // 2) 조작 프레임(저이펙트·건너뛰기: 마지막 프레임만)
            Current = State.Acting;
            if (actionFrames != null && actionFrames.Length > 0)
            {
                if (low || skipRequested) SetFrame(actionFrames.Length - 1);
                else
                {
                    float spf = 1f / Mathf.Max(1f, ap.Fps);
                    for (int i = 0; i < actionFrames.Length && !skipRequested && !cancelRequested; i++)
                    {
                        SetFrame(i);
                        for (float t = 0f; t < spf && !skipRequested && !cancelRequested; t += Time.unscaledDeltaTime) { PollSkip(ref armed); yield return null; }
                    }
                    if (cancelRequested) { yield return Cancel(ap); yield break; }
                    SetFrame(actionFrames.Length - 1);
                }
            }
            float hold = low ? 0.15f : skipRequested ? 0f : ap.HoldSeconds;
            for (float t = 0f; t < hold && !skipRequested && !cancelRequested; t += Time.unscaledDeltaTime) { PollSkip(ref armed); yield return null; }
            if (cancelRequested) { yield return Cancel(ap); yield break; }
            LastSkipped = skipRequested;

            // 3) 모든 입력이 떼어지고 한 프레임 더 → 기존 메뉴 열기(확정·건너뛰기 입력이 메뉴로 새지 않게)
            Current = State.Opening;
            while (AnyHeld()) yield return null;
            yield return null;
            Current = State.Opened;
            running = null;
            TitleTrace.Log($"approach open {f.Id} skipped={LastSkipped}");
            open?.Invoke();
        }

        void PollSkip(ref bool armed)
        {
            if (!armed) { if (!AnyHeld()) armed = true; return; }
            if (NewPress(out bool esc)) { if (esc) cancelRequested = true; else skipRequested = true; }
        }

        IEnumerator Cancel(CafeTitleLayout.Approach ap)
        {
            LastCancelled = true;
            TitleTrace.Log($"approach cancel {ActiveId}");
            yield return RunReturn(ap);
        }

        IEnumerator RunReturn(CafeTitleLayout.Approach ap)
        {
            Current = State.Returning;
            bool low = lowEffects != null && lowEffects();
            // 조작 되돌리기(역재생) → 원위치 시점
            if (actionFrames != null && actionFrames.Length > 0 && !low && ap.ReverseOnReturn)
            {
                float spf = 1f / Mathf.Max(1f, ap.Fps);
                for (int i = actionFrames.Length - 1; i >= 0; i--) { SetFrame(i); for (float t = 0f; t < spf; t += Time.unscaledDeltaTime) yield return null; }
            }
            RestoreAction();
            float dur = low ? 0f : Mathf.Max(0f, ap.ReturnSeconds);
            scene.GetInteriorCamera(out var from, out var fromZoom);
            for (float t = 0f; t < dur; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                float k = Smooth(t / dur);
                scene.SetInteriorCamera(Vector2.Lerp(from, Overview, k), Mathf.Lerp(fromZoom, 1f, k));
                yield return null;
            }
            scene.SetInteriorCamera(Overview, 1f);
            // 닫기 입력(Esc·클릭)이 떼어진 뒤에 카페 입력을 연다
            while (AnyHeld()) yield return null;
            yield return null;
            Finish();
        }

        void Finish()
        {
            string id = ActiveId;
            RestoreAction();
            scene.SetInteriorCamera(Overview, 1f);
            scene.SetNameplatesVisible(true);
            if (id != null && scene.Hotspots.TryGetValue(id, out var hs)) hs.SuppressLight(false);
            Current = State.Idle;
            running = null;
            TitleTrace.Log($"approach returned {id}");
            Returned?.Invoke(id);
        }

        /// <summary>QA·시험: 진행 중 연출을 즉시 끝 상태로(건너뛰기와 같은 경로).</summary>
        public void SkipForQa() { if (Current == State.Approaching || Current == State.Acting) skipRequested = true; }
        public void CancelForQa() { if (Current == State.Approaching || Current == State.Acting) cancelRequested = true; }

        GameObject patchGo;
        Image hiddenLayer;

        void BindAction(CafeTitleLayout.Approach ap)
        {
            actionImage = null; actionFrames = null; actionOriginal = null; hiddenLayer = null;
            if (ap.Frames == null || ap.Frames.Length == 0 || string.IsNullOrEmpty(ap.Layer)) return;
            var layer = scene.Interior.Find(ap.Layer) as RectTransform;
            var layerImg = layer != null ? layer.GetComponent<Image>() : null;
            if (layerImg == null) return;
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var p in ap.Frames) { var s = CafeTitleLayout.LoadSprite(p); if (s != null) list.Add(s); }
            if (list.Count != ap.Frames.Length) return; // 일부 누락이면 조작 없이(그림 섞임 방지)
            actionFrames = list.ToArray();
            if (ap.PatchRect.HasValue)
            {
                // 패치: 원 물체 레이어 바로 위(앞 물체보다 아래)에 같은 실내 좌표로 → 가림 순서 유지
                var rt = CafeTitleScene.Place(scene.Interior, ap.Layer + "_action", ap.PatchRect.Value);
                rt.SetSiblingIndex(layer.GetSiblingIndex() + 1);
                actionImage = rt.gameObject.AddComponent<Image>();
                actionImage.raycastTarget = false;
                patchGo = rt.gameObject;
                if (ap.HideLayer == true) { hiddenLayer = layerImg; layerImg.enabled = false; }
            }
            else
            {
                actionImage = layerImg; // 전체 프레임: 원 레이어 그림을 교체
                actionOriginal = layerImg.sprite;
            }
            SetFrame(0);
        }

        void SetFrame(int i) { if (actionImage != null && actionFrames != null) actionImage.sprite = actionFrames[Mathf.Clamp(i, 0, actionFrames.Length - 1)]; }

        void RestoreAction()
        {
            if (actionImage != null && actionOriginal != null) actionImage.sprite = actionOriginal;
            if (hiddenLayer != null) { hiddenLayer.enabled = true; hiddenLayer = null; }
            if (patchGo != null) { Destroy(patchGo); patchGo = null; actionImage = null; }
        }

        public int ActionFrameCount => actionFrames != null ? actionFrames.Length : 0;
        public Sprite CurrentActionSprite => actionImage != null ? actionImage.sprite : null;

        static readonly Vector2 Overview = new Vector2(0.5f, 0.5f);
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
