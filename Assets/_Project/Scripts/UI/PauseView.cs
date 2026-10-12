using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 일시정지 오버레이 (08장 "### 일시정지"). Esc로 GameManager가 열고 닫는다 — 이 뷰는 자기 상태를
    /// 스스로 판단하지 않고 Show/Hide만 받는다. 08:99 버튼 6개(계속/커피 트리/설정/조작 안내/런 포기/
    /// 타이틀로 종료) 전부 구현(2026-09-28). 설정·커피 트리·조작 안내는 Pause 위에 얹히는 오버레이라
    /// Pause 자체는 그대로 열려 있는다(Esc는 그 오버레이만 먼저 닫음 — GameManager.Update 참고).
    /// 08:102 "1초 '계속' 카운트다운"은 GameManager.ClosePause/ResumeAfterCountdown이 켜고 끄는
    /// resumeCountdownGo로 표시한다 — Pause 메뉴 자체와 독립적이라 root가 숨어도 계속 보인다.
    /// </summary>
    public class PauseView : MonoBehaviour
    {
        GameManager game;
        GameObject root;
        GameObject confirmRoot;
        Text confirmButtonLabel;
        System.Action pendingConfirmAction;
        Button skillTreeButton;
        Text skillTreeReasonText;
        SettingsView settingsView;
        FirstRunTutorialView tutorialView;
        GameObject resumeCountdownGo;
        Button continueButton;
        Button confirmCancelButton;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>QA 2026-10-05: 커피 트리·설정·조작 안내를 Pause에서 열면 그 화면만 보이게 Pause 메뉴를 숨긴다
        /// (Pause는 논리적으로 계속 열려 있음 — Esc 순서·시간정지 불변). 닫으면 메뉴와 연 버튼 포커스를 되돌린다.</summary>
        public bool MenuHiddenForSubView => menuGroup != null && menuGroup.alpha < 0.5f;
        CanvasGroup menuGroup;
        GameObject subViewOpener;
        public bool IsSettingsOpen => settingsView != null && settingsView.IsOpen;
        public void CloseSettings() { AudioService.PlaySfx("ui_cancel"); settingsView.Hide(); }
        public bool IsSkillTreeOpen => game.SkillView != null && game.SkillView.IsReadOnlyOpen;
        public void CloseSkillTree() { AudioService.PlaySfx("ui_cancel"); game.SkillView.CloseReadOnly(); }
        public bool IsTutorialOpen => tutorialView != null && tutorialView.IsOpen;
        public void CloseTutorial() => tutorialView.Dismiss();
        /// <summary>테스트용으로도 참조(SettingsViewTests).</summary>
        public SettingsView SettingsView => settingsView;
        /// <summary>테스트용으로도 참조(PauseTests).</summary>
        public FirstRunTutorialView TutorialView => tutorialView;
        /// <summary>테스트용으로도 참조(PauseTests) — 08:101 비활성/사유문구 확인.</summary>
        public Button SkillTreeButton => skillTreeButton;
        public string SkillTreeReason => skillTreeReasonText.text;
        /// <summary>08:102 "1초 '계속' 카운트다운" — Pause 메뉴(root)와 별개로, Pause를 닫아도 보이는
        /// 화면 중앙 안내. GameManager.ClosePause/ResumeAfterCountdown이 켜고 끈다.</summary>
        public bool IsResumeCountdownVisible => resumeCountdownGo != null && resumeCountdownGo.activeSelf;
        public void ShowResumeCountdown() => resumeCountdownGo.SetActive(true);
        public void HideResumeCountdown() => resumeCountdownGo.SetActive(false);

        public static PauseView Create(Transform canvas, GameManager game)
        {
            var rootRt = UiFactory.Stretch("Pause", canvas);
            var view = rootRt.gameObject.AddComponent<PauseView>();
            view.game = game;
            view.root = rootRt.gameObject;
            view.menuGroup = rootRt.gameObject.AddComponent<CanvasGroup>();

            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.7f));
            UiFactory.AddCafeBackdrop(rootRt);

            UiFactory.AddIcon(rootRt, "ui_pause", new Vector2(0.5f, 0.5f), new Vector2(0f, 345f), 80f); // 09 ui_pause
            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f),
                new Vector2(600f, 90f));
            UiFactory.AddText(title, "일시정지", 54, TextAnchor.MiddleCenter, Color.white);

            // 08:99 버튼 순서 그대로: 계속 / 커피 트리 / 설정 / 조작 안내 / 런 포기 / 타이틀로 종료.
            var continueRt = UiFactory.Place("ContinueButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 140f), new Vector2(400f, 78f));
            var continueBg = UiFactory.AddImage(continueRt, new Color(0.24f, 0.4f, 0.5f));
            view.continueButton = MakeButton(continueRt, continueBg,
                () => { AudioService.PlaySfx("ui_cancel"); view.game.ClosePause(); });
            UiFactory.AddText(UiFactory.Stretch("Label", continueRt), UiFactory.InputLabel("게임 재개", "Esc"), 27,
                TextAnchor.MiddleCenter, Color.white);

            var skillTreeRt = UiFactory.Place("SkillTreeButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 57f), new Vector2(400f, 78f));
            var skillTreeBg = UiFactory.AddImage(skillTreeRt, new Color(0.42f, 0.28f, 0.18f));
            view.skillTreeButton = MakeButton(skillTreeRt, skillTreeBg, () =>
            {
                AudioService.PlaySfx("ui_confirm");
                view.HideMenuForSubView(view.skillTreeButton.gameObject);
                view.game.SkillView.ShowReadOnly(view.game.Skills, () => view.game.CoffeePoints, view.RestoreMenuAfterSubView);
            });
            UiFactory.AddText(UiFactory.Stretch("Label", skillTreeRt), JobStudyTheme.TreeTitle(game != null ? game.Job : Job.Bartender), 27,
                TextAnchor.MiddleCenter, Color.white);
            view.skillTreeReasonText = UiFactory.AddText(UiFactory.Place("Reason", skillTreeRt,
                new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(380f, 26f)),
                "", 17, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.7f));

            var settingsRt = UiFactory.Place("SettingsButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -26f), new Vector2(400f, 78f));
            var settingsBg = UiFactory.AddImage(settingsRt, new Color(0.32f, 0.3f, 0.34f));
            Button settingsButton = null;
            settingsButton = MakeButton(settingsRt, settingsBg, () =>
            {
                AudioService.PlaySfx("ui_confirm");
                view.settingsView.Show(); // 설정은 열 때의 포커스(이 버튼)를 닫을 때 스스로 되돌린다
                view.HideMenuForSubView(settingsButton.gameObject);
            });
            UiFactory.AddText(UiFactory.Stretch("Label", settingsRt), "설정", 27,
                TextAnchor.MiddleCenter, Color.white);

            var tutorialRt = UiFactory.Place("TutorialButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -109f), new Vector2(400f, 78f));
            var tutorialBg = UiFactory.AddImage(tutorialRt, new Color(0.3f, 0.32f, 0.36f));
            Button tutorialButton = null;
            tutorialButton = MakeButton(tutorialRt, tutorialBg, () =>
            {
                AudioService.PlaySfx("ui_confirm");
                view.HideMenuForSubView(tutorialButton.gameObject);
                view.tutorialView.Show("닫기", view.RestoreMenuAfterSubView);
            });
            UiFactory.AddText(UiFactory.Stretch("Label", tutorialRt), "조작 안내", 27,
                TextAnchor.MiddleCenter, Color.white);

            var giveUpRt = UiFactory.Place("GiveUpButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -192f), new Vector2(400f, 78f));
            var giveUpBg = UiFactory.AddImage(giveUpRt, new Color(0.5f, 0.24f, 0.24f));
            MakeButton(giveUpRt, giveUpBg, () =>
            {
                AudioService.PlaySfx("ui_confirm");
                view.ShowConfirm("포기", () => view.game.GiveUp());
            });
            UiFactory.AddText(UiFactory.Stretch("Label", giveUpRt), "런 포기", 27,
                TextAnchor.MiddleCenter, Color.white);

            var quitTitleRt = UiFactory.Place("QuitToTitleButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -275f), new Vector2(400f, 78f));
            var quitTitleBg = UiFactory.AddImage(quitTitleRt, new Color(0.4f, 0.32f, 0.24f));
            MakeButton(quitTitleRt, quitTitleBg, () =>
            {
                AudioService.PlaySfx("ui_confirm");
                view.ShowConfirm("타이틀로 종료", () => view.game.QuitToTitle());
            });
            UiFactory.AddText(UiFactory.Stretch("Label", quitTitleRt), "타이틀로 종료", 27,
                TextAnchor.MiddleCenter, Color.white);

            view.BuildConfirm(rootRt);
            view.settingsView = SettingsView.Create(rootRt.parent, view.RestoreMenuAfterSubView);
            view.tutorialView = FirstRunTutorialView.Create(rootRt.parent);
            view.BuildResumeCountdown(rootRt.parent);

            view.root.SetActive(false);
            return view;
        }

        /// <summary>Pause 메뉴(root)와 독립된 오브젝트라 root가 숨어도 계속 보인다.</summary>
        void BuildResumeCountdown(Transform canvas)
        {
            var rt = UiFactory.Place("ResumeCountdown", canvas, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f),
                new Vector2(400f, 100f));
            resumeCountdownGo = rt.gameObject;
            UiFactory.AddImage(rt, new Color(0f, 0f, 0f, 0.6f));
            UiFactory.AddText(UiFactory.Stretch("Label", rt), "계속...", 36, TextAnchor.MiddleCenter, Color.white);
            resumeCountdownGo.SetActive(false);
        }

        /// <summary>08:99~100 — "런 포기"와 "타이틀로 종료"는 같은 확인 문구를 쓰고 확정 버튼 라벨과
        /// 실행 동작만 다르다.</summary>
        void BuildConfirm(Transform parent)
        {
            var rt = UiFactory.Place("EndRunConfirm", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(760f, 320f));
            confirmRoot = rt.gameObject;
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.1f, 0.08f, 0.1f, 1f))); // 04 §8 ui_panel(모달은 불투명)

            UiFactory.AddText(UiFactory.Place("Message", rt, new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                    new Vector2(660f, 140f)),
                "이번 런은 이어할 수 없습니다.\n획득한 영구 진행은 저장됩니다.", 30, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.9f, 0.85f));

            var cancelRt = UiFactory.Place("Cancel", rt, new Vector2(0.5f, 0f), new Vector2(-190f, 60f),
                new Vector2(300f, 78f));
            var cancelBg = UiFactory.AddImage(cancelRt, new Color(0.3f, 0.32f, 0.36f));
            confirmCancelButton = MakeButton(cancelRt, cancelBg, () => { AudioService.PlaySfx("ui_cancel"); HideConfirm(); });
            UiFactory.AddText(UiFactory.Stretch("Label", cancelRt), "취소", 28, TextAnchor.MiddleCenter, Color.white);

            var confirmRt = UiFactory.Place("Confirm", rt, new Vector2(0.5f, 0f), new Vector2(190f, 60f),
                new Vector2(300f, 78f));
            var confirmBg = UiFactory.AddImage(confirmRt, new Color(0.55f, 0.22f, 0.2f));
            var confirmButton = MakeButton(confirmRt, confirmBg, () =>
            {
                AudioService.PlaySfx("ui_confirm");
                var action = pendingConfirmAction;
                HideConfirm();
                action?.Invoke();
            });
            // 2026-10-09 실제 OS 키보드 검수(G24): 자동 탐색이 확인창 밖의 일시정지 버튼으로 포커스를 넘겨 Enter가 뒤의 "설정"을
            // 열었다. 확인창의 두 버튼은 서로에게만 이동한다(좌우), 위아래는 막는다.
            confirmCancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = confirmButton, selectOnLeft = confirmButton };
            confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = confirmCancelButton, selectOnRight = confirmCancelButton };
            confirmButtonLabel = UiFactory.AddText(UiFactory.Stretch("Label", confirmRt), "", 28,
                TextAnchor.MiddleCenter, Color.white);

            confirmRoot.SetActive(false);
        }

        public void Show()
        {
            SetMenuVisible(true);
            HideConfirm();
            RefreshSkillTreeButton();
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        public void Hide()
        {
            SetMenuVisible(true);
            subViewOpener = null;
            HideConfirm();
            root.SetActive(false);
        }

        void HideMenuForSubView(GameObject opener)
        {
            subViewOpener = opener;
            SetMenuVisible(false);
        }

        void RestoreMenuAfterSubView()
        {
            if (menuGroup == null || MenuHiddenForSubView == false) return;
            SetMenuVisible(true);
            var opener = subViewOpener;
            subViewOpener = null;
            if (IsOpen && EventSystem.current != null && opener != null && opener.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(opener);
        }

        void SetMenuVisible(bool visible)
        {
            if (menuGroup == null) return;
            menuGroup.alpha = visible ? 1f : 0f;
            menuGroup.interactable = visible;
            menuGroup.blocksRaycasts = visible;
        }

        /// <summary>08:101 — 선택화면(카드/스킬/하이브리드) 위에 Pause가 열려 있으면 커피 트리 버튼은
        /// 비활성(서로 다른 보상창 중첩 금지).</summary>
        void RefreshSkillTreeButton()
        {
            bool allowed = game.State == GameState.Playing;
            skillTreeButton.interactable = allowed;
            skillTreeReasonText.text = allowed ? "" : "현재 보상 선택 후 이용";
        }

        /// <summary>08:100 "취소 기본 포커스".</summary>
        void ShowConfirm(string confirmLabel, System.Action onConfirm)
        {
            confirmButtonLabel.text = confirmLabel;
            pendingConfirmAction = onConfirm;
            confirmRoot.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirmCancelButton.gameObject);
        }

        void HideConfirm()
        {
            confirmRoot.SetActive(false);
            pendingConfirmAction = null;
        }

        static Button MakeButton(RectTransform rt, Image bg, System.Action onClick)
        {
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
