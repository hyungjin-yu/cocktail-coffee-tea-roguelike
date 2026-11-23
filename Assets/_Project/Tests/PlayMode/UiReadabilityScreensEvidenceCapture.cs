using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-23 #14 후속: 카드 도감·영구 강화·제조 연구실(탭)·레시피 서랍 대표 렌더(창·입력 없음, 임시 저장). CCT_EVIDENCE_DIR가 있을 때만 실행.</summary>
    public class UiReadabilityScreensEvidenceCapture
    {
        string dir, folder; readonly StringBuilder log = new StringBuilder(); bool setUp;
        static readonly (int w, int h)[] Sizes = { (1280, 720), (1024, 768), (1920, 1080) };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            folder = Path.Combine(Application.temporaryCachePath, "ui14s-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            if (EventSystem.current == null) new GameObject("EvidenceEventSystem", typeof(EventSystem));
            setUp = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!setUp) yield break;
            File.AppendAllText(Path.Combine(dir, "screens-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            yield return null;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[Ui14Screens] " + s); }

        IEnumerator Shots(Canvas canvas, string tag, Action beforeRender = null)
        {
            foreach (var (w, h) in Sizes)
            {
                yield return null;
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, null, new[] { canvas }, w, h, $"{tag}_{w}x{h}", beforeRender);
            }
        }

        [UnityTest]
        public IEnumerator Codex_And_PermanentShop()
        {
            DrinkLab.Data.coins = 2500;
            var canvas = UiFactory.CreateCanvas(null, "Ui14Codex", 100);
            var codex = CardCodexView.Create(canvas.transform, () => { });
            foreach (var c in CardDatabase.LoadAll().Take(12)) PermanentProgress.DiscoverCard(c.id);
            codex.Show();
            yield return Shots(canvas, "codex");
            codex.OpenDetailFor("aged_puerh");
            yield return Shots(canvas, "codex_detail");
            UnityEngine.Object.Destroy(canvas.gameObject);
            yield return null;
            var canvas2 = UiFactory.CreateCanvas(null, "Ui14Shop", 100);
            var shop = PermanentSkillView.Create(canvas2.transform, () => { });
            shop.Show();
            yield return Shots(canvas2, "permanent_shop");
        }

        [UnityTest]
        public IEnumerator DrinkLab_Tabs_And_Drawer()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Stir;
            r.name = "한밤의블루큐라소진레몬셰이킹특제";
            foreach (var (id, ml) in new[] { (DrinkIngredients.Gin, 200), (DrinkIngredients.Rum, 150), (DrinkIngredients.BlueCuracao, 100), (DrinkIngredients.Grenadine, 100) }) r.steps.Add(new DrinkStep(id, ml));
            DrinkLab.Craft(r);
            var lab = DrinkLabView.Show(() => { });
            var canvas = lab.GetComponent<Canvas>();
            foreach (var tab in new[] { DrinkLabView.Tab.Craft, DrinkLabView.Tab.Codex, DrinkLabView.Tab.Recipes, DrinkLabView.Tab.Orders })
            {
                lab.SetTab(tab);
                for (int i = 0; i < 3; i++) yield return null;
                yield return Shots(canvas, "lab_" + tab.ToString().ToLowerInvariant(), () => { lab.RefreshNoticeLayout(); Canvas.ForceUpdateCanvases(); Line("notice=" + lab.NoticeText); });
            }
            lab.Close();
        }
    }
}
