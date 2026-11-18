using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 81 커피 추출법별 전투 리듬: 마신 커피의 추출법마다 다른 결정(에스프레소 정지 충전 샷·핸드 드립 뜸/붓기 주기·모카포트 증기압 폭발)이
    /// 런에 붙고 HUD 칩에 표시된다. 칵테일·차·무음료 런은 그대로이고, 음료는 입장 때 한 번만 적용된다. 같은 입력이면 같은 결과다.
    /// 수치는 임시 게임 밸런스(CoffeeBrewRuntime 상수)이며 실제 카페인 효능이 아니다.
    /// </summary>
    public class CoffeeBrewCombatPlayTests
    {
        string folder;
        GameManager game;
        readonly List<string> folders = new List<string>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            folder = Path.Combine(Application.temporaryCachePath, "coffee-combat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            folders.Add(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return EndGame();
            Time.timeScale = 1f;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var f in folders) try { Directory.Delete(f, true); } catch { }
            folders.Clear();
        }

        IEnumerator EndGame()
        {
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
        }

        static DrinkRecipe Coffee(CoffeeBrewMethod m)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            r.name = "시험 커피";
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, m == CoffeeBrewMethod.EspressoMachine ? 180 : 150));
            return r;
        }

        static DrinkRecipe Cocktail()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.name = "시험 칵테일";
            r.steps.Add(new DrinkStep(DrinkIngredients.Gin, 200));
            r.steps.Add(new DrinkStep(DrinkIngredients.Grenadine, 100));
            return r;
        }

        static DrinkRecipe Tea()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Tea);
            r.name = "시험 차";
            r.steps.Add(new DrinkStep(DrinkIngredients.BlackTea, 30));
            return r;
        }

        /// <summary>새 저장 폴더(원두 첫 지급 30g)로 바꿔 recipe를 만들고(없으면 무음료) 런을 시작한다. 스폰·보스는 끈다.</summary>
        IEnumerator StartRun(DrinkRecipe recipe, bool freshSave = false)
        {
            yield return EndGame();
            if (freshSave)
            {
                var f = Path.Combine(Application.temporaryCachePath, "coffee-combat-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(f);
                folders.Add(f);
                PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
                DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
            }
            if (recipe != null)
            {
                var craft = DrinkLab.Craft(recipe);
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            yield return null;
            game.Player.Coffee?.ResetStateForTests(); // 시작 프레임에 흐른 시간 제거 — 이후 Step으로만 진행(결정적)
        }

        CoffeeBrewRuntime Coffee() => game.Player.Coffee;

        [UnityTest]
        public IEnumerator EachMethod_AttachesOnce_ShowsMethodInHud_KeepsRangeBuffSingle()
        {
            foreach (var m in CoffeeBrewing.All)
            {
                yield return StartRun(Coffee(m), freshSave: true);
                var rt = Coffee();
                Assert.IsNotNull(rt, $"{m}: 커피 런에는 추출법 리듬이 붙는다");
                Assert.AreEqual(m, rt.Method);
                Assert.AreEqual(1, game.Player.GetComponents<CoffeeBrewRuntime>().Length);
                Assert.Greater(game.DrinkBuff[DrinkStat.Range], 0f, "기존 커피 사거리 버프 유지");
                Assert.AreEqual(IntNum.QuantizePercent(game.DrinkBuff[DrinkStat.Range]), game.Stats.rangePercent, 1e-5f, "사거리 버프는 입장 때 한 번만(정수 % 적용)");

                // 다시 붙여도 같은 인스턴스·같은 스탯(이중 적용 없음)
                float range = game.Stats.rangePercent, hp = game.Stats.MaxHp;
                Assert.AreSame(rt, CoffeeBrewRuntime.Attach(game.Player, m));
                Assert.AreEqual(1, game.Player.GetComponents<CoffeeBrewRuntime>().Length);
                Assert.AreEqual(range, game.Stats.rangePercent, 1e-6f);
                Assert.AreEqual(hp, game.Stats.MaxHp, 1e-6f);

                game.DrinkHud.RefreshForTests();
                StringAssert.StartsWith(CoffeeBrewing.ButtonName(m) + " · ", game.DrinkHud.ChipCoffee.text, "HUD 칩에 추출법과 상태 표시");
                Assert.IsTrue(game.DrinkHud.ChipCoffee.gameObject.activeInHierarchy, "커피 상태 줄이 보인다");
            }
        }

        [UnityTest]
        public IEnumerator Espresso_StandStillPullsShot_MovingResetsCharge_ThenRecharges()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.EspressoMachine));
            var rt = Coffee();
            var s = game.Stats;
            float baseInterval = s.AttackInterval, baseMove = s.MoveSpeed, baseRange = s.Range;

            rt.Step(0.6f, false);
            Assert.AreEqual(0.6f, rt.Charge, 1e-4f);
            rt.Step(0.1f, true);
            Assert.AreEqual(0f, rt.Charge, "움직이면 압력이 빠진다");
            Assert.IsFalse(rt.ShotActive);

            rt.Step(0.5f, false);
            rt.Step(0.5f, false);
            Assert.IsTrue(rt.ShotActive, "1초 멈춰 서면 샷 집중");
            Assert.AreEqual(1, rt.ShotsPulled);
            Assert.AreEqual(CoffeeBrewRuntime.EspressoShotAttackSpeed, s.coffeeAttackSpeed, 1e-6f);
            Assert.Less(s.AttackInterval, baseInterval, "샷 동안 공격 간격이 짧아진다");
            Assert.AreEqual(baseMove, s.MoveSpeed, 1e-5f, "에스프레소는 이동 속도를 바꾸지 않는다");
            Assert.AreEqual(baseRange, s.Range, 1e-5f, "에스프레소는 사거리를 더하지 않는다");
            StringAssert.Contains("샷", rt.StatusLine());

            rt.Step(1.0f, true); // 샷 중에는 움직여도 유지
            Assert.IsTrue(rt.ShotActive);
            rt.Step(2.1f, true);
            Assert.IsFalse(rt.ShotActive);
            Assert.Greater(rt.RechargeRemaining, 0f);
            Assert.AreEqual(baseInterval, s.AttackInterval, 1e-5f, "샷이 끝나면 원래 간격");
            StringAssert.Contains("다음 샷 준비", rt.StatusLine());

            rt.Step(CoffeeBrewRuntime.EspressoRechargeSeconds, false);
            rt.Step(0.5f, false);
            Assert.IsFalse(rt.ShotActive, "재충전 뒤 다시 멈춰 서야 한다");
            rt.Step(0.5f, false);
            Assert.IsTrue(rt.ShotActive);
            Assert.AreEqual(2, rt.ShotsPulled);
        }

        [UnityTest]
        public IEnumerator PourOver_FixedBloomPourCycle_SwapsMobilityAndReach()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.PourOver));
            var rt = Coffee();
            var s = game.Stats;
            float baseInterval = s.AttackInterval;
            float baseRange = s.baseRange * (1f + s.rangePercent);
            float baseMove = s.baseMoveSpeed * (1f + s.moveSpeedPercent);

            Assert.IsTrue(rt.Blooming, "뜸으로 시작");
            Assert.AreEqual(baseMove * (1f + CoffeeBrewRuntime.PourBloomMoveSpeed), s.MoveSpeed, 1e-4f);
            Assert.AreEqual(baseRange, s.Range, 1e-4f);
            StringAssert.Contains("뜸", rt.StatusLine());

            rt.Step(CoffeeBrewRuntime.PourBloomSeconds + 0.01f, true);
            Assert.IsFalse(rt.Blooming, "붓기");
            Assert.AreEqual(baseMove, s.MoveSpeed, 1e-4f);
            Assert.AreEqual(s.baseRange * (1f + s.rangePercent + CoffeeBrewRuntime.PourPourRange), s.Range, 1e-4f);
            Assert.AreEqual(baseInterval, s.AttackInterval, 1e-5f, "핸드 드립은 공격 간격을 바꾸지 않는다");
            StringAssert.Contains("붓기", rt.StatusLine());

            rt.Step(CoffeeBrewRuntime.PourPourSeconds, false);
            Assert.IsTrue(rt.Blooming, "주기가 돌아 다시 뜸");
            rt.OnDirectHit(null);
            Assert.AreEqual(0, rt.Heat, "핸드 드립은 증기압을 쌓지 않는다");
        }

        [UnityTest]
        public IEnumerator Moka_DirectHitsBuildHeat_BurstHitsNearbyOnly_DecaysWhenIdle()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.MokaPot));
            var rt = Coffee();
            var s = game.Stats;
            var def = new EnemyDef { name = "시험 몹", dropCount = 0, hp = 1000f, speed = 0f };
            var p = game.Player.Position;
            var near = Enemy.SpawnAt(p + new Vector2(1.5f, 0f), def, 1f, game.Player, game.transform);
            var far = Enemy.SpawnAt(p + new Vector2(CoffeeBrewRuntime.MokaBurstRadius + 2f, 0f), def, 1f, game.Player, game.transform);
            yield return null;
            rt.ResetStateForTests(); // 스폰 프레임의 자동 공격이 쌓은 증기압 제거
            float nearHp = near.Hp, farHp = far.Hp;
            float baseInterval = s.AttackInterval, baseMove = s.MoveSpeed, baseRange = s.Range;

            for (int i = 0; i < CoffeeBrewRuntime.MokaHeatMax - 1; i++) rt.OnDirectHit(near);
            Assert.AreEqual(CoffeeBrewRuntime.MokaHeatMax - 1, rt.Heat);
            Assert.AreEqual(0, rt.Bursts);
            Assert.AreEqual(nearHp, near.Hp, 1e-4f, "가득 차기 전에는 폭발 없음");
            StringAssert.Contains($"증기압 {CoffeeBrewRuntime.MokaHeatMax - 1}/{CoffeeBrewRuntime.MokaHeatMax}", rt.StatusLine());

            rt.OnDirectHit(near);
            Assert.AreEqual(1, rt.Bursts);
            Assert.AreEqual(0, rt.Heat, "폭발 뒤 증기압 0");
            Assert.AreEqual(1, rt.LastBurstHits, "반경 안의 적만");
            Assert.AreEqual(nearHp - IntNum.Damage(s.Damage * CoffeeBrewRuntime.MokaBurstDamageScale), near.Hp, 1e-3f, "정수 전용 피해");
            Assert.AreEqual(farHp, far.Hp, 1e-4f, "반경 밖은 맞지 않는다");
            Assert.IsTrue(near.Staggered, "일반 적은 짧게 경직");
            Assert.AreEqual(baseInterval, s.AttackInterval, 1e-5f);
            Assert.AreEqual(baseMove, s.MoveSpeed, 1e-5f);
            Assert.AreEqual(baseRange, s.Range, 1e-5f, "모카포트는 상시 스탯 보정이 없다");

            for (int i = 0; i < 3; i++) rt.OnDirectHit(near);
            rt.Step(CoffeeBrewRuntime.MokaHeatDecaySeconds + 0.01f, false);
            Assert.AreEqual(2, rt.Heat, "타격이 끊기면 증기압이 식는다");
            rt.Step(CoffeeBrewRuntime.MokaHeatDecaySeconds * 2f, false);
            Assert.AreEqual(0, rt.Heat);
        }

        [UnityTest]
        public IEnumerator SameInputs_ProduceSameTrace_PerMethod()
        {
            foreach (var m in CoffeeBrewing.All)
            {
                yield return StartRun(Coffee(m), freshSave: true);
                string a = Trace(Coffee(), game.Stats);
                yield return StartRun(Coffee(m), freshSave: true);
                string b = Trace(Coffee(), game.Stats);
                Assert.AreEqual(a, b, $"{m}: 같은 입력 → 같은 결과");
            }
            // 세 추출법은 같은 입력에서도 서로 다른 결과를 낸다.
            var traces = new HashSet<string>();
            foreach (var m in CoffeeBrewing.All)
            {
                yield return StartRun(Coffee(m), freshSave: true);
                traces.Add(Trace(Coffee(), game.Stats));
            }
            Assert.AreEqual(3, traces.Count);
        }

        /// <summary>고정 입력(정지·이동·직접 타격 섞음, 적 없음)에 대한 상태 기록.</summary>
        static string Trace(CoffeeBrewRuntime rt, PlayerStats s)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 120; i++)
            {
                bool moving = i % 17 < 5;
                rt.Step(0.1f, moving);
                if (i % 3 == 0) rt.OnDirectHit(null);
                if (i % 10 == 0) sb.Append($"{rt.StatusLine()}|{s.AttackInterval:0.0000}|{s.MoveSpeed:0.0000}|{s.Range:0.0000}|{rt.Bursts}\n");
            }
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator CocktailTeaAndNoDrink_HaveNoCoffeeRhythm()
        {
            foreach (var recipe in new[] { Cocktail(), Tea(), null })
            {
                yield return StartRun(recipe, freshSave: true);
                string label = recipe == null ? "무음료" : recipe.Kind.ToString();
                Assert.IsNull(game.Player.Coffee, label);
                Assert.IsNull(game.Player.GetComponent<CoffeeBrewRuntime>(), label);
                Assert.AreEqual(0f, game.Stats.coffeeAttackSpeed, label);
                Assert.AreEqual(0f, game.Stats.coffeeMoveSpeed, label);
                Assert.AreEqual(0f, game.Stats.coffeeRangePercent, label);
                if (recipe == null) { Assert.IsNull(game.DrinkBuff, label); continue; }
                Assert.IsNotNull(game.DrinkBuff, label);
                Assert.AreEqual(0f, game.DrinkBuff[DrinkStat.Range], label + ": 커피 사거리 없음");
                game.DrinkHud.RefreshForTests();
                Assert.AreEqual("", game.DrinkHud.ChipCoffee.text, label);
                Assert.IsFalse(game.DrinkHud.ChipCoffee.gameObject.activeSelf, label + ": 커피 줄 숨김");
            }
        }
    }
}
