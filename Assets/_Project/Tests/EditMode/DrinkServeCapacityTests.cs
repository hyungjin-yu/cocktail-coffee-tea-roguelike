using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>73: 완성 잔 용량 고정 — 허용 최대 배합 + 최대 얼음에서도 여유 공간, 초과는 몰래 줄이지 않고 거부.</summary>
    public class DrinkServeCapacityTests
    {
        [Test]
        public void MaxCocktailWithMaxIce_FitsWithHeadroom()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.SetAmount("gin", DrinkRules.CocktailLiquidMax - DrinkRules.CocktailWaterMax);
            r.water = DrinkRules.CocktailWaterMax;
            r.ice = DrinkRules.IceMax;
            Assert.IsTrue(DrinkValidator.Check(r).Ok, DrinkValidator.Check(r).First);
            float fill = DrinkRules.ServeFillMl(r) / DrinkRules.ServeCocktailCapacityMl;
            Assert.LessOrEqual(fill, DrinkRules.ServeFillMax);
            Assert.GreaterOrEqual(fill, 0.75f, "최대 배합은 잔의 대부분을 쓰되 윗부분 여유");
        }

        [Test]
        public void MaxTea_FitsTeacupWithHeadroom()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Tea);
            r.SetAmount("black_tea", DrinkRules.TeaLeafMax);
            r.SetAmount("honey", DrinkRules.TeaAdditiveMax);
            Assert.IsTrue(DrinkValidator.Check(r).Ok, DrinkValidator.Check(r).First);
            Assert.LessOrEqual(DrinkRules.ServeFillMl(r) / DrinkRules.ServeTeaCapacityMl, DrinkRules.ServeFillMax);
        }

        [Test]
        public void OverCapacity_IsRefusedNotShrunk()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Tea);
            r.SetAmount("black_tea", 30);
            r.SetAmount("honey", DrinkRules.TeaAdditiveMax);
            r.ice = 2; // 차에 얼음은 화면에서 넣을 수 없지만 공유 코드 등으로 들어오면 용량 초과
            var res = DrinkValidator.Check(r);
            Assert.IsFalse(res.Ok);
            StringAssert.Contains("잔에 다 들어가지 않습니다", string.Join("\n", res.Errors));
            Assert.AreEqual(DrinkRules.TeaAdditiveMax, r.AmountOf("honey"), "배합을 몰래 줄이지 않음");
        }

        [Test]
        public void ServeCapacities_MatchStageVessels()
        {
            Assert.AreEqual(DrinkRules.ServeCocktailCapacityMl, DrinkStageView.CapacityFor("dl_glass_cocktail"));
            Assert.AreEqual(DrinkRules.ServeTeaCapacityMl, DrinkStageView.CapacityFor("dl_teacup"));
            Assert.AreEqual(DrinkVesselView.CubeVolumeMl, DrinkRules.IceCubeMl, 1e-6);
        }
    }
}
