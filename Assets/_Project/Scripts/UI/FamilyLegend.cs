using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-10 몬스터 계열 기본 범례(상세 화면 신설 대신). 화면 아래 가운데 한 줄(y 14~66). 보스전(정규)에는 접고 보스 칩으로 바꾼다.
    /// 색만으로 구분하지 않도록 글자 표식([칵]/[커]/[차]/[중])과 이름을 함께 쓰고, 지금 마신 음료의 상성 대상을 표시한다.
    /// 판정·스폰과 무관한 읽기 전용 텍스트(새 아트·VFX 없음). 전투 화면이 아닐 때는 투명도로만 숨긴다.
    /// </summary>
    public class FamilyLegend : MonoBehaviour
    {
        public const float Y = 14f, W = 1100f, H = 26f;
        /// <summary>2026-11-12 마스터 도전(보스 체력바 없음)은 그림이 읽히게 40, 정규 모드는 보스 체력바(y 33~) 아래 26 그대로.</summary>
        public const float MasterH = 52f;
        /// <summary>2026-11-12 정규 모드 비보스 높이(26 → 60, 720p 판독 — 단계 배지 y 92 아래). 보스전에는 범례 전체를 접고
        /// 오른쪽 아래 상성 칩(마신 잔 → 대상 계열, 보스 체력바 밖)만 크게(BossChip).</summary>
        public const float StandardH = 60f;
        public const float ChipW = 380f, ChipH = 64f, ChipRight = 30f, ChipBottom = 24f, TouchChipY = 136f;
        RectTransform bossChip;
        CanvasGroup chipGroup;
        Image chipDrink, chipTarget;
        Text chipLabel;
        public bool BossChipVisible => chipGroup != null && chipGroup.alpha > 0f;
        public bool LegendVisible => group != null && group.alpha > 0f;
        public string BossChipText => chipLabel != null ? chipLabel.text : "";
        public RectTransform BossChipRect => bossChip;
        Image[] famIcons = new Image[4];
        Text[] famLabels = new Text[4];
        Image drinkIcon, targetIcon;
        Text drinkLabel, targetLabel;
        static readonly MonsterFamily[] Families = { MonsterFamily.Cocktail, MonsterFamily.Coffee, MonsterFamily.Tea, MonsterFamily.Neutral };
        /// <summary>시험·검수용: 그림 칸 상태.</summary>
        public bool TargetIconShown => targetIcon != null && targetIcon.enabled;
        public bool DrinkIconShown => drinkIcon != null && drinkIcon.enabled;
        public string AffinityText => (drinkLabel != null ? drinkLabel.text : "") + " " + (targetLabel != null ? targetLabel.text : "");
        const int Font = 18;
        GameManager game;
        Text label;
        CanvasGroup group;
        float nextRefresh;

        public string Text => label != null ? label.text : "";

        public static FamilyLegend Create(RectTransform hudRoot, GameManager game)
        {
            var rt = UiFactory.Place("FamilyLegend", hudRoot, new Vector2(0.5f, 0f), new Vector2(0f, Y), new Vector2(W, H));
            var view = rt.gameObject.AddComponent<FamilyLegend>();
            view.game = game;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            var bg = UiFactory.AddImage(rt, new Color(0.06f, 0.05f, 0.08f, 0.6f));
            bg.raycastTarget = false;
            view.label = UiFactory.AddText(UiFactory.Stretch("Label", rt), "", Font, TextAnchor.MiddleCenter, new Color(0.88f, 0.88f, 0.86f));
            view.label.resizeTextForBestFit = true;
            view.label.resizeTextMinSize = 12;
            view.label.resizeTextMaxSize = Font;
            view.label.raycastTarget = false;
            view.BuildVisual(rt, game != null && game.IsEndless ? MasterH : StandardH);
            view.BuildBossChip(hudRoot);
            view.Refresh();
            return view;
        }

        /// <summary>그림+짧은 라벨 줄: 계열 4칸(대표 적 그림 + [표식] 이름) | 마신 음료 그림 → 상성 대상 계열 그림 + 계열 이름 +% · 현재 잔 특성.
        /// 원래 한 줄 문구(Text)는 시험·읽기용으로 계속 갱신하되 화면에는 이 그림 줄을 쓴다.</summary>
        void BuildVisual(RectTransform rt, float h)
        {
            rt.sizeDelta = new Vector2(W, h);
            label.enabled = false;
            float icon = h - 6f;
            int font = h >= 56f ? 24 : Font; // 정규 모드 60 높이 → 720p ≈16px
            // 계열 칸 = 대표 적 그림(주표시, 높이 가득) + 오른쪽 아래 계열 잔 배지(칵테일잔·커피잔·찻잔). 중립은 그림이 없어 '중립 = 상성 없음' 글자.
            // 2026-11-12 720p 실측: 적 그림이 칸의 절반 크기면 10px대라 읽히지 않아 높이 가득·잔은 작은 배지로.
            for (int i = 0; i < Families.Length; i++)
            {
                bool neutral = Families[i] == MonsterFamily.Neutral;
                float x = 10f + i * 104f;
                famIcons[i] = HudIcons.Icon(rt, "FamRep" + i, new Vector2(0f, 0.5f), new Vector2(x, 0f), h - 2f, neutral ? null : HudIcons.Family(Families[i]));
                famIcons[i].rectTransform.pivot = new Vector2(0f, 0.5f);
                var cup = HudIcons.Icon(rt, "FamCup" + i, new Vector2(0f, 0f), new Vector2(x + h - 14f, 1f), h * 0.55f, neutral ? null : HudIcons.FamilyCup(Families[i]));
                cup.rectTransform.pivot = new Vector2(0f, 0f);
                famLabels[i] = HudIcons.Label(rt, "FamLabel" + i, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(neutral ? 150f : 0f, h - 4f),
                    neutral ? "중립 = 상성 없음" : "", font, new Color(0.9f, 0.9f, 0.88f), TextAnchor.MiddleLeft, 12);
                famLabels[i].rectTransform.pivot = new Vector2(0f, 0.5f);
                // 그림을 못 읽으면(로드 실패) 정직하게 표식 글자로
                if (!neutral && famIcons[i].sprite == null) { famLabels[i].text = Entry(Families[i]); famLabels[i].rectTransform.sizeDelta = new Vector2(100f, h - 4f); }
            }
            var sep = UiFactory.AddImage(UiFactory.Place("Sep", rt, new Vector2(0f, 0.5f), new Vector2(476f, 0f), new Vector2(2f, h - 8f)), new Color(1f, 0.85f, 0.45f, 0.6f));
            sep.raycastTarget = false;
            drinkIcon = HudIcons.Icon(rt, "DrinkIcon", new Vector2(0f, 0.5f), new Vector2(490f, 0f), icon, null);
            drinkIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            // 핵심 = 마신 잔 → 대상 계열 그림을 붙여서(2026-11-12 실측: 라벨 칸 170이라 화살표와 대상 그림 사이가 멀었다).
            const float labelW = 124f;
            drinkLabel = HudIcons.Label(rt, "DrinkLabel", new Vector2(0f, 0.5f), new Vector2(490f + icon + 4f, 0f), new Vector2(labelW, h - 4f), "", font, Color.white, TextAnchor.MiddleLeft, 12);
            drinkLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            float tx = 490f + icon + 8f + labelW;
            targetIcon = HudIcons.Icon(rt, "TargetIcon", new Vector2(0f, 0.5f), new Vector2(tx, 0f), h - 2f, null);
            targetIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            targetLabel = HudIcons.Label(rt, "TargetLabel", new Vector2(0f, 0.5f), new Vector2(tx + h + 2f, 0f), new Vector2(W - tx - h - 12f, h - 4f), "", font, new Color(1f, 0.86f, 0.45f), TextAnchor.MiddleLeft, 12);
            targetLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        /// <summary>범례 문구(테스트 공용). drink = 지금 상성을 일으키는 음료(없으면 null).</summary>
        public static string Compose(DrinkKind? drink)
        {
            string legend = "범례 " + Entry(MonsterFamily.Cocktail) + " · " + Entry(MonsterFamily.Coffee) + " · " +
                            Entry(MonsterFamily.Tea) + " · " + Entry(MonsterFamily.Neutral);
            var target = DrinkAffinity.TargetOf(drink);
            return target == MonsterFamily.Neutral
                ? legend + "  |  상성 없음(마신 음료 없음)"
                : legend + $"  |  상성 대상 {MonsterFamilies.Mark(target)} {MonsterFamilies.Name(target)} +{DrinkAffinity.BonusPercent}%";
        }

        static string Entry(MonsterFamily f) => MonsterFamilies.Mark(f) + " " + MonsterFamilies.Name(f);

        /// <summary>보스전(정규 모드) 전용: 보스 체력바(가운데 y 50–84, 폭 1000) 오른쪽 바깥 아래 모서리에 마신 잔 → 상성 대상 + % 칩.</summary>
        void BuildBossChip(RectTransform hudRoot)
        {
            // 터치 UI는 오른쪽 아래가 큰 버튼(연구·히든) 자리라 단계 배지(y 92~128) 위 가운데로.
            bossChip = MobilePlatform.IsTouchUI
                ? UiFactory.Place("BossAffinityChip", hudRoot, new Vector2(0.5f, 0f), new Vector2(0f, TouchChipY), new Vector2(ChipW, ChipH))
                : UiFactory.Place("BossAffinityChip", hudRoot, new Vector2(1f, 0f), new Vector2(-ChipRight, ChipBottom), new Vector2(ChipW, ChipH));
            chipGroup = bossChip.gameObject.AddComponent<CanvasGroup>();
            chipGroup.blocksRaycasts = false;
            chipGroup.alpha = 0f;
            UiFactory.AddImage(bossChip, new Color(0.06f, 0.05f, 0.08f, 0.72f)).raycastTarget = false;
            chipDrink = HudIcons.Icon(bossChip, "Drink", new Vector2(0f, 0.5f), new Vector2(8f, 0f), ChipH - 8f, null);
            chipDrink.rectTransform.pivot = new Vector2(0f, 0.5f);
            HudIcons.Label(bossChip, "Arrow", new Vector2(0f, 0.5f), new Vector2(ChipH + 2f, 0f), new Vector2(30f, ChipH - 8f), "→", 30, Color.white, TextAnchor.MiddleCenter, 18)
                .rectTransform.pivot = new Vector2(0f, 0.5f);
            chipTarget = HudIcons.Icon(bossChip, "Target", new Vector2(0f, 0.5f), new Vector2(ChipH + 34f, 0f), ChipH - 6f, null);
            chipTarget.rectTransform.pivot = new Vector2(0f, 0.5f);
            chipLabel = HudIcons.Label(bossChip, "Label", new Vector2(0f, 0.5f), new Vector2(ChipH * 2f + 34f, 0f), new Vector2(ChipW - ChipH * 2f - 42f, ChipH - 8f), "", 26, new Color(1f, 0.86f, 0.45f), TextAnchor.MiddleLeft, 16);
            chipLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        bool BossMode => game != null && !game.IsEndless && game.ActiveBoss != null && !game.ActiveBoss.IsDead;

        void Update()
        {
            bool combat = DrinkRunHud.CombatVisible(game);
            bool boss = combat && BossMode;
            group.alpha = combat && !boss ? 1f : 0f;
            if (chipGroup != null) chipGroup.alpha = boss ? 1f : 0f;
            if (combat && Time.unscaledTime >= nextRefresh) Refresh();
        }

        public void RefreshForTests() { Refresh(); Update(); }

        void RefreshBossChip(DrinkKind? drink)
        {
            if (chipLabel == null) return;
            var method = game?.ActiveRunCup?.recipe != null ? game.ActiveRunCup.recipe.coffeeMethod : (CoffeeBrewMethod)0;
            HudIcons.Set(chipDrink, HudIcons.Drink(drink, method));
            var target = DrinkAffinity.TargetOf(drink);
            HudIcons.Set(chipTarget, drink == null ? null : HudIcons.Family(target));
            var boss = game?.ActiveBoss;
            bool bossIsTarget = boss != null && drink != null && DrinkAffinity.Matches(drink, boss.Family);
            chipLabel.text = drink == null ? "마신 음료 없음 · 상성 없음"
                : $"+{DrinkAffinity.BonusPercent}%" + (boss != null ? (bossIsTarget ? " · 보스도 대상" : " · 보스는 대상 아님") : "");
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.5f;
            var drink = game != null && game.Player != null ? game.Player.AffinityDrink : null;
            label.text = Compose(drink);
            RefreshBossChip(drink);
            if (drinkIcon == null) return;
            var method = game?.ActiveRunCup?.recipe != null ? game.ActiveRunCup.recipe.coffeeMethod : (CoffeeBrewMethod)0;
            HudIcons.Set(drinkIcon, HudIcons.Drink(drink, method));
            var target = DrinkAffinity.TargetOf(drink);
            var trait = game?.Player != null ? game.Player.DrinkTrait : RunDrinkTraitKind.None;
            if (drink == null)
            {
                drinkLabel.text = "안 마심 →"; // 2026-11-12 정규 60높이·라벨칸 124에서 "마신 음료 없음 →"이 두 줄로 접혔다
                HudIcons.Set(targetIcon, null);
                targetLabel.text = "상성 없음";
            }
            else
            {
                drinkLabel.text = "마심 →"; // 마신 음료는 왼쪽 잔 그림으로(이름 반복 없음)
                if (drinkIcon.sprite == null) drinkLabel.text = $"{(drink == DrinkKind.Coffee ? "커피" : drink == DrinkKind.Tea ? "차" : "칵테일")} 마심 →"; // 그림 없으면 글자
                HudIcons.Set(targetIcon, HudIcons.Family(target));
                bool reward = game?.Master != null && game.Master.State.Segment == MasterSegment.Reward;
                bool fair = game?.Master != null && game.Master.State.Segment == MasterSegment.Fair;
                targetLabel.text = $"+{DrinkAffinity.BonusPercent}%" + (targetIcon.sprite == null ? $" {MonsterFamilies.Mark(target)} {MonsterFamilies.Name(target)} 계열" : "") +
                                   (fair ? " (박람회 손님은 중립)" : reward ? " (보상 손님)" : "") +
                                   (trait != RunDrinkTraitKind.None ? " · " + RunDrinkTrait.Short(trait) : "");
            }
        }
    }
}
