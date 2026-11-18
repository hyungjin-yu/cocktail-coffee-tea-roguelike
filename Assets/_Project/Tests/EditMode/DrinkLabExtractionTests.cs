using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>DrinkLab 구조 분리(2026-10-30): 따르기·젓기 계산(DrinkPourMotion), 그릇 마스크 기하(DrinkVesselGeometry),
    /// 레시피 서랍 흐름(DrinkRecipeFlow)이 화면 클래스의 기존 공개 함수와 같은 값을 내는지(동작 보존) 확인한다.</summary>
    public class DrinkLabExtractionTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "drinkextract_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
        }

        [TearDown]
        public void TearDown()
        {
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        static DrinkRecipe Cocktail(string name, params (string id, int amt)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Shake;
            r.name = name;
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.amt));
            return r;
        }

        // ───── DrinkPourMotion ─────

        [Test]
        public void PourMotion_RateAndPourSeconds_MatchStageApi()
        {
            foreach (var def in DrinkIngredients.All)
            {
                for (float tilt = 60f; tilt <= 170f; tilt += 7.5f)
                {
                    Assert.AreEqual(DrinkPourMotion.Rate(tilt, def, DrinkStageView.TiltStart), DrinkStageView.Rate(tilt, def), def.id);
                    Assert.AreEqual(DrinkPourMotion.Rate(tilt, def, 100f), DrinkStageView.Rate(tilt, def, 100f), def.id);
                }
                for (int tenths = 0; tenths <= 600; tenths += 37)
                    Assert.AreEqual(DrinkPourMotion.PourSeconds(def, tenths), DrinkStageView.PourSeconds(def, tenths), def.id);
            }
            Assert.AreEqual(0f, DrinkPourMotion.Rate(90f, null, 85f));
            Assert.AreEqual(0.8f, DrinkPourMotion.PourSeconds(null, 100));
            var gin = DrinkIngredients.Get("gin");
            Assert.AreEqual(0f, DrinkPourMotion.Rate(85f, gin, 85f), "시작 각도에서는 흐르지 않음");
            Assert.AreEqual(DrinkPourMotion.LiquidMaxRate * (1f - 0.6f * gin.viscosity), DrinkPourMotion.Rate(135f, gin, 85f), 1e-4f, "시작 + 50° = 최대");
            Assert.AreEqual(DrinkStageView.LiquidMaxRate, DrinkPourMotion.LiquidMaxRate);
            Assert.AreEqual(DrinkStageView.LeafMaxRate, DrinkPourMotion.LeafMaxRate);
            Assert.AreEqual(DrinkStageView.BeanMaxRate, DrinkPourMotion.BeanMaxRate);
        }

        [Test]
        public void PourMotion_StirTimeline_RampAndMixTotal()
        {
            Assert.AreEqual(DrinkStageView.StirInsert, DrinkPourMotion.StirInsert);
            Assert.AreEqual(DrinkStageView.StirDuration, DrinkPourMotion.StirDuration);
            Assert.AreEqual(2.3f, DrinkPourMotion.StirDuration, 1e-6f);
            Assert.AreEqual(0f, DrinkPourMotion.StirRamp(0f));
            Assert.AreEqual(1f, DrinkPourMotion.StirRamp(DrinkPourMotion.StirInsert));
            Assert.AreEqual(1f, DrinkPourMotion.StirRamp(DrinkPourMotion.StirDuration * 0.5f));
            Assert.AreEqual(0f, DrinkPourMotion.StirRamp(DrinkPourMotion.StirDuration));
            Assert.IsFalse(DrinkPourMotion.StirMixing(DrinkPourMotion.StirInsert), "넣는 구간 끝까지는 섞지 않음");
            Assert.IsTrue(DrinkPourMotion.StirMixing(1f));
            Assert.IsFalse(DrinkPourMotion.StirMixing(DrinkPourMotion.StirDuration - DrinkPourMotion.StirInsert));
            // 1/60초 프레임으로 젓는 구간 전체를 더하면 총 강도 6(최종 혼합과 같은 양)
            double total = 0;
            const float dt = 1f / 60f;
            for (float t = dt; t < DrinkPourMotion.StirDuration; t += dt)
                if (DrinkPourMotion.StirMixing(t)) total += DrinkPourMotion.StirMixStep(dt);
            Assert.AreEqual(6.0, total, 0.1);
        }

        [Test]
        public void PourMotion_SpoonPose_Pieces()
        {
            Assert.AreEqual(0, DrinkPourMotion.SpoonFrame(1.23f, 1));
            Assert.AreEqual(3, DrinkPourMotion.SpoonFrame(0.35f, 4));
            Assert.AreEqual(0, DrinkPourMotion.SpoonFrame(0.45f, 4));
            Assert.AreEqual(0.9f, DrinkPourMotion.SpoonTipY(0.7f, 0f, 0.9f, 0.1f, 64f), 1e-6f, "ramp 0 = 림 위 대기");
            float bottom = DrinkPourMotion.SpoonTipY(0.7f, 1f, 0.9f, 0.1f, 64f);
            Assert.That(bottom, Is.InRange(0.1f, 0.1f + 2f / 64f + 1e-6f), "ramp 1 = 바닥 위 0~2 아트 픽셀");
            Assert.AreEqual(0f, DrinkPourMotion.StirReach(0.4f, 0.5f, 0.2f, 48f), "좁으면 0");
            Assert.AreEqual(0.5f, DrinkPourMotion.SpoonX(0.5f, 1f, 0.2f, 0f), 1e-6f);
            Assert.AreEqual(4f, DrinkPourMotion.SpoonLean(1f, 0f), 1e-6f);
            // 첫 행은 비고, 볼 폭은 끝 중심(6)에서 가장 먼 거리 2
            Assert.AreEqual(2f, DrinkPourMotion.BowlHalfWidth(new[] { -1, -1, 5, 7, 4, 8 }, 16));
            Assert.AreEqual(3f, DrinkPourMotion.BowlHalfWidth(new[] { -1, -1, -1, -1 }, 16), "빈 스푼은 기본 3");
        }

        [Test]
        public void PourMotion_SpillOnsetAngle_Bounds()
        {
            Assert.AreEqual(90f, DrinkPourMotion.SpillOnsetAngle(null, 10.0));
            var profile = DrinkVesselGeometry.Profile("dl_jigger", 60f);
            Assert.AreEqual(90f, DrinkPourMotion.SpillOnsetAngle(profile, 0.0));
            float full = DrinkPourMotion.SpillOnsetAngle(profile, 55.0), low = DrinkPourMotion.SpillOnsetAngle(profile, 10.0);
            Assert.That(full, Is.InRange(0f, 178f));
            Assert.LessOrEqual(full, low, "많이 담길수록 일찍 입구에 닿는다");
        }

        // ───── DrinkVesselGeometry (DrinkArt 위임) ─────

        [Test]
        public void VesselGeometry_DrinkArtForwarders_ReturnSameResults()
        {
            foreach (var id in new[] { "dl_jigger", "dl_mixing_glass", "dl_glass_cocktail", "dl_teacup", "dl_no_such_vessel" })
            {
                Assert.AreSame(DrinkVesselGeometry.Profile(id, 120f), DrinkArt.Profile(id, 120f), id);
                for (float y = 0.05f; y < 1f; y += 0.15f)
                {
                    for (float x = 0.05f; x < 1f; x += 0.15f)
                        Assert.AreEqual(DrinkVesselGeometry.InsideMask(id, new Vector2(x, y)), DrinkArt.InsideMask(id, new Vector2(x, y)), $"{id} {x},{y}");
                    DrinkVesselGeometry.MaskSpan(id, y, out float a0, out float a1);
                    DrinkArt.MaskSpan(id, y, out float b0, out float b1);
                    Assert.AreEqual(a0, b0); Assert.AreEqual(a1, b1);
                }
                Assert.AreEqual(DrinkVesselGeometry.AlphaBottom(id), DrinkArt.AlphaBottom(id));
            }
            Assert.AreEqual(0, DrinkArt.AlphaBottom(null));
            bool a = DrinkVesselGeometry.SpoonExtent(2, out var ra, out int wa, out int ha);
            bool b = DrinkArt.SpoonExtent(2, out var rb, out int wb, out int hb);
            Assert.AreEqual(a, b); Assert.AreSame(ra, rb); Assert.AreEqual(wa, wb); Assert.AreEqual(ha, hb);
        }

        // ───── DrinkRecipeFlow ─────

        [Test]
        public void RecipeFlow_SaveEditLoad_TracksEditingIndex()
        {
            var flow = new DrinkRecipeFlow();
            Assert.AreEqual(-1, flow.EditingIndex);
            var draft = Cocktail("초안", ("gin", 300), ("lemon_juice", 150));
            Assert.IsTrue(flow.SaveDraft(draft, "푸른 밤", out var msg, out var shown), msg);
            Assert.AreEqual("레시피를 저장해 레시피 서랍에 꽂았습니다(재료 소모 없음).", msg);
            Assert.AreEqual("푸른 밤", shown);
            Assert.AreEqual("초안", draft.name, "저장은 사본에 이름을 붙인다");
            Assert.AreEqual(1, DrinkLab.Recipes.Count);

            var loaded = flow.LoadRecipe(0, true);
            Assert.IsNotNull(loaded);
            Assert.AreNotSame(DrinkLab.Recipes[0], loaded, "불러오기는 사본");
            Assert.AreEqual(0, flow.EditingIndex);
            Assert.AreEqual(DrinkKind.Cocktail, DrinkRecipeFlow.CraftKind(loaded));
            loaded.steps[0].amount = 250;
            Assert.IsTrue(flow.SaveDraft(loaded, "", out msg, out shown), msg);
            Assert.AreEqual("레시피를 수정했습니다.", msg);
            Assert.AreEqual(DrinkLab.Recipes[0].name, shown, "수정은 그 칸 이름으로 보인다");
            Assert.AreEqual(1, DrinkLab.Recipes.Count, "수정은 덮어쓰기");
            Assert.AreEqual(250, DrinkLab.Recipes[0].steps[0].amount);

            Assert.IsNull(flow.LoadRecipe(5, false), "범위 밖");
            Assert.AreEqual(0, flow.EditingIndex, "범위 밖은 상태 유지");
            Assert.IsNotNull(flow.LoadRecipe(0, false));
            Assert.AreEqual(-1, flow.EditingIndex);
            flow.LoadRecipe(0, true);
            flow.StopEditing();
            Assert.AreEqual(-1, flow.EditingIndex);
            Assert.IsNotNull(flow.LoadPreset(0));
            Assert.IsNull(flow.LoadPreset(-1));
            Assert.IsFalse(flow.SaveDraft(Cocktail("x", ("lemon_juice", 10)), "x", out msg, out shown), "잘못된 배합 저장 거부");
            Assert.IsNull(shown);
            Assert.IsFalse(string.IsNullOrEmpty(msg));
        }

        [Test]
        public void RecipeFlow_ShareAndImport_RoundTrip()
        {
            var flow = new DrinkRecipeFlow();
            Assert.IsTrue(flow.SaveDraft(Cocktail("공유", ("gin", 300), ("blue_curacao", 40)), "공유", out var msg, out _), msg);
            Assert.IsNull(DrinkRecipeFlow.CopyCode(3, out msg));
            Assert.IsNull(msg);
            var code = DrinkRecipeFlow.CopyCode(0, out msg);
            Assert.AreEqual(DrinkShareCode.Encode(DrinkLab.Recipes[0]), code);
            StringAssert.Contains("\"공유\" 공유 코드", msg);

            Assert.IsFalse(flow.SaveImport(out msg));
            Assert.AreEqual("먼저 코드를 확인하세요.", msg);
            Assert.IsFalse(flow.PreviewImport("CCT1-ZZZZ-ZZZZ", out var error));
            Assert.IsNotNull(error);
            Assert.IsNull(flow.ImportPreview);
            int gin = DrinkLab.Owned("gin");
            Assert.IsTrue(flow.PreviewImport(code, out error), error);
            Assert.AreEqual("공유", flow.ImportPreview.name);
            Assert.IsTrue(flow.SaveImport(out msg), msg);
            Assert.AreEqual("가져온 레시피를 저장했습니다(창고·버프 변화 없음).", msg);
            Assert.IsNull(flow.ImportPreview, "저장 뒤 후보 비움");
            Assert.AreEqual(2, DrinkLab.Recipes.Count);
            Assert.AreEqual(gin, DrinkLab.Owned("gin"), "가져오기는 창고를 바꾸지 않는다");
            Assert.AreEqual("레시피를 불러왔습니다. 확인 후 제조 확정을 누르세요.", DrinkRecipeFlow.LoadedMessage(DrinkLab.Recipes[1], false));
            Assert.AreEqual("레시피 수정 중 — 저장하면 덮어씁니다.", DrinkRecipeFlow.LoadedMessage(DrinkLab.Recipes[1], true));
            var big = Cocktail("많이", ("gin", 1000));
            StringAssert.StartsWith("재료가 부족합니다(자동 대체·차감 없음): ", DrinkRecipeFlow.LoadedMessage(big, false));
        }
    }
}
