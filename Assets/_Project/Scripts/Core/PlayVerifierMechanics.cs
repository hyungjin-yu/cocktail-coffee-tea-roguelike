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
    /// 2026-11-11 마스터 도전 미검증 3항목 집중 검증(-cct-playverify-master-mechanics). **QA 시나리오 — 자연 플레이 아님.**
    /// 전제(실행 전 준비, 이 코드 밖): 격리 -cct-data-dir 폴더의 drinks.json 창고에 칵테일 2종 제조용 재료를 넣은 QA fixture(보고서에 명시).
    /// 이 흐름은 값을 쓰지 않는다: 화면 선택은 실제 버튼 onClick, 이동은 AutoInputOverride(기존 입력 대행), 저이펙트 전환만 설정 저장(격리 settings.json)으로 바꾼다.
    /// 관찰: Enemy.QaDirectHit(이 빌드에만 컴파일되는 읽기 전용 알림)으로 직접 적중 B/A/T, MasterVfx 재생 직후 게임 화면 캡처.
    /// 1) 칵테일: 마티니 → 마티니(같은 종류 반복) → 네그로니, 매 제조 후 U·공격 %·Stats.Damage.
    /// 2) 제출: 최고 품질 커피 1잔(150p) + 칵테일 1잔(100p) → 미리보기 round(250×5/100)=13%, 박람회 접수 스냅샷 13%, 박람회 적 HP = 기본×0.87.
    /// 3) 상성: 커피를 마셔 차 계열 대상 → 실제 적중의 A = max(1, round(B×20%)), T = B + A, 비대상 A = 0. full 3장 → 저이펙트 → low 3장 캡처.
    /// </summary>
    public partial class PlayVerifier
    {
        readonly List<string> hitLog = new List<string>();
        int affHits, affBad, nonAffHits, nonAffBad, affVfxSeen;

        void OnQaHit(Enemy e, DamageResult r)
        {
            if (e == null || boundGame == null) return;
            var drink = boundGame.Player != null ? boundGame.Player.AffinityDrink : null;
            bool target = DrinkAffinity.Matches(drink, e.Family);
            int expectA = target ? DrinkAffinity.Bonus(r.baseDamage, drink, e.Family) : 0;
            bool ok = r.bonusDamage == expectA && r.total == r.baseDamage + r.bonusDamage;
            if (target) { affHits++; if (!ok) affBad++; } else { nonAffHits++; if (!ok) nonAffBad++; }
            if (hitLog.Count < 400)
                hitLog.Add($"{boundGame.RunTime:0.00}s {e.Def.name} family={e.Family} drink={(drink?.ToString() ?? "none")} target={target} B={r.baseDamage} A={r.bonusDamage} T={r.total} expectA={expectA} {(ok ? "OK" : "MISMATCH")} hp→{e.Hp:0}");
        }

        IEnumerator StartMasterRun()
        {
            yield return Human(1f);
            var select = FindFirstObjectByType<CharacterSelectView>();
            float w = Time.realtimeSinceStartup + 5f;
            while (select == null && Time.realtimeSinceStartup < w) { yield return null; select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Event("FAIL no character select"); yield break; }
            select.BartenderButton.onClick.Invoke();
            yield return Human(0.6f);
            if (select.EndlessModeButton == null) { Event("FAIL no master mode button"); yield break; }
            select.EndlessModeButton.onClick.Invoke();
            yield return Human(0.6f);
            select.WeaponButtonA.onClick.Invoke();
            yield return Human(0.5f);
            select.StartButton.onClick.Invoke();
            Event("clicked 바텐더 → 마스터 도전 → 무기 A → 영업 시작");
            yield return Human(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen) { ClickNamed(tutorial, "DismissButton"); yield return Human(0.5f); }
        }

        string StatLine(GameManager g) =>
            $"U={g.Master.CocktailUniqueCount} atk+={g.Master.CocktailAttackPercent}% masterDamagePercent={g.Stats.masterDamagePercent:0.####} DamageBonus={g.Stats.DamageBonus:0.####} Stats.Damage={g.Stats.Damage:0.####}";

        IEnumerator CraftRow(GameManager g, string contains, string tag)
        {
            var b = Buttons(g.MasterHud, "Craft").FirstOrDefault(x => RowText(x).Contains(contains));
            if (b == null) { Event($"FAIL craft row not available: {contains}"); yield break; }
            string row = RowText(b);
            b.onClick.Invoke();
            Event($"[craft {tag}] clicked 만들기 \"{row}\" → msg=\"{g.MasterHud.LastMessage}\" fx={DrinkQualityFx.LastClip} {StatLine(g)}");
            yield return new WaitForSecondsRealtime(0.25f);
            MShot("m_craft_" + tag);
            yield return Human(0.5f);
        }

        IEnumerator UseCup(GameManager g, string button, string contains, string tag)
        {
            var b = Buttons(g.MasterHud, button).FirstOrDefault(x => RowText(x).Contains(contains));
            if (b == null) { Event($"FAIL {button} row not available: {contains}"); yield break; }
            string row = RowText(b);
            b.onClick.Invoke();
            Event($"[{tag}] clicked {button} \"{row}\" → msg=\"{g.MasterHud.LastMessage}\" fx={DrinkQualityFx.LastClip} cupShown={g.MasterHud.SubmitCupShown} " +
                  $"submitted={g.Master.State.SubmittedThisCycle} points={g.Master.State.SubmittedPointsThisCycle} bonusPoints={g.Master.State.BonusPointsThisCycle} preview={g.Master.State.PreviewFairReduction}% drunk={g.Player.DrunkDrink} affinity={g.Player.AffinityDrink}");
            yield return new WaitForSecondsRealtime(0.25f);
            MShot("m_" + tag);
            yield return Human(0.5f);
        }

        IEnumerator CaptureAffinity(GameManager g, string prefix, int want, float realLimit)
        {
            int got = 0, lastPlayed = MasterVfx.Played;
            float until = Time.realtimeSinceStartup + realLimit;
            while (got < want && g.State != GameState.GameOver && Time.realtimeSinceStartup < until)
            {
                HandleRewardScreens(g);
                if (MasterVfx.Played != lastPlayed)
                {
                    lastPlayed = MasterVfx.Played;
                    string clip = MasterVfx.LastClip;
                    if (clip != null && (clip.StartsWith("coffee_bean") || clip.StartsWith("cocktail_foam") || clip.StartsWith("tea_leaf")))
                    {
                        affVfxSeen++;
                        var fx = FindObjectsByType<MasterVfx>(FindObjectsSortMode.None).FirstOrDefault(v => v.Playing && v.ClipName == clip);
                        yield return new WaitForSecondsRealtime(HitFeedback.LowEffects ? 0.09f : 0.17f); // full 6노출: 정점 2번째 근처 / low 3노출(마지막 투명): 1번째
                        got++;
                        Event($"[{prefix}] affinity vfx clip={clip} scale={(fx != null ? fx.transform.localScale.x : 0f):0.##} outline={(fx != null ? fx.OutlineCount : -1)} frame={(fx != null ? fx.CurrentFrame : -1)} activeWorld={MasterVfx.ActiveWorld} low={HitFeedback.LowEffects}");
                        MShot($"m_{prefix}_{got}");
                        yield return null; // 캡처는 프레임 끝 — 다음 화면 전환과 겹치지 않게
                        lastPlayed = MasterVfx.Played;
                        continue;
                    }
                }
                yield return null;
            }
            Event($"[{prefix}] captured {got}/{want}");
        }

        int contToken; // 연속 캡처 1개만: 새 캡처를 시작하거나 0으로 두면 이전 캡처가 멈춘다
        IEnumerator Continuous(string name, float seconds)
        {
            var dir = Path.Combine(outDir, name);
            Directory.CreateDirectory(dir);
            int k = 0;
            float end = Time.realtimeSinceStartup + seconds;
            int token = ++contToken;
            while (contToken == token && Time.realtimeSinceStartup < end)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"c_{k++:0000}.png"));
                yield return new WaitForSecondsRealtime(0.1f);
            }
            Event($"continuous capture {name}: {k} frames @10fps real time (game ScreenCapture, not every rendered frame)");
        }

        IEnumerator AffinityOnly(GameManager g, string which)
        {
            bool tea = which == "tea";
            string recipe = tea ? "센차" : "드라이 마티니";
            var expectTarget = tea ? MonsterFamily.Cocktail : MonsterFamily.Coffee;
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) brew.onClick.Invoke();
            else { Event("FAIL brew button"); yield return EndMaster(g); yield break; }
            yield return Human(0.6f);
            yield return CraftRow(g, recipe, "a1_" + which);
            yield return UseCup(g, "Drink", tea ? "센차" : "마티니", "a2_drink_" + which);
            Event($"drunk={g.Player.DrunkDrink} affinity={g.Player.AffinityDrink} target={DrinkAffinity.TargetOf(g.Player.AffinityDrink)} expectTarget={expectTarget} guide=\"{DrinkAffinity.GuideLine(g.Player.AffinityDrink)}\"");
            if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) close.onClick.Invoke();
            yield return Human(0.8f);
            hitLog.Add($"---- after drinking {which} (affinity target = {expectTarget} family) ----");
            StartCoroutine(Continuous("continuous-full-" + which, 20f));
            yield return CaptureAffinity(g, "aff_full", 3, 120f);
            contToken++;
            yield return null;
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            Event($"effects level → low (settings save, isolated) low={HitFeedback.LowEffects}");
            StartCoroutine(Continuous("continuous-low-" + which, 20f));
            yield return CaptureAffinity(g, "aff_low", 3, 120f);
            contToken++;
            yield return null;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            Event($"AFFINITY CHECK drink={which} targetHits={affHits} targetMismatch={affBad} nonTargetHits={nonAffHits} nonTargetMismatch={nonAffBad} vfxSeen={affVfxSeen} skipped={MasterVfx.Skipped} activeWorld={MasterVfx.ActiveWorld}");
            Enemy.QaDirectHit = null;
            File.WriteAllLines(Path.Combine(outDir, "direct-hits.log"), hitLog);
            yield return EndMaster(g);
        }

        /// <summary>QA 전용 진짜 해상도 렌더 캡처: 메인 카메라(메뉴엔 임시 카메라)와 오버레이 캔버스를 w×h 렌더 텍스처에 그린다(창 크기·전경과 무관, 업스케일 아님).</summary>
        IEnumerator RtShot(string name, int w, int h)
        {
            var cam = Camera.main;
            GameObject tmp = null;
            if (cam == null) { tmp = new GameObject("QaShotCamera"); cam = tmp.AddComponent<Camera>(); cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = ~0; }
            var rt = new RenderTexture(w, h, 24);
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var orders = canvases.Select(c => c.sortingOrder).ToList();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 1000; }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var act = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = act;
            cam.targetTexture = prev;
            for (int i = 0; i < canvases.Count; i++) if (canvases[i] != null) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = null; canvases[i].sortingOrder = orders[i]; }
            File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release(); Destroy(rt);
            if (tmp != null) Destroy(tmp);
            Event($"rtshot {name} {w}x{h}");
        }

        /// <summary>창(1280×720) 실제 화면 캡처 + 1920×1080 렌더 캡처 한 쌍.</summary>
        IEnumerator PairShot(string name)
        {
            MShot(name + "_720");
            yield return null;
            yield return null;
            yield return RtShot(name + "_1080", 1920, 1080);
        }

        /// <summary>2026-11-11 세션 7: 무기 선택 화면 수정 확인 + Steam 스크린샷 후보(-cct-playverify-shots). 화면 선택은 실제 버튼, 전투 이동은 입력 대행. 값 쓰기 없음.</summary>
        IEnumerator ShotsFlow()
        {
            Application.runInBackground = true;
            shotDir = Path.Combine(outDir, "shots");
            Directory.CreateDirectory(shotDir);
            Event("SHOTS QA SCENARIO (automated input, not human play): real buttons for screens, input agent for movement; no stat/time/spawn writes");
            yield return Human(0.8f);
            yield return PairShot("s01_title");
            var title = FindFirstObjectByType<TitleView>();
            if ((title == null || !ClickNamed(title, "StartButton")) && FindFirstObjectByType<CharacterSelectView>() == null) { Event("FAIL title start button"); yield break; }
            var select = FindFirstObjectByType<CharacterSelectView>();
            float w0 = Time.realtimeSinceStartup + 5f;
            while (select == null && Time.realtimeSinceStartup < w0) { yield return null; select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Event("FAIL no character select"); yield break; }
            yield return Human(0.8f);
            yield return PairShot("s02_job_select");
            foreach (var job in new[] { "tea", "bartender" })
            {
                (job == "tea" ? select.TeaSommelierButton : select.BartenderButton).onClick.Invoke();
                yield return Human(0.8f);
                select.StandardModeButton.onClick.Invoke();
                yield return Human(0.5f);
                yield return PairShot($"s03_weapon_{job}_standard");
                select.EndlessModeButton.onClick.Invoke();
                yield return Human(0.5f);
                yield return PairShot($"s04_weapon_{job}_master");
                if (job == "tea") { select.WeaponBackButton.onClick.Invoke(); yield return Human(0.8f); }
            }
            select.WeaponButtonA.onClick.Invoke();
            yield return Human(0.5f);
            select.StartButton.onClick.Invoke();
            Event("clicked 바텐더 → 마스터 도전 → 무기 A → 영업 시작");
            yield return Human(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen) { ClickNamed(tutorial, "DismissButton"); yield return Human(0.5f); }
            var g = GameManager.Instance;
            if (g == null || g.Master == null) { Event("FAIL master run not started"); yield break; }
            BindGame(g);
            // 2026-11-12 준비 단계(다음 웨이브 예고) 캡처 → 전투 시작
            yield return Human(1f);
            yield return PairShot("s04b_prep_wave1");
            yield return PrepStartShot(g, null);
            yield return new WaitForSecondsRealtime(9f);
            yield return PairShot("s05_combat_wave1");
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) brew.onClick.Invoke();
            yield return Human(0.6f);
            var best = Buttons(g.MasterHud, "Craft").FirstOrDefault(x => RowText(x).Contains("최고"));
            if (best != null) { best.onClick.Invoke(); Event($"craft \"{RowText(best)}\" → {g.MasterHud.LastMessage}"); yield return new WaitForSecondsRealtime(0.2f); yield return RtShot("s06_brew_quality_1080", 1920, 1080); MShot("s06_brew_quality_720"); }
            yield return Human(0.6f);
            var drink = Buttons(g.MasterHud, "Drink").FirstOrDefault();
            if (drink != null) { drink.onClick.Invoke(); yield return Human(0.5f); }
            if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) close.onClick.Invoke();
            yield return Human(0.8f);
            yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.AwaitChoice, 600f, "collect-1..5");
            if (g.Master.State.Phase == MasterPhase.AwaitChoice)
            {
                yield return PairShot("s07_await_choice");
                if (Buttons(g.MasterHud, "Fair").FirstOrDefault() is Button fair) fair.onClick.Invoke();
                yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.Transition, 300f, "clearing");
                float t0 = Time.realtimeSinceStartup;
                while (g.Master.State.Phase == MasterPhase.Transition && Time.realtimeSinceStartup - t0 < 2.7f) yield return null; // 글자(07 프레임, ≈2.1초) 이후·전환(3.5초) 전
                if (g.Master.State.Phase == MasterPhase.Transition) yield return RtShot("s08_fair_banner_1080", 1920, 1080);
                yield return WaitPhase(g, () => g.Master.State.Segment == MasterSegment.Fair && Enemy.Active.Count >= 6, 120f, "fair-crowd");
                // 2026-11-12 박람회 밀도 실측: 박람회 1/3 동안 최대 동시·처리 시간·플레이어 체력 변화 + 10fps 게임 자체 연속 캡처 8초
                StartCoroutine(Continuous("continuous-fair", 8f));
                long fw = g.Master.State.Wave; float fs = g.RunTime, hp0 = g.Player.Hp, hpMin = hp0; int peak = 0; bool crowdShot = false;
                float fUntil = Time.realtimeSinceStartup + 90f;
                while (g.State != GameState.GameOver && g.Master.State.Wave == fw && Time.realtimeSinceStartup < fUntil)
                {
                    HandleRewardScreens(g);
                    int alive = Enemy.Active.Count(e => e != null && !e.IsDead && e.MasterWaveTag == fw);
                    peak = Mathf.Max(peak, alive); hpMin = Mathf.Min(hpMin, g.Player.Hp);
                    if (!crowdShot && alive >= 14) { crowdShot = true; yield return PairShot("s09_fair_crowd"); }
                    yield return null;
                }
                contToken++;
                if (!crowdShot) yield return PairShot("s09_fair_crowd");
                Event($"FAIR DENSITY wave={fw} target={MasterRules.FairCountPerWave} peakAlive={peak} combatSeconds={g.RunTime - fs:0.0} hpStart={hp0:0} hpMin={hpMin:0} burst={MasterRules.FairBurstSize} interval={MasterRules.FairBurstInterval:0.00}s");
            }
            yield return EndMaster(g);
            yield return PairShot("s10_result");
        }

        // ───── 2026-11-12 사용자 제보 조사: 직업·무기 선택 화면 '이상한 이펙트' (-cct-playverify-selectseq) ─────
        /// <summary>화면 진입 직후 100ms 연속 캡처 + 보이는 효과 객체 목록(포커스 링·월드 스프라이트·파티클·마스터 효과 풀). 값 쓰기 없음.</summary>
        IEnumerator SelectSeq(string tag, int frames)
        {
            var dir = Path.Combine(outDir, "selectseq");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < frames; i++)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{tag}_{i:00}.png"));
                if (i == 0 || i == frames - 1) Inventory($"{tag}@{i}");
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        void Inventory(string at)
        {
            var rings = FindObjectsByType<CafeUiFocus>(FindObjectsSortMode.None).Where(f => f.isActiveAndEnabled)
                .Select(f => { var r = f.transform.Find("CafeFocus")?.GetComponent<Image>(); return r != null && r.enabled ? f.gameObject.name : null; })
                .Where(n => n != null).ToList();
            var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var world = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(s => s.enabled && s.gameObject.activeInHierarchy && s.sprite != null)
                .Select(s => s.gameObject.name).GroupBy(n => n).Select(g => $"{g.Key}x{g.Count()}").ToList();
            var ps = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p => p.isPlaying).Select(p => p.gameObject.name).ToList();
            var anims = FindObjectsByType<SpriteAnimator>(FindObjectsSortMode.None).Where(a => a.isActiveAndEnabled).Select(a => a.gameObject.name).GroupBy(n => n).Select(g => $"{g.Key}x{g.Count()}").ToList();
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c => $"{c.name}(depth={c.depth},enabled={c.enabled})").ToList();
            var canv = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.isActiveAndEnabled).Select(c => $"{c.name}:{c.renderMode}:{c.sortingOrder}").ToList();
            Event($"INVENTORY {at} selected={(sel != null ? sel.name : "-")} focusRings=[{string.Join(",", rings)}] worldSprites=[{string.Join(",", world)}] particles=[{string.Join(",", ps)}] spriteAnimators=[{string.Join(",", anims)}] masterVfxActive={MasterVfx.ActiveWorld}/{MasterVfx.ActiveUi} pooled={MasterVfx.PooledCount} cameras=[{string.Join(",", cams)}] canvases=[{string.Join(",", canv)}]");
        }

        IEnumerator SelectSeqFlow()
        {
            Application.runInBackground = true;
            Event("SELECT-SCREEN EFFECT INVESTIGATION (QA capture, automated buttons; fresh data dir = first start)");
            yield return SelectSeq("a_title", 8);
            var title = FindFirstObjectByType<TitleView>();
            if (title == null || !ClickNamed(title, "StartButton")) { Event("FAIL title start (already past title?)"); }
            yield return SelectSeq("b_job_entry", 20);
            var select = FindFirstObjectByType<CharacterSelectView>();
            if (select == null) { Event("FAIL no select"); yield break; }
            select.BartenderButton.onClick.Invoke();
            Event("clicked 바텐더");
            yield return SelectSeq("c_weapon_entry", 20);
            select.EndlessModeButton.onClick.Invoke();
            Event("clicked 마스터 도전");
            yield return SelectSeq("d_master_mode", 8);
            select.WeaponButtonA.onClick.Invoke();
            Event("clicked 무기 A");
            yield return SelectSeq("e_weapon_chosen", 8);
            select.WeaponBackButton.onClick.Invoke();
            Event("clicked 뒤로");
            yield return SelectSeq("f_job_again", 10);
            select.TeaSommelierButton.onClick.Invoke();
            Event("clicked 티 소믈리에");
            yield return SelectSeq("g_tea_weapon", 10);
        }

        // ───── 2026-11-12 사용자 제보: 도전 시작 첫 제조 창 우측 상단 효과 (-cct-playverify-brewfx) ─────
        string FxState(GameManager g)
        {
            var slot = g.MasterHud.FxSlot;
            var vs = slot.GetComponentsInChildren<MasterVfx>(true);
            var imgs = slot.GetComponentsInChildren<Image>(true).Where(i => i.enabled && i.gameObject.activeInHierarchy && i.sprite != null).Select(i => $"{i.gameObject.name}:{i.sprite.name}");
            return $"slotActive={slot.gameObject.activeInHierarchy} vfx=[{string.Join(",", vs.Select(v => $"{v.ClipName}/playing={v.Playing}/frame={v.CurrentFrame}/active={v.gameObject.activeSelf}"))}] visibleImages=[{string.Join(",", imgs)}] activeUi={MasterVfx.ActiveUi} played={MasterVfx.Played}";
        }

        IEnumerator BrewFxFlow()
        {
            Application.runInBackground = true;
            shotDir = Path.Combine(outDir, "shots");
            Directory.CreateDirectory(shotDir);
            Event("BREW-WINDOW FX INVESTIGATION (QA, fresh data dir, automated buttons)");
            yield return StartMasterRun();
            var g = GameManager.Instance;
            if (g == null || g.Master == null) { Event("FAIL master run not started"); yield break; }
            BindGame(g);
            yield return Human(1f);
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) brew.onClick.Invoke();
            yield return null; yield return null;
            Event("first open " + FxState(g));
            MShot("x01_first_open_0ms");
            yield return new WaitForSecondsRealtime(0.5f);
            Event("first open +500ms " + FxState(g));
            MShot("x02_first_open_500ms");
            var craft = Buttons(g.MasterHud, "Craft").FirstOrDefault(x => RowText(x).Contains("최고")) ?? Buttons(g.MasterHud, "Craft").FirstOrDefault(x => RowText(x).Contains("[커피]"));
            if (craft == null) { Event("FAIL no craftable coffee"); yield break; }
            craft.onClick.Invoke();
            Event($"crafted \"{RowText(craft)}\" " + FxState(g));
            foreach (int ms in new[] { 80, 250, 500, 800 })
            {
                yield return new WaitForSecondsRealtime(ms == 80 ? 0.08f : ms == 250 ? 0.17f : ms == 500 ? 0.25f : 0.3f);
                Event($"craft +{ms}ms " + FxState(g));
                MShot($"x03_craft_{ms}ms");
            }
            // 효과 재생 중 바로 닫기 → 다시 열기(정지 잔상 확인)
            var craft2 = Buttons(g.MasterHud, "Craft").FirstOrDefault(x => RowText(x).Contains("[커피]"));
            if (craft2 != null)
            {
                craft2.onClick.Invoke();
                Event($"crafted2 \"{RowText(craft2)}\" " + FxState(g));
                yield return new WaitForSecondsRealtime(0.12f);
                if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) close.onClick.Invoke();
                Event("closed mid-effect " + FxState(g));
                yield return new WaitForSecondsRealtime(1.0f);
                if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew2) brew2.onClick.Invoke();
                yield return null; yield return null;
                Event("reopen " + FxState(g));
                MShot("x04_reopen_0ms");
                yield return new WaitForSecondsRealtime(0.15f);
                Event("reopen +150ms " + FxState(g));
                MShot("x05_reopen_150ms");
                yield return new WaitForSecondsRealtime(0.8f);
                Event("reopen +950ms " + FxState(g));
                MShot("x06_reopen_950ms");
            }
            g.CloseMasterBrew();
            yield return EndMaster(g);
        }

        // ───── 2026-11-12 커피·차 보조 전투 특성 실제 빌드 증거(-cct-playverify-traits) ─────
        int traitDrinkBonusHits, traitSlowSeen;
        void OnTraitHit(Enemy e, DamageResult r)
        {
            if (r.drinkBonus > 0) { traitDrinkBonusHits++; if (traitDrinkBonusHits <= 5) Event($"[trait] espresso elite hit {e.Def.name} B={r.baseDamage} drink+={r.drinkBonus} affinity+={r.bonusDamage} T={r.total}"); }
            if (e != null && e.DrinkSlowed) traitSlowSeen++;
        }

        IEnumerator DrinkFromBrew(GameManager g, string recipe, string tag)
        {
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) brew.onClick.Invoke();
            yield return Human(0.6f);
            yield return CraftRow(g, recipe, tag + "_craft");
            yield return UseCup(g, "Drink", recipe, tag + "_drink");
            Event($"[{tag}] trait={g.Player.DrinkTrait} attackSpeedBonus={g.Stats.AttackSpeedBonus:0.###} footer=\"{g.MasterHud.BrewFooterText.Replace('\n', '/')}\"");
            yield return PairShot(tag + "_brew_footer");
            if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) close.onClick.Invoke();
            yield return Human(0.6f);
        }

        IEnumerator ContinueAndDrink(GameManager g, string tag)
        {
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) brew.onClick.Invoke();
            yield return Human(0.6f);
            yield return PairShot(tag + "_before_continue");
            var b = Buttons(g.MasterHud, "ContinueSteep").FirstOrDefault();
            if (b == null) { Event($"FAIL [{tag}] no continue button"); yield break; }
            b.onClick.Invoke();
            Event($"[{tag}] clicked 이어 우리기 → msg=\"{g.MasterHud.LastMessage}\"");
            yield return Human(0.5f);
            var d = Buttons(g.MasterHud, "Drink").FirstOrDefault(x => RowText(x).Contains("번째 우림"));
            if (d == null) { Event($"FAIL [{tag}] no steep cup"); yield break; }
            string row = RowText(d);
            d.onClick.Invoke();
            Event($"[{tag}] drank \"{row.Replace('\n', '/')}\" trait={g.Player.DrinkTrait} attackSpeedBonus={g.Stats.AttackSpeedBonus:0.###}");
            yield return Human(0.4f);
            yield return PairShot(tag + "_after_drink");
            if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) close.onClick.Invoke();
            yield return Human(0.6f);
        }

        IEnumerator WatchTrait(GameManager g, string tag, float seconds, System.Func<bool> captureWhen)
        {
            float until = Time.realtimeSinceStartup + seconds;
            int splash0 = RunDrinkTrait.SplashHits, slow0 = RunDrinkTrait.SlowsApplied, shots = 0;
            while (Time.realtimeSinceStartup < until && g.State != GameState.GameOver)
            {
                HandleRewardScreens(g);
                if (shots < 2 && captureWhen())
                {
                    shots++;
                    MShot($"{tag}_moment_{shots}");
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.4f);
                }
                yield return null;
            }
            Event($"[{tag}] splashHits+={RunDrinkTrait.SplashHits - splash0} slows+={RunDrinkTrait.SlowsApplied - slow0} espressoEliteHits={traitDrinkBonusHits} shots={shots} {MasterLine(g)}");
        }

        IEnumerator TraitsFlow()
        {
            Application.runInBackground = true;
            shotDir = Path.Combine(outDir, "shots");
            Directory.CreateDirectory(shotDir);
            Event("TRAITS QA SCENARIO (not natural play): isolated data copy + bean fixture (see QA-FIXTURE.txt); real buttons, input agent movement; no stat/time/spawn writes");
            yield return StartMasterRun();
            var g = GameManager.Instance;
            if (g == null || g.Master == null) { Event("FAIL master run not started"); yield break; }
            BindGame(g);
            Enemy.QaDirectHit = OnTraitHit;
            // 준비 단계에서 차 1번째 → 전투, 이어 우리기 2번째(둔화) → 3번째(범위)
            yield return PrepStartShot(g, null);
            yield return DrinkFromBrew(g, "센차", "t2_tea1");
            yield return WatchTrait(g, "t2_tea1", 8f, () => false);
            yield return ContinueAndDrink(g, "t3_tea2");
            int sl0 = RunDrinkTrait.SlowsApplied;
            yield return WatchTrait(g, "t3_tea2", 20f, () => RunDrinkTrait.SlowsApplied > sl0);
            yield return ContinueAndDrink(g, "t4_tea3");
            int s3 = RunDrinkTrait.SplashHits;
            yield return WatchTrait(g, "t4_tea3", 20f, () => RunDrinkTrait.SplashHits > s3);
            // 박람회(진상 손님 12명 밀집·강적 판정)에서 핸드 드립 보조 적중과 에스프레소 강적 +20%
            yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.AwaitChoice, 400f, "collect-to-choice");
            if (g.Master.State.Phase == MasterPhase.AwaitChoice && Buttons(g.MasterHud, "Fair").FirstOrDefault() is Button fair) { yield return Human(0.6f); fair.onClick.Invoke(); Event("clicked 박람회 도전"); }
            yield return WaitPhase(g, () => g.Master.State.Segment == MasterSegment.Fair && g.Master.State.Phase == MasterPhase.Wave && Enemy.Active.Count >= 5, 120f, "fair-crowd");
            yield return DrinkFromBrew(g, "핸드 드립", "t1_drip");
            int s0 = RunDrinkTrait.SplashHits;
            yield return WatchTrait(g, "t1_drip", 15f, () => RunDrinkTrait.SplashHits > s0);
            yield return DrinkFromBrew(g, "에스프레소", "t5_espresso");
            int eh0 = traitDrinkBonusHits;
            yield return WatchTrait(g, "t5_espresso", 20f, () => traitDrinkBonusHits > eh0);
            Enemy.QaDirectHit = null;
            yield return EndMaster(g);
        }

        IEnumerator MechanicsFlow()
        {
            Application.runInBackground = true;
            shotDir = Path.Combine(outDir, "shots");
            Directory.CreateDirectory(shotDir);
            Event("MECHANICS QA SCENARIO (not natural play): isolated -cct-data-dir fixture stock (see report) + fixed QA seed if -cct-playverify-seed; " +
                  "screens via real buttons, movement via input agent; no HP/enemy/spawn/time/reward writes; effects-low via settings save only");
            Event($"fixture stock gin={DrinkLab.Owned(DrinkIngredients.Gin)} campari={DrinkLab.Owned(DrinkIngredients.Campari)} sweet_vermouth={DrinkLab.Owned(DrinkIngredients.SweetVermouth)} dry_vermouth={DrinkLab.Owned(DrinkIngredients.DryVermouth)} coffee_bean={DrinkLab.Owned(DrinkIngredients.CoffeeBean)} effects={SettingsService.Current.effectsLevel}");
            yield return StartMasterRun();
            var g = GameManager.Instance;
            if (g == null || g.Master == null) { Event("FAIL master run not started"); yield break; }
            BindGame(g);
            Enemy.QaDirectHit = OnQaHit;
            periodicOn = true;
            StartCoroutine(Periodic());
            Event("run started " + MasterLine(g) + " " + StatLine(g));
            if (g.Master.State.Phase == MasterPhase.Prep && !System.Array.Exists(System.Environment.GetCommandLineArgs(), x => x == "-cct-playverify-shots"))
                yield return PrepStartShot(g, null); // 2026-11-12 준비 단계 → 전투 시작

            // 2026-11-11 세션 6: 칵테일/차 상성만 검증(-cct-playverify-mech-affinity cocktail|tea). 1잔 제조 → 마시기 → full 3 / low 3 캡처 + 10fps 게임 자체 연속 캡처.
            var argv = System.Environment.GetCommandLineArgs();
            int ai = System.Array.IndexOf(argv, "-cct-playverify-mech-affinity");
            if (ai >= 0 && ai + 1 < argv.Length) { yield return AffinityOnly(g, argv[ai + 1]); yield break; }

            // 기준 적중(음료 없음, 칵테일 0종) 6초
            yield return new WaitForSecondsRealtime(6f);
            Event($"baseline hits: affinity={affHits} nonAffinity={nonAffHits} bad={affBad + nonAffBad} " + StatLine(g));

            // 1) 칵테일 고유 종류 + 2) 제출
            if (Buttons(g.MasterHud, "BrewButton").FirstOrDefault() is Button brew) { brew.onClick.Invoke(); Event($"clicked 음료 만들기 open={g.MasterHud.BrewOpen} timeScale={Time.timeScale}"); }
            else { Event("FAIL brew button"); yield return EndMaster(g); yield break; }
            yield return Human(0.6f);
            MShot("m_brew_open");
            float dmg0 = g.Stats.Damage;
            yield return CraftRow(g, "드라이 마티니", "1_martini");
            float dmg1 = g.Stats.Damage;
            yield return CraftRow(g, "드라이 마티니", "2_martini_repeat");
            float dmg2 = g.Stats.Damage;
            yield return CraftRow(g, "네그로니", "3_negroni");
            float dmg3 = g.Stats.Damage;
            Event($"COCKTAIL CHECK dmg0={dmg0:0.####} after martini={dmg1:0.####} (x{dmg1 / dmg0:0.####}) after repeat={dmg2:0.####} (same={Mathf.Approximately(dmg1, dmg2)}) after negroni={dmg3:0.####} (x{dmg3 / dmg0:0.####}) final {StatLine(g)}");
            yield return CraftRow(g, "더블 에스프레소", "4_espresso_best");
            yield return CraftRow(g, "핸드 드립", "5_pourover");
            // 최고 품질 커피 제출(150p) → 칵테일 1잔 제출(100p)
            yield return UseCup(g, "Submit", "최고", "6_submit_best");
            yield return UseCup(g, "Submit", "마티니", "7_submit_martini");
            // 커피 마시기 → 차 계열 상성
            yield return UseCup(g, "Drink", "핸드 드립", "8_drink_coffee");
            MShot("m_9_brew_footer");
            Event($"SUBMIT CHECK submitted={g.Master.State.SubmittedThisCycle} points={g.Master.State.SubmittedPointsThisCycle} expectPoints=250 preview={g.Master.State.PreviewFairReduction}% expect=round(250*5/(100*1))=13 footer=\"{g.MasterHud.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Footer")?.text?.Replace('\n', '/')}\"");
            if (Buttons(g.MasterHud, "Close").FirstOrDefault() is Button close) close.onClick.Invoke();
            yield return Human(0.8f);
            hitLog.Add("---- after drinking coffee (affinity target = Tea family) ----");

            // 3) 상성 적중 full → low
            yield return CaptureAffinity(g, "aff_full", 3, 90f);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            Event($"effects level → low (settings save, isolated) low={HitFeedback.LowEffects}");
            yield return CaptureAffinity(g, "aff_low", 3, 90f);
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            Event($"AFFINITY CHECK targetHits={affHits} targetMismatch={affBad} nonTargetHits={nonAffHits} nonTargetMismatch={nonAffBad} vfxSeen={affVfxSeen} skipped={MasterVfx.Skipped} activeWorld={MasterVfx.ActiveWorld}");

            // 2b) 박람회 접수 스냅샷과 실제 박람회 적 HP
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), x => x == "-cct-playverify-mech-skipfair")) { Enemy.QaDirectHit = null; File.WriteAllLines(Path.Combine(outDir, "direct-hits.log"), hitLog); yield return EndMaster(g); yield break; }
            yield return WaitPhase(g, () => g.Master.State.Phase == MasterPhase.AwaitChoice, 600f, "collect-1..5");
            if (g.Master.State.Phase == MasterPhase.AwaitChoice && Buttons(g.MasterHud, "Fair").FirstOrDefault() is Button fair)
            {
                yield return Human(0.6f);
                fair.onClick.Invoke();
                Event($"clicked 박람회 도전 snapshot={g.Master.State.FairReductionSnapshot}% " + MasterLine(g));
                yield return WaitPhase(g, () => g.Master.State.Segment == MasterSegment.Fair && g.Master.State.Phase == MasterPhase.Wave && Enemy.Active.Any(e => e != null && !e.IsDead), 300f, "to-fair");
                var fe = Enemy.Active.FirstOrDefault(e => e != null && !e.IsDead);
                if (fe != null)
                {
                    long r = g.Master.State.FairReductionSnapshot;
                    float scale = MasterChallengeDirector.Scale(EndlessRules.HpScale(g.Master.State.Wave), MasterRules.ReductionFactor(r));
                    Event($"FAIR CHECK wave={g.Master.State.Wave} enemy={fe.Def.name} maxHp={fe.MaxHp:0.##} hpScale={fe.HpScale:0.####} defHp={MasterRules.FairCustomerHp} expectScale=HpScale({g.Master.State.Wave})={EndlessRules.HpScale(g.Master.State.Wave):0.####}×(1-{r}/100)={scale:0.####} expectMaxHp≈{MasterRules.FairCustomerHp * scale:0.##}");
                    yield return Human(0.5f);
                    MShot("m_10_fair_enemies");
                }
            }
            Enemy.QaDirectHit = null;
            File.WriteAllLines(Path.Combine(outDir, "direct-hits.log"), hitLog);
            yield return EndMaster(g);
        }
    }
}
#endif
