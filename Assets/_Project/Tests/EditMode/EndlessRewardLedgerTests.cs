using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-10 보상 분리(A안 출처 장부): 판매 코인 = 스킨 재화라서 끝없는 모드 재료가 섞인 음료는 마실 수 있지만 팔 수 없다.
    /// 재료 지급·제조·음용은 막지 않는다. 일반 출처 우선 소모, 출처 유지 환불, 재로드·중복 정산·비정상 종료 복구, v3→v4 보존.
    /// </summary>
    public class EndlessRewardLedgerTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "ledger_" + System.Guid.NewGuid().ToString("N"));
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

        static List<DrinkAmount> ItemsFor(DrinkRecipe r)
        {
            var list = new List<DrinkAmount>();
            foreach (var s in r.steps)
            {
                var row = list.Find(x => x.id == s.id);
                if (row != null) row.amount += s.amount; else list.Add(new DrinkAmount(s.id, s.amount));
            }
            return list;
        }

        /// <summary>배합 재료의 일반 재고를 비우고(메모리), 같은 양을 이번 runId의 보상으로 정산한다.</summary>
        static void GrantOnlyFrom(DrinkRecipe r, string runId, bool endless, bool clearFirst = true)
        {
            if (clearFirst) foreach (var s in r.steps) { int owned = DrinkLab.Data.Owned(s.id); if (owned > 0) DrinkLab.Data.Add(s.id, -owned); }
            Assert.IsTrue(DrinkLab.RecordBossAward(runId, "a1", "qa", ItemsFor(r), endless));
            DrinkLab.EndRun(runId, RunEndKind.Died, 10, false);
        }

        [Test]
        public void EndlessSettlement_RecordsLedger_CraftUsesEndless_SaleRefused_NoCoinNoLoss()
        {
            var order = DrinkLab.Orders[0];
            var r = DrinkSales.Reference(order);
            int coins = DrinkLab.Coins;
            GrantOnlyFrom(r, "endlessRun1", true);
            foreach (var it in ItemsFor(r)) Assert.AreEqual(DrinkLab.Owned(it.id), DrinkLab.EndlessOwned(it.id), "정산분 전부 무한 출처");
            Assert.IsTrue(DrinkLab.WouldUseEndless(r), "제조 전 예고");
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status, "무한 재료로도 제조는 가능");
            Assert.IsTrue(DrinkLab.PreparedSaleBlocked);
            string drinkId = DrinkLab.Prepared.drinkId;
            Assert.IsFalse(DrinkLab.Sell(0, out int got, out var err));
            Assert.AreEqual(0, got);
            Assert.AreEqual(DrinkLab.SaleBlockedEndless, err);
            Assert.AreEqual(coins, DrinkLab.Coins, "스킨 재화 증가 0");
            Assert.AreEqual(drinkId, DrinkLab.Prepared.drinkId, "판매 거부 후 음료 그대로");
            Assert.IsTrue(DrinkLab.Orders[0].IsOpen, "주문 그대로");

            DrinkLab.ReloadForTests(); // 재로드 후에도 장부·판매 불가 유지
            Assert.IsTrue(DrinkLab.PreparedSaleBlocked);
            Assert.IsFalse(DrinkLab.Sell(0, out _, out _));
            Assert.AreEqual(coins, DrinkLab.Coins);
            Assert.IsNotNull(DrinkLab.BeginRun("drinkRun"), "판매는 막혀도 마실 수 있음");
        }

        [Test]
        public void NormalSettlement_SellsAsBefore_NormalFirstConsumption_KeepsEndlessUnsold()
        {
            var order = DrinkLab.Orders[0];
            var r = DrinkSales.Reference(order);
            GrantOnlyFrom(r, "normalRun", false);
            GrantOnlyFrom(r, "endlessRun", true, clearFirst: false); // 같은 양을 무한 출처로 더 보유
            Assert.IsFalse(DrinkLab.WouldUseEndless(r), "일반 출처로 충분하면 섞이지 않음");
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.IsFalse(DrinkLab.PreparedSaleBlocked, "일반 재료만 쓴 음료");
            foreach (var it in ItemsFor(r)) Assert.AreEqual(it.amount, DrinkLab.EndlessOwned(it.id), "무한 출처는 그대로 남음");
            int coins = DrinkLab.Coins;
            Assert.IsTrue(DrinkLab.Sell(0, out int got, out var e), e);
            Assert.AreEqual(coins + got, DrinkLab.Coins);
            // 두 번째 잔은 무한 출처만 남았으므로 판매 불가
            Assert.IsTrue(DrinkLab.WouldUseEndless(r));
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.IsTrue(DrinkLab.PreparedSaleBlocked);
        }

        [Test]
        public void MixedOrigins_PartialEndless_Blocked_CancelRefundKeepsOrigins()
        {
            var r = DrinkSales.Reference(DrinkLab.Orders[0]);
            var items = ItemsFor(r);
            foreach (var s in r.steps) { int owned = DrinkLab.Data.Owned(s.id); if (owned > 0) DrinkLab.Data.Add(s.id, -owned); }
            // 첫 재료만 무한 출처, 나머지는 일반 출처
            var endlessPart = new List<DrinkAmount> { items[0] };
            var normalPart = items.GetRange(1, items.Count - 1);
            Assert.IsTrue(DrinkLab.RecordBossAward("mixE", "a", "qa", endlessPart, true));
            DrinkLab.EndRun("mixE", RunEndKind.Died, 9, false);
            Assert.IsTrue(DrinkLab.RecordBossAward("mixN", "a", "qa", normalPart, false));
            DrinkLab.EndRun("mixN", RunEndKind.Died, 9, false);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.IsTrue(DrinkLab.PreparedSaleBlocked, "한 재료라도 무한 출처면 판매 불가");
            Assert.AreEqual(items[0].amount, DrinkLab.Prepared.EndlessAmountOf(items[0].id));
            Assert.IsTrue(DrinkLab.CancelPreparedAndRefund(out var e), e);
            Assert.AreEqual(items[0].amount, DrinkLab.EndlessOwned(items[0].id), "환불도 무한 출처로");
            foreach (var n in normalPart) Assert.AreEqual(0, DrinkLab.EndlessOwned(n.id), "일반 출처는 일반으로");
            foreach (var it in items) Assert.AreEqual(it.amount, DrinkLab.Owned(it.id), "재료 손실 없음");
        }

        [Test]
        public void SaveFailure_RollsBackStockAndLedgerTogether()
        {
            var r = DrinkSales.Reference(DrinkLab.Orders[0]);
            GrantOnlyFrom(r, "endlessFail", true);
            var before = DrinkStore.Serialize(DrinkLab.Data);
            DrinkStore.FaultHookForTests = stage => { if (stage == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입"); };
            Assert.AreEqual(DrinkLab.CraftStatus.SaveFailed, DrinkLab.Craft(r).Status);
            DrinkStore.FaultHookForTests = null;
            var after = JsonUtility.FromJson<DrinkSaveData>(DrinkStore.Serialize(DrinkLab.Data));
            var expect = JsonUtility.FromJson<DrinkSaveData>(before);
            Assert.AreEqual(JsonUtility.ToJson(expect.stock), JsonUtility.ToJson(after.stock));
            Assert.AreEqual(JsonUtility.ToJson(expect.endlessStock), JsonUtility.ToJson(after.endlessStock));
            Assert.IsNull(DrinkLab.Prepared);
        }

        [Test]
        public void DuplicateSettle_And_AbnormalExitRecovery_KeepEndlessOrigin()
        {
            var r = DrinkSales.Reference(DrinkLab.Orders[0]);
            var items = ItemsFor(r);
            foreach (var s in r.steps) { int owned = DrinkLab.Data.Owned(s.id); if (owned > 0) DrinkLab.Data.Add(s.id, -owned); }
            Assert.IsTrue(DrinkLab.RecordBossAward("crashRun", "a", "qa", items, true));
            DrinkLab.ReloadForTests(); // 앱이 정산 없이 꺼졌다가 다시 켜짐
            Assert.IsTrue(DrinkLab.RecoverAbandoned());
            foreach (var it in items) Assert.AreEqual(it.amount, DrinkLab.EndlessOwned(it.id), "복구 정산도 출처 유지");
            DrinkLab.EndRun("crashRun", RunEndKind.Died, 3, false);
            Assert.IsFalse(DrinkLab.RecordBossAward("crashRun", "b", "qa", items, true), "정산된 런에 추가 지급 없음");
            foreach (var it in items) Assert.AreEqual(it.amount, DrinkLab.Owned(it.id), "중복 정산 없음");
        }

        [Test]
        public void V3File_LoadsWithEmptyLedger_SourceFreePreparedStillSells_BackupKept_SavesAsV4()
        {
            var r = DrinkSales.Reference(DrinkLab.Orders[0]);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            var path = Path.Combine(dir, "drinks.json");
            // v3 파일 흉내: schemaVersion만 3(빈 장부 목록 = 장부 없음과 같다)
            var data = JsonUtility.FromJson<DrinkSaveData>(File.ReadAllText(path));
            data.schemaVersion = 3;
            int coins = data.coins;
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            DrinkLab.ReloadForTests();
            Assert.AreEqual(DrinkStore.LoadSource.Original, DrinkLab.LoadSource);
            Assert.IsTrue(File.Exists(path + ".pre-ledger-v3.bak"), "원본 백업");
            Assert.IsNotNull(DrinkLab.Prepared, "기존 준비 음료 보존");
            Assert.IsFalse(DrinkLab.PreparedSaleBlocked, "출처 정보 없는 기존 음료는 그대로 판매 가능");
            Assert.AreEqual(coins, DrinkLab.Coins, "기존 코인 보존");
            Assert.IsTrue(DrinkLab.Sell(0, out _, out var e), e);
            StringAssert.Contains("\"schemaVersion\": 4", File.ReadAllText(path));
        }

        [Test]
        public void FutureV5File_IsProtected_NoWrite()
        {
            var path = Path.Combine(dir, "drinks.json");
            var d = new DrinkSaveData { schemaVersion = 5 };
            File.WriteAllText(path, JsonUtility.ToJson(d, true));
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.WriteBlocked);
            StringAssert.Contains("\"schemaVersion\": 5", File.ReadAllText(path));
        }

        [Test]
        public void Normalize_ClampsCorruptLedgerToStock()
        {
            var d = new DrinkSaveData();
            d.stock.Add(new DrinkAmount("gin", 100));
            d.endlessStock.Add(new DrinkAmount("gin", 900));
            d.endlessStock.Add(new DrinkAmount("not_an_ingredient", 50));
            d.endlessStock.Add(new DrinkAmount("vodka", 30)); // 재고 없음
            DrinkStore.Normalize(d);
            Assert.AreEqual(100, d.EndlessOwned("gin"));
            Assert.AreEqual(0, d.NormalOwned("gin"));
            Assert.AreEqual(1, d.endlessStock.Count);
        }
    }

    /// <summary>끝 단계 확인: 실제 GameManager 끝없는 런 → 처치 보상 기록 → 종료 정산 → 장부 → 판매 시도까지 스킨 재화 증가 0.</summary>
    public class EndlessRewardEndToEndTests
    {
        string dir;
        GameObject root, createdCamera;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "ledger_e2e_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (createdCamera != null) Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        GameManager Start(RunMode mode)
        {
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            var game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier, mode);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
            return game;
        }

        [Test]
        public void EndlessRun_EliteAndNormalSupply_SettledAsEndless_SellAttemptGivesZeroCoins()
        {
            int coinsBefore = DrinkLab.Coins;
            var game = Start(RunMode.Endless);
            for (int i = 0; i < 3; i++) game.OnEnemyKilled(Vector2.zero, EnemyDef.MartiniSpirit); // 정예 처치마다 보급
            for (int i = 0; i < DrinkSupply.NormalKillsPerBundle; i++) game.OnEnemyKilled(Vector2.zero, EnemyDef.Imp);
            var pending = DrinkLab.PendingFor(game.RunId);
            Assert.Greater(pending.Count, 0, "재료 획득 유지");
            foreach (var a in pending) Assert.IsTrue(a.endless, "끝없는 모드 보상 표시");
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            var granted = game.DrinkSettlement.granted;
            Assert.Greater(granted.Count, 0);
            foreach (var g in DrinkLab.Totals(granted))
                Assert.GreaterOrEqual(DrinkLab.EndlessOwned(g.Key), 1, g.Key + " 장부 기록");
            Assert.AreEqual(coinsBefore, DrinkLab.Coins, "런 종료까지 스킨 재화 0");

            // 무한 재료만 남긴 뒤 주문용 음료 제조 → 판매 시도
            foreach (var s in new List<DrinkAmount>(DrinkLab.Data.stock))
            {
                int normal = DrinkLab.Data.NormalOwned(s.id);
                if (normal > 0) DrinkLab.Data.Add(s.id, -normal);
            }
            var r = DrinkSales.Reference(DrinkLab.Orders[0]);
            var items = new List<DrinkAmount>();
            foreach (var st in r.steps) items.Add(new DrinkAmount(st.id, st.amount));
            Assert.IsTrue(DrinkLab.RecordBossAward("endless2", "x", "qa", items, true));
            DrinkLab.EndRun("endless2", RunEndKind.Died, 1, false);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            Assert.IsFalse(DrinkLab.Sell(0, out int got, out _));
            Assert.AreEqual(0, got);
            Assert.AreEqual(coinsBefore, DrinkLab.Coins, "판매 시도까지 스킨 재화 증가 0");
        }

        [Test]
        public void StandardRun_SupplyIsNormalOrigin_SellableAsBefore()
        {
            var game = Start(RunMode.Standard);
            game.OnEnemyKilled(Vector2.zero, EnemyDef.MartiniSpirit);
            foreach (var a in DrinkLab.PendingFor(game.RunId)) Assert.IsFalse(a.endless);
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            foreach (var g in game.DrinkSettlement.granted) Assert.AreEqual(0, DrinkLab.EndlessOwned(g.id));
        }
    }
}
