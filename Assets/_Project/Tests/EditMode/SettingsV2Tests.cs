using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 08 §4/§5 settings.json v2 — 임시 폴더 + 메모리 구형 PlayerPrefs 저장소 + 메모리 화면 어댑터로만 검사한다.
    /// 실제 settings.json/PlayerPrefs/Screen/QualitySettings는 읽거나 바꾸지 않는다.
    /// </summary>
    public abstract class SettingsV2Fixture
    {
        protected string dir;
        protected string settingsPath;
        protected string savePath;
        protected MemoryLegacySettingsStore legacy;
        protected MemoryDisplayAdapter display;
        protected int faultHits;
        float master, music, sfx;

        [SetUp]
        public void BaseSetUp()
        {
            master = AudioBus.MasterVolume;
            music = AudioBus.MusicVolume;
            sfx = AudioBus.SfxVolume;
            dir = Path.Combine(Application.temporaryCachePath, $"cct_settings_v2_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            settingsPath = Path.Combine(dir, "settings.json");
            savePath = Path.Combine(dir, "save.json");
            legacy = new MemoryLegacySettingsStore();
            display = new MemoryDisplayAdapter();
            faultHits = 0;
            SettingsService.FaultHookForTests = null;
            SaveSystem.FaultHookForTests = null;
            SettingsService.SetForTests(settingsPath, legacy, display);
            PermanentProgress.SetPathForTests(savePath);
        }

        [TearDown]
        public void BaseTearDown()
        {
            SettingsService.FaultHookForTests = null;
            SaveSystem.FaultHookForTests = null;
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            AudioBus.MasterVolume = master;
            AudioBus.MusicVolume = music;
            AudioBus.SfxVolume = sfx;
            try { Directory.Delete(dir, true); } catch { /* 임시 폴더 정리 실패는 무시 */ }
        }

        /// <summary>다음 실행 = 메모리 세션을 버리고 같은 경로·저장소·화면으로 다시 로드.</summary>
        protected void Relaunch() => SettingsService.SetForTests(settingsPath, legacy, display);

        protected void FailSettingsAt(SettingsService.SaveStage stage) => SettingsService.FaultHookForTests = s =>
        {
            if (s != stage) return;
            faultHits++;
            throw new IOException("injected " + s);
        };

        protected string Raw() => File.ReadAllText(settingsPath);

        /// <summary>디스크 파일을 별도 세션으로 읽은 값(현재 세션 상태는 보존).</summary>
        protected SettingsData DiskSettings()
        {
            var json = Raw();
            Assert.IsTrue(JsonSyntax.IsCompleteObject(json));
            return JsonUtility.FromJson<SettingsData>(json);
        }

        protected string[] CorruptFiles() => Directory.GetFiles(dir, "settings.json*.corrupt");

        protected void SeedLegacy()
        {
            legacy.Floats[SettingsService.LegacyMasterKey] = 0.35f;
            legacy.Floats[SettingsService.LegacyMusicKey] = 0.556f;
            legacy.Floats[SettingsService.LegacySfxKey] = 1.2f;
            legacy.Ints[SettingsService.LegacyFullscreenKey] = 0;
            legacy.Ints[SettingsService.LegacyVSyncKey] = 0;
            legacy.Ints[SettingsService.LegacySchemeKey] = (int)MovementScheme.Mouse;
            legacy.Ints[SettingsService.LegacyTutorialKey] = 1;
        }
    }

    public class SettingsV2LoadTests : SettingsV2Fixture
    {
        [Test]
        public void NoFileNoLegacy_Defaults_AppliedAtBoot_WithoutWritingAnything()
        {
            var s = SettingsService.Current;
            Assert.AreEqual(SettingsService.LoadSource.Defaults, SettingsService.Source);
            Assert.AreEqual(2, s.schemaVersion);
            Assert.AreEqual((80, 60, 80), (s.masterVolume, s.musicVolume, s.sfxVolume));
            Assert.AreEqual((1920, 1080), (s.width, s.height));
            Assert.AreEqual(SettingsData.WindowBorderless, s.windowMode);
            Assert.IsTrue(s.vsync);
            Assert.AreEqual(SettingsData.SchemeKeyboard, s.movementScheme);
            Assert.AreEqual(50, s.shakePercent);
            Assert.AreEqual(SettingsData.EffectsNormal, s.effectsLevel);
            Assert.IsTrue(s.damageNumbers);
            Assert.IsFalse(s.tutorialSeen);

            SettingsService.ApplyAtBoot();
            Assert.AreEqual(0.8f, AudioBus.MasterVolume, 1e-4f);
            Assert.AreEqual(0.6f, AudioBus.MusicVolume, 1e-4f);
            Assert.AreEqual(0.8f, AudioBus.SfxVolume, 1e-4f);
            Assert.AreEqual((1920, 1080, false), (display.Width, display.Height, display.Windowed));
            Assert.IsTrue(display.VSync);
            Assert.AreEqual(-1, display.TargetFrameRate);
            Assert.IsFalse(File.Exists(settingsPath), "부트 로드는 읽기 전용");
        }

        [Test]
        public void LegacyPlayerPrefs_MigratedOnlyWhenNoFile_KeysKept_FirstChangeWritesFile()
        {
            SeedLegacy();
            var s = SettingsService.Current;
            Assert.AreEqual(SettingsService.LoadSource.Legacy, SettingsService.Source);
            Assert.AreEqual(35, s.masterVolume);
            Assert.AreEqual(56, s.musicVolume, "0.556 → 정수 반올림");
            Assert.AreEqual(100, s.sfxVolume, "범위 밖 1.2 → 100");
            Assert.AreEqual(SettingsData.WindowWindowed, s.windowMode);
            Assert.IsFalse(s.vsync);
            Assert.AreEqual(MovementScheme.Mouse, InputSettings.Scheme);
            Assert.IsTrue(FirstRunTutorial.Seen);

            SettingsService.ApplyAtBoot();
            Assert.IsTrue(display.Windowed);
            Assert.AreEqual(60, display.TargetFrameRate, "VSync 꺼짐 = 60fps 제한");
            Assert.AreEqual(0.35f, AudioBus.MasterVolume, 1e-4f);
            Assert.IsFalse(File.Exists(settingsPath));

            SettingsService.SetVolume(VolumeChannel.Sfx, 70);
            var disk = DiskSettings();
            Assert.AreEqual(35, disk.masterVolume);
            Assert.AreEqual(70, disk.sfxVolume);
            Assert.AreEqual(SettingsData.SchemeMouse, disk.movementScheme);
            Assert.IsTrue(disk.tutorialSeen);
            Assert.AreEqual(7, legacy.Ints.Count + legacy.Floats.Count, "구형 키는 지우지 않는다");

            Relaunch();
            Assert.AreEqual(SettingsService.LoadSource.Original, SettingsService.Source);
            Assert.AreEqual(70, SettingsService.Current.sfxVolume, "파일이 생긴 뒤에는 파일 기준");
        }

        [Test]
        public void ExistingFile_IsAuthoritative_OverLegacy()
        {
            SeedLegacy();
            File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"masterVolume\":10,\"movementScheme\":\"keyboard\",\"tutorialSeen\":false}");
            Assert.AreEqual(SettingsService.LoadSource.Original, SettingsService.Source);
            Assert.AreEqual(10, SettingsService.Current.masterVolume);
            Assert.AreEqual(MovementScheme.Keyboard, InputSettings.Scheme);
            Assert.IsFalse(FirstRunTutorial.Seen);
            Assert.AreEqual(60, SettingsService.Current.musicVolume, "파일에 없는 필드는 PlayerPrefs가 아니라 기본값");
        }

        [Test]
        public void NullMissingRangeNonFiniteBadEnum_AreNormalizedPerField()
        {
            File.WriteAllText(settingsPath,
                "{\"schemaVersion\":2,\"masterVolume\":null,\"musicVolume\":150,\"sfxVolume\":-5,\"width\":100,\"height\":720," +
                "\"windowMode\":\"fullscreenish\",\"vsync\":\"yes\",\"movementScheme\":\"MOUSE\",\"shakePercent\":1e999," +
                "\"effectsLevel\":3,\"damageNumbers\":null,\"tutorialSeen\":true,\"extra\":{\"masterVolume\":1}}");
            var s = SettingsService.Current;
            Assert.AreEqual(SettingsService.LoadSource.Original, SettingsService.Source);
            Assert.AreEqual(80, s.masterVolume, "null → 기본");
            Assert.AreEqual(100, s.musicVolume, "범위 위 → 100");
            Assert.AreEqual(0, s.sfxVolume, "범위 아래 → 0");
            Assert.AreEqual((1920, 1080), (s.width, s.height), "무효 해상도 → 기본 쌍");
            Assert.AreEqual(SettingsData.WindowBorderless, s.windowMode, "잘못된 열거값 → 기본");
            Assert.IsTrue(s.vsync, "불리언 아님 → 기본");
            Assert.AreEqual(SettingsData.SchemeMouse, s.movementScheme, "대소문자 무관 열거값");
            Assert.AreEqual(50, s.shakePercent, "비유한 → 기본");
            Assert.AreEqual(SettingsData.EffectsNormal, s.effectsLevel);
            Assert.IsTrue(s.damageNumbers);
            Assert.IsTrue(s.tutorialSeen);
            Assert.IsTrue(SettingsService.Diagnostics.Count > 0, "정리 내역은 진단로그에");
        }

        [Test]
        public void FractionalVolume_Rounds_MissingSchemaVersion_TreatedAsV2()
        {
            File.WriteAllText(settingsPath, "{\"masterVolume\":55.6,\"effectsLevel\":\"low\",\"windowMode\":\"windowed\",\"width\":1280,\"height\":720}");
            var s = SettingsService.Current;
            Assert.AreEqual(56, s.masterVolume);
            Assert.AreEqual(SettingsData.EffectsLow, s.effectsLevel);
            Assert.AreEqual((1280, 720, true), (s.width, s.height, s.IsWindowed));
        }

        [Test]
        public void SchemaVersionTypeMismatch_IsCorrupt_DefaultsWithNotice_LegacyNotUsed()
        {
            SeedLegacy();
            File.WriteAllText(settingsPath, "{\"schemaVersion\":\"2\",\"masterVolume\":5}");
            Assert.AreEqual(SettingsService.LoadSource.DefaultsAfterCorrupt, SettingsService.Source);
            Assert.AreEqual(80, SettingsService.Current.masterVolume, "손상 파일이 있으면 PlayerPrefs 이전 안 함(파일 없음일 때만)");
            Assert.AreEqual(SettingsService.NoticeCorruptDefaults, SettingsService.LoadNotice);
            Assert.AreEqual(1, CorruptFiles().Length);
        }
    }

    public class SettingsV2FileSafetyTests : SettingsV2Fixture
    {
        [Test]
        public void Save_IsAtomic_BackupKeepsPreviousValid_TmpNeverApplied()
        {
            SettingsService.SetVolume(VolumeChannel.Master, 40);
            Assert.IsFalse(File.Exists(settingsPath + ".bak"), "첫 저장은 백업 없음");
            SettingsService.SetVolume(VolumeChannel.Master, 41);
            Assert.AreEqual(40, JsonUtility.FromJson<SettingsData>(File.ReadAllText(settingsPath + ".bak")).masterVolume);
            Assert.IsFalse(File.Exists(settingsPath + ".tmp"));

            File.WriteAllText(settingsPath + ".tmp", "{\"schemaVersion\":2,\"masterVolume\":99}");
            Relaunch();
            Assert.AreEqual(41, SettingsService.Current.masterVolume, ".tmp는 자동 적용하지 않는다");
        }

        [Test]
        public void TruncatedOriginal_RestoresValidBak_PreservesCorrupt_NextSaveKeepsGoodBak()
        {
            SettingsService.SetVolume(VolumeChannel.Music, 20);
            SettingsService.SetVolume(VolumeChannel.Music, 30); // .bak = 20
            var goodBak = File.ReadAllText(settingsPath + ".bak");
            var full = Raw();
            var truncated = full.Substring(0, full.Length / 2);
            File.WriteAllText(settingsPath, truncated);

            Relaunch();
            Assert.AreEqual(SettingsService.LoadSource.Backup, SettingsService.Source);
            Assert.AreEqual(20, SettingsService.Current.musicVolume);
            Assert.AreEqual(SettingsService.NoticeBackupRestored, SettingsService.LoadNotice);
            Assert.AreEqual(truncated, File.ReadAllText(CorruptFiles().Single()));

            SettingsService.SetVolume(VolumeChannel.Music, 25);
            Assert.AreEqual(goodBak, File.ReadAllText(settingsPath + ".bak"), "손상 원본으로 정상 .bak을 덮지 않는다");
            Assert.AreEqual(25, DiskSettings().musicVolume);
            Assert.AreEqual(1, CorruptFiles().Length, "같은 손상 내용은 한 번만 보존");
        }

        [Test]
        public void CorruptOriginal_NoBak_DefaultsWithNotice()
        {
            File.WriteAllText(settingsPath, "{\"schemaVersion\":2,");
            Assert.AreEqual(SettingsService.LoadSource.DefaultsAfterCorrupt, SettingsService.Source);
            Assert.AreEqual(SettingsService.NoticeCorruptDefaults, SettingsService.LoadNotice);
            Assert.AreEqual(1, CorruptFiles().Length);
        }

        [Test]
        public void FutureSchema_UsesMemoryOnly_NeverOverwritesOriginalOrBak()
        {
            const string future = "{\"schemaVersion\":3,\"masterVolume\":20,\"windowMode\":\"windowed\",\"newField\":[1]}";
            File.WriteAllText(settingsPath, future);
            Assert.IsTrue(SettingsService.IsFutureProtected);
            Assert.AreEqual(SettingsService.NoticeFutureVersion, SettingsService.LoadNotice);
            Assert.AreEqual(20, SettingsService.Current.masterVolume, "알 수 있는 필드는 이번 실행 메모리에서 사용");

            SettingsService.ApplyAtBoot();
            SettingsService.SetVolume(VolumeChannel.Master, 30);
            Assert.AreEqual(0.3f, AudioBus.MasterVolume, 1e-4f, "메모리 설정은 즉시 적용");
            Assert.AreEqual(future, Raw(), "원본을 v2로 덮지 않는다");
            Assert.IsFalse(File.Exists(settingsPath + ".bak"));
            Assert.IsFalse(SettingsService.LastSaveFailed, "보호는 쓰기 실패로 표시하지 않는다");
            Assert.AreEqual(0, CorruptFiles().Length);
        }

        [Test]
        public void FutureBak_WithMissingOriginal_IsProtected()
        {
            const string future = "{\"schemaVersion\":4,\"sfxVolume\":11}";
            File.WriteAllText(settingsPath + ".bak", future);
            Assert.IsTrue(SettingsService.IsFutureProtected);
            SettingsService.SetVolume(VolumeChannel.Sfx, 12);
            Assert.IsFalse(File.Exists(settingsPath));
            Assert.AreEqual(future, File.ReadAllText(settingsPath + ".bak"));
        }

        [Test]
        public void DirectSave_RefusesFutureData_WithoutMutating_AndNeverReplacesFutureFile()
        {
            var data = new SettingsData { schemaVersion = 3, masterVolume = 1 };
            Assert.IsFalse(SettingsService.Save(data, settingsPath));
            Assert.AreEqual(3, data.schemaVersion);
            Assert.IsFalse(File.Exists(settingsPath));

            const string future = "{\"schemaVersion\":3}";
            File.WriteAllText(settingsPath, future);
            Assert.IsFalse(SettingsService.Save(new SettingsData(), settingsPath));
            Assert.AreEqual(future, Raw());
        }

        [Test]
        public void WriteFailure_KeepsMemory_ShortNotice_RetriesEveryFiveUnscaledSeconds()
        {
            FailSettingsAt(SettingsService.SaveStage.AfterTmpWritten);
            SettingsService.SetVolume(VolumeChannel.Master, 33);
            Assert.AreEqual(1, faultHits);
            Assert.IsTrue(SettingsService.LastSaveFailed);
            Assert.IsTrue(SettingsService.IsDirty);
            Assert.AreEqual(33, SettingsService.Current.masterVolume, "메모리 설정·적용 유지");
            Assert.AreEqual(0.33f, AudioBus.MasterVolume, 1e-4f);
            Assert.IsFalse(File.Exists(settingsPath));

            SettingsService.Tick(4.9f);
            Assert.AreEqual(1, faultHits);
            SettingsService.Tick(0.2f);
            Assert.AreEqual(2, faultHits);

            SettingsService.FaultHookForTests = null;
            SettingsService.Tick(5f);
            Assert.IsFalse(SettingsService.LastSaveFailed);
            Assert.IsFalse(SettingsService.IsDirty);
            Assert.AreEqual(33, DiskSettings().masterVolume);
        }
    }

    public class SettingsV2IndependenceTests : SettingsV2Fixture
    {
        [Test]
        public void SettingsFailure_DoesNotMarkProgressFailed_AndViceVersa()
        {
            FailSettingsAt(SettingsService.SaveStage.WriteTmp);
            SettingsService.SetVolume(VolumeChannel.Music, 10);
            Assert.IsTrue(SettingsService.LastSaveFailed);
            PermanentProgress.DiscoverCard("dry_gin");
            Assert.IsFalse(PermanentProgress.LastSaveFailed, "설정 오류가 진행 저장 상태로 번지지 않는다");
            Assert.IsFalse(PermanentProgress.IsDirty);
            CollectionAssert.Contains(SaveSystem.Load(savePath).discoveredCardIds, "dry_gin");
            SettingsService.FaultHookForTests = null;

            SaveSystem.FaultHookForTests = _ => throw new IOException("progress");
            PermanentProgress.DiscoverCard("green_tea");
            Assert.IsTrue(PermanentProgress.LastSaveFailed);
            Assert.IsTrue(SettingsService.RetrySave(), "진행 저장 오류 주입이 설정 저장을 막지 않는다");
            Assert.IsFalse(SettingsService.LastSaveFailed);
            Assert.IsTrue(PermanentProgress.LastSaveFailed, "설정 성공이 진행 실패 표시를 지우지 않는다");

            PermanentProgress.Tick(5f); // 진행 재시도(여전히 실패) — 설정 파일은 다시 쓰지 않는다
            Assert.AreEqual(10, DiskSettings().musicVolume);
        }

        [Test]
        public void ResetToDefaults_ResetsSettingsOnly_ProgressFileUntouched_DisplayNeedsConfirm()
        {
            SettingsService.SetVolume(VolumeChannel.Master, 10);
            SettingsService.SetMovementScheme(MovementScheme.Mouse);
            SettingsService.SetVSync(false);
            SettingsService.SetShakePercent(70);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            SettingsService.SetDamageNumbers(false);
            SettingsService.SetTutorialSeen(true);
            SettingsService.BeginDisplayPreview(1280, 720, true);
            SettingsService.ConfirmDisplayPreview();

            PermanentProgress.AddSpecialty(3);
            PermanentProgress.DiscoverCard("dry_gin");
            PermanentProgress.DiscoverSkill("espresso_shot");
            var saveBytes = File.ReadAllText(savePath);

            SettingsService.ResetToDefaults();
            var s = SettingsService.Current;
            Assert.AreEqual((80, 60, 80), (s.masterVolume, s.musicVolume, s.sfxVolume));
            Assert.AreEqual(SettingsData.SchemeKeyboard, s.movementScheme);
            Assert.IsTrue(s.vsync);
            Assert.AreEqual(50, s.shakePercent);
            Assert.AreEqual(SettingsData.EffectsNormal, s.effectsLevel);
            Assert.IsTrue(s.damageNumbers);
            Assert.IsTrue(s.tutorialSeen, "첫 안내 표시 기록은 사용자 설정이 아니라 유지");
            Assert.AreEqual((1280, 720, true), (s.width, s.height, s.IsWindowed), "해상도는 확인 없이 바꾸지 않는다");
            Assert.AreEqual(saveBytes, File.ReadAllText(savePath), "진행/영구 강화/발견 정보 불변");
            Assert.AreEqual(80, DiskSettings().masterVolume);
        }
    }

    public class SettingsV2DisplayPreviewTests : SettingsV2Fixture
    {
        [Test]
        public void Preview_NotWrittenUntilConfirm_UnrelatedChangesPersist_RevertRestoresCommitted()
        {
            SettingsService.SetVolume(VolumeChannel.Master, 70); // 파일 생성(1920×1080 확정)
            SettingsService.BeginDisplayPreview(1280, 720, false);
            Assert.AreEqual((1280, 720), (display.Width, display.Height), "미리보기는 즉시 화면에 적용");
            Assert.AreEqual(1920, DiskSettings().width, "확인 전에는 settings에 기록하지 않는다");

            SettingsService.SetVolume(VolumeChannel.Music, 15);
            SettingsService.SetMovementScheme(MovementScheme.Mouse);
            var disk = DiskSettings();
            Assert.AreEqual(15, disk.musicVolume, "무관한 음량 변경은 저장");
            Assert.AreEqual(SettingsData.SchemeMouse, disk.movementScheme);
            Assert.AreEqual((1920, 1080), (disk.width, disk.height), "그 저장에도 미리보기 값은 섞이지 않는다");

            SettingsService.RevertDisplayPreview();
            Assert.IsFalse(SettingsService.IsPreviewActive);
            Assert.AreEqual((1920, 1080, false), (display.Width, display.Height, display.Windowed));
            Assert.AreEqual(15, SettingsService.Current.musicVolume, "되돌리기는 음량/입력 변경을 되돌리지 않는다");
            Assert.AreEqual(MovementScheme.Mouse, InputSettings.Scheme);
        }

        [Test]
        public void RepeatedPreview_RollsBackToLastCommitted_NotIntermediate_ConfirmPersists()
        {
            SettingsService.BeginDisplayPreview(1280, 720, false);
            SettingsService.BeginDisplayPreview(1280, 720, true);
            SettingsService.RevertDisplayPreview();
            Assert.AreEqual((1920, 1080, false), (display.Width, display.Height, display.Windowed));

            SettingsService.BeginDisplayPreview(1280, 720, true);
            Assert.IsTrue(SettingsService.ConfirmDisplayPreview());
            var disk = DiskSettings();
            Assert.AreEqual((1280, 720, SettingsData.WindowWindowed), (disk.width, disk.height, disk.windowMode));

            Relaunch();
            SettingsService.ApplyAtBoot();
            Assert.AreEqual((1280, 720, true), (display.Width, display.Height, display.Windowed), "다음 실행에도 유지");
        }

        [Test]
        public void SmallMonitor_AppliesAndShows1280x720_FileKeepsCommittedValue()
        {
            File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"width\":1920,\"height\":1080}");
            display.DisplayWidth = 1600;
            display.DisplayHeight = 900;
            SettingsService.ApplyAtBoot();
            Assert.AreEqual((1280, 720), (display.Width, display.Height));
            var eff = SettingsService.EffectiveDisplay;
            Assert.AreEqual((1280, 720), (eff.width, eff.height), "UI도 실제 적용 크기");
            Assert.AreEqual(1920, SettingsService.Current.width, "확정값은 보존");

            SettingsService.BeginDisplayPreview(1920, 1080, false);
            Assert.AreEqual(1280, display.Width, "미리보기도 모니터 맞춤");
            SettingsService.BeginDisplayPreview(99999, 5, false);
            Assert.AreEqual((1280, 720), (display.Width, display.Height), "무효 크기는 기본→맞춤");
            SettingsService.RevertDisplayPreview();
        }
    }

    /// <summary>SettingsView(실제 위젯)가 SettingsService 경로로 저장/미리보기/재시도하는지와 명목 배치.</summary>
    public class SettingsV2ViewTests : SettingsV2Fixture
    {
        const float W = 1920f, H = 1080f;
        GameObject canvas;
        SettingsView view;

        [SetUp]
        public void CreateView()
        {
            canvas = new GameObject("SettingsCanvas", typeof(RectTransform));
            view = SettingsView.Create(canvas.transform);
            view.Show();
        }

        [TearDown]
        public void DestroyView()
        {
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
        }

        [Test]
        public void VolumeSlider_SavesIntegerPercent_AndAppliesToAudioBus()
        {
            view.SetMasterVolume(0.3f);
            view.SetMusicVolume(0.56f);
            Assert.AreEqual(30, SettingsService.Current.masterVolume);
            Assert.AreEqual(0.3f, AudioBus.MasterVolume, 1e-4f, "기존 런타임 0..1 오디오와 호환");
            Assert.AreEqual(56, DiskSettings().musicVolume);
        }

        [Test]
        public void ResolutionPreview_TimeoutReverts_KeepConfirms_EscCloseReverts()
        {
            SettingsService.SetVolume(VolumeChannel.Master, 50); // 파일 생성
            view.CycleResolution();
            Assert.IsTrue(view.IsResolutionConfirmPending);
            Assert.AreEqual("1280×720", view.ResolutionLabelText);
            Assert.AreEqual(1920, DiskSettings().width);

            view.TickConfirm(9.9f);
            Assert.IsTrue(view.IsResolutionConfirmPending);
            view.TickConfirm(0.2f);
            Assert.IsFalse(view.IsResolutionConfirmPending, "10초 확인 없으면 되돌림");
            Assert.AreEqual((1920, 1080), (display.Width, display.Height));
            Assert.AreEqual("1920×1080", view.ResolutionLabelText);

            view.CycleResolution();
            view.ConfirmResolution();
            Assert.AreEqual(1280, DiskSettings().width, "유지 클릭 후 확정 저장");

            view.ToggleFullscreen();
            Assert.IsTrue(display.Windowed);
            view.Hide(); // Esc/닫기와 같은 경로
            Assert.IsFalse(display.Windowed, "닫으면 미확인 표시모드 복원");
            Assert.AreEqual(SettingsData.WindowBorderless, DiskSettings().windowMode);
        }

        [Test]
        public void VSyncOff_Limits60fps_AndPersists()
        {
            view.ToggleVSync();
            Assert.IsFalse(SettingsService.Current.vsync);
            Assert.AreEqual(60, display.TargetFrameRate);
            StringAssert.Contains("60fps", view.VSyncLabelText);
            Assert.IsFalse(DiskSettings().vsync);
        }

        [Test]
        public void SaveFailure_ShowsShortNoticeAndRetry_RetryClears()
        {
            FailSettingsAt(SettingsService.SaveStage.WriteTmp);
            view.SetSfxVolume(0.2f);
            Assert.IsTrue(view.RetryButtonVisible);
            Assert.AreEqual(SettingsService.NoticeSaveFailed, view.NoticeText);
            StringAssert.DoesNotContain("Exception", view.NoticeText);

            SettingsService.FaultHookForTests = null;
            view.RetrySave();
            Assert.IsFalse(view.RetryButtonVisible);
            Assert.AreEqual(20, DiskSettings().sfxVolume);
        }

        [Test]
        public void ResetButton_RestoresSettings_AndPreviewsDefaultDisplayForConfirm()
        {
            SettingsService.SetVolume(VolumeChannel.Master, 5);
            SettingsService.BeginDisplayPreview(1280, 720, true);
            SettingsService.ConfirmDisplayPreview();

            view.ResetToDefaults();
            Assert.AreEqual(80, SettingsService.Current.masterVolume);
            Assert.IsTrue(view.IsResolutionConfirmPending, "기본 화면값은 확인 미리보기");
            Assert.AreEqual(1280, DiskSettings().width, "확인 전에는 기존 확정 해상도 유지");
            view.ConfirmResolution();
            Assert.AreEqual((1920, SettingsData.WindowBorderless), (DiskSettings().width, DiskSettings().windowMode));
        }

        static Rect Nominal(RectTransform rt)
        {
            var origin = new Vector2(rt.anchorMin.x * W, rt.anchorMin.y * H) + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
            return new Rect(origin, rt.sizeDelta);
        }

        [Test]
        public void NewButtonsAndNotice_DoNotOverlapRows_AndStayInSafeArea()
        {
            FailSettingsAt(SettingsService.SaveStage.WriteTmp);
            view.SetSfxVolume(0.4f); // 다시 시도 버튼까지 보이는 상태
            var root = view.transform;
            var items = root.Cast<RectTransform>()
                .Where(t => t.gameObject.activeSelf && t.anchorMin == t.anchorMax && t.name != "ResolutionConfirm").ToList();
            foreach (var target in new[] { "Notice", "ResetDefaults", "CloseButton", "RetrySettingsSave" })
            {
                var t = (RectTransform)root.Find(target);
                Assert.IsTrue(t.gameObject.activeSelf, target);
                var r = Nominal(t);
                Assert.IsTrue(r.xMin >= 48f && r.yMin >= 48f && r.xMax <= W - 48f && r.yMax <= H - 48f, $"{target} 안전 여백 밖 {r}");
                foreach (var o in items)
                    if (o != t) Assert.IsFalse(r.Overlaps(Nominal(o)), $"{target} ↔ {o.name} 겹침");
            }
        }
    }

    /// <summary>도감의 커피 15노드 discoveredSkillIds 최초 획득 표시.</summary>
    public class CodexCoffeeNodeTests : SettingsV2Fixture
    {
        GameObject canvas;
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyUi()
        {
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void CoffeeNodes_ShowFirstAcquired_DistinctFromCardUndiscoveredAndPermanentLocked_UnknownIdsIgnoredButKept()
        {
            var skills = SkillDatabase.LoadAll();
            created.AddRange(skills.Where(s => !UnityEditor.AssetDatabase.Contains(s)));
            var ordered = skills.OrderBy(s => s.branch).ThenBy(s => s.tier).ToList();
            string acquired = ordered[0].id, notAcquired = ordered[1].id;
            File.WriteAllText(savePath, "{\"schemaVersion\":2,\"discoveredSkillIds\":[\"" + acquired + "\",\"unknown_node\"]," +
                                        "\"unlockedPermanentSkillIds\":[\"perfect_crema\",\"mystery_perm\"]}");
            PermanentProgress.SetPathForTests(savePath);

            canvas = new GameObject("CodexCanvas", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();

            Assert.AreEqual(15, codex.CoffeeEntryCount);
            var a = codex.GetCoffeeText(acquired);
            var n = codex.GetCoffeeText(notAcquired);
            StringAssert.Contains(CardCodexView.CoffeeAcquiredLabel, a.desc);
            StringAssert.Contains(CardCodexView.CoffeeNotAcquiredLabel, n.desc);
            StringAssert.Contains(ordered[1].displayName, n.name, "커피는 미획득이어도 이름/효과 표시(카드 ??? 아님)");
            StringAssert.DoesNotContain("???", n.name);
            StringAssert.DoesNotContain("미해금", n.desc, "영구 '미해금' 문구와 구분");
            StringAssert.StartsWith("선행:", n.prereq);
            StringAssert.Contains("커피 노드 최초 획득: 1 / 15", codex.CountText, "unknown id는 집계 제외");

            StringAssert.Contains("비활성", codex.GetPermanentText("perfect_crema"), "선행 빠진 옛 노드는 비활성");
            Assert.AreEqual("???", codex.GetCardText("dry_gin").name, "카드 미발견은 ???");

            CollectionAssert.Contains(PermanentProgress.Current.discoveredSkillIds, "unknown_node", "원본 id 보존");
            CollectionAssert.Contains(PermanentProgress.Current.unlockedPermanentSkillIds, "mystery_perm");
        }

        [Test]
        public void CoffeeSection_DoesNotOverlapHybridOrOtherColumns()
        {
            canvas = new GameObject("CodexCanvas", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            var content = codex.transform.Find("ScrollArea/Viewport/Content");
            Assert.IsNotNull(content);
            float cw = CardCodexView.ViewportWidth; // 2026-10-05: 08:49 안전 여백 안 1824로 확장(열 440 < 간격 452)
            Rect Nom(RectTransform rt)
            {
                var origin = new Vector2(rt.anchorMin.x * cw + rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x,
                    rt.anchoredPosition.y - rt.pivot.y * rt.sizeDelta.y);
                return new Rect(origin, rt.sizeDelta);
            }
            var all = content.Cast<RectTransform>().ToList();
            var coffee = all.Where(t => t.name.StartsWith("CoffeeRow_") || t.name == "Header_커피 노드").ToList();
            Assert.AreEqual(16, coffee.Count);
            // 같은 열(하이브리드)과 이웃 열(차·영구 강화) 모두와 겹치지 않는지 — 열 폭 440 < 간격 452.
            foreach (var c in coffee)
                foreach (var o in all)
                    if (!coffee.Contains(o)) Assert.IsFalse(Nom(c).Overlaps(Nom(o)), $"{c.name} ↔ {o.name}");
            foreach (var o in all)
            {
                var r = Nom(o);
                Assert.IsTrue(r.xMin >= 0f && r.xMax <= cw, $"{o.name} 뷰포트 가로 밖 {r}");
            }
            for (int i = 0; i < coffee.Count; i++)
                for (int j = i + 1; j < coffee.Count; j++)
                    Assert.IsFalse(Nom(coffee[i]).Overlaps(Nom(coffee[j])), $"{coffee[i].name} ↔ {coffee[j].name}");
            float contentHeight = ((RectTransform)content).sizeDelta.y;
            foreach (var c in coffee) Assert.IsTrue(-Nom(c).yMin <= contentHeight + 0.01f, $"{c.name} 스크롤 영역 밖");
        }
    }

    /// <summary>r4 QA 2026-10-05: 저장된 1280×720 창 모드로 재실행했는데 창 모드만 적용되고 1920×1080으로 남음 — 부트 검증·재적용.</summary>
    public class SettingsV2BootDisplayVerifyTests : SettingsV2Fixture
    {
        [Test]
        public void SavedWindowed720_IgnoredOnFirstApply_IsReappliedByVerify_ThenStable()
        {
            File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"masterVolume\":37,\"musicVolume\":60,\"sfxVolume\":80," +
                "\"width\":1280,\"height\":720,\"windowMode\":\"windowed\",\"vsync\":true,\"movementScheme\":\"mouse\"," +
                "\"shakePercent\":50,\"effectsLevel\":\"normal\",\"damageNumbers\":true,\"tutorialSeen\":true}");
            Relaunch();
            display.IgnoreNextSize = true; // 첫 SetResolution이 크기를 놓친 실제 증상
            SettingsService.ApplyAtBoot();
            Assert.IsTrue(display.Windowed);
            Assert.AreNotEqual(1280, display.Width, "전제: 첫 적용 후 크기가 남지 않음");

            Assert.IsFalse(SettingsService.VerifyCommittedDisplay(1), "불일치 감지 → 재적용");
            Assert.AreEqual((1280, 720, true), (display.Width, display.Height, display.Windowed));
            Assert.IsTrue(SettingsService.VerifyCommittedDisplay(2), "재적용 후 일치");
            Assert.AreEqual(2, display.ApplyCount, "일치하면 더 적용하지 않는다");
            Assert.IsFalse(File.Exists(settingsPath + ".tmp"));
            Assert.AreEqual(1280, DiskSettings().width, "검증은 저장 파일을 바꾸지 않는다");
        }
    }
}
