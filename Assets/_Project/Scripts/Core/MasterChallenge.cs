using System;
using System.Collections.Generic;

namespace CCT
{
    /// <summary>2026-11-11 마스터 도전(Steam 본편, RunMode.Endless의 새 진행 루프) 10웨이브 주기 구간.</summary>
    public enum MasterSegment { Collect = 0, Fair = 1, Reward = 2 }

    /// <summary>마스터 도전 진행 단계. Wave = 웨이브 진행 중, AwaitChoice = 수집 5웨이브 완료 후 반복/박람회 선택 대기,
    /// ClearingForFair = 박람회 도전을 골랐지만 남은 적을 먼저 처리하는 중, Transition = NPC 현수막 설치(전투 정지).</summary>
    /// <summary>2026-11-12 Prep = 주기 시작(런 시작 포함) 전투 전 준비: 다음 웨이브 예고 확인·제조/마시기/제출 후 명시적 '전투 시작'(GameManager가 시간 정지).</summary>
    public enum MasterPhase { Wave = 0, AwaitChoice = 1, ClearingForFair = 2, Transition = 3, Prep = 4 }

    /// <summary>
    /// 마스터 도전 조정 가능한 첫 구현 수치(기획/박람회와_세계정상_도전모드.md). **사용자 확정 수치가 아닌 값**은 이름 옆에 '제안'으로 적는다.
    /// 확정: 10웨이브 주기(수집1~5·박람회6~8·보상9~10), 보상 몬스터 HP=절대 웨이브1 기준·공격력0·공격 행동 없음, R=N×p/C 식, 칵테일 고유 레시피 수 U로 공격력.
    /// </summary>
    public static class MasterRules
    {
        public const int CycleLength = 10;
        public const int CollectWaves = 5;   // 주기 내 1~5
        public const int FairWaves = 3;      // 6~8
        public const int RewardWaves = 2;    // 9~10

        /// <summary>현수막 설치 휴식(초, 실제 시간). 기획 범위 3~4초, 기본 3.5초.</summary>
        public const float PrepSecondsDefault = 3.5f;
        public const float PrepSecondsMin = 3f, PrepSecondsMax = 4f;
        public static float ClampPrep(float s) => float.IsNaN(s) ? PrepSecondsDefault : Math.Max(PrepSecondsMin, Math.Min(PrepSecondsMax, s));

        /// <summary>제출 1잔당 기본 감소율 p(퍼센트포인트). **제안값 5 — 사용자 미확정, 밸런스 검증 대상.**</summary>
        public const int SubmitPercentPerCupDefault = 5;
        /// <summary>칵테일 서로 다른 정식 레시피 1종당 공격력 증가(%). **제안값 2 — 사용자 미확정.**</summary>
        public const int CocktailAttackPercentPerUniqueDefault = 2;

        /// <summary>웨이브 목표 처치량(유한). 수집 = 기존 끝없는 모드 식(8+2(w−1)), 박람회 = 웨이브당 고정(제안), 보상 = 웨이브당 고정(제안).</summary>
        /// <summary>2026-11-12 사용자 지시(박람회가 바쁘게 — 기본 개체 수 최소 2배): 12 → 36(3배). 개체 HP·공격 배율은 그대로.</summary>
        public const int FairCountPerWave = 36;      // 제안(검토판)
        /// <summary>박람회 묶음 등장: 같은 쪽 가장자리에서 4명씩(한 명씩 줄 서는 느낌 대신 군중).</summary>
        public const int FairBurstSize = 4;
        public const int RewardCountPerWave = 8;     // 제안
        /// <summary>목표량을 몇 초 동안 나눠 풀지(생성 속도). 동시 상한·프레임 예산은 별도.</summary>
        public const float CollectReleaseSeconds = 20f, FairReleaseSeconds = 14f, RewardReleaseSeconds = 6f; // 2026-11-12 박람회 16 → 14초(36명 = 묶음 9개 ≈1.56초 간격)
        /// <summary>박람회 묶음 간격(초) = 방출 시간 / 묶음 수.</summary>
        public static float FairBurstInterval => FairReleaseSeconds / Math.Max(1, (FairCountPerWave + FairBurstSize - 1) / FairBurstSize);
        public const int SpawnsPerFrame = 3;

        /// <summary>박람회 작은 진상 손님: 원본 최종 보스와 분리한 별도 정의 수치(다수전용, 제안).</summary>
        public const float FairCustomerHp = 60f, FairCustomerContact = 7f, FairCustomerSpeed = 1.7f, FairCustomerRadius = 0.6f;
        /// <summary>원본 진상 손님 몸 아트(월드 높이 2.65)를 줄이는 시각 배율(작은 진상 손님 ≈ 1.2 월드).</summary>
        public const float FairCustomerVisualScale = 0.45f;

