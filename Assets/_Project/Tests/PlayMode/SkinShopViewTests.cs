using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CCT.Tests
{
    /// <summary>스킨 상점 화면: 결제 꺼짐/미지원 플랫폼에서도 미리보기는 보이고 구매·복원은 막힌다. 뒤로 버튼 이름·터치 크기.</summary>
    public class SkinShopViewTests
    {
        class NullPersister : ICommercePersister
        {
            public CommerceState Load() { var s = new CommerceState(); s.Normalize(); return s; }
            public bool Save(CommerceState state) => true;
        }

        GameObject canvasGo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null) Object.Destroy(canvasGo);
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.Destroy(v.gameObject);
            SkinCommerce.SetSharedForTests(null);
            yield return null;
        }

        static SkinCommerce DisabledCommerce() =>
            new SkinCommerce(CommerceConfig.Disabled(), new UnavailableSkinStore(), null, new NullPersister(), () => 0d);

        [UnityTest]
        public IEnumerator PlatformUnavailable_PreviewShown_BuyAndRestoreDisabled_BackCloses()
        {
            var canvas = UiFactory.CreateCanvas(null, "SkinShopTest", 100);
            canvasGo = canvas.gameObject;
            bool closed = false;
            var commerce = DisabledCommerce();
            var view = SkinShopView.Create(canvas.transform, () => closed = true, () => commerce);
            view.Show();
            yield return null;

            Assert.IsTrue(view.IsOpen);
            foreach (var def in SkinCatalog.All)
            {
                var buy = view.BuyButton(def.productId);
                Assert.IsFalse(buy.interactable, "결제 미지원 플랫폼에서는 구매 불가");
                Assert.IsFalse(view.EquipButton(def.productId).interactable, "미보유 장착 불가");
                Assert.GreaterOrEqual(((RectTransform)buy.transform).sizeDelta.y, 120f, "터치 크기");
                Assert.AreEqual("미보유", view.StateText(def.productId));
                StringAssert.Contains(UnavailableSkinStore.Message, view.GlobalText);
                bool hasSkinArt = SkinArt.PreviewSprite(def) != null;
                Assert.AreEqual(!hasSkinArt, view.ArtNoteVisible(def.productId), "아트 없을 때만 '아트 준비 중'");
                if (hasSkinArt) Assert.AreSame(SkinArt.PreviewSprite(def), view.PreviewSprite(def.productId), "소유 전에도 스킨 미리보기");
            }
            Assert.IsFalse(view.RestoreButton.interactable);
            Assert.AreEqual("BackButton", view.BackButton.name);
            Assert.GreaterOrEqual(((RectTransform)view.BackButton.transform).sizeDelta.y, 120f);

            view.BackButton.onClick.Invoke();
            Assert.IsFalse(view.IsOpen);
            Assert.IsTrue(closed);
        }

        [UnityTest]
        public IEnumerator TitleSkinButton_OpensShop_AndBackReturnsToTitle()
        {
            SkinCommerce.SetSharedForTests(DisabledCommerce());
            var title = TitleView.Show();
            yield return null;

            Button skinButton = null;
            foreach (var b in title.GetComponentsInChildren<Button>(true))
                if (b.name == "SkinShopButton") skinButton = b;
            Assert.IsNotNull(skinButton, "타이틀에 스킨 버튼");
            skinButton.onClick.Invoke();
            Assert.IsTrue(title.SkinShopView.IsOpen);
            Assert.IsFalse(title.IsOpen);

            title.SkinShopView.BackButton.onClick.Invoke();
            Assert.IsTrue(title.IsOpen);
            Assert.IsFalse(title.SkinShopView.IsOpen);
        }

        /// <summary>2026-10-15 사용자 피드백: 스킨 버튼이 타이틀 메뉴 테두리(카페 배경 안쪽 여백 24) 밖으로 나가면 안 되고, 제목(위 140)과도 겹치지 않는다.</summary>
        [UnityTest]
        public IEnumerator TitleSkinButton_StaysInsideMenuFrame()
        {
            SkinCommerce.SetSharedForTests(DisabledCommerce());
            var title = TitleView.Show();
            yield return null;
            RectTransform rt = null;
            foreach (var b in title.GetComponentsInChildren<Button>(true))
                if (b.name == "SkinShopButton") rt = (RectTransform)b.transform;
            Assert.IsNotNull(rt);
            Assert.AreEqual(new Vector2(1f, 1f), rt.anchorMin, "오른쪽 위 기준");
            float right = -rt.anchoredPosition.x, top = -rt.anchoredPosition.y;
            Assert.GreaterOrEqual(right, 24f + 16f, "오른쪽 테두리 안");
            Assert.GreaterOrEqual(top, 24f + 16f, "위 테두리 안");
            Assert.Less(top + rt.sizeDelta.y, 140f, "제목 상자(위 140)와 겹치지 않음");
        }
    }
}
