using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>벨트스크롤(던파식) 시점 전환: 깊이 레인 클램프, 카메라 좌우 전용 추적, 화면 밖 스폰을 확인한다.</summary>
    public class BeltScrollTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard; // 로컬 PlayerPrefs 값과 무관하게 결정론적으로
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDepth_IsClampedToLane()
        {
            var player = GameManager.Instance.Player;

            player.transform.position = new Vector3(3f, 50f, 0f); // 레인 폭을 훌쩍 넘는 위치
            // Rigidbody2D.MovePosition 의 결과는 다음 물리 스텝에야 Transform 에 반영되므로 여유 있게 기다린다
            yield return new WaitForSeconds(0.1f);

            Assert.LessOrEqual(player.Position.y, Balance.DepthMax + 0.01f);

            player.transform.position = new Vector3(3f, -50f, 0f);
            yield return new WaitForSeconds(0.1f);

            Assert.GreaterOrEqual(player.Position.y, Balance.DepthMin - 0.01f);
        }

        [UnityTest]
        public IEnumerator Camera_FollowsPlayerX_ButNotY()
        {
            var cam = Camera.main;
            float baselineCamY = cam.transform.position.y;

            GameManager.Instance.Player.transform.position = new Vector3(20f, 1f, 0f);
            yield return new WaitForSeconds(1f); // 좌우 추적 Lerp 가 충분히 수렴하도록

            Assert.AreEqual(baselineCamY, cam.transform.position.y, 0.001f, "카메라는 위아래로 따라가면 안 된다");
            Assert.Less(Mathf.Abs(cam.transform.position.x - 20f), 0.2f, "카메라는 좌우로는 플레이어를 따라가야 한다");
        }

        [UnityTest]
        public IEnumerator Spawner_SpawnsEnemies_WithinDepthBand()
        {
            // 화면 밖 스폰인지(카메라 화면비 의존)는 결정론적으로 BeltScroll.EdgePosition 자체를
            // EditMode(BeltScrollUtilTests)에서 검증한다. 여기서는 실제 스폰이 일어나고
            // 깊이 레인 안에 들어오는지만 확인한다.
            yield return new WaitForSeconds(1.3f); // 트리클 스폰 간격을 넘기기 충분한 시간

            Assert.Greater(Enemy.Active.Count, 0, "스폰이 한 번은 일어나야 한다");

            foreach (var enemy in Enemy.Active)
            {
                Assert.GreaterOrEqual(enemy.transform.position.y, Balance.DepthMin - 0.01f);
                Assert.LessOrEqual(enemy.transform.position.y, Balance.DepthMax + 0.01f);
            }
        }
    }
}
