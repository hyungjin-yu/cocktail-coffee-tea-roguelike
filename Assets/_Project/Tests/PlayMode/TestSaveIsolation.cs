using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

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

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SessionPath = Path.Combine(Application.temporaryCachePath, $"cct_playmode_session_{Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(SessionPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            PermanentProgress.SetPathForTests(null);
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
        }
    }
}
