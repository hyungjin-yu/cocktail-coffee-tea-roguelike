using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 그릇·도구 아트의 픽셀 기하(마스크 단면 용량, 안쪽 판정, 가로 구간, 바스푼 행 구간, 아래 투명 행).
    /// 스프라이트 로드·포장·임시 그림은 DrinkArt가 맡고, 이 클래스는 그 결과(마스크·JSON 표)를 읽어 형태만 계산한다.
    /// DrinkArt의 같은 이름 공개 함수는 이쪽으로 넘긴다(기존 호출자 유지).
    /// </summary>
    public static class DrinkVesselGeometry
    {
        static readonly Dictionary<string, FluidVesselProfile> profiles = new Dictionary<string, FluidVesselProfile>();

        sealed class MaskPixels { public Color32[] px; public int x0, y0, w, h, texW; }
        static readonly Dictionary<string, MaskPixels> maskPixels = new Dictionary<string, MaskPixels>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            profiles.Clear();
            maskPixels.Clear();
            spoonRows = null;
        }

        /// <summary>
        /// 그릇 누적 용량 C(h): 마스크(그릇 안쪽 단면)의 각 행 불투명 폭(아래→위)을 회전 대칭으로 보고 capacityMl로 정규화(71 §2).
        /// 마스크를 읽을 수 없으면 액체 사각형 폭의 원통으로 근사. 같은 그릇·용량은 캐시한다.
        /// </summary>
        public static FluidVesselProfile Profile(string assetId, float capacityMl)
        {
            string key = assetId + "@" + capacityMl.ToString("0.###");
            if (profiles.TryGetValue(key, out var cached)) return cached;
            FluidVesselProfile result = null;
            var mask = DrinkArt.Mask(assetId);
            var lr = DrinkArt.LiquidRect(assetId);
            try
            {
                if (mask != null && mask.texture != null && mask.texture.isReadable)
                {
                    var tex = mask.texture;
                    var r = mask.rect;
                    int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height;
                    int rowFrom = Mathf.Clamp(Mathf.FloorToInt(lr.yMin * h + 0.001f), 0, h - 1);
                    int rowTo = Mathf.Clamp(Mathf.CeilToInt(lr.yMax * h - 0.001f), rowFrom + 1, h);
                    var px = tex.GetPixels32();
                    var widths = new List<double>();
                    for (int y = rowFrom; y < rowTo; y++)
                    {
                        int n = 0;
                        for (int x = 0; x < w; x++) if (px[(y0 + y) * tex.width + x0 + x].a > 127) n++;
                        widths.Add(Mathf.Max(1, n));
                    }
                    if (widths.Count >= 2) result = FluidVesselProfile.FromRowWidths(widths.ToArray(), capacityMl);
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[DrinkArt] 프로필 계산 실패 {assetId}: {e.Message}"); }
            if (result == null)
            {
                // 원통 근사: 높이는 액체 사각형 비율(가로 대비)에서
                float aspect = Mathf.Max(0.2f, (lr.yMax - lr.yMin) / Mathf.Max(0.05f, lr.xMax - lr.xMin));
                var widths = new double[16];
                for (int i = 0; i < widths.Length; i++) widths[i] = widths.Length / aspect;
                result = FluidVesselProfile.FromRowWidths(widths, capacityMl);
            }
            profiles[key] = result;
            return result;
        }

        /// <summary>그릇 안쪽(마스크 불투명) 여부. 정규화 좌표(왼쪽 아래 0,0). 마스크를 읽을 수 없으면 액체 사각형으로 판정.</summary>
        public static bool InsideMask(string assetId, Vector2 n)
        {
            if (!maskPixels.TryGetValue(assetId, out var m))
            {
                m = null;
                var mask = DrinkArt.Mask(assetId);
                if (mask != null && mask.texture != null && mask.texture.isReadable)
                {
                    var r = mask.rect;
                    m = new MaskPixels { px = mask.texture.GetPixels32(), x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height, texW = mask.texture.width };
                }
                maskPixels[assetId] = m;
            }
            if (m == null) return DrinkArt.LiquidRect(assetId).Contains(n);
            int x = Mathf.FloorToInt(n.x * m.w), y = Mathf.FloorToInt(n.y * m.h);
            if (x < 0 || y < 0 || x >= m.w || y >= m.h) return false;
            return m.px[(m.y0 + y) * m.texW + m.x0 + x].a > 127;
        }

        /// <summary>그 높이(정규화 y)에서 그릇 안쪽 마스크의 가로 구간(정규화 x). 마스크를 읽을 수 없으면 액체 사각형 폭.</summary>
        public static void MaskSpan(string assetId, float y01, out float xMin, out float xMax)
        {
            var lr = DrinkArt.LiquidRect(assetId);
            xMin = lr.xMax; xMax = lr.xMin;
            const int steps = 192;
            for (int i = 0; i < steps; i++)
            {
                float x = (i + 0.5f) / steps;
                if (!InsideMask(assetId, new Vector2(x, y01))) continue;
                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
            }
            if (xMax < xMin) { xMin = lr.xMin; xMax = lr.xMax; }
        }

        static int[][] spoonRows; // [frame][row*2] (아래→위), -1 = 빈 행
        static int spoonW, spoonH;

        static Dictionary<string, int> alphaBottom;

        /// <summary>그 스프라이트 아래쪽 완전 투명 행 수(아트 px, dl_alpha_bottom.json). 잔·다관의 보이는 바닥을 상판 윗면에 맞출 때 쓴다. 모르면 0.</summary>
        public static int AlphaBottom(string assetId)
        {
            if (alphaBottom == null)
            {
                alphaBottom = new Dictionary<string, int>();
                var ta = Resources.Load<TextAsset>("DrinkLab/dl_alpha_bottom");
                if (ta != null)
                {
                    // {"rows": {"id": n, ...}} — 평평한 정수 맵이라 간단히 읽는다(JsonUtility는 사전을 읽지 못함).
                    var text = ta.text;
                    int at = text.IndexOf("\"rows\"", StringComparison.Ordinal);
                    if (at >= 0)
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text.Substring(at), "\"([A-Za-z0-9_]+)\"\\s*:\\s*(\\d+)"))
                            alphaBottom[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
                }
            }
            return assetId != null && alphaBottom.TryGetValue(assetId, out int n) ? n : 0;
        }

        /// <summary>바스푼 프레임의 행별 불투명 x 구간(아트 픽셀, 아래→위). Resources/DrinkLab/dl_barspoon_extent.json(실제 PNG에서 생성).</summary>
        public static bool SpoonExtent(int frame, out int[] rows, out int width, out int height)
        {
            if (spoonRows == null)
            {
                spoonRows = new int[0][];
                var text = Resources.Load<TextAsset>(DrinkArt.ResourceFolder + "dl_barspoon_extent");
                if (text != null)
                {
                    // [[min,max],…] 중첩 배열은 JsonUtility가 읽지 못하므로 숫자만 순서대로 뽑는다.
                    var frames = new List<int[]>();
                    var chunks = text.text.Split(new[] { "\"rows\"" }, StringSplitOptions.None);
                    for (int c = 1; c < chunks.Length; c++) // 첫 조각은 머리말
                    {
                        int end = chunks[c].IndexOf("]]", StringComparison.Ordinal);
                        if (end < 0) continue;
                        var nums = new List<int>();
                        foreach (var tok in chunks[c].Substring(0, end).Split(new[] { '[', ']', ',', ':', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(tok, out int v)) nums.Add(v);
                        frames.Add(nums.ToArray());
                    }
                    spoonRows = frames.ToArray();
                    var spr = DrinkArt.Sprite("dl_barspoon");
                    spoonW = spr != null ? (int)spr.rect.width : 16;
                    spoonH = spoonRows.Length > 0 ? spoonRows[0].Length / 2 : 0;
                }
            }
            width = spoonW; height = spoonH;
            if (spoonRows.Length == 0) { rows = null; return false; }
            rows = spoonRows[((frame % spoonRows.Length) + spoonRows.Length) % spoonRows.Length];
            return true;
        }
    }
}
