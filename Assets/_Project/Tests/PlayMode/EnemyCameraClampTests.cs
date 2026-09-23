using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>몹이 한 번 화면에 들어온 뒤로는 카메라 밖으로 나가지 않는지 확인 (Enemy.ClampToCameraView).</summary>
    public class EnemyCameraClampTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
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
        public IEnumerator EnemyThatHasEnteredView_GetsPulledBackIn_IfLeftBehindOutsideCamera()
        {
            var game = GameManager.Instance;
            var cam = Camera.main;

            // 화면 중앙 근처에 스폰 → 한 프레임 뒤 "화면에 들어온" 상태가 된다 (LeafWarden: 제자리 소환형이라
            // 직접 위치를 다시 조작해도 움직여서 결과를 흐리지 않는다).
            var enemy = Enemy.SpawnAt(game.Player.Position, EnemyDef.LeafWarden, 1f, game.Player, game.transform);
            yield return null;

            var rb = enemy.GetComponent<Rigidbody2D>();
            float halfWidth = cam.orthographicSize * cam.aspect;
            // 카메라가 못 따라잡을 만큼 훨씬 바깥으로 순간이동시켜 "뒤에 남겨진" 상황을 재현
            rb.position = new Vector2(cam.transform.position.x + halfWidth * 3f, rb.position.y);
            yield return null;

            float maxX = cam.transform.position.x + halfWidth - Balance.EnemyViewClampMargin - EnemyDef.LeafWarden.radius + 0.01f;
            Assert.LessOrEqual(rb.position.x, maxX, "한 번 화면에 들어온 몹은 카메라 밖으로 남겨지면 안 된다");
        }

        [UnityTest]
        public IEnumerator FreshlySpawnedEnemy_OutsideCamera_IsNotYankedIn_BeforeEnteringView()
        {
            var game = GameManager.Instance;
            var cam = Camera.main;

            var pos = BeltScroll.EdgePosition(cam, 1f, Balance.SpawnEdgeMargin);
            var enemy = Enemy.SpawnAt(pos, EnemyDef.TeabagImp, 1f, game.Player, game.transform);
            var rb = enemy.GetComponent<Rigidbody2D>();
            float spawnX = rb.position.x;
            yield return null;

            float halfWidth = cam.orthographicSize * cam.aspect;
            Assert.Greater(rb.position.x, cam.transform.position.x + halfWidth,
                "화면 밖 스폰 직후에는 진입 연출을 위해 그대로 화면 밖에 있어야 한다");
            Assert.Less(Mathf.Abs(rb.position.x - spawnX), 0.5f, "첫 프레임에는 순간이동하지 않고 자연스럽게 걸어 들어와야 한다");
        }
    }
}
