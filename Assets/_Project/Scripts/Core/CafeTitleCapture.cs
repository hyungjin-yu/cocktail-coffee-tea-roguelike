using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-18 카페 입장형 첫 화면 실빌드 검수(명시 인자 + 격리 저장에서만):
    /// <c>-cct-data-dir &lt;격리&gt; -cct-title-capture &lt;출력&gt; [-cct-title-capture-mode normal|low|future|savefail]</c>.
    /// 가상 마우스·키보드 장치에 실제 InputSystem 이벤트를 넣어 입장 스킵·물체 클릭·키보드 이동·열기/닫기를 실행하고 캡처한다.
    /// 사람 플레이가 아니며, future/savefail/low 모드의 저장 상태는 격리 폴더 fixture다.
    /// </summary>
    public class CafeTitleCapture : MonoBehaviour
    {
        string dir, mode;
        Mouse mouse;
        Keyboard kb;
        readonly List<string> checks = new List<string>();
        readonly StringBuilder log = new StringBuilder();
        int pass, fail;
        float start;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cct-title-capture");
            if (i < 0 || i + 1 >= args.Length || Array.IndexOf(args, "-cct-data-dir") < 0) return; // 격리 저장 없이는 실행하지 않음
            var go = new GameObject("CafeTitleCapture");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<CafeTitleCapture>();
            c.dir = args[i + 1];
            int m = Array.IndexOf(args, "-cct-title-capture-mode");
            c.mode = m >= 0 && m + 1 < args.Length ? args[m + 1] : "normal";
            c.StartCoroutine(c.Run());
        }

        // ───────── 입력 주입 ─────────

        void SetupDevices()
        {
            Application.runInBackground = true;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>("CafeQaMouse");
            kb = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>("CafeQaKeyboard");
            mouse.MakeCurrent(); kb.MakeCurrent();
            // 이 검수 프로세스 안에서만: 실제 키보드·마우스·패드를 끈다 — 사용자가 다른 창에서 치는 입력이 시험 창에 들어가지 않게(반대도 없음)
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices.ToArray())
                if (d != mouse && d != kb) UnityEngine.InputSystem.InputSystem.DisableDevice(d);
            QaBanner.Show($"QA 자동 시험 중 · 사람 플레이 아님 · 첫 화면 검수({mode})");
            // 2026-11-20 사용자 관찰 "QA 창이 닫히지 않음" 수정: 격리 fixture(저장 실패 등)의 미저장 확인창이 OS 창 닫기·자동 종료를 막고,
            // 실제 키보드·마우스는 꺼져 있어 확인창도 누를 수 없었다. QA 프로세스에서만 종료 가드를 통과시킨다(사용자 저장과 무관한 격리 폴더).
            QuitGuard.AllowNextQuit();
        }

        void MouseState(Vector2 pos, bool down)
        {
            var st = new MouseState { position = pos };
            if (down) st = st.WithButton(MouseButton.Left, true);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, st);
        }

        IEnumerator Click(Vector2 pos, int holdFrames = 2)
        {
            MouseState(pos, false); yield return null; yield return null;
            MouseState(pos, true); for (int i = 0; i < holdFrames; i++) yield return null;
            MouseState(pos, false); yield return null; yield return null; yield return null;
        }

        IEnumerator Press(Key k, int holdFrames = 2)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState(k)); for (int i = 0; i < holdFrames; i++) yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null; yield return null;
        }

        // ───────── 시나리오 ─────────

        IEnumerator Run()
        {
            Directory.CreateDirectory(dir);
            start = Time.realtimeSinceStartup;
            SetupDevices();
            TitleView title = null;
            for (int i = 0; i < 300 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return null; }
            if (title == null) { Check("title_shown", false, "TitleView 없음"); Finish(); Application.Quit(); yield break; }
            var scene = title.Scene;
            Log($"mode={mode} screen={Screen.width}x{Screen.height} layout={scene?.Layout.SourceName} status={scene?.Layout.Status} fit={scene?.Layout.FitMode}");
            Check("cafe_scene_built", scene != null && scene.Hotspots.Count == 8, $"hotspots={scene?.Hotspots.Count}");
            if (scene == null) { Finish(); Application.Quit(); yield break; }

            if (mode == "low") { yield return LowEffects(title); Finish(); yield break; }
            if (mode == "normalpath" || mode == "normalpath-click") { yield return NormalPath(title, mode == "normalpath-click"); Finish(); Application.Quit(); yield break; }

            // 1) 외관 입장 대기: 자동 입장 없음, 간판에 게임 이름
            var intro = title.Intro;
            Check("first_entry_full", intro.PlayedFull, $"duration={intro.Duration:0.00}s");
            Check("exterior_waits_for_input", intro.IsWaiting && intro.BlockingInput, $"waiting={intro.IsWaiting}");
            var sign = scene.SignTitle;
            Check("sign_shows_game_name", sign != null && sign.text == GameInfo.Title && sign.gameObject.activeInHierarchy, $"sign='{sign?.text}'");
            var signR = sign != null ? WorldRect(sign.rectTransform) : Rect.zero;
            Check("sign_text_on_screen", sign != null && OnScreen(signR), Fmt(signR));
            if (intro.PromptPrimary != null)
            {
                var pr = WorldRect((RectTransform)intro.PromptPrimary.transform.parent);
                var door = WorldRect((RectTransform)intro.EnterButton.transform);
                Check("entrance_prompt_beside_door", intro.PromptPrimary.gameObject.activeInHierarchy && OnScreen(pr) && !pr.Overlaps(signR) && !pr.Overlaps(door)
                    && intro.PromptPrimary.text == "입장하기" && intro.PromptSecondary.text == "아무 키나 눌러 입장", $"prompt={Fmt(pr)} door={Fmt(door)}");
            }
            yield return Wait(3.2f);
            Check("no_auto_entry_after_3s", intro.IsWaiting && scene.InteriorAlpha < 0.01f, $"waiting={intro.IsWaiting} interior={scene.InteriorAlpha:0.00}");
            yield return Shot("exterior_waiting");

            // 2) 대기 중: 다른 곳 클릭 무시, 이미 눌려 있던 키 무시(떼어도 시작 안 함), 새 키로 시작
            yield return Click(new Vector2(Screen.width * 0.06f, Screen.height * 0.5f));
            Check("waiting_ignores_click_off_door", intro.IsWaiting, "문/입장하기 밖 클릭");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space));
            yield return null; yield return null;
            intro.ReplayForQa(); // 키가 눌린 채로 대기 시작(실행 직전·포커스 복귀 키 상황)
            yield return Wait(0.5f);
            bool heldIgnored = intro.IsWaiting;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Wait(0.3f);
            Check("held_key_from_before_ignored", heldIgnored && intro.IsWaiting, $"heldWaiting={heldIgnored} afterRelease={intro.IsWaiting}");

            // 3) 입장 연속 프레임(새 키로 시작 → 시각 고정 캡처)
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Enter)); yield return null; yield return null;
            bool startedByKey = !intro.IsWaiting;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null;
            Check("new_key_starts_entry", startedByKey, $"waiting={intro.IsWaiting}");
            var times = new List<float> { 0.36f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 0.95f, 1.02f };
            float total = intro.Duration;
            for (float tt = 1.1f; tt < total; tt += 0.14f) times.Add(tt);
            times.Add(total);
            foreach (var tt in times)
            {
                intro.SeekForQa(tt);
                yield return null;
                yield return Shot($"intro_t{tt:0.00}_{intro.Phase}");
            }
            intro.QaFrozen = false;

            // 4) 실제 재생: 문 클릭으로 시작 → 끝까지(시간 측정), 시작 클릭이 메뉴로 이어지지 않음
            intro.ReplayForQa();
            yield return Wait(0.3f);
            var doorCenter = ScreenCenter((RectTransform)intro.EnterButton.transform);
            yield return Click(doorCenter);
            float t0 = Time.realtimeSinceStartup;
            Check("door_click_starts_entry", !intro.IsWaiting, "문(입장하기) 클릭");
            while (!intro.Finished && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Check("entrance_plays_to_interior", intro.Finished, $"real {Time.realtimeSinceStartup - t0:0.00}s after input (아트 timing 문 열림+bridge)");
            yield return Wait(0.3f);
            bool notStarted = FindFirstObjectByType<CharacterSelectView>() == null && title.IsOpen;
            Check("entry_click_no_clickthrough", notStarted, "입장 시작 클릭이 메뉴를 누르지 않음");
            var startCenter = scene.ScreenRectOf("start").center;
            yield return Shot("interior_arrival");

            // 4b) 문 옆 명패(입장하기) 클릭도 같은 입장(터치/클릭 대체 경로)
            if (intro.PromptButton != null)
            {
                intro.ReplayForQa();
                yield return Wait(0.3f);
                yield return Click(ScreenCenter((RectTransform)intro.PromptButton.transform));
                Check("prompt_plate_click_starts_entry", !intro.IsWaiting, "명패 클릭");
                yield return Wait(0.2f);
                intro.Skip();
                yield return Wait(0.3f);
            }

            // 5) 입장 중 건너뛰기: 시작 입력이 아닌 새 입력만, 그 입력은 메뉴를 누르지 않음(시작 물체 위 클릭)
            intro.ReplayForQa();
            yield return Wait(0.3f);
            yield return Click(doorCenter);
            yield return Wait(0.25f);
            yield return Click(startCenter, 4);
            yield return Wait(0.3f);
            bool stillTitle = FindFirstObjectByType<TitleView>() == title && title.IsOpen && FindFirstObjectByType<CharacterSelectView>() == null;
            Check("skip_click_consumed", intro.Finished && stillTitle, $"finished={intro.Finished} titleOpen={stillTitle}");
            intro.ReplayForQa();
            yield return Wait(0.3f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Enter));
            for (int f = 0; f < 6; f++) yield return null;
            bool heldNoSkip = !intro.Finished && !intro.IsWaiting;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState()); yield return null; yield return null;
            yield return Press(Key.Enter, 4);
            yield return Wait(0.3f);
            stillTitle = title != null && title.IsOpen && FindFirstObjectByType<CharacterSelectView>() == null;
            Check("start_key_held_does_not_skip", heldNoSkip, "입장 시작 키를 누르고 있는 동안 건너뛰지 않음");
            Check("skip_key_consumed", intro.Finished && stillTitle, $"finished={intro.Finished} titleOpen={stillTitle}");
            EventSystem.current?.SetSelectedGameObject(null);

            if (mode == "future") { yield return Future(title); Finish(); Application.Quit(); yield break; }
            if (mode == "savefail") { yield return SaveFail(title); Finish(); Application.Quit(); yield break; }

            // 3) 각 물체: 그려진 위치 = 클릭 결과, 열기/닫기, 닫은 뒤 입장 반복 없음
            var openers = new Dictionary<string, Func<bool>>
            {
                { "permanent", () => title.ShopView.IsOpen }, { "codex", () => title.CodexView.IsOpen },
                { "skins", () => title.SkinShopView.IsOpen }, { "history", () => title.HistoryView.IsOpen },
                { "settings", () => title.SettingsView.IsOpen }, { "credits", () => title.CreditsView.IsOpen },
            };
            foreach (var id in new[] { "permanent", "codex", "skins", "history", "settings", "credits" })
            {
                var r = scene.ScreenRectOf(id);
                var hit = TopHit(r.center);
                bool hitOk = hit != null && hit.GetComponentInParent<CafeHotspot>() == scene.Hotspots[id];
                bool onScreen = r.xMin >= 0 && r.xMax <= Screen.width && r.yMin >= 0 && r.yMax <= Screen.height;
                Check($"hotspot_{id}_hit_matches_render", hitOk && onScreen, $"rect={Fmt(r)} top={hit?.name}");
                MouseState(r.center, false);
                yield return null; yield return null;
                yield return Shot($"hover_{id}");
                var descRt = scene.Hotspots[id].transform.Find("Focus/Desc") as RectTransform;
                var descR = descRt != null ? WorldRect(descRt) : Rect.zero;
                Check($"hotspot_{id}_hover_focus", scene.Hotspots[id].FocusVisible && (descRt == null || OnScreen(descR)), $"hover 테두리·설명 desc={Fmt(descR)}");
                yield return ApproachOpen(title, id, () => Click(r.center), openers[id]);
                Check($"open_{id}", openers[id]() && !title.IsOpen, $"open={openers[id]()} titleOpen={title.IsOpen}");
                yield return Shot($"menu_{id}");
                yield return Back(title);
                yield return WaitReturned(title, id);
                Check($"back_{id}", title.IsOpen && !openers[id]() && intro.Finished, $"titleOpen={title.IsOpen} introFinished={intro.Finished}");
            }
            // r5 비교 기준: 마우스를 빈 바닥으로 옮긴 평상시 화면(강조 없음)
            MouseState(new Vector2(Screen.width * 0.5f, Screen.height * 0.82f), false);
            yield return Wait(0.35f);
            Check("idle_no_highlight", scene.Hotspots.Values.All(x => !x.FocusVisible && x.LightAmount < 0.001f), "평상시 강조 0");
            yield return Shot("idle_room");
            yield return Shot("back_to_room_no_reentry_door");
            yield return ShelfChecks(title);
            // 명패(이름표): 실제 렌더 위치가 화면 안, 서로 겹치지 않음, 글자가 명패 폭 안
            var plates = scene.Hotspots.ToDictionary(kv => kv.Key, kv => WorldRect(kv.Value.Nameplate));
            var overlaps = new List<string>();
            var keys = plates.Keys.ToList();
            for (int a = 0; a < keys.Count; a++) for (int c = a + 1; c < keys.Count; c++) if (plates[keys[a]].Overlaps(plates[keys[c]])) overlaps.Add(keys[a] + "/" + keys[c]);
            bool fit = scene.Hotspots.Values.All(h => h.LabelText.preferredWidth <= h.Nameplate.sizeDelta.x);
            Check("nameplates_on_screen_no_overlap", plates.Values.All(OnScreen) && overlaps.Count == 0 && fit, $"overlaps=[{string.Join(",", overlaps)}] fit={fit} " + string.Join(" ", plates.Select(kv => kv.Key + Fmt(kv.Value))));

            // 4) 키보드: 첫 키는 기본 항목(시작) 표시, 방향키·Tab 이동, Enter 열기, Esc/뒤로
            EventSystem.current?.SetSelectedGameObject(null);
            yield return Press(Key.RightArrow);
            var seq = new List<string> { Selected() };
            foreach (var k in new[] { Key.RightArrow, Key.RightArrow, Key.UpArrow, Key.LeftArrow, Key.DownArrow })
            { yield return Press(k); seq.Add(Selected()); }
            yield return Wait(0.3f);
            {
                var selHs = EventSystem.current?.currentSelectedGameObject?.GetComponent<CafeHotspot>();
                var litIds = scene.Hotspots.Values.Where(x => x.LightAmount > 0.001f).Select(x => x.Feature.Id).ToList();
                Check("keyboard_focus_lights_only_selected", selHs != null && selHs.LightAmount > 0.99f && litIds.Count == 1 && litIds[0] == selHs.Feature.Id
                    && scene.Hotspots.Values.All(x => x.transform.Find("Focus/PolyEdge") == null && x.transform.Find("Focus/Edge") == null),
                    $"selected={selHs?.Feature.Id} lit=[{string.Join(",", litIds)}]");
            }
            yield return Shot("keyboard_focus");
            var tabSeq = new List<string>();
            for (int i = 0; i < 8; i++) { yield return Press(Key.Tab); tabSeq.Add(Selected()); }
            Check("keyboard_arrows_move", seq[0] == "StartButton" && seq.Distinct().Count() >= 3, string.Join(">", seq));
            Check("keyboard_tab_cycles_all", tabSeq.Distinct().Count() == 8, string.Join(">", tabSeq));
            // Tab으로 도감까지 가서 Enter
            for (int i = 0; i < 9 && Selected() != "CardCodexButton"; i++) yield return Press(Key.Tab);
            yield return Press(Key.Enter);
            yield return WaitOpen(() => title.CodexView.IsOpen);
            Check("keyboard_enter_opens", title.CodexView.IsOpen, $"selected=CardCodexButton open={title.CodexView.IsOpen} approach={title.Approach.LastUsedApproach}");
            yield return Back(title);
            yield return WaitReturned(title, "codex");
            yield return ApproachEdgeCases(title);
            yield return EscapeCloses(title);

            // 5) 시작 → 직업 선택, 타이틀 재진입은 짧은 실내 페이드(문 입장 반복 없음)
            var sr = scene.ScreenRectOf("start");
            Check("hotspot_start_hit_matches_render", TopHit(sr.center)?.GetComponentInParent<CafeHotspot>() == scene.Hotspots["start"], Fmt(sr));
            Check("hotspot_quit_hit_matches_render", TopHit(scene.ScreenRectOf("quit").center)?.GetComponentInParent<CafeHotspot>() == scene.Hotspots["quit"], Fmt(scene.ScreenRectOf("quit")));
            var quitCenter = scene.ScreenRectOf("quit").center;
            yield return Click(sr.center);
            yield return Wait(0.2f);
            Check("start_counter_approach_before_select", title != null && title.Approach.Busy && FindFirstObjectByType<CharacterSelectView>() == null, "카운터 영업 준비 연출 중에는 아직 직업 선택 아님");
            yield return Shot("approach_start_mid");
            yield return WaitOpen(() => FindFirstObjectByType<CharacterSelectView>() != null);
            var select = FindFirstObjectByType<CharacterSelectView>();
            Check("start_opens_select", select != null && FindFirstObjectByType<TitleView>() == null, $"select={select != null}");
            yield return Shot("start_to_select");
            yield return SelectBackToTitle(select);
            var t2 = FindFirstObjectByType<TitleView>();
            Check("reentry_short_no_door", t2 != null && !t2.Intro.PlayedFull && !t2.Intro.IsWaiting && Mathf.Approximately(t2.Intro.Duration, TitleIntro.ShortSeconds), $"full={t2?.Intro.PlayedFull} waiting={t2?.Intro.IsWaiting} dur={t2?.Intro.Duration:0.00}");
            yield return Wait(0.6f);
            yield return Shot("reentry_room");

            // 6) 종료(출입문): 정상 저장이면 확인 없이 종료 — 결과를 먼저 쓰고 실제로 누른다
            Finish();
            yield return Click(t2.Scene.ScreenRectOf("quit").center);
        }

        /// <summary>사용자 관찰 재현: 입장 입력 1회(키 또는 문 클릭) 뒤 추가 Enter/Space·무입력 5초 — 한 판 시작·직업 선택·미리 선택이 없어야 한다.</summary>
        IEnumerator NormalPath(TitleView title, bool click)
        {
            var intro = title.Intro;
            var scene = title.Scene;
            yield return Wait(3f);
            Check("np_waiting_no_auto", intro.IsWaiting, "3초 무입력 대기");
            yield return Shot("np_00_waiting");
            if (click) yield return Click(ScreenCenter((RectTransform)intro.EnterButton.transform));
            else yield return Press(Key.Enter);
            Check("np_single_input_starts_entry", !intro.IsWaiting, click ? "문 클릭 1회" : "Enter 1회");
            float t0 = Time.realtimeSinceStartup;
            while (!intro.Finished && Time.realtimeSinceStartup - t0 < 6f) yield return null;
            yield return Wait(0.2f);
            yield return Shot("np_01_arrived");
            // 도착 직후 성급한 추가 입력(Enter·Space 또는 빈 곳 클릭) — 시작이 눌리면 안 된다
            if (click) { yield return Click(new Vector2(Screen.width * 0.5f, Screen.height * 0.08f)); }
            yield return Press(Key.Enter);
            yield return Press(Key.Space);
            float idle = Time.realtimeSinceStartup;
            int frames = 0;
            bool everStarted = false;
            while (Time.realtimeSinceStartup - idle < 5f)
            {
                if (FindFirstObjectByType<CharacterSelectView>() != null || FindFirstObjectByType<GameManager>() != null) everStarted = true;
                frames++;
                yield return null;
            }
            yield return Shot("np_02_after5s");
            var sel = EventSystem.current?.currentSelectedGameObject;
            Check("np_interior_menu_stays", !everStarted && title != null && title.IsOpen && intro.Finished, $"started={everStarted} titleOpen={title != null && title.IsOpen} frames={frames}");
            Check("np_no_preselect_after_enter_space", sel == null, $"selected={(sel != null ? sel.name : "(none)")}");
            yield return Press(Key.RightArrow);
            yield return Wait(0.3f);
            sel = EventSystem.current?.currentSelectedGameObject;
            Check("np_arrow_reveals_start_only", sel != null && sel.name == "StartButton" && FindFirstObjectByType<CharacterSelectView>() == null, $"selected={(sel != null ? sel.name : "(none)")}");
            yield return Shot("np_03_arrow_reveal");
            yield return Wait(5f);
            Check("np_still_no_start_after_reveal", FindFirstObjectByType<CharacterSelectView>() == null && FindFirstObjectByType<GameManager>() == null, "표시 후 5초 무입력");
        }

        IEnumerator LowEffects(TitleView title)
        {
            var intro = title.Intro;
            Check("low_effects_calm_entry", intro.Calm && !intro.PlayedFull && Mathf.Approximately(intro.Duration, TitleIntro.CalmSeconds), $"calm={intro.Calm} full={intro.PlayedFull} dur={intro.Duration:0.00}");
            yield return Wait(1.5f);
            Check("low_effects_still_waits", intro.IsWaiting, "저이펙트도 외관 입장 대기 유지");
            yield return Shot("low_exterior_waiting");
            yield return Press(Key.Enter);
            Check("low_new_key_starts", !intro.IsWaiting, "");
            var scales = new List<float>();
            foreach (var tt in new[] { 0f, 0.2f, 0.4f, 0.55f, 0.7f })
            {
                intro.SeekForQa(tt); yield return null;
                scales.Add(title.Scene.Exterior.localScale.x / title.Scene.FitScale());
                yield return Shot($"low_t{tt:0.00}_{intro.Phase}");
            }
            Check("low_effects_no_zoom", scales.All(s => Mathf.Abs(s - 1f) < 0.001f), string.Join(",", scales.Select(s => s.ToString("0.000"))));
            intro.QaFrozen = false;
            yield return Wait(1f);
            yield return Shot("low_room");
            {
                var scene = title.Scene;
                var cr = scene.ScreenRectOf("credits").center;
                float maxZoom = 1f;
                yield return Click(cr);
                float t0 = Time.realtimeSinceStartup;
                while (!title.CreditsView.IsOpen && Time.realtimeSinceStartup - t0 < 4f) { scene.GetInteriorCamera(out _, out var z); maxZoom = Mathf.Max(maxZoom, z); yield return null; }
                Check("low_menu_no_camera_move_opens", title.CreditsView.IsOpen && Mathf.Approximately(maxZoom, 1f) && title.Approach.LastUsedApproach, $"open={title.CreditsView.IsOpen} maxZoom={maxZoom:0.00} took={Time.realtimeSinceStartup - t0:0.00}s");
                yield return Back(title);
                yield return WaitReturned(title, "credits");
                Check("low_menu_back_idle", !title.Approach.Busy && title.IsOpen, "");
                // 저이펙트 라디오: 이동 없이 마지막 조작 프레임만(실제 화면 캡처)
                var radio = scene.Interior.Find("radio_sideboard").GetComponent<Image>();
                var last = scene.Layout.FindFeature("settings").Approach.Frames;
                yield return Click(scene.ScreenRectOf("settings").center);
                bool lastShown = false; float t1 = Time.realtimeSinceStartup;
                while (!title.SettingsView.IsOpen && Time.realtimeSinceStartup - t1 < 4f)
                {
                    if (!lastShown && radio.sprite != null && radio.sprite.name == System.IO.Path.GetFileNameWithoutExtension(last[last.Length - 1])) { lastShown = true; yield return Shot("low_radio_final_state"); continue; }
                    yield return null;
                }
                Check("low_radio_final_frame_only", lastShown && title.SettingsView.IsOpen, $"lastShown={lastShown}");
                yield return Back(title);
                yield return WaitReturned(title, "settings");
            }
            Application.Quit();
        }

        IEnumerator Future(TitleView title)
        {
            Check("future_protected", PermanentProgress.IsFutureSaveProtected && title.GuestButtonVisible, $"protected={PermanentProgress.IsFutureSaveProtected} guest={title.GuestButtonVisible}");
            yield return Shot("future_protected_room");
            yield return Click(title.Scene.ScreenRectOf("start").center);
            yield return WaitIdle(title);
            yield return Wait(0.3f);
            title.Scene.GetInteriorCamera(out var fc, out var fz);
            Check("future_start_returns_to_room", !title.Approach.Busy && Mathf.Approximately(fz, 1f), $"zoom={fz:0.00}");
            Check("future_start_blocked_guides_guest", FindFirstObjectByType<CharacterSelectView>() == null && title.SaveNoticeText.Contains(TitleView.NoticeChooseGuestFirst), title.SaveNoticeText.Replace("\n", " "));
            yield return Shot("future_start_blocked");
            var guest = title.GetComponentsInChildren<Button>(true).First(b => b.name == "GuestSessionButton");
            yield return Click(ScreenCenter((RectTransform)guest.transform));
            yield return Wait(0.3f);
            Check("guest_session_chosen", PermanentProgress.IsGuestSession && !title.GuestButtonVisible, $"guest={PermanentProgress.IsGuestSession}");
            yield return Shot("guest_session");
        }

        IEnumerator SaveFail(TitleView title)
        {
            PermanentProgress.AddSpecialty(1); // 격리 fixture: save.json 자리가 폴더라 실제 쓰기가 실패한다
            PermanentProgress.RetrySave();
            yield return Wait(0.3f);
            Check("save_failed_retry_visible", PermanentProgress.LastSaveFailed && title.RetryButtonVisible, $"failed={PermanentProgress.LastSaveFailed} retry={title.RetryButtonVisible}");
            yield return Shot("savefail_room");
            var retry = title.GetComponentsInChildren<Button>(true).First(b => b.name == "RetrySaveButton");
            yield return Click(ScreenCenter((RectTransform)retry.transform));
            yield return Wait(0.3f);
            Check("retry_still_failed_kept", PermanentProgress.LastSaveFailed && title.RetryButtonVisible, "재시도도 실패하면 버튼 유지");
            yield return Click(title.Scene.ScreenRectOf("quit").center);
            yield return Wait(0.2f);
            bool approaching = title.Approach.Busy && !title.transform.Find("Root/QuitConfirm").gameObject.activeSelf;
            Check("quit_door_approach_before_confirm", approaching, "문 쪽 접근·문 조작 동안은 종료/확인창 없음");
            yield return WaitOpen(() => title.transform.Find("Root/QuitConfirm").gameObject.activeSelf);
            var confirm = title.transform.Find("Root/QuitConfirm");
            Check("quit_door_asks_when_unsaved", confirm != null && confirm.gameObject.activeSelf, "출입문(종료) → 저장 실패 확인창");
            yield return Shot("savefail_quit_confirm");
            var cancel = title.GetComponentsInChildren<Button>(true).First(b => b.name == "Cancel");
            yield return Click(ScreenCenter((RectTransform)cancel.transform));
            yield return Wait(0.2f);
            yield return WaitIdle(title);
            title.Scene.GetInteriorCamera(out var qc, out var qz);
            Check("quit_confirm_cancel", !confirm.gameObject.activeSelf && title.IsOpen && !title.Approach.Busy && Mathf.Approximately(qz, 1f), $"취소하면 문에서 물러나 카페로 zoom={qz:0.00}");
            yield return Shot("savefail_quit_cancel_returned");
        }

        /// <summary>2026-11-20 v6 도감 책장: 세 선반 높이마다 polygon 안쪽 점이 실제 도감 클릭 영역에 맞고(hover 표시), 아래 선반 클릭으로 열림.
        /// 의상장(스킨) polygon 안쪽 점은 스킨에 맞아 이웃 경계가 섞이지 않는다.</summary>
        IEnumerator ShelfChecks(TitleView title)
        {
            var scene = title.Scene;
            var codex = scene.Hotspots["codex"]; var skins = scene.Hotspots["skins"];
            var labels = new[] { "top", "mid", "low" };
            var ks = new[] { 0.2f, 0.5f, 0.8f };
            Vector2 lowPt = Vector2.zero;
            for (int i = 0; i < 3; i++)
            {
                var n = InsideAt(codex.Feature, ks[i]);
                var sp = NormToScreen(scene, n);
                var hit = TopHit(sp)?.GetComponentInParent<CafeHotspot>();
                MouseState(sp, false); yield return null; yield return null;
                Check($"codex_shelf_{labels[i]}_hit_hover", hit == codex && codex.FocusVisible, $"norm=({n.x:0.000},{n.y:0.000}) screen=({sp.x:0},{sp.y:0}) hit={hit?.name}");
                yield return Shot($"codex_shelf_{labels[i]}_hover");
                if (i == 2) lowPt = sp;
            }
            // r5: 도형 테두리 없음, 도감 hover 중 도감 물체만 조명 반응(다른 물체 0), 명패 강조는 도감만
            {
                bool noOutline = codex.transform.Find("Focus/PolyEdge") == null && codex.transform.Find("Focus/Edge") == null;
                var others = scene.Hotspots.Values.Where(x => x != codex && x.LightAmount > 0.001f).Select(x => x.Feature.Id).ToList();
                yield return Wait(0.3f);
                Check("codex_object_light_only", noOutline && codex.ObjectLight != null && codex.LightAmount > 0.99f && others.Count == 0,
                    $"noOutline={noOutline} light={codex.LightAmount:0.00} layer={codex.ObjectLayerId} othersLit=[{string.Join(",", others)}]");
                yield return Shot("codex_hover_settled_a");
                yield return Wait(0.3f);
                yield return Shot("codex_hover_settled_b"); // 깜빡임 비교용(같은 상태 두 장)
            }
            int skinOk = 0, skinN = 0;
            foreach (var k in new[] { 0.25f, 0.5f, 0.75f })
            {
                var sp = NormToScreen(scene, InsideAt(skins.Feature, k));
                skinN++; if (TopHit(sp)?.GetComponentInParent<CafeHotspot>() == skins) skinOk++;
            }
            Check("skins_neighbor_hits_skins", skinOk == skinN, $"{skinOk}/{skinN}");
            var skinPt = NormToScreen(scene, InsideAt(skins.Feature, 0.5f));
            MouseState(skinPt, false); yield return null; yield return null;
            yield return Wait(0.3f);
            Check("skins_hover_only_skins_focus", skins.FocusVisible && !codex.FocusVisible && skins.LightAmount > 0.99f && codex.LightAmount < 0.001f, $"skins={skins.FocusVisible}/{skins.LightAmount:0.00} codex={codex.FocusVisible}/{codex.LightAmount:0.00}");
            yield return Shot("skins_hover_neighbor");
            yield return Click(lowPt);
            yield return WaitOpen(() => title.CodexView.IsOpen);
            Check("codex_low_shelf_click_opens", title.CodexView.IsOpen, $"open={title.CodexView.IsOpen}");
            yield return Back(title);
            yield return WaitReturned(title, "codex");
        }

        /// <summary>polygon 세로 비율 k 높이에서 polygon 안쪽 가로 구간의 가운데(정규화 좌상단 원점).</summary>
        static Vector2 InsideAt(CafeTitleLayout.Feature f, float k)
        {
            var r = f.HotspotRect; float y = Mathf.Lerp(r.yMin, r.yMax, k);
            float x0 = float.NaN, x1 = float.NaN;
            for (int i = 0; i <= 200; i++)
            {
                float x = Mathf.Lerp(r.xMin, r.xMax, i / 200f);
                bool inside = f.Polygon != null ? CafeHotspot.PointInPolygon(new Vector2(x, y), f.Polygon) : r.Contains(new Vector2(x, y));
                if (inside) { if (float.IsNaN(x0)) x0 = x; x1 = x; }
            }
            return float.IsNaN(x0) ? r.center : new Vector2((x0 + x1) * 0.5f, y);
        }

        static Vector2 NormToScreen(CafeTitleScene scene, Vector2 n) => scene.Interior.TransformPoint(CafeTitleScene.NormToLocal(n));

        // ───────── 메뉴 접근 연출 도우미(2026-11-20 v9) ─────────

        IEnumerator WaitOpen(Func<bool> open, float limit = 4f)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!open() && Time.realtimeSinceStartup - t0 < limit) yield return null;
            yield return Wait(0.2f);
        }

        IEnumerator WaitIdle(TitleView title, float limit = 4f)
        {
            float t0 = Time.realtimeSinceStartup;
            while (title != null && title.Approach != null && title.Approach.Busy && Time.realtimeSinceStartup - t0 < limit) yield return null;
        }

        /// <summary>확정 → 접근(시점 이동 중간 캡처) → 조작 프레임(중간 캡처, 물체 그림 교체 확인) → 메뉴 열림. 시점 목표·중복 확정 차단 검사.</summary>
        IEnumerator ApproachOpen(TitleView title, string id, Func<IEnumerator> confirm, Func<bool> open)
        {
            var scene = title.Scene;
            var ap = scene.Layout.FindFeature(id).Approach;
            var layer = ap != null ? scene.Interior.Find(ap.Layer)?.GetComponent<Image>() : null;
            var original = layer != null ? layer.sprite : null;
            yield return confirm();
            bool started = title.Approach.Busy && title.Approach.LastUsedApproach && !open();
            Check($"approach_{id}_started", ap != null && started, $"busy={title.Approach.Busy} contract={ap != null}");
            bool shotMove = false, shotAct = false, frameChanged = false; float maxZoom = 1f;
            Sprite lastSprite = original;
            float t0 = Time.realtimeSinceStartup;
            while (!open() && Time.realtimeSinceStartup - t0 < 4f)
            {
                scene.GetInteriorCamera(out _, out var z); maxZoom = Mathf.Max(maxZoom, z);
                if (!shotMove && title.Approach.Current == CafeMenuApproach.State.Approaching && z > 1.2f) { shotMove = true; yield return Shot($"approach_{id}_moving"); continue; }
                if (title.Approach.Current == CafeMenuApproach.State.Acting && layer != null && layer.sprite != original) frameChanged = true;
                if (id == "settings" && layer != null && layer.sprite != lastSprite && title.Approach.Current == CafeMenuApproach.State.Acting)
                { lastSprite = layer.sprite; yield return Shot($"radio_forward_{layer.sprite.name}"); continue; } // 라디오 연속 프레임(실제 화면)
                if (!shotAct && frameChanged) { shotAct = true; yield return Shot($"approach_{id}_acting"); continue; }
                yield return null;
            }
            yield return Wait(0.2f);
            Check($"approach_{id}_object_action", ap != null && Mathf.Abs(maxZoom - ap.Zoom) < 0.01f && frameChanged && open(),
                $"targetZoom={ap?.Zoom:0.00} reached={maxZoom:0.00} frameChanged={frameChanged} took={Time.realtimeSinceStartup - t0:0.00}s");
        }

        /// <summary>메뉴 닫기 → 역재생·원시점 복귀 → 입력 재개(물체 그림 원복, 명패 보임).</summary>
        IEnumerator WaitReturned(TitleView title, string id)
        {
            var scene = title.Scene;
            var ap = scene.Layout.FindFeature(id).Approach;
            var layer = ap != null ? scene.Interior.Find(ap.Layer)?.GetComponent<Image>() : null;
            bool shot = false;
            Sprite lastRev = layer != null ? layer.sprite : null;
            float t0 = Time.realtimeSinceStartup;
            while (title.Approach.Busy && Time.realtimeSinceStartup - t0 < 4f)
            {
                if (id == "settings" && layer != null && layer.sprite != lastRev && title.Approach.Current == CafeMenuApproach.State.Returning)
                { lastRev = layer.sprite; yield return Shot($"radio_reverse_{layer.sprite.name}"); continue; }
                if (!shot && title.Approach.Current == CafeMenuApproach.State.Returning) { shot = true; yield return Shot($"return_{id}_moving"); continue; }
                yield return null;
            }
            yield return Wait(0.2f);
            scene.GetInteriorCamera(out var c, out var z);
            bool restored = layer == null || ap.Frames.Length == 0 || layer.sprite == CafeTitleLayout.LoadSprite(scene.Layout.InteriorLayers.First(l => l.Id == ap.Layer).Png);
            Check($"return_{id}_to_room", !title.Approach.Busy && Mathf.Approximately(z, 1f) && restored && scene.Hotspots[id].Nameplate.gameObject.activeSelf && scene.InteriorInteractable,
                $"zoom={z:0.00} restored={restored} took={Time.realtimeSinceStartup - t0:0.00}s");
        }

        /// <summary>중복·다른 메뉴 확정 차단, 새 입력 건너뛰기(입력 누출 없음), Esc 취소(메뉴 미열림·원위치), 메뉴 화면 나무·종이 톤.</summary>
        IEnumerator ApproachEdgeCases(TitleView title)
        {
            var scene = title.Scene;
            // 중복·다른 메뉴: 영구 강화 확정 직후 도감 클릭 → 영구 강화만 열림
            yield return Click(scene.ScreenRectOf("permanent").center);
            yield return Click(scene.ScreenRectOf("codex").center);
            yield return Click(scene.ScreenRectOf("permanent").center);
            yield return WaitOpen(() => title.ShopView.IsOpen);
            Check("approach_blocks_other_menu_and_repeat", title.ShopView.IsOpen && !title.CodexView.IsOpen, $"shop={title.ShopView.IsOpen} codex={title.CodexView.IsOpen}");
            var theme = title.GetComponentsInChildren<CafeMenuTheme>(true).Length;
            Check("menu_screens_have_cafe_theme", theme >= 6, $"themed roots={theme}");
            yield return Back(title);
            yield return WaitReturned(title, "permanent");
            // 건너뛰기: 기록 확정 → 손을 뗀 뒤 Space → 즉시 끝 상태 → 손 뗀 뒤 메뉴(Space가 메뉴를 누르지 않음)
            yield return Click(scene.ScreenRectOf("history").center);
            yield return Wait(0.08f);
            float t0 = Time.realtimeSinceStartup;
            yield return Press(Key.Space, 3);
            yield return WaitOpen(() => title.HistoryView.IsOpen);
            bool stillHistory = title.HistoryView.IsOpen && !title.IsOpen;
            Check("approach_skip_new_input_no_leak", title.Approach.LastSkipped && stillHistory, $"skipped={title.Approach.LastSkipped} historyOpen={title.HistoryView.IsOpen} took={Time.realtimeSinceStartup - t0:0.00}s");
            yield return Shot("approach_history_skipped_menu");
            yield return Back(title);
            yield return WaitReturned(title, "history");
            // 취소: 설정 확정 → Esc → 메뉴 미열림, 원위치
            yield return Click(scene.ScreenRectOf("settings").center);
            yield return Wait(0.08f);
            yield return Press(Key.Escape, 2);
            yield return WaitIdle(title);
            yield return Wait(0.2f);
            scene.GetInteriorCamera(out _, out var z);
            Check("approach_escape_cancels", title.Approach.LastCancelled && !title.SettingsView.IsOpen && title.IsOpen && Mathf.Approximately(z, 1f), $"cancelled={title.Approach.LastCancelled} settings={title.SettingsView.IsOpen} zoom={z:0.00}");
            yield return Shot("approach_settings_cancelled_room");
        }

        /// <summary>2026-11-21 데스크톱 Esc: 설정·제작 정보·도감(상세 포함)·스킨이 Esc로 닫히고 역재생·원시점으로 돌아온다(표기 "· Esc"와 일치).</summary>
        IEnumerator EscapeCloses(TitleView title)
        {
            var scene = title.Scene;
            var openers = new Dictionary<string, Func<bool>>
            {
                { "settings", () => title.SettingsView.IsOpen }, { "credits", () => title.CreditsView.IsOpen },
                { "codex", () => title.CodexView.IsOpen }, { "skins", () => title.SkinShopView.IsOpen },
            };
            foreach (var id in new[] { "settings", "credits", "codex", "skins" })
            {
                yield return Click(scene.ScreenRectOf(id).center);
                yield return WaitOpen(openers[id]);
                var labels = title.GetComponentsInChildren<Button>(false).Where(b => (b.name == "BackButton" || b.name == "CloseButton") && b.gameObject.activeInHierarchy)
                    .Select(b => b.GetComponentInChildren<Text>()?.text ?? "").ToList();
                Check($"esc_{id}_label", labels.Any(s => s.Contains("Esc")), $"labels=[{string.Join("|", labels)}]");
                if (id == "codex")
                {
                    // 상세 위 Esc: 상세만 닫힘(도감 유지)
                    var row = title.CodexView.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name.StartsWith("Row_"));
                    if (row != null) row.onClick.Invoke();
                    bool opened = title.CodexView.IsDetailOpen;
                    if (opened)
                    {
                        yield return Wait(0.2f);
                        yield return Shot("esc_codex_detail_open");
                        yield return Press(Key.Escape, 2);
                        yield return Wait(0.2f);
                        Check("esc_codex_detail_first", !title.CodexView.IsDetailOpen && title.CodexView.IsOpen, $"detail={title.CodexView.IsDetailOpen} codex={title.CodexView.IsOpen}");
                    }
                    else Check("esc_codex_detail_first", false, $"상세를 열 행 없음 row={row?.name}");
                }
                yield return Shot($"esc_{id}_menu");
                yield return Press(Key.Escape, 2);
                yield return null;
                bool closed = !openers[id]() && title.IsOpen;
                yield return WaitReturned(title, id);
                Check($"esc_{id}_closes_and_returns", closed && !openers[id]() && title.IsOpen && !title.Approach.Busy, $"closedSameFrame={closed} open={openers[id]()} titleOpen={title.IsOpen}");
                yield return Shot($"esc_{id}_returned");
            }
        }

        /// <summary>2026-11-21 직업 선택 → 타이틀: 무기 단계 Esc = 직업 단계, 직업 단계 Esc = 카페 실내(짧은 재진입, 미리 선택 없음).
        /// 화면 버튼("카페로 · Esc")은 무기 단계 "뒤로"와 같은 자리, 다른 버튼과 겹치지 않음.</summary>
        IEnumerator SelectBackToTitle(CharacterSelectView select)
        {
            if (select == null) { Check("select_back_to_title", false, "select 없음"); yield break; }
            yield return Wait(0.3f);
            var back = select.TitleBackButton;
            var br = back != null ? WorldRect((RectTransform)back.transform) : Rect.zero;
            var others = select.GetComponentsInChildren<Button>(false).Where(b => b != back && b.gameObject.activeInHierarchy).Select(b => (b.name, r: WorldRect((RectTransform)b.transform))).ToList();
            var hits = others.Where(o => o.r.Overlaps(br)).Select(o => o.name).ToList();
            string label = back != null ? back.GetComponentInChildren<Text>()?.text : null;
            Check("select_title_back_button", back != null && OnScreen(br) && hits.Count == 0 && label != null && label.Contains("Esc") && TopHit(br.center)?.GetComponentInParent<Button>() == back,
                $"rect={Fmt(br)} overlaps=[{string.Join(",", hits)}] label={label}");
            yield return Shot("select_job_step_back_button");
            // 무기 단계 → Esc → 직업 단계
            yield return Click(ScreenCenter((RectTransform)select.BartenderButton.transform));
            yield return Wait(0.3f);
            bool weapon = select.IsWeaponStep;
            yield return Shot("select_weapon_step");
            yield return Press(Key.Escape, 2);
            yield return Wait(0.3f);
            Check("select_weapon_escape_to_job", weapon && select != null && !select.IsWeaponStep && FindFirstObjectByType<TitleView>() == null, $"wasWeapon={weapon} weaponNow={select != null && select.IsWeaponStep}");
            // 직업 단계 → Esc → 타이틀
            yield return Press(Key.Escape, 2);
            yield return Wait(0.2f);
            var t2 = FindFirstObjectByType<TitleView>();
            Check("select_job_escape_to_title", t2 != null && FindFirstObjectByType<CharacterSelectView>() == null && GameManager.Instance == null, $"title={t2 != null}");
            yield return Wait(0.6f);
            Check("select_back_no_preselect", EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null, $"selected={Selected()}");
            yield return Shot("select_back_room");
        }

        // ───────── 도우미 ─────────

        IEnumerator Back(TitleView title)
        {
            var back = title.GetComponentsInChildren<Button>(false).FirstOrDefault(b => (b.name == "BackButton" || b.name == "CloseButton") && b.gameObject.activeInHierarchy);
            if (back != null) yield return Click(ScreenCenter((RectTransform)back.transform));
            else yield return Press(Key.Escape);
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static bool OnScreen(Rect r) => r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= Screen.width + 0.5f && r.yMax <= Screen.height + 0.5f;

        static Vector2 ScreenCenter(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return (c[0] + c[2]) * 0.5f;
        }

        static GameObject TopHit(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return null;
            var results = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = screen }, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        static string Selected() => EventSystem.current?.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "(none)";
        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        IEnumerator Wait(float s) { float e = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < e) yield return null; }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' ? ch : '_').ToArray());
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{safe}_{Screen.width}x{Screen.height}.png"));
            yield return null; yield return null;
        }

        void Check(string id, bool ok, string detail)
        {
            QaBanner.Step(id);
            if (ok) pass++; else fail++;
            checks.Add($"{{\"id\": \"{id}\", \"status\": \"{(ok ? "PASS" : "FAIL")}\", \"detail\": \"{detail.Replace("\\", "/").Replace("\"", "'")}\"}}");
            Log($"{(ok ? "PASS" : "FAIL")} {id}: {detail}");
        }

        void Log(string s)
        {
            var line = $"[{Time.realtimeSinceStartup - start,6:0.0}s] {s}";
            log.AppendLine(line);
            Debug.Log("[CafeTitleCapture] " + line);
        }

        void Finish()
        {
            QuitGuard.AllowNextQuit();
            // 결과를 쓴 뒤에는 실제 입력 장치를 되살린다(창이 남더라도 사용자가 닫거나 조작할 수 있게)
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices.ToArray())
                if (!d.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(d);
            Log($"done pass={pass} fail={fail}");
            File.WriteAllText(Path.Combine(dir, "results.json"),
                $"{{\n  \"mode\": \"{mode}\", \"screen\": \"{Screen.width}x{Screen.height}\", \"pass\": {pass}, \"fail\": {fail},\n  \"checks\": [\n    {string.Join(",\n    ", checks)}\n  ]\n}}\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
