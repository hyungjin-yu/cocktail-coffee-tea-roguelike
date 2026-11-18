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
    /// 2026-11-09 추가 친구 피드백("라운드 안에서 내가 뭘 먹었고 무슨 버프인지 모른다"):
    /// 지속 효과 칸(실제 상태만·종류 모양 구분·정수·상한/묶음·상세), 캐릭터 지속 표식(실제 효과만·예산·효과 낮음),
    /// 런 구간 배지와 배경 강조(실제 BossDirector 진행 기준). 저장은 임시 폴더로 격리.
    /// </summary>
    public class PersistentBuffRoundFeedbackPlayTests
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
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var f in folders) try { Directory.Delete(f, true); } catch { }
            folders.Clear();
        }

        void NewSave()
        {
            var f = Path.Combine(Application.temporaryCachePath, "buff-round-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(f);
            folders.Add(f);
            PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
        }

        static DrinkRecipe Coffee(CoffeeBrewMethod m)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            r.name = "시험 커피";
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, m == CoffeeBrewMethod.EspressoMachine ? 180 : 150));
            return r;
        }

        IEnumerator StartRun(DrinkRecipe recipe, bool keepDirector = false)
        {
            if (game != null) { UnityEngine.Object.Destroy(game.gameObject); game = null; yield return null; }
            NewSave();
            if (recipe != null)
            {
                var craft = DrinkLab.Craft(recipe);
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null && !keepDirector) game.Director.enabled = false;
            foreach (var w in game.Player.GetComponentsInChildren<RapierWeapon>()) w.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            yield return null;
            game.Player.Coffee?.ResetStateForTests();
        }

        ActiveEffect Find(string id) => ActiveEffects.Collect(game).FirstOrDefault(e => e.id == id);

        // ── 범위 1: 지속 효과 칸 ─────────────────────

        [UnityTest]
        public IEnumerator Strip_ShowsActualDrink_AndTimedShotWithRemaining_IntegerOnly()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.EspressoMachine));
            var strip = game.Hud.EffectsStrip;
            Assert.IsNotNull(strip);
            strip.RefreshForTests();
            var drink = Find("drink");
            Assert.IsNotNull(drink, "준비 음료 칸");
            Assert.AreEqual(ActiveEffectKind.Run, drink.kind);
            Assert.AreEqual("음료", drink.label);
            StringAssert.IsMatch(@"^(사거리|공격|공속|치명|체력|쿨감|이속)\+\d+%", drink.value, "짧은 정수 요약");
            StringAssert.Contains("시험 커피", drink.detail, "상세: 음료 이름");
            Assert.IsNotNull(drink.icon, "기존 잔 아트 재사용");
            Assert.AreEqual(ActiveEffectKind.Stack, Find("coffee").kind, "샷 전에는 압력(누적)");

            game.Player.Coffee.Step(1.01f, false);
            var shot = Find("coffee");
            Assert.AreEqual("샷", shot.label);
            Assert.AreEqual(ActiveEffectKind.Timed, shot.kind);
            Assert.AreEqual("공속+30%", shot.value);
            Assert.Greater(shot.remaining, 0f);
            StringAssert.Contains("3초 남음", shot.detail);
            var order = ActiveEffects.Collect(game).Select(e => e.id).ToList();
            Assert.Less(order.IndexOf("coffee"), order.IndexOf("drink"), "시간 제한 효과가 앞");
            strip.RefreshForTests();
            Assert.AreEqual("샷|공속+30%", strip.TileText(0));
            Assert.IsTrue(ActiveEffects.Collect(game).All(e => !e.value.Contains(".")), "모든 칸 값은 정수");

            game.Player.Coffee.Step(CoffeeBrewRuntime.EspressoShotSeconds + 0.01f, true);
            Assert.AreEqual("샷 대기", Find("coffee").label, "샷이 끝나면 실제 상태대로 대기로 바뀐다");
        }

        [UnityTest]
        public IEnumerator Strip_SuppressedDrinkShowsInvalid_NoDrinkRunHasNoDrinkTile()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.PourOver));
            game.Player.Coffee.Step(CoffeeBrewRuntime.PourBloomSeconds + 0.01f, true); // 붓기(사거리 +%)
            game.Markers.Tick(0f);
            Assert.IsTrue(game.Markers.RangeRingVisible, "평소 붓기에는 사거리 고리");
            game.Stats.SuppressBonuses(5f);
            game.Markers.Tick(0f);
            var d = Find("drink");
            Assert.AreEqual(ActiveEffectKind.Suppressed, d.kind);
            StringAssert.StartsWith("무효 ", d.value);
            Assert.AreEqual("무효", Find("coffee").value, "커피 % 보정도 실제로 무효");
            Assert.IsFalse(game.Markers.RangeRingVisible, "무효 중에는 사거리 고리도 없음");

            yield return StartRun(null);
            Assert.IsNull(Find("drink"), "음료 없이 출발하면 음료 칸 없음");
            Assert.IsNull(Find("coffee"));
            game.Hud.EffectsStrip.RefreshForTests();
            Assert.AreEqual(0, game.Hud.EffectsStrip.VisibleTiles);
            Assert.IsFalse(game.Markers.AuraVisible, "음료 없으면 발밑 원 없음");
        }

        [UnityTest]
        public IEnumerator Strip_ManyEffects_CapsAndBundles_DetailsListAll()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.MokaPot));
            var p = game.Player;
            foreach (var s in new[] { HiddenSkill.IcedCup, HiddenSkill.ShakeRhythm, HiddenSkill.HerbGarden, HiddenSkill.BarTempo, HiddenSkill.WeekendRush })
                p.Hidden.ForceForTests(s);
            p.HiddenRuntime.SetBarrierReadyForTests();
            p.HiddenRuntime.AfterDirectHit(null); // 콤보 1 + 증기압 1 (실제 직접 타격 훅)
            p.HiddenRuntime.OnEnemyKilled(EnemyDef.Imp); // 주말 러시 발동
            yield return new WaitForSeconds(0.6f); // 멈춰 서 있기(허브 가든·바 템포 정지)
            var all = ActiveEffects.Collect(game);
            Assert.GreaterOrEqual(all.Count, 7, string.Join(",", all.Select(e => e.id)));
            var strip = game.Hud.EffectsStrip;
            strip.RefreshForTests();
            int max = strip.Rect.localScale.x > 1.2f ? ActiveEffectsStrip.MaxTilesNarrow : ActiveEffectsStrip.MaxTiles;
            Assert.AreEqual(max, strip.VisibleTiles, "상한");
            Assert.AreEqual(all.Count - (max - 1), strip.Overflow);
            Assert.AreEqual($"더보기|+{strip.Overflow}", strip.TileText(max - 1), "묶음 칸");
            Assert.AreEqual("1/10", Find("coffee").value, "증기압 누적 정수");

            game.OpenHiddenDetails();
            string text = string.Join("\n", game.Hud.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text));
            StringAssert.Contains("지금 적용 중", text);
            foreach (var e in all) StringAssert.Contains(e.detail, text, "묶인 효과도 상세에서 모두 보인다: " + e.id);
            StringAssert.Contains("[준비]", text);
            StringAssert.Contains("[누적]", text);
            game.CloseHiddenDetails();
        }

        [UnityTest]
        public IEnumerator Strip_AndBadge_HiddenWhilePaused()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.EspressoMachine));
            yield return null;
            Assert.IsTrue(game.Hud.EffectsStrip.Visible);
            Assert.IsTrue(game.Hud.PhaseBadge.Visible);
            game.OpenPause();
            yield return null;
            Assert.IsFalse(game.Hud.EffectsStrip.Visible, "일시정지 위에 올라오지 않음");
            Assert.IsFalse(game.Hud.PhaseBadge.Visible);
            game.ClosePause();
            yield return null;
            Assert.IsTrue(game.Hud.EffectsStrip.Visible);
        }

        // ── 범위 2: 캐릭터 표식 ─────────────────────

        [UnityTest]
        public IEnumerator Markers_ReflectActualState_RangeRingMatchesWeaponReach_PuffBudget_LowEffects()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.PourOver));
            var m = game.Markers;
            var c = game.Player.Coffee;
            m.Tick(0f);
            Assert.IsTrue(m.AuraVisible, "준비 음료 발밑 원");
            Assert.IsTrue(c.Blooming);
            Assert.IsFalse(m.RangeRingVisible, "뜸 동안엔 사거리 고리 없음");
            c.Step(CoffeeBrewRuntime.PourBloomSeconds + 0.01f, true);
            m.Tick(0f);
            Assert.IsTrue(m.RangeRingVisible, "붓기(사거리 +%) 동안 고리");
            Assert.AreEqual(PlayerEffectMarkers.WeaponReach(game.Player), m.RangeRingRadius, 1e-4f, "주 무기 실제 사거리");
            Assert.Greater(m.RangeRingRadius, 0f);

            yield return StartRun(Coffee(CoffeeBrewMethod.EspressoMachine));
            m = game.Markers;
            game.Player.Coffee.Step(1.01f, false);
            Assert.IsTrue(game.Player.Coffee.ShotActive);
            for (int i = 0; i < 40; i++) m.Tick(0.4f);
            Assert.LessOrEqual(m.LivePuffs, PlayerEffectMarkers.MaxPuffs, "동시 퍼프 상한");
            Assert.Greater(m.PuffsSpawned, 0, "샷 중 김 퍼프");
            int before = m.PuffsSpawned;
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            foreach (var p in UnityEngine.Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(p.gameObject);
            for (int i = 0; i < 10; i++) m.Tick(0.4f);
            Assert.AreEqual(before, m.PuffsSpawned, "효과 낮음: 퍼프 없음");
            Assert.IsTrue(m.AuraVisible, "정보 표식(발밑 원)은 유지");
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);

            // 몸체·피격 색을 건드리지 않는다: 표식은 별도 자식 렌더러.
            var body = game.Player.GetComponentsInChildren<SpriteRenderer>().Where(r => r.gameObject.name != "DrinkAura" && !r.gameObject.name.StartsWith("Dot")).ToList();
            Assert.IsTrue(body.All(r => r.sharedMaterial == null || !r.sharedMaterial.name.Contains("(Instance)")), "공유 머티리얼 복제/변경 없음");
        }

        [UnityTest]
        public IEnumerator Markers_BarrierRingOnlyWhenReady()
        {
            yield return StartRun(null);
            var m = game.Markers;
            m.Tick(0f);
            Assert.IsFalse(m.BarrierRingVisible);
            game.Player.Hidden.ForceForTests(HiddenSkill.IcedCup);
            game.Player.HiddenRuntime.SetBarrierReadyForTests();
            m.Tick(0f);
            Assert.IsTrue(m.BarrierRingVisible, "보호막 준비 = 고리");
            Assert.IsTrue(game.Player.HiddenRuntime.TryBlockHit());
            m.Tick(0f);
            Assert.IsFalse(m.BarrierRingVisible, "소비되면 즉시 사라짐");
        }

        // ── 범위 3: 런 구간 배지·배경 강조 ─────────────────────

        [UnityTest]
        public IEnumerator PhaseBadge_FollowsRealSchedule_IncomingThenBossFight_AmbienceRestores()
        {
            yield return StartRun(null, keepDirector: true);
            var d = game.Director;
            var first = d.NextSpec;
            Assert.IsNotNull(first);
            game.SetRunTimeForTests(10f);
            var info = RunPhaseBadge.Compute(game);
            Assert.AreEqual(RunPhaseState.Calm, info.state);
            Assert.AreEqual(1, info.segment);
            Assert.AreEqual(d.ScheduleCount, info.segments);
            int secs = IntNum.RemainingSeconds(first.time - 10f);
            StringAssert.Contains($"다음 보스 {secs / 60}:{secs % 60:00}", info.text);
            Assert.AreEqual(RunPhaseBadge.DangerTier(10f), info.danger);

            d.enabled = false; // 시험 중 실제 등장 방지 — 꺼진 감독은 예고하지 않는다(가짜 예고 없음)
            game.SetRunTimeForTests(first.time - 5f);
            Assert.AreNotEqual(RunPhaseState.BossIncoming, RunPhaseBadge.Compute(game).state, "감독이 꺼져 있으면 예고 없음");
            d.enabled = true;
            info = RunPhaseBadge.Compute(game);
            Assert.AreEqual(RunPhaseState.BossIncoming, info.state);
            Assert.AreEqual($"보스 접근 5초 · {first.def.name}", info.text);

            var amb = game.Ambience;
            Assert.IsNotNull(amb);
            if (amb.LampCount > 0)
            {
                amb.Apply(RunPhaseState.BossIncoming, 0.3f);
                Assert.AreNotEqual(amb.LampBaseColor(0), amb.LampColor(0), "보스 접근: 램프 강조");
            }

            var boss = d.SpawnNow(d.NextSpec);
            Assert.AreSame(boss, game.ActiveBoss);
            info = RunPhaseBadge.Compute(game);
            Assert.AreEqual(RunPhaseState.BossFight, info.state);
            StringAssert.StartsWith($"보스전 · {first.def.name}", info.text);
            if (amb.HasVeil)
            {
                amb.Apply(RunPhaseState.BossFight, 0f);
                Assert.LessOrEqual(amb.VeilColor.a - amb.VeilBaseColor.a, RunAmbience.VeilAlphaBoost + 1e-4f, "바닥 막은 아주 조금만 진해짐(장판 가독성)");
            }
            amb.Apply(RunPhaseState.Calm, 0f);
            if (amb.LampCount > 0) Assert.AreEqual(amb.LampBaseColor(0), amb.LampColor(0), "평상시 원래 색 복귀");
            if (amb.HasVeil) Assert.AreEqual(amb.VeilBaseColor, amb.VeilColor);
        }

        [Test]
        public void DangerTier_IsIntegerOneToFive_NonDecreasing()
        {
            int last = 0;
            for (float t = 0f; t <= Balance.RunTimeLimitSeconds; t += 30f)
            {
                int d = RunPhaseBadge.DangerTier(t);
                Assert.That(d, Is.InRange(1, 5));
                Assert.GreaterOrEqual(d, last, $"t={t}");
                last = d;
            }
            Assert.AreEqual(1, RunPhaseBadge.DangerTier(0f));
            Assert.AreEqual(5, RunPhaseBadge.DangerTier(Balance.RunTimeLimitSeconds));
        }
    }
}
