using System;
using UnityEngine;

namespace CCT
{
    public enum CardCategory { Cocktail, Tea, Hybrid }

    public enum CardRarity { Common, Rare, Epic }

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
        public CardCategory category;
        public CardRarity rarity;
        [Min(1)] public int maxStacks = 5;
        public CardEffect[] effects;
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
