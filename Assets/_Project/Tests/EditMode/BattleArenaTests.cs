using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>57 P0-1 유한 전투 구역의 순수 규칙: 클램프, 카메라 한계, 벽 쪽 스폰 전환, 보스 화면 밖 판정.</summary>
    public class BattleArenaTests
    {
        const float HalfView16x9 = 6f * 16f / 9f;

        [Test]
        public void ClampX_KeepsBodyInsideWalls()
        {
            Assert.AreEqual(BattleArena.MaxX - 0.4f, BattleArena.ClampX(500f, 0.4f), 1e-4f);
            Assert.AreEqual(BattleArena.MinX + 1f, BattleArena.ClampX(-500f, 1f), 1e-4f);
            Assert.AreEqual(3f, BattleArena.ClampX(3f, 0.4f), 1e-4f);
        }

        [Test]
        public void CameraLimit_ShowsWallButNotBeyondReveal()
        {
            float limit = BattleArena.CameraLimit(HalfView16x9);
            Assert.AreEqual(BattleArena.HalfWidth + BattleArena.OutsideReveal - HalfView16x9, limit, 1e-4f);
            Assert.AreEqual(limit, BattleArena.ClampCameraX(1000f, HalfView16x9), 1e-4f);
            Assert.AreEqual(0f, BattleArena.CameraLimit(100f), "구역보다 넓은 화면은 중앙 고정");
        }

        [Test]
        public void SpawnX_UsesOffscreenSide_WhenInsideArena()
        {
            float x = BattleArena.SpawnX(0f, HalfView16x9, 1f, 1.5f, 0.5f, out float used);
            Assert.AreEqual(HalfView16x9 + 1.5f, x, 1e-4f);
            Assert.AreEqual(1f, used);
        }

        [Test]
        public void SpawnX_FlipsToOtherSide_WhenCameraAtWall()
        {
            float camX = BattleArena.CameraLimit(HalfView16x9);
            float x = BattleArena.SpawnX(camX, HalfView16x9, 1f, 1.5f, 0.5f, out float used);
            Assert.AreEqual(-1f, used, "오른쪽 벽에 붙으면 오른쪽 화면 밖은 구역 밖 — 왼쪽 화면 밖에서 나와야 한다");
            Assert.Less(x, camX - HalfView16x9, "반대쪽도 화면 밖(진입 연출 유지)");
            Assert.IsTrue(BattleArena.Contains(x, 0.5f));
        }

        [Test]
        public void SpawnX_BothSidesBlocked_StaysInsideWall()
        {
            float x = BattleArena.SpawnX(0f, 40f, 1f, 1.5f, 0.5f, out float used);
            Assert.AreEqual(1f, used);
            Assert.AreEqual(BattleArena.MaxX - 0.5f, x, 1e-4f);
        }

        [Test]
        public void SideLabel_MatchesActualSides()
        {
            Assert.AreEqual("양쪽", BattleArena.SideLabel(true, true));
            Assert.AreEqual("왼쪽", BattleArena.SideLabel(true, false));
            Assert.AreEqual("오른쪽", BattleArena.SideLabel(false, true));
        }

        [Test]
        public void BossIndicator_OffscreenRule()
        {
            Assert.IsTrue(BossDirectionIndicator.IsOffscreen(0f, HalfView16x9, -20f, 1f, out float side));
            Assert.AreEqual(-1f, side);
            Assert.IsFalse(BossDirectionIndicator.IsOffscreen(0f, HalfView16x9, 5f, 1f, out _));
            Assert.IsTrue(BossDirectionIndicator.IsOffscreen(0f, HalfView16x9, HalfView16x9 + 1.5f, 1f, out side));
            Assert.AreEqual(1f, side);
        }

        [Test]
        public void BackgroundTone_BarIsCalmerThanTeaHouse()
        {
            var bar = CafeBackground.ToneFor("bg_bar");
            var tea = CafeBackground.ToneFor("bg_tea");
            Assert.Less(bar.floor.maxColorComponent, 0.8f, "바 바닥 밝기를 낮춘다");
            Assert.Greater(bar.veil.a, tea.veil.a, "찻집은 원래 차분해 약하게만");
            Assert.GreaterOrEqual(tea.floor.maxColorComponent, 0.85f, "찻집 부작용 없음");
        }

        /// <summary>2026-11-20 싱글 칵테일 바 밝기 계약: 보정본 4부속 모두 존재, 곱 틴트 1(이중 보정 없음), 바닥 막 ≤ 0.08.
        /// 원본 bg_bar·찻집·공용 카페 톤은 그대로. QA 전후 비교 스위치는 기본 꺼짐.</summary>
        [Test]
        public void WarmBar_FourParts_NoDoubleGrade_VeilWithinContract_OthersUnchanged()
        {
            foreach (var part in new[] { "floor", "wall", "counter", "lamp" })
            {
                Assert.IsNotNull(CafeBackground.JobSprite(CafeBackground.WarmBarPrefix, part), "보정본 " + part);
                Assert.IsNotNull(CafeBackground.JobSprite("bg_bar", part), "원본 보존 " + part);
            }
            var w = CafeBackground.ToneFor(CafeBackground.WarmBarPrefix);
            Assert.AreEqual(Color.white, w.floor); Assert.AreEqual(Color.white, w.wall); Assert.AreEqual(Color.white, w.prop);
            Assert.LessOrEqual(w.veil.a, 0.08f);
            Assert.AreEqual(0.34f, CafeBackground.ToneFor("bg_bar").veil.a, 1e-4f, "원본 경로 톤 불변(보정본 없을 때 대비)");
            Assert.AreEqual(0.12f, CafeBackground.ToneFor("bg_tea").veil.a, 1e-4f, "찻집 불변");
            Assert.AreEqual(0.22f, CafeBackground.ToneFor(null).veil.a, 1e-4f, "공용 카페 불변");
            Assert.AreEqual("bg_bar", JobPreset.For(Job.Bartender).mapArtPrefix, "맵 ID 유지");
            Assert.IsFalse(CafeBackground.ForceOriginalBarForQa, "QA 원본 강제는 기본 꺼짐");
        }

        /// <summary>2026-11-20 보스 접지 그림자 보완: 보정 바 바닥이 아닐 때(찻집·공용 카페·원본 바)는 아무것도 바꾸지 않는다. 값은 상한 안.</summary>
        [Test]
        public void WarmBarBossGround_NoChange_WhenWarmBarInactive()
        {
            var go = new GameObject("BossShadowTest");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(1f, 1f, 1f, 0.22f);
                go.transform.localScale = Vector3.one * 2f;
                if (!CafeBackground.UsesWarmBar)
                {
                    CafeBackground.ApplyWarmBarBossGround(go.transform, sr, 1f);
                    Assert.AreEqual(0.22f, sr.color.a, 1e-5f);
                    Assert.AreEqual(2f, go.transform.localScale.x, 1e-5f);
                }
                Assert.DoesNotThrow(() => CafeBackground.ApplyWarmBarBossGround(go.transform, null, 1f));
                Assert.IsTrue(CafeBackground.BossGroundBoostForQa, "기본 켜짐");
                Assert.LessOrEqual(CafeBackground.WarmBossShadowAlpha, 0.6f, "그림자는 반투명(윤곽 가림 없음)");
                Assert.AreEqual(SortOrders.Shadow, 0, "그림자는 장판 예고(GroundZone)·개체보다 아래");
                Assert.Less(SortOrders.Shadow, SortOrders.GroundZone);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
