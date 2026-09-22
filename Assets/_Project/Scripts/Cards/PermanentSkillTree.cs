using System.Collections.Generic;
using System.Linq;

namespace CCT
{
    /// <summary>
    /// 스페셜티로 직접 찍는 영구 강화 트리 상태. 커피 SkillTree와 달리 해금 여부가 세이브 파일에
    /// 영구히 남고(생성자로 이미 해금된 id 목록을 받는다), 해금에 스페셜티 포인트 비용이 든다.
    /// </summary>
    public class PermanentSkillTree
    {
        readonly List<PermanentSkillData> nodes;
        readonly HashSet<string> unlockedIds;

        public IReadOnlyList<PermanentSkillData> Nodes => nodes;
        public int UnlockedCount => unlockedIds.Count;

        /// <summary>지금까지 해금에 쓴 스페셜티 총량. "사용 가능 = 누적 획득 - SpentPoints"로 계산한다.</summary>
        public int SpentPoints => nodes.Where(n => unlockedIds.Contains(n.id)).Sum(n => n.cost);

        public PermanentSkillTree(IEnumerable<PermanentSkillData> nodes, IEnumerable<string> unlockedIds)
        {
            this.nodes = new List<PermanentSkillData>(nodes);
            this.unlockedIds = new HashSet<string>(unlockedIds ?? Enumerable.Empty<string>());
        }

        public bool IsUnlocked(PermanentSkillData node) => unlockedIds.Contains(node.id);

        public bool IsAvailable(PermanentSkillData node) =>
            !IsUnlocked(node) && (node.prerequisite == null || unlockedIds.Contains(node.prerequisite.id));

        /// <returns>구매 성공 여부. 이미 해금됐거나 선행 조건 미충족, 포인트 부족이면 false.</returns>
        public bool TryUnlock(PermanentSkillData node, int availablePoints)
        {
            if (!IsAvailable(node) || availablePoints < node.cost) return false;
            unlockedIds.Add(node.id);
            return true;
        }

        /// <summary>해금된 모든 노드의 효과를 PlayerStats에 적용한다 (런 시작 시 한 번 호출).</summary>
        public void ApplyAll(PlayerStats stats)
        {
            foreach (var node in nodes)
            {
                if (!IsUnlocked(node)) continue;
                foreach (var effect in node.effects) stats.Apply(effect);
            }
        }
    }
}