        /// <summary>박람회 성공 보상: 각 재료의 데이터 정의 기본 묶음(DrinkSupply.Row.bundle) × 이 배수. 제안 1.</summary>
        public const int FairRewardBundles = 1;

        static long W(long wave) => wave < 1 ? 1 : wave;

        /// <summary>주기 번호 C = floor((절대 웨이브−1)/10)+1.</summary>
        public static long CycleOf(long wave) => (W(wave) - 1) / CycleLength + 1;
        /// <summary>주기 내 위치 1~10 = ((wave−1) % 10)+1.</summary>
        public static int InCycle(long wave) => (int)((W(wave) - 1) % CycleLength) + 1;
        public static long CycleStart(long cycle) => (Math.Max(1L, cycle) - 1) * CycleLength + 1;

        public static MasterSegment SegmentOf(long wave)
        {
            int k = InCycle(wave);
            if (k <= CollectWaves) return MasterSegment.Collect;
            if (k <= CollectWaves + FairWaves) return MasterSegment.Fair;
            return MasterSegment.Reward;
        }

        /// <summary>박람회 내부 표기 1~3(박람회 웨이브가 아니면 0).</summary>
        public static int FairIndex(long wave) => SegmentOf(wave) == MasterSegment.Fair ? InCycle(wave) - CollectWaves : 0;
        public static int RewardIndex(long wave) => SegmentOf(wave) == MasterSegment.Reward ? InCycle(wave) - CollectWaves - FairWaves : 0;

        public static long TargetCount(long wave)
        {
            switch (SegmentOf(wave))
            {
                case MasterSegment.Fair: return FairCountPerWave;
                case MasterSegment.Reward: return RewardCountPerWave;
                default: return EndlessRules.WaveCount(wave);
            }
        }

        public static float ReleaseSeconds(MasterSegment s) =>
            s == MasterSegment.Fair ? FairReleaseSeconds : s == MasterSegment.Reward ? RewardReleaseSeconds : CollectReleaseSeconds;

        /// <summary>제출 가치 정수 point: 일반 잔 100, 최고 품질 커피(Q≥85) 150(코디네이터 승인 첫 조정값 — 품질 추가 가치 +50%).
        /// 실제 잔 수(N)와 유효 제출 가치를 분리한다. 반 잔·잔 복제는 없다.</summary>
        public const int CupPoints = 100, BestCoffeeCupPoints = 150;
        public static int SubmitPoints(DrinkKind kind, int quality) =>
            kind == DrinkKind.Coffee && quality >= DrinkQuality.BestMin ? BestCoffeeCupPoints : CupPoints;

        /// <summary>
        /// 제출 감소율 R(정수 %) = round(N × p / C)의 품질 가중 확장: round(points × p / (100 × C)). 일반 잔만이면 points = 100N이라 기본 식과 같다.
        /// 마지막에 한 번만 정수 반올림(중간값은 0에서 먼 쪽) — 미리보기와 실제 적용이 같은 정수. 상한 없음(100 이상이면 능력치 최소 1).
        /// 음수는 0. decimal 계산 후 long 포화.
        /// </summary>
        public static long ReductionPercentFromPoints(long points, int percentPerCup, long cycle)
        {
            if (points <= 0 || percentPerCup <= 0) return 0;
            decimal r = (decimal)points * percentPerCup / (CupPoints * (decimal)Math.Max(1L, cycle));
            r = Math.Round(r, MidpointRounding.AwayFromZero);
            return r >= long.MaxValue ? long.MaxValue : (long)r;
        }

        /// <summary>일반 잔 N개 기준 기본 식 R = round(N × p / C).</summary>
        public static long ReductionPercent(long submitted, int percentPerCup, long cycle) =>
            submitted <= 0 ? 0 : ReductionPercentFromPoints(submitted > long.MaxValue / CupPoints ? long.MaxValue : submitted * CupPoints, percentPerCup, cycle);

        /// <summary>감소 적용 계수(0~1). R ≥ 100이면 0 → 최종 능력치는 Apply의 최소 1.</summary>
        public static double ReductionFactor(long reductionPercent) =>
            reductionPercent <= 0 ? 1d : reductionPercent >= 100 ? 0d : (100d - reductionPercent) / 100d;

        /// <summary>최종 정수 능력치 = max(1, round(base × (1 − R/100))). base ≤ 0이면 0(보상 몬스터 공격력 0 보존 — 최소 1로 되살리지 않는다).</summary>
        public static int Apply(double baseValue, long reductionPercent)
        {
            if (double.IsNaN(baseValue) || baseValue <= 0d) return 0;
            double v = baseValue * ReductionFactor(reductionPercent);
            if (v >= int.MaxValue) return int.MaxValue;
            return Math.Max(1, (int)Math.Round(v, MidpointRounding.AwayFromZero));
        }

