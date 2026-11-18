#if CCT_QA_TOUR
using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-17 QA 전용(CCT_QA_TOUR) "americano" 모드 — 아메리카노 액면창 머그(dl_coffee_americano_mug) 반입 검증.
    /// 빈 잔 → 에스프레소만 → 뜨거운 물 + 버튼 단계(상한 200mL, 상한에서 한 번 더 눌러도 그대로) → − 한 번 → 비우기(빈 잔) → 다시 채워 제조.
    /// 단계마다 액면 높이(잔 액체 창 0~1)·총 부피/용량·채움 상한 82%·거품 띠 없음·임시 잔 문구 여부를 기록하고 캡처.
    /// 강제 경로(보고 표기): 격리 저장에 원두 지급(시작 지급량 부족), 실습 말풍선은 실제 "그만두기" 버튼.
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator AmericanoRun()
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
            if (title == null) { Fail("americano: no title"); yield break; }
            title.RequestStart();
            CharacterSelectView select = null;
            for (int k = 0; k < 20 && select == null; k++) { yield return Wait(0.3f); select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Fail("americano: no lobby"); yield break; }
            select.DrinkLabButton.onClick.Invoke();
            DrinkLabView lab = null;
            for (int k = 0; k < 20 && lab == null; k++) { yield return Wait(0.3f); lab = FindFirstObjectByType<DrinkLabView>(); }
            if (lab == null) { Fail("americano: no lab"); yield break; }
            yield return Wait(0.5f);
            Click(lab.transform, "Skip"); yield return Wait(0.4f); // 실습 말풍선 그만두기(실제 버튼)
            DrinkLab.Data.Add(DrinkIngredients.CoffeeBean, 1000);
            Click(lab.transform, "KindCoffee"); yield return Wait(0.5f);
            Click(lab.transform, "Primary"); yield return Wait(0.3f); // 커피 첫 진입 안내 확인(있으면)
            lab.SetCoffeeMethod(CoffeeBrewMethod.EspressoMachine); yield return Wait(0.3f);
            Click(lab.transform, "Clear"); yield return Wait(0.3f);
            lab.SetCoffeeStyle(CoffeeStyle.Americano); yield return Wait(0.4f);
            int n = 1;
            yield return Step(lab, H, n++, "empty");
            Click(lab.transform, "Row_" + DrinkIngredients.CoffeeBean); yield return Wait(0.3f);
            foreach (var u in new[] { 100, 50, 20, 20 }) { lab.PourUnit(u); for (int k = 0; k < 80 && (lab.Stage.Pouring || lab.Stage.Transferring); k++) yield return Wait(0.1f); yield return Wait(0.15f); }
            lab.SetCoffeeStyle(CoffeeStyle.Americano); yield return Wait(0.3f);
            yield return Step(lab, H, n++, "espresso_only");
            for (int press = 1; press <= 3; press++) { Click(lab.transform, "HotWaterPlus"); yield return Wait(0.35f); }
            yield return Step(lab, H, n++, "hot_water_mid");
            // 기본 120mL에서 시작 — 상한(HotWaterMax 200mL)까지 + 를 누른다(변화가 멈출 때까지, 최대 12회).
            for (int press = 0; press < 12 && lab.Draft.hotWater < CoffeeStyles.HotWaterMax(CoffeeStyle.Americano); press++) { Click(lab.transform, "HotWaterPlus"); yield return Wait(0.3f); }
            yield return Step(lab, H, n++, "hot_water_max");
            int atMax = lab.Draft.hotWater;
            Click(lab.transform, "HotWaterPlus"); yield return Wait(0.35f);
            Line($"AM press past max: hotWater {atMax}->{lab.Draft.hotWater} (unchanged={atMax == lab.Draft.hotWater})");
            if (atMax != lab.Draft.hotWater) Fail("americano: hot water exceeded cap");
            Click(lab.transform, "HotWaterMinus"); yield return Wait(0.35f);
            yield return Step(lab, H, n++, "hot_water_minus");
            Click(lab.transform, "Clear"); yield return Wait(0.4f);
            lab.SetCoffeeStyle(CoffeeStyle.Americano); yield return Wait(0.3f);
            yield return Step(lab, H, n++, "cleared_empty");
            // 다시 채워 실제 제조(원두 차감 1회)
            Click(lab.transform, "Row_" + DrinkIngredients.CoffeeBean); yield return Wait(0.3f);
            foreach (var u in new[] { 100, 50, 20, 20 }) { lab.PourUnit(u); for (int k = 0; k < 80 && (lab.Stage.Pouring || lab.Stage.Transferring); k++) yield return Wait(0.1f); yield return Wait(0.15f); }
            lab.SetCoffeeStyle(CoffeeStyle.Americano); yield return Wait(0.3f);
            for (int press = 0; press < 6; press++) { Click(lab.transform, "HotWaterPlus"); yield return Wait(0.3f); }
            int beans0 = DrinkLab.Owned(DrinkIngredients.CoffeeBean);
            lab.ConfirmCraft();
            // 제조 연출 전체(추출 근접 화면 → 잔으로 돌아와 물·커피가 채워지는 구간)를 0.5초마다 기록·캡처(최대 20장).
            for (int k = 0, shotN = 0; k < 120 && lab.Stage.SequencePlaying; k++)
            {
                yield return Wait(0.1f);
                var sv = lab.Stage.ServeVessel;
                if (k % 5 == 4 && shotN < 20 && sv != null)
                {
                    Line($"AM craft t={k * 0.1f:0.0}s serve={sv.AssetId} level01={sv.Level01:0.000} surfaceY={sv.SurfaceY:0.000}");
                    yield return Shot($"am_{H}_{n:00}_craft_{shotN++}");
                }
            }
            n++;
            yield return Wait(0.5f);
            var prep = DrinkLab.Prepared;
            Line($"AM crafted style={(prep != null ? prep.recipe.coffeeStyle.ToString() : "null")} beans {beans0}->{DrinkLab.Owned(DrinkIngredients.CoffeeBean)} hotWater={(prep != null ? prep.recipe.hotWater : -1)}");
            if (prep == null || prep.recipe.coffeeStyle != (int)CoffeeStyle.Americano) Fail("americano: craft failed");
            yield return Shot($"am_{H}_{n++:00}_crafted");
        }

        IEnumerator Step(DrinkLabView lab, string H, int n, string tag)
        {
            var d = lab.Draft;
            var serve = lab.Stage.ServeVessel;
            float cap = DrinkRules.ServeCapacityMl(d), total = CoffeeStyles.TotalVolumeMl(d);
            var lr = serve != null ? DrinkArt.LiquidRect(serve.AssetId) : default;
            float level = serve != null ? serve.Level01 : -1f;
            bool temp = CoffeeStyles.UsesTempArt(CoffeeStyle.Americano, CoffeeBrewMethod.EspressoMachine);
            Line($"AM {tag}: serve={serve?.AssetId} temp={temp} hotWater={d.hotWater} total={total:0.0}mL cap={cap:0} fill={(cap > 0 ? total / cap : 0f):0.000} limit={DrinkRules.ServeFillMax:0.00} level01={level:0.000} surfaceY={(serve != null ? serve.SurfaceY : -1f):0.000} liquidRect=({lr.yMin:0.000}..{lr.yMax:0.000}) foamBand={lab.Stage.FoamBandVisible} valid={DrinkValidator.Check(d).Ok}");
            if (serve == null || serve.AssetId != "dl_coffee_americano_mug") Fail($"americano {tag}: serve {serve?.AssetId}");
            if (cap > 0 && total / cap > DrinkRules.ServeFillMax + 1e-3f) Fail($"americano {tag}: fill over limit");
            if (lab.Stage.FoamBandVisible) Fail($"americano {tag}: foam band visible");
            lab.RevealMixRow("Style_"); yield return Wait(0.2f);
            yield return Shot($"am_{H}_{n:00}_{tag}");
        }
    }
}
#endif
