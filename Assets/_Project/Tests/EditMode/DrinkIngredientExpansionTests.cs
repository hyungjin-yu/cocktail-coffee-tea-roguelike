using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 재료 확장 10종(2026-10-21): 칵테일 3·차 4·산지 원두 3종의 데이터·문화 해설, 공유 코드 색인 순서와 이전 코드/저장 호환,
    /// 산지 원두를 모든 경로(검증·계량 한도·추출 농도·색·효과·주문 판매·제조 차감)에서 원두로 인식, 두 산지 블렌드, 수급 표·희귀 보장.
    /// </summary>
    public class DrinkIngredientExpansionTests
    {
        string dir, path;

        static readonly string[] NewIds = DrinkIngredients.ShareIndex.Skip(10).ToArray();
        static readonly string[] LegacyIds = { "gin", "rum", "blue_curacao", "grenadine", "black_tea", "green_tea", "lemon_juice", "honey", "coffee_bean", "steamed_milk" };
        static readonly string[] Origins = { "ethiopia_yirgacheffe_kochere", "colombia_huila_vergel", "brazil_mantiqueira_alves_lima" };

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "drinkexp_" + Guid.NewGuid().ToString("N"));
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

        static DrinkRecipe Coffee(CoffeeBrewMethod m, params (string id, int amt)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            r.name = "커피";
            foreach (var s in steps) r.SetAmount(s.id, s.amt);
            return r;
        }

        // ───── 데이터·문화 해설 ─────

        [Test]
        public void NewIngredients_Defined_WithUseFormEffectAndArtIds()
        {
            var expect = new Dictionary<string, (DrinkUse use, IngredientForm form, bool baseSpirit)>
            {
                { "absinthe", (DrinkUse.Cocktail, IngredientForm.Liquid, true) },
                { "creme_cassis", (DrinkUse.Cocktail, IngredientForm.Liquid, false) },
                { "limoncello", (DrinkUse.Cocktail, IngredientForm.Liquid, false) },
                { "white_tea", (DrinkUse.Tea, IngredientForm.Leaf, false) },
                { "oolong_tea", (DrinkUse.Tea, IngredientForm.Leaf, false) },
                { "jasmine_tea", (DrinkUse.Tea, IngredientForm.Leaf, false) },
                { "chamomile_flower", (DrinkUse.Tea, IngredientForm.Leaf, false) },
                { "ethiopia_yirgacheffe_kochere", (DrinkUse.Coffee, IngredientForm.Leaf, false) },
                { "colombia_huila_vergel", (DrinkUse.Coffee, IngredientForm.Leaf, false) },
                { "brazil_mantiqueira_alves_lima", (DrinkUse.Coffee, IngredientForm.Leaf, false) },
            };
            Assert.AreEqual(87, DrinkIngredients.All.Count, "칵테일·차·커피 각각 30종");
            Assert.AreEqual(30, DrinkIngredients.All.Count(d => d.UsableIn(DrinkKind.Cocktail)), "칵테일 30종");
            Assert.AreEqual(30, DrinkIngredients.All.Count(d => d.UsableIn(DrinkKind.Tea)), "차 30종");
            Assert.AreEqual(30, DrinkIngredients.All.Count(d => d.UsableIn(DrinkKind.Coffee)), "커피 30종");
            Assert.AreEqual(DrinkIngredients.All.Count, DrinkIngredients.All.Select(d => d.id).Distinct().Count(), "id 중복 없음");
            foreach (var kv in expect)
            {
                var d = DrinkIngredients.Get(kv.Key);
                Assert.IsNotNull(d, kv.Key);
                Assert.AreEqual(kv.Value.use, d.use, kv.Key);
                Assert.AreEqual(kv.Value.form, d.form, kv.Key);
                Assert.AreEqual(kv.Value.baseSpirit, d.baseSpirit, kv.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.name), kv.Key);
                Assert.That(d.coefficient, Is.GreaterThan(0f).And.LessThan(1f), kv.Key + ": 한 재료가 상한을 혼자 채우지 않는다");
                Assert.AreEqual("dl_icon_" + d.id, d.iconId, kv.Key);
                bool coffee = kv.Value.use == DrinkUse.Coffee;
                Assert.AreEqual(coffee, d.IsCoffeeBean, kv.Key);
                Assert.AreEqual((kv.Value.form == IngredientForm.Leaf ? "dl_tin_" : "dl_bottle_") + d.id, d.bottleId, kv.Key);
                var art = DrinkArt.ExpectedIngredientArt(d);
                if (coffee) CollectionAssert.AreEqual(new[] { "dl_icon_" + d.id, "dl_tin_" + d.id, "dl_leaf_coffee_bean" }, art, kv.Key + ": 공용 원두 애니메이션");
                else if (kv.Value.form == IngredientForm.Leaf) CollectionAssert.AreEqual(new[] { "dl_icon_" + d.id, "dl_tin_" + d.id, "dl_leaf_" + d.id, "dl_leaf_heap_" + d.id }, art, kv.Key);
                else CollectionAssert.AreEqual(new[] { "dl_icon_" + d.id, "dl_bottle_" + d.id, "dl_srcbottle_" + d.id }, art, kv.Key);
                if (kv.Value.form == IngredientForm.Leaf) Assert.Greater(d.brewColor.a, 0f, kv.Key + ": 우림/추출 색");
            }
            Assert.AreEqual(DrinkStat.Range, DrinkIngredients.Get("brazil_mantiqueira_alves_lima").effect, "산지 원두 효과 = 원두 효과");
            Assert.AreEqual("coffee_bean", DrinkIngredients.Get("colombia_huila_vergel").LeafArtKey);
            Assert.AreEqual("white_tea", DrinkIngredients.Get("white_tea").LeafArtKey);
            Assert.IsTrue(DrinkIngredients.IsCoffeeBean("coffee_bean"), "기본 원두도 원두");
            Assert.IsFalse(DrinkIngredients.IsCoffeeBean("steamed_milk"));
            Assert.IsFalse(DrinkIngredients.IsCoffeeBean("black_tea"));
            Assert.IsFalse(DrinkIngredients.IsCoffeeBean("nope"));
            Assert.IsFalse(DrinkIngredients.IsCoffeeBean(null));
        }

        [Test]
        public void BeginnerPresets_AreSourceBasedAndCoverAllCoffeeMethods()
        {
            var presets = DrinkLab.ExamplePresets;
            Assert.AreEqual(7, presets.Count, "칵테일 2·차 2·커피 3(추출 도구별)");
            foreach (var recipe in presets)
                Assert.IsTrue(DrinkValidator.Check(recipe).Ok, recipe.name + ": " + DrinkValidator.Check(recipe).First);
            CollectionAssert.AreEquivalent(new[] { CoffeeBrewMethod.EspressoMachine, CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot },
                presets.Where(r => r.Kind == DrinkKind.Coffee).Select(r => r.coffeeMethod).ToArray(), "커피 세 추출 방식 모두 추천");
            Assert.AreEqual(300, presets[0].AmountOf(DrinkIngredients.Gin));
            Assert.AreEqual(300, presets[0].AmountOf(DrinkIngredients.Campari));
            Assert.AreEqual(300, presets[0].AmountOf(DrinkIngredients.SweetVermouth), "IBA 네그로니 동량 배합");
            Assert.AreEqual(165, presets.Single(r => r.coffeeMethod == CoffeeBrewMethod.PourOver).CoffeeDose(), "SCA 55g/L × 300mL");
            Assert.AreEqual(160, presets.Single(r => r.coffeeMethod == CoffeeBrewMethod.MokaPot).CoffeeDose(), "모카포트 3컵 추천 투입량");
            Assert.AreEqual(1300, presets.Single(r => r.coffeeMethod == CoffeeBrewMethod.MokaPot).water, "Bialetti 3컵 모카포트의 약 130mL 배출량");
        }

        [Test]
        public void CultureText_PresentForNew_SeparateFromTaste_ConciseAndNoHealthClaims()
        {
            // 건강 효능·의학적 주장 금지(캐모마일 '수면' 같은 출처 문구도 옮기지 않는다).
            string[] banned = { "효능", "효과가 있", "치료", "수면", "숙면", "불면", "진정", "면역", "다이어트", "혈압", "예방", "소화", "해독", "항산화", "건강", "약효", "의학" };
            foreach (var id in NewIds)
            {
                var d = DrinkIngredients.Get(id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.culture), id + ": 문화·산지 해설");
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.taste), id + ": 맛·향");
                Assert.AreNotEqual(d.taste, d.culture, id);
                Assert.LessOrEqual(d.culture.Length, 110, id + ": 간결(도감 카드 2~4줄)");
            }
            foreach (var d in DrinkIngredients.All)
                foreach (var w in banned)
                {
                    StringAssert.DoesNotContain(w, d.taste ?? "", d.id + " 맛·향");
                    StringAssert.DoesNotContain(w, d.culture ?? "", d.id + " 문화");
                }
            // 출처 문서에 있는 핵심 사실
            StringAssert.Contains("발드트라베르", DrinkIngredients.Get("absinthe").culture);
            StringAssert.Contains("2005", DrinkIngredients.Get("absinthe").culture);
            StringAssert.Contains("디종", DrinkIngredients.Get("creme_cassis").culture);
            StringAssert.Contains("소렌토", DrinkIngredients.Get("limoncello").culture);
            StringAssert.Contains("푸젠", DrinkIngredients.Get("white_tea").culture);
            StringAssert.Contains("산화", DrinkIngredients.Get("oolong_tea").culture);
            StringAssert.Contains("가향차", DrinkIngredients.Get("jasmine_tea").culture);
            StringAssert.Contains("차나무가 아닌", DrinkIngredients.Get("chamomile_flower").culture);
            StringAssert.Contains("워시드", DrinkIngredients.Get("ethiopia_yirgacheffe_kochere").culture);
            StringAssert.Contains("피탈리토", DrinkIngredients.Get("colombia_huila_vergel").culture);
            StringAssert.Contains("미나스제라이스", DrinkIngredients.Get("brazil_mantiqueira_alves_lima").culture);
            StringAssert.Contains("라임즙", DrinkIngredients.Get("lime_juice").culture);
            StringAssert.Contains("안시현", DrinkIngredients.Get("anxi_tieguanyin").culture);
            StringAssert.Contains("보케테", DrinkIngredients.Get("panama_esmeralda_geisha").culture);
            StringAssert.Contains("피탈리토", DrinkIngredients.Get("colombia_huila_san_pedro_geisha").culture);
        }

        // ───── 공유 코드 색인·이전 코드 호환 ─────

        [Test]
        public void ShareIndex_LegacyOrderUnchanged_NewIdsAppended()
        {
            CollectionAssert.AreEqual(LegacyIds, DrinkIngredients.ShareIndex.Take(10).ToArray(), "기존 0~9 순서 고정");
            CollectionAssert.AreEqual(NewIds, DrinkIngredients.ShareIndex.Skip(10).ToArray(), "모든 신규 재료는 끝에 추가");
            CollectionAssert.AreEquivalent(DrinkIngredients.All.Select(d => d.id).ToArray(), DrinkIngredients.ShareIndex, "모든 재료가 공유 가능");
        }

        /// <summary>공유 코드 v1 형식을 이 시험 안에서 따로 만든다(제품 인코더와 독립): 이전 빌드가 만든 코드와 같은 바이트.</summary>
        static string IndependentV1(byte kind, byte tech, byte? method, byte ice, int water, (byte idx, int amt)[] steps, string name)
        {
            var b = new List<byte> { 1, kind, tech };
            if (method.HasValue) b.Add(method.Value);
            b.Add(ice); b.Add((byte)(water >> 8)); b.Add((byte)water);
            b.Add((byte)steps.Length);
            foreach (var s in steps) { b.Add(s.idx); b.Add((byte)(s.amt >> 8)); b.Add((byte)s.amt); }
            var nb = Encoding.UTF8.GetBytes(name);
            b.Add((byte)nb.Length); b.AddRange(nb);
            var table = new uint[256];
            for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; table[i] = c; }
            uint crc = 0xFFFFFFFFu;
            foreach (var x in b) crc = table[(crc ^ x) & 0xFF] ^ (crc >> 8);
            crc ^= 0xFFFFFFFFu;
            b.Add((byte)(crc >> 24)); b.Add((byte)(crc >> 16)); b.Add((byte)(crc >> 8)); b.Add((byte)crc);
            const string A = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var body = new StringBuilder();
            int buf = 0, bits = 0;
            foreach (var x in b) { buf = (buf << 8) | x; bits += 8; while (bits >= 5) { body.Append(A[(buf >> (bits - 5)) & 31]); bits -= 5; } }
            if (bits > 0) body.Append(A[(buf << (5 - bits)) & 31]);
            var sb = new StringBuilder("CCT1-");
            for (int i = 0; i < body.Length; i++) { if (i > 0 && i % 5 == 0) sb.Append('-'); sb.Append(body[i]); }
            return sb.ToString();
        }

        [Test]
        public void LegacyShareCodes_DecodeToSameIngredients_AndEncodeByteIdentical()
        {
            // 이전 빌드의 칵테일(진 0·레몬 6·그레나딘 3)·차(홍차 4·꿀 7)·커피(원두 8·우유 9) 코드: 색인이 바뀌었다면 다른 재료로 읽힌다.
            string cocktail = IndependentV1(1, 1, null, 2, 0, new[] { ((byte)0, 450), ((byte)6, 150), ((byte)3, 50) }, "옛 칵테일");
            string tea = IndependentV1(2, 5, null, 0, DrinkRules.TeaWaterFixed, new[] { ((byte)4, 25), ((byte)7, 50) }, "옛 차");
            string coffee = IndependentV1(4, 6, 1, 0, 360, new[] { ((byte)8, 180), ((byte)9, 200) }, "옛 라테");
            var c = DrinkShareCode.Decode(cocktail, out var e1); Assert.IsNotNull(c, e1);
            CollectionAssert.AreEqual(new[] { "gin", "lemon_juice", "grenadine" }, c.steps.Select(s => s.id).ToArray());
            var t = DrinkShareCode.Decode(tea, out var e2); Assert.IsNotNull(t, e2);
            CollectionAssert.AreEqual(new[] { "black_tea", "honey" }, t.steps.Select(s => s.id).ToArray());
            var k = DrinkShareCode.Decode(coffee, out var e3); Assert.IsNotNull(k, e3);
            CollectionAssert.AreEqual(new[] { "coffee_bean", "steamed_milk" }, k.steps.Select(s => s.id).ToArray());
            Assert.AreEqual(CoffeeBrewMethod.EspressoMachine, k.coffeeMethod);
            // 같은 배합을 지금 인코더로 만들면 이전 코드와 문자 단위로 같다(색인 재배치 없음).
            Assert.AreEqual(cocktail, DrinkShareCode.Encode(c));
            Assert.AreEqual(tea, DrinkShareCode.Encode(t));
            Assert.AreEqual(coffee, DrinkShareCode.Encode(k));
            // 새 재료 색인은 10 이상: 독립 인코더로 만든 압생트(10)·예가체프(17) 코드도 읽힌다.
            var ab = DrinkShareCode.Decode(IndependentV1(1, 2, null, 1, 0, new[] { ((byte)10, 300), ((byte)12, 150) }, "압생트"), out var e4);
            Assert.IsNotNull(ab, e4);
            CollectionAssert.AreEqual(new[] { "absinthe", "limoncello" }, ab.steps.Select(s => s.id).ToArray());
            var ye = DrinkShareCode.Decode(IndependentV1(4, 6, 2, 0, 2400, new[] { ((byte)17, 150) }, "예가"), out var e5);
            Assert.IsNotNull(ye, e5);
            Assert.AreEqual("ethiopia_yirgacheffe_kochere", ye.steps[0].id);
            Assert.IsNull(DrinkShareCode.Decode(IndependentV1(1, 1, null, 0, 0, new[] { ((byte)20, 300) }, "x"), out _), "아직 없는 색인 20은 거부");
        }

        [Test]
        public void ShareCode_RoundTrip_EveryNewIngredient()
        {
            var recipes = new List<DrinkRecipe>();
            foreach (var t in new[] { DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer })
            {
                var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail); r.technique = (int)t; r.name = "새 칵테일";
                r.SetAmount("absinthe", 200); r.SetAmount("creme_cassis", 100); r.SetAmount("limoncello", 150);
                recipes.Add(r);
            }
            foreach (var leaf in new[] { "white_tea", "oolong_tea", "jasmine_tea", "chamomile_flower" })
            {
                var r = DrinkRecipe.NewDraft(DrinkKind.Tea); r.name = "새 차"; r.SetAmount(leaf, 25); r.SetAmount("honey", 50);
                recipes.Add(r);
            }
            foreach (var m in CoffeeBrewing.All)
                foreach (var o in Origins) recipes.Add(Coffee(m, (o, CoffeeBrewing.DoseMinTenths(m))));
            recipes.Add(Coffee(CoffeeBrewMethod.PourOver, (Origins[0], 80), (Origins[2], 70)));
            foreach (var r in recipes)
            {
                Assert.IsTrue(DrinkValidator.Check(r).Ok, DrinkValidator.Check(r).First);
                var back = DrinkShareCode.Decode(DrinkShareCode.Encode(r), out var err);
                Assert.IsNotNull(back, err);
                Assert.IsTrue(r.SameMix(back), string.Join(",", r.steps.Select(s => s.id)));
            }
        }

        // ───── 저장 호환 ─────

        [Test]
        public void LegacySave_LoadsWithStockAndRecipes_NewIdsStoredWithoutLoss()
        {
            // 확장 전 drinks.json: 기존 10종 재고 + coffee_bean 레시피 + 알 수 없는 재료 한 줄. 새 재료 추가가 기존 재고를 줄이지 않는다.
            var old = new DrinkSaveData { starterGranted = true, coffeeStarterGranted = true };
            int n = 1;
            foreach (var id in LegacyIds) old.stock.Add(new DrinkAmount(id, 1000 + n++ * 7));
            old.stock.Add(new DrinkAmount("removed_future_thing", 99));
            var latte = Coffee(CoffeeBrewMethod.EspressoMachine, ("coffee_bean", 180), ("steamed_milk", 200)); latte.water = 300; latte.name = "옛 라테";
            old.recipes.Add(latte);
            File.WriteAllText(path, DrinkStore.Serialize(old));
            DrinkLab.SetPathForTests(path);
            n = 1;
            foreach (var id in LegacyIds) Assert.AreEqual(1000 + n++ * 7, DrinkLab.Owned(id), id + " 재고 보존");
            foreach (var id in NewIds) Assert.AreEqual(0, DrinkLab.Owned(id), id + ": 업데이트 직후 0(자동 지급 없음, 드롭으로 획득)");
            Assert.AreEqual(1, DrinkLab.Recipes.Count);
            Assert.AreEqual("coffee_bean", DrinkLab.Recipes[0].steps[0].id, "기존 원두 레시피 그대로");
            Assert.IsTrue(DrinkValidator.Check(DrinkLab.Recipes[0]).Ok);
            // 새 재료 재고는 저장·다시 읽기에서 사라지지 않는다.
            foreach (var id in NewIds) DrinkLab.Data.Add(id, 333);
            Assert.IsTrue(DrinkLab.SaveNow());
            DrinkLab.SetPathForTests(path);
            foreach (var id in NewIds) Assert.AreEqual(333, DrinkLab.Owned(id), id + " 저장 왕복");
            n = 1;
            foreach (var id in LegacyIds) Assert.AreEqual(1000 + n++ * 7, DrinkLab.Owned(id), id + " 재고 보존(왕복)");
        }

        // ───── 산지 원두 = 원두(모든 추출 도구) ─────

        /// <summary>기본 추출량 ÷ 목표 비율에 가장 가까운 허용 원두량(0.1g).</summary>
        static int TargetDose(CoffeeBrewMethod m) =>
            Mathf.Clamp(Mathf.RoundToInt(CoffeeBrewing.DefaultWaterTenths(m) / CoffeeBrewing.ExtractionRatio(m)), CoffeeBrewing.DoseMinTenths(m), CoffeeBrewing.DoseMaxTenths(m));

        [Test]
        public void Origins_EveryBrewMethod_SameRulesAsCoffeeBean()
        {
            foreach (var m in CoffeeBrewing.All)
                foreach (var o in Origins)
                    foreach (int dose in new[] { CoffeeBrewing.DoseMinTenths(m), (CoffeeBrewing.DoseMinTenths(m) + CoffeeBrewing.DoseMaxTenths(m)) / 2, CoffeeBrewing.DoseMaxTenths(m), TargetDose(m) })
                    {
                        string label = $"{m} {o} {dose}";
                        var origin = Coffee(m, (o, dose));
                        var house = Coffee(m, ("coffee_bean", dose));
                        Assert.IsTrue(DrinkValidator.Check(origin).Ok, label + ": " + DrinkValidator.Check(origin).First);
                        Assert.AreEqual(dose, origin.CoffeeDose(), label);
                        Assert.AreEqual(CoffeeBrewing.Strength01(m, house.CoffeeDose(), house.water), CoffeeBrewing.Strength01(m, origin.CoffeeDose(), origin.water), label + " 추출 농도");
                        Assert.AreEqual(CoffeeBrewing.BrewColor(house), CoffeeBrewing.BrewColor(origin), label + " 추출 색");
                        Assert.AreEqual(DrinkCalculator.Look(house).color, DrinkCalculator.Look(origin).color, label + " 완성 색");
                        Assert.AreEqual(DrinkSales.FamilyOf(house), DrinkSales.FamilyOf(origin), label + " 색 계열");
                        var hb = DrinkCalculator.Buff(house); var ob = DrinkCalculator.Buff(origin);
                        if (dose == TargetDose(m)) Assert.Greater(ob[DrinkStat.Range], 0f, label + ": 기본 추출량의 목표 비율에 가까운 원두량은 효과가 있다");
                        foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat))) Assert.AreEqual(hb[s], ob[s], 1e-6f, label + " " + s);
                        Assert.AreEqual(CoffeeBrewing.DoseMinTenths(m), DrinkSales.MinFor(o, m), label + " 주문 최소량");
                    }
            foreach (var o in Origins)
            {
                Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, (o, 100))).Ok, o + ": 에스프레소 최소 12g");
                Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.PourOver, (o, 190))).Ok, o + ": 핸드 드립 최대 18g");
                var cocktail = DrinkRecipe.NewDraft(DrinkKind.Cocktail); cocktail.SetAmount("gin", 300); cocktail.SetAmount(o, 50);
                Assert.IsFalse(DrinkValidator.Check(cocktail).Ok, o + ": 칵테일에 못 씀");
                var tea = DrinkRecipe.NewDraft(DrinkKind.Tea); tea.SetAmount("black_tea", 20); tea.SetAmount(o, 20);
                Assert.IsFalse(DrinkValidator.Check(tea).Ok, o + ": 차에 못 씀");
                var noBean = Coffee(CoffeeBrewMethod.PourOver, ("steamed_milk", 100));
                Assert.IsFalse(DrinkValidator.Check(noBean).Ok, "원두 없는 커피 거부");
            }
        }

        [Test]
        public void TwoOrigins_Blend_CountsAsOneDose()
        {
            var blend = Coffee(CoffeeBrewMethod.PourOver, ("ethiopia_yirgacheffe_kochere", 90), ("brazil_mantiqueira_alves_lima", 60));
            var house = Coffee(CoffeeBrewMethod.PourOver, ("coffee_bean", 150));
            Assert.IsTrue(DrinkValidator.Check(blend).Ok, DrinkValidator.Check(blend).First);
            Assert.AreEqual(150, blend.CoffeeDose());
            Assert.AreEqual(DrinkCalculator.Buff(house)[DrinkStat.Range], DrinkCalculator.Buff(blend)[DrinkStat.Range], 1e-6f, "섞어도 같은 총량의 한 원두와 같은 효과(산지별 따로 정규화하지 않음)");
            Assert.AreEqual(CoffeeBrewing.BrewColor(house), CoffeeBrewing.BrewColor(blend));
            // 산지 원두 + 기본 원두도 섞인다.
            var withHouse = Coffee(CoffeeBrewMethod.MokaPot, ("coffee_bean", 60), ("colombia_huila_vergel", 60));
            Assert.IsTrue(DrinkValidator.Check(withHouse).Ok, DrinkValidator.Check(withHouse).First);
            // 합으로 한도를 본다: 각각은 한도 이하여도 합이 넘으면 거부, 합이 최소 미만이면 거부.
            Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, ("ethiopia_yirgacheffe_kochere", 130), ("colombia_huila_vergel", 130))).Ok, "합 26g > 24g");
            Assert.IsFalse(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, ("ethiopia_yirgacheffe_kochere", 50), ("colombia_huila_vergel", 50))).Ok, "합 10g < 12g");
            Assert.IsTrue(DrinkValidator.Check(Coffee(CoffeeBrewMethod.EspressoMachine, ("ethiopia_yirgacheffe_kochere", 60), ("colombia_huila_vergel", 60))).Ok, "합 12g");
            // 세 산지 블렌드도 공유 코드·버프가 일관된다.
            var three = Coffee(CoffeeBrewMethod.PourOver, (Origins[0], 50), (Origins[1], 50), (Origins[2], 50));
            Assert.AreEqual(DrinkCalculator.Buff(house)[DrinkStat.Range], DrinkCalculator.Buff(three)[DrinkStat.Range], 1e-6f);
        }

        [Test]
        public void Craft_OriginConsumesOriginStock_SellsToBeanOrder()
        {
            DrinkLab.Data.Add("brazil_mantiqueira_alves_lima", 400);
            DrinkLab.Data.Add("ethiopia_yirgacheffe_kochere", 400);
            int house = DrinkLab.Owned("coffee_bean");
            var r = Coffee(CoffeeBrewMethod.PourOver, ("brazil_mantiqueira_alves_lima", 90), ("ethiopia_yirgacheffe_kochere", 60));
            var res = DrinkLab.Craft(r);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, res.Status, res.Message);
            Assert.AreEqual(310, DrinkLab.Owned("brazil_mantiqueira_alves_lima"));
            Assert.AreEqual(340, DrinkLab.Owned("ethiopia_yirgacheffe_kochere"));
            Assert.AreEqual(house, DrinkLab.Owned("coffee_bean"), "기본 원두는 차감하지 않는다");
            Assert.Greater(res.Buff[DrinkStat.Range], 0f);
            // 저장된 원두 주문(ingA = coffee_bean)은 산지 원두로 만든 커피도 받는다.
            var order = new DrinkOrder { id = "orderX", kind = (int)DrinkKind.Coffee, ingA = "coffee_bean", ingB = "", minA = CoffeeBrewing.DoseMinTenths(CoffeeBrewMethod.PourOver), technique = (int)DrinkTechnique.Brew, method = (int)CoffeeBrewMethod.PourOver, state = "Open" };
            order.colorFamily = DrinkSales.FamilyOf(DrinkSales.Reference(order));
            Assert.IsTrue(DrinkSales.Matches(order, DrinkLab.Prepared.recipe));
            Assert.AreEqual(150, DrinkSales.AmountFor(DrinkLab.Prepared.recipe, "coffee_bean"));
            int price = DrinkSales.Price(order, DrinkLab.Prepared.recipe);
            Assert.GreaterOrEqual(price, DrinkSales.BasePrice);
            DrinkLab.Data.orders.Clear();
            DrinkLab.Data.orders.Add(order);
            int coins = DrinkLab.Coins;
            Assert.IsTrue(DrinkLab.Sell(0, out int got, out var err), err);
            Assert.AreEqual(price, got);
            Assert.AreEqual(coins + price, DrinkLab.Coins);
            Assert.IsNull(DrinkLab.Prepared, "판매로 완성 음료 소비");
        }

        [Test]
        public void Orders_OnlyOriginBeansSeen_StillOfferCoffeeOrders()
        {
            var seen = new HashSet<string> { "gin", "lemon_juice", "black_tea", "honey", "colombia_huila_vergel" };
            int coffee = 0;
            for (int serial = 0; serial < 300; serial++)
            {
                var o = DrinkSales.Generate(serial, seen.Contains);
                if ((DrinkKind)o.kind != DrinkKind.Coffee) continue;
                coffee++;
                Assert.AreEqual("coffee_bean", o.ingA, "주문 저장 형식은 그대로(원두 = 산지 무관)");
                var r = Coffee((CoffeeBrewMethod)o.method, ("colombia_huila_vergel", o.minA));
                Assert.IsTrue(DrinkSales.Matches(o, r), "산지 원두만 가진 계정도 원두 주문을 채운다");
            }
            Assert.Greater(coffee, 0, "원두를 하나라도 발견했으면 커피 주문이 나온다");
            var none = new HashSet<string> { "gin", "lemon_juice", "black_tea", "honey" };
            for (int serial = 0; serial < 300; serial++) Assert.AreNotEqual((int)DrinkKind.Coffee, DrinkSales.Generate(serial, none.Contains).kind, "원두를 본 적 없으면 커피 주문 없음");
            // 칵테일·차 주문 후보에 신규 재료가 들어간다(발견했을 때만).
            var all = new HashSet<string>(DrinkIngredients.All.Select(d => d.id));
            var used = new HashSet<string>();
            for (int serial = 0; serial < 2000; serial++) { var o = DrinkSales.Generate(serial, all.Contains); used.Add(o.ingA); used.Add(o.ingB); Assert.IsTrue(DrinkValidator.Check(DrinkSales.Reference(o)).Ok, "주문 기준 배합은 항상 제조 가능"); }
            foreach (var id in new[] { "absinthe", "creme_cassis", "limoncello", "white_tea", "oolong_tea", "jasmine_tea", "chamomile_flower" })
                Assert.IsTrue(used.Contains(id), id + " 주문 후보");
        }

        [Test]
        public void CategoryEffects_AreDisjointAcrossCocktailTeaCoffee()
        {
            var cocktail = new HashSet<DrinkStat> { DrinkStat.Attack, DrinkStat.AttackSpeed, DrinkStat.CritChance, DrinkStat.MaxHp };
            var tea = new HashSet<DrinkStat> { DrinkStat.CooldownReduction, DrinkStat.MoveSpeed };
            var coffee = new HashSet<DrinkStat> { DrinkStat.Range };
            Assert.IsFalse(cocktail.Overlaps(tea));
            Assert.IsFalse(cocktail.Overlaps(coffee));
            Assert.IsFalse(tea.Overlaps(coffee));
            foreach (var d in DrinkIngredients.All)
            {
                if (d.UsableIn(DrinkKind.Cocktail)) Assert.IsTrue(cocktail.Contains(DrinkCalculator.EffectFor(DrinkKind.Cocktail, d)), d.id + "/cocktail");
                if (d.UsableIn(DrinkKind.Tea)) Assert.IsTrue(tea.Contains(DrinkCalculator.EffectFor(DrinkKind.Tea, d)), d.id + "/tea");
                if (d.UsableIn(DrinkKind.Coffee)) Assert.IsTrue(coffee.Contains(DrinkCalculator.EffectFor(DrinkKind.Coffee, d)), d.id + "/coffee");
            }
        }

        // ───── 칵테일·차 효과 ─────

        [Test]
        public void NewCocktailAndTea_Effects_AndBaseRules()
        {
            var abs = DrinkRecipe.NewDraft(DrinkKind.Cocktail); abs.technique = (int)DrinkTechnique.Build; abs.SetAmount("absinthe", 300);
            Assert.IsTrue(DrinkValidator.Check(abs).Ok, "압생트는 베이스 술");
            Assert.Greater(DrinkCalculator.Buff(abs)[DrinkStat.AttackSpeed], 0f);
            var lim = DrinkRecipe.NewDraft(DrinkKind.Cocktail); lim.SetAmount("limoncello", 300); lim.SetAmount("creme_cassis", 100);
            var check = DrinkValidator.Check(lim);
            Assert.IsFalse(check.Ok, "리큐어만으로는 칵테일이 안 된다");
            StringAssert.Contains("압생트", check.First);
            lim.SetAmount("gin", 300);
            Assert.IsTrue(DrinkValidator.Check(lim).Ok);
            var b = DrinkCalculator.Buff(lim);
            Assert.Greater(b[DrinkStat.Attack], 0f, "리몬첼로");
            Assert.Greater(b[DrinkStat.MaxHp], 0f, "크렘 드 카시스");
            foreach (var (leaf, stat) in new[] { ("white_tea", DrinkStat.CooldownReduction), ("oolong_tea", DrinkStat.MoveSpeed), ("jasmine_tea", DrinkStat.CooldownReduction), ("chamomile_flower", DrinkStat.MoveSpeed) })
            {
                var t = DrinkRecipe.NewDraft(DrinkKind.Tea); t.SetAmount(leaf, 50);
                Assert.IsTrue(DrinkValidator.Check(t).Ok, leaf);
                var tb = DrinkCalculator.Buff(t);
                Assert.Greater(tb[stat], 0f, leaf);
                Assert.LessOrEqual(tb[stat], DrinkRules.Cap(stat), leaf);
                // 우린 색은 찻잎마다 다르고 물보다 짙다.
                Assert.AreNotEqual(DrinkPhysicsSim.TeaColor(t), DrinkPhysicsSim.TeaColor(DrinkRecipe.NewDraft(DrinkKind.Tea)), leaf);
            }
            // 진한 카시스는 무색 압생트를 확실히 물들인다(착색력).
            var mix = DrinkRecipe.NewDraft(DrinkKind.Cocktail); mix.technique = (int)DrinkTechnique.Build; mix.SetAmount("absinthe", 300); mix.SetAmount("creme_cassis", 50);
            var c = DrinkCalculator.Look(mix).color;
            Assert.Greater(c.r, c.g, "카시스 붉은 기");
        }

        // ───── 수급 ─────

        [Test]
        public void Supply_EveryIngredientObtainable_NewRowsCoherent_RareGuaranteeUnchanged()
        {
            foreach (var d in DrinkIngredients.All)
            {
                var row = DrinkSupply.Get(d.id);
                Assert.IsNotNull(row, d.id + ": 획득처 표");
                Assert.Greater(row.bundle, 0, d.id); Assert.Greater(row.baseUse, 0, d.id);
                Assert.LessOrEqual(row.bundle, row.baseUse, d.id + ": 묶음 ≤ 기준 사용량");
                Assert.Greater(row.normal + row.elite + row.midBoss + row.finalBoss, 0, d.id);
                StringAssert.Contains("일반", DrinkLab.SourceText(d.id), d.id);
            }
            foreach (var id in NewIds)
            {
                var row = DrinkSupply.Get(id);
                Assert.IsFalse(row.rare, id + ": 보스 희귀 보장 후보를 늘리지 않는다");
                foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source))) Assert.Greater(row.Weight(s), 0, $"{id} {s}");
            }
            foreach (var o in Origins) Assert.AreEqual(DrinkSupply.Get("coffee_bean").baseUse, DrinkSupply.Get(o).baseUse, o + ": 원두와 같은 기준 사용량");
            CollectionAssert.AreEquivalent(new[] { "blue_curacao", "grenadine" }, DrinkSupply.Table.Where(r => r.rare).Select(r => r.id).ToArray());
            foreach (var s in new[] { DrinkSupply.Source.MidBoss, DrinkSupply.Source.FinalBoss })
            {
                CollectionAssert.AreEquivalent(new[] { "blue_curacao", "grenadine" }, DrinkSupply.Probabilities(s, true, id => 0).Keys.ToArray(), s + " 희귀 보장 후보");
                for (int seed = 0; seed < 500; seed++)
                {
                    var roll = DrinkSupply.Roll(s, "exp" + seed, "boss_" + s, id => 0);
                    for (int i = 0; i < roll.Count; i++)
                        if (DrinkSupply.RareOnly(s, i)) Assert.IsTrue(roll[i].id == "blue_curacao" || roll[i].id == "grenadine", $"{s} {seed} #{i} {roll[i].id}");
                }
            }
        }

        [Test]
        public void Supply_NewZeroStock_CaughtUpByRelativeCorrection_PerSourceSumsStay100()
        {
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
                Assert.AreEqual(100 + DrinkIngredients.All.Count - 29, DrinkSupply.Table.Sum(r => r.Weight(s)), s.ToString());
            // 기본 10종은 10회분, 나머지는 0(업데이트 직후): 양수 재고 중앙값으로 신규 품목을 우대한다.
            Func<string, int> held = id => NewIds.Contains(id) ? 0 : DrinkSupply.Get(id).baseUse * 10;
            foreach (DrinkSupply.Source s in Enum.GetValues(typeof(DrinkSupply.Source)))
            {
                var p = DrinkSupply.Probabilities(s, false, held);
                Assert.AreEqual(2.0, DrinkSupply.Multiplier(0, 10), 1e-12, "신규 품목이 절반 이상 0이어도 양수 재고 중앙값 사용");
                Assert.AreEqual(1.0, DrinkSupply.Multiplier(10, 10), 1e-12);
                double sum = DrinkSupply.Table.Sum(r => r.Weight(s) * (NewIds.Contains(r.id) ? 2.0 : 1.0));
                foreach (var r in DrinkSupply.Table)
                {
                    Assert.AreEqual(r.Weight(s) * (NewIds.Contains(r.id) ? 2.0 : 1.0) / sum, p[r.id], 1e-12, $"{s} {r.id}");
                    if (NewIds.Contains(r.id)) Assert.Greater(p[r.id], r.Weight(s) / (100.0 + DrinkIngredients.All.Count - 29), $"{s} {r.id}: 0회분 신규 재료 우대");
                    else Assert.Less(p[r.id], r.Weight(s) / (100.0 + DrinkIngredients.All.Count - 29), $"{s} {r.id}: 따라잡는 동안 기존 재료는 덜 나온다");
                }
                Assert.AreEqual(1.0, p.Values.Sum(), 1e-12);
            }
            // 따라잡으면(모두 같은 회분) 원래 표 비율로 돌아간다.
            var even = DrinkSupply.Probabilities(DrinkSupply.Source.Normal, false, id => DrinkSupply.Get(id).baseUse * 10);
            foreach (var r in DrinkSupply.Table) Assert.AreEqual(r.normal / (100.0 + DrinkIngredients.All.Count - 29), even[r.id], 1e-12, r.id);
        }

        [Test]
        public void Supply_Distribution_ManySeeds_MatchesNewTable()
        {
            const int n = 60000;
            foreach (DrinkSupply.Source s in new[] { DrinkSupply.Source.Normal, DrinkSupply.Source.Elite })
            {
                var count = new Dictionary<string, int>();
                for (int i = 0; i < n; i++)
                {
                    var r = DrinkSupply.Roll(s, "dist" + i, "x", id => 0);
                    count[r[0].id] = (count.TryGetValue(r[0].id, out int c) ? c : 0) + 1;
                }
                foreach (var row in DrinkSupply.Table)
                    Assert.AreEqual(row.Weight(s) / (100.0 + DrinkIngredients.All.Count - 29), (count.TryGetValue(row.id, out int c) ? c : 0) / (double)n, 0.006, $"{s} {row.id}");
            }
        }
    }
}
