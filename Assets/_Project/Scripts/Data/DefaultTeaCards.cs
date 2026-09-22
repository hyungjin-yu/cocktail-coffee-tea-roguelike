using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 차 카드 기본 세트 (전부 가안 수치). 칵테일과 같은 카드 시스템을 공유하고 category만 다르다.
    /// 스킬은 기획서 7장 "차 = 회복/버프/독 계열" 기준 — 회복(RegenBonus), 독(PoisonLevel).
    /// </summary>
    public static class DefaultTeaCards
    {
        public static List<CardData> CreateRuntime()
        {
            return new List<CardData>
            {
                // 일반
                Make("green_tea", "녹차 잎", "공격력 +15%", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.DamagePercent, 0.15f)),
                Make("tea_whisk", "차선", "공격 속도 +12%", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.AttackSpeedPercent, 0.12f)),
                Make("long_spout", "긴 주둥이 주전자", "사거리 +15%", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.RangePercent, 0.15f)), // 근접/원거리 공용 (제한 없음)
                Make("tea_sandals", "다도용 신발", "이동 속도 +10%", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.MoveSpeedPercent, 0.10f)),
                Make("warm_cup", "따뜻한 찻잔", "최대 체력 +20", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.MaxHpFlat, 20f)),
                Make("honey", "꿀 한 스푼", "재료 게이지 필요량 -8%", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f)),
                Make("dried_leaf", "말린 찻잎", "재료 추가 드랍 확률 +10%", CardRarity.Common, 5,
                    new CardEffect(CardEffectType.DropChancePercent, 0.10f)),

                // 희귀
                Make("oolong", "우롱차", "공격력 +30%", CardRarity.Rare, 4,
                    new CardEffect(CardEffectType.DamagePercent, 0.30f)),
                Make("brew_rhythm", "우림 리듬", "공격 속도 +25%", CardRarity.Rare, 4,
                    new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f)),
                Make("second_infusion", "두 번째 우림", "찌르기 +1회 (연속 공격)", CardRarity.Rare, 3,
                    WeaponRestriction.MeleeOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("tea_leaf_scatter", "찻잎 흩뿌리기", "다중 발사 +1발 (부채꼴로 흩뿌림)", CardRarity.Rare, 3,
                    WeaponRestriction.RangedOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("chamomile", "카모마일", "초당 회복량 +1.2", CardRarity.Rare, 5,
                    new CardEffect(CardEffectType.RegenBonus, 1.2f)),
                Make("nettle_tea", "쐐기풀차", "주기적으로 주변에 독 피해 (Lv +1)", CardRarity.Rare, 5,
                    new CardEffect(CardEffectType.PoisonLevel, 1f)),

                // 영웅
                Make("aged_puerh", "숙성 보이차", "공격력 +50%", CardRarity.Epic, 3,
                    new CardEffect(CardEffectType.DamagePercent, 0.50f)),
                Make("golden_ratio", "황금 비율 다도", "공격력 +25%, 공격 속도 +20%", CardRarity.Epic, 3,
                    new CardEffect(CardEffectType.DamagePercent, 0.25f),
                    new CardEffect(CardEffectType.AttackSpeedPercent, 0.20f)),
                Make("ginseng_root", "산삼 뿌리", "초당 회복량 +3", CardRarity.Epic, 3,
                    new CardEffect(CardEffectType.RegenBonus, 3f)),
                Make("nightshade_blend", "가지속 블렌드", "독 피해 Lv +2", CardRarity.Epic, 3,
                    new CardEffect(CardEffectType.PoisonLevel, 2f)),
            };
        }

        static CardData Make(string id, string displayName, string description, CardRarity rarity,
            int maxStacks, params CardEffect[] effects) =>
            Make(id, displayName, description, rarity, maxStacks, WeaponRestriction.Any, effects);

        static CardData Make(string id, string displayName, string description, CardRarity rarity,
            int maxStacks, WeaponRestriction weaponRestriction, params CardEffect[] effects)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.name = id;
            card.id = id;
            card.displayName = displayName;
            card.description = description;
            card.category = CardCategory.Tea;
            card.rarity = rarity;
            card.maxStacks = maxStacks;
            card.effects = effects;
            card.weaponRestriction = weaponRestriction;
            return card;
        }
    }
}
