using System;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-10-12 Android(구글 플레이) 대응: 터치 UI·모바일 수명주기·설정 표시 분기를 한곳에서 판단한다.
    /// 데스크톱은 기본 false(기존 동작 그대로). Android/iOS 플레이어는 true.
    /// PC에서 터치 배치를 확인할 때는 실행 인자 -cct-touch-ui(검증용), 시험은 ForceForTests.
    /// </summary>
    public static class MobilePlatform
    {
        static bool? forced;
        static bool argChecked, argTouch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { forced = null; argChecked = false; argTouch = false; }

        /// <summary>터치 조작(가상 조이스틱·버튼)과 모바일 화면 규칙을 쓸지.</summary>
        public static bool IsTouchUI
        {
            get
            {
                if (forced.HasValue) return forced.Value;
                if (Application.isMobilePlatform) return true;
                if (!argChecked)
                {
                    argChecked = true;
                    argTouch = Array.IndexOf(Environment.GetCommandLineArgs(), "-cct-touch-ui") >= 0;
                }
                return argTouch;
            }
        }

        /// <summary>실제 모바일 OS(해상도/창 모드 개념이 없는 기기)인지. 터치 QA 인자는 포함하지 않는다.</summary>
        public static bool IsMobileOs => forced ?? Application.isMobilePlatform;

        /// <summary>시험 전용: null이면 실제 판정으로 되돌린다.</summary>
        public static void ForceForTests(bool? touch) => forced = touch;

        /// <summary>노치·펀치홀·제스처 영역을 뺀 안전 영역을 정규화 앵커(0..1)로. 화면 크기가 0이면 전체.</summary>
        public static void SafeAnchors(Rect safeArea, int screenWidth, int screenHeight, out Vector2 min, out Vector2 max)
        {
            if (screenWidth <= 0 || screenHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                min = Vector2.zero; max = Vector2.one; return;
            }
            min = new Vector2(Mathf.Clamp01(safeArea.xMin / screenWidth), Mathf.Clamp01(safeArea.yMin / screenHeight));
            max = new Vector2(Mathf.Clamp01(safeArea.xMax / screenWidth), Mathf.Clamp01(safeArea.yMax / screenHeight));
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = Vector2.one; }
        }
    }

    /// <summary>붙은 RectTransform을 Screen.safeArea 안으로 맞춘다(터치 UI일 때만 붙인다). 회전·창 크기 변화도 따라간다.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect lastSafe;
        Vector2Int lastSize;
        RectTransform rt;

        /// <summary>시험용 강제 안전 영역(픽셀). null이면 Screen.safeArea.</summary>
        public static Rect? OverrideForTests;

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void Update() => Apply();

        public void Apply()
        {
            if (rt == null) rt = (RectTransform)transform;
            var safe = OverrideForTests ?? Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == lastSafe && size == lastSize) return;
            lastSafe = safe; lastSize = size;
            MobilePlatform.SafeAnchors(safe, size.x, size.y, out var min, out var max);
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
