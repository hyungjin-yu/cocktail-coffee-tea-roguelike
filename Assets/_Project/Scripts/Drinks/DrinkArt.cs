using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 69 음료 제조 아트 로더. Codex(Aseprite+PixelLab) 원본을 Claude가 Resources/DrinkLab/&lt;assetId&gt;.png(프레임은 _00…)로 복사한다.
    /// 그릇은 빈 잔 + 같은 크기의 &lt;assetId&gt;_mask.png(액체 영역). 액체 색·층·탁도·기포는 코드가 그린다.
    /// 아트가 아직 없으면 단색 실루엣 임시 그림(IsPlaceholder=true)으로 기능만 동작시킨다 — 보고서에서 ready/integrated와 구분한다.
    /// </summary>
    public static class DrinkArt
    {
        public const string ResourceFolder = "DrinkLab/";

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Sprite> placeholders = new Dictionary<string, Sprite>();
        static DrinkAnchorTable anchors;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            frameCache.Clear();
            profiles.Clear();
            placeholders.Clear();
            anchors = null;
        }

        /// <summary>실제 아트(없으면 null).</summary>
        public static Sprite Sprite(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            if (cache.TryGetValue(assetId, out var s)) return s;
            s = Resources.Load<Sprite>(ResourceFolder + assetId);
            if (s == null) s = Resources.Load<Sprite>(ResourceFolder + assetId + "_00");
            cache[assetId] = s;
            return s;
        }

        public static bool Has(string assetId) => Sprite(assetId) != null;

        /// <summary>프레임 애니메이션(_00, _01 …). 없으면 단일 스프라이트 1장 또는 빈 배열.</summary>
        public static Sprite[] Frames(string assetId)
        {
            if (frameCache.TryGetValue(assetId, out var f)) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < 32; i++)
            {
                var s = Resources.Load<Sprite>($"{ResourceFolder}{assetId}_{i:00}");
                if (s == null) break;
                list.Add(s);
            }
            if (list.Count == 0)
            {
                var single = Resources.Load<Sprite>(ResourceFolder + assetId);
                if (single != null) list.Add(single);
            }
            f = list.ToArray();
            frameCache[assetId] = f;
            return f;
        }

        /// <summary>실제 아트 또는 임시 실루엣.</summary>
        public static Sprite SpriteOrPlaceholder(string assetId, out bool placeholder)
        {
            var s = Sprite(assetId);
            placeholder = s == null;
            return s ?? Placeholder(assetId);
        }

        public static Sprite Mask(string assetId) => Sprite(assetId + "_mask");

        /// <summary>그릇의 액체 영역(0~1, 스프라이트 기준 bottom/top/left/right). 매니페스트 앵커 → 마스크 → 기본값 순.</summary>
        public static Rect LiquidRect(string assetId)
        {
            var a = Anchors(assetId);
            if (a != null && a.liquid != null && a.liquid.Length == 4)
                return Rect.MinMaxRect(a.liquid[0], a.liquid[1], a.liquid[2], a.liquid[3]);
            return DefaultLiquid(assetId);
        }

        /// <summary>앵커(0~1 정규화, 왼쪽 아래 원점). 없으면 기본값.</summary>
        public static Vector2 Anchor(string assetId, string name)
        {
            var a = Anchors(assetId);
            if (a != null)
            {
                float[] v = name switch { "pourLip" => a.pourLip, "mouth" => a.mouth, "grip" => a.grip, _ => null };
                if (v != null && v.Length == 2) return new Vector2(v[0], v[1]);
            }
            return name switch
            {
                "pourLip" => new Vector2(0.5f, 1f),
                "mouth" => new Vector2(0.5f, 1f),
                _ => new Vector2(0.5f, 0.35f),
            };
        }

        static DrinkAnchorEntry Anchors(string assetId)
        {
            if (anchors == null)
            {
                var text = Resources.Load<TextAsset>(ResourceFolder + "anchors");
                anchors = text != null ? JsonUtility.FromJson<DrinkAnchorTable>(text.text) ?? new DrinkAnchorTable() : new DrinkAnchorTable();
            }
            if (anchors.entries == null) return null;
            foreach (var e in anchors.entries) if (e != null && e.assetId == assetId) return e;
            return null;
        }

        static readonly Dictionary<string, FluidVesselProfile> profiles = new Dictionary<string, FluidVesselProfile>();

        /// <summary>
        /// 그릇 누적 용량 C(h): 마스크(그릇 안쪽 단면)의 각 행 불투명 폭(아래→위)을 회전 대칭으로 보고 capacityMl로 정규화(71 §2).
        /// 마스크를 읽을 수 없으면 액체 사각형 폭의 원통으로 근사. 같은 그릇·용량은 캐시한다.
        /// </summary>
        public static FluidVesselProfile Profile(string assetId, float capacityMl)
        {
            string key = assetId + "@" + capacityMl.ToString("0.###");
            if (profiles.TryGetValue(key, out var cached)) return cached;
            FluidVesselProfile result = null;
            var mask = Mask(assetId);
            var lr = LiquidRect(assetId);
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

        static Rect DefaultLiquid(string assetId) => assetId switch
        {
            "dl_jigger" => Rect.MinMaxRect(0.22f, 0.52f, 0.78f, 0.95f),
            "dl_shaker" => Rect.MinMaxRect(0.18f, 0.06f, 0.82f, 0.70f),
            "dl_mixing_glass" => Rect.MinMaxRect(0.14f, 0.06f, 0.86f, 0.86f),
            "dl_glass_cocktail" => Rect.MinMaxRect(0.16f, 0.08f, 0.84f, 0.84f),
            "dl_teapot" => Rect.MinMaxRect(0.18f, 0.12f, 0.70f, 0.70f),
            "dl_teacup" => Rect.MinMaxRect(0.16f, 0.14f, 0.84f, 0.80f),
            _ => Rect.MinMaxRect(0.15f, 0.1f, 0.85f, 0.85f),
        };

        // ───── 임시 실루엣(아트 도착 전 기능 검증용, 보고서에 placeholder로 구분) ─────

        static Sprite Placeholder(string assetId)
        {
            if (placeholders.TryGetValue(assetId, out var p)) return p;
            Vector2Int size = assetId switch
            {
                "dl_jigger" => new Vector2Int(32, 48),
                "dl_shaker" => new Vector2Int(48, 80),
                "dl_mixing_glass" => new Vector2Int(48, 64),
                "dl_barspoon" => new Vector2Int(16, 80),
                "dl_glass_cocktail" => new Vector2Int(48, 64),
                "dl_chaha" => new Vector2Int(48, 24),
                "dl_teapot" => new Vector2Int(64, 56),
                "dl_teacup" => new Vector2Int(40, 32),
                "dl_water_kettle" => new Vector2Int(48, 56),
                _ when assetId.StartsWith("dl_bottle_") => new Vector2Int(24, 56),
                _ when assetId.StartsWith("dl_tin_") => new Vector2Int(32, 32),
                _ => new Vector2Int(32, 32),
            };
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "placeholder_" + assetId };
            var px = new Color32[size.x * size.y];
            var outline = new Color32(40, 34, 48, 255);
            var glass = new Color32(200, 220, 235, 110);
            var body = BodyColor(assetId);
            bool vessel = assetId is "dl_jigger" or "dl_shaker" or "dl_mixing_glass" or "dl_glass_cocktail" or "dl_teapot" or "dl_teacup";
            var lr = DefaultLiquid(assetId);
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    float u = (x + 0.5f) / size.x, v = (y + 0.5f) / size.y;
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (vessel)
                    {
                        // 빈 그릇: 테두리만(안쪽은 투명 — 액체가 비친다).
                        bool inside = u > lr.xMin - 0.08f && u < lr.xMax + 0.08f && v > lr.yMin - 0.06f && v < Mathf.Min(1f, lr.yMax + 0.1f);
                        bool interior = u > lr.xMin && u < lr.xMax && v > lr.yMin && v < 1f;
                        if (inside && !interior) c = outline;
                        else if (interior && (x % 7 == 1 && y > size.y * 0.3f)) c = glass; // 반사광 줄(색 판독을 가리지 않는 정도)
                        if (assetId == "dl_teapot" && u > 0.7f && v > 0.35f && v < 0.6f && Mathf.Abs((v - 0.35f) - (u - 0.7f) * 0.8f) < 0.06f) c = outline; // 주둥이
                    }
                    else if (assetId.StartsWith("dl_bottle_"))
                    {
                        bool neck = u > 0.38f && u < 0.62f && v > 0.7f;
                        bool bodyArea = u > 0.12f && u < 0.88f && v < 0.72f;
                        if (neck || bodyArea) c = (u < 0.18f || u > 0.82f || v < 0.04f || (neck && (u < 0.43f || u > 0.57f))) ? outline : body;
                        if (bodyArea && v > 0.3f && v < 0.5f && u > 0.2f && u < 0.8f) c = new Color32(240, 232, 210, 255); // 라벨
                    }
                    else if (assetId == "dl_barspoon")
                    {
                        if (Mathf.Abs(u - 0.5f) < 0.12f || (v < 0.12f && Mathf.Abs(u - 0.5f) < 0.35f)) c = new Color32(190, 196, 206, 255);
                    }
                    else if (assetId == "dl_chaha")
                    {
                        if (v < 0.55f && Mathf.Abs(u - 0.5f) < 0.48f - (0.55f - v) * 0.3f) c = (v < 0.12f || v > 0.45f) ? outline : new Color32(150, 112, 70, 255);
                    }
                    else if (assetId == "dl_water_kettle")
                    {
                        if (u > 0.12f && u < 0.78f && v < 0.75f) c = (u < 0.18f || u > 0.72f || v < 0.05f || v > 0.69f) ? outline : new Color32(170, 176, 186, 255);
                        if (u >= 0.78f && v > 0.45f && v < 0.62f) c = outline;
                    }
                    else
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                        if (d < 0.45f) c = d > 0.38f ? outline : body;
                    }
                    px[y * size.x + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            p = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size.x, size.y), new Vector2(0.5f, 0f), 32f);
            p.name = "placeholder_" + assetId;
            placeholders[assetId] = p;
            return p;
        }

        static Color32 BodyColor(string assetId)
        {
            foreach (var def in DrinkIngredients.All)
                if (assetId == def.bottleId || assetId == def.iconId)
                {
                    var c = def.form == IngredientForm.Leaf ? def.color : DrinkCalculator.IngredientLook(def);
                    c.a = 1f;
                    return c;
                }
            return new Color32(150, 150, 160, 255);
        }

        /// <summary>보고서용: 연결된 아트 ID와 임시 그림 ID.</summary>
        public static readonly string[] ContractIds =
        {
            "dl_jigger", "dl_shaker", "dl_shaker_shake", "dl_mixing_glass", "dl_barspoon", "dl_glass_cocktail", "dl_chaha", "dl_teapot",
            "dl_teacup", "dl_water_kettle", "dl_bottle_gin", "dl_bottle_rum", "dl_bottle_blue_curacao", "dl_bottle_grenadine",
            "dl_bottle_lemon_juice", "dl_bottle_honey", "dl_tin_black_tea", "dl_tin_green_tea", "dl_leaf_black_tea", "dl_leaf_green_tea",
            "dl_icon_gin", "dl_icon_rum", "dl_icon_blue_curacao", "dl_icon_grenadine", "dl_icon_black_tea", "dl_icon_green_tea",
            "dl_icon_lemon_juice", "dl_icon_honey", "dl_ice_cube", "dl_steam", "dl_drink_reveal", "dl_buff_gain", "dl_pour_stream", "dl_bubbles", "dl_leaf_heap_black_tea", "dl_leaf_heap_green_tea", "dl_shaker_open",
        };
    }

    [Serializable]
    public class DrinkAnchorTable { public List<DrinkAnchorEntry> entries = new List<DrinkAnchorEntry>(); }

    /// <summary>Resources/DrinkLab/anchors.json 한 줄(임포트 도구가 Codex 매니페스트 픽셀 좌표를 0~1·왼쪽 아래 원점으로 변환).</summary>
    [Serializable]
    public class DrinkAnchorEntry
    {
        public string assetId;
        public float[] pourLip;
        public float[] mouth;
        public float[] grip;
        /// <summary>xMin, yMin, xMax, yMax.</summary>
        public float[] liquid;
        public int width, height;
    }
}
