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
        }

        readonly Slot[] slots = new Slot[SlotCount];
        readonly List<CardOffer> offers = new List<CardOffer>();
        GameObject root;
        Action<CardOffer> onChosen;
        /// <summary>08:39 — 보상이 같은 호출에서 연달아 열릴 때(07:46) 직전 화면을 닫은 키/클릭/Submit이
        /// 이 화면을 바로 누르지 않도록, 연 프레임과 눌림 해제 전까지 사용자 입력 경로를 잠근다.</summary>
        readonly InputReleaseGate inputGate = new InputReleaseGate();

        public bool IsOpen => root != null && root.activeSelf;

        public static CardSelectView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("CardSelect", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.72f));

            var view = rootRt.gameObject.AddComponent<CardSelectView>();
            view.root = rootRt.gameObject;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 80f));
            UiFactory.AddText(title, "레벨업!  카드를 선택하세요", 56, TextAnchor.MiddleCenter, Color.white);

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
                new Vector2(80f, 40f)), $"[{index + 1}]", 30, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.5f));
            slot.rarity = UiFactory.AddText(UiFactory.Place("Rarity", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(380f, 44f)), "", 32, TextAnchor.MiddleCenter, Color.white);
            slot.title = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(390f, 110f)), "", 44, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SkinHeader(slot.title);
            slot.description = UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(390f, 220f)), "", 24, TextAnchor.UpperCenter,
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

        public void Show(List<CardOffer> newOffers, Action<CardOffer> callback)
        {
            offers.Clear();
            offers.AddRange(newOffers);
            onChosen = callback;

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
                slot.categoryIcon.rectTransform.anchoredPosition = finalForm.HasValue ? new Vector2(170f,250f) : new Vector2(0f,-115f);
                slot.categoryIcon.rectTransform.sizeDelta = finalForm.HasValue ? new Vector2(40f,40f) : new Vector2(80f,80f);
                slot.stacks.text = offer.currentStacks == 0
                    ? "신규"
                    : $"Lv {offer.currentStacks} → {offer.currentStacks + 1}  (최대 {card.maxStacks})";
                slot.odds.text = offer.earlyRangedPriority ? "초반 원거리 우선 추첨 (60%)" : $"{RarityTable.Label(card.rarity)} 등급 확률 {offer.rarityPercent:0.#}%";
                UiFactory.SetChoice(slot.background, false,
                    Color.Lerp(new Color(0.16f, 0.13f, 0.2f), rarityColor, 0.18f));
                var icon = CategoryIcon(card.category);
                slot.categoryIcon.sprite = icon;
                slot.categoryIcon.gameObject.SetActive(icon != null);
            }

            inputGate.Arm(Time.frameCount);
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slots[0].root);
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
