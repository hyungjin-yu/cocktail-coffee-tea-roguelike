using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>계산된 음료 버프(비율). 치명타는 확률 증가량(0.05 = +5%p).</summary>
    public sealed class DrinkBuff
    {
        readonly float[] values = new float[Enum.GetValues(typeof(DrinkStat)).Length];

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

        /// <summary>재료의 기본 효과를 음료 종류별로 분리한다. 공유 재료도 차에서는 차 전용 효과에만 기여한다.</summary>
        public static DrinkStat EffectFor(DrinkKind kind, DrinkIngredientDef def)
        {
            if (kind == DrinkKind.Coffee) return DrinkStat.Range;
            if (kind == DrinkKind.Tea)
            {
                if (def.id == DrinkIngredients.Honey || def.id == DrinkIngredients.BlackTea ||
                    def.id == DrinkIngredients.WhiteTea || def.id == DrinkIngredients.JasmineTea)
                    return DrinkStat.CooldownReduction;
                if (def.id == DrinkIngredients.LemonJuice || def.id == DrinkIngredients.GreenTea ||
                    def.id == DrinkIngredients.LimeJuice || def.id == DrinkIngredients.OolongTea || def.id == DrinkIngredients.ChamomileFlower ||
                    def.id == DrinkIngredients.AnxiTieguanyin || def.id == DrinkIngredients.PuErTea)
                    return DrinkStat.MoveSpeed;
                if (def.id == DrinkIngredients.DarjeelingTea) return DrinkStat.CooldownReduction;
            }
            return def.effect;
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
            if (kind != DrinkKind.Cocktail && kind != DrinkKind.Tea && kind != DrinkKind.Coffee) return buff;
            var raw = new float[Enum.GetValues(typeof(DrinkStat)).Length];
            var seen = new HashSet<string>();
            // 커피 원두(기본·산지)는 한 원두로 합쳐 정규화한다: 두 산지를 섞어도 같은 총량의 한 원두보다 효과가 커지지 않는다.
            int beanTenths = 0, beanKinds = 0;
            float beanCoef = 0f;
            DrinkIngredientDef beanDef = null;
            foreach (var s in r.steps)
            {
                var def = s == null ? null : DrinkIngredients.Get(s.id);
                if (def == null || s.amount <= 0 || !def.UsableIn(kind) || !seen.Add(def.id)) continue;
                if (def.IsCoffeeBean)
                {
                    beanTenths = (int)Math.Min(int.MaxValue, (long)beanTenths + s.amount); // 손상 값 넘침 방지
                    beanCoef += def.coefficient * s.amount;
                    beanDef ??= def;
                    beanKinds++;
                    continue;
                }
                var effect = EffectFor(kind, def);
                raw[(int)effect] += Normalized(def, s.amount) * def.coefficient * DrinkRules.Cap(effect);
            }
            if (beanTenths > 0)
            {
                var effect = EffectFor(kind, beanDef);
                raw[(int)effect] += Normalized(beanDef, beanTenths) * (beanKinds == 1 ? beanDef.coefficient : beanCoef / beanTenths) * DrinkRules.Cap(effect);
            }
            int distinct = 0;
            foreach (var v in raw) if (v > 0f) distinct++;
            var tech = DrinkRules.AllowedTechnique(kind, r.Technique) ? r.Technique : DrinkRules.DefaultTechnique(kind);
            float extraction = kind == DrinkKind.Coffee ? CoffeeBrewing.Strength01(r.coffeeMethod, r.CoffeeDose(), r.water) : 1f;
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] <= 0f) continue;
                var stat = (DrinkStat)i;
                buff[stat] = Mathf.Min(DrinkRules.Cap(stat), raw[i] * Multiplier(tech, stat, distinct) * extraction);
            }
            return buff;
        }

        /// <summary>72 §5 제조 전문 적용: 직원 전문의 종류·기법이 맞으면 그 항목만 ×배율(항목 상한 유지). 없던 효과는 만들지 않는다.</summary>
        public static DrinkBuff Buff(DrinkRecipe r, string specialtyId)
        {
            var buff = Buff(r);
            var st = DrinkResearch.StaffById(specialtyId);
            if (st == null || !SpecialtyApplies(st, r) || !Enum.TryParse<DrinkStat>(st.stat, out var stat)) return buff;
            if (buff[stat] > 0f) buff[stat] = Mathf.Min(DrinkRules.Cap(stat), buff[stat] * (float)st.multiplier);
            return buff;
        }

        public static bool SpecialtyApplies(ResearchStaff st, DrinkRecipe r) =>
            st != null && r != null && st.kind == r.Kind.ToString() && (st.technique == null || st.technique == r.Technique.ToString());

        /// <summary>72 §4 기교4: 양수 버프만 ×k, 항목별 음료 상한 유지(새 사본).</summary>
        public static DrinkBuff ScalePositive(DrinkBuff b, float k)
        {
            var r = new DrinkBuff();
            if (b == null) return r;
            foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat)))
                r[s] = b[s] > 0f ? Mathf.Min(DrinkRules.Cap(s), b[s] * k) : b[s];
            return r;
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
            if (kind == DrinkKind.Coffee)
            {
                // 추출수는 원두/물 비율에 따른 커피 액체다. 도구 시뮬레이션·완성 잔·판매 색 분류가 같은 색을 보도록 한다.
                float coffeeMl = Mathf.Max(0f, r.water / 10f);
                if (coffeeMl > 0f)
                {
                    var coffee = CoffeeBrewing.BrewColor(r);
                    parts.Add(new Part { absorb = Absorb(coffee, 1f), tint = 1f, volume = coffeeMl, turbidity = 0.06f * coffeeMl, density = 1.0f, label = "추출 커피" });
                }
                foreach (var s in r.steps)
                {
                    var def = DrinkIngredients.Get(s?.id);
                    if (def == null || s.amount <= 0 || def.form != IngredientForm.Liquid || !def.UsableIn(kind)) continue;
                    float v = s.amount / 10f;
                    parts.Add(new Part { absorb = Absorb(def.color, 1f), tint = def.tint, volume = v, turbidity = def.turbidity * v, density = def.density, label = def.name });
                }
                return parts;
            }
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
            // 같은 성분이 여러 조각(띠로 묶은 셀마다 한 조각)으로 와도 먼저 합친다: 착색 농도는 상한(1)이 있어
            // 조각별로 따로 더하면 같은 조성인데 조각 수만큼 진해진다. 색은 조성(성분별 부피)만으로 정해진다.
            var merged = new List<FluidPart>();
            var at = new Dictionary<string, int>();
            foreach (var fp in parts)
            {
                if (fp.volumeMl <= 0) continue;
                if (at.TryGetValue(fp.id, out int i)) { var m = merged[i]; m.volumeMl += fp.volumeMl; merged[i] = m; }
                else { at[fp.id] = merged.Count; merged.Add(fp); }
            }
            var list = new List<Part>();
            foreach (var fp in merged)
            {
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
            // 순수 재료 = 혼합 계산(ColorOfParts)에 그 재료만 넣은 색 — 지거·줄기·누적 혼합이 같은 색 모델을 쓴다(73).
            return ColorOfParts(new[] { DrinkPhysicsSim.Part(def.id, 10.0) }, null, out _);
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
            stats.rangePercent += buff[DrinkStat.Range];
        }
    }
}
