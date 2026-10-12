using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 08:176 저장 실패 시 실제 시간 5초 간격 자동 재시도(진행 save.json과 settings.json 각각). 저장이 처음 실패한 순간에만 플레이 중 하나 생기고
    /// (DontDestroyOnLoad), 화면/런이 바뀌어도 dirty가 풀릴 때까지 PermanentProgress.Tick을 부른다.
    /// timeScale 0(Pause·보상창·결과)에서도 돌아야 해서 unscaledDeltaTime을 쓴다.
    /// </summary>
    public class SaveRetryRunner : MonoBehaviour
    {
        static SaveRetryRunner instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void EnsureRunning()
        {
            if (instance != null || !Application.isPlaying) return;
            var go = new GameObject("SaveRetryRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SaveRetryRunner>();
        }

        /// <summary>진행 저장과 설정 저장은 각자의 dirty/타이머로 따로 재시도한다(상태를 섞지 않음 — 러너만 공유).</summary>
        void Update()
        {
            PermanentProgress.Tick(Time.unscaledDeltaTime);
            SettingsService.Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
