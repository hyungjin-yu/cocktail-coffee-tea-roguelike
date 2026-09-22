using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>차 게이지 → 차 카드 레벨업, 칵테일/차 큐 처리 순서를 확인한다.</summary>
    public class TeaFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeaGauge_Full_OpensCardScreen_WithTeaCard()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Tea, game.TeaGauge.Threshold);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.LevelUp, game.State);
            Assert.IsTrue(game.CardView.IsOpen);

            game.CardView.Choose(0);
            yield return null;

            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1, game.Cards.CountOwned(CardCategory.Tea));
        }

        [UnityTest]
        public IEnumerator CocktailAndTeaLevelUps_AreProcessedInQueueOrder()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold); // 먼저 큐에 들어감
            game.AddIngredient(IngredientType.Tea, game.TeaGauge.Threshold);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.LevelUp, game.State, "먼저 큐에 들어간 칵테일이 먼저 처리되어야 한다");
            game.CardView.Choose(0);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.LevelUp, game.State, "이어서 차 카드 화면이 떠야 한다");
            game.CardView.Choose(0);
            yield return null;

            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1, game.Cards.CountOwned(CardCategory.Cocktail));
            Assert.AreEqual(1, game.Cards.CountOwned(CardCategory.Tea));
        }
    }
}
