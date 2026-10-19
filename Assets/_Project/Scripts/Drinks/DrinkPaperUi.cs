using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69/72/73 단계2 종이 UI(Codex ProgressionUI-2026-10-19 ready 아트). 재료 종이 카드·영수증·찢긴 레시피 카드는 9slice(모서리 고정, 가운데 늘림),
    /// 스프링 노트·보관함·모션 프레임은 균일 최근접 확대만 한다. 글자·숫자는 모두 실제 UI Text이며 이미지에 굽지 않는다.
    /// 아트 1픽셀 = pixelScale 기준 화면 단위(1920×1080 기준). 본문 글자는 기준 27 이상(720p에서 실제 18px 이상).
    /// </summary>
    public static class DrinkPaperUi
    {
        public const string Folder = "ProgressionUI/";
        /// <summary>종이 위 잉크 색.</summary>
        public static readonly Color Ink = new Color(0.2f, 0.13f, 0.08f), InkMuted = new Color(0.42f, 0.33f, 0.25f),
            InkGood = new Color(0.12f, 0.42f, 0.18f), InkBad = new Color(0.66f, 0.14f, 0.08f), InkLive = new Color(0.12f, 0.33f, 0.62f);
        /// <summary>본문·작은 글자 기준 크기(1920×1080 기준; 720p에서 18px·16px).</summary>
        public const int Body = 27, Small = 24, Heading = 30;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); tear = null; }

        public static Sprite Sprite(string id)
        {
            if (cache.TryGetValue(id, out var s)) return s;
            s = Resources.Load<Sprite>(Folder + id);
            cache[id] = s;
            return s;
        }

        public static bool Ready => Sprite("receipt") != null;

        /// <summary>9slice 종이(테두리 = 아트 픽셀 × pixelScale). 아트가 없으면 종이색 단색(기능 유지, Placeholder로 보고).</summary>
        public static Image Sliced(RectTransform rt, string id, float pixelScale, Color? tint = null)
        {
            var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            var sp = Sprite(id);
            img.sprite = sp;
            img.type = sp != null ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, pixelScale);
            img.color = tint ?? (sp != null ? Color.white : new Color(0.93f, 0.86f, 0.7f));
            return img;
        }

        /// <summary>균일 확대(늘리지 않음): 크기 = 원본 × pixelScale.</summary>
        public static Image Uniform(RectTransform rt, string id, float pixelScale)
        {
            var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            var sp = Sprite(id);
            img.sprite = sp;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            if (sp != null) rt.sizeDelta = new Vector2(sp.rect.width * pixelScale, sp.rect.height * pixelScale);
            return img;
        }

        /// <summary>종이 버튼(button 9slice, 상태 표식 아트 선택 가능). 글자는 실제 Text(잉크).</summary>
        public static Button Button(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, int fontSize, Action onClick, string art = "button", Color? tint = null)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            var bg = Sliced(rt, art, 2f, tint);
            var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = bg;
            var colors = b.colors;
            colors.disabledColor = new Color(0.62f, 0.6f, 0.58f, 0.75f);
            b.colors = colors;
            UiFactory.AddFocusSfx(b);
            b.onClick.AddListener(() => onClick());
            var labelRt = UiFactory.Stretch("Label", rt);
            labelRt.offsetMin = new Vector2(10f, 2f); labelRt.offsetMax = new Vector2(-10f, -2f);
            var t = UiFactory.AddText(labelRt, label, fontSize, TextAnchor.MiddleCenter, Ink);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(fontSize, Small);
            t.resizeTextMaxSize = fontSize;
            return b;
        }

        /// <summary>선택/일반 상태 아트 교체(선택 = button_equipped 표식).</summary>
        public static void SetSelected(UnityEngine.UI.Button b, bool on)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image;
            if (img != null) img.sprite = Sprite(on ? "button_equipped" : "button") ?? img.sprite;
        }

        // ───── 레시피 찢기·입고 모션 ─────

        [Serializable] public class TearFrame { public float ms, x, y, w, h, scale, clipX, clipY, clipW, clipH; public bool text; }
        [Serializable] public class TearMotion { public int width, height; public List<TearFrame> frames = new List<TearFrame>(); }
        static TearMotion tear;

        public static TearMotion Tear
        {
            get
            {
                if (tear != null) return tear;
                var ta = Resources.Load<TextAsset>(Folder + "tear_motion");
                tear = ta != null ? JsonUtility.FromJson<TearMotion>(ta.text) : new TearMotion();
                return tear;
            }
        }
    }

    /// <summary>
    /// 레시피 저장 성공 뒤에만 재생하는 장식 모션(스프링 노트에서 한 장 찢어 보관함에 넣기, 0.5초). 저장·재료·음료를 만들거나 바꾸지 않는다.
    /// 종이 위 레시피 이름은 실제 Text이며 프레임별 종이 변환(위치·배율)과 보관함 앞면 가림(clip)을 그대로 따른다. 클릭하면 끝 상태로.
    /// </summary>
    public class RecipeTearOverlay : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public const float PixelScale = 2f;
        Image frame;
        RectTransform clip, textRt;
        Text label;
        bool skip;
        public static RecipeTearOverlay Current { get; private set; }
        public int FramesShown { get; private set; }
        public bool Finished { get; private set; }
        public string Label => label != null ? label.text : "";

        /// <summary>찢기 프레임 00~05(RecipeDrawer manifest tearStoreConnection) 뒤, 찢은 종이가 실제 글자를 단 채 레시피 서랍(레시피 탭)으로 날아가 꽂힌다.</summary>
        public const int TearFrames = 6;
        public const float FlySeconds = 0.45f;
        RectTransform flyTarget;

        public static RecipeTearOverlay Play(RectTransform parent, string recipeName, RectTransform drawerTarget = null)
        {
            var m = DrinkPaperUi.Tear;
            if (m.frames.Count == 0 || DrinkPaperUi.Sprite("recipe_tear_store_00") == null) return null;
            if (Current != null) Destroy(Current.gameObject);
            var rt = UiFactory.Place("RecipeTear", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(m.width * PixelScale, m.height * PixelScale));
            var o = rt.gameObject.AddComponent<RecipeTearOverlay>();
            Current = o;
            o.flyTarget = drawerTarget;
            o.Build(m, recipeName);
            o.StartCoroutine(o.Run(m));
            return o;
        }

        void Build(DrinkPaperUi.TearMotion m, string recipeName)
        {
            var rt = (RectTransform)transform;
            frame = UiFactory.Stretch("Frame", rt).gameObject.AddComponent<Image>();
            frame.raycastTarget = true; // 클릭 → 건너뛰기
            clip = new GameObject("Clip", typeof(RectTransform)).GetComponent<RectTransform>();
            clip.SetParent(rt, false);
            clip.anchorMin = clip.anchorMax = clip.pivot = new Vector2(0f, 1f); // 왼쪽 위 원점(아트 좌표와 같다)
            clip.gameObject.AddComponent<RectMask2D>();
            textRt = UiFactory.Place("PaperText", clip, new Vector2(0f, 1f), Vector2.zero, Vector2.one);
            textRt.pivot = new Vector2(0f, 1f);
            label = UiFactory.AddText(textRt, recipeName, DrinkPaperUi.Heading, TextAnchor.MiddleCenter, DrinkPaperUi.Ink);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = DrinkPaperUi.Heading;
            label.raycastTarget = false;
            Show(m, 0);
        }

        void Show(DrinkPaperUi.TearMotion m, int i)
        {
            var f = m.frames[i];
            frame.sprite = DrinkPaperUi.Sprite($"recipe_tear_store_{i:00}");
            clip.anchoredPosition = new Vector2(f.clipX * PixelScale, -f.clipY * PixelScale);
            clip.sizeDelta = new Vector2(f.clipW * PixelScale, f.clipH * PixelScale);
            textRt.anchoredPosition = new Vector2((f.x - f.clipX) * PixelScale, -(f.y - f.clipY) * PixelScale);
            textRt.sizeDelta = new Vector2(f.w * PixelScale, f.h * PixelScale);
            label.resizeTextMaxSize = Mathf.Max(8, Mathf.RoundToInt(DrinkPaperUi.Heading * f.scale));
            label.enabled = f.text;
            FramesShown = i + 1;
        }

        IEnumerator Run(DrinkPaperUi.TearMotion m)
        {
            int n = Mathf.Min(TearFrames, m.frames.Count);
            for (int i = 0; i < n; i++)
            {
                Show(m, skip ? n - 1 : i);
                if (skip) break;
                float t = 0f;
                while (t < m.frames[i].ms / 1000f && !skip) { t += Time.unscaledDeltaTime; yield return null; }
            }
            Show(m, n - 1); // 찢어 낸 종이(옛 보관함 구간 06~09는 쓰지 않는다)
            // 찢은 종이 → 레시피 서랍(레시피 탭)으로: 실제 글자와 함께 작아지며 이동(손 없음)
            frame.enabled = false;
            var rt = (RectTransform)transform;
            var paper = UiFactory.Place("TornSheet", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(172f * PixelScale, 128f * PixelScale));
            var pi = paper.gameObject.AddComponent<Image>();
            pi.sprite = DrinkPaperUi.Sprite("torn_recipe"); pi.raycastTarget = false;
            var tl = UiFactory.AddText(UiFactory.Stretch("Title", paper), label.text, DrinkPaperUi.Body, TextAnchor.MiddleCenter, DrinkPaperUi.Ink);
            tl.raycastTarget = false; tl.horizontalOverflow = HorizontalWrapMode.Wrap;
            clip.gameObject.SetActive(false);
            Vector2 from = Vector2.zero, to = new Vector2(0f, -rt.rect.height);
            if (flyTarget != null) to = rt.InverseTransformPoint(flyTarget.TransformPoint(flyTarget.rect.center));
            float ft = 0f;
            while (ft < FlySeconds && !skip)
            {
                ft += Time.unscaledDeltaTime;
                float e = Mathf.Clamp01(ft / FlySeconds); e = e * e * (3f - 2f * e);
                paper.anchoredPosition = Vector2.Lerp(from, to, e) + Vector2.up * Mathf.Sin(e * Mathf.PI) * 60f;
                paper.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, e);
                yield return null;
            }
            Destroy(paper.gameObject);
            Finished = true;
            float hold = 0f;
            while (hold < 0.1f && !skip) { hold += Time.unscaledDeltaTime; yield return null; }
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) => skip = true;
        public void Skip() => skip = true;
        void OnDestroy() { if (Current == this) Current = null; }
    }
}
