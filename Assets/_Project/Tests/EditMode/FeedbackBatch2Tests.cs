using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 둘째 묶음(5·7·23·24) 규칙·저장·원자성 시험. 화면은 QA 투어(feedback2)로 따로 확인한다.
    /// </summary>
    public class FeedbackBatch2Tests
    {
        string dir, settingsPath, savePath;
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "fb2_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
            settingsPath = Path.Combine(dir, "settings.json");
            SettingsService.SetForTests(settingsPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            savePath = Path.Combine(dir, "save.json");
            PermanentProgress.SetPathForTests(savePath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            DrinkStore.FaultHookForTests = null;
            DrinkLab.SetPathForTests(null);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            NotebookGuide.SuppressForTests = true;
            try { Directory.Delete(dir, true); } catch { }
        }

        Transform Canvas()
        {
            var go = new GameObject("C", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(1920f, 1080f);
            created.Add(go);
            return go.transform;
        }

        // ── 5 단위 ──
        [Test]
        public void PourUnits_NoOneUnit_LiquidMatchesJiggerMarks_TeaFitsLeafLimit()
        {
            foreach (var kind in new[] { DrinkKind.Cocktail, DrinkKind.Tea, DrinkKind.Coffee })
                foreach (var form in new[] { IngredientForm.Liquid, IngredientForm.Leaf })
                {
                    var u = DrinkLabView.PourUnits(form, kind);
                    CollectionAssert.DoesNotContain(u, 10, $"{kind}/{form}: 1 단위 버튼 없음");
                    foreach (var x in u) Assert.AreEqual(0, x % 10, "정수 mL/g");
                }
            CollectionAssert.AreEqual(new[] { 50, 150, 300 }, DrinkLabView.PourUnits(IngredientForm.Liquid, DrinkKind.Cocktail));
            Assert.LessOrEqual(DrinkLabView.PourUnits(IngredientForm.Liquid, DrinkKind.Cocktail).Max(), DrinkRules.JiggerCapacity, "한 번 따르기가 지거(45mL)를 넘지 않음");
            Assert.LessOrEqual(DrinkLabView.PourUnits(IngredientForm.Leaf, DrinkKind.Tea).Max(), DrinkRules.TeaLeafMax, "차 단위는 찻잎 상한 안");
        }

        // ── 5 실습 지급: 부족분만·1회·판매 불가 출처·출처 추적 ──
        [Test]
        public void PracticeKit_ExampleMatchesNegroniPreset_DeficitOnlyOnce_TrackedNotSellable()
        {
            var r = DrinkLab.PracticeRecipe;
            Assert.AreEqual("IBA 네그로니", r.name);
            Assert.AreEqual((int)DrinkTechnique.Build, r.technique, "예제 기법 = 기존 네그로니 데이터(빌드)");
            Assert.AreEqual(300, r.AmountOf(DrinkIngredients.Gin));
            Assert.AreEqual(300, r.AmountOf(DrinkIngredients.Campari));
            Assert.AreEqual(300, r.AmountOf(DrinkIngredients.SweetVermouth));
            Assert.IsFalse(DrinkLab.PracticeGranted, "새 저장 기본값 = 미지급");
            int ginBefore = DrinkLab.Owned(DrinkIngredients.Gin);
            Assert.IsTrue(DrinkLab.GrantPracticeKit(out var got, out var err), err);
            foreach (var st in r.steps) Assert.GreaterOrEqual(DrinkLab.Owned(st.id), st.amount, st.id + " 실습 가능");
            foreach (var g in got) Assert.AreEqual(g.amount, DrinkLab.EndlessOwned(g.id), g.id + ": 지급분 = 판매 불가 출처 장부");
            if (ginBefore >= 300) Assert.IsFalse(got.Any(g => g.id == DrinkIngredients.Gin), "이미 있으면 지급 없음(부족분만)");
            int total = DrinkIngredients.All.Sum(d => DrinkLab.Owned(d.id));
            Assert.IsFalse(DrinkLab.GrantPracticeKit(out var got2, out var err2), "두 번째 지급 거부");
            Assert.AreEqual(0, got2.Count);
            Assert.AreEqual(total, DrinkIngredients.All.Sum(d => DrinkLab.Owned(d.id)), "재고 변화 없음");

            // 재로드 후에도 1회(저장 필드)
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            Assert.IsTrue(DrinkLab.PracticeGranted, "저장 후 다시 읽어도 지급 완료");
            Assert.IsFalse(DrinkLab.GrantPracticeKit(out _, out _));
            StringAssert.Contains($"\"schemaVersion\": {DrinkStore.CurrentVersion}", File.ReadAllText(Path.Combine(dir, "drinks.json")));
            Assert.AreEqual(DrinkStore.PracticeVersion, DrinkStore.CurrentVersion);

            // 실습 잔(지급분 포함) → 판매 불가, 코인 0, 마시기용 준비 음료는 유지
            if (got.Count > 0)
            {
                var res = DrinkLab.Craft(r.Clone());
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, res.Status, res.Message);
                Assert.IsTrue(DrinkLab.PreparedSaleBlocked, "실습 재료가 섞인 잔은 판매 불가");
                int coins = DrinkLab.Coins;
                for (int i = 0; i < DrinkLab.Orders.Count; i++) Assert.IsFalse(DrinkLab.Sell(i, out int c, out _), "판매 거부 " + i);
                Assert.AreEqual(coins, DrinkLab.Coins);
                Assert.IsNotNull(DrinkLab.Prepared, "잔은 그대로(마실 수 있음)");
                // 취소·회수 → 출처 유지 환불(다시 판매 가능 재고로 바뀌지 않음)
                Assert.IsTrue(DrinkLab.CancelPreparedAndRefund(out var e3), e3);
                foreach (var g in got) Assert.AreEqual(g.amount, DrinkLab.EndlessOwned(g.id), g.id + ": 회수해도 판매 불가 출처 유지");
            }
        }

        [Test]
        public void PracticeKit_SaveFailure_GrantsNothing_FlagStaysFalse()
        {
            int total = DrinkIngredients.All.Sum(d => DrinkLab.Owned(d.id));
            DrinkStore.FaultHookForTests = stage => { if (stage == DrinkStore.SaveStage.WriteTmp) throw new IOException("qa disk full"); };
            Assert.IsFalse(DrinkLab.GrantPracticeKit(out var got, out var err));
            DrinkStore.FaultHookForTests = null;
            Assert.IsFalse(DrinkLab.PracticeGranted, "저장 실패 = 플래그 없음");
            Assert.AreEqual(total, DrinkIngredients.All.Sum(d => DrinkLab.Owned(d.id)), "저장 실패 = 지급 없음");
            Assert.IsTrue(DrinkLab.GrantPracticeKit(out _, out var err2), "다음 시도는 정상 1회 " + err2);
        }

        // ── 7 노트북 안내 ──
        [Test]
        public void NotebookGuide_OnlyAfterFirstEndedRun_NotForMasteryUsers_CompletePersists()
        {
            NotebookGuide.SuppressForTests = false;
            Assert.IsFalse(NotebookGuide.Needed, "첫 판 전 없음");
            PermanentProgress.Current.totalRuns = 1; // 출발만 한 판(정산 전)은 아직 아님
            Assert.IsFalse(NotebookGuide.Needed, "totalRuns는 출발 때 늘어 기준이 아님");
            PermanentProgress.Current.abandonedRuns = 1;
            Assert.IsTrue(NotebookGuide.Needed, "끝난 판 1 → 안내");
            DrinkLab.Research.unlockedTraits.Add("service_1");
            Assert.IsTrue(NotebookGuide.Needed, "숙련을 이미 해금한 기존 저장도 안내가 영구 누락되지 않는다");
            DrinkLab.Research.unlockedTraits.Clear();
            Assert.IsTrue(NotebookGuide.FirstRunEnded); Assert.IsFalse(NotebookGuide.GuideDone, "두 상태 독립");
            NotebookGuide.Complete();
            Assert.IsFalse(NotebookGuide.Needed);
            Assert.IsTrue(NotebookGuide.FirstRunEnded, "안내 완료가 첫 판 종료 상태를 바꾸지 않음");
            SettingsService.SetForTests(settingsPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            Assert.AreEqual(NotebookGuide.Version, SettingsService.Current.notebookGuideVersion, "다시 읽어도 완료");
            Assert.IsFalse(NotebookGuide.Needed);
            SettingsService.ResetNotebookGuide();
            Assert.IsTrue(NotebookGuide.Needed, "다시 보기");
            StringAssert.Contains("연구 포인트", NotebookGuide.StepText(3));
        }

        [Test]
        public void OldSettingsFile_WithoutNewGuideFields_ReadsDefaults_KeepsOtherValues()
        {
            File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"tutorialSeen\":true,\"masterVolume\":37,\"cardEducationGuideVersion\":1}");
            SettingsService.SetForTests(settingsPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            Assert.AreEqual(0, SettingsService.Current.notebookGuideVersion);
            Assert.AreEqual(0, SettingsService.Current.craftGuideVersion);
            Assert.AreEqual(0, SettingsService.Current.kindGuideSeen);
            Assert.AreEqual(37, SettingsService.Current.masterVolume);
            SettingsService.MarkKindGuideSeen((int)DrinkKind.Tea);
            SettingsService.CompleteCraftGuide(1);
            SettingsService.SetForTests(settingsPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            Assert.IsTrue(SettingsService.KindGuideSeen((int)DrinkKind.Tea));
            Assert.IsFalse(SettingsService.KindGuideSeen((int)DrinkKind.Coffee));
            Assert.AreEqual(1, SettingsService.Current.craftGuideVersion);
        }

        // ── 23 결과 → 획득 내역 → 다음 행동 ──
        [Test]
        public void AwardContent_EmptySettlementSkipsDetail_NonEmptyLeadsToIt()
        {
            Assert.IsFalse(ResultView.HasAwardContent(null));
            Assert.IsFalse(ResultView.HasAwardContent(new DrinkSettlement { kind = "Settled", drink = "None" }), "받은 것 없음 = 빈 창 경유 안 함");
            Assert.IsTrue(ResultView.HasAwardContent(new DrinkSettlement { kind = "Settled", granted = new List<DrinkAmount> { new DrinkAmount("gin", 10) } }));
            Assert.IsTrue(ResultView.HasAwardContent(new DrinkSettlement { kind = "Settled", researchPoints = 2 }));
            Assert.IsTrue(ResultView.HasAwardContent(new DrinkSettlement { kind = "PreservedAbandon", discarded = new List<DrinkAmount> { new DrinkAmount("gin", 10) } }));
            Assert.IsTrue(ResultView.HasAwardContent(new DrinkSettlement { kind = "Settled", drink = "Consumed" }));
        }

        [Test]
        public void AwardDetail_NextActions_RunOnce_ClosesWindow_ShowsStock()
        {
            var view = DrinkAwardDetailView.Create(Canvas(), null);
            int restarts = 0, titles = 0;
            view.SetNextActions(() => { if (view.IsOpen) { view.Close(); restarts++; } }, () => { if (view.IsOpen) { view.Close(); titles++; } });
            Assert.IsTrue(view.RestartButton.gameObject.activeSelf && view.TitleButton.gameObject.activeSelf);
            DrinkLab.Data.Add("gin", 120);
            var s = new DrinkSettlement { kind = "Settled", granted = new List<DrinkAmount> { new DrinkAmount("gin", 100) }, researchPoints = 1 };
            view.Show(s);
            StringAssert.Contains("+10mL", view.AllText);
            StringAssert.Contains($"창고 {DrinkRules.Format(DrinkLab.Owned("gin"))}", view.AllText, "재고 반영 표시");
            view.RestartButton.onClick.Invoke();
            view.RestartButton.onClick.Invoke(); // 연속 입력
            view.InvokeRestart();
            Assert.AreEqual(1, restarts, "다시 시작 1회만");
            Assert.IsFalse(view.IsOpen);
            view.Show(s);
            view.TitleButton.onClick.Invoke(); view.TitleButton.onClick.Invoke();
            Assert.AreEqual(1, titles);
            foreach (var n in new[] { "Close", "Restart", "TitleLobby" })
                foreach (var m in new[] { "Close", "Restart", "TitleLobby" })
                {
                    if (n == m) continue;
                    var a = (RectTransform)view.transform.Find("Panel/" + n); var b = (RectTransform)view.transform.Find("Panel/" + m);
                    var ca = new Vector3[4]; var cb = new Vector3[4]; a.GetWorldCorners(ca); b.GetWorldCorners(cb);
                    Assert.IsFalse(Rect.MinMaxRect(ca[0].x, ca[0].y, ca[2].x, ca[2].y).Overlaps(Rect.MinMaxRect(cb[0].x, cb[0].y, cb[2].x, cb[2].y)), n + "/" + m);
                }
        }

        // ── 24 목표 음료 ──
        [Test]
        public void Goal_StoredAsUserCopy_ExampleUntouched_ProgressUsesStockPlusRunPending()
        {
            var example = DrinkLab.PracticeRecipe;
            string exampleName = example.name; int exampleGin = example.AmountOf(DrinkIngredients.Gin);
            Assert.IsNull(DrinkLab.GoalRecipe, "새 저장 = 목표 없음");
            Assert.IsTrue(DrinkLab.SetGoal(example, out var e), e);
            Assert.IsTrue(DrinkLab.IsGoal(example));
            Assert.AreNotSame(example, DrinkLab.GoalRecipe, "사본 저장");
            DrinkLab.GoalRecipe.name = "바뀐 이름";
            Assert.AreEqual(exampleName, DrinkLab.PracticeRecipe.name, "전역 예시 정의는 그대로");
            Assert.AreEqual(exampleGin, DrinkLab.PracticeRecipe.AmountOf(DrinkIngredients.Gin));
            Assert.IsTrue(DrinkLab.SetGoal(example, out _));

            foreach (var st in example.steps) { int o = DrinkLab.Data.Owned(st.id); if (o > 0) DrinkLab.Data.Add(st.id, -o); }
            DrinkLab.Data.Add(DrinkIngredients.Gin, 100);
            var p0 = DrinkGoalHud.Progress(DrinkLab.GoalRecipe, "runA");
            var gin0 = p0.First(x => x.id == DrinkIngredients.Gin);
            Assert.AreEqual(100, gin0.stock); Assert.AreEqual(0, gin0.run); Assert.AreEqual(300, gin0.need);
            Assert.IsFalse(DrinkGoalHud.IsReady(p0));

            int events = 0;
            System.Action<string, IList<DrinkAmount>> h = (id, items) => events++;
            DrinkLab.AwardRecorded += h;
            try
            {
                Assert.IsTrue(DrinkLab.RecordBossAward("runA", "b1", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 200), new DrinkAmount(DrinkIngredients.Campari, 300), new DrinkAmount(DrinkIngredients.SweetVermouth, 300) }));
                Assert.IsFalse(DrinkLab.RecordBossAward("runA", "b1", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 200) }), "같은 보상 중복 기록 없음");
            }
            finally { DrinkLab.AwardRecorded -= h; }
            Assert.AreEqual(1, events, "HUD 알림은 실제 기록 1회와 같은 횟수");
            var p1 = DrinkGoalHud.Progress(DrinkLab.GoalRecipe, "runA");
            var gin1 = p1.First(x => x.id == DrinkIngredients.Gin);
            Assert.AreEqual(100, gin1.stock, "창고 = 지금 쓸 수 있는 재고(정산 전 몫 제외)");
            Assert.AreEqual(200, gin1.run, "이번 판 미정산 몫은 따로");
            Assert.IsTrue(DrinkGoalHud.IsReady(p1), "창고 + 이번 판 ≥ 필요");
            Assert.AreEqual(0, DrinkGoalHud.Progress(DrinkLab.GoalRecipe, "otherRun").First(x => x.id == DrinkIngredients.Gin).run, "다른 판 몫은 섞지 않음");

            // 저장·재로드 후 목표 유지, 해제
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            Assert.IsNotNull(DrinkLab.GoalRecipe);
            Assert.IsTrue(DrinkLab.ClearGoal(out _));
            Assert.IsNull(DrinkLab.GoalRecipe);
        }

        [Test]
        public void GoalHud_HiddenWithoutGoal_ShowsChipsWithGoal()
        {
            var canvas = (RectTransform)Canvas();
            var hud = DrinkGoalHud.Create(canvas, null, new Vector2(30f, -548f));
            Assert.IsFalse(hud.Visible, "목표 없으면 숨김");
            Assert.IsTrue(DrinkLab.SetGoal(DrinkLab.PracticeRecipe, out _));
            var hud2 = DrinkGoalHud.Create(canvas, null, new Vector2(30f, -548f));
            Assert.IsTrue(hud2.Visible);
            Assert.AreEqual(3, hud2.ChipCount);
            StringAssert.Contains("/30", hud2.ChipText(0));
            StringAssert.Contains(DrinkLab.PracticeRecipe.name, hud2.TitleText);
        }

        /// <summary>2026-11-16 회귀(feedback2 투어 Player.log NRE): 타이틀 전환으로 파괴된 획득 내역 창에 늦은 연속 입력이 와도 예외 없음.</summary>
        [Test]
        public void AwardDetail_DestroyedThenLateInput_NoException()
        {
            var view = DrinkAwardDetailView.Create(Canvas(), null);
            int titles = 0;
            view.SetNextActions(() => { }, () => { if (view.IsOpen) titles++; });
            var button = view.TitleButton;
            Object.DestroyImmediate(view.gameObject);
            Assert.IsFalse(view.IsOpen, "파괴 = 닫힘");
            Assert.DoesNotThrow(() => button.onClick.Invoke());
            Assert.AreEqual(0, titles, "파괴 뒤 입력은 효과 없음");
        }

        /// <summary>2026-11-16 회귀: 실제 런은 Canvas가 GameManager(일반 Transform) 아래라 root.root가 RectTransform이 아니다.
        /// 11-15 빌드에서 OnAward의 (RectTransform)root.root 형변환이 InvalidCastException → 칸 갱신·비행이 전부 빠졌다(feedback2 Player.log).</summary>
        [Test]
        public void GoalHud_UnderPlainTransformRoot_AwardUpdatesChipsAndFlies()
        {
            var owner = new GameObject("GameManagerLike"); // RectTransform 없음
            created.Add(owner);
            var canvas = UiFactory.CreateCanvas(owner.transform, "UI", 0);
            Assert.IsTrue(DrinkLab.SetGoal(DrinkLab.PracticeRecipe, out _));
            foreach (var st in DrinkLab.PracticeRecipe.steps) { int o = DrinkLab.Data.Owned(st.id); if (o > 0) DrinkLab.Data.Add(st.id, -o); }
            var hud = DrinkGoalHud.Create((RectTransform)canvas.transform, null, new Vector2(30f, -548f), "runHud");
            Assert.IsTrue(hud.Visible);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(DrinkLab.RecordBossAward("runHud", "h1", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 200) }));
            int gi = -1;
            for (int i = 0; i < hud.ChipCount; i++) if (hud.ChipText(i).Contains("판+")) gi = i;
            Assert.GreaterOrEqual(gi, 0, "이번 판 몫(+20)이 칸에 반영: " + hud.ChipText(0));
            StringAssert.StartsWith("20/30", hud.ChipText(gi), "가진 양(창고 0 + 이번 판 20)/필요");
            StringAssert.Contains("mL · 판+20", hud.ChipText(gi));
            if (SettingsService.Current.effectsLevel != SettingsData.EffectsLow) Assert.AreEqual(1, hud.FlightsInAir, "재료 그림 비행 1개");
            Assert.IsTrue(DrinkLab.RecordBossAward("otherRun", "h2", "qa", new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 999) }));
            StringAssert.StartsWith("20/30", hud.ChipText(gi), "다른 판 보상은 섞지 않음");
            // 720p 판독: 칸 3개면 칸 폭이 4등분(약 96)보다 넓고, 값 줄과 단위 줄이 겹치지 않는다
            var chip = (RectTransform)hud.transform.Find("Chip_" + DrinkIngredients.Gin);
            Assert.Greater(chip.sizeDelta.x, 120f, "칸 수에 맞춘 폭");
            var v = (RectTransform)chip.Find("Value"); var sb = (RectTransform)chip.Find("Sub");
            Assert.LessOrEqual(v.anchoredPosition.y - v.sizeDelta.y, sb.anchoredPosition.y + 0.01f, "값/단위 줄 비겹침");
            Assert.GreaterOrEqual(v.GetComponent<Text>().resizeTextMinSize, 16, "값 최소 글자(720p ≈ 11px)");
        }
    }
}
