using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-15 플레이 피드백 4 커피 완성 음료(게임 분류): 용량·상한 표 일관성, 검증, 저장·공유 코드 호환, 제조 재료 소모.</summary>
    public class CoffeeStyleTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "coffeestyle_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
        }

        [TearDown]
        public void TearDown()
        {
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        static DrinkRecipe Make(CoffeeStyle s, int milk, int hotWater, int foam, int water = 450, int dose = 180)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.EspressoMachine);
            r.water = water;
            r.SetAmount(DrinkIngredients.CoffeeBean, dose);
            if (milk > 0) r.SetAmount(DrinkIngredients.SteamedMilk, milk);
            r.coffeeStyle = (int)s; r.hotWater = hotWater; r.foam = foam;
            return r;
        }

        [Test]
        public void Table_MaxCombinationFitsEveryCup_WithHeadroom_LatteNoLongerBlockedByOld100mLCap()
        {
            foreach (var s in CoffeeStyles.All)
            {
                if (s == CoffeeStyle.None) continue;
                var r = Make(s, CoffeeStyles.MilkMax(s), CoffeeStyles.HotWaterMax(s), CoffeeStyles.FoamLevel(s));
                float total = CoffeeStyles.TotalVolumeMl(r), room = DrinkRules.ServeCapacityMl(r) * DrinkRules.ServeFillMax;
                Assert.LessOrEqual(total, room + 0.001f, $"{s}: 최대 추출 45 + 최대 첨가 + 거품 = {total} ≤ 잔 82% {room}");
                Assert.IsTrue(DrinkValidator.Check(r).Ok, $"{s} 최대 조합 통과: {DrinkValidator.Check(r).First}");
                var min = Make(s, CoffeeStyles.MilkMin(s), CoffeeStyles.HotWaterMin(s), CoffeeStyles.FoamLevel(s), 250, 120);
                Assert.IsTrue(DrinkValidator.Check(min).Ok, $"{s} 최소 조합 통과: {DrinkValidator.Check(min).First}");
            }
            Assert.Greater(CoffeeStyles.MilkMin(CoffeeStyle.Latte), DrinkRules.CoffeeAdditiveMax, "라떼 우유 하한이 기존 기본 상한(100)보다 큼 → 스타일별 상한으로 해소");
            Assert.AreEqual(DrinkRules.CoffeeAdditiveMax, CoffeeStyles.MilkMax(CoffeeStyle.None), "기본은 기존 상한 그대로");
        }

        [Test]
        public void Validator_RealSteps_Required_NotJustName()
        {
            Assert.IsFalse(DrinkValidator.Check(Make(CoffeeStyle.Latte, 1700, 0, 0)).Ok, "라떼: 거품 단계 없이는 미완성");
            StringAssert.Contains("거품 내기", DrinkValidator.Check(Make(CoffeeStyle.Latte, 1700, 0, 0)).First);
            Assert.IsFalse(DrinkValidator.Check(Make(CoffeeStyle.Latte, 1000, 0, 1)).Ok, "라떼 우유 부족");
            Assert.IsFalse(DrinkValidator.Check(Make(CoffeeStyle.Cappuccino, 800, 0, 1)).Ok, "카푸치노는 두꺼운 거품(2)");
            Assert.IsTrue(DrinkValidator.Check(Make(CoffeeStyle.Cappuccino, 800, 0, 2)).Ok);
            Assert.IsFalse(DrinkValidator.Check(Make(CoffeeStyle.Americano, 0, 500, 0)).Ok, "아메리카노 물 부족");
            Assert.IsFalse(DrinkValidator.Check(Make(CoffeeStyle.Americano, 300, 1500, 0)).Ok, "아메리카노에 우유 금지");
            Assert.IsTrue(DrinkValidator.Check(Make(CoffeeStyle.Americano, 0, 1500, 0)).Ok);
            var drip = Make(CoffeeStyle.Latte, 1700, 0, 1); drip.coffeeMethod = CoffeeBrewMethod.PourOver; drip.water = 2400; drip.SetAmount(DrinkIngredients.CoffeeBean, 170);
            Assert.IsFalse(DrinkValidator.Check(drip).Ok, "완성 음료는 에스프레소 머신에서만");
            Assert.IsFalse(DrinkValidator.Check(Make(CoffeeStyle.None, 500, 1000, 0)).Ok, "기본에 뜨거운 물 금지");
            var bad = Make(CoffeeStyle.None, 0, 0, 0); bad.coffeeStyle = 9;
            Assert.IsFalse(DrinkValidator.Check(bad).Ok, "알 수 없는 값");
        }

        [Test]
        public void OldSave_WithoutStyleFields_ReadsAsBasic_AndStillValid()
        {
            var r = Make(CoffeeStyle.None, 100, 0, 0); // 45 + 10 = 55 ≤ 데미타세 57
            var json = JsonUtility.ToJson(r).Replace(",\"coffeeStyle\":0", "").Replace(",\"hotWater\":0", "").Replace(",\"foam\":0", "");
            StringAssert.DoesNotContain("coffeeStyle", json);
            var back = JsonUtility.FromJson<DrinkRecipe>(json);
            Assert.AreEqual(0, back.coffeeStyle); Assert.AreEqual(0, back.hotWater); Assert.AreEqual(0, back.foam);
            Assert.AreEqual(CoffeeStyle.None, CoffeeStyles.Of(back));
            Assert.IsTrue(DrinkValidator.Check(back).Ok, DrinkValidator.Check(back).First);
            Assert.AreEqual(CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.EspressoMachine), DrinkRules.ServeCapacityMl(back), "기본은 기존 잔 용량");
        }

        [Test]
        public void ShareCode_BasicStaysFormat1_StyleRoundTripsFormat2()
        {
            var basic = Make(CoffeeStyle.None, 100, 0, 0); basic.name = "기본 샷";
            var code1 = DrinkShareCode.Encode(basic);
            var b1 = DrinkShareCode.Decode(code1, out var e1);
            Assert.IsNotNull(b1, e1);
            Assert.AreEqual(0, b1.coffeeStyle);
            foreach (var s in new[] { CoffeeStyle.Americano, CoffeeStyle.Latte, CoffeeStyle.FlatWhite, CoffeeStyle.Cappuccino })
            {
                var r = Make(s, CoffeeStyles.MilkMin(s), CoffeeStyles.HotWaterMin(s), CoffeeStyles.FoamLevel(s)); r.name = CoffeeStyles.Name(s);
                var code = DrinkShareCode.Encode(r);
                StringAssert.StartsWith(DrinkShareCode.Prefix, code);
                var back = DrinkShareCode.Decode(code, out var err);
                Assert.IsNotNull(back, s + " " + err);
                Assert.AreEqual((int)s, back.coffeeStyle); Assert.AreEqual(r.hotWater, back.hotWater); Assert.AreEqual(r.foam, back.foam);
                Assert.AreEqual(r.AmountOf(DrinkIngredients.SteamedMilk), back.AmountOf(DrinkIngredients.SteamedMilk));
            }
        }

        [Test]
        public void Craft_ConsumesMilkOnce_HotWaterAndFoamFree_RefundRestores_PreparedKeepsStyle()
        {
            DrinkLab.Data.Add(DrinkIngredients.SteamedMilk, 3000);
            DrinkLab.Data.Add(DrinkIngredients.CoffeeBean, 500);
            int milk0 = DrinkLab.Owned(DrinkIngredients.SteamedMilk), bean0 = DrinkLab.Owned(DrinkIngredients.CoffeeBean);
            var latte = Make(CoffeeStyle.Latte, 1600, 0, 1); latte.name = "라떼";
            var res = DrinkLab.Craft(latte);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, res.Status, res.Message);
            Assert.AreEqual(milk0 - 1600, DrinkLab.Owned(DrinkIngredients.SteamedMilk), "우유 1회 차감");
            Assert.AreEqual(bean0 - 180, DrinkLab.Owned(DrinkIngredients.CoffeeBean));
            Assert.AreEqual((int)CoffeeStyle.Latte, DrinkLab.Prepared.recipe.coffeeStyle, "준비 음료(런 음용·판매 대상)에 스타일 보존");
            Assert.AreEqual(DrinkLab.CraftStatus.AlreadyPrepared, DrinkLab.Craft(latte).Status, "연속 제조 = 두 번째 거부(이중 차감 없음)");
            Assert.AreEqual(milk0 - 1600, DrinkLab.Owned(DrinkIngredients.SteamedMilk));
            Assert.IsTrue(DrinkLab.CancelPreparedAndRefund(out var err), err);
            Assert.AreEqual(milk0, DrinkLab.Owned(DrinkIngredients.SteamedMilk), "취소·회수 = 그대로 환불");

            var amer = Make(CoffeeStyle.Americano, 0, 1500, 0); amer.name = "아메리카노";
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(amer).Status);
            Assert.AreEqual(milk0, DrinkLab.Owned(DrinkIngredients.SteamedMilk), "뜨거운 물은 재고와 무관");
            Assert.AreEqual("아메리카노", DrinkLab.DefaultName(amer));
        }
    }
}
