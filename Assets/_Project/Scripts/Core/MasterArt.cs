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
            // 상성 후속 6 clip(affinity/manifest.json) — 128², 83ms, 1회, 마지막 프레임 완전 투명.
            C("cocktail_foam", 128, 128, Repeat(83, 6), false),
            C("coffee_bean_steam", 128, 128, Repeat(83, 6), false),
            C("tea_leaf_wind", 128, 128, Repeat(83, 6), false),
            C("cocktail_foam_low", 128, 128, Repeat(83, 3), false),
            C("coffee_bean_steam_low", 128, 128, Repeat(83, 3), false),
            C("tea_leaf_wind_low", 128, 128, Repeat(83, 3), false),
        };

        /// <summary>인수 아트 총 노출 수(핵심 83 + 상성 27).</summary>
        public const int TotalFrames = 110;

        /// <summary>품질 등급 → clip 이름(저이펙트면 _low로 교체 — full과 함께 재생하지 않는다). None은 null.</summary>
        public static string QualityClip(DrinkQuality.Grade g, bool low)
        {
            string n = g == DrinkQuality.Grade.Best ? "quality_master" : g == DrinkQuality.Grade.Good ? "quality_excellent" : g == DrinkQuality.Grade.Normal ? "quality_normal" : null;
            return n == null ? null : low ? n + "_low" : n;
        }

        /// <summary>마신 음료 종류 → 상성 적중 clip(저이펙트면 _low로 교체).</summary>
        public static string AffinityClip(DrinkKind kind, bool low)
        {
            string n = kind == DrinkKind.Coffee ? "coffee_bean_steam" : kind == DrinkKind.Tea ? "tea_leaf_wind" : "cocktail_foam";
            return low ? n + "_low" : n;
        }

        /// <summary>manifest intro_schedule_ms(3500ms 기준) — 설정 길이에 비례해 쓴다.</summary>
        /// 2026-11-11 코디네이터 승인(진짜 계약은 총 3~4초 휴식): 발 미끄럼 완화를 위해 3500ms 안 분배를 접근 0–1000 / 설치 1000–2500 / 퇴장 2500–3500으로 조정.
        /// 현수막은 설치 시작+250ms(원래 계약과 같은 상대 위치) 1250–2250. 원래 manifest 값: 접근 800·설치 2300·현수막 1050–2050.
        public const int WalkInEndMs = 1000, InstallEndMs = 2500, TotalMs = 3500, BannerStartMs = 1250, BannerEndMs = 2250;
        /// <summary>승인 걷기 프레임 실측 보폭(발 사이 최대 폭 77px − 발 길이 ≈25px = 52px/걸음, 6노출 600ms = 2걸음). 배율 1 기준 월드/ms.</summary>
        public const float WalkStepPx = 52f;
        public static float WalkSpeedPerMs(float npcScale) => 2f * WalkStepPx / 128f * npcScale / 600f;
        /// <summary>접근 시작·퇴장 끝 투명도 전환(ms). 화면 밖에서 걸어오면 3.5초 안에 보폭의 5배 속도가 필요해 미끄러지므로, 설치 위치 가까이에서 보폭에 맞는 속도로 걸으며 나타나고 사라진다.</summary>
        public const float NpcFadeMs = 400f;
        /// <summary>현수막 최종 핀(1280×192, 좌상단 원점)과 글자 안전 영역(정규화 x0,y0,x1,y1, 좌상단 원점).</summary>
        public static readonly Vector2 PinLeftPx = new Vector2(43, 20), PinRightPx = new Vector2(1237, 20);
        /// <summary>현수막 프레임별 천 끝 핀 위치(좌상단 px: 왼쪽 x, 오른쪽 x, y). 2026-11-11 승인 PNG의 알파 상단 띠 끝에서 최종 핀 안쪽 여백(16/15px)·상단+13px로 측정(원본 무수정).
        /// 00–04는 펼치는 중이라 끝이 바깥으로 이동, 05–07은 최종 핀(43/1237, 20)과 같다.</summary>
        public static readonly Vector3[] BannerPinsPx =
        {
            new Vector3(396, 884, 28), new Vector3(372, 910, 16), new Vector3(261, 1020, 17), new Vector3(152, 1128, 18),
            new Vector3(68, 1214, 20), new Vector3(43, 1237, 20), new Vector3(43, 1237, 20), new Vector3(43, 1237, 20),
        };
        public static readonly Rect TextSafe =Rect.MinMaxRect(0.1f, 0.2f, 0.9f, 0.8f);
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
