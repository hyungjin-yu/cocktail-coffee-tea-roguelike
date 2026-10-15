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
            public Text key;
            public Text rarity;
            public Text title;
            public Text description;
            public Text stacks;
            public Text odds;
            public Image categoryIcon;
            public Text advice;
            public GameObject badge;
            public GameObject influenceRoot;
            public Text influence;
        }

        Text reasonLine, targetLine;

        readonly Slot[] slots = new Slot[SlotCount];
        readonly List<CardOffer> offers = new List<CardOffer>();
        readonly List<CardAdvice> advice = new List<CardAdvice>();
        GameObject root;
        Action<CardOffer> onChosen;
        /// <summary>08:39 — 보상이 같은 호출에서 연달아 열릴 때(07:46) 직전 화면을 닫은 키/클릭/Submit이
        /// 이 화면을 바로 누르지 않도록, 연 프레임과 눌림 해제 전까지 사용자 입력 경로를 잠근다.</summary>
        readonly InputReleaseGate inputGate = new InputReleaseGate();

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>49 추천 결과(테스트·QA용, 표시 중인 제시 순서와 같다).</summary>
        public IReadOnlyList<CardAdvice> Advice => advice;
        public string AdviceText(int index) => index >= 0 && index < SlotCount ? slots[index].advice.text : "";
        public bool BadgeVisible(int index) => index >= 0 && index < SlotCount && slots[index].badge.activeSelf;

        public static CardSelectView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("CardSelect", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.72f));
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

            slot.key = UiFactory.AddText(UiFactory.Place("Key", rt, new Vector2(0f, 1f), new Vector2(18f, -12f),
                new Vector2(80f, 40f)), MobilePlatform.IsTouchUI ? "" : $"[{index + 1}]", 30, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.5f));
            slot.rarity = UiFactory.AddText(UiFactory.Place("Rarity", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(380f, 44f)), "", 32, TextAnchor.MiddleCenter, Color.white);
            slot.title = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(390f, 110f)), "", 44, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SkinHeader(slot.title);
            slot.description = UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(390f, 220f)), "", 24,
                MobilePlatform.IsTouchUI ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter, // 2026-10-12 모바일: 설명 세로 가운데
                new Color(0.92f, 0.92f, 0.92f));
            // r13 검수 U2 / 09:54 — 카드별 일러스트 대신 기존 재료 아이콘을 카테고리 도안으로 쓴다(하이브리드만 전용 아이콘).
            // 설명(최대 2줄)과 중첩 문구 사이의 빈 영역에 둔다. 스프라이트는 Show에서 카테고리별로 고른다.
            var iconRt = UiFactory.Place("CategoryIcon", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -115f),
                new Vector2(80f, 80f)); // 카드 중심 기준 -75..-155, 중첩 문구(-160..-200) 위
            slot.categoryIcon = iconRt.gameObject.AddComponent<Image>();
            slot.categoryIcon.preserveAspect = true;
            slot.categoryIcon.raycastTarget = false;
            slot.stacks = UiFactory.AddText(UiFactory.Place("Stacks", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(380f, 40f)), "", 28, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.85f, 1f));
            // 49 추천: 근거 문구(기존 확률 문구 자리 — 확률 문구는 숨김 유지)와 카드 위 테두리의 "추천" 표식. 선택·포커스는 바꾸지 않는다.
            slot.advice = UiFactory.AddText(UiFactory.Place("Advice", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(400f, 70f)), "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.55f));
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
            slot.odds = UiFactory.AddText(UiFactory.Place("Odds", rt, new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(380f, 44f)), "", 28, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.6f));
            return slot;
        }

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
                slot.root.SetActive(used);
                if (!used) continue;

                var offer = offers[i];
                var card = offer.card;
                var rarityColor = RarityTable.Color(card.rarity);

                slot.rarity.text = RarityTable.Label(card.rarity);
                var job=GameManager.Instance;
                if(job!=null && ((job.Job==Job.Bartender && card.category==CardCategory.Cocktail && offer.currentStacks==0)
                    || (job.Job==Job.TeaSommelier && card.category==CardCategory.Tea && offer.currentStacks>0)))
                    slot.rarity.text+=" · 직업 연계";
                slot.rarity.color = rarityColor;
                slot.title.text = card.displayName;
                slot.description.text = card.description;
                var finalForm = AttackFinalForms.ForCard(card.id);
                if (finalForm.HasValue)
                    slot.description.text += $"\n현재 {offer.currentStacks} → 다음 {Mathf.Min(card.maxStacks,offer.currentStacks+1)} / {card.maxStacks}\n최대 중첩: {AttackFinalForms.Description(finalForm.Value)}";
                // 모바일은 설명을 가운데 정렬하므로 5줄 설명(아래 끝 -87)과 겹치지 않게 도안을 조금 아래(-145)·작게(50, 위 끝 -120, 중첩 문구 위 -180 위).
                bool touch = MobilePlatform.IsTouchUI;
                slot.categoryIcon.rectTransform.anchoredPosition = finalForm.HasValue ? new Vector2(170f,250f) : new Vector2(0f, touch ? -145f : -115f);
                slot.categoryIcon.rectTransform.sizeDelta = finalForm.HasValue ? new Vector2(40f,40f) : touch ? new Vector2(50f,50f) : new Vector2(80f,80f);
                slot.stacks.text = offer.currentStacks == 0
                    ? "신규"
                    : $"Lv {offer.currentStacks} → {offer.currentStacks + 1}  (최대 {card.maxStacks})";
                slot.odds.text = offer.earlyRangedPriority ? "초반 원거리 우선 추첨 (60%)" : $"{RarityTable.Label(card.rarity)} 등급 확률 {offer.rarityPercent:0.#}%";
                slot.odds.gameObject.SetActive(false);
                var a = advice[i];
                slot.advice.text = a.reason;
                slot.advice.color = a.recommended ? new Color(1f, 0.86f, 0.55f) : new Color(0.85f, 0.85f, 0.9f, 0.85f);
                slot.badge.SetActive(a.recommended);
                UiFactory.SetChoice(slot.background, false,
                    Color.Lerp(new Color(0.16f, 0.13f, 0.2f), rarityColor, 0.18f));
                var icon = CategoryIcon(card.category);
                slot.categoryIcon.sprite = icon;
                slot.categoryIcon.gameObject.SetActive(icon != null);
            }

            reasonLine.text = ReasonText(offers.Count > 0 ? offers[0].card.category : CardCategory.Cocktail, GameManager.Instance);
            RefreshTargetInfo();
            inputGate.Arm(Time.frameCount);
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slots[0].root);
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
            for (int i = 0; i < SlotCount; i++) slots[i].influenceRoot.SetActive(false);
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
            }
        }

        void Update()
        {
            // 08:23 "키 관통 없음" — Pause가 이 화면 위에 겹쳐 열려 있으면(오버레이 스택) 밑에 깔린
            // 이 화면의 단축키(1/2/3)가 새어 들어오면 안 된다.
            if (!IsOpen || (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;

            // 매 프레임 눌림 상태를 봐야 "뗐다가 다시 누름"을 구분한다(클릭/Submit 경로도 이 판정을 공유).
            inputGate.Poll(Time.frameCount, InputReleaseGate.AnyConfirmHeld());
            var kb = Keyboard.current;
            if (kb == null || inputGate.Locked) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        /// <summary>버튼 클릭/Submit 경로 — 08:39 입력 잠금을 거친다(테스트는 Choose를 직접 호출).</summary>
        void ChooseFromInput(int index)
        {
            if (!inputGate.Allows(Time.frameCount, InputReleaseGate.AnyConfirmHeld())) return;
            Choose(index);
        }

        /// <summary>인덱스의 카드를 선택. 이미 닫혔거나 범위 밖이거나 Pause가 위에 열려 있으면 무시한다
        /// (08:23 "키 관통 없음" — 클릭도 마찬가지로 막는다, 방어적으로 Update 가드와 이중 확인).</summary>
        public void Choose(int index)
        {
            if (!IsOpen || onChosen == null || index < 0 || index >= offers.Count) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            var callback = onChosen;
            var offer = offers[index];
            onChosen = null;
            root.SetActive(false);
            callback(offer);
        }
    }
}
