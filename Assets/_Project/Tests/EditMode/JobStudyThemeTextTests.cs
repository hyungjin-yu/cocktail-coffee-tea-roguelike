using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>2026-11-12 실제 빌드 찻집 HUD "우림 포인트 0 [K] 우림 연구 연구" 중복 수정.</summary>
    public class JobStudyThemeTextTests
    {
        [Test]
        public void TeaTheme_CoffeeResearchPhrase_IsNotDoubled()
        {
            string tea = JobStudyTheme.Text(HudView.CoffeePointsLabel(0), Job.TeaSommelier);
            StringAssert.Contains("우림 포인트 0", tea);
            StringAssert.Contains("[K] 우림 연구", tea);
            StringAssert.DoesNotContain("연구 연구", tea);
            Assert.AreEqual(HudView.CoffeePointsLabel(0), JobStudyTheme.Text(HudView.CoffeePointsLabel(0), Job.Bartender), "바텐더는 그대로");
            StringAssert.DoesNotContain("연구 연구", JobStudyTheme.Text("[K] 커피 연구에서 사용", Job.TeaSommelier));
        }
    }
}
