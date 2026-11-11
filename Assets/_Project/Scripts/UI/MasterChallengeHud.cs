using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-11 마스터 도전 HUD(1920×1080 기준).
    /// - 우상단 '음료 만들기' 단일 버튼(드링크 칩 아래 고정). 재고 변화 때 문구만 갱신하고 창을 강제로 열지 않는다. [B]로도 연다.
    /// - 수집 5웨이브 완료 후 상단 중앙 선택 패널(재료 모으기 반복 / 박람회 도전) — 비모달(남은 드롭 줍기·제조 가능), 중복 입력은 State가 거부.
    /// - 제조 창(모달, 전투 일시정지): 왼쪽 레시피(저장 + 예시) 9줄 페이지, 오른쪽 보관 잔(마시기/제출).
    /// </summary>
    public class MasterChallengeHud : MonoBehaviour
    {
        GameManager game;
        RectTransform root;
        Button brewButton;
        Text brewLabel;
        GameObject choiceRoot;
        Text choiceText;
        Button repeatButton, fairButton;
        GameObject brewPanel;
        Text brewTitle, brewFooter, brewMessage, pageLabel;
        readonly List<GameObject> recipeRows = new List<GameObject>();
        readonly List<GameObject> cupRows = new List<GameObject>();
        RectTransform recipeArea, cupArea;
        int recipePage, cupPage;
        float refreshTimer;
        RectTransform fxSlot;
        Image submitCup;
        public RectTransform FxSlot => fxSlot;
        public bool SubmitCupShown => submitCup != null && submitCup.enabled;
        public const float FxSlotPx = 112f;
        /// <summary>2026-11-11 가독성 국소 개선: 품질 효과 그림 크기(기준 1920×1080 단위). 슬롯(112, 컵 배치 기준)은 그대로 두고 효과만 1.5배로 그린다.
        /// 승인 PNG 내용은 128 캔버스의 약 70%라 112면 720p에서 ≈45px — 168이면 ≈65px. 중심이 슬롯 중심이라 보이는 내용은 패널 위 가장자리 안·첫 잔 줄(−118) 위에 머문다.</summary>
        public const float FxArtPx = 168f;
        public const int RowsPerPage = 9;
        /// <summary>2026-11-12 보관 잔은 한 쪽 5줄 — 아래(−428…−676)는 '현재 잔 특성' 상자.</summary>
        public const int CupRowsPerPage = 5;
        public const float BrewButtonTop = 304f, BrewButtonW = 330f, BrewButtonH = 60f;

        public bool BrewOpen => brewPanel != null && brewPanel.activeSelf;
        public bool ChoiceVisible => choiceRoot != null && choiceRoot.activeSelf;
        public string BrewButtonText => brewLabel != null ? brewLabel.text : "";
        public string ChoiceText => choiceText != null ? choiceText.text : "";
        public string LastMessage => brewMessage != null ? brewMessage.text : "";

        public static MasterChallengeHud Create(Transform canvas, GameManager game)
        {
            var go = new GameObject("MasterChallengeHud", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hud = go.AddComponent<MasterChallengeHud>();
            hud.game = game;
            hud.root = rt;
            hud.Build();
            if (game.Master != null) game.Master.Changed += hud.Refresh;
            hud.Refresh();
            return hud;
        }

        static Button MakeButton(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, int font, Color bg, out Text text)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            var img = UiFactory.AddImage(rt, bg);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            UiFactory.ApplyHoverColors(b);
            UiFactory.AddFocusSfx(b);
            var lrt = UiFactory.Stretch("Label", rt);
            lrt.offsetMin = new Vector2(8f, 2f); lrt.offsetMax = new Vector2(-8f, -2f);
            text = UiFactory.AddText(lrt, label, font, TextAnchor.MiddleCenter, Color.white);
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = font;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return b;
        }

        void Build()
        {
            brewButton = MakeButton(root, "BrewButton", new Vector2(1f, 1f), new Vector2(-DrinkRunHud.ChipRight, -BrewButtonTop),
                new Vector2(BrewButtonW, BrewButtonH), "음료 만들기", 26, new Color(0.32f, 0.18f, 0.1f, 0.92f), out brewLabel);
            brewButton.onClick.AddListener(() => game?.OpenMasterBrew());

            var crt = UiFactory.Place("MasterChoice", root, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(880f, 176f));
            UiFactory.AddImage(crt, new Color(0.06f, 0.05f, 0.08f, 0.9f)).raycastTarget = true;
            choiceRoot = crt.gameObject;
            var trt = UiFactory.Place("Prompt", crt, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(840f, 74f));
            choiceText = UiFactory.AddText(trt, "", 24, TextAnchor.MiddleCenter, Color.white);
            choiceText.resizeTextForBestFit = true; choiceText.resizeTextMinSize = 16; choiceText.resizeTextMaxSize = 24;
            choiceText.verticalOverflow = VerticalWrapMode.Truncate;
            repeatButton = MakeButton(crt, "Repeat", new Vector2(0.5f, 0f), new Vector2(-215f, 14f), new Vector2(400f, 70f),
                "재료 모으기 반복 (1~5)", 26, new Color(0.18f, 0.32f, 0.2f, 0.95f), out _);
            repeatButton.onClick.AddListener(() => { if (game?.Master != null && game.Master.ChooseRepeat()) Refresh(); });
            fairButton = MakeButton(crt, "Fair", new Vector2(0.5f, 0f), new Vector2(215f, 14f), new Vector2(400f, 70f),
                "박람회 도전", 28, new Color(0.55f, 0.14f, 0.16f, 0.95f), out _);
            fairButton.onClick.AddListener(() => { if (game?.Master != null && game.Master.ChooseFair()) Refresh(); });

            // 2026-11-12 다음 웨이브 예고: 준비 패널(Prep·선택 대기)과 전투 중 사전 예고. 제조 창보다 먼저 만들어 제조 창이 위에 그려진다.
            prepView = MasterPrepView.Create(root, game, (parent, name, anchor, pos, size, label, font, bg) => MakeButton(parent, name, anchor, pos, size, label, font, bg, out _));
            BuildBrewPanel();
        }

        MasterPrepView prepView;
        public MasterPrepView Prep => prepView;

        void BuildBrewPanel()
        {
            var prt = UiFactory.Place("BrewPanel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 860f));
            var bg = UiFactory.AddImage(prt, new Color(0.07f, 0.06f, 0.08f, 0.97f));
            UiFactory.SkinPanel(bg);
            brewPanel = prt.gameObject;
            brewTitle = UiFactory.AddText(UiFactory.Place("Title", prt, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1400f, 48f)),
                "음료 만들기 — 전투 일시정지 중", 34, TextAnchor.MiddleCenter, Color.white);
            UiFactory.AddText(UiFactory.Place("RecipeHeader", prt, new Vector2(0f, 1f), new Vector2(40f, -76f), new Vector2(760f, 36f)),
                "레시피 (이번 판 재료 → 부족분 창고 재고)", 24, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.45f));
            UiFactory.AddText(UiFactory.Place("CupHeader", prt, new Vector2(1f, 1f), new Vector2(-40f, -76f), new Vector2(600f, 36f)),
                "보관한 잔 — 한 잔은 한 용도", 24, TextAnchor.MiddleLeft, new Color(0.75f, 0.92f, 1f));
            recipeArea = UiFactory.Place("Recipes", prt, new Vector2(0f, 1f), new Vector2(40f, -118f), new Vector2(780f, RowsPerPage * 62f));
            cupArea = UiFactory.Place("Cups", prt, new Vector2(1f, 1f), new Vector2(-40f, -118f), new Vector2(600f, CupRowsPerPage * 62f));
            // 2026-11-12 현재 잔 특성 상자(보관 잔 5줄 아래 −436…−676 — 잔 쪽 넘김 버튼(아래 120–172) 위)
            var trt = UiFactory.Place("TraitBox", prt, new Vector2(1f, 1f), new Vector2(-40f, -436f), new Vector2(600f, 240f));
            UiFactory.AddImage(trt, new Color(0.12f, 0.2f, 0.22f, 0.55f)).raycastTarget = false;
            var tlrt = UiFactory.Stretch("Text", trt);
            tlrt.offsetMin = new Vector2(14f, 8f); tlrt.offsetMax = new Vector2(-14f, -8f);
            traitBox = UiFactory.AddText(tlrt, "", 20, TextAnchor.UpperLeft, new Color(0.88f, 0.95f, 1f));
            traitBox.resizeTextForBestFit = true; traitBox.resizeTextMinSize = 16; traitBox.resizeTextMaxSize = 20;
            traitBox.verticalOverflow = VerticalWrapMode.Truncate;
            traitBox.raycastTarget = false;
            var prev = MakeButton(prt, "PrevPage", new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(150f, 52f), "◀ 이전", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            prev.onClick.AddListener(() => { recipePage = Mathf.Max(0, recipePage - 1); RefreshBrew(); });
            var next = MakeButton(prt, "NextPage", new Vector2(0f, 0f), new Vector2(200f, 120f), new Vector2(150f, 52f), "다음 ▶", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            next.onClick.AddListener(() => { recipePage++; RefreshBrew(); });
            pageLabel = UiFactory.AddText(UiFactory.Place("Page", prt, new Vector2(0f, 0f), new Vector2(370f, 120f), new Vector2(200f, 52f)), "", 22, TextAnchor.MiddleLeft, Color.white);
            var cprev = MakeButton(prt, "CupPrev", new Vector2(1f, 0f), new Vector2(-200f, 120f), new Vector2(150f, 52f), "◀ 이전", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            cprev.onClick.AddListener(() => { cupPage = Mathf.Max(0, cupPage - 1); RefreshBrew(); });
            var cnext = MakeButton(prt, "CupNext", new Vector2(1f, 0f), new Vector2(-40f, 120f), new Vector2(150f, 52f), "다음 ▶", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            cnext.onClick.AddListener(() => { cupPage++; RefreshBrew(); });
            // 품질 VFX 슬롯: 오른쪽 위 '보관한 잔' 머리글 오른쪽 빈 영역(머리글 글자·첫 잔 버튼과 겹치지 않음). 제출 때는 기존 컵(32×26 → 64×52, Point)을 뒤에 둔다.
            fxSlot = UiFactory.Place("QualityFx", prt, new Vector2(1f, 1f), new Vector2(-44f, -6f), new Vector2(FxSlotPx, FxSlotPx));
            var cupRt = UiFactory.Place("SubmitCup", fxSlot, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(64f, 52f));
            cupRt.pivot = new Vector2(0.5f, 0.5f);
            submitCup = cupRt.gameObject.AddComponent<Image>();
            submitCup.sprite = Resources.Load<Sprite>("DrinkLab/dl_coffee_cup");
            submitCup.preserveAspect = true;
            submitCup.raycastTarget = false;
            submitCup.enabled = false;
            // 캡션: 효과 슬롯(오른쪽 위 −44…−156) 왼쪽, 제목 줄 높이(−24…−64). 가운데 제목 글자(±≈260)와 '보관한 잔' 머리글(−76…−112) 사이.
            var capRt = UiFactory.Place("QualityCaption", prt, new Vector2(1f, 1f), new Vector2(-164f, -24f), new Vector2(300f, 40f));
            fxCaption = UiFactory.AddText(capRt, "", 24, TextAnchor.MiddleRight, new Color(1f, 0.86f, 0.45f));
            fxCaption.resizeTextForBestFit = true; fxCaption.resizeTextMinSize = 16; fxCaption.resizeTextMaxSize = 24;
            fxCaption.verticalOverflow = VerticalWrapMode.Truncate;
            fxCaption.raycastTarget = false;
            fxCaption.gameObject.SetActive(false);
            brewMessage = UiFactory.AddText(UiFactory.Place("Message", prt, new Vector2(0.5f, 0f), new Vector2(0f, 178f), new Vector2(1400f, 34f)), "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            brewFooter = UiFactory.AddText(UiFactory.Place("Footer", prt, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(1100f, 70f)), "", 22, TextAnchor.MiddleLeft, Color.white);
            brewFooter.resizeTextForBestFit = true; brewFooter.resizeTextMinSize = 16; brewFooter.resizeTextMaxSize = 22;
            brewFooter.verticalOverflow = VerticalWrapMode.Truncate;
            var close = MakeButton(prt, "Close", new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(260f, 62f), "닫기 (Esc)", 26, new Color(0.25f, 0.22f, 0.3f, 0.95f), out _);
            close.onClick.AddListener(() => game?.CloseMasterBrew());
            brewPanel.SetActive(false);
        }

        public void ShowBrew() { recipePage = 0; cupPage = 0; if (brewMessage != null) brewMessage.text = ""; brewPanel.SetActive(true); prepView?.Refresh(); RefreshBrew(); }
        public void HideBrew()
        {
            MasterVfx.ReleaseUi(); // 2026-11-12 재생 중 닫아도 다음 열기 때 멈춘 프레임이 남지 않게
            HideQualityCaption();
            if (submitCup != null) submitCup.enabled = false;
            if (brewPanel != null) brewPanel.SetActive(false);
            Refresh();
        }

        // ───── 2026-11-12 품질 효과 설명 캡션(제조 창 안, 효과 왼쪽) ─────
        Text fxCaption;
        float fxCaptionUntil;
        public const float FxCaptionSeconds = 1.6f;
        public bool QualityCaptionVisible => fxCaption != null && fxCaption.gameObject.activeSelf;
        public string QualityCaptionText => fxCaption != null ? fxCaption.text : "";

        /// <summary>품질 효과와 같은 순간 제조 창 안에 같은 설명을 보인다(HUD 배너는 불투명 제조 창 뒤에 깔려 보이지 않았다 — 2026-11-12 사용자 제보).</summary>
        public void ShowQualityCaption(string text)
        {
            if (fxCaption == null) return;
            fxCaption.text = text;
            fxCaption.gameObject.SetActive(!string.IsNullOrEmpty(text));
            fxCaptionUntil = Time.unscaledTime + FxCaptionSeconds;
        }

        void HideQualityCaption() { if (fxCaption != null) fxCaption.gameObject.SetActive(false); }

        /// <summary>제조 창에 보일 레시피 = 저장 레시피 + 예시 레시피(이름 중복 제외).</summary>
        public static List<DrinkRecipe> RecipeList()
        {
            var list = new List<DrinkRecipe>();
            foreach (var r in DrinkLab.Recipes) if (r != null) list.Add(r);
            foreach (var r in DrinkLab.ExamplePresets) if (r != null && !list.Exists(x => x.name == r.name)) list.Add(r);
            return list;
        }

        /// <summary>지금 실제로 만들 수 있는 레시피 수(런 임시 재료 + 창고 재고 기준).</summary>
        public static int CraftableCount(string runId)
        {
            var pool = DrinkLab.RunPool(runId);
            int n = 0;
            foreach (var r in RecipeList()) if (CanCraft(r, pool)) n++;
            return n;
        }

        static bool CanCraft(DrinkRecipe r, Dictionary<string, int> pool) =>
            DrinkValidator.Check(r).Ok && DrinkValidator.Shortages(r, id => (int)System.Math.Min(int.MaxValue, (long)(pool.TryGetValue(id, out int p) ? p : 0) + DrinkLab.Owned(id))).Count == 0;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame && game != null)
            {
                if (BrewOpen) game.CloseMasterBrew(); else game.OpenMasterBrew();
            }
            if (fxCaption != null && fxCaption.gameObject.activeSelf && Time.unscaledTime >= fxCaptionUntil) HideQualityCaption();
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 0.5f; Refresh(); }
        }

        public void Refresh()
        {
            if (this == null || game == null || game.Master == null) return;
            var st = game.Master.State;
            int held = 0;
            foreach (var c in DrinkLab.RunCupsFor(game.RunId)) if (c.IsHeld) held++;
            int craftable = CraftableCount(game.RunId);
            brewLabel.text = $"음료 만들기 [B] · 가능 {craftable} · 보관 {held}";
            brewButton.interactable = game.State == GameState.Playing && !game.MasterFreezeActive;

            bool awaiting = st.Phase == MasterPhase.AwaitChoice, clearing = st.Phase == MasterPhase.ClearingForFair;
            // 2026-11-12 선택 대기는 준비 패널(MasterPrepView)이 예고와 함께 맡는다 — 이 상단 패널은 박람회 접수 뒤 '남은 손님 처리' 안내만.
            choiceRoot.SetActive(clearing && game.State != GameState.GameOver);
            repeatButton.gameObject.SetActive(false);
            fairButton.gameObject.SetActive(false);
            prepView?.Refresh();
            if (awaiting)
                choiceText.text = $"재료 모으기 5/5 완료! 제출 {st.SubmittedThisCycle}잔{BonusText(st)} → 박람회 약화 {st.PreviewFairReduction}%\n" +
                                  $"반복해도 주기·난이도·약화율은 그대로({st.RepeatCount}회 반복) · 준비되면 박람회에 도전하세요";
            else if (clearing)
                choiceText.text = $"박람회 접수 완료(약화 {st.FairReductionSnapshot}% 확정)\n남은 손님 {Enemy.Active.Count}명을 먼저 처리하면 NPC가 현수막을 설치합니다";
            if (BrewOpen) RefreshBrew();
        }

        static string BonusText(MasterChallengeState st) => st.BonusPointsThisCycle > 0 ? $"(품질 추가 가치 +{st.BonusPointsThisCycle}p)" : "";

        void ClearRows(List<GameObject> rows) { foreach (var r in rows) if (r != null) Destroy(r); rows.Clear(); }

        public void RefreshBrew()
        {
            if (game == null || game.Master == null) return;
            ClearRows(recipeRows); ClearRows(cupRows);
            var pool = DrinkLab.RunPool(game.RunId);
            var recipes = RecipeList();
            int pages = Mathf.Max(1, (recipes.Count + RowsPerPage - 1) / RowsPerPage);
            recipePage = Mathf.Clamp(recipePage, 0, pages - 1);
            pageLabel.text = $"{recipePage + 1}/{pages}";
            for (int i = 0; i < RowsPerPage; i++)
            {
                int idx = recipePage * RowsPerPage + i;
                if (idx >= recipes.Count) break;
                var r = recipes[idx];
                bool can = CanCraft(r, pool);
                var row = UiFactory.Place("Recipe" + i, recipeArea, new Vector2(0f, 1f), new Vector2(0f, -i * 62f), new Vector2(780f, 56f));
                UiFactory.AddImage(row, new Color(1f, 1f, 1f, 0.06f));
                int q = DrinkQuality.Of(r);
                string kind = r.Kind == DrinkKind.Coffee ? "커피" : r.Kind == DrinkKind.Tea ? "차" : "칵테일";
                string newKind = r.Kind == DrinkKind.Cocktail && !string.IsNullOrEmpty(DrinkLab.RecipeKey(r)) && !game.Master.HasCocktailKind(DrinkLab.RecipeKey(r)) ? " · 새 종류" : "";
                var t = UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(560f, 52f)),
                    $"[{kind}] {DrinkValidator.SanitizeName(r.name, DrinkLab.DefaultName(r))}{(q >= 0 ? "  " + DrinkQuality.Label(q) : "")}{newKind}{(can ? "" : "  (재료 부족)")}",
                    22, TextAnchor.MiddleLeft, can ? Color.white : new Color(0.65f, 0.65f, 0.65f));
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = 22; t.verticalOverflow = VerticalWrapMode.Truncate;
                var b = MakeButton(row, "Craft", new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(190f, 48f), "만들기", 22, new Color(0.3f, 0.2f, 0.1f, 0.95f), out _);
                b.interactable = can;
                var recipe = r;
                b.onClick.AddListener(() => OnCraft(recipe));
                recipeRows.Add(row.gameObject);
            }

            // 2026-11-12: 보관 잔 + 이어 우릴 수 있는 마신 차(같은 찻잎 계보의 최신 잔만 — 원 잔당 1회라 자연히 하나)
            var cups = DrinkLab.RunCupsFor(game.RunId).FindAll(c => c.IsHeld || c.CanContinueSteep);
            cups.Sort((x, y) => (x.IsHeld ? 0 : 1).CompareTo(y.IsHeld ? 0 : 1));
            int cpages = Mathf.Max(1, (cups.Count + CupRowsPerPage - 1) / CupRowsPerPage);
            cupPage = Mathf.Clamp(cupPage, 0, cpages - 1);
            bool canSubmit = game.Master.State.CanSubmit;
            for (int i = 0; i < CupRowsPerPage; i++)
            {
                int idx = cupPage * CupRowsPerPage + i;
                if (idx >= cups.Count) break;
                var cup = cups[idx];
                var row = UiFactory.Place("Cup" + i, cupArea, new Vector2(0f, 1f), new Vector2(0f, -i * 62f), new Vector2(600f, 56f));
                UiFactory.AddImage(row, new Color(1f, 1f, 1f, 0.06f));
                string ql = cup.quality >= 0 ? " " + DrinkQuality.Label(cup.quality) : "";
                string steep = cup.Kind == DrinkKind.Tea ? $" · {cup.Steep}번째 우림" : "";
                if (!cup.IsHeld)
                {
                    // 마신 차: 같은 찻잎으로 다음 단계(재료 0) — 실행 전 규칙을 분명히
                    int next = cup.Steep + 1;
                    var ct = UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(390f, 52f)),
                        $"{(cup.recipe != null ? cup.recipe.name : "차")} {cup.Steep}번째 마심\n→ {next}번째: 재료 0·제출 불가·{RunDrinkTrait.Short(next >= 3 ? RunDrinkTraitKind.TeaThird : RunDrinkTraitKind.TeaSecond)}",
                        20, TextAnchor.MiddleLeft, new Color(0.75f, 0.92f, 1f));
                    ct.resizeTextForBestFit = true; ct.resizeTextMinSize = 15; ct.resizeTextMaxSize = 20; ct.verticalOverflow = VerticalWrapMode.Truncate;
                    var steepB = MakeButton(row, "ContinueSteep", new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(190f, 48f), $"이어 우리기 ({next}번째)", 20, new Color(0.18f, 0.3f, 0.32f, 0.95f), out _);
                    var src = cup;
                    steepB.onClick.AddListener(() => OnContinueSteep(src));
                    cupRows.Add(row.gameObject);
                    continue;
                }
                var t = UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(330f, 52f)),
                    $"{(cup.recipe != null ? cup.recipe.name : "음료")}{ql}{steep}\n마시면 → {RunDrinkTrait.Short(RunDrinkTrait.For(cup))}", 20, TextAnchor.MiddleLeft, Color.white);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 15; t.resizeTextMaxSize = 20; t.verticalOverflow = VerticalWrapMode.Truncate;
                var drink = MakeButton(row, "Drink", new Vector2(1f, 0.5f), new Vector2(-136f, 0f), new Vector2(120f, 48f), "마시기", 20, new Color(0.18f, 0.32f, 0.2f, 0.95f), out _);
                drink.onClick.AddListener(() => OnDrink(cup));
                var submit = MakeButton(row, "Submit", new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(124f, 48f),
                    MasterRules.SubmitPoints(cup.Kind, cup.quality) > MasterRules.CupPoints ? "제출 +50%" : "제출", 20, new Color(0.55f, 0.14f, 0.16f, 0.95f), out _);
                submit.interactable = canSubmit && cup.Submittable;
                if (!cup.Submittable) { var lbl = submit.GetComponentInChildren<Text>(); if (lbl != null) lbl.text = "제출 불가"; }
                submit.onClick.AddListener(() => OnSubmit(cup));
                cupRows.Add(row.gameObject);
            }
            if (cups.Count == 0)
            {
                var empty = UiFactory.Place("Empty", cupArea, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 90f));
                UiFactory.AddText(empty, "아직 보관한 잔이 없습니다.\n왼쪽에서 만들면 여기에서 마시거나 박람회에 제출합니다.", 20, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
                cupRows.Add(empty.gameObject);
            }

            var st = game.Master.State;
            string drunk = game.ActiveRunCup != null ? $"마신 잔: {game.ActiveRunCup.recipe?.name}" : "마신 잔: 출발 음료(또는 없음)";
            string submitLine = canSubmit ? $"이번 박람회 제출 {st.SubmittedThisCycle}잔{BonusText(st)} → 약화 {st.PreviewFairReduction}%"
                : st.FairReductionSnapshot >= 0 ? $"이번 박람회 약화 {st.FairReductionSnapshot}% 확정(접수 후 제출 불가)" : "제출은 재료 모으기 구간에서만";
            var trait = game.Player != null ? game.Player.DrinkTrait : RunDrinkTraitKind.None;
            string traitLine = trait == RunDrinkTraitKind.None ? "현재 잔 특성: 없음" : $"현재 잔 특성: {RunDrinkTrait.Name(trait)} — {RunDrinkTrait.Describe(trait)}";
            brewFooter.text = $"{submitLine} · 칵테일 {game.Master.CocktailUniqueCount}종 → 공격 +{game.Master.CocktailAttackPercent}%\n{drunk} · 런 제조 음료는 판매 불가";
            if (traitBox != null)
                traitBox.text = (trait == RunDrinkTraitKind.None ? "현재 잔 특성: 없음" : $"현재 잔 특성: {RunDrinkTrait.Name(trait)}\n{RunDrinkTrait.Describe(trait)}") +
                                "\n\n교체: 다른 잔을 마시면 지금 효과·특성은 사라지고 새 잔 것으로(중첩 없음)" +
                                "\n차 우림(게임 규칙): 마신 차에서 [이어 우리기] 1회 · 3번째까지 · 2·3번째는 재료 0·제출 불가 · 새 찻잎 = 1번째부터";
        }

        void OnCraft(DrinkRecipe r)
        {
            var res = game.CraftRunCup(r);
            brewMessage.text = res.Ok ? $"{res.Cup.recipe.name} 1잔 완성{(res.Cup.quality >= 0 ? " · " + DrinkQuality.Label(res.Cup.quality) : "")}" : res.Message;
            if (res.Ok) PlayQuality(res.Cup.quality, DrinkQualityFx.Moment.Crafted);
            Refresh(); RefreshBrew();
        }

        void OnContinueSteep(RunCup drunk)
        {
            var res = game.ContinueRunTea(drunk);
            brewMessage.text = res.Ok ? res.Message + " — 보관함에 추가, 마셔야 적용" : res.Message;
            Refresh(); RefreshBrew();
        }

        public string BrewFooterText => (brewFooter != null ? brewFooter.text : "") + "\n" + (traitBox != null ? traitBox.text : "");
        public string TraitBoxText => traitBox != null ? traitBox.text : "";
        Text traitBox;

        void OnDrink(RunCup cup)
        {
            bool ok = game.DrinkRunCup(cup, out var error);
            brewMessage.text = ok ? $"{cup.recipe?.name}을(를) 마셨습니다 — 이전 음료 효과는 교체" : error;
            if (ok) PlayQuality(cup.quality, DrinkQualityFx.Moment.Drunk);
            Refresh(); RefreshBrew();
        }

        void OnSubmit(RunCup cup)
        {
            bool ok = game.SubmitRunCup(cup, out var error);
            var st = game.Master.State;
            brewMessage.text = ok ? $"박람회에 제출 — {st.SubmittedThisCycle}잔{BonusText(st)}, 예상 약화 {st.PreviewFairReduction}%" : error;
            if (ok) PlayQuality(cup.quality, DrinkQualityFx.Moment.Submitted);
            Refresh(); RefreshBrew();
        }

        /// <summary>성공 거래 1건 = 품질 효과 1회(제조 창 슬롯, 실제 시간). 품질 없는 음료(칵테일·차)는 효과 없이 컵만(제출 시).</summary>
        void PlayQuality(int quality, DrinkQualityFx.Moment moment)
        {
            if (submitCup != null) submitCup.enabled = moment == DrinkQualityFx.Moment.Submitted && submitCup.sprite != null;
            DrinkQualityFx.Play(game, quality, moment, fxSlot);
        }
    }

    /// <summary>
    /// 품질 시각 피드백(제조 완료·음용·제출). 인수 아트 quality_normal/excellent/master(저이펙트 _low 교체)를 제조 창 슬롯에 1회 재생하고
    /// 등급 글자·추가 성능을 HUD 배너로 함께 알린다. 슬롯이 없으면(창 밖 호출) 배너만.
    /// </summary>
    public static class DrinkQualityFx
    {
        public enum Moment { Crafted, Drunk, Submitted }
        public static int LastGrade { get; private set; } = -1;
        public static Moment LastMoment { get; private set; }
        public static string LastClip { get; private set; }

        public static void Play(GameManager game, int quality, Moment moment, RectTransform slot = null)
        {
            var g = DrinkQuality.GradeOf(quality);
            LastGrade = (int)g; LastMoment = moment; LastClip = null;
            string what = moment == Moment.Crafted ? "제조 완료" : moment == Moment.Drunk ? "마심" : "박람회 제출";
            if (g == DrinkQuality.Grade.None) return;
            string clip = MasterArt.QualityClip(g, HitFeedback.LowEffects);
            if (slot != null && clip != null && MasterVfx.PlayUi(clip, slot, MasterChallengeHud.FxArtPx) != null) LastClip = clip;
            string extra = g == DrinkQuality.Grade.Best ? (moment == Moment.Submitted ? " · 제출 가치 +50%" : moment == Moment.Drunk ? " · 효과 +5%" : "") : "";
            // 2026-11-12: 제조 창 안이면 효과 옆 캡션으로(뒤에 깔리는 HUD 배너 생략). 창 밖 호출이면 기존 배너.
            if (slot != null && game != null && game.MasterHud != null && game.MasterBrewOpen) game.MasterHud.ShowQualityCaption($"{DrinkQuality.Label(quality)} · {what}{extra}");
            else game?.Hud?.ShowBanner(DrinkQuality.Label(quality), $"{what}{extra}", g == DrinkQuality.Grade.Best ? 1.8f : 1.2f);
        }
    }
}
