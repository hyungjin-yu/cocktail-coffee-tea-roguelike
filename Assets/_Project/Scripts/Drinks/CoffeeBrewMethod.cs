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

        public static bool IsDefined(CoffeeBrewMethod method) => Enum.IsDefined(typeof(CoffeeBrewMethod), method);

        public static float Strength01(CoffeeBrewMethod method, int doseTenths, int waterTenths)
        {
            float ratio = waterTenths <= 0 ? float.PositiveInfinity : (float)waterTenths / Mathf.Max(1, doseTenths);
            float target = ExtractionRatio(method);
            float tolerance = method == CoffeeBrewMethod.EspressoMachine ? 1.1f : method == CoffeeBrewMethod.MokaPot ? 4f : 7f;
            return target <= 0f ? 0f : Mathf.Clamp01(1f - Mathf.Abs(ratio - target) / tolerance);
        }
    }
}
