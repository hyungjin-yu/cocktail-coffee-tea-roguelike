using System.Collections.Generic;
using CCT.Editor;
using NUnit.Framework;

namespace CCT.Tests.ArtV1
{
    /// <summary>
    /// Art/v1 임포트 결정 규칙(04 §9). TextureImporter를 만들지 않고 결정 값만 검사한다 —
    /// 실제 임포트(ArtV1ImportSettings)는 에셋을 Art/v1에 둔 뒤 Unity 안에서 확인해야 한다.
    /// </summary>
    public class ArtV1ImportRulesTests
    {
        static ArtV1Manifest Parse(Dictionary<string, object> m)
        {
            var manifest = ArtV1Manifest.Parse(ArtV1TestAssets.ToJson(m), out string error);
            Assert.IsNull(error);
            return manifest;
        }

        [TestCase("Assets/_Project/Resources/Icons/Enemies/grinder_golem.png")]
        [TestCase("Assets/_Project/Resources/Icons/Weapons/rapier.png")]
        [TestCase("ArtSource/v1/bartender/reference/generated_v01/bartender_idle_right_00_source.png")]
        [TestCase("Assets/_Project/Art/v1/lime_wedge/manifest.json")]
        [TestCase("Assets/_Project/Art/v2/lime_wedge/frames/a.png")]
        public void PathsOutsideArtV1_AreNeverTouched(string path)
        {
            Assert.IsFalse(ArtV1ImportRules.IsArtV1Png(path));
            Assert.IsFalse(ArtV1ImportRules.Resolve(path, null).Applies, "기존 Resources/Icons 설정(IconImportSettings)은 그대로 둔다");
        }

        [Test]
        public void ReadyBody_Gets128PpuAndFootPivot_EvenIfManifestSaysOtherwise()
        {
            var m = ArtV1TestAssets.BodyManifest("grinder_golem");
            m["pivotNormalized"] = new List<object> { 0.5, 0.5 };

            var plan = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/grinder_golem/frames/grinder_golem_idle_right_00.png", Parse(m));

            Assert.IsTrue(plan.Applies);
            Assert.IsTrue(plan.IsReady);
            Assert.AreEqual("grinder_golem", plan.AssetId);
            Assert.AreEqual(ArtV1Kind.Body, plan.Kind);
            Assert.AreEqual(128, plan.PixelsPerUnit);
            Assert.AreEqual(0.5f, plan.PivotX);
            Assert.AreEqual(0.125f, plan.PivotY);
            Assert.AreEqual(ArtV1PivotSource.BodySpec, plan.PivotSource);
            Assert.IsEmpty(plan.Errors);
            Assert.That(plan.Warnings, Has.Some.Contains("0.125"));
        }

        [Test]
        public void Vfx_UsesManifestPivot_NotBodyPivot()
        {
            var m = ArtV1TestAssets.FlatManifest("rage_overlay", new List<object> { 0.5, 0.125 });
            var plan = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/rage_overlay/frames/rage_overlay_play_right_00.png", Parse(m));
            Assert.AreEqual(ArtV1PivotSource.Manifest, plan.PivotSource);
            Assert.AreEqual(0.125f, plan.PivotY);

            var ring = ArtV1TestAssets.FlatManifest("warning_ring", new List<object> { 0.5, 0.5 });
            var ringPlan = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/warning_ring/frames/warning_ring_play_right_00.png", Parse(ring));
            Assert.AreEqual(ArtV1Kind.Vfx, ringPlan.Kind);
            Assert.AreEqual(0.5f, ringPlan.PivotY);
            Assert.AreEqual(128, ringPlan.PixelsPerUnit);
            Assert.IsEmpty(ringPlan.Errors);
        }

        [Test]
        public void VfxWithoutPivot_FallsBackToCenter_AndReportsError()
        {
            var m = ArtV1TestAssets.FlatManifest("hit_spark", null);
            var plan = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/hit_spark/frames/hit_spark_play_right_00.png", Parse(m));

            Assert.AreEqual(ArtV1PivotSource.CenterFallback, plan.PivotSource);
            Assert.AreEqual(0.5f, plan.PivotX);
            Assert.AreEqual(0.5f, plan.PivotY);
            Assert.That(plan.Errors, Has.Some.Contains("pivotNormalized"));
        }

        [Test]
        public void UiBorder_IsTakenFromManifest_OnlyForUi()
        {
            var ui = ArtV1TestAssets.FlatManifest("ui_panel", new List<object> { 0.5, 0.5 });
            ui["spriteBorderPx"] = new List<object> { 16.0, 16.0, 16.0, 16.0 };
            var vfx = ArtV1TestAssets.FlatManifest("hit_spark", new List<object> { 0.5, 0.5 });
            vfx["spriteBorderPx"] = new List<object> { 4.0, 4.0, 4.0, 4.0 };

            var uiPlan = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/ui_panel/frames/ui_panel_play_right_00.png", Parse(ui));
            var vfxPlan = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/hit_spark/frames/hit_spark_play_right_00.png", Parse(vfx));

            CollectionAssert.AreEqual(new[] { 16, 16, 16, 16 }, uiPlan.BorderPx);
            Assert.IsNull(vfxPlan.BorderPx);
            Assert.IsNotEmpty(vfxPlan.Warnings);
        }

        [Test]
        public void NeedsFixOrMissingManifest_IsReportedAsError_NotReady()
        {
            var m = ArtV1TestAssets.BodyManifest("bartender");
            m["status"] = "needs_fix";

            var needsFix = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png", Parse(m));
            var noManifest = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png", null);
            var brokenManifest = ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png", null, "위치 3");

            Assert.IsFalse(needsFix.IsReady);
            Assert.That(needsFix.Errors, Has.Some.Contains("needs_fix"));
            Assert.IsFalse(noManifest.IsReady);
            Assert.That(noManifest.Errors, Has.Some.Contains("manifest.json"));
            Assert.AreEqual(ArtV1PivotSource.BodySpec, noManifest.PivotSource, "04 몸체 ID는 매니페스트가 없어도 발 피벗");
            Assert.That(brokenManifest.Errors, Has.Some.Contains("위치 3"));
        }

        [Test]
        public void ManifestPathAndAssetId_AreDerivedFromFolder()
        {
            const string png = "Assets\\_Project\\Art\\v1\\lemon_brute\\frames\\lemon_brute_idle_right_00.png";
            Assert.IsTrue(ArtV1ImportRules.IsArtV1Png(png));
            Assert.AreEqual("lemon_brute", ArtV1ImportRules.AssetIdFromPath(png));
            Assert.AreEqual("Assets/_Project/Art/v1/lemon_brute/manifest.json", ArtV1ImportRules.ManifestPathFor(png));
            Assert.IsNull(ArtV1ImportRules.AssetIdFromPath("Assets/_Project/Art/v1/loose.png"));
            Assert.IsNotEmpty(ArtV1ImportRules.Resolve("Assets/_Project/Art/v1/loose.png", null).Errors);
        }
    }
}
