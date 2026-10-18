using System;
using UnityEngine;

namespace CCT
{
    public enum CardCategory { Cocktail, Tea, Hybrid }

    /// <summary>Special은 07:114 "티니 보우"(하이브리드) 전용 — 랜덤 카드 뽑기 풀에 절대 섞이지 않는다
    /// (CardSystem.Draw는 category==Hybrid를 절대 요청하지 않음). RarityTable.Weight가 있어도 실제로
    /// 쓰이지 않는다.</summary>
    public enum CardRarity { Common, Rare, Epic, Special }

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
        /// <summary>차 스킬 — 떫은 여운 레벨(저장 호환 enum) (FlameLevel과 같은 방식, 다른 컬러/이름).</summary>
        PoisonLevel,
        /// <summary>원거리 전용 — 발사체가 뚫고 지나갈 수 있는 추가 대상 수.</summary>
        PierceCount,
        /// <summary>영구 강화 전용 — 커피 원두 획득량 % 보너스 (PermanentSkillData가 사용).</summary>
        BeanGainPercent,
        /// <summary>받은 피해 감소 % (07:19 steady_hand 등). PlayerStats.DamageTakenMultiplier에서 0~0.25로 clamp.</summary>
        DamageReduction,
        /// <summary>줍기 반경 가산치 (07:141 wide_tray/zero_waste 등).</summary>
        PickupRadiusFlat,
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
        [TextArea] public string realWorldFact;
        [TextArea] public string gameplayConnection;
        public string sourceName;
        public string sourceUrl;
        [TextArea] public string additionalSources;
        public string educationArtId;
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
                case CardRarity.Epic: return 10f;
                default: return 0f; // Special — 절대 랜덤 뽑기 대상이 아님
            }
        }

        public static string Label(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return "일반";
                case CardRarity.Rare: return "희귀";
                case CardRarity.Epic: return "영웅";
                default: return "특수";
            }
        }

        public static Color Color(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return new Color(0.82f, 0.82f, 0.82f);
                case CardRarity.Rare: return new Color(0.4f, 0.72f, 1f);
                case CardRarity.Epic: return new Color(0.82f, 0.5f, 1f);
                default: return new Color(1f, 0.75f, 0.35f); // Special
            }
        }
    }
}
