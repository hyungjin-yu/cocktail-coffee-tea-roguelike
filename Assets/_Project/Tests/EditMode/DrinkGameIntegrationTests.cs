using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>69 실제 GameManager(Initialize 전체)와 음료 수명주기: 입장 1회 적용, 일시정지·카드 선택 중 재적용 없음,
    /// 0~4/5킬 포기, 보스 음료 재료 → 결과 화면, 정상 앱 종료, 치명타·재사용 감소의 실제 전투 효과.</summary>
    public class DrinkGameIntegrationTests
    {
        string dir;
        GameObject root, createdCamera;
        GameManager game;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "drinkgame_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            DrinkStore.FaultHookForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (createdCamera != null) UnityEngine.Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
            DrinkStore.FaultHookForTests = null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        static DrinkRecipe HpAtkDrink()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Stir;
            r.name = "통합";
            r.steps.Add(new DrinkStep("gin", 300));
            r.steps.Add(new DrinkStep("grenadine", 100));
            r.steps.Add(new DrinkStep("rum", 150));
            return r;
        }

        void StartGame(bool withDrink, DrinkRecipe recipe = null)
        {
            if (withDrink)
            {
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(recipe ?? HpAtkDrink()).Status);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
        }

        static readonly EnemyDef Mob = new EnemyDef { name = "테스트 손님", dropCount = 0, hp = 10f };
        static readonly EnemyDef MidBoss = new EnemyDef { name = "테스트 중간 보스", dropCount = 0, hp = 10f, boss = BossKind.Mid };

        void Kill(int n) { for (int i = 0; i < n; i++) game.OnEnemyKilled(Vector2.zero, Mob); }

        [Test]
        public void Buff_AppliedOnceAtEntry_MatchesPreview_NotReappliedByPauseOrCards()
        {
            var recipe = HpAtkDrink();
            var preview = DrinkCalculator.Buff(recipe);
            StartGame(true, recipe);
            Assert.IsNotNull(game.DrinkBuff);
            Assert.AreEqual(preview.Describe(), game.DrinkBuff.Describe(), "미리보기와 실제 적용이 같은 계산기");
            var plain = new PlayerStats();
            Assert.AreEqual(plain.baseMaxHp * (1f + preview[DrinkStat.MaxHp]), game.Stats.MaxHp, 1e-3f);
            Assert.AreEqual(game.Stats.MaxHp, game.Player.Hp, 1e-3f, "늘어난 최대 체력으로 시작");
            float damage = game.Stats.Damage, hp = game.Stats.MaxHp, interval = game.Stats.AttackInterval;
            Assert.Greater(damage, plain.Damage);
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.LevelUp, game.State);
            game.OpenPause(); game.ClosePause(); // 카드 선택 위 일시정지
            Assert.AreEqual(hp, game.Stats.MaxHp, 1e-3f, "일시정지가 음료를 다시 적용하지 않음");
            Assert.AreEqual(damage, game.Stats.Damage, 1e-4f);
            game.OpenPause(); game.ClosePause();
            Assert.AreEqual(hp, game.Stats.MaxHp, 1e-3f);
            Assert.AreEqual(1, CountDrinkApplications(), "음료 상태는 이번 런 InRun 하나");
            Assert.AreEqual(PreparedDrink.InRun, DrinkLab.Prepared.state);
            Assert.AreEqual(game.RunId, DrinkLab.Prepared.runId, "출발 표가 실제 runId로 바뀜");
            Assert.LessOrEqual(interval, plain.AttackInterval);
        }

        static int CountDrinkApplications() => DrinkLab.Prepared != null && DrinkLab.Prepared.state == PreparedDrink.InRun ? 1 : 0;

        [TestCase(0, "Preserved")] [TestCase(4, "Preserved")] [TestCase(5, "Consumed")] [TestCase(9, "Consumed")]
        public void GiveUp_KillBoundary(int kills, string expected)
        {
            StartGame(true);
            Kill(kills);
            game.OpenPause();
            game.GiveUp();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(expected, game.DrinkSettlement.drink);
            if (expected == "Preserved")
            {
                Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state);
                StringAssert.Contains("보존", ResultView.DrinkStatusLine(game.DrinkSettlement));
            }
            else Assert.IsNull(DrinkLab.Prepared);
        }

        [Test]
        public void HudChipAndPauseHint_ShowPreserveCounter()
        {
            StartGame(true);
            Kill(3);
            game.DrinkHud.RefreshForTests();
            StringAssert.Contains("3/5", game.DrinkHud.ChipStateText);
            game.OpenPause();
            StringAssert.Contains("보존", game.PauseView.DrinkHintText);
            game.ClosePause();
            Kill(2);
            game.DrinkHud.RefreshForTests();
            StringAssert.Contains("사용됨", game.DrinkHud.ChipStateText);
        }

        [Test]
        public void MidBossThenDeath_MaterialsGrantedAndShownOnResult()
        {
            StartGame(false);
            int gin = DrinkLab.Owned("gin");
            Kill(10);
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            Assert.AreEqual(1, DrinkLab.PendingFor(game.RunId).Count, "보스 사망 즉시 확정 기록");
            StringAssert.Contains("보스 음료 재료", game.DrinkHud.ToastText);
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(gin + 300, DrinkLab.Owned("gin"));
            StringAssert.Contains("창고에 저장했습니다", ResultView.DrinkStatusLine(game.DrinkSettlement));
        }

        [Test]
        public void AppQuitDuringRun_IsNotExplicitAbandon()
        {
            StartGame(true);
            Kill(1);
            game.PrepareForQuit();
            Assert.IsNull(DrinkLab.Prepared, "정상 앱 종료는 보존 포기가 아님 — 소비");
        }

        [Test]
        public void NoDrinkRun_UnaffectedStats()
        {
            StartGame(false);
            Assert.IsNull(game.DrinkBuff);
            var plain = new PlayerStats();
            Assert.AreEqual(plain.MaxHp, game.Stats.MaxHp, 1e-4f);
            Assert.AreEqual(0f, game.Stats.drinkCritChance);
        }

        [Test]
        public void CritChance_UsesExistingDoubleDamageRule()
        {
            StartGame(false);
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Brute, 1f, null, null);
            try
            {
                game.Stats.drinkCritChance = 0f;
                Assert.AreEqual(10f, game.Player.HiddenRuntime.ModifyDirectDamage(enemy, 10f), 1e-5f);
                game.Stats.drinkCritChance = 1f; // 경계: 항상 발동하면 ×2 (기존 완벽한 한 잔 치명타와 같은 규칙)
                Assert.AreEqual(20f, game.Player.HiddenRuntime.ModifyDirectDamage(enemy, 10f), 1e-5f);
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }

        [Test]
        public void CooldownReduction_ShortensAttackInterval()
        {
            var tea = DrinkRecipe.NewDraft(DrinkKind.Tea);
            tea.name = "홍차";
            tea.steps.Add(new DrinkStep("black_tea", 50));
            StartGame(true, tea);
            var plain = new PlayerStats();
            Assert.Greater(game.DrinkBuff[DrinkStat.CooldownReduction], 0f);
            Assert.AreEqual(plain.AttackInterval * (1f - game.DrinkBuff[DrinkStat.CooldownReduction]), game.Stats.AttackInterval, 1e-5f);
        }

        [Test]
        public void ResultLine_SaveFailureShowsRetrying()
        {
            StartGame(false);
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            DrinkStore.FaultHookForTests = s => { if (s == DrinkStore.SaveStage.WriteTmp) throw new IOException("x"); };
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            StringAssert.Contains("다시 시도", ResultView.DrinkStatusLine(game.DrinkSettlement));
            DrinkStore.FaultHookForTests = null;
            Assert.IsTrue(DrinkLab.RetrySave());
            StringAssert.Contains("창고에 저장했습니다", ResultView.DrinkStatusLine(game.DrinkSettlement));
        }
    }
}
