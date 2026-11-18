using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>재료 확장 10종(PlayMode): 도감의 문화·산지 칸(맛·향과 분리, 720p·가장 좁은 배치에서 잘림 없음), 재료 목록 이름 한 줄,
    /// 산지 원두가 원두 스쿱·공용 원두 애니메이션으로 계량되어 배합·장부에 그 산지로 들어가는지.</summary>
    public class DrinkIngredientExpansionPlayTests
    {
        string dir;

        static readonly string[] Origins = { "ethiopia_yirgacheffe_kochere", "colombia_huila_vergel", "brazil_mantiqueira_alves_lima" };

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
            dir = Path.Combine(Application.temporaryCachePath, "drinkexpplay_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
            foreach (var d in DrinkIngredients.All) DrinkLab.Data.Add(d.id, 1000);
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

        static IEnumerator OpenLab(Action<DrinkLabView> got, string kindButton = null)
        {
            var select = CharacterSelectView.Show((j, w) => { });
            yield return null;
            select.DrinkLabButton.onClick.Invoke();
            yield return null;
            var lab = select.DrinkLabScreen;
            Assert.IsNotNull(lab);
            if (kindButton != null)
            {
                var btn = GameObject.Find(kindButton);
                Assert.IsNotNull(btn, kindButton);
                btn.GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
            got(lab);
        }

        static IEnumerator Until(Func<bool> cond, float timeout = 12f)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "시간 초과");
        }

        static float HeightAt(Text t, float width) =>
            t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(width, 0f))) / t.pixelsPerUnit;

        /// <summary>한 줄 폭(논리 단위). 최적 맞춤(best fit) 글은 가장 작은 허용 크기에서 잰다 — 그 크기에서 들어가야 렌더링 크기가 칸 안에 들어간다.</summary>
        static float WidthOf(Text t)
        {
            var s = t.GetGenerationSettings(Vector2.zero);
            if (t.resizeTextForBestFit) { s.resizeTextForBestFit = false; s.fontSize = t.resizeTextMinSize; }
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, s) / t.pixelsPerUnit;
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [UnityTest]
        public IEnumerator Codex_CultureSeparateFromTaste_FitsAt720pAndNarrowest()
        {
            // 2026-11-15 피드백 2·6: 도감은 그림 격자 + 칸을 누르면 상세(Codex_<id>). 본문·문화·산지 칸 분리와 상자 안 맞춤은 상세에서 검사한다.
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.SetTab(DrinkLabView.Tab.Codex);
            yield return null;
            int cards = 0, cultures = 0;
            var problems = new System.Collections.Generic.List<string>();
            foreach (var def in DrinkIngredients.All)
            {
                lab.OpenCodexDetail(def);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var card = lab.transform.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "Codex_" + def.id && r.gameObject.activeInHierarchy);
                Assert.IsNotNull(card, def.id + " 도감 상세");
                Assert.AreEqual(def.id, lab.CodexDetailId);
                cards++;
                var body = card.Find("DetailScroll/Content/Body").GetComponent<Text>();
                var name = card.Find("Name").GetComponent<Text>();
                Assert.IsTrue(body.supportRichText, def.id + ": 리치 텍스트 렌더링 활성화");
                StringAssert.StartsWith("<b>맛·향</b>: " + def.taste, body.text, def.id + ": 맛·향은 굵게 본문 첫 줄");
                if (!string.IsNullOrEmpty(def.culture)) StringAssert.DoesNotContain(def.culture, body.text, def.id + ": 문화 사실은 본문과 분리");
                if (WidthOf(name) > name.rectTransform.rect.width + 0.5f) problems.Add($"{def.id} 이름 폭 {WidthOf(name):0}>{name.rectTransform.rect.width}");
                if (name.resizeTextForBestFit && name.resizeTextMinSize < DrinkPaperUi.Small) problems.Add($"{def.id} 이름 최소 크기 {name.resizeTextMinSize} < {DrinkPaperUi.Small}");
                var cardRect = WorldRect(card);
                if (WorldRect(name.rectTransform).Overlaps(WorldRect((RectTransform)card.Find("ColorGlass")))) problems.Add($"{def.id} 이름 칸이 색 잔과 겹침");
                if (WorldRect((RectTransform)card.Find("Icon")).Overlaps(WorldRect((RectTransform)card.Find("DetailScroll")))) problems.Add($"{def.id} 큰 그림이 본문과 겹침");
                var cultureTf = card.Find("DetailScroll/Content/Culture");
                Assert.AreEqual(!string.IsNullOrEmpty(def.culture), cultureTf != null, def.id + ": 문화·산지 칸은 해설이 있을 때만");
                if (HeightAt(body, body.rectTransform.rect.width) > body.rectTransform.rect.height + 0.5f) problems.Add($"{def.id} 본문 {HeightAt(body, body.rectTransform.rect.width):0}>{body.rectTransform.rect.height}");
                var content = (RectTransform)body.transform.parent;
                float bottom = -body.rectTransform.anchoredPosition.y + body.rectTransform.rect.height;
                if (cultureTf != null)
                {
                    cultures++;
                    var culture = cultureTf.GetComponent<Text>();
                    Assert.IsTrue(culture.supportRichText, def.id + ": 문화·산지 리치 텍스트 렌더링 활성화");
                    Assert.AreEqual("<b>문화·산지</b>: " + def.culture, culture.text, def.id + ": 문화·산지 강조 표시");
                    Assert.GreaterOrEqual(culture.fontSize, DrinkPaperUi.Body, def.id + ": 본문 크기(720p 18px) 이상, 작은 글씨로 줄이지 않음");
                    Assert.AreNotEqual(body.color, culture.color, def.id + ": 색으로도 구분");
                    if (HeightAt(culture, culture.rectTransform.rect.width) > culture.rectTransform.rect.height + 0.5f) problems.Add($"{def.id} 문화 {HeightAt(culture, culture.rectTransform.rect.width):0}>{culture.rectTransform.rect.height}");
                    var b = WorldRect(body.rectTransform); var c = WorldRect(culture.rectTransform);
                    if (c.yMax > b.yMin + 0.01f) problems.Add($"{def.id} 문화 칸이 본문과 겹침");
                    bottom = -culture.rectTransform.anchoredPosition.y + culture.rectTransform.rect.height;
                }
                if (bottom > content.rect.height + 0.5f) problems.Add($"{def.id} 스크롤 내용 {content.rect.height} < 글 끝 {bottom}");
                var close = card.Find("CloseDetail") as RectTransform;
                Assert.IsNotNull(close, def.id + " 목록으로 버튼");
                if (WorldRect(close).Overlaps(WorldRect((RectTransform)card.Find("DetailScroll")))) problems.Add($"{def.id} 닫기 버튼이 본문 스크롤과 겹침");
                if (!cardRect.Contains(WorldRect(close).center)) problems.Add($"{def.id} 닫기 버튼이 카드 밖");
                TestContext.WriteLine($"{def.id} body {body.rectTransform.rect.height:0} content {content.rect.height:0} card {card.rect.height:0}");
                lab.CloseCodexDetail();
                Assert.IsFalse(lab.CodexDetailOpen, "목록으로");
            }
            Assert.IsEmpty(problems, string.Join(" / ", problems));
            Assert.AreEqual(DrinkIngredients.All.Count, cards);
            Assert.AreEqual(DrinkIngredients.All.Count(d => !string.IsNullOrEmpty(d.culture)), cultures);
            Assert.GreaterOrEqual(cultures, 10, "신규 10종 모두 문화·산지 칸");
            // 격자: 같은 행 칸은 같은 높이, 다음 행은 겹치지 않는다. 스크롤 내용이 마지막 칸과 하단 안내까지 담는다.
            var cells = DrinkIngredients.All.Select(d => (RectTransform)lab.CodexGridContent.Find("Cell_" + d.id)).ToList();
            Assert.IsTrue(cells.All(c => c != null), "필터 없음 = 모든 재료 칸");
            for (int i = 1; i < cells.Count; i++)
            {
                var prev = WorldRect(cells[i - 1]); var cur = WorldRect(cells[i]);
                if (Mathf.Abs(prev.yMax - cur.yMax) < 0.01f) { Assert.AreEqual(prev.height, cur.height, 0.01f); Assert.LessOrEqual(prev.xMax, cur.xMin + 0.01f, "같은 행 겹침 " + cells[i].name); }
                else Assert.LessOrEqual(cur.yMax, prev.yMin + 0.01f, "행이 겹치지 않음 " + cells[i].name);
            }
            var rule = lab.CodexGridContent.Find("SupplyRule") as RectTransform;
            Assert.GreaterOrEqual(WorldRect(rule).yMin, WorldRect(lab.CodexGridContent).yMin - 0.01f, "스크롤 높이 안");
            Assert.Less(WorldRect(rule).yMax, WorldRect(cells[cells.Count - 1]).yMin + 0.01f, "안내 칸은 마지막 칸 아래");
            TestContext.WriteLine($"narrow={lab.Narrow} screen={Screen.width}x{Screen.height}");
        }

        [UnityTest]
        public IEnumerator IngredientRows_EveryKind_NewIngredientsListed_NamesOneLine(
            [Values("KindCocktail", "KindTea", "KindCoffee")] string kindButton)
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v, kindButton);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var kind = lab.Kind;
            var rows = lab.GetComponentsInChildren<RectTransform>(true).Where(r => r.name.StartsWith("Row_", StringComparison.Ordinal)).ToList();
            foreach (var def in DrinkIngredients.All)
            {
                var row = rows.FirstOrDefault(r => r.name == "Row_" + def.id);
                Assert.AreEqual(def.UsableIn(kind), row != null, $"{kind} {def.id} 목록 필터");
                if (row == null) continue;
                var name = row.Find("Name").GetComponent<Text>();
                Assert.AreEqual(def.name, name.text);
                Assert.LessOrEqual(WidthOf(name), name.rectTransform.rect.width + 0.5f, $"{def.id} 이름 한 줄({WidthOf(name):0}/{name.rectTransform.rect.width})");
                var owned = row.Find("Owned").GetComponent<Text>();
                Assert.LessOrEqual(owned.preferredHeight, owned.rectTransform.rect.height + 0.5f, def.id + " 보유·효과 줄");
            }
        }

        [UnityTest]
        public IEnumerator OriginBeans_ScoopWithSharedBeanAnimation_IntoEveryApparatus(
            [Values(CoffeeBrewMethod.EspressoMachine, CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot)] CoffeeBrewMethod method)
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v, "KindCoffee");
            Assert.AreEqual(DrinkKind.Coffee, lab.Kind);
            lab.SetCoffeeMethod(method);
            var stage = lab.Stage;
            Assert.AreEqual(3, DrinkArt.Frames("dl_leaf_coffee_bean").Length, "공용 원두 애니메이션");
            // 두 산지를 차례로 덜어 블렌드: 각 산지가 자기 id로 배합·장부에 들어가고 합이 원두 한 분량이다.
            string first = Origins[(int)method - 1], second = Origins[(int)method % 3];
            int owned0 = DrinkLab.Owned(first);
            foreach (var id in new[] { first, second })
            {
                stage.Select(DrinkIngredients.Get(id));
                Assert.AreEqual(CoffeeBrewing.ScoopArt, stage.LeafToolArt, id + ": 원두 스쿱(다하 아님)");
                stage.BeginPour(Vector2.zero);
                stage.SetTilt(DrinkStageView.TiltMax);
                bool sawBeans = false;
                yield return Until(() =>
                {
                    sawBeans |= UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
                        .Any(i => i.isActiveAndEnabled && i.name.StartsWith("CoffeeBeanInScoop", StringComparison.Ordinal) && i.sprite != null && i.sprite.name.Contains("coffee_bean"));
                    return stage.InMeasure >= 60 || stage.MeasureDumps >= 1 || !stage.Pouring;
                }, 10f);
                stage.StopPour();
                yield return Until(() => !stage.Transferring && !stage.Ledger.HasInFlight(), 10f);
                Assert.IsTrue(sawBeans, id + ": 스쿱 안에 공용 원두 그림이 쌓인다");
                Assert.Greater(lab.Draft.AmountOf(id), 0, id + " 배합에 그 산지로 기록");
                Assert.AreEqual(lab.Draft.AmountOf(id) / 10.0, stage.Ledger[DrinkStageView.LedgerVessel].Leaf(id), 0.01, id + " 장부 = 배합");
            }
            Assert.AreEqual(0, lab.Draft.AmountOf(DrinkIngredients.CoffeeBean), "기본 원두는 쓰지 않음");
            Assert.AreEqual(lab.Draft.CoffeeDose() / 10.0, stage.Ledger[DrinkStageView.LedgerVessel].LeafG, 0.01, "장부 총량 = 원두 합");
            Assert.LessOrEqual(lab.Draft.CoffeeDose(), CoffeeBrewing.DoseMaxTenths(method), "두 산지 합으로 최대량 제한");
            Assert.AreEqual(owned0, DrinkLab.Owned(first), "제조 확정 전에는 재고 차감 없음");
        }
    }
}
