using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    public enum PermanentSkillGroup { Attack, Survival, Ingredient, Weapon, Master }
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
        public PermanentSkillData[] additionalPrerequisites = new PermanentSkillData[0];
        public int minimumClears;
        public bool weaponSpecific;
        public WeaponKind targetWeapon;
        public PermanentSkillGroup group;
        /// <summary>해금에 필요한 스페셜티 포인트.</summary>
        [Min(1)] public int cost = 3;
        public CardEffect[] effects;
        /// <summary>Resources 아이콘 경로. 없으면 PermanentSkillView가 아이콘 없이 텍스트만 보여준다.</summary>
        public string iconResourcePath;
        public string iconArtId;

        public bool AppliesTo(WeaponKind startingWeapon) => !weaponSpecific || targetWeapon == startingWeapon;
        public IEnumerable<PermanentSkillData> Prerequisites
        {
            get
            {
                if (prerequisite != null) yield return prerequisite;
                if (additionalPrerequisites != null)
                    foreach (var n in additionalPrerequisites) if (n != null) yield return n;
            }
        }
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
            var tools = Make("precision_tools", "정밀한 도구", "영구 사거리 +10%", SkillBranch.Attack, 3, 3,
                crema, new CardEffect(CardEffectType.RangePercent, 0.10f));
            var profile = Make("roast_profile", "로스팅 프로파일", "영구 공격력 +10%", SkillBranch.Attack, 4, 6,
                tools, new CardEffect(CardEffectType.DamagePercent, 0.10f));
            profile.minimumClears = 1;
            var service = Make("master_service", "마스터 서비스", "영구 공격 속도 +10%", SkillBranch.Attack, 5, 9,
                profile, new CardEffect(CardEffectType.AttackSpeedPercent, 0.10f));
            service.minimumClears = 3;

            var tumbler = Make("sturdy_tumbler", "아이스음료의 필수품", "영구 최대 체력 +30", SkillBranch.Survival, 1, 3,
                null, new CardEffect(CardEffectType.MaxHpFlat, 30f));
            var veteran = Make("veteran_instinct", "베테랑의 감각", "영구 이동 속도 +10%", SkillBranch.Survival, 2, 6,
                tumbler, new CardEffect(CardEffectType.MoveSpeedPercent, 0.10f));
            var apron = Make("padded_apron", "두툼한 앞치마", "영구 받는 피해 감소 +3%p", SkillBranch.Survival, 3, 3,
                veteran, new CardEffect(CardEffectType.DamageReduction, 0.03f));
            var rest = Make("restorative_break", "잠깐의 티타임", "영구 초당 회복 +0.15", SkillBranch.Survival, 4, 6,
                apron, new CardEffect(CardEffectType.RegenBonus, 0.15f));
            rest.minimumClears = 1;
            var tray = Make("balanced_tray", "균형 잡힌 트레이", "영구 최대 체력 +20", SkillBranch.Survival, 5, 9,
                rest, new CardEffect(CardEffectType.MaxHpFlat, 20f));
            tray.minimumClears = 3;

            var cupper = Make("bean_cupper", "원두 감별사", "영구 원두 획득량 +20%", SkillBranch.Ingredient, 1, 3,
                null, new CardEffect(CardEffectType.BeanGainPercent, 0.20f));
            var roasting = Make("skilled_roasting", "정확한 계량", "영구 재료 게이지 필요량 -10%",
                SkillBranch.Ingredient, 2, 6, cupper, new CardEffect(CardEffectType.GaugeNeedPercent, -0.10f));
            var tidy = Make("tidy_counter", "정돈된 카운터", "영구 줍기 반경 +0.5", SkillBranch.Ingredient, 3, 3,
                roasting, new CardEffect(CardEffectType.PickupRadiusFlat, 0.5f));
            var sourcing = Make("careful_sourcing", "엄선한 재료", "영구 추가 재료 드랍 +5%p", SkillBranch.Ingredient, 4, 6,
                tidy, new CardEffect(CardEffectType.DropChancePercent, 0.05f));
            sourcing.minimumClears = 1;
            var origin = Make("origin_roastery", "산지 로스터리", "영구 원두 획득량 +10%", SkillBranch.Ingredient, 5, 9,
                sourcing, new CardEffect(CardEffectType.BeanGainPercent, 0.10f));
            origin.minimumClears = 3;

            var rapier = Specialty("rapier_precision", "섬세한 푸어링", "영구 사거리 +10%", WeaponKind.Rapier, 1, 3, crema,
                new CardEffect(CardEffectType.RangePercent, 0.10f));
            var flow = Specialty("rapier_flow", "연속 서비스", "영구 연속타 +1", WeaponKind.Rapier, 2, 6, rapier,
                new CardEffect(CardEffectType.ExtraThrust, 1f));
            var sword = Specialty("sword_reach", "넓은 스윙", "영구 사거리 +10%", WeaponKind.Sword, 1, 3, crema,
                new CardEffect(CardEffectType.RangePercent, 0.10f));
            var weight = Specialty("sword_weight", "묵직한 한 잔", "영구 공격력 +15%", WeaponKind.Sword, 2, 6, sword,
                new CardEffect(CardEffectType.DamagePercent, 0.15f));
            var gun = Specialty("gun_calibration", "정교한 조준", "영구 사거리 +10%", WeaponKind.Gun, 1, 3, crema,
                new CardEffect(CardEffectType.RangePercent, 0.10f));
            var doubleMeasure = Specialty("gun_double_measure", "더블 계량", "영구 동시 발사 +1", WeaponKind.Gun, 2, 6, gun,
                new CardEffect(CardEffectType.ExtraThrust, 1f));
            var bow = Specialty("bow_infusion", "깊은 우림", "영구 관통 +1", WeaponKind.Bow, 1, 3, crema,
                new CardEffect(CardEffectType.PierceCount, 1f));
            var rhythm = Specialty("bow_rhythm", "차분한 템포", "영구 공격 속도 +10%", WeaponKind.Bow, 2, 6, bow,
                new CardEffect(CardEffectType.AttackSpeedPercent, 0.10f));
            var master = Make("master_blend", "나만의 시그니처", "추가 드랍 +5%p · 원두 +10% · 줍기 +0.5",
                SkillBranch.Ingredient, 6, 12, service,
                new CardEffect(CardEffectType.DropChancePercent, 0.05f),
                new CardEffect(CardEffectType.BeanGainPercent, 0.10f),
                new CardEffect(CardEffectType.PickupRadiusFlat, 0.5f));
            master.additionalPrerequisites = new[] { tray, origin };
            master.minimumClears = 5;
            master.group = PermanentSkillGroup.Master;
            master.iconArtId = "specialty";

            return new List<PermanentSkillData> { roast, crema, tools, profile, service,
                tumbler, veteran, apron, rest, tray, cupper, roasting, tidy, sourcing, origin,
                rapier, flow, sword, weight, gun, doubleMeasure, bow, rhythm, master };
        }

        static PermanentSkillData Specialty(string id, string name, string description, WeaponKind weapon,
            int tier, int cost, PermanentSkillData prerequisite, params CardEffect[] effects)
        {
            var skill = Make(id, name, description, SkillBranch.Attack, tier, cost, prerequisite, effects);
            skill.group = PermanentSkillGroup.Weapon;
            skill.weaponSpecific = true;
            skill.targetWeapon = weapon;
            skill.minimumClears = tier == 2 ? 1 : 0;
            skill.iconResourcePath = WeaponPreset.For(weapon).iconResourcePath;
            skill.iconArtId = null;
            return skill;
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
            skill.group = (PermanentSkillGroup)branch;
            skill.effects = effects;
            bool legacy = id == "dark_roast_blend" || id == "perfect_crema" || id == "sturdy_tumbler" ||
                id == "veteran_instinct" || id == "bean_cupper" || id == "skilled_roasting";
            skill.iconResourcePath = legacy ? "Icons/PermanentSkills/" + id : null;
            skill.iconArtId = legacy ? null : SkillTreeView.BranchIconId(branch);
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
