using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>체력바 / 재료·원두 게이지 / 생존 시간·처치 수 / 보스 체력바 / 보스 대사 배너.</summary>
    public class HudView : MonoBehaviour
    {
        GameManager game;
        FillBar hpBar;
        FillBar gaugeBar;
        FillBar teaBar;
        FillBar beanBar;
        FillBar bossBar;
        Text info;
        Text coffeePoints;
        Text suppressed;
        GameObject suppressedBack;
        Text bannerTitle;
        Text bannerSub;
        Text schemeLabel;
        GameObject saveFailedRoot;
        Image slowIcon, suppressedIcon, sealIcon, bossInvulnerableIcon;
        Button pauseButton;
        float bannerRemaining;
        Text hiddenSummary, hiddenActivity, jobTrait, comboTarget, guide;
        GameObject guideRoot;
        float guideRemaining, pulseRemaining;
        IngredientType pulseType;
        GameObject hiddenDetails;
        RectTransform hiddenContent;
        float hiddenRefresh;
        /// <summary>현수막 폭 = 왼쪽 패널 오른쪽 끝(610) 대칭 사이 가운데 열.</summary>
        public const float BannerW = 680f;
        public bool HiddenDetailsOpen => hiddenDetails != null && hiddenDetails.activeSelf;
        /// <summary>2026-11-09 지금 적용 중인 음료·추출법·히든 상태 칸(실제 상태 기준).</summary>
        public ActiveEffectsStrip EffectsStrip { get; private set; }
        /// <summary>2026-11-09 런 구간·위험도·실제 보스 예고 배지.</summary>
        public RunPhaseBadge PhaseBadge { get; private set; }
        public FamilyLegend Legend { get; private set; }

        // ───── 2026-11-12 좌측 패널 그림 ─────
        Text hiddenBadge; Image hiddenBadgeBg, jobPortrait, comboCard, comboSlot; Image[] jobPips;
        public string HiddenBadgeText => hiddenBadge != null ? hiddenBadge.text : "";
        public bool JobPortraitShown => jobPortrait != null && jobPortrait.enabled;
        public int JobPipsFilled { get; private set; }
        public bool ComboCardShown => comboCard != null && comboCard.enabled;

        void BuildLeftVisuals(RectTransform hiddenRt, RectTransform jobRt, RectTransform targetRt, GameManager g)
        {
            if (g == null) return;
            // 히든: 왼쪽 둥근 배지(해금 수) — 글자 'H' 안내는 오른쪽 문구에 그대로
            var brt = UiFactory.Place("CountBadge", hiddenRt, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(42f, 42f));
            brt.pivot = new Vector2(0f, 0.5f);
            hiddenBadgeBg = brt.gameObject.AddComponent<Image>();
            hiddenBadgeBg.sprite = SpriteFactory.Circle; hiddenBadgeBg.color = new Color(0.85f, 0.65f, 0.25f, 1f); hiddenBadgeBg.raycastTarget = false;
            hiddenBadge = UiFactory.AddText(UiFactory.Stretch("N", brt), "0", 24, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.05f));
            hiddenBadge.raycastTarget = false;
            if (hiddenSummary != null) hiddenSummary.rectTransform.offsetMin = new Vector2(60f, 10f);
            // 직업: 몸체 첫 프레임 초상 + 진행 칸(바텐더 새 칵테일 3칸 / 티 소믈리에 깊은 우림 6칸)
            jobPortrait = HudIcons.Icon(jobRt, "Portrait", new Vector2(0f, 0.5f), new Vector2(10f, 0f), 90f, HudIcons.Job(g.Job));
            jobPortrait.rectTransform.pivot = new Vector2(0f, 0.5f);
            if (jobTrait != null) { jobTrait.rectTransform.offsetMin = new Vector2(110f, 30f); jobTrait.resizeTextForBestFit = true; jobTrait.resizeTextMinSize = 16; jobTrait.resizeTextMaxSize = 22; }
            int pipCount = g.Job == Job.Bartender ? 3 : 6;
            // 진행 칸 앞에 무엇을 세는지 그림(바텐더 = 칵테일잔 · 티 소믈리에 = 찻잔)
            var kindIcon = HudIcons.Icon(jobRt, "PipKind", new Vector2(0f, 0f), new Vector2(110f, 4f), 26f, HudIcons.FamilyCup(g.Job == Job.Bartender ? MonsterFamily.Cocktail : MonsterFamily.Tea));
            kindIcon.rectTransform.pivot = new Vector2(0f, 0f);
            jobPips = HudIcons.Pips(jobRt, "Pip", new Vector2(0f, 0f), new Vector2(142f, 10f), pipCount, 16f, 6f);
            // 목표 조합: 첫 미충족 카드 그림(없으면 빈 칸 테두리)
            comboSlot = UiFactory.AddImage(UiFactory.Place("CardSlot", targetRt, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(42f, 42f)), new Color(1f, 1f, 1f, 0.18f));
            comboSlot.rectTransform.pivot = new Vector2(0f, 0.5f); comboSlot.raycastTarget = false;
            comboCard = HudIcons.Icon(targetRt, "Card", new Vector2(0f, 0.5f), new Vector2(8f, 0f), 42f, null);
            comboCard.rectTransform.pivot = new Vector2(0f, 0.5f);
            if (comboTarget != null) comboTarget.rectTransform.offsetMin = new Vector2(60f, 10f);
        }

        void RefreshLeftVisuals()
        {
            if (game == null || game.Player == null) return;
            if (hiddenBadge != null)
            {
                int n = game.Player.Hidden.Count;
                hiddenBadge.text = n.ToString();
                hiddenBadgeBg.color = n > 0 ? new Color(0.85f, 0.65f, 0.25f, 1f) : new Color(0.4f, 0.36f, 0.32f, 1f); // 0개는 흐린 배지 + 숫자 0
            }
            if (jobPips != null)
            {
                int filled = game.Job == Job.Bartender ? (game.MixingRushRemaining > 0f ? 3 : game.MixingRushProgress) : game.DeepSteepLevel;
                JobPipsFilled = filled;
                HudIcons.SetPips(jobPips, filled, game.MixingRushRemaining > 0f ? new Color(1f, 0.55f, 0.35f) : new Color(0.75f, 1f, 0.65f), new Color(1f, 1f, 1f, 0.18f));
            }
            if (comboCard != null)
            {
                Sprite card = null;
                if (game.ComboTarget.HasValue)
                {
                    var def = HiddenSkillDatabase.Get(game.ComboTarget.Value);
                    foreach (var p in HiddenRequirements.Progress(def.skill, game.Job, game.HiddenState))
                        if (!p.met && p.req.kind == HiddenReqKind.Card) { var c = game.Cards?.FindInPool(p.req.id); if (c != null) { card = CardEducationIcons.For(c); break; } }
                    if (card == null) card = Resources.Load<Sprite>("Icons/coffee_bean"); // 카드 조건 충족 뒤 남은 커피 조건 등
                }
                HudIcons.Set(comboCard, card);
            }
        }

        static Text PaddedLabel(RectTransform box, string content, int size, TextAnchor alignment, Color color)
        {
            var rect = UiFactory.Stretch("Label", box);
            rect.offsetMin = new Vector2(16f, 10f);
            rect.offsetMax = new Vector2(-16f, -10f);
            var label = UiFactory.AddText(rect, content, size, alignment, color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        public static HudView Create(Transform canvas, GameManager game)
        {
            var root = UiFactory.Stretch("Hud", canvas);
            var view = root.gameObject.AddComponent<HudView>();
            view.game = game;

            view.hpBar = new FillBar(root, "HpBar", new Vector2(0f, 1f), new Vector2(30f, -30f),
                new Vector2(420f, 30f), new Color(0.85f, 0.25f, 0.3f), 22);
            // r13 검수 U3: 중앙 게이지 글자가 벽등·선반 위에 직접 놓여 줄마다 대비가 달랐다. 세 줄 뒤에 공통 어두운 바탕
            // 하나를 깐다(게이지보다 먼저 만들어 뒤에 그려지게, 입력은 막지 않음). 게이지 크기·위치·색은 그대로.
            var gaugeBackRt = UiFactory.Place("GaugeBacking", root, new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(940f, 112f)); // 피벗=상단: -20..-132, 세 바(-30..-122)를 감싼다
            UiFactory.AddImage(gaugeBackRt, new Color(0.06f, 0.05f, 0.08f, 0.82f)).raycastTarget = false;
            view.gaugeBar = new FillBar(root, "GaugeBar", new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(900f, 30f), new Color(0.9f, 0.35f, 0.75f), 22);
            view.teaBar = new FillBar(root, "TeaBar", new Vector2(0.5f, 1f), new Vector2(0f, -66f),
                new Vector2(900f, 26f), new Color(0.45f, 0.75f, 0.4f), 20);
            view.beanBar = new FillBar(root, "BeanBar", new Vector2(0.5f, 1f), new Vector2(0f, -98f),
                new Vector2(600f, 24f), new Color(0.65f, 0.42f, 0.25f), 20);
            // 47(2026-10-12): 찻집(티 소믈리에) 런은 칵테일 몹·재료가 없다 — 비어 있는 칵테일 바를 숨기고 찻잎·원두를 한 칸씩 올린다.
            if (game != null && game.Job == Job.TeaSommelier)
            {
                view.gaugeBar.Root.gameObject.SetActive(false);
                view.teaBar.Root.anchoredPosition = new Vector2(0f, -30f);
                view.beanBar.Root.anchoredPosition = new Vector2(0f, -62f);
                gaugeBackRt.sizeDelta = new Vector2(940f, 76f);
            }

            // 57 P1 첫 수집 안내(게이지 바로 아래, 입력 안 막음). RunGuide가 처음 몇 판만 띄운다.
            var guideRt = UiFactory.Place("Guide", root, new Vector2(0.5f, 1f), new Vector2(0f, game != null && game.Job == Job.TeaSommelier ? -102f : -138f),
                new Vector2(860f, 50f));
            UiFactory.AddImage(guideRt, new Color(0.1f, 0.2f, 0.24f, 0.92f)).raycastTarget = false;
            view.guide = PaddedLabel(guideRt, "", 22, TextAnchor.MiddleCenter, new Color(0.85f, 0.97f, 1f));
            var guideLabelRt = view.guide.rectTransform;
            guideLabelRt.offsetMin = new Vector2(10f, 2f);
            guideLabelRt.offsetMax = new Vector2(-10f, -2f);
            view.guide.resizeTextForBestFit = true; // 긴 안내도 한 상자 안(잘려서 빈 상자가 되지 않게)
            view.guide.resizeTextMinSize = 14;
            view.guide.resizeTextMaxSize = 22;
            view.guideRoot = guideRt.gameObject;
            view.guideRoot.SetActive(false);

            var infoRt = UiFactory.Place("Info", root, new Vector2(1f, 1f), new Vector2(-30f, -24f),
                new Vector2(320f, 90f));
            view.info = UiFactory.AddText(infoRt, "", 30, TextAnchor.UpperRight, Color.white);

            // 08:79 "상단 우측: 커피 미사용 포인트와 K 안내"
            // 2026-10-12: 찻집 "우림 연구 포인트 … [K] 우림 연구 트리"가 길고 다탁 그림 위에 바로 놓여 안 읽혔다 — 어두운 바탕+여백,
            // 넘치면 글자만 줄인다. 폭 450은 중앙 게이지 바탕(x 490..1430) 오른쪽, 높이는 Info(-114) 아래·Challenge(-164) 위.
            var pointsRt = UiFactory.Place("CoffeePoints", root, new Vector2(1f, 1f), new Vector2(-30f, -118f),
                new Vector2(450f, 42f));
            UiFactory.AddImage(pointsRt, new Color(.06f, .05f, .08f, .82f)).raycastTarget = false;
            var pointsLabel = UiFactory.Stretch("Label", pointsRt);
            pointsLabel.offsetMin = new Vector2(12f, 4f);
            pointsLabel.offsetMax = new Vector2(-12f, -4f);
            view.coffeePoints = UiFactory.AddText(pointsLabel, CoffeePointsLabel(0), 24, TextAnchor.MiddleRight,
                new Color(0.9f, 0.7f, 0.45f));
            view.coffeePoints.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.coffeePoints.verticalOverflow = VerticalWrapMode.Truncate;
            view.coffeePoints.resizeTextForBestFit = true;
            view.coffeePoints.resizeTextMinSize = 16;
            view.coffeePoints.resizeTextMaxSize = 24;

            // 08:75 상태아이콘(둔화/환불/봉인) — 04 §8 status_slow/status_suppressed, 09 seal_lock. 아이콘 오른쪽에 남은 초 문구.
            view.slowIcon = UiFactory.AddIcon(root, "status_slow", new Vector2(0f, 1f), new Vector2(48f, -70f), 40f);
            view.suppressedIcon = UiFactory.AddIcon(root, "status_suppressed", new Vector2(0f, 1f), new Vector2(92f, -70f), 40f);
            view.sealIcon = UiFactory.AddIcon(root, "seal_lock", new Vector2(0f, 1f), new Vector2(136f, -70f), 40f);
            foreach (var icon in new[] { view.slowIcon, view.suppressedIcon, view.sealIcon })
                if (icon != null) icon.gameObject.SetActive(false);
            bool hasStatusIcons = view.slowIcon != null;
            // 2026-10-12: 환불 무효 상태가 선반 그림 위 글자만이라 안 보였다 — 걸렸을 때만 어두운 바탕(입력 안 막음).
            var suppressBackRt = UiFactory.Place("SuppressedBacking", root, new Vector2(0f, 1f), new Vector2(hasStatusIcons ? 176f : 22f, -66f),
                new Vector2(hasStatusIcons ? 336f : 536f, 46f));
            UiFactory.AddImage(suppressBackRt, new Color(.06f, .05f, .08f, .86f)).raycastTarget = false;
            view.suppressedBack = suppressBackRt.gameObject;
            view.suppressedBack.SetActive(false);
            var suppressRt = UiFactory.Place("Suppressed", root, new Vector2(0f, 1f), new Vector2(hasStatusIcons ? 184f : 30f, -70f),
                new Vector2(hasStatusIcons ? 320f : 520f, 40f));
            view.suppressed = UiFactory.AddText(suppressRt, "", 26, TextAnchor.UpperLeft,
                new Color(1f, 0.4f, 0.45f));

            // 08:175 저장 실패 — 짧은 문구 + 다시 시도 버튼(디스크 정보는 로그로만). 5초 자동 재시도는 SaveRetryRunner.
            var saveRt = UiFactory.Place("SaveFailed", root, new Vector2(0f, 1f), new Vector2(48f, -116f),
                new Vector2(520f, 44f));
            var saveBg = UiFactory.AddImage(saveRt, new Color(0.4f, 0.12f, 0.1f, 0.9f));
            var saveButton = saveRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(saveButton);
            saveButton.targetGraphic = saveBg;
            UiFactory.ApplyHoverColors(saveButton);
            UiFactory.AddText(UiFactory.Stretch("Label", saveRt), $"⚠ {PermanentProgress.NoticeSaveFailed}", 24,
                TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.7f));
            UiFactory.AddIcon(saveRt, "ui_save_error", new Vector2(0f, 0.5f), new Vector2(4f, 0f), 36f); // 09 ui_save_error
            saveButton.onClick.AddListener(() => PermanentProgress.RetrySave());
            view.saveFailedRoot = saveRt.gameObject;
            view.saveFailedRoot.SetActive(PermanentProgress.LastSaveFailed);

            view.bossBar = new FillBar(root, "BossBar", new Vector2(0.5f, 0f), new Vector2(0f, 50f),
                new Vector2(1000f, 34f), new Color(0.8f, 0.15f, 0.2f), 24);
            view.bossBar.Root.gameObject.SetActive(false);
            view.BossIndicator = BossDirectionIndicator.Create(root, game); // 57 P0-1 화면 밖 보스 방향
            // 04 §8 status_invulnerable — 보스가 무적(등장 안내·투명화)일 때 체력바 왼쪽에 표시.
            view.bossInvulnerableIcon = UiFactory.AddIcon(view.bossBar.Root, "status_invulnerable", new Vector2(0f, 0.5f),
                new Vector2(-50f, 0f), 44f);

            // 08:76 상단 우측 일시정지 버튼(09 ui_pause). Esc와 같은 GameManager.OpenPause 경로.
            var pauseRt = UiFactory.Place("PauseButton", root, new Vector2(1f, 1f), new Vector2(-370f, -24f), new Vector2(72f, 72f));
            var pauseBg = UiFactory.AddImage(pauseRt, new Color(0f, 0f, 0f, 0.45f));
            view.pauseButton = pauseRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(view.pauseButton);
            view.pauseButton.targetGraphic = pauseBg;
            UiFactory.ApplyHoverColors(view.pauseButton);
            view.pauseButton.onClick.AddListener(() => { if (view.game != null) view.game.OpenPause(); });
            if (UiFactory.AddIcon(pauseRt, "ui_pause", new Vector2(0.5f, 0.5f), Vector2.zero, 60f) == null)
                UiFactory.AddText(UiFactory.Stretch("Label", pauseRt), "II", 34, TextAnchor.MiddleCenter, Color.white);

            // 2026-11-12 실제 빌드 720p: 폭 1400 현수막 부제가 왼쪽 패널(x 30~610, y 174~406) 밑으로 들어가 "[커] 커피 계열"이 가려졌다 →
            // 제목·부제를 양쪽 패널 사이 가운데 열(BannerW)로 제한하고 부제는 두 줄 줄바꿈 + 크기 맞춤.
            var titleRt = UiFactory.Place("BannerTitle", root, new Vector2(0.5f, 1f), new Vector2(0f, -202f),
                new Vector2(BannerW, 64f)); // 64 = 한 줄만 들어가는 높이 → 크기 맞춤이 줄바꿈 대신 글자를 줄인다
            view.bannerTitle = UiFactory.AddText(titleRt, "", 64, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
            view.bannerTitle.resizeTextForBestFit = true; view.bannerTitle.resizeTextMinSize = 30; view.bannerTitle.resizeTextMaxSize = 64;
            view.bannerTitle.horizontalOverflow = HorizontalWrapMode.Wrap; view.bannerTitle.verticalOverflow = VerticalWrapMode.Truncate; // 높이 제한이 있어야 크기 맞춤이 줄인다
            var subRt = UiFactory.Place("BannerSub", root, new Vector2(0.5f, 1f), new Vector2(0f, -278f),
                new Vector2(BannerW, 96f));
            view.bannerSub = UiFactory.AddText(subRt, "", 30, TextAnchor.UpperCenter, Color.white);
            view.bannerSub.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.bannerSub.resizeTextForBestFit = true; view.bannerSub.resizeTextMinSize = 22; view.bannerSub.resizeTextMaxSize = 30;
            view.bannerSub.verticalOverflow = VerticalWrapMode.Truncate;

            var schemeRt = UiFactory.Place("MovementScheme", root, new Vector2(0f, 0f), new Vector2(30f, 30f),
                new Vector2(290f, 84f));
            var schemeBg = UiFactory.AddImage(schemeRt, new Color(0f, 0f, 0f, 0.55f));
            var schemeButton = schemeRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(schemeButton);
            schemeButton.targetGraphic = schemeBg;
            UiFactory.ApplyHoverColors(schemeButton);
            view.schemeLabel = PaddedLabel(schemeRt, "", 22, TextAnchor.MiddleCenter, Color.white);
            schemeButton.onClick.AddListener(() =>
            {
                InputSettings.Toggle();
                view.RefreshSchemeLabel();
            });
            view.RefreshSchemeLabel();

            var hiddenRt = UiFactory.Place("HiddenSkillsButton", root, new Vector2(0f,1f), new Vector2(30f,-174f), new Vector2(500f,56f));
            var hiddenBg = UiFactory.AddImage(hiddenRt,new Color(.12f,.09f,.12f,.92f));
            UiFactory.SkinButton(hiddenBg);
            var hiddenButton = hiddenRt.gameObject.AddComponent<Button>();
            hiddenButton.targetGraphic=hiddenBg; UiFactory.ApplyHoverColors(hiddenButton);
            view.hiddenSummary=PaddedLabel(hiddenRt,MobilePlatform.IsTouchUI ? "히든 0 · 효과 보기" : "히든 0 · 효과 보기 [H]",24,TextAnchor.MiddleLeft,new Color(1f,.85f,.4f));
            hiddenButton.onClick.AddListener(()=>view.game?.OpenHiddenDetails());
            // 57 P1 목표 조합 버튼(직업 특성 아래). 히든 효과 발동 상자는 그 아래로 내린다.
            var targetRt=UiFactory.Place("ComboTargetButton",root,new Vector2(0f,1f),new Vector2(30f,-350f),new Vector2(580f,56f));
            var targetBg=UiFactory.AddImage(targetRt,new Color(.12f,.09f,.12f,.92f));
            UiFactory.SkinButton(targetBg);
            var targetButton=targetRt.gameObject.AddComponent<Button>();
            targetButton.targetGraphic=targetBg; UiFactory.ApplyHoverColors(targetButton);
            view.comboTarget=PaddedLabel(targetRt,"",22,TextAnchor.MiddleLeft,new Color(.75f,.92f,1f));
            view.comboTarget.resizeTextForBestFit=true; view.comboTarget.resizeTextMinSize=16; view.comboTarget.resizeTextMaxSize=22;
            targetButton.onClick.AddListener(()=>view.game?.OpenComboTarget());
            var activityRt=UiFactory.Place("HiddenActivity",root,new Vector2(0f,1f),new Vector2(30f,-414f),new Vector2(580f,128f));
            var activityBg=UiFactory.AddImage(activityRt,new Color(.12f,.09f,.12f,.92f));
            UiFactory.SkinPanel(activityBg); activityBg.raycastTarget=false;
            view.hiddenActivity=PaddedLabel(activityRt,"",24,TextAnchor.UpperLeft,Color.white);
            var jobRt=UiFactory.Place("JobTrait",root,new Vector2(0,1),new Vector2(30f,-234f),new Vector2(580f,110f));
            var jobBg=UiFactory.AddImage(jobRt,new Color(.12f,.09f,.12f,.92f));
            UiFactory.SkinPanel(jobBg); jobBg.raycastTarget=false;
            view.jobTrait=PaddedLabel(jobRt,"",24,TextAnchor.UpperLeft,new Color(.85f,1f,.8f));
            // 2026-11-12 HUD 가시성(사용자 요청): 히든 = 개수 배지, 직업 = 초상 + 진행 칸 + 강화 남은 시간, 목표 조합 = 카드 그림/빈 칸. 기존 크기·위치·H/T 동작 유지.
            view.BuildLeftVisuals(hiddenRt, jobRt, targetRt, game);

            var detailRt=UiFactory.Place("HiddenDetails",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(980f,780f));
            UiFactory.SkinPanel(UiFactory.AddImage(detailRt,new Color(.12f,.09f,.12f,1f)));
            view.hiddenDetails=detailRt.gameObject;
            UiFactory.AddText(UiFactory.Place("Title",detailRt,new Vector2(.5f,1f),new Vector2(0,-24f),new Vector2(900f,50f)),"이번 런 효과 · 음료 · 히든 스킬",34,TextAnchor.MiddleCenter,Color.white);
            var scrollRt=UiFactory.Place("Scroll",detailRt,new Vector2(.5f,1f),new Vector2(0,-88f),new Vector2(920f,590f));
            UiFactory.AddImage(scrollRt,Color.clear).raycastTarget=true;
            scrollRt.gameObject.AddComponent<RectMask2D>();
            var scroll=scrollRt.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=40f;
            var barRt=UiFactory.Place("Scrollbar",detailRt,new Vector2(1f,1f),new Vector2(-16f,-88f),new Vector2(12f,590f));
            var barBg=UiFactory.AddImage(barRt,new Color(.05f,.04f,.06f,1f));
            var handleRt=UiFactory.Stretch("Handle",barRt);
            var handleBg=UiFactory.AddImage(handleRt,new Color(.8f,.64f,.36f,1f));
            var bar=barRt.gameObject.AddComponent<Scrollbar>(); bar.targetGraphic=handleBg;
            bar.handleRect=handleRt; bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scrollRt,false); content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1);
            view.hiddenContent=content; scroll.viewport=scrollRt; scroll.content=content;
            var closeRt=UiFactory.Place("Close",detailRt,new Vector2(.5f,0),new Vector2(0,30f),new Vector2(320f,58f));
            var closeBg=UiFactory.AddImage(closeRt,new Color(.35f,.22f,.18f)); UiFactory.SkinButton(closeBg);
            var close=closeRt.gameObject.AddComponent<Button>(); close.targetGraphic=closeBg; UiFactory.ApplyHoverColors(close);
            close.onClick.AddListener(()=>view.game?.CloseHiddenDetails());
            UiFactory.AddText(UiFactory.Stretch("Label",closeRt),MobilePlatform.IsTouchUI ? "게임 재개" : "게임 재개 · Esc",26,TextAnchor.MiddleCenter,Color.white);
            view.hiddenDetails.SetActive(false);

            // 2026-11-09 지속 효과 칸 + 런 구간 배지(상세 창보다 먼저 만들어 상세 창이 위에 그려지게 형제 순서를 앞으로).
            if (game != null)
            {
                view.EffectsStrip = ActiveEffectsStrip.Create(root, game);
                view.EffectsStrip.transform.SetSiblingIndex(view.hiddenDetails.transform.GetSiblingIndex());
                view.PhaseBadge = RunPhaseBadge.Create(root, game);
                view.PhaseBadge.transform.SetSiblingIndex(view.hiddenDetails.transform.GetSiblingIndex());
                view.Legend = FamilyLegend.Create(root, game); // 2026-11-10 몬스터 계열 범례(글자 표식 + 상성 대상)
                view.Legend.transform.SetSiblingIndex(view.hiddenDetails.transform.GetSiblingIndex());
            }

            // 2026-10-12 Android 터치: 안전 영역(노치·제스처) 안에 HUD를 두고, 조이스틱·연구·히든 버튼을 붙인다.
            // 조작 방식(키보드/마우스) 전환은 의미가 없어 숨기고, 일시정지 버튼은 손가락 크기로 키운다. 데스크톱은 그대로.
            if (MobilePlatform.IsTouchUI)
            {
                root.gameObject.AddComponent<SafeAreaFitter>();
                schemeRt.gameObject.SetActive(false);
                pauseRt.anchoredPosition = new Vector2(-370f, -16f);
                pauseRt.sizeDelta = new Vector2(100f, 100f);
                if (game != null) view.TouchControls = TouchControlsView.Create(root, game);
            }

            return view;
        }

        public void ShowHiddenDetails()
        {
            foreach(Transform child in hiddenContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int row=0;
            float y=0f;
            // 2026-11-09 "지금 적용 중"(HUD 칸과 같은 실제 상태) — 칸 상한으로 묶인 효과도 여기서 모두 본다.
            var active=ActiveEffects.Collect(game);
            if(active.Count>0)
            {
                UiFactory.AddText(UiFactory.Place("ActiveHeader",hiddenContent,new Vector2(.5f,1f),new Vector2(0,-y),new Vector2(890f,40f)),"지금 적용 중",28,TextAnchor.MiddleLeft,new Color(.75f,.92f,1f));
                y+=44f;
                foreach(var e in active)
                {
                    var art=UiFactory.Place("Active_"+e.id,hiddenContent,new Vector2(.5f,1f),new Vector2(0,-y),new Vector2(890f,64f));
                    UiFactory.AddImage(art,new Color(.14f,.12f,.17f,1f)).raycastTarget=false;
                    string tag=e.kind==ActiveEffectKind.Run?"[상시]":e.kind==ActiveEffectKind.Timed?$"[지속 {IntNum.RemainingSeconds(e.remaining)}초]":e.kind==ActiveEffectKind.Stack?"[누적]":e.kind==ActiveEffectKind.Ready?"[준비]":"[무효]";
                    var line=UiFactory.AddText(UiFactory.Place("Line",art,new Vector2(.5f,.5f),Vector2.zero,new Vector2(860f,58f)),tag+" "+e.detail,22,TextAnchor.MiddleLeft,Color.white);
                    line.resizeTextForBestFit=true; line.resizeTextMinSize=16; line.resizeTextMaxSize=22; line.verticalOverflow=VerticalWrapMode.Truncate;
                    y+=70f;
                }
                y+=10f;
            }
            foreach(var def in HiddenSkillDatabase.All)
            {
                if(!game.Player.Hidden.Has(def.skill)) continue;
                var rt=UiFactory.Place(def.id,hiddenContent,new Vector2(.5f,1f),new Vector2(0,-y-row*116f),new Vector2(890f,108f));
                UiFactory.AddImage(rt,new Color(.19f,.14f,.17f,1f)).raycastTarget=false;
                UiFactory.AddText(UiFactory.Place("Name",rt,new Vector2(.5f,1),new Vector2(0,-4),new Vector2(860,34)),JobStudyTheme.HiddenName(def,game.Job)+" · "+HiddenSkillDatabase.TierLabel(def.tier),26,TextAnchor.MiddleLeft,new Color(1f,.85f,.4f));
                UiFactory.AddText(UiFactory.Place("Effect",rt,new Vector2(.5f,1),new Vector2(0,-40),new Vector2(860,66)),game.Player.HiddenRuntime.StateLabel(def.skill),24,TextAnchor.UpperLeft,Color.white);
                row++;
            }
            if(row==0) UiFactory.AddText(UiFactory.Place("Empty",hiddenContent,new Vector2(.5f,1),new Vector2(0,-y),new Vector2(880,80)),JobStudyTheme.Text("이번 런에는 아직 해금한 히든 스킬이 없습니다.\n카드·커피 조합 조건을 달성하면 자동으로 적용됩니다.",game.Job),26,TextAnchor.MiddleCenter,Color.white);
            hiddenContent.sizeDelta=new Vector2(0,Mathf.Max(100,y+Mathf.Max(1,row)*116)); hiddenContent.anchoredPosition=Vector2.zero;
            hiddenDetails.SetActive(true); hiddenDetails.transform.SetAsLastSibling();
        }
        public void HideHiddenDetails() => hiddenDetails.SetActive(false);

        /// <summary>화면 상단에 큰 글씨 배너를 잠시 띄운다 (보스 등장·대사용).</summary>
        public void ShowBanner(string title, string subtitle, float seconds)
        {
            bannerTitle.text = title;
            bannerSub.text = subtitle;
            bannerRemaining = seconds;
        }

        /// <summary>57 P1 첫 수집 안내 한 줄(seconds 동안).</summary>
        public void ShowGuide(string text, float seconds)
        {
            guide.text = text;
            guideRemaining = seconds;
            guideRoot.SetActive(true);
        }
        public bool GuideVisible => guideRoot != null && guideRoot.activeSelf;
        public string GuideText => guide != null ? guide.text : "";
        public string ComboTargetText => comboTarget != null ? comboTarget.text : "";

        /// <summary>57 P1: 처음 주운 재료의 게이지를 잠깐 맥동시켜 "어디가 찼는지" 보이게 한다.</summary>
        public void PulseGauge(IngredientType type)
        {
            pulseType = type;
            pulseRemaining = 1.6f;
        }

        FillBar BarFor(IngredientType type) => type == IngredientType.Tea ? teaBar : type == IngredientType.Coffee ? beanBar : gaugeBar;

        /// <summary>테스트용 — 지금 떠 있는 배너 부제(안내 문구가 실제와 맞는지).</summary>
        public string BannerSubText => bannerSub != null ? bannerSub.text : "";

        /// <summary>57 P1 HUD 목표 조합 문구 — 실제 조건 진행도(HiddenRequirements).</summary>
        public static string ComboTargetLabel(GameManager g)
        {
            string key = MobilePlatform.IsTouchUI ? "" : " [T]";
            if (!g.ComboTarget.HasValue) return "목표 조합 정하기" + key + " · 필요한 카드 표시";
            var def = HiddenSkillDatabase.Get(g.ComboTarget.Value);
            var progress = HiddenRequirements.Progress(def.skill, g.Job, g.HiddenState);
            string name = HiddenRequirements.DisplayName(def, g.Job, g.HiddenDiscovered(def.id));
            return $"목표: {name} · 조건 {HiddenRequirements.MetCount(progress)}/{progress.Count}{key}";
        }

        public static string CoffeePointsLabel(int points) => $"커피 포인트 {points}  [K] 커피 연구";

        static void SetIcon(Image icon, bool visible)
        {
            if (icon != null && icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
        }

        /// <summary>57 P0-1 화면 밖 보스 방향 표시.</summary>
        public BossDirectionIndicator BossIndicator { get; private set; }

        /// <summary>테스트용 — 상단 우측 일시정지 버튼(08:76).</summary>
        public Button PauseButton => pauseButton;
        /// <summary>터치 UI일 때만 있는 가상 조이스틱·버튼(없으면 null).</summary>
        public TouchControlsView TouchControls { get; private set; }

        /// <summary>테스트용 — 저장 실패 알림/재시도 버튼이 보이는지.</summary>
        public bool SaveFailedVisible => saveFailedRoot != null && saveFailedRoot.activeSelf;

        /// <summary>38: 결과 화면 동안 전투 HUD를 숨긴다(각 요소의 자체 활성 규칙은 그대로, CanvasGroup 투명·입력 차단).
        /// 저장 실패 알림/다시 시도는 결과 화면에서도 보이도록 제외한다(08:175).</summary>
        public bool CombatHidden { get; private set; }
        public void SetCombatHidden(bool hidden)
        {
            foreach (Transform child in transform)
            {
                if (saveFailedRoot != null && child == saveFailedRoot.transform) continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                group.alpha = hidden ? 0f : 1f;
                group.interactable = group.blocksRaycasts = !hidden;
            }
            CombatHidden = hidden;
        }

        /// <summary>테스트용 — HUD 상단 우측 커피 포인트 문구.</summary>
        public string CoffeePointsText => coffeePoints.text;
        public bool SuppressedStatusVisible => suppressedBack != null && suppressedBack.activeSelf;
        public string SuppressedText => suppressed.text;

        void RefreshSchemeLabel()
        {
            schemeLabel.text = $"조작: {InputSettings.Label(InputSettings.Scheme)}\n(클릭해서 전환)";
        }

        void Update()
        {
            if(hiddenSummary!=null && game!=null && game.Player!=null && Time.unscaledTime>=hiddenRefresh)
            {
                hiddenRefresh=Time.unscaledTime+.1f;
                hiddenSummary.text=MobilePlatform.IsTouchUI ? $"히든 스킬 {game.Player.Hidden.Count}개 · 효과 보기" : $"히든 스킬 {game.Player.Hidden.Count}개 · 효과 보기 [H]"; // 2026-11-12 왼쪽 배지와 같은 수(색만으로 구분하지 않게 글자 유지)
                jobTrait.text=JobPreset.For(game.Job).name+" · "+game.JobTraitStatus;
                if(comboTarget!=null) comboTarget.text=ComboTargetLabel(game);
                RefreshLeftVisuals();
                var runtime=game.Player.HiddenRuntime;
                hiddenActivity.text=runtime.LastActivationRemaining>0f ? "[발동] "+runtime.LastActivation
                    : "";
                hiddenActivity.transform.parent.gameObject.SetActive(runtime.LastActivationRemaining>0f);
            }
            if (guideRemaining > 0f)
            {
                guideRemaining -= Time.unscaledDeltaTime;
                if (guideRemaining <= 0f) guideRoot.SetActive(false);
            }
            foreach (var bar in new[] { gaugeBar, teaBar, beanBar }) bar.Root.localScale = Vector3.one;
            if (pulseRemaining > 0f)
            {
                pulseRemaining -= Time.unscaledDeltaTime;
                float k = Mathf.Abs(Mathf.Sin(pulseRemaining * 9f));
                BarFor(pulseType).Root.localScale = new Vector3(1f + 0.03f * k, 1f + 0.25f * k, 1f);
            }
            bool saveFailed = PermanentProgress.LastSaveFailed;
            if (saveFailedRoot.activeSelf != saveFailed) saveFailedRoot.SetActive(saveFailed);

            var player = game.Player;
            hpBar.Set(player.Hp / player.MaxHp);
            hpBar.Label.text = $"HP {Mathf.CeilToInt(player.Hp)} / {Mathf.CeilToInt(player.MaxHp)}";

            var gauge = game.CocktailGauge;
            gaugeBar.Set(gauge.Fill01);
            gaugeBar.Label.text = $"Lv {gauge.Level}   칵테일 재료 {gauge.Current} / {gauge.Threshold}";

            var tea = game.TeaGauge;
            teaBar.Set(tea.Fill01);
            teaBar.Label.text = $"Lv {tea.Level}   찻잎 {tea.Current} / {tea.Threshold}";

            var beans = game.BeanGauge;
            beanBar.Set(beans.Fill01);
            beanBar.Label.text = JobStudyTheme.Text($"Lv {beans.Level}   원두 {beans.Current} / {beans.Threshold}  (스킬 {game.Skills.UnlockedCount}/{game.Skills.Nodes.Count})",game.Job);
            // 터치: 키 안내([K]) 없이 짧게(연구 트리는 오른쪽 아래 버튼).
            coffeePoints.text = MobilePlatform.IsTouchUI ? JobStudyTheme.Text($"커피 포인트 {game.CoffeePoints}", game.Job)
                : JobStudyTheme.Text(CoffeePointsLabel(game.CoffeePoints),game.Job);

            int seconds = Mathf.FloorToInt(game.RunTime);
            info.text = $"{seconds / 60:00}:{seconds % 60:00}\n처치 {game.Kills}";

            var stats = game.Stats;
            SetIcon(slowIcon, stats.IsSlowed);
            SetIcon(suppressedIcon, stats.BonusesSuppressed);
            SetIcon(sealIcon, stats.FlameLocked || stats.PoisonLocked || stats.HybridLocked);
            suppressed.text = stats.BonusesSuppressed
                ? $"환불 · 강화 무효 {IntNum.RemainingSeconds(stats.SuppressedRemaining)}초"
                : "";
            if (suppressedBack.activeSelf != stats.BonusesSuppressed) suppressedBack.SetActive(stats.BonusesSuppressed);

            var boss = game.ActiveBoss;
            bool showBoss = boss != null && !boss.IsDead;
            if (bossBar.Root.gameObject.activeSelf != showBoss) bossBar.Root.gameObject.SetActive(showBoss);
            SetIcon(bossInvulnerableIcon, showBoss && boss.Invulnerable);
            if (showBoss)
            {
                bossBar.Set(boss.Hp / boss.MaxHp);
                bossBar.Label.text = $"{MonsterFamilies.Mark(boss.Family)} {boss.Def.name}  {Mathf.CeilToInt(boss.Hp)} / {Mathf.CeilToInt(boss.MaxHp)}"; // 2026-11-10 계열 글자 표식
            }

            if (bannerRemaining > 0f)
            {
                bannerRemaining -= Time.unscaledDeltaTime;
                if (bannerRemaining <= 0f)
                {
                    bannerTitle.text = "";
                    bannerSub.text = "";
                }
            }
        }
    }
}

