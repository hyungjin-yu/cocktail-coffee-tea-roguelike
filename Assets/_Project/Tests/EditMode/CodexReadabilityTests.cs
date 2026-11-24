using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-24 도감 가독성 정리: 상단 집계 구분, 재료 종류·누적량 우선, 효과 풀어쓰기, 등급/획득 분리, 영구 강화 상태 분리, 미발견 보호.
    /// 임시 저장 파일로만 검사한다(실제 저장은 건드리지 않음).
    /// </summary>
    public class CodexReadabilityTests : SettingsV2Fixture
    {
        GameObject canvas;

        [TearDown]
        public void DestroyUi()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        CardCodexView Open(string saveJson)
        {
            File.WriteAllText(savePath, saveJson);
            PermanentProgress.SetPathForTests(savePath);
            canvas = new GameObject("CodexCanvas", typeof(RectTransform));
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            return codex;
        }

        [Test]
        public void CountLine_NamesEachCount_DiscoveryVersusUnlock()
        {
            var codex = Open("{\"schemaVersion\":2,\"discoveredCardIds\":[\"liqueur\"],\"unlockedPermanentSkillIds\":[\"dark_roast_blend\"]}");
            StringAssert.Contains("카드 발견 1/", codex.CountText);
            StringAssert.Contains("히든 스킬 발견 0/", codex.CountText);
            StringAssert.Contains("직업 연구 해금 기록 0/15", codex.CountText);
            StringAssert.Contains("영구 강화 해금 1/", codex.CountText);
            StringAssert.DoesNotContain("최초 획득", codex.CountText);
        }

        [Test]
        public void MaterialRow_KindsAndTotalFirst_NamesPreviewedAndFullListInDetail_UndiscoveredStayHidden()
        {
            var codex = Open("{\"schemaVersion\":2,\"cocktailIngredientsCollected\":3038,\"discoveredMaterialVarietyIds\":[\"gin\",\"rum\"]}");
            var (header, names) = codex.GetMaterialText(IngredientType.Cocktail);
            Assert.AreEqual("칵테일 재료 2/6종 · 누적 3038개", header);
            StringAssert.Contains("진", names);
            StringAssert.Contains("럼", names);
            StringAssert.Contains("미발견 4종", names);
            StringAssert.DoesNotContain("보드카", names);
            StringAssert.DoesNotContain("???", names, "줄에는 ??? 반복 대신 미발견 N종");

            Assert.IsTrue(codex.OpenMaterialDetailForTests(IngredientType.Cocktail));
            Assert.IsTrue(codex.IsDetailOpen);
            StringAssert.Contains("종류 2/6종 발견", codex.DetailBodyText);
            StringAssert.Contains("· 진 — ", codex.DetailBodyText);
            StringAssert.DoesNotContain("보드카", codex.DetailBodyText, "미발견 품종 이름은 상세에서도 숨김");
            StringAssert.Contains("??? — 미발견", codex.DetailBodyText);
        }

        [Test]
        public void MaterialRow_ManyDiscovered_ShortensNamesWithRemainderCount()
        {
            var codex = Open("{\"schemaVersion\":2,\"discoveredMaterialVarietyIds\":[\"ethiopia\",\"colombia\",\"brazil\",\"guatemala\",\"kenya\",\"yirgacheffe\"]}");
            var (header, names) = codex.GetMaterialText(IngredientType.Coffee);
            StringAssert.StartsWith("커피 원두 6/6종", header);
            StringAssert.Contains("외 ", names);
            StringAssert.DoesNotContain("예가체프", names, "긴 이름 전부 나열하지 않음(상세에서 확인)");
        }

        [Test]
        public void CardRow_EffectSpelledOut_RarityAndStateSeparate_LockedNotDuplicated()
        {
            var codex = Open("{\"schemaVersion\":2,\"discoveredCardIds\":[\"liqueur\"]}");
            var liqueur = codex.GetCardText("liqueur");
            Assert.AreEqual("게이지를 채우는 데 필요한 재료 -8%", liqueur.desc);
            var meta = codex.GetCardMeta("liqueur");
            Assert.AreEqual("등급 일반", meta.rarity);
            Assert.AreEqual("발견함", meta.status);

            var locked = codex.GetCardText("dry_gin");
            Assert.AreEqual("???", locked.name);
            Assert.AreEqual(CardCodexView.LockedHint, locked.desc);
            var lockedMeta = codex.GetCardMeta("dry_gin");
            Assert.AreEqual("칵테일 카드", lockedMeta.rarity);
            Assert.AreEqual("", lockedMeta.status);
            StringAssert.DoesNotContain("미발견", lockedMeta.rarity, "그림 위 미발견 칩과 중복하지 않음");
            StringAssert.DoesNotContain("드라이", locked.desc, "미발견 카드 효과·이름 비공개");
        }

        [Test]
        public void GaugeCards_BothJobs_UseSameSpelledOutEffect_FromCardValue()
        {
            var cards = CardDatabase.LoadAll().Where(c => c.effects != null && c.effects.Length == 1 && c.effects[0].type == CardEffectType.GaugeNeedPercent).ToList();
            Assert.GreaterOrEqual(cards.Count, 2, "리큐르·티스푼 계량");
            foreach (var c in cards)
                Assert.AreEqual("게이지를 채우는 데 필요한 재료 " + StatStyle.Value(c.effects[0]), CardCodexView.EffectText(c));
        }

        [Test]
        public void Permanent_EffectAndUnlockState_AreSeparateLines()
        {
            var codex = Open("{\"schemaVersion\":2,\"unlockedPermanentSkillIds\":[\"dark_roast_blend\"]}");
            var on = codex.GetPermanentText("dark_roast_blend").Split('\n');
            Assert.AreEqual(2, on.Length);
            StringAssert.Contains("영구 공격력 +10%", on[0]);
            Assert.AreEqual("해금됨", on[1]);
            var off = codex.GetPermanentText("perfect_crema").Split('\n');
            StringAssert.StartsWith("영구 공격 속도 +10%", off[0]);
            StringAssert.DoesNotContain("미해금", off[0]);
            StringAssert.StartsWith("미해금 · 비용 ", off[1]);
        }

        [Test]
        public void Coffee_WithArt_EffectLineHasNoStatus_StateIsOnChip()
        {
            var codex = Open("{\"schemaVersion\":2}");
            var first = SkillDatabase.LoadAll().OrderBy(s => s.branch).ThenBy(s => s.tier).First();
            var t = codex.GetCoffeeText(first.id);
            StringAssert.Contains(CardCodexView.CoffeeNotAcquiredLabel, t.desc);
            StringAssert.DoesNotContain("미해금", t.desc);
        }
    }
}
