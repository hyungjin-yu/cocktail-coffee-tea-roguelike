using System;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>커피(세 번째 음료 종류) 레시피·검증·저장·공유 코드·완성 색. 칵테일·차 규칙이 바뀌지 않았는지도 함께 본다.</summary>
    public class CoffeeRecipeTests
    {
        static DrinkRecipe Coffee(CoffeeBrewMethod m, int beanTenths)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            if (beanTenths > 0) r.SetAmount(DrinkIngredients.CoffeeBean, beanTenths);
            r.name = "커피";
            return r;
        }

        [Test]
        public void NewDraft_Coffee_UsesBrewTechniqueMethodDefaultsAndNoIce()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee);
            Assert.AreEqual(DrinkTechnique.Brew, r.Technique);
            Assert.AreEqual(CoffeeBrewMethod.EspressoMachine, r.coffeeMethod);
            Assert.AreEqual(CoffeeBrewing.DefaultWaterTenths(CoffeeBrewMethod.EspressoMachine), r.water);
            Assert.AreEqual(0, r.ice);
            var pour = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.PourOver);
            Assert.AreEqual(CoffeeBrewMethod.PourOver, pour.coffeeMethod);
            Assert.AreEqual(0, (int)DrinkRecipe.NewDraft(DrinkKind.Tea).coffeeMethod, "차·칵테일은 추출 도구 없음");
            Assert.AreEqual(0, (int)DrinkRecipe.NewDraft(DrinkKind.Cocktail).coffeeMethod);
        }

        [TestCase(CoffeeBrewMethod.EspressoMachine, 180)]
        [TestCase(CoffeeBrewMethod.PourOver, 150)]
        [TestCase(CoffeeBrewMethod.MokaPot, 150)]
        public void Validator_CompleteCoffeePerMethod_IsOk_AndFitsItsCup(CoffeeBrewMethod m, int bean)
        {
            var r = Coffee(m, bean);
            var check = DrinkValidator.Check(r);
            Assert.IsTrue(check.Ok, check.First);
            r.water = CoffeeBrewing.WaterMaxTenths(m);
            Assert.IsTrue(DrinkValidator.Check(r).Ok, "최대 추출량도 잔에 들어간다");
            Assert.LessOrEqual(DrinkRules.ServeFillMl(r), DrinkRules.ServeCapacityMl(r) * DrinkRules.ServeFillMax);
        }

        [Test]
        public void Validator_RejectsMissingBeanDoseOutOfRangeWaterIceAndUnknownMethod()
        {
            Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, 0)).Ok, "원두 필수");
            Assert.IsTrue(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, 0), requireComplete: false).Ok, "편집 중 빈 배합은 허용");
            Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, 100)).Ok, "에스프레소 최소 12g");
            Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.PourOver, 200), requireComplete: false).Ok, "핸드 드립 최대 18g은 편집 중에도");
            var water = Coffee(CoffeeBrewMethod.MokaPot, 150); water.water = 3000;
            StringAssert.Contains("추출량", DrinkValidator.Check(water).First);
            var ice = Coffee(CoffeeBrewMethod.PourOver, 150); ice.ice = 1;
            Assert.IsFalse(DrinkValidator.Check(ice).Ok, "커피에는 얼음 없음");
            var unknown = Coffee(CoffeeBrewMethod.PourOver, 150); unknown.coffeeMethod = (CoffeeBrewMethod)99;
            Assert.AreEqual("알 수 없는 추출 도구입니다.", DrinkValidator.Check(unknown).First);
            var tech = Coffee(CoffeeBrewMethod.PourOver, 150); tech.technique = (int)DrinkTechnique.Steep;
            Assert.IsFalse(DrinkValidator.Check(tech).Ok, "커피는 추출 기법만");
        }

        [Test]
        public void Ingredients_CoffeeBeanOnlyInCoffee_OtherIngredientsNotInCoffee()
        {
            var bean = DrinkIngredients.Get(DrinkIngredients.CoffeeBean);
            Assert.IsNotNull(bean);
            Assert.AreEqual(IngredientForm.Leaf, bean.form, "원두는 g 단위 건조 재료");
            Assert.IsTrue(bean.UsableIn(DrinkKind.Coffee));
            Assert.IsFalse(bean.UsableIn(DrinkKind.Tea));
            Assert.IsFalse(bean.UsableIn(DrinkKind.Cocktail));
            Assert.AreEqual("커피", bean.UseLabel);
            Assert.AreEqual("칵테일 · 차", DrinkIngredients.Get(DrinkIngredients.Honey).UseLabel);
            foreach (var d in DrinkIngredients.All)
                if (d.id != DrinkIngredients.CoffeeBean && d.id != DrinkIngredients.SteamedMilk) Assert.IsFalse(d.UsableIn(DrinkKind.Coffee), d.id + ": 칵테일·차 재료는 커피에 쓰지 않는다");
            var milk = DrinkIngredients.Get(DrinkIngredients.SteamedMilk);
            Assert.AreEqual(IngredientForm.Liquid, milk.form);
            Assert.AreEqual(DrinkUse.Coffee, milk.use, "스팀 우유는 커피 전용 선택 재료");
            Assert.AreEqual(DrinkStat.Range, bean.effect, "커피 고유 효과");
            var latte = Coffee(CoffeeBrewMethod.EspressoMachine, 180); latte.water = 300; latte.SetAmount(DrinkIngredients.SteamedMilk, 200);
            Assert.IsTrue(DrinkValidator.Check(latte).Ok, DrinkValidator.Check(latte).First);
            latte.SetAmount(DrinkIngredients.SteamedMilk, 400);
            Assert.IsFalse(DrinkValidator.Check(latte).Ok, "잔 여유를 넘는 우유는 거부");
            var tea = Coffee(CoffeeBrewMethod.PourOver, 150); tea.SetAmount(DrinkIngredients.BlackTea, 20);
            Assert.IsFalse(DrinkValidator.Check(tea).Ok, "찻잎은 커피에 쓸 수 없음");
            var cocktail = DrinkRecipe.NewDraft(DrinkKind.Cocktail); cocktail.SetAmount(DrinkIngredients.Gin, 300); cocktail.SetAmount(DrinkIngredients.CoffeeBean, 50);
            Assert.IsFalse(DrinkValidator.Check(cocktail).Ok, "원두는 칵테일에 쓸 수 없음");
        }

        [Test]
        public void Effects_CoffeeRangeIsSeparateFromCocktailAndTea_AndAppliesToPlayerRange()
        {
            var coffee = DrinkCalculator.Buff(Coffee(CoffeeBrewMethod.PourOver, 150));
            Assert.Greater(coffee[DrinkStat.Range], 0f);
            foreach (DrinkStat stat in Enum.GetValues(typeof(DrinkStat)))
                if (stat != DrinkStat.Range) Assert.AreEqual(0f, coffee[stat], stat.ToString());

            var teaRecipe = DrinkRecipe.NewDraft(DrinkKind.Tea);
            teaRecipe.SetAmount(DrinkIngredients.BlackTea, 20);
            teaRecipe.SetAmount(DrinkIngredients.GreenTea, 20);
            var tea = DrinkCalculator.Buff(teaRecipe);
            Assert.AreEqual(0f, tea[DrinkStat.Range]);
            Assert.Greater(tea[DrinkStat.CooldownReduction], 0f);
            Assert.Greater(tea[DrinkStat.MoveSpeed], 0f);

            var cocktailRecipe = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            cocktailRecipe.SetAmount(DrinkIngredients.Gin, 100);
            cocktailRecipe.SetAmount(DrinkIngredients.Rum, 100);
            var cocktail = DrinkCalculator.Buff(cocktailRecipe);
            Assert.AreEqual(0f, cocktail[DrinkStat.Range]);
            Assert.Greater(cocktail[DrinkStat.Attack], 0f);
            Assert.Greater(cocktail[DrinkStat.AttackSpeed], 0f);

            var stats = new PlayerStats();
            DrinkCalculator.ApplyTo(stats, coffee);
            Assert.AreEqual(coffee[DrinkStat.Range], stats.rangePercent, 1e-6f);
        }

        [Test]
        public void Techniques_BrewOnlyForCoffee_CocktailAndTeaUnchanged()
        {
            Assert.IsTrue(DrinkRules.AllowedTechnique(DrinkKind.Coffee, DrinkTechnique.Brew));
            Assert.IsFalse(DrinkRules.AllowedTechnique(DrinkKind.Coffee, DrinkTechnique.Shake));
            Assert.IsFalse(DrinkRules.AllowedTechnique(DrinkKind.Cocktail, DrinkTechnique.Brew));
            Assert.IsFalse(DrinkRules.AllowedTechnique(DrinkKind.Tea, DrinkTechnique.Brew));
            foreach (var t in new[] { DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer })
                Assert.IsTrue(DrinkRules.AllowedTechnique(DrinkKind.Cocktail, t), t.ToString());
            Assert.IsTrue(DrinkRules.AllowedTechnique(DrinkKind.Tea, DrinkTechnique.Steep));
            Assert.AreEqual(DrinkTechnique.Shake, DrinkRules.DefaultTechnique(DrinkKind.Cocktail));
            Assert.AreEqual(DrinkTechnique.Steep, DrinkRules.DefaultTechnique(DrinkKind.Tea));
        }

        [Test]
        public void SetCoffeeMethod_ResetsWaterKeepsBean_SameMixSeesMethod()
        {
            var r = Coffee(CoffeeBrewMethod.EspressoMachine, 150);
            var before = r.Clone();
            r.SetCoffeeMethod(CoffeeBrewMethod.MokaPot);
            Assert.AreEqual(CoffeeBrewMethod.MokaPot, r.coffeeMethod);
            Assert.AreEqual(CoffeeBrewing.DefaultWaterTenths(CoffeeBrewMethod.MokaPot), r.water);
            Assert.AreEqual(150, r.AmountOf(DrinkIngredients.CoffeeBean));
            Assert.IsFalse(before.SameMix(r));
            var same = before.Clone(); same.coffeeMethod = CoffeeBrewMethod.PourOver;
            Assert.IsFalse(before.SameMix(same), "추출 도구만 달라도 다른 배합");
            var tea = DrinkRecipe.NewDraft(DrinkKind.Tea);
            tea.SetCoffeeMethod(CoffeeBrewMethod.PourOver);
            Assert.AreEqual(0, (int)tea.coffeeMethod, "차에는 도구를 넣지 않는다");
        }

        [Test]
        public void SaveJson_PersistsMethod_OldRecipeWithoutFieldReadsZero()
        {
            var data = new DrinkSaveData();
            data.recipes.Add(Coffee(CoffeeBrewMethod.PourOver, 150));
            var back = JsonUtility.FromJson<DrinkSaveData>(JsonUtility.ToJson(data));
            Assert.AreEqual(CoffeeBrewMethod.PourOver, back.recipes[0].coffeeMethod);
            Assert.AreEqual(DrinkKind.Coffee, back.recipes[0].Kind);
            Assert.IsTrue(back.recipes[0].SameMix(data.recipes[0]));
            var old = JsonUtility.FromJson<DrinkRecipe>("{\"schemaVersion\":1,\"name\":\"옛 차\",\"kind\":2,\"technique\":5,\"steps\":[{\"id\":\"black_tea\",\"amount\":20}],\"water\":1500,\"ice\":0}");
            Assert.AreEqual(0, (int)old.coffeeMethod);
            Assert.IsTrue(DrinkValidator.Check(old).Ok, DrinkValidator.Check(old).First);
        }

        [TestCase(CoffeeBrewMethod.EspressoMachine)]
        [TestCase(CoffeeBrewMethod.PourOver)]
        [TestCase(CoffeeBrewMethod.MokaPot)]
        public void ShareCode_RoundTripsCoffeeMethod(CoffeeBrewMethod m)
        {
            var r = Coffee(m, m == CoffeeBrewMethod.EspressoMachine ? 180 : 150);
            var code = DrinkShareCode.Encode(r);
            Assert.LessOrEqual(code.Length, DrinkShareCode.MaxCodeLength);
            var back = DrinkShareCode.Decode(code, out var err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(m, back.coffeeMethod);
            Assert.IsTrue(r.SameMix(back));
        }

        [Test]
        public void ShareCode_CocktailAndTeaCodesUnchangedAndStillDecode()
        {
            var tea = DrinkRecipe.NewDraft(DrinkKind.Tea); tea.SetAmount(DrinkIngredients.BlackTea, 20); tea.name = "차";
            var back = DrinkShareCode.Decode(DrinkShareCode.Encode(tea), out var err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(0, (int)back.coffeeMethod);
            Assert.IsTrue(tea.SameMix(back));
            // 형식상 최대 커피(재료 8줄 + 최대 이름)도 선언한 최대 길이 안
            var max = Coffee(CoffeeBrewMethod.MokaPot, 0);
            for (int i = 0; i < 8; i++) max.steps.Add(new DrinkStep(DrinkIngredients.ShareIndex[i], 65535));
            max.name = new string('힣', DrinkRules.RecipeNameMax);
            Assert.LessOrEqual(DrinkShareCode.Encode(max).Length, DrinkShareCode.MaxCodeLength);
        }

        [Test]
        public void FinalCoffee_VolumeIsWater_ColorDarkensWithConcentration()
        {
            var espresso = Coffee(CoffeeBrewMethod.EspressoMachine, 180);
            var pour = Coffee(CoffeeBrewMethod.PourOver, 150);
            var fin = DrinkPhysicsSim.Final(espresso);
            Assert.AreEqual(espresso.water / 10.0, fin.TotalVolume, 0.01);
            var layers = DrinkPhysicsSim.Layers(fin, espresso);
            Assert.Greater(layers.Count, 0);
            var c = layers[0].color;
            Assert.Greater(c.r, c.b, "갈색(붉은 쪽 > 푸른 쪽)");
            Assert.Greater(c.a, 0.6f, "진한 커피는 불투명에 가깝다");
            float dark(Color x) => x.r + x.g + x.b;
            Assert.Less(dark(CoffeeBrewing.BrewColor(espresso)), dark(CoffeeBrewing.BrewColor(pour)), "에스프레소가 핸드 드립보다 진하다");
            Assert.AreEqual(0, DrinkPhysicsSim.Build(pour).TotalVolume, 1e-6, "편집 중 도구에는 물이 없다(추출 때 붓는다)");
        }

        [Test]
        public void CoffeeArt_AllContractAssetsLoadWithAnchorsAndMasks()
        {
            foreach (var m in CoffeeBrewing.All)
            {
                Assert.IsTrue(DrinkArt.Has(CoffeeBrewing.ApparatusArt(m)), CoffeeBrewing.ApparatusArt(m));
                Assert.IsTrue(DrinkArt.Has(CoffeeBrewing.CupArt(m)), CoffeeBrewing.CupArt(m));
                Assert.IsNotNull(DrinkArt.Mask(CoffeeBrewing.CupArt(m)), "잔 마스크 " + m);
            }
            foreach (var id in new[] { CoffeeBrewing.DripperArt, CoffeeBrewing.ScoopArt, "dl_icon_coffee_bean", "dl_tin_coffee_bean" })
                Assert.IsTrue(DrinkArt.Has(id), id);
            Assert.AreEqual(3, DrinkArt.Frames(CoffeeBrewing.FlameArt).Length);
            Assert.AreEqual(4, DrinkArt.Frames("dl_coffee_espresso_brew").Length);
            Assert.AreEqual(3, DrinkArt.Frames("dl_leaf_coffee_bean").Length);
            // 추출구는 데미타스 잔 입구보다 위(받침 위 잔이 추출구 아래에 들어간다).
            Assert.Greater(DrinkArt.Anchor("dl_coffee_espresso", "pourLip").y * 64f, 6f + 18f);
            Assert.Greater(DrinkArt.Anchor("dl_coffee_carafe", "pourLip").x, DrinkArt.Anchor("dl_coffee_carafe", "grip").x, "서버 주둥이는 잔 쪽(오른쪽)");
            Assert.Greater(DrinkArt.Anchor("dl_coffee_moka", "pourLip").x, DrinkArt.Anchor("dl_coffee_moka", "grip").x);
            Assert.IsTrue(DrinkArt.Has("dl_bottle_steamed_milk") && DrinkArt.Has("dl_icon_steamed_milk"), "스팀 우유 피처·아이콘");
        }
    }
}
