using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-24 #25 출발 잔 도핑(상한 약 2.5배·합산 상한)·출발 잔/런 잔 분리·정식 레시피 3종 판정·발견 저장·도감 누출 방지·
    /// 시그니처 1회 사용(포기 환불·저장 실패 재시도·런 교체·재시작 악용) 검사(EditMode, 격리 경로).
    /// </summary>
    public class DrinkSignatureTests
    {
        string dir, path;
        GameObject root, createdCamera;
        GameManager game;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "drinksig_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "drinks.json");
            DrinkLab.SetPathForTests(path);
            DrinkStore.FaultHookForTests = null;
            SignatureRevealOverlay.SkipForTests = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (createdCamera != null) UnityEngine.Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
            Enemy.Active.RemoveAll(e => e == null);
            SignatureRevealOverlay.SkipForTests = false;
            DrinkStore.FaultHookForTests = null;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        static void FailSaves() => DrinkStore.FaultHookForTests = st => { if (st == DrinkStore.SaveStage.WriteTmp) throw new IOException("주입된 디스크 실패"); };

        static DrinkRecipe Cocktail(DrinkTechnique t, params (string id, int ml)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)t;
            r.name = "실험";
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.ml * 10));
            return r;
        }

        static DrinkRecipe Margarita(int a = 50, int b = 20, int c = 15, DrinkTechnique t = DrinkTechnique.Shake) =>
            Cocktail(t, (DrinkIngredients.TequilaBlanco, a), (DrinkIngredients.TripleSec, b), (DrinkIngredients.LimeJuice, c));
        static DrinkRecipe Sidecar() => Cocktail(DrinkTechnique.Shake, (DrinkIngredients.Cognac, 50), (DrinkIngredients.TripleSec, 20), (DrinkIngredients.LemonJuice, 20));
        static DrinkRecipe WhiteLady() => Cocktail(DrinkTechnique.Shake, (DrinkIngredients.Gin, 40), (DrinkIngredients.TripleSec, 30), (DrinkIngredients.LemonJuice, 20));

        static void Stock(DrinkRecipe r, int times = 1)
        {
            foreach (var s in r.steps) DrinkLab.Data.Add(s.id, s.amount * times);
        }

        /// <summary>EditMode에서는 OnEnable이 돌지 않아 Enemy.Active에 안 들어간다(통합 검수에서 확인: Active=0) — 실제 런과 같게 등록한다.</summary>
        static Enemy Register(Enemy e) { if (e != null && !Enemy.Active.Contains(e)) Enemy.Active.Add(e); return e; }

        void StartGame(DrinkRecipe drink, RunMode mode = RunMode.Standard)
        {
            if (drink != null)
            {
                Stock(drink);
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(drink).Status);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier, mode);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
            if (game.Master != null) { game.Master.BannersEnabled = false; Assert.IsTrue(game.Master.StartBattle()); }
        }

        // ───── A층: 출발 잔 도핑 ─────

        [Test]
        public void DepartureCaps_AreAbout2_5xBase_AndDepartureBuffStaysWithinCaps()
        {
            foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat)))
            {
                float k = DrinkRules.DepartureCap(s) / DrinkRules.Cap(s);
                Assert.That(k, Is.InRange(2.3f, 2.6f), s.ToString());
            }
            var strong = Cocktail(DrinkTechnique.Stir, (DrinkIngredients.Gin, 60), (DrinkIngredients.Vodka, 40), (DrinkIngredients.LemonJuice, 20));
            var b = DrinkCalculator.Buff(strong);
            var d = DrinkCalculator.DepartureBuff(strong);
            foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat)))
            {
                Assert.LessOrEqual(d[s], DrinkRules.DepartureCap(s) + 1e-6f, s.ToString());
                if (b[s] > 0f) Assert.AreEqual(Mathf.Min(DrinkRules.DepartureCap(s), b[s] * DrinkRules.DepartureScale), d[s], 1e-6f, s.ToString());
            }
            Assert.Greater(d[DrinkStat.Attack], DrinkRules.Cap(DrinkStat.Attack), "강한 배합은 기존 상한(10%)을 넘어 체감된다");
        }

        [Test]
        public void Combine_SumsDepartureAndRunCup_ClampedPerStatAtDepartureCap()
        {
            var strong = Cocktail(DrinkTechnique.Stir, (DrinkIngredients.Gin, 60), (DrinkIngredients.Vodka, 40), (DrinkIngredients.Rum, 20));
            var dep = DrinkCalculator.DepartureBuff(strong);
            var cup = DrinkCalculator.Buff(strong);
            var c = DrinkCalculator.Combine(dep, cup);
            foreach (DrinkStat s in Enum.GetValues(typeof(DrinkStat)))
            {
                Assert.AreEqual(Mathf.Min(DrinkRules.DepartureCap(s), dep[s] + cup[s]), c[s], 1e-6f, s.ToString());
                Assert.LessOrEqual(c[s], DrinkRules.DepartureCap(s) + 1e-6f, s.ToString());
            }
            Assert.AreEqual(DrinkRules.DepartureCap(DrinkStat.Attack), c[DrinkStat.Attack], 1e-6f, "강한 출발 잔 + 강한 런 잔도 공격력은 출발 잔 상한에서 멈춤");
            Assert.IsNull(DrinkCalculator.Combine(null, null));
            Assert.AreEqual(cup[DrinkStat.Attack], DrinkCalculator.Combine(null, cup)[DrinkStat.Attack], 1e-6f);
        }

        [Test]
        public void RunCup_DoesNotEraseDepartureBuff_AndSwapsOnlyRunCupShare_NoStacking()
        {
            var departure = Cocktail(DrinkTechnique.Stir, (DrinkIngredients.Gin, 30), (DrinkIngredients.Grenadine, 10));
            StartGame(departure, RunMode.Endless);
            Assume.That(game.Master != null, "끝없는 모드(마스터 도전)에서만 런 잔");
            var depBuff = game.DepartureDrinkBuff;
            Assert.IsNotNull(depBuff);
            float dmgBase = game.Stats.damagePercent - DrinkBuff.Percent(depBuff[DrinkStat.Attack]) / 100f;
            var gin = Cocktail(DrinkTechnique.Stir, (DrinkIngredients.Gin, 60));
            Stock(gin, 3);
            var c1 = game.CraftRunCup(gin).Cup; var c2 = game.CraftRunCup(gin).Cup; var c3 = game.CraftRunCup(gin).Cup;
            Assert.IsTrue(game.DrinkRunCup(c1, out var e1), e1);
            Assert.AreSame(depBuff, game.DepartureDrinkBuff, "출발 잔 효과는 그대로");
            Assert.Greater(game.DrinkBuff[DrinkStat.MaxHp], 0f, "출발 잔의 최대 체력 효과가 런 잔을 마셔도 남는다");
            float afterOne = game.Stats.damagePercent;
            Assert.IsTrue(game.DrinkRunCup(c2, out var e2), e2);
            Assert.IsTrue(game.DrinkRunCup(c3, out var e3), e3);
            Assert.AreEqual(afterOne, game.Stats.damagePercent, 1e-6f, "같은 런 잔을 여러 번 마셔도 누적 없음(교체)");
            float expected = DrinkCalculator.Combine(depBuff, game.RunCupDrinkBuff)[DrinkStat.Attack];
            Assert.AreEqual(dmgBase + DrinkBuff.Percent(expected) / 100f, game.Stats.damagePercent, 1e-5f);
            Assert.LessOrEqual(game.DrinkBuff[DrinkStat.Attack], DrinkRules.DepartureCap(DrinkStat.Attack) + 1e-6f, "합산 상한");
        }

        // ───── B층: 정식 레시피 판정 ─────

        [Test]
        public void Match_OfficialRecipes_AndScaledRatios()
        {
            Assert.AreEqual("iba_margarita", DrinkSignatures.Match(Margarita())?.id);
            Assert.AreEqual("iba_sidecar", DrinkSignatures.Match(Sidecar())?.id);
            Assert.AreEqual("iba_white_lady", DrinkSignatures.Match(WhiteLady())?.id);
            Assert.AreEqual("iba_margarita", DrinkSignatures.Match(Margarita(40, 16, 12))?.id, "같은 비율 축소(68mL)");
            Assert.AreEqual("iba_margarita", DrinkSignatures.Match(Margarita(52, 20, 15))?.id, "허용 오차 안");
            var withWater = Margarita(); withWater.water = 200; withWater.ice = 3;
            Assert.AreEqual("iba_margarita", DrinkSignatures.Match(withWater)?.id, "희석수·얼음은 판정하지 않음");
        }

        [Test]
        public void Match_Rejects_WrongRatio_Technique_ExtraOrMissing_TooSmall_OtherKinds()
        {
            Assert.IsNull(DrinkSignatures.Match(Margarita(50, 20, 20)), "라임 비율 초과");
            Assert.IsNull(DrinkSignatures.Match(Margarita(t: DrinkTechnique.Stir)), "기법 불일치");
            var extra = Margarita(); extra.steps.Add(new DrinkStep(DrinkIngredients.SimpleSyrup, 50));
            Assert.IsNull(DrinkSignatures.Match(extra), "재료 추가");
            Assert.IsNull(DrinkSignatures.Match(Cocktail(DrinkTechnique.Shake, (DrinkIngredients.TequilaBlanco, 50), (DrinkIngredients.TripleSec, 20))), "재료 누락");
            Assert.IsNull(DrinkSignatures.Match(Margarita(25, 10, 8)), "총량 43mL < 45mL");
            Assert.IsNull(DrinkSignatures.Match(DrinkLab.ExamplePresets[0]), "네그로니(교본)는 1차 판정 대상 아님");
            Assert.IsNull(DrinkSignatures.Match(DrinkLab.ExamplePresets[2]), "드라이 마티니(예시)는 1차 판정 대상 아님");
            foreach (var ex in DrinkLab.ExamplePresets) Assert.IsNull(DrinkSignatures.Match(ex), "예시 배합은 정답이 아니다: " + ex.name);
            Assert.IsNull(DrinkSignatures.Match(null));
            var tea = DrinkRecipe.NewDraft(DrinkKind.Tea); tea.steps.Add(new DrinkStep(DrinkIngredients.SenchaTea, 20));
            Assert.IsNull(DrinkSignatures.Match(tea), "차·커피 숨은 정답 없음");
            Assert.AreEqual(0, DrinkSignatures.Count(DrinkKind.Tea));
            Assert.AreEqual(0, DrinkSignatures.Count(DrinkKind.Coffee));
        }

        [Test]
        public void Catalog_UsesOnlyExistingIngredients_NoSubstitutesOrBitters()
        {
            Assert.AreEqual(3, DrinkSignatures.All.Count);
            foreach (var d in DrinkSignatures.All)
            {
                Assert.AreEqual(d.ingredientIds.Length, d.amountsMl.Length, d.id);
                foreach (var id in d.ingredientIds) Assert.IsNotNull(DrinkIngredients.Get(id), d.id + " " + id);
                StringAssert.StartsWith("https://iba-world.com/iba-cocktail/", d.sourceUrl);
                Assert.LessOrEqual(d.OfficialTotalMl, DrinkRules.CocktailLiquidMax / 10f, "공식 양 그대로 만들 수 있음");
                Assert.AreNotEqual(SignatureEffect.None, d.effect);
            }
        }

        // ───── 발견: 실제 제조 때만 · 저장 · 누출 방지 ─────

        [Test]
        public void PreviewSaveRecipeAndImport_DoNotDiscover_OnlyCraftDoes_FirstThenRepeat()
        {
            var m = Margarita();
            DrinkCalculator.DepartureBuff(m); DrinkCalculator.Look(m);
            Assert.IsTrue(DrinkLab.SaveRecipe(m, -1, out var e), e);
            Assert.AreEqual(0, DrinkLab.DiscoveredCount, "미리보기·레시피 저장으로는 발견되지 않음");
            StringAssert.DoesNotContain("마가리타", DrinkLabView.SignatureBookText());
            StringAssert.DoesNotContain("솔트 림", DrinkLabView.SignatureBookText());
            Stock(m, 2);
            var r1 = DrinkLab.Craft(m);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, r1.Status, r1.Message);
            Assert.AreEqual("iba_margarita", r1.SignatureId);
            Assert.IsTrue(r1.FirstDiscovery);
            Assert.AreEqual("iba_margarita", DrinkLab.Prepared.signatureId);
            Assert.AreEqual(1, DrinkLab.DiscoveredCount);
            Assert.IsTrue(DrinkLab.DiscardPrepared(out var de), de);
            var r2 = DrinkLab.Craft(m);
            Assert.AreEqual("iba_margarita", r2.SignatureId);
            Assert.IsFalse(r2.FirstDiscovery, "두 번째는 재발견(짧은 알림)");
            Assert.AreEqual(1, DrinkLab.DiscoveredSignatures.Count);
        }

        [Test]
        public void Discovery_PersistsAcrossReload_SurvivesNormalize_V7Saved()
        {
            var m = Sidecar(); Stock(m);
            Assert.IsTrue(DrinkLab.Craft(m).FirstDiscovery);
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.IsDiscovered("iba_sidecar"));
            Assert.AreEqual("iba_sidecar", DrinkLab.Prepared.signatureId);
            StringAssert.Contains($"\"schemaVersion\": {DrinkStore.SignatureVersion}", File.ReadAllText(path));
            var text = DrinkLabView.SignatureBookText();
            StringAssert.Contains("사이드카", text);
            StringAssert.Contains("시그니처 · 사이드카 질주", text);
            StringAssert.DoesNotContain("마가리타", text, "미발견은 이름 없음");
            StringAssert.DoesNotContain("화이트 레이디", text);
            StringAssert.DoesNotContain("솔트 림", text);
            StringAssert.DoesNotContain("화이트 아웃", text);
            StringAssert.Contains(DrinkLabView.SignatureLockedName, text);
            Assert.AreEqual("정식 레시피 1/3", DrinkLabView.SignatureBookButtonLabel());
        }

        [Test]
        public void Craft_SaveFailure_NoDiscoveryNoStockChange()
        {
            var m = WhiteLady(); Stock(m);
            int gin = DrinkLab.Owned(DrinkIngredients.Gin);
            FailSaves();
            Assert.AreEqual(DrinkLab.CraftStatus.SaveFailed, DrinkLab.Craft(m).Status);
            DrinkStore.FaultHookForTests = null;
            Assert.AreEqual(0, DrinkLab.DiscoveredCount);
            Assert.AreEqual(gin, DrinkLab.Owned(DrinkIngredients.Gin));
        }

        [Test]
        public void OldV6File_LoadsWithEmptySignatures()
        {
            File.WriteAllText(path, "{\"schemaVersion\": 6, \"starterGranted\": true, \"coffeeStarterGranted\": true}");
            DrinkLab.ReloadForTests();
            Assert.AreEqual(0, DrinkLab.DiscoveredCount);
            Assert.IsNotNull(DrinkLab.Data.signatureUsedRunIds);
            Assert.IsFalse(DrinkLab.WriteBlocked);
        }

        // ───── 시그니처 1회 사용 · 악용 ─────

        [Test]
        public void DepartureSignature_ChargedAtStart_UsedOnce_ConsumesWithLock()
        {
            StartGame(Margarita());
            Assert.IsNotNull(game.Signature);
            Assert.AreEqual("iba_margarita", game.Signature.Charged?.id);
            var near = Register(Enemy.SpawnAt(game.Player.Position + new Vector2(1f, 0f), EnemyDef.Brute, 1f, null, null));
            try
            {
                float hp0 = near.Hp;
                Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
                Assert.Less(near.Hp, hp0, $"솔트 림 피해 (affected={game.Signature.LastAffected} dmg={game.Stats.Damage} inv={near.Invulnerable} active={Enemy.Active.Count} dead={near.IsDead} def={near.Def?.name} hp={near.Hp}/{near.MaxHp})");
                Assert.IsTrue(near.Staggered, "일반 적 경직");
                Assert.IsTrue(game.Signature.Used);
                Assert.IsNull(game.Signature.Charged);
                Assert.IsTrue(DrinkLab.SignatureUsedIn(game.RunId));
                Assert.IsTrue(DrinkLab.Prepared.locked, "사용 순간 잔이 이 판에 귀속");
                Assert.IsFalse(game.TryUseSignature(), "판당 1회");
                StringAssert.Contains("이미 사용", game.Signature.LastMessage);
            }
            finally { if (near != null) UnityEngine.Object.DestroyImmediate(near.gameObject); }
        }

        [Test]
        public void UseThenAbandonBefore5Kills_DrinkIsNotRefunded()
        {
            StartGame(WhiteLady());
            Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
            game.OpenPause();
            game.GiveUp();
            Assert.AreEqual("Consumed", game.DrinkSettlement.drink, "효과를 쓴 잔은 포기 보존 없음");
            Assert.IsNull(DrinkLab.Prepared);
        }

        [Test]
        public void AbandonWithoutUsing_PreservesDrinkAndSignature_NextRunCanCharge()
        {
            StartGame(WhiteLady());
            game.OpenPause();
            game.GiveUp();
            Assert.AreEqual("Preserved", game.DrinkSettlement.drink);
            Assert.AreEqual("iba_white_lady", DrinkLab.Prepared.signatureId, "쓰지 않은 시그니처는 잔과 함께 보존");
            UnityEngine.Object.DestroyImmediate(root);
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            root = new GameObject("Game2");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier);
            Assert.AreEqual("iba_white_lady", game.Signature.Charged?.id);
        }

        [Test]
        public void SaveFailure_NoEffect_ChargeKept_RetrySucceeds()
        {
            StartGame(Sidecar());
            FailSaves();
            Assert.IsFalse(game.TryUseSignature());
            Assert.AreEqual(0f, game.Stats.signatureMoveSpeed, "저장 실패면 효과 없음");
            Assert.IsNotNull(game.Signature.Charged, "충전 유지");
            Assert.IsFalse(DrinkLab.SignatureUsedIn(game.RunId));
            DrinkStore.FaultHookForTests = null;
            Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
            Assert.AreEqual(SignatureRuntime.SidecarMoveSpeed, game.Stats.signatureMoveSpeed, 1e-6f);
        }

        [Test]
        public void UsedRecordOnDisk_BlocksRecharge_ForSameRun()
        {
            StartGame(Margarita());
            Assert.IsTrue(game.TryUseSignature());
            Assert.IsFalse(DrinkLab.CommitSignatureUse(game.RunId, "iba_margarita", true, out _), "같은 runId 두 번째 확정 거부");
            Assert.IsFalse(game.Signature.Charge(DrinkSignatures.Get("iba_sidecar"), false), "사용 후 런 잔으로 재충전 불가");
            DrinkLab.ReloadForTests();
            Assert.IsTrue(DrinkLab.SignatureUsedIn(game.RunId), "재시작(다시 읽기) 뒤에도 사용 기록 유지");
        }

        [Test]
        public void NotUsableWhilePaused()
        {
            StartGame(Margarita());
            game.OpenPause();
            Assert.IsFalse(game.TryUseSignature());
            Assert.IsNotNull(game.Signature.Charged);
            Assert.IsFalse(DrinkLab.SignatureUsedIn(game.RunId), "일시정지 중 입력은 디스크 기록도 없음");
        }

        [Test]
        public void BossDamage_CappedAt12PercentMaxHp()
        {
            var boss = Register(Enemy.SpawnAt(Vector2.zero, new EnemyDef { name = "시험 보스", hp = 1000f, boss = BossKind.Mid }, 1f, null, null));
            var mob = Register(Enemy.SpawnAt(Vector2.zero, EnemyDef.Brute, 1f, null, null));
            try
            {
                Assert.AreEqual(boss.MaxHp * SignatureRuntime.BossDamageCapFraction, SignatureRuntime.CappedDamage(boss, 1e6f), 1e-3f);
                Assert.AreEqual(50f, SignatureRuntime.CappedDamage(mob, 50f), 1e-6f);
                Assert.IsFalse(boss.ApplySignatureHold(2f), "보스는 경직 없음");
            }
            finally { UnityEngine.Object.DestroyImmediate(boss.gameObject); UnityEngine.Object.DestroyImmediate(mob.gameObject); }
        }

        [Test]
        public void RunCup_HeldDoesNotDiscover_DrinkingDiscoversAndCharges_SecondCupCannotRechargeAfterUse()
        {
            StartGame(null, RunMode.Endless);
            Assume.That(game.Master != null);
            var m = Margarita(); Stock(m, 2);
            var c1 = game.CraftRunCup(m).Cup;
            Assert.AreEqual("iba_margarita", c1.signatureId, "제조 확정 때 판정");
            Assert.AreEqual(0, DrinkLab.DiscoveredCount, "보관만으로는 발견 아님(정산 때 환원되는 잔)");
            Assert.IsTrue(game.DrinkRunCup(c1, out var e), e);
            Assert.AreEqual(1, DrinkLab.DiscoveredCount);
            StringAssert.EndsWith(":iba_margarita", SignatureRevealOverlay.LastShown);
            StringAssert.DoesNotStartWith("short:", SignatureRevealOverlay.LastShown, "첫 발견은 긴 공개");
            Assert.AreEqual("iba_margarita", game.Signature.Charged?.id);
            Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
            var c2 = game.CraftRunCup(m).Cup;
            Assert.IsTrue(game.DrinkRunCup(c2, out e), e);
            Assert.IsNull(game.Signature.Charged, "이번 판에 이미 썼으면 런 잔을 다시 마셔도 충전 없음");
            Assert.IsFalse(game.TryUseSignature());
        }

        // ───── #25 아트 연결(ArtSource/signature-drinks-2026-11-24) ─────

        [Test]
        public void ArtTable_MatchesArtSourceManifest_Durations()
        {
            var manifest = Path.Combine(Application.dataPath, "..", "ArtSource", "signature-drinks-2026-11-24", "manifest.json");
            Assume.That(File.Exists(manifest), "ArtSource 반입 필요");
            var text = File.ReadAllText(manifest);
            foreach (var c in SignatureArt.All)
            {
                var m = System.Text.RegularExpressions.Regex.Match(text, "\"" + c.name + @""":\s*\{[^{}]*?""frame_px"":\s*\[\s*(\d+),\s*(\d+)\s*\][^{}]*?""frames_ms"":\s*\[([^\]]*)\]");
                Assert.IsTrue(m.Success, c.name);
                Assert.AreEqual(int.Parse(m.Groups[1].Value), c.framePx.x, c.name);
                Assert.AreEqual(int.Parse(m.Groups[2].Value), c.framePx.y, c.name);
                var ms = Array.ConvertAll(m.Groups[3].Value.Replace(",", " ").Split((char[])null, StringSplitOptions.RemoveEmptyEntries), int.Parse);
                CollectionAssert.AreEqual(ms, c.durationsMs, c.name);
            }
            Assert.AreEqual(SignatureRevealOverlay.FullSeconds * 1000f, SignatureArt.Get("iba_margarita_discovery").TotalMs, 1f, "최초 발견 2.6초 = 시트 길이");
            Assert.AreEqual(450, SignatureArt.Get("salt_ring").TotalMs);
            Assert.AreEqual(350, SignatureArt.Get("whiteout_edge").TotalMs);
            Assert.AreEqual("salt_ring_low", SignatureArt.Resolve("salt_ring", true));
            Assert.AreEqual("iba_white_lady_discovery_low", SignatureArt.DiscoveryClip("iba_white_lady", true));
            Assert.IsNull(SignatureArt.DiscoveryClip("iba_negroni", false), "1차 3종 외 발견 아트 없음");
            int frames = 0; foreach (var c in SignatureArt.All) frames += c.durationsMs.Length;
            Assert.AreEqual(SignatureArt.TotalFrames, frames);
        }

        [Test]
        public void ArtResources_Load_SaltRimDiameterMatchesRadius()
        {
            foreach (var c in SignatureArt.All) Assert.IsNotNull(SignatureArt.Load(c.name), "Resources/SignatureDrinks 프레임: " + c.name);
            Assert.AreEqual(SignatureRuntime.SaltRimRadius * 2f, SignatureArt.Load("salt_ring")[0].bounds.size.x, 0.01f, "표시 지름 8 = 반경 4");
            Assert.AreEqual(SignatureRuntime.SaltRimRadius * 2f, SignatureArt.Load("salt_ring_low")[0].bounds.size.x, 0.01f);
            Assert.AreEqual(FilterMode.Point, SignatureArt.Load("iba_sidecar_discovery")[0].texture.filterMode);
            Assert.Greater(SignatureArt.Load("whiteout_edge")[0].border.x, 0f, "서리는 9분할(가장자리만)");
        }

        [Test]
        public void WhiteOut_IceBadge_OnlyOnActuallyHeldNormalEnemies_NotBoss()
        {
            StartGame(WhiteLady());
            var at = game.Player.Position;
            var mob = Register(Enemy.SpawnAt(at + new Vector2(1.5f, 0f), EnemyDef.Brute, 1f, null, null));
            var boss = Register(Enemy.SpawnAt(at + new Vector2(0f, 1f), new EnemyDef { name = "시험 보스", hp = 1000f, boss = BossKind.Mid }, 1f, null, null));
            try
            {
                Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
                var badges = UnityEngine.Object.FindObjectsByType<SignatureVfx.IceBadge>(FindObjectsSortMode.None);
                Assume.That(SignatureArt.Load("whiteout_hold") != null, "아트 임포트 후 배지 검사");
                Assert.AreEqual(1, game.Signature.LastHeld, "경직이 실제로 걸린 적은 일반 1마리");
                Assert.AreEqual(1, badges.Length);
                Assert.AreSame(mob, badges[0].Target, "보스에는 배지 없음(쓰러진 적은 ApplySignatureHold가 false — EditMode에서 Destroy 오류를 피하려 처치 경우는 PlayMode 항목)");
                Assert.IsFalse(boss.Staggered);
                foreach (var b in badges) UnityEngine.Object.DestroyImmediate(b.gameObject);
            }
            finally
            {
                foreach (var e in new[] { mob, boss }) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            }
        }

        [Test]
        public void SidecarAndSaltRim_PlayArtClips_LowEffectsUsesLowClips()
        {
            StartGame(Sidecar());
            Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
            Assert.AreEqual(HitFeedback.LowEffects ? "sidecar_low" : "sidecar_wind", SignatureVfx.LastPlayed);
            Assert.AreEqual(SignatureRuntime.SidecarSeconds, game.Signature.ActiveRemaining, 1e-4f, "5초 질주");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<SignatureVfx.SidecarTrail>());
            SignatureVfx.SaltRim(Vector2.zero, Color.white);
            Assert.AreEqual(HitFeedback.LowEffects ? "salt_ring_low" : "salt_ring", SignatureVfx.LastPlayed, "아트 임포트 후 솔트 림 클립(저이펙트는 정적 고리)");
            foreach (var s in UnityEngine.Object.FindObjectsByType<SignatureVfx.Sequence>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(s.gameObject);
        }

        [Test]
        public void NoSignatureDrink_HudHiddenAndNoCharge()
        {
            StartGame(Cocktail(DrinkTechnique.Stir, (DrinkIngredients.Gin, 30), (DrinkIngredients.Grenadine, 10)));
            Assert.IsNull(game.Signature.Charged);
            Assert.IsFalse(game.TryUseSignature());
            Assert.IsFalse(DrinkLab.SignatureUsedIn(game.RunId));
        }
    }
}
