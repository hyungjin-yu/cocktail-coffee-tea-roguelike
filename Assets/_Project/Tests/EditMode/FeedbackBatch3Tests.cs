using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-11-15 플레이 피드백 셋째 묶음(13·14·15·16·20·21) 규칙 시험. 체감·화면은 QA 투어(smooth13 A/B 측정, 캡처)로 따로.</summary>
    public class FeedbackBatch3Tests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        RectTransform Canvas()
        {
            var go = new GameObject("C", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(1920f, 1080f);
            created.Add(go);
            return (RectTransform)go.transform;
        }

        // ── 1 첫 화면 ──
        /// <summary>2026-11-18 카페 입장형으로 대체: 입장 연출 중에는 실내 메뉴가 눌리지 않고, 건너뛰면 끝 상태(실내·외부 숨김)로 간다.
        /// (2026-11-16 소품/어둠 판 순서 회귀 시험은 그 연출 자체가 없어져 새 의미로 교체.)</summary>
        [Test]
        public void TitleIntro_Entrance_BlocksMenuUntilDone_SkipEndsAtInterior()
        {
            TitleIntro.ResetSessionForTests();
            var view = TitleView.Show();
            try
            {
                Assert.IsNotNull(view.Scene, "카페 장면");
                var intro = view.Intro;
                Assert.IsTrue(intro.PlayedFull, "첫 진입 = 전체 입장 연출");
                Assert.IsTrue(intro.IsWaiting, "외관(간판+문)에서 입장 입력 대기 — 자동 입장 없음");
                Assert.IsTrue(intro.BlockingInput, "대기·연출 중 실내 메뉴 입력 차단");
                Assert.IsTrue(view.Scene.Exterior.gameObject.activeSelf, "외부 전경부터");
                Assert.IsTrue(intro.EnterButton.gameObject.activeInHierarchy, "입장하기(문) 버튼 표시");
                Assert.Less(view.Scene.InteriorAlpha, 0.01f, "시작 시 실내는 아직 안 보임");
                intro.StartEntering();
                Assert.IsFalse(intro.IsWaiting);
                Assert.IsFalse(intro.EnterButton.gameObject.activeInHierarchy, "입장 시작 후 입장 안내 없음");
                Assert.IsTrue(intro.BlockingInput, "입장 중 메뉴 입력 차단");
                intro.Skip();
                Assert.IsTrue(intro.Finished);
                Assert.IsFalse(view.Scene.Exterior.gameObject.activeSelf, "끝나면 외부 장면 없음(중복 오버레이 없음)");
                Assert.AreEqual(1f, view.Scene.InteriorAlpha, 1e-4f);
            }
            finally { Object.DestroyImmediate(view.gameObject); TitleIntro.ResetSessionForTests(); }
        }

        [Test]
        public void TitleIntro_Reentry_IsShortInteriorFade_NoDoor()
        {
            TitleIntro.ResetSessionForTests();
            var first = TitleView.Show();
            Object.DestroyImmediate(first.gameObject);
            var view = TitleView.Show();
            try
            {
                Assert.IsFalse(view.Intro.PlayedFull, "재진입은 문 입장 반복 강제 없음");
                Assert.IsFalse(view.Intro.IsWaiting, "재진입은 외관 대기 없이 실내");
                Assert.AreEqual(TitleIntro.ShortSeconds, view.Intro.Duration, 1e-4f);
                Assert.IsFalse(view.Scene.Exterior.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(view.gameObject); TitleIntro.ResetSessionForTests(); }
        }

        // ── 21 ──
        [Test]
        public void LowHp_StrictlyBelowTwentyPercent_ExactBoundaryNormal_DeadAndZeroMaxOff()
        {
            Assert.IsFalse(LowHpWarning.IsLow(20f, 100f), "정확히 20%는 정상");
            Assert.IsTrue(LowHpWarning.IsLow(19.99f, 100f), "직전");
            Assert.IsFalse(LowHpWarning.IsLow(20.01f, 100f), "직후");
            Assert.IsFalse(LowHpWarning.IsLow(0f, 100f), "사망은 경고 아님");
            Assert.IsFalse(LowHpWarning.IsLow(5f, 0f));
            Assert.IsTrue(LowHpWarning.IsLow(29f, 150f), "최대 HP 변화(150의 20% = 30)");
            Assert.IsFalse(LowHpWarning.IsLow(29f, 140f), "최대 HP가 줄면 같은 HP도 정상(28 이상)");
        }

        [Test]
        public void LowHp_ChipAndEdges_OnBelow_OffImmediatelyOnRecovery_EdgesHiddenWhenPaused_NoRaycast()
        {
            var root = Canvas();
            var bar = UiFactory.Place("HpBar", root, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(420f, 30f));
            var w = LowHpWarning.Attach(bar, root);
            w.Tick(50f, 100f, false);
            Assert.IsFalse(w.Active); Assert.IsFalse(w.ChipVisible); Assert.AreEqual(0f, w.EdgeAlpha, 1e-4f);
            w.Tick(10f, 100f, false);
            Assert.IsTrue(w.Active); Assert.IsTrue(w.ChipVisible); Assert.Greater(w.EdgeAlpha, 0f);
            Assert.LessOrEqual(w.EdgeAlpha, 0.2f, "가장자리 띠는 약하게");
            w.Tick(10f, 100f, true);
            Assert.AreEqual(0f, w.EdgeAlpha, 1e-4f, "일시정지·결과 화면에서는 띠 없음");
            w.Tick(25f, 100f, false);
            Assert.IsFalse(w.Active); Assert.IsFalse(w.ChipVisible, "회복 즉시 정상화");
            foreach (var g in w.GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(g.raycastTarget, g.name + " 입력을 막지 않음");
            foreach (var g in bar.Find("LowHpChip").GetComponentsInChildren<Graphic>(true)) Assert.IsFalse(g.raycastTarget);
            Assert.LessOrEqual(LowHpWarning.EdgeWidth, 32f, "가장자리 띠 폭(적 예고를 가리지 않음)");
        }

        // ── 20 ──
        [Test]
        public void ProjectileTier_FromShooter_OnlyRimAndTailDiffer_HitRadiusSame()
        {
            Assert.AreEqual(EnemyProjectile.Tier.Normal, EnemyProjectile.TierOf(null));
            var normal = new EnemyDef { archetype = EnemyArchetype.Ranged };
            var elite = new EnemyDef { archetype = EnemyArchetype.EliteBarrage };
            var boss = new EnemyDef { archetype = EnemyArchetype.Ranged, boss = BossKind.Mid };
            Assert.AreEqual(EnemyProjectile.Tier.Normal, EnemyProjectile.TierOf(normal));
            Assert.AreEqual(EnemyProjectile.Tier.Elite, EnemyProjectile.TierOf(elite));
            Assert.AreEqual(EnemyProjectile.Tier.Boss, EnemyProjectile.TierOf(boss));
            Assert.Less(EnemyProjectile.TailCount(EnemyProjectile.Tier.Normal), EnemyProjectile.TailCount(EnemyProjectile.Tier.Elite));
            Assert.Less(EnemyProjectile.TailCount(EnemyProjectile.Tier.Elite), EnemyProjectile.TailCount(EnemyProjectile.Tier.Boss));
            Assert.AreNotEqual(EnemyProjectile.RimColor(EnemyProjectile.Tier.Normal), EnemyProjectile.RimColor(EnemyProjectile.Tier.Boss));
            Assert.LessOrEqual(EnemyProjectile.RimScale(EnemyProjectile.Tier.Boss), 1.4f, "테두리는 몸통보다 조금만 크게(충돌 범위 오해 방지)");

            var parent = new GameObject("P"); created.Add(parent);
            float[] radii = new float[3];
            int i = 0;
            foreach (var t in new[] { EnemyProjectile.Tier.Normal, EnemyProjectile.Tier.Elite, EnemyProjectile.Tier.Boss })
            {
                EnemyProjectile.Spawn(Vector2.zero, Vector2.right, 1f, 1f, null, parent.transform, EnemyProjectile.ArtPrefix + "glass_shard", null, default, t);
                var p = parent.GetComponentsInChildren<EnemyProjectile>()[i];
                Assert.AreEqual(t, p.ShotTier);
                radii[i++] = p.HitRadiusForTests;
            }
            Assert.AreEqual(radii[0], radii[1], 1e-6f, "판정 반지름 동일");
            Assert.AreEqual(radii[0], radii[2], 1e-6f);
        }

        // ── 15 ──
        [Test]
        public void BossPortraits_ExistForScheduledBosses_FromApprovedArt()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
                foreach (var spec in BossDirector.DefaultSchedule(job))
                {
                    var s = HudIcons.Enemy(spec.def);
                    Assert.IsNotNull(s, $"{job} {spec.def.name} 초상(기존 몸체 아트)");
                }
        }

        // ── 13 ──
        [Test]
        public void PlayerMovement_Interpolated_ByDefault()
        {
            Assert.IsFalse(PlayerController.QaDisableInterpolation, "출시 경로 기본 = 보간");
            Assert.AreEqual(0.02f, Time.fixedDeltaTime, 1e-4f, "물리 50Hz(렌더보다 느림 → 보간이 필요한 이유)");
        }
    }
}
