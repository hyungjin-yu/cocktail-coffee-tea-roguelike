using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>06:23 — 같은 틱에 여러 장판이 겹쳐도 가장 높은 DPS 하나만 적용돼야 한다.</summary>
    public class PlayerZoneDamageTests
    {
        [Test]
        public void SameFrame_MultipleRequests_OnlyHighestSurvives()
        {
            PlayerZoneDamage.RequestTick(5f);
            PlayerZoneDamage.RequestTick(9f);
            PlayerZoneDamage.RequestTick(3f);

            Assert.AreEqual(9f, PlayerZoneDamage.ConsumeFrame(), 0.0001f);
        }

        [Test]
        public void ConsumeFrame_ClearsPendingState_SoASecondCallReturnsZero()
        {
            PlayerZoneDamage.RequestTick(4f);
            PlayerZoneDamage.ConsumeFrame();

            Assert.AreEqual(0f, PlayerZoneDamage.ConsumeFrame(), "이미 소비된 프레임은 다시 반환하면 안 된다");
        }

        [Test]
        public void NoRequests_ConsumeFrame_ReturnsZero()
        {
            Assert.AreEqual(0f, PlayerZoneDamage.ConsumeFrame());
        }
    }
}
