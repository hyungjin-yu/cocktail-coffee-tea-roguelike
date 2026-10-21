using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>72 §6 주문·판매·코인 외형: 판매는 준비 음료를 한 번만 소비하고 코인을 같은 거래로 저장, 직접 사용과 중복 없음, 가격 상한 150.</summary>
    public class DrinkSalesTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "drinksales_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            DrinkStore.FaultHookForTests = null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        static void FailSaves() => DrinkStore.FaultHookForTests = stage => { if (stage == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입된 디스크 실패"); };

        [Test]
        public void FreshSave_ThreeOpenOrders_FirstTwoBasic_EachSatisfiableAtFullPrice()
        {
            var orders = DrinkLab.Orders;
            Assert.AreEqual(3, orders.Count);
            Assert.AreEqual("gin", orders[0].ingA); Assert.AreEqual("lemon_juice", orders[0].ingB);
            Assert.AreEqual("black_tea", orders[1].ingA); Assert.AreEqual("honey", orders[1].ingB);
            Assert.AreEqual(DrinkKind.Coffee, (DrinkKind)orders[2].kind);
            Assert.AreEqual(DrinkIngredients.CoffeeBean, orders[2].ingA);
            Assert.AreEqual("", orders[2].ingB);
            for (int i = 0; i < 300; i++)
            {
                var o = i < 3 ? orders[i] : DrinkSales.Generate(i, null);
                Assert.IsTrue(o.IsOpen);
                Assert.AreNotEqual(o.ingA, o.ingB, "지정 재료 중복 없음");
                Assert.AreEqual(o.kind == (int)DrinkKind.Coffee ? DrinkSales.MinFor(o.ingA, (CoffeeBrewMethod)o.method) : DrinkSales.MinFor(o.ingA), o.minA, "주문 최소량에 맞는 재료");
                var r = DrinkSales.Reference(o);
                Assert.IsTrue(DrinkValidator.Check(r).Ok, $"주문 {i}: 모순·용량 초과 없음 — {DrinkValidator.Check(r).First}");
                Assert.AreEqual(150, DrinkSales.Price(o, r), $"주문 {i}: 기법·색까지 맞추면 150");
            }
        }

        [Test]
        public void CoffeeOrder_RequiresCorrectBrewMethodAndSellsForSameCap()
        {
            var o = DrinkLab.Orders[2];
            var r = DrinkSales.Reference(o);
            Assert.IsTrue(DrinkValidator.Check(r).Ok, DrinkValidator.Check(r).First);
            Assert.IsTrue(DrinkSales.CoffeeMethodMatches(o, r));
            Assert.AreEqual(150, DrinkSales.Price(o, r), "추출 방식 보너스와 색 보너스, 기존 최대가 유지");
            var wrongMethod = r.Clone();
            wrongMethod.SetCoffeeMethod((CoffeeBrewMethod)(o.method % 3 + 1));
            Assert.AreEqual(0, DrinkSales.Price(o, wrongMethod), "주문과 다른 추출 도구는 충족되지 않음");
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.IsTrue(DrinkLab.Sell(2, out int got, out var error), error);
            Assert.AreEqual(150, got);
            Assert.AreEqual(150, DrinkLab.Coins);
        }

        [Test]
        public void Sell_ConsumesPreparedOnce_CoinsSameTransaction_SlotRefreshed_NoDoubleUse()
        {
            var o = DrinkLab.Orders[0];
            var r = DrinkSales.Reference(o);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            string drinkId = DrinkLab.Prepared.drinkId;
            Assert.IsTrue(DrinkLab.Sell(0, out int got, out var e), e);
            Assert.AreEqual(150, got);
            Assert.AreEqual(150, DrinkLab.Coins);
            Assert.IsNull(DrinkLab.Prepared, "판매 = 음료 소비");
            Assert.AreNotEqual(o.id, DrinkLab.Orders[0].id, "완료 칸은 바로 다음 주문");
            Assert.IsFalse(DrinkLab.Sell(0, out _, out _), "이중 판매 없음");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(150, DrinkLab.Coins);
            CollectionAssert.Contains(DrinkLab.Data.soldDrinkIds, drinkId);
            Assert.IsNull(DrinkLab.BeginRun("runAfterSale"), "판 음료는 출발용으로 쓸 수 없음");
        }

        [Test]
        public void Price_NoExtraForMoreIngredients_ZeroWhenCoreMissing_InRunCannotSell()
        {
            var o = DrinkLab.Orders[0];
            var r = DrinkSales.Reference(o);
            r.SetAmount(o.ingA, o.minA * 4);
            Assert.LessOrEqual(DrinkSales.Price(o, r), 150, "많이 넣어도 가격 상한");
            var miss = DrinkSales.Reference(o);
            miss.SetAmount(o.ingB, o.minB - 1);
            Assert.AreEqual(0, DrinkSales.Price(o, miss), "지정 재료 최소량 미달 = 판매 불가");
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(DrinkSales.Reference(o)).Status);
            Assert.IsNotNull(DrinkLab.BeginRun("runX"));
            Assert.IsFalse(DrinkLab.Sell(0, out _, out var e));
            StringAssert.Contains("사용 중", e);
        }

        [Test]
        public void SaveFailure_SellChangesNothing()
        {
            var o = DrinkLab.Orders[1];
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(DrinkSales.Reference(o)).Status);
            FailSaves();
            Assert.IsFalse(DrinkLab.Sell(1, out _, out var e));
            StringAssert.Contains("저장", e);
            Assert.AreEqual(0, DrinkLab.Coins);
            Assert.IsNotNull(DrinkLab.Prepared);
            Assert.AreEqual(o.id, DrinkLab.Orders[1].id);
        }

        [Test]
        public void Cancel_RefreshedOnlyAtNormalRunEnd()
        {
            Assert.IsTrue(DrinkLab.CancelOrder(2, out var e), e);
            Assert.IsFalse(DrinkLab.Orders[2].IsOpen);
            Assert.IsFalse(DrinkLab.CancelOrder(2, out _));
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(DrinkSales.Reference(DrinkLab.Orders[0])).Status);
            DrinkLab.BeginRun("runP");
            DrinkLab.EndRun("runP", RunEndKind.Abandoned, 2, true);
            Assert.IsFalse(DrinkLab.Orders[2].IsOpen, "0~4킬 보존 포기는 주문 갱신 없음");
            DrinkLab.BeginRun("runQ");
            DrinkLab.EndRun("runQ", RunEndKind.Died, 30, false);
            Assert.IsTrue(DrinkLab.Orders[2].IsOpen, "정상 종료 정산 때 새 주문");
        }

        [Test]
        public void Discard_ExplicitOnly_AllowsNextCraft()
        {
            var r = DrinkSales.Reference(DrinkLab.Orders[0]);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.AreEqual(DrinkLab.CraftStatus.AlreadyPrepared, DrinkLab.Craft(r).Status, "자동 덮어쓰기 없음");
            Assert.IsTrue(DrinkLab.DiscardPrepared(out var e), e);
            Assert.IsNull(DrinkLab.Prepared);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
        }

        [Test]
        public void Cosmetics_CoinOnly_NoDuplicateWithCash_MapEquipPersists()
        {
            Assert.IsFalse(DrinkLab.BuyCosmetic(DrinkSales.CafeMap, null, out var e));
            StringAssert.Contains("부족", e);
            DrinkLab.Data.coins = 2600;
            Assert.IsTrue(DrinkLab.BuyCosmetic(DrinkSales.CafeMap, null, out e), e);
            Assert.IsFalse(DrinkLab.BuyCosmetic(DrinkSales.CafeMap, null, out _), "중복 구매 없음");
            Assert.IsFalse(DrinkLab.BuyCosmetic(SkinCatalog.TeaJade, id => id == SkinCatalog.TeaJade, out e), "현금으로 이미 보유한 외형은 코인 구매 없음");
            Assert.IsTrue(DrinkLab.BuyCosmetic(SkinCatalog.BartenderMidnight, null, out e), e);
            Assert.AreEqual(100, DrinkLab.Coins);
            Assert.IsTrue(DrinkLab.CoinOwns(SkinCatalog.BartenderMidnight));
            Assert.IsFalse(DrinkLab.EquipMap("map_unknown", out _));
            Assert.IsTrue(DrinkLab.EquipMap(DrinkSales.CafeMap, out e), e);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(DrinkSales.CafeMap, DrinkLab.EquippedMap);
            Assert.AreEqual(0, DrinkLab.Research.researchPoints - 10, "코인은 RP로 바뀌지 않음");
        }
    }
}
