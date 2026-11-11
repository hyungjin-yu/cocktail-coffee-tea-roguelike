using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-12 커피 추출·차 우림 보조 전투 특성: 대응표, 강적 판정, 차 계보(이어 우리기·소비·중복·저장 호환), 잔 교체.</summary>
    public class RunDrinkTraitTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "traits_" + System.Guid.NewGuid().ToString("N"));
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

        static DrinkRecipe Preset(string name) => DrinkLab.ExamplePresets.First(r => r.name.Contains(name)).Clone();
        static void Stock(DrinkRecipe r, int cups) { foreach (var s in r.steps) DrinkLab.Data.Add(s.id, s.amount * cups); }

        [Test]
        public void TraitMapping_FromRealRecipeMethods_AndSteeps()
        {
            Assert.AreEqual(RunDrinkTraitKind.EspressoFocus, RunDrinkTrait.For(new RunCup { recipe = Preset("에스프레소") }));
            Assert.AreEqual(RunDrinkTraitKind.DripSpread, RunDrinkTrait.For(new RunCup { recipe = Preset("핸드 드립") }));
            Assert.AreEqual(RunDrinkTraitKind.MokaSteam, RunDrinkTrait.For(new RunCup { recipe = Preset("모카포트") }), "모카포트는 기존 효과 유지(새 분류 없음)");
            var tea = Preset("센차");
            Assert.AreEqual(RunDrinkTraitKind.TeaFirst, RunDrinkTrait.For(new RunCup { recipe = tea, teaSteep = 1 }));
            Assert.AreEqual(RunDrinkTraitKind.TeaFirst, RunDrinkTrait.For(new RunCup { recipe = tea, teaSteep = 0 }), "기존 저장 잔(단계 없음) = 1번째");
            Assert.AreEqual(RunDrinkTraitKind.TeaSecond, RunDrinkTrait.For(new RunCup { recipe = tea, teaSteep = 2 }));
            Assert.AreEqual(RunDrinkTraitKind.TeaThird, RunDrinkTrait.For(new RunCup { recipe = tea, teaSteep = 3 }));
            Assert.AreEqual(RunDrinkTraitKind.None, RunDrinkTrait.For(new RunCup { recipe = Preset("네그로니") }), "칵테일은 기존 규칙(종류 보너스)만");
            foreach (RunDrinkTraitKind k in System.Enum.GetValues(typeof(RunDrinkTraitKind)))
                if (k != RunDrinkTraitKind.None) Assert.IsNotEmpty(RunDrinkTrait.Describe(k));
            StringAssert.Contains("보스 제외", RunDrinkTrait.Describe(RunDrinkTraitKind.TeaSecond), "제한 숨기지 않음");
            StringAssert.Contains("일반 적은 변화 없음", RunDrinkTrait.Describe(RunDrinkTraitKind.EspressoFocus));
        }

        [Test]
        public void EliteCheck_UsesExistingDefs()
        {
            Assert.IsTrue(RunDrinkTrait.IsElite(EnemyDef.Brute));
            Assert.IsTrue(RunDrinkTrait.IsElite(EnemyDef.MartiniSpirit));
            Assert.IsTrue(RunDrinkTrait.IsElite(EnemyDef.DifficultCustomer), "보스");
            Assert.IsTrue(RunDrinkTrait.IsElite(MasterDefs.FairCustomer(Job.Bartender)), "박람회 진상 손님");
            Assert.IsFalse(RunDrinkTrait.IsElite(EnemyDef.Imp));
            Assert.IsFalse(RunDrinkTrait.IsElite(EnemyDef.EspressoBug));
        }

        [Test]
        public void TeaLineage_ContinueOnlyFromDrunk_OncePerCup_MaxThree_NoIngredients_NewBrewNewLineage()
        {
            var tea = Preset("센차");
            Stock(tea, 2);
            var first = DrinkLab.CraftInRun("runT", tea);
            Assert.IsTrue(first.Ok, first.Message);
            Assert.AreEqual(1, first.Cup.Steep);
            Assert.AreEqual(first.Cup.cupId, first.Cup.Lineage);
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("runT", first.Cup.cupId).Ok, "보관 중(마시기 전)에는 이어 우리기 없음");
            Assert.IsTrue(DrinkLab.UseRunCup("runT", first.Cup.cupId, RunCup.Drunk, out _));
            int leavesBefore = DrinkLab.Owned(DrinkIngredients.SenchaTea);
            var second = DrinkLab.ContinueTeaSteep("runT", first.Cup.cupId);
            Assert.IsTrue(second.Ok, second.Message);
            Assert.AreEqual(2, second.Cup.Steep);
            Assert.AreEqual(first.Cup.cupId, second.Cup.Lineage, "마신 뒤에도 계보 유지");
            Assert.AreEqual(leavesBefore, DrinkLab.Owned(DrinkIngredients.SenchaTea), "이어 우리기 재료 0");
            Assert.IsEmpty(second.Cup.fromRun); Assert.IsEmpty(second.Cup.fromStockNormal); Assert.IsEmpty(second.Cup.fromStockEndless);
            Assert.IsFalse(second.Cup.Submittable, "2번째 제출 불가");
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("runT", first.Cup.cupId).Ok, "원 잔당 1회(중복 클릭·창 재열기)");
            Assert.AreEqual(2, DrinkLab.RunCupsFor("runT").Count, "중복 계보 잔 없음");
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("runT", second.Cup.cupId).Ok, "2번째도 마셔야 3번째");
            Assert.IsTrue(DrinkLab.UseRunCup("runT", second.Cup.cupId, RunCup.Drunk, out _));
            var third = DrinkLab.ContinueTeaSteep("runT", second.Cup.cupId);
            Assert.IsTrue(third.Ok, third.Message);
            Assert.AreEqual(3, third.Cup.Steep);
            Assert.IsTrue(DrinkLab.UseRunCup("runT", third.Cup.cupId, RunCup.Drunk, out _));
            var fourth = DrinkLab.ContinueTeaSteep("runT", third.Cup.cupId);
            Assert.IsFalse(fourth.Ok, "3번째 이후 → 새로 우리기");
            StringAssert.Contains("새로 우리", fourth.Message);
            // 새로 우리기 = 새 계보 1번째(같은 차 종류라도 이전 계보와 무관)
            var fresh = DrinkLab.CraftInRun("runT", tea);
            Assert.IsTrue(fresh.Ok);
            Assert.AreEqual(1, fresh.Cup.Steep);
            Assert.AreNotEqual(first.Cup.Lineage, fresh.Cup.Lineage);
            Assert.IsTrue(fresh.Cup.Submittable);
            // 다른 런·제출한 잔은 이어 우리기 없음
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("other", first.Cup.cupId).Ok);
            Assert.IsTrue(DrinkLab.UseRunCup("runT", fresh.Cup.cupId, RunCup.Submitted, out _));
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("runT", fresh.Cup.cupId).Ok, "제출한 잔 재사용 없음");
            // 커피·칵테일은 이어 우리기 없음
            var cof = Preset("에스프레소"); Stock(cof, 1);
            var c = DrinkLab.CraftInRun("runT", cof); DrinkLab.UseRunCup("runT", c.Cup.cupId, RunCup.Drunk, out _);
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("runT", c.Cup.cupId).Ok);
        }

        [Test]
        public void TeaLineage_PersistsAcrossReload_AndOldCupsGetDefaults()
        {
            var tea = Preset("센차"); Stock(tea, 1);
            var first = DrinkLab.CraftInRun("runP", tea);
            DrinkLab.UseRunCup("runP", first.Cup.cupId, RunCup.Drunk, out _);
            var second = DrinkLab.ContinueTeaSteep("runP", first.Cup.cupId);
            Assert.IsTrue(second.Ok);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json")); // 다시 읽기
            var cups = DrinkLab.RunCupsFor("runP");
            var r1 = cups.Single(x => x.cupId == first.Cup.cupId);
            var r2 = cups.Single(x => x.cupId == second.Cup.cupId);
            Assert.AreEqual(second.Cup.cupId, r1.continuedTo, "저장 후에도 1회 사용 기록");
            Assert.AreEqual(2, r2.Steep);
            Assert.AreEqual(first.Cup.cupId, r2.Lineage);
            Assert.IsFalse(DrinkLab.ContinueTeaSteep("runP", first.Cup.cupId).Ok, "재시작 후 중복 이어 우리기 없음");
            // 필드 없는 기존 잔(JSON) → 1번째·자기 계보·이어 우리기 기록 없음
            var old = JsonUtility.FromJson<RunCup>("{\"runId\":\"r\",\"cupId\":\"c1\",\"state\":\"Drunk\",\"recipe\":" + JsonUtility.ToJson(tea) + "}");
            Assert.AreEqual(1, old.Steep);
            Assert.AreEqual("c1", old.Lineage);
            Assert.IsTrue(old.CanContinueSteep);
            Assert.IsTrue(old.Submittable);
        }
    }
}
