using UnityEngine;

namespace CCT
{
    /// <summary>2026-11-10 런 모드. Standard = 기존 진상 보스 영업(보스 처치 클리어·20분 제한), Endless = 끝없는 재료 모으기.</summary>
    public enum RunMode { Standard = 0, Endless = 1 }

    /// <summary>2026-11-10 진상 보스 영업의 계열 스폰 표(사용자 승인 선택 옵션). JobTheme = 기존 직업 테마 표(기본, 그대로 보존).
    /// AllFamilies = 같은 추첨 결과의 아키타입 자리를 세 계열 중 하나로 바꿔 모든 음료 상성을 쓸 수 있게 한다(보상·능력치·보스는 기존 그대로).</summary>
    public enum SpawnTable { JobTheme = 0, AllFamilies = 1 }

    /// <summary>출전 전 선택한 모드(이번 실행 동안 기억 — 재시작은 같은 모드로 웨이브 1부터). 기본은 기존 모드.</summary>
    public static class RunModeSelection
    {
        public static RunMode Current { get; set; } = RunMode.Standard;
        /// <summary>진상 보스 영업에서 쓸 계열 표(이번 실행 동안 기억). 기본 = 기존 직업 테마 표.</summary>
        public static SpawnTable StandardTable { get; set; } = SpawnTable.JobTheme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = RunMode.Standard; StandardTable = SpawnTable.JobTheme; }

        public static string TableName(SpawnTable t) => t == SpawnTable.AllFamilies ? "세 계열 고르게" : "직업 테마";
        public static SpawnTable ParseTable(string s) => s == nameof(SpawnTable.AllFamilies) ? SpawnTable.AllFamilies : SpawnTable.JobTheme;
        /// <summary>시작 화면 짧은 안내(코디네이터 확정 A안: 런 중 음료 교체 없음).</summary>
        public const string NoSwapLine = "음료는 출발 때 한 잔만 마시며 런 중 교체 불가 · 집중 계열은 1웨이브 전 예고";
        /// <summary>2026-11-11 마스터 도전 출전 안내(런 중 제조·교체 가능).</summary>
        public const string MasterLine = "10웨이브 주기: 재료 모으기 1~5 → 박람회 6~8 → 보상 손님 9~10 · [B] 런 중 음료 제조(마시면 교체)";

        /// <summary>2026-11-11 표시명: 끝없는 모드(RunMode.Endless, 저장·기록 값 그대로)는 본편 작업명 '마스터 도전'. 세계 1등 대회는 후속(우승 표시 없음).</summary>
        public static string Name(RunMode mode) => mode == RunMode.Endless ? "마스터 도전" : "진상 보스 영업";

        /// <summary>기록 문자열(빈 값·알 수 없는 값 = 기존 정규 모드로 호환).</summary>
        public static RunMode Parse(string s) => s == nameof(RunMode.Endless) ? RunMode.Endless : RunMode.Standard;

