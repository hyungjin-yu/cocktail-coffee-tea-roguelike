#if CCT_QA_TOUR
using System.Collections;
using System.Linq;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-16 QA 전용(CCT_QA_TOUR) "feedback4" 모드 — 19 화살 속성 표현, 25 히든 칸 모티프, 26 선택형 취기 표시 실제 화면.
    /// 강제 경로(보고 표기): 스폰 정지·체력 증가, 화염/여운 Lv 직접 지정(카드와 같은 스탯 필드), 무피해 정지 표적,
    /// 탄이 날아가는 순간 timeScale 0으로 멈춰 촬영, 히든은 ForceForTests, 칵테일 재료는 격리 저장에 직접 추가 후 실제 제조·출발 예약 함수.
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator Feedback4Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string H = $"{Screen.width}x{Screen.height}";
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("feedback4: no title"); yield break; }

            // ── 26 설정 화면(기본 꺼짐 → 켬) ──
            title.SettingsView.Show(); yield return Wait(0.5f);
            Line($"F4 settings tipsy default='{title.SettingsView.TipsyLabelText}' value={SettingsService.Current.tipsyBadge}");
            if (SettingsService.Current.tipsyBadge) Fail("feedback4: tipsy default on");
            yield return Shot($"f4_{H}_01_settings_tipsy_off");
            title.SettingsView.ToggleTipsyBadge(); yield return Wait(0.3f);
            Line($"F4 settings tipsy after toggle='{title.SettingsView.TipsyLabelText}' value={SettingsService.Current.tipsyBadge}");
            yield return Shot($"f4_{H}_02_settings_tipsy_on");
            title.SettingsView.Hide(); yield return Wait(0.4f);

            // ── 19 활 화살: 기본 / 화염 / 여운 / 둘 다 ──
            title = FindFirstObjectByType<TitleView>();
            if (title != null) title.RequestStart();
            yield return StartCombo(Combos[3], false);
            var game = GameManager.Instance;
            if (game == null) { Fail("feedback4: no bow run"); yield break; }
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(100000f);
            FreezeWorld(game); yield return Wait(0.4f);
            var cases = new[] { ("base", 0, 0), ("flame", 1, 0), ("bitter", 0, 1), ("both", 1, 1) };
            for (int c = 0; c < cases.Length; c++)
            {
                var (tag, flame, bitter) = cases[c];
                game.Stats.flameLevel = flame; game.Stats.poisonLevel = bitter;
                SpawnDummy(game, 3.2f); // 활 탐지 거리(사거리+3) 안
                float until = Time.realtimeSinceStartup + 4f;
                PlayerProjectile shot = null;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    shot = FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None).FirstOrDefault(p => p.transform.position.x > game.Player.Position.x + 0.9f);
                    if (shot != null) break;
                }
                if (shot == null) { Fail("feedback4: no arrow " + tag); continue; }
                Time.timeScale = 0f;
                var sr = shot.GetComponent<SpriteRenderer>();
                var tail = shot.transform.Find("ElementTail");
                Line($"F4 arrow {tag}: element={PlayerProjectile.ArrowElementOf(game.Stats)} color={sr.color} tail={(tail != null ? tail.GetComponent<SpriteRenderer>().color.ToString() : "none")} scale={shot.transform.localScale.x:0.0000}");
                yield return Shot($"f4_{H}_1{c}_arrow_{tag}", game);
                Time.timeScale = 1f;
                KillDummy();
                foreach (var p in FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
                yield return Wait(0.3f);
            }
            game.Stats.flameLevel = 0; game.Stats.poisonLevel = 0;

            // ── 25 히든 칸 모티프(충전·준비·누적·조건) ──
            foreach (var s in new[] { HiddenSkill.IcedCup, HiddenSkill.SodaPump, HiddenSkill.ShakeRhythm, HiddenSkill.HerbGarden, HiddenSkill.ThickMug })
                game.Player.Hidden.ForceForTests(s);
            game.Player.HiddenRuntime.AfterDirectHit(null); game.Player.HiddenRuntime.AfterDirectHit(null);
            yield return Wait(1.2f); // 정지 0.5초 → 허브 칸
            var strip = game.Hud.EffectsStrip;
            Line($"F4 hidden tiles visible={strip.VisibleTiles} overflow={strip.Overflow} ids=[{string.Join(",", strip.Current.Select(e => e.id + (e.icon != null ? "+icon" : "")))}]");
            yield return Shot($"f4_{H}_20_hidden_tiles", game);
            game.GoToTitle(); yield return Wait(1f);

            // ── 25 히든 칸 B(2026-11-17 전용 모티프 9종 전부 화면): 바텐더 런에서 주말 러시(처치 직후)·각성(HP 35% 이하)·바 템포(이동/정지)·콤보 ──
            title = FindFirstObjectByType<TitleView>();
            if (title != null) title.RequestStart();
            yield return StartCombo(Combos[0], false);
            game = GameManager.Instance;
            if (game != null)
            {
                FreezeWorld(game); yield return Wait(0.4f);
                foreach (var s in new[] { HiddenSkill.WeekendRush, HiddenSkill.BitterWake, HiddenSkill.BarTempo, HiddenSkill.ShakeRhythm })
                    game.Player.Hidden.ForceForTests(s);
                for (int hit = 0; hit < 40 && game.Player.Hp > game.Player.MaxHp * 0.3f; hit++) game.Player.TakeZoneDamage(game.Player.MaxHp * 0.05f); // 각성 조건 HP 35% 이하 — 장판 피해 경로(단발 무적 없음), 사망 전 멈춤
                game.Player.AutoInputOverride = new Vector2(1f, 0f); yield return Wait(0.3f); // 이동 중 = 템포 이동 그림
                game.Player.HiddenRuntime.OnEnemyKilled(EnemyDef.Imp); game.Player.HiddenRuntime.AfterDirectHit(null);
                yield return Wait(0.15f);
                var stripB = game.Hud.EffectsStrip;
                Line($"F4 hidden tiles B (moving) hp={game.Player.Hp:0}/{game.Player.MaxHp:0} ids=[{string.Join(",", stripB.Current.Select(e => e.id + (e.icon != null ? "+" + e.icon.name : "")))}]");
                yield return Shot($"f4_{H}_21_hidden_tiles_moving", game);
                game.Player.AutoInputOverride = Vector2.zero; yield return Wait(0.8f); // 정지 0.5초 이상 = 템포 정지 그림
                Line($"F4 hidden tiles B (standing) ids=[{string.Join(",", stripB.Current.Select(e => e.id + (e.icon != null ? "+" + e.icon.name : "")))}]");
                yield return Shot($"f4_{H}_22_hidden_tiles_standing", game);
                game.Player.AutoInputOverride = null;
                game.GoToTitle(); yield return Wait(1f);
            }

            // ── 26 끝없는 모드 + 칵테일(실제 제조·출발 예약) / 기본 모드 대조 ──
            foreach (var mode in new[] { RunMode.Endless, RunMode.Standard })
            {
                var r = DrinkLab.PracticeRecipe.Clone();
                foreach (var st in r.steps) { int have = DrinkLab.Data.Owned(st.id); if (have < st.amount) DrinkLab.Data.Add(st.id, st.amount - have); }
                if (DrinkLab.Prepared != null) DrinkLab.DiscardPrepared(out _);
                var craft = DrinkLab.Craft(r);
                var dep = DrinkLab.ReserveForDeparture(out var depErr);
                Line($"F4 {mode} craft={craft.Status} depart={dep} {depErr}");
                RunModeSelection.Current = mode;
                title = FindFirstObjectByType<TitleView>();
                if (title != null) title.RequestStart();
                yield return StartCombo(Combos[0], false);
                game = GameManager.Instance;
                if (game == null) { Fail("feedback4: no run " + mode); continue; }
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(100000f);
                game.Master?.StartBattle();
                FreezeWorld(game); yield return Wait(0.8f);
                string stats = $"{game.Stats.DamageBonus:0.000}/{game.Stats.MoveSpeed:0.00}";
                bool shown = ActiveEffects.TipsyShown(game);
                Line($"F4 {mode} drunk={game.Player.DrunkDrink} tipsyShown={shown} stats={stats} tiles=[{string.Join(",", game.Hud.EffectsStrip.Current.Select(e => e.id))}]");
                if (shown != (mode == RunMode.Endless)) Fail($"feedback4: tipsy {mode} shown={shown}");
                yield return Shot($"f4_{H}_3{(mode == RunMode.Endless ? 0 : 1)}_tipsy_{mode}", game);
                if (mode == RunMode.Endless)
                {
                    SettingsService.SetTipsyBadge(false); yield return Wait(0.3f);
                    string after = $"{game.Stats.DamageBonus:0.000}/{game.Stats.MoveSpeed:0.00}";
                    Line($"F4 endless tipsy off shown={ActiveEffects.TipsyShown(game)} stats={after} same={stats == after}");
                    SettingsService.SetTipsyBadge(true);
                    game.OpenPause(); yield return Wait(0.4f);
                    Line($"F4 paused strip visible={game.Hud.EffectsStrip.Visible}");
                    yield return Shot($"f4_{H}_32_tipsy_paused");
                    game.ClosePause(); yield return Wait(0.3f);
                }
                game.GiveUp(); yield return Wait(1.5f);
                Line($"F4 after give up shown={ActiveEffects.TipsyShown(game)}");
                game.GoToTitle(); yield return Wait(1f);
            }
            RunModeSelection.Current = RunMode.Standard;
        }
    }
}
#endif
