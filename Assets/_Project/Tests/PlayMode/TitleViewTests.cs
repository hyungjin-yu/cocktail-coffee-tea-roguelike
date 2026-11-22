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

        // ───── 2026-11-21 데스크톱 Esc(타이틀 메뉴) · 직업 선택 → 타이틀 ─────

        static IEnumerator OpenFromRoom(TitleView view, string buttonName, System.Func<bool> open)
        {
            Click(view, buttonName);
            yield return WaitUntil(open);
            Assert.IsTrue(open(), buttonName + " 열림");
        }

        static IEnumerator EnteredTitle(System.Action<TitleView> got)
        {
            TitleIntro.ResetSessionForTests();
            var view = TitleView.Show();
            yield return null;
            view.Intro.Skip();
            for (int i = 0; i < 5; i++) yield return null;
            got(view);
        }

        /// <summary>설정·제작 정보·도감·스킨: Esc = 닫기/뒤로 버튼과 같은 경로 → 타이틀 루트 복귀 → 역재생·원시점(zoom 1) → 입력 재개. 표기에 Esc 포함.</summary>
        [UnityTest]
        public IEnumerator Escape_ClosesSettingsCreditsCodexSkins_AndReturnsToRoom()
        {
            TitleView view = null;
            yield return EnteredTitle(v => view = v);
            var cases = new (string button, System.Func<bool> open, Transform screen)[]
            {
                ("SettingsButton", () => view.SettingsView.IsOpen, view.SettingsView.transform),
                ("CreditsButton", () => view.CreditsView.IsOpen, view.CreditsView.transform),
                ("CardCodexButton", () => view.CodexView.IsOpen, view.CodexView.transform),
                ("SkinShopButton", () => view.SkinShopView.IsOpen, view.SkinShopView.transform),
            };
            foreach (var c in cases)
            {
                yield return OpenFromRoom(view, c.button, c.open);
                bool labelled = false;
                foreach (var b in c.screen.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                    if ((b.name == "CloseButton" || b.name == "BackButton") && b.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("Esc")) labelled = true;
                Assert.IsTrue(labelled, c.button + ": 닫기/뒤로 표기에 Esc");
                Assert.IsTrue(view.HandleMenuEscape(), c.button + ": Esc 처리");
                Assert.IsFalse(c.open(), c.button + ": Esc로 닫힘");
                Assert.IsTrue(view.IsOpen, c.button + ": 타이틀 루트 복귀");
                Assert.IsFalse(view.HandleMenuEscape(), "실내(메뉴 없음)에서 Esc는 메뉴 라우터가 아무것도 안 함");
                yield return WaitUntil(() => !view.Approach.Busy);
                yield return null; // 입력 게이트는 다음 Update에서 갱신
                view.Scene.GetInteriorCamera(out var center, out var zoom);
                Assert.AreEqual(1f, zoom, 1e-4f, c.button + ": 원시점");
                Assert.IsTrue(view.Scene.InteriorInteractable, c.button + ": 입력 재개");
            }
            TitleIntro.ResetSessionForTests();
        }

        /// <summary>영구 강화·최근 기록은 자체 Esc가 있어 타이틀 라우터가 건드리지 않는다(한 번의 Esc가 두 번 처리되지 않음).</summary>
        [UnityTest]
        public IEnumerator Escape_LeavesPermanentAndHistoryToOwnHandlers()
        {
            TitleView view = null;
            yield return EnteredTitle(v => view = v);
            yield return OpenFromRoom(view, "PermanentShopButton", () => view.ShopView.IsOpen);
            Assert.IsFalse(view.HandleMenuEscape(), "영구 강화는 자체 Esc");
            Assert.IsTrue(view.ShopView.IsOpen);
            foreach (var b in view.ShopView.GetComponentsInChildren<UnityEngine.UI.Button>(false)) if (b.name == "BackButton") { b.onClick.Invoke(); break; }
            yield return WaitUntil(() => view.IsOpen && !view.Approach.Busy);
            Assert.IsFalse(view.ShopView.IsOpen);
            yield return OpenFromRoom(view, "RunHistoryButton", () => view.HistoryView.IsOpen);
            Assert.IsFalse(view.HandleMenuEscape(), "최근 기록은 자체 Esc");
            yield return null;
            Assert.IsTrue(view.HistoryView.HandleEscape());
            yield return WaitUntil(() => view.IsOpen && !view.Approach.Busy);
            TitleIntro.ResetSessionForTests();
        }

        /// <summary>가장 위 하나만: 도감 상세 → 상세만 닫힘. 설정 해상도 확인창 → 되돌리기만(설정 유지). 조작 안내 → 안내만.</summary>
        [UnityTest]
        public IEnumerator Escape_ClosesTopmostLayerOnly()
        {
            TitleView view = null;
            yield return EnteredTitle(v => view = v);
            yield return OpenFromRoom(view, "CardCodexButton", () => view.CodexView.IsOpen);
            UnityEngine.UI.Button row = null;
            foreach (var b in view.CodexView.GetComponentsInChildren<UnityEngine.UI.Button>(false)) if (b.name.StartsWith("Row_")) { row = b; break; }
            Assert.IsNotNull(row, "도감 행");
            row.onClick.Invoke();
            Assert.IsTrue(view.CodexView.IsDetailOpen);
            Assert.IsTrue(view.HandleMenuEscape());
            Assert.IsFalse(view.CodexView.IsDetailOpen, "상세만 닫힘");
            Assert.IsTrue(view.CodexView.IsOpen, "도감은 유지");
            Assert.IsTrue(view.HandleMenuEscape());
            Assert.IsFalse(view.CodexView.IsOpen);
            yield return WaitUntil(() => !view.Approach.Busy);

            yield return OpenFromRoom(view, "SettingsButton", () => view.SettingsView.IsOpen);
            view.SettingsView.CycleResolution();
            Assert.IsTrue(view.SettingsView.IsResolutionConfirmPending);
            Assert.IsTrue(view.HandleMenuEscape());
            Assert.IsFalse(view.SettingsView.IsResolutionConfirmPending, "해상도 확인창은 되돌리기");
            Assert.IsTrue(view.SettingsView.IsOpen, "설정은 유지");
            view.SettingsView.ShowTutorialReplay();
            Assert.IsTrue(view.SettingsView.TutorialView.IsOpen);
            Assert.IsTrue(view.HandleMenuEscape());
            Assert.IsFalse(view.SettingsView.TutorialView.IsOpen, "조작 안내만 닫힘");
            Assert.IsTrue(view.SettingsView.IsOpen);
            Assert.IsTrue(view.HandleMenuEscape());
            Assert.IsFalse(view.SettingsView.IsOpen);
            yield return WaitUntil(() => !view.Approach.Busy);
            TitleIntro.ResetSessionForTests();
        }

        /// <summary>실제 키 입력 경로(Update): 가상 키보드의 Esc 상태 이벤트로 제작 정보가 닫힌다(배치 PlayMode 포커스 없음 → 이 시험 동안만 입력 항상 전달).</summary>
        [UnityTest]
        public IEnumerator Escape_KeyboardEvent_ClosesCredits()
        {
            TitleView view = null;
            yield return EnteredTitle(v => view = v);
            yield return OpenFromRoom(view, "CreditsButton", () => view.CreditsView.IsOpen);
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var savedEditor = settings.editorInputBehaviorInPlayMode; var savedBg = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            var kb = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("TitleEscKeyboard");
            try
            {
                kb.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                Assert.IsFalse(view.CreditsView.IsOpen, "Esc 키로 닫힘");
                Assert.IsTrue(view.IsOpen);
                yield return WaitUntil(() => !view.Approach.Busy);
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(kb);
                settings.editorInputBehaviorInPlayMode = savedEditor; settings.backgroundBehavior = savedBg;
                TitleIntro.ResetSessionForTests();
            }
        }

        /// <summary>직업 선택 1단계: "카페로 · Esc" 버튼과 Esc가 타이틀(카페 실내)로 돌아간다. 무기 단계 Esc는 직업 단계로. 전투 시작·미리 선택 없음.</summary>
        [UnityTest]
        public IEnumerator CharacterSelect_BackAndEscape_ReturnToTitle_NoRunStart()
        {
            TitleIntro.ResetSessionForTests();
            var first = TitleView.Show(); yield return null; first.Intro.Skip(); yield return null;
            Object.Destroy(first.gameObject); yield return null; // 이번 실행에서 이미 입장함 → 재진입은 짧은 페이드
            bool chosen = false;
            var select = CharacterSelectView.Show((j, w) => chosen = true);
            yield return null; yield return null;
            Assert.IsNotNull(select.TitleBackButton, "직업 단계에 타이틀로 돌아가는 버튼");
            Assert.AreEqual("BackButton", select.TitleBackButton.name, "터치 뒤로 가기도 같은 버튼");
            StringAssert.Contains("Esc", select.TitleBackButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
            // 무기 단계 → Esc → 직업 단계(타이틀로 가지 않음)
            select.BartenderButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.IsTrue(select.IsWeaponStep);
            Assert.IsNull(select.TitleBackButton, "무기 단계에는 직업 단계로 가는 뒤로만");
            Assert.IsTrue(select.HandleEscape());
            Assert.IsFalse(select.IsWeaponStep, "무기 단계 Esc = 직업 단계");
            Assert.IsFalse(select.HandleEscape(), "다시 그려진 직후 같은 Esc가 타이틀로 새지 않음");
            yield return null; yield return null;
            // 설정 위 Esc는 설정만
            select.SettingsButton.onClick.Invoke();
            Assert.IsTrue(select.SettingsView.IsOpen);
            Assert.IsTrue(select.HandleEscape());
            Assert.IsFalse(select.SettingsView.IsOpen);
            Assert.IsNotNull(Object.FindFirstObjectByType<CharacterSelectView>(), "설정만 닫히고 직업 선택 유지");
            yield return null; yield return null;
            // 화면 버튼 → 타이틀
            select.TitleBackButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.IsTrue(select == null, "직업 선택 닫힘");
            var title = Object.FindFirstObjectByType<TitleView>();
            Assert.IsNotNull(title, "타이틀(카페 실내)로");
            Assert.IsFalse(title.Intro.PlayedFull, "문 입장 반복 없음(짧은 재진입)");
            Assert.IsNull(GameManager.Instance, "전투 시작 없음");
            Assert.IsFalse(chosen);
            yield return WaitUntil(() => title.Intro.Finished);
            var es = UnityEngine.EventSystems.EventSystem.current;
            Assert.IsTrue(es == null || es.currentSelectedGameObject == null, "미리 선택 없음(Enter만으로 시작 안 됨)");
            // Esc 경로: 다시 직업 선택 → Esc → 타이틀
            Object.Destroy(title.gameObject); yield return null;
            var select2 = CharacterSelectView.Show((j, w) => chosen = true);
            yield return null; yield return null;
            Assert.IsTrue(select2.HandleEscape());
            yield return null; yield return null;
            Assert.IsTrue(select2 == null);
            Assert.IsNotNull(Object.FindFirstObjectByType<TitleView>());
            Assert.IsNull(GameManager.Instance);
            Assert.IsFalse(chosen);
            TitleIntro.ResetSessionForTests();
        }
    }
}
