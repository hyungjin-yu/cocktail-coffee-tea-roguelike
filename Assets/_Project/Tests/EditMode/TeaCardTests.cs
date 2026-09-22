using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    public class TeaCardTests
    {
        [Test]
        public void CreateRuntime_AllCardsAreTeaCategory()
        {
            var cards = DefaultTeaCards.CreateRuntime();
            Assert.Greater(cards.Count, 0);
            Assert.IsTrue(cards.All(c => c.category == CardCategory.Tea));
            foreach (var c in cards) Object.DestroyImmediate(c);
        }

        [Test]
        public void CreateRuntime_HasRegenAndPoisonSkillCards()
        {
            var cards = DefaultTeaCards.CreateRuntime();
            Assert.IsTrue(cards.Any(c => c.effects.Any(e => e.type == CardEffectType.RegenBonus)));
            Assert.IsTrue(cards.Any(c => c.effects.Any(e => e.type == CardEffectType.PoisonLevel)));
            foreach (var c in cards) Object.DestroyImmediate(c);
        }

        [Test]
        public void CardDatabase_LoadAll_IncludesBothCocktailAndTeaByDefault()
        {
            // Resources/Cards 에 에셋이 없는 순수 런타임 폴백 상황을 전제로 한 테스트.
            // 만약 에셋이 이미 구워져 있다면(CCT/Create Default Card Assets 실행됨) 이 테스트는 그 결과를 검증한다.
            var all = CardDatabase.LoadAll();
            Assert.IsTrue(all.Any(c => c.category == CardCategory.Cocktail), "칵테일 카드가 있어야 한다");
            Assert.IsTrue(all.Any(c => c.category == CardCategory.Tea), "차 카드가 있어야 한다");
        }
    }
}
