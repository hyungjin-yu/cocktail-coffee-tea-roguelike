using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CCT.Contest
{
    /// <summary>공모전판 Windows 빌드. 본편 BuildRelease(출시 검사·Steam 출력 경로)와 분리.
    /// 배포본: Builds/PPSContest/BeverageMasterPPS.exe (PPS_CONTEST)
    /// 캡처본: Builds/PPSContestCapture/BeverageMasterPPS.exe (PPS_CONTEST + PPS_CAPTURE, 제출 금지)</summary>
    public static class ContestBuild
    {
        const string Scene = "Assets/_Project/Scenes/Boot.unity";
        const string Exe = "BeverageMasterPPS.exe";

        [MenuItem("PPS Contest/Build Windows (submission)")]
        public static void Release() => Build("Builds/PPSContest", null);

        [MenuItem("PPS Contest/Build Windows (capture only, NOT for submission)")]
        public static void Capture() => Build("Builds/PPSContestCapture", new[] { "PPS_CAPTURE" });

        public static void ReleaseBatch() => Build(Arg("-ppsOut") ?? "Builds/PPSContest", null);
        public static void CaptureBatch() => Build(Arg("-ppsOut") ?? "Builds/PPSContestCapture", new[] { "PPS_CAPTURE" });

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        static void Build(string dir, string[] extra)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (!defines.Contains("PPS_CONTEST"))
                throw new BuildFailedException("PPS_CONTEST define missing in Standalone player settings");
            if (PlayerSettings.productName != "BeverageMasterPPS" || PlayerSettings.companyName != "PPSContest2026")
                throw new BuildFailedException("contest product/company name not set — save path would not be isolated");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory(dir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(dir, Exe),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = extra,
            });
            Debug.Log("[PPS] build " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors + " → " + dir);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("PPS build failed: " + report.summary.result);
        }
    }
}
