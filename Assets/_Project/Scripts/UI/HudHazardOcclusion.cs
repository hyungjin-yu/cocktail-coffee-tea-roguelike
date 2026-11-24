using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>화면에 위험 예고를 그리는 몹(드라이버)이 HUD 가림 판정에 내놓는 화면 영역. 표시 전용 — 판정·위치에 영향 없음.</summary>
    public interface IHudHazard
    {
        /// <summary>지금 가리면 안 되는 월드 영역(몸체, 예고·파동이 보이는 동안은 바닥 테두리까지 합친 경계). 없으면 false.</summary>
        bool TryGetHudHazardBounds(out Bounds worldBounds);
    }

    /// <summary>
    /// 2026-11-23 실창 720p 검수: 쓴맛 안개가 화면 왼쪽에 있으면 왼쪽 HUD 패널(히든·직업·목표 조합·발동 효과)이 몸체와 보라 파동 테두리
    /// (예고 = 판정 경계)를 일부 가렸다. 몹·예고의 위치와 크기, 판정 반경은 그대로 두고 **겹친 패널만 반투명으로 낮춰** 아래 예고가 보이게 한다.
    /// 겹침이 끝나면 원래 불투명도로 돌아온다. 패널의 버튼 입력·배치는 바꾸지 않는다(CanvasGroup.alpha만). 시간 정지 중에도 실제 시간으로 전환.
    /// </summary>
    public sealed class HudHazardOcclusion : MonoBehaviour
    {
        /// <summary>겹친 패널의 불투명도. 글자 윤곽은 남고 아래 보라 테두리·몸체가 비친다.</summary>
        public const float OccludedAlpha = 0.28f;
        /// <summary>불투명도 전환 속도(초당). 0.28→1 약 0.1초.</summary>
        public const float FadePerSecond = 7f;
        /// <summary>패널 가장자리 여유(화면 픽셀, 1080 기준). 테두리가 패널 모서리를 스치기 직전부터 비치게.</summary>
        public const float MarginRef = 12f;

        sealed class Panel { public RectTransform rect; public CanvasGroup group; public bool occluded; }
        readonly List<Panel> panels = new List<Panel>();
        readonly List<Rect> hazards = new List<Rect>();
        Canvas canvas;

        /// <summary>2026-11-24 #22: 몹이 아닌 위험(바닥 물기·얼룩 구역)도 같은 판정에 넣는다. 등록한 쪽이 사라질 때 해제한다.</summary>
        static readonly List<IHudHazard> registered = new List<IHudHazard>();
        public static void Register(IHudHazard h) { if (h != null && !registered.Contains(h)) registered.Add(h); }
        public static void Unregister(IHudHazard h) { if (h != null) registered.Remove(h); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistered() => registered.Clear();

        public int PanelCount => panels.Count;
        public int HazardCount => hazards.Count;

        public static HudHazardOcclusion Attach(RectTransform hudRoot, params RectTransform[] watched)
        {
            var o = hudRoot.gameObject.AddComponent<HudHazardOcclusion>();
            foreach (var rt in watched) o.Watch(rt);
            return o;
        }

        public void Watch(RectTransform rt)
        {
            if (rt == null) return;
            var g = rt.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            panels.Add(new Panel { rect = rt, group = g });
        }

        /// <summary>시험·QA: 패널이 지금 위험 영역과 겹쳐 낮춰졌는지.</summary>
        public bool IsOccluded(RectTransform rt)
        {
            foreach (var p in panels) if (p.rect == rt) return p.occluded;
            return false;
        }

        public float AlphaOf(RectTransform rt)
        {
            foreach (var p in panels) if (p.rect == rt) return p.group.alpha;
            return 1f;
        }

        void LateUpdate() => Evaluate(Camera.main, false);

        /// <summary>겹침을 다시 계산한다. immediate면 전환 없이 목표 불투명도로 바로(증거 캡처가 캔버스를 바꾼 직후 호출).</summary>
        public void Evaluate(Camera cam, bool immediate)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            CollectHazards(cam);
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float margin = MarginRef * (canvas != null ? canvas.scaleFactor : 1f);
            float step = immediate ? 1f : FadePerSecond * Time.unscaledDeltaTime;
            foreach (var p in panels)
            {
                if (p.rect == null) continue;
                bool hit = false;
                if (p.rect.gameObject.activeInHierarchy && hazards.Count > 0)
                {
                    var r = ScreenRect(p.rect, uiCam);
                    r.xMin -= margin; r.yMin -= margin; r.xMax += margin; r.yMax += margin;
                    foreach (var h in hazards) if (r.Overlaps(h)) { hit = true; break; }
                }
                p.occluded = hit;
                p.group.alpha = Mathf.MoveTowards(p.group.alpha, hit ? OccludedAlpha : 1f, step);
            }
        }

        void CollectHazards(Camera cam)
        {
            hazards.Clear();
            if (cam == null) return;
            foreach (var e in Enemy.Active)
            {
                if (e == null || e.IsDead) continue;
                var src = e.GetComponent<IHudHazard>();
                if (src == null || !src.TryGetHudHazardBounds(out var b)) continue;
                hazards.Add(WorldToScreen(cam, b));
            }
            foreach (var h in registered)
                if (h != null && h.TryGetHudHazardBounds(out var rb)) hazards.Add(WorldToScreen(cam, rb));
        }

        static readonly Vector3[] corners = new Vector3[4];
        static Rect ScreenRect(RectTransform rt, Camera uiCam)
        {
            rt.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var s = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
                min = Vector2.Min(min, s); max = Vector2.Max(max, s);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Rect WorldToScreen(Camera cam, Bounds b)
        {
            var a = cam.WorldToScreenPoint(b.min);
            var c = cam.WorldToScreenPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }
    }
}