        /// <summary>출전 화면·결과 화면 공용 보상 안내(명세 문구).</summary>
        public const string EndlessRewardLine = "재료 획득 가능 · 스킨 재화 획득 불가";
        public const string EndlessRewardReason =
            "마스터 도전은 스킨 재화(판매 코인)를 주지 않습니다. 이번 판 재료·박람회 보상으로 만든 음료는 마실 수 있지만 판매할 수 없습니다.";
        public const string StandardRewardLine = "진상 손님 처치로 클리어 · 모은 재료로 만든 음료 판매 가능";
    }

    /// <summary>
    /// 끝없는 재료 모으기 수치(첫 구현 조정값 — 기획/끝없는 재료 모으기.md). 2026-11-10 개정: **죽을 때까지 매 웨이브 증가**.
    /// - 생존 전투 시간 30초마다 다음 웨이브(일시정지·카드 선택 중에는 진행 안 함). 웨이브 번호는 long.
    /// - 웨이브 w 총 등장 수 = 8 + 2(w−1) (long). 체력 % = 100 + 15(w−1), 공격 % = 100 + 8(w−1) (double). 수치 증가에 웨이브 상한 없음.
    /// - 기존 시간 체력 배율(Balance.EnemyHpScale)과 곱하지 않는다. 영구 해금 도전 배율(RunChallenge)은 기존처럼 Enemy.Init이 적용.
    /// - 수치 표현 한계(명시 포화, 조용한 소실 아님):
    ///   · 적 최대 체력·공격 피해는 기존 정수 전투 경계(int)라서 2,147,483,647에서 포화한다. 기본 체력 150 기준 체력 %가
    ///     약 14억 3천만%가 되는 웨이브 약 9,500만(30초 × 9,500만 ≈ 90년) 이후다. 포화 웨이브는 HpSaturationWave로 계산할 수 있다.
    ///   · 웨이브 번호·등장 수·생성 대기는 long(9.2×10^18)에서 포화한다(SaturatedLong). 경과 시간은 double이라 2^53초까지 정확하다.
    /// - 생성 대기는 개별 개체 큐가 아니라 압축된 long 부채 장부(EnemySpawner.EndlessPending) — 상한으로 버리지 않는다.
    /// </summary>
    public static class EndlessRules
    {
        public const float WaveSeconds = 30f;
        public const int FirstWaveCount = 8;
        public const int CountGrowthPerWave = 2;
        public const int HpPercentGrowthPerWave = 15;
        public const int AttackPercentGrowthPerWave = 8;
        /// <summary>프레임당 생성 예산(동시 개체 상한과 별개 — 남은 양은 대기 장부에 남는다).</summary>
        public const int SpawnsPerFrame = 3;
        /// <summary>집중 계열은 3웨이브마다 커피 → 차 → 칵테일 순환.</summary>
        public const int WavesPerFocus = 3;
        /// <summary>집중 계열 가중치 70%, 나머지 계열 각 15%(정수 퍼센트, 합 100).</summary>
        public const int FocusWeightPercent = 70;
        public const int OtherWeightPercent = 15;
        public const long SaturatedLong = long.MaxValue;

        static readonly MonsterFamily[] FocusOrder = { MonsterFamily.Coffee, MonsterFamily.Tea, MonsterFamily.Cocktail };

        static long W(long wave) => wave < 1 ? 1 : wave;

        /// <summary>double → long(내림, 범위 밖·NaN은 명시 포화).</summary>
        public static long ToLongSaturated(double v)
        {
            if (double.IsNaN(v) || v <= 0d) return 0;
            if (v >= 9.2e18) return SaturatedLong;
            return (long)System.Math.Floor(v);
        }

        /// <summary>웨이브 번호(1부터). 경과 시간은 이 모드의 생존 전투 시간(double).</summary>
        public static long WaveAt(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0d) return 1;
            long w = ToLongSaturated(seconds / WaveSeconds);
            return w == SaturatedLong ? SaturatedLong : w + 1;
        }

        /// <summary>웨이브 w의 총 등장 수(long, 포화 명시).</summary>
        public static long WaveCount(long wave)
        {
            wave = W(wave);
            if (wave - 1 > (SaturatedLong - FirstWaveCount) / CountGrowthPerWave) return SaturatedLong;
            return FirstWaveCount + CountGrowthPerWave * (wave - 1);
        }

        /// <summary>웨이브 a..b(포함) 총 등장 수의 합(닫힌 식, decimal 계산 후 long 포화). a &gt; b면 0.</summary>
        public static long SumWaveCounts(long a, long b)
        {
            a = W(a); if (b < a) return 0;
            // 2026-11-11 decimal 범위(약 7.9×10^28) 밖 곱셈 OverflowException 방지: double 추정이 long 한계를 넘으면 계산 전에 포화.
            double approx = ((double)b - a + 1d) * (FirstWaveCount + CountGrowthPerWave * (((double)a - 1d + ((double)b - 1d)) / 2d));
            if (double.IsNaN(approx) || approx >= 9.2e18) return SaturatedLong;
            decimal n = (decimal)b - a + 1m;
            decimal sum = n * FirstWaveCount + CountGrowthPerWave * (((decimal)a - 1m + ((decimal)b - 1m)) * n / 2m);
            return sum >= SaturatedLong ? SaturatedLong : (long)sum;
        }

        public static double HpPercent(long wave) => 100d + HpPercentGrowthPerWave * (double)(W(wave) - 1);
        public static double AttackPercent(long wave) => 100d + AttackPercentGrowthPerWave * (double)(W(wave) - 1);
        public static float HpScale(long wave) => (float)(HpPercent(wave) / 100d);
        public static float AttackScale(long wave) => (float)(AttackPercent(wave) / 100d);

        /// <summary>표시용 정수 %(long, 포화 명시).</summary>
        public static long HpPercentInt(long wave) => ToLongSaturated(System.Math.Round(HpPercent(wave), System.MidpointRounding.AwayFromZero));
        public static long AttackPercentInt(long wave) => ToLongSaturated(System.Math.Round(AttackPercent(wave), System.MidpointRounding.AwayFromZero));

        /// <summary>기본 체력 baseHp인 적의 최대 체력이 int 경계에서 포화하기 시작하는 웨이브(문서·시험용).</summary>
        public static long HpSaturationWave(double baseHp)
        {
            if (baseHp <= 0d) return SaturatedLong;
            double pct = int.MaxValue / baseHp * 100d;
            return ToLongSaturated((pct - 100d) / HpPercentGrowthPerWave) + 1;
        }

        /// <summary>다음 웨이브까지 남은 정수 초(올림, 최소 1).</summary>
        public static int SecondsToNextWave(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0d) seconds = 0d;
            double into = seconds % WaveSeconds;
            return Mathf.Max(1, (int)System.Math.Ceiling(WaveSeconds - into - 1e-6));
        }

        public static MonsterFamily FocusFor(long wave)
        {
            long block = (W(wave) - 1) / WavesPerFocus;
            return FocusOrder[(int)(block % FocusOrder.Length)];
        }

        /// <summary>이 웨이브가 집중 계열 전환 직전(다음 웨이브부터 계열이 바뀜)이면 true — 1웨이브 전 예고.</summary>
        public static bool IsFocusPreviewWave(long wave) => wave >= 1 && wave % WavesPerFocus == 0;

        /// <summary>계열별 정수 가중치(%). 집중 70 / 나머지 각 15. 중립은 계열 추첨 대상이 아니다(0).</summary>
        public static int WeightPercent(long wave, MonsterFamily family)
        {
            if (family == MonsterFamily.Neutral) return 0;
            return family == FocusFor(wave) ? FocusWeightPercent : OtherWeightPercent;
        }

        /// <summary>표시용 long 문자열: 포화면 "최대(포화)".</summary>
        public static string Show(long v) => v == SaturatedLong ? "최대(포화)" : v.ToString();
    }
}
