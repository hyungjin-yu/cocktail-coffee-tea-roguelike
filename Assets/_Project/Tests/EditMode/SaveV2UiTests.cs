using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 저장 v2 알림/버튼 UI(타이틀·HUD·영구 강화)의 상태와 클릭 동작, 1920×1080 기준 배치 겹침을 EditMode로 검사한다.
    /// 배치는 렌더링 없이 앵커/피벗/크기로 계산한 명목 좌표다 — 실제 렌더 캡처·720p 육안 확인을 대신하지 않는다.
    /// </summary>
    public class SaveV2UiTests : SaveV2Fixture
    {
        const float W = 1920f, H = 1080f, Safe = 48f;
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyUi()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        TitleView ShowTitle()
        {
            var view = TitleView.Show();
            created.Add(view.gameObject);
            return view;
        }

        /// <summary>앵커가 한 점인 요소의 기준 해상도 좌표(왼쪽 아래 원점).</summary>
        static Rect Nominal(RectTransform rt)
        {
            Assert.AreEqual(rt.anchorMin, rt.anchorMax, $"{rt.name}: 점 앵커만 계산");
            var origin = new Vector2(rt.anchorMin.x * W, rt.anchorMin.y * H) + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
            return new Rect(origin, rt.sizeDelta);
        }

        /// <summary>새로 추가한 저장 알림 요소가 같은 화면의 다른 요소와 겹치지 않는지(기존 요소끼리의 배치는 이번 범위 밖).</summary>
        static void AssertNoOverlapWith(Transform root, string target, IEnumerable<RectTransform> others)
        {
            var t = (RectTransform)root.Find(target);
            Assert.IsNotNull(t, target);
            foreach (var o in others)
                if (o != t) Assert.IsFalse(Nominal(t).Overlaps(Nominal(o)), $"{target} ↔ {o.name} 겹침");
        }

        static void AssertInSafeArea(RectTransform rt)
        {
            var r = Nominal(rt);
            Assert.IsTrue(r.xMin >= Safe && r.yMin >= Safe && r.xMax <= W - Safe && r.yMax <= H - Safe, $"{rt.name} 안전 여백48 밖: {r}");
        }

        static List<RectTransform> ActivePointChildren(Transform parent) => parent.Cast<RectTransform>()
            .Where(t => t.gameObject.activeSelf && t.anchorMin == t.anchorMax).ToList();

        [Test]
        public void Title_FutureVersion_BlocksStartUntilGuestChosen_ThenGuestButtonHides()
        {
            const string future = "{\"schemaVersion\":3,\"specialtyPoints\":5}";
            File.WriteAllText(savePath, future);
            var view = ShowTitle();

            Assert.IsTrue(view.GuestButtonVisible);
            Assert.IsFalse(view.RetryButtonVisible);
            StringAssert.Contains(SaveSystem.NoticeFutureVersion, view.SaveNoticeText);

            Assert.IsFalse(view.RequestStart(), "임시 플레이를 고르기 전에는 시작하지 않는다");
            Assert.IsTrue(view.IsOpen);
            StringAssert.Contains(TitleView.NoticeChooseGuestFirst, view.SaveNoticeText);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "guest_session.json")));

            view.ClickGuestButtonForTests();
            Assert.IsFalse(view.GuestButtonVisible);
            Assert.IsTrue(PermanentProgress.IsGuestSession);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "guest_session.json")));
            Assert.AreEqual(future, File.ReadAllText(savePath), "원본은 그대로");
        }

        [Test]
        public void Title_SaveFailure_ShowsShortNoticeAndRetry_RetryClearsIt()
        {
            FailAt(SaveSystem.SaveStage.WriteTmp);
            PermanentProgress.DiscoverCard("dry_gin");
            var view = ShowTitle();

            Assert.IsTrue(view.RetryButtonVisible);
            Assert.IsFalse(view.GuestButtonVisible);
            StringAssert.Contains(PermanentProgress.NoticeSaveFailed, view.SaveNoticeText);
            StringAssert.DoesNotContain("Exception", view.SaveNoticeText, "긴 예외문을 UI에 노출하지 않는다");

            view.ClickRetryButtonForTests();
            Assert.IsTrue(view.RetryButtonVisible, "디스크가 여전히 실패하면 버튼 유지");
            ClearFault();
            view.ClickRetryButtonForTests();
            Assert.IsFalse(view.RetryButtonVisible);
            CollectionAssert.Contains(Disk().discoveredCardIds, "dry_gin");
        }

        [Test]
        public void Title_BackgroundAutoRetrySuccess_ClearsOpenTitleNotice_OnStateChangeOnly()
        {
            FailAt(SaveSystem.SaveStage.WriteTmp);
            PermanentProgress.DiscoverCard("dry_gin");
            var view = ShowTitle();
            Assert.IsTrue(view.RetryButtonVisible);

            view.PollSaveState(); // 상태 불변 — 그대로
            Assert.IsTrue(view.RetryButtonVisible);

            ClearFault();
            PermanentProgress.Tick(PermanentProgress.RetryIntervalSeconds); // SaveRetryRunner가 부르는 것과 같은 경로
            Assert.IsFalse(PermanentProgress.LastSaveFailed);
            view.PollSaveState(); // 타이틀 Update가 매 프레임 부르는 것과 같은 메서드
            Assert.IsFalse(view.RetryButtonVisible, "자동 재시도 성공이 열린 타이틀에 바로 반영");
            StringAssert.DoesNotContain(PermanentProgress.NoticeSaveFailed, view.SaveNoticeText);
        }

        [Test]
        public void Title_StartGuidance_SurvivesStateRefresh_WhileStillProtected()
        {
            File.WriteAllText(savePath, "{\"schemaVersion\":3}");
            var view = ShowTitle();
            view.RequestStart();
            FailAt(SaveSystem.SaveStage.WriteTmp); // 보호 중엔 쓰기를 안 하므로 실패 상태로 바뀌지 않는다
            PermanentProgress.DiscoverCard("dry_gin");
            view.PollSaveState();
            StringAssert.Contains(TitleView.NoticeChooseGuestFirst, view.SaveNoticeText);
            Assert.IsTrue(view.GuestButtonVisible);
            Assert.AreEqual(0, faultHits, "버튼 전에는 디스크 쓰기 시도 자체가 없다");
        }

        [Test]
        public void Title_NoticeAndButtons_DoNotOverlapMenuOrFooter_InEitherState()
        {
            // 미래 버전(임시 플레이 버튼) 상태
            File.WriteAllText(savePath, "{\"schemaVersion\":3}");
            var view = ShowTitle();
            var root = view.transform.Find("Root");
            var items = ActivePointChildren(root);
            Assert.IsTrue(items.Any(t => t.name == "GuestSessionButton"));
            AssertNoOverlapWith(root, "SaveNotice", items);
            AssertNoOverlapWith(root, "GuestSessionButton", items);
            AssertInSafeArea((RectTransform)root.Find("SaveNotice"));
            AssertInSafeArea((RectTransform)root.Find("GuestSessionButton"));
            Object.DestroyImmediate(view.gameObject);

            // 저장 실패(다시 시도 버튼) 상태
            PermanentProgress.SetPathForTests(Path.Combine(dir, "other", "save.json"));
            FailAt(SaveSystem.SaveStage.WriteTmp);
            PermanentProgress.DiscoverCard("dry_gin");
            view = ShowTitle();
            root = view.transform.Find("Root");
            items = ActivePointChildren(root);
            Assert.IsTrue(items.Any(t => t.name == "RetrySaveButton"));
            Assert.IsFalse(items.Any(t => t.name == "GuestSessionButton"), "두 버튼은 같은 자리 — 동시에 보이지 않는다");
            AssertNoOverlapWith(root, "SaveNotice", items);
            AssertNoOverlapWith(root, "RetrySaveButton", items);
            AssertInSafeArea((RectTransform)root.Find("RetrySaveButton"));
        }

        [Test]
        public void Hud_SaveFailedNotice_VisibleOnlyOnFailure_AndDoesNotOverlapTopLeftHud()
        {
            var canvas = new GameObject("HudCanvas", typeof(RectTransform));
            created.Add(canvas);
            var hud = HudView.Create(canvas.transform, null);
            Assert.IsFalse(hud.SaveFailedVisible);

            FailAt(SaveSystem.SaveStage.WriteTmp);
            PermanentProgress.DiscoverCard("dry_gin");
            var failedHud = HudView.Create(canvas.transform, null);
            Assert.IsTrue(failedHud.SaveFailedVisible);

            var root = failedHud.transform;
            var names = new[] { "HpBar", "Suppressed", "SaveFailed", "GaugeBar", "TeaBar", "BeanBar", "Info", "CoffeePoints" };
            var items = names.Select(n => (RectTransform)root.Find(n)).ToList();
            Assert.IsTrue(items.All(i => i != null));
            AssertNoOverlapWith(root, "SaveFailed", items);
            AssertInSafeArea((RectTransform)root.Find("SaveFailed"));
        }

        [Test]
        public void PermanentShop_ShowsLockAndLegacyNotices_AndPurchaseFailureNotice()
        {
            File.WriteAllText(savePath, "{\"specialtyPoints\":3,\"unlockedPermanentSkillIds\":[\"bean_cupper\",\"skilled_roasting\",\"perfect_crema\"]}");
            var canvas = new GameObject("ShopCanvas", typeof(RectTransform));
            created.Add(canvas);
            var shop = PermanentSkillView.Create(canvas.transform, null);
            shop.Show();
            created.AddRange(shop.Nodes.Where(n => n != null && !UnityEditor.AssetDatabase.Contains(n)));

            StringAssert.Contains("구매가 잠겼습니다", shop.NoticeText, "음수 잔액(3-9) 잠금 알림");
            StringAssert.Contains("이전 데이터의 선행 강화 확인 필요", shop.NoticeText, "perfect_crema 선행 없음");
            var tumbler = shop.Nodes.Single(n => n.id == "sturdy_tumbler");
            shop.TryBuy(tumbler);
            Assert.AreEqual(PermanentPurchaseResult.Locked, shop.LastPurchaseResult);

            var root = shop.transform;
            AssertNoOverlapWith(root, "Notice", ActivePointChildren(root));
        }

        [Test]
        public void PermanentShop_LockedCards_ShowShortageNotNegativeBalance_AndDetailKeepsConditions()
        {
            // 2026-10-09 실제720 검수/38: "구매 후 잔액 -3" 대신 부족한 스페셜티 수, 선행·클리어·대상·네 무기 예측은 상세 영역.
            var canvas = new GameObject("ShopCanvas", typeof(RectTransform));
            created.Add(canvas);
            var shop = PermanentSkillView.Create(canvas.transform, null);
            shop.Show();
            created.AddRange(shop.Nodes.Where(n => n != null && !UnityEditor.AssetDatabase.Contains(n)));

            string StatusOf(string id) => shop.transform.Find("Node_" + id + "/Chip/Status").GetComponent<Text>().text;
            Assert.AreEqual("스페셜티 3 부족", StatusOf("dark_roast_blend"), "선행 없음·잔액0: 부족 수");
            Assert.AreEqual("선행 필요", StatusOf("perfect_crema"));
            foreach (var t in shop.GetComponentsInChildren<Text>(true))
                StringAssert.DoesNotContain("잔액 -", t.text, t.name + ": 음수 잔액 표시 금지");

            Assert.AreEqual("dark_roast_blend", shop.DetailNode.id, "탭을 열면 첫 카드 상세");
            var crema = shop.Nodes.Single(n => n.id == "perfect_crema");
            shop.TryBuy(crema); // 잠긴 카드 클릭과 같은 경로: 거절되고 상세는 남는다
            Assert.AreEqual(PermanentPurchaseResult.Denied, shop.LastPurchaseResult);
            shop.transform.Find("Node_perfect_crema").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("perfect_crema", shop.DetailNode.id, "잠긴 카드도 상세를 연다");
            StringAssert.Contains("선행   다크 로스트 블렌드 (미구매)", shop.DetailText);
            StringAssert.Contains("클리어   0회 필요", shop.DetailText);
            StringAssert.Contains("레이피어   체력 100% → 101% · 피해 100% → 100.4%", shop.DetailText);
            StringAssert.Contains("보유 스페셜티 0", shop.HeaderText);
            StringAssert.Contains("해금 0 / 24", shop.HeaderText);
        }

        [Test]
        public void PermanentShop_SaveFailure_RollsBackAndShowsRetryNotice()
        {
            PermanentProgress.AddSpecialty(3);
            var canvas = new GameObject("ShopCanvas", typeof(RectTransform));
            created.Add(canvas);
            var shop = PermanentSkillView.Create(canvas.transform, null);
            shop.Show();
            created.AddRange(shop.Nodes.Where(n => n != null && !UnityEditor.AssetDatabase.Contains(n)));
            var cupper = shop.Nodes.Single(n => n.id == "bean_cupper");

            FailAt(SaveSystem.SaveStage.BeforeReplace);
            shop.TryBuy(cupper);
            Assert.AreEqual(PermanentPurchaseResult.SaveFailed, shop.LastPurchaseResult);
            StringAssert.Contains(PermanentProgress.NoticeSaveFailed, shop.NoticeText);
            CollectionAssert.DoesNotContain(PermanentProgress.Current.unlockedPermanentSkillIds, "bean_cupper");

            ClearFault();
            shop.TryBuy(cupper);
            Assert.AreEqual(PermanentPurchaseResult.Purchased, shop.LastPurchaseResult, "버튼은 다시 활성 — 재구매 가능");
            Assert.AreEqual("", shop.NoticeText);
        }
    }
}
