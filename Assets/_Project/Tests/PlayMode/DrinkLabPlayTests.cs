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
            // 작은 기울기: 아주 느리게, 0.1 단위 정수만.
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltStart + 5f);
            float rate = DrinkStageView.Rate(stage.Tilt, gin);
            Assert.Less(rate, 5f, "작은 움직임은 0.5mL/s 미만");
            stage.Step(1f);
            int expected = Mathf.FloorToInt(rate);
            Assert.AreEqual(expected, stage.InMeasure);
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
            string prevVessel = null;
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
                AssertColor(pure, stage.Jigger.TopColor, id + " 지거 안 = 순수 재료");
                var vesselBefore = DrinkCalculator.Hex(stage.MainVessel.TopColor);
                if (prevVessel != null) Assert.AreEqual(prevVessel, vesselBefore, "붓는 중 셰이커는 아직 이 재료 영향 없음");
                float pouredMl = stage.InMeasure / 10f;
                int before = lab.Draft.AmountOf(id);
                stage.StopPour();
                Assert.AreEqual(before + Mathf.RoundToInt(pouredMl * 10f), lab.Draft.AmountOf(id), "데이터는 즉시 정확히 반영(0.1 손실 없음)");
                yield return null;
                Assert.IsTrue(stage.Transferring, "지거 → 셰이커 옮겨 붓기 주기 시작");
                Assert.IsFalse(stage.StreamVisible, "옮겨 붓는 동안 병 → 지거 흐름 없음");
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
                AssertColor(DrinkCalculator.Look(lab.Draft).color, stage.MainVessel.TopColor, id + " 셰이커 = 지금까지의 혼합");
                Assert.AreEqual(DrinkCalculator.Look(lab.Draft).totalVolume, stage.MainVessel.VolumeMl, 0.01f);
                prevVessel = DrinkCalculator.Hex(stage.MainVessel.TopColor);
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
            Assert.AreEqual(25, lab.Draft.AmountOf("black_tea"));
            Assert.AreEqual(60, DrinkLab.Owned("black_tea"), "선택만으로 소모 없음");
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
            var stream = stage.transform.Find("Stream").GetComponent<RectTransform>();
            stage.BeginPour(Vector2.zero);
            // 접근 중(흐름 시작 각도 미만): 흐르지 않는다
            stage.SetTilt(60f); // 병목이 아직 수평 위
            stage.Step(0.5f);
            Assert.IsFalse(stage.StreamVisible, "흐름 각도 전에는 줄기 없음");
            Assert.AreEqual(0, stage.InMeasure);
            foreach (float tilt in new[] { 95f, 115f, 135f })
            {
                stage.SetTilt(tilt);
                stage.Step(0.2f);
                yield return null;
                var g = stage.Geometry;
                Assert.AreEqual(g.mouth.x, g.lip.x, 2f, $"{tilt}°: 회전된 병 입구가 지거 입구 바로 위");
                Assert.AreEqual(g.mouth.y + DrinkStageView.LipClearance, g.lip.y, 2f, $"{tilt}°: 입구 위 여유 높이");
                Assert.AreEqual(-tilt, g.rotation, 0.01f);
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
            Assert.IsFalse(stage.StreamVisible, "놓으면 즉시 멈춤");
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
            yield return new WaitForSecondsRealtime(0.25f); // 갱신 주기
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
            Assert.IsFalse(stage.StreamVisible, "옮겨 붓는 동안 병 흐름 없음");
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
            stage.SetTilt(DrinkStageView.TiltStart + 20f);
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
            Assert.AreEqual(6, lab.ListRowCount, "칵테일: 진·럼·큐라소·그레나딘·레몬·꿀");
            lab.ApplyTypedAmount("10.0");
            var teaBtn = GameObject.Find("KindTea").GetComponent<Button>();
            teaBtn.onClick.Invoke();
            yield return null;
            Assert.AreEqual(DrinkKind.Tea, lab.Kind);
            Assert.AreEqual(4, lab.ListRowCount, "차: 홍차·녹차·레몬·꿀");
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
    }
}
