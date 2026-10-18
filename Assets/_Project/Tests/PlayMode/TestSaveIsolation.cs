using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CCT.Tests
{
    /// <summary>
    /// PlayMode 스위트 전체 동안 PermanentProgress가 실제 플레이어 세이브 파일
    /// (Application.persistentDataPath/save.json)을 절대 읽거나 쓰지 않도록, 스위트 시작 시
    /// 한 번 임시 경로로 리다이렉트하고 끝나면 정리한다. GameBootstrap.Build()를 쓰는 모든 테스트가
    /// (스페셜티와 무관한 테스트 포함) GameManager.Initialize에서 PermanentProgress.Current를 읽으므로,
    /// 실제 세이브에 쌓인 스페셜티가 있으면 무관한 테스트의 기준 스탯까지 흔들릴 수 있다.
    /// 개별 테스트가 스페셜티를 직접 다룰 때는 SessionPath로 되돌리는 것을 잊지 말 것.
    /// </summary>
    [SetUpFixture]
    public class TestSaveIsolation
    {
        public static string SessionPath { get; private set; }
        /// <summary>settings.json도 실제 경로·실제 PlayerPrefs 대신 세션 임시 경로와 빈 메모리 구형 저장소를 쓴다.</summary>
        public static string SettingsSessionPath { get; private set; }

        /// <summary>2026-10-06: GameBootstrap.AutoStart(RuntimeInitializeOnLoad AfterSceneLoad)는 러너의 RUN START·OneTimeSetUp보다
        /// 먼저 돌아 실제 persistentDataPath의 save.json/settings.json과 구형 PlayerPrefs를 읽었다(fix1 로그, 읽기만 확인).
        /// 실제 세이브에 activeRun이 남아 있었다면 RecoverInterruptedRun이 실제 파일에 쓸 수도 있었다. 그래서 테스트 러너의
        /// 초기 씬(InitTestScene*)일 때만 씬 로드 전에 세션 임시 경로로 먼저 돌린다. 일반 에디터 Play에는 영향 없음.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RedirectBeforeAutoStart()
        {
            if (!SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return;
            EnsureSessionPaths();
            PermanentProgress.SetPathForTests(SessionPath);
            CardEducationGuide.SuppressAutomaticForTests=true;
            SettingsService.SetForTests(SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            Debug.Log("[TestSaveIsolation] AutoStart 전에 세션 임시 경로로 리다이렉트: " + SessionPath);
        }

        static void EnsureSessionPaths()
        {
            if (SessionPath == null)
                SessionPath = Path.Combine(Application.temporaryCachePath, $"cct_playmode_session_{Guid.NewGuid():N}.json");
            if (SettingsSessionPath == null)
                SettingsSessionPath = Path.Combine(Application.temporaryCachePath, $"cct_playmode_settings_{Guid.NewGuid():N}.json");
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            EnsureSessionPaths();
            PermanentProgress.SetPathForTests(SessionPath);
            CardEducationGuide.SuppressAutomaticForTests=true;
            // 2026-10-06: EditMode와 같이 메모리 화면 어댑터. 실제 어댑터는 에디터의 Screen/QualitySettings를 바꾸고,
            // 에디터 PlayMode에서는 창 모드 전환이 반영되지 않아 검증도 안 됐다(SettingsViewTests 참고).
            SettingsService.SetForTests(SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            PermanentProgress.SetPathForTests(null);
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(SessionPath + suffix)) File.Delete(SessionPath + suffix);
            // 67 최근 기록은 세이브 경로를 따라 같은 임시 폴더에 생긴다 — 함께 지운다.
            RunHistory.SetPathForTests(null);
            // 69 음료 창고(drinks.json)도 세이브 경로를 따라 같은 임시 폴더에 생긴다 — 함께 지운다.
            DrinkLab.SetPathForTests(null);
            var drinksPath = DrinkLab.PathForSave(SessionPath);
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(drinksPath + suffix)) File.Delete(drinksPath + suffix);
            var historyPath = RunHistory.PathForSave(SessionPath);
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(historyPath + suffix)) File.Delete(historyPath + suffix);
            SettingsService.SetForTests(null, null, null);
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(SettingsSessionPath + suffix)) File.Delete(SettingsSessionPath + suffix);
            SessionPath = null; // 같은 도메인에서 다시 실행해도 새 세션 경로를 만든다
            SettingsSessionPath = null;
        }
    }
}
