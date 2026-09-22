using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    public class PermanentSkillTreeTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        List<PermanentSkillData> DefaultNodes()
        {
            var nodes = DefaultPermanentSkills.CreateRuntime();
            created.AddRange(nodes);
            return nodes;
        }

        PermanentSkillData Find(List<PermanentSkillData> nodes, string id) => nodes.Single(n => n.id == id);

        [Test]
        public void DefaultSet_HasThreeBranchesOfTwoTiers()
        {
            var nodes = DefaultNodes();
            Assert.AreEqual(6, nodes.Count);
            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
                Assert.AreEqual(2, nodes.Count(n => n.branch == branch), branch.ToString());
        }

        [Test]
        public void FreshTree_OnlyTier1NodesAreAvailable()
        {
            var nodes = DefaultNodes();
            var tree = new PermanentSkillTree(nodes, null);

            var available = nodes.Where(tree.IsAvailable).ToList();
            Assert.AreEqual(3, available.Count);
            Assert.IsTrue(available.All(n => n.tier == 1));
        }

        [Test]
        public void TryUnlock_FailsWithoutEnoughPoints()
        {
            var nodes = DefaultNodes();
            var tree = new PermanentSkillTree(nodes, null);
            var cupper = Find(nodes, "bean_cupper"); // cost 3

            Assert.IsFalse(tree.TryUnlock(cupper, 2));
            Assert.IsFalse(tree.IsUnlocked(cupper));
            Assert.AreEqual(0, tree.SpentPoints);
        }

        [Test]
        public void TryUnlock_FailsWithoutPrerequisite_EvenWithEnoughPoints()
        {
            var nodes = DefaultNodes();
            var tree = new PermanentSkillTree(nodes, null);
            var roasting = Find(nodes, "skilled_roasting"); // tier 2, cupper가 선행

            Assert.IsFalse(tree.TryUnlock(roasting, 999));
        }

        [Test]
        public void TryUnlock_Succeeds_ThenUnlocksTier2AfterPrerequisite()
        {
            var nodes = DefaultNodes();
            var tree = new PermanentSkillTree(nodes, null);
            var cupper = Find(nodes, "bean_cupper");
            var roasting = Find(nodes, "skilled_roasting");

            Assert.IsTrue(tree.TryUnlock(cupper, 3));
            Assert.AreEqual(3, tree.SpentPoints);
            Assert.IsTrue(tree.IsAvailable(roasting));

            Assert.IsTrue(tree.TryUnlock(roasting, 6));
            Assert.AreEqual(9, tree.SpentPoints);
            Assert.AreEqual(2, tree.UnlockedCount);
        }

        [Test]
        public void TryUnlock_CannotRepeatAlreadyUnlockedNode()
        {
            var nodes = DefaultNodes();
            var tree = new PermanentSkillTree(nodes, null);
            var cupper = Find(nodes, "bean_cupper");

            Assert.IsTrue(tree.TryUnlock(cupper, 3));
            Assert.IsFalse(tree.TryUnlock(cupper, 3), "이미 해금된 노드는 다시 찍을 수 없어야 한다");
            Assert.AreEqual(3, tree.SpentPoints, "중복 구매로 비용이 두 번 빠지면 안 된다");
        }

        [Test]
        public void Constructor_SeedsAlreadyUnlockedIds_FromSave()
        {
            var nodes = DefaultNodes();
            var cupper = Find(nodes, "bean_cupper");
            var tree = new PermanentSkillTree(nodes, new[] { cupper.id });

            Assert.IsTrue(tree.IsUnlocked(cupper));
            Assert.AreEqual(3, tree.SpentPoints, "이미 해금된 걸로 시작해도 비용은 계산에 잡혀야 한다");
        }

        [Test]
        public void ApplyAll_OnlyAppliesUnlockedNodesEffects()
        {
            var nodes = DefaultNodes();
            var cupper = Find(nodes, "bean_cupper");
            var tree = new PermanentSkillTree(nodes, new[] { cupper.id });
            var stats = new PlayerStats();

            tree.ApplyAll(stats);

            Assert.AreEqual(0.20f, stats.beanGainPercent, 0.0001f, "해금된 노드(원두 감별사)의 효과만 적용돼야 한다");
            Assert.AreEqual(0f, stats.damagePercent, 0.0001f, "해금하지 않은 노드의 효과는 적용되면 안 된다");
        }
    }
}
