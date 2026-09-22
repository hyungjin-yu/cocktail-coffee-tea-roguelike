using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    public enum SkillBranch { Attack, Survival, Ingredient }

    /// <summary>커피 스킬트리 노드. 카드와 달리 플레이어가 직접 골라 해금하는 결정론적 빌드.</summary>
    [CreateAssetMenu(menuName = "CCT/Skill Data", fileName = "Skill_")]
    public class SkillData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public SkillBranch branch;
        /// <summary>브랜치 내 단계 (1이 뿌리). UI 배치에 쓴다.</summary>
        [Min(1)] public int tier = 1;
        /// <summary>선행 노드. 없으면 처음부터 해금 가능.</summary>
        public SkillData prerequisite;
        public CardEffect[] effects;

        public static string BranchLabel(SkillBranch branch)
        {
            switch (branch)
            {
                case SkillBranch.Attack: return "공격";
                case SkillBranch.Survival: return "생존";
                default: return "재료";
            }
        }
    }

    /// <summary>MVP 스킬트리 기본 세트 (3갈래 × 2단계, 가안 수치).</summary>
    public static class DefaultSkills
    {
        public static List<SkillData> CreateRuntime()
        {
            var espresso = Make("espresso_shot", "에스프레소 샷", "공격력 +20%", SkillBranch.Attack, 1, null,
                new CardEffect(CardEffectType.DamagePercent, 0.20f));
            var ristretto = Make("ristretto", "리스트레토", "공격 속도 +25%", SkillBranch.Attack, 2, espresso,
                new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f));

            var caffeine = Make("caffeine_rush", "카페인 각성", "이동 속도 +12%", SkillBranch.Survival, 1, null,
                new CardEffect(CardEffectType.MoveSpeedPercent, 0.12f));
            var doppio = Make("doppio_body", "도피오 체력", "최대 체력 +50", SkillBranch.Survival, 2, caffeine,
                new CardEffect(CardEffectType.MaxHpFlat, 50f));

            var coldBrew = Make("cold_brew", "콜드브루", "재료 게이지 필요량 -10%", SkillBranch.Ingredient, 1, null,
                new CardEffect(CardEffectType.GaugeNeedPercent, -0.10f));
            var grinder = Make("fine_grind", "곱게 갈기", "재료 추가 드랍 확률 +15%", SkillBranch.Ingredient, 2, coldBrew,
                new CardEffect(CardEffectType.DropChancePercent, 0.15f));

            return new List<SkillData> { espresso, ristretto, caffeine, doppio, coldBrew, grinder };
        }

        static SkillData Make(string id, string displayName, string description, SkillBranch branch, int tier,
            SkillData prerequisite, params CardEffect[] effects)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.name = id;
            skill.id = id;
            skill.displayName = displayName;
            skill.description = description;
            skill.branch = branch;
            skill.tier = tier;
            skill.prerequisite = prerequisite;
            skill.effects = effects;
            return skill;
        }
    }

    public static class SkillDatabase
    {
        public static List<SkillData> LoadAll()
        {
            var loaded = Resources.LoadAll<SkillData>("Skills");
            if (loaded != null && loaded.Length > 0)
                return new List<SkillData>(loaded);
            return DefaultSkills.CreateRuntime();
        }
    }
}
