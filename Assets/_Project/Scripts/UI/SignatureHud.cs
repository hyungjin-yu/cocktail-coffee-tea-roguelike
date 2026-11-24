using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 시그니처 칸(오른쪽 아래). 이번 판에 충전·사용한 시그니처가 있을 때만 보인다(없으면 아무것도 그리지 않음 — 정답 힌트 없음).
    /// 상태는 색에만 기대지 않고 글자로 함께 표시: "1회 남음 · Q 또는 클릭" → "사용함". 사이드카처럼 지속형이면 남은 초를 숫자로.
    /// 클릭은 Q 키와 같은 GameManager.TryUseSignature 경로다. 전투 화면이 아닐 때(카드·일시정지·결과 등)는 숨긴다.
    /// 위치는 정적 배치 가안 — 다음 Unity 검수에서 720p/1080p/4:3 캡처로 다른 HUD와 겹침을 확인해야 한다.
    /// </summary>
    public class SignatureHud : MonoBehaviour
    {
        public const float Right = 30f, Bottom = 150f, W = 380f, H = 124f;
        GameManager game;
        GameObject root;
        Button button;
        Image bg, icon;
        Text title, state, line;
        float messageUntil;

        public bool Visible => root != null && root.activeSelf;
        public string StateText => state != null ? state.text : "";
        public string TitleText => title != null ? title.text : "";
        public Button Button => button;

        public static SignatureHud Create(Transform canvas, GameManager game)
        {
            var go = new GameObject("SignatureHud", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var hud = go.AddComponent<SignatureHud>();
            hud.game = game;
            hud.Build((RectTransform)go.transform);
            return hud;
        }

        void Build(RectTransform parent)
        {
            var stretch = parent;
            stretch.anchorMin = Vector2.zero; stretch.anchorMax = Vector2.one; stretch.offsetMin = Vector2.zero; stretch.offsetMax = Vector2.zero;
            var rt = UiFactory.Place("SignatureSlot", parent, new Vector2(1f, 0f), new Vector2(-Right, Bottom), new Vector2(W, H));
            root = rt.gameObject;
            bg = UiFactory.AddImage(rt, new Color(0.1f, 0.07f, 0.04f, 0.9f));
            button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => { if (game != null) game.TryUseSignature(); });
            var irt = UiFactory.Place("Icon", rt, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(64f, 64f));
            icon = UiFactory.AddImage(irt, Color.white);
            icon.sprite = SpriteFactory.Circle;
            icon.raycastTarget = false;
            title = UiFactory.AddText(UiFactory.Place("Title", rt, new Vector2(0f, 1f), new Vector2(92f, -8f), new Vector2(W - 104f, 34f)), "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.55f));
            state = UiFactory.AddText(UiFactory.Place("State", rt, new Vector2(0f, 1f), new Vector2(92f, -42f), new Vector2(W - 104f, 30f)), "", 20, TextAnchor.MiddleLeft, Color.white);
            line = UiFactory.AddText(UiFactory.Place("Line", rt, new Vector2(0f, 0f), new Vector2(92f, 6f), new Vector2(W - 104f, 46f)), "", 18, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            foreach (var t in new[] { title, state, line })
            {
                t.raycastTarget = false; t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = t.fontSize;
                t.verticalOverflow = VerticalWrapMode.Truncate;
            }
            root.SetActive(false);
        }

        /// <summary>사용 시도 결과 한 줄(2.5초).</summary>
        public void ShowMessage(string text)
        {
            if (line == null || string.IsNullOrEmpty(text)) return;
            line.text = text;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        void Update()
        {
            var sig = game != null ? game.Signature : null;
            var def = sig == null ? null : sig.Charged ?? sig.UsedDef;
            bool show = def != null && DrinkRunHud.CombatVisible(game);
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;
            bool used = sig.Used;
            title.text = $"시그니처 · {def.effectName}";
            state.text = used
                ? (sig.ActiveRemaining > 0f ? $"사용함 · {IntNum.RemainingSeconds(sig.ActiveRemaining)}초 남음" : "사용함")
                : "1회 남음 · Q 또는 클릭";
            icon.color = used ? new Color(0.45f, 0.45f, 0.45f, 1f) : def.accent;
            bg.color = used ? new Color(0.12f, 0.12f, 0.12f, 0.85f) : new Color(0.1f, 0.07f, 0.04f, 0.9f);
            button.interactable = !used && sig.CanUseNow;
            if (Time.unscaledTime >= messageUntil) line.text = used ? "이번 판에서는 다시 쓸 수 없습니다" : def.effectShort;
        }
    }
}
