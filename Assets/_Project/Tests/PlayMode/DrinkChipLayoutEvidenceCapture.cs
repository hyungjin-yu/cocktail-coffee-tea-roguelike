using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-23 준비 음료 칩 배치 증거(창·입력 없음, 가상 렌더). CCT_EVIDENCE_DIR가 있을 때만 실행.
    /// 진 빌드·긴 이름 다효과·패널 없음·보스 접근/전투·마스터(끝없는) 모드를 1280x720 / 1920x1080 / 1024x768(4:3)로 찍고,
    /// 같은 장면에서 칩과 위쪽 구간 배지 사각형이 겹치지 않는지 로그에 남기고 단언한다.
    /// </summary>
    public class DrinkChipLayoutEvidenceCapture
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
            folder = Path.Combine(Application.temporaryCachePath, "chip-evidence-" + Guid.NewGuid().ToString("N"));
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

        void Line(string s) { log.AppendLine(s); Debug.Log("[ChipLayoutEvidence] " + s); }

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

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        IEnumerator Start(RunMode mode, bool director)
        {
            RunModeSelection.Current = mode;
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = director;
            canvas = game.Hud.GetComponentInParent<Canvas>();
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(100000f);
            yield return null;
        }

        IEnumerator Shots(string tag)
        {
            foreach (var (w, h) in Sizes)
            {
                yield return null;
                game.DrinkHud.RefreshForTests();
                game.Hud.PhaseBadge.RefreshForTests();
                yield return null;
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, w, h, $"{tag}_{w}x{h}", () =>
                {
                    game.Hud.PhaseBadge.RefreshForTests();
                    game.DrinkHud.RefreshForTests();
                    Canvas.ForceUpdateCanvases();
                    var hud = game.DrinkHud;
                    var rows = string.Join(" | ", hud.EffectRows.Select(r => r.label + " " + r.value));
                    var badge = game.Hud.PhaseBadge;
                    string overlap = "n/a";
                    if (hud.ChipVisible && badge.Visible)
                    {
                        var c = WorldRect(hud.ChipRect); var b = WorldRect((RectTransform)badge.transform);
                        overlap = c.Overlaps(b) ? "OVERLAP" : $"ok gap={b.yMin - c.yMax:0.0}px(canvas {canvas.scaleFactor:0.###})";
                        Assert.IsFalse(c.Overlaps(b), $"{tag} {w}x{h}: 칩이 배지와 겹침");
                    }
                    Line($"{tag} {w}x{h} chipVisible={hud.ChipVisible} badgeAtTop={badge.AtTop} badge='{badge.Text}' chipTitle='{hud.ChipTitle.text}' rows=[{rows}] state='{hud.ChipStateText}' overlap={overlap}");
                });
            }
        }

        [UnityTest]
        public IEnumerator GinBuild_Standard()
        {
            Reserve(Recipe("진 빌드", (DrinkIngredients.Gin, 60)));
            yield return Start(RunMode.Standard, true);
            yield return Shots("gin_standard");
        }

        [UnityTest]
        public IEnumerator LongNameManyEffects_Standard()
        {
            Reserve(Recipe("한밤의블루큐라소진레몬셰이킹특제", (DrinkIngredients.Gin, 200), (DrinkIngredients.Rum, 150), (DrinkIngredients.BlueCuracao, 100), (DrinkIngredients.Grenadine, 100)));
            yield return Start(RunMode.Standard, true);
            yield return Shots("longname_many_standard");
        }

        [UnityTest]
        public IEnumerator NoDrink_Standard()
        {
            yield return Start(RunMode.Standard, true);
            Assert.IsFalse(game.DrinkHud.ChipVisible, "음료 없으면 칩 없음");
            yield return Shots("nopanel_standard");
        }

        [UnityTest]
        public IEnumerator BossIncomingAndFight_Standard()
        {
            Reserve(Recipe("진 빌드", (DrinkIngredients.Gin, 60)));
            yield return Start(RunMode.Standard, true);
            var spec = game.Director.NextSpec;
            Assert.IsNotNull(spec);
            game.SetRunTimeForTests(spec.time - 6f);
            yield return null;
            yield return Shots("gin_boss_incoming");
            game.Director.SpawnNow(spec);
            yield return null; yield return null;
            yield return Shots("gin_boss_fight");
        }

        [UnityTest]
        public IEnumerator GinBuild_MasterEndless()
        {
            Reserve(Recipe("진 빌드", (DrinkIngredients.Gin, 60)));
            yield return Start(RunMode.Endless, false);
            yield return Shots("gin_master");
        }
    }
}
