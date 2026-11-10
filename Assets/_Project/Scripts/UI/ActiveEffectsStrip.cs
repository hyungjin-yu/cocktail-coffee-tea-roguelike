using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-09 지속 효과 칸(HUD 왼쪽 아래, 터치는 가운데 아래). 실제 상태(ActiveEffects.Collect)만 그린다.
    /// - 칸 = 짧은 이름 + 아이콘(기존 잔 아트/도형) + 짧은 정수 요약. 종류는 색이 아니라 모양으로도 구분:
    ///   이번 판(테두리) · 시간 제한(아래 남은 시간 막대) · 쌓임(칸 점) · 준비(위아래 굵은 띠) · 무효(회색 아이콘 + "무효").
    /// - 보통 화면 6칸, 좁은 화면(글자 확대가 필요한 배율) 4칸. 넘치면 마지막 칸이 "+N" 묶음. 누르면(또는 [H]) 상세 창.
    /// - 전투 화면에서만 보이고(일시정지·카드 창 위에 올라오지 않음), 0.1초마다 갱신(칸 오브젝트 재사용).
    /// </summary>
    public class ActiveEffectsStrip : MonoBehaviour
    {
        public const float TileW = 58f, TileH = 66f, Gap = 6f;
        /// <summary>데스크톱 아래 기준 높이: 보통 126(조작 상자 위), 좁은 화면 240(확대된 재료 알림 150..230 위로 피함).</summary>
        public const float DesktopY = 126f, DesktopNarrowY = 240f;
        public const int MaxTiles = 6, MaxTilesNarrow = 4;
        const int LabelFont = 14, ValueFont = 14;

        sealed class Tile
        {
            public RectTransform root;
            public Image bg, icon, drainBack, drain, bandTop, bandBottom;
            public Image[] border = new Image[4];
            public Image[] pips = new Image[10];
            public Text label, value;
        }

        GameManager game;
        RectTransform rt;
        readonly List<Tile> tiles = new List<Tile>();
        float nextRefresh;
        List<ActiveEffect> current = new List<ActiveEffect>();
        bool touch;
        CanvasGroup group;

        /// <summary>테스트/검수용: 마지막으로 그린 효과(정렬 순)·보이는 칸 수·묶인 수.</summary>
        public IReadOnlyList<ActiveEffect> Current => current;
        public int VisibleTiles { get; private set; }
        public int Overflow { get; private set; }
        /// <summary>보이는지(자기 오브젝트를 끄면 Update가 멈추므로 CanvasGroup으로 숨긴다).</summary>
        public bool Visible => rt != null && rt.gameObject.activeInHierarchy && group.alpha > 0f && VisibleTiles > 0;
        public string TileText(int i) => i < tiles.Count && tiles[i].root.gameObject.activeSelf ? tiles[i].label.text + "|" + tiles[i].value.text : "";
        public RectTransform Rect => rt;

        public static ActiveEffectsStrip Create(RectTransform hudRoot, GameManager game)
        {
            bool touch = MobilePlatform.IsTouchUI;
            var anchor = touch ? new Vector2(0.5f, 0f) : new Vector2(0f, 0f);
            // 데스크톱: 조작 방식 상자(30..114) 위. 터치: 왼쪽 아래는 조이스틱 — 음료 발동 알림(242..300) 위 가운데.
            var rt = UiFactory.Place("ActiveEffects", hudRoot, anchor, touch ? new Vector2(0f, 306f) : new Vector2(30f, DesktopY),
                new Vector2(MaxTiles * (TileW + Gap), TileH));
            var view = rt.gameObject.AddComponent<ActiveEffectsStrip>();
            view.game = game;
            view.rt = rt;
            view.touch = touch;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            // 칸 전체를 누르면 상세(일시정지 + 이번 런 효과 창). 투명 배경으로 누를 영역만 잡는다.
            var hit = UiFactory.AddImage(rt, new Color(0f, 0f, 0f, 0f));
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => view.game?.OpenHiddenDetails());
            for (int i = 0; i < MaxTiles; i++) view.tiles.Add(view.MakeTile(i));
            view.Refresh();
            return view;
        }

        Tile MakeTile(int i)
        {
            var t = new Tile();
            t.root = UiFactory.Place("Effect" + i, rt, new Vector2(0f, 0f), new Vector2(i * (TileW + Gap), 0f), new Vector2(TileW, TileH));
            t.bg = UiFactory.AddImage(t.root, new Color(0.08f, 0.06f, 0.1f, 0.88f));
            t.bg.raycastTarget = false;
            Vector2[] pos = { new Vector2(0f, TileH - 2f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(TileW - 2f, 0f) };
            Vector2[] size = { new Vector2(TileW, 2f), new Vector2(TileW, 2f), new Vector2(2f, TileH), new Vector2(2f, TileH) };
            for (int k = 0; k < 4; k++) t.border[k] = Bar(t.root, "Border" + k, pos[k], size[k]);
            t.bandTop = Bar(t.root, "ReadyTop", new Vector2(0f, TileH - 5f), new Vector2(TileW, 5f));
            t.bandBottom = Bar(t.root, "ReadyBottom", new Vector2(0f, 0f), new Vector2(TileW, 5f));
            var labelRt = UiFactory.Place("Label", t.root, new Vector2(0f, 0f), new Vector2(1f, TileH - 18f), new Vector2(TileW - 2f, 17f));
            t.label = UiFactory.AddText(labelRt, "", LabelFont, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.75f));
            ShrinkToFit(t.label, 10);
            var iconRt = UiFactory.Place("Icon", t.root, new Vector2(0f, 0f), new Vector2((TileW - 26f) * 0.5f, 22f), new Vector2(26f, 24f));
            t.icon = UiFactory.AddImage(iconRt, Color.white);
            t.icon.preserveAspect = true;
            t.icon.raycastTarget = false;
            var valueRt = UiFactory.Place("Value", t.root, new Vector2(0f, 0f), new Vector2(1f, 5f), new Vector2(TileW - 2f, 17f));
            t.value = UiFactory.AddText(valueRt, "", ValueFont, TextAnchor.MiddleCenter, Color.white);
            ShrinkToFit(t.value, 10);
            t.drainBack = Bar(t.root, "DrainBack", new Vector2(2f, 2f), new Vector2(TileW - 4f, 3f));
            t.drainBack.color = new Color(1f, 1f, 1f, 0.18f);
            t.drain = Bar(t.root, "Drain", new Vector2(2f, 2f), new Vector2(TileW - 4f, 3f));
            for (int k = 0; k < t.pips.Length; k++)
                t.pips[k] = Bar(t.root, "Pip" + k, new Vector2(4f + k * 5.2f, 2f), new Vector2(4f, 3f));
            return t;
        }

        static void ShrinkToFit(Text t, int min)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; // 긴 한글("사거리+20%")도 칸 폭 안에서 글자만 줄인다(겹침·잘림 방지)
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = t.fontSize;
        }

        static Image Bar(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var img = UiFactory.AddImage(UiFactory.Place(name, parent, new Vector2(0f, 0f), pos, size), Color.white);
            img.raycastTarget = false;
            return img;
        }

        void Update()
        {
            bool combat = DrinkRunHud.CombatVisible(game);
            group.alpha = combat ? 1f : 0f;
            group.blocksRaycasts = group.interactable = combat;
            if (!combat || Time.unscaledTime < nextRefresh) return;
            Refresh();
        }

        public void RefreshForTests() => Refresh();

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.1f;
            current = ActiveEffects.Collect(game);
            float canvasScale = rt.GetComponentInParent<Canvas>() != null ? rt.GetComponentInParent<Canvas>().scaleFactor : 1f;
            float k = DrinkRunHud.ReadableScale(LabelFont, canvasScale);
            rt.localScale = new Vector3(k, k, 1f);
            bool narrow = k > 1.2f;
            int max = narrow ? MaxTilesNarrow : MaxTiles;
            if (!touch) rt.anchoredPosition = new Vector2(30f, narrow ? DesktopNarrowY : DesktopY);
            int shown = ActiveEffects.VisibleCount(current.Count, max);
            Overflow = current.Count - shown;
            int used = shown + (Overflow > 0 ? 1 : 0);
            VisibleTiles = used;
            float width = Mathf.Max(0f, used * (TileW + Gap) - Gap);
            rt.sizeDelta = new Vector2(width, TileH);
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                bool on = i < used;
                if (t.root.gameObject.activeSelf != on) t.root.gameObject.SetActive(on);
                if (!on) continue;
                if (i < shown) Apply(t, current[i]);
                else ApplyOverflow(t, Overflow);
            }
        }

        void Apply(Tile t, ActiveEffect e)
        {
            t.label.text = e.label;
            t.value.text = e.value;
            bool sup = e.kind == ActiveEffectKind.Suppressed;
            t.value.color = sup ? new Color(1f, 0.55f, 0.55f) : Color.white;
            t.icon.sprite = e.icon != null ? e.icon : ShapeFor(e.id);
            t.icon.color = sup ? new Color(0.5f, 0.5f, 0.5f, 0.9f) : e.icon != null ? Color.white : e.accent;
            foreach (var b in t.border) { b.enabled = e.kind == ActiveEffectKind.Run; b.color = e.accent; }
            t.bandTop.enabled = t.bandBottom.enabled = e.kind == ActiveEffectKind.Ready;
            t.bandTop.color = t.bandBottom.color = e.accent;
            bool timed = e.kind == ActiveEffectKind.Timed && e.duration > 0f;
            t.drainBack.enabled = t.drain.enabled = timed;
            if (timed)
            {
                t.drain.color = e.accent;
                t.drain.rectTransform.sizeDelta = new Vector2((TileW - 4f) * Mathf.Clamp01(e.remaining / e.duration), 3f);
            }
            for (int k = 0; k < t.pips.Length; k++)
            {
                bool pip = e.kind == ActiveEffectKind.Stack && k < e.maxStacks;
                t.pips[k].enabled = pip;
                if (pip) t.pips[k].color = k < e.stacks ? e.accent : new Color(1f, 1f, 1f, 0.2f);
            }
        }

        void ApplyOverflow(Tile t, int n)
        {
            t.label.text = "더보기";
            t.value.text = $"+{n}";
            t.value.color = Color.white;
            t.icon.sprite = SpriteFactory.Square;
            t.icon.color = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            foreach (var b in t.border) { b.enabled = true; b.color = new Color(0.8f, 0.8f, 0.8f, 0.6f); }
            t.bandTop.enabled = t.bandBottom.enabled = t.drainBack.enabled = t.drain.enabled = false;
            foreach (var p in t.pips) p.enabled = false;
        }

        /// <summary>아트가 없는 효과의 도형(이름 글자와 함께 써서 색만으로 구분하지 않는다).</summary>
        static Sprite ShapeFor(string id) => id switch
        {
            "iced_cup" => SpriteFactory.Circle,
            "shake_rhythm" => SpriteFactory.Crescent,
            "weekend_rush" => SpriteFactory.Capsule,
            "herb_garden" => SpriteFactory.Semicircle,
            _ => SpriteFactory.Square,
        };
    }
}
