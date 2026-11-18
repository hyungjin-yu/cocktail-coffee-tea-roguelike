using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-16 피드백 26 선택형 '취기' 표시 프로토타입: 실제 런 경로(준비 음료 출발)로 끝없는/기본 모드, 칵테일/커피, 설정 켬/끔,
    /// 판 종료 뒤, 새 런, 일시정지 상태를 확인한다. 표시 전용 — 능력치(공격·공속·이속·체력)가 설정과 무관한지도 본다. 저장은 임시 폴더.
    /// </summary>
    public class FeedbackBatch4PlayTests
    {
        GameManager game;
        readonly List<string> folders = new List<string>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            NewSave();
            SettingsService.SetForTests(Path.Combine(folders[0], "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            RunModeSelection.Current = RunMode.Standard;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var f in folders) try { Directory.Delete(f, true); } catch { }
            folders.Clear();
        }

        void NewSave()
        {
            var f = Path.Combine(Application.temporaryCachePath, "fb4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(f);
            folders.Add(f);
            PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
        }

        static DrinkRecipe Cocktail()
        {
            var r = DrinkLab.PracticeRecipe.Clone();
            foreach (var s in r.steps) { int have = DrinkLab.Data.Owned(s.id); if (have < s.amount) DrinkLab.Data.Add(s.id, s.amount - have); }
            return r;
        }

        static DrinkRecipe Coffee()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.PourOver);
            r.name = "시험 커피";
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, 150));
            int have = DrinkLab.Data.Owned(DrinkIngredients.CoffeeBean); if (have < 150) DrinkLab.Data.Add(DrinkIngredients.CoffeeBean, 150 - have);
            return r;
        }

        IEnumerator StartRun(RunMode mode, Func<DrinkRecipe> recipe)
        {
            if (game != null) { UnityEngine.Object.Destroy(game.gameObject); game = null; yield return null; }
            NewSave();
            if (recipe != null)
            {
                var craft = DrinkLab.Craft(recipe());
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            RunModeSelection.Current = mode;
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            yield return null;
        }

        static string StatsKey(GameManager g) =>
            $"{g.Stats.DamageBonus:0.0000}|{g.Stats.AttackSpeedBonus:0.0000}|{g.Stats.MoveSpeed:0.0000}|{g.Stats.MaxHp:0.0}|{g.Player.Hp:0.0}";

        bool Tile => ActiveEffects.Collect(game).Any(e => e.id == "tipsy");

        [UnityTest]
        public IEnumerator Tipsy_DefaultOff_EndlessCocktailOnly_DisplayOnly_ClearedAtRunEnd()
        {
            Assert.IsFalse(SettingsService.Current.tipsyBadge, "기본 꺼짐");

            yield return StartRun(RunMode.Endless, Cocktail);
            Assert.AreEqual(DrinkKind.Cocktail, game.Player.DrunkDrink, "실제 출발 음료 경로로 칵테일을 마심");
            Assert.IsFalse(Tile, "설정 꺼짐이면 표시 없음");
            string before = StatsKey(game);
            SettingsService.SetTipsyBadge(true);
            Assert.IsTrue(Tile, "끝없는 모드 + 칵테일 + 켬 = 배지");
            var tile = ActiveEffects.Collect(game).First(e => e.id == "tipsy");
            StringAssert.Contains("표시 전용", tile.detail);
            Assert.AreEqual(before, StatsKey(game), "켜도 능력치·체력 그대로(표시 전용)");
            SettingsService.SetTipsyBadge(false);
            Assert.IsFalse(Tile);
            Assert.AreEqual(before, StatsKey(game));
            SettingsService.SetTipsyBadge(true);

            game.GiveUp(); yield return null;
            Assert.IsFalse(Tile, "판 종료(포기) 뒤 표시 해제");

            yield return StartRun(RunMode.Standard, Cocktail);
            Assert.AreEqual(DrinkKind.Cocktail, game.Player.DrunkDrink);
            Assert.IsFalse(Tile, "기본 모드는 표시 안 함(새 런·모드 전환)");

            yield return StartRun(RunMode.Endless, Coffee);
            Assert.IsFalse(Tile, "커피는 표시 안 함");

            yield return StartRun(RunMode.Endless, null);
            Assert.IsNull(game.Player.DrunkDrink);
            Assert.IsFalse(Tile, "마신 음료 없음 = 표시 없음");
        }

        [UnityTest]
        public IEnumerator Tipsy_StripHiddenWhilePaused()
        {
            SettingsService.SetTipsyBadge(true);
            yield return StartRun(RunMode.Endless, Cocktail);
            game.Master?.StartBattle(); // 준비 단계 → 전투(마스터 도전 실제 버튼과 같은 함수)
            yield return new WaitForSecondsRealtime(0.3f);
            var strip = game.Hud.EffectsStrip;
            Assert.IsTrue(strip.Current.Any(e => e.id == "tipsy"), "전투 중 칸 목록에 있음");
            Assert.IsTrue(strip.Visible);
            game.OpenPause(); yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(strip.Visible, "일시정지 중 칸 숨김(기존 규칙)");
            game.ClosePause(); yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(strip.Visible, "재개 후 다시 보임");
        }
    }
}
