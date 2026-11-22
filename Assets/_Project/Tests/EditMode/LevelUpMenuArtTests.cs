using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-22 레벨업 메뉴판 아트 반입: 매니페스트 대조(프레임 ms·선택 시점), 시간표, 스프라이트 크기·Point·무밉맵·128 PPU.</summary>
    public class LevelUpMenuArtTests
    {
        static string ManifestText()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource/v1/levelup-menu-2026-11-22/manifest.json"));
            Assert.IsTrue(File.Exists(path), "원본 매니페스트 없음: " + path);
            return File.ReadAllText(path);
        }

        [Test]
        public void FrameTimes_And_SelectTimes_MatchManifest()
        {
            var text = ManifestText();
            var durations = new List<int>();
            foreach (Match m in Regex.Matches(text, @"""durationMs"":\s*(\d+)")) durations.Add(int.Parse(m.Groups[1].Value));
            var handoffAt = new List<int>(); var selectAt = new List<int>();
            foreach (Match m in Regex.Matches(text, @"""handoffAtMs"":\s*(\d+)")) handoffAt.Add(int.Parse(m.Groups[1].Value));
            foreach (Match m in Regex.Matches(text, @"""selectAtMs"":\s*(\d+)")) selectAt.Add(int.Parse(m.Groups[1].Value));
            Assert.AreEqual(8, handoffAt.Count, "4등급 × 2직업");
            int d = 0, c = 0;
            for (int tier = 0; tier < 4; tier++)
                for (int job = 0; job < 2; job++, c++)
                {
                    int sum = 0;
                    for (int f = 0; f < LevelUpMenuArt.FrameCount(tier); f++, d++)
                    {
                        Assert.AreEqual(durations[d], LevelUpMenuArt.FrameMsAt(tier, f), $"tier {tier} job {job} frame {f}");
                        sum += durations[d];
                    }
                    Assert.AreEqual(handoffAt[c], sum, "전달 종료 = 프레임 합");
                    Assert.AreEqual(handoffAt[c], LevelUpMenuArt.HandoffMs(tier));
                    Assert.AreEqual(selectAt[c], LevelUpMenuArt.SelectMs(tier), "선택 가능 = 전달 종료 + 70 ms");
                }
            Assert.AreEqual(durations.Count, d);
            Assert.AreEqual(0.070f, LevelUpMenuArt.RevealSeconds, 1e-6f);
        }

        [Test]
        public void Evaluate_FramesAdvance_RevealOnlyAfterHandoff_DoneAtSelectTime()
        {
            for (int tier = 0; tier < 4; tier++)
            {
                LevelUpMenuArt.Evaluate(tier, 0f, out int f0, out float r0, out bool d0);
                Assert.AreEqual(0, f0); Assert.AreEqual(0f, r0); Assert.IsFalse(d0);
                float handoff = LevelUpMenuArt.HandoffMs(tier) / 1000f;
                LevelUpMenuArt.Evaluate(tier, handoff - 0.001f, out int fl, out float rl, out bool dl);
                Assert.AreEqual(LevelUpMenuArt.FrameCount(tier) - 1, fl, "마지막 포즈에서 대기"); Assert.AreEqual(0f, rl); Assert.IsFalse(dl);
                LevelUpMenuArt.Evaluate(tier, handoff + 0.035f, out int fm, out float rm, out bool dm);
                Assert.AreEqual(LevelUpMenuArt.FrameCount(tier) - 1, fm, "페이드 중 포즈 고정"); Assert.AreEqual(0.5f, rm, 0.01f); Assert.IsFalse(dm);
                LevelUpMenuArt.Evaluate(tier, LevelUpMenuArt.SelectMs(tier) / 1000f, out _, out float re, out bool de);
                Assert.AreEqual(1f, re, 1e-4f); Assert.IsTrue(de);
            }
        }

        [Test]
        public void MaxTier_UsesHighestVisibleRarity_NoneIsMinusOne()
        {
            CardOffer O(CardRarity r) { var c = ScriptableObject.CreateInstance<CardData>(); c.rarity = r; return new CardOffer { card = c }; }
            Assert.AreEqual(-1, LevelUpMenuArt.MaxTier(new List<CardOffer>()));
            Assert.AreEqual(2, LevelUpMenuArt.MaxTier(new List<CardOffer> { O(CardRarity.Common), O(CardRarity.Epic), O(CardRarity.Rare) }));
            Assert.AreEqual(0, LevelUpMenuArt.MaxTier(new List<CardOffer> { O(CardRarity.Common) }));
            Assert.AreEqual(3, LevelUpMenuArt.MaxTier(new List<CardOffer> { O(CardRarity.Special) }));
        }

        static void CheckSprite(Sprite s, int w, int h, string what)
        {
            Assert.IsNotNull(s, what + " 로드 실패");
            Assert.AreEqual(w, s.texture.width, what); Assert.AreEqual(h, s.texture.height, what);
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode, what + " Point");
            Assert.AreEqual(1, s.texture.mipmapCount, what + " 밉맵 없음");
            Assert.AreEqual(128f, s.pixelsPerUnit, what + " PPU");
        }

        [Test]
        public void AllFortyOneSprites_Load_WithImportRules()
        {
            int n = 0;
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
                for (int tier = 0; tier < 4; tier++)
                {
                    var frames = LevelUpMenuArt.Frames(job, tier);
                    Assert.IsNotNull(frames, $"{job} tier {tier}");
                    Assert.AreEqual(LevelUpMenuArt.FrameCount(tier), frames.Length);
                    foreach (var s in frames) { CheckSprite(s, 128, 96, $"{job}/{tier}"); n++; }
                }
            for (int tier = 0; tier < 4; tier++) { CheckSprite(LevelUpMenuArt.CardDecoration(tier), 86, 124, "card " + tier); n++; }
            CheckSprite(LevelUpMenuArt.HybridDecoration(), 196, 112, "hybrid"); n++;
            Assert.AreEqual(41, n);
        }

        [Test]
        public void ImportedPngs_AreByteIdenticalToArtSource()
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource/v1/levelup-menu-2026-11-22/ready"));
            var dst = Path.Combine(Application.dataPath, "_Project/Resources/LevelUpMenu");
            int n = 0;
            foreach (var f in Directory.GetFiles(src, "*.png", SearchOption.AllDirectories))
            {
                var rel = f.Substring(src.Length + 1);
                CollectionAssert.AreEqual(File.ReadAllBytes(f), File.ReadAllBytes(Path.Combine(dst, rel)), rel);
                n++;
            }
            Assert.AreEqual(41, n);
        }

        [Test]
        public void SpriteAnchor_PivotIsThirdOfCanvas_AndAnchorRowIs32()
        {
            Assert.AreEqual(64f / 128f, LevelUpMenuArt.Pivot.x, 1e-6f);
            Assert.AreEqual(32f / 96f, LevelUpMenuArt.Pivot.y, 1e-6f);
            Assert.AreEqual(384, LevelUpMenuArt.CanvasWidthPx * LevelUpMenuArt.DisplayScale);
            Assert.AreEqual(288, LevelUpMenuArt.CanvasHeightPx * LevelUpMenuArt.DisplayScale);
        }
    }
}
