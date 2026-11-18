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
        Image portrait;
        Text arrow;
        /// <summary>테스트/보고용 — 초상 스프라이트(없으면 null).</summary>
        public Sprite PortraitSprite => portrait != null && portrait.enabled ? portrait.sprite : null;

        /// <summary>테스트/보고용 — 지금 보이는지와 가리키는 쪽(−1 왼쪽/+1 오른쪽, 숨김이면 0).</summary>
        public bool Visible { get; private set; }
        public float Side { get; private set; }
        public string LabelText => label == null ? "" : (Side < 0f && arrow != null ? arrow.text + " " : "") + label.text + (Side > 0f && arrow != null ? " " + arrow.text : "");

        public static BossDirectionIndicator Create(Transform hudRoot, GameManager game)
        {
            // 2026-11-15 피드백 15: 보스 초상 + 큰 방향 화살표 + 경고 테두리 + 거리·이름(이전: 글 상자만). 등장 소리는 기존 boss_warning(등장 순간 1회).
            var rt = UiFactory.Place("BossDirection", hudRoot, new Vector2(1f, 0.45f), Vector2.zero, new Vector2(360f, 104f));
            var frame = UiFactory.AddImage(rt, new Color(1f, 0.45f, 0.3f, 0.95f));
            frame.raycastTarget = false;
            var inner = UiFactory.Stretch("Inner", rt);
            inner.offsetMin = new Vector2(4f, 4f); inner.offsetMax = new Vector2(-4f, -4f);
            var bg = UiFactory.AddImage(inner, new Color(0.3f, 0.04f, 0.07f, 0.95f));
            bg.raycastTarget = false;
            var pRt = UiFactory.Place("Portrait", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
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
            view.portrait = pRt.gameObject.AddComponent<Image>();
            view.portrait.preserveAspect = true; view.portrait.raycastTarget = false;
            view.arrow = UiFactory.AddText(UiFactory.Place("Arrow", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 84f)), "▶", 44, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.45f));
            view.arrow.raycastTarget = false;
            view.Apply(false, 0f, 0.45f, "");
            return view;
        }

        /// <summary>순수 판정 — 보스 중심이 화면 안(반지름의 절반 이상 보임)인지. 화면 밖이면 side=보스 쪽.</summary>
        EnemyDef lastDef;

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
            // 일시정지·카드·연구·제조·안내 화면 위에는 띄우지 않는다(전투 HUD가 보일 때만).
            if (boss == null || boss.IsDead || cam == null || game.Player == null || game.State == GameState.GameOver || !DrinkRunHud.CombatVisible(game))
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
            int meters = Mathf.Max(1, IntNum.Round(Mathf.Abs(bossPos.x - game.Player.Position.x)));
            string text = $"보스 {meters}m\n{boss.Def.name}";
            if (portrait.sprite == null || lastDef != boss.Def) { lastDef = boss.Def; portrait.sprite = HudIcons.Enemy(boss.Def); portrait.enabled = portrait.sprite != null; }
            Apply(true, side, vy, text);
            group.alpha = HitFeedback.LowEffects ? 1f : 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
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
            // 화살표는 화면 끝 쪽(보스 방향), 초상은 그 안쪽, 글은 나머지(왼쪽 보스: ◀ 초상 글 / 오른쪽: 글 초상 ▶).
            float w = rt.sizeDelta.x;
            arrow.text = side < 0f ? "◀" : "▶";
            ((RectTransform)arrow.transform).anchoredPosition = new Vector2(side < 0f ? -w * 0.5f + 30f : w * 0.5f - 30f, 0f);
            ((RectTransform)portrait.transform).anchoredPosition = new Vector2(side < 0f ? -w * 0.5f + 104f : w * 0.5f - 104f, 0f);
            var lr = label.rectTransform;
            lr.offsetMin = new Vector2(side < 0f ? 152f : 12f, 6f);
            lr.offsetMax = new Vector2(side < 0f ? -12f : -152f, -6f);
        }
    }
}
