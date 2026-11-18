using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 5(단위 따르기·제조 실습)·7(로비 노트북 안내) 실제 UI 경로(PlayMode).
    /// 단위 버튼 = 실제 병→지거 따르기, 연속 입력·Esc 중단에서 배합 반영 1회, 실습 단계가 실제 상태로만 진행.
    /// </summary>
    public class FeedbackBatch2PlayTests
    {
        string dir;

        static void Clear()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkLabView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<ResearchLaptopView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Clear();
            dir = Path.Combine(Application.temporaryCachePath, "fb2play_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DrinkLabView.PracticeSuppressForTests = true;
            NotebookGuide.SuppressForTests = true;
            Clear();
            yield return null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        IEnumerator OpenLab(Action<DrinkLabView> got)
        {
            var select = CharacterSelectView.Show((j, w) => { });
            yield return null;
            select.DrinkLabButton.onClick.Invoke();
            yield return null;
            got(select.DrinkLabScreen);
        }

        static IEnumerator WaitIdle(DrinkLabView lab, float max = 12f)
        {
            float t = 0f;
            while (t < max && (lab.Stage.Pouring || lab.Stage.Transferring)) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator UnitPour_PoursExactAmount_RejectsRepeatWhilePouring_CommitsOnce()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var gin = DrinkIngredients.Get(DrinkIngredients.Gin);
            lab.transform.GetComponentsInChildren<Button>(true).First(b => b.name == "Row_" + gin.id).onClick.Invoke();
            yield return null;
            Assert.IsNotNull(lab.transform.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Pour300"), "+30mL 단위 버튼");
            Assert.IsNull(lab.transform.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Nudge10" || b.name == "Nudge-10" || b.name == "Pour10"), "1 단위 버튼 없음");
            int stockBefore = DrinkLab.Owned(gin.id);
            int changes = 0;
            lab.Stage.MeasureChanged += () => { };
            Assert.IsTrue(lab.PourUnit(150));
            Assert.IsTrue(lab.Stage.AutoPouring, "단위 버튼 = 실제 따르기 시작(즉시 수치 변경 아님)");
            Assert.AreEqual(0, lab.Draft.AmountOf(gin.id), "따르기 전에는 배합에 반영되지 않음");
            Assert.IsFalse(lab.PourUnit(150), "따르는 중 연속 입력 거부");
            Assert.IsFalse(lab.PourUnit(300));
            yield return WaitIdle(lab);
            Assert.AreEqual(150, lab.Draft.AmountOf(gin.id), "정확히 15mL만 반영(중복 없음)");
            Assert.AreEqual(stockBefore, DrinkLab.Owned(gin.id), "재고는 제조 확정 전 그대로");
            Assert.IsTrue(lab.PourUnit(300));
            yield return WaitIdle(lab);
            Assert.AreEqual(450, lab.Draft.AmountOf(gin.id), "15 + 30");
            changes++;
            Assert.AreEqual(1, changes);
        }

        [UnityTest]
        public IEnumerator UnitPour_StopMidway_CommitsOnlyWhatIsInJigger_NeverAboveTarget()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            var gin = DrinkIngredients.Get(DrinkIngredients.Gin);
            lab.transform.GetComponentsInChildren<Button>(true).First(b => b.name == "Row_" + gin.id).onClick.Invoke();
            yield return null;
            Assert.IsTrue(lab.PourUnit(300));
            float t = 0f;
            while (t < 8f && lab.Stage.InMeasure <= 0) { t += Time.unscaledDeltaTime; yield return null; }
            lab.Stage.StopPour(); // Esc와 같은 정지 경로
            yield return WaitIdle(lab);
            int got = lab.Draft.AmountOf(gin.id);
            Assert.Greater(got, 0);
            Assert.LessOrEqual(got, 300, "목표를 넘지 않음");
            Assert.AreEqual(0, got % 10, "정수 mL");
            Assert.IsFalse(lab.Stage.AutoPouring);
            Assert.IsTrue(lab.PourUnit(50), "멈춘 뒤 다시 따를 수 있음");
            yield return WaitIdle(lab);
            Assert.AreEqual(got + 50, lab.Draft.AmountOf(gin.id));
        }

        [UnityTest]
        public IEnumerator Practice_StepsFollowRealState_GrantOnce_CraftEndsDone()
        {
            DrinkLabView.PracticeSuppressForTests = false;
            SettingsService.ResetCraftGuideForTests();
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            yield return null;
            Assert.AreEqual(DrinkLabView.PracticeStep.Intro, lab.CurrentPracticeStep, "첫 진입 실습 제안");
            lab.BeginPracticeSteps();
            yield return null;
            Assert.IsTrue(DrinkLab.PracticeGranted);
            Assert.AreEqual(DrinkLabView.PracticeStep.Gin, lab.CurrentPracticeStep);
            foreach (var id in new[] { DrinkIngredients.Gin, DrinkIngredients.Campari, DrinkIngredients.SweetVermouth })
            {
                lab.transform.GetComponentsInChildren<Button>(true).First(b => b.name == "Row_" + id).onClick.Invoke();
                yield return null;
                Assert.IsTrue(lab.PourUnit(300), id);
                yield return WaitIdle(lab);
                yield return null;
            }
            Assert.AreEqual(DrinkLabView.PracticeStep.Technique, lab.CurrentPracticeStep, "세 재료 30mL → 기법 단계");
            lab.Nudge(-50); yield return null;
            Assert.AreEqual(DrinkLabView.PracticeStep.Vermouth, lab.CurrentPracticeStep, "빼면 앞 단계로 되돌아감(실제 상태 기준)");
            lab.Nudge(50); yield return null;
            lab.transform.GetComponentsInChildren<Button>(true).First(b => b.name == "Tech" + DrinkTechnique.Build).onClick.Invoke();
            yield return null; yield return null;
            Assert.AreEqual(DrinkLabView.PracticeStep.Craft, lab.CurrentPracticeStep);
            lab.ConfirmCraft();
            lab.ConfirmCraft(); // 연속 입력 — 두 번째는 이미 준비 음료가 있어 실패(재료 이중 차감 없음)
            float t = 0f;
            while (t < 15f && lab.Stage.SequencePlaying) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            Assert.IsNotNull(DrinkLab.Prepared);
            Assert.AreEqual(DrinkLabView.PracticeStep.Done, lab.CurrentPracticeStep);
            Assert.AreEqual(0, DrinkLab.Owned(DrinkIngredients.Campari) % 10);
            lab.SkipPractice();
            Assert.AreEqual(DrinkLabView.PracticeStep.None, lab.CurrentPracticeStep);
            Assert.AreEqual(DrinkLabView.PracticeGuideVersion, SettingsService.Current.craftGuideVersion);
        }

        [UnityTest]
        public IEnumerator Lobby_NotebookGuide_AfterFirstEndedRun_SkipPersists()
        {
            NotebookGuide.SuppressForTests = false;
            var save = PermanentProgress.Current;
            int prevAb = save.abandonedRuns;
            int prevVer = SettingsService.Current.notebookGuideVersion;
            try
            {
                SettingsService.ResetNotebookGuide();
                save.abandonedRuns = Math.Max(1, save.abandonedRuns);
                DrinkLab.Research.unlockedTraits.Clear();
                var select = CharacterSelectView.Show((j, w) => { });
                yield return null;
                Assert.IsTrue(select.NotebookGuideVisible, "첫 판 뒤 로비 안내");
                select.transform.GetComponentsInChildren<Button>(true).First(b => b.name == "Skip").onClick.Invoke();
                yield return null;
                Assert.IsFalse(select.NotebookGuideVisible);
                Assert.IsFalse(NotebookGuide.Needed, "건너뛰기 = 완료 저장");
                Clear();
                var again = CharacterSelectView.Show((j, w) => { });
                yield return null;
                Assert.IsFalse(again.NotebookGuideVisible, "재진입 시 다시 안 뜸");
            }
            finally
            {
                save.abandonedRuns = prevAb;
                SettingsService.Current.notebookGuideVersion = prevVer;
            }
        }
    }
}
