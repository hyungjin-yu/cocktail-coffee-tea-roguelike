using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-24 #22 바닥 위험(물기·얼룩): 일정·모드 범위·순서(예고 → 생성 즉시 디버프 → 진입 → 연속 체류 → 청소·해제)·
    /// 이탈 초기화·자연 해제 없음·다중 구역 개별 청소·계열 비중첩·상한·배치 규칙·미끄러짐 단계·스트레스 공속·HUD 문구.</summary>
    public class FloorHazardTests
    {
        static readonly Vector2 Far = new Vector2(-20f, Balance.LaneCenterY);

        [Test]
        public void CleanSeconds_ProvisionalThree_SingleSetting()
        {
            Assert.AreEqual(3f, FloorHazardRules.CleanSeconds, "사용자 미정 — 검수용 잠정 3초");
        }

        [Test]
        public void Schedule_From120_Every30_Then20After300_Alternates_StandardOnly()
        {
            Assert.AreEqual(0, FloorHazardRules.DueCount(119.9f));
            Assert.AreEqual(1, FloorHazardRules.DueCount(120f));
            Assert.AreEqual(2, FloorHazardRules.DueCount(150f));
            Assert.AreEqual(6, FloorHazardRules.DueCount(299.9f));
            Assert.AreEqual(7, FloorHazardRules.DueCount(300f));
            Assert.AreEqual(8, FloorHazardRules.DueCount(320f));
            for (float t = 0f; t < 900f; t += 0.5f) Assert.LessOrEqual(FloorHazardRules.DueCount(t), FloorHazardRules.DueCount(t + 0.5f));
            Assert.AreEqual(FloorHazardKind.Water, FloorHazardRules.KindFor(0));
            Assert.AreEqual(FloorHazardKind.Stain, FloorHazardRules.KindFor(1));
            Assert.IsTrue(FloorHazardRules.ModeHasHazards(RunMode.Standard));
            Assert.IsFalse(FloorHazardRules.ModeHasHazards(RunMode.Endless), "마스터 도전은 기존처럼 범위 밖");
            Assert.IsFalse(EnemySpawner.AutoDebuffMobs, "정규 런 디버프 몹 자동 등장 대체");
        }

        [Test]
        public void Sequence_ImmediateDebuff_Enter_Stay_Clean_RemovesZoneAndDebuff()
        {
            var f = new FloorHazardField();
            int materialized = 0, cleaned = 0;
            f.Materialized += _ => materialized++;
            f.CleanedZone += _ => cleaned++;
            var c = new Vector2(5f, Balance.LaneCenterY);
            var z = f.Add(FloorHazardKind.Water, c);
            Assert.IsTrue(z.Warning);
            f.Tick(FloorHazardRules.WarnSeconds - 0.1f, Far, true);
            Assert.IsFalse(f.SlipActive, "예고 중에는 디버프 없음");
            // 1) 생성 즉시: 플레이어가 멀리 있어도 디버프
            f.Tick(0.2f, Far, true);
            Assert.AreEqual(1, materialized);
            Assert.IsTrue(z.Active);
            Assert.IsTrue(f.SlipActive, "생성 순간 구역 밖이어도 미끄러짐");
            // 2) 오래 지나도·멀리 있어도 해제 없음(자연 소멸 없음)
            for (int i = 0; i < 600; i++) f.Tick(1f, Far, true);
            Assert.IsTrue(f.SlipActive); Assert.AreEqual(1, f.Total);
            // 3) 진입 → 체류 중 → 이탈하면 카운터 초기화, 디버프 유지
            f.Tick(1f, c, true);
            Assert.AreEqual(1f, z.CleanProgress, 1e-4f);
            Assert.AreSame(z, f.Cleaning);
            f.Tick(0.1f, Far, true);
            Assert.AreEqual(0f, z.CleanProgress, "나가면 연속 체류 초기화");
            Assert.IsTrue(f.SlipActive, "구역을 떠나도 해제 없음");
            // 4) 연속 체류 CleanSeconds → 청소: 구역·디버프 함께 제거
            float stayed = 0f;
            while (stayed < FloorHazardRules.CleanSeconds - 0.05f) { f.Tick(0.05f, c, true); stayed += 0.05f; Assert.IsTrue(f.SlipActive || z.Cleaned); }
            f.Tick(0.1f, c, true);
            Assert.IsTrue(z.Cleaned);
            Assert.AreEqual(1, cleaned);
            Assert.AreEqual(0, f.Total, "구역 제거");
            Assert.IsFalse(f.SlipActive, "디버프 해제");
        }

        [Test]
        public void DeadOrBlockedPlayer_DoesNotClean_StayDuringWarning_DoesNotCount()
        {
            var f = new FloorHazardField();
            var c = new Vector2(5f, Balance.LaneCenterY);
            var z = f.Add(FloorHazardKind.Stain, c);
            f.Tick(FloorHazardRules.WarnSeconds + 0.01f, c, true); // 예고 내내 안에 있었어도
            Assert.AreEqual(0f, z.CleanProgress, "예고 중 체류는 청소가 아님");
            f.Tick(10f, c, false);
            Assert.AreEqual(0f, z.CleanProgress, "청소 불가 상태(사망)에서는 진행 없음");
            Assert.IsTrue(f.StressActive);
        }

        [Test]
        public void MultipleZones_IndividualCleaning_SameKindNotStacked()
        {
            var f = new FloorHazardField();
            var a = new Vector2(4f, -3f); var b = new Vector2(-4f, -3f); var s = new Vector2(0f, 0f);
            var za = f.Add(FloorHazardKind.Water, a, 0f);
            var zb = f.Add(FloorHazardKind.Water, b, 0f);
            var zs = f.Add(FloorHazardKind.Stain, s, 0f);
            Assert.IsTrue(f.SlipActive && f.StressActive);
            var st = new PlayerStats();
            float baseInterval = st.AttackInterval;
            st.SetFloorHazards(f.SlipActive, f.StressActive);
            Assert.AreEqual(baseInterval / FloorHazardRules.StressAttackMultiplier, st.AttackInterval, 1e-4f, "스트레스 = 공속 감소 1단계(곳 수와 무관)");
            StringAssert.Contains("미끄러짐(물기 2곳)", f.StatusText());
            StringAssert.Contains("스트레스(얼룩 1곳)", f.StatusText());
            for (int i = 0; i < 40; i++) f.Tick(0.1f, a, true);
            Assert.IsTrue(za.Cleaned); Assert.IsFalse(zb.Cleaned); Assert.IsFalse(zs.Cleaned);
            Assert.IsTrue(f.SlipActive, "같은 계열 구역이 남아 있으면 미끄러짐 유지");
            for (int i = 0; i < 40; i++) f.Tick(0.1f, b, true);
            Assert.IsFalse(f.SlipActive, "물기 모두 청소 → 미끄러짐 해제");
            Assert.IsTrue(f.StressActive, "얼룩은 따로");
            for (int i = 0; i < 40; i++) f.Tick(0.1f, s, true);
            Assert.IsFalse(f.AnyDebuff);
            st.SetFloorHazards(false, false);
            Assert.AreEqual(baseInterval, st.AttackInterval, 1e-4f);
        }

        [Test]
        public void Caps_TotalAndPerKind_AndClear()
        {
            var f = new FloorHazardField();
            Assert.AreEqual(FloorHazardKind.Water, f.NextKind(0));
            f.Add(FloorHazardKind.Water, new Vector2(0, 0), 0f);
            f.Add(FloorHazardKind.Water, new Vector2(5, 0), 0f);
            Assert.AreEqual(FloorHazardKind.Stain, f.NextKind(2), "물기 상한이면 얼룩");
            f.Add(FloorHazardKind.Stain, new Vector2(10, 0), 0f);
            Assert.IsNull(f.NextKind(3), "총 상한 3");
            f.Clear();
            Assert.AreEqual(0, f.Total);
            Assert.IsFalse(f.AnyDebuff, "런 종료 정리 = 디버프도 꺼짐");
        }

        [Test]
        public void Placement_NotUnderPlayer_NotAtStart_NoOverlap_InsideLaneAndView()
        {
            var player = new Vector2(10f, Balance.LaneCenterY);
            var start = new Vector2(0f, Balance.LaneCenterY);
            float minX = player.x - 10.6f, maxX = player.x + 10.6f;
            var none = new List<FloorHazardZone>();
            Assert.IsFalse(FloorHazardRules.IsValidPlacement(player, 1f, player, start, minX, maxX, none), "발밑 금지");
            Assert.IsFalse(FloorHazardRules.IsValidPlacement(player + new Vector2(2.5f, 0f), 1f, player, start, minX, maxX, none), "바로 옆 금지");
            Assert.IsFalse(FloorHazardRules.IsValidPlacement(start + new Vector2(1f, 0f), 1f, start + new Vector2(5f, 0f), start, start.x - 10f, start.x + 10f, none), "시작 지점 금지");
            Assert.IsFalse(FloorHazardRules.IsValidPlacement(new Vector2(14f, Balance.DepthMax - 0.2f), 1f, player, start, minX, maxX, none), "레인 밖 금지");
            var existing = new List<FloorHazardZone> { new FloorHazardZone { Center = new Vector2(15f, Balance.LaneCenterY), Radius = 1f } };
            Assert.IsFalse(FloorHazardRules.IsValidPlacement(new Vector2(16f, Balance.LaneCenterY), 1f, player, start, minX, maxX, existing), "겹침 금지");
            var rng = new System.Random(1);
            int placed = 0;
            for (int i = 0; i < 50; i++)
            {
                if (!FloorHazardRules.TryPlace(FloorHazardKind.Water, player, start, minX, maxX, none, rng, out var pos)) continue;
                placed++;
                Assert.IsTrue(FloorHazardRules.IsValidPlacement(pos, FloorHazardRules.WaterRadius, player, start, minX, maxX, none));
                Assert.GreaterOrEqual((pos - player).magnitude - FloorHazardRules.WaterRadius, FloorHazardRules.MinPlayerEdgeDistance);
            }
            Assert.Greater(placed, 40, "보통 위치에서는 거의 항상 배치");
        }

        [Test]
        public void SlipStep_NoForceWhenStill_ShortBoundedGlide_NeverFasterThanSpeed()
        {
            const float speed = 5f, dt = 0.02f;
            Assert.AreEqual(Vector2.zero, FloorHazardRules.SlipStep(Vector2.zero, Vector2.zero, speed, dt), "서 있으면 강제 가속 없음");
            var v = Vector2.right * speed; float dist = 0f, time = 0f;
            while (v.sqrMagnitude > 1e-6f && time < 2f) { v = FloorHazardRules.SlipStep(v, Vector2.zero, speed, dt); dist += v.magnitude * dt; time += dt; Assert.GreaterOrEqual(v.x, 0f, "현재 이동 방향으로만"); Assert.AreEqual(0f, v.y, 1e-5f); }
            Assert.LessOrEqual(time, FloorHazardRules.SlipResponseSeconds + 0.03f, "짧게 멈춘다");
            Assert.LessOrEqual(dist, speed * FloorHazardRules.SlipResponseSeconds * 0.5f + 0.05f, "미끄러짐 거리 상한");
            var u = Vector2.zero;
            for (int i = 0; i < 100; i++) { u = FloorHazardRules.SlipStep(u, new Vector2(0.6f, 0.8f) * speed, speed, dt); Assert.LessOrEqual(u.magnitude, speed + 1e-4f); }
            Assert.AreEqual(speed, u.magnitude, 1e-3f, "입력 방향으로 결국 정상 속도");
        }

        [Test]
        public void StatusText_ShowsKindCountAndCleaningProgress_EmptyWhenNone()
        {
            Assert.AreEqual("", FloorHazardField.StatusText(0, 0, null));
            StringAssert.Contains("머물러 청소", FloorHazardField.StatusText(1, 0, null));
            var z = new FloorHazardZone { Kind = FloorHazardKind.Stain, CleanProgress = 1.24f };
            StringAssert.Contains("얼룩 청소 1.2/3초", FloorHazardField.StatusText(0, 1, z));
        }
    }
}
