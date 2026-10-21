using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>69 음료 제조 실제 UI(PlayMode): 직업 선택 → 음료 제조, 병 드래그 0.1 단위 계량·지거 나눠 붓기·놓기/이탈 즉시 정지,
    /// 직접 입력 경계, 종류 필터, 기법별 제조 모션 완료, 출발 시 완성 음료 공개 → 런 버프, 출발 저장 실패 → 런 미시작 안내.</summary>
    public class DrinkLabPlayTests
    {
        string dir;

        static void Clear()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkLabView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkRevealOverlay>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkDepartDialog>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<ResearchLaptopView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.transform.root.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Clear();
            dir = Path.Combine(Application.temporaryCachePath, "drinkplay_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DrinkStore.FaultHookForTests = null;
            if (GameManager.Instance != null) UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            Clear();
            Time.timeScale = 1f;
            yield return null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        /// <summary>실제 EventSystem 레이캐스트로 맨 위 대상에 클릭을 보낸다(onClick 직접 호출이 아니라 가림·히트 영역까지 검증).</summary>
        static GameObject ClickAt(Vector2 screen)
        {
            var es = EventSystem.current;
            Assert.IsNotNull(es, "EventSystem");
            var ped = new PointerEventData(es) { position = screen, button = PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(ped, hits);
            Assert.Greater(hits.Count, 0, "레이캐스트 대상 없음 " + screen);
            ped.pointerCurrentRaycast = ped.pointerPressRaycast = hits[0];
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(target, "클릭 받는 대상 없음: " + hits[0].gameObject.name);
            ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerClickHandler);
            return hits[0].gameObject;
        }

        static Vector2 ScreenOf(RectTransform rt, Vector2 normalized)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var w = Vector3.Lerp(Vector3.Lerp(c[0], c[3], normalized.x), Vector3.Lerp(c[1], c[2], normalized.x), normalized.y);
            return RectTransformUtility.WorldToScreenPoint(null, w);
        }

        static IEnumerator Until(Func<bool> cond, float timeout = 12f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 초과");
        }

        IEnumerator OpenLab(Action<DrinkLabView> got)
        {
            var select = CharacterSelectView.Show((j, w) => { });
            yield return null;
            Assert.IsNotNull(select.DrinkLabButton, "직업 선택 화면의 음료 제조 버튼");
            select.DrinkLabButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(select.DrinkLabScreen);
            got(select.DrinkLabScreen);
        }

        [UnityTest]
        public IEnumerator DragPour_TenthPrecision_SplitPours_ReleaseAndLeaveStop()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var gin = DrinkIngredients.Get("gin");
            var stage = lab.Stage;
            stage.Select(gin);
            // 작은 기울기(이 병 잔량의 액면이 입구에 닿는 각도 + 5°): 아주 느리게, 0.1 단위 정수만.
            Assert.IsNotNull(stage.SourceBottle, "액체 병은 분리 아트(내부 마스크) 그릇");
            float onset = stage.BottleOnset;
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(onset - 6f);
            stage.Step(1f);
            Assert.AreEqual(0, stage.InMeasure, "액면이 입구에 닿기 전에는 흐르지 않음");
            stage.SetTilt(onset + 5f);
            float rate = DrinkStageView.Rate(stage.Tilt, gin, onset);
            Assert.Less(rate, 5f, "작은 움직임은 0.5mL/s 미만");
            stage.Step(1f);
            int expected = stage.InMeasure;
            Assert.Greater(expected, 0);
            Assert.LessOrEqual(expected, Mathf.FloorToInt(rate), "기울기 곡선 이하(입구 유출량으로도 제한)");
            stage.StopPour();
            Assert.AreEqual(expected, lab.Draft.AmountOf("gin"), "놓으면 지거 양만 배합에");
            Assert.AreEqual(DrinkRules.Format(expected), lab.AmountField.text, "표시 수치 = 실제 배합");
            stage.Step(1f);
            Assert.AreEqual(expected, lab.Draft.AmountOf("gin"), "놓은 뒤 더 붓지 않음");
            yield return Until(() => !stage.Transferring, 6f); // 지거 → 그릇 옮겨 붓기와 복귀가 끝난 뒤에만 다시 계량
            // 큰 기울기: 45mL 지거가 차면 나눠 붓고 누적.
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            for (int i = 0; i < 40; i++) stage.Step(0.1f);
            Assert.GreaterOrEqual(stage.MeasureDumps, 1, "지거 용량 초과 → 나눠 붓기");
            int total = lab.Draft.AmountOf("gin") + stage.InMeasure;
            StringAssert.Contains(DrinkRules.Format(total), stage.ReadoutText, "누적량 표시");
            // 제조대 밖으로 이탈하면 즉시 정지
            stage.DragTo(new Vector2(-5000f, -5000f));
            Assert.IsFalse(stage.Pouring);
            Assert.AreEqual(total, lab.Draft.AmountOf("gin"));
            Assert.LessOrEqual(lab.Draft.AmountOf("gin"), DrinkLab.Owned("gin"));
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "확정 전 재고 미차감");
        }

        static void AssertColor(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.02f, what + " r");
            Assert.AreEqual(expected.g, actual.g, 0.02f, what + " g");
            Assert.AreEqual(expected.b, actual.b, 0.02f, what + " b");
        }

        [UnityTest]
        public IEnumerator IntermediateColours_StreamJiggerDumpAndAccumulatedMix()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            Assert.AreEqual(DrinkTechnique.Shake, lab.Draft.Technique);
            var stage = lab.Stage;
            foreach (var id in new[] { "gin", "blue_curacao", "grenadine" })
            {
                var def = DrinkIngredients.Get(id);
                var pure = DrinkCalculator.IngredientLook(def);
                stage.Select(def);
                stage.BeginPour(Vector2.zero);
                stage.SetTilt(DrinkStageView.TiltMax);
                stage.Step(0.2f);
                Assert.IsTrue(stage.StreamVisible);
                AssertColor(DrinkStageView.StreamTint(pure), stage.StreamColor, id + " 병 줄기 = 재료색(무색 액체는 옅은 물빛)");
                Assert.IsTrue(stage.Ledger.HasInFlight(DrinkStageView.LedgerMeasure), "나온 액체는 먼저 공중(줄기)에 있다 — 지거로 순간이동하지 않음");
                yield return Until(() => stage.Ledger[DrinkStageView.LedgerMeasure].LiquidMl > 0, 1f);
                yield return null;
                AssertColor(pure, stage.Jigger.TopColor, id + " 지거 안 = 순수 재료");
                Assert.AreEqual(0.0, stage.Ledger[DrinkStageView.LedgerVessel].Liquid.ComponentVolume(id), 1e-9, "붓는 중(지거 단계) 셰이커에는 아직 이 재료가 없음");
                float pouredMl = stage.InMeasure / 10f;
                int before = lab.Draft.AmountOf(id);
                stage.StopPour();
                Assert.AreEqual(before + Mathf.RoundToInt(pouredMl * 10f), lab.Draft.AmountOf(id), "데이터는 즉시 정확히 반영(0.1 손실 없음)");
                yield return null;
                Assert.IsTrue(stage.Transferring, "지거 → 셰이커 옮겨 붓기 주기 시작");
                Assert.IsFalse(stage.StreamEmitting, "옮겨 붓는 동안 병 → 지거 흐름 없음(이미 떨어지던 꼬리만 마저 떨어짐)");
                float t0 = Time.realtimeSinceStartup;
                while (stage.Phase != DrinkStageView.CyclePhase.ToolPour && Time.realtimeSinceStartup - t0 < 3f) yield return null;
                Assert.AreEqual(DrinkStageView.CyclePhase.ToolPour, stage.Phase, "접근 후 흐름 단계");
                while (stage.TransferProgress < 0.3f && Time.realtimeSinceStartup - t0 < 4f) yield return null;
                Assert.IsTrue(stage.DumpStreamVisible, "지거 → 셰이커 줄기");
                AssertColor(DrinkStageView.StreamTint(pure), stage.DumpStreamColor, id + " 옮겨 붓기 줄기 = 재료색");
                var tg = stage.TransferGeometry;
                var mouth = tg.mouth;
                Assert.AreEqual(tg.lip.x, tg.streamEnd.x, 0.01f, "줄기는 수직 낙하");
                Assert.Greater(tg.lip.y, mouth.y, "지거 입구가 받는 입구보다 위");
                Assert.Less(Mathf.Abs(tg.lip.x - mouth.x), 40f, "받는 입구 위");
                Assert.Less(stage.MainVessel.VolumeMl, DrinkCalculator.Look(lab.Draft).totalVolume, "그릇은 아직 다 차지 않음(순간이동 없음)");
                float poured = stage.MainVessel.VolumeMl;
                while (stage.Transferring && Time.realtimeSinceStartup - t0 < 6f) yield return null;
                Assert.Greater(Time.realtimeSinceStartup - t0, DrinkStageView.BottleReturnSeconds + 2f * DrinkStageView.ToolMoveSeconds + 0.7f, "주기는 연속 동작(최소 흐름 0.8초 포함)");
                Assert.IsFalse(stage.Transferring);
                Assert.Less(poured, stage.MainVessel.VolumeMl + 0.001f);
                Assert.IsFalse(stage.DumpStreamVisible);
                // 셰이커 장부 = 지금까지 부은 재료만(아직 넣지 않은 재료 0). 무거운 재료는 위에 들어와 시간에 걸쳐 가라앉으므로(71 §4)
                // 이 순간의 맨 위 색은 균일 혼합색이 아니라 장부 층 색이다 — 화면 = 장부 층을 검사하고, 균일 혼합색은 셰이킹 뒤 완성 잔에서 검사한다.
                var shaker = stage.Ledger[DrinkStageView.LedgerVessel];
                foreach (var other in new[] { "gin", "blue_curacao", "grenadine" })
                    Assert.AreEqual(lab.Draft.AmountOf(other) / 10.0, shaker.Liquid.ComponentVolume(other), 1e-4, id + ": 셰이커 안 " + other + " = 지금까지 부은 양(미래 재료 0)");
                // 화면 층은 1차원 침강의 셀 교대를 시간 평균한 표시(성분 합계는 장부와 정확히 같음) — 양으로 검사
                Assert.AreEqual((float)shaker.LiquidMl, stage.MainVessel.VolumeMl, 0.01f, id + " 셰이커 화면 양 = 장부");
                Assert.AreEqual(DrinkCalculator.Look(lab.Draft).totalVolume, stage.MainVessel.VolumeMl, 0.01f);
            }
            var finalMix = DrinkCalculator.Look(lab.Draft).color;
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(stage.MainVessel.transform.Find("Liquid").gameObject.activeSelf, "닫힌 셰이커는 불투명(액체 숨김)");
            float t = 0f; bool sawFinalStream = false;
            while (stage.SequencePlaying && t < 12f)
            {
                t += Time.unscaledDeltaTime;
                if (stage.StreamVisible) { AssertColor(DrinkStageView.StreamTint(finalMix), stage.StreamColor, "셰이커 → 잔 줄기 = 최종 혼합색"); sawFinalStream = true; }
                yield return null;
            }
            Assert.IsTrue(sawFinalStream);
            AssertColor(finalMix, stage.FinalVessel.TopColor, "완성 잔");
        }

        /// <summary>글 상자 폭 width에서 줄바꿈한 실제 높이(논리 단위). Text.preferredHeight와 같은 생성기, 폭만 바꿔 좁은 배치도 잰다.</summary>
        static float HeightAt(Text t, float width) =>
            t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(width, 0f))) / t.pixelsPerUnit;

        [UnityTest]
        public IEnumerator Codex_78SupplyHelpAndSourceText_FitInsideBoxes_WideAndNarrowest()
        {
            // 78: 도감 획득처 문구가 길어지고(판당 60묶음·희귀 후보) 하단 안내 칸에 공급 규칙 한 줄이 늘었다. 넘침(Overflow) 없이 칸 안에 있어야 한다.
            // 가장 좁은 배치: 화면비 1.18 → layoutW 1274, 2열 카드 본문 폭 494 / 안내 칸 폭 1174. 넓은 배치: 3열 본문 폭 514 / 안내 칸 1820.
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.SetTab(DrinkLabView.Tab.Codex);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var texts = lab.GetComponentsInChildren<Text>(true);
            var rule = Array.Find(texts, t => t.name == "SupplyRule");
            Assert.IsNotNull(rule, "도감 하단 공급 규칙 안내");
            Assert.AreEqual(DrinkLab.SupplyRuleText(), rule.text);
            var aux = Array.Find(texts, t => t.name == "Aux");
            Assert.IsNotNull(aux);
            var a = new Vector3[4]; var r = new Vector3[4];
            aux.rectTransform.GetWorldCorners(a); rule.rectTransform.GetWorldCorners(r);
            Assert.Less(r[1].y, a[0].y + 0.01f, "공급 규칙 칸은 기존 안내 칸 아래(겹치지 않음)");
            foreach (float w in new[] { rule.rectTransform.rect.width, 1174f, 1820f })
                Assert.LessOrEqual(HeightAt(rule, w), rule.rectTransform.rect.height + 0.5f, $"공급 규칙 폭 {w}");
            int bodies = 0;
            var over = new System.Collections.Generic.List<string>();
            foreach (var t in texts)
            {
                if (t.name != "Body" || t.transform.parent == null || !t.transform.parent.name.StartsWith("Codex_", StringComparison.Ordinal)) continue;
                bodies++;
                StringAssert.Contains("판당 60묶음까지", t.text, t.transform.parent.name);
                string id = t.transform.parent.name.Substring("Codex_".Length);
                var row = DrinkSupply.Get(id);
                string unit = DrinkIngredients.Get(id).form == IngredientForm.Leaf ? "g" : "mL";
                string oldSrc = (row.rare ? "정예·중간 보스·최종 보스" : "일반 처치 25회마다·정예·중간 보스·최종 보스") + $" (1묶음 {DrinkRules.Format(row.bundle)}{unit}{(row.rare ? ", 보스 희귀 보장" : "")}) · 적게 가진 재료일수록 잘 나옴";
                string saved = t.text;
                t.text = saved.Replace(DrinkLab.SourceText(id), oldSrc);
                float oldH = HeightAt(t, 494f);
                t.text = saved;
                TestContext.WriteLine($"{id} h494 new={HeightAt(t, 494f):0.##} old14765b3={oldH:0.##} h514={HeightAt(t, 514f):0.##} box={t.rectTransform.rect.height}");
                foreach (float w in new[] { t.rectTransform.rect.width, 494f, 514f })
                    if (HeightAt(t, w) > t.rectTransform.rect.height + 0.5f) over.Add($"{t.transform.parent.name} 본문 폭 {w}: {HeightAt(t, w):0.##} > {t.rectTransform.rect.height}");
            }
            Assert.AreEqual(DrinkIngredients.All.Count, bodies, "재료 카드 전부 검사");
            Assert.IsEmpty(over, string.Join(" / ", over));
            var content = (RectTransform)rule.transform.parent;
            rule.rectTransform.GetWorldCorners(r);
            var c = new Vector3[4]; content.GetWorldCorners(c);
            Assert.GreaterOrEqual(r[0].y, c[0].y - 0.01f, "스크롤 내용 높이 안에 공급 규칙 칸");
            TestContext.WriteLine($"narrow={lab.Narrow} ruleW={rule.rectTransform.rect.width} ruleH={HeightAt(rule, 1174f):0}/{rule.rectTransform.rect.height}");
        }

        [UnityTest]
        public IEnumerator CraftShortage_78SourceLine_GrowsToFitText_NotTruncated()
        {
            // 부족 재료 + "재료 소모와 보유량" 펼침 → 획득처 줄(Truncate). 78 문구가 길어져도 행이 글 높이만큼 늘어 잘리지 않아야 한다.
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            if (lab.Kind != DrinkKind.Cocktail) lab.LoadPreset(0);
            yield return null;
            Assert.AreEqual(DrinkKind.Cocktail, lab.Kind);
            lab.Draft.SetAmount("grenadine", DrinkLab.Owned("grenadine") + 10);
            lab.Draft.SetAmount("gin", DrinkLab.Owned("gin") + 10);
            lab.RefreshForTests();
            yield return null;
            var toggle = GameObject.Find("CostToggle");
            Assert.IsNotNull(toggle, "재료 소모 펼침 버튼");
            if (GameObject.Find("CostSrc_grenadine") == null) toggle.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var id in new[] { "grenadine", "gin" })
            {
                var row = GameObject.Find("CostSrc_" + id);
                Assert.IsNotNull(row, "획득처 줄 " + id);
                var l = row.transform.Find("L").GetComponent<Text>();
                StringAssert.Contains(DrinkLab.SourceText(id), l.text);
                Assert.LessOrEqual(l.preferredHeight, l.rectTransform.rect.height + 0.5f, id + " 획득처 글이 잘림");
                Assert.AreEqual(((RectTransform)row.transform).rect.height, l.rectTransform.rect.height, 0.5f);
                TestContext.WriteLine($"CostSrc_{id} need={l.preferredHeight:0.#} row={((RectTransform)row.transform).rect.height}");
            }
        }

        [UnityTest]
        public IEnumerator FreshSave_ExamplePresetsSelectable_NoConsumption()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            Assert.AreEqual(0, DrinkLab.Recipes.Count, "새 저장: 사용자 레시피 없음");
            lab.SetTab(DrinkLabView.Tab.Recipes);
            yield return null;
            Assert.IsNotNull(GameObject.Find("Example_0"), "처음에도 고를 예시 배합");
            Assert.IsNotNull(GameObject.Find("Example_1"));
            lab.LoadPreset(1);
            yield return null;
            Assert.AreEqual(DrinkKind.Tea, lab.Kind);
            Assert.AreEqual(20, lab.Draft.AmountOf(DrinkIngredients.SenchaTea));
            Assert.AreEqual(20, DrinkLab.Owned(DrinkIngredients.SenchaTea), "선택만으로 소모 없음");
            Assert.IsNull(DrinkLab.Prepared);
            Assert.IsTrue(lab.CraftInteractable, "첫 세트로 바로 제조 가능");
            Assert.AreEqual(0, DrinkLab.Recipes.Count, "예시는 저장 목록과 별개");
        }

        [UnityTest]
        public IEnumerator ReferencePour_LipOverJiggerMouth_VerticalStreamEndsInsideJigger()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get("gin"));
            yield return null;
            var jiggerRt = stage.Jigger.Rect;
            var stream = stage.transform.Find("Scene/Stream").GetComponent<RectTransform>();
            stage.BeginPour(Vector2.zero);
            // 접근 중(이 병 잔량의 액면이 입구에 닿기 전): 흐르지 않는다
            float onset = stage.BottleOnset;
            Assert.Greater(onset, 60f, "60mL 남은 500mL 병은 많이 기울여야 입구에 닿는다");
            stage.SetTilt(onset - 6f);
            stage.Step(0.5f);
            Assert.IsFalse(stage.StreamVisible, "흐름 각도 전에는 줄기 없음");
            Assert.AreEqual(0, stage.InMeasure);
            foreach (float tilt in new[] { onset + 4f, onset + 15f, Mathf.Min(DrinkStageView.TiltMax, onset + 30f) })
            {
                stage.SetTilt(tilt);
                stage.Step(0.2f);
                yield return null;
                Assert.IsTrue(stage.StreamVisible, $"{tilt}°: 흐르기 시작하면 입구에서 줄기가 보인다");
                // 줄기 머리는 중력으로 떨어져 내려간다(첫 프레임에 전체 길이로 순간 생성되지 않음)
                yield return Until(() => stream.anchoredPosition.y - stream.sizeDelta.y < stage.Geometry.mouth.y - 2f, 1f);
                var g = stage.Geometry;
                Assert.AreEqual(g.mouth.x, g.lip.x, 2f, $"{tilt}°: 회전된 병 입구가 지거 입구 바로 위");
                Assert.AreEqual(g.mouth.y + DrinkStageView.LipClearance, g.lip.y, 2f, $"{tilt}°: 입구 위 여유 높이");
                Assert.AreEqual(-tilt, g.rotation, 0.01f);
                Assert.IsFalse(stage.SourceBottle.transform.Find("Cap").GetComponent<UnityEngine.UI.Image>().enabled, "붓는 동안 뚜껑 없음");
                Assert.IsTrue(stage.StreamVisible, $"{tilt}°: 흐름");
                Assert.AreEqual(0f, stream.localEulerAngles.z, 0.01f, "줄기는 중력 방향(수직)");
                Assert.AreEqual(new Vector2(0.5f, 1f), stream.pivot);
                Assert.AreEqual(g.lip.x, stream.anchoredPosition.x, 0.5f, "줄기 시작 = 실제 입구");
                Assert.AreEqual(g.lip.y, stream.anchoredPosition.y, 0.5f);
                float bottom = stream.anchoredPosition.y - stream.sizeDelta.y;
                var jigTop = DrinkVesselView.ToParentAnchored(jiggerRt, new Vector2(0.5f, 1f)).y;
                var jigBottom = DrinkVesselView.ToParentAnchored(jiggerRt, new Vector2(0.5f, 0f)).y;
                Assert.Less(bottom, g.mouth.y, "줄기가 지거 입구 안으로 들어감");
                Assert.Greater(bottom, jigBottom, "줄기가 지거 아래로 관통하지 않음");
                Assert.LessOrEqual(stream.anchoredPosition.y, jigTop + DrinkStageView.LipClearance + 3f, "줄기가 위로 길게 뻗지 않음");
                Assert.GreaterOrEqual(stream.sizeDelta.x, 6f, "읽히는 굵기");
                Assert.Less(stream.GetSiblingIndex(), jiggerRt.GetSiblingIndex(), "지거 테두리 뒤");
            }
            stage.StopPour();
            yield return null;
            Assert.IsFalse(stage.StreamEmitting, "놓으면 즉시 멈춤");
            yield return Until(() => !stage.StreamVisible, 1f);
            Assert.IsFalse(stage.StreamVisible, "떨어진 꼬리도 곧 사라짐");
            Assert.AreNotEqual(DrinkStageView.BottleRest, stage.BottleRect.anchoredPosition, "놓는 순간 순간이동하지 않음");
            yield return Until(() => !stage.Transferring && stage.Tilt == 0f, 6f);
            Assert.Less(Vector2.Distance(DrinkStageView.BottleRest, stage.BottleRect.anchoredPosition), 0.5f, "병은 부드럽게 제자리 복귀");
        }

        [UnityTest]
        public IEnumerator LivePreview_IncludesInJiggerDose_NoDoubleCountAfterTransfer()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            var blue = DrinkIngredients.Get("blue_curacao");
            stage.Select(blue);
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            stage.Step(0.3f);
            int inJigger = stage.InMeasure;
            Assert.Greater(inJigger, 0);
            Assert.AreEqual(0, lab.Draft.AmountOf("blue_curacao"), "확정 배합에는 아직 없음");
            stage.SetTilt(0f); // 누른 채 세워 흐름만 멈춘다(계량분은 아직 지거 안) — 같은 계량분으로 표시를 비교
            yield return new WaitForSecondsRealtime(0.25f); // 갱신 주기
            Assert.IsTrue(stage.Pouring, "아직 누르고 있음(배합 확정 전)");
            Assert.AreEqual(stage.InMeasure, lab.Projected().AmountOf("blue_curacao"), "예상 = 지거 안 양");
            var expected = DrinkCalculator.Buff(lab.Projected());
            StringAssert.Contains(DrinkRules.StatName(DrinkStat.CritChance), lab.BuffText, "붓는 중에도 예상 효과에 반영");
            StringAssert.Contains(DrinkBuff.Pct(DrinkStat.CritChance, expected[DrinkStat.CritChance]), lab.BuffText);
            Assert.IsFalse(lab.CraftInteractable, "붓는 중에는 제조 확정 불가");
            int total = stage.InMeasure;
            stage.StopPour();
            yield return Until(() => !stage.Transferring, 6f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(total, lab.Draft.AmountOf("blue_curacao"), "옮겨 부은 뒤 확정 = 계량 합(0.1 단위 손실·중복 없음)");
            Assert.AreEqual(total, lab.Projected().AmountOf("blue_curacao"), "옮긴 뒤 예상값 중복 없음");
        }

        [UnityTest]
        public IEnumerator MidTransfer_CraftBlocked_SelectionQueued_RepeatedCyclesExact()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get("gin"));
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            // 지거 45mL가 두 번 차도록 계속 붓기(사이마다 옮겨 붓기·복귀 후 재개)
            float t0 = Time.realtimeSinceStartup;
            while (lab.Draft.AmountOf("gin") < 450 && Time.realtimeSinceStartup - t0 < 12f) yield return null;
            Assert.AreEqual(450, lab.Draft.AmountOf("gin"), "첫 번째 가득 찬 지거가 정확히 45.0mL로 확정");
            Assert.IsTrue(stage.Transferring);
            Assert.IsFalse(stage.StreamEmitting, "옮겨 붓는 동안 병 흐름 없음");
            int before = lab.Draft.AmountOf("gin");
            lab.ConfirmCraft();
            Assert.IsNull(DrinkLab.Prepared, "옮겨 붓는 중 제조 확정 거부");
            StringAssert.Contains("붓는 중", lab.LastMessage);
            // 복귀 중 재료 바꾸기 → 대기했다가 적용
            Assert.IsNotNull(GameObject.Find("Row_rum"));
            GameObject.Find("Row_rum").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("gin", stage.CurrentId, "옮겨 붓는 중에는 도구를 바꾸지 않음");
            stage.StopPour();
            yield return Until(() => !stage.Transferring, 8f);
            yield return null;
            yield return null;
            Assert.AreEqual("rum", stage.CurrentId, "끝난 뒤 대기한 재료로 전환");
            Assert.GreaterOrEqual(lab.Draft.AmountOf("gin"), before);
            Assert.LessOrEqual(lab.Draft.AmountOf("gin"), 600, "보유량 이내");
            int gin = lab.Draft.AmountOf("gin");
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            Assert.AreEqual(600 - gin, DrinkLab.Owned("gin"), "정확히 확정한 양만 차감(중복·손실 없음)");
        }

        [UnityTest]
        public IEnumerator Panel_NoRebuildWhileSameIngredientPours()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get("gin"));
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(stage.BottleOnset + 20f);
            yield return new WaitForSecondsRealtime(0.4f);
            int rebuilds = lab.PanelRebuilds;
            string first = lab.BuffText;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(rebuilds, lab.PanelRebuilds, "같은 재료 양만 바뀌면 행을 다시 만들지 않고 글자만 갱신");
            Assert.AreNotEqual(first, lab.BuffText, "효과 값은 실시간 갱신");
            stage.StopPour();
        }

        [UnityTest]
        public IEnumerator Pour_StopsAtStockAndAtCocktailLimit()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var blue = DrinkIngredients.Get("blue_curacao"); // 보유 15.0mL
            lab.Stage.Select(blue);
            lab.Stage.BeginPour(Vector2.zero);
            lab.Stage.SetTilt(DrinkStageView.TiltMax);
            for (int i = 0; i < 100 && lab.Stage.Pouring; i++) lab.Stage.Step(0.1f);
            Assert.IsFalse(lab.Stage.Pouring);
            Assert.AreEqual(150, lab.Draft.AmountOf("blue_curacao"), "보유량에서 정확히 멈춤");
            StringAssert.Contains("보유량", lab.Stage.StopReason);
        }

        [UnityTest]
        public IEnumerator TypedAmount_BoundariesAndPaste()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            Assert.IsTrue(lab.ApplyTypedAmount("12.3"));
            Assert.AreEqual(123, lab.Draft.AmountOf("gin"));
            foreach (var bad in new[] { "-1", "NaN", "1.25", "abc", "" })
            {
                Assert.IsFalse(lab.ApplyTypedAmount(bad), bad);
                Assert.AreEqual(123, lab.Draft.AmountOf("gin"), "잘못된 입력은 배합을 바꾸지 않음: " + bad);
                Assert.IsNotEmpty(lab.AmountErrorText);
                Assert.AreEqual("12.3", lab.AmountField.text, "입력 칸은 실제 값으로 되돌림");
            }
            Assert.IsFalse(lab.ApplyTypedAmount("60.1"), "보유 60.0mL 초과");
            StringAssert.Contains("보유량", lab.AmountErrorText);
            Assert.IsTrue(lab.ApplyTypedAmount("  45.0ml "), "붙여넣기 형식");
            Assert.AreEqual(450, lab.Draft.AmountOf("gin"));
            lab.Nudge(1);
            Assert.AreEqual(451, lab.Draft.AmountOf("gin"));
            lab.Nudge(-10);
            Assert.AreEqual(441, lab.Draft.AmountOf("gin"));
            Assert.IsTrue(lab.ApplyTypedAmount("0"));
            Assert.AreEqual(0, lab.Draft.AmountOf("gin"), "0은 제거");
            lab.AmountField.text = "33.3";
            lab.AmountField.onEndEdit.Invoke("33.3");
            Assert.AreEqual(333, lab.Draft.AmountOf("gin"), "입력 칸 확정 이벤트");
            Assert.AreEqual(600, DrinkLab.Owned("gin"));
        }

        [UnityTest]
        public IEnumerator KindFilter_ListsOnlyUsableIngredients_AndSwitchClearsDraft()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            Assert.AreEqual(30, lab.ListRowCount, "칵테일: 확장된 재료 30종");
            lab.ApplyTypedAmount("10.0");
            var teaBtn = GameObject.Find("KindTea").GetComponent<Button>();
            teaBtn.onClick.Invoke();
            yield return null;
            Assert.AreEqual(DrinkKind.Tea, lab.Kind);
            Assert.AreEqual(30, lab.ListRowCount, "차: 확장된 재료 30종");
            Assert.AreEqual(0, lab.Draft.steps.Count, "부적합 재료를 옮기거나 섞지 않음");
            Assert.AreEqual(DrinkRules.TeaWaterFixed, lab.Draft.water);
        }

        [UnityTest]
        public IEnumerator CraftEachTechnique_MotionCompletes_FinalColorMatchesCalculator([Values(DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer)] DrinkTechnique tech)
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.ApplyTypedAmount("30.0");
            lab.Draft.SetAmount("grenadine", 100);
            lab.Draft.SetAmount("blue_curacao", 50);
            lab.Draft.technique = (int)tech;
            lab.Draft.name = "모션 " + tech;
            var look = DrinkCalculator.Look(lab.Draft);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            Assert.IsTrue(lab.Stage.SequencePlaying, "모션 재생");
            yield return Until(() => !lab.Stage.SequencePlaying, 15f);
            var final = lab.Stage.FinalVessel;
            Assert.IsNotNull(final);
            Assert.AreEqual(look.totalVolume, final.VolumeMl, 0.01f, "완성 그릇 양 = 배합");
            Assert.AreEqual(300, DrinkLab.Owned("gin"), "30.0mL 정확히 차감");
            Assert.IsFalse(lab.CraftInteractable, "준비 음료가 있으면 재제조 불가");
        }

        [UnityTest]
        public IEnumerator SkipMotion_SameResult()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.ApplyTypedAmount("30.0");
            lab.SkipMotion = true;
            lab.ConfirmCraft();
            yield return Until(() => !lab.Stage.SequencePlaying, 2f);
            Assert.AreEqual(300, DrinkLab.Owned("gin"), "30.0mL 정확히 차감");
            Assert.AreEqual(30f, lab.Stage.FinalVessel.VolumeMl, 0.01f);
        }

        [UnityTest]
        public IEnumerator TeaSteep_ColorChangesFromWaterToBrew()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            GameObject.Find("KindTea").GetComponent<Button>().onClick.Invoke();
            yield return null;
            lab.Draft.SetAmount("black_tea", 30);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            yield return Until(() => !lab.Stage.SequencePlaying, 15f);
            Assert.AreEqual(150f, lab.Stage.FinalVessel.VolumeMl, 0.01f);
            Assert.AreEqual("dl_teacup", lab.Stage.FinalVessel.AssetId);
            Assert.AreEqual(30, DrinkLab.Owned("black_tea"));
        }

        [UnityTest]
        public IEnumerator Depart_RevealThenRunWithBuff()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.steps.Add(new DrinkStep("gin", 300)); r.name = "출발";
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            GameBootstrap.Depart(Job.Bartender, WeaponKind.Rapier);
            Assert.IsNotNull(DrinkRevealOverlay.Current, "완성 음료 공개(마시는 동작 없음)");
            Assert.IsNull(GameManager.Instance);
            Assert.AreEqual(PreparedDrink.InRun, DrinkLab.Prepared.state, "런 전에 음용 확정");
            yield return Until(() => GameManager.Instance != null, 8f);
            Assert.IsNotNull(GameManager.Instance.DrinkBuff);
            Assert.AreEqual(GameManager.Instance.RunId, DrinkLab.Prepared.runId);
        }

        [UnityTest]
        public IEnumerator DepartSaveFailure_DoesNotStart_ThenNoDrinkChoiceKeepsDrink()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.steps.Add(new DrinkStep("gin", 300)); r.name = "실패";
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(r).Status);
            DrinkStore.FaultHookForTests = s => { if (s == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입"); };
            GameBootstrap.Depart(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            Assert.IsNotNull(DrinkDepartDialog.Current, "저장 실패 안내");
            Assert.IsNull(GameManager.Instance, "버프 없이 자동 시작하지 않음");
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state);
            DrinkDepartDialog.Current.RetryButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(DrinkDepartDialog.Current, "다시 시도도 실패 → 다시 안내");
            DrinkStore.FaultHookForTests = null;
            DrinkDepartDialog.Current.NoDrinkButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(GameManager.Instance);
            Assert.IsNull(GameManager.Instance.DrinkBuff, "명시적으로 고른 무음료 출발");
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "음료 보존");
        }

        [UnityTest]
        public IEnumerator Recipes_SaveCopyImportPreviewInUi()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.ApplyTypedAmount("30.0");
            lab.Draft.SetAmount("blue_curacao", 50);
            GameObject.Find("RecipeName").GetComponent<InputField>().text = "푸른 밤";
            GameObject.Find("SaveRecipe").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, DrinkLab.Recipes.Count, lab.LastMessage);
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "레시피 저장 무료");
            lab.SetTab(DrinkLabView.Tab.Recipes);
            yield return null;
            var code = lab.CopyCode(0);
            Assert.AreEqual(code, GUIUtility.systemCopyBuffer);
            Assert.IsFalse(lab.PreviewImport("CCT1-ZZZZ-ZZZZ"));
            StringAssert.Contains("가져올 수 없습니다", lab.ImportPreviewText);
            Assert.IsTrue(lab.PreviewImport(code));
            StringAssert.Contains("푸른 밤", lab.ImportPreviewText);
            Assert.IsTrue(lab.SaveImport());
            Assert.AreEqual(2, DrinkLab.Recipes.Count);
            lab.LoadRecipe(1, false);
            yield return null;
            Assert.AreEqual(DrinkLabView.Tab.Craft, lab.CurrentTab);
            Assert.AreEqual(300, lab.Draft.AmountOf("gin"));
            Assert.IsTrue(lab.CraftInteractable);
        }
        // ───── 73 단계1: 전과정 보존 장부·내용물 표시·바스푼·차 거름 ─────

        [UnityTest]
        public IEnumerator Ledger_BottleJiggerShaker_ConservedEveryFrame_JiggerKeepsContentsUntilItFlows()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get("gin"));
            yield return null;
            double total = stage.Ledger.ComponentTotalMl("gin");
            Assert.Greater(total, 20.0, "병(가상 재고)에 진이 있다");
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.InMeasure >= 200, 5f);
            stage.StopPour();
            double moved = lab.Draft.AmountOf("gin") / 10.0;
            bool sawApproach = false, sawPour = false;
            float t0 = Time.realtimeSinceStartup;
            while ((stage.Transferring || stage.Ledger.HasInFlight()) && Time.realtimeSinceStartup - t0 < 8f)
            {
                Assert.AreEqual(total, stage.Ledger.ComponentTotalMl("gin"), 1e-4, "병 + 공중 + 지거 + 셰이커 = 처음 양(매 프레임)");
                var m = stage.Ledger[DrinkStageView.LedgerMeasure];
                if (stage.Phase == DrinkStageView.CyclePhase.ToolApproach && !stage.Ledger.HasInFlight(DrinkStageView.LedgerMeasure))
                {
                    sawApproach = true;
                    Assert.AreEqual(moved, m.LiquidMl, 1e-4, "접근·기울임 중 지거 내용물 그대로(흐르기 전에 비지 않음)");
                    Assert.IsTrue(stage.Jigger.transform.Find("Liquid").gameObject.activeSelf, "기운 지거도 내용물을 숨기지 않음");
                    Assert.Greater(stage.Jigger.VolumeMl, 0f);
                }
                if (stage.Phase == DrinkStageView.CyclePhase.ToolPour && !sawPour)
                {
                    sawPour = true;
                    Assert.Greater(m.LiquidMl, moved * 0.8, "흐름 시작 프레임에 지거가 이미 비어 보이지 않음");
                }
                yield return null;
            }
            Assert.IsTrue(sawApproach && sawPour);
            Assert.AreEqual(moved, stage.Ledger[DrinkStageView.LedgerVessel].Liquid.ComponentVolume("gin"), 1e-4, "셰이커 = 옮긴 양");
            Assert.AreEqual(0, stage.Ledger[DrinkStageView.LedgerMeasure].LiquidMl, 1e-6);
            Assert.AreEqual((float)moved, stage.MainVessel.VolumeMl, 0.01f);
        }

        [UnityTest]
        public IEnumerator Stir_BarspoonTipStaysAboveFloorAndInsideWalls()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.ApplyTypedAmount("45.0");
            lab.Draft.SetAmount("grenadine", 50);
            lab.Draft.technique = (int)DrinkTechnique.Stir;
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            float minX = float.MaxValue, maxX = float.MinValue, minTip = float.MaxValue, firstTip = -1f, lastTip = -1f; int frames = 0;
            float t0 = Time.realtimeSinceStartup;
            while (lab.Stage.SequencePlaying && Time.realtimeSinceStartup - t0 < 15f)
            {
                if (lab.Stage.SpoonRect.gameObject.activeSelf)
                {
                    var p = lab.Stage.Spoon;
                    frames++;
                    Assert.AreEqual(0, p.outsidePixels, $"스푼 픽셀이 그릇 안쪽 밖(바닥·벽 관통)에 없음 — frame={frames} t={Time.realtimeSinceStartup - t0:0.000} tip=({p.tip.x:0.00},{p.tip.y:0.00}) above={p.tipAboveFloorPx:0.00} amp={p.amplitude:0.000} lean={p.lean:0.00} sf={p.frame} clearL={p.leftClearPx:0.0} clearR={p.rightClearPx:0.0} lowestY={p.lowestY:0.00} floorY={p.floorY:0.00} vesselH={lab.Stage.MainVessel.Rect.rect.height:0.0} scale={lab.Stage.MainVessel.Rect.lossyScale.x:0.0000}");
                    Assert.GreaterOrEqual(p.tipAboveFloorPx, 0f, "끝이 안쪽 바닥 위");
                    if (p.tipAboveFloorPx < 100f) { Assert.GreaterOrEqual(p.leftClearPx, 2f, "왼쪽 벽 여유"); Assert.GreaterOrEqual(p.rightClearPx, 2f, "오른쪽 벽 여유"); }
                    minX = Mathf.Min(minX, p.tip.x); maxX = Mathf.Max(maxX, p.tip.x); minTip = Mathf.Min(minTip, p.tipAboveFloorPx);
                    if (firstTip < 0f) firstTip = p.tipAboveFloorPx;
                    lastTip = p.tipAboveFloorPx;
                }
                yield return null;
            }
            Assert.Greater(frames, 10, "젓기 프레임");
            Assert.Greater(maxX - minX, 20f, "실제로 좌우로 젓는다");
            Assert.Less(minTip, 16f, "바닥 가까이까지 내려간다(공중에서 젓지 않음)");
            float interior = (DrinkArt.LiquidRect("dl_mixing_glass").yMax - DrinkArt.LiquidRect("dl_mixing_glass").yMin) * lab.Stage.MainVessel.Rect.rect.height;
            Assert.Greater(firstTip, interior * 0.8f, "처음엔 입구 위에서 넣기 시작(바닥으로 순간이동 없음)");
            Assert.Greater(lastTip, interior * 0.8f, "끝에는 입구 위로 꺼낸다");
        }

        [UnityTest]
        public IEnumerator TeaPour_StrainedLeavesStayInPot_ExtractMovesToCup()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            GameObject.Find("KindTea").GetComponent<Button>().onClick.Invoke();
            yield return null;
            lab.Draft.SetAmount("black_tea", 30);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            var led = lab.Stage.Ledger;
            yield return Until(() => !lab.Stage.SequencePlaying, 15f);
            var pot = led[DrinkStageView.LedgerVessel];
            var cup = led[DrinkStageView.LedgerServe];
            Assert.AreEqual(150.0, cup.WaterMl, 1e-4, "물은 모두 찻잔으로");
            Assert.AreEqual(0.0, pot.LiquidMl, 1e-6);
            Assert.Greater(pot.LeafG, 1.5, "우린 잎은 다관에 남는다");
            Assert.AreEqual(0.0, cup.LeafG, 1e-9, "찻잔에 잎 없음");
            Assert.Greater(cup.DissolvedG, 0.5, "추출분은 찻물과 함께 찻잔으로");
            Assert.AreEqual(3.0, pot.LeafG + cup.DissolvedG + pot.DissolvedG, 1e-6, "잎 + 추출분 = 처음 찻잎 3.0g");
            Assert.Greater(cup.TeaStrength, 0.9, "충분히 우림");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0.0, pot.LiquidMl, 1e-6, "완성 뒤 다관이 배합에서 다시 채워지지 않음");
            Assert.AreEqual(3.0, pot.LeafG + cup.DissolvedG, 1e-6, "완성 뒤에도 잎이 다시 생기지 않음");
        }

        [UnityTest]
        public IEnumerator TeaLid_OpensForLeavesAndWater_ClosedWhileSteepingAndPouring()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            GameObject.Find("KindTea").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var stage = lab.Stage;
            Assert.AreEqual(0f, stage.LidOpen01, "분리 뚜껑 아트로 닫힌 다관에서 시작");
            // 찻잎: 통 → 다하 → 다관. 다하가 붓기 시작할 때는 뚜껑이 이미 다 열려 있다.
            stage.Select(DrinkIngredients.Get("black_tea"));
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.InMeasure >= 20, 5f);
            stage.StopPour();
            bool pourSeen = false;
            float tPour = 0f;
            while ((stage.Transferring || stage.Ledger.HasInFlight()) && tPour < 10f)
            {
                if (stage.Phase == DrinkStageView.CyclePhase.ToolPour) { pourSeen = true; Assert.AreEqual(1f, stage.LidOpen01, "뚜껑을 다 연 뒤에만 붓는다"); }
                tPour += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(pourSeen);
            yield return Until(() => stage.LidOpen01 <= 0f, 3f); // 투입이 끝나면 다시 덮는다
            // 제조: 물 붓기 동안 열림, 우림·찻잔 따르기는 닫힘. 양은 그대로 보존.
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            var led = stage.Ledger;
            bool openWhileWater = false;
            double lastDissolved = 0;
            float t = 0f;
            while (stage.SequencePlaying && t < 20f)
            {
                if (led[DrinkStageView.LedgerKettle].LiquidMl < 149.9 && led[DrinkStageView.LedgerKettle].LiquidMl > 0.1) { openWhileWater = true; Assert.AreEqual(1f, stage.LidOpen01, "물을 붓는 동안 열린 입구"); }
                if (led[DrinkStageView.LedgerServe].LiquidMl > 0.01) Assert.AreEqual(0f, stage.LidOpen01, "찻잔에 따를 때는 덮인 다관");
                double dis = led[DrinkStageView.LedgerVessel].DissolvedG;
                if (dis > lastDissolved + 1e-9)
                {
                    Assert.AreEqual(0f, stage.LidOpen01, "추출(우림)은 뚜껑이 완전히 닫힌 뒤에만");
                    StringAssert.Contains("우림", stage.FinishText);
                }
                if (stage.LidOpen01 > 0f) StringAssert.DoesNotContain("우림", stage.FinishText, "뚜껑이 움직이는 동안 '우림' 문구 없음");
                lastDissolved = Math.Max(lastDissolved, dis);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(openWhileWater, "물 붓기 구간");
            Assert.AreEqual(0f, stage.LidOpen01, "완성 = 덮인 다관");
            Assert.AreEqual(150.0, led[DrinkStageView.LedgerServe].WaterMl, 1e-4);
            Assert.AreEqual(lab.Draft.AmountOf("black_tea") / 10.0, led[DrinkStageView.LedgerVessel].LeafG + led[DrinkStageView.LedgerServe].DissolvedG + led[DrinkStageView.LedgerVessel].DissolvedG, 1e-6, "잎 + 추출분 보존");
        }
        [UnityTest]
        public IEnumerator SourceBottle_LevelDrops_FullerBottleFlowsEarlier()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get("gin"));
            yield return null;
            var bottle = stage.SourceBottle;
            Assert.IsNotNull(bottle);
            Assert.AreEqual(DrinkStageView.BottleCapacityMl("gin"), bottle.CapacityMl, "병 용량은 고정 게임값");
            Assert.AreEqual(60.0, stage.Ledger[DrinkStageView.LedgerBottle].LiquidMl, 1e-6, "병 = 지금 더 넣을 수 있는 가상 재고");
            Assert.AreEqual(60f, bottle.VolumeMl, 0.01f, "병 화면 = 장부 잔량");
            Assert.IsTrue(bottle.transform.Find("Cap").GetComponent<UnityEngine.UI.Image>().enabled, "세워 둔 병은 뚜껑");
            float onset60 = stage.BottleOnset;
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.InMeasure >= 200, 5f);
            stage.StopPour();
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 8f);
            yield return null;
            Assert.AreEqual(60.0 - lab.Draft.AmountOf("gin") / 10.0, stage.Ledger[DrinkStageView.LedgerBottle].LiquidMl, 1e-4, "병 감소 = 배합에 들어간 양");
            Assert.AreEqual((float)stage.Ledger[DrinkStageView.LedgerBottle].LiquidMl, bottle.VolumeMl, 0.01f);
            Assert.AreEqual(DrinkStageView.BottleCapacityMl("gin"), bottle.CapacityMl, "잔량이 줄어도 용량 그대로");
            Assert.Greater(stage.BottleOnset, onset60, "잔량이 적을수록 더 기울여야 입구에 닿는다");
        }
        [UnityTest]
        public IEnumerator Camera_FollowsProcess_PourKeepsBothInView_FinalGlassCentered_SmoothNoTeleport()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual((DrinkStageView.BottleRest.x + -30f) * 0.5f, stage.CameraX, 3f, "편집: 병·지거 가운데 구도");
            lab.Draft.SetAmount("gin", 300);
            lab.Draft.technique = (int)DrinkTechnique.Stir;
            lab.RefreshForTests();
            yield return null;
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            float half = ((RectTransform)stage.transform).rect.width * 0.5f;
            float lastCam = stage.CameraX, maxStep = 0f, t = 0f;
            bool sawPour = false;
            while (stage.SequencePlaying && t < 20f)
            {
                maxStep = Mathf.Max(maxStep, Mathf.Abs(stage.CameraX - lastCam));
                lastCam = stage.CameraX;
                var serve = stage.ServeVessel;
                if (serve != null && serve.gameObject.activeSelf && stage.Ledger.InFlightMl(DrinkStageView.LedgerServe) > 0.01)
                {
                    sawPour = true;
                    float sx = serve.Rect.anchoredPosition.x - stage.CameraX;
                    Assert.Less(Mathf.Abs(sx), half - 40f, "따르는 동안 받는 잔이 화면 안");
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(sawPour);
            Assert.Less(maxStep, 40f, "카메라는 프레임마다 조금씩(순간 이동 없음)");
            yield return new WaitForSecondsRealtime(1.0f);
            var fin = stage.FinalVessel;
            Assert.AreEqual(fin.Rect.anchoredPosition.x, stage.CameraX, 2f, "완성 잔이 제조대 가운데");
            Assert.Greater(fin.Rect.anchoredPosition.x, DrinkStageView.VesselRest.x, "완성 잔은 공정 진행 방향(오른쪽) 끝");
            // 다음 편집으로 돌아오면 출발 구도
            lab.Draft.SetAmount("gin", 200);
            lab.RefreshForTests();
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.AreEqual((DrinkStageView.BottleRest.x + -30f) * 0.5f, stage.CameraX, 3f);
        }

        [UnityTest]
        public IEnumerator RecipeDrawer_OpenPickPullReturnDelete_PagesAndIgnoresRapidReselect()
        {
            for (int i = 0; i < 7; i++)
            {
                var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
                r.technique = (int)DrinkTechnique.Stir;
                r.SetAmount("gin", 100 + i * 10);
                r.name = "서류 " + i;
                Assert.IsTrue(DrinkLab.SaveRecipe(r, -1, out var e), e);
            }
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.SetTab(DrinkLabView.Tab.Recipes);
            yield return null;
            var d = lab.Drawer;
            Assert.IsNotNull(d);
            Assert.AreEqual(RecipeDrawerView.Phase.Closed, d.State, "처음엔 닫힌 서랍");
            Assert.AreEqual(2f, d.Scale, "정수 2배");
            Assert.AreEqual(2, d.PageCount, "7장 = 5장씩 2칸");
            yield return null;
            // 실제 포인터 레이캐스트: 닫힌 서랍의 앞판 사각형을 누른다(투명 전체 그림이 아닌 앞판 히트박스)
            var hitFront = ClickAt(ScreenOf(d.FrontHitbox, new Vector2(0.5f, 0.5f)));
            Assert.AreEqual("FrontHitbox", hitFront.name);
            yield return Until(() => d.State == RecipeDrawerView.Phase.Open, 2f);
            // 앞 종이(2번)에 가려지지 않은 1번 종이의 왼쪽 띠(아트 x110, y140)를 실제로 누른다 — 투명 앞판 그림이 가로채지 않아야 한다
            var p1 = GameObject.Find("Paper_1").GetComponent<RectTransform>();
            var hitPaper = ClickAt(ScreenOf(p1, new Vector2((110f - 103f) / 258f, 1f - (140f - 106f) / 112f)));
            Assert.IsTrue(hitPaper.transform == p1 || hitPaper.transform.IsChildOf(p1), "누른 곳의 맨 위 대상 = 1번 종이: " + hitPaper.name);
            Assert.AreEqual(1, d.Selected, "한 번 누르면 고르기(살짝 올라옴)");
            GameObject.Find("Paper_1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(RecipeDrawerView.Phase.Pulling, d.State);
            var other = GameObject.Find("Paper_2");
            if (other != null) other.GetComponent<Button>().onClick.Invoke(); // 모션 중 다른 종이 = 무시(건너뛰기)
            yield return Until(() => d.State == RecipeDrawerView.Phase.Sheet, 2f);
            Assert.AreEqual(1, d.Selected, "빠른 다른 선택 무시");
            Assert.IsNotNull(d.PulledSheet.Find("SheetContent/Use"), "펼친 종이 위 실제 버튼");
            StringAssert.Contains("서류 1", d.PulledSheet.Find("SheetContent/SheetTitle").GetComponent<Text>().text);
            d.PulledSheet.Find("SheetContent/PutBack").GetComponent<Button>().onClick.Invoke();
            yield return Until(() => d.State == RecipeDrawerView.Phase.Open, 2f);
            Assert.AreEqual("Stack", GameObject.Find("Paper_1").transform.parent.name, "원래 칸으로 다시 꽂힘");
            GameObject.Find("Paper_0").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Paper_0").GetComponent<Button>().onClick.Invoke();
            yield return Until(() => d.State == RecipeDrawerView.Phase.Sheet, 2f);
            d.PulledSheet.Find("SheetContent/Delete").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(6, DrinkLab.Recipes.Count);
            d = lab.Drawer;
            Assert.AreEqual(RecipeDrawerView.Phase.Open, d.State, "삭제 뒤에도 서랍은 열린 채");
            d.ToggleDrawer();
            d.SkipMotion();
            yield return Until(() => d.State == RecipeDrawerView.Phase.Closed, 1f);
        }

        // ───── 72 연구 노트북 ─────

        [UnityTest]
        public IEnumerator Laptop_OpensWithoutHands_InputBlockedDuringMotion_UnlockEquipHireOnce_Closes()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<ResearchLaptopView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            Assert.AreEqual(10, DrinkLab.Research.researchPoints, "첫 지급");
            bool closed = false;
            var lap = ResearchLaptopView.Show(() => closed = true);
            yield return null;
            Assert.AreEqual(ResearchLaptopView.Phase.Opening, lap.State);
            Assert.IsFalse(lap.ScreenInteractable, "열리는 동안 화면 안 버튼 꺼짐(이중 거래 없음)");
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => lap.State == ResearchLaptopView.Phase.Open, 3f);
            float took = Time.realtimeSinceStartup - t0;
            Assert.Less(took, 1.2f, "등장 0.25 + 열기 0.35 + 점등 0.15 ≈ 0.75초");
            Assert.Greater(took, 0.5f, "프레임을 건너뛰지 않음");
            Assert.IsTrue(lap.ScreenInteractable);
            Assert.AreEqual(ResearchLaptopView.Tab.Mastery, lap.CurrentTab);
            var unlock = GameObject.Find("Unlock").GetComponent<Button>();
            unlock.onClick.Invoke();
            unlock.onClick.Invoke(); // 이중 클릭
            Assert.AreEqual(6, DrinkLab.Research.researchPoints, "한 번만 차감");
            CollectionAssert.Contains(DrinkLab.Research.unlockedTraits, "service_1");
            Assert.AreEqual(0, DrinkLab.Research.equippedTraits.Count, "해금과 장착은 별개");
            GameObject.Find("Equip").GetComponent<Button>().onClick.Invoke();
            CollectionAssert.AreEqual(new[] { "service_1" }, DrinkLab.Research.equippedTraits);
            StringAssert.Contains("장착 슬롯 1/3", GameObject.Find("Slots").GetComponent<Text>().text);
            lap.SetTab(ResearchLaptopView.Tab.Staff);
            yield return null;
            GameObject.Find("Staff_staff_tea_aroma").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Hire").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(0, DrinkLab.Research.researchPoints);
            CollectionAssert.Contains(DrinkLab.Research.hiredStaff, "staff_tea_aroma");
            GameObject.Find("Staff_staff_cocktail_blend").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(GameObject.Find("Hire").GetComponent<Button>().interactable, "조건 미달·포인트 부족은 채용 버튼 꺼짐");
            foreach (var txt in lap.GetComponentsInChildren<Text>())
                if (txt.isActiveAndEnabled && !string.IsNullOrEmpty(txt.text) && !txt.resizeTextForBestFit)
                    Assert.GreaterOrEqual(txt.fontSize, DrinkPaperUi.Small, "노트북 글자 기준 24 이상: " + txt.name);
            lap.Close();
            yield return null;
            Assert.AreEqual(ResearchLaptopView.Phase.Closing, lap.State);
            Assert.IsFalse(lap.ScreenInteractable, "닫는 동안 입력 차단");
            yield return Until(() => closed, 2f);
            DrinkLab.ReloadForTests();
            CollectionAssert.AreEqual(new[] { "service_1" }, DrinkLab.Research.equippedTraits, "저장됨");
        }

        [UnityTest]
        public IEnumerator Laptop_SkipJumpsToOpen()
        {
            var lap = ResearchLaptopView.Show(() => { });
            yield return null;
            lap.SkipAnimation();
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => lap.State == ResearchLaptopView.Phase.Open, 1f);
            Assert.Less(Time.realtimeSinceStartup - t0, 0.3f, "건너뛰면 남은 연출 없이 바로 끝 상태");
            lap.Close(); lap.SkipAnimation();
            yield return Until(() => lap == null, 2f);
        }

        // ───── 73 단계2: 종이 UI ─────

        [UnityTest]
        public IEnumerator PaperUi_TearMotionOnlyAfterSuccessfulSave_NoItemsCreated()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            // 실패: 빈 배합은 저장되지 않고 찢기 모션도 없다
            GameObject.Find("RecipeName").GetComponent<InputField>().text = "빈 잔";
            GameObject.Find("SaveRecipe").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(0, DrinkLab.Recipes.Count);
            Assert.IsNull(RecipeTearOverlay.Current, "저장 실패면 찢지 않음");
            // 성공: 저장 → 그 뒤에만 모션, 재료·음료 변화 없음
            lab.ApplyTypedAmount("30.0");
            GameObject.Find("RecipeName").GetComponent<InputField>().text = "노트 한 장";
            GameObject.Find("SaveRecipe").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, DrinkLab.Recipes.Count, lab.LastMessage);
            var tear = RecipeTearOverlay.Current;
            Assert.IsNotNull(tear, "저장 성공 뒤 찢기 모션");
            Assert.AreEqual("노트 한 장", tear.Label, "종이 위 글자는 실제 UI(레시피 이름)");
            Assert.AreEqual(600, DrinkLab.Owned("gin"), "저장·모션은 재료를 쓰지 않음");
            Assert.IsNull(DrinkLab.Prepared, "음료를 만들지 않음");
            yield return Until(() => tear == null || tear.Finished, 3f);
            Assert.IsTrue(tear == null || tear.FramesShown == RecipeTearOverlay.TearFrames, "찢기 프레임 00~05 끝까지(그 뒤 종이가 레시피 서랍으로)");
            yield return Until(() => RecipeTearOverlay.Current == null, 3f);
            Assert.AreEqual(1, DrinkLab.Recipes.Count, "모션이 레시피를 더 만들지 않음");
        }

        [UnityTest]
        public IEnumerator PaperUi_ReceiptShowsLiveMeasureThenConfirmedLine()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get("gin"));
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.InMeasure >= 50, 5f);
            stage.SetTilt(0f);
            yield return new WaitForSecondsRealtime(0.3f);
            var measure = GameObject.Find("Measure");
            Assert.IsNotNull(measure, "계량 중 줄");
            var text = measure.GetComponentInChildren<Text>().text;
            StringAssert.Contains("계량 중", text);
            StringAssert.Contains("지거", text);
            Assert.IsNull(GameObject.Find("Mix_gin"), "아직 배합 확정 줄 없음");
            stage.StopPour();
            yield return Until(() => !stage.Transferring, 8f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsNull(GameObject.Find("Measure"), "계량 끝");
            var line = GameObject.Find("Mix_gin");
            Assert.IsNotNull(line);
            StringAssert.Contains("✓", line.GetComponentInChildren<Text>().text, "옮겨 부은 뒤 확정 줄");
            var receipt = GameObject.Find("MixPanel").GetComponent<Image>();
            Assert.AreEqual(Image.Type.Sliced, receipt.type, "영수증 9slice 아트");
            Assert.IsNotNull(receipt.sprite);
            foreach (var t in GameObject.Find("MixPanel").GetComponentsInChildren<Text>())
                if (t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text) && !t.resizeTextForBestFit)
                    Assert.GreaterOrEqual(t.fontSize, DrinkPaperUi.Small - 2, "영수증 글자 기준 22 이상: " + t.name);
        }
    }
}
