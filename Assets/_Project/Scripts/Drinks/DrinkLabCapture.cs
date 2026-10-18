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
                    var streamRt = stage.transform.Find("Stream") as RectTransform;
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
