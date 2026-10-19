using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CCT.Tests
{
    /// <summary>79: 캐릭터 스킨이 12종으로 늘어도 목록 스크롤·선택·상세 미리보기·코인 구매(중복 클릭)·장착·직업 전용·키보드가 동작.</summary>
    public class SkinShopListTests
    {
        class NullPersister : ICommercePersister
        {
            public CommerceState Load() { var s = new CommerceState(); s.Normalize(); return s; }
            public bool Save(CommerceState state) => true;
        }

        GameObject canvasGo, esGo;
        string dir;
        Func<string, bool> savedReady;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "skinshop79_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            savedReady = DrinkSales.SkinArtReady;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null) Object.Destroy(canvasGo);
            if (esGo != null) Object.Destroy(esGo);
            DrinkSales.SkinArtReady = savedReady;
            DrinkLab.SetPathForTests(null);
            SkinCommerce.SetSharedForTests(null);
            yield return null;
            try { Directory.Delete(dir, true); } catch { }
        }

        SkinShopView Open(SkinCommerce commerce)
        {
            if (EventSystem.current == null) esGo = new GameObject("TestEventSystem", typeof(EventSystem));
            var canvas = UiFactory.CreateCanvas(null, "SkinShopListTest", 100);
            canvasGo = canvas.gameObject;
            var view = SkinShopView.Create(canvas.transform, () => { }, () => commerce);
            view.Show();
            return view;
        }

        static SkinCommerce DisabledCommerce() =>
            new SkinCommerce(CommerceConfig.Disabled(), new UnavailableSkinStore(), null, new NullPersister(), () => 0d);

        [UnityTest]
        public IEnumerator TwelveRows_ScrollFollowsKeyboard_SelectionUpdatesDetail()
        {
            var view = Open(DisabledCommerce());
            yield return null;
            Assert.AreEqual(12, view.RowCount);
            var viewport = view.ListScroll.viewport;
            Assert.Greater(view.ListContent.rect.height, viewport.rect.height, "12종은 한 화면에 다 안 들어가므로 스크롤");
            Assert.IsNotNull(view.ListScroll.verticalScrollbar, "스크롤 막대 표시");

            // 행 ↓ 탐색: 12행 모두 차례로 도달, 마지막 아래 = 복원/뒤로.
            var ids = SkinCatalog.All.Select(d => d.productId).ToList();
            for (int i = 0; i < ids.Count - 1; i++)
                Assert.AreSame(view.RowButton(ids[i + 1]), view.RowButton(ids[i]).FindSelectableOnDown(), ids[i]);
            Assert.AreSame(view.CharacterTabButton, view.RowButton(ids[0]).FindSelectableOnUp());

            // 마지막 행에 포커스 → 상세가 그 스킨, 목록이 따라 내려가 행이 뷰포트 안에 보인다.
            var last = ids[ids.Count - 1];
            EventSystem.current.SetSelectedGameObject(view.RowButton(last).gameObject);
            yield return null;
            yield return null;
            Assert.AreEqual(last, view.SelectedId, "포커스 = 선택");
            Assert.Greater(view.ListContent.anchoredPosition.y, 0f, "아래로 스크롤됨");
            var rowRt = (RectTransform)view.RowButton(last).transform;
            var corners = new Vector3[4];
            rowRt.GetWorldCorners(corners);
            var vp = new Vector3[4];
            viewport.GetWorldCorners(vp);
            Assert.GreaterOrEqual(corners[0].y, vp[0].y - 1f, "행 아래가 뷰포트 안");
            Assert.LessOrEqual(corners[1].y, vp[1].y + 1f, "행 위가 뷰포트 안");
            StringAssert.Contains("티 소믈리에 전용", view.RowStatusText(last));

            // 클릭으로 다른 스킨 선택 → 상세 차이 문구가 그 스킨 것.
            view.RowButton(SkinCatalog.BartenderStage).onClick.Invoke();
            Assert.AreEqual(SkinCatalog.BartenderStage, view.SelectedId);
            StringAssert.Contains("실크햇", view.DifferenceText(SkinCatalog.BartenderStage));
            StringAssert.Contains("바텐더 전용", view.RowStatusText(SkinCatalog.BartenderStage));

            // 목록과 상세가 겹치지 않음(같은 캔버스 기준).
            var listRt = (RectTransform)view.ListScroll.transform;
            var detail = view.BuyButton(SkinCatalog.TeaJade).transform.parent as RectTransform;
            listRt.GetWorldCorners(corners);
            detail.GetWorldCorners(vp);
            Assert.Less(corners[2].x, vp[0].x, "목록 오른쪽 < 상세 왼쪽");
        }

        [UnityTest]
        public IEnumerator CoinSkin_BuyOnce_DuplicateClick_Equip_JobScoped_PaidUnaffected()
        {
            DrinkSales.SkinArtReady = _ => true;
            DrinkLab.Data.coins = 1500;
            var commerce = DisabledCommerce();
            var view = Open(commerce);
            yield return null;

            var buy = view.BuyButton(SkinCatalog.BartenderRail);
            Assert.IsTrue(buy.interactable, "아트 완성 + 코인 1500 → 구매 가능(결제 꺼진 플랫폼에서도)");
            StringAssert.Contains("1000코인", view.BuyLabel(SkinCatalog.BartenderRail));
            Assert.AreSame(buy, view.RowButton(SkinCatalog.BartenderRail).FindSelectableOnRight(), "행 → 구매 버튼");
            buy.onClick.Invoke();
            buy.onClick.Invoke(); // 중복 클릭
            yield return null;
            Assert.AreEqual(500, DrinkLab.Coins, "1000코인 한 번만 차감");
            Assert.IsTrue(DrinkLab.CoinOwns(SkinCatalog.BartenderRail));
            Assert.IsFalse(view.BuyButton(SkinCatalog.BartenderRail).interactable);
            StringAssert.Contains("보유", view.StateText(SkinCatalog.BartenderRail));

            var equip = view.EquipButton(SkinCatalog.BartenderRail);
            Assert.IsTrue(equip.interactable);
            Assert.AreSame(equip, view.RowButton(SkinCatalog.BartenderRail).FindSelectableOnRight(), "구매 불가면 행 → 장착");
            equip.onClick.Invoke();
            Assert.IsTrue(commerce.IsEquipped(SkinCatalog.BartenderRail));
            Assert.IsNull(commerce.State.EquippedFor(Job.TeaSommelier), "다른 직업 장착에는 영향 없음");
            StringAssert.Contains("장착 중", view.RowStatusText(SkinCatalog.BartenderRail));

            Assert.IsFalse(view.BuyButton(SkinCatalog.TeaMountain).interactable, "잔액 500 < 1000");
            StringAssert.Contains("500 부족", view.StateText(SkinCatalog.TeaMountain));
            Assert.IsFalse(view.EquipButton(SkinCatalog.TeaMountain).interactable, "미보유 장착 불가");

            Assert.IsFalse(view.BuyButton(SkinCatalog.BartenderMidnight).interactable, "기존 유료 스킨은 코인 경로로 사지 않는다(이 화면은 스토어 결제)");
            Assert.AreEqual(500, DrinkLab.Coins);

            DrinkSales.SkinArtReady = id => SkinCatalog.IsPaid(id);
            DrinkLab.Data.coins = 5000;
            view.Refresh();
            Assert.IsFalse(view.BuyButton(SkinCatalog.TeaApiary).interactable, "아트 미완성 스킨은 판매 안 함");
            StringAssert.Contains("아트 준비 중", view.StateText(SkinCatalog.TeaApiary));
        }
    }
}
