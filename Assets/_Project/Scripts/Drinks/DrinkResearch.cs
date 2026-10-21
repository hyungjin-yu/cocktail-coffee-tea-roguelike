using System;
using System.Collections.Generic;

namespace CCT
{
    /// <summary>72 §4 직업 숙련 트리 노드(3가지 × 4단계). 해금(영구, RP)과 장착(출발 전 3칸, 무료 교체)은 별개.</summary>
    public sealed class MasteryTrait
    {
        public string id, branch, name, effect;
        public int tier, cost;
        public string Prerequisite => tier <= 1 ? null : DrinkResearch.TraitAt(branch, tier - 1)?.id;
    }

    /// <summary>72 §5 직원(노트북 채용 사이트). 채용 = 보유 연구 1개 영구 확보. 제조 전문은 한 번에 하나만 활성.</summary>
    public sealed class ResearchStaff
    {
        public string id, name, area, research, effect;
        /// <summary>"cocktail"/"tea"(제조 누적 조건 종류), 0이면 조건 없음.</summary>
        public int craftsRequired;
        public int cost;
        /// <summary>전문 적용 대상: 기법(null = 종류 전체), 종류, 효과 항목, 배율.</summary>
        public string technique, kind, stat;
        public double multiplier = 1.08;
        public string portrait;
    }

    /// <summary>연구·숙련·판매 상태(drinks.json 안에 저장되는 순수 자료). JsonUtility 호환(필드·List).</summary>
    [Serializable]
    public class ResearchState
    {
        public int researchPoints;
        public bool firstGrantDone;
        public List<string> unlockedTraits = new List<string>();
        public List<string> equippedTraits = new List<string>();
        public List<string> hiredStaff = new List<string>();
        public string activeSpecialty = "";
        public int cocktailCrafts, teaCrafts, coffeeCrafts;
        public List<string> settledRpRunIds = new List<string>();
    }

    /// <summary>72 §4·§5 정의와 상태 변경(모두 사본 → 검사 → 반영; 실패는 상태 불변 + 이유).</summary>
    public static class DrinkResearch
    {
        public const int Slots = 3;
        public static readonly int[] TierCost = { 4, 6, 8, 10 };
        public const string Service = "service", Stability = "stability", Finesse = "finesse";

        public static readonly MasteryTrait[] Traits =
        {
            T(Service, 1, "service_1", "첫 잔의 기세", "런 시작 30초 동안 피해 +8%"),
            T(Service, 2, "service_2", "단골 응대", "같은 적에게 주무기 직접 적중 3회째 피해 +15% (대상 변경·2초 미적중 시 초기화)"),
            T(Service, 3, "service_3", "시그니처 준비", "출발 전 고른 목표 조합을 런 중 완성하면 30초 동안 피해 +10% (1회)"),
            T(Service, 4, "service_4", "마감 집중", "최종 보스가 살아 있는 동안 주무기 공격 속도 +5%"),
            T(Stability, 1, "stability_1", "여유로운 서비스", "피해 없이 8초 유지 시 이동 속도 +4% (피격하면 해제)"),
            T(Stability, 2, "stability_2", "마지막 한 모금", "체력 25% 이하 첫 진입 시 4초 동안 받는 피해 -10%p (런당 1회)"),
            T(Stability, 3, "stability_3", "쉬는 시간", "보스 처치 후 최대 체력 3% 회복 (보스별 1회)"),
            T(Stability, 4, "stability_4", "침착한 복귀", "체력 30% 이하에서 이동 속도 +6%"),
            T(Finesse, 1, "finesse_1", "첫 타의 정확성", "체력이 가득 찬 적에 대한 첫 주무기 직접 적중 피해 +8%"),
            T(Finesse, 2, "finesse_2", "거리 조절", "주무기 유효 사거리 70% 이상 거리의 직접 적중 피해 +5%"),
            T(Finesse, 3, "finesse_3", "다채로운 서비스", "서로 다른 칵테일·차 카드 4종 이상 보유 시 주무기 치명타 확률 +1%p"),
            T(Finesse, 4, "finesse_4", "한 잔의 집중", "출발 음료의 양수 전투 버프 ×1.05 (항목별 상한 유지)"),
        };

