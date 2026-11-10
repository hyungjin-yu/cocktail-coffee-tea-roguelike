using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT.Contest
{
    /// <summary>공모전판 UI 헬퍼. 본편 UiFactory(폰트·카페 UI 스킨)를 그대로 쓰고, 기존 DrinkLab 픽셀 아트만 불러온다.
    /// 좌표는 1920×1080 기준, 좌상단 기준점.</summary>
    public static class Kit
    {
        public static readonly Color Bg = new Color(0.12f, 0.09f, 0.08f, 1f);
        public static readonly Color PanelTint = new Color(0.23f, 0.17f, 0.15f, 0.96f);
        public static readonly Color Cream = new Color(248f / 255f, 241f / 255f, 221f / 255f, 1f);
        public static readonly Color Dim = new Color(0.78f, 0.72f, 0.64f, 1f);
        public static readonly Color Ok = new Color(0.55f, 0.85f, 0.5f, 1f);
        public static readonly Color Ng = new Color(1f, 0.48f, 0.42f, 1f);
        public static readonly Color Warn = new Color(1f, 0.78f, 0.36f, 1f);
        public static readonly Color Accent = new Color(0.36f, 0.82f, 0.76f, 1f);
        public static readonly Color Paper = new Color(0.95f, 0.91f, 0.82f, 1f);
        public static readonly Color Ink = new Color(0.2f, 0.15f, 0.12f, 1f);

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Art(string id)
        {
            if (cache.TryGetValue(id, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("DrinkLab/" + id);
            if (s == null) Debug.LogWarning("[PPS] missing sprite DrinkLab/" + id);
            cache[id] = s;
            return s;
        }

        public static string Tin(Product p) => p switch
        {
            Product.GreenTea => "dl_tin_green_tea",
            Product.Chamomile => "dl_tin_chamomile_flower",
            Product.BlackTea => "dl_tin_black_tea",
            _ => "dl_tin_coffee_bean",
        };

        public static string Cup(Vessel v) => v switch { Vessel.TeaCup => "dl_teacup", Vessel.Mug => "dl_coffee_mug", _ => "dl_coffee_cup" };

        public static Color Liquid(Product p) => p switch
        {
            Product.GreenTea => new Color(0.62f, 0.72f, 0.3f, 1f),
            Product.Chamomile => new Color(0.93f, 0.76f, 0.3f, 1f),
            Product.BlackTea => new Color(0.6f, 0.26f, 0.12f, 1f),
            _ => new Color(0.3f, 0.17f, 0.1f, 1f),
        };

        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
            return rt;
        }

        public static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, bool skin = true)
        {
            var rt = Rect(name, parent, x, y, w, h);
            var img = UiFactory.AddImage(rt, PanelTint);
            if (skin) UiFactory.SkinPanel(img);
            img.raycastTarget = false;
            return rt;
        }

        public static RectTransform PaperPanel(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = Rect(name, parent, x, y, w, h);
            UiFactory.AddImage(rt, Paper).raycastTarget = false;
            return rt;
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size,
            Color color, TextAnchor anchor = TextAnchor.UpperLeft, string name = "Label")
        {
            var rt = Rect(name, parent, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFactory.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 1.05f;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, float x, float y, float w, float h,
            UnityEngine.Events.UnityAction onClick, int size = 30, bool interactable = true, Color? tint = null)
        {
            var rt = Rect(name, parent, x, y, w, h);
            var img = UiFactory.AddImage(rt, tint ?? new Color(0.45f, 0.3f, 0.22f, 1f));
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            UiFactory.ApplyHoverColors(b);
            UiFactory.AddFocusSfx(b);
            var t = Label(rt, label, 12, 4, w - 24, h - 8, size, Cream, TextAnchor.MiddleCenter, "Text");
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(18, size - 10);
            t.resizeTextMaxSize = size;
            if (onClick != null) b.onClick.AddListener(onClick);
            b.interactable = interactable;
            if (!interactable) t.color = new Color(Cream.r, Cream.g, Cream.b, 0.5f);
            return b;
        }

        public static Image Sprite(Transform parent, string id, float x, float y, float w, float h, string name = null)
        {
            var rt = Rect(name ?? id, parent, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Art(id);
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (img.sprite == null) img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        /// <summary>그릇 아트 + 기존 _mask(안쪽 단면) 스텐실로 자른 액체. fill 0~1.</summary>
        public static RectTransform FilledCup(Transform parent, Vessel v, Product p, float fill, float x, float y, float scale, string name = "Cup")
        {
            var id = Cup(v);
            var sprite = Art(id);
            float w = sprite != null ? sprite.rect.width * scale : 32f * scale;
            float h = sprite != null ? sprite.rect.height * scale : 32f * scale;
            var root = Rect(name, parent, x, y, w, h);
            var cup = root.gameObject.AddComponent<Image>();
            cup.sprite = sprite;
            cup.raycastTarget = false;
            var maskSprite = Art(id + "_mask");
            if (maskSprite != null && fill > 0f)
            {
                var mrt = UiFactory.Stretch("LiquidMask", root);
                var mimg = mrt.gameObject.AddComponent<Image>();
                mimg.sprite = maskSprite;
                mimg.raycastTarget = false;
                var mask = mrt.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var lrt = UiFactory.Stretch("Liquid", mrt);
                lrt.anchorMax = new Vector2(1f, Mathf.Clamp01(fill));
                UiFactory.AddImage(lrt, Liquid(p)).raycastTarget = false;
            }
            return root;
        }

        public static void Frames(Image img, string prefix, int count, float fps)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++) frames[i] = Art(prefix + i.ToString("00"));
            var a = img.gameObject.AddComponent<FrameAnim>();
            a.frames = frames;
            a.fps = fps;
            img.sprite = frames[0];
        }
    }

    /// <summary>기존 프레임 시퀀스(김·붓기) 재생. 새 VFX가 아니라 본편 DrinkLab 프레임 재사용.</summary>
    public sealed class FrameAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 8f;
        Image img;

        void Awake() => img = GetComponent<Image>();

        void Update()
        {
            if (frames == null || frames.Length == 0 || img == null) return;
            int i = Mathf.FloorToInt(Time.time * fps) % frames.Length;
            if (frames[i] != null) img.sprite = frames[i];
        }
    }

    /// <summary>누르고 있는 동안 진행하는 버튼(붓기). 마우스 누름 또는 키보드 Space 유지.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData e) => Held = true;
        public void OnPointerUp(PointerEventData e) => Held = false;
        public void OnPointerExit(PointerEventData e) => Held = false;
        void OnDisable() => Held = false;
    }
}
