using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 08 §5/§6 save.json v2 — G27(이전)/G28(중단·손상)/G29(실패·롤백·재시도)/G30(activeRun 복구 로직) 임시 폴더 테스트.
    /// 매 테스트마다 temporaryCachePath 아래 고유 폴더의 save.json을 쓰고 지운다. 실제 사용자 세이브는 건드리지 않는다.
    /// G30의 "별도 테스트 저장 빌드" 실행은 여기서 하지 않는다(로직만 — 강제종료는 세션 재시작으로 흉내).
    /// </summary>
    public abstract class SaveV2Fixture
    {
        protected string dir;
        protected string savePath;
        protected int faultHits;

        [SetUp]
        public void BaseSetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, $"cct_save_v2_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            savePath = Path.Combine(dir, "save.json");
            faultHits = 0;
            SaveSystem.FaultHookForTests = null;
            PermanentProgress.SetPathForTests(savePath);
        }

        [TearDown]
        public void BaseTearDown()
        {
            SaveSystem.FaultHookForTests = null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            try { Directory.Delete(dir, true); } catch { /* 임시 폴더 정리 실패는 무시 */ }
        }

        /// <summary>강제종료 후 다음 실행 = 메모리 캐시를 버리고 같은 경로로 다시 로드.</summary>
        protected void Relaunch() => PermanentProgress.SetPathForTests(savePath);

        protected void FailAt(SaveSystem.SaveStage stage) => SaveSystem.FaultHookForTests = s =>
        {
            if (s != stage) return;
            faultHits++;
            throw new IOException("injected " + s);
        };

        protected void ClearFault() => SaveSystem.FaultHookForTests = null;

        protected SaveData Disk() => SaveSystem.Load(savePath);

        protected string[] CorruptFiles() => Directory.GetFiles(dir, "*.corrupt");

        protected static List<PermanentSkillData> Nodes(List<UnityEngine.Object> created)
        {
            var nodes = DefaultPermanentSkills.CreateRuntime();
            created.AddRange(nodes);
            return nodes;
        }
    }

    public class SaveV2MigrationTests : SaveV2Fixture
    {
        [Test]
        public void LegacyV0_PreservesProgressAndUnknownIds_DedupesNulls_ClampsNegative_NoInventedStats()
        {
            const string legacy = "{\"specialtyPoints\":9,\"unlockedPermanentSkillIds\":[\"bean_cupper\",\"bean_cupper\",\"mystery_node\",\"\"]," +
                                  "\"discoveredCardIds\":null,\"discoveredMaterialVarietyIds\":[\"gin\",\"gin\",\"rum\"]," +
                                  "\"cocktailIngredientsCollected\":-4,\"teaLeavesCollected\":12}";
            File.WriteAllText(savePath, legacy);

            var result = SaveSystem.LoadDetailed(savePath);
            var data = result.Data;

            Assert.AreEqual(0, result.SourceVersion);
            Assert.AreEqual(SaveSystem.CurrentVersion, data.schemaVersion);
            Assert.AreEqual(9, data.specialtyPoints, "옛 specialtyPoints는 누적 획득으로 그대로");
            CollectionAssert.AreEqual(new[] { "bean_cupper", "mystery_node" }, data.unlockedPermanentSkillIds,
                "중복은 한 번만, 빈 값 제거, 알 수 없는 id 보존");
            Assert.IsNotNull(data.discoveredCardIds);
            Assert.AreEqual(0, data.discoveredCardIds.Count);
            Assert.IsNotNull(data.discoveredSkillIds);
            CollectionAssert.AreEqual(new[] { "gin", "rum" }, data.discoveredMaterialVarietyIds);
            Assert.AreEqual(0, data.cocktailIngredientsCollected);
            Assert.AreEqual(12, data.teaLeavesCollected);
            StringAssert.Contains(SaveSystem.NoticeNegativeClamped, result.Notice, "음수 보정은 명시");
            Assert.AreEqual(0, data.totalRuns + data.clearedRuns + data.failedRuns + data.abandonedRuns);
            Assert.AreEqual(0f, data.bestClearSeconds);
            Assert.IsNull(data.activeRun);
            Assert.AreEqual(legacy, File.ReadAllText(savePath), "로드만으로 원본을 바꾸지 않는다");
        }

        [Test]
        public void LegacyV1_VersionField_Migrates_AndGarbageStatFieldsAreNotTrusted()
        {
            File.WriteAllText(savePath, "{\"version\":1,\"specialtyPoints\":6,\"totalRuns\":40,\"clearedRuns\":7," +
                                        "\"activeRun\":{\"runId\":\"x\"},\"discoveredCardIds\":[\"dry_gin\",\"dry_gin\"]}");
            var result = SaveSystem.LoadDetailed(savePath);

            Assert.AreEqual(1, result.SourceVersion);
            Assert.AreEqual(SaveSystem.LoadSource.Original, result.Source);
            Assert.AreEqual(6, result.Data.specialtyPoints);
            Assert.AreEqual(0, result.Data.totalRuns, "v1에는 런 통계가 없었다 — 추정해 만들지 않는다");
            Assert.AreEqual(0, result.Data.clearedRuns);
            Assert.IsNull(result.Data.activeRun);
            CollectionAssert.AreEqual(new[] { "dry_gin" }, result.Data.discoveredCardIds);
        }

        [Test]
        public void LegacyThenFirstSave_OldFileBecomesBak_NewFileIsV2WithUnknownIdKept()
        {
            const string legacy = "{\"specialtyPoints\":3,\"unlockedPermanentSkillIds\":[\"mystery_node\"]}";
            File.WriteAllText(savePath, legacy);

            PermanentProgress.DiscoverCard("dry_gin");

            Assert.AreEqual(legacy, File.ReadAllText(savePath + ".bak"), "정상 옛 원본이 .bak으로 보존");
            var json = File.ReadAllText(savePath);
            StringAssert.Contains("\"schemaVersion\":2", json);
            StringAssert.Contains("mystery_node", json, "알 수 없는 id는 삭제하지 않는다");
            Assert.AreEqual(1, Disk().saveRevision);
        }

        [Test]
        public void V2_NestedUnknownObjectWithVersionKeys_DoesNotChangeRootClassification()
        {
            File.WriteAllText(savePath, "{\"schemaVersion\":2,\"specialtyPoints\":4,\"ext\":{\"schemaVersion\":9,\"version\":9}}");
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.AreEqual(SaveSystem.LoadSource.Original, result.Source);
            Assert.IsFalse(result.FutureProtected);
            Assert.AreEqual(4, result.Data.specialtyPoints);
        }

        [Test]
        public void SchemaVersionTypeMismatch_IsTreatedAsCorrupt_NotAsZero()
        {
            File.WriteAllText(savePath, "{\"schemaVersion\":\"3\",\"specialtyPoints\":4}");
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.AreEqual(SaveSystem.LoadSource.FreshAfterCorrupt, result.Source);
            Assert.AreEqual(1, CorruptFiles().Length);
        }

        [Test]
        public void ActiveRunNull_IsWrittenAsJsonNull_AndRoundTrips()
        {
            var data = new SaveData { specialtyPoints = 2 };
            Assert.IsTrue(SaveSystem.Save(data, savePath));
            StringAssert.Contains("\"activeRun\":null", File.ReadAllText(savePath));
            Assert.IsNull(Disk().activeRun);

            data.activeRun = new ActiveRunRecord { runId = "r1", seed = 77, cocktail = 3 };
            Assert.IsTrue(SaveSystem.Save(data, savePath));
            var loaded = Disk();
            Assert.AreEqual("r1", loaded.activeRun.runId);
            Assert.AreEqual(77, loaded.activeRun.seed);
            Assert.AreEqual(3, loaded.activeRun.cocktail);
        }

        [Test]
        public void ActiveRunNegativeOrNonFiniteCheckpoint_IsSanitized_AndReported()
        {
            File.WriteAllText(savePath, "{\"schemaVersion\":2,\"saveRevision\":-2,\"activeRun\":{\"runId\":\"r\",\"checkpointGameSeconds\":-5.0,\"tea\":-1}}");
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.AreEqual(0f, result.Data.activeRun.checkpointGameSeconds);
            Assert.AreEqual(0, result.Data.activeRun.tea);
            Assert.AreEqual(0, result.Data.saveRevision);
            StringAssert.Contains(SaveSystem.NoticeNegativeClamped, result.Notice);
        }
    }

    public class SaveV2FutureTests : SaveV2Fixture
    {
        const string Future = "{\"schemaVersion\":3,\"specialtyPoints\":50,\"newThing\":[1,2]}";

        [Test]
        public void FutureOriginal_IsNeverWritten_UntilGuestAccepted_ThenOnlyGuestFileIsWritten()
        {
            File.WriteAllText(savePath, Future);

            Assert.IsTrue(PermanentProgress.IsFutureSaveProtected);
            StringAssert.Contains(SaveSystem.NoticeFutureVersion, PermanentProgress.LastLoadIssue);
            Assert.AreEqual(3, PermanentProgress.LastLoad.SourceVersion);
            Assert.AreEqual(0, PermanentProgress.Current.specialtyPoints, "미래 데이터를 추측해 읽지 않고 임시 새 진행");

            PermanentProgress.DiscoverCard("dry_gin");
            PermanentProgress.BeginRun(5);
            Assert.AreEqual(Future, File.ReadAllText(savePath), "버튼 전에는 원본을 쓰지 않는다");
            Assert.IsFalse(File.Exists(savePath + ".bak"));
            Assert.IsFalse(File.Exists(Path.Combine(dir, "guest_session.json")), "버튼 전에는 어떤 파일도 새로 쓰지 않는다");
            Assert.IsFalse(PermanentProgress.LastSaveFailed, "쓰기 보류는 디스크 실패로 표시하지 않는다");

            Assert.IsTrue(PermanentProgress.AcceptGuestSession());
            Assert.IsTrue(PermanentProgress.IsGuestSession);
            var guest = SaveSystem.Load(Path.Combine(dir, "guest_session.json"));
            CollectionAssert.Contains(guest.discoveredCardIds, "dry_gin");
            Assert.AreEqual(1, guest.totalRuns);

            PermanentProgress.DiscoverCard("green_tea");
            Assert.AreEqual(Future, File.ReadAllText(savePath), "임시 플레이 중에도 원본은 그대로");
            Assert.IsFalse(File.Exists(savePath + ".bak"));
            Assert.AreEqual(0, CorruptFiles().Length, "미래 버전을 손상으로 취급하지 않는다");
        }

        [Test]
        public void FutureLegacyRootVersion_WithNestedSchemaZero_StaysProtected()
        {
            const string json = "{\"version\":3,\"specialtyPoints\":8,\"extra\":{\"schemaVersion\":0}}";
            File.WriteAllText(savePath, json);
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.IsTrue(result.FutureProtected);
            Assert.AreEqual(3, result.SourceVersion);
            Assert.IsFalse(SaveSystem.Save(new SaveData(), savePath), "Save 직접 호출도 미래 원본을 덮지 않는다");
            Assert.AreEqual(json, File.ReadAllText(savePath));
        }

        [Test]
        public void FutureBackup_WithOriginalMissing_IsProtected_AndNotReplacedByOlderData()
        {
            File.WriteAllText(savePath + ".bak", Future);
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.IsTrue(result.FutureProtected);
            Assert.AreEqual(3, result.SourceVersion, "진단의 버전은 실제 .bak 버전");

            PermanentProgress.DiscoverCard("dry_gin");
            Assert.IsFalse(File.Exists(savePath));
            Assert.AreEqual(Future, File.ReadAllText(savePath + ".bak"));
        }

        [Test]
        public void ValidOriginal_WithFutureBackup_SaveDoesNotOverwriteFutureBackup()
        {
            File.WriteAllText(savePath, "{\"schemaVersion\":2,\"specialtyPoints\":1}");
            File.WriteAllText(savePath + ".bak", Future);

            Assert.IsTrue(SaveSystem.Save(new SaveData { specialtyPoints = 2 }, savePath));
            Assert.AreEqual(2, Disk().specialtyPoints);
            Assert.AreEqual(Future, File.ReadAllText(savePath + ".bak"));
        }

        [Test]
        public void SaveRefusesFutureSchemaData_WithoutMutatingIt()
        {
            var data = new SaveData { schemaVersion = 3, saveRevision = 4, specialtyPoints = 1 };
            Assert.IsFalse(SaveSystem.Save(data, savePath));
            Assert.AreEqual(3, data.schemaVersion);
            Assert.AreEqual(4, data.saveRevision);
            Assert.IsFalse(File.Exists(savePath));
            Assert.IsFalse(File.Exists(savePath + ".tmp"));
        }
    }

    public class SaveV2IntegrityTests : SaveV2Fixture
    {
        [Test]
        public void JsonSyntax_RejectsEveryTruncationOfAValidSave()
        {
            var json = SaveSystem.Serialize(new SaveData
            {
                specialtyPoints = 12,
                discoveredCardIds = new List<string> { "dry_gin", "green_tea" },
                activeRun = new ActiveRunRecord { runId = "abc", checkpointGameSeconds = 12.5f },
            });
            Assert.IsTrue(JsonSyntax.IsCompleteObject(json));
            for (int len = 0; len < json.Length; len++)
                Assert.IsFalse(JsonSyntax.IsCompleteObject(json.Substring(0, len)), $"잘린 길이 {len}");
            Assert.IsFalse(JsonSyntax.IsCompleteObject(json + "x"));
            Assert.IsFalse(JsonSyntax.IsCompleteObject("{\"a\":1,\"a\":2}"), "루트 중복 키는 모호 — 거부");
        }

        [Test]
        public void TruncatedOriginal_FallsBackToValidBak_AndPreservesCorruptCopy()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 5 }, savePath);
            SaveSystem.Save(new SaveData { specialtyPoints = 9 }, savePath); // .bak = 5
            var full = File.ReadAllText(savePath);
            var truncated = full.Substring(0, full.Length / 2);
            File.WriteAllText(savePath, truncated);

            var result = SaveSystem.LoadDetailed(savePath);
            Assert.AreEqual(SaveSystem.LoadSource.Backup, result.Source);
            Assert.AreEqual(5, result.Data.specialtyPoints);
            Assert.AreEqual(SaveSystem.NoticeBackupRestored, result.Notice, "정상 .bak 복구면 그 사실만");
            var corrupt = CorruptFiles();
            Assert.AreEqual(1, corrupt.Length);
            Assert.AreEqual(truncated, File.ReadAllText(corrupt[0]), "손상 원본을 시간 접미사 .corrupt로 그대로 보존");
        }

        [Test]
        public void CorruptOriginal_NextSave_DoesNotOverwriteGoodBak()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 5 }, savePath);
            SaveSystem.Save(new SaveData { specialtyPoints = 9 }, savePath); // .bak = 5
            var goodBak = File.ReadAllText(savePath + ".bak");
            File.WriteAllText(savePath, "{ broken");

            Relaunch();
            Assert.AreEqual(5, PermanentProgress.Current.specialtyPoints);
            PermanentProgress.DiscoverCard("dry_gin");

            Assert.AreEqual(goodBak, File.ReadAllText(savePath + ".bak"), "손상 원본으로 정상 .bak을 덮지 않는다");
            Assert.AreEqual(5, Disk().specialtyPoints);
            CollectionAssert.Contains(Disk().discoveredCardIds, "dry_gin");
            Assert.AreEqual(1, CorruptFiles().Length, "같은 손상 내용은 한 번만 보존");
        }

        [Test]
        public void CorruptOriginal_NoBackup_StartsFresh_WithNotice_AndKeepsCorruptCopy()
        {
            File.WriteAllText(savePath, "{\"specialtyPoints\":7,");
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.AreEqual(SaveSystem.LoadSource.FreshAfterCorrupt, result.Source);
            Assert.AreEqual(0, result.Data.specialtyPoints);
            Assert.AreEqual(SaveSystem.NoticeCorruptFresh, result.Notice);
            Assert.AreEqual(1, CorruptFiles().Length);
        }

        [Test]
        public void OriginalAbsent_ValidBak_IsLoaded()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 4 }, savePath + ".bak");
            var result = SaveSystem.LoadDetailed(savePath);
            Assert.AreEqual(SaveSystem.LoadSource.Backup, result.Source);
            Assert.AreEqual(4, result.Data.specialtyPoints);
        }

        [Test]
        public void LeftoverTmp_IsNeverAutoApplied_ValidOrGarbage()
        {
            SaveSystem.Save(new SaveData { specialtyPoints = 1 }, savePath);
            File.WriteAllText(savePath + ".tmp", SaveSystem.Serialize(new SaveData { specialtyPoints = 99, saveRevision = 50 }));
            Assert.AreEqual(1, SaveSystem.Load(savePath).specialtyPoints, "완료 확인 없는 .tmp는 적용하지 않는다");

            File.WriteAllText(savePath + ".tmp", "{\"specialty");
            Assert.AreEqual(1, SaveSystem.Load(savePath).specialtyPoints);
        }

        [Test]
        public void CrashAfterBackupBeforeReplace_LoadsOldSnapshotFromBak_NotTmp()
        {
            // 원본이 .bak으로 옮겨지고 새 .tmp가 아직 원본 자리에 오지 않은 순간에 프로세스가 죽은 상태
            SaveSystem.Save(new SaveData { specialtyPoints = 3 }, savePath + ".bak");
            File.WriteAllText(savePath + ".tmp", SaveSystem.Serialize(new SaveData { specialtyPoints = 8 }));
            Assert.AreEqual(3, SaveSystem.Load(savePath).specialtyPoints);
        }

        [Test]
        public void InterruptedWrite_AtEachStage_LeavesOldSnapshot_AndRevisionUnchanged()
        {
            var data = new SaveData { specialtyPoints = 1 };
            Assert.IsTrue(SaveSystem.Save(data, savePath));
            Assert.AreEqual(1, data.saveRevision);
            var before = File.ReadAllText(savePath);

            foreach (SaveSystem.SaveStage stage in Enum.GetValues(typeof(SaveSystem.SaveStage)))
            {
                data.specialtyPoints = 50;
                FailAt(stage);
                Assert.IsFalse(SaveSystem.Save(data, savePath), stage.ToString());
                Assert.IsTrue(SaveSystem.LastSaveFailed);
                Assert.AreEqual(1, data.saveRevision, $"{stage}: 실패하면 리비전을 올리지 않는다");
                Assert.AreEqual(before, File.ReadAllText(savePath), $"{stage}: 원본은 이전 정상본");
                Assert.AreEqual(1, SaveSystem.Load(savePath).specialtyPoints);
            }

            ClearFault();
            Assert.IsTrue(SaveSystem.Save(data, savePath), "남은 .tmp가 있어도 다음 저장은 정상");
            Assert.AreEqual(2, data.saveRevision);
            Assert.AreEqual(50, Disk().specialtyPoints);
            Assert.AreEqual(1, SaveSystem.Load(savePath + ".bak").specialtyPoints);
        }
    }

    public class SaveV2RunLifecycleTests : SaveV2Fixture
    {
        [Test]
        public void BeginRun_IncrementsTotalRuns_AndWritesActiveRunImmediately()
        {
            var runId = PermanentProgress.BeginRun(1234);
            var disk = Disk();
            Assert.AreEqual(1, disk.totalRuns);
            Assert.IsNotNull(disk.activeRun);
            Assert.AreEqual(runId, disk.activeRun.runId);
            Assert.AreEqual(1234, disk.activeRun.seed);
            Assert.IsFalse(disk.activeRun.resultCommitted);
            Assert.IsFalse(string.IsNullOrEmpty(disk.activeRun.startedUtc));
        }

        [Test]
        public void Checkpoint_EveryFiveGameSeconds_OnlyWhenChanged_AddsOnlyDelta_InSameSnapshot()
        {
            var runId = PermanentProgress.BeginRun(1);
            int rev = Disk().saveRevision;

            PermanentProgress.SetRunProgress(runId, 5, 2, 0, 1f);
            Assert.IsFalse(PermanentProgress.CheckpointIfDue(runId), "5초 전에는 저장하지 않는다");
            PermanentProgress.SetRunProgress(runId, 5, 2, 0, 5f);
            Assert.IsTrue(PermanentProgress.CheckpointIfDue(runId));
            var disk = Disk();
            Assert.AreEqual(5, disk.cocktailIngredientsCollected);
            Assert.AreEqual(2, disk.teaLeavesCollected);
            Assert.AreEqual(5, disk.activeRun.cocktail, "반영량은 같은 저장에 함께");
            Assert.AreEqual(2, disk.activeRun.tea);
            Assert.AreEqual(5f, disk.activeRun.checkpointGameSeconds, 0.001f);
            Assert.AreEqual(rev + 1, disk.saveRevision);

            PermanentProgress.SetRunProgress(runId, 8, 2, 4, 7f);
            Assert.IsFalse(PermanentProgress.CheckpointIfDue(runId));
            PermanentProgress.SetRunProgress(runId, 8, 2, 4, 10.1f);
            Assert.IsTrue(PermanentProgress.CheckpointIfDue(runId));
            disk = Disk();
            Assert.AreEqual(8, disk.cocktailIngredientsCollected, "차이(3)만 더한다 — 13이 아님");
            Assert.AreEqual(4, disk.coffeeBeansCollected);

            PermanentProgress.SetRunProgress(runId, 8, 2, 4, 30f);
            Assert.IsFalse(PermanentProgress.CheckpointIfDue(runId), "변경이 없으면 저장하지 않는다");
        }

        [Test]
        public void ImmediateDiscovery_DuringRun_IncludesPendingDeltaInSameSave()
        {
            var runId = PermanentProgress.BeginRun(1);
            PermanentProgress.SetRunProgress(runId, 3, 0, 0, 0.5f);
            PermanentProgress.DiscoverCard("dry_gin");
            var disk = Disk();
            CollectionAssert.Contains(disk.discoveredCardIds, "dry_gin");
            Assert.AreEqual(3, disk.cocktailIngredientsCollected);
            Assert.AreEqual(3, disk.activeRun.cocktail);

            PermanentProgress.DiscoverSkill("espresso_shot");
            CollectionAssert.Contains(Disk().discoveredSkillIds, "espresso_shot");
        }

        [Test]
        public void ForcedTermination_NextLoad_AbandonsOnce_KeepsCheckpointOnly_ShowsNotice()
        {
            var runId = PermanentProgress.BeginRun(1);
            PermanentProgress.SetRunProgress(runId, 4, 0, 0, 5f);
            PermanentProgress.CheckpointIfDue(runId);
            PermanentProgress.SetRunProgress(runId, 9, 0, 0, 7f); // 마지막 5초 미저장분

            Relaunch(); // 강제종료 → 다음 실행
            var data = PermanentProgress.Current;
            Assert.AreEqual(1, data.abandonedRuns);
            Assert.IsNull(data.activeRun);
            Assert.AreEqual(4, data.cocktailIngredientsCollected, "체크포인트까지만 유지, 미저장분 복원 없음");
            StringAssert.Contains(PermanentProgress.NoticeRunInterrupted, PermanentProgress.LastLoadIssue);
            Assert.AreEqual(1, Disk().abandonedRuns, "복구 결과를 1회 저장");
            Assert.IsNull(Disk().activeRun);

            Relaunch();
            Assert.AreEqual(1, PermanentProgress.Current.abandonedRuns, "다음 실행에서 다시 세지 않는다");
            Assert.IsNull(PermanentProgress.LastLoadIssue);
        }

        [Test]
        public void RecoverySaveFailure_StaysDirty_NeverCountsTwiceInSession_RetriesAfterFiveSeconds()
        {
            PermanentProgress.BeginRun(1);
            FailAt(SaveSystem.SaveStage.WriteTmp);

            Relaunch();
            Assert.AreEqual(1, PermanentProgress.Current.abandonedRuns);
            Assert.AreEqual(1, PermanentProgress.Current.abandonedRuns);
            Assert.IsTrue(PermanentProgress.LastSaveFailed);
            Assert.IsTrue(PermanentProgress.IsDirty);
            Assert.IsNotNull(Disk().activeRun, "디스크는 아직 이전 정상본");

            ClearFault();
            PermanentProgress.Tick(4.9f);
            Assert.IsNotNull(Disk().activeRun, "5초 전에는 재시도하지 않는다");
            PermanentProgress.Tick(0.2f);
            Assert.IsFalse(PermanentProgress.LastSaveFailed);
            Assert.AreEqual(1, Disk().abandonedRuns);
            Assert.IsNull(Disk().activeRun);
            Assert.AreEqual(1, PermanentProgress.Current.abandonedRuns);
        }

        [Test]
        public void CommitRunEnd_IsIdempotent_AndDoesNotReAddSpecialty()
        {
            var runId = PermanentProgress.BeginRun(1);
            Assert.IsTrue(PermanentProgress.AwardRunSpecialty(runId, 3));
            Assert.IsFalse(PermanentProgress.AwardRunSpecialty(runId, 3), "같은 런의 중복 보스 이벤트는 상한으로 거부");
            Assert.AreEqual(3, Disk().specialtyPoints, "스페셜티는 즉시 저장");
            Assert.AreEqual(3, Disk().activeRun.specialtyAwarded);

            Assert.IsTrue(PermanentProgress.CommitRunEnd(runId, RunOutcome.Cleared, 900f));
            Assert.IsFalse(PermanentProgress.CommitRunEnd(runId, RunOutcome.Cleared, 900f));
            Assert.IsFalse(PermanentProgress.CommitRunEnd(runId, RunOutcome.Abandoned, 900f), "결과가 달라도 같은 runId는 한 번만");
            Assert.IsFalse(PermanentProgress.AwardRunSpecialty(runId, 3), "종료된 런에는 지급 없음");

            var disk = Disk();
            Assert.AreEqual(1, disk.totalRuns);
            Assert.AreEqual(1, disk.clearedRuns);
            Assert.AreEqual(0, disk.abandonedRuns);
            Assert.AreEqual(3, disk.specialtyPoints, "종료 집계가 스페셜티를 다시 더하지 않는다");
            Assert.AreEqual(runId, disk.lastCommittedRunId);
            Assert.IsNull(disk.activeRun);
        }

        [Test]
        public void RunStats_AllOutcomes_ExactlyOneCounterEach_BestClearIsMinimum()
        {
            void Run(RunOutcome outcome, float seconds, int cocktail)
            {
                var id = PermanentProgress.BeginRun(7);
                PermanentProgress.SetRunProgress(id, cocktail, 0, 0, seconds);
                Assert.IsTrue(PermanentProgress.CommitRunEnd(id, outcome, seconds));
            }

            Run(RunOutcome.Failed, 120f, 2);
            Assert.AreEqual(0f, Disk().bestClearSeconds, "클리어 없으면 0");
            Run(RunOutcome.Cleared, 900f, 3);
            Run(RunOutcome.Cleared, 800f, 1);
            Run(RunOutcome.Cleared, 1000f, 1);
            Run(RunOutcome.Abandoned, 30f, 4);
            Run(RunOutcome.Failed, 1200f, 0);

            var disk = Disk();
            Assert.AreEqual(6, disk.totalRuns);
            Assert.AreEqual(3, disk.clearedRuns);
            Assert.AreEqual(2, disk.failedRuns);
            Assert.AreEqual(1, disk.abandonedRuns);
            Assert.AreEqual(disk.totalRuns, disk.clearedRuns + disk.failedRuns + disk.abandonedRuns);
            Assert.AreEqual(800f, disk.bestClearSeconds, 0.001f);
            Assert.AreEqual(11, disk.cocktailIngredientsCollected, "실패/포기 런의 획득분도 보존(07:179)");
        }

        [Test]
        public void BeginRun_WithOrphanActiveRunInSession_CountsItAbandonedOnce()
        {
            PermanentProgress.BeginRun(1);
            PermanentProgress.BeginRun(2);
            var disk = Disk();
            Assert.AreEqual(2, disk.totalRuns);
            Assert.AreEqual(1, disk.abandonedRuns);
            Assert.AreEqual(2, disk.activeRun.seed);
        }

        [Test]
        public void SaveFailureDuringRun_KeepsMemoryProgress_AndRetryPersistsWithoutDoubleCount()
        {
            var runId = PermanentProgress.BeginRun(1);
            FailAt(SaveSystem.SaveStage.BeforeReplace);
            PermanentProgress.SetRunProgress(runId, 6, 0, 0, 5f);
            PermanentProgress.CheckpointIfDue(runId);
            Assert.IsTrue(PermanentProgress.LastSaveFailed);
            Assert.AreEqual(6, PermanentProgress.Current.cocktailIngredientsCollected, "메모리 진행 유지");
            Assert.AreEqual(0, Disk().cocktailIngredientsCollected);

            Assert.IsTrue(PermanentProgress.CommitRunEnd(runId, RunOutcome.Failed, 6f));
            ClearFault();
            Assert.IsTrue(PermanentProgress.RetrySave(), "사용자 버튼 재시도");
            var disk = Disk();
            Assert.AreEqual(6, disk.cocktailIngredientsCollected);
            Assert.AreEqual(1, disk.failedRuns);
            Assert.IsNull(disk.activeRun);
        }
    }

    public class SaveV2PurchaseTests : SaveV2Fixture
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyNodes()
        {
            foreach (var o in created) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void Purchase_SaveFailure_RollsBackUnlockCostAndEffects_ThenRetrySucceeds()
        {
            var nodes = Nodes(created);
            var cupper = nodes.Single(n => n.id == "bean_cupper");
            PermanentProgress.AddSpecialty(3);
            var diskBefore = File.ReadAllText(savePath);

            FailAt(SaveSystem.SaveStage.AfterTmpWritten);
            Assert.AreEqual(PermanentPurchaseResult.SaveFailed, PermanentProgress.TryPurchasePermanentSkill(cupper, nodes));
            CollectionAssert.DoesNotContain(PermanentProgress.Current.unlockedPermanentSkillIds, "bean_cupper");
            var tree = new PermanentSkillTree(nodes, PermanentProgress.Current.unlockedPermanentSkillIds);
            Assert.AreEqual(3, PermanentProgress.Current.specialtyPoints - tree.SpentPoints, "차감도 롤백");
            var stats = new PlayerStats();
            tree.ApplyAll(stats);
            Assert.AreEqual(0f, stats.beanGainPercent, "소유하지 않은 노드 효과 없음");
            Assert.AreEqual(diskBefore, File.ReadAllText(savePath));
            Assert.IsFalse(PermanentProgress.IsDirty, "롤백된 메모리는 디스크와 같다");

            ClearFault();
            Assert.AreEqual(PermanentPurchaseResult.Purchased, PermanentProgress.TryPurchasePermanentSkill(cupper, nodes));
            CollectionAssert.Contains(Disk().unlockedPermanentSkillIds, "bean_cupper");
            Assert.AreEqual(PermanentPurchaseResult.Denied, PermanentProgress.TryPurchasePermanentSkill(cupper, nodes),
                "중복 구매 없음");
        }

        [Test]
        public void NegativeBalance_LocksPurchases_KeepsValidEffects_DoesNotChangeTotals()
        {
            File.WriteAllText(savePath, "{\"specialtyPoints\":3,\"unlockedPermanentSkillIds\":[\"bean_cupper\",\"skilled_roasting\"]}");
            var nodes = Nodes(created);
            var dark = nodes.Single(n => n.id == "dark_roast_blend");

            Assert.AreEqual(PermanentPurchaseResult.Locked, PermanentProgress.TryPurchasePermanentSkill(dark, nodes));
            Assert.AreEqual(3, PermanentProgress.Current.specialtyPoints, "잔액/총량을 임의로 늘리지 않는다");
            var tree = new PermanentSkillTree(nodes, PermanentProgress.Current.unlockedPermanentSkillIds);
            var stats = new PlayerStats();
            tree.ApplyAll(stats);
            Assert.AreEqual(0.20f, stats.beanGainPercent, 0.0001f, "기존 유효 노드 효과 유지");
            Assert.IsFalse(File.Exists(savePath + ".bak"), "잠긴 구매는 저장하지 않는다");
        }

        [Test]
        public void MissingPrerequisite_LegacyNodeInactive_Preserved_RepairedOnlyByNormalPurchase()
        {
            File.WriteAllText(savePath, "{\"specialtyPoints\":3,\"unlockedPermanentSkillIds\":[\"skilled_roasting\"]}");
            var nodes = Nodes(created);
            var cupper = nodes.Single(n => n.id == "bean_cupper");
            var roasting = nodes.Single(n => n.id == "skilled_roasting");

            var tree = new PermanentSkillTree(nodes, PermanentProgress.Current.unlockedPermanentSkillIds);
            Assert.IsTrue(tree.IsInactiveLegacy(roasting));
            Assert.IsTrue(tree.HasInactiveLegacyNodes);
            Assert.IsFalse(tree.IsUnlocked(roasting));
            Assert.IsFalse(tree.IsUnlocked(cupper), "선행을 무료로 만들지 않는다");
            Assert.AreEqual(0, tree.SpentPoints, "유효 구매 비용만 계산");
            var stats = new PlayerStats();
            tree.ApplyAll(stats);
            Assert.AreEqual(0f, stats.gaugeNeedPercent, 0.0001f, "비활성 노드 효과 없음");

            Assert.AreEqual(PermanentPurchaseResult.Denied, PermanentProgress.TryPurchasePermanentSkill(cupper, nodes),
                "선행 구매로 비활성 노드가 살아나면 잔액이 음수 — 거부");
            PermanentProgress.AddSpecialty(6); // 총 9
            CollectionAssert.Contains(Disk().unlockedPermanentSkillIds, "skilled_roasting", "기록은 유지");

            Assert.AreEqual(PermanentPurchaseResult.Purchased, PermanentProgress.TryPurchasePermanentSkill(cupper, nodes));
            tree = new PermanentSkillTree(nodes, PermanentProgress.Current.unlockedPermanentSkillIds);
            Assert.IsTrue(tree.IsUnlocked(roasting), "정상 구매로 복구");
            Assert.AreEqual(9, tree.SpentPoints);
        }
    }

    public class SaveV2RetryTests : SaveV2Fixture
    {
        [Test]
        public void WriteFailure_MarksDirty_AutoRetryEveryFiveRealSeconds_UntilSuccess()
        {
            FailAt(SaveSystem.SaveStage.WriteTmp);
            PermanentProgress.DiscoverCard("dry_gin");
            Assert.AreEqual(1, faultHits);
            Assert.IsTrue(PermanentProgress.LastSaveFailed);
            Assert.IsTrue(PermanentProgress.IsDirty);
            CollectionAssert.Contains(PermanentProgress.Current.discoveredCardIds, "dry_gin", "메모리 진행 유지·플레이 가능");

            PermanentProgress.Tick(4.9f);
            Assert.AreEqual(1, faultHits, "5초 전 재시도 없음");
            PermanentProgress.Tick(0.2f);
            Assert.AreEqual(2, faultHits, "5초 간격 재시도");
            PermanentProgress.Tick(5f);
            Assert.AreEqual(3, faultHits);

            ClearFault();
            PermanentProgress.Tick(5f);
            Assert.IsFalse(PermanentProgress.LastSaveFailed);
            Assert.IsFalse(PermanentProgress.IsDirty);
            CollectionAssert.Contains(Disk().discoveredCardIds, "dry_gin");

            int rev = Disk().saveRevision;
            PermanentProgress.Tick(10f);
            Assert.AreEqual(rev, Disk().saveRevision, "깨끗하면 재시도 저장 없음");
        }
    }

    /// <summary>실제 GameManager 경로(Initialize/AddIngredient/보스 처치/종료)의 저장 연결. 엔진 프레임 루프는 돌리지 않는다.</summary>
    public class SaveV2GameManagerTests : SaveV2Fixture
    {
        GameObject root;
        GameObject createdCamera;
        GameManager game;

        [SetUp]
        public void CreateGame()
        {
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
        }

        [TearDown]
        public void DestroyGame()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (createdCamera != null) UnityEngine.Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
        }

        [Test]
        public void Initialize_BeginsRun_TotalRunsAndActiveRunOnDisk()
        {
            Assert.IsFalse(string.IsNullOrEmpty(game.RunId));
            var disk = Disk();
            Assert.AreEqual(1, disk.totalRuns);
            Assert.AreEqual(game.RunId, disk.activeRun.runId);
            Assert.AreEqual(game.Seed, disk.activeRun.seed);
        }

        [Test]
        public void FirstVarietyDiscovery_SaveIncludesThePickupThatDiscoveredIt()
        {
            game.AddIngredient(IngredientType.Cocktail, 2);
            var disk = Disk();
            Assert.AreEqual(1, disk.discoveredMaterialVarietyIds.Count);
            Assert.AreEqual(2, disk.cocktailIngredientsCollected, "발견 저장에 이번 픽업 재료가 포함");
            Assert.AreEqual(2, disk.activeRun.cocktail);
        }

        [Test]
        public void CardSelectionOpening_SavesPickupsCollectedSinceLastSave()
        {
            // 품종을 미리 모두 발견시켜 픽업이 "첫 발견 즉시 저장"을 일으키지 않게 한다.
            foreach (var v in MaterialVarieties.For(IngredientType.Cocktail)) PermanentProgress.DiscoverMaterialVariety(v.id);
            int threshold = game.CocktailGauge.Threshold;
            game.AddIngredient(IngredientType.Cocktail, threshold);
            Assert.AreEqual(0, Disk().cocktailIngredientsCollected, "선택 창 전에는 아직 미저장(5초 체크포인트 전)");

            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.LevelUp, game.State);
            var disk = Disk();
            Assert.AreEqual(threshold, disk.cocktailIngredientsCollected, "카드 창 진입 시 즉시 저장(08:155)");
            Assert.AreEqual(threshold, disk.activeRun.cocktail);

            game.CardView.Choose(0);
            Assert.AreEqual(1, Disk().discoveredCardIds.Count, "첫 카드 발견 즉시 저장");
        }

        [Test]
        public void DuplicateEnhancedBossKill_GrantsSpecialtyOnce()
        {
            game.OnEnemyKilled(Vector2.zero, EnemyDef.EspressoLeviathan);
            game.OnEnemyKilled(Vector2.zero, EnemyDef.EspressoLeviathan);
            Assert.AreEqual(Balance.SpecialtyDropAmount, Disk().specialtyPoints);
            Assert.AreEqual(Balance.SpecialtyDropAmount, PermanentProgress.Current.specialtyPoints);
        }

        [Test]
        public void Death_CommitsFailedOnce_FrozenChainKept_LaterAbandonPathsDoNotRecount()
        {
            game.AddIngredient(IngredientType.Tea, 3);
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(0f, Time.timeScale, "종료 후 시간 정지 유지");

            game.GiveUp(); // GameOver 뒤 포기 요청은 무시
            game.ResolveEndOfFrame();
            var disk = Disk();
            Assert.AreEqual(1, disk.failedRuns);
            Assert.AreEqual(0, disk.abandonedRuns);
            Assert.AreEqual(3, disk.teaLeavesCollected);
            Assert.IsNull(disk.activeRun);
            Assert.AreEqual(game.RunId, disk.lastCommittedRunId);
        }

        [Test]
        public void GiveUp_CommitsAbandonedOnly()
        {
            game.GiveUp(); // Pause/카운트다운 코루틴은 EditMode에서 돌지 않으므로 Pause 없이 직접 호출
            Assert.AreEqual("포기", game.EndReason);
            var disk = Disk();
            Assert.AreEqual(1, disk.abandonedRuns);
            Assert.AreEqual(0, disk.failedRuns);
            Assert.AreEqual(0, disk.clearedRuns);
        }

        [Test]
        public void Timeout_CommitsFailed()
        {
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            game.ResolveEndOfFrame();
            Assert.AreEqual("시간 초과", game.EndReason);
            Assert.AreEqual(1, Disk().failedRuns);
        }
    }
}
