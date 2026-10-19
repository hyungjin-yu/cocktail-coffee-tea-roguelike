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
    /// 73 단계1: 모든 그릇의 내용물은 DrinkTransferLedger 하나로 관리한다(병 → 지거/다하 → 셰이커·믹싱 글라스·다관 → 완성 잔 + 공중 줄기).
    /// 원래 그릇에서 흐른 만큼만 줄고, 낙하 시간이 지나 받는 그릇에 닿은 만큼만 는다. 기운 투명 그릇은 수평 액면으로 그리고(내용물 숨김 없음),
    /// 액체가 실제 입구 높이를 넘을 때(SolveTilt)만 흐른다. 배합 데이터(draft)는 기존대로 즉시 정확히 반영되고 장부는 화면의 가상 예약분이다.
    /// 제조 확정 후에는 기법별 모션(셰이킹/스터링/빌드/레이어링/차 우림)을 재생한다(건너뛰기 가능, 결과 동일).
    /// </summary>
    public class DrinkStageView : MonoBehaviour
    {
        /// <summary>찻잎 통(그리고 병 마스크가 없을 때)은 85°부터 흐른다. 액체 병은 실제 잔량의 SolveTilt로 입구에 닿는 각도부터 흐른다
        /// (많으면 일찍, 적으면 더 기울여야). 거의 빈 병까지 따를 수 있게 165°까지 기울인다.</summary>
        public const float TiltStart = 85f, TiltMax = 165f;
        public const float LiquidMaxRate = 250f; // 0.1mL/초 → 25mL/s
        public const float LeafMaxRate = 25f;    // 0.1g/초 → 2.5g/s
        /// <summary>화면 중력(px/s²). 줄기 머리·꼬리와 장부 덩어리의 낙하 시간이 같은 값을 쓴다.</summary>
        public const float GravityPx = 1800f;

        /// <summary>장부 그릇 이름.</summary>
        public const string LedgerBottle = "bottle", LedgerMeasure = "measure", LedgerVessel = "vessel", LedgerServe = "serve", LedgerKettle = "kettle";

        RectTransform root, scene;
        /// <summary>카메라가 움직이는 장면 층(도구가 여기 붙는다).</summary>
        public RectTransform Scene => scene;
        Text readout, toolLabel, finishLabel, sourceLabel;
        Image bottle, stream, leafHeap, chaha, kettle, steam, spoon, chahaHeap;
        RectTransform bottleRt, leafRoot;
        DrinkVesselView jigger, vessel, serve;
        DrinkRecipe draft;
        Func<DrinkIngredientDef, int> allowed;
        Action changed;
        DrinkIngredientDef current;
        string vesselId;
        FallStream bottleFall, toolFall;
        /// <summary>액체 원천 병(분리 아트: 열린 전경 + 물리 내부 마스크 + 뚜껑). 없으면 기존 단일 PNG 병(Image).</summary>
        DrinkVesselView srcBottle;
        Image srcCap;
        float bottleOnset = TiltStart;
        double onsetVolume = -1;

        /// <summary>제조대 전체 내용물 장부(가상 예약분 — 창고 재고와 별개).</summary>
        public DrinkTransferLedger Ledger { get; } = new DrinkTransferLedger();

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
        /// <summary>원천 병(분리 아트가 있는 액체만). 내용물은 장부의 병 잔량.</summary>
        public DrinkVesselView SourceBottle => srcBottle;
        /// <summary>지금 병 잔량의 액면이 실제 입구에 처음 닿는 기울기(도). 찻잎 통은 TiltStart.</summary>
        public float BottleOnset => srcBottle != null ? bottleOnset : TiltStart;

        /// <summary>원천 병 고정 용량(mL, 게임값 — 실측 아님). 잔량은 이 안에서만 변하고 용량을 잔량에 맞춰 바꾸지 않는다.
        /// 창고 가상 재고가 병 용량보다 많으면 나머지는 창고에 남고 병을 다시 고르면 채운다.</summary>
        public static float BottleCapacityMl(string id) => id switch
        {
            "gin" or "rum" => 500f,
            "blue_curacao" or "grenadine" => 350f,
            _ => 250f, // lemon_juice, honey
        };
        public DrinkVesselView MainVessel => vessel;
        public DrinkVesselView ServeVessel => serve;
        public DrinkVesselView Jigger => jigger;
        public RectTransform ChahaRect => chaha != null ? chaha.rectTransform : null;
        public RectTransform KettleRect => kettle != null ? kettle.rectTransform : null;
        public RectTransform SpoonRect => spoon != null ? spoon.rectTransform : null;
        public string ReadoutText => readout != null ? readout.text : "";
        /// <summary>지금 고른 재료(계량 중인 재료) id.</summary>
        public string CurrentId => current?.id;
        /// <summary>계량 도구 안 양이 바뀔 때(오른쪽 미리보기 실시간 갱신용).</summary>
        public event Action MeasureChanged;
        public Color StreamColor => stream != null ? stream.color : Color.clear;
        public bool StreamVisible => stream != null && stream.gameObject.activeSelf;
        /// <summary>병(또는 그릇) 입구에서 지금 액체가 나오는 중(줄기가 입구에 붙어 있음). 멈춘 뒤 떨어지는 꼬리는 false.</summary>
        public bool StreamEmitting => bottleFall != null && bottleFall.Emitting;
        public Color DumpStreamColor => dumpStream != null ? dumpStream.color : Color.clear;
        public bool DumpStreamVisible => dumpStream != null && dumpStream.gameObject.activeSelf;
        public bool DumpStreamEmitting => toolFall != null && toolFall.Emitting;
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
            root.gameObject.AddComponent<RectMask2D>(); // 배경 아트를 제조대 안에서만
            backdropRoot = UiFactory.Stretch("Backdrop", root);
            // 조용한 상판(도구가 놓이는 면, 높이 HudHeight..HudHeight+TableHeight)과 그 아래 완전 불투명 정보 띠(안내·수량, 배경과 겹치지 않음).
            var counter = UiFactory.Place("Counter", root, new Vector2(0.5f, 0f), new Vector2(0f, HudHeight), new Vector2(780f, TableHeight));
            counter.anchorMin = new Vector2(0f, 0f); counter.anchorMax = new Vector2(1f, 0f); counter.sizeDelta = new Vector2(0f, TableHeight);
            counterImage = UiFactory.AddImage(counter, new Color(0.36f, 0.23f, 0.14f, 1f));
            counterImage.raycastTarget = false;
            var hud = UiFactory.Place("InfoHud", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(780f, HudHeight));
            hud.anchorMin = new Vector2(0f, 0f); hud.anchorMax = new Vector2(1f, 0f); hud.sizeDelta = new Vector2(0f, HudHeight);
            UiFactory.AddImage(hud, new Color(0.07f, 0.055f, 0.05f, 1f)).raycastTarget = false;

            // 카메라가 움직이는 장면(도구·줄기·내용물·잎). 벽·상판·정보띠·안내 글자는 고정. 손가락 드래그는 차이(델타)로만 읽어 팬과 무관.
            scene = UiFactory.Stretch("Scene", root);
            toolLabel = UiFactory.AddText(UiFactory.Place("ToolLabel", root, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(760f, 38f)), "", DrinkPaperUi.Small, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f));
            readout = UiFactory.AddText(UiFactory.Place("Readout", root, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(760f, 38f)), "", DrinkPaperUi.Small, TextAnchor.MiddleCenter, Color.white);
            finishLabel = UiFactory.AddText(UiFactory.Place("Finish", root, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(760f, 44f)), "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.5f));

            leafRoot = UiFactory.Stretch("Leaves", scene);
            stream = AddImage("Stream", scene, DrinkArt.Sprite("dl_pour_stream") ?? DrinkVesselView.White, new Vector2(16f, 100f));
            stream.rectTransform.pivot = new Vector2(0.5f, 1f);
            stream.gameObject.SetActive(false);
            bottleFall = new FallStream(stream);
            // 지거/다하 → 그릇 옮겨 붓기 줄기(재료 고유 색). 병 줄기와 따로 둔다.
            dumpStream = AddImage("DumpStream", scene, DrinkArt.Sprite("dl_pour_stream") ?? DrinkVesselView.White, new Vector2(12f, 100f));
            dumpStream.rectTransform.pivot = new Vector2(0.5f, 1f);
            dumpStream.gameObject.SetActive(false);
            toolFall = new FallStream(dumpStream);

            kettle = AddImage("Kettle", scene, DrinkArt.SpriteOrPlaceholder("dl_water_kettle", out bool kp), Vector2.zero);
            NativeSize(kettle, 3);
            // 다관 오른쪽 위: 왼쪽의 재료 병/통·가운데 지거/다하·다관 오른쪽에 내려놓는 뚜껑과 겹치지 않는다. 물을 부을 때만 다관 입구 위로 옮긴다.
            kettle.rectTransform.anchoredPosition = new Vector2(300f, 140f);
            DrinkVesselView.SetPivotKeepPosition(kettle.rectTransform, DrinkArt.Anchor("dl_water_kettle", "grip"));
            kettleRest = kettle.rectTransform.anchoredPosition;
            kettle.gameObject.SetActive(false);
            Placeholders |= kp;

            steam = AddImage("Steam", scene, DrinkArt.Sprite("dl_steam") ?? DrinkVesselView.White, new Vector2(64f, 96f));
            steam.gameObject.SetActive(false);

            bottle = AddImage("Bottle", scene, DrinkVesselView.White, new Vector2(96f, 224f));
            bottleRt = bottle.rectTransform;
            bottle.raycastTarget = true;
            var handle = bottle.gameObject.AddComponent<DrinkPourHandle>();
            handle.Stage = this;
            AddGrabArea(bottleRt);
            // 원천(병·통) 남은 양: 불투명 병/통 내부를 억지로 비추지 않고 장부의 현재 남은 양을 글자로 보인다(창고 차감은 제조 확정 때).
            sourceLabel = UiFactory.AddText(UiFactory.Place("SourceLeft", scene, new Vector2(0.5f, 0.5f), BottleRest + new Vector2(0f, -134f), new Vector2(300f, 40f)), "", DrinkPaperUi.Small, TextAnchor.MiddleCenter, new Color(0.95f, 0.88f, 0.75f));
            sourceLabel.raycastTarget = false;
            AddPlate(sourceLabel);

            chaha = AddImage("Chaha", scene, DrinkArt.SpriteOrPlaceholder("dl_chaha", out bool cp), Vector2.zero);
            NativeSize(chaha, 4);
            chaha.rectTransform.anchoredPosition = new Vector2(-30f, 10f);
            DrinkVesselView.SetPivotKeepPosition(chaha.rectTransform, DrinkArt.Anchor("dl_chaha", "grip"));
            chahaRest = chaha.rectTransform.anchoredPosition;
            chahaHeap = AddImage("ChahaHeap", chaha.rectTransform, DrinkVesselView.White, new Vector2(120f, 30f));
            chahaHeap.rectTransform.anchorMin = chahaHeap.rectTransform.anchorMax = new Vector2(0.5f, 0.45f);
            chahaHeap.rectTransform.pivot = new Vector2(0.5f, 0f);
            Placeholders |= cp;

            jigger = DrinkVesselView.Create(scene, "dl_jigger", new Vector2(-30f, -20f), DrinkRules.JiggerCapacity / 10f);
            DrinkVesselView.SetPivotKeepPosition(jigger.Rect, DrinkArt.Anchor("dl_jigger", "grip"));
            jiggerRest = jigger.Rect.anchoredPosition;
            Placeholders |= jigger.Placeholder;

            leafHeap = AddImage("LeafHeap", scene, DrinkVesselView.White, new Vector2(128f, 64f));
            leafHeap.rectTransform.pivot = new Vector2(0.5f, 0f);
            leafHeap.gameObject.SetActive(false);

            spoon = AddImage("Barspoon", scene, DrinkArt.SpriteOrPlaceholder("dl_barspoon", out bool sp), Vector2.zero);
            NativeSize(spoon, 4);
            spoon.gameObject.SetActive(false);
            Placeholders |= sp;
        }

        readonly List<(Text label, Image plate)> plates = new List<(Text, Image)>();

        /// <summary>안내·수량 글자 뒤 단색 판: 배경 그림의 선반 세부와 겹쳐도 읽힌다. 크기는 글자에 맞춰 UpdatePlates가 맞춘다.</summary>
        void AddPlate(Text label)
        {
            var lr = label.rectTransform;
            var pr = UiFactory.Place(lr.name + "Plate", lr.parent, lr.anchorMin, Vector2.zero, lr.sizeDelta);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.SetSiblingIndex(lr.GetSiblingIndex());
            var img = pr.gameObject.AddComponent<Image>();
            img.color = new Color(0.07f, 0.055f, 0.05f, 1f); // 완전 불투명(뒤 선반 세부가 글자와 섞이지 않음)
            img.raycastTarget = false;
            plates.Add((label, img));
        }

        void UpdatePlates()
        {
            foreach (var (label, img) in plates)
            {
                bool on = label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text);
                if (img.enabled != on) img.enabled = on;
                if (!on) continue;
                var lr = label.rectTransform;
                var pr = img.rectTransform;
                pr.anchoredPosition = lr.anchoredPosition + new Vector2((0.5f - lr.pivot.x) * lr.sizeDelta.x, (0.5f - lr.pivot.y) * lr.sizeDelta.y);
                pr.sizeDelta = new Vector2(Mathf.Min(lr.sizeDelta.x, label.preferredWidth + 28f), Mathf.Min(lr.sizeDelta.y, label.preferredHeight + 10f));
            }
        }

        /// <summary>병 주위 투명 잡기 영역(터치에서도 잡기 쉽게). 누르기·끌기·놓기를 제조대로 넘긴다.</summary>
        void AddGrabArea(RectTransform target)
        {
            var hit = UiFactory.Stretch("HitArea", target);
            hit.offsetMin = new Vector2(-40f, -30f); hit.offsetMax = new Vector2(40f, 30f);
            var img = hit.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            hit.gameObject.AddComponent<DrinkPourHandle>().Stage = this;
        }

        /// <summary>병 잔량이 바뀌면 입구에 닿는 각도를 다시 구한다(0.2mL 이상 변할 때만, 프로필은 캐시).
        /// 탐색은 SolveTilt 수십 회라 붓는 중(매 프레임 잔량 변화)에는 하지 않는다 — 그동안 흐름은 매 프레임 SolveTilt 1회의 유출량으로 정한다.
        /// 이 각도는 다음 접근 자세(입구 정렬 완료 각도)와 표시에만 쓴다.</summary>
        void UpdateOnset()
        {
            if (srcBottle == null) return;
            if (onsetVolume >= 0 && (Pouring || phase != CyclePhase.None)) return;
            double have = Ledger[LedgerBottle].LiquidMl;
            if (onsetVolume >= 0 && Math.Abs(have - onsetVolume) < 0.2) return;
            onsetVolume = have;
            bottleOnset = have > 0 ? SpillOnsetAngle(srcBottle, have) : TiltMax;
        }

        string BottleArt => srcBottle != null ? srcBottle.AssetId : current?.bottleId;

        /// <summary>제조대 아래 정보 띠·상판 높이(1920×1080 기준). 도구는 상판 위(VesselRest 바닥 = 아래에서 약 115).</summary>
        public const float HudHeight = 92f, TableHeight = 24f;
        RectTransform backdropRoot;
        Image counterImage;
        DrinkKind? backdropKind;

        /// <summary>
        /// 종류별 제조 배경(기존 ArtExtra 재사용): 칵테일 = 바 벽(bg_bar_wall)·바 카운터 앞면(bg_bar_counter), 차 = 차 벽·차 탁자 앞면.
        /// 벽은 균일 확대 후 어둡게(투명 액체·줄기 판독 우선), 카운터는 위 물건 줄을 잘라 앞면 나무판만 도구가 놓이는 바닥으로 쓴다.
        /// 아트가 없으면 기존 단색(기능 유지).
        /// </summary>
        void SetBackdrop(DrinkKind kind)
        {
            if (backdropKind == kind) return;
            backdropKind = kind;
            foreach (Transform c in backdropRoot) Destroy(c.gameObject);
            var size = root.rect.size.x > 0 ? root.rect.size : new Vector2(800f, 690f);
            // 73 제조 전용 배경(CraftingBackdrops ready): 저대비 벽 400×287·상판 400×12를 정수 2배로 그대로(막·반복 캐비닛 없음).
            var craftWall = DrinkArt.Sprite(kind == DrinkKind.Tea ? "dl_craft_tea_wall" : "dl_craft_bar_wall");
            var craftTable = DrinkArt.Sprite(kind == DrinkKind.Tea ? "dl_craft_tea_table" : "dl_craft_bar_table");
            if (craftWall != null && craftTable != null)
            {
                var wr = UiFactory.Place("CraftWall", backdropRoot, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(craftWall.rect.width * 2f, craftWall.rect.height * 2f));
                wr.pivot = new Vector2(0.5f, 1f);
                var wi = wr.gameObject.AddComponent<Image>();
                wi.sprite = craftWall; wi.raycastTarget = false;
                var cRt = counterImage.rectTransform;
                foreach (Transform c in cRt) Destroy(c.gameObject);
                counterImage.enabled = true;
                counterImage.sprite = craftTable;
                counterImage.type = Image.Type.Simple;
                counterImage.color = Color.white;
                return;
            }
            string prefix = kind == DrinkKind.Tea ? "bg_tea" : "bg_bar";
            var wall = Resources.Load<Sprite>($"ArtExtra/{prefix}_wall/{prefix}_wall_00");
            counterImage.sprite = null;
            if (wall != null)
            {
                float k = Mathf.Max(size.x / wall.rect.width, (size.y - 110f) / wall.rect.height); // 덮기(가운데 위 정렬, 넘치는 부분은 잘림)
                var wr = UiFactory.Place("Wall", backdropRoot, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(wall.rect.width * k, wall.rect.height * k));
                wr.pivot = new Vector2(0.5f, 1f);
                var wi = wr.gameObject.AddComponent<Image>();
                wi.sprite = wall; wi.raycastTarget = false;
                wi.color = kind == DrinkKind.Tea ? new Color(0.55f, 0.53f, 0.5f) : new Color(0.52f, 0.5f, 0.5f);
                // 고른 어두운 막: 그림의 밝은 선반과 어두운 가운데 거울 판의 명암 차를 줄여 경계가 도드라지지 않고, 액체·줄기·안내가 먼저 읽힌다.
                var veil = UiFactory.Stretch("Veil", backdropRoot).gameObject.AddComponent<Image>();
                veil.color = kind == DrinkKind.Tea ? new Color(0.12f, 0.09f, 0.07f, 0.84f) : new Color(0.1f, 0.075f, 0.07f, 0.86f);
                veil.raycastTarget = false;
            }
            var counterRt = counterImage != null ? counterImage.rectTransform : null;
            if (counterRt != null) foreach (Transform c in counterRt) Destroy(c.gameObject);
            // 임시 조용한 상판(제조 전용 상판 아트 도착 전): 기존 counter 아트의 상판 띠에는 잔·병 같은 소품이 섞여 있어 쓰지 않고
            // 바=어두운 호두, 찻집=저채도 밝은 나무의 두 톤 판(윗면 밝은 선 4px)만 그린다. 아트가 오면 이 자리를 교체한다.
            if (counterRt != null)
            {
                counterImage.enabled = true;
                counterImage.color = kind == DrinkKind.Tea ? new Color(0.5f, 0.38f, 0.26f, 1f) : new Color(0.3f, 0.19f, 0.12f, 1f);
                var top = UiFactory.Place("TableTop", counterRt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f));
                top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f); top.pivot = new Vector2(0.5f, 1f); top.sizeDelta = new Vector2(0f, 4f);
                UiFactory.AddImage(top, kind == DrinkKind.Tea ? new Color(0.68f, 0.55f, 0.4f, 1f) : new Color(0.46f, 0.31f, 0.2f, 1f)).raycastTarget = false;
            }
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

        /// <summary>그릇 용량(mL). 완성 잔은 DrinkRules의 고정 용량(최대 배합+얼음에서도 약 80~82%), 제조 도구는 최대 배합의 약 80%.</summary>
        public static float CapacityFor(string id) => id switch
        {
            "dl_teapot" => 260f,
            "dl_teacup" => DrinkRules.ServeTeaCapacityMl,
            "dl_glass_cocktail" => DrinkRules.ServeCocktailCapacityMl,
            "dl_jigger" => DrinkRules.JiggerCapacity / 10f,
            _ => 150f,
        };

        /// <summary>배합(기법) 바뀜 → 그릇 교체·내용 다시 그림.</summary>
        public void RefreshDraft()
        {
            if (sequence != null) return;
            showingFinal = false;
            SetBackdrop(draft.Kind);
            finishLabel.text = "";
            EnsureVessel(draft);
            vessel.transform.localRotation = Quaternion.identity;
            DrinkVesselView.SetPivotKeepPosition(vessel.Rect, new Vector2(0.5f, 0f));
            vessel.Rect.anchoredPosition = StandOnTable(vessel, VesselRest.x);
            vessel.SetLiquidHidden(false);
            if (kettle != null) { kettle.rectTransform.localRotation = Quaternion.identity; kettle.rectTransform.anchoredPosition = kettleRest; }
            vessel.SetEffects(0f, 0f, Pouring ? 0.6f : 0.15f);
            if (serve != null) serve.gameObject.SetActive(false);
            kettle.gameObject.SetActive(draft.Kind == DrinkKind.Tea);
            steam.gameObject.SetActive(false);
            spoon.gameObject.SetActive(false);
            vessel.SetIce(draft.Kind == DrinkKind.Cocktail && (draft.Technique == DrinkTechnique.Build || draft.Technique == DrinkTechnique.Layer) ? draft.ice : 0);
            SyncVesselFromDraft();
            RenderLedger();
            UpdateReadout();
        }

        /// <summary>배합 기법에 맞는 제조 그릇(셰이커/믹싱 글라스/다관/잔). 바뀌면 이전 그릇 장부는 의미가 없어 비우고 배합에서 다시 만든다.</summary>
        void EnsureVessel(DrinkRecipe r)
        {
            string want = VesselFor(r);
            if (vessel != null && vesselId == want) return;
            if (vessel != null) Destroy(vessel.gameObject);
            vesselId = want;
            vessel = DrinkVesselView.Create(scene, want, VesselRest, CapacityFor(want), 4);
            vessel.transform.SetSiblingIndex(0); // 장면 맨 뒤(잎·줄기·지거가 그 앞)
            Placeholders |= vessel.Placeholder;
            vessel.Rect.anchoredPosition = StandOnTable(vessel, VesselRest.x);
            SetupLid();
            Ledger.Flush(LedgerVessel);
            Ledger[LedgerVessel].Clear();
        }

        // ───── 제조대 카메라(73 사용자: 공정 진행 방향을 따라 작업 지점을 가운데, 완성 잔은 오른쪽으로 이동해 가운데) ─────
        float camX, camVel, craftFocus = VesselRest.x;
        bool camSnap = true;
        /// <summary>팬 부드러움(SmoothDamp 시간 상수). 0.18이면 약 0.5~0.7초에 자리 잡는다.</summary>
        public const float CameraSmooth = 0.18f;
        public float CameraX => camX;
        public float CameraTarget => FocusTarget();

        /// <summary>
        /// 지금 작업의 가운데(장면 좌표 x): 편집 중 병→지거/다하 = 둘의 가운데, 옮겨 붓기 = 지거/다하와 제조 그릇의 가운데,
        /// 제조 모션 = 단계별(그릇 / 그릇→완성 잔 가운데), 완성 = 완성 잔. 장면만 움직이고 벽·상판·정보띠는 고정.
        /// </summary>
        float FocusTarget()
        {
            if (showingFinal) { var f = FinalVessel; return f != null ? f.Rect.anchoredPosition.x : VesselRest.x; }
            if (sequence != null) return craftFocus;
            if (phase != CyclePhase.None && phase != CyclePhase.BottleReturn) return (ToolRest.x + VesselRest.x) * 0.5f;
            return (BottleRest.x + ToolRest.x) * 0.5f;
        }

        void UpdateCamera(float dt)
        {
            if (scene == null) return;
            float target = FocusTarget();
            if (camSnap || skipRequested) { camX = target; camVel = 0f; camSnap = false; }
            else camX = Mathf.SmoothDamp(camX, target, ref camVel, CameraSmooth, Mathf.Infinity, Mathf.Max(0f, dt));
            scene.anchoredPosition = new Vector2(-Mathf.Round(camX), 0f); // 화면 픽셀 정렬
            // 병 잔량판은 병 아래가 기본이지만 카메라가 옮겨 가도 보이는 영역 안에서 잘리지 않게 붙든다.
            if (sourceLabel != null)
            {
                var lr = sourceLabel.rectTransform;
                float half = (root.rect.width > 0f ? root.rect.width : 800f) * 0.5f, w = lr.sizeDelta.x * 0.5f + 12f;
                float x = Mathf.Clamp(BottleRest.x, camX - half + w, camX + half - w);
                lr.anchoredPosition = new Vector2(x, lr.anchoredPosition.y);
            }
        }

        /// <summary>시험·QA: 카메라를 지금 목표로 바로.</summary>
        public void SnapCameraForTests() => camSnap = true;

        // ───── 다관 뚜껑(73 사용자: 손 없이 열기 → 열린 입구로 재료·물 → 닫기 → 우리기) ─────
        Image lid;
        float lidT, lidLinger;
        bool craftLidOpen;
        public const float LidOpenSeconds = 0.45f, LidCloseSeconds = 0.4f;
        /// <summary>뚜껑 위치(아트 px, 몸체 기준): 들어 올림 높이와 다관 오른쪽 탁자에 내려놓은 자리.</summary>
        static readonly Vector2 LidLift = new Vector2(0f, 20f), LidAside = new Vector2(44f, -30f);
        /// <summary>0 = 닫힘, 1 = 옆에 내려놓음(입구 열림).</summary>
        public float LidOpen01 => lid != null ? lidT : -1f;
        /// <summary>제조대 위 단계 문구(QA·시험).</summary>
        public string FinishText => finishLabel != null ? finishLabel.text : "";
        bool HasLid => lid != null;
        bool LidReady => lid == null || lidT >= 1f;
        bool LidClosed => lid == null || lidT <= 0f;

        /// <summary>
        /// 분리 아트가 있으면 다관 = 열린 몸체 + 같은 캔버스의 뚜껑(겹치면 원래 다관과 같은 픽셀). 물리 마스크·용량은 원래 dl_teapot 그대로.
        /// 닫혀 있으면 뚜껑은 몸체의 자식이라 따르기 회전에도 함께 움직인다.
        /// </summary>
        void SetupLid()
        {
            lid = null; lidT = 0f; lidLinger = 0f; craftLidOpen = false;
            if (vesselId != "dl_teapot") return;
            var body = DrinkArt.Sprite("dl_teapot_open_body");
            var lidArt = DrinkArt.Sprite("dl_teapot_lid");
            if (body == null || lidArt == null) return;
            vessel.Glass.sprite = body;
            var rt = UiFactory.Stretch("Lid", vessel.transform);
            lid = rt.gameObject.AddComponent<Image>();
            lid.sprite = lidArt;
            lid.raycastTarget = false;
            PoseLid();
        }

        /// <summary>편집 중 붓기 주기(다하·지거 → 다관) 또는 제조 중 물 붓기 동안 열고, 끝나면 잠시 뒤 닫는다.</summary>
        void UpdateLid(float dt)
        {
            if (lid == null) return;
            bool want = craftLidOpen || (sequence == null && phase != CyclePhase.None);
            if (want) lidLinger = 0.35f; else lidLinger -= dt; // 연달아 넣을 때 여닫기를 반복하지 않게
            float goal = want || lidLinger > 0f ? 1f : 0f;
            if (skipRequested && sequence != null) lidT = goal;
            else lidT = Mathf.MoveTowards(lidT, goal, dt / (goal > lidT ? LidOpenSeconds : LidCloseSeconds));
            PoseLid();
        }

        /// <summary>들기(앞 40%: 수직) → 옆으로 호를 그리며 내려놓기. 닫기는 같은 경로를 거꾸로.</summary>
        void PoseLid()
        {
            if (lid == null) return;
            Vector2 p;
            if (lidT <= 0.4f) p = LidLift * Ease(lidT / 0.4f);
            else
            {
                float u = Ease((lidT - 0.4f) / 0.6f);
                p = Vector2.Lerp(LidLift, LidAside, u) + new Vector2(0f, 10f * Mathf.Sin(Mathf.PI * u));
            }
            float k = vessel.Rect.rect.width / (vessel.Glass.sprite != null ? vessel.Glass.sprite.rect.width : 64f);
            lid.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x) * k, Mathf.Round(p.y) * k); // 아트 픽셀 격자
            lid.transform.SetAsLastSibling();
        }

        IEnumerator LidTo(bool open)
        {
            craftLidOpen = open;
            if (open) lidLinger = 0f;
            float s0 = Time.realtimeSinceStartup;
            while (HasLid && !skipRequested && (open ? !LidReady : !LidClosed) && Time.realtimeSinceStartup - s0 < 2f) yield return null;
            if (!open) lidLinger = 0f;
        }

        /// <summary>
        /// 장부의 제조 그릇이 배합과 다르면(직접 입력·빼기·레시피 불러오기 = 명시적 조작) 배합에서 다시 만든다.
        /// 옮겨 붓는 중이거나 공중 덩어리가 그릇으로 가는 중에는 하지 않는다(그 사이 차이는 이동 중인 양이다).
        /// </summary>
        void SyncVesselFromDraft()
        {
            if (showingFinal || phase != CyclePhase.None || sequence != null || Ledger.HasInFlight(LedgerVessel)) return;
            var v = Ledger[LedgerVessel];
            bool leavesMatch = true;
            if (draft.Kind == DrinkKind.Tea)
            {
                double draftLeaf = 0;
                foreach (var s in draft.steps)
                {
                    var d = s == null ? null : DrinkIngredients.Get(s.id);
                    if (d == null || d.form != IngredientForm.Leaf || s.amount <= 0) continue;
                    draftLeaf += s.amount / 10.0;
                    if (Math.Abs(v.Leaf(d.id) - s.amount / 10.0) > 0.005) leavesMatch = false;
                }
                if (Math.Abs(v.LeafG - draftLeaf) > 0.005) leavesMatch = false;
            }
            else if (v.LeafG > 0) leavesMatch = false;
            if (DrinkPhysicsSim.Matches(v.Liquid, draft) && leavesMatch && v.DissolvedG <= 0) return;
            v.Clear();
            v.Liquid = DrinkPhysicsSim.Build(draft);
            if (draft.Kind == DrinkKind.Tea)
                foreach (var s in draft.steps)
                {
                    var d = s == null ? null : DrinkIngredients.Get(s.id);
                    if (d != null && d.form == IngredientForm.Leaf && s.amount > 0) v.AddLeaf(d.id, s.amount / 10.0);
                }
        }

        /// <summary>장부 그릇 → 화면 층(찻물은 우림 정도만큼 차 색).</summary>
        List<DrinkLayer> LayersOf(string ledgerName, DrinkRecipe r)
        {
            var s = Ledger[ledgerName];
            if (!smoothers.TryGetValue(ledgerName, out var sm)) smoothers[ledgerName] = sm = new DrinkPhysicsSim.DisplaySmoother();
            // 1차원 침강의 셀 교대가 프레임마다 깜박이지 않게 화면은 시간 평균 기둥(성분 합계는 장부와 정확히 같음)
            return DrinkPhysicsSim.Layers(sm.Get(s.Liquid, Time.unscaledTimeAsDouble, Time.frameCount), r, s.TeaStrength);
        }

        readonly Dictionary<string, DrinkPhysicsSim.DisplaySmoother> smoothers = new Dictionary<string, DrinkPhysicsSim.DisplaySmoother>();

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

        /// <summary>찻잎 더미(그릇 안). 양은 장부의 건조 잎 g.</summary>
        void ShowLeafHeap(Image heap, DrinkVesselView inVessel, double leafG, string leafId)
        {
            bool on = leafG > 0.0005 && Mathf.Abs(inVessel.TiltDegrees) < 12f;
            heap.gameObject.SetActive(on);
            if (!on) return;
            var frames = leafId != null ? DrinkArt.Frames("dl_leaf_heap_" + leafId) : new Sprite[0];
            // 찻잎 양이 많을수록 더미가 커진다(5g에서 최대). 프레임이 3장이면 작은/중간/큰 더미.
            float t = Mathf.Clamp01((float)leafG * 10f / DrinkRules.TeaLeafMax);
            if (frames.Length > 0) heap.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))];
            var def = DrinkIngredients.Get(leafId);
            heap.color = frames.Length > 0 ? Color.white : (def != null ? def.color : Color.gray);
            heap.rectTransform.sizeDelta = frames.Length > 0 ? new Vector2(frames[0].rect.width * 4, frames[0].rect.height * 4) : new Vector2(120f, 40f);
            heap.rectTransform.localScale = new Vector3(0.55f + 0.45f * t, 0.35f + 0.65f * t, 1f);
            heap.rectTransform.anchoredPosition = inVessel.PointInParent(new Vector2(0.45f, DrinkArt.LiquidRect(inVessel.AssetId).yMin));
            heap.transform.SetAsLastSibling();
        }

        static string FirstLeafId(DrinkVesselState s)
        {
            string best = null; double g = 0;
            foreach (var kv in s.LeavesG) if (kv.Value > g) { g = kv.Value; best = kv.Key; }
            return best;
        }

        /// <summary>왼쪽 목록에서 고른 재료 → 병/통과 계량 도구 교체.</summary>
        public void Select(DrinkIngredientDef def)
        {
            StopPour();
            FinishTransferVisual();
            Tilt = 0f; targetTilt = 0f;
            current = def;
            if (def != null) showingFinal = false;
            bool leaf = def != null && def.form == IngredientForm.Leaf;
            if (srcBottle != null) { Destroy(srcBottle.gameObject); srcBottle = null; srcCap = null; }
            string srcId = def != null && !leaf ? "dl_srcbottle_" + def.id : null;
            bool vesselBottle = srcId != null && DrinkArt.Has(srcId) && DrinkArt.Mask(srcId) != null;
            bottle.gameObject.SetActive(def != null && !vesselBottle);
            bottleRt = bottle.rectTransform;
            jigger.gameObject.SetActive(def != null && !leaf);
            chaha.gameObject.SetActive(def != null && leaf);
            // 새 병/통: 장부의 원천 = 지금 더 넣을 수 있는 가상 재고(창고 − 이미 배합에 든 양). 계량 도구는 비어 있다.
            Ledger.Flush(LedgerMeasure);
            Ledger[LedgerBottle].Clear();
            Ledger[LedgerMeasure].Clear();
            bottleFall.Cut();
            if (def == null) { toolLabel.text = "왼쪽에서 재료를 고르세요"; UpdateSourceLabel(); UpdateReadout(); return; }
            int stock = Owned(def);
            int source = stock == int.MaxValue ? allowed(def) : Mathf.Max(0, stock - draft.AmountOf(def.id));
            if (vesselBottle) source = Mathf.Min(source, Mathf.FloorToInt(BottleCapacityMl(def.id) * 10f));
            if (leaf) Ledger[LedgerBottle].AddLeaf(def.id, source / 10.0);
            else if (source > 0) Ledger[LedgerBottle].Liquid.Pour(DrinkPhysicsSim.Part(def.id, source / 10.0), 1.0);
            if (vesselBottle)
            {
                // 합성: 장부 잔량 액체 → 물리 내부 마스크(라벨 뒤 포함) → 열린 전경(라벨이 앞을 가림) → 뚜껑(세워 둔 동안만).
                srcBottle = DrinkVesselView.Create(scene, srcId, BottleRest, BottleCapacityMl(def.id));
                srcBottle.transform.SetSiblingIndex(bottle.transform.GetSiblingIndex());
                bottleRt = srcBottle.Rect;
                DrinkVesselView.SetPivotKeepPosition(bottleRt, DrinkArt.Anchor(srcId, "grip"));
                bottleRt.anchoredPosition = BottleRest;
                var capRt = UiFactory.Stretch("Cap", bottleRt);
                srcCap = capRt.gameObject.AddComponent<Image>();
                srcCap.sprite = DrinkArt.Sprite(srcId + "_cap");
                srcCap.raycastTarget = false;
                srcCap.enabled = srcCap.sprite != null;
                AddGrabArea(bottleRt);
                onsetVolume = -1;
                UpdateOnset();
            }
            else
            {
                var sprite = DrinkArt.SpriteOrPlaceholder(def.bottleId, out bool ph);
                Placeholders |= ph;
                bottle.sprite = sprite;
                bottle.color = Color.white;
                bottleRt.sizeDelta = new Vector2(sprite.rect.width * 4, sprite.rect.height * 4);
                bottleRt.pivot = DrinkArt.Anchor(def.bottleId, "grip");
                bottleRt.anchoredPosition = BottleRest;
            }
            bottleRt.localRotation = Quaternion.identity;
            toolLabel.text = leaf ? $"{def.name} — 통을 끌어 다하에 덜기(3.0g씩 다관으로)" : $"{def.name} — 병을 끌어 지거에 붓기(45.0mL씩 나눠 붓기)";
            jigger.SetEmpty();
            UpdateMeasure();
            UpdateSourceLabel();
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
            targetTilt = Mathf.Clamp(d * 0.6f, 0f, TiltMax); // 실제 병 자세는 Update에서 부드럽게 따라간다
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
            var lipN = DrinkArt.Anchor(BottleArt, "pourLip");
            var lipAtRest = DrinkVesselView.ToParentAnchored(bottleRt, lipN);
            var mouth = MeasureMouth();
            var target = mouth + new Vector2(0f, LipClearance);
            // 액면이 입구에 닿기 전에 입구가 받는 입구 위에 와 있어야 한다.
            float reach = srcBottle != null ? Mathf.Clamp(bottleOnset - 8f, 40f, TiltMax) : TiltStart;
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Tilt / reach));
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

        /// <summary>즉시 멈춤: 지거/다하에 담긴 양만 배합에 넣는다(재고 미차감). 이미 입구를 떠난 줄기 꼬리는 마저 떨어져 지거에 들어간다.</summary>
        public void StopPour()
        {
            if (!Pouring && InMeasure == 0) return;
            Pouring = false;
            targetTilt = 0f;
            bottleFall.Stop(); // 흐름은 즉시 멈춘다(병은 Update에서 부드럽게 제자리로)
            Geometry = new PourGeometry { lip = Geometry.lip, mouth = Geometry.mouth, pivot = Geometry.pivot, rotation = Geometry.rotation, streamEnd = Geometry.lip, flowing = false };
            CommitMeasure();
            if (phase == CyclePhase.None) ReturnBottleIfIdle();
            UpdateReadout();
        }

        /// <summary>현재 기울기에서 0.1 단위/초 속도. 30° 미만은 0, 그 위는 제곱 곡선(작게 끌면 매우 느림), 점성이 높으면 느리게.</summary>
        public static float Rate(float tilt, DrinkIngredientDef def) => Rate(tilt, def, TiltStart);

        /// <summary>흐름 시작 각도 onset에서 50° 더 기울이면 최대 속도(제곱 곡선).</summary>
        public static float Rate(float tilt, DrinkIngredientDef def, float onset)
        {
            if (def == null || tilt <= onset) return 0f;
            float t = Mathf.Clamp01((tilt - onset) / 50f);
            float max = def.form == IngredientForm.Leaf ? LeafMaxRate : LiquidMaxRate;
            return t * t * max * (1f - 0.6f * def.viscosity);
        }

        int MeasureCapacity => current != null && current.form == IngredientForm.Leaf ? DrinkRules.ChahaCapacity : DrinkRules.JiggerCapacity;

        /// <summary>계량 도구 안 받는 면(액면 또는 바닥)의 높이(제조대 좌표).</summary>
        float MeasureSurfaceY()
        {
            if (current != null && current.form == IngredientForm.Leaf)
                return DrinkVesselView.ToParentAnchored(chaha.rectTransform, new Vector2(0.5f, DrinkArt.LiquidRect("dl_chaha").yMin)).y;
            return jigger.PointInParent(new Vector2(0.5f, Mathf.Max(jigger.SurfaceY, DrinkArt.LiquidRect("dl_jigger").yMin))).y;
        }

        /// <summary>붓기 한 단계(테스트·Update 공용). 정수 0.1 단위만 더한다. 나온 양은 장부에서 병 → 공중 → (낙하 후) 계량 도구.</summary>
        public void Step(float dt)
        {
            if (!Pouring || current == null) return;
            if (phase != CyclePhase.None) { bottleFall.Stop(); return; } // 옮겨 붓는 동안 병→지거 흐름 없음
            double spillTenths = double.MaxValue;
            float rate;
            if (srcBottle != null)
            {
                // 액면이 실제 입구를 넘는 부피만 흐른다(잔량이 적으면 더 기울여야 한다).
                double have = Ledger[LedgerBottle].LiquidMl;
                double spill = have > 0 ? DrinkFluidPhysics.SolveTilt(srcBottle.Profile, new[] { have }, Tilt).SpillMl : 0;
                spillTenths = spill * 10.0 + 1e-6;
                rate = LipAboveMouth && spill > 1e-4 ? Rate(Tilt, current, bottleOnset) : 0f;
                if (have < 0.05 && Pouring)
                {
                    StopReason = Owned(current) <= draft.AmountOf(current.id) + InMeasure ? "보유량을 모두 넣었습니다." : "병이 비었습니다. 재료를 다시 고르면 창고에서 채웁니다.";
                    StopPour();
                    return;
                }
            }
            else rate = LipAboveMouth ? Rate(Tilt, current) : 0f; // 입구가 받는 입구 위가 아니면 흐르지 않는다
            if (rate <= 0f) { bottleFall.Stop(); UpdateReadout(); return; }
            UpdateStream(rate);
            accumulator += rate * dt;
            int add = Mathf.FloorToInt(accumulator);
            if (add <= 0) { UpdateReadout(); return; }
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
            if (spillTenths < add) { add = (int)Math.Floor(spillTenths); accumulator = 0f; }
            if (add <= 0) { UpdateReadout(); return; }
            InMeasure += add;
            PouredThisGesture += add;
            float fall = (float)DrinkTransferLedger.FallSeconds(Geometry.lip.y - MeasureSurfaceY(), GravityPx);
            if (current.form == IngredientForm.Leaf) Ledger.EmitLeaves(LedgerBottle, LedgerMeasure, current.id, add / 10.0, fall);
            else Ledger.Emit(LedgerBottle, LedgerMeasure, add / 10.0, fall, 0.3);
            MeasureChanged?.Invoke();
            if (InMeasure >= MeasureCapacity)
            {
                // 계량 도구가 가득: 그릇에 옮겨 붓고(누적 표시) 이어서 붓는다.
                bottleFall.Stop();
                CommitMeasure();
                MeasureDumps++;
                UpdateReadout();
                return;
            }
            UpdateReadout();
        }

        Func<DrinkIngredientDef, int> ownedLookup;
        public void SetOwnedLookup(Func<DrinkIngredientDef, int> f) => ownedLookup = f;
        int Owned(DrinkIngredientDef d) => ownedLookup != null ? ownedLookup(d) : int.MaxValue;

        void CommitMeasure()
        {
            if (current == null || InMeasure <= 0) { InMeasure = 0; UpdateMeasure(); return; }
            if (phase != CyclePhase.None) FinishTransferVisual(); // (보통 일어나지 않음) 앞 주기는 즉시 마무리 — 데이터는 이미 반영됨
            dumpStream.color = StreamTint(current.form == IngredientForm.Leaf ? current.color : DrinkCalculator.IngredientLook(current));
            transferFromPending = draft.AmountOf(current.id);
            // 배합(데이터)은 즉시 정확히 반영 — 화면은 장부대로 지거가 기울며 흐른 만큼 비고 그릇이 받은 만큼 찬다.
            draft.SetAmount(current.id, draft.AmountOf(current.id) + InMeasure);
            InMeasure = 0;
            StartCycle();
            changed?.Invoke();
        }

        /// <summary>계량 도구 표시 = 장부(병에서 떨어져 도착한 양만).</summary>
        void UpdateMeasure()
        {
            var m = Ledger[LedgerMeasure];
            var def = current ?? transferDef;
            if (def != null && def.form == IngredientForm.Leaf)
            {
                double g = m.LeafG;
                float t = (float)(g * 10.0 / DrinkRules.ChahaCapacity);
                chahaHeap.gameObject.SetActive(g > 0.0005);
                var frames = DrinkArt.Frames("dl_leaf_heap_" + def.id);
                if (frames.Length > 0) { chahaHeap.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(Mathf.Clamp01(t) * frames.Length))]; chahaHeap.color = Color.white; }
                else chahaHeap.color = def.color;
                chahaHeap.rectTransform.localScale = new Vector3(0.4f + 0.6f * t, 0.3f + 0.7f * t, 1f);
            }
            else
            {
                jigger.SetLayers(LayersOf(LedgerMeasure, draft));
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

        /// <summary>줄기 한 토막 = top에서 중력 방향(수직 아래)으로 bottom까지. 위쪽 피벗(0.5,1), 회전 없음, 받는 그릇 테두리 뒤.</summary>
        static void DrawFall(Image img, Vector2 top, float bottomY, float width, Color color, Transform behind)
        {
            img.gameObject.SetActive(true);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = top;
            rt.sizeDelta = new Vector2(width, Mathf.Max(2f, top.y - bottomY));
            img.sprite = DrinkVesselView.White;
            img.color = color;
            if (behind != null && behind.parent == img.transform.parent)
            {
                int target = behind.GetSiblingIndex();
                if (img.transform.GetSiblingIndex() > target) img.transform.SetSiblingIndex(target);
            }
        }

        /// <summary>
        /// 입구에서 받는 면까지 떨어지는 줄기. 흐름이 시작되면 머리가 중력으로 떨어져 내려가고(순간 전체 길이 금지),
        /// 멈추면 입구에서 떨어진 꼬리가 같은 중력으로 떨어져 사라진다. 장부 덩어리의 낙하 시간과 같은 가속도를 쓴다.
        /// </summary>
        sealed class FallStream
        {
            readonly Image img;
            bool active;
            float x, top, head, headV, tailV, endY, width;
            Color color;
            Transform behind;
            public bool Emitting { get; private set; }
            public Vector2 Top => new Vector2(x, top);
            public float Head => head;
            public FallStream(Image img) { this.img = img; }

            public void Emit(Vector2 lip, float endY, float width, Color color, Transform behind)
            {
                if (!active) { active = true; head = lip.y; headV = 0f; }
                Emitting = true;
                x = lip.x; top = lip.y; tailV = 0f;
                this.endY = endY; this.width = width; this.color = color; this.behind = behind;
            }

            public void Stop() => Emitting = false;
            public void Cut() { Emitting = false; active = false; img.gameObject.SetActive(false); }

            public void Tick(float dt)
            {
                if (!active) { if (img.gameObject.activeSelf) img.gameObject.SetActive(false); return; }
                if (head > endY) { headV += GravityPx * dt; head = Mathf.Max(endY, head - headV * dt); }
                else head = endY;
                if (!Emitting)
                {
                    tailV += GravityPx * dt;
                    top -= tailV * dt;
                    if (top <= endY + 1f) { Cut(); return; }
                }
                if (top - head < 1f && !Emitting) { img.gameObject.SetActive(false); return; } // 흐르는 중이면 입구의 짧은 첫 토막부터 보인다
                DrawFall(img, new Vector2(x, top), head, width, color, behind);
            }
        }

        void UpdateStream(float rate)
        {
            if (current == null) return;
            var g = Geometry;
            if (current.form == IngredientForm.Leaf)
            {
                // 찻잎은 액체 줄기가 아니라 잎 입자가 떨어진다.
                bottleFall.Stop();
                SpawnFallingLeaf(g.lip, new Vector2(g.lip.x, MeasureSurfaceY()), current);
                return;
            }
            float surfaceY = MeasureSurfaceY();
            float width = Mathf.Lerp(6f, 14f, Mathf.Sqrt(Mathf.Clamp01(rate / LiquidMaxRate)));
            bottleFall.Emit(g.lip, surfaceY, width, StreamTint(DrinkCalculator.IngredientLook(current)), jigger.transform);
            bottleFall.Tick(0f);
            g.streamEnd = new Vector2(g.lip.x, surfaceY);
            g.flowing = true;
            Geometry = g;
        }

        float leafTimer;
        void SpawnFallingLeaf(Vector2 from, Vector2 to, DrinkIngredientDef def)
        {
            leafTimer -= Time.unscaledDeltaTime;
            if (leafTimer > 0f) return;
            leafTimer = 0.06f;
            var frames = DrinkArt.Frames("dl_leaf_" + def.id);
            var img = AddImage("Leaf", leafRoot, frames.Length > 0 ? frames[UnityEngine.Random.Range(0, frames.Length)] : DrinkVesselView.White, new Vector2(24f, 24f));
            if (frames.Length == 0) img.color = def.color;
            img.gameObject.AddComponent<DrinkFallingBit>().Init(from, to, (float)DrinkTransferLedger.FallSeconds(from.y - to.y, GravityPx));
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
            UpdateLid(dt);
            UpdateCamera(dt);
            Ledger.Advance(dt);
            bottleFall.Tick(dt);
            toolFall.Tick(dt);
            if (sequence == null)
            {
                // 열린 그릇의 액체는 가만히 있어도 밀도에 따라 계속 가라앉고 경계가 퍼진다(71 §4). 양은 바뀌지 않는다.
                var settling = Ledger[LedgerVessel].Liquid;
                if (!settling.IsEmpty) settling.Step(dt);
                if (showingFinal && Signature(draft) != finalSignature) showingFinal = false; // 완성 뒤 배합을 고치면 다시 편집 화면
                if (phase == CyclePhase.None) SyncVesselFromDraft();
                RenderLedger();
            }
            UpdatePlates();
        }

        /// <summary>병/통에 남은 가상 재고(장부) — 붓는 동안 줄어든다.</summary>
        void UpdateSourceLabel()
        {
            if (srcBottle != null)
            {
                srcBottle.SetLayers(LayersOf(LedgerBottle, draft));
                if (srcCap != null) srcCap.enabled = srcCap.sprite != null && Tilt < 0.5f && !Pouring;
                UpdateOnset();
            }
            if (sourceLabel == null) return;
            if (current == null) { sourceLabel.text = ""; return; }
            var b = Ledger[LedgerBottle];
            bool leaf = current.form == IngredientForm.Leaf;
            int tenths = Mathf.RoundToInt((float)((leaf ? b.LeafG : b.LiquidMl) * 10.0));
            sourceLabel.text = $"{(leaf ? "통" : "병")}에 남은 양 {DrinkRules.Format(tenths)}{current.Unit}";
        }

        /// <summary>편집 중 화면 = 장부(계량 도구·제조 그릇·찻잎 더미).</summary>
        void RenderLedger()
        {
            if (vessel == null) return;
            UpdateMeasure();
            UpdateSourceLabel();
            vessel.SetLayers(LayersOf(LedgerVessel, draft));
            var v = Ledger[LedgerVessel];
            if (draft.Kind == DrinkKind.Tea || v.LeafG > 0) ShowLeafHeap(leafHeap, vessel, v.LeafG, FirstLeafId(v));
            else leafHeap.gameObject.SetActive(false);
        }

        // ───── 옮겨 붓기 주기: 병 정지·복귀 → 계량 도구 접근(이동 후 기울임) → 액면이 입구를 넘는 만큼 흐름 → 복귀 ─────
        public enum CyclePhase { None, BottleReturn, ToolApproach, ToolPour, ToolReturn }
        CyclePhase phase;
        float phaseT, phaseDur, bottleFrom;
        public CyclePhase Phase => phase;
        public const float BottleReturnSeconds = 0.45f, ToolMoveSeconds = 0.45f, ToolLipClearance = 26f;
        /// <summary>계량 도구 붓기 최대 기울기(지거가 다 비도록 계속 기울인다).</summary>
        public const float ToolPourTilt = 105f, ToolPourTiltMax = 140f;
        /// <summary>계량 도구의 제자리(손잡이 피벗 위치). Build에서 피벗을 grip으로 옮긴 뒤 기록한다.</summary>
        Vector2 jiggerRest, chahaRest, kettleRest;
        public static readonly Vector2 VesselRest = new Vector2(150f, -230f);

        /// <summary>상판 윗면(제조대 가운데 기준 y): 아래 정보띠 + 상판 두께 위.</summary>
        float TableTopY => -(root != null && root.rect.height > 0f ? root.rect.height : 690f) * 0.5f + HudHeight + TableHeight;

        /// <summary>바닥 가운데 피벗 그릇을 x에 두되, 스프라이트의 실제 보이는 바닥(아래 투명 행 제외)이 상판 윗면에 닿게 한다(떠 보이지 않게).</summary>
        Vector2 StandOnTable(DrinkVesselView v, float x)
        {
            float artH = v.Glass != null && v.Glass.sprite != null ? v.Glass.sprite.rect.height : 0f;
            float scale = artH > 0f ? v.Rect.rect.height / artH : 4f;
            return new Vector2(x, TableTopY - DrinkArt.AlphaBottom(v.AssetId) * scale);
        }
        string transferId;
        int transferFrom, transferAdded;
        DrinkIngredientDef transferDef;
        double transferTotal, transferEmitted;
        float toolTilt, toolStartTilt, toolMove;
        /// <summary>옮겨 붓기 주기 진행 중(병 복귀부터 계량 도구 복귀·마지막 줄기 도착까지).</summary>
        public bool Transferring => phase != CyclePhase.None;
        /// <summary>옮겨 붓는 중인 재료 id(아니면 null).</summary>
        public string TransferId => phase != CyclePhase.None ? transferId : null;
        /// <summary>흐름 진행도 0~1 = 계량 도구에서 흘러나간 양 / 옮길 양. 받는 그릇은 도착한 만큼만 찬다.</summary>
        public float TransferProgress => phase == CyclePhase.None ? 1f : transferTotal > 0 ? (float)(transferEmitted / transferTotal) : 0f;
        public float ToolTilt => toolTilt;

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
            transferEmitted = 0;
            transferTotal = transferAdded / 10.0;
            bottleFrom = Tilt;
            toolTilt = 0f; toolMove = 0f;
            SetPhase(Tilt > 0.5f || Ledger.HasInFlight(LedgerMeasure) ? CyclePhase.BottleReturn : CyclePhase.ToolApproach);
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
            if (p == CyclePhase.ToolApproach)
                toolStartTilt = transferDef.form == IngredientForm.Liquid ? Mathf.Min(70f, SpillOnsetAngle(jigger, Ledger[LedgerMeasure].LiquidMl) - 4f) : 35f;
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>그 양의 액면이 입구 높이에 처음 닿는 기울기(도). 접근 중에는 이 각도 아래로만 기울여 흘리지 않는다.</summary>
        static float SpillOnsetAngle(DrinkVesselView v, double volumeMl)
        {
            if (v == null || v.Profile == null || volumeMl <= 0) return 90f;
            bool Reached(float a)
            {
                var s = DrinkFluidPhysics.SolveTilt(v.Profile, new[] { volumeMl }, a);
                return s.LipReached || s.SpillMl > 1e-6;
            }
            for (float a = 0f; a <= 178f; a += 3f)
            {
                if (!Reached(a)) continue;
                float lo = Mathf.Max(0f, a - 3f), hi = a;
                for (int i = 0; i < 6; i++) { float mid = (lo + hi) * 0.5f; if (Reached(mid)) hi = mid; else lo = mid; }
                return hi;
            }
            return 178f;
        }

        /// <summary>
        /// 계량 도구 자세: move(0=제자리, 1=받는 입구 위)와 tilt(도). 손잡이 피벗으로 회전하고, move=1이면 회전된 실제 입구가 받는 입구 위에 온다.
        /// 기운 동안에도 내용물은 숨기지 않는다 — 투명(단면) 그릇은 SolveTilt로 수평 액면을 그린다.
        /// </summary>
        void PoseTool(float move, float tilt)
        {
            var tool = Tool;
            toolMove = move; toolTilt = tilt;
            tool.localRotation = Quaternion.Euler(0f, 0f, -tilt);
            tool.anchoredPosition = ToolRest;
            var lipN = DrinkArt.Anchor(ToolArt, "pourLip");
            var lipAtRest = DrinkVesselView.ToParentAnchored(tool, lipN);
            var target = ReceiverMouth() + new Vector2(0f, ToolLipClearance);
            tool.anchoredPosition = ToolRest + (target - lipAtRest) * move;
        }

        Vector2 ReceiverMouth() => vessel.PointInParent(DrinkArt.Anchor(HasLid ? "dl_teapot_open_body" : vessel.AssetId, "mouth"));

        float ReceiverSurfaceY(DrinkVesselView v) => v.PointInParent(new Vector2(0.5f, Mathf.Max(v.SurfaceY, DrinkArt.LiquidRect(v.AssetId).yMin))).y;

        void UpdateCycle(float dt)
        {
            if (phase == CyclePhase.None || vessel == null) return;
            if (sequence != null) { FinishTransferVisual(); return; }
            // 편집으로 그 재료 양이 바뀌었으면(빼기·직접 입력 = 명시적 조작) 표시를 끝내고 실제 배합을 그린다.
            if (draft.AmountOf(transferId) != transferFrom + transferAdded) { FinishTransferVisual(); return; }
            phaseT += dt;
            float t = phaseDur <= 0f ? 1f : Mathf.Clamp01(phaseT / phaseDur);
            var m = Ledger[LedgerMeasure];
            switch (phase)
            {
                case CyclePhase.BottleReturn:
                    ApplyTilt(Mathf.Lerp(bottleFrom, 0f, Ease(t)));
                    // 병에서 이미 떨어지던 줄기 꼬리가 지거에 다 닿은 뒤 지거가 움직인다.
                    if (t >= 1f && !Ledger.HasInFlight(LedgerMeasure)) SetPhase(CyclePhase.ToolApproach);
                    break;
                case CyclePhase.ToolApproach:
                {
                    // 먼저 옮기고(이동이 앞서고) 흘리지 않는 각도까지만 기울인다.
                    float move = Ease(Mathf.Clamp01(t * 1.25f));
                    float tilt = toolStartTilt * Ease(Mathf.Clamp01((t - 0.3f) / 0.7f));
                    PoseTool(move, tilt);
                    if (t >= 1f && LidReady) SetPhase(CyclePhase.ToolPour); // 다관은 뚜껑을 다 열고서 붓는다

                    break;
                }
                case CyclePhase.ToolPour:
                {
                    float endTilt = transferDef.form == IngredientForm.Liquid ? ToolPourTiltMax : 110f;
                    float tilt = Mathf.Lerp(toolStartTilt, endTilt, Ease(t));
                    PoseTool(1f, tilt);
                    var lip = DrinkVesselView.ToParentAnchored(Tool, DrinkArt.Anchor(ToolArt, "pourLip"));
                    float surface = ReceiverSurfaceY(vessel);
                    float fall = (float)DrinkTransferLedger.FallSeconds(lip.y - surface, GravityPx);
                    double emitted = 0;
                    if (transferDef.form == IngredientForm.Liquid)
                    {
                        // 액면이 입구 높이를 넘는 부피만 흐른다(SolveTilt). 최대 유량은 양/점성에 맞춘 시간으로 정한다.
                        double have = m.LiquidMl;
                        double spill = have > 0 ? DrinkFluidPhysics.SolveTilt(jigger.Profile, new[] { have }, tilt).SpillMl : 0;
                        double maxRate = transferTotal / Mathf.Max(0.3f, phaseDur) * 2.2;
                        // 끝 기울기에 닿은 뒤 남은 방울은 같은 최대 유량으로 마저 흐른다.
                        double want = t >= 1f ? Math.Min(have, maxRate * dt) : Math.Min(spill, maxRate * dt);
                        emitted = Ledger.Emit(LedgerMeasure, LedgerVessel, want, fall, DrinkPhysicsSim.Gentleness(draft.Technique));
                        if (emitted > 1e-6)
                            toolFall.Emit(lip, surface, Mathf.Lerp(5f, 14f, Mathf.Clamp01((float)(emitted / Math.Max(1e-6, maxRate * dt)))), dumpStream.color, vessel.transform);
                        else toolFall.Stop();
                    }
                    else
                    {
                        // 찻잎: 기울기 동안 일정하게 굴러 떨어진다(입자). 다하를 다 비운다.
                        double have = m.LeafG;
                        double want = t >= 1f ? have : Math.Min(have, transferTotal * Ease(t) - transferEmitted);
                        if (want > 0)
                        {
                            emitted = Ledger.EmitLeaves(LedgerMeasure, LedgerVessel, transferDef.id, want, fall);
                            SpawnFallingLeaf(lip, new Vector2(lip.x, surface), transferDef);
                        }
                        toolFall.Stop();
                    }
                    transferEmitted += emitted;
                    TransferGeometry = new PourGeometry { lip = lip, mouth = ReceiverMouth(), streamEnd = new Vector2(lip.x, surface), pivot = Tool.pivot, rotation = Tool.localEulerAngles.z, flowing = emitted > 0 && transferDef.form == IngredientForm.Liquid };
                    bool empty = transferDef.form == IngredientForm.Liquid ? m.Liquid.IsEmpty : m.LeafG <= 1e-9;
                    if (t >= 1f && empty) { toolFall.Stop(); SetPhase(CyclePhase.ToolReturn); }
                    break;
                }
                case CyclePhase.ToolReturn:
                {
                    float tiltFrom = transferDef.form == IngredientForm.Liquid ? ToolPourTiltMax : 110f;
                    PoseTool(1f - Ease(Mathf.Clamp01((t - 0.15f) / 0.85f)), tiltFrom * (1f - Ease(Mathf.Clamp01(t * 1.4f))));
                    // 도구가 돌아온 뒤에도 마지막 줄기가 그릇에 닿을 때까지 주기를 끝내지 않는다.
                    if (t >= 1f && !Ledger.HasInFlight(LedgerVessel)) EndCycle();
                    break;
                }
            }
        }

        /// <summary>옮겨 붓기 줄기 기하(QA·시험).</summary>
        public PourGeometry TransferGeometry { get; private set; }

        void EndCycle()
        {
            phase = CyclePhase.None;
            toolFall.Stop();
            var tool = Tool;
            tool.localRotation = Quaternion.identity;
            tool.anchoredPosition = ToolRest;
            toolTilt = 0f; toolMove = 0f;
            transferEmitted = transferTotal;
            if (vessel != null) { SyncVesselFromDraft(); RenderLedger(); }
            ReturnBottleIfIdle();
        }

        void ReturnBottleIfIdle()
        {
            // 놓은 상태면 병은 Update에서 제자리(0°)로 이어서 돌아간다. 계속 누르고 있으면 그 기울기로 다시 돌아가 계량을 이어 간다.
            if (!Pouring) targetTilt = 0f;
        }

        /// <summary>명시적 조작(편집·제조 확정·도구 교체)에서만 주기를 즉시 끝낸다: 남은 계량분과 공중 덩어리를 그대로 그릇에 넣는다(양 보존).</summary>
        void FinishTransferVisual()
        {
            if (phase == CyclePhase.None) return;
            Ledger.Flush(LedgerMeasure);
            var m = Ledger[LedgerMeasure];
            if (!m.Liquid.IsEmpty) Ledger.Emit(LedgerMeasure, LedgerVessel, m.LiquidMl, 0, DrinkPhysicsSim.Gentleness(draft.Technique));
            foreach (var id in new List<string>(m.LeavesG.Keys)) Ledger.EmitLeaves(LedgerMeasure, LedgerVessel, id, m.Leaf(id), 0);
            Ledger.Flush(LedgerVessel);
            bottleFall.Cut(); toolFall.Cut();
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

        /// <summary>제조 확정 후 기법별 모션. skipMotion이면 즉시 완성 상태. done은 항상 한 번 불린다.</summary>
        public void PlayCraft(DrinkRecipe r, bool skipMotion, Action done)
        {
            StopPour();
            FinishTransferVisual();
            Select(null);
            toolLabel.text = "";
            if (sequence != null) { StopCoroutine(sequence); sequence = null; }
            skipRequested = skipMotion;
            craftFocus = VesselRest.x;
            // 확정 배합의 기법에 맞는 그릇과 장부인지 마지막으로 맞춘다(불러온 레시피를 바로 제조하는 경우 등).
            Ledger.Flush();
            EnsureVessel(r);
            vessel.transform.localRotation = Quaternion.identity;
            DrinkVesselView.SetPivotKeepPosition(vessel.Rect, new Vector2(0.5f, 0f));
            vessel.Rect.anchoredPosition = StandOnTable(vessel, VesselRest.x);
            vessel.SetIce(r.Kind == DrinkKind.Cocktail && (r.Technique == DrinkTechnique.Build || r.Technique == DrinkTechnique.Layer) ? r.ice : 0);
            SyncVesselFromDraftFor(r);
            sequence = StartCoroutine(CraftRoutine(r.Clone(), done));
        }

        void SyncVesselFromDraftFor(DrinkRecipe r)
        {
            var keep = draft;
            draft = r;
            try { SyncVesselFromDraft(); } finally { draft = keep; }
        }

        /// <summary>스터링 단계(QA): 바스푼 실제 스프라이트 끝(행 기준) 위치·좌우 극점·그릇 내부 밖 픽셀 수.</summary>
        public struct SpoonProbe
        {
            public Vector2 tip;
            public float floorY, tipAboveFloorPx, leftClearPx, rightClearPx, minX, maxX, lowestY;
            public int outsidePixels, frame;
            public float amplitude, lean;
        }
        public SpoonProbe Spoon { get; private set; }

        IEnumerator CraftRoutine(DrinkRecipe r, Action done)
        {
            var look = DrinkCalculator.Look(r);
            EnsureServe(r);
            serve.gameObject.SetActive(false);
            finishLabel.text = DrinkRules.TechniqueName(r.Technique) + "…";
            var v = Ledger[LedgerVessel];
            bool played = !skipRequested;
            switch (r.Technique)
            {
                case DrinkTechnique.Shake:
                    if (!skipRequested)
                    {
                        // 뚜껑 닫기 → 흔들기(프레임 또는 흔들림, 장부에서 실제 혼합) → 열고 잔에 따르기.
                        var open = vessel.Glass.sprite;
                        var closed = DrinkArt.Sprite("dl_shaker");
                        var shakeFrames = DrinkArt.Frames("dl_shaker_shake");
                        if (closed != null) vessel.Glass.sprite = closed;
                        vessel.SetLiquidHidden(true); // 닫힌 금속 셰이커는 불투명 — 액체를 비춰 보이지 않는다
                        float t = 0f;
                        const float dur = 1.6f;
                        while (t < dur && !skipRequested)
                        {
                            float dt = Time.unscaledDeltaTime;
                            t += dt;
                            if (!v.Liquid.IsEmpty) v.Liquid.Mix(1.0, dt * 6.0 / dur);
                            if (shakeFrames.Length > 1) vessel.Glass.sprite = shakeFrames[Mathf.FloorToInt(t * 12f) % shakeFrames.Length];
                            vessel.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 50f) * 14f);
                            vessel.Rect.anchoredPosition = StandOnTable(vessel, VesselRest.x) + new Vector2(0f, Mathf.Abs(Mathf.Sin(t * 25f)) * 24f);
                            yield return null;
                        }
                        vessel.transform.localRotation = Quaternion.identity;
                        vessel.Rect.anchoredPosition = StandOnTable(vessel, VesselRest.x);
                        vessel.Glass.sprite = open;
                        vessel.SetLiquidHidden(false);
                        vessel.SetLayers(LayersOf(LedgerVessel, r));
                        vessel.SetEffects(1f, 0f, 0.5f);
                        yield return PourInto(vessel, serve, r, true);
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
                        spoon.transform.SetAsLastSibling();
                        PoseSpoon(0f, 0f); // 림 위 대기 자세에서 시작
                        spoon.gameObject.SetActive(true);
                        float t = 0f;
                        const float insert = 0.25f, dur = 1.8f + 2f * insert;
                        while (t < dur && !skipRequested)
                        {
                            float dt = Time.unscaledDeltaTime;
                            t += dt;
                            // 젓는 동안만 장부 혼합(넣기·빼기 구간 제외, 최종과 같은 총 강도)
                            if (!v.Liquid.IsEmpty && t > insert && t < dur - insert) v.Liquid.Mix(0.6, dt * 6.0 / (dur - 2f * insert));
                            vessel.SetLayers(LayersOf(LedgerVessel, r));
                            vessel.SetEffects(0f, 1f - Mathf.Clamp01(t / 1.4f) * 0.4f, 0.3f);
                            PoseSpoon(t, Mathf.Clamp01(Mathf.Min(t, dur - t) / insert));
                            yield return null;
                        }
                        PoseSpoon(dur, 0f);
                        spoon.gameObject.SetActive(false);
                        vessel.SetEffects(0f, 0.4f, 0.2f);
                        yield return PourInto(vessel, serve, r, false);
                    }
                    break;
                case DrinkTechnique.Build:
                case DrinkTechnique.Layer:
                    if (!skipRequested)
                    {
                        // 마실 잔에 이미 부은 그대로: 밀도에 따라 가라앉고 경계가 퍼진다(레이어링은 천천히 쌓아 경계가 남는다).
                        serve.gameObject.SetActive(false);
                        float t = 0f;
                        while (t < 1.8f && !skipRequested)
                        {
                            float dt = Time.unscaledDeltaTime;
                            t += dt;
                            if (!v.Liquid.IsEmpty) v.Liquid.Step(dt * 3.0 / 1.8);
                            vessel.SetLayers(LayersOf(LedgerVessel, r));
                            vessel.SetEffects(0f, r.Technique == DrinkTechnique.Build ? 0.15f : 0f, 0.2f);
                            yield return null;
                        }
                    }
                    break;
                case DrinkTechnique.Steep:
                    if (!skipRequested)
                    {
                        // 물 붓기: 물 용기를 다관 입구 위로 옮겨 손잡이 축으로 기울이고(주둥이 → 다관), 장부에서 물이 흐른 만큼 다관에 닿는다.
                        var k = Ledger[LedgerKettle];
                        k.Clear();
                        k.Liquid.Pour(DrinkPhysicsSim.Part(DrinkPhysicsSim.WaterId, r.water / 10.0), 1.0);
                        var kettleRt = kettle.rectTransform;
                        if (HasLid) finishLabel.text = "다관 뚜껑 여는 중…";
                        yield return LidTo(true);
                        finishLabel.text = "물 붓는 중…";
                        yield return PoseOver(kettleRt, "dl_water_kettle", kettleRest, ReceiverMouth(), 95f, 0.5f, false);
                        float t = 0f, pourDur = 1.3f;
                        double total = k.LiquidMl, sent = 0;
                        while ((t < pourDur || !k.Liquid.IsEmpty) && !skipRequested)
                        {
                            float dt = Time.unscaledDeltaTime;
                            t += dt;
                            var lip = DrinkVesselView.ToParentAnchored(kettleRt, DrinkArt.Anchor("dl_water_kettle", "pourLip"));
                            float surf = ReceiverSurfaceY(vessel);
                            double want = total * Ease(Mathf.Clamp01(t / pourDur)) - sent;
                            if (t >= pourDur) want = k.LiquidMl;
                            sent += Ledger.Emit(LedgerKettle, LedgerVessel, Math.Max(0, want), DrinkTransferLedger.FallSeconds(lip.y - surf, GravityPx), 0.5);
                            if (!k.Liquid.IsEmpty) toolFall.Emit(lip, surf, Mathf.Lerp(5f, 12f, Mathf.Sin(Mathf.PI * Mathf.Clamp(t / pourDur, 0.05f, 0.95f))), StreamTint(DrinkIngredients.WaterColor), vessel.transform);
                            else toolFall.Stop();
                            vessel.SetLayers(LayersOf(LedgerVessel, r));
                            vessel.SetEffects(0f, 0f, 0.8f);
                            ShowLeafHeap(leafHeap, vessel, v.LeafG, FirstLeafId(v));
                            yield return null;
                        }
                        toolFall.Stop();
                        yield return PoseOver(kettleRt, "dl_water_kettle", kettleRest, ReceiverMouth(), 95f, 0.45f, true);
                        // 물이 다 닿으면 뚜껑을 덮고 우린다.
                        while (Ledger.HasInFlight(LedgerVessel) && !skipRequested) yield return null;
                        if (HasLid) finishLabel.text = "다관 뚜껑 닫는 중…";
                        yield return LidTo(false);
                        finishLabel.text = "차 우림…"; // 뚜껑이 완전히 닫힌 뒤에만 우림(추출)이 시작된다
                        // 우림: 찻잎 → 물로 추출(장부의 잎 g 감소 = 추출분 g 증가), 김. 첨가 액체와 천천히 섞인다.
                        t = 0f;
                        steam.gameObject.SetActive(true);
                        var steamFrames = DrinkArt.Frames("dl_steam");
                        while ((t < 2.0f || Ledger.HasInFlight(LedgerVessel)) && !skipRequested)
                        {
                            float dt = Time.unscaledDeltaTime;
                            t += dt;
                            Ledger.Steep(LedgerVessel, dt);
                            if (!v.Liquid.IsEmpty) v.Liquid.Mix(0.6, dt * 3.0 / 2.0);
                            vessel.SetLayers(LayersOf(LedgerVessel, r));
                            vessel.SetEffects(0f, 0.1f, 0.2f);
                            ShowLeafHeap(leafHeap, vessel, v.LeafG, FirstLeafId(v));
                            AnimateSteam(steamFrames, vessel, t);
                            yield return null;
                        }
                        // 거름망: 찻물만 찻잔으로, 우린 잎은 다관에 남는다(장부의 잎 g은 그대로).
                        finishLabel.text = "찻잔에 따르는 중…";
                        yield return PourInto(vessel, serve, r, false);
                    }
                    break;
            }
            ShowFinal(r, played && !skipRequested);
            sequence = null;
            skipRequested = false;
            done?.Invoke();
        }

        /// <summary>
        /// 바스푼 자세: 실제 스프라이트의 맨 아래 불투명 행(끝)을 피벗으로, 끝이 그릇 안쪽 바닥보다 1.5 아트 픽셀 위에서 좌우로 젓는다.
        /// 그릇 림 아래에 오는 스푼의 모든 불투명 픽셀을 그릇 마스크(안쪽 단면)와 대조해, 하나라도 밖이면 진폭/기울기를 줄인다(잘라 숨기지 않음).
        /// </summary>
        void PoseSpoon(float t, float ramp)
        {
            var frames = DrinkArt.Frames("dl_barspoon");
            int fi = frames.Length > 1 ? Mathf.FloorToInt(t * 10f) % frames.Length : 0;
            if (frames.Length > 0) spoon.sprite = frames[fi];
            var rt = spoon.rectTransform;
            var lr = DrinkArt.LiquidRect(vessel.AssetId);
            float artH = vessel.Glass.sprite != null ? vessel.Glass.sprite.rect.height : 64f;
            bool haveRows = DrinkArt.SpoonExtent(fi, out var rows, out int sw, out int sh);
            Vector2 tipN = new Vector2(0.5f, 0f);
            int tipRow = 0;
            if (haveRows)
            {
                while (tipRow < sh && rows[tipRow * 2] < 0) tipRow++;
                tipN = new Vector2((rows[tipRow * 2] + rows[tipRow * 2 + 1]) * 0.5f / sw, tipRow / (float)sh);
            }
            rt.pivot = tipN;
            float floor = lr.yMin + 1.5f / artH;
            float bob = (0.5f + 0.5f * Mathf.Sin(t * 7f)) * 2f / artH; // 바닥 위로 0~2 아트 픽셀 들썩임
            // 넣기/빼기: ramp 0 = 끝이 림 위(입구 위 대기), 1 = 바닥 근처. 끝 높이가 이어져 순간이동 없이 내려가고 올라온다.
            float above = lr.yMax + 4f / artH;
            float tipY = Mathf.Lerp(above, floor + bob, Ease(Mathf.Clamp01(ramp)));
            float phase = t * 9f;
            // 젓는 중심과 폭 = 볼 높이(끝 위 4 아트 픽셀)에서 실제 마스크 안쪽 구간 — 마스크가 액체 사각형과 어긋나도 벽 사이 가운데.
            float artW = vessel.Glass.sprite != null ? vessel.Glass.sprite.rect.width : 48f;
            float spanMin, spanMax;
            DrinkArt.MaskSpan(vessel.AssetId, floor + 4f / artH, out spanMin, out spanMax);
            float cx = (spanMin + spanMax) * 0.5f;
            float bowl = haveRows ? BowlHalfWidth(rows, sw) / artW : 3f / artW;
            float reach = Mathf.Max(0f, (spanMax - spanMin) * 0.5f - bowl - 1.5f / artW);
            float amp = ramp, lean = 0f;
            int outside = 0;
            for (int i = 0; i <= 10; i++)
            {
                float x = cx + amp * reach * Mathf.Sin(phase);
                lean = amp * 4f * Mathf.Cos(phase);
                rt.localRotation = Quaternion.Euler(0f, 0f, lean);
                rt.anchoredPosition = vessel.PointInParent(new Vector2(x, tipY));
                outside = haveRows ? SpoonOutside(rows, sw, sh, out _) : 0;
                if (outside == 0) break;
                amp = i < 9 ? amp * 0.8f : 0f;
            }
            var probe = new SpoonProbe { frame = fi, amplitude = amp, lean = lean, outsidePixels = outside };
            if (haveRows) Measure(ref probe, rows, sw, sh, tipRow, floor, artH);
            Spoon = probe;
        }

        /// <summary>바스푼 아래쪽(볼) 행들 중 끝 중심에서 가장 먼 가로 거리(아트 픽셀).</summary>
        static float BowlHalfWidth(int[] rows, int sw)
        {
            int tip = 0;
            while (tip * 2 < rows.Length && rows[tip * 2] < 0) tip++;
            if (tip * 2 >= rows.Length) return 3f;
            float c = (rows[tip * 2] + rows[tip * 2 + 1]) * 0.5f, half = 0f;
            for (int y = tip; y < Mathf.Min(rows.Length / 2, tip + 14); y++)
            {
                if (rows[y * 2] < 0) continue;
                half = Mathf.Max(half, Mathf.Max(c - rows[y * 2], rows[y * 2 + 1] - c));
            }
            return half;
        }

        /// <summary>스푼 불투명 픽셀(행 구간) 중 그릇 림 아래인데 그릇 안쪽 마스크 밖이거나 벽·바닥에서 1 아트 픽셀 안쪽이 아닌 개수.</summary>
        int SpoonOutside(int[] rows, int sw, int sh, out float lowest)
        {
            var rt = spoon.rectTransform;
            var vr = vessel.Rect;
            var lr = DrinkArt.LiquidRect(vessel.AssetId);
            int bad = 0;
            lowest = float.MaxValue;
            // 안쪽 마스크 안이면서 좌우·아래로 1 아트 픽셀 떨어져 있어야 한다(벽·바닥 테두리에 붙거나 겹치지 않음).
            var gs = vessel.Glass.sprite;
            var mx = new Vector2(1f / (gs != null ? gs.rect.width : 48f), 0f);
            var my = new Vector2(0f, 1f / (gs != null ? gs.rect.height : 64f));
            for (int y = 0; y < sh; y++)
            {
                int a = rows[y * 2], b = rows[y * 2 + 1];
                if (a < 0) continue;
                for (int x = a; x < b; x++)
                {
                    var n = VesselNormalized(rt, vr, new Vector2((x + 0.5f) / sw, (y + 0.5f) / sh));
                    if (n.y > lr.yMax) continue; // 림 위: 그릇 밖으로 나온 손잡이 쪽
                    lowest = Mathf.Min(lowest, n.y);
                    if (!DrinkArt.InsideMask(vessel.AssetId, n) || !DrinkArt.InsideMask(vessel.AssetId, n + mx) || !DrinkArt.InsideMask(vessel.AssetId, n - mx)
                        || !DrinkArt.InsideMask(vessel.AssetId, n - my)) bad++;
                }
            }
            return bad;
        }

        static Vector2 VesselNormalized(RectTransform from, RectTransform vesselRt, Vector2 n)
        {
            var r = from.rect;
            var world = from.TransformPoint(new Vector3(r.xMin + r.width * n.x, r.yMin + r.height * n.y, 0f));
            var local = vesselRt.InverseTransformPoint(world);
            var vr = vesselRt.rect;
            return new Vector2((local.x - vr.xMin) / vr.width, (local.y - vr.yMin) / vr.height);
        }

        /// <summary>QA 측정: 끝 높이(바닥 위 px), 좌우 극점, 극점 높이에서 벽까지 여유(px). 마스크를 행 단위로 훑는다.</summary>
        void Measure(ref SpoonProbe p, int[] rows, int sw, int sh, int tipRow, float floor01, float artH)
        {
            var rt = spoon.rectTransform;
            var vr = vessel.Rect;
            var lr = DrinkArt.LiquidRect(vessel.AssetId);
            float pxPerN = vr.rect.height; // 그릇 정규화 y 1 = 그릇 높이 px
            float pxPerNx = vr.rect.width;
            var tipN = VesselNormalized(rt, vr, new Vector2((rows[tipRow * 2] + rows[tipRow * 2 + 1]) * 0.5f / sw, tipRow / (float)sh));
            p.tip = vessel.PointInParent(tipN);
            p.floorY = vessel.PointInParent(new Vector2(0.5f, lr.yMin)).y;
            p.tipAboveFloorPx = (tipN.y - lr.yMin) * pxPerN;
            float minX = float.MaxValue, maxX = float.MinValue, leftClear = float.MaxValue, rightClear = float.MaxValue, lowest = float.MaxValue;
            for (int y = 0; y < sh; y++)
            {
                int a = rows[y * 2], b = rows[y * 2 + 1];
                if (a < 0) continue;
                foreach (int x in new[] { a, b - 1 })
                {
                    var n = VesselNormalized(rt, vr, new Vector2((x + 0.5f) / sw, (y + 0.5f) / sh));
                    if (n.y > lr.yMax) continue;
                    lowest = Mathf.Min(lowest, n.y);
                    minX = Mathf.Min(minX, n.x); maxX = Mathf.Max(maxX, n.x);
                    // 같은 높이의 안쪽 벽: 마스크 행을 좌우로 훑는다
                    float step = 0.5f / pxPerNx;
                    float l = n.x, rr = n.x;
                    while (l > 0f && DrinkArt.InsideMask(vessel.AssetId, new Vector2(l - step, n.y))) l -= step;
                    while (rr < 1f && DrinkArt.InsideMask(vessel.AssetId, new Vector2(rr + step, n.y))) rr += step;
                    if (x == a) leftClear = Mathf.Min(leftClear, (n.x - l) * pxPerNx);
                    if (x == b - 1) rightClear = Mathf.Min(rightClear, (rr - n.x) * pxPerNx);
                }
            }
            p.minX = vessel.PointInParent(new Vector2(minX, 0.5f)).x;
            p.maxX = vessel.PointInParent(new Vector2(maxX, 0.5f)).x;
            p.lowestY = vessel.PointInParent(new Vector2(0.5f, lowest)).y;
            p.leftClearPx = leftClear; p.rightClearPx = rightClear;
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
                serve = DrinkVesselView.Create(scene, id, new Vector2(-170f, -230f), CapacityFor(id));
                serve.Rect.anchoredPosition = StandOnTable(serve, -170f);
                Placeholders |= serve.Placeholder;
            }
            Ledger[LedgerServe].Clear();
            serve.SetEmpty();
            // 셰이킹/스터링은 얼음을 먼저 담은 잔에 따른다(액면이 얼음 사이로 오른다). 빌드/레이어링은 제조 그릇이 곧 잔.
            serve.SetIce(r.Kind == DrinkKind.Cocktail && (r.Technique == DrinkTechnique.Shake || r.Technique == DrinkTechnique.Stir) ? r.ice : 0);
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
        /// 그릇 → 마실 잔(또는 다관 → 찻잔). 받는 잔은 붓는 입구 쪽에 놓고, 붓는 그릇은 손잡이 축으로 기울여 입구를 잔 위로 가져간다.
        /// 접근은 액면이 입구에 닿기 직전 각도까지만 기울이고, 그 뒤 기울기를 더하면 SolveTilt로 입구를 넘는 부피만 장부에서 흘러나가
        /// 낙하 시간 뒤 잔에 닿는다. 기운 그릇의 액면은 수평으로 그린다(내용물 숨김 없음). 차는 액체·추출분만 이동하고 잎은 다관에 남는다.
        /// </summary>
        IEnumerator PourInto(DrinkVesselView from, DrinkVesselView to, DrinkRecipe r, bool strain)
        {
            steam.gameObject.SetActive(false); // 김은 그릇을 따라가지 않으므로 붓는 동안 숨긴다(완성 잔에서 다시)
            var src = Ledger[LedgerVessel];
            var lipN = DrinkArt.Anchor(from.AssetId, "pourLip");
            var gripN = DrinkArt.Anchor(from.AssetId, "grip");
            float dir = lipN.x >= gripN.x ? 1f : -1f;
            DrinkVesselView.SetPivotKeepPosition(from.Rect, new Vector2(0.5f, 0f));
            var fromBase = from.Rect.anchoredPosition;
            // 받는 잔은 공정 진행 방향(따르는 입구 쪽)으로 이어 놓는다 — 화면 밖이어도 카메라가 따라가 둘의 가운데를 보인다.
            float toX = fromBase.x + dir * 270f, shift = 0f;
            // 공간이 모자라면 붓는 그릇을 먼저 반대쪽으로 부드럽게 옮긴다.
            if (Mathf.Abs(shift) > 0.5f)
            {
                float m = 0f;
                while (m < 0.4f && !skipRequested) { m += Time.unscaledDeltaTime; from.Rect.anchoredPosition = fromBase + new Vector2(shift * Ease(Mathf.Clamp01(m / 0.4f)), 0f); yield return null; }
                fromBase += new Vector2(shift, 0f);
                from.Rect.anchoredPosition = fromBase;
            }
            DrinkVesselView.SetPivotKeepPosition(to.Rect, new Vector2(0.5f, 0f));
            to.Rect.anchoredPosition = StandOnTable(to, toX + shift);
            craftFocus = (fromBase.x + toX + shift) * 0.5f; // 따르는 동안 두 입구가 함께 보이는 구도
            to.transform.localRotation = Quaternion.identity;
            to.gameObject.SetActive(true);
            to.SetLayers(LayersOf(LedgerServe, r));
            DrinkVesselView.SetPivotKeepPosition(from.Rect, gripN);
            var gripBase = from.Rect.anchoredPosition;
            var toMouth = to.PointInParent(DrinkArt.Anchor(to.AssetId, "mouth"));
            from.SetLiquidHidden(false);
            float onset = SpillOnsetAngle(from, src.LiquidMl);
            float startTilt = Mathf.Min(onset - 4f, 80f), endTilt = Mathf.Max(onset + 30f, 125f);
            // 접근·기울임(흘리지 않는 각도까지)
            float t = 0f;
            const float approach = 0.5f;
            while (t < approach && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease(Mathf.Clamp01(t / approach));
                PoseVessel(from, gripBase, toMouth, Ease(Mathf.Clamp01(t / approach * 1.25f)), startTilt * k, dir);
                RenderPour(from, to, r);
                yield return null;
            }
            // 흐름: 기울기를 더하면 입구를 넘는 부피만큼 흐른다. 양에 비례한 시간.
            double total = src.LiquidMl;
            float dur = Mathf.Clamp(0.9f + (float)total / 150f * 0.9f, 0.9f, 1.8f);
            double maxRate = total / dur * 2.2;
            t = 0f;
            while ((t < dur || !src.Liquid.IsEmpty) && !skipRequested && t < dur + 3f)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float tilt = Mathf.Lerp(startTilt, endTilt, Ease(Mathf.Clamp01(t / dur)));
                PoseVessel(from, gripBase, toMouth, 1f, tilt, dir);
                var lip = DrinkVesselView.ToParentAnchored(from.Rect, lipN);
                float surf = ReceiverSurfaceY(to);
                double have = src.LiquidMl;
                double spill = have > 0 ? DrinkFluidPhysics.SolveTilt(from.Profile, new[] { have }, tilt).SpillMl : 0;
                double want = t >= dur ? Math.Min(have, maxRate * dt) : Math.Min(spill, maxRate * dt);
                double sent = Ledger.Emit(LedgerVessel, LedgerServe, want, DrinkTransferLedger.FallSeconds(lip.y - surf, GravityPx), 0.5);
                var col = LayersOf(LedgerVessel, r);
                var streamColor = StreamTint(col.Count > 0 ? col[col.Count - 1].color : DrinkCalculator.Look(r).color);
                if (sent > 1e-6) bottleFall.Emit(lip, surf, Mathf.Lerp(5f, 13f, Mathf.Clamp01((float)(sent / Math.Max(1e-6, maxRate * dt)))), streamColor, to.transform);
                else bottleFall.Stop();
                to.SetEffects(strain ? 0.8f : 0f, 0f, 0.7f);
                RenderPour(from, to, r);
                ServeGeometry = new PourGeometry { lip = lip, mouth = toMouth, streamEnd = new Vector2(lip.x, surf), pivot = from.Rect.pivot, rotation = from.Rect.localEulerAngles.z, flowing = sent > 1e-6 };
                yield return null;
            }
            bottleFall.Stop();
            // 복귀(마지막 줄기는 계속 떨어져 잔에 닿는다)
            t = 0f;
            while ((t < approach || Ledger.HasInFlight(LedgerServe)) && !skipRequested && t < approach + 2f)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float k = 1f - Ease(Mathf.Clamp01(t / approach));
                PoseVessel(from, gripBase, toMouth, k, endTilt * k, dir);
                RenderPour(from, to, r);
                yield return null;
            }
            PoseVessel(from, gripBase, toMouth, 0f, 0f, dir);
            from.SetLiquidHidden(false);
            RenderPour(from, to, r);
        }

        void RenderPour(DrinkVesselView from, DrinkVesselView to, DrinkRecipe r)
        {
            from.SetLayers(LayersOf(LedgerVessel, r));
            to.SetLayers(LayersOf(LedgerServe, r));
            var v = Ledger[LedgerVessel];
            if (v.LeafG > 0) ShowLeafHeap(leafHeap, from, v.LeafG, FirstLeafId(v));
            else leafHeap.gameObject.SetActive(false);
        }

        /// <summary>그릇 → 잔 붓기 기하(QA·시험).</summary>
        public PourGeometry ServeGeometry { get; private set; }

        void PoseVessel(DrinkVesselView v, Vector2 gripBase, Vector2 targetMouth, float move, float tilt, float dir)
        {
            var rt = v.Rect;
            rt.localRotation = Quaternion.Euler(0f, 0f, -dir * tilt);
            rt.anchoredPosition = gripBase;
            var lipN = DrinkArt.Anchor(v.AssetId, "pourLip");
            var lipAtBase = DrinkVesselView.ToParentAnchored(rt, lipN);
            rt.anchoredPosition = gripBase + (targetMouth + new Vector2(0f, 30f) - lipAtBase) * move;
        }

        /// <summary>
        /// 완성 상태. 모션을 끝까지 재생했으면 장부의 실제 결과(따른 잔 또는 빌드/레이어 잔), 건너뛰었으면 같은 배합의 결정적 최종 상태
        /// (DrinkPhysicsSim.Final)로 장부를 맞춘다. 셰이킹·스터링·차는 균일 혼합이라 두 경로의 양·색이 같다.
        /// </summary>
        void ShowFinal(DrinkRecipe r, bool played)
        {
            bottleFall.Cut(); toolFall.Cut();
            spoon.gameObject.SetActive(false);
            craftLidOpen = false; lidLinger = 0f; lidT = 0f; PoseLid(); // 완성(건너뛰기 포함)은 덮인 다관
            bool inVessel = r.Technique == DrinkTechnique.Build || r.Technique == DrinkTechnique.Layer;
            var target = inVessel ? vessel : serve;
            string targetName = inVessel ? LedgerVessel : LedgerServe;
            Ledger.Flush();
            if (!played)
            {
                var fin = DrinkPhysicsSim.Final(r);
                var v = Ledger[LedgerVessel];
                if (inVessel) { v.Clear(); v.Liquid = fin; }
                else
                {
                    var s = Ledger[LedgerServe];
                    s.Clear();
                    s.Liquid = fin;
                    double leaf = r.LeafTotal() / 10.0;
                    v.Clear();
                    if (r.Kind == DrinkKind.Tea && leaf > 0)
                    {
                        // 건너뛰기에서도 차 장부: 추출분은 찻잔으로, 우린 잎은 다관에 남는다(색은 Final의 차 성분이 이미 표현).
                        s.DissolvedG = leaf * DrinkTransferLedger.ExtractableFraction;
                        foreach (var st in r.steps)
                        {
                            var d = st == null ? null : DrinkIngredients.Get(st.id);
                            if (d != null && d.form == IngredientForm.Leaf && st.amount > 0) v.AddLeaf(d.id, st.amount / 10.0 * (1 - DrinkTransferLedger.ExtractableFraction));
                        }
                    }
                }
            }
            if (!inVessel) { vessel.SetLayers(LayersOf(LedgerVessel, r)); serve.gameObject.SetActive(true); }
            else if (serve != null) serve.gameObject.SetActive(false);
            leafHeap.gameObject.SetActive(false);
            target.transform.localRotation = Quaternion.identity;
            target.SetLiquidHidden(false);
            target.SetLayers(LayersOf(targetName, r));
            target.SetEffects(0f, 0f, 0.1f);
            target.SetIce(r.Kind == DrinkKind.Cocktail ? r.ice : 0);
            steam.gameObject.SetActive(r.Kind == DrinkKind.Tea);
            if (r.Kind == DrinkKind.Tea) AnimateSteam(DrinkArt.Frames("dl_steam"), target, 0f);
            finishLabel.text = $"완성 — {r.name}";
            FinalVessel = target;
            showingFinal = true;
            finalSignature = Signature(r);
        }

        public DrinkVesselView FinalVessel { get; private set; }
        /// <summary>제조 완료 후 완성 잔을 보이는 중. 장부의 제조 그릇은 비었고(따랐음) 배합에서 다시 채우지 않는다 — 다음 편집·재료 선택에서 끝난다.</summary>
        bool showingFinal;
        string finalSignature;

        static string Signature(DrinkRecipe r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(r.kind).Append('|').Append(r.technique).Append('|').Append(r.water).Append('|').Append(r.ice);
            if (r.steps != null) foreach (var st in r.steps) if (st != null) sb.Append('|').Append(st.id).Append(':').Append(st.amount);
            return sb.ToString();
        }
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

    /// <summary>떨어지는 찻잎 한 장(표시 전용, 장부 덩어리와 같은 낙하 시간).</summary>
    public class DrinkFallingBit : MonoBehaviour
    {
        Vector2 from, to;
        float t, dur;
        public void Init(Vector2 from, Vector2 to, float dur) { this.from = from; this.to = to; this.dur = Mathf.Max(0.05f, dur); }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            // 중력 낙하: 높이는 k² 곡선(처음 느리고 점점 빠르게)
            ((RectTransform)transform).anchoredPosition = new Vector2(Mathf.Lerp(from.x, to.x, k) + Mathf.Sin(k * 9f) * 4f, Mathf.Lerp(from.y, to.y, k * k));
            transform.localRotation = Quaternion.Euler(0, 0, k * 220f);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
