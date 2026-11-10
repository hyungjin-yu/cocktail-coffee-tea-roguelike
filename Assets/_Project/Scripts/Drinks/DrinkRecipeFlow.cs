using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 레시피 서랍 흐름(저장·수정·불러오기·공유 코드 복사·가져오기 미리보기/저장). 화면 없이 상태(수정 중인 칸, 가져오기 미리보기)와
    /// 결과 문구만 낸다 — 탭 전환·입력칸·찢는 모션 같은 화면 반영은 DrinkLabView가 한다. 저장 자체는 DrinkLab 트랜잭션.
    /// </summary>
    public sealed class DrinkRecipeFlow
    {
        public const string PresetLoadedMessage = "예시 배합을 불러왔습니다 — 양·기법을 고친 뒤 제조 확정하거나 레시피로 저장하세요.";

        /// <summary>수정 중인 서랍 칸(아니면 -1). 저장하면 그 칸을 덮어쓴다.</summary>
        public int EditingIndex { get; private set; } = -1;
        /// <summary>코드 확인으로 읽은 가져오기 후보(아니면 null).</summary>
        public DrinkRecipe ImportPreview { get; private set; }

        public void StopEditing() => EditingIndex = -1;

        /// <summary>
        /// 배합을 이름과 함께 서랍에 저장(수정 중이면 덮어쓰기, 재료 소모 없음). 성공하면 shownName = 서랍에 꽂힌 이름(빈 이름은 저장이 붙인 이름).
        /// </summary>
        public bool SaveDraft(DrinkRecipe draft, string name, out string message, out string shownName)
        {
            shownName = null;
            var r = draft.Clone();
            r.name = name;
            if (!DrinkLab.SaveRecipe(r, EditingIndex, out var error)) { message = error; return false; }
            message = EditingIndex >= 0 ? "레시피를 수정했습니다." : "레시피를 저장해 레시피 서랍에 꽂았습니다(재료 소모 없음).";
            shownName = string.IsNullOrWhiteSpace(r.name) ? DrinkLab.Recipes[DrinkLab.Recipes.Count - 1].name : r.name;
            if (EditingIndex >= 0 && EditingIndex < DrinkLab.Recipes.Count) shownName = DrinkLab.Recipes[EditingIndex].name;
            return true;
        }

        /// <summary>추천 배합 사본(범위 밖이면 null). 불러오면 수정 모드가 아니다.</summary>
        public DrinkRecipe LoadPreset(int index)
        {
            var list = DrinkLab.ExamplePresets;
            if (index < 0 || index >= list.Count) return null;
            var r = list[index].Clone();
            EditingIndex = -1;
            return r;
        }

        /// <summary>서랍 레시피 사본(범위 밖이면 null). edit이면 그 칸을 수정 중으로 둔다.</summary>
        public DrinkRecipe LoadRecipe(int index, bool edit)
        {
            var list = DrinkLab.Recipes;
            if (index < 0 || index >= list.Count) return null;
            var r = list[index].Clone();
            EditingIndex = edit ? index : -1;
            return r;
        }

        /// <summary>불러온 레시피로 여는 제조 종류(차·커피 외에는 칵테일).</summary>
        public static DrinkKind CraftKind(DrinkRecipe r) => r.Kind == DrinkKind.Tea || r.Kind == DrinkKind.Coffee ? r.Kind : DrinkKind.Cocktail;

        /// <summary>서랍 레시피를 불러온 뒤 안내(부족 재료는 자동 대체·차감 없이 알린다).</summary>
        public static string LoadedMessage(DrinkRecipe r, bool edit)
        {
            var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
            return shortages.Count > 0 ? "재료가 부족합니다(자동 대체·차감 없음): " + ShortageLine(shortages) : edit ? "레시피 수정 중 — 저장하면 덮어씁니다." : "레시피를 불러왔습니다. 확인 후 제조 확정을 누르세요.";
        }

        /// <summary>서랍 레시피의 공유 코드를 클립보드에 복사(범위 밖이면 null).</summary>
        public static string CopyCode(int index, out string message)
        {
            message = null;
            var list = DrinkLab.Recipes;
            if (index < 0 || index >= list.Count) return null;
            var code = DrinkShareCode.Encode(list[index]);
            GUIUtility.systemCopyBuffer = code;
            message = $"\"{list[index].name}\" 공유 코드를 클립보드에 복사했습니다(붙여넣기로 전달).";
            return code;
        }

        /// <summary>공유 코드를 읽어 가져오기 후보로 둔다(실패하면 후보 없음 + error).</summary>
        public bool PreviewImport(string code, out string error)
        {
            ImportPreview = DrinkShareCode.Decode(code, out error);
            return ImportPreview != null;
        }

        /// <summary>가져오기 후보를 새 칸으로 저장(창고·버프 변화 없음). 성공하면 후보를 비운다.</summary>
        public bool SaveImport(out string message)
        {
            if (ImportPreview == null) { message = "먼저 코드를 확인하세요."; return false; }
            bool ok = DrinkLab.SaveRecipe(ImportPreview, -1, out var error);
            message = ok ? "가져온 레시피를 저장했습니다(창고·버프 변화 없음)." : error;
            if (ok) ImportPreview = null;
            return ok;
        }

        /// <summary>부족 재료 한 줄(이름 양 (획득처)).</summary>
        public static string ShortageLine(Dictionary<string, int> s)
        {
            var parts = new List<string>();
            foreach (var kv in s) { var d = DrinkIngredients.Get(kv.Key); if (d != null) parts.Add($"{d.name} {DrinkRules.Format(kv.Value, d.form)} (획득처: {DrinkLab.SourceText(d.id)})"); }
            return string.Join(", ", parts);
        }
    }
}
