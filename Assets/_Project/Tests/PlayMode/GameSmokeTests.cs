using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>실제로 게임을 띄워 코어 루프(스폰 → 처치 → 드랍 → 레벨업 → 카드 선택 → 종료 → 재시작)를 확인한다.</summary>
    public class GameSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard; // 로컬 PlayerPrefs 값과 무관하게 결정론적으로
            var game = GameBootstrap.Build();
            Assert.IsNotNull(game);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            // 재시작 테스트가 띄운 직업 선택 화면(클릭 안 하고 우회)이 남아있으면 정리
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null; // 파괴가 프레임 끝에 처리되므로 다음 테스트 전에 한 프레임 넘긴다
        }

        [UnityTest]
        public IEnumerator Boot_BuildsPlayerAndSpawnsEnemies()
        {
            var game = GameManager.Instance;
            Assert.IsNotNull(game.Player);
            Assert.AreEqual(GameState.Playing, game.State);

            yield return new WaitForSeconds(3f);

            Assert.Greater(Enemy.Active.Count, 0, "몹이 스폰되어야 한다");
        }

        [UnityTest]
        public IEnumerator PlayerAutoAttack_KillsEnemies_AndDropsIngredients()
        {
            var game = GameManager.Instance;
            // 몹이 오도록 시간을 준다 (스폰 링 13유닛, 임프 속도 2.6)
            float deadline = Time.realtimeSinceStartup + 25f;
            while (game.Kills == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.Greater(game.Kills, 0, "자동 공격으로 몹을 처치해야 한다");

            // 드랍이 생겼고, 플레이어가 가까이 있으면 게이지가 오른다.
            // 몹은 칵테일/차 50:50 이라 둘 중 하나(또는 둘 다)가 올라야 한다.
            deadline = Time.realtimeSinceStartup + 10f;
            bool AnyProgress() => game.CocktailGauge.Current > 0 || game.CocktailGauge.Level > 1
                                   || game.TeaGauge.Current > 0 || game.TeaGauge.Level > 1;
            while (!AnyProgress() && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(AnyProgress(), "처치한 몹의 재료가 게이지(칵테일 또는 차)에 들어가야 한다");
        }

        [UnityTest]
        public IEnumerator LevelUp_PausesGame_ShowsThreeCards_AndResumesAfterChoice()
        {
            var game = GameManager.Instance;
            int levelBefore = game.CocktailGauge.Level;

            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.LevelUp, game.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(game.CardView.IsOpen);
            Assert.AreEqual(levelBefore + 1, game.CocktailGauge.Level);

            game.CardView.Choose(0);
            yield return null;

            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(game.CardView.IsOpen);
            Assert.AreEqual(1, game.Cards.CountOwned(CardCategory.Cocktail));
        }

        [UnityTest]
        public IEnumerator Player_RegeneratesSlowly_AndCannotExceedMaxHp()
        {
            var player = GameManager.Instance.Player;
            player.TakeDamage(player.MaxHp * 0.5f);
            float before = player.Hp;
            Assert.Less(before, player.MaxHp);

            yield return new WaitForSeconds(1f);

            Assert.Greater(player.Hp, before, "자연 회복이 있어야 한다");
            Assert.LessOrEqual(player.Hp, player.MaxHp);

            player.Heal(5f);
            Assert.AreEqual(player.MaxHp, player.Hp, 0.001f, "최대 체력을 넘겨 회복되면 안 된다");
        }

        [UnityTest]
        public IEnumerator ChoosingACard_HealsPlayer()
        {
            var game = GameManager.Instance;
            game.Player.TakeDamage(game.Player.MaxHp * 0.6f);
            float before = game.Player.Hp;

            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            yield return null;
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);
            Assert.AreEqual(before, game.Player.Hp, 0.5f, "카드 선택 전에는 회복하지 않는다 (일시정지 중)");

            game.CardView.Choose(0);
            yield return null;

            float expected = Mathf.Min(game.Player.MaxHp, before + game.Player.MaxHp * Balance.LevelUpHealFraction);
            Assert.AreEqual(expected, game.Player.Hp, 1f);
        }

        [UnityTest]
        public IEnumerator MultipleLevelUps_AreQueued_OneCardScreenAtATime()
        {
            var game = GameManager.Instance;

            game.AddIngredient(IngredientType.Cocktail, 100); // 여러 번 레벨업
            int expectedPicks = game.CocktailGauge.Level - 1;
            Assert.GreaterOrEqual(expectedPicks, 2);

            for (int i = 0; i < expectedPicks; i++)
            {
                yield return null;
                yield return null;
                Assert.AreEqual(GameState.LevelUp, game.State, $"{i + 1}번째 카드 화면");
                game.CardView.Choose(1);
            }

            yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(expectedPicks, game.Cards.CountOwned(CardCategory.Cocktail));
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ShowsResult_AndRestartCreatesFreshRun()
        {
            var game = GameManager.Instance;
            game.Player.TakeDamage(9999f);
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(0f, Time.timeScale);

            game.Restart(); // 직업 선택 화면을 띄움
            yield return null;
            var fresh = GameBootstrap.Build(Job.Bartender); // 테스트에서는 화면 클릭 대신 직접 진행
            yield return null;

            Assert.IsNotNull(fresh);
            Assert.AreNotSame(game, fresh);
            Assert.AreEqual(GameState.Playing, fresh.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, fresh.Kills);
            Assert.AreEqual(1, fresh.CocktailGauge.Level);
            Assert.IsNotNull(Camera.main, "재시작 후에도 카메라가 있어야 한다");
        }
    }
}
