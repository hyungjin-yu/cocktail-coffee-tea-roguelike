using NUnit.Framework;

namespace CCT.Tests
{
    public class SpecialtyBonusTests
    {
        [Test]
        public void ZeroPoints_GiveNoBonus()
        {
            Assert.AreEqual(0f, SpecialtyBonus.DamagePercent(0), 0.0001f);
            Assert.AreEqual(1f, SpecialtyBonus.BeanGainMultiplier(0), 0.0001f);
        }

        [Test]
        public void DamagePercent_GrowsLinearly_BeforeCap()
        {
            Assert.AreEqual(0.015f, SpecialtyBonus.DamagePercent(1), 0.0001f);
            Assert.AreEqual(0.15f, SpecialtyBonus.DamagePercent(10), 0.0001f);
        }

        [Test]
        public void DamagePercent_ClampsAtCap()
        {
            Assert.AreEqual(Balance.SpecialtyDamagePercentCap, SpecialtyBonus.DamagePercent(1000), 0.0001f);
            // 상한을 넘어서도(음수 방지 포함) 계속 상한에 머물러야 한다
            Assert.AreEqual(Balance.SpecialtyDamagePercentCap, SpecialtyBonus.DamagePercent(9999), 0.0001f);
        }

        [Test]
        public void BeanGainMultiplier_GrowsThenClamps()
        {
            Assert.AreEqual(1.09f, SpecialtyBonus.BeanGainMultiplier(3), 0.0001f);
            Assert.AreEqual(1f + Balance.SpecialtyBeanBonusCap, SpecialtyBonus.BeanGainMultiplier(1000), 0.0001f);
        }

        [Test]
        public void NegativePoints_AreTreatedAsZero()
        {
            Assert.AreEqual(0f, SpecialtyBonus.DamagePercent(-5), 0.0001f);
            Assert.AreEqual(1f, SpecialtyBonus.BeanGainMultiplier(-5), 0.0001f);
        }
    }
}
