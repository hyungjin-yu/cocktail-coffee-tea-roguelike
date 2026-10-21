using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>72 §2·§3 수급/연구 포인트(순수 계산). 보정은 77 후보 중앙값 상대 보정, 일반·정예 가중치는 78(희귀 후보 포함).</summary>
    public class DrinkSupplyTests
    {
        static int None(string id) => 0;

        /// <summary>재료별 회분(uses × 기준 사용량, 0.1 단위) 보유.</summary>
        static Func<string, int> Uses(Func<string, double> uses) => id => (int)Math.Round(uses(id) * DrinkSupply.Get(id).baseUse);

        static void AssertFiniteSumOne(Dictionary<string, double> p, string label)
        {
            double sum = 0;
            foreach (var v in p.Values) { Assert.IsFalse(double.IsNaN(v) || double.IsInfinity(v), label); Assert.GreaterOrEqual(v, 0.0, label); sum += v; }
            Assert.AreEqual(1.0, sum, 1e-12, label);
        }

        [Test]
        public void Multiplier_RelativeToMedian_Clamped()
        {
            Assert.AreEqual(1.0, DrinkSupply.Multiplier(6, 6), 1e-12);
            Assert.AreEqual(1.0, DrinkSupply.Multiplier(24, 24), 1e-12);
            Assert.AreEqual(9.0 / 6.0, DrinkSupply.Multiplier(3, 6), 1e-12, "(m+3)/(e+3)");
            Assert.AreEqual(15.0 / 27.0, DrinkSupply.Multiplier(24, 12), 1e-12);
            Assert.AreEqual(2.0, DrinkSupply.Multiplier(0, 1000), 1e-12, "상한 2.0");
            Assert.AreEqual(0.5, DrinkSupply.Multiplier(1000, 0), 1e-12, "하한 0.5");
            Assert.AreEqual(2.0, DrinkSupply.Multiplier(-5, 3), 1e-12, "음수 보유는 0회분");
        }

        [Test]
        public void Median_OddEvenEmpty()
        {
            Assert.AreEqual(0, DrinkSupply.Median(new List<double>()), 1e-12);
            Assert.AreEqual(3, DrinkSupply.Median(new List<double> { 9, 3, 1 }), 1e-12);
            Assert.AreEqual(5, DrinkSupply.Median(new List<double> { 10, 0, 4, 6 }), 1e-12, "짝수 = 가운데 두 값 평균");
        }

        [Test]
        public void Tables_SumTo100_78Weights_RareCandidatesInNormalAndElite()
        {
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
            {
                int sum = 0;
                foreach (var r in DrinkSupply.Table) sum += r.Weight(s);
                Assert.AreEqual(100 + DrinkIngredients.All.Count - 29, sum, s.ToString());
            }
            // 81: 원두 14%를 모든 획득처에 추가하고 합계 100%를 유지하도록 조정.
            // 초기 29종의 고정 가중치. 이후 각 분야 30종까지 추가된 58종은 각 획득원에서 1점씩 받는다.
            var expected = new Dictionary<string, int[]>
            {
                { "gin", new[] { 6, 5, 4, 3 } }, { "rum", new[] { 6, 5, 4, 3 } },
                { "black_tea", new[] { 6, 5, 4, 3 } }, { "green_tea", new[] { 6, 5, 4, 3 } }, { "coffee_bean", new[] { 5, 5, 5, 4 } }, { "steamed_milk", new[] { 6, 5, 4, 4 } },
                { "lemon_juice", new[] { 7, 6, 7, 6 } }, { "honey", new[] { 7, 6, 7, 6 } },
                { "blue_curacao", new[] { 4, 8, 8, 12 } }, { "grenadine", new[] { 4, 8, 8, 12 } },
                { "absinthe", new[] { 2, 3, 3, 3 } }, { "creme_cassis", new[] { 3, 4, 4, 4 } }, { "limoncello", new[] { 4, 3, 4, 3 } },
                { "white_tea", new[] { 3, 4, 4, 4 } }, { "oolong_tea", new[] { 4, 3, 3, 3 } }, { "jasmine_tea", new[] { 4, 3, 3, 3 } }, { "chamomile_flower", new[] { 4, 3, 3, 3 } },
                { "ethiopia_yirgacheffe_kochere", new[] { 3, 4, 4, 4 } }, { "colombia_huila_vergel", new[] { 3, 3, 4, 4 } }, { "brazil_mantiqueira_alves_lima", new[] { 4, 3, 4, 4 } },
                { "lime_juice", new[] { 1, 1, 1, 1 } }, { "simple_syrup", new[] { 1, 1, 1, 1 } }, { "dry_vermouth", new[] { 1, 1, 1, 1 } },
                { "darjeeling_tea", new[] { 1, 1, 1, 1 } }, { "anxi_tieguanyin", new[] { 1, 1, 1, 1 } }, { "pu_er_tea", new[] { 1, 1, 1, 1 } },
                { "panama_esmeralda_geisha", new[] { 1, 1, 1, 1 } }, { "colombia_huila_san_pedro_geisha", new[] { 1, 1, 1, 1 } }, { "colombia_la_esperanza_bourbon", new[] { 1, 1, 1, 1 } },
            };
            Assert.AreEqual(DrinkIngredients.All.Count, DrinkSupply.Table.Length, "표에 87종 모두 등록");
            foreach (var r in DrinkSupply.Table)
            {
                if (expected.TryGetValue(r.id, out var weights)) CollectionAssert.AreEqual(weights, new[] { r.normal, r.elite, r.midBoss, r.finalBoss }, r.id);
                else CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, new[] { r.normal, r.elite, r.midBoss, r.finalBoss }, r.id);
            }
            Assert.AreEqual(25, DrinkSupply.NormalKillsPerBundle);
            Assert.AreEqual(60, DrinkSupply.NormalBundleCap, "판당 일반 최대 60묶음(1500킬)");
            Assert.AreEqual(3, DrinkSupply.MidBossDraws);
            Assert.AreEqual(6, DrinkSupply.FinalBossDraws);
            var normal = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, None);
            Assert.AreEqual(DrinkIngredients.All.Count, normal.Count, "일반 후보 전체");
            Assert.AreEqual(4.0 / 158.0, normal["blue_curacao"], 1e-12);
            Assert.AreEqual(4.0 / 158.0, normal["grenadine"], 1e-12);
            Assert.AreEqual(6.0 / 158.0, normal["gin"], 1e-12, "보유 0이면 모두 같은 회분이라 기본 비율 그대로");
            var sumP = 0.0; foreach (var v in normal.Values) sumP += v;
            Assert.AreEqual(1.0, sumP, 1e-12);
        }

        [Test]
        public void Correction_HeldIngredientLessLikely_LowStockMoreLikely()
        {
            // 진 6회분, 나머지 재료 24회분: 양수 중앙값 24를 기준으로 진은 부족 재료 보정을 받는다.
            var p = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Uses(id => id == "gin" ? 6 : 24));
            double w = DrinkSupply.Get("gin").normal;
            Assert.AreEqual(2 * w / (158 + w), p["gin"], 1e-12);
            Assert.Less(p["rum"], DrinkSupply.Get("rum").normal / 158.0, "중앙값보다 더 보유한 재료는 상대적으로 낮아진다");
        }

        [Test]
        public void Relative_SixUsesFavoredOverTwentyFour()
        {
            // 72 절대 보정은 6회분 이상을 모두 ×0.5로 포화시켜 구분하지 못했다. 77: 중앙값 24 대비 6회분 진은 c=27/9→2.0.
            var p = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Uses(id => id == "gin" ? 6 : 24));
            double w = DrinkSupply.Get("gin").normal;
            Assert.AreEqual(2 * w / (158 + w), p["gin"], 1e-12);
            Assert.Greater(p["gin"], p["rum"]);
            // 진 6, 럼 24, 나머지 12: 진 > 홍차 > 럼
            var q = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Uses(id => id == "gin" ? 6 : id == "rum" ? 24 : 12));
            Assert.Greater(q["gin"], q["black_tea"]);
            Assert.Greater(q["black_tea"], q["rum"]);
            AssertFiniteSumOne(q, "6/12/24");
        }

        [Test]
        public void EqualUses_KeepOriginalSourceProbabilities([Values(0, 6, 24, 100000)] int uses)
        {
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
            {
                var p = DrinkSupply.Probabilities(s, false, Uses(id => uses));
                foreach (var r in DrinkSupply.Table)
                    if (r.Weight(s) > 0) Assert.AreEqual(r.Weight(s) / 158.0, p[r.id], 1e-12, $"{s} {r.id} {uses}");
                    else Assert.IsFalse(p.ContainsKey(r.id));
                if (s == DrinkSupply.Source.MidBoss || s == DrinkSupply.Source.FinalBoss)
                {
                    var rare = DrinkSupply.Probabilities(s, true, Uses(id => uses));
                    Assert.AreEqual(2, rare.Count);
                    Assert.AreEqual(0.5, rare["blue_curacao"], 1e-12);
                    Assert.AreEqual(0.5, rare["grenadine"], 1e-12);
                }
            }
        }

        [Test]
        public void ExtremeHoldings_FiniteAndSumOne()
        {
            var cases = new Func<string, int>[] { None, id => int.MaxValue, id => -1000, id => id == "honey" ? 0 : int.MaxValue, id => id == "honey" ? int.MaxValue : 0 };
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
                for (int c = 0; c < cases.Length; c++)
                {
                    AssertFiniteSumOne(DrinkSupply.Probabilities(s, false, cases[c]), $"{s} case{c}");
                    if (s == DrinkSupply.Source.MidBoss || s == DrinkSupply.Source.FinalBoss)
                        AssertFiniteSumOne(DrinkSupply.Probabilities(s, true, cases[c]), $"{s} rare case{c}");
                }
        }

        [Test]
        public void RareGuarantee_ComparesOnlyTheTwoRares()
        {
            // 큐라소 0, 그레나딘 10회분: 양수 보유량 중앙값 10 → 큐라소 c=2.0, 그레나딘 c=1.0. 일반 재료 보유량은 영향 없음.
            var pa = DrinkSupply.Probabilities(DrinkSupply.Source.FinalBoss, true, Uses(id => id == "grenadine" ? 10 : 0));
            var pb = DrinkSupply.Probabilities(DrinkSupply.Source.FinalBoss, true, Uses(id => id == "grenadine" ? 10 : id == "blue_curacao" ? 0 : 500));
            Assert.AreEqual(2, pa.Count);
            Assert.AreEqual(2.0 / 3.0, pa["blue_curacao"], 1e-12);
            Assert.AreEqual(pa["blue_curacao"], pb["blue_curacao"], 1e-12);
        }

        [Test]
        public void Normal_RaresAreCandidates_IncludedInMedian_NotGuaranteed()
        {
            // 양수 재고 중앙값으로 보유량 보정을 계산한다. 일반 희귀는 후보에 포함되지만 보장하지 않는다.
            var a = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Uses(id => 8));
            var b = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Uses(id => DrinkSupply.Get(id).rare ? 0 : 8));
            Assert.AreEqual(DrinkIngredients.All.Count, b.Count);
            Assert.AreEqual(4.0 / 158.0, a["grenadine"], 1e-12);
            // 희귀 두 재료가 모두 0회분이라 가중치가 각각 2배(4→8). 총 가중치 158+8.
            Assert.AreEqual(8.0 / 166.0, b["grenadine"], 1e-12);
            Assert.Less(b["grenadine"], 0.5, "부족해도 매 묶음 희귀가 나오는 것은 아니다");
            Assert.IsFalse(DrinkSupply.RareOnly(DrinkSupply.Source.Normal, 0));
            Assert.IsFalse(DrinkSupply.RareOnly(DrinkSupply.Source.Elite, 0));
            AssertFiniteSumOne(b, "rare-short");
        }

        [Test]
        public void UserReportedBalance_LowestUsesFavoredAmongEqualWeights()
        {
            // 72 사용자 최종 잔고(0.1 단위): 진480mL 럼525mL 블루60mL 그레나딘85mL 레몬365mL 꿀195mL 홍차31g 녹차41g.
            // 81: 커피 원두·스팀 우유 포함. 재료 확장: 업데이트 직후라 신규 10종은 0 — 중앙값이 0 쪽으로 내려가 신규 재료가 우대된다.
            var units = new Dictionary<string, int> { { "gin", 4800 }, { "rum", 5250 }, { "blue_curacao", 600 }, { "grenadine", 850 }, { "lemon_juice", 3650 }, { "honey", 1950 }, { "black_tea", 310 }, { "green_tea", 410 }, { "coffee_bean", 600 }, { "steamed_milk", 0 } };
            int Held(string id) => units.TryGetValue(id, out int v) ? v : 0;
            var p = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Held);
            Assert.AreEqual(DrinkIngredients.All.Count, p.Count, "모든 일반 후보");
            foreach (var r in DrinkSupply.Table)
                if (!units.ContainsKey(r.id)) Assert.Greater(p[r.id], r.normal / 158.0, r.id + ": 업데이트 직후 0회분 신규 재료는 기본치보다 잘 나온다");
            Assert.Less(p["gin"], DrinkSupply.Get("gin").normal / 158.0, "보유 회분이 양수 중앙값보다 많으면 확률 감소");
            Assert.Greater(p["steamed_milk"], DrinkSupply.Get("steamed_milk").normal / 158.0, "재고 0인 재료는 보정으로 확률 증가");
            AssertFiniteSumOne(p, "user");
            // 신규 재료가 기존 재료 중앙값만큼 쌓였다고 보면 적은 원두·희귀가 기본치보다 올라간다.
            var legacy = new List<double>();
            foreach (var kv in units) legacy.Add(DrinkSupply.Uses(DrinkSupply.Get(kv.Key), kv.Value));
            double m = DrinkSupply.Median(legacy);
            int Caught(string id) => units.TryGetValue(id, out int v) ? v : (int)Math.Round(m * DrinkSupply.Get(id).baseUse);
            var q = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, Caught);
            Assert.Greater(q["coffee_bean"], DrinkSupply.Get("coffee_bean").normal / 158.0, "보유량이 적은 원두의 확률이 기본치보다 상승");
            Assert.Greater(q["gin"], q["rum"], "보유량이 더 적은 진이 럼보다 상대적으로 유리");
            var e = DrinkSupply.Probabilities(DrinkSupply.Source.Elite, false, Caught);
            Assert.Greater(e["blue_curacao"], DrinkSupply.Get("blue_curacao").elite / 158.0, "정예: 부족한 희귀가 기본치보다 올라간다");
            Assert.Greater(e["grenadine"], DrinkSupply.Get("grenadine").elite / 158.0, "정예: 부족한 희귀가 기본치보다 올라간다");
            AssertFiniteSumOne(e, "user elite");
        }

        [Test]
        public void Roll_ReplaysAccumulatedHeldThroughRealPath()
        {
            // 실제 Roll 경로: 각 추첨은 (보상 이전 보유 + 이 보상의 앞 묶음)으로 Probabilities를 구하고 같은 u로 고른다.
            Func<string, int> held = Uses(id => id == "gin" ? 1 : id == "blue_curacao" ? 0 : 4);
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
                for (int seed = 0; seed < 200; seed++)
                {
                    string runId = "replay" + seed, awardId = "award_" + s;
                    var rolled = DrinkSupply.Roll(s, runId, awardId, held);
                    Assert.AreEqual(DrinkSupply.Draws(s), rolled.Count);
                    var got = new Dictionary<string, int>();
                    for (int i = 0; i < rolled.Count; i++)
                    {
                        Func<string, int> h = x => held(x) + (got.TryGetValue(x, out int g) ? g : 0);
                        Assert.AreEqual(DrinkSupply.Pick(s, DrinkSupply.RareOnly(s, i), h, DrinkSupply.Unit(runId, awardId, i)), rolled[i].id, $"{s} {seed} #{i}");
                        got[rolled[i].id] = (got.TryGetValue(rolled[i].id, out int a) ? a : 0) + rolled[i].amount;
                    }
                }
        }

        [Test]
        public void Roll_Deterministic_BundleAmountsFixed_RareGuarantee()
        {
            var a = DrinkSupply.Roll(DrinkSupply.Source.FinalBoss, "run-1", "boss4_Final", None);
            var b = DrinkSupply.Roll(DrinkSupply.Source.FinalBoss, "run-1", "boss4_Final", None);
            Assert.AreEqual(6, a.Count);
            CollectionAssert.AreEqual(a, b, "같은 runId·보상 ID는 저장 재시도에도 같은 결과");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(DrinkSupply.Get(a[i].id).bundle, a[i].amount, "묶음 수량은 고정");
                if (i >= 4) Assert.IsTrue(DrinkSupply.Get(a[i].id).rare, "최종 보스 5·6번째는 희귀 후보만");
            }
            var mid = DrinkSupply.Roll(DrinkSupply.Source.MidBoss, "run-1", "boss1_Mid", None);
            Assert.AreEqual(3, mid.Count);
            Assert.IsTrue(DrinkSupply.Get(mid[2].id).rare, "중간 보스 3번째는 희귀 보장");
        }

        [Test]
        public void Roll_EarlierBundlesCountTowardHeld()
        {
            // 진 0, 나머지 3회분: 중앙값 3 → 진 c=2. 진 묶음(0.5회분)이 쌓이면 다음 추첨의 진 확률이 내려간다
            Func<string, int> held = Uses(id => id == "gin" ? 0 : 3);
            var p0 = DrinkSupply.Probabilities(DrinkSupply.Source.MidBoss, false, held);
            var p1 = DrinkSupply.Probabilities(DrinkSupply.Source.MidBoss, false, id => held(id) + (id == "gin" ? 150 : 0));
            Assert.Less(p1["gin"], p0["gin"]);
        }

        [Test]
        public void Distribution_ManyRuns_MatchesTable()
        {
            var count = new Dictionary<string, int>();
            const int n = 40000;
            for (int i = 0; i < n; i++)
            {
                var r = DrinkSupply.Roll(DrinkSupply.Source.Normal, "seed" + i, "normal1", None);
                count[r[0].id] = (count.TryGetValue(r[0].id, out int c) ? c : 0) + 1;
            }
            foreach (var row in DrinkSupply.Table)
            {
                double expected = row.normal / 158.0;
                double got = (count.TryGetValue(row.id, out int c) ? c : 0) / (double)n;
                Assert.AreEqual(expected, got, 0.01, row.id);
            }
        }

        [Test]
        public void Unit_InRange_IndependentPerIndex()
        {
            var seen = new HashSet<double>();
            for (int i = 0; i < 1000; i++)
            {
                double u = DrinkSupply.Unit("r", "a", i);
                Assert.GreaterOrEqual(u, 0.0); Assert.Less(u, 1.0);
                seen.Add(u);
            }
            Assert.AreEqual(1000, seen.Count);
        }

        [Test]
        public void ResearchPoints_PerRun()
        {
            Assert.AreEqual(0, DrinkSupply.RunRp(99, 0, 0));
            Assert.AreEqual(2, DrinkSupply.RunRp(250, 0, 0));
            Assert.AreEqual(5, DrinkSupply.RunRp(9999, 0, 0), "처치분 최대 5");
            Assert.AreEqual(5 + 2 * 3 + 4, DrinkSupply.RunRp(800, 3, 1), "중간 보스 3(강화 포함) + 최종");
            Assert.AreEqual(0, DrinkSupply.RunRp(-5, 0, 0));
        }
    }
}
