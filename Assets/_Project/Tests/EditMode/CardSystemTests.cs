using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    public class CardSystemTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        List<CardData> DefaultPool()
        {
            var pool = DefaultCards.CreateRuntime();
            created.AddRange(pool);
            return pool;
        }

        CardData MakeCard(string id, CardCategory category, CardRarity rarity, int maxStacks,
            params CardEffect[] effects) => MakeCard(id, category, rarity, maxStacks, WeaponRestriction.Any, effects);

        CardData MakeCard(string id, CardCategory category, CardRarity rarity, int maxStacks,
            WeaponRestriction weaponRestriction, params CardEffect[] effects)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.id = id;
            card.category = category;
            card.rarity = rarity;
            card.maxStacks = maxStacks;
            card.effects = effects;
            card.weaponRestriction = weaponRestriction;
            created.Add(card);
            return card;
        }

        [Test]
        public void Draw_ReturnsThreeDistinctCardsOfRequestedCategory()
        {
            var system = new CardSystem(DefaultPool(), new System.Random(1));

            for (int i = 0; i < 50; i++)
            {
                var offers = system.Draw(CardCategory.Cocktail, false);
                Assert.AreEqual(3, offers.Count);
                Assert.AreEqual(3, offers.Select(o => o.card).Distinct().Count());
                Assert.IsTrue(offers.All(o => o.card.category == CardCategory.Cocktail));
            }
        }

        [Test]
        public void Draw_ExposesRarityPercent_ThatSumsOverAvailableRarities()
        {
            var system = new CardSystem(DefaultPool(), new System.Random(2));
            var offers = system.Draw(CardCategory.Cocktail, false);

            foreach (var offer in offers)
            {
                float expected = RarityTable.Weight(offer.card.rarity);
                Assert.AreEqual(expected, offer.rarityPercent, 0.01f); // 60/30/10 → 합 100
            }
        }

        [Test]
        public void Draw_OtherCategory_ReturnsNothing()
        {
            var system = new CardSystem(DefaultPool(), new System.Random(3));
            Assert.AreEqual(0, system.Draw(CardCategory.Tea, false).Count);
        }

        [Test]
        public void Draw_FewerCardsThanRequested_ReturnsWhatExists()
        {
            var pool = new List<CardData>
            {
                MakeCard("a", CardCategory.Cocktail, CardRarity.Common, 1),
                MakeCard("b", CardCategory.Cocktail, CardRarity.Epic, 1),
            };
            var offers = new CardSystem(pool, new System.Random(4)).Draw(CardCategory.Cocktail, false);

            Assert.AreEqual(2, offers.Count);
            // 남은 등급이 Common/Epic 뿐이면 60:10 → 85.7% / 14.3%
            var common = offers.Single(o => o.card.rarity == CardRarity.Common);
            Assert.AreEqual(60f / 70f * 100f, common.rarityPercent, 0.01f);
        }

        [Test]
        public void Take_AppliesEffectsToStats_AndTracksStacks()
        {
            var martini = MakeCard("martini", CardCategory.Cocktail, CardRarity.Epic, 3,
                new CardEffect(CardEffectType.DamagePercent, 0.25f),
                new CardEffect(CardEffectType.AttackSpeedPercent, 0.20f));
            var system = new CardSystem(new[] { martini }, new System.Random(5));
            var stats = new PlayerStats();

            system.Take(martini, stats);
            system.Take(martini, stats);

            Assert.AreEqual(2, system.GetStacks(martini));
            Assert.AreEqual(0.5f, stats.damagePercent, 0.0001f);
            Assert.AreEqual(0.4f, stats.attackSpeedPercent, 0.0001f);
            Assert.AreEqual(stats.baseDamage * 1.5f, stats.Damage, 0.0001f);
        }

        [Test]
        public void MaxedCards_AreExcludedFromDraw()
        {
            var once = MakeCard("once", CardCategory.Cocktail, CardRarity.Common, 1);
            var other = MakeCard("other", CardCategory.Cocktail, CardRarity.Common, 5);
            var system = new CardSystem(new[] { once, other }, new System.Random(6));

            system.Take(once, new PlayerStats());
            var offers = system.Draw(CardCategory.Cocktail, false);

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(other, offers[0].card);
        }

        [Test]
        public void GaugeNeedMultiplier_HasFloor()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.GaugeNeedPercent, -0.9f));
            Assert.AreEqual(0.5f, stats.GaugeNeedMultiplier, 0.0001f);
        }

        [Test]
        public void Draw_ExcludesMeleeOnlyCards_ForRangedWeapon()
        {
            var melee = MakeCard("melee_only", CardCategory.Cocktail, CardRarity.Common, 1,
                WeaponRestriction.MeleeOnly);
            var any = MakeCard("any", CardCategory.Cocktail, CardRarity.Common, 1, WeaponRestriction.Any);
            var system = new CardSystem(new[] { melee, any }, new System.Random(7));

            var offers = system.Draw(CardCategory.Cocktail, true); // 원거리

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(any, offers[0].card);
        }

        [Test]
        public void Draw_ExcludesRangedOnlyCards_ForMeleeWeapon()
        {
            var ranged = MakeCard("ranged_only", CardCategory.Cocktail, CardRarity.Common, 1,
                WeaponRestriction.RangedOnly);
            var any = MakeCard("any2", CardCategory.Cocktail, CardRarity.Common, 1, WeaponRestriction.Any);
            var system = new CardSystem(new[] { ranged, any }, new System.Random(8));

            var offers = system.Draw(CardCategory.Cocktail, false); // 근접

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(any, offers[0].card);
        }

        [Test]
        public void FlameAndThrust_AreCountedAsIntegers()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.FlameLevel, 2f));
            stats.Apply(new CardEffect(CardEffectType.ExtraThrust, 1f));
            Assert.AreEqual(2, stats.flameLevel);
            Assert.AreEqual(1, stats.extraThrusts);
        }
    }
}
