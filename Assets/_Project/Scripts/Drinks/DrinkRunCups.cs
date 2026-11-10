using System;
using System.Collections.Generic;
using System.Linq;

namespace CCT
{
    /// <summary>
    /// 2026-11-11 마스터 도전 런 안에서 만든 한 잔. 로비 준비 음료(prepared 1슬롯)와 분리된 런 전용 목록.
    /// 재료 출처를 잔별로 기록한다: fromRun = 이번 런 미정산 보상(pending, 무한 출처)에서 쓴 양, fromStockNormal/Endless = 로비 재고에서 쓴 양(출처별).
    /// 상태: Held(보관) → Drunk(마심) 또는 Submitted(박람회 제출). 한 잔은 한 용도로만, 상태 전환은 되돌리지 않는다.
    /// 런 제조 잔은 판매 경로(주문·prepared)에 들어가지 않는다 — 마실 수 있지만 팔 수 없다.
    /// </summary>
    [Serializable]
    public class RunCup
    {
        public const string Held = "Held", Drunk = "Drunk", Submitted = "Submitted";
        public string runId = "";
        public string cupId = "";
        public DrinkRecipe recipe;
        public string state = Held;
        /// <summary>정수 품질 0~100(커피만 — CoffeeBrewing.Strength01 기반), 그 외 -1(품질 판정 없음).</summary>
        public int quality = -1;
        /// <summary>종류 집계 키(종류 + 재료 ID 집합). 양·이름·공유 코드·얼음/물·기법은 무시.</summary>
        public string recipeKey = "";
        public List<DrinkAmount> fromRun = new List<DrinkAmount>();
        public List<DrinkAmount> fromStockNormal = new List<DrinkAmount>();
        public List<DrinkAmount> fromStockEndless = new List<DrinkAmount>();

        public DrinkKind Kind => recipe != null ? recipe.Kind : DrinkKind.Cocktail;
        public bool IsHeld => state == Held;
    }

    /// <summary>커피 품질(정수 0~100)과 등급. 추출법별 목표 비율이 달라 모든 커피에 같은 최적 조건을 강요하지 않는다.</summary>
    public static class DrinkQuality
    {
        public const int GoodMin = 60, BestMin = 85;
        public enum Grade { None, Normal, Good, Best }

        public static int Of(DrinkRecipe r)
        {
            if (r == null || r.Kind != DrinkKind.Coffee) return -1;
            return IntNum.Percent(CoffeeBrewing.Strength01(r.coffeeMethod, r.CoffeeDose(), r.water));
        }

        public static Grade GradeOf(int q) => q < 0 ? Grade.None : q >= BestMin ? Grade.Best : q >= GoodMin ? Grade.Good : Grade.Normal;

        /// <summary>색에 기대지 않는 표식 + 이름.</summary>
        public static string Label(int q) => GradeOf(q) switch
        {
            Grade.Best => $"★★★ 최고 {q}",
            Grade.Good => $"★★ 좋음 {q}",
            Grade.Normal => $"★ 보통 {q}",
            _ => "",
        };
    }

    public static partial class DrinkLab
    {
        /// <summary>종류 집계 키: 종류 + 사용한 재료 ID 집합(정렬). 양·이름·장식만 바꾼 변형은 같은 종류.</summary>
        public static string RecipeKey(DrinkRecipe r)
        {
            if (r?.steps == null) return "";
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var s in r.steps) if (s != null && s.amount > 0 && DrinkIngredients.Get(s.id) != null) ids.Add(s.id);
            return ids.Count == 0 ? "" : (int)r.Kind + ":" + string.Join("+", ids);
        }

        public static List<RunCup> RunCupsFor(string runId)
        {
            var list = new List<RunCup>();
            if (string.IsNullOrEmpty(runId)) return list;
            foreach (var c in Data.runCups) if (c != null && c.runId == runId) list.Add(c);
            return list;
        }

