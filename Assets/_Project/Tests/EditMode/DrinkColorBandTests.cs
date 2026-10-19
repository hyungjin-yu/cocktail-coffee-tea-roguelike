using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>73 사용자 피드백 "중간 색이 계속 바뀐다": 표시 띠는 고정 분할(띠 수 상한), 시간 평균 표시는 성분 합계가 정확하고 맨 위 색이 프레임마다 튀지 않는다.</summary>
    public class DrinkColorBandTests
    {
        static FluidColumn Settling()
        {
            var col = new FluidColumn();
            col.Pour(DrinkPhysicsSim.Part("gin", 90), 0.6);
            col.Pour(DrinkPhysicsSim.Part("grenadine", 30), 0.6);
            return col;
        }

        [Test]
        public void MidOverturn_FixedBands_VolumeAndMassConserved()
        {
            var col = Settling();
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            for (int step = 0; step < 40; step++)
            {
                col.Step(0.1);
                var bands = DrinkPhysicsSim.Layers(col, r);
                Assert.LessOrEqual(bands.Count, DrinkPhysicsSim.MaxDisplayBands, $"t={step * 0.1:0.0}");
                float v = 0f, m = 0f;
                foreach (var b in bands) { v += b.volume; m += b.volume * b.density; }
                Assert.AreEqual(120f, v, 1e-3f, "띠 합계 = 기둥 부피");
                Assert.AreEqual((float)col.TotalMass, m, 1e-2f, "띠 질량 합 = 기둥 질량");
            }
        }

        static UnityEngine.Color Top(System.Collections.Generic.List<DrinkLayer> l) => l[l.Count - 1].color;

        static float Jump(UnityEngine.Color a, UnityEngine.Color b) =>
            UnityEngine.Mathf.Max(UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(a.r - b.r), UnityEngine.Mathf.Abs(a.g - b.g)), UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(a.b - b.b), UnityEngine.Mathf.Abs(a.a - b.a)));

        [Test]
        public void DisplaySmoother_ExactTotals_LessTopFlickerThanRawColumn()
        {
            // 진 위에 레몬즙(진보다 무거움)이 들어와 가라앉는 중: 1차원 셀 교대가 맨 위 색을 프레임마다 바꾼다
            var col = new FluidColumn();
            col.Pour(DrinkPhysicsSim.Part("blue_curacao", 10), 0.4);
            col.Pour(DrinkPhysicsSim.Part("gin", 30), 0.4);
            col.Pour(DrinkPhysicsSim.Part("lemon_juice", 15), 0.4);
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            var sm = new DrinkPhysicsSim.DisplaySmoother();
            float rawMax = 0f, smoothMax = 0f;
            UnityEngine.Color? rawPrev = null, smPrev = null;
            for (int f = 0; f < 180; f++)
            {
                col.Step(1 / 60.0);
                var disp = sm.Get(col, f / 60.0, f);
                foreach (var id in new[] { "gin", "blue_curacao", "lemon_juice" })
                    Assert.AreEqual(col.ComponentVolume(id), disp.ComponentVolume(id), 1e-6, $"frame {f} {id}: 표시 기둥 성분 합계 = 실제");
                var raw = Top(DrinkPhysicsSim.Layers(col, r));
                var smooth = Top(DrinkPhysicsSim.Layers(disp, r));
                if (rawPrev != null) { rawMax = UnityEngine.Mathf.Max(rawMax, Jump(rawPrev.Value, raw)); smoothMax = UnityEngine.Mathf.Max(smoothMax, Jump(smPrev.Value, smooth)); }
                rawPrev = raw; smPrev = smooth;
            }
            UnityEngine.Debug.Log($"top colour max per-frame jump raw={rawMax:0.000} smoothed={smoothMax:0.000}");
            Assert.LessOrEqual(smoothMax, 0.08f, "표시 맨 위 색은 프레임당 0.08 이하로 이어서 변한다");
        }

        [Test]
        public void ColorOfParts_SameCompositionSplitIntoPieces_SameColour()
        {
            // 띠 = 셀 조각 묶음: 같은 조성이 1조각이든 8조각이든 같은 색(착색 상한이 조각마다 적용되어 진해지던 결함)
            var whole = new[] { DrinkPhysicsSim.Part("blue_curacao", 8), DrinkPhysicsSim.Part("gin", 24) };
            var pieces = new System.Collections.Generic.List<FluidPart>();
            for (int i = 0; i < 8; i++) { pieces.Add(DrinkPhysicsSim.Part("blue_curacao", 1)); pieces.Add(DrinkPhysicsSim.Part("gin", 3)); }
            var a = DrinkCalculator.ColorOfParts(whole, null, out float ta);
            var b = DrinkCalculator.ColorOfParts(pieces, null, out float tb);
            Assert.Less(Jump(a, b), 1e-5f);
            Assert.AreEqual(ta, tb, 1e-6f);
            var pure = DrinkCalculator.ColorOfParts(new[] { DrinkPhysicsSim.Part("blue_curacao", 0.5), DrinkPhysicsSim.Part("blue_curacao", 0.5), DrinkPhysicsSim.Part("blue_curacao", 0.5) }, null, out _);
            Assert.Less(Jump(DrinkCalculator.IngredientLook(DrinkIngredients.Get("blue_curacao")), pure), 1e-5f, "순수 재료는 양·조각 수와 무관하게 같은 색");
        }

        [Test]
        public void UniformMix_IsOneBand()
        {
            var col = Settling();
            col.Mix(1.0, 6);
            Assert.AreEqual(1, DrinkPhysicsSim.Layers(col, DrinkRecipe.NewDraft(DrinkKind.Cocktail)).Count, "다 섞인 음료는 한 색");
        }

        [Test]
        public void DistinctLayers_StayDistinct()
        {
            // 천천히 쌓은 다른 색은 합치지 않는다(레이어링 표현 유지)
            var col = new FluidColumn();
            col.Pour(DrinkPhysicsSim.Part("grenadine", 20), 1.0);
            col.Pour(DrinkPhysicsSim.Part("blue_curacao", 20), 1.0);
            col.Pour(DrinkPhysicsSim.Part("gin", 30), 1.0);
            Assert.GreaterOrEqual(DrinkPhysicsSim.Layers(col, DrinkRecipe.NewDraft(DrinkKind.Cocktail)).Count, 3);
        }
    }
}
