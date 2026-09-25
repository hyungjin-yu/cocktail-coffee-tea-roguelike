using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>중간보스 B: 드립 서버 스펙터 패턴(DripSpecterDriver) — 드립 웨이브 / 투명화.</summary>
    public class DripSpecterDriverTests
    {
        /// <summary>대기 중 레벨업/스킬 팝업이 뜨면(앰비언트 스폰) 시간이 멈추므로 자동으로 닫는다 (BossFlowTests 참고).</summary>
        static void DismissPopups(GameManager game)
        {
            if (game.State == GameState.LevelUp)
                game.CardView.Choose(0);
            else if (game.State == GameState.SkillSelect)
                game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
        }

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
        public IEnumerator DripWave_SpawnsBurstZones_NearPlayer()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); // 접촉 피해로 죽어서 대기가 멈추지 않게
            var specter = Enemy.SpawnAt(game.Player.Position + new Vector2(3f, 0f), EnemyDef.DripServerSpecter,
                1f, game.Player, game.transform);
            specter.gameObject.AddComponent<DripSpecterDriver>().Init(specter, game);

            // 등장 안내(1초, 06:24) + 첫 패턴(드립 웨이브)까지 텀(2.5초) + 순차 생성(4발×0.4초)을 넘기기 충분히 대기
            float deadline = Time.time + 6f;
            while (Time.time < deadline)
            {
                DismissPopups(game);
                yield return null;
            }

            var zones = Object.FindObjectsByType<BurstZone>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(zones.Length, 1, "드립 웨이브는 장판을 순서대로 생성해야 한다");
        }

        [UnityTest]
        public IEnumerator Invisibility_GrantsTemporaryInvulnerability_ThenTeleportsNearPlayer()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); // 접촉 피해로 죽어서 대기가 멈추지 않게
            var specter = Enemy.SpawnAt(game.Player.Position + new Vector2(3f, 0f), EnemyDef.DripServerSpecter,
                1f, game.Player, game.transform);
            specter.gameObject.AddComponent<DripSpecterDriver>().Init(specter, game);

            // 두 번째 패턴(투명화)까지: 등장 안내(1초, 06:24) + 첫 텀(2.5초) + 쿨타임(6초) = 9.5초,
            // 그 뒤 무적 1.2초 → 여유 있게 11.5초
            float deadline = Time.time + 11.5f;
            bool wasInvulnerable = false;
            bool wasFrozenWhileInvulnerable = false;
            var rb = specter.GetComponent<Rigidbody2D>();
            while (Time.time < deadline)
            {
                if (specter != null && specter.Invulnerable)
                {
                    wasInvulnerable = true;
                    if (rb.linearVelocity == Vector2.zero) wasFrozenWhileInvulnerable = true;
                }
                DismissPopups(game);
                yield return null;
            }

            Assert.IsTrue(wasInvulnerable, "투명화 중에는 한 번은 무적 상태였어야 한다");
            Assert.IsTrue(wasFrozenWhileInvulnerable, "06:123 — 투명화 중에는 이동도 멈춰야 한다");
            Assert.IsNotNull(specter, "이 시나리오에서 스펙터가 죽으면 안 된다");
            Assert.IsFalse(specter.Invulnerable, "재등장 후에는 무적이 풀려 있어야 한다");
        }

        [UnityTest]
        public IEnumerator DripWave_DoesNotSpawnZoneImmediately_WaitsOutTelegraph()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            var specter = Enemy.SpawnAt(game.Player.Position + new Vector2(3f, 0f), EnemyDef.DripServerSpecter,
                1f, game.Player, game.transform);
            specter.gameObject.AddComponent<DripSpecterDriver>().Init(specter, game);

            // 등장 안내(1초) + 첫 텀(2.5초) 직후, 첫 장판의 예고(0.6초)가 끝나기 전 시점을 관찰
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 2.5f + 0.2f);

            Assert.IsNull(Object.FindFirstObjectByType<BurstZone>(),
                "06:121 — 웨이브 첫 점도 예고(0.6초) 없이 즉시 장판이 생기면 안 된다");
        }
    }
}
