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
            view.Refresh();
            return view;
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
            label.text = Compose(game != null && game.Player != null ? game.Player.AffinityDrink : null);
        }
    }
}
