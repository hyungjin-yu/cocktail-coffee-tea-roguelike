using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>발사체 아트(물방울/얼음조각)는 위가 뾰족하고 아래(로컬 -Y)가 두꺼운 모양이라,
    /// 두꺼운 쪽이 진행 방향을 향하도록 회전해야 한다.</summary>
    public class EnemyProjectileRotationTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
        }

        static void AssertFacesDirection(Vector2 direction, float expectedAngle)
        {
            EnemyProjectile.Spawn(Vector2.zero, direction, 1f, 1f, null, null);
            var p = Object.FindFirstObjectByType<EnemyProjectile>();

            float actual = p.transform.rotation.eulerAngles.z;
            float diff = Mathf.DeltaAngle(actual, expectedAngle);
            Assert.AreEqual(0f, diff, 0.1f, $"dir={direction} 이면 로컬 -Y(두꺼운 쪽)가 그 방향을 향해야 한다");

            Object.DestroyImmediate(p.gameObject);
        }

        [Test]
        public void FacingRight_RotatesSoBottomPointsRight()
        {
            AssertFacesDirection(Vector2.right, 90f);
        }

        [Test]
        public void FacingLeft_RotatesSoBottomPointsLeft()
        {
            AssertFacesDirection(Vector2.left, -90f);
        }

        [Test]
        public void FacingDown_NoRotationNeeded_BottomAlreadyPointsDown()
        {
            AssertFacesDirection(Vector2.down, 0f);
        }

        [Test]
        public void FacingUp_RotatesHalfCircle()
        {
            AssertFacesDirection(Vector2.up, 180f);
        }

        [Test]
        public void ZeroDirection_LeavesRotationUnchanged()
        {
            EnemyProjectile.Spawn(Vector2.zero, Vector2.zero, 1f, 1f, null, null);
            var p = Object.FindFirstObjectByType<EnemyProjectile>();

            Assert.AreEqual(Quaternion.identity, p.transform.rotation);
        }
    }
}
