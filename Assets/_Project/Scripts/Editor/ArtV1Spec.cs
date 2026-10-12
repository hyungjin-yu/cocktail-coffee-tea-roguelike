using System;
using System.Collections.Generic;

namespace CCT.Editor
{
    public enum ArtV1Kind
    {
        Unknown,
        Body,
        Vfx,
        Ui,
        Background,
    }

    /// <summary>04 §6 표의 클립 1개: 이름, 프레임 수, 기대 fps(0이면 검사하지 않음), 반복 여부(null이면 검사하지 않음).</summary>
    public sealed class ArtV1ClipSpec
    {
        public readonly string Name;
        public readonly int Frames;
        public readonly float Fps;
        public readonly bool? Loop;

        public ArtV1ClipSpec(string name, int frames, float fps, bool? loop)
        {
            Name = name;
            Frames = frames;
            Fps = fps;
            Loop = loop;
        }
    }

    public sealed class ArtV1BodySpec
    {
        public readonly string AssetId;
        /// <summary>04 §5 세트 기호: A, R, T, E, S, B.</summary>
        public readonly string Set;
        public readonly float TargetHeightWorld;
        public readonly bool IsBoss;
        public readonly IReadOnlyList<ArtV1ClipSpec> Clips;

        public ArtV1BodySpec(string assetId, string set, float targetHeightWorld, bool isBoss, IReadOnlyList<ArtV1ClipSpec> clips)
        {
            AssetId = assetId;
            Set = set;
            TargetHeightWorld = targetHeightWorld;
            IsBoss = isBoss;
            Clips = clips;
        }

        public int FrameSizePx => IsBoss ? 512 : 256;
        /// <summary>좌하단 원점 발 접지선 y (04 §7: 일반 32, 보스 64).</summary>
        public int FootLinePx => IsBoss ? 64 : 32;
        /// <summary>좌우 안전 범위 (04 §7: 일반 16..239, 보스 32..479).</summary>
        public int SafeMinXPx => IsBoss ? 32 : 16;
        public int SafeMaxXPx => IsBoss ? 479 : 239;

        public int TotalFrames
        {
            get
            {
                int n = 0;
                foreach (var c in Clips) n += c.Frames;
                return n;
            }
        }
    }

    /// <summary>VFX/UI/배경 같은 단일 크기 에셋 (04 §8, 09 §2).</summary>
    public sealed class ArtV1FlatSpec
    {
        public readonly string AssetId;
        public readonly ArtV1Kind Kind;
        public readonly int Width;
        public readonly int Height;
        public readonly int Frames;

        public ArtV1FlatSpec(string assetId, ArtV1Kind kind, int width, int height, int frames)
        {
            AssetId = assetId;
            Kind = kind;
            Width = width;
            Height = height;
            Frames = frames;
        }
    }

    /// <summary>
    /// 기획 04 아트 제작 기준 v1.0 / 09 완성판 아트 사운드 명세 v1.0의 고정 수치를 코드로 옮긴 표.
    /// 문서가 바뀌면 이 표도 함께 바꾼다 (테스트가 355/363 총량을 검사한다).
    /// 2026-10-04: 04 §2/§5/§6(2026-09-25 갱신)대로 커피 일반 몹 espresso_bug(R)·milk_foamer(T)를 포함한
    /// 19종 — 기본 355 + 09 진상 손님 review/reject 8 = 363프레임. 옛 17종/321·329 계약은 폐기.
    /// </summary>
    public static class ArtV1Spec
    {
        public const string SpecVersion = "1.0";
        public const int PixelsPerUnit = 128;
        public const float BodyPivotX = 0.5f;
        public const float BodyPivotY = 0.125f;
        public const int BaseBodyFrameTotal = 355;
        public const int FullBodyFrameTotal = 363;

        public static readonly IReadOnlyDictionary<string, ArtV1BodySpec> Bodies = BuildBodies();
        public static readonly IReadOnlyDictionary<string, ArtV1FlatSpec> Flats = BuildFlats();

        public static bool TryGetBody(string assetId, out ArtV1BodySpec spec) =>
            Bodies.TryGetValue(assetId ?? "", out spec);

        public static bool TryGetFlat(string assetId, out ArtV1FlatSpec spec) =>
            Flats.TryGetValue(assetId ?? "", out spec);

        /// <summary>manifest에 kind가 없을 때 asset ID로 종류를 추정한다. 모르면 Unknown.</summary>
        public static ArtV1Kind InferKind(string assetId)
        {
            if (TryGetBody(assetId, out _)) return ArtV1Kind.Body;
            if (TryGetFlat(assetId, out var flat)) return flat.Kind;
            return ArtV1Kind.Unknown;
        }

        public static ArtV1Kind ParseKind(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "body": return ArtV1Kind.Body;
                case "vfx": return ArtV1Kind.Vfx;
                case "ui": return ArtV1Kind.Ui;
                case "background": return ArtV1Kind.Background;
                default: return ArtV1Kind.Unknown;
            }
        }

