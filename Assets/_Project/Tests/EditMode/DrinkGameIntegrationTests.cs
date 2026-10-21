using System;
using System.IO;
using System.Linq;
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
            Assert.Greater(DrinkSupply.Get(p[0].items[0].id).normal, 0, "일반 표 후보(78: 희귀 포함)");
            StringAssert.Contains("음료 재료", game.DrinkHud.ToastText);
            game.OnEnemyKilled(Vector2.zero, Elite);
            Assert.IsTrue(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId == "elite1"), "정예 1묶음");
            Kill(24);
            Assert.AreEqual(2, DrinkLab.PendingFor(game.RunId).Count, "정예는 일반 25회 카운트에 넣지 않는다");
            Kill(1);
            Assert.AreEqual(3, DrinkLab.PendingFor(game.RunId).Count);
            Assert.AreEqual(51, game.ResearchKills, "RP 처치 = 일반 + 정예");
        }

        // ── 78 일반 보상 판당 60묶음 상한: 실제 GameManager.OnEnemyKilled 경로 ──
        static int NormalAwards(string runId) => DrinkLab.PendingFor(runId).Count(a => a.awardId.StartsWith("normal", StringComparison.Ordinal));

        [Test]
        public void Supply_NormalCap60_RealKillPath_Boundaries_EliteBossRpContinue()
        {
            StartGame(false);
            Kill(1499);
            Assert.AreEqual(59, NormalAwards(game.RunId), "1499킬 = 59묶음");
            Assert.AreEqual(59, game.NormalSupplyBundles);
            Assert.AreEqual(24, game.NormalKillsTowardBundle);
            Kill(1);
            Assert.AreEqual(60, NormalAwards(game.RunId), "1500킬 = 60묶음");
            Assert.IsTrue(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId == "normal60"));
            Assert.AreEqual(0, game.NormalKillsTowardBundle);
            Kill(25);
            Assert.AreEqual(60, NormalAwards(game.RunId), "1525킬도 60묶음(상한)");
            Assert.IsFalse(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId == "normal61"));
            Assert.AreEqual(0, game.NormalKillsTowardBundle, "상한 뒤 일반 카운터는 더 세지 않는다");
            Kill(1000);
            Assert.AreEqual(60, game.NormalSupplyBundles);
            Assert.AreEqual(0, game.NormalKillsTowardBundle);
            // 정예·보스 보상은 상한과 별도로 계속
            game.OnEnemyKilled(Vector2.zero, Elite);
            Assert.IsTrue(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId == "elite1"), "상한 뒤 정예 보상");
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            Assert.IsTrue(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId.StartsWith("boss", StringComparison.Ordinal)), "상한 뒤 보스 보상");
            Assert.AreEqual(60, NormalAwards(game.RunId), "정예·보스는 일반 상한에 포함되지 않음");
            Assert.AreEqual(2526, game.ResearchKills, "RP 처치는 상한 뒤에도 계속(일반 2525 + 정예 1)");
            Assert.AreEqual(2527, game.Kills);
            // 정산: 창고 = 이전 + 보상 합계, RP = min(5, 2526/100) + 중간 보스 2
            var totals = DrinkLab.Totals(DrinkLab.PendingFor(game.RunId).SelectMany(a => a.items).ToList());
            var before = DrinkIngredients.All.ToDictionary(d => d.id, d => DrinkLab.Owned(d.id));
            int rpBefore = DrinkLab.Research.researchPoints;
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(DrinkSupply.KillRpCap + DrinkSupply.MidBossRp, game.DrinkSettlement.researchPoints);
            Assert.AreEqual(rpBefore + game.DrinkSettlement.researchPoints, DrinkLab.Research.researchPoints);
            foreach (var d in DrinkIngredients.All) { totals.TryGetValue(d.id, out var t); Assert.AreEqual(before[d.id] + t, DrinkLab.Owned(d.id), d.id); }
            // 정산 뒤 처치·프레임 끝 처리를 반복해도 새 보상·재정산 없음
            Kill(25);
            game.ResolveEndOfFrame();
            foreach (var d in DrinkIngredients.All) { totals.TryGetValue(d.id, out var t); Assert.AreEqual(before[d.id] + t, DrinkLab.Owned(d.id), d.id + " 중복 정산"); }
            Assert.AreEqual(rpBefore + game.DrinkSettlement.researchPoints, DrinkLab.Research.researchPoints, "RP 중복 없음");
        }

        [Test]
        public void Supply_NormalCap_ResetsOnNewRun()
        {
            StartGame(false);
            Kill(1525);
            Assert.AreEqual(60, NormalAwards(game.RunId));
            string firstRun = game.RunId;
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            UnityEngine.Object.DestroyImmediate(root);
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier);
            Assert.AreNotEqual(firstRun, game.RunId);
            Assert.AreEqual(0, game.NormalSupplyBundles, "새 런 초기화");
            Assert.AreEqual(0, game.NormalKillsTowardBundle);
            Kill(24);
            Assert.AreEqual(0, NormalAwards(game.RunId));
            Kill(1);
            Assert.IsTrue(DrinkLab.PendingFor(game.RunId).Exists(a => a.awardId == "normal1"), "새 런 첫 25킬 보상");
            Kill(1475);
            Assert.AreEqual(60, NormalAwards(game.RunId), "새 런도 60까지 다시 지급");
        }

        [Test]
        public void Supply_CapBundleSaveFailure_KeptOnceAndRetried_NoExtraBundle()
        {
            StartGame(false);
            Kill(1475);
            Assert.AreEqual(59, NormalAwards(game.RunId));
            DrinkStore.FaultHookForTests = s => { if (s == DrinkStore.SaveStage.WriteTmp) throw new IOException("x"); };
            Kill(25);
            Assert.AreEqual(1, DrinkLab.PendingFor(game.RunId).Count(a => a.awardId == "normal60"), "저장 실패해도 메모리에 한 번 기록");
            Kill(50);
            Assert.AreEqual(60, NormalAwards(game.RunId), "실패·재시도 동안 추가 묶음 없음");
            DrinkStore.FaultHookForTests = null;
            Assert.IsTrue(DrinkLab.RetrySave());
            var totals = DrinkLab.Totals(DrinkLab.PendingFor(game.RunId).SelectMany(a => a.items).ToList());
            var before = DrinkIngredients.All.ToDictionary(d => d.id, d => DrinkLab.Owned(d.id));
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            foreach (var d in DrinkIngredients.All) { totals.TryGetValue(d.id, out var t); Assert.AreEqual(before[d.id] + t, DrinkLab.Owned(d.id), d.id); }
            int bundles = 0;
            foreach (var kv in totals) bundles += (int)(kv.Value / DrinkSupply.Get(kv.Key).bundle);
            Assert.AreEqual(60, bundles, "일반 60묶음이 정확히 한 번 정산");
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

        // ── hudfix 2026-10-19: 긴 판 결과 화면 재료 목록(묶음 수백 개가 화면 전체를 덮음) — 표시만 합계, 정산·창고·RP는 그대로 ──
        static readonly EnemyDef FinalBoss = new EnemyDef { name = "테스트 최종 보스", dropCount = 0, hp = 10f, boss = BossKind.Final };

        [Test]
        public void Summarize_RepeatedPackets_OneEntryPerIngredient_LongTotals_TableOrder()
        {
            var ids = new[] { DrinkIngredients.Honey, DrinkIngredients.Gin, DrinkIngredients.GreenTea, DrinkIngredients.Rum, DrinkIngredients.LemonJuice,
                DrinkIngredients.BlackTea, DrinkIngredients.BlueCuracao, DrinkIngredients.Grenadine };
            var packets = new System.Collections.Generic.List<DrinkAmount>();
            var expected = new System.Collections.Generic.Dictionary<string, long>();
            for (int i = 0; i < 240; i++)
            {
                string id = ids[i % ids.Length];
                int amount = 10 + (i % 7) * 5;
                packets.Add(new DrinkAmount(id, amount));
                expected.TryGetValue(id, out var t);
                expected[id] = t + amount;
            }
            packets.Add(new DrinkAmount("unknown_item", 99));
            packets.Add(null);
            var totals = DrinkLab.Totals(packets);
            CollectionAssert.AreEquivalent(expected, totals);
            string text = DrinkLab.Summarize(packets);
            var parts = text.Split(new[] { " · " }, StringSplitOptions.None);
            Assert.AreEqual(8, parts.Length, text);
            int last = -1;
            foreach (var def in DrinkIngredients.All)
            {
                if (!expected.ContainsKey(def.id)) continue; // 이 묶음에 없는 재료(예: 원두)는 요약에도 없다
                int at = text.IndexOf(def.name + " ", StringComparison.Ordinal);
                Assert.Greater(at, last, "재료표 순서: " + text);
                last = at;
                StringAssert.Contains($"{def.name} {DrinkRules.Format((int)expected[def.id], def.form)}", text);
            }
            // int 범위를 넘는 합계도 넘침 없이 long으로 더한다(표시는 int 최대로 묶음).
            var huge = new[] { new DrinkAmount(DrinkIngredients.Gin, int.MaxValue), new DrinkAmount(DrinkIngredients.Gin, int.MaxValue) };
            Assert.AreEqual(2L * int.MaxValue, DrinkLab.Totals(huge)[DrinkIngredients.Gin]);
            Assert.AreEqual("없음", DrinkLab.Summarize(null));
        }

        [Test]
        public void LongClear_200PlusPackets_StockAndRpUnchanged_ResultLineAggregated()
        {
            StartGame(true);
            var before = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var def in DrinkIngredients.All) before[def.id] = DrinkLab.Owned(def.id);
            int rpBefore = DrinkLab.Research.researchPoints;
            Kill(5000);
            for (int i = 0; i < 20; i++) game.OnEnemyKilled(Vector2.zero, Elite);
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            game.OnEnemyKilled(Vector2.zero, FinalBoss);
            // 출처별 묶음 수(경제 조정용 기록 — 수치 변경 없음)
            int packets = 0;
            var bySource = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var a in DrinkLab.PendingFor(game.RunId))
            {
                packets += a.items.Count;
                string src = a.awardId.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                bySource.TryGetValue(src, out var n);
                bySource[src] = n + a.items.Count;
            }
            TestContext.WriteLine("packets by source: " + string.Join(", ", bySource) + $" total={packets}");
            // 78: 일반 5000킬이어도 일반 묶음은 판당 60(정예 20·보스는 별도). 보상 하나 = 같은 재료 합친 기록 줄.
            Assert.AreEqual(DrinkSupply.NormalBundleCap, bySource["normal"], "일반 상한 60");
            Assert.AreEqual(20, bySource["elite"], "정예는 상한과 별도");
            Assert.GreaterOrEqual(packets, DrinkSupply.NormalBundleCap + 20 + 2, "긴 판 재료 기록 수");
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsTrue(game.Cleared);
            var s = game.DrinkSettlement;
            Assert.AreEqual("Consumed", s.drink, "5킬 넘음 = 사용");
            Assert.AreEqual(packets, s.granted.Count, "정산 기록(출처 단위)은 합치지 않고 그대로");
            var totals = DrinkLab.Totals(s.granted);
            foreach (var def in DrinkIngredients.All)
            {
                totals.TryGetValue(def.id, out var t);
                Assert.AreEqual(before[def.id] + t, DrinkLab.Owned(def.id), def.id + " 창고 = 이전 + 합계");
            }
            Assert.AreEqual(rpBefore + s.researchPoints, DrinkLab.Research.researchPoints);
            Assert.Greater(s.researchPoints, 0);
            // 다시 프레임 끝 처리를 해도 재정산·RP 중복 없음
            game.ResolveEndOfFrame();
            Assert.AreEqual(rpBefore + s.researchPoints, DrinkLab.Research.researchPoints);
            foreach (var def in DrinkIngredients.All) { totals.TryGetValue(def.id, out var t); Assert.AreEqual(before[def.id] + t, DrinkLab.Owned(def.id)); }

            string line = ResultView.DrinkStatusLine(s);
            var lines = line.Split('\n');
            Assert.AreEqual(4, lines.Length, line);
            Assert.Less(line.Length, 400, "결과 문구 길이 상한");
            StringAssert.Contains("창고에 저장했습니다", lines[0]);
            StringAssert.Contains($"연구 포인트 +{s.researchPoints}", lines[2]);
            // 결과 화면 한 줄 요약: 재료 종류 수 + 저장 상태 + RP + 음료(전체 합계는 획득 내역 창)
            string summary = ResultView.DrinkSummaryLine(s);
            Assert.IsFalse(summary.Contains("\n"));
            StringAssert.Contains($"음료 재료 {totals.Count}종 획득", summary);
            StringAssert.Contains($"연구 포인트 +{s.researchPoints}", summary);
            StringAssert.Contains("준비 음료 사용됨", summary);
            StringAssert.Contains($"재료 {totals.Count}종 획득", lines[1], "결과 요약은 종류 수만 표시해 길이를 제한");
            string allMaterials = DrinkLab.Summarize(s.granted);
            foreach (var def in DrinkIngredients.All)
            {
                if (!totals.ContainsKey(def.id)) continue;
                int first = allMaterials.IndexOf(def.name + " ", StringComparison.Ordinal);
                Assert.GreaterOrEqual(first, 0, def.name);
                Assert.AreEqual(-1, allMaterials.IndexOf(def.name + " ", first + 1, StringComparison.Ordinal), def.name + " 한 번만");
            }
            StringAssert.Contains("사용되어", lines[lines.Length - 1]);
        }

        [Test]
        public void ResultLine_DeathNoDrink_AbandonPreserved_NoRewards()
        {
            StartGame(false);
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual("음료 재료: 이번 판 재료 보상 없음", ResultView.DrinkStatusLine(game.DrinkSettlement));
            UnityEngine.Object.DestroyImmediate(root);

            StartGame(true);
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            game.OnEnemyKilled(Vector2.zero, MidBoss);
            int owned = DrinkLab.Owned(DrinkIngredients.Gin);
            game.OpenPause();
            game.GiveUp();
            var s = game.DrinkSettlement;
            Assert.AreEqual("PreservedAbandon", s.kind);
            Assert.AreEqual("Preserved", s.drink, "5킬 전 포기 = 보존");
            Assert.AreEqual(owned, DrinkLab.Owned(DrinkIngredients.Gin), "보존 포기 재료는 창고에 안 들어감");
            var lines = ResultView.DrinkStatusLine(s).Split('\n');
            Assert.AreEqual(3, lines.Length);
            StringAssert.Contains("정산하지 않았습니다", lines[0]);
            StringAssert.StartsWith("정산 안 함: ", lines[1]);
            StringAssert.Contains("보존", lines[2]);
            Assert.AreEqual(0, s.researchPoints);
        }

        // hudfix 2026-10-19 사용자 확인: 완성 음료 이름 띄어쓰기 — 기본 이름(재료 + 기법)과 직접 입력한 이름 모두 저장·공유·HUD까지 띄어쓰기 그대로.
        [Test]
        public void DrinkNames_KeepSpaces_DefaultCustomSavedSharedAndHud()
        {
            var plain = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            plain.technique = (int)DrinkTechnique.Stir;
            plain.steps.Add(new DrinkStep(DrinkIngredients.BlueCuracao, 50));
            plain.steps.Add(new DrinkStep(DrinkIngredients.Gin, 200));
            Assert.AreEqual("블루 큐라소 스터링", DrinkLab.DefaultName(plain), "기본 이름 = 첫 재료 이름 + 공백 + 기법");
            Assert.AreEqual("블루 큐라소 스터링", DrinkValidator.SanitizeName("  블루 큐라소 스터링 ", "x"), "가운데 공백 유지, 앞뒤만 정리");

            plain.name = "";
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(plain).Status);
            Assert.AreEqual("블루 큐라소 스터링", DrinkLab.Prepared.recipe.name, "이름 없이 만들면 띄어 쓴 기본 이름");
            Assert.IsTrue(DrinkLab.DiscardPrepared(out var discardError), discardError);

            var custom = plain.Clone();
            custom.name = "진 레몬 셰이킹"; // 사용자가 띄어 쓴 이름은 그대로
            Assert.IsTrue(DrinkLab.SaveRecipe(custom, -1, out var saveError), saveError);
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.Recipes.Any(r => r.name == "진 레몬 셰이킹"), "저장 후 다시 읽어도 띄어쓰기 유지");
            var decoded = DrinkShareCode.Decode(DrinkShareCode.Encode(custom), out var codeError);
            Assert.IsNotNull(decoded, codeError);
            Assert.AreEqual("진 레몬 셰이킹", decoded.name, "공유 코드 왕복");

            StartGame(true, custom);
            game.DrinkHud.RefreshForTests();
            Assert.AreEqual("진 레몬 셰이킹", DrinkLab.Prepared.recipe.name);
            Assert.AreEqual("준비 음료 · 진 레몬 셰이킹", game.DrinkHud.ChipTitle.text, "HUD 칩 제목도 띄어쓰기 그대로");
        }
    }
}
