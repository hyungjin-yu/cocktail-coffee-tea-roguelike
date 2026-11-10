using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-10 끝없는 재료 모으기 수치·계열 표·상성 계산(순수 계산 — 게임 오브젝트 없음).</summary>
    public class EndlessRulesTests
    {
        [Test]
        public void Wave1_Defaults_AndMonotonicGrowth_CappedAtMaxScaledWave()
        {
            Assert.AreEqual(8, EndlessRules.WaveCount(1));
            Assert.AreEqual(100, EndlessRules.HpPercent(1));
            Assert.AreEqual(100, EndlessRules.AttackPercent(1));
            Assert.AreEqual(10, EndlessRules.WaveCount(2));
            Assert.AreEqual(115, EndlessRules.HpPercent(2));
            Assert.AreEqual(108, EndlessRules.AttackPercent(2));
            for (int w = 2; w <= EndlessRules.MaxScaledWave; w++)
            {
                Assert.Greater(EndlessRules.WaveCount(w), EndlessRules.WaveCount(w - 1));
                Assert.Greater(EndlessRules.HpPercent(w), EndlessRules.HpPercent(w - 1));
                Assert.Greater(EndlessRules.AttackPercent(w), EndlessRules.AttackPercent(w - 1));
            }
            Assert.AreEqual(EndlessRules.HpPercent(EndlessRules.MaxScaledWave), EndlessRules.HpPercent(int.MaxValue), "상한 이후 고정(오버플로 없음)");
            Assert.AreEqual(EndlessRules.WaveCount(EndlessRules.MaxScaledWave), EndlessRules.WaveCount(int.MaxValue));
        }

        [Test]
        public void WaveAt_And_SecondsToNext_AreIntegers_NoNaNAtLongTimes()
        {
            Assert.AreEqual(1, EndlessRules.WaveAt(0f));
            Assert.AreEqual(1, EndlessRules.WaveAt(29.99f));
            Assert.AreEqual(2, EndlessRules.WaveAt(30f));
            Assert.AreEqual(1, EndlessRules.WaveAt(float.NaN));
            Assert.AreEqual(30, EndlessRules.SecondsToNextWave(0f));
            Assert.AreEqual(1, EndlessRules.SecondsToNextWave(29.5f));
            Assert.AreEqual(int.MaxValue, EndlessRules.WaveAt(float.MaxValue));
            int w = EndlessRules.WaveAt(1e7f);
            Assert.Greater(w, 300000);
            Assert.IsFalse(float.IsNaN(EndlessRules.HpScale(w)) || float.IsInfinity(EndlessRules.HpScale(w)));
        }

        [Test]
        public void Focus_RotatesCoffeeTeaCocktail_Every3Waves_PreviewOneWaveBefore()
        {
            var expect = new[] { MonsterFamily.Coffee, MonsterFamily.Coffee, MonsterFamily.Coffee, MonsterFamily.Tea, MonsterFamily.Tea, MonsterFamily.Tea,
                MonsterFamily.Cocktail, MonsterFamily.Cocktail, MonsterFamily.Cocktail, MonsterFamily.Coffee };
            for (int i = 0; i < expect.Length; i++) Assert.AreEqual(expect[i], EndlessRules.FocusFor(i + 1), $"wave {i + 1}");
            for (int wave = 1; wave <= 30; wave++)
                Assert.AreEqual(EndlessRules.FocusFor(wave) != EndlessRules.FocusFor(wave + 1), EndlessRules.IsFocusPreviewWave(wave),
                    $"예고는 실제 전환 바로 전 웨이브에만: {wave}");
        }

        [Test]
        public void EndlessTable_WeightsSum100_EveryFamilyHasCandidates_ForBothJobs()
        {
            foreach (Job? job in new Job?[] { Job.Bartender, Job.TeaSommelier, null })
                for (int wave = 1; wave <= 12; wave++)
                    CollectionAssert.IsEmpty(EnemyRoster.ValidateEndlessTable(job, wave), $"job={job} wave={wave}");
        }

        [Test]
        public void PickEndless_SameSeedSameSequence_DistributionNear70_15_15_AndFamilyMatchesTable()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                var a = new SeededRandomSource(1234);
                var b = new SeededRandomSource(1234);
                for (int i = 0; i < 500; i++)
                {
                    var da = EnemyRoster.PickEndless(1, 300f, a, job, out var fa);
                    var db = EnemyRoster.PickEndless(1, 300f, b, job, out var fb);
                    Assert.AreSame(da, db, "같은 시드 → 같은 적");
                    Assert.AreEqual(fa, fb);
                }
                var rng = new SeededRandomSource(99);
                var counts = new Dictionary<MonsterFamily, int>();
                const int n = 20000;
                for (int i = 0; i < n; i++)
                {
                    var def = EnemyRoster.PickEndless(4, 300f, rng, job, out var fam); // 웨이브 4 = 차 집중
                    Assert.AreEqual(fam, MonsterFamilies.Of(def), def.name);
                    counts[fam] = counts.TryGetValue(fam, out int c) ? c + 1 : 1;
                }
                Assert.AreEqual(0.70, counts[MonsterFamily.Tea] / (double)n, 0.02, $"{job} 집중");
                Assert.AreEqual(0.15, counts[MonsterFamily.Coffee] / (double)n, 0.02, $"{job} 커피");
                Assert.AreEqual(0.15, counts[MonsterFamily.Cocktail] / (double)n, 0.02, $"{job} 칵테일(찻집도 끝없는 모드에서만 등장)");
            }
        }

        [Test]
        public void StandardTable_UnchangedFromExistingRoster_AndReportsTeaHouseGaps()
        {
            EnemyRoster.StandardFamilyPercents(Job.Bartender, 0f, out int c, out int t, out int k);
            Assert.AreEqual((65, 35, 0), (c, t, k));
            EnemyRoster.StandardFamilyPercents(Job.Bartender, 200f, out c, out t, out k);
            Assert.AreEqual(100, c + t + k);
            Assert.AreEqual(2, k);
            EnemyRoster.StandardFamilyPercents(Job.TeaSommelier, 200f, out c, out t, out k);
            Assert.AreEqual((0, 100, 0), (c, t, k), "정규 찻집은 칵테일·커피 계열 일반 몹 없음(기존 규칙 보존) — 보고 대상");
            // 정규 추첨 자체도 그대로: 찻집 1만 회 추첨에 칵테일·커피 계열 0
            var rng = new SeededRandomSource(5);
            for (int i = 0; i < 10000; i++)
            {
                var f = MonsterFamilies.Of(EnemyRoster.PickCommon(400f, rng, Job.TeaSommelier));
                Assert.AreEqual(MonsterFamily.Tea, f);
            }
        }

        [Test]
        public void EveryStaticEnemyDef_IsExplicitlyClassified()
        {
            var defs = typeof(EnemyDef).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(EnemyDef)).Select(f => (EnemyDef)f.GetValue(null)).ToList();
            Assert.Greater(defs.Count, 20);
            foreach (var d in defs) Assert.IsTrue(MonsterFamilies.IsClassified(d), d.name + " 명시 분류 없음");
            Assert.AreEqual(MonsterFamily.Tea, MonsterFamilies.Of(EnemyDef.TeaLeafScholar), "원두 드롭이어도 차 테마");
            Assert.AreEqual(MonsterFamily.Neutral, MonsterFamilies.Of(EnemyDef.DifficultCustomer));
            Assert.AreEqual(MonsterFamily.Neutral, MonsterFamilies.Of(new EnemyDef { name = "unknown" }));
        }

        [Test]
        public void Affinity_3x3Matrix_NeutralAndNoDrink()
        {
            var fams = new[] { MonsterFamily.Cocktail, MonsterFamily.Coffee, MonsterFamily.Tea, MonsterFamily.Neutral };
            var expected = new Dictionary<DrinkKind, MonsterFamily>
            {
                { DrinkKind.Cocktail, MonsterFamily.Coffee }, { DrinkKind.Coffee, MonsterFamily.Tea }, { DrinkKind.Tea, MonsterFamily.Cocktail },
            };
            foreach (var kv in expected)
                foreach (var f in fams)
                    Assert.AreEqual(f == kv.Value ? 20 : 0, DrinkAffinity.Bonus(100, kv.Key, f), $"{kv.Key} → {f}");
            foreach (var f in fams) Assert.AreEqual(0, DrinkAffinity.Bonus(100, null, f), "미음용");
        }

        [Test]
        public void AffinityBonus_RoundingMinimumAndOverflow()
        {
            Assert.AreEqual(20, DrinkAffinity.Bonus(100, DrinkKind.Cocktail, MonsterFamily.Coffee));
            Assert.AreEqual(1, DrinkAffinity.Bonus(1, DrinkKind.Cocktail, MonsterFamily.Coffee), "최소 1");
            Assert.AreEqual(1, DrinkAffinity.Bonus(7, DrinkKind.Cocktail, MonsterFamily.Coffee), "1.4 → 1");
            Assert.AreEqual(2, DrinkAffinity.Bonus(8, DrinkKind.Cocktail, MonsterFamily.Coffee), "1.6 → 2");
            Assert.AreEqual(3, DrinkAffinity.Bonus(13, DrinkKind.Cocktail, MonsterFamily.Coffee), "2.6 → 3");
            Assert.AreEqual(1, DrinkAffinity.Bonus(5, DrinkKind.Cocktail, MonsterFamily.Coffee), "1.0");
            Assert.AreEqual(3, DrinkAffinity.Bonus(15, DrinkKind.Cocktail, MonsterFamily.Coffee), "3.0");
            Assert.AreEqual(0, DrinkAffinity.Bonus(0, DrinkKind.Cocktail, MonsterFamily.Coffee), "0 피해는 상성 없음");
            int big = DrinkAffinity.Bonus(int.MaxValue, DrinkKind.Cocktail, MonsterFamily.Coffee);
            Assert.Greater(big, 0);
            int saved = DrinkAffinity.BonusPercent;
            try { DrinkAffinity.BonusPercent = 0; Assert.AreEqual(0, DrinkAffinity.Bonus(100, DrinkKind.Cocktail, MonsterFamily.Coffee)); }
            finally { DrinkAffinity.BonusPercent = saved; }
            Assert.AreEqual("커피 계열 추가 피해 +20%", DrinkAffinity.Describe(DrinkKind.Cocktail));
        }

        [Test]
        public void DamageNumberFormat_ShowsTotalWithBonus_NotTwoNumbers()
        {
            Assert.AreEqual("120 (추가 20)", DamageNumbers.Format(120f, 20, false, false));
            Assert.AreEqual("120! (추가 20)\n상성!", DamageNumbers.Format(120f, 20, true, true));
            Assert.AreEqual("100", DamageNumbers.Format(100f, 0, false, true));
        }

        [Test]
        public void RunRecord_OldRecordsWithoutMode_AreStandard()
        {
            var old = JsonUtility.FromJson<RunRecord>("{\"runId\":\"a\",\"endKind\":\"Died\"}");
            Assert.AreEqual(RunMode.Standard, old.Mode);
            Assert.AreEqual(0, old.endlessWave);
            Assert.AreEqual(RunMode.Endless, JsonUtility.FromJson<RunRecord>("{\"mode\":\"Endless\"}").Mode);
            Assert.AreEqual("", RunRecordText.ModeSuffix(old), "이전 기록 문구 그대로");
            StringAssert.Contains("끝없는 재료 모으기 웨이브 7", RunRecordText.ModeSuffix(new RunRecord { mode = "Endless", endlessWave = 7 }));
        }
    }

    /// <summary>실제 GameManager(EditMode)로 끝없는 모드 종료 분기·스포너·상성 단일 피해 결과를 확인한다. 저장은 TestSaveIsolation 임시 경로.</summary>
    public class EndlessGameTests
    {
        GameObject root, createdCamera;
        GameManager game;

        void Start(RunMode mode, Job job = Job.Bartender)
        {
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(job, WeaponKind.Rapier, mode);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (createdCamera != null) Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
            RunModeSelection.Current = RunMode.Standard;
        }

        [Test]
        public void Endless_NoBossSchedule_TimeLimitDoesNotEnd_DeathEndsOnce_RecordHasModeAndWave()
        {
            Start(RunMode.Endless);
            Assert.IsTrue(game.IsEndless);
            Assert.AreEqual(0, game.Director.ScheduleCount, "정규 보스 스케줄 비활성");
            Assert.IsNull(game.Director.NextSpec, "가짜 보스 예고 없음");
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds + 500f);
            game.Spawner.TickEndlessForTests(game.RunTime);
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.Playing, game.State, "시간 초과로 끝나지 않음");
            var info = RunPhaseBadge.Compute(game);
            Assert.AreEqual(RunPhaseState.Endless, info.state);
            StringAssert.Contains("끝없는 재료 모으기 · 웨이브 57", info.text);
            StringAssert.DoesNotContain("보스", info.text);

            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual("사망", game.EndReason);
            Assert.IsNotNull(game.RunRecord);
            Assert.AreEqual("Endless", game.RunRecord.mode);
            Assert.AreEqual(57, game.RunRecord.endlessWave);
            var settlement = game.DrinkSettlement;
            game.ResolveEndOfFrame();
            game.GiveUp();
            Assert.AreSame(settlement, game.DrinkSettlement, "중복 정산 없음");
            Assert.IsNull(game.Player.DrunkDrink, "런 종료 즉시 상성 해제");
        }

        [Test]
        public void Standard_TimeLimitStillEnds_AndBossScheduleKept()
        {
            Start(RunMode.Standard);
            Assert.AreEqual(4, game.Director.ScheduleCount);
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            game.ResolveEndOfFrame();
            Assert.AreEqual("시간 초과", game.EndReason);
            Assert.AreEqual("Standard", game.RunRecord.mode);
        }

        [Test]
        public void EndlessSpawner_DistributesWave_CarriesPending_RespectsAliveCap_ScalesNewEnemiesOnly()
        {
            Start(RunMode.Endless);
            var sp = game.Spawner;
            sp.TickEndlessForTests(0f);
            Assert.AreEqual(1, sp.EndlessWave);
            Assert.AreEqual(0, sp.EndlessReleased, "구간 시작에 한꺼번에 몰아내지 않음");
            sp.TickEndlessForTests(15f);
            Assert.AreEqual(4, sp.EndlessReleased, "8 × 15/30");
            for (int i = 0; i < 10; i++) sp.TickEndlessForTests(15f);
            Assert.AreEqual(4, sp.EndlessSpawned);
            var first = Enemy.Active.ToList();
            Assert.IsTrue(first.All(e => e.HpScale == 1f && e.AttackScale == 1f));

            sp.TickEndlessForTests(31f);
            Assert.AreEqual(2, sp.EndlessWave);
            for (int i = 0; i < 10; i++) sp.TickEndlessForTests(31f);
            Assert.AreEqual(8, sp.EndlessSpawned, "앞 웨이브 잔여분은 이월되어 생성");
            var newer = Enemy.Active.Except(first).ToList();
            Assert.IsTrue(newer.All(e => Mathf.Approximately(e.HpScale, 1.15f) && Mathf.Approximately(e.AttackScale, 1.08f)), "신규 적만 웨이브 2 수치");
            Assert.IsTrue(first.All(e => e.HpScale == 1f), "살아 있는 적은 그대로");

            // 장시간 경계: 큰 시간 점프에도 예외·무제한 대기 없음, 동시 상한 유지
            sp.TickEndlessForTests(30f * 5000f);
            for (int i = 0; i < 200; i++) sp.TickEndlessForTests(30f * 5000f + 29f);
            Assert.LessOrEqual(sp.EndlessPending, EndlessRules.MaxPending);
            Assert.LessOrEqual(Enemy.Active.Count, Balance.MaxAliveEnemiesAbsolute);
            Assert.LessOrEqual(Enemy.Active.Count, sp.RegularCap);
            Assert.Greater(sp.EndlessPending, 0, "상한에 막힌 적은 사라지지 않고 대기");
        }

        [Test]
        public void EndlessAttackScale_AppliesToContactProjectileAndSummons_WithoutTouchingSharedDefs()
        {
            Start(RunMode.Endless);
            float contactBefore = EnemyDef.Imp.contactDamage;
            var e = Enemy.SpawnAt(Vector2.zero, EnemyDef.ShakerGolem, EndlessRules.HpScale(11), game.Player, root.transform, EndlessRules.AttackScale(11));
            Assert.AreEqual(180, EndlessRules.AttackPercent(11));
            Assert.AreEqual(18f, e.ScaledAttack(EnemyDef.ShakerGolem.projectileDamage), "투사체 10 × 180%");
            Assert.AreEqual(9f, e.ScaledAttack(EnemyDef.ShakerGolem.contactDamage), "접촉 5 × 180%");
            Assert.AreEqual(Mathf.Max(1, IntNum.Round(55f * 2.5f * game.Challenge.HpMultiplier)), (int)e.MaxHp, "체력 55 × 250% (영구 도전 배율은 기존대로)");
            Assert.AreEqual(contactBefore, EnemyDef.Imp.contactDamage, "공유 정의 불변");
            var normal = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 1f, game.Player, root.transform);
            Assert.AreEqual(EnemyDef.Imp.contactDamage, normal.ScaledAttack(EnemyDef.Imp.contactDamage), "배율 1은 원래 값 그대로");
        }

        [Test]
        public void DirectHit_AffinityIsSingleDamageResult_KillCountedOnce_NeutralAndDotUnaffected()
        {
            Start(RunMode.Standard);
            game.Player.DrunkDrink = DrinkKind.Cocktail; // 칵테일을 마셨다 → 커피 계열 +20%
            var bug = Enemy.SpawnAt(Vector2.zero, EnemyDef.EspressoBug, 5f, game.Player, root.transform); // HP 100 × 도전 배율
            Assert.Greater(bug.MaxHp, 30f);
            float before = bug.Hp;
            var r = bug.TakeDirectHit(10f, HitKind.Normal);
            Assert.AreEqual((10, 2, 12), (r.baseDamage, r.bonusDamage, r.total));
            Assert.AreEqual(before - 12f, bug.Hp);

            bug.TakeDamage(10f, HitKind.Dot);
            Assert.AreEqual(before - 22f, bug.Hp, "지속 피해는 상성 없음");

            var tea = Enemy.SpawnAt(Vector2.zero, EnemyDef.TeabagImp, 5f, game.Player, root.transform);
            Assert.AreEqual(0, tea.TakeDirectHit(10f, HitKind.Normal).bonusDamage, "칵테일 음료 → 차 계열은 미일치");
            var customer = Enemy.SpawnAt(Vector2.zero, EnemyDef.DifficultCustomer, 1f, game.Player, root.transform);
            customer.Invulnerable = false;
            Assert.AreEqual(0, customer.TakeDirectHit(10f, HitKind.Normal).bonusDamage, "중립");

            bug.Invulnerable = true;
            float hpInv = bug.Hp;
            Assert.AreEqual(0, bug.TakeDirectHit(50f, HitKind.Critical).total, "무적: 결과 없음");
            Assert.AreEqual(hpInv, bug.Hp);
            bug.Invulnerable = false;
            Assert.AreEqual(0, bug.TakeDirectHit(0f, HitKind.Normal).total, "0 피해는 상성 없음");

            int kills = game.Kills;
            // EditMode에서는 Die()의 Destroy가 오류 로그를 남긴다(실제 플레이는 정상) — 처치 경로 자체만 확인한다.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called"));
            bug.TakeDirectHit(bug.Hp, HitKind.Normal); // 막타: B만으로 처치, 추가분이 있어도 처치는 한 번
            Assert.IsTrue(bug.IsDead);
            Assert.AreEqual(kills + 1, game.Kills, "처치 1회");
            bug.TakeDirectHit(10f, HitKind.Normal);
            Assert.AreEqual(kills + 1, game.Kills, "죽은 적 재처치 없음");

            game.Player.DrunkDrink = DrinkKind.Tea; // 교체 시 즉시 갱신
            var lime = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 5f, game.Player, root.transform);
            Assert.AreEqual(2, lime.TakeDirectHit(10f, HitKind.Normal).bonusDamage, "차 → 칵테일");
            game.Player.DrunkDrink = null;
            Assert.AreEqual(0, lime.TakeDirectHit(10f, HitKind.Normal).bonusDamage, "미음용·만료");
        }

        [Test]
        public void EndlessSpecialty_NotGranted_StandardStillGranted()
        {
            Start(RunMode.Endless);
            int before = PermanentProgress.Current.specialtyPoints;
            game.OnEnemyKilled(Vector2.zero, EnemyDef.EspressoLeviathan); // 강화 보스 처치 경로를 직접 통과(무한 모드에는 원래 등장하지 않음)
            Assert.AreEqual(before, PermanentProgress.Current.specialtyPoints, "끝없는 모드: 보스 클리어 보상 없음");
            Object.DestroyImmediate(root); root = null;
            Start(RunMode.Standard);
            before = PermanentProgress.Current.specialtyPoints;
            game.OnEnemyKilled(Vector2.zero, EnemyDef.EspressoLeviathan);
            Assert.AreEqual(before + Balance.SpecialtyDropAmount, PermanentProgress.Current.specialtyPoints, "정규 모드 기존 지급 유지");
        }

        [Test]
        public void ModeSelection_DefaultsStandard_InitializeReadsSelection()
        {
            Assert.AreEqual(RunMode.Standard, RunModeSelection.Current);
            RunModeSelection.Current = RunMode.Endless;
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.TeaSommelier, WeaponKind.Sword);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
            Assert.AreEqual(RunMode.Endless, game.Mode);
        }
    }
}
