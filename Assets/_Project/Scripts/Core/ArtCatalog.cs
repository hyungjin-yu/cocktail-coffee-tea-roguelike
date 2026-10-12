using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>04/09 아트 1개 클립: 이름, fps, 반복, 프레임 스프라이트(manifest의 files 순서).</summary>
    [Serializable]
    public class ArtClip
    {
        public string name;
        public float fps;
        public bool loop;
        public Sprite[] frames = new Sprite[0];

        public float Duration => frames.Length == 0 ? 0f : fps > 0f ? frames.Length / fps : 0f;

        /// <summary>경과 시간 t의 프레임(반복이면 감고, 아니면 마지막 프레임 유지). fps 0이면 첫 프레임.</summary>
        public Sprite FrameAt(float t)
        {
            if (frames.Length == 0) return null;
            if (fps <= 0f || frames.Length == 1) return frames[0];
            int i = Mathf.FloorToInt(Mathf.Max(0f, t) * fps);
            i = loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
            return frames[i];
        }
    }

    /// <summary>manifest anchorsByFrame 1개(좌하단 원점 픽셀) — 클립/프레임 인덱스로 미리 풀어 둔다.</summary>
    [Serializable]
    public class ArtAnchor
    {
        public string clip;
        public int frame;
        public string name;
        public Vector2 px;
    }

    [Serializable]
    public class ArtTint
    {
        public string name;
        public Color color = Color.white;
    }

    /// <summary>
    /// 런타임 아트 1개(manifest를 에디터가 굽은 값). 상태 구분: ready(소스 검수 통과 + Art/v1 임포트) /
    /// firstPassProvisional(사용자가 1차 실행본에만 명시 허용한 미완성 소스 — 출시 검사 대상 아님).
    /// </summary>
    [Serializable]
    public class ArtAsset
    {
        public string assetId;
        public string kind;
        public string status;
        public bool firstPassProvisional;
        public string sourcePath;
        public float pixelsPerUnit = 128f;
        public Vector2 frameSizePx;
        public Vector2 pivotNormalized = new Vector2(0.5f, 0.5f);
        public float targetBodyHeightWorld;
        public float bodyHeightPx;
        /// <summary>manifest에 bodyHeightPx가 없어 idle_00 알파로 측정했으면 true(보고 대상).</summary>
        public bool bodyHeightEstimated;
        public float runtimeOpacity = 1f;
        public float targetVisualLengthRelativeToPlayer;
        /// <summary>첫 프레임 알파 영역(정규화, 좌하단 원점 xMin,yMin,width,height). 장판 판정 맞춤 등에 쓴다.</summary>
        public Rect contentRectNormalized = new Rect(0f, 0f, 1f, 1f);
        public ArtClip[] clips = new ArtClip[0];
        public ArtAnchor[] anchors = new ArtAnchor[0];
        public ArtTint[] tints = new ArtTint[0];
        public string[] deviations = new string[0];

        public bool IsBody => kind == "body";

        public ArtClip Clip(string clipName)
        {
            foreach (var c in clips)
                if (c != null && c.name == clipName && c.frames.Length > 0) return c;
            return null;
        }

        /// <summary>단일 클립 VFX/UI는 "effect", 없으면 첫 클립.</summary>
        public ArtClip Main => Clip("effect") ?? (clips.Length > 0 ? clips[0] : null);

        public Sprite FirstSprite => Main != null && Main.frames.Length > 0 ? Main.frames[0] : null;

        /// <summary>04 §9: visualScale = targetBodyHeightWorld / (bodyHeightPx / pixelsPerUnit).</summary>
        public float BodyVisualScale =>
            targetBodyHeightWorld > 0f && bodyHeightPx > 0f ? targetBodyHeightWorld / (bodyHeightPx / pixelsPerUnit) : 1f;

        /// <summary>첫 프레임 알파 영역의 월드 크기(스케일 1 기준).</summary>
        public Vector2 ContentWorldSize => new Vector2(
            frameSizePx.x * contentRectNormalized.width / pixelsPerUnit,
            frameSizePx.y * contentRectNormalized.height / pixelsPerUnit);

        public bool TryGetAnchor(string clipName, int frame, string anchorName, out Vector2 px)
        {
            foreach (var a in anchors)
            {
                if (a.name == anchorName && a.clip == clipName && a.frame == frame)
                {
                    px = a.px;
                    return true;
                }
            }
            px = default;
            return false;
        }

        /// <summary>픽셀 anchor → 피벗 기준 로컬 좌표(스케일 1).</summary>
        public Vector2 AnchorToLocal(Vector2 px) =>
            new Vector2((px.x - pivotNormalized.x * frameSizePx.x) / pixelsPerUnit,
                (px.y - pivotNormalized.y * frameSizePx.y) / pixelsPerUnit);

        public bool TryGetTint(string tintName, out Color color)
        {
            foreach (var t in tints)
            {
                if (t.name == tintName)
                {
                    color = t.color;
                    return true;
                }
            }
            color = Color.white;
            return false;
        }
    }

    /// <summary>에디터(CCT/Art/Integrate First Pass Art)가 Art/v1 + Art/firstpass 임포트 결과로 굽는 런타임 목록.</summary>
    public class ArtCatalog : ScriptableObject
    {
        public const string ResourcePath = "ArtV1/ArtCatalog";
        public string generatedUtc;
        public ArtAsset[] assets = new ArtAsset[0];
    }

    /// <summary>
    /// 런타임 아트 조회. 목록에 없는 ID는 null — 호출부가 기존 플레이스홀더/아이콘으로 떨어진다(1차 실행본에서
    /// 아트 누락이 기능을 막지 않게). 어떤 ID가 실제로 쓰였는지는 UsedIds로 기록해 1차 보고에 쓴다.
    /// </summary>
    public static class ArtLibrary
    {
        static ArtCatalog catalog;
        static bool loaded;
        static Dictionary<string, ArtAsset> byId;
        static readonly HashSet<string> used = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            catalog = null;
            loaded = false;
            byId = null;
            used.Clear();
        }

        public static IReadOnlyCollection<string> UsedIds => used;

        public static ArtCatalog Catalog
        {
            get
            {
                EnsureLoaded();
                return catalog;
            }
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            if (catalog == null) catalog = Resources.Load<ArtCatalog>(ArtCatalog.ResourcePath);
            byId = new Dictionary<string, ArtAsset>(StringComparer.Ordinal);
            if (catalog == null) return;
            foreach (var a in catalog.assets)
                if (a != null && !string.IsNullOrEmpty(a.assetId)) byId[a.assetId] = a;
        }

        public static ArtAsset Get(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            EnsureLoaded();
            if (!byId.TryGetValue(assetId, out var a))
            {
                a=TeaEncounterArt.Load(assetId);
                if(a==null) return null;
                byId[assetId]=a;
            }
            used.Add(assetId);
            return a;
        }

        public static Sprite Sprite(string assetId) => Get(assetId)?.FirstSprite;

        /// <summary>테스트 전용 — 목록을 교체한다(null이면 Resources에서 다시 읽음).</summary>
        public static void SetCatalogForTests(ArtCatalog value)
        {
            catalog = value;
            loaded = false;
            byId = null;
            used.Clear();
        }
    }

    /// <summary>
    /// 04 §9 정렬 순서: 배경 → 그림자 → 장판 → 개체(카메라 Y 깊이 정렬) → 공중 효과 → HUD.
    /// 개체끼리는 같은 순서값을 두고 카메라 transparencySortAxis(Y)로 앞뒤를 가른다.
    /// </summary>
    public static class SortOrders
    {
        public const int Background = -10;
        public const int BackgroundProp = -9;
        public const int Shadow = 0;
        public const int GroundZone = 1;
        public const int Actor = 2;
        public const int Projectile = 3;
        public const int AirVfx = 4;
    }
}
