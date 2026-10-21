using System;
using System.Collections.Generic;
using System.Text;

namespace CCT
{
    /// <summary>배합 한 줄(투입 순서 = 리스트 순서). amount는 정수 1 = 0.1mL/0.1g.</summary>
    [Serializable]
    public class DrinkStep
    {
        public string id;
        public int amount;

        public DrinkStep() { }
        public DrinkStep(string id, int amount) { this.id = id; this.amount = amount; }
    }

    /// <summary>
    /// 69 레시피/배합. 저장 필드: 사용자 이름, 음료 종류, 재료 ID·정수량(단위는 재료 정의), 투입 순서, 기법, 물/얼음, 커피 추출 도구, 스키마 버전.
    /// 버프·색은 저장하지 않는다 — 항상 현재 밸런스로 DrinkCalculator가 재계산한다.
    /// </summary>
    [Serializable]
    public class DrinkRecipe
    {
        public const int CurrentSchema = 1;

        public int schemaVersion = CurrentSchema;
        public string name = "";
        /// <summary>DrinkKind 정수(1 칵테일, 2 차).</summary>
        public int kind = (int)DrinkKind.Cocktail;
        /// <summary>DrinkTechnique 정수.</summary>
        public int technique = (int)DrinkTechnique.Shake;
        public List<DrinkStep> steps = new List<DrinkStep>();
        /// <summary>물(0.1mL). 차는 항상 TeaWaterFixed, 칵테일은 희석수.</summary>
        public int water;
        public int ice;
        /// <summary>커피 추출 도구. 칵테일·차는 0(정의되지 않은 값). 이전 저장에 없던 필드라 기존 레시피는 0으로 읽힌다(JsonUtility는 정수로 저장).</summary>
        public CoffeeBrewMethod coffeeMethod;

        public DrinkKind Kind => (DrinkKind)kind;
        public DrinkTechnique Technique => (DrinkTechnique)technique;

        public DrinkRecipe Clone()
        {
            var copy = (DrinkRecipe)MemberwiseClone();
            copy.steps = new List<DrinkStep>();
            if (steps != null) foreach (var s in steps) if (s != null) copy.steps.Add(new DrinkStep(s.id, s.amount));
            return copy;
        }

        public static DrinkRecipe NewDraft(DrinkKind kind) => NewDraft(kind, CoffeeBrewMethod.EspressoMachine);

        /// <summary>새 배합. 커피는 고른 추출 도구의 기본 추출량으로 시작한다(원두는 비어 있음).</summary>
        public static DrinkRecipe NewDraft(DrinkKind kind, CoffeeBrewMethod method)
        {
            bool coffee = kind == DrinkKind.Coffee;
            return new DrinkRecipe
            {
                kind = (int)kind,
                technique = (int)DrinkRules.DefaultTechnique(kind),
                water = kind == DrinkKind.Tea ? DrinkRules.TeaWaterFixed : coffee ? CoffeeBrewing.DefaultWaterTenths(method) : 0,
                ice = kind == DrinkKind.Cocktail ? 2 : 0,
                coffeeMethod = coffee ? method : 0,
            };
        }

        /// <summary>커피 추출 도구 변경: 물(추출량)은 새 도구의 기본값, 원두 양은 그대로(범위 밖이면 검증이 알린다).</summary>
        public void SetCoffeeMethod(CoffeeBrewMethod m)
        {
            if (Kind != DrinkKind.Coffee || !CoffeeBrewing.IsDefined(m)) return;
            coffeeMethod = m;
            water = CoffeeBrewing.DefaultWaterTenths(m);
        }

        public int AmountOf(string id)
        {
            if (steps != null) foreach (var s in steps) if (s != null && s.id == id) return s.amount;
            return 0;
        }

