using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>커피 제조 화면: 종류 버튼 → 추출 도구 선택 → 원두 스쿱 계량 → 도구별 추출 모션(손 없음) → 완성 잔·저장된 도구.</summary>
    public class CoffeeLabPlayTests
    {
        string dir, path;

        static void Clear()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkLabView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.transform.root.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Clear();
            dir = Path.Combine(Application.temporaryCachePath, "coffeeplay_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "drinks.json");
            DrinkLab.SetPathForTests(path);
            DrinkLab.Data.Add(DrinkIngredients.CoffeeBean, 1000); // 100.0g (원두 지급·드롭 연결은 코디네이터 통합 범위)
            DrinkLab.Data.Add(DrinkIngredients.SteamedMilk, 3000);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Clear();
            yield return null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        IEnumerator OpenCoffee(Action<DrinkLabView> got)
        {
            var select = CharacterSelectView.Show((j, w) => { });
            yield return null;
            select.DrinkLabButton.onClick.Invoke();
            yield return null;
            var lab = select.DrinkLabScreen;
            Assert.IsNotNull(lab);
            var btn = GameObject.Find("KindCoffee");
            Assert.IsNotNull(btn, "커피 종류 버튼");
            btn.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(DrinkKind.Coffee, lab.Kind);
            got(lab);
        }

        static IEnumerator Until(Func<bool> cond, float timeout = 15f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 초과");
        }

        [UnityTest]
        public IEnumerator CoffeeKind_ListsOnlyBeans_MethodButtonsSwapCenteredApparatus()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            Assert.AreEqual(30, lab.ListRowCount, "커피 재료: 기본 재료와 산지별 원두 30종");
            Assert.AreEqual(DrinkTechnique.Brew, lab.Draft.Technique);
            foreach (var m in CoffeeBrewing.All)
            {
                var b = GameObject.Find("Method" + m);
                Assert.IsNotNull(b, "추출 도구 버튼 " + m);
                b.GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.AreEqual(m, lab.Draft.coffeeMethod);
                Assert.AreEqual(CoffeeBrewing.DefaultWaterTenths(m), lab.Draft.water);
                Assert.AreEqual(CoffeeBrewing.ApparatusArt(m), lab.Stage.MainVessel.AssetId);
                Assert.IsFalse(lab.Stage.MainVessel.Placeholder, "실제 아트 " + m);
                Assert.AreEqual(m == CoffeeBrewMethod.PourOver, lab.Stage.DripperRect != null, "드리퍼는 핸드 드립에만");
                Assert.AreEqual(m == CoffeeBrewMethod.PourOver, lab.Stage.KettleRect.gameObject.activeSelf, "주전자는 핸드 드립에만");
                Assert.AreEqual(m == CoffeeBrewMethod.MokaPot, lab.Stage.FlameRect != null, "버너 불꽃은 모카포트에만");
                StringAssert.Contains(CoffeeBrewing.DisplayName(m), lab.NoticeText);
            }
            // 칵테일로 돌아가면 기존 그대로
            GameObject.Find("KindCocktail").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(DrinkKind.Cocktail, lab.Kind);
            Assert.AreEqual(0, (int)lab.Draft.coffeeMethod);
            Assert.IsNull(lab.Stage.DripperRect);
        }

        [UnityTest]
        public IEnumerator BeanScoop_PoursBeansIntoApparatus_ExactDraftAmount()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            int initialBeans = DrinkLab.Owned(DrinkIngredients.CoffeeBean);
            lab.SetCoffeeMethod(CoffeeBrewMethod.PourOver);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get(DrinkIngredients.CoffeeBean));
            Assert.AreEqual(CoffeeBrewing.ScoopArt, stage.LeafToolArt, "원두는 다하가 아니라 원두 스쿱");
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            // 20.0g 스쿱보다 핸드 드립 최대 원두량(18.0g)이 작으면 스쿱이 차기 전에 최대량에서 멈춘다.
            yield return Until(() => stage.MeasureDumps >= 1 || stage.InMeasure >= DrinkRules.CoffeeScoopCapacity || !stage.Pouring, 8f);
            stage.StopPour();
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
            int bean = lab.Draft.AmountOf(DrinkIngredients.CoffeeBean);
            Assert.Greater(bean, 0);
            Assert.LessOrEqual(bean, CoffeeBrewing.DoseMaxTenths(CoffeeBrewMethod.PourOver));
            Assert.AreEqual(bean / 10.0, stage.Ledger[DrinkStageView.LedgerVessel].LeafG, 0.01, "장부의 도구 안 원두 = 배합");
            Assert.AreEqual(initialBeans, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "제조 확정 전에는 재고 차감 없음");
        }

        [UnityTest]
        public IEnumerator BeanScoop_OneScoopHolds20g_TinStaysClearOfScoop()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine); // 최대 24.0g이라 한 스쿱(20.0g)이 그대로 찬다
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get(DrinkIngredients.CoffeeBean));
            Assert.Greater(stage.CoffeeTinScoopGap, -1e6f);
            stage.BeginPour(Vector2.zero);
            float minGap = float.MaxValue;
            for (float a = 0f; a < DrinkStageView.TiltMax; a += 3f)
            {
                stage.SetTilt(a);
                if (a >= DrinkStageView.TiltStart) minGap = Mathf.Min(minGap, stage.CoffeeTinScoopGap);
                yield return null;
            }
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() =>
            {
                if (stage.Pouring) minGap = Mathf.Min(minGap, stage.CoffeeTinScoopGap);
                return stage.MeasureDumps >= 1;
            }, 10f);
            Assert.AreEqual(200, DrinkRules.CoffeeScoopCapacity);
            Assert.AreEqual(1, stage.MeasureDumps, "가득 찬 스쿱 한 번");
            Assert.GreaterOrEqual(minGap, DrinkStageView.CoffeeTinMinGap - 0.5f, "기울여 덜 때 원두 통과 스쿱 사이 빈틈");
            stage.StopPour();
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
            Assert.AreEqual(200, lab.Draft.AmountOf(DrinkIngredients.CoffeeBean), "한 스쿱 = 배합 20.0g");
        }

        [UnityTest]
        public IEnumerator BeanScoop_FlipsTowardEveryCoffeeMethodOnlyWhileTransferring(
            [Values(CoffeeBrewMethod.EspressoMachine, CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot)] CoffeeBrewMethod method)
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(method);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get(DrinkIngredients.CoffeeBean));
            Assert.IsFalse(stage.ScoopFlipped, "제자리 스쿱은 원래 방향");
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            bool flippedWhileScooping = false;
            yield return Until(() =>
            {
                if (!stage.Transferring) flippedWhileScooping |= stage.ScoopFlipped;
                return stage.InMeasure >= 60 || stage.MeasureDumps >= 1;
            }, 10f);
            Assert.IsFalse(flippedWhileScooping, "통에서 덜 때는 원래 방향");
            stage.StopPour();
            yield return Until(() => stage.Transferring && stage.ToolTilt >= 60f, 10f);
            Assert.IsTrue(stage.ScoopFlipped, "세 커피 추출법 모두 받는 도구 쪽으로 스쿱을 뒤집는다");
            if (stage.ScoopFlipped)
            {
                // 그릇(오목한 쪽)이 손잡이 끝보다 머신 입구에 가깝고, 원두는 그릇 테두리에서 떨어진다.
                var g = stage.TransferGeometry;
                var bowl = DrinkVesselView.ToParentAnchored(stage.ChahaRect, new Vector2(0.27f, 0.4f));
                var handle = DrinkVesselView.ToParentAnchored(stage.ChahaRect, new Vector2(0.95f, 0.8f));
                Assert.Less(Mathf.Abs(bowl.x - g.mouth.x), Mathf.Abs(handle.x - g.mouth.x), "그릇이 머신 쪽");
                Assert.Less(Vector2.Distance(g.lip, bowl), Vector2.Distance(g.lip, handle), "쏟는 입구 = 그릇 테두리");
            }
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
            Assert.IsFalse(stage.ScoopFlipped, "옮긴 뒤 원래 방향으로 복귀");
            Assert.Greater(lab.Draft.AmountOf(DrinkIngredients.CoffeeBean), 0);
            Assert.AreEqual(lab.Draft.AmountOf(DrinkIngredients.CoffeeBean) / 10.0, stage.Ledger[DrinkStageView.LedgerVessel].LeafG, 0.01, "장부 = 배합");
        }

        [UnityTest]
        public IEnumerator CoffeeScreen_TopAndBottomControlsStayInsideFrame()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (lab.Narrow) { lab.SetReceiptOpen(true); yield return null; Canvas.ForceUpdateCanvases(); }
            var canvasRt = (RectTransform)lab.GetComponentInChildren<Canvas>().rootCanvas.transform;
            var c = canvasRt.rect;
            // 카페 테두리 = 가장자리 24 + 두께 약 7(안쪽 끝 31). 글자·버튼은 그보다 더 안쪽(FrameSafeInset)에서 끝나야 테두리에 닿아 보이지 않는다.
            Assert.GreaterOrEqual(DrinkLabView.FrameSafeInset, 31f + 8f, "테두리 안쪽 끝과의 빈틈");
            var safe = Rect.MinMaxRect(c.xMin + DrinkLabView.FrameSafeInset - 0.5f, c.yMin + DrinkLabView.FrameSafeInset - 0.5f,
                                       c.xMax - DrinkLabView.FrameSafeInset + 0.5f, c.yMax - DrinkLabView.FrameSafeInset + 0.5f);
            var w = new Vector3[4];
            var all = lab.GetComponentsInChildren<RectTransform>();
            Rect Bounds(RectTransform rt)
            {
                rt.GetWorldCorners(w);
                Vector2 a = canvasRt.InverseTransformPoint(w[0]), b = canvasRt.InverseTransformPoint(w[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            var names = new System.Collections.Generic.List<string> { "Title", "TitlePlate", "Close", "Notice", "KindCocktail", "KindTea", "KindCoffee", "IngredientList", "Stage", "AmountControls", "MixPanel" };
            foreach (DrinkLabView.Tab t in Enum.GetValues(typeof(DrinkLabView.Tab))) names.Add("Tab" + t);
            if (!lab.Narrow) names.Add("Mvp"); else names.Add("ReceiptToggle");
            foreach (var name in names)
            {
                var rt = System.Array.Find(all, r => r.name == name);
                Assert.IsNotNull(rt, name);
                var r = Bounds(rt);
                Assert.IsTrue(safe.Contains(r.min) && safe.Contains(r.max), $"{name} {r} 가 테두리 안전선 {safe} 안쪽 (narrow={lab.Narrow})");
            }
            // 아래 입력 줄의 모든 버튼·글자도 테두리 안쪽, 글자는 한 줄이 칸 높이에 들어간다(잘려 안 보이지 않음).
            var ctl = System.Array.Find(all, r => r.name == "AmountControls");
            foreach (var g in ctl.GetComponentsInChildren<Graphic>())
            {
                var r = Bounds(g.rectTransform);
                Assert.IsTrue(safe.Contains(r.min) && safe.Contains(r.max), $"{g.name} {r} 테두리 안쪽");
                if (g is Text tx && !string.IsNullOrEmpty(tx.text))
                    Assert.LessOrEqual(tx.preferredHeight, tx.rectTransform.rect.height + 0.5f, $"{g.name} 글자 높이가 칸 안: {tx.text}");
            }
            foreach (var name in new[] { "Title", "Notice" })
            {
                var tx = System.Array.Find(all, r => r.name == name).GetComponent<Text>();
                Assert.LessOrEqual(tx.fontSize * 1.1f, tx.rectTransform.rect.height + 0.5f, name + " 한 줄 높이");
            }
            // 가운데 제조대는 왼쪽 목록·오른쪽 영수증과 겹치지 않는다(넓은 창).
            if (!lab.Narrow)
            {
                Rect B(string n) => Bounds(System.Array.Find(all, r => r.name == n));
                Assert.LessOrEqual(B("IngredientList").xMax, B("Stage").xMin + 0.5f, "목록 ↔ 제조대");
                Assert.LessOrEqual(B("Stage").xMax, B("MixPanel").xMin + 0.5f, "제조대 ↔ 영수증");
                Assert.GreaterOrEqual(B("Stage").yMin, B("AmountControls").yMax - 0.5f, "제조대 ↔ 입력 줄");
            }
        }

        [UnityTest]
        public IEnumerator MethodButtons_LabelsFit_SelectedCheckDoesNotCoverLabel()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            foreach (var sel in CoffeeBrewing.All)
            {
                lab.SetCoffeeMethod(sel);
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var m in CoffeeBrewing.All)
                {
                    var b = (RectTransform)GameObject.Find("Method" + m).transform;
                    var t = b.GetComponentInChildren<Text>();
                    Assert.AreEqual(CoffeeBrewing.ButtonName(m), t.text);
                    Assert.LessOrEqual(t.preferredWidth, t.rectTransform.rect.width + 0.5f, "한 줄에 들어간다: " + m);
                    // 체크 표식 = 오른쪽 테두리 안쪽 16~46 단위. 글자 칸은 그 왼쪽에서 끝난다.
                    float labelRight = t.rectTransform.offsetMax.x;
                    Assert.LessOrEqual(labelRight, -46f, "선택 체크와 글자 칸이 겹치지 않는다: " + m);
                }
                // 버튼끼리 겹치지 않는다.
                var rects = new System.Collections.Generic.List<Rect>();
                foreach (var m in CoffeeBrewing.All)
                {
                    var rt = (RectTransform)GameObject.Find("Method" + m).transform;
                    var r = new Rect(rt.anchoredPosition.x, rt.anchoredPosition.y - rt.sizeDelta.y, rt.sizeDelta.x, rt.sizeDelta.y);
                    foreach (var o in rects) Assert.IsFalse(o.Overlaps(r), "제조법 버튼 겹침");
                    rects.Add(r);
                }
            }
        }

        [UnityTest]
        public IEnumerator Craft_EachMethod_PlaysApparatusFlow_FinalCupAndSavedMethod(
            [Values(CoffeeBrewMethod.EspressoMachine, CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot)] CoffeeBrewMethod m)
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            int initialBeans = DrinkLab.Owned(DrinkIngredients.CoffeeBean);
            lab.SetCoffeeMethod(m);
            int bean = m == CoffeeBrewMethod.EspressoMachine ? 180 : 150;
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, bean);
            lab.Draft.name = "커피 " + (int)m;
            lab.RefreshForTests();
            Assert.IsTrue(lab.CraftInteractable, lab.FooterStatusText);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            var stages = new System.Collections.Generic.HashSet<string>();
            bool sawCloseUp = false;
            bool sawMokaGrinder = false, sawMokaGrounds = false;
            yield return Until(() =>
            {
                if (lab.Stage.CoffeeStage != null) stages.Add(lab.Stage.CoffeeStage);
                sawCloseUp |= lab.Stage.CloseUpActive;
                if (m == CoffeeBrewMethod.MokaPot)
                {
                    sawMokaGrinder |= lab.Stage.CoffeeStage == "grind" && lab.Stage.MokaGrinderVisible;
                    sawMokaGrounds |= lab.Stage.CoffeeStage == "dose" && lab.Stage.MokaGroundsTransferVisible;
                }
                return !lab.Stage.SequencePlaying;
            }, 20f);
            Assert.AreEqual(m == CoffeeBrewMethod.EspressoMachine, sawCloseUp, "추출 클로즈업은 에스프레소 머신에만");
            Assert.IsFalse(lab.Stage.CloseUpActive, "완성 화면에는 클로즈업 없음");
            string expect = m switch { CoffeeBrewMethod.EspressoMachine => "extract", CoffeeBrewMethod.PourOver => "dripperAside", _ => "rise" };
            Assert.IsTrue(stages.Contains(expect), $"{m} 단계: {string.Join(",", stages)}");
            if (m == CoffeeBrewMethod.MokaPot)
            {
                CollectionAssert.IsSubsetOf(new[] { "grind", "dose", "heat", "rise", "serve" }, stages,
                    "모카포트는 분쇄·바스켓 투입 뒤 가열과 추출을 진행");
                Assert.IsTrue(sawMokaGrinder, "분쇄 단계에서 그라인더가 화면에 나온다");
                Assert.IsTrue(sawMokaGrounds, "투입 단계에서 분쇄 원두가 모카포트 바스켓으로 이동한다");
            }
            if (m == CoffeeBrewMethod.EspressoMachine)
            {
                Assert.IsTrue(stages.Contains("prepare"));
                StringAssert.Contains("dl_coffee_espresso_portafilter", lab.Stage.MainVessel.Glass.sprite.name);
            }
            if (m == CoffeeBrewMethod.PourOver)
            {
                Assert.IsTrue(stages.Contains("bloom"));
                Assert.AreEqual(1f, lab.Stage.DripperAside01, 1e-4f);
                Assert.GreaterOrEqual(lab.Stage.KettleSweepMaxOffset, 27f, "주전자가 드리퍼 위를 넓게 왕복한다");
                Assert.IsTrue(lab.Stage.DripperGroundsVisible, "분쇄 원두가 드리퍼 안에 남아 있다");
                Assert.That(lab.Stage.DripperGroundsAnchorY, Is.InRange(0.1f, 0.4f), "원두가 드리퍼 입구가 아니라 아랫부분에 놓인다");
            }
            var final = lab.Stage.FinalVessel;
            Assert.AreEqual(CoffeeBrewing.CupArt(m), final.AssetId);
            Assert.IsFalse(final.Placeholder);
            Assert.AreEqual(lab.Draft.water / 10f, final.VolumeMl, 0.05f, "추출량 전부 잔에");
            Assert.AreEqual(initialBeans - bean, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "원두는 제조 확정에서만 차감");
            StringAssert.StartsWith("완성", lab.Stage.FinishText);
            // 저장된 준비 음료에 추출 도구가 남는다(다시 읽어도).
            DrinkLab.SetPathForTests(path);
            Assert.IsNotNull(DrinkLab.Prepared);
            Assert.AreEqual(m, DrinkLab.Prepared.recipe.coffeeMethod);
            Assert.AreEqual(DrinkKind.Coffee, DrinkLab.Prepared.recipe.Kind);
        }

        [UnityTest]
        public IEnumerator SteamedMilk_PoursIntoCupFirst_EspressoLandsOnTop()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.MokaPot);
            var stage = lab.Stage;
            stage.Select(DrinkIngredients.Get(DrinkIngredients.SteamedMilk));
            stage.BeginPour(Vector2.zero);
            stage.SetTilt(DrinkStageView.TiltMax);
            yield return Until(() => stage.InMeasure >= 200 || stage.MeasureDumps > 0 || stage.StopReason != null, 8f);
            stage.StopPour();
            yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
            int milk = lab.Draft.AmountOf(DrinkIngredients.SteamedMilk);
            Assert.Greater(milk, 0);
            Assert.AreEqual(milk / 10.0, stage.Ledger[DrinkStageView.LedgerServe].LiquidMl, 0.05, "우유는 잔에");
            Assert.AreEqual(0.0, stage.Ledger[DrinkStageView.LedgerVessel].LiquidMl, 1e-6, "모카포트 안에는 액체 없음");
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, 150);
            lab.Draft.water = 1200;
            lab.RefreshForTests();
            Assert.IsTrue(lab.CraftInteractable, lab.FooterStatusText);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            yield return Until(() => !lab.Stage.SequencePlaying, 20f);
            Assert.AreEqual((1200 + milk) / 10f, lab.Stage.FinalVessel.VolumeMl, 0.1f, "우유 + 추출 커피");
        }

        [UnityTest]
        public IEnumerator Craft_SkipMotion_SameFinalCup()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.MokaPot);
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, 150);
            lab.RefreshForTests();
            lab.SkipMotion = true;
            lab.ConfirmCraft();
            yield return Until(() => !lab.Stage.SequencePlaying, 5f);
            Assert.AreEqual("dl_coffee_cup", lab.Stage.FinalVessel.AssetId);
            Assert.AreEqual(lab.Draft.water / 10f, lab.Stage.FinalVessel.VolumeMl, 0.05f);
        }

        int initialBeans, initialMilk;

        IEnumerator StartEspresso(Action<DrinkLabView> got, int milk)
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            initialBeans = DrinkLab.Owned(DrinkIngredients.CoffeeBean);
            initialMilk = DrinkLab.Owned(DrinkIngredients.SteamedMilk);
            lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine);
            lab.Draft.SetAmount(DrinkIngredients.CoffeeBean, 180);
            if (milk > 0) lab.Draft.SetAmount(DrinkIngredients.SteamedMilk, milk);
            lab.Draft.name = "에스프레소 클로즈업";
            lab.RefreshForTests();
            Assert.IsTrue(lab.CraftInteractable, lab.FooterStatusText);
            lab.SkipMotion = false;
            lab.ConfirmCraft();
            Assert.IsNotNull(DrinkLab.Prepared, lab.LastMessage);
            got(lab);
        }

        [UnityTest]
        public IEnumerator EspressoCloseUp_DropsThenStream_LevelAndColorFromLedger_ReturnsToScene()
        {
            DrinkLabView lab = null;
            const int milk = 150;
            yield return StartEspresso(v => lab = v, milk);
            var stage = lab.Stage;
            double yieldMl = lab.Draft.water / 10.0, milkMl = milk / 10.0, cap = CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.EspressoMachine);
            var order = new System.Collections.Generic.List<string>();
            float lastExtracted = 0f, maxConservationErr = 0f;
            int closeUpFrames = 0, dropFrames = 0;
            Color firstDrop = Color.clear, lateStream = Color.clear;
            var cam = new System.Collections.Generic.List<float>();
            yield return Until(() =>
            {
                if (stage.CloseUpActive)
                {
                    closeUpFrames++;
                    Assert.IsFalse(stage.FinishLabelVisible, "클로즈업 동안 단계 문구는 머신 앞판 위에 그리지 않는다");
                    string ph = stage.CloseUpPhase;
                    if (order.Count == 0 || order[order.Count - 1] != ph) order.Add(ph);
                    // 장부 보존: 추출 대기 + 떨어지는 중 + 잔에 닿은 추출분 = 목표 추출량(우유는 처음부터 잔에).
                    double serveMl = stage.Ledger[DrinkStageView.LedgerServe].LiquidMl;
                    double total = stage.Ledger[DrinkStageView.LedgerBrew].LiquidMl + stage.Ledger.InFlightMl(DrinkStageView.LedgerServe) + (serveMl - milkMl);
                    maxConservationErr = Mathf.Max(maxConservationErr, (float)Math.Abs(total - yieldMl));
                    Assert.GreaterOrEqual(serveMl, milkMl - 1e-6, "우유는 추출 전부터 잔에");
                    Assert.AreEqual(serveMl / cap, stage.CloseUpLevel01, 1e-4, "수면 = 잔 장부 실제 양");
                    Assert.GreaterOrEqual(stage.CloseUpExtracted01, lastExtracted - 1e-6f, "추출 진행은 줄지 않는다");
                    Assert.LessOrEqual(stage.CloseUpLanded01, stage.CloseUpExtracted01 + 1e-4f, "잔에 닿은 양 ≤ 나온 양");
                    lastExtracted = stage.CloseUpExtracted01;
                    if (ph == "drops" && stage.CloseUpDropsVisible > 0) { dropFrames++; if (firstDrop.a == 0f) firstDrop = stage.CloseUpFlowColor; }
                    if (ph == "stream" && stage.CloseUpExtracted01 > 0.9f && stage.CloseUpStreamVisible) { lateStream = stage.CloseUpStreamColor; Assert.AreEqual(stage.CloseUpFlowColor, lateStream, "줄기 = 지금 흐름 색"); }
                    Assert.AreEqual(stage.MainVessel.Rect.anchoredPosition.x, stage.CameraTarget, 1e-3f, "클로즈업 동안 장면 카메라는 머신에");
                }
                if (stage.CoffeeStage == null && !stage.CloseUpActive && order.Count > 0) cam.Add(stage.CameraTarget);
                return !stage.SequencePlaying;
            }, 20f);
            Assert.Greater(closeUpFrames, 10);
            Assert.AreEqual(DrinkStageView.CloseUpArtReady, stage.CloseUpUsesArt, "클로즈업 아트가 있으면 그 id 그대로 쓴다(없으면 도형 대체)");
            Assert.Greater(dropFrames, 0, "첫 방울이 보인다");
            int di = order.IndexOf("drops"), si = order.IndexOf("stream");
            Assert.IsTrue(di >= 0 && si > di, "첫 방울 → 이어진 줄기: " + string.Join(",", order));
            Assert.Less(maxConservationErr, 1e-3f, "추출 중 장부 합계 보존");
            Assert.AreEqual(1f, lastExtracted, 1e-4f, "목표 추출량 전부 나옴");
            Assert.Greater(lateStream.a, 0f, "끝물 줄기");
            Assert.Less(Lum(firstDrop), Lum(lateStream), "첫 방울은 끝물보다 어둡다");
            // 끝: 클로즈업이 걷히고 기존 장면의 완성 잔(받침 위 데미타스)으로, 카메라는 완성 잔.
            Assert.IsFalse(stage.CloseUpActive);
            Assert.IsNull(stage.CloseUpPhase);
            var final = stage.FinalVessel;
            Assert.AreEqual(CoffeeBrewing.CupArt(CoffeeBrewMethod.EspressoMachine), final.AssetId);
            Assert.IsTrue(final.gameObject.activeSelf && stage.MainVessel.gameObject.activeSelf);
            Assert.AreEqual(yieldMl + milkMl, final.VolumeMl, 0.1f, "우유 + 추출 커피");
            Assert.AreEqual(final.Rect.anchoredPosition.x, stage.CameraTarget, 1e-3f, "카메라 목표 = 완성 잔");
            StringAssert.StartsWith("완성", stage.FinishText);
            Assert.IsTrue(stage.FinishLabelVisible, "클로즈업이 닫히면 단계 문구 복원");
            Assert.AreEqual(initialBeans - 180, DrinkLab.Owned(DrinkIngredients.CoffeeBean));
            Assert.AreEqual(initialMilk - milk, DrinkLab.Owned(DrinkIngredients.SteamedMilk));
        }

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [UnityTest]
        public IEnumerator EspressoCloseUp_SkipMidStream_CleansUp_SameFinalCup()
        {
            DrinkLabView lab = null;
            yield return StartEspresso(v => lab = v, 0);
            var stage = lab.Stage;
            yield return Until(() => stage.CloseUpPhase == "stream", 10f);
            Assert.IsTrue(stage.CloseUpActive);
            Assert.IsTrue(stage.CloseUpUsesBackdropArt, "추출 클로즈업 뒤는 단색이 아니라 커피 제조 벽 아트");
            stage.Skip();
            yield return Until(() => !stage.SequencePlaying, 5f);
            Assert.IsFalse(stage.CloseUpActive, "건너뛰면 클로즈업 정리");
            Assert.IsTrue(stage.FinishLabelVisible, "건너뛰어도 단계 문구 복원");
            Assert.IsNull(stage.CloseUpPhase);
            Assert.IsFalse(stage.Ledger.HasInFlight());
            Assert.AreEqual(0.0, stage.Ledger[DrinkStageView.LedgerBrew].LiquidMl, 1e-9, "추출 대기분이 남지 않는다");
            Assert.AreEqual(lab.Draft.water / 10f, stage.FinalVessel.VolumeMl, 0.05f);
            Assert.AreEqual(stage.FinalVessel.Rect.anchoredPosition.x, stage.CameraTarget, 1e-3f);
            Assert.AreEqual(initialBeans - 180, DrinkLab.Owned(DrinkIngredients.CoffeeBean), "건너뛰어도 차감은 한 번");
            // 편집으로 돌아가도 클로즈업은 다시 나오지 않는다.
            lab.SetCoffeeMethod(CoffeeBrewMethod.PourOver);
            yield return null;
            Assert.IsFalse(stage.CloseUpActive);
        }
    }
}
