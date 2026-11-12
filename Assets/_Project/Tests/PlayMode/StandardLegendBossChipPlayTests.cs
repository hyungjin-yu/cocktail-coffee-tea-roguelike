using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-12 정규 모드 범례: 비보스 = 큰 그림 줄(StandardH), 보스전 = 범례 접고 오른쪽 아래 상성 칩(마신 잔 → 대상 계열),
    /// 칩은 보스 체력바와 겹치지 않고, 보스가 죽으면 범례가 돌아온다.</summary>
    public class StandardLegendBossChipPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            GameManager.Instance.Player.GrantInvulnerability(9999f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        static Rect Screen(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static RectTransform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true)) if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator BossFight_CollapsesLegend_ShowsAffinityChip_NotOverBossBar_ThenRestores()
        {
            var game = GameManager.Instance;
            Assert.IsFalse(game.IsEndless, "정규 모드");
            var legend = game.Hud.Legend;
            Assert.AreEqual(FamilyLegend.StandardH, ((RectTransform)legend.transform).sizeDelta.y, 0.01f, "정규 비보스 범례는 확대 높이");
            game.Player.DrunkDrink = DrinkKind.Cocktail;
            legend.RefreshForTests();
            yield return null;
            Assert.IsTrue(legend.LegendVisible, "비보스: 범례 보임");
            Assert.IsFalse(legend.BossChipVisible, "비보스: 칩 숨김");
            Assert.IsTrue(legend.TargetIconShown, "마신 칵테일 → 대상 계열 그림");
            foreach (var f in new[] { MonsterFamily.Cocktail, MonsterFamily.Coffee, MonsterFamily.Tea })
            {
                var def = HudIcons.FamilyRepresentative(f);
                var src = MasterWavePlan.IconOf(def);
                var shown = HudIcons.Family(f);
                Assert.IsNotNull(shown, f + " 대표 그림");
                Assert.AreSame(src.texture, shown.texture, "같은 원본 텍스처(새 아트 아님)");
                var cn = ArtLibrary.Get(def.ArtId).contentRectNormalized;
                if (cn.width < 0.95f || cn.height < 0.95f)
                    Assert.Less(shown.rect.width * shown.rect.height, src.rect.width * src.rect.height * 0.8f, f + " 투명 여백을 잘라 칸을 채운다");
            }

            var boss = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.GrinderGolem, hpScale = 50f });
            yield return null;
            legend.RefreshForTests();
            yield return null;
            Assert.IsFalse(legend.LegendVisible, "보스전: 범례 접힘(보스 체력바 자리)");
            Assert.IsTrue(legend.BossChipVisible, "보스전: 상성 칩");
            StringAssert.Contains("+" + DrinkAffinity.BonusPercent + "%", legend.BossChipText);
            var bar = Find(legend.transform.parent, "BossBar");
            Assert.IsNotNull(bar);
            Assert.IsTrue(bar.gameObject.activeInHierarchy, "보스 체력바 표시");
            Assert.IsFalse(Screen(legend.BossChipRect).Overlaps(Screen(bar)), "칩이 보스 체력바를 가리면 안 된다");

            // 보스 등장 현수막: 제목 한 줄, 제목·부제가 왼쪽 패널(히든·직업·목표)과 겹치지 않는다.
            var hudRoot = legend.transform.parent;
            var title = Find(hudRoot, "BannerTitle"); var sub = Find(hudRoot, "BannerSub");
            Assert.IsFalse(string.IsNullOrEmpty(game.Hud.BannerSubText), "보스 현수막 부제");
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(1, title.GetComponent<UnityEngine.UI.Text>().cachedTextGenerator.lineCount, "현수막 제목은 한 줄(크기 맞춤)");
            foreach (var panel in new[] { "HiddenSkillsButton", "JobTrait", "ComboTargetButton" })
            {
                var pr = Screen(Find(hudRoot, panel));
                Assert.IsFalse(Screen(title).Overlaps(pr), "현수막 제목이 " + panel + "와 겹침");
                Assert.IsFalse(Screen(sub).Overlaps(pr), "현수막 부제가 " + panel + "와 겹침");
            }

            game.Player.DrunkDrink = null;
            legend.RefreshForTests();
            StringAssert.Contains("마신 음료 없음", legend.BossChipText);

            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.2f); // 등장 무적 끝
            for (int i = 0; i < 20 && boss != null && !boss.IsDead; i++) { boss.TakeDamage(1e9f); yield return null; }
            Assert.IsTrue(boss == null || boss.IsDead, "보스 처치");
            for (int i = 0; i < 40 && game.State != GameState.Playing; i++)
            {
                if (game.CardView.IsLearningOpen) game.CardView.CloseLearning();
                else if (game.CardView.IsOpen) game.CardView.Choose(0);
                else if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen) game.SkillView.Continue();
                else if (game.HybridView.IsOpen) game.HybridView.Unlock();
                yield return null;
            }
            legend.RefreshForTests();
            yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.IsTrue(legend.LegendVisible, $"보스 처치 후 범례 복귀 (activeBoss={(game.ActiveBoss != null)})");
            Assert.IsFalse(legend.BossChipVisible);
        }
    }
}
