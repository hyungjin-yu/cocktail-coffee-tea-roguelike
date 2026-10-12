using UnityEngine;
using UnityEngine.EventSystems;
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

        PermanentSkillView shopView;
        CardCodexView codexView;
        SettingsView settingsView;
        CreditsView creditsView;
        SkinShopView skinShopView;

        public bool IsOpen => root != null && root.activeSelf;
        public PermanentSkillView ShopView => shopView;
        public CardCodexView CodexView => codexView;
        public SettingsView SettingsView => settingsView;
        public CreditsView CreditsView => creditsView;
        public SkinShopView SkinShopView => skinShopView;
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

            view.BuildRoot();
            return view;
        }

        void BuildRoot()
        {
            root = UiFactory.Stretch("Root", canvas.transform).gameObject;
            var rootRt = (RectTransform)root.transform;
            UiFactory.AddImage(rootRt, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(rootRt);

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                new Vector2(1400f, 110f));
            UiFactory.AddText(title, "칵테일 · 커피 · 차", 76, TextAnchor.MiddleCenter, Color.white);

            var subtitle = UiFactory.Place("Subtitle", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -230f),
                new Vector2(1200f, 60f));
            UiFactory.AddText(subtitle, "한 잔의 빌드로 영업을 끝내세요", 32, TextAnchor.MiddleCenter,
                new Color(0.8f, 0.8f, 0.85f));

            // 2026-10-04 1차 빌드 캡처에서 발견: 이전 y=-30(위 고정)은 메뉴가 제목·부제 위에 겹쳤다. 부제(-230..-290) 아래부터.
            float y = -310f;
            startButton = BuildMenuButton(rootRt, "StartButton", "시작", ref y, new Color(0.9f, 0.35f, 0.75f), () => RequestStart());
            var shopButton = BuildMenuButton(rootRt, "PermanentShopButton", "영구 강화", ref y, new Color(0.4f, 0.24f, 0.5f), () =>
            {
                root.SetActive(false);
                shopView.Show();
            });
            var codexButton = BuildMenuButton(rootRt, "CardCodexButton", "도감", ref y, new Color(0.24f, 0.4f, 0.5f), () =>
            {
                root.SetActive(false);
                codexView.Show();
            });
            var settingsButton = BuildMenuButton(rootRt, "SettingsButton", "설정", ref y, new Color(0.32f, 0.3f, 0.34f), () =>
            {
                root.SetActive(false);
                settingsView.Show();
            });
            // 09 ui_codex/ui_settings, 04 §8 specialty 아이콘(버튼 왼쪽, 글자는 Unity 텍스트).
            UiFactory.AddIcon(shopButton.transform, "specialty", new Vector2(0f, 0.5f), new Vector2(12f, 0f), 52f);
            UiFactory.AddIcon(codexButton.transform, "ui_codex", new Vector2(0f, 0.5f), new Vector2(12f, 0f), 52f);
            UiFactory.AddIcon(settingsButton.transform, "ui_settings", new Vector2(0f, 0.5f), new Vector2(12f, 0f), 52f);
            BuildMenuButton(rootRt, "CreditsButton", "제작 정보", ref y, new Color(0.3f, 0.32f, 0.28f), () =>
            {
                root.SetActive(false);
                creditsView.Show();
            });
            BuildMenuButton(rootRt, "QuitButton", "종료", ref y, new Color(0.5f, 0.24f, 0.24f), RequestQuit);

            // 2026-10-12 치장 스킨 상점: 세로 메뉴는 저장 알림(SaveNotice)과 겹치지 않게 6개로 꽉 차 있어 오른쪽 위에 둔다.
            BuildSkinShopButton(rootRt);

            BuildFooter(rootRt);
            BuildQuitConfirm(rootRt);
            RefreshFooter();
            SelectDefault();
        }

        /// <summary>G31 — 마우스로 아무것도 안 눌러도 Tab/화살표로 바로 다닐 수 있게 기본 항목을 정해 둔다.
        /// 2026-10-12: 진입 즉시 선택하면 "시작"이 파란 강조로 고정돼 보여, 첫 키보드 입력 때 드러내도록 UiInitialFocus에 맡긴다.</summary>
        void SelectDefault()
        {
            // 미래 버전 보호 중이면 임시 플레이 선택 버튼을 먼저 가리킨다.
            var target = guestButtonRoot != null && guestButtonRoot.activeSelf ? guestButtonRoot : startButton.gameObject;
            UiInitialFocus.Offer(target);
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

        void BuildSkinShopButton(RectTransform parent)
        {
            var rt = UiFactory.Place("SkinShopButton", parent, new Vector2(1f, 1f), new Vector2(-40f, -16f), new Vector2(300f, 120f)); // 제목 상자(위 140..250) 위쪽
            var bg = UiFactory.AddImage(rt, new Color(0.55f, 0.4f, 0.2f));
            MakeConfirmButton(rt, bg, () =>
            {
                root.SetActive(false);
                skinShopView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), "스킨", 34, TextAnchor.MiddleCenter, Color.white);
        }

        void BuildFooter(RectTransform parent)
        {
            UiFactory.AddText(UiFactory.Place("Version", parent, new Vector2(0f, 0f), new Vector2(30f, 30f),
                new Vector2(300f, 40f)), $"v{Application.version}", 22, TextAnchor.LowerLeft,
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

        void Update() => PollSaveState();

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
