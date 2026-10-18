using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>57 P1 실제 런: 첫 수집 안내 → 게이지 → 카드 창 사유, 목표 조합 지정·카드별 진행 표시·달성 시 해제,
    /// 카드 창 위 목표 바꾸기(입력 잠금), 직업·무기로 노릴 수 없는 히든 차단.</summary>
    public class ComboTargetPlayTests
    {
        string tempPath;
        GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_target_{System.Guid.NewGuid():N}", "save.json");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            try { Directory.Delete(Path.GetDirectoryName(tempPath), true); } catch { }
        }

        IEnumerator Start(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            Assert.IsNotNull(game);
            yield return null;
            game.Player.GrantInvulnerability(9999f);
            foreach (var e in Enemy.Active.ToArray()) Object.Destroy(e.gameObject);
        }

        [UnityTest]
        public IEnumerator FirstPickup_ExplainsGauge_AndCardWindowSaysWhyAndWhatNext()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            Assert.IsTrue(game.Guide.Active, "새 저장(첫 판)은 안내를 켠다");
            game.AddIngredient(IngredientType.Cocktail, 1);
            StringAssert.Contains($"칵테일 재료 1/{game.CocktailGauge.Threshold}", game.Guide.LastText);
            StringAssert.Contains("칵테일 카드 3장 중 1장", game.Guide.LastText);
            Assert.IsTrue(game.Hud.GuideVisible);

            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            yield return null;
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);
            string reason = game.CardView.ReasonLineText;
            StringAssert.Contains("칵테일 재료 게이지가 가득 차서 칵테일 카드를 고릅니다", reason);
            StringAssert.Contains($"칵테일 재료 {game.CocktailGauge.Current}/{game.CocktailGauge.Threshold}", reason);
            game.CardView.Choose(0);
            yield return null;
            StringAssert.Contains("다음 카드 창", game.Guide.LastText);
        }

        [UnityTest]
        public IEnumerator Target_ShowsPerCardProgress_IncludingNonRecommended_AndClearsWhenAchieved()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            game.SetComboTarget(HiddenSkill.FirstSip);
            StringAssert.Contains("조건 0/2", HudView.ComboTargetLabel(game));

            var gin = game.Cards.FindInPool("dry_gin");
            var green = game.Cards.FindInPool("green_tea");
            var other = game.Cards.FindInPool("molotov");
            Assert.IsNotNull(gin); Assert.IsNotNull(green); Assert.IsNotNull(other);
            CardOffer chosen = default;
            game.CardView.Show(new List<CardOffer> { new CardOffer { card = other }, new CardOffer { card = gin }, new CardOffer { card = green } },
                o => chosen = o);
            StringAssert.Contains("목표 ???", game.CardView.TargetLineText, "미발견 히든 이름은 숨김");
            StringAssert.Contains("드라이 진 0/2", game.CardView.TargetLineText, "목표 조건은 모두 공개");
            StringAssert.Contains("녹차 0/2", game.CardView.TargetLineText);
            Assert.AreEqual("", game.CardView.InfluenceText(0), "목표와 무관한 카드는 띠 없음");
            StringAssert.Contains("드라이 진 0→1/2", game.CardView.InfluenceText(1));
            StringAssert.Contains("녹차 0→1/2", game.CardView.InfluenceText(2), "추천과 무관하게 목표 카드는 표시");
            game.CardView.Choose(2);
            Assert.AreSame(green, chosen.card);

            game.GrantCardForTests(green);
            game.GrantCardForTests(green);
            game.GrantCardForTests(gin);
            Assert.AreEqual(HiddenSkill.FirstSip, game.ComboTarget);
            game.CardView.Show(new List<CardOffer> { new CardOffer { card = gin }, new CardOffer { card = other } }, _ => { });
            StringAssert.Contains("조건 완성", game.CardView.InfluenceText(0));
            game.CardView.Choose(1);
            game.GrantCardForTests(gin);
            Assert.IsTrue(game.Player.Hidden.Has(HiddenSkill.FirstSip));
            Assert.IsNull(game.ComboTarget, "달성하면 목표를 해제한다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChangeTarget_OverCardWindow_LocksCardKeys_AndRefreshes()
        {
            yield return Start(Job.Bartender, WeaponKind.Gun);
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            yield return null;
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);
            StringAssert.Contains("목표 조합 미지정", game.CardView.TargetLineText);

            game.OpenComboTarget();
            Assert.IsTrue(game.ComboTargetOpen);
            Assert.IsTrue(game.IsPaused, "목표 창이 위에 있는 동안 카드 단축키·클릭 잠금");
            game.CardView.Choose(0);
            Assert.IsTrue(game.CardView.IsOpen, "목표 창 아래 카드가 선택되면 안 된다");

            var row = game.ComboView.Rows.First(r => r.skill == HiddenSkill.PointBlank);
            Assert.IsTrue(row.button.interactable, "총은 원거리 — 근거리 산탄 가능");
            var melee = game.ComboView.Rows.First(r => r.skill == HiddenSkill.DoubleFinish);
            Assert.IsFalse(melee.button.interactable, "총으로는 근접 전용 카드가 안 나오므로 고를 수 없다");
            var tini = game.ComboView.Rows.First(r => r.skill == HiddenSkill.TiniMaster);
            Assert.IsFalse(tini.button.interactable, "티니 보우는 레이피어 시작 전용");

            game.ComboView.Pick(HiddenSkill.PointBlank);
            Assert.IsFalse(game.ComboTargetOpen);
            Assert.IsFalse(game.IsPaused);
            Assert.AreEqual(0f, Time.timeScale, "카드 창은 계속 정지");
            Assert.AreEqual(GameState.LevelUp, game.State);
            StringAssert.Contains("스플릿 베이스 0/1", game.CardView.TargetLineText);
            game.CardView.Choose(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeaHouse_ListsOnlyTeaReachableHidden_AndEscCloses()
        {
            yield return Start(Job.TeaSommelier, WeaponKind.Sword);
            game.OpenComboTarget();
            Assert.IsTrue(game.ComboTargetOpen);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(game.ComboView.Rows.Any(r => r.skill == HiddenSkill.HotToddy), "바텐더 전용은 목록에 없다");
            Assert.IsTrue(game.ComboView.Rows.Any(r => r.skill == HiddenSkill.FirstSip));
            Assert.IsFalse(game.ComboView.Rows.First(r => r.skill == HiddenSkill.PointBlank).button.interactable, "검은 원거리 전용 카드 불가");
            game.RequestEscape();
            Assert.IsFalse(game.ComboTargetOpen);
            Assert.AreEqual(1f, Time.timeScale);
            StringAssert.Contains("목표 조합 정하기", HudView.ComboTargetLabel(game));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeaHouse_FieldDrops_UseNewDropArt_IncludingCoffee()
        {
            yield return Start(Job.TeaSommelier, WeaponKind.Bow);
            foreach (var t in new[] { IngredientType.Tea, IngredientType.Coffee })
            {
                IngredientPickup.Spawn(game, game.Player, game.Player.Position + new Vector2(8f, 0f), t, 1, game.transform);
                var sprites = Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)
                    .Select(p => p.GetComponent<SpriteRenderer>().sprite).ToList();
                Assert.IsNotNull(IngredientPickup.DropArt(t));
                Assert.Contains(IngredientPickup.DropArt(t), sprites, $"찻집 {t} 드롭은 새 drop 아트");
            }
            yield return null;
        }
    }
}