        /// <summary>칵테일 다양성 공격력 % = U × 종류당 증가율(정수). 기본 피해에 한 번 더하는 % (현재 프레임 공격력에 반복 곱하지 않음).</summary>
        public static int CocktailAttackPercent(int uniqueRecipes, int percentPerUnique) =>
            uniqueRecipes <= 0 || percentPerUnique <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)uniqueRecipes * percentPerUnique);
    }

    /// <summary>
    /// 마스터 도전 한 판의 진행 상태(순수 로직 — Unity 프레임 없이 시험 가능). 실제 생성·처치 확인은 MasterChallengeDirector가 알린다.
    /// 반복·도전 선택, 박람회 감소율 스냅샷, 다음 주기 전달 감소율, 박람회 성공 ID 1회를 관리한다.
    /// </summary>
    public sealed class MasterChallengeState
    {
        public long Wave { get; private set; } = 1;
        public MasterPhase Phase { get; private set; } = MasterPhase.Wave;
        public long Cycle => MasterRules.CycleOf(Wave);
        public MasterSegment Segment => MasterRules.SegmentOf(Wave);
        public long Target { get; private set; }
        public long Spawned { get; private set; }
        public long Cleared { get; private set; }
        /// <summary>이번 주기에 수집 1~5를 반복한 횟수(표시용, 난이도·주기·감소율에 영향 없음).</summary>
        public int RepeatCount { get; private set; }
        public int PercentPerCup { get; }

        /// <summary>현재 주기 박람회에 제출한 실제 잔 수 N(도전 접수 전까지 증가 가능).</summary>
        public long SubmittedThisCycle { get; private set; }
        /// <summary>현재 주기 유효 제출 가치(정수 point, 일반 잔 100 / 최고 품질 커피 150).</summary>
        public long SubmittedPointsThisCycle { get; private set; }
        /// <summary>박람회 도전 접수 때 확정한 이번 박람회 감소율(접수 전 -1).</summary>
        public long FairReductionSnapshot { get; private set; } = -1;
        /// <summary>직전 주기에서 확정돼 이번 주기 일반 수집에 쓰는 감소율(첫 주기 0). 누적 합산하지 않고 매 주기 교체.</summary>
        public long CarriedReduction { get; private set; }
        readonly HashSet<long> fairRewarded = new HashSet<long>();

        public event Action<long> WaveStarted;
        /// <summary>Prep 진입(웨이브 번호). 생성자에서 첫 Prep은 구독 전이라 Director가 직접 처리한다.</summary>
        public event Action<long> PrepStarted;
        public event Action<long> FairSucceeded;   // cycle
        public event Action<long> CycleCompleted;  // finished cycle

        /// <summary>true면 주기 시작 웨이브(1, 11, 21…)를 바로 시작하지 않고 Prep에서 기다린다(반복 선택은 선택 자체가 준비 지점이라 제외).</summary>
        public bool CyclePrep { get; }

        public MasterChallengeState(int percentPerCup = MasterRules.SubmitPercentPerCupDefault, bool cyclePrep = false)
        {
            PercentPerCup = Math.Max(0, percentPerCup);
            CyclePrep = cyclePrep;
            BeginWave(1, cyclePrep);
        }

        /// <summary>준비 지점(전투 정지): 주기 시작 Prep 또는 수집 5/5 뒤 선택 대기.</summary>
        public bool IsPreparing => Phase == MasterPhase.Prep || Phase == MasterPhase.AwaitChoice;

        /// <summary>Prep에서 '전투 시작'. 같은 웨이브(이미 정한 목표·계획)를 그대로 시작한다. 중복 입력은 false.</summary>
        public bool StartFromPrep()
        {
            if (Phase != MasterPhase.Prep) return false;
            Phase = MasterPhase.Wave;
            WaveStarted?.Invoke(Wave);
            return true;
        }

        public long Remaining => Math.Max(0, Target - Cleared);
        public long ToSpawn => Math.Max(0, Target - Spawned);

        /// <summary>지금 새로 생성되는 적이 받는 감소율: 수집 = 직전 주기 전달값, 박람회 = 이번 박람회 스냅샷, 보상 = 0(예외).</summary>
        public long ActiveReduction
        {
            get
            {
                switch (Segment)
                {
                    case MasterSegment.Fair: return Math.Max(0, FairReductionSnapshot);
                    case MasterSegment.Reward: return 0;
                    default: return CarriedReduction;
                }
            }
        }

        /// <summary>제출해서 접수 시 확정될 예상 감소율(표시용 — 실제 접수 때 같은 식으로 한 번 확정).</summary>
        public long PreviewFairReduction => MasterRules.ReductionPercentFromPoints(SubmittedPointsThisCycle, PercentPerCup, Cycle);
        /// <summary>품질 추가 가치(point, 표시용) = 유효 가치 − 실제 잔 수×100.</summary>
        public long BonusPointsThisCycle => Math.Max(0, SubmittedPointsThisCycle - SubmittedThisCycle * MasterRules.CupPoints);

        /// <summary>박람회 제출 가능 시점: 이번 주기 접수 전(수집 웨이브 진행·선택 대기).</summary>
        public bool CanSubmit => FairReductionSnapshot < 0 && (Phase == MasterPhase.Wave || Phase == MasterPhase.AwaitChoice || Phase == MasterPhase.Prep) && Segment == MasterSegment.Collect;

        public bool TrySubmit(int points = MasterRules.CupPoints)
        {
            if (!CanSubmit) return false;
            if (SubmittedThisCycle < long.MaxValue) SubmittedThisCycle++;
            points = Math.Max(MasterRules.CupPoints, points);
            SubmittedPointsThisCycle = SubmittedPointsThisCycle > long.MaxValue - points ? long.MaxValue : SubmittedPointsThisCycle + points;
            return true;
        }

        void BeginWave(long wave, bool prep = false)
        {
            Wave = Math.Max(1, wave);
            Phase = prep ? MasterPhase.Prep : MasterPhase.Wave;
            Target = MasterRules.TargetCount(Wave);
            Spawned = 0;
            Cleared = 0;
            if (prep) PrepStarted?.Invoke(Wave); else WaveStarted?.Invoke(Wave);
        }

        /// <summary>실제로 1마리 생성했다(웨이브 소속).</summary>
        public void NotifySpawned() { if (Phase == MasterPhase.Wave && Spawned < Target) Spawned++; }

        /// <summary>웨이브 소속 개체가 처리(처치)됐다. 목표 처리 완료 시 다음 단계로.</summary>
        public void NotifyCleared()
        {
            if (Phase != MasterPhase.Wave || Cleared >= Spawned) return;
            Cleared++;
            if (Cleared >= Target) CompleteWave();
        }

        void CompleteWave()
        {
            int k = MasterRules.InCycle(Wave);
            if (k < MasterRules.CollectWaves) { BeginWave(Wave + 1); return; }
            if (k == MasterRules.CollectWaves) { Phase = MasterPhase.AwaitChoice; return; }
            if (k == MasterRules.CollectWaves + MasterRules.FairWaves)
            {
                long c = Cycle;
                if (fairRewarded.Add(c)) FairSucceeded?.Invoke(c); // 성공 ID(주기)당 1회
                BeginWave(Wave + 1);
                return;
            }
            if (k == MasterRules.CycleLength)
            {
                long finished = Cycle;
                // 직전 박람회에서 확정한 R을 다음 주기 일반 수집에 한 번 전달(이전 주기 값은 교체 — 누적 아님).
                CarriedReduction = Math.Max(0, FairReductionSnapshot);
                FairReductionSnapshot = -1;
                SubmittedThisCycle = 0; // 제출은 그 주기 소비
                SubmittedPointsThisCycle = 0;
                RepeatCount = 0;
                CycleCompleted?.Invoke(finished);
                BeginWave(Wave + 1, CyclePrep);
                return;
            }
            BeginWave(Wave + 1);
        }

        /// <summary>선택 대기에서 같은 주기 1~5를 같은 난이도로 반복. 주기·감소율·제출 수는 그대로.</summary>
        public bool ChooseRepeat()
        {
            if (Phase != MasterPhase.AwaitChoice) return false;
            RepeatCount++;
            BeginWave(MasterRules.CycleStart(Cycle));
            return true;
        }

        /// <summary>박람회 도전 접수(중복 입력은 false). 이 시점에 제출 수로 R을 한 번 확정한다.</summary>
        public bool ChooseFair()
        {
            if (Phase != MasterPhase.AwaitChoice) return false;
            FairReductionSnapshot = PreviewFairReduction;
            Phase = MasterPhase.ClearingForFair;
            return true;
        }

        /// <summary>남은 적 처리 완료 → 현수막 설치 시작.</summary>
        public bool BeginTransition()
        {
            if (Phase != MasterPhase.ClearingForFair) return false;
            Phase = MasterPhase.Transition;
            return true;
        }

        /// <summary>현수막 설치 완료 → 박람회 1/3.</summary>
        public bool FinishTransition()
        {
            if (Phase != MasterPhase.Transition) return false;
            BeginWave(MasterRules.CycleStart(Cycle) + MasterRules.CollectWaves);
            return true;
        }

        public bool FairRewardedFor(long cycle) => fairRewarded.Contains(cycle);
    }
}
