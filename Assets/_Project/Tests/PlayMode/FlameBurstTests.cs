using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// FlameBurst 만 격리해서 검증 (GameBootstrap 을 쓰지 않음).
    /// 전체 게임을 띄우면 플레이어의 레이피어 자동공격도 같은 몹을 때려서 피해량이 섞이기 때문.
    /// </summary>
    public class FlameBurstTests
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
            playerGo.AddComponent<FlameBurst>().Init(stats, player);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            // 이 테스트는 GameManager 없이 격리해서 도는 거라, 스폰된 파티클이 부모 없이 씬 루트에 남는다
            // (실제 게임에서는 GameManager 밑이라 같이 정리됨). 수명이 남아있으면 다음 테스트로 새는 걸 방지.
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                Object.Destroy(ps.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlameLevel_Zero_DoesNothing()
        {
            var enemy = Enemy.SpawnAt(player.Position, EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(hpBefore, enemy.Hp, "flameLevel이 0이면 아무 피해도 없어야 한다");
            Assert.IsNull(Object.FindFirstObjectByType<ParticleSystem>(), "이펙트도 생기면 안 된다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator FlameLevel_DealsDamage_AndSpawnsParticleBurst()
        {
            stats.Apply(new CardEffect(CardEffectType.FlameLevel, 1f));

            var enemy = Enemy.SpawnAt(player.Position, EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return null; // 첫 프레임에 즉시 발동 (timer 초기값 0)

            Assert.Less(enemy.Hp, hpBefore, "화염 폭발이 주변 몹에 피해를 줘야 한다");

            var particles = Object.FindFirstObjectByType<ParticleSystem>();
            Assert.IsNotNull(particles, "파티클 기반 이펙트가 생겨야 한다");
            Assert.IsTrue(particles.isPlaying, "생성되면 바로 재생 중이어야 한다");

            Object.Destroy(enemy.gameObject);
        }
    }
}
