#if CCT_QA_TOUR
using System.Collections;
using System.Linq;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-12 QA 전용(CCT_QA_TOUR) "ui-states" 모드 — 지난 점검표에서 NOT RUN/FAIL로 남은 상태를 실제 빌드에서 찍는다.
    /// 정규 모드 범례(마신 음료 없음/있음), 정규 보스전(범례 접힘 + 상성 칩, 보스 체력바 겹침 계산), 히든 0→1개 배지·히든 상세(1개/여러 개),
    /// 바텐더 혼합 러시 강화 중(실제 카드 받기 경로), 목표 조합 + 카드 선택 창, 목표 카드 HUD, 목표 바꾸기 창, 티 소믈리에 범례·깊은 우림,
    /// 최근 기록(빈 목록 → 기록 생김 → 상세 → 닫고 재진입).
    /// 강제 경로(보고 표기): 스폰 정지(FreezeWorld), 체력 크게, 마신 음료는 Player.DrunkDrink 직접 지정, 히든은 ForceForTests,
    /// 보스는 BossDirector.SpawnNow, 카드는 GrantCardForTests(실제 선택과 같은 중첩·러시·히든 판정).
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator UiStatesRun()
        {
            var args = System.Environment.GetCommandLineArgs();
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string H = $"{Screen.width}x{Screen.height}";
            Line($"US screen {H} {Screen.fullScreenMode}");
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("ui-states: no title"); yield break; }

            // 최근 기록 — 빈 목록(첫 실행 데이터)
            title.OpenHistory(); yield return Wait(0.4f);
            Line($"US history empty: open={title.HistoryView.IsOpen} rows={title.HistoryView.RowCount} empty={title.HistoryView.EmptyVisible} count='{title.HistoryView.CountText}'");
            yield return Shot($"us_{H}_01_history_empty");
            title.HistoryView.HandleEscape(); yield return Wait(0.3f);
            if (!title.IsOpen) Fail("ui-states: title not restored after empty history");

            foreach (var combo in new[] { Combos[0], Combos[3] })
            {
                title = FindFirstObjectByType<TitleView>();
                title.RequestStart(); yield return StartCombo(combo, false);
                var game = GameManager.Instance;
                if (game == null) { Fail("ui-states: no game " + combo.label); continue; }
                string tag = $"us_{H}_{combo.label}";
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
                game.Player.Heal(1f);
                FreezeWorld(game);
                yield return Wait(0.6f);

                game.Player.DrunkDrink = null; game.Hud.Legend.RefreshForTests();
                LogLegend(game, tag + "_nodrink");
                yield return Shot(tag + "_10_legend_nodrink", game);
                var drink = combo.job == Job.Bartender ? DrinkKind.Cocktail : DrinkKind.Tea;
                game.Player.DrunkDrink = drink; game.Hud.Legend.RefreshForTests();
                yield return Wait(0.3f);
                LogLegend(game, tag + "_drink");
                yield return Shot(tag + "_11_legend_drink", game);

                // 커피/우림 연구 트리(노드 그림 없음 — 승인 아트 부족 보고 근거)
                game.OpenCoffeeTree(); yield return Wait(0.4f);
                Line($"US {tag} study tree open={game.SkillView.IsOpen}");
                yield return Shot(tag + "_15_study_tree");
                game.RequestEscape(); yield return Wait(0.4f);
                yield return ClearOverlays(game, tag + " after study");

                // 정규 보스전 — 범례 접힘, 상성 칩
                var schedule = BossDirector.DefaultSchedule(combo.job);
                var boss = game.Director.SpawnNow(schedule[0]);
                yield return Wait(Balance.BossArrivalGraceSeconds + 0.5f);
                game.Hud.Legend.RefreshForTests();
                yield return Wait(0.6f);
                LogLegend(game, tag + "_boss");
                yield return Shot(tag + "_12_boss_chip", game);
                game.Player.DrunkDrink = null; game.Hud.Legend.RefreshForTests(); yield return Wait(0.6f);
                LogLegend(game, tag + "_boss_nodrink");
                yield return Shot(tag + "_13_boss_chip_nodrink", game);
                game.Player.DrunkDrink = drink;
                for (int i = 0; i < 20 && boss != null && !boss.IsDead; i++) { boss.TakeDamage(1e9f); yield return null; }
                yield return Wait(0.8f);
                yield return ClearOverlays(game, tag + " boss dead");
                FreezeWorld(game);
                game.Hud.Legend.RefreshForTests(); yield return Wait(0.6f);
                LogLegend(game, tag + "_after_boss");
                yield return Shot(tag + "_14_legend_after_boss", game);

                // 히든 배지 0 → 1, 상세
                Line($"US {tag} hidden before={game.Player.Hidden.Count} badge='{game.Hud.HiddenBadgeText}'");
                var first = HiddenSkillDatabase.All.FirstOrDefault(d => d.AvailableFor(combo.job) && !game.Player.Hidden.Has(d.skill));
                if (first != null) game.Player.Hidden.ForceForTests(first.skill);
                yield return Wait(0.5f);
                Line($"US {tag} hidden after={game.Player.Hidden.Count} badge='{game.Hud.HiddenBadgeText}' forced={first?.id}");
                if (game.Hud.HiddenBadgeText != game.Player.Hidden.Count.ToString()) Fail($"{tag} hidden badge mismatch");
                yield return Shot(tag + "_20_hidden1_badge", game);
                game.OpenHiddenDetails(); yield return Wait(0.4f);
                Line($"US {tag} hidden details open={game.Hud.HiddenDetailsOpen}");
                if (!game.Hud.HiddenDetailsOpen) Fail($"{tag} hidden details did not open");
                yield return Shot(tag + "_21_hidden_details_1");
                game.RequestEscape(); yield return Wait(0.4f);
                if (game.Hud.HiddenDetailsOpen || game.IsPaused) Fail($"{tag} hidden details Escape did not resume");

                // 혼합 러시 강화 중(바텐더) / 깊은 우림(티 소믈리에) — 실제 카드 받기 경로
                var category = combo.job == Job.Bartender ? CardCategory.Cocktail : CardCategory.Tea;
                var offers = game.Cards.Draw(category, !combo.weaponA, 3);
                if (combo.job == Job.Bartender) foreach (var o in offers) game.GrantCardForTests(o.card);
                else if (offers.Count > 0) for (int k = 0; k < 4; k++) game.GrantCardForTests(offers[0].card);
                yield return Wait(0.5f);
                Line($"US {tag} job trait='{game.JobTraitStatus.Replace('\n', '/')}' rush={F(game.MixingRushRemaining)} deep={game.DeepSteepLevel} pips={game.Hud.JobPipsFilled} granted=[{string.Join(",", offers.Select(o => o.card.id))}]");
                if (combo.job == Job.Bartender && game.MixingRushRemaining <= 0f) Fail($"{tag} mixing rush not active after 3 cocktail kinds");
                yield return Shot(tag + "_22_job_active", game);

                // 목표 조합 → 카드 선택 창 → 목표 바꾸기 창 → 선택 후 HUD 목표 카드
                var target = HiddenSkillDatabase.All.FirstOrDefault(d => game.CanTarget(d.skill));
                if (target != null) game.SetComboTarget(target.skill);
                var gauge = combo.job == Job.TeaSommelier ? game.TeaGauge : game.CocktailGauge;
                game.AddIngredient(combo.job == Job.TeaSommelier ? IngredientType.Tea : IngredientType.Cocktail, gauge.Threshold - gauge.Current);
                yield return Wait(0.6f);
                Line($"US {tag} combo target={(target != null ? target.id : "none")} cardOpen={game.CardView.IsOpen}");
                if (game.CardView.IsLearningOpen)
                {
                    Line($"US {tag} first card guide open (first-run education)");
                    yield return Shot(tag + "_29_card_guide_first");
                    game.CardView.CloseLearning(); yield return Wait(0.4f);
                }
                if (game.CardView.IsOpen)
                {
                    foreach (var def in HiddenSkillDatabase.All)
                    {
                        if (!game.CanTarget(def.skill)) continue;
                        if (!game.CardView.Offers.Any(o => HiddenRequirements.CardAdvances(def, game.Job, game.HiddenState, o.card.id, out _, out _))) continue;
                        game.SetComboTarget(def.skill); game.CardView.RefreshTargetInfo();
                        Line($"US {tag} target switched to {def.id} (matches an offered card)");
                        break;
                    }
                    Line($"US {tag} card target='{game.CardView.TargetLineText}' influence=[{game.CardView.InfluenceText(0)}|{game.CardView.InfluenceText(1)}|{game.CardView.InfluenceText(2)}]");
                    yield return Shot(tag + "_30_card_select_target");
                    game.OpenComboTarget(); yield return Wait(0.4f);
                    yield return Shot(tag + "_31_combo_target_view");
                    game.RequestEscape(); yield return Wait(0.3f);
                    game.CardView.Choose(0); yield return Wait(0.5f);
                    for (int k = 0; k < 10 && game.CardView.IsOpen; k++) { if (game.CardView.IsLearningOpen) game.CardView.CloseLearning(); else game.CardView.Choose(0); yield return Wait(0.3f); }
                }
                else if (game.ComboTarget.HasValue == false) Line($"US {tag} no targetable hidden left (all forced)");
                else Fail($"{tag} card window did not open");
                yield return ClearOverlays(game, tag + " after card");
                Line($"US {tag} hud goal card shown={game.Hud.ComboCardShown} text='{game.Hud.ComboTargetText.Replace('\n', '/')}'");
                yield return Shot(tag + "_32_hud_goal_card", game);

                // 히든 여러 개 → 상세 긴 목록
                foreach (var d in HiddenSkillDatabase.All) if (d.AvailableFor(combo.job)) game.Player.Hidden.ForceForTests(d.skill);
                yield return Wait(0.3f);
                game.OpenHiddenDetails(); yield return Wait(0.4f);
                Line($"US {tag} hidden many={game.Player.Hidden.Count} badge='{game.Hud.HiddenBadgeText}' open={game.Hud.HiddenDetailsOpen}");
                yield return Shot(tag + "_23_hidden_details_many");
                game.RequestEscape(); yield return Wait(0.4f);

                game.GiveUp(); yield return Wait(0.6f);
                yield return Shot(tag + "_40_result");
                var res = game.ResultScreen;
                if (res != null && res.DrinkAwardButton != null && res.DrinkAwardButton.gameObject.activeInHierarchy)
                {
                    res.DrinkAwardButton.onClick.Invoke(); yield return Wait(0.4f);
                    Line($"US {tag} award detail open={res.AwardDetail.IsOpen}");
                    if (!res.AwardDetail.IsOpen) Fail($"{tag} award detail did not open");
                    yield return Shot(tag + "_41_award_detail");
                    res.AwardDetail.Close(); yield return Wait(0.3f);
                }
                else Line($"US {tag} award button not shown");
                if (res != null && res.BuildDetailButton != null && res.BuildDetailButton.gameObject.activeInHierarchy)
                {
                    res.BuildDetailButton.onClick.Invoke(); yield return Wait(0.4f);
                    yield return Shot(tag + "_42_build_detail");
                    var rd = res.Detail != null && res.Detail.IsOpen ? res.Detail : null;
                    Line($"US {tag} build detail open={(rd != null)}");
                    if (rd != null) rd.Close(); yield return Wait(0.3f);
                }
                game.GoToTitle(); yield return Wait(0.9f);
            }

            // 최근 기록 — 기록 2개, 상세, 닫고 재진입
            title = FindFirstObjectByType<TitleView>();
            title.OpenHistory(); yield return Wait(0.4f);
            Line($"US history: rows={title.HistoryView.RowCount} empty={title.HistoryView.EmptyVisible} count='{title.HistoryView.CountText}'");
            if (title.HistoryView.RowCount < 2) Fail("ui-states: history rows < 2 after two runs");
            yield return Shot($"us_{H}_50_history_list");
            if (title.HistoryView.RowCount > 0)
            {
                title.HistoryView.Row(0).onClick.Invoke(); yield return Wait(0.4f);
                Line($"US history detail open={title.HistoryView.Detail.IsOpen}");
                yield return Shot($"us_{H}_51_history_detail");
                title.HistoryView.HandleEscape(); yield return Wait(0.3f);
                Line($"US history after detail close: listOpen={title.HistoryView.IsOpen} detailOpen={title.HistoryView.Detail.IsOpen}");
            }
            title.HistoryView.HandleEscape(); yield return Wait(0.3f);
            title.OpenHistory(); yield return Wait(0.4f);
            Line($"US history reenter rows={title.HistoryView.RowCount}");
            yield return Shot($"us_{H}_52_history_reenter");
            title.HistoryView.HandleEscape(); yield return Wait(0.3f);
            if (!title.IsOpen) Fail("ui-states: title not restored after history");
        }

        /// <summary>범례/보스 칩/보스 체력바의 화면 픽셀 사각형과 겹침을 기록한다.</summary>
        void LogLegend(GameManager game, string tag)
        {
            var lg = game.Hud.Legend;
            Rect legendR = ScreenRect(lg.transform as RectTransform), chipR = ScreenRect(lg.BossChipRect);
            var barT = FindDeep(lg.transform.parent, "BossBar") as RectTransform;
            bool barOn = barT != null && barT.gameObject.activeInHierarchy;
            Rect barR = barOn ? ScreenRect(barT) : default;
            bool overlap = barOn && lg.BossChipVisible && chipR.Overlaps(barR);
            bool legendOverBar = barOn && lg.LegendVisible && legendR.Overlaps(barR);
            var target = lg.transform.Find("TargetIcon") as RectTransform;
            Line($"US LEGEND {tag}: famSprite={(lg.transform.Find("FamRep0") is RectTransform f0 && f0.GetComponent<UnityEngine.UI.Image>().sprite != null ? f0.GetComponent<UnityEngine.UI.Image>().sprite.name : "-")} legendVisible={lg.LegendVisible} legendPx={R(legendR)} targetIconPx={(target != null ? F(ScreenRect(target).height) : "-")} " +
                 $"chipVisible={lg.BossChipVisible} chipPx={R(chipR)} chip='{lg.BossChipText}' bossBar={(barOn ? R(barR) : "off")} chipOverBar={overlap} legendOverBar={legendOverBar} text='{lg.AffinityText}'");
            if (overlap || legendOverBar) Fail($"{tag} legend/chip overlaps boss bar");
        }

        static string R(Rect r) => $"({F(r.x)},{F(r.y)} {F(r.width)}x{F(r.height)})";

        static Rect ScreenRect(RectTransform rt)
        {
            if (rt == null) return default;
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
#endif
