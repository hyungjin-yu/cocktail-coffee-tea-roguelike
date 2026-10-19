#if CCT_QA_TOUR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69 QA 플레이어 전용(CCT_QA_TOUR 심볼에서만 컴파일, -cct-data-dir 격리 필수): 실제 런타임 UI로 음료 제조 전 과정을 캡처한다 —
    /// 선택 화면, 제조대 붓기(중간·나눠 붓기), 색 비교(무색 진/큐라소/그레나딘/레몬 탁도), 기법별 모션(셰이킹·스터링·빌드·레이어링·차 우림),
    /// 도감·레시피·공유 코드 가져오기, 실제 제조 → 출발 완성 음료 공개·버프 → HUD 칩·일시정지 안내 → 4킬 포기 보존 결과 → 재입장 →
    /// 보스 음료 재료 → 사망 결과, 출발 저장 실패 안내. 글 상자 넘침과 임시 그림 사용 여부를 report.json에 남긴다.
    /// 색 비교·기법 모션 컷은 같은 렌더러로 재생한 미리보기(재료 차감 없음)이며 실제 제조 컷과 구분해 이름 붙인다.
    /// </summary>
    public class DrinkLabCapture : MonoBehaviour
    {
        string output;
        readonly List<string> shots = new List<string>();
        readonly List<string> overflow = new List<string>();
        readonly List<string> notes = new List<string>();
        int minFontPx = int.MaxValue;
        bool placeholders;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cct-drinklab-capture");
            if (i < 0 || i + 1 >= args.Length || Array.IndexOf(args, "-cct-data-dir") < 0) return;
            Application.runInBackground = true; // QA 빌드만: 포커스 없이(입력·포커스 탈취 없이) 캡처가 진행되게
            var go = new GameObject("DrinkLabCapture");
            DontDestroyOnLoad(go);
            var v = go.AddComponent<DrinkLabCapture>();
            v.output = args[i + 1];
            Directory.CreateDirectory(v.output);
            v.StartCoroutine(v.Run());
        }

        IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Shot(string label, Transform measureRoot)
        {
            yield return null;
            if (measureRoot != null) Measure(label, measureRoot);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, label + ".png"));
            shots.Add(label);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        void Measure(string label, Transform root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var t in root.GetComponentsInChildren<Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || !t.isActiveAndEnabled) continue;
                var rect = t.rectTransform.rect;
                if (t.preferredHeight > rect.height + 1.5f && t.verticalOverflow == VerticalWrapMode.Overflow)
                    overflow.Add($"{label}: {HierPath(t.transform)} pref={t.preferredHeight:0} box={rect.height:0} '{t.text.Replace("\n", "⏎").Substring(0, Math.Min(60, t.text.Length))}'");
                // 잘림(Truncate) 상자가 한 줄보다 낮으면 글이 통째로 사라진다(보이지 않는 제목) — 따로 잡는다.
                if (t.verticalOverflow == VerticalWrapMode.Truncate && !t.resizeTextForBestFit && rect.height < t.fontSize * 1.25f)
                    overflow.Add($"{label}: HIDDEN {HierPath(t.transform)} box={rect.height:0} font={t.fontSize} '{t.text.Replace("\n", "⏎").Substring(0, Math.Min(40, t.text.Length))}'");
                if (t.canvas != null) minFontPx = Mathf.Min(minFontPx, Mathf.RoundToInt(t.fontSize * t.canvas.scaleFactor));
            }
        }

        static string HierPath(Transform t) => t.parent == null ? t.name : HierPath(t.parent) + "/" + t.name;

        static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
        static Button Btn(string name) { var go = GameObject.Find(name); return go != null ? go.GetComponent<Button>() : null; }

        static DrinkRecipe Mix(DrinkKind kind, DrinkTechnique t, string name, params (string id, int amt)[] steps)
        {
            var r = DrinkRecipe.NewDraft(kind);
            r.technique = (int)t;
            r.name = name;
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.amt));
            return r;
        }

        void Load(DrinkLabView lab, DrinkRecipe r)
        {
            lab.Draft.kind = r.kind; lab.Draft.technique = r.technique; lab.Draft.water = r.water; lab.Draft.ice = r.ice; lab.Draft.name = r.name;
            lab.Draft.steps.Clear();
            foreach (var s in r.steps) lab.Draft.steps.Add(new DrinkStep(s.id, s.amount));
            lab.RefreshForTests();
        }

        IEnumerator Preview(DrinkLabView lab, DrinkRecipe r, string label, float midAt, string midLabel, float mid2At = -1f, string mid2Label = null)
        {
            Load(lab, r);
            yield return Wait(0.3f);
            bool done = false;
            lab.Stage.PlayCraft(lab.Draft, false, () => done = true);
            float t = 0f;
            bool shot1 = false, shot2 = false;
            while (!done && t < 12f)
            {
                t += Time.unscaledDeltaTime;
                if (!shot1 && t >= midAt) { shot1 = true; yield return Shot(midLabel, lab.transform); }
                if (mid2Label != null && !shot2 && t >= mid2At) { shot2 = true; yield return Shot(mid2Label, lab.transform); }
                yield return null;
            }
            yield return Wait(0.4f);
            yield return Shot(label, lab.transform);
            placeholders |= lab.Stage.Placeholders;
        }

        string Hex(Color c) => DrinkCalculator.Hex(c);

        IEnumerator PourStage(DrinkLabView lab, string id, int targetTenths, string tag)
        {
            var def = DrinkIngredients.Get(id);
            var row = Btn("Row_" + id);
            if (row != null) row.onClick.Invoke();
            lab.Stage.Select(def);
            lab.Stage.BeginPour(Vector2.zero);
            int start = lab.Draft.AmountOf(id);
            lab.Stage.SetTilt(115f);
            float g = 0f;
            bool shot = false;
            while (lab.Stage.Pouring && lab.Draft.AmountOf(id) + lab.Stage.InMeasure - start < targetTenths && g < 15f)
            {
                g += Time.unscaledDeltaTime;
                if (!shot && lab.Stage.InMeasure >= Math.Max(1, Math.Min(targetTenths / 2, 60)) && lab.Stage.StreamVisible)
                {
                    shot = true;
                    notes.Add($"{tag} pour: stream={Hex(lab.Stage.StreamColor)} jigger={Hex(lab.Stage.Jigger.TopColor)} expected={Hex(DrinkCalculator.IngredientLook(def))} vesselBefore={Hex(lab.Stage.MainVessel.TopColor)}");
                    yield return Shot(tag + "_a_bottle_stream_and_jigger", lab.transform);
                }
                yield return null;
            }
            Debug.Log($"[DrinkCapture] {tag} stop: lab={(lab != null)} stage={(lab != null && lab.Stage != null)} vessel={(lab != null && lab.Stage != null && lab.Stage.MainVessel != null)}");
            string vesselBefore = Hex(lab.Stage.MainVessel.TopColor);
            lab.Stage.StopPour();
            Debug.Log($"[DrinkCapture] {tag} stopped");
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
            notes.Add($"{tag} transfer: dumpStream={Hex(lab.Stage.DumpStreamColor)} visible={lab.Stage.DumpStreamVisible} progress={lab.Stage.TransferProgress:0.00} jiggerMl={lab.Stage.Jigger.VolumeMl:0.0} vesselBefore={vesselBefore} vesselNow={Hex(lab.Stage.MainVessel.TopColor)}");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, tag + "_b_jigger_into_shaker_transfer.png"));
            shots.Add(tag + "_b_jigger_into_shaker_transfer");
            yield return Wait(0.6f);
            Debug.Log($"[DrinkCapture] {tag} after-wait");
            notes.Add($"{tag} after: vessel={Hex(lab.Stage.MainVessel.TopColor)} subsetMix={Hex(DrinkCalculator.Look(lab.Draft).color)} ml={lab.Stage.MainVessel.VolumeMl:0.0}");
            yield return Shot(tag + "_c_shaker_after_transfer", lab.transform);
        }

        IEnumerator ColourStages(DrinkLabView lab)
        {
            Load(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Shake, "단계 색"));
            yield return Wait(0.3f);
            yield return PourStage(lab, "gin", 300, "C1_gin_clear");
            yield return PourStage(lab, "blue_curacao", 50, "C2_blue");
            yield return PourStage(lab, "grenadine", 50, "C3_grenadine");
            yield return Shot("C4_open_shaker_accumulated_mix", lab.transform);
            bool done = false;
            lab.Stage.PlayCraft(lab.Draft, false, () => done = true);
            float t = 0f; bool s1 = false, s2 = false;
            while (!done && t < 12f)
            {
                t += Time.unscaledDeltaTime;
                if (!s1 && t > 0.8f) { s1 = true; yield return Shot("C5_closed_shaker_shaking_opaque", lab.transform); }
                if (!s2 && t > 2.2f && lab.Stage.StreamVisible)
                {
                    s2 = true;
                    notes.Add($"C6 final pour: stream={Hex(lab.Stage.StreamColor)} final={Hex(DrinkCalculator.Look(lab.Draft).color)}");
                    yield return Shot("C6_final_mix_stream_into_glass", lab.transform);
                }
                yield return null;
            }
            yield return Wait(0.3f);
            notes.Add($"C7 final glass: {Hex(lab.Stage.FinalVessel.TopColor)}");
            yield return Shot("C7_final_drink", lab.transform);
        }

        /// <summary>QA 전용 점 표식(캡처에만 존재, 일반 빌드에는 이 파일이 없다): 빨강 = 회전된 병 입구, 초록 = 지거 입구, 노랑 = 줄기 끝.</summary>
        static Image Marker(Transform parent, Color c)
        {
            var rt = UiFactory.Place("QaMarker", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>오른쪽 패널·제목·재료 행 검수: 최대 재료(칵테일 6종+물·얼음, 효과 4종), 소모 펼침, 부족, 차 최대, 레시피 탭.
        /// 실제 렌더된 글 상자 경계를 서로 겹침·패널 밖 여부로 검사해 report.json에 남긴다.</summary>
        IEnumerator UiLayout()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            // 73 단계2: 영수증 실시간 계량 줄(지거 안 + 떨어지는 중) → 옮긴 뒤 확정 줄
            var st = lab.Stage;
            Btn("Row_gin")?.onClick.Invoke();
            st.Select(DrinkIngredients.Get("gin"));
            st.BeginPour(Vector2.zero);
            st.SetTilt(DrinkStageView.TiltMax);
            float lm = 0f;
            while (st.InMeasure < 120 && lm < 6f) { lm += Time.unscaledDeltaTime; yield return null; }
            st.SetTilt(0f);
            yield return Wait(0.35f);
            CheckOverlap("UI0a_receipt_live_measure", lab.transform);
            yield return Shot("UI0a_receipt_live_measure", lab.transform);
            st.StopPour();
            lm = 0f;
            while (st.Transferring && lm < 8f) { lm += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.4f);
            CheckOverlap("UI0b_receipt_confirmed_line", lab.transform);
            yield return Shot("UI0b_receipt_confirmed_line", lab.transform);
            // 레시피 저장 성공 → 찢기·입고 모션(실제 저장 뒤에만)
            Load(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "", ("gin", 300), ("blue_curacao", 50)));
            yield return Wait(0.3f);
            var nameIn = GameObject.Find("RecipeName")?.GetComponent<InputField>();
            if (nameIn != null) nameIn.text = "푸른 밤 첫 장";
            Btn("SaveRecipe")?.onClick.Invoke();
            float savedAt = Time.realtimeSinceStartup;
            notes.Add($"save recipe: recipes={DrinkLab.Recipes.Count} tear={(RecipeTearOverlay.Current != null)} msg={lab.LastMessage}");
            // 0.5초 모션이라 Shot()의 대기 없이 바로 찍는다(프레임 번호를 기록)
            foreach (var (at, tag) in new[] { (0.12f, "UI0c_recipe_tear_early"), (0.3f, "UI0d_recipe_tear_mid"), (0.47f, "UI0e_recipe_tear_into_box") })
            {
                var tearNow = RecipeTearOverlay.Current;
                while (Time.realtimeSinceStartup - savedAt < at && tearNow != null && !tearNow.Finished) yield return null;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, tag + ".png"));
                shots.Add(tag);
                notes.Add($"{tag}: frame={(RecipeTearOverlay.Current != null ? RecipeTearOverlay.Current.FramesShown : -1)}");
            }
            yield return Wait(1.0f);
            var max = Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "최대 배합", ("gin", 300), ("rum", 200), ("blue_curacao", 150), ("grenadine", 150), ("lemon_juice", 200), ("honey", 150));
            max.water = 50; max.ice = 3;
            Load(lab, max);
            yield return Wait(0.4f);
            CheckOverlap("UI1_max_cocktail", lab.transform);
            yield return Shot("UI1_max_cocktail", lab.transform);
            var toggle = Btn("CostToggle");
            if (toggle != null) toggle.onClick.Invoke();
            yield return Wait(0.4f);
            CheckOverlap("UI2_cost_expanded", lab.transform);
            yield return Shot("UI2_cost_expanded", lab.transform);
            var scroll = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "MixScroll");
            if (scroll != null) { scroll.verticalNormalizedPosition = 0f; yield return Wait(0.3f); yield return Shot("UI3_cost_expanded_scrolled_bottom", lab.transform); }
            var shortMix = Mix(DrinkKind.Cocktail, DrinkTechnique.Shake, "부족", ("gin", 600), ("blue_curacao", 160));
            Load(lab, shortMix);
            yield return Wait(0.4f);
            CheckOverlap("UI4_shortage", lab.transform);
            yield return Shot("UI4_shortage", lab.transform);
            Btn("KindTea").onClick.Invoke();
            yield return Wait(0.5f);
            lab = select.DrinkLabScreen;
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "차 최대", ("black_tea", 30), ("green_tea", 20), ("lemon_juice", 150), ("honey", 150)));
            yield return Wait(0.4f);
            CheckOverlap("UI5_tea_max", lab.transform);
            yield return Shot("UI5_tea_max", lab.transform);
            lab.SetTab(DrinkLabView.Tab.Recipes);
            yield return Wait(0.4f);
            CheckOverlap("UI6_recipes_examples", lab.transform);
            yield return Shot("UI6_recipes_examples", lab.transform);
            lab.SetTab(DrinkLabView.Tab.Codex);
            yield return Wait(0.4f);
            yield return Shot("UI7_codex", lab.transform);
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, placeholderArtUsed = false, version = Application.version,
            }, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        /// <summary>
        /// 73 단계2 전체 UI 상태 검수(사용자 지시): 칵테일/차·종류 전환·재료 목록 맨 위/아래 선택·계량 전/중/옮기는 중/완료·부족·최대·소모 펼침/닫힘·
        /// 영수증 끝까지 스크롤·제조 불가/가능/준비 음료·모션 생략·레시피 없음/여러 개/긴 이름·저장 성공(찢기)/실패·공유 코드 오류/확인·도감 위/끝.
        /// 상태마다 글 겹침, 버튼 도달(고정 버튼은 화면 안, 스크롤 안 버튼은 보이는 영역 또는 스크롤로 도달), 터치 44px(터치 UI), 영수증 마지막 줄 노출을 검사한다.
        /// </summary>
        IEnumerator UiFull()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            int n = 0;
            IEnumerator Snap(string tag)
            {
                Canvas.ForceUpdateCanvases();
                string label = $"F{++n:00}_{tag}";
                CheckOverlap(label, lab.transform);
                CheckReach(label, lab.transform);
                yield return Shot(label, lab.transform);
                if (lab.Narrow && lab.CurrentTab == DrinkLabView.Tab.Craft)
                {
                    // 좁은 창: 같은 상태를 영수증 서랍을 펼쳐 한 번 더(영수증 글·버튼 검사)
                    lab.SetReceiptOpen(true);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    CheckOverlap(label + "_receipt", lab.transform);
                    CheckReach(label + "_receipt", lab.transform);
                    yield return Shot(label + "_receipt", lab.transform);
                    lab.SetReceiptOpen(false);
                    yield return null;
                }
            }
            // 레시피 탭: 비어 있음(첫 저장 전)
            lab.SetTab(DrinkLabView.Tab.Recipes); yield return Wait(0.4f);
            yield return Snap("recipes_empty");
            Assert(!lab.PreviewImport("CCT1-ZZZZ-ZZZZ"), "잘못된 코드 거부");
            yield return Wait(0.2f);
            yield return Snap("import_error");
            lab.SetTab(DrinkLabView.Tab.Craft); yield return Wait(0.4f);
            yield return Snap("cocktail_start_empty_craft_disabled");
            var st = lab.Stage;
            // 재료 목록 맨 아래 선택(꿀) → 맨 위(진)
            var list = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "IngredientList");
            if (list != null) { list.verticalNormalizedPosition = 0f; yield return Wait(0.2f); }
            Btn("Row_honey")?.onClick.Invoke(); yield return Wait(0.3f);
            yield return Snap("list_bottom_select_honey");
            if (list != null) { list.verticalNormalizedPosition = 1f; yield return Wait(0.2f); }
            Btn("Row_gin")?.onClick.Invoke(); yield return Wait(0.3f);
            yield return Snap("list_top_select_gin_before_measure");
            // 계량 중 → 옮기는 중 → 완료
            st.BeginPour(Vector2.zero); st.SetTilt(DrinkStageView.TiltMax);
            float g = 0f; while (st.InMeasure < 150 && g < 6f) { g += Time.unscaledDeltaTime; yield return null; }
            st.SetTilt(0f); yield return Wait(0.35f);
            yield return Snap("measuring_in_jigger");
            st.StopPour();
            g = 0f; while (st.Phase != DrinkStageView.CyclePhase.ToolPour && g < 4f) { g += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.25f);
            yield return Snap("transferring_jigger_to_vessel");
            g = 0f; while (st.Transferring && g < 8f) { g += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.4f);
            yield return Snap("transfer_done_confirmed_line_craft_enabled");
            // 부족 / 최대 / 소모 펼침 / 영수증 끝까지
            Load(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Shake, "부족", ("gin", 600), ("blue_curacao", 160)));
            yield return Wait(0.4f);
            yield return Snap("shortage_craft_disabled");
            var max = Mix(DrinkKind.Cocktail, DrinkTechnique.Layer, "아주 긴 이름의 레시피", ("gin", 300), ("rum", 200), ("blue_curacao", 150), ("grenadine", 150), ("lemon_juice", 200), ("honey", 150));
            max.water = 50; max.ice = 4;
            Load(lab, max); yield return Wait(0.4f);
            yield return Snap("max_cocktail_cost_collapsed");
            Btn("CostToggle")?.onClick.Invoke(); yield return Wait(0.4f);
            yield return Snap("cost_expanded");
            var mixScroll = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "MixScroll");
            if (mixScroll != null) { mixScroll.verticalNormalizedPosition = 0f; yield return Wait(0.3f); CheckLastLineVisible("receipt_bottom", mixScroll); }
            yield return Snap("receipt_scrolled_to_end");
            Btn("CostToggle")?.onClick.Invoke(); yield return Wait(0.3f);
            // 저장 실패(빈 배합) / 성공(찢기) / 여러 개·긴 이름
            Load(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "")); yield return Wait(0.3f);
            var nameIn = GameObject.Find("RecipeName")?.GetComponent<InputField>();
            if (nameIn != null) nameIn.text = "빈 잔";
            Btn("SaveRecipe")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Snap("save_failed_no_tear");
            string[] names = { "푸른 밤", "레몬 진", "럼 하이볼", "꿀 한 방울", "열여섯글자레시피이름입니다" };
            for (int i = 0; i < names.Length; i++)
            {
                Load(lab, Mix(DrinkKind.Cocktail, i % 2 == 0 ? DrinkTechnique.Stir : DrinkTechnique.Build, "", ("gin", 200 + i * 50), ("blue_curacao", 30 + i * 10)));
                yield return Wait(0.2f);
                if (nameIn != null) nameIn.text = names[i];
                Btn("SaveRecipe")?.onClick.Invoke();
                if (i == 0) { yield return Wait(0.2f); yield return Snap("save_success_tear_motion"); }
                yield return Wait(0.8f);
            }
            notes.Add($"saved recipes={DrinkLab.Recipes.Count}");
            lab.SetTab(DrinkLabView.Tab.Recipes); yield return Wait(0.4f);
            yield return Snap("recipes_many_long_name");
            var recipesScroll = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "RecipeList");
            if (recipesScroll != null) { recipesScroll.verticalNormalizedPosition = 0f; yield return Wait(0.3f); CheckLastLineVisible("recipes_bottom", recipesScroll); }
            yield return Snap("recipes_scrolled_to_end");
            var code = lab.CopyCode(0);
            Assert(lab.PreviewImport(code), "공유 코드 확인");
            yield return Wait(0.2f);
            yield return Snap("import_preview_ok");
            // 도감 위/끝
            lab.SetTab(DrinkLabView.Tab.Codex); yield return Wait(0.4f);
            yield return Snap("codex_top");
            var codexScroll = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "CodexList");
            if (codexScroll != null) { codexScroll.verticalNormalizedPosition = 0f; yield return Wait(0.3f); CheckLastLineVisible("codex_bottom", codexScroll); }
            yield return Snap("codex_scrolled_to_end");
            // 차: 종류 전환·최대
            lab.SetTab(DrinkLabView.Tab.Craft); yield return Wait(0.3f);
            Btn("KindTea")?.onClick.Invoke(); yield return Wait(0.5f);
            lab = select.DrinkLabScreen;
            yield return Snap("tea_switched");
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "차 최대", ("black_tea", 30), ("green_tea", 20), ("lemon_juice", 150), ("honey", 150)));
            yield return Wait(0.4f);
            yield return Snap("tea_max");
            // 모션 생략 토글 → 제조 → 준비 음료(새 제조 불가)
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "완성 차", ("black_tea", 20)));
            yield return Wait(0.3f);
            Btn("SkipMotion")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Snap("skip_motion_on");
            lab.ConfirmCraft(); yield return Wait(1.0f);
            yield return Snap("prepared_drink_craft_blocked");
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, placeholderArtUsed = lab.Stage != null && lab.Stage.Placeholders, version = Application.version,
            }, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        void Assert(bool ok, string what) { if (!ok) overflow.Add("ASSERT FAILED: " + what); }

        /// <summary>버튼 도달: 고정 버튼은 화면 안에 다 있어야 하고, 스크롤 안 버튼은 스크롤 영역과 화면 안(스크롤 영역 자체가 화면 안). 터치 UI면 높이·폭 44px 이상.</summary>
        void CheckReach(string label, Transform root)
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            int bad = 0, checkedN = 0, small = 0;
            var labView = root.GetComponentInParent<DrinkLabView>() ?? root.GetComponentInChildren<DrinkLabView>();
            foreach (var b in root.GetComponentsInChildren<Button>(false))
            {
                if (!b.isActiveAndEnabled) continue;
                // 좁은 창의 접힌 영수증 서랍(화면 밖)은 여닫기 버튼으로 도달 — 펼친 상태 캡처에서 따로 검사한다.
                if (labView != null && labView.Narrow && !labView.ReceiptOpen && HierPath(b.transform).Contains("/MixPanel/")) continue;
                var rt = (RectTransform)b.transform;
                var c = new Vector3[4]; rt.GetWorldCorners(c);
                var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                checkedN++;
                var scroll = b.GetComponentInParent<ScrollRect>();
                Rect area = screen;
                if (scroll != null)
                {
                    var vc = new Vector3[4]; ((RectTransform)scroll.transform).GetWorldCorners(vc);
                    area = Rect.MinMaxRect(vc[0].x, vc[0].y, vc[2].x, vc[2].y);
                    if (area.xMin < -1 || area.yMin < -1 || area.xMax > Screen.width + 1 || area.yMax > Screen.height + 1) { overflow.Add($"{label}: REACH scroll area off-screen {HierPath(scroll.transform)}"); bad++; }
                    if (!area.Overlaps(r)) continue; // 지금 보이지 않는 줄: 스크롤로 도달
                    // 보이는 영역 경계에 걸쳐 일부만 보이는 것은 스크롤로 마저 볼 수 있으므로 허용
                }
                else if (r.xMin < -1 || r.yMin < -1 || r.xMax > Screen.width + 1 || r.yMax > Screen.height + 1) { overflow.Add($"{label}: REACH off-screen {HierPath(b.transform)}"); bad++; }
                if (MobilePlatform.IsTouchUI && (r.height < 43.5f || r.width < 43.5f)) { overflow.Add($"{label}: TOUCH small {HierPath(b.transform)} {r.width:0}x{r.height:0}px"); small++; }
            }
            notes.Add($"{label}: buttons={checkedN} reachIssues={bad} touchSmall={small}");
        }

        /// <summary>스크롤을 끝까지 내렸을 때 내용의 마지막 자식이 보이는 영역 안에 완전히 들어오는지.</summary>
        void CheckLastLineVisible(string label, ScrollRect scroll)
        {
            Canvas.ForceUpdateCanvases();
            var content = scroll.content;
            RectTransform last = null; float lowest = float.MaxValue;
            foreach (RectTransform ch in content)
            {
                if (!ch.gameObject.activeSelf) continue;
                var c = new Vector3[4]; ch.GetWorldCorners(c);
                if (c[0].y < lowest) { lowest = c[0].y; last = ch; }
            }
            var vc = new Vector3[4]; ((RectTransform)scroll.transform).GetWorldCorners(vc);
            bool ok = last == null || lowest >= vc[0].y - 1f;
            notes.Add($"{label}: last={(last != null ? last.name : "-")} bottom={lowest:0} viewportBottom={vc[0].y:0} visible={ok}");
            if (!ok) overflow.Add($"{label}: LAST LINE HIDDEN {(last != null ? last.name : "")}");
        }

        /// <summary>렌더된 글자 실제 높이(preferredHeight)를 반영한 화면 사각형끼리 겹치는지, 글자가 자기 패널 밖으로 나가는지.</summary>
        void CheckOverlap(string label, Transform root)
        {
            Canvas.ForceUpdateCanvases();
            var texts = root.GetComponentsInChildren<Text>(false).Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)).ToList();
            var rects = new List<(Text t, Rect r)>();
            foreach (var t in texts)
            {
                var corners = new Vector3[4];
                t.rectTransform.GetWorldCorners(corners);
                var r = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                float scale = t.canvas != null ? t.canvas.scaleFactor : 1f;
                float pref = t.preferredHeight * scale;
                // 실제 글자 높이: 가운데 정렬이면 위아래로, 위 정렬이면 아래로 늘어난다
                if (pref > r.height)
                {
                    bool upper = t.alignment == TextAnchor.UpperLeft || t.alignment == TextAnchor.UpperCenter || t.alignment == TextAnchor.UpperRight;
                    r = upper ? Rect.MinMaxRect(r.xMin, r.yMax - pref, r.xMax, r.yMax) : Rect.MinMaxRect(r.xMin, r.center.y - pref / 2, r.xMax, r.center.y + pref / 2);
                    if (t.verticalOverflow == VerticalWrapMode.Overflow) overflow.Add($"{label}: TALL {HierPath(t.transform)} pref={pref:0} box={r.height:0}");
                }
                // 글 폭은 실제 줄 폭으로 줄인다(왼쪽/오른쪽 정렬)
                float w = Mathf.Min(r.width, t.preferredWidth * scale);
                bool right = t.alignment == TextAnchor.MiddleRight || t.alignment == TextAnchor.UpperRight || t.alignment == TextAnchor.LowerRight;
                bool center = t.alignment == TextAnchor.MiddleCenter || t.alignment == TextAnchor.UpperCenter || t.alignment == TextAnchor.LowerCenter;
                r = right ? Rect.MinMaxRect(r.xMax - w, r.yMin, r.xMax, r.yMax) : center ? Rect.MinMaxRect(r.center.x - w / 2, r.yMin, r.center.x + w / 2, r.yMax) : Rect.MinMaxRect(r.xMin, r.yMin, r.xMin + w, r.yMax);
                // 스크롤 영역 밖(가려진) 글자는 화면에 없으므로 제외
                var mask = t.GetComponentInParent<RectMask2D>();
                if (mask != null)
                {
                    var mc = new Vector3[4];
                    ((RectTransform)mask.transform).GetWorldCorners(mc);
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
                    if (ox > 2f && oy > 2f) { overflow.Add($"{label}: OVERLAP '{rects[a].t.text.Split('\n')[0]}' x '{rects[b].t.text.Split('\n')[0]}' ({ox:0}x{oy:0}px)"); n++; }
                }
            notes.Add($"{label}: checked {rects.Count} visible text boxes, overlaps={n}");
        }

        /// <summary>연속 동작 증거: 실제 드래그(누르기→끌기→유지)로 지거가 두 번 차 자동 옮겨 붓기·복귀·재개, 보유량 끝에서 멈춤 → 마지막 옮겨 붓기.
        /// 이어서 스터링(믹싱 글라스 → 잔), 차(물 용기 → 다관 → 찻잔). 15fps 프레임 PNG와 매 프레임 CSV(단계·자세·입구·양·줄기).</summary>
        IEnumerator CycleEvidence()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            var stage = lab.Stage;
            var csv = new System.Text.StringBuilder("seq,time,phase,bottleTilt,bottleX,bottleY,toolRot,toolX,toolY,jiggerMl,vesselMl,serveMl,stream,dumpStream,lipX,lipY,mouthX,mouthY,draftGin\n");
            var framesDir = System.IO.Path.Combine(output, "frames");
            Directory.CreateDirectory(framesDir);
            float start = Time.realtimeSinceStartup, nextShot = 0f;
            int frameNo = 0;
            string seq = "pour";
            void Row()
            {
                var g = stage.Phase == DrinkStageView.CyclePhase.ToolPour ? stage.TransferGeometry : stage.Geometry;
                var tool = stage.Jigger.Rect;
                csv.Append($"{seq},{Time.realtimeSinceStartup - start:0.000},{stage.Phase},{stage.Tilt:0.0},{stage.BottleRect.anchoredPosition.x:0.0},{stage.BottleRect.anchoredPosition.y:0.0},{tool.localEulerAngles.z:0.0},{tool.anchoredPosition.x:0.0},{tool.anchoredPosition.y:0.0},{stage.Jigger.VolumeMl:0.00},{stage.MainVessel.VolumeMl:0.00},{(stage.ServeVessel != null ? stage.ServeVessel.VolumeMl : 0f):0.00},{stage.StreamVisible},{stage.DumpStreamVisible},{g.lip.x:0.0},{g.lip.y:0.0},{g.mouth.x:0.0},{g.mouth.y:0.0},{lab.Draft.AmountOf("gin")}\n");
            }
            IEnumerator Frame()
            {
                Row();
                float now = Time.realtimeSinceStartup - start;
                if (now >= nextShot)
                {
                    nextShot = now + 1f / 15f;
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"{seq}_{frameNo++:0000}.png"));
                }
                yield return null;
            }

            stage.Select(DrinkIngredients.Get("gin"));
            yield return Wait(0.4f);
            // 실제 드래그 경로: 병을 누르고 오른쪽으로 끌어 유지(입력 → 목표 기울기 → 병이 일정 속도로 따라감)
            var canvas = stage.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            var sc = new Vector3[4]; ((RectTransform)stage.transform).GetWorldCorners(sc); var centre = (Vector2)((sc[0] + sc[2]) * 0.5f); // 오버레이 캔버스: 월드 = 화면
            var press = centre + new Vector2(-200f * scale, 0f);
            stage.BeginPour(press);
            float g0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - g0 < 0.6f) { stage.DragTo(Vector2.Lerp(press, press + new Vector2(260f * scale, 0f), (Time.realtimeSinceStartup - g0) / 0.6f)); yield return Frame(); }
            while (stage.Pouring && Time.realtimeSinceStartup - g0 < 25f) { stage.DragTo(press + new Vector2(260f * scale, 0f)); yield return Frame(); }
            // 보유량(60.0mL)에서 자동 정지 → 마지막 옮겨 붓기·복귀
            while (stage.Transferring && Time.realtimeSinceStartup - g0 < 30f) yield return Frame();
            for (int i = 0; i < 10; i++) yield return Frame();
            notes.Add($"cycle pour: draft gin={DrinkRules.Format(lab.Draft.AmountOf("gin"))}mL dumps={stage.MeasureDumps} reason={stage.StopReason} seconds={Time.realtimeSinceStartup - g0:0.00}");

            // 스터링: 믹싱 글라스 → 잔
            seq = "stir"; frameNo = 0;
            Load(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "스터링", ("gin", 450), ("rum", 150), ("grenadine", 50)));
            yield return Wait(0.3f);
            bool done = false;
            lab.Stage.PlayCraft(lab.Draft, false, () => done = true);
            float s0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - s0 < 15f) yield return Frame();
            for (int i = 0; i < 8; i++) yield return Frame();

            // 차: 물 용기 → 다관 → 찻잔
            seq = "tea"; frameNo = 0;
            Btn("KindTea").onClick.Invoke();
            yield return Wait(0.5f);
            lab = select.DrinkLabScreen;
            stage = lab.Stage;
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "홍차", ("black_tea", 30), ("honey", 100)));
            yield return Wait(0.3f);
            done = false;
            stage.PlayCraft(lab.Draft, false, () => done = true);
            s0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - s0 < 15f) yield return Frame();
            for (int i = 0; i < 8; i++) yield return Frame();
            notes.Add($"serve geometry last: lip=({stage.ServeGeometry.lip.x:0.0},{stage.ServeGeometry.lip.y:0.0}) mouth=({stage.ServeGeometry.mouth.x:0.0},{stage.ServeGeometry.mouth.y:0.0}) rot={stage.ServeGeometry.rotation:0.0}");

            File.WriteAllText(System.IO.Path.Combine(output, "cycle.csv"), csv.ToString());
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = 0, placeholderArtUsed = stage.Placeholders, version = Application.version,
            }, true));
            yield return Wait(1.0f);
            Application.Quit();
        }

        /// <summary>
        /// 73 단계1 검수 증거(QA 표식 없음): 실제 드래그 입력으로 기준 1회 전과정 —
        /// 스터링(진·블루 큐라소·레몬: 병 → 지거 → 믹싱 글라스 → 바스푼 → 얼음 2개 잔)과 차(홍차 통 → 다하 → 다관, 꿀 지거 → 다관, 물 용기 → 다관, 우림, 찻잔).
        /// 매 프레임 장부 CSV(원천/공중/대상 양, 성분 합, 잎·추출분, 스푼 끝·극점·관통 픽셀)와 연속 프레임 PNG. 이어서 빌드 잔 얼음 0/2/4/최대 비교.
        /// </summary>
        IEnumerator Stage1Evidence(bool noShots = false)
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            var stage = lab.Stage;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var csv = new System.Text.StringBuilder("seq,frame,time,phase,current,bottleTilt,toolTilt,bottleMl,airToMeasureMl,measureMl,airToVesselMl,vesselMl,airToServeMl,serveMl,kettleMl,componentTotalMl,ledgerLiquidMl,tinG,chahaG,potLeafG,dissolvedG,solidsG,teaStrength,dtMs,stream,streamEmitting,dump,dumpEmitting,jiggerViewMl,vesselViewMl,serveViewMl,serveSurface01,serveHeadspaceMl,spoonOn,spoonTipX,spoonTipY,spoonAboveFloorPx,spoonOutside,spoonLeftClearPx,spoonRightClearPx,spoonMinX,spoonMaxX,toolLipX,toolLipY,serveLipX,serveLipY,jiggerColor,vesselTopColor,vesselBottomColor,vesselLayers,serveTopColor,streamColor,dumpColor,lidOpen,stageLabel,camX,camTarget,vesselX,serveX\n");
            var framesDir = System.IO.Path.Combine(output, "frames");
            Directory.CreateDirectory(framesDir);
            float start = Time.realtimeSinceStartup, nextShot = 0f;
            int frameNo = 0;
            string seq = "stir";
            bool everyFrame = false;
            string F(double v) => v.ToString("0.000", inv);
            void Row(string file)
            {
                var L = stage.Ledger;
                var sp = stage.Spoon;
                bool spoonOn = stage.SpoonRect != null && stage.SpoonRect.gameObject.activeSelf;
                string cur = stage.CurrentId ?? "";
                double comp = cur != "" ? L.ComponentTotalMl(cur) : 0;
                var serveV = stage.ServeVessel;
                csv.Append(string.Join(",", seq, file, F(Time.realtimeSinceStartup - start), stage.Phase, cur, F(stage.Tilt), F(stage.ToolTilt),
                    F(L[DrinkStageView.LedgerBottle].LiquidMl), F(L.InFlightMl(DrinkStageView.LedgerMeasure)), F(L[DrinkStageView.LedgerMeasure].LiquidMl),
                    F(L.InFlightMl(DrinkStageView.LedgerVessel)), F(L[DrinkStageView.LedgerVessel].LiquidMl), F(L.InFlightMl(DrinkStageView.LedgerServe)), F(L[DrinkStageView.LedgerServe].LiquidMl),
                    F(L[DrinkStageView.LedgerKettle].LiquidMl), F(comp), F(L.TotalLiquidMl()),
                    F(L[DrinkStageView.LedgerBottle].LeafG), F(L[DrinkStageView.LedgerMeasure].LeafG), F(L[DrinkStageView.LedgerVessel].LeafG),
                    F(L[DrinkStageView.LedgerVessel].DissolvedG + L[DrinkStageView.LedgerServe].DissolvedG), F(L.SolidsTotalG()), F(Math.Max(L[DrinkStageView.LedgerVessel].TeaStrength, L[DrinkStageView.LedgerServe].TeaStrength)), F(Time.unscaledDeltaTime * 1000.0),
                    stage.StreamVisible, stage.StreamEmitting, stage.DumpStreamVisible, stage.DumpStreamEmitting,
                    F(stage.Jigger.VolumeMl), F(stage.MainVessel != null ? stage.MainVessel.VolumeMl : 0), F(serveV != null && serveV.gameObject.activeSelf ? serveV.VolumeMl : 0),
                    F(serveV != null ? serveV.Level01 : 0), F(serveV != null ? serveV.HeadspaceMl : 0),
                    spoonOn, F(sp.tip.x), F(sp.tip.y), F(sp.tipAboveFloorPx), sp.outsidePixels, F(sp.leftClearPx), F(sp.rightClearPx), F(sp.minX), F(sp.maxX),
                    F(stage.TransferGeometry.lip.x), F(stage.TransferGeometry.lip.y), F(stage.ServeGeometry.lip.x), F(stage.ServeGeometry.lip.y),
                    Hex(stage.Jigger.TopColor), Hex(stage.MainVessel != null ? stage.MainVessel.TopColor : Color.clear), Hex(stage.MainVessel != null ? stage.MainVessel.BottomColor : Color.clear), stage.MainVessel != null ? stage.MainVessel.LayerCount : 0,
                    Hex(serveV != null && serveV.gameObject.activeSelf ? serveV.TopColor : Color.clear), stage.StreamVisible ? Hex(stage.StreamColor) : "", stage.DumpStreamVisible ? Hex(stage.DumpStreamColor) : "", F(stage.LidOpen01), stage.FinishText.Replace(",", " "), F(stage.CameraX), F(stage.CameraTarget),
                    F(stage.MainVessel != null ? stage.MainVessel.Rect.anchoredPosition.x : 0), F(serveV != null && serveV.gameObject.activeSelf ? serveV.Rect.anchoredPosition.x : 0)) + "\n");
            }
            IEnumerator Frame()
            {
                float now = Time.realtimeSinceStartup - start;
                string file = "";
                if (!noShots && (everyFrame || now >= nextShot))
                {
                    nextShot = now + 1f / 20f;
                    file = $"{seq}_{frameNo++:0000}";
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, file + ".png"));
                }
                Row(file);
                yield return null;
            }
            var canvas = stage.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            IEnumerator Drag(string id, int tenths)
            {
                var row = Btn("Row_" + id);
                if (row != null) row.onClick.Invoke();
                stage.Select(DrinkIngredients.Get(id));
                for (int i = 0; i < 8; i++) yield return Frame();
                var sc = new Vector3[4]; ((RectTransform)stage.transform).GetWorldCorners(sc);
                var press = (Vector2)((sc[0] + sc[2]) * 0.5f) + new Vector2(-200f * scale, 0f);
                stage.BeginPour(press);
                int before = lab.Draft.AmountOf(id);
                float g0 = Time.realtimeSinceStartup;
                // 누르고 오른쪽으로 끌어(실제 입력 경로) 원하는 양에서 놓는다
                while (stage.Pouring && lab.Draft.AmountOf(id) + stage.InMeasure - before < tenths && Time.realtimeSinceStartup - g0 < 20f)
                {
                    float k = Mathf.Clamp01((Time.realtimeSinceStartup - g0) / 0.6f);
                    stage.DragTo(Vector2.Lerp(press, press + new Vector2(230f * scale, 0f), k));
                    yield return Frame();
                }
                stage.StopPour();
                while ((stage.Transferring || stage.Ledger.HasInFlight()) && Time.realtimeSinceStartup - g0 < 30f) yield return Frame();
                for (int i = 0; i < 10; i++) yield return Frame();
                notes.Add($"{seq} drag {id}: draft={DrinkRules.Format(lab.Draft.AmountOf(id))} vesselLedger={stage.Ledger[DrinkStageView.LedgerVessel].Liquid.ComponentVolume(id):0.000}mL/{stage.Ledger[DrinkStageView.LedgerVessel].Leaf(id):0.000}g dumps={stage.MeasureDumps}");
            }
            IEnumerator Craft()
            {
                bool done = false;
                lab.Stage.PlayCraft(lab.Draft, false, () => done = true);
                float s0 = Time.realtimeSinceStartup;
                while (!done && Time.realtimeSinceStartup - s0 < 20f)
                {
                    everyFrame = stage.SpoonRect != null && stage.SpoonRect.gameObject.activeSelf; // 바스푼 구간은 매 프레임(극점 검수)
                    yield return Frame();
                }
                everyFrame = false;
                // 완성 뒤 최소 1.5초: 안정 상태에서 제조 그릇이 다시 채워지거나 양이 바뀌지 않는지 CSV·프레임으로 남긴다.
                float after = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - after < 1.5f) yield return Frame();
            }

            // ── 기준 1회: 스터링 ──
            var stirR = Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "기준 스터링");
            stirR.ice = 2;
            Load(lab, stirR);
            yield return Wait(0.4f);
            yield return Drag("gin", 300);
            yield return Drag("blue_curacao", 100);
            yield return Drag("lemon_juice", 150);
            yield return Craft();
            var sv = stage.FinalVessel;
            notes.Add($"stir final: serveLedger={stage.Ledger[DrinkStageView.LedgerServe].LiquidMl:0.000}mL gin={stage.Ledger[DrinkStageView.LedgerServe].Liquid.ComponentVolume("gin"):0.000} blue={stage.Ledger[DrinkStageView.LedgerServe].Liquid.ComponentVolume("blue_curacao"):0.000} lemon={stage.Ledger[DrinkStageView.LedgerServe].Liquid.ComponentVolume("lemon_juice"):0.000} view={sv.VolumeMl:0.00} ice={sv.IceCount} headspace={sv.HeadspaceMl:0.0} free={(sv.IceSolution != null ? sv.IceSolution.FreeVolumeMl : 0):0.0} cap={sv.CapacityMl}");
            yield return Shot("S1_stir_final_glass", null);
            // 출발 완성 공개(같은 배합): 같은 고정 잔 용량·최종 물리 상태여야 한다
            bool revealDone = false;
            DrinkRevealOverlay.Play(new PreparedDrink { recipe = lab.Draft.Clone(), state = PreparedDrink.Ready }, () => revealDone = true);
            yield return Wait(1.3f);
            var rv = DrinkRevealOverlay.Current != null ? DrinkRevealOverlay.Current.Vessel : null;
            if (rv != null)
                notes.Add($"reveal: vessel={rv.AssetId} view={rv.VolumeMl:0.00}mL surface01={rv.Level01:0.000} ice={rv.IceCount} free={(rv.IceSolution != null ? rv.IceSolution.FreeVolumeMl : 0):0.0} cap={rv.CapacityMl} | stage final surface01={sv.Level01:0.000} free={(sv.IceSolution != null ? sv.IceSolution.FreeVolumeMl : 0):0.0}");
            yield return Shot("S1b_reveal_same_drink", null);
            if (DrinkRevealOverlay.Current != null) DrinkRevealOverlay.Current.Finish();
            while (!revealDone) yield return null;
            yield return Wait(0.3f);

            // ── 기준 1회: 차 ──
            seq = "tea"; frameNo = 0;
            Btn("KindTea").onClick.Invoke();
            yield return Wait(0.5f);
            lab = select.DrinkLabScreen;
            stage = lab.Stage;
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "기준 홍차"));
            yield return Wait(0.4f);
            yield return Drag("black_tea", 25);
            yield return Drag("honey", 100);
            yield return Craft();
            var cup = stage.Ledger[DrinkStageView.LedgerServe]; var pot = stage.Ledger[DrinkStageView.LedgerVessel];
            notes.Add($"tea final: cupWater={cup.WaterMl:0.000} cupHoney={cup.Liquid.ComponentVolume("honey"):0.000} cupLeaf={cup.LeafG:0.000} cupDissolved={cup.DissolvedG:0.0000} potLeaf={pot.LeafG:0.0000} potLiquid={pot.LiquidMl:0.000} strength={cup.TeaStrength:0.000} solids={stage.Ledger.SolidsTotalG():0.0000} view={stage.FinalVessel.VolumeMl:0.00} cap={stage.FinalVessel.CapacityMl}");
            yield return Shot("S2_tea_final_cup", null);
            File.WriteAllText(System.IO.Path.Combine(output, noShots ? "stage1_perf.csv" : "stage1.csv"), csv.ToString());

            // ── 얼음 0/2/4 및 최대 비교(빌드 잔, 같은 액체량) ──
            Btn("KindCocktail")?.onClick.Invoke();
            yield return Wait(0.5f);
            lab = select.DrinkLabScreen;
            stage = lab.Stage;
            foreach (var (ice, gin, gren, tag) in new[] { (0, 400, 200, "ice0_60ml"), (2, 400, 200, "ice2_60ml"), (4, 400, 200, "ice4_60ml"), (4, 0, 0, "ice4_empty"), (4, 900, 300, "ice4_max120ml") })
            {
                var r = Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "얼음 " + ice, ("gin", gin), ("grenadine", gren));
                if (gin == 0) r.steps.Clear();
                if (tag == "ice4_max120ml") r.water = 0;
                r.ice = ice;
                Load(lab, r);
                yield return Wait(1.2f);
                var v = stage.MainVessel;
                var s = v.IceSolution;
                double liquid = stage.Ledger[DrinkStageView.LedgerVessel].LiquidMl;
                notes.Add($"ICE {tag}: ice={v.IceCount} liquidLedger={liquid:0.000} liquidView={v.VolumeMl:0.000} surface01={v.Level01:0.000} submerged={(s != null ? s.SubmergedIceVolume : 0):0.00} free={(s != null ? s.FreeVolumeMl : v.CapacityMl - v.VolumeMl):0.0} cap={v.CapacityMl} fillPct={(v.CapacityMl - (s != null ? s.FreeVolumeMl : v.CapacityMl - v.VolumeMl)) / v.CapacityMl * 100:0.0} cubes=" +
                          (s != null ? string.Join(";", s.Cubes.Select(c => $"{c.Support}@{c.Bottom01:0.000}-{c.Top01:0.000}")) : ""));
                yield return Shot("I_" + tag, null);
                if (gin > 0)
                {
                    // 같은 잔을 침강이 끝난 뒤(무거운 그레나딘이 바닥) 한 번 더: 수위·얼음 위치는 층 배치와 무관하게 같아야 한다.
                    yield return Wait(5.5f);
                    notes.Add($"ICE {tag}_settled: liquidLedger={stage.Ledger[DrinkStageView.LedgerVessel].LiquidMl:0.000} liquidView={v.VolumeMl:0.000} surface01={v.Level01:0.000} free={(v.IceSolution != null ? v.IceSolution.FreeVolumeMl : v.CapacityMl - v.VolumeMl):0.0} bottom={Hex(v.BottomColor)} top={Hex(v.TopColor)}");
                    yield return Shot("I_" + tag + "_settled", null);
                }
            }

            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = 0, placeholderArtUsed = stage.Placeholders, version = Application.version,
            }, true));
            yield return Wait(1.0f);
            Application.Quit();
        }

        IEnumerator ReferencePour()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            var stage = lab.Stage;
            foreach (var id in new[] { "gin", "blue_curacao", "grenadine" })
            {
                var def = DrinkIngredients.Get(id);
                var row = Btn("Row_" + id);
                if (row != null) row.onClick.Invoke();
                stage.Select(def);
                yield return Wait(0.3f);
                var mLip = Marker(stage.transform, Color.red);
                var mMouth = Marker(stage.transform, Color.green);
                var mEnd = Marker(stage.transform, Color.yellow);
                stage.BeginPour(Vector2.zero);
                foreach (float tilt in new[] { 0f, 45f, 80f, 100f, 135f })
                {
                    stage.SetTilt(tilt);
                    float t = 0f;
                    while (t < 0.6f) { t += Time.unscaledDeltaTime; stage.SetTilt(tilt); yield return null; }
                    var g = stage.Geometry;
                    mLip.rectTransform.anchoredPosition = g.lip; mMouth.rectTransform.anchoredPosition = g.mouth; mEnd.rectTransform.anchoredPosition = g.streamEnd;
                    mEnd.gameObject.SetActive(stage.StreamVisible);
                    foreach (var m in new[] { mLip, mMouth, mEnd }) m.transform.SetAsLastSibling();
                    var streamRt = stage.transform.Find("Scene/Stream") as RectTransform;
                    notes.Add($"REF {id} tilt={tilt:0} rot={g.rotation:0.0} pivot=({g.pivot.x:0.00},{g.pivot.y:0.00}) lip=({g.lip.x:0.0},{g.lip.y:0.0}) mouth=({g.mouth.x:0.0},{g.mouth.y:0.0}) " +
                              $"flow={stage.StreamVisible} streamTop=({streamRt.anchoredPosition.x:0.0},{streamRt.anchoredPosition.y:0.0}) streamLen={streamRt.sizeDelta.y:0.0} width={streamRt.sizeDelta.x:0.0} streamRot={streamRt.localEulerAngles.z:0.0} " +
                              $"color={Hex(stage.StreamColor)} jiggerMl={stage.Jigger.VolumeMl:0.0} jiggerColor={Hex(stage.Jigger.TopColor)}");
                    yield return Shot($"REF_{id}_tilt{tilt:000}", lab.transform);
                }
                stage.StopPour();
                yield return Wait(0.2f);
                Destroy(mLip.gameObject); Destroy(mMouth.gameObject); Destroy(mEnd.gameObject);
                yield return Wait(0.8f);
            }
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, placeholderArtUsed = stage.Placeholders, version = Application.version,
            }, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        /// <summary>
        /// 72·73 단계3/4 실제 화면 증거(격리 저장 QA 전용 준비: 코인·RP를 직접 넣는다): 셰이킹·빌드·레이어링 제조 카메라 연속 프레임,
        /// 주문·판매 탭(조건 미충족 → 기준 배합 제조 → 판매 → 외형 구매·장착), 연구 노트북 열기 프레임·숙련 트리·직원 채용·기존 강화·닫기.
        /// </summary>
        IEnumerator ProgressEvidence()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            yield return Shot("P00_select_laptop_button", select.transform);
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            var stage = lab.Stage;
            var framesDir = System.IO.Path.Combine(output, "frames");
            Directory.CreateDirectory(framesDir);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var csv = new System.Text.StringBuilder("seq,frame,time,camX,camTarget,finalX,label\n");
            // ── 제조 카메라: 셰이킹·빌드·레이어링(모션 보기) ──
            foreach (var (tech, tag) in new[] { (DrinkTechnique.Shake, "shake"), (DrinkTechnique.Build, "build"), (DrinkTechnique.Layer, "layer") })
            {
                var r = Mix(DrinkKind.Cocktail, tech, "카메라 " + tag, ("gin", 300), ("grenadine", 100), ("blue_curacao", 50));
                r.ice = tech == DrinkTechnique.Shake ? 2 : 1;
                Load(lab, r);
                yield return Wait(0.8f);
                bool done = false;
                stage.PlayCraft(lab.Draft, false, () => done = true);
                float s0 = Time.realtimeSinceStartup, next = 0f, doneAt = -1f;
                int n = 0;
                while (Time.realtimeSinceStartup - s0 < 25f)
                {
                    float now = Time.realtimeSinceStartup - s0;
                    if (done && doneAt < 0f) doneAt = now;
                    if (doneAt >= 0f && now - doneAt > 1.2f) break; // 완성 뒤 카메라가 완성 잔으로 자리 잡는 구간까지
                    string file = "";
                    if (now >= next) { next = now + 0.1f; file = $"{tag}_{n++:000}"; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, file + ".png")); }
                    var fv = stage.FinalVessel;
                    csv.Append(string.Join(",", tag, file, now.ToString("0.000", inv), stage.CameraX.ToString("0.0", inv), stage.CameraTarget.ToString("0.0", inv),
                        (fv != null && done ? fv.Rect.anchoredPosition.x : 0f).ToString("0.0", inv), stage.FinishText.Replace(",", " "))).Append('\n');
                    yield return null;
                }
                yield return Wait(0.3f);
                notes.Add($"CAMERA {tag}: camX={stage.CameraX:0.0} final={stage.FinalVessel.Rect.anchoredPosition.x:0.0} vessel={stage.MainVessel.AssetId}");
                yield return Shot($"P1_{tag}_final_centered", lab.transform);
            }
            File.WriteAllText(System.IO.Path.Combine(output, "camera.csv"), csv.ToString());

            // ── 주문·판매 ──
            lab.SetTab(DrinkLabView.Tab.Orders); yield return Wait(0.4f);
            yield return Shot("P20_orders_no_prepared", lab.transform);
            var order0 = DrinkLab.Orders[0];
            var cr = DrinkLab.Craft(DrinkSales.Reference(order0));
            notes.Add($"ORDERS craft reference of order0 ({DrinkSales.Describe(order0)}): {cr.Status} price={DrinkSales.Price(order0, DrinkLab.Prepared != null ? DrinkLab.Prepared.recipe : null)}");
            lab.SetTab(DrinkLabView.Tab.Orders); yield return Wait(0.4f);
            yield return Shot("P21_orders_prepared_matches_order1", lab.transform);
            Btn("Sell")?.onClick.Invoke(); yield return Wait(0.4f);
            notes.Add($"ORDERS after sell: coins={DrinkLab.Coins} prepared={(DrinkLab.Prepared != null)} order0={DrinkLab.Orders[0].id}");
            yield return Shot("P22_orders_sold_coins", lab.transform);
            DrinkLab.Data.coins = 2600; // QA 격리 저장 준비(구매 화면 상태 확인용)
            lab.SetTab(DrinkLabView.Tab.Orders); yield return Wait(0.4f);
            var ordersScroll = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "OrdersList");
            if (ordersScroll != null) { ordersScroll.verticalNormalizedPosition = 0f; yield return Wait(0.3f); }
            yield return Shot("P23_shop_can_buy", lab.transform);
            GameObject.Find("Cosmetic_" + DrinkSales.CafeMap + "/Buy")?.GetComponent<Button>().onClick.Invoke();
            yield return Wait(0.4f);
            GameObject.Find("Cosmetic_" + DrinkSales.CafeMap + "/EquipCosmetic")?.GetComponent<Button>().onClick.Invoke();
            yield return Wait(0.4f);
            ordersScroll = lab.transform.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "OrdersList");
            if (ordersScroll != null) { ordersScroll.verticalNormalizedPosition = 0f; yield return Wait(0.3f); }
            notes.Add($"SHOP: coins={DrinkLab.Coins} coinOwned={string.Join("|", DrinkLab.Data.coinOwned)} map={DrinkLab.EquippedMap}");
            CheckOverlap("P24", lab.transform); CheckReach("P24", lab.transform);
            yield return Shot("P24_shop_map_bought_equipped", lab.transform);

            // ── 레시피 서랍(실제 종이 서류) ──
            for (int i = 0; i < 12; i++)
            {
                var rr = Mix(i % 3 == 2 ? DrinkKind.Tea : DrinkKind.Cocktail, i % 3 == 2 ? DrinkTechnique.Steep : (DrinkTechnique)(1 + i % 4), i == 3 ? "긴 이름 저녁 칵테일 시험용 한잔" : "서랍 레시피 " + (i + 1),
                    i % 3 == 2 ? new[] { ("black_tea", 20 + i), ("honey", 50) } : new[] { ("gin", 200 + i * 10), ("blue_curacao", 30) });
                if (i == 1)
                {
                    // 2번 종이 = 최대 배합(재료 6종 + 물·얼음)·긴 이름: 펼친 종이 본문이 잘리지 않고 스크롤되는지
                    rr = Mix(DrinkKind.Cocktail, DrinkTechnique.Layer, "열여섯글자레시피이름입니다", ("gin", 300), ("rum", 200), ("blue_curacao", 150), ("grenadine", 150), ("lemon_juice", 200), ("honey", 150));
                    rr.water = 50; rr.ice = 4;
                }
                bool saved = DrinkLab.SaveRecipe(rr, -1, out var saveErr);
                if (!saved) notes.Add($"DRAWER seed {i} not saved: {saveErr}");
            }
            lab.SetTab(DrinkLabView.Tab.Recipes); yield return Wait(0.4f);
            CheckOverlap("P40", lab.transform); CheckReach("P40", lab.transform);
            yield return Shot("P40_drawer_closed", lab.transform);
            var dv = lab.Drawer;
            notes.Add("DRAWER click front: " + ClickAt(dv.FrontHitbox, new Vector2(0.5f, 0.5f)));
            int df = 0;
            while (dv.State == RecipeDrawerView.Phase.Opening && df < 60) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"drawer_open_{df++:00}.png")); yield return null; }
            yield return Wait(0.2f);
            yield return Shot("P41_drawer_open_page1", lab.transform);
            var p1rt = GameObject.Find("Paper_1")?.GetComponent<RectTransform>();
            if (p1rt != null) notes.Add("DRAWER click paper1 strip: " + ClickAt(p1rt, new Vector2((110f - 103f) / 258f, 1f - (140f - 106f) / 112f)) + " selected=" + dv.Selected);
            yield return Wait(0.2f);
            yield return Shot("P42_drawer_paper_selected", lab.transform);
            var selRt = GameObject.Find("Paper_1")?.GetComponent<RectTransform>();
            if (selRt != null) notes.Add("DRAWER click selected paper again: " + ClickAt(selRt, new Vector2(0.5f, 0.85f)));
            df = 0;
            while (dv.State == RecipeDrawerView.Phase.Pulling && df < 80) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"drawer_pull_{df++:00}.png")); yield return null; }
            yield return Wait(0.2f);
            CheckOverlap("P43", lab.transform); CheckReach("P43", lab.transform);
            yield return Shot("P43_drawer_sheet_unfolded", lab.transform);
            dv.ReturnSheet();
            df = 0;
            while (dv.State == RecipeDrawerView.Phase.Returning && df < 80) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"drawer_return_{df++:00}.png")); yield return null; }
            yield return Wait(0.2f);
            GameObject.Find("Divider_2")?.GetComponent<Button>().onClick.Invoke(); yield return Wait(0.3f);
            yield return Shot("P44_drawer_page3", lab.transform);
            notes.Add($"DRAWER: recipes={DrinkLab.Recipes.Count} pages={dv.PageCount} scale={dv.Scale} state={dv.State}");
            lab.Close();
            yield return Wait(0.6f);

            // ── 연구 노트북(손 없음) ──
            DrinkLab.Data.research.researchPoints = 40; // QA 격리 저장 준비
            select = Find<CharacterSelectView>();
            if (select != null && select.LaptopButton != null) select.LaptopButton.onClick.Invoke(); else ResearchLaptopView.Show(() => { });
            float l0 = Time.realtimeSinceStartup;
            int lf = 0;
            yield return null;
            while (ResearchLaptopView.Current != null && ResearchLaptopView.Current.State == ResearchLaptopView.Phase.Opening && Time.realtimeSinceStartup - l0 < 3f)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"laptop_open_{lf++:00}.png"));
                yield return null;
            }
            notes.Add($"LAPTOP open took {Time.realtimeSinceStartup - l0:0.000}s frames={lf}");
            var lap = ResearchLaptopView.Current;
            yield return Wait(0.2f);
            yield return Shot("P30_laptop_mastery_start", lap.transform);
            Btn("Unlock")?.onClick.Invoke(); yield return Wait(0.2f);
            Btn("Equip")?.onClick.Invoke(); yield return Wait(0.2f);
            Btn("Node_service_2")?.onClick.Invoke(); yield return Wait(0.2f);
            Btn("Unlock")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Shot("P31_laptop_mastery_unlocked_equipped", lap.transform);
            Btn("Branch_stability")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Shot("P32_laptop_branch_stability", lap.transform);
            lap.SetTab(ResearchLaptopView.Tab.Staff); yield return Wait(0.3f);
            Btn("Staff_staff_tea_aroma")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Shot("P33_laptop_staff_preview", lap.transform);
            Btn("Hire")?.onClick.Invoke(); yield return Wait(0.2f);
            Btn("Specialty")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Shot("P34_laptop_staff_hired_active", lap.transform);
            Btn("Staff_staff_cocktail_blend")?.onClick.Invoke(); yield return Wait(0.2f);
            yield return Shot("P35_laptop_staff_locked_condition", lap.transform);
            lap.SetTab(ResearchLaptopView.Tab.Upgrades); yield return Wait(0.3f);
            CheckOverlap("P36", lap.transform); CheckReach("P36", lap.transform);
            yield return Shot("P36_laptop_existing_upgrades", lap.transform);
            lap.Close();
            lf = 0;
            while (ResearchLaptopView.Current != null && lf < 60)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"laptop_close_{lf++:00}.png"));
                yield return null;
            }
            yield return Wait(0.5f);
            var r2 = DrinkLab.Research;
            notes.Add($"RESEARCH: rp={r2.researchPoints} unlocked={string.Join("|", r2.unlockedTraits)} equipped={string.Join("|", r2.equippedTraits)} hired={string.Join("|", r2.hiredStaff)} active={r2.activeSpecialty}");
            var sel = Find<CharacterSelectView>();
            yield return Shot("P37_select_after_laptop", sel != null ? sel.transform : null);
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, placeholderArtUsed = false, version = Application.version,
            }, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        /// <summary>실제 EventSystem 레이캐스트 클릭(가림·히트 영역 검증). 맞은 맨 위 대상 이름을 돌려준다.</summary>
        string ClickAt(RectTransform rt, Vector2 normalized)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var w = Vector3.Lerp(Vector3.Lerp(c[0], c[3], normalized.x), Vector3.Lerp(c[1], c[2], normalized.x), normalized.y);
            var screen = RectTransformUtility.WorldToScreenPoint(null, w);
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return "(no EventSystem)";
            var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = screen };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(ped, hits);
            if (hits.Count == 0) return "(no hit)";
            ped.pointerCurrentRaycast = ped.pointerPressRaycast = hits[0];
            var target = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            if (target != null) UnityEngine.EventSystems.ExecuteEvents.Execute(target, ped, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            return hits[0].gameObject.name;
        }

        /// <summary>노트북 배경(직업별 바/찻집) 실제 화면: 바텐더·티 소믈리에 각각 직업 선택 → 무기 단계 → 연구 노트북 열기·닫기 프레임 → 복귀 화면.</summary>
        IEnumerator LaptopRoomEvidence()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            var framesDir = System.IO.Path.Combine(output, "frames");
            Directory.CreateDirectory(framesDir);
            foreach (var (pick, tag) in new[] { (0, "bar"), (1, "tea") })
            {
                // 캡처가 만든 이전 선택 화면을 치우고 새로 시작(RestartToSelect는 이전 화면을 지우지 않는다 — 낡은 화면을 잡지 않게)
                foreach (var old in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) Destroy(old.gameObject);
                yield return null;
                GameBootstrap.RestartToSelect();
                yield return Wait(0.5f);
                var all = UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None);
                var select = all.Length > 0 ? all[0] : null;
                notes.Add($"LAPTOP {tag}: select views={all.Length}");
                if (select.WeaponBackButton != null) { select.WeaponBackButton.onClick.Invoke(); yield return Wait(0.3f); } // 직업 선택 단계로
                string jobHit = ClickAt((RectTransform)(pick == 0 ? select.BartenderButton : select.TeaSommelierButton).transform, new Vector2(0.5f, 0.5f));
                notes.Add($"LAPTOP {tag}: job click {jobHit}");
                yield return Wait(0.5f);
                yield return Shot($"L0_{tag}_weapon_step_before", select.transform);
                string hit = ClickAt((RectTransform)select.LaptopButton.transform, new Vector2(0.5f, 0.5f));
                notes.Add($"LAPTOP {tag}: clicked {hit}");
                int f = 0;
                yield return null;
                while (ResearchLaptopView.Current != null && ResearchLaptopView.Current.State == ResearchLaptopView.Phase.Opening && f < 80)
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"{tag}_open_{f++:00}.png"));
                    yield return null;
                }
                yield return Wait(0.2f);
                var lapNow = ResearchLaptopView.Current;
                var wallImg = lapNow != null ? lapNow.transform.Find("Root/RoomWall")?.GetComponent<Image>() : null;
                var title = select != null ? select.transform.Find("Root/Title")?.GetComponentInChildren<Text>() : null;
                notes.Add($"LAPTOP {tag}: weaponTitle='{(title != null ? title.text : "?")}' roomWall={(wallImg != null && wallImg.sprite != null ? wallImg.sprite.name : "none")} expected={(pick == 0 ? "dl_craft_bar_wall" : "dl_craft_tea_wall")}");
                yield return Shot($"L1_{tag}_laptop_open", lapNow != null ? lapNow.transform : null);
                ResearchLaptopView.Current?.Close();
                f = 0;
                while (ResearchLaptopView.Current != null && f < 80)
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"{tag}_close_{f++:00}.png"));
                    yield return null;
                }
                // 닫힌 직후 몇 프레임(검은 번쩍임 없는지)
                for (int i = 0; i < 4; i++) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(framesDir, $"{tag}_after_{i:00}.png")); yield return null; }
                yield return Wait(0.3f);
                var back = Find<CharacterSelectView>();
                notes.Add($"LAPTOP {tag}: returned select={(back != null)} weaponStep={(back != null && back.StartButton != null)} laptopOpen={(ResearchLaptopView.Current != null)}");
                yield return Shot($"L2_{tag}_returned", back != null ? back.transform : null);
            }
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, placeholderArtUsed = false, version = Application.version,
            }, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        /// <summary>
        /// 72 실제 런 통합 증거(격리 저장 QA 준비: RP·코인만 직접 넣고 해금·장착·채용·제조는 실제 API/거래): 직업·무기 선택 화면에서 실제 출발 →
        /// 숙련 스냅샷·한 잔의 집중·제조 전문 버프 → 실제 처치 처리기(일반 25·정예·중간 보스) 재료 묶음·HUD 알림 → 직접 타격 보정 → 사망 결과 화면(재료·RP) → 노트북 잔액.
        /// </summary>
        IEnumerator Run72Evidence()
        {
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            // ── 서랍 끝 상태: 빈 서랍 / 30장(6칸) / 31번째 저장 거부 ──
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var sel0 = Find<CharacterSelectView>();
            sel0.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab0 = sel0.DrinkLabScreen;
            lab0.SetTab(DrinkLabView.Tab.Recipes); yield return Wait(0.3f);
            ClickAt(lab0.Drawer.FrontHitbox, new Vector2(0.5f, 0.5f));
            yield return Wait(0.7f);
            CheckOverlap("D0", lab0.transform);
            yield return Shot("D0_drawer_empty_open", lab0.transform);
            for (int i = 0; i < 30; i++)
                DrinkLab.SaveRecipe(Mix(i % 2 == 0 ? DrinkKind.Cocktail : DrinkKind.Tea, i % 2 == 0 ? DrinkTechnique.Build : DrinkTechnique.Steep, $"서류 {i + 1:00}",
                    i % 2 == 0 ? new[] { ("gin", 100 + i * 10) } : new[] { ("green_tea", 10 + i) }), -1, out _);
            bool extra = DrinkLab.SaveRecipe(Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "31번째", ("gin", 100)), -1, out var extraErr);
            notes.Add($"DRAWER30: saved={DrinkLab.Recipes.Count} 31st={(extra ? "saved?!" : "rejected: " + extraErr)}");
            lab0.SetTab(DrinkLabView.Tab.Recipes); yield return Wait(0.6f);
            GameObject.Find("Divider_5")?.GetComponent<Button>().onClick.Invoke();
            yield return Wait(0.3f);
            notes.Add($"DRAWER30: pages={lab0.Drawer.PageCount} page={lab0.Drawer.Page + 1} state={lab0.Drawer.State}");
            CheckOverlap("D1", lab0.transform); CheckReach("D1", lab0.transform);
            yield return Shot("D1_drawer_30_page6", lab0.transform);
            lab0.Close();
            yield return Wait(0.5f);
            var r = DrinkLab.Data.research;
            r.researchPoints = 60; // QA 격리 저장 준비
            foreach (var id in new[] { "service_1", "finesse_1", "finesse_2", "finesse_3", "finesse_4", "stability_1", "stability_2", "stability_3" })
                if (!DrinkLab.UnlockTrait(id, out var e)) notes.Add($"RUN72 unlock {id} failed: {e}");
            foreach (var id in new[] { "service_1", "finesse_4", "stability_3" })
                if (!DrinkLab.EquipTrait(id, out var e)) notes.Add($"RUN72 equip {id} failed: {e}");
            DrinkLab.HireStaff("staff_cocktail_aroma", out _);
            DrinkLab.SetSpecialty("staff_cocktail_aroma", out _);
            var drink = Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "출발 스터링", ("gin", 300), ("rum", 150));
            var craft = DrinkLab.Craft(drink);
            var preview = DrinkCalculator.Buff(drink, "staff_cocktail_aroma");
            notes.Add($"RUN72 craft={craft.Status} specialty={DrinkLab.Prepared?.specialtyId} previewAttack={preview[DrinkStat.Attack]:0.0000} plainAttack={DrinkCalculator.Buff(drink)[DrinkStat.Attack]:0.0000} rp={r.researchPoints} equipped={string.Join("|", r.equippedTraits)}");
            foreach (var old in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) Destroy(old.gameObject);
            yield return null;
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            ClickAt((RectTransform)select.BartenderButton.transform, new Vector2(0.5f, 0.5f));
            yield return Wait(0.4f);
            yield return Shot("R0_weapon_step_prepared_drink", select.transform);
            notes.Add("RUN72 start click: " + ClickAt((RectTransform)select.StartButton.transform, new Vector2(0.5f, 0.5f)));
            float w0 = Time.realtimeSinceStartup;
            while ((GameManager.Instance == null || GameManager.Instance.Player == null) && Time.realtimeSinceStartup - w0 < 10f) yield return null;
            var game = GameManager.Instance;
            if (game == null) { notes.Add("RUN72: run did not start"); yield break; }
            yield return Wait(1.5f);
            var buff = game.DrinkBuff;
            notes.Add($"RUN72 run={game.RunId} traits={string.Join("|", game.MasteryTraits)} mastery={(game.Player.Mastery != null)} drinkAttack={(buff != null ? buff[DrinkStat.Attack] : -1):0.0000} expected={Mathf.Min(DrinkRules.Cap(DrinkStat.Attack), preview[DrinkStat.Attack] * 1.05f):0.0000} prepared={DrinkLab.Prepared?.state}");
            yield return Shot("R1_run_start_hud", null);
            // 실제 처치 처리기
            for (int i = 0; i < 25; i++) game.OnEnemyKilled(game.Player.Position + Vector2.right * 2f, EnemyDef.Brute);
            yield return Wait(0.4f);
            notes.Add($"RUN72 after 25 normal: pending={DrinkLab.PendingFor(game.RunId).Count} toast='{game.DrinkHud?.ToastText}'");
            yield return Shot("R2_normal25_supply_toast", null);
            game.OnEnemyKilled(game.Player.Position + Vector2.right * 2f, EnemyDef.MartiniSpirit);
            yield return Wait(0.3f);
            float hpBefore = game.Player.Hp;
            game.Player.TakeDamage(game.Player.MaxHp * 0.2f);
            float hpHurt = game.Player.Hp;
            game.OnEnemyKilled(game.Player.Position + Vector2.right * 3f, EnemyDef.GrinderGolem);
            yield return Wait(0.6f);
            var pend = DrinkLab.PendingFor(game.RunId);
            notes.Add($"RUN72 awards={string.Join(" / ", pend.ConvertAll(a => a.awardId + ":" + DrinkLab.Describe(a.items)))}");
            notes.Add($"RUN72 boss heal: hp {hpHurt:0.0} -> {game.Player.Hp:0.0} (max {game.Player.MaxHp:0.0}, 3% + mid-boss heal) before={hpBefore:0.0}");
            yield return Shot("R3_boss_supply_toast", null);
            // 직접 타격(첫 30초 첫 잔의 기세 +8%)
            var enemy = Enemy.SpawnAt(game.Player.Position + Vector2.right * 1.5f, EnemyDef.Imp, 1f, game.Player, game.transform);
            if (enemy != null)
            {
                game.Stats.drinkCritChance = 0f;
                float dmg = game.Player.HiddenRuntime.ModifyDirectDamage(enemy, 10f);
                notes.Add($"RUN72 direct hit 10 -> {dmg:0.00} at runTime {game.Player.Mastery.RunTime:0.0}s (service_1 opening +8%)");
                Destroy(enemy.gameObject);
            }
            notes.Add($"RUN72 kills={game.Kills} rpKills={game.ResearchKills} mid={game.MidBossKills}");
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            yield return Wait(1.5f);
            var s = game.DrinkSettlement;
            notes.Add($"RUN72 settlement kind={s?.kind} drink={s?.drink} granted={(s != null ? DrinkLab.Describe(s.granted) : "")} rp=+{s?.researchPoints} total={DrinkLab.Research.researchPoints}");
            notes.Add("RUN72 result line: " + ResultView.DrinkStatusLine(s).Replace("\n", " | "));
            yield return Shot("R4_result_materials_rp", null);
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report
            {
                screen = $"{Screen.width}x{Screen.height}", touchUi = MobilePlatform.IsTouchUI, shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, placeholderArtUsed = false, version = Application.version,
            }, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        IEnumerator Run()
        {
            yield return Wait(4.0f); // 저장된 화면 설정 부트 확인(DisplayBootVerifier 2회)이 끝난 뒤 캡처 해상도로
            DrinkStageView.IgnoreFocusLossForCapture = true;
            var args = Environment.GetCommandLineArgs();
            int w = Array.IndexOf(args, "-cct-capture-width"), h = Array.IndexOf(args, "-cct-capture-height");
            if (w >= 0 && h >= 0 && w + 1 < args.Length && h + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[w + 1]), int.Parse(args[h + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            FirstRunTutorial.MarkSeen();
            int modeIdx = Array.IndexOf(args, "-cct-drinklab-capture-mode");
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "reference")
            {
                yield return ReferencePour();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "stage1")
            {
                yield return Stage1Evidence();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "run72")
            {
                yield return Run72Evidence();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "laptop")
            {
                yield return LaptopRoomEvidence();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "progress")
            {
                yield return ProgressEvidence();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "uifull")
            {
                yield return UiFull();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "perf")
            {
                yield return Stage1Evidence(noShots: true); // 같은 과정, 프레임 파일 쓰기 없이 프레임 시간만(끊김 검수)
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "cycle")
            {
                yield return CycleEvidence();
                yield break;
            }
            if (modeIdx >= 0 && modeIdx + 1 < args.Length && args[modeIdx + 1] == "ui")
            {
                yield return UiLayout();
                yield break;
            }
            var title = Find<TitleView>();
            yield return Shot("01_title_v11_label", title != null ? title.transform : null);
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            GameBootstrap.RestartToSelect();
            yield return Wait(0.5f);
            var select = Find<CharacterSelectView>();
            yield return Shot("02_select_job_drink_button", select.transform);
            select.BartenderButton.onClick.Invoke();
            yield return Wait(0.4f);
            yield return Shot("03_select_weapon_no_drink", select.transform);
            select.DrinkLabButton.onClick.Invoke();
            yield return Wait(0.6f);
            var lab = select.DrinkLabScreen;
            yield return Shot("04_lab_cocktail_empty", lab.transform);

            // 실제 붓기(드래그와 같은 경로: 기울기 → 연속 속도 → 0.1 단위)
            var gin = DrinkIngredients.Get("gin");
            lab.Stage.Select(gin);
            lab.Stage.BeginPour(Vector2.zero);
            for (float a = 0f; a < 70f; a += 4f) { lab.Stage.SetTilt(a); yield return null; }
            yield return Wait(1.0f);
            yield return Shot("05_pour_gin_midstream", lab.transform);
            lab.Stage.SetTilt(DrinkStageView.TiltMax);
            float guard = 0f;
            while (lab.Stage.MeasureDumps < 1 && guard < 5f) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.6f);
            yield return Shot("06_pour_split_cumulative", lab.transform);
            lab.Stage.StopPour();
            notes.Add($"pour: draft gin={DrinkRules.Format(lab.Draft.AmountOf("gin"))}mL field='{lab.AmountField.text}' dumps={lab.Stage.MeasureDumps}");
            var blueRow = Btn("Row_blue_curacao");
            if (blueRow != null) blueRow.onClick.Invoke();
            yield return Wait(0.2f);
            lab.ApplyTypedAmount("5.0");
            yield return Wait(0.4f);
            yield return Shot("07_typed_blue_5ml_buff_color_preview", lab.transform);
            lab.ApplyTypedAmount("-3");
            yield return Wait(0.2f);
            yield return Shot("08_typed_negative_rejected", lab.transform);

            // 단계별 색(실제 붓기): 병 → 재료색 줄기 → 지거 속 순수 재료 → 같은 색 옮겨 붓기 → 열린 셰이커 안 누적 혼합 → 흔들기(닫힘, 불투명) → 최종 혼합색 줄기 → 완성
            yield return ColourStages(lab);

            // 색 비교(미리보기 렌더, 재료 차감 없음)
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "무색 진", ("gin", 600)), "09_color_clear_gin", 99f, null);
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "진+큐라소", ("gin", 900), ("blue_curacao", 50)), "10_color_blue_5ml", 99f, null);
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "진+그레나딘", ("gin", 900), ("grenadine", 50)), "11_color_red_5ml", 99f, null);
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "진+레몬", ("gin", 300), ("lemon_juice", 300)), "12_color_lemon_cloudy", 99f, null);
            var dilute = Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "희석", ("gin", 300), ("grenadine", 100)); dilute.water = 300;
            yield return Preview(lab, dilute, "13_color_diluted_with_water", 99f, null);
            // 기법 모션
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Shake, "셰이킹", ("gin", 450), ("blue_curacao", 50), ("lemon_juice", 150)), "16_shake_final_foam_settled", 0.7f, "14_shake_motion", 2.0f, "15_shake_pour_bubbles");
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Stir, "스터링", ("gin", 450), ("rum", 150), ("grenadine", 50)), "18_stir_final_clear", 0.8f, "17_stir_swirl");
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "빌드", ("grenadine", 150), ("gin", 450), ("blue_curacao", 50)), "20_build_final", 0.6f, "19_build_diffusing");
            yield return Preview(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Layer, "레이어", ("gin", 300), ("grenadine", 150), ("blue_curacao", 150)), "22_layer_final_boundaries", 1.4f, "21_layer_pouring");

            // 차
            Btn("KindTea").onClick.Invoke();
            yield return Wait(0.5f);
            lab = select.DrinkLabScreen;
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "홍차", ("black_tea", 30), ("honey", 100)));
            lab.Stage.Select(DrinkIngredients.Get("black_tea"));
            lab.Stage.BeginPour(Vector2.zero);
            lab.Stage.SetTilt(115f);
            yield return Wait(0.8f);
            yield return Shot("23_tea_leaves_into_chaha", lab.transform);
            lab.Stage.StopPour();
            Load(lab, Mix(DrinkKind.Tea, DrinkTechnique.Steep, "홍차", ("black_tea", 30), ("honey", 100)));
            yield return Preview(lab, lab.Draft.Clone(), "26_tea_final_cup", 0.6f, "24_tea_water_pour", 1.9f, "25_tea_steeping_steam");
            notes.Add($"tea final cup: {Hex(lab.Stage.FinalVessel.TopColor)} brew={Hex(DrinkCalculator.Look(lab.Draft).color)}");

            // 도감(녹차는 QA가 보유량 0·미발견으로 바꾼 잠금 예시 — 메모리만, 저장 안 함)
            var data = DrinkLab.Data;
            data.stock.RemoveAll(s => s.id == "green_tea");
            data.seenIds.Remove("green_tea");
            notes.Add("codex: green_tea locked state is QA-seeded in memory only");
            lab.SetTab(DrinkLabView.Tab.Codex);
            yield return Wait(0.5f);
            yield return Shot("27_codex_with_locked", lab.transform);
            DrinkLab.ReloadForTests(); // 디스크 상태로 되돌림

            // 레시피·공유 코드
            lab.SetTab(DrinkLabView.Tab.Craft);
            Btn("KindCocktail").onClick.Invoke();
            yield return Wait(0.4f);
            lab = select.DrinkLabScreen;
            Load(lab, Mix(DrinkKind.Cocktail, DrinkTechnique.Shake, "푸른 밤", ("gin", 450), ("blue_curacao", 50), ("lemon_juice", 100)));
            GameObject.Find("RecipeName").GetComponent<InputField>().text = "푸른 밤";
            Btn("SaveRecipe").onClick.Invoke();
            lab.SetTab(DrinkLabView.Tab.Recipes);
            yield return Wait(0.4f);
            var code = lab.CopyCode(0);
            notes.Add("share code: " + code);
            yield return Shot("28_recipes_list_code_copied", lab.transform);
            lab.ImportField.text = code;
            lab.PreviewImport(code);
            yield return Wait(0.3f);
            yield return Shot("29_import_preview_valid", lab.transform);
            lab.ImportField.text = code.Substring(0, code.Length - 4) + "ZZZZ";
            lab.PreviewImport(lab.ImportField.text);
            yield return Wait(0.3f);
            yield return Shot("30_import_rejected_checksum", lab.transform);

            // 실제 제조(재료 차감) → 준비 음료
            lab.LoadRecipe(0, false);
            yield return Wait(0.4f);
            lab = select.DrinkLabScreen;
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            guard = 0f;
            while (lab.Stage.SequencePlaying && guard < 12f) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.4f);
            yield return Shot("31_real_craft_prepared", lab.transform);
            notes.Add($"real craft: prepared={DrinkLab.Prepared?.recipe.name} gin left={DrinkLab.Owned("gin")}");
            lab.Close();
            yield return Wait(0.5f);
            select = Find<CharacterSelectView>();
            yield return Shot("32_select_weapon_prepared_line", select.transform);

            // 출발 → 완성 음료 공개(마시는 동작 없음) → 버프
            select.StartButton.onClick.Invoke();
            yield return Wait(0.5f);
            yield return Shot("33_depart_drink_reveal", DrinkRevealOverlay.Current != null ? DrinkRevealOverlay.Current.transform : null);
            yield return Wait(0.9f);
            yield return Shot("34_buff_gain", DrinkRevealOverlay.Current != null ? DrinkRevealOverlay.Current.transform : null);
            guard = 0f;
            while (GameManager.Instance == null && guard < 8f) { guard += Time.unscaledDeltaTime; yield return null; }
            var game = GameManager.Instance;
            yield return Wait(1.2f);
            yield return Shot("35_run_hud_drink_chip", game.Hud.transform.parent);
            notes.Add($"run1 buff: {game.DrinkBuff?.Describe()} maxHp={game.Stats.MaxHp:0.0} damage={game.Stats.Damage:0.00}");
            var mob = new EnemyDef { name = "QA 손님", dropCount = 0, hp = 10f };
            for (int k = 0; k < 4; k++) game.OnEnemyKilled(Vector2.zero, mob);
            game.OpenPause();
            yield return Wait(0.4f);
            yield return Shot("36_pause_preserve_hint_4kills", game.PauseView.transform);
            game.GiveUp();
            yield return Wait(1.0f);
            yield return Shot("37_result_abandon_4kills_preserved", game.Hud.transform.parent);
            notes.Add("run1 settlement: " + ResultView.DrinkStatusLine(game.DrinkSettlement).Replace("\n", " / "));

            // 재입장(보존 음료 그대로) → 5킬 이상 + 중간 보스 → 사망
            game.Restart();
            yield return Wait(0.6f);
            select = Find<CharacterSelectView>();
            select.BartenderButton.onClick.Invoke();
            yield return Wait(0.3f);
            select.StartButton.onClick.Invoke();
            yield return Wait(0.3f);
            if (DrinkRevealOverlay.Current != null) DrinkRevealOverlay.Current.Skip();
            guard = 0f;
            while (GameManager.Instance == null && guard < 8f) { guard += Time.unscaledDeltaTime; yield return null; }
            game = GameManager.Instance;
            yield return Wait(0.8f);
            for (int k = 0; k < 6; k++) game.OnEnemyKilled(Vector2.zero, mob);
            game.OnEnemyKilled(game.Player.Position + new Vector2(4f, 0f), new EnemyDef { name = "QA 중간 보스", dropCount = 0, hp = 10f, boss = BossKind.Mid });
            yield return Wait(0.5f);
            yield return Shot("38_boss_drink_material_pickup_toast", game.Hud.transform.parent);
            game.MarkPlayerDiedForTests();
            yield return Wait(2.2f);
            yield return Shot("39_result_death_materials_saved_drink_used", game.Hud.transform.parent);
            notes.Add("run2 settlement: " + ResultView.DrinkStatusLine(game.DrinkSettlement).Replace("\n", " / "));

            // 출발 저장 실패 → 런 미시작 안내
            game.GoToTitle();
            yield return Wait(0.5f);
            foreach (var tv in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(tv.gameObject);
            var again = Mix(DrinkKind.Cocktail, DrinkTechnique.Build, "실패 시험", ("rum", 100));
            var cr = DrinkLab.Craft(again);
            notes.Add("fault craft: " + cr.Status);
            DrinkStore.FaultHookForTests = s => { if (s == DrinkStore.SaveStage.WriteTmp) throw new IOException("QA 주입 디스크 실패"); };
            GameBootstrap.Depart(Job.Bartender, WeaponKind.Rapier);
            yield return Wait(0.5f);
            yield return Shot("40_depart_save_failure_dialog", DrinkDepartDialog.Current != null ? DrinkDepartDialog.Current.transform : null);
            notes.Add($"fault dialog shown={DrinkDepartDialog.Current != null} gameStarted={GameManager.Instance != null} prepared={DrinkLab.Prepared?.state}");
            DrinkStore.FaultHookForTests = null;
            if (DrinkDepartDialog.Current != null) DrinkDepartDialog.Current.CancelButton.onClick.Invoke();
            yield return Wait(0.5f);

            var report = new Report
            {
                screen = $"{Screen.width}x{Screen.height}",
                touchUi = MobilePlatform.IsTouchUI,
                shots = shots, overflow = overflow, notes = notes,
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx,
                placeholderArtUsed = placeholders,
                version = Application.version,
            };
            File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            yield return Wait(0.5f);
            Application.Quit();
        }

        [Serializable]
        class Report
        {
            public string screen;
            public bool touchUi;
            public string version;
            public int minFontPx;
            public bool placeholderArtUsed;
            public List<string> shots;
            public List<string> overflow;
            public List<string> notes;
        }
    }
}
#endif
