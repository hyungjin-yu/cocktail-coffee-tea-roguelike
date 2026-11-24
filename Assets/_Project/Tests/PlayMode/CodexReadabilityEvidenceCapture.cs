using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-11-24 도감 가독성 + 검 카드 시간 표기 증거: 임시 저장·창/입력 없음. CCT_EVIDENCE_DIR가 있을 때만 실행.
    /// 상태: 혼합(발견·미발견·해금/잠금), 전부 미발견, 필터(칵테일/하이브리드), 스크롤 하단(히든·영구·연구), 키보드 포커스 행, 재료 상세, 카드/영구 상세, 무기 선택(검).</summary>
    public class CodexReadabilityEvidenceCapture
    {
        string dir, folder; readonly StringBuilder log = new StringBuilder(); bool setUp;
        static readonly (int w, int h)[] Sizes = { (1280, 720), (1024, 768), (1920, 1080) };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            folder = Path.Combine(Application.temporaryCachePath, "codex1124-" + Guid.NewGuid().ToString("N"));
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
            File.AppendAllText(Path.Combine(dir, "codex-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            yield return null;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[Codex1124] " + s); }

        IEnumerator Shots(Canvas canvas, string tag, Action beforeRender = null)
        {
            foreach (var (w, h) in Sizes)
            {
                yield return null;
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, null, new[] { canvas }, w, h, $"{tag}_{w}x{h}", beforeRender);
            }
        }

        static void Scroll(Canvas canvas, float normalized)
        {
            var sr = canvas.GetComponentInChildren<ScrollRect>(true);
            if (sr != null) { Canvas.ForceUpdateCanvases(); sr.verticalNormalizedPosition = normalized; }
        }

        [UnityTest]
        public IEnumerator Codex_States()
        {
            var cards = CardDatabase.LoadAll();
            foreach (var c in cards.Where(c => c.category != CardCategory.Hybrid).Take(14)) PermanentProgress.DiscoverCard(c.id);
            PermanentProgress.DiscoverCard("liqueur");
            PermanentProgress.DiscoverCard("honey");
            var save = PermanentProgress.Current;
            save.cocktailIngredientsCollected = 3038; save.teaLeavesCollected = 1868; save.coffeeBeansCollected = 292;
            foreach (var id in new[] { "gin", "rum", "vodka", "tequila", "vermouth", "triple_sec", "green", "black", "oolong", "white", "puerh", "jasmine",
                                       "ethiopia", "colombia", "brazil", "guatemala", "kenya", "yirgacheffe" }) if (!save.discoveredMaterialVarietyIds.Contains(id)) save.discoveredMaterialVarietyIds.Add(id);
            save.discoveredSkillIds.AddRange(SkillDatabase.LoadAll().Take(7).Select(s => s.id));
            save.unlockedPermanentSkillIds.Add("dark_roast_blend");

            var canvas = UiFactory.CreateCanvas(null, "Cdx1124", 100);
            var codex = CardCodexView.Create(canvas.transform, () => { });
            codex.Show();
            Line("count=" + codex.CountText);
            Line("liqueur=" + codex.GetCardText("liqueur").desc + " | meta=" + codex.GetCardMeta("liqueur"));
            Line("materials=" + codex.GetMaterialText(IngredientType.Cocktail) + " / " + codex.GetMaterialText(IngredientType.Coffee));
            yield return Shots(canvas, "codex_all");
            codex.SelectCocktailTab(); yield return Shots(canvas, "codex_filter_cocktail");
            codex.SelectHybridTab(); yield return Shots(canvas, "codex_filter_hybrid");
            codex.SelectAllTab();
            Scroll(canvas, 0.5f); yield return Shots(canvas, "codex_scroll_mid");
            Scroll(canvas, 0f); yield return Shots(canvas, "codex_scroll_bottom");
            Scroll(canvas, 1f);
            EventSystem.current.SetSelectedGameObject(GameObject.Find("Row_liqueur"));
            yield return Shots(canvas, "codex_keyboard_focus_card");
            var mat = GameObject.Find("Material_칵테일 재료");
            if (mat != null) { EventSystem.current.SetSelectedGameObject(mat); yield return Shots(canvas, "codex_keyboard_focus_material"); }
            Assert.IsTrue(codex.OpenMaterialDetailForTests(IngredientType.Coffee));
            yield return Shots(canvas, "codex_detail_material_coffee");
            codex.HideDetail();
            codex.OpenDetailFor("liqueur"); yield return Shots(canvas, "codex_detail_card");
            codex.HideDetail();
            codex.OpenDetailFor("dark_roast_blend"); yield return Shots(canvas, "codex_detail_permanent");
            UnityEngine.Object.Destroy(canvas.gameObject);
        }

        [UnityTest]
        public IEnumerator Codex_AllUndiscovered()
        {
            var canvas = UiFactory.CreateCanvas(null, "Cdx1124U", 100);
            var codex = CardCodexView.Create(canvas.transform, () => { });
            codex.Show();
            Line("undiscovered count=" + codex.CountText);
            yield return Shots(canvas, "codex_undiscovered");
            Assert.IsTrue(codex.OpenMaterialDetailForTests(IngredientType.Tea));
            yield return Shots(canvas, "codex_detail_material_tea_undiscovered");
            UnityEngine.Object.Destroy(canvas.gameObject);
        }

        [UnityTest]
        public IEnumerator PermanentShop_States()
        {
            PermanentProgress.AddSpecialty(9);
            var canvas = UiFactory.CreateCanvas(null, "Perm17", 100);
            var shop = PermanentSkillView.Create(canvas.transform, () => { });
            shop.Show();
            Line($"perm summary={PermanentSkillView.Summary()}");
            yield return Shots(canvas, "perm17_before_purchase");
            shop.TryBuy(shop.Nodes.First(n => n.id == "dark_roast_blend"));
            Line("perm purchased=" + shop.LastPurchaseResult + " header=" + shop.HeaderText.Replace("
", " | "));
            shop.GetComponentInChildren<Button>(); // 갱신은 TryBuy → Refresh 안에서 이미 끝났다
            yield return Shots(canvas, "perm17_after_purchase_next_step");
            shop.SelectGroup(PermanentSkillGroup.Attack);
            EventSystem.current.SetSelectedGameObject(GameObject.Find("Node_dark_roast_blend"));
            yield return Shots(canvas, "perm17_owned_detail");
            shop.SelectGroup(PermanentSkillGroup.Master);
            yield return Shots(canvas, "perm17_master_locked");
            PermanentProgress.Current.clearedRuns = 5;
            PermanentProgress.AddSpecialty(300);
            foreach (var id in new[] { "perfect_crema", "precision_tools", "roast_profile", "master_service", "sturdy_tumbler", "veteran_instinct", "padded_apron", "restorative_break", "balanced_tray",
                                       "bean_cupper", "skilled_roasting", "tidy_counter", "careful_sourcing", "origin_roastery", "master_blend" })
                shop.TryBuy(shop.Nodes.First(n => n.id == id));
            shop.SelectGroup(PermanentSkillGroup.Master);
            yield return Shots(canvas, "perm17_master_complete");
            UnityEngine.Object.Destroy(canvas.gameObject);
        }

        [UnityTest]
        public IEnumerator CharacterSelect_PermanentButton_Buyable()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            PermanentProgress.AddSpecialty(3);
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            Line("char-select shop label=" + view.PermanentShopButton.GetComponentInChildren<Text>().text.Replace("
", " / "));
            yield return Shots(view.GetComponent<Canvas>(), "perm17_charselect_button_buyable");
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [UnityTest]
        public IEnumerator WeaponSelect_SwordCard()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.BartenderButton.onClick.Invoke();
            yield return null;
            var canvas = view.GetComponent<Canvas>();
            Line("sword desc=" + WeaponPreset.Sword.description.Replace("\n", " / "));
            yield return Shots(canvas, "weapon_select_bartender");
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }
    }
}
