using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CCT.Contest
{
    public enum View { Title, Select, Mission, Result }

    /// <summary>공모전판 진입점과 화면. 본편 타이틀·무한 모드·상점·스킨 메뉴를 거치지 않는다.</summary>
    public sealed class ContestApp : MonoBehaviour
    {
        public const string Title = "음료 마스터: 공공조달 체험편";
        public const string Version = "0.1.0-pps";

        public static ContestApp Instance { get; private set; }

        public ContestSave Save { get; private set; }
        public ContestSession Session { get; private set; }
        public int MissionIndex { get; private set; }
        public View View { get; private set; }
        public bool HelpOpen { get; private set; }
        public bool LastBadgeFirst { get; private set; }

        // 화면 선택 상태(로직 상태가 아닌 입력 중 값)
        public int OfferSel { get; private set; } = -1;
        public int Packs { get; private set; } = 1;
        public OfferVerdict LastVerdict { get; private set; }
        public Product? Recipe { get; private set; }
        public Temp? TempSel { get; private set; }
        public Vessel? VesselSel { get; private set; }
        public int Cups { get; private set; } = 4;
        public float PourProgress { get; private set; }
        public string Message { get; private set; }
        public bool VirtualHold { get; set; } // 캡처 빌드의 게임 내부 시연 입력 전용(OS 입력 아님)

        Canvas canvas;
        RectTransform root;
        RectTransform helpRoot;
        HoldButton pourButton;
        Image pourBar;
        Image pourStream;
        readonly List<RectTransform> previewLiquids = new List<RectTransform>();
        RectTransform frontPack;
        int shownPackSerial;
        Color messageColor = Kit.Cream;

#if PPS_CONTEST
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (!Application.isPlaying) return;
            if (SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return; // 테스트 러너는 직접 생성
            Create(ContestSave.OpenDefault());
        }
#endif

        public static ContestApp Create(ContestSave save)
        {
            if (Instance != null) return Instance;
            EnsureEventSystem();
            var go = new GameObject("PpsContestApp");
            DontDestroyOnLoad(go);
            var app = go.AddComponent<ContestApp>();
            app.Save = save;
            app.Build();
            return app;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (canvas != null) Destroy(canvas.gameObject);
            if (ownedCamera != null) Destroy(ownedCamera);
        }

        GameObject ownedCamera;

        void Build()
        {
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("PpsCamera");
                camGo.tag = "MainCamera";
                DontDestroyOnLoad(camGo);
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                ownedCamera = camGo;
            }
            cam.backgroundColor = Kit.Bg;
            cam.clearFlags = CameraClearFlags.SolidColor;
            canvas = UiFactory.CreateCanvas(null, "PpsContestCanvas", 200);
            DontDestroyOnLoad(canvas.gameObject);
            var bg = UiFactory.Stretch("Background", canvas.transform);
            UiFactory.AddImage(bg, Kit.Bg).raycastTarget = false;
            var wall = UiFactory.Stretch("Wall", bg);
            var wimg = wall.gameObject.AddComponent<Image>();
            wimg.sprite = Kit.Art("dl_craft_tea_wall");
            wimg.color = new Color(0.45f, 0.4f, 0.38f, 1f); // 정보 가독성을 위해 어둡게
            wimg.raycastTarget = false;
            root = UiFactory.Stretch("Root", canvas.transform);
            ShowTitle();
        }

        // ================= 화면 전환 =================
        public void ShowTitle() { View = View.Title; Session = null; Refresh(); }
        public void ShowSelect() { View = View.Select; Session = null; Refresh(); }

        public bool StartMission(int index)
        {
            if (index < 0 || index >= ContestMissions.All.Length || !Save.IsUnlocked(index)) return false;
            MissionIndex = index;
            Session = new ContestSession(ContestMissions.Get(index)); // 재시작은 항상 새 상태(이전 시도 오염 없음)
            OfferSel = -1; Packs = 1; LastVerdict = null; Recipe = null; TempSel = null; VesselSel = null;
            Cups = Mathf.Min(ContestMissions.MaxCupsPerBatch, Session.Mission.Quantity);
            PourProgress = 0f; Message = null; shownPackSerial = 0; LastBadgeFirst = false;
            View = View.Mission;
            Refresh();
            return true;
        }

        public void ToggleHelp()
        {
            HelpOpen = !HelpOpen;
            RefreshHelp();
        }

        void Say(string text, Color color) { Message = text; messageColor = color; }

        // ================= 플레이어 동작(버튼·키보드·시연 공통) =================
        public void CheckCondition(int i)
        {
            if (Session == null || !Session.CheckCondition(i)) return;
            Say("확인: " + Session.Conditions[i], Kit.Ok);
            Refresh();
        }

        public void CheckNextCondition()
        {
            if (Session == null) return;
            for (int i = 0; i < Session.Conditions.Count; i++)
                if (!Session.IsChecked(i)) { CheckCondition(i); return; }
            FinishRequest();
        }

        public void FinishRequest()
        {
            if (Session == null || !Session.FinishRequest()) return;
            Say("요청 조건을 기준으로 공급안 3개를 비교하세요.", Kit.Cream);
            Refresh();
        }

        public void SelectOffer(int i)
        {
            if (Session == null || Session.Stage != Stage.Supply) return;
            OfferSel = i;
            LastVerdict = null;
            var o = Session.Mission.Offers[i];
            Packs = Mathf.Max(1, (Session.Mission.Quantity + o.UnitsPerPack - 1) / o.UnitsPerPack);
            Message = null;
            Refresh();
        }

        public void ChangePacks(int delta)
        {
            if (Session == null) return;
            Packs = Mathf.Clamp(Packs + delta, 1, 9);
            Refresh();
        }

        public void PlaceOrder()
        {
            if (Session == null || OfferSel < 0) return;
            var offer = Session.Mission.Offers[OfferSel];
            var v = Session.Order(offer, Packs);
            LastVerdict = v;
            if (v.Ok) Say(offer.Supplier + " 주문 완료. 도착한 포장을 하나씩 확인하세요.", Kit.Ok);
            else Say("이 공급안은 요청 조건을 충족하지 않습니다. 사유를 확인하고 다시 선택하세요.", Kit.Ng);
            Refresh();
        }

        public void Reorder(int packs)
        {
            if (Session == null || Session.Chosen == null) return;
            var v = Session.Order(Session.Chosen, packs);
            Say(v.Ok ? "추가 확보 주문 완료. 입고를 확인하세요." : string.Join(" ", v.Reasons), v.Ok ? Kit.Ok : Kit.Ng);
            Refresh();
        }

        public void AcceptPack()
        {
            if (Session == null) return;
            var p = Session.FrontPack;
            if (!Session.AcceptPack()) return;
            Say("#" + p.Serial + " 입고 처리", Kit.Cream);
            AfterReceive();
        }

        public void RejectPack()
        {
            if (Session == null) return;
            var p = Session.FrontPack;
            if (!Session.RejectPack()) return;
            Say(p.Defect != PackDefect.None ? "#" + p.Serial + " " + ContestText.Name(p.Defect) + " — 반품했습니다." : "#" + p.Serial + " 정상 포장이었습니다. 다시 배송됩니다(+5분).",
                p.Defect != PackDefect.None ? Kit.Ok : Kit.Warn);
            AfterReceive();
        }

        void AfterReceive()
        {
            if (Session.Stage == Stage.Brew)
            {
                if (Recipe == null && Session.UnitsOf(Session.Mission.Product) > 0) Recipe = Session.Mission.Product;
                Say("입고 완료. 재료·온도·용기를 정하고 붓기 버튼을 누르고 있으세요.", Kit.Ok);
            }
            Refresh();
        }

        public void SetRecipe(Product p) { Recipe = p; Refresh(); }
        public void SetTemp(Temp t) { TempSel = t; Refresh(); }
        public void SetVessel(Vessel v) { VesselSel = v; Refresh(); }
        public void ChangeCups(int d) { Cups = Mathf.Clamp(Cups + d, 1, ContestMissions.MaxCupsPerBatch); Refresh(); }

        public string BrewBlocker()
        {
            if (Session == null) return "임무 없음";
            if (Recipe == null) return "재료를 고르세요.";
            if (TempSel == null) return "물 온도를 고르세요.";
            if (VesselSel == null) return "용기를 고르세요.";
            return Session.BrewError(Recipe.Value, TempSel.Value, VesselSel.Value, Cups);
        }

        public bool BrewNow()
        {
            if (BrewBlocker() != null) return false;
            int n = Cups;
            bool ok = Session.Brew(Recipe.Value, TempSel.Value, VesselSel.Value, n);
            PourProgress = 0f;
            if (ok) Say(ContestText.Name(Recipe.Value) + " " + n + "잔 제조. 다 만들었으면 검수를 요청하세요.", Kit.Ok);
            Refresh();
            return ok;
        }

        public void RequestInspection()
        {
            if (Session == null || !Session.CanInspect) return;
            var r = Session.Inspect();
            Say(r.Pass ? "검수 합격 — 납품할 수 있습니다." : "검수 불합격 — 보완 안내를 확인하세요.", r.Pass ? Kit.Ok : Kit.Ng);
            Refresh();
        }

        public void ExcludeNonconforming()
        {
            if (Session == null) return;
            int n = Session.ExcludeNonconforming();
            if (n > 0) Say("불일치 " + n + "잔을 제외했습니다. 요청 조건으로 다시 제조하세요.", Kit.Warn);
            Refresh();
        }

        public void BackToBrew()
        {
            if (Session == null || Session.Stage != Stage.Inspect) return;
            // 검수 결과는 유지하되 제조 화면으로(트레이가 바뀌면 재검수 필요)
            viewBrewFromInspect = true;
            Refresh();
        }

        bool viewBrewFromInspect;
        bool pourLatch; // 한 번 붓고 나면 손을 뗄 때까지 다음 배치를 시작하지 않는다

        public void Deliver()
        {
            if (Session == null) return;
            var receipt = Session.Deliver();
            if (receipt == null) return; // 중복 납품·미승인 납품 차단
            LastBadgeFirst = Save.Commit(receipt);
            View = View.Result;
            Refresh();
        }

        public void Retry() => StartMission(MissionIndex);

        public void NextMission()
        {
            if (MissionIndex + 1 < ContestMissions.All.Length) StartMission(MissionIndex + 1);
            else ShowSelect();
        }

        // ================= 입력 =================
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null) HandleKeys(kb);
            UpdatePour(kb);
            AnimateFrontPack();
        }

        void HandleKeys(Keyboard kb)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame) { ToggleHelp(); return; }
            if (HelpOpen) return;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            int digit = Digit(kb);
            switch (View)
            {
                case View.Title:
                    if (enter) ShowSelect();
                    break;
                case View.Select:
                    if (digit >= 1 && digit <= 3) StartMission(digit - 1);
                    else if (kb.backspaceKey.wasPressedThisFrame) ShowTitle();
                    break;
                case View.Result:
                    if (enter) NextMission();
                    else if (kb.rKey.wasPressedThisFrame) Retry();
                    else if (kb.backspaceKey.wasPressedThisFrame) ShowSelect();
                    break;
                case View.Mission:
                    MissionKeys(kb, enter, digit);
                    break;
            }
        }

        static int Digit(Keyboard kb)
        {
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < keys.Length; i++) if (kb[keys[i]].wasPressedThisFrame) return i + 1;
            return 0;
        }

        void MissionKeys(Keyboard kb, bool enter, int digit)
        {
            switch (Session.Stage)
            {
                case Stage.Request:
                    if (digit >= 1 && digit <= Session.Conditions.Count) CheckCondition(digit - 1);
                    else if (enter) CheckNextCondition();
                    break;
                case Stage.Supply:
                    if (digit >= 1 && digit <= 3) SelectOffer(digit - 1);
                    else if (kb.leftArrowKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame) ChangePacks(-1);
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.equalsKey.wasPressedThisFrame) ChangePacks(1);
                    else if (enter) PlaceOrder();
                    break;
                case Stage.Receive:
                    if (kb.aKey.wasPressedThisFrame) AcceptPack();
                    else if (kb.rKey.wasPressedThisFrame) RejectPack();
                    break;
                case Stage.Brew:
                case Stage.Inspect:
                    if (Session.Stage == Stage.Inspect && !viewBrewFromInspect)
                    {
                        if (kb.xKey.wasPressedThisFrame) ExcludeNonconforming();
                        else if (kb.bKey.wasPressedThisFrame) BackToBrew();
                        else if (enter) Deliver();
                        break;
                    }
                    var products = Session.StockProducts.ToList();
                    if (digit >= 1 && digit <= products.Count) SetRecipe(products[digit - 1]);
                    else if (kb.tKey.wasPressedThisFrame) SetTemp(TempSel == null ? Temp.T70 : (Temp)(((int)TempSel.Value + 1) % 3));
                    else if (kb.vKey.wasPressedThisFrame) SetVessel(VesselSel == null ? Vessel.TeaCup : (Vessel)(((int)VesselSel.Value + 1) % 3));
                    else if (kb.minusKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) ChangeCups(-1);
                    else if (kb.equalsKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) ChangeCups(1);
                    else if (kb.iKey.wasPressedThisFrame) { viewBrewFromInspect = false; RequestInspection(); }
                    break;
                case Stage.Failed:
                    if (kb.rKey.wasPressedThisFrame || enter) Retry();
                    break;
            }
        }

        void UpdatePour(Keyboard kb)
        {
            bool brewScreen = View == View.Mission && Session != null && !HelpOpen &&
                (Session.Stage == Stage.Brew || (Session.Stage == Stage.Inspect && viewBrewFromInspect));
            bool held = brewScreen && (VirtualHold || (pourButton != null && pourButton.Held) || (kb != null && kb.spaceKey.isPressed));
            if (!held) pourLatch = false;
            if (held && !pourLatch && BrewBlocker() == null)
            {
                PourProgress = Mathf.Min(1f, PourProgress + Time.deltaTime / 1.2f);
                if (PourProgress >= 1f) { viewBrewFromInspect = false; pourLatch = true; BrewNow(); return; }
            }
            else if (PourProgress > 0f) PourProgress = Mathf.Max(0f, PourProgress - Time.deltaTime * 2f);
            if (pourBar != null) pourBar.rectTransform.anchorMax = new Vector2(PourProgress, 1f);
            if (pourStream != null) pourStream.enabled = PourProgress > 0.02f && held;
            foreach (var l in previewLiquids) if (l != null) l.anchorMax = new Vector2(1f, PourProgress * 0.85f);
        }

        void AnimateFrontPack()
        {
            if (frontPack == null) return;
            var p = frontPack.anchoredPosition;
            if (p.x > 0f) frontPack.anchoredPosition = new Vector2(Mathf.Max(0f, p.x - Time.deltaTime * 1800f), p.y);
        }

        // ================= 그리기 =================
        public void Refresh()
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            root.DetachChildren();
            pourButton = null; pourBar = null; pourStream = null; frontPack = null;
            previewLiquids.Clear();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (View == View.Mission && Session != null && Session.Stage != Stage.Inspect) viewBrewFromInspect = false;
            switch (View)
            {
                case View.Title: DrawTitle(); break;
                case View.Select: DrawSelect(); break;
                case View.Mission: DrawMission(); break;
                case View.Result: DrawResult(); break;
            }
            RefreshHelp();
        }

        void Footer(string hint)
        {
            var bar = Kit.Panel("Footer", root, 32, 996, 1856, 60, false);
            bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            Kit.Label(bar, hint, 24, 10, 1500, 44, 24, Kit.Dim, TextAnchor.MiddleLeft);
            Kit.Label(bar, "H/Esc 도움말·출처", 1540, 10, 300, 44, 24, Kit.Accent, TextAnchor.MiddleRight);
        }

        void DrawTitle()
        {
            Kit.Sprite(root, "dl_teapot", 220, 420, 256, 224);
            Kit.Sprite(root, "dl_water_kettle", 1460, 400, 192, 224);
            Kit.Sprite(root, "dl_coffee_dripper", 1450, 640, 160, 96);
            Kit.Sprite(root, "dl_teacup", 300, 680, 160, 128);
            var steam = Kit.Sprite(root, "dl_steam_00", 340, 600, 64, 96);
            Kit.Frames(steam, "dl_steam_", 4, 6f);

            Kit.Label(root, "음료 마스터", 0, 150, 1920, 120, 96, Kit.Cream, TextAnchor.MiddleCenter, "TitleMain");
            Kit.Label(root, "공공조달 체험편", 0, 270, 1920, 70, 48, Kit.Accent, TextAnchor.MiddleCenter, "TitleSub");
            Kit.Label(root, "가상의 지역 행사에 필요한 커피·차를 요청 조건대로 준비해 납품하는 공급 협력자가 되어 보세요.",
                460, 360, 1000, 90, 28, Kit.Cream, TextAnchor.MiddleCenter, "Pitch");

            var b1 = Kit.Button(root, "StartButton", "임무 시작 (Enter)", 760, 480, 400, 84, ShowSelect, 34, true, new Color(0.3f, 0.55f, 0.5f));
            Kit.Button(root, "HelpButton", "도움말·정책 출처 (H)", 760, 584, 400, 72, ToggleHelp, 28);
            Kit.Button(root, "QuitButton", "종료", 760, 676, 400, 72, Application.Quit, 28, true, new Color(0.45f, 0.22f, 0.2f));

            var disc = Kit.Panel("Disclaimer", root, 360, 790, 1200, 170);
            Kit.Label(disc, "이 게임은 공공조달의 요청·준비·확인 흐름을 단순화한 창작 체험입니다.\n" +
                "조달청이 제작하거나 후원한 게임이 아니며, 등장하는 기관·행사·업체·가격·수치는 모두 가상입니다.\n" +
                "나라장터 접속·실제 입찰·온라인 접수 기능은 없습니다.", 32, 24, 1136, 124, 24, Kit.Cream, TextAnchor.MiddleCenter);
            Kit.Label(root, "버전 " + Version, 1640, 1040, 260, 32, 20, Kit.Dim, TextAnchor.MiddleRight, "Version");
            Footer("마우스 클릭 또는 키보드로 플레이합니다. 저장 위치: 공모전판 전용 폴더(본편과 분리)");
        }

        void DrawSelect()
        {
            Kit.Label(root, "임무 선택", 64, 48, 800, 80, 56, Kit.Cream, TextAnchor.MiddleLeft, "Heading");
            Kit.Label(root, "협력 배지 " + Save.Data.badges + " / " + ContestMissions.All.Length, 1300, 48, 556, 80, 32, Kit.Accent, TextAnchor.MiddleRight, "Badges");
            for (int i = 0; i < ContestMissions.All.Length; i++)
            {
                var m = ContestMissions.Get(i);
                int idx = i;
                bool unlocked = Save.IsUnlocked(i);
                var rec = Save.Data.missions.Find(r => r.id == m.Id);
                var card = Kit.Panel("Mission" + (i + 1), root, 64 + i * 608, 160, 576, 760);
                Kit.Label(card, "임무 " + (i + 1), 32, 28, 512, 48, 34, Kit.Accent);
                Kit.Label(card, m.Agency, 32, 84, 512, 44, 30, Kit.Cream);
                Kit.Label(card, m.EventName, 32, 130, 512, 44, 28, Kit.Dim);
                Kit.Sprite(card, Kit.Tin(m.Product), 72, 200, 160, 160);
                Kit.FilledCup(card, m.Vessel, m.Product, 0.8f, 300, 230, 5f);
                Kit.Label(card, ContestText.Name(m.Product) + " " + m.Quantity + "잔 · " + ContestText.Name(m.Temp) + " · " + ContestText.Name(m.Vessel) +
                    "\n납기 " + m.DeadlineMinutes + "분 · 예산 " + m.Budget + "코인", 32, 400, 512, 96, 26, Kit.Cream);
                Kit.Label(card, m.Purpose, 32, 500, 512, 90, 24, Kit.Dim);
                string status = !unlocked ? "잠김 — 이전 임무를 먼저 납품하세요" :
                    rec != null && rec.completed ? "완료 · 최고 신뢰도 " + rec.bestStars + "/3" : "도전 가능";
                Kit.Label(card, status, 32, 600, 512, 40, 24, !unlocked ? Kit.Ng : rec != null && rec.completed ? Kit.Ok : Kit.Warn);
                Kit.Button(card, "StartMission" + i, unlocked ? "시작 (" + (i + 1) + ")" : "잠김", 32, 660, 512, 72, () => StartMission(idx), 30, unlocked,
                    unlocked ? new Color(0.3f, 0.55f, 0.5f) : (Color?)null);
            }
            Kit.Button(root, "BackButton", "타이틀 (Backspace)", 64, 932, 360, 56, ShowTitle, 24);
            Footer("임무 1은 2~3분 안에 끝낼 수 있는 입문 임무입니다. 숫자 1~3으로 시작");
        }

        static readonly string[] StageNames = { "요청 확인", "공급안 선택", "입고 확인", "제조", "검수", "납품" };

        void DrawMission()
        {
            var s = Session;
            var m = s.Mission;
            // 상단 바
            var top = Kit.Panel("TopBar", root, 32, 16, 1856, 88);
            Kit.Label(top, "임무 " + (MissionIndex + 1) + " · " + m.Agency + " · " + m.EventName, 28, 18, 1100, 52, 30, Kit.Cream, TextAnchor.MiddleLeft, "MissionTitle");
            bool late = s.Clock > m.DeadlineMinutes;
            Kit.Label(top, "작업 시간 " + s.Clock + " / " + m.DeadlineMinutes + "분", 1130, 18, 380, 52, 28, late ? Kit.Ng : s.Clock + 15 > m.DeadlineMinutes ? Kit.Warn : Kit.Cream, TextAnchor.MiddleRight, "Clock");
            Kit.Label(top, "예산 " + s.Remaining + " / " + m.Budget + "코인", 1520, 18, 310, 52, 28, Kit.Cream, TextAnchor.MiddleRight, "Budget");

            // 단계 표시
            int cur = s.Stage switch { Stage.Request => 0, Stage.Supply => 1, Stage.Receive => 2, Stage.Brew => 3, Stage.Inspect => 4, _ => 5 };
            for (int i = 0; i < StageNames.Length; i++)
            {
                var chip = Kit.Rect("Stage" + i, root, 32 + i * 312, 116, 296, 52);
                var img = UiFactory.AddImage(chip, i == cur ? new Color(0.3f, 0.55f, 0.5f, 1f) : i < cur ? new Color(0.28f, 0.24f, 0.2f, 1f) : new Color(0.16f, 0.13f, 0.12f, 1f));
                img.raycastTarget = false;
                Kit.Label(chip, (i + 1) + ". " + StageNames[i] + (i < cur ? "  완료" : ""), 8, 4, 280, 44, 24, i == cur ? Kit.Cream : i < cur ? Kit.Ok : Kit.Dim, TextAnchor.MiddleCenter);
            }

            DrawRequestPanel(s);
            var main = Kit.Panel("Main", root, 592, 184, 1296, 800);
            if (s.Stage == Stage.Failed) { DrawFailed(main, s); return; }
            switch (s.Stage)
            {
                case Stage.Request: DrawRequest(main, s); break;
                case Stage.Supply: DrawSupply(main, s); break;
                case Stage.Receive: DrawReceive(main, s); break;
                case Stage.Brew: DrawBrew(main, s); break;
                case Stage.Inspect:
                    if (viewBrewFromInspect) DrawBrew(main, s); else DrawInspect(main, s);
                    break;
            }
        }

        void DrawRequestPanel(ContestSession s)
        {
            var p = Kit.PaperPanel("RequestSheet", root, 32, 184, 544, 800);
            Kit.Label(p, "조달 요청서 (가상)", 24, 20, 496, 48, 32, Kit.Ink, TextAnchor.MiddleLeft, "SheetTitle");
            Kit.Label(p, s.Mission.Agency + "\n" + s.Mission.EventName, 24, 72, 496, 76, 24, Kit.Ink);
            float y = 160;
            for (int i = 0; i < s.Conditions.Count; i++)
            {
                bool c = s.IsChecked(i);
                var row = Kit.Rect("Cond" + i, p, 16, y, 512, 58);
                UiFactory.AddImage(row, c ? new Color(0.78f, 0.88f, 0.72f, 1f) : new Color(0.88f, 0.83f, 0.74f, 1f)).raycastTarget = false;
                Kit.Label(row, s.Conditions[i], 12, 4, 380, 50, 24, Kit.Ink, TextAnchor.MiddleLeft);
                Kit.Label(row, c ? "확인함" : "미확인", 392, 4, 108, 50, 22, c ? new Color(0.15f, 0.45f, 0.2f) : new Color(0.6f, 0.25f, 0.2f), TextAnchor.MiddleRight);
                y += 66;
            }
            Kit.Label(p, "재고", 24, 700, 120, 36, 24, Kit.Ink);
            float x = 100;
            foreach (var prod in s.StockProducts)
            {
                Kit.Sprite(p, Kit.Tin(prod), x, 692, 48, 48);
                Kit.Label(p, s.UnitsOf(prod) + "단위", x + 52, 700, 100, 36, 22, Kit.Ink);
                x += 150;
            }
            if (!s.StockProducts.Any()) Kit.Label(p, "없음", 100, 700, 200, 36, 22, Kit.Ink);
            Kit.Label(p, "완성 " + s.Tray.Count + "잔", 24, 744, 496, 36, 24, Kit.Ink);
        }

        void DrawRequest(RectTransform main, ContestSession s)
        {
            Kit.Label(main, "1. 요청 확인", 40, 32, 1216, 56, 40, Kit.Accent);
            Kit.Label(main, s.Mission.Purpose + "\n요청서의 각 항목을 눌러 확인하세요. 확인한 조건이 공급안 선택·제조·검수의 기준이 됩니다.", 40, 100, 1216, 100, 26, Kit.Cream);
            float y = 210;
            for (int i = 0; i < s.Conditions.Count; i++)
            {
                int idx = i;
                bool c = s.IsChecked(i);
                Kit.Button(main, "CheckCond" + i, (i + 1) + ". " + s.Conditions[i] + (c ? "  — 확인함" : ""), 40 + (i % 2) * 616, y + (i / 2) * 84, 600, 72,
                    () => CheckCondition(idx), 26, !c, c ? new Color(0.25f, 0.4f, 0.3f) : (Color?)null);
            }
            bool all = s.CanFinishRequest;
            Kit.Button(main, "FinishRequest", all ? "확인 완료 → 공급안 비교 (Enter)" : "모든 항목을 확인하세요 (" + s.CheckedCount + "/" + s.Conditions.Count + ")",
                340, 680, 616, 80, FinishRequest, 30, all, all ? new Color(0.3f, 0.55f, 0.5f) : (Color?)null);
            DrawMessage(main, 40, 620);
            Footer("마우스로 항목 클릭 · 숫자키로 항목 확인 · Enter 다음 미확인 항목 확인/진행");
        }

        void DrawSupply(RectTransform main, ContestSession s)
        {
            Kit.Label(main, "2. 공급안 선택 — 같은 조건으로 3개 공급안 비교", 40, 24, 1216, 52, 36, Kit.Accent);
            for (int i = 0; i < 3; i++)
            {
                var o = s.Mission.Offers[i];
                int idx = i;
                bool sel = OfferSel == i;
                var card = Kit.Rect("Offer" + i, main, 40 + i * 412, 90, 396, 400);
                var img = UiFactory.AddImage(card, sel ? new Color(0.3f, 0.5f, 0.46f, 1f) : new Color(0.3f, 0.23f, 0.2f, 1f));
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => SelectOffer(idx));
                Kit.Label(card, (i + 1) + ". " + o.Supplier, 20, 16, 356, 44, 30, Kit.Cream);
                Kit.Sprite(card, Kit.Tin(o.Product), 20, 70, 96, 96);
                Kit.Label(card, ContestText.Name(o.Product) + "\n" + o.Note, 132, 76, 244, 90, 24, Kit.Cream);
                string lines =
                    "포장: " + o.UnitsPerPack + "단위/봉\n" +
                    "가격: " + o.PricePerPack + "코인/봉\n" +
                    "원산지 표시: " + (o.OriginLabeled ? "있음" : "없음") + "\n" +
                    "카페인: " + (o.ContainsCaffeine ? "들어 있음" : "없음");
                Kit.Label(card, lines, 20, 184, 356, 170, 26, Kit.Cream);
                if (sel) Kit.Label(card, "선택됨", 20, 356, 356, 36, 24, Kit.Accent, TextAnchor.MiddleRight);
            }
            if (OfferSel >= 0)
            {
                var o = s.Mission.Offers[OfferSel];
                var v = s.Evaluate(o, Packs);
                Kit.Label(main, "주문 수량", 40, 512, 180, 56, 28, Kit.Cream, TextAnchor.MiddleLeft);
                Kit.Button(main, "PacksMinus", "−", 220, 512, 64, 56, () => ChangePacks(-1), 32);
                Kit.Label(main, Packs + "봉", 292, 512, 100, 56, 30, Kit.Cream, TextAnchor.MiddleCenter, "Packs");
                Kit.Button(main, "PacksPlus", "+", 400, 512, 64, 56, () => ChangePacks(1), 32);
                Kit.Label(main, "= " + v.Units + "단위 (필요 " + s.Mission.Quantity + "단위) · 비용 " + v.Cost + "코인 (예산 " + s.Remaining + ")", 488, 512, 768, 56, 26, Kit.Cream, TextAnchor.MiddleLeft, "OrderCalc");
                Kit.Button(main, "PlaceOrder", "이 공급안으로 주문 (Enter)", 40, 584, 520, 72, PlaceOrder, 28, true, new Color(0.3f, 0.55f, 0.5f));
            }
            else Kit.Label(main, "공급안을 클릭하거나 숫자 1~3으로 고르세요.", 40, 520, 1216, 56, 28, Kit.Warn);

            if (LastVerdict != null && !LastVerdict.Ok)
            {
                var box = Kit.Rect("Verdict", main, 584, 576, 672, 136);
                UiFactory.AddImage(box, new Color(0.42f, 0.16f, 0.14f, 0.95f)).raycastTarget = false;
                Kit.Label(box, "반려 사유\n" + string.Join("\n", LastVerdict.Reasons) + "\n→ 다른 공급안이나 수량을 다시 고르세요.", 16, 8, 640, 120, 22, Kit.Cream, TextAnchor.UpperLeft, "VerdictText");
            }
            else DrawMessage(main, 584, 590);
            Kit.Label(main, "게임 규칙: 요청 조건·필요 수량·예산을 모두 충족해야 주문할 수 있습니다. 실제 공공조달의 업체 선정·계약 방식을 재현하지 않습니다.",
                40, 724, 1216, 64, 22, Kit.Dim, TextAnchor.UpperLeft, "GameRuleNote");
            Footer("1~3 공급안 선택 · ←/→ 수량 · Enter 주문");
        }

        void DrawReceive(RectTransform main, ContestSession s)
        {
            Kit.Label(main, "3. 입고 확인 — 도착한 포장을 하나씩 검사", 40, 24, 1216, 52, 36, Kit.Accent);
            Kit.Label(main, "주문: " + s.Chosen.Supplier + " " + ContestText.Name(s.Chosen.Product) + " (" + s.Chosen.UnitsPerPack + "단위/봉, 원산지 표시 " + (s.Chosen.OriginLabeled ? "있음" : "없음") + ")",
                40, 84, 1216, 40, 26, Kit.Cream);
            // 컨베이어(카운터 아트 재사용)
            var belt = Kit.Sprite(main, "dl_craft_tea_table", 40, 420, 1216, 40, "Belt");
            belt.preserveAspect = false;
            var p = s.FrontPack;
            if (p != null)
            {
                var gate = Kit.Rect("Gate", main, 360, 140, 576, 290);
                frontPack = Kit.Rect("FrontPack", gate, 0, 0, 576, 290);
                if (p.Serial != shownPackSerial) { frontPack.anchoredPosition = new Vector2(700f, 0f); shownPackSerial = p.Serial; }
                var tin = Kit.Sprite(frontPack, Kit.Tin(p.Product), 24, 40, 224, 224, "PackTin");
                if (p.Defect == PackDefect.Damaged) { tin.color = new Color(0.75f, 0.6f, 0.55f); tin.rectTransform.localRotation = Quaternion.Euler(0, 0, 14f); }
                var tag = Kit.PaperPanel("PackLabel", frontPack, 268, 30, 300, 240);
                Kit.Label(tag, "포장 #" + p.Serial + "\n품목: " + ContestText.Name(p.Product) + "\n수량: " + p.Units + "단위\n원산지 표시: " +
                    (p.Defect == PackDefect.NoOriginLabel || !s.Chosen.OriginLabeled ? "없음" : "있음") + "\n포장 상태: " + (p.Defect == PackDefect.Damaged ? "찢어짐" : "정상"),
                    16, 12, 268, 216, 26, Kit.Ink, TextAnchor.UpperLeft, "PackText");
            }
            // 대기 중인 포장 미리보기
            Kit.Label(main, "대기 " + Mathf.Max(0, s.InboundCount - 1) + "봉", 980, 300, 260, 36, 24, Kit.Dim, TextAnchor.MiddleRight);
            for (int i = 1; i < Mathf.Min(s.InboundCount, 5); i++)
                Kit.Sprite(main, Kit.Tin(s.Chosen.Product), 1240 - i * 64, 350, 56, 56, "Queued" + i).color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            Kit.Button(main, "Accept", "입고 (A)", 300, 490, 320, 88, AcceptPack, 34, p != null, new Color(0.3f, 0.55f, 0.5f));
            Kit.Button(main, "Reject", "반품 (R)", 676, 490, 320, 88, RejectPack, 34, p != null, new Color(0.55f, 0.25f, 0.22f));
            Kit.Label(main, "주문한 품목인지, 원산지 표시와 포장 상태가 요청 조건에 맞는지 보고 판단하세요.\n정상 포장을 반품하면 다시 배송되어 5분이 더 걸립니다.",
                40, 600, 1216, 80, 24, Kit.Dim);
            DrawMessage(main, 40, 690);
            Footer("A 입고 · R 반품 · 마우스로 버튼 클릭");
        }

        void DrawBrew(RectTransform main, ContestSession s)
        {
            var m = s.Mission;
            Kit.Label(main, "4. 제조 — 요청 조건대로 음료 만들기", 40, 24, 1216, 52, 36, Kit.Accent);
            // 재료
            Kit.Label(main, "재료", 40, 92, 200, 36, 26, Kit.Cream);
            var prods = s.StockProducts.ToList();
            for (int i = 0; i < prods.Count; i++)
            {
                var prod = prods[i];
                bool sel = Recipe == prod;
                var b = Kit.Button(main, "Recipe" + i, "", 40 + i * 176, 132, 160, 132, () => SetRecipe(prod), 22, true, sel ? new Color(0.3f, 0.55f, 0.5f) : (Color?)null);
                Kit.Sprite(b.transform, Kit.Tin(prod), 40, 8, 80, 80);
                Kit.Label(b.transform, (i + 1) + ". " + ContestText.Name(prod) + " " + s.UnitsOf(prod), 4, 92, 152, 36, 20, Kit.Cream, TextAnchor.MiddleCenter);
            }
            if (prods.Count == 0) Kit.Label(main, "재고 없음 — 추가 확보가 필요합니다.", 40, 140, 520, 40, 24, Kit.Ng);
            // 온도
            Kit.Label(main, "물 온도 (T)", 40, 288, 220, 36, 26, Kit.Cream);
            for (int i = 0; i < 3; i++)
            {
                var t = (Temp)i;
                Kit.Button(main, "Temp" + i, ContestText.Name(t), 40 + i * 132, 328, 120, 64, () => SetTemp(t), 28, true, TempSel == t ? new Color(0.3f, 0.55f, 0.5f) : (Color?)null);
            }
            // 용기
            Kit.Label(main, "용기 (V)", 40, 412, 220, 36, 26, Kit.Cream);
            for (int i = 0; i < 3; i++)
            {
                var v = (Vessel)i;
                var b = Kit.Button(main, "Vessel" + i, "", 40 + i * 132, 452, 120, 120, () => SetVessel(v), 22, true, VesselSel == v ? new Color(0.3f, 0.55f, 0.5f) : (Color?)null);
                Kit.Sprite(b.transform, Kit.Cup(v), 20, 8, 80, 68);
                Kit.Label(b.transform, ContestText.Name(v), 4, 80, 112, 36, 22, Kit.Cream, TextAnchor.MiddleCenter);
            }
            // 잔 수
            Kit.Label(main, "잔 수", 40, 592, 120, 56, 26, Kit.Cream, TextAnchor.MiddleLeft);
            Kit.Button(main, "CupsMinus", "−", 150, 592, 64, 56, () => ChangeCups(-1), 32);
            Kit.Label(main, Cups + "잔", 220, 592, 96, 56, 30, Kit.Cream, TextAnchor.MiddleCenter, "Cups");
            Kit.Button(main, "CupsPlus", "+", 322, 592, 64, 56, () => ChangeCups(1), 32);

            // 제조대(기존 주전자·다관·드리퍼 아트)
            var station = Kit.Rect("Station", main, 456, 92, 420, 500);
            UiFactory.AddImage(station, new Color(0f, 0f, 0f, 0.25f)).raycastTarget = false;
            bool coffee = Recipe == Product.Coffee;
            Kit.Sprite(station, "dl_water_kettle", 24, 30, 144, 168, "Kettle");
            Kit.Sprite(station, coffee ? "dl_coffee_dripper" : "dl_teapot", 196, 60, coffee ? 200 : 192, coffee ? 120 : 168, "Brewer");
            pourStream = Kit.Sprite(station, "dl_pour_stream_00", 270, 220, 36, 144, "Stream");
            Kit.Frames(pourStream, "dl_pour_stream_", 4, 12f);
            pourStream.enabled = false;
            if (VesselSel != null && Recipe != null)
            {
                for (int i = 0; i < Cups; i++)
                {
                    var cup = Kit.FilledCup(station, VesselSel.Value, Recipe.Value, 0.01f, 20 + i * 100, 380, 2.5f, "Preview" + i);
                    var liquid = cup.Find("LiquidMask/Liquid") as RectTransform;
                    if (liquid != null) { liquid.anchorMax = new Vector2(1f, 0f); previewLiquids.Add(liquid); }
                }
            }
            var blocker = BrewBlocker();
            var hold = Kit.Button(main, "PourButton", blocker == null ? "누르고 있기: 붓기 (Space)" : blocker, 456, 608, 420, 72, null, 26, blocker == null,
                blocker == null ? new Color(0.3f, 0.5f, 0.6f) : (Color?)null);
            pourButton = hold.gameObject.AddComponent<HoldButton>();
            var barBg = Kit.Rect("PourBarBg", main, 456, 688, 420, 16);
            UiFactory.AddImage(barBg, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = false;
            var fill = UiFactory.Stretch("PourBar", barBg);
            fill.anchorMax = new Vector2(PourProgress, 1f);
            pourBar = UiFactory.AddImage(fill, Kit.Accent);
            pourBar.raycastTarget = false;

            // 완성 트레이
            var trayRt = Kit.Rect("Tray", main, 900, 92, 356, 500);
            UiFactory.AddImage(trayRt, new Color(0f, 0f, 0f, 0.25f)).raycastTarget = false;
            Kit.Label(trayRt, "완성 " + s.Tray.Count + "잔", 16, 8, 324, 36, 26, Kit.Cream);
            for (int i = 0; i < s.Tray.Count && i < 12; i++)
            {
                var c = s.Tray[i];
                Kit.FilledCup(trayRt, c.Vessel, c.Product, 0.8f, 16 + (i % 4) * 84, 56 + (i / 4) * 92, 2f, "TrayCup" + i);
            }
            if (s.Tray.Count > 0)
            {
                var steam = Kit.Sprite(trayRt, "dl_steam_00", 40, 30, 32, 48, "TraySteam");
                Kit.Frames(steam, "dl_steam_", 4, 6f);
            }
            var batches = s.Tray.GroupBy(c => c.Batch).Select(g => "배치" + g.Key + ": " + ContestText.Name(g.First().Product) + " " + ContestText.Name(g.First().Temp) + " " + ContestText.Name(g.First().Vessel) + " ×" + g.Count());
            Kit.Label(trayRt, string.Join("\n", batches.Reverse().Take(3)), 16, 340, 324, 150, 20, Kit.Dim);
            Kit.Button(main, "Inspect", "검수 요청 (I)", 900, 608, 356, 72, () => { viewBrewFromInspect = false; RequestInspection(); }, 28, s.CanInspect, new Color(0.3f, 0.55f, 0.5f));

            // 추가 확보
            int usable = s.UnitsOf(m.Product);
            int conforming = s.Tray.Count(c => s.CupFailure(c) == null);
            if (s.Chosen != null && usable + conforming < m.Quantity)
                Kit.Button(main, "Reorder", "추가 확보: " + s.Chosen.Supplier + " 1봉 (" + s.Chosen.PricePerPack + "코인)", 456, 716, 420, 60, () => Reorder(1), 24, s.Remaining >= s.Chosen.PricePerPack);
            DrawMessage(main, 40, 724, 400);
            Footer("1~4 재료 · T 온도 · V 용기 · ←/→ 잔 수 · Space 길게 눌러 붓기 · I 검수 요청");
        }

        void DrawInspect(RectTransform main, ContestSession s)
        {
            var r = s.LastReport;
            var m = s.Mission;
            Kit.Label(main, "5. 검사·검수 — 요청 조건과 제조 결과 비교", 40, 24, 1216, 52, 36, Kit.Accent);
            Kit.Label(main, "요청: " + ContestText.Name(m.Product) + " · " + ContestText.Name(m.Temp) + " · " + ContestText.Name(m.Vessel) + " · " + m.Quantity + "잔" +
                (m.RequireOriginLabel ? " · 원산지 표시" : "") + (m.RequireCaffeineFree ? " · 카페인 없음" : ""), 40, 84, 1216, 40, 26, Kit.Cream, TextAnchor.UpperLeft, "InspectRequest");
            var list = Kit.Rect("CupList", main, 40, 136, 760, 520);
            UiFactory.AddImage(list, new Color(0f, 0f, 0f, 0.25f)).raycastTarget = false;
            var rows = new List<GameObject>();
            for (int i = 0; i < s.Tray.Count && i < 12; i++)
            {
                var c = s.Tray[i];
                var row = Kit.Rect("Row" + i, list, 8 + (i % 2) * 376, 8 + (i / 2) * 84, 368, 76);
                bool ok = !r.CupFailures.ContainsKey(c.Id);
                UiFactory.AddImage(row, ok ? new Color(0.2f, 0.35f, 0.22f, 0.9f) : new Color(0.42f, 0.16f, 0.14f, 0.9f)).raycastTarget = false;
                Kit.FilledCup(row, c.Vessel, c.Product, 0.8f, 8, 10, 1.6f, "Cup");
                Kit.Label(row, "#" + c.Id + " " + ContestText.Name(c.Product) + " " + ContestText.Name(c.Temp) + " " + ContestText.Name(c.Vessel) + "\n" + (ok ? "적합" : "불일치: " + r.CupFailures[c.Id]),
                    84, 4, 280, 68, 20, ok ? Kit.Cream : Kit.Cream, TextAnchor.MiddleLeft);
                rows.Add(row.gameObject);
            }
            if (s.Tray.Count > 12) Kit.Label(list, "외 " + (s.Tray.Count - 12) + "잔", 8, 492, 744, 28, 20, Kit.Dim);
            StartCoroutine(RevealRows(rows));

            var sum = Kit.Rect("Summary", main, 820, 136, 436, 520);
            UiFactory.AddImage(sum, r.Pass ? new Color(0.18f, 0.32f, 0.22f, 0.95f) : new Color(0.34f, 0.15f, 0.13f, 0.95f)).raycastTarget = false;
            Kit.Label(sum, r.Pass ? "검수 합격" : "검수 불합격 — 납품 승인 불가", 20, 16, 396, 48, 30, r.Pass ? Kit.Ok : Kit.Ng, TextAnchor.UpperLeft, "Verdict");
            string body = "적합 " + r.Conforming + " / 요청 " + m.Quantity + "잔" + (r.Surplus > 0 ? "\n초과 " + r.Surplus + "잔은 인수 대상이 아닙니다." : "") +
                (r.Issues.Count > 0 ? "\n\n문제\n· " + string.Join("\n· ", r.Issues) : "") +
                (r.Fixes.Count > 0 ? "\n\n보완 안내\n· " + string.Join("\n· ", r.Fixes) : "");
            Kit.Label(sum, body, 20, 72, 396, 436, 22, Kit.Cream, TextAnchor.UpperLeft, "InspectBody");

            if (r.Pass)
                Kit.Button(main, "Deliver", "납품하기 (Enter)", 820, 680, 436, 84, Deliver, 32, s.CanDeliver, new Color(0.3f, 0.55f, 0.5f));
            else
            {
                Kit.Button(main, "Exclude", "불일치 " + r.Nonconforming + "잔 제외 (X)", 40, 680, 380, 80, ExcludeNonconforming, 28, r.Nonconforming > 0, new Color(0.55f, 0.35f, 0.2f));
                Kit.Button(main, "BackBrew", "제조로 돌아가기 (B)", 440, 680, 360, 80, BackToBrew, 28);
                Kit.Button(main, "DeliverBlocked", "납품 불가", 820, 680, 436, 80, null, 28, false);
            }
            Footer(r.Pass ? "Enter 납품" : "X 불일치 제외 · B 제조로 · 보완 후 다시 검수 요청");
        }

        IEnumerator RevealRows(List<GameObject> rows)
        {
            foreach (var r in rows) if (r != null) r.SetActive(false);
            foreach (var r in rows)
            {
                if (r == null) yield break;
                r.SetActive(true);
                yield return new WaitForSeconds(0.08f);
            }
        }

        void DrawFailed(RectTransform main, ContestSession s)
        {
            Kit.Label(main, "임무 중단", 40, 32, 1216, 60, 44, Kit.Ng);
            Kit.Label(main, s.FailReason + "\n같은 임무를 처음부터 다시 시작할 수 있습니다(이전 시도의 재고·제조 결과는 남지 않습니다).", 40, 110, 1216, 100, 28, Kit.Cream);
            Kit.Label(main, string.Join("\n", s.History.Skip(Mathf.Max(0, s.History.Count - 12))), 40, 230, 1216, 420, 22, Kit.Dim, TextAnchor.UpperLeft, "History");
            Kit.Button(main, "Retry", "처음부터 다시 (R)", 40, 680, 420, 80, Retry, 30, true, new Color(0.3f, 0.55f, 0.5f));
            Kit.Button(main, "ToSelect", "임무 목록", 480, 680, 300, 80, ShowSelect, 28);
            Footer("R 또는 Enter 다시 시작");
        }

        void DrawResult()
        {
            var s = Session;
            var rc = s.Receipt;
            var m = s.Mission;
            Kit.Label(root, "납품 완료 — 인수 처리되었습니다", 64, 32, 1300, 72, 48, Kit.Ok, TextAnchor.MiddleLeft, "Heading");
            Kit.Label(root, "임무 " + (MissionIndex + 1) + " · " + m.Agency + " · " + m.EventName, 64, 104, 1300, 44, 28, Kit.Cream, TextAnchor.MiddleLeft);
            var card = Kit.Panel("Receipt", root, 64, 168, 600, 560);
            Kit.Label(card, "인수 기록", 32, 24, 536, 48, 32, Kit.Accent);
            Kit.Label(card,
                "인수 수량: " + rc.Accepted + " / " + m.Quantity + "잔\n" +
                (rc.Surplus > 0 ? "인수 제외(초과): " + rc.Surplus + "잔\n" : "") +
                "작업 시간: " + rc.Minutes + " / 납기 " + m.DeadlineMinutes + "분\n" +
                "사용 예산: " + rc.Spent + " / " + m.Budget + "코인\n" +
                "보완 횟수: " + rc.Reworks + "회\n" +
                "신뢰도: " + rc.Stars + " / 3\n" +
                (LastBadgeFirst ? "협력 배지를 받았습니다." : "배지는 이미 받았습니다(중복 지급 없음)."), 32, 84, 536, 330, 28, Kit.Cream);
            Kit.FilledCup(card, m.Vessel, m.Product, 0.8f, 400, 400, 4f);
            var steam = Kit.Sprite(card, "dl_steam_00", 440, 330, 48, 72);
            Kit.Frames(steam, "dl_steam_", 4, 6f);

            var hist = Kit.Panel("HistoryPanel", root, 688, 168, 1168, 560);
            Kit.Label(hist, "요청 → 공급 → 검수 → 인수 기록", 32, 24, 1104, 48, 32, Kit.Accent);
            Kit.Label(hist, string.Join("\n", s.History.Skip(Mathf.Max(0, s.History.Count - 15))), 32, 80, 1104, 460, 20, Kit.Cream, TextAnchor.UpperLeft, "History");

            var learn = Kit.Panel("Lessons", root, 64, 744, 1792, 168);
            Kit.Label(learn, "이번 임무에서 확인한 것", 32, 16, 1728, 40, 28, Kit.Accent);
            Kit.Label(learn, "· " + string.Join("\n· ", m.Lessons), 32, 60, 1728, 100, 22, Kit.Cream);

            bool last = MissionIndex + 1 >= ContestMissions.All.Length;
            Kit.Button(root, "Next", last ? "임무 목록 (Enter)" : "다음 임무 (Enter)", 64, 924, 400, 64, NextMission, 28, true, new Color(0.3f, 0.55f, 0.5f));
            Kit.Button(root, "Retry", "다시 하기 (R)", 488, 924, 300, 64, Retry, 26);
            Kit.Button(root, "ToSelect", "임무 목록 (Backspace)", 812, 924, 360, 64, ShowSelect, 26);
        }

        void DrawMessage(RectTransform parent, float x, float y, float w = 1216)
        {
            if (string.IsNullOrEmpty(Message)) return;
            Kit.Label(parent, Message, x, y, w, 56, 24, messageColor, TextAnchor.UpperLeft, "Message");
        }

        // ================= 도움말·출처 =================
        void RefreshHelp()
        {
            if (helpRoot != null) { Destroy(helpRoot.gameObject); helpRoot = null; }
            if (!HelpOpen || canvas == null) return;
            helpRoot = UiFactory.Stretch("Help", canvas.transform);
            UiFactory.AddImage(helpRoot, new Color(0f, 0f, 0f, 0.82f));
            var p = Kit.Panel("HelpPanel", helpRoot, 160, 60, 1600, 960);
            Kit.Label(p, "도움말 · 정책 해설 · 출처", 48, 32, 1504, 60, 40, Kit.Accent);
            Kit.Label(p, HelpText, 48, 104, 1504, 740, 23, Kit.Cream, TextAnchor.UpperLeft, "HelpBody");
            Kit.Button(p, "CloseHelp", "닫기 (Esc)", 600, 860, 400, 72, ToggleHelp, 28, true, new Color(0.3f, 0.55f, 0.5f));
        }

        public const string HelpText =
            "이 게임은 공공조달의 요청·준비·확인 흐름을 단순화한 창작 체험입니다. 조달청이 제작하거나 후원한 게임이 아니며, 기관·행사·업체·가격·수치는 모두 가상입니다. 나라장터 접속·실제 입찰·온라인 접수 기능은 없습니다.\n\n" +
            "참고한 공식 절차 — 조달청 누리집 「조달업무 > 물품구매 > 총액계약 > 업무처리절차」\n" +
            "· 공식 흐름도 단계: 수요발생 → 조달요청 → 조달요청접수 → 규격검토 → 구매결의 → 입찰공고 → 예정가격결정 → 입찰 → 낙찰자선정 및 계약체결 → 계약체결 통보 → 착수계 제출 → 용역이행 → 검사/검수요청 → 검사검수 및 인수 → 영수증발급 → 대금청구 및 영수\n" +
            "· 조달요청: 수요기관이 품명, 규격(시방서), 납기, 특수조건 및 추산가격, 배정예산 등을 기재하여 나라장터(국가종합전자조달) 시스템으로 요청한다고 안내합니다.\n\n" +
            "게임 단계와의 대응(단순화)\n" +
            "· 요청 확인 ↔ 수요발생·조달요청: 품명·수량·규격·납기·예산 확인\n" +
            "· 공급안 선택 ↔ 규격검토~계약체결을 대신하는 게임 규칙: 요청 조건·필요 수량·예산을 모두 충족한 공급안만 주문 가능. 실제 입찰·낙찰·계약 방식은 재현하지 않습니다.\n" +
            "· 입고 확인·제조 ↔ 계약 조건에 맞는 공급(이행) 준비\n" +
            "· 검수 ↔ 검사/검수요청, 검사검수 및 인수: 조건과 다른 물품은 인수되지 않고 보완합니다.\n" +
            "· 영수증발급·대금청구 단계와 법령상 기한·수치는 다루지 않습니다.\n\n" +
            "출처: https://www.pps.go.kr/kor/content.do?key=00179 (2026-11-10 확인)\n" +
            "조작: 마우스 클릭 또는 화면에 표시된 키. 공식 로고는 사용하지 않았습니다. 기존 「음료 마스터」 픽셀 아트를 재사용했습니다.";
    }
}
