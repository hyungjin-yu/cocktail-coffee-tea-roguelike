using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>07:114 — 레이피어 시작으로 칵테일3+차3를 모으면 뜨는 확정 해금 창(추첨 아님, 버튼 1개).</summary>
    public class HybridUnlockView : MonoBehaviour
    {
        GameObject root;
        Action onUnlock;
        Button unlockButton;
        // 2026-11-22 특수 등급 메뉴판: 패널(980×560)에 196×112 장식을 5배로, 패널 가운데(Y +105)에 건네기 앵커. 확정 해금 경로(추첨 풀과 별개).
        CanvasGroup content;
        Image decoration, handoffImage;
        readonly MenuHandoffPlayer handoff = new MenuHandoffPlayer();
        public bool HandoffPlaying => handoff.Active;
        public int HandoffFrame => handoff.Active ? handoff.Frame : -1;
        public int HandoffTier => handoff.Active ? handoff.Tier : -1;
        public float ContentAlpha => content != null ? content.alpha : 1f;
        public bool InputLocked => inputGate.Locked;
        public Sprite HandoffSprite => handoff.Active ? handoffImage.sprite : null;
        public bool ContentInteractive => content != null && content.interactable && content.blocksRaycasts;
        public Sprite Decoration => decoration != null && decoration.gameObject.activeSelf ? decoration.sprite : null;

        public bool IsOpen => root != null && root.activeSelf;

        public static HybridUnlockView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("HybridUnlock", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.8f));

            var view = rootRt.gameObject.AddComponent<HybridUnlockView>();
            view.root = rootRt.gameObject;
            // 패널·아이콘·글·버튼을 한 묶음(CanvasGroup)으로 — 건네기 뒤 70 ms 동안 함께 나타난다. 덮개는 묶음 밖.
            var contentRt = UiFactory.Stretch("Content", rootRt);
            view.content = contentRt.gameObject.AddComponent<CanvasGroup>();
            var holder = rootRt;
            rootRt = contentRt;

            // r13 검수 U1: 반투명 덮개만으로는 뒤 전투 효과·피해 숫자가 안내 글자와 겹쳐 보였다. 첫 안내/결과 화면과 같은
            // 불투명 야간 패널(+ ui_panel 테두리)로 아이콘·제목·설명·버튼 영역을 가린다. 레이아웃·문구는 그대로.
            var panelRt = UiFactory.Place("Panel", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 105f),
                new Vector2(980f, 560f));
            UiFactory.AddImage(panelRt, new Color(0.1f, 0.08f, 0.1f, 1f));
            UiFactory.SkinPanel(UiFactory.AddImage(UiFactory.Stretch("Frame", panelRt), new Color(0.1f, 0.08f, 0.1f, 1f)));
            var decoRt = UiFactory.Stretch("MenuDeco", panelRt); // 기존 바탕 위, 아이콘·글(패널 밖 형제)·버튼 아래
            view.decoration = decoRt.gameObject.AddComponent<Image>();
            view.decoration.color = Color.white; view.decoration.raycastTarget = false;

            // 09 hybrid_tea_martini 아이콘(텍스트는 Unity 텍스트 그대로).
            UiFactory.AddIcon(rootRt, "hybrid_tea_martini", new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), 150f);
            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f),
                new Vector2(900f, 90f));
            UiFactory.AddText(title, "하이브리드 해금!  티니 보우", 52, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.4f));

            var desc = UiFactory.Place("Desc", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f),
                new Vector2(820f, 90f));
            UiFactory.AddText(desc, "레이피어에 차의 활을 더합니다 — 보조 화살이 자동 발사됩니다.", 30,
                TextAnchor.MiddleCenter, Color.white);

            var buttonRt = UiFactory.Place("UnlockButton", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f),
                new Vector2(300f, 84f));
            var bg = UiFactory.AddImage(buttonRt, new Color(0.9f, 0.7f, 0.25f));
            var button = buttonRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() =>
            {
                // 08:39 — 직전 카드 선택을 닫은 Enter/클릭이 이 창을 바로 확정하지 않게 한다.
                if (!view.inputGate.Allows(Time.frameCount, InputReleaseGate.AnyConfirmHeld())) return;
                view.Unlock();
            });
            view.unlockButton = button;
            // U1: 공용 ui_button 스킨이 입혀지면 버튼 바탕이 어두워져 기존 어두운 글자가 묻혔다 — 다른 메뉴 버튼과 같은 밝은 글자.
            UiFactory.AddText(UiFactory.Stretch("Label", buttonRt), "해금", 32, TextAnchor.MiddleCenter, Color.white);

            view.handoffImage = MenuHandoffPlayer.CreateImage(holder, "MenuHandoff", LevelUpMenuArt.HybridAnchorY);
            view.root.SetActive(false);
            return view;
        }

        readonly InputReleaseGate inputGate = new InputReleaseGate();

        public void Show(Action callback)
        {
            CancelHandoff(); // 재오픈: 이전 재생 상태를 지운다
            onUnlock = callback;
            var deco = LevelUpMenuArt.HybridDecoration();
            decoration.sprite = deco;
            decoration.gameObject.SetActive(deco != null);
            inputGate.Arm(Time.frameCount);
            // 현 해금 조건은 바텐더·레이피어 — 실제 직업 외형을 쓰되 직업을 알 수 없으면 바텐더. 저이펙트는 전달 없이 정착 상태.
            var job = GameManager.Instance != null ? GameManager.Instance.Job : Job.Bartender;
            var clip = HitFeedback.LowEffects ? null : LevelUpMenuArt.Frames(job, 3);
            if (clip != null)
            {
                handoff.Begin(handoffImage, clip, 3);
                SetContent(0f, false);
            }
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(unlockButton.gameObject);
        }

        void Update()
        {
            // 08:39 — 매 프레임 눌림 상태를 봐야 "뗐다가 다시 누른" Enter/클릭을 허용할 수 있다.
            if (!IsOpen) return;
            if (handoff.Active)
            {
                bool done = handoff.Tick(Time.unscaledDeltaTime);
                SetContent(done ? 1f : handoff.Reveal, done);
                if (done) inputGate.Arm(Time.frameCount); // 정착 뒤 입력 게이트를 새로 무장
            }
            inputGate.Poll(Time.frameCount, InputReleaseGate.AnyConfirmHeld());
        }

        void SetContent(float k, bool interactive)
        {
            content.alpha = k;
            content.interactable = interactive;
            content.blocksRaycasts = interactive;
            content.transform.localScale = Vector3.one * Mathf.Lerp(LevelUpMenuArt.StartScale, 1f, k);
        }

        void CancelHandoff()
        {
            handoff.Stop();
            if (content != null) SetContent(1f, true);
        }

        void OnDisable() { CancelHandoff(); }

        /// <summary>"해금" 버튼과 테스트가 함께 쓰는 공개 진입점(CardSelectView.Choose 등과 동일 관례).</summary>
        public void Unlock()
        {
            if (!IsOpen || onUnlock == null) return;
            // 08:23 "키 관통 없음"과 동일한 이유로, Pause가 이 화면 위에 열려있으면 무시한다.
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            var callback = onUnlock;
            onUnlock = null;
            CancelHandoff();
            root.SetActive(false);
            callback();
        }
    }
}
