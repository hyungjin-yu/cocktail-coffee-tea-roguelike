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
            placeholders.Clear();
            byIcon = null;
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

        /// <summary>실제 아트 또는 임시 실루엣. 재료 아이콘(iconId)에 아트가 없으면 같은 재료의 실제 포장 아트(bottleId: 병/틴)를 쓰고,
        /// 그것도 없으면 원형 대신 재료 형태에 맞는 포장 실루엣(액체 = 라벨 병, 찻잎·원두 = 틴)을 재료색으로 그린다(placeholder=true).</summary>
        public static Sprite SpriteOrPlaceholder(string assetId, out bool placeholder)
        {
            var s = Sprite(assetId);
            placeholder = s == null;
            if (s != null) return s;
            var def = IngredientByIcon(assetId);
            if (def != null && !string.IsNullOrEmpty(def.bottleId))
            {
                s = Sprite(def.bottleId);
                placeholder = s == null;
                return s ?? Placeholder(PackagingId(def));
            }
            return Placeholder(assetId);
        }

        /// <summary>재료 포장 ID(병/틴). bottleId가 형태 규칙(dl_bottle_ / dl_tin_)을 따르지 않으면 형태로 정한다.</summary>
        public static string PackagingId(DrinkIngredientDef def)
        {
            if (def == null) return null;
            string prefix = def.form == IngredientForm.Leaf ? "dl_tin_" : "dl_bottle_";
            return def.bottleId != null && def.bottleId.StartsWith(prefix) ? def.bottleId : prefix + def.id;
        }

        static Dictionary<string, DrinkIngredientDef> byIcon;

        static DrinkIngredientDef IngredientByIcon(string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || !assetId.StartsWith("dl_icon_")) return null;
            if (byIcon == null)
            {
                byIcon = new Dictionary<string, DrinkIngredientDef>();
                foreach (var def in DrinkIngredients.All) if (!string.IsNullOrEmpty(def.iconId)) byIcon[def.iconId] = def;
            }
            return byIcon.TryGetValue(assetId, out var d) ? d : null;
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

        // ───── 마스크 기하·프로브(DrinkVesselGeometry로 위임, 기존 호출자 유지) ─────

        /// <summary>그릇 누적 용량 C(h). <see cref="DrinkVesselGeometry.Profile"/>.</summary>
        public static FluidVesselProfile Profile(string assetId, float capacityMl) => DrinkVesselGeometry.Profile(assetId, capacityMl);

        /// <summary>그릇 안쪽(마스크 불투명) 여부. <see cref="DrinkVesselGeometry.InsideMask"/>.</summary>
        public static bool InsideMask(string assetId, Vector2 n) => DrinkVesselGeometry.InsideMask(assetId, n);

        /// <summary>그 높이에서 그릇 안쪽 마스크의 가로 구간. <see cref="DrinkVesselGeometry.MaskSpan"/>.</summary>
        public static void MaskSpan(string assetId, float y01, out float xMin, out float xMax) => DrinkVesselGeometry.MaskSpan(assetId, y01, out xMin, out xMax);

        /// <summary>스프라이트 아래쪽 완전 투명 행 수. <see cref="DrinkVesselGeometry.AlphaBottom"/>.</summary>
        public static int AlphaBottom(string assetId) => DrinkVesselGeometry.AlphaBottom(assetId);

        /// <summary>바스푼 프레임의 행별 불투명 x 구간. <see cref="DrinkVesselGeometry.SpoonExtent"/>.</summary>
        public static bool SpoonExtent(int frame, out int[] rows, out int width, out int height) => DrinkVesselGeometry.SpoonExtent(frame, out rows, out width, out height);

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
                    else if (assetId.StartsWith("dl_tin_"))
                    {
                        // 찻잎·원두 틴: 원통 몸체 + 위 뚜껑 띠 + 가운데 라벨(원형 대체 아이콘이 아니라 포장 실루엣).
                        bool lid = u > 0.08f && u < 0.92f && v >= 0.80f && v < 0.94f;
                        bool bodyArea = u > 0.12f && u < 0.88f && v < 0.80f;
                        if (lid) c = (v > 0.90f || u < 0.12f || u > 0.88f) ? outline : new Color32(96, 88, 92, 255);
                        else if (bodyArea) c = (u < 0.18f || u > 0.82f || v < 0.05f) ? outline : body;
                        if (bodyArea && v > 0.30f && v < 0.56f && u > 0.22f && u < 0.78f) c = new Color32(240, 232, 210, 255); // 라벨
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

        /// <summary>재료 확장 10종: 재료 하나에 대해 화면 코드가 실제로 찾는 아트 ID(아트 작업 인계용). 없는 그림은 임시 그림(placeholder)으로 그린다.
        /// 액체 = 아이콘·병(dl_bottle)·원천 병(dl_srcbottle, 있으면 물리 병), 찻잎 = 아이콘·통(dl_tin)·낙하(dl_leaf)·더미(dl_leaf_heap),
        /// 원두(기본·산지) = 아이콘·통 + 공용 원두 낙하·스쿱 더미(dl_leaf_coffee_bean).</summary>
        public static string[] ExpectedIngredientArt(DrinkIngredientDef def)
        {
            if (def == null) return new string[0];
            if (def.IsCoffeeBean) return new[] { def.iconId, def.bottleId, "dl_leaf_" + def.LeafArtKey };
            if (def.form == IngredientForm.Leaf) return new[] { def.iconId, def.bottleId, "dl_leaf_" + def.LeafArtKey, "dl_leaf_heap_" + def.LeafArtKey };
            return new[] { def.iconId, def.bottleId, "dl_srcbottle_" + def.id };
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
