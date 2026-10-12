using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>06:148 — 런 종료 우선순위(플레이어 사망 → 최종보스 처치 → 시간 제한)와
    /// 20분 시간 제한 자체를 검증한다.</summary>
    public class RunEndPriorityTests
    {
        static BossDirector.Spec Final() =>
            new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f };

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
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TimeLimitReached_WithoutClearing_EndsRunAsFailure()
        {
            var game = GameManager.Instance;
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(game.Cleared, "최종보스를 못 잡고 시간이 다 되면 실패로 끝나야 한다");
            Assert.AreEqual("시간 초과", game.EndReason);
        }

        [UnityTest]
        public IEnumerator SimultaneousPlayerDeathAndBossKill_PlayerDeathTakesPriority()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(Final());
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f); // 06:24 등장 안내 동안은 무적

            // 같은 프레임 안에서 둘 다 트리거 — LateUpdate가 한 번에 우선순위대로 해소해야 한다.
            boss.TakeDamage(999999f);
            game.Player.TakeDamage(9999f);
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(game.Cleared, "같은 틱에 플레이어도 죽으면 최종보스를 잡았어도 실패로 처리돼야 한다(06:148)");
            Assert.AreEqual("사망", game.EndReason);
        }

        [UnityTest]
        public IEnumerator BossKillSameTickAsTimeLimit_StillCountsAsClear()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(Final());
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);

            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            boss.TakeDamage(999999f);
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsTrue(game.Cleared, "같은 틱에 시간이 다 돼도 최종보스를 잡았으면 클리어가 우선해야 한다(06:148)");
            Assert.AreEqual("클리어", game.EndReason);
        }
    }
}
