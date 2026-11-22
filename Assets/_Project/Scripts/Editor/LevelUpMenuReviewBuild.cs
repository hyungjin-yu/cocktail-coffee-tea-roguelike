using System;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>2026-11-22 레벨업 메뉴판 실창 검수용 일반 Windows 빌드(배포 아님). 엄격 배포 검사(오디오 측정 리포트 시각 등)는 거치지 않고,
    /// 씬·설정은 바꾸지 않는다. 출력: Builds/LevelUpReview(git ignore).</summary>
    public static class LevelUpMenuReviewBuild
    {
        public static void Windows64()
        {
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/Boot.unity" },
                    locationPathName = "Builds/LevelUpReview/CocktailCoffeeTea.exe",
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                });
                Debug.Log($"[LevelUpReview] {report.summary.result} {report.summary.outputPath}");
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }
    }
}
