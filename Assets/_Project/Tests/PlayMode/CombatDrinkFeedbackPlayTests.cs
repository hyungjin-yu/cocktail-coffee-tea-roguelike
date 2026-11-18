using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-09 친구 피드백(직관성·타격감) 회귀: 음료 적용/발동 알림(출처·효과·정수·남은 시간, 합산·쿨다운, 상시/지속/발동 구분),
    /// 음료 사용 잔 연출, 적 피격 연출(일반/치명/강타/지속 피해 구분, 무적·0 피해 무표시, 다수 적 불꽃 예산,
    /// 전역 timeScale 불변·피격마다 카메라 흔들림 없음), 일시정지·설정 off 존중. 저장은 임시 폴더로 격리한다.
    /// </summary>
    public class CombatDrinkFeedbackPlayTests
    {
        GameManager game;
        readonly List<string> folders = new List<string>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            NewSave();
            SettingsService.SetForTests(Path.Combine(folders[0], "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            HitFeedback.ResetForTests();
            DrinkUseFx.ResetForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var f in folders) try { Directory.Delete(f, true); } catch { }
            folders.Clear();
        }

        void NewSave()
        {
            var f = Path.Combine(Application.temporaryCachePath, "combat-feedback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(f);
            folders.Add(f);
            PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
        }

        static DrinkRecipe Coffee(CoffeeBrewMethod m)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            r.name = "시험 커피";
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, m == CoffeeBrewMethod.EspressoMachine ? 180 : 150));
            return r;
        }

        IEnumerator StartRun(DrinkRecipe recipe)
        {
            if (game != null) { UnityEngine.Object.Destroy(game.gameObject); game = null; yield return null; }
            NewSave();
            if (recipe != null)
            {
                var craft = DrinkLab.Craft(recipe);
                Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
                Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            }
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            foreach (var w in game.Player.GetComponentsInChildren<RapierWeapon>()) w.enabled = false; // 자동 공격이 시험 적을 때리지 않게
            yield return null;
            game.Player.Coffee?.ResetStateForTests();
            foreach (var n in Numbers()) UnityEngine.Object.DestroyImmediate(n.gameObject); // 이전 시험의 숫자 제거
            foreach (var v in UnityEngine.Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            HitFeedback.ResetForTests();
            DrinkUseFx.ResetForTests();
        }

        Enemy Spawn(Vector2 offset, float hp = 1000f)
        {
            var def = new EnemyDef { name = "시험 몹", dropCount = 0, hp = hp, speed = 0f, radius = 0.4f, color = new Color(0.55f, 0.75f, 0.95f, 1f) };
            return Enemy.SpawnAt(game.Player.Position + offset, def, 1f, game.Player, game.transform);
        }

        static int CountSparks() => UnityEngine.Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)
            .Count(v => v.Renderer != null && v.Renderer.sprite != null && v.Renderer.sprite.name.StartsWith("hit_spark"));

        static DamageNumbers[] Numbers() => UnityEngine.Object.FindObjectsByType<DamageNumbers>(FindObjectsSortMode.None);

        // ── 피격 연출 ─────────────────────

        [UnityTest]
        public IEnumerator NormalCritHeavy_HaveDistinctTintNumberAndSound_NoGlobalHitstopOrShake()
        {
            yield return StartRun(null);
            var a = Spawn(new Vector2(3f, 0f));
            var b = Spawn(new Vector2(3f, 2f));
            var c = Spawn(new Vector2(3f, -2f));
            yield return null;
            Color baseColor = a.BaseColor;

            a.TakeDamage(5f, HitKind.Normal, a.transform.position);
            b.TakeDamage(5f, HitKind.Critical, b.transform.position);
            c.TakeDamage(5f, HitKind.Heavy, c.transform.position);

            Assert.AreEqual(1f, Time.timeScale, "전역 timeScale 히트스톱 없음");
            Assert.AreEqual(0f, CameraRig.CurrentShakeAmplitude, "직접 타격마다 카메라 흔들림 없음");
            Assert.IsTrue(a.HitTintActive && b.HitTintActive && c.HitTintActive);
            Assert.AreNotEqual(baseColor, a.CurrentBodyColor, "원본 아트에서도 보이는 색조(흰 곱셈 아님)");
            Assert.AreNotEqual(a.CurrentBodyColor, b.CurrentBodyColor, "치명 색조는 일반과 다르다");
            Assert.AreNotEqual(b.CurrentBodyColor, c.CurrentBodyColor, "강타 색조는 치명과 다르다");
            Assert.AreEqual(3, CountSparks(), "적중 위치 불꽃 3개");

            var nums = Numbers();
            var na = nums.Single(n => n.Kind == HitKind.Normal);
            var nb = nums.Single(n => n.Kind == HitKind.Critical);
            var nc = nums.Single(n => n.Kind == HitKind.Heavy);
            Assert.AreEqual("5", na.Text);
            Assert.AreEqual("5!", nb.Text, "치명타 숫자는 ! 표시");
            Assert.AreEqual("5", nc.Text);

            Assert.AreEqual(("hit_enemy", 1f), AudioService.ResolveForTests("hit_enemy"));
            Assert.AreEqual("hit_enemy", AudioService.ResolveForTests("hit_crit").clip, "치명음은 기존 음원 재사용");
            Assert.Greater(AudioService.ResolveForTests("hit_crit").pitch, 1f);
            Assert.Less(AudioService.ResolveForTests("hit_heavy").pitch, 1f);
            Assert.AreEqual("hit_crit", HitFeedback.Sound(HitKind.Critical));
            Assert.IsNull(HitFeedback.Sound(HitKind.Dot), "지속 피해는 무음");

            // 반동은 Visual만 잠깐 밀었다가 원위치(판정 루트 위치 불변).
            Vector3 rootBefore = a.transform.position;
            yield return null;
            Assert.AreEqual(rootBefore, a.transform.position);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(a.HitTintActive, "색조는 짧게 끝난다");
            Assert.AreEqual(Vector3.zero, a.VisualOffset, "반동 원위치");
            Assert.AreEqual(baseColor, a.CurrentBodyColor, "원래 색 복귀");
        }

        [UnityTest]
        public IEnumerator Invulnerable_ZeroDamage_ShowNoHitFeedback()
        {
            yield return StartRun(null);
            var e = Spawn(new Vector2(3f, 0f));
            yield return null;
            float hp = e.Hp;
            int sparks = CountSparks();

            e.Invulnerable = true;
            e.TakeDamage(50f, HitKind.Critical);
            Assert.AreEqual(hp, e.Hp);
            Assert.IsFalse(e.HitTintActive, "무적: 색조 없음");
            e.Invulnerable = false;

            e.TakeDamage(0f, HitKind.Normal);
            Assert.AreEqual(hp, e.Hp);
            Assert.IsFalse(e.HitTintActive, "0 피해: 색조 없음");
            Assert.AreEqual(sparks, CountSparks(), "무적·0 피해: 불꽃 없음");
            Assert.AreEqual(0, Numbers().Length, "무적·0 피해: 숫자 없음");
            Assert.AreEqual(Vector3.zero, e.VisualOffset, "반동 없음");
        }

        [UnityTest]
        public IEnumerator Dot_IsQuietAndRateLimited_SeparateFromDirectNumbers()
        {
            yield return StartRun(null);
            var e = Spawn(new Vector2(3f, 0f));
            yield return null;
            int sparks = CountSparks();
            e.TakeDamage(2f, HitKind.Dot);
            Assert.IsTrue(e.HitTintActive, "첫 틱은 짧은 색조");
            Assert.AreEqual(sparks, CountSparks(), "지속 피해는 불꽃 없음");
            Assert.AreEqual(Vector3.zero, e.VisualOffset, "지속 피해는 반동 없음");
            yield return new WaitForSeconds(0.12f); // 색조 끝(0.05초), 쿨다운(0.45초) 안
            Assert.IsFalse(e.HitTintActive);
            e.TakeDamage(2f, HitKind.Dot);
            Assert.IsFalse(e.HitTintActive, "쿨다운 안의 다음 틱은 색조 생략");

            e.TakeDamage(7f, HitKind.Normal);
            var nums = Numbers();
            // 지속 숫자와 직접 숫자는 절대 합치지 않는다(합산 창 0.12초 안의 같은 종류끼리만 합침 — 1초 틱은 따로 뜬다).
            Assert.AreEqual(7f, nums.Single(n => n.Kind == HitKind.Normal).Amount, 1e-4f, "직접 숫자에 지속 피해가 섞이지 않음");
            Assert.AreEqual(4f, nums.Where(n => n.Kind == HitKind.Dot).Sum(n => n.Amount), 1e-4f, "지속 숫자 합 = 지속 틱 합");
        }

        [UnityTest]
        public IEnumerator ManyEnemiesSameFrame_SparkBudgetCaps_EffectsLowHalves()
        {
            yield return StartRun(null);
            var list = new List<Enemy>();
            for (int i = 0; i < 24; i++) list.Add(Spawn(new Vector2(2f + (i % 6) * 0.9f, -2f + (i / 6) * 1.1f)));
            yield return null;
            HitFeedback.ResetForTests();
            int before = CountSparks();
            foreach (var e in list) e.TakeDamage(3f, HitKind.Normal);
            Assert.AreEqual(HitFeedback.MaxSparksPerFrame, CountSparks() - before, "한 프레임 불꽃 상한");
            Assert.IsTrue(list.All(e => e.HitTintActive), "예산과 무관하게 모든 적에 색조(누가 맞았는지 보임)");
            Assert.LessOrEqual(DamageNumbers.ActiveCount, DamageNumbers.MaxConcurrent);

            yield return new WaitForSeconds(0.5f); // 불꽃 소멸 + 창 경과
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            HitFeedback.ResetForTests();
            before = CountSparks();
            foreach (var e in list) e.TakeDamage(3f, HitKind.Critical);
            Assert.AreEqual(HitFeedback.MaxSparksPerFrame / 2, CountSparks() - before, "효과 낮음: 절반");
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesHitFeedback_And_SettingsOffRespected()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.MokaPot));
            var e = Spawn(new Vector2(1.2f, 0f));
            yield return null;

            SettingsService.SetDamageNumbers(false);
            e.TakeDamage(4f, HitKind.Critical, e.transform.position);
            Assert.AreEqual(0, Numbers().Length, "숫자 끄기: 숫자만 숨김");
            Assert.IsTrue(e.HitTintActive, "숫자를 꺼도 피격 색조는 그대로");

            game.OpenPause();
            Assert.AreEqual(0f, Time.timeScale);
            Vector3 offset = e.VisualOffset;
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsTrue(e.HitTintActive, "일시정지 중 피격 연출 시간이 흐르지 않는다");
            Assert.AreEqual(offset, e.VisualOffset);
            game.DrinkHud.ShowDrinkNotice("pause_test", "시험", "효과", DrinkNoticeKind.Instant, 0f, Color.white);
            StringAssert.DoesNotContain("시험 · 효과", game.DrinkHud.ProcText, "일시정지 중 새 알림 없음");
            Assert.IsFalse(game.DrinkHud.ProcVisible);
            game.ClosePause();
            // 재개 카운트다운(GameManager.ClosePause → ShowResumeCountdown) 동안은 계속 멈춰 있다 — 끝나면 1.
            float t0 = Time.realtimeSinceStartup;
            while (Time.timeScale < 1f && Time.realtimeSinceStartup - t0 < 6f) yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            SettingsService.SetDamageNumbers(true);

            // 흔들림 0%: 증기 폭발도 흔들지 않는다. 기본(50%)이면 폭발 한 번만 짧게.
            SettingsService.SetShakePercent(0);
            var rt = game.Player.Coffee;
            for (int i = 0; i < CoffeeBrewRuntime.MokaHeatMax; i++) rt.OnDirectHit(e);
            Assert.AreEqual(1, rt.Bursts);
            Assert.AreEqual(0f, CameraRig.CurrentShakeAmplitude, "흔들림 0%");
            yield return new WaitForSeconds(0.1f);
            SettingsService.SetShakePercent(50);
            for (int i = 0; i < CoffeeBrewRuntime.MokaHeatMax; i++) rt.OnDirectHit(e);
            Assert.AreEqual(2, rt.Bursts);
            Assert.Greater(CameraRig.CurrentShakeAmplitude, 0f, "증기 폭발은 설정 비율로 한 번");
        }

        // ── 음료 알림 ─────────────────────

        [UnityTest]
        public IEnumerator DrinkApplied_RunNotice_AndCupFx_ThenEspressoTimedNotice()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.EspressoMachine));
            yield return null;
            var hud = game.DrinkHud;
            // 런 시작: 상시 적용 알림이 첫 전투 화면에서 한 번(빌드 첫 프레임에 이미 떴을 수 있다 — 아직 보이거나 지나갔어야 함).
            if (hud.ProcKind == DrinkNoticeKind.Run)
            {
                StringAssert.StartsWith("[상시] 시험 커피 적용 · ", hud.ProcText);
                StringAssert.Contains("사거리 +", hud.ProcText, "실제 적용된 능력치(정수 %)");
                StringAssert.DoesNotContain(".", hud.ProcText.Replace("·", ""), "정수 표기");
            }
            // 에스프레소 샷: 지속 알림(출처 · 효과 · 남은 초), 잔 연출.
            DrinkUseFx.ResetForTests();
            var rt = game.Player.Coffee;
            rt.Step(1.01f, false);
            Assert.IsTrue(rt.ShotActive);
            Assert.IsNotNull(DrinkUseFx.LastCup, "샷 순간 잔이 머리 위로 떠오른다");
            Assert.LessOrEqual(DrinkUseFx.Life, 0.6f, "짧은 연출(입력 차단 없음)");
            float t0 = Time.realtimeSinceStartup;
            while (hud.ProcKind != DrinkNoticeKind.Timed && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            Assert.AreEqual(DrinkNoticeKind.Timed, hud.ProcKind);
            StringAssert.StartsWith("[지속 ", hud.ProcText);
            StringAssert.Contains("에스프레소 · 공격 속도 +30%", hud.ProcText);
            StringAssert.IsMatch(@"^\[지속 [0-3]초\]", hud.ProcText, "남은 시간은 정수 초");
        }

        [UnityTest]
        public IEnumerator RepeatedNotices_MergeAsCount_PourOverCooldownPreventsSpam()
        {
            yield return StartRun(Coffee(CoffeeBrewMethod.PourOver));
            var hud = game.DrinkHud;
            float t0 = Time.realtimeSinceStartup;
            while (hud.ProcVisible && Time.realtimeSinceStartup - t0 < 6f) yield return null; // 적용 알림 지나감
            Assert.IsFalse(hud.ProcVisible);

            for (int i = 0; i < 5; i++)
                hud.ShowDrinkNotice("drink_crit", hud.PreparedDrinkName, "치명타 · 피해 ×2", DrinkNoticeKind.Instant, 0f, Color.yellow);
            Assert.AreEqual(5, hud.ProcCount, "반복 발동은 한 알림 ×N");
            Assert.AreEqual("[발동] 시험 커피 · 치명타 · 피해 ×2 ×5", hud.ProcText);
            Assert.AreEqual(0, hud.ProcQueued);

            // 다른 알림은 버리지 않고 대기(최대 3).
            for (int i = 0; i < 6; i++) hud.ShowDrinkNotice("k" + i, "출처", "효과 " + i, DrinkNoticeKind.Instant, 0f, Color.white);
            Assert.AreEqual(DrinkRunHud.NoticeQueueMax, hud.ProcQueued);

            t0 = Time.realtimeSinceStartup;
            while (hud.ProcVisible && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsFalse(hud.ProcVisible, "대기열이 비면 사라진다");

            // 핸드 드립: 2·4초마다 단계가 바뀌어도 알림은 단계별 쿨다운에 한 번.
            var rt = game.Player.Coffee;
            int shown = 0;
            string last = "";
            for (int step = 0; step < 6 * 5 * 10; step++) // 30초(5주기)
            {
                rt.Step(0.1f, true);
                hud.TickNoticeForTests(0.1f, 0.1f);
                string t = hud.ProcText;
                if (t != last && (t.Contains("뜸") || t.Contains("붓기"))) shown++;
                last = t;
            }
            Assert.LessOrEqual(shown, 2 * Mathf.CeilToInt(30f / CoffeeBrewRuntime.PourNoticeCooldown) + 1, $"알림 {shown}회 — 단계 전환 10회보다 적게");
            Assert.GreaterOrEqual(shown, 1, "첫 단계 전환은 알린다");
        }

        [Test]
        public void NoticeFormat_DistinguishesRunTimedInstant_WithIntegers()
        {
            Assert.AreEqual("[상시] 라떼 적용 · 공격 +4%", DrinkRunHud.Format(DrinkNoticeKind.Run, "라떼 적용", "공격 +4%", 0f, 1));
            Assert.AreEqual("[지속 3초] 에스프레소 · 공격 속도 +30%", DrinkRunHud.Format(DrinkNoticeKind.Timed, "에스프레소", "공격 속도 +30%", 2.2f, 1));
            Assert.AreEqual("[발동] 모카포트 · 증기 폭발 ×2", DrinkRunHud.Format(DrinkNoticeKind.Instant, "모카포트", "증기 폭발", 0f, 2));
        }
    }
}
