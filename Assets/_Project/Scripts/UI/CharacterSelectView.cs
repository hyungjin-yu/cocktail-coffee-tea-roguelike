using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 런 시작 전 직업 → 무기 2단계 선택 화면 (기획서 5장).
    /// GameManager 가 아직 없는 시점에 뜨는 화면이라 스스로 최상위 Canvas를 만든다.
    /// </summary>
    public class CharacterSelectView : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root;
        Action<Job, WeaponKind> onChosen;
        PermanentSkillView shopView;
        CardCodexView codexView;
        SettingsView settingsView;

        public Button BartenderButton { get; private set; }
        public Button TeaSommelierButton { get; private set; }
        /// <summary>2단계(무기 선택) 진입 후에만 채워진다. weapons[0]/[1] 순서는 JobPreset.weapons 와 같다.</summary>
        public Button WeaponButtonA { get; private set; }
        public Button WeaponButtonB { get; private set; }
        /// <summary>08:67 무기 선택 후 확정하는 "영업 시작" 버튼 / 직업 선택으로 돌아가는 "뒤로" 버튼(2단계에서만).</summary>
        public Button StartButton { get; private set; }
        public Button WeaponBackButton { get; private set; }
        /// <summary>2단계에서 현재 고른 무기(무기 카드를 누르면 바뀌고, 영업 시작으로 확정).</summary>
        public WeaponKind SelectedWeapon { get; private set; }

        // 08:67 이전 선택 기억(이번 실행 동안). 시작 기본값은 바텐더/레이피어.
        static Job lastJob = Job.Bartender;
        static WeaponKind lastWeapon = WeaponKind.Rapier;
        JobPreset currentJob;
        Image weaponBgA, weaponBgB;
        static readonly Color WeaponCardColor = new Color(0.16f, 0.13f, 0.2f, 1f);
        static readonly Color WeaponCardSelectedColor = new Color(0.36f, 0.26f, 0.16f, 1f);
        /// <summary>직업 선택 화면에서 여는 영구 강화 버튼 (테스트용으로도 참조).</summary>
        public Button PermanentShopButton { get; private set; }
        /// <summary>영구 강화 화면 (테스트용으로도 참조).</summary>
        public PermanentSkillView ShopView => shopView;
        /// <summary>직업 선택 화면에서 여는 카드 도감 버튼 (테스트용으로도 참조).</summary>
        public Button CardCodexButton { get; private set; }
        /// <summary>카드 도감 화면 (테스트용으로도 참조).</summary>
        public CardCodexView CodexView => codexView;
        /// <summary>직업 선택 화면에서 여는 설정 버튼 (테스트용으로도 참조).</summary>
        public Button SettingsButton { get; private set; }
        /// <summary>설정 화면 (테스트용으로도 참조).</summary>
        public SettingsView SettingsView => settingsView;

        public static CharacterSelectView Show(Action<Job, WeaponKind> onChosen)
        {
            var canvas = UiFactory.CreateCanvas(null, "CharacterSelect", 100);
            var view = canvas.gameObject.AddComponent<CharacterSelectView>();
            view.canvas = canvas;
            view.onChosen = onChosen;
            view.shopView = PermanentSkillView.Create(canvas.transform, view.ShowJobStep);
            view.codexView = CardCodexView.Create(canvas.transform, view.ShowJobStep);
            view.settingsView = SettingsView.Create(canvas.transform, () => view.root.gameObject.SetActive(true));
            view.ShowJobStep();
            return view;
        }

        void ClearRoot()
        {
            if (root != null) Destroy(root.gameObject);
            WeaponButtonA = null;
            WeaponButtonB = null;
            StandardModeButton = null; EndlessModeButton = null; SpawnTableButton = null; tableLabel = null; affinityLine = null;
            standardModeBg = null; endlessModeBg = null; modeHint = null; standardModeLabel = null; endlessModeLabel = null;
            StartButton = null;
            WeaponBackButton = null;
        }

        void ShowJobStep()
        {
            ClearRoot();
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(root);
            CenterBand.Attach(root); // 2026-10-12 폴드 펼침 등 세로가 긴 화면: 내용 세로 가운데

            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 90f));
            UiFactory.AddText(title, "직업을 선택하세요", 60, TextAnchor.MiddleCenter, Color.white);

            BartenderButton = BuildJobOption(-1, JobPreset.Bartender);
            TeaSommelierButton = BuildJobOption(1, JobPreset.TeaSommelier);
            PermanentShopButton = BuildPermanentShopButton();
            CardCodexButton = BuildCardCodexButton();
            SettingsButton = BuildSettingsButton();
            DrinkLabButton = BuildDrinkLabButton();
            LaptopButton = BuildLaptopButton();
            BuildSaveIssueBanner();
            // 2026-10-12: 진입 즉시 선택하면 바텐더 카드가 파란 강조로 고정돼 보여 첫 키보드 입력 때 드러낸다.
            var focus = lastJob == Job.TeaSommelier ? TeaSommelierButton : BartenderButton;
            UiInitialFocus.Offer(focus.gameObject);
        }

        /// <summary>G29 — 저장 파일이 손상돼 복구했거나 마지막 저장이 실패했으면 조용히 넘어가지 않고
        /// 알린다. 문제 없으면 아무것도 안 만든다.</summary>
        void BuildSaveIssueBanner()
        {
            string issue = PermanentProgress.LastLoadIssue;
            if (issue == null && !PermanentProgress.LastSaveFailed) return;

            string message = issue ?? "마지막 저장에 실패했습니다 — 다음 저장 때 다시 시도합니다.";
            var rt = UiFactory.Place("SaveIssueBanner", root, new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(900f, 50f));
            UiFactory.AddImage(rt, new Color(0.4f, 0.12f, 0.1f, 0.9f));
            UiFactory.AddText(UiFactory.Stretch("Label", rt), $"⚠ {message}", 22, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.7f));
        }

        Button BuildPermanentShopButton()
        {
            int specialty = PermanentProgress.Current.specialtyPoints;
            var rt = UiFactory.Place("PermanentShopButton", root, new Vector2(1f, 1f), new Vector2(-210f, -50f),
                new Vector2(380f, 80f));
            var bg = UiFactory.AddImage(rt, new Color(0.4f, 0.24f, 0.5f));
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                shopView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), $"영구 강화 (스페셜티 {specialty})", 26,
                TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        // ───── 69 음료 제조(출발 전) ─────
        /// <summary>직업/무기 선택 화면의 "음료 제조" 버튼(테스트용으로도 참조).</summary>
        public Button DrinkLabButton { get; private set; }
        public DrinkLabView DrinkLabScreen { get; private set; }

        public static string PreparedDrinkLine()
        {
            var p = CCT.DrinkLab.Prepared;
            if (p == null) return "준비 음료 없음 — 버프 없이 출발합니다 (음료 제조에서 만들 수 있음)";
            // 2026-11-10 상성 대상과 정수 수치(예: 커피 계열 추가 피해 +20%)를 출전 전에 보여 준다.
            string aff = DrinkAffinity.Describe(p.recipe.Kind);
            return $"준비 음료: {p.recipe.name} — {DrinkCalculator.Buff(p.recipe, p.specialtyId).Describe()}" +
                   (aff != null ? " · " + aff : "") + " (출발할 때 마심)";
        }

        // ───── 2026-11-10 모드 선택(출전 전 기존 무기 선택 화면, 기본 = 기존 모드) ─────
        public Button StandardModeButton { get; private set; }
        public Button EndlessModeButton { get; private set; }
        Image standardModeBg, endlessModeBg;
        Text modeHint, standardModeLabel, endlessModeLabel, tableLabel, affinityLine;
        public Button SpawnTableButton { get; private set; }
        Job currentJobForLine;
        static readonly Color ModeColor = new Color(0.16f, 0.13f, 0.2f, 1f);
        static readonly Color ModeSelectedColor = new Color(0.36f, 0.26f, 0.16f, 1f);

        /// <summary>무기 단계 세로 띠(기준 1920×1080). 카드 상단(위 기준)·높이, 모드 줄 아래 기준 y, 모드 안내 아래 기준 y. 시험이 겹침 없음을 단언한다.</summary>
        public const float WeaponCardTop = 248f, WeaponCardHeight = 510f, ModeRowBottom = 246f, ModeRowHeight = 52f, ModeHintBottom = 218f, StartButtonBottom = 130f, StartButtonHeight = 84f;
        /// <summary>모드 줄: 무기 카드 아래(하단 y=1080−758=322)·영업 시작 위(상단 y=214) 사이 띠에 두 버튼(246–298)과 보상 안내 한 줄(218–244).</summary>
        void BuildModeRow()
        {
            StandardModeButton = BuildModeOption(RunMode.Standard, -410f, out standardModeBg, out standardModeLabel);
            EndlessModeButton = BuildModeOption(RunMode.Endless, 0f, out endlessModeBg, out endlessModeLabel);
            // 2026-11-10 진상 보스 영업 계열 표 선택(기본 = 기존 직업 테마). 끝없는 모드에서는 자체 순환 표라 비활성.
            var tableRt = UiFactory.Place("SpawnTable", root, new Vector2(0.5f, 0f), new Vector2(410f, ModeRowBottom), new Vector2(380f, ModeRowHeight));
            SpawnTableButton = MakeButton(tableRt, UiFactory.AddImage(tableRt, ModeColor), ToggleSpawnTable);
            tableLabel = UiFactory.AddText(UiFactory.Stretch("Label", tableRt), "", 24, TextAnchor.MiddleCenter, Color.white);
            tableLabel.resizeTextForBestFit = true; tableLabel.resizeTextMinSize = 16; tableLabel.resizeTextMaxSize = 24;
            modeHint = UiFactory.AddText(UiFactory.Place("ModeHint", root, new Vector2(0.5f, 0f), new Vector2(0f, ModeHintBottom), new Vector2(1300f, 26f)),
                "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f));
            modeHint.resizeTextForBestFit = true; modeHint.resizeTextMinSize = 16; modeHint.resizeTextMaxSize = 22;
            SetMode(RunModeSelection.Current);
        }

        Button BuildModeOption(RunMode mode, float x, out Image bg, out Text label)
        {
            var rt = UiFactory.Place("Mode_" + mode, root, new Vector2(0.5f, 0f), new Vector2(x, ModeRowBottom), new Vector2(380f, ModeRowHeight));
            bg = UiFactory.AddImage(rt, ModeColor);
            var button = MakeButton(rt, bg, () => SetMode(mode));
            label = UiFactory.AddText(UiFactory.Stretch("Label", rt), RunModeSelection.Name(mode), 26, TextAnchor.MiddleCenter, Color.white);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 18; label.resizeTextMaxSize = 26;
            return button;
        }

        /// <summary>테스트·버튼 공용.</summary>
        public void SetMode(RunMode mode)
        {
            RunModeSelection.Current = mode;
            bool endless = mode == RunMode.Endless;
            // 색만으로 구분하지 않게 선택 버튼은 테두리 장식(SetChoice)도 함께 켠다.
            if (standardModeBg != null) UiFactory.SetChoice(standardModeBg, !endless, !endless ? ModeSelectedColor : ModeColor);
            if (endlessModeBg != null) UiFactory.SetChoice(endlessModeBg, endless, endless ? ModeSelectedColor : ModeColor);
            // 선택 표시는 글자(●/○)로도 — 색만으로 판별하지 않게.
            if (standardModeLabel != null) standardModeLabel.text = (endless ? "○ " : "● ") + RunModeSelection.Name(RunMode.Standard);
            if (endlessModeLabel != null) endlessModeLabel.text = (endless ? "● " : "○ ") + RunModeSelection.Name(RunMode.Endless);
            RefreshTableAndLines();
            if (modeHint != null)
                modeHint.text = endless
                    ? $"{RunModeSelection.Name(mode)} · 사망·포기까지 30초마다 웨이브 · {RunModeSelection.EndlessRewardLine}"
                    : $"{RunModeSelection.Name(mode)} · {RunModeSelection.StandardRewardLine}";
        }

        public string ModeHintText => modeHint != null ? modeHint.text : "";
        public string TableLabelText => tableLabel != null ? tableLabel.text : "";
        public string AffinityLineText => affinityLine != null ? affinityLine.text : "";

        /// <summary>테스트·버튼 공용: 진상 보스 영업 계열 표 전환(끝없는 모드에서는 무시).</summary>
        public void ToggleSpawnTable()
        {
            if (RunModeSelection.Current == RunMode.Endless) return;
            RunModeSelection.StandardTable = RunModeSelection.StandardTable == SpawnTable.JobTheme ? SpawnTable.AllFamilies : SpawnTable.JobTheme;
            RefreshTableAndLines();
        }

        void RefreshTableAndLines()
        {
            bool endless = RunModeSelection.Current == RunMode.Endless;
            if (SpawnTableButton != null) SpawnTableButton.interactable = !endless;
            if (tableLabel != null)
                tableLabel.text = endless ? "계열 표: 마스터 도전 순환" : "계열 표: " + RunModeSelection.TableName(RunModeSelection.StandardTable) + " ▸";
            if (affinityLine != null)
                affinityLine.text = DrinkAffinity.CycleLine + " · " + (endless ? EndlessFamilyLine() : StandardFamilyLine(currentJobForLine, RunModeSelection.StandardTable))
                                    + "\n" + (endless ? RunModeSelection.MasterLine : RunModeSelection.NoSwapLine);
        }

        public static string EndlessFamilyLine() =>
            $"끝없는 모드: 세 계열 모두 등장, 집중 계열 {EndlessRules.FocusWeightPercent}%(커피→차→칵테일, 3웨이브마다)";

        Button BuildDrinkLabButton()
        {
            var rt = UiFactory.Place("DrinkLabButton", root, new Vector2(1f, 0f), new Vector2(-40f, 130f), new Vector2(420f, 84f));
            var bg = UiFactory.AddImage(rt, new Color(0.5f, 0.28f, 0.16f));
            var weaponStep = StartButton != null;
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                DrinkLabScreen = DrinkLabView.Show(() =>
                {
                    DrinkLabScreen = null;
                    if (weaponStep && currentJob != null) ShowWeaponStep(currentJob); else ShowJobStep();
                });
            });
            var p = CCT.DrinkLab.Prepared;
            UiFactory.AddText(UiFactory.Place("Label", rt, new Vector2(0.5f, 1f), new Vector2(0f, -8f),
                    new Vector2(396f, 34f)), p != null ? "음료 제조 · 준비됨" : "음료 제조 (출발 전)", 25,
                TextAnchor.MiddleCenter, Color.white);
            UiFactory.AddText(UiFactory.Place("Hint", rt, new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                    new Vector2(396f, 26f)), p != null ? "출발 시 마시고 이번 판 동안 효과 적용" : "한 잔 준비하면 이번 판 버프 적용", 20,
                TextAnchor.MiddleCenter, new Color(1f, 0.91f, 0.76f));
            return button;
        }

        // ───── 72 연구 노트북(숙련 트리·직원 채용·기존 강화) ─────
        public Button LaptopButton { get; private set; }
        public ResearchLaptopView LaptopScreen { get; private set; }

        Button BuildLaptopButton()
        {
            // 아래 줄의 빈 자리(왼쪽 "뒤로"와 가운데 "영업 시작" 사이): 직업·무기 카드와 겹치지 않는다.
            var rt = UiFactory.Place("LaptopButton", root, new Vector2(0f, 0f), new Vector2(430f, 124f), new Vector2(330f, 92f));
            var bg = UiFactory.AddImage(rt, new Color(0.22f, 0.32f, 0.42f));
            var weaponStep = StartButton != null;
            void Back() { LaptopScreen = null; if (weaponStep && currentJob != null) ShowWeaponStep(currentJob); else ShowJobStep(); }
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                LaptopScreen = ResearchLaptopView.Show(Back, () => { LaptopScreen = null; shopView.Show(); }, currentJob != null ? currentJob.job : lastJob);
            });
            var r = CCT.DrinkLab.Research;
            UiFactory.AddText(UiFactory.Stretch("Label", rt), $"연구 노트북\nRP {r.researchPoints} · 장착 {r.equippedTraits.Count}/{DrinkResearch.Slots}", 24,
                TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        Button BuildSettingsButton()
        {
            var rt = UiFactory.Place("SettingsButton", root, new Vector2(0.5f, 0f), new Vector2(0f, 130f),
                new Vector2(300f, 70f));
            var bg = UiFactory.AddImage(rt, new Color(0.28f, 0.26f, 0.3f));
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                settingsView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), "설정", 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        Button BuildCardCodexButton()
        {
            var rt = UiFactory.Place("CardCodexButton", root, new Vector2(0f, 1f), new Vector2(210f, -50f),
                new Vector2(380f, 80f));
            var bg = UiFactory.AddImage(rt, new Color(0.24f, 0.4f, 0.5f));
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                codexView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), "카드 도감", 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        void ShowWeaponStep(JobPreset job)
        {
            ClearRoot();
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(root);
            CenterBand.Attach(root); // 2026-10-12 폴드 펼침 등 세로가 긴 화면: 내용 세로 가운데

            // 2026-11-11 세로 띠 재배치(기준 1080): 제목 96–176 / 안내 2줄 180–240 / 무기 카드 248–758 / 모드 줄 782–834(아래 기준 246–298) /
            // 모드 안내 836–862 / 영업 시작 866–950. 이전에는 카드 하단(800)이 모드 줄(782)과, 안내 2번째 줄이 카드 위(220)와, 모드 안내가 영업 시작과 겹쳤다.
            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(1200f, 80f));
            UiFactory.AddText(title, $"{job.name} — 무기를 선택하세요", 56, TextAnchor.MiddleCenter, Color.white);
            // 2026-11-10 순환 상성과 기본 스테이지 주요 등장 계열(정규 표). 제목과 카드 사이 빈 띠.
            currentJobForLine = job.job;
            affinityLine = UiFactory.AddText(UiFactory.Place("AffinityCycle", root, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1600f, 60f)),
                "", 20, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));
            affinityLine.verticalOverflow = VerticalWrapMode.Truncate; // 띠 밖(카드 위)으로 넘치지 않게 — 넘치면 best-fit이 글자를 줄인다
            affinityLine.resizeTextForBestFit = true; affinityLine.resizeTextMinSize = 14; affinityLine.resizeTextMaxSize = 20;

            currentJob = job;
            WeaponButtonA = BuildWeaponOption(-1, job.weapons[0], out weaponBgA);
            WeaponButtonB = BuildWeaponOption(1, job.weapons[1], out weaponBgB);

            var startRt = UiFactory.Place("StartButton", root, new Vector2(0.5f, 0f), new Vector2(0f, 130f),
                new Vector2(360f, 84f));
            StartButton = MakeButton(startRt, UiFactory.AddImage(startRt, new Color(0.55f, 0.32f, 0.14f)), ConfirmStart);
            UiFactory.AddText(UiFactory.Stretch("Label", startRt), "영업 시작", 34, TextAnchor.MiddleCenter, Color.white);

            DrinkLabButton = BuildDrinkLabButton();
            LaptopButton = BuildLaptopButton();
            BuildModeRow();
            UiFactory.AddText(UiFactory.Place("PreparedDrink", root, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(1300f, 50f)),
                PreparedDrinkLine(), 24, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f));

            var backRt = UiFactory.Place("BackButton", root, new Vector2(0f, 0f), new Vector2(170f, 130f),
                new Vector2(240f, 70f));
            WeaponBackButton = MakeButton(backRt, UiFactory.AddImage(backRt, new Color(0.28f, 0.26f, 0.3f)), ShowJobStep);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 26, TextAnchor.MiddleCenter, Color.white);

            // 같은 직업을 다시 고르면 지난 무기, 아니면 첫 무기(바텐더=레이피어)를 기본 선택.
            // 2026-10-12: 기본값은 논리 상태로만 두고 "선택됨" 테두리는 사용자가 무기를 직접 고른 뒤에만 그린다.
            SelectWeapon(job.weapons[1].kind == lastWeapon ? job.weapons[1] : job.weapons[0], false);
            var focus = SelectedWeapon == job.weapons[1].kind ? WeaponButtonB : WeaponButtonA;
            UiInitialFocus.Offer(focus.gameObject);
        }

        /// <summary>기본 스테이지 주요 등장 계열(기존 정규 표 그대로, 보스 포함 요약).</summary>
        public static string StandardFamilyLine(Job job, SpawnTable table)
        {
            if (table == SpawnTable.AllFamilies)
                return "기본 영업(세 계열 고르게): 같은 자리의 칵테일·차·커피 몬스터가 고르게 등장 · 보상·보스는 직업 테마와 같음";
            return StandardFamilyLine(job);
        }

        public static string StandardFamilyLine(Job job)
        {
            EnemyRoster.StandardFamilyPercents(job, EnemyRoster.CoffeeTrackUnlockTime, out int c, out int t, out int k);
            return job == Job.TeaSommelier
                ? $"기본 영업 주요 등장: 차 {t}% (보스 차 계열, 진상 손님 중립)"
                : $"기본 영업 주요 등장: 칵테일 {c}% · 차 {t}% · 커피 {k}%(2:30부터) · 중간보스 커피 계열";
        }

        void SelectWeapon(WeaponPreset weapon, bool showChoice)
        {
            SelectedWeapon = weapon.kind;
            bool a = currentJob.weapons[0].kind == weapon.kind;
            UiFactory.SetChoice(weaponBgA, showChoice && a, showChoice && a ? WeaponCardSelectedColor : WeaponCardColor);
            UiFactory.SetChoice(weaponBgB, showChoice && !a, showChoice && !a ? WeaponCardSelectedColor : WeaponCardColor);
        }

        /// <summary>테스트용 — 무기 카드에 "선택됨" 장식이 켜져 있는지.</summary>
        public bool WeaponChoiceShownForTests(bool first)
        {
            var bg = first ? weaponBgA : weaponBgB;
            var ring = bg != null ? bg.GetComponent<CafeUiFocus>() : null;
            return ring != null ? ring.ChosenForTests : bg != null && bg.color == WeaponCardSelectedColor;
        }

        void ConfirmStart()
        {
            lastJob = currentJob.job;
            lastWeapon = SelectedWeapon;
            var chosen = onChosen;
            Destroy(canvas.gameObject);
            chosen(currentJob.job, SelectedWeapon);
        }

        Button BuildJobOption(float side, JobPreset preset)
        {
            var rt = UiFactory.Place($"Option_{preset.job}", root, new Vector2(0.5f, 1f),
                new Vector2(side * 300f, -220f), new Vector2(520f, 640f));
            var bg = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));
            UiFactory.SkinCard(bg);

            var button = MakeButton(rt, bg, () => ShowWeaponStep(preset));

            var portrait = ArtLibrary.Get(preset.bodyArtId)?.Clip("idle")?.frames[0];
            if (portrait != null)
            {
                // 04 §8 "직업 선택 초상화는 각 idle_00을 잘라 재사용" — 몸체 알파 영역 위쪽(상반신)을 보여 준다.
                var portraitRt = UiFactory.Place("Portrait", rt, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(150f, 150f));
                var img = portraitRt.gameObject.AddComponent<Image>();
                img.sprite = portrait;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                var swatchRt = UiFactory.Place("Swatch", rt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                    new Vector2(90f, 90f));
                UiFactory.AddImage(swatchRt, preset.playerColor);
            }

            UiFactory.SkinHeader(UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -170f),
                new Vector2(460f, 70f)), preset.name, 46, TextAnchor.MiddleCenter, Color.white));
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -260f),
                new Vector2(440f, 260f)), preset.description, 26, MobilePlatform.IsTouchUI ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));
            // 47(2026-10-12): 직업 맵과 몬스터 출현 비중(숨은 편향이 아닌 공개 규칙). 특성 설명 아래 별도 줄.
            UiFactory.AddText(UiFactory.Place("Theme", rt, new Vector2(0.5f, 1f), new Vector2(0f, -545f),
                new Vector2(460f, 70f)), preset.ThemeLine, 24, TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.55f));

            return button;
        }

        Button BuildWeaponOption(float side, WeaponPreset weapon, out Image bg)
        {
            var rt = UiFactory.Place($"Weapon_{weapon.kind}", root, new Vector2(0.5f, 1f),
                new Vector2(side * 300f, -WeaponCardTop), new Vector2(520f, WeaponCardHeight));
            bg = UiFactory.AddImage(rt, WeaponCardColor);
            UiFactory.SkinCard(bg);

            // 08:67 — 무기 카드는 선택만 하고, 확정은 "영업 시작". 선택 후 포커스를 영업 시작으로 옮겨 Enter 두 번이면 시작.
            // 마우스로 고른 경우엔 영업 시작을 미리 강조하지 않는다(UiInitialFocus.MoveAfterChoice).
            var button = MakeButton(rt, bg, () =>
            {
                SelectWeapon(weapon, true);
                if (StartButton != null) UiInitialFocus.MoveAfterChoice(StartButton.gameObject);
            });

            var icon = string.IsNullOrEmpty(weapon.iconResourcePath)
                ? null : Resources.Load<Sprite>(weapon.iconResourcePath);
            if (icon != null)
            {
                var iconRt = UiFactory.Place("Icon", rt, new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                    new Vector2(140f, 140f));
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            UiFactory.SkinHeader(UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -190f),
                new Vector2(460f, 70f)), weapon.name, 46, TextAnchor.MiddleCenter, Color.white));
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -280f),
                new Vector2(440f, 200f)), weapon.description, 30, MobilePlatform.IsTouchUI ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));

            return button;
        }

        static Button MakeButton(RectTransform rt, Image bg, Action onClick)
        {
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
