using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-11 배치 모드 PlayMode 시험은 무음으로 돌린다(사용자 PC 소리 방해 금지). 시험 어셈블리 전용 — 게임 빌드에는 들어가지 않는다.
    /// 오디오 판정은 AudioService 호출 기록으로 하므로 리스너 음량 0은 시험 결과에 영향 없음.</summary>
    static class QuietBatchAudio
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Mute()
        {
            if (Application.isBatchMode) AudioListener.volume = 0f;
        }
    }
}