        /// <summary>이번 런 미정산 보상에서 아직 런 제조에 쓰지 않은 양(재료별).</summary>
        public static Dictionary<string, int> RunPool(string runId) => RunPool(Data, runId);

        static Dictionary<string, int> RunPool(DrinkSaveData d, string runId)
        {
            var pool = new Dictionary<string, long>();
            if (string.IsNullOrEmpty(runId)) return new Dictionary<string, int>();
            foreach (var a in d.pending)
            {
                if (a == null || a.runId != runId) continue;
                foreach (var i in a.items) if (i != null && i.amount > 0) { pool.TryGetValue(i.id, out long v); pool[i.id] = v + i.amount; }
            }
            foreach (var c in d.runCups)
            {
                if (c == null || c.runId != runId) continue;
                foreach (var i in c.fromRun) if (i != null && i.amount > 0) { pool.TryGetValue(i.id, out long v); pool[i.id] = v - i.amount; }
            }
            var result = new Dictionary<string, int>();
            foreach (var kv in pool) if (kv.Value > 0) result[kv.Key] = (int)Math.Min(int.MaxValue, kv.Value);
            return result;
        }

        /// <summary>런 제조에 쓸 수 있는 양 = 런 임시 재료 + 로비 재고.</summary>
        public static int RunAvailable(string runId, string id)
        {
            RunPool(runId).TryGetValue(id ?? "", out int p);
            return (int)Math.Min(int.MaxValue, (long)p + Owned(id));
        }

        public sealed class RunCraftResult
        {
            public bool Ok;
            public string Message;
            public RunCup Cup;
            public Dictionary<string, int> Shortages = new Dictionary<string, int>();
        }

        /// <summary>
        /// 런 제조(한 잔). 런 임시 재료를 먼저, 부족분만 로비 재고(일반 출처 먼저)에서 한 번 차감한다. 저장 성공 때만 확정(실패 시 재고·잔 모두 그대로).
        /// 레시피만으로 음료를 주지 않으며, 정산이 끝난 런에는 만들 수 없다.
        /// </summary>
        public static RunCraftResult CraftInRun(string runId, DrinkRecipe recipe)
        {
            var res = new RunCraftResult();
            EnsureLoaded();
            if (string.IsNullOrEmpty(runId) || IsSettled(runId)) { res.Message = "이 판은 이미 정산되어 제조할 수 없습니다."; return res; }
            var check = DrinkValidator.Check(recipe);
            if (!check.Ok) { res.Message = check.First; return res; }
            var pool = RunPool(runId);
            res.Shortages = DrinkValidator.Shortages(recipe, id => (int)Math.Min(int.MaxValue, (long)(pool.TryGetValue(id, out int p) ? p : 0) + data.Owned(id)));
            if (res.Shortages.Count > 0) { res.Message = "재료가 부족합니다."; return res; }
            var copy = recipe.Clone();
            copy.name = DrinkValidator.SanitizeName(copy.name, DefaultName(copy));
            RunCup made = null;
            bool ok = Transact(d =>
            {
                var dpool = RunPool(d, runId);
                var need = new Dictionary<string, int>();
                var order = new List<string>();
                foreach (var s in copy.steps)
                {
                    if (s == null || s.amount <= 0) continue;
                    if (!need.ContainsKey(s.id)) { need[s.id] = 0; order.Add(s.id); }
                    need[s.id] = (int)Math.Min(int.MaxValue, (long)need[s.id] + s.amount);
                }
                var cup = new RunCup { runId = runId, cupId = Guid.NewGuid().ToString("N"), recipe = copy, quality = DrinkQuality.Of(copy), recipeKey = RecipeKey(copy) };
                foreach (var id in order)
                {
                    int want = need[id];
                    int fromRun = Math.Min(want, dpool.TryGetValue(id, out int p) ? p : 0);
                    int rest = want - fromRun;
                    if (rest > d.Owned(id)) return false; // 동시 변경 방어(위 검사와 같은 기준)
                    if (fromRun > 0) cup.fromRun.Add(new DrinkAmount(id, fromRun));
                    if (rest > 0)
                    {
                        int endless = d.ConsumeNormalFirst(id, rest);
                        if (rest - endless > 0) cup.fromStockNormal.Add(new DrinkAmount(id, rest - endless));
                        if (endless > 0) cup.fromStockEndless.Add(new DrinkAmount(id, endless));
                    }
                }
                d.runCups.Add(cup);
                made = cup;
                return true;
            }, out var error);
            if (!ok) { res.Message = error ?? "제조하지 못했습니다. 재료는 그대로입니다."; return res; }
            res.Ok = true;
            res.Cup = made;
            res.Message = "한 잔을 만들어 보관했습니다.";
            return res;
        }

