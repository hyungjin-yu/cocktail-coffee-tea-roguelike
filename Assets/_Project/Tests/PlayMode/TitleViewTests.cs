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

        /// <summary>2026-11-20 메뉴 접근 연출(합성 계약): 확정 → 시점 이동·조작 프레임 동안 실내 입력·중복 확정 차단 → 기존 메뉴 열림 →
        /// 닫으면 원위치(카메라 1·명패 다시 보임·물체 그림 원복)·입력 재개. 실제 아트 계약 도착 전 코드 경로 검증용.</summary>
        [UnityTest]
        public IEnumerator MenuApproach_SyntheticContract_OpensAfterApproach_ReturnsOnClose()
        {
            TitleIntro.ResetSessionForTests();
            var view = TitleView.Show();
            yield return null;
            view.Intro.Skip();
            for (int i = 0; i < 5; i++) yield return null;
            var scene = view.Scene;
            var f = scene.Layout.FindFeature("credits");
            var layer = scene.Interior.Find("staff_noticeboard").GetComponent<UnityEngine.UI.Image>();
            var original = layer.sprite;
            f.Approach = new CafeTitleLayout.Approach { Center = new Vector2(0.36f, 0.25f), Zoom = 2f, Seconds = 0.2f, ReturnSeconds = 0.15f,
                Layer = "staff_noticeboard", Frames = new[] { "ready/interior/g7.png", "ready/interior/g7.png" }, Fps = 20f, HoldSeconds = 0.05f };
            try
            {
                Click(view, "CreditsButton");
                Assert.IsTrue(view.Approach.Busy, "확정 즉시 접근 시작");
                Assert.IsFalse(view.CreditsView.IsOpen, "메뉴는 접근 뒤에 연다");
                Assert.IsFalse(view.Approach.Begin("codex", () => Assert.Fail("진행 중 다른 메뉴 열림")), "진행 중 중복·다른 메뉴 확정 무시");
                yield return null;
                Assert.IsFalse(scene.InteriorInteractable, "접근 중 실내 입력 차단");
                Assert.IsFalse(scene.Hotspots["credits"].Nameplate.gameObject.activeSelf, "접근 중 명패 숨김");
                float t0 = Time.realtimeSinceStartup;
                while (!view.CreditsView.IsOpen && Time.realtimeSinceStartup - t0 < 3f) yield return null;
                Assert.IsTrue(view.CreditsView.IsOpen, "접근·조작 뒤 기존 제작 정보 화면");
                Assert.IsTrue(view.Approach.LastUsedApproach);
                scene.GetInteriorCamera(out var c, out var z);
                Assert.AreEqual(2f, z, 1e-3f, "물체로 다가간 시점 유지");
                view.CreditsView.Hide();
                t0 = Time.realtimeSinceStartup;
                while (view.Approach.Busy && Time.realtimeSinceStartup - t0 < 3f) yield return null;
                yield return null;
                Assert.IsFalse(view.Approach.Busy, "원위치 완료");
                scene.GetInteriorCamera(out c, out z);
                Assert.AreEqual(1f, z, 1e-4f); Assert.AreEqual(0.5f, c.x, 1e-4f);
                Assert.AreSame(original, layer.sprite, "조작 그림 원복");
                Assert.IsTrue(scene.Hotspots["credits"].Nameplate.gameObject.activeSelf, "명패 다시 보임");
                Assert.IsTrue(scene.InteriorInteractable, "입력 재개");
                Assert.IsTrue(view.IsOpen);
            }
            finally { f.Approach = null; TitleIntro.ResetSessionForTests(); }
        }

        /// <summary>접근 계약이 없는 물체는 기존처럼 바로 메뉴가 열린다(임의 확대·페이드 대체 없음).</summary>
        [UnityTest]
        public IEnumerator MenuApproach_NoContract_OpensImmediately()
        {
            TitleIntro.ResetSessionForTests();
            var view = TitleView.Show();
            yield return null;
            view.Intro.Skip();
            for (int i = 0; i < 5; i++) yield return null;
            var feature = view.Scene.Layout.FindFeature("credits");
            var saved = feature.Approach;
            feature.Approach = null; // 계약 없는 물체(중간 fallback) 경로만 검사
            Click(view, "CreditsButton");
            feature.Approach = saved;
            Assert.IsTrue(view.CreditsView.IsOpen);
            Assert.IsFalse(view.Approach.Busy);
            view.CreditsView.Hide();
            yield return null;
            Assert.IsTrue(view.IsOpen);
            TitleIntro.ResetSessionForTests();
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
            // 2026-11-20: 물체 접근·조작 연출(v9 계약) 뒤에 열린다 → 열릴 때까지 기다린다. 닫으면 원위치 연출 뒤 입력 재개.
            Click(view, "SettingsButton");
            yield return WaitUntil(() => view.SettingsView.IsOpen);
            Assert.IsTrue(view.SettingsView.IsOpen);
            Assert.IsFalse(view.IsOpen, "하위 화면을 열면 타이틀 루트는 숨어야 한다");
            view.SettingsView.Hide();
            Assert.IsTrue(view.IsOpen, "닫으면 타이틀로 돌아와야 한다");
            yield return WaitUntil(() => view.Approach == null || !view.Approach.Busy);

            Click(view, "CreditsButton");
            yield return WaitUntil(() => view.CreditsView.IsOpen);
            Assert.IsTrue(view.CreditsView.IsOpen);
            Assert.IsFalse(view.IsOpen);
            view.CreditsView.Hide();
            Assert.IsTrue(view.IsOpen);
            yield return WaitUntil(() => view.Approach == null || !view.Approach.Busy);
        }

        static IEnumerator WaitUntil(System.Func<bool> cond, float limit = 4f)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < limit) yield return null;
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
            // 2026-11-20: 카운터 영업 준비 연출(접근 계약이 있을 때) 뒤에 직업 선택으로 — 기존 순서 그대로
            yield return WaitUntil(() => view == null);

            Assert.IsTrue(view == null, "시작을 누르면 타이틀 캔버스는 파괴되어야 한다");
            var jobSelect = Object.FindFirstObjectByType<CharacterSelectView>();
            Assert.IsNotNull(jobSelect, "직업/무기 선택 화면으로 이어져야 한다");
        }
    }
}
