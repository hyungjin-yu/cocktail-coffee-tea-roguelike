using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-10-13 기획 52/53: 표시 제목 "음료 마스터", 설정·제작 정보의 개인정보처리방침 링크, Android 식별자 유지.</summary>
    public class StoreInfoTests
    {
        GameObject canvasGo;
        System.Action<string> previousOpener;
        string opened;

        [SetUp]
        public void SetUp()
        {
            previousOpener = GameInfo.UrlOpener;
            opened = null;
            GameInfo.UrlOpener = url => opened = url; // 실제 브라우저를 열지 않는다
            canvasGo = UiFactory.CreateCanvas(null, "StoreInfoTest", 0).gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            GameInfo.UrlOpener = previousOpener;
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void Constants_TitleAndPrivacyUrl_AndroidIdentityPreserved()
        {
            Assert.AreEqual("음료 마스터", GameInfo.Title);
            Assert.AreEqual("https://hyungjin-yu.itch.io/coffee-cocktail-tea/devlog/1704962/-privacy-policy", GameInfo.PrivacyPolicyUrl);
            StringAssert.StartsWith("https://", GameInfo.PrivacyPolicyUrl);
            Assert.AreEqual("com.hyungjinyu.cocktailcoffeetea", CCT.Editor.BuildRelease.AndroidPackageId, "패키지 ID는 제목과 별개로 유지");
            Assert.GreaterOrEqual(CCT.Editor.BuildRelease.AndroidTargetSdk, 35, "Google Play 목표 API");
        }

        [Test]
        public void Title_ShowsGameTitle()
        {
            var view = TitleView.Show();
            try
            {
                var title = view.transform.Find("Root/Title").GetComponent<Text>();
                Assert.AreEqual(GameInfo.Title, title.text);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void Credits_ShowsTitle_AndPrivacyButtonOpensPolicy()
        {
            var view = CreditsView.Create(canvasGo.transform, null);
            var texts = view.GetComponentsInChildren<Text>(true).Select(t => t.text).ToList();
            Assert.IsTrue(texts.Any(t => t.Contains(GameInfo.Title)), "제작 정보에 표시 제목");
            Assert.IsFalse(texts.Any(t => t.Contains("칵테일 · 커피 · 차")), "이전 가제 표시 없음");
            var button = view.GetComponentsInChildren<Button>(true).Single(b => b.name == "PrivacyPolicyButton");
            button.onClick.Invoke();
            Assert.AreEqual(GameInfo.PrivacyPolicyUrl, opened);
        }

        [Test]
        public void Settings_PrivacyButtonOpensPolicy()
        {
            var view = SettingsView.Create(canvasGo.transform);
            var button = view.GetComponentsInChildren<Button>(true).Single(b => b.name == "PrivacyPolicyButton");
            StringAssert.Contains(GameInfo.PrivacyPolicyLabel, button.GetComponentInChildren<Text>(true).text);
            button.onClick.Invoke();
            Assert.AreEqual(GameInfo.PrivacyPolicyUrl, opened);
        }
    }
}
