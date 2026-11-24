using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 정식 레시피 **첫 발견** 공개(제조 확정 저장 성공 직후에만 호출). 이 화면이 레시피 이름·시그니처가 처음 보이는 곳이다.
    /// 일반(약 2.6초): 어둡게 → 레시피 색 고리가 퍼지고 재료 이름이 하나씩 → 이름 크게 → 시그니처 한 줄 → "도감에 기록".
    /// 저이펙트(HitFeedback.LowEffects): 고리·광선·맥동 없이 글자만 순서대로(같은 정보, 같은 길이 이하).
    /// 클릭·Enter·Space·Esc로 건너뛴다. 실시간(unscaled) — 마스터 런 제조 창(일시정지) 위에서도 재생된다. 표시 전용(저장·판정 없음).
    /// 이미 발견한 레시피를 다시 만들면 짧은 알림만(PlayShort, 약 0.9초).
    /// </summary>
    public class SignatureRevealOverlay : MonoBehaviour
    {
        public const float FullSeconds = 2.6f, ShortSeconds = 0.9f;
        public static bool SkipForTests;
        public static SignatureRevealOverlay Current { get; private set; }
        /// <summary>마지막 공개 기록(검수용): "full:id" / "low:id" / "short:id".</summary>
        public static string LastShown { get; private set; } = "";

        Canvas canvas;
        Action done;
        bool skip, finished;

        public static void Play(SignatureDef def, Action done = null) => Show(def, false, done);
        public static void PlayShort(SignatureDef def, Action done = null) => Show(def, true, done);

        static void Show(SignatureDef def, bool shortForm, Action done)
        {
            if (def == null) { done?.Invoke(); return; }
            bool low = HitFeedback.LowEffects;
            LastShown = (shortForm ? "short:" : low ? "low:" : "full:") + def.id;
            if (SkipForTests || !Application.isPlaying) { done?.Invoke(); return; }
            Current?.Finish();
            var canvas = UiFactory.CreateCanvas(null, "SignatureReveal", 170);
            var o = canvas.gameObject.AddComponent<SignatureRevealOverlay>();
            o.canvas = canvas;
            o.done = done;
            Current = o;
            o.StartCoroutine(shortForm ? o.RunShort(def) : o.RunFull(def, low));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) skip = true;
        }

        /// <summary>최초 발견 시트 표시 이미지(검수용). 아트가 없으면 null(절차형 대체).</summary>
        public Image ArtImage { get; private set; }
        public string ArtClip { get; private set; }

        IEnumerator RunFull(SignatureDef def, bool low)
        {
            var root = UiFactory.Stretch("Root", canvas.transform);
            var dim = UiFactory.AddImage(root, new Color(0.03f, 0.02f, 0.04f, 0f));
            dim.raycastTarget = true;
            root.gameObject.AddComponent<ClickToSkip>().Owner = this;

            // #25 아트(ArtSource/signature-drinks-2026-11-24): 320×180 시트를 16:9 비율 그대로 화면 가운데에 맞춘다(720p 4배·1080p 6배 정수 확대, Point).
            // 저이펙트는 1.3초부터 같은 크기의 정적 잔만(입자·배경 장식·크기 변화 없음). 글자는 아트에 굽지 않고 아래 런타임 텍스트로.
            ArtClip = SignatureArt.DiscoveryClip(def.id, low);
            var artFrames = SignatureArt.Load(ArtClip);
            var artClip = SignatureArt.Get(ArtClip);
            if (artFrames != null)
            {
                ArtImage = UiFactory.AddImage(UiFactory.Stretch("Art", root), Color.white);
                ArtImage.sprite = artFrames[0];
                ArtImage.preserveAspect = true;
                ArtImage.raycastTarget = false;
                ArtImage.enabled = !low;
            }
            Image ring = null, ring2 = null;
            if (!low && artFrames == null)
            {
                // 아트 미임포트 대체(기존 절차형)
                ring = UiFactory.AddImage(UiFactory.Place("Ring", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(100f, 100f)), def.accent);
                ring.sprite = SpriteFactory.Circle; ring.raycastTarget = false;
                ring2 = UiFactory.AddImage(UiFactory.Place("Ring2", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(60f, 60f)), Color.white);
                ring2.sprite = SpriteFactory.Circle; ring2.raycastTarget = false;
            }
            // 글자 배치(HANDOFF 720p 시안 → 1080 기준 ×1.5): 제목 y113→170, 이름 586→879, 시그니처 636→954, 하단 안내 678→1017(위에서).
            // 재료 줄은 제목 아래, 분류는 제목 줄에 붙여 중앙 잔과 겹치지 않게 한다.
            var top = new Vector2(0.5f, 1f);
            var head = UiFactory.AddText(UiFactory.Place("Head", root, top, new Vector2(0f, -150f), new Vector2(1500f, 44f)), "정식 레시피 발견!", 34, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.5f));
            var ingredients = UiFactory.AddText(UiFactory.Place("Ingredients", root, top, new Vector2(0f, -200f), new Vector2(1500f, 40f)), "", 26, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.9f));
            var name = UiFactory.AddText(UiFactory.Place("Name", root, top, new Vector2(0f, -835f), new Vector2(1500f, 88f)), "", 64, TextAnchor.MiddleCenter, Color.white);
            var sig = UiFactory.AddText(UiFactory.Place("Signature", root, top, new Vector2(0f, -932f), new Vector2(1600f, 44f)), "", 30, TextAnchor.MiddleCenter, def.accent);
            var foot = UiFactory.AddText(UiFactory.Place("Foot", root, top, new Vector2(0f, -998f), new Vector2(1600f, 38f)), "", 22, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f));
            var skipHint = UiFactory.AddText(UiFactory.Place("Skip", root, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(420f, 32f)), "클릭·Enter로 건너뛰기", 20, TextAnchor.MiddleRight, new Color(0.7f, 0.7f, 0.7f));
            foreach (var tx in new[] { head, ingredients, name, sig, foot, skipHint }) { tx.raycastTarget = false; tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 16; tx.resizeTextMaxSize = tx.fontSize; }
            head.enabled = false;
            string category = string.IsNullOrEmpty(def.category) ? "IBA 공식 레시피" : $"IBA 공식 레시피 · {def.category}";

            float t0 = 0f;
            while (t0 < FullSeconds && !skip)
            {
                t0 += Time.unscaledDeltaTime;
                float ms = t0 * 1000f;
                dim.color = new Color(0.03f, 0.02f, 0.04f, Mathf.Min(0.9f, t0 / 0.3f * 0.9f));
                head.enabled = t0 >= 0.15f;
                // 0.3~1.2s 재료 이름이 하나씩(처음 공개). 1.3s 잔·이름(아트의 잔 등장과 같은 시각), 1.8s 시그니처, 2.2s 도감 기록.
                int shownIng = Mathf.Clamp(Mathf.FloorToInt((t0 - 0.3f) / 0.3f) + 1, 0, def.ingredientIds.Length);
                ingredients.text = IngredientNames(def, shownIng);
                if (t0 >= 1.3f) { name.text = def.name; head.text = "정식 레시피 발견! · " + category; }
                if (t0 >= 1.8f) sig.text = $"시그니처 · {def.effectName} — {def.effectShort}";
                if (t0 >= 2.2f) foot.text = "이 잔을 마시고 출발하면 전투에서 한 번 (Q 또는 시그니처 칸) · 도감에 기록했습니다";
                if (ArtImage != null)
                {
                    if (low) ArtImage.enabled = ms >= SignatureArt.LowDiscoveryStartMs;
                    else ArtImage.sprite = artFrames[SignatureArt.FrameAt(artClip, ms)];
                }
                if (ring != null)
                {
                    float rk = Mathf.Clamp01((t0 - 0.3f) / 0.9f);
                    ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(100f, 900f, rk);
                    var c = def.accent; c.a = 0.55f * (1f - rk); ring.color = c;
                    float r2 = Mathf.Clamp01((t0 - 1.25f) / 0.5f);
                    ring2.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(60f, 520f, r2);
                    ring2.color = new Color(1f, 1f, 1f, t0 < 1.25f ? 0f : 0.6f * (1f - r2));
                }
                yield return null;
            }
            Finish();
        }

        IEnumerator RunShort(SignatureDef def)
        {
            var rt = UiFactory.Place("Toast", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1000f, 90f));
            UiFactory.AddImage(rt, new Color(0.08f, 0.06f, 0.04f, 0.92f)).raycastTarget = false;
            var text = UiFactory.AddText(UiFactory.Stretch("Text", rt), $"정식 레시피 · {def.name}\n시그니처 · {def.effectName} 1회 준비", 28, TextAnchor.MiddleCenter, def.accent);
            text.raycastTarget = false;
            float t0 = 0f;
            while (t0 < ShortSeconds && !skip) { t0 += Time.unscaledDeltaTime; yield return null; }
            Finish();
        }

        static string IngredientNames(SignatureDef def, int n)
        {
            var parts = new string[n];
            for (int i = 0; i < n; i++) parts[i] = DrinkIngredients.Get(def.ingredientIds[i])?.name ?? def.ingredientIds[i];
            return string.Join("  +  ", parts);
        }

        public void Finish()
        {
            if (finished) return;
            finished = true;
            if (Current == this) Current = null;
            var cb = done;
            if (canvas != null) Destroy(canvas.gameObject);
            cb?.Invoke();
        }

        class ClickToSkip : MonoBehaviour, IPointerClickHandler
        {
            public SignatureRevealOverlay Owner;
            public void OnPointerClick(PointerEventData e) { if (Owner != null) Owner.skip = true; }
        }
    }
}
