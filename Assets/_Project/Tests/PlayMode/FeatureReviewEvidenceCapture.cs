using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-26 통합 대표 화면 검수(#18 발사 묶음 폭발 총알 0/1/2/폭발, #19 카드 다음 단계·도감, #21 속성 화살 순서·머리 위 아이콘·저이펙트,
    /// #22 물기/얼룩 + 차 음료 오라 공존). 창·입력·세이브 없음, CCT_EVIDENCE_DIR가 있을 때만 실행.</summary>
    public class FeatureReviewEvidenceCapture
    {
        string dir, folder; readonly StringBuilder log = new StringBuilder();
        GameManager game; PlayerController player; bool setUp;
        static readonly (int w, int h)[] Sizes = { (1280, 720), (1920, 1080), (1024, 768) };
        class Stationary : IEnemyDriver { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음");
            dir = Path.Combine(dir, "features"); Directory.CreateDirectory(dir);
            folder = Path.Combine(Application.temporaryCachePath, "feat-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            CardEducationGuide.SuppressAutomaticForTests = true;
            InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            setUp = true; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!setUp) yield break;
            File.AppendAllText(Path.Combine(dir, "features-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null; Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            yield return null;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[FeatureReview] " + s); }

        IEnumerator Build(Job job, WeaponKind weapon, bool quiet = true)
        {
            RunModeSelection.Current = RunMode.Standard;
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            if (quiet)
            {
                game.Spawner.enabled = false;
                if (game.Director != null) game.Director.enabled = false;
                foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.Destroy(e.gameObject);
            }
            player = game.Player;
            player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            player.Heal(100000f);
            yield return null;
        }

        Canvas HudCanvas => game.Hud.GetComponentInParent<Canvas>();

        IEnumerator Shot(string tag, params (int w, int h)[] sizes)
        {
            foreach (var (w, h) in (sizes.Length > 0 ? sizes : Sizes))
            {
                yield return null;
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { HudCanvas }, w, h, $"{tag}_{w}x{h}");
            }
        }

        Enemy Target(Vector2 offset, float hp = 100f)
        {
            var e = Enemy.SpawnAt(player.Position + offset, EnemyDef.Brute, hp, player, game.transform);
            e.SetDriver(new Stationary());
            e.GetComponent<Rigidbody2D>().simulated = false;
            return e;
        }

        // ── #18 발사 묶음 폭발 총알 0/1/2/폭발 ──
        [UnityTest]
        public IEnumerator Hidden18_GunVolleyPips_0_1_2_Burst()
        {
            yield return Build(Job.Bartender, WeaponKind.Gun);
            player.Hidden.ForceForTests(HiddenSkill.PointBlank);
            var rt = player.HiddenRuntime; var pips = player.GetComponent<GunVolleyPips>();
            var target = Target(new Vector2(3.5f, 0f)); Target(new Vector2(4.3f, 0.4f));
            foreach (var sc in game.GetComponentsInChildren<PlayerProjectileWeapon>()) sc.enabled = false;
            pips.Refresh(); yield return Shot("h18_pips0");
            int v1 = rt.NextGunVolley(), v2 = rt.NextGunVolley(), v3 = rt.NextGunVolley(); Vector2 at = target.transform.position;
            rt.OnGunVolleyHit(v1, at); pips.Refresh(); yield return Shot("h18_pips1", Sizes[0]);
            rt.OnGunVolleyHit(v2, at); pips.Refresh(); yield return Shot("h18_pips2", Sizes[0]);
            rt.OnGunVolleyHit(v3, at); pips.Refresh();
            Line($"h18 burst total={rt.VolleyBurstTotal} filledShown={pips.FilledShown} flashing={rt.VolleyFlashing}");
            yield return Shot("h18_burst_flash");
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            int w1 = rt.NextGunVolley(), w2 = rt.NextGunVolley(), w3 = rt.NextGunVolley();
            rt.OnGunVolleyHit(w1, at); rt.OnGunVolleyHit(w2, at); pips.Refresh(); yield return Shot("h18_pips2_low", Sizes[0]);
            rt.OnGunVolleyHit(w3, at); pips.Refresh(); yield return Shot("h18_burst_low", Sizes[0]);
        }

        // ── #19 카드 다음 단계 + 도감 ──
        [UnityTest]
        public IEnumerator Tea19_CardNextStage_And_CodexDetail()
        {
            yield return Build(Job.TeaSommelier, WeaponKind.Sword, false);
            CardData C(string id) => CardDatabase.LoadAll().First(c => c.id == id);
            var offers = new List<CardOffer> { new CardOffer { card = C("green_tea"), currentStacks = 0 }, new CardOffer { card = C("oolong"), currentStacks = 1 }, new CardOffer { card = C("aged_puerh"), currentStacks = 2 } };
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow); // 등장 연출 없이 바로 정착한 모습(일반 효과의 정착 화면은 기존 standard_card_open_settled)
            game.CardView.Show(offers, o => { });
            for (int fr = 0; fr < 6000 && !(game.CardView.EntranceDone(0) && game.CardView.EntranceDone(1) && game.CardView.EntranceDone(2)); fr++) yield return null;
            for (int fr = 0; fr < 30; fr++) yield return null;
            Line($"tea19 stage lines: [{game.CardView.StageLine(0)}] [{game.CardView.StageLine(1)}] [{game.CardView.StageLine(2)}]");
            var cardCanvas = game.CardView.GetComponentInParent<Canvas>().rootCanvas;
            foreach (var (w, h) in Sizes) yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { cardCanvas }, w, h, $"t19_card_stage_{w}x{h}");
            PermanentProgress.DiscoverCard("oolong"); PermanentProgress.DiscoverCard("green_tea");
            var canvas = UiFactory.CreateCanvas(null, "T19Codex", 200);
            var codex = CardCodexView.Create(canvas.transform, () => { }); codex.Show();
            Assert.IsTrue(codex.OpenDetailFor("oolong"));
            foreach (var (w, h) in Sizes) yield return SkinEvidenceCapture.CaptureTo(dir, Line, null, new[] { canvas }, w, h, $"t19_codex_detail_oolong_{w}x{h}");
            UnityEngine.Object.Destroy(canvas.gameObject);
        }

        // ── #21 속성 화살: 같은 발사 순서 + 머리 위 아이콘 + 저이펙트 ──
        [UnityTest]
        public IEnumerator Arrow21_OrderHeadIcons_FlightFrames_Low()
        {
            yield return Build(Job.TeaSommelier, WeaponKind.Bow);
            var s = player.Stats; s.arrowBurnLevel = 1; s.arrowFreezeLevel = 1; s.arrowShockLevel = 1; s.arrowPoisonLevel = 1; s.extraThrusts = 3;
            var weapon = player.GetComponents<PlayerProjectileWeapon>().FirstOrDefault();
            var preview = player.GetComponent<ArrowQueuePreview>();
            foreach (var wpn in player.GetComponents<PlayerProjectileWeapon>()) wpn.enabled = false;
            yield return null; yield return null;
            Line("arrow21 head preview next4=" + string.Join(",", preview.Shown.Take(4)));
            yield return Shot("a21_head_icons");
            foreach (var wpn in player.GetComponents<PlayerProjectileWeapon>()) wpn.enabled = true;
            var t = Target(new Vector2(4.5f, 0f), 1000f);
            var spawned = new List<string>();
            for (int f = 0; f < 4; f++)
            {
                yield return new WaitForSeconds(f == 0 ? 0.05f : 0.08f);
                var arrows = game.GetComponentsInChildren<PlayerProjectile>().Where(p => p.Element != ArrowElementKind.None).Select(p => p.Element).ToList();
                Line($"arrow21 flight f{f} in-air={string.Join(",", arrows)}");
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { HudCanvas }, 1280, 720, $"a21_flight_f{f}_1280x720");
            }
            foreach (var wpn in player.GetComponents<PlayerProjectileWeapon>()) wpn.enabled = false;
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            yield return null; yield return null;
            yield return Shot("a21_head_icons_low", Sizes[0]);
            foreach (var wpn in player.GetComponents<PlayerProjectileWeapon>()) wpn.enabled = true;
            yield return new WaitForSeconds(0.2f);
            yield return Shot("a21_flight_low", Sizes[0]);
        }

        // ── #22 물기/얼룩 + 차 음료 오라 공존 ──
        [UnityTest]
        public IEnumerator Floor22_WaterStain_Aura_CoexistWithTeaDrinkAura()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail); r.technique = (int)DrinkTechnique.Shake; r.name = "그레나딘 시럽 셰이킹";
            foreach (var (id, ml) in new[] { (DrinkIngredients.Gin, 40), (DrinkIngredients.Grenadine, 30), (DrinkIngredients.Rum, 20) }) r.steps.Add(new DrinkStep(id, ml));
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var err), err);
            yield return Build(Job.TeaSommelier, WeaponKind.Bow);
            var hz = game.Spawner.Hazards;
            var w = hz.SpawnAt(FloorHazardKind.Water, player.Position + new Vector2(-4.5f, 0.6f));
            var st = hz.SpawnAt(FloorHazardKind.Stain, player.Position + new Vector2(4.5f, -0.6f));
            yield return Shot("f22_warning", Sizes[0]);
            yield return new WaitForSeconds(FloorHazardRules.WarnSeconds + 0.3f);
            Line($"floor22 tea aura showing={game.TeaAura.Showing} fade={game.TeaAura.Fade} hazardAura={hz.Aura != null && hz.Aura.Visible} slip={player.Stats.FloorSlip} stress={player.Stats.FloorStress} status='{game.Hud.StatusLineText}'");
            yield return Shot("f22_active_both_tea_aura");
            // 청소 진행
            var rb = player.GetComponent<Rigidbody2D>(); Vector2 p = w.Center; player.transform.position = p; if (rb != null) rb.position = p;
            yield return new WaitForSeconds(1.2f);
            yield return Shot("f22_cleaning_progress", Sizes[0]);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("f22_low_effects", Sizes[0]);
            var bartender = game; // 바텐더/레이피어: 얼룩만
        }

        // ── #24 최종 보스: 저격 추적/고정/발사, 폐점 러시 통로, 저이펙트, 근접·원거리, 바닥 위험 + 속성 화살 동시 ──
        IEnumerator FinalBoss(Job job, WeaponKind weapon, string tag, bool low, bool withHazardsAndArrows)
        {
            yield return Build(job, weapon);
            SettingsService.SetEffectsLevel(low ? SettingsData.EffectsLow : SettingsData.EffectsNormal);
            if (!withHazardsAndArrows) foreach (var mb in player.GetComponents<MonoBehaviour>()) if (mb is PlayerProjectileWeapon || mb is RapierWeapon) mb.enabled = false;
            player.transform.position = new Vector3(0f, Balance.LaneCenterY, 0f);
            var def = job == Job.TeaSommelier ? EnemyDef.TeaCustomer : EnemyDef.DifficultCustomer;
            var boss = Enemy.SpawnAt(player.Position + new Vector2(7.5f, 0f), def, 1f, player, game.transform);
            var driver = boss.gameObject.AddComponent<CustomerBossDriver>(); driver.Init(boss, game); driver.enabled = false; game.ActiveBoss = boss;
            boss.TakeDamage(boss.Hp - boss.MaxHp * 0.29f);
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);
            System.Action<string, bool> start = (m, arg) =>
            {
                var mi = typeof(CustomerBossDriver).GetMethod(m, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                driver.StartCoroutine((IEnumerator)(mi.GetParameters().Length == 0 ? mi.Invoke(driver, null) : mi.Invoke(driver, new object[] { arg })));
            };
            if (withHazardsAndArrows)
            {
                var s = player.Stats; s.arrowBurnLevel = 1; s.arrowShockLevel = 1; s.extraThrusts = 2;
                var hz = game.Spawner.Hazards;
                hz.SpawnAt(FloorHazardKind.Water, player.Position + new Vector2(-4f, 1.2f), 0f);
                hz.SpawnAt(FloorHazardKind.Stain, player.Position + new Vector2(3f, -1.4f), 0f);
            }
            start("Snipe", false);
            yield return new WaitForSeconds(0.6f);
            Line($"{tag} snipe tracking, volleys={driver.SnipeVolleys} dir={driver.LastSnipeDirection}");
            yield return Shot(tag + "_snipe_tracking", low ? new[] { Sizes[0] } : Sizes);
            yield return new WaitForSeconds(CustomerBossDriver.SnipeFirstTrackSeconds - 0.6f + 0.2f);
            Line($"{tag} snipe locked, dir={driver.LastSnipeDirection}");
            yield return Shot(tag + "_snipe_locked", Sizes[0]);
            yield return new WaitForSeconds(CustomerBossDriver.SnipeLockSeconds + 0.35f);
            yield return Shot(tag + "_snipe_fired", Sizes[0]);
            yield return new WaitForSeconds(2.2f);
            start("ClosingRush", false);
            yield return null;
            Line($"{tag} rush gapCenterY={driver.LastGapCenterY} player.y={player.Position.y}");
            yield return Shot(tag + "_rush_telegraph", low ? new[] { Sizes[0] } : Sizes);
            yield return new WaitForSeconds(CustomerBossDriver.WallTelegraphSeconds + 0.3f);
            yield return Shot(tag + "_rush_walls", Sizes[0]);
            var dodge = HudCanvas.GetComponentsInChildren<UnityEngine.UI.Text>(false).Where(t => t.text.Contains("회피")).Select(t => t.text).ToList();
            Line($"{tag} texts containing 회피 (should be none): [{string.Join("|", dodge)}]");
            Assert.AreEqual(0, dodge.Count);
        }

        [UnityTest] public IEnumerator Boss24_Bartender_Rapier_Melee() { yield return FinalBoss(Job.Bartender, WeaponKind.Rapier, "b24_bar_rapier", false, false); }
        [UnityTest] public IEnumerator Boss24_Tea_Bow_Ranged_Low() { yield return FinalBoss(Job.TeaSommelier, WeaponKind.Bow, "b24_tea_bow_low", true, false); }
        [UnityTest] public IEnumerator Boss24_Tea_Bow_HazardsAndArrows() { yield return FinalBoss(Job.TeaSommelier, WeaponKind.Bow, "b24_tea_bow_hazard_arrows", false, true); }
    }
}
