using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>#19(2026-11-24) 티 소믈리에 카드 획득 단계 표: 실제 카드 데이터·상한과 1:1, 직업 분리, 금지 능력치 없음.</summary>
    public class TeaCardStageTests
    {
        static readonly HashSet<CardEffectType> Forbidden = new HashSet<CardEffectType>
        {
            // 무기·최종 형태와 얽히는 능력치, 런 중 가산이 반영되지 않는 원두 획득은 단계 줄에 쓰지 않는다.
            CardEffectType.ExtraThrust, CardEffectType.PierceCount, CardEffectType.FlameLevel, CardEffectType.PoisonLevel, CardEffectType.BeanGainPercent,
        };

        [Test]
        public void EveryTeaCard_HasOneLinePerStage_2ToItsRealMaxStacks_KindsMatchRule()
        {
            // #21 속성 화살 4종(기본 활 전용 차 카드)은 자체 레벨 규칙(ArrowElementRules)이라 18종 단계표 대상이 아니다.
            var tea = CardDatabase.LoadAll().Where(c => c.category == CardCategory.Tea && !(c.effects != null && c.effects.Any(e => e.type >= CardEffectType.ArrowBurn && e.type <= CardEffectType.ArrowPoison))).ToList();
            Assert.AreEqual(18, tea.Count, "차 카드 18종");
            CollectionAssert.AreEquivalent(tea.Select(c => c.id), TeaCardStages.CardIds, "표 = 차 카드 전체(칵테일·하이브리드 없음)");
            foreach (var card in tea)
            {
                var stages = TeaCardStages.For(card.id);
                CollectionAssert.AreEqual(Enumerable.Range(2, card.maxStacks - 1).ToArray(), stages.Select(s => s.stack).ToArray(),
                    $"{card.id}: 2..{card.maxStacks}회 한 줄씩(기존 상한 그대로)");
                foreach (var s in stages)
                {
                    var want = s.stack == 2 ? TeaStageKind.Hidden : s.stack == 3 ? TeaStageKind.Core : TeaStageKind.Utility;
                    Assert.AreEqual(want, s.kind, $"{card.id} {s.stack}회");
                    Assert.IsFalse(Forbidden.Contains(s.effect.type), $"{card.id} {s.stack}회: 금지 능력치 {s.effect.type}");
                    Assert.IsTrue(StatStyle.Has(s.effect.type), "표시 가능한 능력치");
                    Assert.AreNotEqual(0f, s.effect.value);
                }
                Assert.IsFalse(TeaCardStages.TryGet(card.id, 1, out _), "1회 = 기본 효과만");
                Assert.IsFalse(TeaCardStages.TryGet(card.id, card.maxStacks + 1, out _), "상한 밖 없음");
            }
        }

        [Test]
        public void Applies_TeaSommelierTeaCardOnly_BartenderAndCocktailNever()
        {
            var all = CardDatabase.LoadAll();
            var green = all.First(c => c.id == "green_tea");
            var gin = all.First(c => c.id == "dry_gin");
            Assert.IsTrue(TeaCardStages.Applies(Job.TeaSommelier, green));
            Assert.IsFalse(TeaCardStages.Applies(Job.Bartender, green), "바텐더 무변화");
            Assert.IsFalse(TeaCardStages.Applies(Job.TeaSommelier, gin), "칵테일 카드 없음");
            Assert.AreEqual("", TeaCardStages.NextLine(Job.Bartender, green, 1));
            Assert.AreEqual("", TeaCardStages.NextLine(Job.TeaSommelier, green, 0), "1회째는 기본 효과만");
            Assert.AreEqual("2회 숨은 효과 · 공격 속도 +8%", TeaCardStages.NextLine(Job.TeaSommelier, green, 1));
            Assert.AreEqual("3회 핵심 효과 · 공격력 +10%", TeaCardStages.NextLine(Job.TeaSommelier, green, 2));
            StringAssert.StartsWith("4회 유틸 효과 · ", TeaCardStages.NextLine(Job.TeaSommelier, green, 3));
            StringAssert.StartsWith("5회 유틸 효과 · ", TeaCardStages.NextLine(Job.TeaSommelier, green, 4));
            Assert.AreEqual("", TeaCardStages.NextLine(Job.TeaSommelier, green, 5), "상한");
        }

        [Test]
        public void ApplyOnTake_AddsExactlyThatStageOnce_BaseStatsUntouched()
        {
            var green = CardDatabase.LoadAll().First(c => c.id == "green_tea");
            var s = new PlayerStats();
            Assert.IsFalse(TeaCardStages.ApplyOnTake(Job.TeaSommelier, green, 1, s));
            Assert.IsTrue(TeaCardStages.ApplyOnTake(Job.TeaSommelier, green, 2, s));
            Assert.AreEqual(0.08f, s.attackSpeedPercent, 1e-6f);
            Assert.AreEqual(0f, s.damagePercent, 1e-6f, "기본 효과는 CardSystem.Take가 따로");
            Assert.IsFalse(TeaCardStages.ApplyOnTake(Job.Bartender, green, 3, s));
            Assert.AreEqual(0f, s.damagePercent, 1e-6f);
        }

        [Test]
        public void DamageReductionLines_SumWithinExistingCap()
        {
            float sum = TeaCardStages.CardIds.SelectMany(TeaCardStages.For)
                .Where(s => s.effect.type == CardEffectType.DamageReduction).Sum(s => s.effect.value);
            Assert.LessOrEqual(sum, 0.25f + 1e-6f, "모두 모아도 기존 받는 피해 감소 상한 25% 이내");
        }

        [Test]
        public void CodexText_TeaOnly_ListsEveryStage()
        {
            var all = CardDatabase.LoadAll();
            var oolong = all.First(c => c.id == "oolong");
            string text = TeaCardStages.CodexText(oolong);
            StringAssert.Contains("1회 기본 효과", text);
            foreach (var s in TeaCardStages.For("oolong")) StringAssert.Contains(TeaCardStages.Line(s), text);
            Assert.AreEqual("", TeaCardStages.CodexText(all.First(c => c.id == "dry_gin")));
        }
    }
}