        /// <summary>재료 양을 절대값으로 설정(0이면 제거). 처음 넣은 재료는 순서 끝에 붙는다. 재고는 건드리지 않는다.</summary>
        public void SetAmount(string id, int amount)
        {
            steps ??= new List<DrinkStep>();
            steps.RemoveAll(s => s == null);
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].id != id) continue;
                if (amount <= 0) steps.RemoveAt(i);
                else steps[i].amount = amount;
                return;
            }
            if (amount > 0) steps.Add(new DrinkStep(id, amount));
        }

        public void Remove(string id) => SetAmount(id, 0);

        /// <summary>재료 총량(액체·찻잎 분리, 물 제외). null 목록/줄은 건너뛰고, 음수는 0, 합은 long으로 더해 int 범위로 자른다(넘침으로 한도 우회 방지).</summary>
        public int LiquidTotal() => Total(IngredientForm.Liquid);
        public int LeafTotal() => Total(IngredientForm.Leaf);

        int Total(IngredientForm form)
        {
            if (steps == null) return 0;
            long sum = 0;
            foreach (var s in steps)
            {
                var def = s == null ? null : DrinkIngredients.Get(s.id);
                if (def != null && def.form == form) sum += Math.Max(0, s.amount);
            }
            return (int)Math.Min(int.MaxValue, sum);
        }

        /// <summary>같은 레시피 판정(이름 제외, 순서 포함).</summary>
        public bool SameMix(DrinkRecipe other)
        {
            if (other == null || other.kind != kind || other.technique != technique || other.water != water || other.ice != ice || other.coffeeMethod != coffeeMethod) return false;
            if (other.steps == null || steps == null || other.steps.Count != steps.Count) return false;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i] == null || other.steps[i] == null || steps[i].id != other.steps[i].id || steps[i].amount != other.steps[i].amount) return false;
            return true;
        }
    }

    /// <summary>69 배합 검증. 제조 확정·레시피 저장·공유 코드 가져오기가 같은 규칙을 쓴다.</summary>
    public static class DrinkValidator
    {
        public sealed class Result
        {
            public readonly List<string> Errors = new List<string>();
            public bool Ok => Errors.Count == 0;
            public string First => Errors.Count > 0 ? Errors[0] : null;
        }

        /// <summary>배합 자체의 규칙(재고 무관). requireComplete=false면 빈 배합/필수 재료 누락은 오류로 보지 않는다(편집 중 미리보기).</summary>
        public static Result Check(DrinkRecipe r, bool requireComplete = true)
        {
            var res = new Result();
            if (r == null) { res.Errors.Add("배합이 없습니다."); return res; }
            if (r.schemaVersion < 1 || r.schemaVersion > DrinkRecipe.CurrentSchema) res.Errors.Add("지원하지 않는 레시피 버전입니다.");
            if (r.kind != (int)DrinkKind.Cocktail && r.kind != (int)DrinkKind.Tea && r.kind != (int)DrinkKind.Coffee) { res.Errors.Add("알 수 없는 음료 종류입니다."); return res; }
            var kind = r.Kind;
            if (!Enum.IsDefined(typeof(DrinkTechnique), r.technique) || !DrinkRules.AllowedTechnique(kind, r.Technique))
                res.Errors.Add(kind == DrinkKind.Tea ? "차는 우림 기법만 쓸 수 있습니다." : kind == DrinkKind.Coffee ? "커피는 추출 기법만 쓸 수 있습니다." : "칵테일에 쓸 수 없는 기법입니다.");
            if (kind == DrinkKind.Coffee && !CoffeeBrewing.IsDefined(r.coffeeMethod)) { res.Errors.Add("알 수 없는 추출 도구입니다."); return res; }
            if (r.steps == null) { res.Errors.Add("재료 목록이 없습니다."); return res; }
            if (r.steps.Count > DrinkRules.MaxSteps) { res.Errors.Add($"재료는 최대 {DrinkRules.MaxSteps}종입니다."); return res; }

            var seen = new HashSet<string>();
            bool hasBase = false, hasLeaf = false, hasBean = false;
            foreach (var s in r.steps)
            {
                if (s == null) { res.Errors.Add("비어 있는 재료 줄이 있습니다."); continue; }
                var def = DrinkIngredients.Get(s.id);
                if (def == null) { res.Errors.Add("알 수 없는 재료가 있습니다."); continue; }
                if (!seen.Add(def.id)) res.Errors.Add($"{def.name}이(가) 중복되어 있습니다.");
                if (s.amount <= 0) res.Errors.Add($"{def.name}의 양이 0 이하입니다.");
                else if (s.amount > DrinkRules.StepMax) res.Errors.Add($"{def.name}의 양이 너무 많습니다.");
                if (!def.UsableIn(kind)) res.Errors.Add($"{def.name}은(는) {DrinkRules.KindName(kind)}에 쓸 수 없습니다.");
                if (def.baseSpirit) hasBase = true;
                if (def.id == DrinkIngredients.CoffeeBean) hasBean = true;
                if (def.form == IngredientForm.Leaf) hasLeaf = true;
            }
            if (r.ice < 0 || r.ice > DrinkRules.IceMax) res.Errors.Add($"얼음은 0~{DrinkRules.IceMax}개입니다.");

            int liquid = r.LiquidTotal(), leaf = r.LeafTotal();
            if (kind == DrinkKind.Cocktail)
            {
                if (r.water < 0 || r.water > DrinkRules.CocktailWaterMax) res.Errors.Add($"희석수는 0~{DrinkRules.Format(DrinkRules.CocktailWaterMax)}mL입니다.");
                if ((long)liquid + Math.Max(0, r.water) > DrinkRules.CocktailLiquidMax)
                    res.Errors.Add($"칵테일 액체 합이 {DrinkRules.Format(DrinkRules.CocktailLiquidMax)}mL를 넘습니다.");
                if (r.ice > 0 && r.Technique == DrinkTechnique.Layer) { /* 허용: 층 위에 얼음 없음은 표시만 */ }
                if (requireComplete && !hasBase) res.Errors.Add("칵테일은 베이스 술(진·럼)이 1종 이상 필요합니다.");
            }
            else if (kind == DrinkKind.Coffee)
            {
                var m = r.coffeeMethod;
                int wMin = CoffeeBrewing.WaterMinTenths(m), wMax = CoffeeBrewing.WaterMaxTenths(m);
                if (r.water < wMin || r.water > wMax) res.Errors.Add($"{CoffeeBrewing.DisplayName(m)} 추출량은 {DrinkRules.Format(wMin)}~{DrinkRules.Format(wMax)}mL입니다.");
                if (r.ice != 0) res.Errors.Add("커피에는 얼음을 넣지 않습니다.");
                if (liquid > DrinkRules.CoffeeAdditiveMax) res.Errors.Add($"커피 첨가 액체는 최대 {DrinkRules.Format(DrinkRules.CoffeeAdditiveMax)}mL입니다.");
                int dMin = CoffeeBrewing.DoseMinTenths(m), dMax = CoffeeBrewing.DoseMaxTenths(m);
                if (leaf > dMax) res.Errors.Add($"{CoffeeBrewing.DisplayName(m)} 원두는 최대 {DrinkRules.Format(dMax)}g입니다.");
                if (requireComplete && (!hasBean || leaf < dMin)) res.Errors.Add($"{CoffeeBrewing.DisplayName(m)}에는 원두가 {DrinkRules.Format(dMin)}g 이상 필요합니다.");
            }
            else
            {
                if (r.water != DrinkRules.TeaWaterFixed) res.Errors.Add($"차의 물은 {DrinkRules.Format(DrinkRules.TeaWaterFixed)}mL로 고정입니다.");
                if (liquid > DrinkRules.TeaAdditiveMax) res.Errors.Add($"차 첨가 액체는 최대 {DrinkRules.Format(DrinkRules.TeaAdditiveMax)}mL입니다.");
                if (leaf > DrinkRules.TeaLeafMax) res.Errors.Add($"찻잎은 최대 {DrinkRules.Format(DrinkRules.TeaLeafMax)}g입니다.");
                if (requireComplete && (!hasLeaf || leaf < DrinkRules.TeaLeafMin)) res.Errors.Add("차는 찻잎이 1종 이상(0.1g 이상) 필요합니다.");
            }
            float fill = DrinkRules.ServeFillMl(r), room = DrinkRules.ServeCapacityMl(r) * DrinkRules.ServeFillMax;
            if (fill > room + 0.001f)
                res.Errors.Add($"잔에 다 들어가지 않습니다(액체+얼음 {fill:0.#}mL / 최대 {room:0.#}mL). 재료나 얼음을 줄여 주세요.");
            if (requireComplete && r.steps.Count == 0) res.Errors.Add("재료를 넣어 주세요.");
            if (r.name != null && r.name.Length > DrinkRules.RecipeNameMax) res.Errors.Add($"이름은 {DrinkRules.RecipeNameMax}자 이하입니다.");
            return res;
        }

        /// <summary>재고 부족 목록(재료 id → 부족량). 비어 있으면 제조 가능.</summary>
        public static Dictionary<string, int> Shortages(DrinkRecipe r, Func<string, int> owned)
        {
            var map = new Dictionary<string, int>();
            if (r?.steps == null) return map;
            foreach (var s in r.steps)
            {
                if (s == null || s.amount <= 0 || s.id == null) continue;
                int have = owned(s.id);
                if (have < s.amount) map[s.id] = s.amount - have;
            }
            return map;
        }

        /// <summary>표시용 이름 정리: 제어 문자·줄바꿈 제거, 앞뒤 공백 정리, 길이 제한. 비면 기본 이름.</summary>
        public static string SanitizeName(string name, string fallback)
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsHighSurrogate(c))
                {
                    // 서로게이트 쌍(이모지 등)은 함께 넣거나 함께 버린다(반쪽 문자 금지).
                    if (i + 1 >= name.Length || !char.IsLowSurrogate(name[i + 1])) continue;
                    if (sb.Length + 2 > DrinkRules.RecipeNameMax) break;
                    sb.Append(c).Append(name[i + 1]);
                    i++;
                    continue;
                }
                if (char.IsLowSurrogate(c) || char.IsControl(c) || c == '<' || c == '>' || c == '|' || c == '\\' || c == '/') continue;
                sb.Append(c);
                if (sb.Length >= DrinkRules.RecipeNameMax) break;
            }
            var s = sb.ToString().Trim();
            return s.Length == 0 ? fallback : s;
        }
    }
}
