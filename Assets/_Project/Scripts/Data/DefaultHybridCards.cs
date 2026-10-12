using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>07:112~120 "하이브리드1종 — 티니 보우". CardSystem.Draw는 절대 category==Hybrid를
    /// 요청하지 않으므로(레이피어+칵테일3+차3 조건 확정 해금 전용, GameManager.BeginHybridOffer) 이
    /// 카드가 일반 레벨업 뽑기 풀에 섞일 일은 없다 — 오직 도감(CardCodexView) 표시용.</summary>
    public static class DefaultHybridCards
    {
        public static List<CardData> CreateRuntime()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.name = "hybrid_tea_martini";
            card.id = "hybrid_tea_martini";
            card.displayName = "티니 보우";
            card.description = "레이피어에 차의 활을 더합니다 — 보조 화살이 자동 발사됩니다.";
            card.flavorText = "";
            card.category = CardCategory.Hybrid;
            card.rarity = CardRarity.Special;
            card.maxStacks = 1;
            card.effects = System.Array.Empty<CardEffect>(); // 실제 효과는 HybridBowWeapon 컴포넌트 자체
            card.weaponRestriction = WeaponRestriction.Any;
            return new List<CardData> { card };
        }
    }
}
