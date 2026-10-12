using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>43(2026-10-12) 몬스터 설치 장판·예고 v2 그림(PixelLab+Aseprite, ArtSource/status-zone-vfx-2026-10-12).
    /// 판정은 월드 원(반지름 r)이므로, 그림의 불투명 영역(전 프레임 합집합) 가로·세로가 정확히 2r이 되게 축별로 맞춘다
    /// — 예전 v1은 원이 타원 안에 들어가게 키워서 가로로 실제 위험 영역보다 두 배 넓어 보였다.
    /// 규칙(반경·피해·틱·예고 시간·지속)은 바꾸지 않는다. 아트가 없으면 false → 호출부가 기존 v1 그림을 쓴다.</summary>
    public sealed class ZoneVisualV2 : MonoBehaviour
    {
        public const float FadeInSeconds = 0.15f;
        public const float FadeOutSeconds = 0.35f;

        sealed class Clip
        {
            public ArtClip art;
            public Rect content; // 스프라이트 rect 기준 불투명 영역(픽셀)
            public float ppu;
        }
        static readonly Dictionary<string, Clip> cache = new Dictionary<string, Clip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cache.Clear();

        static Clip Load(string id)
        {
            if (cache.TryGetValue(id, out var c) && c != null && c.art.frames[0] != null) return c;
            var all = Resources.LoadAll<Sprite>("ArtExtra/" + id);
            c = null;
            if (all != null && all.Length > 0)
            {
                System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
                c = new Clip { art = new ArtClip { name = id, fps = 10f, loop = true, frames = all }, ppu = all[0].pixelsPerUnit };
                c.content = ContentRect(all);
            }
            cache[id] = c;
            return c;
        }

        /// <summary>전 프레임 알파 합집합 사각형(픽셀). 텍스처를 못 읽으면 스프라이트 전체.</summary>
        static Rect ContentRect(Sprite[] frames)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            foreach (var s in frames)
            {
                var tex = s.texture;
                if (tex == null || !tex.isReadable) return s.rect;
                var r = s.rect;
                var px = tex.GetPixels32();
                for (int y = 0; y < (int)r.height; y++)
                    for (int x = 0; x < (int)r.width; x++)
                        if (px[((int)r.y + y) * tex.width + (int)r.x + x].a > 24)
                        {
                            if (x < minX) minX = x; if (y < minY) minY = y;
                            if (x > maxX) maxX = x; if (y > maxY) maxY = y;
                        }
            }
            if (maxX < 0) return frames[0].rect;
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public static bool Available(string id) => Load(id) != null;

        /// <summary>그림 불투명 영역의 월드 크기가 (2r, 2r)이 되는 축별 배율과, 영역 중심을 판정 중심에 맞추는 오프셋.</summary>
        static void Fit(Clip c, float radius, out Vector3 scale, out Vector2 offset)
        {
            var spriteRect = c.art.frames[0].rect;
            float sx = 2f * radius / (c.content.width / c.ppu), sy = 2f * radius / (c.content.height / c.ppu);
            scale = new Vector3(sx, sy, 1f);
            // 가운데 pivot 기준 내용 중심의 어긋남(픽셀) → 월드(배율 적용) 반대 방향 보정
            float cx = c.content.center.x - spriteRect.width / 2f, cy = c.content.center.y - spriteRect.height / 2f;
            offset = new Vector2(-cx / c.ppu * sx, -cy / c.ppu * sy);
        }

        public static Vector2 VisualHalfExtents(string id, float radius)
        {
            var c = Load(id);
            if (c == null) return Vector2.zero;
            Fit(c, radius, out var s, out _);
            return new Vector2(c.content.width / c.ppu * s.x, c.content.height / c.ppu * s.y) * 0.5f;
        }

        // ── 활성 장판 ─────────────────────
        SpriteRenderer sr;
        float life, age, baseAlpha = 1f;
        Vector3 fullScale;
        Transform visual;

        /// <summary>활성 장판: 판정 원과 같은 경계, 0.15초 나타남, 마지막 0.35초 가장자리 수축·투명화로 "곧 사라짐".</summary>
        public static bool AttachActive(GameObject zone, string id, float radius, float lifetime, int sortingOrder)
        {
            var c = Load(id);
            if (c == null) return false;
            var child = new GameObject("ZoneV2_" + id);
            child.transform.SetParent(zone.transform, false);
            Fit(c, radius, out var scale, out var offset);
            child.transform.localPosition = offset;
            child.transform.localScale = scale;
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = c.art.frames[0];
            sr.sortingOrder = sortingOrder;
            var anim = child.AddComponent<SpriteAnimator>();
            anim.Bind(sr);
            anim.Play(c.art, true);
            var v = zone.AddComponent<ZoneVisualV2>();
            v.sr = sr; v.visual = child.transform; v.fullScale = scale; v.life = lifetime; v.baseAlpha = 0.9f;
            v.Apply();
            return true;
        }

        void Update()
        {
            age += Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            if (sr == null) return;
            float a = baseAlpha, k = 1f;
            if (age < FadeInSeconds) { float t = age / FadeInSeconds; a *= t; k = Mathf.Lerp(0.85f, 1f, t); }
            float left = life - age;
            if (left < FadeOutSeconds) { float t = Mathf.Clamp01(left / FadeOutSeconds); a *= t; k = Mathf.Lerp(0.8f, 1f, t); }
            sr.color = new Color(1f, 1f, 1f, a);
            visual.localScale = fullScale * k;
        }

        public float AlphaForTests => sr != null ? sr.color.a : 0f;
        public Vector3 ScaleForTests => visual != null ? visual.localScale : Vector3.zero;
    }

    /// <summary>43 장판 예고 v2: 판정 원과 같은 테두리(일렁이는 경고 링) + 안쪽에서 테두리까지 차오르는 링이 예고 시간과 정확히 함께
    /// 끝나 "언제 활성화되는지"가 보인다. 면 채움 없이 테두리만 — 캐릭터를 가리지 않는다. 예고 시간·판정은 호출부 그대로.</summary>
    public sealed class ZoneTelegraphV2 : MonoBehaviour
    {
        public const string RingId = "zone_warning_v2";
        Transform inner;
        SpriteRenderer outerSr, innerSr;
        Vector3 fullScale;
        float duration, age;
        Color tint;

        public static ZoneTelegraphV2 Spawn(Vector2 position, float radius, float seconds, Color tint, Transform parent = null)
        {
            if (!ZoneVisualV2.Available(RingId)) return null;
            var go = new GameObject("ZoneTelegraphV2");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var t = go.AddComponent<ZoneTelegraphV2>();
            t.duration = Mathf.Max(0.05f, seconds);
            t.tint = tint;
            ZoneVisualV2.AttachActive(go, RingId, radius, float.MaxValue, SortOrders.GroundZone + 1);
            var outer = go.GetComponent<ZoneVisualV2>();
            t.outerSr = go.GetComponentInChildren<SpriteRenderer>();
            t.fullScale = t.outerSr.transform.localScale;
            Destroy(outer); // 수명 제어는 예고가 직접(예고 시간에 정확히 사라짐)
            var innerGo = Instantiate(t.outerSr.gameObject, go.transform);
            innerGo.name = "Progress";
            t.inner = innerGo.transform;
            t.innerSr = innerGo.GetComponent<SpriteRenderer>();
            var innerAnim = t.innerSr.GetComponent<SpriteAnimator>();
            innerAnim.Bind(t.innerSr);
            innerAnim.Play(t.outerSr.GetComponent<SpriteAnimator>().Current, true);
            t.Apply();
            Destroy(go, t.duration);
            return t;
        }

        void Update() { age += Time.deltaTime; Apply(); }

        void Apply()
        {
            float p = Mathf.Clamp01(age / duration);
            outerSr.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.55f, 1f, p));
            inner.localScale = new Vector3(fullScale.x * p, fullScale.y * p, 1f);
            inner.localPosition = outerSr.transform.localPosition * p;
            innerSr.color = new Color(tint.r, tint.g, tint.b, 0.35f + 0.45f * p);
        }

        public float ProgressForTests => duration > 0f ? Mathf.Clamp01(age / duration) : 1f;
    }
}
