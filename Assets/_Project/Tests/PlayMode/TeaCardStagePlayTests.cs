using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>#19(2026-11-24) 실제 카드 선택 경로(GrantCardForTests = ChooseCard와 같은 TakeCard)에서 단계 줄 적용, 바텐더 무변화,
    /// 히든 조건·최종 형태·깊은 우림은 중첩 수 그대로, 카드 창 선택 전 공개, 도감은 발견 카드 상세에서만.</summary>
    public class TeaCardStagePlayTests
    {
        string tempPath;
        GameManager game;
        readonly List<GameObject> created = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_teastage_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var o in created) if (o != null) Object.Destroy(o);
            created.Clear();
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            foreach (var s in new[] { "", ".bak", ".tmp" }) if (File.Exists(tempPath + s)) File.Delete(tempPath + s);
        }

        IEnumerator Build(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            yield return null;
        }

        static CardData Card(string id) => CardDatabase.LoadAll().First(c => c.id == id);

        [UnityTest]
        public IEnumerator TeaSommelier_GreenTeaToMax_BaseEveryPickPlusEachStageOnce()
        {
            yield return Build(Job.TeaSommelier, WeaponKind.Sword);
            var s = game.Stats;
            float dmg0 = s.damagePercent, spd0 = s.attackSpeedPercent, pick0 = s.pickupRadiusFlat, gauge0 = s.gaugeNeedPercent;
            var green = Card("green_tea");
            for (int i = 0; i < green.maxStacks; i++) game.GrantCardForTests(green);
            Assert.AreEqual(green.maxStacks, game.Cards.StacksOf("green_tea"));
            Assert.AreEqual(dmg0 + 0.15f * 5 + 0.10f, s.damagePercent, 1e-5f, "기본 +15%×5 그대로 + 3회 핵심 +10%");
            Assert.AreEqual(spd0 + 0.08f, s.attackSpeedPercent, 1e-5f, "2회 숨은 +8%");
            Assert.AreEqual(pick0 + 0.3f, s.pickupRadiusFlat, 1e-5f, "4회 유틸");
            Assert.AreEqual(gauge0 - 0.04f, s.gaugeNeedPercent, 1e-5f, "5회 유틸");
            game.GrantCardForTests(green); // 상한: 아무것도 늘지 않음
            Assert.AreEqual(dmg0 + 0.85f, s.damagePercent, 1e-5f);
            Assert.AreEqual(4, game.DeepSteepLevel, "깊은 우림은 중첩 수 기준 그대로");
            Assert.IsTrue(game.Player.Hidden.Has(HiddenSkill.FirstSip), "찻집 히든 조건(녹차 4)도 중첩 수 그대로");
        }

        [UnityTest]
        public IEnumerator Bartender_SameTeaCardToMax_NoStageLines()
        {
            yield return Build(Job.Bartender, WeaponKind.Rapier);
            var s = game.Stats;
            float dmg0 = s.damagePercent, spd0 = s.attackSpeedPercent, pick0 = s.pickupRadiusFlat, gauge0 = s.gaugeNeedPercent;
            var green = Card("green_tea");
            for (int i = 0; i < green.maxStacks; i++) game.GrantCardForTests(green);
            Assert.AreEqual(dmg0 + 0.75f, s.damagePercent, 1e-5f, "바텐더 = 기본 효과만");
            Assert.AreEqual(spd0, s.attackSpeedPercent, 1e-5f);
            Assert.AreEqual(pick0, s.pickupRadiusFlat, 1e-5f);
            Assert.AreEqual(gauge0, s.gaugeNeedPercent, 1e-5f);
        }

        [UnityTest]
        public IEnumerator FinalFormCard_StageLinesDoNotChangeFinalFormTiming()
        {
            yield return Build(Job.TeaSommelier, WeaponKind.Sword);
            var c = Card("second_infusion");
            game.GrantCardForTests(c); game.GrantCardForTests(c);
            Assert.IsFalse(game.Player.FinalForms.Has(AttackFinalForm.Chain));
            int thrusts = game.Stats.extraThrusts;
            game.GrantCardForTests(c);
            Assert.IsTrue(game.Player.FinalForms.Has(AttackFinalForm.Chain), "최종 형태는 기존대로 3회");
            Assert.AreEqual(thrusts + 1, game.Stats.extraThrusts, "단계 줄은 연속 타격을 바꾸지 않음(기본 +1만)");
        }

        [UnityTest]
        public IEnumerator CardSelect_ShowsNextStageBeforeChoosing_TeaOnly()
        {
            yield return Build(Job.TeaSommelier, WeaponKind.Sword);
            var canvas = new GameObject("C", typeof(RectTransform)); created.Add(canvas);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
            var view = CardSelectView.Create(canvas.transform);
            var green = Card("green_tea"); var oolong = Card("oolong"); var puerh = Card("aged_puerh");
            view.Show(new List<CardOffer>
            {
                new CardOffer { card = green, currentStacks = 0 },
                new CardOffer { card = oolong, currentStacks = 1 },
                new CardOffer { card = puerh, currentStacks = 2 },
            }, _ => { });
            Assert.AreEqual("", view.StageLine(0), "1회째 = 기본만");
            Assert.AreEqual(TeaCardStages.NextLine(Job.TeaSommelier, oolong, 1), view.StageLine(1));
            StringAssert.StartsWith("2회 숨은 효과", view.StageLine(1));
            StringAssert.StartsWith("3회 핵심 효과", view.StageLine(2));
            Assert.IsNotNull(view.transform.Find("Card2/Effects/TeaStage"), "단계 줄 칸");
            Assert.AreEqual(1, view.EffectLineCount(1), "기본 효과 줄 수는 그대로");
            yield return null;

            Object.Destroy(game.gameObject); yield return null;
            yield return Build(Job.Bartender, WeaponKind.Rapier);
            view.Show(new List<CardOffer> { new CardOffer { card = oolong, currentStacks = 1 } }, _ => { });
            Assert.AreEqual("", view.StageLine(0), "바텐더 = 단계 줄 없음");
            Assert.IsNull(view.transform.Find("Card1/Effects/TeaStage"));
        }

        [UnityTest]
        public IEnumerator Codex_StagesOnlyInDiscoveredTeaCardDetail_HiddenCodexUntouched()
        {
            var canvas = new GameObject("C", typeof(RectTransform)); created.Add(canvas);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
            var codex = CardCodexView.Create(canvas.transform, null);
            codex.Show();
            int hiddenFound = codex.HiddenFoundForTests;
            Assert.IsTrue(codex.OpenDetailFor("oolong"));
            StringAssert.DoesNotContain("획득 단계", codex.DetailBodyText, "미발견 카드는 단계 비공개");
            codex.HideDetail();
            PermanentProgress.DiscoverCard("oolong");
            codex.Show();
            Assert.IsTrue(codex.OpenDetailFor("oolong"));
            StringAssert.Contains("티 소믈리에 획득 단계", codex.DetailBodyText);
            foreach (var s in TeaCardStages.For("oolong")) StringAssert.Contains(TeaCardStages.Line(s), codex.DetailBodyText);
            codex.HideDetail();
            Assert.AreEqual(hiddenFound, codex.HiddenFoundForTests, "히든 스킬 도감 집계 불변");
            yield return null;
        }
    }
}
