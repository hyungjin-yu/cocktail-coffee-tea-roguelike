using NUnit.Framework;

namespace CCT.Tests
{
    public class CoffeeBrewingTests
    {
        [TestCase(CoffeeBrewMethod.EspressoMachine, 250, 450, 120, 240)]
        [TestCase(CoffeeBrewMethod.PourOver, 1800, 3000, 80, 180)]
        [TestCase(CoffeeBrewMethod.MokaPot, 1200, 2200, 80, 180)]
        public void EachMethodHasItsOwnSafeRecipeRange(CoffeeBrewMethod method, int waterMin, int waterMax, int doseMin, int doseMax)
        {
            Assert.AreEqual(waterMin, CoffeeBrewing.WaterMinTenths(method));
            Assert.AreEqual(waterMax, CoffeeBrewing.WaterMaxTenths(method));
            Assert.AreEqual(doseMin, CoffeeBrewing.DoseMinTenths(method));
            Assert.AreEqual(doseMax, CoffeeBrewing.DoseMaxTenths(method));
        }

        [Test]
        public void TargetExtractionRatioScoresAboveAnOutOfRangeRecipe()
        {
            Assert.Greater(CoffeeBrewing.Strength01(CoffeeBrewMethod.EspressoMachine, 180, 360),
                CoffeeBrewing.Strength01(CoffeeBrewMethod.EspressoMachine, 180, 900));
            Assert.Greater(CoffeeBrewing.Strength01(CoffeeBrewMethod.PourOver, 120, 1800),
                CoffeeBrewing.Strength01(CoffeeBrewMethod.PourOver, 120, 400));
            Assert.Greater(CoffeeBrewing.Strength01(CoffeeBrewMethod.MokaPot, 120, 960),
                CoffeeBrewing.Strength01(CoffeeBrewMethod.MokaPot, 120, 1800));
        }

        [Test]
        public void InvalidMethodHasNoValidRangeOrStrength()
        {
            var invalid = (CoffeeBrewMethod)99;
            Assert.IsFalse(CoffeeBrewing.IsDefined(invalid));
            Assert.AreEqual(0, CoffeeBrewing.WaterMaxTenths(invalid));
            Assert.AreEqual(0f, CoffeeBrewing.Strength01(invalid, 100, 1000));
        }
    }
}
