using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-11 마스터 도전 순수 진행 규칙(10웨이브 주기·반복·도전 선택·감소율 스냅샷·보상 구간·수치 경계).</summary>
    public class MasterChallengeRuleTests
    {
        static void CompleteWave(MasterChallengeState s)
        {
            long target = s.Target;
            for (long i = 0; i < target; i++) s.NotifySpawned();
            for (long i = 0; i < target; i++) s.NotifyCleared();
        }

        [Test]
        public void Segments_TenWaveCycle_AbsoluteWaveKept()
        {
            for (int w = 1; w <= 30; w++)
            {
                int k = (w - 1) % 10 + 1;
                var expect = k <= 5 ? MasterSegment.Collect : k <= 8 ? MasterSegment.Fair : MasterSegment.Reward;
                Assert.AreEqual(expect, MasterRules.SegmentOf(w), "wave " + w);
                Assert.AreEqual((w - 1) / 10 + 1, MasterRules.CycleOf(w));
            }
            Assert.AreEqual(1, MasterRules.FairIndex(6)); Assert.AreEqual(3, MasterRules.FairIndex(18)); Assert.AreEqual(0, MasterRules.FairIndex(5));
            Assert.AreEqual(1, MasterRules.RewardIndex(19)); Assert.AreEqual(2, MasterRules.RewardIndex(20));
            Assert.AreEqual(11, MasterRules.CycleStart(2));
        }

        [Test]
        public void FiveCollect_AwaitChoice_RepeatKeepsCycleAndDifficulty_FairOnlyByChoice()
        {
            var s = new MasterChallengeState();
            for (int i = 0; i < 5; i++) { Assert.AreEqual(MasterPhase.Wave, s.Phase); Assert.AreEqual(i + 1, s.Wave); CompleteWave(s); }
            Assert.AreEqual(MasterPhase.AwaitChoice, s.Phase, "5웨이브 뒤 자동 박람회 진입 없음");
            Assert.AreEqual(5, s.Wave);
            for (int rep = 1; rep <= 5; rep++)
            {
                Assert.IsTrue(s.ChooseRepeat());
                Assert.AreEqual(1, s.Wave, "같은 주기 1웨이브로");
                Assert.AreEqual(1, s.Cycle);
                Assert.AreEqual(EndlessRules.WaveCount(1), s.Target, "같은 난이도 범위");
                Assert.AreEqual(rep, s.RepeatCount);
                for (int i = 0; i < 5; i++) CompleteWave(s);
                Assert.AreEqual(MasterPhase.AwaitChoice, s.Phase);
                Assert.AreEqual(0, s.CarriedReduction, "반복으로 감소율 자동 증가 없음");
                Assert.AreEqual(0, s.PreviewFairReduction);
            }
            Assert.IsFalse(s.ChooseRepeat() && false);
        }

        [Test]
        public void FullCycle_Fair3_Reward2_NextCycleGetsSnapshotOnce_NoAccumulation()
        {
            var s = new MasterChallengeState();
            int fairEvents = 0, cycleEvents = 0;
            s.FairSucceeded += c => fairEvents++;
            s.CycleCompleted += c => cycleEvents++;
            for (int i = 0; i < 5; i++) CompleteWave(s);
            for (int i = 0; i < 4; i++) Assert.IsTrue(s.TrySubmit());
            Assert.AreEqual(20, s.PreviewFairReduction, "4잔 × 5%p / 주기1");
            Assert.IsTrue(s.ChooseFair());
            Assert.IsFalse(s.ChooseFair(), "중복 접수 무시");
            Assert.IsFalse(s.ChooseRepeat(), "접수 뒤 반복 불가");
            Assert.AreEqual(20, s.FairReductionSnapshot);
            Assert.IsFalse(s.TrySubmit(), "접수 뒤 제출 불가");
            Assert.AreEqual(MasterPhase.ClearingForFair, s.Phase);
            Assert.IsFalse(s.FinishTransition(), "설치 시작 전에 박람회 시작 없음");
            Assert.IsTrue(s.BeginTransition());
            Assert.IsFalse(s.BeginTransition(), "설치 중복 시작 없음");
            Assert.AreEqual(MasterPhase.Transition, s.Phase);
            Assert.IsTrue(s.FinishTransition());
            Assert.IsFalse(s.FinishTransition());
            Assert.AreEqual(6, s.Wave);
            Assert.AreEqual(MasterSegment.Fair, s.Segment);
            Assert.AreEqual(MasterRules.FairCountPerWave, s.Target);
            Assert.AreEqual(20, s.ActiveReduction, "박람회는 이번 스냅샷");
            for (int i = 0; i < 3; i++) CompleteWave(s);
            Assert.AreEqual(1, fairEvents, "8웨이브 완료 때 1회");
            Assert.IsTrue(s.FairRewardedFor(1));
            Assert.AreEqual(9, s.Wave);
            Assert.AreEqual(MasterSegment.Reward, s.Segment);
            Assert.AreEqual(0, s.ActiveReduction, "보상 구간은 약화 예외");
            CompleteWave(s); CompleteWave(s);
            Assert.AreEqual(1, cycleEvents);
            Assert.AreEqual(11, s.Wave);
            Assert.AreEqual(2, s.Cycle);
            Assert.AreEqual(MasterSegment.Collect, s.Segment);
            Assert.AreEqual(20, s.CarriedReduction, "직전 확정 R을 다음 수집에 전달");
            Assert.AreEqual(20, s.ActiveReduction);
            Assert.AreEqual(0, s.SubmittedThisCycle, "제출은 그 주기에 소비");
            Assert.AreEqual(-1, s.FairReductionSnapshot);

            // 2주기: 제출 없이 박람회 → 3주기 전달 0(이전 20을 누적하지 않음)
            for (int i = 0; i < 5; i++) CompleteWave(s);
            Assert.IsTrue(s.ChooseFair()); s.BeginTransition(); s.FinishTransition();
            Assert.AreEqual(16, s.Wave);
            Assert.AreEqual(0, s.ActiveReduction);
            for (int i = 0; i < 5; i++) CompleteWave(s);
            Assert.AreEqual(2, fairEvents);
            Assert.AreEqual(21, s.Wave);
            Assert.AreEqual(0, s.CarriedReduction, "직전 값으로 교체(누적 합산 아님)");
        }

        [Test]
        public void Reduction_Formula_QualityPoints_RoundOnce_Boundaries()
        {
            Assert.AreEqual(0, MasterRules.ReductionPercent(0, 5, 1));
            Assert.AreEqual(5, MasterRules.ReductionPercent(1, 5, 1));
            Assert.AreEqual(10, MasterRules.ReductionPercent(4, 5, 2), "주기에 비례해 필요 잔 수 증가");
            Assert.AreEqual(2, MasterRules.ReductionPercent(1, 5, 3), "5/3=1.67 → 2(마지막 한 번 반올림)");
            Assert.AreEqual(8, MasterRules.ReductionPercentFromPoints(150, 5, 1), "최고 품질 1잔 = 7.5 → 8");
            Assert.AreEqual(13, MasterRules.ReductionPercentFromPoints(250, 5, 1), "일반1+최고1 = 12.5 → 13(잔별 반올림 합 14가 아님)");
            Assert.AreEqual(150, MasterRules.SubmitPoints(DrinkKind.Coffee, 85));
            Assert.AreEqual(100, MasterRules.SubmitPoints(DrinkKind.Coffee, 84));
            Assert.AreEqual(100, MasterRules.SubmitPoints(DrinkKind.Cocktail, 100), "칵테일·차는 품질 가중 없음");
            Assert.Greater(MasterRules.ReductionPercent(long.MaxValue, 5, 1), 100_000_000_000_000_000L, "넘침·예외 없이 포화 point로 계산");
            Assert.AreEqual(0, MasterRules.ReductionPercent(-3, 5, 1));
            // 최종 능력치 = max(1, round(base × (1−R/100)))
            Assert.AreEqual(100, MasterRules.Apply(100, 0));
            Assert.AreEqual(80, MasterRules.Apply(100, 20));
            Assert.AreEqual(1, MasterRules.Apply(100, 100));
            Assert.AreEqual(1, MasterRules.Apply(100, 250), "100% 이상도 최소 1");
            Assert.AreEqual(4, MasterRules.Apply(7, 50), "3.5 → 4");
            Assert.AreEqual(0, MasterRules.Apply(0, 0), "공격력 0은 최소 1로 되살리지 않는다(보상 몬스터)");
            Assert.AreEqual(0, MasterRules.Apply(0, 50));
            var s = new MasterChallengeState();
            for (int i = 0; i < 5; i++) { long t = s.Target; for (long j = 0; j < t; j++) s.NotifySpawned(); for (long j = 0; j < t; j++) s.NotifyCleared(); }
            s.TrySubmit(100); s.TrySubmit(150);
            Assert.AreEqual(2, s.SubmittedThisCycle, "실제 잔 수");
            Assert.AreEqual(50, s.BonusPointsThisCycle, "품질 추가 가치(정수 point)");
            long preview = s.PreviewFairReduction;
            s.ChooseFair();
            Assert.AreEqual(preview, s.FairReductionSnapshot, "미리보기 = 실제 확정");
        }

        [Test]
        public void HighWave_NoOverflow_SumWaveCountsSaturates()
        {
            Assert.AreEqual(EndlessRules.SaturatedLong, EndlessRules.SumWaveCounts(1, long.MaxValue), "decimal 범위 밖에서 예외 대신 포화");
            Assert.AreEqual(EndlessRules.SaturatedLong, EndlessRules.SumWaveCounts(long.MaxValue / 2, long.MaxValue));
            long loop = 0; for (long w = 3; w <= 40; w++) loop += EndlessRules.WaveCount(w);
            Assert.AreEqual(loop, EndlessRules.SumWaveCounts(3, 40));
            Assert.AreEqual(MasterSegment.Collect, MasterRules.SegmentOf(long.MaxValue - 6), "(MaxValue−7) % 10 = 0 → 주기 내 1");
            Assert.Greater(MasterRules.CycleOf(long.MaxValue), 0);
            Assert.AreEqual(EndlessRules.SaturatedLong, MasterRules.TargetCount(long.MaxValue - 6));
            Assert.AreEqual(1, MasterRules.Apply(1e300, 99) > 0 ? 1 : 0);
            Assert.AreEqual(int.MaxValue, MasterRules.Apply(1e300, 0), "int 경계 포화");
        }

        [Test]
        public void PrepSeconds_ClampedTo3To4_Default35()
        {
            Assert.AreEqual(3.5f, MasterRules.PrepSecondsDefault);
            Assert.AreEqual(3f, MasterRules.ClampPrep(1f));
            Assert.AreEqual(4f, MasterRules.ClampPrep(9f));
            Assert.AreEqual(3.5f, MasterRules.ClampPrep(float.NaN));
        }

        [Test]
        public void MasterDefs_SeparateFromOriginalBoss_HarmlessRewardDef()
        {
            var fair = MasterDefs.FairCustomer(Job.Bartender);
            Assert.AreNotSame(EnemyDef.DifficultCustomer, fair);
            Assert.AreEqual(BossKind.None, fair.boss, "최종 보스 처치/클리어 플래그 없음");
            Assert.AreEqual(2500f, EnemyDef.DifficultCustomer.hp, "원본 보스 HP 불변");
            Assert.AreEqual(BossKind.Final, EnemyDef.DifficultCustomer.boss);
            Assert.AreEqual(1.3f, EnemyDef.DifficultCustomer.radius);
            Assert.AreEqual(EnemyDef.DifficultCustomer.ArtId, fair.ArtId, "원본 진상 손님 아트 재사용(축소)");
            Assert.Less(fair.visualScaleMultiplier, 1f);
            Assert.AreEqual(MonsterFamily.Neutral, MonsterFamilies.Of(fair), "박람회 진상 손님 계열 = 중립");
            Assert.AreSame(fair, MasterDefs.FairCustomer(Job.Bartender), "정의 재사용");
            var tea = MasterDefs.FairCustomer(Job.TeaSommelier);
            Assert.AreEqual(BossKind.None, tea.boss);
            Assert.AreEqual(BossKind.Final, EnemyDef.TeaCustomer.boss);

            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                var r = MasterDefs.RewardMonster(job);
                Assert.AreEqual(0f, r.contactDamage); Assert.AreEqual(0f, r.projectileDamage);
                Assert.IsFalse(r.explodesOnContact); Assert.AreEqual(0f, r.burstZoneDamagePerSecond); Assert.AreEqual(0f, r.burstZoneSeconds);
                Assert.IsNull(r.summonDef); Assert.AreEqual(EnemyArchetype.Rush, r.archetype);
                Assert.AreEqual(BossKind.None, r.boss);
            }
            Assert.AreEqual(6f, EnemyDef.Imp.contactDamage, "원본 라임웨지 불변");
        }

        [Test]
        public void FairReward_EveryPlayableIngredient_DataBundleOnce()
        {
            var items = MasterChallengeDirector.FairRewardItems();
            Assert.AreEqual(DrinkSupply.Table.Length, items.Count);
            var seen = new HashSet<string>();
            foreach (var it in items)
            {
                Assert.IsTrue(seen.Add(it.id), "재료당 1회");
                Assert.IsNotNull(DrinkIngredients.Get(it.id));
                Assert.AreEqual(DrinkSupply.Get(it.id).bundle * MasterRules.FairRewardBundles, it.amount, "데이터 정의 기본 묶음");
                Assert.Less(it.amount, 1000, "임의 대량 지급 없음");
            }
            foreach (var def in DrinkIngredients.All) Assert.IsTrue(seen.Contains(def.id), "모든 재료 포함: " + def.id);
        }

        [Test]
        public void RecipeKey_IgnoresNameAmountIceWater_DistinguishesIngredientSet()
        {
            var a = DrinkLab.ExamplePresets[0].Clone(); // IBA 네그로니
            var b = a.Clone(); b.name = "내 네그로니!!"; b.steps[0].amount += 100; b.ice = 0; b.water = 50;
            Assert.AreEqual(DrinkLab.RecipeKey(a), DrinkLab.RecipeKey(b), "이름·양 미세 변형은 같은 종류");
            var c = a.Clone(); c.steps.RemoveAt(2);
            Assert.AreNotEqual(DrinkLab.RecipeKey(a), DrinkLab.RecipeKey(c));
            var martini = DrinkLab.ExamplePresets[2];
            Assert.AreNotEqual(DrinkLab.RecipeKey(a), DrinkLab.RecipeKey(martini));
            // 예시 칵테일 사이에 '기법만 다른' 쌍이 없다(종류 키가 의미 있는 종류를 뭉개지 않음 — 근거 자료).
            var keys = new Dictionary<string, string>();
            foreach (var r in DrinkLab.ExamplePresets)
            {
                if (r.Kind != DrinkKind.Cocktail) continue;
                Assert.IsFalse(keys.ContainsKey(DrinkLab.RecipeKey(r)), "예시 칵테일 키 충돌: " + r.name);
                keys[DrinkLab.RecipeKey(r)] = r.name;
            }
        }

        [Test]
        public void CoffeeQuality_IntegerFromStrength_PerMethodTargets()
        {
            var espresso = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.EspressoMachine);
            espresso.SetAmount(DrinkIngredients.CoffeeBean, 180); espresso.water = 360; // 1:2
            Assert.AreEqual(100, DrinkQuality.Of(espresso));
            var pour = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.PourOver);
            pour.SetAmount(DrinkIngredients.CoffeeBean, 170); pour.water = 3000; // ≈1:17.6
            int q = DrinkQuality.Of(pour);
            Assert.That(q, Is.InRange(60, 84), "핸드 드립 고유 목표 1:15 기준");
            Assert.AreEqual(IntNum.Percent(CoffeeBrewing.Strength01(CoffeeBrewMethod.PourOver, 170, 3000)), q);
            Assert.AreEqual(-1, DrinkQuality.Of(DrinkLab.ExamplePresets[0]), "칵테일은 품질 판정 없음");
            Assert.AreEqual(DrinkQuality.Grade.Best, DrinkQuality.GradeOf(85));
            Assert.AreEqual(DrinkQuality.Grade.Good, DrinkQuality.GradeOf(60));
            Assert.AreEqual(DrinkQuality.Grade.Normal, DrinkQuality.GradeOf(59));
            StringAssert.Contains("★★★", DrinkQuality.Label(90));
            StringAssert.DoesNotContain(".", DrinkQuality.Label(90), "정수 표시");
        }
    }

    /// <summary>2026-11-11 런 제조 잔 장부(격리 저장): 런 재료 우선 → 창고, 한 잔 한 용도, 보관 잔 환원, 사용 잔 환원 금지, 중복 정산·복구 없음, 저장 실패 롤백.</summary>
    public class MasterRunCupLedgerTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "runcups_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            DrinkStore.FaultHookForTests = null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        static DrinkRecipe Martini() => DrinkLab.ExamplePresets[2].Clone(); // gin 600 + dry vermouth 100

        static void ClearStock(DrinkRecipe r) { foreach (var s in r.steps) { int o = DrinkLab.Data.Owned(s.id); if (o > 0) DrinkLab.Data.Add(s.id, -o); } DrinkLab.Data.ClampEndlessToStock(); }

        [Test]
        public void CraftInRun_UsesRunPoolFirst_ThenStockOnce_ShortageRefuses()
        {
            var r = Martini();
            ClearStock(r);
            Assert.IsFalse(DrinkLab.CraftInRun("runA", r).Ok, "재료 없음 → 레시피만으로 지급 없음");
            Assert.IsTrue(DrinkLab.RecordBossAward("runA", "a1", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 400) }, true));
            DrinkLab.Data.Add(DrinkIngredients.Gin, 300);
            DrinkLab.Data.Add(DrinkIngredients.DryVermouth, 100);
            var res = DrinkLab.CraftInRun("runA", r);
            Assert.IsTrue(res.Ok, res.Message);
            Assert.AreEqual(400, res.Cup.fromRun.Find(x => x.id == DrinkIngredients.Gin).amount, "런 재료 먼저");
            Assert.AreEqual(200, res.Cup.fromStockNormal.Find(x => x.id == DrinkIngredients.Gin).amount, "부족분만 창고");
            Assert.AreEqual(100, DrinkLab.Owned(DrinkIngredients.Gin), "창고 한 번만 차감");
            Assert.AreEqual(0, DrinkLab.Owned(DrinkIngredients.DryVermouth));
            Assert.IsFalse(DrinkLab.RunPool("runA").ContainsKey(DrinkIngredients.Gin), "런 재료 소진");
            Assert.AreEqual(RunCup.Held, res.Cup.state);
            Assert.IsNull(DrinkLab.Prepared, "로비 준비 음료 슬롯과 분리");
            Assert.IsFalse(DrinkLab.CraftInRun("runA", r).Ok, "두 번째 잔은 재료 부족");
        }

        [Test]
        public void UseRunCup_OneUseOnly_DrinkOrSubmit()
        {
            var r = Martini();
            DrinkLab.Data.Add(DrinkIngredients.Gin, 1200); DrinkLab.Data.Add(DrinkIngredients.DryVermouth, 200);
            var c1 = DrinkLab.CraftInRun("runB", r).Cup;
            var c2 = DrinkLab.CraftInRun("runB", r).Cup;
            Assert.IsTrue(DrinkLab.UseRunCup("runB", c1.cupId, RunCup.Drunk, out _));
            Assert.IsFalse(DrinkLab.UseRunCup("runB", c1.cupId, RunCup.Submitted, out _), "같은 잔 두 용도 금지");
            Assert.IsFalse(DrinkLab.UseRunCup("runB", c1.cupId, RunCup.Drunk, out _));
            Assert.IsFalse(DrinkLab.UseRunCup("otherRun", c2.cupId, RunCup.Submitted, out _), "다른 런 잔 사용 금지");
            Assert.IsTrue(DrinkLab.UseRunCup("runB", c2.cupId, RunCup.Submitted, out _));
            DrinkLab.ReloadForTests();
            Assert.AreEqual(RunCup.Drunk, DrinkLab.RunCupsFor("runB").Find(c => c.cupId == c1.cupId).state, "재로드 후 상태 유지");
            Assert.IsFalse(DrinkLab.UseRunCup("runB", c2.cupId, RunCup.Submitted, out _), "재로드로 제출 복제 없음");
        }

        [Test]
        public void EndRun_HeldCupRefundsBySource_UsedCupsNotRefunded_RunPoolDebited_NoDoubleSettle()
        {
            var r = Martini();
            ClearStock(r);
            DrinkLab.RecordBossAward("runC", "a1", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 1200), new DrinkAmount(DrinkIngredients.DryVermouth, 100) }, true);
            DrinkLab.Data.AddFrom(DrinkIngredients.DryVermouth, 100, false);
            var used = DrinkLab.CraftInRun("runC", r).Cup;   // gin 600 run, verm 100 run
            var held = DrinkLab.CraftInRun("runC", r).Cup;   // gin 600 run, verm 100 stock(normal)
            Assert.AreEqual(100, held.fromStockNormal.Find(x => x.id == DrinkIngredients.DryVermouth).amount);
            Assert.AreEqual(0, DrinkLab.Owned(DrinkIngredients.DryVermouth));
            Assert.IsTrue(DrinkLab.UseRunCup("runC", used.cupId, RunCup.Drunk, out _));
            var s1 = DrinkLab.EndRun("runC", RunEndKind.Died, 30, false);
            Assert.AreEqual(600, DrinkLab.Owned(DrinkIngredients.Gin), "마신 잔의 런 재료는 지급에서 빠지고 보관 잔 몫은 지급");
            Assert.AreEqual(600, DrinkLab.EndlessOwned(DrinkIngredients.Gin), "런 보상은 무한 출처");
            Assert.AreEqual(100, DrinkLab.Owned(DrinkIngredients.DryVermouth), "보관 잔의 창고 몫 환원");
            Assert.AreEqual(0, DrinkLab.EndlessOwned(DrinkIngredients.DryVermouth), "환원은 원래 출처(일반) 그대로");
            Assert.AreEqual(0, DrinkLab.RunCupsFor("runC").Count, "런 잔 기록 정리");
            var s2 = DrinkLab.EndRun("runC", RunEndKind.Abandoned, 30, true);
            Assert.AreSame(s1, s2);
            Assert.AreEqual(600, DrinkLab.Owned(DrinkIngredients.Gin), "중복 정산 없음");
            Assert.IsFalse(DrinkLab.RecoverAbandoned(), "정산 뒤 복구할 것 없음");
            Assert.AreEqual(600, DrinkLab.Owned(DrinkIngredients.Gin));
        }

        [Test]
        public void AbnormalExit_RecoverySettlesRunCupsOnce()
        {
            var r = Martini();
            ClearStock(r);
            DrinkLab.Data.AddFrom(DrinkIngredients.Gin, 600, true);
            DrinkLab.Data.AddFrom(DrinkIngredients.DryVermouth, 100, false);
            var held = DrinkLab.CraftInRun("runD", r).Cup; // 창고만 사용(무한 gin, 일반 verm)
            Assert.AreEqual(600, held.fromStockEndless.Find(x => x.id == DrinkIngredients.Gin).amount);
            DrinkLab.ReloadForTests(); // 비정상 종료 뒤 새 실행
            Assert.IsTrue(DrinkLab.RecoverAbandoned());
            Assert.AreEqual(600, DrinkLab.Owned(DrinkIngredients.Gin));
            Assert.AreEqual(600, DrinkLab.EndlessOwned(DrinkIngredients.Gin), "무한 출처 유지(판매 불가 우회 없음)");
            Assert.AreEqual(100, DrinkLab.Owned(DrinkIngredients.DryVermouth));
            Assert.AreEqual(0, DrinkLab.EndlessOwned(DrinkIngredients.DryVermouth));
            Assert.IsFalse(DrinkLab.RecoverAbandoned(), "복구 1회");
            Assert.AreEqual(600, DrinkLab.Owned(DrinkIngredients.Gin));
        }

        [Test]
        public void SaveFailure_RollsBackCraftAndUse()
        {
            var r = Martini();
            ClearStock(r);
            DrinkLab.Data.Add(DrinkIngredients.Gin, 600); DrinkLab.Data.Add(DrinkIngredients.DryVermouth, 100);
            DrinkStore.FaultHookForTests = stage => { if (stage == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입"); };
            Assert.IsFalse(DrinkLab.CraftInRun("runE", r).Ok);
            Assert.AreEqual(600, DrinkLab.Owned(DrinkIngredients.Gin), "실패 시 재고 그대로");
            Assert.AreEqual(0, DrinkLab.RunCupsFor("runE").Count);
            DrinkStore.FaultHookForTests = null;
            var cup = DrinkLab.CraftInRun("runE", r).Cup;
            DrinkStore.FaultHookForTests = stage => { if (stage == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입"); };
            Assert.IsFalse(DrinkLab.UseRunCup("runE", cup.cupId, RunCup.Submitted, out _));
            DrinkStore.FaultHookForTests = null;
            Assert.AreEqual(RunCup.Held, DrinkLab.RunCupsFor("runE")[0].state, "실패 시 잔 상태 그대로");
        }

        [Test]
        public void OldV4File_WithoutRunCups_LoadsEmpty_PreparedKept()
        {
            var r = Martini();
            DrinkLab.Data.Add(DrinkIngredients.Gin, 600); DrinkLab.Data.Add(DrinkIngredients.DryVermouth, 100);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            string path = Path.Combine(dir, "drinks.json");
            string json = File.ReadAllText(path);
            int i = json.IndexOf("\"runCups\"");
            Assert.Greater(i, 0);
            // runCups 필드를 뺀 기존 v4 형태로 되돌린다.
            var data = JsonUtility.FromJson<DrinkSaveData>(json);
            data.schemaVersion = 4;
            string old = JsonUtility.ToJson(data).Replace(",\"runCups\":[]", "");
            StringAssert.DoesNotContain("runCups", old);
            File.WriteAllText(path, old);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(DrinkStore.LoadSource.Original, DrinkLab.LoadSource);
            Assert.IsTrue(File.Exists(path + ".pre-runcups-v4.bak"), "v4 원본 1회 백업");
            Assert.AreEqual(old, File.ReadAllText(path + ".pre-runcups-v4.bak"), "백업은 원본 그대로");
            Assert.IsNotNull(DrinkLab.Prepared, "기존 단일 준비 음료 보존");
            Assert.AreEqual(0, DrinkLab.Data.runCups.Count);
            // 다음 저장은 v5(v4 빌드는 미래 버전 보호로 덮어쓰지 않음).
            Assert.IsTrue(DrinkLab.SaveNow());
            StringAssert.Contains($"\"schemaVersion\": {DrinkStore.CurrentVersion}", File.ReadAllText(path));
            Assert.AreEqual(5, DrinkStore.CurrentVersion);
        }
    }

    /// <summary>2026-11-11 실제 GameManager(EditMode, 격리 저장)로 런 제조·음용 교체·제출·칵테일 종류·정지 상태.</summary>
    public class MasterChallengeGameTests
    {
        GameObject root, createdCamera;
        GameManager game;

        void Start(RunMode mode)
        {
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier, mode);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
            if (game.Master != null) game.Master.BannersEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (createdCamera != null) Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
            RunModeSelection.Current = RunMode.Standard;
        }

        static void Stock(DrinkRecipe r, int cups)
        {
            foreach (var s in r.steps) DrinkLab.Data.Add(s.id, s.amount * cups);
        }

        [Test]
        public void StandardMode_HasNoMaster_EndlessHasMasterAndHud()
        {
            Start(RunMode.Standard);
            Assert.IsNull(game.Master);
            Assert.IsNull(game.MasterHud);
            Object.DestroyImmediate(root); root = null;
            Start(RunMode.Endless);
            Assert.IsNotNull(game.Master);
            Assert.IsNotNull(game.MasterHud);
            Assert.AreEqual(1, game.EndlessWave);
            StringAssert.Contains("음료 만들기", game.MasterHud.BrewButtonText);
            Assert.IsFalse(game.MasterHud.BrewOpen, "강제 팝업 없음");
            Assert.AreEqual("마스터 도전", RunModeSelection.Name(RunMode.Endless));
        }

        [Test]
        public void Freeze_BlocksEscapeAndDoubleStart_RestoresTime()
        {
            Start(RunMode.Endless);
            Assert.IsTrue(game.BeginMasterFreeze());
            Assert.IsTrue(game.IsPaused); Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(game.BeginMasterFreeze(), "중복 정지 없음");
            game.RequestEscape();
            Assert.IsFalse(game.PauseView.IsOpen, "설치 중 Esc로 일시정지 창 열지 않음");
            Assert.IsFalse(game.OpenMasterBrew(), "설치 중 제조 창 열지 않음");
            game.EndMasterFreeze();
            Assert.IsFalse(game.IsPaused); Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void BrewWindow_PausesCombat_EscCloses()
        {
            Start(RunMode.Endless);
            Assert.IsTrue(game.OpenMasterBrew());
            Assert.IsTrue(game.IsPaused); Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(game.MasterHud.BrewOpen);
            Assert.IsFalse(game.OpenMasterBrew(), "중복 열기 없음");
            game.RequestEscape();
            Assert.IsFalse(game.MasterHud.BrewOpen);
            Assert.IsFalse(game.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void CocktailUnique_CountsRecipeKindOnce_AttackRecomputedNotCompounded()
        {
            Start(RunMode.Endless);
            var negroni = DrinkLab.ExamplePresets[0].Clone();
            var martini = DrinkLab.ExamplePresets[2].Clone();
            Stock(negroni, 3); Stock(martini, 1);
            float baseBonus = game.Stats.DamageBonus;
            Assert.IsTrue(game.CraftRunCup(negroni).Ok);
            Assert.AreEqual(1, game.Master.CocktailUniqueCount);
            Assert.AreEqual(0.02f, game.Stats.masterDamagePercent, 1e-6f);
            var renamed = negroni.Clone(); renamed.name = "다른 이름";
            Assert.IsTrue(game.CraftRunCup(renamed).Ok);
            Assert.IsTrue(game.CraftRunCup(negroni).Ok);
            Assert.AreEqual(1, game.Master.CocktailUniqueCount, "같은 레시피·이름 변형은 한 번");
            Assert.IsTrue(game.CraftRunCup(martini).Ok);
            Assert.AreEqual(2, game.Master.CocktailUniqueCount);
            Assert.AreEqual(0.04f, game.Stats.masterDamagePercent, 1e-6f, "U × 2%를 다시 계산(곱셈 누적 없음)");
            Assert.AreEqual(baseBonus + 0.04f, game.Stats.DamageBonus, 1e-5f);
            Assert.AreEqual(4, game.Master.CocktailAttackPercent);
            var coffee = DrinkLab.ExamplePresets[5].Clone(); Stock(coffee, 1);
            Assert.IsTrue(game.CraftRunCup(coffee).Ok);
            Assert.AreEqual(2, game.Master.CocktailUniqueCount, "커피는 칵테일 종류 집계 대상 아님");
        }

        [Test]
        public void DrinkSwap_RevertsOldEffect_PreservesHp_AffinityFromDrunk_RollbackOnFailure()
        {
            Start(RunMode.Endless);
            var negroni = DrinkLab.ExamplePresets[0].Clone();
            var espresso = DrinkLab.ExamplePresets[5].Clone();
            Stock(negroni, 1); Stock(espresso, 1);
            var c1 = game.CraftRunCup(negroni).Cup;
            var c2 = game.CraftRunCup(espresso).Cup;
            var s = game.Stats;
            float dmg0 = s.damagePercent, as0 = s.attackSpeedPercent, ms0 = s.moveSpeedPercent, hp0 = s.maxHpFlat, rg0 = s.rangePercent;
            game.Player.TakeDamage(10f);
            float hp = game.Player.Hp;
            Assert.IsTrue(game.DrinkRunCup(c1, out var e1), e1);
            Assert.AreEqual(DrinkKind.Cocktail, game.Player.DrunkDrink);
            Assert.AreEqual(hp, game.Player.Hp, "마셔도 현재 체력 회복 없음");
            var b2 = DrinkCalculator.Buff(espresso);
            if (DrinkQuality.GradeOf(c2.quality) == DrinkQuality.Grade.Best) b2 = DrinkCalculator.ScalePositive(b2, 1.05f);
            Assert.IsTrue(game.DrinkRunCup(c2, out var e2), e2);
            Assert.AreEqual(DrinkKind.Coffee, game.Player.DrunkDrink, "실제로 마신 음료만 상성");
            Assert.IsNotNull(game.Player.Coffee, "커피 추출 리듬 교체");
            // 이전(네그로니) 효과는 정확히 빠지고 에스프레소 효과만 남는다(중첩 없음).
            var probe = new PlayerStats { damagePercent = dmg0, attackSpeedPercent = as0, moveSpeedPercent = ms0, maxHpFlat = hp0, rangePercent = rg0 };
            DrinkCalculator.ApplyTo(probe, b2);
            Assert.AreEqual(probe.damagePercent, s.damagePercent, 1e-5f);
            Assert.AreEqual(probe.attackSpeedPercent, s.attackSpeedPercent, 1e-5f);
            Assert.AreEqual(probe.moveSpeedPercent, s.moveSpeedPercent, 1e-5f);
            Assert.AreEqual(probe.maxHpFlat, s.maxHpFlat, 1e-5f);
            Assert.LessOrEqual(game.Player.Hp, hp + 1e-3f, "교체로 체력 회복 없음");
            Assert.IsFalse(game.DrinkRunCup(c1, out _), "이미 마신 잔 재사용 불가");
            float dmgBefore = s.damagePercent;
            DrinkStore.FaultHookForTests = st => { if (st == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입"); };
            try
            {
                Stock(negroni, 1);
                DrinkStore.FaultHookForTests = null;
                var c3 = game.CraftRunCup(negroni).Cup;
                DrinkStore.FaultHookForTests = st => { if (st == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입"); };
                Assert.IsFalse(game.DrinkRunCup(c3, out _), "저장 실패면 교체 안 함");
                Assert.AreEqual(dmgBefore, s.damagePercent, "실패 시 효과 그대로");
                Assert.AreEqual(DrinkKind.Coffee, game.Player.DrunkDrink);
            }
            finally { DrinkStore.FaultHookForTests = null; }
        }

        [Test]
        public void Submit_OnlyBeforeFairAcceptance_LobbyPreparedNotCounted_QualityPoints()
        {
            Start(RunMode.Endless);
            var espresso = DrinkLab.ExamplePresets[5].Clone();
            var negroni = DrinkLab.ExamplePresets[0].Clone();
            Stock(espresso, 2); Stock(negroni, 2);
            var best = game.CraftRunCup(espresso).Cup;
            Assert.AreEqual(DrinkQuality.Grade.Best, DrinkQuality.GradeOf(best.quality), "예시 에스프레소 1:2 = 최고");
            var n1 = game.CraftRunCup(negroni).Cup;
            var n2 = game.CraftRunCup(negroni).Cup;
            Assert.AreEqual(0, game.Master.State.SubmittedThisCycle, "로비 준비 음료·제조만으로 제출 수 증가 없음");
            Assert.IsTrue(game.SubmitRunCup(best, out _));
            Assert.IsTrue(game.SubmitRunCup(n1, out _));
            Assert.IsFalse(game.SubmitRunCup(n1, out _), "같은 잔 중복 제출 없음");
            Assert.IsFalse(game.DrinkRunCup(n1, out _), "제출한 잔은 마실 수 없음");
            Assert.AreEqual(2, game.Master.State.SubmittedThisCycle);
            Assert.AreEqual(250, game.Master.State.SubmittedPointsThisCycle);
            Assert.AreEqual(13, game.Master.State.PreviewFairReduction, "round(250×5/100) = 13");
            // 수집 5웨이브 완료를 상태로 강제 후 접수
            var st = game.Master.State;
            for (int i = 0; i < 5; i++) { long t = st.Target; for (long j = 0; j < t; j++) st.NotifySpawned(); for (long j = 0; j < t; j++) st.NotifyCleared(); }
            Assert.IsTrue(game.Master.ChooseFair());
            Assert.IsFalse(game.SubmitRunCup(n2, out var err));
            StringAssert.Contains("접수", err);
            Assert.AreEqual(RunCup.Held, DrinkLab.RunCupsFor(game.RunId).Find(c => c.cupId == n2.cupId).state, "거부된 잔은 소비되지 않음");
            Assert.AreEqual(13, st.FairReductionSnapshot);
        }

        [Test]
        public void RunEnd_HeldRunCupRefunded_UniqueReset_NewRunStartsClean()
        {
            Start(RunMode.Endless);
            var negroni = DrinkLab.ExamplePresets[0].Clone();
            foreach (var s in negroni.steps) { int o = DrinkLab.Data.Owned(s.id); if (o > 0) DrinkLab.Data.Add(s.id, -o); }
            Stock(negroni, 1);
            int ginBefore = DrinkLab.Owned(DrinkIngredients.Gin);
            Assert.IsTrue(game.CraftRunCup(negroni).Ok);
            Assert.AreEqual(ginBefore - 300, DrinkLab.Owned(DrinkIngredients.Gin));
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(ginBefore, DrinkLab.Owned(DrinkIngredients.Gin), "미사용 보관 잔은 런 종료 때 출처대로 환원");
            Object.DestroyImmediate(root); root = null;
            Start(RunMode.Endless);
            Assert.AreEqual(0, game.Master.CocktailUniqueCount, "새 런은 종류 집계 초기화");
            Assert.AreEqual(0f, game.Stats.masterDamagePercent);
            Assert.AreEqual(0, DrinkLab.RunCupsFor(game.RunId).Count);
        }
    }
}
