using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>10 G14 "기본카드 전34행+asset DB: ID/등급/중첩/효과/제한이 07과 완전일치". 07 §3 표를 그대로 옮겨
    /// 실제 Resources asset DB(CardDatabase.LoadAll)와 런타임 기본값(Default*Cards.CreateRuntime) 양쪽을 행 단위로 대조한다.</summary>
    public class Spec07CardTableTests
    {
        const CardRarity C = CardRarity.Common, R = CardRarity.Rare, E = CardRarity.Epic;
        const WeaponRestriction Any = WeaponRestriction.Any, Melee = WeaponRestriction.MeleeOnly, Ranged = WeaponRestriction.RangedOnly;

        struct Row
        {
            public string id; public CardCategory cat; public CardRarity rarity; public int max; public WeaponRestriction restriction;
            public (CardEffectType type, float value)[] effects;
        }

        static Row K(string id, CardRarity r, int max, WeaponRestriction w, params (CardEffectType, float)[] fx) =>
            new Row { id = id, cat = CardCategory.Cocktail, rarity = r, max = max, restriction = w, effects = fx };
        static Row T(string id, CardRarity r, int max, WeaponRestriction w, params (CardEffectType, float)[] fx) =>
            new Row { id = id, cat = CardCategory.Tea, rarity = r, max = max, restriction = w, effects = fx };

        const CardEffectType Dmg = CardEffectType.DamagePercent, Aspd = CardEffectType.AttackSpeedPercent,
            Rng = CardEffectType.RangePercent, Move = CardEffectType.MoveSpeedPercent, Hp = CardEffectType.MaxHpFlat,
            Need = CardEffectType.GaugeNeedPercent, Drop = CardEffectType.DropChancePercent, Thrust = CardEffectType.ExtraThrust,
            Flame = CardEffectType.FlameLevel, Poison = CardEffectType.PoisonLevel, Regen = CardEffectType.RegenBonus,
            Pierce = CardEffectType.PierceCount;

        // 07 §3 칵테일16 + 차18 (획득 1회당 증가량).
        static readonly Row[] Spec =
        {
            K("dry_gin", C, 5, Any, (Dmg, 0.15f)),
            K("shot_glass", C, 5, Any, (Aspd, 0.12f)),
            K("long_stirrer", C, 5, Any, (Rng, 0.15f)),
            K("bar_shoes", C, 5, Any, (Move, 0.10f)),
            K("ice_cube", C, 5, Any, (Hp, 20f)),
            K("liqueur", C, 5, Any, (Need, -0.08f)),
            K("syrup", C, 5, Any, (Drop, 0.10f)),
            K("double_shot", R, 4, Any, (Dmg, 0.30f)),
            K("mixing_tempo", R, 4, Any, (Aspd, 0.25f)),
            K("flambe", R, 3, Melee, (Thrust, 1f)),
            K("champagne_spray", R, 3, Ranged, (Thrust, 1f)),
            K("molotov", R, 5, Any, (Flame, 1f)),
            K("scotch_rocks", E, 3, Any, (Dmg, 0.50f)),
            K("dry_martini", E, 3, Any, (Dmg, 0.25f), (Aspd, 0.20f)),
            K("blazer", E, 3, Any, (Flame, 2f)),
            K("rifled_barrel", E, 3, Ranged, (Pierce, 1f)),

            T("green_tea", C, 5, Any, (Dmg, 0.15f)),
            T("tea_whisk", C, 5, Any, (Aspd, 0.12f)),
            T("long_spout", C, 5, Any, (Rng, 0.15f)),
            T("tea_sandals", C, 5, Any, (Move, 0.10f)),
            T("warm_cup", C, 5, Any, (Hp, 20f)),
            T("honey", C, 5, Any, (Need, -0.08f)),
            T("dried_leaf", C, 5, Any, (Drop, 0.10f)),
            T("oolong", R, 4, Any, (Dmg, 0.30f)),
            T("brew_rhythm", R, 4, Any, (Aspd, 0.25f)),
            T("second_infusion", R, 3, Melee, (Thrust, 1f)),
            T("tea_leaf_scatter", R, 3, Ranged, (Thrust, 1f)),
            T("chamomile", R, 5, Any, (Regen, 1.2f)),
            T("nettle_tea", R, 5, Any, (Poison, 1f)),
            T("aged_puerh", E, 3, Any, (Dmg, 0.50f)),
            T("golden_ratio", E, 3, Any, (Dmg, 0.25f), (Aspd, 0.20f)),
            T("ginseng_root", E, 3, Any, (Regen, 3f)),
            T("nightshade_blend", E, 3, Any, (Poison, 2f)),
            T("piercing_arrowhead", E, 3, Ranged, (Pierce, 1f)),
        };

        static void AssertMatchesSpec(IEnumerable<CardData> source, string label)
        {
            var basic = source.Where(c => c != null && c.category != CardCategory.Hybrid).ToList();
            Assert.AreEqual(34, basic.Count, $"{label}: 기본 카드 34장");
            Assert.AreEqual(34, basic.Select(c => c.id).Distinct().Count(), $"{label}: ID 중복 없음");
            Assert.AreEqual(16, Spec.Count(r => r.cat == CardCategory.Cocktail));
            Assert.AreEqual(18, Spec.Count(r => r.cat == CardCategory.Tea));

            var byId = basic.ToDictionary(c => c.id);
            foreach (var row in Spec)
            {
                Assert.IsTrue(byId.TryGetValue(row.id, out var card), $"{label}: {row.id} 없음");
                Assert.AreEqual(row.cat, card.category, $"{label}: {row.id} 카테고리");
                Assert.AreEqual(row.rarity, card.rarity, $"{label}: {row.id} 등급");
                Assert.AreEqual(row.max, card.maxStacks, $"{label}: {row.id} 최대중첩");
                Assert.AreEqual(row.restriction, card.weaponRestriction, $"{label}: {row.id} 무기 제한");
                Assert.IsNotNull(card.effects, $"{label}: {row.id} 효과");
                Assert.AreEqual(row.effects.Length, card.effects.Length, $"{label}: {row.id} 효과 수");
                foreach (var (type, value) in row.effects)
                {
                    var match = card.effects.Where(e => e.type == type).ToList();
                    Assert.AreEqual(1, match.Count, $"{label}: {row.id} 효과 {type} 1개");
                    Assert.AreEqual(value, match[0].value, 1e-5f, $"{label}: {row.id} {type} 값");
                }
                Assert.IsFalse(string.IsNullOrEmpty(card.displayName), $"{label}: {row.id} 표시명");
            }
        }

        [Test]
        public void AssetDatabase_BasicCards_MatchSpec07Table_RowByRow()
        {
            AssertMatchesSpec(CardDatabase.LoadAll(), "asset DB");
        }

        [Test]
        public void RuntimeDefaults_BasicCards_MatchSpec07Table_RowByRow()
        {
            var runtime = DefaultCards.CreateRuntime();
            runtime.AddRange(DefaultTeaCards.CreateRuntime());
            AssertMatchesSpec(runtime, "runtime defaults");
        }

        [Test]
        public void Hybrid_IsSingleSpecialMaxOne()
        {
            var hybrids = CardDatabase.LoadAll().Where(c => c != null && c.category == CardCategory.Hybrid).ToList();
            Assert.AreEqual(1, hybrids.Count);
            Assert.AreEqual("hybrid_tea_martini", hybrids[0].id);
            Assert.AreEqual(CardRarity.Special, hybrids[0].rarity);
            Assert.AreEqual(1, hybrids[0].maxStacks);
        }
    }
}
