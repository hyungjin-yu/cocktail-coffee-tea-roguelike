using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-12 다음 웨이브 예고·준비(실제 프레임): 준비 정지(시간·생성·피해), 제조 창/일시정지 복귀, 계획 = 실제 생성 순서, 재추첨 없음,
    /// 선택 대기 예고 = 반복/박람회 실제 계획, 보상 뒤 다음 주기 준비. 적 처치는 시험 코드가 직접 피해(자연 플레이 아님).
    /// </summary>
    public class MasterPrepPlayTests
    {
        GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            RunModeSelection.Current = RunMode.Endless;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            game = GameManager.Instance;
            game.Master.BannersEnabled = false;
            game.Stats.baseMaxHp = 100000f;
            game.Player.Heal(1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            RunModeSelection.Current = RunMode.Standard;
            yield return null;
        }

        void Dismiss()
        {
            if (game.State == GameState.LevelUp) game.CardView.Choose(0);
            else if (game.State == GameState.SkillSelect)
            {
                var node = game.Skills.Nodes.FirstOrDefault(n => game.Skills.IsAvailable(n));
                if (node != null) game.SkillView.Choose(node); else game.SkillView.Continue();
            }
        }

        static List<Enemy> WaveEnemies(long wave, ICollection<Enemy> exclude = null) =>
            Enemy.Active.Where(e => e != null && !e.IsDead && e.MasterWaveTag == wave && (exclude == null || !exclude.Contains(e))).ToList();

        IEnumerator RunUntil(System.Func<bool> done, float realSeconds, bool kill = true)
        {
            float until = Time.realtimeSinceStartup + realSeconds;
            while (!done() && Time.realtimeSinceStartup < until)
            {
                Dismiss();
                if (kill && !game.IsPaused && game.State == GameState.Playing)
                    foreach (var e in Enemy.Active.ToList()) if (e != null && !e.IsDead) e.TakeDamage(1e9f);
                yield return null;
            }
            Assert.IsTrue(done(), "시간 안에 조건 도달");
        }

        [UnityTest]
        public IEnumerator Prep_FreezesTimeSpawnDamage_BrewAndPauseReturnToPrep_SamePlan_ThenSpawnsPlanInOrder()
        {
            var st = game.Master.State;
            Assert.AreEqual(MasterPhase.Prep, st.Phase);
            Assert.IsTrue(game.MasterPrepActive);
            Assert.AreEqual(0f, Time.timeScale);
            yield return null;
            Assert.IsTrue(game.MasterHud.Prep.PanelVisible, "준비 패널");
            Assert.IsFalse(game.MasterHud.Prep.NoticeVisible, "준비 중엔 사전 예고 상자 대신 패널");
            var plan = game.Master.CurrentPlan;
            int built = game.Master.PlansBuilt;
            var ups = game.Master.UpcomingWaves();
            Assert.AreEqual(1, ups.Count);
            Assert.AreSame(plan, ups[0].plan, "예고 = 현재 웨이브 확정 계획");
            Assert.AreEqual(st.Target, plan.Count);
            var rows = game.MasterHud.Prep.RowTexts.ToList();
            Assert.IsNotEmpty(rows);
            StringAssert.Contains(plan.Groups()[0].def.name, rows[0], "대표 적 기존 이름");

            // 위험 요소를 붙여도 준비 중 피해·시간·생성 0
            var p = game.Player.Position;
            var hazard = Enemy.SpawnAt(p, EnemyDef.Brute, 1f, game.Player, game.Spawner.transform);
            float hp = game.Player.Hp, rt = game.RunTime;
            float until = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(hp, game.Player.Hp, "준비 중 피해 0");
            Assert.AreEqual(rt, game.RunTime, "준비 중 전투 시간 정지");
            Assert.AreEqual(0, st.Spawned, "준비 중 생성 0");
            Object.DestroyImmediate(hazard.gameObject);

            // 제조 창 열고 닫기 → 준비 정지 유지, 같은 계획(재추첨 없음)
            Assert.IsTrue(game.OpenMasterBrew());
            yield return null;
            Assert.IsFalse(game.MasterHud.Prep.PanelVisible, "제조 창 위에 준비 패널 겹치지 않음");
            game.RequestEscape();
            yield return null;
            Assert.IsFalse(game.MasterHud.BrewOpen);
            Assert.AreEqual(0f, Time.timeScale, "제조 창 닫아도 준비 정지 유지");
            Assert.IsTrue(game.MasterHud.Prep.PanelVisible);
            // 일시정지 열고 계속(1초 카운트다운) → 여전히 정지
            game.OpenPause();
            yield return null;
            game.ClosePause();
            until = Time.realtimeSinceStartup + 1.3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(0f, Time.timeScale, "일시정지 복귀 후에도 준비 정지 유지");
            Assert.AreEqual(MasterPhase.Prep, st.Phase);
            Assert.AreSame(plan, game.Master.CurrentPlan, "재개 시 같은 계획");
            Assert.AreEqual(built, game.Master.PlansBuilt, "UI 재열기로 재추첨 없음");

            // 전투 시작 → 계획 순서대로 생성
            Assert.IsTrue(game.Master.StartBattle());
            Assert.IsFalse(game.MasterPrepActive);
            Assert.AreEqual(1f, Time.timeScale);
            Time.timeScale = 6f;
            yield return RunUntil(() => st.Spawned >= 5, 15f, kill: false);
            Time.timeScale = 1f;
            var seen = game.Master.SpawnedThisWave.ToList();
            Assert.GreaterOrEqual(seen.Count, 5);
            CollectionAssert.AreEqual(plan.Defs.Take(seen.Count).ToList(), seen, "실제 생성 = 예고 계획 순서");
            Assert.IsTrue(game.MasterHud.Prep.NoticeVisible, "전투 중 다음 웨이브 사전 예고(비모달)");
            StringAssert.Contains("웨이브 2", game.MasterHud.Prep.NoticeText);
        }

        [UnityTest]
        public IEnumerator NextWavePlan_PreviewedDuringWave_IsTheOneSpawned_AndChoicePreviewsAreRealPlans()
        {
            var st = game.Master.State;
            Assert.IsTrue(game.Master.StartBattle());
            yield return null;
            var next = game.Master.UpcomingWaves().Single();
            Assert.AreEqual(2, next.plan.Wave);
            Time.timeScale = 10f;
            yield return RunUntil(() => st.Wave == 2 && st.Spawned >= 3, 30f);
            Assert.AreSame(next.plan, game.Master.CurrentPlan, "웨이브 1 중 예고한 계획 = 웨이브 2 실제 계획");
            CollectionAssert.AreEqual(next.plan.Defs.Take(game.Master.SpawnedThisWave.Count).ToList(), game.Master.SpawnedThisWave, "웨이브 2 실제 생성 = 웨이브 1 중 예고 계획");
            yield return RunUntil(() => st.Phase == MasterPhase.AwaitChoice, 60f);
            Time.timeScale = 1f;
            yield return null;
            Assert.IsTrue(game.MasterPrepActive, "선택 대기 = 정지");
            var ups = game.Master.UpcomingWaves();
            Assert.AreEqual(2, ups.Count);
            Assert.AreEqual("재료 모으기 반복", ups[0].choice);
            Assert.AreEqual(1, ups[0].plan.Wave);
            Assert.AreEqual(1, ups[0].plan.Repeat);
            Assert.AreEqual("박람회", ups[1].plan.Label);
            Assert.AreEqual(2, game.MasterHud.Prep.BlockHeaders.Count);
            // 제출하면 박람회 예고 약화가 바뀌지만 계획(적 구성)은 그대로
            var fairPlan = ups[1].plan;
            Assert.IsTrue(st.TrySubmit(150));
            var ups2 = game.Master.UpcomingWaves();
            Assert.AreSame(fairPlan, ups2[1].plan);
            Assert.AreEqual(8, ups2[1].reduction);
            Assert.IsFalse(ups2[1].reductionFinal, "접수 전 = 예상치");
            // 반복 선택 → 예고했던 반복 계획 그대로 생성
            var repeatPlan = ups[0].plan;
            var before = new HashSet<Enemy>(Enemy.Active.Where(e => e != null));
            Assert.IsTrue(game.Master.ChooseRepeat());
            Assert.IsFalse(game.MasterPrepActive);
            Assert.AreSame(repeatPlan, game.Master.CurrentPlan);
            Time.timeScale = 6f;
            yield return RunUntil(() => st.Spawned >= 3, 15f, kill: false);
            Time.timeScale = 1f;
            var seen = game.Master.SpawnedThisWave.ToList();
            Assert.GreaterOrEqual(seen.Count, 3);
            CollectionAssert.AreEqual(repeatPlan.Defs.Take(seen.Count).ToList(), seen, "반복 1웨이브 = 예고한 반복 계획");
        }

        /// <summary>2026-11-12 사용자 제보(첫 제조 창 우측 상단 '이상한 이펙트'): 첫 열기 빈 슬롯, 품질 효과와 같은 순간 창 안 설명 캡션,
        /// 창 뒤에 깔리는 HUD 배너 생략, 재생 중 닫으면 즉시 반납(다시 열어도 멈춘 프레임 없음).</summary>
        [UnityTest]
        public IEnumerator BrewWindow_QualityFx_CaptionInsideWindow_ReleasedOnClose_EmptyOnFirstOpen()
        {
            Assert.IsTrue(game.OpenMasterBrew(), "준비 단계에서 제조 창");
            yield return null;
            var slot = game.MasterHud.FxSlot;
            Assert.AreEqual(0, slot.GetComponentsInChildren<MasterVfx>(true).Count(v => v.Playing), "첫 열기: 효과 없음");
            Assert.IsFalse(game.MasterHud.QualityCaptionVisible);
            string bannerBefore = game.Hud.BannerSubText;
            DrinkQualityFx.Play(game, 100, DrinkQualityFx.Moment.Crafted, slot);
            Assert.AreEqual("quality_master", DrinkQualityFx.LastClip);
            Assert.IsTrue(game.MasterHud.QualityCaptionVisible, "효과 옆 설명");
            StringAssert.Contains("최고", game.MasterHud.QualityCaptionText);
            StringAssert.Contains("제조 완료", game.MasterHud.QualityCaptionText);
            Assert.AreEqual(bannerBefore, game.Hud.BannerSubText, "제조 창 뒤 HUD 배너 생략");
            yield return null;
            game.CloseMasterBrew(); // 재생 중 닫기
            Assert.AreEqual(0, MasterVfx.ActiveUi, "닫으면 즉시 반납");
            Assert.IsFalse(game.MasterHud.QualityCaptionVisible);
            Assert.IsFalse(game.MasterHud.SubmitCupShown);
            Assert.IsTrue(game.OpenMasterBrew());
            yield return null;
            Assert.AreEqual(0, slot.GetComponentsInChildren<MasterVfx>(true).Count(v => v.Playing), "다시 열어도 멈춘 프레임 없음");
            Assert.IsFalse(slot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(i => i.enabled && i.gameObject.activeInHierarchy && i.sprite != null), "슬롯에 보이는 그림 없음");
            game.CloseMasterBrew();
            // 창 밖 호출은 기존 HUD 배너 그대로
            DrinkQualityFx.Play(game, 100, DrinkQualityFx.Moment.Drunk, null);
            Assert.AreNotEqual(bannerBefore, game.Hud.BannerSubText);
        }

        [UnityTest]
        public IEnumerator GiveUp_FromPrep_EndsCleanly()
        {
            Assert.AreEqual(MasterPhase.Prep, game.Master.State.Phase);
            game.OpenPause();
            yield return null;
            game.GiveUp();
            float until = Time.realtimeSinceStartup + 0.7f; // HUD 갱신 주기(0.5초) 포함
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(game.MasterPrepActive, "포기 후 준비 정지 해제");
            Assert.IsFalse(game.MasterHud.Prep.PanelVisible, "결과 화면 위 준비 패널 없음");
            Assert.IsFalse(game.Master.StartBattle(), "종료 후 시작 무시");
        }
    }
}
