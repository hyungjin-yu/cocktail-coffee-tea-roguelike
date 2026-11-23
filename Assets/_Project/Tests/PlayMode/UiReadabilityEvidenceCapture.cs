using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-23 #14 UI 가독성 공동 검수 증거(창·입력 없음, 가상 렌더). CCT_EVIDENCE_DIR가 있을 때만 실행.
    /// 직업 2 × 모드 2 × 1280x720 / 1920x1080 / 1024x768(4:3). 저장·설정은 임시 경로로 분리. 게임 규칙 불변(상태는 공개 API·시험 훅으로만 만든다).
    /// 각 장면은 화면 안 글자(Text) 중 칸을 넘치거나 화면을 벗어난 것을 로그에 남긴다.
    /// </summary>
    public class UiReadabilityEvidenceCapture
    {
        string dir, folder; readonly StringBuilder log = new StringBuilder();
        GameManager game; Canvas canvas; bool setUp;
        static readonly (int w, int h)[] Sizes = { (1280, 720), (1920, 1080), (1024, 768) };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            folder = Path.Combine(Application.temporaryCachePath, "ui14-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            CardEducationGuide.SuppressAutomaticForTests = true;
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            setUp = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!setUp) yield break;
            File.AppendAllText(Path.Combine(dir, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            RunModeSelection.Current = RunMode.Standard;
            Time.timeScale = 1f;
            yield return null;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[Ui14Evidence] " + s); }

        static DrinkRecipe Recipe(string name, params (string id, int ml)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Stir;
            r.name = name;
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.ml));
            return r;
        }

        void Reserve(DrinkRecipe r)
        {
            var craft = DrinkLab.Craft(r);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
        }

        IEnumerator Start(Job job, RunMode mode, WeaponKind weapon = WeaponKind.Rapier, bool director = true)
        {
            RunModeSelection.Current = mode;
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = director;
            if (game.Master != null) game.Master.BannersEnabled = false;
            canvas = game.Hud.GetComponentInParent<Canvas>();
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(100000f);
            yield return null;
        }

        /// <summary>활성 Text 중 글자가 칸(권장 크기 기준)을 넘치거나 화면 밖인 것, 최소 글자 크기를 로그로 남긴다.</summary>
        void Audit(string tag, int w, int h)
        {
            var screen = new Rect(0, 0, w, h);
            int n = 0, minFont = 999; string minName = "";
            foreach (var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text) || t.canvasRenderer.GetAlpha() < 0.05f) continue;
                var cg = t.GetComponentInParent<CanvasGroup>(); if (cg != null && cg.alpha < 0.05f) continue;
                var c = new Vector3[4]; t.rectTransform.GetWorldCorners(c);
                var cam = canvas != null ? canvas.worldCamera : null;
                var a = RectTransformUtility.WorldToScreenPoint(cam, c[0]); var b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                float scale = t.canvas != null ? t.canvas.scaleFactor : 1f;
                int eff = t.resizeTextForBestFit ? Mathf.Max(t.cachedTextGenerator.fontSizeUsedForBestFit, 0) : t.fontSize;
                if (eff > 0 && eff * scale < minFont) { minFont = Mathf.RoundToInt(eff * scale); minName = $"{t.name}:'{Trim(t.text)}'"; }
                bool offscreen = r.xMin < -2 || r.yMin < -2 || r.xMax > w + 2 || r.yMax > h + 2;
                bool overflowH = !t.resizeTextForBestFit && t.horizontalOverflow == HorizontalWrapMode.Wrap && t.verticalOverflow == VerticalWrapMode.Truncate
                    && t.cachedTextGenerator.lineCount > 0 && t.preferredHeight > t.rectTransform.rect.height + 1f;
                if (offscreen || overflowH) { n++; Line($"  FLAG {tag} {w}x{h} {(offscreen ? "OFFSCREEN" : "OVERFLOW")} {t.name} fs={t.fontSize} '{Trim(t.text)}'"); }
            }
            Line($"audit {tag} {w}x{h} flagged={n}");
        }

        static string Trim(string s) { s = s.Replace("\n", " / "); return s.Length > 60 ? s.Substring(0, 60) + "…" : s; }

        IEnumerator Shots(string tag, Action refresh = null)
        {
            foreach (var (w, h) in Sizes)
            {
                yield return null;
                refresh?.Invoke();
                yield return null;
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, w, h, $"{tag}_{w}x{h}", () =>
                {
                    refresh?.Invoke();
                    Canvas.ForceUpdateCanvases();
                    Audit(tag, w, h);
                });
            }
        }

        void Refresh()
        {
            game.Hud.PhaseBadge.RefreshForTests();
            if (game.DrinkHud != null) game.DrinkHud.RefreshForTests();
            if (game.MasterHud != null) game.MasterHud.Refresh();
        }

        static Job[] Jobs = { Job.Bartender, Job.TeaSommelier };

        // ---- 런 HUD: 게이지 + 디버프 4종 + 커피 연구 + 새 준비 음료 칩 공존 ----
        IEnumerator HudScene(Job job, RunMode mode, bool withDebuffs, string name)
        {
            Reserve(Recipe("진 빌드", (DrinkIngredients.Gin, 60)));
            yield return Start(job, mode, WeaponKind.Rapier, mode == RunMode.Standard);
            game.AddIngredient(IngredientType.Cocktail, 3); game.AddIngredient(IngredientType.Tea, 2); game.AddIngredient(IngredientType.Coffee, 40); game.Rewards.DisarmTreeAutoOpen();
            var st = game.Player.Stats;
            if (withDebuffs)
            {
                st.SuppressBonuses(5f); st.ApplySlow(3f, 0.7f); st.ApplyAttackSlow(3f, 0.75f); st.BlockHeal(4f);
            }
            for (int i = 0; i < 3; i++) { game.Rewards.DisarmTreeAutoOpen(); if (game.State == GameState.SkillSelect) game.SkillView.Continue(); yield return null; }
            // 시간이 흘러 지워지지 않게 계속 갱신
            yield return Shots(name, () =>
            {
                if (withDebuffs) { st.SuppressBonuses(5f); st.ApplySlow(3f, 0.7f); st.ApplyAttackSlow(3f, 0.75f); st.BlockHeal(4f); }
                Refresh();
            });
        }

        [UnityTest] public IEnumerator Hud_Bartender_Standard_Debuffs() { yield return HudScene(Job.Bartender, RunMode.Standard, true, "hud_bartender_standard_4debuffs"); }
        [UnityTest] public IEnumerator Hud_Tea_Standard_Debuffs() { yield return HudScene(Job.TeaSommelier, RunMode.Standard, true, "hud_tea_standard_4debuffs"); }
        [UnityTest] public IEnumerator Hud_Bartender_Master_Debuffs() { yield return HudScene(Job.Bartender, RunMode.Endless, true, "hud_bartender_master_4debuffs"); }
        [UnityTest] public IEnumerator Hud_Tea_Master_Plain() { yield return HudScene(Job.TeaSommelier, RunMode.Endless, false, "hud_tea_master_plain"); }

        // ---- 카드 창(수치 우선) + 그 위 일시정지 ----
        IEnumerator CardScene(Job job, RunMode mode, string name, bool pause)
        {
            yield return Start(job, mode, WeaponKind.Rapier, mode == RunMode.Standard);
            if (mode == RunMode.Endless && game.Master.State.Phase == MasterPhase.Prep) game.Master.StartBattle();
            var gauge = job == Job.Bartender ? IngredientType.Cocktail : IngredientType.Tea;
            for (int i = 0; i < 400 && game.State != GameState.LevelUp; i++) { game.AddIngredient(gauge, 20); yield return null; }
            Assert.AreEqual(GameState.LevelUp, game.State);
            { float until = Time.realtimeSinceStartup + 4f; while (Time.realtimeSinceStartup < until) yield return null; } // 카드 건네기 연출이 끝날 때까지(실시간)
            var parts = new StringBuilder();
            for (int i = 0; i < 6; i++) { try { parts.Append($"[{i}] {game.CardView.AdviceText(i)} | "); } catch { break; } }
            Line($"{name} cards open={game.CardView.IsOpen} {parts}");
            if (pause) { game.OpenPause(); for (int i = 0; i < 10; i++) yield return null; }
            yield return Shots(name);
        }

        [UnityTest] public IEnumerator Card_Bartender_Standard() { yield return CardScene(Job.Bartender, RunMode.Standard, "card_bartender_standard", false); }
        [UnityTest] public IEnumerator Card_Tea_Master() { yield return CardScene(Job.TeaSommelier, RunMode.Endless, "card_tea_master", false); }
        [UnityTest] public IEnumerator PauseOverCard_Bartender_Standard() { yield return CardScene(Job.Bartender, RunMode.Standard, "pause_over_card_bartender_standard", true); }
        [UnityTest] public IEnumerator PauseOverCard_Tea_Master() { yield return CardScene(Job.TeaSommelier, RunMode.Endless, "pause_over_card_tea_master", true); }

        IEnumerator PausePlain(Job job, string name)
        {
            yield return Start(job, RunMode.Standard);
            game.OpenPause();
            for (int i = 0; i < 10; i++) yield return null;
            yield return Shots(name);
        }
        [UnityTest] public IEnumerator Pause_Bartender() { yield return PausePlain(Job.Bartender, "pause_bartender"); }
        [UnityTest] public IEnumerator Pause_Tea() { yield return PausePlain(Job.TeaSommelier, "pause_tea"); }

        [UnityTest]
        public IEnumerator Settings_And_Tutorial_FromPause()
        {
            yield return Start(Job.Bartender, RunMode.Standard);
            game.OpenPause();
            for (int i = 0; i < 5; i++) yield return null;
            game.PauseView.SettingsView.Show();
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shots("settings");
            game.PauseView.SettingsView.Hide();
            game.PauseView.TutorialView.Show("닫기", () => { }, Job.Bartender);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shots("tutorial_bartender");
            game.PauseView.TutorialView.Dismiss();
            game.PauseView.TutorialView.Show("닫기", () => { }, Job.TeaSommelier);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shots("tutorial_tea");
        }

        // ---- 마스터 준비 / 제조 ----
        IEnumerator MasterPrep(Job job, string name)
        {
            Reserve(Recipe("진 빌드", (DrinkIngredients.Gin, 60)));
            yield return Start(job, RunMode.Endless, WeaponKind.Rapier, false);
            Assert.AreEqual(MasterPhase.Prep, game.Master.State.Phase);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shots(name + "_prep", Refresh);
            DrinkLab.RecordBossAward(game.RunId, "qa_pick", "qa", new System.Collections.Generic.List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 150) }, true);
            Assert.IsTrue(game.OpenMasterBrew());
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shots(name + "_brew", Refresh);
        }
        [UnityTest] public IEnumerator Master_Bartender() { yield return MasterPrep(Job.Bartender, "master_bartender"); }
        [UnityTest] public IEnumerator Master_Tea() { yield return MasterPrep(Job.TeaSommelier, "master_tea"); }

        [UnityTest]
        public IEnumerator Master_LongRecipeName_Brew()
        {
            Reserve(Recipe("한밤의블루큐라소진레몬셰이킹특제", (DrinkIngredients.Gin, 200), (DrinkIngredients.Rum, 150), (DrinkIngredients.BlueCuracao, 100), (DrinkIngredients.Grenadine, 100)));
            yield return Start(Job.Bartender, RunMode.Endless, WeaponKind.Rapier, false);
            DrinkLab.RecordBossAward(game.RunId, "qa_pick", "qa", new System.Collections.Generic.List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 400), new DrinkAmount(DrinkIngredients.Rum, 300), new DrinkAmount(DrinkIngredients.BlueCuracao, 200), new DrinkAmount(DrinkIngredients.Grenadine, 200) }, true);
            yield return Shots("master_longname_prep", Refresh);
            Assert.IsTrue(game.OpenMasterBrew());
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shots("master_longname_brew", Refresh);
        }

        // ---- 보스 배너·방향, 하이브리드 해금 ----
        [UnityTest]
        public IEnumerator Boss_Banner_Standard()
        {
            yield return Start(Job.Bartender, RunMode.Standard);
            var spec = game.Director.NextSpec; Assert.IsNotNull(spec);
            game.SetRunTimeForTests(spec.time - 6f); yield return null;
            game.Director.SpawnNow(spec);
            for (int i = 0; i < 6; i++) yield return null;
            yield return Shots("boss_banner_bartender", Refresh);
        }

        [UnityTest]
        public IEnumerator Hybrid_Unlock_Window()
        {
            yield return Start(Job.TeaSommelier, RunMode.Standard, WeaponKind.Rapier, false);
            game.HybridView.Show(() => { });
            { float until = Time.realtimeSinceStartup + 4f; while (Time.realtimeSinceStartup < until) yield return null; }
            yield return Shots("hybrid_unlock_tea");
        }

        // ---- 결과 ----
        IEnumerator Result(Job job, RunMode mode, string name)
        {
            yield return Start(job, mode, WeaponKind.Rapier, false);
            game.MarkPlayerDiedForTests();
            float until = Time.realtimeSinceStartup + 15f; while (!game.ResultScreen.IsOpen && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(game.ResultScreen.IsOpen);
            for (int i = 0; i < 20; i++) yield return null;
            yield return Shots(name);
        }
        [UnityTest] public IEnumerator Result_Bartender_Standard() { yield return Result(Job.Bartender, RunMode.Standard, "result_bartender_standard"); }
        [UnityTest] public IEnumerator Result_Tea_Master() { yield return Result(Job.TeaSommelier, RunMode.Endless, "result_tea_master"); }
    }
}