        public static readonly ResearchStaff[] Staff =
        {
            new ResearchStaff { id = "staff_cocktail_aroma", name = "향미 분석가", area = "cocktail", craftsRequired = 0, cost = 6,
                research = "스터링 전문", effect = "스터링 음료의 공격력 버프 ×1.08", technique = "Stir", kind = "Cocktail", stat = "Attack", portrait = "staff_cocktail_aroma" },
            new ResearchStaff { id = "staff_cocktail_blend", name = "배합 기술자", area = "cocktail", craftsRequired = 3, cost = 10,
                research = "셰이킹 전문", effect = "셰이킹 음료의 최대 체력 버프 ×1.08", technique = "Shake", kind = "Cocktail", stat = "MaxHp", portrait = "staff_cocktail_blend" },
            new ResearchStaff { id = "staff_tea_aroma", name = "향 평가사", area = "tea", craftsRequired = 0, cost = 6,
                research = "맑은 우림", effect = "차 음료의 공격력 버프 ×1.08", technique = null, kind = "Tea", stat = "Attack", portrait = "staff_tea_aroma" },
            new ResearchStaff { id = "staff_tea_blend", name = "블렌딩 연구원", area = "tea", craftsRequired = 3, cost = 10,
                research = "포근한 우림", effect = "차 음료의 최대 체력 버프 ×1.08", technique = null, kind = "Tea", stat = "MaxHp", portrait = "staff_tea_blend" },
        };

        static MasteryTrait T(string branch, int tier, string id, string name, string effect) =>
            new MasteryTrait { branch = branch, tier = tier, id = id, name = name, effect = effect, cost = TierCost[tier - 1] };

        public static MasteryTrait Trait(string id) { foreach (var t in Traits) if (t.id == id) return t; return null; }
        public static MasteryTrait TraitAt(string branch, int tier) { foreach (var t in Traits) if (t.branch == branch && t.tier == tier) return t; return null; }
        public static ResearchStaff StaffById(string id) { foreach (var s in Staff) if (s.id == id) return s; return null; }
        public static string BranchName(string b) => b == Service ? "서비스" : b == Stability ? "안정" : "기교";

        public enum NodeState { Locked, Available, Owned, Equipped }

        public static NodeState StateOf(ResearchState s, MasteryTrait t)
        {
            if (s.equippedTraits.Contains(t.id)) return NodeState.Equipped;
            if (s.unlockedTraits.Contains(t.id)) return NodeState.Owned;
            var pre = t.Prerequisite;
            return pre == null || s.unlockedTraits.Contains(pre) ? NodeState.Available : NodeState.Locked;
        }

        public static bool CanUnlock(ResearchState s, string id, out string why)
        {
            var t = Trait(id);
            why = null;
            if (t == null) { why = "알 수 없는 특성입니다."; return false; }
            if (s.unlockedTraits.Contains(id)) { why = "이미 해금했습니다."; return false; }
            var pre = t.Prerequisite;
            if (pre != null && !s.unlockedTraits.Contains(pre)) { why = $"먼저 '{Trait(pre).name}'을(를) 해금하세요."; return false; }
            if (s.researchPoints < t.cost) { why = $"연구 포인트가 {t.cost - s.researchPoints} 부족합니다."; return false; }
            return true;
        }

        public static bool Unlock(ResearchState s, string id, out string why)
        {
            if (!CanUnlock(s, id, out why)) return false;
            s.researchPoints -= Trait(id).cost;
            s.unlockedTraits.Add(id);
            return true;
        }

        /// <summary>장착: 해금한 특성만, 3칸, 같은 가지 여러 개 가능. 이미 장착이면 그대로 성공.</summary>
        public static bool Equip(ResearchState s, string id, out string why)
        {
            why = null;
            if (!s.unlockedTraits.Contains(id)) { why = "해금한 특성만 장착할 수 있습니다."; return false; }
            if (s.equippedTraits.Contains(id)) return true;
            if (s.equippedTraits.Count >= Slots) { why = $"슬롯 {Slots}칸이 모두 찼습니다. 하나를 해제하세요."; return false; }
            s.equippedTraits.Add(id);
            return true;
        }

        public static bool Unequip(ResearchState s, string id) => s.equippedTraits.Remove(id);

        public static int CraftsOf(ResearchState s, string area) => area == "tea" ? s.teaCrafts : area == "coffee" ? s.coffeeCrafts : s.cocktailCrafts;