        /// <summary>보관 잔을 마시기/제출로 확정(한 번만). 다른 런·이미 쓴 잔·정산된 런은 거부.</summary>
        public static bool UseRunCup(string runId, string cupId, string newState, out string error)
        {
            error = null;
            if (newState != RunCup.Drunk && newState != RunCup.Submitted) { error = "알 수 없는 사용입니다."; return false; }
            if (string.IsNullOrEmpty(runId) || IsSettled(runId)) { error = "이 판은 이미 정산되었습니다."; return false; }
            var cup = Data.runCups.Find(c => c != null && c.runId == runId && c.cupId == cupId);
            if (cup == null || !cup.IsHeld) { error = "이미 사용했거나 없는 잔입니다."; return false; }
            return Transact(d =>
            {
                var dc = d.runCups.Find(c => c != null && c.runId == runId && c.cupId == cupId);
                if (dc == null || !dc.IsHeld) return false;
                dc.state = newState;
                return true;
            }, out error);
        }

        /// <summary>
        /// 런 정산·복구 공용(같은 저장 사건 안에서 호출): 보관(Held) 잔은 로비 재고에서 쓴 양을 원래 출처로 되돌리고(런 재료는 차감하지 않음 = 그대로 지급),
        /// 마시거나 제출한 잔은 런 재료 사용량을 debit에 더한다(정산 지급에서 뺀다). 이 런의 잔 기록은 모두 지운다.
        /// </summary>
        static void SettleRunCups(DrinkSaveData d, string runId, Dictionary<string, long> debit)
        {
            for (int i = d.runCups.Count - 1; i >= 0; i--)
            {
                var c = d.runCups[i];
                if (c == null) { d.runCups.RemoveAt(i); continue; }
                if ((c.runId ?? "") != (runId ?? "")) continue;
                if (c.IsHeld)
                {
                    foreach (var a in c.fromStockNormal) if (a != null && a.amount > 0) d.AddFrom(a.id, a.amount, false);
                    foreach (var a in c.fromStockEndless) if (a != null && a.amount > 0) d.AddFrom(a.id, a.amount, true);
                }
                else
                {
                    foreach (var a in c.fromRun)
                    {
                        if (a == null || a.amount <= 0) continue;
                        debit.TryGetValue(a.id, out long v);
                        debit[a.id] = v + a.amount;
                    }
                }
                d.runCups.RemoveAt(i);
            }
        }

        /// <summary>지급량에서 런 제조로 이미 쓴 양을 뺀다(남은 차감은 다음 보상 항목으로).</summary>
        static int ApplyDebit(Dictionary<string, long> debit, string id, int amount)
        {
            if (debit == null || amount <= 0 || !debit.TryGetValue(id, out long owe) || owe <= 0) return amount;
            long take = Math.Min(owe, amount);
            debit[id] = owe - take;
            return (int)(amount - take);
        }

        static IEnumerable<string> RunCupRunIds(DrinkSaveData d) => d.runCups.Where(c => c != null).Select(c => c.runId ?? "").Distinct();
    }
}
