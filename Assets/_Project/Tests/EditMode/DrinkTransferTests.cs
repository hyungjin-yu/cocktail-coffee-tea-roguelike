using System;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>73 단계1 장부: 병→지거→용기→잔 전체와 공중 덩어리까지 성분·질량 보존, 차 추출·거름.</summary>
    public class DrinkTransferTests
    {
        const double Eps = 1e-6;

        static FluidPart Gin(double ml) => new FluidPart("gin", ml, 0.95, 0.1);
        static FluidPart Curacao(double ml) => new FluidPart("blue_curacao", ml, 1.08, 0.35);
        static FluidPart Water(double ml) => new FluidPart(DrinkTransferLedger.WaterId, ml, 1.0, 0);

        [Test]
        public void Emit_KeepsContentInFlightUntilArrival()
        {
            var l = new DrinkTransferLedger();
            l["bottle"].Liquid.Pour(Gin(60), 1);
            double moved = l.Emit("bottle", "jigger", 10, 0.3, 0.3);
            Assert.AreEqual(10, moved, Eps);
            Assert.AreEqual(50, l["bottle"].LiquidMl, Eps);
            Assert.AreEqual(0, l["jigger"].LiquidMl, Eps, "아직 공중: 받는 그릇에 순간이동하지 않는다");
            Assert.AreEqual(10, l.InFlightMl("jigger"), Eps);
            Assert.AreEqual(60, l.ComponentTotalMl("gin"), Eps);
            l.Advance(0.2);
            Assert.AreEqual(0, l["jigger"].LiquidMl, Eps);
            l.Advance(0.11);
            Assert.AreEqual(10, l["jigger"].LiquidMl, Eps);
            Assert.IsFalse(l.HasInFlight());
        }

        [Test]
        public void Emit_NeverTakesMoreThanSource()
        {
            var l = new DrinkTransferLedger();
            l["jigger"].Liquid.Pour(Gin(4), 1);
            Assert.AreEqual(4, l.Emit("jigger", "glass", 9, 0, 0.5), Eps);
            Assert.AreEqual(0, l.Emit("jigger", "glass", 1, 0, 0.5), Eps);
        }

        [Test]
        public void FullCocktailChain_ConservesEveryComponentEveryStep()
        {
            var l = new DrinkTransferLedger();
            l["bottle_gin"].Liquid.Pour(Gin(80), 1);
            l["bottle_cur"].Liquid.Pour(Curacao(20), 1);
            double gin0 = 80, cur0 = 20, mass0 = l.TotalMassG();
            void Check()
            {
                Assert.AreEqual(gin0, l.ComponentTotalMl("gin"), Eps);
                Assert.AreEqual(cur0, l.ComponentTotalMl("blue_curacao"), Eps);
                Assert.AreEqual(mass0, l.TotalMassG(), 1e-6);
            }
            // 병 → 지거(작은 조각, 낙하 중) → 믹싱 글라스 → 잔
            for (int i = 0; i < 45; i++) { l.Emit("bottle_gin", "jigger", 1, 0.25, 0.3); l.Advance(1 / 60.0); Check(); }
            l.Flush("jigger");
            Assert.AreEqual(45, l["jigger"].LiquidMl, Eps);
            while (!l["jigger"].Liquid.IsEmpty) { l.Emit("jigger", "mix", 0.7, 0.3, 0.4); l.Advance(1 / 60.0); Check(); }
            l.Emit("bottle_cur", "jigger", 15, 0.2, 0.3);
            l.Flush();
            l.Emit("jigger", "mix", 15, 0.2, 0.4);
            l.Flush();
            Check();
            l["mix"].Liquid.Mix(0.6, 6);
            Check();
            while (!l["mix"].Liquid.IsEmpty) { l.Emit("mix", "serve", 1.3, 0.35, 0.5); l.Advance(1 / 60.0); Check(); }
            l.Flush();
            Assert.AreEqual(60, l["serve"].LiquidMl, Eps);
            Assert.AreEqual(45, l["serve"].Liquid.ComponentVolume("gin"), Eps);
            Assert.AreEqual(15, l["serve"].Liquid.ComponentVolume("blue_curacao"), Eps);
            Assert.AreEqual(35, l["bottle_gin"].LiquidMl, Eps);
            Assert.AreEqual(5, l["bottle_cur"].LiquidMl, Eps);
        }

        [Test]
        public void TeaChain_LeavesStayInPot_ExtractMovesWithLiquid()
        {
            var l = new DrinkTransferLedger();
            l["tin"].AddLeaf("black_tea", 10);
            l["kettle"].Liquid.Pour(Water(150), 1);
            double solids0 = l.SolidsTotalG(), mass0 = l.TotalMassG();
            // 통 → 다하 3g → 다관
            for (int i = 0; i < 30; i++) { l.EmitLeaves("tin", "chaha", "black_tea", 0.1, 0.2); l.Advance(1 / 30.0); }
            l.Flush();
            Assert.AreEqual(3, l["chaha"].Leaf("black_tea"), Eps);
            while (l["chaha"].LeafG > 1e-9) { l.EmitLeaves("chaha", "pot", "black_tea", 0.25, 0.3); l.Advance(1 / 30.0); Assert.AreEqual(solids0, l.SolidsTotalG(), Eps); }
            l.Flush();
            // 물 붓기
            while (!l["kettle"].Liquid.IsEmpty) { l.Emit("kettle", "pot", 5, 0.3, 0.5); l.Advance(1 / 30.0); }
            l.Flush();
            Assert.AreEqual(150, l["pot"].WaterMl, Eps);
            Assert.AreEqual(0, l["pot"].TeaStrength, Eps, "우리기 전에는 맑은 물");
            for (int i = 0; i < 120; i++) l.Steep("pot", 1 / 60.0);
            Assert.Greater(l["pot"].TeaStrength, 0.9);
            Assert.AreEqual(3 * DrinkTransferLedger.ExtractableFraction * l["pot"].TeaStrength, l["pot"].DissolvedG, 1e-6);
            Assert.AreEqual(solids0, l.SolidsTotalG(), Eps, "잎 + 추출분 = 처음 잎");
            Assert.AreEqual(mass0, l.TotalMassG(), 1e-6);
            double leavesInPot = l["pot"].LeafG, dissolved = l["pot"].DissolvedG;
            // 찻잔으로: 액체와 추출분만, 잎은 다관에 남는다
            while (!l["pot"].Liquid.IsEmpty) { l.Emit("pot", "cup", 3, 0.3, 0.5); l.Advance(1 / 60.0); Assert.AreEqual(mass0, l.TotalMassG(), 1e-6); }
            l.Flush();
            Assert.AreEqual(150, l["cup"].WaterMl, Eps);
            Assert.AreEqual(leavesInPot, l["pot"].LeafG, Eps);
            Assert.AreEqual(0, l["cup"].LeafG, Eps);
            Assert.AreEqual(dissolved, l["cup"].DissolvedG, 1e-6);
            Assert.AreEqual(0, l["pot"].DissolvedG, 1e-9);
            Assert.Greater(l["cup"].TeaStrength, 0.9, "찻잔의 우림 정도는 다관과 같다");
        }

        [Test]
        public void PartialTeaPour_SplitsExtractByWaterFraction()
        {
            var l = new DrinkTransferLedger();
            l["pot"].AddLeaf("green_tea", 2);
            l["pot"].Liquid.Pour(Water(100), 1);
            for (int i = 0; i < 300; i++) l.Steep("pot", 0.02);
            double d = l["pot"].DissolvedG;
            l.Emit("pot", "cup", 25, 0, 0.5);
            l.Flush();
            Assert.AreEqual(d * 0.25, l["cup"].DissolvedG, 1e-9);
            Assert.AreEqual(d * 0.75, l["pot"].DissolvedG, 1e-9);
        }

        [Test]
        public void FallSeconds_FromDropAndGravity()
        {
            Assert.AreEqual(0, DrinkTransferLedger.FallSeconds(0, 1800), Eps);
            Assert.AreEqual(Math.Sqrt(2 * 180 / 1800.0), DrinkTransferLedger.FallSeconds(180, 1800), Eps);
        }
    }
}
