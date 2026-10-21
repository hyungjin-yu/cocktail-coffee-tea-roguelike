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
    /// 제조 확정 후에는 기법별 모션(셰이킹/스터링/빌드/레이어링/차 우림/커피 추출)을 재생한다(건너뛰기 가능, 결과 동일).
    /// 커피: 원두 통 → 원두 스쿱 → 가운데 추출 도구(에스프레소 머신 / 드리퍼+서버 / 모카포트). 손 없이 도구만 움직인다.
    /// </summary>
    public class DrinkStageView : MonoBehaviour
    {
        /// <summary>찻잎 통(그리고 병 마스크가 없을 때)은 85°부터 흐른다. 액체 병은 실제 잔량의 SolveTilt로 입구에 닿는 각도부터 흐른다
        /// (많으면 일찍, 적으면 더 기울여야). 거의 빈 병까지 따를 수 있게 165°까지 기울인다.</summary>
        public const float TiltStart = 85f, TiltMax = 165f;
        public const float LiquidMaxRate = 250f; // 0.1mL/초 → 25mL/s
        public const float LeafMaxRate = 25f;    // 0.1g/초 → 2.5g/s
        public const float BeanMaxRate = 80f;    // 0.1g/초 → 8g/s (원두는 알갱이가 커서 빨리 떨어진다)
        /// <summary>화면 중력(px/s²). 줄기 머리·꼬리와 장부 덩어리의 낙하 시간이 같은 값을 쓴다.</summary>
        public const float GravityPx = 1800f;

        /// <summary>장부 그릇 이름.</summary>
        public const string LedgerBottle = "bottle", LedgerMeasure = "measure", LedgerVessel = "vessel", LedgerServe = "serve", LedgerKettle = "kettle";
        /// <summary>커피 추출 장부: 드리퍼(받은 물) / 추출 대기 커피(도구 안에서 만들어져 나올 커피).</summary>
        public const string LedgerDripper = "dripper", LedgerBrew = "brew";

        RectTransform root, scene;
        /// <summary>카메라가 움직이는 장면 층(도구가 여기 붙는다).</summary>
        public RectTransform Scene => scene;
        Text readout, toolLabel, finishLabel, sourceLabel;
        Image bottle, stream, leafHeap, chaha, kettle, steam, spoon, chahaHeap;
        readonly Image[] coffeeBeanHeap = new Image[8];
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
            "gin" or "rum" or "absinthe" => 500f,
            "blue_curacao" or "grenadine" or "creme_cassis" or "limoncello" => 350f,
            _ => 250f, // lemon_juice, honey
        };
        public DrinkVesselView MainVessel => vessel;
        public DrinkVesselView ServeVessel => serve;
        public DrinkVesselView Jigger => jigger;
        public RectTransform ChahaRect => chaha != null ? chaha.rectTransform : null;
        /// <summary>건조 재료 계량 도구 아트(찻잎 = 다하, 원두 = 원두 스쿱).</summary>
        public string LeafToolArt => leafToolArt;
        public RectTransform DripperRect => dripper != null ? dripper.rectTransform : null;
        public RectTransform FlameRect => flame != null ? flame.rectTransform : null;
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
            chaha.rectTransform.anchoredPosition = ChahaCenter;
            DrinkVesselView.SetPivotKeepPosition(chaha.rectTransform, DrinkArt.Anchor("dl_chaha", "grip"));
            chahaRest = chaha.rectTransform.anchoredPosition;
            chahaHeap = AddImage("ChahaHeap", chaha.rectTransform, DrinkVesselView.White, new Vector2(120f, 30f));
            chahaHeap.rectTransform.anchorMin = chahaHeap.rectTransform.anchorMax = new Vector2(0.5f, 0.45f);
            chahaHeap.rectTransform.pivot = new Vector2(0.5f, 0f);
            for (int i = 0; i < coffeeBeanHeap.Length; i++)
            {
                var bean = AddImage("CoffeeBeanInScoop" + i, chaha.rectTransform, DrinkVesselView.White, new Vector2(15f, 12f));
                bean.raycastTarget = false;
                bean.gameObject.SetActive(false);
                coffeeBeanHeap[i] = bean;
            }
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

        static readonly Vector2 ChahaCenter = new Vector2(-30f, 10f);
        string leafToolArt = "dl_chaha";

        /// <summary>건조 재료 계량 도구 교체(찻잎 = 다하, 원두 = 원두 스쿱). 같은 자리·손잡이 피벗, 원두 스쿱 아트가 없으면 다하.</summary>
        void SetLeafTool(DrinkIngredientDef def)
        {
            string want = def != null && def.IsCoffeeBean && DrinkArt.Has(CoffeeBrewing.ScoopArt) ? CoffeeBrewing.ScoopArt : "dl_chaha";
            if (want == leafToolArt) return;
            leafToolArt = want;
            var rt = chaha.rectTransform;
            chaha.sprite = DrinkArt.SpriteOrPlaceholder(want, out bool ph);
            Placeholders |= ph;
            NativeSize(chaha, 4);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ChahaCenter;
            DrinkVesselView.SetPivotKeepPosition(rt, DrinkArt.Anchor(want, "grip"));
            chahaRest = rt.anchoredPosition;
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

        /// <summary>제조 배경 아트 묶음 이름(커피 전용 아트가 없으면 바 배경).</summary>
        static string CraftSet(DrinkKind kind) => kind == DrinkKind.Tea ? "tea" : kind == DrinkKind.Coffee && DrinkArt.Has("dl_craft_coffee_wall") && DrinkArt.Has("dl_craft_coffee_table") ? "coffee" : "bar";

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
            // 커피 전용 배경 아트가 없으면 바 배경을 쓴다.
            string set = CraftSet(kind);
            var craftWall = DrinkArt.Sprite($"dl_craft_{set}_wall");
            var craftTable = DrinkArt.Sprite($"dl_craft_{set}_table");
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
                case DrinkTechnique.Brew: return CoffeeBrewing.ApparatusArt(r.coffeeMethod);
                default: return "dl_glass_cocktail";
            }
        }

        public static string ServeFor(DrinkRecipe r) => r.Kind == DrinkKind.Tea ? "dl_teacup" : r.Kind == DrinkKind.Coffee ? CoffeeBrewing.CupArt(r.coffeeMethod) : "dl_glass_cocktail";

        /// <summary>그릇 용량(mL). 완성 잔은 DrinkRules의 고정 용량(최대 배합+얼음에서도 약 80~82%), 제조 도구는 최대 배합의 약 80%.</summary>
        public static float CapacityFor(string id) => id switch
        {
            "dl_teapot" => 260f,
            "dl_teacup" => DrinkRules.ServeTeaCapacityMl,
            "dl_glass_cocktail" => DrinkRules.ServeCocktailCapacityMl,
            "dl_jigger" => DrinkRules.JiggerCapacity / 10f,
            "dl_coffee_demitasse" => CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.EspressoMachine),
            "dl_coffee_cup" => CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.MokaPot),
            "dl_coffee_mug" => CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.PourOver),
            "dl_coffee_carafe" => 400f,
            "dl_coffee_moka" => 300f,
            "dl_coffee_espresso" => 80f,
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
            kettle.gameObject.SetActive(NeedsKettle(draft));
            steam.gameObject.SetActive(false);
            spoon.gameObject.SetActive(false);
            if (flame != null) flame.gameObject.SetActive(false);
            HideCloseUp();
            PoseDripper(0f);
            PlaceCoffeeCup(draft, true);
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
            SetupCoffeeParts();
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
            if (phase != CyclePhase.None && phase != CyclePhase.BottleReturn) return (ToolRest.x + (Receiver != null ? Receiver.Rect.anchoredPosition.x : VesselRest.x)) * 0.5f;
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
        /// <summary>단계 문구가 화면에 보이는지(에스프레소 클로즈업 동안 숨김).</summary>
        public bool FinishLabelVisible => finishLabel != null && finishLabel.isActiveAndEnabled;
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
            if (draft.Kind == DrinkKind.Tea || draft.Kind == DrinkKind.Coffee)
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
            if (draft.Kind == DrinkKind.Coffee) SyncCupFromDraft();
            bool liquidMatch = draft.Kind == DrinkKind.Coffee ? v.Liquid.IsEmpty : DrinkPhysicsSim.Matches(v.Liquid, draft);
            if (liquidMatch && leavesMatch && v.DissolvedG <= 0) return;
            v.Clear();
            v.Liquid = DrinkPhysicsSim.Build(draft);
            if (draft.Kind == DrinkKind.Tea || draft.Kind == DrinkKind.Coffee)
                foreach (var s in draft.steps)
                {
                    var d = s == null ? null : DrinkIngredients.Get(s.id);
                    if (d != null && d.form == IngredientForm.Leaf && s.amount > 0) v.AddLeaf(d.id, s.amount / 10.0);
                }
        }

        /// <summary>커피 편집 중 완성 잔 = 배합의 첨가 액체(스팀 우유). 직접 입력·빼기로 다르면 다시 만든다(옮기는 중·공중 덩어리 있으면 그대로).</summary>
        void SyncCupFromDraft()
        {
            if (serve == null || !serve.gameObject.activeSelf || Ledger.HasInFlight(LedgerServe)) return;
            var s = Ledger[LedgerServe];
            var want = DrinkPhysicsSim.CupAdditions(draft);
            bool same = Math.Abs(s.Liquid.TotalVolume - want.TotalVolume) <= 0.1;
            if (same)
                foreach (var st in draft.steps)
                {
                    var d = st == null ? null : DrinkIngredients.Get(st.id);
                    if (d != null && d.form == IngredientForm.Liquid && Math.Abs(s.Liquid.ComponentVolume(d.id) - st.amount / 10.0) > 0.05) same = false;
                }
            if (same) return;
            s.Clear();
            s.Liquid = want;
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
            if (leaf) SetLeafTool(def);
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
            toolLabel.text = def.IsCoffeeBean ? $"{def.name} — 통을 끌어 스쿱에 덜기({DrinkRules.Format(DrinkRules.CoffeeScoopCapacity)}g씩 {CoffeeBrewing.DisplayName(draft.coffeeMethod)}에)"
                : leaf ? $"{def.name} — 통을 끌어 다하에 덜기(3.0g씩 다관으로)"
                : CupReceives(def) ? $"{def.name} — 피처를 끌어 지거에 붓기(잔에 먼저 담고 커피를 그 위로)" : $"{def.name} — 병을 끌어 지거에 붓기(45.0mL씩 나눠 붓기)";
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
            {
                if (current.IsCoffeeBean)
                    return DrinkVesselView.ToParentAnchored(chaha.rectTransform, new Vector2(0.27f, 0.28f));
                return DrinkVesselView.ToParentAnchored(chaha.rectTransform, DrinkArt.Anchor(leafToolArt, "mouth"));
            }
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
            // 원두 통은 스쿱보다 크다: 입구만 맞추면 기울어진 통 몸체가 스쿱 그릇을 덮는다. 다가갈수록 통 전체를 들어 올려 스쿱 위 빈틈을 남긴다.
            if (current.IsCoffeeBean && chaha.gameObject.activeSelf)
                bottleRt.anchoredPosition += new Vector2(0f, approach * Mathf.Max(0f, CoffeeTinMinGap - CoffeeTinScoopGap));
            var lip = DrinkVesselView.ToParentAnchored(bottleRt, lipN);
            Geometry = new PourGeometry { lip = lip, mouth = mouth, pivot = bottleRt.pivot, rotation = -Tilt, streamEnd = lip, flowing = false };
        }

        /// <summary>원두를 덜 때 통(회전 포함 사각형) 가장 낮은 점과 스쿱 사각형 가장 높은 점 사이 최소 세로 간격(px, 1920×1080 기준).</summary>
        public const float CoffeeTinMinGap = 16f;

        /// <summary>원두 통 사각형 바닥(회전 적용) − 원두 스쿱 사각형 위(제조대 px). 음수면 겹침.</summary>
        public float CoffeeTinScoopGap
        {
            get
            {
                if (bottleRt == null || chaha == null) return float.MaxValue;
                float tinBottom = float.MaxValue, scoopTop = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    var n = new Vector2(i & 1, i >> 1);
                    tinBottom = Mathf.Min(tinBottom, DrinkVesselView.ToParentAnchored(bottleRt, n).y);
                    scoopTop = Mathf.Max(scoopTop, DrinkVesselView.ToParentAnchored(chaha.rectTransform, n).y);
                }
                return tinBottom - scoopTop;
            }
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
            float max = def.form == IngredientForm.Leaf ? (def.IsCoffeeBean ? BeanMaxRate : LeafMaxRate) : LiquidMaxRate;
            return t * t * max * (1f - 0.6f * def.viscosity);
        }

        int MeasureCapacity => current != null && current.form == IngredientForm.Leaf ? LeafToolCapacity(current) : DrinkRules.JiggerCapacity;

        static int LeafToolCapacity(DrinkIngredientDef def) => def != null && def.IsCoffeeBean ? DrinkRules.CoffeeScoopCapacity : DrinkRules.ChahaCapacity;

        /// <summary>계량 도구 안 받는 면(액면 또는 바닥)의 높이(제조대 좌표).</summary>
        float MeasureSurfaceY()
        {
            if (current != null && current.form == IngredientForm.Leaf)
                return DrinkVesselView.ToParentAnchored(chaha.rectTransform, new Vector2(0.5f, DrinkArt.LiquidRect(leafToolArt).yMin)).y;
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
                float t = (float)(g * 10.0 / LeafToolCapacity(def));
                chahaHeap.gameObject.SetActive(g > 0.0005);
                if (def.IsCoffeeBean)
                {
                    // Show actual bean sprites collecting in the scoop bowl instead of stretching one icon into a fake mound.
                    var beanFrames = DrinkArt.Frames("dl_leaf_coffee_bean");
                    chahaHeap.gameObject.SetActive(false);
                    int count = Mathf.Clamp(Mathf.CeilToInt(t * coffeeBeanHeap.Length), 0, coffeeBeanHeap.Length);
                    Vector2[] pile = { new Vector2(.19f,.30f), new Vector2(.31f,.28f), new Vector2(.25f,.39f), new Vector2(.38f,.34f), new Vector2(.16f,.42f), new Vector2(.32f,.47f), new Vector2(.23f,.53f), new Vector2(.41f,.45f) };
                    for (int i = 0; i < coffeeBeanHeap.Length; i++)
                    {
                        var img = coffeeBeanHeap[i];
                        img.gameObject.SetActive(i < count && beanFrames.Length > 0);
                        if (i >= count || beanFrames.Length == 0) continue;
                        img.sprite = beanFrames[(i + Mathf.FloorToInt(Time.unscaledTime * 3f)) % beanFrames.Length];
                        img.color = Color.white;
                        img.rectTransform.anchorMin = img.rectTransform.anchorMax = pile[i];
                        img.rectTransform.anchoredPosition = Vector2.zero;
                        img.rectTransform.sizeDelta = new Vector2(15f, 12f);
                    }
                }
                else
                {
                    chahaHeap.gameObject.SetActive(g > 0.0005);
                    foreach (var bean in coffeeBeanHeap) bean.gameObject.SetActive(false);
                    var frames = DrinkArt.Frames("dl_leaf_heap_" + def.LeafArtKey);
                    if (frames.Length > 0) { chahaHeap.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(Mathf.Clamp01(t) * frames.Length))]; chahaHeap.color = Color.white; }
                    else { chahaHeap.sprite = DrinkVesselView.White; chahaHeap.color = def.color; }
                    chahaHeap.rectTransform.sizeDelta = new Vector2(120f, 30f);
                }
                chahaHeap.rectTransform.localScale = def.IsCoffeeBean
                    ? new Vector3(0.55f + 0.45f * t, 0.55f + 0.45f * t, 1f)
                    : new Vector3(0.4f + 0.6f * t, 0.3f + 0.7f * t, 1f);
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
                Vector2 target = MeasureMouth();
                if (current.IsCoffeeBean)
                {
                    // Scatter the beans within the bowl, not along the handle.
                    target += new Vector2(UnityEngine.Random.Range(-7f, 8f), UnityEngine.Random.Range(-3f, 5f));
                }
                else target = new Vector2(g.lip.x, MeasureSurfaceY());
                SpawnFallingLeaf(g.lip, target, current);
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
            leafTimer = def.IsCoffeeBean ? 0.045f : 0.06f;
            var frames = DrinkArt.Frames("dl_leaf_" + def.LeafArtKey);
            bool bean = def.IsCoffeeBean;
            var img = AddImage("Leaf", leafRoot, frames.Length > 0 ? frames[UnityEngine.Random.Range(0, frames.Length)] : DrinkVesselView.White, bean ? new Vector2(22f, 18f) : new Vector2(24f, 24f));
            if (frames.Length == 0) img.color = def.color;
            img.gameObject.AddComponent<DrinkFallingBit>().Init(from, to, (float)DrinkTransferLedger.FallSeconds(from.y - to.y, GravityPx), bean ? UnityEngine.Random.Range(5f, 9f) : 4f, bean ? UnityEngine.Random.Range(300f, 520f) : 220f);
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
            // 커피 가루는 도구 안(포타필터·드리퍼·바스켓)에 들어가 보이지 않는다 — 대신 드리퍼 가루 더미만.
            if (draft.Kind != DrinkKind.Coffee && (draft.Kind == DrinkKind.Tea || v.LeafG > 0)) ShowLeafHeap(leafHeap, vessel, v.LeafG, FirstLeafId(v));
            else leafHeap.gameObject.SetActive(false);
            UpdateDripperGrounds(v.LeafG);
            if (draft.Kind == DrinkKind.Coffee && serve != null && serve.gameObject.activeSelf && !showingFinal) serve.SetLayers(LayersOf(LedgerServe, draft));
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
            if (def.form == IngredientForm.Leaf) return Mathf.Lerp(0.7f, 1.1f, Mathf.Clamp01(tenths / (float)LeafToolCapacity(def)));
            float baseT = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(tenths / (float)DrinkRules.JiggerCapacity));
            return baseT * (1f + 0.6f * def.viscosity);
        }

        RectTransform Tool => transferDef != null && transferDef.form == IngredientForm.Leaf ? chaha.rectTransform : jigger.Rect;
        Vector2 ToolRest => transferDef != null && transferDef.form == IngredientForm.Leaf ? chahaRest : jiggerRest;
        string ToolArt => transferDef != null && transferDef.form == IngredientForm.Leaf ? leafToolArt : "dl_jigger";

        /// <summary>원두 스쿱의 그릇을 현재 추출 도구 쪽으로 향하게 한다(에스프레소·핸드 드립·모카포트 공통).</summary>
        bool ScoopFacesReceiver => transferDef != null && transferDef.IsCoffeeBean && leafToolArt == CoffeeBrewing.ScoopArt
            && draft != null && draft.Kind == DrinkKind.Coffee;
        /// <summary>뒤집힌 스쿱의 쏟는 입구 = 그릇 바깥 테두리(좌우 반전 뒤 받는 도구 쪽).</summary>
        static readonly Vector2 ScoopBowlLip = new Vector2(0.06f, 0.62f);
        Vector2 ToolLip => ScoopFlipped && Tool == chaha.rectTransform ? ScoopBowlLip : DrinkArt.Anchor(ToolArt, "pourLip");
        /// <summary>스쿱이 좌우로 뒤집혀 있는가(QA·시험).</summary>
        public bool ScoopFlipped => chaha != null && chaha.rectTransform.localScale.x < 0f;

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
            if (tool == chaha.rectTransform) tool.localScale = new Vector3(ScoopFacesReceiver ? -1f : 1f, 1f, 1f);
            tool.anchoredPosition = ToolRest;
            var lipN = ToolLip;
            var lipAtRest = DrinkVesselView.ToParentAnchored(tool, lipN);
            var target = ReceiverMouth() + new Vector2(0f, ToolLipClearance);
            tool.anchoredPosition = ToolRest + (target - lipAtRest) * move;
        }

        /// <summary>
        /// 편집 중 계량 도구가 붓는 그릇: 커피의 액체 첨가 재료(스팀 우유)는 추출 도구가 아니라 완성 잔에 먼저 담는다. 그 밖에는 제조 그릇.
        /// </summary>
        DrinkVesselView Receiver => CupReceives(transferDef ?? current) && serve != null && serve.gameObject.activeSelf ? serve : vessel;
        string ReceiverLedger => Receiver == serve && serve != null ? LedgerServe : LedgerVessel;
        bool CupReceives(DrinkIngredientDef d) => draft != null && draft.Kind == DrinkKind.Coffee && d != null && d.form == IngredientForm.Liquid;

        Vector2 ReceiverMouth()
        {
            if (Receiver == serve && serve != null) return serve.PointInParent(DrinkArt.Anchor(serve.AssetId, "mouth"));
            // 핸드 드립은 서버 위 드리퍼 입구가 받는 입구(원두·물 모두 드리퍼로).
            if (dripper != null && dripper.gameObject.activeSelf)
                return DrinkVesselView.ToParentAnchored(dripper.rectTransform, DrinkArt.Anchor(CoffeeBrewing.DripperArt, "mouth"));
            return vessel.PointInParent(DrinkArt.Anchor(HasLid ? "dl_teapot_open_body" : vessel.AssetId, "mouth"));
        }

        float ReceiverSurfaceY(DrinkVesselView v)
        {
            // 원두가 드리퍼·머신·모카포트로 들어갈 때는 받는 입구 바로 아래(도구 안)에서 사라진다.
            if (v == vessel && draft != null && draft.Kind == DrinkKind.Coffee && sequence == null) return ReceiverMouth().y - 12f;
            return v.PointInParent(new Vector2(0.5f, Mathf.Max(v.SurfaceY, DrinkArt.LiquidRect(v.AssetId).yMin))).y;
        }

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
                    var lip = DrinkVesselView.ToParentAnchored(Tool, ToolLip);
                    var receiver = Receiver;
                    float surface = ReceiverSurfaceY(receiver);
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
                        emitted = Ledger.Emit(LedgerMeasure, ReceiverLedger, want, fall, DrinkPhysicsSim.Gentleness(draft.Technique));
                        if (emitted > 1e-6)
                            toolFall.Emit(lip, surface, Mathf.Lerp(5f, 14f, Mathf.Clamp01((float)(emitted / Math.Max(1e-6, maxRate * dt)))), dumpStream.color, receiver.transform);
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
                    if (t >= 1f && !Ledger.HasInFlight(ReceiverLedger)) EndCycle();
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
            tool.localScale = Vector3.one;
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
            if (!m.Liquid.IsEmpty) Ledger.Emit(LedgerMeasure, ReceiverLedger, m.LiquidMl, 0, DrinkPhysicsSim.Gentleness(draft.Technique));
            foreach (var id in new List<string>(m.LeavesG.Keys)) Ledger.EmitLeaves(LedgerMeasure, LedgerVessel, id, m.Leaf(id), 0);
            Ledger.Flush(LedgerVessel);
            Ledger.Flush(LedgerServe);
            bottleFall.Cut(); toolFall.Cut();
            EndCycle();
        }

        void OnDisable() { StopPour(); HideCloseUp(); }
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
            HideCloseUp();
            // 확정 배합의 기법에 맞는 그릇과 장부인지 마지막으로 맞춘다(불러온 레시피를 바로 제조하는 경우 등).
            Ledger.Flush();
            EnsureVessel(r);
            vessel.transform.localRotation = Quaternion.identity;
            DrinkVesselView.SetPivotKeepPosition(vessel.Rect, new Vector2(0.5f, 0f));
            vessel.Rect.anchoredPosition = StandOnTable(vessel, VesselRest.x);
            vessel.SetIce(r.Kind == DrinkKind.Cocktail && (r.Technique == DrinkTechnique.Build || r.Technique == DrinkTechnique.Layer) ? r.ice : 0);
            PoseDripper(0f);
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
                case DrinkTechnique.Brew:
                    if (!skipRequested) yield return BrewCoffee(r);
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

        // ───── 커피 추출 도구(손 없음): 에스프레소 머신 / 드리퍼 + 서버 / 모카포트 + 버너 ─────
        Image dripper, dripperGrounds, flame;
        Image grinderBody, grinderHopper, grinderCup, grinderCrank, grinderHandle;
        Image[] grinderGrounds = new Image[5];
        Vector2 grinderDoseTarget;
        float dripperAside;
        /// <summary>드리퍼를 서버 위에서 들어 옆(오른쪽 탁자)에 내려놓는 거리(장면 px)와 들어 올리는 높이.</summary>
        static readonly Vector2 DripperAside = new Vector2(150f, 0f);
        const float DripperLift = 60f;
        /// <summary>커피 추출 단계 이름(QA·시험). 끝나면 null.</summary>
        public string CoffeeStage { get; private set; }
        /// <summary>드리퍼 내려놓기 진행 0(서버 위)~1(옆 탁자).</summary>
        public float DripperAside01 => dripperAside;
        /// <summary>핸드 드립 물줄기가 드리퍼 중심에서 좌우로 이동한 최대 거리(px, QA).</summary>
        public float KettleSweepMaxOffset { get; private set; }
        public bool MokaGrinderVisible => grinderBody != null && grinderBody.transform.parent.gameObject.activeSelf;
        public bool MokaGroundsTransferVisible => grinderGrounds != null && Array.Exists(grinderGrounds, p => p != null && p.gameObject.activeSelf);
        public bool DripperGroundsVisible => dripperGrounds != null && dripperGrounds.gameObject.activeSelf;
        public float DripperGroundsAnchorY => dripperGrounds != null ? dripperGrounds.rectTransform.anchorMin.y : -1f;

        static bool NeedsKettle(DrinkRecipe r) => r.Kind == DrinkKind.Tea || (r.Kind == DrinkKind.Coffee && r.coffeeMethod == CoffeeBrewMethod.PourOver);

        /// <summary>추출 도구가 바뀔 때(EnsureVessel) 딸린 부품을 만든다: 핸드 드립 = 서버 위 드리퍼, 모카포트 = 아래 버너 불꽃.</summary>
        void SetupCoffeeParts()
        {
            if (dripper != null) { Destroy(dripper.gameObject); dripper = null; dripperGrounds = null; }
            if (flame != null) { Destroy(flame.gameObject); flame = null; }
            if (grinderBody != null) { Destroy(grinderBody.transform.parent.gameObject); grinderBody = grinderHopper = grinderCup = grinderCrank = grinderHandle = null; grinderGrounds = new Image[5]; }
            dripperAside = 0f;
            if (vesselId == CoffeeBrewing.ApparatusArt(CoffeeBrewMethod.EspressoMachine) && DrinkArt.Has(CoffeeBrewing.PortafilterArt))
            {
                // Use a pixel-perfect composite sprite: the portafilter is already seated under the group head.
                // Keep the base machine asset ID for its liquid anchors and masks; only swap the rendered sprite.
                vessel.Glass.sprite = DrinkArt.Sprite("dl_coffee_espresso_portafilter") ?? DrinkArt.Sprite(vesselId);
            }
            if (vesselId == CoffeeBrewing.ApparatusArt(CoffeeBrewMethod.PourOver))
            {
                dripper = AddImage("Dripper", scene, DrinkArt.SpriteOrPlaceholder(CoffeeBrewing.DripperArt, out bool ph), Vector2.zero);
                Placeholders |= ph;
                NativeSize(dripper, 4);
                dripper.rectTransform.pivot = new Vector2(0.5f, 0f);
                // 드리퍼 가루 더미: 드리퍼 원뿔 안(입구 아래) 원두 양만큼 커진다.
                dripperGrounds = AddImage("DripperGrounds", dripper.rectTransform, DrinkVesselView.White, new Vector2(30f, 8f));
                dripperGrounds.rectTransform.anchorMin = dripperGrounds.rectTransform.anchorMax = new Vector2(0.5f, 0.28f);
                dripperGrounds.rectTransform.pivot = new Vector2(0.5f, 0f);
                dripperGrounds.color = DrinkIngredients.Get(DrinkIngredients.CoffeeBean).color;
                dripperGrounds.gameObject.SetActive(false); // 분쇄 원두가 드리퍼 바닥에 얇게 깔린다.
                PoseDripper(0f);
            }
            else if (vesselId == CoffeeBrewing.ApparatusArt(CoffeeBrewMethod.MokaPot))
            {
                SetupMokaGrinder();
                var frames = DrinkArt.Frames(CoffeeBrewing.FlameArt);
                flame = AddImage("Flame", scene, frames.Length > 0 ? frames[0] : DrinkVesselView.White, new Vector2(64f, 32f));
                if (frames.Length > 0) NativeSize(flame, 4); else flame.color = new Color(1f, 0.55f, 0.15f, 0.9f);
                flame.rectTransform.pivot = new Vector2(0.5f, 0f);
                flame.rectTransform.anchoredPosition = new Vector2(VesselRest.x, TableTopY - 2f);
                flame.transform.SetSiblingIndex(0); // 모카포트 뒤(바닥 아래쪽에서 핥아 오른다)
                flame.gameObject.SetActive(false);
            }
        }

        /// <summary>작은 수동 버 그라인더. 독립된 픽셀 블록으로 구성해 분쇄와 바스켓 투입을 보여준다.</summary>
        void SetupMokaGrinder()
        {
            var group = new GameObject("MokaHandGrinder", typeof(RectTransform));
            var rt = (RectTransform)group.transform;
            rt.SetParent(scene, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = new Vector2(VesselRest.x - 145f, TableTopY - 76f);
            grinderDoseTarget = vessel.PointInParent(DrinkArt.Anchor(vesselId, "mouth")) - rt.anchoredPosition;
            var caseOutline = AddImage("GrinderCaseOutline", rt, DrinkVesselView.White, new Vector2(56f, 62f));
            caseOutline.color = new Color(0.16f, 0.095f, 0.075f);
            caseOutline.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            grinderBody = AddImage("GrinderBody", rt, DrinkVesselView.White, new Vector2(48f, 54f));
            grinderBody.color = new Color(0.42f, 0.24f, 0.14f);
            grinderBody.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            var bodyPanel = AddImage("GrinderFrontPanel", rt, DrinkVesselView.White, new Vector2(30f, 30f));
            bodyPanel.color = new Color(0.56f, 0.34f, 0.19f);
            bodyPanel.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            var panelInset = AddImage("GrinderPanelInset", rt, DrinkVesselView.White, new Vector2(20f, 18f));
            panelInset.color = new Color(0.3f, 0.17f, 0.11f);
            panelInset.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            var hopperOutline = AddImage("GrinderHopperOutline", rt, DrinkVesselView.White, new Vector2(40f, 21f));
            hopperOutline.color = new Color(0.17f, 0.11f, 0.075f);
            hopperOutline.rectTransform.anchoredPosition = new Vector2(0f, 27f);
            grinderHopper = AddImage("GrinderHopper", rt, DrinkVesselView.White, new Vector2(34f, 16f));
            grinderHopper.color = new Color(0.72f, 0.49f, 0.24f);
            grinderHopper.rectTransform.anchoredPosition = new Vector2(0f, 27f);
            var cupOutline = AddImage("GrinderCupOutline", rt, DrinkVesselView.White, new Vector2(44f, 24f));
            cupOutline.color = new Color(0.16f, 0.095f, 0.075f);
            cupOutline.rectTransform.anchoredPosition = new Vector2(0f, -32f);
            grinderCup = AddImage("GrinderCatchCup", rt, DrinkVesselView.White, new Vector2(38f, 20f));
            grinderCup.color = new Color(0.76f, 0.62f, 0.43f);
            grinderCup.rectTransform.anchoredPosition = new Vector2(0f, -32f);
            var cupGrounds = AddImage("GroundCoffeeInCup", rt, DrinkVesselView.White, new Vector2(24f, 5f));
            cupGrounds.color = new Color(0.19f, 0.09f, 0.035f);
            cupGrounds.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            var gear = AddImage("GrinderGear", rt, DrinkVesselView.White, new Vector2(12f, 12f));
            gear.color = new Color(0.83f, 0.68f, 0.35f);
            gear.rectTransform.anchoredPosition = new Vector2(21f, 18f);
            grinderCrank = AddImage("GrinderCrank", rt, DrinkVesselView.White, new Vector2(5f, 30f));
            grinderCrank.color = new Color(0.72f, 0.72f, 0.67f);
            grinderCrank.rectTransform.pivot = new Vector2(0.5f, 0f);
            grinderCrank.rectTransform.anchoredPosition = new Vector2(21f, 18f);
            grinderHandle = AddImage("GrinderCrankHandle", grinderCrank.transform, DrinkVesselView.White, new Vector2(12f, 9f));
            grinderHandle.color = new Color(0.22f, 0.19f, 0.17f);
            grinderHandle.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            for (int i = 0; i < grinderGrounds.Length; i++)
            {
                grinderGrounds[i] = AddImage("GroundParticle" + i, rt, DrinkVesselView.White, new Vector2(5f, 5f));
                grinderGrounds[i].color = new Color(0.19f, 0.09f, 0.035f);
                grinderGrounds[i].gameObject.SetActive(false);
            }
            group.SetActive(false);
        }

        void AnimateMokaGrinding(float t, bool dosing)
        {
            if (grinderBody == null) return;
            var group = grinderBody.transform.parent;
            if (!group.gameObject.activeSelf) group.gameObject.SetActive(true);
            grinderCrank.rectTransform.localRotation = Quaternion.Euler(0f, 0f, dosing ? 0f : Mathf.Sin(t * 18f) * 58f);
            for (int i = 0; i < grinderGrounds.Length; i++)
            {
                var particle = grinderGrounds[i];
                bool active = dosing && t > i * 0.11f;
                particle.gameObject.SetActive(active);
                if (!active) continue;
                float p = Mathf.Clamp01((t - i * 0.11f) / 0.42f);
                var start = new Vector2((i - 2) * 5f, -21f);
                var end = grinderDoseTarget + new Vector2((i - 2) * 3f, 8f);
                particle.rectTransform.anchoredPosition = Vector2.Lerp(start, end, p) + Vector2.up * (Mathf.Sin(p * Mathf.PI) * 26f);
                particle.color = new Color(0.19f, 0.09f, 0.035f, 1f - 0.5f * p);
            }
        }

        /// <summary>드리퍼 자세: 0 = 서버 입구 위, 1 = 들어 올렸다가 오른쪽 탁자에 내려놓음(호를 그리며, 손 없이).</summary>
        void PoseDripper(float aside)
        {
            if (dripper == null || vessel == null) return;
            dripperAside = Mathf.Clamp01(aside);
            var seat = vessel.PointInParent(DrinkArt.Anchor(vessel.AssetId, "mouth")) + new Vector2(0f, -6f);
            var ground = new Vector2(seat.x + DripperAside.x, TableTopY - DrinkArt.AlphaBottom(CoffeeBrewing.DripperArt) * 4f);
            float u = Ease(dripperAside);
            var p = Vector2.Lerp(seat, ground, u) + new Vector2(0f, DripperLift * Mathf.Sin(Mathf.PI * u));
            dripper.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            dripper.transform.SetAsLastSibling();
        }

        /// <summary>드리퍼 안 가루 더미(원두 g에 비례, 최대 원두량에서 가득).</summary>
        void UpdateDripperGrounds(double g)
        {
            if (dripperGrounds == null) return;
            bool on = g > 0.0005;
            dripperGrounds.gameObject.SetActive(on);
            if (!on) return;
            float t = Mathf.Clamp01((float)g * 10f / CoffeeBrewing.DoseMaxTenths(CoffeeBrewMethod.PourOver));
            dripperGrounds.rectTransform.localScale = new Vector3(0.45f + 0.55f * t, 0.3f + 0.7f * t, 1f);
        }

        /// <summary>커피 잔 자리(편집부터 보임 — 스팀 우유를 먼저 담는다). 에스프레소 = 추출구 바로 아래 받침 위,
        /// 핸드 드립·모카포트 = 도구 오른쪽(따르는 주둥이 쪽, PourInto가 붓는 자리와 같다). empty면 빈 잔으로(편집 화면 다시 그림).</summary>
        void PlaceCoffeeCup(DrinkRecipe r, bool empty)
        {
            if (r.Kind != DrinkKind.Coffee || vessel == null) return;
            if (empty || serve == null || serve.AssetId != ServeFor(r)) EnsureServe(r);
            DrinkVesselView.SetPivotKeepPosition(serve.Rect, new Vector2(0.5f, 0f));
            serve.transform.localRotation = Quaternion.identity;
            if (r.coffeeMethod == CoffeeBrewMethod.EspressoMachine)
            {
                var spout = vessel.PointInParent(DrinkArt.Anchor(vessel.AssetId, "pourLip"));
                serve.Rect.anchoredPosition = StandOnTable(serve, spout.x) + new Vector2(0f, EspressoTrayPx);
            }
            else serve.Rect.anchoredPosition = StandOnTable(serve, VesselRest.x + CupOffsetX);
            serve.gameObject.SetActive(true);
            serve.transform.SetAsLastSibling();
        }

        /// <summary>에스프레소 머신 받침(드립 트레이, 아트 아래 6행 × 4배) 높이(장면 px): 잔은 탁자가 아니라 머신 받침 위에 선다.</summary>
        const float EspressoTrayPx = 24f;
        /// <summary>핸드 드립·모카포트 잔의 도구 기준 가로 거리(PourInto의 받는 잔 거리와 같다).</summary>
        const float CupOffsetX = 270f;

        // ───── 에스프레소 추출 클로즈업(에스프레소 머신의 추출 단계에만): 살짝 위에서 비스듬히 본 추출구 → 잔 ─────
        /// <summary>클로즈업 아트 id(ArtSource에서 만들어 Resources/DrinkLab에 들어온다). 없으면 단색 도형으로 대신한다.</summary>
        public const string CloseUpGroupheadArt = "dl_coffee_extraction_grouphead", CloseUpCupRimArt = "dl_coffee_extraction_cup_rim", CloseUpLiquidArt = "dl_coffee_extraction_liquid";
        /// <summary>추출 흐름 시간(앞 0.4초 예비 적심 포함)과 클로즈업이 걷히는 시간.</summary>
        public const float EspressoExtractSeconds = 3.0f, CloseUpFadeSeconds = 0.3f;
        /// <summary>목표 추출량 중 이 비율까지는 끊긴 첫 방울, 그 뒤 이어진 줄기(시간이 아니라 실제 나온 양 기준).</summary>
        public const float FirstDropsFraction = 0.08f;
        const int CloseUpDropPool = 8;

        RectTransform closeUp, cuFrame;
        CanvasGroup cuGroup;
        Image cuGrouphead, cuRim, cuInside, cuLiquid;
        /// <summary>쌍둥이 추출구의 두 줄기(같은 양·색으로 나눠 흐른다).</summary>
        readonly Image[] cuStreams = new Image[2];
        Image cuStream => cuStreams[0];
        float cuOutletDx;
        int cuNextOutlet;
        readonly Image[] cuDrops = new Image[CloseUpDropPool];
        readonly float[] cuDropX = new float[CloseUpDropPool], cuDropY = new float[CloseUpDropPool], cuDropV = new float[CloseUpDropPool];
        Vector2 cuSpout, cuCupCenter, cuInner;
        double cuYield, cuMilkMl, cuEmitted, cuSinceDrop, cuShadeSum;
        Color cuMilkColor, cuBrew;
        float cuCapacity, cuAge, cuFlow, cuStreamTop, cuStreamV;
        bool cuFadingOut;
        static Sprite ellipseSprite, ringSprite;

        /// <summary>클로즈업이 보이는 중(QA·시험).</summary>
        public bool CloseUpActive => closeUp != null && closeUp.gameObject.activeSelf;
        /// <summary>prewet → drops → stream → tail → settle. 클로즈업이 없으면 null.</summary>
        public string CloseUpPhase { get; private set; }
        /// <summary>추출구에서 나온 양 / 목표 추출량(장부 Emit 합).</summary>
        public float CloseUpExtracted01 { get; private set; }
        /// <summary>잔에 실제로 닿은 추출 커피 / 목표 추출량(잔 장부 − 먼저 담은 첨가 액체).</summary>
        public float CloseUpLanded01 { get; private set; }
        /// <summary>잔 장부 액체량 / 잔 용량(첨가 액체 포함) — 클로즈업 수면 높이.</summary>
        public float CloseUpLevel01 { get; private set; }
        public Color CloseUpLiquidColor => cuLiquid != null ? cuLiquid.color : Color.clear;
        public Color CloseUpStreamColor => cuStream != null ? cuStream.color : Color.clear;
        /// <summary>지금 추출구에서 나오는 흐름 색(첫 방울·줄기).</summary>
        public Color CloseUpFlowColor { get; private set; }
        public bool CloseUpStreamVisible => cuStream != null && cuStream.gameObject.activeSelf;
        public int CloseUpDropsVisible { get { int n = 0; foreach (var d in cuDrops) if (d != null && d.gameObject.activeSelf) n++; return n; } }
        /// <summary>지금 클로즈업이 실제 아트 세 장으로 그려졌는지(아니면 도형 대체).</summary>
        public bool CloseUpUsesArt => cuGrouphead != null && cuGrouphead.sprite == DrinkArt.Sprite(CloseUpGroupheadArt) && cuRim.sprite == DrinkArt.Sprite(CloseUpCupRimArt) && cuLiquid.sprite == DrinkArt.Sprite(CloseUpLiquidArt) && cuGrouphead.sprite != null;
        /// <summary>클로즈업 세 아트가 모두 들어왔는지(아니면 도형 대체).</summary>
        public static bool CloseUpArtReady => DrinkArt.Has(CloseUpGroupheadArt) && DrinkArt.Has(CloseUpCupRimArt) && DrinkArt.Has(CloseUpLiquidArt);

        /// <summary>
        /// 추출 진행(나온 양 비율 0~1)에 따른 줄기 색. 첫 방울은 어둡고, 가운데는 진한 갈색, 끝물은 약간만 밝아질 수 있다.
        /// 보편적인 색·멈춤 색은 없으므로 기본 추출색(BrewColor)의 명도만 절제해서 바꾸고(색상 유지),
        /// 전체 추출량의 부피 평균이 기본 추출색과 같게 정규화한다(완성 잔 색과 어긋나지 않음). 화면 표시 전용.
        /// </summary>
        public static Color EspressoFlowColor(Color brew, float extracted01)
        {
            float k = FlowShade(Mathf.Clamp01(extracted01)) / FlowShadeMean;
            return new Color(Mathf.Clamp01(brew.r * k), Mathf.Clamp01(brew.g * k), Mathf.Clamp01(brew.b * k), brew.a);
        }

        static float FlowShade(float p) =>
            Mathf.Lerp(0.84f, 1f, Mathf.SmoothStep(0f, 1f, p / 0.4f)) + 0.12f * Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.3f);

        static float flowShadeMean = -1f;
        static float FlowShadeMean
        {
            get
            {
                if (flowShadeMean > 0f) return flowShadeMean;
                const int n = 400;
                double s = 0;
                for (int i = 0; i < n; i++) s += FlowShade((i + 0.5f) / n);
                return flowShadeMean = (float)(s / n);
            }
        }

        static Sprite ShapeSprite(bool ring)
        {
            const int w = 96, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                    float d = dx * dx + dy * dy;
                    bool on = ring ? d <= 1f && d >= 0.62f : d <= 1f;
                    px[y * w + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>클로즈업 아트 배율(정수, 세 아트 공통 — 픽셀 크기가 같다). 추출구~림이 정보 띠 위 영역에 들어간다.</summary>
        const int CloseUpArtScale = 3;
        /// <summary>그룹헤드 아트의 쌍둥이 추출구 간격(아트 px, 가운데 pourLip에서 좌우 — manifest stream_left/right 52·70, 가운데 61).</summary>
        const float TwinOutletArtPx = 9f;

        /// <summary>아트가 있으면 공통 정수 배율 원래 크기, 없으면 단색 도형.</summary>
        Image CloseUpPart(string name, string art, Sprite fallback, Vector2 fallbackSize, Color fallbackColor)
        {
            var sp = DrinkArt.Sprite(art);
            var img = AddImage(name, cuFrame, sp ?? fallback, fallbackSize);
            if (sp != null) NativeSize(img, CloseUpArtScale);
            else img.color = fallbackColor;
            return img;
        }

        void BuildCloseUp()
        {
            if (ellipseSprite == null) ellipseSprite = ShapeSprite(false);
            if (ringSprite == null) ringSprite = ShapeSprite(true);
            closeUp = UiFactory.Stretch("EspressoCloseUp", root);
            closeUp.SetSiblingIndex(scene.GetSiblingIndex() + 1); // 장면 위, 안내 글자 아래
            cuGroup = closeUp.gameObject.AddComponent<CanvasGroup>();
            cuGroup.blocksRaycasts = false; cuGroup.interactable = false;
            UiFactory.AddImage(closeUp, new Color(0.07f, 0.055f, 0.05f, 1f)).raycastTarget = false; // 완전히 덮는 컷(뒤 장면이 비치지 않음)
            AddCloseUpBackdrop();
            // 정보 띠·상판 위 영역의 가운데.
            cuFrame = UiFactory.Place("Frame", closeUp, new Vector2(0.5f, 0.5f), new Vector2(0f, (HudHeight + TableHeight) * 0.5f), new Vector2(560f, 560f));
            bool headArt = DrinkArt.Has(CloseUpGroupheadArt), rimArt = DrinkArt.Has(CloseUpCupRimArt);
            // 그릴 순서: 그룹헤드 / 잔 안쪽(통합 쪽에서 그림) → 액체 → 림 고리 → 줄기·방울(림 안쪽 수면에서 끝나므로 맨 앞).
            cuGrouphead = CloseUpPart("Grouphead", CloseUpGroupheadArt, DrinkVesselView.White, new Vector2(300f, 120f), new Color(0.55f, 0.56f, 0.58f));
            var gh = cuGrouphead.rectTransform;
            gh.anchoredPosition = new Vector2(0f, 280f - gh.sizeDelta.y * 0.5f);
            Vector2 lipN = headArt ? DrinkArt.Anchor(CloseUpGroupheadArt, "pourLip") : new Vector2(0.5f, 0f);
            if (lipN.y > 0.999f) lipN = new Vector2(0.5f, 0f); // 앵커가 없으면 아래 가운데(추출구)
            if (!headArt)
            {
                var spout = AddImage("Spout", gh, DrinkVesselView.White, new Vector2(48f, 26f));
                spout.color = new Color(0.32f, 0.32f, 0.34f);
                spout.rectTransform.anchorMin = spout.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                spout.rectTransform.pivot = new Vector2(0.5f, 1f);
                spout.rectTransform.anchoredPosition = Vector2.zero;
                lipN = new Vector2(0.5f, -26f / gh.sizeDelta.y);
            }
            cuSpout = gh.anchoredPosition + Vector2.Scale(lipN - new Vector2(0.5f, 0.5f), gh.sizeDelta);
            cuOutletDx = headArt ? TwinOutletArtPx * CloseUpArtScale : 16f;
            // 잔: 살짝 위에서 본 타원 림(가운데가 뚫린 고리). 잔 안쪽 그늘이 액체 뒤를 채운다.
            var rimSp = DrinkArt.Sprite(CloseUpCupRimArt);
            Vector2 rimSize = rimSp != null ? rimSp.rect.size * CloseUpArtScale : new Vector2(300f, 116f);
            Vector2 rimCenter = new Vector2(0f, -280f + rimSize.y * (0.25f + 0.62f) + 20f); // 아래 몸통(0.62)·바닥 타원(0.25) 자리를 남긴다
            Rect open = rimArt ? DrinkArt.LiquidRect(CloseUpCupRimArt) : new Rect(0.19f, 0.19f, 0.62f, 0.62f);
            Vector2 mouthN = rimArt ? DrinkArt.Anchor(CloseUpCupRimArt, "mouth") : new Vector2(0.5f, 0.5f);
            cuCupCenter = rimCenter + Vector2.Scale(mouthN - new Vector2(0.5f, 0.5f), rimSize);
            // 잔 몸통(림 아트는 고리만): 림 아래로 좁아지는 도자기 몸통 + 오른쪽 손잡이. 림과 같은 크림색의 그늘 톤.
            var bodyColor = new Color(0.86f, 0.8f, 0.69f);
            float bw = Mathf.Round(rimSize.x * 0.84f / CloseUpArtScale) * CloseUpArtScale, bh = Mathf.Round(rimSize.y * 0.62f / CloseUpArtScale) * CloseUpArtScale;
            var handle = AddImage("CupHandle", cuFrame, ringSprite, new Vector2(bh * 0.7f, bh * 0.8f));
            handle.color = bodyColor;
            handle.rectTransform.anchoredPosition = rimCenter + new Vector2(bw * 0.5f, -bh * 0.55f);
            var body = AddImage("CupBody", cuFrame, DrinkVesselView.White, new Vector2(bw, bh));
            body.color = bodyColor;
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.anchoredPosition = rimCenter;
            var bottom = AddImage("CupBottom", cuFrame, ellipseSprite, new Vector2(bw, Mathf.Round(rimSize.y * 0.5f / CloseUpArtScale) * CloseUpArtScale));
            bottom.color = bodyColor;
            bottom.rectTransform.anchoredPosition = rimCenter + new Vector2(0f, -bh);
            var shade = AddImage("CupShade", body.rectTransform, DrinkVesselView.White, new Vector2(Mathf.Round(bw * 0.22f / CloseUpArtScale) * CloseUpArtScale, bh));
            shade.color = new Color(0.72f, 0.65f, 0.55f);
            shade.rectTransform.anchorMin = shade.rectTransform.anchorMax = shade.rectTransform.pivot = new Vector2(1f, 1f);
            cuInside = AddImage("CupInside", cuFrame, ellipseSprite, Vector2.Scale(open.size, rimSize) * 1.08f);
            cuInside.color = new Color(0.6f, 0.52f, 0.43f);
            cuInside.rectTransform.anchoredPosition = rimCenter + Vector2.Scale(open.center - new Vector2(0.5f, 0.5f), rimSize);
            cuLiquid = CloseUpPart("Liquid", CloseUpLiquidArt, ellipseSprite, Vector2.Scale(open.size, rimSize) * 1.1f, Color.white);
            cuRim = CloseUpPart("CupRim", CloseUpCupRimArt, ringSprite, rimSize, new Color(0.95f, 0.93f, 0.88f));
            cuRim.rectTransform.anchoredPosition = rimCenter;
            cuInner = cuLiquid.rectTransform.sizeDelta;
            for (int k = 0; k < 2; k++)
            {
                cuStreams[k] = AddImage("Stream" + k, cuFrame, DrinkVesselView.White, new Vector2(6f, 10f));
                cuStreams[k].rectTransform.pivot = new Vector2(0.5f, 1f);
                cuStreams[k].gameObject.SetActive(false);
            }
            for (int i = 0; i < CloseUpDropPool; i++)
            {
                cuDrops[i] = AddImage("Drop" + i, cuFrame, ellipseSprite, new Vector2(2f, 3f) * CloseUpArtScale);
                cuDrops[i].gameObject.SetActive(false);
            }
            closeUp.gameObject.SetActive(false);
        }

        /// <summary>추출 클로즈업 배경 = 커피 제조대와 같은 제조 벽·상판 아트(정수 2배) 위에 따뜻한 어두운 막(줄기·방울 판독 우선). 아트가 없으면 단색 컷 그대로.</summary>
        void AddCloseUpBackdrop()
        {
            string set = CraftSet(DrinkKind.Coffee);
            var wall = DrinkArt.Sprite($"dl_craft_{set}_wall");
            var table = DrinkArt.Sprite($"dl_craft_{set}_table");
            if (wall == null || table == null) return;
            var wr = UiFactory.Place("CloseUpWall", closeUp, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(wall.rect.width * 2f, wall.rect.height * 2f));
            wr.pivot = new Vector2(0.5f, 1f);
            var wi = wr.gameObject.AddComponent<Image>();
            wi.sprite = wall; wi.raycastTarget = false;
            var tr = UiFactory.Place("CloseUpTable", closeUp, new Vector2(0.5f, 0f), new Vector2(0f, HudHeight), new Vector2(0f, TableHeight));
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f); tr.sizeDelta = new Vector2(0f, TableHeight);
            var ti = tr.gameObject.AddComponent<Image>();
            ti.sprite = table; ti.raycastTarget = false;
            var veil = UiFactory.Stretch("CloseUpVeil", closeUp).gameObject.AddComponent<Image>();
            veil.color = new Color(0.12f, 0.07f, 0.035f, 0.35f);
            veil.raycastTarget = false;
            cuBackdrop = wi;
        }

        Image cuBackdrop;
        /// <summary>클로즈업 뒤가 단색이 아니라 커피 제조 벽 아트인지(QA·시험).</summary>
        public bool CloseUpUsesBackdropArt => cuBackdrop != null && cuBackdrop.sprite != null && cuBackdrop.sprite == DrinkArt.Sprite($"dl_craft_{CraftSet(DrinkKind.Coffee)}_wall");

        void ShowCloseUp(DrinkRecipe r, double yieldMl)
        {
            if (closeUp == null) BuildCloseUp();
            cuYield = Math.Max(1e-6, yieldMl);
            cuMilkMl = Ledger[LedgerServe].LiquidMl; // 먼저 담아 둔 첨가 액체(스팀 우유)
            cuMilkColor = DrinkIngredients.Get(DrinkIngredients.SteamedMilk)?.color ?? Color.white;
            cuBrew = CoffeeBrewing.BrewColor(r);
            cuCapacity = Mathf.Max(1f, CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.EspressoMachine));
            cuEmitted = cuSinceDrop = cuShadeSum = 0;
            cuAge = 0f; cuFlow = 0f; cuFadingOut = false;
            cuStreamTop = cuSpout.y; cuStreamV = 0f;
            foreach (var d in cuDrops) d.gameObject.SetActive(false);
            foreach (var st in cuStreams) st.gameObject.SetActive(false);
            cuNextOutlet = 0;
            closeUp.gameObject.SetActive(true);
            finishLabel.enabled = false; // 단계 문구가 그룹헤드(머신 앞판) 위에 겹치지 않게 클로즈업 동안 숨긴다(문구 내용은 유지)
            closeUp.SetSiblingIndex(scene.GetSiblingIndex() + 1);
            cuGroup.alpha = 0f;
            CloseUpPhase = "prewet";
            CloseUpExtracted01 = CloseUpLanded01 = 0f;
            UpdateCloseUp(0, 0f);
        }

        void SetCloseUpAlpha(float a)
        {
            if (cuGroup == null) return;
            cuFadingOut = true;
            cuGroup.alpha = Mathf.Clamp01(a);
        }

        void HideCloseUp()
        {
            if (finishLabel != null) finishLabel.enabled = true; // 클로즈업이 닫히면 단계 문구 복원
            if (closeUp == null) return;
            closeUp.gameObject.SetActive(false);
            CloseUpPhase = null;
            cuFadingOut = false;
        }

        /// <summary>
        /// 클로즈업 한 프레임. emitted = 이번 프레임 추출구에서 나온 양(장부 Emit 결과). 줄기 색 = 지금까지 나온 양 비율의 흐름 색,
        /// 수면 높이 = 잔 장부의 실제 액체량, 수면 색 = 먼저 담은 우유 + 잔에 닿은 추출분(나온 순서대로의 흐름 색 평균).
        /// </summary>
        void UpdateCloseUp(double emitted, float dt)
        {
            if (!CloseUpActive) return;
            cuAge += dt;
            if (!cuFadingOut) cuGroup.alpha = Mathf.Clamp01(cuAge / CloseUpFadeSeconds);
            if (emitted > 0)
            {
                // 이번 덩어리의 가운데 진행도에서의 흐름 색(부피 가중 합 — 명도 비율만 쌓는다).
                double mid = (cuEmitted + emitted * 0.5) / cuYield;
                cuShadeSum += FlowShade(Mathf.Clamp01((float)mid)) / FlowShadeMean * emitted;
                cuEmitted += emitted;
                cuSinceDrop += emitted;
            }
            float p = Mathf.Clamp01((float)(cuEmitted / cuYield));
            CloseUpExtracted01 = p;
            double serveMl = Ledger[LedgerServe].LiquidMl;
            double landed = Math.Max(0, serveMl - cuMilkMl);
            CloseUpLanded01 = Mathf.Clamp01((float)(landed / cuYield));
            CloseUpLevel01 = Mathf.Clamp01((float)(serveMl / cuCapacity));
            // 순간 유량(부드럽게) → 줄기 굵기. 최고 유량 ≈ 목표량 / 흐름 시간 × 1.5(Ease 가운데).
            float rate = dt > 0f ? (float)(emitted / dt) : 0f;
            float peak = (float)(cuYield / (EspressoExtractSeconds - 0.4f) * 1.5);
            cuFlow = Mathf.Lerp(cuFlow, Mathf.Clamp01(rate / Mathf.Max(1e-3f, peak)), dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 0f);

            // 수면: 차오를수록 림 쪽으로 커지고 올라온다(위에서 비스듬히 본 잔).
            var lr = cuLiquid.rectTransform;
            float lv = CloseUpLevel01;
            bool any = serveMl > 1e-4;
            cuLiquid.gameObject.SetActive(any);
            float sc = Mathf.Lerp(0.5f, 1f, lv);
            float px = CloseUpArtScale; // 아트 픽셀 격자에 맞춘다
            lr.sizeDelta = new Vector2(Mathf.Round(cuInner.x * sc / px) * px, Mathf.Round(cuInner.y * sc / px) * px);
            lr.anchoredPosition = cuCupCenter + new Vector2(0f, Mathf.Round(-cuInner.y * 0.9f * (1f - sc) / px) * px);
            double coffeeMl = Math.Min(landed, cuEmitted);
            Color coffee = cuEmitted > 1e-9 ? Scale(cuBrew, (float)(cuShadeSum / cuEmitted)) : cuBrew;
            double tot = cuMilkMl + coffeeMl;
            Color surf = tot > 1e-9 ? Color.Lerp(cuMilkColor, coffee, (float)(coffeeMl / tot)) : coffee;
            surf.a = 1f;
            cuLiquid.color = surf;
            float surfY = any ? lr.anchoredPosition.y : cuCupCenter.y - cuInner.y * 0.25f;

            bool flowing = !Ledger[LedgerBrew].Liquid.IsEmpty && cuEmitted > 0;
            var flowColor = EspressoFlowColor(cuBrew, p);
            CloseUpFlowColor = flowColor;
            if (cuEmitted <= 0) CloseUpPhase = "prewet";
            else if (flowing) CloseUpPhase = p < FirstDropsFraction ? "drops" : "stream";
            else CloseUpPhase = Ledger.HasInFlight(LedgerServe) || cuStream.gameObject.activeSelf || CloseUpDropsVisible > 0 ? "tail" : "settle";

            // 첫 방울: 나온 양이 한 방울 몫만큼 쌓일 때마다 하나(시간 간격이 아니라 실제 양).
            double dropMl = cuYield * FirstDropsFraction / 6.0;
            if (CloseUpPhase == "drops")
                while (cuSinceDrop >= dropMl) { cuSinceDrop -= dropMl; SpawnDrop(flowColor); }
            for (int i = 0; i < CloseUpDropPool; i++)
            {
                var d = cuDrops[i];
                if (!d.gameObject.activeSelf) continue;
                cuDropV[i] += GravityPx * dt;
                cuDropY[i] -= cuDropV[i] * dt;
                if (cuDropY[i] <= surfY) { d.gameObject.SetActive(false); continue; }
                d.rectTransform.anchoredPosition = new Vector2(cuDropX[i], cuDropY[i]);
            }
            // 이어진 줄기: 나오는 동안 위는 추출구에 붙고, 멈추면 위끝이 중력으로 떨어져 수면에서 끝난다.
            if (CloseUpPhase == "stream") { cuStreamTop = cuSpout.y; cuStreamV = 0f; }
            else if (cuStream.gameObject.activeSelf) { cuStreamV += GravityPx * dt; cuStreamTop -= cuStreamV * dt; }
            bool streamOn = (CloseUpPhase == "stream" || CloseUpPhase == "tail") && cuStreamTop > surfY + 1f && (CloseUpPhase == "stream" || cuStream.gameObject.activeSelf);
            for (int k = 0; k < 2; k++)
            {
                var st = cuStreams[k];
                st.gameObject.SetActive(streamOn);
                if (!streamOn) continue;
                // 두 추출구에서 곧게 아래로(가로 속도 없음) — 수면 가운데 양옆에 닿는다. 굵기 = 유량(아트 1~2px).
                var sr = st.rectTransform;
                sr.anchoredPosition = new Vector2(cuSpout.x + (k == 0 ? -cuOutletDx : cuOutletDx), cuStreamTop);
                sr.sizeDelta = new Vector2(Mathf.Round(Mathf.Lerp(1f, 2f, cuFlow) * CloseUpArtScale), cuStreamTop - surfY);
                st.color = flowColor;
            }
        }

        static Color Scale(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        void SpawnDrop(Color c)
        {
            for (int i = 0; i < CloseUpDropPool; i++)
            {
                if (cuDrops[i].gameObject.activeSelf) continue;
                cuDropY[i] = cuSpout.y; cuDropV[i] = 0f;
                cuDrops[i].color = c;
                cuDropX[i] = cuSpout.x + (cuNextOutlet++ % 2 == 0 ? -cuOutletDx : cuOutletDx); // 두 추출구에서 번갈아
                cuDrops[i].rectTransform.anchoredPosition = new Vector2(cuDropX[i], cuSpout.y);
                cuDrops[i].gameObject.SetActive(true);
                return;
            }
        }

        /// <summary>
        /// 커피 추출 모션(손 없음, 도구가 화면 가운데). 장부: 원두는 이미 도구 안(LedgerVessel 잎 g),
        /// 추출된 커피는 커피 성분(TeaId, 색 = CoffeeBrewing.BrewColor)으로 만들어져 잔/서버로 흐른다. 추출량 = 레시피 물.
        /// </summary>
        IEnumerator BrewCoffee(DrinkRecipe r)
        {
            var m = r.coffeeMethod;
            double yieldMl = Math.Max(0, r.water) / 10.0;
            var brew = Ledger[LedgerBrew];
            brew.Clear();
            brew.Liquid.Pour(DrinkPhysicsSim.Part(DrinkPhysicsSim.TeaId, yieldMl), 1.0);
            var coffeeStream = StreamTint(CoffeeBrewing.BrewColor(r));
            HideCloseUp();
            craftFocus = vessel.Rect.anchoredPosition.x; // 도구가 가운데
            // 잔: 스팀 우유(있으면)를 먼저 담아 둔 채 제자리에(EnsureServe가 비운 장부를 배합으로 다시 채움).
            PlaceCoffeeCup(r, false);
            Ledger[LedgerServe].Liquid = DrinkPhysicsSim.CupAdditions(r);
            serve.SetLayers(LayersOf(LedgerServe, r));
            switch (m)
            {
                case CoffeeBrewMethod.EspressoMachine:
                {
                    CoffeeStage = "prepare";
                    finishLabel.text = "원두를 담은 포터필터가 그룹헤드에 장착되어 있습니다…";
                    float prep = 0f;
                    while (prep < 0.55f && !skipRequested) { prep += Time.unscaledDeltaTime; yield return null; }
                    CoffeeStage = "extract";
                    finishLabel.text = "에스프레소 추출 시작…";
                    // 추출 동안만 추출구·잔 클로즈업(고정 층, 장면 카메라는 그대로). 양·색은 장부의 실제 추출량에서.
                    ShowCloseUp(r, yieldMl);
                    const float dur = EspressoExtractSeconds;
                    double sent = 0;
                    float t = 0f;
                    while ((t < dur || !brew.Liquid.IsEmpty) && !skipRequested && t < dur + 2f)
                    {
                        float dt = Time.unscaledDeltaTime;
                        t += dt;
                        var lip = vessel.PointInParent(DrinkArt.Anchor(vessel.AssetId, "pourLip"));
                        float surf = ReceiverSurfaceY(serve);
                        // 처음 0.4초는 예비 적심(흐름 없음), 그 뒤 첫 방울 → 가는 줄기로.
                        double want = yieldMl * Ease(Mathf.Clamp01((t - 0.4f) / (dur - 0.4f))) - sent;
                        if (t >= dur) want = brew.LiquidMl;
                        double s = Ledger.Emit(LedgerBrew, LedgerServe, Math.Max(0, want), DrinkTransferLedger.FallSeconds(lip.y - surf, GravityPx), 0.5);
                        sent += s;
                        if (!brew.Liquid.IsEmpty && t > 0.4f) toolFall.Emit(lip, surf, 5f, StreamTint(EspressoFlowColor(CoffeeBrewing.BrewColor(r), (float)(sent / Math.Max(1e-6, yieldMl)))), serve.transform); else toolFall.Stop();
                        serve.SetLayers(LayersOf(LedgerServe, r));
                        serve.SetEffects(0f, 0f, 0.6f);
                        UpdateCloseUp(s, dt);
                        yield return null;
                    }
                    toolFall.Stop();
                    while (Ledger.HasInFlight(LedgerServe) && !skipRequested) { serve.SetLayers(LayersOf(LedgerServe, r)); UpdateCloseUp(0, Time.unscaledDeltaTime); yield return null; }
                    // 클로즈업이 걷히며 기존 장면(받침 위 잔)으로 돌아간 뒤 완성 잔으로 카메라가 옮겨 간다.
                    t = 0f;
                    while (t < CloseUpFadeSeconds && !skipRequested) { t += Time.unscaledDeltaTime; UpdateCloseUp(0, 0f); SetCloseUpAlpha(1f - t / CloseUpFadeSeconds); yield return null; }
                    HideCloseUp();
                    craftFocus = serve.Rect.anchoredPosition.x;
                    break;
                }
                case CoffeeBrewMethod.PourOver:
                {
                    var k = Ledger[LedgerKettle];
                    k.Clear();
                    k.Liquid.Pour(DrinkPhysicsSim.Part(DrinkPhysicsSim.WaterId, yieldMl), 1.0);
                    Ledger[LedgerDripper].Clear();
                    var kettleRt = kettle.rectTransform;
                    kettle.gameObject.SetActive(true);
                    CoffeeStage = "bloom";
                    finishLabel.text = "뜸 들이기…";
                    yield return PoseOver(kettleRt, "dl_water_kettle", kettleRest, ReceiverMouth(), 95f, 0.5f, false);
                    // 뜸(물 10%) → 잠시 멈춤 → 나머지를 천천히 원을 그리듯. 드리퍼가 받은 물만큼(조금 늦게) 커피가 서버로 떨어진다.
                    double total = k.LiquidMl, poured = 0, dripped = 0;
                    var kettlePourRest = kettleRt.anchoredPosition;
                    KettleSweepMaxOffset = 0f;
                    float t = 0f;
                    const float bloomPour = 0.5f, bloomWait = 0.8f, mainPour = 1.8f, end = bloomPour + bloomWait + mainPour;
                    while ((t < end || !brew.Liquid.IsEmpty) && !skipRequested && t < end + 4f)
                    {
                        float dt = Time.unscaledDeltaTime;
                        t += dt;
                        if (t > bloomPour + bloomWait && CoffeeStage == "bloom") { CoffeeStage = "pour"; finishLabel.text = "핸드 드립 — 천천히 붓는 중…"; }
                        double goal = t < bloomPour ? total * 0.1 * (t / bloomPour) : t < bloomPour + bloomWait ? total * 0.1
                            : total * (0.1 + 0.9 * Ease(Mathf.Clamp01((t - bloomPour - bloomWait) / mainPour)));
                        var lip = DrinkVesselView.ToParentAnchored(kettleRt, DrinkArt.Anchor("dl_water_kettle", "pourLip"));
                        var mouth = ReceiverMouth();
                        if (!k.Liquid.IsEmpty)
                        {
                            // 누적 이동 대신 고정 중심 기준으로 넓게 왕복해 드리퍼 전체에 물을 고르게 붓는다.
                            float sweep01 = Mathf.Clamp01((t - bloomPour - bloomWait) / mainPour);
                            float sweepX = Mathf.Sin(sweep01 * Mathf.PI * 4f) * 28f;
                            float sweepY = Mathf.Sin(sweep01 * Mathf.PI * 2f) * 4f;
                            kettleRt.anchoredPosition = kettlePourRest + new Vector2(sweepX, sweepY);
                            KettleSweepMaxOffset = Mathf.Max(KettleSweepMaxOffset, Mathf.Abs(sweepX));
                            double want = Math.Max(0, Math.Min(goal - poured, k.LiquidMl));
                            poured += Ledger.Emit(LedgerKettle, LedgerDripper, want, DrinkTransferLedger.FallSeconds(lip.y - mouth.y, GravityPx), 0.5);
                            if (want > 1e-6 || Ledger.HasInFlight(LedgerDripper)) toolFall.Emit(lip, mouth.y - 10f, 4f, StreamTint(DrinkIngredients.WaterColor), dripper.transform);
                            else toolFall.Stop();
                        }
                        else toolFall.Stop();
                        // 드리퍼 → 서버: 받은 물 중 가루가 머금은 몫(원두 g × 2mL)을 뺀 만큼만 커피로 떨어진다.
                        double held = Math.Min(Ledger[LedgerDripper].LiquidMl, r.LeafTotal() / 10.0 * 2.0 * Math.Min(1.0, t / 1.0));
                        double canDrip = Ledger[LedgerDripper].LiquidMl - held - dripped;
                        if (t >= end) canDrip = brew.LiquidMl;
                        if (canDrip > 1e-6)
                        {
                            double rate = yieldMl / mainPour * 1.4 * dt;
                            var outlet = DrinkVesselView.ToParentAnchored(dripper.rectTransform, DrinkArt.Anchor(CoffeeBrewing.DripperArt, "pourLip"));
                            float surf = ReceiverSurfaceY(vessel);
                            dripped += Ledger.Emit(LedgerBrew, LedgerVessel, Math.Min(canDrip, rate), DrinkTransferLedger.FallSeconds(outlet.y - surf, GravityPx), 0.5);
                            bottleFall.Emit(outlet, surf, 3f, coffeeStream, vessel.transform);
                        }
                        else bottleFall.Stop();
                        vessel.SetLayers(LayersOf(LedgerVessel, r));
                        UpdateDripperGrounds(Ledger[LedgerVessel].LeafG);
                        yield return null;
                    }
                    toolFall.Stop(); bottleFall.Stop();
                    yield return PoseOver(kettleRt, "dl_water_kettle", kettleRest, ReceiverMouth(), 95f, 0.45f, true);
                    while (Ledger.HasInFlight(LedgerVessel) && !skipRequested) { vessel.SetLayers(LayersOf(LedgerVessel, r)); yield return null; }
                    // 드리퍼를 들어 옆에 내려놓고(가루는 드리퍼에 남음) 서버의 커피를 머그에 따른다.
                    CoffeeStage = "dripperAside";
                    finishLabel.text = "드리퍼 내려놓기…";
                    t = 0f;
                    while (t < 0.6f && !skipRequested) { t += Time.unscaledDeltaTime; PoseDripper(t / 0.6f); yield return null; }
                    PoseDripper(1f);
                    Ledger[LedgerDripper].Clear();
                    CoffeeStage = "serve";
                    finishLabel.text = "머그에 따르는 중…";
                    yield return PourInto(vessel, serve, r, false);
                    break;
                }
                case CoffeeBrewMethod.MokaPot:
                {
                    var v = Ledger[LedgerVessel];
                    var frames = DrinkArt.Frames(CoffeeBrewing.FlameArt);
                    var steamFrames = DrinkArt.Frames("dl_steam");
                    // 모카포트는 추출 전에 원두를 갈고, 분쇄 원두를 바스켓에 담는 순서를 보여준다.
                    CoffeeStage = "grind";
                    finishLabel.text = "원두를 모카포트용으로 가는 중…";
                    float grindT = 0f;
                    while (grindT < 1.35f && !skipRequested)
                    {
                        grindT += Time.unscaledDeltaTime;
                        AnimateMokaGrinding(grindT, false);
                        yield return null;
                    }
                    CoffeeStage = "dose";
                    finishLabel.text = "곱게 간 원두를 모카포트 바스켓에 담는 중…";
                    grindT = 0f;
                    while (grindT < 1.0f && !skipRequested)
                    {
                        grindT += Time.unscaledDeltaTime;
                        AnimateMokaGrinding(grindT, true);
                        yield return null;
                    }
                    if (grinderBody != null) grinderBody.transform.parent.gameObject.SetActive(false);
                    CoffeeStage = "heat";
                    finishLabel.text = "모카포트 가열…";
                    if (flame != null) flame.gameObject.SetActive(true);
                    float t = 0f;
                    const float heat = 1.0f, rise = 2.0f;
                    double sent = 0;
                    while ((t < heat + rise || !brew.Liquid.IsEmpty) && !skipRequested && t < heat + rise + 2f)
                    {
                        float dt = Time.unscaledDeltaTime;
                        t += dt;
                        if (flame != null && frames.Length > 0) flame.sprite = frames[Mathf.FloorToInt(t * 10f) % frames.Length];
                        if (t > heat)
                        {
                            if (CoffeeStage == "heat") { CoffeeStage = "rise"; finishLabel.text = "증기압으로 커피가 올라오는 중…"; }
                            // 위 칸에 커피가 솟아 차오른다(장부: 추출 대기분 → 모카포트 위 칸, 낙하 없음). 보글거리는 김과 잔떨림.
                            double want = yieldMl * Ease(Mathf.Clamp01((t - heat) / rise)) - sent;
                            if (t >= heat + rise) want = brew.LiquidMl;
                            sent += Ledger.Emit(LedgerBrew, LedgerVessel, Math.Max(0, want), 0, 0.3);
                            steam.gameObject.SetActive(true);
                            AnimateSteam(steamFrames, vessel, t);
                            vessel.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * 1.2f * Mathf.Clamp01((t - heat) * 2f));
                        }
                        vessel.SetLayers(LayersOf(LedgerVessel, r));
                        yield return null;
                    }
                    vessel.transform.localRotation = Quaternion.identity;
                    if (flame != null) flame.gameObject.SetActive(false);
                    steam.gameObject.SetActive(false);
                    CoffeeStage = "serve";
                    finishLabel.text = "잔에 따르는 중…";
                    yield return PourInto(vessel, serve, r, false);
                    break;
                }
            }
            brew.Clear();
            CoffeeStage = null;
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
            if (v.LeafG > 0 && r.Kind != DrinkKind.Coffee) ShowLeafHeap(leafHeap, from, v.LeafG, FirstLeafId(v));
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
            HideCloseUp();
            spoon.gameObject.SetActive(false);
            craftLidOpen = false; lidLinger = 0f; lidT = 0f; PoseLid(); // 완성(건너뛰기 포함)은 덮인 다관
            if (flame != null) flame.gameObject.SetActive(false);
            if (r.Kind == DrinkKind.Coffee) PoseDripper(1f); // 완성: 드리퍼는 옆에 내려둔 상태
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
                    if ((r.Kind == DrinkKind.Tea || r.Kind == DrinkKind.Coffee) && leaf > 0)
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
            if (r.Kind == DrinkKind.Coffee && r.coffeeMethod == CoffeeBrewMethod.EspressoMachine) PlaceCoffeeCup(r, false);
            CoffeeStage = null;
            leafHeap.gameObject.SetActive(false);
            target.transform.localRotation = Quaternion.identity;
            target.SetLiquidHidden(false);
            target.SetLayers(LayersOf(targetName, r));
            target.SetEffects(0f, 0f, 0.1f);
            target.SetIce(r.Kind == DrinkKind.Cocktail ? r.ice : 0);
            bool hot = r.Kind == DrinkKind.Tea || r.Kind == DrinkKind.Coffee;
            steam.gameObject.SetActive(hot);
            if (hot) AnimateSteam(DrinkArt.Frames("dl_steam"), target, 0f);
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
            sb.Append(r.kind).Append('|').Append(r.technique).Append('|').Append(r.water).Append('|').Append(r.ice).Append('|').Append((int)r.coffeeMethod);
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
        float lateral = 4f, rotation = 220f, phase;
        public void Init(Vector2 from, Vector2 to, float dur, float lateral = 4f, float rotation = 220f) { this.from = from; this.to = to; this.dur = Mathf.Max(0.05f, dur); this.lateral = lateral; this.rotation = rotation; phase = UnityEngine.Random.Range(0f, 6.28f); }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            // 중력 낙하: 높이는 k² 곡선(처음 느리고 점점 빠르게)
            ((RectTransform)transform).anchoredPosition = new Vector2(Mathf.Lerp(from.x, to.x, k) + Mathf.Sin(k * 9f + phase) * lateral, Mathf.Lerp(from.y, to.y, k * k));
            transform.localRotation = Quaternion.Euler(0, 0, k * rotation);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
