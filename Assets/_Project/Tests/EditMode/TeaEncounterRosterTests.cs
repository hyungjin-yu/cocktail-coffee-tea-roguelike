using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>2026-10-12 찻집 전용 몬스터(새싹 찻잎 정령·차시 궁수·차 연구 정령)와 보스 3+최종 일정: 찻집 몹 풀, 바 무변경, 원본 전투 수치 유지.</summary>
    public class TeaEncounterRosterTests
    {
        static readonly EnemyDef[] TeaHousePool =
        {
            EnemyDef.SpringLeafSprite, EnemyDef.BambooScoopArcher, EnemyDef.TeabagImp, EnemyDef.KettleCannon,
            EnemyDef.SugarCubeBomber, EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian, EnemyDef.TeaLeafScholar,
        };
        static readonly EnemyDef[] TeaOnly = { EnemyDef.SpringLeafSprite, EnemyDef.BambooScoopArcher, EnemyDef.TeaLeafScholar };

        static Dictionary<EnemyDef, int> Sample(Job? job, float t, int n, int seed)
        {
            var rng = new SeededRandomSource(seed);
            var counts = new Dictionary<EnemyDef, int>();
            for (int i = 0; i < n; i++)
            {
                var d = EnemyRoster.PickCommon(t, rng, job);
                counts[d] = counts.TryGetValue(d, out var c) ? c + 1 : 1;
            }
            return counts;
        }

        [Test]
        public void TeaHouse_PicksOnlyTeaHousePool_AndNewTeaMonstersAppear()
        {
            var seen = new HashSet<EnemyDef>();
            foreach (var t in new[] { 0f, 20f, 50f, 70f, 130f, 160f, 400f, 700f })
                foreach (var kv in Sample(Job.TeaSommelier, t, 3000, 9100 + (int)t))
                {
                    CollectionAssert.Contains(TeaHousePool, kv.Key, $"찻집 풀 밖 몹 {kv.Key.name} t={t}");
                    seen.Add(kv.Key);
                }
            foreach (var d in TeaHousePool) Assert.IsTrue(seen.Contains(d), "찻집 풀 미등장 " + d.name);
            var early = Sample(Job.TeaSommelier, 10f, 2000, 1);
            Assert.IsFalse(early.ContainsKey(EnemyDef.BambooScoopArcher), "원거리는 15초 전 없음(기존 규칙)");
            Assert.IsFalse(Sample(Job.TeaSommelier, 100f, 3000, 2).ContainsKey(EnemyDef.TeaLeafScholar), "커피 몫(150초 전)은 없음");
        }

        [Test]
        public void TeaHouse_RangedSplitBetweenArcherAndKettle_BeforeCoffee()
        {
            var c = Sample(Job.TeaSommelier, 100f, 20000, 77);
            int archer = c.GetValueOrDefault(EnemyDef.BambooScoopArcher), kettle = c.GetValueOrDefault(EnemyDef.KettleCannon);
            Assert.Greater(archer, 0); Assert.Greater(kettle, 0);
            Assert.AreEqual(1f, archer / (float)kettle, 0.15f, "원거리 몫 0.4를 차시 궁수·주전자 대포가 반씩");
            Assert.Greater(c.GetValueOrDefault(EnemyDef.SpringLeafSprite), 0);
            Assert.IsFalse(c.ContainsKey(EnemyDef.TeabagImp) && c[EnemyDef.TeabagImp] > c[EnemyDef.SpringLeafSprite],
                "돌격 기본은 새싹 찻잎 정령, 티백 임프는 추가 몫");
        }

        [Test]
        public void Bartender_AndNoJob_NeverGetTeaHouseOnlyMonsters()
        {
            foreach (Job? job in new Job?[] { Job.Bartender, null })
                foreach (var t in new[] { 20f, 200f, 700f })
                    foreach (var d in TeaOnly)
                        Assert.IsFalse(Sample(job, t, 3000, 5).ContainsKey(d), $"{job?.ToString() ?? "없음"} t={t} {d.name}");
        }

        [Test]
        public void TeaSchedule_SameTimesAndKinds_TeaDefs_BarUnchanged()
        {
            var bar = BossDirector.DefaultSchedule(Job.Bartender);
            var tea = BossDirector.DefaultSchedule(Job.TeaSommelier);
            var basic = BossDirector.DefaultSchedule();
            Assert.AreEqual(basic.Count, tea.Count);
            CollectionAssert.AreEqual(basic.Select(s => s.def).ToArray(), bar.Select(s => s.def).ToArray(), "바 일정 무변경");
            CollectionAssert.AreEqual(new[] { EnemyDef.GaiwanSentinel, EnemyDef.SteamKettleElder, EnemyDef.TeaKilnDragon, EnemyDef.TeaCustomer },
                tea.Select(s => s.def).ToArray());
            for (int i = 0; i < basic.Count; i++)
            {
                Assert.AreEqual(basic[i].time, tea[i].time, "보스 등장 시각 동일");
                Assert.AreEqual(basic[i].hpScale, tea[i].hpScale);
                Assert.AreEqual(basic[i].def.boss, tea[i].def.boss, "보스 종류(보상 경로) 동일");
            }
            Assert.AreSame(EnemyDef.GrinderGolem, BossDirector.DefaultSchedule()[0].def, "기본 일정 객체가 직업 일정에 오염되지 않음");
        }

        [Test]
        public void TeaVariants_KeepSourceCombatNumbers_ChangeOnlyIdentity()
        {
            var pairs = new[]
            {
                (EnemyDef.GaiwanSentinel, EnemyDef.GrinderGolem), (EnemyDef.SteamKettleElder, EnemyDef.DripServerSpecter),
                (EnemyDef.TeaKilnDragon, EnemyDef.EspressoLeviathan), (EnemyDef.TeaCustomer, EnemyDef.DifficultCustomer),
                (EnemyDef.SpringLeafSprite, EnemyDef.TeabagImp), (EnemyDef.BambooScoopArcher, EnemyDef.KettleCannon),
                (EnemyDef.TeaLeafScholar, EnemyDef.EspressoBug),
            };
            foreach (var (v, s) in pairs)
            {
                string n = v.name;
                Assert.AreNotEqual(s.name, v.name, n);
                Assert.AreEqual(s.hp, v.hp, n); Assert.AreEqual(s.speed, v.speed, n); Assert.AreEqual(s.radius, v.radius, n);
                Assert.AreEqual(s.contactDamage, v.contactDamage, n); Assert.AreEqual(s.boss, v.boss, n);
                Assert.AreEqual(s.archetype, v.archetype, n); Assert.AreEqual(s.dropType, v.dropType, n);
                Assert.AreEqual(s.dropCount, v.dropCount, n); Assert.AreEqual(s.ingredientAmount, v.ingredientAmount, n);
                Assert.AreEqual(s.keepDistance, v.keepDistance, n); Assert.AreEqual(s.projectileDamage, v.projectileDamage, n);
                Assert.AreEqual(s.attackInterval, v.attackInterval, n);
            }
            foreach (var v in new[] { EnemyDef.GaiwanSentinel, EnemyDef.SteamKettleElder, EnemyDef.TeaKilnDragon, EnemyDef.SpringLeafSprite, EnemyDef.BambooScoopArcher })
                CollectionAssert.Contains(TeaEncounterArt.Ids, v.ArtId, "새 PixelLab 몸체 아트 id " + v.name);
        }
    }
}
