using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-11-23 #14 공동 검수 회귀: 실제 렌더 캡처에서 찾은 겹침 3건(디버프 4종 줄, 마스터 준비 패널↔준비 음료 칩, 마스터 제조 버튼↔여러 줄 칩).
    /// 모두 화면 사각형(월드 코너) 기준으로 단언한다 — 저장은 임시 경로로 분리.</summary>
    public class UiReadability14PlayTests
    {
        GameManager game;
        string folder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            folder = System.IO.Path.Combine(Application.temporaryCachePath, "ui14-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(System.IO.Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(System.IO.Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(System.IO.Path.Combine(folder, "drinks.json"));
            CardEducationGuide.SuppressAutomaticForTests = true;
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            RunModeSelection.Current = RunMode.Standard;
            Time.timeScale = 1f;
            yield return null;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { System.IO.Directory.Delete(folder, true); } catch { }
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static DrinkRecipe LongRecipe()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Stir;
            r.name = "한밤의블루큐라소진레몬셰이킹특제";
            foreach (var (id, ml) in new[] { (DrinkIngredients.Gin, 200), (DrinkIngredients.Rum, 150), (DrinkIngredients.BlueCuracao, 100), (DrinkIngredients.Grenadine, 100) }) r.steps.Add(new DrinkStep(id, ml));
            return r;
        }

        IEnumerator StartMaster(bool drink)
        {
            if (drink)
            {
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(LongRecipe()).Status);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            RunModeSelection.Current = RunMode.Endless;
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            game.Master.BannersEnabled = false;
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(100000f);
            for (int i = 0; i < 4; i++) yield return null;
            game.DrinkHud.RefreshForTests();
            game.MasterHud.Refresh();
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator MasterPrep_PanelBrewButtonAndNotice_DoNotCoverTallDrinkChip()
        {
            yield return StartMaster(true);
            var chip = game.DrinkHud;
            var canvas = game.Hud.GetComponentInParent<Canvas>();
            foreach (var (w, h) in new[] { (1280, 720), (1024, 768), (1920, 1080) })
                yield return SkinEvidenceCapture.CaptureTo(folder, _ => { }, Camera.main, new[] { canvas }, w, h, $"prep_{w}x{h}", () =>
                {
                    chip.RefreshForTests();
                    game.MasterHud.Refresh();
                    Canvas.ForceUpdateCanvases();
                    Assert.IsTrue(chip.ChipVisible);
                    Assert.GreaterOrEqual(chip.EffectRows.Count(), 3, "여러 줄 칩");
                    var chipRect = WorldRect(chip.ChipRect);
                    Assert.IsTrue(game.MasterHud.Prep.PanelVisible);
                    Assert.IsFalse(WorldRect(game.MasterHud.Prep.Panel).Overlaps(chipRect), $"{w}x{h}: 준비 패널이 오른쪽 위 준비 음료 칩을 가리지 않음 panel={WorldRect(game.MasterHud.Prep.Panel)} chip={chipRect} k={chip.ChipRect.localScale.x}");
                    var brew = WorldRect((RectTransform)Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(x => x.name == "BrewButton").transform);
                    Assert.IsFalse(brew.Overlaps(chipRect), $"{w}x{h}: 음료 만들기 버튼이 여러 줄 칩 위에 겹치지 않음");
                    Assert.Less(brew.yMax, chipRect.yMin + 0.5f, $"{w}x{h}: 버튼은 칩 아래");
                });
        }

        [UnityTest]
        public IEnumerator FourDebuffs_StatusBlock_BelowIcons_ClearOfGaugeBarsAndLeftPanels()
        {
            yield return StartMaster(false);
            var st = game.Player.Stats;
            st.SuppressBonuses(5f); st.ApplySlow(3f, 0.7f); st.ApplyAttackSlow(3f, 0.75f); st.BlockHeal(4f);
            for (int i = 0; i < 3; i++) yield return null;
            Canvas.ForceUpdateCanvases();
            var hud = game.Hud;
            var canvas = hud.GetComponentInParent<Canvas>();
            foreach (var (w, h) in new[] { (1280, 720), (1024, 768), (1920, 1080) })
                yield return SkinEvidenceCapture.CaptureTo(folder, _ => { }, Camera.main, new[] { canvas }, w, h, $"debuff_{w}x{h}", () =>
                {
                    st.SuppressBonuses(5f); st.ApplySlow(3f, 0.7f); st.ApplyAttackSlow(3f, 0.75f); st.BlockHeal(4f);
                    Canvas.ForceUpdateCanvases();
                    Assert.IsTrue(hud.SuppressedStatusVisible);
                    StringAssert.Contains("회복 차단", hud.StatusLineText);
                    var back = WorldRect((RectTransform)hud.transform.Find("SuppressedBacking"));
                    var text = (RectTransform)hud.transform.Find("Suppressed");
                    var label = text.GetComponent<Text>();
                    foreach (var name in new[] { "GaugeBar", "TeaBar", "BeanBar" })
                        Assert.IsFalse(back.Overlaps(WorldRect((RectTransform)hud.transform.Find(name))), $"{w}x{h}: 디버프 줄이 {name}과 겹침");
                    foreach (var p in hud.LeftPanels) Assert.IsFalse(back.Overlaps(WorldRect(p)), $"{w}x{h}: 디버프 줄이 왼쪽 패널 {p.name}과 겹침");
                    // 최소 글자(17)에서도 두 줄 안에 들어가는지(= 최악의 4종 동시도 잘리지 않음)를 직접 잰다.
                    var gs = label.GetGenerationSettings(new Vector2(text.rect.width, 0f));
                    gs.resizeTextForBestFit = false; gs.fontSize = 17; gs.scaleFactor = 1f;
                    var gen = new TextGenerator();
                    float ph = gen.GetPreferredHeight(label.text, gs);
                    Assert.LessOrEqual(ph, text.rect.height + 1f, $"{w}x{h}: 17pt 두 줄이 칸 높이 {text.rect.height} 안(필요 {ph}): {label.text}");
                });
        }
    }
}
