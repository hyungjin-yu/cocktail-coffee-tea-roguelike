using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>08장 "### 타이틀" (2026-09-28, Codex 오디오/아트와 무관). "종료" 버튼의 실제 클릭은
    /// 여기서 검증하지 않는다 — 저장 실패가 없을 때는 곧장 DoQuit()으로 이어지는데, 에디터에서
    /// EditorApplication.isPlaying=false를 호출해버리면 이 테스트가 실행되는 배치/PlayMode 세션 자체가
    /// 끊긴다. 확인창이 뜨는지/버튼 존재만 코드 리뷰 + 아래 간접 시나리오로 확인한다.</summary>
    public class TitleViewTests
    {
        static void DestroyAllTitleAndSelectScreens()
        {
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            DestroyAllTitleAndSelectScreens();
            yield return null;
        }

        string tempPath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            DestroyAllTitleAndSelectScreens();
            Time.timeScale = 1f;
            // 2026-10-06: 예전엔 테스트 본문 끝에서 SetPathForTests(null)로 "되돌려" 다음 테스트가 실제 사용자
            // save.json(persistentDataPath)을 읽었다(play2 로그 source=Original, v1). 실패해도 항상 세션 임시 경로로.
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            if (tempPath != null)
            {
                foreach (var suffix in new[] { "", ".bak", ".tmp" })
                    if (System.IO.File.Exists(tempPath + suffix)) System.IO.File.Delete(tempPath + suffix);
                tempPath = null;
            }
            yield return null;
        }

        /// <summary>이 타이틀 화면 안의 메뉴 버튼만 찾아 누른다(같은 이름의 다른 화면 버튼과 혼동하지 않게).</summary>
        static void Click(TitleView view, string buttonName)
        {
            foreach (var b in view.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (b.name == buttonName) { b.onClick.Invoke(); return; }
            Assert.Fail($"타이틀에 {buttonName} 버튼이 없다");
        }

        [UnityTest]
        public IEnumerator Show_CreatesAllSubScreens_ClosedByDefault()
        {
            var view = TitleView.Show();
            yield return null;

            Assert.IsTrue(view.IsOpen);
            Assert.IsFalse(view.ShopView.IsOpen);
            Assert.IsFalse(view.CodexView.IsOpen);
            Assert.IsFalse(view.SettingsView.IsOpen);
            Assert.IsFalse(view.CreditsView.IsOpen);
        }

        [UnityTest]
        public IEnumerator OpeningEachSubScreen_HidesTitleRoot_AndClosingRestoresIt()
        {
            var view = TitleView.Show();
            yield return null;

            // 타이틀 루트를 숨기는 건 타이틀 메뉴 버튼(TitleView.BuildRoot)의 책임이다 — 하위 화면의 Show()만 직접
            // 부르면 그 경로를 건너뛴다(2026-10-05 play2 실패 원인). 실제 사용자 경로인 버튼 클릭으로 검증한다.
            Click(view, "SettingsButton");
            Assert.IsTrue(view.SettingsView.IsOpen);
            Assert.IsFalse(view.IsOpen, "하위 화면을 열면 타이틀 루트는 숨어야 한다");
            view.SettingsView.Hide();
            Assert.IsTrue(view.IsOpen, "닫으면 타이틀로 돌아와야 한다");

            Click(view, "CreditsButton");
            Assert.IsTrue(view.CreditsView.IsOpen);
            Assert.IsFalse(view.IsOpen);
            view.CreditsView.Hide();
            Assert.IsTrue(view.IsOpen);
        }

        [UnityTest]
        public IEnumerator FooterShowsCurrentSpecialtyBalance()
        {
            tempPath = System.IO.Path.Combine(Application.temporaryCachePath, $"title_test_{Random.Range(0, 999999)}.json");
            PermanentProgress.SetPathForTests(tempPath);
            PermanentProgress.AddSpecialty(7);

            var view = TitleView.Show();
            yield return null;

            var footerText = view.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            bool foundBalance = false;
            foreach (var t in footerText)
                if (t.text.Contains("스페셜티 7")) foundBalance = true;
            Assert.IsTrue(foundBalance, "하단에 현재 스페셜티 잔액이 보여야 한다(08:57)");
        }

        [UnityTest]
        public IEnumerator StartButton_DestroysTitle_AndOpensJobSelect()
        {
            var view = TitleView.Show();
            yield return null;

            var startButton = GameObject.Find("StartButton").GetComponent<UnityEngine.UI.Button>();
            startButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(view == null, "시작을 누르면 타이틀 캔버스는 파괴되어야 한다");
            var jobSelect = Object.FindFirstObjectByType<CharacterSelectView>();
            Assert.IsNotNull(jobSelect, "직업/무기 선택 화면으로 이어져야 한다");
        }
    }
}
