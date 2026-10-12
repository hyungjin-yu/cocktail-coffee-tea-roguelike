using UnityEngine;

namespace CCT
{
    /// <summary>
    /// ArtClip을 SpriteRenderer에 재생한다(여러 PNG로 된 04 클립). 반복 클립은 계속 감고, 1회 클립은 마지막 프레임에서
    /// 멈추며 Finished가 true가 된다. 같은 클립을 다시 Play하면 restart=false일 때 이어서 재생한다(걷기↔대기 전환 시
    /// 같은 클립이면 끊기지 않게). 게임 이벤트가 클립을 재생할 뿐, 프레임이 판정 시간을 정하지 않는다(04 §6).
    /// </summary>
    public class SpriteAnimator : MonoBehaviour
    {
        SpriteRenderer sr;
        ArtClip clip;
        float time;

        public bool UseUnscaledTime { get; set; }
        public ArtClip Current => clip;
        public float ClipTime => time;
        public int FrameIndex => clip == null || clip.frames.Length == 0 || clip.fps <= 0f ? 0
            : clip.loop ? Mathf.FloorToInt(time * clip.fps) % clip.frames.Length
            : Mathf.Min(Mathf.FloorToInt(time * clip.fps), clip.frames.Length - 1);
        public bool Finished => clip != null && !clip.loop && time >= clip.Duration;

        public void Bind(SpriteRenderer renderer) => sr = renderer;

        public void Play(ArtClip next, bool restart = false)
        {
            if (next == null) return;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (clip == next && !restart) return;
            clip = next;
            time = 0f;
            Apply();
        }

        /// <summary>테스트/수동 진행용 — Update와 같은 경로.</summary>
        public void Advance(float dt)
        {
            if (clip == null) return;
            time += Mathf.Max(0f, dt);
            Apply();
        }

        void Update() => Advance(UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        void Apply()
        {
            if (sr == null || clip == null) return;
            var s = clip.FrameAt(time);
            if (s != null) sr.sprite = s;
        }
    }

    /// <summary>
    /// 04 §8 ground_shadow(Unity alpha 0.22, 바닥 기준) — 개체 발밑 그림자. 아트가 없으면 만들지 않는다.
    /// 정렬은 SortOrders.Shadow라 장판·개체보다 항상 아래.
    /// </summary>
    public static class GroundShadow
    {
        public const string AssetId = "ground_shadow";

        /// <param name="widthWorld">그림자 알파 영역의 가로 월드 폭.</param>
        public static SpriteRenderer Attach(Transform parent, Vector2 localFeet, float widthWorld)
        {
            var art = ArtLibrary.Get(AssetId);
            var sprite = art?.FirstSprite;
            if (sprite == null) return null;

            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localFeet;
            float contentWidth = Mathf.Max(0.01f, art.ContentWorldSize.x);
            go.transform.localScale = Vector3.one * (widthWorld / contentWidth);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 1f, 1f, art.runtimeOpacity > 0f && art.runtimeOpacity < 1f ? art.runtimeOpacity : 0.22f);
            sr.sortingOrder = SortOrders.Shadow;
            return sr;
        }
    }

    /// <summary>04 §8 VFX 생성 공용 경로: 아트가 있으면 클립 재생 FadeVfx, 없으면 null(호출부가 기존 도형 사용).</summary>
    public static class ArtVfx
    {
        /// <summary>알파 영역 가로가 widthWorld가 되도록 균일 배율. scaleEnd는 시작 대비 비율.</summary>
        public static FadeVfx Spawn(string assetId, Vector2 position, float angleDeg, float widthWorld,
            float life, Color tint, int sortingOrder = SortOrders.AirVfx, float endScaleRatio = 1f, bool fade = true,
            bool unscaled = false)
        {
            var art = ArtLibrary.Get(assetId);
            var clip = art?.Main;
            if (clip == null) return null;
            float contentWidth = Mathf.Max(0.01f, art.ContentWorldSize.x);
            float s = widthWorld / contentWidth;
            var c = tint;
            if (art.runtimeOpacity > 0f && art.runtimeOpacity < 1f) c.a *= art.runtimeOpacity;
            var vfx = FadeVfx.Spawn(clip.frames[0], position, angleDeg, Vector2.one * s, Vector2.one * s * endScaleRatio,
                c, life, sortingOrder);
            vfx.SetClip(clip, fade, unscaled);
            return vfx;
        }

        /// <summary>바닥 원형 예고/장판 그림(warning_ring, pressure_wave, summon_ring 등) — 반지름 r 원 판정이 타원 알파 영역 안에
        /// 완전히 들어가게 맞춘다. 기존 정확한 원 표시는 호출부가 그대로 둔다(경고를 작게 그려 판정을 숨기지 않음, 04 §8).</summary>
        public static FadeVfx GroundRing(string assetId, Vector2 position, float radius, float life, Color tint,
            bool fade = false, int sortingOrder = SortOrders.GroundZone, string tintVariant = null)
        {
            var art = ArtLibrary.Get(assetId);
            var clip = art?.Main;
            if (clip == null) return null;
            if (tintVariant != null && art.TryGetTint(tintVariant, out var variant)) tint *= variant;
            float s = ScaleForGroundRadius(art, radius);
            var vfx = FadeVfx.Spawn(clip.frames[0], position, 0f, Vector2.one * s, Vector2.one * s, tint, life, sortingOrder);
            vfx.SetClip(clip, fade);
            return vfx;
        }

        /// <summary>가로선 예고(warning_line)·증기(steam_jet) 같은 띠: 길이·두께를 월드로 맞춘 비균일 배율(정적 효과 크기 연출).</summary>
        public static FadeVfx Band(string assetId, Vector2 mid, float angleDeg, float length, float thickness, float life,
            Color tint, bool fade = true, int sortingOrder = SortOrders.AirVfx)
        {
            var art = ArtLibrary.Get(assetId);
            var clip = art?.Main;
            if (clip == null) return null;
            var size = art.ContentWorldSize;
            var scale = new Vector2(length / Mathf.Max(0.01f, size.x), thickness / Mathf.Max(0.01f, size.y));
            var vfx = FadeVfx.Spawn(clip.frames[0], mid, angleDeg, scale, scale, tint, life, sortingOrder);
            vfx.SetClip(clip, fade);
            return vfx;
        }

        /// <summary>장판: 코드의 원형 판정(반지름 r)이 그림의 타원 알파 영역 안에 완전히 들어가도록 세로 기준으로 맞춘다(04 §8).</summary>
        public static float ScaleForGroundRadius(ArtAsset art, float radius)
        {
            var size = art.ContentWorldSize;
            float h = Mathf.Max(0.01f, size.y), w = Mathf.Max(0.01f, size.x);
            // 타원(반축 w/2, h/2)이 반지름 r 원을 포함하려면 짧은 반축 ≥ r.
            return 2f * radius / Mathf.Min(h, w);
        }
    }
}
