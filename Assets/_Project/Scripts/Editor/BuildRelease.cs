using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// 10_검증 빌드 납품 기준 v1.0 §4 — Windows64 릴리스 빌드 진입점. Windows x86_64, Mono 백엔드,
    /// Development Build/Script Debugging 둘 다 off, 버전 1.0.0. 출력은 Builds/Windows64/에.
    ///
    /// "빌드 전에 필수 데이터·폰트·오디오·씬·asset manifest를 검사한다. 누락은 명시적 빌드 실패다"(10:91) —
    /// 2026-09-25엔 오디오·정식 폰트가 실제로 없어서 이 검사가 의도적으로 실패했었음. 2026-09-27에
    /// 폰트(Noto Sans CJK KR Regular)를 실제로 동봉하고 UiFactory가 그걸 쓰게 배선했으므로 그 항목은
    /// 이제 통과해야 정상이다.
    /// </summary>
    public static class BuildRelease
    {
        const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        const string OutputExePath = "Builds/Windows64/CocktailCoffeeTea.exe";
        /// <summary>09의 "Assets/_Project/Audio/v1/" 표기를 Resources.Load 런타임 요건에 맞게 조정한
        /// 실제 경로(AudioService.LoadClip과 반드시 일치해야 함).</summary>
        const string AudioFolder = "Assets/_Project/Resources/Audio/v1";
        const int ExpectedSfxCount = 22;
        const int ExpectedBgmCount = 3;
        /// <summary>09:126 — UiFactory.Font가 Resources.Load로 읽는 바로 그 경로(Resources 폴더 기준
        /// "Fonts/NotoSansKR-Regular"). 파일 시스템 경로로는 Resources 접두어가 붙는다.</summary>
        const string FontFolder = "Assets/_Project/Resources/Fonts";
        const string FontFileStem = "NotoSansKR-Regular";

        [MenuItem("CCT/Build Windows64 Release")]
        public static void Windows64() => BuildStrict(OutputExePath, null);

        // RC 검증도 동일한 strict 검사와 1.0.0/Release 설정을 사용한다. QA 타입은 전용 심볼 빌드에만 포함한다.
        public static void Windows64ReleasePlayVerifyBatch() => StrictQaBatch("CCT_PLAY_VERIFIER", "Builds/ReleasePlayVerify");
        public static void Windows64ReleaseLoadTestBatch() => StrictQaBatch("CCT_QA_LOAD", "Builds/ReleaseLoadTest");
        public static void Windows64ReleaseQaTourBatch() => StrictQaBatch("CCT_QA_TOUR", "Builds/ReleaseQaTour");

        /// <summary>2026-10-13: 같은 strict 출시 빌드를 -cct-preview-out 폴더에 만든다(기존 Builds/Windows64·이전 실행본을 덮어쓰지 않음).</summary>
        public static void Windows64ReleaseToBatch()
        {
            try
            {
                BuildStrict(Path.Combine(PreviewOutputDir("Builds/Release"), "CocktailCoffeeTea.exe"), null);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] strict release build failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void StrictQaBatch(string define, string defaultDir)
        {
            try
            {
                BuildStrict(Path.Combine(PreviewOutputDir(defaultDir), "CocktailCoffeeTea.exe"), new[] { define });
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] strict RC QA build failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void BuildStrict(string outputExe, string[] defines)
        {
            var problems = Validate();
            ValidateNoProvisionalArt(problems);
            ValidateCafeUi(problems);
            if (problems.Count > 0)
            {
                var message = "빌드 전 필수 검사 실패(10:91) — 아래 항목이 갖춰질 때까지 빌드를 만들지 않습니다:\n- " +
                               string.Join("\n- ", problems);
                Debug.LogError($"[CCT] {message}");
                throw new BuildFailedException(message);
            }

            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var dir = Path.GetDirectoryName(outputExe);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootScenePath },
                locationPathName = outputExe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                // BuildOptions.None = Development Build도, Script Debugging도 둘 다 off인 기본 상태.
                options = BuildOptions.None,
                extraScriptingDefines = defines,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                var message = $"Unity 빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}개";
                Debug.LogError($"[CCT] {message}");
                throw new BuildFailedException(message);
            }

            Debug.Log($"[CCT] 빌드 완료 — {report.summary.outputPath}, " +
                      $"{report.summary.totalSize / 1024 / 1024}MB, {report.summary.totalTime}");
        }

        /// <summary>씬·폰트·오디오·핵심 데이터 수량·아트 v1 매니페스트를 검사한다. *Database.LoadAll()을
        /// 직접 불러서 실행 경로를 확인한다. 32 영구 강화는 별도로 실제 Resources24개와 폴백의 일치를
        /// 요구하며, 31 새 UI는 Unity 동작 승인이 있어야 strict 빌드를 허용한다.</summary>
        static List<string> Validate()
        {
            var problems = new List<string>();

            if (!File.Exists(BootScenePath))
                problems.Add($"시작 씬 없음: {BootScenePath} (CCT/Create Boot Scene And Fix Build List 먼저 실행)");
            else if (EditorBuildSettings.scenes.Length != 1 ||
                     EditorBuildSettings.scenes[0].path != BootScenePath)
                problems.Add("빌드 씬 목록이 Boot.unity 하나로 정리돼 있지 않음(기존 SampleScene 등이 남아있을 수 있음)");

            bool hasFont = Directory.Exists(FontFolder) &&
                           Directory.GetFiles(FontFolder, $"{FontFileStem}.*")
                               .Any(f => f.EndsWith(".otf") || f.EndsWith(".ttf"));
            if (!hasFont)
                problems.Add($"동봉 폰트 없음: {FontFolder}/{FontFileStem}.otf (09:126 — Noto Sans CJK KR Regular 필요)");

            ValidateAudio(problems);

            const string MixerAssetPath = "Assets/_Project/Resources/Audio/CCTMixer.mixer";
            if (!File.Exists(MixerAssetPath))
                problems.Add($"AudioMixer 없음: {MixerAssetPath} (09:71 — CCT/Build Audio Mixer (Master Music SFX) 먼저 실행)");

            CheckCount("Cards", CardDatabase.LoadAll().Count, 34, problems);
            CheckCount("Skills", SkillDatabase.LoadAll().Count, 15, problems);
            CheckCount("PermanentSkills", PermanentSkillDatabase.LoadAll().Count, 24, problems);
            ValidatePermanentSkills(problems);

            ValidateArt(problems);
            try { TeaEncounterArtImport.Validate(); }
            catch(Exception error) { problems.Add(error.Message); }

            return problems;
        }

        static void ValidatePermanentSkills(List<string> problems)
        {
            var baked = Resources.LoadAll<PermanentSkillData>("PermanentSkills");
            if (baked.Length != 24) { problems.Add("32 Resources/PermanentSkills 실제 24개 에셋 필요"); return; }
            var tree = new PermanentSkillTree(baked, null);
            problems.AddRange(tree.DataErrors);
            var fresh = DefaultPermanentSkills.CreateRuntime();
            foreach (var expected in fresh)
            {
                var actual = baked.FirstOrDefault(n => n.id == expected.id);
                bool same = actual != null && actual.cost == expected.cost && actual.displayName == expected.displayName &&
                    actual.description == expected.description && actual.branch == expected.branch && actual.tier == expected.tier &&
                    actual.group == expected.group && actual.minimumClears == expected.minimumClears &&
                    actual.weaponSpecific == expected.weaponSpecific && (!actual.weaponSpecific || actual.targetWeapon == expected.targetWeapon) &&
                    actual.Prerequisites.Select(n => n.id).SequenceEqual(expected.Prerequisites.Select(n => n.id)) &&
                    actual.effects.SequenceEqual(expected.effects) && (actual.iconResourcePath ?? string.Empty) == (expected.iconResourcePath ?? string.Empty) &&
                    (actual.iconArtId ?? string.Empty) == (expected.iconArtId ?? string.Empty);
                if (!same) problems.Add($"32 baked/fallback 불일치: {expected.id}");
            }
            if (baked.Sum(n => n.cost) != 129) problems.Add("32 영구 총비용은129이어야 함");
            foreach (var n in fresh) UnityEngine.Object.DestroyImmediate(n);
        }

        /// <summary>기획 20: 사용자가 1차 실행본에만 허용한 임시 아트가 런타임 목록에 있으면 출시 빌드는 실패한다.</summary>
        static void ValidateNoProvisionalArt(List<string> problems)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(FirstPassArtIntegrator.CatalogAssetPath);
            if (catalog == null) return;
            var provisional = catalog.assets.Where(a => a != null && a.firstPassProvisional).Select(a => a.assetId).ToList();
            if (provisional.Count > 0)
                problems.Add($"1차 실행본 전용 임시 아트 {provisional.Count}종이 런타임 목록에 포함됨({string.Join(", ", provisional)}) — 출시 불가");
        }

        static void ValidateCafeUi(List<string> problems)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(FirstPassArtIntegrator.CatalogAssetPath);
            var borders = new Dictionary<string, Vector4>
            {
                { "ui_cafe_panel", new Vector4(12,12,12,12) },
                { "ui_cafe_button", new Vector4(10,10,10,10) },
                { "ui_cafe_card", new Vector4(12,12,12,12) },
                { "ui_cafe_tab", new Vector4(8,8,8,8) },
                { "ui_cafe_bar_frame", new Vector4(6,6,6,6) },
                { "ui_cafe_bar_fill", new Vector4(3,2,3,2) },
                { "ui_cafe_focus", new Vector4(12,12,12,12) },
                { "ui_cafe_header", new Vector4(8,8,8,8) },
            };
            foreach (var contract in borders)
            {
                var asset = catalog?.assets.FirstOrDefault(a => a != null && a.assetId == contract.Key);
                if (asset == null || asset.status != "ready" || asset.firstPassProvisional)
                { problems.Add($"31 UI 정식 승인 아트 없음: {contract.Key}"); continue; }
                var manifestPath = Path.Combine(asset.sourcePath, "manifest.json");
                if (!File.Exists(manifestPath) || !ArtV1Json.TryParse(File.ReadAllText(manifestPath), out object parsed, out _) ||
                    !(parsed is Dictionary<string, object> manifest) ||
                    !manifest.TryGetValue("checks", out object checkValue) || !(checkValue is Dictionary<string, object> checks) ||
                    !checks.TryGetValue("unityVerified", out object unityVerified) || !(unityVerified is bool verified && verified) ||
                    !checks.TryGetValue("strictReleaseReady", out object releaseReady) || !(releaseReady is bool ready && ready))
                    problems.Add($"31 UI 1080/720 동작 승인 전: {contract.Key}");
                var spec = ArtV1Spec.Flats[contract.Key];
                var states = contract.Key == "ui_cafe_button" || contract.Key == "ui_cafe_tab"
                    ? new[] { "normal", "hover", "pressed", "disabled" } : new[] { "effect" };
                var sprites = new List<Sprite>();
                foreach (var state in states)
                {
                    var clip = asset.Clip(state);
                    if (clip == null || clip.frames.Length != 1 || clip.fps != 0 || clip.loop)
                    { problems.Add($"31 UI 단일 정적 state 누락/규격 오류: {contract.Key}/{state}"); continue; }
                    var sprite = clip.frames[0];
                    if (sprite == null || sprite.rect.width != spec.Width || sprite.rect.height != spec.Height ||
                        sprite.border != contract.Value || sprite.pixelsPerUnit != 128f)
                    { problems.Add($"31 UI canvas/9-slice/PPU 오류: {contract.Key}/{state}"); continue; }
                    var path = AssetDatabase.GetAssetPath(sprite);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                        importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != TextureImporterCompression.Uncompressed)
                        problems.Add($"31 UI pixel import 설정 오류: {contract.Key}/{state}");
                    sprites.Add(sprite);
                }
                if (sprites.Distinct().Count() != states.Length)
                    problems.Add($"31 UI state 별 실제 sprite가 필요함: {contract.Key}");
            }
        }

        // ── 1차 프리뷰 빌드(출시 아님) ─────────────────────────────────────────────

        const string PreviewDefaultDir = "Builds/FirstPassPreview";
        const string PreviewExeName = "CocktailCoffeeTea.exe";
        const string PreviewStatusName = "FIRST_PASS_PREVIEW_NOT_RELEASE.txt";
        /// <summary>배치 인자 -cct-preview-out &lt;폴더&gt; — QA가 기존 프리뷰를 실행 중일 때 덮어쓰지 않도록 다른 폴더에 빌드한다.</summary>
        public const string PreviewOutArg = "-cct-preview-out";

        // ── 2026-10-12 Android(구글 플레이) ─────────────────────────────────────────────
        // 같은 strict 검사 뒤 IL2CPP·ARM64·targetSdk 36·가로 화면으로 빌드한다. 서명은 Unity 기본 디버그 키(사용자 키스토어 미설정)라
        // 산출물 이름에 debugsigned를 넣는다 — APK는 사이드로드 시험용, AAB는 구조 확인용이며 Play 업로드용이 아니다(키스토어·계정은 나중에).
        public const string AndroidPackageId = "com.hyungjinyu.cocktailcoffeetea"; // 잠정(사용자 지정)
        public const int AndroidVersionCode = 2;
        public const int AndroidMinSdk = 26;    // Android 8.0
        public const int AndroidTargetSdk = 36; // Google Play 현행 목표 API(로컬 SDK platforms/android-36 확인)

        public static void AndroidApkBatch() => AndroidBatch(false);
        public static void AndroidAabBatch() => AndroidBatch(true);

        static void AndroidBatch(bool appBundle)
        {
            try
            {
                var dir = PreviewOutputDir("Builds/Android");
                bool signed = UploadKeyAvailable();
                var file = appBundle
                    ? (signed ? $"CocktailCoffeeTea-1.0.1-vc{AndroidVersionCode}-upload-signed.aab" : "CocktailCoffeeTea-1.0.1-debugsigned-NOT-FOR-PLAY-UPLOAD.aab")
                    : (signed ? $"CocktailCoffeeTea-1.0.1-vc{AndroidVersionCode}-upload-signed.apk" : "CocktailCoffeeTea-1.0.1-debugsigned-sideload.apk");
                BuildAndroid(Path.Combine(dir, file), appBundle);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] Android build failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Android 플레이어 설정(시험이 값을 확인한다). 데스크톱 설정은 건드리지 않는다.</summary>
        public static void ApplyAndroidPlayerSettings()
        {
            PlayerSettings.bundleVersion = "1.0.1";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)AndroidMinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)AndroidTargetSdk;
            PlayerSettings.Android.bundleVersionCode = AndroidVersionCode;
            PlayerSettings.Android.useCustomKeystore = false; // 서명 키는 사용자가 나중에 제공
            PlayerSettings.Android.splitApplicationBinary = false;
            // 가로 전용(좌·우 가로 자동 회전), 세로 금지.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            ApplyAndroidIcon();
        }

        /// <summary>root가 만든 512 앱 아이콘(Aseprite, PixelLab 원화 기반)을 Android 런처 아이콘(Legacy·Round)에만 넣는다.
        /// 적응형 아이콘(전경/배경 2층)은 별도 층 아트가 없어 비워 두고 Android가 Legacy를 쓰게 한다. 데스크톱 아이콘은 건드리지 않는다.</summary>
        public const string AndroidIconPath = "Assets/_Project/Art/Mobile/app-icon-512.png";
        public static int ApplyAndroidIcon()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidIconPath);
            if (tex == null) { Debug.LogWarning($"[CCT] Android 아이콘 없음: {AndroidIconPath}"); return 0; }
            int set = 0;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                bool changed = false;
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount > 1) continue; // 적응형 — 층 아트 없음
                    icon.SetTexture(tex, 0);
                    changed = true; set++;
                }
                if (changed) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            return set;
        }

        static void BuildAndroid(string output, bool appBundle)
        {
            var problems = Validate();
            ValidateNoProvisionalArt(problems);
            ValidateCafeUi(problems);
            if (problems.Count > 0)
                throw new BuildFailedException("빌드 전 필수 검사 실패:\n- " + string.Join("\n- ", problems));

            ApplyAndroidPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = appBundle;
            var dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // 2026-10-13 기획 52: Android 앱 이름 = "음료 마스터". productName은 Windows 세이브 경로(persistentDataPath)와 묶여 있어
            // 이 빌드 동안만 바꾸고 되돌린다(Android 세이브 경로는 패키지 ID 기준이라 영향 없음).
            var previousProductName = PlayerSettings.productName;
            var previousKeystore = PlayerSettings.Android.keystoreName;
            var previousAlias = PlayerSettings.Android.keyaliasName;
            try
            {
                PlayerSettings.productName = GameInfo.Title;
                bool uploadSigned = ApplyUploadKeyFromEnvironment();
                Debug.Log($"[CCT] Android signing: {(uploadSigned ? "upload key from environment (values not logged)" : "Unity debug key — NOT for Play upload")}");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { BootScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"Unity Android 빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}개");
                Debug.Log($"[CCT] Android 빌드 완료 — {report.summary.outputPath}, {report.summary.totalSize / 1024 / 1024}MB, {report.summary.totalTime}");
            }
            finally
            {
                // 비밀값·키 경로를 ProjectSettings에 남기지 않는다.
                PlayerSettings.productName = previousProductName;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = previousKeystore;
                PlayerSettings.Android.keyaliasName = previousAlias;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Play 업로드 키(사용자 보관) 환경 변수: CCT_ANDROID_KEYSTORE(파일 경로), CCT_ANDROID_KEYSTORE_PASS, CCT_ANDROID_KEY_ALIAS,
        /// CCT_ANDROID_KEY_PASS. 모두 있고 파일이 존재할 때만 적용한다. 값은 로그·파일에 남기지 않는다. 없으면 Unity 디버그 키(업로드 불가).</summary>
        public static bool UploadKeyAvailable()
        {
            var path = Environment.GetEnvironmentVariable("CCT_ANDROID_KEYSTORE");
            return !string.IsNullOrEmpty(path) && File.Exists(path) &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CCT_ANDROID_KEYSTORE_PASS")) &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CCT_ANDROID_KEY_ALIAS")) &&
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CCT_ANDROID_KEY_PASS"));
        }

        static bool ApplyUploadKeyFromEnvironment()
        {
            if (!UploadKeyAvailable()) { PlayerSettings.Android.useCustomKeystore = false; return false; }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Environment.GetEnvironmentVariable("CCT_ANDROID_KEYSTORE");
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("CCT_ANDROID_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("CCT_ANDROID_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("CCT_ANDROID_KEY_PASS");
            return true;
        }

        static string PreviewOutputDir(string defaultDir)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == PreviewOutArg && !string.IsNullOrWhiteSpace(args[i + 1])) return args[i + 1];
            return defaultDir;
        }
        public const string PreviewVersion = "1.0.0-firstpass";

        /// <summary>
        /// 기획 20 "1차 통합 실행본" — 출시 검사와 구별된 별도 빌드. 씬·폰트·오디오·믹서·데이터 수량 검사는 그대로 엄격(실패 시 빌드 안 함).
        /// 아트 검사(Art/v1 누락·에러, 임시 아트)만 "알려진 예외"로 기록하고 진행한다. 버전 표기는 1.0.0-firstpass(빌드 후 원래 값 복원),
        /// 출력은 Builds/FirstPassPreview/, 상태 파일에 예외 전체를 적는다. 이 빌드를 출시 합격으로 보고하지 않는다.
        /// </summary>
        [MenuItem("CCT/Build Windows64 First-Pass Preview (NOT release)")]
        public static void Windows64FirstPassPreview() => BuildFirstPassPreview(null, PreviewDefaultDir);

        /// <param name="extraDefines">검증 빌드 전용 스크립팅 심볼(예: CCT_PLAY_VERIFIER). null이면 일반 프리뷰와 같다.</param>
        static void BuildFirstPassPreview(string[] extraDefines, string defaultOutDir)
        {
            var artProblems = new List<string>();
            ValidateArt(artProblems);
            ValidateNoProvisionalArt(artProblems);
            var all = Validate();
            var blocking = all.Where(p => !artProblems.Contains(p)).ToList();
            if (blocking.Count > 0)
            {
                var message = "1차 프리뷰도 필수(아트 외) 검사 실패:\n- " + string.Join("\n- ", blocking);
                Debug.LogError($"[CCT] {message}");
                throw new BuildFailedException(message);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(FirstPassArtIntegrator.CatalogAssetPath);
            var lines = new List<string>
            {
                "칵테일 · 커피 · 차 — 1차 통합 프리뷰 빌드 (출시 빌드 아님, NOT A RELEASE)",
                $"버전 {PreviewVersion}, 생성 {DateTime.UtcNow:o} UTC",
                "출시 BuildRelease 엄격 검사는 아래 아트 예외 때문에 실패 상태 그대로다.",
                "",
                "[아트 예외(출시 검사 실패 항목)]",
            };
            lines.AddRange(artProblems.Select(p => "- " + p));
            lines.Add("");
            lines.Add("[런타임 아트 목록]");
            if (catalog != null)
                foreach (var a in catalog.assets.OrderBy(a => a.assetId))
                    lines.Add($"- {a.assetId} ({a.kind}, {a.status}{(a.firstPassProvisional ? ", 1차 임시 허용" : "")}{(a.bodyHeightEstimated ? ", bodyHeightPx 알파 추정" : "")}) clips=" +
                              string.Join("/", a.clips.Select(c => $"{c.name}:{c.frames.Length}@{c.fps:0}")));
            var missingBodies = ArtV1Spec.Bodies.Keys.Where(id => catalog == null || catalog.assets.All(a => a.assetId != id)).OrderBy(x => x).ToList();
            lines.Add("");
            lines.Add($"[새 몸체 아트 미반입 {missingBodies.Count}/{ArtV1Spec.Bodies.Count}종 — 기존 아이콘/캡슐 대체 표시] " + string.Join(", ", missingBodies));

            if (extraDefines != null)
            {
                var purpose = extraDefines.Contains("CCT_QA_LOAD") ? "10 §3 고정 부하 QA 시나리오(-cct-loadtest, -cct-data-dir 필수)"
                    : extraDefines.Contains("CCT_QA_TOUR") ? "화면·모션·오디오 자동 투어(-cct-autotour, -cct-data-dir 필수)"
                    : "정상 플레이 입력 대행(-cct-playverify, -cct-data-dir 필수)";
                lines.Insert(1, "[검증 빌드] 스크립팅 심볼: " + string.Join(",", extraDefines) + " — " + purpose + " 포함. 배포용 아님.");
            }
            string outDir = PreviewOutputDir(defaultOutDir);
            string previewExePath = Path.Combine(outDir, PreviewExeName);
            string originalVersion = PlayerSettings.bundleVersion;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            try
            {
                PlayerSettings.bundleVersion = PreviewVersion;
                Directory.CreateDirectory(outDir);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { BootScenePath },
                    locationPathName = previewExePath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                    extraScriptingDefines = extraDefines,
                });
                lines.Add("");
                lines.Add($"[빌드] {report.summary.result}, 에러 {report.summary.totalErrors}, {report.summary.totalSize / 1024 / 1024}MB, {report.summary.totalTime}");
                File.WriteAllLines(Path.Combine(outDir, PreviewStatusName), lines);
                if (extraDefines == null) // 검증 빌드는 일반 프리뷰 상태 기록을 덮어쓰지 않는다
                {
                    Directory.CreateDirectory("Delivery/logs");
                    File.WriteAllLines("Delivery/logs/firstpass-preview-status.txt", lines);
                }
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"1차 프리뷰 빌드 실패: {report.summary.result}");
                Debug.Log($"[CCT] 1차 프리뷰 빌드 완료(출시 아님) — {report.summary.outputPath}");
            }
            finally
            {
                PlayerSettings.bundleVersion = originalVersion;
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>-executeMethod CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch</summary>
        public static void Windows64FirstPassPreviewBatch()
        {
            try
            {
                Windows64FirstPassPreview();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] 1차 프리뷰 빌드 실패: {e.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>정상 플레이 검증 빌드(출시·배포 아님): 1차 프리뷰와 같은 검사·내용에 CCT_PLAY_VERIFIER 심볼만 더한다.
        /// 기본 출력은 Builds/PlayVerify(-cct-preview-out으로 변경). 일반 프리뷰/출시 빌드에는 입력 대행 코드가 들어가지 않는다.
        /// -executeMethod CCT.Editor.BuildRelease.Windows64PlayVerifyBatch</summary>
        public static void Windows64PlayVerifyBatch()
        {
            try
            {
                BuildFirstPassPreview(new[] { "CCT_PLAY_VERIFIER" }, "Builds/PlayVerify");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] 검증 빌드 실패: {e.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>10 §3 고정 부하 측정 빌드(출시·배포 아님): 1차 프리뷰와 같은 검사·내용에 CCT_QA_LOAD 심볼만 더한다.
        /// 기본 출력은 Builds/LoadTest(-cct-preview-out으로 변경). 일반 프리뷰/출시 빌드에는 부하 시나리오 코드가 들어가지 않는다.
        /// -executeMethod CCT.Editor.BuildRelease.Windows64LoadTestBatch</summary>
        public static void Windows64LoadTestBatch()
        {
            try
            {
                BuildFirstPassPreview(new[] { "CCT_QA_LOAD" }, "Builds/LoadTest");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] 부하 측정 빌드 실패: {e.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>화면·모션·오디오 QA 투어 빌드(출시·배포 아님): 1차 프리뷰와 같은 검사·내용에 CCT_QA_TOUR 심볼만 더한다.
        /// 기본 출력은 Builds/QaTour(-cct-preview-out으로 변경). 일반 프리뷰/출시 빌드에는 자동 투어 코드가 들어가지 않는다.
        /// -executeMethod CCT.Editor.BuildRelease.Windows64QaTourBatch</summary>
        public static void Windows64QaTourBatch()
        {
            try
            {
                BuildFirstPassPreview(new[] { "CCT_QA_TOUR" }, "Builds/QaTour");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CCT] QA 투어 빌드 실패: {e.Message}");
                EditorApplication.Exit(1);
            }
        }

        const string ArtImportRoot = "Assets/_Project/Art/v1";

        /// <summary>10:91 "asset manifest 누락은 명시적 빌드 실패다" — 이전엔 데이터 수량(카드/스킬/영구)으로만
        /// 대신했다("완전한 아트 매니페스트 검사는 ArtV1이 연결된 뒤 추가해야 한다"던 자리). ArtV1Validator를
        /// Import 모드로 실제 게임 임포트 경로(Assets/_Project/Art/v1)에 대해 돌려 진짜 게이트로 쓴다.
        /// ArtV1Validator.ValidateRoot는 폴더 자체가 없거나 04/09 표에 있는 에셋 폴더가 안 보이면
        /// MissingAsset을 Info 심각도로만 기록한다(Source 단계에서 "아직 없음"은 정상적인 중간 상태라
        /// Error로 잡으면 안 되기 때문) — 그래서 report.ErrorCount만 보면 폴더가 통째로 없어도 0건으로
        /// 통과해버린다. 빌드 게이트에서는 그 구분이 의미 없으므로(누락은 Import 기준으로 전부 실패) Error와
        /// MissingAsset을 심각도와 무관하게 둘 다 실패로 취급한다.</summary>
        static void ValidateArt(List<string> problems)
        {
            var report = ArtV1Validator.ValidateRoot(ArtImportRoot, ArtV1ValidationMode.Import);

            var errors = report.Issues.Where(i => i.Severity == ArtV1Severity.Error).ToList();
            var missing = report.Issues.Where(i => i.Code == ArtV1IssueCode.MissingAsset).ToList();

            if (errors.Count > 0)
                problems.Add($"아트 v1 검수 에러 {errors.Count}건({ArtImportRoot}) — 예: {errors[0]}");
            if (missing.Count > 0)
                problems.Add($"아트 v1 필수 에셋 누락 {missing.Count}건({ArtImportRoot}) — " +
                             string.Join(" / ", missing.Select(i => i.Message)));
        }

        static void CheckCount(string label, int actual, int expected, List<string> problems)
        {
            if (actual < expected)
                problems.Add($"{label} 데이터 부족: {actual}/{expected}");
        }

        [System.Serializable]
        class AudioMeasurementsReport
        {
            public string[] issues;
        }

        /// <summary>09:69/136 "형식/길이/피크/루프를 검사" — 파일 개수만 세는 대신 Tools/Audio/measure_audio.py가
        /// 실제로 만든 리포트(audio_measurements.json)를 읽어 문제 0건인지 확인한다. 오디오를 다시 합성만
        /// 하고 측정을 다시 안 돌리면(wav가 리포트보다 최신) 그 사실도 명시적 실패로 잡는다 — "측정 리포트가
        /// 있다"는 사실만으로 "지금 이 wav들이 그 기준을 통과했다"고 가정하지 않는다.</summary>
        static void ValidateAudio(List<string> problems)
        {
            int need = ExpectedSfxCount + ExpectedBgmCount;
            if (!Directory.Exists(AudioFolder))
            {
                problems.Add($"오디오 폴더 없음: {AudioFolder} (09:4 — SFX {ExpectedSfxCount}개 + BGM {ExpectedBgmCount}개 필요)");
                return;
            }

            var wavFiles = Directory.GetFiles(AudioFolder, "*.wav", SearchOption.AllDirectories);
            if (wavFiles.Length < need)
                problems.Add($"오디오 파일 부족: {wavFiles.Length}/{need} (.wav, {AudioFolder} 기준)");

            var measurementsPath = Path.GetFullPath(Path.Combine(AudioFolder, "..", "audio_measurements.json"));
            if (!File.Exists(measurementsPath))
            {
                problems.Add($"오디오 측정 리포트 없음: {measurementsPath} (Tools/Audio/measure_audio.py 먼저 실행)");
                return;
            }

            var newestWav = wavFiles.Length > 0
                ? wavFiles.Max(File.GetLastWriteTimeUtc)
                : DateTime.MinValue;
            var measuredAt = File.GetLastWriteTimeUtc(measurementsPath);
            if (newestWav > measuredAt)
            {
                problems.Add("오디오 wav가 측정 리포트보다 최신 — Tools/Audio/measure_audio.py 재실행 필요" +
                             "(형식/길이/피크/루프가 지금 파일 기준으로 재검증되지 않음)");
                return;
            }

            AudioMeasurementsReport report;
            try
            {
                report = JsonUtility.FromJson<AudioMeasurementsReport>(File.ReadAllText(measurementsPath));
            }
            catch (System.Exception e)
            {
                problems.Add($"오디오 측정 리포트 파싱 실패: {measurementsPath} ({e.Message})");
                return;
            }

            // measure_audio.py는 정상 실행되면 issues를 항상 배열로 쓴다(문제 없어도 빈 배열 []) —
            // null이거나 report 자체가 비어있으면 "측정이 제대로 안 됨"이라는 뜻이므로 통과가 아니라
            // 실패로 닫는다(코디네이터 지적: "issues=null도 실패로 잡을 것").
            if (report == null || report.issues == null)
                problems.Add($"오디오 측정 리포트 형식 이상(issues 필드 없음/파싱 실패): {measurementsPath}");
            else if (report.issues.Length > 0)
                problems.Add($"오디오 측정 문제 {report.issues.Length}건(형식/길이/피크/루프) — 상세는 {measurementsPath} 참고: " +
                             string.Join(" | ", report.issues));
        }
    }
}
