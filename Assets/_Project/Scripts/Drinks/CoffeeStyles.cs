namespace CCT
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 4: 커피 완성 음료(게임 내부 분류). 추출 방식(CoffeeBrewMethod)과 별개로, 에스프레소 머신 추출 위에
    /// 스팀 우유·뜨거운 물·거품 단계를 더해 만든다. 값은 Delivery/Reports/feedback-review-19-25-26-and-4-2026-11-15.md 표와 같다.
    /// 현실의 단일 정답 배합이 아니다(화면에도 "게임 분류"). 새 전투 효과 없음 — 추출 운용과 재료(스팀 우유) 효과만.
    /// 저장 정수 값: 0 기본, 1 아메리카노, 2 카페라떼, 3 플랫화이트, 4 카푸치노(순서를 바꾸지 않는다).
    /// </summary>
    public enum CoffeeStyle { None = 0, Americano = 1, Latte = 2, FlatWhite = 3, Cappuccino = 4 }

    public static class CoffeeStyles
    {
        public static readonly CoffeeStyle[] All = { CoffeeStyle.None, CoffeeStyle.Americano, CoffeeStyle.Latte, CoffeeStyle.FlatWhite, CoffeeStyle.Cappuccino };
        public const string ClassNote = "게임 분류";

        public static bool IsDefined(int s) => s >= 0 && s <= 4;

        public static string Name(CoffeeStyle s) => s switch
        {
            CoffeeStyle.Americano => "아메리카노",
            CoffeeStyle.Latte => "카페라떼",
            CoffeeStyle.FlatWhite => "플랫화이트",
            CoffeeStyle.Cappuccino => "카푸치노",
            _ => "기본",
        };

        /// <summary>스팀 우유 범위(0.1mL). 기본은 (0, 기존 CoffeeAdditiveMax).</summary>
        public static int MilkMin(CoffeeStyle s) => s switch { CoffeeStyle.Latte => 1500, CoffeeStyle.FlatWhite => 800, CoffeeStyle.Cappuccino => 600, _ => 0 };
        public static int MilkMax(CoffeeStyle s) => s switch { CoffeeStyle.Americano => 0, CoffeeStyle.Latte => 2000, CoffeeStyle.FlatWhite => 1200, CoffeeStyle.Cappuccino => 1000, _ => DrinkRules.CoffeeAdditiveMax };

        /// <summary>뜨거운 물(무료, 0.1mL) — 아메리카노만.</summary>
        public static int HotWaterMin(CoffeeStyle s) => s == CoffeeStyle.Americano ? 1200 : 0;
        public static int HotWaterMax(CoffeeStyle s) => s == CoffeeStyle.Americano ? 2000 : 0;
        public const int HotWaterStep = 100;

        /// <summary>필요한 거품 단계(0 없음, 1 얇은, 2 두꺼운)와 거품 부피 비율(우유 대비), 거품 내기 시간(초).</summary>
        public static int FoamLevel(CoffeeStyle s) => s switch { CoffeeStyle.Latte => 1, CoffeeStyle.FlatWhite => 1, CoffeeStyle.Cappuccino => 2, _ => 0 };
        public static float FoamRatio(int level) => level >= 2 ? 0.5f : level == 1 ? 0.1f : 0f;
        public static float FoamSeconds(int level) => level >= 2 ? 2.0f : level == 1 ? 1.2f : 0f;

        /// <summary>완성 잔 용량(mL). 기본은 추출 도구 잔.</summary>
        public static float CupCapacityMl(CoffeeStyle s, CoffeeBrewMethod m) => s switch
        {
            CoffeeStyle.Americano => 380f,
            CoffeeStyle.Latte => 340f,
            CoffeeStyle.FlatWhite => 220f,
            CoffeeStyle.Cappuccino => 240f,
            _ => CoffeeBrewing.CupCapacityMl(m),
        };

        /// <summary>목표 잔 아트 ID(신규 아트) — 아직 없으면 TempCupArt로 대체(개발용 임시, 시각 합격 아님).</summary>
        public static string CupArt(CoffeeStyle s, CoffeeBrewMethod m) => s switch
        {
            // 2026-11-17: 기존 머그는 몸통이 불투명(알파 255)이라 액체가 보이지 않는다 → 액면창 머그(Codex 요청) 전까지 기존 머그 임시.
            CoffeeStyle.Americano => "dl_coffee_americano_mug",
            CoffeeStyle.Latte => "dl_coffee_latte_glass",
            CoffeeStyle.FlatWhite => "dl_coffee_flatwhite_cup",
            CoffeeStyle.Cappuccino => "dl_coffee_cappuccino_cup",
            _ => CoffeeBrewing.CupArt(m),
        };

        public static string TempCupArt(CoffeeStyle s, CoffeeBrewMethod m) => s switch
        {
            CoffeeStyle.Americano => "dl_coffee_mug",
            CoffeeStyle.Latte => "dl_coffee_mug",
            CoffeeStyle.FlatWhite => "dl_coffee_cup",
            CoffeeStyle.Cappuccino => "dl_coffee_cup",
            _ => CupArt(s, m),
        };

        /// <summary>실제로 쓸 잔 아트(신규 아트가 임포트돼 있으면 그것, 없으면 임시).</summary>
        public static string ResolvedCupArt(CoffeeStyle s, CoffeeBrewMethod m)
        {
            var want = CupArt(s, m);
            return DrinkArt.Sprite(want) != null ? want : TempCupArt(s, m);
        }

        public static bool UsesTempArt(CoffeeStyle s, CoffeeBrewMethod m) => ResolvedCupArt(s, m) != CupArt(s, m);

        public static string Description(CoffeeStyle s) => s switch
        {
            CoffeeStyle.Americano => "에스프레소 + 뜨거운 물 120~200mL",
            CoffeeStyle.Latte => "에스프레소 + 스팀 우유 150~200mL + 얇은 거품",
            CoffeeStyle.FlatWhite => "에스프레소 + 스팀 우유 80~120mL + 얇은 거품",
            CoffeeStyle.Cappuccino => "에스프레소 + 스팀 우유 60~100mL + 두꺼운 거품",
            _ => "추출 그대로",
        };

        public static CoffeeStyle Of(DrinkRecipe r) => r != null && r.Kind == DrinkKind.Coffee && IsDefined(r.coffeeStyle) ? (CoffeeStyle)r.coffeeStyle : CoffeeStyle.None;

        /// <summary>잔에 들어가는 전체 부피(mL): 추출 + 첨가 액체 + 뜨거운 물 + 거품.</summary>
        public static float TotalVolumeMl(DrinkRecipe r)
        {
            if (r == null) return 0f;
            int milk = r.AmountOf(DrinkIngredients.SteamedMilk);
            return (r.water + r.LiquidTotal() + r.hotWater + milk * FoamRatio(r.foam)) / 10f;
        }
    }
}
