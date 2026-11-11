using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-11 실제 프레임 마스터 도전 수직 구간: 수집 5 → 반복 → 직접 박람회 도전 → 남은 적 처리 대기 → 현수막 3.5초(전투·피해·시간 정지)
    /// → 박람회 3웨이브(작은 진상 손님, 클리어 플래그 없음) → 성공 보상 1회 → 보상 손님 2웨이브(접촉 무피해) → 다음 주기 스냅샷.
    /// 적 처치는 시험 코드가 직접 피해를 줘서 진행한다(자연 플레이 아님 — 자동 상태 진행 시험).
    /// </summary>
    public class MasterChallengePlayTests
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
            game.Stats.baseMaxHp = 100000f; // 가속 진행 중 사망 방지(피해 판정은 그대로)
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

        static void KillAll()
        {
            foreach (var e in Enemy.Active.ToList()) if (e != null && !e.IsDead) e.TakeDamage(1e9f);
        }

        IEnumerator RunUntil(System.Func<bool> done, float realSeconds, bool kill = true)
        {
            float until = Time.realtimeSinceStartup + realSeconds;
            while (!done() && Time.realtimeSinceStartup < until)
            {
                Dismiss();
                if (kill && !game.IsPaused && game.State == GameState.Playing) KillAll();
                yield return null;
            }
            Assert.IsTrue(done(), "시간 안에 조건 도달");
        }

        [UnityTest]
        public IEnumerator FullCycle_RealFrames_RepeatChallengeFreezeFairRewardNextCycle()
        {
            var st = game.Master.State;
            Time.timeScale = 10f;
            int maxAlive = 0;
            yield return RunUntil(() => { maxAlive = Mathf.Max(maxAlive, Enemy.Active.Count); return st.Phase == MasterPhase.AwaitChoice; }, 40f);
            Assert.AreEqual(5, st.Wave);
            Assert.LessOrEqual(maxAlive, Balance.MaxAliveEnemiesAbsolute, "동시 상한");
            Assert.IsTrue(game.MasterHud.ChoiceVisible, "선택 패널 표시");
            StringAssert.Contains("박람회에 도전", game.MasterHud.ChoiceText);
            // 대기 중 신규 생성 없음(자동 박람회 진입 없음)
            long spawnedBefore = st.Spawned;
            for (int i = 0; i < 30; i++) { Dismiss(); yield return null; }
            Assert.AreEqual(MasterPhase.AwaitChoice, st.Phase);
            Assert.AreEqual(spawnedBefore, st.Spawned);

            Assert.IsTrue(game.Master.ChooseRepeat());
            Assert.AreEqual(1, st.Wave);
            yield return RunUntil(() => st.Phase == MasterPhase.AwaitChoice, 40f);
            Assert.AreEqual(1, st.Cycle, "반복해도 주기 그대로");

            // 박람회 도전 접수 + 남은 적이 있으면 대기(삭제하지 않음)
            Time.timeScale = 1f;
            var leftover = game.Spawner.SpawnMaster(EnemyDef.Imp, 1f, 1f, false, false);
            Assert.IsNotNull(leftover);
            Assert.IsTrue(game.Master.ChooseFair());
            Assert.IsFalse(game.Master.ChooseFair(), "중복 도전 입력 무시");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(MasterPhase.ClearingForFair, st.Phase, "남은 적 처리 전에는 설치 시작 없음");
            Assert.IsTrue(leftover != null && !leftover.IsDead, "남은 적을 갑자기 삭제하지 않음");
            leftover.TakeDamage(1e9f);
            yield return RunUntil(() => st.Phase == MasterPhase.Transition, 5f, kill: false);
            float started = Time.realtimeSinceStartup;
            Assert.IsTrue(game.MasterFreezeActive);
            Assert.AreEqual(0f, Time.timeScale);

            // 설치 중 위험 요소: 접촉 적·투사체·장판을 플레이어 위에 둔다 → 피해 0, 전투 시간·생성 정지
            var p = game.Player.Position;
            var hazard = Enemy.SpawnAt(p, EnemyDef.Brute, 1f, game.Player, game.Spawner.transform);
            EnemyProjectile.Spawn(p + Vector2.right * 0.2f, Vector2.left, 6f, 30f, game.Player, game.transform);
            BurstZone.Spawn(p, 2f, 50f, 10f, game.Player, game.transform);
            float hp = game.Player.Hp, runTime = game.RunTime;
            long spawned = st.Spawned;
            while (Time.realtimeSinceStartup - started < 3.0f)
            {
                game.RequestEscape(); // 중복 입력·Esc로 상태가 꼬이지 않는다
                Assert.IsFalse(game.Master.ChooseFair());
                yield return null;
            }
            Assert.AreEqual(MasterPhase.Transition, st.Phase, "3초 시점 아직 설치 중");
            Assert.AreEqual(hp, game.Player.Hp, "설치 중 피해 0(접촉·투사체·장판)");
            Assert.AreEqual(runTime, game.RunTime, "전투 시간 정지");
            Assert.AreEqual(spawned, st.Spawned, "생성 정지");
            Assert.IsFalse(game.PauseView.IsOpen);
            Object.Destroy(hazard.gameObject);
            foreach (var z in Object.FindObjectsByType<BurstZone>(FindObjectsSortMode.None)) Object.Destroy(z.gameObject);
            foreach (var pr in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Object.Destroy(pr.gameObject);
            yield return RunUntil(() => st.Phase == MasterPhase.Wave, 3f, kill: false);
            float took = Time.realtimeSinceStartup - started;
            Assert.That(took, Is.InRange(3.3f, 4.4f), "현수막 설치 3.5초(실제 시간 상태 타이머)");
            Assert.IsFalse(game.MasterFreezeActive);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(6, st.Wave);
            var hung = Object.FindObjectsByType<FairBannerShow>(FindObjectsSortMode.None).Single();
            Assert.IsTrue(hung.Settled && !hung.NpcsPresent, "설치 완료 후 현수막만 남음");
            Assert.AreEqual("박람회 1/3", hung.LabelText, "박람회 진행은 천 글자로");
            Assert.AreEqual("", game.Master.LastBannerTitle, "현수막이 걸린 동안 HUD 큰 제목 없음(천과 겹침 방지)");
            StringAssert.Contains("박람회 시작!", game.Master.LastBannerSub);

            // 박람회 3웨이브: 작은 진상 손님만, 클리어 없음
            Time.timeScale = 10f;
            var fairDef = MasterDefs.FairCustomer(game.Job);
            bool sawOther = false;
            yield return RunUntil(() =>
            {
                foreach (var e in Enemy.Active) if (e != null && e.MasterWaveTag >= 6 && e.MasterWaveTag <= 8 && e.Def != fairDef) sawOther = true;
                return st.Wave == 9;
            }, 40f);
            Assert.IsFalse(sawOther, "박람회는 진상 손님 전용");
            Assert.AreEqual("박람회 성공!", game.Master.LastBannerTitle, "성공 안내가 보상 웨이브 제목에 덮이지 않음");
            StringAssert.Contains("보상 손님", game.Master.LastBannerSub);
            Assert.AreEqual(GameState.Playing, game.State, "최종 보스 클리어 플래그 없음");
            var awards = DrinkLab.PendingFor(game.RunId).Where(a => a.awardId == "master_fair_c1").ToList();
            Assert.AreEqual(1, awards.Count, "박람회 성공 보상 1회");
            Assert.IsTrue(awards[0].endless, "무한 출처 장부");
            Assert.AreEqual(DrinkSupply.Table.Length, awards[0].items.Count);

            // 보상 손님: 접촉해도 피해 0, 1웨이브 기준 HP
            Time.timeScale = 1f;
            yield return RunUntil(() => Enemy.Active.Count(e => e != null && e.Harmless) >= 3, 10f, kill: false);
            var rewardDef = MasterDefs.RewardMonster(game.Job);
            float hp2 = game.Player.Hp;
            foreach (var e in Enemy.Active.Where(e => e != null && e.Harmless).ToList())
            {
                Assert.AreEqual(rewardDef, e.Def);
                Assert.AreEqual(Mathf.Max(1, IntNum.Round(rewardDef.hp)), e.MaxHp, "절대 웨이브1 기준 HP(도전 배율 제외)");
                Assert.IsNull(e.GetComponent<RangedAttackDriver>());
                Assert.IsNull(e.GetComponent<SummonerDriver>());
                e.transform.position = game.Player.Position;
            }
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until) { Dismiss(); foreach (var e in Enemy.Active.Where(e => e != null && e.Harmless)) e.transform.position = game.Player.Position; yield return null; }
            Assert.AreEqual(hp2, game.Player.Hp, "보상 손님 접촉 피해 0");
            Time.timeScale = 10f;
            yield return RunUntil(() => st.Wave == 11, 30f);
            Assert.AreEqual(2, st.Cycle);
            Assert.AreEqual(MasterSegment.Collect, st.Segment);
            Assert.AreEqual(0, st.CarriedReduction, "제출 0잔 → 다음 주기 약화 0");
            Assert.IsNull(game.ActiveBoss);
            Assert.IsFalse(Object.FindObjectsByType<FairBannerShow>(FindObjectsSortMode.None).Any(), "보상 구간에서 현수막 정리");
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator BrewButton_OpensPausedWindow_NoForcedPopupOnPickup()
        {
            Assert.IsFalse(game.MasterHud.BrewOpen);
            DrinkLab.RecordBossAward(game.RunId, "qa_pick", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 150) }, true);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.IsFalse(game.MasterHud.BrewOpen, "재료 획득으로 창이 강제로 열리지 않음");
            Assert.IsTrue(game.OpenMasterBrew());
            float t = game.RunTime; long spawned = game.Master.State.Spawned;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(t, game.RunTime, "제조 창 동안 전투 정지");
            Assert.AreEqual(spawned, game.Master.State.Spawned);
            game.RequestEscape();
            yield return null;
            Assert.IsFalse(game.MasterHud.BrewOpen);
            Assert.AreEqual(GameState.Playing, game.State);
        }
    }
}
