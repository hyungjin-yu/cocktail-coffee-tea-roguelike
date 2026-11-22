using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-22 정식 픽셀 아트 반입 계약: 128px 몸체 별도 등록, 클립 장수·fps, 발 피벗, 예고 1초, 아이콘 크기.</summary>
    public class FeedbackArtFormalTests
    {
        [Test]
        public void AllFormalAssets_Load_WithManifestClipCounts()
        {
            int total = 0;
            foreach (var id in FeedbackArt.Ids)
            {
                var art = ArtLibrary.Get(id);
                Assert.IsNotNull(art, id + " 로드");
                foreach (var spec in FeedbackArt.ClipsOf(id))
                {
                    var clip = art.Clip(spec.name);
                    Assert.IsNotNull(clip, id + "/" + spec.name);
                    Assert.AreEqual(spec.frames, clip.frames.Length, id + "/" + spec.name + " 장수");
                    total += clip.frames.Length;
                }
            }
            Assert.AreEqual(68, total, "몸체 46 + 바닥 효과 20 + HUD 2");
        }

        [Test]
        public void Bodies_Are128px_FootPivot_AndSeparatedFromSharedEnemyFiles()
        {
            foreach (var d in new[] { EnemyDef.BitterMist, EnemyDef.GlassThief })
            {
                var art = ArtLibrary.Get(d.ArtId);
                Assert.IsTrue(art.IsBody, d.name);
                Assert.AreEqual(new Vector2(128, 128), art.frameSizePx);
                Assert.AreEqual(0.125f, art.pivotNormalized.y, 1e-5f);
                Assert.AreEqual(0.125f, art.Clip("idle").frames[0].pivot.y / 128f, 1e-4f, "스프라이트 발 피벗");
                Assert.AreNotEqual("swirl_stick_conjuror", d.ArtId);
                Assert.AreNotEqual("milk_foamer", d.ArtId);
            }
            Assert.IsNotNull(ArtLibrary.Get(EnemyDef.GlassThief.ArtId).Clip("flee"));
        }

        [Test]
        public void PulseTelegraph_IsExactlyOneSecond_MatchingDriver()
        {
            var charge = ArtLibrary.Get(FeedbackArt.BitterMistPulse).Clip("charge");
            Assert.AreEqual(BitterMistDriver.TelegraphSeconds, charge.Duration, 1e-4f);
            Assert.AreEqual(0.6f, ArtLibrary.Get(FeedbackArt.BitterMist).Clip("action").Duration, 1e-4f);
        }

        [Test]
        public void StatAndStatusIcons_AreFormalSizes()
        {
            foreach (var key in new[] { "damage", "attack_speed", "range", "move_speed", "max_hp", "regen", "damage_reduction", "flame", "poison", "pierce", "multi_hit", "drop", "gauge", "bean", "pickup" })
            {
                var s = Resources.Load<Sprite>("StatIcons/stat_" + key);
                Assert.IsNotNull(s, key);
                Assert.AreEqual(32f, s.rect.width, key); Assert.AreEqual(32f, s.rect.height, key);
            }
            foreach (var id in new[] { FeedbackArt.StatusAttackSpeedDown, FeedbackArt.StatusHealBlock })
                Assert.AreEqual(40f, ArtLibrary.Sprite(id).rect.width, id);
        }
    }
}
