using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>2026-11-18 카페 입장형 첫 화면 검수본: 별도 폴더에만 빌드(기존 Builds/·공개 ZIP 미변경, PlayerSettings 미변경).
    /// batch: -executeMethod CCT.Editor.CafeTitleBuild.Windows64Batch [-cct-cafetitle-out &lt;폴더&gt;]</summary>
    public static class CafeTitleBuild
    {
        const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        const string DefaultOut = "Builds/CafeTitle-2026-11-18";

        public static void Windows64Batch()
        {
            try
            {
                var outDir = Arg("-cct-cafetitle-out") ?? DefaultOut;
                if (Directory.Exists(outDir) && Directory.GetFileSystemEntries(outDir).Length > 0)
                    throw new Exception($"출력 폴더가 비어 있지 않음(덮어쓰기 금지): {outDir}");
                Directory.CreateDirectory(outDir);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { BootScenePath },
                    locationPathName = Path.Combine(outDir, "CocktailCoffeeTea.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception($"빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}");
                Debug.Log($"[CafeTitle] build ok {report.summary.outputPath} {report.summary.totalSize / 1024 / 1024}MB {report.summary.totalTime}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CafeTitle] build failed: {e}");
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
