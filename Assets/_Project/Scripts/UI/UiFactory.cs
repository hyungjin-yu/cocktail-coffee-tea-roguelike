using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>코드로 uGUI 요소를 만드는 헬퍼. 프리팹 UI가 생기면 교체할 임시 계층.</summary>
    public static class UiFactory
    {
        static Font font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => font = null;

        /// <summary>한글이 나오는 OS 폰트. 정식 폰트 에셋이 정해지기 전까지의 임시 처리.</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "Apple SD Gothic Neo", "NanumGothic", "Arial" }, 32);
                }
                return font;
            }
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>부모 전체를 채우는 RectTransform.</summary>
        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>한 점(anchor)에 고정되는 RectTransform. pivot 은 anchor 와 동일하게 둔다.</summary>
        public static RectTransform Place(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image AddImage(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text AddText(RectTransform rt, string content, int size, TextAnchor anchor, Color color)
        {
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }

    /// <summary>배경 위에 왼쪽부터 채워지는 막대.</summary>
    public class FillBar
    {
        readonly RectTransform fill;

        public RectTransform Root { get; }
        public Text Label { get; }

        public FillBar(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            Color fillColor, int labelSize)
        {
            Root = UiFactory.Place(name, parent, anchor, position, size);
            UiFactory.AddImage(Root, new Color(0f, 0f, 0f, 0.55f));

            fill = UiFactory.Stretch("Fill", Root);
            UiFactory.AddImage(fill, fillColor);

            var labelRt = UiFactory.Stretch("Label", Root);
            Label = UiFactory.AddText(labelRt, "", labelSize, TextAnchor.MiddleCenter, Color.white);
        }

        public void Set(float fill01)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
        }
    }
}
