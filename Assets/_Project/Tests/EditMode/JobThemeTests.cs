using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>47(2026-10-12) 직업 테마: 찻집(티 소믈리에)에는 칵테일 몹 없음, 바 65:35, 직업 없음은 기존 그대로, 히든 직업별 조건.</summary>
    public class JobThemeTests
    {
        sealed class CountingRandom : IRandomSource
        {
            readonly System.Random r;
            public int Calls;
            public CountingRandom(int seed) => r = new System.Random(seed);
            public float Value() { Calls++; return (float)r.NextDouble(); }
        }

        static readonly HashSet<EnemyDef> CocktailTrack = new HashSet<EnemyDef>
        { EnemyDef.Imp, EnemyDef.ShakerGolem, EnemyDef.Brute, EnemyDef.OliveBomber, EnemyDef.SwirlConjuror, EnemyDef.MartiniSpirit };

        [Test]
        public void TeaSommelier_NeverGetsCocktailTrack_AtAnyRunTime_IncludingSummonTable()
        {
            var rng = new CountingRandom(7);
            foreach (float t in new[] { 0f, 20f, 60f, 100f, 149f, 150f, 300f, 600f, 900f, 1199f })
                for (int i = 0; i < 3000; i++)
                {
                    var def = EnemyRoster.PickCommon(t, rng, Job.TeaSommelier);
                    Assert.IsFalse(CocktailTrack.Contains(def), $"t={t}: {def.name}");
                    Assert.AreNotEqual(IngredientType.Cocktail, def.dropType, "찻집 런 칵테일 재료 드랍 없음");
                    if (def.summonDef != null) Assert.IsFalse(CocktailTrack.Contains(def.summonDef), "소환형도 차 소환");
                }
        }

        [Test]
        public void Bartender_CocktailMain65_TeaAllowed35_CoffeeStill2Percent()
        {
            var rng = new CountingRandom(11);
            int n = 40000, cocktail = 0, tea = 0;
            for (int i = 0; i < n; i++) { var d = EnemyRoster.PickCommon(60f, rng, Job.Bartender); if (d.dropType == IngredientType.Cocktail) cocktail++; else tea++; }
            Assert.AreEqual(0.65, cocktail / (double)n, 0.01);
            Assert.AreEqual(0.35, tea / (double)n, 0.01, "바에도 차 몹(차 활용) 등장");
            int c2 = 0, t2 = 0, k2 = 0;
            for (int i = 0; i < n; i++)
                switch (EnemyRoster.PickCommon(600f, rng, Job.Bartender).dropType)
                { case IngredientType.Cocktail: c2++; break; case IngredientType.Tea: t2++; break; default: k2++; break; }
            Assert.AreEqual(0.637, c2 / (double)n, 0.01);
            Assert.AreEqual(0.343, t2 / (double)n, 0.01);
            Assert.AreEqual(0.02, k2 / (double)n, 0.005, "커피 2% 유지");
        }

        [Test]
        public void NoJob_IdenticalToLegacy_AndSameRandomConsumption()
        {
            foreach (float t in new[] { 10f, 200f, 800f })
            {
                var a = new CountingRandom(99); var b = new CountingRandom(99);
                for (int i = 0; i < 2000; i++)
                    Assert.AreSame(EnemyRoster.PickCommon(t, a), EnemyRoster.PickCommon(t, b, null), "직업 없음 = 기존 경로");
                Assert.AreEqual(a.Calls, b.Calls);
                var c = new CountingRandom(5); var d = new CountingRandom(5);
                for (int i = 0; i < 2000; i++) { EnemyRoster.PickCommon(t, c, Job.Bartender); EnemyRoster.PickCommon(t, d, Job.TeaSommelier); }
                Assert.AreEqual(2000 * 2, c.Calls, "트랙 1회 + 아키타입 1회(커피 포함)");
                Assert.AreEqual(2000 * 2, d.Calls);
            }
        }

        [Test]
        public void JobCards_ShowMapAndShares()
        {
            StringAssert.Contains("칵테일 바", JobPreset.Bartender.ThemeLine);
            StringAssert.Contains("다양한 음료 재료", JobPreset.Bartender.ThemeLine);
            StringAssert.Contains("전장", JobPreset.Bartender.ThemeLine);
            StringAssert.Contains("찻집", JobPreset.TeaSommelier.ThemeLine);
            StringAssert.Contains("차 재료와 다구", JobPreset.TeaSommelier.ThemeLine);
        }

        sealed class State : IHiddenRunState
        {
            public Dictionary<string, int> cards = new Dictionary<string, int>();
            public HashSet<string> coffee = new HashSet<string>();
            public Job job;
            public int Stacks(string id) => cards.TryGetValue(id, out var n) ? n : 0;
            public bool Coffee(string id) => coffee.Contains(id);
            public bool Hybrid => false;
            public int FinalFormCount => 0;
            public bool HasFinalForm(AttackFinalForm f) => false;
            public Job Job => job;
        }

        [Test]
        public void Hidden_TeaConditionsUseOnlyTeaCardsAndCoffee_SevenBartenderOnly()
        {
            var cards = CardDatabase.LoadAll().ToDictionary(c => c.id);
            var bartenderOnly = HiddenSkillDatabase.All.Where(d => !d.AvailableFor(Job.TeaSommelier)).Select(d => d.skill).ToList();
            CollectionAssert.AreEquivalent(new[] { HiddenSkill.HotToddy, HiddenSkill.BitterBlend, HiddenSkill.ShakeRhythm, HiddenSkill.SodaPump,
                HiddenSkill.FireShow, HiddenSkill.TiniMaster, HiddenSkill.BarTempo }, bartenderOnly);
            Assert.AreEqual(24, HiddenSkillDatabase.All.Count(d => d.AvailableFor(Job.Bartender)));
            // 티 조건 문자열에 칵테일 카드 이름이 없고, 모든 칵테일 카드를 최대로 가져도 티 런에서는 칵테일 의존 히든이 열리지 않는다
            var cocktailNames = cards.Values.Where(c => c.category == CardCategory.Cocktail).Select(c => c.displayName).ToList();
            foreach (var d in HiddenSkillDatabase.All.Where(x => x.AvailableFor(Job.TeaSommelier)))
                foreach (var n in cocktailNames) StringAssert.DoesNotContain(n, d.ConditionFor(Job.TeaSommelier), d.name);
            var onlyCocktail = new State { job = Job.TeaSommelier };
            foreach (var c in cards.Values.Where(c => c.category == CardCategory.Cocktail)) onlyCocktail.cards[c.id] = c.maxStacks;
            foreach (var d in HiddenSkillDatabase.All) Assert.IsFalse(d.MetFor(onlyCocktail), d.name + " 칵테일 카드만으로 티 런 해금 안 됨");
        }

        [Test]
        public void Hidden_TeaVariantsReachableWithTeaCardsWithinMaxStacks()
        {
            var cards = CardDatabase.LoadAll().ToDictionary(c => c.id);
            var allTea = new State { job = Job.TeaSommelier };
            foreach (var c in cards.Values.Where(c => c.category == CardCategory.Tea)) allTea.cards[c.id] = c.maxStacks;
            foreach (var s in SkillDatabase.LoadAll()) if (s != null) allTea.coffee.Add(s.id);
            foreach (var d in HiddenSkillDatabase.All.Where(x => x.AvailableFor(Job.TeaSommelier) && x.skill != HiddenSkill.PerfectCup))
                Assert.IsTrue(d.MetFor(allTea), d.name + " 차·커피 최대로 티 런 해금 가능");
            var bartender = new State { job = Job.Bartender, cards = new Dictionary<string, int>(allTea.cards), coffee = allTea.coffee };
            Assert.IsFalse(HiddenSkillDatabase.Get(HiddenSkill.FirstSip).MetFor(bartender), "바텐더는 기존 조건(드라이 진 필요)");
            var run = new RunHiddenSkills();
            Assert.Greater(run.Evaluate(allTea).Count, 10);
            Assert.AreEqual(0, run.Evaluate(allTea).Count, "런당 1회");
        }
    }
}
