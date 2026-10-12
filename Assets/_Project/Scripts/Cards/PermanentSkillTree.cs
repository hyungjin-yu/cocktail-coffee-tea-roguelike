using System.Collections.Generic;
using System.Linq;

namespace CCT
{
    /// <summary>
    /// 스페셜티로 직접 찍는 영구 강화 트리 상태. 커피 SkillTree와 달리 해금 여부가 세이브 파일에
    /// 영구히 남고(생성자로 이미 해금된 id 목록을 받는다), 해금에 스페셜티 포인트 비용이 든다.
    /// 08:169~171 — 기록된 id 중 "유효" 노드만 효과·비용에 포함한다: 알려진 id이고 선행이 있으면 선행도
    /// 유효해야 한다. 선행이 빠진 옛 기록(단계2만 있음)은 비활성으로 보존하고 선행을 무료로 만들지 않는다.
    /// </summary>
    public class PermanentSkillTree
    {
        readonly List<PermanentSkillData> nodes;
        readonly HashSet<string> unlockedIds;
        readonly Dictionary<string, PermanentSkillData> byId = new Dictionary<string, PermanentSkillData>();
        readonly List<string> dataErrors = new List<string>();
        readonly HashSet<PlayerStats> applied = new HashSet<PlayerStats>();
        readonly int clearedRuns;
        public IReadOnlyList<string> DataErrors => dataErrors;
        public bool HasDataErrors => dataErrors.Count > 0;

        public IReadOnlyList<PermanentSkillData> Nodes => nodes;
        /// <summary>유효(효과가 적용되는) 해금 노드 수.</summary>
        public int UnlockedCount => nodes.Count(IsUnlocked);

        /// <summary>유효 노드의 비용 합. "잔액 = 누적 획득 - SpentPoints"(07:151).</summary>
        public int SpentPoints => SpentFor(unlockedIds);

        /// <summary>기록돼 있지만 선행이 빠져 비활성인 노드가 있는지(화면 "이전 데이터의 선행 강화 확인 필요").</summary>
        public bool HasInactiveLegacyNodes => nodes.Any(IsInactiveLegacy);

        public PermanentSkillTree(IEnumerable<PermanentSkillData> nodes, IEnumerable<string> unlockedIds, int clearedRuns = 0)
        {
            this.nodes = new List<PermanentSkillData>(nodes);
            this.unlockedIds = new HashSet<string>(unlockedIds ?? Enumerable.Empty<string>());
            this.clearedRuns = System.Math.Max(0, clearedRuns);
            foreach (var n in this.nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id) || byId.ContainsKey(n.id))
                { dataErrors.Add("영구 강화 ID 누락 또는 중복"); continue; }
                byId.Add(n.id, n);
            }
            foreach (var n in this.nodes)
                if (n != null && !ValidGraph(n, new HashSet<string>()))
                    dataErrors.Add($"영구 강화 선행 누락/순환 또는 데이터 오류: {n.id}");
        }

        /// <summary>유효 해금(효과 적용) 여부.</summary>
        public bool IsUnlocked(PermanentSkillData node) => node != null && byId.TryGetValue(node.id, out var canonical) &&
            IsEffective(canonical, unlockedIds, new HashSet<string>());

        /// <summary>세이브에 기록은 있으나 선행 미보유로 비활성인 옛 노드.</summary>
        public bool IsInactiveLegacy(PermanentSkillData node) =>
            node != null && unlockedIds.Contains(node.id) && !IsUnlocked(node);

        public bool IsAvailable(PermanentSkillData node) =>
            !HasDataErrors && IsCanonical(node) && !unlockedIds.Contains(node.id) &&
            clearedRuns >= node.minimumClears && node.Prerequisites.All(IsUnlocked);

        public bool PrerequisitesMet(PermanentSkillData node) => IsCanonical(node) && node.Prerequisites.All(IsUnlocked);
        public bool ClearsMet(PermanentSkillData node) => node != null && clearedRuns >= node.minimumClears;
        public int AppliedCount(WeaponKind startingWeapon) => nodes.Count(n => IsUnlocked(n) && n.AppliesTo(startingWeapon));
        public int BalanceAfter(PermanentSkillData node, int totalEarned)
        {
            if (!IsCanonical(node)) return totalEarned - SpentPoints;
            return totalEarned - SpentFor(new HashSet<string>(unlockedIds) { node.id });
        }

        /// <returns>구매 성공 여부. 이미 기록됐거나 선행 미충족, 포인트 부족이면 false. 선행을 사서 비활성 옛 노드가
        /// 다시 유효해지면 그 비용도 잔액에서 빠지므로 구매 후 잔액이 음수가 되는 구매는 거부한다(07:163).</returns>
        public bool TryUnlock(PermanentSkillData node, int availablePoints)
        {
            if (!IsAvailable(node)) return false;
            var after = new HashSet<string>(unlockedIds) { node.id };
            int extra = SpentFor(after) - SpentPoints;
            if (availablePoints < node.cost || availablePoints < extra) return false;
            unlockedIds.Add(node.id);
            return true;
        }

        /// <summary>유효 노드의 효과만 PlayerStats에 적용한다 (런 시작 시 한 번 호출).</summary>
        public void ApplyAll(PlayerStats stats, WeaponKind startingWeapon = WeaponKind.Rapier)
        {
            if (stats == null || !applied.Add(stats)) return;
            foreach (var node in nodes)
            {
                if (!IsUnlocked(node) || !node.AppliesTo(startingWeapon)) continue;
                foreach (var effect in node.effects) stats.Apply(effect);
            }
        }

        int SpentFor(HashSet<string> ids) => nodes.Where(n => IsEffective(n, ids, new HashSet<string>())).Sum(n => n.cost);

        bool IsCanonical(PermanentSkillData node) => node != null && !string.IsNullOrEmpty(node.id) &&
            byId.TryGetValue(node.id, out var canonical) && ReferenceEquals(node, canonical);

        bool ValidGraph(PermanentSkillData node, HashSet<string> path)
        {
            if (!IsCanonical(node) || node.cost < 1 || node.minimumClears < 0 || node.effects == null ||
                (node.additionalPrerequisites != null && node.additionalPrerequisites.Any(n => n == null)) ||
                !path.Add(node.id)) return false;
            foreach (var parent in node.Prerequisites)
                if (!ValidGraph(parent, path)) { path.Remove(node.id); return false; }
            path.Remove(node.id);
            return true;
        }

        bool IsEffective(PermanentSkillData node, HashSet<string> ids, HashSet<string> path)
        {
            if (!IsCanonical(node) || !ids.Contains(node.id) || !path.Add(node.id)) return false;
            bool valid = node.cost > 0 && node.effects != null &&
                (node.additionalPrerequisites == null || node.additionalPrerequisites.All(n => n != null)) &&
                node.Prerequisites.All(n => IsEffective(n, ids, path));
            path.Remove(node.id);
            return valid;
        }
    }
}
