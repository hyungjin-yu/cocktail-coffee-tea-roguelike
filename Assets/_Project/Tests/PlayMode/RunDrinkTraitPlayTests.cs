using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-12 런 음료 보조 전투 특성 실제 프레임: 제조 → 마시기 → 실제 직접 적중(TakeDirectHit). 적·피해는 시험 코드가 직접 둔다(자연 플레이 아님).
    /// </summary>
    public class RunDrinkTraitPlayTests
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
            Assert.IsTrue(game.Master.StartBattle());
            foreach (var sp in game.GetComponentsInChildren<EnemySpawner>()) sp.enabled = false;
            game.Master.enabled = false; // 웨이브 생성 없이 시험 적만
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.DestroyImmediate(e.gameObject);
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

        static DrinkRecipe Preset(string name) => DrinkLab.ExamplePresets.First(r => r.name.Contains(name)).Clone();

        RunCup CraftAndDrink(string preset)
        {
            var r = Preset(preset);
            foreach (var s in r.steps) DrinkLab.Data.Add(s.id, s.amount);
            var res = game.CraftRunCup(r);
            Assert.IsTrue(res.Ok, res.Message);
            Assert.IsTrue(game.DrinkRunCup(res.Cup, out var err), err);
            return res.Cup;
        }

        Enemy Spawn(EnemyDef def, Vector2 offset, float hpScale = 50f) =>
            Enemy.SpawnAt((Vector2)game.Player.Position + offset, def, hpScale, game.Player, game.transform);

        [UnityTest]
        public IEnumerator Espresso_EliteOnly_Plus20_AffinityFromBaseNotBoosted_ReplacedByOtherCup()
        {
            CraftAndDrink("에스프레소");
            Assert.AreEqual(RunDrinkTraitKind.EspressoFocus, game.Player.DrinkTrait);
            Assert.AreEqual(DrinkKind.Coffee, game.Player.AffinityDrink, "커피 → 차 계열 상성");
            var guardian = Spawn(EnemyDef.TeaCeremonyGuardian, new Vector2(14f, 0f)); // 차 계열 강적
            var imp = Spawn(EnemyDef.TeabagImp, new Vector2(-14f, 0f));            // 차 계열 일반
            var brute = Spawn(EnemyDef.Brute, new Vector2(0f, 14f));               // 칵테일 계열 강적(상성 없음)
            yield return null;
            var r1 = guardian.TakeDirectHit(100f, HitKind.Normal);
            Assert.AreEqual(100, r1.baseDamage);
            Assert.AreEqual(20, r1.drinkBonus, "강적 +20%");
            Assert.AreEqual(20, r1.bonusDamage, "상성 A = 보너스 전 B×20%(증폭 없음)");
            Assert.AreEqual(140, r1.total);
            var r2 = imp.TakeDirectHit(100f, HitKind.Normal);
            Assert.AreEqual(0, r2.drinkBonus, "일반 적 변화 없음");
            Assert.AreEqual(20, r2.bonusDamage);
            var r3 = brute.TakeDirectHit(100f, HitKind.Normal);
            Assert.AreEqual(20, r3.drinkBonus);
            Assert.AreEqual(0, r3.bonusDamage, "비대상 계열 상성 0");
            Assert.AreEqual(120, r3.total);
            float before = brute.Hp;
            brute.TakeDamage(10f, HitKind.Dot);
            Assert.AreEqual(before - 10f, brute.Hp, 1e-3f, "지속 피해에는 특성 없음");
            // 다른 잔을 마시면 완전 교체
            CraftAndDrink("핸드 드립");
            Assert.AreEqual(RunDrinkTraitKind.DripSpread, game.Player.DrinkTrait);
            Assert.AreEqual(0, guardian.TakeDirectHit(100f, HitKind.Normal).drinkBonus, "에스프레소 특성 제거");
        }

        [UnityTest]
        public IEnumerator Drip_SplashTwoNearest_LowCoef_Cooldown_NoRecursion_KillCountsOnce()
        {
            CraftAndDrink("핸드 드립");
            var target = Spawn(EnemyDef.Imp, new Vector2(10f, 0f));
            var n1 = Spawn(EnemyDef.Imp, new Vector2(10.6f, 0f));
            var n2 = Spawn(EnemyDef.Imp, new Vector2(10f, 0.9f));
            var n3 = Spawn(EnemyDef.Imp, new Vector2(11.3f, 0.6f));        // 범위 안이지만 세 번째로 가까움
            var far = Spawn(EnemyDef.Imp, new Vector2(13f, 0f));           // 범위 밖
            yield return null;
            float h1 = n1.Hp, h2 = n2.Hp, h3 = n3.Hp, hf = far.Hp;
            int splash0 = RunDrinkTrait.SplashHits;
            target.TakeDirectHit(40f, HitKind.Normal);
            Assert.AreEqual(splash0 + 2, RunDrinkTrait.SplashHits, "최대 2명, 보조 적중이 다시 보조 적중을 부르지 않음");
            int expect = Mathf.Max(1, IntNum.Round(40 * RunDrinkTrait.DripCoef));
            Assert.AreEqual(h1 - expect, n1.Hp, 1e-3f);
            Assert.AreEqual(h2 - expect, n2.Hp, 1e-3f);
            Assert.AreEqual(h3, n3.Hp, 1e-3f, "가까운 2명만");
            Assert.AreEqual(hf, far.Hp, 1e-3f, "범위 밖 없음");
            target.TakeDirectHit(40f, HitKind.Normal);
            Assert.AreEqual(splash0 + 2, RunDrinkTrait.SplashHits, "0.2초 간격 제한");
            yield return new WaitForSeconds(RunDrinkTrait.DripInterval + 0.05f);
            // 보조 적중으로 처치 → 처치 1회
            var weak = Spawn(EnemyDef.Imp, (Vector2)target.transform.position - (Vector2)game.Player.Position + new Vector2(0.2f, -0.2f), 0.01f);
            int kills = 0; weak.Killed += _ => kills++;
            yield return null;
            target.TakeDirectHit(200f, HitKind.Normal);
            yield return null;
            Assert.IsTrue(weak == null || weak.IsDead, "보조 적중 실제 처치");
            Assert.AreEqual(1, kills, "처치 1회");
        }

        [UnityTest]
        public IEnumerator TeaSteeps_FirstAttackSpeed_SecondSlowRefreshNoStackBossExempt_ThirdSplashThree_ReplaceClears()
        {
            float baseAs = game.Stats.AttackSpeedBonus;
            var cup1 = CraftAndDrink("센차");
            Assert.AreEqual(RunDrinkTraitKind.TeaFirst, game.Player.DrinkTrait);
            Assert.AreEqual(baseAs + RunDrinkTrait.TeaFirstAttackSpeed, game.Stats.AttackSpeedBonus, 1e-4f, "공격 속도 +15%(전용 슬롯)");
            // 이어 우리기 → 2번째 마시기
            var c2 = game.ContinueRunTea(cup1);
            Assert.IsTrue(c2.Ok, c2.Message);
            Assert.IsFalse(game.SubmitRunCup(c2.Cup, out var err) , "2번째 제출 불가");
            StringAssert.Contains("제출할 수 없습니다", err);
            Assert.IsTrue(game.DrinkRunCup(c2.Cup, out _));
            Assert.AreEqual(RunDrinkTraitKind.TeaSecond, game.Player.DrinkTrait);
            Assert.AreEqual(baseAs, game.Stats.AttackSpeedBonus, 1e-4f, "1번째 공격 속도 제거(누적 없음)");
            var e = Spawn(EnemyDef.Imp, new Vector2(14f, 0f)); // 플레이어 실제 자동 공격 사거리 밖(둔화 해제 확인이 흔들리지 않게)
            var boss = Spawn(EnemyDef.DifficultCustomer, new Vector2(-10f, 0f), 1f);
            yield return null;
            float status = e.StatusSpeedMultiplier;
            e.TakeDirectHit(10f, HitKind.Normal);
            Assert.IsTrue(e.DrinkSlowed);
            Assert.AreEqual(RunDrinkTrait.TeaSlowFactor, e.DrinkSlowMultiplier, 1e-4f);
            Assert.AreEqual(status, e.StatusSpeedMultiplier, "기존 상태 둔화 슬롯은 건드리지 않음");
            yield return new WaitForSeconds(0.8f);
            e.TakeDirectHit(10f, HitKind.Normal);
            Assert.AreEqual(RunDrinkTrait.TeaSlowFactor, e.DrinkSlowMultiplier, 1e-4f, "다시 맞혀도 중첩 없음(0.6×0.6 아님)");
            Assert.Greater(e.DrinkSlowRemaining, RunDrinkTrait.TeaSlowSeconds - 0.2f, "시간만 갱신");
            boss.TakeDirectHit(10f, HitKind.Normal);
            Assert.IsFalse(boss.DrinkSlowed, "보스 제외");
            yield return new WaitForSeconds(RunDrinkTrait.TeaSlowSeconds + 0.1f);
            Assert.IsFalse(e.DrinkSlowed, "시간 지나면 해제");
            Assert.AreEqual(1f, e.DrinkSlowMultiplier);
            Object.DestroyImmediate(boss.gameObject);
            // 3번째
            var c3 = game.ContinueRunTea(c2.Cup);
            Assert.IsTrue(c3.Ok, c3.Message);
            Assert.IsTrue(game.DrinkRunCup(c3.Cup, out _));
            Assert.AreEqual(RunDrinkTraitKind.TeaThird, game.Player.DrinkTrait);
            Assert.IsFalse(game.ContinueRunTea(c3.Cup).Ok, "3번째 이후 이어 우리기 없음");
            var t = Spawn(EnemyDef.Imp, new Vector2(10f, 5f));
            var ns = new[] { new Vector2(10.5f, 5f), new Vector2(10f, 5.6f), new Vector2(9.4f, 5f), new Vector2(10.3f, 4.2f) }.Select(o => Spawn(EnemyDef.Imp, o)).ToList();
            yield return null;
            int s0 = RunDrinkTrait.SplashHits;
            var hp = ns.Select(x => x.Hp).ToList();
            t.TakeDirectHit(40f, HitKind.Normal);
            Assert.AreEqual(s0 + RunDrinkTrait.TeaThirdTargets, RunDrinkTrait.SplashHits, "최대 3명");
            int hitCount = ns.Where((x, i) => x.Hp < hp[i]).Count();
            Assert.AreEqual(3, hitCount);
            Assert.IsFalse(t.DrinkSlowed, "3번째는 둔화 없음(현재 단계 하나만)");
            // 칵테일을 마시면 차 특성 제거
            CraftAndDrink("드라이 마티니");
            Assert.AreEqual(RunDrinkTraitKind.None, game.Player.DrinkTrait);
            int s1 = RunDrinkTrait.SplashHits;
            yield return new WaitForSeconds(0.3f);
            t.TakeDirectHit(40f, HitKind.Normal);
            Assert.AreEqual(s1, RunDrinkTrait.SplashHits, "교체 후 보조 적중 없음");
        }

        [UnityTest]
        public IEnumerator BrewWindow_ShowsTraitOnDrink_ContinueButton_FooterCurrentTrait()
        {
            var cup = CraftAndDrink("센차");
            Assert.IsTrue(game.OpenMasterBrew());
            yield return null;
            var continueBtn = game.MasterHud.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(b => b.gameObject.name == "ContinueSteep");
            Assert.IsNotNull(continueBtn, "마신 차에 이어 우리기 버튼");
            StringAssert.Contains("현재 잔 특성: 차 1번째 우림", game.MasterHud.BrewFooterText);
            StringAssert.Contains("차 우림(게임 규칙)", game.MasterHud.TraitBoxText);
            StringAssert.Contains("공격 속도 +15%", game.MasterHud.TraitBoxText, "실제 효과 수치");
            continueBtn.onClick.Invoke();
            yield return null;
            continueBtn.onClick.Invoke(); // 이미 파괴됐거나 다시 눌러도 하나만
            yield return null;
            Assert.AreEqual(1, DrinkLab.RunCupsFor(game.RunId).Count(c => c.Steep == 2), "중복 클릭으로 2번째 잔 하나만");
            game.CloseMasterBrew();
            Assert.IsTrue(game.OpenMasterBrew()); // 창 재열기로 단계·재료 변화 없음
            yield return null;
            Assert.AreEqual(1, DrinkLab.RunCupsFor(game.RunId).Count(c => c.Steep == 2));
            var rows = game.MasterHud.GetComponentsInChildren<UnityEngine.UI.Text>(false).Select(x => x.text).ToList();
            Assert.IsTrue(rows.Any(x => x.Contains("2번째 우림") && x.Contains("마시면 → 적중 둔화")), "보관 잔에 마시면 바뀌는 특성 표시");
            Assert.IsTrue(game.MasterHud.GetComponentsInChildren<UnityEngine.UI.Text>(false).Any(x => x.text == "제출 불가"), "2번째 제출 불가 표시");
            game.CloseMasterBrew();
        }
    }
}
