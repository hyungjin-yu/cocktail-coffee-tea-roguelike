using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CCT.Tests
{
    /// <summary>40(2026-10-12): 유효 영구 해금 총수 N → 일반 스폰 배율 1+min(.60,.03N), 일반 동시 상한 floor(100×배율).
    /// 33의 체력/피해(P: 시작 무기 적용 노드)와 별개다.</summary>
    public class SpawnScalingTests
    {
        readonly List<Object> created = new List<Object>();
        List<PermanentSkillData> Nodes() { var list = DefaultPermanentSkills.CreateRuntime(); created.AddRange(list); return list; }
        [TearDown] public void Cleanup() { foreach (var item in created) Object.DestroyImmediate(item); created.Clear(); }

        [TestCase(0, 0, 1f, 100)] [TestCase(1, 3, 1.03f, 103)] [TestCase(10, 30, 1.3f, 130)]
        [TestCase(20, 60, 1.6f, 160)] [TestCase(24, 60, 1.6f, 160)]
        [TestCase(-3, 0, 1f, 100)] [TestCase(int.MaxValue, 60, 1.6f, 160)]
        public void Formula_ExactPercentMultiplierAndCap(int n, int percent, float multiplier, int cap)
        {
            Assert.AreEqual(percent, RunChallenge.SpawnBonusPercentFor(n));
            Assert.AreEqual(multiplier, RunChallenge.SpawnMultiplierFor(n), 1e-6f);
            Assert.AreEqual(cap, RunChallenge.RegularAliveCapFor(n));
            var run = new RunChallenge(0, n);
            Assert.AreEqual(multiplier, run.SpawnMultiplier, 1e-6f);
            Assert.AreEqual(cap, run.RegularAliveCap);
            Assert.LessOrEqual(run.RegularAliveCap, Balance.MaxAliveEnemiesAbsolute);
        }

        [Test]
        public void SpawnCount_DoesNotChangeHpOrDamageFormula()
        {
            foreach (int n in new[] { 0, 1, 20, 24 })
            {
                var run = new RunChallenge(18, n); run.SetCoffeeCount(15);
                Assert.AreEqual(RunChallenge.HpFor(18, 15), run.HpMultiplier, 0f, "33 체력 공식 그대로");
                Assert.AreEqual(RunChallenge.DamageFor(18, 15), run.DamageMultiplier, 0f, "33 피해 공식 그대로");
                Assert.AreEqual(33, run.Stage, "도전 단계 P+C에 N은 섞지 않는다");
            }
        }

        [Test]
        public void RegularInterval_TimeGrowthAndPermanentGrowthCompose_BossStillDoublesInterval()
        {
            foreach (float t in new[] { 0f, 120f, 600f, 850f, 1199f })
                foreach (bool boss in new[] { false, true })
                {
                    float old = Mathf.Max(Balance.SpawnIntervalMin, Balance.SpawnIntervalStart - t * Balance.SpawnIntervalDecayPerSecond);
                    if (boss) old *= 2f;
                    old /= Balance.TimeSpawnMultiplier(t);
                    Assert.AreEqual(old, EnemySpawner.RegularInterval(t, boss, RunChallenge.SpawnMultiplierFor(0)), 1e-6f, "영구0에도 시간 성장 적용");
                    Assert.AreEqual(old / 1.6f, EnemySpawner.RegularInterval(t, boss, RunChallenge.SpawnMultiplierFor(24)), 1e-6f);
                    Assert.AreEqual(old / 1.03f, EnemySpawner.RegularInterval(t, boss, RunChallenge.SpawnMultiplierFor(1)), 1e-6f);
                }
        }

        [Test]
        public void LateRunPressure_KeepsIncreasingAfterTenMinutes_AndCapsAt160()
        {
            float previousInterval = float.MaxValue;
            int previousCap = 0, previousWave = 0;
            foreach (float t in new[] { 0f, 120f, 300f, 480f, 600f, 720f, 900f, 1200f })
            {
                float interval = EnemySpawner.RegularInterval(t, false, 1f);
                Assert.Less(interval, previousInterval, "후반에도 일반 스폰 빈도가 계속 증가");
                int cap = Balance.TimeAliveCap(t, 100);
                int wave = Balance.TimeWaveCountPerSide(t);
                Assert.GreaterOrEqual(cap, previousCap);
                Assert.GreaterOrEqual(wave, previousWave);
                Assert.LessOrEqual(Balance.TimeAliveCap(t, 160), 160);
                previousInterval = interval; previousCap = cap; previousWave = wave;
            }
            Assert.AreEqual(4f, Balance.TimeSpawnMultiplier(600f));
            Assert.AreEqual(6f, Balance.TimeSpawnMultiplier(1200f));
            Assert.AreEqual(100, Balance.TimeAliveCap(0f, 100));
            Assert.AreEqual(160, Balance.TimeAliveCap(1200f, 100));
            Assert.AreEqual(1f, Balance.TimeSpawnMultiplier(-1f));
            Assert.AreEqual(6f, Balance.TimeSpawnMultiplier(float.MaxValue));
        }

        [TestCase(419.99f, 1)]
        [TestCase(420f, 2)]
        [TestCase(600f, 2)]
        [TestCase(1200f, 2)]
        public void SevenMinutes_DoublesRegularRateAndWaveCount(float seconds, int multiplier)
        {
            float previousRate = 1f + Mathf.Clamp(seconds, 0f, Balance.RunTimeLimitSeconds) / 600f;
            int previousWave = Balance.WaveCountPerSide + Mathf.FloorToInt(Mathf.Clamp(seconds, 0f, Balance.RunTimeLimitSeconds) / 180f);
            Assert.AreEqual(previousRate * multiplier, Balance.TimeSpawnMultiplier(seconds), 1e-5f);
            Assert.AreEqual(previousWave * multiplier, Balance.TimeWaveCountPerSide(seconds));
            float previousInterval = Mathf.Max(Balance.SpawnIntervalMin, Balance.SpawnIntervalStart - seconds * Balance.SpawnIntervalDecayPerSecond) / previousRate;
            Assert.AreEqual(previousInterval / multiplier, EnemySpawner.RegularInterval(seconds, false, 1f), 1e-5f);
            Assert.AreEqual(previousInterval * 2f / multiplier, EnemySpawner.RegularInterval(seconds, true, 1f), 1e-5f);
            Assert.LessOrEqual(Balance.TimeAliveCap(seconds, 160), 160);
        }

        [Test]
        public void UnlockTotal_Counts24ValidIds_ExcludesDuplicateUnknownCoffeeAndOrphans_IncludesOtherWeaponNodes()
        {
            var nodes = Nodes();
            Assert.AreEqual(24, nodes.Select(n => n.id).Distinct().Count());
            var ids = nodes.Select(n => n.id).ToList();
            ids.AddRange(new[] { ids[0], ids[1], "unknown_node", "espresso_shot", "", null });
            var tree = new PermanentSkillTree(nodes, ids, 5);
            Assert.AreEqual(24, tree.UnlockedCount, "중복/unknown/커피 노드 id 제외, 24 유효");
            foreach (var w in new[] { WeaponKind.Rapier, WeaponKind.Sword, WeaponKind.Gun, WeaponKind.Bow })
                Assert.AreEqual(18, tree.AppliedCount(w), "P는 시작 무기 기준 18, N은 다른 무기 전용 포함 24");

            var child = nodes.First(n => n.Prerequisites.Any());
            var orphan = new PermanentSkillTree(nodes, new[] { child.id }, 5);
            Assert.AreEqual(0, orphan.UnlockedCount, "선행이 빠진 옛 노드는 유효하지 않다");

            Assert.AreEqual(0, new PermanentSkillTree(nodes, new string[0], 0).UnlockedCount, "보유0 = 완전 기존");
        }

        [Test]
        public void FailedOrDuplicatePurchase_DoesNotRaiseUnlockTotal()
        {
            var nodes = Nodes();
            var tree = new PermanentSkillTree(nodes, new string[0], 0);
            var root = nodes.First(n => !n.Prerequisites.Any() && n.minimumClears == 0);
            Assert.IsFalse(tree.TryUnlock(root, 0), "포인트 부족");
            Assert.AreEqual(0, tree.UnlockedCount);
            Assert.IsTrue(tree.TryUnlock(root, 999));
            Assert.AreEqual(1, tree.UnlockedCount);
            Assert.IsFalse(tree.TryUnlock(root, 999), "중복 클릭");
            Assert.AreEqual(1, tree.UnlockedCount);
            Assert.AreEqual(103, RunChallenge.RegularAliveCapFor(tree.UnlockedCount));
        }

        [Test]
        public void SpawnStreamDraws_DoNotShiftOtherSeededStreams()
        {
            var a = new RunRandom(12345);
            var b = new RunRandom(12345);
            for (int i = 0; i < 500; i++) b.Spawns.Value(); // N이 커서 스폰 추첨이 더 많아진 런
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.Cards.Next(), b.Cards.Next(), "카드 추첨 스트림 불변");
                Assert.AreEqual(a.Varieties.Value(), b.Varieties.Value(), "품종 스트림 불변");
                Assert.AreEqual(a.BossPositions.Value(), b.BossPositions.Value(), "보스 위치 스트림 불변");
            }
        }
    }
}
