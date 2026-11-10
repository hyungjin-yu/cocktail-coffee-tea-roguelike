using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-06 정수 전용 표기·거래값(사용자 지시 "소수점 싹다 없애. 정수로만"). 반올림 규칙, 음료 수량 경제 계산,
    /// drinks.json v2→v3 마이그레이션(격리 복사본·백업·1회·반복 이득 없음), 표시=적용 일치, 전투 피해 경계, 확률·타이머 경계.
    /// 기본 사용자 세이브는 건드리지 않는다(모든 저장은 Application.temporaryCachePath 아래 격리 경로).
    /// </summary>
    public class IntegerOnlyTests
    {
        static readonly Regex Decimal = new Regex(@"\d[.,]\d");
        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "inttest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "drinks.json");
            DrinkLab.SetPathForTests(path);
            DrinkStore.FaultHookForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            DrinkStore.FaultHookForTests = null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        // ───── 반올림 규칙 ─────

        [TestCase(2.5, 3)] [TestCase(-2.5, -3)] [TestCase(0.5, 1)] [TestCase(1.4999, 1)] [TestCase(3.5, 4)] [TestCase(-0.4, 0)]
        public void Round_MidpointAwayFromZero(double v, int expected) => Assert.AreEqual(expected, IntNum.Round(v));

        [Test]
        public void Round_DiffersFromBankersRounding()
        {
            Assert.AreEqual(2, Mathf.RoundToInt(2.5f), "Unity 기본은 짝수 반올림");
            Assert.AreEqual(3, IntNum.Round(2.5f), "정수 전용 규칙은 0에서 먼 쪽");
            Assert.AreEqual(0, IntNum.Round(double.NaN));
            Assert.AreEqual(int.MaxValue, IntNum.Round(1e20));
        }

        [TestCase(2.0f, 2)] [TestCase(2.00009f, 2)] [TestCase(1.01f, 2)] [TestCase(0.01f, 1)] [TestCase(0f, 0)] [TestCase(-1f, 0)] [TestCase(11.9f, 12)]
        public void RemainingSeconds_Ceil(float seconds, int expected) => Assert.AreEqual(expected, IntNum.RemainingSeconds(seconds));

        [Test]
        public void Percent_Multiplier_Duration_Distance_PerMinute()
        {
            Assert.AreEqual("150%", IntNum.Multiplier(1.5));
            Assert.AreEqual("115%", IntNum.Multiplier(1.15f));
            Assert.AreEqual("95%", IntNum.Multiplier(0.95f));
            Assert.AreEqual(13, IntNum.Percent(0.125), "12.5% → 13%(중간값)");
            Assert.AreEqual(33, IntNum.Percent(1 / 3.0));
            Assert.AreEqual(67, IntNum.Percent(2 / 3.0));
            Assert.AreEqual("500ms", IntNum.Duration(0.5f));
            Assert.AreEqual("350ms", IntNum.Duration(0.35f));
            Assert.AreEqual("1500ms", IntNum.Duration(1.5f));
            Assert.AreEqual("3초", IntNum.Duration(3f));
            Assert.AreEqual("250cm", IntNum.Distance(2.5f));
            Assert.AreEqual("260cm", IntNum.Distance(CoffeeBrewRuntime.MokaBurstRadius));
            Assert.AreEqual(72, IntNum.PerMinute(1.2f));
            Assert.AreEqual(9, IntNum.PerMinute(0.15f));
            Assert.AreEqual(6, IntNum.PerMinute(0.1f));
        }

        [TestCase(10.5f, 11)] [TestCase(10.4f, 10)] [TestCase(0.2f, 1)] [TestCase(2.5f, 3)] [TestCase(0f, 0)] [TestCase(-3f, 0)]
        public void Damage_RoundsWithMinimumOne(float raw, int expected) => Assert.AreEqual(expected, IntNum.Damage(raw));

        // ───── 음료 수량: 표시·입력·거래 ─────

        [TestCase(235, "24")] [TestCase(234, "23")] [TestCase(-15, "-2")] [TestCase(0, "0")] [TestCase(1500, "150")] [TestCase(5, "1")]
        public void Format_IsIntegerOnly(int tenths, string expected) => Assert.AreEqual(expected, DrinkRules.Format(tenths));

        [Test]
        public void Format_WithUnit_HasNoDecimalPoint()
        {
            Assert.AreEqual("23g", DrinkRules.Format(234, IngredientForm.Leaf));
            Assert.AreEqual("45mL", DrinkRules.Format(DrinkRules.JiggerCapacity, IngredientForm.Liquid));
        }

        [TestCase("12", 120)] [TestCase("12.0", 120)] [TestCase(" 5 ", 50)] [TestCase("5ml", 50)] [TestCase("3g", 30)] [TestCase("0", 0)] [TestCase("7.", 70)]
        public void ParseAmount_AcceptsIntegers(string text, int expected)
        {
            Assert.IsTrue(DrinkRules.TryParseAmount(text, out int v, out var e), e);
            Assert.AreEqual(expected, v);
            Assert.AreEqual(0, v % DrinkRules.TenthsPerUnit);
        }

        [TestCase("12.5")] [TestCase("0.1")] [TestCase(".5")] [TestCase("1,5")] [TestCase("2.05")] [TestCase("-1")] [TestCase("1e3")] [TestCase("")] [TestCase("1.2.3")]
        public void ParseAmount_RejectsDecimalsAndJunk(string text)
        {
            Assert.IsFalse(DrinkRules.TryParseAmount(text, out _, out var e));
            Assert.IsFalse(string.IsNullOrEmpty(e));
        }

        [Test]
        public void SetAmount_SnapsToWholeUnits()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.SetAmount(DrinkIngredients.Gin, 235);
            Assert.AreEqual(240, r.AmountOf(DrinkIngredients.Gin), "23.5mL → 24mL(중간값은 0에서 먼 쪽)");
            r.SetAmount(DrinkIngredients.Gin, 234);
            Assert.AreEqual(230, r.AmountOf(DrinkIngredients.Gin));
            r.SetAmount(DrinkIngredients.Gin, 4);
            Assert.AreEqual(0, r.AmountOf(DrinkIngredients.Gin), "0.4mL는 0으로 반올림 → 제거");
            Assert.AreEqual(0, r.steps.Count);
        }

        [Test]
        public void Validator_RejectsFractionalSteps_AndTeaMinimumIsOneGram()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Tea);
            r.steps.Add(new DrinkStep(DrinkIngredients.BlackTea, 15)); // 손상·옛 값 1.5g(직접 넣기)
            Assert.IsFalse(DrinkValidator.Check(r).Ok);
            r.steps[0].amount = 20;
            Assert.IsTrue(DrinkValidator.Check(r).Ok, DrinkValidator.Check(r).First);
            Assert.AreEqual(10, DrinkRules.TeaLeafMin);
        }

        [Test]
        public void StockAdd_RoundsDeltaToWholeUnits()
        {
            var d = new DrinkSaveData();
            d.Add(DrinkIngredients.Gin, 155);
            Assert.AreEqual(160, d.Owned(DrinkIngredients.Gin));
            d.Add(DrinkIngredients.Gin, -44);
            Assert.AreEqual(120, d.Owned(DrinkIngredients.Gin));
            d.Add(DrinkIngredients.Gin, 4); // 0.4 → 0, 변화 없음
            Assert.AreEqual(120, d.Owned(DrinkIngredients.Gin));
        }

        [Test]
        public void OrderMinimums_AndSupplyBundles_AreWholeUnits()
        {
            foreach (var row in DrinkSupply.Table)
            {
                Assert.AreEqual(0, row.bundle % DrinkRules.TenthsPerUnit, row.id + " 묶음");
                int min = DrinkSales.MinFor(row.id);
                Assert.AreEqual(0, min % DrinkRules.TenthsPerUnit, row.id + " 주문 최소량");
                Assert.GreaterOrEqual(min, DrinkRules.TenthsPerUnit, row.id);
            }
            Assert.AreEqual(10, DrinkSales.MinFor(DrinkIngredients.ChamomileFlower), "1.5g의 50% = 0.75g → 1g");
            Assert.AreEqual(10, DrinkSales.MinFor(DrinkIngredients.WhiteTea), "2.5g의 50% = 1.25g → 1g");
            Assert.AreEqual(150, DrinkSales.MinFor(DrinkIngredients.Gin));
            foreach (CoffeeBrewMethod m in CoffeeBrewing.All)
            {
                Assert.AreEqual(0, CoffeeBrewing.DoseMinTenths(m) % 10);
                Assert.AreEqual(0, CoffeeBrewing.WaterStepTenths(m) % 10);
                Assert.AreEqual(0, CoffeeBrewing.DefaultWaterTenths(m) % 10);
            }
            foreach (var item in DrinkLab.StarterKit) Assert.AreEqual(0, item.amount % 10, item.id);
            foreach (var r in DrinkLab.Data.recipes)
            {
                foreach (var s in r.steps) Assert.AreEqual(0, s.amount % 10, r.name + " " + s.id);
                Assert.AreEqual(0, r.water % 10, r.name);
            }
        }

        [Test]
        public void Craft_And_Sell_AreIntegerTransactions_MatchingDisplay()
        {
            var lab = DrinkLab.Data; // 새 격리 저장 + 첫 세트
            int ginBefore = DrinkLab.Owned(DrinkIngredients.Gin), lemonBefore = DrinkLab.Owned(DrinkIngredients.LemonJuice);
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.name = "정수";
            r.SetAmount(DrinkIngredients.Gin, 300);
            r.SetAmount(DrinkIngredients.LemonJuice, 150);
            var res = DrinkLab.Craft(r);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, res.Status, res.Message);
            Assert.AreEqual(ginBefore - 300, DrinkLab.Owned(DrinkIngredients.Gin));
            Assert.AreEqual(lemonBefore - 150, DrinkLab.Owned(DrinkIngredients.LemonJuice));
            foreach (var s in DrinkLab.Data.stock) Assert.AreEqual(0, s.amount % 10, s.id);

            // 판매: 표시 가격(정수) = 실제 증가 코인.
            var order = new DrinkOrder { id = "t", kind = (int)DrinkKind.Cocktail, ingA = DrinkIngredients.Gin, ingB = DrinkIngredients.LemonJuice,
                minA = 150, minB = 50, technique = (int)DrinkTechnique.Shake, state = "Open", colorFamily = DrinkSales.FamilyOf(DrinkLab.Prepared.recipe) };
            DrinkLab.Data.orders[0] = order;
            int price = DrinkSales.Price(order, DrinkLab.Prepared.recipe);
            int coinsBefore = DrinkLab.Data.coins;
            Assert.IsTrue(DrinkLab.Sell(0, out int got, out var err), err);
            Assert.AreEqual(price, got);
            Assert.AreEqual(coinsBefore + price, DrinkLab.Data.coins);
            Assert.AreEqual(DrinkSales.BasePrice + DrinkSales.TechniqueBonus + DrinkSales.ColorBonus, price);

            // 저장·재로드 후 같은 정수.
            int coins = DrinkLab.Data.coins, gin = DrinkLab.Owned(DrinkIngredients.Gin);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(coins, DrinkLab.Data.coins);
            Assert.AreEqual(gin, DrinkLab.Owned(DrinkIngredients.Gin));
        }

        [Test]
        public void ShareCode_LegacyFractionalAmounts_ImportAsWhole()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.name = "옛 코드";
            r.steps.Add(new DrinkStep(DrinkIngredients.Gin, 235)); // 옛 빌드의 23.5mL를 직접 인코딩
            r.steps.Add(new DrinkStep(DrinkIngredients.LemonJuice, 4)); // 0.4mL → 최소 1mL
            var code = DrinkShareCode.Encode(r);
            var back = DrinkShareCode.Decode(code, out var error);
            Assert.IsNotNull(back, error);
            Assert.AreEqual(240, back.AmountOf(DrinkIngredients.Gin));
            Assert.AreEqual(10, back.AmountOf(DrinkIngredients.LemonJuice));
        }

        // ───── 세이브 마이그레이션(v2 → v3) ─────

        static DrinkSaveData LegacyFractional()
        {
            var d = new DrinkSaveData { starterGranted = true, coffeeStarterGranted = true, coins = 125 };
            d.stock.Add(new DrinkAmount(DrinkIngredients.Gin, 455));
            d.stock.Add(new DrinkAmount(DrinkIngredients.Rum, 454));
            d.stock.Add(new DrinkAmount(DrinkIngredients.BlackTea, 5));
            d.stock.Add(new DrinkAmount(DrinkIngredients.Honey, 3));
            var tea = DrinkRecipe.NewDraft(DrinkKind.Tea);
            tea.name = "옛 차";
            tea.steps.Add(new DrinkStep(DrinkIngredients.BlackTea, 15));
            tea.steps.Add(new DrinkStep(DrinkIngredients.Honey, 55));
            d.recipes.Add(tea);
            var prepared = DrinkRecipe.NewDraft(DrinkKind.Tea);
            prepared.name = "준비 차";
            prepared.steps.Add(new DrinkStep(DrinkIngredients.BlackTea, 5)); // 옛 최소 0.1g 규칙에서는 유효
            d.prepared = new PreparedDrink { state = PreparedDrink.Ready, drinkId = "legacy", recipe = prepared };
            d.pending.Add(new DrinkRunAward { runId = "r1", awardId = "a1", label = "보스", items = new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 155) } });
            d.orders.Add(new DrinkOrder { id = "o1", kind = (int)DrinkKind.Tea, ingA = DrinkIngredients.ChamomileFlower, ingB = DrinkIngredients.Honey, minA = 7, minB = 50,
                technique = (int)DrinkTechnique.Steep, state = "Open", colorFamily = "" });
            d.schemaVersion = 2;
            return d;
        }

        string WriteLegacy(DrinkSaveData d)
        {
            var json = JsonUtility.ToJson(d, true);
            File.WriteAllText(path, json);
            return json;
        }

        [Test]
        public void Migration_V2_RoundsOnce_BacksUp_AndSavesV3()
        {
            string original = WriteLegacy(LegacyFractional());
            DrinkLab.ReloadForTests();
            var d = DrinkLab.Data;
            Assert.AreEqual(460, d.Owned(DrinkIngredients.Gin), "45.5 → 46");
            Assert.AreEqual(450, d.Owned(DrinkIngredients.Rum), "45.4 → 45");
            Assert.AreEqual(10, d.Owned(DrinkIngredients.BlackTea), "0.5 → 1");
            Assert.AreEqual(0, d.Owned(DrinkIngredients.Honey), "0.3 → 0");
            Assert.AreEqual(125, d.coins, "코인은 원래 정수 — 그대로");
            var tea = d.recipes.Find(x => x.name == "옛 차");
            Assert.IsNotNull(tea);
            Assert.AreEqual(20, tea.AmountOf(DrinkIngredients.BlackTea));
            Assert.AreEqual(60, tea.AmountOf(DrinkIngredients.Honey));
            Assert.IsTrue(d.prepared.Exists, "옛 0.5g 준비 음료가 새 최소량 검증에 지워지지 않는다(정규화 전에 1g으로)");
            Assert.AreEqual(10, d.prepared.recipe.AmountOf(DrinkIngredients.BlackTea));
            Assert.AreEqual(160, d.pending[0].items[0].amount);
            Assert.AreEqual(10, d.orders[0].minA);
            Assert.AreEqual(2, d.integerMigrationFromVersion);
            Assert.IsFalse(string.IsNullOrEmpty(d.integerMigrationUtc));
            StringAssert.Contains(DrinkLab.NoticeIntegerMigrated, DrinkLab.Notice ?? "");

            string backup = path + ".pre-integer-v2.bak";
            Assert.IsTrue(File.Exists(backup), "원본 백업");
            Assert.AreEqual(original, File.ReadAllText(backup), "백업은 마이그레이션 전 원본 그대로");
            // 2026-11-10: 현재 스키마는 v4(출처 장부 추가). 정수 마이그레이션 뒤 바로 현재 버전으로 저장되고, 장부 백업도 함께 남는다.
            StringAssert.Contains($"\"schemaVersion\": {DrinkStore.CurrentVersion}", File.ReadAllText(path), "바로 현재 버전으로 저장");
            Assert.IsTrue(File.Exists(path + ".pre-ledger-v2.bak"), "v4 장부 백업");
        }

        [Test]
        public void Migration_IsAppliedOnce_NoRepeatedGain()
        {
            string original = WriteLegacy(LegacyFractional());
            DrinkLab.ReloadForTests();
            int gin = DrinkLab.Owned(DrinkIngredients.Gin), black = DrinkLab.Owned(DrinkIngredients.BlackTea);
            string utc = DrinkLab.Data.integerMigrationUtc;

            for (int i = 0; i < 3; i++)
            {
                DrinkLab.ReloadForTests();
                Assert.AreEqual(gin, DrinkLab.Owned(DrinkIngredients.Gin), "재로드마다 다시 올림되지 않는다");
                Assert.AreEqual(black, DrinkLab.Owned(DrinkIngredients.BlackTea));
                Assert.AreEqual(utc, DrinkLab.Data.integerMigrationUtc, "v3 파일은 다시 마이그레이션하지 않는다");
                Assert.IsFalse((DrinkLab.Notice ?? "").Contains(DrinkLab.NoticeIntegerMigrated));
            }

            // 같은 v2 파일이 다시 놓여도(저장 실패 후 재시작 등) 결과는 원본에서 다시 계산한 같은 값 — 누적 이득 없음, 첫 백업 보존.
            var changed = LegacyFractional();
            changed.coins = 999;
            WriteLegacy(changed);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(gin, DrinkLab.Owned(DrinkIngredients.Gin));
            Assert.AreEqual(original, File.ReadAllText(path + ".pre-integer-v2.bak"), "첫 백업을 덮어쓰지 않는다");
        }

        /// <summary>실제 사용자 세이브의 "복사본"(환경 변수 CCT_MIGRATION_SAMPLE)을 격리 경로에서 마이그레이션한다. 원본 경로는 읽지도 쓰지도 않는다.</summary>
        [Test]
        public void Migration_UserSaveCopy_IsolatedRun()
        {
            string sample = Environment.GetEnvironmentVariable("CCT_MIGRATION_SAMPLE");
            if (string.IsNullOrEmpty(sample) || !File.Exists(sample)) Assert.Ignore("CCT_MIGRATION_SAMPLE 없음 — 사용자 세이브 복사본 검증 전용");
            File.Copy(sample, path, true);
            string before = File.ReadAllText(path);
            var raw = JsonUtility.FromJson<DrinkSaveData>(before);
            DrinkLab.ReloadForTests();
            var d = DrinkLab.Data;
            var report = new System.Text.StringBuilder();
            report.AppendLine($"source schema={raw.schemaVersion} -> file now {(File.ReadAllText(path).Contains("\"schemaVersion\": 3") ? 3 : -1)}");
            foreach (var s in d.stock) { Assert.AreEqual(0, s.amount % 10, s.id); report.AppendLine($"stock {s.id} {raw.stock.Find(x => x.id == s.id)?.amount} -> {s.amount}"); }
            for (int i = 0; i < d.orders.Count; i++)
            {
                var o = d.orders[i];
                Assert.AreEqual(0, o.minA % 10); Assert.AreEqual(0, o.minB % 10);
                var ro = i < raw.orders.Count ? raw.orders[i] : null;
                report.AppendLine($"order {o.ingA}/{o.ingB} min {ro?.minA}/{ro?.minB} -> {o.minA}/{o.minB} : {DrinkSales.Describe(o)}");
            }
            foreach (var r in d.recipes) foreach (var s in r.steps) Assert.AreEqual(0, s.amount % 10, r.name);
            Assert.AreEqual(raw.coins, d.coins, "코인 보존");
            if (raw.schemaVersion < DrinkStore.IntegerUnitsVersion) Assert.AreEqual(before, File.ReadAllText(path + ".pre-integer-v" + raw.schemaVersion + ".bak"), "복사본 백업");
            string outDir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(outDir)) { Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, "migration-user-copy.txt"), report.ToString()); }
            Debug.Log("[IntegerOnly] " + report);
        }

        [Test]
        public void Migration_FutureFile_IsNotTouched()
        {
            File.WriteAllText(path, "{\"schemaVersion\": 99, \"coins\": 5}");
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.WriteBlocked);
            Assert.IsFalse(File.Exists(path + ".pre-integer-v99.bak"));
            Assert.AreEqual("{\"schemaVersion\": 99, \"coins\": 5}", File.ReadAllText(path));
        }

        [Test]
        public void Migration_IsIdempotent_OnWholeData()
        {
            var d = LegacyFractional();
            DrinkStore.MigrateToWholeUnits(d, 2);
            var once = JsonUtility.ToJson(d);
            DrinkStore.MigrateToWholeUnits(d, 3);
            d.integerMigrationUtc = JsonUtility.FromJson<DrinkSaveData>(once).integerMigrationUtc;
            Assert.AreEqual(once, JsonUtility.ToJson(d));
        }

        // ───── 표시 = 적용(음료 버프) ─────

        [Test]
        public void DrinkBuff_DisplayedPercent_EqualsAppliedPercent()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Stir;
            r.SetAmount(DrinkIngredients.Gin, 300);
            r.SetAmount(DrinkIngredients.LemonJuice, 70);
            r.SetAmount(DrinkIngredients.BlueCuracao, 30);
            var buff = DrinkCalculator.ScalePositive(DrinkCalculator.Buff(r), 1.05f);
            var stats = new PlayerStats();
            DrinkCalculator.ApplyTo(stats, buff);
            Assert.AreEqual(DrinkBuff.Percent(buff[DrinkStat.Attack]) / 100f, stats.damagePercent, 1e-6f);
            Assert.AreEqual(DrinkBuff.Percent(buff[DrinkStat.AttackSpeed]) / 100f, stats.attackSpeedPercent, 1e-6f);
            Assert.AreEqual(DrinkBuff.Percent(buff[DrinkStat.CritChance]) / 100f, stats.drinkCritChance, 1e-6f);
            Assert.AreEqual(Mathf.Round(stats.maxHpFlat), stats.maxHpFlat, "최대 체력 증가량은 정수");
            string text = buff.Describe();
            Assert.IsFalse(Decimal.IsMatch(text), text);
            foreach (var s in buff.Shown()) StringAssert.Contains(DrinkBuff.Percent(buff[s]) + "%", text);
        }

        [Test]
        public void DrinkBuff_TinyEffect_RoundsToZero_IsHiddenAndNotApplied()
        {
            var b = DrinkCalculator.ScalePositive(new DrinkBuff(), 1f);
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.SetAmount(DrinkIngredients.Gin, 10); // 1mL → 공격력 기여가 0.5% 미만
            b = DrinkCalculator.Buff(r);
            Assume.That(b[DrinkStat.Attack] > 0f && DrinkBuff.Percent(b[DrinkStat.Attack]) == 0, "이 배합의 공격력은 0~0.5% 사이여야 이 경계를 검사한다");
            var stats = new PlayerStats();
            DrinkCalculator.ApplyTo(stats, b);
            Assert.AreEqual(0f, stats.damagePercent);
            Assert.AreEqual("효과 없음", b.Describe());
        }

        // ───── 전투 피해 경계 ─────

        [Test]
        public void Enemy_TakesIntegerDamage_AndHasIntegerMaxHp()
        {
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Brute, 1.37f, null, null);
            try
            {
                Assert.AreEqual(Mathf.Round(enemy.MaxHp), enemy.MaxHp, "최대 체력 정수");
                float received = -1f;
                enemy.Damaged += a => received = a;
                float hp = enemy.Hp;
                enemy.TakeDamage(10.5f);
                Assert.AreEqual(11f, received);
                Assert.AreEqual(hp - 11f, enemy.Hp);
                enemy.TakeDamage(0.2f);
                Assert.AreEqual(1f, received, "양수 피해는 최소 1");
                Assert.AreEqual(hp - 12f, enemy.Hp);
                enemy.TakeDamage(2.4f);
                Assert.AreEqual(hp - 14f, enemy.Hp);
                Assert.AreEqual(Mathf.Round(enemy.Hp), enemy.Hp, "HP는 항상 정수");
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }

        [Test]
        public void Player_TakesIntegerDamage_AfterAllMultipliers()
        {
            var go = new GameObject("IntPlayer");
            try
            {
                var stats = new PlayerStats();
                var player = go.AddComponent<PlayerController>();
                player.Init(stats, Color.white, "bartender");
                Assert.AreEqual(100f, player.Hp);
                player.TakeZoneDamage(10.5f);
                Assert.AreEqual(89f, player.Hp, "10.5 → 11");
                stats.damageReduction = 0.25f;
                player.TakeZoneDamage(10f); // 7.5 → 8
                Assert.AreEqual(81f, player.Hp);
                player.TakeZoneDamage(1f); // 0.75 → 1
                Assert.AreEqual(80f, player.Hp);
                player.TakeZoneDamage(0f);
                Assert.AreEqual(80f, player.Hp, "0 피해는 0");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ───── 화면 문구 감사(소수 없음) ─────

        static IEnumerable<string> UserTexts()
        {
            foreach (var c in CardDatabase.LoadAll()) { yield return c.displayName; yield return c.description; }
            foreach (var c in DefaultCards.CreateRuntime()) yield return c.description;
            foreach (var c in DefaultTeaCards.CreateRuntime()) yield return c.description;
            foreach (var c in DefaultHybridCards.CreateRuntime()) yield return c.description;
            foreach (var s in SkillDatabase.LoadAll()) yield return s.description;
            foreach (var s in DefaultSkills.CreateRuntime()) yield return s.description;
            foreach (var s in PermanentSkillDatabase.LoadAll()) yield return s.description;
            foreach (var s in DefaultPermanentSkills.CreateRuntime()) yield return s.description;
            foreach (var h in HiddenSkillDatabase.All) { yield return h.effect; yield return h.condition; yield return h.hint; yield return h.teaCondition; yield return h.teaHint; }
            yield return JobPreset.Bartender.description;
            yield return JobPreset.TeaSommelier.description;
            foreach (var w in new[] { WeaponPreset.Rapier, WeaponPreset.Gun, WeaponPreset.Sword, WeaponPreset.Bow }) yield return w.description;
            foreach (DrinkTechnique t in Enum.GetValues(typeof(DrinkTechnique))) { yield return DrinkRules.TechniqueRule(t); yield return DrinkRules.TechniqueShort(t); }
            foreach (var t in DrinkResearch.Traits) yield return t.effect;
            foreach (var s in DrinkResearch.Staff) { yield return s.effect; yield return s.research; }
            foreach (CoffeeBrewMethod m in CoffeeBrewing.All) yield return CoffeeBrewing.ShortDescription(m);
            foreach (var row in DrinkSupply.Table) yield return DrinkLab.SourceText(row.id);
            yield return DrinkLab.SupplyRuleText();
        }

        [Test]
        public void UserFacingDataTexts_HaveNoDecimalNumbers()
        {
            var bad = new List<string>();
            foreach (var t in UserTexts()) if (!string.IsNullOrEmpty(t) && Decimal.IsMatch(t)) bad.Add(t);
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void HiddenSkillRegenAndRadiusTexts_UseIntegerUnits()
        {
            var cham = CardDatabase.LoadAll().Find(c => c.id == "chamomile");
            Assert.IsNotNull(cham);
            Assert.AreEqual("분당 회복량 +72", cham.description, "Resources 에셋과 코드 기본값이 같다");
            Assert.AreEqual("분당 회복량 +72", DefaultTeaCards.CreateRuntime().Find(c => c.id == "chamomile").description);
        }
    }
}
