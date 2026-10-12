using System;
using System.Collections.Generic;
using System.IO;
using CCT.Editor;
using NUnit.Framework;

namespace CCT.Tests.ArtV1
{
    /// <summary>매니페스트 JSON·PNG 디코딩·04/09 표 자체의 정확성.</summary>
    public class ArtV1ParsingTests
    {
        [Test]
        public void Json_ParsesNestedValuesEscapesAndBom()
        {
            var root = (Dictionary<string, object>)ArtV1Json.Parse("﻿{\"a\":[1,-2.5e1,true,null],\"s\":\"x\\\"\\u00e9\\n\",\"o\":{}}");
            var a = (List<object>)root["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(-25.0, a[1]);
            Assert.AreEqual(true, a[2]);
            Assert.IsNull(a[3]);
            Assert.AreEqual("x\"é\n", root["s"]);
            Assert.IsEmpty((Dictionary<string, object>)root["o"]);
        }

        [TestCase("{\"a\":1,}")]
        [TestCase("{\"a\" 1}")]
        [TestCase("[1,2")]
        [TestCase("{\"a\":1} x")]
        [TestCase("")]
        public void Json_RejectsMalformedInput(string text)
        {
            Assert.IsFalse(ArtV1Json.TryParse(text, out _, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void Manifest_ReadsClipsAnchorsAndReportsWrongFieldTypes()
        {
            var m = ArtV1Manifest.Parse(
                "{\"specVersion\":\"1.0\",\"assetId\":\"bartender\",\"status\":\"ready\",\"frameSizePx\":[256,256]," +
                "\"pixelsPerUnit\":\"128\",\"pivotNormalized\":[0.5,0.125]," +
                "\"clips\":{\"idle\":{\"fps\":6,\"loop\":true,\"files\":[\"frames/a.png\",\"frames/b.png\"]}}," +
                "\"anchorsByFrame\":{\"a.png\":{\"hand\":[10,20]}}}", out string error);

            Assert.IsNull(error);
            Assert.AreEqual("bartender", m.AssetId);
            Assert.IsTrue(m.IsReady);
            CollectionAssert.AreEqual(new[] { 256, 256 }, m.FrameSizePx);
            Assert.AreEqual(1, m.Clips.Count);
            Assert.AreEqual(6f, m.Clips[0].Fps);
            Assert.AreEqual(true, m.Clips[0].Loop);
            Assert.AreEqual(2, m.Clips[0].Files.Count);
            CollectionAssert.AreEqual(new[] { 10f, 20f }, m.AnchorsByFrame["a.png"]["hand"]);
            Assert.IsNull(m.PixelsPerUnit, "문자열 \"128\"은 숫자로 받아들이지 않는다");
            Assert.That(m.ParseErrors, Has.Some.Contains("pixelsPerUnit"));
            Assert.AreEqual(ArtV1Kind.Body, m.ResolveKind());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Png_DecodesEveryFilterTypeExactly(int filter)
        {
            var img = new ArtV1TestImage(67, 45); // 홀수 크기 + 충분한 픽셀 수로 Paeth 동점 분기까지 밟게 한다
            var rnd = new Random(1234 + filter);
            rnd.NextBytes(img.Rgba);

            var png = ArtV1Png.Read(ArtV1TestPng.EncodeRgba(img, filter));

            Assert.AreEqual(67, png.Width);
            Assert.AreEqual(45, png.Height);
            Assert.AreEqual(ArtV1Png.ColorRgba, png.ColorType);
            Assert.IsTrue(png.HasAlphaChannel);
            CollectionAssert.AreEqual(img.Rgba, png.Rgba);
        }

        [Test]
        public void Png_RgbWithoutAlpha_IsReportedAsNoAlphaChannelAndDecodedOpaque()
        {
            var img = new ArtV1TestImage(4, 4).Fill(0, 0, 3, 3, 10, 20, 30, 0);
            var png = ArtV1Png.Read(ArtV1TestPng.EncodeRgb(img, 4));

            Assert.AreEqual(ArtV1Png.ColorRgb, png.ColorType);
            Assert.IsFalse(png.HasAlphaChannel);
            Assert.AreEqual(255, png.AlphaAt(0, 0), "알파 없는 PNG는 불투명으로 읽힌다 — 원본 알파가 사라졌다는 뜻");
            Assert.AreEqual(10, png.Rgba[0]);
        }

        [Test]
        public void Png_RejectsNonPngBytes()
        {
            Assert.Throws<InvalidDataException>(() => ArtV1Png.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
        }

        [Test]
        public void AlphaStats_UsesBottomLeftOriginForBodyBounds()
        {
            var img = ArtV1TestImage.Body(256, 32, 120);
            var s = ArtV1AlphaStats.Analyze(ArtV1Png.Read(ArtV1TestPng.EncodeRgba(img)));

            Assert.IsTrue(s.HasContent);
            Assert.AreEqual(32, s.BottomYFromBottom);
            Assert.AreEqual(151, s.TopYFromBottom);
            Assert.AreEqual(0, s.BorderOpaquePixels);
            Assert.Greater(s.TransparentPixels, 0);
        }

        [Test]
        public void Spec_BodyFrameTotalsMatch04And09()
        {
            int total = 0;
            foreach (var b in ArtV1Spec.Bodies.Values) total += b.TotalFrames;

            // 2026-10-04: 04 최신 19종/355·363 계약으로 갱신(옛 17종/321·329 폐기 — 커피 일반 몹 2종 포함).
            Assert.AreEqual(19, ArtV1Spec.Bodies.Count, "04 §5: 플레이어 2 + 일반/엘리트 13 + 보스 4");
            Assert.AreEqual(363, total, "09 §1: 363프레임");
            Assert.AreEqual(ArtV1Spec.FullBodyFrameTotal, total);
            Assert.AreEqual(355, ArtV1Spec.BaseBodyFrameTotal);
            Assert.AreEqual(ArtV1Spec.BaseBodyFrameTotal, total - 8, "04 §6: 기본분 355 + 진상 review/reject 8");
            Assert.AreEqual(21, ArtV1Spec.Bodies["grinder_golem"].TotalFrames);
            Assert.AreEqual(23, ArtV1Spec.Bodies["bartender"].TotalFrames);
            Assert.IsTrue(ArtV1Spec.Bodies.ContainsKey("lemon_brute"), "레몬 브루트는 04 필수 아트다");
            Assert.AreEqual(15, ArtV1Spec.Bodies["espresso_bug"].TotalFrames, "04 §6: espresso_bug R 15");
            Assert.AreEqual(19, ArtV1Spec.Bodies["milk_foamer"].TotalFrames, "04 §6: milk_foamer T 19");
        }

        [Test]
        public void Spec_VisualScaleFormulaFrom04()
        {
            // H 2.40, 실측 300px, 128 PPU → 2.4 / (300/128) = 1.024
            Assert.AreEqual(1.024f, ArtV1Spec.VisualScale(2.40f, 300f), 1e-4f);
            Assert.Throws<ArgumentOutOfRangeException>(() => ArtV1Spec.VisualScale(1f, 0f));
        }
    }
}
