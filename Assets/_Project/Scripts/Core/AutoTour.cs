#if CCT_QA_TOUR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// QA 검증 빌드 전용 자동 투어(CCT_QA_TOUR 심볼에서만 컴파일, LaunchOptions -cct-autotour, -cct-data-dir 필수).
    /// 일반 프리뷰/출시 빌드에는 들어가지 않는다(QaToolIsolationTests).
    /// 모드(-cct-autotour-mode):
    /// - screens: 실제 게임 경로(타이틀·선택 화면 버튼·첫 안내·전투·카드·커피 트리·보스·일시정지·결과·재시작·타이틀 복귀)를
    ///   공개 API로 진행하며 PNG 저장. 보스 4종과 불/독은 메뉴를 닫은 상태로 Time.captureFramerate 30 연속 프레임(JPG)과
    ///   프레임별 CSV로 녹화한다(E1/E2 보완).
    /// - motion: 4무기×좌우 idle/move/정지/공격/피격/사망 전환을 연속 프레임과 프레임별 클립·앵커 CSV로 녹화.
    /// - all: screens 뒤 motion.
    /// - audio: AudioListener 최종 출력 탭(QaAudioTap)으로 볼륨 0/Pause/보상창/재시작 구간의 RMS·피크와 WAV를 남긴다.
    /// 강제 경로(보고 시 반드시 표기): 보스 즉시 소환(BossDirector.SpawnNow — 일정 시간만 건너뜀), 일반 스폰 정지·적/픽업 제거(녹화 구간), AddIngredient로 카드/커피 보상 유도,
    /// MarkPlayerDiedForTests·장판 피해 사망, GiveUp 포기, AutoInputOverride 이동, RequestHybrid 하이브리드 예약,
    /// 화염/독 레벨 직접 부여, 관찰 중 회복, 정지 표적(속도0·접촉0·고HP 복제 정의) — 정상 플레이 완주·키보드 UX·밸런스 검증이 아니다.
    /// </summary>
    public class AutoTour : MonoBehaviour
    {
        const int CaptureFps = 30;

        string outDir;
        string mode = "screens";
        readonly StringBuilder log = new StringBuilder();
        int errors, exceptions, shots, recordedFrames;
        float started;

        static readonly (Job job, bool weaponA, string label)[] Combos =
        {
            (Job.Bartender, true, "bartender_rapier"),
            (Job.Bartender, false, "bartender_gun"),
            (Job.TeaSommelier, true, "tea_sword"),
            (Job.TeaSommelier, false, "tea_bow"),
        };

        public void Init(string dir, string tourMode)
        {
            Application.runInBackground = true; // 검수 투어만: 다른 창에 가려져도 렌더 캡처를 끝낸다.
            outDir = dir;
            if (!string.IsNullOrEmpty(tourMode)) mode = tourMode.ToLowerInvariant();
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions++;
            else if (type == LogType.Error || type == LogType.Assert) errors++;
            else return;
            Line($"[{type}] {condition}");
        }

        void Fail(string text)
        {
            errors++;
            Line("FAIL " + text);
        }

        void Line(string text) => log.AppendLine($"{Time.realtimeSinceStartup - started,7:0.00}s {DateTime.UtcNow:HH:mm:ss.fff}Z {text}");

        readonly StringBuilder shotSprites = new StringBuilder("frame,kind,sprite,object,sortingLayer,sortingOrder,alpha,screenX,screenY\n");

        void FlushLog()
        {
            File.WriteAllText(Path.Combine(outDir, "tour-log.txt"), log.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, "shots-sprites.csv"), shotSprites.ToString(), new UTF8Encoding(false));
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        // ── 오버레이 ─────────────────────────────────────────────

        static bool OverlayOpen(GameManager game) =>
            game != null && (game.CardView.IsOpen || game.SkillView.IsOpen || game.HybridView.IsOpen || game.IsPaused);

        /// <summary>자연 발생한 보상 창(카드/커피 트리/하이브리드)은 첫 선택지로 닫아 시간정지를 푼다(기록 남김).</summary>
        bool ResolveRewardScreens(GameManager game, string where)
        {
            if (game == null) return false;
            if (game.CardView.IsOpen) { game.CardView.Choose(0); Line($"auto-resolved card reward ({where})"); return true; }
            if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen) { game.SkillView.Continue(); Line($"auto-closed coffee tree ({where})"); return true; }
            if (game.HybridView.IsOpen) { game.HybridView.Unlock(); Line($"auto-unlocked hybrid ({where})"); return true; }
            return false;
        }

        /// <summary>E1/E2: 전투 캡처 전 보상 창을 모두 닫고 메뉴가 없는 Playing 상태를 확인한다.</summary>
        IEnumerator ClearOverlays(GameManager game, string where)
        {
            for (int i = 0; i < 12 && game != null && OverlayOpen(game); i++)
            {
                ResolveRewardScreens(game, where);
                yield return Wait(0.35f);
            }
            if (OverlayOpen(game)) Fail($"overlay still open before {where}");
        }

        /// <summary>녹화 구간: 일반 스폰·보스 일정을 멈추고 기존 적/탄/픽업을 지운다(강제, 보고 대상).</summary>
        void FreezeWorld(GameManager game, bool keepBoss = false)
        {
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            foreach (var e in new List<Enemy>(Enemy.Active))
                if (e != null && !(keepBoss && e == game.ActiveBoss)) Destroy(e.gameObject);
            foreach (var p in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            foreach (var p in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            Line($"world frozen (spawner/director off, enemies/pickups/enemy shots cleared{(keepBoss ? ", boss kept" : "")})");
        }

        // ── 캡처 ─────────────────────────────────────────────

        IEnumerator Shot(string name, GameManager combatGame = null)
        {
            if (combatGame != null)
            {
                // 전투 화면: 캡처 순간 보상 창이 새로 열려 있으면 닫고 다시 찍는다(최대 4회). 그래도 열려 있으면 실패로 남긴다.
                for (int attempt = 0; ; attempt++)
                {
                    yield return ClearOverlays(combatGame, name);
                    yield return new WaitForEndOfFrame();
                    if (!OverlayOpen(combatGame)) break;
                    Line($"overlay opened right before combat shot {name} — retry {attempt + 1}");
                    if (attempt >= 3) { Fail($"overlay open at combat shot {name}"); break; }
                }
            }
            else yield return new WaitForEndOfFrame();
            var path = Path.Combine(outDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            shots++;
            shotSprites.AppendLine($"#shot {shots} {name}");
            LogVisibleSprites(shotSprites, shots, Camera.main);
            Line($"shot {name}.png ({Screen.width}x{Screen.height})");
            FlushLog(); // Partial evidence is readable before the long tour finishes.
            yield return null;
            yield return null;
        }

        // QA only: drive the actual Selectable state machine, keeping its production SpriteSwap binding.
        IEnumerator CaptureSelectableStates(Button button, string label)
        {
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            button.OnPointerExit(pointer);
            yield return Wait(.2f); yield return Shot(label + "_normal");
            button.OnPointerEnter(pointer);
            yield return Wait(.2f); yield return Shot(label + "_hover");
            button.OnPointerDown(pointer);
            yield return Wait(.2f); yield return Shot(label + "_pressed");
            button.OnPointerUp(pointer); button.OnPointerExit(pointer);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            button.interactable = false;
            yield return Wait(.2f); yield return Shot(label + "_disabled");
            button.interactable = true;
        }

        /// <summary>Time.captureFramerate=30으로 게임 시간을 프레임당 1/30초로 고정하고 매 프레임 실제 엔진 화면을 JPG로 저장한다.
        /// step(f)는 그 프레임 Update 직후에 불려 다음 입력을 정한다. row()는 캡처 순간의 상태 CSV 열. png(f)가 이름을 주면 같은 프레임을 PNG로도 저장.</summary>
        IEnumerator Record(string clip, int frames, string header, Action<int> step, Func<string> row,
            Func<int, string> png = null, GameManager game = null)
        {
            var dir = Path.Combine(outDir, "video", clip);
            Directory.CreateDirectory(dir);
            var csv = new StringBuilder();
            csv.AppendLine("frame,gameTime,timeScale,overlay," + header);
            // 아트 검수 공통 에셋 매핑: 매 프레임 실제로 그려지는 SpriteRenderer/UI Image의 스프라이트·정렬·불투명도
            var spritesCsv = new StringBuilder();
            spritesCsv.AppendLine("frame,kind,sprite,object,sortingLayer,sortingOrder,alpha,screenX,screenY");
            var camForSprites = Camera.main;
            Time.captureFramerate = CaptureFps;
            Line($"record {clip} start ({frames} frames @{CaptureFps}fps game time, {Screen.width}x{Screen.height})");
            float t0 = Time.time;
            int overlayFrames = 0;
            try
            {
                for (int f = 0; f < frames; f++)
                {
                    yield return null;
                    step?.Invoke(f);
                    yield return new WaitForEndOfFrame();
                    bool overlay = OverlayOpen(game);
                    if (overlay) overlayFrames++;
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, $"{f:D5}.jpg"), tex.EncodeToJPG(92));
                    var still = png?.Invoke(f);
                    if (!string.IsNullOrEmpty(still))
                    {
                        File.WriteAllBytes(Path.Combine(outDir, still + ".png"), tex.EncodeToPNG());
                        shots++;
                        Line($"shot {still}.png from {clip} frame {f}");
                    }
                    Destroy(tex);
                    csv.AppendLine($"{f},{F(Time.time - t0)},{F(Time.timeScale)},{(overlay ? 1 : 0)},{row?.Invoke()}");
                    LogVisibleSprites(spritesCsv, f, camForSprites);
                    recordedFrames++;
                    // 녹화 중 자연 보상 창이 열리면 다음 프레임 전에 닫는다(overlay 열에 기록).
                    if (overlay && game != null && !game.IsPaused) ResolveRewardScreens(game, clip);
                }
            }
            finally
            {
                Time.captureFramerate = 0;
                File.WriteAllText(Path.Combine(dir, "frames.csv"), csv.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, "sprites.csv"), spritesCsv.ToString(), new UTF8Encoding(false));
            }
            Line($"record {clip} done (overlay frames={overlayFrames})");
        }

        static void LogVisibleSprites(StringBuilder sb, int frame, Camera cam)
        {
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.sprite == null || !sr.enabled || !sr.gameObject.activeInHierarchy || sr.color.a <= 0.001f) continue;
                Vector3 sp = cam != null ? cam.WorldToScreenPoint(sr.transform.position) : Vector3.zero;
                if (cam != null && (sp.x < -200 || sp.x > Screen.width + 200 || sp.y < -200 || sp.y > Screen.height + 200)) continue;
                var group = sr.GetComponentInParent<UnityEngine.Rendering.SortingGroup>();
                int order = group != null ? group.sortingOrder : sr.sortingOrder;
                sb.AppendLine($"{frame},sprite,{sr.sprite.name},{sr.gameObject.name},{sr.sortingLayerName},{order},{F(sr.color.a)},{F(sp.x)},{F(sp.y)}");
            }
            foreach (var img in FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                var renderedSprite = img.overrideSprite;
                if (renderedSprite == null || !img.enabled || !img.gameObject.activeInHierarchy || img.color.a <= 0.001f) continue;
                var cg = img.GetComponentInParent<CanvasGroup>();
                if (cg != null && cg.alpha <= 0.001f) continue;
                var canvas = img.canvas;
                Vector3 p = img.rectTransform.position;
                sb.AppendLine($"{frame},ui,{renderedSprite.name},{img.gameObject.name},canvas,{(canvas != null ? canvas.sortingOrder : 0)},{F(img.color.a * (cg != null ? cg.alpha : 1f))},{F(p.x)},{F(p.y)}");
            }
        }

        // ── 진행 ─────────────────────────────────────────────

        IEnumerator Run()
        {
            Line($"autotour start mode={mode}, version {Application.version}, data={LaunchOptions.DataDir}, " +
                 $"screen {Screen.width}x{Screen.height} {Screen.fullScreenMode}, gfx={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                 $"cpu={SystemInfo.processorType} x{SystemInfo.processorCount}, ram={SystemInfo.systemMemorySize}MB, os={SystemInfo.operatingSystem}");
            yield return Wait(3f);
            if (mode == "audio") yield return AudioRun();
            else
            {
                if (mode == "screens" || mode == "all") yield return ScreensRun();
                if (mode == "motion" || mode == "all") yield return MotionRun();
                if (mode == "bestiary") yield return BestiaryRun();
                if (mode == "additional" || mode == "deaths") yield return AdditionalRun(mode == "deaths");
                if (mode == "sfx") yield return SfxRun();
                if (mode == "ice") yield return IceRun();
                if (mode == "save") yield return SaveRecoveryRun();
                if (mode == "remaining") yield return RemainingRun();
                if (mode == "focus") yield return FocusRun();
                if (mode == "vfx") yield return VfxRun();
                if (mode == "review-fixes") yield return ReviewFixesRun();
            }
            var used = new List<string>(ArtLibrary.UsedIds);
            used.Sort();
            Line($"art used ({used.Count}): {string.Join(",", used)}");
            var save = PermanentProgress.Current;
            Line($"save: path={PermanentProgress.ActiveSavePath} exists={File.Exists(PermanentProgress.ActiveSavePath)} " +
                 $"totalRuns={save.totalRuns} cleared={save.clearedRuns} failed={save.failedRuns} abandoned={save.abandonedRuns} " +
                 $"activeRun={(save.activeRun == null ? "null" : save.activeRun.runId)} saveFailed={PermanentProgress.LastSaveFailed}");
            Line($"done shots={shots} recordedFrames={recordedFrames} errors={errors} exceptions={exceptions}");
            FlushLog();
            yield return Wait(0.5f);
            Application.Quit();
        }

        // ── 2026-10-12 초기 포커스 하이라이트 검수(focus 모드) ─────────────────────
        // OS 입력을 쓰지 않는다: Input System 장치에 상태 이벤트를 넣어 실제 InputSystemUIInputModule·UiInitialFocus 경로를 그대로 탄다
        // (사용자 마우스/키보드 포커스를 빼앗지 않음). backgroundBehavior=IgnoreFocus는 이 QA 모드에서만.

        static UnityEngine.InputSystem.Mouse QaMouse;
        static UnityEngine.InputSystem.Keyboard QaKeyboard;

        static Vector2 ScreenCenter(Component c)
        {
            var rt = (RectTransform)c.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f; // Screen Space Overlay: 월드 = 스크린 좌표
        }

        IEnumerator MouseTo(Vector2 pos, bool press = false)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(QaMouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = pos, buttons = (ushort)(press ? 1 : 0) });
            yield return null; yield return null;
        }

        IEnumerator Click(Component c)
        {
            var p = ScreenCenter(c);
            yield return MouseTo(p);
            yield return MouseTo(p, true);
            yield return MouseTo(p);
            yield return Wait(0.3f);
        }

        IEnumerator Key(UnityEngine.InputSystem.Key key)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(QaKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(QaKeyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null; yield return Wait(0.25f);
        }

        static string Sel()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "none";
        }

        static string Visual(Button b)
        {
            if (b == null) return "null";
            var img = b.targetGraphic as Image;
            string sprite = img == null ? "-" : img.overrideSprite != null ? img.overrideSprite.name : img.sprite != null ? img.sprite.name : "-";
            var ring = b.transform.Find("CafeFocus");
            bool ringOn = ring != null && ring.GetComponent<Image>().enabled;
            return $"{b.name}[sprite={sprite} ring={(ringOn ? "on" : "off")}]";
        }

        IEnumerator State(string shot, params Button[] buttons)
        {
            yield return Wait(0.35f);
            Line($"FOCUS {shot}: selected={Sel()} keyboardMode={UiInitialFocus.KeyboardMode} " + string.Join(" ", buttons.Select(Visual)));
            yield return Shot($"focus_{Screen.height}_{shot}");
        }

        IEnumerator FocusRun()
        {
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            QaMouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("QaMouse");
            QaKeyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("QaKeyboard");
            QaMouse.MakeCurrent(); QaKeyboard.MakeCurrent();
            var away = new Vector2(30f, 30f);
            yield return MouseTo(away);

            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("no title"); yield break; }
            Button TB(string n) => title.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == n);
            var start = TB("StartButton"); var shop = TB("PermanentShopButton");

            yield return State("01_title_entry", start, shop);
            if (Sel() != "none") Fail("title entry has selection");
            yield return MouseTo(ScreenCenter(start));
            yield return State("02_title_hover_start", start, shop);
            yield return MouseTo(away);
            yield return State("03_title_mouse_left", start, shop);
            yield return Key(UnityEngine.InputSystem.Key.DownArrow);
            yield return State("04_title_key1_reveals_start", start, shop);
            if (Sel() != "StartButton") Fail("first key should reveal StartButton");
            yield return Key(UnityEngine.InputSystem.Key.DownArrow);
            yield return State("05_title_key2_moves", start, shop);

            // 재진입: 마우스로 영구 강화 → 뒤로 → 타이틀(포인터 모드면 고정 강조 없음)
            yield return Click(shop);
            yield return State("06_shop_open_by_mouse");
            var shopBack = title.ShopView.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "BackButton");
            yield return Click(shopBack);
            yield return State("07_title_reentry_mouse", start, shop);
            if (Sel() != "none") Fail("title re-entry (mouse) has selection");

            yield return Click(start);
            CharacterSelectView select = null;
            for (int i = 0; i < 50 && select == null; i++) { select = FindFirstObjectByType<CharacterSelectView>(); yield return Wait(0.1f); }
            if (select == null) { Fail("no character select"); yield break; }
            yield return MouseTo(away);
            yield return State("08_job_entry_mouse", select.BartenderButton, select.TeaSommelierButton);
            if (Sel() != "none") Fail("job entry has selection");
            yield return MouseTo(ScreenCenter(select.BartenderButton));
            yield return State("09_job_hover_bartender", select.BartenderButton, select.TeaSommelierButton);
            yield return MouseTo(away);
            yield return State("10_job_mouse_left", select.BartenderButton, select.TeaSommelierButton);
            yield return Key(UnityEngine.InputSystem.Key.RightArrow);
            yield return State("11_job_key1_reveals_default", select.BartenderButton, select.TeaSommelierButton);
            yield return Key(UnityEngine.InputSystem.Key.LeftArrow);
            yield return State("12_job_key2_bartender", select.BartenderButton, select.TeaSommelierButton);
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            yield return State("13_weapon_entry_keyboard", select.WeaponButtonA, select.WeaponButtonB, select.StartButton);
            yield return Click(select.WeaponBackButton);
            yield return MouseTo(away);
            yield return State("14_job_reentry_mouse", select.BartenderButton, select.TeaSommelierButton);
            if (Sel() != "none") Fail("job re-entry (mouse) has selection");
            yield return Click(select.BartenderButton);
            yield return MouseTo(away);
            yield return State("15_weapon_entry_mouse", select.WeaponButtonA, select.WeaponButtonB, select.StartButton);
            if (Sel() != "none") Fail("weapon entry (mouse) has selection");
            Line($"FOCUS weapon logical default={select.SelectedWeapon} shownA={select.WeaponChoiceShownForTests(true)} shownB={select.WeaponChoiceShownForTests(false)}");
            yield return MouseTo(ScreenCenter(select.WeaponButtonB));
            yield return State("16_weapon_hover_b", select.WeaponButtonA, select.WeaponButtonB, select.StartButton);
            yield return Click(select.WeaponButtonB);
            yield return MouseTo(away);
            yield return State("17_weapon_b_chosen_mouse_left", select.WeaponButtonA, select.WeaponButtonB, select.StartButton);
            Line($"FOCUS weapon chosen={select.SelectedWeapon} shownA={select.WeaponChoiceShownForTests(true)} shownB={select.WeaponChoiceShownForTests(false)}");
            yield return Key(UnityEngine.InputSystem.Key.Tab);
            yield return State("18_weapon_key_reveals", select.WeaponButtonA, select.WeaponButtonB, select.StartButton);
        }

        IEnumerator ReviewFixesRun()
        {
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            QaKeyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("ReviewKeyboard");
            QaKeyboard.MakeCurrent();
            yield return Shot("review_title");
            FindFirstObjectByType<TitleView>().RequestStart();
            yield return StartCombo(Combos[0],true);
            var game=GameManager.Instance; FreezeWorld(game); yield return Wait(.3f);
            foreach(var card in CardDatabase.LoadAll().Where(c=>c.category==CardCategory.Cocktail).Take(3)) game.GrantCardForTests(card);
            yield return Shot("review_bartender_trait");
            game.OpenCoffeeTree(); yield return Wait(.2f); yield return Shot("review_coffee_open");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            Line($"COFFEE ESC actual input: state={game.State} paused={game.IsPaused} tree={game.SkillView.IsOpen} scale={Time.timeScale}");
            if(game.SkillView.IsOpen || game.IsPaused || game.State!=GameState.Playing || Time.timeScale!=1f) Fail("coffee Escape did not resume");
            yield return Shot("review_coffee_escaped");
            game.Player.Hidden.ForceForTests(HiddenSkill.IcedCup); game.Player.HiddenRuntime.SetBarrierReadyForTests();
            game.Player.Hidden.ForceForTests(HiddenSkill.ShakeRhythm);
            yield return Key(UnityEngine.InputSystem.Key.H); yield return Shot("review_hidden_details");
            if(!game.Hud.HiddenDetailsOpen) Fail("H did not show hidden effects");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            if(game.Hud.HiddenDetailsOpen || game.IsPaused) Fail("hidden Escape did not resume");
            game.Player.TakeDamage(1f); yield return Wait(.1f); yield return Shot("review_hidden_block_activation");
            yield return Wait(.9f);
            game.Player.Hidden.ForceForTests(HiddenSkill.HotToddy);
            game.Player.TakeDamage(10f);
            game.Player.HiddenRuntime.OnEnemyKilled(EnemyDef.Brute);
            yield return Wait(.2f); yield return Shot("review_hidden_long_text");
            foreach(var def in HiddenSkillDatabase.All) game.Player.Hidden.ForceForTests(def.skill);
            yield return Key(UnityEngine.InputSystem.Key.H); yield return Wait(.2f);
            var list=game.Hud.GetComponentsInChildren<ScrollRect>().First(s=>s.gameObject.name=="Scroll");
            var events=UnityEngine.EventSystems.EventSystem.current;
            var pointer=new UnityEngine.EventSystems.PointerEventData(events);
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,list.viewport.TransformPoint(list.viewport.rect.center));
            pointer.pressPosition=pointer.position;
            var hits=new List<UnityEngine.EventSystems.RaycastResult>(); events.RaycastAll(pointer,hits);
            var dragTarget=hits.Count>0 ? UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(hits[0].gameObject) : null;
            if(dragTarget!=list.gameObject) Fail("hidden list viewport did not receive drag raycast");
            else
            {
                UnityEngine.EventSystems.ExecuteEvents.Execute(dragTarget,pointer,UnityEngine.EventSystems.ExecuteEvents.initializePotentialDrag);
                UnityEngine.EventSystems.ExecuteEvents.Execute(dragTarget,pointer,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
                pointer.position+=Vector2.up*180f;
                UnityEngine.EventSystems.ExecuteEvents.Execute(dragTarget,pointer,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(dragTarget,pointer,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
                if(list.content.anchoredPosition.y<=0f) Fail("hidden list drag did not move content");
                Line($"Hidden list injected pointer drag: y={list.content.anchoredPosition.y:0.0}");
                yield return Shot("review_hidden_drag");
                list.velocity=Vector2.zero; list.verticalNormalizedPosition=1f; yield return null;
                pointer.scrollDelta=Vector2.down*3f;
                UnityEngine.EventSystems.ExecuteEvents.Execute(dragTarget,pointer,UnityEngine.EventSystems.ExecuteEvents.scrollHandler);
                if(list.content.anchoredPosition.y<=0f) Fail("hidden list wheel did not move content");
                Line($"Hidden list injected wheel: y={list.content.anchoredPosition.y:0.0}");
                yield return Shot("review_hidden_wheel");
            }
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            game.GiveUp(); game.GoToTitle(); yield return Wait(.8f);
            FindFirstObjectByType<TitleView>().RequestStart(); yield return StartCombo(Combos[3],true);
            game=GameManager.Instance; FreezeWorld(game); yield return Wait(.3f);
            var tea=CardDatabase.LoadAll().First(c=>c.category==CardCategory.Tea && c.maxStacks>=3);
            for(int i=0;i<3;i++) game.GrantCardForTests(tea);
            game.Player.AutoInputOverride=Vector2.zero;
            game.Player.SetFacing(game.Player.Position.x+10f); yield return Wait(.25f); yield return Shot("review_bow_idle_R");
            game.Player.AutoInputOverride=Vector2.right; yield return Wait(.4f); yield return Shot("review_bow_move_R");
            game.Player.AutoInputOverride=Vector2.zero; game.Player.Visual.PlayAction("ranged"); yield return Wait(.06f); yield return Shot("review_bow_attack_R");
            yield return Wait(.4f); game.Player.SetFacing(game.Player.Position.x-10f); yield return Wait(.1f); yield return Shot("review_bow_idle_L");
            game.Player.AutoInputOverride=Vector2.left; yield return Wait(.4f); yield return Shot("review_bow_move_L");
            game.Player.AutoInputOverride=Vector2.zero; game.Player.Visual.PlayAction("ranged"); yield return Wait(.06f); yield return Shot("review_bow_attack_L");
            Line($"QA forced class/hidden setup; tea level={game.DeepSteepLevel}. Not a normal playthrough.");
            UnityEngine.InputSystem.InputSystem.RemoveDevice(QaKeyboard); QaKeyboard=null;
        }

        // ── 2026-10-12 42/43/41 실제 화면 검수(vfx 모드) ─────────────────────
        // 강제 경로(보고 표기): 정지 표적(속도0·접촉0), 무기 제거, 화염/독 레벨 직접 부여, 장판·예고 직접 생성(규칙 수치는 실제 패턴 값), 카드 직접 지급.

        sealed class QaStill : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        Enemy Still(GameManager game, Vector2 offset, EnemyDef def)
        {
            var e = Enemy.SpawnAt(game.Player.Position + offset, def, 50f, game.Player, game.transform);
            e.SetDriver(new QaStill());
            e.GetComponent<Rigidbody2D>().simulated = false;
            return e;
        }

        IEnumerator VfxRun()
        {
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("no title"); yield break; }
            title.RequestStart();
            CharacterSelectView select = null;
            for (int i = 0; i < 50 && select == null; i++) { select = FindFirstObjectByType<CharacterSelectView>(); yield return Wait(0.1f); }
            select.BartenderButton.onClick.Invoke();
            yield return Wait(0.3f);
            select.StartButton.onClick.Invoke();
            yield return Wait(1f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen) tutorial.Dismiss();
            var game = GameManager.Instance;
            if (game == null) { Fail("run did not start"); yield break; }
            yield return Wait(0.5f);
            FreezeWorld(game);
            foreach (var mb in game.Player.GetComponents<MonoBehaviour>())
                if (mb is RapierWeapon || mb is PlayerProjectileWeapon) Destroy(mb);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(1f);
            game.Player.AutoInputOverride = Vector2.zero;
            yield return Wait(0.3f);

            // 42 상태 표식: 둘 다 / 화상만 / 중독만
            var both = Still(game, new Vector2(2.4f, 0.3f), EnemyDef.Brute);
            var burnOnly = Still(game, new Vector2(4.4f, -0.4f), EnemyDef.Imp);
            var poisonOnly = Still(game, new Vector2(-3f, 0.2f), EnemyDef.TeabagImp);
            var stats = game.Stats;
            stats.flameLevel = 1; stats.poisonLevel = 1; EnemyHitStatus.AttachDirect(both, game.Player);
            stats.poisonLevel = 0; EnemyHitStatus.AttachDirect(burnOnly, game.Player);
            stats.flameLevel = 0; stats.poisonLevel = 1; EnemyHitStatus.AttachDirect(poisonOnly, game.Player);
            stats.flameLevel = 1;
            for (int k = 0; k < 3; k++)
            {
                yield return Wait(0.12f);
                var s = both.GetComponent<EnemyHitStatus>();
                Line($"VFX status frame{k}: burn={s.FlameMarkerSprite} poison={s.PoisonMarkerSprite}");
                yield return Shot($"vfx_{Screen.height}_status_{k}", game);
            }
            stats.LockFlame(3f);
            yield return Wait(0.3f);
            Line($"VFX sealed: burnSprite={both.GetComponent<EnemyHitStatus>().FlameMarkerSprite}");
            yield return Shot($"vfx_{Screen.height}_status_flame_sealed", game);
            foreach (var e in new[] { both, burnOnly, poisonOnly }) if (e != null) Destroy(e.gameObject);
            yield return Wait(0.2f);

            // 43 예고 → 활성 → 소멸 (드립 웨이브 실제 값: 반경 1.3, 예고 0.6초, 지속 2초)
            Vector2 at = game.Player.Position + new Vector2(3.2f, 0f);
            var tel = ZoneTelegraphV2.Spawn(at, 1.3f, 0.6f, new Color(1f, .62f, .3f), game.transform);
            if (tel == null) Fail("telegraph v2 missing");
            yield return Wait(0.3f);
            yield return Shot($"vfx_{Screen.height}_zone_telegraph_half", game);
            yield return Wait(0.22f);
            yield return Shot($"vfx_{Screen.height}_zone_telegraph_end", game);
            yield return Wait(0.1f);
            BurstZone.Spawn(at, 1.3f, 9f, 2f, game.Player, game.transform, BurstZone.CoffeePoolArt);
            yield return Wait(0.6f);
            yield return Shot($"vfx_{Screen.height}_zone_coffee_active", game);
            yield return Wait(1.15f);
            yield return Shot($"vfx_{Screen.height}_zone_coffee_fading", game);
            yield return Wait(0.5f);

            // 세 장판 나란히(같은 반경 1.2) + 판정 경계 기록
            var p = game.Player.Position;
            BurstZone.Spawn(p + new Vector2(-4f, 0f), 1.2f, 5f, 6f, game.Player, game.transform, BurstZone.OliveFireArt);
            BurstZone.Spawn(p + new Vector2(0f, 0f), 1.2f, 5f, 6f, game.Player, game.transform, BurstZone.CoffeePoolArt);
            SlowZone.Spawn(p + new Vector2(4f, 0f), 1.2f, 0.6f, 6f, game.Player, game.transform);
            foreach (var id in new[] { "zone_olive_fire_v2", "zone_coffee_pool_v2", "zone_sugar_pool_v2", ZoneTelegraphV2.RingId })
            {
                var h = ZoneVisualV2.VisualHalfExtents(id, 1.2f);
                Line($"VFX boundary {id}: halfExtents=({F(h.x)},{F(h.y)}) hitRadius=1.2");
            }
            yield return Wait(0.6f);
            yield return Shot($"vfx_{Screen.height}_zones_three", game);

            // 41 히든 해금 배너(실제 카드 선택 경로)
            var cards = CardDatabase.LoadAll();
            game.GrantCardForTests(cards.First(c => c.id == "molotov"));
            game.GrantCardForTests(cards.First(c => c.id == "chamomile"));
            Line($"VFX hidden unlocked: {game.Player.Hidden.Count} hot_toddy={game.Player.Hidden.Has(HiddenSkill.HotToddy)}");
            yield return Wait(0.3f);
            yield return Shot($"vfx_{Screen.height}_hidden_banner", game);

            // 41 도감 히든 섹션(발견 1 + 미발견 23: ??? + 등급 + 힌트, 티니 보우 전체 조건)
            Destroy(game.gameObject);
            yield return Wait(0.3f);
            var t2 = TitleView.Show();
            yield return Wait(0.3f);
            var codexButton = t2.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "CardCodexButton");
            if (codexButton != null) codexButton.onClick.Invoke(); else Fail("codex button not found"); // 실제 버튼 경로(타이틀 숨김 + 도감 열기)
            yield return Wait(0.3f);
            var scroll = t2.CodexView.GetComponentInChildren<ScrollRect>();
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0f; }
            yield return Wait(0.3f);
            Line($"VFX codex count: {t2.CodexView.CountText}");
            yield return Shot($"vfx_{Screen.height}_codex_hidden_bottom");
            if (scroll != null) { scroll.verticalNormalizedPosition = 0.35f; }
            yield return Wait(0.3f);
            yield return Shot($"vfx_{Screen.height}_codex_hidden_mid");
        }

        /// <summary>장착 무기 보이는 길이(월드)와 몸 높이 비율 — 총 2배 지시 검증용.</summary>
        static string WeaponSize(PlayerController player)
        {
            var v = player.Visual;
            var w = ArtLibrary.Get(v.WeaponArtId);
            if (w == null || v.Weapon == null || v.BodyArt == null) return "weaponLength=n/a";
            var c = w.ContentWorldSize;
            float len = Mathf.Max(c.x, c.y) * Mathf.Abs(v.Weapon.transform.lossyScale.y);
            return $"weaponTarget={w.targetVisualLengthRelativeToPlayer:0.###} weaponLengthWorld={len:0.###} bodyHeightWorld={v.BodyArt.targetBodyHeightWorld:0.###} ratio={len / v.BodyArt.targetBodyHeightWorld:0.###}";
        }

        static void SetPlayerWeapons(PlayerController player, bool on)
        {
            if (player == null) return;
            foreach (var mb in player.GetComponents<MonoBehaviour>())
                if (mb is RapierWeapon || mb is PlayerProjectileWeapon || mb is HybridBowWeapon)
                    mb.enabled = on;
        }

        static Button FindButton(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                if (b.name == name) return b;
            return null;
        }

        /// <summary>Selects a shop card through the EventSystem (the keyboard path) and captures its detail.</summary>
        IEnumerator SelectAndShoot(PermanentSkillView shop, string id, string shot)
        {
            var card = shop.transform.Find("Node_" + id);
            if (card == null) { Fail("shop card missing " + id); yield break; }
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(card.gameObject);
            yield return Wait(.2f);
            if (shop.DetailNode == null || shop.DetailNode.id != id) Fail("shop detail did not follow selection " + id);
            yield return Shot(shot);
        }

        static bool Click(Transform root, string name)
        {
            var b = FindButton(root, name);
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        /// <summary>선택 화면에서 실제 버튼으로 런을 시작하고 첫 안내가 있으면 촬영 후 닫는다.</summary>
        IEnumerator StartCombo((Job job, bool weaponA, string label) combo, bool shootSelect)
        {
            CharacterSelectView select = null;
            for (int k = 0; k < 20 && select == null; k++)
            {
                yield return Wait(0.3f);
                select = FindFirstObjectByType<CharacterSelectView>();
            }
            if (select == null) { Fail("no CharacterSelectView"); yield break; }
            yield return Wait(0.5f);
            if (shootSelect) yield return Shot("10_job_select");
            (combo.job == Job.Bartender ? select.BartenderButton : select.TeaSommelierButton).onClick.Invoke();
            yield return Wait(0.5f);
            yield return Shot(shootSelect ? "11_weapon_select" : "11_weapon_select_" + combo.label); // 2026-10-09: 모든 직업 무기 선택 화면
            (combo.weaponA ? select.WeaponButtonA : select.WeaponButtonB).onClick.Invoke();
            select.StartButton.onClick.Invoke(); // 08:67
            yield return Wait(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen)
            {
                if (shootSelect) yield return Shot("12_first_run_tutorial");
                tutorial.Dismiss();
                yield return Wait(0.6f);
            }
            if (GameManager.Instance == null) Fail($"run did not start ({combo.label})");
        }

        IEnumerator ScreensRun()
        {
            var title = FindFirstObjectByType<TitleView>();
            Line($"title open={title != null && title.IsOpen}");
            if (title == null || !title.IsOpen) Fail("title not open");
            yield return Shot("01_title");

            if (title != null)
            {
                var skinButton = title.GetComponentsInChildren<Button>().FirstOrDefault(b =>
                    b.targetGraphic is Image image && UiFactory.IsCafe(image, "ui_cafe_button"));
                if (skinButton != null) yield return CaptureSelectableStates(skinButton, "ui_button");
                else Fail("no actual cafe title button for state capture");
            }

            if (title != null)
            {
                // 실제 타이틀 버튼(onClick)으로 열고 각 화면의 뒤로/닫기 버튼으로 돌아온다.
                foreach (var (button, shot) in new[]
                         {
                             ("CardCodexButton", "02_codex"),
                             ("SettingsButton", "03_settings"),
                             ("PermanentShopButton", "04_permanent_shop"),
                             ("CreditsButton", "05_credits"),
                         })
                {
                    if (!Click(title.transform, button)) { Fail($"title button {button} missing"); continue; }
                    yield return Wait(0.6f);
                    yield return Shot(shot);
                    Component view = shot switch
                    {
                        "02_codex" => title.CodexView,
                        "03_settings" => title.SettingsView,
                        "04_permanent_shop" => title.ShopView,
                        _ => title.CreditsView,
                    };
                    if (view == null || !view.gameObject.activeInHierarchy) Fail($"{shot} view not visible after button");
                    if (shot == "02_codex" && title.CodexView.OpenDetailFor("dry_gin"))
                    {
                        yield return Wait(0.3f);
                        yield return Shot("02b_codex_detail");
                        title.CodexView.HideDetail();
                        var detailIds = PermanentSkillDatabase.LoadAll().Select(n => n.id)
                            .Concat(SkillDatabase.LoadAll().Select(n => n.id))
                            .Concat(new[] { "rifled_barrel", "champagne_spray", "flambe", "molotov", "nettle_tea" });
                        foreach (var id in detailIds)
                        {
                            if (!title.CodexView.OpenDetailFor(id)) { Fail("codex detail missing " + id); continue; }
                            yield return Wait(.1f); yield return Shot("02_detail_" + id);
                            title.CodexView.HideDetail();
                        }
                    }
                    if (shot == "04_permanent_shop")
                    {
                        foreach (PermanentSkillGroup group in Enum.GetValues(typeof(PermanentSkillGroup)))
                        {
                            title.ShopView.SelectGroup(group);
                            yield return Wait(.3f);
                            yield return Shot("04_shop_" + group);
                        }
                        var tab = title.ShopView.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Tab_Master");
                        if (tab != null) yield return CaptureSelectableStates(tab, "ui_tab");
                        // Isolated QA save only: exercise actual purchase UI at sufficient funds and max progress.
                        // 38: locked card detail is reachable by keyboard selection (same Select trigger as arrow keys).
                        title.ShopView.SelectGroup(PermanentSkillGroup.Attack);
                        yield return SelectAndShoot(title.ShopView, "perfect_crema", "04_detail_locked_prerequisite");
                        title.ShopView.SelectGroup(PermanentSkillGroup.Weapon);
                        yield return SelectAndShoot(title.ShopView, "gun_double_measure", "04_detail_locked_weapon_gun");
                        title.ShopView.SelectGroup(PermanentSkillGroup.Master);
                        yield return SelectAndShoot(title.ShopView, "master_blend", "04_detail_locked_master_three");
                        Line("[forced QA save] specialty129/clears0 then clears5, real purchase API; never normal-play evidence");
                        PermanentProgress.Current.specialtyPoints = 129;
                        PermanentProgress.Current.clearedRuns = 0;
                        title.ShopView.Show();
                        yield return Wait(.3f); yield return Shot("04_shop_purchase_available");
                        foreach (var id in new[] { "dark_roast_blend", "perfect_crema", "precision_tools" })
                            title.ShopView.TryBuy(title.ShopView.Nodes.First(n => n.id == id));
                        title.ShopView.SelectGroup(PermanentSkillGroup.Attack);
                        yield return SelectAndShoot(title.ShopView, "roast_profile", "04_shop_clear_goal_locked");
                        yield return SelectAndShoot(title.ShopView, "dark_roast_blend", "04_detail_owned");
                        title.ShopView.SelectGroup(PermanentSkillGroup.Weapon);
                        yield return SelectAndShoot(title.ShopView, "rapier_precision", "04_detail_buyable_weapon_prediction");
                        PermanentProgress.Current.clearedRuns = 5;
                        title.ShopView.Show();
                        for (int pass = 0; pass < 8; pass++)
                            foreach (var node in title.ShopView.Nodes) title.ShopView.TryBuy(node);
                        foreach (PermanentSkillGroup group in Enum.GetValues(typeof(PermanentSkillGroup)))
                        {
                            title.ShopView.SelectGroup(group);
                            yield return Wait(.3f); yield return Shot("04_shop_max_" + group);
                        }
                    }
                    if (view != null && !Click(view.transform, "BackButton") && !Click(view.transform, "CloseButton"))
                        Fail($"{shot} has no Back/Close button");
                    yield return Wait(0.4f);
                    if (!title.IsOpen) Fail($"title not restored after {shot}");
                }
                title.RequestStart();
            }

            EnemyDef[] bosses = { EnemyDef.GrinderGolem, EnemyDef.DripServerSpecter, EnemyDef.EspressoLeviathan, EnemyDef.DifficultCustomer };
            int[] bossFrames = { 16 * CaptureFps, 20 * CaptureFps, 24 * CaptureFps, 32 * CaptureFps };
            for (int i = 0; i < Combos.Length; i++)
            {
                var combo = Combos[i];
                yield return Wait(0.6f);
                yield return StartCombo(combo, i == 0);
                var game = GameManager.Instance;
                if (game == null) break;
                var player = game.Player;
                Line($"run {combo.label}: job={game.Job} weapon={game.Weapon} bodyArt={player.Visual.UsesBodyArt} " +
                     $"weaponArt={player.Visual.WeaponArtId ?? "icon"} provisionalHand={player.Visual.WeaponUsesProvisionalAnchor} {WeaponSize(player)}");

                player.AutoInputOverride = Vector2.right;
                yield return Wait(0.9f);
                yield return Shot($"20_walk_{combo.label}", game);
                player.AutoInputOverride = new Vector2(-1f, 0.4f);
                yield return Wait(0.9f);
                player.AutoInputOverride = Vector2.zero;
                yield return Wait(5f);
                float hpBefore = player.Hp;
                yield return Shot($"21_combat_{combo.label}", game);
                Line($"combat kills={game.Kills} hp={player.Hp:0.0}/{player.MaxHp:0.0} runTime={game.RunTime:0.0}");

                if (i == 0)
                {
                    // 일반/엘리트 13종 라인업(실제 Enemy.SpawnAt — 아트/그림자/정렬 확인용)
                    EnemyDef[] roster =
                    {
                        EnemyDef.Imp, EnemyDef.Brute, EnemyDef.ShakerGolem, EnemyDef.OliveBomber, EnemyDef.SwirlConjuror,
                        EnemyDef.MartiniSpirit, EnemyDef.TeabagImp, EnemyDef.KettleCannon, EnemyDef.SugarCubeBomber,
                        EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian, EnemyDef.EspressoBug, EnemyDef.MilkFoamer,
                    };
                    yield return ClearOverlays(game, "lineup");
                    for (int k = 0; k < roster.Length; k++)
                    {
                        var pos = new Vector2(player.Position.x - 6.5f + (k % 7) * 2.1f, Balance.LaneCenterY + (k < 7 ? 1.6f : -1.6f));
                        var e = Enemy.SpawnAt(pos, roster[k], 1f, player, game.transform);
                        EnemyRoster.AttachBehavior(e, game, player);
                    }
                    yield return Wait(0.5f);
                    yield return Shot("22_enemy_lineup", game);
                }

                // 카드 보상(실제 게이지 경로)
                yield return ClearOverlays(game, "card");
                game.AddIngredient(IngredientType.Cocktail, Mathf.Max(1, game.CocktailGauge.Threshold - game.CocktailGauge.Current));
                yield return Wait(0.6f);
                if (game.CardView.IsOpen)
                {
                    yield return Shot("30_card_select_" + combo.label);
                    game.CardView.Choose(0);
                    Line("card chosen");
                }
                else Fail("card view did not open after cocktail gauge threshold");
                yield return Wait(0.5f);

                if (i == 0)
                {
                    // [강제] 하이브리드 예약(RunRewards.RequestHybrid) → 실제 해금 창 → 해금 버튼 경로
                    yield return ClearOverlays(game, "hybrid");
                    game.Rewards.RequestHybrid();
                    yield return Wait(0.5f);
                    if (game.HybridView.IsOpen)
                    {
                        yield return Shot("32_hybrid_unlock");
                        game.HybridView.Unlock();
                        Line($"hybrid unlocked={game.HybridUnlocked} secondaryProvisional={player.Visual.SecondaryUsesProvisionalAnchor}");
                    }
                    else Fail("hybrid unlock view did not open");
                    // [강제] 화염/독 레벨 부여 → 주기 스킬 VFX. E1: 메뉴 없는 상태로 정지 화면 + 4초 연속 녹화.
                    game.Stats.flameLevel = Mathf.Max(1, game.Stats.flameLevel);
                    game.Stats.poisonLevel = Mathf.Max(1, game.Stats.poisonLevel);
                    yield return Wait(3.2f);
                    yield return Shot("33_hybrid_fire_poison", game);
                    yield return ClearOverlays(game, "fire_poison video");
                    yield return Record("33_fire_poison", 5 * CaptureFps, "kills,hp,flame,poison,enemies",
                        f => { if (f % 15 == 0 && player != null && !player.IsDead) player.Heal(1f); },
                        () => $"{game.Kills},{F(player.Hp)},{game.Stats.flameLevel},{game.Stats.poisonLevel},{Enemy.Active.Count}",
                        f => f == 75 ? "33b_fire_poison_video_frame" : null, game);
                }

                if (i == 1)
                {
                    yield return ClearOverlays(game, "coffee");
                    game.AddIngredient(IngredientType.Coffee, 10);
                    yield return Wait(0.6f);
                    if (game.SkillView.IsOpen)
                    {
                        yield return Shot("31_coffee_tree");
                        game.AddIngredient(IngredientType.Coffee, 140);
                        for (int pass = 0; pass < 6; pass++)
                            foreach (var node in SkillDatabase.LoadAll())
                                game.SkillView.Choose(node);
                        if (!game.SkillView.IsOpen) game.OpenCoffeeTree();
                        yield return Wait(.3f); yield return Shot("31_coffee_tree_after");
                        game.SkillView.Continue();
                    }
                    else Fail("coffee tree did not open after 10 beans");
                    yield return Wait(0.5f);
                }

                // 보스(4종 각각 한 번). E2: 메뉴를 닫고 일반 스폰을 멈춘 뒤 등장→예고→실행→복귀를 연속 녹화.
                if (i == 1)
                {
                    yield return ClearOverlays(game, "final forms"); FreezeWorld(game);
                    foreach (var id in new[] { "rifled_barrel", "piercing_arrowhead", "champagne_spray", "tea_leaf_scatter", "molotov", "blazer", "nettle_tea", "nightshade_blend" })
                    {
                        var card = CardDatabase.LoadAll().First(c => c.id == id);
                        for (int n = 0; n < card.maxStacks; n++) game.Cards.Take(card, game.Stats);
                        player.FinalForms.OnCardTaken(card, game.Cards.GetStacks(card));
                    }
                    for (int n = 0; n < 4; n++)
                        Enemy.SpawnAt(player.Position + new Vector2(2.5f + n * .7f, (n - 1.5f) * .3f), DummyDef(), 1f, player, game.transform);
                    yield return Record("34_final_direct_hit", 6 * CaptureFps, "playerShots,statusEnemies,flameMarkers,poisonMarkers,pierce,spread,flame,poison",
                        null, () => $"{PlayerProjectile.ActiveCount},{FindObjectsByType<EnemyHitStatus>(FindObjectsSortMode.None).Length}," +
                            $"{FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(s => s.name == "FlameHitMarker" && s.enabled)}," +
                            $"{FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(s => s.name == "PoisonHitMarker" && s.enabled)}," +
                            $"{player.FinalForms.Has(AttackFinalForm.Pierce)},{player.FinalForms.Has(AttackFinalForm.Spread)},{player.FinalForms.Has(AttackFinalForm.Flame)},{player.FinalForms.Has(AttackFinalForm.Poison)}",
                        f => f == 90 ? "34_final_direct_hit" : null, game);
                }
                yield return ClearOverlays(game, "boss");
                FreezeWorld(game);
                player.AutoInputOverride = Vector2.zero;
                // 실제 보스 등장 경로(BossDirector.SpawnNow: 화면 가장자리 입장·패턴 드라이버·배너·보스 BGM). 일정 시간만 건너뛴다.
                var boss = game.Director.SpawnNow(new BossDirector.Spec { def = bosses[i], hpScale = 1f });
                var seenClips = new HashSet<string>();
                string bossId = bosses[i].ArtId;
                // [강제] 관찰 동안 플레이어 무기를 멈춘다(피격 섬광·숫자가 예고를 가리지 않게). HP 문턱 패턴은 중간에 직접 피해로 넘긴다.
                SetPlayerWeapons(player, false);
                float hpTarget = bosses[i] == EnemyDef.EspressoLeviathan ? 0.29f : bosses[i] == EnemyDef.DifficultCustomer ? 0.48f : 0f;
                int pendingExec = -1;
                string pendingName = null;
                yield return Record($"40_boss_{bossId}", bossFrames[i],
                    "bossClip,bossFrame,bossX,bossY,bossHp,bossInvuln,bossAlpha,playerX,playerY,playerHp,enemyShots,burstZones,slowZones,enemies,bossFacing,bossSprite",
                    f =>
                    {
                        // [강제] 패턴 관찰 동안 0.5초마다 회복(사망으로 끊기지 않게) — 밸런스/난도 검증 아님
                        if (f % 15 == 0 && player != null && !player.IsDead) player.Heal(1f);
                        if (hpTarget > 0f && f == bossFrames[i] / 2 && boss != null && !boss.Invulnerable)
                        {
                            boss.TakeDamage(Mathf.Max(0f, boss.Hp - boss.MaxHp * hpTarget));
                            Line($"[강제] boss {bossId} hp -> {boss.Hp:0}/{boss.MaxHp:0} (threshold pattern check)");
                        }
                    },
                    () =>
                    {
                        if (boss == null) return ",,,,,,,,,,,,,,,";
                        var sr = boss.GetComponentInChildren<SpriteRenderer>();
                        return $"{boss.Animator?.Current?.name},{boss.Animator?.FrameIndex},{F(boss.transform.position.x)},{F(boss.transform.position.y)}," +
                               $"{F(boss.Hp)},{(boss.Invulnerable ? 1 : 0)},{F(sr != null ? sr.color.a : 1f)},{F(player.Position.x)},{F(player.Position.y)},{F(player.Hp)}," +
                               $"{EnemyProjectile.ActiveCount},{BurstZone.ActiveCount},{SlowZone.ActiveCount},{Enemy.Active.Count}," +
                               $"{(boss.Animator != null && boss.Animator.transform.localScale.x < 0 ? -1 : 1)},{sr?.sprite?.name}";
                    },
                    f =>
                    {
                        if (f == 15) return $"40_boss_arrival_{bossId}";
                        if (boss == null) return null;
                        if (f == pendingExec) { pendingExec = -1; return pendingName; }
                        var clip = boss.Animator?.Current?.name;
                        if (clip != null && clip != "idle" && clip != "move" && clip != "walk" && clip != "hurt" && seenClips.Add(clip))
                        {
                            pendingExec = f + 15; // 예고 시작 0.5초 뒤(실행/판정 표시) 한 장 더
                            pendingName = $"42_boss_pattern_{bossId}_{clip}_b";
                            return $"42_boss_pattern_{bossId}_{clip}_a";
                        }
                        return f == bossFrames[i] / 2 ? $"41_boss_{bossId}" : null;
                    }, game);
                SetPlayerWeapons(player, true);
                Line($"boss {bossId}: clips seen=[{string.Join(",", seenClips)}] hp={(boss != null ? boss.Hp : 0f):0}/{(boss != null ? boss.MaxHp : 0f):0} player hp {hpBefore:0.0}->{player.Hp:0.0}");

                if (i == 0)
                {
                    yield return ClearOverlays(game, "pause");
                    game.OpenPause();
                    yield return Wait(0.5f);
                    yield return Shot("50_pause");
                    // D6 남은 경로: 일시정지 → 설정, 일시정지 → 안내 다시보기(키보드/마우스 방식)
                    if (Click(game.PauseView.transform, "SettingsButton"))
                    {
                        yield return Wait(0.5f);
                        yield return Shot("51_pause_settings");
                        game.PauseView.CloseSettings();
                        yield return Wait(0.4f);
                    }
                    else Fail("pause SettingsButton missing");
                    if (Click(game.PauseView.transform, "TutorialButton"))
                    {
                        yield return Wait(0.5f);
                        yield return Shot("52_pause_tutorial_keyboard");
                        game.PauseView.CloseTutorial();
                        yield return Wait(0.4f);
                    }
                    else Fail("pause TutorialButton missing");
                    SettingsService.SetMovementScheme(MovementScheme.Mouse);
                    if (Click(game.PauseView.transform, "TutorialButton"))
                    {
                        yield return Wait(0.5f);
                        yield return Shot("53_pause_tutorial_mouse");
                        game.PauseView.CloseTutorial();
                        yield return Wait(0.4f);
                    }
                    SettingsService.SetMovementScheme(MovementScheme.Keyboard);
                    if (!game.IsPaused) Fail("pause closed after sub-views");
                    game.ClosePause();
                    yield return Wait(1.3f);
                    Line($"after pause: state={game.State} paused={game.IsPaused} timeScale={Time.timeScale}");
                }

                if (GameManager.Instance == null || game.State == GameState.GameOver)
                {
                    Line("run already ended");
                }
                else if (i == Combos.Length - 1)
                {
                    game.MarkPlayerDiedForTests();
                    Line("forced death");
                }
                else
                {
                    game.GiveUp();
                    Line("give up");
                }
                yield return Wait(1.8f);
                yield return Shot($"60_result_{combo.label}");
                Line($"result state={game.State} reason={game.EndReason} cleared={game.Cleared}");
                if (game.State != GameState.GameOver) Fail($"run {combo.label} did not reach result");

                if (i < Combos.Length - 1) game.Restart();
                else game.GoToTitle();
            }

            yield return Wait(2f);
            yield return Shot("70_title_after_runs");
            // 2026-10-09: cards taken in the QA runs are now discovered — capture their codex text incl. final forms.
            title = FindFirstObjectByType<TitleView>();
            if (title != null && Click(title.transform, "CardCodexButton"))
            {
                yield return Wait(.6f);
                foreach (var id in new[] { "rifled_barrel", "piercing_arrowhead", "champagne_spray", "tea_leaf_scatter", "molotov", "blazer", "nettle_tea", "nightshade_blend", "flambe", "hybrid_tea_martini" })
                {
                    if (!title.CodexView.OpenDetailFor(id)) { Line("codex detail unavailable after runs " + id); continue; }
                    yield return Wait(.1f); yield return Shot("71_detail_discovered_" + id);
                    title.CodexView.HideDetail();
                }
                if (!Click(title.CodexView.transform, "BackButton") && !Click(title.CodexView.transform, "CloseButton"))
                    Fail("codex has no Back/Close button after runs");
                yield return Wait(.4f);
            }
            // A separate isolated forced final kill gives the real success Result screen.
            title = FindFirstObjectByType<TitleView>();
            title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var successGame = GameManager.Instance;
            yield return ClearOverlays(successGame, "forced success");
            FreezeWorld(successGame);
            var finalBoss = successGame.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f });
            yield return Wait(1.2f);
            finalBoss.Invulnerable = false;
            finalBoss.TakeDamage(finalBoss.Hp + 1f);
            successGame.ResolveEndOfFrame();
            yield return Wait(1.5f); yield return Shot("61_result_success");
            var successResult = FindFirstObjectByType<ResultView>();
            if (successResult != null && Click(successResult.transform, "SeedToggle"))
            {
                yield return Wait(.2f); yield return Shot("62_result_seed_expanded");
                if (!successResult.SeedVisible) Fail("seed detail did not expand");
            }
            else Fail("result seed toggle missing");
            if (!successGame.Cleared) Fail("forced final kill did not produce success Result");
            successGame.GoToTitle();
            yield return Wait(.5f);
            FlushLog();
        }

        // ── 플레이어 모션(4무기×좌우) ─────────────────────────────────────────────

        static EnemyDef dummyDef;

        /// <summary>정지 표적: 기존 Imp 정의 복제 — 속도0·접촉 피해0·투척 없음, 체력 매우 큼(강제, 보고 대상).</summary>
        static EnemyDef DummyDef()
        {
            if (dummyDef != null) return dummyDef;
            var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            dummyDef = (EnemyDef)clone.Invoke(EnemyDef.Imp, null);
            dummyDef.speed = 0f;
            dummyDef.contactDamage = 0f;
            dummyDef.projectileDamage = 0f;
            dummyDef.explodesOnContact = false;
            dummyDef.hp = 1000000f;
            return dummyDef;
        }

        Enemy dummy;

        void SpawnDummy(GameManager game, float dx)
        {
            KillDummy();
            dummy = Enemy.SpawnAt(game.Player.Position + new Vector2(dx, 0f), DummyDef(), 1f, game.Player, game.transform);
        }

        void KillDummy()
        {
            if (!ReferenceEquals(dummy, null) && dummy != null) Destroy(dummy.gameObject);
            dummy = null;
        }

        IEnumerator MotionRun()
        {
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            for (int i = 0; i < Combos.Length; i++)
            {
                var combo = Combos[i];
                // 런1: 전체 전환 + 우향 사망
                yield return StartCombo(combo, false);
                var game = GameManager.Instance;
                if (game == null) yield break;
                yield return ClearOverlays(game, "motion");
                FreezeWorld(game);
                yield return Wait(0.5f);
                yield return MotionSequence(game, combo.label, true);
                yield return Wait(2f);
                Line($"motion {combo.label} run1 result state={game.State} reason={game.EndReason}");
                if (game.State != GameState.GameOver) Fail($"motion {combo.label} run1 did not end");
                game.Restart();
                // 런2: 좌향 사망 + 재시작 경로
                yield return StartCombo(combo, false);
                game = GameManager.Instance;
                if (game == null) yield break;
                yield return ClearOverlays(game, "motion2");
                FreezeWorld(game);
                yield return Wait(0.5f);
                yield return MotionSequence(game, combo.label, false);
                yield return Wait(2f);
                Line($"motion {combo.label} run2 result state={game.State} reason={game.EndReason}");
                if (game.State != GameState.GameOver) Fail($"motion {combo.label} run2 did not end");
                if (i < Combos.Length - 1) game.Restart();
                else game.GoToTitle();
                FlushLog();
            }
            yield return Wait(2f);
        }

        /// <param name="full">true: idle/move/정지/공격/피격 전부 + 우향 사망. false: 좌향 대기 후 좌향 사망만.</param>
        IEnumerator MotionSequence(GameManager game, string label, bool full)
        {
            var player = game.Player;
            var anim = player.Visual.GetComponent<SpriteAnimator>();
            bool melee = game.Weapon == WeaponKind.Rapier || game.Weapon == WeaponKind.Sword;
            float stopDist = melee ? 1.1f : 3.2f;
            var cam = Camera.main;

            var segs = new List<(string name, int frames, Action<int> step)>();
            Vector2 In(float x) => new Vector2(x, 0f);
            void Toward(int f)
            {
                if (dummy == null) { player.AutoInputOverride = Vector2.zero; return; }
                float dx = dummy.transform.position.x - player.Position.x;
                player.AutoInputOverride = Mathf.Abs(dx) > stopDist ? In(Mathf.Sign(dx)) : Vector2.zero;
            }
            if (full)
            {
                segs.Add(("idle_R", 90, f => { if (f == 0) player.SetFacing(player.Position.x + 10f); player.AutoInputOverride = Vector2.zero; }));
                segs.Add(("move_R", 75, f => player.AutoInputOverride = In(1f)));
                segs.Add(("stop_R", 45, f => player.AutoInputOverride = Vector2.zero));
                segs.Add(("move_L", 75, f => player.AutoInputOverride = In(-1f)));
                segs.Add(("idle_L", 90, f => player.AutoInputOverride = Vector2.zero));
                segs.Add(("approach_attack_R", 120, f => { if (f == 0) SpawnDummy(game, 3.6f); Toward(f); }));
                segs.Add(("attack_end_idle_R", 45, f => { if (f == 0) KillDummy(); player.AutoInputOverride = Vector2.zero; }));
                segs.Add(("approach_attack_L", 120, f => { if (f == 0) SpawnDummy(game, -3.6f); Toward(f); }));
                segs.Add(("attack_end_idle_L", 45, f => { if (f == 0) KillDummy(); player.AutoInputOverride = Vector2.zero; }));
                segs.Add(("hit_move_R", 60, f => { player.AutoInputOverride = In(1f); if (f == 0 || f == 30) player.TakeDamage(1f); }));
                segs.Add(("hit_attack_L", 75, f =>
                {
                    if (f == 0) { player.SetFacing(player.Position.x - 10f); SpawnDummy(game, -stopDist * 0.8f); }
                    player.AutoInputOverride = Vector2.zero;
                    if (f == 20 || f == 50) player.TakeDamage(1f);
                    if (f == 74) KillDummy();
                }));
                segs.Add(("hit_idle_R", 45, f => { if (f == 0) player.SetFacing(player.Position.x + 10f); player.AutoInputOverride = Vector2.zero; if (f == 5) player.TakeDamage(1f); }));
                if (game.Weapon == WeaponKind.Rapier)
                {
                    segs.Add(("hybrid_unlock", 1, f =>
                    {
                        game.Rewards.RequestHybrid();
                    }));
                    segs.Add(("hybrid_move_R", 60, f => player.AutoInputOverride = In(1f)));
                    segs.Add(("hybrid_move_L", 60, f => player.AutoInputOverride = In(-1f)));
                    segs.Add(("hybrid_attack_R", 90, f => { if (f == 0) SpawnDummy(game, 5f); player.AutoInputOverride = Vector2.zero; if (f == 0) player.SetFacing(player.Position.x + 10f); }));
                    // 가까운 표적: 주무기 근접 베기 + 보조 활 발사가 함께 나오는지 좌우 각각
                    segs.Add(("hybrid_melee_bow_R", 90, f => { if (f == 0) SpawnDummy(game, 1.0f); player.AutoInputOverride = Vector2.zero; }));
                    segs.Add(("hybrid_melee_bow_L", 90, f => { if (f == 0) SpawnDummy(game, -1.0f); player.AutoInputOverride = Vector2.zero; }));
                    segs.Add(("hybrid_end", 15, f => { if (f == 0) KillDummy(); }));
                }
                segs.Add(("death_R", 100, f =>
                {
                    player.AutoInputOverride = Vector2.zero;
                    if (f == 0) { player.Heal(1f); player.SetFacing(player.Position.x + 10f); }
                    if (f == 10) player.TakeZoneDamage(100000f); // [강제] 사망
                }));
            }
            else
            {
                segs.Add(("turn_L", 20, f => player.AutoInputOverride = In(-1f)));
                segs.Add(("idle_L2", 45, f => player.AutoInputOverride = Vector2.zero));
                segs.Add(("death_L", 100, f =>
                {
                    player.AutoInputOverride = Vector2.zero;
                    if (f == 10) player.TakeZoneDamage(100000f); // [강제] 사망
                }));
            }

            int total = 0;
            foreach (var s in segs) total += s.frames;
            var starts = new List<int>();
            int acc = 0;
            foreach (var s in segs) { starts.Add(acc); acc += s.frames; }
            int segIndex = 0, local = 0;
            string clipName = $"motion_{label}_{(full ? "run1" : "run2_deathL")}";
            File.WriteAllText(Path.Combine(outDir, $"{clipName}-segments.txt"),
                string.Join("\n", segs.ConvertAll(s => $"{s.name},{s.frames}")), new UTF8Encoding(false));

            string Row()
            {
                var v = player.Visual;
                var clip = anim != null ? anim.Current : null;
                Vector3 sp = cam != null ? cam.WorldToScreenPoint(player.transform.position) : Vector3.zero;
                Vector3 wp = v.Weapon != null ? v.Weapon.transform.parent.position : Vector3.zero;
                Vector3 sp2 = v.SecondaryWeapon != null ? v.SecondaryWeapon.transform.parent.position : Vector3.zero;
                string seg = segs[Mathf.Min(segIndex, segs.Count - 1)].name;
                return $"{seg},{F(player.Facing)},{F(player.AutoInputOverride?.x ?? 0f)},{F(player.Position.x)},{F(player.Position.y)}," +
                       $"{F(sp.x)},{F(sp.y)},{clip?.name},{anim?.FrameIndex},{v.Body.sprite?.name},{F(v.transform.localScale.x)}," +
                       $"{F(wp.x - player.Position.x)},{F(wp.y - player.Position.y)},{(v.WeaponUsesProvisionalAnchor ? 1 : 0)}," +
                       $"{(v.SecondaryWeapon != null ? F(sp2.x - player.Position.x) : "")},{(v.SecondaryWeapon != null ? F(sp2.y - player.Position.y) : "")}," +
                       $"{F(v.Body.color.g)},{F(player.Hp)},{(player.IsDead ? 1 : 0)},{game.State},{(dummy != null ? F(dummy.transform.position.x - player.Position.x) : "")}," +
                       $"{PlayerProjectile.ActiveCount}";
            }

            yield return Record(clipName, total,
                "segment,facing,inputX,posX,posY,screenX,screenY,clip,clipFrame,sprite,visualScaleX,handDX,handDY,provisionalHand,offHandDX,offHandDY,tintG,hp,dead,state,dummyDX,playerShots",
                f =>
                {
                    while (segIndex < segs.Count - 1 && f >= starts[segIndex + 1]) segIndex++;
                    local = f - starts[segIndex];
                    segs[segIndex].step(local);
                    if (game.HybridView.IsOpen) { game.HybridView.Unlock(); Line($"motion: hybrid unlocked={game.HybridUnlocked}"); }
                },
                Row,
                f =>
                {
                    // 각 구간 첫 프레임과 중간 프레임을 PNG로(검수용 정지 화면)
                    for (int k = 0; k < segs.Count; k++)
                    {
                        if (segs[k].frames < 10) continue;
                        if (f == starts[k] + 2) return $"80_{clipName}_{k:D2}_{segs[k].name}_a";
                        if (f == starts[k] + segs[k].frames / 2) return $"80_{clipName}_{k:D2}_{segs[k].name}_b";
                    }
                    return null;
                }, game);
            KillDummy();
            player.AutoInputOverride = null;
        }

        // ── 적/보스 몸체 프레임 노출 갤러리(아트 검수 363프레임 매핑용) ─────────────────────────────────────────────

        static EnemyDef StillClone(EnemyDef def)
        {
            var clone = (EnemyDef)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(def, null);
            clone.speed = 0f;
            clone.contactDamage = 0f;
            clone.projectileDamage = 0f;
            clone.explodesOnContact = false;
            return clone;
        }

        /// <summary>[강제] 몸체마다 혼자 스폰(속도0·접촉0 복제 정의, 드라이버 없음, 플레이어 무기 정지) → 왼쪽 향함 idle·move·action·hurt →
        /// 반대편으로 옮겨 오른쪽 향함 같은 순서 → 처치해 defeat(DefeatVisual). 프레임별 assetId/clip/frame/sprite/facing CSV.
        /// 실제 AI 행동 검증이 아니라 원본 프레임의 실제 엔진 노출·좌우·전환 기록이다.</summary>
        IEnumerator BestiaryRun()
        {
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var game = GameManager.Instance;
            if (game == null) yield break;
            var player = game.Player;
            yield return ClearOverlays(game, "bestiary");
            FreezeWorld(game);
            SetPlayerWeapons(player, false);
            // olive_fire 가림 검수: 실제 올리브 폭탄병(원래 정의·접촉 폭발)이 플레이어에 닿아 터지는 순간과 장판 지속을 연속 기록.
            {
                Enemy bomber = null;
                yield return Record("bestiary_olive_fire", 150, "bomberAlive,burstZones,playerHp,playerX",
                    f =>
                    {
                        if (f == 0) bomber = Enemy.SpawnAt(player.Position + new Vector2(2.6f, 0f), EnemyDef.OliveBomber, 1f, player, game.transform);
                        if (f % 15 == 0 && player != null) player.Heal(1f);
                    },
                    () => $"{(bomber != null ? 1 : 0)},{BurstZone.ActiveCount},{F(player.Hp)},{F(player.Position.x)}",
                    f => f == 45 || f == 75 ? $"91_olive_fire_f{f}" : null, game);
                if (bomber != null) Destroy(bomber.gameObject);
                yield return ClearOverlays(game, "olive_fire");
            }
            EnemyDef[] bodies =
            {
                EnemyDef.Imp, EnemyDef.Brute, EnemyDef.ShakerGolem, EnemyDef.OliveBomber, EnemyDef.SwirlConjuror,
                EnemyDef.MartiniSpirit, EnemyDef.TeabagImp, EnemyDef.KettleCannon, EnemyDef.SugarCubeBomber,
                EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian, EnemyDef.EspressoBug, EnemyDef.MilkFoamer,
                EnemyDef.GrinderGolem, EnemyDef.DripServerSpecter, EnemyDef.EspressoLeviathan, EnemyDef.DifficultCustomer,
            };
            foreach (var def in bodies)
            {
                var clone = StillClone(def);
                clone.boss = BossKind.None; // 갤러리 처치가 보스 보상/클리어로 런을 끝내지 않게(보스 몸체·클립은 그대로)
                string id = def.ArtId;
                string[] specials = def == EnemyDef.GrinderGolem ? new[] { "charge_prepare", "charge" }
                    : def == EnemyDef.DripServerSpecter ? new[] { "drip", "vanish", "appear" }
                    : def == EnemyDef.EspressoLeviathan ? new[] { "steam", "rain", "pressure" }
                    : def == EnemyDef.DifficultCustomer ? new[] { "refund", "summon", "review", "reject" }
                    : new[] { "action" };
                Enemy e = null;
                DefeatVisual dv = null;
                var segs = new List<(string name, int frames, Action<int> step)>();
                void Place(float dx) { if (e != null) e.transform.position = new Vector3(player.Position.x + dx, Balance.LaneCenterY + 0.3f, 0f); }
                foreach (var side in new[] { 3.2f, -3.2f })
                {
                    string dir = side > 0 ? "faceL" : "faceR";
                    float s = side;
                    segs.Add(($"idle_{dir}", 45, f =>
                    {
                        if (e == null && f == 0) { e = Enemy.SpawnAt(player.Position + new Vector2(s, 0.3f), clone, 1000f, player, game.transform); }
                        if (f == 0) Place(s);
                        clone.speed = 0f;
                        if (e != null && e.Invulnerable && f > 35) e.Invulnerable = false;
                    }));
                    segs.Add(($"move_{dir}", 30, f =>
                    {
                        // 플레이어 쪽으로 이동(실제 이동 클립). 끝에 원래 거리로 되돌린다.
                        clone.speed = Mathf.Max(1.2f, def.speed);
                        if (f == 29) { clone.speed = 0f; Place(s); }
                    }));
                    foreach (var clipName in specials)
                    {
                        string cn = clipName;
                        segs.Add(($"{cn}_{dir}", 30, f => { if (f == 0 && e != null && !e.PlayAction(cn, 0.9f)) Line($"bestiary {id}: no {cn} clip"); }));
                    }
                    segs.Add(($"hurt_{dir}", 12, f => { if (f == 0 && e != null) { e.Invulnerable = false; e.TakeDamage(1f); } }));
                    // 그 방향을 바라본 채 쓰러짐(defeat L/R 각각) — 다음 방향 구간 첫 프레임에서 새로 스폰한다
                    segs.Add(($"defeat_{dir}", 30, f =>
                    {
                        if (f == 0) { dv = null; if (e != null) { e.Invulnerable = false; e.TakeDamage(e.Hp + 1f);
                            // 원본 사망 포즈 QA에서만 피격 효과를 제거한다. 일반 플레이에는 적용하지 않는다.
                            foreach (var v in FindObjectsByType<FadeVfx>(FindObjectsSortMode.None))
                                if (v.Renderer?.sprite != null && v.Renderer.sprite.name.StartsWith("hit_spark")) Destroy(v.gameObject);
                        } }
                        if (dv == null) dv = FindFirstObjectByType<DefeatVisual>();
                    }));
                }
                var starts = new List<int>();
                int total = 0;
                foreach (var sg in segs) { starts.Add(total); total += sg.frames; }
                int segIndex = 0;
                yield return Record($"bestiary_{id}", total,
                    "assetId,segment,clip,clipFrame,sprite,facing,enemyX,playerX,source",
                    f =>
                    {
                        while (segIndex < segs.Count - 1 && f >= starts[segIndex + 1]) segIndex++;
                        segs[segIndex].step(f - starts[segIndex]);
                        if (player != null && f % 15 == 0) player.Heal(1f);
                    },
                    () =>
                    {
                        string seg = segs[segIndex].name;
                        if (e != null && !e.IsDead && e.Animator != null)
                        {
                            var sr = e.Animator.GetComponent<SpriteRenderer>();
                            return $"{id},{seg},{e.Animator.Current?.name},{e.Animator.FrameIndex},{sr?.sprite?.name},{(e.Animator.transform.localScale.x < 0 ? -1 : 1)},{F(e.transform.position.x)},{F(player.Position.x)},enemy";
                        }
                        if (dv != null)
                        {
                            var a = dv.GetComponent<SpriteAnimator>();
                            var sr = dv.GetComponent<SpriteRenderer>();
                            return $"{id},{seg},{a?.Current?.name},{a?.FrameIndex},{sr?.sprite?.name},{(dv.transform.localScale.x < 0 ? -1 : 1)},{F(dv.transform.position.x)},{F(player.Position.x)},defeatVisual";
                        }
                        return $"{id},{seg},,,,,,{F(player.Position.x)},none";
                    },
                    f => f == starts[0] + 20 ? $"90_bestiary_{id}" : null, game);
                if (e != null) Destroy(e.gameObject);
                foreach (var p in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)) Destroy(p.gameObject);
                yield return ClearOverlays(game, "bestiary " + id);
                if (game.State == GameState.GameOver) { Line($"bestiary: run ended after {id} ({game.EndReason})"); break; }
                FlushLog();
            }
            if (GameManager.Instance != null && game.State != GameState.GameOver) { game.OpenPause(); yield return Wait(0.3f); game.GiveUp(); }
            yield return Wait(1.5f);
        }

        /// <summary>2026-10-09 검수표 잔여: 활 초반 원거리 우선 카드, 레이피어/검 최대 연속타(마무리 일격) 좌우, 20:00 시간초과 결과.
        /// 강제 경로(레벨업 재료 지급·최종형태 카드 직접 부여·정지 표적·런 시간 설정) — 화면 근거 전용, 정상 플레이 근거 아님.</summary>
        IEnumerator RemainingRun()
        {
            Line("[forced QA] remaining checklist captures; never normal-play evidence");
            // 1) 활 초반 원거리 우선 슬롯(180초·6선택 이내 60%) — 실제 레벨업 보상 화면을 우선 라벨이 나올 때까지 연다.
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[3], false);
            var game = GameManager.Instance;
            if (game == null) { Fail("bow run did not start"); yield break; }
            bool found = false;
            for (int i = 0; i < 6 && !found; i++)
            {
                yield return ClearOverlays(game, "bow priority");
                game.AddIngredient(IngredientType.Cocktail, Mathf.Max(1, game.CocktailGauge.Threshold - game.CocktailGauge.Current));
                yield return Wait(0.6f);
                if (!game.CardView.IsOpen) continue;
                found = game.CardView.GetComponentsInChildren<Text>().Any(t => t.text.Contains("초반 원거리 우선 추첨"));
                Line($"bow offer {i}: priority label={found} runTime={game.RunTime:0.0}");
                if (found) yield return Shot("80_card_reward_bow_priority"); // game 인자를 주면 보상창을 닫고 찍는다
                else game.CardView.Choose(0);
            }
            if (!found) Fail("bow early priority label not observed in 6 offers");
            game.GoToTitle();
            yield return Wait(0.8f);

            // 2) 레이피어·검 최대 연속타 최종형태(마무리 일격: 마지막 타격 범위125%·피해150%) — 좌우 연속 프레임.
            foreach (var (comboIndex, cardId, label) in new[] { (0, "flambe", "rapier"), (2, "second_infusion", "sword") })
            {
                title = FindFirstObjectByType<TitleView>();
                if (title != null && title.IsOpen) title.RequestStart();
                yield return StartCombo(Combos[comboIndex], false);
                game = GameManager.Instance;
                if (game == null) { Fail(label + " run did not start"); continue; }
                yield return ClearOverlays(game, "chain " + label);
                FreezeWorld(game);
                var player = game.Player;
                var card = CardDatabase.LoadAll().First(c => c.id == cardId);
                for (int n = 0; n < card.maxStacks; n++) game.Cards.Take(card, game.Stats);
                player.FinalForms.OnCardTaken(card, game.Cards.GetStacks(card));
                yield return ClearOverlays(game, "chain banner " + label);
                foreach (var side in new[] { 1f, -1f })
                {
                    foreach (var e in new List<Enemy>(Enemy.Active)) if (e != null) Destroy(e.gameObject);
                    for (int n = 0; n < 3; n++)
                        Enemy.SpawnAt(player.Position + new Vector2(side * (1.0f + n * 0.45f), (n - 1) * 0.25f), DummyDef(), 1f, player, game.transform);
                    string clip = $"81_final_chain_{label}_{(side > 0 ? "R" : "L")}";
                    yield return Record(clip, 75, "chainForm,swordSwing,facing,enemies",
                        f => { }, // 고HP 정지 표적(DummyDef)
                        () => $"{player.FinalForms.Has(AttackFinalForm.Chain)},{player.Visual.SwordSwingActive},{player.Facing},{Enemy.Active.Count}",
                        f => f == 12 ? clip : null, game);
                }
                game.GoToTitle();
                yield return Wait(0.8f);
            }

            // 3) 20:00 시간초과 결과(06 종료 우선순위 경로) — 런 시간만 19:58.5로 설정.
            title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            game = GameManager.Instance;
            if (game == null) { Fail("timeout run did not start"); yield break; }
            yield return ClearOverlays(game, "timeout");
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds - 1.5f);
            yield return Wait(3f);
            var result = FindFirstObjectByType<ResultView>();
            Line($"timeout result open={result != null && result.IsOpen} title={result?.TitleText} reason={game.EndReason}");
            if (result == null || !result.IsOpen || result.TitleText != "영업 종료") Fail("timeout result not shown");
            yield return Shot("63_result_timeout", game);
            game.GoToTitle();
            yield return Wait(0.6f);
        }

        IEnumerator SaveRecoveryRun()
        {
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var game = GameManager.Instance;
            if (game == null) yield break;
            yield return ClearOverlays(game, "save recovery");
            FreezeWorld(game); SetPlayerWeapons(game.Player, false);
            SaveSystem.FaultHookForTests = stage => { throw new IOException("QA isolated save failure"); };
            game.AddIngredient(IngredientType.Cocktail, 1); PermanentProgress.SaveIfDirty();
            yield return Record("additional_save_error_before", 10, "saveFailed,hudVisible", f => { },
                () => $"{PermanentProgress.LastSaveFailed},{game.Hud.SaveFailedVisible}", null, game);
            SaveSystem.FaultHookForTests = null;
            PermanentProgress.SaveIfDirty();
            yield return Record("additional_save_recovered", 15, "saveFailed,hudVisible", f => { },
                () => $"{PermanentProgress.LastSaveFailed},{game.Hud.SaveFailedVisible}", null, game);
            game.QuitToTitle();
        }

        IEnumerator IceRun()
        {
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var game = GameManager.Instance;
            if (game == null) yield break;
            yield return ClearOverlays(game, "ice");
            FreezeWorld(game); SetPlayerWeapons(game.Player, false);
            var ice = Enemy.SpawnAt(game.Player.Position + new Vector2(5f, 1f), EnemyDef.ShakerGolem, 1f, game.Player, game.transform);
            ice.gameObject.AddComponent<RangedAttackDriver>().Init(ice, game.Player, game);
            yield return Record("additional_shaker_ice_shard", 90, "projectiles,hp", f => { },
                () => $"{EnemyProjectile.ActiveCount},{F(game.Player.Hp)}", null, game);
            Destroy(ice.gameObject); // 새로운 사격은 중단, 남은 탄의 정상 충돌/3초 수명 끝을 관찰
            yield return Record("additional_ice_expiry", 105, "projectiles,hp", f => { },
                () => $"{EnemyProjectile.ActiveCount},{F(game.Player.Hp)}", null, game);
            game.QuitToTitle();
        }

        // 아트 담당이 요청한 남은 포즈/공용 효과만 촬영. QA에서만 hit_spark를 제거해 사망 원본을 확인한다.
        IEnumerator AdditionalRun(bool deathsOnly = false)
        {
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var game = GameManager.Instance;
            if (game == null) yield break;
            yield return ClearOverlays(game, "additional");
            FreezeWorld(game);
            var player = game.Player;
            SetPlayerWeapons(player, false);
            EnemyDef[] bodies = { EnemyDef.Imp, EnemyDef.Brute, EnemyDef.ShakerGolem, EnemyDef.OliveBomber,
                EnemyDef.SwirlConjuror, EnemyDef.MartiniSpirit, EnemyDef.TeabagImp, EnemyDef.KettleCannon,
                EnemyDef.SugarCubeBomber, EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian, EnemyDef.EspressoBug,
                EnemyDef.MilkFoamer, EnemyDef.GrinderGolem, EnemyDef.DripServerSpecter, EnemyDef.EspressoLeviathan,
                EnemyDef.DifficultCustomer };
            foreach (var def in bodies)
            foreach (int facing in def == EnemyDef.DifficultCustomer ? new[] { -1, 1 } : new[] { -1 })
            {
                var clone = StillClone(def); clone.boss = BossKind.None;
                var e = Enemy.SpawnAt(player.Position + new Vector2(-facing * 3.2f, 0.3f), clone, 1f, player, game.transform);
                yield return null;
                DefeatVisual dv = null;
                yield return Record($"additional_death_{def.ArtId}_{facing}", 30,
                    "assetId,clip,clipFrame,sprite,facing,source",
                    f => { if (f == 0) {
                        e.Invulnerable = false; e.TakeDamage(e.Hp + 1f);
                        foreach (var v in FindObjectsByType<FadeVfx>(FindObjectsSortMode.None))
                            if (v.Renderer != null && v.Renderer.sprite != null && v.Renderer.sprite.name.StartsWith("hit_spark")) Destroy(v.gameObject);
                        dv = FindFirstObjectByType<DefeatVisual>();
                        Line($"source-pose QA ONLY: hit_spark suppressed {def.ArtId} facing={facing}");
                    } },
                    () => { if (dv == null) return $"{def.ArtId},,,,,expired";
                        var anim = dv.GetComponent<SpriteAnimator>(); var sr = dv.GetComponent<SpriteRenderer>();
                        return $"{def.ArtId},{anim.Current?.name},{anim.FrameIndex},{sr.sprite?.name},{facing},defeatVisual"; },
                    null, game);
                foreach (var p in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            }
            if (deathsOnly) { game.QuitToTitle(); yield break; }
            // 실제 보스 등장 경로를 사용하되 QA에서 중앙으로 옮겨 전체 등장 효과를 보인다.
            Enemy boss = null;
            yield return Record("additional_boss_arrival", 60, "bossSprite,bossFacing",
                f => { if (f == 0) { boss = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.GrinderGolem, hpScale = 1f });
                    if (boss != null) { Vector3 delta = (Vector3)(player.Position + new Vector2(4f, 0f)) - boss.transform.position;
                        boss.transform.position += delta;
                        foreach (var v in FindObjectsByType<FadeVfx>(FindObjectsSortMode.None))
                            if (v.Renderer?.sprite != null && v.Renderer.sprite.name.StartsWith("boss_arrival")) v.transform.position += delta;
                    } } },
                () => $"{boss?.Animator?.GetComponent<SpriteRenderer>()?.sprite?.name},-1", null, game);
            if (boss != null) Destroy(boss.gameObject);
            // 실제 재료 획득/보상 경로. 보상 선택 패널을 즉시 닫은 후 glint의 unscaled 재생/소멸을 기록한다.
            yield return Record("additional_glint_poison", 120, "overlay,poison",
                f => { if (f == 0) { game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold); }
                    ResolveRewardScreens(game, "additional glint");
                    if (f == 35) game.Stats.Apply(new CardEffect(CardEffectType.PoisonLevel, 1f)); },
                () => $"{(OverlayOpen(game) ? 1 : 0)},{game.Stats.poisonLevel}", null, game);
            var ice = Enemy.SpawnAt(player.Position + new Vector2(5f, 1f), EnemyDef.MartiniSpirit, 1f, player, game.transform);
            ice.gameObject.AddComponent<RangedAttackDriver>().Init(ice, player, game);
            yield return Record("additional_ice_shard", 180, "projectiles,hp", f => { if (f % 30 == 0) player.Heal(1f); },
                () => $"{EnemyProjectile.ActiveCount},{F(player.Hp)}", null, game);
            Destroy(ice.gameObject);
            Enemy customer = null;
            yield return Record("additional_suppression_expiry", 360, "suppressed,hp",
                f => { if (f == 0) { customer = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f });
                        customer.transform.position = player.Position + new Vector2(2.5f, 0f); }
                    if (f % 30 == 0) player.Heal(1f); },
                () => $"{game.Stats.BonusesSuppressed},{F(player.Hp)}", null, game);
            if (customer != null) Destroy(customer.gameObject);
            // 격리 QA 저장에서만 디스크 쓰기 실패를 주입하고 실제 HUD 오류 아이콘/복구를 기록한다.
            SaveSystem.FaultHookForTests = stage => { throw new IOException("QA isolated save failure"); };
            game.AddIngredient(IngredientType.Cocktail, 1);
            PermanentProgress.SaveIfDirty();
            yield return Record("additional_save_error", 45, "saveFailed", f => { },
                () => PermanentProgress.LastSaveFailed.ToString(), null, game);
            SaveSystem.FaultHookForTests = null;
            PermanentProgress.SaveIfDirty();
            yield return Wait(0.5f);
            game.QuitToTitle();
        }

        // ── 오디오 출력 탭 ─────────────────────────────────────────────

        QaAudioTap tap;

        void Mark(string name)
        {
            var svc = AudioService.Instance;
            int listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            var playing = new List<string>();
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying) playing.Add($"{s.name}:{(s.clip != null ? s.clip.name : "null")}@{s.volume:0.###}");
            string text = $"MARK {name} listeners={listeners} audioListener.volume={AudioListener.volume:0.##} pause={AudioListener.pause} " +
                          $"bus master={AudioBus.MasterVolume:0.##} music={AudioBus.MusicVolume:0.##} sfx={AudioBus.SfxVolume:0.##} ducked={AudioBus.MusicDucked} " +
                          $"suppressed={AudioService.CombatSuppressed} effMusic={AudioBus.EffectiveMusicVolume:0.###} effSfx={AudioBus.EffectiveSfxVolume:0.###} " +
                          $"service={(svc != null)} playing[{playing.Count}]={string.Join(";", playing)}";
            Line(text);
            tap?.SetMarker(name);
            // [강제] 측정 구간이 사망으로 끊기지 않게 회복(오디오 경로 측정용, 난도 검증 아님)
            var g = GameManager.Instance;
            if (g != null && g.Player != null && !g.Player.IsDead) g.Player.Heal(1f);
        }

        IEnumerator SfxRun()
        {
            var title = FindFirstObjectByType<TitleView>();
            if (title != null && title.IsOpen) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var game = GameManager.Instance;
            if (game == null) yield break;
            yield return ClearOverlays(game, "sfx playback");
            FreezeWorld(game);
            SetPlayerWeapons(game.Player, false);
            SettingsService.SetVolume(VolumeChannel.Music, 0);
            SettingsService.SetVolume(VolumeChannel.Sfx, 80);
            var svc = AudioService.Instance;
            tap = svc.gameObject.AddComponent<QaAudioTap>();
            tap.Begin(Path.Combine(outDir, "audio-tap.wav"), Path.Combine(outDir, "audio-tap.csv"));
            yield return Wait(1f);
            Line($"isolated playback actual Run state={game.State} combatSuppressed={AudioService.CombatSuppressed}");
            if (AudioService.CombatSuppressed) { Fail("combat SFX playback incorrectly suppressed"); yield break; }
            foreach (var id in new[] { "boss_warning", "clear", "defeat", "enemy_defeat", "explosion", "hit_enemy", "hit_player",
                         "hybrid_unlock", "levelup", "pickup", "shot_bow", "shot_gun", "shot_hybrid", "skill_unlock", "spell", "steam",
                         "swing_rapier", "swing_sword", "ui_cancel", "ui_confirm", "ui_denied", "ui_focus" })
            {
                Mark("sfx_" + id);
                AudioService.PlaySfx(id);
                yield return Wait(1.3f);
            }
            Mark("sfx_done_silence"); yield return Wait(1f);
            tap.End(); tap = null;
            game.QuitToTitle();
        }

        IEnumerator AudioRun()
        {
            var svc = AudioService.Instance;
            if (svc == null) { Fail("no AudioService"); yield break; }
            tap = svc.gameObject.AddComponent<QaAudioTap>();
            tap.Begin(Path.Combine(outDir, "audio-tap.wav"), Path.Combine(outDir, "audio-tap.csv"));
            Line($"audio: outputSampleRate={AudioSettings.outputSampleRate} speakerMode={AudioSettings.speakerMode} driverCaps={AudioSettings.driverCapabilities} " +
                 $"dspBuffer={AudioSettings.GetConfiguration().dspBufferSize}");

            Mark("title_menu_bgm"); yield return Wait(4f);
            // 각 실제 BGM 소스가 파일 끝을 지나 처음으로 반복하는 구간까지 최종 출력 탭을 연속 기록한다.
            foreach (var pair in new[] { ("bgm_menu", AudioService.MusicState.Menu), ("bgm_run", AudioService.MusicState.Run), ("bgm_boss", AudioService.MusicState.Boss) })
            {
                AudioService.SetMusicState(pair.Item2);
                yield return Wait(1f);
                var clip = Resources.Load<AudioClip>("Audio/v1/" + pair.Item1);
                Mark("loop_" + pair.Item1);
                Line($"loop observation {pair.Item1} clipSeconds={clip.length:0.000} duration={clip.length + 2f:0.000}");
                yield return Wait(clip.length + 2f);
            }
            AudioService.SetMusicState(AudioService.MusicState.Menu);
            yield return Wait(1f);
            // G33: 22 SFX를 하나씩 실제 출력 경로로 재생(BGM 0으로 두고 각 효과만 측정)
            SettingsService.SetVolume(VolumeChannel.Music, 0);
            yield return Wait(0.6f);
            foreach (var id in new[] { "boss_warning", "clear", "defeat", "enemy_defeat", "explosion", "hit_enemy", "hit_player",
                         "hybrid_unlock", "levelup", "pickup", "shot_bow", "shot_gun", "shot_hybrid", "skill_unlock", "spell", "steam",
                         "swing_rapier", "swing_sword", "ui_cancel", "ui_confirm", "ui_denied", "ui_focus" })
            {
                Mark("sfx_" + id);
                AudioService.PlaySfx(id);
                yield return Wait(1.3f);
            }
            Mark("sfx_done_silence"); yield return Wait(1f);
            SettingsService.SetVolume(VolumeChannel.Music, 60);
            Mark("title_menu_bgm_again"); yield return Wait(2f);
            SettingsService.SetVolume(VolumeChannel.Master, 0); Mark("title_master0"); yield return Wait(3f);
            SettingsService.SetVolume(VolumeChannel.Master, 80); Mark("title_master80"); yield return Wait(2f);
            SettingsService.SetVolume(VolumeChannel.Music, 0); Mark("title_music0"); yield return Wait(2f);
            SettingsService.SetVolume(VolumeChannel.Music, 60); Mark("title_music60"); yield return Wait(2f);

            var title = FindFirstObjectByType<TitleView>();
            if (title != null) title.RequestStart();
            yield return StartCombo(Combos[1], false);
            var game = GameManager.Instance;
            if (game == null) { tap.End(); yield break; }
            // 전투 SFX가 나도록 표적을 둔다(총 자동 사격, 처치음) — 강제 스폰
            for (int k = 0; k < 6; k++)
                Enemy.SpawnAt(game.Player.Position + new Vector2(5f + k * 0.6f, (k % 3 - 1) * 1.2f), StillClone(EnemyDef.Imp), 1f, game.Player, game.transform);
            Mark("run_bgm_combat"); yield return Wait(5f);
            yield return ClearOverlays(game, "audio pause");
            game.OpenPause(); Mark("pause_open"); yield return Wait(3f);
            game.ClosePause(); Mark("pause_closed"); yield return Wait(4f);
            SettingsService.SetVolume(VolumeChannel.Sfx, 0); Mark("run_sfx0"); yield return Wait(3f);
            SettingsService.SetVolume(VolumeChannel.Sfx, 80); Mark("run_sfx80"); yield return Wait(2f);
            SettingsService.SetVolume(VolumeChannel.Music, 0); Mark("run_music0"); yield return Wait(3f);
            SettingsService.SetVolume(VolumeChannel.Music, 60); Mark("run_music60"); yield return Wait(2f);
            SettingsService.SetVolume(VolumeChannel.Master, 0); Mark("run_master0"); yield return Wait(3f);
            SettingsService.SetVolume(VolumeChannel.Master, 80); Mark("run_master80"); yield return Wait(2f);
            yield return ClearOverlays(game, "audio card");
            game.AddIngredient(IngredientType.Cocktail, Mathf.Max(1, game.CocktailGauge.Threshold - game.CocktailGauge.Current));
            yield return Wait(0.3f);
            Mark($"card_overlay open={game.CardView.IsOpen}"); yield return Wait(3f);
            if (game.CardView.IsOpen) game.CardView.Choose(0);
            Mark("card_closed"); yield return Wait(2f);
            game.OpenPause(); yield return Wait(0.3f);
            game.GiveUp(); Mark("result_giveup_stinger"); yield return Wait(5f);
            for (int r = 1; r <= 3; r++)
            {
                game.Restart(); Mark($"restart{r}_select"); yield return Wait(1f);
                yield return StartCombo(Combos[1], false);
                game = GameManager.Instance;
                if (game == null) break;
                Mark($"restart{r}_run"); yield return Wait(4f);
                game.OpenPause(); yield return Wait(0.3f);
                game.GiveUp(); Mark($"restart{r}_result"); yield return Wait(2.5f);
            }
            if (game != null) game.GoToTitle();
            Mark("back_to_title"); yield return Wait(3f);
            tap.End();
            Line($"audio tap: windows={tap.Windows} samples={tap.Samples} channels={tap.Channels}");
        }
    }

    /// <summary>QA 전용: AudioListener와 같은 오브젝트에서 OnAudioFilterRead로 Unity의 최종 출력 믹스(기기로 가는 샘플)를 받아
    /// 100ms 창 RMS/피크 CSV와 16비트 WAV로 저장한다. 사람이 듣는 청취 검수를 대신하지 않는다.</summary>
    public class QaAudioTap : MonoBehaviour
    {
        readonly object gate = new object();
        readonly Queue<float[]> pending = new Queue<float[]>();
        double sumSq, windowStart;
        float peak;
        long windowSamples;
        volatile int channels;
        int sampleRate;
        string marker = "start";
        FileStream wav;
        StreamWriter csv;
        long dataBytes;
        bool running;
        float lastWindowAt;

        public int Windows { get; private set; }
        public long Samples { get; private set; }
        public int Channels => channels;

        public void Begin(string wavPath, string csvPath)
        {
            sampleRate = AudioSettings.outputSampleRate;
            wav = new FileStream(wavPath, FileMode.Create, FileAccess.Write);
            wav.Write(new byte[44], 0, 44);
            csv = new StreamWriter(csvPath, false, new UTF8Encoding(false));
            csv.WriteLine("utc,realtime,marker,samples,rms,rms_dbfs,peak");
            lastWindowAt = Time.realtimeSinceStartup;
            running = true;
        }

        public void SetMarker(string name)
        {
            FlushWindow();
            marker = name;
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            if (!running) return;
            channels = ch;
            var copy = new float[data.Length];
            Array.Copy(data, copy, data.Length);
            lock (gate)
            {
                pending.Enqueue(copy);
                for (int i = 0; i < data.Length; i++)
                {
                    float v = data[i];
                    sumSq += v * v;
                    float a = v < 0 ? -v : v;
                    if (a > peak) peak = a;
                }
                windowSamples += data.Length;
            }
        }

        void Update()
        {
            if (!running) return;
            Drain();
            if (Time.realtimeSinceStartup - lastWindowAt >= 0.1f) FlushWindow();
        }

        void Drain()
        {
            while (true)
            {
                float[] buf;
                lock (gate)
                {
                    if (pending.Count == 0) break;
                    buf = pending.Dequeue();
                }
                var bytes = new byte[buf.Length * 2];
                for (int i = 0; i < buf.Length; i++)
                {
                    int s = Mathf.Clamp(Mathf.RoundToInt(buf[i] * 32767f), -32768, 32767);
                    bytes[2 * i] = (byte)(s & 0xff);
                    bytes[2 * i + 1] = (byte)((s >> 8) & 0xff);
                }
                wav.Write(bytes, 0, bytes.Length);
                dataBytes += bytes.Length;
                Samples += buf.Length;
            }
        }

        void FlushWindow()
        {
            if (csv == null) return;
            double s; float p; long n;
            lock (gate)
            {
                s = sumSq; p = peak; n = windowSamples;
                sumSq = 0; peak = 0; windowSamples = 0;
            }
            double rms = n > 0 ? Math.Sqrt(s / n) : 0;
            string db = rms > 0 ? (20 * Math.Log10(rms)).ToString("0.0", CultureInfo.InvariantCulture) : "-inf";
            csv.WriteLine($"{DateTime.UtcNow:o},{Time.realtimeSinceStartup.ToString("0.000", CultureInfo.InvariantCulture)},{marker},{n}," +
                          $"{rms.ToString("0.000000", CultureInfo.InvariantCulture)},{db},{p.ToString("0.000000", CultureInfo.InvariantCulture)}");
            Windows++;
            lastWindowAt = Time.realtimeSinceStartup;
        }

        public void End()
        {
            if (!running) return;
            running = false;
            Drain();
            FlushWindow();
            csv.Dispose();
            int ch = Mathf.Max(1, channels);
            wav.Seek(0, SeekOrigin.Begin);
            using (var w = new BinaryWriter(wav, Encoding.ASCII, true))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write((int)(36 + dataBytes)); w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(sampleRate);
                w.Write(sampleRate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write((int)dataBytes);
            }
            wav.Dispose();
        }

        void OnDestroy()
        {
            if (running) End();
        }
    }
}
#endif

