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
        static readonly EnemyDef Elite = new EnemyDef { name = "테스트 정예", dropCount = 0, hp = 10f, archetype = EnemyArchetype.EliteBarrage };

        [Test]
        public void Supply_Every25NormalKills_EliteSeparate_ResearchKillsCountBoth()
        {
            StartGame(false);
            Kill(24);
            Assert.AreEqual(0, DrinkLab.PendingFor(game.RunId).Count);
            Kill(1);
            var p = DrinkLab.PendingFor(game.RunId);
            Assert.AreEqual(1, p.Count);
            Assert.AreEqual("normal1", p[0].awardId);
            Assert.AreEqual(1, p[0].items.Count, "1묶음");
            Assert.AreEqual(DrinkSupply.Get(p[0].items[0].id).bundle, p[0].items[0].amount, "고정 수량");
            Assert.IsFalse(DrinkSupply.Get(p[0].items[0].id).rare, "일반 표에 희귀 없음");
            StringAssert.Contains("음료 재료", game.DrinkHud.ToastText);
            game.OnEnemyKilled(Vector2.zero, Elite);
            Assert.IsTrue(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId == "elite1"), "정예 1묶음");
            Kill(24);
            Assert.AreEqual(2, DrinkLab.PendingFor(game.RunId).Count, "정예는 일반 25회 카운트에 넣지 않는다");
            Kill(1);
            Assert.AreEqual(3, DrinkLab.PendingFor(game.RunId).Count);
            Assert.AreEqual(51, game.ResearchKills, "RP 처치 = 일반 + 정예");
        }

        [Test]
        public void Mastery_SnapshotAtStart_Finesse4ScalesDrinkWithinCaps_RuntimeAttached()
        {
            var r = DrinkLab.Data.research;
            r.researchPoints = 100;
            foreach (var id in new[] { "finesse_1", "finesse_2", "finesse_3", "finesse_4" }) Assert.IsTrue(DrinkLab.UnlockTrait(id, out var e), e);
            Assert.IsTrue(DrinkLab.EquipTrait("finesse_4", out var e2), e2);
            Assert.IsTrue(DrinkLab.EquipTrait("finesse_1", out e2), e2);
            var preview = DrinkCalculator.Buff(HpAtkDrink());
            StartGame(true);
            CollectionAssert.AreEquivalent(new[] { "finesse_4", "finesse_1" }, game.MasteryTraits);
            foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat)))
                Assert.AreEqual(preview[s] > 0f ? Mathf.Min(DrinkRules.Cap(s), preview[s] * 1.05f) : preview[s], game.DrinkBuff[s], 1e-6f, s.ToString());
            Assert.IsNotNull(game.Player.Mastery);
            Assert.IsTrue(game.Player.Mastery.Has("finesse_1"));
            Assert.IsFalse(game.Player.Mastery.Has("finesse_2"), "해금만 하고 장착 안 한 특성은 효과 없음");
            Assert.IsTrue(DrinkLab.UnequipTrait("finesse_1", out e2));
            Assert.IsTrue(game.Player.Mastery.Has("finesse_1"), "런 중 교체는 이번 런에 영향 없음(스냅샷)");
        }

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
            Kill(10);
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            Assert.AreEqual(1, DrinkLab.PendingFor(game.RunId).Count, "보스 사망 즉시 확정 기록");
            StringAssert.Contains("보스 음료 재료", game.DrinkHud.ToastText);
            // 72 §2 중간 보스 = 고정 묶음 3개(보유량 보정 추첨), 창고에는 정산 때 한 번
            var award = DrinkLab.PendingFor(game.RunId)[0].items;
            int bundles = 0;
            var before = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var a in award) { bundles += a.amount / DrinkSupply.Get(a.id).bundle; before[a.id] = DrinkLab.Owned(a.id); }
            Assert.AreEqual(3, bundles);
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            foreach (var a in award) Assert.AreEqual(before[a.id] + a.amount, DrinkLab.Owned(a.id), a.id);
            Assert.AreEqual(2, game.DrinkSettlement.researchPoints, "중간 보스 RP 2(처치 10회는 100 미만)");
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
        public void Mastery_DirectHitBonuses_RegularThirdHit_FullHpAndOpening()
        {
            var r = DrinkLab.Data.research;
            r.researchPoints = 100;
            foreach (var id in new[] { "service_1", "service_2", "finesse_1" }) Assert.IsTrue(DrinkLab.UnlockTrait(id, out var e), e);
            foreach (var id in new[] { "service_1", "service_2", "finesse_1" }) Assert.IsTrue(DrinkLab.EquipTrait(id, out var e), e);
            StartGame(false);
            game.Stats.drinkCritChance = 0f;
            var m = game.Player.Mastery;
            var a = Enemy.SpawnAt(new Vector2(0.5f, 0f), EnemyDef.Brute, 1f, null, null);
            var b2 = Enemy.SpawnAt(new Vector2(0.5f, 0f), EnemyDef.Brute, 1f, null, null);
            try
            {
                var hidden = game.Player.HiddenRuntime;
                // 첫 30초 + 체력 가득 = +8% +8%, 단골 1타
                Assert.AreEqual(10f * 1.16f, hidden.ModifyDirectDamage(a, 10f), 1e-4f);
                a.TakeDamage(1f);
                Assert.AreEqual(10f * 1.08f, hidden.ModifyDirectDamage(a, 10f), 1e-4f, "2타: 체력이 깎인 적");
                Assert.AreEqual(10f * (1.08f + 0.15f), hidden.ModifyDirectDamage(a, 10f), 1e-4f, "같은 적 3타째 +15%");
                Assert.AreEqual(10f * 1.08f, hidden.ModifyDirectDamage(a, 10f), 1e-4f, "3타 뒤 다시 1부터");
                hidden.ModifyDirectDamage(a, 10f);
                Assert.AreEqual(10f * 1.16f, hidden.ModifyDirectDamage(b2, 10f), 1e-4f, "대상 변경 = 단골 초기화");
                m.ForceRunTimeForTests(31f);
                b2.TakeDamage(1f);
                Assert.AreEqual(10f, hidden.ModifyDirectDamage(b2, 10f), 1e-4f, "30초 뒤 첫 잔의 기세 끝");
            }
            finally { UnityEngine.Object.DestroyImmediate(a.gameObject); UnityEngine.Object.DestroyImmediate(b2.gameObject); }
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
