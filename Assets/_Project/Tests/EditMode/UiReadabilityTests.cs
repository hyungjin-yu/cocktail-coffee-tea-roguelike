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
            var root = view.transform.Find("Content"); // 2026-11-22 메뉴판 건네기 때 한 묶음(CanvasGroup)으로 감싼 내용
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
                foreach (var other in new[] { "Stacks", "Odds", "Rarity", "Name" })
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

        /// <summary>UI 가독성 감사 E-3: 세 게이지 바는 서로 겹치지 않고, 글자는 바 높이 안에서만 줄어든다(바 밖으로 넘치지 않음).
        /// 안내 띠(Guide)는 게이지 바탕 아래에서 시작한다.</summary>
        [Test]
        public void Hud_CenterGauges_DoNotOverlap_LabelsFitInsideBars()
        {
            var hud = HudView.Create(Canvas(), null);
            var root = hud.transform;
            var names = new[] { "GaugeBar", "TeaBar", "BeanBar" };
            for (int i = 0; i < names.Length; i++)
            {
                var bar = root.Find(names[i]);
                Assert.GreaterOrEqual(((RectTransform)bar).sizeDelta.y, 28f, $"{names[i]} 높이(20pt 글자가 들어가는 칸)");
                var label = bar.Find("Label").GetComponent<Text>();
                Assert.IsTrue(label.resizeTextForBestFit, $"{names[i]} 글자는 바 안에서 줄인다");
                Assert.AreEqual(VerticalWrapMode.Truncate, label.verticalOverflow, $"{names[i]} 글자가 바 밖으로 넘치지 않는다");
                for (int j = i + 1; j < names.Length; j++)
                    Assert.IsFalse(WorldRect(bar).Overlaps(WorldRect(root.Find(names[j]))), $"{names[i]}와 {names[j]} 겹침");
            }
            var guide = root.Find("Guide");
            Assert.LessOrEqual(WorldRect(guide).yMax, WorldRect(root.Find("GaugeBacking")).yMin + 0.01f, "안내 띠는 게이지 바탕 아래");
        }

        /// <summary>감사 E-5/S-1: 상태 줄은 전체 명칭 + 실제 감소율(부호 -, 정수 %) + 괄호 안 남은 초(올림).</summary>
        [Test]
        public void Hud_DebuffLine_FullNames_RateAndSecondsSeparated()
        {
            Assert.AreEqual("", HudView.DebuffLine(false, 0f, false, 1f, 0f, false, 1f, 0f, false, 0f));
            Assert.AreEqual("공격 속도 -25% (3초) · 회복 차단 (5초)",
                HudView.DebuffLine(false, 0f, false, 1f, 0f, true, 0.75f, 2.2f, true, 4.1f));
            Assert.AreEqual("환불: % 보너스 무효 (2초) · 이동 속도 -30% (3초)",
                HudView.DebuffLine(true, 1.5f, true, 0.7f, 3f, false, 1f, 0f, false, 0f));
            StringAssert.Contains("이동 속도 느려짐 (1초)", HudView.DebuffLine(false, 0f, true, 0.999f, 0.5f, false, 1f, 0f, false, 0f),
                "0%로 반올림되는 둔화는 0%를 쓰지 않는다");
        }

        /// <summary>감사 S-10/P5: "현재 → 선택 후"는 최종값이 단순 가산으로 바로 계산되는 능력치만. 시간제 보정이 섞이는 능력치는 빈 글(추정 금지).</summary>
        [Test]
        public void StatStyle_Preview_OnlyForExactStats_UsesRealFinalValues()
        {
            var s = new PlayerStats();
            Assert.AreEqual("현재 100 → 선택 후 120", StatStyle.Preview(new CardEffect(CardEffectType.MaxHpFlat, 20f), s));
            Assert.AreEqual("필요량 현재 100% → 선택 후 92%", StatStyle.Preview(new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f), s));
            Assert.AreEqual("확률 현재 0% → 선택 후 10%", StatStyle.Preview(new CardEffect(CardEffectType.DropChancePercent, 0.10f), s));
            s.Apply(new CardEffect(CardEffectType.GaugeNeedPercent, -0.48f));
            Assert.AreEqual("필요량 현재 52% → 선택 후 50%", StatStyle.Preview(new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f), s), "하한 50% 그대로");
            s.Apply(new CardEffect(CardEffectType.DamageReduction, 0.2f));
            Assert.AreEqual("현재 20% → 선택 후 25% (최대 25%)", StatStyle.Preview(new CardEffect(CardEffectType.DamageReduction, 0.1f), s), "상한 25% 그대로");
            Assert.AreEqual("현재 250cm → 선택 후 300cm", StatStyle.Preview(new CardEffect(CardEffectType.PickupRadiusFlat, 0.5f), new PlayerStats()));
            foreach (var t in new[] { CardEffectType.DamagePercent, CardEffectType.AttackSpeedPercent, CardEffectType.RangePercent, CardEffectType.MoveSpeedPercent,
                         CardEffectType.RegenBonus, CardEffectType.FlameLevel, CardEffectType.PoisonLevel, CardEffectType.PierceCount, CardEffectType.ExtraThrust })
                Assert.AreEqual("", StatStyle.Preview(new CardEffect(t, 1f), s), $"{t}: 시간제·무기별 보정이 섞여 미리보기 없음");
            Assert.AreEqual("", StatStyle.Preview(new CardEffect(CardEffectType.MaxHpFlat, 20f), null));
        }

        /// <summary>감사 S-11: 조사 괄호("을(를)", "(으)로")를 받침으로 고른다. 판정 못 하는 끝 글자(영문)는 괄호 표기 유지.</summary>
        [Test]
        public void KoreanParticle_PicksByFinalConsonant()
        {
            Assert.AreEqual("칵테일을", KoreanParticle.EulReul("칵테일"));
            Assert.AreEqual("차를", KoreanParticle.EulReul("차"));
            Assert.AreEqual("커피를", KoreanParticle.EulReul("커피"));
            Assert.AreEqual("창 모드로", KoreanParticle.EuRo("창 모드"));
            Assert.AreEqual("전체 화면으로", KoreanParticle.EuRo("전체 화면"));
            Assert.AreEqual("레일로", KoreanParticle.EuRo("레일"), "ㄹ 받침은 로");
            Assert.AreEqual("설탕이", KoreanParticle.IGa("설탕"));
            Assert.AreEqual("우유는", KoreanParticle.EunNeun("우유"));
            Assert.AreEqual("모카포트 3을", KoreanParticle.EulReul("모카포트 3"));
            Assert.AreEqual("IBA(으)로", KoreanParticle.EuRo("IBA"), "영문 끝은 판정 불가 → 괄호 유지");
            Assert.AreEqual("을(를)", KoreanParticle.EulReulOf(null));
        }
    }
}
