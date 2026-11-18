#if CCT_QA_TOUR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 QA 전용(CCT_QA_TOUR) "feedback2" 모드 — 둘째 묶음(5 단위 따르기·제조 실습, 7 노트북 안내, 23 결과→획득 내역, 24 목표 음료) 실제 화면.
    /// 강제 경로(보고 표기): 실습 시작·재료 선택·단위 따르기·기법·제조 확정은 실제 버튼 onClick/공개 API, 목표 음료는 DrinkLab.SetGoal(레시피 서랍
    /// 버튼과 같은 함수), 보스 재료 보상은 DrinkLab.RecordBossAward 직접 호출(실제 보스 처치 아님), 사망은 MarkPlayerDiedForTests, 스폰 정지·체력 증가.
    /// 데이터는 -cct-data-dir 격리(빈 저장에서 시작 = 첫 판).
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator Feedback2Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string H = $"{Screen.width}x{Screen.height}";
            Line($"F2 screen {H}");
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("feedback2: no title"); yield break; }

            // ── 로비(첫 판 전): 노트북 안내 없음 ──
            title.RequestStart();
            CharacterSelectView select = null;
            for (int k = 0; k < 20 && select == null; k++) { yield return Wait(0.3f); select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Fail("feedback2: no lobby"); yield break; }
            Line($"F2 lobby before first run: guide={select.NotebookGuideVisible} ended={NotebookGuide.EndedRuns(PermanentProgress.Current)}");
            if (select.NotebookGuideVisible) Fail("feedback2: notebook guide before first run");

            // ── 5 제조 실습 ──
            select.DrinkLabButton.onClick.Invoke();
            DrinkLabView lab = null;
            for (int k = 0; k < 20 && lab == null; k++) { yield return Wait(0.3f); lab = FindFirstObjectByType<DrinkLabView>(); }
            if (lab == null) { Fail("feedback2: no lab"); yield break; }
            yield return Wait(0.5f);
            Line($"F2 practice offered step={lab.CurrentPracticeStep} granted={DrinkLab.PracticeGranted} gin={DrinkLab.Owned(DrinkIngredients.Gin)} campari={DrinkLab.Owned(DrinkIngredients.Campari)} sv={DrinkLab.Owned(DrinkIngredients.SweetVermouth)}");
            yield return Shot($"f2_{H}_01_practice_intro");
            Click(lab.transform, "Primary"); // 시작 — 실제 말풍선 버튼
            yield return Wait(0.6f);
            Line($"F2 practice start step={lab.CurrentPracticeStep} granted={DrinkLab.PracticeGranted} endless gin={DrinkLab.EndlessOwned(DrinkIngredients.Gin)} campari={DrinkLab.EndlessOwned(DrinkIngredients.Campari)} sv={DrinkLab.EndlessOwned(DrinkIngredients.SweetVermouth)} stock campari={DrinkLab.Owned(DrinkIngredients.Campari)} sv={DrinkLab.Owned(DrinkIngredients.SweetVermouth)}");
            // 다시 시작해도 재지급 없음
            bool again = DrinkLab.GrantPracticeKit(out var again2, out var againErr);
            Line($"F2 regrant attempt ok={again} err='{againErr}' items={again2.Count}");
            if (again) Fail("feedback2: practice kit granted twice");
            yield return Shot($"f2_{H}_02_practice_gin");
            int shot = 3;
            foreach (var id in new[] { DrinkIngredients.Gin, DrinkIngredients.Campari, DrinkIngredients.SweetVermouth })
            {
                Click(lab.transform, "Row_" + id); yield return Wait(0.4f);
                bool first = lab.PourUnit(300);
                bool second = lab.PourUnit(300); // 연속 입력: 붓는 중이라 거절돼야 한다
                Line($"F2 pour {id} first={first} second={second} draftBefore={lab.Draft.AmountOf(id)}");
                if (!first || second) Fail($"feedback2: pour unit guard {id} first={first} second={second}");
                yield return Wait(0.5f);
                if (id == DrinkIngredients.Gin) yield return Shot($"f2_{H}_{shot++:00}_pour_mid_{id}");
                for (int k = 0; k < 60 && (lab.Draft.AmountOf(id) < 300 || FindFirstObjectByType<DrinkStageView>().Transferring || FindFirstObjectByType<DrinkStageView>().Pouring); k++) yield return Wait(0.1f);
                yield return Wait(0.4f);
                Line($"F2 poured {id} draft={lab.Draft.AmountOf(id)} step={lab.CurrentPracticeStep}");
                if (lab.Draft.AmountOf(id) != 300) Fail($"feedback2: {id} draft {lab.Draft.AmountOf(id)} != 300");
                yield return Shot($"f2_{H}_{shot++:00}_poured_{id}");
            }
            Click(lab.transform, "Tech" + DrinkTechnique.Build); yield return Wait(0.5f);
            Line($"F2 technique={lab.Draft.technique} step={lab.CurrentPracticeStep}");
            yield return Shot($"f2_{H}_{shot++:00}_practice_craft_step");
            lab.ConfirmCraft(); yield return Wait(0.3f);
            lab.ConfirmCraft(); // 연속 입력: 이미 준비 음료가 있어 두 번째는 실패해야 한다
            for (int k = 0; k < 80 && FindFirstObjectByType<DrinkStageView>() != null && FindFirstObjectByType<DrinkStageView>().SequencePlaying; k++) yield return Wait(0.1f);
            yield return Wait(0.6f);
            var prep = DrinkLab.Prepared;
            Line($"F2 crafted prepared={(prep != null ? prep.recipe.name : "null")} saleBlocked={DrinkLab.PreparedSaleBlocked} step={lab.CurrentPracticeStep} stock gin={DrinkLab.Owned(DrinkIngredients.Gin)} campari={DrinkLab.Owned(DrinkIngredients.Campari)} sv={DrinkLab.Owned(DrinkIngredients.SweetVermouth)} endlessLeft campari={DrinkLab.EndlessOwned(DrinkIngredients.Campari)}");
            yield return Shot($"f2_{H}_{shot++:00}_practice_done");
            lab.SetTab(DrinkLabView.Tab.Orders); yield return Wait(0.4f);
            yield return Shot($"f2_{H}_{shot++:00}_orders_sale_blocked");
            int coinsBefore = DrinkLab.Coins;
            bool sold = DrinkLab.Sell(0, out int got, out string sellErr);
            Line($"F2 sell attempt sold={sold} got={got} err='{sellErr}' coins {coinsBefore}->{DrinkLab.Coins}");
            if (sold) Fail("feedback2: practice drink sold");
            lab.SetTab(DrinkLabView.Tab.Craft); yield return Wait(0.3f);
            Click(lab.transform, "Primary"); yield return Wait(0.3f); // 완료
            Line($"F2 practice complete step={lab.CurrentPracticeStep} craftGuideVersion={SettingsService.Current.craftGuideVersion}");
            // 차·커피 첫 진입 안내
            Click(lab.transform, "KindTea"); yield return Wait(0.5f);
            Line($"F2 tea guide visible={lab.KindGuideVisible}");
            yield return Shot($"f2_{H}_{shot++:00}_tea_guide");
            Click(lab.transform, "Primary"); yield return Wait(0.3f);
            Click(lab.transform, "KindCoffee"); yield return Wait(0.5f);
            Line($"F2 coffee guide visible={lab.KindGuideVisible}");
            yield return Shot($"f2_{H}_{shot++:00}_coffee_guide");
            Click(lab.transform, "Primary"); yield return Wait(0.3f);
            Click(lab.transform, "KindTea"); yield return Wait(0.4f);
            Line($"F2 tea guide again visible={lab.KindGuideVisible} (once only)");
            // ── 4 커피 완성 음료(카페라떼): 실습 잔을 버리고(명시 폐기) 에스프레소 → 원두 단위 → 라떼 → 우유 단위 → 거품 내기 → 제조 ──
            Line($"F2 discard practice drink ok={DrinkLab.DiscardPrepared(out var discErr)} err='{discErr}'");
            // 강제(fixture): 격리 저장에 스팀 우유 600mL·원두 100g 지급 — 시작 지급(우유 15mL)으로는 라떼(150mL+)를 만들 수 없다. 정상 수집 아님.
            DrinkLab.Data.Add(DrinkIngredients.SteamedMilk, 6000); DrinkLab.Data.Add(DrinkIngredients.CoffeeBean, 1000);
            Click(lab.transform, "KindCoffee"); yield return Wait(0.5f);
            lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine); yield return Wait(0.3f);
            Click(lab.transform, "Row_" + DrinkIngredients.CoffeeBean); yield return Wait(0.3f);
            foreach (var u in new[] { 100, 50, 20, 20 }) { lab.PourUnit(u); for (int k = 0; k < 80 && (lab.Stage.Pouring || lab.Stage.Transferring); k++) yield return Wait(0.1f); yield return Wait(0.2f); }
            lab.SetCoffeeStyle(CoffeeStyle.Americano); yield return Wait(0.3f);
            Line($"F2 coffee americano hotWater={lab.Draft.hotWater} cap={DrinkRules.ServeCapacityMl(lab.Draft)} ok={DrinkValidator.Check(lab.Draft).Ok} '{DrinkValidator.Check(lab.Draft).First}'");
            lab.RevealMixRow("Style_"); yield return Wait(0.15f);
            yield return Shot($"f2_{H}_{shot++:00}_coffee_americano");
            lab.SetCoffeeStyle(CoffeeStyle.Latte); yield return Wait(0.3f);
            Click(lab.transform, "Row_" + DrinkIngredients.SteamedMilk); yield return Wait(0.3f);
            for (int n = 0; n < 5; n++) { lab.PourUnit(300); for (int k = 0; k < 100 && (lab.Stage.Pouring || lab.Stage.Transferring); k++) yield return Wait(0.1f); yield return Wait(0.2f); } // 30mL × 5 = 150mL
            Line($"F2 latte before foam milk={lab.Draft.AmountOf(DrinkIngredients.SteamedMilk)} dose={lab.Draft.CoffeeDose()} foam={lab.Draft.foam} check='{DrinkValidator.Check(lab.Draft).First}'");
            lab.RevealMixRow("Style_"); yield return Wait(0.15f);
            yield return Shot($"f2_{H}_{shot++:00}_coffee_latte_need_foam");
            lab.StartFoam(); lab.StartFoam(); // 연속 입력
            yield return Wait(0.5f);
            Line($"F2 foaming={lab.Foaming}");
            lab.RevealMixRow("Style_"); yield return Wait(0.15f);
            yield return Shot($"f2_{H}_{shot++:00}_coffee_latte_foaming");
            for (int k = 0; k < 40 && lab.Foaming; k++) yield return Wait(0.1f);
            yield return Wait(0.3f);
            Line($"F2 latte foam={lab.Draft.foam} band={lab.Stage.FoamBandVisible} h01={lab.Stage.FoamBandHeight01:0.000} ok={DrinkValidator.Check(lab.Draft).Ok} tempArt={CoffeeStyles.UsesTempArt(CoffeeStyle.Latte, CoffeeBrewMethod.EspressoMachine)}");
            { var fp = lab.Stage.FoamPixels(); Line($"F2 foam px Latte: foamMl={fp.foamMl:0} bandPx={fp.bandPx:0.0} expectedPx={fp.expectedPx:0.0} capPx={fp.capPx:0.0} artPx={fp.cupPxPerArtPx:0.00} textured={lab.Stage.FoamTextured}"); }
            lab.RevealMixRow("Style_"); yield return Wait(0.15f);
            yield return Shot($"f2_{H}_{shot++:00}_coffee_latte_ready");
            int milkBefore = DrinkLab.Owned(DrinkIngredients.SteamedMilk);
            lab.ConfirmCraft();
            for (int k = 0; k < 100 && lab.Stage.SequencePlaying; k++) yield return Wait(0.1f);
            yield return Wait(0.6f);
            Line($"F2 latte crafted prepared={(DrinkLab.Prepared != null ? DrinkLab.Prepared.recipe.name + " style=" + DrinkLab.Prepared.recipe.coffeeStyle : "null")} milk {milkBefore}->{DrinkLab.Owned(DrinkIngredients.SteamedMilk)}");
            yield return Shot($"f2_{H}_{shot++:00}_coffee_latte_done");
            // 나머지 3종 실제 제조(위 fixture 재고 사용). 우유 단위 = 0.1mL 저장값(300 = 30mL).
            foreach (var (style, milkUnits) in new[] { (CoffeeStyle.FlatWhite, new[] { 300, 300, 300 }), (CoffeeStyle.Cappuccino, new[] { 300, 300, 300 }), (CoffeeStyle.Americano, new int[0]) })
            {
                DrinkLab.DiscardPrepared(out _);
                lab.SetTab(DrinkLabView.Tab.Craft); yield return Wait(0.3f);
                Click(lab.transform, "Clear"); yield return Wait(0.3f);
                lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine); yield return Wait(0.2f);
                Click(lab.transform, "Row_" + DrinkIngredients.CoffeeBean); yield return Wait(0.3f);
                foreach (var u in new[] { 100, 50, 20, 20 }) { lab.PourUnit(u); for (int k = 0; k < 80 && (lab.Stage.Pouring || lab.Stage.Transferring); k++) yield return Wait(0.1f); yield return Wait(0.15f); }
                lab.SetCoffeeStyle(style); yield return Wait(0.2f);
                if (milkUnits.Length > 0)
                {
                    Click(lab.transform, "Row_" + DrinkIngredients.SteamedMilk); yield return Wait(0.3f);
                    foreach (var u in milkUnits) { lab.PourUnit(u); for (int k = 0; k < 100 && (lab.Stage.Pouring || lab.Stage.Transferring); k++) yield return Wait(0.1f); yield return Wait(0.2f); }
                    lab.StartFoam();
                    for (int k = 0; k < 50 && lab.Foaming; k++) yield return Wait(0.1f);
                    yield return Wait(0.3f);
                }
                int m0 = DrinkLab.Owned(DrinkIngredients.SteamedMilk);
                var chk = DrinkValidator.Check(lab.Draft);
                Line($"F2 style {style} milk={lab.Draft.AmountOf(DrinkIngredients.SteamedMilk)} hot={lab.Draft.hotWater} foam={lab.Draft.foam} fill={CoffeeStyles.TotalVolumeMl(lab.Draft):0}/{DrinkRules.ServeCapacityMl(lab.Draft) * DrinkRules.ServeFillMax:0} ok={chk.Ok} '{chk.First}' band={lab.Stage.FoamBandVisible}");
                { var fp = lab.Stage.FoamPixels(); Line($"F2 foam px {style}: foamMl={fp.foamMl:0} bandPx={fp.bandPx:0.0} expectedPx={fp.expectedPx:0.0} capPx={fp.capPx:0.0} artPx={fp.cupPxPerArtPx:0.00} textured={lab.Stage.FoamTextured}"); }
                if (!chk.Ok) Fail($"feedback2: {style} not valid: {chk.First}");
                lab.RevealMixRow("Style_"); yield return Wait(0.15f);
                yield return Shot($"f2_{H}_{shot++:00}_coffee_{style}_ready");
                lab.ConfirmCraft();
                for (int k = 0; k < 100 && lab.Stage.SequencePlaying; k++) yield return Wait(0.1f);
                yield return Wait(0.5f);
                Line($"F2 style {style} crafted={(DrinkLab.Prepared != null ? DrinkLab.Prepared.recipe.coffeeStyle.ToString() : "null")} milk {m0}->{DrinkLab.Owned(DrinkIngredients.SteamedMilk)}");
                if (DrinkLab.Prepared == null || DrinkLab.Prepared.recipe.coffeeStyle != (int)style) Fail($"feedback2: {style} craft failed");
                yield return Shot($"f2_{H}_{shot++:00}_coffee_{style}_done");
            }

            // 액체 단위 버튼 줄(칵테일) 캡처
            Click(lab.transform, "KindCocktail"); yield return Wait(0.4f);
            Click(lab.transform, "Row_" + DrinkIngredients.Gin); yield return Wait(0.3f);
            yield return Shot($"f2_{H}_{shot++:00}_unit_buttons_liquid");

            // ── 24 목표 음료 지정(레시피 서랍 버튼과 같은 함수) ──
            var goalRecipe = DrinkLab.PracticeRecipe.Clone();
            goalRecipe.steps.Clear();
            goalRecipe.steps.Add(new DrinkStep(DrinkIngredients.Gin, 600)); goalRecipe.steps.Add(new DrinkStep(DrinkIngredients.Campari, 300)); goalRecipe.steps.Add(new DrinkStep(DrinkIngredients.SweetVermouth, 300));
            goalRecipe.name = "더블 진 네그로니";
            if (DrinkLab.SaveRecipe(goalRecipe, -1, out var saveErr)) { lab.SetTab(DrinkLabView.Tab.Recipes); yield return Wait(0.6f); }
            bool goalSet = DrinkLab.SetGoal(goalRecipe, out var goalErr);
            Line($"F2 goal set={goalSet} err='{goalErr}' goal={DrinkLab.GoalRecipe?.name}");
            Line($"F2 discard americano before depart ok={DrinkLab.DiscardPrepared(out _)} (목표 음료 HUD 확인 런은 준비 음료 없이)");
            lab.Close(); yield return Wait(1f);

            // ── 런: 목표 칸 → 재료 보상(강제) 비행 → 사망 → 결과 → 획득 내역 → 타이틀 ──
            select = FindFirstObjectByType<CharacterSelectView>();
            select.BartenderButton.onClick.Invoke(); yield return Wait(0.4f);
            select.WeaponButtonA.onClick.Invoke(); select.StartButton.onClick.Invoke(); yield return Wait(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen) { tutorial.Dismiss(); yield return Wait(0.5f); }
            var game = GameManager.Instance;
            if (game == null) { Fail("feedback2: run did not start"); yield break; }
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(1f);
            FreezeWorld(game); yield return Wait(0.6f);
            var hud = game.Hud.DrinkGoal;
            Line($"F2 hud goal visible={hud != null && hud.Visible} title='{hud?.TitleText.Replace("\n", " | ")}' chips={hud?.ChipCount} c0='{hud?.ChipText(0)}' c1='{hud?.ChipText(1)}' c2='{hud?.ChipText(2)}'");
            yield return Shot($"f2_{H}_20_hud_goal_start", game);
            var items = new System.Collections.Generic.List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 300), new DrinkAmount(DrinkIngredients.Campari, 150), new DrinkAmount(DrinkIngredients.LemonJuice, 150) };
            bool rec = DrinkLab.RecordBossAward(game.RunId, "qa-fb2-1", "QA 강제 재료 보상", items);
            bool recDup = DrinkLab.RecordBossAward(game.RunId, "qa-fb2-1", "QA 강제 재료 보상", items);
            yield return Wait(0.25f);
            Line($"F2 award rec={rec} dup={recDup} flights={hud?.FlightsInAir}");
            if (!rec || recDup) Fail("feedback2: award record/dup guard");
            yield return Shot($"f2_{H}_21_hud_goal_flight", game);
            yield return Wait(0.8f);
            Line($"F2 hud after award c0='{hud?.ChipText(0)}' c1='{hud?.ChipText(1)}' c2='{hud?.ChipText(2)}' ready={hud?.ReadyFlashing}");
            yield return Shot($"f2_{H}_22_hud_goal_after", game);
            rec = DrinkLab.RecordBossAward(game.RunId, "qa-fb2-2", "QA 강제 재료 보상", new System.Collections.Generic.List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 600), new DrinkAmount(DrinkIngredients.Campari, 300), new DrinkAmount(DrinkIngredients.SweetVermouth, 300) });
            yield return Wait(0.5f);
            Line($"F2 hud ready c0='{hud?.ChipText(0)}' c1='{hud?.ChipText(1)}' c2='{hud?.ChipText(2)}' flashing={hud?.ReadyFlashing} title='{hud?.TitleText.Replace("\n", " | ")}'");
            yield return Shot($"f2_{H}_23_hud_goal_ready", game);
            yield return Wait(1.2f);

            game.MarkPlayerDiedForTests();
            // 사망 깃발은 다음 Update에서 처리되고 결과 패널은 그 뒤 DeathResultDelaySeconds(실시간) 후 열린다 — 고정 1.2초 대기는 경합(11-15 'no result').
            var res = game.ResultScreen;
            for (int k = 0; k < 60 && (res == null || !res.IsOpen); k++) { yield return Wait(0.1f); res = game.ResultScreen; }
            if (res == null || !res.IsOpen) { Fail("feedback2: no result"); yield break; }
            Line($"F2 result award='{res.AwardButtonLabel}' hasAward={ResultView.HasAwardContent(game.DrinkSettlement)} focus='{EventSystem.current?.currentSelectedGameObject?.name}' kind={game.DrinkSettlement?.kind} granted={game.DrinkSettlement?.granted.Count}");
            yield return Shot($"f2_{H}_30_result_with_award");
            res.DrinkAwardButton.onClick.Invoke(); yield return Wait(0.5f);
            Line($"F2 award detail open={res.AwardDetail.IsOpen} focus='{EventSystem.current?.currentSelectedGameObject?.name}' text='{res.AwardDetail.AllText.Replace("\n", " | ")}'");
            yield return Shot($"f2_{H}_31_award_detail_items");
            res.AwardDetail.TitleButton.onClick.Invoke(); yield return Wait(1.2f);
            res.AwardDetail?.TitleButton?.onClick.Invoke(); // 연속 입력(이미 닫힘·전환 중) — 두 번째는 효과 없음

            // ── 7 첫 판 뒤 로비 노트북 안내 ──
            title = FindFirstObjectByType<TitleView>();
            if (title == null) { Fail("feedback2: no title after result"); yield break; }
            title.RequestStart();
            select = null;
            for (int k = 0; k < 20 && select == null; k++) { yield return Wait(0.3f); select = FindFirstObjectByType<CharacterSelectView>(); }
            yield return Wait(0.4f);
            Line($"F2 lobby after first run: guide={select.NotebookGuideVisible} ended={NotebookGuide.EndedRuns(PermanentProgress.Current)} rp={DrinkLab.Research.researchPoints}");
            if (!select.NotebookGuideVisible) Fail("feedback2: notebook guide missing after first run");
            yield return Shot($"f2_{H}_40_lobby_notebook_guide");
            Click(select.transform, "Primary"); // 노트북 열기
            for (int k = 0; k < 40 && (ResearchLaptopView.Current == null || ResearchLaptopView.Current.State != ResearchLaptopView.Phase.Open); k++) yield return Wait(0.1f);
            var lap = ResearchLaptopView.Current;
            if (lap == null) { Fail("feedback2: laptop not open"); yield break; }
            yield return Wait(0.3f);
            Line($"F2 laptop guide step={lap.GuideStep} tab={lap.CurrentTab} text='{lap.GuideText}'");
            yield return Shot($"f2_{H}_41_laptop_guide_step2");
            Click(lap.transform, "Primary"); yield return Wait(0.3f);
            Line($"F2 laptop guide step={lap.GuideStep} text='{lap.GuideText}'");
            yield return Shot($"f2_{H}_42_laptop_guide_step3");
            Click(lap.transform, "Primary"); yield return Wait(0.3f);
            Line($"F2 laptop guide done step={lap.GuideStep} notebookGuideVersion={SettingsService.Current.notebookGuideVersion}");
            Click(lap.transform, "GuideReplay"); yield return Wait(0.3f);
            Line($"F2 laptop guide replay step={lap.GuideStep}");
            yield return Shot($"f2_{H}_43_laptop_guide_replay");
            lap.Close(); yield return Wait(1f);
            select = FindFirstObjectByType<CharacterSelectView>();
            yield return Wait(0.3f);
            Line($"F2 lobby after guide: guide={select != null && select.NotebookGuideVisible}");
            if (select != null && select.NotebookGuideVisible) Fail("feedback2: guide shown again after completion");
            yield return Shot($"f2_{H}_44_lobby_after_guide");
        }
    }
}
#endif
