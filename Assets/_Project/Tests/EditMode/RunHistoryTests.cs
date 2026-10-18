using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 67(2026-10-16) 플레이 결과 기록 — run-history.json 저장 계약(왕복·최신순·20판·중복·손상·미래 버전·IO 실패·기존 저장 보존)과
    /// 실제 GameManager의 종료 스냅샷(카드·중첩·완성 조합·종료 종류·사망 원인)·피해 출처 판정.
    /// 모든 파일은 테스트마다 새 임시 폴더(save.json·run-history.json 실제 이름)에서만 다룬다.
    /// </summary>
    public class RunHistoryTests
    {
        string dir;
        string savePath;
        string historyPath;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, $"cct_runhistory_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            savePath = Path.Combine(dir, "save.json");
            historyPath = Path.Combine(dir, "run-history.json");
            PermanentProgress.SetPathForTests(savePath);
            RunHistory.SetPathForTests(null);
            RunHistoryStore.FaultHookForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyGame();
            RunHistoryStore.FaultHookForTests = null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            RunHistory.SetPathForTests(null);
            Time.timeScale = 1f;
            try { Directory.Delete(dir, true); } catch { /* 임시 폴더 */ }
        }

        static RunRecord Record(string id, RunEndKind kind = RunEndKind.Died, string cause = "라임웨지 · 몸통 접촉")
        {
            var r = new RunRecord
            {
                runId = id, endedUtc = DateTime.UtcNow.ToString("o"), endKind = kind.ToString(),
                job = Job.Bartender.ToString(), jobName = "바텐더", weapon = WeaponKind.Rapier.ToString(), weaponName = "레이피어",
                runSeconds = 321.5f, kills = 77, cocktailLevel = 4, teaLevel = 2, researchUnlocked = 3, specialtyEarned = 3,
                hybridUnlocked = true, deathCause = kind == RunEndKind.Died ? cause : "", deathCauseKind = kind == RunEndKind.Died ? "Contact" : "",
                appVersion = "test",
            };
            r.cards.Add(new RunCardEntry { id = "dry_gin", name = "드라이 진", category = "Cocktail", stacks = 5, maxStacks = 5 });
            r.cards.Add(new RunCardEntry { id = "green_tea", name = "녹차", category = "Tea", stacks = 2, maxStacks = 5 });
            r.combos.Add(new RunNamedEntry { kind = "hybrid", id = "hybrid_tea_martini", name = "티니 보우" });
            r.combos.Add(new RunNamedEntry { kind = "finalForm", id = "Flame", name = "집중 연소" });
            r.research.Add(new RunNamedEntry { kind = "research", id = "espresso_shot", name = "에스프레소 샷" });
            return r;
        }

        // ── 저장 계약 ─────────────────────────────────────────────

        [Test]
        public void Store_RoundTrip_KeepsEveryField()
        {
            var original = Record("a1");
            Assert.IsTrue(RunHistoryStore.Save(historyPath, new System.Collections.Generic.List<RunRecord> { original }));
            var loaded = RunHistoryStore.Load(historyPath);
            Assert.AreEqual(RunHistoryStore.LoadSource.Original, loaded.Source);
            Assert.AreEqual(1, loaded.Records.Count);
            var r = loaded.Records[0];
            Assert.AreEqual("a1", r.runId);
            Assert.AreEqual(RunEndKind.Died, r.EndKind);
            Assert.AreEqual("라임웨지 · 몸통 접촉", r.deathCause);
            Assert.AreEqual(321.5f, r.runSeconds, 0.001f);
            Assert.AreEqual(77, r.kills);
            Assert.IsTrue(r.hybridUnlocked);
            CollectionAssert.AreEqual(new[] { "dry_gin:5/5", "green_tea:2/5" }, r.cards.Select(c => $"{c.id}:{c.stacks}/{c.maxStacks}").ToArray());
            CollectionAssert.AreEqual(new[] { "hybrid:hybrid_tea_martini", "finalForm:Flame" }, r.combos.Select(c => $"{c.kind}:{c.id}").ToArray());
            Assert.AreEqual("espresso_shot", r.research.Single().id);
            StringAssert.Contains("\"schemaVersion\":1", File.ReadAllText(historyPath));
        }

        [Test]
        public void Add_NewestFirst_KeepsOnly20_AndSurvivesReload()
        {
            for (int i = 0; i < 25; i++) Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("run" + i)));
            Assert.AreEqual(RunHistoryStore.MaxRecords, RunHistory.Records.Count);
            Assert.AreEqual("run24", RunHistory.Records[0].runId, "최신이 맨 앞");
            Assert.AreEqual("run5", RunHistory.Records[19].runId, "가장 오래된 5판은 버린다");

            RunHistory.ReloadForTests(); // 새 실행처럼 디스크에서 다시
            CollectionAssert.AreEqual(Enumerable.Range(5, 20).Reverse().Select(i => "run" + i).ToArray(),
                RunHistory.Records.Select(r => r.runId).ToArray());
            Assert.AreEqual(Path.GetFullPath(historyPath), Path.GetFullPath(RunHistory.ActivePath), "save.json 폴더의 run-history.json");
        }

        [Test]
        public void Add_SameRunIdTwice_IsRecordedOnce()
        {
            Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("dup")));
            Assert.AreEqual(RunHistory.AddResult.Duplicate, RunHistory.Add(Record("dup", RunEndKind.Abandoned)));
            Assert.AreEqual(1, RunHistory.Records.Count);
            Assert.AreEqual(RunEndKind.Died, RunHistory.Records[0].EndKind, "먼저 기록한 종료가 유지된다");
            RunHistory.ReloadForTests();
            Assert.AreEqual(1, RunHistory.Records.Count);
            Assert.AreEqual(RunHistory.AddResult.Invalid, RunHistory.Add(new RunRecord { runId = "" }));
        }

        [Test]
        public void Load_DuplicateAndEmptyIdsInFile_AreCleaned()
        {
            var a = Record("x"); var b = Record("x"); b.kills = 1; var c = Record("");
            File.WriteAllText(historyPath, RunHistoryStore.Serialize(new System.Collections.Generic.List<RunRecord> { a, b, c }));
            var loaded = RunHistoryStore.Load(historyPath);
            Assert.AreEqual(1, loaded.Records.Count);
            Assert.AreEqual(77, loaded.Records[0].kills, "앞(최신) 기록을 남긴다");
        }

        [Test]
        public void CorruptFile_IsPreserved_AndValidBackupIsUsed_AndNotOverwrittenByCorrupt()
        {
            Assert.IsTrue(RunHistoryStore.Save(historyPath, new System.Collections.Generic.List<RunRecord> { Record("old") }));
            Assert.IsTrue(RunHistoryStore.Save(historyPath, new System.Collections.Generic.List<RunRecord> { Record("new"), Record("old") }));
            var goodBackup = File.ReadAllBytes(historyPath + ".bak");
            File.WriteAllText(historyPath, "{\"schemaVersion\":1,\"runs\":[{\"runId\":\"tru");
            var corruptBytes = File.ReadAllBytes(historyPath);

            LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(1, RunHistory.Records.Count, "정상 .bak(1판)으로 복구");
            Assert.AreEqual("old", RunHistory.Records[0].runId);
            Assert.AreEqual(RunHistory.NoticeBackup, RunHistory.Notice);
            var copies = Directory.GetFiles(dir, "run-history.json.*.corrupt");
            Assert.AreEqual(1, copies.Length, "손상 원본을 .corrupt로 보존");
            CollectionAssert.AreEqual(corruptBytes, File.ReadAllBytes(copies[0]));

            Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("after")));
            CollectionAssert.AreEqual(goodBackup, File.ReadAllBytes(historyPath + ".bak"), "손상 원본으로 정상 .bak을 덮지 않는다");
            RunHistory.ReloadForTests();
            CollectionAssert.AreEqual(new[] { "after", "old" }, RunHistory.Records.Select(r => r.runId).ToArray());
        }

        [TestCase("{\"schemaVersion\":1}")]
        [TestCase("{\"schemaVersion\":1,\"runs\":null}")]
        [TestCase("{\"schemaVersion\":1,\"runs\":{\"a\":1}}")]
        [TestCase("{\"runs\":[]}")]
        [TestCase("not json")]
        public void MalformedFile_IsInvalid_NotSilentlyEmpty(string json)
        {
            File.WriteAllText(historyPath, json);
            var loaded = RunHistoryStore.Load(historyPath);
            Assert.AreEqual(RunHistoryStore.LoadSource.FreshAfterCorrupt, loaded.Source);
            Assert.AreEqual(1, loaded.CorruptPreservedPaths.Count);
        }

        [Test]
        public void FutureSchema_IsNeverWritten_AndRecordStaysInMemory()
        {
            const string future = "{\"schemaVersion\":9,\"runs\":[],\"newField\":true}";
            File.WriteAllText(historyPath, future);
            Assert.AreEqual(RunHistory.AddResult.WriteBlocked, RunHistory.Add(Record("mem")));
            Assert.IsTrue(RunHistory.IsWriteBlocked);
            Assert.AreEqual(RunHistory.NoticeFuture, RunHistory.Notice);
            Assert.AreEqual("mem", RunHistory.Records[0].runId, "결과·목록은 메모리 기록으로 읽을 수 있다");
            Assert.AreEqual(future, File.ReadAllText(historyPath), "미래 버전 원본 그대로");
            Assert.IsFalse(File.Exists(historyPath + ".bak"));
            Assert.IsFalse(RunHistory.SaveNow());
            Assert.AreEqual(future, File.ReadAllText(historyPath));
        }

        [Test]
        public void FutureBackup_WithValidOriginal_BlocksWrites()
        {
            Assert.IsTrue(RunHistoryStore.Save(historyPath, new System.Collections.Generic.List<RunRecord> { Record("keep") }));
            const string futureBak = "{\"schemaVersion\":3,\"runs\":[]}";
            File.WriteAllText(historyPath + ".bak", futureBak);
            var originalBytes = File.ReadAllBytes(historyPath);
            Assert.AreEqual(RunHistory.AddResult.WriteBlocked, RunHistory.Add(Record("mem")));
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(historyPath));
            Assert.AreEqual(futureBak, File.ReadAllText(historyPath + ".bak"));
            LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(RunHistoryStore.Save(historyPath, RunHistory.Records.ToList()), "직접 저장도 미래 .bak이면 거부");
            Assert.AreEqual(futureBak, File.ReadAllText(historyPath + ".bak"));
        }

        [TestCase(RunHistoryStore.SaveStage.WriteTmp)]
        [TestCase(RunHistoryStore.SaveStage.AfterTmpWritten)]
        [TestCase(RunHistoryStore.SaveStage.BeforeReplace)]
        public void IoFailure_KeepsPreviousFile_ReportsFailure_AndRetrySucceeds(RunHistoryStore.SaveStage stage)
        {
            Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("first")));
            var before = File.ReadAllBytes(historyPath);
            RunHistoryStore.FaultHookForTests = s => { if (s == stage) throw new IOException("disk full (test)"); };
            LogAssert.ignoreFailingMessages = true;

            Assert.AreEqual(RunHistory.AddResult.SaveFailed, RunHistory.Add(Record("second")));
            Assert.IsTrue(RunHistory.IsDirty);
            Assert.IsTrue(RunHistory.LastSaveFailed);
            Assert.AreEqual(RunHistory.NoticeSaveFailed, RunHistory.Notice, "실패를 성공으로 표시하지 않는다");
            Assert.AreEqual("second", RunHistory.Records[0].runId, "메모리 기록은 남는다");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(historyPath), "이전 정상 파일 그대로");
            Assert.IsFalse(File.Exists(historyPath + ".tmp"));
            Assert.IsTrue(QuitGuard.HasUnsaved, "종료 확인에도 최근 기록 미저장이 잡힌다");
            StringAssert.Contains("run-history.json", QuitGuard.UnsavedMessage);

            RunHistory.Tick(PermanentProgress.RetryIntervalSeconds - 0.5f);
            Assert.IsTrue(RunHistory.IsDirty, "5초 전에는 재시도하지 않는다");
            RunHistoryStore.FaultHookForTests = null;
            RunHistory.Tick(1f);
            Assert.IsFalse(RunHistory.IsDirty);
            Assert.IsFalse(RunHistory.LastSaveFailed);
            RunHistory.ReloadForTests();
            CollectionAssert.AreEqual(new[] { "second", "first" }, RunHistory.Records.Select(r => r.runId).ToArray());
        }

        [Test]
        public void History_IsSeparateFile_AndNeverTouchesSaveOrSettings()
        {
            PermanentProgress.AddSpecialty(5); // save.json 생성
            Assert.IsTrue(File.Exists(savePath));
            var saveBytes = File.ReadAllBytes(savePath);
            Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("sep")));
            CollectionAssert.AreEqual(saveBytes, File.ReadAllBytes(savePath), "save.json v2 그대로");
            Assert.IsTrue(File.Exists(historyPath));
            Assert.AreEqual(2, PermanentProgress.Current.schemaVersion);
            Assert.AreEqual(5, PermanentProgress.Current.specialtyPoints, "경제 집계 불변");
            Assert.AreEqual(SaveSystem.CurrentVersion, SaveSystem.Load(savePath).schemaVersion);
        }

        [Test]
        public void PathForSave_SeparatesGuestAndTestFiles()
        {
            Assert.AreEqual(Path.Combine("d", "run-history.json"), RunHistory.PathForSave(Path.Combine("d", "save.json")));
            Assert.AreEqual(Path.Combine("d", "guest_session.run-history.json"), RunHistory.PathForSave(SaveSystem.GuestPathFor(Path.Combine("d", "save.json"))));
            Assert.AreEqual(Path.Combine("d", "cct_x.run-history.json"), RunHistory.PathForSave(Path.Combine("d", "cct_x.json")));
        }

        [Test]
        public void GuestSession_WritesSeparateHistory_AndKeepsOriginalHistoryBytes()
        {
            Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("normal")));
            var normalBytes = File.ReadAllBytes(historyPath);
            File.WriteAllText(savePath, "{\"schemaVersion\":7}"); // 새 버전 진행 저장 → 게스트 세션
            PermanentProgress.SetPathForTests(savePath);
            LogAssert.ignoreFailingMessages = true;
            Assert.IsTrue(PermanentProgress.IsFutureSaveProtected);
            Assert.IsTrue(PermanentProgress.AcceptGuestSession());
            Assert.AreEqual(Path.GetFullPath(Path.Combine(dir, "guest_session.run-history.json")), Path.GetFullPath(RunHistory.ActivePath));
            Assert.AreEqual(0, RunHistory.Records.Count, "게스트는 별도 기록");
            Assert.AreEqual(RunHistory.AddResult.Saved, RunHistory.Add(Record("guest")));
            CollectionAssert.AreEqual(normalBytes, File.ReadAllBytes(historyPath), "원본 기록 그대로");
            Assert.IsTrue(File.Exists(Path.Combine(dir, "guest_session.run-history.json")));
        }

        // ── 표시 ─────────────────────────────────────────────

        [Test]
        public void Text_UsesCurrentNames_FallsBackToStoredNames_AndRejectsUnknownEnums()
        {
            var r = Record("t");
            var gin = CardDatabase.LoadAll().First(c => c.id == "dry_gin");
            Assert.AreEqual(gin.displayName, RunRecordText.CardName(r.cards[0]));
            Assert.AreEqual("사라진 카드", RunRecordText.CardName(new RunCardEntry { id = "removed_card", name = "사라진 카드" }));
            Assert.AreEqual(RunRecordText.UnknownCard, RunRecordText.CardName(new RunCardEntry { id = "removed_card" }));
            Assert.AreEqual("옛 조합", RunRecordText.ComboName(new RunNamedEntry { kind = "hidden", id = "hidden_gone", name = "옛 조합" }, r));
            Assert.AreEqual("집중 연소", RunRecordText.ComboName(r.combos[1], r));
            r.job = "7"; r.jobName = "옛 직업";
            Assert.AreEqual("옛 직업", RunRecordText.JobName(r), "숫자 문자열 enum은 받지 않는다");
            r.endKind = "Exploded";
            Assert.AreEqual(RunEndKind.Abandoned, r.EndKind);
            var died = Record("d");
            StringAssert.Contains("라임웨지 · 몸통 접촉", RunRecordText.EndDetail(died));
            var timeUp = Record("u", RunEndKind.TimeUp);
            StringAssert.DoesNotContain("라임웨지", RunRecordText.EndDetail(timeUp), "사망이 아닌 종료에 적 이름을 붙이지 않는다");
            Assert.AreEqual("05:21", RunRecordText.Time(321.9f));
        }

        // ── 피해 출처 ─────────────────────────────────────────────

        [Test]
        public void ZoneDamage_KeepsSourceOfHighestDps_AndOldApiStillWorks()
        {
            var a = new DamageSource(DamageKind.Zone, "A", "장판");
            var b = new DamageSource(DamageKind.Zone, "B", "장판");
            var c = new DamageSource(DamageKind.Zone, "C", "장판");
            PlayerZoneDamage.RequestTick(5f, a);
            PlayerZoneDamage.RequestTick(9f, b);
            PlayerZoneDamage.RequestTick(9f, c); // 같은 dps면 먼저 등록한 것
            PlayerZoneDamage.RequestTick(2f, a);
            Assert.AreEqual(9f, PlayerZoneDamage.ConsumeFrame());
            Assert.AreEqual("B", PlayerZoneDamage.LastConsumedSource.attacker);
            Assert.AreEqual(0f, PlayerZoneDamage.ConsumeFrame(), "한 번만 소비");
            Assert.IsFalse(PlayerZoneDamage.LastConsumedSource.IsKnown);
            PlayerZoneDamage.RequestTick(4f);
            Assert.AreEqual(4f, PlayerZoneDamage.ConsumeFrame(), "기존 호출(출처 없음) 호환");
            Assert.AreEqual(DamageSource.UnknownLabel, PlayerZoneDamage.LastConsumedSource.Label);
        }

        [Test]
        public void DamageSource_Labels()
        {
            Assert.AreEqual("라임웨지 · 몸통 접촉", DamageSource.Contact(EnemyDef.Imp).Label);
            Assert.AreEqual("올리브 바머 · 자폭", DamageSource.Contact(EnemyDef.OliveBomber).Label);
            Assert.AreEqual(DamageSource.UnknownLabel, default(DamageSource).Label);
            Assert.AreEqual("증기 분사", new DamageSource(DamageKind.BossSkill, null, "증기 분사").Label);
        }

        // ── 실제 GameManager 스냅샷 ─────────────────────────────────────────────

        GameObject root;
        GameObject createdCamera;

        GameManager NewGame(Job job = Job.Bartender, WeaponKind weapon = WeaponKind.Rapier)
        {
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            var game = root.AddComponent<GameManager>();
            game.Initialize(job, weapon);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
            return game;
        }

        void DestroyGame()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (createdCamera != null) UnityEngine.Object.DestroyImmediate(createdCamera);
            root = createdCamera = null;
        }

        static CardData Card(string id) => CardDatabase.LoadAll().First(c => c.id == id);

        [Test]
        public void Snapshot_HasExactCardsStacks_AndOnlyCompletedCombos_AndIsNotChangedByLaterPicks()
        {
            var game = NewGame();
            for (int i = 0; i < 5; i++) game.GrantCardForTests(Card("molotov")); // 최대 중첩 → 집중 연소 최종 형태
            game.GrantCardForTests(Card("green_tea"));
            game.GrantCardForTests(Card("green_tea"));
            var record = game.BuildRunRecord(RunEndKind.Abandoned);

            var molotov = record.cards.Single(c => c.id == "molotov");
            Assert.AreEqual(5, molotov.stacks);
            Assert.AreEqual(Card("molotov").maxStacks, molotov.maxStacks);
            Assert.AreEqual(2, record.cards.Single(c => c.id == "green_tea").stacks);
            Assert.AreEqual(2, record.cards.Count, "고르지 않은 카드는 없다");
            Assert.IsTrue(record.combos.Any(c => c.kind == "finalForm" && c.id == "Flame"));
            Assert.IsFalse(record.combos.Any(c => c.kind == "hybrid"), "하이브리드 미해금이면 표시하지 않는다");
            foreach (var hidden in record.combos.Where(c => c.kind == "hidden"))
                Assert.IsTrue(game.Player.Hidden.Has(HiddenSkillDatabase.All.First(d => d.id == hidden.id).skill), "실제로 해금한 히든만");
            Assert.AreEqual("", record.deathCause, "사망이 아니면 원인 없음");

            game.GrantCardForTests(Card("green_tea"));
            game.GrantCardForTests(Card("dry_gin"));
            game.Player.Hidden.ForceForTests(HiddenSkill.IcedCup);
            Assert.AreEqual(2, record.cards.Single(c => c.id == "green_tea").stacks, "나중 선택이 기록을 바꾸지 않는다");
            Assert.AreEqual(2, record.cards.Count);
            Assert.IsFalse(record.combos.Any(c => c.id == HiddenSkillDatabase.Get(HiddenSkill.IcedCup).id));

            var later = game.BuildRunRecord(RunEndKind.Abandoned);
            Assert.AreEqual(3, later.cards.Single(c => c.id == "green_tea").stacks);
            Assert.IsTrue(later.combos.Any(c => c.id == HiddenSkillDatabase.Get(HiddenSkill.IcedCup).id));
        }

        [Test]
        public void Death_ByRealDamagePath_RecordsLethalSource_Once_AndResultHasRecord()
        {
            var game = NewGame();
            game.GrantCardForTests(Card("dry_gin"));
            var src = DamageSource.Projectile(EnemyDef.Imp);
            game.Player.TakeDamage(game.Player.MaxHp * 10f, src);
            Assert.IsTrue(game.Player.IsDead);
            game.ResolveEndOfFrame();

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsNotNull(game.RunRecord);
            Assert.AreEqual(RunEndKind.Died, game.RunRecord.EndKind);
            Assert.AreEqual(src.Label, game.RunRecord.deathCause);
            Assert.AreEqual("Projectile", game.RunRecord.deathCauseKind);
            Assert.AreEqual(RunHistory.AddResult.Saved, game.RunHistoryResult);
            Assert.AreEqual(game.RunId, RunHistory.Records[0].runId);
            Assert.AreEqual(1, RunHistory.Records[0].cards.Count);

            game.PrepareForQuit(); // 결과 뒤 종료·재진입 경로도 다시 기록하지 않는다
            game.GiveUp();
            game.ResolveEndOfFrame();
            Assert.AreEqual(1, RunHistory.Records.Count);
            Assert.AreEqual(1, PermanentProgress.Current.failedRuns, "보상·집계 중복 없음");
            Assert.AreEqual(0, PermanentProgress.Current.abandonedRuns);
        }

        [Test]
        public void TimeUp_And_Clear_And_GiveUp_AreDistinct_WithoutDeathCause()
        {
            var game = NewGame();
            game.Player.TakeDamage(1f, DamageSource.Contact(EnemyDef.Brute)); // 맞은 적이 있어도
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            game.ResolveEndOfFrame();
            Assert.AreEqual(RunEndKind.TimeUp, game.RunRecord.EndKind);
            Assert.AreEqual("", game.RunRecord.deathCause, "시간 초과에 마지막 적 이름을 붙이지 않는다");
            DestroyGame();

            game = NewGame(Job.TeaSommelier, WeaponKind.Bow);
            game.Player.TakeDamage(1f, DamageSource.Contact(EnemyDef.Brute));
            game.OnEnemyKilled(Vector2.zero, EnemyDef.DifficultCustomer);
            game.ResolveEndOfFrame();
            Assert.AreEqual(RunEndKind.Cleared, game.RunRecord.EndKind);
            Assert.AreEqual("", game.RunRecord.deathCause);
            Assert.AreEqual(Job.TeaSommelier.ToString(), game.RunRecord.job);
            Assert.AreEqual(WeaponKind.Bow.ToString(), game.RunRecord.weapon);
            DestroyGame();

            game = NewGame();
            game.GiveUp(); // Pause의 "런 포기"와 같은 진입점
            Assert.AreEqual(RunEndKind.Abandoned, game.RunRecord.EndKind);
            Assert.AreEqual("", game.RunRecord.deathCause);
            CollectionAssert.AreEqual(new[] { RunEndKind.Abandoned, RunEndKind.Cleared, RunEndKind.TimeUp },
                RunHistory.Records.Select(r => r.EndKind).ToArray(), "최신순");
        }

        [Test]
        public void NormalQuitWhilePlaying_RecordsAbandonedOnce()
        {
            var game = NewGame();
            game.PrepareForQuit();
            game.PrepareForQuit();
            Assert.AreEqual(1, RunHistory.Records.Count);
            Assert.AreEqual(RunEndKind.Abandoned, RunHistory.Records[0].EndKind);
            Assert.AreEqual(1, PermanentProgress.Current.abandonedRuns);
        }

        [Test]
        public void HistorySaveFailure_DoesNotBlockEndOrResult()
        {
            var game = NewGame();
            RunHistoryStore.FaultHookForTests = _ => throw new IOException("locked (test)");
            LogAssert.ignoreFailingMessages = true;
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(RunHistory.AddResult.SaveFailed, game.RunHistoryResult);
            var result = UnityEngine.Object.FindObjectsByType<ResultView>(FindObjectsSortMode.None).First(v => v.IsOpen);
            StringAssert.Contains("저장 실패", result.HistoryStatusText, "저장 실패를 성공으로 보이지 않는다");
            Assert.IsTrue(result.BuildDetailButton.interactable, "메모리 기록으로 이번 판 빌드는 볼 수 있다");
            Assert.AreEqual(1, PermanentProgress.Current.failedRuns, "진행 저장·집계는 그대로");
        }

        // ── 무효 피해는 원인 기록을 덮지 않는다 ─────────────────────────────────────────────

        [Test]
        public void InvalidDamage_DoesNotOverwritePreviousSource()
        {
            var game = NewGame();
            var p = game.Player;
            var a = new DamageSource(DamageKind.Contact, "A", "몸통 접촉");
            var b = new DamageSource(DamageKind.Projectile, "B", "원거리 공격");
            p.TakeDamage(5f, a);
            Assert.AreEqual("A", p.LastDamageSource.Value.attacker);
            p.TakeDamage(5f, b); // 0.8초 무적에 막힘
            Assert.AreEqual("A", p.LastDamageSource.Value.attacker, "무적에 막힌 피해");
            p.TakeZoneDamage(0f, b);
            p.TakeZoneDamage(-3f, b);
            Assert.AreEqual("A", p.LastDamageSource.Value.attacker, "0·음수 피해");
            p.TakeZoneDamage(2f, b); // DoT는 단발 무적과 무관하게 들어간다
            Assert.AreEqual("B", p.LastDamageSource.Value.attacker);
        }

        [Test]
        public void HiddenBarrierBlock_DoesNotRecordSource()
        {
            var game = NewGame();
            var p = game.Player;
            p.Hidden.ForceForTests(HiddenSkill.IcedCup);
            p.HiddenRuntime.SetBarrierReadyForTests();
            float hp = p.Hp;
            p.TakeDamage(10f, new DamageSource(DamageKind.Projectile, "B", "원거리 공격"));
            Assert.AreEqual(hp, p.Hp, 0.001f, "보호막이 막았다");
            Assert.IsNull(p.LastDamageSource);
        }

        [Test]
        public void InfiniteLethalDamage_IsRecordedAsUnknown_NotPreviousAttacker()
        {
            var game = NewGame();
            var p = game.Player;
            p.TakeZoneDamage(1f, new DamageSource(DamageKind.Zone, "A", "장판"));
            p.TakeZoneDamage(float.PositiveInfinity, new DamageSource(DamageKind.Zone, "B", "장판"));
            Assert.IsTrue(p.IsDead);
            Assert.AreEqual("A", p.LastDamageSource.Value.attacker, "비유한 피해는 기록을 덮지 않는다");
            Assert.AreEqual(DamageSource.UnknownLabel, p.LethalDamageSource.Value.Label, "이전 공격자를 사망 원인으로 지목하지 않는다");
            game.ResolveEndOfFrame();
            Assert.AreEqual(DamageSource.UnknownLabel, game.RunRecord.deathCause);
        }

        [Test]
        public void NaNDamage_IsNotRecorded()
        {
            var game = NewGame();
            var p = game.Player;
            p.TakeZoneDamage(1f, new DamageSource(DamageKind.Zone, "A", "장판"));
            p.TakeZoneDamage(float.NaN, new DamageSource(DamageKind.Zone, "B", "장판"));
            Assert.AreEqual("A", p.LastDamageSource.Value.attacker);
        }

        [Test]
        public void LethalDoT_RecordsZoneSource()
        {
            var game = NewGame();
            var p = game.Player;
            p.TakeDamage(1f, DamageSource.Contact(EnemyDef.Imp)); // 무적 중
            var zone = DamageSource.Zone(EnemyDef.OliveBomber, "자폭 장판");
            p.TakeZoneDamage(p.MaxHp * 5f, zone);
            Assert.IsTrue(p.IsDead);
            Assert.AreEqual(zone.Label, p.LethalDamageSource.Value.Label);
        }
    }
}
