using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// PoisonAura 만 격리해서 검증 (GameBootstrap 을 쓰지 않음).
    /// 전체 게임을 띄우면 플레이어의 레이피어 자동공격도 같은 몹을 때려서 피해량이 섞이기 때문.
    /// </summary>
    public class PoisonAuraTests
    {
        GameObject playerGo;
        PlayerController player;
        PlayerStats stats;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            playerGo = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(SpriteRenderer));
            player = playerGo.AddComponent<PlayerController>();
            stats = new PlayerStats();
            player.Init(stats, Color.white);
            playerGo.AddComponent<PoisonAura>().Init(stats, player);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PoisonLevel_Zero_DoesNothing()
        {
            var enemy = Enemy.SpawnAt(player.Position, EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(hpBefore, enemy.Hp, "poisonLevel 이 0이면 아무 피해도 없어야 한다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator PoisonLevel_AppliesPeriodicDamage_ToNearbyEnemies()
        {
            stats.Apply(new CardEffect(CardEffectType.PoisonLevel, 1f));

            var enemy = Enemy.SpawnAt(player.Position, EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return new WaitForSeconds(2.7f); // 초기 간격(2.6초 - 0.2×1 = 2.4초)을 넘기기 충분히

            Assert.Less(enemy.Hp, hpBefore, "독 오라가 주기적으로 주변 몹에 피해를 줘야 한다");
            Object.Destroy(enemy.gameObject);
        }
    }
}
