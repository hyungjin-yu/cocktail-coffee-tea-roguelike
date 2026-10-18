using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69 제조대(가운데 패널). 병/찻잎 통을 누르고 끌면 기울기가 바뀌고 기울기에 따라 붓는 속도가 연속으로 변한다(조금 끌면 아주 천천히).
    /// 붓는 양은 정수 0.1 단위로만 늘고(표시 = 실제), 지거(45mL)·다하(3g) 용량이 차면 자동으로 그릇에 옮겨 나눠 붓고 누적량을 보인다.
    /// 포인터를 놓거나 제조대를 벗어나거나 창이 포커스를 잃으면 즉시 멈추고 지거 내용만 배합에 넣는다. 재고는 제조 확정 전까지 차감하지 않는다.
    /// 제조 확정 후에는 기법별 모션(셰이킹/스터링/빌드/레이어링/차 우림)과 음용 모션을 재생한다(건너뛰기 가능, 결과 동일).
    /// </summary>
    public class DrinkStageView : MonoBehaviour
    {
        /// <summary>병목이 수평을 지나야(85°~) 액체가 입구로 나온다. 135°까지 기울이면 최대 속도.</summary>
        public const float TiltStart = 85f, TiltMax = 135f;
        public const float LiquidMaxRate = 250f; // 0.1mL/초 → 25mL/s
        public const float LeafMaxRate = 25f;    // 0.1g/초 → 2.5g/s

        RectTransform root;
        Text readout, toolLabel, finishLabel;
        Image bottle, stream, leafHeap, chaha, kettle, steam, spoon, chahaHeap;
        RectTransform bottleRt, leafRoot;
        DrinkVesselView jigger, vessel, serve;
        DrinkRecipe draft;
        Func<DrinkIngredientDef, int> allowed;
        Action changed;
        DrinkIngredientDef current;
        string vesselId;

        // 붓기 상태
        public bool Pouring { get; private set; }
        public float Tilt { get; private set; }
        public int InMeasure { get; private set; }
        public int MeasureDumps { get; private set; }
        public int PouredThisGesture { get; private set; }
        public string StopReason { get; private set; }
        float accumulator;
        Vector2 pressPos;
        Coroutine sequence;
        bool skipRequested;
        public bool SequencePlaying => sequence != null;
        public bool Placeholders { get; private set; }

        public RectTransform BottleRect => bottleRt;
        public DrinkVesselView MainVessel => vessel;
        public DrinkVesselView ServeVessel => serve;
        public DrinkVesselView Jigger => jigger;
        public string ReadoutText => readout != null ? readout.text : "";
        /// <summary>지금 고른 재료(계량 중인 재료) id.</summary>
        public string CurrentId => current?.id;
        /// <summary>계량 도구 안 양이 바뀔 때(오른쪽 미리보기 실시간 갱신용).</summary>
        public event Action MeasureChanged;
        public Color StreamColor => stream != null ? stream.color : Color.clear;
        public bool StreamVisible => stream != null && stream.gameObject.activeSelf;
        public Color DumpStreamColor => dumpStream != null ? dumpStream.color : Color.clear;
        public bool DumpStreamVisible => dumpStream != null && dumpStream.gameObject.activeSelf;
        Image dumpStream;
        public float LeafHeapScale => leafHeap != null && leafHeap.gameObject.activeSelf ? leafHeap.rectTransform.localScale.y : 0f;

        public static DrinkStageView Create(RectTransform parent, DrinkRecipe draft, Func<DrinkIngredientDef, int> allowed, Action changed)
        {
            var view = parent.gameObject.AddComponent<DrinkStageView>();
            view.root = parent;
            view.draft = draft;
            view.allowed = allowed;
            view.changed = changed;
            view.Build();
            return view;
        }

        void Build()
        {
            UiFactory.AddImage(root, new Color(0.11f, 0.08f, 0.07f, 0.95f));
            var counter = UiFactory.Place("Counter", root, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(root.rect.width > 0 ? root.rect.width : 780f, 120f));
            counter.anchorMin = new Vector2(0f, 0f); counter.anchorMax = new Vector2(1f, 0f); counter.sizeDelta = new Vector2(0f, 120f);
            UiFactory.AddImage(counter, new Color(0.32f, 0.2f, 0.12f, 1f)).raycastTarget = false;

            toolLabel = UiFactory.AddText(UiFactory.Place("ToolLabel", root, new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(760f, 30f)), "", 20, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f));
            readout = UiFactory.AddText(UiFactory.Place("Readout", root, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(760f, 64f)), "", 22, TextAnchor.MiddleCenter, Color.white);
            finishLabel = UiFactory.AddText(UiFactory.Place("Finish", root, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(760f, 44f)), "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.5f));

            leafRoot = UiFactory.Stretch("Leaves", root);
            stream = AddImage("Stream", root, DrinkArt.Sprite("dl_pour_stream") ?? DrinkVesselView.White, new Vector2(16f, 100f));
            stream.rectTransform.pivot = new Vector2(0.5f, 1f);
            stream.gameObject.SetActive(false);
            // 지거/다하 → 그릇 옮겨 붓기 줄기(재료 고유 색). 병 줄기와 따로 둔다.
            dumpStream = AddImage("DumpStream", root, DrinkArt.Sprite("dl_pour_stream") ?? DrinkVesselView.White, new Vector2(12f, 100f));
            dumpStream.rectTransform.pivot = new Vector2(0.5f, 1f);
            dumpStream.gameObject.SetActive(false);

            kettle = AddImage("Kettle", root, DrinkArt.SpriteOrPlaceholder("dl_water_kettle", out bool kp), Vector2.zero);
            NativeSize(kettle, 3);
            kettle.rectTransform.anchoredPosition = new Vector2(-230f, 120f); // 주둥이(오른쪽)가 다관 쪽을 향하도록 왼쪽 위
            DrinkVesselView.SetPivotKeepPosition(kettle.rectTransform, DrinkArt.Anchor("dl_water_kettle", "grip"));
            kettleRest = kettle.rectTransform.anchoredPosition;
            kettle.gameObject.SetActive(false);
            Placeholders |= kp;

            steam = AddImage("Steam", root, DrinkArt.Sprite("dl_steam") ?? DrinkVesselView.White, new Vector2(64f, 96f));
            steam.gameObject.SetActive(false);

            bottle = AddImage("Bottle", root, DrinkVesselView.White, new Vector2(96f, 224f));
            bottleRt = bottle.rectTransform;
            bottle.raycastTarget = true;
            var handle = bottle.gameObject.AddComponent<DrinkPourHandle>();
            handle.Stage = this;
            // 터치에서도 잡기 쉽게 병 주위에 투명 잡기 영역.
            var hit = UiFactory.Stretch("HitArea", bottleRt);
            hit.offsetMin = new Vector2(-40f, -30f); hit.offsetMax = new Vector2(40f, 30f);
            var hitImg = hit.gameObject.AddComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0f);
            hit.gameObject.AddComponent<DrinkPourHandle>().Stage = this;

            chaha = AddImage("Chaha", root, DrinkArt.SpriteOrPlaceholder("dl_chaha", out bool cp), Vector2.zero);
            NativeSize(chaha, 4);
            chaha.rectTransform.anchoredPosition = new Vector2(-30f, 10f);
            DrinkVesselView.SetPivotKeepPosition(chaha.rectTransform, DrinkArt.Anchor("dl_chaha", "grip"));
            chahaRest = chaha.rectTransform.anchoredPosition;
            chahaHeap = AddImage("ChahaHeap", chaha.rectTransform, DrinkVesselView.White, new Vector2(120f, 30f));
            chahaHeap.rectTransform.anchorMin = chahaHeap.rectTransform.anchorMax = new Vector2(0.5f, 0.45f);
            chahaHeap.rectTransform.pivot = new Vector2(0.5f, 0f);
            Placeholders |= cp;

            jigger = DrinkVesselView.Create(root, "dl_jigger", new Vector2(-30f, -20f), DrinkRules.JiggerCapacity / 10f);
            DrinkVesselView.SetPivotKeepPosition(jigger.Rect, DrinkArt.Anchor("dl_jigger", "grip"));
            jiggerRest = jigger.Rect.anchoredPosition;
            Placeholders |= jigger.Placeholder;

            leafHeap = AddImage("LeafHeap", root, DrinkVesselView.White, new Vector2(128f, 64f));
            leafHeap.rectTransform.pivot = new Vector2(0.5f, 0f);
            leafHeap.gameObject.SetActive(false);

            spoon = AddImage("Barspoon", root, DrinkArt.SpriteOrPlaceholder("dl_barspoon", out bool sp), Vector2.zero);
            NativeSize(spoon, 4);
            spoon.gameObject.SetActive(false);
            Placeholders |= sp;
        }

        static Image AddImage(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static void NativeSize(Image img, int scale)
        {
            if (img.sprite == null) return;
            img.rectTransform.sizeDelta = new Vector2(img.sprite.rect.width * scale, img.sprite.rect.height * scale);
        }

        public static string VesselFor(DrinkRecipe r)
        {
            switch (r.Technique)
            {
                case DrinkTechnique.Shake: return DrinkArt.Has("dl_shaker_open") ? "dl_shaker_open" : "dl_shaker";
                case DrinkTechnique.Stir: return "dl_mixing_glass";
                case DrinkTechnique.Steep: return "dl_teapot";
                default: return "dl_glass_cocktail";
            }
        }

        static string ServeFor(DrinkRecipe r) => r.Kind == DrinkKind.Tea ? "dl_teacup" : "dl_glass_cocktail";

        static float CapacityFor(string id) => id switch
        {
            "dl_teapot" => 200f,
            "dl_teacup" => 190f,
            "dl_jigger" => DrinkRules.JiggerCapacity / 10f,
            _ => 150f,
        };

        /// <summary>배합(기법) 바뀜 → 그릇 교체·내용 다시 그림.</summary>
        public void RefreshDraft()
        {
            if (sequence != null) return;
            finishLabel.text = "";
            string want = VesselFor(draft);
            if (vessel == null || vesselId != want)
            {
                if (vessel != null) Destroy(vessel.gameObject);
                vesselId = want;
                vessel = DrinkVesselView.Create(root, want, new Vector2(150f, -230f), CapacityFor(want), want == "dl_teapot" ? 4 : 4);
                vessel.transform.SetSiblingIndex(1);
                Placeholders |= vessel.Placeholder;
            }
            vessel.transform.localRotation = Quaternion.identity;
            DrinkVesselView.SetPivotKeepPosition(vessel.Rect, new Vector2(0.5f, 0f));
            vessel.Rect.anchoredPosition = VesselRest;
            vessel.SetLiquidHidden(false);
            if (kettle != null) { kettle.rectTransform.localRotation = Quaternion.identity; kettle.rectTransform.anchoredPosition = kettleRest; }
            vessel.SetEffects(0f, 0f, Pouring ? 0.6f : 0.15f);
            if (serve != null) serve.gameObject.SetActive(false);
            kettle.gameObject.SetActive(draft.Kind == DrinkKind.Tea);
            steam.gameObject.SetActive(false);
            spoon.gameObject.SetActive(false);
            if (Transferring)
            {
                RenderVessel(TransferPreview(TransferProgress)); // 옮겨 붓는 중: 지금까지 부은 만큼만
                UpdateReadout();
                return;
            }
            if (draft.Kind == DrinkKind.Tea)
            {
                // 다관에는 찻잎 더미만(물은 우릴 때 붓는다) + 첨가 액체(레몬/꿀)는 바닥 층.
                vessel.SetLayers(PourOrderLayers(draft, liquidsOnly: true));
                ShowLeafHeap(leafHeap, vessel, draft.LeafTotal());
            }
            else
            {
                // 열린 셰이커/믹싱 글라스/잔: 지금까지 넣은 재료(현재 부분 배합)만의 혼합 색. 레이어링은 밀도 층.
                // 아직 넣지 않은 재료의 색은 반영되지 않는다(배합 = 지금까지 부은 것).
                // 빌드는 붓는 순서대로 띠가 남고 제조 모션에서 확산된다.
                if (draft.Technique == DrinkTechnique.Build) vessel.SetLayers(PourOrderLayers(draft, liquidsOnly: true));
                else
                {
                    var look = DrinkCalculator.Look(draft);
                    vessel.SetLayers(look.totalVolume > 0f ? look.layers : new List<DrinkLayer>());
                }
                vessel.SetIce(draft.Technique == DrinkTechnique.Build || draft.Technique == DrinkTechnique.Layer ? draft.ice : 0);
                leafHeap.gameObject.SetActive(false);
            }
            UpdateReadout();
        }

        /// <summary>편집 중 그릇 안 모습: 투입 순서대로 층(아직 섞지 않음). 물은 마지막.</summary>
        public static List<DrinkLayer> PourOrderLayers(DrinkRecipe r, bool liquidsOnly)
        {
            var list = new List<DrinkLayer>();
            if (r?.steps == null) return list;
            foreach (var s in r.steps)
            {
                var def = s == null ? null : DrinkIngredients.Get(s.id);
                if (def == null || s.amount <= 0 || (liquidsOnly && def.form != IngredientForm.Liquid)) continue;
                list.Add(new DrinkLayer { color = DrinkCalculator.IngredientLook(def), turbidity = def.turbidity, volume = s.amount / 10f, label = def.name });
            }
            if (r.Kind == DrinkKind.Cocktail && r.water > 0)
                list.Add(new DrinkLayer { color = new Color(0.93f, 0.97f, 1f, 0.22f), volume = r.water / 10f, label = "물" });
            return list;
        }

        void ShowLeafHeap(Image heap, DrinkVesselView inVessel, int leafTenths)
        {
            bool on = leafTenths > 0;
            heap.gameObject.SetActive(on);
            if (!on) return;
            var first = FirstLeaf(draft);
            var frames = first != null ? DrinkArt.Frames("dl_leaf_heap_" + first.id) : new Sprite[0];
            // 찻잎 양이 많을수록 더미가 커진다(5g에서 최대). 프레임이 3장이면 작은/중간/큰 더미.
            float t = Mathf.Clamp01(leafTenths / (float)DrinkRules.TeaLeafMax);
            if (frames.Length > 0) heap.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))];
            heap.color = frames.Length > 0 ? Color.white : (first != null ? first.color : Color.gray);
            heap.rectTransform.sizeDelta = frames.Length > 0 ? new Vector2(frames[0].rect.width * 4, frames[0].rect.height * 4) : new Vector2(120f, 40f);
            heap.rectTransform.localScale = new Vector3(0.55f + 0.45f * t, 0.35f + 0.65f * t, 1f);
            heap.rectTransform.anchoredPosition = inVessel.PointInParent(new Vector2(0.45f, DrinkArt.LiquidRect(inVessel.AssetId).yMin));
            heap.transform.SetAsLastSibling();
        }

        static DrinkIngredientDef FirstLeaf(DrinkRecipe r)
        {
            if (r?.steps == null) return null;
            foreach (var s in r.steps)
            {
                var d = s == null ? null : DrinkIngredients.Get(s.id);
                if (d != null && d.form == IngredientForm.Leaf) return d;
            }
            return null;
        }

        /// <summary>왼쪽 목록에서 고른 재료 → 병/통과 계량 도구 교체.</summary>
        public void Select(DrinkIngredientDef def)
        {
            StopPour();
            FinishTransferVisual();
            Tilt = 0f; targetTilt = 0f;
            current = def;
            bool leaf = def != null && def.form == IngredientForm.Leaf;
            bottle.gameObject.SetActive(def != null);
            jigger.gameObject.SetActive(def != null && !leaf);
            chaha.gameObject.SetActive(def != null && leaf);
            if (def == null) { toolLabel.text = "왼쪽에서 재료를 고르세요"; UpdateReadout(); return; }
            var sprite = DrinkArt.SpriteOrPlaceholder(def.bottleId, out bool ph);
            Placeholders |= ph;
            bottle.sprite = sprite;
            bottle.color = Color.white;
            bottleRt.sizeDelta = new Vector2(sprite.rect.width * 4, sprite.rect.height * 4);
            var grip = DrinkArt.Anchor(def.bottleId, "grip");
            bottleRt.pivot = grip;
            bottleRt.anchoredPosition = BottleRest;
            bottleRt.localRotation = Quaternion.identity;
            toolLabel.text = leaf ? $"{def.name} — 통을 누른 채 끌어 다하에 덜기 (3.0g씩 다관으로)" : $"{def.name} — 병을 누른 채 끌어 지거에 붓기 (45.0mL씩 나눠 붓기)";
            jigger.SetEmpty();
            UpdateMeasure();
            UpdateReadout();
        }

        // ───── 붓기 입력 ─────

        public void BeginPour(Vector2 screenPos)
        {
            if (current == null || sequence != null) return;
            Pouring = true;
            pressPos = screenPos;
            targetTilt = Tilt;
            accumulator = 0f;
            PouredThisGesture = 0;
            MeasureDumps = 0;
            StopReason = null;
        }

        public void DragTo(Vector2 screenPos)
        {
            if (!Pouring) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(root, screenPos, null)) { StopPour(); StopReason = "제조대 밖으로 나가 멈췄습니다."; UpdateReadout(); return; }
            var canvas = root.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            var delta = (screenPos - pressPos) / Mathf.Max(0.01f, scale);
            // 오른쪽/아래로 끌수록 기울어진다. 작은 움직임은 시작 각도 근처라 아주 천천히 흐른다.
            float d = Mathf.Max(0f, delta.x) + Mathf.Max(0f, -delta.y) * 0.8f;
            targetTilt = Mathf.Clamp(d * 0.5f, 0f, TiltMax); // 실제 병 자세는 Update에서 부드럽게 따라간다
        }

        public static readonly Vector2 BottleRest = new Vector2(-250f, 70f);
        /// <summary>병 입구가 지거 입구보다 이만큼 위(px)에 오도록 자세를 잡는다.</summary>
        public const float LipClearance = 30f;

        /// <summary>QA·시험용 붓기 기하: 회전 적용 실제 병 입구, 받는 입구, 줄기 도착점(모두 제조대 가운데 기준 px), 피벗, 회전각.</summary>
        public struct PourGeometry { public Vector2 lip, mouth, streamEnd, pivot; public float rotation; public bool flowing; }
        public PourGeometry Geometry { get; private set; }

        Vector2 MeasureMouth()
        {
            if (current != null && current.form == IngredientForm.Leaf)
                return DrinkVesselView.ToParentAnchored(chaha.rectTransform, DrinkArt.Anchor("dl_chaha", "mouth"));
            return jigger.PointInParent(DrinkArt.Anchor("dl_jigger", "mouth"));
        }

        /// <summary>기울기 → 병 자세. 손잡이(grip) 피벗으로 시계 방향 회전하고, 기울기가 흐름 시작 각도에 닿을 때까지 병을 옮겨
        /// 회전된 실제 입구가 지거 입구 바로 위에 오게 한다(입구가 받는 입구 위가 아니면 흐르지 않는다).</summary>
        float targetTilt;
        /// <summary>병이 따라가는 최대 회전 속도(도/초). 순간 이동 없이 이어진다.</summary>
        public const float BottleTurnSpeed = 220f;

        /// <summary>즉시 자세 지정(시험·QA). 사용자 드래그는 targetTilt만 바꾸고 Update가 이어서 회전한다.</summary>
        public void SetTilt(float degrees)
        {
            targetTilt = Mathf.Clamp(degrees, 0f, TiltMax);
            ApplyTilt(targetTilt);
        }

        void ApplyTilt(float degrees)
        {
            Tilt = Mathf.Clamp(degrees, 0f, TiltMax);
            if (bottleRt == null || current == null) return;
            bottleRt.localRotation = Quaternion.Euler(0f, 0f, -Tilt);
            bottleRt.anchoredPosition = BottleRest;
            var lipN = DrinkArt.Anchor(current.bottleId, "pourLip");
            var lipAtRest = DrinkVesselView.ToParentAnchored(bottleRt, lipN);
            var mouth = MeasureMouth();
            var target = mouth + new Vector2(0f, LipClearance);
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Tilt / TiltStart));
            bottleRt.anchoredPosition = BottleRest + (target - lipAtRest) * approach;
            var lip = DrinkVesselView.ToParentAnchored(bottleRt, lipN);
            Geometry = new PourGeometry { lip = lip, mouth = mouth, pivot = bottleRt.pivot, rotation = -Tilt, streamEnd = lip, flowing = false };
        }

        /// <summary>회전된 입구가 받는 입구 위(가로 오차 안·위쪽)에 있을 때만 흐른다.</summary>
        public bool LipAboveMouth
        {
            get
            {
                var g = Geometry;
                float half = current != null && current.form == IngredientForm.Leaf ? 60f : 34f;
                return g.lip.y > g.mouth.y + 4f && Mathf.Abs(g.lip.x - g.mouth.x) <= half;
            }
        }

        /// <summary>즉시 멈춤: 지거/다하에 담긴 양만 배합에 넣는다(재고 미차감).</summary>
        public void StopPour()
        {
            if (!Pouring && InMeasure == 0) return;
            Pouring = false;
            targetTilt = 0f;
            if (stream != null) stream.gameObject.SetActive(false); // 흐름은 즉시 멈춘다(병은 Update에서 부드럽게 제자리로)
            Geometry = new PourGeometry { lip = Geometry.lip, mouth = Geometry.mouth, pivot = Geometry.pivot, rotation = Geometry.rotation, streamEnd = Geometry.lip, flowing = false };
            CommitMeasure();
            if (phase == CyclePhase.None) ReturnBottleIfIdle();
            UpdateReadout();
        }

        /// <summary>현재 기울기에서 0.1 단위/초 속도. 30° 미만은 0, 그 위는 제곱 곡선(작게 끌면 매우 느림), 점성이 높으면 느리게.</summary>
        public static float Rate(float tilt, DrinkIngredientDef def)
        {
            if (def == null || tilt <= TiltStart) return 0f;
            float t = Mathf.Clamp01((tilt - TiltStart) / (TiltMax - TiltStart));
            float max = def.form == IngredientForm.Leaf ? LeafMaxRate : LiquidMaxRate;
            return t * t * max * (1f - 0.6f * def.viscosity);
        }

        int MeasureCapacity => current != null && current.form == IngredientForm.Leaf ? DrinkRules.ChahaCapacity : DrinkRules.JiggerCapacity;

        /// <summary>붓기 한 단계(테스트·Update 공용). 정수 0.1 단위만 더한다.</summary>
        public void Step(float dt)
        {
            if (!Pouring || current == null) return;
            if (phase != CyclePhase.None) { stream.gameObject.SetActive(false); return; } // 옮겨 붓는 동안 병→지거 흐름 없음
            float rate = LipAboveMouth ? Rate(Tilt, current) : 0f; // 입구가 받는 입구 위가 아니면 흐르지 않는다
            stream.gameObject.SetActive(rate > 0f && current.form == IngredientForm.Liquid);
            if (rate <= 0f) { UpdateReadout(); return; }
            accumulator += rate * dt;
            int add = Mathf.FloorToInt(accumulator);
            if (add <= 0) { UpdateStream(rate); UpdateReadout(); return; }
            accumulator -= add;
            int room = allowed(current) - InMeasure;
            if (room <= 0)
            {
                accumulator = 0f;
                StopReason = Owned(current) <= draft.AmountOf(current.id) + InMeasure ? "보유량을 모두 넣었습니다." : "이 음료의 최대 양에 도달했습니다.";
                StopPour();
                return;
            }
            add = Mathf.Min(add, room, MeasureCapacity - InMeasure);
            InMeasure += add;
            PouredThisGesture += add;
            MeasureChanged?.Invoke();
            if (InMeasure >= MeasureCapacity)
            {
                // 계량 도구가 가득: 그릇에 옮겨 붓고(누적 표시) 이어서 붓는다.
                stream.gameObject.SetActive(false);
                CommitMeasure();
                MeasureDumps++;
                UpdateReadout();
                return; // 옮겨 붓기 주기 시작 — 이 프레임에 병 줄기를 다시 그리지 않는다
            }
            UpdateMeasure();
            UpdateStream(rate);
            UpdateReadout();
        }

        Func<DrinkIngredientDef, int> ownedLookup;
        public void SetOwnedLookup(Func<DrinkIngredientDef, int> f) => ownedLookup = f;
        int Owned(DrinkIngredientDef d) => ownedLookup != null ? ownedLookup(d) : int.MaxValue;

        void CommitMeasure()
        {
            if (current == null || InMeasure <= 0) { InMeasure = 0; UpdateMeasure(); return; }
            // 계량 도구를 기울여 그릇에 붓는 표시: 아직 섞이기 전 재료 고유 색 줄기.
            if (phase != CyclePhase.None) FinishTransferVisual(); // (보통 일어나지 않음) 앞 주기는 즉시 마무리 — 데이터는 이미 반영됨
            dumpStream.color = StreamTint(current.form == IngredientForm.Leaf ? current.color : DrinkCalculator.IngredientLook(current));
            transferFromPending = draft.AmountOf(current.id);
            // 배합(데이터)은 즉시 정확히 반영 — 화면만 지거가 비워지고 그릇이 차오르는 과정을 보인다.
            draft.SetAmount(current.id, draft.AmountOf(current.id) + InMeasure);
            InMeasure = 0;
            StartCycle();
            changed?.Invoke();
        }

        void UpdateMeasure()
        {
            if (current == null) return;
            if (current.form == IngredientForm.Leaf)
            {
                float t = InMeasure / (float)DrinkRules.ChahaCapacity;
                chahaHeap.gameObject.SetActive(InMeasure > 0);
                var frames = DrinkArt.Frames("dl_leaf_heap_" + current.id);
                if (frames.Length > 0) { chahaHeap.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))]; chahaHeap.color = Color.white; }
                else chahaHeap.color = current.color;
                chahaHeap.rectTransform.localScale = new Vector3(0.4f + 0.6f * t, 0.3f + 0.7f * t, 1f);
            }
            else
            {
                if (InMeasure > 0) jigger.SetLiquid(DrinkCalculator.IngredientLook(current), InMeasure / 10f, current.turbidity);
                else jigger.SetEmpty();
                jigger.SetEffects(0f, 0f, Pouring ? 0.8f : 0.1f);
            }
        }

        /// <summary>액체 줄기 표시색: 재료 고유 색. 무색 투명 액체(진·물)는 흰 선이 아니라 옅은 물빛 반투명으로.</summary>
        public static Color StreamTint(Color liquid)
        {
            var c = liquid;
            float clear = 1f - Mathf.Clamp01(c.a);
            c = Color.Lerp(c, new Color(0.72f, 0.84f, 0.95f, 1f), clear * 0.55f);
            c.a = Mathf.Lerp(0.95f, 0.7f, clear);
            return c;
        }

        /// <summary>줄기 = 출구에서 중력 방향(수직 아래)으로 받는 액면까지. 위쪽 피벗(0.5,1), 회전 없음, 길이는 낙차만큼, 받는 그릇 테두리 뒤.</summary>
        void DrawFall(Image img, Vector2 from, float endY, float width, Color color, Transform behind)
        {
            img.gameObject.SetActive(true);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(width, Mathf.Max(2f, from.y - endY));
            img.sprite = DrinkVesselView.White;
            img.color = color;
            if (behind != null && behind.parent == img.transform.parent)
            {
                int target = behind.GetSiblingIndex();
                if (img.transform.GetSiblingIndex() > target) img.transform.SetSiblingIndex(target);
            }
        }

        void UpdateStream(float rate)
        {
            if (current == null) return;
            var g = Geometry;
            if (current.form == IngredientForm.Leaf)
            {
                // 찻잎은 액체 줄기가 아니라 잎 입자가 떨어진다.
                stream.gameObject.SetActive(false);
                SpawnFallingLeaf(g.lip, new Vector2(g.lip.x, g.mouth.y));
                return;
            }
            // 받는 지점: 출구 바로 아래 지거 안 액면(비었으면 액체 영역 바닥).
            float surfaceY = jigger.PointInParent(new Vector2(0.5f, Mathf.Max(jigger.SurfaceY, DrinkArt.LiquidRect("dl_jigger").yMin))).y;
            float width = Mathf.Lerp(6f, 14f, Mathf.Sqrt(Mathf.Clamp01(rate / LiquidMaxRate)));
            DrawFall(stream, g.lip, surfaceY, width, StreamTint(DrinkCalculator.IngredientLook(current)), jigger.transform);
            g.streamEnd = new Vector2(g.lip.x, surfaceY);
            g.flowing = true;
            Geometry = g;
        }

        float leafTimer;
        void SpawnFallingLeaf(Vector2 from, Vector2 to)
        {
            leafTimer -= Time.unscaledDeltaTime;
            if (leafTimer > 0f) return;
            leafTimer = 0.06f;
            var frames = DrinkArt.Frames("dl_leaf_" + current.id);
            var img = AddImage("Leaf", leafRoot, frames.Length > 0 ? frames[UnityEngine.Random.Range(0, frames.Length)] : DrinkVesselView.White, new Vector2(24f, 24f));
            if (frames.Length == 0) img.color = current.color;
            img.gameObject.AddComponent<DrinkFallingBit>().Init(from, to, 0.45f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (phase == CyclePhase.None && current != null && bottleRt != null && sequence == null)
            {
                // 병은 사용자가 원하는 기울기(놓으면 0)로 일정 속도로 이어서 회전한다.
                float goal = Pouring ? targetTilt : 0f;
                if (!Mathf.Approximately(Tilt, goal)) ApplyTilt(Mathf.MoveTowards(Tilt, goal, BottleTurnSpeed * dt));
            }
            if (Pouring) Step(dt);
            UpdateCycle(dt);
        }

        // ───── 옮겨 붓기 주기: 병 정지·복귀 → 계량 도구 접근·기울임 → 양에 비례한 흐름 → 복귀 ─────
        public enum CyclePhase { None, BottleReturn, ToolApproach, ToolPour, ToolReturn }
        CyclePhase phase;
        float phaseT, phaseDur, bottleFrom;
        public CyclePhase Phase => phase;
        public const float BottleReturnSeconds = 0.45f, ToolMoveSeconds = 0.45f, ToolPourTilt = 105f, ToolLipClearance = 26f;
        /// <summary>계량 도구의 제자리(손잡이 피벗 위치). Build에서 피벗을 grip으로 옮긴 뒤 기록한다.</summary>
        Vector2 jiggerRest, chahaRest, kettleRest;
        public static readonly Vector2 VesselRest = new Vector2(150f, -230f);
        string transferId;
        int transferFrom, transferAdded;
        DrinkIngredientDef transferDef;
        float pourK;
        /// <summary>옮겨 붓기 주기 진행 중(병 복귀부터 계량 도구 복귀까지).</summary>
        public bool Transferring => phase != CyclePhase.None;
        /// <summary>흐름 진행도 0~1(흐르기 전 0, 다 부은 뒤 1). 받는 그릇 수위와 원래 도구 감소가 같은 값을 쓴다.</summary>
        public float TransferProgress => phase == CyclePhase.None || phase == CyclePhase.ToolReturn ? 1f : phase == CyclePhase.ToolPour ? pourK : 0f;

        /// <summary>흐름 시간: 양·점성 비례(보통 0.8~1.2초, 많거나 걸쭉하면 길게). 찻잎은 잎이 다 떨어지는 시간.</summary>
        public static float PourSeconds(DrinkIngredientDef def, int tenths)
        {
            if (def == null) return 0.8f;
            if (def.form == IngredientForm.Leaf) return Mathf.Lerp(0.7f, 1.1f, Mathf.Clamp01(tenths / (float)DrinkRules.ChahaCapacity));
            float baseT = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(tenths / (float)DrinkRules.JiggerCapacity));
            return baseT * (1f + 0.6f * def.viscosity);
        }

        RectTransform Tool => transferDef != null && transferDef.form == IngredientForm.Leaf ? chaha.rectTransform : jigger.Rect;
        Vector2 ToolRest => transferDef != null && transferDef.form == IngredientForm.Leaf ? chahaRest : jiggerRest;
        string ToolArt => transferDef != null && transferDef.form == IngredientForm.Leaf ? "dl_chaha" : "dl_jigger";

        void StartCycle()
        {
            transferId = current.id;
            transferDef = current;
            transferAdded = draft.AmountOf(current.id) - transferFromPending;
            transferFrom = transferFromPending;
            pourK = 0f;
            bottleFrom = Tilt;
            SetPhase(Tilt > 0.5f ? CyclePhase.BottleReturn : CyclePhase.ToolApproach);
            RenderVessel(TransferPreview(0f));
        }
        int transferFromPending;

        void SetPhase(CyclePhase p)
        {
            phase = p;
            phaseT = 0f;
            phaseDur = p switch
            {
                CyclePhase.BottleReturn => BottleReturnSeconds,
                CyclePhase.ToolApproach => ToolMoveSeconds,
                CyclePhase.ToolPour => PourSeconds(transferDef, transferAdded),
                CyclePhase.ToolReturn => ToolMoveSeconds,
                _ => 0f,
            };
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>계량 도구 자세 p(0=제자리 직립, 1=받는 입구 위에서 기울임). 회전된 실제 입구가 받는 입구 위에 오도록 옮긴다.</summary>
        void PoseTool(float p)
        {
            var tool = Tool;
            float tilt = ToolPourTilt * p;
            tool.localRotation = Quaternion.Euler(0f, 0f, -tilt);
            tool.anchoredPosition = ToolRest;
            var lipN = DrinkArt.Anchor(ToolArt, "pourLip");
            var lipAtRest = DrinkVesselView.ToParentAnchored(tool, lipN);
            var target = ReceiverMouth() + new Vector2(0f, ToolLipClearance);
            tool.anchoredPosition = ToolRest + (target - lipAtRest) * p;
            // 금속 지거는 불투명: 기울면 내부 액체를 비추지 않는다(회전한 액체 판 금지). 직립일 때만 보인다.
            if (transferDef.form == IngredientForm.Liquid) jigger.SetLiquidHidden(tilt > 12f);
        }

        Vector2 ReceiverMouth() => vessel.PointInParent(DrinkArt.Anchor(vessel.AssetId, "mouth"));

        void UpdateCycle(float dt)
        {
            if (phase == CyclePhase.None || vessel == null) return;
            if (sequence != null) { FinishTransferVisual(); return; }
            // 편집으로 그 재료 양이 바뀌었으면(빼기·직접 입력 = 명시적 조작) 표시를 끝내고 실제 배합을 그린다.
            if (draft.AmountOf(transferId) != transferFrom + transferAdded) { FinishTransferVisual(); return; }
            phaseT += dt;
            float t = phaseDur <= 0f ? 1f : Mathf.Clamp01(phaseT / phaseDur);
            switch (phase)
            {
                case CyclePhase.BottleReturn:
                    ApplyTilt(Mathf.Lerp(bottleFrom, 0f, Ease(t)));
                    RenderVessel(TransferPreview(0f)); // 아직 붓지 않은 양은 그릇에 없다
                    if (t >= 1f) SetPhase(CyclePhase.ToolApproach);
                    break;
                case CyclePhase.ToolApproach:
                    PoseTool(Ease(t));
                    ShowToolContents(0f);
                    RenderVessel(TransferPreview(0f));
                    if (t >= 1f) SetPhase(CyclePhase.ToolPour);
                    break;
                case CyclePhase.ToolPour:
                    PoseTool(1f);
                    pourK = Ease(t);
                    ShowToolContents(pourK);
                    RenderVessel(TransferPreview(pourK));
                    float flow = Mathf.Sin(Mathf.PI * Mathf.Clamp(t, 0.05f, 0.95f)); // 시작·끝은 가늘게
                    var lip = DrinkVesselView.ToParentAnchored(Tool, DrinkArt.Anchor(ToolArt, "pourLip"));
                    float surface = vessel.PointInParent(new Vector2(0.5f, Mathf.Max(vessel.SurfaceY, DrinkArt.LiquidRect(vessel.AssetId).yMin))).y;
                    if (transferDef.form == IngredientForm.Liquid)
                        DrawFall(dumpStream, lip, surface, Mathf.Lerp(5f, 14f, flow), dumpStream.color, vessel.transform);
                    else
                    {
                        dumpStream.gameObject.SetActive(false); // 찻잎은 줄기 없이 잎만 굴러 떨어진다
                        leafTimer -= dt;
                        if (leafTimer <= 0f && t < 0.9f)
                        {
                            leafTimer = 0.05f;
                            var leafFrames = DrinkArt.Frames("dl_leaf_" + transferDef.id);
                            var img = AddImage("Leaf", leafRoot, leafFrames.Length > 0 ? leafFrames[UnityEngine.Random.Range(0, leafFrames.Length)] : DrinkVesselView.White, new Vector2(24f, 24f));
                            if (leafFrames.Length == 0) img.color = transferDef.color;
                            img.gameObject.AddComponent<DrinkFallingBit>().Init(lip, new Vector2(lip.x, surface), 0.4f);
                        }
                    }
                    TransferGeometry = new PourGeometry { lip = lip, mouth = ReceiverMouth(), streamEnd = new Vector2(lip.x, surface), pivot = Tool.pivot, rotation = Tool.localEulerAngles.z, flowing = transferDef.form == IngredientForm.Liquid };
                    if (t >= 1f) { dumpStream.gameObject.SetActive(false); SetPhase(CyclePhase.ToolReturn); }
                    break;
                case CyclePhase.ToolReturn:
                    PoseTool(1f - Ease(t));
                    ShowToolContents(1f);
                    if (t >= 1f) EndCycle();
                    break;
            }
        }

        /// <summary>옮겨 붓기 줄기 기하(QA·시험).</summary>
        public PourGeometry TransferGeometry { get; private set; }

        /// <summary>계량 도구 안 남은 양(흐름 진행도에 따라 줄어듦).</summary>
        void ShowToolContents(float k)
        {
            float left = transferAdded * (1f - k);
            if (transferDef.form == IngredientForm.Liquid)
            {
                if (left > 0.5f) jigger.SetLiquid(DrinkCalculator.IngredientLook(transferDef), left / 10f, transferDef.turbidity);
                else jigger.SetEmpty();
            }
            else
            {
                float tt = left / DrinkRules.ChahaCapacity;
                chahaHeap.gameObject.SetActive(left > 0.5f);
                chahaHeap.rectTransform.localScale = new Vector3(0.4f + 0.6f * tt, 0.3f + 0.7f * tt, 1f);
            }
        }

        void EndCycle()
        {
            phase = CyclePhase.None;
            dumpStream.gameObject.SetActive(false);
            var tool = Tool;
            tool.localRotation = Quaternion.identity;
            tool.anchoredPosition = ToolRest;
            if (transferDef != null && transferDef.form == IngredientForm.Liquid) { jigger.SetLiquidHidden(false); jigger.SetEmpty(); }
            if (vessel != null) RenderVessel(draft);
            UpdateMeasure();
            ReturnBottleIfIdle();
        }

        void ReturnBottleIfIdle()
        {
            // 놓은 상태면 병은 Update에서 제자리(0°)로 이어서 돌아간다. 계속 누르고 있으면 그 기울기로 다시 돌아가 계량을 이어 간다.
            if (!Pouring) targetTilt = 0f;
        }

        /// <summary>표시용 배합: 옮겨 붓는 재료만 (이전 양 + 옮긴 양 × 진행도). 아직 안 넣은 재료는 없다.</summary>
        DrinkRecipe TransferPreview(float k)
        {
            var r = draft.Clone();
            r.SetAmount(transferId, transferFrom + Mathf.FloorToInt(transferAdded * k));
            return r;
        }

        void RenderVessel(DrinkRecipe r)
        {
            if (vessel == null) return;
            if (r.Kind == DrinkKind.Tea || r.Technique == DrinkTechnique.Build) vessel.SetLayers(PourOrderLayers(r, liquidsOnly: true));
            else
            {
                var look = DrinkCalculator.Look(r);
                vessel.SetLayers(look.totalVolume > 0f ? look.layers : new List<DrinkLayer>());
            }
            if (r.Kind == DrinkKind.Tea) ShowLeafHeap(leafHeap, vessel, r.LeafTotal());
        }

        /// <summary>명시적 조작(편집·제조 확정·도구 교체)에서만 주기를 즉시 끝낸다. 데이터는 이미 정확히 반영돼 있다.</summary>
        void FinishTransferVisual()
        {
            if (phase == CyclePhase.None) return;
            EndCycle();
        }

        void OnDisable() => StopPour();
        /// <summary>QA 캡처(최소화 창, 포커스 없음)에서만 true — 실제 플레이에서는 창 포커스를 잃으면 즉시 멈춘다.</summary>
        public static bool IgnoreFocusLossForCapture;
        void OnApplicationFocus(bool focus) { if (!focus && !IgnoreFocusLossForCapture) StopPour(); }

        void UpdateReadout()
        {
            if (readout == null) return;
            if (current == null) { readout.text = StopReason ?? ""; return; }
            int inDraft = draft.AmountOf(current.id);
            string unit = current.Unit;
            string line = Pouring
                ? $"붓는 중  {(current.form == IngredientForm.Leaf ? "다하" : "지거")} {DrinkRules.Format(InMeasure)}{unit} · 배합 합계 {DrinkRules.Format(inDraft + InMeasure)}{unit}" + (MeasureDumps > 0 ? $" (나눠 붓기 {MeasureDumps}회)" : "")
                : $"배합에 {current.name} {DrinkRules.Format(inDraft)}{unit}";
            if (StopReason != null) line += "\n" + StopReason;
            readout.text = line;
        }

        // ───── 제조 모션 ─────

        public void Skip() => skipRequested = true;

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>제조 확정 후 기법별 모션. skipMotion이면 즉시 완성 상태. done은 항상 한 번 불린다.</summary>
        public void PlayCraft(DrinkRecipe r, bool skipMotion, Action done)
        {
            StopPour();
            Select(null);
            toolLabel.text = "";
            if (sequence != null) StopCoroutine(sequence);
            skipRequested = skipMotion;
            sequence = StartCoroutine(CraftRoutine(r.Clone(), done));
        }

        IEnumerator CraftRoutine(DrinkRecipe r, Action done)
        {
            var look = DrinkCalculator.Look(r);
            EnsureServe(r);
            serve.gameObject.SetActive(false);
            finishLabel.text = DrinkRules.TechniqueName(r.Technique) + "…";
            switch (r.Technique)
            {
                case DrinkTechnique.Shake:
                    if (!skipRequested)
                    {
                        // 뚜껑 닫기 → 흔들기(프레임 또는 흔들림) → 열고 잔에 따르기.
                        var open = vessel.Glass.sprite;
                        var closed = DrinkArt.Sprite("dl_shaker");
                        var shakeFrames = DrinkArt.Frames("dl_shaker_shake");
                        if (closed != null) vessel.Glass.sprite = closed;
                        vessel.SetLiquidHidden(true); // 닫힌 금속 셰이커는 불투명 — 액체를 비춰 보이지 않는다
                        float t = 0f;
                        while (t < 1.6f && !skipRequested)
                        {
                            t += Time.unscaledDeltaTime;
                            if (shakeFrames.Length > 1) vessel.Glass.sprite = shakeFrames[Mathf.FloorToInt(t * 12f) % shakeFrames.Length];
                            vessel.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 50f) * 14f);
                            vessel.Rect.anchoredPosition = new Vector2(150f, -230f + Mathf.Abs(Mathf.Sin(t * 25f)) * 24f);
                            yield return null;
                        }
                        vessel.transform.localRotation = Quaternion.identity;
                        vessel.Rect.anchoredPosition = new Vector2(150f, -230f);
                        vessel.Glass.sprite = open;
                        vessel.SetLiquidHidden(false);
                        vessel.SetLiquid(look.color, look.totalVolume, Mathf.Clamp01(look.turbidity + 0.3f));
                        vessel.SetEffects(1f, 0f, 0.5f);
                        yield return PourInto(vessel, serve, look, true);
                        float f = 0f;
                        while (f < 1.8f && !skipRequested)
                        {
                            f += Time.unscaledDeltaTime;
                            serve.SetEffects(1f - f / 1.8f, 0f, 0.3f); // 일시적인 기포·탁함이 가라앉는다
                            yield return null;
                        }
                    }
                    break;
                case DrinkTechnique.Stir:
                    if (!skipRequested)
                    {
                        spoon.gameObject.SetActive(true);
                        spoon.rectTransform.anchoredPosition = vessel.PointInParent(new Vector2(0.5f, 0.5f));
                        var spoonFrames = DrinkArt.Frames("dl_barspoon");
                        float t = 0f;
                        vessel.SetLiquid(look.color, look.totalVolume, look.turbidity); // 열린 믹싱 글라스: 이미 부은 재료의 혼합색
                        while (t < 1.8f && !skipRequested)
                        {
                            t += Time.unscaledDeltaTime;
                            float k = Mathf.Clamp01(t / 1.4f);
                            vessel.SetEffects(0f, 1f - k * 0.4f, 0.3f);
                            if (spoonFrames.Length > 1) spoon.sprite = spoonFrames[Mathf.FloorToInt(t * 10f) % spoonFrames.Length];
                            else spoon.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 12f) * 10f);
                            spoon.rectTransform.anchoredPosition = vessel.PointInParent(new Vector2(0.5f + Mathf.Sin(t * 12f) * 0.12f, 0.55f));
                            yield return null;
                        }
                        spoon.gameObject.SetActive(false);
                        vessel.SetLiquid(look.color, look.totalVolume, look.turbidity);
                        vessel.SetEffects(0f, 0.4f, 0.2f);
                        yield return PourInto(vessel, serve, look, false);
                    }
                    break;
                case DrinkTechnique.Build:
                    if (!skipRequested)
                    {
                        // 마실 잔에 그대로: 아래 층부터 색이 퍼져 섞인다.
                        serve.gameObject.SetActive(false);
                        var layers = PourOrderLayers(r, true);
                        float t = 0f;
                        while (t < 1.8f && !skipRequested)
                        {
                            t += Time.unscaledDeltaTime;
                            var mixed = new List<DrinkLayer>();
                            for (int i = 0; i < layers.Count; i++)
                            {
                                float k = Mathf.Clamp01(t / 1.6f * 1.4f - i * 0.12f);
                                mixed.Add(new DrinkLayer { color = Color.Lerp(layers[i].color, look.color, k), volume = layers[i].volume, turbidity = Mathf.Lerp(layers[i].turbidity, look.turbidity, k) });
                            }
                            vessel.SetLayers(mixed);
                            vessel.SetEffects(0f, 0.15f, 0.25f);
                            yield return null;
                        }
                    }
                    break;
                case DrinkTechnique.Layer:
                    if (!skipRequested)
                    {
                        // 순서대로 천천히: 밀도 순 층이 하나씩 쌓이고 경계가 남는다.
                        vessel.SetEmpty();
                        var built = new List<DrinkLayer>();
                        for (int i = 0; i < look.layers.Count && !skipRequested; i++)
                        {
                            var l = look.layers[i];
                            float t = 0f;
                            while (t < 0.9f && !skipRequested)
                            {
                                t += Time.unscaledDeltaTime;
                                var cur = new List<DrinkLayer>(built) { new DrinkLayer { color = l.color, turbidity = l.turbidity, volume = l.volume * Mathf.Clamp01(t / 0.8f) } };
                                vessel.SetLayers(cur);
                                vessel.SetEffects(0f, 0f, 0.2f);
                                var m = ReceiverMouth();
                                DrawFall(stream, m + new Vector2(0f, 40f), vessel.PointInParent(new Vector2(0.5f, vessel.SurfaceY)).y, 5f, StreamTint(l.color), vessel.transform); // 천천히 가늘게
                                yield return null;
                            }
                            built.Add(l);
                        }
                        stream.gameObject.SetActive(false);
                    }
                    break;
                case DrinkTechnique.Steep:
                    if (!skipRequested)
                    {
                        var water = new Color(0.93f, 0.97f, 1f, 0.3f);
                        var brew = look.color;
                        int leaf = r.LeafTotal();
                        // 물 붓기: 물 용기를 다관 입구 위로 옮겨 손잡이 축으로 기울이고(주둥이 → 다관), 수직 줄기로 붓고, 제자리로.
                        ShowLeafHeap(leafHeap, vessel, leaf);
                        var kettleRt = kettle.rectTransform;
                        yield return PoseOver(kettleRt, "dl_water_kettle", kettleRest, ReceiverMouth(), 95f, 0.5f, false);
                        float t = 0f, pourDur = 1.3f;
                        while (t < pourDur && !skipRequested)
                        {
                            t += Time.unscaledDeltaTime;
                            float k = Ease(Mathf.Clamp01(t / pourDur));
                            vessel.SetLiquid(water, r.LiquidTotal() / 10f + r.water / 10f * k, 0f); // 먼저 넣은 꿀·레몬 위로 물이 더해진다
                            vessel.SetEffects(0f, 0f, 0.8f);
                            var lip = DrinkVesselView.ToParentAnchored(kettleRt, DrinkArt.Anchor("dl_water_kettle", "pourLip"));
                            DrawFall(stream, lip, vessel.PointInParent(new Vector2(0.5f, Mathf.Max(vessel.SurfaceY, DrinkArt.LiquidRect(vessel.AssetId).yMin))).y,
                                Mathf.Lerp(5f, 12f, Mathf.Sin(Mathf.PI * Mathf.Clamp(t / pourDur, 0.05f, 0.95f))), StreamTint(water), vessel.transform);
                            ShowLeafHeap(leafHeap, vessel, leaf);
                            yield return null;
                        }
                        stream.gameObject.SetActive(false);
                        yield return PoseOver(kettleRt, "dl_water_kettle", kettleRest, ReceiverMouth(), 95f, 0.45f, true);
                        // 우림: 투명한 물 → 우린 색, 김
                        t = 0f;
                        steam.gameObject.SetActive(true);
                        var steamFrames = DrinkArt.Frames("dl_steam");
                        while (t < 2.0f && !skipRequested)
                        {
                            t += Time.unscaledDeltaTime;
                            float k = Mathf.SmoothStep(0f, 1f, t / 1.9f);
                            vessel.SetLiquid(Color.Lerp(water, brew, k), look.totalVolume, look.turbidity * k);
                            vessel.SetEffects(0f, 0.1f, 0.2f);
                            AnimateSteam(steamFrames, vessel, t);
                            yield return null;
                        }
                        leafHeap.gameObject.SetActive(false);
                        yield return PourInto(vessel, serve, look, false);
                    }
                    break;
            }
            ShowFinal(r, look);
            sequence = null;
            skipRequested = false;
            done?.Invoke();
        }

        void AnimateSteam(Sprite[] frames, DrinkVesselView over, float t)
        {
            if (frames.Length > 0) { steam.sprite = frames[Mathf.FloorToInt(t * 8f) % frames.Length]; steam.color = Color.white; NativeSize(steam, 4); }
            else steam.color = new Color(1f, 1f, 1f, 0.25f + 0.15f * Mathf.Sin(t * 6f));
            steam.rectTransform.anchoredPosition = over.PointInParent(new Vector2(0.45f, 1.05f)) + new Vector2(0f, 50f + Mathf.Sin(t * 3f) * 6f);
        }

        void EnsureServe(DrinkRecipe r)
        {
            string id = ServeFor(r);
            if (serve != null && serve.AssetId != id) { Destroy(serve.gameObject); serve = null; }
            if (serve == null)
            {
                serve = DrinkVesselView.Create(root, id, new Vector2(-170f, -230f), CapacityFor(id));
                Placeholders |= serve.Placeholder;
            }
            serve.SetEmpty();
        }

        /// <summary>
        /// 붓는 도구 자세 보간: 손잡이 피벗으로 입구 쪽으로 기울이며(입구가 손잡이 오른쪽이면 시계 방향) 이동해,
        /// 회전된 실제 입구가 받는 입구 위(여유 30px)에 오게 한다. back=true면 같은 경로로 제자리·직립으로 돌아간다.
        /// </summary>
        IEnumerator PoseOver(RectTransform rt, string artId, Vector2 basePos, Vector2 targetMouth, float maxTilt, float seconds, bool back)
        {
            var lipN = DrinkArt.Anchor(artId, "pourLip");
            var gripN = DrinkArt.Anchor(artId, "grip");
            float side = lipN.x >= gripN.x ? -1f : 1f; // z 회전 부호(음수 = 시계 방향)
            float t = 0f;
            do
            {
                t += Time.unscaledDeltaTime;
                float p = Ease(Mathf.Clamp01(t / seconds));
                if (back) p = 1f - p;
                if (skipRequested) p = back ? 0f : 1f;
                rt.localRotation = Quaternion.Euler(0f, 0f, side * maxTilt * p);
                rt.anchoredPosition = basePos;
                var lipAtBase = DrinkVesselView.ToParentAnchored(rt, lipN);
                rt.anchoredPosition = basePos + (targetMouth + new Vector2(0f, 30f) - lipAtBase) * p;
                yield return null;
            } while (t < seconds && !skipRequested);
        }

        /// <summary>
        /// 그릇 → 마실 잔(또는 다관 → 찻잔). 받는 잔은 붓는 입구 쪽에 놓고(다관 주둥이가 오른쪽이면 찻잔도 오른쪽), 붓는 그릇은 손잡이 축으로
        /// 기울여 입구를 잔 위로 가져간다. 불투명한 금속 셰이커·다관과 기운 그릇의 내부 액체 판은 회전시켜 그리지 않는다(숨김).
        /// 흐름 동안 원래 그릇의 남은 양과 받는 잔의 양은 같은 진행도로 줄고/늘어 합이 보존된다.
        /// </summary>
        IEnumerator PourInto(DrinkVesselView from, DrinkVesselView to, DrinkLook look, bool strain)
        {
            steam.gameObject.SetActive(false); // 김은 그릇을 따라가지 않으므로 붓는 동안 숨긴다(완성 잔에서 다시)
            var lipN = DrinkArt.Anchor(from.AssetId, "pourLip");
            var gripN = DrinkArt.Anchor(from.AssetId, "grip");
            float dir = lipN.x >= gripN.x ? 1f : -1f;
            DrinkVesselView.SetPivotKeepPosition(from.Rect, new Vector2(0.5f, 0f));
            var fromBase = from.Rect.anchoredPosition;
            float toX = fromBase.x + dir * 270f, shift = 0f;
            if (toX > 300f) shift = 300f - toX;
            if (toX < -300f) shift = -300f - toX;
            // 공간이 모자라면 붓는 그릇을 먼저 반대쪽으로 부드럽게 옮긴다.
            if (Mathf.Abs(shift) > 0.5f)
            {
                float m = 0f;
                while (m < 0.4f && !skipRequested) { m += Time.unscaledDeltaTime; from.Rect.anchoredPosition = fromBase + new Vector2(shift * Ease(Mathf.Clamp01(m / 0.4f)), 0f); yield return null; }
                fromBase += new Vector2(shift, 0f);
                from.Rect.anchoredPosition = fromBase;
            }
            DrinkVesselView.SetPivotKeepPosition(to.Rect, new Vector2(0.5f, 0f));
            to.Rect.anchoredPosition = new Vector2(toX + shift, VesselRest.y);
            to.transform.localRotation = Quaternion.identity;
            to.gameObject.SetActive(true);
            to.SetEmpty();
            DrinkVesselView.SetPivotKeepPosition(from.Rect, gripN);
            var gripBase = from.Rect.anchoredPosition;
            var toMouth = to.PointInParent(DrinkArt.Anchor(to.AssetId, "mouth"));
            from.SetLiquidHidden(false);
            from.SetLiquid(look.color, look.totalVolume, look.turbidity);
            // 접근·기울임
            float t = 0f;
            const float approach = 0.5f;
            while (t < approach && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                PoseVessel(from, gripBase, toMouth, Ease(Mathf.Clamp01(t / approach)), dir);
                yield return null;
            }
            // 흐름: 양에 비례한 시간
            float dur = Mathf.Clamp(0.9f + look.totalVolume / 150f * 0.9f, 0.9f, 1.8f);
            t = 0f;
            while (t < dur && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease(Mathf.Clamp01(t / dur));
                PoseVessel(from, gripBase, toMouth, 1f, dir);
                from.SetLiquid(look.color, look.totalVolume * (1f - k), look.turbidity);
                to.SetLiquid(look.color, look.totalVolume * k, look.turbidity);
                to.SetEffects(strain ? 0.8f : 0f, 0f, 0.7f);
                var lip = DrinkVesselView.ToParentAnchored(from.Rect, lipN);
                float surf = to.PointInParent(new Vector2(0.5f, Mathf.Max(to.SurfaceY, DrinkArt.LiquidRect(to.AssetId).yMin))).y;
                DrawFall(stream, lip, surf, Mathf.Lerp(5f, 13f, Mathf.Sin(Mathf.PI * Mathf.Clamp(t / dur, 0.05f, 0.95f))), StreamTint(look.color), to.transform);
                ServeGeometry = new PourGeometry { lip = lip, mouth = toMouth, streamEnd = new Vector2(lip.x, surf), pivot = from.Rect.pivot, rotation = from.Rect.localEulerAngles.z, flowing = true };
                yield return null;
            }
            stream.gameObject.SetActive(false);
            from.SetEmpty();
            // 복귀
            t = 0f;
            while (t < approach && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                PoseVessel(from, gripBase, toMouth, 1f - Ease(Mathf.Clamp01(t / approach)), dir);
                yield return null;
            }
            PoseVessel(from, gripBase, toMouth, 0f, dir);
            from.SetLiquidHidden(false);
        }

        /// <summary>그릇 → 잔 붓기 기하(QA·시험).</summary>
        public PourGeometry ServeGeometry { get; private set; }

        void PoseVessel(DrinkVesselView v, Vector2 gripBase, Vector2 targetMouth, float p, float dir)
        {
            var rt = v.Rect;
            float tilt = 100f * p;
            rt.localRotation = Quaternion.Euler(0f, 0f, -dir * tilt);
            rt.anchoredPosition = gripBase;
            var lipN = DrinkArt.Anchor(v.AssetId, "pourLip");
            var lipAtBase = DrinkVesselView.ToParentAnchored(rt, lipN);
            rt.anchoredPosition = gripBase + (targetMouth + new Vector2(0f, 30f) - lipAtBase) * p;
            // 수평 액면을 그리지 못하는 기운 상태에서는 내부 액체 판을 숨긴다(셰이커·다관은 불투명, 유리는 붓는 동안 내부 생략).
            v.SetLiquidHidden(tilt > 12f);
        }

        /// <summary>완성 상태(모션 건너뛰기와 동일 결과): 마실 그릇에 최종 색·층·얼음.</summary>
        void ShowFinal(DrinkRecipe r, DrinkLook look)
        {
            stream.gameObject.SetActive(false);
            spoon.gameObject.SetActive(false);
            leafHeap.gameObject.SetActive(false);
            bool inVessel = r.Technique == DrinkTechnique.Build || r.Technique == DrinkTechnique.Layer;
            var target = inVessel ? vessel : serve;
            if (!inVessel) { vessel.SetEmpty(); serve.gameObject.SetActive(true); }
            else if (serve != null) serve.gameObject.SetActive(false);
            target.transform.localRotation = Quaternion.identity;
            target.SetLayers(look.layers);
            target.SetEffects(0f, 0f, 0.1f);
            target.SetIce(r.Kind == DrinkKind.Cocktail ? r.ice : 0);
            steam.gameObject.SetActive(r.Kind == DrinkKind.Tea);
            if (r.Kind == DrinkKind.Tea) AnimateSteam(DrinkArt.Frames("dl_steam"), target, 0f);
            finishLabel.text = $"완성 — {r.name}";
            FinalVessel = target;
        }

        public DrinkVesselView FinalVessel { get; private set; }
    }

    /// <summary>병/통 잡기 영역. 누르는 순간부터 끄는 동안 기울기 갱신, 놓기·드래그 끝·취소에 즉시 멈춘다.</summary>
    public class DrinkPourHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler, IBeginDragHandler
    {
        public DrinkStageView Stage;
        public void OnPointerDown(PointerEventData e) => Stage?.BeginPour(e.position);
        public void OnBeginDrag(PointerEventData e) => Stage?.DragTo(e.position);
        public void OnDrag(PointerEventData e) => Stage?.DragTo(e.position);
        public void OnPointerUp(PointerEventData e) => Stage?.StopPour();
        public void OnEndDrag(PointerEventData e) => Stage?.StopPour();
        void OnDisable() => Stage?.StopPour();
    }

    /// <summary>떨어지는 찻잎 한 장(표시 전용).</summary>
    public class DrinkFallingBit : MonoBehaviour
    {
        Vector2 from, to;
        float t, dur;
        public void Init(Vector2 from, Vector2 to, float dur) { this.from = from; this.to = to; this.dur = dur; }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            ((RectTransform)transform).anchoredPosition = Vector2.Lerp(from, to, k) + new Vector2(Mathf.Sin(k * 9f) * 6f, 0f);
            transform.localRotation = Quaternion.Euler(0, 0, k * 220f);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
