using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 69/71 배합 ↔ 축약 유체 상태(DrinkFluidPhysics.FluidColumn) 연결. 재료 부피(0.1 정수 → mL)와 DrinkIngredientDef.density/viscosity로
    /// 성분을 만들고, 기법별 붓기 부드러움(gentleness)·혼합 강도로 기둥을 만든다. 같은 배합·기법은 항상 같은 최종 상태(결정적)라
    /// 제조 화면의 완성 잔, 모션 건너뛰기, 출발 공개 화면이 같은 양·층·색을 쓴다. 색은 층 성분으로 DrinkCalculator가 계산한다.
    /// </summary>
    public static class DrinkPhysicsSim
    {
        public const string WaterId = "water", TeaId = "tea";

        /// <summary>붓기 부드러움: 레이어링은 바스푼 등으로 아주 천천히(0.95), 빌드는 잔에 그대로(0.6), 셰이커·믹싱 글라스 투입은 빠르게(0.4).</summary>
        public static double Gentleness(DrinkTechnique t) => t switch
        {
            DrinkTechnique.Layer => 0.95,
            DrinkTechnique.Build => 0.6,
            DrinkTechnique.Steep => 0.5,
            _ => 0.4,
        };

        public static FluidPart Part(string id, double ml)
        {
            if (id == WaterId || id == TeaId) return new FluidPart(id, ml, 1.0, 0.0);
            var def = DrinkIngredients.Get(id);
            return new FluidPart(id, ml, def != null ? def.density : 1.0, def != null ? Mathf.Clamp01(def.viscosity) : 0.0);
        }

        /// <summary>편집 중 그릇 상태: 투입 순서대로 기법의 부드러움으로 붓고 잠시 둔다(무거운 재료는 가라앉기 시작).</summary>
        public static FluidColumn Build(DrinkRecipe r, double settleSeconds = 1.5)
        {
            var col = new FluidColumn();
            if (r?.steps == null) return col;
            double g = Gentleness(r.Technique);
            if (r.Kind == DrinkKind.Tea)
            {
                foreach (var s in r.steps)
                {
                    var def = DrinkIngredients.Get(s?.id);
                    if (def != null && def.form == IngredientForm.Liquid && s.amount > 0) col.Pour(Part(def.id, s.amount / 10.0), g);
                }
                return col; // 물은 우릴 때 붓는다
            }
            foreach (var s in r.steps)
            {
                var def = DrinkIngredients.Get(s?.id);
                if (def != null && def.form == IngredientForm.Liquid && s.amount > 0) col.Pour(Part(def.id, s.amount / 10.0), g);
            }
            if (r.water > 0) col.Pour(Part(WaterId, r.water / 10.0), g);
            if (settleSeconds > 0 && !col.IsEmpty) col.Step(settleSeconds);
            return col;
        }

        /// <summary>완성(마실 그릇) 최종 상태. 셰이킹·스터링은 균일, 빌드는 붓는 순서 + 침강, 레이어링은 부드러운 층 + 침강, 차는 우린 차 + 첨가.</summary>
        public static FluidColumn Final(DrinkRecipe r)
        {
            switch (r.Technique)
            {
                case DrinkTechnique.Shake:
                case DrinkTechnique.Stir:
                {
                    var col = Build(r, 0);
                    if (!col.IsEmpty) col.Mix(r.Technique == DrinkTechnique.Shake ? 1.0 : 0.6, 6);
                    return Poured(col);
                }
                case DrinkTechnique.Steep:
                {
                    var col = new FluidColumn();
                    col.Pour(Part(TeaId, r.water / 10.0), 0.5);
                    foreach (var s in r.steps)
                    {
                        var def = DrinkIngredients.Get(s?.id);
                        if (def != null && def.form == IngredientForm.Liquid && s.amount > 0) col.Pour(Part(def.id, s.amount / 10.0), 0.5);
                    }
                    col.Mix(0.6, 3); // 다관에서 우리며 섞이고 찻잔으로 따른다
                    return Poured(col);
                }
                default:
                {
                    var col = Build(r, 0);
                    if (!col.IsEmpty) col.Step(3.0);
                    return col;
                }
            }
        }

        /// <summary>다른 그릇으로 전부 따른 결과(균일 혼합은 그대로 균일).</summary>
        static FluidColumn Poured(FluidColumn src)
        {
            var dst = new FluidColumn();
            if (!src.IsEmpty) DrinkFluidPhysics.Transfer(src, dst, src.TotalVolume, 0.5);
            return dst;
        }

        /// <summary>기둥이 배합의 재료량과 같은지(0.05mL 허용). 다르면(직접 입력·빼기) 다시 만든다.</summary>
        public static bool Matches(FluidColumn col, DrinkRecipe r)
        {
            if (col == null || r?.steps == null) return false;
            double expected = 0;
            foreach (var s in r.steps)
            {
                var def = DrinkIngredients.Get(s?.id);
                if (def == null || def.form != IngredientForm.Liquid || s.amount <= 0) continue;
                expected += s.amount / 10.0;
                if (Math.Abs(col.ComponentVolume(def.id) - s.amount / 10.0) > 0.05) return false;
            }
            if (r.Kind == DrinkKind.Cocktail)
            {
                expected += r.water / 10.0;
                if (Math.Abs(col.ComponentVolume(WaterId) - r.water / 10.0) > 0.05) return false;
            }
            return Math.Abs(col.TotalVolume - expected) <= 0.1;
        }

        /// <summary>기둥 층 → 화면 층(색·탁도·밀도). teaColor: 우린 차 성분의 색(배합의 찻잎에서).</summary>
        public static List<DrinkLayer> Layers(FluidColumn col, DrinkRecipe r)
        {
            var list = new List<DrinkLayer>();
            if (col == null || col.IsEmpty) return list;
            Color? tea = r != null && r.Kind == DrinkKind.Tea ? TeaColor(r) : (Color?)null;
            foreach (var layer in col.GetLayers())
            {
                var c = DrinkCalculator.ColorOfParts(layer.parts, tea, out float turb);
                list.Add(new DrinkLayer { color = c, turbidity = turb, volume = (float)layer.Volume, density = (float)layer.Density, label = "" });
            }
            return list;
        }

        /// <summary>찻잎만 우린 차 색(첨가 액체 제외).</summary>
        public static Color TeaColor(DrinkRecipe r)
        {
            var leaves = r.Clone();
            leaves.steps.RemoveAll(s => DrinkIngredients.Get(s?.id)?.form != IngredientForm.Leaf);
            return DrinkCalculator.Look(leaves).color;
        }
    }
}
