using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CCT.Editor
{
    public enum ArtV1Severity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// Source = ArtSource/v1 (Codex 작업 중. needs_fix는 정보로만 보고).
    /// Import = Assets/_Project/Art/v1 (04 §9: ready만 복사 가능. needs_fix는 에러).
    /// </summary>
    public enum ArtV1ValidationMode
    {
        Source,
        Import,
    }

    public static class ArtV1IssueCode
    {
        public const string ManifestMissing = "ManifestMissing";
        public const string ManifestParseError = "ManifestParseError";
        public const string ManifestFieldError = "ManifestFieldError";
        public const string SpecVersion = "SpecVersion";
        public const string AssetIdMismatch = "AssetIdMismatch";
        public const string StatusInvalid = "StatusInvalid";
        public const string NotReady = "NotReady";
        public const string ReadyWithDeviations = "ReadyWithDeviations";
        public const string ChecksMissing = "ChecksMissing";
        public const string UnknownAsset = "UnknownAsset";
        public const string ScopeUndecided = "ScopeUndecided";
        public const string PpuMismatch = "PpuMismatch";
        public const string FrameSizeMissing = "FrameSizeMissing";
        public const string FrameSizeSpec = "FrameSizeSpec";
        public const string PivotMissing = "PivotMissing";
        public const string PivotSpec = "PivotSpec";
        public const string TargetHeightMissing = "TargetHeightMissing";
        public const string TargetHeightSpec = "TargetHeightSpec";
        public const string BodyHeightMissing = "BodyHeightMissing";
        public const string BodyHeightMismatch = "BodyHeightMismatch";
        public const string ClipsMissing = "ClipsMissing";
        public const string ClipMissing = "ClipMissing";
        public const string ClipUnexpected = "ClipUnexpected";
        public const string ClipFieldMissing = "ClipFieldMissing";
        public const string ClipTiming = "ClipTiming";
        public const string FrameCountMismatch = "FrameCountMismatch";
        public const string FileMissing = "FileMissing";
        public const string FileNameConvention = "FileNameConvention";
        public const string LeftFacingFile = "LeftFacingFile";
        public const string UnreferencedFile = "UnreferencedFile";
        public const string PreviewInImport = "PreviewInImport";
        public const string PngUnreadable = "PngUnreadable";
        public const string PngDecodeUnsupported = "PngDecodeUnsupported";
        public const string ImageSizeMismatch = "ImageSizeMismatch";
        public const string NoAlphaChannel = "NoAlphaChannel";
        public const string NoTransparentPixels = "NoTransparentPixels";
        public const string EmptyFrame = "EmptyFrame";
        public const string OpaqueCanvasBorder = "OpaqueCanvasBorder";
        public const string PossiblePaintedBackground = "PossiblePaintedBackground";
        public const string OutsideSafeArea = "OutsideSafeArea";
        public const string BelowFootLine = "BelowFootLine";
        public const string AnchorsMissing = "AnchorsMissing";
        public const string AnchorOutOfCanvas = "AnchorOutOfCanvas";
        public const string MissingAsset = "MissingAsset";
    }

    public sealed class ArtV1Issue
    {
        public readonly ArtV1Severity Severity;
        public readonly string Code;
        public readonly string AssetId;
        public readonly string Path;
        public readonly string Message;

        public ArtV1Issue(ArtV1Severity severity, string code, string assetId, string path, string message)
        {
            Severity = severity;
            Code = code;
            AssetId = assetId;
            Path = path;
            Message = message;
        }

        public override string ToString() => $"[{Severity}] {Code} {AssetId} {Path}: {Message}";
    }

    public sealed class ArtV1AssetSummary
    {
        public string AssetId;
        public string Directory;
        public ArtV1Kind Kind;
        public string Status;
        public int FramesListed;
        public int FramesExpected;
        public float? VisualScale;
        public int ErrorCount;
        public int WarningCount;
        /// <summary>status가 ready이고 에러가 없을 때만 true. 경고는 사람이 보고 판단한다.</summary>
        public bool Importable;
    }

    public sealed class ArtV1Report
    {
        public ArtV1ValidationMode Mode;
        public string Root;
        public readonly List<ArtV1Issue> Issues = new List<ArtV1Issue>();
        public readonly List<ArtV1AssetSummary> Assets = new List<ArtV1AssetSummary>();

        public int ErrorCount => Issues.Count(i => i.Severity == ArtV1Severity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == ArtV1Severity.Warning);

        public bool Has(string code, string assetId = null) =>
            Issues.Any(i => i.Code == code && (assetId == null || i.AssetId == assetId));

        public IEnumerable<ArtV1Issue> For(string assetId) => Issues.Where(i => i.AssetId == assetId);

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Art v1 검수 보고 (자동 생성)");
            sb.AppendLine();
            sb.AppendLine($"- 모드: {Mode} / 대상: `{Root}`");
            sb.AppendLine($"- 에셋 {Assets.Count}개, 에러 {ErrorCount}, 경고 {WarningCount}");
            sb.AppendLine("- 알파/배경 항목은 휴리스틱이다. 픽셀로 그려 넣은 바둑판·흰 배경은 알파만으로 확실히 판별할 수 없으므로 사람이 이미지를 직접 확인해야 한다.");
            sb.AppendLine();
            sb.AppendLine("| assetId | kind | status | 프레임(목록/기준) | visualScale | 에러 | 경고 | 적용 가능 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var a in Assets)
            {
                string scale = a.VisualScale.HasValue ? a.VisualScale.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
                string expected = a.FramesExpected > 0 ? a.FramesExpected.ToString(CultureInfo.InvariantCulture) : "?";
                sb.AppendLine($"| {Esc(a.AssetId)} | {a.Kind} | {Esc(a.Status ?? "-")} | {a.FramesListed}/{expected} | {scale} | {a.ErrorCount} | {a.WarningCount} | {(a.Importable ? "예" : "아니오")} |");
            }
            sb.AppendLine();
            sb.AppendLine("| 심각도 | 코드 | assetId | 경로 | 내용 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var i in Issues.OrderByDescending(i => i.Severity).ThenBy(i => i.AssetId, StringComparer.Ordinal))
                sb.AppendLine($"| {i.Severity} | {i.Code} | {Esc(i.AssetId ?? "-")} | {Esc(i.Path ?? "-")} | {Esc(i.Message)} |");
            return sb.ToString();
        }

        static string Esc(string s) => (s ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }

    /// <summary>프레임 한 장의 알파 통계. 좌표는 PNG와 같은 위쪽 행 기준.</summary>
    public sealed class ArtV1AlphaStats
    {
        public int Width, Height;
        public int TransparentPixels;   // alpha == 0
        public int OpaquePixels;        // alpha >= 250
        public int PartialPixels;       // 그 사이
        public int BorderPixels;
        public int BorderOpaquePixels;
        public int BorderLightNeutralOpaquePixels;
        /// <summary>alpha &gt; 임계값인 픽셀의 경계 상자. 없으면 HasContent=false.</summary>
        public bool HasContent;
        public int MinX, MaxX, MinYTop, MaxYTop;

        public int TotalPixels => Width * Height;
        public float BorderOpaqueRatio => BorderPixels == 0 ? 0f : (float)BorderOpaquePixels / BorderPixels;
        public float BorderLightNeutralRatio => BorderPixels == 0 ? 0f : (float)BorderLightNeutralOpaquePixels / BorderPixels;
        /// <summary>좌하단 원점 기준 최고 본체 픽셀 y.</summary>
        public int TopYFromBottom => Height - 1 - MinYTop;
        /// <summary>좌하단 원점 기준 최저 본체 픽셀 y.</summary>
        public int BottomYFromBottom => Height - 1 - MaxYTop;

        public const int ContentAlphaThreshold = 16;

        public static ArtV1AlphaStats Analyze(ArtV1Png png)
        {
            if (png.Rgba == null) throw new InvalidOperationException("픽셀이 디코딩되지 않은 PNG");
            var s = new ArtV1AlphaStats { Width = png.Width, Height = png.Height, MinX = int.MaxValue, MinYTop = int.MaxValue, MaxX = -1, MaxYTop = -1 };
            var px = png.Rgba;
            for (int y = 0; y < png.Height; y++)
            {
                for (int x = 0; x < png.Width; x++)
                {
                    int o = (y * png.Width + x) * 4;
                    byte a = px[o + 3];
                    if (a == 0) s.TransparentPixels++;
                    else if (a >= 250) s.OpaquePixels++;
                    else s.PartialPixels++;

                    if (a > ContentAlphaThreshold)
                    {
                        s.HasContent = true;
                        if (x < s.MinX) s.MinX = x;
                        if (x > s.MaxX) s.MaxX = x;
                        if (y < s.MinYTop) s.MinYTop = y;
                        if (y > s.MaxYTop) s.MaxYTop = y;
                    }

                    bool border = x == 0 || y == 0 || x == png.Width - 1 || y == png.Height - 1;
                    if (!border) continue;
                    s.BorderPixels++;
                    if (a < 250) continue;
                    s.BorderOpaquePixels++;
                    int r = px[o], g = px[o + 1], b = px[o + 2];
                    int min = Math.Min(r, Math.Min(g, b));
                    int max = Math.Max(r, Math.Max(g, b));
                    // 흰색/밝은 회색(바둑판 칸 색으로 흔함)인지. 이것만으로 바둑판이라고 단정하지 않는다.
                    if (min >= 170 && max - min <= 24) s.BorderLightNeutralOpaquePixels++;
                }
            }
            return s;
        }
    }

    /// <summary>
    /// 04 §7 매니페스트/프레임 계약과 09 §1·§2 추가분을 기준으로 Art v1 에셋 폴더를 검수한다.
    /// 파일을 읽기만 하고 아무것도 쓰거나 옮기지 않는다. UnityEngine 비의존.
    /// </summary>
    public static class ArtV1Validator
    {
        public const string ManifestFileName = "manifest.json";
        public const string PreviewFileName = "preview.png";
        const int BodyHeightTolerancePx = 2;
        const float BorderOpaqueWarnRatio = 0.9f;
        const float BorderLightNeutralWarnRatio = 0.6f;

        /// <summary>root 아래 하위 폴더 하나당 에셋 하나로 보고 모두 검수한다.
        /// 04/09 표에 있는데 폴더가 없는 ID는 Info(MissingAsset)로 한 줄씩 모아 보고한다.</summary>
        public static ArtV1Report ValidateRoot(string root, ArtV1ValidationMode mode)
        {
            var report = new ArtV1Report { Mode = mode, Root = root };
            if (!Directory.Exists(root))
            {
                report.Issues.Add(new ArtV1Issue(ArtV1Severity.Info, ArtV1IssueCode.MissingAsset, null, root, "검수 대상 폴더가 아직 없다"));
                return report;
            }

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
            {
                present.Add(Path.GetFileName(dir));
                ValidateAssetDirectory(dir, mode, report);
            }

            var missingBodies = ArtV1Spec.Bodies.Keys.Where(id => !present.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var missingFlats = ArtV1Spec.Flats.Keys.Where(id => !present.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (missingBodies.Count > 0)
                report.Issues.Add(new ArtV1Issue(ArtV1Severity.Info, ArtV1IssueCode.MissingAsset, null, root,
                    $"04/09 몸체 {ArtV1Spec.Bodies.Count}종 중 폴더 없음 {missingBodies.Count}종: {string.Join(", ", missingBodies)}"));
            if (missingFlats.Count > 0)
                report.Issues.Add(new ArtV1Issue(ArtV1Severity.Info, ArtV1IssueCode.MissingAsset, null, root,
                    $"04/09 VFX·UI·배경 {ArtV1Spec.Flats.Count}개 중 폴더 없음 {missingFlats.Count}개: {string.Join(", ", missingFlats)}"));
            return report;
        }

        public static ArtV1Report ValidateAssetDirectory(string assetDir, ArtV1ValidationMode mode)
        {
            var report = new ArtV1Report { Mode = mode, Root = assetDir };
            ValidateAssetDirectory(assetDir, mode, report);
            return report;
        }

        static void ValidateAssetDirectory(string assetDir, ArtV1ValidationMode mode, ArtV1Report report)
        {
            string folderId = Path.GetFileName(assetDir.TrimEnd('/', '\\'));
            var ctx = new Ctx(report, folderId);
            var summary = new ArtV1AssetSummary { AssetId = folderId, Directory = assetDir };
            int issuesBefore = report.Issues.Count;

            try
            {
                Validate(assetDir, mode, ctx, summary);
            }
            finally
            {
                var mine = report.Issues.Skip(issuesBefore).ToList();
                summary.ErrorCount = mine.Count(i => i.Severity == ArtV1Severity.Error);
                summary.WarningCount = mine.Count(i => i.Severity == ArtV1Severity.Warning);
                summary.Importable = summary.Status == "ready" && summary.ErrorCount == 0;
                report.Assets.Add(summary);
            }
        }

        static void Validate(string assetDir, ArtV1ValidationMode mode, Ctx ctx, ArtV1AssetSummary summary)
        {
            string manifestPath = Path.Combine(assetDir, ManifestFileName);
            summary.Kind = ArtV1Spec.InferKind(ctx.AssetId);
            if (ArtV1Spec.TryGetBody(ctx.AssetId, out var knownBody)) summary.FramesExpected = knownBody.TotalFrames;
            else if (ArtV1Spec.TryGetFlat(ctx.AssetId, out var knownFlat)) summary.FramesExpected = knownFlat.Frames;

            if (mode == ArtV1ValidationMode.Import && File.Exists(Path.Combine(assetDir, PreviewFileName)))
                ctx.Warn(ArtV1IssueCode.PreviewInImport, PreviewFileName, "preview.png는 연락용 컨택트 시트라 Art/v1로 복사하지 않는다 (04 §7)");

            if (!File.Exists(manifestPath))
            {
                ctx.Add(mode == ArtV1ValidationMode.Import ? ArtV1Severity.Error : ArtV1Severity.Warning,
                    ArtV1IssueCode.ManifestMissing, ManifestFileName, "manifest.json이 없다 — ready 판정·임포트 불가");
                return;
            }

            var manifest = ArtV1Manifest.Parse(File.ReadAllText(manifestPath), out string parseError);
            if (manifest == null)
            {
                ctx.Error(ArtV1IssueCode.ManifestParseError, ManifestFileName, parseError);
                return;
            }
            foreach (var e in manifest.ParseErrors) ctx.Error(ArtV1IssueCode.ManifestFieldError, ManifestFileName, e);

            summary.Status = manifest.Status;
            var kind = manifest.ResolveKind();
            summary.Kind = kind;

            CheckHeader(manifest, mode, ctx);

            ArtV1Spec.TryGetBody(ctx.AssetId, out var bodySpec);
            ArtV1Spec.TryGetFlat(ctx.AssetId, out var flatSpec);
            if (bodySpec == null && flatSpec == null)
            {
                ctx.Warn(ArtV1IssueCode.UnknownAsset, null, "04/09 표에 없는 asset ID — 형식만 검수한다");
            }
            if (bodySpec != null && kind != ArtV1Kind.Body)
                ctx.Error(ArtV1IssueCode.ManifestFieldError, ManifestFileName, $"04 몸체 ID인데 kind가 {manifest.KindText}로 적혀 있다");

            CheckCanvasFields(manifest, kind, bodySpec, flatSpec, ctx);

            // 프레임 목록: 클립 파일 전체(중복 참조는 한 장으로), 클립이 없으면 files.
            var frames = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var clip in manifest.Clips)
                foreach (var f in clip.Files)
                    if (seen.Add(Normalize(f))) frames.Add(f);
            if (manifest.Clips.Count == 0)
                foreach (var f in manifest.Files)
                    if (seen.Add(Normalize(f))) frames.Add(f);
            summary.FramesListed = manifest.TotalClipFrames > 0 ? manifest.TotalClipFrames : frames.Count;

            if (bodySpec != null)
            {
                summary.FramesExpected = bodySpec.TotalFrames;
                CheckBodyClips(manifest, bodySpec, ctx);
                if (manifest.TargetBodyHeightWorld.HasValue && manifest.BodyHeightPx.HasValue && manifest.BodyHeightPx.Value > 0f)
                    summary.VisualScale = ArtV1Spec.VisualScale(manifest.TargetBodyHeightWorld.Value, manifest.BodyHeightPx.Value);
            }
            else if (flatSpec != null)
            {
                summary.FramesExpected = flatSpec.Frames;
                if (frames.Count != flatSpec.Frames)
                    ctx.Error(ArtV1IssueCode.FrameCountMismatch, null, $"프레임 {frames.Count}장, 04/09 기준 {flatSpec.Frames}장");
            }
            else if (kind == ArtV1Kind.Body && !manifest.HasClips)
            {
                ctx.Error(ArtV1IssueCode.ClipsMissing, ManifestFileName, "몸체 매니페스트에 clips가 없다");
            }

            if (frames.Count == 0)
                ctx.Error(ArtV1IssueCode.FileMissing, ManifestFileName, "매니페스트에 프레임 파일이 하나도 없다");

            foreach (var f in frames)
                if (Path.GetFileName(f).IndexOf("_left_", StringComparison.OrdinalIgnoreCase) >= 0)
                    ctx.Error(ArtV1IssueCode.LeftFacingFile, f, "별도 좌향 PNG는 만들지 않는다 — 좌향은 Unity가 반전한다 (04 §7)");

            CheckFrames(assetDir, manifest, kind, bodySpec, flatSpec, frames, ctx);
            CheckAnchors(manifest, bodySpec, frames, ctx);
            CheckUnreferencedFiles(assetDir, frames, ctx);
        }

        static void CheckHeader(ArtV1Manifest m, ArtV1ValidationMode mode, Ctx ctx)
        {
            if (m.SpecVersion != ArtV1Spec.SpecVersion)
                ctx.Error(ArtV1IssueCode.SpecVersion, ManifestFileName, $"specVersion은 \"{ArtV1Spec.SpecVersion}\"이어야 한다 (현재: {m.SpecVersion ?? "없음"})");

            if (string.IsNullOrEmpty(m.AssetId))
                ctx.Error(ArtV1IssueCode.AssetIdMismatch, ManifestFileName, "assetId가 없다");
            else if (m.AssetId != ctx.AssetId)
                ctx.Error(ArtV1IssueCode.AssetIdMismatch, ManifestFileName, $"assetId \"{m.AssetId}\"가 폴더 이름 \"{ctx.AssetId}\"와 다르다");

            if (m.Status != "ready" && m.Status != "needs_fix")
            {
                ctx.Error(ArtV1IssueCode.StatusInvalid, ManifestFileName, $"status는 ready 또는 needs_fix여야 한다 (현재: {m.Status ?? "없음"})");
            }
            else if (!m.IsReady)
            {
                if (mode == ArtV1ValidationMode.Import)
                    ctx.Error(ArtV1IssueCode.NotReady, ManifestFileName, "needs_fix 에셋이 Assets/_Project/Art/v1에 있다 — ready만 복사한다 (04 §9)");
                else
                    ctx.Info(ArtV1IssueCode.NotReady, ManifestFileName, "needs_fix — 아직 Unity로 복사하지 않는다");
            }

            if (m.IsReady && m.Deviations.Count > 0)
                ctx.Warn(ArtV1IssueCode.ReadyWithDeviations, ManifestFileName,
                    $"ready인데 deviations {m.Deviations.Count}건이 있다 — 04 §7: 합격하려면 기능·외형 차이가 없어야 한다");
            if (m.IsReady && !m.HasChecks)
                ctx.Warn(ArtV1IssueCode.ChecksMissing, ManifestFileName, "ready인데 검수 항목별 결과(checks)가 없다 (04 §7)");
        }

        static void CheckCanvasFields(ArtV1Manifest m, ArtV1Kind kind, ArtV1BodySpec body, ArtV1FlatSpec flat, Ctx ctx)
        {
            if (!m.PixelsPerUnit.HasValue)
                ctx.Add(kind == ArtV1Kind.Body ? ArtV1Severity.Error : ArtV1Severity.Warning, ArtV1IssueCode.PpuMismatch, ManifestFileName,
                    $"pixelsPerUnit이 없다 (기준 {ArtV1Spec.PixelsPerUnit})");
            else if (Math.Abs(m.PixelsPerUnit.Value - ArtV1Spec.PixelsPerUnit) > 0.001f)
                ctx.Error(ArtV1IssueCode.PpuMismatch, ManifestFileName, $"pixelsPerUnit {m.PixelsPerUnit.Value}, 기준 {ArtV1Spec.PixelsPerUnit}");

            if (m.FrameSizePx == null)
                ctx.Error(ArtV1IssueCode.FrameSizeMissing, ManifestFileName, "frameSizePx가 없다");
            else if (body != null && (m.FrameSizePx[0] != body.FrameSizePx || m.FrameSizePx[1] != body.FrameSizePx))
                ctx.Error(ArtV1IssueCode.FrameSizeSpec, ManifestFileName,
                    $"frameSizePx {m.FrameSizePx[0]}×{m.FrameSizePx[1]}, 04 기준 {body.FrameSizePx}×{body.FrameSizePx}");
            else if (flat != null && (m.FrameSizePx[0] != flat.Width || m.FrameSizePx[1] != flat.Height))
                ctx.Error(ArtV1IssueCode.FrameSizeSpec, ManifestFileName,
                    $"frameSizePx {m.FrameSizePx[0]}×{m.FrameSizePx[1]}, 04/09 기준 {flat.Width}×{flat.Height}");
            else if (body == null && flat == null && kind == ArtV1Kind.Body
                     && !((m.FrameSizePx[0] == 256 && m.FrameSizePx[1] == 256) || (m.FrameSizePx[0] == 512 && m.FrameSizePx[1] == 512)))
                ctx.Error(ArtV1IssueCode.FrameSizeSpec, ManifestFileName, "몸체 프레임은 256×256 또는 512×512여야 한다");

            if (m.PivotNormalized == null)
            {
                ctx.Error(ArtV1IssueCode.PivotMissing, ManifestFileName, kind == ArtV1Kind.Body
                    ? "pivotNormalized가 없다 (몸체 기준 [0.5, 0.125])"
                    : "pivotNormalized가 없다 — VFX/UI는 매니페스트 피벗을 쓰므로 반드시 적어야 한다 (없으면 임포터가 중앙으로 둔다)");
            }
            else if (kind == ArtV1Kind.Body
                     && (Math.Abs(m.PivotNormalized[0] - ArtV1Spec.BodyPivotX) > 0.0005f || Math.Abs(m.PivotNormalized[1] - ArtV1Spec.BodyPivotY) > 0.0005f))
            {
                ctx.Error(ArtV1IssueCode.PivotSpec, ManifestFileName,
                    $"몸체 pivotNormalized는 [0.5, 0.125]여야 한다 (현재 [{F(m.PivotNormalized[0])}, {F(m.PivotNormalized[1])}])");
            }
            else if (m.PivotNormalized[0] < 0f || m.PivotNormalized[0] > 1f || m.PivotNormalized[1] < 0f || m.PivotNormalized[1] > 1f)
            {
                ctx.Error(ArtV1IssueCode.PivotSpec, ManifestFileName, "pivotNormalized는 0..1 범위여야 한다");
            }

            if (kind != ArtV1Kind.Body) return;

            if (!m.TargetBodyHeightWorld.HasValue)
                ctx.Error(ArtV1IssueCode.TargetHeightMissing, ManifestFileName, "targetBodyHeightWorld가 없다");
            else if (body != null && Math.Abs(m.TargetBodyHeightWorld.Value - body.TargetHeightWorld) > 0.005f)
                ctx.Error(ArtV1IssueCode.TargetHeightSpec, ManifestFileName,
                    $"targetBodyHeightWorld {F(m.TargetBodyHeightWorld.Value)}, 04 표 H {F(body.TargetHeightWorld)}");

            if (!m.BodyHeightPx.HasValue || m.BodyHeightPx.Value <= 0f)
                ctx.Error(ArtV1IssueCode.BodyHeightMissing, ManifestFileName, "bodyHeightPx(idle_00 실측)가 없거나 0 이하다");
        }

        static void CheckBodyClips(ArtV1Manifest m, ArtV1BodySpec spec, Ctx ctx)
        {
            if (!m.HasClips)
            {
                ctx.Error(ArtV1IssueCode.ClipsMissing, ManifestFileName, "몸체 매니페스트에 clips가 없다");
                return;
            }

            var byName = new Dictionary<string, ArtV1ManifestClip>(StringComparer.Ordinal);
            foreach (var c in m.Clips) byName[c.Name] = c;

            foreach (var cs in spec.Clips)
            {
                if (!byName.TryGetValue(cs.Name, out var clip))
                {
                    ctx.Error(ArtV1IssueCode.ClipMissing, ManifestFileName, $"클립 {cs.Name}({cs.Frames}프레임)이 없다 (세트 {spec.Set})");
                    continue;
                }
                if (clip.Files.Count != cs.Frames)
                    ctx.Error(ArtV1IssueCode.FrameCountMismatch, ManifestFileName, $"클립 {cs.Name}: {clip.Files.Count}프레임, 기준 {cs.Frames}프레임");
                if (!clip.Fps.HasValue || !clip.Loop.HasValue)
                    ctx.Error(ArtV1IssueCode.ClipFieldMissing, ManifestFileName, $"클립 {cs.Name}: fps와 loop를 모두 적어야 한다 (04 §7)");
                if (cs.Fps > 0f && clip.Fps.HasValue && Math.Abs(clip.Fps.Value - cs.Fps) > 0.01f)
                    ctx.Warn(ArtV1IssueCode.ClipTiming, ManifestFileName, $"클립 {cs.Name}: fps {F(clip.Fps.Value)}, 기준 {F(cs.Fps)}");
                if (cs.Loop.HasValue && clip.Loop.HasValue && clip.Loop.Value != cs.Loop.Value)
                    ctx.Warn(ArtV1IssueCode.ClipTiming, ManifestFileName, $"클립 {cs.Name}: loop {clip.Loop.Value}, 기준 {cs.Loop.Value}");

                for (int k = 0; k < clip.Files.Count; k++)
                {
                    string expected = $"frames/{spec.AssetId}_{cs.Name}_right_{k:00}.png";
                    if (!string.Equals(Normalize(clip.Files[k]), expected, StringComparison.OrdinalIgnoreCase))
                        ctx.Warn(ArtV1IssueCode.FileNameConvention, clip.Files[k], $"04 §7 이름 규칙과 다르다 (기대: {expected})");
                }
            }

            foreach (var c in m.Clips)
                if (spec.Clips.All(cs => cs.Name != c.Name))
                    ctx.Warn(ArtV1IssueCode.ClipUnexpected, ManifestFileName, $"04/09 표에 없는 클립 {c.Name}");
        }

        static void CheckFrames(string assetDir, ArtV1Manifest m, ArtV1Kind kind, ArtV1BodySpec body, ArtV1FlatSpec flat,
            List<string> frames, Ctx ctx)
        {
            int expectW = m.FrameSizePx != null ? m.FrameSizePx[0] : body != null ? body.FrameSizePx : flat?.Width ?? -1;
            int expectH = m.FrameSizePx != null ? m.FrameSizePx[1] : body != null ? body.FrameSizePx : flat?.Height ?? -1;
            int footLine = body?.FootLinePx ?? (expectH == 512 ? 64 : 32);
            int safeMinX = body?.SafeMinXPx ?? (expectW == 512 ? 32 : 16);
            int safeMaxX = body?.SafeMaxXPx ?? (expectW == 512 ? 479 : 239);
            int safeTop = expectH - 1 - (expectH == 512 ? 32 : 16);

            string idle00 = m.Clips.FirstOrDefault(c => c.Name == "idle")?.Files.FirstOrDefault();
            var outsideSafe = new List<string>();
            var belowFoot = new List<string>();

            foreach (var rel in frames)
            {
                string full = Path.Combine(assetDir, rel);
                if (!File.Exists(full))
                {
                    ctx.Error(ArtV1IssueCode.FileMissing, rel, "매니페스트에 있는 파일이 없다");
                    continue;
                }

                ArtV1Png png;
                try { png = ArtV1Png.ReadFile(full); }
                catch (Exception e) when (e is InvalidDataException || e is IOException)
                {
                    ctx.Error(ArtV1IssueCode.PngUnreadable, rel, e.Message);
                    continue;
                }

                bool sizeOk = expectW <= 0 || (png.Width == expectW && png.Height == expectH);
                if (!sizeOk)
                    ctx.Error(ArtV1IssueCode.ImageSizeMismatch, rel, $"실제 {png.Width}×{png.Height}, 매니페스트/기준 {expectW}×{expectH}");

                if (kind == ArtV1Kind.Background) continue; // 반복 타일 배경은 불투명이 정상이다.

                if (!png.HasAlphaChannel)
                {
                    ctx.Error(ArtV1IssueCode.NoAlphaChannel, rel, "PNG에 알파 채널/tRNS가 없다 — 04 §7: 배경 없음은 실제 알파 0이어야 한다");
                    continue;
                }
                if (png.Rgba == null)
                {
                    ctx.Warn(ArtV1IssueCode.PngDecodeUnsupported, rel, png.DecodeError + " — 알파 검수를 건너뛰었다");
                    continue;
                }

                var s = ArtV1AlphaStats.Analyze(png);
                var opaqueSeverity = kind == ArtV1Kind.Ui ? ArtV1Severity.Warning : ArtV1Severity.Error;
                if (!s.HasContent)
                {
                    ctx.Error(ArtV1IssueCode.EmptyFrame, rel, "보이는 픽셀이 없다(모두 투명)");
                    continue;
                }
                if (s.TransparentPixels == 0)
                {
                    ctx.Add(opaqueSeverity, ArtV1IssueCode.NoTransparentPixels, rel,
                        "투명 픽셀이 하나도 없다 — 배경이 그려졌거나 캔버스를 꽉 채웠다. 흰 배경/바둑판을 그려 넣은 경우일 수 있으니 이미지를 직접 확인할 것");
                }
                if (s.BorderOpaqueRatio >= BorderOpaqueWarnRatio)
                {
                    ctx.Warn(ArtV1IssueCode.OpaqueCanvasBorder, rel,
                        $"캔버스 가장자리 {P(s.BorderOpaqueRatio)}가 불투명 — 여백 없는 캔버스이거나 배경이 남아 있을 수 있다");
                    if (s.BorderLightNeutralRatio >= BorderLightNeutralWarnRatio)
                        ctx.Warn(ArtV1IssueCode.PossiblePaintedBackground, rel,
                            $"가장자리 {P(s.BorderLightNeutralRatio)}가 불투명한 흰색/밝은 회색 — 바둑판·흰 배경을 그려 넣었을 가능성(휴리스틱). 알파만으로 확정할 수 없으므로 눈으로 확인할 것");
                }

                // 캔버스 크기가 틀리면 접지선·안전 범위·본체 높이 좌표가 의미 없으므로 크기 에러만 남긴다.
                if (!sizeOk || kind != ArtV1Kind.Body || (body == null && expectW != expectH)) continue;

                if (s.MinX < safeMinX || s.MaxX > safeMaxX || s.TopYFromBottom > safeTop)
                    outsideSafe.Add(Path.GetFileName(rel));
                if (s.BottomYFromBottom < footLine - BodyHeightTolerancePx)
                    belowFoot.Add(Path.GetFileName(rel));

                if (idle00 != null && Normalize(rel) == Normalize(idle00) && m.BodyHeightPx.HasValue)
                {
                    int measured = s.TopYFromBottom + 1 - footLine;
                    if (Math.Abs(measured - m.BodyHeightPx.Value) > BodyHeightTolerancePx)
                        ctx.Error(ArtV1IssueCode.BodyHeightMismatch, rel,
                            $"idle_00 실측 본체 높이 {measured}px(접지선 y={footLine}부터, 알파>{ArtV1AlphaStats.ContentAlphaThreshold}), 매니페스트 bodyHeightPx {F(m.BodyHeightPx.Value)} — visualScale 계산이 틀어진다");
                }
            }

            if (outsideSafe.Count > 0)
                ctx.Warn(ArtV1IssueCode.OutsideSafeArea, null,
                    $"본체 픽셀이 04 §7 안전 범위(x {safeMinX}..{safeMaxX}, 최고 y {safeTop})를 벗어난 프레임 {outsideSafe.Count}장: {Sample(outsideSafe)}");
            if (belowFoot.Count > 0)
                ctx.Warn(ArtV1IssueCode.BelowFootLine, null,
                    $"접지선 y={footLine} 아래에 픽셀이 있는 프레임 {belowFoot.Count}장 — 그림자를 몸체에 그렸거나 피벗이 어긋났을 수 있다: {Sample(belowFoot)}");
        }

        static void CheckAnchors(ArtV1Manifest m, ArtV1BodySpec body, List<string> frames, Ctx ctx)
        {
            bool isPlayer = body != null && body.Set == "A";
            if (!m.HasAnchorsByFrame)
            {
                if (isPlayer)
                    ctx.Error(ArtV1IssueCode.AnchorsMissing, ManifestFileName, "플레이어는 프레임마다 손잡이 anchor가 필요하다 (04 §6)");
                else if (m.ResolveKind() == ArtV1Kind.Body)
                    ctx.Warn(ArtV1IssueCode.AnchorsMissing, ManifestFileName, "anchorsByFrame이 없다 — 무기 없는 개체는 빈 객체로 적는다 (04 §7)");
            }
            else if (isPlayer)
            {
                var missing = frames.Where(f => !HasAnchorEntry(m, f)).Select(Path.GetFileName).ToList();
                if (missing.Count > 0)
                    ctx.Error(ArtV1IssueCode.AnchorsMissing, ManifestFileName, $"anchor 없는 플레이어 프레임 {missing.Count}장: {Sample(missing)}");
            }

            if (m.FrameSizePx == null) return;
            foreach (var frame in m.AnchorsByFrame)
                foreach (var a in frame.Value)
                    if (a.Value[0] < 0f || a.Value[1] < 0f || a.Value[0] >= m.FrameSizePx[0] || a.Value[1] >= m.FrameSizePx[1])
                        ctx.Error(ArtV1IssueCode.AnchorOutOfCanvas, frame.Key, $"anchor {a.Key} ({F(a.Value[0])}, {F(a.Value[1])})가 캔버스 밖이다");
        }

        static bool HasAnchorEntry(ArtV1Manifest m, string frame)
        {
            string n = Normalize(frame);
            string name = Path.GetFileName(n);
            return m.AnchorsByFrame.Keys.Any(k => Normalize(k) == n || string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        }

        static void CheckUnreferencedFiles(string assetDir, List<string> frames, Ctx ctx)
        {
            var referenced = new HashSet<string>(frames.Select(Normalize), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(assetDir, "*.png", SearchOption.AllDirectories))
            {
                string rel = Normalize(file.Substring(assetDir.Length).TrimStart('/', '\\'));
                if (rel.StartsWith("reference/", StringComparison.OrdinalIgnoreCase)) continue; // 참조 원본 보관 위치
                if (string.Equals(rel, PreviewFileName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!referenced.Contains(rel))
                    ctx.Warn(ArtV1IssueCode.UnreferencedFile, rel, "매니페스트 어디에도 없는 PNG — 누락된 프레임 목록이거나 남은 파일");
            }
        }

        static string Normalize(string path) => (path ?? "").Replace('\\', '/').TrimStart('.', '/');
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string P(float ratio) => (ratio * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string Sample(List<string> items) => string.Join(", ", items.Take(5)) + (items.Count > 5 ? " …" : "");

        sealed class Ctx
        {
            readonly ArtV1Report report;
            public readonly string AssetId;

            public Ctx(ArtV1Report report, string assetId)
            {
                this.report = report;
                AssetId = assetId;
            }

            public void Add(ArtV1Severity severity, string code, string path, string message) =>
                report.Issues.Add(new ArtV1Issue(severity, code, AssetId, path, message));

            public void Error(string code, string path, string message) => Add(ArtV1Severity.Error, code, path, message);
            public void Warn(string code, string path, string message) => Add(ArtV1Severity.Warning, code, path, message);
            public void Info(string code, string path, string message) => Add(ArtV1Severity.Info, code, path, message);
        }
    }
}
