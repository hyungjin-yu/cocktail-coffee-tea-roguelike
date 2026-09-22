using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 스페셜티(영구 재화)로 직접 찍는 영구 강화 트리 노드 (기획서 11장). 커피 스킬트리(SkillData)와
    /// 구조는 같지만(갈래/단계/선행 노드), 이건 런이 끝나도 초기화되지 않고 세이브 파일에 남는다.
    /// </summary>
    [CreateAssetMenu(menuName = "CCT/Permanent Skill Data", fileName = "PermanentSkill_")]
    public class PermanentSkillData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public SkillBranch branch;
        /// <summary>브랜치 내 단계 (1이 뿌리). UI 배치에 쓴다.</summary>
        [Min(1)] public int tier = 1;
        /// <summary>선행 노드. 없으면 처음부터 구매 가능.</summary>
        public PermanentSkillData prerequisite;
        /// <summary>해금에 필요한 스페셜티 포인트.</summary>
        [Min(1)] public int cost = 3;
        public CardEffect[] effects;
        /// <summary>Resources 아이콘 경로. 없으면 PermanentSkillView가 아이콘 없이 텍스트만 보여준다.</summary>
        public string iconResourcePath;
    }

    /// <summary>영구 강화 기본 세트 (3갈래 × 2단계, 가안 수치). 1단계는 강화 보스 1마리(스페셜티 3)면
    /// 바로 찍을 수 있고, 2단계는 2마리(6) 분량 — 선행 노드도 필요.</summary>
    public static class DefaultPermanentSkills
    {
        public static List<PermanentSkillData> CreateRuntime()
        {
            var roast = Make("dark_roast_blend", "다크 로스트 블렌드", "영구 공격력 +10%", SkillBranch.Attack, 1, 3,
                null, new CardEffect(CardEffectType.DamagePercent, 0.10f));
            var crema = Make("perfect_crema", "완벽한 크레마", "영구 공격 속도 +10%", SkillBranch.Attack, 2, 6,
                roast, new CardEffect(CardEffectType.AttackSpeedPercent, 0.10f));

            var tumbler = Make("sturdy_tumbler", "튼튼한 텀블러", "영구 최대 체력 +30", SkillBranch.Survival, 1, 3,
                null, new CardEffect(CardEffectType.MaxHpFlat, 30f));
            var veteran = Make("veteran_instinct", "베테랑의 감각", "영구 이동 속도 +10%", SkillBranch.Survival, 2, 6,
                tumbler, new CardEffect(CardEffectType.MoveSpeedPercent, 0.10f));

            var cupper = Make("bean_cupper", "원두 감별사", "영구 원두 획득량 +20%", SkillBranch.Ingredient, 1, 3,
                null, new CardEffect(CardEffectType.BeanGainPercent, 0.20f));
            var roasting = Make("skilled_roasting", "숙련된 로스팅", "영구 재료 게이지 필요량 -10%",
                SkillBranch.Ingredient, 2, 6, cupper, new CardEffect(CardEffectType.GaugeNeedPercent, -0.10f));

            return new List<PermanentSkillData> { roast, crema, tumbler, veteran, cupper, roasting };
        }

        static PermanentSkillData Make(string id, string displayName, string description, SkillBranch branch,
            int tier, int cost, PermanentSkillData prerequisite, params CardEffect[] effects)
        {
            var skill = ScriptableObject.CreateInstance<PermanentSkillData>();
            skill.name = id;
            skill.id = id;
            skill.displayName = displayName;
            skill.description = description;
            skill.branch = branch;
            skill.tier = tier;
            skill.cost = cost;
            skill.prerequisite = prerequisite;
            skill.effects = effects;
            skill.iconResourcePath = "Icons/PermanentSkills/" + id;
            return skill;
        }
    }

    public static class PermanentSkillDatabase
    {
        public static List<PermanentSkillData> LoadAll()
        {
            var loaded = Resources.LoadAll<PermanentSkillData>("PermanentSkills");
            if (loaded != null && loaded.Length > 0)
                return new List<PermanentSkillData>(loaded);
            return DefaultPermanentSkills.CreateRuntime();
        }
    }
}
