using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-11 마스터 도전 인수 아트 표(21 clip·110 노출)와 실제 임포트 결과(PPU·크기·pivot·Point) 및 등급/상성 clip 선택.</summary>
    public class MasterArtTests
    {
        [Test]
        public void AllAcceptedClips_LoadWithContractImportSettings()
        {
            int frames = 0;
            foreach (var clip in MasterArt.AllClips)
            {
                var sprites = MasterArt.Load(clip.name);
                Assert.IsNotNull(sprites, clip.name);
                Assert.AreEqual(clip.durationsMs.Length, sprites.Length, clip.name);
                foreach (var s in sprites)
                {
                    Assert.AreEqual(128f, s.pixelsPerUnit, 0.01f, s.name);
                    Assert.AreEqual(clip.canvas.x, s.texture.width, s.name);
                    Assert.AreEqual(clip.canvas.y, s.texture.height, s.name);
                    Assert.AreEqual(FilterMode.Point, s.texture.filterMode, s.name);
                    var pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    var expect = clip.name.StartsWith("npc_") ? new Vector2(0.5f, 0.125f) : clip.name.StartsWith("banner_") ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
                    Assert.Less((pivot - expect).magnitude, 1e-3f, s.name);
                    frames++;
                }
            }
            Assert.AreEqual(MasterArt.TotalFrames, frames);
            Assert.AreEqual(21, MasterArt.AllClips.Length);
        }

        [Test]
        public void ContractTimings_MatchManifest()
        {
            Assert.AreEqual(1500, MasterArt.Get("npc_porter_install").TotalMs);
            Assert.AreEqual(1000, MasterArt.Get("banner_install").TotalMs);
            Assert.AreEqual(498, MasterArt.Get("quality_normal").TotalMs);
            Assert.AreEqual(664, MasterArt.Get("quality_master").TotalMs);
            Assert.AreEqual(249, MasterArt.Get("tea_leaf_wind_low").TotalMs);
            Assert.IsFalse(MasterArt.Get("quality_master").loop, "최고 효과 반복 loop 금지");
            var banner = MasterArt.Get("banner_install");
            Assert.AreEqual(7, MasterArt.FrameAt(banner, 5000f), "현수막 마지막 프레임 고정");
            Assert.AreEqual(1, MasterArt.FrameAt(MasterArt.Get("npc_rigger_walk_in"), 700f), "걷기 루프");
            // 손 앵커 flipX 대칭: x = 255 − x
            var a = MasterArt.NpcAnchorLocal(new Vector2(61, 93), false);
            var b = MasterArt.NpcAnchorLocal(new Vector2(194, 93), true);
            Assert.AreEqual(a.x, b.x, 1e-5f);
        }

        [Test]
        public void QualityAndAffinityClipSelection_LowReplacesFull()
        {
            Assert.AreEqual("quality_normal", MasterArt.QualityClip(DrinkQuality.GradeOf(0), false));
            Assert.AreEqual("quality_normal", MasterArt.QualityClip(DrinkQuality.GradeOf(59), false));
            Assert.AreEqual("quality_excellent", MasterArt.QualityClip(DrinkQuality.GradeOf(60), false));
            Assert.AreEqual("quality_master", MasterArt.QualityClip(DrinkQuality.GradeOf(85), false));
            Assert.AreEqual("quality_master_low", MasterArt.QualityClip(DrinkQuality.GradeOf(100), true));
            Assert.IsNull(MasterArt.QualityClip(DrinkQuality.GradeOf(-1), false), "품질 없음 → 효과 없음");
            Assert.AreEqual("cocktail_foam", MasterArt.AffinityClip(DrinkKind.Cocktail, false));
            Assert.AreEqual("coffee_bean_steam_low", MasterArt.AffinityClip(DrinkKind.Coffee, true));
            Assert.AreEqual("tea_leaf_wind", MasterArt.AffinityClip(DrinkKind.Tea, false));
        }

        [Test]
        public void BannerGap_WorldFixedBetweenCounters_PrefersHudClearGap()
        {
            // 카운터 x = -30 + 12k(폭 4) → 빈 칸 중앙 -24, -12, 0, 12, 24.
            Assert.AreEqual(-24f, CafeBackground.FirstGapX, 1e-4f);
            Assert.AreEqual(0f, CafeBackground.NearestCounterGapX(1f, -26.5f, 26.5f), 1e-4f);
            float half = FairBannerShow.BannerHalfWidth;
            Assert.LessOrEqual(half, CafeBackground.CounterSpacing * 0.5f - 2f - 0.4f, "현수막 반폭이 빈 칸(카운터 사이 8월드) 안");
            float min = -BattleArena.HalfWidth + half, max = BattleArena.HalfWidth - half;
            Assert.AreEqual(0f, FairBannerShow.ChooseGap(0f, min, max), 1e-4f, "카메라 중앙이면 가운데 칸");
            Assert.AreEqual(12f, FairBannerShow.ChooseGap(9f, min, max), 1e-4f, "왼쪽 칸이 좌측 HUD 밑이면 오른쪽 칸(off 3)");
            Assert.AreEqual(12f, FairBannerShow.ChooseGap(7f, min, max), 1e-4f, "off 5 칸이 off −7 칸보다 우선");
            Assert.AreEqual(24f, FairBannerShow.ChooseGap(28f, min, max), 1e-4f, "구역 끝에서는 구역 안 칸만");
        }
    }
}
