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
                Make("dry_gin", "드라이 진", "공격력 +15%", "달지 않고 깔끔한 정통 진. 마티니의 베이스로 흔히 쓰인다.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.DamagePercent, 0.15f)),
                Make("shot_glass", "샷 글래스", "공격 속도 +12%", "한 번에 털어 넣는 작은 잔. 빠른 리듬의 상징.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.AttackSpeedPercent, 0.12f)),
                Make("long_stirrer", "롱 스터러", "사거리 +15%", "얼음과 재료를 길게 저어 섞는 바 스푼.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.RangePercent, 0.15f)),
                Make("bar_shoes", "바텐더 슈즈", "이동 속도 +10%", "긴 근무 시간을 버티게 해주는 편한 신발.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.MoveSpeedPercent, 0.10f)),
                Make("ice_cube", "얼음 한 조각", "최대 체력 +20", "칵테일을 차갑게, 그리고 천천히 묽어지게 한다.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.MaxHpFlat, 20f)),
                Make("liqueur", "리큐르 병", "재료 게이지 필요량 -8%", "과일·허브 향을 입힌 달콤한 술. 소량으로도 풍미를 바꾼다.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f)),
                Make("syrup", "시럽 한 방울", "재료 추가 드랍 확률 +10%", "설탕을 녹여 만든 단맛의 원천.",
                    CardRarity.Common, 5, new CardEffect(CardEffectType.DropChancePercent, 0.10f)),

                // 희귀
                Make("double_shot", "더블 샷", "공격력 +30%", "보통의 두 배 — 독하게, 화끈하게.",
                    CardRarity.Rare, 4, new CardEffect(CardEffectType.DamagePercent, 0.30f)),
                Make("mixing_tempo", "믹싱 템포", "공격 속도 +25%", "셰이커를 흔드는 리듬. 빠를수록 잘 섞이고 잘 희석된다.",
                    CardRarity.Rare, 4, new CardEffect(CardEffectType.AttackSpeedPercent, 0.25f)),
                Make("flambe", "플람베", "찌르기 +1회 (연속 공격)", "술에 불을 붙여 향과 알코올을 날리는 화려한 기법.",
                    CardRarity.Rare, 3, WeaponRestriction.MeleeOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("champagne_spray", "샴페인 스프레이", "다중 발사 +1발 (부채꼴로 흩뿌림)",
                    "코르크를 터뜨리며 사방으로 흩뿌리는 축하의 세리머니.",
                    CardRarity.Rare, 3, WeaponRestriction.RangedOnly, new CardEffect(CardEffectType.ExtraThrust, 1f)),
                Make("molotov", "화염병 칵테일", "주기적으로 주변에 화염 폭발 (Lv +1)", "이름 그대로 위험하고 강렬한 한 잔.",
                    CardRarity.Rare, 5, new CardEffect(CardEffectType.FlameLevel, 1f)),

                // 영웅
                Make("scotch_rocks", "스카치 온 더 락", "공격력 +50%", "얼음 위에 부은 스카치 위스키. 향을 오래 즐기는 방식.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.DamagePercent, 0.50f)),
                Make("dry_martini", "드라이 마티니", "공격력 +25%, 공격 속도 +20%",
                    "베르무트를 최소로, 진의 존재감을 극대화한 클래식.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.DamagePercent, 0.25f),
                    new CardEffect(CardEffectType.AttackSpeedPercent, 0.20f)),
                Make("blazer", "블레이저", "화염 폭발 Lv +2", "불붙인 위스키를 잔 사이로 옮겨 붓는 곡예 같은 기법.",
                    CardRarity.Epic, 3, new CardEffect(CardEffectType.FlameLevel, 2f)),
                Make("rifled_barrel", "강선 개조", "발사체 관통 +1 (몹을 뚫고 지나감)", "총열에 나선을 새겨 탄이 뚫고 나가는 힘을 더한다.",
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
            card.category = CardCategory.Cocktail;
            card.rarity = rarity;
            card.maxStacks = maxStacks;
            card.effects = effects;
            card.weaponRestriction = weaponRestriction;
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
            return runtime;
        }
    }
}
