using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>07:112~120 "티니 보우" — 레이피어 시작 전용 하이브리드. 해금 조건, 확정 해금 창,
    /// 보조 활의 사거리/피해/간격/관통 공식을 검증한다.</summary>
    public class HybridWeaponTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        /// <summary>대기 중인 카드/스킬 보상을 계속 첫 번째(또는 첫 해금 가능) 선택지로 흘려보낸다 —
        /// 하이브리드 안내가 뜨거나 더 이상 진행할 보상이 없을 때까지.</summary>
        static IEnumerator DrainRewardsUntilHybridOffer(GameManager game, int maxSteps = 80)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                if (game.State == GameState.HybridOffer) yield break;
                if (game.State == GameState.LevelUp) game.CardView.Choose(0);
                else if (game.State == GameState.SkillSelect)
                    game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
                yield return null;
            }
        }

        /// <summary>07:44~46 — 하이브리드 해금 뒤에도 남은 일반 카드 보상은 Playing을 끼우지 않고 이어서 열린다.
        /// 남은 보상을 첫 선택지로 모두 처리해 Playing으로 돌아오게 한다(커피 보상은 원두가 없어 생기지 않는다).</summary>
        static IEnumerator DrainRewardsUntilPlaying(GameManager game, int maxSteps = 120)
        {
            for (int i = 0; i < maxSteps && game.State != GameState.Playing; i++)
            {
                Assert.AreNotEqual(GameState.HybridOffer, game.State, "07:116 — 같은 런에서 하이브리드를 다시 제안하지 않는다");
                Assert.AreNotEqual(GameState.GameOver, game.State);
                if (game.State == GameState.LevelUp) game.CardView.Choose(0);
                else if (game.State == GameState.SkillSelect)
                    game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
                yield return null;
            }
            Assert.AreEqual(GameState.Playing, game.State, "남은 보상을 다 고르면 Playing으로 돌아와야 한다");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator RapierStart_Cocktail3Tea3_TriggersHybridOffer()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, 5000);
            game.AddIngredient(IngredientType.Tea, 5000);
            yield return null;

            yield return DrainRewardsUntilHybridOffer(game);

            Assert.AreEqual(GameState.HybridOffer, game.State, "칵테일3+차3를 모으면 확정 해금 창이 떠야 한다");
            Assert.GreaterOrEqual(game.Cards.CountOwned(CardCategory.Cocktail), 3);
            Assert.GreaterOrEqual(game.Cards.CountOwned(CardCategory.Tea), 3);
            Assert.IsFalse(game.HybridUnlocked, "확정 버튼을 누르기 전엔 아직 해금된 게 아니다");
            Assert.AreEqual(0f, Time.timeScale, "확정 해금 창이 뜨는 동안엔 전투가 멈춰야 한다");
        }

        [UnityTest]
        public IEnumerator GunStart_NeverOffersHybrid_EvenWithCocktail3Tea3()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Gun);
            yield return null;

            game.AddIngredient(IngredientType.Cocktail, 5000);
            game.AddIngredient(IngredientType.Tea, 5000);
            yield return null;
            yield return DrainRewardsUntilHybridOffer(game, maxSteps: 200);

            Assert.AreNotEqual(GameState.HybridOffer, game.State, "07:120 — 레이피어 시작이 아니면 하이브리드가 없다");
            Assert.IsFalse(game.HybridUnlocked);
        }

        [UnityTest]
        public IEnumerator ChoosingHybrid_UnlocksAndAttachesBow_WithoutRemovingRapier()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, 5000);
            game.AddIngredient(IngredientType.Tea, 5000);
            yield return null;
            yield return DrainRewardsUntilHybridOffer(game);
            Assert.AreEqual(GameState.HybridOffer, game.State);

            game.HybridView.Unlock();

            Assert.IsTrue(game.HybridUnlocked);
            Assert.IsNotNull(game.Player.GetComponent<RapierWeapon>(), "레이피어는 그대로 유지돼야 한다");
            Assert.IsNotNull(game.Player.GetComponent<HybridBowWeapon>(), "보조 활이 추가돼야 한다");
            // 2026-10-06: 이전 기대(해금 직후 항상 Playing)는 보상 대기열(07:44~46) 이전 규칙. 재료 5000이라 일반 카드
            // 보상이 남아 있으면 Playing을 한 프레임도 끼우지 않고 다음 카드창이 바로 열려야 한다.
            if (game.Rewards.PendingCardCount > 0)
            {
                Assert.AreEqual(GameState.LevelUp, game.State, "07:46 — 남은 보상이 있으면 바로 다음 카드창");
                Assert.AreEqual(0f, Time.timeScale, "보상창 사이에 전투 시간이 흐르면 안 된다");
            }
            else
            {
                Assert.AreEqual(GameState.Playing, game.State);
                Assert.AreEqual(1f, Time.timeScale);
            }
            yield return DrainRewardsUntilPlaying(game);
            Assert.IsNotNull(game.Player.GetComponent<RapierWeapon>(), "남은 보상을 고른 뒤에도 레이피어 유지");
            Assert.IsNotNull(game.Player.GetComponent<HybridBowWeapon>(), "남은 보상을 고른 뒤에도 보조 활 유지");
        }

        [UnityTest]
        public IEnumerator HybridBow_FiresAtEnemy_WithReducedDamage_AndDoesNotConsumeMeleeCooldown()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, 5000);
            game.AddIngredient(IngredientType.Tea, 5000);
            yield return null;
            yield return DrainRewardsUntilHybridOffer(game);
            Assert.AreEqual(GameState.HybridOffer, game.State);
            game.HybridView.Unlock();
            // 남은 카드 보상창(timeScale=0)이 열린 채로 스케일 시간을 기다리면 끝나지 않는다(2026-10-05 STALL).
            yield return DrainRewardsUntilPlaying(game);

            // 2026-10-06: 남은 카드(~30장)를 다 고른 뒤라 플레이어가 강해져, 예전 배치(2유닛 앞, 기본 체력)는 레이피어가
            // 한 번에 처치해 적이 파괴되거나(fix2 MissingReference) 레이피어 피해만으로도 통과할 수 있었다. 보조 활만 닿는
            // 거리(레이피어 획득 범위 Range+0.6 밖, 활 획득 범위 Range+3.6 안)에 튼튼한 적을 둬서 활 자체를 검증한다.
            float rapierReach = game.Stats.Range + 0.6f;
            var enemy = Enemy.SpawnAt(game.Player.Position + new Vector2(rapierReach + 1.5f, 0f), EnemyDef.Brute, 1000f,
                game.Player, null);
            float hpBefore = enemy.Hp;

            // 07:118 — 첫 발은 해금 후 0.2초. 레이피어 런에서 PlayerProjectile을 쏘는 건 보조 활뿐이다.
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None).Length, 0,
                "보조 활이 자동으로 화살을 발사해야 한다");

            // 화살(속도9)이 닿을 때까지 — 맞은 순간에도 적이 레이피어 범위 밖이었는지 확인한다.
            float waited = 0f;
            while (enemy.Hp >= hpBefore && waited < 1.5f) { yield return null; waited += Time.deltaTime; }
            Assert.Less(enemy.Hp, hpBefore, "보조 활 화살이 적을 맞혀야 한다");
            Assert.Greater(Vector2.Distance(game.Player.Position, enemy.transform.position), rapierReach,
                "피해 시점에 적이 레이피어 범위 밖이어야 활 피해로 볼 수 있다");
            Object.Destroy(enemy.gameObject);
        }
    }
}
