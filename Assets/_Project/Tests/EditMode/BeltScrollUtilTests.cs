using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    public class BeltScrollUtilTests
    {
        static Camera CreateCamera(float orthoSize, float aspect, Vector3 position)
        {
            var go = new GameObject("TestCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.aspect = aspect; // 명시적으로 지정하면 실제 화면 해상도와 무관하게 고정된다
            cam.transform.position = position;
            return cam;
        }

        [Test]
        public void EdgePosition_RightSide_IsOutsideVisibleWidth()
        {
            var cam = CreateCamera(6f, 16f / 9f, new Vector3(10f, 0f, -10f));
            float halfWidth = cam.orthographicSize * cam.aspect;

            var pos = BeltScroll.EdgePosition(cam, 1f, 1.5f);

            Assert.AreEqual(cam.transform.position.x + halfWidth + 1.5f, pos.x, 0.001f);
            Assert.GreaterOrEqual(pos.y, Balance.DepthMin);
            Assert.LessOrEqual(pos.y, Balance.DepthMax);

            Object.DestroyImmediate(cam.gameObject);
        }

        [Test]
        public void EdgePosition_LeftSide_IsOutsideVisibleWidth()
        {
            var cam = CreateCamera(6f, 16f / 9f, new Vector3(10f, 0f, -10f));
            float halfWidth = cam.orthographicSize * cam.aspect;

            var pos = BeltScroll.EdgePosition(cam, -1f, 1.5f);

            Assert.AreEqual(cam.transform.position.x - halfWidth - 1.5f, pos.x, 0.001f);

            Object.DestroyImmediate(cam.gameObject);
        }

        [Test]
        public void EdgePosition_FollowsCameraX_NotFixedOrigin()
        {
            var cam = CreateCamera(6f, 1f, new Vector3(37f, 0f, -10f));
            var pos = BeltScroll.EdgePosition(cam, 1f, 1.5f);

            Assert.AreEqual(37f + 6f + 1.5f, pos.x, 0.001f);

            Object.DestroyImmediate(cam.gameObject);
        }

        [Test]
        public void EdgePosition_NullCamera_FallsBackGracefully()
        {
            var pos = BeltScroll.EdgePosition(null, 1f, 1.5f);
            Assert.GreaterOrEqual(pos.y, Balance.DepthMin);
            Assert.LessOrEqual(pos.y, Balance.DepthMax);
        }
    }
}
