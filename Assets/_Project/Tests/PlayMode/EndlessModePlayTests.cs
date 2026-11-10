using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-10 실제 프레임(Update/LateUpdate)으로 끝없는 재료 모으기: 웨이브 진행·생성·일시정지 정지·선택 화면·정규 모드 회귀.</summary>
    public class EndlessModePlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) Object.Destroy(v.gameObject);
            RunModeSelection.Current = RunMode.Standard;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Endless_RealFrames_WaveSpawnsEnemies_PauseStopsProgress_NoBoss()
        {
            RunModeSelection.Current = RunMode.Endless;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var game = GameManager.Instance;
            Assert.AreEqual(RunMode.Endless, game.Mode);
            Time.timeScale = 8f;
            float until = Time.realtimeSinceStartup + 8f;
            while (game.Spawner.EndlessSpawned < 3 && Time.realtimeSinceStartup < until)
            {
                if (game.State == GameState.LevelUp) game.CardView.Choose(0);
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.GreaterOrEqual(game.Spawner.EndlessSpawned, 3, "웨이브 1 분산 생성");
            Assert.AreEqual(1, game.Spawner.EndlessWave);
            Assert.IsNull(game.ActiveBoss);
            StringAssert.Contains("끝없는 재료 모으기", game.Hud.PhaseBadge != null ? RunPhaseBadge.Compute(game).text : "");

            game.OpenPause();
            yield return null;
            float t = game.RunTime;
            int spawned = game.Spawner.EndlessSpawned;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(t, game.RunTime, "일시정지 중 웨이브 시간 정지");
            Assert.AreEqual(spawned, game.Spawner.EndlessSpawned, "일시정지 중 생성 없음");
            game.GiveUp();
            yield return null;
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual("포기", game.EndReason);
            Assert.AreEqual("Endless", game.RunRecord.mode);
        }

        [UnityTest]
        public IEnumerator Standard_RealFrames_UsesRegularSpawner_NotEndless()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var game = GameManager.Instance;
            Assert.AreEqual(RunMode.Standard, game.Mode);
            for (int i = 0; i < 90; i++) yield return null;
            Assert.AreEqual(0, game.Spawner.EndlessSpawned, "정규 모드는 끝없는 스포너를 쓰지 않음");
            Assert.AreEqual(4, game.Director.ScheduleCount);
        }

        [UnityTest]
        public IEnumerator CharacterSelect_ModeToggle_DefaultsStandard_ShowsRewardLine()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.BartenderButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(view.EndlessModeButton);
            Assert.AreEqual(RunMode.Standard, RunModeSelection.Current, "기본은 기존 모드");
            view.EndlessModeButton.onClick.Invoke();
            Assert.AreEqual(RunMode.Endless, RunModeSelection.Current);
            StringAssert.Contains(RunModeSelection.EndlessRewardLine, view.ModeHintText);
            view.StandardModeButton.onClick.Invoke();
            Assert.AreEqual(RunMode.Standard, RunModeSelection.Current);
            Object.Destroy(view.gameObject);
        }
    }
}
