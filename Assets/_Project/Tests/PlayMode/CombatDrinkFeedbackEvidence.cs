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
    /// 2026-11-09 QA 증거 프레임(수동 지정 실행 전용 — [Explicit]). 창·포커스 없이(배치 모드, 그래픽 장치만) 메인 카메라와
    /// HUD 캔버스를 오프스크린 렌더 텍스처로 그려 PNG로 남긴다. 저장은 임시 폴더로 격리. 출력: 환경 변수 CCT_EVIDENCE_DIR
    /// (없으면 Delivery/Verification/2026-11-09/combat-drink-feedback/frames).
    /// </summary>
    public class CombatDrinkFeedbackEvidence
    {
        const int W = 1280, H = 720;
        GameManager game;
        string outDir;
        readonly List<string> folders = new List<string>();
        readonly List<string> notes = new List<string>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            outDir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Delivery/Verification/2026-11-09/combat-drink-feedback/frames");
            Directory.CreateDirectory(outDir);
            NewSave();
            SettingsService.SetForTests(Path.Combine(folders[0], "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            File.WriteAllLines(Path.Combine(outDir, "frames-notes.txt"), notes);
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
            var f = Path.Combine(Application.temporaryCachePath, "combat-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(f);
            folders.Add(f);
            PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
        }

        IEnumerator StartRun(DrinkRecipe recipe)
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
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            foreach (var w in game.Player.GetComponentsInChildren<RapierWeapon>()) w.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            game.Player.Coffee?.ResetStateForTests();
            HitFeedback.ResetForTests();
            DrinkUseFx.ResetForTests();
        }

        static DrinkRecipe Coffee(CoffeeBrewMethod m)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            r.name = "시험 커피";
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, m == CoffeeBrewMethod.EspressoMachine ? 180 : 150));
            return r;
        }

        Enemy Spawn(Vector2 offset)
        {
            var def = new EnemyDef { name = "시험 몹", dropCount = 0, hp = 5000f, speed = 0f, radius = 0.45f, color = new Color(0.55f, 0.75f, 0.95f) };
            return Enemy.SpawnAt(game.Player.Position + offset, def, 1f, game.Player, game.transform);
        }

        /// <summary>오버레이 캔버스를 잠시 카메라 캔버스로 바꿔 카메라와 함께 오프스크린으로 그린다.</summary>
        IEnumerator Shot(string name)
        {
            var cam = Camera.main;
            Assert.IsNotNull(cam, "메인 카메라");
            var rt = new RenderTexture(W, H, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var oldOrders = canvases.Select(c => c.sortingOrder).ToList();
            // 실제 게임의 오버레이처럼 HUD가 월드 스프라이트 위에 그려지게 정렬 순서를 잠시 올린다(캡처 전용).
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 1000; }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            for (int ci = 0; ci < canvases.Count; ci++) { canvases[ci].renderMode = RenderMode.ScreenSpaceOverlay; canvases[ci].worldCamera = null; canvases[ci].sortingOrder = oldOrders[ci]; }
            string path = Path.Combine(outDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            notes.Add($"{name}.png | proc='{game?.DrinkHud?.ProcText}' kind={game?.DrinkHud?.ProcKind} | hidden='{game?.Player?.HiddenRuntime?.LastActivation?.Replace("\n", " / ")}' | timeScale={Time.timeScale} shake={CameraRig.CurrentShakeAmplitude:0.000}");
        }

        [UnityTest, Explicit("QA 증거 프레임 — 지정 실행 전용")]
        public IEnumerator CaptureEvidenceFrames()
        {
            // 01 음료 없음(전후 비교 기준)
            yield return StartRun(null);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("01_no_drink_combat");

            // 02 준비 음료 적용: [상시] 알림 + 머리 위 잔 + 발밑 고리(첫 전투 프레임)
            yield return StartRun(Coffee(CoffeeBrewMethod.EspressoMachine));
            yield return Shot("02_drink_applied_notice_cup");
            yield return new WaitForSeconds(0.2f);
            yield return Shot("02b_drink_applied_cup_rising");

            // 03 에스프레소 샷 지속 상태(남은 초)
            yield return new WaitForSeconds(2.5f);
            game.Player.Coffee.Step(1.01f, false);
            yield return Shot("03_espresso_shot_cup_pop");
            float t0 = Time.realtimeSinceStartup;
            while (game.DrinkHud.ProcKind != DrinkNoticeKind.Timed && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return Shot("03b_espresso_shot_timed_notice");

            // 04 일반/치명/강타 비교(같은 몹 3마리) + 치명 연속 프레임
            var a = Spawn(new Vector2(2.6f, 1.6f));
            var b = Spawn(new Vector2(2.6f, 0f));
            var c = Spawn(new Vector2(2.6f, -1.6f));
            yield return new WaitForSeconds(0.2f);
            yield return Shot("04a_three_enemies_before_hit");
            a.TakeDamage(12f, HitKind.Normal, (Vector2)a.transform.position + Vector2.left * 0.3f);
            b.TakeDamage(24f, HitKind.Critical, (Vector2)b.transform.position + Vector2.left * 0.3f);
            c.TakeDamage(18f, HitKind.Heavy, (Vector2)c.transform.position + Vector2.left * 0.3f);
            for (int i = 0; i < 6; i++) yield return Shot($"04b_normal_crit_heavy_f{i}");

            // 05 치명 알림 합산(×N)
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < 4; i++)
                game.DrinkHud.ShowDrinkNotice("drink_crit", game.DrinkHud.PreparedDrinkName, "치명타 · 피해 ×2", DrinkNoticeKind.Instant, 0f, new Color(1f, 0.82f, 0.32f));
            yield return Shot("05_crit_notice_merged");

            // 06 무적·0 피해: 표시 없음
            yield return new WaitForSeconds(0.8f);
            b.Invulnerable = true;
            b.TakeDamage(50f, HitKind.Critical);
            b.Invulnerable = false;
            a.TakeDamage(0f, HitKind.Normal);
            yield return Shot("06_invulnerable_and_zero_no_feedback");

            // 07 지속 피해 틱(소리·불꽃 없음, 작은 숫자)
            c.TakeDamage(3f, HitKind.Dot);
            yield return Shot("07_dot_tick_quiet");

            // 08 다수 적 동시 피격(불꽃 예산)
            var crowd = new List<Enemy>();
            for (int i = 0; i < 18; i++) crowd.Add(Spawn(new Vector2(-1.5f - (i % 6) * 0.8f, -1.8f + (i / 6) * 1.6f)));
            yield return new WaitForSeconds(0.4f);
            HitFeedback.ResetForTests();
            foreach (var e in crowd) e.TakeDamage(6f, HitKind.Normal);
            yield return Shot("08_many_enemies_spark_budget");
            notes.Add($"crowd hit: sparks={UnityEngine.Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None).Count(v => v.Renderer != null && v.Renderer.sprite != null && v.Renderer.sprite.name.StartsWith("hit_spark"))} cap={HitFeedback.MaxSparksPerFrame}");

            // 09 일시정지: 알림·전투 칩 숨김, 연출 정지
            b.TakeDamage(10f, HitKind.Critical);
            game.OpenPause();
            yield return Shot("09_paused");
            game.ClosePause();

            // 10 설정 off: 숫자 끄기·흔들림 0
            SettingsService.SetDamageNumbers(false);
            SettingsService.SetShakePercent(0);
            yield return new WaitForSeconds(0.7f);
            a.TakeDamage(12f, HitKind.Normal);
            b.TakeDamage(24f, HitKind.Critical);
            yield return Shot("10_damage_numbers_off");
            SettingsService.SetDamageNumbers(true);
            SettingsService.SetShakePercent(50);

            // 11 모카포트 증기 폭발(강타 다수 + 발동 알림)
            yield return StartRun(Coffee(CoffeeBrewMethod.MokaPot));
            yield return new WaitForSeconds(2.6f); // 적용 알림 지나감
            var m1 = Spawn(new Vector2(1.4f, 0.6f));
            var m2 = Spawn(new Vector2(-1.4f, -0.6f));
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < CoffeeBrewRuntime.MokaHeatMax; i++) game.Player.Coffee.OnDirectHit(m1);
            yield return Shot("11_moka_burst_heavy");
            t0 = Time.realtimeSinceStartup;
            while (game.DrinkHud.ProcKind != DrinkNoticeKind.Instant && Time.realtimeSinceStartup - t0 < 4f) yield return null;
            yield return Shot("11b_moka_burst_notice");

            // 12 지속 알림 배치(1280×720 렌더) 잘림 확인
            game.DrinkHud.ShowDrinkNotice("narrow", "에스프레소", "공격 속도 +30%", DrinkNoticeKind.Timed, 3f, new Color(1f, 0.82f, 0.48f));
            yield return Shot("12_notice_layout_1280x720");
            Assert.Pass("frames written to " + outDir);
        }
    }
}
