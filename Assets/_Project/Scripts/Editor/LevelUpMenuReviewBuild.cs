using System;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>2026-11-22 레벨업 메뉴판 실창 검수용 일반 Windows 빌드(배포 아님). 엄격 배포 검사(오디오 측정 리포트 시각 등)는 거치지 않고,
    /// 씬·설정은 바꾸지 않는다. 출력: Builds/LevelUpReview(git ignore).</summary>
    public static class LevelUpMenuReviewBuild
    {
        /// <summary>2026-11-24 통합 검수 빌드: 출력 폴더를 환경변수 CCT_REVIEW_BUILD_DIR로 지정(기존 실행본과 다른 고유 폴더). 배포 아님.</summary>
        public static void Windows64Integrated()
        {
            try
            {
                var dir = Environment.GetEnvironmentVariable("CCT_REVIEW_BUILD_DIR");
                if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("CCT_REVIEW_BUILD_DIR 없음");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/Boot.unity" },
                    locationPathName = dir + "/CocktailCoffeeTea.exe",
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                });
                Debug.Log($"[IntegratedReview] {report.summary.result} {report.summary.outputPath} errors={report.summary.totalErrors}");
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }

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
