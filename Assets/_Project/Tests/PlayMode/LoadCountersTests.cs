using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>10 §3 고정 부하 시나리오가 유지 개수를 판단하는 ActiveCount가 실제 생성/파괴와 일치하는지(GameBootstrap 없이).</summary>
    public class LoadCountersTests
    {
        GameObject playerGo;
        PlayerController player;
        GameObject parent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            playerGo = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(SpriteRenderer));
            player = playerGo.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white);
            parent = new GameObject("LoadCountersRoot");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(parent);
            Object.Destroy(playerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveCounts_FollowSpawnAndDestroy()
        {
            int e0 = EnemyProjectile.ActiveCount, p0 = PlayerProjectile.ActiveCount, b0 = BurstZone.ActiveCount,
                s0 = SlowZone.ActiveCount, i0 = IngredientPickup.ActiveCount;
            var far = player.Position + new Vector2(40f, 0f);

            EnemyProjectile.Spawn(far, Vector2.right, 1f, 1f, player, parent.transform);
            PlayerProjectile.Spawn(far, Vector2.right, 1f, 1f, 0, parent.transform);
            BurstZone.Spawn(far, 0.5f, 1f, 30f, player, parent.transform);
            SlowZone.Spawn(far, 0.5f, 0.5f, 30f, player, parent.transform);
            IngredientPickup.Spawn(null, player, far, IngredientType.Cocktail, 1, parent.transform);
            IngredientPickup.Spawn(null, player, far + Vector2.up, IngredientType.Tea, 1, parent.transform);

            Assert.AreEqual(e0 + 1, EnemyProjectile.ActiveCount, "적 탄");
            Assert.AreEqual(p0 + 1, PlayerProjectile.ActiveCount, "플레이어 탄");
            Assert.AreEqual(b0 + 1, BurstZone.ActiveCount, "화염 장판");
            Assert.AreEqual(s0 + 1, SlowZone.ActiveCount, "설탕 장판");
            Assert.AreEqual(i0 + 2, IngredientPickup.ActiveCount, "픽업");

            Object.Destroy(parent);
            yield return null;

            Assert.AreEqual(e0, EnemyProjectile.ActiveCount, "적 탄 파괴 후");
            Assert.AreEqual(p0, PlayerProjectile.ActiveCount, "플레이어 탄 파괴 후");
            Assert.AreEqual(b0, BurstZone.ActiveCount, "화염 장판 파괴 후");
            Assert.AreEqual(s0, SlowZone.ActiveCount, "설탕 장판 파괴 후");
            Assert.AreEqual(i0, IngredientPickup.ActiveCount, "픽업 파괴 후");
        }
    }
}
