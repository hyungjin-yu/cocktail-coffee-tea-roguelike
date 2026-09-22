using NUnit.Framework;

namespace CCT.Tests
{
    public class IngredientGaugeTests
    {
        [Test]
        public void ThresholdsGrow_10_15_22_33()
        {
            var gauge = new IngredientGauge(IngredientType.Cocktail);
            Assert.AreEqual(10, gauge.Threshold);

            gauge.Add(10);
            Assert.AreEqual(2, gauge.Level);
            Assert.AreEqual(15, gauge.Threshold);

            gauge.Add(15);
            Assert.AreEqual(3, gauge.Level);
            Assert.AreEqual(22, gauge.Threshold);

            gauge.Add(22);
            Assert.AreEqual(4, gauge.Level);
            Assert.AreEqual(33, gauge.Threshold);
        }

        [Test]
        public void Overflow_CarriesOver_AndCanLevelMultipleTimes()
        {
            var gauge = new IngredientGauge(IngredientType.Cocktail);
            int fired = 0;
            gauge.LeveledUp += _ => fired++;

            // 10 + 15 = 25 이 필요, 27 을 넣으면 레벨업 2회 + 잔량 2
            int ups = gauge.Add(27);

            Assert.AreEqual(2, ups);
            Assert.AreEqual(2, fired);
            Assert.AreEqual(3, gauge.Level);
            Assert.AreEqual(2, gauge.Current);
        }

        [Test]
        public void BelowThreshold_DoesNotLevelUp()
        {
            var gauge = new IngredientGauge(IngredientType.Cocktail);
            Assert.AreEqual(0, gauge.Add(9));
            Assert.AreEqual(1, gauge.Level);
            Assert.AreEqual(0.9f, gauge.Fill01, 0.001f);
        }

        [Test]
        public void NeedMultiplier_LowersThreshold()
        {
            float multiplier = 1f;
            var gauge = new IngredientGauge(IngredientType.Cocktail, 10f, 1.5f, () => multiplier);

            multiplier = 0.8f;
            Assert.AreEqual(8, gauge.Threshold);
            Assert.AreEqual(1, gauge.Add(8));
        }

        [Test]
        public void NonPositiveAmount_IsIgnored()
        {
            var gauge = new IngredientGauge(IngredientType.Cocktail);
            Assert.AreEqual(0, gauge.Add(0));
            Assert.AreEqual(0, gauge.Add(-5));
            Assert.AreEqual(0, gauge.Current);
        }
    }
}
