using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-22 주인 결정 2차: 혼합 러시 규칙(지속시간 없음·중복 카드로 꺼짐·이동/공속 +40%)과 공용 디버프 몹(일정·수치·디버프 효과).</summary>
    public class PlayFeedback1122RushDebuffTests
    {
        [Test]
        public void DebuffSchedule_From120s_Every30s_Alternates_SharedNeutral_NoDrops()
        {
            Assert.AreEqual(0, DebuffMobSchedule.DueCount(119.9f));
            Assert.AreEqual(1, DebuffMobSchedule.DueCount(120f));
            Assert.AreEqual(1, DebuffMobSchedule.DueCount(149.9f));
            Assert.AreEqual(2, DebuffMobSchedule.DueCount(150f));
            Assert.AreSame(EnemyDef.BitterMist, DebuffMobSchedule.DefFor(0));
            Assert.AreSame(EnemyDef.GlassThief, DebuffMobSchedule.DefFor(1));
            Assert.AreSame(EnemyDef.BitterMist, DebuffMobSchedule.DefFor(2));
            foreach (var d in new[] { EnemyDef.BitterMist, EnemyDef.GlassThief })
            {
                Assert.AreEqual(MonsterFamily.Neutral, MonsterFamilies.Of(d), d.name + " 공용(중립)");
                Assert.IsTrue(MonsterFamilies.IsClassified(d));
                Assert.AreEqual(0, d.dropCount, d.name + " 재료 드랍 없음(경제 불변)");
                Assert.LessOrEqual(d.hp, 45f, d.name + " 낮은 체력(처치 우선순위)");
                Assert.AreNotEqual(Color.white, d.iconTint, d.name + " 임시 아트 = 기존 그림 색만 바꿈");
                Assert.IsNotNull(Resources.Load<Sprite>(d.spriteResourcePath), d.name + " 기존 그림 경로");
                Assert.IsFalse(d.IsBoss);
            }
            Assert.Greater(EnemyDef.GlassThief.contactHealBlockSeconds, 0f);
            Assert.Less(EnemyDef.GlassThief.contactAttackSlowMultiplier, 1f);
        }

        [Test]
        public void PlayerStats_AttackSlowAndHealBlock_ApplyAndExpire()
        {
            var s = new PlayerStats();
            float interval = s.AttackInterval, regen = s.RegenPerSecond;
            s.ApplyAttackSlow(3f, 0.75f);
            Assert.IsTrue(s.AttackSlowed);
            Assert.AreEqual(interval / 0.75f, s.AttackInterval, 1e-4f, "공격 간격 늘어남");
            s.ApplyAttackSlow(1f, 0.9f);
            Assert.AreEqual(0.75f, s.AttackSlowMultiplier, 1e-6f, "약한 효과로 덮어쓰지 않음");
            s.BlockHeal(5f);
            Assert.AreEqual(0f, s.RegenPerSecond, "회복 차단");
            s.Tick(3.1f);
            Assert.IsFalse(s.AttackSlowed);
            Assert.AreEqual(interval, s.AttackInterval, 1e-4f);
            Assert.IsTrue(s.HealBlocked);
            s.Tick(2f);
            Assert.AreEqual(regen, s.RegenPerSecond, 1e-5f, "차단 끝나면 회복");
        }

        [Test]
        public void Advisor_WarnsDuplicateWhenRushActive_NewKindKeepsRush()
        {
            var cards = CardDatabase.LoadAll();
            var gin = cards.Find(c => c.id == "dry_gin");
            var shot = cards.Find(c => c.id == "shot_glass");
            var run = new FakeRun(); run.stacks["dry_gin"] = 1;
            var ctx = new CardAdviceContext { job = Job.Bartender, weapon = WeaponKind.Rapier, run = run, cocktailKinds = 3, mixingRushActive = true };
            var adv = CardAdvisor.Evaluate(new System.Collections.Generic.List<CardOffer> { new CardOffer { card = gin, currentStacks = 1 }, new CardOffer { card = shot } }, ctx);
            StringAssert.Contains("혼합 러시 꺼짐", adv[0].reason);
            StringAssert.Contains("러시 유지", adv[1].reason);
            StringAssert.DoesNotContain("발동", adv[1].reason, "켜져 있으면 다시 발동하지 않음");
            Assert.AreEqual(0.40f, GameManager.MixingRushSpeedBonus, 1e-6f);
            StringAssert.Contains("이동·공속 +40%", JobPreset.Bartender.description);
        }

        class FakeRun : IHiddenRunState
        {
            public readonly System.Collections.Generic.Dictionary<string, int> stacks = new System.Collections.Generic.Dictionary<string, int>();
            public int Stacks(string id) => stacks.TryGetValue(id, out var n) ? n : 0;
            public bool Coffee(string id) => false;
            public bool Hybrid => false;
            public int FinalFormCount => 0;
            public bool HasFinalForm(AttackFinalForm f) => false;
            public Job Job => Job.Bartender;
        }
    }
}
