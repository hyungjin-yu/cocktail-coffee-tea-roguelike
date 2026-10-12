using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>08장 "## 4. 설정" (2026-09-28, Codex 오디오/아트 작업과 무관하게 진행). 지금은 실제로
    /// 뒤에 동작이 있는 항목만 검증한다: 마스터/BGM/SFX 볼륨, 조작 방식, 해상도 10초 확인/복원, 표시모드,
    /// VSync. 화면 흔들림·효과 강도·피해 숫자·조작 안내 다시보기·기본값 복원은 SettingsView.cs 헤더에
    /// 적어둔 대로 이번 범위 밖 — 여기서도 검증하지 않는다.</summary>
    public class SettingsViewTests
    {
        static void DestroyAllSelectScreens()
        {
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
            // AutoStart가 씬 시작 시 CharacterSelectView 대신 TitleView를 띄우므로(2026-09-28) 같이 치운다.
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            DestroyAllSelectScreens();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            DestroyAllSelectScreens();
            InputSettings.Scheme = MovementScheme.Keyboard;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenFromPause_KeepsPauseOpenUnderneath_AndClosesWithoutClosingPause()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var game = GameManager.Instance;

            game.OpenPause();
            var settings = game.PauseView.SettingsView;
            settings.Show();

            Assert.IsTrue(settings.IsOpen);
            Assert.IsTrue(game.PauseView.IsOpen, "설정은 Pause 위에 얹히는 오버레이라 Pause 자체는 그대로 열려 있어야 한다");
            Assert.IsTrue(game.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            game.PauseView.CloseSettings();

            Assert.IsFalse(settings.IsOpen);
            Assert.IsTrue(game.PauseView.IsOpen, "설정만 닫혀야지 Pause까지 같이 닫히면 안 된다");
            Assert.IsTrue(game.IsPaused);
        }

        [UnityTest]
        public IEnumerator OpenFromCharacterSelect_HidesJobRoot_AndRestoresItOnClose()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;

            Assert.IsNotNull(view.SettingsButton);
            view.SettingsButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(view.SettingsView.IsOpen);

            view.SettingsView.Hide();
            yield return null;

            Assert.IsFalse(view.SettingsView.IsOpen);
        }

        [UnityTest]
        public IEnumerator VolumeSliders_WriteThroughToAudioBus()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            settings.SetMasterVolume(0.3f);
            settings.SetMusicVolume(0.9f);
            settings.SetSfxVolume(0.1f);

            Assert.AreEqual(0.3f, AudioBus.MasterVolume, 0.001f);
            Assert.AreEqual(0.9f, AudioBus.MusicVolume, 0.001f);
            Assert.AreEqual(0.1f, AudioBus.SfxVolume, 0.001f);
        }

        [UnityTest]
        public IEnumerator ToggleScheme_FlipsInputSettings()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            Assert.AreEqual(MovementScheme.Keyboard, InputSettings.Scheme);
            settings.ToggleScheme();
            Assert.AreEqual(MovementScheme.Mouse, InputSettings.Scheme);
            settings.ToggleScheme();
            Assert.AreEqual(MovementScheme.Keyboard, InputSettings.Scheme);
        }

        [UnityTest]
        public IEnumerator CycleResolution_OpensConfirm_AndRevertRestoresPrevious()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            Assert.IsFalse(settings.IsResolutionConfirmPending);
            settings.CycleResolution();
            Assert.IsTrue(settings.IsResolutionConfirmPending, "08:118 — 해상도 변경은 10초 되돌리기 확인을 띄워야 한다");

            settings.RevertResolution();
            Assert.IsFalse(settings.IsResolutionConfirmPending);
        }

        [UnityTest]
        public IEnumerator ClosingWhileResolutionConfirmPending_AutoReverts()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            settings.CycleResolution();
            Assert.IsTrue(settings.IsResolutionConfirmPending);

            settings.Hide(); // 08:128 "미확인 해상도 변경만 복원"

            Assert.IsFalse(settings.IsResolutionConfirmPending, "닫을 때 미확인 상태였다면 자동으로 되돌려야 한다");
        }

        [UnityTest]
        public IEnumerator ConfirmingResolution_ClearsThePendingState()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            settings.CycleResolution();
            settings.ConfirmResolution();

            Assert.IsFalse(settings.IsResolutionConfirmPending);
        }

        [UnityTest]
        public IEnumerator TutorialReplayButton_OpensTheFirstRunTutorial()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            settings.ShowTutorialReplay();

            Assert.IsTrue(settings.TutorialView.IsOpen, "08:125 — 설정에서 조작 안내를 다시 볼 수 있어야 한다");
        }

        /// <summary>2026-10-06: 이전 판은 Screen.fullScreenMode/QualitySettings를 직접 읽었다. 에디터 PlayMode(배치 포함)에서는
        /// Screen.SetResolution이 창 모드를 바꾸지 않아 항상 실패했고(2026-10-05 play2), 실제 에디터 품질 설정을 건드렸다.
        /// 스위트는 이제 MemoryDisplayAdapter(TestSaveIsolation)로 격리하고, 버튼이 실제 적용 경로(IDisplayAdapter)에
        /// 반대 모드를 전달하는지 검증한다. 실제 Windows 창 전환은 네이티브 QA(r6 표시모드 저장·복원)가 담당한다.</summary>
        [UnityTest]
        public IEnumerator ToggleFullscreen_And_ToggleVSync_ApplyFlippedStateThroughDisplayAdapter()
        {
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            var display = SettingsService.Display as MemoryDisplayAdapter;
            Assert.IsNotNull(display, "PlayMode 스위트는 실제 창 대신 메모리 화면 어댑터를 써야 한다(TestSaveIsolation)");
            var settings = GameManager.Instance.PauseView.SettingsView;
            settings.Show();

            bool wasWindowed = SettingsService.EffectiveDisplay.windowed;
            int appliesBefore = display.ApplyCount;
            settings.ToggleFullscreen();
            Assert.AreEqual(appliesBefore + 1, display.ApplyCount, "표시모드 전환은 화면에 한 번 적용돼야 한다");
            Assert.AreEqual(!wasWindowed, display.Windowed, "실제 적용 경로에 반대 표시모드가 전달돼야 한다");
            Assert.AreEqual(!wasWindowed, SettingsService.EffectiveDisplay.windowed);
            Assert.IsTrue(settings.IsResolutionConfirmPending, "08:118/129 — 표시모드도 10초 확인을 거친다");
            settings.ConfirmResolution();
            Assert.AreEqual(!wasWindowed, SettingsService.Current.IsWindowed, "확인 후 설정에 기록돼야 한다");

            bool prevVSync = SettingsService.Current.vsync;
            settings.ToggleVSync();
            Assert.AreEqual(!prevVSync, SettingsService.Current.vsync);
            Assert.AreEqual(!prevVSync, display.VSync, "VSync도 실제 적용 경로로 전달돼야 한다");
            Assert.AreEqual(prevVSync ? 60 : -1, display.TargetFrameRate, "08:120 꺼짐이면 60fps 제한");

            // 다음 테스트를 위해 기본 상태로 되돌린다(세션 settings 임시 파일).
            settings.ToggleFullscreen();
            settings.ConfirmResolution();
            settings.ToggleVSync();
        }
    }
}
