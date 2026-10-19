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
        public static List<DrinkLayer> Layers(FluidColumn col, DrinkRecipe r) => Layers(col, r, 0);

        /// <summary>장부 그릇 층 → 화면 층. teaStrength(0~1)만큼 물 성분을 우린 차로 칠한다(흡광은 양에 비례: 물 v → 차 v·s + 물 v·(1−s)).</summary>
        public static List<DrinkLayer> Layers(FluidColumn col, DrinkRecipe r, double teaStrength)
        {
            var list = new List<DrinkLayer>();
            if (col == null || col.IsEmpty) return list;
            Color? tea = r != null && r.Kind == DrinkKind.Tea ? TeaColor(r) : (Color?)null;
            double s = Math.Max(0, Math.Min(1, teaStrength));
            foreach (var layer in DisplayBands(col, tea, s))
            {
                var c = DrinkCalculator.ColorOfParts(TeaMapped(layer.parts, tea, s), tea, out float turb);
                list.Add(new DrinkLayer { color = c, turbidity = turb, volume = (float)layer.Volume, density = (float)layer.Density, label = "" });
            }
            return list;
        }

        /// <summary>화면 띠 = 셀 몇 개씩 고정 분할(64셀 → 16띠). 자료에 따라 띠를 고르지 않아 시간이 지나도 띠 경계가 튀지 않는다.</summary>
        public const int CellsPerBand = 4;
        public const int MaxDisplayBands = FluidColumn.DefaultCells / CellsPerBand;
        /// <summary>이웃 띠 색 차이(채널 최대)가 이보다 작으면(눈으로 구별 불가) 한 띠로 그린다.</summary>
        public const float BandMergeColor = 0.012f;

        /// <summary>
        /// 표시용 띠: 기둥 셀을 아래부터 CellsPerBand개씩 고정으로 묶어 각 띠의 실제 성분으로 색을 계산하고(성분이 이어서 변하면 색도 이어서 변함),
        /// 눈으로 구별되지 않는 이웃 띠만 합친다. 성분을 그대로 더하므로 양·질량·밀도가 보존된다. 계산 상태(FluidColumn)는 바꾸지 않는다.
        /// 한계: 층 경계 위치 해상도 = 전체 부피의 1/MaxDisplayBands.
        /// </summary>
        static List<FluidLayer> DisplayBands(FluidColumn col, Color? tea, double strength)
        {
            var bands = new List<FluidLayer>();
            var colors = new List<Color>();
            for (int start = 0; start < col.CellCount; start += CellsPerBand)
            {
                var parts = new List<FluidPart>();
                for (int i = start; i < Mathf.Min(col.CellCount, start + CellsPerBand); i++) parts.AddRange(col.CellParts(i));
                var layer = FluidLayer.From(parts);
                if (layer.Volume <= 0) continue;
                var c = BandColor(layer, tea, strength);
                int last = bands.Count - 1;
                if (last >= 0 && ColorDistance(colors[last], c) < BandMergeColor)
                {
                    var merged = new List<FluidPart>(bands[last].parts);
                    merged.AddRange(layer.parts);
                    bands[last] = FluidLayer.From(merged);
                    colors[last] = BandColor(bands[last], tea, strength);
                }
                else { bands.Add(layer); colors.Add(c); }
            }
            return bands;
        }

        static Color BandColor(FluidLayer l, Color? tea, double s) => DrinkCalculator.ColorOfParts(TeaMapped(l.parts, tea, s), tea, out _);

        static float ColorDistance(Color a, Color b) => Mathf.Max(Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g)), Mathf.Max(Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a)));

        static FluidPart[] TeaMapped(FluidPart[] parts, Color? tea, double s)
        {
            if (s <= 0 || tea == null) return parts;
            var mapped = new List<FluidPart>();
            foreach (var p in parts)
            {
                if (p.id != WaterId) { mapped.Add(p); continue; }
                mapped.Add(new FluidPart(TeaId, p.volumeMl * s, p.density));
                if (s < 1) mapped.Add(new FluidPart(WaterId, p.volumeMl * (1 - s), p.density));
            }
            return mapped.ToArray();
        }

        /// <summary>
        /// 표시용 시간 평균 기둥(그릇 하나당 하나). 1차원 기둥의 밀도 역전 침강은 셀 덩어리를 통째로 바꾸며 진행해(가로로 나란한 손가락 흐름의 근사)
        /// 맨 위·경계 셀 성분이 프레임마다 번갈아 바뀐다. 실제 잔은 그 손가락들이 폭 방향으로 평균되어 보이므로, 셀별 성분 부피를 τ초 지수 평균하고
        /// 성분별 합계를 현재 기둥과 정확히 같게 다시 맞춘 기둥으로 띠를 만든다(양·질량 보존, 계산 상태 불변). 다른 기둥으로 바뀌면 바로 맞춘다.
        /// </summary>
        public sealed class DisplaySmoother
        {
            public const double Tau = 0.2;
            FluidColumn source;
            readonly List<Dictionary<string, FluidPart>> avg = new List<Dictionary<string, FluidPart>>();
            int lastFrame = -1;
            double lastTime = -1;
            FluidColumn display;

            /// <summary>이 프레임의 표시 기둥(now = 실제 시간 초). 같은 프레임 두 번째 호출은 갱신 없이 재사용.</summary>
            public FluidColumn Get(FluidColumn col, double now, int frame)
            {
                double dt = lastTime < 0 ? 0 : now - lastTime;
                if (col == null || col.IsEmpty) { source = col; avg.Clear(); display = col; return col; }
                if (frame == lastFrame && ReferenceEquals(col, source) && display != null) return display;
                lastFrame = frame;
                lastTime = now;
                bool snap = !ReferenceEquals(col, source) || avg.Count != col.CellCount;
                source = col;
                double a = snap ? 1.0 : 1.0 - Math.Exp(-Math.Max(0, dt) / Tau);
                if (snap) { avg.Clear(); for (int i = 0; i < col.CellCount; i++) avg.Add(new Dictionary<string, FluidPart>()); }
                var totals = new Dictionary<string, double>();
                for (int i = 0; i < col.CellCount; i++)
                {
                    var cur = col.CellParts(i);
                    var cell = avg[i];
                    var keys = new List<string>(cell.Keys);
                    foreach (var p in cur) if (!cell.ContainsKey(p.id)) { cell[p.id] = new FluidPart(p.id, 0, p.density, p.viscosity, p.miscibilityGroup); keys.Add(p.id); }
                    foreach (var id in keys)
                    {
                        double target = 0; FluidPart proto = cell[id];
                        foreach (var p in cur) if (p.id == id) { target = p.volumeMl; proto = p; }
                        var old = cell[id];
                        cell[id] = new FluidPart(id, old.volumeMl + (target - old.volumeMl) * a, proto.density, proto.viscosity, proto.miscibilityGroup);
                    }
                    foreach (var p in cur) totals[p.id] = (totals.TryGetValue(p.id, out var t) ? t : 0) + p.volumeMl;
                }
                // 성분별 합계를 현재와 같게(평균이 양을 늦게 따라가지 않게)
                var sums = new Dictionary<string, double>();
                foreach (var cell in avg) foreach (var kv in cell) sums[kv.Key] = (sums.TryGetValue(kv.Key, out var t) ? t : 0) + kv.Value.volumeMl;
                display = new FluidColumn(col.CellCount);
                foreach (var cell in avg)
                {
                    var parts = new List<FluidPart>();
                    foreach (var kv in cell)
                    {
                        double want = totals.TryGetValue(kv.Key, out var t) ? t : 0;
                        double have = sums.TryGetValue(kv.Key, out var h) ? h : 0;
                        double v = have > 1e-12 ? kv.Value.volumeMl * want / have : 0;
                        if (v > 1e-9) parts.Add(new FluidPart(kv.Key, v, kv.Value.density, kv.Value.viscosity, kv.Value.miscibilityGroup));
                    }
                    if (parts.Count > 0) display.Pour(parts, 1.0);
                }
                return display;
            }
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
