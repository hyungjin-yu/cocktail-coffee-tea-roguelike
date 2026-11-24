using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>총 전용 탄환 유도(GunHoming) 결정론 검증: 선회 상한·단계별 증가·프레임 독립·소실·중첩 상한·카드 치환(두 직업 공통).</summary>
    public class GunHomingTests
    {
        const float AngleTolerance = 0.5f; // 도. 프레임 간격이 달라도 같은 시간 뒤 선회각 허용 오차.

        readonly List<Object> created = new List<Object>();
        [TearDown] public void TearDown() { foreach (var o in created) Object.DestroyImmediate(o); created.Clear(); }

        /// <summary>표적이 멀리 90° 위에 있다고 두고, dt 간격으로 seconds초 동안 돈 총 각도.</summary>
        static float TurnedAfter(int level, float dt, float seconds, float startBudgetOverride = -1f)
        {
            Vector2 dir = Vector2.right, to = Vector2.up;
            float budget = startBudgetOverride >= 0f ? startBudgetOverride : GunHoming.BudgetDeg(level);
            for (float t = 0f; t < seconds - 1e-4f; t += dt) dir = GunHoming.Steer(dir, to, level, dt, ref budget);
            return Vector2.Angle(Vector2.right, dir);
        }

        [Test]
        public void TurnRate_IsPerSecondCap_AndGrowsPerLevel()
        {
            float prev = 0f;
            for (int lv = 1; lv <= GunHoming.MaxLevel; lv++)
            {
                Assert.AreEqual(60f * lv, GunHoming.TurnRate(lv), 1e-3f);
                float turned = TurnedAfter(lv, 0.01f, 0.1f, 1000f);
                Assert.AreEqual(GunHoming.TurnRate(lv) * 0.1f, turned, AngleTolerance, $"{lv}단계 0.1초 선회각 = 초당 선회각 × 시간");
                Assert.Greater(GunHoming.TurnRate(lv), prev); prev = GunHoming.TurnRate(lv);
            }
            Assert.AreEqual(GunHoming.TurnRate(GunHoming.MaxLevel), GunHoming.TurnRate(99), "상한 5단계 초과 없음");
            Assert.AreEqual(0f, GunHoming.TurnRate(0));
        }

        [Test]
        public void SameElapsedTime_SameAngle_AcrossFrameIntervals()
        {
            foreach (int lv in new[] { 1, 3, 5 })
            {
                float a60 = TurnedAfter(lv, 1f / 60f, 0.3f, 1000f), a30 = TurnedAfter(lv, 1f / 30f, 0.3f, 1000f), a10 = TurnedAfter(lv, 0.1f, 0.3f, 1000f);
                Assert.AreEqual(a60, a30, AngleTolerance, $"{lv}단계 60fps vs 30fps");
                Assert.AreEqual(a60, a10, AngleTolerance, $"{lv}단계 60fps vs 10fps");
            }
        }

        [Test]
        public void NoInstantSnap_AndNeverOvershootsTarget()
        {
            float budget = 1000f;
            var dir = GunHoming.Steer(Vector2.right, Vector2.up, 5, 1f / 60f, ref budget);
            Assert.Less(Vector2.Angle(Vector2.right, dir), 5.01f, "300°/s ÷ 60 = 5°: 한 프레임에 순간 꺾이지 않는다");
            // 표적이 2° 앞이면 그 각도까지만(넘어서 흔들리지 않음)
            var near = GunHoming.Steer(Vector2.right, new Vector2(Mathf.Cos(2f * Mathf.Deg2Rad), Mathf.Sin(2f * Mathf.Deg2Rad)), 5, 0.1f, ref budget);
            Assert.AreEqual(2f, Vector2.Angle(Vector2.right, near), 0.01f);
        }

        [Test]
        public void TotalTurnBudget_Limits_NoEndlessChase()
        {
            for (int lv = 1; lv <= GunHoming.MaxLevel; lv++)
            {
                float turned = TurnedAfter(lv, 1f / 60f, 2f); // 탄 수명 2초 내내 90° 밖 표적을 쫓아도
                Assert.LessOrEqual(turned, Mathf.Min(90f, GunHoming.BudgetDeg(lv)) + AngleTolerance);
            }
            Assert.Greater(GunHoming.BudgetDeg(5), GunHoming.BudgetDeg(1), "단계가 오르면 예산도 는다");
            float b = 0f;
            Assert.AreEqual(Vector2.right, GunHoming.Steer(Vector2.right, Vector2.up, 5, 0.1f, ref b), "예산 소진 후 직진");
        }

        [Test]
        public void NoHoming_Level0_And_LostTarget_GoStraight()
        {
            float budget = 100f;
            Assert.AreEqual(Vector2.right, GunHoming.Steer(Vector2.right, Vector2.up, 0, 0.1f, ref budget), "유도 미획득 = 직진");
            Assert.AreEqual(100f, budget);
            Assert.AreEqual(Vector2.right, GunHoming.Steer(Vector2.right, Vector2.zero, 3, 0.1f, ref budget), "표적 방향 없음(소실) = 직진");
            Assert.IsFalse(GunHoming.InCone(Vector2.right, Vector2.up * 2f, 0));
        }

        [Test]
        public void Cone_GrowsWithLevel_AndIsLimitedByDetectRadius()
        {
            Assert.IsFalse(GunHoming.InCone(Vector2.right, Vector2.left, 5), "뒤쪽 적은 잡지 않는다");
            Assert.Greater(GunHoming.ConeHalfDeg(5), GunHoming.ConeHalfDeg(1));
            Assert.IsFalse(GunHoming.InCone(Vector2.right, Vector2.right * (GunHoming.DetectRadius + 0.1f), 5), "탐지 반경 밖");
            Assert.IsTrue(GunHoming.InCone(Vector2.right, Vector2.right * 2f, 1));
        }

        [Test]
        public void Describe_IsBehaviorTextWithoutNumbers_AndChangesWithStack()
        {
            Assert.AreEqual("탄환이 움직이는 적을 따라갑니다", GunHoming.Describe(1));
            Assert.AreEqual("탄환이 적을 더 빠르게 따라갑니다", GunHoming.Describe(2));
            Assert.AreEqual(GunHoming.Describe(2), GunHoming.Describe(5));
        }

        CardData Range(string id, CardCategory cat)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id; c.category = cat; c.maxStacks = 5; c.effects = new[] { new CardEffect(CardEffectType.RangePercent, 0.15f) };
            created.Add(c); return c;
        }

        [Test]
        public void GunRun_RangeCards_BecomeHoming_BothJobs_RangeUnchanged_StackCap()
        {
            foreach (var (id, cat) in new[] { ("long_stirrer", CardCategory.Cocktail), ("long_spout", CardCategory.Tea) })
            {
                var stats = new PlayerStats { gunHoming = true };
                float baseRange = stats.Range;
                var system = new CardSystem(new List<CardData>());
                var card = Range(id, cat);
                for (int i = 1; i <= 5; i++)
                {
                    system.Take(card, stats);
                    Assert.AreEqual(i, stats.homingLevel, $"{id} {i}장 = {i}단계");
                    Assert.AreEqual(baseRange, stats.Range, 1e-5f, "총은 카드로 사거리가 늘지 않는다(기본 조준 거리 유지)");
                    Assert.AreEqual(0f, stats.rangePercent, 1e-6f);
                }
                system.Take(card, stats); // 카드 상한 5 — 더 쌓이지 않는다
                Assert.AreEqual(GunHoming.MaxLevel, stats.homingLevel);
            }
        }

        [Test]
        public void NonGun_RangeCard_StillRange_AndPermanentRangeStillWorksForGun()
        {
            var melee = new PlayerStats();
            new CardSystem(new List<CardData>()).Take(Range("long_stirrer", CardCategory.Cocktail), melee);
            Assert.AreEqual(0.15f, melee.rangePercent, 1e-6f);
            Assert.AreEqual(0, melee.homingLevel);

            var gun = new PlayerStats { gunHoming = true };
            gun.Apply(new CardEffect(CardEffectType.RangePercent, 0.10f)); // 영구 강화(정교한 조준)는 Apply 경로 — 사거리 유지
            Assert.AreEqual(0.10f, gun.rangePercent, 1e-6f);
            Assert.AreEqual(0, gun.homingLevel);
        }

        [Test]
        public void CardEffectsView_MapsRangeOnlyForGun_AndKeepsOtherEffects()
        {
            var fx = new[] { new CardEffect(CardEffectType.DamagePercent, 0.15f), new CardEffect(CardEffectType.RangePercent, 0.15f) };
            var gun = CardEffectsView.For(fx, true);
            Assert.AreEqual(CardEffectType.DamagePercent, gun[0].type);
            Assert.AreEqual(CardEffectType.BulletHoming, gun[1].type);
            Assert.AreEqual(CardEffectType.RangePercent, fx[1].type, "원본 카드 데이터는 변하지 않는다(ID·세이브·도감 호환)");
            Assert.AreSame(fx, CardEffectsView.For(fx, false));
        }
    }
}
