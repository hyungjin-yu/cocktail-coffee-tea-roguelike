using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>치장 스킨 1종. 능력치·밸런스에는 전혀 관여하지 않는다(몸체 스프라이트만 교체).</summary>
    public class SkinDefinition
    {
        public string productId;
        public Job job;
        public string displayName;
        public string description;
        /// <summary>Resources/CosmeticSkins/&lt;productId&gt;/ — 파일 이름이 원본 몸체 프레임 이름과 같다.</summary>
        public string ResourceFolder => SkinCatalog.ResourceRoot + productId;
    }

    /// <summary>스킨 목록 + 장착 해석(이번 런 직업·스토어 소유·장착·아트 유무).</summary>
    public static class SkinCatalog
    {
        public const string ResourceRoot = "CosmeticSkins/";
        public const string BartenderMidnight = "skin_bartender_midnight";
        public const string TeaJade = "skin_tea_jade";

        public static readonly SkinDefinition[] All =
        {
            new SkinDefinition
            {
                productId = BartenderMidnight, job = Job.Bartender, displayName = "미드나잇 바텐더",
                description = "바텐더 전용 · 한밤의 바 색감 스킨\n외형만 바뀌며 능력치는 같습니다.",
            },
            new SkinDefinition
            {
                productId = TeaJade, job = Job.TeaSommelier, displayName = "비취 티 소믈리에",
                description = "티 소믈리에 전용 · 비취빛 찻잎 색감 스킨\n외형만 바뀌며 능력치는 같습니다.",
            },
        };

        public static IReadOnlyList<string> ProductIds
        {
            get
            {
                var ids = new List<string>();
                foreach (var s in All) ids.Add(s.productId);
                return ids;
            }
        }

        public static SkinDefinition Find(string productId)
        {
            foreach (var s in All) if (s.productId == productId) return s;
            return null;
        }

        public static bool IsKnown(string productId) => Find(productId) != null;

        /// <summary>
        /// 이번 런에 입힐 스킨(없으면 null = 기본 아트). 조건: 해당 직업 스킨이 장착돼 있고, 현재 스토어프런트에서 소유,
        /// 아트가 실제로 있음. 하나라도 빠지면 기본 아트.
        /// </summary>
        public static SkinDefinition ResolveEquipped(Job job, CommerceState state, string storefront, Func<SkinDefinition, bool> artExists)
        {
            if (state == null) return null;
            var id = state.EquippedFor(job);
            var def = Find(id);
            if (def == null || def.job != job) return null;
            if (!state.IsOwned(storefront, def.productId)) return null;
            if (artExists != null && !artExists(def)) return null;
            return def;
        }
    }

    /// <summary>스킨 스프라이트 사전(원본 스프라이트 이름 → 스킨 스프라이트). Resources에서 한 번 읽어 캐시.</summary>
    public static class SkinArt
    {
        static readonly Dictionary<string, Dictionary<string, Sprite>> cache = new Dictionary<string, Dictionary<string, Sprite>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        /// <summary>스킨 프레임 사전. 아트가 없으면 빈 사전.</summary>
        public static Dictionary<string, Sprite> Sprites(SkinDefinition def)
        {
            if (def == null) return new Dictionary<string, Sprite>();
            if (cache.TryGetValue(def.productId, out var map) && map != null) return map;
            map = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var s in Resources.LoadAll<Sprite>(def.ResourceFolder))
                if (s != null && !map.ContainsKey(s.name)) map[s.name] = s;
            cache[def.productId] = map;
            return map;
        }

        public static bool Exists(SkinDefinition def) => Sprites(def).Count > 0;

        /// <summary>직업 기본 대기 첫 프레임(미리보기 기준).</summary>
        public static Sprite BaseIdleSprite(Job job)
        {
            var art = ArtLibrary.Get(JobPreset.For(job).bodyArtId);
            var clip = art?.Clip("idle");
            return clip != null && clip.frames.Length > 0 ? clip.frames[0] : null;
        }

        /// <summary>미리보기: 기본 대기 첫 프레임의 스킨 버전. 없으면 null(호출부가 기본 아트 + "아트 준비 중").</summary>
        public static Sprite PreviewSprite(SkinDefinition def)
        {
            if (def == null) return null;
            var map = Sprites(def);
            var baseSprite = BaseIdleSprite(def.job);
            if (baseSprite != null && map.TryGetValue(baseSprite.name, out var s)) return s;
            // 기본 아트가 없는 환경 — 이름 규칙으로 찾는다.
            var fallbackName = JobPreset.For(def.job).bodyArtId + "_idle_right_00";
            return map.TryGetValue(fallbackName, out s) ? s : null;
        }
    }
}
