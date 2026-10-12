using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>41 히든 스킬 24종: 목록·조건·도달 가능성·런당 1회.</summary>
    public class HiddenSkillTests
    {
        sealed class FakeState : IHiddenRunState
        {
            public readonly Dictionary<string, int> cards = new Dictionary<string, int>();
            public readonly HashSet<string> coffee = new HashSet<string>();
            public readonly HashSet<AttackFinalForm> forms = new HashSet<AttackFinalForm>();
            public bool hybrid;
            public int Stacks(string id) => cards.TryGetValue(id, out var n) ? n : 0;
            public bool Coffee(string id) => coffee.Contains(id);
            public bool Hybrid => hybrid;
            public int FinalFormCount => forms.Count;
            public bool HasFinalForm(AttackFinalForm f) => forms.Contains(f);
            public Job Job => Job.Bartender; // 47: 이 표는 바텐더 조건
        }

        // 문서 41 표 그대로: 카드 id→중첩, 커피 노드, 하이브리드, 최종 형태.
        static readonly Dictionary<HiddenSkill, (Dictionary<string, int> cards, string[] coffee, bool hybrid, AttackFinalForm[] forms)> Req =
            new Dictionary<HiddenSkill, (Dictionary<string, int>, string[], bool, AttackFinalForm[])>
            {
                [HiddenSkill.FirstSip] = (C(("dry_gin", 2), ("green_tea", 2)), N(), false, F()),
                [HiddenSkill.HotToddy] = (C(("molotov", 1), ("chamomile", 1)), N(), false, F()),
                [HiddenSkill.BitterBlend] = (C(("molotov", 1), ("nettle_tea", 1)), N(), false, F()),
                [HiddenSkill.IcedCup] = (C(("ice_cube", 3), ("warm_cup", 3)), N(), false, F()),
                [HiddenSkill.WeekendRush] = (C(("bar_shoes", 2), ("tea_sandals", 2)), N(), false, F()),
                [HiddenSkill.ShakeRhythm] = (C(("shot_glass", 3), ("tea_whisk", 3)), N(), false, F()),
                [HiddenSkill.SodaPump] = (C(("syrup", 2)), N("cold_brew"), false, F()),
                [HiddenSkill.CalmService] = (C(("chamomile", 2)), N("doppio_body"), false, F()),
                [HiddenSkill.LongReach] = (C(("long_stirrer", 2), ("long_spout", 2)), N(), false, F()),
                [HiddenSkill.DoubleFinish] = (C(("flambe", 2), ("second_infusion", 2)), N(), false, F()),
                [HiddenSkill.PointBlank] = (C(("champagne_spray", 1), ("tea_leaf_scatter", 1)), N(), false, F()),
                [HiddenSkill.BitterWake] = (C(("oolong", 1)), N("espresso_shot"), false, F()),
                [HiddenSkill.VipService] = (C(("scotch_rocks", 1), ("aged_puerh", 1)), N("full_body_roast"), false, F()),
                [HiddenSkill.HerbGarden] = (C(("chamomile", 2), ("ginseng_root", 1)), N("mellow_finish"), false, F()),
                [HiddenSkill.PoisonGarden] = (C(("nettle_tea", 2), ("nightshade_blend", 1)), N("ristretto"), false, F()),
                [HiddenSkill.FireShow] = (C(("molotov", 2), ("blazer", 1)), N("espresso_shot"), false, F()),
                [HiddenSkill.BeanCrate] = (C(("syrup", 2), ("dried_leaf", 2)), N("fine_grind"), false, F()),
                [HiddenSkill.ThickMug] = (C(("ice_cube", 2), ("warm_cup", 2)), N("insulated_cup"), false, F()),
                [HiddenSkill.TrayGuard] = (C(("ice_cube", 1), ("warm_cup", 1)), N("wide_tray"), false, F()),
                [HiddenSkill.LongSteep] = (C(("molotov", 1), ("nettle_tea", 1)), N("balanced_extraction"), false, F()),
                [HiddenSkill.PerfectCup] = (C(), N("perfect_extraction"), false, F(AttackFinalForm.Pierce)),
                [HiddenSkill.TiniMaster] = (C(), N(), true, F(AttackFinalForm.Chain)),
                [HiddenSkill.TriRoast] = (C(), N("balanced_extraction", "mellow_finish", "wide_tray"), false, F()),
                [HiddenSkill.BarTempo] = (C(("mixing_tempo", 4), ("brew_rhythm", 4)), N("ristretto"), false, F()),
            };
        static Dictionary<string, int> C(params (string, int)[] x) => x.ToDictionary(p => p.Item1, p => p.Item2);
        static string[] N(params string[] x) => x;
        static AttackFinalForm[] F(params AttackFinalForm[] x) => x;

        static FakeState StateFor(HiddenSkill s)
        {
            var r = Req[s]; var st = new FakeState { hybrid = r.hybrid };
            foreach (var kv in r.cards) st.cards[kv.Key] = kv.Value;
            foreach (var n in r.coffee) st.coffee.Add(n);
            foreach (var f in r.forms) st.forms.Add(f);
            return st;
        }

        [Test]
        public void Database_24_UniqueIds_Tiers12_8_4_EnumOrder()
        {
            var all = HiddenSkillDatabase.All;
            Assert.AreEqual(24, all.Count);
            Assert.AreEqual(24, all.Select(d => d.id).Distinct().Count());
            Assert.AreEqual(24, all.Select(d => d.name).Distinct().Count());
            Assert.AreEqual(24, all.Select(d => d.effect).Distinct().Count(), "이름만 다른 같은 효과 없음");
            Assert.IsTrue(all.All(d => d.id.StartsWith(HiddenSkillDatabase.IdPrefix)));
            Assert.AreEqual(12, all.Count(d => d.tier == HiddenTier.Two));
            Assert.AreEqual(8, all.Count(d => d.tier == HiddenTier.Three));
            Assert.AreEqual(4, all.Count(d => d.tier == HiddenTier.Hard));
            for (int i = 0; i < all.Count; i++) Assert.AreEqual(i, (int)all[i].skill);
            Assert.AreEqual(24, Req.Count);
        }

        [Test]
        public void EachCondition_MetExactly_AndAnySingleMissingElementFails()
        {
            foreach (var def in HiddenSkillDatabase.All)
            {
                Assert.IsTrue(def.met(StateFor(def.skill)), def.name + " 최소 조건 충족");
                var r = Req[def.skill];
                foreach (var card in r.cards.Keys)
                {
                    var st = StateFor(def.skill); st.cards[card] -= 1;
                    Assert.IsFalse(def.met(st), $"{def.name}: {card} 1 부족");
                }
                foreach (var n in r.coffee)
                {
                    var st = StateFor(def.skill); st.coffee.Remove(n);
                    Assert.IsFalse(def.met(st), $"{def.name}: 커피 {n} 없음");
                }
                if (r.hybrid) { var st = StateFor(def.skill); st.hybrid = false; Assert.IsFalse(def.met(st)); }
                foreach (var f in r.forms) { var st = StateFor(def.skill); st.forms.Remove(f); Assert.IsFalse(def.met(st)); }
                Assert.IsFalse(def.met(new FakeState()), def.name + " 빈 상태");
            }
        }

        [Test]
        public void Reachable_CardsExistWithEnoughStacksAndWeaponTypes_CoffeeNodesExist()
        {
            var cards = CardDatabase.LoadAll().ToDictionary(c => c.id);
            var coffee = new HashSet<string>(SkillDatabase.LoadAll().Where(s => s != null).Select(s => s.id));
            foreach (var kv in Req)
            {
                foreach (var c in kv.Value.cards)
                {
                    Assert.IsTrue(cards.ContainsKey(c.Key), c.Key);
                    Assert.GreaterOrEqual(cards[c.Key].maxStacks, c.Value, $"{kv.Key}: {c.Key} 최대 중첩 안");
                }
                foreach (var n in kv.Value.coffee) Assert.IsTrue(coffee.Contains(n), n);
                var restrictions = kv.Value.cards.Keys.Select(id => cards[id].weaponRestriction).Distinct().ToList();
                Assert.IsFalse(restrictions.Contains(WeaponRestriction.MeleeOnly) && restrictions.Contains(WeaponRestriction.RangedOnly),
                    $"{kv.Key}: 한 런에서 근접·원거리 전용 카드를 함께 요구하면 도달 불가");
                int picks = kv.Value.cards.Values.Sum();
                Assert.LessOrEqual(picks, 8, $"{kv.Key}: 실제 런 카드 25장 안에서 도달 가능한 요구량");
            }
            Assert.AreEqual(WeaponRestriction.MeleeOnly, cards["flambe"].weaponRestriction);
            Assert.AreEqual(WeaponRestriction.MeleeOnly, cards["second_infusion"].weaponRestriction);
            Assert.AreEqual(WeaponRestriction.RangedOnly, cards["champagne_spray"].weaponRestriction);
            Assert.AreEqual(WeaponRestriction.RangedOnly, cards["tea_leaf_scatter"].weaponRestriction);
        }

        [Test]
        public void Evaluate_OncePerRun_NewRunStartsEmpty()
        {
            var run = new RunHiddenSkills();
            var st = StateFor(HiddenSkill.HotToddy);
            var first = run.Evaluate(st);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(HiddenSkill.HotToddy, first[0].skill);
            Assert.AreEqual(0, run.Evaluate(st).Count, "런당 1회");
            Assert.IsTrue(run.Has(HiddenSkill.HotToddy));
            Assert.AreEqual(1, run.Count);
            Assert.AreEqual(0, new RunHiddenSkills().Count, "새 런 초기화");
            Assert.AreEqual(0, run.Evaluate(null).Count);
        }

        [TestCase(100f, 0f)] [TestCase(119f, 0f)] [TestCase(120f, .01f)] [TestCase(250f, .07f)] [TestCase(900f, .20f)]
        public void ThickMug_PerTwentyOverHundred_Capped(float maxHp, float bonus)
        {
            Assert.AreEqual(bonus, HiddenSkillRuntime.ThickMugBonus(maxHp), 1e-5f);
        }
    }
}
