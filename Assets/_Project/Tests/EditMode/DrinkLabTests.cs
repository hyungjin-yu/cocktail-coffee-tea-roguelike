using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>69 음료 제조 v1.1 — 입력·계산·색·검증·공유 코드·저장 트랜잭션·종료 정산·강제 종료 창·손상 복구(EditMode, 격리 경로).</summary>
    public class DrinkLabTests
    {
        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "drinktest_" + Guid.NewGuid().ToString("N"));
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

        static DrinkRecipe Cocktail(DrinkTechnique t, params (string id, int amt)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)t;
            r.name = "테스트";
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.amt));
            return r;
        }

        static DrinkRecipe Tea(params (string id, int amt)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Tea);
            r.name = "차";
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.amt));
            return r;
        }

        static void FailSaves() => DrinkStore.FaultHookForTests = stage => { if (stage == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입된 디스크 실패"); };

        // ───── 입력(0.1 단위) ─────

        [TestCase("12.3", 123)] [TestCase("0.1", 1)] [TestCase("1.20", 12)] [TestCase(" 5 ", 50)] [TestCase("5ml", 50)]
        [TestCase("2.5g", 25)] [TestCase("1,5", 15)] [TestCase(".5", 5)] [TestCase("7.", 70)] [TestCase("0", 0)]
        public void ParseAmount_AcceptsTenths(string text, int expected)
        {
            Assert.IsTrue(DrinkRules.TryParseAmount(text, out int v, out var e), e);
            Assert.AreEqual(expected, v);
        }

        [TestCase("-1")] [TestCase("NaN")] [TestCase("1e3")] [TestCase("1.25")] [TestCase("")] [TestCase("abc")]
        [TestCase("1..2")] [TestCase("99999999")] [TestCase("Infinity")] [TestCase(null)] [TestCase("1.2.3")] [TestCase("0.05")]
        public void ParseAmount_RejectsInvalid(string text)
        {
            Assert.IsFalse(DrinkRules.TryParseAmount(text, out int v, out var e));
            Assert.IsNotEmpty(e);
            Assert.AreEqual(0, v);
        }

        [Test]
        public void Format_IsExactWithoutFloat()
        {
            Assert.AreEqual("12.3", DrinkRules.Format(123));
            Assert.AreEqual("0.1", DrinkRules.Format(1));
            Assert.AreEqual("120.0mL", DrinkRules.Format(1200, IngredientForm.Liquid));
            Assert.AreEqual("4.9g", DrinkRules.Format(49, IngredientForm.Leaf));
        }

        // ───── 버프 ─────

        [Test]
        public void Buff_UsesNormalizedFormulaAndCoefficient()
        {
            var gin = DrinkIngredients.Get("gin");
            var r = Cocktail(DrinkTechnique.Build, ("gin", 150)); // q = k = 15mL → 0.5
            float expected = 0.5f * gin.coefficient * DrinkRules.Cap(DrinkStat.Attack);
            Assert.AreEqual(expected, DrinkCalculator.Buff(r)[DrinkStat.Attack], 1e-6f);
            var tea = Tea(("black_tea", 20)); // 2g = k → 0.5
            Assert.AreEqual(0.5f * 0.8f * 0.05f, DrinkCalculator.Buff(tea)[DrinkStat.CooldownReduction], 1e-6f);
        }

        [Test]
        public void Buff_SingleIngredientNeverReachesCap_TwoSameEffectCan()
        {
            var huge = Cocktail(DrinkTechnique.Build, ("gin", 1200));
            Assert.Less(DrinkCalculator.Buff(huge)[DrinkStat.Attack], DrinkRules.Cap(DrinkStat.Attack));
            var both = Cocktail(DrinkTechnique.Stir, ("gin", 900), ("lemon_juice", 300));
            Assert.AreEqual(DrinkRules.Cap(DrinkStat.Attack), DrinkCalculator.Buff(both)[DrinkStat.Attack], 1e-6f, "합산 후 상한 제한");
        }

        [Test]
        public void Buff_AmountChangeMonotonicAndTenthMatters()
        {
            float a = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Build, ("rum", 300))).Describe().Length;
            var r1 = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Build, ("rum", 300)))[DrinkStat.AttackSpeed];
            var r2 = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Build, ("rum", 301)))[DrinkStat.AttackSpeed];
            Assert.Greater(r2, r1, "0.1mL 차이도 계산에 반영");
            Assert.Greater(a, 0);
        }

        [Test]
        public void Buff_TechniqueMultipliers()
        {
            var steps = new[] { ("gin", 300), ("blue_curacao", 150) };
            var build = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Build, steps));
            var shake = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Shake, steps));
            var stir = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Stir, steps));
            var layer = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Layer, steps));
            Assert.AreEqual(build[DrinkStat.CritChance] * 1.15f, shake[DrinkStat.CritChance], 1e-6f);
            Assert.AreEqual(build[DrinkStat.Attack] * 0.95f, shake[DrinkStat.Attack], 1e-6f);
            Assert.AreEqual(build[DrinkStat.Attack] * 1.08f, stir[DrinkStat.Attack], 1e-6f);
            Assert.AreEqual(build[DrinkStat.CritChance] * 0.95f, stir[DrinkStat.CritChance], 1e-6f);
            Assert.AreEqual(build[DrinkStat.Attack] * 1.05f, layer[DrinkStat.Attack], 1e-6f, "효과 2종 이상 레이어링 ×1.05");
            var single = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Layer, ("gin", 300), ("lemon_juice", 100)));
            var singleBuild = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Build, ("gin", 300), ("lemon_juice", 100)));
            Assert.AreEqual(singleBuild[DrinkStat.Attack] * 0.90f, single[DrinkStat.Attack], 1e-6f, "단일 효과 레이어링 ×0.90");
            Assert.AreEqual(0f, shake[DrinkStat.MoveSpeed], "기법으로 없는 효과가 생기지 않는다");
        }

        [Test]
        public void Buff_CapsHoldAfterTechnique()
        {
            var r = Cocktail(DrinkTechnique.Shake, ("gin", 400), ("blue_curacao", 500), ("lemon_juice", 300));
            var b = DrinkCalculator.Buff(r);
            foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat))) Assert.LessOrEqual(b[s], DrinkRules.Cap(s) + 1e-6f, s.ToString());
            Assert.Less(b[DrinkStat.CritChance], DrinkRules.Cap(DrinkStat.CritChance), "치명타 재료는 1종 — 셰이킹 ×1.15여도 계수 0.8이라 상한 미만");
        }

        [Test]
        public void Buff_WaterAndIceContributeNothing()
        {
            var a = Cocktail(DrinkTechnique.Build, ("gin", 300));
            var b = a.Clone(); b.water = 300; b.ice = 4;
            Assert.AreEqual(DrinkCalculator.Buff(a).Describe(), DrinkCalculator.Buff(b).Describe());
        }

        [Test]
        public void ApplyTo_RespectsExistingFinalClamps()
        {
            var stats = new PlayerStats();
            var buff = DrinkCalculator.Buff(Cocktail(DrinkTechnique.Stir, ("gin", 900), ("lemon_juice", 300), ("rum", 0)));
            float hp = stats.MaxHp;
            DrinkCalculator.ApplyTo(stats, buff);
            Assert.AreEqual(stats.baseDamage * (1f + buff[DrinkStat.Attack]), stats.Damage, 1e-4f);
            Assert.AreEqual(hp, stats.MaxHp, 1e-4f, "체력 재료 없음");
            stats.attackSpeedPercent = 50f; stats.drinkCooldownReduction = 1f;
            Assert.AreEqual(0.12f, stats.AttackInterval, 1e-5f, "기존 최소 공격 간격 유지");
            Assert.AreEqual(0.95f, stats.CooldownScale, 1e-6f, "재사용 감소는 상한 5%로 제한");
        }

        // ───── 색 ─────

        [Test]
        public void Color_DeterministicSameMixSameColor()
        {
            var r = Cocktail(DrinkTechnique.Shake, ("gin", 450), ("blue_curacao", 75), ("lemon_juice", 150));
            Assert.AreEqual(DrinkCalculator.Hex(DrinkCalculator.Look(r).color), DrinkCalculator.Hex(DrinkCalculator.Look(r.Clone()).color));
        }

        [Test]
        public void Color_ClearGinTransparent_StrongColorantsDominate_LemonCloudy()
        {
            var gin = DrinkCalculator.Look(Cocktail(DrinkTechnique.Build, ("gin", 600)));
            Assert.Less(gin.color.a, 0.45f, "진은 투명");
            Assert.Greater(gin.color.r + gin.color.g + gin.color.b, 2.7f, "무색");
            var blue = DrinkCalculator.Look(Cocktail(DrinkTechnique.Build, ("gin", 900), ("blue_curacao", 50)));
            Assert.Greater(blue.color.b, blue.color.r + 0.2f, "소량 큐라소 5mL가 90mL 진을 파랗게");
            var red = DrinkCalculator.Look(Cocktail(DrinkTechnique.Build, ("gin", 900), ("grenadine", 50)));
            Assert.Greater(red.color.r, red.color.b + 0.2f, "소량 그레나딘이 붉게");
            var lemon = DrinkCalculator.Look(Cocktail(DrinkTechnique.Build, ("gin", 300), ("lemon_juice", 300)));
            Assert.Greater(lemon.turbidity, gin.turbidity + 0.2f, "레몬즙 탁도");
            Assert.Greater(lemon.color.a, gin.color.a, "탁하면 덜 투명");
        }

        [Test]
        public void Color_WaterDilutes_NotSrgbAverage()
        {
            var strong = DrinkCalculator.Look(Cocktail(DrinkTechnique.Build, ("gin", 300), ("grenadine", 100)));
            var diluted = Cocktail(DrinkTechnique.Build, ("gin", 300), ("grenadine", 100));
            diluted.water = 300;
            var d = DrinkCalculator.Look(diluted);
            Assert.Greater(d.color.g, strong.color.g, "물이 색을 옅게");
            // 단순 sRGB 평균이면 진(거의 흰색) 3 : 그레나딘 1에서 g ≈ 0.75. Beer–Lambert는 강한 색소가 이긴다.
            Assert.Less(strong.color.g, 0.5f);
        }

        [Test]
        public void Layering_DensityOrderAndBoundaries()
        {
            var r = Cocktail(DrinkTechnique.Layer, ("gin", 300), ("grenadine", 150), ("blue_curacao", 150));
            var look = DrinkCalculator.Look(r);
            Assert.AreEqual(3, look.layers.Count);
            Assert.AreEqual(2, look.boundaries);
            Assert.That(look.layers[0].label, Does.Contain("그레나딘"), "가장 무거운 그레나딘이 바닥(투입 순서가 2번째여도)");
            Assert.That(look.layers[2].label, Does.Contain("진"), "가벼운 진이 맨 위");
            var mixed = r.Clone(); mixed.technique = (int)DrinkTechnique.Stir;
            Assert.AreEqual(1, DrinkCalculator.Look(mixed).layers.Count, "스터링은 한 덩어리");
        }

        [Test]
        public void TeaColor_BrewDarkensWithLeaves()
        {
            var light = DrinkCalculator.Look(Tea(("black_tea", 10)));
            var strong = DrinkCalculator.Look(Tea(("black_tea", 50)));
            Assert.Less(strong.color.g, light.color.g);
            Assert.AreEqual(150f, light.totalVolume, 0.01f);
        }

        // ───── 검증 ─────

        [Test]
        public void Validator_RulesAndHostileInput()
        {
            Assert.IsTrue(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", 300))).Ok);
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("lemon_juice", 300))).Ok, "베이스 술 필요");
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", 300), ("black_tea", 10))).Ok, "칵테일에 찻잎 불가");
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Steep, ("gin", 300))).Ok, "칵테일 우림 불가");
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", 1201))).Ok, "120mL 초과");
            Assert.IsTrue(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", 1200))).Ok, "경계 120.0mL 허용");
            var water = Cocktail(DrinkTechnique.Shake, ("gin", 1000)); water.water = 201;
            Assert.IsFalse(DrinkValidator.Check(water).Ok, "물 포함 합 한도");
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", 100), ("gin", 100))).Ok, "중복");
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", -5))).Ok, "음수");
            Assert.IsFalse(DrinkValidator.Check(Cocktail(DrinkTechnique.Shake, ("gin", 300), ("mint", 10))).Ok, "알 수 없는 재료");
            var overflow = Cocktail(DrinkTechnique.Shake, ("gin", int.MaxValue), ("rum", int.MaxValue));
            Assert.IsFalse(DrinkValidator.Check(overflow).Ok, "int 넘침으로 한도 우회 불가");
            Assert.AreEqual(int.MaxValue, overflow.LiquidTotal(), "합은 long으로 잘라 음수가 되지 않는다");
            var nullStep = Cocktail(DrinkTechnique.Shake, ("gin", 300)); nullStep.steps.Add(null);
            Assert.DoesNotThrow(() => DrinkValidator.Check(nullStep));
            Assert.IsFalse(DrinkValidator.Check(nullStep).Ok);
            var nullList = Cocktail(DrinkTechnique.Shake); nullList.steps = null;
            Assert.DoesNotThrow(() => { DrinkValidator.Check(nullList); DrinkCalculator.Buff(nullList); DrinkCalculator.Look(nullList); nullList.Clone(); });
            var many = Cocktail(DrinkTechnique.Shake); for (int i = 0; i < 50; i++) many.steps.Add(new DrinkStep("gin", 1));
            Assert.IsFalse(DrinkValidator.Check(many).Ok, "재료 수 상한");
            Assert.IsTrue(DrinkValidator.Check(Tea(("green_tea", 1))).Ok, "찻잎 0.1g 최소");
            Assert.IsTrue(DrinkValidator.Check(Tea(("green_tea", 50))).Ok, "5.0g 경계");
            Assert.IsFalse(DrinkValidator.Check(Tea(("green_tea", 51))).Ok, "5.1g 초과");
            Assert.IsFalse(DrinkValidator.Check(Tea(("honey", 100))).Ok, "찻잎 없는 차");
            Assert.IsFalse(DrinkValidator.Check(Tea(("green_tea", 10), ("honey", 301))).Ok, "첨가 액체 30mL 초과");
            Assert.IsFalse(DrinkValidator.Check(Tea(("green_tea", 10), ("rum", 10))).Ok, "차에 럼 불가");
            var teaWater = Tea(("green_tea", 10)); teaWater.water = 1000;
            Assert.IsFalse(DrinkValidator.Check(teaWater).Ok, "차 물은 150mL 고정");
        }

        // ───── 공유 코드 ─────

        [Test]
        public void ShareCode_RoundTripReproducesMixColorAndBuff()
        {
            var r = Cocktail(DrinkTechnique.Layer, ("grenadine", 150), ("blue_curacao", 123), ("gin", 301));
            r.ice = 2; r.water = 55; r.name = "노을 칵테일";
            var code = DrinkShareCode.Encode(r);
            StringAssert.StartsWith("CCT1-", code);
            Assert.LessOrEqual(code.Length, DrinkShareCode.MaxCodeLength);
            var back = DrinkShareCode.Decode(code, out var err);
            Assert.IsNotNull(back, err);
            Assert.IsTrue(r.SameMix(back));
            Assert.AreEqual("노을 칵테일", back.name);
            Assert.AreEqual(DrinkCalculator.Hex(DrinkCalculator.Look(r).color), DrinkCalculator.Hex(DrinkCalculator.Look(back).color));
            Assert.AreEqual(DrinkCalculator.Buff(r).Describe(), DrinkCalculator.Buff(back).Describe());
            Assert.IsNotNull(DrinkShareCode.Decode("  " + code.ToLowerInvariant().Replace("-", " - ") + "\n", out err), "공백·소문자·구분자 허용: " + err);
            var tea = Tea(("green_tea", 33), ("honey", 120));
            Assert.IsTrue(tea.SameMix(DrinkShareCode.Decode(DrinkShareCode.Encode(tea), out _)));
        }

        [Test]
        public void ShareCode_MaxKoreanNameAndAllCocktailIngredients_RoundTripWithinLimit()
        {
            var r = Cocktail(DrinkTechnique.Layer, ("gin", 300), ("rum", 200), ("blue_curacao", 150), ("grenadine", 150), ("lemon_juice", 200), ("honey", 199));
            r.ice = DrinkRules.IceMax; r.water = 0;
            r.name = new string('가', DrinkRules.RecipeNameMax); // 16자 = 48바이트
            Assert.IsTrue(DrinkValidator.Check(r).Ok, DrinkValidator.Check(r).First);
            var code = DrinkShareCode.Encode(r);
            Assert.LessOrEqual(code.Length, DrinkShareCode.MaxCodeLength, code);
            var back = DrinkShareCode.Decode(code, out var err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(r.name, back.name, "최대 길이 한글 이름 보존");
            Assert.IsTrue(r.SameMix(back));
            // 형식상 최대(재료 8줄)도 선언한 최대 길이 안
            var eight = Cocktail(DrinkTechnique.Shake);
            foreach (var id in DrinkIngredients.ShareIndex) eight.steps.Add(new DrinkStep(id, 65535));
            eight.name = new string('힣', DrinkRules.RecipeNameMax);
            Assert.LessOrEqual(DrinkShareCode.Encode(eight).Length, DrinkShareCode.MaxCodeLength);
            var emoji = Cocktail(DrinkTechnique.Shake, ("gin", 100)); emoji.name = "🍸🍸🍸🍸🍸🍸🍸🍸";
            var e2 = DrinkShareCode.Decode(DrinkShareCode.Encode(emoji), out err);
            Assert.IsNotNull(e2, err);
            Assert.AreEqual(emoji.name, e2.name, "서로게이트 쌍 이름 보존");
        }

        [Test]
        public void ShareCode_RejectsInvalid()
        {
            var good = DrinkShareCode.Encode(Cocktail(DrinkTechnique.Shake, ("gin", 300), ("lemon_juice", 150)));
            string err;
            Assert.IsNull(DrinkShareCode.Decode("", out err)); Assert.IsNotEmpty(err);
            Assert.IsNull(DrinkShareCode.Decode("hello", out err)); StringAssert.Contains("CCT", err);
            Assert.IsNull(DrinkShareCode.Decode("CCT2-" + good.Substring(5), out err)); StringAssert.Contains("버전", err);
            Assert.IsNull(DrinkShareCode.Decode(good.Substring(0, good.Length - 3), out err), "잘린 코드");
            int at = 9; // 패딩 비트가 아닌 데이터 문자
            var flipped = good.Substring(0, at) + (good[at] == 'A' ? 'B' : 'A') + good.Substring(at + 1);
            Assert.IsNull(DrinkShareCode.Decode(flipped, out err), "검사값 불일치");
            Assert.IsNull(DrinkShareCode.Decode(good + "; rm -rf C:/", out err)); StringAssert.Contains("문자", err);
            Assert.IsNull(DrinkShareCode.Decode("CCT1-" + new string('A', 400), out err)); StringAssert.Contains("너무", err);
            Assert.IsNull(DrinkShareCode.Decode("CCT1-" + new string('A', 200), out err)); StringAssert.Contains("너무", err);
            // 끝 문자 패딩 비트만 바꾼 비정규 코드도 거부(데이터 바이트는 같아도).
            string body = good.Replace("-", "").Substring(4);
            char lastCh = body[body.Length - 1];
            const string alpha = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            int v = alpha.IndexOf(lastCh);
            var padFlip = "CCT1-" + body.Substring(0, body.Length - 1) + alpha[v ^ 1];
            Assert.IsNull(DrinkShareCode.Decode(padFlip, out err), "패딩 비트 변조 거부: " + padFlip);
            Assert.IsNull(DrinkShareCode.Decode(good + "0", out err), "여분 문자 거부");
            Assert.IsNull(DrinkShareCode.Decode(DrinkShareCode.Encode(Cocktail(DrinkTechnique.Steep, ("gin", 300))), out err), "허용되지 않는 기법");
            Assert.IsNull(DrinkShareCode.Decode(DrinkShareCode.Encode(Cocktail(DrinkTechnique.Shake, ("gin", 300), ("mint", 10))), out err)); StringAssert.Contains("알 수 없는 재료", err);
            Assert.IsNull(DrinkShareCode.Decode(DrinkShareCode.Encode(Cocktail(DrinkTechnique.Shake, ("gin", 1300))), out err), "초과량");
            Assert.IsNull(DrinkShareCode.Decode(DrinkShareCode.Encode(Cocktail(DrinkTechnique.Shake, ("gin", 300), ("gin", 100))), out err), "중복");
            Assert.IsNull(DrinkShareCode.Decode(DrinkShareCode.Encode(Cocktail(DrinkTechnique.Shake, ("gin", 0))), out err), "0/음수(0으로 잘림) 거부");
        }

        // ───── 저장소: 첫 세트·제조 트랜잭션 ─────

        [Test]
        public void FreshSave_GrantsStarterOnce_AndPersists()
        {
            Assert.AreEqual(600, DrinkLab.Owned("gin"));
            StringAssert.Contains("첫 제조용", DrinkLab.Notice);
            Assert.IsTrue(File.Exists(path));
            DrinkLab.ReloadForTests();
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "재시작해도 다시 지급하지 않는다");
            Assert.AreEqual(300, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "커피 원두 중복 지급 없음");
            Assert.AreEqual(150, DrinkLab.Owned(DrinkIngredients.SteamedMilk), "우유 중복 지급 없음");
            Assert.IsNull(DrinkLab.Notice);
        }

        [Test]
        public void Craft_DeductsExactlyAndRecordsPreparedInSameSave()
        {
            var r = Cocktail(DrinkTechnique.Shake, ("gin", 453), ("lemon_juice", 101));
            var res = DrinkLab.Craft(r);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, res.Status, res.Message);
            Assert.AreEqual(600 - 453, DrinkLab.Owned("gin"));
            Assert.AreEqual(300 - 101, DrinkLab.Owned("lemon_juice"));
            DrinkLab.ReloadForTests(); // 제조 후 재시작
            Assert.AreEqual(147, DrinkLab.Owned("gin"));
            Assert.IsNotNull(DrinkLab.Prepared);
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "출발 전 음료는 재시작해도 남는다");
            Assert.IsTrue(r.SameMix(DrinkLab.Prepared.recipe));
            Assert.AreEqual(DrinkLab.CraftStatus.AlreadyPrepared, DrinkLab.Craft(r).Status, "한 슬롯 — 덮어쓰지 않음");
            Assert.AreEqual(147, DrinkLab.Owned("gin"));
        }

        [Test]
        public void Craft_RejectsShortageAndUnownedAndInvalid_WithoutDeducting()
        {
            Assert.AreEqual(DrinkLab.CraftStatus.Shortage, DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("gin", 601))).Status);
            var data = DrinkLab.Data;
            data.stock.RemoveAll(s => s.id == "rum");
            Assert.AreEqual(DrinkLab.CraftStatus.Shortage, DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("rum", 10))).Status, "없는 재료 제조 거부");
            Assert.AreEqual(DrinkLab.CraftStatus.Invalid, DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("lemon_juice", 10))).Status);
            Assert.AreEqual(600, DrinkLab.Owned("gin"));
            Assert.IsNull(DrinkLab.Prepared);
        }

        [Test]
        public void Craft_SaveFailure_RollsBackEverything()
        {
            FailSaves();
            var res = DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("gin", 300)));
            Assert.AreEqual(DrinkLab.CraftStatus.SaveFailed, res.Status);
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "메모리 재고 미차감");
            Assert.IsNull(DrinkLab.Prepared);
            DrinkStore.FaultHookForTests = null;
            DrinkLab.ReloadForTests();
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "디스크도 그대로");
            Assert.IsNull(DrinkLab.Prepared);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("gin", 300))).Status, "복구 후 재시도");
        }

        [Test]
        public void Recipes_SaveEditDuplicateDeleteLimit_FreeOfStock()
        {
            var r = Cocktail(DrinkTechnique.Stir, ("gin", 300), ("rum", 200));
            Assert.IsTrue(DrinkLab.SaveRecipe(r, -1, out var e), e);
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "레시피 저장은 무료");
            r.steps[0].amount = 250;
            Assert.IsTrue(DrinkLab.SaveRecipe(r, 0, out e), e);
            Assert.AreEqual(250, DrinkLab.Recipes[0].steps[0].amount);
            Assert.IsTrue(DrinkLab.DuplicateRecipe(0, out e), e);
            Assert.AreEqual(2, DrinkLab.Recipes.Count);
            Assert.IsTrue(DrinkLab.DeleteRecipe(0, out e), e);
            Assert.AreEqual(1, DrinkLab.Recipes.Count);
            for (int i = DrinkLab.Recipes.Count; i < DrinkRules.RecipeLimit; i++) Assert.IsTrue(DrinkLab.SaveRecipe(r, -1, out e), e);
            Assert.IsFalse(DrinkLab.SaveRecipe(r, -1, out e), "30개 한도");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(DrinkRules.RecipeLimit, DrinkLab.Recipes.Count);
            Assert.IsFalse(DrinkLab.SaveRecipe(Cocktail(DrinkTechnique.Shake, ("lemon_juice", 10)), -1, out e), "잘못된 배합 저장 거부");
        }

        [Test]
        public void ImportIntoAnotherSave_ReproducesAndUsesOwnStock()
        {
            var r = Cocktail(DrinkTechnique.Shake, ("gin", 300), ("blue_curacao", 40));
            r.name = "공유";
            var code = DrinkShareCode.Encode(r);
            // 다른 저장(다른 유저)
            var other = Path.Combine(dir, "other.drinks.json");
            DrinkLab.SetPathForTests(other);
            var imported = DrinkShareCode.Decode(code, out var err);
            Assert.IsNotNull(imported, err);
            int before = DrinkLab.Owned("gin");
            Assert.IsTrue(DrinkLab.SaveRecipe(imported, -1, out err), err);
            Assert.AreEqual(before, DrinkLab.Owned("gin"), "가져오기·저장은 창고를 바꾸지 않는다");
            Assert.IsNull(DrinkLab.Prepared, "버프/준비 음료 즉시 변경 없음");
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(DrinkLab.Recipes[0]).Status);
            Assert.AreEqual(before - 300, DrinkLab.Owned("gin"), "다른 유저도 자기 재료를 소비");
            Assert.AreEqual(DrinkCalculator.Hex(DrinkCalculator.Look(r).color), DrinkCalculator.Hex(DrinkCalculator.Look(DrinkLab.Prepared.recipe).color));
        }

        // ───── 런 수명주기 ─────

        void Prepare(DrinkRecipe r = null)
        {
            var res = DrinkLab.Craft(r ?? Cocktail(DrinkTechnique.Shake, ("gin", 300), ("blue_curacao", 50)));
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, res.Status, res.Message);
        }

        DrinkBuff Start(string runId)
        {
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            return DrinkLab.BeginRun(runId);
        }

        [TestCase(0)] [TestCase(4)]
        public void ExplicitAbandon_0to4Kills_PreservesDrink_NoMaterials(int kills)
        {
            Prepare();
            Assert.IsNotNull(Start("runA"));
            for (int k = 1; k <= kills; k++) DrinkLab.OnKill("runA", k);
            DrinkLab.RecordBossAward("runA", "boss1_Mid", "테스트", BossItems());
            int gin = DrinkLab.Owned("gin");
            var s = DrinkLab.EndRun("runA", RunEndKind.Abandoned, kills, explicitAbandon: true);
            Assert.AreEqual("Preserved", s.drink);
            Assert.AreEqual("PreservedAbandon", s.kind);
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state);
            Assert.AreEqual(gin, DrinkLab.Owned("gin"), "보존 포기는 재료 보상 미정산");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "보존 상태 저장");
            // 보존 재입장: 재제조/재소모 없이 같은 음료
            int ginNow = DrinkLab.Owned("gin");
            var buff = Start("runB");
            Assert.IsNotNull(buff);
            Assert.AreEqual(ginNow, DrinkLab.Owned("gin"));
            Assert.AreEqual("runB", DrinkLab.Prepared.runId);
        }

        [Test]
        public void ExplicitAbandon_5Kills_ConsumesDrink()
        {
            Prepare();
            Start("runA");
            for (int k = 1; k <= 5; k++) DrinkLab.OnKill("runA", k);
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.Data.prepared.locked, "5킬 경계는 즉시 저장(재시작 전)");
            var s = DrinkLab.EndRun("runA", RunEndKind.Abandoned, 5, true);
            Assert.AreEqual("Consumed", s.drink);
            Assert.IsNull(DrinkLab.Prepared);
        }

        [Test]
        public void EarlyDeath_ConsumesDrink_AndAppQuitIsNotExplicit()
        {
            Prepare();
            Start("runA");
            DrinkLab.OnKill("runA", 2);
            Assert.AreEqual("Consumed", DrinkLab.EndRun("runA", RunEndKind.Died, 2, false).drink, "0~4킬 사망은 보존 대상 아님");
            Prepare(Cocktail(DrinkTechnique.Build, ("rum", 100)));
            Start("runB");
            Assert.AreEqual("Consumed", DrinkLab.EndRun("runB", RunEndKind.Abandoned, 0, false).drink, "정상 앱 종료는 명시적 포기 아님");
        }

        [Test]
        public void MidBossThenDeath_GrantsMaterialsOnce_EvenWithRepeatedEnds()
        {
            Start0("runA");
            var items = BossItems();
            Assert.IsTrue(DrinkLab.RecordBossAward("runA", "boss1_Mid", "골렘", items));
            Assert.IsFalse(DrinkLab.RecordBossAward("runA", "boss1_Mid", "골렘", items), "같은 보스 보상 중복 없음");
            DrinkLab.ReloadForTests(); // 보스 보상은 즉시 저장(런 임시 보상)
            Assert.AreEqual(1, DrinkLab.PendingFor("runA").Count);
            int gin = DrinkLab.Owned("gin");
            var s = DrinkLab.EndRun("runA", RunEndKind.Died, 40, false);
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"), "중간 보스 후 사망도 보존");
            Assert.AreEqual(3, s.granted.Count);
            DrinkLab.EndRun("runA", RunEndKind.Died, 40, false);
            DrinkLab.EndRun("runA", RunEndKind.Abandoned, 40, true);
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"), "같은 runId 재정산 없음");
            Assert.IsFalse(DrinkLab.RecordBossAward("runA", "boss2_Mid", "x", items), "정산 끝난 런에는 추가 지급 없음");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"));
        }

        void Start0(string runId) => DrinkLab.BeginRun(runId); // 준비 음료 없이 시작

        /// <summary>기록·정산 시험용 고정 보상(수급 표와 분리): 진 30 + 럼 30 + 레몬 15mL.</summary>
        static List<DrinkAmount> BossItems() => new List<DrinkAmount> { new DrinkAmount("gin", 300), new DrinkAmount("rum", 300), new DrinkAmount("lemon_juice", 150) };

        [Test]
        public void SupplyRoll_FixedBundles_RareGuarantee_Deterministic_HeldIncludesPending()
        {
            Start0("runS");
            var mid = DrinkLab.RollAward(DrinkSupply.Source.MidBoss, "runS", "boss1_Mid");
            int bundles = 0;
            foreach (var a in mid) { var row = DrinkSupply.Get(a.id); Assert.AreEqual(0, a.amount % row.bundle, "묶음 단위"); bundles += a.amount / row.bundle; }
            Assert.AreEqual(3, bundles, "중간 보스 3묶음");
            Assert.IsTrue(mid.Exists(a => DrinkSupply.Get(a.id).rare), "희귀 보장");
            Assert.AreEqual(DrinkLab.Describe(mid), DrinkLab.Describe(DrinkLab.RollAward(DrinkSupply.Source.MidBoss, "runS", "boss1_Mid")), "같은 보상 ID = 같은 결과(재시도 재추첨 없음)");
            int fin = 0;
            foreach (var a in DrinkLab.RollAward(DrinkSupply.Source.FinalBoss, "runS", "boss4_Final")) fin += a.amount / DrinkSupply.Get(a.id).bundle;
            Assert.AreEqual(6, fin);
            int before = DrinkLab.HeldForSupply("runS", "gin");
            Assert.IsTrue(DrinkLab.RecordBossAward("runS", "normal1", "처치 보상", new List<DrinkAmount> { new DrinkAmount("gin", 150) }));
            Assert.AreEqual(before + 150, DrinkLab.HeldForSupply("runS", "gin"), "보유량 보정 = 창고 + 이번 런 미정산");
            Assert.AreEqual(before, DrinkLab.HeldForSupply("otherRun", "gin"));
            DrinkLab.EndRun("runS", RunEndKind.Died, 30, false);
            Assert.AreEqual(0, DrinkLab.RollAward(DrinkSupply.Source.Normal, "runS", "normal2").Count, "정산 끝난 런은 추첨 없음");
        }

        [Test]
        public void SupplyDescriptions_Match78Rules_NoBossOnlyRareWording()
        {
            // 78: 희귀도 일반·정예 후보(보장은 보스), 일반은 판당 60묶음 상한 — 도감 획득처·도움말·재료 데이터 문구가 실제 표와 같아야 한다.
            foreach (var r in DrinkSupply.Table)
            {
                string text = DrinkLab.SourceText(r.id);
                StringAssert.Contains($"일반 {DrinkSupply.NormalKillsPerBundle}킬마다(판당 {DrinkSupply.NormalBundleCap}묶음까지)", text, r.id);
                StringAssert.Contains("정예", text, r.id);
                StringAssert.DoesNotContain("에서만", text, r.id);
                if (r.rare) StringAssert.Contains("일반/정예는 가끔", text, r.id);
                var def = DrinkIngredients.Get(r.id);
                StringAssert.Contains("일반·정예", def.source, r.id);
                StringAssert.DoesNotContain("최종 보스 희귀", def.source, r.id);
            }
            string rule = DrinkLab.SupplyRuleText();
            StringAssert.Contains("판당 최대 60묶음(1500킬)", rule);
            StringAssert.Contains("상한과 별도", rule);
            StringAssert.Contains("일반·정예에서도 가끔", rule);
        }

        [Test]
        public void ResearchPoints_FirstGrantOnce_SettledWithRunOnce_NotOnPreservedAbandon()
        {
            Assert.AreEqual(10, DrinkLab.Research.researchPoints, "새 저장 첫 지급 10");
            StringAssert.Contains("연구 포인트 10", DrinkLab.Notice);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(10, DrinkLab.Research.researchPoints, "재실행으로 다시 받지 않음");
            Start0("runR");
            var s = DrinkLab.EndRun("runR", RunEndKind.Died, 260, false, 250, 1, 0);
            Assert.AreEqual(2 + 2, s.researchPoints);
            DrinkLab.EndRun("runR", RunEndKind.Died, 260, false, 250, 1, 0);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(14, DrinkLab.Research.researchPoints, "같은 런 1회, 저장됨");
            Prepare();
            Start("runP");
            var p = DrinkLab.EndRun("runP", RunEndKind.Abandoned, 3, true, 3, 0, 0);
            Assert.AreEqual("PreservedAbandon", p.kind);
            Assert.AreEqual(0, p.researchPoints, "보존 포기는 보상 없음");
        }

        [Test]
        public void ResearchActions_AreTransactions_PersistOrRollback()
        {
            Assert.IsTrue(DrinkLab.UnlockTrait("finesse_1", out var e), e);
            Assert.IsFalse(DrinkLab.UnlockTrait("finesse_1", out _), "이중 클릭 = 한 번만");
            Assert.AreEqual(6, DrinkLab.Research.researchPoints);
            FailSaves();
            Assert.IsFalse(DrinkLab.EquipTrait("finesse_1", out e));
            StringAssert.Contains("저장", e);
            Assert.AreEqual(0, DrinkLab.Research.equippedTraits.Count, "저장 실패면 장착 안 됨");
            DrinkStore.FaultHookForTests = null;
            Assert.IsTrue(DrinkLab.EquipTrait("finesse_1", out e), e);
            Assert.IsTrue(DrinkLab.HireStaff("staff_tea_aroma", out e), e);
            Assert.IsTrue(DrinkLab.SetSpecialty("staff_tea_aroma", out e), e);
            DrinkLab.ReloadForTests();
            CollectionAssert.AreEqual(new[] { "finesse_1" }, DrinkLab.EquippedTraitsSnapshot());
            Assert.AreEqual(0, DrinkLab.Research.researchPoints);
            Assert.AreEqual("staff_tea_aroma", DrinkLab.Research.activeSpecialty);
        }

        [Test]
        public void Craft_CountsForStaff_FreezesSpecialtyBuff()
        {
            DrinkLab.Data.research.researchPoints = 20;
            Assert.IsTrue(DrinkLab.HireStaff("staff_cocktail_aroma", out var e), e);
            Assert.IsTrue(DrinkLab.SetSpecialty("staff_cocktail_aroma", out e), e);
            var stir = Cocktail(DrinkTechnique.Stir, ("gin", 300));
            var plain = DrinkCalculator.Buff(stir)[DrinkStat.Attack];
            var r = DrinkLab.Craft(stir);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, r.Status, r.Message);
            Assert.AreEqual(Mathf.Min(DrinkRules.Cap(DrinkStat.Attack), plain * 1.08f), r.Buff[DrinkStat.Attack], 1e-6f, "스터링 전문: 공격력 ×1.08");
            Assert.AreEqual("staff_cocktail_aroma", DrinkLab.Prepared.specialtyId);
            Assert.AreEqual(1, DrinkLab.Research.cocktailCrafts);
            Assert.IsTrue(DrinkLab.SetSpecialty("", out e));
            Assert.AreEqual(r.Buff[DrinkStat.Attack], DrinkLab.BeginRun("runZ")[DrinkStat.Attack], 1e-6f, "이미 만든 음료는 전문 교체로 바뀌지 않음");
            var shake = Cocktail(DrinkTechnique.Shake, ("gin", 300));
            Assert.AreEqual(DrinkCalculator.Buff(shake)[DrinkStat.Attack], DrinkCalculator.Buff(shake, "staff_cocktail_aroma")[DrinkStat.Attack], 1e-6f, "기법이 다르면 효과 없음");
            Assert.AreEqual(0f, DrinkCalculator.Buff(stir, "staff_cocktail_blend")[DrinkStat.MaxHp], "없던 효과를 만들지 않음");
        }

        [Test]
        public void SettlementSaveFailure_RetryDoesNotDuplicate()
        {
            Start0("runA");
            DrinkLab.RecordBossAward("runA", "boss1_Mid", "골렘", BossItems());
            int gin = DrinkLab.Owned("gin");
            FailSaves();
            var s = DrinkLab.EndRun("runA", RunEndKind.Died, 30, false);
            Assert.IsTrue(DrinkLab.IsDirty);
            Assert.IsTrue(DrinkLab.LastSaveFailed);
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"), "메모리 확정");
            Assert.IsFalse(DrinkLab.RetrySave(), "디스크 아직 실패");
            DrinkStore.FaultHookForTests = null;
            Assert.IsTrue(DrinkLab.RetrySave());
            Assert.IsTrue(DrinkLab.RetrySave(), "더 저장할 것 없음");
            DrinkLab.EndRun("runA", RunEndKind.Died, 30, false);
            DrinkLab.ReloadForTests();
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"), "재시도로 한 번만 반영");
            Assert.AreEqual(0, DrinkLab.PendingFor("runA").Count);
            Assert.IsNotNull(s);
        }

        [Test]
        public void CrashDuringSettlementSaveFailure_RecoveryConsumesAndGrantsOnce()
        {
            Prepare();
            Start("runA");
            DrinkLab.RecordBossAward("runA", "boss1_Mid", "골렘", BossItems());
            int gin = DrinkLab.Owned("gin");
            FailSaves();
            DrinkLab.EndRun("runA", RunEndKind.Abandoned, 3, true); // 보존 포기였지만 저장 실패
            DrinkStore.FaultHookForTests = null;
            DrinkLab.ReloadForTests(); // 그 상태로 강제 종료 → 다음 실행
            Assert.IsTrue(DrinkLab.RecoverAbandoned());
            Assert.IsNull(DrinkLab.Prepared, "보수적으로 소비(복제 없음)");
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"), "그 런 보스 재료는 한 번 정산");
            StringAssert.Contains("비정상 종료", DrinkLab.Notice);
            Assert.IsFalse(DrinkLab.RecoverAbandoned(), "두 번째 복구 없음");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"));
        }

        [Test]
        public void ForceKillDuringRun_DoesNotDuplicateDrink()
        {
            Prepare();
            Start("runA");
            DrinkLab.OnKill("runA", 1);
            DrinkLab.ReloadForTests(); // 강제 종료(포기 보존으로 취급하지 않음)
            Assert.AreEqual(PreparedDrink.InRun, DrinkLab.Data.prepared.state);
            DrinkLab.RecoverAbandoned();
            Assert.IsNull(DrinkLab.Prepared);
            Assert.IsNull(DrinkLab.BeginRun("runB"), "다음 런에 공짜 버프 없음");
        }

        [Test]
        public void CrashAfterDepartureReservation_BeforeRunStart_ConsumesOnRecovery()
        {
            Prepare();
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out _));
            DrinkLab.ReloadForTests();
            Assert.AreEqual(PreparedDrink.InRun, DrinkLab.Data.prepared.state, "출발 표가 디스크에 먼저 확정");
            DrinkLab.RecoverAbandoned();
            Assert.IsNull(DrinkLab.Prepared);
        }

        [Test]
        public void DepartureSaveFailure_BlocksStart_ThenRetryOrExplicitNoDrink()
        {
            Prepare();
            FailSaves();
            Assert.AreEqual(DrinkLab.DepartStatus.SaveFailed, DrinkLab.ReserveForDeparture(out var e));
            Assert.IsNotEmpty(e);
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "메모리 Ready 유지");
            DrinkStore.FaultHookForTests = null;
            DrinkLab.ReloadForTests();
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "디스크 Ready 유지(무료 버프 없음)");
            // 사용자: 음료 없이 출발
            DrinkLab.DepartWithoutDrink();
            Assert.IsNull(DrinkLab.BeginRun("runA"));
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "명시적 무음료 출발은 음료 보존");
            DrinkLab.EndRun("runA", RunEndKind.Died, 10, false);
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state);
            // 다시 시도 성공
            Assert.IsNotNull(Start("runB"));
            Assert.AreEqual("runB", DrinkLab.Prepared.runId);
        }

        [Test]
        public void BeginRun_DirectPathSaveFailure_GivesNoBuffAndKeepsReady()
        {
            Prepare();
            FailSaves();
            Assert.IsNull(DrinkLab.BeginRun("runA"));
            StringAssert.Contains("마시지 않고", DrinkLab.LastBeginError);
            DrinkStore.FaultHookForTests = null;
            DrinkLab.ReloadForTests();
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state);
        }

        [Test]
        public void SameRunBeginTwice_DoesNotReapply()
        {
            Prepare();
            var b1 = Start("runA");
            var b2 = DrinkLab.BeginRun("runA");
            Assert.AreEqual(b1.Describe(), b2.Describe());
            Assert.AreEqual("runA", DrinkLab.Prepared.runId);
        }

        // ───── 손상·미래 버전·마이그레이션 ─────

        [Test]
        public void CorruptOriginal_RestoresBackup_PreservesCorruptCopy()
        {
            DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("gin", 100)));
            DrinkLab.SaveRecipe(Cocktail(DrinkTechnique.Shake, ("gin", 100)), -1, out _); // .bak = 제조 직후 상태
            File.WriteAllText(path, "{\"schemaVersion\":1, \"stock\": [ {\"id\":");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(DrinkStore.LoadSource.Backup, DrinkLab.LoadSource);
            Assert.AreEqual(500, DrinkLab.Owned("gin"));
            StringAssert.Contains("백업", DrinkLab.Notice);
            Assert.IsNotEmpty(Directory.GetFiles(dir, "drinks.json.*.corrupt"));
        }

        [Test]
        public void CorruptBoth_FreshStartAndKeepsCopies()
        {
            DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("gin", 100)));
            DrinkLab.SaveRecipe(Cocktail(DrinkTechnique.Shake, ("gin", 100)), -1, out _);
            File.WriteAllText(path, "garbage");
            File.WriteAllText(path + ".bak", "");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(DrinkStore.LoadSource.FreshAfterCorrupt, DrinkLab.LoadSource);
            StringAssert.Contains("보관", DrinkLab.Notice);
            Assert.GreaterOrEqual(Directory.GetFiles(dir, "*.corrupt").Length, 2, "원본·백업 손상본 모두 보관");
        }

        [Test]
        public void FutureVersion_IsNeverOverwritten_NoStarterNoCraft()
        {
            File.WriteAllText(path, "{\"schemaVersion\": 9, \"stock\": []}");
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.WriteBlocked);
            Assert.AreEqual(0, DrinkLab.Owned("gin"), "보호 중에는 첫 세트도 주지 않는다");
            Assert.AreEqual(DrinkLab.DepartStatus.NoDrink, DrinkLab.ReserveForDeparture(out _));
            Assert.AreNotEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(Cocktail(DrinkTechnique.Shake, ("gin", 10))).Status);
            Assert.AreEqual("{\"schemaVersion\": 9, \"stock\": []}", File.ReadAllText(path));
        }

        [Test]
        public void NestedCorruptData_IsRepairedWithoutThrowing()
        {
            File.WriteAllText(path, "{\"schemaVersion\":1,\"starterGranted\":true,\"stock\":[{\"id\":\"gin\",\"amount\":-50},{\"id\":\"gin\",\"amount\":2147483647},{\"id\":\"gin\",\"amount\":10},{\"id\":\"mint\",\"amount\":5},{\"id\":\"rum\",\"amount\":30}]," +
                "\"recipes\":[{\"schemaVersion\":1,\"name\":\"x\",\"kind\":1,\"technique\":1,\"steps\":[{\"id\":\"gin\",\"amount\":10}]}]," +
                "\"prepared\":{\"state\":\"Ready\",\"recipe\":{\"schemaVersion\":1,\"kind\":1,\"technique\":1,\"steps\":[{\"id\":\"gin\",\"amount\":999999}]}}," +
                "\"pending\":[{\"runId\":\"\",\"awardId\":\"a\"},{\"runId\":\"r1\",\"awardId\":\"b\",\"items\":[{\"id\":\"gin\",\"amount\":-3},{\"id\":\"rum\",\"amount\":20}]}]}");
            Assert.DoesNotThrow(() => DrinkLab.ReloadForTests());
            Assert.AreEqual(DrinkStore.LoadSource.Original, DrinkLab.LoadSource);
            Assert.AreEqual(DrinkRules.StockMax, DrinkLab.Owned("gin"), "중복 합침 + 상한");
            Assert.AreEqual(30, DrinkLab.Owned("rum"));
            Assert.IsNull(DrinkLab.Prepared, "규칙 위반 준비 음료는 버프 없이 비움(환불도 없음)");
            Assert.AreEqual(1, DrinkLab.Data.pending.Count);
            Assert.AreEqual(1, DrinkLab.Data.pending[0].items.Count);
            Assert.DoesNotThrow(() => DrinkLab.RecoverAbandoned());
            Assert.AreEqual(50, DrinkLab.Owned("rum"), "고아 보상은 복구 정산");
        }

        [Test]
        public void StoreAddOverflow_Clamps()
        {
            var d = new DrinkSaveData();
            d.Add("gin", int.MaxValue);
            d.Add("gin", int.MaxValue);
            Assert.AreEqual(DrinkRules.StockMax, d.Owned("gin"));
            d.Add("gin", int.MinValue);
            Assert.AreEqual(0, d.Owned("gin"));
        }

        [Test]
        public void DevGrant_OnlyInIsolatedSave()
        {
            Assert.IsTrue(DrinkLab.DevGrant());
            Assert.AreEqual(1200, DrinkLab.Owned("gin"));
            DrinkLab.SetPathForTests(null);
            try { Assert.IsFalse(DrinkLab.IsIsolatedSave); Assert.IsFalse(DrinkLab.DevGrant(), "실제(비격리) 경로 반복 지급 거부"); }
            finally { DrinkLab.SetPathForTests(path); }
        }

        [Test]
        public void ExistingV2Save_Migration_DoesNotTouchSaveJson()
        {
            var save = Path.Combine(dir, "save.json");
            File.WriteAllText(save, "{\"schemaVersion\":2,\"specialtyPoints\":7}");
            Assert.AreEqual(Path.GetFullPath(Path.Combine(dir, "drinks.json")), Path.GetFullPath(DrinkLab.PathForSave(save)));
            DrinkLab.SetPathForTests(DrinkLab.PathForSave(save));
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "기존 저장에도 첫 세트 1회");
            Assert.AreEqual(300, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "기존 창고에 커피 원두 30g을 한 번 마이그레이션");
            Assert.AreEqual(150, DrinkLab.Owned(DrinkIngredients.SteamedMilk), "기존 창고에 스팀 우유 15mL를 한 번 마이그레이션");
            Assert.AreEqual("{\"schemaVersion\":2,\"specialtyPoints\":7}", File.ReadAllText(save), "save.json v2는 그대로");
        }
    }
}
