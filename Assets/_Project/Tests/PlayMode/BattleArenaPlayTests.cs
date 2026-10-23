using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>57 P0-1 실제 런: 한 방향 지속 이동이 벽에서 멈추고, 화면 밖 보스 방향 표시가 뜨고, 도망쳐도 보스와 만나며,
    /// 벽에 붙었을 때 웨이브·보스가 구역 안 반대쪽에서 나오고 안내 문구가 실제 쪽을 말하는지.</summary>
    public class BattleArenaPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            GameManager.Instance.Player.GrantInvulnerability(9999f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.Player != null) GameManager.Instance.Player.AutoInputOverride = null;
                Object.Destroy(GameManager.Instance.gameObject);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        static void DismissPopups(GameManager game)
        {
            if (game.State != GameState.LevelUp || game.CardView == null) return;
            // First-run card education is intentionally modal; close it before auto-selecting in movement tests.
            if (game.CardView.IsLearningOpen) game.CardView.CloseLearning();
            if (!game.CardView.IsLearningOpen) game.CardView.Choose(0);
        }

        [UnityTest]
        public IEnumerator HoldingOneDirection_StopsAtWall_AndCameraStopsAtLimit([Values(1f, -1f)] float dir)
        {
            var game = GameManager.Instance;
            var cam = Camera.main;
            game.Player.AutoInputOverride = new Vector2(dir, 0f);
            Time.timeScale = 4f;
            float until = Time.time + 16f; // 이동 5 × 16초 = 80 > 구역 폭 60
            while (Time.time < until) { DismissPopups(game); yield return null; }
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.5f);

            float px = game.Player.Position.x * dir; // 오른쪽 기준으로 접어서 같은 규칙 확인
            Assert.LessOrEqual(px, BattleArena.MaxX - PlayerController.Radius + 0.01f, "벽을 넘으면 안 된다");
            Assert.Greater(px, BattleArena.MaxX - 1.5f, "벽까지는 갈 수 있어야 한다");
            float limit = BattleArena.CameraLimit(cam.orthographicSize * cam.aspect);
            Assert.LessOrEqual(cam.transform.position.x * dir, limit + 0.4f, "카메라는 벽 너머 OutsideReveal까지만");
            foreach (var e in Enemy.Active)
                Assert.IsTrue(BattleArena.Contains(e.transform.position.x, 0f), $"{e.name} x={e.transform.position.x}");
        }

        [UnityTest]
        public IEnumerator BossOffscreen_ShowsDirection_AndFleeingPlayerStillMeetsBoss_ThenHidesOnDeath()
        {
            var game = GameManager.Instance;
            var cam = Camera.main;
            var boss = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.GrinderGolem, hpScale = 50f });
            var rb = boss.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(BattleArena.MinX + 2f, Balance.LaneCenterY);
            boss.transform.position = new Vector3(BattleArena.MinX + 2f, Balance.LaneCenterY, 0f); // 물리 스텝 전 프레임에도 같은 위치
            game.Player.AutoInputOverride = Vector2.right; // 보스 반대쪽으로 계속 도망
            yield return null;
            yield return null;

            var indicator = game.Hud.BossIndicator;
            Assert.IsTrue(indicator.Visible, "화면 밖 보스는 방향 표시");
            Assert.AreEqual(-1f, indicator.Side, "왼쪽 보스는 왼쪽 화살표");
            StringAssert.Contains("◀", indicator.LabelText);

            Time.timeScale = 4f;
            bool met = false;
            float until = Time.time + 60f;
            while (Time.time < until)
            {
                DismissPopups(game);
                if (boss == null || boss.IsDead) break;
                float halfView = cam.orthographicSize * cam.aspect;
                if (!BossDirectionIndicator.IsOffscreen(cam.transform.position.x, halfView, boss.transform.position.x, boss.Def.radius, out _))
                {
                    met = true;
                    break;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.IsTrue(met, "한 방향으로 도망쳐도 벽에서 멈춰 보스가 화면에 들어와야 한다");
            yield return null;
            Assert.IsFalse(indicator.Visible, "보스가 화면 안이면 표시를 숨긴다");

            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds);
            boss.TakeDamage(1e9f);
            yield return null;
            yield return null;
            Assert.IsFalse(indicator.Visible, "보스가 죽으면 표시가 사라진다");
        }

        [UnityTest]
        public IEnumerator AtRightWall_WaveAndBossComeFromLeft_InsideArena_AndNoticeSaysSo()
        {
            var game = GameManager.Instance;
            var cam = Camera.main;
            game.Player.transform.position = new Vector3(BattleArena.MaxX - 1f, Balance.LaneCenterY, 0f);
            yield return new WaitForSeconds(1.2f); // 카메라 수렴
            foreach (var e in Enemy.Active.ToArray()) Object.Destroy(e.gameObject);
            yield return null;

            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            spawner.SpawnWaveForTests();
            Assert.AreEqual("왼쪽", spawner.LastWaveSides, "벽 너머(오른쪽)에서는 나올 수 없다");
            float halfView = cam.orthographicSize * cam.aspect;
            foreach (var e in Enemy.Active)
            {
                Assert.IsTrue(BattleArena.Contains(e.transform.position.x, e.Def.radius - 0.001f));
                Assert.Less(e.transform.position.x, cam.transform.position.x - halfView, "화면 밖 왼쪽에서 걸어 들어온다");
            }

            for (int i = 0; i < 6; i++)
            {
                var boss = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.GrinderGolem, hpScale = 1f });
                Assert.IsTrue(BattleArena.Contains(boss.transform.position.x, boss.Def.radius - 0.001f));
                Assert.Less(boss.transform.position.x, game.Player.Position.x, "보스도 구역 안 왼쪽");
                Object.DestroyImmediate(boss.gameObject);
                game.ActiveBoss = null;
            }
        }

        [UnityTest]
        public IEnumerator NearCap_WaveBannerNamesOnlyTheSideThatActuallySpawned()
        {
            var game = GameManager.Instance;
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            foreach (var e in Enemy.Active.ToArray()) Object.Destroy(e.gameObject);
            yield return null;
            int cap = Mathf.Min(spawner.RegularCap, Balance.MaxAliveEnemiesAbsolute - 1);
            while (Enemy.Active.Count < cap - 1)
                Enemy.SpawnAt(new Vector2(BattleArena.MinX + 3f, Balance.LaneCenterY), EnemyDef.LeafWarden, 1f, game.Player, game.transform);
            int before = spawner.WaveSpawns;
            spawner.SpawnWaveForTests();
            Assert.AreEqual(1, spawner.WaveSpawns - before, "상한 직전에는 한 마리만");
            Assert.AreEqual("왼쪽", spawner.LastWaveSides);
            StringAssert.Contains("왼쪽에서", game.Hud.BannerSubText, "한쪽만 나왔으면 '양쪽'이라고 하지 않는다");
            Assert.LessOrEqual(Enemy.Active.Count, cap);
        }
    }
}
