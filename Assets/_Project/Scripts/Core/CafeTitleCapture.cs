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
            if (title == null) { Check("title_shown", false, "TitleView 없음"); Finish(); yield break; }
            var scene = title.Scene;
            Log($"mode={mode} screen={Screen.width}x{Screen.height} layout={scene?.Layout.SourceName} status={scene?.Layout.Status} fit={scene?.Layout.FitMode}");
            Check("cafe_scene_built", scene != null && scene.Hotspots.Count == 8, $"hotspots={scene?.Hotspots.Count}");
            if (scene == null) { Finish(); yield break; }

            if (mode == "low") { yield return LowEffects(title); Finish(); yield break; }
            if (mode == "normalpath" || mode == "normalpath-click") { yield return NormalPath(title, mode == "normalpath-click"); Finish(); Application.Quit(); yield break; }

            // 1) 외관 입장 대기: 자동 입장 없음, 간판에 게임 이름
            var intro = title.Intro;
            Check("first_entry_full", intro.PlayedFull, $"duration={intro.Duration:0.00}s");
            Check("exterior_waits_for_input", intro.IsWaiting && intro.BlockingInput, $"waiting={intro.IsWaiting}");
            var sign = scene.SignTitle;
            Check("sign_shows_game_name", sign != null && sign.text == GameInfo.Title && sign.gameObject.activeInHierarchy, $"sign='{sign?.text}'");
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
                Check($"hotspot_{id}_hover_focus", scene.Hotspots[id].FocusVisible, "hover 테두리·설명");
                yield return Click(r.center);
                yield return Wait(0.25f);
                Check($"open_{id}", openers[id]() && !title.IsOpen, $"open={openers[id]()} titleOpen={title.IsOpen}");
                yield return Shot($"menu_{id}");
                yield return Back(title);
                yield return Wait(0.25f);
                Check($"back_{id}", title.IsOpen && !openers[id]() && intro.Finished, $"titleOpen={title.IsOpen} introFinished={intro.Finished}");
            }
            yield return Shot("back_to_room_no_reentry_door");

            // 4) 키보드: 첫 키는 기본 항목(시작) 표시, 방향키·Tab 이동, Enter 열기, Esc/뒤로
            EventSystem.current?.SetSelectedGameObject(null);
            yield return Press(Key.RightArrow);
            var seq = new List<string> { Selected() };
            foreach (var k in new[] { Key.RightArrow, Key.RightArrow, Key.UpArrow, Key.LeftArrow, Key.DownArrow })
            { yield return Press(k); seq.Add(Selected()); }
            yield return Shot("keyboard_focus");
            var tabSeq = new List<string>();
            for (int i = 0; i < 8; i++) { yield return Press(Key.Tab); tabSeq.Add(Selected()); }
            Check("keyboard_arrows_move", seq[0] == "StartButton" && seq.Distinct().Count() >= 3, string.Join(">", seq));
            Check("keyboard_tab_cycles_all", tabSeq.Distinct().Count() == 8, string.Join(">", tabSeq));
            // Tab으로 도감까지 가서 Enter
            for (int i = 0; i < 9 && Selected() != "CardCodexButton"; i++) yield return Press(Key.Tab);
            yield return Press(Key.Enter);
            yield return Wait(0.25f);
            Check("keyboard_enter_opens", title.CodexView.IsOpen, $"selected=CardCodexButton open={title.CodexView.IsOpen}");
            yield return Back(title);
            yield return Wait(0.25f);

            // 5) 시작 → 직업 선택, 타이틀 재진입은 짧은 실내 페이드(문 입장 반복 없음)
            var sr = scene.ScreenRectOf("start");
            Check("hotspot_start_hit_matches_render", TopHit(sr.center)?.GetComponentInParent<CafeHotspot>() == scene.Hotspots["start"], Fmt(sr));
            Check("hotspot_quit_hit_matches_render", TopHit(scene.ScreenRectOf("quit").center)?.GetComponentInParent<CafeHotspot>() == scene.Hotspots["quit"], Fmt(scene.ScreenRectOf("quit")));
            var quitCenter = scene.ScreenRectOf("quit").center;
            yield return Click(sr.center);
            yield return Wait(0.4f);
            var select = FindFirstObjectByType<CharacterSelectView>();
            Check("start_opens_select", select != null && FindFirstObjectByType<TitleView>() == null, $"select={select != null}");
            yield return Shot("start_to_select");
            if (select != null) Destroy(select.gameObject);
            yield return null;
            GameBootstrap.ShowTitle();
            yield return null;
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
            Application.Quit();
        }

        IEnumerator Future(TitleView title)
        {
            Check("future_protected", PermanentProgress.IsFutureSaveProtected && title.GuestButtonVisible, $"protected={PermanentProgress.IsFutureSaveProtected} guest={title.GuestButtonVisible}");
            yield return Shot("future_protected_room");
            yield return Click(title.Scene.ScreenRectOf("start").center);
            yield return Wait(0.3f);
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
            yield return Wait(0.3f);
            var confirm = title.transform.Find("Root/QuitConfirm");
            Check("quit_door_asks_when_unsaved", confirm != null && confirm.gameObject.activeSelf, "출입문(종료) → 저장 실패 확인창");
            yield return Shot("savefail_quit_confirm");
            var cancel = title.GetComponentsInChildren<Button>(true).First(b => b.name == "Cancel");
            yield return Click(ScreenCenter((RectTransform)cancel.transform));
            yield return Wait(0.2f);
            Check("quit_confirm_cancel", !confirm.gameObject.activeSelf && title.IsOpen, "취소하면 카페로");
        }

        // ───────── 도우미 ─────────

        IEnumerator Back(TitleView title)
        {
            var back = title.GetComponentsInChildren<Button>(false).FirstOrDefault(b => (b.name == "BackButton" || b.name == "CloseButton") && b.gameObject.activeInHierarchy);
            if (back != null) yield return Click(ScreenCenter((RectTransform)back.transform));
            else yield return Press(Key.Escape);
        }

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
            Log($"done pass={pass} fail={fail}");
            File.WriteAllText(Path.Combine(dir, "results.json"),
                $"{{\n  \"mode\": \"{mode}\", \"screen\": \"{Screen.width}x{Screen.height}\", \"pass\": {pass}, \"fail\": {fail},\n  \"checks\": [\n    {string.Join(",\n    ", checks)}\n  ]\n}}\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