        public static bool CanHire(ResearchState s, string id, out string why)
        {
            var st = StaffById(id);
            why = null;
            if (st == null) { why = "알 수 없는 직원입니다."; return false; }
            if (s.hiredStaff.Contains(id)) { why = "이미 채용했습니다."; return false; }
            if (CraftsOf(s, st.area) < st.craftsRequired) { why = $"{(st.area == "tea" ? "차" : "칵테일")} 제조 {st.craftsRequired}회가 필요합니다(현재 {CraftsOf(s, st.area)}회)."; return false; }
            if (s.researchPoints < st.cost) { why = $"연구 포인트가 {st.cost - s.researchPoints} 부족합니다."; return false; }
            return true;
        }

        public static bool Hire(ResearchState s, string id, out string why)
        {
            if (!CanHire(s, id, out why)) return false;
            s.researchPoints -= StaffById(id).cost;
            s.hiredStaff.Add(id);
            return true;
        }

        /// <summary>제조 전문 활성(무료 교체, 한 번에 하나). null/""이면 해제.</summary>
        public static bool SetSpecialty(ResearchState s, string id, out string why)
        {
            why = null;
            if (string.IsNullOrEmpty(id)) { s.activeSpecialty = ""; return true; }
            if (!s.hiredStaff.Contains(id)) { why = "채용한 직원의 연구만 활성화할 수 있습니다."; return false; }
            s.activeSpecialty = id;
            return true;
        }

        /// <summary>RP 첫 지급(새/기존 저장 1회). 이미 지급했으면 false.</summary>
        public static bool GrantFirst(ResearchState s)
        {
            if (s.firstGrantDone) return false;
            s.firstGrantDone = true;
            s.researchPoints += DrinkSupply.FirstGrantRp;
            return true;
        }

        /// <summary>런 종료 RP 정산(같은 runId 1회).</summary>
        public static int SettleRun(ResearchState s, string runId, int effectiveKills, int midBossKills, int finalBossKills)
        {
            if (string.IsNullOrEmpty(runId) || s.settledRpRunIds.Contains(runId)) return 0;
            int rp = DrinkSupply.RunRp(effectiveKills, midBossKills, finalBossKills);
            s.researchPoints += rp;
            s.settledRpRunIds.Add(runId);
            if (s.settledRpRunIds.Count > 200) s.settledRpRunIds.RemoveRange(0, s.settledRpRunIds.Count - 200);
            return rp;
        }

        /// <summary>저장 자료 정리: 알 수 없는 해금/채용 ID는 보존만 하고 효과·비용 계산에서 무시, 장착은 알려진 해금 특성만 3칸, 중복·음수 제거. 손상 저장에서도 안전.</summary>
        public static void Sanitize(ResearchState s)
        {
            if (s.unlockedTraits == null) s.unlockedTraits = new List<string>();
            if (s.equippedTraits == null) s.equippedTraits = new List<string>();
            if (s.hiredStaff == null) s.hiredStaff = new List<string>();
            if (s.settledRpRunIds == null) s.settledRpRunIds = new List<string>();
            if (s.activeSpecialty == null) s.activeSpecialty = "";
            s.unlockedTraits = Distinct(s.unlockedTraits);
            s.hiredStaff = Distinct(s.hiredStaff);
            s.equippedTraits.RemoveAll(id => Trait(id) == null || !s.unlockedTraits.Contains(id));
            s.equippedTraits = Distinct(s.equippedTraits);
            if (s.equippedTraits.Count > Slots) s.equippedTraits.RemoveRange(Slots, s.equippedTraits.Count - Slots);
            if (s.activeSpecialty != "" && (StaffById(s.activeSpecialty) == null || !s.hiredStaff.Contains(s.activeSpecialty))) s.activeSpecialty = "";
            if (s.researchPoints < 0) s.researchPoints = 0;
            if (s.cocktailCrafts < 0) s.cocktailCrafts = 0;
            if (s.teaCrafts < 0) s.teaCrafts = 0;
            if (s.coffeeCrafts < 0) s.coffeeCrafts = 0;
        }

        static List<string> Distinct(List<string> list)
        {
            var seen = new HashSet<string>();
            var r = new List<string>();
            foreach (var x in list) if (!string.IsNullOrEmpty(x) && seen.Add(x)) r.Add(x);
            return r;
        }
    }
}
