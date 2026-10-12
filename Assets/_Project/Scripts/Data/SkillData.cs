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

    /// <summary>완성판 스킬트리 (07:122 — 3갈래 × 5단계, 확정 데이터). 1~2단계는 MVP 때 만든 그대로
    /// 유지(이미 구운 에셋과 ID가 같아야 SkillDatabase가 같은 노드로 인식한다), 3~5단계를 2026-09-25에
    /// 추가했다.</summary>
    public static class DefaultSkills
    {
        public static List<SkillData> CreateRuntime()
        {
            var espresso = Make("espresso_shot", "에스프레소 샷", "공격력 +20%", SkillBranch.Attack, 1, null,
                new CardEffect(CardEffectType.DamagePercent, 0.20f));
            var ristretto = Make("ristretto", "리스트레토", "공격 속도 +25%", SkillBranch.Attack, 2, espresso,
                new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f));
            var balanced = Make("balanced_extraction", "균형 추출", "사거리 +15%", SkillBranch.Attack, 3, ristretto,
                new CardEffect(CardEffectType.RangePercent, 0.15f));
            var fullBody = Make("full_body_roast", "풀바디 로스트", "공격력 +25%", SkillBranch.Attack, 4, balanced,
                new CardEffect(CardEffectType.DamagePercent, 0.25f));
            // 07:133 "연속타/동시발사+1 (시작무기군에 맞게 설명)" — 근접/원거리 둘 다 같은 stats.extraThrusts를
            // 쓰므로 효과는 하나. 무기별로 다른 문구를 보여주려면 SkillTreeView가 현재 무기를 알아야 하는데
            // 이번 범위에서는 그 배선을 새로 만들지 않고, 두 경우를 한 문장에 같이 적는 쪽을 택했다.
            var perfect = Make("perfect_extraction", "완전 추출", "연속타/동시발사 +1", SkillBranch.Attack, 5, fullBody,
                new CardEffect(CardEffectType.ExtraThrust, 1f));

            var caffeine = Make("caffeine_rush", "카페인 각성", "이동 속도 +12%", SkillBranch.Survival, 1, null,
                new CardEffect(CardEffectType.MoveSpeedPercent, 0.12f));
            var doppio = Make("doppio_body", "도피오 체력", "최대 체력 +50", SkillBranch.Survival, 2, caffeine,
                new CardEffect(CardEffectType.MaxHpFlat, 50f));
            var mellow = Make("mellow_finish", "부드러운 여운", "초당 회복량 +1", SkillBranch.Survival, 3, doppio,
                new CardEffect(CardEffectType.RegenBonus, 1f));
            var insulated = Make("insulated_cup", "보온 컵", "최대 체력 +30", SkillBranch.Survival, 4, mellow,
                new CardEffect(CardEffectType.MaxHpFlat, 30f));
            var steady = Make("steady_hand", "흔들림 없는 손", "받은 피해 -15%", SkillBranch.Survival, 5, insulated,
                new CardEffect(CardEffectType.DamageReduction, 0.15f));

            var coldBrew = Make("cold_brew", "콜드브루", "재료 게이지 필요량 -10%", SkillBranch.Ingredient, 1, null,
                new CardEffect(CardEffectType.GaugeNeedPercent, -0.10f));
            var grinder = Make("fine_grind", "곱게 갈기", "재료 추가 드랍 확률 +15%", SkillBranch.Ingredient, 2, coldBrew,
                new CardEffect(CardEffectType.DropChancePercent, 0.15f));
            var wideTray = Make("wide_tray", "넓은 트레이", "줍기 반경 +1", SkillBranch.Ingredient, 3, grinder,
                new CardEffect(CardEffectType.PickupRadiusFlat, 1f));
            var measured = Make("measured_pour", "정량 따르기", "재료 게이지 필요량 -10%", SkillBranch.Ingredient, 4, wideTray,
                new CardEffect(CardEffectType.GaugeNeedPercent, -0.10f));
            var zeroWaste = Make("zero_waste", "남김없는 추출", "재료 추가 드랍 확률 +20%, 줍기 반경 +0.5",
                SkillBranch.Ingredient, 5, measured,
                new CardEffect(CardEffectType.DropChancePercent, 0.20f),
                new CardEffect(CardEffectType.PickupRadiusFlat, 0.5f));

            return new List<SkillData>
            {
                espresso, ristretto, balanced, fullBody, perfect,
                caffeine, doppio, mellow, insulated, steady,
                coldBrew, grinder, wideTray, measured, zeroWaste,
            };
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
