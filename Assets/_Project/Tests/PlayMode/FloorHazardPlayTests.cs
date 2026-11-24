using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-24 #22 바닥 위험 실제 런 경로: 예고 → 생성 즉시 디버프(구역 밖) → 오라·HUD → 진입 → 이탈 초기화 → 연속 체류 → 청소(구역·디버프·오라 즉시 제거),
    /// 미끄러짐 이동, 일시정지 중 진행 없음, 런 종료 정리, 마스터 도전 범위 밖.</summary>
    public class FloorHazardPlayTests
    {
        GameManager game;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        IEnumerator Run(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            game.Director.enabled = false;
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(1f);
        }

        void Teleport(Vector2 p)
        {
            game.Player.transform.position = p;
            var rb = game.Player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = p;
        }

        [UnityTest]
        public IEnumerator Water_ImmediateSlip_AuraAndHud_EnterLeaveResets_StayCleans_AllRemovedAtOnce()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow);
            var hz = game.Spawner.Hazards;
            var p = game.Player;
            var center = p.Position + new Vector2(5f, 0f);
            var z = hz.SpawnAt(FloorHazardKind.Water, center);
            Assert.IsTrue(hz.HasWarning(z), "예고 링");
            Assert.IsFalse(p.Stats.FloorSlip, "예고 중에는 디버프 없음");
            yield return new WaitForSeconds(FloorHazardRules.WarnSeconds + 0.2f);
            Assert.IsTrue(z.Active);
            Assert.IsFalse(hz.HasWarning(z)); Assert.IsTrue(hz.HasPool(z), "웅덩이 표시");
            Assert.IsFalse(z.Contains(p.Position), "플레이어는 구역 밖");
            Assert.IsTrue(p.Stats.FloorSlip, "생성 즉시 미끄러짐(구역 밖이어도)");
            Assert.IsTrue(hz.Aura != null && hz.Aura.Visible, "어두운 외곽 오라");
            yield return new WaitForSecondsRealtime(0.15f);
            StringAssert.Contains("미끄러짐(물기 1곳)", game.Hud.StatusLineText);

            // 진입 → 1초 → 이탈: 카운터 초기화, 디버프 유지
            Teleport(center);
            yield return new WaitForSeconds(1f);
            Assert.Greater(z.CleanProgress, 0.5f);
            Assert.Greater(hz.CleanRingProgress(z), 0.1f, "바닥 청소 링 진행");
            Teleport(center + new Vector2(4f, 0f));
            yield return null; yield return null;
            Assert.AreEqual(0f, z.CleanProgress, "나가면 초기화");
            Assert.IsTrue(p.Stats.FloorSlip, "떠나도 해제 없음");

            // 다시 진입해 연속 체류 → 청소
            Teleport(center);
            float waited = 0f;
            while (!z.Cleaned && waited < FloorHazardRules.CleanSeconds + 1f) { waited += Time.deltaTime; yield return null; }
            Assert.IsTrue(z.Cleaned, "연속 체류로 청소");
            Assert.GreaterOrEqual(waited, FloorHazardRules.CleanSeconds - 0.1f);
            Assert.IsFalse(p.Stats.FloorSlip, "청소 즉시 해제");
            Assert.IsFalse(hz.Aura.Visible, "마지막 디버프 해제 = 오라 즉시 사라짐");
            Assert.AreEqual(0, hz.ViewCount, "구역 그림 제거");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsFalse(game.Hud.StatusLineText.Contains("미끄러짐"));
        }

        [UnityTest]
        public IEnumerator Slip_GlidesBrieflyInMoveDirection_NoForceWhenStill()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var hz = game.Spawner.Hazards;
            var p = game.Player;
            hz.SpawnAt(FloorHazardKind.Water, p.Position + new Vector2(-6f, 0f), 0f);
            Assert.IsTrue(p.Stats.FloorSlip);
            var before = p.Position;
            yield return new WaitForSeconds(0.3f);
            Assert.Less((p.Position - before).magnitude, 0.01f, "서 있으면 움직이지 않음");
            p.AutoInputOverride = Vector2.right;
            yield return new WaitForSeconds(0.6f);
            p.AutoInputOverride = Vector2.zero;
            var stopAt = p.Position;
            yield return new WaitForSeconds(0.6f);
            float glide = p.Position.x - stopAt.x;
            Assert.Greater(glide, 0.05f, "현재 이동 방향으로 미끄러짐");
            Assert.Less(glide, 1.2f, "짧은 미끄러짐");
            Assert.AreEqual(stopAt.y, p.Position.y, 0.01f);
            p.AutoInputOverride = null;
        }

        [UnityTest]
        public IEnumerator Stain_StressAttackSlow_PauseFreezesCleaning_RunEndClearsEverything()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var hz = game.Spawner.Hazards;
            var p = game.Player;
            float baseInterval = p.Stats.AttackInterval;
            var center = p.Position + new Vector2(5f, 0f);
            var z = hz.SpawnAt(FloorHazardKind.Stain, center, 0f);
            Assert.IsTrue(p.Stats.FloorStress);
            Assert.AreEqual(baseInterval / FloorHazardRules.StressAttackMultiplier, p.Stats.AttackInterval, 1e-3f);
            hz.SpawnAt(FloorHazardKind.Stain, p.Position + new Vector2(-5f, 0f), 0f);
            Assert.AreEqual(baseInterval / FloorHazardRules.StressAttackMultiplier, p.Stats.AttackInterval, 1e-3f, "얼룩 2곳이어도 1단계");
            Teleport(center);
            yield return new WaitForSeconds(0.5f);
            float prog = z.CleanProgress;
            game.OpenPause();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(prog, z.CleanProgress, 1e-4f, "일시정지 중 진행 없음");
            game.ClosePause();
            yield return new WaitForSecondsRealtime(1.5f); // 재개 카운트다운
            game.MarkPlayerDiedForTests();
            for (int i = 0; i < 10 && game.State != GameState.GameOver; i++) yield return null;
            Assert.AreEqual(GameState.GameOver, game.State);
            yield return null;
            Assert.AreEqual(0, hz.Field.Total, "런 종료 = 구역 정리");
            Assert.IsFalse(p.Stats.FloorStress || p.Stats.FloorSlip, "디버프 정리");
            Assert.IsFalse(hz.Aura.Visible);
        }

        [UnityTest]
        public IEnumerator StandardSchedule_At120_PlacesWarnedZoneAwayFromPlayerAndStart_EndlessExcluded()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            Assert.AreEqual(RunMode.Standard, game.Mode);
            Assert.IsFalse(FloorHazardRules.ModeHasHazards(RunMode.Endless), "마스터 도전은 범위 밖");
            var hz = game.Spawner.Hazards;
            hz.TickSchedule(119f);
            Assert.AreEqual(0, hz.Spawned, "120초 전 없음");
            for (int i = 0; i < 5 && hz.Spawned == 0; i++) hz.TickSchedule(121f);
            Assert.AreEqual(1, hz.Spawned, "120초 첫 차례");
            var z = hz.Field.Zones[0];
            Assert.AreEqual(FloorHazardKind.Water, z.Kind);
            Assert.IsTrue(z.Warning, "반드시 예고부터");
            Assert.GreaterOrEqual((z.Center - game.Player.Position).magnitude - z.Radius, FloorHazardRules.MinPlayerEdgeDistance, "플레이어 바로 밑 금지");
            hz.TickSchedule(122f);
            Assert.AreEqual(1, hz.Spawned, "같은 차례 중복 생성 없음");
            yield return null;
        }
    }
}
