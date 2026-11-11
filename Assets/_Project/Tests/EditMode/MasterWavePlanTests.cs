using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>2026-11-12 다음 웨이브 예고: 확정 계획(결정성·목표 수·고정 구간)·분류·준비 단계 상태 경계.</summary>
    public class MasterWavePlanTests
    {
        static MasterWavePlan Build(long wave, int repeat, int seed = 12345, float runTime = 30f) =>
            MasterWavePlan.Build(wave, repeat, seed, runTime, Job.Bartender, MasterDefs.FairCustomer(Job.Bartender), MasterDefs.RewardMonster(Job.Bartender));

        [Test]
        public void CollectPlan_Deterministic_CountMatchesTarget_RepeatAndWaveVary()
        {
            for (long w = 1; w <= 5; w++)
            {
                var a = Build(w, 0); var b = Build(w, 0);
                Assert.AreEqual(MasterRules.TargetCount(w), a.Count, "계획 수 = 목표 수");
                CollectionAssert.AreEqual(a.Defs, b.Defs, "같은 seed·웨이브·반복 = 같은 계획");
                Assert.IsTrue(a.Defs.All(d => d != null && !d.IsBoss));
            }
            // 반복 회차·seed가 다르면 새 계획(결정적으로 다른 흐름) — 30개 웨이브 중 대부분 달라야 한다.
            int differ = 0, collect = 0;
            for (long w = 1; w <= 30; w++)
            {
                if (MasterRules.SegmentOf(w) != MasterSegment.Collect) continue; // 박람회·보상은 고정 정의
                collect++;
                if (!Build(w, 0).Defs.SequenceEqual(Build(w, 1).Defs)) differ++;
            }
            Assert.AreEqual(15, collect);
            Assert.GreaterOrEqual(differ, 13, "반복 회차가 다르면 새 계획");
            Assert.IsFalse(Build(3, 0, 1).Defs.SequenceEqual(Build(3, 0, 2).Defs));
        }

        [Test]
        public void FairAndRewardPlans_FixedDefs_TruthfulLabels()
        {
            var fair = Build(6, 0);
            Assert.AreEqual(MasterRules.FairCountPerWave, fair.Count);
            Assert.IsTrue(fair.Defs.All(d => d == MasterDefs.FairCustomer(Job.Bartender)));
            Assert.AreEqual("박람회", fair.Label);
            StringAssert.Contains("상성 없음", fair.Advice());
            var reward = Build(9, 0);
            Assert.AreEqual(MasterRules.RewardCountPerWave, reward.Count);
            Assert.IsTrue(reward.Defs.All(d => d == MasterDefs.RewardMonster(Job.Bartender)));
            Assert.AreEqual("무해 보상", reward.Label);
            StringAssert.Contains("공격하지 않는", reward.Advice());
        }

        [Test]
        public void Kinds_FromExistingDefFields()
        {
            Assert.AreEqual(MasterWavePlan.Kind.Swarm, MasterWavePlan.KindOf(EnemyDef.Imp));
            Assert.AreEqual(MasterWavePlan.Kind.Rush, MasterWavePlan.KindOf(EnemyDef.EspressoBug), "속도 3.6");
            Assert.AreEqual(MasterWavePlan.Kind.Rush, MasterWavePlan.KindOf(EnemyDef.OliveBomber), "자폭");
            Assert.AreEqual(MasterWavePlan.Kind.Elite, MasterWavePlan.KindOf(EnemyDef.Brute), "체력 70");
            Assert.AreEqual(MasterWavePlan.Kind.Elite, MasterWavePlan.KindOf(EnemyDef.MartiniSpirit));
            StringAssert.Contains("멀리서 공격", MasterWavePlan.TraitOf(EnemyDef.ShakerGolem));
            StringAssert.Contains("제자리 소환", MasterWavePlan.TraitOf(EnemyDef.SwirlConjuror));
        }

        [Test]
        public void GroupsAndLabel_SumToCount_AdviceHasNoUnimplementedDrinkAttacks()
        {
            for (long w = 1; w <= 25; w++)
            {
                if (MasterRules.SegmentOf(w) != MasterSegment.Collect) continue;
                var p = Build(w, 0, 777, w * 30f);
                Assert.AreEqual(p.Count, p.Groups().Sum(g => g.count));
                Assert.Contains(p.Label, new[] { "무리", "강적", "돌진", "혼합" });
                string adv = p.Advice() + p.AffinityHint();
                foreach (var banned in new[] { "추출", "우림", "공격 전환", "공격 변화" })
                    StringAssert.DoesNotContain(banned, adv, "후속 기획(커피·차 공격 변화) 문구 금지");
                var f = p.MainFamily();
                if (f != null) Assert.IsTrue(DrinkAffinity.Matches(MasterWavePlan.CounterDrink(f.Value), f.Value), "구현된 순환 상성만 안내");
            }
        }

        [Test]
        public void ExpectedHp_MatchesSpawnFormula()
        {
            // 실제 생성: def.hp × Scale(HpScale(w), 1−R) × 도전 배율 → 정수 반올림
            float scale = MasterChallengeDirector.Scale(EndlessRules.HpScale(6), MasterRules.ReductionFactor(13));
            Assert.AreEqual(IntNum.Round(60f * scale * 1.036f), MasterWavePlan.ExpectedHp(MasterDefs.FairCustomer(Job.Bartender), MasterSegment.Fair, 6, 13, 1.036f));
            Assert.AreEqual(95, MasterWavePlan.ExpectedHp(MasterDefs.FairCustomer(Job.Bartender), MasterSegment.Fair, 6, 13, 1.036f), "2026-11-11 실제 빌드 측정 maxHp 95");
            var r = MasterDefs.RewardMonster(Job.Bartender);
            Assert.AreEqual(System.Math.Max(1, IntNum.Round(r.hp)), MasterWavePlan.ExpectedHp(r, MasterSegment.Reward, 9, 50, 2f), "보상 = 웨이브1·도전 배율 제외");
        }

        static void Complete(MasterChallengeState s) { for (long i = 0; i < s.Target; i++) s.NotifySpawned(); for (long i = 0; i < s.Target; i++) s.NotifyCleared(); }

        [Test]
        public void PrepPhase_CycleStartsOnly_FrozenCounters_SubmitAllowed_RepeatSkipsPrep()
        {
            var s = new MasterChallengeState(5, cyclePrep: true);
            Assert.AreEqual(MasterPhase.Prep, s.Phase);
            Assert.IsTrue(s.IsPreparing);
            Assert.IsTrue(s.CanSubmit, "준비 중 박람회 제출 가능");
            s.NotifySpawned();
            Assert.AreEqual(0, s.Spawned, "준비 중 생성 집계 없음");
            Assert.IsTrue(s.StartFromPrep());
            Assert.IsFalse(s.StartFromPrep(), "중복 시작 무시");
            Assert.AreEqual(MasterPhase.Wave, s.Phase);
            for (int i = 0; i < 5; i++) Complete(s);
            Assert.AreEqual(MasterPhase.AwaitChoice, s.Phase);
            Assert.IsTrue(s.IsPreparing, "선택 대기 = 준비 지점");
            Assert.IsTrue(s.ChooseRepeat());
            Assert.AreEqual(MasterPhase.Wave, s.Phase, "반복 선택은 선택 자체가 준비 — 바로 진행");
            for (int i = 0; i < 5; i++) Complete(s);
            Assert.IsTrue(s.TrySubmit(150));
            Assert.IsTrue(s.ChooseFair());
            Assert.IsTrue(s.BeginTransition());
            Assert.IsTrue(s.FinishTransition());
            for (int i = 0; i < 5; i++) Complete(s);
            Assert.AreEqual(11, s.Wave);
            Assert.AreEqual(MasterPhase.Prep, s.Phase, "다음 주기 1웨이브는 준비에서 시작");
            Assert.AreEqual(8, s.CarriedReduction, "박람회 약화 전달(150p × 5% = 7.5 → 8)");
            Assert.AreEqual(8, s.ActiveReduction);
            Assert.IsTrue(s.CanSubmit, "새 주기 제출 가능");
            Assert.IsTrue(s.FairRewardedFor(1));
        }

        [Test]
        public void DefaultState_NoPrep_LegacyBehaviorUnchanged()
        {
            var s = new MasterChallengeState();
            Assert.AreEqual(MasterPhase.Wave, s.Phase);
            Assert.IsFalse(s.IsPreparing);
        }
    }
}
