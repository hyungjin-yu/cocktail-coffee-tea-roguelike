using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69/71 그릇 + 동적 액체(UI). 빈 그릇 아트 위에, 아트의 _mask.png(그릇 안쪽 단면) 스텐실로 자른 액체를 그린다.
    /// 높이는 DrinkFluidPhysics로 계산한다 — 마스크 행 폭에서 만든 누적 용량 C(h)(회전 대칭 근사), 직립이면 SolveIce(얼음 배제 부피·
    /// 국소 층 밀도에 따른 부력·바닥 접촉), 그릇이 화면에서 기울면 SolveTilt(중력에 수직인 수평 액면, 부피 보존).
    /// 층은 화면 기준 수평 띠로 그리고 회전한 스텐실이 그릇 모양으로 자른다(회전한 액체 판 없음).
    /// 탁도·기포·물결은 표시 효과일 뿐 색/버프/양 계산과 무관하다. 얼음은 실제 데이터 부피에 맞는 크기로 그린다.
    /// </summary>
    public class DrinkVesselView : MonoBehaviour
    {
        public const int PixelScale = 4;
        /// <summary>얼음 한 조각 부피(mL, 약 2.15cm 정육면체). 질량은 밀도 0.917로 고정(제조 동안 녹지 않는 근사).</summary>
        public const double CubeVolumeMl = 10.0;

        public string AssetId { get; private set; }
        public bool Placeholder { get; private set; }
        public float CapacityMl { get; private set; }
        public float VolumeMl { get; private set; }
        public float Level01 => surface01;
        public RectTransform Rect => (RectTransform)transform;
        public Image Glass { get; private set; }
        /// <summary>마지막 직립 해(얼음·여유 공간). 기울었거나 비었으면 직전 값.</summary>
        public FluidIceSolution IceSolution { get; private set; }
        public float HeadspaceMl => IceSolution != null ? (float)IceSolution.HeadspaceMl : CapacityMl - VolumeMl;
        public float OverflowMl => IceSolution != null ? (float)IceSolution.OverflowMl : 0f;
        public float TiltDegrees { get; private set; }
        public FluidTiltSolution TiltSolution { get; private set; }
        public int IceCount => iceCount;
        /// <summary>그릇 누적 용량 C(h)(캐시). 붓기 시작 각도·입구 유출량 계산에 쓴다.</summary>
        public FluidVesselProfile Profile => profile;
        public IReadOnlyList<Image> IceImages => ice;

        RectTransform liquidArea, worldFill, surface, turbid, bubbleRoot;
        Image surfaceImg, turbidImg;
        readonly List<Image> layerImages = new List<Image>();
        readonly List<RectTransform> bubbles = new List<RectTransform>();
        readonly List<Image> ice = new List<Image>();
        readonly List<DrinkLayer> layers = new List<DrinkLayer>();
        float bubbleStrength, ripple;
        int iceCount;
        float surface01;
        float lastSolvedAngle = float.NaN;
        bool hidden;
        Rect liquidRect;
        FluidVesselProfile profile;
        static Sprite white;

        public static Sprite White
        {
            get
            {
                if (white != null) return white;
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                t.SetPixels32(px);
                t.Apply();
                white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                return white;
            }
        }

        static Sprite noise;
        static Sprite Noise
        {
            get
            {
                if (noise != null) return noise;
                var t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[256];
                uint s = 12345;
                for (int i = 0; i < 256; i++)
                {
                    s = s * 1664525u + 1013904223u;
                    byte a = (byte)(90 + (s >> 24) % 120);
                    px[i] = new Color32(255, 255, 250, a);
                }
                t.SetPixels32(px);
                t.Apply();
                noise = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
                return noise;
            }
        }

        public static DrinkVesselView Create(Transform parent, string assetId, Vector2 anchoredPos, float capacityMl, int scale = PixelScale)
        {
            var sprite = DrinkArt.SpriteOrPlaceholder(assetId, out bool placeholder);
            var size = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
            var rt = UiFactory.Place(assetId, parent, new Vector2(0.5f, 0.5f), anchoredPos, size);
            rt.pivot = new Vector2(0.5f, 0f);
            var v = rt.gameObject.AddComponent<DrinkVesselView>();
            v.AssetId = assetId;
            v.Placeholder = placeholder;
            v.CapacityMl = capacityMl;
            v.Build(sprite);
            return v;
        }

        void Build(Sprite sprite)
        {
            liquidRect = DrinkArt.LiquidRect(AssetId);
            profile = DrinkArt.Profile(AssetId, CapacityMl);
            var mask = DrinkArt.Mask(AssetId);
            liquidArea = UiFactory.Stretch("Liquid", transform);
            if (mask != null)
            {
                var mimg = liquidArea.gameObject.AddComponent<Image>();
                mimg.sprite = mask;
                mimg.raycastTarget = false;
                liquidArea.gameObject.AddComponent<Mask>().showMaskGraphic = false; // 스텐실: 그릇과 함께 회전
            }
            else
            {
                // 임시 그림(마스크 없음): 사각 내부. 회전하면 사각 클립이 회전하지 않으므로 기울기 표시는 근사.
                liquidArea.anchorMin = liquidRect.min;
                liquidArea.anchorMax = liquidRect.max;
                liquidArea.gameObject.AddComponent<RectMask2D>();
            }
            // 화면 기준(중력 방향) 좌표계: 원점 = 그릇 안쪽 바닥의 축, 회전은 매번 화면에 맞춘다.
            worldFill = new GameObject("WorldFill", typeof(RectTransform)).GetComponent<RectTransform>();
            worldFill.SetParent(liquidArea, false);
            worldFill.anchorMin = worldFill.anchorMax = new Vector2(0.5f, 0.5f);
            worldFill.pivot = new Vector2(0.5f, 0f);
            worldFill.sizeDelta = Vector2.zero;

            turbid = Strip("Turbidity");
            turbidImg = turbid.GetComponent<Image>();
            turbidImg.sprite = Noise;
            turbidImg.type = Image.Type.Tiled;
            turbidImg.color = new Color(1f, 1f, 1f, 0f);
            bubbleRoot = new GameObject("Bubbles", typeof(RectTransform)).GetComponent<RectTransform>();
            bubbleRoot.SetParent(worldFill, false);
            bubbleRoot.anchorMin = bubbleRoot.anchorMax = bubbleRoot.pivot = new Vector2(0.5f, 0f);
            surface = Strip("Surface");
            surfaceImg = surface.GetComponent<Image>();
            surfaceImg.color = Color.clear;

            var glassRt = UiFactory.Stretch("Glass", transform);
            Glass = glassRt.gameObject.AddComponent<Image>();
            Glass.sprite = sprite;
            Glass.raycastTarget = false;
        }

        RectTransform Strip(string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(worldFill, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>그릇 안쪽 높이(px, 그릇 로컬). 물리의 정규화 단위 1 = 이 길이.</summary>
        float InteriorPx => Rect.rect.height * (liquidRect.yMax - liquidRect.yMin);
        Vector2 InteriorOriginLocal => new Vector2(Rect.rect.xMin + Rect.rect.width * liquidRect.center.x, Rect.rect.yMin + Rect.rect.height * liquidRect.yMin);

        /// <summary>한 덩어리 액체.</summary>
        public void SetLiquid(Color color, float volumeMl, float turbidity, float density = 1f) =>
            SetLayers(new List<DrinkLayer> { new DrinkLayer { color = color, volume = volumeMl, turbidity = turbidity, density = density } });

        /// <summary>아래부터 층(부피·밀도·색). 높이는 C(h)와 얼음 해로 정한다.</summary>
        public void SetLayers(IList<DrinkLayer> newLayers)
        {
            if (SameLayers(newLayers)) return; // 매 프레임 같은 내용이면 얼음/기울기 해를 다시 풀지 않는다(프레임 끊김 방지)
            layers.Clear();
            float total = 0f;
            foreach (var l in newLayers) if (l.volume > 1e-4f) { layers.Add(l); total += l.volume; }
            VolumeMl = total;
            Solve(force: true);
        }

        bool SameLayers(IList<DrinkLayer> other)
        {
            int n = 0;
            foreach (var l in other) if (l.volume > 1e-4f) n++;
            if (n != layers.Count || profile == null) return false;
            int i = 0;
            foreach (var l in other)
            {
                if (l.volume <= 1e-4f) continue;
                var o = layers[i++];
                if (Mathf.Abs(o.volume - l.volume) > 1e-4f || o.color != l.color || !Mathf.Approximately(o.turbidity, l.turbidity) || !Mathf.Approximately(o.density, l.density)) return false;
            }
            return true;
        }

        public void SetEmpty() => SetLayers(new List<DrinkLayer>());

        /// <summary>얼음 개수(서빙·빌드·레이어 잔). 액체량은 그대로, 잠긴 부피만큼 수면이 오른다.</summary>
        public void SetIce(int count)
        {
            count = Mathf.Clamp(count, 0, 16);
            if (count == iceCount) return;
            iceCount = count;
            Solve(force: true);
        }

        /// <summary>화면에서의 기울기(도). θ&gt;0 = 그릇 윗부분이 오른쪽(+x)으로 기욺(시계 방향 회전).</summary>
        float ScreenTilt()
        {
            float z = Rect.eulerAngles.z;
            if (z > 180f) z -= 360f;
            return -z;
        }

        void LateUpdate()
        {
            float a = ScreenTilt();
            if (float.IsNaN(lastSolvedAngle) || Mathf.Abs(a - lastSolvedAngle) > 0.25f) Solve(force: false);
            AlignWorldFill();
            AnimateEffects();
        }

        void AlignWorldFill()
        {
            if (worldFill == null) return;
            worldFill.position = Rect.TransformPoint(InteriorOriginLocal);
            var parentCanvas = Rect.root;
            worldFill.rotation = parentCanvas != null ? parentCanvas.rotation : Quaternion.identity;
        }

        void Solve(bool force)
        {
            if (profile == null) return;
            float angle = ScreenTilt();
            TiltDegrees = angle;
            lastSolvedAngle = angle;
            AlignWorldFill();
            float H = InteriorPx;
            var levels = new List<float>();
            float baseLevel = 0f;
            bool upright = Mathf.Abs(angle) < 1f;
            if (layers.Count == 0)
            {
                surface01 = 0f;
                // 빈 잔의 얼음은 바닥/다른 얼음 위에 놓인다(허공에 띄우지 않고 배제 부피로 수면을 만들지 않는다).
                IceSolution = upright && iceCount > 0 ? DrinkFluidPhysics.SolveIce(profile, new FluidLayer[0], iceCount, CubeVolumeMl) : null;
            }
            else if (upright)
            {
                var fl = new FluidLayer[layers.Count];
                for (int i = 0; i < layers.Count; i++)
                    fl[i] = FluidLayer.From(new[] { new FluidPart("L" + i, layers[i].volume, Mathf.Max(0.5f, layers[i].density <= 0f ? 1f : layers[i].density)) });
                IceSolution = DrinkFluidPhysics.SolveIce(profile, fl, iceCount, CubeVolumeMl);
                var sol = IceSolution;
                for (int i = 0; i < layers.Count; i++) levels.Add(i < sol.Layers.Length ? (float)sol.Layers[i].top01 : (float)sol.Surface01);
                surface01 = sol.HasLiquid ? (float)sol.Surface01 : 0f;
                baseLevel = -0.05f;
                TiltSolution = null;
            }
            else
            {
                var cumul = new double[layers.Count];
                double acc = 0;
                for (int i = 0; i < layers.Count; i++) { acc += layers[i].volume; cumul[i] = acc; }
                TiltSolution = DrinkFluidPhysics.SolveTilt(profile, cumul, Mathf.Clamp(angle, -179f, 179f));
                for (int i = 0; i < layers.Count; i++) levels.Add((float)TiltSolution.Levels[i]);
                baseLevel = -2f; // 기울면 가장 낮은 지점이 원점 아래일 수 있다 — 마스크가 그릇 모양으로 자른다
                surface01 = levels.Count > 0 ? levels[levels.Count - 1] : 0f;
            }
            Draw(levels, baseLevel, H, upright);
        }

        void Draw(List<float> levels, float baseLevel, float H, bool upright)
        {
            float W = Rect.rect.width * 3f;
            while (layerImages.Count < layers.Count)
            {
                var rt = Strip("Layer" + layerImages.Count);
                rt.SetSiblingIndex(layerImages.Count);
                layerImages.Add(rt.GetComponent<Image>());
            }
            float prev = baseLevel, turbSum = 0f;
            for (int i = 0; i < layerImages.Count; i++)
            {
                bool on = i < layers.Count && !hidden;
                layerImages[i].gameObject.SetActive(on);
                if (!on) continue;
                float top = Mathf.Max(prev, levels[i]);
                var rt = layerImages[i].rectTransform;
                rt.anchoredPosition = new Vector2(0f, prev * H);
                rt.sizeDelta = new Vector2(W, Mathf.Max(0f, (top - prev) * H));
                layerImages[i].color = layers[i].color;
                turbSum += layers[i].turbidity * layers[i].volume;
                prev = top;
            }
            float turb = VolumeMl > 0f ? turbSum / VolumeMl : 0f;
            turbid.gameObject.SetActive(!hidden && layers.Count > 0);
            turbid.anchoredPosition = new Vector2(0f, baseLevel * H);
            turbid.sizeDelta = new Vector2(W, Mathf.Max(0f, (surface01 - baseLevel) * H));
            turbidImg.color = new Color(1f, 1f, 0.96f, Mathf.Clamp01(Mathf.Max(turb * 0.45f, bubbleStrength * 0.35f)));
            turbid.SetAsLastSibling();
            bubbleRoot.SetAsLastSibling();
            // 수면: 중력에 수직인 얇은 밝은 선(메니스커스 근사)
            var topColor = layers.Count > 0 ? layers[layers.Count - 1].color : Color.clear;
            surface.gameObject.SetActive(!hidden && layers.Count > 0);
            surface.anchoredPosition = new Vector2(0f, surface01 * H - PixelScale * 0.5f);
            surface.sizeDelta = new Vector2(W, PixelScale);
            surfaceImg.color = new Color(Mathf.Min(1f, topColor.r + 0.25f), Mathf.Min(1f, topColor.g + 0.25f), Mathf.Min(1f, topColor.b + 0.25f), Mathf.Max(0.5f, topColor.a));
            surface.SetAsLastSibling();
            DrawIce(H, upright);
        }

        void DrawIce(float H, bool upright)
        {
            var sprite = DrinkArt.Sprite("dl_ice_cube");
            var cubes = IceSolution != null && upright ? IceSolution.Cubes : new FluidIceCube[0];
            int n = upright ? Mathf.Min(iceCount, cubes.Length) : 0;
            while (ice.Count < n)
            {
                var rt = new GameObject("Ice" + ice.Count, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(worldFill, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite != null ? sprite : White;
                img.raycastTarget = false;
                img.color = sprite != null ? new Color(1f, 1f, 1f, 0.8f) : new Color(0.9f, 0.96f, 1f, 0.35f);
                ice.Add(img);
            }
            for (int i = 0; i < ice.Count; i++)
            {
                bool on = i < n && !hidden;
                ice[i].gameObject.SetActive(on);
                if (!on) continue;
                var c = cubes[i];
                var rt = ice[i].rectTransform;
                float side = (float)c.Side01 * H;
                rt.anchoredPosition = new Vector2((float)c.CentreX * H, (float)c.Bottom01 * H);
                rt.sizeDelta = new Vector2(side, side);
                rt.SetAsLastSibling();
            }
            surface.SetAsLastSibling();
        }

        public void SetEffects(float bubbles01, float swirl01, float ripple01)
        {
            bubbleStrength = Mathf.Clamp01(bubbles01);
            // 스터링은 큰 회전 판 대신 작은 물결만(바스푼은 실제 스프라이트로 따로).
            ripple = Mathf.Clamp01(Mathf.Max(ripple01, swirl01 * 0.6f));
        }

        void AnimateEffects()
        {
            if (surface == null) return;
            float t = Time.unscaledTime;
            float H = InteriorPx;
            surface.anchoredPosition = new Vector2(Mathf.Sin(t * 7f) * ripple * 3f, surface01 * H - PixelScale * 0.5f + Mathf.Sin(t * 9f) * ripple * 2f);
            int want = layers.Count > 0 && !hidden ? Mathf.RoundToInt(bubbleStrength * 14f) : 0;
            while (bubbles.Count < want)
            {
                var rt = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(bubbleRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(PixelScale * 2, PixelScale * 2);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = White;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0.7f);
                bubbles.Add(rt);
            }
            for (int i = 0; i < bubbles.Count; i++)
            {
                bool on = i < want;
                bubbles[i].gameObject.SetActive(on);
                if (!on) continue;
                float phase = (t * (0.6f + (i % 5) * 0.13f) + i * 0.37f) % 1f;
                float y = phase * surface01;
                float half = profile != null ? (float)profile.HalfWidthAt(Mathf.Clamp01(y)) : 0.3f;
                bubbles[i].anchoredPosition = new Vector2((((i * 53) % 97) / 97f * 2f - 1f) * half * 0.8f * H, y * H);
            }
        }

        /// <summary>불투명 그릇(닫힌 셰이커·금속 지거가 기울 때 등)에서 내부를 그리지 않는다.</summary>
        public void SetLiquidHidden(bool value)
        {
            if (hidden == value) return;
            hidden = value;
            if (liquidArea != null) liquidArea.gameObject.SetActive(!value);
            Solve(force: true); // 숨긴 동안 그린 층(비활성)을 다시 그린다
        }

        /// <summary>맨 위 층 색(검증·캡처용). 비었으면 투명.</summary>
        public Color TopColor => layers.Count > 0 ? layers[layers.Count - 1].color : Color.clear;
        /// <summary>맨 아래 층 색·층 수(색 연속성 검수용).</summary>
        public Color BottomColor => layers.Count > 0 ? layers[0].color : Color.clear;
        public int LayerCount => layers.Count;

        public Vector2 PointInParent(Vector2 normalized) => ToParentAnchored(Rect, normalized);

        /// <summary>rt 내부 정규화 좌표(왼쪽 아래 0,0) → 회전·크기·피벗을 모두 적용한 실제 점을, 부모 가운데 기준(anchor 0.5,0.5 자식의
        /// anchoredPosition과 같은 좌표계)으로. InverseTransformPoint 결과는 부모 피벗 기준이라 부모 rect 중심을 빼서 맞춘다.</summary>
        public static Vector2 ToParentAnchored(RectTransform rt, Vector2 normalized)
        {
            var parent = rt.parent as RectTransform;
            var r = rt.rect;
            var local = new Vector3(r.xMin + r.width * normalized.x, r.yMin + r.height * normalized.y, 0f);
            var world = rt.TransformPoint(local);
            if (parent == null) return world;
            return (Vector2)parent.InverseTransformPoint(world) - parent.rect.center;
        }

        /// <summary>화면 위치를 유지한 채 피벗(회전축)을 옮긴다 — 도구는 손잡이(grip) 앵커를 축으로 기울인다.</summary>
        public static void SetPivotKeepPosition(RectTransform rt, Vector2 pivot)
        {
            var size = rt.rect.size;
            var delta = pivot - rt.pivot;
            rt.pivot = pivot;
            rt.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
        }

        /// <summary>현재(직립) 수면 높이의 그릇 기준 정규화 y.</summary>
        public float SurfaceY => Mathf.Lerp(liquidRect.yMin, liquidRect.yMax, Mathf.Clamp01(surface01));
    }
}
