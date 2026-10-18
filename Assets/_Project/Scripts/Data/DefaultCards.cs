using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 칵테일 카드 기본 세트 (전부 가안 수치). Resources/Cards 에 에셋이 없으면 이 목록이 런타임에 쓰인다.
    /// 에디터 메뉴 CCT/Create Default Card Assets 로 에셋화하면 이후에는 인스펙터에서 튜닝 가능.
    /// </summary>
    public static class DefaultCards
    {
        public static List<CardData> CreateRuntime()
        {
            return new List<CardData>
            {
                // 일반
                Make("dry_gin", "드라이 진", "공격력 +15%",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.DamagePercent, 0.15f)),
                Make("shot_glass", "지거", "공격 속도 +12%",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.AttackSpeedPercent, 0.12f)),
                Make("long_stirrer", "바 스푼", "사거리 +15%",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.RangePercent, 0.15f)),
                Make("bar_shoes", "빌드", "이동 속도 +10%",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.MoveSpeedPercent, 0.10f)),
                Make("ice_cube", "얼음과 희석", "최대 체력 +20",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.MaxHpFlat, 20f)),
                Make("liqueur", "리큐르", "재료 게이지 필요량 -8%",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f)),
                Make("syrup", "프리배치", "재료 추가 드랍 확률 +10%",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.DropChancePercent, 0.10f)),

                // 희귀
                Make("double_shot", "네그로니", "공격력 +30%",
                    CardRarity.Rare, 4, new CardEffect(CardEffectType.DamagePercent, 0.30f)),
                Make("mixing_tempo", "셰이킹", "공격 속도 +25%",
                    CardRarity.Rare, 4, new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f)),
                Make("flambe", "더블 스트레인", "베기 +1회 (연속 공격)",
                    CardRarity.Rare, 3, WeaponRestriction.MeleeOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("champagne_spray", "스플릿 베이스", "다중 발사 +1발 (부채꼴로 흩뿌림)",
                    CardRarity.Rare, 3, WeaponRestriction.RangedOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("molotov", "플레임드 제스트", "주무기 적중 화염 (Lv +1): 1초마다 3초 지속",
                    CardRarity.Rare, 5, new CardEffect(CardEffectType.FlameLevel, 1f)),

                // 영웅
                Make("scotch_rocks", "올드패션드", "공격력 +50%",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.DamagePercent, 0.50f)),
                Make("dry_martini", "드라이 마티니", "공격력 +25%, 공격 속도 +20%",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.DamagePercent, 0.25f),
                    new CardEffect(CardEffectType.AttackSpeedPercent, 0.20f)),
                Make("blazer", "블루 블레이저", "주무기 적중 화염 Lv +2 · 3초 지속",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.FlameLevel, 2f)),
                Make("rifled_barrel", "스트레이너", "발사체 관통 +1 (몹을 뚫고 지나감)",
                    CardRarity.Epic, 3, WeaponRestriction.RangedOnly, new CardEffect(CardEffectType.PierceCount, 1f)),
            };
        }

        static CardData Make(string id, string displayName, string description,
            CardRarity rarity, int maxStacks, params CardEffect[] effects) =>
            Make(id, displayName, description, rarity, maxStacks, WeaponRestriction.Any, effects);

        static CardData Make(string id, string displayName, string description,
            CardRarity rarity, int maxStacks, WeaponRestriction weaponRestriction, params CardEffect[] effects)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.name = id;
            card.id = id;
            card.displayName = displayName;
            card.description = description;
            card.category = CardCategory.Cocktail;
            card.rarity = rarity;
            card.maxStacks = maxStacks;
            card.effects = effects;
            card.weaponRestriction = weaponRestriction;
            CardEducationData.Author(card);
            return card;
        }
    }

    public static class CardDatabase
    {
        public static List<CardData> LoadAll()
        {
            var loaded = Resources.LoadAll<CardData>("Cards");
            if (loaded != null && loaded.Length > 0)
                return new List<CardData>(loaded);

            var runtime = DefaultCards.CreateRuntime();
            runtime.AddRange(DefaultTeaCards.CreateRuntime());
            runtime.AddRange(DefaultHybridCards.CreateRuntime());
            return runtime;
        }
    }
}
