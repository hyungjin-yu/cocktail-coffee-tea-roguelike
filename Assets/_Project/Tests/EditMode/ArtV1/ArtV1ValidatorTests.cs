using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CCT.Editor;
using NUnit.Framework;

namespace CCT.Tests.ArtV1
{
    /// <summary>
    /// 04 §7 매니페스트/프레임 계약 검수. 각 테스트는 정상 ready 에셋에서 한 가지만 깨뜨린다.
    /// 파일은 시스템 임시 폴더에만 만들고 프로젝트 에셋은 건드리지 않는다.
    /// </summary>
    public class ArtV1ValidatorTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ArtV1ValidatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static string Codes(ArtV1Report r) => string.Join("\n", r.Issues.Select(i => i.ToString()));

        [TestCase("lime_wedge")]
        [TestCase("lemon_brute")]
        [TestCase("martini_spirit")]
        [TestCase("leaf_warden")]
        [TestCase("grinder_golem")]
        [TestCase("difficult_customer")]
        [TestCase("bartender")]
        public void ContractCompliantReadyBody_HasNoErrorsOrWarnings_AndIsImportable(string assetId)
        {
            string dir = ArtV1TestAssets.WriteBody(root, assetId);

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Import);

            Assert.AreEqual(0, report.ErrorCount, Codes(report));
            Assert.AreEqual(0, report.WarningCount, Codes(report));
            var summary = report.Assets.Single();
            Assert.IsTrue(summary.Importable);
            Assert.AreEqual(ArtV1Spec.Bodies[assetId].TotalFrames, summary.FramesListed);
            Assert.AreEqual(ArtV1Spec.Bodies[assetId].TotalFrames, summary.FramesExpected);
            Assert.IsTrue(summary.VisualScale.HasValue);
        }

        [Test]
        public void GrinderVisualScale_UsesHeightFormula_NotRadiusOrLegacyIconScale()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "grinder_golem");
            var summary = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source).Assets.Single();

            // 2.40 / (300 / 128) — 기존 radius*2*iconVisualScale(=2.2)과 무관
            Assert.AreEqual(2.40f / (ArtV1TestAssets.BossBodyHeightPx / 128f), summary.VisualScale.Value, 1e-4f);
        }

        [Test]
        public void NeedsFix_IsErrorInImportMode_ButOnlyInfoInSourceMode()
        {
            var m = ArtV1TestAssets.BodyManifest("teabag_imp");
            m["status"] = "needs_fix";
            string dir = ArtV1TestAssets.WriteBody(root, "teabag_imp", m);

            var import = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Import);
            var source = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            Assert.IsTrue(import.Issues.Any(i => i.Code == ArtV1IssueCode.NotReady && i.Severity == ArtV1Severity.Error));
            Assert.IsFalse(import.Assets.Single().Importable);
            Assert.IsTrue(source.Issues.Any(i => i.Code == ArtV1IssueCode.NotReady && i.Severity == ArtV1Severity.Info));
            Assert.AreEqual(0, source.ErrorCount, Codes(source));
            Assert.IsFalse(source.Assets.Single().Importable, "needs_fix는 오류가 없어도 적용 가능으로 표시하지 않는다");
        }

        [Test]
        public void UnknownStatus_IsRejected()
        {
            var m = ArtV1TestAssets.BodyManifest("teabag_imp");
            m["status"] = "done";
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "teabag_imp", m), ArtV1ValidationMode.Source);
            Assert.IsTrue(report.Has(ArtV1IssueCode.StatusInvalid), Codes(report));
        }

        [Test]
        public void MissingFrameFile_IsError()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "lime_wedge");
            File.Delete(Path.Combine(dir, "frames", "lime_wedge_move_right_03.png"));

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            var issue = report.Issues.Single(i => i.Code == ArtV1IssueCode.FileMissing);
            Assert.AreEqual(ArtV1Severity.Error, issue.Severity);
            StringAssert.Contains("lime_wedge_move_right_03.png", issue.Path);
        }

        [Test]
        public void ClipFrameCountDifferentFromTable_IsError()
        {
            var m = ArtV1TestAssets.BodyManifest("lime_wedge");
            ArtV1TestAssets.ClipFiles(m, "idle").RemoveAt(3);
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "lime_wedge", m), ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Issues.Any(i => i.Code == ArtV1IssueCode.FrameCountMismatch && i.Message.Contains("idle")), Codes(report));
        }

        [Test]
        public void MissingBossSpecificClip_IsError()
        {
            var m = ArtV1TestAssets.BodyManifest("difficult_customer");
            ((Dictionary<string, object>)m["clips"]).Remove("reject"); // 09 §1 추가 클립
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "difficult_customer", m), ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Issues.Any(i => i.Code == ArtV1IssueCode.ClipMissing && i.Message.Contains("reject")), Codes(report));
        }

        [Test]
        public void ActualImageSizeDifferentFromManifest_IsError()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "olive_bomber", null,
                file => file.EndsWith("hurt_right_00.png") ? ArtV1TestImage.Body(128, 16, 60) : null);

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            var issue = report.Issues.Single(i => i.Code == ArtV1IssueCode.ImageSizeMismatch);
            StringAssert.Contains("128×128", issue.Message);
        }

        [Test]
        public void WrongSizedIdle00_ReportsOnlySize_NotBogusGeometry()
        {
            // 현재 grinder_golem.png(128²)를 512² 보스 규격으로 검사할 때 접지선/높이 좌표가 엉터리로 나오던 문제 회귀 방지.
            string dir = ArtV1TestAssets.WriteBody(root, "grinder_golem", null,
                file => file.EndsWith("idle_right_00.png") ? ArtV1TestImage.Body(128, 0, 128) : null);

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Has(ArtV1IssueCode.ImageSizeMismatch), Codes(report));
            Assert.IsFalse(report.Has(ArtV1IssueCode.BodyHeightMismatch), Codes(report));
            Assert.IsFalse(report.Has(ArtV1IssueCode.BelowFootLine), Codes(report));
            Assert.IsFalse(report.Has(ArtV1IssueCode.OutsideSafeArea), Codes(report));
        }

        [Test]
        public void KnownAssetWithoutManifest_StillReportsExpectedFrameCount()
        {
            Directory.CreateDirectory(Path.Combine(root, "bartender", "reference"));
            var summary = ArtV1Validator.ValidateAssetDirectory(Path.Combine(root, "bartender"), ArtV1ValidationMode.Source).Assets.Single();
            Assert.AreEqual(23, summary.FramesExpected);
            Assert.IsFalse(summary.Importable);
        }

        [Test]
        public void WrongFrameSizeForBoss_IsSpecError()
        {
            var m = ArtV1TestAssets.BodyManifest("grinder_golem");
            m["frameSizePx"] = new List<object> { 128.0, 128.0 };
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "grinder_golem", m), ArtV1ValidationMode.Source);
            Assert.IsTrue(report.Has(ArtV1IssueCode.FrameSizeSpec), Codes(report));
        }

        [Test]
        public void RgbFramesWithoutAlpha_AreErrors()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "sugar_cube_bomber");
            string idle = Path.Combine(dir, "frames", "sugar_cube_bomber_idle_right_00.png");
            ArtV1TestPng.Write(idle, ArtV1TestPng.EncodeRgb(ArtV1TestImage.Body(256, 32, 120)));

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Issues.Any(i => i.Code == ArtV1IssueCode.NoAlphaChannel && i.Path.EndsWith("idle_right_00.png")), Codes(report));
        }

        [Test]
        public void PaintedCheckerboardWithFullAlpha_IsFlaggedAsOpaque_WithHeuristicWarning()
        {
            // 현재 grinder_golem.png와 같은 경우: RGBA인데 모든 픽셀 알파 255, 흰/회색 바둑판이 그림으로 들어감.
            string dir = ArtV1TestAssets.WriteBody(root, "grinder_golem", null,
                file => ArtV1TestImage.Body(512, 64, ArtV1TestAssets.BossBodyHeightPx).PaintCheckerboard(0, 0, 511, 511));

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Issues.Any(i => i.Code == ArtV1IssueCode.NoTransparentPixels && i.Severity == ArtV1Severity.Error), Codes(report));
            var hint = report.Issues.First(i => i.Code == ArtV1IssueCode.PossiblePaintedBackground);
            Assert.AreEqual(ArtV1Severity.Warning, hint.Severity, "그림으로 된 바둑판은 추정일 뿐이라 경고로만 낸다");
            StringAssert.Contains("눈으로 확인", hint.Message);
        }

        [Test]
        public void CheckerboardPaintedOnlyInsideTransparentCanvas_IsNotDetected_KnownLimitation()
        {
            // 알파는 정상(가장자리 투명)인데 본체 영역 안쪽에 바둑판이 그려진 경우 — 알파/가장자리 휴리스틱으로는
            // 잡을 수 없다. 이 테스트는 "알파만으로 바둑판을 확실히 판별할 수 없다"는 한계를 고정한다.
            string dir = ArtV1TestAssets.WriteBody(root, "teabag_imp", null,
                file => ArtV1TestImage.Body(256, 32, ArtV1TestAssets.NormalBodyHeightPx).PaintCheckerboard(100, 60, 150, 140));

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            Assert.IsFalse(report.Has(ArtV1IssueCode.PossiblePaintedBackground));
            Assert.IsFalse(report.Has(ArtV1IssueCode.NoTransparentPixels));
            Assert.AreEqual(0, report.ErrorCount, Codes(report));
        }

        [Test]
        public void FullyTransparentFrame_IsEmptyFrameError()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "lime_wedge", null,
                file => file.EndsWith("defeat_right_03.png") ? new ArtV1TestImage(256, 256) : null);
            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);
            Assert.IsTrue(report.Has(ArtV1IssueCode.EmptyFrame), Codes(report));
        }

        [Test]
        public void BodyHeightPxThatDoesNotMatchIdle00_IsError()
        {
            var m = ArtV1TestAssets.BodyManifest("kettle_cannon");
            m["bodyHeightPx"] = 150.0; // 실측 120
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "kettle_cannon", m), ArtV1ValidationMode.Source);

            var issue = report.Issues.Single(i => i.Code == ArtV1IssueCode.BodyHeightMismatch);
            StringAssert.Contains("120px", issue.Message);
        }

        [Test]
        public void BodyPivotOtherThanFootPivot_PpuOtherThan128_AndWrongTargetHeight_AreErrors()
        {
            var m = ArtV1TestAssets.BodyManifest("shaker_golem");
            m["pivotNormalized"] = new List<object> { 0.5, 0.5 };
            m["pixelsPerUnit"] = 1024.0;
            m["targetBodyHeightWorld"] = 2.5;
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "shaker_golem", m), ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Has(ArtV1IssueCode.PivotSpec), Codes(report));
            Assert.IsTrue(report.Has(ArtV1IssueCode.PpuMismatch), Codes(report));
            Assert.IsTrue(report.Has(ArtV1IssueCode.TargetHeightSpec), Codes(report));
        }

        [Test]
        public void ShadowOrPixelsBelowFootLine_AreWarned()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "teabag_imp", null,
                file => ArtV1TestImage.Body(256, 32, 120).Fill(100, 10, 156, 20, 0, 0, 0, 200));
            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);
            Assert.IsTrue(report.Has(ArtV1IssueCode.BelowFootLine), Codes(report));
        }

        [Test]
        public void LeftFacingFramesAndUnreferencedPngs_AreReported()
        {
            var m = ArtV1TestAssets.BodyManifest("lime_wedge");
            ArtV1TestAssets.ClipFiles(m, "hurt")[0] = "frames/lime_wedge_hurt_left_00.png";
            string dir = ArtV1TestAssets.WriteBody(root, "lime_wedge", m);
            ArtV1TestPng.Write(Path.Combine(dir, "frames", "stray.png"), ArtV1TestPng.EncodeRgba(ArtV1TestImage.Body(256, 32, 120)));
            ArtV1TestPng.Write(Path.Combine(dir, "reference", "source.png"), ArtV1TestPng.EncodeRgb(new ArtV1TestImage(8, 8)));

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Has(ArtV1IssueCode.LeftFacingFile), Codes(report));
            Assert.IsTrue(report.Issues.Any(i => i.Code == ArtV1IssueCode.UnreferencedFile && i.Path == "frames/stray.png"), Codes(report));
            Assert.IsFalse(report.Issues.Any(i => i.Code == ArtV1IssueCode.UnreferencedFile && i.Path.StartsWith("reference/")),
                "reference/는 생성 원본 보관 위치라 검사하지 않는다");
        }

        [Test]
        public void PlayerWithoutPerFrameHandAnchors_IsError()
        {
            var m = ArtV1TestAssets.BodyManifest("tea_sommelier");
            ((Dictionary<string, object>)m["anchorsByFrame"]).Remove("tea_sommelier_melee_right_02.png");
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "tea_sommelier", m), ArtV1ValidationMode.Source);

            var issue = report.Issues.Single(i => i.Code == ArtV1IssueCode.AnchorsMissing);
            Assert.AreEqual(ArtV1Severity.Error, issue.Severity);
            StringAssert.Contains("melee_right_02", issue.Message);
        }

        [Test]
        public void ReadyWithDeviationsOrWithoutChecks_IsWarned()
        {
            var m = ArtV1TestAssets.BodyManifest("olive_bomber");
            m["deviations"] = new List<object> { "눈 위치 2px 차이" };
            m.Remove("checks");
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "olive_bomber", m), ArtV1ValidationMode.Source);

            Assert.IsTrue(report.Has(ArtV1IssueCode.ReadyWithDeviations), Codes(report));
            Assert.IsTrue(report.Has(ArtV1IssueCode.ChecksMissing), Codes(report));
        }

        [Test]
        public void ManifestProblems_MissingMalformedAndAssetIdMismatch()
        {
            Directory.CreateDirectory(Path.Combine(root, "no_manifest"));
            string bad = Path.Combine(root, "broken");
            Directory.CreateDirectory(bad);
            File.WriteAllText(Path.Combine(bad, "manifest.json"), "{ \"assetId\": ");
            var m = ArtV1TestAssets.BodyManifest("lime_wedge");
            m["assetId"] = "lime";

            var missing = ArtV1Validator.ValidateAssetDirectory(Path.Combine(root, "no_manifest"), ArtV1ValidationMode.Import);
            var missingSource = ArtV1Validator.ValidateAssetDirectory(Path.Combine(root, "no_manifest"), ArtV1ValidationMode.Source);
            var broken = ArtV1Validator.ValidateAssetDirectory(bad, ArtV1ValidationMode.Source);
            var mismatch = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteBody(root, "lime_wedge", m), ArtV1ValidationMode.Source);

            Assert.AreEqual(ArtV1Severity.Error, missing.Issues.Single(i => i.Code == ArtV1IssueCode.ManifestMissing).Severity);
            Assert.AreEqual(ArtV1Severity.Warning, missingSource.Issues.Single(i => i.Code == ArtV1IssueCode.ManifestMissing).Severity);
            Assert.IsTrue(broken.Has(ArtV1IssueCode.ManifestParseError), Codes(broken));
            Assert.IsTrue(mismatch.Has(ArtV1IssueCode.AssetIdMismatch), Codes(mismatch));
        }

        [Test]
        public void CoffeeCommonMob_IsNowInSpec_ValidatedAsRSet()
        {
            var m = ArtV1TestAssets.BodyManifest("lime_wedge");
            m["assetId"] = "espresso_bug";
            m["kind"] = "body";
            string dir = Path.Combine(root, "espresso_bug");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), ArtV1TestAssets.ToJson(m));
            foreach (var f in ArtV1TestAssets.AllFrameFiles(m))
                ArtV1TestPng.Write(Path.Combine(dir, f), ArtV1TestPng.EncodeRgba(ArtV1TestImage.Body(256, 32, 120)));

            var report = ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source);

            // 2026-10-04: 04 최신 계약(19종)에서 espresso_bug는 R 세트·H 0.90으로 확정 — 범위 미정/미지 ID가 아니다.
            Assert.IsFalse(report.Has(ArtV1IssueCode.ScopeUndecided), Codes(report));
            Assert.IsFalse(report.Has(ArtV1IssueCode.UnknownAsset), Codes(report));
            Assert.AreEqual(15, report.Assets.Single().FramesExpected);
        }

        [Test]
        public void VfxManifest_UsesDeclaredPivot_AndRequiresOne()
        {
            string ok = ArtV1TestAssets.WriteFlat(root, "hit_spark", ArtV1TestAssets.FlatManifest("hit_spark", new List<object> { 0.5, 0.5 }));
            string noPivot = ArtV1TestAssets.WriteFlat(root, "summon_ring", ArtV1TestAssets.FlatManifest("summon_ring", null));

            var okReport = ArtV1Validator.ValidateAssetDirectory(ok, ArtV1ValidationMode.Import);
            var noPivotReport = ArtV1Validator.ValidateAssetDirectory(noPivot, ArtV1ValidationMode.Import);

            Assert.AreEqual(0, okReport.ErrorCount, Codes(okReport));
            Assert.AreEqual(4, okReport.Assets.Single().FramesExpected);
            Assert.IsTrue(noPivotReport.Has(ArtV1IssueCode.PivotMissing), Codes(noPivotReport));
            Assert.IsFalse(noPivotReport.Has(ArtV1IssueCode.PivotSpec), "VFX에 몸체 피벗을 강제하지 않는다");
        }

        [Test]
        public void VfxFrameCountDifferentFromTable_IsError()
        {
            var m = ArtV1TestAssets.FlatManifest("hit_spark", new List<object> { 0.5, 0.5 });
            ArtV1TestAssets.ClipFiles(m, "play").RemoveAt(0);
            var report = ArtV1Validator.ValidateAssetDirectory(ArtV1TestAssets.WriteFlat(root, "hit_spark", m), ArtV1ValidationMode.Source);
            Assert.IsTrue(report.Has(ArtV1IssueCode.FrameCountMismatch), Codes(report));
        }

        [Test]
        public void PreviewPngInsideImportFolder_IsWarned()
        {
            string dir = ArtV1TestAssets.WriteBody(root, "lime_wedge");
            ArtV1TestPng.Write(Path.Combine(dir, "preview.png"), ArtV1TestPng.EncodeRgba(new ArtV1TestImage(8, 8)));

            Assert.IsTrue(ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Import).Has(ArtV1IssueCode.PreviewInImport));
            Assert.IsFalse(ArtV1Validator.ValidateAssetDirectory(dir, ArtV1ValidationMode.Source).Has(ArtV1IssueCode.PreviewInImport));
        }

        [Test]
        public void ValidateRoot_ListsMissingSpecAssets_IncludingLemonBrute_AndRendersMarkdown()
        {
            ArtV1TestAssets.WriteBody(root, "lime_wedge");

            var report = ArtV1Validator.ValidateRoot(root, ArtV1ValidationMode.Source);

            Assert.AreEqual(1, report.Assets.Count);
            var missing = report.Issues.First(i => i.Code == ArtV1IssueCode.MissingAsset);
            StringAssert.Contains("lemon_brute", missing.Message);
            StringAssert.DoesNotContain("lime_wedge", missing.Message);
            string md = report.ToMarkdown();
            StringAssert.Contains("| lime_wedge |", md);
            StringAssert.Contains("알파만으로 확실히 판별할 수 없으므로", md);
        }

        [Test]
        public void ValidateRoot_OnMissingFolder_DoesNotThrow()
        {
            var report = ArtV1Validator.ValidateRoot(Path.Combine(root, "does_not_exist"), ArtV1ValidationMode.Import);
            Assert.AreEqual(0, report.Assets.Count);
            Assert.AreEqual(0, report.ErrorCount);
        }
    }
}
