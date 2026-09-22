using System;
using System.Collections.Generic;

namespace CCT
{
    public struct CardOffer
    {
        public CardData card;
        /// <summary>이 카드의 등급이 뽑힐 확률(%). 뽑기 시점에 남아있는 등급들 기준.</summary>
        public float rarityPercent;
        public int currentStacks;
    }

    /// <summary>카드 뽑기/보유/적용. 칵테일·차 카드가 같은 시스템을 공유한다.</summary>
    public class CardSystem
    {
        readonly List<CardData> pool;
        readonly Dictionary<CardData, int> owned = new Dictionary<CardData, int>();
        readonly Random rng;

        public CardSystem(IEnumerable<CardData> pool, Random rng = null)
        {
            this.pool = new List<CardData>(pool);
            this.rng = rng ?? new Random();
        }

        public int GetStacks(CardData card) => owned.TryGetValue(card, out var n) ? n : 0;

        public int CountOwned(CardCategory category)
        {
            int total = 0;
            foreach (var kv in owned)
                if (kv.Key.category == category) total += kv.Value;
            return total;
        }

        public List<CardOffer> Draw(CardCategory category, bool isRanged, int count = 3)
        {
            var eligible = new List<CardData>();
            foreach (var card in pool)
                if (card.category == category && GetStacks(card) < card.maxStacks && IsWeaponCompatible(card, isRanged))
                    eligible.Add(card);

            // 노출 확률: 지금 뽑을 수 있는 카드가 남아있는 등급들끼리 가중치를 정규화
            var rarityPercent = new Dictionary<CardRarity, float>();
            float totalWeight = 0f;
            foreach (CardRarity r in Enum.GetValues(typeof(CardRarity)))
            {
                if (eligible.Exists(c => c.rarity == r))
                    totalWeight += RarityTable.Weight(r);
            }
            foreach (CardRarity r in Enum.GetValues(typeof(CardRarity)))
            {
                if (eligible.Exists(c => c.rarity == r))
                    rarityPercent[r] = RarityTable.Weight(r) / totalWeight * 100f;
            }

            var offers = new List<CardOffer>();
            while (offers.Count < count && eligible.Count > 0)
            {
                var rarity = RollRarity(eligible);
                var candidates = eligible.FindAll(c => c.rarity == rarity);
                var picked = candidates[rng.Next(candidates.Count)];
                eligible.Remove(picked);
                offers.Add(new CardOffer
                {
                    card = picked,
                    rarityPercent = rarityPercent[rarity],
                    currentStacks = GetStacks(picked),
                });
            }
            return offers;
        }

        static bool IsWeaponCompatible(CardData card, bool isRanged)
        {
            return card.weaponRestriction switch
            {
                WeaponRestriction.MeleeOnly => !isRanged,
                WeaponRestriction.RangedOnly => isRanged,
                _ => true,
            };
        }

        public void Take(CardData card, PlayerStats stats)
        {
            owned[card] = GetStacks(card) + 1;
            if (card.effects == null) return;
            foreach (var effect in card.effects)
                stats.Apply(effect);
        }

        CardRarity RollRarity(List<CardData> eligible)
        {
            float total = 0f;
            foreach (CardRarity r in Enum.GetValues(typeof(CardRarity)))
                if (eligible.Exists(c => c.rarity == r)) total += RarityTable.Weight(r);

            float roll = (float)(rng.NextDouble() * total);
            CardRarity last = CardRarity.Common;
            foreach (CardRarity r in Enum.GetValues(typeof(CardRarity)))
            {
                if (!eligible.Exists(c => c.rarity == r)) continue;
                last = r;
                roll -= RarityTable.Weight(r);
                if (roll < 0f) return r;
            }
            return last;
        }
    }
}
