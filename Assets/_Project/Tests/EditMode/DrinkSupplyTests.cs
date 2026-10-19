using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>72 §2·§3 수급/연구 포인트(순수 계산). 수치는 72의 최초 기본값.</summary>
    public class DrinkSupplyTests
    {
        static int None(string id) => 0;

        [Test]
        public void Multiplier_ZeroThreeSixUses()
        {
            Assert.AreEqual(1.5, DrinkSupply.Multiplier(0), 1e-12);
            Assert.AreEqual(1.0, DrinkSupply.Multiplier(3), 1e-12);
            Assert.AreEqual(0.5, DrinkSupply.Multiplier(6), 1e-12);
            Assert.AreEqual(0.5, DrinkSupply.Multiplier(40), 1e-12, "상한(완만한 하한 0.5)");
            Assert.AreEqual(1.25, DrinkSupply.Multiplier(1.5), 1e-12);
        }

        [Test]
        public void Tables_SumTo100_RareOnlyFromElite()
        {
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
            {
                int sum = 0;
                foreach (var r in DrinkSupply.Table) sum += r.Weight(s);
                Assert.AreEqual(100, sum, s.ToString());
            }
            var normal = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, None);
            Assert.IsFalse(normal.ContainsKey("blue_curacao"));
            Assert.IsFalse(normal.ContainsKey("grenadine"));
            Assert.AreEqual(0.18, normal["gin"], 1e-12, "보유 0이면 모두 ×1.5라 기본 비율 그대로");
            var sumP = 0.0; foreach (var v in normal.Values) sumP += v;
            Assert.AreEqual(1.0, sumP, 1e-12);
        }

        [Test]
        public void Correction_HeldIngredientLessLikely_LowStockMoreLikely()
        {
            // 진만 6회분(180mL) 보유: 진 ×0.5, 나머지 ×1.5 → 진 9 / (9 + 82×1.5)
            var p = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, id => id == "gin" ? 1800 : 0);
            Assert.AreEqual(9.0 / (9.0 + 82 * 1.5), p["gin"], 1e-12);
            Assert.Greater(p["rum"], 0.18, "부족 재료는 상대적으로 올라간다(확정 지급은 아님)");
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
            // 진 외 전부 6회분 이상 보유: 첫 묶음은 진이 매우 유력, 진 묶음이 쌓이면 다음 추첨의 진 확률이 내려간다
            Func<string, int> held = id => id == "gin" ? 0 : 100000;
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
                double expected = row.normal / 100.0;
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
