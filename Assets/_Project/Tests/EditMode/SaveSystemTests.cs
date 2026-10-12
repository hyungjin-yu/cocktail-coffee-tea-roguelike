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
            if (File.Exists(tempPath + ".bak")) File.Delete(tempPath + ".bak");
            if (File.Exists(tempPath + ".tmp")) File.Delete(tempPath + ".tmp");
            // v2: 손상 파일은 시간 접미사 .corrupt 사본으로 보존되므로(08:162) 그 사본도 정리한다.
            foreach (var copy in Directory.GetFiles(Path.GetDirectoryName(tempPath), Path.GetFileName(tempPath) + "*.corrupt"))
                File.Delete(copy);
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

        [Test]
        public void Load_StampsCurrentVersion_EvenOnFreshOrOldData()
        {
            // 버전 필드가 아예 없던 시절(v0) 파일을 흉내: version을 안 쓴 채 저장.
            File.WriteAllText(tempPath, "{\"specialtyPoints\":5}");
            // v2: 필드명 version → schemaVersion(08:138)으로 바뀌어 단언 대상만 교체.
            Assert.AreEqual(SaveSystem.CurrentVersion, SaveSystem.Load(tempPath).schemaVersion);
        }

        [Test]
        public void Save_WritesBackupOfPreviousGoodFile()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 1 }, tempPath);
            Assert.IsFalse(File.Exists(tempPath + ".bak"), "첫 저장은 이전 파일이 없으니 백업도 없어야 한다");

            SaveSystem.Save(new SaveData { specialtyPoints = 2 }, tempPath);
            Assert.IsTrue(File.Exists(tempPath + ".bak"), "두 번째 저장부터는 직전 내용이 .bak으로 남아야 한다(G28)");
            Assert.AreEqual(1, SaveSystem.Load(tempPath + ".bak").specialtyPoints);
        }

        [Test]
        public void Load_CorruptFile_RecoversFromBackup_AndReportsIssue()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 42 }, tempPath); // 정상본 1회
            SaveSystem.Save(new SaveData { specialtyPoints = 999 }, tempPath); // .bak에 42가 남음
            File.WriteAllText(tempPath, "{ broken"); // 최신 파일만 손상시킴

            var data = SaveSystem.Load(tempPath);

            Assert.AreEqual(42, data.specialtyPoints, "손상된 원본 대신 .bak(직전 정상본)으로 복구해야 한다(G28)");
            Assert.IsNotNull(SaveSystem.LastLoadIssue, "조용히 넘어가지 말고 이유를 남겨야 한다(G29)");
            Assert.IsTrue(File.Exists(tempPath), "손상된 원본 파일 자체는 그대로 보존해야 한다(G28 — 디버깅 근거)");
        }

        [Test]
        public void Load_CorruptFile_NoBackup_FallsBackToDefault_AndReportsIssue()
        {
            File.WriteAllText(tempPath, "not even json");

            var data = SaveSystem.Load(tempPath);

            Assert.AreEqual(0, data.specialtyPoints);
            Assert.IsNotNull(SaveSystem.LastLoadIssue);
        }

        [Test]
        public void Load_CleanFile_ReportsNoIssue()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 1 }, tempPath);
            SaveSystem.Load(tempPath);
            Assert.IsNull(SaveSystem.LastLoadIssue);
        }

        [Test]
        public void Load_MissingLists_AreMigratedToEmpty_NotNull()
        {
            File.WriteAllText(tempPath, "{\"discoveredCardIds\":null}");
            var data = SaveSystem.Load(tempPath);

            Assert.IsNotNull(data.discoveredCardIds);
            Assert.IsNotNull(data.unlockedPermanentSkillIds);
            Assert.IsNotNull(data.discoveredMaterialVarietyIds);
        }

        [Test]
        public void Load_NegativeCounters_AreClamped_AndReported()
        {
            File.WriteAllText(tempPath, "{\"specialtyPoints\":-3,\"coffeeBeansCollected\":-1}");
            var data = SaveSystem.Load(tempPath);

            Assert.AreEqual(0, data.specialtyPoints);
            Assert.AreEqual(0, data.coffeeBeansCollected);
            Assert.IsNotNull(SaveSystem.LastLoadIssue, "음수 손상은 조용히 고치기만 하면 안 되고 알려야 한다(G27)");
        }

        [Test]
        public void Save_Failure_SetsLastSaveFailed_AndNextSuccessClearsIt()
        {
            // 디렉터리를 파일 자리에 만들어 WriteAllText가 실패하도록 강제한다.
            Directory.CreateDirectory(tempPath);
            SaveSystem.Save(new SaveData(), tempPath);
            Assert.IsTrue(SaveSystem.LastSaveFailed);

            Directory.Delete(tempPath);
            SaveSystem.Save(new SaveData(), tempPath);
            Assert.IsFalse(SaveSystem.LastSaveFailed, "다음 저장이 성공하면 실패 표시가 풀려야 한다");
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
            if (File.Exists(tempPath + ".bak")) File.Delete(tempPath + ".bak");
            if (File.Exists(tempPath + ".tmp")) File.Delete(tempPath + ".tmp");
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

        [Test]
        public void LastLoadIssue_ForcesLoad_AndIsNullForFreshPath()
        {
            // Current를 한 번도 안 거쳤어도(캐시 안 됐어도) LastLoadIssue 접근만으로 로드가 트리거돼야 한다.
            Assert.IsNull(PermanentProgress.LastLoadIssue);
        }
    }
}
