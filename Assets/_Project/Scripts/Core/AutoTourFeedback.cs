#if CCT_QA_TOUR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 QA 전용(CCT_QA_TOUR) "feedback1" 모드 — 플레이 피드백 첫 묶음(2·3·6·8·9·10·17·18·22)과 23(결과 흐름)의 실제 화면 캡처.
    /// 강제 경로(보고 표기): 카드 발견은 PermanentProgress.DiscoverCard(격리 저장), 카드 창은 CardView.Show 직접 제시, 연구 포인트는 원두 지급
    /// (AddIngredient), 스폰 정지(FreezeWorld)·체력 증가. 사람 입력 아님.
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator Feedback1Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string H = $"{Screen.width}x{Screen.height}";
            Line($"FB screen {H}");
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("feedback1: no title"); yield break; }

            // ── 2·3·10 카드 도감: 전부 미발견 → 미발견 상세 → 일부 발견 → 발견 상세(출처) ──
            Click(title.transform, "CardCodexButton"); yield return Wait(0.5f);
            var codex = title.CodexView;
            yield return Shot($"fb_{H}_01_codex_locked");
            if (codex.OpenDetailFor("dry_gin")) { yield return Wait(0.3f); Line($"FB codex locked detail title='{codex.DetailTitleText}'"); yield return Shot($"fb_{H}_02_codex_locked_detail"); codex.HideDetail(); }
            else Fail("feedback1: dry_gin row missing");
            if (string.IsNullOrEmpty(MobileBackRouter.HandleBack())) Click(codex.transform, "BackButton");
            yield return Wait(0.4f);
            foreach (var c in CardDatabase.LoadAll().Where((c, i) => i % 2 == 0)) PermanentProgress.DiscoverCard(c.id); // 강제: 절반 발견
            Click(title.transform, "CardCodexButton"); yield return Wait(0.5f);
            yield return Shot($"fb_{H}_03_codex_mixed");
            var multi = CardDatabase.LoadAll().FirstOrDefault(c => PermanentProgress.Current.discoveredCardIds.Contains(c.id) && CardSourceList.For(c).Count >= 2)
                        ?? CardDatabase.LoadAll().First(c => PermanentProgress.Current.discoveredCardIds.Contains(c.id));
            if (codex.OpenDetailFor(multi.id))
            {
                yield return Wait(0.3f);
                Line($"FB codex detail {multi.id} sources={CardSourceList.For(multi).Count} buttons={codex.DetailSourceButtons.Count}");
                yield return Shot($"fb_{H}_04_codex_detail_top_{multi.id}");
                var sr = codex.transform.Find("Detail/DetailScroll")?.GetComponent<ScrollRect>();
                if (sr != null) { sr.verticalNormalizedPosition = 0f; yield return Wait(0.2f); }
                yield return Shot($"fb_{H}_05_codex_detail_sources_{multi.id}");
                if (codex.DetailSourceButtons.Count > 0)
                {
                    codex.DetailSourceButtons[0].onClick.Invoke(); yield return Wait(0.2f);
                    Line($"FB copy -> clipboard='{GUIUtility.systemCopyBuffer}' expected='{multi.sourceUrl}' match={GUIUtility.systemCopyBuffer == multi.sourceUrl}");
                    if (GUIUtility.systemCopyBuffer != multi.sourceUrl) Fail("feedback1: copy mismatch");
                    yield return Shot($"fb_{H}_06_codex_source_copied");
                }
                codex.HideDetail();
            }
            if (string.IsNullOrEmpty(MobileBackRouter.HandleBack())) Click(codex.transform, "BackButton");
            yield return Wait(0.4f);

            // ── 2·3·6 재료 도감(로비 음료 제조 → 재료 도감 탭) ──
            title = FindFirstObjectByType<TitleView>();
            title.RequestStart();
            CharacterSelectView select = null;
            for (int k = 0; k < 20 && select == null; k++) { yield return Wait(0.3f); select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select != null && select.DrinkLabButton != null)
            {
                select.DrinkLabButton.onClick.Invoke();
                DrinkLabView lab = null;
                for (int k = 0; k < 20 && lab == null; k++) { yield return Wait(0.3f); lab = FindFirstObjectByType<DrinkLabView>(); }
                if (lab != null)
                {
                    lab.SetTab(DrinkLabView.Tab.Codex); yield return Wait(0.5f);
                    Line($"FB ingcodex all: {lab.CodexCountText} visible={lab.CodexVisibleIds.Count}");
                    yield return Shot($"fb_{H}_10_ingcodex_all");
                    lab.CodexScroll.verticalNormalizedPosition = 0f; yield return Wait(0.2f);
                    yield return Shot($"fb_{H}_11_ingcodex_bottom");
                    lab.SetCodexKind(DrinkLabView.CodexKind.Coffee); yield return Wait(0.3f);
                    Line($"FB ingcodex coffee: {lab.CodexCountText}");
                    yield return Shot($"fb_{H}_12_ingcodex_coffee");
                    lab.SetCodexKind(DrinkLabView.CodexKind.All); lab.SetCodexSeen(DrinkLabView.CodexSeen.Unseen); yield return Wait(0.3f);
                    Line($"FB ingcodex unseen: {lab.CodexCountText}");
                    yield return Shot($"fb_{H}_13_ingcodex_unseen");
                    var unseen = DrinkIngredients.All.FirstOrDefault(d => !DrinkLab.Seen(d.id));
                    if (unseen != null) { lab.OpenCodexDetail(unseen); yield return Wait(0.3f); yield return Shot($"fb_{H}_14_ingcodex_detail_unseen"); lab.CloseCodexDetail(); }
                    lab.ResetCodexFilters();
                    lab.SetCodexQuery("진"); yield return Wait(0.3f);
                    Line($"FB ingcodex query 진: {lab.CodexCountText} ids=[{string.Join(",", lab.CodexVisibleIds)}]");
                    yield return Shot($"fb_{H}_15_ingcodex_search");
                    lab.SetCodexQuery("없는재료xyz"); yield return Wait(0.3f);
                    Line($"FB ingcodex empty: {lab.CodexCountText}");
                    yield return Shot($"fb_{H}_16_ingcodex_empty");
                    lab.ResetCodexFilters(); yield return Wait(0.2f);
                    var seen = DrinkIngredients.All.FirstOrDefault(d => DrinkLab.Seen(d.id) && !string.IsNullOrEmpty(d.culture)) ?? DrinkIngredients.All.First(d => DrinkLab.Seen(d.id));
                    lab.OpenCodexDetail(seen); yield return Wait(0.3f);
                    yield return Shot($"fb_{H}_17_ingcodex_detail_{seen.id}");
                    lab.CloseCodexDetail(); yield return Wait(0.2f);
                    // 2026-11-16 최종 아트 반입 확인(강제: 격리 저장에 산지 원두 28종 각 1g 지급 = 발견 처리) — 원색 산지 아이콘·여백.
                    int given = 0;
                    foreach (var d in DrinkIngredients.All) if (d.IsCoffeeBean && d.iconId != "dl_icon_coffee_bean") { DrinkLab.Data.Add(d.id, 10); given++; }
                    lab.SetCodexKind(DrinkLabView.CodexKind.Coffee); yield return Wait(0.4f);
                    Line($"FB ingcodex origins seen (forced {given}): {lab.CodexCountText}");
                    yield return Shot($"fb_{H}_18_ingcodex_coffee_origins_seen");
                    lab.CodexScroll.verticalNormalizedPosition = 0f; yield return Wait(0.2f);
                    yield return Shot($"fb_{H}_19_ingcodex_coffee_origins_seen_bottom");
                    var origin = DrinkIngredients.Get("coffee_jamaica_blue_mountain");
                    if (origin != null) { lab.OpenCodexDetail(origin); yield return Wait(0.3f); yield return Shot($"fb_{H}_20_ingcodex_detail_origin"); lab.CloseCodexDetail(); }
                    lab.ResetCodexFilters(); yield return Wait(0.2f);
                    Line($"FB ingcodex after close: detailOpen={lab.CodexDetailOpen} visible={lab.CodexVisibleIds.Count}");
                    lab.Close(); yield return Wait(0.8f);
                }
                else Fail("feedback1: DrinkLabView did not open");
            }
            else Fail("feedback1: no drink lab button");

            // ── 8·9·10 카드 선택 + 17·18 연구 트리 + 22·23 결과(두 직업) ──
            foreach (var combo in new[] { Combos[0], Combos[3] })
            {
                select = FindFirstObjectByType<CharacterSelectView>();
                if (select == null) { title = FindFirstObjectByType<TitleView>(); if (title != null) title.RequestStart(); }
                yield return StartCombo(combo, false);
                var game = GameManager.Instance;
                if (game == null) { Fail("feedback1: no game"); continue; }
                string tag = $"fb_{H}_{combo.label}";
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
                game.Player.Heal(1f);
                FreezeWorld(game);
                yield return Wait(0.5f);
                var category = combo.job == Job.Bartender ? CardCategory.Cocktail : CardCategory.Tea;
                var pool = CardDatabase.LoadAll().Where(c => c.category == category).ToList();
                // 짧은 효과·긴 효과·최대 효과가 있는 카드를 섞어 제시
                var picks = pool.OrderByDescending(c => CardEducationSummary.Effect(c).Length).Take(1)
                    .Concat(pool.OrderBy(c => CardEducationSummary.Effect(c).Length).Take(1))
                    .Concat(pool.Where(c => AttackFinalForms.ForCard(c.id).HasValue).Take(1)).Distinct().Take(3).ToList();
                while (picks.Count < 3) picks.Add(pool.First(c => !picks.Contains(c)));
                var offers = picks.Select(c => new CardOffer { card = game.Cards.FindInPool(c.id) ?? c, rarityPercent = 60f, currentStacks = 0 }).ToList();
                game.CardView.Show(offers, _ => { });
                yield return WaitCardSettled(game);
                if (game.CardView.IsLearningOpen) { game.CardView.CloseLearning(); yield return Wait(0.3f); }
                Line($"FB {tag} card window [{string.Join(",", offers.Select(o => o.card.id + "='" + CardEducationSummary.Effect(o.card) + "'"))}] hint='{game.CardView.KeyHintLabel}'");
                yield return Shot($"{tag}_20_card_select");
                game.CardView.OpenLearning(0); yield return Wait(0.4f);
                yield return Shot($"{tag}_21_card_learning_top");
                var learnScroll = game.CardView.transform.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(s => s.gameObject.activeInHierarchy && s.name == "Scroll");
                if (learnScroll != null) { learnScroll.verticalNormalizedPosition = 0f; yield return Wait(0.2f); }
                yield return Shot($"{tag}_22_card_learning_sources");
                game.CardView.CloseLearning(); yield return Wait(0.3f);
                game.CardView.Choose(0); yield return Wait(0.3f);
                yield return ClearOverlays(game, tag + " after card");

                // 연구 트리: 조회 전용(포인트 0) → 원두 지급으로 실제 보상 창(배분 모드, 단축키 안내)
                game.OpenCoffeeTree(); yield return Wait(0.4f);
                yield return Shot($"{tag}_30_study_readonly");
                game.RequestEscape(); yield return Wait(0.4f);
                yield return ClearOverlays(game, tag + " after study ro");
                game.AddIngredient(IngredientType.Coffee, 30);
                for (int k = 0; k < 15 && !game.SkillView.IsOpen; k++) yield return Wait(0.2f);
                if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen)
                {
                    Line($"FB {tag} study alloc hint='{game.SkillView.KeyHintLabel}'");
                    yield return Shot($"{tag}_31_study_alloc");
                    var first = game.Skills.Nodes.FirstOrDefault(n => game.Skills.IsAvailable(n));
                    if (first != null) game.SkillView.Choose(first);
                    yield return Wait(0.4f);
                    if (game.SkillView.IsOpen) { yield return Shot($"{tag}_32_study_mixed"); if (!game.SkillView.IsReadOnlyOpen) game.SkillView.Continue(); }
                    yield return Wait(0.3f);
                }
                else Fail($"{tag}: coffee reward tree did not open");
                yield return ClearOverlays(game, tag + " after study");

                // 결과 → 성장·조합 보기 → 획득 내역(다음 행동 버튼)
                game.AddIngredient(IngredientType.Cocktail, 3);
                game.GiveUp(); yield return Wait(0.8f);
                var res = game.ResultScreen;
                if (res == null || !res.IsOpen) { Fail(tag + ": no result"); game.GoToTitle(); yield return Wait(0.9f); continue; }
                Line($"FB {tag} result award='{res.AwardButtonLabel}' hasAward={ResultView.HasAwardContent(game.DrinkSettlement)} focus='{UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name}' build='{res.BuildDetailButton.GetComponentInChildren<Text>().text}'");
                yield return Shot($"{tag}_40_result");
                res.BuildDetailButton.onClick.Invoke(); yield return Wait(0.4f);
                yield return Shot($"{tag}_41_growth_detail");
                if (res.Detail != null && res.Detail.IsOpen) res.Detail.Close();
                yield return Wait(0.3f);
                if (res.DrinkAwardButton != null && res.DrinkAwardButton.gameObject.activeInHierarchy)
                {
                    res.OpenAwardDetail(); yield return Wait(0.4f);
                    Line($"FB {tag} award open={res.AwardDetail.IsOpen} restart={(res.AwardDetail.RestartButton != null && res.AwardDetail.RestartButton.gameObject.activeSelf)} focus='{UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name}'");
                    yield return Shot($"{tag}_42_award_detail");
                    res.AwardDetail.Close(); yield return Wait(0.3f);
                }
                game.GoToTitle(); yield return Wait(0.9f);
            }
        }
    }
}
#endif
