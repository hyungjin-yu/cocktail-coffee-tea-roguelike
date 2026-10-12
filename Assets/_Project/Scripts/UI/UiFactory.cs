using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>코드로 uGUI 요소를 만드는 헬퍼. 프리팹 UI가 생기면 교체할 임시 계층.</summary>
    public static class UiFactory
    {
        static Font font;

        /// <summary>09 ui_focus SFX 훅을 버튼에 붙인다. 각 화면이 Button을 만드는 곳마다 한 줄만
        /// 추가하면 되게 하는 공용 진입점 — 이 프로젝트는 uGUI 요소를 화면마다 직접 만들고 공유
        /// CreateButton 헬퍼가 없어서, 새 헬퍼를 추가하는 대신 기존 생성 지점에 이 호출만 끼워 넣는다.</summary>
        public static void AddFocusSfx(Button button) => button.gameObject.AddComponent<UiFocusSfx>();

        /// <summary>08:50 "마우스 hover와 키보드 focus는 같은 청록 테두리" — Unity Selectable은 마우스
        /// hover(Highlighted)와 키보드/게임패드 포커스(Selected)를 서로 다른 색으로 다룬다. 지금까지 이
        /// 코드베이스의 모든 화면이 highlighted/pressed만 맞춰두고 selected는 기본값(사실상 무늬 없음)
        /// 그대로라 키보드로만 다니면 지금 뭐가 선택됐는지 안 보였다 — 한 곳에서 세 색을 전부 맞춘다.</summary>
        /// <summary>04 §8 UI 공용 아트를 Image에 입힌다(9-slice면 Sliced). tintKeep: 기존 색을 남길 비율(0=아트 원색).
        /// 아트가 없으면 false — 기존 단색 그대로.</summary>
        public static bool Skin(Image img, string assetId, float tintKeep)
        {
            if (img == null) return false;
            string cafeId = assetId == "ui_panel" ? "ui_cafe_panel" :
                assetId == "ui_button" ? "ui_cafe_button" :
                assetId == "ui_card_frame" ? "ui_cafe_card" : assetId;
            var sprite = CafeSprite(cafeId);
            bool cafe = sprite != null;
            if (!cafe) sprite = ArtLibrary.Sprite(assetId);
            if (sprite == null) return false;
            var original = img.color;
            img.sprite = sprite;
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (cafe) img.pixelsPerUnitMultiplier = 100f / 128f;
            var c = cafe ? Color.white : Color.Lerp(Color.white, original, tintKeep);
            c.a = original.a;
            img.color = c;
            return true;
        }

        public static bool SkinButton(Image img) => Skin(img, "ui_button", 0.35f);
        public static bool SkinPanel(Image img) => Skin(img, "ui_panel", 0.2f);
        public static bool SkinCard(Image img) => Skin(img, "ui_card_frame", 0.25f);
        public static bool SkinTab(Image img) => Skin(img, "ui_cafe_tab", 0f);

        public static void AddCafeBackdrop(RectTransform parent)
        {
            if (CafeSprite("ui_cafe_panel") == null) { CenterBand.Attach(parent); return; }
            var rt = Stretch("CafeBackdrop", parent);
            rt.offsetMin = new Vector2(24f, 24f);
            rt.offsetMax = new Vector2(-24f, -24f);
            var img = AddImage(rt, new Color(1f, 1f, 1f, 0.97f));
            SkinPanel(img);
            img.raycastTarget = false;
            CenterBand.Attach(parent);
        }

        public static string InputLabel(string label, string key) =>
            MobilePlatform.IsTouchUI ? label : label + " · " + key;

        public static void SkinHeader(Text label)
        {
            if (label == null || CafeSprite("ui_cafe_header") == null) return;
            var source = label.rectTransform;
            var plate = Stretch(source.name + "Plate", source.parent);
            plate.anchorMin = source.anchorMin; plate.anchorMax = source.anchorMax; plate.pivot = source.pivot;
            plate.sizeDelta = source.sizeDelta + new Vector2(0f, 16f);
            plate.anchoredPosition = source.anchoredPosition + new Vector2(0f, (source.pivot.y * 2f - 1f) * 8f);
            plate.SetSiblingIndex(source.GetSiblingIndex());
            var img = AddImage(plate, Color.white);
            Skin(img, "ui_cafe_header", 0f);
            img.raycastTarget = false;
            label.color = new Color(0.29f, 0.23f, 0.22f, 1f);
        }

        static Sprite CafeSprite(string id, string state = "normal") =>
            ArtLibrary.Get(id)?.Clip(state)?.frames[0] ?? ArtLibrary.Get(id)?.Clip("effect")?.frames[0];

        public static bool IsCafe(Image img, string id) => img != null &&
            (img.sprite == CafeSprite(id) || img.sprite == CafeSprite(id, "hover"));

        /// <summary>38: 구매 상태 칩 — 완료(어두운 버튼+초록 ✓ 글자), 구매 가능(밝은 크림 header 판+어두운 글자), 잠김(disabled 버튼+자물쇠).
        /// 색만으로 구분하지 않도록 글자/아이콘도 함께 바뀐다. 카페 스킨이 없으면 단색으로 대체.</summary>
        public static void SkinChip(Image img, string state)
        {
            if (img == null) return;
            var sprite = state == "buyable" ? CafeSprite("ui_cafe_header") :
                state == "locked" ? CafeSprite("ui_cafe_button", "disabled") : CafeSprite("ui_cafe_button");
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (sprite != null) img.pixelsPerUnitMultiplier = 100f / 128f;
            img.color = sprite != null ? (state == "owned" ? new Color(.78f, 1f, .82f, 1f) : Color.white)
                : state == "buyable" ? new Color(.93f, .83f, .62f) : state == "owned" ? new Color(.18f, .3f, .2f) : new Color(.25f, .22f, .24f);
        }

        static Sprite lockSprite;
        /// <summary>38: 잠금 자물쇠(12×14 픽셀, Point 필터). 폰트에 자물쇠 글리프가 없어 코드로 그린다 — 원본 아트 파일은 건드리지 않는다.</summary>
        public static Sprite LockSprite
        {
            get
            {
                if (lockSprite != null) return lockSprite;
                string[] rows =
                {
                    "....oooo....", "...obbbbo...", "..obo..obo..", "..obo..obo..", "..obo..obo..",
                    "oooooooooooo", "oyyyyyyyyyyo", "oyyyyddyyyyo", "oyyyyddyyyyo", "oyyyyddyyyyo",
                    "oyyyyyyyyyyo", "oyyyyyyyyyyo", "obbbbbbbbbbo", "oooooooooooo",
                };
                var tex = new Texture2D(12, 14, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 14; y++)
                    for (int x = 0; x < 12; x++)
                    {
                        char c = rows[13 - y][x];
                        tex.SetPixel(x, y, c == 'o' ? new Color(.16f, .1f, .1f) : c == 'b' ? new Color(.72f, .52f, .26f) :
                            c == 'y' ? new Color(.95f, .76f, .38f) : c == 'd' ? new Color(.3f, .2f, .16f) : Color.clear);
                    }
                tex.Apply();
                lockSprite = Sprite.Create(tex, new Rect(0, 0, 12, 14), new Vector2(.5f, .5f), 12f);
                lockSprite.name = "ui_lock_procedural";
                return lockSprite;
            }
        }

        // Persistent choices use the same brass/teal focus ring as pointer and keyboard focus.
        public static void SetChoice(Image img, bool selected, Color fallback)
        {
            if (IsCafe(img, "ui_cafe_card") || IsCafe(img, "ui_cafe_tab"))
            {
                img.color = Color.white;
                var focus = img.GetComponent<CafeUiFocus>();
                if (focus != null) focus.SetChosen(selected);
            }
            else img.color = fallback;
        }

        /// <summary>04 §8/09 UI 아이콘(128², Unity 텍스트와 별도). 아트가 없으면 null.</summary>
        public static Image AddIcon(Transform parent, string assetId, Vector2 anchor, Vector2 position, float size)
        {
            var sprite = ArtLibrary.Sprite(assetId);
            if (sprite == null) return null;
            var rt = Place($"Icon_{assetId}", parent, anchor, position, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static void ApplyHoverColors(Button button)
        {
            // 04 §8 ui_button(12px 9-slice): 단색 버튼 배경에 공용 버튼 아트를 입힌다(색은 일부 남겨 용도 구분 유지).
            if (button.targetGraphic is Image skinTarget && skinTarget.sprite == null) SkinButton(skinTarget);
            if (button.targetGraphic is Image target)
            {
                string id = IsCafe(target, "ui_cafe_tab") ? "ui_cafe_tab" :
                    IsCafe(target, "ui_cafe_button") ? "ui_cafe_button" : null;
                if (id != null)
                {
                    button.transition = Selectable.Transition.SpriteSwap;
                    button.spriteState = new SpriteState
                    {
                        highlightedSprite = CafeSprite(id, "hover"), selectedSprite = CafeSprite(id, "hover"),
                        pressedSprite = CafeSprite(id, "pressed"), disabledSprite = CafeSprite(id, "disabled"),
                    };
                    target.color = Color.white;
                }
                if (id == "ui_cafe_tab" || IsCafe(target, "ui_cafe_card"))
                {
                    var ring = CafeSprite("ui_cafe_focus");
                    if (ring != null && button.GetComponent<CafeUiFocus>() == null)
                        button.gameObject.AddComponent<CafeUiFocus>().Initialize(button, ring);
                }
                if (id != null) return;
            }
            var colors = button.colors;
            if (button.targetGraphic is Image card && IsCafe(card, "ui_cafe_card"))
            {
                colors.normalColor = colors.disabledColor = Color.white;
                colors.highlightedColor = colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                return;
            }
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => font = null;

        /// <summary>09:126 — 동봉 폰트(Noto Sans CJK KR Regular, OFL 1.1). OS 맑은고딕 설치 여부에
        /// 의존하지 않는다. 빌드 누락 등으로 못 찾으면(정상 상태에선 일어나면 안 됨) 게임이 아예 안
        /// 뜨는 것보단 OS 폰트로 대체하되 경고를 남긴다 — 그 경로를 타면 09가 요구하는 "동봉 폰트"
        /// 상태가 아니므로 BuildRelease의 검사에서 걸러진다.</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
                    if (font == null)
                    {
                        Debug.LogWarning("[CCT] 동봉 폰트(Resources/Fonts/NotoSansKR-Regular)를 못 찾아 OS 폰트로 대체합니다.");
                        font = Font.CreateDynamicFontFromOSFont(
                            new[] { "Malgun Gothic", "Apple SD Gothic Neo", "NanumGothic", "Arial" }, 32);
                    }
                }
                return font;
            }
        }

        /// <summary>QA r6(2026-10-05, 720p): VerticalWrapMode.Truncate 상자가 폰트 실제 줄 높이(Noto Sans KR ≈ 1.45em)보다
        /// 낮아 카드 이름·재료 합계가 통째로 사라졌다. 잘리는 상자 높이는 이 값(실제 폰트 측정, 1080 기준)으로 정한다.</summary>
        public static float LineHeight(int fontSize, int lines = 1)
        {
            var f = Font;
            if (f == null) return fontSize * 1.5f * lines;
            var settings = new TextGenerationSettings
            {
                font = f,
                fontSize = fontSize,
                fontStyle = FontStyle.Normal,
                lineSpacing = 1f,
                scaleFactor = 1f,
                richText = false,
                textAnchor = TextAnchor.UpperLeft,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = new Vector2(4000f, 4000f),
                pivot = Vector2.zero,
                updateBounds = true,
                generateOutOfBounds = true,
                resizeTextForBestFit = false,
                color = Color.white,
            };
            var sample = "가Ay";
            for (int i = 1; i < lines; i++) sample += "\n가Ay";
            float h = new TextGenerator().GetPreferredHeight(sample, settings);
            return h > 0f ? h : fontSize * 1.5f * lines;
        }

        /// <summary>잘리는(Truncate) 글 상자 높이: 실제 줄 높이 × 1.2 + 4 — 720p 캔버스 배율(0.667)의 픽셀 반올림에도
        /// 마지막 줄이 사라지지 않게 하는 여유.</summary>
        public static float TruncatedBoxHeight(int fontSize, int lines = 1) => Mathf.Ceil(LineHeight(fontSize, lines) * 1.2f + 4f); // 정수 px — 행 경계 누적 오차 없음

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
            go.AddComponent<AdaptiveCanvasScaler>(); // 2026-10-12 16:9보다 세로가 긴 화면(폴드 펼침)은 폭 맞춤

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
            text.color = color == Color.white && CafeSprite("ui_cafe_panel") != null
                ? new Color(248f / 255f, 241f / 255f, 221f / 255f, 1f) : color;
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
            var frame = UiFactory.AddImage(Root, new Color(0f, 0f, 0f, 0.55f));
            bool cafe = UiFactory.Skin(frame, "ui_cafe_bar_frame", 0f);

            var track = cafe ? UiFactory.Stretch("FillTrack", Root) : Root;
            if (cafe) { track.offsetMin = new Vector2(6f, 6f); track.offsetMax = new Vector2(-6f, -6f); }
            fill = UiFactory.Stretch("Fill", track);
            var fillImage = UiFactory.AddImage(fill, fillColor);
            if (cafe) { UiFactory.Skin(fillImage, "ui_cafe_bar_fill", 0f); fillImage.color = fillColor; }

            var labelRt = UiFactory.Stretch("Label", Root);
            Label = UiFactory.AddText(labelRt, "", labelSize, TextAnchor.MiddleCenter, Color.white);
        }

        public void Set(float fill01)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
        }
    }
}
