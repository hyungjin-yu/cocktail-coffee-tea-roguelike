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
        public void DefaultTree_HasThreeBranchesOfTwoTiers()
        {
            var tree = DefaultTree();
            Assert.AreEqual(6, tree.Nodes.Count);
            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
                Assert.AreEqual(2, tree.Nodes.Count(n => n.branch == branch), branch.ToString());
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
    }
}
