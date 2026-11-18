#if CCT_QA_TOUR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-02 첫 판매 검수(QA 투어 빌드 전용, CCT_QA_TOUR + -cct-data-dir 격리 필수).
    /// 새 격리 저장에서 타이틀 → 시작 → 음료 제조 → 재료 목록·도감 → 주문 확인 → 제조(재료 행 클릭·양 입력·기법 버튼·제조 확정) →
    /// 주문·판매 → 판매 버튼까지 실제 화면의 버튼만 눌러 진행한다. 저장 데이터를 직접 바꾸지 않는다(테스트 재료 지급·코인 주입 없음).
    /// 단계별 화면 캡처, 보이는 글자 겹침·버튼 도달, 단계 소요 시간, 비활성 버튼과 그 이유 문구를 firstsale.json에 남긴다.
    /// -cct-firstsale-capture &lt;폴더&gt; [-cct-firstsale-phase sale|reload] [-cct-capture-width W -cct-capture-height H]
    /// reload 단계는 같은 -cct-data-dir로 앱을 다시 켜서 코인·주문·창고가 저장됐는지 화면으로 확인한다.
    /// </summary>
    public class DrinkFirstSaleCapture : MonoBehaviour
    {
        string output, phase;
        readonly List<string> steps = new List<string>();
        readonly List<string> issues = new List<string>();
        readonly StringBuilder screenText = new StringBuilder();
        float t0, stepStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cct-firstsale-capture");
            if (i < 0 || i + 1 >= args.Length || Array.IndexOf(args, "-cct-data-dir") < 0) return;
            Application.runInBackground = true;
            var go = new GameObject("DrinkFirstSaleCapture");
            DontDestroyOnLoad(go);
            var v = go.AddComponent<DrinkFirstSaleCapture>();
            v.output = args[i + 1];
            int p = Array.IndexOf(args, "-cct-firstsale-phase");
            v.phase = p >= 0 && p + 1 < args.Length ? args[p + 1] : "sale";
            Directory.CreateDirectory(v.output);
            v.StartCoroutine(v.Run());
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }
        static Button Btn(string path) { var go = GameObject.Find(path); return go != null ? go.GetComponent<Button>() : null; }
        static string HierPath(Transform t) => t.parent == null ? t.name : HierPath(t.parent) + "/" + t.name;
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        void Step(string what)
        {
            float now = Time.realtimeSinceStartup;
            steps.Add($"{now - t0:0.00}s (+{now - stepStart:0.00}s) {what}");
            stepStart = now;
        }

        bool Click(string path, string what)
        {
            var b = Btn(path);
            if (b == null) { issues.Add($"MISSING button {path} ({what})"); return false; }
            if (!b.interactable || !b.isActiveAndEnabled) { issues.Add($"DISABLED button {path} ({what})"); return false; }
            b.onClick.Invoke();
            Step("click " + what);
            return true;
        }

        IEnumerator Shot(string label, Transform root)
        {
            yield return null;
            if (root != null) { CheckOverlap(label, root); CheckReach(label, root); DumpText(label, root); }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, label + ".png"));
            yield return Wait(0.3f);
        }

        void DumpText(string label, Transform root)
        {
            screenText.Append("## ").Append(label).Append('\n');
            foreach (var t in root.GetComponentsInChildren<Text>(false))
                if (t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text))
                {
                    var b = t.GetComponentInParent<Button>();
                    string state = b != null && b.transform == t.transform.parent ? (b.interactable ? " [button]" : " [button DISABLED]") : "";
                    screenText.Append("- ").Append(t.text.Replace("\n", " ⏎ ")).Append(state).Append('\n');
                }
        }

        /// <summary>보이는 글자 상자(실제 글자 높이·폭 반영)끼리 겹치는지. 스크롤 마스크 밖은 제외.</summary>
        void CheckOverlap(string label, Transform root)
        {
            Canvas.ForceUpdateCanvases();
            var rects = new List<(Text t, Rect r)>();
            foreach (var t in root.GetComponentsInChildren<Text>(false).Where(x => x.isActiveAndEnabled && !string.IsNullOrEmpty(x.text)))
            {
                var c = new Vector3[4]; t.rectTransform.GetWorldCorners(c);
                var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                float scale = t.canvas != null ? t.canvas.scaleFactor : 1f;
                float pref = t.preferredHeight * scale;
                if (pref > r.height + 1.5f && t.verticalOverflow == VerticalWrapMode.Overflow) issues.Add($"{label}: TALL {HierPath(t.transform)} pref={pref:0} box={r.height:0}");
                if (pref > r.height && t.verticalOverflow == VerticalWrapMode.Overflow)
                {
                    bool upper = t.alignment == TextAnchor.UpperLeft || t.alignment == TextAnchor.UpperCenter || t.alignment == TextAnchor.UpperRight;
                    r = upper ? Rect.MinMaxRect(r.xMin, r.yMax - pref, r.xMax, r.yMax) : Rect.MinMaxRect(r.xMin, r.center.y - pref / 2, r.xMax, r.center.y + pref / 2);
                }
                if (pref > r.height + 1.5f && t.verticalOverflow == VerticalWrapMode.Truncate) issues.Add($"{label}: CLIPPED {HierPath(t.transform)} pref={pref:0} box={r.height:0} '{t.text.Replace("\n", "⏎").Substring(0, Math.Min(50, t.text.Length))}'");
                float w = Mathf.Min(r.width, t.preferredWidth * scale);
                bool right = t.alignment == TextAnchor.MiddleRight || t.alignment == TextAnchor.UpperRight || t.alignment == TextAnchor.LowerRight;
                bool center = t.alignment == TextAnchor.MiddleCenter || t.alignment == TextAnchor.UpperCenter || t.alignment == TextAnchor.LowerCenter;
                r = right ? Rect.MinMaxRect(r.xMax - w, r.yMin, r.xMax, r.yMax) : center ? Rect.MinMaxRect(r.center.x - w / 2, r.yMin, r.center.x + w / 2, r.yMax) : Rect.MinMaxRect(r.xMin, r.yMin, r.xMin + w, r.yMax);
                var mask = t.GetComponentInParent<RectMask2D>();
                if (mask != null)
                {
                    var mc = new Vector3[4]; ((RectTransform)mask.transform).GetWorldCorners(mc);
                    var mr = Rect.MinMaxRect(mc[0].x, mc[0].y, mc[2].x, mc[2].y);
                    if (!mr.Overlaps(r)) continue;
                    r = Rect.MinMaxRect(Mathf.Max(r.xMin, mr.xMin), Mathf.Max(r.yMin, mr.yMin), Mathf.Min(r.xMax, mr.xMax), Mathf.Min(r.yMax, mr.yMax));
                }
                rects.Add((t, r));
            }
            int n = 0;
            for (int a = 0; a < rects.Count; a++)
                for (int b = a + 1; b < rects.Count; b++)
                {
                    var A = rects[a].r; var B = rects[b].r;
                    float ox = Mathf.Min(A.xMax, B.xMax) - Mathf.Max(A.xMin, B.xMin), oy = Mathf.Min(A.yMax, B.yMax) - Mathf.Max(A.yMin, B.yMin);
                    if (ox > 2f && oy > 2f) { issues.Add($"{label}: OVERLAP '{rects[a].t.text.Split('\n')[0]}' x '{rects[b].t.text.Split('\n')[0]}' ({ox:0}x{oy:0}px)"); n++; }
                }
            steps.Add($"   {label}: {rects.Count} visible text boxes, overlaps={n}");
        }

        /// <summary>보이는 버튼이 화면(또는 스크롤 영역) 안에 있는지.</summary>
        void CheckReach(string label, Transform root)
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            int bad = 0, n = 0;
            var lab = root.GetComponentInParent<DrinkLabView>() ?? root.GetComponentInChildren<DrinkLabView>();
            foreach (var b in root.GetComponentsInChildren<Button>(false))
            {
                if (!b.isActiveAndEnabled) continue;
                if (lab != null && lab.Narrow && !lab.ReceiptOpen && HierPath(b.transform).Contains("/MixPanel/")) continue;
                var c = new Vector3[4]; ((RectTransform)b.transform).GetWorldCorners(c);
                var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                var scroll = b.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    var vc = new Vector3[4]; ((RectTransform)scroll.transform).GetWorldCorners(vc);
                    if (!Rect.MinMaxRect(vc[0].x, vc[0].y, vc[2].x, vc[2].y).Overlaps(r)) continue;
                }
                else if (r.xMin < -1 || r.yMin < -1 || r.xMax > Screen.width + 1 || r.yMax > Screen.height + 1) { issues.Add($"{label}: REACH off-screen {HierPath(b.transform)}"); bad++; }
                n++;
            }
            steps.Add($"   {label}: {n} visible buttons, reachIssues={bad}");
        }

        IEnumerator Run()
        {
            t0 = stepStart = Time.realtimeSinceStartup;
            yield return Wait(4.0f); // 저장된 화면 설정 부트 확인이 끝난 뒤
            var args = Environment.GetCommandLineArgs();
            int w = Array.IndexOf(args, "-cct-capture-width"), h = Array.IndexOf(args, "-cct-capture-height");
            if (w >= 0 && h >= 0 && w + 1 < args.Length && h + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[w + 1]), int.Parse(args[h + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            Step($"boot settled at {Screen.width}x{Screen.height}");
            DrinkStageView.IgnoreFocusLossForCapture = true;
            if (phase == "reload") yield return Reload();
            else if (phase == "states") yield return States();
            else yield return FirstSale();
            Write();
            yield return Wait(0.5f);
            Application.Quit();
        }

        IEnumerator FirstSale()
        {
            int coinsAtStart = DrinkLab.Coins;
            steps.Add($"   start state: coins={coinsAtStart} prepared={(DrinkLab.Prepared != null)} recipes={DrinkLab.Recipes.Count} devTools={DrinkLab.DevToolsEnabled} tutorialSeen={FirstRunTutorial.Seen}");
            var title = FindFirstObjectByType<TitleView>();
            yield return Shot("F01_title_fresh_save", title != null ? title.transform : null);
            if (!Click("StartButton", "타이틀 시작")) yield break;
            yield return Wait(0.6f);
            var select = FindFirstObjectByType<CharacterSelectView>();
            if (select == null) { issues.Add("character select not shown after Start"); yield break; }
            yield return Shot("F02_select_job", select.transform);
            select.DrinkLabButton.onClick.Invoke(); Step("click 음료 제조 (출발 전)");
            yield return Wait(0.8f);
            var lab = select.DrinkLabScreen;
            if (lab == null) { issues.Add("drink lab not opened"); yield break; }
            steps.Add($"   notice on open: {lab.NoticeText}");
            yield return Shot("F03_lab_craft_first_open", lab.transform);

            // 재고 확인: 재료 도감 탭
            Click("TabCodex", "재료 도감 탭");
            yield return Wait(0.5f);
            yield return Shot("F04_codex_inventory", lab.transform);

            // 주문 확인(준비 음료 없음 → 판매 버튼 비활성과 안내 문구)
            Click("TabOrders", "주문·판매 탭");
            yield return Wait(0.5f);
            yield return Shot("F05_orders_before_craft", lab.transform);
            var sell0 = Btn("Order_0/Sell");
            steps.Add($"   order0 sell button interactable before craft = {(sell0 != null && sell0.interactable)}");
            var order = DrinkLab.Orders.FirstOrDefault(o => o.IsOpen && o.kind == (int)DrinkKind.Cocktail);
            if (order == null) { issues.Add("no open cocktail order on a fresh save"); yield break; }
            int orderIndex = DrinkLab.Orders.ToList().IndexOf(order);
            steps.Add($"   target order {orderIndex + 1}: {DrinkSales.Describe(order)} technique={(DrinkTechnique)order.technique} color={order.colorFamily}");

            // 제조 탭으로 돌아가 주문 재료를 화면 조작으로 넣는다(재료 행 클릭 → 양 입력 → 적용).
            Click("TabCraft", "제조 탭");
            yield return Wait(0.6f);
            lab = select.DrinkLabScreen;
            if (lab.Narrow && !lab.ReceiptOpen) { Click("ReceiptToggle", "영수증 열기(좁은 창)"); yield return Wait(0.3f); }
            yield return Shot("F06_craft_empty_receipt", lab.transform);
            steps.Add($"   craft footer before ingredients: '{lab.FooterStatusText}' craftInteractable={lab.CraftInteractable}");
            foreach (var (id, amount) in new[] { (order.ingA, order.minA), (order.ingB, order.minB) })
            {
                if (lab.Narrow && lab.ReceiptOpen) { Click("ReceiptToggle", "영수증 접기(좁은 창)"); yield return Wait(0.3f); }
                Click("Row_" + id, "재료 " + DrinkIngredients.Get(id)?.name);
                yield return Wait(0.3f);
                // 최소량보다 조금 넉넉히(0.1 단위 입력 필드에 사람이 치는 값처럼 문자열로)
                int typed = Math.Min(DrinkLab.Owned(id), amount * 2);
                lab.AmountField.text = DrinkRules.Format(typed);
                Click("AmountControls/Apply", $"양 {DrinkRules.Format(typed)} 적용");
                yield return Wait(0.3f);
                if (!string.IsNullOrEmpty(lab.AmountErrorText)) issues.Add($"amount error for {id}: {lab.AmountErrorText}");
            }
            if (lab.Narrow && !lab.ReceiptOpen) { Click("ReceiptToggle", "영수증 열기(좁은 창)"); yield return Wait(0.3f); }
            var techName = "Tech" + (DrinkTechnique)order.technique;
            var tech = lab.transform.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == techName);
            if (tech != null && tech.isActiveAndEnabled) { tech.onClick.Invoke(); Step("click 기법 " + DrinkRules.TechniqueName((DrinkTechnique)order.technique)); }
            else issues.Add($"technique button {techName} not visible without scrolling (active={(tech != null && tech.gameObject.activeInHierarchy)})");
            yield return Wait(0.4f);
            yield return Shot("F07_craft_ready", lab.transform);
            steps.Add($"   craft footer ready: '{lab.FooterStatusText}' craftInteractable={lab.CraftInteractable} buff='{lab.BuffText}'");
            if (!Click("MixPanel/Craft", "제조 확정")) yield break;
            float motion = 0f;
            yield return Wait(0.2f);
            yield return Shot("F08_craft_motion_start", lab.transform);
            while (lab.Stage != null && lab.Stage.SequencePlaying && motion < 60f) { motion += Time.unscaledDeltaTime; yield return null; }
            if (lab.Stage != null && lab.Stage.SequencePlaying) { issues.Add("FAIL craft motion still playing after 60s"); yield break; }
            Step($"craft motion finished ({motion:0.0}s waiting)");
            yield return Wait(0.6f);
            yield return Shot("F09_craft_done", lab.transform);
            steps.Add($"   after craft: prepared={(DrinkLab.Prepared != null ? DrinkLab.Prepared.recipe.name : "none")} msg={lab.LastMessage}");

            Click("TabOrders", "주문·판매 탭");
            yield return Wait(0.5f);
            int price = DrinkSales.Price(order, DrinkLab.Prepared != null ? DrinkLab.Prepared.recipe : null);
            yield return Shot("F10_orders_ready_to_sell", lab.transform);
            steps.Add($"   expected price for order {orderIndex + 1} = {price}");
            if (!Click($"Order_{orderIndex}/Sell", $"판매 ({price}코인)")) yield break;
            yield return Wait(0.5f);
            yield return Shot("F11_sold", lab.transform);
            steps.Add($"   after sale: coins {coinsAtStart} -> {DrinkLab.Coins}, prepared={(DrinkLab.Prepared != null)}, msg={lab.LastMessage}, saveDirty={DrinkLab.LastSaveFailed}");
            if (DrinkLab.Coins != coinsAtStart + price) issues.Add($"FAIL coin delta {DrinkLab.Coins - coinsAtStart} != price {price}");
            if (DrinkLab.Prepared != null) issues.Add("FAIL prepared drink still present after sale");
            // 같은 음료 이중 판매 시도: 같은 칸 판매 버튼(새 주문, 음료 없음)과 판매 API 둘 다 코인을 올리지 않아야 한다.
            int coinsAfter = DrinkLab.Coins;
            var again = Btn($"Order_{orderIndex}/Sell");
            steps.Add($"   repeat sale: button interactable={(again != null && again.interactable)} label='{(again != null ? again.GetComponentInChildren<Text>().text : "-")}'");
            if (again != null && again.interactable) issues.Add("FAIL sell button still enabled after sale");
            bool apiAgain = DrinkLab.Sell(orderIndex, out _, out var againError);
            steps.Add($"   repeat sale API: ok={apiAgain} error={againError} coins={DrinkLab.Coins}");
            if (apiAgain || DrinkLab.Coins != coinsAfter) issues.Add("FAIL repeat sale changed coins");
            File.WriteAllText(Path.Combine(output, "expected.txt"), StateLine());
            Click("Root/Close", "닫기");
            yield return Wait(0.5f);
            yield return Shot("F12_back_to_select", select != null ? select.transform : null);
        }

        /// <summary>판매 불가 이유 화면(별도 격리 저장, 첫 판매 시간 측정과 무관): 각 상태의 음료는 판매 API 검증용으로 DrinkLab.Craft로 만들고
        /// (화면 제조 아님 — 보고서에 API 제조로 표기) 확인 뒤 취소·회수로 재료를 돌려받는다. 코인·재료 직접 수정 없음.</summary>
        IEnumerator States()
        {
            if (!Click("StartButton", "타이틀 시작")) yield break;
            yield return Wait(0.6f);
            var select = FindFirstObjectByType<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.8f);
            var lab = select.DrinkLabScreen;
            var cocktail = DrinkLab.Orders[0];
            var coffee = DrinkLab.Orders.FirstOrDefault(o => o.kind == (int)DrinkKind.Coffee);
            var tea = DrinkSales.Reference(DrinkLab.Orders[1]);
            var under = DrinkSales.Reference(cocktail); under.SetAmount(cocktail.ingB, cocktail.minB - 5);
            // 다른 추출 도구의 올바른 최소 배합(도구마다 원두 최소량이 달라 단순 도구 교체는 제조 검사에서 막힌다)
            DrinkRecipe wrongMethod = null;
            if (coffee != null)
            {
                var other = (CoffeeBrewMethod)(coffee.method % 3 + 1);
                wrongMethod = DrinkSales.Reference(new DrinkOrder { kind = coffee.kind, ingA = coffee.ingA, minA = CoffeeBrewing.DoseMinTenths(other), method = (int)other, technique = coffee.technique, state = "Open" });
            }
            var longName = DrinkSales.Reference(cocktail); longName.name = "한밤의레몬진셰이크특별주문한잔더".Substring(0, DrinkRules.RecipeNameMax);
            var cases = new (string tag, DrinkRecipe r)[] { ("S01_no_prepared", null), ("S02_wrong_kind_tea", tea), ("S03_under_min_lemon", under), ("S04_wrong_coffee_method", wrongMethod), ("S05_long_name_sellable", longName) };
            foreach (var (tag, r) in cases)
            {
                if (r != null)
                {
                    var res = DrinkLab.Craft(r);
                    steps.Add($"   {tag}: API craft {res.Status} {res.Message}");
                    if (res.Status != DrinkLab.CraftStatus.Crafted) { issues.Add($"FAIL {tag}: could not craft state drink: {res.Message}"); continue; }
                }
                Click("TabOrders", "주문·판매 탭");
                yield return Wait(0.5f);
                var labels = new List<string>();
                for (int i = 0; i < DrinkLab.Orders.Count; i++)
                {
                    var b = Btn($"Order_{i}/Sell");
                    labels.Add($"{i + 1}:{(b != null ? b.GetComponentInChildren<Text>().text : "-")}[{(b != null && b.interactable ? "on" : "off")}]");
                }
                steps.Add($"   {tag}: sell buttons {string.Join("  ", labels)}");
                yield return Shot(tag + "_orders", lab.transform);
                if (r != null)
                {
                    Click("TabCraft", "제조 탭");
                    yield return Wait(0.5f);
                    if (lab.Narrow && !lab.ReceiptOpen) { Click("ReceiptToggle", "영수증 열기(좁은 창)"); yield return Wait(0.3f); }
                    yield return Shot(tag + "_craft_receipt", lab.transform);
                    if (!Click("MixPanel/RefundPrepared", "취소·회수")) issues.Add($"FAIL {tag}: refund button unavailable");
                    yield return Wait(0.4f);
                }
            }
        }

        static string StateLine() =>
            $"coins={DrinkLab.Coins};prepared={(DrinkLab.Prepared != null)};gin={DrinkLab.Owned(DrinkIngredients.Gin)};lemon_juice={DrinkLab.Owned(DrinkIngredients.LemonJuice)};orderSerial={DrinkLab.Data.orderSerial};soldDrinks={DrinkLab.Data.soldDrinkIds.Count};orders={string.Join("|", DrinkLab.Orders.Select(o => o.id + ":" + o.state))}";

        IEnumerator Reload()
        {
            // 판매 단계가 남긴 상태와 다시 켠 앱의 저장 상태를 그대로 비교(-cct-firstsale-expected <판매 단계 expected.txt>).
            var args = Environment.GetCommandLineArgs();
            int e = Array.IndexOf(args, "-cct-firstsale-expected");
            string expected = e >= 0 && e + 1 < args.Length && File.Exists(args[e + 1]) ? File.ReadAllText(args[e + 1]).Trim() : null;
            string actual = StateLine();
            steps.Add($"   reloaded state: {actual} notice={DrinkLab.Notice}");
            steps.Add($"   expected state: {expected ?? "(missing)"}");
            if (expected == null) issues.Add("FAIL expected.txt from sale phase missing");
            else if (expected != actual) issues.Add($"FAIL reload state differs: expected '{expected}' actual '{actual}'");
            if (!string.IsNullOrEmpty(DrinkLab.Notice) && DrinkLab.Notice.Contains(DrinkLab.NoticeStarter)) issues.Add("FAIL starter kit granted again on reload");
            var title = FindFirstObjectByType<TitleView>();
            yield return Shot("R01_title_after_restart", title != null ? title.transform : null);
            if (!Click("StartButton", "타이틀 시작")) yield break;
            yield return Wait(0.6f);
            var select = FindFirstObjectByType<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke(); Step("click 음료 제조");
            yield return Wait(0.8f);
            var lab = select.DrinkLabScreen;
            Click("TabOrders", "주문·판매 탭");
            yield return Wait(0.5f);
            yield return Shot("R02_orders_after_restart", lab.transform);
        }

        void Write()
        {
            var sb = new StringBuilder("{\n");
            sb.Append($"  \"phase\": \"{phase}\",\n  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            sb.Append("  \"steps\": [\n").Append(string.Join(",\n", steps.Select(s => $"    \"{Esc(s)}\""))).Append("\n  ],\n");
            sb.Append("  \"issues\": [\n").Append(string.Join(",\n", issues.Select(s => $"    \"{Esc(s)}\""))).Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(output, "firstsale.json"), sb.ToString());
            File.WriteAllText(Path.Combine(output, "screen-text.md"), screenText.ToString());
        }
    }
}
#endif
