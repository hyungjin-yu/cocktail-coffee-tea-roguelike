using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>레이피어/검 공용(RapierWeapon)의 근접 판정을 격리해서 검증한다 (GameBootstrap 없이).
    /// 2026-09-25 버그 리포트("VFX 밖에서도 몹이 맞는다")를 계기로 찌르기→반원 슬래시로 재설계했고,
    /// 판정과 VFX가 항상 같은 반원(사거리 반경, 바라보는 방향 180°)을 쓴다는 걸 지키는 회귀 테스트.</summary>
    public class RapierWeaponTests
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
            playerGo.AddComponent<RapierWeapon>().Init(stats, player);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slash_HitsEnemy_DirectlyInFront()
        {
            var enemy = Enemy.SpawnAt(player.Position + new Vector2(1f, 0f), EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return null; // cooldown 초기값 0 — 첫 프레임에 즉시 슬래시

            Assert.Less(enemy.Hp, hpBefore, "바라보는 방향 정면의 적은 맞아야 한다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator Slash_HitsEnemy_DiagonallyWithinForwardSemicircle()
        {
            // 반원이라 정면 직선이 아니어도, 사거리 반경 안이고 바라보는 쪽 반평면이면 맞아야 한다.
            var enemy = Enemy.SpawnAt(player.Position + new Vector2(1f, 1f), EnemyDef.Brute, 1f, player, null);
            float hpBefore = enemy.Hp;

            yield return null;

            Assert.Less(enemy.Hp, hpBefore, "바라보는 쪽 반원 안이면 대각선 방향도 맞아야 한다");
            Object.Destroy(enemy.gameObject);
        }

        [UnityTest]
        public IEnumerator Slash_Misses_EnemyBehindPlayer()
        {
            // 정면의 가까운 적을 향해 바라보게 만든 뒤, 사거리 반경 안이지만 등 뒤(반대쪽 반원)에 있는
            // 다른 적은 베이지 않아야 한다.
            var front = Enemy.SpawnAt(player.Position + new Vector2(0.5f, 0f), EnemyDef.Brute, 1f, player, null);
            var behind = Enemy.SpawnAt(player.Position + new Vector2(-1.5f, 0f), EnemyDef.Brute, 1f, player, null);
            float behindHpBefore = behind.Hp;

            yield return null;

            Assert.AreEqual(behindHpBefore, behind.Hp, "등 뒤(바라보는 반대쪽 반원)의 적은 맞으면 안 된다");
            Object.Destroy(front.gameObject);
            Object.Destroy(behind.gameObject);
        }
    }
}
