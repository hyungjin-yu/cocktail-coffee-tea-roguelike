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

        static Button LabButton(DrinkLabView lab, string name)
        {
            foreach (var b in lab.GetComponentsInChildren<Button>(true)) if (b.name == name) return b;
            Assert.Fail("버튼 없음: " + name);
            return null;
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

        /// <summary>2026-11-17 산지 원두 제조 통 28종(Codex ready, 실제 입구 앵커): 기존 원두 통 시험과 같은 방법으로 통마다
        /// 기울여 덜 때 통-스쿱 빈틈 ≥ 16px, 한 스쿱 20g 한 번, 배합 +20g(재고는 제조 전 그대로).</summary>
        [UnityTest]
        public IEnumerator OriginTins_All28_TinStaysClearOfScoop_OneScoop()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine);
            var stage = lab.Stage;
            int n = 0;
            foreach (var def in DrinkIngredients.All)
            {
                if (!def.IsCoffeeBean || def.id == DrinkIngredients.CoffeeBean) continue;
                n++;
                Assert.IsTrue(DrinkArt.Has(def.bottleId), def.bottleId + " 통 아트");
                Assert.AreNotEqual(new Vector2(0.5f, 1f), DrinkArt.Anchor(def.bottleId, "pourLip"), def.bottleId + " 실제 붓는 끝 앵커(기본값 아님)");
                DrinkLab.Data.Add(def.id, 400);
                LabButton(lab, "Clear").onClick.Invoke(); // 실제 "비우기"(제조 화면 안의 버튼)
                yield return null;
                int stock0 = DrinkLab.Owned(def.id);
                LabButton(lab, "Row_" + def.id).onClick.Invoke(); // 실제 재료 줄 선택
                yield return null;
                stage = lab.Stage;
                stage.BeginPour(Vector2.zero);
                float minGap = float.MaxValue;
                for (float a = 0f; a < DrinkStageView.TiltMax; a += 3f)
                {
                    stage.SetTilt(a);
                    if (a >= DrinkStageView.TiltStart) minGap = Mathf.Min(minGap, stage.CoffeeTinScoopGap);
                    yield return null;
                }
                stage.SetTilt(DrinkStageView.TiltMax);
                int dumps0 = stage.MeasureDumps;
                yield return Until(() =>
                {
                    if (stage.Pouring) minGap = Mathf.Min(minGap, stage.CoffeeTinScoopGap);
                    return stage.MeasureDumps >= dumps0 + 1;
                }, 10f);
                stage.StopPour();
                yield return Until(() => !stage.Pouring && !stage.Transferring, 10f);
                Assert.GreaterOrEqual(minGap, DrinkStageView.CoffeeTinMinGap - 0.5f, def.id + " 기울여 덜 때 통과 스쿱 사이 빈틈");
                Assert.AreEqual(200, lab.Draft.AmountOf(def.id), def.id + " 한 스쿱 20g");
                Assert.AreEqual(stock0, DrinkLab.Owned(def.id), def.id + " 제조 전 재고 차감 없음");
            }
            Assert.AreEqual(28, n, "산지 28종");
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
            // 안내 줄은 Truncate라 실제 글꼴 줄 높이가 칸보다 크면 줄 전체가 지워진다(2026-11-02 판매·제조 안내가 안 보이던 결함).
            var noticeText = System.Array.Find(all, r => r.name == "Notice").GetComponent<Text>();
            var noticeRect = noticeText.rectTransform.rect;
            float lineH = noticeText.cachedTextGeneratorForLayout.GetPreferredHeight("판매했습니다: +150코인 · 재료가 부족합니다",
                noticeText.GetGenerationSettings(new Vector2(noticeRect.width, noticeRect.height))) / noticeText.pixelsPerUnit;
            Assert.LessOrEqual(lineH, noticeRect.height + 0.5f, $"안내 줄 실제 글자 높이 {lineH:0.0}가 칸 {noticeRect.height:0.0} 안(narrow={lab.Narrow})");
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
        public IEnumerator OrdersTab_DisabledSellShowsReason_PreparedHeaderStaysTwoLines()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            Text SellLabel(int i) => GameObject.Find($"Order_{i}/Sell").GetComponentInChildren<Text>();
            Button Sell(int i) => GameObject.Find($"Order_{i}/Sell").GetComponent<Button>();
            lab.SetTab(DrinkLabView.Tab.Orders);
            yield return null;
            for (int i = 0; i < DrinkLab.Orders.Count; i++)
            {
                Assert.IsFalse(Sell(i).interactable);
                Assert.AreEqual("✗ 음료 없음", SellLabel(i).text, $"주문 {i + 1}: 음료가 없으면 이유를 버튼에");
            }
            // 첫 주문(칵테일: 진·레몬즙)을 그대로 만족하는 음료 → 그 주문만 판매 가능, 차 주문은 종류 다름
            var order = DrinkLab.Orders[0];
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(DrinkSales.Reference(order)).Status);
            lab.SetTab(DrinkLabView.Tab.Orders);
            yield return null;
            Assert.IsTrue(Sell(0).interactable);
            StringAssert.StartsWith("판매 (", SellLabel(0).text);
            Assert.IsFalse(Sell(1).interactable);
            Assert.AreEqual("✗ 종류 다름", SellLabel(1).text);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 2; i++)
                Assert.LessOrEqual(SellLabel(i).preferredHeight, SellLabel(i).rectTransform.rect.height + 0.5f, $"판매 버튼 글이 한 칸 안: {SellLabel(i).text}");
            // 제조 탭 영수증 위 준비 음료 안내: 취소·회수 버튼 옆 좁은 폭에서도 두 줄(아래 배합 줄을 덮지 않음)
            lab.SetTab(DrinkLabView.Tab.Craft);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var prepared = GameObject.Find("MixPanel/Prepared").GetComponent<Text>();
            Assert.AreEqual(2, prepared.text.Split('\n').Length, prepared.text);
            Assert.LessOrEqual(prepared.preferredHeight, prepared.rectTransform.rect.height + 0.5f, $"준비 음료 안내가 칸 안: {prepared.text}");
        }

        [UnityTest]
        public IEnumerator OtherTabs_PanelsShareCraftSafeInset_WideContentStaysInCenteredBand()
        {
            // 도감·주문·레시피 탭도 제조 탭과 같은 테두리 안전선(FrameSafeInset) 안쪽. 예전 30 여백은 카페 테두리 안쪽 끝(31)에 닿았다.
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            var canvasRt = (RectTransform)lab.GetComponentInChildren<Canvas>().rootCanvas.transform;
            var w = new Vector3[4];
            Rect Bounds(RectTransform rt)
            {
                rt.GetWorldCorners(w);
                Vector2 a = canvasRt.InverseTransformPoint(w[0]), b = canvasRt.InverseTransformPoint(w[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            var root = (RectTransform)lab.transform.Find("Root");
            var band = root.GetComponent<CenterBand>();
            Assert.IsNotNull(band, "카페 배경 띠");
            Canvas.ForceUpdateCanvases();
            if (!lab.Narrow)
            {
                // 16:9보다 넓은 창은 머리·본문을 가운데 1920 띠에(제조대 ↔ 영수증 사이가 벌어지지 않음). 16:9 이하에서는 폭 그대로.
                Assert.AreEqual(1920f, lab.ContentWidth, 0.01f);
                Assert.AreEqual(lab.ContentWidth, band.MaxWidth, 0.01f);
                Assert.LessOrEqual(root.rect.width, lab.ContentWidth + 0.5f, "내용 폭 ≤ 1920");
                Assert.AreEqual(canvasRt.rect.center.x, Bounds(root).center.x, 0.5f, "띠는 가로 가운데");
            }
            else Assert.AreEqual(0f, band.ExtraWidth, 0.01f, "좁은 창은 가로 띠 없음");
            var c = canvasRt.rect;
            float inset = DrinkLabView.FrameSafeInset;
            var safe = Rect.MinMaxRect(c.xMin + inset - 0.5f, c.yMin + inset - 0.5f, c.xMax - inset + 0.5f, c.yMax - inset + 0.5f);
            var checks = new (DrinkLabView.Tab tab, string[] names)[]
            {
                (DrinkLabView.Tab.Codex, new[] { "CodexList" }),
                (DrinkLabView.Tab.Orders, new[] { "OrdersList" }),
                (DrinkLabView.Tab.Recipes, new[] { "RecipeDrawer", "Import", "Count", "ImportToggle", "Example_0", "Example_1" }),
            };
            foreach (var (tab, names) in checks)
            {
                lab.SetTab(tab);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var all = lab.GetComponentsInChildren<RectTransform>(false);
                int found = 0;
                foreach (var name in names)
                {
                    var rt = System.Array.Find(all, r => r.name == name && r.parent != null && r.parent.name == "Body");
                    if (rt == null) continue; // 좁은/넓은 배치에 따라 없는 판(Count·ImportToggle·닫힌 가져오기)
                    found++;
                    var r = Bounds(rt);
                    Assert.IsTrue(safe.Contains(r.min) && safe.Contains(r.max), $"{tab}/{name} {r} 가 테두리 안전선 {safe} 안쪽 (narrow={lab.Narrow})");
                    if (name == "Import")
                        foreach (var g in rt.GetComponentsInChildren<Graphic>())
                        {
                            var gr = Bounds(g.rectTransform);
                            Assert.IsTrue(r.Contains(gr.min - new Vector2(-0.5f, -0.5f)) && r.Contains(gr.max - new Vector2(0.5f, 0.5f)), $"가져오기 판 안: {g.name} {gr} / {r}");
                        }
                }
                Assert.Greater(found, 0, tab + " 탭 판 검사");
            }
        }

        [UnityTest]
        public IEnumerator RecipesTab_SummaryFitsOneLine_ClearOfFirstExampleRow()
        {
            // 1920×1080 QA: 요약(Count 700×36)의 긴 문장이 두 줄(약 70)로 접혀 첫 줄 추천 카드에 겹쳤다.
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            if (lab.Narrow) Assert.Ignore("요약 줄은 넓은 배치에만 있다(좁은 창은 버튼 줄).");
            lab.SetTab(DrinkLabView.Tab.Recipes);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var canvasRt = (RectTransform)lab.GetComponentInChildren<Canvas>().rootCanvas.transform;
            var all = lab.GetComponentsInChildren<RectTransform>(false);
            var w = new Vector3[4];
            Rect B(string n)
            {
                var rt = System.Array.Find(all, r => r.name == n && r.parent != null && r.parent.name == "Body");
                Assert.IsNotNull(rt, n);
                rt.GetWorldCorners(w);
                Vector2 a = canvasRt.InverseTransformPoint(w[0]), b = canvasRt.InverseTransformPoint(w[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            var count = System.Array.Find(all, r => r.name == "Count" && r.parent.name == "Body").GetComponent<Text>();
            Assert.GreaterOrEqual(count.fontSize, DrinkPaperUi.Small, "작은 글보다 줄이지 않는다");
            Assert.AreEqual(HorizontalWrapMode.Overflow, count.horizontalOverflow, "줄바꿈해 아래로 내려오지 않는다");
            StringAssert.DoesNotContain("…", count.text, "1920 넓은 배치에서는 요약이 말줄임 없이 다 보인다");
            StringAssert.Contains($"{DrinkLab.Recipes.Count} / {DrinkRules.RecipeLimit}", count.text);
            StringAssert.Contains($"{DrinkLab.ExamplePresets.Count}개", count.text);
            var rect = count.rectTransform.rect;
            Assert.LessOrEqual(count.preferredWidth, rect.width + 0.5f, $"요약 폭이 칸 안: {count.text}");
            Assert.LessOrEqual(count.preferredHeight, rect.height + 0.5f, $"요약 한 줄 높이가 칸 안: {count.text}");
            var cr = B("Count");
            foreach (var card in new[] { "Example_0", "Example_1" })
                Assert.GreaterOrEqual(cr.yMin - B(card).yMax, 8f - 0.5f, $"요약 ↔ 첫 줄 {card} 사이 8 빈틈");
            // 추천 카드 마지막 줄도 가져오기 판과 겹치지 않는다.
            int last = DrinkLab.ExamplePresets.Count - 1;
            Assert.GreaterOrEqual(B("Example_" + last).yMin, B("Import").yMax - 0.5f, "마지막 추천 카드 ↔ 가져오기 판");
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
        public IEnumerator MethodCombatGuide_UpdatesWithSelection_FitsBelowMethodDescription()
        {
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            var expected = new System.Collections.Generic.Dictionary<CoffeeBrewMethod, string[]>
            {
                [CoffeeBrewMethod.EspressoMachine] = new[] { "1초 멈춰 서서", "움직이면 취소", "샷 3초", "공격 속도 +30%", "4초 재충전" },
                [CoffeeBrewMethod.PourOver] = new[] { "뜸 2초", "붓기 4초", "이동 속도 +15%", "사거리 +20%" },
                [CoffeeBrewMethod.MokaPot] = new[] { "직접 타격마다 증기압 +1", "최대 10", "1.5초마다 −1", "반경 260cm", "기본 피해 120%", "0.35초 경직" },
            };
            // 좁은 창에서는 추출 도구 버튼이 영수증 서랍 안에 있다 — 실제 조작처럼 서랍을 연 뒤 고른다(넓은 창에서는 영향 없음).
            lab.SetReceiptOpen(true);
            yield return null;
            foreach (var m in CoffeeBrewing.All)
            {
                lab.SetCoffeeMethod(m);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var row = GameObject.Find("CoffeeCombat");
                Assert.IsNotNull(row, "전투 운용 줄: " + m);
                var t = row.transform.Find("L").GetComponent<Text>();
                Assert.AreEqual(DrinkLabView.CoffeeCombatGuide(m), t.text, "고른 추출법의 안내로 갱신: " + m);
                StringAssert.Contains("전투 운용 · " + CoffeeBrewing.ButtonName(m), t.text, "제목에 고른 도구 이름");
                StringAssert.Contains("행동 · ", t.text);
                StringAssert.Contains("효과 · ", t.text);
                foreach (var part in expected[m]) StringAssert.Contains(part, t.text, $"{m} 수치");
                Assert.LessOrEqual(t.preferredHeight, t.rectTransform.rect.height + 0.5f, $"잘리지 않는다: {m}");
                // 추출 도구 설명 바로 아래, 배합 재료 줄보다 위(제조 확정 전 스크롤 맨 위 근처에서 보인다).
                var desc = (RectTransform)GameObject.Find("TechDesc").transform;
                var rt = (RectTransform)row.transform;
                Assert.LessOrEqual(rt.anchoredPosition.y, desc.anchoredPosition.y - desc.sizeDelta.y + 0.5f, "설명과 겹치지 않는다");
                Assert.AreEqual(rt.parent, desc.parent);
                Assert.LessOrEqual(rt.sizeDelta.x, ((RectTransform)rt.parent).rect.width + 0.5f, "스크롤 폭 안");
                // 도구를 바꾸면 그 도구의 안내가 영수증 스크롤 보이는 칸 안에 들어온다(위·아래 끝 모두).
                if (m != CoffeeBrewMethod.EspressoMachine)
                {
                    var view = (RectTransform)GameObject.Find("MixScroll").transform;
                    var corners = new Vector3[4]; var vc = new Vector3[4];
                    rt.GetWorldCorners(corners); view.GetWorldCorners(vc);
                    // 가림 여백(반쯤 걸린 줄을 가리는 칸)을 뺀 실제 보이는 칸 안에 안내 전체(제목 = 도구 이름 포함)가 있다.
                    var pad = view.GetComponent<ScrollRect>().viewport.GetComponent<RectMask2D>().padding;
                    float sc = view.lossyScale.y;
                    Assert.GreaterOrEqual(corners[0].y, vc[0].y + pad.y * sc - 0.5f, $"안내 아래 끝이 보인다: {m} (보이는 칸 {view.rect.height:0}, 줄 {rt.sizeDelta.y:0})");
                    Assert.LessOrEqual(corners[1].y, vc[1].y - pad.w * sc + 0.5f, $"안내 위 끝이 보인다: {m}");
                    AssertReceiptTopClean(view, m.ToString());
                    AssertReceiptRowsWhole(view, m.ToString());
                    // 고른 뒤 어디까지 스크롤해도 위·아래 끝에 반쯤 걸린 줄·버튼이 보이지 않는다.
                    var scroll = view.GetComponent<ScrollRect>();
                    float keep = scroll.verticalNormalizedPosition;
                    for (int k = 0; k <= 8; k++)
                    {
                        scroll.verticalNormalizedPosition = 1f - k / 8f;
                        yield return null; // ScrollRect는 LateUpdate에서 onValueChanged를 보낸다(가림 여백 갱신은 렌더 전)
                        Canvas.ForceUpdateCanvases();
                        AssertReceiptRowsWhole(view, $"{m} 스크롤 {k}/8");
                    }
                    scroll.verticalNormalizedPosition = keep;
                    yield return null;
                }
            }
            // 짧은 제조법 설명도 줄바꿈된 만큼 행이 늘어나 잘리지 않는다.
            var descText = GameObject.Find("TechDesc").transform.Find("L").GetComponent<Text>();
            Assert.LessOrEqual(descText.preferredHeight, descText.rectTransform.rect.height + 0.5f, "추출 도구 설명이 잘리지 않는다");
        }

        /// <summary>
        /// 영수증 위 준비 음료 안내와 스크롤 칸이 겹치지 않고, 추출 도구 영역의 제목·버튼·설명·안내가 보이는 칸 위 끝에 반쯤 걸려
        /// (안내 글 바로 아래에서 잘려) 보이지 않는다 — 각 요소는 위 끝 위로 완전히 넘어갔거나, 위 끝이 칸 안에 있다.
        /// </summary>
        static void AssertReceiptTopClean(RectTransform view, string label)
        {
            var vc = new Vector3[4]; view.GetWorldCorners(vc);
            var pc = new Vector3[4]; ((RectTransform)GameObject.Find("Prepared").transform).GetWorldCorners(pc);
            Assert.GreaterOrEqual(pc[0].y, vc[1].y - 0.5f, $"준비 음료 안내가 스크롤 칸과 겹치지 않는다: {label}");
            var names = new System.Collections.Generic.List<string> { "H_tech", "TechDesc", "CoffeeCombat" };
            foreach (var m in CoffeeBrewing.All) names.Add("Method" + m);
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var c = new Vector3[4]; ((RectTransform)go.transform).GetWorldCorners(c);
                bool above = c[0].y >= vc[1].y - 0.5f, topInside = c[1].y <= vc[1].y + 0.5f;
                Assert.IsTrue(above || topInside, $"{n}가 보이는 칸 위 끝에 반쯤 걸려 잘린다: {label}");
            }
        }

        /// <summary>
        /// 영수증 스크롤 보이는 칸(가림 여백 반영)의 아래·위 끝에 줄·버튼이 반쯤 걸려 보이지 않는다 — 고정 아래쪽(제조 확정) 바로 위에서
        /// −/+ 버튼이 반만 보이던 문제. 각 줄·버튼은 보이는 칸 안에 통째로 있거나 완전히 바깥(가려짐)이다.
        /// </summary>
        static void AssertReceiptRowsWhole(RectTransform view, string label)
        {
            var viewport = view.GetComponent<ScrollRect>().viewport;
            var mask = viewport.GetComponent<RectMask2D>();
            Assert.IsNotNull(mask, "영수증 보이는 칸 가림");
            var vc = new Vector3[4]; viewport.GetWorldCorners(vc);
            float scale = viewport.lossyScale.y;
            float visBottom = vc[0].y + mask.padding.y * scale, visTop = vc[1].y - mask.padding.w * scale;
            Assert.Greater(visTop - visBottom, 66f * scale, $"보이는 칸이 줄 하나보다 크다: {label}");
            float tol = 0.5f * Mathf.Max(scale, 0.01f);
            var content = view.GetComponent<ScrollRect>().content;
            var items = new System.Collections.Generic.List<RectTransform>();
            foreach (RectTransform c in content) if (c.gameObject.activeInHierarchy) items.Add(c);
            foreach (var b in content.GetComponentsInChildren<Button>()) items.Add((RectTransform)b.transform);
            foreach (var it in items)
            {
                var c = new Vector3[4]; it.GetWorldCorners(c);
                bool outside = c[1].y <= visBottom + tol || c[0].y >= visTop - tol;
                bool inside = c[0].y >= visBottom - tol && c[1].y <= visTop + tol;
                Assert.IsTrue(outside || inside, $"{it.name}가 보이는 칸 끝에 반쯤 걸려 보인다: {label} (줄 {c[0].y:0.#}~{c[1].y:0.#}, 칸 {visBottom:0.#}~{visTop:0.#})");
            }
        }

        /// <summary>시각 증거(창 없음, CCT_EVIDENCE_DIR이 있을 때만): 추출법별 전투 운용 안내를 1920×1080·1280×720으로 렌더한다.</summary>
        [UnityTest]
        public IEnumerator Evidence_MethodCombatGuide_Captures()
        {
            string evidence = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(evidence)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(evidence);
            DrinkLabView lab = null;
            yield return OpenCoffee(v => lab = v);
            // 배치 모드 화면은 16:9가 아니라 좁은 배치(영수증 서랍)로 만들어진다. 증거는 기본 16:9 넓은 배치로 다시 만들고,
            // 화면비 검사(Update)가 되돌리지 않게 캡처 동안만 뷰를 멈춘다.
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var t = typeof(DrinkLabView);
            lab.enabled = false;
            t.GetField("narrow", F).SetValue(lab, false);
            t.GetField("receiptOpen", F).SetValue(lab, false);
            t.GetField("layoutW", F).SetValue(lab, 1920f);
            ((System.Collections.IDictionary)t.GetField("tabButtons", F).GetValue(lab)).Clear();
            UnityEngine.Object.DestroyImmediate(((RectTransform)t.GetField("root", F).GetValue(lab)).gameObject);
            foreach (var f in new[] { "body", "stage", "mixPanel", "receiptToggle" }) t.GetField(f, F).SetValue(lab, null);
            t.GetMethod("Build", F).Invoke(lab, null);
            yield return null;
            Assert.IsFalse(lab.Narrow);
            var canvas = GameObject.Find("MixScroll").GetComponentInParent<Canvas>().rootCanvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var adaptive = canvas.GetComponent<AdaptiveCanvasScaler>();
            if (adaptive != null) adaptive.enabled = false;
            foreach (var (w, h) in new[] { (1920, 1080), (1280, 720) })
            {
                // 그 해상도의 배율로 먼저 바꾼 뒤 도구를 골라 행 높이를 실제 게임과 같은 글자 크기로 잰다(캡처는 같은 배율로 렌더).
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = w / 1920f;
                Canvas.ForceUpdateCanvases();
                // 에스프레소가 기본값이라, 다른 도구를 먼저 골랐다가 돌아와야 '고르는 순간'의 스크롤 위치가 찍힌다.
                foreach (var m in new[] { CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot, CoffeeBrewMethod.EspressoMachine })
                {
                    lab.SetCoffeeMethod(m);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertReceiptTopClean((RectTransform)GameObject.Find("MixScroll").transform, $"{m} {w}x{h}");
                    AssertReceiptRowsWhole((RectTransform)GameObject.Find("MixScroll").transform, $"{m} {w}x{h}");
                    yield return SkinEvidenceCapture.CaptureTo(evidence, s => Debug.Log("[Evidence] " + s), null, new[] { canvas }, w, h, $"coffee_combat_{m}_{w}x{h}");
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
