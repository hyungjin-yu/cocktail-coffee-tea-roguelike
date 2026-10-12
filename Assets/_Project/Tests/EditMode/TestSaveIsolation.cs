using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// EditMode 스위트 전체 동안 PermanentProgress가 실제 플레이어 세이브 파일
    /// (Application.persistentDataPath/save.json)을 절대 읽거나 쓰지 않도록, 스위트 시작 시
    /// 한 번 임시 경로로 리다이렉트하고 끝나면 정리한다. 개발 중인 기기에 이미 스페셜티를 모은
    /// 실제 세이브가 있어도 테스트 결과가 거기에 좌우되면 안 되기 때문 (재현성).
    /// 개별 테스트가 스페셜티를 직접 다룰 때는 SessionPath로 되돌리는 것을 잊지 말 것 (null로 되돌리면
    /// 그 뒤 테스트들이 다시 실제 세이브 파일을 보게 된다).
    /// </summary>
    [SetUpFixture]
    public class TestSaveIsolation
    {
        public static string SessionPath { get; private set; }
        /// <summary>settings.json도 실제 경로·실제 PlayerPrefs 대신 세션 임시 경로와 빈 메모리 구형 저장소를 쓴다.</summary>
        public static string SettingsSessionPath { get; private set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SessionPath = Path.Combine(Application.temporaryCachePath, $"cct_editmode_session_{Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(SessionPath);
            SettingsSessionPath = Path.Combine(Application.temporaryCachePath, $"cct_editmode_settings_{Guid.NewGuid():N}.json");
            // EditMode는 실제 창/QualitySettings도 건드리지 않는 메모리 화면 어댑터
            SettingsService.SetForTests(SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            PermanentProgress.SetPathForTests(null);
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
            SettingsService.SetForTests(null, null, null);
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(SettingsSessionPath + suffix)) File.Delete(SettingsSessionPath + suffix);
        }
    }
}
