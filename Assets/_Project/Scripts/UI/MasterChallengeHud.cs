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
        public const int RowsPerPage = 9;
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

            BuildBrewPanel();
        }

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
            cupArea = UiFactory.Place("Cups", prt, new Vector2(1f, 1f), new Vector2(-40f, -118f), new Vector2(600f, RowsPerPage * 62f));
            var prev = MakeButton(prt, "PrevPage", new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(150f, 52f), "◀ 이전", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            prev.onClick.AddListener(() => { recipePage = Mathf.Max(0, recipePage - 1); RefreshBrew(); });
            var next = MakeButton(prt, "NextPage", new Vector2(0f, 0f), new Vector2(200f, 120f), new Vector2(150f, 52f), "다음 ▶", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            next.onClick.AddListener(() => { recipePage++; RefreshBrew(); });
            pageLabel = UiFactory.AddText(UiFactory.Place("Page", prt, new Vector2(0f, 0f), new Vector2(370f, 120f), new Vector2(200f, 52f)), "", 22, TextAnchor.MiddleLeft, Color.white);
            var cprev = MakeButton(prt, "CupPrev", new Vector2(1f, 0f), new Vector2(-200f, 120f), new Vector2(150f, 52f), "◀ 이전", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            cprev.onClick.AddListener(() => { cupPage = Mathf.Max(0, cupPage - 1); RefreshBrew(); });
            var cnext = MakeButton(prt, "CupNext", new Vector2(1f, 0f), new Vector2(-40f, 120f), new Vector2(150f, 52f), "다음 ▶", 22, new Color(0.2f, 0.2f, 0.24f, 0.95f), out _);
            cnext.onClick.AddListener(() => { cupPage++; RefreshBrew(); });
            brewMessage = UiFactory.AddText(UiFactory.Place("Message", prt, new Vector2(0.5f, 0f), new Vector2(0f, 178f), new Vector2(1400f, 34f)), "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            brewFooter = UiFactory.AddText(UiFactory.Place("Footer", prt, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(1100f, 70f)), "", 22, TextAnchor.MiddleLeft, Color.white);
            brewFooter.verticalOverflow = VerticalWrapMode.Truncate;
            var close = MakeButton(prt, "Close", new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(260f, 62f), "닫기 (Esc)", 26, new Color(0.25f, 0.22f, 0.3f, 0.95f), out _);
            close.onClick.AddListener(() => game?.CloseMasterBrew());
            brewPanel.SetActive(false);
        }

        public void ShowBrew() { recipePage = 0; cupPage = 0; if (brewMessage != null) brewMessage.text = ""; brewPanel.SetActive(true); RefreshBrew(); }
        public void HideBrew() { if (brewPanel != null) brewPanel.SetActive(false); Refresh(); }

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
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 0.5f; Refresh(); }
        }

        public void Refresh()
        {
            if (game == null || game.Master == null) return;
            var st = game.Master.State;
            int held = 0;
            foreach (var c in DrinkLab.RunCupsFor(game.RunId)) if (c.IsHeld) held++;
            int craftable = CraftableCount(game.RunId);
            brewLabel.text = $"음료 만들기 [B] · 가능 {craftable} · 보관 {held}";
            brewButton.interactable = game.State == GameState.Playing && !game.MasterFreezeActive;

            bool awaiting = st.Phase == MasterPhase.AwaitChoice, clearing = st.Phase == MasterPhase.ClearingForFair;
            choiceRoot.SetActive((awaiting || clearing) && game.State != GameState.GameOver);
            repeatButton.gameObject.SetActive(awaiting);
            fairButton.gameObject.SetActive(awaiting);
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

            var cups = DrinkLab.RunCupsFor(game.RunId).FindAll(c => c.IsHeld);
            int cpages = Mathf.Max(1, (cups.Count + RowsPerPage - 1) / RowsPerPage);
            cupPage = Mathf.Clamp(cupPage, 0, cpages - 1);
            bool canSubmit = game.Master.State.CanSubmit;
            for (int i = 0; i < RowsPerPage; i++)
            {
                int idx = cupPage * RowsPerPage + i;
                if (idx >= cups.Count) break;
                var cup = cups[idx];
                var row = UiFactory.Place("Cup" + i, cupArea, new Vector2(0f, 1f), new Vector2(0f, -i * 62f), new Vector2(600f, 56f));
                UiFactory.AddImage(row, new Color(1f, 1f, 1f, 0.06f));
                string ql = cup.quality >= 0 ? " " + DrinkQuality.Label(cup.quality) : "";
                var t = UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(330f, 52f)),
                    (cup.recipe != null ? cup.recipe.name : "음료") + ql, 20, TextAnchor.MiddleLeft, Color.white);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 13; t.resizeTextMaxSize = 20; t.verticalOverflow = VerticalWrapMode.Truncate;
                var drink = MakeButton(row, "Drink", new Vector2(1f, 0.5f), new Vector2(-136f, 0f), new Vector2(120f, 48f), "마시기", 20, new Color(0.18f, 0.32f, 0.2f, 0.95f), out _);
                drink.onClick.AddListener(() => OnDrink(cup));
                var submit = MakeButton(row, "Submit", new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(124f, 48f),
                    MasterRules.SubmitPoints(cup.Kind, cup.quality) > MasterRules.CupPoints ? "제출 +50%" : "제출", 20, new Color(0.55f, 0.14f, 0.16f, 0.95f), out _);
                submit.interactable = canSubmit;
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
            brewFooter.text = $"{submitLine} · 칵테일 {game.Master.CocktailUniqueCount}종 → 공격 +{game.Master.CocktailAttackPercent}%\n{drunk} · 런 제조 음료는 판매 불가 · 다른 잔을 마시면 이전 효과는 교체(중첩 없음)";
        }

        void OnCraft(DrinkRecipe r)
        {
            var res = game.CraftRunCup(r);
            brewMessage.text = res.Ok ? $"{res.Cup.recipe.name} 1잔 완성{(res.Cup.quality >= 0 ? " · " + DrinkQuality.Label(res.Cup.quality) : "")}" : res.Message;
            if (res.Ok) DrinkQualityFx.Play(game, res.Cup.quality, DrinkQualityFx.Moment.Crafted);
            Refresh(); RefreshBrew();
        }

        void OnDrink(RunCup cup)
        {
            bool ok = game.DrinkRunCup(cup, out var error);
            brewMessage.text = ok ? $"{cup.recipe?.name}을(를) 마셨습니다 — 이전 음료 효과는 교체" : error;
            if (ok) DrinkQualityFx.Play(game, cup.quality, DrinkQualityFx.Moment.Drunk);
            Refresh(); RefreshBrew();
        }

        void OnSubmit(RunCup cup)
        {
            bool ok = game.SubmitRunCup(cup, out var error);
            var st = game.Master.State;
            brewMessage.text = ok ? $"박람회에 제출 — {st.SubmittedThisCycle}잔{BonusText(st)}, 예상 약화 {st.PreviewFairReduction}%" : error;
            if (ok) DrinkQualityFx.Play(game, cup.quality, DrinkQualityFx.Moment.Submitted);
            Refresh(); RefreshBrew();
        }
    }

    /// <summary>
    /// 품질 시각 피드백(제조 완료·음용·제출). **임시**: 등급별로 모양이 다른 별 표식(★/★★/★★★)과 짧은 확대 연출을 HUD 배너로 보여준다.
    /// 아트 담당 품질 VFX(보통/좋음/최고 6/8/8f@12fps, 저이펙트 3f) 검수·인수 후 월드 VFX로 교체한다 — 최종 아트 아님.
    /// </summary>
    public static class DrinkQualityFx
    {
        public enum Moment { Crafted, Drunk, Submitted }
        public static int LastGrade { get; private set; } = -1;
        public static Moment LastMoment { get; private set; }

        public static void Play(GameManager game, int quality, Moment moment)
        {
            var g = DrinkQuality.GradeOf(quality);
            LastGrade = (int)g; LastMoment = moment;
            string what = moment == Moment.Crafted ? "제조 완료" : moment == Moment.Drunk ? "마심" : "박람회 제출";
            if (g == DrinkQuality.Grade.None) return;
            string extra = g == DrinkQuality.Grade.Best ? (moment == Moment.Submitted ? " · 제출 가치 +50%" : moment == Moment.Drunk ? " · 효과 +5%" : "") : "";
            game?.Hud?.ShowBanner(DrinkQuality.Label(quality), $"{what}{extra}", g == DrinkQuality.Grade.Best ? 1.8f : 1.2f);
        }
    }
}
