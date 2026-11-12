using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-12 UI 아이콘 묶음(ArtSource/ui-icons-2026-11-12, 아트 담당 매니페스트 ready 파일만 Resources로 임포트) 조회.
    /// 커피/우림 연구 노드 = ResearchIcons/research_&lt;skillId&gt;(티 소믈리에는 research_&lt;skillId&gt;_tea 우선 — 티 이름이 커피 도구와 다른 노드용 변형),
    /// 연구 노트북 숙련 노드 = ResearchIcons/mastery_&lt;traitId&gt;. 파일이 없으면 null → 호출자는 기존 표시(글 상자·공용 원두)를 그대로 쓴다.
    /// 공용/대체 아이콘을 전용 그림처럼 반환하지 않는다.
    /// </summary>
    public static class ResearchIcons
    {
        public const string Folder = "ResearchIcons/";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Dictionary<string, RectInt> bounds;

        /// <summary>임포트 도구가 PNG 알파로 계산한 불투명 영역(bounds.txt: "이름 x0 y0 x1 y1", 위 기준·끝 미포함).
        /// 가로로 긴 그림도 표시 칸을 채우도록 같은 텍스처의 부분 사각형으로 자른다(원본 PNG·텍스처 불변).</summary>
        static Sprite Load(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>(Folder + name);
            if (s != null) s = Cropped(s, name);
            cache[name] = s;
            return s;
        }

        static Sprite Cropped(Sprite src, string name)
        {
            if (bounds == null)
            {
                bounds = new Dictionary<string, RectInt>();
                var txt = Resources.Load<TextAsset>(Folder + "bounds");
                if (txt != null)
                    foreach (var line in txt.text.Split('\n'))
                    {
                        var p = line.Trim().Split(' ');
                        if (p.Length == 5 && int.TryParse(p[1], out var x0) && int.TryParse(p[2], out var y0) && int.TryParse(p[3], out var x1) && int.TryParse(p[4], out var y1))
                            bounds[p[0]] = new RectInt(x0, y0, x1 - x0, y1 - y0);
                    }
            }
            if (src.packed || !bounds.TryGetValue(name, out var b)) return src;
            var r = src.rect;
            if (b.width <= 0 || b.height <= 0 || b.xMax > r.width || b.yMax > r.height) return src;
            int pad = 1;
            float x = Mathf.Max(0, b.x - pad), yTop = Mathf.Max(0, b.y - pad);
            float w = Mathf.Min(r.width - x, b.width + pad * 2), h = Mathf.Min(r.height - yTop, b.height + pad * 2);
            var crop = new Rect(r.x + x, r.y + (r.height - yTop - h), w, h); // 텍스처 좌표는 아래 기준
            var sp = Sprite.Create(src.texture, crop, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sp.name = src.name;
            return sp;
        }

        static HashSet<string> teaRequired;
        /// <summary>티 변형이 계획된 노드(임포트 도구가 매니페스트 research_tea 항목으로 생성). 이 노드는 티 소믈리에에게 커피 그림을 보이지 않는다.</summary>
        public static bool TeaVariantRequired(string skillId)
        {
            if (teaRequired == null)
            {
                teaRequired = new HashSet<string>();
                var txt = Resources.Load<TextAsset>(Folder + "tea_variant_required");
                if (txt != null) foreach (var l in txt.text.Split('\n')) { var t = l.Trim(); if (t.Length > 0) teaRequired.Add(t); }
            }
            return teaRequired.Contains(skillId);
        }

        public static Sprite Research(string skillId, Job job)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            if (job == Job.TeaSommelier)
            {
                var tea = Load("research_" + skillId + "_tea");
                if (tea != null) return tea;
                if (TeaVariantRequired(skillId)) return null; // 차 그림이 아직 없으면 커피 그림 대신 기존 글 상자
            }
            return Load("research_" + skillId);
        }

        /// <summary>티 소믈리에가 공용 그림이 아니라 차 전용 변형을 보는지(시험·검수용).</summary>
        public static bool HasTeaVariant(string skillId) => Load("research_" + skillId + "_tea") != null;

        public static Sprite Mastery(string traitId) => string.IsNullOrEmpty(traitId) ? null : Load("mastery_" + traitId);

        /// <summary>시험 전용: 캐시 비우기(임포트 전후 비교).</summary>
        public static void ClearCacheForTests() { cache.Clear(); teaRequired = null; bounds = null; }
    }
}
