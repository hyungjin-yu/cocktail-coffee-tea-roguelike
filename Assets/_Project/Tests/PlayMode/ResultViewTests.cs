using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>08:107 결과 화면 "영구 강화" 버튼 — 결과 → 영구 강화 → 결과 복귀 흐름
    /// (2026-10-04). 실제 세이브 파일은 건드리지 않도록 임시 경로로 리다이렉트한다.</summary>
    public class ResultViewTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_result_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResultShop_OpensFromResult_AndReturnsToResultOnClose()
        {
            var game = GameManager.Instance;
            game.Player.TakeDamage(99999f);
            yield return null;
            // 2026-10-08 규칙: 사망은 쓰러지는 동작을 보인 뒤(DeathResultDelaySeconds) 결과 패널을 연다.
            yield return new WaitForSecondsRealtime(GameManager.DeathResultDelaySeconds + 0.2f);

            var result = Object.FindFirstObjectByType<ResultView>();
            Assert.IsTrue(result.IsOpen, "전제: 결과 화면이 떠 있어야 한다");

            // 이 결과 화면의 버튼을 직접 누른다(다른 화면의 같은 이름 버튼과 섞이지 않게).
            result.transform.Find("PermanentShopButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            var shop = System.Linq.Enumerable.First(Object.FindObjectsByType<PermanentSkillView>(FindObjectsSortMode.None), v => v.IsOpen);
            Assert.IsTrue(shop.IsOpen, "08:107 — 영구 강화 화면이 열려야 한다");
            Assert.IsFalse(result.IsOpen, "영구 강화 중에는 결과 화면이 가려져야 한다");

            shop.transform.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.IsFalse(shop.IsOpen);
            Assert.IsTrue(result.IsOpen, "닫으면 같은 결과 화면으로 돌아와야 한다");
            Assert.IsTrue(game.Hud.CombatHidden, "38: 영구 강화에서 돌아와도 결과 화면 동안 전투 HUD 숨김 유지");
        }

        [UnityTest]
        public IEnumerator Result_HidesCombatHud_KeepsSaveNotice_AndRestartRestoresHud()
        {
            var game = GameManager.Instance;
            Assert.IsFalse(game.Hud.CombatHidden, "전투 중 HUD 표시");
            game.Player.TakeDamage(99999f);
            yield return null;
            yield return new WaitForSecondsRealtime(GameManager.DeathResultDelaySeconds + 0.2f);
            var result = Object.FindFirstObjectByType<ResultView>();
            Assert.IsTrue(result.IsOpen);
            Assert.IsTrue(game.Hud.CombatHidden, "38: 결과 화면 동안 전투 HUD 숨김");
            var hp = game.Hud.transform.Find("HpBar");
            if (hp != null) Assert.AreEqual(0f, hp.GetComponent<CanvasGroup>().alpha);
            var save = game.Hud.transform.Find("SaveFailed");
            Assert.IsNotNull(save);
            Assert.IsNull(save.GetComponent<CanvasGroup>(), "저장 실패 알림은 결과 화면에서도 숨기지 않는다(08:175)");

            result.transform.Find("RestartButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(GameManager.Instance == null || GameManager.Instance != game, "다시 시작은 이 런을 정리한다");
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            Assert.IsFalse(GameManager.Instance.Hud.CombatHidden, "다시 시작한 런은 HUD가 정상으로 보인다");
        }
    }
}
