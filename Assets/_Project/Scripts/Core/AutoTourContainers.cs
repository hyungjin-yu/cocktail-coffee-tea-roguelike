#if CCT_QA_TOUR
using System.Collections;
using System.Linq;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-17 QA 전용(CCT_QA_TOUR) "containers" 모드 — 산지 원두 제조 통(dl_tin_&lt;산지&gt;) 반입 검증.
    /// 산지 원두마다: 재료 줄 선택 → 통 그림·앵커(grip 피벗·pourLip) 확인 → 실제 단위 붓기(20g, 실제 스쿱 연출) 중 입구 위치·통/스쿱 간격 기록
    /// → 배합에 원두 1회 증가 → 비우기(취소)로 배합만 0, 재고는 제조 전 차감 없음. 3종은 붓는 중간을 캡처, 전 종은 로그.
    /// 강제 경로(보고 표기): 격리 저장에 산지 원두 각 40g 지급(발견·보유), 실습 말풍선은 실제 "그만두기" 버튼.
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator ContainersRun()
        {
            var args = System.Environment.GetCommandLineArgs();
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string H = $"{Screen.width}x{Screen.height}";
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            if (title == null) { Fail("containers: no title"); yield break; }
            title.RequestStart();
            CharacterSelectView select = null;
            for (int k = 0; k < 20 && select == null; k++) { yield return Wait(0.3f); select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Fail("containers: no lobby"); yield break; }
            select.DrinkLabButton.onClick.Invoke();
            DrinkLabView lab = null;
            for (int k = 0; k < 20 && lab == null; k++) { yield return Wait(0.3f); lab = FindFirstObjectByType<DrinkLabView>(); }
            if (lab == null) { Fail("containers: no lab"); yield break; }
            yield return Wait(0.5f);
            Click(lab.transform, "Skip"); yield return Wait(0.4f);
            var origins = DrinkIngredients.All.Where(d => d.IsCoffeeBean && d.id != DrinkIngredients.CoffeeBean).ToList();
            foreach (var d in origins) DrinkLab.Data.Add(d.id, 400);
            Click(lab.transform, "KindCoffee"); yield return Wait(0.5f);
            Click(lab.transform, "Primary"); yield return Wait(0.3f);
            lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine); yield return Wait(0.3f);
            int shots = 0, bad = 0;
            foreach (var d in origins)
            {
                Click(lab.transform, "Clear"); yield return Wait(0.25f);
                if (!Click(lab.transform, "Row_" + d.id)) { Fail("containers: no row " + d.id); continue; }
                yield return Wait(0.35f);
                var lip = DrinkArt.Anchor(d.bottleId, "pourLip"); var grip = DrinkArt.Anchor(d.bottleId, "grip"); var mouth = DrinkArt.Anchor(d.bottleId, "mouth");
                bool hasArt = DrinkArt.Has(d.bottleId);
                int stock0 = DrinkLab.Owned(d.id), dose0 = lab.Draft.AmountOf(d.id);
                lab.PourUnit(200);
                bool captured = false; float minGap = float.MaxValue; Vector2 lipAt = Vector2.zero;
                for (int k = 0; k < 80 && (lab.Stage.Pouring || lab.Stage.Transferring); k++)
                {
                    yield return Wait(0.1f);
                    if (lab.Stage.Pouring && lab.Stage.Tilt >= DrinkStageView.TiltStart) minGap = Mathf.Min(minGap, lab.Stage.CoffeeTinScoopGap); // 기존 시험과 같은 구간(기울여 덜 때)만
                    if (lab.Stage.Geometry.lip != Vector2.zero) lipAt = lab.Stage.Geometry.lip;
                    if (!captured && shots < 3 && k == 4) { captured = true; shots++; yield return Shot($"ct_{H}_{shots:00}_{d.id}_pouring"); }
                }
                yield return Wait(0.2f);
                int dose1 = lab.Draft.AmountOf(d.id), stock1 = DrinkLab.Owned(d.id);
                bool ok = hasArt && dose1 - dose0 == 200 && stock1 == stock0 && minGap >= DrinkStageView.CoffeeTinMinGap - 0.5f; // PlayMode 시험과 같은 허용(코드가 통을 최소 간격에 정확히 맞춰 부동소수 오차)
                if (!ok) bad++;
                Line($"CT {d.id}: art={hasArt} anchors lip=({lip.x:0.000},{lip.y:0.000}) grip=({grip.x:0.000},{grip.y:0.000}) mouth=({mouth.x:0.000},{mouth.y:0.000}) dose {dose0}->{dose1} stock {stock0}->{stock1} minTinScoopGap={minGap:0.0}px lipAt=({lipAt.x:0},{lipAt.y:0}) ok={ok}");
                Click(lab.transform, "Clear"); yield return Wait(0.25f);
                Line($"CT {d.id}: after clear dose={lab.Draft.AmountOf(d.id)} stock={DrinkLab.Owned(d.id)} (cancel = 배합만 비움, 재고 불변)");
                if (lab.Draft.AmountOf(d.id) != 0 || DrinkLab.Owned(d.id) != stock0) { bad++; Fail($"containers: clear changed stock {d.id}"); }
            }
            Line($"CT summary origins={origins.Count} bad={bad}");
            if (bad > 0) Fail($"containers: {bad} origin checks failed");
            lab.SetTab(DrinkLabView.Tab.Codex); yield return Wait(0.4f);
            lab.SetCodexKind(DrinkLabView.CodexKind.Coffee); yield return Wait(0.3f);
            yield return Shot($"ct_{H}_10_codex_coffee_owned");
        }
    }
}
#endif
