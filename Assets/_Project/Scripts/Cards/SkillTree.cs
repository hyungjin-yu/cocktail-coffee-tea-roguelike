using System.Collections.Generic;

namespace CCT
{
    /// <summary>08:89 커피 트리 노드 상태(해금/가능/선행필요/포인트부족).</summary>
    public enum CoffeeNodeState { Unlocked, Available, NeedsPrerequisite, NotEnoughPoints }

    /// <summary>커피 스킬트리 상태. 해금 가능 여부 판단과 효과 적용을 담당한다.</summary>
    public class SkillTree
    {
        /// <summary>노드 비용은 항상 1포인트(07:124).</summary>
        public const int NodeCost = 1;

        public CoffeeNodeState StateOf(SkillData node, int unspentPoints)
        {
            if (unlocked.Contains(node)) return CoffeeNodeState.Unlocked;
            if (!IsAvailable(node)) return CoffeeNodeState.NeedsPrerequisite;
            return unspentPoints >= NodeCost ? CoffeeNodeState.Available : CoffeeNodeState.NotEnoughPoints;
        }

        readonly List<SkillData> nodes;
        readonly HashSet<SkillData> unlocked = new HashSet<SkillData>();

        public IReadOnlyList<SkillData> Nodes => nodes;
        public int UnlockedCount => unlocked.Count;

        public SkillTree(IEnumerable<SkillData> nodes)
        {
            this.nodes = new List<SkillData>(nodes);
        }

        public bool IsUnlocked(SkillData node) => unlocked.Contains(node);

        public bool IsAvailable(SkillData node) =>
            !unlocked.Contains(node) && (node.prerequisite == null || unlocked.Contains(node.prerequisite));

        public bool AnyAvailable()
        {
            foreach (var node in nodes)
                if (IsAvailable(node)) return true;
            return false;
        }

        /// <returns>해금에 성공하면 true. 이미 해금됐거나 선행 노드가 없으면 false.</returns>
        public bool Unlock(SkillData node, PlayerStats stats)
        {
            if (!IsAvailable(node)) return false;

            unlocked.Add(node);
            if (node.effects != null)
                foreach (var effect in node.effects)
                    stats.Apply(effect);
            return true;
        }
    }
}
