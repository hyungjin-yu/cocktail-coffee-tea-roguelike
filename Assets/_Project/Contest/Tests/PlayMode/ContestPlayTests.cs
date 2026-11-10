using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Contest.Tests
{
    /// <summary>실제 프레임에서 공모전판 앱을 만들고 화면(uGUI)을 거쳐 임무 1 전체를 진행한다.
    /// 입력은 게임 메서드 호출 + 붓기 유지(VirtualHold) — 버튼 onClick 호출로 실제 UI 연결도 확인.</summary>
    public class ContestPlayTests
    {
        string dir;
        ContestApp app;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "pps-contest-play-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            app = ContestApp.Create(new ContestSave(Path.Combine(dir, ContestSave.FileName)));
        }

        [TearDown]
        public void TearDown()
        {
            if (app != null) Object.DestroyImmediate(app.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.DestroyImmediate(c.gameObject);
            try { Directory.Delete(dir, true); } catch { }
        }

        static Button Btn(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);

        static Text Txt(string name) =>
            Object.FindObjectsByType<Text>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        static IEnumerator Click(string name)
        {
            var b = Btn(name);
            Assert.IsNotNull(b, "button " + name);
            Assert.IsTrue(b.interactable, "button " + name + " interactable");
            b.onClick.Invoke();
            yield return null;
        }

        IEnumerator Pour()
        {
            int before = app.Session.Tray.Count;
            app.VirtualHold = true;
            float t = 0f;
            while (app.Session.Tray.Count == before && t < 3f) { t += Time.deltaTime; yield return null; }
            app.VirtualHold = false;
            yield return null;
            Assert.Greater(app.Session.Tray.Count, before, "pour hold must brew");
            Assert.GreaterOrEqual(t, 1.0f, "brewing requires holding ~1.2s, not a single click");
        }

        [UnityTest]
        public IEnumerator ContestStartsOnOwnTitle_WithoutSteamMenus()
        {
            yield return null;
            Assert.AreEqual(View.Title, app.View);
            Assert.IsNotNull(Txt("TitleMain"));
            Assert.IsNull(Object.FindFirstObjectByType<TitleView>(), "Steam title must not exist");
            Assert.IsNull(Btn("SkinShopButton"));
            Assert.IsNull(Btn("PermanentShopButton"));
            Assert.IsNotNull(Txt("Pitch"));
            var disclaimer = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text.Contains("조달청이 제작하거나 후원한 게임이 아니며"));
            Assert.IsTrue(disclaimer, "misattribution disclaimer visible on title");
            yield return Click("HelpButton");
            Assert.IsTrue(app.HelpOpen);
            StringAssert.Contains("https://www.pps.go.kr/kor/content.do?key=00179", Txt("HelpBody").text);
            yield return Click("CloseHelp");
            Assert.IsFalse(app.HelpOpen);
        }

        [UnityTest]
        public IEnumerator Mission1_FullFlowThroughUi_WithFailedInspectionAndFix()
        {
            yield return Click("StartButton");
            Assert.AreEqual(View.Select, app.View);
            Assert.IsFalse(Btn("StartMission1").interactable, "mission 2 locked");
            yield return Click("StartMission0");
            Assert.AreEqual(View.Mission, app.View);
            Assert.AreEqual(Stage.Request, app.Session.Stage);
            Assert.IsFalse(Btn("FinishRequest").interactable);
            for (int i = 0; i < app.Session.Conditions.Count; i++) yield return Click("CheckCond" + i);
            yield return Click("FinishRequest");
            Assert.AreEqual(Stage.Supply, app.Session.Stage);

            yield return Click("Offer1");
            yield return Click("PlaceOrder");
            Assert.AreEqual(Stage.Supply, app.Session.Stage);
            StringAssert.Contains("품명 불일치", Txt("VerdictText").text);
            yield return Click("Offer0");
            yield return Click("PlaceOrder");
            Assert.AreEqual(Stage.Receive, app.Session.Stage);
            Assert.AreEqual(6, app.Session.Remaining);

            yield return Click("Accept");
            StringAssert.Contains("찢어짐", Txt("PackText").text);
            yield return Click("Accept"); // 파손 포장 입고(실수)
            yield return Click("Accept");
            Assert.AreEqual(Stage.Brew, app.Session.Stage);

            Assert.IsFalse(Btn("PourButton").interactable, "temperature/vessel must be chosen first");
            yield return Click("Temp1");
            yield return Click("Vessel0");
            Assert.IsTrue(Btn("PourButton").interactable);
            yield return Pour();
            Assert.AreEqual(4, app.Session.Tray.Count);

            yield return Click("Inspect");
            Assert.AreEqual(Stage.Inspect, app.Session.Stage);
            Assert.IsFalse(app.Session.LastReport.Pass);
            StringAssert.Contains("재료 이력 불일치", Txt("InspectBody").text);
            Assert.IsFalse(Btn("DeliverBlocked").interactable);
            yield return Click("Exclude");
            Assert.AreEqual(2, app.Session.Tray.Count);
            yield return Click("CupsMinus");
            yield return Click("CupsMinus");
            yield return Pour();
            yield return Click("Inspect");
            Assert.IsTrue(app.Session.LastReport.Pass);
            yield return Click("Deliver");
            Assert.AreEqual(View.Result, app.View);
            Assert.IsTrue(app.LastBadgeFirst);
            Assert.AreEqual(1, app.Save.Data.badges);
            Assert.IsTrue(File.Exists(app.Save.Path));
            StringAssert.Contains("인수", Txt("History").text);

            // 재실행: 깨끗한 새 상태, 배지 중복 없음
            yield return Click("Retry");
            Assert.AreEqual(Stage.Request, app.Session.Stage);
            Assert.AreEqual(0, app.Session.Tray.Count);
            Assert.AreEqual(0, app.Session.Clock);
            Assert.AreEqual(1, app.Save.Data.badges);
            Assert.IsTrue(app.Save.IsUnlocked(1));
        }

        [UnityTest]
        public IEnumerator LockedMission_CannotStart()
        {
            yield return null;
            Assert.IsFalse(app.StartMission(2));
            Assert.AreEqual(View.Title, app.View);
        }

        [UnityTest]
        public IEnumerator FailedMission_ShowsReason_AndRetryResets()
        {
            app.StartMission(0);
            for (int i = 0; i < app.Session.Conditions.Count; i++) app.CheckCondition(i);
            app.FinishRequest();
            app.SelectOffer(0);
            app.PlaceOrder();
            for (int i = 0; i < 40 && app.Session.Stage == Stage.Receive; i++) app.RejectPack();
            yield return null;
            Assert.AreEqual(Stage.Failed, app.Session.Stage);
            Assert.IsNotNull(Btn("Retry"));
            yield return Click("Retry");
            Assert.AreEqual(Stage.Request, app.Session.Stage);
            Assert.AreEqual(0, app.Session.Spent);
        }
    }
}
