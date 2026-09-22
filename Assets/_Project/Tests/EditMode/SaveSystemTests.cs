using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>실제 세이브 파일(Application.persistentDataPath)은 절대 건드리지 않고, 매 테스트마다
    /// 자기만의 임시 경로를 쓴다 — 플레이어의 진짜 스페셜티 저장 데이터를 오염시키면 안 되기 때문.</summary>
    public class SaveSystemTests
    {
        string tempPath;

        [SetUp]
        public void SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_save_{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        [Test]
        public void Load_MissingFile_ReturnsFreshDefault()
        {
            var data = SaveSystem.Load(tempPath);
            Assert.AreEqual(0, data.specialtyPoints);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var data = new SaveData { specialtyPoints = 7 };
            SaveSystem.Save(data, tempPath);

            var loaded = SaveSystem.Load(tempPath);
            Assert.AreEqual(7, loaded.specialtyPoints);
        }

        [Test]
        public void Load_CorruptFile_FallsBackToDefault_WithoutThrowing()
        {
            File.WriteAllText(tempPath, "{ not valid json ]]]");

            SaveData data = null;
            Assert.DoesNotThrow(() => data = SaveSystem.Load(tempPath));
            Assert.AreEqual(0, data.specialtyPoints);
        }

        [Test]
        public void Save_OverwritesPreviousValue()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 3 }, tempPath);
            SaveSystem.Save(new SaveData { specialtyPoints = 9 }, tempPath);

            Assert.AreEqual(9, SaveSystem.Load(tempPath).specialtyPoints);
        }
    }

    public class PermanentProgressTests
    {
        string tempPath;

        [SetUp]
        public void SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_progress_{Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            // 실제 세이브 경로가 아니라 스위트 공용 임시 경로로 되돌린다 (TestSaveIsolation 참고) —
            // null로 되돌리면 이후 테스트들이 실제 플레이어 세이브 파일을 다시 보게 된다.
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        [Test]
        public void AddSpecialty_AccumulatesAndPersists()
        {
            Assert.AreEqual(0, PermanentProgress.Current.specialtyPoints);

            PermanentProgress.AddSpecialty(3);
            PermanentProgress.AddSpecialty(3);

            Assert.AreEqual(6, PermanentProgress.Current.specialtyPoints);
            Assert.AreEqual(6, SaveSystem.Load(tempPath).specialtyPoints, "즉시 저장되어야 한다 (런 종료를 기다리지 않음)");
        }

        [Test]
        public void AddSpecialty_IgnoresZeroOrNegative()
        {
            PermanentProgress.AddSpecialty(0);
            PermanentProgress.AddSpecialty(-5);
            Assert.AreEqual(0, PermanentProgress.Current.specialtyPoints);
        }
    }
}
