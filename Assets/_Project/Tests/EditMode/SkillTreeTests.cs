using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    public class SkillTreeTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SkillTree DefaultTree()
        {
            var nodes = DefaultSkills.CreateRuntime();
            created.AddRange(nodes);
            return new SkillTree(nodes);
        }

        SkillData Find(SkillTree tree, string id) => tree.Nodes.Single(n => n.id == id);

        [Test]
        public void DefaultTree_HasThreeBranchesOfFiveTiers()
        {
            // 07:122 — 완성판은 3갈래×5단계(15노드). 2026-09-25 이전엔 2단계(6노드)뿐이었음(G18).
            var tree = DefaultTree();
            Assert.AreEqual(15, tree.Nodes.Count);
            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
                Assert.AreEqual(5, tree.Nodes.Count(n => n.branch == branch), branch.ToString());
        }

        [Test]
        public void EachBranch_FormsASingleChainFromTier1To5()
        {
            var tree = DefaultTree();
            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
            {
                var chain = tree.Nodes.Where(n => n.branch == branch).OrderBy(n => n.tier).ToList();
                Assert.AreEqual(new[] { 1, 2, 3, 4, 5 }, chain.Select(n => n.tier).ToArray(), branch.ToString());
                Assert.IsNull(chain[0].prerequisite, "1단계는 선행이 없어야 한다");
                for (int i = 1; i < chain.Count; i++)
                    Assert.AreSame(chain[i - 1], chain[i].prerequisite,
                        $"{branch} {chain[i].id}의 선행은 바로 앞 단계여야 한다");
            }
        }

        [Test]
        public void OnlyRootNodes_AreAvailableAtStart()
        {
            var tree = DefaultTree();
            var available = tree.Nodes.Where(tree.IsAvailable).ToList();

            Assert.AreEqual(3, available.Count);
            Assert.IsTrue(available.All(n => n.tier == 1));
        }

        [Test]
        public void Unlock_ChildRequiresParent()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            var child = Find(tree, "ristretto");
            var parent = Find(tree, "espresso_shot");

            Assert.IsFalse(tree.Unlock(child, stats), "선행 노드 없이 해금되면 안 된다");
            Assert.AreEqual(0f, stats.attackSpeedPercent);

            Assert.IsTrue(tree.Unlock(parent, stats));
            Assert.IsTrue(tree.IsAvailable(child));
            Assert.IsTrue(tree.Unlock(child, stats));
            Assert.AreEqual(2, tree.UnlockedCount);
        }

        [Test]
        public void Unlock_AppliesEffects_AndCannotBeRepeated()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            var node = Find(tree, "espresso_shot");

            Assert.IsTrue(tree.Unlock(node, stats));
            Assert.AreEqual(0.2f, stats.damagePercent, 0.0001f);

            Assert.IsFalse(tree.Unlock(node, stats));
            Assert.AreEqual(0.2f, stats.damagePercent, 0.0001f, "중복 해금으로 효과가 두 번 적용되면 안 된다");
        }

        [Test]
        public void FullAttackChain_Unlock_AppliesAllFiveEffectsInOrder()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            foreach (var id in new[]
                     {
                         "espresso_shot", "ristretto", "balanced_extraction", "full_body_roast", "perfect_extraction",
                     })
                Assert.IsTrue(tree.Unlock(Find(tree, id), stats), id);

            Assert.AreEqual(0.20f + 0.25f, stats.damagePercent, 0.0001f, "공격력 두 단계(1,4) 합산");
            Assert.AreEqual(0.25f, stats.attackSpeedPercent, 0.0001f);
            Assert.AreEqual(0.15f, stats.rangePercent, 0.0001f);
            Assert.AreEqual(1, stats.extraThrusts);
        }

        [Test]
        public void SurvivalTier5_Unlock_AppliesDamageReduction()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            foreach (var id in new[] { "caffeine_rush", "doppio_body", "mellow_finish", "insulated_cup", "steady_hand" })
                Assert.IsTrue(tree.Unlock(Find(tree, id), stats), id);

            Assert.AreEqual(0.15f, stats.damageReduction, 0.0001f);
            Assert.AreEqual(0.85f, stats.DamageTakenMultiplier, 0.0001f);
        }

        [Test]
        public void IngredientTier5_ZeroWaste_AppliesBothEffects()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            foreach (var id in new[] { "cold_brew", "fine_grind", "wide_tray", "measured_pour", "zero_waste" })
                Assert.IsTrue(tree.Unlock(Find(tree, id), stats), id);

            Assert.AreEqual(0.15f + 0.20f, stats.dropChancePercent, 0.0001f, "재료2와 재료5 드랍확률 합산");
            Assert.AreEqual(1f + 0.5f, stats.pickupRadiusFlat, 0.0001f, "재료3과 재료5 줍기반경 합산");
        }

        [Test]
        public void AnyAvailable_BecomesFalse_WhenEverythingUnlocked()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();

            foreach (var node in tree.Nodes.OrderBy(n => n.tier))
                Assert.IsTrue(tree.Unlock(node, stats), node.id);

            Assert.IsFalse(tree.AnyAvailable());
        }
    }

    public class PlayerStatsSuppressTests
    {
        [Test]
        public void Suppress_DisablesOffensiveAndMoveBonuses_ThenRestores()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.DamagePercent, 0.5f));
            stats.Apply(new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f));
            stats.Apply(new CardEffect(CardEffectType.MoveSpeedPercent, 0.2f));
            stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 30f));

            Assert.AreEqual(stats.baseDamage * 1.5f, stats.Damage, 0.0001f);

            stats.SuppressBonuses(5f);
            Assert.IsTrue(stats.BonusesSuppressed);
            Assert.AreEqual(stats.baseDamage, stats.Damage, 0.0001f);
            Assert.AreEqual(stats.baseAttackInterval, stats.AttackInterval, 0.0001f);
            Assert.AreEqual(stats.baseMoveSpeed, stats.MoveSpeed, 0.0001f);
            Assert.AreEqual(130f, stats.MaxHp, 0.0001f, "최대 체력은 무효화 대상이 아니다");

            stats.Tick(4.9f);
            Assert.IsTrue(stats.BonusesSuppressed);
            stats.Tick(0.2f);
            Assert.IsFalse(stats.BonusesSuppressed);
            Assert.AreEqual(stats.baseDamage * 1.5f, stats.Damage, 0.0001f);
        }

        [Test]
        public void Suppress_DoesNotShortenExistingDuration()
        {
            var stats = new PlayerStats();
            stats.SuppressBonuses(5f);
            stats.SuppressBonuses(2f);
            Assert.AreEqual(5f, stats.SuppressedRemaining, 0.0001f);
        }

        [Test]
        public void AttackSpeedBonus_IsSuppressedDuringRefund_LikeDamageBonus()
        {
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.AttackSpeedPercent, 0.3f));
            Assert.AreEqual(0.3f, stats.AttackSpeedBonus, 0.0001f);

            stats.SuppressBonuses(3f);
            Assert.AreEqual(0f, stats.AttackSpeedBonus, 0.0001f, "07:119 — 환불 중엔 보조 활 간격 공식도 무효화돼야 한다");
        }

        [Test]
        public void HybridLock_BlocksOnlyHybrid_AndTicksDownOnlyWhilePlaying()
        {
            var stats = new PlayerStats();
            Assert.IsFalse(stats.HybridLocked);

            stats.LockHybrid(5f);
            Assert.IsTrue(stats.HybridLocked);
            Assert.IsFalse(stats.FlameLocked, "07:119 — 하이브리드 잠금은 화염/독을 건드리지 않아야 한다");

            stats.Tick(4.9f);
            Assert.IsTrue(stats.HybridLocked);
            stats.Tick(0.2f);
            Assert.IsFalse(stats.HybridLocked);
        }
    }
}
