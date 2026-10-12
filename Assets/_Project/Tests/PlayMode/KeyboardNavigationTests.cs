using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>G31 "키보드 전용 내비게이션" (2026-09-28, Codex 작업과 무관). 마우스로 아무것도 안 눌러도
    /// 화면이 뜨는 순간 Tab/화살표로 바로 다닐 수 있어야 한다 — 각 화면이 뜰 때 EventSystem에 기본 선택
    /// 항목이 채워지는지만 확인한다(실제 Tab 키 시퀀스 자체는 Unity의 자동 내비게이션에 맡긴다). 08:50
    /// "마우스 hover와 키보드 focus는 같은 [강조]"는 UiFactory.ApplyHoverColors로 통일했다 — 색상 자체는
    /// 코드 리뷰로만 확인, 여기서는 선택 유무만 검증한다.</summary>
    public class KeyboardNavigationTests
    {
        static void DestroyStrayScreens()
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
            UiInitialFocus.SetKeyboardModeForTests(true); // 기존 화면들(Pause/Settings 등)은 키보드 흐름 기준
            DestroyStrayScreens();
            EventSystem.current?.SetSelectedGameObject(null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            DestroyStrayScreens();
            UiInitialFocus.SetKeyboardModeForTests(false);
            Time.timeScale = 1f;
            yield return null;
        }

        static bool HasSelection() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;

        static string Selected() => EventSystem.current?.currentSelectedGameObject?.name;

        [UnityTest]
        public IEnumerator CoffeeTree_EscapeInput_ClosesOnlyTree_ResumesAndNextEscapePauses()
        {
            var game = GameBootstrap.Build();
            game.Director.enabled = false;
            game.GetComponentInChildren<EnemySpawner>().enabled = false;
            yield return null;
            for (int i = 0; i < 10; i++)
            {
                game.OpenCoffeeTree();
                Assert.IsTrue(game.SkillView.IsOpen);
                game.RequestEscape(); // 실제 키보드가 호출하는 화면 우선순위 라우터
                Assert.IsFalse(game.SkillView.IsOpen);
                Assert.IsFalse(game.IsPaused);
                Assert.AreEqual(GameState.Playing, game.State);
                Assert.AreEqual(1f, Time.timeScale);
                yield return null;
            }
            game.RequestEscape();
            Assert.IsTrue(game.IsPaused);
        }

        [UnityTest]
        public IEnumerator Title_NoFocusOnEntry_FirstKeyRevealsStartButton()
        {
            UiInitialFocus.SetKeyboardModeForTests(false);
            var view = TitleView.Show();
            yield return null;
            Assert.IsFalse(HasSelection(), "2026-10-12: 타이틀 진입 즉시 시작 버튼이 강조로 고정되면 안 된다");

            UiInitialFocus.SimulateNavigationForTests();
            Assert.AreEqual("StartButton", Selected(), "첫 키보드 입력은 시작 버튼을 드러낸다(G31)");
            Object.DestroyImmediate(view.gameObject);
        }

        [UnityTest]
        public IEnumerator Title_KeyboardMode_SelectsStartButtonImmediately()
        {
            UiInitialFocus.SetKeyboardModeForTests(true);
            var view = TitleView.Show();
            yield return null;
            Assert.AreEqual("StartButton", Selected(), "키보드로 넘어온 경우 바로 탐색 가능");
            UiInitialFocus.SetKeyboardModeForTests(false);
            Object.DestroyImmediate(view.gameObject);
        }

        [UnityTest]
        public IEnumerator CharacterSelect_JobStepAndWeaponStep_SelectSomething()
        {
            UiInitialFocus.SetKeyboardModeForTests(false);
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            Assert.IsFalse(HasSelection(), "직업 선택 진입 시 바텐더가 강조로 고정되면 안 된다");
            UiInitialFocus.SimulateNavigationForTests();
            var sel = EventSystem.current.currentSelectedGameObject;
            Assert.IsTrue(sel == view.BartenderButton.gameObject || sel == view.TeaSommelierButton.gameObject, "직업 선택 단계");

            view.BartenderButton.onClick.Invoke();
            yield return null;
            sel = EventSystem.current.currentSelectedGameObject;
            Assert.IsTrue(sel == view.WeaponButtonA.gameObject || sel == view.WeaponButtonB.gameObject,
                "키보드로 넘어오면 무기 선택 단계도 바로 탐색");
            UiInitialFocus.SetKeyboardModeForTests(false);
        }

        [UnityTest]
        public IEnumerator CharacterSelect_WeaponDefault_LogicalOnlyUntilExplicitChoice()
        {
            UiInitialFocus.SetKeyboardModeForTests(false);
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.BartenderButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(HasSelection(), "마우스 진입 시 무기 카드 강조 없음");
            Assert.IsTrue(view.SelectedWeapon == JobPreset.Bartender.weapons[0].kind ||
                view.SelectedWeapon == JobPreset.Bartender.weapons[1].kind, "기본 무기 데이터는 유지");
            Assert.IsFalse(view.WeaponChoiceShownForTests(true) || view.WeaponChoiceShownForTests(false),
                "기본값은 명시 선택 전 선택됨 장식 없음");

            view.WeaponButtonB.onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.WeaponChoiceShownForTests(false), "직접 고른 무기는 선택됨 표시");
            Assert.IsFalse(HasSelection(), "마우스로 고르면 영업 시작을 미리 강조하지 않는다");
        }

        [UnityTest]
        public IEnumerator Pause_SelectsContinueButton_AndEndRunConfirm_SelectsCancel()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var game = GameManager.Instance;

            game.OpenPause();
            Assert.IsTrue(HasSelection(), "Pause 메인 메뉴");

            var giveUpButton = GameObject.Find("GiveUpButton").GetComponent<UnityEngine.UI.Button>();
            giveUpButton.onClick.Invoke();

            Assert.IsTrue(HasSelection());
            Assert.AreEqual("Cancel", EventSystem.current.currentSelectedGameObject.name,
                "08:100 '취소 기본 포커스'");

            // 2026-10-09 실제 OS 키보드 G34: 오른쪽 화살표가 확인창 밖 일시정지 메뉴로 빠져 Enter가 "설정"을 열었다.
            var cancel = EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.Selectable>();
            var right = cancel.FindSelectableOnRight();
            Assert.IsNotNull(right);
            Assert.AreEqual("Confirm", right.name, "취소 → 오른쪽은 확인창의 포기 버튼");
            Assert.IsNull(cancel.FindSelectableOnUp(), "확인창 밖으로 위 이동 없음");
            Assert.IsNull(cancel.FindSelectableOnDown(), "확인창 밖으로 아래 이동 없음");
            Assert.AreEqual("Cancel", right.FindSelectableOnLeft().name);
            Assert.IsNull(right.FindSelectableOnUp());
        }

        [UnityTest]
        public IEnumerator Settings_SelectsMasterVolumeSlider()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            GameManager.Instance.PauseView.SettingsView.Show();

            Assert.IsTrue(HasSelection());
        }

        [UnityTest]
        public IEnumerator CardCodex_SelectsAllTab()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.CardCodexButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(HasSelection());
        }

        [UnityTest]
        public IEnumerator PermanentSkillView_SelectsSomething()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.PermanentShopButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(HasSelection());
        }

        [UnityTest]
        public IEnumerator SkillTreeReadOnly_SelectsCloseButton()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var game = GameManager.Instance;
            game.OpenPause();

            game.SkillView.ShowReadOnly(game.Skills, () => game.CoffeePoints, () => { });
            yield return null;

            Assert.IsTrue(HasSelection());
        }

        [UnityTest]
        public IEnumerator ResultView_SelectsRestartButton()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var game = GameManager.Instance;
            game.Player.TakeDamage(99999f);
            yield return null;
            yield return new WaitForSecondsRealtime(GameManager.DeathResultDelaySeconds + 0.2f); // 사망 동작 후 결과 패널

            Assert.IsTrue(HasSelection());
        }
    }
}
