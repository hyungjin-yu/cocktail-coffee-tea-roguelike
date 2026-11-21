using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-20 카페 첫 화면에서 연 메뉴 화면(영구 강화·도감·스킨·최근 기록·설정·제작 정보)만 카페의 따뜻한 나무·종이 톤으로 바꾼다.
    /// 같은 화면 클래스를 쓰는 전투 일시정지·결과·직업 선택 인스턴스에는 붙이지 않는다(타이틀이 만든 루트에만 Attach).
    /// 규칙: 무채색·보라 계열의 바탕/패널/중립 버튼/글자만 같은 밝기(상대 휘도)의 나무·종이 색으로 옮긴다 → 글자 대비 유지.
    /// 의미 있는 색(빨강 경고·초록·파랑 선택/확인·금색)은 그대로, 알파 그대로(비활성 반투명 의미 유지). 아이콘 스프라이트(흰 바탕 아님)는 건드리지 않는다.
    /// 화면이 나중에 만들거나 색을 바꾼 요소도 프레임마다 확인해 같은 규칙으로 맞춘다(원래 색을 기억해 이중 변환 없음).
    /// </summary>
    public sealed class CafeMenuTheme : MonoBehaviour
    {
        readonly Dictionary<Graphic, Color> applied = new Dictionary<Graphic, Color>(); // 내가 마지막으로 칠한 색
        Graphic[] cache = new Graphic[0];
        int lastCount = -1;

        public static CafeMenuTheme Attach(GameObject root) => root.GetComponent<CafeMenuTheme>() ?? root.AddComponent<CafeMenuTheme>();

        void OnEnable() { lastCount = -1; Apply(); }
        void LateUpdate() => Apply();

        public void Apply()
        {
            int count = transform.hierarchyCount;
            if (count != lastCount) { cache = GetComponentsInChildren<Graphic>(true); lastCount = count; }
            foreach (var g in cache)
            {
                if (g == null) continue;
                var cur = g.color;
                if (applied.TryGetValue(g, out var mine) && mine == cur) continue; // 이미 내가 칠한 그대로
                if (!Themable(g)) { applied[g] = cur; continue; }
                var next = g is Image im && im.sprite != null ? MapSpriteTint(cur) : Map(cur, g is Text);
                g.color = next;
                applied[g] = next;
            }
        }

        static bool Themable(Graphic g)
        {
            if (g is Text) return true;
            if (g is Image img)
            {
                // 아이콘·그림 스프라이트는 제외, 단색/9-slice 패널(ui_panel 등)만
                if (img.sprite == null) return true;
                return img.type == Image.Type.Sliced || img.type == Image.Type.Tiled;
            }
            return false;
        }

        // 나무(어두운 바탕·패널·중립 버튼)와 종이(밝은 글자) 기준색
        static readonly Color WoodHue = new Color(0.36f, 0.24f, 0.15f);
        static readonly Color PaperHue = new Color(1f, 0.94f, 0.82f);

        /// <summary>나무 판 그림(9-slice 카페 패널 등)에 곱하는 따뜻한 색: 흰색 틴트면 보라 기운(파랑)을 덜어 나무색으로. 색이 있는 틴트는 일반 규칙.</summary>
        public static readonly Color PanelWarmTint = new Color(1f, 0.92f, 0.62f);

        public static Color MapSpriteTint(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            if (s < 0.1f && v > 0.9f) return new Color(PanelWarmTint.r, PanelWarmTint.g, PanelWarmTint.b, c.a);
            return Map(c, false);
        }

        /// <summary>색 변환(시험 공개). 의미 색·알파는 그대로, 무채색/보라는 같은 휘도의 나무(어두움)~종이(밝음) 색으로.</summary>
        public static Color Map(Color c, bool isText)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            bool purple = h >= 0.68f && h <= 0.95f && s >= 0.12f;
            bool neutral = s < 0.25f || (purple && v < 0.75f);
            bool semantic = !neutral && !purple; // 빨강·주황·노랑·초록·청록·파랑 계열(경고/선택/확인/보상)
            if (semantic) return c;
            float lum = Luma(c);
            // 어두운 바탕은 아주 조금 밝혀 나무 결 색이 보이게(순서 유지, 글자는 그대로): 0.05 + 1.15×휘도, 0.35 이하 구간만
            if (!isText && lum < 0.35f) lum = Mathf.Min(0.35f, 0.05f + lum * 1.15f);
            // 밝은 쪽(글자·밝은 패널)은 종이, 어두운 쪽은 나무 색조로 같은 휘도 맞춤
            Color baseHue = lum > 0.5f || isText ? PaperHue : WoodHue;
            float bl = Luma(baseHue);
            float k = bl > 1e-4f ? lum / bl : 0f;
            var o = new Color(Mathf.Clamp01(baseHue.r * k), Mathf.Clamp01(baseHue.g * k), Mathf.Clamp01(baseHue.b * k), c.a);
            // 밝은 끝에서 잘려 휘도가 떨어지면 남은 만큼 흰색 쪽으로(글자 대비 보존)
            float ol = Luma(o);
            if (ol + 1e-3f < lum) { float t = Mathf.Clamp01((lum - ol) / Mathf.Max(1e-3f, 1f - ol)); o = Color.Lerp(o, new Color(1f, 1f, 1f, c.a), t); }
            return o;
        }

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
