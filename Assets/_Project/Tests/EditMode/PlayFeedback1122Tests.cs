using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-11-22 주인 플레이 피드백 1차(A 카드·B 색표·C 카페·E 보스 수치) 회귀 시험. 화면 판독은 QA 투어 캡처로 따로 확인한다.</summary>
    public class PlayFeedback1122Tests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        Transform Canvas()
        {
            var go = new GameObject("C", typeof(RectTransform));
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

        // ── B(15): 능력치 색표 ──
        [Test]
        public void StatStyle_EveryUsedEffect_HasDistinctColor_LabelAndIcon()
        {
            var used = new HashSet<CardEffectType>();
            foreach (var c in CardDatabase.LoadAll()) if (c.effects != null) foreach (var e in c.effects) used.Add(e.type);
            foreach (var n in PermanentSkillDatabase.LoadAll()) if (n.effects != null) foreach (var e in n.effects) used.Add(e.type);
            foreach (var t in used) Assert.IsTrue(StatStyle.Has(t), t + " 색표 항목");
            var types = StatStyle.Types.ToList();
            for (int i = 0; i < types.Count; i++)
            {
                Assert.IsNotNull(StatStyle.Icon(types[i]), types[i] + " 아이콘(임시 아트)");
                Assert.IsFalse(string.IsNullOrEmpty(StatStyle.Get(types[i]).label), types[i] + " 이름");
                var a = StatStyle.ColorOf(types[i]);
                Assert.Greater(0.2126f * a.r + 0.7152f * a.g + 0.0722f * a.b, 0.45f, types[i] + " 어두운 카드 위에서 밝게");
                for (int j = i + 1; j < types.Count; j++)
                {
                    var b = StatStyle.ColorOf(types[j]);
                    float d = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    Assert.Greater(d, 0.18f, $"{types[i]}/{types[j]} 색 구분");
                }
            }
        }

        /// <summary>효과 줄의 수치는 기존 카드 설명 문구의 수치와 같다(새 표시가 다른 값을 말하지 않게).</summary>
        [Test]
        public void StatStyle_Values_MatchCardAndPermanentDescriptions()
        {
            var problems = new List<string>();
            var numeric = new[] { CardEffectType.DamagePercent, CardEffectType.AttackSpeedPercent, CardEffectType.RangePercent, CardEffectType.MoveSpeedPercent,
                CardEffectType.MaxHpFlat, CardEffectType.GaugeNeedPercent, CardEffectType.RegenBonus, CardEffectType.BeanGainPercent };
            foreach (var c in CardDatabase.LoadAll())
                if (c.effects != null)
                    foreach (var e in c.effects)
                        if (numeric.Contains(e.type) && !c.description.Contains(StatStyle.Value(e))) problems.Add($"{c.id}: '{c.description}' !~ {StatStyle.Value(e)}");
            foreach (var n in PermanentSkillDatabase.LoadAll())
                if (n.effects != null)
                    foreach (var e in n.effects)
                        if (numeric.Contains(e.type) && !n.description.Contains(StatStyle.Value(e, true))) problems.Add($"{n.id}: '{n.description}' !~ {StatStyle.Value(e, true)}");
            Assert.IsEmpty(problems, string.Join(" / ", problems));
            Assert.AreEqual("공격력 +15%", StatStyle.LineText(new CardEffect(CardEffectType.DamagePercent, 0.15f)));
            Assert.AreEqual("다중 발사 +1회", StatStyle.LineText(new CardEffect(CardEffectType.ExtraThrust, 1f), WeaponRestriction.RangedOnly));
            Assert.AreEqual("받는 피해 감소 +3%p", StatStyle.LineText(new CardEffect(CardEffectType.DamageReduction, 0.03f)));
        }

        // ── A(5·9·12): 효과 줄 ──
        [Test]
        public void CardSelect_EffectsAsIconColorLines_OneLinePerStatEffect_SpecialCardsKeepText()
        {
            var view = CardSelectView.Create(Canvas());
            var all = CardDatabase.LoadAll();
            var martini = all.First(c => c.id == "dry_martini");
            var gin = all.First(c => c.id == "dry_gin");
            var hybrid = ScriptableObject.CreateInstance<CardData>(); created.Add(hybrid);
            hybrid.id = "fb_special"; hybrid.displayName = "특수"; hybrid.description = "보조 활 · 자동 화살 발사"; hybrid.category = CardCategory.Hybrid; hybrid.rarity = CardRarity.Special;
            view.Show(new List<CardOffer> { new CardOffer { card = martini }, new CardOffer { card = gin }, new CardOffer { card = hybrid } }, _ => { });
            Assert.AreEqual(2, view.EffectLineCount(0), "드라이 마티니 = 공격력·공격 속도 두 줄");
            Assert.AreEqual("공격력 +25% / 공격 속도 +20%", view.EffectLines(0));
            Assert.AreEqual(1, view.EffectLineCount(1));
            Assert.AreEqual(0, view.EffectLineCount(2), "능력치 효과 없는 특수 카드는 줄 없음");
            Assert.IsTrue(view.transform.Find("Card3/Desc").GetComponent<Text>().enabled, "특수 카드는 기존 효과 글");
            Assert.IsFalse(view.transform.Find("Card1/Desc").GetComponent<Text>().enabled, "줄로 바뀐 카드는 글 숨김");
            var line = view.transform.Find("Card1/Effects/Effect0");
            Assert.IsNotNull(line.Find("Icon").GetComponent<Image>().sprite, "줄 앞 아이콘");
            Assert.AreEqual(StatStyle.ColorOf(CardEffectType.DamagePercent), line.Find("Text").GetComponent<Text>().color, "능력치 색");
            // 줄 묶음은 카드 안·이름판·지식 글과 겹치지 않는다
            var card = view.transform.Find("Card1");
            foreach (Transform row in view.transform.Find("Card1/Effects"))
            {
                var r = WorldRect(row);
                Assert.IsTrue(WorldRect(card).Contains(r.min) && WorldRect(card).Contains(r.max), row.name + " 카드 안");
                Assert.IsFalse(r.Overlaps(WorldRect(card.Find("Name"))), row.name + " 이름판 비겹침");
                Assert.IsFalse(r.Overlaps(WorldRect(card.Find("RealWorldFact"))), row.name + " 지식 글 비겹침");
            }
            Assert.IsFalse(card.Find("GameConnection").gameObject.activeSelf, "해석 줄은 카드 앞면에서 뺐다(지식 상세에 있음)");
        }

        [Test]
        public void CardSelect_AdviceTint_HiddenGold_JobColor()
        {
            string s = CardSelectView.TintAdvice("새 칵테일 종류 (러시 1/3) · 숨은 조합 재료", Job.Bartender);
            StringAssert.Contains("<color=#" + ColorUtility.ToHtmlStringRGB(StatStyle.HiddenColor) + ">숨은 조합 재료</color>", s);
            StringAssert.Contains("<color=#" + ColorUtility.ToHtmlStringRGB(StatStyle.JobColorBartender) + ">새 칵테일 종류 (러시 1/3)</color>", s);
            Assert.AreEqual("공격력 우선", CardSelectView.TintAdvice("공격력 우선", Job.Bartender), "해당 없는 문구는 그대로");
        }

        // ── B(16): 영구 강화 그림이 글보다 눈에 먼저 ──
        [Test]
        public void Permanent_IconLargerThanText_EffectsUseStatLines_NoOverlap()
        {
            var view = PermanentSkillView.Create(Canvas(), () => { });
            view.Show();
            Assert.GreaterOrEqual(PermanentSkillView.IconSize, 80f);
            Assert.LessOrEqual(PermanentSkillView.NameFont, 24);
            Assert.Greater(PermanentSkillView.IconSize, PermanentSkillView.NameFont * 3f, "그림 높이 > 이름 글자 3배");
            int lines = 0;
            foreach (var n in view.Nodes)
            {
                var node = view.transform.Find("Node_" + n.id);
                var effects = node.Find("Effects");
                if (effects == null) continue;
                lines++;
                var icon = node.Find("Icon");
                foreach (Transform row in effects)
                {
                    Assert.IsNotNull(row.Find("Icon").GetComponent<Image>().sprite, n.id + " 효과 줄 능력치 아이콘");
                    if (icon != null) Assert.IsFalse(WorldRect(row).Overlaps(WorldRect(icon)), n.id + " 효과 줄과 그림 비겹침");
                    Assert.IsFalse(WorldRect(row).Overlaps(WorldRect(node.Find("Name"))), n.id + " 효과 줄과 이름 비겹침");
                    Assert.IsFalse(WorldRect(row).Overlaps(WorldRect(node.Find("Cost"))), n.id + " 효과 줄과 비용 비겹침");
                }
            }
            Assert.AreEqual(view.Nodes.Count, lines, "모든 영구 강화가 능력치 색표 줄");
        }

        // ── C(1·3): 카페 외곽선·이름표 ──
        [Test]
        public void CafeHotspots_HaveHiddenOutlineBehindObject_AndNameplateIsClickTarget()
        {
            var view = TitleView.Show();
            try
            {
                var scene = view.Scene;
                foreach (var h in scene.Hotspots.Values)
                {
                    var id = h.Feature.Id;
                    Assert.IsNotNull(h.ObjectOutline, id + " 외곽선");
                    Assert.AreEqual(0f, h.OutlineAmount, 1e-5f, id + " 평상시 투명");
                    Assert.IsFalse(h.ObjectOutline.raycastTarget, id + " 외곽선은 클릭 판정 없음");
                    var src = scene.Interior.Find(h.ObjectLayerId);
                    Assert.AreEqual(src.GetSiblingIndex() - 1, h.ObjectOutline.transform.GetSiblingIndex(), id + " 원 레이어 바로 뒤(앞 물체가 가림)");
                    // 외곽선 픽셀은 물체 밖에만(물체 그림을 덮지 않음)
                    var a = src.GetComponent<Image>().sprite; var o = h.ObjectOutline.sprite;
                    var pa = a.texture.GetPixels32(); var po = o.texture.GetPixels32();
                    int w = (int)a.rect.width, hh = (int)a.rect.height, ox = (int)a.rect.x, oy = (int)a.rect.y, tw = a.texture.width, ring = 0;
                    for (int y = 0; y < hh; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (po[y * w + x].a == 0) continue;
                            ring++;
                            Assert.LessOrEqual(pa[(y + oy) * tw + x + ox].a, 24, id + " 외곽선이 물체 안쪽을 덮음");
                        }
                    Assert.Greater(ring, 20, id + " 외곽선 픽셀");
                    var plate = h.Nameplate.GetComponent<Image>();
                    Assert.IsTrue(plate.raycastTarget, id + " 이름표 클릭 대상");
                    Assert.AreSame(h, h.Nameplate.GetComponent<CafeNameplateTarget>().Owner, id + " 이름표 → 같은 물체");
                    h.SetPlateHover(true);
                    Assert.IsTrue(h.FocusVisible, id + " 이름표 hover = 물체 강조");
                    h.SetPlateHover(false);
                    Assert.IsFalse(h.FocusVisible);
                }
                bool clicked = false;
                var credits = scene.Hotspots["credits"];
                credits.Button.onClick.RemoveAllListeners();
                credits.Button.onClick.AddListener(() => clicked = true);
                credits.ClickFromPlate();
                Assert.IsTrue(clicked, "이름표 클릭 = 물체 버튼");
                clicked = false; credits.Button.interactable = false;
                credits.ClickFromPlate();
                Assert.IsFalse(clicked, "비활성(연출 중)이면 이름표도 안 눌림");
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        // ── E(8·11·14): 보스 체력·피해 배율표 ──
        [Test]
        public void BossTuning_SecondMidMuchTougher_AllBossesUp_FinalAtLeastTenTimesStrength()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                var s = BossDirector.DefaultSchedule(job);
                Assert.AreEqual(4, s.Count);
                for (int i = 0; i < 4; i++)
                {
                    Assert.Greater(s[i].hpScale, 1f, job + " 보스 " + (i + 1) + " 체력 상향");
                    Assert.GreaterOrEqual(s[i].attackScale, 1.2f, job + " 보스 " + (i + 1) + " 피해 상향");
                }
                Assert.GreaterOrEqual(s[1].hpScale, 4f, "두 번째 중간보스 체력 대폭(×4 이상)");
                Assert.Greater(s[1].def.hp * s[1].hpScale, s[2].def.hp * 1f, "두 번째 중간보스가 기존 강화 보스보다 단단");
                Assert.GreaterOrEqual(s[3].hpScale * s[3].attackScale, 10f, "최종보스 체력×피해 10배 이상");
                Assert.Less(s[0].def.hp * s[0].hpScale, s[1].def.hp * s[1].hpScale, "등장 순서대로 단단해짐(1<2)");
                Assert.Less(s[1].def.hp * s[1].hpScale, s[3].def.hp * s[3].hpScale, "최종이 가장 단단");
            }
            Assert.AreEqual(2500f, EnemyDef.DifficultCustomer.hp, "공유 정의 원본 불변(배율은 생성 때만)");
        }
    }
}
