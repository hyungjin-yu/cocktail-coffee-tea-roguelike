using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-24 #21 기본 활 전용 속성 화살(화상·빙결·감전·중독): 카드 범위·순환·상한·문구=규칙·저장 호환.</summary>
    public class ArrowElementTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        [TearDown] public void TearDown() { foreach (var o in created) if (o != null && !UnityEditor.EditorUtility.IsPersistent(o)) UnityEngine.Object.DestroyImmediate(o); created.Clear(); }

        List<CardData> Pool() { var p = CardDatabase.LoadAll(); created.AddRange(p); return p; }

        [Test]
        public void EnumValues_AppendedOnly_SaveCompatible()
        {
            Assert.AreEqual(14, (int)CardEffectType.PickupRadiusFlat, "기존 마지막 값 불변");
            Assert.AreEqual(15, (int)CardEffectType.ArrowBurn);
            Assert.AreEqual(18, (int)CardEffectType.ArrowPoison);
            Assert.AreEqual(2, (int)WeaponRestriction.RangedOnly);
            Assert.AreEqual(3, (int)WeaponRestriction.BowOnly);
        }

        [Test]
        public void FourArrowCards_TeaRareMax3_BowOnly_OneEffectEach()
        {
            var pool = Pool();
            var arrows = pool.Where(c => c.weaponRestriction == WeaponRestriction.BowOnly).ToList();
            CollectionAssert.AreEquivalent(DefaultArrowCards.Ids, arrows.Select(c => c.id));
            Assert.AreEqual(pool.Count, pool.Select(c => c.id).Distinct().Count(), "ID 중복 없음");
            foreach (var c in arrows)
            {
                Assert.AreEqual(CardCategory.Tea, c.category, c.id + " 찻집에서도 열리는 차 카드 창");
                Assert.AreEqual(CardRarity.Rare, c.rarity);
                Assert.AreEqual(ArrowElementRules.MaxLevel, c.maxStacks);
                Assert.AreEqual(1, c.effects.Length);
                Assert.IsTrue(DefaultArrowCards.IsArrowEffect(c.effects[0].type));
                Assert.AreEqual(1f, c.effects[0].value);
                Assert.IsTrue(Uri.TryCreate(c.sourceUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https", c.id);
                Assert.LessOrEqual(CardEducationSummary.Fact(c).Length, 65, c.id);
                Assert.LessOrEqual(CardEducationSummary.Connection(c).Length, 32, c.id);
                StringAssert.DoesNotContain("실제", CardEducationData.Detail(c));
                StringAssert.Contains("번갈아", c.description, "순환 규칙을 설명");
                Assert.LessOrEqual(CardEducationSummary.Effect(c).Length, 22, c.id + " 2줄 효과 칸");
            }
        }

        [Test]
        public void Scope_OnlyBowStartingWeapon_NeverGunMeleeOrHybrid()
        {
            var bow = Pool().First(c => c.id == "arrow_burn");
            Assert.IsTrue(CardSystem.IsWeaponCompatible(bow, true, true), "기본 활");
            Assert.IsFalse(CardSystem.IsWeaponCompatible(bow, true, false), "총");
            Assert.IsFalse(CardSystem.IsWeaponCompatible(bow, false, false), "레이피어·검(보조 활 포함)");
            var ranged = Pool().First(c => c.id == "piercing_arrowhead");
            Assert.IsTrue(CardSystem.IsWeaponCompatible(ranged, true, true), "활도 기존 원거리 카드는 그대로");

            foreach (var (ranged2, isBow, expect) in new[] { (true, true, true), (true, false, false), (false, false, false) })
            {
                var system = new CardSystem(Pool(), new System.Random(5));
                bool seen = false;
                for (int i = 0; i < 400; i++)
                    foreach (var o in system.Draw(CardCategory.Tea, ranged2, 3, 999f, 99, isBow))
                        if (o.card.weaponRestriction == WeaponRestriction.BowOnly) seen = true;
                Assert.AreEqual(expect, seen, $"ranged={ranged2} bow={isBow}");
                foreach (var o in system.Draw(CardCategory.Cocktail, ranged2, 3, 999f, 99, isBow))
                    Assert.AreNotEqual(WeaponRestriction.BowOnly, o.card.weaponRestriction, "칵테일 창에는 없음");
            }
        }

        [Test]
        public void Advisor_AppliesOnlyToBow()
        {
            var card = Pool().First(c => c.id == "arrow_shock");
            Assert.IsTrue(CardAdvisor.WeaponApplies(card, new CardAdviceContext { weapon = WeaponKind.Bow }));
            Assert.IsFalse(CardAdvisor.WeaponApplies(card, new CardAdviceContext { weapon = WeaponKind.Gun }));
            Assert.IsFalse(CardAdvisor.WeaponApplies(card, new CardAdviceContext { weapon = WeaponKind.Rapier }));
        }

        [Test]
        public void Stats_LevelsCapAtMax()
        {
            var s = new PlayerStats();
            for (int i = 0; i < 5; i++) s.Apply(new CardEffect(CardEffectType.ArrowPoison, 1f));
            Assert.AreEqual(ArrowElementRules.MaxLevel, s.arrowPoisonLevel);
            Assert.AreEqual(0, s.arrowBurnLevel);
        }

        [Test]
        public void Rotation_FixedBurnFreezeShockPoison_SkipsUnowned_NoneWhenEmpty()
        {
            var s = new PlayerStats();
            Assert.AreEqual(ArrowElementKind.None, s.ConsumeArrowElement());
            Assert.AreEqual(ArrowElementKind.None, s.PeekArrowElement(0));
            s.Apply(new CardEffect(CardEffectType.ArrowShock, 1f));
            for (int i = 0; i < 4; i++) Assert.AreEqual(ArrowElementKind.Shock, s.ConsumeArrowElement(), "하나면 매 화살");
            // 얻은 순서(감전 → 중독 → 화상)와 무관하게 고정 순서. 빙결은 없어서 건너뜀.
            s.Apply(new CardEffect(CardEffectType.ArrowPoison, 1f));
            s.Apply(new CardEffect(CardEffectType.ArrowBurn, 1f));
            s.Apply(new CardEffect(CardEffectType.ArrowBurn, 1f)); // 중첩은 레벨만
            var seq = new List<ArrowElementKind>();
            for (int i = 0; i < 7; i++) seq.Add(s.ConsumeArrowElement());
            // 마지막 소비가 감전이었으므로 다음은 중독부터.
            CollectionAssert.AreEqual(new[] { ArrowElementKind.Poison, ArrowElementKind.Burn, ArrowElementKind.Shock, ArrowElementKind.Poison,
                ArrowElementKind.Burn, ArrowElementKind.Shock, ArrowElementKind.Poison }, seq, "화상→빙결(없음)→감전→중독→화상");
            Assert.AreEqual(2, s.arrowBurnLevel);
        }

        [Test]
        public void Preview_PeekMatchesActualConsumeOrder_IncludingMultiShotVolley()
        {
            var s = new PlayerStats();
            foreach (var t in new[] { CardEffectType.ArrowFreeze, CardEffectType.ArrowBurn, CardEffectType.ArrowShock, CardEffectType.ArrowPoison })
                s.Apply(new CardEffect(t, 1f));
            s.extraThrusts = 2; // 한 번에 3발
            for (int volley = 0; volley < 5; volley++)
            {
                var peek = Enumerable.Range(0, 1 + s.extraThrusts).Select(i => s.PeekArrowElement(i)).ToList();
                var fired = Enumerable.Range(0, 1 + s.extraThrusts).Select(_ => s.ConsumeArrowElement()).ToList();
                CollectionAssert.AreEqual(peek, fired, "머리 위 아이콘 i번 = 실제 i번째 spawn");
            }
            var a = s.PeekArrowElement(0); Assert.AreEqual(a, s.PeekArrowElement(0), "미리보기는 소비하지 않음");
        }

        [Test]
        public void Numbers_MatchDescriptions_NoDecimals()
        {
            var dec = new Regex(@"\d[.,]\d");
            foreach (ArrowElementKind k in Enum.GetValues(typeof(ArrowElementKind)))
            {
                if (k == ArrowElementKind.None) continue;
                Assert.IsFalse(dec.IsMatch(ArrowElementRules.Description(k)), k + " 설명 정수");
                Assert.IsFalse(dec.IsMatch(ArrowElementRules.ShortEffect(k)), k + " 짧은 글 정수");
            }
            StringAssert.Contains("2초", ArrowElementRules.Description(ArrowElementKind.Burn));
            StringAssert.Contains("30%", ArrowElementRules.Description(ArrowElementKind.Burn));
            Assert.AreEqual(0.5f, ArrowElementRules.BurnDpsFraction(3), 1e-5f);
            StringAssert.Contains("4초", ArrowElementRules.Description(ArrowElementKind.Freeze));
            Assert.AreEqual(2f, ArrowElementRules.FreezeCooldown(3), 1e-5f);
            StringAssert.Contains("+10%", ArrowElementRules.Description(ArrowElementKind.Freeze));
            StringAssert.Contains("250cm", ArrowElementRules.Description(ArrowElementKind.Shock));
            StringAssert.Contains("40%", ArrowElementRules.Description(ArrowElementKind.Shock));
            Assert.AreEqual(3, ArrowElementRules.ShockJumps(9), "연쇄 상한");
            StringAssert.Contains("5중첩", ArrowElementRules.Description(ArrowElementKind.Poison));
            StringAssert.Contains("6초", ArrowElementRules.Description(ArrowElementKind.Poison));
            StringAssert.Contains("5%", ArrowElementRules.Description(ArrowElementKind.Poison));
            Assert.AreEqual(0.09f, ArrowElementRules.PoisonStackDpsFraction(3), 1e-5f);
        }
    }
}
