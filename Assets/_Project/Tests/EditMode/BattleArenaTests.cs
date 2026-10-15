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
    }
}
