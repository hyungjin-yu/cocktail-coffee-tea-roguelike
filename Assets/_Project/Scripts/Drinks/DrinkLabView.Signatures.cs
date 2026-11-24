using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 도감 · 정식 레시피(재료 도감 도구줄의 "정식 레시피 n/전체" 버튼 → 기존 상세 덮개에 표시).
    /// 발견 보호: 미발견 칸은 "???"와 "아직 발견하지 못한 정식 레시피"만 — 이름·재료·비율·기법·잔·색·효과·출처를 쓰지 않는다.
    /// 발견한 칸만 공식 배합·방법·가니시(사실), 처음 맞힌 내 배합, 발견 일시, 시그니처 효과, 출처, 판정 허용 안내를 보인다. 표시 전용(저장 변경 없음).
    /// </summary>
    public partial class DrinkLabView
    {
        public const string SignatureLockedName = "???";
        public const string SignatureLockedLine = "아직 발견하지 못한 정식 레시피 — 직접 만들어 확인하세요.";

        public bool SignatureBookOpen { get; private set; }
        public string SignatureBookShownText { get; private set; } = "";

        public static string SignatureBookButtonLabel() => $"정식 레시피 {DrinkLab.DiscoveredCount}/{DrinkSignatures.Count(DrinkKind.Cocktail)}";

        /// <summary>도감 본문(발견 기록만으로 계산 — 시험이 같은 함수로 누출 여부를 확인한다).</summary>
        public static string SignatureBookText()
        {
            var sb = new StringBuilder();
            int total = DrinkSignatures.Count(DrinkKind.Cocktail);
            sb.Append($"<b>칵테일 정식 레시피 {DrinkLab.DiscoveredCount} / {total} 발견</b>\n");
            sb.Append("국제바텐더협회(IBA) 공식 레시피와 같은 재료·비율·기법으로 실제 제조하면 발견됩니다. 차·커피는 이번 판정 대상이 아닙니다.\n\n");
            var records = new Dictionary<string, DrinkSignatureRecord>();
            foreach (var r in DrinkLab.DiscoveredSignatures) if (r != null && !string.IsNullOrEmpty(r.id) && !records.ContainsKey(r.id)) records[r.id] = r;
            int locked = 0;
            foreach (var def in DrinkSignatures.All)
            {
                if (!records.TryGetValue(def.id, out var rec)) { locked++; continue; }
                sb.Append($"<b>◆ {def.name}</b>  ·  IBA 공식 레시피{(string.IsNullOrEmpty(def.category) ? "" : " · " + def.category)}\n");
                sb.Append($"공식 배합: {DrinkSignatures.OfficialLine(def)}\n");
                sb.Append($"방법: {def.method}{(string.IsNullOrEmpty(def.garnish) ? "" : " · 장식: " + def.garnish + "(게임에서는 판정하지 않음)")}\n");
                sb.Append($"내가 처음 맞힌 배합: {FirstRecipeLine(rec.firstRecipe)}\n");
                sb.Append($"발견: {ShortUtc(rec.utc)}\n");
                sb.Append($"<b>시그니처 · {def.effectName}</b> — {def.effectDetail}\n");
                sb.Append("이 레시피로 만든 잔을 마시면 그 판에서 한 번 사용(Q 또는 시그니처 칸). 쓰면 그 판에서는 다시 쓸 수 없습니다.\n");
                sb.Append($"출처: {def.sourceUrl}\n\n");
            }
            // 판정 대상에서 빠진 예전 발견(이후 버전)도 지우지 않고 이름 없이 개수만 보존 표시
            int stale = 0;
            foreach (var kv in records) if (DrinkSignatures.Get(kv.Key) == null) stale++;
            for (int i = 0; i < locked; i++) sb.Append($"<b>{SignatureLockedName}</b> — {SignatureLockedLine}\n");
            if (stale > 0) sb.Append($"\n(현재 판정 대상이 아닌 예전 발견 기록 {stale}건 보존 중)\n");
            if (DrinkLab.DiscoveredCount > 0) sb.Append("\n<b>판정 안내</b>: " + DrinkSignatures.ToleranceNote);
            return sb.ToString();
        }

        static string FirstRecipeLine(DrinkRecipe r)
        {
            if (r?.steps == null || r.steps.Count == 0) return "(기록 없음)";
            var parts = new List<string>();
            foreach (var s in r.steps)
            {
                var d = s == null ? null : DrinkIngredients.Get(s.id);
                if (d == null || s.amount <= 0) continue;
                parts.Add($"{d.name} {DrinkRules.Format(s.amount, d.form)}");
            }
            return string.Join(" · ", parts) + $" · {DrinkRules.TechniqueName(r.Technique)}";
        }

        static string ShortUtc(string utc) =>
            System.DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "";

        /// <summary>도구줄 버튼 → 상세 덮개에 정식 레시피 도감을 연다(닫기는 재료 상세와 같은 CloseCodexDetail).</summary>
        public void OpenSignatureBook()
        {
            if (codexDetail == null) return;
            for (int i = codexDetail.childCount - 1; i >= 0; i--)
            {
                var child = codexDetail.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            codexReturnId = null;
            CodexDetailId = "signatures";
            SignatureBookOpen = true;
            codexDetail.gameObject.SetActive(true);
            codexDetail.SetAsLastSibling();
            float listW = codexDetail.sizeDelta.x;
            float W = Mathf.Min(listW - 40f, 1500f), H = 864f, closeH = 66f;
            var card = UiFactory.Place("SignatureBook", codexDetail, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W, H));
            DrinkPaperUi.Sliced(card, "ingredient_card", 3f, Color.white);
            float rw = W - 80f, scrollTop = 40f, scrollH = H - scrollTop - closeH - 60f;
            var area = UiFactory.Place("SignatureScroll", card, new Vector2(0f, 1f), new Vector2(40f, -scrollTop), new Vector2(rw, scrollH));
            var content = MakeScroll(area, true);
            SignatureBookShownText = SignatureBookText();
            var t = UiFactory.AddText(UiFactory.Place("Body", content, new Vector2(0f, 1f), Vector2.zero, new Vector2(rw - 24f, 40f)), SignatureBookShownText, DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
            float bodyH = Mathf.Ceil(TextHeightAt(t, rw - 24f)) + 4f;
            t.rectTransform.sizeDelta = new Vector2(rw - 24f, bodyH);
            content.sizeDelta = new Vector2(0f, bodyH + 8f);
            var close = DrinkPaperUi.Button(card, "CloseSignatureBook", new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, closeH), "목록으로", DrinkPaperUi.Small,
                () => { SignatureBookOpen = false; CloseCodexDetail(); });
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
    }
}
