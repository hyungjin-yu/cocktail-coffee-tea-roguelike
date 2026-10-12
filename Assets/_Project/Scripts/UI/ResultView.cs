using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>08:106 결과 화면에 보여 줄 런 요약(GameManager가 런 종료 시 채운다).</summary>
    public class ResultSummary
    {
        public bool cleared;
        /// <summary>GameManager.EndRun 사유: "클리어" / "사망" / "시간 초과" / "포기".</summary>
        public string reason;
        public Job job;
        public WeaponKind weapon;
        public bool hybridUnlocked;
        public float runSeconds;
        public int kills;
        public int cocktailLevel;
        public int teaLevel;
        public int coffeeSkillsUnlocked;
        public int cocktailCollected;
        public int teaCollected;
        public int beansCollected;
        public int specialtyEarned;
        public int seed;
    }

    /// <summary>
    /// 런 종료 화면 (08:104~108). 버튼: 다시 영업 시작(R, 주 버튼) / 영구 강화 / 타이틀. 영구 강화에서 스페셜티를
    /// 쓰고 돌아오면 같은 결과 화면으로 복귀하고 잔액 표시를 갱신한다(08:107).
    /// 38(2026-10-09): 결과·시간·이번 스페셜티를 가장 크게, 직업/무기/하이브리드는 보조 줄, 처치·레벨·재료는 작은 통계,
    /// seed는 접힌 상세. 뒤 전투는 더 어둡게 덮고 전투 HUD는 숨긴다(다시 시작/타이틀은 GameManager를 새로 만들어 HUD 복원).
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        /// <summary>08:108 클리어 후 짧은 문구.</summary>
        public const string ClearLine = "오늘의 마지막 주문까지 마쳤습니다.";

        static readonly Color Gold = new Color(1f, 0.84f, 0.45f);
        static readonly Color Muted = new Color(0.80f, 0.74f, 0.68f);
        static readonly Color DarkInk = new Color(0.13f, 0.09f, 0.08f);

        GameManager game;
        GameObject root;
        Text title;
        Text subtitle;
        Text timeLabel, timeValue, specialtyValue, balanceValue, identity, statsSmall, seedText, seedToggleLabel;
        Button restartButton;
        PermanentSkillView shopView;
        ResultSummary last;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트/QA용 — 제목과 전체 요약(로그에는 seed까지 포함한 6줄 요약).</summary>
        public string TitleText => title != null ? title.text : null;
        public string StatsText => last != null ? BuildStats(last, SpecialtyBalance()) : null;
        public bool SeedVisible => seedText != null && seedText.gameObject.activeSelf;
        public bool HudHidden { get; private set; }

        public static ResultView Create(Transform canvas, GameManager game)
        {
            var rootRt = UiFactory.Stretch("Result", canvas);
            UiFactory.AddImage(rootRt, new Color(0.02f, 0.015f, 0.03f, 0.94f));

            var view = rootRt.gameObject.AddComponent<ResultView>();
            view.game = game;
            view.root = rootRt.gameObject;

            // 1920×1080 기준: 제목 → 문구 → 핵심 패널(시간·스페셜티) → 작은 통계 → 버튼 → 접힌 상세(seed).
            view.title = UiFactory.AddText(UiFactory.Place("Title", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 400f),
                new Vector2(1200f, 120f)), "", 88, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.6f));
            view.subtitle = UiFactory.AddText(UiFactory.Place("Subtitle", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 322f),
                new Vector2(1200f, 50f)), "", 32, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));

            var hero = UiFactory.Place("SummaryPanel", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1240f, 300f));
            UiFactory.AddImage(hero, new Color(0.1f, 0.08f, 0.1f, 1f));
            UiFactory.SkinPanel(UiFactory.AddImage(UiFactory.Stretch("Frame", hero), new Color(0.1f, 0.08f, 0.1f, 1f)));
            view.timeLabel = UiFactory.AddText(UiFactory.Place("TimeLabel", hero, new Vector2(0.5f, 1f), new Vector2(-300f, -22f),
                new Vector2(520f, 44f)), "", 28, TextAnchor.MiddleCenter, Muted);
            view.timeValue = UiFactory.AddText(UiFactory.Place("TimeValue", hero, new Vector2(0.5f, 1f), new Vector2(-300f, -66f),
                new Vector2(520f, 108f)), "", 72, TextAnchor.MiddleCenter, Color.white);
            UiFactory.AddText(UiFactory.Place("SpecialtyLabel", hero, new Vector2(0.5f, 1f), new Vector2(300f, -22f),
                new Vector2(520f, 44f)), "이번 스페셜티", 28, TextAnchor.MiddleCenter, Muted);
            view.specialtyValue = UiFactory.AddText(UiFactory.Place("SpecialtyValue", hero, new Vector2(0.5f, 1f), new Vector2(300f, -66f),
                new Vector2(520f, 108f)), "", 72, TextAnchor.MiddleCenter, Gold);
            UiFactory.AddIcon(hero, "specialty", new Vector2(0.5f, 1f), new Vector2(170f, -100f), 52f); // 04 §8 specialty
            view.balanceValue = UiFactory.AddText(UiFactory.Place("BalanceValue", hero, new Vector2(0.5f, 1f), new Vector2(300f, -178f),
                new Vector2(520f, 40f)), "", 26, TextAnchor.MiddleCenter, Muted);
            view.identity = UiFactory.AddText(UiFactory.Place("Identity", hero, new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(1160f, 48f)), "", 30, TextAnchor.MiddleCenter, Color.white);

            var small = UiFactory.Place("StatsPanel", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(1240f, 112f));
            UiFactory.AddImage(small, new Color(0.08f, 0.065f, 0.08f, 1f));
            view.statsSmall = UiFactory.AddText(UiFactory.Place("Stats", small, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1200f, 100f)), "", 26, TextAnchor.MiddleCenter, Muted);
            view.statsSmall.lineSpacing = 1.1f;

            view.restartButton = BuildButton(rootRt, "RestartButton", 0f, "다시 영업 시작  [R]", true, view.Restart);
            var shopButton = BuildButton(rootRt, "PermanentShopButton", -440f, "영구 강화", false, view.OpenShop);
            UiFactory.AddIcon(shopButton.transform, "specialty", new Vector2(0f, 0.5f), new Vector2(14f, 0f), 44f);
            BuildButton(rootRt, "TitleButton", 440f, "타이틀", false, () => view.game.GoToTitle());

            // 접힌 상세: seed(06:98)는 버튼으로 펼친다(키보드 Enter/마우스).
            var toggleRt = UiFactory.Place("SeedToggle", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -420f), new Vector2(300f, 52f));
            var toggleBg = UiFactory.AddImage(toggleRt, Color.white);
            var toggle = toggleRt.gameObject.AddComponent<Button>();
            toggle.targetGraphic = toggleBg;
            UiFactory.ApplyHoverColors(toggle);
            UiFactory.AddFocusSfx(toggle);
            toggle.onClick.AddListener(view.ToggleSeed);
            view.seedToggleLabel = UiFactory.AddText(UiFactory.Stretch("Label", toggleRt), "", 24, TextAnchor.MiddleCenter, Color.white);
            view.seedText = UiFactory.AddText(UiFactory.Place("Seed", rootRt, new Vector2(0.5f, 0.5f), new Vector2(370f, -420f),
                new Vector2(400f, 52f)), "", 24, TextAnchor.MiddleCenter, Muted);
            view.seedText.gameObject.SetActive(false);
            view.RefreshSeedToggle();

            view.shopView = PermanentSkillView.Create(canvas, view.ShowRoot);

            view.root.SetActive(false);
            return view;
        }

        static Button BuildButton(RectTransform parent, string name, float x, string label, bool primary,
            UnityEngine.Events.UnityAction onClick)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 0.5f), new Vector2(x, -282f),
                primary ? new Vector2(480f, 112f) : new Vector2(320f, 84f));
            var bg = UiFactory.AddImage(rt, primary ? new Color(0.9f, 0.35f, 0.75f) : new Color(0.32f, 0.3f, 0.34f));
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(onClick);
            if (primary)
            {
                // 주 버튼: 버튼 틀 안에 밝은 크림 판 + 어두운 큰 글자(보조 버튼과 밝기·크기로 구분).
                var plate = UiFactory.Stretch("PrimaryPlate", rt);
                plate.offsetMin = new Vector2(12f, 12f);
                plate.offsetMax = new Vector2(-12f, -12f);
                var plateImg = UiFactory.AddImage(plate, new Color(0.93f, 0.83f, 0.62f));
                UiFactory.Skin(plateImg, "ui_cafe_header", 0f);
                plateImg.raycastTarget = false;
            }
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, primary ? 38 : 30, TextAnchor.MiddleCenter, primary ? DarkInk : Color.white);
            return button;
        }

        public void Show(ResultSummary summary)
        {
            last = summary;
            ShowRoot();
        }

        /// <summary>08:105 제목: 영업 완료 / 쓰러졌습니다 / 영업 종료(20분 시간 초과) / 런을 포기했습니다.</summary>
        public static string TitleFor(bool cleared, string reason)
        {
            if (cleared) return "영업 완료";
            switch (reason)
            {
                case "포기": return "런을 포기했습니다";
                case "시간 초과": return "영업 종료";
                default: return "쓰러졌습니다";
            }
        }

        /// <summary>08:106 요약: 직업/시작무기/하이브리드, 생존시간, 처치, 칵테일·차 레벨, 커피 해금 수, 획득재료3종,
        /// 이번 스페셜티, 잔액(누적 획득 − 영구 강화 사용, 07:151), seed(06:98). 38 이후 화면은 이 정보를 위계별로 나눠 보이고
        /// 이 6줄 요약은 로그·검증용으로 유지한다.</summary>
        public static string BuildStats(ResultSummary s, int specialtyBalance)
        {
            int seconds = Mathf.FloorToInt(s.runSeconds);
            string hybrid = s.hybridUnlocked ? "하이브리드 티니 보우" : "하이브리드 없음";
            return $"{JobPreset.For(s.job).name}  ·  {WeaponPreset.For(s.weapon).name}  ·  {hybrid}\n" +
                   $"{(s.cleared ? "클리어 시간" : "생존 시간")}  {seconds / 60:00}:{seconds % 60:00}  ·  처치  {s.kills}\n" +
                   $"칵테일 Lv {s.cocktailLevel}  ·  차 Lv {s.teaLevel}  ·  커피 스킬 해금  {s.coffeeSkillsUnlocked}\n" +
                   $"획득 재료  칵테일 {s.cocktailCollected}  ·  찻잎 {s.teaCollected}  ·  원두 {s.beansCollected}\n" +
                   $"이번 스페셜티  +{s.specialtyEarned}  ·  잔액  {specialtyBalance}\n" +
                   $"seed  {s.seed}";
        }

        /// <summary>영구 강화 화면과 같은 식: 누적 획득 − 유효 노드 비용 합.</summary>
        static int SpecialtyBalance()
        {
            var save = PermanentProgress.Current;
            var tree = new PermanentSkillTree(PermanentSkillDatabase.LoadAll(), save.unlockedPermanentSkillIds);
            return save.specialtyPoints - tree.SpentPoints;
        }

        void RefreshText()
        {
            if (last == null) return;
            title.text = TitleFor(last.cleared, last.reason);
            title.color = last.cleared ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.5f, 0.6f);
            subtitle.text = last.cleared ? ClearLine : "";
            int seconds = Mathf.FloorToInt(last.runSeconds);
            timeLabel.text = last.cleared ? "클리어 시간" : "생존 시간";
            timeValue.text = $"{seconds / 60:00}:{seconds % 60:00}";
            specialtyValue.text = $"+{last.specialtyEarned}";
            balanceValue.text = $"잔액  {SpecialtyBalance()}";
            identity.text = $"{JobPreset.For(last.job).name}  ·  {WeaponPreset.For(last.weapon).name}  ·  " +
                            (last.hybridUnlocked ? "하이브리드 티니 보우" : "하이브리드 없음");
            statsSmall.text = $"처치  {last.kills}  ·  칵테일 Lv {last.cocktailLevel}  ·  차 Lv {last.teaLevel}  ·  커피 스킬 해금  {last.coffeeSkillsUnlocked}\n" +
                              $"획득 재료  칵테일 {last.cocktailCollected}  ·  찻잎 {last.teaCollected}  ·  원두 {last.beansCollected}";
            seedText.text = $"seed  {last.seed}";
            statsSmall.text=JobStudyTheme.Text(statsSmall.text,last.job);
        }

        public void ToggleSeed()
        {
            if (seedText == null) return;
            seedText.gameObject.SetActive(!seedText.gameObject.activeSelf);
            RefreshSeedToggle();
        }

        void RefreshSeedToggle()
        {
            if (seedToggleLabel != null) seedToggleLabel.text = SeedVisible ? "상세 정보 접기 ▲" : "상세 정보 (seed) ▼";
        }

        void ShowRoot()
        {
            RefreshText();
            root.SetActive(true);
            SetHudHidden(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        /// <summary>결과 동안 전투 HUD(체력·게이지·도전·배너)를 숨긴다. 다시 시작/타이틀은 이 GameManager를 파괴하고 새로 만들어
        /// 새 HUD가 보인다.</summary>
        void SetHudHidden(bool hidden)
        {
            var hud = game != null ? game.Hud : null;
            if (hud == null) return;
            hud.SetCombatHidden(hidden);
            HudHidden = hidden;
        }

        void OpenShop()
        {
            root.SetActive(false);
            shopView.Show();
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        void Restart()
        {
            if (!IsOpen) return;
            game.Restart();
        }
    }
}
