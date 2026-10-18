using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>계산된 음료 버프(비율). 치명타는 확률 증가량(0.05 = +5%p).</summary>
    public sealed class DrinkBuff
    {
        readonly float[] values = new float[6];

        public float this[DrinkStat s]
        {
            get => values[(int)s];
            internal set => values[(int)s] = value;
        }

        public bool IsEmpty
        {
            get
            {
                foreach (var v in values) if (v > 0f) return false;
                return true;
            }
        }

        public IEnumerable<DrinkStat> Active()
        {
            for (int i = 0; i < values.Length; i++) if (values[i] > 0f) yield return (DrinkStat)i;
        }

        /// <summary>표시만 반올림(소수 1자리 %). 배합량·계산값은 그대로 둔다.</summary>
        public static string Pct(DrinkStat s, float v) => s == DrinkStat.CritChance ? $"+{v * 100f:0.0}%p"
            : s == DrinkStat.CooldownReduction ? $"-{v * 100f:0.0}%" : $"+{v * 100f:0.0}%";

        public string Describe(string separator = " · ")
        {
            var parts = new List<string>();
            foreach (var s in Active()) parts.Add($"{DrinkRules.StatName(s)} {Pct(s, this[s])}");
            return parts.Count == 0 ? "효과 없음" : string.Join(separator, parts);
        }
    }

    /// <summary>한 층(레이어링/빌드 표시용). 아래(0)부터 위로.</summary>
    public struct DrinkLayer
    {
        public Color color;     // sRGB, a = 불투명도
        public float turbidity;
        public float volume;    // mL
        public string label;
        /// <summary>층 평균 밀도(g/mL). 얼음 부력·층 순서에 쓴다. 0이면 1.0으로 본다.</summary>
        public float density;
    }

    /// <summary>색 계산 결과. color는 sRGB(알파=불투명도). 동일 배합 → 항상 동일 값.</summary>
    public sealed class DrinkLook
    {
        public Color color;
        public float turbidity;
        public float totalVolume;
        public readonly List<DrinkLayer> layers = new List<DrinkLayer>();
        /// <summary>레이어링에서 서로 섞이지 않은 층 경계 수.</summary>
        public int boundaries;
    }

    /// <summary>
    /// 69 버프·색 계산기 — 실시간 미리보기와 실제 런 적용이 같은 함수를 쓴다.
    /// 버프: 재료별 정규화 기여 q/(q+k)(액체 k=15mL, 찻잎 k=2g) × 계수 × 효과 상한 → 효과별 합 → 기법 배율 → 상한 제한.
    /// 색: 선형 공간 Beer–Lambert. 재료 고유 흡광 = -ln(선형 색), 유효 농도 = min(1, 부피비 × 착색력)으로 가중 합 → 투과색.
    /// 순수 재료(부피비 1)는 정확히 자기 색, 무색 술/물은 희석, 착색력 높은 시럽·리큐어는 소량도 크게 반영.
    /// </summary>
    public static class DrinkCalculator
    {
        public static float Multiplier(DrinkTechnique t, DrinkStat s, int distinctEffects)
        {
            switch (t)
            {
                case DrinkTechnique.Shake: return s == DrinkStat.CritChance ? 1.15f : 0.95f;
                case DrinkTechnique.Stir: return s == DrinkStat.Attack || s == DrinkStat.AttackSpeed ? 1.08f : 0.95f;
                case DrinkTechnique.Layer: return distinctEffects >= 2 ? 1.05f : 0.90f;
                default: return 1f;
            }
        }

        public static float Normalized(DrinkIngredientDef def, int tenths)
        {
            if (tenths <= 0) return 0f;
            float k = def.form == IngredientForm.Leaf ? DrinkRules.LeafK : DrinkRules.LiquidK;
            return tenths / (tenths + k);
        }

        /// <summary>버프 계산. 유효하지 않은 재료(종류 부적합·알 수 없음·0 이하)는 기여 0. 물/얼음은 기여 없음.</summary>
        public static DrinkBuff Buff(DrinkRecipe r)
        {
            var buff = new DrinkBuff();
            if (r?.steps == null) return buff;
            var kind = r.Kind;
            if (kind != DrinkKind.Cocktail && kind != DrinkKind.Tea) return buff;
            var raw = new float[6];
            var seen = new HashSet<string>();
            foreach (var s in r.steps)
            {
                var def = s == null ? null : DrinkIngredients.Get(s.id);
                if (def == null || s.amount <= 0 || !def.UsableIn(kind) || !seen.Add(def.id)) continue;
                raw[(int)def.effect] += Normalized(def, s.amount) * def.coefficient * DrinkRules.Cap(def.effect);
            }
            int distinct = 0;
            foreach (var v in raw) if (v > 0f) distinct++;
            var tech = DrinkRules.AllowedTechnique(kind, r.Technique) ? r.Technique : DrinkRules.DefaultTechnique(kind);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] <= 0f) continue;
                var stat = (DrinkStat)i;
                buff[stat] = Mathf.Min(DrinkRules.Cap(stat), raw[i] * Multiplier(tech, stat, distinct));
            }
            return buff;
        }

        // ───── 색 ─────

        static Vector3 Absorb(Color srgb, float strength)
        {
            var lin = srgb.linear;
            return new Vector3(-Mathf.Log(Mathf.Max(0.02f, lin.r)), -Mathf.Log(Mathf.Max(0.02f, lin.g)), -Mathf.Log(Mathf.Max(0.02f, lin.b))) * strength;
        }

        struct Part
        {
            public Vector3 absorb;  // 순수 재료 흡광 -ln(선형 색)
            public float tint;      // 착색력(유효 농도 배율)
            public float volume;
            public float turbidity; // 탁도 × 부피
            public float density;
            public string label;
        }

        /// <summary>재료 하나(또는 물/우린 차)를 색 계산 단위로 바꾼다.</summary>
        static List<Part> Parts(DrinkRecipe r)
        {
            var parts = new List<Part>();
            if (r?.steps == null) return parts;
            var kind = r.Kind;
            if (kind == DrinkKind.Tea)
            {
                // 차: 물에 찻잎이 우러난 한 액체. 우림 농도 = 찻잎 g / 물 100mL 기준 3g.
                float water = Mathf.Max(1f, r.water / 10f);
                var absorb = Vector3.zero;
                float turb = 0f;
                foreach (var s in r.steps)
                {
                    var def = DrinkIngredients.Get(s?.id);
                    if (def == null || s.amount <= 0 || def.form != IngredientForm.Leaf || !def.UsableIn(kind)) continue;
                    // 우림 농도: 물 100mL당 3g이면 기준 우림 색(착색력 반영, 최대 1)
                    float strength = Mathf.Min(1f, (s.amount / 10f) / (water / 100f) / 3f * def.tint);
                    absorb += Absorb(def.brewColor, strength);
                    turb += def.turbidity * strength * water;
                }
                parts.Add(new Part { absorb = absorb, tint = 1f, volume = water, turbidity = turb, density = 1f, label = "우린 차" });
                foreach (var s in r.steps)
                {
                    var def = DrinkIngredients.Get(s?.id);
                    if (def == null || s.amount <= 0 || def.form != IngredientForm.Liquid || !def.UsableIn(kind)) continue;
                    float v = s.amount / 10f;
                    parts.Add(new Part { absorb = Absorb(def.color, 1f), tint = def.tint, volume = v, turbidity = def.turbidity * v, density = def.density, label = def.name });
                }
                return parts;
            }
            foreach (var s in r.steps)
            {
                var def = DrinkIngredients.Get(s?.id);
                if (def == null || s.amount <= 0 || def.form != IngredientForm.Liquid || !def.UsableIn(kind)) continue;
                float v = s.amount / 10f;
                parts.Add(new Part { absorb = Absorb(def.color, 1f), tint = def.tint, volume = v, turbidity = def.turbidity * v, density = def.density, label = def.name });
            }
            if (r.water > 0)
                parts.Add(new Part { absorb = Vector3.zero, tint = 0f, volume = r.water / 10f, turbidity = 0f, density = 1f, label = "물" });
            return parts;
        }

        /// <summary>부피 가중 흡광 합을 유효 농도 가중으로 바꾼다: Σ min(1, v_i/V × tint_i) × A_i.</summary>
        static Vector3 Concentrate(List<Part> parts, out float volume, out float turb)
        {
            volume = 0f; turb = 0f;
            foreach (var p in parts) { volume += p.volume; turb += p.turbidity; }
            var sum = Vector3.zero;
            if (volume <= 0f) return sum;
            foreach (var p in parts)
            {
                if (p.volume <= 0f) continue;
                float c = Mathf.Min(1f, p.volume / volume * p.tint);
                sum += p.absorb * c;
            }
            return sum;
        }

        static Color Resolve(Vector3 absorbSum, float volume, float turbSum, out float turbidity)
        {
            if (volume <= 0f) { turbidity = 0f; return new Color(1f, 1f, 1f, 0f); }
            var a = absorbSum; // 이미 유효 농도로 가중된 흡광 합(부피로 나누지 않음)
            var lin = new Color(Mathf.Exp(-a.x), Mathf.Exp(-a.y), Mathf.Exp(-a.z), 1f);
            turbidity = Mathf.Clamp01(turbSum / volume);
            var c = lin.gamma;
            // 탁도: 빛 산란으로 밝은 쪽으로 뿌옇게 + 불투명도 증가. 맑은 액체는 반투명(배경이 비친다).
            float darkness = 1f - (c.r + c.g + c.b) / 3f;
            c = Color.Lerp(c, new Color(0.97f, 0.96f, 0.9f), turbidity * 0.35f);
            c.a = Mathf.Clamp01(0.32f + darkness * 0.55f + turbidity * 0.5f);
            return c;
        }

        /// <summary>완성 색과 층. 레이어링은 투입 순서대로 쌓되 아래보다 무거운 재료는 가라앉아 밀도 순으로 정렬,
        /// 밀도 차 0.05 미만 이웃은 섞인다. 그 밖의 기법은 한 덩어리(빌드는 표시에서만 확산 그라데이션).</summary>
        public static DrinkLook Look(DrinkRecipe r)
        {
            var look = new DrinkLook();
            var parts = Parts(r);
            var sumA = Concentrate(parts, out float vol, out float turb);
            look.totalVolume = vol;
            look.color = Resolve(sumA, vol, turb, out look.turbidity);
            if (r != null && r.Kind == DrinkKind.Cocktail && r.Technique == DrinkTechnique.Layer && parts.Count > 1)
            {
                // 투입 순서(인덱스) 유지하며 무거운 것이 아래로 — 안정 정렬(밀도 내림차순).
                var order = new List<int>();
                for (int i = 0; i < parts.Count; i++) order.Add(i);
                order.Sort((x, y) =>
                {
                    int c = parts[y].density.CompareTo(parts[x].density);
                    return c != 0 ? c : x.CompareTo(y);
                });
                var groups = new List<List<Part>>();
                float lastDensity = float.NaN;
                foreach (int idx in order)
                {
                    var p = parts[idx];
                    if (groups.Count == 0 || Mathf.Abs(lastDensity - p.density) >= 0.05f) groups.Add(new List<Part>());
                    groups[groups.Count - 1].Add(p);
                    lastDensity = p.density;
                }
                foreach (var g in groups)
                {
                    var names = new List<string>();
                    float mass = 0f;
                    foreach (var p in g) { names.Add(p.label); mass += p.density * p.volume; }
                    var ga = Concentrate(g, out float gv, out float gt);
                    var c = Resolve(ga, gv, gt, out float t);
                    look.layers.Add(new DrinkLayer { color = c, turbidity = t, volume = gv, density = gv > 0f ? mass / gv : 1f, label = string.Join("+", names) });
                }
                look.boundaries = Mathf.Max(0, look.layers.Count - 1);
            }
            else
            {
                float mass = 0f;
                foreach (var p in parts) mass += p.density * p.volume;
                look.layers.Add(new DrinkLayer { color = look.color, turbidity = look.turbidity, volume = vol, density = vol > 0f ? mass / vol : 1f, label = "혼합" });
            }
            return look;
        }

        /// <summary>물리 기둥 한 층의 실제 성분(재료 id·mL)으로 색 계산 — Look과 같은 유효 농도 Beer–Lambert. 물은 희석만, 우린 차는 teaColor.</summary>
        public static Color ColorOfParts(IEnumerable<FluidPart> parts, Color? teaColor, out float turbidity)
        {
            var list = new List<Part>();
            foreach (var fp in parts)
            {
                if (fp.volumeMl <= 0) continue;
                float v = (float)fp.volumeMl;
                if (fp.id == DrinkPhysicsSim.WaterId) { list.Add(new Part { absorb = Vector3.zero, tint = 0f, volume = v, density = 1f }); continue; }
                if (fp.id == DrinkPhysicsSim.TeaId)
                {
                    var tc = teaColor ?? new Color(0.93f, 0.97f, 1f, 1f);
                    list.Add(new Part { absorb = Absorb(tc, 1f), tint = 1f, volume = v, density = 1f });
                    continue;
                }
                var def = DrinkIngredients.Get(fp.id);
                if (def == null) continue;
                list.Add(new Part { absorb = Absorb(def.color, 1f), tint = def.tint, volume = v, turbidity = def.turbidity * v, density = def.density });
            }
            var a = Concentrate(list, out float vol, out float turb);
            return Resolve(a, vol, turb, out turbidity);
        }

        /// <summary>단일 재료 색(지거 안 재료색 표시용).</summary>
        public static Color IngredientLook(DrinkIngredientDef def)
        {
            if (def == null) return Color.clear;
            if (def.form == IngredientForm.Leaf) return def.color;
            return Resolve(Absorb(def.color, 1f), 1f, def.turbidity, out _); // 순수 재료 = 고유 색
        }

        /// <summary>테스트/보고용: 색 → "#RRGGBB/aa".</summary>
        public static string Hex(Color c) =>
            $"#{Mathf.RoundToInt(c.r * 255):X2}{Mathf.RoundToInt(c.g * 255):X2}{Mathf.RoundToInt(c.b * 255):X2}/{Mathf.RoundToInt(c.a * 255):X2}";

        /// <summary>런 스탯에 버프를 한 번 적용한다(GameManager.Initialize). 기존 스탯 상한(PlayerStats의 최종 clamp)은 그대로 유지된다.</summary>
        public static void ApplyTo(PlayerStats stats, DrinkBuff buff)
        {
            if (stats == null || buff == null) return;
            stats.damagePercent += buff[DrinkStat.Attack];
            stats.attackSpeedPercent += buff[DrinkStat.AttackSpeed];
            stats.moveSpeedPercent += buff[DrinkStat.MoveSpeed];
            stats.maxHpFlat += stats.baseMaxHp * buff[DrinkStat.MaxHp];
            stats.drinkCritChance = buff[DrinkStat.CritChance];
            stats.drinkCooldownReduction = buff[DrinkStat.CooldownReduction];
        }
    }
}
