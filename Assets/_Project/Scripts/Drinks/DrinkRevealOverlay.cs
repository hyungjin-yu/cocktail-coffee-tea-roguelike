using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69(2026-10-19 사용자 수정) 출발 직전 완성 음료 공개 + 버프 획득. 손·얼굴·마시는 동작은 쓰지 않는다(dl_drink_sip/dl_hand 제외).
    /// dl_drink_reveal 프레임(빈 잔) 아래에 프레임별 마스크로 자른 완성 색·층을 그린다. 표시 전용 — 음용 기록은 출발 전
    /// DrinkLab.ReserveForDeparture(디스크 확정), 버프 적용은 GameManager.Initialize 한 번뿐. 클릭/Enter/Esc로 건너뛸 수 있다.
    /// </summary>
    public class DrinkRevealOverlay : MonoBehaviour
    {
        Canvas canvas;
        Action done;
        bool skip, finished;
        public static bool SkipForTests;
        public static DrinkRevealOverlay Current { get; private set; }
        public int FramesShown { get; private set; }
        public string BuffLine { get; private set; }

        public static void Play(PreparedDrink drink, Action done)
        {
            if (drink == null || SkipForTests || !Application.isPlaying) { done?.Invoke(); return; }
            var canvas = UiFactory.CreateCanvas(null, "DrinkReveal", 150);
            var o = canvas.gameObject.AddComponent<DrinkRevealOverlay>();
            o.canvas = canvas;
            o.done = done;
            Current = o;
            o.StartCoroutine(o.Run(drink));
        }

        public void Skip() => skip = true;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) skip = true;
        }

        IEnumerator Run(PreparedDrink drink)
        {
            var root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.04f, 0.03f, 0.05f, 0.92f)).raycastTarget = true;
            root.gameObject.AddComponent<ClickToSkip>().Owner = this;
            var r = drink.recipe;
            var look = DrinkCalculator.Look(r);
            var buff = DrinkCalculator.Buff(r);
            UiFactory.AddText(UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1200f, 70f)), $"완성 — {r.name}", 48, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            UiFactory.AddText(UiFactory.Place("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 40f)), "클릭·Enter로 건너뛰기", 22, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
            var stage = UiFactory.Place("Stage", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(448f, 448f));

            var frames = DrinkArt.Frames("dl_drink_reveal");
            bool tea = r.Kind == DrinkKind.Tea;
            float t = 0f;
            if (frames.Length > 1 && !tea)
            {
                // 프레임별 마스크(잔 안쪽) → 완성 색 층을 그 안에 그린다. 마스크/프레임 크기 동일(64px × 7).
                var size = new Vector2(frames[0].rect.width * 7, frames[0].rect.height * 7);
                var liquidRt = UiFactory.Place("Liquid", stage, new Vector2(0.5f, 0.5f), Vector2.zero, size);
                var maskImg = liquidRt.gameObject.AddComponent<Image>();
                maskImg.raycastTarget = false;
                liquidRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                BuildLayers(liquidRt, look.layers);
                var frameImg = UiFactory.Place("Glass", stage, new Vector2(0.5f, 0.5f), Vector2.zero, size).gameObject.AddComponent<Image>();
                frameImg.raycastTarget = false;
                while (!skip)
                {
                    t += Time.unscaledDeltaTime;
                    int i = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * 10f));
                    frameImg.sprite = frames[i];
                    var m = DrinkArt.Sprite($"dl_drink_reveal_mask_{i:00}");
                    maskImg.sprite = m;
                    liquidRt.gameObject.SetActive(m != null);
                    FramesShown = i + 1;
                    if (i == frames.Length - 1 && t > frames.Length / 10f + 0.4f) break;
                    yield return null;
                }
            }
            else
            {
                // 차(찻잔) 또는 프레임이 없을 때: 완성 그릇이 아래에서 떠오르며 채워진 채로 놓인다(마시는 동작 없음).
                string vesselId = tea ? "dl_teacup" : "dl_glass_cocktail";
                var v = DrinkVesselView.Create(stage, vesselId, new Vector2(0f, -200f), tea ? 190f : 150f, 7);
                v.SetLayers(look.layers);
                v.SetIce(!tea ? r.ice : 0);
                while (t < 1.0f && !skip)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                    v.Rect.anchoredPosition = new Vector2(0f, -260f + 60f * k);
                    v.SetEffects(0f, 0f, 0.4f * (1f - k));
                    FramesShown = 1;
                    yield return null;
                }
                v.Rect.anchoredPosition = new Vector2(0f, -200f);
            }

            var gain = DrinkArt.Frames("dl_buff_gain");
            var gainImg = UiFactory.Place("BuffGain", root, new Vector2(0.5f, 0.5f), new Vector2(260f, 180f), new Vector2(192f, 192f)).gameObject.AddComponent<Image>();
            gainImg.raycastTarget = false;
            gainImg.enabled = gain.Length > 0;
            BuffLine = $"버프 획득 — {buff.Describe()}";
            UiFactory.AddText(UiFactory.Place("BuffText", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1400f, 120f)),
                BuffLine + "\n(이번 판 동안 유지 · 5마리 처치 전에 포기하면 음료 보존)", 30, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f));
            t = 0f;
            while (t < 1.1f && !skip)
            {
                t += Time.unscaledDeltaTime;
                if (gain.Length > 0) gainImg.sprite = gain[Mathf.Min(gain.Length - 1, Mathf.FloorToInt(t / 0.8f * gain.Length))];
                yield return null;
            }
            Finish();
        }

        static void BuildLayers(RectTransform parent, IList<DrinkLayer> layers)
        {
            float total = 0f;
            foreach (var l in layers) total += Mathf.Max(0f, l.volume);
            if (total <= 0f) return;
            // 마스크 안쪽 대략 높이(잔 내부: 캔버스 아래 25%~위 85%)에 부피 비례로 쌓는다.
            float y = 0.25f;
            foreach (var l in layers)
            {
                var rt = UiFactory.Stretch("Layer", parent);
                float h = 0.6f * Mathf.Max(0f, l.volume) / total;
                rt.anchorMin = new Vector2(0f, y);
                rt.anchorMax = new Vector2(1f, y + h + (l.Equals(layers[layers.Count - 1]) ? 0.15f : 0f));
                y += h;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = DrinkVesselView.White;
                img.color = l.color;
                img.raycastTarget = false;
            }
            var below = UiFactory.Stretch("Bottom", parent);
            below.anchorMax = new Vector2(1f, 0.25f);
            var bimg = below.gameObject.AddComponent<Image>();
            bimg.sprite = DrinkVesselView.White;
            bimg.color = layers[0].color;
            bimg.raycastTarget = false;
        }

        public void Finish()
        {
            if (finished) return;
            finished = true;
            if (Current == this) Current = null;
            var cb = done;
            Destroy(canvas.gameObject);
            cb?.Invoke();
        }

        class ClickToSkip : MonoBehaviour, IPointerClickHandler
        {
            public DrinkRevealOverlay Owner;
            public void OnPointerClick(PointerEventData e) { if (Owner != null) Owner.skip = true; }
        }
    }

    /// <summary>출발 전 음용 기록 저장 실패: 런을 시작하지 않고 다시 시도 / 음료 없이 출발(음료 보존) / 취소를 고르게 한다.</summary>
    public class DrinkDepartDialog : MonoBehaviour
    {
        public static DrinkDepartDialog Current { get; private set; }
        Canvas canvas;
        public Button RetryButton { get; private set; }
        public Button NoDrinkButton { get; private set; }
        public Button CancelButton { get; private set; }
        public string Message { get; private set; }

        public static DrinkDepartDialog Show(string error, Action retry, Action noDrink, Action cancel)
        {
            var canvas = UiFactory.CreateCanvas(null, "DrinkDepartDialog", 160);
            var d = canvas.gameObject.AddComponent<DrinkDepartDialog>();
            d.canvas = canvas;
            Current = d;
            var root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0f, 0f, 0f, 0.8f));
            var panel = UiFactory.Place("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 420f));
            UiFactory.SkinPanel(UiFactory.AddImage(panel, new Color(0.14f, 0.1f, 0.12f, 1f)));
            d.Message = "준비 음료를 마신 기록을 저장하지 못해 아직 출발하지 않았습니다.\n" + (error ?? "") + "\n음료는 그대로 남아 있습니다.";
            UiFactory.AddText(UiFactory.Place("Text", panel, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1020f, 200f)), d.Message, 28, TextAnchor.MiddleCenter, Color.white);
            d.RetryButton = Btn(panel, "Retry", -360f, "다시 시도", new Color(0.55f, 0.32f, 0.14f), () => d.Close(retry));
            d.NoDrinkButton = Btn(panel, "NoDrink", 0f, "음료 없이 출발", new Color(0.3f, 0.3f, 0.4f), () => d.Close(noDrink));
            d.CancelButton = Btn(panel, "Cancel", 360f, "취소", new Color(0.3f, 0.26f, 0.3f), () => d.Close(cancel));
            UiInitialFocus.Offer(d.RetryButton.gameObject);
            return d;
        }

        static Button Btn(Transform parent, string name, float x, string label, Color color, Action onClick)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 0f), new Vector2(x, 40f), new Vector2(320f, 84f));
            var bg = UiFactory.AddImage(rt, color);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            UiFactory.AddFocusSfx(b);
            UiFactory.ApplyHoverColors(b);
            b.onClick.AddListener(() => onClick());
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 30, TextAnchor.MiddleCenter, Color.white);
            return b;
        }

        void Close(Action next)
        {
            if (Current == this) Current = null;
            Destroy(canvas.gameObject);
            next?.Invoke();
        }
    }
}
