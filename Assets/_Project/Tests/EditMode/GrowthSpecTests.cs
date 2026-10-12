using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>07장 §2(게이지)·§3(카드 추첨)을 숫자 그대로 검증한다(G15/G16, 2026-09-28). 기존
    /// IngredientGaugeTests/CardSystemTests는 초창기 가안 수치(10→15→22)나 동작 위주라 07 v1.0의 확정
    /// 수치와 직접 대조하는 테스트가 없었다.</summary>
    public class GrowthSpecTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        static IngredientGauge RealGauge(System.Func<float> mult = null) =>
            new IngredientGauge(IngredientType.Cocktail, Balance.CocktailGaugeStart, Balance.CocktailGaugeGrowth, mult);

        // ---- 07:30 "8→11→15→21→29…" (float 곱셈 뒤 floor가 경계에서 한 칸 밀리면 안 됨) ----

        [Test]
        public void RealGauge_Thresholds_Follow_8_11_15_21_29_40()
        {
            var gauge = RealGauge();
            var expected = new[] { 8, 11, 15, 21, 29, 40 };
            foreach (int need in expected)
            {
                Assert.AreEqual(need, gauge.Threshold);
                Assert.AreEqual(1, gauge.Add(need), $"요구량 {need}을 넣으면 정확히 1레벨업");
                Assert.AreEqual(0, gauge.Current);
            }
        }

        [Test]
        public void RealGauge_StartsAtLevel1_Current0()
        {
            var gauge = RealGauge();
            Assert.AreEqual(1, gauge.Level);
            Assert.AreEqual(0, gauge.Current);
        }

        // ---- 07:30 "한 번에 여러 번 레벨업 가능, 남은 양 보존" ----

        [Test]
        public void RealGauge_BigPickup_LevelsMultipleTimes_AndKeepsRemainder()
        {
            var gauge = RealGauge();
            int fired = 0;
            gauge.LeveledUp += _ => fired++;

            int ups = gauge.Add(8 + 11 + 15 + 3); // 세 번 레벨업 + 잔량 3

            Assert.AreEqual(3, ups);
            Assert.AreEqual(3, fired);
            Assert.AreEqual(4, gauge.Level);
            Assert.AreEqual(3, gauge.Current);
        }

        // ---- 07:30 요구량=최대(3, RoundToInt(기본요구×배율)), .5는 가장 가까운 짝수로 ----

        [TestCase(11f, 0.5f, 6)]  // 5.5 → 6 (짝수)
        [TestCase(15f, 0.5f, 8)]  // 7.5 → 8 (짝수)
        [TestCase(21f, 0.5f, 10)] // 10.5 → 10 (짝수)
        [TestCase(8f, 0.92f, 7)]  // 7.36 → 7
        public void Threshold_RoundsHalfToEven(float baseNeed, float mult, int expected)
        {
            var gauge = new IngredientGauge(IngredientType.Cocktail, baseNeed, 1.4f, () => mult);
            Assert.AreEqual(expected, gauge.Threshold);
        }

        [Test]
        public void Threshold_NeverBelow3()
        {
            var gauge = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f, () => 0.1f);
            Assert.AreEqual(3, gauge.Threshold);
        }

        [Test]
        public void GaugeNeedMultiplier_FloorIs0_5_AndOnlyReducesDown()
        {
            var stats = new PlayerStats();
            Assert.AreEqual(1f, stats.GaugeNeedMultiplier, 0.0001f);

            stats.Apply(new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f));
            Assert.AreEqual(0.92f, stats.GaugeNeedMultiplier, 0.0001f);

            for (int i = 0; i < 20; i++) stats.Apply(new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f));
            Assert.AreEqual(0.5f, stats.GaugeNeedMultiplier, 0.0001f, "07:22 max(0.5, 1+gaugeNeedPercent)");
        }

        // ---- 07:53~59 카드 추첨 ----

        [Test]
        public void RealPool_HasExactly16Cocktail_18Tea_1Hybrid_WithUniqueIds()
        {
            var pool = CardDatabase.LoadAll();
            created.AddRange(pool.Where(c => !UnityEditor.EditorUtility.IsPersistent(c)));

            Assert.AreEqual(16, pool.Count(c => c.category == CardCategory.Cocktail), "07:53 칵테일16");
            Assert.AreEqual(18, pool.Count(c => c.category == CardCategory.Tea), "07:53 차18");
            Assert.AreEqual(1, pool.Count(c => c.category == CardCategory.Hybrid), "07:112 하이브리드1");
            Assert.AreEqual(pool.Count, pool.Select(c => c.id).Distinct().Count(), "ID 중복 없음");
        }

        [Test]
        public void Draw_NeverOffersTheHybridCard_FromCocktailOrTeaDraws()
        {
            var pool = CardDatabase.LoadAll();
            created.AddRange(pool.Where(c => !UnityEditor.EditorUtility.IsPersistent(c)));
            var system = new CardSystem(pool, new System.Random(11));

            for (int i = 0; i < 300; i++)
            {
                foreach (var category in new[] { CardCategory.Cocktail, CardCategory.Tea })
                    foreach (var offer in system.Draw(category, i % 2 == 0))
                        Assert.AreNotEqual(CardCategory.Hybrid, offer.card.category);
            }
        }

        [Test]
        public void Draw_OfferedRarityPercents_AreNormalizedOverRaritiesThatStillHaveCandidates()
        {
            // 07:56 — 후보가 남은 등급끼리만 60/30/10 정규화: 일반이 전부 소진되면 희귀 75% / 영웅 25%.
            var rare = MakeCard("r", CardRarity.Rare, 1);
            var epic = MakeCard("e", CardRarity.Epic, 1);
            var offers = new CardSystem(new[] { rare, epic }, new System.Random(3)).Draw(CardCategory.Cocktail, false);

            Assert.AreEqual(rare,offers[0].card,"Fixed seed draws rare while both tiers remain.");
            Assert.AreEqual(75f,offers[0].rarityPercent,.01f);
            Assert.AreEqual(epic,offers[1].card);
            Assert.AreEqual(100f,offers[1].rarityPercent,.01f,"34: the second slot has only epic left.");
        }

        [Test]
        public void Draw_NoEligibleCandidates_ReturnsEmpty_ForTheMaintenanceButton()
        {
            // 07:61 후보0 → "정비" 버튼 1개(호출 쪽 처리). 여기선 빈 목록이어야 그쪽 분기가 탄다.
            var only = MakeCard("only", CardRarity.Common, 1);
            var system = new CardSystem(new[] { only }, new System.Random(1));
            system.Take(only, new PlayerStats());

            Assert.AreEqual(0, system.Draw(CardCategory.Cocktail, false).Count);
        }

        [Test]
        public void Draw_Count1Or2_WhenOnlyThatManyCandidatesRemain()
        {
            var pool = new[] { MakeCard("a", CardRarity.Common, 1), MakeCard("b", CardRarity.Rare, 1) };
            Assert.AreEqual(2, new CardSystem(pool, new System.Random(5)).Draw(CardCategory.Cocktail, false).Count);
        }

        [Test]
        public void CountOwned_SumsStacks_NotDistinctIds_ForTheHybridCondition()
        {
            // 07:115 "서로 다른 ID3종 조건이 아니다" — 한 카드를 3번 먹어도 3으로 센다.
            var card = MakeCard("stacky", CardRarity.Common, 5);
            var system = new CardSystem(new[] { card }, new System.Random(2));
            var stats = new PlayerStats();

            system.Take(card, stats);
            system.Take(card, stats);
            system.Take(card, stats);

            Assert.AreEqual(3, system.CountOwned(CardCategory.Cocktail));
        }

        CardData MakeCard(string id, CardRarity rarity, int maxStacks)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.id = id;
            card.category = CardCategory.Cocktail;
            card.rarity = rarity;
            card.maxStacks = maxStacks;
            card.effects = System.Array.Empty<CardEffect>();
            created.Add(card);
            return card;
        }
    }
}
