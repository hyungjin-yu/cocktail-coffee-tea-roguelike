using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>항목 16 차 소믈리에 음료 오라 — 순수 규칙(표시 조건·잔물결 곡선·고정 풀 크기). 실제 런 상태 추종은 TeaDrinkAuraPlayTests.</summary>
    public class TeaDrinkAuraTests
    {
        [Test]
        public void ShouldShow_OnlyTeaSommelier_WithAppliedBuff_WhilePlayingAndAlive()
        {
            Assert.IsTrue(TeaDrinkAura.ShouldShow(Job.TeaSommelier, true, true, true));
            Assert.IsFalse(TeaDrinkAura.ShouldShow(Job.TeaSommelier, false, true, true), "음료 미사용");
            Assert.IsFalse(TeaDrinkAura.ShouldShow(Job.Bartender, true, true, true), "바텐더는 혼합 러시 오라 — 겹치지 않음");
            Assert.IsFalse(TeaDrinkAura.ShouldShow(Job.TeaSommelier, true, false, true), "런 종료/정지 상태");
            Assert.IsFalse(TeaDrinkAura.ShouldShow(Job.TeaSommelier, true, true, false), "사망");
        }

        [Test]
        public void RippleCurve_Thin_ExpandsAndFadesWithinBudget()
        {
            Assert.AreEqual(TeaDrinkAura.RippleMinRadius, TeaDrinkAura.RippleRadius(0f), 1e-5f);
            Assert.AreEqual(TeaDrinkAura.RippleMaxRadius, TeaDrinkAura.RippleRadius(1f), 1e-5f);
            Assert.Less(TeaDrinkAura.RippleMaxRadius, MixingRushAura.Radius, "혼합 러시 오라보다 작게");
            Assert.AreEqual(0f, TeaDrinkAura.RippleAlphaAt(1f), 1e-5f, "끝에서 완전히 사라짐");
            for (float p = 0f; p <= 1f; p += 0.05f)
                Assert.LessOrEqual(TeaDrinkAura.RippleAlphaAt(p), TeaDrinkAura.RingAlpha + 1e-5f, "은은함 상한");
        }

        [Test]
        public void Pool_IsFixed_AndTintDiffersFromRushOrange()
        {
            Assert.AreEqual(5, TeaDrinkAura.Ripples + TeaDrinkAura.Wisps);
            var j = TeaDrinkAura.JadeTint; var o = MixingRushAura.AuraColor;
            Assert.Greater(j.g, j.r, "옥빛(초록 우세)");
            Assert.Greater(o.r, o.b, "러시=주황");
            Assert.Greater(Mathf.Abs(j.r - o.r) + Mathf.Abs(j.b - o.b), 0.5f);
        }
    }
}
