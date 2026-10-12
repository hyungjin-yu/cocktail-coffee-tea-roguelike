using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>08장 "### 제작 정보". 개인 실명/계정 이름은 자동 삽입하지 않는다 — 명세 그대로.</summary>
    public class CreditsView : MonoBehaviour
    {
        GameObject root;
        Action onClose;
        Button closeButton;

        public bool IsOpen => root != null && root.activeSelf;

        public static CreditsView Create(Transform canvas, Action onClose)
        {
            var rootRt = UiFactory.Stretch("Credits", canvas);
            var view = rootRt.gameObject.AddComponent<CreditsView>();
            view.root = rootRt.gameObject;
            view.onClose = onClose;

            UiFactory.AddImage(rootRt, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(rootRt);

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -90f),
                new Vector2(900f, 90f));
            UiFactory.AddText(title, "제작 정보", 54, TextAnchor.MiddleCenter, Color.white);

            string body =
                $"칵테일 · 커피 · 차  ·  v{Application.version}\n\n" +
                "아트\n" +
                "PixelLab으로 제작한 픽셀 아트를 Aseprite에서 편집했습니다.\n\n" +
                "폰트\n" +
                "Noto Sans CJK KR (Regular) — SIL Open Font License 1.1.\n" +
                (MobilePlatform.IsTouchUI ? "폰트는 SIL Open Font License 1.1에 따라 사용했습니다.\n\n" : "라이선스 전문은 Licenses/LICENSE_NotoSansCJK.txt에 동봉되어 있습니다.\n\n") +
                "음악·효과음\n" +
                "모든 배경음악과 효과음은 외부 음원 없이 절차적 합성으로 직접 생성했습니다.";

            var bodyRt = UiFactory.Place("Body", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(1100f, 640f));
            UiFactory.AddText(bodyRt, body, 30, TextAnchor.UpperLeft, new Color(0.92f, 0.92f, 0.92f));

            var closeRt = UiFactory.Place("CloseButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 70f),
                new Vector2(320f, 80f));
            var closeBg = UiFactory.AddImage(closeRt, new Color(0.24f, 0.4f, 0.5f));
            var button = closeRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = closeBg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => { AudioService.PlaySfx("ui_cancel"); view.Hide(); });
            view.closeButton = button;
            UiFactory.AddText(UiFactory.Stretch("Label", closeRt), "닫기", 30, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        public void Show()
        {
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Hide()
        {
            root.SetActive(false);
            onClose?.Invoke();
        }
    }
}
