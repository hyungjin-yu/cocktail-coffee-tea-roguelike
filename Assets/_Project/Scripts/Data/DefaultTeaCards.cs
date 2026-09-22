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
                Make("green_tea", "녹차 잎", "공격력 +15%", "덖거나 찌기만 한 발효 안 된 찻잎. 산뜻한 첫 맛.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.DamagePercent, 0.15f)),
                Make("tea_whisk", "차선", "공격 속도 +12%", "말차를 곱게 풀어 거품 내는 대나무 솔.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.AttackSpeedPercent, 0.12f)),
                Make("long_spout", "긴 주둥이 주전자", "사거리 +15%", "멀리, 가늘게 따를 수 있는 다도용 주전자.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.RangePercent, 0.15f)), // 근접/원거리 공용 (제한 없음)
                Make("tea_sandals", "다도용 신발", "이동 속도 +10%", "정갈한 걸음을 위한 조용한 신발.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.MoveSpeedPercent, 0.10f)),
                Make("warm_cup", "따뜻한 찻잔", "최대 체력 +20", "손을 데우고 차의 향을 오래 지켜주는 잔.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.MaxHpFlat, 20f)),
                Make("honey", "꿀 한 스푼", "재료 게이지 필요량 -8%", "차의 쓴맛을 부드럽게 감싸는 천연 단맛.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f)),
                Make("dried_leaf", "말린 찻잎", "재료 추가 드랍 확률 +10%", "볕에 말려 오래 두고 우릴 수 있게 한 찻잎.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.DropChancePercent, 0.10f)),

                // 희귀
                Make("oolong", "우롱차", "공격력 +30%", "녹차와 홍차 사이, 반쯤 발효시킨 깊은 향의 차.",
                    CardRarity.Rare, 4, new CardEffect(CardEffectType.DamagePercent, 0.30f)),
                Make("brew_rhythm", "우림 리듬", "공격 속도 +25%", "찻잎을 우리는 절묘한 타이밍의 감각.",
                    CardRarity.Rare, 4, new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f)),
                Make("second_infusion", "두 번째 우림", "찌르기 +1회 (연속 공격)", "한 번 우린 찻잎에 다시 물을 부어 얻는 또 다른 풍미.",
                    CardRarity.Rare, 3, WeaponRestriction.MeleeOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("tea_leaf_scatter", "찻잎 흩뿌리기", "다중 발사 +1발 (부채꼴로 흩뿌림)", "찻잎을 허공에 흩뿌려 향을 퍼뜨리는 퍼포먼스.",
                    CardRarity.Rare, 3, WeaponRestriction.RangedOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("chamomile", "카모마일", "초당 회복량 +1.2", "은은한 사과 향의 허브차. 마음을 가라앉힌다.",
                    CardRarity.Rare, 5, new CardEffect(CardEffectType.RegenBonus, 1.2f)),
                Make("nettle_tea", "쐐기풀차", "주기적으로 주변에 독 피해 (Lv +1)", "쏘는 잎이지만 우리면 순한 약차가 되는 쐐기풀.",
                    CardRarity.Rare, 5, new CardEffect(CardEffectType.PoisonLevel, 1f)),

                // 영웅
                Make("aged_puerh", "숙성 보이차", "공격력 +50%", "오래 묵힐수록 깊어지는 발효차의 정점.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.DamagePercent, 0.50f)),
                Make("golden_ratio", "황금 비율 다도", "공격력 +25%, 공격 속도 +20%", "물 온도·찻잎량·시간의 완벽한 균형.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.DamagePercent, 0.25f),
                    new CardEffect(CardEffectType.AttackSpeedPercent, 0.20f)),
                Make("ginseng_root", "산삼 뿌리", "초당 회복량 +3", "귀한 약재 뿌리 — 강한 기운을 불어넣는다.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.RegenBonus, 3f)),
                Make("nightshade_blend", "가지속 블렌드", "독 피해 Lv +2", "독성 식물에서 조심스레 추출한 강한 블렌드.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.PoisonLevel, 2f)),
                Make("piercing_arrowhead", "관통 화살촉", "발사체 관통 +1 (몹을 뚫고 지나감)", "끝을 날카롭게 벼려 무엇이든 뚫고 지나가게 한다.",
                    CardRarity.Epic, 3, WeaponRestriction.RangedOnly, new CardEffect(CardEffectType.PierceCount, 1f)),
            };
        }

        static CardData Make(string id, string displayName, string description, string flavorText,
            CardRarity rarity, int maxStacks, params CardEffect[] effects) =>
            Make(id, displayName, description, flavorText, rarity, maxStacks, WeaponRestriction.Any, effects);

        static CardData Make(string id, string displayName, string description, string flavorText,
            CardRarity rarity, int maxStacks, WeaponRestriction weaponRestriction, params CardEffect[] effects)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.name = id;
            card.id = id;
            card.displayName = displayName;
            card.description = description;
            card.flavorText = flavorText;
            card.category = CardCategory.Tea;
            card.rarity = rarity;
            card.maxStacks = maxStacks;
            card.effects = effects;
            card.weaponRestriction = weaponRestriction;
            return card;
        }
    }
}
