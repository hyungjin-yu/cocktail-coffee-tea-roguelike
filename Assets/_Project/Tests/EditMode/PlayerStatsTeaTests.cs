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

        [Test]
        public void AttackInterval_NeverGoesBelow_0_12Seconds()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.AttackSpeedPercent, 50f)); // 극단적으로 큰 공속 보너스
            Assert.AreEqual(0.12f, stats.AttackInterval, 0.0001f, "06:19 — max(0.12, ...) 하한");
        }

        [Test]
        public void MoveSpeed_CappedAt10_BeforeSlowMultiplier()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.MoveSpeedPercent, 5f)); // 극단적으로 큰 이속 보너스
            Assert.AreEqual(10f, stats.MoveSpeed, 0.0001f, "06:19 — min(10, ...) 상한");
        }

        [Test]
        public void DamageTakenMultiplier_ClampsDamageReductionTo0And0_25()
        {
            var stats = new PlayerStats();
            Assert.AreEqual(1f, stats.DamageTakenMultiplier, 0.0001f, "기본값은 감소 없음");

            stats.Apply(new CardEffect(CardEffectType.DamageReduction, 0.15f));
            Assert.AreEqual(0.85f, stats.DamageTakenMultiplier, 0.0001f);

            stats.Apply(new CardEffect(CardEffectType.DamageReduction, 1f)); // 합 1.15 → 0.25로 clamp
            Assert.AreEqual(0.75f, stats.DamageTakenMultiplier, 0.0001f, "07:19 — 0~0.25로 clamp");
        }

        [Test]
        public void PickupRadius_AddsFlatBonus()
        {
            var stats = new PlayerStats();
            float before = stats.PickupRadius;
            stats.Apply(new CardEffect(CardEffectType.PickupRadiusFlat, 1f));
            Assert.AreEqual(before + 1f, stats.PickupRadius, 0.0001f);
        }

        [Test]
        public void ExtraThrusts_And_PierceCount_AreCappedAtSpecMax()
        {
            var stats = new PlayerStats();
            for (int i = 0; i < 20; i++)
            {
                stats.Apply(new CardEffect(CardEffectType.ExtraThrust, 1f));
                stats.Apply(new CardEffect(CardEffectType.PierceCount, 1f));
            }
            Assert.AreEqual(PlayerStats.MaxExtraThrusts, stats.extraThrusts, "06:19 — 연속타/동시발사 합계 상한 7");
            Assert.AreEqual(PlayerStats.MaxPierceCount, stats.pierceCount, "06:19 — 관통 상한 6");
        }

        [Test]
        public void RegenPerSecond_NeverGoesNegative()
        {
            var stats = new PlayerStats();
            stats.regenBonus = -10f; // 이론상 음수 보너스가 들어와도
            Assert.AreEqual(0f, stats.RegenPerSecond, "07:18 — max(0, 0.5+regenBonus)");
        }

        [Test]
        public void LockFlame_TicksDown_ThenUnlocks()
        {
            var stats = new PlayerStats();
            Assert.IsFalse(stats.FlameLocked);

            stats.LockFlame(5f);
            Assert.IsTrue(stats.FlameLocked);

            stats.Tick(4.9f);
            Assert.IsTrue(stats.FlameLocked, "06:142 — 봉인 시간이 남아있으면 계속 잠겨 있어야 한다");

            stats.Tick(0.2f);
            Assert.IsFalse(stats.FlameLocked);
        }

        [Test]
        public void LockPoison_IsIndependentFromLockFlame()
        {
            var stats = new PlayerStats();
            stats.LockPoison(3f);

            Assert.IsTrue(stats.PoisonLocked);
            Assert.IsFalse(stats.FlameLocked, "화염과 독 봉인은 서로 독립적이어야 한다");
        }

        [Test]
        public void LockFlame_LongerDurationExtendsRemaining_ShorterDoesNotShorten()
        {
            var stats = new PlayerStats();
            stats.LockFlame(2f);
            stats.LockFlame(6f); // 더 긴 지속시간으로 갱신
            stats.Tick(5.9f);
            Assert.IsTrue(stats.FlameLocked, "더 긴 지속시간으로 갱신됐으니 아직 풀리면 안 된다");

            stats.LockFlame(0.5f); // 더 짧은 지속시간은 기존 남은 시간을 줄이면 안 된다
            Assert.IsTrue(stats.FlameLocked);
        }
    }
}
