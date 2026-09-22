using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>총/활(PlayerProjectileWeapon, PlayerProjectile)을 격리해서 검증한다 (GameBootstrap 없이).</summary>
    public class PlayerProjectileWeaponTests
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
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutoFires_AtEnemy_BeyondMeleeRange()
        {
            playerGo.AddComponent<PlayerProjectileWeapon>().Init(stats, player);

            // 근접 사거리(baseRange 2.2)보다는 멀지만, 원거리 무기는 닿아야 하는 거리
            var pos = player.Position + new Vector2(stats.Range + 2f, 0f);
            var enemy = Enemy.SpawnAt(pos, EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return new WaitForSeconds(stats.AttackInterval + 1.5f); // 발사 + 탄 도달 시간 여유

            Assert.Less(enemy.Hp, hpBefore, "원거리 무기는 근접 사거리 밖의 적도 맞혀야 한다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator ExtraThrust_FiresMultipleProjectiles_InASpread()
        {
            stats.Apply(new CardEffect(CardEffectType.ExtraThrust, 2f)); // 총 3발
            playerGo.AddComponent<PlayerProjectileWeapon>().Init(stats, player);

            var enemy = Enemy.SpawnAt(player.Position + new Vector2(3f, 0f), EnemyDef.Brute, 1f, player, null);

            yield return null; // 첫 프레임에 즉시 발사됨 (cooldown 초기값 0)

            var projectiles = Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None);
            Assert.AreEqual(3, projectiles.Length, "연속 공격 +2 카드는 원거리에서 3발을 동시에 쏴야 한다");

            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator PlayerProjectile_HitsEnemy_ThenDestroysItself()
        {
            var enemy = Enemy.SpawnAt(player.Position + new Vector2(2f, 0f), EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            PlayerProjectile.Spawn(player.Position, Vector2.right, 9f, 15f, 0, null);
            yield return new WaitForSeconds(0.4f); // 2유닛 / 9유닛초 ≈ 0.22초면 명중

            Assert.Less(enemy.Hp, hpBefore);
            Assert.IsNull(Object.FindFirstObjectByType<PlayerProjectile>(), "관통이 없으면 명중 즉시 사라져야 한다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator PlayerProjectile_Miss_ExpiresAfterLifetime()
        {
            PlayerProjectile.Spawn(player.Position, Vector2.up, 9f, 15f, 0, null); // 아무도 없는 방향

            yield return new WaitForSeconds(2.2f); // Lifetime(2초) 초과

            Assert.IsNull(Object.FindFirstObjectByType<PlayerProjectile>(), "수명이 다하면 스스로 사라져야 한다");
        }

        [UnityTest]
        public IEnumerator PlayerProjectile_WithPierce_HitsMultipleEnemies_WithoutDoubleHitting()
        {
            var enemyA = Enemy.SpawnAt(player.Position + new Vector2(2f, 0f), EnemyDef.Brute, 1f, player, null);
            var enemyB = Enemy.SpawnAt(player.Position + new Vector2(4f, 0f), EnemyDef.Brute, 1f, player, null);
            float hpABefore = enemyA.Hp;
            float hpBBefore = enemyB.Hp;

            PlayerProjectile.Spawn(player.Position, Vector2.right, 9f, 15f, 1, null); // 관통 +1

            yield return new WaitForSeconds(0.15f); // A만 지나칠 시점 (2유닛 / 9유닛초 ≈ 0.22초)
            Assert.Less(enemyA.Hp, hpABefore, "관통 발사체는 첫 번째 몹도 맞혀야 한다");

            yield return new WaitForSeconds(0.4f); // B까지 도달 + 관통 소진 후 소멸할 시간
            Assert.Less(enemyB.Hp, hpBBefore, "관통 발사체는 두 번째 몹까지 뚫고 지나가야 한다");
            Assert.AreEqual(hpABefore - 15f, enemyA.Hp, 0.01f, "같은 몹을 두 번 때리면 안 된다");
            Assert.IsNull(Object.FindFirstObjectByType<PlayerProjectile>(), "관통 횟수를 다 쓰면 사라져야 한다");

            Object.Destroy(enemyA.gameObject);
            Object.Destroy(enemyB.gameObject);
        }
    }
}
