using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-10 몬스터 계열 기본 범례(상세 화면 신설 대신). 화면 아래 가운데 한 줄 — 보스 체력바(y 33~67) 아래.
    /// 색만으로 구분하지 않도록 글자 표식([칵]/[커]/[차]/[중])과 이름을 함께 쓰고, 지금 마신 음료의 상성 대상을 표시한다.
    /// 판정·스폰과 무관한 읽기 전용 텍스트(새 아트·VFX 없음). 전투 화면이 아닐 때는 투명도로만 숨긴다.
    /// </summary>
    public class FamilyLegend : MonoBehaviour
    {
        public const float Y = 14f, W = 1100f, H = 26f;
        /// <summary>2026-11-12 마스터 도전(보스 체력바 없음)은 그림이 읽히게 40, 정규 모드는 보스 체력바(y 33~) 아래 26 그대로.</summary>
        public const float MasterH = 52f;
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
            view.BuildVisual(rt, game != null && game.IsEndless ? MasterH : H);
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
                    neutral ? "중립 = 상성 없음" : "", Font, new Color(0.9f, 0.9f, 0.88f), TextAnchor.MiddleLeft, 12);
                famLabels[i].rectTransform.pivot = new Vector2(0f, 0.5f);
                // 그림을 못 읽으면(로드 실패) 정직하게 표식 글자로
                if (!neutral && famIcons[i].sprite == null) { famLabels[i].text = Entry(Families[i]); famLabels[i].rectTransform.sizeDelta = new Vector2(100f, h - 4f); }
            }
            var sep = UiFactory.AddImage(UiFactory.Place("Sep", rt, new Vector2(0f, 0.5f), new Vector2(476f, 0f), new Vector2(2f, h - 8f)), new Color(1f, 0.85f, 0.45f, 0.6f));
            sep.raycastTarget = false;
            drinkIcon = HudIcons.Icon(rt, "DrinkIcon", new Vector2(0f, 0.5f), new Vector2(490f, 0f), icon, null);
            drinkIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            drinkLabel = HudIcons.Label(rt, "DrinkLabel", new Vector2(0f, 0.5f), new Vector2(490f + icon + 4f, 0f), new Vector2(170f, h - 4f), "", Font, Color.white, TextAnchor.MiddleLeft, 12);
            drinkLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            targetIcon = HudIcons.Icon(rt, "TargetIcon", new Vector2(0f, 0.5f), new Vector2(670f + icon, 0f), h - 2f, null);
            targetIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            targetLabel = HudIcons.Label(rt, "TargetLabel", new Vector2(0f, 0.5f), new Vector2(674f + icon + h, 0f), new Vector2(W - 680f - icon - h, h - 4f), "", Font, new Color(1f, 0.86f, 0.45f), TextAnchor.MiddleLeft, 12);
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

        void Update()
        {
            bool combat = DrinkRunHud.CombatVisible(game);
            group.alpha = combat ? 1f : 0f;
            if (combat && Time.unscaledTime >= nextRefresh) Refresh();
        }

        public void RefreshForTests() => Refresh();

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.5f;
            var drink = game != null && game.Player != null ? game.Player.AffinityDrink : null;
            label.text = Compose(drink);
            if (drinkIcon == null) return;
            var method = game?.ActiveRunCup?.recipe != null ? game.ActiveRunCup.recipe.coffeeMethod : (CoffeeBrewMethod)0;
            HudIcons.Set(drinkIcon, HudIcons.Drink(drink, method));
            var target = DrinkAffinity.TargetOf(drink);
            var trait = game?.Player != null ? game.Player.DrinkTrait : RunDrinkTraitKind.None;
            if (drink == null)
            {
                drinkLabel.text = "마신 음료 없음 →";
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