        /// <summary>04 §9: visualScale = targetBodyHeightWorld / (bodyHeightPx / pixelsPerUnit).</summary>
        public static float VisualScale(float targetBodyHeightWorld, float bodyHeightPx, float pixelsPerUnit = PixelsPerUnit)
        {
            if (bodyHeightPx <= 0f || pixelsPerUnit <= 0f) throw new ArgumentOutOfRangeException(nameof(bodyHeightPx));
            return targetBodyHeightWorld / (bodyHeightPx / pixelsPerUnit);
        }

        static ArtV1ClipSpec Idle => new ArtV1ClipSpec("idle", 4, 6f, true);
        static ArtV1ClipSpec Move => new ArtV1ClipSpec("move", 6, 10f, true);
        static ArtV1ClipSpec Action(string name = "action", int frames = 4) => new ArtV1ClipSpec(name, frames, 12f, false);
        static ArtV1ClipSpec Hurt => new ArtV1ClipSpec("hurt", 1, 0f, null); // 04: Unity가 0.08초 표시
        static ArtV1ClipSpec Defeat => new ArtV1ClipSpec("defeat", 4, 10f, false);

        static List<ArtV1ClipSpec> SetA() => new List<ArtV1ClipSpec> { Idle, Move, Action("melee"), Action("ranged"), Hurt, Defeat };
        static List<ArtV1ClipSpec> SetR() => new List<ArtV1ClipSpec> { Idle, Move, Hurt, Defeat };
        static List<ArtV1ClipSpec> SetTE() => new List<ArtV1ClipSpec> { Idle, Move, Action(), Hurt, Defeat };
        static List<ArtV1ClipSpec> SetS() => new List<ArtV1ClipSpec> { Idle, Action(), Hurt, Defeat };
        static List<ArtV1ClipSpec> SetB(params ArtV1ClipSpec[] extra)
        {
            var list = new List<ArtV1ClipSpec> { Idle, Move, Hurt, Defeat };
            list.AddRange(extra);
            return list;
        }

        static Dictionary<string, ArtV1BodySpec> BuildBodies()
        {
            var d = new Dictionary<string, ArtV1BodySpec>(StringComparer.Ordinal);
            void Add(string id, string set, float h, bool boss, List<ArtV1ClipSpec> clips) =>
                d.Add(id, new ArtV1BodySpec(id, set, h, boss, clips));

            // 04 §5 표 (H = 발/기준면부터 최고 본체까지 목표 월드 높이)
            Add("bartender", "A", 1.60f, false, SetA());
            Add("tea_sommelier", "A", 1.60f, false, SetA());
            Add("lime_wedge", "R", 0.90f, false, SetR());
            Add("lemon_brute", "R", 1.40f, false, SetR());
            Add("shaker_golem", "T", 1.25f, false, SetTE());
            Add("olive_bomber", "R", 0.85f, false, SetR());
            Add("swirl_stick_conjuror", "S", 1.20f, false, SetS());
            Add("martini_spirit", "E", 1.45f, false, SetTE());
            Add("teabag_imp", "R", 0.90f, false, SetR());
            Add("kettle_cannon", "T", 1.25f, false, SetTE());
            Add("sugar_cube_bomber", "R", 0.85f, false, SetR());
            Add("leaf_warden", "S", 1.20f, false, SetS());
            Add("tea_ceremony_guardian", "E", 1.45f, false, SetTE());
            // 04 §5/§6(2026-09-25 갱신) 커피 일반 몹 2종
            Add("espresso_bug", "R", 0.90f, false, SetR());
            Add("milk_foamer", "T", 1.25f, false, SetTE());
            // 04 §6 보스 전용 클립. fps가 문서에 없는 클립은 0(검사 안 함).
            Add("grinder_golem", "B", 2.40f, true, SetB(
                new ArtV1ClipSpec("charge_prepare", 4, 0f, false),
                new ArtV1ClipSpec("charge", 2, 0f, true)));
            Add("drip_server_specter", "B", 2.20f, true, SetB(
                new ArtV1ClipSpec("drip", 4, 0f, null),
                new ArtV1ClipSpec("vanish", 4, 0f, false),
                new ArtV1ClipSpec("appear", 4, 0f, false)));
            Add("espresso_leviathan", "B", 3.10f, true, SetB(
                new ArtV1ClipSpec("steam", 4, 0f, null),
                new ArtV1ClipSpec("rain", 4, 0f, null),
                new ArtV1ClipSpec("pressure", 4, 0f, null)));
            // 04: refund/summon, 09 §1: review/reject 4프레임 12fps 추가 → 31
            Add("difficult_customer", "B", 2.65f, true, SetB(
                new ArtV1ClipSpec("refund", 4, 0f, null),
                new ArtV1ClipSpec("summon", 4, 0f, null),
                new ArtV1ClipSpec("review", 4, 12f, false),
                new ArtV1ClipSpec("reject", 4, 12f, false)));
            return d;
        }

