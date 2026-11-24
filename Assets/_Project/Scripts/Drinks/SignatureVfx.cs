using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 시그니처 전투 발동 VFX(임시 절차형 — 전용 아트 없음, SpriteFactory.Circle + FadeVfx만 재사용).
    /// 저이펙트(HitFeedback.LowEffects): 화면 전체 섬광·겹 고리·잔상 없음, 범위 고리 1개(연한 알파)만. 판정·피해와 무관한 표시 전용.
    /// 전용 아트가 들어오면 이 함수들의 내부만 바꾼다(호출부 불변) — 필요 자산 목록은 보고서 #25-1 참고.
    /// </summary>
    public static class SignatureVfx
    {
        public const int SortingOrder = 30;
        /// <summary>마지막으로 재생한 효과 종류(검수용).</summary>
        public static string LastPlayed { get; private set; } = "";

        /// <summary>범위 고리: 반지름 radius(월드)까지 0.45초에 퍼지며 사라진다. 일반은 2겹(바깥 빠르게), 저이펙트는 1겹.</summary>
        public static void Ring(Vector2 at, float radius, Color accent)
        {
            bool low = HitFeedback.LowEffects;
            float d = Mathf.Max(0.5f, radius * 2f);
            var c = accent; c.a = low ? 0.35f : 0.6f;
            FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * d * 0.25f, Vector2.one * d, c, 0.45f, SortingOrder);
            if (!low)
            {
                var c2 = Color.white; c2.a = 0.5f;
                FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * d * 0.1f, Vector2.one * d * 0.7f, c2, 0.3f, SortingOrder + 1);
            }
            LastPlayed = low ? "ring_low" : "ring";
        }

        /// <summary>지속 효과 표식(사이드카): 플레이어를 따라가는 연한 원이 seconds 동안 남는다. 저이펙트도 같은 표식(깜빡임 없음 — 무적 깜빡임은 기존 규칙).</summary>
        public static void Aura(Transform follow, float seconds, Color accent)
        {
            if (follow == null) return;
            var c = accent; c.a = HitFeedback.LowEffects ? 0.22f : 0.35f;
            var v = FadeVfx.Spawn(SpriteFactory.Circle, follow.position, 0f, Vector2.one * 1.8f, Vector2.one * 1.6f, c, seconds, SortingOrder - 1);
            v.transform.SetParent(follow, true);
            if (!HitFeedback.LowEffects) Ring(follow.position, 2.2f, accent);
            LastPlayed = HitFeedback.LowEffects ? "aura_low" : "aura";
        }

        /// <summary>화면 전체 0.35초 색 섬광(화이트 아웃). 저이펙트에서는 재생하지 않는다.</summary>
        public static void ScreenFlash(Color accent)
        {
            if (HitFeedback.LowEffects) { LastPlayed = "flash_skipped_low"; return; }
            var go = new GameObject("SignatureFlash");
            var canvas = UiFactory.CreateCanvas(go.transform, "Canvas", 400);
            var img = UiFactory.AddImage(UiFactory.Stretch("Flash", canvas.transform), new Color(accent.r, accent.g, accent.b, 0.45f));
            img.raycastTarget = false;
            go.AddComponent<FlashFade>().Init(img, 0.35f);
            LastPlayed = "flash";
        }

        sealed class FlashFade : MonoBehaviour
        {
            Image img; float life, t, a0;
            public void Init(Image i, float seconds) { img = i; life = seconds; a0 = i.color.a; }
            void Update()
            {
                t += Time.unscaledDeltaTime;
                if (img == null || t >= life) { Destroy(gameObject); return; }
                var c = img.color; c.a = a0 * (1f - t / life); img.color = c;
            }
        }
    }
}
