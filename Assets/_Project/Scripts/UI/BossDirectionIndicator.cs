using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 57(2026-10-15) P0-1: 보스가 화면 밖에 있으면 그쪽 화면 가장자리에 방향·거리·이름을 띄운다.
    /// 보스가 없거나 죽었거나 화면 안에 들어오면 숨긴다. HUD 루트 자식이라 결과 화면(SetCombatHidden)에서도 함께 숨는다.
    /// </summary>
    public class BossDirectionIndicator : MonoBehaviour
    {
        GameManager game;
        RectTransform rt;
        Text label;
        CanvasGroup group;

        /// <summary>테스트/보고용 — 지금 보이는지와 가리키는 쪽(−1 왼쪽/+1 오른쪽, 숨김이면 0).</summary>
        public bool Visible { get; private set; }
        public float Side { get; private set; }
        public string LabelText => label != null ? label.text : "";

        public static BossDirectionIndicator Create(Transform hudRoot, GameManager game)
        {
            var rt = UiFactory.Place("BossDirection", hudRoot, new Vector2(1f, 0.45f), Vector2.zero, new Vector2(300f, 92f));
            var bg = UiFactory.AddImage(rt, new Color(0.35f, 0.05f, 0.08f, 0.9f));
            bg.raycastTarget = false;
            var labelRt = UiFactory.Stretch("Label", rt);
            labelRt.offsetMin = new Vector2(14f, 6f);
            labelRt.offsetMax = new Vector2(-14f, -6f);
            var view = rt.gameObject.AddComponent<BossDirectionIndicator>();
            view.game = game;
            view.rt = rt;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            view.group.interactable = view.group.blocksRaycasts = false;
            view.label = UiFactory.AddText(labelRt, "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
            view.label.verticalOverflow = VerticalWrapMode.Truncate;
            view.Apply(false, 0f, 0.45f, "");
            return view;
        }

        /// <summary>순수 판정 — 보스 중심이 화면 안(반지름의 절반 이상 보임)인지. 화면 밖이면 side=보스 쪽.</summary>
        public static bool IsOffscreen(float camX, float halfView, float bossX, float bossRadius, out float side)
        {
            float dx = bossX - camX;
            side = dx < 0f ? -1f : 1f;
            return Mathf.Abs(dx) > halfView + bossRadius * 0.5f - 0.3f;
        }

        void LateUpdate()
        {
            var boss = game != null ? game.ActiveBoss : null;
            var cam = Camera.main;
            if (boss == null || boss.IsDead || cam == null || game.Player == null || game.State == GameState.GameOver)
            {
                if (Visible) Apply(false, 0f, 0.45f, "");
                return;
            }
            float halfView = cam.orthographicSize * cam.aspect;
            Vector2 bossPos = boss.transform.position;
            if (!IsOffscreen(cam.transform.position.x, halfView, bossPos.x, boss.Def.radius, out float side))
            {
                if (Visible) Apply(false, 0f, 0.45f, "");
                return;
            }
            float vy = Mathf.Clamp(cam.WorldToViewportPoint(bossPos).y, 0.3f, 0.62f);
            int meters = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(bossPos.x - game.Player.Position.x)));
            string text = side < 0f ? $"◀ 보스 {meters}m\n{boss.Def.name}" : $"보스 {meters}m ▶\n{boss.Def.name}";
            Apply(true, side, vy, text);
            group.alpha = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
        }

        void Apply(bool visible, float side, float vy, string text)
        {
            Visible = visible;
            Side = visible ? side : 0f;
            group.alpha = visible ? 1f : 0f;
            if (!visible) return;
            label.text = text;
            var anchor = new Vector2(side < 0f ? 0f : 1f, vy);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(side < 0f ? 16f : -16f, 0f);
        }
    }
}
