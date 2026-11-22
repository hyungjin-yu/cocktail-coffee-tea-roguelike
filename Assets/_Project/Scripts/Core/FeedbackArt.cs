using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-22 플레이 피드백 정식 픽셀 아트(ArtSource/feedback-art-2026-11-22/revisions/v1-formal-pixels, manifest.ready.json).
    /// 기존 ArtV1 목록·일괄 검수표(일반 몸체 256px 가정)와 분리해 Resources/FeedbackArt 에 별도 등록한다.
    /// 몸체 128px(발 피벗 0.5,0.125, 128 PPU), 바닥 효과 256×128 센터 피벗, HUD 40×40. ArtLibrary.Get 이 목록에 없는 ID를 여기서 찾는다.
    /// 클립 표(이름·장수·fps·반복)는 manifest 와 같다(테스트가 파일 존재·장수를 대조).
    /// </summary>
    public static class FeedbackArt
    {
        public const string BitterMist = "bitter_mist", GlassThief = "glass_thief", BitterMistPulse = "bitter_mist_pulse",
            MixingRushAura = "mixing_rush_aura", StatusAttackSpeedDown = "status_attack_speed_down", StatusHealBlock = "status_heal_block";

        /// <summary>바닥 효과 알파 영역 가로(px, manifest 실측): 쓴맛 안개 파동 바깥 테두리 244, 혼합 러시 오라 활성 평균 약 200.</summary>
        public const float PulseOutlinePx = 244f, AuraContentPx = 200f;

        public struct ClipSpec
        {
            public string name; public int frames; public float fps; public bool loop;
            public ClipSpec(string n, int f, float fps, bool l) { name = n; frames = f; this.fps = fps; loop = l; }
        }

        struct Spec
        {
            public string kind; public Vector2 frame; public Vector2 pivot; public float bodyPx, targetWorld; public ClipSpec[] clips; public string folder, pattern;
        }

        static readonly Dictionary<string, Spec> table = new Dictionary<string, Spec>
        {
            // 몸체: 안개 발선 위 90px / 도둑 70px. 목표 월드 높이는 기존 화면상 높이(같은 반경 계열 일반 몹 1.15~1.5)에 맞췄다.
            [BitterMist] = new Spec
            {
                kind = "body", frame = new Vector2(128, 128), pivot = new Vector2(0.5f, 0.125f), bodyPx = 90f, targetWorld = 1.5f,
                folder = "FeedbackArt/bitter_mist/", pattern = "{0}_{1}_right_{2:00}",
                clips = new[] { new ClipSpec("idle", 4, 6f, true), new ClipSpec("move", 6, 8f, true), new ClipSpec("action", 6, 10f, false), new ClipSpec("hurt", 1, 12.5f, false), new ClipSpec("defeat", 4, 10f, false) },
            },
            [GlassThief] = new Spec
            {
                kind = "body", frame = new Vector2(128, 128), pivot = new Vector2(0.5f, 0.125f), bodyPx = 70f, targetWorld = 1.15f,
                folder = "FeedbackArt/glass_thief/", pattern = "{0}_{1}_right_{2:00}",
                clips = new[] { new ClipSpec("idle", 4, 6f, true), new ClipSpec("move", 6, 12f, true), new ClipSpec("action", 4, 14f, false), new ClipSpec("flee", 6, 14f, true), new ClipSpec("hurt", 1, 12.5f, false), new ClipSpec("defeat", 4, 10f, false) },
            },
            [BitterMistPulse] = new Spec
            {
                kind = "ground_fx", frame = new Vector2(256, 128), pivot = new Vector2(0.5f, 0.5f), folder = "FeedbackArt/bitter_mist_pulse/", pattern = "{0}_{1}_{2:00}",
                clips = new[] { new ClipSpec("charge", 6, 6f, false), new ClipSpec("burst", 4, 14f, false) },
            },
            [MixingRushAura] = new Spec
            {
                kind = "ground_fx", frame = new Vector2(256, 128), pivot = new Vector2(0.5f, 0.5f), folder = "FeedbackArt/mixing_rush_aura/", pattern = "{0}_{1}_{2:00}",
                clips = new[] { new ClipSpec("active", 6, 8f, true), new ClipSpec("off", 4, 12f, false) },
            },
            [StatusAttackSpeedDown] = StatusSpec(),
            [StatusHealBlock] = StatusSpec(),
        };

        static Spec StatusSpec() => new Spec
        {
            kind = "ui", frame = new Vector2(40, 40), pivot = new Vector2(0.5f, 0.5f), folder = "FeedbackArt/Status/", pattern = "{0}",
            clips = new[] { new ClipSpec("static", 1, 0f, false) },
        };

        public static IEnumerable<string> Ids => table.Keys;
        public static IEnumerable<ClipSpec> ClipsOf(string id) => table.TryGetValue(id, out var s) ? s.clips : null;

        /// <summary>Resources 경로(확장자 없음) — 테스트·검수용.</summary>
        public static string SpritePath(string id, string clip, int frame)
        {
            var s = table[id];
            return s.folder + string.Format(s.pattern, id, clip, frame);
        }

        /// <summary>목록에 없는 ID를 위한 조회. 이 납품의 ID가 아니거나 스프라이트가 하나라도 빠지면 null(호출부가 기존 경로로 떨어진다).</summary>
        public static ArtAsset Load(string id)
        {
            if (string.IsNullOrEmpty(id) || !table.TryGetValue(id, out var s)) return null;
            var clips = new ArtClip[s.clips.Length];
            for (int c = 0; c < clips.Length; c++)
            {
                var spec = s.clips[c];
                var frames = new Sprite[spec.frames];
                for (int i = 0; i < frames.Length; i++)
                {
                    frames[i] = Resources.Load<Sprite>(SpritePath(id, spec.name, i));
                    if (frames[i] == null) return null;
                }
                clips[c] = new ArtClip { name = spec.name, fps = spec.fps, loop = spec.loop, frames = frames };
            }
            bool body = s.kind == "body";
            float footY = s.pivot.y * s.frame.y;
            return new ArtAsset
            {
                assetId = id, kind = s.kind, status = "ready", sourcePath = "ArtSource/feedback-art-2026-11-22/revisions/v1-formal-pixels",
                pixelsPerUnit = 128f, frameSizePx = s.frame, pivotNormalized = s.pivot,
                bodyHeightPx = s.bodyPx, targetBodyHeightWorld = s.targetWorld,
                contentRectNormalized = body ? new Rect(0f, s.pivot.y, 1f, s.bodyPx / s.frame.y) : new Rect(0f, 0f, 1f, 1f),
                clips = clips,
            };
        }
    }
}
