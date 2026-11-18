using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 08장 "### 타이틀". "계속하기" 버튼은 없음(07:112 등 이번 런 상태를 저장/복원하지 않는 설계와 일치
    /// — G30 참고). 처음 실행도 이 화면을 거친다(GameBootstrap.ShowTitle). "종료"는 08:174~178의
    /// 저장 실패 확인 흐름만 다룬다 — OS 창 닫기(X 버튼) 가로채기는 모든 화면에 걸친 별도 범위라
    /// 이번엔 하지 않음(2026-09-28).
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        Canvas canvas;
        GameObject root;
        GameObject quitConfirmRoot;
        Text quitMessageText;
        Text saveStatusText;
        Text saveNoticeText;
        Image saveOkIcon;
        Image saveErrorIcon;
        GameObject guestButtonRoot;
        GameObject retryButtonRoot;
        /// <summary>미래 버전 보호 중 "시작"을 눌렀을 때의 추가 안내를 유지할지(상태 갱신이 지우지 않게).</summary>
        bool guestGuidanceRequested;
        /// <summary>마지막으로 화면에 반영한 저장 상태(실패/보호/임시 플레이). 바뀔 때만 다시 그린다.</summary>
        int shownSaveState = -1;
        Button quitCancelButton;
        Button startButton;
        /// <summary>2026-11-18 카페 입장형 첫 화면: 메뉴 = 카페 실내 물체(같은 버튼 이름 유지). 배치 계약 layout.json.</summary>
        CafeTitleScene scene;
        Text tempArtBadge;
        public CafeTitleScene Scene => scene;

        PermanentSkillView shopView;
        CardCodexView codexView;
        SettingsView settingsView;
        CreditsView creditsView;
        SkinShopView skinShopView;
        RunHistoryListView historyView;

        public bool IsOpen => root != null && root.activeSelf;
        public PermanentSkillView ShopView => shopView;
        public CardCodexView CodexView => codexView;
        public SettingsView SettingsView => settingsView;
        public CreditsView CreditsView => creditsView;
        public SkinShopView SkinShopView => skinShopView;
        public RunHistoryListView HistoryView => historyView;
        /// <summary>테스트용 — 08:172 미래 버전 "임시 플레이" 버튼 / 08:175 저장 재시도 버튼 표시 여부와 알림 문구.</summary>
        public bool GuestButtonVisible => guestButtonRoot != null && guestButtonRoot.activeSelf;
        public bool RetryButtonVisible => retryButtonRoot != null && retryButtonRoot.activeSelf;
        public string SaveNoticeText => saveNoticeText != null ? saveNoticeText.text : "";

        public static TitleView Show()
        {
            var canvasComp = UiFactory.CreateCanvas(null, "Title", 100);
            var view = canvasComp.gameObject.AddComponent<TitleView>();
            view.canvas = canvasComp;

            view.shopView = PermanentSkillView.Create(canvasComp.transform, view.ShowRoot);
            view.codexView = CardCodexView.Create(canvasComp.transform, view.ShowRoot);
            view.settingsView = SettingsView.Create(canvasComp.transform, view.ShowRoot);
            view.creditsView = CreditsView.Create(canvasComp.transform, view.ShowRoot);
            view.skinShopView = SkinShopView.Create(canvasComp.transform, view.ShowRoot);
            view.historyView = RunHistoryListView.Create(canvasComp.transform, view.ShowRoot); // 67 최근 기록

            view.BuildRoot();
            return view;
        }

        void BuildRoot()
        {
            root = UiFactory.Stretch("Root", canvas.transform).gameObject;
            var rootRt = (RectTransform)root.transform;
            UiFactory.AddImage(rootRt, new Color(0.04f, 0.03f, 0.03f, 1f)); // 장면 밖(contain 여백) 배경

            // 기존 8메뉴 동작 그대로 — 카페 실내 물체가 같은 이름의 버튼이 된다.
            var actions = new Dictionary<string, System.Action>
            {
                { "start", () => RequestStart() },
                { "permanent", () => { root.SetActive(false); shopView.Show(); } },
                { "codex", () => { root.SetActive(false); codexView.Show(); } },
                { "skins", () => { root.SetActive(false); skinShopView.Show(); } },
                { "history", OpenHistory },
                { "settings", () => { root.SetActive(false); settingsView.Show(); } },
                { "credits", () => { root.SetActive(false); creditsView.Show(); } },
                { "quit", RequestQuit },
            };
            var layout = CafeTitleLayout.Load();
            if (layout != null && layout.Features.Count >= 8)
            {
                scene = CafeTitleScene.Build(rootRt, layout, actions);
                startButton = scene.Hotspots.TryGetValue("start", out var s) ? s.Button : null;
            }
            if (startButton == null) BuildFallbackMenu(rootRt, actions); // 배치 파일이 없거나 깨졌을 때도 메뉴 기능은 잃지 않는다

            // 제목: 상단 안전 영역에 간결하게(장면 위 Unity 텍스트).
            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(900f, 64f));
            var titleText = UiFactory.AddText(title, GameInfo.Title, 46, TextAnchor.MiddleCenter, new Color(0.98f, 0.92f, 0.78f));
            var sh = title.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f); sh.effectDistance = new Vector2(2f, -3f);
            title.GetComponent<Text>().raycastTarget = false;

            if (layout != null && !layout.IsFinalArt)
            {
                tempArtBadge = UiFactory.AddText(UiFactory.Place("TempArtBadge", rootRt, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(520f, 32f)),
                    $"임시 배치({layout.SourceName}) · 최종 카페 아트 대기", 20, TextAnchor.MiddleRight, new Color(1f, 0.75f, 0.55f, 0.9f));
                tempArtBadge.raycastTarget = false;
            }

            BuildFooter(rootRt);
            BuildQuitConfirm(rootRt);
            RefreshFooter();
            if (scene != null)
            {
                Intro = TitleIntro.Attach(scene, titleText);
                Intro.Completed += SelectDefault; // 입장 연출이 끝난 뒤에 기본 선택 후보를 준다(건너뛴 키가 메뉴를 고르지 않게)
                ApplyInputGate();
            }
            else SelectDefault();
        }

        /// <summary>배치 파일을 못 읽었을 때만: 기존과 같은 세로 메뉴(기능 보존용 안전망).</summary>
        void BuildFallbackMenu(RectTransform rootRt, Dictionary<string, System.Action> actions)
        {
            float y = -120f;
            foreach (var kv in CafeTitleScene.ButtonNames)
            {
                var act = actions[kv.Key];
                var b = BuildMenuButton(rootRt, kv.Value, FallbackLabel(kv.Key), ref y, new Color(0.3f, 0.22f, 0.18f), () => act());
                if (kv.Key == "start") startButton = b;
            }
        }

        static string FallbackLabel(string id) => id switch
        {
            "start" => "시작", "permanent" => "영구 강화", "codex" => "도감", "skins" => "스킨", "history" => "최근 기록",
            "settings" => "설정", "credits" => "제작 정보", _ => "종료",
        };

        /// <summary>입장 연출 중·건너뛴 입력이 떼어지기 전에는 실내 물체와 알림 버튼이 눌리지 않는다.</summary>
        void ApplyInputGate()
        {
            bool block = Intro != null && Intro.BlockingInput;
            if (scene != null) scene.InteriorInteractable = !block;
            foreach (var go in new[] { guestButtonRoot, retryButtonRoot })
                if (go != null) go.GetComponent<Button>().interactable = !block;
        }

        /// <summary>Tab / Shift+Tab: layout nav_order 순서로 실내 물체를 돈다(방향키는 공간 기준 이웃).</summary>
        void HandleTab()
        {
            if (scene == null || !root.activeSelf || quitConfirmRoot.activeSelf || (Intro != null && Intro.BlockingInput)) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            bool arrow = kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            var es = EventSystem.current;
            if (es == null) return;
            if (es.currentSelectedGameObject == null && (arrow || kb.tabKey.wasPressedThisFrame)) { RevealDefaultOnNavigation(); return; }
            if (!kb.tabKey.wasPressedThisFrame) return;
            CafeHotspot cur = null;
            if (es.currentSelectedGameObject != null) cur = es.currentSelectedGameObject.GetComponent<CafeHotspot>();
            var next = scene.NextInOrder(cur, kb.shiftKey.isPressed ? -1 : 1);
            if (next != null) es.SetSelectedGameObject(next.gameObject);
        }

        public TitleIntro Intro { get; private set; }

        /// <summary>G31 — 마우스로 아무것도 안 눌러도 Tab/화살표로 바로 다닐 수 있게 기본 항목을 정해 둔다.
        /// 2026-10-12: 진입 즉시 선택하면 "시작"이 파란 강조로 고정돼 보여, 첫 키보드 입력 때 드러내도록 UiInitialFocus에 맡긴다.</summary>
        void SelectDefault()
        {
            if (Intro != null && !Intro.Finished) return; // 입장 연출이 끝나면(Completed) 다시 불린다
            // 미래 버전 보호 중이면 임시 플레이 선택 버튼을 먼저 가리킨다.
            var target = guestButtonRoot != null && guestButtonRoot.activeSelf ? guestButtonRoot : startButton.gameObject;
            if (scene == null) { UiInitialFocus.Offer(target); return; }
            // 2026-11-18 사용자 관찰 "입장 입력 뒤 추가 입력에 바로 게임 시작" 수정: 카페 실내에서는 키보드 모드여도 아무것도 미리 고르지 않는다.
            // 기본 항목은 방향키/Tab으로만 드러나고, Enter/Space만으로는 시작(카운터)이 눌리지 않는다.
            titlePending = target;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(transform)) es.SetSelectedGameObject(null);
            TitleTrace.Log($"select-default pending={target.name} (no preselect)");
        }

        GameObject titlePending;

        /// <summary>방향키/Tab 첫 입력: 선택이 없으면 기본 항목을 드러내기만 한다(이동·실행 없음). Enter/Space는 드러내지 않는다.</summary>
        public bool RevealDefaultOnNavigation()
        {
            var es = EventSystem.current;
            if (es == null || titlePending == null || es.currentSelectedGameObject != null || !titlePending.activeInHierarchy) return false;
            es.SetSelectedGameObject(titlePending);
            TitleTrace.Log($"reveal-default {titlePending.name}");
            titlePending = null;
            return true;
        }

        Button BuildMenuButton(RectTransform parent, string name, string label, ref float y, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(420f, 76f));
            var bg = UiFactory.AddImage(rt, color);
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => { AudioService.PlaySfx("ui_confirm"); onClick(); });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 32, TextAnchor.MiddleCenter, Color.white);

            y -= 88f;
            return button;
        }

        /// <summary>테스트/버튼 공용 — 최근 기록 목록을 연다.</summary>
        public void OpenHistory()
        {
            root.SetActive(false);
            historyView.Show();
        }

        void BuildFooter(RectTransform parent)
        {
            UiFactory.AddText(UiFactory.Place("Version", parent, new Vector2(0f, 0f), new Vector2(30f, 30f),
                new Vector2(900f, 40f)), $"v{Application.version} · {DrinkLab.MvpLabel}", 22, TextAnchor.LowerLeft,
                new Color(0.6f, 0.6f, 0.6f));

            saveStatusText = UiFactory.AddText(UiFactory.Place("SaveStatus", parent, new Vector2(1f, 0f),
                new Vector2(-30f, 30f), new Vector2(600f, 40f)), "", 22, TextAnchor.LowerRight,
                new Color(1f, 0.85f, 0.7f));
            // 09 ui_save_ok / ui_save_error — 저장 상태 아이콘(RefreshFooter가 바꾼다).
            saveOkIcon = UiFactory.AddIcon(parent, "ui_save_ok", new Vector2(1f, 0f), new Vector2(-640f, 30f), 40f);
            saveErrorIcon = UiFactory.AddIcon(parent, "ui_save_error", new Vector2(1f, 0f), new Vector2(-640f, 30f), 40f);

            // 08:157/162/172/175 로드 알림(백업 복구·손상·미래 버전·중단된 런)과 저장 실패 — 짧은 문구 + 버튼.
            saveNoticeText = UiFactory.AddText(UiFactory.Place("SaveNotice", parent, new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(1400f, 80f)), "", 24, TextAnchor.LowerCenter,
                new Color(1f, 0.85f, 0.7f));
            // 2026-11-18: 카페 바닥 그림 위에서도 읽히게 저장 알림에 어두운 외곽선(장식으로 숨기지 않음)
            var noticeOutline = saveNoticeText.gameObject.AddComponent<Outline>();
            noticeOutline.effectColor = new Color(0f, 0f, 0f, 0.95f); noticeOutline.effectDistance = new Vector2(2f, -2f);
            foreach (var t in new[] { saveStatusText })
            { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2f, -2f); }
            guestButtonRoot = BuildNoticeButton(parent, "GuestSessionButton", "임시 플레이 시작", () =>
            {
                PermanentProgress.AcceptGuestSession();
                RefreshFooter();
                SelectDefault();
            });
            retryButtonRoot = BuildNoticeButton(parent, "RetrySaveButton", "다시 시도", () =>
            {
                PermanentProgress.RetrySave();
                RefreshFooter();
            });
        }

        public const string NoticeChooseGuestFirst = "먼저 \"임시 플레이 시작\"을 눌러 주세요. 기존 파일은 그대로 보존됩니다.";

        /// <summary>"시작" 버튼. 08:172 — 미래 버전 원본 보호 중에는 사용자가 임시 플레이를 명시적으로 고르기 전까지
        /// 런을 시작하지 않고 그 선택으로 안내한다(시작하면 런 시작 저장이 필요하므로). 시작하면 true.</summary>
        public bool RequestStart()
        {
            TitleTrace.Log("RequestStart called", true);
            if (PermanentProgress.IsFutureSaveProtected)
            {
                guestGuidanceRequested = true;
                RefreshFooter();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(guestButtonRoot);
                return false;
            }
            Destroy(canvas.gameObject);
            GameBootstrap.RestartToSelect();
            return true;
        }

        /// <summary>테스트용 — 알림 버튼을 실제 클릭 경로(onClick)로 누른다.</summary>
        public void ClickGuestButtonForTests() => guestButtonRoot.GetComponent<Button>().onClick.Invoke();
        public void ClickRetryButtonForTests() => retryButtonRoot.GetComponent<Button>().onClick.Invoke();

        GameObject BuildNoticeButton(RectTransform parent, string name, string label, System.Action onClick)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(360f, 64f));
            var bg = UiFactory.AddImage(rt, new Color(0.24f, 0.4f, 0.5f));
            MakeConfirmButton(rt, bg, onClick);
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 26, TextAnchor.MiddleCenter, Color.white);
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        void BuildQuitConfirm(RectTransform parent)
        {
            var rt = UiFactory.Place("QuitConfirm", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(760f, 300f));
            quitConfirmRoot = rt.gameObject;
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.1f, 0.08f, 0.1f, 0.98f))); // 04 §8 ui_panel

            quitMessageText = UiFactory.AddText(UiFactory.Place("Message", rt, new Vector2(0.5f, 1f),
                    new Vector2(0f, -70f), new Vector2(680f, 140f)),
                "", 28, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.85f));

            var retryRt = UiFactory.Place("Retry", rt, new Vector2(0.5f, 0f), new Vector2(-230f, 50f),
                new Vector2(260f, 78f));
            var retryBg = UiFactory.AddImage(retryRt, new Color(0.24f, 0.4f, 0.5f));
            MakeConfirmButton(retryRt, retryBg, RetrySaveThenQuitCheck);
            UiFactory.AddText(UiFactory.Stretch("Label", retryRt), "저장 재시도", 26, TextAnchor.MiddleCenter,
                Color.white);

            var quitAnywayRt = UiFactory.Place("QuitAnyway", rt, new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(260f, 78f));
            var quitAnywayBg = UiFactory.AddImage(quitAnywayRt, new Color(0.55f, 0.22f, 0.2f));
            MakeConfirmButton(quitAnywayRt, quitAnywayBg, DoQuit);
            UiFactory.AddText(UiFactory.Stretch("Label", quitAnywayRt), "저장 없이 종료", 26,
                TextAnchor.MiddleCenter, Color.white);

            var cancelRt = UiFactory.Place("Cancel", rt, new Vector2(0.5f, 0f), new Vector2(230f, 50f),
                new Vector2(260f, 78f));
            var cancelBg = UiFactory.AddImage(cancelRt, new Color(0.3f, 0.32f, 0.36f));
            quitCancelButton = MakeConfirmButton(cancelRt, cancelBg, () => quitConfirmRoot.SetActive(false));
            UiFactory.AddText(UiFactory.Stretch("Label", cancelRt), "취소", 26, TextAnchor.MiddleCenter, Color.white);

            quitConfirmRoot.SetActive(false);
        }

        Button MakeConfirmButton(RectTransform rt, Image bg, System.Action onClick)
        {
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => { AudioService.PlaySfx("ui_confirm"); onClick(); });
            return button;
        }

        void ShowRoot()
        {
            RefreshFooter();
            root.SetActive(true);
            SelectDefault();
        }

        void RefreshFooter()
        {
            int specialty = PermanentProgress.Current.specialtyPoints;
            bool protectedFuture = PermanentProgress.IsFutureSaveProtected;
            bool failed = PermanentProgress.LastSaveFailed && !protectedFuture;
            saveStatusText.text = protectedFuture ? $"스페셜티 {specialty}   ⚠ 저장 보류(새 버전 파일 보존)"
                : failed ? $"스페셜티 {specialty}   ⚠ 저장 실패"
                : PermanentProgress.IsGuestSession ? $"스페셜티 {specialty}   임시 플레이 저장"
                : $"스페셜티 {specialty}   저장됨";

            string notice = PermanentProgress.LastLoadIssue;
            if (failed) notice = string.IsNullOrEmpty(notice) ? PermanentProgress.NoticeSaveFailed
                : notice + " " + PermanentProgress.NoticeSaveFailed;
            if (protectedFuture && guestGuidanceRequested)
                notice = string.IsNullOrEmpty(notice) ? NoticeChooseGuestFirst : notice + "\n" + NoticeChooseGuestFirst;
            saveNoticeText.text = notice ?? "";
            guestButtonRoot.SetActive(protectedFuture);
            retryButtonRoot.SetActive(failed);
            if (saveOkIcon != null) saveOkIcon.gameObject.SetActive(!failed && !protectedFuture);
            if (saveErrorIcon != null) saveErrorIcon.gameObject.SetActive(failed || protectedFuture);
            shownSaveState = CurrentSaveState();
        }

        static int CurrentSaveState() =>
            (PermanentProgress.LastSaveFailed ? 1 : 0) | (PermanentProgress.IsFutureSaveProtected ? 2 : 0) |
            (PermanentProgress.IsGuestSession ? 4 : 0);

        /// <summary>SaveRetryRunner의 5초 자동 재시도 등으로 저장 상태가 바뀌면 열린 타이틀의 알림/버튼을 즉시
        /// 갱신한다. 상태가 같으면 아무것도 바꾸지 않는다(매 프레임 텍스트 재작성 없음).</summary>
        public void PollSaveState()
        {
            if (root == null || !root.activeSelf) return;
            if (CurrentSaveState() != shownSaveState) RefreshFooter();
        }

        void LateUpdate() => HandleTab(); // EventSystem(방향키 이동) 뒤에 처리: 선택 없을 때의 첫 방향키는 기본 항목 표시만

        void Update()
        {
            PollSaveState();
            ApplyInputGate();
        }

        /// <summary>08:174~178 — 미저장 상태(직전 저장 실패)면 재시도/저장 없이 종료/취소를 묻는다.
        /// "저장 없이 종료"를 기본 포커스로 두지 않는다.</summary>
        void RequestQuit()
        {
            // QA 2026-10-04: 진행 save와 설정 저장을 함께 확인한다(설정만 실패해도 조용히 종료하지 않음).
            if (QuitGuard.TrySaveEverything())
            {
                DoQuit();
                return;
            }

            quitMessageText.text = QuitGuard.UnsavedMessage;
            quitConfirmRoot.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(quitCancelButton.gameObject);
        }

        void RetrySaveThenQuitCheck()
        {
            bool ok = QuitGuard.TrySaveEverything(); // 진행·설정 둘 다 재시도(오류 상태는 각자 유지)
            RefreshFooter();
            if (ok)
            {
                quitConfirmRoot.SetActive(false);
                DoQuit();
            }
            else
            {
                quitMessageText.text = "다시 저장하지 못했습니다.\n" + QuitGuard.UnsavedMessage;
            }
        }

        void DoQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            QuitGuard.AllowNextQuit(); // 이미 이 창에서 저장/확인을 거쳤다 — OS 종료 가드를 다시 띄우지 않는다
            Application.Quit();
#endif
        }
    }
}
