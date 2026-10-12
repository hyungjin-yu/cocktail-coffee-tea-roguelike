using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CCT
{
    public enum VolumeChannel { Master, Music, Sfx }

    /// <summary>
    /// 08 §4/§5 settings.json 스키마 v2. 진행(save.json)과 완전히 별개의 파일·상태다. 열거형은 문자열로 저장해
    /// 잘못된 값을 판별할 수 있게 한다. 화면 흔들림/효과 강도/피해 숫자는 스키마·저장만 있고 아직 게임 효과가 없다.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        public const string WindowBorderless = "borderless";
        public const string WindowWindowed = "windowed";
        public const string SchemeKeyboard = "keyboard";
        public const string SchemeMouse = "mouse";
        public const string EffectsNormal = "normal";
        public const string EffectsLow = "low";

        public int schemaVersion = SettingsService.CurrentVersion;
        public int masterVolume = 80;
        public int musicVolume = 60;
        public int sfxVolume = 80;
        public int width = 1920;
        public int height = 1080;
        public string windowMode = WindowBorderless;
        public bool vsync = true;
        public string movementScheme = SchemeKeyboard;
        public int shakePercent = 50;
        public string effectsLevel = EffectsNormal;
        public bool damageNumbers = true;
        public bool tutorialSeen;

        public bool IsWindowed => windowMode == WindowWindowed;
        public MovementScheme Scheme => movementScheme == SchemeMouse ? MovementScheme.Mouse : MovementScheme.Keyboard;

        public SettingsData Clone() => (SettingsData)MemberwiseClone();
    }

    /// <summary>예전 PlayerPrefs 설정 읽기(이전 전용 — 쓰거나 지우지 않는다). 테스트는 메모리 구현을 주입한다.</summary>
    public interface ILegacySettingsStore
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        float GetFloat(string key, float fallback);
    }

    public sealed class PlayerPrefsLegacySettingsStore : ILegacySettingsStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
    }

    /// <summary>테스트/도구용 메모리 저장소 — 실제 PlayerPrefs를 건드리지 않는다.</summary>
    public sealed class MemoryLegacySettingsStore : ILegacySettingsStore
    {
        public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
        public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
        public bool HasKey(string key) => Ints.ContainsKey(key) || Floats.ContainsKey(key);
        public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
        public float GetFloat(string key, float fallback) => Floats.TryGetValue(key, out var v) ? v : fallback;
    }

    /// <summary>해상도/표시모드/VSync를 실제로 적용하는 경로. 테스트는 메모리 구현으로 실제 창을 건드리지 않는다.</summary>
    public interface IDisplayAdapter
    {
        int DisplayWidth { get; }
        int DisplayHeight { get; }
        /// <summary>지금 실제 창/렌더 크기와 창 모드 여부(부트 적용 검증용).</summary>
        int CurrentWidth { get; }
        int CurrentHeight { get; }
        bool CurrentWindowed { get; }
        void Apply(int width, int height, bool windowed);
        void ApplyVSync(bool on);
    }

    public sealed class UnityDisplayAdapter : IDisplayAdapter
    {
        public int DisplayWidth
        {
            get
            {
                int w = Screen.mainWindowDisplayInfo.width;
                return w > 0 ? w : Screen.currentResolution.width;
            }
        }

        public int DisplayHeight
        {
            get
            {
                int h = Screen.mainWindowDisplayInfo.height;
                return h > 0 ? h : Screen.currentResolution.height;
            }
        }

        public int CurrentWidth => Screen.width;
        public int CurrentHeight => Screen.height;
        public bool CurrentWindowed => Screen.fullScreenMode == FullScreenMode.Windowed;

        public void Apply(int width, int height, bool windowed) =>
            Screen.SetResolution(width, height, windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);

        /// <summary>08:120 켜짐이면 디스플레이 동기화, 꺼짐이면 60fps 제한.</summary>
        public void ApplyVSync(bool on)
        {
            QualitySettings.vSyncCount = on ? 1 : 0;
            Application.targetFrameRate = on ? -1 : 60;
        }
    }

    public sealed class MemoryDisplayAdapter : IDisplayAdapter
    {
        public int DisplayWidth { get; set; } = 2560;
        public int DisplayHeight { get; set; } = 1440;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool Windowed { get; private set; }
        public bool VSync { get; private set; } = true;
        public int TargetFrameRate { get; private set; } = -1;
        public int ApplyCount { get; private set; }
        /// <summary>테스트: 실제 창이 요청과 다르게 남은 상황(r4 QA: 창 모드는 됐지만 1920×1080 유지)을 흉내 낸다.</summary>
        public bool IgnoreNextSize { get; set; }
        public int CurrentWidth => Width;
        public int CurrentHeight => Height;
        public bool CurrentWindowed => Windowed;

        public void Apply(int width, int height, bool windowed)
        {
            ApplyCount++;
            Windowed = windowed;
            if (IgnoreNextSize)
            {
                IgnoreNextSize = false;
                if (Width == 0) { Width = DisplayWidth; Height = DisplayHeight; }
                return;
            }
            Width = width;
            Height = height;
        }

        public void ApplyVSync(bool on)
        {
            VSync = on;
            TargetFrameRate = on ? -1 : 60;
        }
    }

    /// <summary>
    /// 설정 단일 진입점(08 §4/§5): settings.json 로드·정리·이전·원자 저장·재시도와 오디오/입력/튜토리얼/화면 적용.
    /// - 부트에서 UI를 열기 전에 ApplyAtBoot. 로드는 읽기 전용(파일이 없을 때 PlayerPrefs 이전값은 메모리에만 쓰고,
    ///   사용자가 설정을 바꿀 때 처음 기록한다 — 구형 키는 지우지 않는다).
    /// - JSON이 있으면 그것이 기준. null/누락/범위/비유한/잘못된 열거값은 필드별 기본값·범위로 정리.
    /// - settings.json.tmp/.bak, 완전 JSON 검증, 정상 원본만 백업, 손상 사본 .corrupt, .tmp 무시, 미래 schemaVersion은
    ///   원본을 덮지 않고 이번 실행 메모리에서만 사용.
    /// - 해상도/표시모드는 미리보기(10초 확인) 동안 파일에 쓰지 않고, "유지"로 확정할 때 저장한다.
    /// - 진행 저장(PermanentProgress)과 dirty/오류/오류 주입 상태를 공유하지 않는다.
    /// </summary>
    public static class SettingsService
    {
        public const int CurrentVersion = 2;
        public const float RetryIntervalSeconds = 5f;

        public const string NoticeBackupRestored = "설정 파일을 읽지 못해 백업에서 복구했습니다.";
        public const string NoticeCorruptDefaults = "설정 파일을 읽지 못해 기본 설정으로 시작합니다. 기존 파일은 보관했습니다.";
        public const string NoticeFutureVersion = "새 버전 설정 파일입니다. 기존 파일을 보존하고 이번 실행에서만 설정을 적용합니다.";
        public const string NoticeSaveFailed = "설정을 저장하지 못했습니다. 다시 시도";

        public const string LegacyMasterKey = "cct_vol_master";
        public const string LegacyMusicKey = "cct_vol_music";
        public const string LegacySfxKey = "cct_vol_sfx";
        public const string LegacyFullscreenKey = "cct_fullscreen";
        public const string LegacyVSyncKey = "cct_vsync";
        public const string LegacySchemeKey = "cct_movement_scheme";
        public const string LegacyTutorialKey = "cct_tutorial_seen";

        public enum LoadSource { Defaults, Legacy, Original, Backup, DefaultsAfterCorrupt, FutureMemoryOnly }
        public enum SaveStage { WriteTmp, AfterTmpWritten, BeforeReplace }
        enum FileState { Missing, Valid, Invalid, Future }

        struct ReadResult
        {
            public FileState State;
            public SettingsData Data;
            public int Version;
        }

        /// <summary>테스트 전용 오류 주입(진행 저장의 SaveSystem.FaultHookForTests와 별개).</summary>
        public static Action<SaveStage> FaultHookForTests;

        static SettingsData current;
        static string overridePath;
        static ILegacySettingsStore legacy;
        static IDisplayAdapter display;
        static bool futureProtected;
        static bool dirty;
        static bool lastSaveFailed;
        static float retryTimer;
        static string loadNotice;
        static LoadSource source;
        static readonly List<string> diagnostics = new List<string>();
        static bool previewActive;
        static int previewWidth, previewHeight;
        static bool previewWindowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            overridePath = null;
            legacy = null;
            display = null;
            ResetSession();
        }

        static void ResetSession()
        {
            current = null;
            futureProtected = false;
            dirty = false;
            lastSaveFailed = false;
            retryTimer = 0f;
            loadNotice = null;
            source = LoadSource.Defaults;
            diagnostics.Clear();
            previewActive = false;
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "settings.json");
        public static string ActivePath => string.IsNullOrEmpty(overridePath) ? DefaultPath : overridePath;
        static ILegacySettingsStore Legacy => legacy ??= new PlayerPrefsLegacySettingsStore();
        public static IDisplayAdapter Display => display ??= new UnityDisplayAdapter();

        /// <summary>현재 확정 설정(미리보기 중인 해상도/표시모드는 포함하지 않음). 직접 수정하지 말고 Set* 사용.</summary>
        public static SettingsData Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        public static LoadSource Source { get { EnsureLoaded(); return source; } }
        public static string LoadNotice { get { EnsureLoaded(); return loadNotice; } }
        public static IReadOnlyList<string> Diagnostics { get { EnsureLoaded(); return diagnostics; } }
        public static bool IsFutureProtected { get { EnsureLoaded(); return futureProtected; } }
        public static bool LastSaveFailed => lastSaveFailed;
        public static bool IsDirty => dirty;

        static void EnsureLoaded()
        {
            if (current != null) return;
            current = Load(ActivePath);
            Debug.Log($"[Settings] settings.json 경로: {ActivePath} (source={source})");
            foreach (var line in diagnostics) Debug.Log($"[Settings] {line}");
        }

        // ── 적용 ─────────────────────────────────────────────

        /// <summary>부트: UI를 열기 전에 오디오/화면/VSync를 확정 설정으로 맞춘다(입력·튜토리얼은 Current를 직접 읽음).</summary>
        public static void ApplyAtBoot()
        {
            EnsureLoaded();
            ApplyAudio();
            ApplyCommittedDisplay();
            Display.ApplyVSync(current.vsync);
        }

        static void ApplyAudio()
        {
            AudioBus.MasterVolume = current.masterVolume / 100f;
            AudioBus.MusicVolume = current.musicVolume / 100f;
            AudioBus.SfxVolume = current.sfxVolume / 100f;
        }

        /// <summary>08:118 기본 1920×1080, 모니터보다 크면 1280×720. 저장값이 모니터보다 크면 적용만 1280×720(파일 불변).</summary>
        static void ApplyCommittedDisplay()
        {
            var size = FitToDisplay(current.width, current.height);
            Display.Apply(size.x, size.y, current.IsWindowed);
            // QA 2026-10-05(r4 압축 해제 재실행): 저장된 1280×720 창 모드가 첫 프레임 SetResolution 뒤에도 창 모드만 바뀌고
            // 1920×1080으로 남았다. 실제 창 상태를 몇 프레임 뒤 확인해 다르면 확정값을 다시 적용한다(최대 3회, 로그 남김).
            if (Display is UnityDisplayAdapter && Application.isPlaying && !Application.isBatchMode)
                DisplayBootVerifier.Begin();
        }

        /// <summary>부트 직후 검증: 미리보기 중이 아니고 실제 창이 확정 해상도/창 모드와 다르면 다시 적용하고 false.
        /// 일치하면 true. 결과는 [Settings] 로그로 남긴다(GUI QA가 player.log로 확인).</summary>
        public static bool VerifyCommittedDisplay(int attempt)
        {
            EnsureLoaded();
            if (previewActive) return true; // 사용자가 설정에서 10초 미리보기 중이면 건드리지 않는다
            var size = FitToDisplay(current.width, current.height);
            bool windowed = current.IsWindowed;
            var d = Display;
            bool ok = d.CurrentWidth == size.x && d.CurrentHeight == size.y && d.CurrentWindowed == windowed;
            Debug.Log($"[Settings] display check {attempt}: committed {size.x}x{size.y} {(windowed ? "windowed" : "borderless")}, " +
                      $"actual {d.CurrentWidth}x{d.CurrentHeight} {(d.CurrentWindowed ? "windowed" : "fullscreen")} -> {(ok ? "ok" : "reapply")}");
            if (!ok) d.Apply(size.x, size.y, windowed);
            return ok;
        }

        public static Vector2Int FitToDisplay(int width, int height)
        {
            int dw = Display.DisplayWidth, dh = Display.DisplayHeight;
            if (dw > 0 && dh > 0 && (width > dw || height > dh)) return new Vector2Int(1280, 720);
            return new Vector2Int(width, height);
        }

        public static Vector2Int DefaultResolution => FitToDisplay(1920, 1080);

        // ── 즉시 저장 설정 ─────────────────────────────────────────────

        public static int GetVolume(VolumeChannel channel)
        {
            var c = Current;
            return channel == VolumeChannel.Master ? c.masterVolume : channel == VolumeChannel.Music ? c.musicVolume : c.sfxVolume;
        }

        /// <summary>08:117 0..100 정수, 즉시 미리듣기·저장.</summary>
        public static void SetVolume(VolumeChannel channel, int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (GetVolume(channel) == percent) return;
            if (channel == VolumeChannel.Master) current.masterVolume = percent;
            else if (channel == VolumeChannel.Music) current.musicVolume = percent;
            else current.sfxVolume = percent;
            ApplyAudio();
            Persist();
        }

        public static void SetMovementScheme(MovementScheme scheme)
        {
            var value = scheme == MovementScheme.Mouse ? SettingsData.SchemeMouse : SettingsData.SchemeKeyboard;
            if (Current.movementScheme == value) return;
            current.movementScheme = value;
            Persist();
        }

        public static void SetVSync(bool on)
        {
            if (Current.vsync == on) return;
            current.vsync = on;
            Display.ApplyVSync(on);
            Persist();
        }

        public static void SetTutorialSeen(bool seen)
        {
            if (Current.tutorialSeen == seen) return;
            current.tutorialSeen = seen;
            Persist();
        }

        /// <summary>스키마·저장만(런타임 효과 미구현 — 설정 화면에 노출하지 않음).</summary>
        public static void SetShakePercent(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (Current.shakePercent == percent) return;
            current.shakePercent = percent;
            Persist();
        }

        public static void SetEffectsLevel(string level)
        {
            if (level != SettingsData.EffectsNormal && level != SettingsData.EffectsLow) return;
            if (Current.effectsLevel == level) return;
            current.effectsLevel = level;
            Persist();
        }

        public static void SetDamageNumbers(bool on)
        {
            if (Current.damageNumbers == on) return;
            current.damageNumbers = on;
            Persist();
        }

        /// <summary>08:127 "기본값 복원은 설정만 초기화" — 진행 save는 건드리지 않는다. 해상도/표시모드는 확인 없이 바꾸지 않으므로
        /// 여기서는 바꾸지 않는다(설정 화면이 기본 화면값을 미리보기로 띄워 확인받는다). tutorialSeen은 사용자 선택값이 아니라
        /// 첫 안내 표시 기록이라 유지한다.</summary>
        public static void ResetToDefaults()
        {
            EnsureLoaded();
            var d = new SettingsData();
            current.masterVolume = d.masterVolume;
            current.musicVolume = d.musicVolume;
            current.sfxVolume = d.sfxVolume;
            current.vsync = d.vsync;
            current.movementScheme = d.movementScheme;
            current.shakePercent = d.shakePercent;
            current.effectsLevel = d.effectsLevel;
            current.damageNumbers = d.damageNumbers;
            ApplyAudio();
            Display.ApplyVSync(current.vsync);
            Persist();
        }

        // ── 해상도/표시모드 미리보기 ─────────────────────────────────────────────

        public static bool IsPreviewActive => previewActive;

        /// <summary>지금 화면에 실제 적용된(미리보기 포함) 해상도/표시모드. 확정값이 모니터보다 크면 실제 적용 크기(1280×720)를
        /// 돌려준다 — 파일의 확정값은 그대로 보존된다.</summary>
        public static (int width, int height, bool windowed) EffectiveDisplay
        {
            get
            {
                if (previewActive) return (previewWidth, previewHeight, previewWindowed);
                var size = FitToDisplay(Current.width, Current.height);
                return (size.x, size.y, current.IsWindowed);
            }
        }

        /// <summary>08:118 적용 후 10초 확인. 확인 전에는 settings에 기록하지 않는다. 미리보기 중 다시 미리보기해도
        /// 되돌릴 기준은 마지막 확정값 그대로다.</summary>
        public static void BeginDisplayPreview(int width, int height, bool windowed)
        {
            EnsureLoaded();
            // 저장 스키마와 같은 유효 범위 + 모니터 맞춤을 적용한 크기만 어댑터에 준다(확정 시 그 값이 저장됨).
            if (width < 640 || width > 7680 || height < 360 || height > 4320) { width = 1920; height = 1080; }
            var fitted = FitToDisplay(width, height);
            width = fitted.x;
            height = fitted.y;
            previewActive = true;
            previewWidth = width;
            previewHeight = height;
            previewWindowed = windowed;
            Display.Apply(width, height, windowed);
        }

        /// <summary>"유지" — 미리보기 값을 확정하고 저장한다.</summary>
        public static bool ConfirmDisplayPreview()
        {
            if (!previewActive) return false;
            previewActive = false;
            current.width = previewWidth;
            current.height = previewHeight;
            current.windowMode = previewWindowed ? SettingsData.WindowWindowed : SettingsData.WindowBorderless;
            Persist();
            return true;
        }

        /// <summary>타임아웃/되돌리기/Esc/닫기 — 직전 확정값으로 화면을 되돌린다(파일은 원래 확정값 그대로).</summary>
        public static void RevertDisplayPreview()
        {
            if (!previewActive) return;
            previewActive = false;
            ApplyCommittedDisplay();
        }

        // ── 저장/재시도 ─────────────────────────────────────────────

        static bool Persist()
        {
            if (futureProtected)
            {
                dirty = false; // 미래 버전 파일은 덮지 않는다 — 이번 실행 메모리에서만 사용(실패로 표시하지 않음)
                return false;
            }
            bool ok = Save(current, ActivePath);
            dirty = !ok;
            lastSaveFailed = !ok;
            retryTimer = 0f;
            if (!ok) SaveRetryRunner.EnsureRunning();
            return ok;
        }

        public static bool RetrySave()
        {
            EnsureLoaded();
            return Persist();
        }

        /// <summary>08:176과 같은 규칙 — 실패 상태면 실제 시간 5초 간격으로 재시도(SaveRetryRunner가 호출).</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (current == null || !dirty || futureProtected)
            {
                retryTimer = 0f;
                return;
            }
            retryTimer += Mathf.Max(0f, unscaledDeltaTime);
            if (retryTimer >= RetryIntervalSeconds) Persist();
        }

        /// <summary>테스트 전용 — 경로/구형 저장소/화면 어댑터를 바꾸고 세션을 새 실행처럼 초기화한다. null이면 실제 기본값.</summary>
        public static void SetForTests(string path, ILegacySettingsStore legacyStore, IDisplayAdapter displayAdapter)
        {
            overridePath = path;
            legacy = legacyStore;
            display = displayAdapter;
            ResetSession();
        }

        // ── 로드 ─────────────────────────────────────────────

        static SettingsData Load(string path)
        {
            diagnostics.Clear();
            loadNotice = null;
            futureProtected = false;

            var original = Read(path);
            switch (original.State)
            {
                case FileState.Valid:
                    source = LoadSource.Original;
                    return original.Data;
                case FileState.Future:
                    return FutureMemoryOnly(original);
            }

            if (original.State == FileState.Invalid) PreserveCorrupt(path);
            var backup = Read(path + ".bak");
            if (backup.State == FileState.Valid)
            {
                source = LoadSource.Backup;
                loadNotice = NoticeBackupRestored;
                return backup.Data;
            }
            if (backup.State == FileState.Future) return FutureMemoryOnly(backup);
            if (backup.State == FileState.Invalid) PreserveCorrupt(path + ".bak");

            if (original.State == FileState.Missing && backup.State == FileState.Missing)
            {
                var migrated = TryMigrateLegacy();
                if (migrated != null)
                {
                    source = LoadSource.Legacy;
                    return migrated;
                }
                source = LoadSource.Defaults;
                return new SettingsData();
            }

            source = LoadSource.DefaultsAfterCorrupt;
            loadNotice = NoticeCorruptDefaults;
            return new SettingsData();
        }

        static SettingsData FutureMemoryOnly(ReadResult r)
        {
            futureProtected = true;
            source = LoadSource.FutureMemoryOnly;
            loadNotice = NoticeFutureVersion;
            diagnostics.Add($"settings: schemaVersion {r.Version} 파일 보존, 이번 실행 메모리 전용");
            return r.Data;
        }

        /// <summary>파일이 전혀 없을 때만 구형 PlayerPrefs 값을 가져온다(구형 키는 삭제하지 않음). 키가 하나도 없으면 null.</summary>
        static SettingsData TryMigrateLegacy()
        {
            var store = Legacy;
            string[] keys = { LegacyMasterKey, LegacyMusicKey, LegacySfxKey, LegacyFullscreenKey, LegacyVSyncKey, LegacySchemeKey, LegacyTutorialKey };
            bool any = false;
            foreach (var k in keys) any |= store.HasKey(k);
            if (!any) return null;

            var d = new SettingsData();
            d.masterVolume = LegacyPercent(store, LegacyMasterKey, d.masterVolume);
            d.musicVolume = LegacyPercent(store, LegacyMusicKey, d.musicVolume);
            d.sfxVolume = LegacyPercent(store, LegacySfxKey, d.sfxVolume);
            if (store.HasKey(LegacyFullscreenKey))
                d.windowMode = store.GetInt(LegacyFullscreenKey, 1) == 0 ? SettingsData.WindowWindowed : SettingsData.WindowBorderless;
            if (store.HasKey(LegacyVSyncKey)) d.vsync = store.GetInt(LegacyVSyncKey, 1) != 0;
            if (store.HasKey(LegacySchemeKey))
                d.movementScheme = store.GetInt(LegacySchemeKey, 0) == (int)MovementScheme.Mouse ? SettingsData.SchemeMouse : SettingsData.SchemeKeyboard;
            if (store.HasKey(LegacyTutorialKey)) d.tutorialSeen = store.GetInt(LegacyTutorialKey, 0) == 1;
            diagnostics.Add("settings: 파일 없음 → PlayerPrefs 구형 설정을 메모리로 이전(구형 키 유지)");
            return d;
        }

        /// <summary>구형 음량은 0..1 float. 정수 퍼센트로 반올림, 비유한/범위 밖은 정리.</summary>
        static int LegacyPercent(ILegacySettingsStore store, string key, int fallback)
        {
            if (!store.HasKey(key)) return fallback;
            float v = store.GetFloat(key, fallback / 100f);
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return Mathf.Clamp(Mathf.RoundToInt(v * 100f), 0, 100);
        }

        static ReadResult Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return new ReadResult { State = FileState.Missing };
                return Parse(File.ReadAllText(path, Encoding.UTF8), diagnostics);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 읽기 실패 {path}: {e.GetType().Name}");
                return new ReadResult { State = FileState.Invalid };
            }
        }

        /// <summary>완전한 JSON 객체만 받고(루트 중복 키 거부), 루트 schemaVersion이 정수가 아니면 손상. 필드는 하나씩 정리한다.</summary>
        static ReadResult Parse(string json, List<string> diag)
        {
            if (!JsonSyntax.TryGetRootMembers(json, out var m)) return new ReadResult { State = FileState.Invalid };
            int version = CurrentVersion;
            if (m.TryGetValue("schemaVersion", out var rawVersion))
            {
                if (!JsonSyntax.TryParseInt(rawVersion, out version)) return new ReadResult { State = FileState.Invalid };
            }
            else
            {
                diag?.Add("settings: schemaVersion 없음 → v2로 정리");
            }

            var d = new SettingsData();
            d.masterVolume = ReadInt(m, "masterVolume", d.masterVolume, 0, 100, true, diag);
            d.musicVolume = ReadInt(m, "musicVolume", d.musicVolume, 0, 100, true, diag);
            d.sfxVolume = ReadInt(m, "sfxVolume", d.sfxVolume, 0, 100, true, diag);
            int w = ReadInt(m, "width", -1, 640, 7680, false, diag);
            int h = ReadInt(m, "height", -1, 360, 4320, false, diag);
            if (w < 0 || h < 0) { w = d.width; h = d.height; }
            d.width = w;
            d.height = h;
            d.windowMode = ReadEnum(m, "windowMode", d.windowMode, diag, SettingsData.WindowBorderless, SettingsData.WindowWindowed);
            d.vsync = ReadBool(m, "vsync", d.vsync, diag);
            d.movementScheme = ReadEnum(m, "movementScheme", d.movementScheme, diag, SettingsData.SchemeKeyboard, SettingsData.SchemeMouse);
            d.shakePercent = ReadInt(m, "shakePercent", d.shakePercent, 0, 100, true, diag);
            d.effectsLevel = ReadEnum(m, "effectsLevel", d.effectsLevel, diag, SettingsData.EffectsNormal, SettingsData.EffectsLow);
            d.damageNumbers = ReadBool(m, "damageNumbers", d.damageNumbers, diag);
            d.tutorialSeen = ReadBool(m, "tutorialSeen", d.tutorialSeen, diag);
            d.schemaVersion = CurrentVersion;

            return new ReadResult
            {
                State = version > CurrentVersion ? FileState.Future : FileState.Valid,
                Data = d,
                Version = version,
            };
        }

        /// <summary>숫자 필드: 누락/null/숫자 아님/비유한 → 기본값. 소수는 반올림. 범위 밖은 clamp=true면 경계로, 아니면 기본값.</summary>
        static int ReadInt(Dictionary<string, string> m, string key, int fallback, int min, int max, bool clamp, List<string> diag)
        {
            if (!m.TryGetValue(key, out var raw) || raw == "null")
            {
                diag?.Add($"settings: {key} 누락/null → 기본값");
                return fallback;
            }
            double v;
            try
            {
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = double.NaN;
            }
            catch
            {
                v = double.NaN;
            }
            if (double.IsNaN(v) || double.IsInfinity(v))
            {
                diag?.Add($"settings: {key} 숫자 아님/비유한 → 기본값");
                return fallback;
            }
            double rounded = Math.Round(v, MidpointRounding.AwayFromZero);
            if (rounded < min || rounded > max)
            {
                diag?.Add($"settings: {key} 범위 밖 {raw}");
                if (!clamp) return fallback;
                return rounded < min ? min : max;
            }
            return (int)rounded;
        }

        static bool ReadBool(Dictionary<string, string> m, string key, bool fallback, List<string> diag)
        {
            if (m.TryGetValue(key, out var raw))
            {
                if (raw == "true") return true;
                if (raw == "false") return false;
            }
            diag?.Add($"settings: {key} 누락/잘못된 값 → 기본값");
            return fallback;
        }

        static string ReadEnum(Dictionary<string, string> m, string key, string fallback, List<string> diag, params string[] allowed)
        {
            if (m.TryGetValue(key, out var raw) && raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"' &&
                raw.IndexOf('\\') < 0)
            {
                var value = raw.Substring(1, raw.Length - 2).ToLowerInvariant();
                foreach (var a in allowed)
                    if (a == value) return a;
            }
            diag?.Add($"settings: {key} 누락/잘못된 열거값 → 기본값");
            return fallback;
        }

        static void PreserveCorrupt(string path)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string target = $"{path}.{stamp}.corrupt";
                for (int i = 1; File.Exists(target); i++) target = $"{path}.{stamp}-{i}.corrupt";
                File.Copy(path, target);
                diagnostics.Add($"settings: 손상 파일 보존 {target}");
            }
            catch (Exception e)
            {
                diagnostics.Add($"settings: 손상 파일 보존 실패 {path}: {e.GetType().Name}");
            }
        }

        static bool HasCorruptCopy(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                var bytes = File.ReadAllBytes(path);
                foreach (var candidate in Directory.GetFiles(dir, Path.GetFileName(path) + ".*.corrupt"))
                {
                    var other = File.ReadAllBytes(candidate);
                    if (other.Length != bytes.Length) continue;
                    bool same = true;
                    for (int k = 0; k < bytes.Length && same; k++) same = bytes[k] == other[k];
                    if (same) return true;
                }
            }
            catch
            {
                // 확인 실패 시 사본을 하나 더 남긴다.
            }
            return false;
        }

        /// <summary>원자적 저장: .tmp 완전 쓰기 → 재읽기 검증 → 정상 원본만 .bak → 교체. 원본 손상이면 .corrupt 사본을 확보한 뒤
        /// 백업 없이 교체, 미래 버전 원본은 덮지 않고 미래 버전 .bak도 덮지 않는다. 실패 시 false(파일은 이전 정상 상태).</summary>
        public static bool Save(SettingsData data, string path)
        {
            if (data == null || data.schemaVersion > CurrentVersion)
            {
                // 미래 버전 데이터를 v2로 낮춰 쓰지 않는다 — 객체도 바꾸지 않고 거부.
                Debug.LogWarning($"[Settings] 저장 거부 {path}: schemaVersion {data?.schemaVersion} > {CurrentVersion}");
                return false;
            }
            var tmp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                data.schemaVersion = CurrentVersion;
                var json = JsonUtility.ToJson(data);

                FaultHookForTests?.Invoke(SaveStage.WriteTmp);
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                           FileOptions.WriteThrough))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                FaultHookForTests?.Invoke(SaveStage.AfterTmpWritten);

                var check = Parse(File.ReadAllText(tmp, Encoding.UTF8), null);
                if (check.State != FileState.Valid) throw new IOException("settings.tmp 검증 실패");

                var originalState = ReadStateOnly(path);
                var backupState = ReadStateOnly(path + ".bak");
                FaultHookForTests?.Invoke(SaveStage.BeforeReplace);
                switch (originalState)
                {
                    case FileState.Missing:
                        File.Move(tmp, path);
                        break;
                    case FileState.Valid:
                        File.Replace(tmp, path, backupState == FileState.Future ? null : path + ".bak", true);
                        break;
                    case FileState.Invalid:
                        if (!HasCorruptCopy(path))
                        {
                            int before = diagnostics.Count;
                            PreserveCorrupt(path);
                            if (diagnostics.Count == before || diagnostics[diagnostics.Count - 1].Contains("실패"))
                                throw new IOException("손상 설정 원본 보존 실패 — 교체하지 않는다");
                        }
                        File.Replace(tmp, path, null, true);
                        break;
                    default:
                        throw new IOException("미래 버전 settings.json은 덮어쓰지 않는다");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 저장 실패 {path}: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        static FileState ReadStateOnly(string path)
        {
            try
            {
                if (!File.Exists(path)) return FileState.Missing;
                return Parse(File.ReadAllText(path, Encoding.UTF8), null).State;
            }
            catch
            {
                return FileState.Invalid;
            }
        }
    }
}
