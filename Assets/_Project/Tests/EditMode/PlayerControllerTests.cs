using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    public class PlayerControllerTests
    {
        static PlayerController CreatePlayer()
        {
            var go = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(SpriteRenderer));
            var player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white);
            return player;
        }

        [Test]
        public void SetFacing_PointsTowardTargetSide()
        {
            var player = CreatePlayer();

            player.SetFacing(player.Position.x + 5f);
            Assert.AreEqual(1f, player.Facing);

            player.SetFacing(player.Position.x - 5f);
            Assert.AreEqual(-1f, player.Facing);

            Object.DestroyImmediate(player.gameObject);
        }

        [Test]
        public void SetFacing_SamePosition_KeepsPreviousFacing()
        {
            var player = CreatePlayer();
            player.SetFacing(player.Position.x + 5f);
            Assert.AreEqual(1f, player.Facing);

            player.SetFacing(player.Position.x); // 같은 위치 → 방향 유지
            Assert.AreEqual(1f, player.Facing);

            Object.DestroyImmediate(player.gameObject);
        }
    }
}
