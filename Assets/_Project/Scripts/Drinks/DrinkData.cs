using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>69(2026-10-19) 음료 종류. 커피(4)는 추출 도구(CoffeeBrewMethod)를 고르는 세 번째 종류 — 값이 DrinkUse 비트와 같다(저장·공유 코드 값).</summary>
    public enum DrinkKind { Cocktail = 1, Tea = 2, Coffee = 4 }

    /// <summary>재료를 쓸 수 있는 음료 종류(비트 합). 공용 = Cocktail|Tea. 커피 = 4.</summary>
    [Flags]
    public enum DrinkUse { Cocktail = 1, Tea = 2, Both = 3, Coffee = 4 }

    /// <summary>69 제조 기법. 저장·공유 코드에는 이 정수 값을 쓴다(순서를 바꾸지 않는다). Brew = 커피 추출(도구는 레시피의 method).</summary>
    public enum DrinkTechnique { Shake = 1, Stir = 2, Build = 3, Layer = 4, Steep = 5, Brew = 6 }

    /// <summary>69 음료 버프 효과. 저장/공유 데이터에는 쓰지 않는다(항상 현재 밸런스로 재계산). Range = 커피 전용 집중(조준) 사거리.</summary>
    public enum DrinkStat { Attack, AttackSpeed, CritChance, MaxHp, CooldownReduction, MoveSpeed, Range }

    /// <summary>재료 형태. 액체는 mL, 찻잎·원두(마른 고형 재료)는 g. 내부 저장은 둘 다 정수 1 = 0.1단위.</summary>
    public enum IngredientForm { Liquid, Leaf }

    /// <summary>69 실제 재료 정의(도감·계량·색·버프 데이터). 수치는 임시 기본값이며 구현 보고서에 표로 남긴다.</summary>
    public sealed class DrinkIngredientDef
    {
        public string id;
        public string name;
        public DrinkUse use;
        public IngredientForm form;
        /// <summary>칵테일 베이스 술(칵테일은 1종 이상 필요).</summary>
        public bool baseSpirit;
        /// <summary>탄산 재료(후속). 밀폐 셰이킹 금지·마지막 투입 규칙 대상.</summary>
        public bool carbonated;
        /// <summary>sRGB 기본색. 찻잎은 잎 색(그림)이고 우림 색은 brewColor.</summary>
        public Color color;
        /// <summary>찻잎 우림 색(sRGB). 액체는 사용하지 않는다.</summary>
        public Color brewColor;
        /// <summary>착색력(흡광 배율). 무색 술/물은 0에 가깝고 강한 시럽·리큐어는 크다.</summary>
        public float tint;
        /// <summary>탁도 0~1(빛 산란). 레몬즙이 대표적.</summary>
        public float turbidity;
        /// <summary>점성 0~1(붓기 속도·액면 물결에만 사용).</summary>
        public float viscosity;
        /// <summary>층 밀도(g/mL 근사). 레이어링 층 순서/경계.</summary>
        public float density;
        public DrinkStat effect;
        /// <summary>효과 상한 대비 최대 기여 비율(정규화 기여 1일 때). 1 미만이라 한 재료가 상한을 혼자 채우지 않는다.</summary>
        public float coefficient;
        public string taste;
        public string source;
        public string iconId;
        public string bottleId;

        public bool UsableIn(DrinkKind kind) => (use & UseBit(kind)) != 0;
        public string Unit => form == IngredientForm.Leaf ? "g" : "mL";
        public string UseLabel
        {
            get
            {
                var parts = new List<string>();
                if ((use & DrinkUse.Cocktail) != 0) parts.Add("칵테일");
                if ((use & DrinkUse.Tea) != 0) parts.Add("차");
                if ((use & DrinkUse.Coffee) != 0) parts.Add("커피");
                return string.Join(" · ", parts);
            }
        }

        /// <summary>음료 종류 → 사용 비트. 알 수 없는 종류는 0(어떤 재료도 쓸 수 없음).</summary>
        public static DrinkUse UseBit(DrinkKind kind) => kind switch
        {
            DrinkKind.Cocktail => DrinkUse.Cocktail,
            DrinkKind.Tea => DrinkUse.Tea,
            DrinkKind.Coffee => DrinkUse.Coffee,
            _ => 0,
        };
    }

    /// <summary>69 재료 표. ID는 저장·공유 코드에 쓰이므로 바꾸지 않는다. 공유 코드 색인은 ShareIndex 순서.</summary>
    public static class DrinkIngredients
    {
        public const string Gin = "gin", Rum = "rum", BlueCuracao = "blue_curacao", Grenadine = "grenadine",
            BlackTea = "black_tea", GreenTea = "green_tea", LemonJuice = "lemon_juice", Honey = "honey", CoffeeBean = "coffee_bean", SteamedMilk = "steamed_milk";

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        public static readonly IReadOnlyList<DrinkIngredientDef> All = new List<DrinkIngredientDef>
        {
            new DrinkIngredientDef
            {
                id = Gin, name = "진", use = DrinkUse.Cocktail, form = IngredientForm.Liquid, baseSpirit = true,
                color = C(246, 249, 252), tint = 0.02f, turbidity = 0f, viscosity = 0.1f, density = 0.95f,
                effect = DrinkStat.Attack, coefficient = 0.75f,
                taste = "주니퍼 베리의 송진 향과 시트러스 껍질 향. 무색 투명해 다른 색을 흐리게 만든다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_gin", bottleId = "dl_bottle_gin",
            },
            new DrinkIngredientDef
            {
                id = Rum, name = "럼", use = DrinkUse.Cocktail, form = IngredientForm.Liquid, baseSpirit = true,
                color = C(214, 160, 82), tint = 0.35f, turbidity = 0f, viscosity = 0.15f, density = 0.95f,
                effect = DrinkStat.AttackSpeed, coefficient = 0.8f,
                taste = "사탕수수로 만든 술. 바닐라·캐러멜 향의 옅은 호박색.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_rum", bottleId = "dl_bottle_rum",
            },
            new DrinkIngredientDef
            {
                id = BlueCuracao, name = "블루 큐라소", use = DrinkUse.Cocktail, form = IngredientForm.Liquid,
                color = C(22, 120, 236), tint = 3.2f, turbidity = 0f, viscosity = 0.35f, density = 1.08f,
                effect = DrinkStat.CritChance, coefficient = 0.8f,
                taste = "오렌지 껍질 향의 달콤한 리큐어. 착색력이 높아 조금만 넣어도 푸르게 물든다.",
                source = "희귀 착색 재료: 보스 희귀 보상 후보(두 종 중 추첨) · 일반·정예에서도 가끔", iconId = "dl_icon_blue_curacao", bottleId = "dl_bottle_blue_curacao",
            },
            new DrinkIngredientDef
            {
                id = Grenadine, name = "그레나딘 시럽", use = DrinkUse.Cocktail, form = IngredientForm.Liquid,
                color = C(206, 18, 52), tint = 3.6f, turbidity = 0.05f, viscosity = 0.7f, density = 1.30f,
                effect = DrinkStat.MaxHp, coefficient = 0.8f,
                taste = "석류 풍미의 진한 붉은 시럽. 무거워서 잔 바닥으로 가라앉는다.",
                source = "희귀 착색 재료: 보스 희귀 보상 후보(두 종 중 추첨) · 일반·정예에서도 가끔", iconId = "dl_icon_grenadine", bottleId = "dl_bottle_grenadine",
            },
            new DrinkIngredientDef
            {
                id = BlackTea, name = "홍차 찻잎", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(92, 58, 34), brewColor = C(170, 64, 30), tint = 1.6f, turbidity = 0.02f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.CooldownReduction, coefficient = 0.8f,
                taste = "완전 발효한 갈색 잎. 우리면 적갈색이 되고 몰트·꿀 향이 난다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_black_tea", bottleId = "dl_tin_black_tea",
            },
            new DrinkIngredientDef
            {
                id = GreenTea, name = "녹차 찻잎", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(70, 128, 52), brewColor = C(196, 204, 92), tint = 0.9f, turbidity = 0.04f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.MoveSpeed, coefficient = 0.8f,
                taste = "덖거나 쪄서 발효를 멈춘 녹색 잎. 우리면 맑은 황록색, 풋풋한 향.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_green_tea", bottleId = "dl_tin_green_tea",
            },
            new DrinkIngredientDef
            {
                id = LemonJuice, name = "레몬즙", use = DrinkUse.Both, form = IngredientForm.Liquid,
                color = C(246, 236, 150), tint = 0.25f, turbidity = 0.55f, viscosity = 0.1f, density = 1.03f,
                effect = DrinkStat.Attack, coefficient = 0.5f,
                taste = "갓 짠 레몬즙. 신맛이 강하고 과육 때문에 살짝 뿌옇다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_lemon_juice", bottleId = "dl_bottle_lemon_juice",
            },
            new DrinkIngredientDef
            {
                id = Honey, name = "꿀", use = DrinkUse.Both, form = IngredientForm.Liquid,
                color = C(232, 162, 32), tint = 1.1f, turbidity = 0.12f, viscosity = 1f, density = 1.40f,
                effect = DrinkStat.MaxHp, coefficient = 0.5f,
                taste = "금빛의 걸쭉한 꿀. 천천히 흘러내리고 단맛과 꽃 향이 난다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_honey", bottleId = "dl_bottle_honey",
            },
            new DrinkIngredientDef
            {
                // 커피 필수 재료. 원두 g(다하 대신 원두 스쿱으로 계량). 추출 색은 CoffeeBrewing.BrewColor(도구·원두·물)로 계산한다.
                id = CoffeeBean, name = "원두", use = DrinkUse.Coffee, form = IngredientForm.Leaf,
                color = C(84, 50, 30), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.Range, coefficient = 0.8f,
                taste = "볶은 커피콩. 추출 도구에 따라 진한 에스프레소, 맑은 핸드 드립, 묵직한 모카포트 커피가 된다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_coffee_bean", bottleId = "dl_tin_coffee_bean",
            },
            new DrinkIngredientDef
            {
                // 커피 선택 재료. 완성 잔에 먼저 담고 커피를 그 위로 내린다(도구 안에는 넣지 않는다). 뿌옇고 커피보다 무거워 아래에 깔린다.
                id = SteamedMilk, name = "스팀 우유", use = DrinkUse.Coffee, form = IngredientForm.Liquid,
                color = C(246, 240, 226), tint = 0.6f, turbidity = 0.9f, viscosity = 0.15f, density = 1.03f,
                effect = DrinkStat.Range, coefficient = 0.4f,
                taste = "스팀으로 데운 우유. 고운 거품이 올라오고 커피를 부드럽고 밝은 갈색으로 만든다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_steamed_milk", bottleId = "dl_bottle_steamed_milk",
            },
        };

        /// <summary>공유 코드 v1 재료 색인(0부터). 새 재료는 끝에만 추가한다.</summary>
        public static readonly string[] ShareIndex = { Gin, Rum, BlueCuracao, Grenadine, BlackTea, GreenTea, LemonJuice, Honey, CoffeeBean, SteamedMilk };

        static Dictionary<string, DrinkIngredientDef> byId;

        public static DrinkIngredientDef Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, DrinkIngredientDef>();
                foreach (var def in All) byId[def.id] = def;
            }
            return id != null && byId.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>물(무료 보조 자원) 표시색과 성질.</summary>
        public static readonly Color WaterColor = new Color(0.93f, 0.97f, 1f, 1f);
    }

    /// <summary>69 제조 규칙·한도(임시 기본값). 모든 양은 정수 1 = 0.1 단위.</summary>
    public static class DrinkRules
    {
        public const int TenthsPerUnit = 10;
        public const int CocktailLiquidMax = 1200;   // 120.0 mL (물 포함)
        public const int CocktailWaterMax = 300;      // 30.0 mL 희석수
        public const int TeaWaterFixed = 1500;        // 150.0 mL 고정
        public const int TeaAdditiveMax = 300;        // 첨가 액체 30.0 mL
        public const int TeaLeafMin = 1;              // 0.1 g
        public const int TeaLeafMax = 50;             // 5.0 g
        public const int IceMax = 4;
        public const int JiggerCapacity = 450;        // 45.0 mL 더블 지거
        public const int ChahaCapacity = 30;          // 3.0 g 다하
        public const int CoffeeScoopCapacity = 100;   // 10.0 g 원두 스쿱
        public const int CoffeeAdditiveMax = 1000;    // 커피 첨가 액체(스팀 우유) 100.0 mL — 실제로는 잔 여유(ServeFillMax)가 먼저 막는다
        public const int RecipeLimit = 30;
        public const int RecipeNameMax = 16;
        /// <summary>한 배합 재료 종류 상한(초기 재료 8종).</summary>
        public const int MaxSteps = 8;
        /// <summary>한 줄 양 상한(어떤 한도보다 크다 — 이보다 크면 손상/조작 값으로 거부).</summary>
        public const int StepMax = 2000;
        /// <summary>재료별 창고 상한(0.1 단위, 넘침 방지).</summary>
        public const int StockMax = 10_000_000;
        /// <summary>정규화 기여 q/(q+k)의 k: 액체 15mL, 찻잎 2g.</summary>
        public const int LiquidK = 150, LeafK = 20;

        /// <summary>73: 완성 잔 용량(mL, 고정). 허용 최대 액체 + 최대 얼음(칵테일 120+40, 차 150+30)에서도 약 80~82%만 찬다.</summary>
        public const float ServeCocktailCapacityMl = 200f, ServeTeaCapacityMl = 220f;
        /// <summary>완성 잔에서 액체 + 얼음이 차지할 수 있는 최대 비율. 넘으면 몰래 줄이지 않고 제조를 거부·안내한다.</summary>
        public const float ServeFillMax = 0.82f;
        /// <summary>얼음 한 조각 부피(mL). DrinkVesselView.CubeVolumeMl와 같다.</summary>
        public const float IceCubeMl = 10f;

        public static float ServeCapacityMl(DrinkKind kind) => kind switch
        {
            DrinkKind.Tea => ServeTeaCapacityMl,
            DrinkKind.Coffee => CoffeeBrewing.CupCapacityMl(CoffeeBrewMethod.PourOver),
            _ => ServeCocktailCapacityMl,
        };

        /// <summary>완성 잔 용량(mL). 커피는 추출 도구마다 잔이 다르다(에스프레소 잔·머그).</summary>
        public static float ServeCapacityMl(DrinkRecipe r) =>
            r != null && r.Kind == DrinkKind.Coffee ? CoffeeBrewing.CupCapacityMl(r.coffeeMethod) : ServeCapacityMl(r != null ? r.Kind : DrinkKind.Cocktail);

        /// <summary>완성 잔에 들어갈 액체 + 얼음 부피(mL).</summary>
        public static float ServeFillMl(DrinkRecipe r) => r == null ? 0f : (r.LiquidTotal() + Math.Max(0, r.water)) / 10f + Math.Max(0, r.ice) * IceCubeMl;

        public static bool AllowedTechnique(DrinkKind kind, DrinkTechnique t) => kind switch
        {
            DrinkKind.Tea => t == DrinkTechnique.Steep,
            DrinkKind.Coffee => t == DrinkTechnique.Brew,
            _ => t != DrinkTechnique.Steep && t != DrinkTechnique.Brew && Enum.IsDefined(typeof(DrinkTechnique), t),
        };

        public static DrinkTechnique DefaultTechnique(DrinkKind kind) => kind switch
        {
            DrinkKind.Tea => DrinkTechnique.Steep,
            DrinkKind.Coffee => DrinkTechnique.Brew,
            _ => DrinkTechnique.Shake,
        };

        /// <summary>화면용 음료 종류 이름.</summary>
        public static string KindName(DrinkKind kind) => kind switch
        {
            DrinkKind.Cocktail => "칵테일",
            DrinkKind.Tea => "차",
            DrinkKind.Coffee => "커피",
            _ => "알 수 없음",
        };

        public static string TechniqueName(DrinkTechnique t) => t switch
        {
            DrinkTechnique.Shake => "셰이킹",
            DrinkTechnique.Stir => "스터링",
            DrinkTechnique.Build => "빌드",
            DrinkTechnique.Layer => "레이어링",
            DrinkTechnique.Steep => "차 우림",
            DrinkTechnique.Brew => "커피 추출",
            _ => "알 수 없음",
        };

        public static string TechniqueRule(DrinkTechnique t) => t switch
        {
            DrinkTechnique.Shake => "셰이커로 흔들기 — 치명타 기여 ×1.15, 나머지 ×0.95",
            DrinkTechnique.Stir => "믹싱 글라스에서 젓기 — 공격력·공격 속도 기여 ×1.08, 나머지 ×0.95",
            DrinkTechnique.Build => "마실 잔에 순서대로 붓기 — 전체 ×1.00",
            DrinkTechnique.Layer => "천천히 층 쌓기 — 서로 다른 효과 2종 이상이면 전체 ×1.05, 1종이면 ×0.90",
            DrinkTechnique.Steep => "다관에 우리기 — 전체 ×1.00",
            DrinkTechnique.Brew => "고른 추출 도구로 내리기 — 전체 ×1.00",
            _ => "",
        };

        /// <summary>화면용 짧은 설명(배율 수식은 도움말/보고서에만).</summary>
        public static string TechniqueShort(DrinkTechnique t) => t switch
        {
            DrinkTechnique.Shake => "셰이커로 흔들어 차갑고 살짝 탁하게 · 치명타 쪽 강화",
            DrinkTechnique.Stir => "믹싱 글라스에서 저어 맑게 · 공격력·공격 속도 강화",
            DrinkTechnique.Build => "마실 잔에 그대로 붓기 · 기본 효과",
            DrinkTechnique.Layer => "천천히 층을 쌓기 · 효과가 2종 이상이면 강화",
            DrinkTechnique.Steep => "다관에 물을 부어 우리기",
            DrinkTechnique.Brew => "고른 추출 도구로 원두를 내리기",
            _ => "",
        };

        public static string StatName(DrinkStat s) => s switch
        {
            DrinkStat.Attack => "공격력",
            DrinkStat.AttackSpeed => "공격 속도",
            DrinkStat.CritChance => "치명타 확률",
            DrinkStat.MaxHp => "최대 체력",
            DrinkStat.CooldownReduction => "재사용 시간 감소",
            DrinkStat.MoveSpeed => "이동 속도",
            DrinkStat.Range => "사거리",
            _ => "",
        };

        /// <summary>효과별 상한(비율). 치명타는 확률 %p.</summary>
        public static float Cap(DrinkStat s) => s switch
        {
            DrinkStat.Attack => 0.10f,
            DrinkStat.AttackSpeed => 0.08f,
            DrinkStat.CritChance => 0.05f,
            DrinkStat.MaxHp => 0.10f,
            DrinkStat.CooldownReduction => 0.05f,
            DrinkStat.MoveSpeed => 0.05f,
            DrinkStat.Range => 0.08f,
            _ => 0f,
        };

        /// <summary>0.1 단위 정수 → "12.3" 표시(반올림 없음, 정확).</summary>
        public static string Format(int tenths)
        {
            bool neg = tenths < 0;
            long abs = Math.Abs((long)tenths);
            return (neg ? "-" : "") + (abs / 10) + "." + (abs % 10);
        }

        public static string Format(int tenths, IngredientForm form) => Format(tenths) + (form == IngredientForm.Leaf ? "g" : "mL");

        /// <summary>사용자 입력(직접 입력·붙여넣기) → 0.1 단위 정수. 숫자·소수점 한 개만 허용, 소수 둘째 자리 이상·음수·NaN·지수·빈 값은 거부.</summary>
        public static bool TryParseAmount(string text, out int tenths, out string error)
        {
            tenths = 0;
            error = null;
            if (text == null) { error = "양을 입력하세요."; return false; }
            var s = text.Trim().Replace(',', '.');
            if (s.EndsWith("ml", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 2).Trim();
            else if (s.EndsWith("g", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 1).Trim();
            if (s.Length == 0) { error = "양을 입력하세요."; return false; }
            if (s.Length > 8) { error = "숫자가 너무 깁니다."; return false; }
            if (s[0] == '-') { error = "음수는 넣을 수 없습니다."; return false; }
            int dot = -1;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '.')
                {
                    if (dot >= 0) { error = "소수점은 한 번만 쓸 수 있습니다."; return false; }
                    dot = i;
                }
                else if (c < '0' || c > '9') { error = "숫자와 소수점만 입력할 수 있습니다."; return false; }
            }
            string whole = dot < 0 ? s : s.Substring(0, dot);
            string frac = dot < 0 ? "" : s.Substring(dot + 1);
            if (whole.Length == 0 && frac.Length == 0) { error = "숫자를 입력하세요."; return false; }
            if (frac.Length > 1)
            {
                // "1.20"처럼 0으로 끝나는 자리는 허용, 0.05 같은 0.1 미만 단위는 거부.
                for (int i = 1; i < frac.Length; i++)
                    if (frac[i] != '0') { error = "0.1 단위까지만 입력할 수 있습니다."; return false; }
                frac = frac.Substring(0, 1);
            }
            long w = whole.Length == 0 ? 0 : long.Parse(whole);
            long f = frac.Length == 0 ? 0 : frac[0] - '0';
            long value = w * 10 + f;
            if (value > 100000) { error = "값이 너무 큽니다."; return false; }
            tenths = (int)value;
            return true;
        }
    }
}
