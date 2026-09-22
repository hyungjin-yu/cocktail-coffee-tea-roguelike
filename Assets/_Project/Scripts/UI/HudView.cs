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
        FillBar beanBar;
        FillBar bossBar;
        Text info;
        Text suppressed;
        Text bannerTitle;
        Text bannerSub;
        Text schemeLabel;
        float bannerRemaining;

        public static HudView Create(Transform canvas, GameManager game)
        {
            var root = UiFactory.Stretch("Hud", canvas);
            var view = root.gameObject.AddComponent<HudView>();
            view.game = game;

            view.hpBar = new FillBar(root, "HpBar", new Vector2(0f, 1f), new Vector2(30f, -30f),
                new Vector2(420f, 30f), new Color(0.85f, 0.25f, 0.3f), 22);
            view.gaugeBar = new FillBar(root, "GaugeBar", new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(900f, 30f), new Color(0.9f, 0.35f, 0.75f), 22);
            view.beanBar = new FillBar(root, "BeanBar", new Vector2(0.5f, 1f), new Vector2(0f, -68f),
                new Vector2(600f, 24f), new Color(0.65f, 0.42f, 0.25f), 20);

            var infoRt = UiFactory.Place("Info", root, new Vector2(1f, 1f), new Vector2(-30f, -24f),
                new Vector2(320f, 90f));
            view.info = UiFactory.AddText(infoRt, "", 30, TextAnchor.UpperRight, Color.white);

            var suppressRt = UiFactory.Place("Suppressed", root, new Vector2(0f, 1f), new Vector2(30f, -70f),
                new Vector2(520f, 40f));
            view.suppressed = UiFactory.AddText(suppressRt, "", 26, TextAnchor.UpperLeft,
                new Color(1f, 0.4f, 0.45f));

            view.bossBar = new FillBar(root, "BossBar", new Vector2(0.5f, 0f), new Vector2(0f, 50f),
                new Vector2(1000f, 34f), new Color(0.8f, 0.15f, 0.2f), 24);
            view.bossBar.Root.gameObject.SetActive(false);

            var titleRt = UiFactory.Place("BannerTitle", root, new Vector2(0.5f, 1f), new Vector2(0f, -190f),
                new Vector2(1400f, 90f));
            view.bannerTitle = UiFactory.AddText(titleRt, "", 64, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
            var subRt = UiFactory.Place("BannerSub", root, new Vector2(0.5f, 1f), new Vector2(0f, -270f),
                new Vector2(1400f, 50f));
            view.bannerSub = UiFactory.AddText(subRt, "", 32, TextAnchor.MiddleCenter, Color.white);

            var schemeRt = UiFactory.Place("MovementScheme", root, new Vector2(0f, 0f), new Vector2(30f, 30f),
                new Vector2(260f, 60f));
            var schemeBg = UiFactory.AddImage(schemeRt, new Color(0f, 0f, 0f, 0.55f));
            var schemeButton = schemeRt.gameObject.AddComponent<Button>();
            schemeButton.targetGraphic = schemeBg;
            var schemeLabelRt = UiFactory.Stretch("Label", schemeRt);
            view.schemeLabel = UiFactory.AddText(schemeLabelRt, "", 24, TextAnchor.MiddleCenter, Color.white);
            schemeButton.onClick.AddListener(() =>
            {
                InputSettings.Toggle();
                view.RefreshSchemeLabel();
            });
            view.RefreshSchemeLabel();

            return view;
        }

        /// <summary>화면 상단에 큰 글씨 배너를 잠시 띄운다 (보스 등장·대사용).</summary>
        public void ShowBanner(string title, string subtitle, float seconds)
        {
            bannerTitle.text = title;
            bannerSub.text = subtitle;
            bannerRemaining = seconds;
        }

        void RefreshSchemeLabel()
        {
            schemeLabel.text = $"조작: {InputSettings.Label(InputSettings.Scheme)}\n(클릭해서 전환)";
        }

        void Update()
        {
            var player = game.Player;
            hpBar.Set(player.Hp / player.MaxHp);
            hpBar.Label.text = $"HP {Mathf.CeilToInt(player.Hp)} / {Mathf.CeilToInt(player.MaxHp)}";

            var gauge = game.CocktailGauge;
            gaugeBar.Set(gauge.Fill01);
            gaugeBar.Label.text = $"Lv {gauge.Level}   칵테일 재료 {gauge.Current} / {gauge.Threshold}";

            var beans = game.BeanGauge;
            beanBar.Set(beans.Fill01);
            beanBar.Label.text = $"원두 {beans.Current} / {beans.Threshold}  (스킬 {game.Skills.UnlockedCount}/{game.Skills.Nodes.Count})";

            int seconds = Mathf.FloorToInt(game.RunTime);
            info.text = $"{seconds / 60:00}:{seconds % 60:00}\n처치 {game.Kills}";

            var stats = game.Stats;
            suppressed.text = stats.BonusesSuppressed
                ? $"강화 효과 무효  {stats.SuppressedRemaining:0.0}초"
                : "";

            var boss = game.ActiveBoss;
            bool showBoss = boss != null && !boss.IsDead;
            if (bossBar.Root.gameObject.activeSelf != showBoss) bossBar.Root.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                bossBar.Set(boss.Hp / boss.MaxHp);
                bossBar.Label.text = $"{boss.Def.name}  {Mathf.CeilToInt(boss.Hp)} / {Mathf.CeilToInt(boss.MaxHp)}";
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
