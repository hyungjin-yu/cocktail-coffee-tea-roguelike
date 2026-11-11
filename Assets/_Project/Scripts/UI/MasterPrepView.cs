using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-12 다음 웨이브 예고(1920×1080 기준). MasterChallengeHud가 만든다.
    /// - 준비 패널(전투 정지 중): 주기 시작 Prep(예고 1개 + [음료 만들기] [전투 시작]) / 수집 5/5 뒤 선택(예고 2개 + [재료 모으기 반복] [음료 만들기] [박람회 도전]).
    ///   좌측 HUD 정보 패널(x≤605)과 우측 '음료 만들기' 버튼(x≥1565) 사이 610–1550, 상단 바 아래 y 130–912(하단 상태 줄 위).
    /// - 사전 예고(전투 중, 비모달): 우측 '음료 만들기' 버튼 아래 작은 상자 — 다음 웨이브(또는 두 선택지) 요약. 매 웨이브 모달 없음.
    /// 표시 내용은 모두 Director.UpcomingWaves()의 확정 계획(실제 생성과 같은 목록)에서 계산한다.
    /// </summary>
    public sealed class MasterPrepView : MonoBehaviour
    {
        public const float PanelX = 1080f, PanelTop = 130f, PanelW = 940f, PanelH = 782f;
        public const float BlockH = 296f, RowH = 52f, MaxRows = 3; // 블록 296: 머리 6-38, 요약 38-64, 줄 3x52 66-222, 조언 224-282
        public const float NoticeW = 330f, NoticeH = 112f, NoticeGap = 12f;

        GameManager game;
        RectTransform panel, blocks, notice;
        Text panelTitle, panelFooter, noticeText;
        Button startButton, brewButton, repeatButton, fairButton;
        string builtKey;

        public bool PanelVisible => panel != null && panel.gameObject.activeSelf;
        public bool NoticeVisible => notice != null && notice.gameObject.activeSelf;
        public string NoticeText => noticeText != null ? noticeText.text : "";
        public string PanelTitle => panelTitle != null ? panelTitle.text : "";
        public RectTransform Panel => panel;
        public RectTransform Notice => notice;
        /// <summary>준비 패널에 그린 줄 문구(시험·검수용).</summary>
        public readonly List<string> RowTexts = new List<string>();
        public readonly List<string> BlockHeaders = new List<string>();
        public int Rebuilds { get; private set; }

        public static MasterPrepView Create(RectTransform root, GameManager game, System.Func<RectTransform, string, Vector2, Vector2, Vector2, string, int, Color, Button> makeButton)
        {
            var v = root.gameObject.AddComponent<MasterPrepView>();
            v.game = game;
            v.Build(root, makeButton);
            return v;
        }

        void Build(RectTransform root, System.Func<RectTransform, string, Vector2, Vector2, Vector2, string, int, Color, Button> makeButton)
        {
            panel = UiFactory.Place("MasterPrep", root, new Vector2(0f, 1f), new Vector2(PanelX - PanelW * 0.5f, -PanelTop), new Vector2(PanelW, PanelH));
            var bg = UiFactory.AddImage(panel, new Color(0.06f, 0.05f, 0.08f, 1f)); // 불투명 — 뒤 HUD 배너·피해 숫자가 비치지 않게
            bg.raycastTarget = true;
            UiFactory.SkinPanel(bg);
            panelTitle = UiFactory.AddText(UiFactory.Place("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(PanelW - 40f, 44f)),
                "", 30, TextAnchor.MiddleCenter, Color.white);
            panelTitle.resizeTextForBestFit = true; panelTitle.resizeTextMinSize = 20; panelTitle.resizeTextMaxSize = 30;
            blocks = UiFactory.Place("Blocks", panel, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(PanelW - 40f, BlockH * 2f));
            panelFooter = UiFactory.AddText(UiFactory.Place("Footer", panel, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(PanelW - 40f, 30f)),
                "", 20, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f));
            panelFooter.resizeTextForBestFit = true; panelFooter.resizeTextMinSize = 14; panelFooter.resizeTextMaxSize = 20;
            panelFooter.verticalOverflow = VerticalWrapMode.Truncate;

            repeatButton = makeButton(panel, "Repeat", new Vector2(0.5f, 0f), new Vector2(-300f, 16f), new Vector2(280f, 70f), "재료 모으기 반복 (1~5)", 24, new Color(0.18f, 0.32f, 0.2f, 0.95f));
            repeatButton.onClick.AddListener(() => { if (game?.Master != null && game.Master.ChooseRepeat()) Refresh(); });
            brewButton = makeButton(panel, "PrepBrew", new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(280f, 70f), "음료 만들기 [B]", 24, new Color(0.32f, 0.18f, 0.1f, 0.95f));
            brewButton.onClick.AddListener(() => game?.OpenMasterBrew());
            fairButton = makeButton(panel, "Fair", new Vector2(0.5f, 0f), new Vector2(300f, 16f), new Vector2(280f, 70f), "박람회 도전", 26, new Color(0.55f, 0.14f, 0.16f, 0.95f));
            fairButton.onClick.AddListener(() => { if (game?.Master != null && game.Master.ChooseFair()) Refresh(); });
            startButton = makeButton(panel, "PrepStart", new Vector2(0.5f, 0f), new Vector2(300f, 16f), new Vector2(280f, 70f), "전투 시작", 28, new Color(0.55f, 0.32f, 0.14f, 0.95f));
            startButton.onClick.AddListener(() => { if (game?.Master != null && game.Master.StartBattle()) Refresh(); });
            panel.gameObject.SetActive(false);

            notice = UiFactory.Place("NextWaveNotice", root, new Vector2(1f, 1f),
                new Vector2(-DrinkRunHud.ChipRight, -(MasterChallengeHud.BrewButtonTop + MasterChallengeHud.BrewButtonH + NoticeGap)), new Vector2(NoticeW, NoticeH));
            var nbg = UiFactory.AddImage(notice, new Color(0.06f, 0.05f, 0.08f, 0.78f));
            nbg.raycastTarget = false;
            var nrt = UiFactory.Stretch("Text", notice);
            nrt.offsetMin = new Vector2(10f, 6f); nrt.offsetMax = new Vector2(-10f, -6f);
            noticeText = UiFactory.AddText(nrt, "", 20, TextAnchor.UpperLeft, new Color(0.92f, 0.92f, 0.95f));
            noticeText.resizeTextForBestFit = true; noticeText.resizeTextMinSize = 14; noticeText.resizeTextMaxSize = 20;
            noticeText.verticalOverflow = VerticalWrapMode.Truncate;
            noticeText.raycastTarget = false;
            notice.gameObject.SetActive(false);
        }

        public static string ReductionText(MasterChallengeDirector.Upcoming u) =>
            u.plan.Segment == MasterSegment.Reward ? "약화 없음(무해)" : u.reduction > 0 ? $"약화 {u.reduction}%{(u.reductionFinal ? "" : "(제출에 따라 변동)")}" : u.reductionFinal ? "약화 0%" : "약화 0%(제출하면 증가)";

        /// <summary>사전 예고 한 줄 요약: "돌진 웨이브 · 14명 · 에스프레소 벅 ×8".</summary>
        public static string ShortLine(MasterChallengeDirector.Upcoming u)
        {
            var groups = u.plan.Groups();
            string top = groups.Count > 0 ? $"{groups[0].def.name} ×{groups[0].count}" : "";
            string label = u.plan.Label;
            string head = label == "박람회" ? "박람회" : label == "무해 보상" ? "무해 보상" : label + " 웨이브";
            return $"{head} · {u.plan.Count}명{(top.Length > 0 ? " · " + top : "")}";
        }

        public void Refresh()
        {
            if (this == null || panel == null || notice == null || game == null || game.Master == null) return;
            var st = game.Master.State;
            bool over = game.State == GameState.GameOver;
            bool preparing = st.IsPreparing && !over;
            bool showPanel = preparing && !game.MasterBrewOpen;
            panel.gameObject.SetActive(showPanel);
            var ups = game.Master.UpcomingWaves();
            if (showPanel) RebuildPanel(st, ups);

            bool showNotice = !over && !preparing && st.Phase == MasterPhase.Wave && ups.Count > 0 && !game.MasterBrewOpen;
            notice.gameObject.SetActive(showNotice);
            if (showNotice)
            {
                if (ups.Count == 1) noticeText.text = $"다음 · {ups[0].title}\n{ShortLine(ups[0])}";
                else noticeText.text = $"다음 선택(5/5 뒤 준비)\n반복: {ShortLine(ups[0])}\n박람회: {ShortLine(ups[1])}";
            }
        }

        void RebuildPanel(MasterChallengeState st, List<MasterChallengeDirector.Upcoming> ups)
        {
            bool choice = st.Phase == MasterPhase.AwaitChoice;
            // 예고 블록 수에 맞춘 높이: 제목 60 + 블록 n×296 + 안내·버튼 130 (Prep 1개 = 486, 선택 2개 = 782)
            panel.sizeDelta = new Vector2(PanelW, 60f + Mathf.Clamp(ups.Count, 1, 2) * BlockH + 130f);
            repeatButton.gameObject.SetActive(choice);
            fairButton.gameObject.SetActive(choice);
            startButton.gameObject.SetActive(!choice);
            brewButton.gameObject.SetActive(true);
            panelTitle.text = choice ? "재료 모으기 5/5 완료 — 다음을 고르세요 (전투 정지 중)" : $"다음 웨이브 준비 — {MasterChallengeDirector.WaveTitle(st.Wave)} (전투 정지 중)";
            panelFooter.text = choice
                ? $"제출 {st.SubmittedThisCycle}잔 → 박람회 약화 {st.PreviewFairReduction}% · 반복해도 주기·난이도는 그대로({st.RepeatCount}회 반복) · 음료는 지금 만들고 마실 수 있음"
                : st.CanSubmit ? $"음료를 만들고 마신 뒤 시작하세요 · 이번 주기 박람회 제출 {st.SubmittedThisCycle}잔 → 약화 {st.PreviewFairReduction}%" : "음료를 만들고 마신 뒤 시작하세요";

            string key = $"{st.Phase}|{st.Wave}|{st.RepeatCount}|{st.PreviewFairReduction}|{st.CarriedReduction}|{(game.Challenge != null ? game.Challenge.HpMultiplier : 1f):0.###}|{game.Player?.AffinityDrink}";
            foreach (var u in ups) key += "|" + u.plan.GetHashCode();
            if (key == builtKey) return;
            builtKey = key;
            Rebuilds++;
            for (int i = blocks.childCount - 1; i >= 0; i--) { var go = blocks.GetChild(i).gameObject; if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            RowTexts.Clear(); BlockHeaders.Clear();
            float mult = game.Challenge != null ? game.Challenge.HpMultiplier : 1f;
            for (int b = 0; b < ups.Count && b < 2; b++) BuildBlock(ups[b], b, mult);
        }

        void BuildBlock(MasterChallengeDirector.Upcoming u, int index, float mult)
        {
            var brt = UiFactory.Place("Block" + index, blocks, new Vector2(0.5f, 1f), new Vector2(0f, -index * BlockH), new Vector2(PanelW - 40f, BlockH - 10f));
            UiFactory.AddImage(brt, new Color(1f, 1f, 1f, 0.05f)).raycastTarget = false;
            string head = (u.choice != null ? $"[{u.choice}] " : "") + $"{u.title} — {u.plan.Label}{(u.plan.Label == "박람회" || u.plan.Label == "무해 보상" ? "" : " 웨이브")}";
            BlockHeaders.Add(head);
            var h = UiFactory.AddText(UiFactory.Place("Head", brt, new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(PanelW - 72f, 32f)),
                head, 24, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.45f));
            h.resizeTextForBestFit = true; h.resizeTextMinSize = 16; h.resizeTextMaxSize = 24;
            int kindSwarm = u.plan.CountOf(MasterWavePlan.Kind.Swarm), kindElite = u.plan.CountOf(MasterWavePlan.Kind.Elite), kindRush = u.plan.CountOf(MasterWavePlan.Kind.Rush);
            string mix = u.plan.Segment == MasterSegment.Collect ? $" · 무리 {kindSwarm} · 강적 {kindElite} · 돌진 {kindRush}" : "";
            var sum = UiFactory.AddText(UiFactory.Place("Summary", brt, new Vector2(0f, 1f), new Vector2(16f, -38f), new Vector2(PanelW - 72f, 26f)),
                $"총 {u.plan.Count}명{mix} · {ReductionText(u)}", 20, TextAnchor.MiddleLeft, Color.white);
            sum.resizeTextForBestFit = true; sum.resizeTextMinSize = 14; sum.resizeTextMaxSize = 20;

            var groups = u.plan.Groups();
            int rows = Mathf.Min(groups.Count, (int)MaxRows);
            for (int r = 0; r < rows; r++)
            {
                var g = groups[r];
                var rrt = UiFactory.Place("Row" + r, brt, new Vector2(0f, 1f), new Vector2(16f, -66f - r * RowH), new Vector2(PanelW - 72f, RowH - 4f));
                var irt = UiFactory.Place("Icon", rrt, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(RowH - 6f, RowH - 6f));
                irt.pivot = new Vector2(0f, 0.5f);
                var icon = irt.gameObject.AddComponent<Image>();
                icon.sprite = MasterWavePlan.IconOf(g.def);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = icon.sprite != null;
                int hp = MasterWavePlan.ExpectedHp(g.def, u.plan.Segment, u.plan.Wave, u.reduction, mult);
                string fam = g.family != MonsterFamily.Neutral ? MonsterFamilies.Mark(g.family) + " " : "";
                string kind = u.plan.Segment == MasterSegment.Reward ? "무해" : u.plan.Segment == MasterSegment.Fair ? "박람회" : MasterWavePlan.KindName(g.kind);
                string line = $"{fam}{g.def.name} ×{g.count} · 체력 약 {hp} · {MasterWavePlan.TraitOf(g.def)} [{kind}]";
                RowTexts.Add(line);
                var t = UiFactory.AddText(UiFactory.Place("Text", rrt, new Vector2(0f, 0.5f), new Vector2(RowH + 6f, 0f), new Vector2(PanelW - 72f - RowH - 10f, RowH - 6f)),
                    line, 22, TextAnchor.MiddleLeft, Color.white);
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = 22;
                t.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (groups.Count > rows)
            {
                int rest = 0; for (int r = rows; r < groups.Count; r++) rest += groups[r].count;
                RowTexts.Add($"그 외 {groups.Count - rows}종 {rest}명");
            }
            string extra = groups.Count > rows ? $"그 외 {groups.Count - rows}종 · " : "";
            string aff = u.plan.AffinityHint();
            var adv = UiFactory.AddText(UiFactory.Place("Advice", brt, new Vector2(0f, 0f), new Vector2(16f, 4f), new Vector2(PanelW - 72f, 58f)),
                $"{extra}{u.plan.Advice()}{(aff.Length > 0 ? "\n" + aff : "")}", 19, TextAnchor.LowerLeft, new Color(0.78f, 0.92f, 1f));
            adv.resizeTextForBestFit = true; adv.resizeTextMinSize = 13; adv.resizeTextMaxSize = 19;
            adv.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
