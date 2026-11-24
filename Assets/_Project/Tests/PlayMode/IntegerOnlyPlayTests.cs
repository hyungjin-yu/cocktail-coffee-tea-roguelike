using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-06 정수 전용 — 실제 음료 제조 화면(PlayMode)에서 보이는 모든 글자에 소수가 없고, 직접 입력 소수는 거부되며 정수 입력은
    /// 배합·표시가 같은 값인지 확인한다. 환경 변수 CCT_EVIDENCE_DIR이 있으면 창 없이 RenderTexture로 화면을 PNG로 남긴다(포커스·OS 입력 없음).
    /// 저장은 모두 격리 경로(temporaryCachePath) — 기본 사용자 세이브는 건드리지 않는다.
    /// </summary>
    public class IntegerOnlyPlayTests
    {
        // 소수점(1.5, 1,5)만 잡는다. 천 단위 구분(1,850m — 콤마 뒤 정확히 세 자리)은 정수 표기라 제외.
        static readonly Regex Decimal = new Regex(@"\d\.\d(?!\d*초)|\d,(?!\d{3}(?!\d))\d"); // 2026-11-24: 초 단위 소수("0.25초")는 허용
        string dir, evidence;
        readonly StringBuilder log = new StringBuilder();

        static void Clear()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkLabView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<DrinkRevealOverlay>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<ResearchLaptopView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.transform.root.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Clear();
            dir = Path.Combine(Application.temporaryCachePath, "intplay_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
            evidence = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(evidence)) { evidence = Path.Combine(evidence, "integer-only"); Directory.CreateDirectory(evidence); }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!string.IsNullOrEmpty(evidence)) File.AppendAllText(Path.Combine(evidence, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false));
            Clear();
            Time.timeScale = 1f;
            yield return null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[IntegerOnly] " + s); }

        static IEnumerator OpenLab(Action<DrinkLabView> got)
        {
            var select = CharacterSelectView.Show((j, w) => { });
            yield return null;
            select.DrinkLabButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsNotNull(select.DrinkLabScreen);
            got(select.DrinkLabScreen);
        }

        static void Click(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            go.GetComponent<Button>().onClick.Invoke();
        }

        /// <summary>활성 캔버스의 보이는 Text·InputField 글자 전체. 버전 표기(v1.1 등)는 수량이 아니므로 제외한다.</summary>
        static List<string> VisibleTexts()
        {
            var list = new List<string>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                list.Add(t.text);
            }
            return list;
        }

        static List<string> DecimalTexts()
        {
            var bad = new List<string>();
            foreach (var s in VisibleTexts())
            {
                string scrub = Regex.Replace(s, @"v\d+(\.\d+)+(-\w+)?", ""); // 버전 식별자 제외
                if (Decimal.IsMatch(scrub)) bad.Add(s.Replace("\n", "⏎"));
            }
            return bad;
        }

        IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(evidence)) yield break;
            var canvases = new List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.isRootCanvas && c.isActiveAndEnabled) canvases.Add(c);
            foreach (var (w, h) in new[] { (1920, 1080), (1280, 720) })
                yield return SkinEvidenceCapture.CaptureTo(evidence, Line, Camera.main, canvases, w, h, $"{name}-{w}x{h}");
            File.WriteAllText(Path.Combine(evidence, name + "-texts.txt"), string.Join("\n---\n", VisibleTexts()), new UTF8Encoding(false));
        }

        [UnityTest]
        public IEnumerator CraftScreen_DecimalInputRejected_IntegerInputMatchesDisplay_NoDecimalsVisible()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.SkipMotion = true;
            Click("Row_gin");
            yield return null;

            Assert.IsFalse(lab.ApplyTypedAmount("23.5"), "소수 입력 거부");
            StringAssert.Contains("정수", lab.AmountErrorText);
            Assert.AreEqual(0, lab.Draft.AmountOf(DrinkIngredients.Gin), "거부 시 배합 그대로");
            yield return null;
            Line("reject: " + lab.AmountErrorText);
            yield return Shot("craft-reject-decimal");

            Assert.IsTrue(lab.ApplyTypedAmount("24"), lab.AmountErrorText);
            yield return null;
            Assert.AreEqual(240, lab.Draft.AmountOf(DrinkIngredients.Gin));
            Assert.AreEqual("24", lab.AmountField.text, "입력 칸 = 실제 배합");
            StringAssert.Contains("24mL", lab.MixText);
            lab.Nudge(10);
            Assert.AreEqual(250, lab.Draft.AmountOf(DrinkIngredients.Gin), "↑ 한 번 = 1mL");
            lab.Nudge(-100);
            Assert.AreEqual(150, lab.Draft.AmountOf(DrinkIngredients.Gin), "Shift ↓ = 10mL");
            Click("Row_lemon_juice");
            yield return null;
            Assert.IsTrue(lab.ApplyTypedAmount("8"));
            lab.RefreshForTests();
            yield return null;
            Line("mix: " + lab.MixText.Replace("\n", " | "));
            Line("buff: " + lab.BuffText.Replace("\n", " | "));
            Assert.IsFalse(Decimal.IsMatch(lab.BuffText), lab.BuffText);
            Assert.IsFalse(Decimal.IsMatch(lab.MixText), lab.MixText);
            Assert.IsFalse(Decimal.IsMatch(lab.CostText), lab.CostText);
            var bad = DecimalTexts();
            Assert.IsEmpty(bad, string.Join("\n", bad));
            yield return Shot("craft-integer");
        }

        [UnityTest]
        public IEnumerator CoffeeTeaCodexOrders_NoDecimalsVisible()
        {
            DrinkLabView lab = null;
            yield return OpenLab(v => lab = v);
            lab.SkipMotion = true;
            var bad = new List<string>();

            Click("KindTea");
            yield return null;
            bad.AddRange(DecimalTexts());
            yield return Shot("tea");

            Click("KindCoffee");
            yield return null;
            foreach (CoffeeBrewMethod m in CoffeeBrewing.All)
            {
                lab.SetCoffeeMethod(m);
                yield return null;
                Assert.IsFalse(Decimal.IsMatch(DrinkLabView.CoffeeCombatGuide(m)), DrinkLabView.CoffeeCombatGuide(m));
                bad.AddRange(DecimalTexts());
            }
            lab.SetReceiptOpen(true);
            yield return null;
            Line("coffee guide moka: " + DrinkLabView.CoffeeCombatGuide(CoffeeBrewMethod.MokaPot).Replace("\n", " | "));
            yield return Shot("coffee-moka");

            lab.SetTab(DrinkLabView.Tab.Codex);
            yield return null;
            bad.AddRange(DecimalTexts());
            yield return Shot("codex");

            lab.SetTab(DrinkLabView.Tab.Orders);
            yield return null;
            bad.AddRange(DecimalTexts());
            yield return Shot("orders");

            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
