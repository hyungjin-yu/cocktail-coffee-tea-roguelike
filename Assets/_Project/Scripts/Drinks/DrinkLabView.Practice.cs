using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 5: 음료 제조 첫 실습(네그로니 1잔) + 차·커피 화면 첫 진입 안내.
    /// 실습은 실제 제조 기능을 그대로 쓴다(재료 선택 → 단위 따르기 → 기법 → 제조 확정 → 준비 음료 보관 → 출발 시 마시기 안내).
    /// 진행 판정은 실제 배합·준비 음료 상태로만 하고 화면 안내는 판정 결과를 보여 줄 뿐이다(건너뛰기·다시 보기·창 닫고 다시 열기 모두 상태에서 다시 계산).
    /// 재료: 부족분만 저장당 1회 지급(DrinkLab.GrantPracticeKit, 판매 불가 출처). 안내 완료 = settings.json craftGuideVersion.
    /// </summary>
    public partial class DrinkLabView
    {
        public enum PracticeStep { None, Intro, Gin, Campari, Vermouth, Technique, Craft, Done }
        PracticeStep practiceStep;
        RectTransform practiceBubble;
        bool practiceOffered;
        public PracticeStep CurrentPracticeStep => practiceStep;
        public string PracticeBubbleText => practiceBubble != null ? practiceBubble.Find("Line")?.GetComponent<Text>()?.text + "\n" + practiceBubble.Find("Sub")?.GetComponent<Text>()?.text : "";
        public static bool PracticeSuppressForTests;
        public const int PracticeGuideVersion = 1;
        static bool PracticeNeeded => !PracticeSuppressForTests && SettingsService.Current.craftGuideVersion < PracticeGuideVersion;

        /// <summary>제조 탭을 만든 뒤 호출 — 진행 중이면 다시 그리고, 첫 진입이면 실습 제안, 차·커피 첫 진입이면 그 화면 안내.</summary>
        void AfterCraftBuilt()
        {
            practiceBubble = null; // 본문과 함께 파괴됨
            if (practiceStep != PracticeStep.None) { RenderPractice(); return; }
            if (kind == DrinkKind.Cocktail && PracticeNeeded && !practiceOffered)
            {
                practiceOffered = true;
                practiceStep = PracticeStep.Intro;
                RenderPractice();
                return;
            }
            if (!PracticeSuppressForTests && kind != DrinkKind.Cocktail && !SettingsService.KindGuideSeen((int)kind)) ShowKindGuide();
        }

        /// <summary>"실습 다시 보기"·첫 제안(시험도 같은 경로). 칵테일이 아니면 칵테일로 바꾼다(기존 SetKind 규칙: 배합 비움).</summary>
        public void StartPractice(bool replay)
        {
            if (Busy("실습 시작")) return;
            practiceStep = PracticeStep.Intro;
            practiceOffered = true;
            if (kind != DrinkKind.Cocktail) { SetKind(DrinkKind.Cocktail); return; } // SetTab → AfterCraftBuilt → RenderPractice
            RenderPractice();
        }

        public void SkipPractice()
        {
            practiceStep = PracticeStep.None;
            SettingsService.CompleteCraftGuide(PracticeGuideVersion);
            RemovePracticeBubble();
        }

        /// <summary>실습 시작: 부족분 1회 지급(이미 받았으면 지급 없이 진행) → 빈 칵테일 배합 → 1단계.</summary>
        public void BeginPracticeSteps()
        {
            if (practiceStep != PracticeStep.Intro || Busy("실습 시작")) return;
            if (DrinkLab.Prepared != null) { RenderPractice(); return; } // 준비 음료가 있으면 시작하지 않는다(안내 문구로 이유 표시)
            if (!DrinkLab.PracticeGranted)
            {
                if (DrinkLab.GrantPracticeKit(out var got, out var error))
                    Message(got.Count == 0 ? "실습에 필요한 재료가 이미 창고에 있습니다." : $"실습 재료를 채웠습니다: {string.Join(", ", got.ConvertAll(a => DrinkIngredients.Get(a.id)?.name + " " + DrinkRules.Format(a.amount) + "mL"))} (판매 불가 출처)");
                else { Message(error); return; } // 저장 실패 — 지급·진행 모두 없음
            }
            draft = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            recipes.StopEditing();
            selected = null;
            practiceStep = PracticeStep.Gin;
            Rebind(); // SetTab(Craft) → AfterCraftBuilt → RenderPractice
        }

        /// <summary>실제 상태에서 단계를 다시 계산(매 프레임). 앞 단계 조건이 깨지면(빼기·비우기) 그 단계로 돌아간다.</summary>
        void TickPractice()
        {
            if (practiceStep == PracticeStep.None || practiceStep == PracticeStep.Intro || practiceStep == PracticeStep.Done || tab != Tab.Craft) return;
            var target = PracticeRecipe;
            PracticeStep next;
            var prepared = DrinkLab.Prepared;
            if (prepared != null && prepared.state == PreparedDrink.Ready && SameAsPractice(prepared.recipe)) next = PracticeStep.Done;
            else if (kind != DrinkKind.Cocktail) next = PracticeStep.Gin;
            else if (draft.AmountOf(DrinkIngredients.Gin) < Need(DrinkIngredients.Gin)) next = PracticeStep.Gin;
            else if (draft.AmountOf(DrinkIngredients.Campari) < Need(DrinkIngredients.Campari)) next = PracticeStep.Campari;
            else if (draft.AmountOf(DrinkIngredients.SweetVermouth) < Need(DrinkIngredients.SweetVermouth)) next = PracticeStep.Vermouth;
            else if (draft.technique != target.technique) next = PracticeStep.Technique;
            else next = PracticeStep.Craft;
            if (next != practiceStep) { practiceStep = next; RenderPractice(); }
            else if (practiceBubble != null && (next == PracticeStep.Gin || next == PracticeStep.Campari || next == PracticeStep.Vermouth))
            {
                var sub = practiceBubble.Find("Sub")?.GetComponent<Text>();
                if (sub != null) { var s = PracticeSub(next); if (sub.text != s) sub.text = s; } // 지금 양 갱신
            }
        }

        static DrinkRecipe PracticeRecipe => DrinkLab.PracticeRecipe;
        static int Need(string id) => PracticeRecipe.AmountOf(id);

        static bool SameAsPractice(DrinkRecipe r) =>
            r != null && r.Kind == DrinkKind.Cocktail && r.AmountOf(DrinkIngredients.Gin) >= Need(DrinkIngredients.Gin)
            && r.AmountOf(DrinkIngredients.Campari) >= Need(DrinkIngredients.Campari) && r.AmountOf(DrinkIngredients.SweetVermouth) >= Need(DrinkIngredients.SweetVermouth);

        string IngredientStepSub(string id, int index)
        {
            var def = DrinkIngredients.Get(id);
            int now = draft.AmountOf(id), need = Need(id);
            string sel = selected == def ? "" : $"왼쪽 목록에서 {KoreanParticle.EulReul(def?.name)} 고르고 ";
            string over = now > need ? $" · {DrinkRules.Format(now - need)}mL 넘쳤습니다 — −5로 줄여도 됩니다" : "";
            return $"{sel}+30mL 단위 버튼을 누르면 병이 기울어 지거에 30mL를 따른 뒤 잔으로 옮깁니다(병을 직접 끌어 부어도 됩니다). 지금 {DrinkRules.Format(now)} / {DrinkRules.Format(need)}mL{over}";
        }

        string PracticeSub(PracticeStep s)
        {
            switch (s)
            {
                case PracticeStep.Intro:
                    return DrinkLab.Prepared != null
                        ? "이미 준비 음료가 있어 새 잔을 만들 수 없습니다 — '주문·판매' 탭에서 팔거나 버리거나, 출발해 마신 뒤 '실습 다시 보기'를 누르세요."
                        : "진·캄파리·스위트 베르무트를 30mL씩 지거로 따라 빌드합니다. 부족한 재료는 한 번만 실습용으로 채워 드립니다(실습 재료가 든 잔은 판매 불가).";
                case PracticeStep.Gin: return IngredientStepSub(DrinkIngredients.Gin, 1);
                case PracticeStep.Campari: return IngredientStepSub(DrinkIngredients.Campari, 2);
                case PracticeStep.Vermouth: return IngredientStepSub(DrinkIngredients.SweetVermouth, 3);
                case PracticeStep.Technique: return $"오른쪽 영수증 '제조 방법'에서 {DrinkRules.TechniqueName(DrinkTechnique.Build)}를 고르세요 — 잔에 재료를 바로 쌓는 방법입니다. 얼음은 무료 보조 자원(지금 {draft.ice}개).";
                case PracticeStep.Craft: return "영수증 아래 '제조 확정'을 누르면 창고에서 재료가 차감되고 준비 음료 1잔이 됩니다. 이름을 적고 '레시피 저장'을 누르면 '레시피' 탭에서 다시 불러올 수 있습니다.";
                case PracticeStep.Done: return "영업을 시작하면 이 잔을 마시고 버프를 받습니다. 한 잔은 마시기와 '주문·판매' 중 하나에만 쓰입니다(실습 재료가 든 잔은 판매 불가). '비우기'로 새 배합을 시작하세요.";
            }
            return "";
        }

        static string PracticeLine(PracticeStep s) => s switch
        {
            PracticeStep.Intro => "음료 제조 실습 · 네그로니 한 잔",
            PracticeStep.Gin => "실습 1/5 · 진 30mL",
            PracticeStep.Campari => "실습 2/5 · 캄파리 30mL",
            PracticeStep.Vermouth => "실습 3/5 · 스위트 베르무트 30mL",
            PracticeStep.Technique => "실습 4/5 · 제조 방법: 빌드",
            PracticeStep.Craft => "실습 5/5 · 제조 확정",
            PracticeStep.Done => "완성! 준비 음료로 보관했습니다",
            _ => "",
        };

        void RenderPractice()
        {
            RemovePracticeBubble();
            if (practiceStep == PracticeStep.None || body == null || tab != Tab.Craft) return;
            // 제조대 위쪽 장식 띠(병·지거보다 위) — 병 끌기·지거·잔을 가리지 않는다.
            Action primary; string primaryLabel; Action skip = SkipPractice; string skipLabel = "건너뛰기";
            switch (practiceStep)
            {
                case PracticeStep.Intro:
                    primaryLabel = DrinkLab.Prepared != null ? "닫기" : "시작";
                    primary = DrinkLab.Prepared != null ? (Action)(() => { practiceStep = PracticeStep.None; RemovePracticeBubble(); }) : BeginPracticeSteps;
                    break;
                case PracticeStep.Done:
                    primaryLabel = "완료"; primary = SkipPractice; skip = null; break;
                default:
                    primaryLabel = "그만두기"; primary = SkipPractice; skip = null; break;
            }
            practiceBubble = NotebookGuide.Bubble(body, "PracticeGuide", new Vector2(0f, 1f), new Vector2(460f, -10f), new Vector2(800f, 150f),
                PracticeLine(practiceStep), PracticeSub(practiceStep), primaryLabel, primary, skipLabel, skip);
            // 좁은 말풍선: 버튼을 오른쪽 아래로 작게
            foreach (var n in new[] { "Primary", "Skip" })
            {
                var b = practiceBubble.Find(n) as RectTransform;
                if (b == null) continue;
                b.sizeDelta = new Vector2(150f, 52f);
                b.anchorMin = b.anchorMax = b.pivot = new Vector2(1f, 0f);
                b.anchoredPosition = new Vector2(n == "Primary" ? -(skip != null ? 176f : 16f) : -16f, 12f);
            }
            var line = practiceBubble.Find("Line") as RectTransform; if (line != null) line.sizeDelta = new Vector2(760f, 40f);
            var sub = practiceBubble.Find("Sub") as RectTransform;
            if (sub != null) { sub.sizeDelta = new Vector2(760f - 340f, 86f); var t = sub.GetComponent<Text>(); t.fontSize = DrinkPaperUi.Small - 2; t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = DrinkPaperUi.Small - 2; }
            if (sub != null) sub.sizeDelta = new Vector2(760f - (skip != null ? 340f : 180f), 92f);
        }

        void RemovePracticeBubble()
        {
            if (practiceBubble != null) { practiceBubble.gameObject.SetActive(false); Destroy(practiceBubble.gameObject); }
            practiceBubble = null;
        }

        /// <summary>차·커피 화면 첫 진입 안내(1회, 실제 기능 설명 — 지급 없음).</summary>
        void ShowKindGuide()
        {
            string line = kind == DrinkKind.Tea ? "차 우리기 안내" : "커피 추출 안내";
            string sub = kind == DrinkKind.Tea
                ? $"찻잎을 고르고 +2g·+3g·+5g 버튼(또는 통 끌기)으로 다하에 덜면 다관으로 옮겨집니다(다하 {DrinkRules.Format(DrinkRules.ChahaCapacity)}g, 찻잎 최대 {DrinkRules.Format(DrinkRules.TeaLeafMax)}g). 물은 무료이고 첨가 액체는 {DrinkRules.Format(DrinkRules.TeaAdditiveMax)}mL까지, 제조 방법은 우리기입니다."
                : $"원두를 고르고 +2g·+5g·+10g 버튼(또는 통 끌기)으로 스쿱에 덜어 추출 도구에 넣습니다. 핸드 드립·에스프레소·모카포트를 골라 추출량을 조절하며, 도구마다 전투 운용이 다릅니다(영수증 '전투 운용' 안내).";
            int k = (int)kind;
            RectTransform bubble = null;
            bubble = NotebookGuide.Bubble(body, "KindGuide", new Vector2(0f, 1f), new Vector2(460f, -10f), new Vector2(800f, 150f), line, sub,
                "확인", () => { SettingsService.MarkKindGuideSeen(k); if (bubble != null) Destroy(bubble.gameObject); }, null, null);
            var b = bubble.Find("Primary") as RectTransform;
            if (b != null) { b.sizeDelta = new Vector2(150f, 52f); b.anchorMin = b.anchorMax = b.pivot = new Vector2(1f, 0f); b.anchoredPosition = new Vector2(-16f, 12f); }
            var s = bubble.Find("Sub") as RectTransform;
            if (s != null) { s.sizeDelta = new Vector2(580f, 92f); var t = s.GetComponent<Text>(); t.fontSize = DrinkPaperUi.Small - 2; t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = DrinkPaperUi.Small - 2; }
        }

        public bool KindGuideVisible => body != null && body.Find("KindGuide") != null;
    }
}
