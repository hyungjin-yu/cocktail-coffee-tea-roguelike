using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-27 #25 정식 레시피 시그니처 실제 장면 검증(임시 저장·창/입력 없음): 세 레시피 실제 제조·발견, 미발견 비공개, 최초/반복 연출,
    /// 출발 버프와 런 컵 공존, 1회 사용·재사용 방지, 경직 성공 시에만 얼음 배지. CCT_EVIDENCE_DIR가 있으면 720p/1080p/4:3 캡처도 남긴다.</summary>
    public class SignatureLivePlayTests
    {
        string folder, evDir; GameManager game; readonly StringBuilder log = new StringBuilder();
        static readonly (int w, int h)[] Sizes = { (1280, 720), (1920, 1080), (1024, 768) };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            folder = Path.Combine(Application.temporaryCachePath, "siglive-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            CardEducationGuide.SuppressAutomaticForTests = true;
            SignatureRevealOverlay.SkipForTests = false;
            InputSettings.Scheme = MovementScheme.Keyboard;
            var e = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            evDir = string.IsNullOrEmpty(e) ? null : Path.Combine(e, "signature");
            if (evDir != null) Directory.CreateDirectory(evDir);
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (evDir != null) File.AppendAllText(Path.Combine(evDir, "signature-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            foreach (var o in UnityEngine.Object.FindObjectsByType<DrinkLabView>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(o.gameObject);
            game = null; Time.timeScale = 1f; RunModeSelection.Current = RunMode.Standard;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            SignatureRevealOverlay.SkipForTests = false;
            yield return null;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[SigLive] " + s); }

        static DrinkRecipe Cocktail(string name, params (string id, int ml)[] steps)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail); r.technique = (int)DrinkTechnique.Shake; r.name = name;
            foreach (var s in steps) r.steps.Add(new DrinkStep(s.id, s.ml * 10));
            return r;
        }
        static DrinkRecipe Margarita() => Cocktail("실험1", (DrinkIngredients.TequilaBlanco, 50), (DrinkIngredients.TripleSec, 20), (DrinkIngredients.LimeJuice, 15));
        static DrinkRecipe Sidecar() => Cocktail("실험2", (DrinkIngredients.Cognac, 50), (DrinkIngredients.TripleSec, 20), (DrinkIngredients.LemonJuice, 20));
        static DrinkRecipe WhiteLady() => Cocktail("실험3", (DrinkIngredients.Gin, 40), (DrinkIngredients.TripleSec, 30), (DrinkIngredients.LemonJuice, 20));
        static DrinkRecipe Plain() => Cocktail("평범", (DrinkIngredients.Gin, 30), (DrinkIngredients.Grenadine, 10));

        static void Stock(DrinkRecipe r, int times = 1) { foreach (var s in r.steps) DrinkLab.Data.Add(s.id, s.amount * times); }

        IEnumerator Shots(string tag, Canvas canvas, params (int w, int h)[] sizes)
        {
            if (evDir == null) yield break;
            foreach (var (w, h) in (sizes.Length > 0 ? sizes : Sizes))
            {
                yield return null;
                yield return SkinEvidenceCapture.CaptureTo(evDir, Line, Camera.main, new[] { canvas }, w, h, $"{tag}_{w}x{h}");
            }
        }

        IEnumerator Start(DrinkRecipe departure, RunMode mode = RunMode.Standard, Job job = Job.Bartender, WeaponKind weapon = WeaponKind.Rapier)
        {
            if (departure != null)
            {
                Stock(departure);
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, DrinkLab.Craft(departure).Status);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var err), err);
            }
            RunModeSelection.Current = mode;
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.Destroy(e.gameObject);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(100000f);
            if (game.Master != null) { game.Master.BannersEnabled = false; game.Master.StartBattle(); }
            yield return null;
        }

        Canvas HudCanvas => game.Hud.GetComponentInParent<Canvas>();

        // ── 세 레시피 실제 제조·발견 + 미발견 비공개 + 최초/반복 판정 ──
        [UnityTest]
        public IEnumerator ThreeRecipes_RealCraft_DiscoverOnce_LockedBookHidesEverything()
        {
            var lab = DrinkLabView.Show(() => { });
            yield return null;
            string lockedBook = DrinkLabView.SignatureBookText();
            foreach (var d in DrinkSignatures.All)
            {
                StringAssert.DoesNotContain(d.name, lockedBook, "미발견 이름 비공개");
                StringAssert.DoesNotContain(d.effectName, lockedBook, "미발견 효과 비공개");
                foreach (var ing in d.ingredientIds) StringAssert.DoesNotContain(DrinkIngredients.Get(ing)?.name ?? "§§", lockedBook, "미발견 재료 비공개");
            }
            StringAssert.Contains(DrinkLabView.SignatureLockedName, lockedBook);
            Line("book locked: " + lockedBook.Replace("\n", " | "));
            Line("button label: " + DrinkLabView.SignatureBookButtonLabel());
            lab.OpenSignatureBook();
            yield return Shots("sig_book_locked", lab.GetComponent<Canvas>());

            foreach (var (recipe, id) in new[] { (Margarita(), "iba_margarita"), (Sidecar(), "iba_sidecar"), (WhiteLady(), "iba_white_lady") })
            {
                Stock(recipe, 2);
                var first = DrinkLab.Craft(recipe.Clone());
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, first.Status, first.Message);
                Assert.AreEqual(id, first.SignatureId, "정식 레시피 인식");
                Assert.IsTrue(first.FirstDiscovery, "첫 발견");
                DrinkLab.Data.prepared = new PreparedDrink(); // 같은 레시피 재제조 확인을 위해 준비 슬롯만 비운다(테스트 전용)
                var again = DrinkLab.Craft(recipe.Clone());
                Assert.AreEqual(id, again.SignatureId);
                Assert.IsFalse(again.FirstDiscovery, "반복 제조는 최초 아님");
                DrinkLab.Data.prepared = new PreparedDrink();
            }
            Assert.AreEqual(3, DrinkLab.DiscoveredCount);
            Assert.IsNull(DrinkLab.Craft(Plain()).SignatureId, "일반 레시피는 시그니처 아님");
            string openBook = DrinkLabView.SignatureBookText();
            foreach (var d in DrinkSignatures.All) StringAssert.Contains(d.name, openBook, "발견 후 공개");
            Line("book open: " + openBook.Replace("\n", " | "));
            lab.OpenSignatureBook();
            yield return Shots("sig_book_discovered", lab.GetComponent<Canvas>());
        }

        // ── 최초/반복 연출 ──
        [UnityTest]
        public IEnumerator Reveal_FullFirst_ShortRepeat_LowEffects()
        {
            yield return Start(null);
            var def = DrinkSignatures.Get("iba_margarita");
            SignatureRevealOverlay.Play(def);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsNotNull(SignatureRevealOverlay.Current);
            var rc = SignatureRevealOverlay.Current.GetComponentInParent<Canvas>().rootCanvas;
            Line($"reveal full clip={SignatureRevealOverlay.Current.ArtClip} last='{SignatureRevealOverlay.LastShown}'");
            yield return Shots("sig_reveal_full_0p5s", rc);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shots("sig_reveal_full_1p5s", rc, Sizes[0]);
            SignatureRevealOverlay.Current?.Finish();
            yield return null;
            SignatureRevealOverlay.PlayShort(def);
            yield return new WaitForSecondsRealtime(0.3f);
            var shortLen = SignatureRevealOverlay.ShortSeconds; Assert.Less(shortLen, SignatureRevealOverlay.FullSeconds);
            yield return Shots("sig_reveal_short", SignatureRevealOverlay.Current.GetComponentInParent<Canvas>().rootCanvas, Sizes[0]);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsTrue(SignatureRevealOverlay.Current == null || !SignatureRevealOverlay.Current.gameObject.activeInHierarchy, "반복 연출은 짧게 끝난다");
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            SignatureRevealOverlay.Play(DrinkSignatures.Get("iba_white_lady"));
            yield return new WaitForSecondsRealtime(1.6f);
            if (SignatureRevealOverlay.Current != null)
                yield return Shots("sig_reveal_low", SignatureRevealOverlay.Current.GetComponentInParent<Canvas>().rootCanvas, Sizes);
            SignatureRevealOverlay.Current?.Finish();
        }

        // ── 출발 버프 + 런 컵 공존, 1회 사용, 재사용·재충전 방지 ──
        [UnityTest]
        public IEnumerator DepartureBuff_And_RunCup_Coexist_SignatureOnce_NoReuse_NoRecharge()
        {
            SignatureRevealOverlay.SkipForTests = true;
            yield return Start(Margarita(), RunMode.Endless);
            var sig = game.Signature;
            Assert.AreEqual("iba_margarita", sig.Charged?.id, "출발 잔 시그니처 충전");
            Assert.IsTrue(sig.ChargedFromPrepared);
            var depBuff = game.DrinkBuff; Assert.IsNotNull(depBuff);
            float dmgDeparture = game.Stats.Damage;
            Line($"departure buff={depBuff.Describe()} dmg={dmgDeparture}");
            Assert.IsTrue(game.SignatureSlot.Visible, "충전되면 HUD 칸");
            Line($"hud charged title='{game.SignatureSlot.TitleText}' state='{game.SignatureSlot.StateText}'");
            yield return Shots("sig_hud_charged", HudCanvas);

            // 런 컵(다른 잔)을 마셔도 출발 버프의 몫은 남는다 + 시그니처 충전은 그대로(비시그니처 컵은 교체하지 않음)
            var plain = Plain(); foreach (var s in plain.steps) DrinkLab.Data.Add(s.id, s.amount * 2);
            var res = game.CraftRunCup(plain); Assert.IsTrue(res.Ok, res.Message);
            Assert.IsTrue(game.DrinkRunCup(res.Cup, out var err), err);
            Assert.AreEqual("iba_margarita", sig.Charged?.id, "비시그니처 런 컵은 충전을 지우지 않는다");
            Assert.GreaterOrEqual(game.Stats.Damage, dmgDeparture - 0.01f, "출발 버프가 런 컵으로 사라지지 않는다");
            Line($"after run cup dmg={game.Stats.Damage} buff={game.DrinkBuff.Describe()}");

            var near = Enemy.SpawnAt(game.Player.Position + new Vector2(1.5f, 0.3f), EnemyDef.Brute, 5000f, game.Player, game.transform);
            near.SetDriver(new Stationary()); near.GetComponent<Rigidbody2D>().simulated = false;
            float hp0 = near.Hp;
            Assert.IsTrue(game.TryUseSignature(), sig.LastMessage);
            Assert.Less(near.Hp, hp0, "솔트 림 피해");
            Assert.IsTrue(near.Staggered, "일반 적은 2초 경직");
            Assert.IsTrue(sig.Used); Assert.IsNull(sig.Charged);
            Assert.IsTrue(DrinkLab.SignatureUsedIn(game.RunId));
            yield return null;
            yield return Shots("sig_salt_rim_used", HudCanvas);
            // 재사용: Q 경로(TryUseSignature)와 HUD 버튼 클릭 모두 거절
            float hp1 = near.Hp;
            Assert.IsFalse(game.TryUseSignature(), "같은 판 재사용 금지");
            game.SignatureSlot.Button.onClick.Invoke();
            Assert.AreEqual(hp1, near.Hp, 0.001f, "HUD 클릭도 효과 없음");
            Assert.AreEqual(1, sig.UseSerial, "사용 1회");
            // 같은 시그니처 런 컵을 마셔도 다시 충전되지 않는다
            var m = Margarita(); Stock(m, 2);
            var mres = game.CraftRunCup(m); Assert.IsTrue(mres.Ok, mres.Message);
            Assert.IsTrue(game.DrinkRunCup(mres.Cup, out err), err);
            Assert.IsNull(sig.Charged, "사용 후 재충전 없음");
            Assert.IsFalse(game.TryUseSignature());
            Line($"reuse blocked: '{sig.LastMessage}' serial={sig.UseSerial}");
            yield return Shots("sig_hud_used", HudCanvas, Sizes[0]);
        }

        class Stationary : IEnemyDriver { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        // ── 경직이 실제로 걸린 적에게만 배지 / 사이드카 / 저이펙트 ──
        [UnityTest]
        public IEnumerator WhiteOut_BadgeOnlyOnHeldNormal_NotBoss_Low_And_Sidecar()
        {
            SignatureRevealOverlay.SkipForTests = true;
            yield return Start(WhiteLady());
            var at = game.Player.Position;
            var mob = Enemy.SpawnAt(at + new Vector2(2.5f, 0.5f), EnemyDef.Brute, 5000f, game.Player, game.transform);
            var boss = Enemy.SpawnAt(at + new Vector2(2.5f, -1.2f), EnemyDef.DifficultCustomer, 1f, game.Player, game.transform);
            foreach (var e in new[] { mob, boss }) { e.SetDriver(new Stationary()); e.GetComponent<Rigidbody2D>().simulated = false; }
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.2f);
            int badgesBefore = UnityEngine.Object.FindObjectsByType<SignatureVfx.IceBadge>(FindObjectsSortMode.None).Length;
            Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
            yield return null;
            var badges = UnityEngine.Object.FindObjectsByType<SignatureVfx.IceBadge>(FindObjectsSortMode.None);
            Line($"whiteout held={game.Signature.LastHeld} badges={badges.Length - badgesBefore} mobStaggered={mob.Staggered} bossStaggered={boss.Staggered}");
            Assert.IsTrue(mob.Staggered); Assert.IsFalse(boss.Staggered, "보스는 경직 없음");
            Assert.AreEqual(1, game.Signature.LastHeld);
            Assert.IsTrue(badges.All(b => b.Target != boss), "보스에는 배지 없음");
            Assert.IsTrue(badges.Any(b => b.Target == mob), "경직 성공 적에게만 배지");
            yield return new WaitForSeconds(0.25f);
            yield return Shots("sig_whiteout_badge", HudCanvas);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            yield return Shots("sig_whiteout_badge_low", HudCanvas, Sizes[0]);
        }

        [UnityTest]
        public IEnumerator Sidecar_Invulnerable5s_MoveBoost_ClearsAtEnd_AndLowEffects()
        {
            SignatureRevealOverlay.SkipForTests = true;
            yield return Start(Sidecar());
            float baseSpeed = game.Stats.MoveSpeed;
            Assert.IsTrue(game.TryUseSignature(), game.Signature.LastMessage);
            Assert.Greater(game.Stats.MoveSpeed, baseSpeed, "이동 속도 증가");
            game.Player.AutoInputOverride = Vector2.right;
            yield return new WaitForSeconds(0.5f);
            yield return Shots("sig_sidecar_moving", HudCanvas, Sizes[0]);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            yield return new WaitForSeconds(0.3f);
            yield return Shots("sig_sidecar_low", HudCanvas, Sizes[0]);
            game.Player.AutoInputOverride = null;
            yield return new WaitForSeconds(SignatureRuntime.SidecarSeconds);
            Assert.AreEqual(baseSpeed, game.Stats.MoveSpeed, 0.01f, "5초 뒤 이동 속도 원복");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<SignatureVfx.SidecarTrail>(FindObjectsSortMode.None).Length, "버프 종료 = 연출 제거");
        }
    }
}
