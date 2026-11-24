using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-24 #24 최종 보스 신규 패턴의 정적 계약: 위험 폭·통로·사거리·도달 가능성, 예고 아트(알파 상자 = 판정 경계) 반입.
    /// </summary>
    public class FinalBossPatternGeometryTests
    {
        const float SlowedSpeed = 5f * 0.75f; // 기본 이동 5 × 별점 둔화 0.75 (가장 흔한 둔화 상태)

        [Test]
        public void DangerWidth_IsProjectilePlusPlayerRadius()
        {
            Assert.AreEqual(0.62f, CustomerBossDriver.DangerHalfWidth, 1e-5f);
            Assert.AreEqual(EnemyProjectile.BaseHitRadius + PlayerController.Radius, CustomerBossDriver.DangerHalfWidth, 1e-6f);
        }

        [Test]
        public void Snipe_ReachesAcrossScreen_AndLockIsDodgeableWhileSlowed()
        {
            float screen16x9 = Balance.DepthHalfWidth * 1.5f * 2f * 16f / 9f; // ortho 6 → 폭 21.3
            Assert.Greater(CustomerBossDriver.SnipeRange, screen16x9, "멀리서도 닿는 사거리");
            Assert.GreaterOrEqual(CustomerBossDriver.SnipeProjectileSpeed * EnemyProjectile.LifetimeSeconds, CustomerBossDriver.SnipeRange, "탄 비행 거리 ≥ 예고 길이");
            // 선 한가운데 서 있어도 가장 짧은 고정 시간 안에 위험 반폭 밖으로 나갈 수 있다.
            Assert.Less(CustomerBossDriver.DangerHalfWidth / SlowedSpeed, CustomerBossDriver.SnipeEnragedLockSeconds);
            Assert.Greater(CustomerBossDriver.SnipeFirstTrackSeconds, CustomerBossDriver.SnipeTrackSeconds, "최초 노출은 더 길게");
            Assert.Greater(CustomerBossDriver.SnipeTrackSeconds, CustomerBossDriver.SnipeEnragedTrackSeconds, "후반 강화는 더 짧게");
        }

        [Test]
        public void Wall_LanesCoverWholeBand_ExceptOneCorridor()
        {
            float s = CustomerBossDriver.WallLaneSpacing, h = CustomerBossDriver.DangerHalfWidth;
            Assert.Less(s, 2f * h, "이웃 줄의 위험 폭이 겹쳐 줄 사이로 빠져나갈 수 없다");
            Assert.LessOrEqual(CustomerBossDriver.LaneY(0) - h, Balance.DepthMin, "아래 끝 덮음");
            Assert.GreaterOrEqual(CustomerBossDriver.LaneY(CustomerBossDriver.WallLaneCount - 1) + h, Balance.DepthMax, "위 끝 덮음");
            float corridor = (CustomerBossDriver.WallGapLanes + 1) * s - 2f * h;
            Assert.AreEqual(1.76f, corridor, 1e-4f, "통로(플레이어 중심 기준) 폭");
        }

        [Test]
        public void ChooseGap_NeverUnderPlayer_AndReachableInTelegraph()
        {
            float halfCorridor = ((CustomerBossDriver.WallGapLanes + 1) * CustomerBossDriver.WallLaneSpacing - 2f * CustomerBossDriver.DangerHalfWidth) * 0.5f;
            for (float p = Balance.DepthMin; p <= Balance.DepthMax + 1e-4f; p += 0.25f)
            {
                foreach (int side in new[] { 0, 1, -1 })
                {
                    int g = CustomerBossDriver.ChooseGapStart(p, side);
                    Assert.That(g, Is.InRange(0, CustomerBossDriver.WallLaneCount - CustomerBossDriver.WallGapLanes), $"p={p} side={side}");
                    float dist = Mathf.Abs(CustomerBossDriver.GapCenterY(g) - p);
                    Assert.Greater(dist, halfCorridor, $"p={p} side={side}: 제자리 안전 금지");
                    Assert.LessOrEqual(dist, CustomerBossDriver.WallGapMaxOffset + 1e-4f, $"p={p} side={side}");
                    Assert.Less((dist - halfCorridor) / SlowedSpeed, CustomerBossDriver.WallTelegraphSeconds, $"p={p}: 둔화 상태로도 예고 안에 도달");
                }
            }
        }

        [Test]
        public void ChooseGap_HonorsPreferredSide_InMiddleOfBand()
        {
            float p = Balance.LaneCenterY;
            Assert.Greater(CustomerBossDriver.GapCenterY(CustomerBossDriver.ChooseGapStart(p, 1)), p);
            Assert.Less(CustomerBossDriver.GapCenterY(CustomerBossDriver.ChooseGapStart(p, -1)), p);
        }

        [Test]
        public void TelegraphArt_Loads_AlphaBoxMatchesManifestScale()
        {
            foreach (var id in new[] { FeedbackArt.FinalRefundRing, FeedbackArt.FinalStarFan, FeedbackArt.FinalAimTrack, FeedbackArt.FinalAimLock, FeedbackArt.FinalQueueWall })
                Assert.IsNotNull(ArtLibrary.Get(id), id);

            // manifest scaleXY — Band/GroundRing이 알파 상자를 판정 치수로 맞춘 결과와 같아야 한다.
            var track = ArtLibrary.Get(FeedbackArt.FinalAimTrack).ContentWorldSize;
            Assert.AreEqual(13.206349f, 26f / track.x, 1e-3f); Assert.AreEqual(1.28f, 0.12f / track.y, 1e-3f);
            var lockBand = ArtLibrary.Get(FeedbackArt.FinalAimLock).ContentWorldSize;
            Assert.AreEqual(5.668571f, 1.24f / lockBand.y, 1e-3f);
            var wall = ArtLibrary.Get(FeedbackArt.FinalQueueWall).ContentWorldSize;
            Assert.AreEqual(21f * 128f / 252f, 21f / wall.x, 1e-3f);
            Assert.AreEqual(8.533333f, ArtVfx.ScaleForGroundRadius(ArtLibrary.Get(FeedbackArt.FinalRefundRing), 4f), 1e-3f);
            var fan = ArtLibrary.Get(FeedbackArt.FinalStarFan);
            Assert.AreEqual(8.248889f, 16.24f / fan.ContentWorldSize.x, 1e-3f);
            Assert.AreEqual(16.763871f, 16.24f / fan.ContentWorldSize.y, 1e-3f);
            Assert.AreEqual(11.620689f, fan.Main.frames[0].pivot.x, 0.05f, "별점 부채 피벗 = 발사점(좌측 11.62px)");
        }

        [Test]
        public void TelegraphArt_ClipDurations_MatchPatternTimers()
        {
            Assert.AreEqual(0.8f, ArtLibrary.Get(FeedbackArt.FinalRefundRing).Main.Duration, 1e-4f, "환불 0.8초");
            Assert.AreEqual(0.8f, ArtLibrary.Get(FeedbackArt.FinalStarFan).Main.Duration, 1e-4f, "별점 0.8초");
            Assert.AreEqual(CustomerBossDriver.WallTelegraphSeconds, ArtLibrary.Get(FeedbackArt.FinalQueueWall).Main.Duration, 1e-4f, "벽 1초");
            Assert.IsTrue(ArtLibrary.Get(FeedbackArt.FinalAimTrack).Main.loop, "추적선은 반복");
            Assert.LessOrEqual(ArtLibrary.Get(FeedbackArt.FinalAimLock).Main.Duration, CustomerBossDriver.SnipeEnragedLockSeconds, "고정 띠 3프레임은 가장 짧은 고정 안에 끝나고 마지막 프레임 유지");
        }
    }
}
