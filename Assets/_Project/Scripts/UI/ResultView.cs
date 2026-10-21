using System.Collections.Generic;
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
        /// <summary>67 이번 판 기록(카드·조합·종료 사유). 기록 구성 실패 시 null — 결과 화면은 그대로 열린다.</summary>
        public RunRecord record;
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
        // 67 이번 판 빌드 상세와 기록 저장 상태
        Button buildButton;
        Text historyStatus;
        RunDetailView detail;
        DrinkAwardDetailView awardDetail;
        Button awardButton;
        int detailClosedFrame = -1;
        int shownHistoryState = -1;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트/QA용 — 제목과 전체 요약(로그에는 seed까지 포함한 6줄 요약).</summary>
        public string TitleText => title != null ? title.text : null;
        public string StatsText => last != null ? BuildStats(last, SpecialtyBalance()) : null;
        public bool SeedVisible => seedText != null && seedText.gameObject.activeSelf;
        public bool HudHidden { get; private set; }
        public RunDetailView Detail => detail;
        public Button BuildDetailButton => buildButton;
        public string HistoryStatusText => historyStatus != null ? historyStatus.text : "";

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
            UiFactory.SkinPanel(UiFactory.AddImage(small, new Color(0.08f, 0.065f, 0.08f, 1f)));
            view.statsSmall = UiFactory.AddText(UiFactory.Place("Stats", small, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1200f, 100f)), "", 26, TextAnchor.MiddleCenter, Muted);
            view.statsSmall.lineSpacing = 1.1f;

            // 69 음료: 보스 음료 재료 창고 반영 여부 + 준비 음료 보존/소모.
            // hudfix 2026-10-19: 긴 판(재료 묶음 수백 개)이 결과 화면 전체를 덮던 문제 — 통계 판 아래(-142)와 버튼 위(-226) 사이에는
            // 한 줄 요약 + "획득 내역" 버튼만 둔다. 재료별 전체 합계·연구 포인트·준비 음료는 스크롤 창(DrinkAwardDetailView)에서 본다.
            view.drinkStatus = UiFactory.AddText(UiFactory.Place("DrinkStatus", rootRt, new Vector2(0.5f, 0.5f), new Vector2(DrinkSummaryX, -184f),
                new Vector2(DrinkSummaryWidth, 44f)), "", DrinkSummaryFont, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f));
            view.drinkStatus.horizontalOverflow = HorizontalWrapMode.Overflow; // 한 줄 — 넘치면 요약 단계를 줄인다(글자 크기는 그대로)
            view.drinkStatus.verticalOverflow = VerticalWrapMode.Truncate;
            var awardRt = UiFactory.Place("DrinkAwardButton", rootRt, new Vector2(0.5f, 0.5f), new Vector2(DrinkAwardButtonX, -184f), new Vector2(280f, 64f));
            var awardBg = UiFactory.AddImage(awardRt, new Color(0.24f, 0.4f, 0.5f));
            view.awardButton = awardRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(view.awardButton);
            view.awardButton.targetGraphic = awardBg;
            UiFactory.ApplyHoverColors(view.awardButton);
            view.awardButton.onClick.AddListener(view.OpenAwardDetail);
            UiFactory.AddText(UiFactory.Stretch("Label", awardRt), "획득 내역", 30, TextAnchor.MiddleCenter, Color.white);

            view.restartButton = BuildButton(rootRt, "RestartButton", 0f, UiFactory.InputLabel("다시 시작", "R"), true, view.Restart);
            var shopButton = BuildButton(rootRt, "PermanentShopButton", -440f, "영구 강화", false, view.OpenShop);
            UiFactory.AddIcon(shopButton.transform, "specialty", new Vector2(0f, 0.5f), new Vector2(14f, 0f), 44f);
            BuildButton(rootRt, "TitleButton", 440f, "타이틀", false, () => view.game.GoToTitle());

            // 67 "이번 판 빌드" — 선택 카드·중첩·완성 조합·종료 사유는 별도 스크롤 창(큰 결과 화면에 압축하지 않는다).
            var buildRt = UiFactory.Place("BuildDetailButton", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -420f), new Vector2(420f, 76f));
            var buildBg = UiFactory.AddImage(buildRt, new Color(0.24f, 0.4f, 0.5f));
            view.buildButton = buildRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(view.buildButton);
            view.buildButton.targetGraphic = buildBg;
            UiFactory.ApplyHoverColors(view.buildButton);
            view.buildButton.onClick.AddListener(view.OpenDetail);
            UiFactory.AddText(UiFactory.Stretch("Label", buildRt), "이번 판 빌드", 30, TextAnchor.MiddleCenter, Color.white);
            view.historyStatus = UiFactory.AddText(UiFactory.Place("HistoryStatus", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -488f),
                new Vector2(1400f, 40f)), "", 24, TextAnchor.MiddleCenter, Muted);

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
            toggleRt.gameObject.SetActive(false); // Run seed remains available in diagnostic logs, not in player menus.

            view.shopView = PermanentSkillView.Create(canvas, view.ShowRoot);
            // 상세는 결과 화면의 마지막 자식 — 그리기 순서와 MobileBackRouter 탐색에서 "타이틀"보다 위에 있다.
            view.awardDetail = DrinkAwardDetailView.Create(rootRt, () => view.detailClosedFrame = Time.frameCount);
            view.detail = RunDetailView.Create(rootRt, () => view.detailClosedFrame = Time.frameCount);

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
            if (buildButton != null) buildButton.interactable = last.record != null;
            RefreshHistoryStatus();
            RefreshDrinkStatus();
            statsSmall.text=JobStudyTheme.Text(statsSmall.text,last.job);
            if (last.job == Job.TeaSommelier)
                statsSmall.text = $"처치  {last.kills}  ·  차 Lv {last.teaLevel}  ·  우림 연구 스킬 해금  {last.coffeeSkillsUnlocked}\n획득 재료  찻잎 {last.teaCollected}  ·  우림 지식 {last.beansCollected}";
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
            if ((detail != null && detail.IsOpen) || (awardDetail != null && awardDetail.IsOpen)) return;
            root.SetActive(false);
            shopView.Show();
        }

        /// <summary>67 "이번 판 빌드" 버튼(테스트도 같은 경로). 기록이 없으면 열지 않는다.</summary>
        public void OpenDetail()
        {
            if (!IsOpen || last == null || last.record == null || detail.IsOpen || (awardDetail != null && awardDetail.IsOpen)) return;
            detail.Show(last.record, buildButton.gameObject);
        }

        /// <summary>67 기록 저장 상태 — 저장 실패를 성공으로 보이지 않는다(재시도 성공 시 갱신).</summary>
        Text drinkStatus;
        int shownDrinkState = -1;
        public string DrinkStatusText => drinkStatus != null ? drinkStatus.text : "";

        public const float DrinkSummaryWidth = 1300f, DrinkSummaryX = -170f, DrinkAwardButtonX = 660f;
        public const int DrinkSummaryFont = 26;

        /// <summary>정산 상태 머리말(재료 창고 반영·실패·보존 포기).</summary>
        public static string DrinkStatusHead(DrinkSettlement s)
        {
            if (s == null) return "";
            if (s.kind == "PreservedAbandon")
                return s.discarded.Count > 0 ? "음료 재료: 보존 포기라 이번 판 보상은 정산하지 않았습니다" : "음료 재료: 보존 포기 — 이번 판 재료 보상 없음";
            if (s.granted.Count == 0) return "음료 재료: 이번 판 재료 보상 없음";
            return "음료 재료 획득 — " + (DrinkLab.IsDirty ? "창고 저장 실패, 자동으로 다시 시도 중" : DrinkLab.WriteBlocked ? "새 버전 창고 보호 중(저장 안 됨)" : "창고에 저장했습니다");
        }

        /// <summary>전체 정산 문구(로그·검증용, 여러 줄). 재료는 같은 재료끼리 합친 전체 합계 — 정산 기록(s.granted)은 그대로.</summary>
        public static string DrinkStatusLine(DrinkSettlement s)
        {
            if (s == null) return "";
            var lines = new List<string> { DrinkStatusHead(s) };
            if (s.kind == "PreservedAbandon") { if (s.discarded.Count > 0) lines.Add("정산 안 함: " + DrinkLab.Summarize(s.discarded)); }
            else if (s.granted.Count > 0)
                // 재료가 늘어도 결과 패널을 넘치지 않게 종류 수만 표시한다. 각 재료와 수량은 획득 내역 상세에서 확인.
                lines.Add($"재료 {DrinkLab.Totals(s.granted).Count}종 획득 (전체 수량은 획득 내역)");
            // 72 §3 연구 포인트(노트북 숙련·직원 채용용, 기존 스페셜티와 별개)
            if (s.researchPoints > 0) lines.Add($"연구 포인트 +{s.researchPoints} (현재 {DrinkLab.Research.researchPoints}, 연구 노트북에서 사용)");
            if (s.drink == "Preserved") lines.Add("준비 음료: 5마리 처치 전 포기라 보존했습니다(다음 출발 때 그대로 마심)");
            else if (s.drink == "Consumed") lines.Add("준비 음료: 이번 판에 사용되어 사라졌습니다");
            return string.Join("\n", lines);
        }

        /// <summary>결과 화면 한 줄 요약. detail 0 = 가장 자세히, 2 = 가장 짧게(전체는 "획득 내역" 창에 항상 있음).</summary>
        public static string DrinkSummaryLine(DrinkSettlement s, int detail = 0)
        {
            if (s == null) return "";
            string head;
            if (s.kind == "PreservedAbandon") head = s.discarded.Count > 0 ? "음료 재료: 보존 포기라 정산 안 함" : "음료 재료 보상 없음(보존 포기)";
            else if (s.granted.Count == 0) head = "음료 재료 보상 없음";
            else head = $"음료 재료 {DrinkLab.Totals(s.granted).Count}종 획득 · " + (DrinkLab.IsDirty ? "창고 저장 실패, 다시 시도 중" : DrinkLab.WriteBlocked ? "창고 보호 중(저장 안 됨)" : "창고에 저장했습니다");
            var parts = new List<string> { head };
            if (detail <= 1 && s.researchPoints > 0) parts.Add($"연구 포인트 +{s.researchPoints}");
            if (detail <= 0 && s.drink == "Preserved") parts.Add("준비 음료 보존");
            else if (detail <= 0 && s.drink == "Consumed") parts.Add("준비 음료 사용됨");
            return string.Join(" · ", parts);
        }

        public Text DrinkStatus => drinkStatus;
        public Button DrinkAwardButton => awardButton;
        public DrinkAwardDetailView AwardDetail => awardDetail;

        /// <summary>"획득 내역" 버튼(테스트도 같은 경로).</summary>
        public void OpenAwardDetail()
        {
            var s = game != null ? game.DrinkSettlement : null;
            if (!IsOpen || s == null || awardDetail.IsOpen || (detail != null && detail.IsOpen)) return;
            awardDetail.Show(s, awardButton.gameObject);
        }

        void RefreshDrinkStatus()
        {
            if (drinkStatus == null) return;
            shownDrinkState = DrinkLab.IsDirty ? 1 : 0;
            var settlement = game != null ? game.DrinkSettlement : null;
            // 한 줄에 들어가는 가장 자세한 요약(글자 크기는 그대로). 전체 합계는 "획득 내역" 창.
            for (int d = 0; d <= 2; d++)
            {
                drinkStatus.text = DrinkSummaryLine(settlement, d);
                if (drinkStatus.preferredWidth <= drinkStatus.rectTransform.rect.width) break;
            }
            if (awardButton != null) awardButton.gameObject.SetActive(settlement != null);
        }

        void RefreshHistoryStatus()
        {
            if (historyStatus == null) return;
            shownHistoryState = HistoryState();
            if (last == null || last.record == null) { historyStatus.text = "이번 판 기록을 만들지 못했습니다."; return; }
            historyStatus.text = RunHistory.IsWriteBlocked ? "새 버전 기록 파일 보존 중 — 이번 판 기록은 이 화면에서만 볼 수 있습니다."
                : RunHistory.IsDirty ? "기록 저장 실패 — 자동으로 다시 시도합니다. 이번 판 빌드는 지금 볼 수 있습니다."
                : "최근 기록에 저장했습니다 · 타이틀 > 최근 기록";
        }

        static int HistoryState() => (RunHistory.IsDirty ? 1 : 0) | (RunHistory.LastSaveFailed ? 2 : 0);

        void Update()
        {
            if (!IsOpen) return;
            if (shownHistoryState != HistoryState()) RefreshHistoryStatus();
            if (shownDrinkState != (DrinkLab.IsDirty ? 1 : 0)) RefreshDrinkStatus();
            var kb = Keyboard.current;
            // 상세가 열려 있거나 방금 닫힌 프레임이면 R·Esc가 결과 화면 버튼으로 관통하지 않는다.
            if (awardDetail != null && awardDetail.IsOpen)
            {
                if (!MobilePlatform.IsTouchUI && kb != null && kb.escapeKey.wasPressedThisFrame) awardDetail.Close();
                return;
            }
            if (detail != null && (detail.IsOpen || detailClosedFrame == Time.frameCount))
            {
                if (detail.IsOpen && !MobilePlatform.IsTouchUI && kb != null && kb.escapeKey.wasPressedThisFrame) detail.Close();
                return;
            }
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        void Restart()
        {
            if (!IsOpen || (detail != null && detail.IsOpen) || (awardDetail != null && awardDetail.IsOpen)) return;
            game.Restart();
        }
    }
}
