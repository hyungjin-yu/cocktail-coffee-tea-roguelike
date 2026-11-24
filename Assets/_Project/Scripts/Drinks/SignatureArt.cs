using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 시그니처 아트(ArtSource/signature-drinks-2026-11-24/manifest.json 14 clip을 그대로 옮긴 표).
    /// 프레임 PNG는 Resources/SignatureDrinks/{clip}_{nn}.png(시트를 왼→오, 위→아래로 자른 원본 크기·원본 색, Point, 무압축).
    /// PPU·피벗은 .meta에 manifest 값으로 기록: salt_ring 960px = 8월드(PPU 120·중심), sidecar_wind 224px = 1.75월드(PPU 128·앵커 210,40),
    /// sidecar_low 64px = 0.5월드, whiteout_hold 하단 중앙(16,32), whiteout_edge 9분할 테두리 14px(720p 56px). 표시 전용 — 판정·피해와 무관.
    /// </summary>
    public static class SignatureArt
    {
        public const string Folder = "SignatureDrinks";

        public sealed class Clip
        {
            public string name;
            public Vector2Int framePx;
            public int[] durationsMs;
            public string low;
            public int TotalMs { get { int t = 0; foreach (var d in durationsMs) t += d; return t; } }
        }

        static Clip C(string n, int w, int h, int[] d, string low = null) => new Clip { name = n, framePx = new Vector2Int(w, h), durationsMs = d, low = low };
        static readonly int[] Discovery = { 300, 300, 300, 400, 100, 120, 140, 140, 500, 300 };

        public static readonly Clip[] All =
        {
            C("salt_ring", 960, 960, new[] { 40, 45, 45, 50, 55, 55, 65, 95 }, "salt_ring_low"),
            C("salt_ring_low", 960, 960, new[] { 260 }),
            C("sidecar_wind", 224, 80, new[] { 75, 75, 75, 75, 75, 75 }, "sidecar_low"),
            C("sidecar_low", 64, 36, new[] { 5000 }),
            C("whiteout_edge", 320, 180, new[] { 45, 55, 65, 80, 105 }, "whiteout_edge_low"),
            C("whiteout_edge_low", 320, 180, new[] { 220 }),
            C("whiteout_hold", 32, 32, new[] { 4000 }, "whiteout_hold_low"),
            C("whiteout_hold_low", 32, 32, new[] { 4000 }),
            C("iba_margarita_discovery", 320, 180, Discovery, "iba_margarita_discovery_low"),
            C("iba_margarita_discovery_low", 320, 180, new[] { 1300 }),
            C("iba_sidecar_discovery", 320, 180, Discovery, "iba_sidecar_discovery_low"),
            C("iba_sidecar_discovery_low", 320, 180, new[] { 1300 }),
            C("iba_white_lady_discovery", 320, 180, Discovery, "iba_white_lady_discovery_low"),
            C("iba_white_lady_discovery_low", 320, 180, new[] { 1300 }),
        };

        /// <summary>전체 프레임 수(manifest 합계).</summary>
        public const int TotalFrames = 57;
        /// <summary>저이펙트 발견 잔이 나타나는 시각(manifest start_ms).</summary>
        public const int LowDiscoveryStartMs = 1300;
        /// <summary>질주 잔상(manifest sidecar_ghost).</summary>
        public const float GhostSpacing = 0.075f, GhostLife = 0.21f;
        public static readonly float[] GhostOpacity = { 0.43f, 0.28f, 0.15f };
        /// <summary>얼음 배지 720p 기준 20px, 서리 테두리 720p 56px.</summary>
        public const float BadgeRef720Px = 20f, EdgeRef720Px = 56f;

        public static Clip Get(string name) { foreach (var c in All) if (c.name == name) return c; return null; }

        /// <summary>저이펙트면 low clip 이름(없으면 원래 이름). 일반과 low를 함께 재생하지 않는다.</summary>
        public static string Resolve(string name, bool low) { var c = Get(name); return low && c != null && c.low != null ? c.low : name; }

        /// <summary>정식 레시피 id → 발견 clip(저이펙트면 _low). 알 수 없으면 null.</summary>
        public static string DiscoveryClip(string signatureId, bool low) =>
            Get(signatureId + "_discovery") == null ? null : Resolve(signatureId + "_discovery", low);

        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        /// <summary>clip 프레임(빠진 프레임이 하나라도 있으면 null — 호출부는 기존 절차형 연출로 대체).</summary>
        public static Sprite[] Load(string clip)
        {
            if (clip == null) return null;
            if (cache.TryGetValue(clip, out var hit)) return hit;
            var c = Get(clip);
            Sprite[] arr = null;
            if (c != null)
            {
                arr = new Sprite[c.durationsMs.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    arr[i] = Resources.Load<Sprite>($"{Folder}/{clip}_{i:00}");
                    if (arr[i] == null) { arr = null; break; }
                }
            }
            cache[clip] = arr;
            return arr;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        /// <summary>경과 ms의 프레임 번호(노출 시간 누적). loop면 감고, 아니면 마지막 프레임 유지.</summary>
        public static int FrameAt(Clip clip, float elapsedMs, bool loop = false)
        {
            if (clip == null || clip.durationsMs.Length == 0) return 0;
            float t = Mathf.Max(0f, elapsedMs);
            if (loop) t %= Mathf.Max(1, clip.TotalMs);
            int acc = 0;
            for (int i = 0; i < clip.durationsMs.Length; i++) { acc += clip.durationsMs[i]; if (t < acc) return i; }
            return clip.durationsMs.Length - 1;
        }

        /// <summary>720p 기준 px → 지금 화면의 월드 길이(직교 카메라). 카메라가 없으면 px/128.</summary>
        public static float WorldPer720Px(Camera cam, float px) =>
            cam != null && cam.orthographic ? px / 720f * cam.orthographicSize * 2f : px / 128f;
    }
}
