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
        public void Reload_KeepsOpenCoffeeOrder_StillCancelsBrokenOrders()
        {
            var before = DrinkLab.Orders[2];
            Assert.AreEqual(DrinkKind.Coffee, (DrinkKind)before.kind);
            Assert.IsTrue(before.IsOpen);
            DrinkLab.ReloadForTests(); // 앱을 다시 켠 것과 같은 경로(drinks.json 읽기 → 정리)
            var after = DrinkLab.Orders[2];
            Assert.IsTrue(after.IsOpen, "열린 커피 주문(두 번째 재료 없음·추출 도구로 판정)은 다시 켜도 열린 채");
            Assert.AreEqual(before.id, after.id);
            Assert.AreEqual(before.method, after.method);
            Assert.IsTrue(DrinkLab.Orders[0].IsOpen && DrinkLab.Orders[1].IsOpen);
            // 손상된 주문은 종류별로 여전히 취소 칸이 된다.
            var bad = new DrinkSaveData();
            bad.orders.Add(new DrinkOrder { id = "c1", kind = (int)DrinkKind.Coffee, ingA = DrinkIngredients.CoffeeBean, minA = 80, method = 9, state = "Open" });
            bad.orders.Add(new DrinkOrder { id = "c2", kind = (int)DrinkKind.Coffee, ingA = "gin", minA = 80, method = 1, state = "Open" });
            bad.orders.Add(new DrinkOrder { id = "k1", kind = (int)DrinkKind.Cocktail, ingA = "gin", ingB = "", minA = 75, minB = 0, state = "Open" });
            var path = Path.Combine(dir, "bad.json");
            File.WriteAllText(path, DrinkStore.Serialize(bad));
            var loaded = DrinkStore.Load(path).Data;
            foreach (var o in loaded.orders) Assert.IsFalse(o.IsOpen, $"{o.id}: 손상 주문은 취소");
        }

        [Test]
        public void BlockReason_NamesFirstUnmetCondition_NullExactlyWhenSellable()
        {
            var cocktail = DrinkLab.Orders[0];
            var coffee = DrinkLab.Orders[2];
            var ok = DrinkSales.Reference(cocktail);
            Assert.IsNull(DrinkSales.BlockReason(cocktail, ok));
            Assert.Greater(DrinkSales.Price(cocktail, ok), 0);
            Assert.AreEqual("음료 없음", DrinkSales.BlockReason(cocktail, null));
            Assert.AreEqual("종류 다름", DrinkSales.BlockReason(cocktail, DrinkSales.Reference(DrinkLab.Orders[1])));
            var underA = ok.Clone(); underA.SetAmount(cocktail.ingA, cocktail.minA - DrinkRules.TenthsPerUnit); // 정수 전용: 1 단위 모자람
            Assert.AreEqual("재료 부족", DrinkSales.BlockReason(cocktail, underA));
            var underB = ok.Clone(); underB.SetAmount(cocktail.ingB, cocktail.minB - DrinkRules.TenthsPerUnit);
            Assert.AreEqual("재료 부족", DrinkSales.BlockReason(cocktail, underB));
            var wrongMethod = DrinkSales.Reference(coffee).Clone();
            wrongMethod.SetCoffeeMethod((CoffeeBrewMethod)(coffee.method % 3 + 1));
            Assert.AreEqual("도구 다름", DrinkSales.BlockReason(coffee, wrongMethod));
            var cancelled = DrinkSales.Generate(5, null); cancelled.state = "Cancelled";
            Assert.AreEqual("주문 없음", DrinkSales.BlockReason(cancelled, ok));
            // 이유가 없을 때만 팔 수 있다(가격 > 0) — 300개 생성 주문 × 기준/미달/다른 종류 배합
            for (int i = 0; i < 300; i++)
            {
                var o = DrinkSales.Generate(i, null);
                var r = DrinkSales.Reference(o);
                var under = r.Clone(); under.SetAmount(o.ingA, o.minA - DrinkRules.TenthsPerUnit);
                var other = DrinkSales.Reference(DrinkSales.Generate(i + 1, null));
                foreach (var x in new[] { r, under, other, null })
                    Assert.AreEqual(DrinkSales.Price(o, x) > 0, DrinkSales.BlockReason(o, x) == null, $"주문 {i}: 이유 ↔ 판매 가능 일치");
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
            miss.SetAmount(o.ingB, o.minB - DrinkRules.TenthsPerUnit);
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
