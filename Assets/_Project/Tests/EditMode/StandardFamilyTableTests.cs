using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-10 진상 보스 영업 계열 표 선택(사용자 승인 옵션): 기본 직업 테마 표는 기존과 완전히 같고,
    /// "세 계열 고르게"는 모든 음료 상성 대상이 나오며 보상 출처(드롭·보급·히든)는 직업 테마 추첨 결과 그대로.</summary>
    public class StandardFamilyTableTests
    {
        [Test]
        public void JobTheme_IsIdenticalToExistingPickCommon_SameSeedSameSequence()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                var a = new SeededRandomSource(42); var b = new SeededRandomSource(42); var remap = new SeededRandomSource(7);
                for (int i = 0; i < 3000; i++)
                {
                    float t = i * 0.4f;
                    var old = EnemyRoster.PickCommon(t, a, job);
                    var now = EnemyRoster.PickStandard(t, b, remap, job, SpawnTable.JobTheme, out var reward);
                    Assert.AreSame(old, now, $"{job} #{i}");
                    Assert.AreSame(now, reward);
                }
            }
        }

        [Test]
        public void AllFamilies_KeepsSpawnStreamAndRewards_SameSlot_AllThreeFamiliesAppear()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                var a = new SeededRandomSource(11); var b = new SeededRandomSource(11); var remap = new SeededRandomSource(3);
                var fams = new Dictionary<MonsterFamily, int>();
                for (int i = 0; i < 6000; i++)
                {
                    float t = 30f + i * 0.2f; // 30초~1230초: 모든 아키타입 시간 조건 포함
                    var theme = EnemyRoster.PickCommon(t, a, job);
                    var pick = EnemyRoster.PickStandard(t, b, remap, job, SpawnTable.AllFamilies, out var reward);
                    Assert.AreSame(theme, reward, "보상 출처 = 직업 테마 결과(같은 스폰 난수 소비)");
                    Assert.AreEqual(theme.archetype, pick.archetype, "같은 자리(아키타입)");
                    Assert.AreEqual(theme.IsBoss, pick.IsBoss);
                    var f = MonsterFamilies.Of(pick);
                    fams[f] = fams.TryGetValue(f, out int c) ? c + 1 : 1;
                }
                foreach (var f in new[] { MonsterFamily.Cocktail, MonsterFamily.Tea, MonsterFamily.Coffee })
                    Assert.Greater(fams.TryGetValue(f, out int n) ? n : 0, 300, $"{job}: {f} 계열 등장 → 해당 음료 상성 가능");
            }
        }

        [Test]
        public void AllFamilies_SameSeed_Reproducible()
        {
            var seqA = new List<EnemyDef>(); var seqB = new List<EnemyDef>();
            for (int run = 0; run < 2; run++)
            {
                var s = new SeededRandomSource(99); var r = new SeededRandomSource(5);
                var seq = run == 0 ? seqA : seqB;
                for (int i = 0; i < 1000; i++) seq.Add(EnemyRoster.PickStandard(200f + i, s, r, Job.TeaSommelier, SpawnTable.AllFamilies, out _));
            }
            CollectionAssert.AreEqual(seqA, seqB);
        }

        [Test]
        public void SlotCandidates_AreExistingDefs_ExplicitlyClassified()
        {
            foreach (EnemyRoster.Slot slot in System.Enum.GetValues(typeof(EnemyRoster.Slot)))
                foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
                    foreach (var d in EnemyRoster.SlotCandidates(slot, job))
                    {
                        Assert.IsTrue(MonsterFamilies.IsClassified(d), d.name);
                        Assert.IsFalse(d.IsBoss);
                    }
        }

        [Test]
        public void Legend_UsesTextMarks_NotColorOnly_AndNamesTarget()
        {
            string none = FamilyLegend.Compose(null);
            foreach (var mark in new[] { "[칵] 칵테일", "[커] 커피", "[차] 차", "[중] 중립" }) StringAssert.Contains(mark, none);
            StringAssert.Contains("상성 없음", none);
            StringAssert.Contains("상성 대상 [커] 커피 +20%", FamilyLegend.Compose(DrinkKind.Cocktail));
            StringAssert.Contains("상성 대상 [차] 차 +20%", FamilyLegend.Compose(DrinkKind.Coffee));
            StringAssert.Contains("상성 대상 [칵] 칵테일 +20%", FamilyLegend.Compose(DrinkKind.Tea));
        }

        [Test]
        public void RecordFields_OldRecordsDefaultToJobTheme()
        {
            var old = JsonUtility.FromJson<RunRecord>("{\"runId\":\"x\"}");
            Assert.AreEqual(SpawnTable.JobTheme, RunModeSelection.ParseTable(old.spawnTable));
            Assert.AreEqual(SpawnTable.AllFamilies, RunModeSelection.ParseTable("AllFamilies"));
        }
    }

    /// <summary>실제 GameManager: 계열 표 선택이 런에 고정되고, 바뀐 몬스터를 처치해도 보상은 직업 테마 정의 기준.</summary>
    public class StandardFamilyTableGameTests
    {
        GameObject root, createdCamera;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (createdCamera != null) Object.DestroyImmediate(createdCamera);
            RunModeSelection.StandardTable = SpawnTable.JobTheme;
            Time.timeScale = 1f;
        }

        GameManager Start(SpawnTable table, Job job)
        {
            RunModeSelection.StandardTable = table;
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            var g = root.AddComponent<GameManager>();
            g.Initialize(job, job == Job.TeaSommelier ? WeaponKind.Sword : WeaponKind.Rapier, RunMode.Standard);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
            return g;
        }

        [Test]
        public void TeaHouse_AllFamilies_SpawnsCocktailOrCoffee_RewardsStayTea_RecordTable()
        {
            var g = Start(SpawnTable.AllFamilies, Job.TeaSommelier);
            Assert.AreEqual(SpawnTable.AllFamilies, g.SpawnTable);
            Assert.AreEqual(4, g.Director.ScheduleCount, "보스 일정 그대로");
            g.SetRunTimeForTests(400f);
            bool sawOther = false;
            for (int i = 0; i < 60; i++)
            {
                g.Spawner.SpawnOneForTests();
            }
            foreach (var e in Enemy.Active)
            {
                if (e == null || e.Def.IsBoss) continue;
                var f = e.Family;
                if (f == MonsterFamily.Cocktail || f == MonsterFamily.Coffee)
                {
                    sawOther = true;
                    Assert.IsNotNull(e.RewardDef, "바뀐 몬스터는 보상 출처를 가진다");
                    Assert.AreEqual(MonsterFamily.Tea, MonsterFamilies.Of(e.RewardDef), "찻집 보상은 차 테마 그대로");
                }
            }
            Assert.IsTrue(sawOther, "찻집에서도 칵테일·커피 계열 등장(옵션)");
            g.MarkPlayerDiedForTests(); g.ResolveEndOfFrame();
            Assert.AreEqual("AllFamilies", g.RunRecord.spawnTable);
        }

        [Test]
        public void Default_JobTheme_TeaHouseHasOnlyTea()
        {
            var g = Start(SpawnTable.JobTheme, Job.TeaSommelier);
            g.SetRunTimeForTests(400f);
            for (int i = 0; i < 60; i++) g.Spawner.SpawnOneForTests();
            foreach (var e in Enemy.Active)
                if (e != null && !e.Def.IsBoss) { Assert.AreEqual(MonsterFamily.Tea, e.Family); Assert.IsNull(e.RewardDef); }
        }

        [Test]
        public void KillingRemappedEnemy_DropsThemeIngredient()
        {
            var g = Start(SpawnTable.AllFamilies, Job.TeaSommelier);
            var e = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 1f, g.Player, root.transform); // 칵테일 계열 외형·능력치
            e.RewardDef = EnemyDef.TeabagImp;                                                // 보상 출처 = 차 테마
            int kills = g.Kills;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called"));
            e.TakeDamage(e.Hp + 1f);
            Assert.AreEqual(kills + 1, g.Kills);
            bool cocktailPickup = false, teaPickup = false;
            foreach (var p in Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
            {
                if (p.Type == IngredientType.Cocktail) cocktailPickup = true;
                if (p.Type == IngredientType.Tea) teaPickup = true;
            }
            Assert.IsTrue(teaPickup, "차 재료 드롭");
            Assert.IsFalse(cocktailPickup, "칵테일 재료 없음(찻집 보상 보존)");
        }
    }
}
