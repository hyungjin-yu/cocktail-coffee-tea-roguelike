using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// r13 정적 아트 검수(ArtProduction/resume-review-2026-10-07) U1~U3의 구조 회귀:
    /// U1 하이브리드 안내는 불투명 패널이 글자/버튼을 덮고 버튼 글자가 밝다, U2 카드는 카테고리 재료 아이콘을 표시한다,
    /// U3 중앙 게이지 세 줄 뒤에 공통 어두운 바탕이 있다. 실제 화면 대비·가독성(픽셀) 검수를 대신하지 않는다.
    /// </summary>
    public class UiReadabilityTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        Transform Canvas()
        {
            var go = new GameObject("UiReadabilityCanvas", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(1920f, 1080f);
            created.Add(go);
            return go.transform;
        }

        static Rect WorldRect(Transform t)
        {
            var c = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 0.01f && inner.xMax <= outer.xMax + 0.01f &&
            inner.yMin >= outer.yMin - 0.01f && inner.yMax <= outer.yMax + 0.01f;

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [Test]
        public void HybridUnlock_OpaquePanelCoversTextAndButton_ButtonLabelIsLight()
        {
            var view = HybridUnlockView.Create(Canvas());
            var root = view.transform;
            var panel = root.Find("Panel");
            Assert.IsNotNull(panel, "U1: 안내 뒤 불투명 패널");
            Assert.AreEqual(1f, panel.GetComponent<Image>().color.a, 1e-4f, "패널은 불투명이어야 뒤 전투 효과를 가린다");
            Assert.Less(Luminance(panel.GetComponent<Image>().color), 0.2f, "야간 팔레트(어두운 패널) 유지");

            var panelRect = WorldRect(panel);
            var covered = new List<string> { "Title", "Desc", "UnlockButton" };
            if (root.Find("Icon_hybrid_tea_martini") != null) covered.Add("Icon_hybrid_tea_martini");
            foreach (var name in covered)
            {
                var t = root.Find(name);
                Assert.IsNotNull(t, name);
                Assert.IsTrue(Contains(panelRect, WorldRect(t)), $"{name}가 패널 밖으로 나가 전투 화면 위에 직접 그려진다");
                Assert.Less(panel.GetSiblingIndex(), t.GetSiblingIndex(), $"패널이 {name}보다 뒤에 그려져야 한다");
            }

            var label = root.Find("UnlockButton/Label").GetComponent<Text>();
            Assert.Greater(Luminance(label.color), 0.8f, "U1: 해금 버튼 글자는 어두운 버튼 바탕에서 읽히는 밝은 색");
        }

        [Test]
        public void CardSelect_ShowsCategoryMaterialIcon_PerCategory_WithoutOverlappingStatusLines()
        {
            var view = CardSelectView.Create(Canvas());
            var cards = new List<CardData>();
            var offers = new List<CardOffer>();
            foreach (var category in new[] { CardCategory.Cocktail, CardCategory.Tea, CardCategory.Hybrid })
            {
                var card = ScriptableObject.CreateInstance<CardData>();
                created.Add(card);
                card.id = "ui_test_" + category;
                card.displayName = "테스트 " + category;
                card.description = "사거리 +15%";
                card.category = category;
                card.rarity = CardRarity.Common;
                cards.Add(card);
                offers.Add(new CardOffer { card = card, rarityPercent = 60f, currentStacks = 0 });
            }
            view.Show(offers, _ => { });

            Assert.IsNotNull(SpriteFactory.CocktailIngredientIcon, "칵테일 재료 공용 아이콘 리소스");
            Assert.IsNotNull(SpriteFactory.TeaIcon, "찻잎 공용 아이콘 리소스");
            Assert.AreSame(SpriteFactory.CocktailIngredientIcon, CardSelectView.CategoryIcon(CardCategory.Cocktail));
            Assert.AreSame(SpriteFactory.TeaIcon, CardSelectView.CategoryIcon(CardCategory.Tea));
            Assert.AreSame(ArtLibrary.Sprite("hybrid_tea_martini"), CardSelectView.CategoryIcon(CardCategory.Hybrid));

            for (int i = 0; i < offers.Count; i++)
            {
                var slot = view.transform.Find($"Card{i + 1}");
                var iconT = slot.Find("CategoryIcon");
                Assert.IsNotNull(iconT, "U2: 카드에 카테고리 도안");
                var icon = iconT.GetComponent<Image>();
                var expected = CardSelectView.CategoryIcon(cards[i].category);
                Assert.AreSame(expected, icon.sprite, $"카드 {i + 1}({cards[i].category}) 아이콘");
                Assert.AreEqual(expected != null, iconT.gameObject.activeSelf, "아이콘이 없으면 숨김, 있으면 표시");
                Assert.IsFalse(icon.raycastTarget, "아이콘이 카드 버튼 클릭을 가로채지 않는다");

                var iconRect = WorldRect(iconT);
                Assert.IsTrue(Contains(WorldRect(slot), iconRect), "아이콘은 카드 안");
                foreach (var other in new[] { "Stacks", "Odds", "Key", "Rarity", "Name" })
                    Assert.IsFalse(iconRect.Overlaps(WorldRect(slot.Find(other))), $"아이콘이 {other} 줄과 겹친다");
            }
        }

        [Test]
        public void Hud_CenterGauges_ShareDarkBacking_BehindAllThreeRows()
        {
            var hud = HudView.Create(Canvas(), null);
            var root = hud.transform;
            var backing = root.Find("GaugeBacking");
            Assert.IsNotNull(backing, "U3: 중앙 게이지 글자 뒤 공통 바탕");
            var image = backing.GetComponent<Image>();
            Assert.GreaterOrEqual(image.color.a, 0.8f, "벽등 위에서도 균일한 대비");
            Assert.Less(Luminance(image.color), 0.15f, "어두운 바탕(야간 팔레트)");
            Assert.IsFalse(image.raycastTarget, "HUD 바탕은 입력을 막지 않는다");

            var backRect = WorldRect(backing);
            foreach (var name in new[] { "GaugeBar", "TeaBar", "BeanBar" })
            {
                var bar = root.Find(name);
                Assert.IsTrue(Contains(backRect, WorldRect(bar)), $"{name} 글자 줄이 바탕 밖으로 나간다");
                Assert.Less(backing.GetSiblingIndex(), bar.GetSiblingIndex(), $"바탕이 {name}보다 뒤");
            }
            foreach (var name in new[] { "HpBar", "Info", "CoffeePoints", "PauseButton" })
                Assert.IsFalse(backRect.Overlaps(WorldRect(root.Find(name))), $"바탕이 {name}를 가린다");
        }
    }
}
