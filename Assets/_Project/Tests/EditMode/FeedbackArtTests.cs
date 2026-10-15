using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>57(2026-10-15) 비교 빌드 게이트: 새 드롭 3종과 바/찻집 경계 모듈(Aseprite+PixelLab, ArtSource/feedback-2026-10-15)이
    /// 실제로 반입돼 쓰이는지. 없으면 개발용 대체 화면으로 빌드되므로 실패해야 한다.</summary>
    public class FeedbackArtTests
    {
        [Test]
        public void NewDropSprites_ArePresent_AndUsedByPickups()
        {
            foreach (var t in new[] { IngredientType.Cocktail, IngredientType.Tea, IngredientType.Coffee })
                Assert.IsNotNull(IngredientPickup.DropArt(t), $"drop sprite for {t}");
        }

        [Test]
        public void BoundaryModules_ArePresent_ForBothJobMaps()
        {
            Assert.IsNotNull(CafeBackground.JobSprite("bg_bar", "boundary"), "bg_bar_boundary");
            Assert.IsNotNull(CafeBackground.JobSprite("bg_tea", "boundary"), "bg_tea_boundary");
        }
    }
}
