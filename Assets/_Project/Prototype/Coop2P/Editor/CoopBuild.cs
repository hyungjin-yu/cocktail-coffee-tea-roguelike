using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CCT.Prototype.Coop2P.Editor
{
    /// <summary>
    /// 로컬 2인 실험판 별도 Windows 실행 폴더. 기존 Builds/·릴리스·공개 ZIP을 덮어쓰지 않도록 전용 폴더에만 쓴다.
    /// PlayerSettings(버전·백엔드·제품명)는 바꾸지 않는다. 실행 진입은 -cct-coop2p 인자(README/cmd).
    /// batch: -executeMethod CCT.Prototype.Coop2P.Editor.CoopBuild.Windows64Batch [-cct-coop-out &lt;폴더&gt;]
    /// </summary>
    public static class CoopBuild
    {
        const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        const string DefaultOut = "Builds/Coop2P-Prototype-2026-11-17";
        public const string ExeName = "CocktailCoffeeTea_Coop2P.exe";

        public static void Windows64Batch()
        {
            try
            {
                var outDir = Arg("-cct-coop-out") ?? DefaultOut;
                if (Directory.Exists(outDir) && Directory.GetFileSystemEntries(outDir).Length > 0)
                    throw new Exception($"출력 폴더가 비어 있지 않음(덮어쓰기 금지): {outDir}");
                Directory.CreateDirectory(outDir);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { BootScenePath },
                    locationPathName = Path.Combine(outDir, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception($"빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}");
                Debug.Log($"[Coop2P] build ok {report.summary.outputPath} {report.summary.totalSize / 1024 / 1024}MB {report.summary.totalTime}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Coop2P] build failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
