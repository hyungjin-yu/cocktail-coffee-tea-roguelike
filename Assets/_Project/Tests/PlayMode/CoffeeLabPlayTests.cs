using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>커피 제조 화면: 종류 버튼 → 추출 도구 선택 → 원두 스쿱 계량 → 도구별 추출 모션(손 없음) → 완성 잔·저장된 도구.</summary>
    public class CoffeeLabPlayTests
    {
        string dir, path;

        static void Clear()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkLabView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.transform.root.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Clear();
            dir = Path.Combine(Application.temporaryCachePath, "coffeeplay_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "drinks.json");
            DrinkLab.SetPathForTests(path);
            DrinkLab.Data.Add(DrinkIngredients.CoffeeBean, 1000); // 100.0g (원두 지급·드롭 연결은 코디네이터 통합 범위)
            DrinkLab.Data.Add(DrinkIngredients.SteamedMilk, 3000);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Clear();
            yield return null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        IEnumerator OpenCoffee(Action<DrinkLabView> got)
        {
            var select = CharacterSelectView.Show((j, w) => { });
            yield return null;
            select.DrinkLabButton.onClick.Invoke();
            yield return null;
            var lab = select.DrinkLabScreen;
            Assert.IsNotNull(lab);
            var btn = GameObject.Find("KindCoffee");
            Assert.IsNotNull(btn, "커피 종류 버튼");
            btn.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(DrinkKind.Coffee, lab.Kind);
            got(lab);
        }

        static IEnumerator Until(Func<bool> cond, float timeout = 15f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 초과");
        }

        [UnityTest]
        public IEnumerator CoffeeKind_ListsOnlyBeans_MethodButtonsSwapCenteredApparatus()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            Assert.AreEqual(2, lab.ListRowCount, "커피 재료: 원두·스팀 우유");
            Assert.AreEqual(DrinkTechnique.Brew, lab.Draft.Technique);
            foreach (var m in CoffeeBrewing.All)
            {
                var b = GameObject.Find("Method" + m);
                Assert.IsNotNull(b, "추출 도구 버튼 " + m);
                b.GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.AreEqual(m, lab.Draft.coffeeMethod);
                Assert.AreEqual(CoffeeBrewing.DefaultWaterTenths(m), lab.Draft.water);
                Assert.AreEqual(CoffeeBrewing.ApparatusArt(m), lab.Stage.MainVessel.AssetId);
                Assert.IsFalse(lab.Stage.MainVessel.Placeholder, "실제 아트 " + m);
                Assert.AreEqual(m == CoffeeBrewMethod.PourOver, lab.Stage.DripperRect != null, "드리퍼는 핸드 드립에만");
                Assert.AreEqual(m == CoffeeBrewMethod.PourOver, lab.Stage.KettleRect.gameObject.activeSelf, "주전자는 핸드 드립에만");
                Assert.AreEqual(m == CoffeeBrewMethod.MokaPot, lab.Stage.FlameRect != null, "버너 불꽃은 모카포트에만");
                StringAssert.Contains(CoffeeBrewing.DisplayName(m), lab.NoticeText);
            }
            // 칵테일로 돌아가면 기존 그대로
            GameObject.Find("KindCocktail").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(DrinkKind.Cocktail, lab.Kind);
            Assert.AreEqual(0, (int)lab.Draft.coffeeMethod);
            Assert.IsNull(lab.Stage.DripperRect);
        }

        [UnityTest]
        public IEnumerator BeanScoop_PoursBeansIntoApparatus_ExactDraftAmount()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.PourOver);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get(DrinkIngredients.CoffeeBean));
            Assert.AreEqual(CoffeeBrewing.ScoopArt, stage.LeafToolArt, "원두는 다하가 아니라 원두 스쿱");
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.MeasureDumps >= 1 || stage.InMeasure >= DrinkRules.CoffeeScoopCapacity, 8f);
            stage.StopPour();
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
            int bean = lab.Draft.AmountOf(DrinkIngredients.CoffeeBean);
            Assert.Greater(bean, 0);
            Assert.LessOrEqual(bean, CoffeeBrewing.DoseMaxTenths(CoffeeBrewMethod.PourOver));
            Assert.AreEqual(bean / 10.0, stage.Ledger[DrinkStageView.LedgerVessel].LeafG, 0.01, "장부의 도구 안 원두 = 배합");
            Assert.AreEqual(1000, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "제조 확정 전에는 재고 차감 없음");
        }

        [UnityTest]
        public IEnumerator Craft_EachMethod_PlaysApparatusFlow_FinalCupAndSavedMethod(
            [Values(CoffeeBrewMethod.EspressoMachine, CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot)] CoffeeBrewMethod m)
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(m);
            int bean = m == CoffeeBrewMethod.EspressoMachine ? 180 : 150;
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, bean);
            lab.Draft.name = "커피 " + (int)m;
            lab.RefreshForTests();
            Assert.IsTrue(lab.CraftInteractable, lab.FooterStatusText);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            var stages = new System.Collections.Generic.HashSet<string>();
            yield return Until(() => { if (lab.Stage.CoffeeStage != null) stages.Add(lab.Stage.CoffeeStage); return !lab.Stage.SequencePlaying; }, 20f);
            string expect = m switch { CoffeeBrewMethod.EspressoMachine => "extract", CoffeeBrewMethod.PourOver => "dripperAside", _ => "rise" };
            Assert.IsTrue(stages.Contains(expect), $"{m} 단계: {string.Join(",", stages)}");
            if (m == CoffeeBrewMethod.PourOver) { Assert.IsTrue(stages.Contains("bloom")); Assert.AreEqual(1f, lab.Stage.DripperAside01, 1e-4f); }
            var final = lab.Stage.FinalVessel;
            Assert.AreEqual(CoffeeBrewing.CupArt(m), final.AssetId);
            Assert.IsFalse(final.Placeholder);
            Assert.AreEqual(lab.Draft.water / 10f, final.VolumeMl, 0.05f, "추출량 전부 잔에");
            Assert.AreEqual(1000 - bean, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "원두는 제조 확정에서만 차감");
            StringAssert.StartsWith("완성", lab.Stage.FinishText);
            // 저장된 준비 음료에 추출 도구가 남는다(다시 읽어도).
            DrinkLab.SetPathForTests(path);
            Assert.IsNotNull(DrinkLab.Prepared);
            Assert.AreEqual(m, DrinkLab.Prepared.recipe.coffeeMethod);
            Assert.AreEqual(DrinkKind.Coffee, DrinkLab.Prepared.recipe.Kind);
        }

        [UnityTest]
        public IEnumerator SteamedMilk_PoursIntoCupFirst_EspressoLandsOnTop()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.MokaPot);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get(DrinkIngredients.SteamedMilk));
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.InMeasure >= 200 || stage.MeasureDumps > 0 || stage.StopReason != null, 8f);
            stage.StopPour();
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
            int milk = lab.Draft.AmountOf(DrinkIngredients.SteamedMilk);
            Assert.Greater(milk, 0);
            Assert.AreEqual(milk / 10.0, stage.Ledger[DrinkStageView.LedgerServe].LiquidMl, 0.05, "우유는 잔에");
            Assert.AreEqual(0.0, stage.Ledger[DrinkStageView.LedgerVessel].LiquidMl, 1e-6, "모카포트 안에는 액체 없음");
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, 150);
            lab.Draft.water = 1200;
            lab.RefreshForTests();
            Assert.IsTrue(lab.CraftInteractable, lab.FooterStatusText);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            yield return Until(() => !lab.Stage.SequencePlaying, 20f);
            Assert.AreEqual((1200 + milk) / 10f, lab.Stage.FinalVessel.VolumeMl, 0.1f, "우유 + 추출 커피");
        }

        [UnityTest]
        public IEnumerator Craft_SkipMotion_SameFinalCup()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.MokaPot);
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, 150);
            lab.RefreshForTests();
            lab.SkipMotion = true;
            lab.ConfirmCraft();
            yield return Until(() => !lab.Stage.SequencePlaying, 5f);
            Assert.AreEqual("dl_coffee_cup", lab.Stage.FinalVessel.AssetId);
            Assert.AreEqual(lab.Draft.water / 10f, lab.Stage.FinalVessel.VolumeMl, 0.05f);
        }
    }
}