        static Dictionary<string, ArtV1FlatSpec> BuildFlats()
        {
            var d = new Dictionary<string, ArtV1FlatSpec>(StringComparer.Ordinal);
            void Add(string id, ArtV1Kind kind, int w, int h, int frames) =>
                d.Add(id, new ArtV1FlatSpec(id, kind, w, h, frames));

            // 04 §8 VFX
            Add("ice_shard", ArtV1Kind.Vfx, 128, 128, 1);
            Add("water_drop", ArtV1Kind.Vfx, 128, 128, 1);
            Add("glass_shard", ArtV1Kind.Vfx, 128, 128, 1);
            Add("bullet", ArtV1Kind.Vfx, 128, 128, 1);
            Add("arrow", ArtV1Kind.Vfx, 128, 128, 1);
            Add("melee_streak", ArtV1Kind.Vfx, 128, 128, 4);
            Add("hit_spark", ArtV1Kind.Vfx, 128, 128, 4);
            Add("olive_fire", ArtV1Kind.Vfx, 256, 128, 4);
            Add("sugar_pool", ArtV1Kind.Vfx, 256, 128, 1);
            Add("coffee_pool", ArtV1Kind.Vfx, 256, 128, 1);
            Add("summon_ring", ArtV1Kind.Vfx, 256, 128, 4);
            Add("steam_jet", ArtV1Kind.Vfx, 128, 128, 4);
            Add("warning_ring", ArtV1Kind.Vfx, 256, 128, 1);
            Add("warning_line", ArtV1Kind.Vfx, 128, 128, 1);
            Add("pressure_wave", ArtV1Kind.Vfx, 256, 128, 4);
            Add("rage_overlay", ArtV1Kind.Vfx, 512, 512, 1);
            Add("ground_shadow", ArtV1Kind.Vfx, 256, 128, 1);
            Add("leaf_mote", ArtV1Kind.Vfx, 128, 128, 1);
            // 04 §8 배경
            Add("cafe_wall", ArtV1Kind.Background, 1024, 512, 1);
            Add("cafe_floor", ArtV1Kind.Background, 512, 256, 1);
            Add("cafe_counter", ArtV1Kind.Background, 512, 256, 1);
            Add("cafe_lamp", ArtV1Kind.Background, 128, 256, 1);
            // 04 §8 UI
            Add("ui_panel", ArtV1Kind.Ui, 256, 256, 1);
            Add("ui_card_frame", ArtV1Kind.Ui, 256, 384, 1);
            Add("ui_button", ArtV1Kind.Ui, 256, 64, 1);
            Add("status_slow", ArtV1Kind.Ui, 128, 128, 1);
            Add("status_suppressed", ArtV1Kind.Ui, 128, 128, 1);
            Add("status_invulnerable", ArtV1Kind.Ui, 128, 128, 1);
            Add("specialty", ArtV1Kind.Ui, 128, 128, 1);
            // 09 §2 추가 20항목
            Add("hybrid_tea_martini", ArtV1Kind.Ui, 128, 128, 1);
            Add("weapon_tea_bow", ArtV1Kind.Vfx, 256, 256, 1);
            Add("tea_arrow", ArtV1Kind.Vfx, 128, 128, 1);
            Add("review_star", ArtV1Kind.Vfx, 128, 128, 4);
            Add("seal_lock", ArtV1Kind.Ui, 128, 128, 1);
            Add("seal_burst", ArtV1Kind.Vfx, 128, 128, 4);
            // User change 35 retires player-centred flame/poison floor art from the active release contract.
            Add("player_melee_area", ArtV1Kind.Vfx, 256, 128, 1);
            Add("boss_arrival", ArtV1Kind.Vfx, 256, 128, 4);
            Add("pickup_glint", ArtV1Kind.Vfx, 128, 128, 4);
            Add("levelup_glint", ArtV1Kind.Vfx, 128, 128, 4);
            Add("skill_attack", ArtV1Kind.Ui, 128, 128, 1);
            Add("skill_survival", ArtV1Kind.Ui, 128, 128, 1);
            Add("skill_ingredient", ArtV1Kind.Ui, 128, 128, 1);
            Add("ui_pause", ArtV1Kind.Ui, 128, 128, 1);
            Add("ui_settings", ArtV1Kind.Ui, 128, 128, 1);
            Add("ui_codex", ArtV1Kind.Ui, 128, 128, 1);
            Add("ui_save_ok", ArtV1Kind.Ui, 128, 128, 1);
            Add("ui_save_error", ArtV1Kind.Ui, 128, 128, 1);
            // 31 UI polish: additional IDs preserve every original 04/09 canvas contract.
            Add("ui_cafe_panel", ArtV1Kind.Ui, 96, 96, 1);
            Add("ui_cafe_button", ArtV1Kind.Ui, 128, 40, 4);
            Add("ui_cafe_card", ArtV1Kind.Ui, 96, 128, 1);
            Add("ui_cafe_tab", ArtV1Kind.Ui, 80, 32, 4);
            Add("ui_cafe_bar_frame", ArtV1Kind.Ui, 128, 16, 1);
            Add("ui_cafe_bar_fill", ArtV1Kind.Ui, 32, 8, 1);
            Add("ui_cafe_focus", ArtV1Kind.Ui, 96, 96, 1);
            Add("ui_cafe_header", ArtV1Kind.Ui, 96, 24, 1);
            return d;
        }
    }
}
