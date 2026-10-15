using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>49 카드 추천: 명세 표의 상태별 합격/회피 안내, 결정성, 혼합 러시 고유 종류 규칙.</summary>
    public class CardAdvisorTests
    {
        sealed class FakeState : IHiddenRunState
        {
            public readonly Dictionary<string, int> cards = new Dictionary<string, int>();
            public readonly HashSet<string> coffee = new HashSet<string>();
            public Job job = Job.Bartender;
            public int Stacks(string id) => cards.TryGetValue(id, out var n) ? n : 0;
            public bool Coffee(string id) => coffee.Contains(id);
            public bool Hybrid => false;
            public int FinalFormCount => 0;
            public bool HasFinalForm(AttackFinalForm f) => false;
            public Job Job => job;
        }

        readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in made) Object.DestroyImmediate(o);
            made.Clear();
        }

        CardData Card(string id, CardCategory cat, int max = 5, WeaponRestriction r = WeaponRestriction.Any, params CardEffect[] effects)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id; c.displayName = id; c.category = cat; c.maxStacks = max; c.weaponRestriction = r;
            c.effects = effects.Length > 0 ? effects : new[] { new CardEffect(CardEffectType.DamagePercent, 0.1f) };
            made.Add(c);
            return c;
        }

        static CardAdviceContext Ctx(FakeState s, Job job, WeaponKind w)
        {
            s.job = job;
            int kinds = 0, cocktail = 0, tea = 0, highestTea = 0;
            foreach (var kv in s.cards)
            {
                var data = CardDatabase.LoadAll().Find(c => c.id == kv.Key);
                if (data == null || kv.Value <= 0) continue;
                if (data.category == CardCategory.Cocktail) { kinds++; cocktail += kv.Value; }
                else if (data.category == CardCategory.Tea) { tea += kv.Value; highestTea = Mathf.Max(highestTea, kv.Value); }
            }
            return new CardAdviceContext
            {
                job = job, weapon = w, run = s, hp = 100f, maxHp = 100f,
                cocktailKinds = kinds, cocktailStacks = cocktail, teaStacks = tea,
                deepSteepLevel = Mathf.Clamp(highestTea - 1, 0, 6),
            };
        }

        static List<CardOffer> Offers(params CardData[] cards)
        {
            var list = new List<CardOffer>();
            foreach (var c in cards) list.Add(new CardOffer { card = c });
            return list;
        }

        [Test]
        public void Bartender_TwoKinds_NewKindTriggersRush_RepeatDoesNot()
        {
            var s = new FakeState();
            s.cards["dry_gin"] = 1; s.cards["shot_glass"] = 1;
            var ctx = Ctx(s, Job.Bartender, WeaponKind.Gun);
            Assert.AreEqual(2, ctx.cocktailKinds);
            var fresh = Card("bar_shoes", CardCategory.Cocktail);
            var repeat = Card("dry_gin", CardCategory.Cocktail);
            var advice = CardAdvisor.Evaluate(Offers(repeat, fresh), ctx);
            StringAssert.Contains("혼합 러시 발동", advice[1].reason);
            Assert.IsTrue(advice[1].recommended);
            StringAssert.DoesNotContain("러시", advice[0].reason, "이미 보유한 카드의 추가 중첩은 새 종류가 아님");
            Assert.IsFalse(advice[0].recommended);
        }

        [Test]
        public void MixingRush_CountsKindsNotStacks()
        {
            Assert.IsTrue(CardAdvisor.MixingRushTriggers(2, 3));
            Assert.IsTrue(CardAdvisor.MixingRushTriggers(5, 6));
            Assert.IsFalse(CardAdvisor.MixingRushTriggers(3, 3), "종류 수가 그대로면(추가 중첩) 다시 발동하지 않음");
            Assert.IsFalse(CardAdvisor.MixingRushTriggers(3, 4));
            var sys = new CardSystem(new List<CardData>());
            var a = Card("dry_gin", CardCategory.Cocktail);
            var stats = new PlayerStats();
            sys.Take(a, stats); sys.Take(a, stats); sys.Take(a, stats);
            Assert.AreEqual(1, sys.CountKinds(CardCategory.Cocktail));
            Assert.AreEqual(3, sys.CountOwned(CardCategory.Cocktail));
        }

        [Test]
        public void TeaSommelier_FocusedTeaRaisesDeepSteep_OtherTeaFirstCardDoesNot()
        {
            var s = new FakeState();
            s.cards["oolong"] = 3; // 깊은 우림 2
            var ctx = Ctx(s, Job.TeaSommelier, WeaponKind.Sword);
            Assert.AreEqual(2, ctx.deepSteepLevel);
            var same = Card("oolong", CardCategory.Tea);
            var other = Card("brew_rhythm", CardCategory.Tea);
            var advice = CardAdvisor.Evaluate(Offers(same, other), ctx);
            StringAssert.Contains("깊은 우림 2→3", advice[0].reason);
            StringAssert.DoesNotContain("깊은 우림", advice[1].reason, "다른 차 첫 장은 집중 중첩이 아님");
        }

        [Test]
        public void DeepSteep_AtCapSix_NoFurtherIncreaseReason()
        {
            var s = new FakeState();
            s.cards["oolong"] = 7;
            var ctx = Ctx(s, Job.TeaSommelier, WeaponKind.Sword);
            Assert.AreEqual(6, ctx.deepSteepLevel);
            var advice = CardAdvisor.Advise(Card("oolong", CardCategory.Tea, 10), ctx);
            StringAssert.DoesNotContain("깊은 우림", advice.reason);
        }

        [Test]
        public void RangedWeapon_MeleeEffect_NotRecommended()
        {
            var s = new FakeState();
            var ctx = Ctx(s, Job.Bartender, WeaponKind.Gun);
            var thrust = Card("test_thrust", CardCategory.Tea, 5, WeaponRestriction.Any, new CardEffect(CardEffectType.ExtraThrust, 1f));
            var advice = CardAdvisor.Advise(thrust, ctx);
            Assert.Less(advice.score, CardAdvisor.MinRecommendScore);
            var melee = CardAdvisor.Advise(Card("test_melee", CardCategory.Tea, 5, WeaponRestriction.MeleeOnly), Ctx(new FakeState(), Job.TeaSommelier, WeaponKind.Gun));
            StringAssert.DoesNotContain("전용", melee.reason);
            Assert.AreEqual(0, melee.score);

            // 다른 강한 근거(혼합 러시 발동)가 있어도 무기 비호환 후보는 통째로 추천 제외.
            var s2 = new FakeState();
            s2.cards["dry_gin"] = 1; s2.cards["shot_glass"] = 1;
            var rushThrust = Card("test_rush_thrust", CardCategory.Cocktail, 5, WeaponRestriction.Any, new CardEffect(CardEffectType.ExtraThrust, 1f));
            var gunAdvice = CardAdvisor.Evaluate(Offers(rushThrust), Ctx(s2, Job.Bartender, WeaponKind.Gun));
            Assert.AreEqual(0, gunAdvice[0].score);
            Assert.IsFalse(gunAdvice[0].recommended);
            var rapierCtx = Ctx(s2, Job.Bartender, WeaponKind.Rapier);
            StringAssert.Contains("혼합 러시 발동", CardAdvisor.Advise(rushThrust, rapierCtx).reason, "근접 무기에는 적용");
            rapierCtx.extraThrusts = PlayerStats.MaxExtraThrusts;
            Assert.AreEqual(0, CardAdvisor.Advise(rushThrust, rapierCtx).score, "연속타 상한이면 제외");
        }

        [Test]
        public void MaxStacks_NoProgressReason()
        {
            var s = new FakeState();
            s.cards["green_tea"] = 3;
            var ctx = Ctx(s, Job.Bartender, WeaponKind.Rapier);
            var advice = CardAdvisor.Advise(Card("green_tea", CardCategory.Tea, 3), ctx);
            Assert.AreEqual(0, advice.score);
            Assert.AreEqual("", advice.reason);
        }

        [Test]
        public void LowHp_RealHealCardRecommended_NonHealCardNot()
        {
            var s = new FakeState();
            var ctx = Ctx(s, Job.TeaSommelier, WeaponKind.Bow);
            ctx.hp = 3f; // 레벨업 회복 30% 뒤에도 33%
            var regen = Card("test_regen", CardCategory.Tea, 5, WeaponRestriction.Any, new CardEffect(CardEffectType.RegenBonus, 0.5f));
            var dmg = Card("test_dmg", CardCategory.Tea);
            var advice = CardAdvisor.Evaluate(Offers(dmg, regen), ctx);
            StringAssert.Contains("체력 3%", advice[1].reason);
            Assert.IsTrue(advice[1].recommended);
            StringAssert.DoesNotContain("체력", advice[0].reason);

            ctx.hp = 80f;
            Assert.AreEqual("", CardAdvisor.Advise(regen, ctx).reason, "체력이 충분하면 회복 이유 없음");
            ctx.hp = 3f; ctx.damageReduction = 0.25f;
            var guard = Card("test_guard", CardCategory.Tea, 5, WeaponRestriction.Any, new CardEffect(CardEffectType.DamageReduction, 0.05f));
            Assert.AreEqual("", CardAdvisor.Advise(guard, ctx).reason, "피해 감소 상한이면 이유 없음");
        }

        [Test]
        public void Hidden_CompletionOnlyWhenOtherPartsMet_UndiscoveredNameHidden()
        {
            var s = new FakeState();
            s.cards["dry_gin"] = 2; s.cards["green_tea"] = 1; // 첫 잔의 향: 드라이 진 2 + 녹차 잎 2
            var ctx = Ctx(s, Job.Bartender, WeaponKind.Gun);
            var tea = Card("green_tea", CardCategory.Tea);
            var a = CardAdvisor.Advise(tea, ctx);
            StringAssert.Contains("숨은 조합 조건 완성", a.reason);
            Assert.AreEqual(100, a.score);
            ctx.hiddenDiscovered = id => id == "hidden_first_sip";
            StringAssert.Contains("히든 '첫 잔의 향' 조건 완성", CardAdvisor.Advise(tea, ctx).reason);

            s.cards.Remove("dry_gin"); s.cards["green_tea"] = 0;
            var partial = CardAdvisor.Advise(tea, Ctx(s, Job.Bartender, WeaponKind.Gun));
            StringAssert.DoesNotContain("완성", partial.reason, "드라이 진이 없으면 완성/확정이라고 하지 않음");
            StringAssert.Contains("재료", partial.reason);

            var owned = Ctx(new FakeState { cards = { ["dry_gin"] = 2, ["green_tea"] = 1 } }, Job.Bartender, WeaponKind.Gun);
            owned.hiddenOwned = h => h == HiddenSkill.FirstSip;
            StringAssert.DoesNotContain("첫 잔", CardAdvisor.Advise(tea, owned).reason);
        }

        [Test]
        public void SameStateTwice_SameAdvice_NoRandomness()
        {
            var s = new FakeState();
            s.cards["dry_gin"] = 1; s.cards["shot_glass"] = 1;
            var ctx = Ctx(s, Job.Bartender, WeaponKind.Rapier);
            var offers = Offers(Card("bar_shoes", CardCategory.Cocktail), Card("green_tea", CardCategory.Tea), Card("dry_gin", CardCategory.Cocktail));
            var a = CardAdvisor.Evaluate(offers, ctx);
            var b = CardAdvisor.Evaluate(offers, ctx);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].reason, b[i].reason);
                Assert.AreEqual(a[i].recommended, b[i].recommended);
            }
        }

        [Test]
        public void AllOffersEqual_NoBadge_ReasonsKept()
        {
            var s = new FakeState();
            s.cards["dry_gin"] = 1; s.cards["shot_glass"] = 1;
            var ctx = Ctx(s, Job.Bartender, WeaponKind.Gun);
            var advice = CardAdvisor.Evaluate(Offers(Card("test_a", CardCategory.Cocktail), Card("test_b", CardCategory.Cocktail)), ctx);
            Assert.IsFalse(advice[0].recommended || advice[1].recommended, "모두 같으면 추천 표식 없음");
            StringAssert.Contains("혼합 러시 발동", advice[0].reason);
        }

        [Test]
        public void NoContext_NoRecommendation()
        {
            var advice = CardAdvisor.Evaluate(Offers(Card("dry_gin", CardCategory.Cocktail)), null);
            Assert.IsFalse(advice[0].recommended);
            Assert.AreEqual("", advice[0].reason);
        }
    }
}
