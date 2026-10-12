using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 08:118 저장된 해상도/표시모드 부트 적용 확인. 첫 프레임의 Screen.SetResolution이 표시모드만 바꾸고 크기를 놓치는 경우
    /// (r4 QA 2026-10-05: 저장 1280×720 창 모드 → 실제 1920×1080 창, 화면 밖으로 넘침)를 몇 프레임 뒤 실제 창 상태로 확인해
    /// 확정값을 다시 적용한다. 최대 3회, 결과는 [Settings] 로그. 시간정지(timeScale 0)와 무관하게 실제 시간으로 기다린다.
    /// </summary>
    public class DisplayBootVerifier : MonoBehaviour
    {
        public const int MaxAttempts = 3;
        static DisplayBootVerifier instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void Begin()
        {
            if (instance != null) return;
            var go = new GameObject("DisplayBootVerifier");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DisplayBootVerifier>();
        }

        IEnumerator Start()
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // 표시모드 전환이 끝날 시간을 준다(프레임 2개 + 0.25초).
                yield return null;
                yield return null;
                yield return new WaitForSecondsRealtime(0.25f);
                if (SettingsService.VerifyCommittedDisplay(attempt)) break;
                if (attempt == MaxAttempts)
                {
                    // 마지막 재적용 결과도 한 번 더 기록만 한다(더 이상 재적용하지 않음).
                    yield return new WaitForSecondsRealtime(0.5f);
                    var d = SettingsService.Display;
                    Debug.LogWarning($"[Settings] display after {MaxAttempts} reapply: actual {d.CurrentWidth}x{d.CurrentHeight} " +
                                     $"{(d.CurrentWindowed ? "windowed" : "fullscreen")}");
                }
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
