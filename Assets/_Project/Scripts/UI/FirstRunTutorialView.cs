using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>08:70~72 "첫 런 안내". 전투 시작 직전에 한 번 자동으로 뜨고(버튼 "시작"), 이후엔 Pause의
    /// "조작 안내"나 설정의 "조작 안내 다시보기"에서 언제든 재열람("닫기"). 두 경로 모두 같은 뷰를 각자
    /// 자기 캔버스에 만들어 쓴다(SettingsView가 Title/Pause 양쪽에서 따로 만들어지는 것과 같은 패턴).</summary>
    public class FirstRunTutorialView : MonoBehaviour
    {
        GameObject root;
        Text buttonLabel;
        Text bodyText;
        Button dismissButton;
        System.Action onDismiss;

        public bool IsOpen => root != null && root.activeSelf;
        public string BodyText => bodyText != null ? bodyText.text : "";

        /// <summary>08:70 안내 4줄. 첫 줄은 08:30 조작 표대로 지금 고른 조작 방식(키보드/마우스 모드)에 맞춘다.</summary>
        public static string BodyFor(MovementScheme scheme) =>
            (MobilePlatform.IsTouchUI ? "왼쪽 아래를 누르고 끌어 이동 · 공격은 자동\n"
                : scheme == MovementScheme.Mouse ? "마우스 커서 쪽으로 이동 · 공격은 자동\n" : "WASD/방향키 이동 · 공격은 자동\n") +
            "적이 떨어뜨린 재료를 주워 게이지를 채우면 카드 선택\n" + // 57: 처치만으로는 안 찬다(주워야 함)
            (MobilePlatform.IsTouchUI ? "원두로 커피 스킬 해금 · 목표 조합 버튼\n" : "원두로 커피 스킬 해금 · [T] 목표 조합\n") +
            (MobilePlatform.IsTouchUI ? "오른쪽 위 II 버튼·뒤로 가기로 일시정지" : "Esc 일시정지");

        public static FirstRunTutorialView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("FirstRunTutorial", canvas);
            var view = rootRt.gameObject.AddComponent<FirstRunTutorialView>();
            view.root = rootRt.gameObject;

            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.88f));
            CenterBand.Attach(rootRt); // 2026-10-12 세로가 긴 화면: 내용 세로 가운데

            var panelRt = UiFactory.Place("Panel", rootRt, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1000f, 560f));
            // 2026-10-06 r8 네이티브 QA: 설정의 "조작 안내 다시보기"로 열면 0.88 딤 너머로 설정 글자가 비쳐 읽기 어려웠다.
            // 패널 자체를 불투명 배경으로 막고, 스킨 프레임은 그 위 첫 자식으로 둔다(글자보다 뒤).
            UiFactory.AddImage(panelRt, new Color(0.1f, 0.08f, 0.1f, 1f));
            UiFactory.SkinPanel(UiFactory.AddImage(UiFactory.Stretch("Frame", panelRt), new Color(0.1f, 0.08f, 0.1f, 1f)));

            var title = UiFactory.Place("Title", panelRt, new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(900f, 70f));
            UiFactory.AddText(title, "시작하기 전에", 44, TextAnchor.MiddleCenter, Color.white);

            var bodyRt = UiFactory.Place("Body", panelRt, new Vector2(0.5f, 0.5f), new Vector2(0f, MobilePlatform.IsTouchUI ? 25f : -20f), // 모바일: 본문이 아래로 쏠려 보여 위로
                new Vector2(820f, 320f));
            view.bodyText = UiFactory.AddText(bodyRt, BodyFor(InputSettings.Scheme), 24, TextAnchor.MiddleCenter,
                new Color(0.92f, 0.92f, 0.92f));

            var buttonRt = UiFactory.Place("DismissButton", panelRt, new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(320f, 84f));
            var bg = UiFactory.AddImage(buttonRt, new Color(0.9f, 0.35f, 0.75f));
            var button = buttonRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(view.Dismiss);
            view.dismissButton = button;
            view.buttonLabel = UiFactory.AddText(UiFactory.Stretch("Label", buttonRt), "", 32,
                TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        /// <summary>buttonLabel: 첫 안내면 "시작", 재열람이면 "닫기"(08:72). onDismiss: 버튼을 누른 뒤
        /// 해야 할 일(첫 안내면 실제 런 시작, 재열람이면 없음).</summary>
        public void Show(string buttonLabelText, System.Action onDismiss, Job? job = null)
        {
            bodyText.text = BodyFor(InputSettings.Scheme); // 설정에서 조작 방식을 바꾼 뒤 열어도 현재 방식으로 안내
            // 2026-10-12 찻집 런이면 커피/원두 문구를 우림 연구/우림 지식으로(바텐더는 그대로). 첫 안내는 런 생성 전이라 직업을 넘겨받는다.
            var themeJob = job ?? (GameManager.Instance != null ? GameManager.Instance.Job : (Job?)null);
            if (themeJob.HasValue) bodyText.text = JobStudyTheme.Text(bodyText.text, themeJob.Value);
            buttonLabel.text = buttonLabelText;
            this.onDismiss = onDismiss;
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);
        }

        /// <summary>버튼 클릭과 같은 동작 — Esc로 닫을 때도 이걸 쓴다(PauseView.CloseTutorial).</summary>
        public void Dismiss()
        {
            AudioService.PlaySfx("ui_confirm");
            root.SetActive(false);
            var callback = onDismiss;
            onDismiss = null;
            callback?.Invoke();
        }
    }
}
