using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>PermanentProgress.DiscoverCard (카드 도감 기록). 실제 세이브 파일은 절대 건드리지 않도록
    /// 매 테스트마다 임시 경로로 리다이렉트한다.</summary>
    public class CardCodexTests
    {
        string tempPath;

        [SetUp]
        public void SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_codex_{Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath); // 세션 공용 경로로 복원
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        [Test]
        public void DiscoverCard_AddsToPermanentList_AndPersists()
        {
            Assert.AreEqual(0, PermanentProgress.Current.discoveredCardIds.Count);

            PermanentProgress.DiscoverCard("dry_gin");

            Assert.IsTrue(PermanentProgress.Current.discoveredCardIds.Contains("dry_gin"));
            Assert.IsTrue(SaveSystem.Load(tempPath).discoveredCardIds.Contains("dry_gin"),
                "즉시 저장되어야 한다 (런 종료를 기다리지 않음)");
        }

        [Test]
        public void DiscoverCard_SameCardTwice_DoesNotDuplicate()
        {
            PermanentProgress.DiscoverCard("dry_gin");
            PermanentProgress.DiscoverCard("dry_gin");

            Assert.AreEqual(1, PermanentProgress.Current.discoveredCardIds.Count);
        }

        [Test]
        public void DiscoverCard_IgnoresNullOrEmpty()
        {
            PermanentProgress.DiscoverCard(null);
            PermanentProgress.DiscoverCard("");

            Assert.AreEqual(0, PermanentProgress.Current.discoveredCardIds.Count);
        }

        [Test]
        public void DiscoverCard_MultipleDifferentCards_AllRecorded()
        {
            PermanentProgress.DiscoverCard("dry_gin");
            PermanentProgress.DiscoverCard("green_tea");

            Assert.AreEqual(2, PermanentProgress.Current.discoveredCardIds.Count);
        }
    }
}
