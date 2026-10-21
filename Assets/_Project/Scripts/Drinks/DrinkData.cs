using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>맛·향(감각 설명). 문화·산지 사실은 culture에 따로 둔다.</summary>
        public string taste;
        /// <summary>문화·산지 해설(도감 전용, 맛·향과 구분). 출처는 Docs/DrinkLab-v1.1/재료 확장 10종과 문화 해설_2026-10-21.md에 기록하며 건강 효능은 쓰지 않는다. 없으면 null.</summary>
        public string culture;
        public string source;
        public string iconId;
        public string bottleId;

        public bool UsableIn(DrinkKind kind) => (use & UseBit(kind)) != 0;
        /// <summary>커피 원두(기본 원두·산지 원두 공통) = 커피 전용 건조 재료. 검증·계량 스쿱·추출 농도·주문·효과가 모두 이 판정으로 같은 원두로 본다.</summary>
        public bool IsCoffeeBean => use == DrinkUse.Coffee && form == IngredientForm.Leaf;
        /// <summary>낙하·더미 애니메이션 키(dl_leaf_&lt;key&gt;, dl_leaf_heap_&lt;key&gt;). 산지 원두는 공용 원두 애니메이션을 쓴다.</summary>
        public string LeafArtKey => IsCoffeeBean ? DrinkIngredients.CoffeeBean : id;
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

    /// <summary>87 재료 표(칵테일·차·커피 각 30종, 라임즙 공용). ID는 저장·공유 코드에 쓰이므로 바꾸지 않는다. 공유 코드 색인은 ShareIndex 순서.</summary>
    public static class DrinkIngredients
    {
        public const string Gin = "gin", Rum = "rum", BlueCuracao = "blue_curacao", Grenadine = "grenadine",
            BlackTea = "black_tea", GreenTea = "green_tea", LemonJuice = "lemon_juice", Honey = "honey", CoffeeBean = "coffee_bean", SteamedMilk = "steamed_milk";
        /// <summary>재료 확장 10종(2026-10-21): 칵테일 3·차 4·산지 원두 3.</summary>
        public const string Absinthe = "absinthe", CremeCassis = "creme_cassis", Limoncello = "limoncello",
            WhiteTea = "white_tea", OolongTea = "oolong_tea", JasmineTea = "jasmine_tea", ChamomileFlower = "chamomile_flower",
            EthiopiaYirgacheffeKochere = "ethiopia_yirgacheffe_kochere", ColombiaHuilaVergel = "colombia_huila_vergel", BrazilMantiqueiraAlvesLima = "brazil_mantiqueira_alves_lima";
        // 30종 목표 확장 1차(2026-10-21): 분야별 신규 3종. ID는 공유 코드 끝에 추가.
        public const string LimeJuice = "lime_juice", SimpleSyrup = "simple_syrup", DryVermouth = "dry_vermouth",
            DarjeelingTea = "darjeeling_tea", AnxiTieguanyin = "anxi_tieguanyin", PuErTea = "pu_er_tea",
            PanamaEsmeraldaGeisha = "panama_esmeralda_geisha", ColombiaSanPedroGeisha = "colombia_huila_san_pedro_geisha", ColombiaLaEsperanzaBourbon = "colombia_la_esperanza_bourbon";
        public const string Cachaca = "cachaca", Vodka = "vodka", TequilaBlanco = "tequila_blanco", Mezcal = "mezcal", BourbonWhiskey = "bourbon_whiskey", RyeWhiskey = "rye_whiskey", Cognac = "cognac", TripleSec = "triple_sec", SweetVermouth = "sweet_vermouth", Campari = "campari", CoffeeLiqueur = "coffee_liqueur", PeachLiqueur = "peach_liqueur", ElderflowerLiqueur = "elderflower_liqueur", TonicWater = "tonic_water", SodaWater = "soda_water", Cola = "cola", OrangeJuice = "orange_juice", PineappleJuice = "pineapple_juice";
        public const string AssamTea = "assam_tea", NilgiriTea = "nilgiri_tea", CeylonUvaTea = "ceylon_uva_tea", LongjingTea = "longjing_tea", BiluochunTea = "biluochun_tea", HuangshanMaofengTea = "huangshan_maofeng_tea", AnjiBaicha = "anji_baicha", DaHongPao = "da_hong_pao", WuyiRougui = "wuyi_rougui", DongDingTea = "dong_ding_tea", OrientalBeautyTea = "oriental_beauty_tea", UjiMatcha = "uji_matcha", SenchaTea = "sencha_tea", GyokuroTea = "gyokuro_tea", GenmaichaTea = "genmaicha_tea", HojichaTea = "hojicha_tea", KukichaTea = "kukicha_tea", LapsangSouchongTea = "lapsang_souchong_tea";
        public const string CoffeeGuji = "coffee_ethiopia_guji", CoffeeSidama = "coffee_ethiopia_sidama", CoffeeKenyaNyeri = "coffee_kenya_nyeri", CoffeeRwanda = "coffee_rwanda", CoffeeBurundi = "coffee_burundi", CoffeeUganda = "coffee_uganda", CoffeeIndonesiaGayo = "coffee_indonesia_gayo", CoffeeIndonesiaJava = "coffee_indonesia_java", CoffeeVietnamDalat = "coffee_vietnam_dalat", CoffeeIndiaMalabar = "coffee_india_malabar", CoffeeCostaRicaTarrazu = "coffee_costa_rica_tarrazu", CoffeeGuatemalaAntigua = "coffee_guatemala_antigua", CoffeeHondurasMarcala = "coffee_honduras_marcala", CoffeePeruChanchamayo = "coffee_peru_chanchamayo", CoffeeMexicoChiapas = "coffee_mexico_chiapas", CoffeeJamaicaBlueMountain = "coffee_jamaica_blue_mountain", CoffeeBrazilCerrado = "coffee_brazil_cerrado", CoffeeColombiaNarino = "coffee_colombia_narino", CoffeeElSalvador = "coffee_el_salvador", CoffeeNicaragua = "coffee_nicaragua", CoffeeTanzania = "coffee_tanzania", CoffeeBolivia = "coffee_bolivia";

        /// <summary>id가 커피 원두(기본·산지)인가. 알 수 없는 id는 false.</summary>
        public static bool IsCoffeeBean(string id) => Get(id)?.IsCoffeeBean ?? false;

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
                culture = "찻잎을 끝까지 산화시킨 차. 긴 운송에도 향이 오래가 아프리카·인도·스리랑카 등 세계 곳곳에서 만든다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_black_tea", bottleId = "dl_tin_black_tea",
            },
            new DrinkIngredientDef
            {
                id = GreenTea, name = "녹차 찻잎", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(70, 128, 52), brewColor = C(196, 204, 92), tint = 0.9f, turbidity = 0.04f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.MoveSpeed, coefficient = 0.8f,
                taste = "덖거나 쪄서 발효를 멈춘 녹색 잎. 우리면 맑은 황록색, 풋풋한 향.",
                culture = "찻잎을 찌거나 솥에 덖어 효소를 멈춘 뒤 비비고 말린, 산화시키지 않은 차.",
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
                culture = "여러 산지를 섞은 기본 원두. 스페셜티커피협회(SCA)는 맛·향과 함께 산지·생산자·재배·가공 정보도 커피의 가치로 본다.",
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
            // ───── 재료 확장 10종(2026-10-21). 수치는 임시 기본값, 출처·가정은 Docs/DrinkLab-v1.1/재료 확장 10종 문서. culture는 출처 페이지에 있는 사실만. ─────
            new DrinkIngredientDef
            {
                // 고도수 증류주라 진·럼보다 가볍다. 아트 dl_icon_absinthe / dl_bottle_absinthe / dl_srcbottle_absinthe.
                id = Absinthe, name = "압생트", use = DrinkUse.Cocktail, form = IngredientForm.Liquid, baseSpirit = true,
                color = C(168, 204, 112), tint = 0.8f, turbidity = 0f, viscosity = 0.1f, density = 0.92f,
                effect = DrinkStat.AttackSpeed, coefficient = 0.7f,
                taste = "아니스·회향의 달큰한 향 뒤로 쌉쌀한 허브 맛. 맑은 연두색이고 물을 더하면 뿌옇게 흐려진다.",
                culture = "스위스 뇌샤텔주 발드트라베르가 발상지. 쑥(그랑 웜우드)·아니스·회향으로 만들며, 스위스에서 1910년 금지됐다가 2005년 다시 허용됐다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_absinthe", bottleId = "dl_bottle_absinthe",
            },
            new DrinkIngredientDef
            {
                // 당도 높은 과일 리큐어: 무겁고 착색력이 강하다(그레나딘 다음으로 가라앉음).
                id = CremeCassis, name = "크렘 드 카시스", use = DrinkUse.Cocktail, form = IngredientForm.Liquid,
                color = C(104, 16, 52), tint = 3.4f, turbidity = 0.04f, viscosity = 0.65f, density = 1.25f,
                effect = DrinkStat.MaxHp, coefficient = 0.65f,
                taste = "갓 딴 블랙커런트의 진한 향과 벨벳처럼 부드러운 단맛. 조금만 넣어도 짙은 석류빛 붉은색으로 물든다.",
                culture = "카시스 드 디종은 블랙커런트 열매를 통째로 알코올에 차갑게 담가 만들며 1841년 디종에서 시작됐다. 지금도 디종에서만 만드는 지리적 표시(IG) 제품이다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_creme_cassis", bottleId = "dl_bottle_creme_cassis",
            },
            new DrinkIngredientDef
            {
                id = Limoncello, name = "리몬첼로", use = DrinkUse.Cocktail, form = IngredientForm.Liquid,
                color = C(246, 226, 84), tint = 0.9f, turbidity = 0.2f, viscosity = 0.4f, density = 1.10f,
                effect = DrinkStat.Attack, coefficient = 0.65f,
                taste = "레몬 껍질의 상큼한 향과 진한 단맛이 함께 나는 노란 리큐어.",
                culture = "이탈리아 소렌토반도와 카프리섬에서 자라는 소렌토 레몬(지리적 표시 P.G.I.)의 껍질로 만드는 남부 이탈리아의 리큐어.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_limoncello", bottleId = "dl_bottle_limoncello",
            },
            new DrinkIngredientDef
            {
                // 차 아트: dl_icon_white_tea / dl_tin_white_tea / dl_leaf_white_tea(낙하) / dl_leaf_heap_white_tea(더미).
                id = WhiteTea, name = "백차 찻잎", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(204, 206, 186), brewColor = C(234, 214, 150), tint = 0.4f, turbidity = 0.02f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.CooldownReduction, coefficient = 0.7f,
                taste = "은빛 솜털이 덮인 어린 싹. 아주 옅은 금빛으로 우러나고 맛이 은은하다.",
                culture = "전통적으로 중국 푸젠성에서 대백(Da Bai) 품종의 어린 싹과 잎을 따 햇볕 등에 말리기만 하는, 가장 단순하게 만드는 차. 싹만 쓰면 은침(Yin Zhen)이다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_white_tea", bottleId = "dl_tin_white_tea",
            },
            new DrinkIngredientDef
            {
                id = OolongTea, name = "우롱차 찻잎", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(70, 90, 50), brewColor = C(208, 140, 52), tint = 1.2f, turbidity = 0.02f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.MoveSpeed, coefficient = 0.75f,
                taste = "꽃향과 구운 곡물 향 사이를 오가는 맛. 호박빛으로 우러난다.",
                culture = "'검은 용'이라는 뜻의 차. 산화를 약 10%에서 80% 가까이까지 일부만 진행하며, 잎을 동그랗게 만 것부터 짙게 산화한 것까지 갈래가 다양하다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_oolong_tea", bottleId = "dl_tin_oolong_tea",
            },
            new DrinkIngredientDef
            {
                id = JasmineTea, name = "재스민차 찻잎", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(104, 134, 74), brewColor = C(216, 212, 116), tint = 0.75f, turbidity = 0.03f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.CooldownReduction, coefficient = 0.75f,
                taste = "재스민 꽃 향이 은은하게 밴 찻잎. 맑은 연둣빛으로 우러난다.",
                culture = "얼그레이처럼 찻잎에 향을 입힌 가향차(flavoured and scented tea) 갈래에 속한다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_jasmine_tea", bottleId = "dl_tin_jasmine_tea",
            },
            new DrinkIngredientDef
            {
                // 차나무 잎이 아닌 허브 인퓨전. 제조 규칙상 '찻잎' 칸(건조 g)을 그대로 쓴다.
                id = ChamomileFlower, name = "캐모마일 꽃", use = DrinkUse.Tea, form = IngredientForm.Leaf,
                color = C(238, 214, 96), brewColor = C(236, 198, 92), tint = 0.6f, turbidity = 0.04f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.MoveSpeed, coefficient = 0.7f,
                taste = "데이지를 닮은 작은 꽃송이. 달콤한 꽃향이 나고 맑은 금빛으로 우러난다.",
                culture = "캐모마일(Matricaria chamomilla)은 차나무가 아닌 국화과 식물로, 말린 꽃송이를 우리는 허브 인퓨전이다. 허브차에서 가장 흔한 단일 재료 중 하나다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_chamomile_flower", bottleId = "dl_tin_chamomile_flower",
            },
            // 산지 원두: 커피 효과·계량·추출은 기본 원두와 같다(IsCoffeeBean). 아트 dl_icon_<id> / dl_tin_<id>, 낙하·스쿱은 공용 dl_leaf_coffee_bean.
            new DrinkIngredientDef
            {
                id = EthiopiaYirgacheffeKochere, name = "예가체프 코체레", use = DrinkUse.Coffee, form = IngredientForm.Leaf,
                color = C(124, 78, 44), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.Range, coefficient = 0.8f,
                taste = "꽃향과 레몬 같은 시트러스 산미가 밝은 원두.",
                culture = "에티오피아 남부 예가체프 지역 코체레의 아노가라 가공소에서 물로 씻어 가공한(워시드) 원두.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_ethiopia_yirgacheffe_kochere", bottleId = "dl_tin_ethiopia_yirgacheffe_kochere",
            },
            new DrinkIngredientDef
            {
                id = ColombiaHuilaVergel, name = "엘 베르헬 (피탈리토)", use = DrinkUse.Coffee, form = IngredientForm.Leaf,
                color = C(100, 62, 36), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.Range, coefficient = 0.8f,
                taste = "부르봉 로사도 내추럴과 부르봉 라야도 워시드, 두 로트가 소개된 농장.",
                culture = "콜롬비아 우일라주 피탈리토의 핀카 엘 베르헬 농장 원두. 부르봉 로사도 내추럴, 부르봉 라야도 워시드처럼 품종·가공법별로 로트를 나눈다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_colombia_huila_vergel", bottleId = "dl_tin_colombia_huila_vergel",
            },
            new DrinkIngredientDef
            {
                id = BrazilMantiqueiraAlvesLima, name = "파젠다 알베스 리마", use = DrinkUse.Coffee, form = IngredientForm.Leaf,
                color = C(78, 46, 26), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1.0f,
                effect = DrinkStat.Range, coefficient = 0.8f,
                taste = "가볍고 단맛이 돌며 초콜릿·캐러멜·견과 향이 난다.",
                culture = "브라질 미나스제라이스의 파젠다 알베스 리마. 할아버지 제툴리우 알베스가 첫 커피나무를 심은 뒤 가족이 이어 온 약 100년 된 농장이다.",
                source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_brazil_mantiqueira_alves_lima", bottleId = "dl_tin_brazil_mantiqueira_alves_lima",
            },
            // 30종 목표 확장 1차. 구체 산지·품종 정보는 Docs/DrinkLab-v1.2/30종 확장 1차 출처와 초보 레시피.md 참조.
            new DrinkIngredientDef { id = LimeJuice, name = "라임즙", use = DrinkUse.Both, form = IngredientForm.Liquid, color = C(186, 218, 92), tint = 0.3f, turbidity = 0.5f, viscosity = 0.1f, density = 1.03f, effect = DrinkStat.Attack, coefficient = 0.45f, taste = "레몬보다 둥글고 향긋한 산미의 라임즙.", culture = "라임은 모히토·다이키리 등 여러 칵테일에 쓰이며, 모히토에는 라임즙과 민트·설탕·럼이 들어간다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_lime_juice", bottleId = "dl_bottle_lime_juice" },
            new DrinkIngredientDef { id = SimpleSyrup, name = "설탕 시럽", use = DrinkUse.Cocktail, form = IngredientForm.Liquid, color = C(238, 218, 162), tint = 0.18f, turbidity = 0.01f, viscosity = 0.55f, density = 1.18f, effect = DrinkStat.MaxHp, coefficient = 0.42f, taste = "설탕을 물에 녹여 만든 투명한 단맛 재료.", culture = "칵테일에서 설탕의 단맛을 고르게 섞기 위한 시럽이다. IBA 사우스사이드에는 진·레몬즙·심플 시럽이 쓰인다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_simple_syrup", bottleId = "dl_bottle_simple_syrup" },
            new DrinkIngredientDef { id = DryVermouth, name = "드라이 베르무트", use = DrinkUse.Cocktail, form = IngredientForm.Liquid, color = C(222, 218, 170), tint = 0.35f, turbidity = 0.02f, viscosity = 0.12f, density = 1.02f, effect = DrinkStat.AttackSpeed, coefficient = 0.45f, taste = "허브와 향신료로 향을 더한 드라이한 강화 와인.", culture = "베르무트는 향료를 넣은 강화 와인이다. IBA 드라이 마티니는 진과 드라이 베르무트를 얼음과 저어 만든다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_dry_vermouth", bottleId = "dl_bottle_dry_vermouth" },
            new DrinkIngredientDef { id = DarjeelingTea, name = "다르질링 홍차", use = DrinkUse.Tea, form = IngredientForm.Leaf, color = C(96, 68, 42), brewColor = C(192, 128, 68), tint = 1.15f, turbidity = 0.02f, viscosity = 0f, density = 1f, effect = DrinkStat.CooldownReduction, coefficient = 0.7f, taste = "가볍고 향이 섬세한 홍차. 다원·수확 시기에 따라 인상이 달라진다.", culture = "인도 서벵골주 다르질링 지역의 차다. 인도 차위원회가 지리적 표시와 인증 체계로 진품 산지를 관리한다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_darjeeling_tea", bottleId = "dl_tin_darjeeling_tea" },
            new DrinkIngredientDef { id = AnxiTieguanyin, name = "안시 철관음", use = DrinkUse.Tea, form = IngredientForm.Leaf, color = C(70, 100, 50), brewColor = C(218, 164, 72), tint = 0.9f, turbidity = 0.02f, viscosity = 0f, density = 1f, effect = DrinkStat.MoveSpeed, coefficient = 0.7f, taste = "단단히 말린 잎과 황금빛 또는 주황빛 우림색이 특징인 우롱차.", culture = "중국 푸젠성 안시현에서 생산되는 철관음 품종의 우롱차다. 중국 국가 지식산권국은 지리적 표시 제품으로 소개한다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_anxi_tieguanyin", bottleId = "dl_tin_anxi_tieguanyin" },
            new DrinkIngredientDef { id = PuErTea, name = "윈난 보이차(숙차)", use = DrinkUse.Tea, form = IngredientForm.Leaf, color = C(72, 58, 38), brewColor = C(112, 66, 34), tint = 1.7f, turbidity = 0.03f, viscosity = 0f, density = 1f, effect = DrinkStat.MoveSpeed, coefficient = 0.65f, taste = "숙성·후발효 과정을 거쳐 짙은 적갈색으로 우러나는 차.", culture = "중국 윈난성의 대엽종 찻잎으로 만드는 보이차다. 숙차는 미생물 발효를 거치는 방식으로, 생차와 구별된다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_pu_er_tea", bottleId = "dl_tin_pu_er_tea" },
            new DrinkIngredientDef { id = PanamaEsmeraldaGeisha, name = "에스메랄다 게이샤", use = DrinkUse.Coffee, form = IngredientForm.Leaf, color = C(126, 78, 42), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1f, effect = DrinkStat.Range, coefficient = 0.8f, taste = "재스민 꽃향과 핵과류 향이 알려진 게이샤 커피.", culture = "파나마 보케테의 하시엔다 라 에스메랄다 농장은 2004년 자사 농장에서 게이샤의 독특한 향미를 발견했다고 소개한다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_panama_esmeralda_geisha", bottleId = "dl_tin_panama_esmeralda_geisha" },
            new DrinkIngredientDef { id = ColombiaSanPedroGeisha, name = "산 페드로 게이샤", use = DrinkUse.Coffee, form = IngredientForm.Leaf, color = C(116, 70, 40), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1f, effect = DrinkStat.Range, coefficient = 0.8f, taste = "재스민과 베르가모트 향으로 소개된 워시드 게이샤.", culture = "콜롬비아 우일라주 피탈리토의 아시엔다 산 페드로에서 생산된 게이샤 로트다. 생산자 Primavera Coffee는 36시간 발효 후 워시드 가공으로 기록한다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_colombia_huila_san_pedro_geisha", bottleId = "dl_tin_colombia_huila_san_pedro_geisha" },
            new DrinkIngredientDef { id = ColombiaLaEsperanzaBourbon, name = "라 에스페란사 부르봉", use = DrinkUse.Coffee, form = IngredientForm.Leaf, color = C(132, 76, 46), brewColor = C(62, 34, 18), tint = 2.4f, turbidity = 0.08f, viscosity = 0f, density = 1f, effect = DrinkStat.Range, coefficient = 0.8f, taste = "꿀 가공과 카라멜·붉은 과일·카카오 노트로 소개된 로트.", culture = "콜롬비아 카이세도니아의 핀카 라 에스페란사에서 생산한 부르봉 로사도다. Primavera Coffee 자료는 허니 가공과 18일 태양 건조를 명시한다.", source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_colombia_la_esperanza_bourbon", bottleId = "dl_tin_colombia_la_esperanza_bourbon" },
        };

        /// <summary>공유 코드 v1 재료 색인(0부터). 새 재료는 끝에만 추가한다(10~19 = 재료 확장 10종, 기존 0~9 순서 고정).</summary>
        public static string[] ShareIndex =
        {
            Gin, Rum, BlueCuracao, Grenadine, BlackTea, GreenTea, LemonJuice, Honey, CoffeeBean, SteamedMilk,
            Absinthe, CremeCassis, Limoncello, WhiteTea, OolongTea, JasmineTea, ChamomileFlower,
            EthiopiaYirgacheffeKochere, ColombiaHuilaVergel, BrazilMantiqueiraAlvesLima,
            LimeJuice, SimpleSyrup, DryVermouth, DarjeelingTea, AnxiTieguanyin, PuErTea,
            PanamaEsmeraldaGeisha, ColombiaSanPedroGeisha, ColombiaLaEsperanzaBourbon,
        };

        static Dictionary<string, DrinkIngredientDef> byId;

        static DrinkIngredients()
        {
            var all = (List<DrinkIngredientDef>)All;
            var extra = Build30Catalog();
            all.AddRange(extra);
            ShareIndex = ShareIndex.Concat(extra.ConvertAll(d => d.id)).ToArray();
        }

        static List<DrinkIngredientDef> Build30Catalog()
        {
            var a = new List<DrinkIngredientDef>();
            void Add(string id, string name, DrinkUse use, IngredientForm form, DrinkStat effect, Color color, Color brew,
                string taste, string culture, bool baseSpirit = false, float density = 1f, float tint = 0.8f)
            {
                a.Add(new DrinkIngredientDef { id = id, name = name, use = use, form = form, baseSpirit = baseSpirit, color = color, brewColor = brew,
                    tint = tint, turbidity = form == IngredientForm.Liquid ? 0.06f : 0.02f, viscosity = form == IngredientForm.Liquid ? 0.15f : 0f,
                    density = density, effect = effect, coefficient = 0.42f, taste = taste, culture = culture,
                    source = "일반·정예 처치, 중간·최종 보스 보상", iconId = "dl_icon_" + id,
                    bottleId = (form == IngredientForm.Leaf ? "dl_tin_" : "dl_bottle_") + id });
            }
            Color cream = C(240, 226, 184), herb = C(112, 150, 74), red = C(188, 42, 52), brown = C(116, 74, 42), darkTea = C(86, 56, 36), greenTea = C(82, 132, 58), bean = C(110, 68, 38), coffee = C(62, 34, 18);
            // 18 additional cocktail materials: named, widely documented bar ingredients; category effects remain cocktail-only.
            Add(Cachaca,"카샤사",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.AttackSpeed,C(226,238,220),Color.clear,"사탕수수즙을 발효·증류한 브라질 증류주.","브라질의 사탕수수 증류주로, 카이피리냐의 전통적인 베이스 술이다.",true,0.95f);
            Add(Vodka,"보드카",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.Attack,C(246,249,252),Color.clear,"대체로 무색이며 향이 절제된 증류주.","유럽 여러 지역에서 생산되는 곡물·감자 기반 증류주로, 칵테일의 중성 베이스로 널리 쓰인다.",true,0.95f);
            Add(TequilaBlanco,"블랑코 테킬라",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.CritChance,C(238,242,218),Color.clear,"아가베 향이 살아 있는 숙성 전 테킬라.","멕시코의 테킬라는 블루 아가베로 만들며, 블랑코는 오크 숙성이 없거나 짧은 유형이다.",true,0.95f);
            Add(Mezcal,"메스칼",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.Attack,C(216,226,196),Color.clear,"아가베의 풀 향과 훈연 향이 두드러질 수 있다.","멕시코의 여러 주에서 아가베로 만드는 증류주다. 생산 지역과 아가베 종류가 다양하다.",true,0.96f);
            Add(BourbonWhiskey,"버번 위스키",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.Attack,C(178,108,46),Color.clear,"옥수수 비중이 높은 곡물 향과 오크 숙성 향.","미국에서 생산되는 위스키 유형. 칵테일에서는 올드 패션드 같은 레시피에 쓰인다.",true,0.97f);
            Add(RyeWhiskey,"라이 위스키",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.AttackSpeed,C(164,98,44),Color.clear,"호밀의 알싸하고 향신료 같은 인상.","호밀을 주원료로 한 위스키 유형으로, 맨해튼 등 클래식 칵테일의 베이스로 쓰인다.",true,0.97f);
            Add(Cognac,"코냑",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.MaxHp,C(182,104,40),Color.clear,"포도 증류주를 오크통에서 숙성한 향.","프랑스 코냑 지역에서 정해진 규정에 따라 만드는 브랜디의 한 종류다.",true,0.98f);
            Add(TripleSec,"트리플 섹",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.CritChance,C(238,204,112),Color.clear,"오렌지 껍질 향의 달콤한 오렌지 리큐어.","오렌지 향 리큐어의 한 갈래. 마가리타·사이드카 등 여러 칵테일에 쓰인다.",false,1.08f,1.1f);
            Add(SweetVermouth,"스위트 베르무트",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.MaxHp,C(166,80,42),Color.clear,"허브와 향신료 향을 더한 달콤한 강화 와인.","향료를 넣은 강화 와인. 네그로니와 맨해튼 같은 저어 만드는 칵테일에 쓰인다.",false,1.05f,0.9f);
            Add(Campari,"캄파리",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.CritChance,C(202,40,50),Color.clear,"붉은빛과 쌉쌀한 허브·시트러스 향.","이탈리아에서 시작된 비터 아페리티보. 네그로니의 주요 재료 중 하나다.",false,1.05f,1.4f);
            Add(CoffeeLiqueur,"커피 리큐어",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.AttackSpeed,C(76,42,26),Color.clear,"커피 향과 단맛을 더하는 진한 리큐어.","커피를 활용한 리큐어 갈래. 블랙 러시안 등 칵테일 재료로 사용된다.",false,1.1f,1.4f);
            Add(PeachLiqueur,"피치 리큐어",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.MaxHp,C(224,140,92),Color.clear,"복숭아 과실 향의 달콤한 리큐어.","복숭아 향 리큐어. 벨리니 등 과일 칵테일에 쓰이는 재료다.",false,1.08f,0.8f);
            Add(ElderflowerLiqueur,"엘더플라워 리큐어",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.AttackSpeed,C(226,214,150),Color.clear,"흰 꽃과 배를 떠올리는 향의 리큐어.","엘더플라워 꽃을 향료로 사용하는 리큐어 갈래. 허브·시트러스와 조합한다.",false,1.08f,0.65f);
            Add(TonicWater,"토닉워터",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.Attack,C(226,238,218),Color.clear,"탄산과 퀴닌 계열의 쌉쌀함.","탄산수에 퀴닌 계열의 쌉쌀한 맛을 더한 믹서. 진 토닉에 쓰인다.",false,1.01f,0.12f);
            Add(SodaWater,"탄산수",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.AttackSpeed,C(236,246,250),Color.clear,"향과 단맛이 거의 없는 탄산 믹서.","물에 이산화탄소를 녹인 탄산 믹서. 하이볼과 피즈 계열에 쓰인다.",false,1f,0.05f);
            Add(Cola,"콜라",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.MaxHp,C(92,48,30),Color.clear,"캐러멜빛과 향신료 향의 탄산 음료.","쿠바 리브레처럼 럼·라임과 조합하는 칵테일 믹서로도 쓰인다.",false,1.04f,0.45f);
            Add(OrangeJuice,"오렌지 주스",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.Attack,C(240,150,38),Color.clear,"오렌지 과즙의 산미와 단맛.","감귤류 주스는 칵테일 믹서로 널리 쓰인다.",false,1.04f,0.55f);
            Add(PineappleJuice,"파인애플 주스",DrinkUse.Cocktail,IngredientForm.Liquid,DrinkStat.MaxHp,C(236,186,54),Color.clear,"파인애플 과즙의 달콤하고 산뜻한 맛.","열대 과일 주스는 피냐 콜라다 등 칵테일의 재료로 쓰인다.",false,1.05f,0.65f);
            // 18 additional teas: recognized regional teas and processing styles.
            Add(AssamTea,"아삼 홍차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,darkTea,C(176,72,32),"짙은 호박색과 몰트 풍미로 알려진 홍차.","인도 아삼주의 브라마푸트라 계곡에서 재배되는 차. Tea Board India가 산지 차로 소개한다.");
            Add(NilgiriTea,"닐기리 홍차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,darkTea,C(168,84,40),"남인도 고지대 차 산지의 향긋한 홍차.","인도 타밀나두의 닐기리 산맥에서 생산되는 차 산지명이다.");
            Add(CeylonUvaTea,"우바 홍차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,darkTea,C(174,80,42),"스리랑카 우바 산지의 계절성 홍차.","스리랑카의 주요 차 산지 가운데 하나인 우바 지역의 홍차다.");
            Add(LongjingTea,"룽징 녹차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,greenTea,C(206,196,102),"납작하게 덖은 잎의 고소하고 풋풋한 인상.","중국 저장성 항저우 서호 주변의 덖음 녹차로, 용정차라는 이름으로 알려져 있다.");
            Add(BiluochunTea,"벽라춘",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,greenTea,C(214,190,100),"가늘게 말린 잎의 향긋한 중국 녹차.","중국 장쑤성 둥팅산의 녹차. 봄철 어린 잎을 말아 만드는 명차다.");
            Add(HuangshanMaofengTea,"황산 모봉",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,greenTea,C(204,192,104),"어린 싹과 잎으로 만드는 가벼운 녹차.","중국 안후이성 황산 지역에서 생산되는 녹차다.");
            Add(AnjiBaicha,"안지 백차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,C(146,168,100),C(222,214,142),"연한 황록색으로 우리며 부드러운 녹차.","이름에 백차가 들어가지만 차 분류는 녹차다. 중국 저장성 안지현의 백엽 품종으로 만든다.");
            Add(DaHongPao,"대홍포",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,C(76,104,54),C(186,116,58),"암차 특유의 볶은 향과 깊은 우림색.","중국 푸젠성 우이산의 무이암차에 속하는 우롱차 이름이다.");
            Add(WuyiRougui,"무이 육계",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,C(76,102,52),C(178,112,54),"계피를 연상시키는 향으로 알려진 우롱차.","푸젠성 우이산 암차 품종·차 이름인 육계(肉桂)다.");
            Add(DongDingTea,"동정 우롱차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,C(68,98,48),C(190,132,58),"둥글게 말린 잎을 여러 차례 우릴 수 있는 차.","대만 난터우현 루구의 동정산 일대에서 발전한 우롱차다.");
            Add(OrientalBeautyTea,"동방미인차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,C(82,100,56),C(194,132,70),"꿀·과실을 떠올리는 향으로 소개되는 대만 우롱차.","대만의 팽풍차·동방미인차로 알려진 우롱차. 작은 매미충의 흡즙을 받은 잎을 활용한다.");
            Add(UjiMatcha,"우지 말차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,C(66,142,52),C(132,174,68),"그늘 재배 찻잎을 갈아 만든 선명한 녹색 가루차.","교토부 우지차의 한 유형. 덴차를 곱게 갈아 물에 풀어 마신다.");
            Add(SenchaTea,"센차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,greenTea,C(194,208,104),"찐 뒤 비벼 말린 일본 녹차.","일본 농림수산성은 센차를 대표적인 일본 녹차로 소개하며, 증열·유념·건조로 만든다.");
            Add(GyokuroTea,"교쿠로",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,C(54,120,48),C(152,184,74),"차광 재배로 감칠맛이 강조되는 일본 녹차.","새잎을 햇빛에서 가려 재배하는 일본 녹차 유형이다.");
            Add(GenmaichaTea,"현미차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,C(120,126,68),C(192,158,76),"녹차에 볶은 쌀을 섞어 고소한 향을 낸다.","일본의 녹차 음료 유형. 찻잎에 볶은 현미를 섞어 우린다.");
            Add(HojichaTea,"호지차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,C(116,88,48),C(176,106,48),"센차·번차 등을 볶아 구수한 향을 낸 차.","일본 우지차의 제조 갈래 중 하나로, 녹차를 볶아 만든다.");
            Add(KukichaTea,"쿠키차",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.CooldownReduction,C(118,132,72),C(194,180,96),"찻잎 줄기와 잎자루를 포함한 차.","일본 녹차 가공에서 잎자루와 줄기 부분을 골라 만든 차로, 보차라고도 한다.");
            Add(LapsangSouchongTea,"정산소종",DrinkUse.Tea,IngredientForm.Leaf,DrinkStat.MoveSpeed,darkTea,C(148,76,40),"훈연 향이 특징인 짙은 중국 홍차.","중국 푸젠성 우이산 지역의 홍차. 전통적으로 소나무 장작 훈연 향을 입히는 방식으로 알려져 있다.");
            // 22 further coffee origins. These are origin-region lots rather than invented farm names; flavor notes are not assigned without a cited lot.
            string[] coffeeIds = { CoffeeGuji,CoffeeSidama,CoffeeKenyaNyeri,CoffeeRwanda,CoffeeBurundi,CoffeeUganda,CoffeeIndonesiaGayo,CoffeeIndonesiaJava,CoffeeVietnamDalat,CoffeeIndiaMalabar,CoffeeCostaRicaTarrazu,CoffeeGuatemalaAntigua,CoffeeHondurasMarcala,CoffeePeruChanchamayo,CoffeeMexicoChiapas,CoffeeJamaicaBlueMountain,CoffeeBrazilCerrado,CoffeeColombiaNarino,CoffeeElSalvador,CoffeeNicaragua,CoffeeTanzania,CoffeeBolivia };
            string[] coffeeNames = { "에티오피아 구지","에티오피아 시다마","케냐 니에리","르완다 커피","부룬디 커피","우간다 커피","인도네시아 가요","인도네시아 자바","베트남 달랏","인도 말라바르","코스타리카 타라주","과테말라 안티구아","온두라스 마르칼라","페루 찬차마요","멕시코 치아파스","자메이카 블루마운틴","브라질 세하두","콜롬비아 나리뇨","엘살바도르 커피","니카라과 커피","탄자니아 커피","볼리비아 커피" };
            string[] coffeePlaces = { "에티오피아 구지","에티오피아 시다마","케냐 니에리","르완다","부룬디","우간다","인도네시아 아체주 가요 고원","인도네시아 자바섬","베트남 람동성 달랏 고원","인도 말라바르 해안","코스타리카 타라주","과테말라 안티구아","온두라스 마르칼라","페루 찬차마요","멕시코 치아파스","자메이카 블루마운틴","브라질 세하두","콜롬비아 나리뇨","엘살바도르","니카라과","탄자니아","볼리비아" };
            for (int i = 0; i < coffeeIds.Length; i++)
                Add(coffeeIds[i], coffeeNames[i], DrinkUse.Coffee, IngredientForm.Leaf, DrinkStat.Range,
                    C(104 + (i % 4) * 7, 62 + (i % 3) * 5, 34), coffee,
                    "산지·품종·수확 로트와 가공에 따라 향미가 달라진다.", coffeePlaces[i] + "에서 커피가 재배되는 산지다. 이 항목은 농장·개별 로트의 맛을 단정하지 않는 산지 표기다.");
            return a;
        }

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

    /// <summary>제조 규칙·한도(임시 기본값). 모든 양은 정수 1 = 0.1 단위.</summary>
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
        public const int CoffeeScoopCapacity = 200;   // 20.0 g 원두 스쿱(한 번에 한 잔 분량)
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
