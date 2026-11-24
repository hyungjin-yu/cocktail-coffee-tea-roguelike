using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #21 기본 활 전용 속성 화살 카드 4장(화상·빙결·감전·중독). 차 카드 창(활은 티 소믈리에 무기 — 찻집에서도 열림)에서
    /// WeaponRestriction.BowOnly로만 나온다: 총·레이피어·검 런과 레이피어의 보조 활(티니 보우)에는 나오지 않고 적용되지도 않는다.
    /// 07 §3 기본 34장과 별개 목록(에셋 DB에 굽지 않고 CardDatabase.LoadAll이 덧붙임). 실제 규칙과 수치는 ArrowElementRules.
    /// </summary>
    public static class DefaultArrowCards
    {
        public static readonly string[] Ids = { "arrow_burn", "arrow_freeze", "arrow_shock", "arrow_poison" };

        public static List<CardData> CreateRuntime()
        {
            return new List<CardData>
            {
                Make("arrow_burn", "화상 화살", ArrowElementKind.Burn, CardEffectType.ArrowBurn),
                Make("arrow_freeze", "빙결 화살", ArrowElementKind.Freeze, CardEffectType.ArrowFreeze),
                Make("arrow_shock", "감전 화살", ArrowElementKind.Shock, CardEffectType.ArrowShock),
                Make("arrow_poison", "중독 화살", ArrowElementKind.Poison, CardEffectType.ArrowPoison),
            };
        }

        public static bool IsArrowCard(string id) => System.Array.IndexOf(Ids, id) >= 0;

        public static bool IsArrowEffect(CardEffectType t) =>
            t == CardEffectType.ArrowBurn || t == CardEffectType.ArrowFreeze || t == CardEffectType.ArrowShock || t == CardEffectType.ArrowPoison;

        static CardData Make(string id, string displayName, ArrowElementKind kind, CardEffectType effect)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.name = id;
            card.id = id;
            card.displayName = displayName;
            card.description = ArrowElementRules.Description(kind);
            card.category = CardCategory.Tea;
            card.rarity = CardRarity.Rare;
            card.maxStacks = ArrowElementRules.MaxLevel;
            card.effects = new[] { new CardEffect(effect, 1f) };
            card.weaponRestriction = WeaponRestriction.BowOnly;
            CardEducationData.Author(card);
            return card;
        }
    }
}
