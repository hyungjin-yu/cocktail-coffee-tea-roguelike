using NUnit.Framework;

namespace CCT.Tests
{
    public class PlayerStatsTeaTests
    {
        [Test]
        public void RegenBonus_AccumulatesFromCards()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.RegenBonus, 1.2f));
            stats.Apply(new CardEffect(CardEffectType.RegenBonus, 3f));
            Assert.AreEqual(4.2f, stats.regenBonus, 0.0001f);
        }

        [Test]
        public void PoisonLevel_AccumulatesAsInteger()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.PoisonLevel, 1f));
            stats.Apply(new CardEffect(CardEffectType.PoisonLevel, 2f));
            Assert.AreEqual(3, stats.poisonLevel);
        }

        [Test]
        public void ApplySlow_ReducesMoveSpeed_ThenExpires()
        {
            var stats = new PlayerStats();
            float normalSpeed = stats.MoveSpeed;

            stats.ApplySlow(1f, 0.5f);
            Assert.IsTrue(stats.IsSlowed);
            Assert.AreEqual(normalSpeed * 0.5f, stats.MoveSpeed, 0.0001f);

            stats.Tick(0.9f);
            Assert.IsTrue(stats.IsSlowed);
            stats.Tick(0.2f);
            Assert.IsFalse(stats.IsSlowed);
            Assert.AreEqual(normalSpeed, stats.MoveSpeed, 0.0001f);
        }

        [Test]
        public void ApplySlow_StrongerEffectOverridesWeaker_ButWeakerNeverWeakensActiveEffect()
        {
            var stats = new PlayerStats();

            stats.ApplySlow(1f, 0.8f);
            stats.ApplySlow(0.5f, 0.4f); // 더 강한 배율 → 적용
            Assert.AreEqual(stats.baseMoveSpeed * 0.4f, stats.MoveSpeed, 0.0001f);

            stats.ApplySlow(0.3f, 0.9f); // 더 약한 배율 → 기존 0.4 유지
            Assert.AreEqual(stats.baseMoveSpeed * 0.4f, stats.MoveSpeed, 0.0001f);
        }

        [Test]
        public void ApplySlow_LongerDurationExtendsRemaining()
        {
            var stats = new PlayerStats();
            stats.ApplySlow(1f, 0.5f);
            stats.ApplySlow(3f, 0.5f);

            stats.Tick(2.5f);
            Assert.IsTrue(stats.IsSlowed, "더 긴 지속시간으로 갱신됐으니 아직 풀리면 안 된다");
        }
    }
}
