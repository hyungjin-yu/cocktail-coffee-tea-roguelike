using UnityEngine;

namespace CCT
{
    /// <summary>적이 받은 피해의 표시 종류(판정·피해량과 무관한 연출 구분). 2026-11-09 타격감 개선.
    /// Normal = 무기의 일반 직접 타격, Critical = 치명타(×2), Heavy = 강한 공격(연속 마무리·증기 폭발·처형),
    /// Dot = 화상·중독 같은 지속 피해 틱, Effect = 반격 등 보조 효과.</summary>
    public enum HitKind { Normal, Critical, Heavy, Dot, Effect }

    /// <summary>
    /// 적 피격 연출 예산과 스타일. 기존 hit_spark·hit_enemy·FadeVfx만 재사용한다(새 아트/사운드 없음).
    /// - 연출은 실제로 0보다 큰 피해가 들어갔을 때만(무적·0 피해·빗나감에는 아무것도 없음 — 호출부 Enemy.TakeDamage가 보장).
    /// - 전역 timeScale 히트스톱 없음, 피격마다 카메라 흔들림 없음.
    /// - 한 프레임/짧은 구간에 생기는 불꽃 수를 제한하고(다수 적·관통), 효과 설정 "낮음"이면 절반·보조 링 생략.
    /// - 지속 피해는 소리·불꽃 없이 짧은 색조만(적마다 쿨다운) — 1초 틱이 화면과 소리를 도배하지 않게.
    /// </summary>
    public static class HitFeedback
    {
        public const int MaxSparksPerFrame = 6;
        public const int MaxSparksPerWindow = 18;
        public const float SparkWindow = 0.3f;
        public const float DotTintCooldown = 0.45f;

        static int frame = -1, frameCount;
        static float windowStart = -999f;
        static int windowCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { frame = -1; frameCount = 0; windowStart = -999f; windowCount = 0; }

        public static bool LowEffects => SettingsService.Current.effectsLevel == SettingsData.EffectsLow;

        /// <summary>불꽃 하나를 만들 예산이 있으면 예약하고 true. 게임 시간 기준이라 일시정지 중에는 창이 흐르지 않는다.</summary>
        public static bool TryReserveSpark()
        {
            int perFrame = LowEffects ? MaxSparksPerFrame / 2 : MaxSparksPerFrame;
            int perWindow = LowEffects ? MaxSparksPerWindow / 2 : MaxSparksPerWindow;
            if (Time.frameCount != frame) { frame = Time.frameCount; frameCount = 0; }
            if (Time.time - windowStart >= SparkWindow) { windowStart = Time.time; windowCount = 0; }
            if (frameCount >= perFrame || windowCount >= perWindow) return false;
            frameCount++;
            windowCount++;
            return true;
        }

        /// <summary>몸체 색조(SpriteRenderer.color는 곱셈이라 흰색은 원본 아트에서 보이지 않는다 — 종류별로 보이는 색을 쓴다).</summary>
        public static Color Tint(HitKind kind) => kind switch
        {
            HitKind.Critical => new Color(1f, 0.82f, 0.28f),
            HitKind.Heavy => new Color(1f, 0.58f, 0.34f),
            HitKind.Dot => new Color(1f, 0.8f, 0.72f),
            _ => new Color(1f, 0.62f, 0.6f),
        };

        public static float TintSeconds(HitKind kind) => kind switch
        {
            HitKind.Critical => 0.1f,
            HitKind.Heavy => 0.09f,
            HitKind.Dot => 0.05f,
            _ => 0.07f,
        };

        /// <summary>몸체만 잠깐 밀렸다 돌아오는 거리(월드). 판정·이동과 무관(Visual 자식만).</summary>
        public static float Recoil(HitKind kind) => kind switch
        {
            HitKind.Critical => 0.09f,
            HitKind.Heavy => 0.12f,
            HitKind.Dot => 0f,
            HitKind.Effect => 0.03f,
            _ => 0.05f,
        };

        public const float RecoilSeconds = 0.09f;

        /// <summary>종류별 효과음 ID(없으면 무음). hit_crit·hit_heavy는 기존 hit_enemy 음원을 음높이만 바꿔 쓴다(AudioService 별칭).</summary>
        public static string Sound(HitKind kind) => kind switch
        {
            HitKind.Critical => "hit_crit",
            HitKind.Heavy => "hit_heavy",
            HitKind.Dot => null,
            _ => "hit_enemy",
        };

        public static float SparkScale(HitKind kind) => kind switch
        {
            HitKind.Critical => 1.45f,
            HitKind.Heavy => 1.6f,
            HitKind.Effect => 0.8f,
            _ => 1f,
        };

        public static Color SparkColor(HitKind kind) => kind switch
        {
            HitKind.Critical => new Color(1f, 0.9f, 0.45f),
            HitKind.Heavy => new Color(1f, 0.78f, 0.55f),
            _ => Color.white,
        };

        /// <summary>테스트 전용.</summary>
        public static void ResetForTests() => ResetStatics();
    }
}
