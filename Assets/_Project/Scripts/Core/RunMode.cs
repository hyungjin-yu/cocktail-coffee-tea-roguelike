using UnityEngine;

namespace CCT
{
    /// <summary>2026-11-10 런 모드. Standard = 기존 진상 보스 영업(보스 처치 클리어·20분 제한), Endless = 끝없는 재료 모으기.</summary>
    public enum RunMode { Standard = 0, Endless = 1 }

    /// <summary>출전 전 선택한 모드(이번 실행 동안 기억 — 재시작은 같은 모드로 웨이브 1부터). 기본은 기존 모드.</summary>
    public static class RunModeSelection
    {
        public static RunMode Current { get; set; } = RunMode.Standard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = RunMode.Standard;

        public static string Name(RunMode mode) => mode == RunMode.Endless ? "끝없는 재료 모으기" : "진상 보스 영업";

        /// <summary>기록 문자열(빈 값·알 수 없는 값 = 기존 정규 모드로 호환).</summary>
        public static RunMode Parse(string s) => s == nameof(RunMode.Endless) ? RunMode.Endless : RunMode.Standard;

        /// <summary>출전 화면·결과 화면 공용 보상 안내(명세 문구).</summary>
        public const string EndlessRewardLine = "재료 획득 가능 · 스킨 재화 획득 불가";
        public const string EndlessRewardReason =
            "끝없는 재료 모으기는 스킨 재화(판매 코인)를 주지 않습니다. 이번 판 재료로 만든 음료는 마실 수 있지만 판매할 수 없습니다.";
        public const string StandardRewardLine = "진상 손님 처치로 클리어 · 모은 재료로 만든 음료 판매 가능";
    }

    /// <summary>
    /// 끝없는 재료 모으기 수치(첫 구현 조정값 — 기획/끝없는 재료 모으기.md). 전부 정수 계산, 장시간 상한 명시.
    /// - 생존 전투 시간 30초마다 다음 웨이브(일시정지·카드 선택 중에는 RunTime이 흐르지 않으므로 진행 안 함).
    /// - 웨이브 w 총 등장 수 = 8 + 2(w−1). 체력 % = 100 + 15(w−1), 공격 % = 100 + 8(w−1).
    /// - 기존 시간 체력 배율(Balance.EnemyHpScale)과 곱하지 않는다. 영구 해금 도전 배율(RunChallenge)은 기존처럼 Enemy.Init이 적용.
    /// - 수치 계산용 웨이브는 MaxScaledWave에서 멈춘다(오버플로/NaN 방지). 웨이브 번호 자체는 int 범위에서 계속 증가.
    /// </summary>
    public static class EndlessRules
    {
        public const float WaveSeconds = 30f;
        public const int FirstWaveCount = 8;
        public const int CountGrowthPerWave = 2;
        public const int HpPercentGrowthPerWave = 15;
        public const int AttackPercentGrowthPerWave = 8;
        /// <summary>수치 증가 상한 웨이브(30초×999 ≈ 8.3시간). 이후 웨이브는 같은 배율·같은 등장 수.</summary>
        public const int MaxScaledWave = 999;
        /// <summary>생성 대기 상한(압축된 정수 묶음 — 개별 개체 큐 없음). 넘치면 더 쌓지 않는다.</summary>
        public const int MaxPending = 5000;
        /// <summary>프레임당 생성 예산.</summary>
        public const int SpawnsPerFrame = 3;
        /// <summary>집중 계열은 3웨이브마다 커피 → 차 → 칵테일 순환.</summary>
        public const int WavesPerFocus = 3;
        /// <summary>집중 계열 가중치 70%, 나머지 계열 각 15%(정수 퍼센트, 합 100).</summary>
        public const int FocusWeightPercent = 70;
        public const int OtherWeightPercent = 15;

        static readonly MonsterFamily[] FocusOrder = { MonsterFamily.Coffee, MonsterFamily.Tea, MonsterFamily.Cocktail };

        static int Scaled(int wave) => Mathf.Clamp(wave, 1, MaxScaledWave);

        /// <summary>웨이브 번호(1부터). 경과 시간은 이 모드의 생존 전투 시간.</summary>
        public static int WaveAt(float seconds)
        {
            if (float.IsNaN(seconds) || seconds <= 0f) return 1;
            double w = System.Math.Floor(seconds / WaveSeconds) + 1d;
            return w >= int.MaxValue ? int.MaxValue : (int)w;
        }

        public static int WaveCount(int wave) => FirstWaveCount + CountGrowthPerWave * (Scaled(wave) - 1);
        public static int HpPercent(int wave) => 100 + HpPercentGrowthPerWave * (Scaled(wave) - 1);
        public static int AttackPercent(int wave) => 100 + AttackPercentGrowthPerWave * (Scaled(wave) - 1);
        public static float HpScale(int wave) => HpPercent(wave) / 100f;
        public static float AttackScale(int wave) => AttackPercent(wave) / 100f;

        /// <summary>다음 웨이브까지 남은 정수 초(올림, 최소 1).</summary>
        public static int SecondsToNextWave(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
            float into = seconds % WaveSeconds;
            return Mathf.Max(1, Mathf.CeilToInt(WaveSeconds - into));
        }

        public static MonsterFamily FocusFor(int wave)
        {
            int block = (Mathf.Max(1, wave) - 1) / WavesPerFocus;
            return FocusOrder[block % FocusOrder.Length];
        }

        /// <summary>이 웨이브가 집중 계열 전환 직전(다음 웨이브부터 계열이 바뀜)이면 true — 1웨이브 전 예고.</summary>
        public static bool IsFocusPreviewWave(int wave) => wave >= 1 && wave % WavesPerFocus == 0;

        /// <summary>계열별 정수 가중치(%). 집중 70 / 나머지 각 15. 중립은 계열 추첨 대상이 아니다(0).</summary>
        public static int WeightPercent(int wave, MonsterFamily family)
        {
            if (family == MonsterFamily.Neutral) return 0;
            return family == FocusFor(wave) ? FocusWeightPercent : OtherWeightPercent;
        }
    }
}
