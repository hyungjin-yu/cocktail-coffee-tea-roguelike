using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// Art v1 검수 메뉴. 파일을 읽기만 하고 에셋을 복사·임포트·수정하지 않는다.
    /// 결과 마크다운은 Logs/ 아래에 쓴다(깃 무시 폴더).
    /// </summary>
    public static class ArtV1ValidationMenu
    {
        const string SourceRoot = "ArtSource/v1";
        const string ImportRoot = "Assets/_Project/Art/v1";

        [MenuItem("CCT/Art v1/Validate ArtSource v1 (report only)")]
        static void ValidateSource() => Run(SourceRoot, ArtV1ValidationMode.Source);

        [MenuItem("CCT/Art v1/Validate Assets Art v1 (import rules)")]
        static void ValidateImported() => Run(ImportRoot, ArtV1ValidationMode.Import);

        static void Run(string root, ArtV1ValidationMode mode)
        {
            var report = ArtV1Validator.ValidateRoot(root, mode);
            Directory.CreateDirectory("Logs");
            string outPath = Path.Combine("Logs", $"ArtV1Validation-{mode}.md");
            File.WriteAllText(outPath, report.ToMarkdown());

            string summary = $"[Art v1 validate] {mode} {root}: 에셋 {report.Assets.Count}, 에러 {report.ErrorCount}, 경고 {report.WarningCount} → {outPath}";
            if (report.ErrorCount > 0) Debug.LogError(summary);
            else if (report.WarningCount > 0) Debug.LogWarning(summary);
            else Debug.Log(summary);
        }
    }
}
