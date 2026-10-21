using System;
using UnityEngine;

namespace CCT
{
    /// <summary>Distinct coffee extraction paths. The numeric values are save/share-code stable.</summary>
    public enum CoffeeBrewMethod { EspressoMachine = 1, PourOver = 2, MokaPot = 3 }

    /// <summary>Method-specific extraction rules used by coffee recipes and their live previews.</summary>
    public static class CoffeeBrewing
    {
        public static int WaterMinTenths(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => 250,
            CoffeeBrewMethod.PourOver => 1800,
            CoffeeBrewMethod.MokaPot => 1200,
            _ => 0,
        };

        public static int WaterMaxTenths(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => 450,
            CoffeeBrewMethod.PourOver => 3000,
            CoffeeBrewMethod.MokaPot => 2200,
            _ => 0,
        };

        public static int DoseMinTenths(CoffeeBrewMethod method) => method == CoffeeBrewMethod.EspressoMachine ? 120 : 80;
        public static int DoseMaxTenths(CoffeeBrewMethod method) => method == CoffeeBrewMethod.EspressoMachine ? 240 : 180;

        public static float ExtractionRatio(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => 2.0f,
            CoffeeBrewMethod.PourOver => 15.0f,
            CoffeeBrewMethod.MokaPot => 8.0f,
            _ => 0f,
        };

        public static string DisplayName(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => "에스프레소 머신",
            CoffeeBrewMethod.PourOver => "핸드 드립",
            CoffeeBrewMethod.MokaPot => "모카포트",
            _ => "알 수 없는 추출법",
        };

        /// <summary>좁은 제조법 선택 버튼에 쓰는 한 줄 이름. 상세 명칭은 DisplayName을 사용한다.</summary>
        public static string ButtonName(CoffeeBrewMethod method) => method == CoffeeBrewMethod.EspressoMachine ? "에스프레소" : DisplayName(method);

        public static bool IsDefined(CoffeeBrewMethod method) => Enum.IsDefined(typeof(CoffeeBrewMethod), method);

        /// <summary>제조 화면의 도구 선택 순서.</summary>
        public static readonly CoffeeBrewMethod[] All = { CoffeeBrewMethod.EspressoMachine, CoffeeBrewMethod.PourOver, CoffeeBrewMethod.MokaPot };

        /// <summary>도구를 고를 때 채우는 기본 물(추출량, 0.1mL). 목표 비율 근처의 표준 한 잔.</summary>
        public static int DefaultWaterTenths(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => 360,
            CoffeeBrewMethod.PourOver => 2400,
            CoffeeBrewMethod.MokaPot => 1600,
            _ => 0,
        };

        /// <summary>물 조절 한 번의 단계(0.1mL).</summary>
        public static int WaterStepTenths(CoffeeBrewMethod method) => method == CoffeeBrewMethod.EspressoMachine ? 20 : 100;

        /// <summary>완성 잔 용량(mL, 고정). 최대 추출량에서도 잔의 약 64~79%만 찬다(DrinkRules.ServeFillMax 0.82 이내).</summary>
        public static float CupCapacityMl(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => 70f,
            CoffeeBrewMethod.MokaPot => 280f,
            _ => 380f,
        };

        /// <summary>도구 본체 아트(제조대 가운데). 핸드 드립은 서버(유리 주전자) 위에 드리퍼(DripperArt)를 얹는다.</summary>
        public static string ApparatusArt(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => "dl_coffee_espresso",
            CoffeeBrewMethod.MokaPot => "dl_coffee_moka",
            _ => "dl_coffee_carafe",
        };

        public const string DripperArt = "dl_coffee_dripper", ScoopArt = "dl_coffee_scoop", FlameArt = "dl_coffee_flame", PortafilterArt = "dl_coffee_portafilter";

        /// <summary>완성 잔 아트.</summary>
        public static string CupArt(CoffeeBrewMethod method) => method == CoffeeBrewMethod.EspressoMachine ? "dl_coffee_demitasse" : method == CoffeeBrewMethod.MokaPot ? "dl_coffee_cup" : "dl_coffee_mug";

        /// <summary>제조대 안내용 짧은 설명.</summary>
        public static string ShortDescription(CoffeeBrewMethod method) => method switch
        {
            CoffeeBrewMethod.EspressoMachine => "고압으로 짧게 — 원두 12~24g, 25~45mL",
            CoffeeBrewMethod.PourOver => "드리퍼에 천천히 물 붓기 — 원두 8~18g, 180~300mL",
            CoffeeBrewMethod.MokaPot => "불 위에서 증기압으로 — 원두 8~18g, 120~220mL",
            _ => "",
        };

        /// <summary>
        /// 추출 커피 색(sRGB). 농도 = 원두 g / 추출 mL가 진할수록 어둡고(에스프레소 ≈ 0.5) 묽을수록 맑은 갈색(핸드 드립 ≈ 0.06).
        /// 화면 표시 전용(장부의 커피 성분 색) — 버프·저장에는 쓰지 않는다.
        /// </summary>
        public static Color BrewColor(CoffeeBrewMethod method, int doseTenths, int waterTenths)
        {
            float conc = waterTenths <= 0 ? 0.5f : Mathf.Max(0, doseTenths) / (float)waterTenths;
            float t = Mathf.Sqrt(Mathf.Clamp01((conc - 0.03f) / 0.47f));
            var light = new Color(0.64f, 0.38f, 0.18f, 1f);
            var dark = new Color(0.17f, 0.09f, 0.045f, 1f);
            var c = Color.Lerp(light, dark, t);
            if (method == CoffeeBrewMethod.MokaPot) c = Color.Lerp(c, dark, 0.15f); // 모카포트는 같은 농도에서도 더 묵직하다
            return c;
        }

        public static Color BrewColor(DrinkRecipe r) => r == null ? Color.clear : BrewColor(r.coffeeMethod, r.CoffeeDose(), r.water); // 기본·산지 원두 합

        public static float Strength01(CoffeeBrewMethod method, int doseTenths, int waterTenths)
        {
            float ratio = waterTenths <= 0 ? float.PositiveInfinity : (float)waterTenths / Mathf.Max(1, doseTenths);
            float target = ExtractionRatio(method);
            float tolerance = method == CoffeeBrewMethod.EspressoMachine ? 1.1f : method == CoffeeBrewMethod.MokaPot ? 4f : 7f;
            return target <= 0f ? 0f : Mathf.Clamp01(1f - Mathf.Abs(ratio - target) / tolerance);
        }
    }
}
