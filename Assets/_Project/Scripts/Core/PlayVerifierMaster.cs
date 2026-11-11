#if CCT_PLAY_VERIFIER
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-11 마스터 도전 1주기 실제 Windows 빌드 검증(-cct-playverify-master). PlayVerifier와 같은 입력 대행 원칙:
    /// 이동은 AutoInputOverride(8방향 디지털)만, 화면 선택은 실제 버튼 onClick(사람 간격 ≥0.45초). HP·적·생성·재료·시간·무적·보상 값은 쓰지 않는다.
    /// 순서: 캐릭터 선택에서 "마스터 도전" 버튼 → 수집 1~5 → 반복 버튼 → 수집 1~5 → 음료 만들기(커피·칵테일 제조, 1잔 마시기, 나머지 제출) → 박람회 도전
    /// → 남은 적 처리 → 현수막 3.5초(실제 시간 100ms 간격 캡처) → 박람회 3 → 보상 2(HP 감소 감시) → 다음 주기 1웨이브 → 일시정지·포기로 종료.
    /// 캡처는 게임 자신의 화면(ScreenCapture)이라 다른 창 포커스·입력과 무관하다.
    /// </summary>
    public partial class PlayVerifier
    {
        string shotDir;
        int periodicIndex;
        bool periodicOn;

        void MShot(string name)
        {
            if (shotDir == null) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
            Event($"shot {name}");
        }

        IEnumerator Periodic()
        {
            var dir = Path.Combine(outDir, "periodic");
            Directory.CreateDirectory(dir);
            while (periodicOn)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"p_{periodicIndex++:0000}.png"));
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        static List<Button> Buttons(Component root, string name) =>
            root == null ? new List<Button>() : root.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.name == name && b.gameObject.activeInHierarchy && b.interactable).ToList();

        static string RowText(Button b)
        {
            var row = b.transform.parent;
            var t = row != null ? row.Find("Name")?.GetComponent<Text>() : null;
            return t != null ? t.text : "";
        }

        string MasterLine(GameManager g)
        {
            var st = g.Master.State;
            return $"wave={st.Wave} cycle={st.Cycle} seg={st.Segment} phase={st.Phase} target={st.Target} spawned={st.Spawned} cleared={st.Cleared} repeat={st.RepeatCount} " +
                   $"submitted={st.SubmittedThisCycle} points={st.SubmittedPointsThisCycle} preview={st.PreviewFairReduction} snapshot={st.FairReductionSnapshot} carried={st.CarriedReduction} " +
                   $"cocktailU={g.Master.CocktailUniqueCount} atk+={g.Master.CocktailAttackPercent}% hp={g.Player.Hp:0}/{g.Player.MaxHp:0} enemies={Enemy.Active.Count} state={g.State} paused={g.IsPaused} timeScale={Time.timeScale:0.##}";
        }

        IEnumerator WaitPhase(GameManager g, System.Func<bool> done, float realLimit, string label)
        {
            float until = Time.realtimeSinceStartup + realLimit;
            float nextLog = 0f;
            while (g != null && g.State != GameState.GameOver && !done() && Time.realtimeSinceStartup < until)
            {
                HandleRewardScreens(g);
                if (Time.realtimeSinceStartup >= nextLog) { nextLog = Time.realtimeSinceStartup + 10f; Event($"[{label}] {MasterLine(g)}"); }
                yield return null;
            }
            bool ok = g != null && g.State != GameState.GameOver && done();
            Event($"[{label}] {(ok ? "REACHED" : "NOT REACHED")} {(g != null ? MasterLine(g) : "")}");
        }

        IEnumerator MasterFlow()
        {
            Application.runInBackground = true; // 포커스 없이도 진행(입력은 대행, 다른 창에 입력하지 않음)
            shotDir = Path.Combine(outDir, "shots");
            Directory.CreateDirectory(shotDir);
            Event("master flow: input-only agent; screens via real buttons; no stat/time/spawn writes; captures = in-game ScreenCapture");
            yield return Human(1f);
            var select = FindFirstObjectByType<CharacterSelectView>();
            float w = Time.realtimeSinceStartup + 5f;
            while (select == null && Time.realtimeSinceStartup < w) { yield return null; select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Event("FAIL no character select"); yield break; }
            select.BartenderButton.onClick.Invoke(); // 모드 줄은 무기 단계에서 만들어진다
            Event("clicked job Bartender");
            yield return Human(0.6f);
            if (select.EndlessModeButton == null) { Event("FAIL no master mode button in weapon step"); yield break; }
            select.EndlessModeButton.onClick.Invoke();
            Event($"clicked mode 마스터 도전 → current={RunModeSelection.Current}");
            yield return Human(0.6f);
            MShot("00_select_master");
            yield return Human(0.4f);
            select.WeaponButtonA.onClick.Invoke();
            Event($"clicked weapon A selected={select.SelectedWeapon}");
            yield return Human(0.5f);
            select.StartButton.onClick.Invoke();
            Event("clicked 영업 시작");
            yield return Human(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen) { ClickNamed(tutorial, "DismissButton"); Event("first-run tutorial → 시작"); yield return Human(0.5f); }
            var g = GameManager.Instance;
            if (g == null || g.Master == null) { Event($"FAIL run not started in master mode (game={g != null} master={g?.Master != null})"); yield break; }
            BindGame(g);
            periodicOn = true;
            StartCoroutine(Periodic());
            Event("run started " + MasterLine(g));
            yield return PrepStartShot(g, "01a_prep_wave1");
            yield return Human(1.5f);
            MShot("01_wave1");

            // 수집 1~5 → 선택 대기
            yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.AwaitChoice, 600f, "collect-1");
            if (g.Master.State.Phase != MasterPhase.AwaitChoice) { yield return EndMaster(g); yield break; }
            yield return Human(0.8f);
            MShot("02_await_choice_first");
            yield return Human(0.5f);
            if (Buttons(g.MasterHud, "Repeat").FirstOrDefault() is Button rep) { rep.onClick.Invoke(); Event("clicked 재료 모으기 반복 → " + MasterLine(g)); }
            else { Event("FAIL repeat button"); }
            yield return Human(1f);
            MShot("03_after_repeat");

            yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.AwaitChoice, 600f, "collect-repeat");
            if (g.Master.State.Phase != MasterPhase.AwaitChoice) { yield return EndMaster(g); yield break; }
            yield return Human(0.8f);

            // 음료 만들기: 버튼 → 커피 최대 3잔(품질별) + 칵테일 1잔 → 1잔 마시기 → 나머지 제출
            yield return Brew(g);

            // 박람회 도전
            MShot("10_before_fair_choice");
            yield return Human(0.5f);
            if (Buttons(g.MasterHud, "Fair").FirstOrDefault() is Button fair) { fair.onClick.Invoke(); Event("clicked 박람회 도전 → " + MasterLine(g) + $" banner=\"{g.Hud.BannerSubText}\""); }
            else { Event("FAIL fair button"); yield return EndMaster(g); yield break; }
            yield return Human(0.3f);
            MShot("11_clearing_for_fair");

            // 남은 적 처리 → 현수막(실제 시간 연속 캡처)
            yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.Transition, 300f, "clearing");
            if (g.Master.State.Phase == MasterPhase.Transition)
            {
                float t0 = Time.realtimeSinceStartup;
                float hp0 = g.Player.Hp, rt0 = g.RunTime;
                long sp0 = g.Master.State.Spawned;
                var seq = Path.Combine(outDir, "transition");
                Directory.CreateDirectory(seq);
                int k = 0;
                while (g.Master.State.Phase == MasterPhase.Transition && Time.realtimeSinceStartup - t0 < 6f)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(seq, $"t_{k++:000}.png"));
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                float took = Time.realtimeSinceStartup - t0;
                Event($"transition took={took:0.00}s frames={k} hpDelta={g.Player.Hp - hp0:0.##} runTimeDelta={g.RunTime - rt0:0.###} spawnedDelta={g.Master.State.Spawned - sp0} → {MasterLine(g)} banner=\"{g.Hud.BannerSubText}\"");
                yield return Human(0.6f);
                MShot("12_fair_start");
            }

            // 박람회 3웨이브
            yield return WaitPhase(g, () => g.Master.State.Segment == MasterSegment.Reward, 600f, "fair");
            if (g.Master.State.Segment != MasterSegment.Reward) { yield return EndMaster(g); yield break; }
            var awards = DrinkLab.PendingFor(g.RunId).Where(a => a.awardId.StartsWith("master_fair_c")).ToList();
            Event($"fair success awards={awards.Count} items={(awards.Count > 0 ? awards[0].items.Count : 0)} endless={(awards.Count > 0 && awards[0].endless)} lastBanner=\"{g.Master.LastBannerTitle}\" / \"{g.Master.LastBannerSub}\"");
            yield return Human(0.5f);
            MShot("13_reward_start");

            // 보상 2웨이브: HP가 줄어들지 않는지(읽기만)
            float hpStart = g.Player.Hp, hpMin = hpStart;
            int harmlessSeen = 0, nonHarmless = 0;
            float until = Time.realtimeSinceStartup + 300f;
            bool midShot = false;
            while (g.State != GameState.GameOver && g.Master.State.Segment == MasterSegment.Reward && Time.realtimeSinceStartup < until)
            {
                HandleRewardScreens(g);
                hpMin = Mathf.Min(hpMin, g.Player.Hp);
                foreach (var e in Enemy.Active) if (e != null && !e.IsDead) { if (e.Harmless) harmlessSeen++; else nonHarmless++; }
                if (!midShot && Enemy.Active.Count >= 3) { midShot = true; MShot("14_reward_enemies"); }
                yield return null;
            }
            Event($"reward waves: hpStart={hpStart:0.#} hpMin={hpMin:0.#} hpEnd={g.Player.Hp:0.#} harmlessFrames={harmlessSeen} nonHarmlessFrames={nonHarmless} → {MasterLine(g)}");

            // 다음 주기
            if (g.State != GameState.GameOver && g.Master.State.Wave >= 11)
            {
                yield return Human(1.2f);
                // 2026-11-12 다음 주기 준비 패널(전투 정지) 확인 — 사람 간격 뒤 HandleMasterPrep가 실제 '전투 시작'을 누른다.
                var ups11 = g.Master.UpcomingWaves();
                Event($"cycle2 prep phase={g.Master.State.Phase} prepActive={g.MasterPrepActive} timeScale={Time.timeScale} panel={g.MasterHud.Prep.PanelVisible} title=\"{g.MasterHud.Prep.PanelTitle}\" plan={(ups11.Count > 0 ? ups11[0].plan.Label + " " + ups11[0].plan.Count + " red=" + ups11[0].reduction : "-")} rows=[{string.Join(" | ", g.MasterHud.Prep.RowTexts)}]");
                MShot("15_next_cycle_wave11");
                yield return RtShot("15_next_cycle_wave11_1080", 1920, 1080);
                yield return WaitPhase(g, () => g.Master.State.Spawned >= 3, 30f, "cycle2");
                var sample = Enemy.Active.FirstOrDefault(e => e != null && !e.IsDead);
                Event($"cycle2 sample enemy={(sample != null ? sample.Def.name : "-")} maxHp={(sample != null ? sample.MaxHp : 0):0} carried={g.Master.State.CarriedReduction}% expectedScale=EndlessRules.HpScale(11)*(1-carried/100)");
                MShot("16_cycle2_enemies");
            }
            yield return EndMaster(g);
        }

        IEnumerator Brew(GameManager g)
        {
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) { brew.onClick.Invoke(); Event($"clicked 음료 만들기 → open={g.MasterHud.BrewOpen} paused={g.IsPaused} timeScale={Time.timeScale}"); }
            else { Event("FAIL brew button"); yield break; }
            yield return Human(0.6f);
            MShot("04_brew_open");
            int coffees = 0, cocktails = 0;
            for (int guard = 0; guard < 12 && (coffees < 3 || cocktails < 1); guard++)
            {
                var crafts = Buttons(g.MasterHud, "Craft");
                Button pick = null;
                if (coffees < 3) pick = crafts.FirstOrDefault(b => RowText(b).Contains("[커피]") && (coffees == 0 || !RowText(b).Contains(lastCoffee ?? "\u0000")));
                if (pick == null && cocktails < 1) pick = crafts.FirstOrDefault(b => RowText(b).Contains("[칵테일]"));
                if (pick == null && coffees < 3) pick = crafts.FirstOrDefault(b => RowText(b).Contains("[커피]"));
                if (pick == null) break;
                string row = RowText(pick);
                bool coffee = row.Contains("[커피]");
                if (coffee) { coffees++; lastCoffee = row; } else cocktails++;
                pick.onClick.Invoke();
                Event($"clicked 만들기 \"{row}\" → msg=\"{g.MasterHud.LastMessage}\" fxClip={DrinkQualityFx.LastClip} grade={DrinkQualityFx.LastGrade} {MasterLine(g)}");
                yield return new WaitForSecondsRealtime(0.25f); // 품질 효과 정점 근처
                MShot($"05_craft_{coffees + cocktails}");
                yield return Human(0.5f);
            }
            // 1잔 마시기(첫 커피 우선)
            var drinks = Buttons(g.MasterHud, "Drink");
            var d = drinks.FirstOrDefault(b => RowText(b).Contains("최고")) ?? drinks.FirstOrDefault();
            if (d != null)
            {
                string row = RowText(d);
                d.onClick.Invoke();
                Event($"clicked 마시기 \"{row}\" → msg=\"{g.MasterHud.LastMessage}\" fxClip={DrinkQualityFx.LastClip} activeCup={g.ActiveRunCup?.recipe?.name} drunk={g.Player.DrunkDrink} atk+={g.Master.CocktailAttackPercent}%");
                yield return new WaitForSecondsRealtime(0.25f);
                MShot("06_drink");
                yield return Human(0.5f);
            }
            // 나머지 제출
            for (int guard = 0; guard < 6; guard++)
            {
                var s = Buttons(g.MasterHud, "Submit").FirstOrDefault();
                if (s == null) break;
                string row = RowText(s);
                s.onClick.Invoke();
                Event($"clicked 제출 \"{row}\" → msg=\"{g.MasterHud.LastMessage}\" fxClip={DrinkQualityFx.LastClip} cup={g.MasterHud.SubmitCupShown} {MasterLine(g)}");
                yield return new WaitForSecondsRealtime(0.25f);
                MShot($"07_submit_{guard + 1}");
                yield return Human(0.5f);
            }
            MShot("08_brew_footer");
            yield return Human(0.4f);
            if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) { close.onClick.Invoke(); Event($"clicked 닫기 → open={g.MasterHud.BrewOpen} paused={g.IsPaused}"); }
            yield return Human(0.8f);
            MShot("09_after_brew");
        }

        string lastCoffee;

        /// <summary>2026-11-12 런 시작 준비 단계: 예고 패널 캡처 후 실제 '전투 시작' 버튼.</summary>
        IEnumerator PrepStartShot(GameManager g, string shot)
        {
            float until = Time.realtimeSinceStartup + 3f;
            while (g.Master.State.Phase != MasterPhase.Prep && Time.realtimeSinceStartup < until) yield return null;
            if (g.Master.State.Phase != MasterPhase.Prep) { Event("no prep phase at run start"); yield break; }
            yield return Human(0.8f);
            var ups = g.Master.UpcomingWaves();
            Event($"prep wave={g.Master.State.Wave} timeScale={Time.timeScale} prepActive={g.MasterPrepActive} plan={(ups.Count > 0 ? ups[0].plan.Label + " " + ups[0].plan.Count : "-")} rows=[{string.Join(" | ", g.MasterHud.Prep.RowTexts)}]");
            if (shot != null) MShot(shot);
            yield return Human(0.6f);
            if (Buttons(g.MasterHud, "PrepStart").FirstOrDefault() is Button b) { b.onClick.Invoke(); Event("clicked 전투 시작 → " + MasterLine(g)); }
            else Event("FAIL prep start button");
            prepSince = -1f;
        }

        IEnumerator EndMaster(GameManager g)
        {
            periodicOn = false;
            if (g == null) yield break;
            Event("END master " + MasterLine(g) + $" reason={g.EndReason}");
            if (g.State != GameState.GameOver)
            {
                g.Player.AutoInputOverride = Vector2.zero;
                g.OpenPause(); // Esc와 같은 진입점
                yield return Human(0.6f);
                if (ClickNamed(g.PauseView, "GiveUpButton")) Event("clicked 런 포기");
                yield return Human(0.6f);
                Transform confirm = null;
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name == "EndRunConfirm") { confirm = t; break; }
                if (confirm != null && ClickNamed(confirm, "Confirm")) Event("clicked confirm(포기)");
            }
            yield return Human(2f);
            MShot("17_result");
            var r = FindFirstObjectByType<ResultView>();
            Event($"result open={(r != null && r.IsOpen)} title=\"{r?.TitleText}\" stats=\"{r?.StatsText?.Replace("\n", " / ")}\"");
            SnapshotSave("after master run");
            yield return Human(1f);
        }
    }
}
#endif
