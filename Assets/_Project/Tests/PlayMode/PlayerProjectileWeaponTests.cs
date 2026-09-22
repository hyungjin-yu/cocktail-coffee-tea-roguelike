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

            PlayerProjectile.Spawn(player.Position, Vector2.right, 9f, 15f, null);
            yield return new WaitForSeconds(0.4f); // 2유닛 / 9유닛초 ≈ 0.22초면 명중

            Assert.Less(enemy.Hp, hpBefore);
            Assert.IsNull(Object.FindFirstObjectByType<PlayerProjectile>(), "명중하면 곧바로 사라져야 한다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator PlayerProjectile_Miss_ExpiresAfterLifetime()
        {
            PlayerProjectile.Spawn(player.Position, Vector2.up, 9f, 15f, null); // 아무도 없는 방향

            yield return new WaitForSeconds(2.2f); // Lifetime(2초) 초과

            Assert.IsNull(Object.FindFirstObjectByType<PlayerProjectile>(), "수명이 다하면 스스로 사라져야 한다");
        }
    }
}
