using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>레벨업 시 카드 3장 제시. 클릭 또는 1/2/3 키로 선택. 등급 확률을 카드에 표기한다.</summary>
    public class CardSelectView : MonoBehaviour
    {
        const int SlotCount = 3;

        class Slot
        {
            public GameObject root;
            public Image background;
            public Text rarity;
            public Text title;
            public Text description;
            public Text stacks;
            public Text fact, connection;
            public Button learn;
            public Text odds;
            public Image categoryIcon;
            public Text advice;
            public GameObject badge;
            public GameObject influenceRoot;
            public Text influence;
            // 2026-11-15 피드백 11 등급 테두리·등장 / 12 목표 표식(서로 다른 모양·자리 — 등급 = 카드 둘레 테두리, 목표 = 왼쪽 위 표식 + 아래 띠)
            public Image rarityFrame, sweep;
            public RectTransform cardRt;
            public GameObject goalMark;
            public Text goalMarkText;
            public float enterT;
            public bool sweepOn;
            // 2026-11-22 플레이 피드백 A(5·9·12·2·17): 효과 줄(아이콘+색+수치), 직업 연계 칩, 테두리 빛, hover 확대, 잔 채우기 등장
            public RectTransform effects;
            public GameObject jobChip;
            public Text jobChipText;
            public Image glow, fill, fillSurface;
            public CanvasGroup group;
            public CardRarity rarityValue;
            public bool hovered;
            public float hoverScale = 1f, delay;
            public int index;
            public float sweepPhase;
            // 2026-11-22 레벨업 메뉴판: 등급별 내부 장식(불투명 바탕 포함), 건네기 중 노출·정착 상태
            public Image deco, focusTint;
            public CanvasGroup frameGroup, glowGroup;
            public float baseX, settle = 1f;
            // 2026-11-23 항목 8 후속: 카드마다 자기 등급·직업 외형의 건네기(슬롯 자리에서 재생, 끝나면 그 카드가 정착)
            public Image handoffImage;
            public readonly MenuHandoffPlayer handoff = new MenuHandoffPlayer();
        }

        Text reasonLine, targetLine, keyHint;
        public string KeyHintLabel => keyHint != null ? keyHint.text : "";
        CardEducationView learning;
        CardEducationGuide tutorial;
        public CardEducationGuide Tutorial => tutorial;
        public bool IsTutorialOpen => tutorial != null && tutorial.IsOpen;
        public bool IsLearningOpen => (learning != null && learning.IsOpen) || IsTutorialOpen;
        public string LearningBodyText => learning != null ? learning.BodyText : "";
        public string FactText(int index) => slots[index].fact.text;
        public string ConnectionText(int index) => slots[index].connection.text;

        readonly Slot[] slots = new Slot[SlotCount];
        readonly List<CardOffer> offers = new List<CardOffer>();
        readonly List<CardAdvice> advice = new List<CardAdvice>();
        GameObject root;
        Action<CardOffer> onChosen;
        /// <summary>08:39 — 보상이 같은 호출에서 연달아 열릴 때(07:46) 직전 화면을 닫은 키/클릭/Submit이
        /// 이 화면을 바로 누르지 않도록, 연 프레임과 눌림 해제 전까지 사용자 입력 경로를 잠근다.</summary>
        readonly InputReleaseGate inputGate = new InputReleaseGate();
        /// <summary>2026-11-23 메뉴판 건네기: 카드마다 자기 등급 동작을 그 슬롯에서 동시에 재생한다(슬롯별 MenuHandoffPlayer).
        /// 각 카드는 자기 전달이 끝나면 정착하고, 모든 카드가 정착한 프레임에 입력 게이트를 새로 무장한다.</summary>
        bool handoffRunning;
        /// <summary>창을 열기 직전의 포커스(예: 마스터 도전 준비 패널 버튼). 닫힐 때 다른 화면이 포커스를 가져가지 않았으면 되돌린다.</summary>
        GameObject focusBeforeOpen;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>49 추천 결과(테스트·QA용, 표시 중인 제시 순서와 같다).</summary>
        public IReadOnlyList<CardAdvice> Advice => advice;
        public string AdviceText(int index) => index >= 0 && index < SlotCount ? slots[index].advice.text : "";
        public bool BadgeVisible(int index) => index >= 0 && index < SlotCount && slots[index].badge.activeSelf;

        public static CardSelectView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("CardSelect", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.96f));
            CenterBand.Attach(rootRt); // 2026-10-12 세로가 긴 화면: 카드 내용 세로 가운데

            var view = rootRt.gameObject.AddComponent<CardSelectView>();
            view.root = rootRt.gameObject;

            // 57 P1: 제목을 올리고 그 아래에 "왜 열렸나·다음 카드 창 조건"과 목표 조합 진행 줄을 둔다(카드 위 추천 표식 위쪽).
            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(1200f, 80f));
            UiFactory.AddText(title, "레벨업!  카드를 선택하세요", 56, TextAnchor.MiddleCenter, Color.white);
            view.reasonLine = UiFactory.AddText(UiFactory.Place("Reason", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -114f),
                new Vector2(1600f, 40f)), "", 26, TextAnchor.MiddleCenter, new Color(0.9f, 0.92f, 1f));
            view.targetLine = UiFactory.AddText(UiFactory.Place("TargetLine", rootRt, new Vector2(0.5f, 1f), new Vector2(-150f, -162f),
                new Vector2(1220f, 44f)), "", 24, TextAnchor.MiddleCenter, new Color(0.75f, 0.92f, 1f));
            view.targetLine.resizeTextForBestFit = true;
            view.targetLine.resizeTextMinSize = 16;
            view.targetLine.resizeTextMaxSize = 24;
            view.targetLine.verticalOverflow = VerticalWrapMode.Truncate;
            var changeRt = UiFactory.Place("ChangeTarget", rootRt, new Vector2(0.5f, 1f), new Vector2(620f, -160f), new Vector2(280f, 48f));
            var changeBg = UiFactory.AddImage(changeRt, new Color(0.18f, 0.32f, 0.38f));
            UiFactory.SkinButton(changeBg);
            var change = changeRt.gameObject.AddComponent<Button>();
            change.targetGraphic = changeBg;
            UiFactory.ApplyHoverColors(change);
            UiFactory.AddFocusSfx(change);
            change.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.OpenComboTarget(); });
            UiFactory.AddText(UiFactory.Stretch("Label", changeRt), MobilePlatform.IsTouchUI ? "목표 바꾸기" : "목표 바꾸기 [T]", 24,
                TextAnchor.MiddleCenter, Color.white);
            view.ChangeTargetButton = change;

            for (int i = 0; i < SlotCount; i++)
                view.slots[i] = view.BuildSlot(rootRt, i);
            // 카드별 건네기 이미지 — 모든 카드 위(이웃 카드보다 앞)에 그린다. 384 폭 < 시작 간격 422(=480×0.88)라 서로 겹치지 않는다.
            for (int i = 0; i < SlotCount; i++)
                view.slots[i].handoffImage = MenuHandoffPlayer.CreateImage(rootRt, $"MenuHandoff{i + 1}", LevelUpMenuArt.CardGroupAnchorY);
            // 2026-11-15 피드백 8: 키보드 선택 안내는 카드 밖 한 줄(카드 아래 목표 진행 띠 -443 아래, 화면 안전선 안).
            view.keyHint = UiFactory.AddText(UiFactory.Place("KeyHint", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -478f),
                new Vector2(900f, 36f)), MobilePlatform.IsTouchUI ? "" : KeyHintText, 22, TextAnchor.MiddleCenter, new Color(0.78f, 0.8f, 0.86f));
            view.keyHint.raycastTarget = false;

            view.learning = CardEducationView.Create(rootRt, () => view.inputGate.Arm(Time.frameCount));
            view.tutorial = CardEducationGuide.Create(rootRt, () => view.inputGate.Arm(Time.frameCount), () => view.OpenLearning(0));
            var helpRt=UiFactory.Place("LearningHelp",rootRt,new Vector2(.5f,1),new Vector2(790,-45),new Vector2(260,54));
            var helpBg=UiFactory.AddImage(helpRt,new Color(.17f,.3f,.32f));
            var help=helpRt.gameObject.AddComponent<Button>(); help.targetGraphic=helpBg;
            help.onClick.AddListener(view.OpenTutorial);
            view.LearningHelpButton = help;
            UiFactory.AddText(UiFactory.Stretch("Label",helpRt),"학습 안내",26,TextAnchor.MiddleCenter,Color.white);
            view.root.SetActive(false);
            return view;
        }

        Slot BuildSlot(Transform parent, int index)
        {
            var slot = new Slot();
            float x = (index - 1) * 480f;
            var rt = UiFactory.Place($"Card{index + 1}", parent, new Vector2(0.5f, 0.5f), new Vector2(x, -30f),
                new Vector2(430f, 620f));
            slot.root = rt.gameObject;
            slot.background = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));

            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = slot.background;
            UiFactory.SkinCard(slot.background); // 04 §8 ui_card_frame(16px 9-slice) — 희귀도 색은 기존 텍스트/틴트 유지
            UiFactory.ApplyHoverColors(button);
            int captured = index;
            button.onClick.AddListener(() => ChooseFromInput(captured));

            // 2026-11-15 피드백 8: 카드 왼쪽 위 순번 "[1]/[2]/[3]"은 카드 내용(중첩·비용)처럼 읽혀 제거 — 1/2/3 키 선택은 그대로, 안내는 화면 아래 KeyHint 한 줄.
            slot.rarity = UiFactory.AddText(UiFactory.Place("Rarity", rt, new Vector2(0.5f, 1f),
                new Vector2(-15f, -30f), new Vector2(280f, 44f)), "", 32, TextAnchor.MiddleCenter, Color.white);
            slot.title = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -85f), new Vector2(390f, 100f)), "", 36, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SkinHeader(slot.title);
            // 2026-11-12 그림 우선: 이름판(390×100) 왼쪽에 카드 그림(ArtX, ArtSize), 이름 글자는 오른쪽 칸으로(판은 그대로).
            slot.title.rectTransform.anchoredPosition = new Vector2(50f, -85f);
            slot.title.rectTransform.sizeDelta = new Vector2(290f, 100f);
            slot.title.horizontalOverflow = HorizontalWrapMode.Wrap;
            slot.title.resizeTextForBestFit = true; slot.title.resizeTextMinSize = 24; slot.title.resizeTextMaxSize = 36;
            // 2026-11-15 피드백 9 글자 계층: 이름 → 게임 효과(크게·밝게, 최대 2줄, 자동 축소 없음) → 음료 지식·해석(작게·흐리게).
            slot.description = UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, EffectBoxY), new Vector2(390f, EffectBoxHeight)), "", EffectFontSize,
                MobilePlatform.IsTouchUI ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter, // 2026-10-12 모바일: 설명 세로 가운데
                EffectColor);
            slot.description.verticalOverflow = VerticalWrapMode.Truncate;
            slot.fact = UiFactory.AddText(UiFactory.Place("RealWorldFact", rt, new Vector2(.5f,.5f), new Vector2(0,-28), new Vector2(380,100)), "", SupportFontSize, TextAnchor.UpperLeft, new Color(.68f,.8f,.72f));
            slot.connection = UiFactory.AddText(UiFactory.Place("GameConnection", rt, new Vector2(.5f,.5f), new Vector2(0,-112), new Vector2(380,60)), "", SupportFontSize, TextAnchor.UpperLeft, new Color(.82f,.74f,.62f));
            slot.fact.verticalOverflow = slot.connection.verticalOverflow = VerticalWrapMode.Truncate;
            var learnRt = UiFactory.Place("Learn", rt, new Vector2(.5f,0), new Vector2(0,118), new Vector2(300,48));
            var learnBg = UiFactory.AddImage(learnRt, new Color(.15f,.3f,.3f));
            slot.learn = learnRt.gameObject.AddComponent<Button>(); slot.learn.targetGraphic = learnBg;
            slot.learn.onClick.AddListener(() => OpenLearning(captured));
            UiFactory.AddText(UiFactory.Stretch("Label",learnRt),"지식 상세 보기",24,TextAnchor.MiddleCenter,Color.white);
            // r13 검수 U2 / 09:54 — 카드별 일러스트 대신 기존 재료 아이콘을 카테고리 도안으로 쓴다(하이브리드만 전용 아이콘).
            // 설명(최대 2줄)과 중첩 문구 사이의 빈 영역에 둔다. 스프라이트는 Show에서 카테고리별로 고른다.
            var iconRt = UiFactory.Place("CategoryIcon", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -115f),
                new Vector2(80f, 80f)); // 카드 중심 기준 -75..-155, 중첩 문구(-160..-200) 위
            slot.categoryIcon = iconRt.gameObject.AddComponent<Image>();
            slot.categoryIcon.preserveAspect = true;
            slot.categoryIcon.raycastTarget = false;
            slot.stacks = UiFactory.AddText(UiFactory.Place("Stacks", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 82f), new Vector2(380f, 32f)), "", 28, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.85f, 1f));
            // 49 추천: 근거 문구(기존 확률 문구 자리 — 확률 문구는 숨김 유지)와 카드 위 테두리의 "추천" 표식. 선택·포커스는 바꾸지 않는다.
            slot.advice = UiFactory.AddText(UiFactory.Place("Advice", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(380f, 60f)), "", 20, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.55f));
            // Keep both recommendation reasons above the frame, including wrapped numeric suffixes.
            slot.advice.resizeTextForBestFit = true;
            slot.advice.resizeTextMinSize = 16;
            slot.advice.resizeTextMaxSize = 20;
            slot.advice.verticalOverflow = VerticalWrapMode.Truncate;
            slot.advice.raycastTarget = false;
            var badgeRt = UiFactory.Place("RecommendBadge", rt, new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(180f, 48f));
            UiFactory.AddImage(badgeRt, new Color(0.95f, 0.72f, 0.25f, 1f)).raycastTarget = false;
            UiFactory.AddText(UiFactory.Stretch("Label", badgeRt), "추천", 30, TextAnchor.MiddleCenter, new Color(0.18f, 0.1f, 0.04f))
                .raycastTarget = false;
            slot.badge = badgeRt.gameObject;
            slot.badge.SetActive(false);
            // 57 P1 목표 조합 진행: 이 카드가 목표 조건을 몇에서 몇으로 올리는지(카드 아래 띠, 해당 없으면 숨김).
            var infRt = UiFactory.Place("TargetInfluence", rt, new Vector2(0.5f, 0f), new Vector2(0f, -72f), new Vector2(430f, 62f));
            UiFactory.AddImage(infRt, new Color(0.08f, 0.24f, 0.3f, 0.95f)).raycastTarget = false;
            slot.influence = UiFactory.AddText(UiFactory.Stretch("Label", infRt), "", 22, TextAnchor.MiddleCenter, new Color(0.8f, 0.96f, 1f));
            slot.influence.resizeTextForBestFit = true;
            slot.influence.resizeTextMinSize = 15;
            slot.influence.resizeTextMaxSize = 22;
            slot.influenceRoot = infRt.gameObject;
            slot.influenceRoot.SetActive(false);
            slot.cardRt = rt;
            slot.index = index;
            slot.group = rt.gameObject.AddComponent<CanvasGroup>();
            // 효과 줄 묶음(Desc 상자와 같은 자리). 능력치 효과가 없는 특수 카드만 Desc 글을 그대로 보인다.
            slot.effects = UiFactory.Place("Effects", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, EffectBoxY + EffectBoxHeight * 0.5f), new Vector2(400f, EffectBoxHeight));
            slot.effects.pivot = new Vector2(0.5f, 1f);
            // 직업 연계: 등급 글에 붙이던 " · 직업 연계"를 오른쪽 위 칩으로(왼쪽 위 = 목표, 가운데 = 추천 — 자리·색이 서로 다름)
            var jc = UiFactory.Place("JobChip", rt, new Vector2(1f, 1f), new Vector2(24f, 34f), new Vector2(136f, 40f)); // 가운데 "추천"(±90)과 겹치지 않게 오른쪽 끝으로
            var jcBg = UiFactory.AddImage(jc, StatStyle.JobColorBartender);
            jcBg.raycastTarget = false;
            slot.jobChipText = UiFactory.AddText(UiFactory.Stretch("Label", jc), "직업 연계", 22, TextAnchor.MiddleCenter, new Color(0.16f, 0.08f, 0.03f));
            slot.jobChipText.raycastTarget = false;
            slot.jobChip = jc.gameObject;
            slot.jobChip.SetActive(false);
            // 잔 채우기 등장: 카드 아래에서 위로 차오르는 액체 막(등급 색) + 수면 선. 다 차면 사라진다. 글·버튼 위(마지막 형제), 클릭 막지 않음.
            var fillRt = UiFactory.Place("PourFill", rt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(rt.sizeDelta.x - 8f, 0f));
            fillRt.pivot = new Vector2(0.5f, 0f);
            slot.fill = UiFactory.AddImage(fillRt, Color.clear); slot.fill.raycastTarget = false;
            var surf = UiFactory.Place("Surface", fillRt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(rt.sizeDelta.x - 8f, 6f));
            surf.pivot = new Vector2(0.5f, 0.5f);
            slot.fillSurface = UiFactory.AddImage(surf, Color.clear); slot.fillSurface.raycastTarget = false;
            // hover·키보드 선택 확대
            var trig = rt.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; enter.callback.AddListener(_ => slot.hovered = true); trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit }; exit.callback.AddListener(_ => slot.hovered = false); trig.triggers.Add(exit);
            // 피드백 11: 등급 테두리(카드 둘레 6px 띠, 등급 색). 일반은 띠 없음. 영웅·특수는 두께 10 + 등장 때 한 번 빛줄기.
            // 테두리는 카드 뒤 형제(카드 바탕이 가운데를 덮어 둘레 띠만 보인다 — 효과 글 위에 그리지 않는다).
            var frameRt = UiFactory.Place($"RarityFrame{index + 1}", parent, new Vector2(0.5f, 0.5f), rt.anchoredPosition, rt.sizeDelta + new Vector2(12f, 12f));
            frameRt.SetSiblingIndex(rt.GetSiblingIndex());
            slot.rarityFrame = UiFactory.AddImage(frameRt, Color.clear);
            slot.rarityFrame.raycastTarget = false;
            slot.frameGroup = frameRt.gameObject.AddComponent<CanvasGroup>();
            slot.frameGroup.interactable = slot.frameGroup.blocksRaycasts = false;
            // 테두리 빛(희귀 이상): 테두리 바깥 은은한 빛 띠, 천천히 숨쉬듯 밝기 변화(저이펙트 = 고정)
            var glowRt = UiFactory.Place($"RarityGlow{index + 1}", parent, new Vector2(0.5f, 0.5f), rt.anchoredPosition, rt.sizeDelta + new Vector2(40f, 40f));
            glowRt.SetSiblingIndex(frameRt.GetSiblingIndex());
            slot.glow = UiFactory.AddImage(glowRt, Color.clear);
            slot.glow.raycastTarget = false;
            slot.glowGroup = glowRt.gameObject.AddComponent<CanvasGroup>();
            slot.glowGroup.interactable = slot.glowGroup.blocksRaycasts = false;
            var sweepRt = UiFactory.Place("RaritySweep", rt, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(70f, 560f));
            slot.sweep = UiFactory.AddImage(sweepRt, Color.clear);
            slot.sweep.raycastTarget = false;
            sweepRt.localRotation = Quaternion.Euler(0f, 0f, -12f);
            sweepRt.SetAsFirstSibling(); // 빛줄기는 글·그림 아래(카드 바탕 위)
            // 2026-11-22 등급별 내부 장식: 86×124 원본을 430×620(5배, Point)에 맞춘다. 불투명 바탕이라 기존 바탕 위·빛줄기/아이콘/글 아래, 클릭을 막지 않는다.
            var decoRt = UiFactory.Stretch("MenuDeco", rt);
            slot.deco = decoRt.gameObject.AddComponent<Image>();
            slot.deco.color = Color.white; slot.deco.raycastTarget = false;
            decoRt.SetAsFirstSibling();
            // 불투명 장식이 기존 바탕 위 hover/선택 색 변화를 가리므로, 장식 바로 위·글/아이콘 아래에 은은한 등급색 막을 둔다(확대·테두리와 함께 표시).
            var tintRt = UiFactory.Stretch("FocusTint", rt);
            slot.focusTint = tintRt.gameObject.AddComponent<Image>();
            slot.focusTint.color = Color.clear; slot.focusTint.raycastTarget = false;
            tintRt.SetSiblingIndex(1);
            // 피드백 12: 목표 기여 표식 — 왼쪽 위(순번을 뺀 자리). 청록 표식 + "목표" / 완성이면 금색 "목표 완성".
            // 카드 위 테두리에 걸친 왼쪽 표식 줄(가운데 "추천" 표식과 같은 줄, 겹치지 않는 왼쪽) — 카드 안 등급 글과 겹치지 않는다.
            var gm = UiFactory.Place("GoalMark", rt, new Vector2(0f, 1f), new Vector2(-10f, 26f), new Vector2(124f, 44f));
            UiFactory.AddImage(gm, new Color(0.1f, 0.55f, 0.6f, 0.95f)).raycastTarget = false;
            slot.goalMarkText = UiFactory.AddText(UiFactory.Stretch("Label", gm), "◎ 목표", 24, TextAnchor.MiddleCenter, Color.white);
            slot.goalMarkText.raycastTarget = false;
            slot.goalMarkText.resizeTextForBestFit = true; slot.goalMarkText.resizeTextMinSize = 18; slot.goalMarkText.resizeTextMaxSize = 24;
            slot.goalMark = gm.gameObject;
            slot.goalMark.SetActive(false);
            slot.odds = UiFactory.AddText(UiFactory.Place("Odds", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(380f, 44f)), "", 28, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.6f));
            return slot;
        }

        /// <summary>2026-11-15 피드백 9: 게임 효과 글(카드 중심 기준 상자 가운데 y, 높이 = 32 두 줄), 보조 글 크기.
        /// 이름판 아래 끝 125(카드 중심 기준) 바로 밑 120 ~ 28(32 두 줄), 지식 22 ~ -78(21 세 줄), 해석 -82 ~ -142(두 줄), 지식 버튼 위 끝 -144.</summary>
        public const int EffectFontSize = 32, SupportFontSize = 21;
        public const float EffectBoxY = 74f, EffectBoxHeight = 92f;
        static readonly Color EffectColor = new Color(1f, 0.97f, 0.86f);
        /// <summary>2026-11-15 피드백 8: 카드에서 뺀 순번 대신 화면 아래 공통 단축키 안내(터치 UI는 표시 안 함).</summary>
        public const string KeyHintText = "숫자 키 1 · 2 · 3으로 왼쪽부터 카드 선택";

        /// <summary>카드 그림 위치(카드 중심 기준): 이름판 왼쪽 칸. 2026-11-12 이전 = 오른쪽 위 64(720p ≈43px, 장식처럼 보였다).</summary>
        public const float ArtX = -143f, ArtY = 175f, ArtSize = 88f;

        /// <summary>09:54 카테고리 도안 — 칵테일/차는 필드 픽업과 같은 재료 아이콘, 하이브리드는 전용 아이콘. 없으면 null(숨김).</summary>
        public static Sprite CategoryIcon(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Tea: return SpriteFactory.TeaIcon;
                case CardCategory.Hybrid: return ArtLibrary.Sprite("hybrid_tea_martini");
                default: return SpriteFactory.CocktailIngredientIcon;
            }
        }

        /// <param name="adviceContext">49 추천 판정 상태. null이면 실행 중 게임의 실제 상태(없으면 추천 표시 없음).</param>
        public void Show(List<CardOffer> newOffers, Action<CardOffer> callback, CardAdviceContext adviceContext = null)
        {
            if (!IsOpen) // 다시 추첨(열린 채 Show)은 처음 연 시점의 포커스를 유지
            {
                // 연속 보상으로 닫힌 직후 다시 열리면 지금 포커스는 이 창의 (꺼진) 카드다 — 그때는 처음 열기 전 포커스를 유지한다.
                var cur = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (cur == null || !cur.transform.IsChildOf(transform)) focusBeforeOpen = cur;
            }
            CancelHandoff(); // 다시 추첨·재오픈: 이전 재생을 지우고 새 후보가 확정된 시점에 새로 시작
            offers.Clear();
            offers.AddRange(newOffers);
            onChosen = callback;
            if (adviceContext == null && GameManager.Instance != null && GameManager.Instance.Player != null)
                adviceContext = GameManager.Instance.BuildCardAdviceContext();
            advice.Clear();
            advice.AddRange(CardAdvisor.Evaluate(offers, adviceContext));

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = slots[i];
                bool used = i < offers.Count;
                // 1·2장도 같은 규칙으로 가운데 정렬(전체 묶음 폭 기준)
                slot.baseX = (i - (offers.Count - 1) * 0.5f) * 480f;
                PlaceSlot(slot, 1f, 1f);
                slot.root.SetActive(used);
                slot.rarityFrame.gameObject.SetActive(used);
                if (!used) continue;

                var offer = offers[i];
                var card = offer.card;
                var rarityColor = RarityTable.Color(card.rarity);

                slot.rarity.text = RarityTable.Label(card.rarity);
                var job=GameManager.Instance;
                bool jobLink = job!=null && ((job.Job==Job.Bartender && card.category==CardCategory.Cocktail && offer.currentStacks==0)
                    || (job.Job==Job.TeaSommelier && card.category==CardCategory.Tea && offer.currentStacks>0));
                // 2026-11-22 혼합 러시가 켜져 있으면 이미 가진 카드(중첩)는 러시를 끈다 → 빨간 칩으로 미리 경고.
                bool rushOff = job!=null && job.Job==Job.Bartender && job.MixingRushActive && offer.currentStacks>0;
                // 2026-11-22 피드백 A: 직업 연계는 등급 글 대신 오른쪽 위 칩(직업 색)으로 따로 보인다.
                slot.jobChip.SetActive(jobLink || rushOff);
                if (rushOff)
                {
                    slot.jobChip.GetComponent<Image>().color = RushOffColor;
                    slot.jobChipText.text = "러시 꺼짐!";
                    slot.jobChipText.color = Color.white;
                }
                else if (jobLink)
                {
                    slot.jobChip.GetComponent<Image>().color = StatStyle.JobColor(job.Job);
                    slot.jobChipText.text = job.Job == Job.Bartender ? (job.MixingRushActive ? "러시 유지" : "혼합 러시") : "깊은 우림";
                    slot.jobChipText.color = new Color(0.16f, 0.08f, 0.03f);
                }
                slot.rarity.color = rarityColor;
                slot.title.text = card.displayName;
                slot.description.text = GunRangeCard(card) ? GunHoming.Describe(offer.currentStacks + 1) : CardEducationSummary.Effect(card);
                // 효과 한 줄 = 아이콘 + 능력치 색 이름 + 수치. 능력치 표에 없는 특수 효과(하이브리드 보조 활 등)는 기존 효과 글.
                BuildEffectLines(slot, card);
                var finalForm = AttackFinalForms.ForCard(card.id);
                slot.learn.GetComponentInChildren<Text>().text = finalForm.HasValue ? "지식 · 최대 효과 보기" : "지식 상세 보기";
                slot.fact.text = CardEducationSummary.Fact(card);
                slot.connection.text = "해석 · " + CardEducationSummary.Connection(card);
                slot.connection.gameObject.SetActive(false); // 피드백 9·12 글 줄이기: 해석은 '지식 상세 보기'에서(글 자체는 유지)
                slot.categoryIcon.rectTransform.anchoredPosition = new Vector2(ArtX, ArtY);
                slot.categoryIcon.rectTransform.sizeDelta = new Vector2(ArtSize, ArtSize);
                slot.stacks.text = offer.currentStacks == 0
                    ? $"신규 · 최대 {card.maxStacks}"
                    : $"Lv {offer.currentStacks} → {offer.currentStacks + 1}  (최대 {card.maxStacks})";
                slot.odds.text = offer.earlyRangedPriority ? "초반 원거리 우선 추첨 (60%)" : $"{RarityTable.Label(card.rarity)} 등급 확률 {IntNum.Round(offer.rarityPercent)}%";
                slot.odds.gameObject.SetActive(false);
                var a = advice[i];
                slot.advice.text = TintAdvice(a.reason, job != null ? job.Job : (Job?)null);
                slot.advice.color = a.recommended ? new Color(1f, 0.86f, 0.55f) : new Color(0.85f, 0.85f, 0.9f, 0.85f);
                slot.badge.SetActive(a.recommended);
                UiFactory.SetChoice(slot.background, false,
                    Color.Lerp(new Color(0.16f, 0.13f, 0.2f), rarityColor, 0.18f));
                var icon = CardEducationIcons.For(card);
                slot.categoryIcon.sprite = icon;
                slot.categoryIcon.gameObject.SetActive(icon != null);
                ApplyRarityStyle(slot, card.rarity);
                var deco = LevelUpMenuArt.CardDecoration(LevelUpMenuArt.Tier(card.rarity));
                slot.deco.sprite = deco;
                slot.deco.gameObject.SetActive(deco != null);
                slot.delay = i * 0.08f;
                slot.fill.transform.SetAsLastSibling();
            }
            // 영웅·특수 카드가 있으면 창이 열릴 때 한 번만 소리(희소성 표현 — 추천과 무관, 추첨·효과 불변)
            if (!HitFeedback.LowEffects)
                foreach (var o in offers) if (o.card.rarity >= CardRarity.Epic) { AudioService.PlaySfx("skill_unlock"); break; }

            BeginHandoff();
            reasonLine.text = ReasonText(offers.Count > 0 ? offers[0].card.category : CardCategory.Cocktail, GameManager.Instance);
            RefreshTargetInfo();
            WireNavigation();
            inputGate.Arm(Time.frameCount);
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slots[0].root);
            var game=GameManager.Instance;
            if (!CardEducationGuide.SuppressAutomaticForTests && CardEducationGuide.NeedsIntroduction && game!=null && game.State==GameState.LevelUp)
                OpenTutorial();
        }

        /// <summary>테스트·QA용 — 슬롯의 목표 표식 문구(없으면 "")와 등급 테두리 두께(0 = 없음).</summary>
        public string GoalMarkText(int index) => index >= 0 && index < SlotCount && slots[index].goalMark.activeSelf ? slots[index].goalMarkText.text : "";
        public float RarityFrameWidth(int index) => index >= 0 && index < SlotCount && slots[index].rarityFrame.color.a > 0f ? (slots[index].rarityFrame.rectTransform.sizeDelta.x - slots[index].cardRt.sizeDelta.x) * 0.5f : 0f;

        /// <summary>2026-11-22 피드백 A(5·9·12): 효과를 줄 단위로(아이콘 + 능력치 색 이름 + 짧은 수치). 3줄까지, 줄이 많으면 줄 높이·글자만 줄인다.
        /// 능력치 효과가 하나도 없으면(특수 카드) 기존 효과 글(Desc)을 그대로 보인다.</summary>
        /// <summary>총을 고른 런에서 사거리 카드 = 탄환 유도 카드(CardSystem.Take가 같은 규칙으로 치환). 카드 ID·이름·도감은 그대로.</summary>
        static bool GunRangeCard(CardData card) =>
            GameManager.Instance != null && GameManager.Instance.Stats != null && GameManager.Instance.Stats.gunHoming && CardEffectsView.IsRangeCard(card);

        void BuildEffectLines(Slot slot, CardData card)
        {
            for (int c = slot.effects.childCount - 1; c >= 0; c--) DestroyImmediate(slot.effects.GetChild(c).gameObject);
            var list = new List<CardEffect>();
            bool gunRange = GunRangeCard(card); // 총: 사거리 효과 줄 대신 유도 설명 글(description)을 보인다
            if (card.effects != null) foreach (var e in card.effects) if (StatStyle.Has(e.type) && !(gunRange && e.type == CardEffectType.RangePercent)) list.Add(e);
            bool lines = list.Count > 0;
            slot.description.enabled = !lines;
            slot.effects.gameObject.SetActive(lines);
            effectLineTexts[slot.index] = lines ? string.Join(" / ", list.ConvertAll(e => StatStyle.LineText(e, card.weaponRestriction))) : "";
            previewTexts[slot.index] = "";
            if (!lines) return;
            int n = Mathf.Min(3, list.Count);
            float h = n <= 2 ? 44f : 30f; int font = n <= 2 ? 30 : 24;
            // P5(감사 S-10): 효과가 한 줄뿐이고 실제 최종값이 바로 계산되는 능력치면(StatStyle.Preview) 아래 둘째 줄에 "현재 → 선택 후".
            // 상자(92) = 효과 줄 44 + 미리보기 30. 추정이 필요한 능력치·여러 효과 카드는 미리보기 없음.
            var stats = GameManager.Instance != null ? GameManager.Instance.Stats : null;
            string preview = n == 1 ? StatStyle.Preview(list[0], stats) : "";
            previewTexts[slot.index] = preview;
            const float PreviewH = 30f;
            float top = (EffectBoxHeight - n * h - (preview.Length > 0 ? PreviewH : 0f)) * 0.5f; // 줄 묶음을 상자 세로 가운데
            for (int k = 0; k < n; k++)
            {
                var row = StatStyle.BuildLine(slot.effects, "Effect" + k, list[k], card.weaponRestriction, 400f, h, font);
                row.anchoredPosition = new Vector2(0f, -top - k * h);
            }
            if (preview.Length > 0)
            {
                var prt = UiFactory.Place("Preview", slot.effects, new Vector2(0.5f, 1f), new Vector2(0f, -top - n * h), new Vector2(400f, PreviewH));
                prt.pivot = new Vector2(0.5f, 1f);
                var pt = UiFactory.AddText(prt, preview, 22, TextAnchor.MiddleCenter, new Color(0.86f, 0.86f, 0.9f));
                pt.resizeTextForBestFit = true; pt.resizeTextMinSize = 16; pt.resizeTextMaxSize = 22;
                pt.verticalOverflow = VerticalWrapMode.Truncate;
                pt.raycastTarget = false;
            }
        }

        readonly string[] previewTexts = new string[SlotCount];
        /// <summary>테스트·QA용 — 카드 효과 "현재 → 선택 후" 미리보기 글(없으면 "").</summary>
        /// <summary>검증용(읽기 전용): 카드 슬롯의 효과 설명 글.</summary>
        public string DescriptionText(int index) => index >= 0 && index < SlotCount && slots[index].description.enabled ? slots[index].description.text : "";
        public string EffectPreview(int index) => index >= 0 && index < SlotCount ? previewTexts[index] ?? "" : "";

        readonly string[] effectLineTexts = new string[SlotCount];
        /// <summary>테스트·QA용 — 카드 효과 줄 글("공격력 +15% / 공격 속도 +20%"), 특수 카드는 "".</summary>
        public string EffectLines(int index) => index >= 0 && index < SlotCount ? effectLineTexts[index] ?? "" : "";
        public int EffectLineCount(int index)
        {
            if (index < 0 || index >= SlotCount || !slots[index].effects.gameObject.activeSelf) return 0;
            int n = 0; // "현재 → 선택 후" 미리보기 줄(Preview)은 효과 줄이 아니다
            foreach (Transform c in slots[index].effects) if (c.name.StartsWith("Effect")) n++;
            return n;
        }
        public static readonly Color RushOffColor = new Color(0.78f, 0.2f, 0.18f);
        public string JobChipText(int index) => JobChipVisible(index) ? slots[index].jobChipText.text : "";
        public bool JobChipVisible(int index) => index >= 0 && index < SlotCount && slots[index].jobChip.activeSelf;

        /// <summary>추천 근거 문구의 조각을 색으로 구분: 히든(숨은 조합·히든) = 금색, 직업 연계(러시·우림) = 직업 색. 나머지는 원래 색.</summary>
        public static string TintAdvice(string reason, Job? job)
        {
            if (string.IsNullOrEmpty(reason)) return reason;
            var parts = reason.Split(new[] { " · " }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                Color? c = p.Contains("꺼짐") ? RushOffColor
                    : p.Contains("숨은 조합") || p.Contains("히든") ? StatStyle.HiddenColor
                    : p.Contains("러시") || p.Contains("우림") ? StatStyle.JobColor(job ?? (p.Contains("우림") ? Job.TeaSommelier : Job.Bartender)) : (Color?)null;
                if (c.HasValue) parts[i] = "<color=#" + ColorUtility.ToHtmlStringRGB(c.Value) + ">" + p + "</color>";
            }
            return string.Join(" · ", parts);
        }

        /// <summary>피드백 11 + 2026-11-22 피드백 A(2·17): 등급 테두리(일반 없음 / 희귀 6px / 영웅·특수 10px) + 희귀 이상 테두리 빛(숨쉬기),
        /// 영웅·특수 빛줄기 반복(2.6초마다), 모든 카드 "잔 채우기" 등장(카드마다 0.08초 차이), hover·선택 확대(1.05).
        /// 저이펙트: 등장·빛줄기·숨쉬기 없이 테두리·고정 빛만(확대는 유지 — 선택 위치 표시).</summary>
        void ApplyRarityStyle(Slot slot, CardRarity r)
        {
            var c = RarityTable.Color(r);
            float w = r == CardRarity.Common ? 0f : r == CardRarity.Rare ? 6f : 10f;
            slot.rarityValue = r;
            slot.rarityFrame.color = w > 0f ? new Color(c.r, c.g, c.b, 0.95f) : Color.clear;
            slot.rarityFrame.rectTransform.sizeDelta = slot.cardRt.sizeDelta + new Vector2(2f * w, 2f * w);
            slot.glow.color = r == CardRarity.Common ? Color.clear : new Color(c.r, c.g, c.b, GlowBase);
            slot.sweep.color = Color.clear;
            slot.sweepPhase = 0f;
            slot.cardRt.localScale = Vector3.one;
            slot.hoverScale = 1f;
            bool low = HitFeedback.LowEffects;
            slot.enterT = low ? -1f : 0f;
            slot.sweepOn = r >= CardRarity.Epic && !low;
            slot.group.alpha = low ? 1f : 0f;
            slot.fill.color = slot.fillSurface.color = Color.clear;
            slot.fill.rectTransform.sizeDelta = new Vector2(slot.fill.rectTransform.sizeDelta.x, 0f);
        }

        public const float FocusTintAlpha = 0.16f;
        public float FocusAlpha(int index) => index >= 0 && index < SlotCount ? slots[index].focusTint.color.a : 0f;
        public const float PourSeconds = 0.34f, PourFadeSeconds = 0.18f, HoverScale = 1.05f, GlowBase = 0.22f;
        /// <summary>테스트·QA용 — 등장 연출이 끝났는지, 카드 확대 배율, 테두리 빛 알파, 카드 투명도.</summary>
        public bool EntranceDone(int index) => index >= 0 && index < SlotCount && slots[index].enterT < 0f;
        public float CardScale(int index) => index >= 0 && index < SlotCount ? slots[index].cardRt.localScale.x : 1f;
        public float GlowAlpha(int index) => index >= 0 && index < SlotCount ? slots[index].glow.color.a : 0f;
        public float CardAlpha(int index) => index >= 0 && index < SlotCount ? slots[index].group.alpha : 1f;

        void TickEntrance()
        {
            var es = EventSystem.current;
            bool low = HitFeedback.LowEffects;
            foreach (var s in slots)
            {
                if (s == null || !s.root.activeSelf) continue;
                float entrance = s.settle; // 건네기 정착 중 94% → 100%(기존 채우기 등장과 동시에 쓰지 않는다)
                if (s.enterT >= 0f)
                {
                    s.enterT += Time.unscaledDeltaTime;
                    float t = s.enterT - s.delay;
                    float k = Mathf.Clamp01(t / PourSeconds);
                    float ease = 1f - (1f - k) * (1f - k);
                    entrance = Mathf.Lerp(0.96f, 1f, ease);
                    s.group.alpha = Mathf.Clamp01(k * 1.4f); // 잔이 차오르며 내용이 드러남
                    var rc = RarityTable.Color(s.rarityValue);
                    var liquid = Color.Lerp(new Color(0.95f, 0.62f, 0.28f), rc, 0.45f);
                    float fade = t <= PourSeconds ? 1f : 1f - Mathf.Clamp01((t - PourSeconds) / PourFadeSeconds);
                    bool pouring = t > 0f;
                    var frt = s.fill.rectTransform;
                    frt.sizeDelta = new Vector2(frt.sizeDelta.x, s.cardRt.sizeDelta.y * k);
                    s.fill.color = pouring ? new Color(liquid.r, liquid.g, liquid.b, 0.5f * fade) : Color.clear;
                    s.fillSurface.color = pouring && k < 1f ? new Color(1f, 0.96f, 0.85f, 0.85f) : Color.clear;
                    if (t >= PourSeconds + PourFadeSeconds)
                    {
                        s.enterT = -1f; s.group.alpha = 1f;
                        s.fill.color = s.fillSurface.color = Color.clear;
                        frt.sizeDelta = new Vector2(frt.sizeDelta.x, 0f);
                    }
                }
                if (s.sweepOn && s.enterT < 0f)
                {
                    // 영웅·특수: 빛줄기가 2.6초마다 카드 안을 한 번 지나간다(효과 글을 가리지 않게 반투명 최대 0.35)
                    s.sweepPhase += Time.unscaledDeltaTime;
                    float st = Mathf.Clamp01(s.sweepPhase / 0.45f);
                    var srt = s.sweep.rectTransform;
                    srt.anchoredPosition = new Vector2(Mathf.Lerp(20f, 380f, st), 0f);
                    var c = s.rarityFrame.color; c.a = 0.35f * Mathf.Sin(st * Mathf.PI);
                    s.sweep.color = c;
                    if (s.sweepPhase >= 2.6f) s.sweepPhase = 0f;
                }
                if (s.rarityValue != CardRarity.Common)
                {
                    var g = s.glow.color;
                    g.a = low ? GlowBase : GlowBase + 0.12f * Mathf.Sin(Time.unscaledTime * 2.6f + s.index);
                    s.glow.color = g;
                }
                bool focus = s.hovered || (es != null && es.currentSelectedGameObject == s.root);
                s.hoverScale = Mathf.MoveTowards(s.hoverScale, focus ? HoverScale : 1f, Time.unscaledDeltaTime * 0.5f / 0.12f);
                float scale = entrance * s.hoverScale;
                var v = new Vector3(scale, scale, 1f);
                s.cardRt.localScale = v;
                float focusK = Mathf.InverseLerp(1f, HoverScale, s.hoverScale);
                var tc = Color.Lerp(RarityTable.Color(s.rarityValue), Color.white, 0.5f);
                s.focusTint.color = new Color(tc.r, tc.g, tc.b, FocusTintAlpha * focusK);
                s.rarityFrame.rectTransform.localScale = v;
                s.glow.rectTransform.localScale = v;
            }
        }

        /// <summary>57 P1 "카드 창이 왜 열렸나 · 다음 카드 창 조건" — 실제 게이지 기준(RunGuide.NextCardStatus).</summary>
        public static string ReasonText(CardCategory category, GameManager g)
        {
            if (g == null) return "";
            var type = category == CardCategory.Tea ? IngredientType.Tea : IngredientType.Cocktail;
            return $"{RunGuide.IngredientName(type, g.Job)} 게이지가 가득 차서 {RunGuide.CardKind(type)} 카드를 고릅니다 · 다음 카드 창: {RunGuide.NextCardStatus(g)}";
        }

        public Button ChangeTargetButton { get; private set; }
        /// <summary>지금 제시 중인 카드(QA·테스트용, 읽기 전용).</summary>
        public IReadOnlyList<CardOffer> Offers => offers;
        public string ReasonLineText => reasonLine != null ? reasonLine.text : "";
        public string TargetLineText => targetLine != null ? targetLine.text : "";
        public string InfluenceText(int index) => index >= 0 && index < SlotCount && slots[index].influenceRoot.activeSelf ? slots[index].influence.text : "";

        /// <summary>목표 조합 줄과 카드별 진행 띠를 실제 런 상태로 다시 그린다(목표를 바꾼 뒤에도 호출).</summary>
        public void RefreshTargetInfo()
        {
            var g = GameManager.Instance;
            bool hasGame = g != null && g.Player != null;
            targetLine.gameObject.SetActive(hasGame);
            ChangeTargetButton.gameObject.SetActive(hasGame);
            for (int i = 0; i < SlotCount; i++) { slots[i].influenceRoot.SetActive(false); slots[i].goalMark.SetActive(false); }
            if (!hasGame) return;
            string key = MobilePlatform.IsTouchUI ? "" : " [T]";
            if (!g.ComboTarget.HasValue)
            {
                targetLine.text = $"목표 조합 미지정 · 목표 바꾸기{key}로 원하는 히든을 정하면 필요한 카드가 표시됩니다";
                return;
            }
            var def = HiddenSkillDatabase.Get(g.ComboTarget.Value);
            var state = g.HiddenState;
            bool discovered = g.HiddenDiscovered(def.id);
            string name = HiddenRequirements.DisplayName(def, g.Job, discovered);
            targetLine.text = $"목표 {name}: {HiddenRequirements.ProgressLine(def, g.Job, state)}";
            for (int i = 0; i < SlotCount && i < offers.Count; i++)
            {
                if (!HiddenRequirements.CardAdvances(def, g.Job, state, offers[i].card.id, out var before, out bool completes)) continue;
                string step = $"{JobStudyTheme.Text(before.req.label, g.Job)} {before.current}→{before.current + 1}/{before.req.count}";
                slots[i].influence.text = completes ? $"목표 '{name}' 조건 완성!" : $"목표 진행 · {step}";
                slots[i].influenceRoot.SetActive(true);
                // 피드백 12: 왼쪽 위 목표 표식(등급 테두리와 다른 자리·모양). 마지막 조건이면 금색 "목표 완성".
                slots[i].goalMark.SetActive(true);
                slots[i].goalMarkText.text = completes ? "★ 목표 완성" : "◎ 목표";
                slots[i].goalMark.GetComponent<Image>().color = completes ? new Color(0.85f, 0.62f, 0.12f, 0.98f) : new Color(0.1f, 0.55f, 0.6f, 0.95f);
                slots[i].goalMarkText.color = completes ? new Color(0.16f, 0.08f, 0.02f) : Color.white;
            }
        }

        void Update()
        {
            // 08:23 "키 관통 없음" — Pause가 이 화면 위에 겹쳐 열려 있으면(오버레이 스택) 밑에 깔린
            // 이 화면의 단축키(1/2/3)가 새어 들어오면 안 된다.
            if (IsOpen) TickHandoff();
            if (IsOpen) TickEntrance(); // 등장 연출은 학습 창·일시정지와 무관하게 끝까지(입력과 무관)
            if (!IsOpen || IsLearningOpen || (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;

            // 매 프레임 눌림 상태를 봐야 "뗐다가 다시 누름"을 구분한다(클릭/Submit 경로도 이 판정을 공유).
            inputGate.Poll(Time.frameCount, InputReleaseGate.AnyConfirmHeld());
            var kb = Keyboard.current;
            if (kb == null || inputGate.Locked || handoffRunning) return; // 건네기 중에는 사용자 입력 선택 없음(테스트 API Choose는 영향 없음)
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        void OnDisable() { CancelHandoff(); }

        // ---- 2026-11-22 메뉴판 건네기 / 2026-11-23 카드별 건네기 ----
        /// <summary>테스트·QA용 — 건네기 재생 중 여부(어느 카드든), 가장 늦게 정착하는(최고 등급) 재생 중 카드의 등급·프레임·스프라이트, 재생 중 카드들의 최소 노출 비율.</summary>
        public bool HandoffPlaying => handoffRunning;
        public int HandoffTier { get { var s = LeadSlot(); return s != null ? s.handoff.Tier : -1; } }
        public int HandoffFrame { get { var s = LeadSlot(); return s != null ? s.handoff.Frame : -1; } }
        public float HandoffReveal
        {
            get
            {
                if (!handoffRunning) return 0f;
                float min = 1f;
                for (int i = 0; i < offers.Count && i < SlotCount; i++) if (slots[i].handoff.Active) min = Mathf.Min(min, slots[i].handoff.Reveal);
                return min;
            }
        }
        public Sprite HandoffSprite { get { var s = LeadSlot(); return s != null ? s.handoff.CurrentSprite : null; } }
        /// <summary>카드별: 재생 중 여부·등급(-1 = 없음)·프레임·스프라이트·건네기 앵커 위치(슬롯 시작 좌표)·재생 속도.</summary>
        public bool SlotHandoffPlaying(int index) => index >= 0 && index < SlotCount && slots[index].handoff.Active;
        public int SlotHandoffTier(int index) => SlotHandoffPlaying(index) ? slots[index].handoff.Tier : -1;
        public int SlotHandoffFrame(int index) => SlotHandoffPlaying(index) ? slots[index].handoff.Frame : -1;
        public Sprite SlotHandoffSprite(int index) => SlotHandoffPlaying(index) ? slots[index].handoff.CurrentSprite : null;
        public Vector2 SlotHandoffAnchor(int index) => slots[index].handoffImage.rectTransform.anchoredPosition;
        public float SlotHandoffSpeed(int index) => SlotHandoffPlaying(index) ? slots[index].handoff.Speed : 0f;
        public Sprite SlotDecoration(int index) => index >= 0 && index < SlotCount && slots[index].deco.gameObject.activeSelf ? slots[index].deco.sprite : null;
        public bool InputLocked => inputGate.Locked;
        public Vector2 SlotPosition(int index) => slots[index].cardRt.anchoredPosition;
        public bool SlotInteractive(int index) => slots[index].group.interactable && slots[index].group.blocksRaycasts;
        public Button SlotButton(int index) => slots[index].root.GetComponent<Button>();
        public Button LearnButton(int index) => slots[index].learn;
        public Button LearningHelpButton { get; private set; }

        /// <summary>재생 중인 카드 가운데 가장 늦게 정착하는(최고 등급, 같으면 앞) 카드.</summary>
        Slot LeadSlot()
        {
            Slot lead = null;
            for (int i = 0; i < offers.Count && i < SlotCount; i++)
            {
                var s = slots[i];
                if (!s.handoff.Active) continue;
                if (lead == null || s.handoff.Tier > lead.handoff.Tier) lead = s;
            }
            return lead;
        }

        void PlaceSlot(Slot s, float offsetFraction, float scale)
        {
            var pos = new Vector2(s.baseX * offsetFraction, -30f);
            s.cardRt.anchoredPosition = pos;
            s.rarityFrame.rectTransform.anchoredPosition = pos;
            s.glow.rectTransform.anchoredPosition = pos;
            s.settle = scale;
        }

        void SetSlotReveal(Slot s, float k, bool interactive)
        {
            s.group.alpha = k;
            s.frameGroup.alpha = k;
            s.glowGroup.alpha = k;
            s.group.interactable = interactive;
            s.group.blocksRaycasts = interactive;
        }

        /// <summary>카드마다 자기 등급의 동작을 그 슬롯의 시작 좌표(최종 가로 오프셋 88%)에서 동시에 재생한다. 판 앵커가 카드 시작 위치와 같아
        /// 마지막 포즈의 메뉴판이 그 카드로 이어지고, 카드는 자기 전달이 끝나면 거기서 최종 좌표·크기로 정착한다(카드마다 별도 대기 없음).
        /// 저이펙트·후보 없음·직업 클립 없음은 기존처럼 즉시(또는 기존 등장) 경로.</summary>
        void BeginHandoff()
        {
            var game = GameManager.Instance;
            if (offers.Count == 0 || game == null || HitFeedback.LowEffects) return;
            float speed = LevelUpMenuArt.PlaybackSpeed; // 한 번 연 창의 카드들은 같은 속도
            for (int i = 0; i < offers.Count && i < SlotCount; i++)
            {
                var s = slots[i];
                if (s.handoffImage == null || offers[i].card == null) continue;
                int tier = LevelUpMenuArt.Tier(offers[i].card.rarity);
                var clip = LevelUpMenuArt.Frames(game.Job, tier);
                if (clip == null) continue;
                s.handoffImage.rectTransform.anchoredPosition = new Vector2(s.baseX * LevelUpMenuArt.StartOffsetFraction, LevelUpMenuArt.CardGroupAnchorY);
                s.handoff.Begin(s.handoffImage, clip, tier, speed);
                handoffRunning |= s.handoff.Active;
            }
            if (!handoffRunning) return;
            for (int i = 0; i < offers.Count && i < SlotCount; i++)
            {
                var s = slots[i];
                s.enterT = -1f; // 기존 잔 채우기 등장은 이 전환으로 대체
                s.fill.color = s.fillSurface.color = Color.clear;
                bool playing = s.handoff.Active;
                PlaceSlot(s, playing ? LevelUpMenuArt.StartOffsetFraction : 1f, playing ? LevelUpMenuArt.StartScale : 1f);
                SetSlotReveal(s, playing ? 0f : 1f, false); // 클립이 없는 카드는 바로 보이되 모두 정착할 때까지 입력은 닫힘
            }
        }

        void TickHandoff()
        {
            if (!handoffRunning) return;
            bool allDone = true;
            for (int i = 0; i < offers.Count && i < SlotCount; i++)
            {
                var s = slots[i];
                if (!s.handoff.Active) continue;
                bool done = s.handoff.Tick(Time.unscaledDeltaTime);
                float k = done ? 1f : s.handoff.Reveal;
                PlaceSlot(s, Mathf.Lerp(LevelUpMenuArt.StartOffsetFraction, 1f, k), Mathf.Lerp(LevelUpMenuArt.StartScale, 1f, k));
                SetSlotReveal(s, k, false);
                if (!done) allDone = false;
            }
            if (!allDone) return;
            handoffRunning = false;
            for (int i = 0; i < offers.Count && i < SlotCount; i++) SetSlotReveal(slots[i], 1f, true);
            // 모든 카드가 정착한 뒤에만 입력을 새로 무장한다 — 전달 중 눌린 키·버튼이 정착 순간 선택으로 이어지지 않는다.
            inputGate.Arm(Time.frameCount);
        }

        /// <summary>닫기·재오픈·비활성화에서 재생 상태를 지운다(잔상·잠금 없음): 위치·크기·투명도·상호작용을 최종 상태로 되돌린다.</summary>
        void CancelHandoff()
        {
            bool was = handoffRunning;
            handoffRunning = false;
            foreach (var s in slots)
            {
                if (s == null || s.cardRt == null) continue;
                s.handoff.Stop();
                if (was) s.enterT = -1f;
                PlaceSlot(s, 1f, 1f);
                SetSlotReveal(s, 1f, true);
            }
        }

        /// <summary>2026-11-23 모드별 포커스 묶음: 카드 창의 키보드 이동을 이 창 안(카드 ↔ 지식 버튼 ↔ 목표 바꾸기 ↔ 학습 안내)으로 명시한다.
        /// 자동 이동은 덮개 아래의 HUD(일시정지·조작 방식·히든·목표) 버튼이나 마스터 도전 준비 패널(반복·박람회·전투 시작·음료 만들기)로
        /// 포커스를 넘길 수 있었다 — 보이지 않는 그 버튼에서 Enter가 눌리면 카드 창 아래 화면이 조작된다. 숨긴 버튼(게임 없음의 목표 바꾸기)은 대상에서 뺀다.
        /// 마우스·터치는 기존 전체 덮개가 막는다. 닫히면 Choose가 열기 전 포커스를 복원한다.</summary>
        void WireNavigation()
        {
            int n = Mathf.Min(offers.Count, SlotCount);
            bool changeOn = ChangeTargetButton != null && ChangeTargetButton.gameObject.activeSelf;
            Selectable top = changeOn ? ChangeTargetButton : (Selectable)LearningHelpButton;
            Selectable first = n > 0 ? SlotButton(0) : null;
            for (int i = 0; i < SlotCount; i++)
            {
                bool used = i < n;
                SlotButton(i).navigation = !used ? new Navigation { mode = Navigation.Mode.None } : new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? SlotButton(i - 1) : null,
                    selectOnRight = i + 1 < n ? SlotButton(i + 1) : null,
                    selectOnUp = top,
                    selectOnDown = slots[i].learn,
                };
                slots[i].learn.navigation = !used ? new Navigation { mode = Navigation.Mode.None } : new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? slots[i - 1].learn : null,
                    selectOnRight = i + 1 < n ? slots[i + 1].learn : null,
                    selectOnUp = SlotButton(i),
                };
            }
            if (ChangeTargetButton != null)
                ChangeTargetButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit, selectOnDown = first, selectOnUp = LearningHelpButton, selectOnRight = LearningHelpButton,
                };
            if (LearningHelpButton != null)
                LearningHelpButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnDown = changeOn ? ChangeTargetButton : first,
                    selectOnLeft = changeOn ? ChangeTargetButton : null,
                };
        }

        public void OpenLearning(int index)
        {
            if (!IsOpen || IsLearningOpen || index < 0 || index >= offers.Count) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            learning.Show(offers[index].card);
        }
        public void OpenTutorial()
        {
            if(!IsOpen || IsLearningOpen || offers.Count==0 || (GameManager.Instance!=null && GameManager.Instance.IsPaused))return;
            tutorial.Show(offers[0].card);
        }
        public void CloseLearning() { if(IsTutorialOpen)tutorial.Close();else learning.Close(); }

        /// <summary>버튼 클릭/Submit 경로 — 08:39 입력 잠금을 거친다(테스트는 Choose를 직접 호출).</summary>
        void ChooseFromInput(int index)
        {
            if (handoffRunning || !inputGate.Allows(Time.frameCount, InputReleaseGate.AnyConfirmHeld())) return;
            Choose(index);
        }

        /// <summary>인덱스의 카드를 선택. 이미 닫혔거나 범위 밖이거나 Pause가 위에 열려 있으면 무시한다
        /// (08:23 "키 관통 없음" — 클릭도 마찬가지로 막는다, 방어적으로 Update 가드와 이중 확인).</summary>
        public void Choose(int index)
        {
            if (!IsOpen || IsLearningOpen || onChosen == null || index < 0 || index >= offers.Count) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            var callback = onChosen;
            var offer = offers[index];
            onChosen = null;
            CancelHandoff();
            root.SetActive(false);
            var restore = focusBeforeOpen;
            callback(offer);
            // 전환 복원: 이어서 열린 화면(다음 카드·하이브리드·커피 트리)이 포커스를 가져가지 않았으면 열기 전 포커스로(예: 마스터 준비 패널).
            // 같은 호출에서 카드 창이 다시 열렸으면(연속 보상) 처음 열기 전 포커스를 그대로 들고 간다.
            if (IsOpen) return;
            focusBeforeOpen = null;
            var es = EventSystem.current;
            if (es == null) return;
            var now = es.currentSelectedGameObject;
            bool lost = now == null || !now.activeInHierarchy || now.transform.IsChildOf(transform);
            if (lost) es.SetSelectedGameObject(restore != null && restore.activeInHierarchy ? restore : null);
        }
    }
}
