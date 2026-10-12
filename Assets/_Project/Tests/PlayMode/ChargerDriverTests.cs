using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>ChargerDriver(그라인더 골렘류) — 06:116 "돌진 방향은 예고 시작 시 고정, 예고 끝에 재조준하지 않는다".</summary>
    public class ChargerDriverTests
    {
        GameObject playerGo;
        PlayerController player;
        GameObject parentGo;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard; // 키 입력 없음 → 텔레포트로만 위치 제어
            playerGo = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(SpriteRenderer));
            player = playerGo.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white);
            parentGo = new GameObject("Parent");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            Object.Destroy(parentGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DashDirection_LocksAtTelegraphStart_IgnoresPlayerMovementDuringTelegraph()
        {
            player.transform.position = new Vector3(5f, 0f, 0f);
            var boss = Enemy.SpawnAt(Vector2.zero, EnemyDef.GrinderGolem, 1f, player, parentGo.transform);
            boss.gameObject.AddComponent<ChargerDriver>().Init(boss);
            var rb = boss.GetComponent<Rigidbody2D>();

            // 등장 안내(1초) + 추적(3.5초)을 넘겨 예고(텔레그래프) 단계 초입으로 이동
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 3.5f + 0.1f);

            Vector2 expectedDir = ((Vector2)player.Position - (Vector2)boss.transform.position).normalized;

            // 예고 중(0.8초) 플레이어를 완전히 다른 방향으로 순간이동 — 재조준되면 안 된다
            player.transform.position = new Vector3(-5f, boss.transform.position.y, 0f);

            yield return new WaitForSeconds(0.85f); // 예고(0.8초)가 끝나고 돌진(Dash)이 시작될 시점

            Vector2 actualDir = rb.linearVelocity.normalized;
            Assert.Greater(Vector2.Dot(expectedDir, actualDir), 0.9f,
                "돌진 방향은 예고 시작 시의 플레이어 위치로 고정돼야 하고, 예고 중 플레이어가 움직여도 재조준되면 안 된다");
        }

        /// <summary>2026-10-08 아트 검수 P1: 예고 띠(warning_line·예고 사각)는 캐릭터(Actor) 아래 바닥 층에 그려 몸을 가리지 않는다.</summary>
        [UnityTest]
        public IEnumerator TelegraphBand_IsDrawnBelowActors()
        {
            player.transform.position = new Vector3(5f, 0f, 0f);
            var boss = Enemy.SpawnAt(Vector2.zero, EnemyDef.GrinderGolem, 1f, player, parentGo.transform);
            boss.gameObject.AddComponent<ChargerDriver>().Init(boss);
            var before = new System.Collections.Generic.HashSet<FadeVfx>(Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None));
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 3.5f + 0.15f); // 예고 시작 직후
            int telegraphs = 0;
            foreach (var v in Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None))
            {
                if (before.Contains(v)) continue;
                var sr = v.GetComponent<SpriteRenderer>();
                if (sr == null || sr.transform.localScale.x < 3f) continue; // 길이 5.4 띠만(작은 효과 제외)
                telegraphs++;
                Assert.Less(sr.sortingOrder, SortOrders.Actor, "예고 띠는 캐릭터 아래 층이어야 한다");
            }
            Assert.Greater(telegraphs, 0, "예고 띠가 실제로 생성돼야 한다");
            Object.Destroy(boss.gameObject);
        }
    }
}
