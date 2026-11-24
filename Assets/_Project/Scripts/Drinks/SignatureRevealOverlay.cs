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

        IEnumerator RunFull(SignatureDef def, bool low)
        {
            var root = UiFactory.Stretch("Root", canvas.transform);
            var dim = UiFactory.AddImage(root, new Color(0.03f, 0.02f, 0.04f, 0f));
            dim.raycastTarget = true;
            root.gameObject.AddComponent<ClickToSkip>().Owner = this;
            Image ring = null, ring2 = null;
            if (!low)
            {
                ring = UiFactory.AddImage(UiFactory.Place("Ring", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(100f, 100f)), def.accent);
                ring.sprite = SpriteFactory.Circle; ring.raycastTarget = false;
                ring2 = UiFactory.AddImage(UiFactory.Place("Ring2", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(60f, 60f)), Color.white);
                ring2.sprite = SpriteFactory.Circle; ring2.raycastTarget = false;
            }
            var head = UiFactory.AddText(UiFactory.Place("Head", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1400f, 50f)), "정식 레시피 발견!", 34, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.5f));
            var ingredients = UiFactory.AddText(UiFactory.Place("Ingredients", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 50f)), "", 26, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.9f));
            var name = UiFactory.AddText(UiFactory.Place("Name", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 110f)), "", 76, TextAnchor.MiddleCenter, Color.white);
            var category = UiFactory.AddText(UiFactory.Place("Category", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1400f, 40f)), "", 24, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            var sig = UiFactory.AddText(UiFactory.Place("Signature", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1500f, 110f)), "", 30, TextAnchor.MiddleCenter, def.accent);
            var foot = UiFactory.AddText(UiFactory.Place("Foot", root, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 40f)), "클릭·Enter로 건너뛰기", 22, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));
            foreach (var t in new[] { head, ingredients, name, category, sig, foot }) t.raycastTarget = false;
            head.enabled = false;

            float t0 = 0f;
            while (t0 < FullSeconds && !skip)
            {
                t0 += Time.unscaledDeltaTime;
                float k = t0 / FullSeconds;
                dim.color = new Color(0.03f, 0.02f, 0.04f, Mathf.Min(0.9f, t0 / 0.3f * 0.9f));
                head.enabled = t0 >= 0.15f;
                // 0.3~1.2s 재료 이름이 하나씩(처음 공개). 1.3s 이름, 1.8s 시그니처, 2.2s 도감 기록.
                int shownIng = Mathf.Clamp(Mathf.FloorToInt((t0 - 0.3f) / 0.3f) + 1, 0, def.ingredientIds.Length);
                ingredients.text = IngredientNames(def, shownIng);
                if (t0 >= 1.3f) { name.text = def.name; category.text = string.IsNullOrEmpty(def.category) ? "IBA 공식 레시피" : $"IBA 공식 레시피 · {def.category}"; }
                if (t0 >= 1.8f) sig.text = $"시그니처 · {def.effectName} — {def.effectShort}\n이 잔을 마시고 출발하면 전투에서 <b>한 번</b> 쓸 수 있어요 (Q 또는 시그니처 칸 클릭)";
                if (t0 >= 2.2f) foot.text = "도감 · 정식 레시피에 기록했습니다";
                if (ring != null)
                {
                    float rk = Mathf.Clamp01((t0 - 0.3f) / 0.9f);
                    ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(100f, 900f, rk);
                    var c = def.accent; c.a = 0.55f * (1f - rk); ring.color = c;
                    float r2 = Mathf.Clamp01((t0 - 1.25f) / 0.5f);
                    ring2.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(60f, 520f, r2);
                    ring2.color = new Color(1f, 1f, 1f, t0 < 1.25f ? 0f : 0.6f * (1f - r2));
                    name.transform.localScale = Vector3.one * (t0 < 1.3f ? 1f : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t0 - 1.3f) / 0.25f)));
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
