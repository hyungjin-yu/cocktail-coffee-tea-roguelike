using System;
using UnityEngine;

namespace CCT
{
    public enum CardCategory { Cocktail, Tea, Hybrid }

    public enum CardRarity { Common, Rare, Epic }

    /// <summary>이 카드가 어느 무기군에만 나올지. 대부분 카드는 Any(둘 다). 무기별 정체성이 있는 카드만 제한.</summary>
    public enum WeaponRestriction { Any, MeleeOnly, RangedOnly }

    public enum CardEffectType
    {
        DamagePercent,
        AttackSpeedPercent,
        RangePercent,
        MoveSpeedPercent,
        MaxHpFlat,
        DropChancePercent,
        GaugeNeedPercent,
        ExtraThrust,
        FlameLevel,
        /// <summary>차 스킬 — 초당 회복량 보너스.</summary>
        RegenBonus,
        /// <summary>차 스킬 — 독 오라 레벨 (FlameLevel과 같은 방식, 다른 컬러/이름).</summary>
        PoisonLevel,
        /// <summary>원거리 전용 — 발사체가 뚫고 지나갈 수 있는 추가 대상 수.</summary>
        PierceCount,
        /// <summary>영구 강화 전용 — 커피 원두 획득량 % 보너스 (PermanentSkillData가 사용).</summary>
        BeanGainPercent,
    }

    [Serializable]
    public struct CardEffect
    {
        public CardEffectType type;
        public float value;

        public CardEffect(CardEffectType type, float value)
        {
            this.type = type;
            this.value = value;
        }
    }

    /// <summary>
    /// 칵테일/차/하이브리드 카드 공용 데이터. 카테고리는 enum 값으로만 구분한다 (기획서 13장).
    /// </summary>
    [CreateAssetMenu(menuName = "CCT/Card Data", fileName = "Card_")]
    public class CardData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        /// <summary>카드 제목(이름)이 실제로 뭘 가리키는지 설명하는 풍미 텍스트. 효과 설명(description)과
        /// 달리 게임플레이에 영향 없는 순수 플레이버 — 카드 도감에 표시된다.</summary>
        [TextArea] public string flavorText;
        public CardCategory category;
        public CardRarity rarity;
        [Min(1)] public int maxStacks = 5;
        public CardEffect[] effects;
        public WeaponRestriction weaponRestriction = WeaponRestriction.Any;
    }

    /// <summary>등급별 가중치 (가안). 카드 UI에 노출되는 확률의 기준.</summary>
    public static class RarityTable
    {
        public static float Weight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return 60f;
                case CardRarity.Rare: return 30f;
                default: return 10f;
            }
        }

        public static string Label(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return "일반";
                case CardRarity.Rare: return "희귀";
                default: return "영웅";
            }
        }

        public static Color Color(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return new Color(0.82f, 0.82f, 0.82f);
                case CardRarity.Rare: return new Color(0.4f, 0.72f, 1f);
                default: return new Color(0.82f, 0.5f, 1f);
            }
        }
    }
}
