using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>PlayerController.ApplyKnockback (강화 보스의 "압력 게이지 폭발" 패턴이 사용).</summary>
    public class PlayerKnockbackTests
    {
        GameObject playerGo;
        PlayerController player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard; // 키 입력 없음 → 넉백만 관찰
            playerGo = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(SpriteRenderer));
            player = playerGo.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApplyKnockback_PushesPlayer_ThenStopsAfterDuration()
        {
            Vector2 start = player.Position;
            player.ApplyKnockback(Vector2.right * 6f, 0.25f);

            yield return new WaitForSeconds(0.15f);
            Assert.Greater(Vector2.Distance(start, player.Position), 0.3f,
                "입력이 없어도 넉백 중에는 밀려나야 한다");

            yield return new WaitForSeconds(0.3f); // duration(0.25초) 경과 후 정착
            float settledX = player.Position.x;
            yield return new WaitForSeconds(0.15f);

            Assert.AreEqual(settledX, player.Position.x, 0.01f, "넉백이 끝나면 입력 없이 더 밀리면 안 된다");
        }
    }
}
