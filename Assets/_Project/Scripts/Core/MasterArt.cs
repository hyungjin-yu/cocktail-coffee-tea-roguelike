using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-11 마스터 도전 아트(ArtSource/master-challenge-2026-11-11/manifest.json 15 clip·83 frame을 그대로 옮긴 표).
    /// 프레임 시간은 노출 단위(ms) — 고유 그림 수와 노출 수를 구분한다(예: idle 4노출 = 1그림, install 6노출 = 3그림).
    /// </summary>
    public static class MasterArt
    {
        public sealed class Clip
        {
            public string name;
            public Vector2Int canvas;
            public int[] durationsMs;
            public bool loop;
            public int TotalMs { get { int t = 0; foreach (var d in durationsMs) t += d; return t; } }
        }

        static int[] Repeat(int ms, int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = ms; return a; }
        static Clip C(string n, int w, int h, int[] d, bool loop) => new Clip { name = n, canvas = new Vector2Int(w, h), durationsMs = d, loop = loop };

        public static readonly Clip[] AllClips =
        {
            C("quality_normal", 128, 128, Repeat(83, 6), false),
            C("quality_excellent", 128, 128, Repeat(83, 8), false),
            C("quality_master", 128, 128, Repeat(83, 8), false),
            C("quality_normal_low", 128, 128, Repeat(83, 3), false),
            C("quality_excellent_low", 128, 128, Repeat(83, 3), false),
            C("quality_master_low", 128, 128, Repeat(83, 3), false),
            C("banner_install", 1280, 192, Repeat(125, 8), false),
            C("npc_porter_walk_in", 256, 256, Repeat(100, 6), true),
            C("npc_porter_install", 256, 256, new[] { 125, 125, 500, 500, 125, 125 }, false),
            C("npc_porter_idle", 256, 256, Repeat(333, 4), true),
            C("npc_porter_walk_out", 256, 256, Repeat(100, 6), true),
            C("npc_rigger_walk_in", 256, 256, Repeat(100, 6), true),
            C("npc_rigger_install", 256, 256, new[] { 125, 125, 500, 500, 125, 125 }, false),
            C("npc_rigger_idle", 256, 256, Repeat(333, 4), true),
            C("npc_rigger_walk_out", 256, 256, Repeat(100, 6), true),
        };

        /// <summary>manifest intro_schedule_ms(3500ms 기준) — 설정 길이에 비례해 쓴다.</summary>
        public const int WalkInEndMs = 800, InstallEndMs = 2300, TotalMs = 3500, BannerStartMs = 1050, BannerEndMs = 2050;
        /// <summary>현수막 최종 핀(1280×192, 좌상단 원점)과 글자 안전 영역(정규화 x0,y0,x1,y1, 좌상단 원점).</summary>
        public static readonly Vector2 PinLeftPx = new Vector2(43, 20), PinRightPx = new Vector2(1237, 20);
        public static readonly Rect TextSafe = Rect.MinMaxRect(0.1f, 0.2f, 0.9f, 0.8f);
        /// <summary>설치 노출 02/03의 안정된 최고 손 좌표(256 캔버스 좌상단 원점).</summary>
        public static readonly Vector2 PorterGripPx = new Vector2(61, 93), RiggerGripPx = new Vector2(194, 93);
        /// <summary>NPC 지면(발) y — 이미지 상단 기준 224 = pivot .125.</summary>
        public const float NpcGroundYPx = 224f;

        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static Clip Get(string name) { foreach (var c in AllClips) if (c.name == name) return c; return null; }

        /// <summary>Resources/MasterChallenge/{clip}_{00..}. 하나라도 없으면 null(임시 대체 연출로 떨어진다).</summary>
        public static Sprite[] Load(string clip)
        {
            if (cache.TryGetValue(clip, out var s)) return s;
            var def = Get(clip);
            if (def == null) return null;
            var arr = new Sprite[def.durationsMs.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = Resources.Load<Sprite>($"MasterChallenge/{clip}_{i:00}");
                if (arr[i] == null) { arr = null; break; }
            }
            cache[clip] = arr;
            return arr;
        }

        /// <summary>경과 ms의 노출 프레임 번호(루프면 반복, 아니면 마지막 고정).</summary>
        public static int FrameAt(Clip clip, float elapsedMs)
        {
            if (clip == null || clip.durationsMs.Length == 0) return 0;
            int total = clip.TotalMs;
            float t = Mathf.Max(0f, elapsedMs);
            if (clip.loop && total > 0) t %= total;
            else if (t >= total) return clip.durationsMs.Length - 1;
            int acc = 0;
            for (int i = 0; i < clip.durationsMs.Length; i++) { acc += clip.durationsMs[i]; if (t < acc) return i; }
            return clip.durationsMs.Length - 1;
        }

        /// <summary>256 캔버스 좌상단 px → pivot(발) 기준 로컬 월드 좌표. flipX면 x = 255 − x를 먼저 적용한다.</summary>
        public static Vector2 NpcAnchorLocal(Vector2 px, bool flipX)
        {
            float x = flipX ? 255f - px.x : px.x;
            return new Vector2((x - 128f) / 128f, (NpcGroundYPx - px.y) / 128f);
        }

        /// <summary>현수막 좌상단 px → pivot(상단 중앙) 기준 로컬 월드 좌표.</summary>
        public static Vector2 BannerLocal(Vector2 px) => new Vector2((px.x - 640f) / 128f, -px.y / 128f);
    }
}
