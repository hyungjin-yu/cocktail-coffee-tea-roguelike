using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 09:71 "Unity AudioMixer의 Master/Music/SFX 그룹 분리"는 이제 실제 `.mixer` 에셋
    /// (Assets/_Project/Resources/Audio/CCTMixer.mixer, `AudioMixerBuilder`가 리플렉션으로 만듦 —
    /// UnityEditor.Audio.AudioMixerController 클래스 선언은 internal이지만 CreateMixerControllerAtPath/
    /// CreateNewGroup 등 필요한 멤버는 public이라 안전하게 호출 가능함을 실측 확인)에 진짜 Master(루트)/
    /// Music/SFX 3그룹이 있고, `AudioService`가 각 AudioSource.outputAudioMixerGroup을 거기로 라우팅한다.
    /// 이 클래스(AudioBus)는 그 위에서 "숫자 볼륨 계산"만 맡는다 — 그룹 페이더는 0dB(단위 이득)로 둬서
    /// 여기서 계산한 값(AudioSource.volume에 직접 곱함)을 그대로 통과시킨다. 즉 그룹 분리·라우팅은
    /// 실제 AudioMixer, 마스터/BGM/SFX 볼륨과 Pause 덕킹의 실시간 계산은 이 코드 버스가 담당하는
    /// 조합이다(08 설정 화면이 생기면 믹서의 Exposed Parameter로 옮기거나 지금 구조를 유지한 채
    /// 슬라이더만 연결하는 두 경로 다 가능).
    /// 08:117 값은 settings.json의 0..100 정수(SettingsService)가 기준이며, 부트와 설정 변경 시 /100으로 여기에 적용된다.
    /// 기본값은 마스터80/BGM60/SFX80.
    /// </summary>
    public static class AudioBus
    {
        [Range(0f, 1f)] public static float MasterVolume = 0.8f;
        [Range(0f, 1f)] public static float MusicVolume = 0.6f;
        [Range(0f, 1f)] public static float SfxVolume = 0.8f;

        /// <summary>09:139 — Pause/보상창 중 음악은 계속 재생하되 -8dB로 낮춘다.</summary>
        public static bool MusicDucked;
        const float DuckDb = -8f;

        public static float EffectiveMusicVolume =>
            MasterVolume * MusicVolume * (MusicDucked ? DbToLinear(DuckDb) : 1f);

        public static float EffectiveSfxVolume => MasterVolume * SfxVolume;

        static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);
    }
}
