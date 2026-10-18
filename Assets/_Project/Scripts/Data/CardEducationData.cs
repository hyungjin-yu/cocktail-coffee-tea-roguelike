using System;
namespace CCT
{
    // Authored defaults only. Resource loading never rewrites serialized cards.
    public static class CardEducationData
    {
        public static void Author(CardData card)
        {
            switch (card.id)
            {
                case "dry_gin":
                    card.realWorldFact = "주니퍼 향이 특징인 증류주입니다. 드라이는 단맛에 관한 표현이며, 진은 칵테일의 베이스로 쓰입니다.";
                    card.gameplayConnection = "베이스의 존재감을 공격력으로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Tanqueray London Dry Gin";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-us/home/our-brands/tanqueray-london-dry-gin";
                    card.additionalSources = "";
                    card.educationArtId = "card_gin";
                    card.flavorText = card.realWorldFact;
                    break;
                case "shot_glass":
                    card.realWorldFact = "재료의 양을 재는 도구. 용량은 제품마다 다릅니다.";
                    card.gameplayConnection = "반복 계량의 리듬을 공격 속도로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Jigger";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/bartender-skills-and-techniques/jigger";
                    card.additionalSources = "";
                    card.educationArtId = "card_jigger";
                    card.flavorText = card.realWorldFact;
                    break;
                case "long_stirrer":
                    card.realWorldFact = "긴 손잡이로 얼음과 재료를 저어 섞는 바 도구입니다.";
                    card.gameplayConnection = "긴 도구을 사거리로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Cocktail stirring";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/bartender-skills-and-techniques/the-subtle-art-of-cocktail-stirring";
                    card.additionalSources = "";
                    card.educationArtId = "card_bar_spoon";
                    card.flavorText = card.realWorldFact;
                    break;
                case "bar_shoes":
                    card.realWorldFact = "제공할 잔에 재료를 직접 넣어 만드는 방식입니다.";
                    card.gameplayConnection = "간결한 동선을 이동 속도로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Building cocktails";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/bartender-skills-and-techniques/the-art-of-building-cocktails-directly-in-the-glass";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "ice_cube":
                    card.realWorldFact = "얼음은 냉각하고 녹은 물은 맛과 농도에 관여합니다.";
                    card.gameplayConnection = "맛의 완충을 최대 체력으로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Quality serves";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-us/home/bartender-skills-and-techniques/quality-serves";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "liqueur":
                    card.realWorldFact = "당과 과일·허브 등의 풍미로 베이스를 보완하는 술입니다.";
                    card.gameplayConnection = "소량의 풍미 보완을 게이지 절약으로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Liqueurs, bitters and vermouth";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-us/home/explore-all-categories/liqueurs-bitters-and-vermouth";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "syrup":
                    card.realWorldFact = "서비스 전에 여러 잔 분량의 재료를 미리 준비합니다.";
                    card.gameplayConnection = "준비한 분량을 추가 재료로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Batch cocktails";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-us/home/masterclass-webinars/crafting-the-perfect-crowd-pleaser-the-method-behind-batch-cocktails";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "double_shot":
                    card.realWorldFact = "국제바텐더협회(IBA) 레시피는 진·캄파리·스위트 레드 베르무트를 동량으로 구성합니다. 캄파리는 쌉쌀한 식전주이며, 베르무트는 향을 입힌 강화 와인입니다.";
                    card.gameplayConnection = "세 재료의 구성을 공격력으로 표현했습니다.";
                    card.sourceName = "International Bartenders Association · negroni";
                    card.sourceUrl = "https://iba-world.com/iba-cocktail/negroni/";
                    card.additionalSources = "재료 용어: Diageo Bar Academy · Liqueurs, bitters and vermouth\nhttps://www.diageobaracademy.com/en-zz/home/explore-all-categories/liqueurs-bitters-and-vermouth\nCampari · Bitter aperitif\nhttps://www.campari.com/en-gb/our-products/campari/";
                    card.educationArtId = "card_negroni";
                    card.flavorText = card.realWorldFact;
                    break;
                case "mixing_tempo":
                    card.realWorldFact = "얼음과 흔들어 혼합·냉각·희석하고 질감도 바꿉니다. 빠르게 흔들수록 무조건 좋은 것은 아니며 재료와 목적에 맞게 조절합니다.";
                    card.gameplayConnection = "반복 동작을 공격 속도로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Cocktail shaking";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-us/home/customer-service/mastering-the-art-of-cocktail-shaking";
                    card.additionalSources = "";
                    card.educationArtId = "card_shaker";
                    card.flavorText = card.realWorldFact;
                    break;
                case "flambe":
                    card.realWorldFact = "바 스트레이너와 고운 체로 작은 고형물을 거릅니다.";
                    card.gameplayConnection = "두 단계 통과을 연속 타격으로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Cocktail strainers";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/bartender-skills-and-techniques/cocktail-strainers";
                    card.additionalSources = "";
                    card.educationArtId = "card_double_strain";
                    card.flavorText = card.realWorldFact;
                    break;
                case "champagne_spray":
                    card.realWorldFact = "베이스 역할을 두 가지 이상의 술로 나눠 구성합니다.";
                    card.gameplayConnection = "나눠진 기반을 다중 발사로 표현했습니다.";
                    card.sourceName = "The Bar · Split-base cocktails";
                    card.sourceUrl = "https://www.thebar.com/en-gb/articles/split-base-cocktails";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "molotov":
                    card.realWorldFact = "감귤 껍질의 향유와 불꽃을 이용해 향과 시각적 인상을 더하는 전문 바 연출입니다.";
                    card.gameplayConnection = "순간 불꽃을 적중 화염으로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Copper Colours";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/explore-all-recipes/copper-colours";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "scotch_rocks":
                    card.realWorldFact = "위스키·설탕·비터스·물로 바탕 술의 풍미를 살립니다.";
                    card.gameplayConnection = "베이스 중심을 공격력으로 표현했습니다.";
                    card.sourceName = "International Bartenders Association · old-fashioned";
                    card.sourceUrl = "https://iba-world.com/iba-cocktail/old-fashioned/";
                    card.additionalSources = "";
                    card.educationArtId = "card_old_fashioned";
                    card.flavorText = card.realWorldFact;
                    break;
                case "dry_martini":
                    card.realWorldFact = "국제바텐더협회(IBA) 레시피는 진과 드라이 베르무트를 얼음과 저어 만듭니다. 베르무트는 향을 입힌 강화 와인이며, 배합 비율은 마티니 스타일마다 달라집니다. 스티어는 저어 섞는 동작입니다.";
                    card.gameplayConnection = "존재감과 스티어을 공격·공속으로 표현했습니다.";
                    card.sourceName = "International Bartenders Association · dry-martini";
                    card.sourceUrl = "https://iba-world.com/iba-cocktail/dry-martini/";
                    card.additionalSources = "베르무트: Campari Academy · Understanding vermouth\nhttps://www.campariacademy.com/en-ch/training/products-category/vermouth/";
                    card.educationArtId = "card_martini";
                    card.flavorText = card.realWorldFact;
                    break;
                case "blazer":
                    card.realWorldFact = "위스키와 물을 사용하고 두 금속 머그 사이로 불꽃을 잇는 고전 전문 바 연출입니다.";
                    card.gameplayConnection = "이어지는 불꽃을 적중 화염으로 표현했습니다.";
                    card.sourceName = "Oxford Companion to Spirits and Cocktails · Blue Blazer";
                    card.sourceUrl = "https://www.spiritsanddistilling.com/dictionary/acref-9780199311132-e-260";
                    card.additionalSources = "";
                    card.educationArtId = "card_blue_blazer";
                    card.flavorText = card.realWorldFact;
                    break;
                case "rifled_barrel":
                    card.realWorldFact = "따르는 액체에서 얼음·과육 같은 고형물을 거릅니다.";
                    card.gameplayConnection = "액체의 통과을 관통으로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Cocktail strainers";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/bartender-skills-and-techniques/cocktail-strainers";
                    card.additionalSources = "";
                    card.educationArtId = "card_double_strain";
                    card.flavorText = card.realWorldFact;
                    break;
                case "green_tea":
                    card.realWorldFact = "찻나무 잎을 가열해 효소적 산화를 억제한 차입니다.";
                    card.gameplayConnection = "산뜻한 풍미의 인상을 공격력으로 표현했습니다.";
                    card.sourceName = "Oregon State University · Tea";
                    card.sourceUrl = "https://lpi.oregonstate.edu/mic/food-beverages/tea";
                    card.additionalSources = "";
                    card.educationArtId = "card_green_tea";
                    card.flavorText = card.realWorldFact;
                    break;
                case "tea_whisk":
                    card.realWorldFact = "말차 가루를 물에 풀어 섞는 대나무 도구입니다.";
                    card.gameplayConnection = "여러 가닥의 움직임을 공속으로 표현했습니다.";
                    card.sourceName = "Ippodo Tea · Bamboo whisk";
                    card.sourceUrl = "https://global.ippodo-tea.co.jp/products/utensils901042";
                    card.additionalSources = "";
                    card.educationArtId = "card_tea_whisk";
                    card.flavorText = card.realWorldFact;
                    break;
                case "long_spout":
                    card.realWorldFact = "가늘고 긴 주둥이로 따르는 물줄기를 조절합니다.";
                    card.gameplayConnection = "물줄기의 길이을 사거리로 표현했습니다.";
                    card.sourceName = "HARIO · Product catalogue 2019, p26";
                    card.sourceUrl = "https://global.hario.com/pdf2019/2019HARIO_GC_COFFEE_EN.pdf";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "tea_sandals":
                    card.realWorldFact = "우린 차를 따라 찻잎과 차탕을 분리하는 과정입니다.";
                    card.gameplayConnection = "제때 따라내는 동작을 이동으로 표현했습니다.";
                    card.sourceName = "TeaVivre · Gongfu brewing";
                    card.sourceUrl = "https://www.teavivre.com/info/common-gongfu-tea-brewing-mistakes-to-avoid.html";
                    card.additionalSources = "";
                    card.educationArtId = "card_gaiwan";
                    card.flavorText = card.realWorldFact;
                    break;
                case "warm_cup":
                    card.realWorldFact = "차를 담기 전 잔을 데워 온도 손실을 줄입니다.";
                    card.gameplayConnection = "온도 완충을 최대 체력으로 표현했습니다.";
                    card.sourceName = "Global Tea Hut · Gongfu brewing and preheating";
                    card.sourceUrl = "https://archive.globalteahut.org/docs/issues/2014-05.pdf";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "honey":
                    card.realWorldFact = "잎 양을 가늠하지만 부피는 무게와 같지 않습니다.";
                    card.gameplayConnection = "분량 조절을 게이지 절약으로 표현했습니다.";
                    card.sourceName = "UK Tea & Infusions Association · The perfect brew";
                    card.sourceUrl = "https://www.tea.co.uk/make-a-perfect-brew";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "dried_leaf":
                    card.realWorldFact = "제조 중 수분을 줄입니다. 햇볕만 쓰지는 않습니다.";
                    card.gameplayConnection = "재료 확보을 추가 재료로 표현했습니다.";
                    card.sourceName = "UK Tea & Infusions Association · Processing and blending";
                    card.sourceUrl = "https://www.tea.co.uk/tea-processing-and-blending";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "oolong":
                    card.realWorldFact = "부분 산화한 차. 산화와 로스팅의 폭이 다양합니다. 효소적 산화는 미생물 발효와 다르며 산화 정도가 정확히 절반으로 고정되지 않습니다.";
                    card.gameplayConnection = "겹치는 풍미을 공격력으로 표현했습니다.";
                    card.sourceName = "Oregon State University · Tea";
                    card.sourceUrl = "https://lpi.oregonstate.edu/mic/food-beverages/tea";
                    card.additionalSources = "";
                    card.educationArtId = "card_oolong";
                    card.flavorText = card.realWorldFact;
                    break;
                case "brew_rhythm":
                    card.realWorldFact = "비교적 작은 다구(차를 우리고 담는 도구)로 잎 양과 우림 시간을 조절하며 여러 번 우리는 접근입니다. 하나로 고정된 레시피가 아니라 차와 도구에 맞게 조절합니다.";
                    card.gameplayConnection = "짧은 우림 반복을 공속으로 표현했습니다.";
                    card.sourceName = "TeaVivre · Gongfu brewing";
                    card.sourceUrl = "https://www.teavivre.com/info/common-gongfu-tea-brewing-mistakes-to-avoid.html";
                    card.additionalSources = "";
                    card.educationArtId = "card_gaiwan";
                    card.flavorText = card.realWorldFact;
                    break;
                case "second_infusion":
                    card.realWorldFact = "같은 잎을 다시 우려 풍미 변화를 살펴봅니다. 재우림 가능 횟수와 맛은 잎과 우림 조건에 따라 다릅니다.";
                    card.gameplayConnection = "한 번 더 이어짐을 연속 타격으로 표현했습니다.";
                    card.sourceName = "Ippodo Tea · Rebrewing tea leaves";
                    card.sourceUrl = "https://support.global.ippodo-tea.co.jp/hc/en-us/articles/4404037134991-Can-I-rebrew-my-tea-leaves";
                    card.additionalSources = "";
                    card.educationArtId = "card_gaiwan";
                    card.flavorText = card.realWorldFact;
                    break;
                case "tea_leaf_scatter":
                    card.realWorldFact = "서로 다른 차 또는 다른 재료를 섞어 풍미를 만듭니다.";
                    card.gameplayConnection = "여러 갈래 재료을 다중 발사로 표현했습니다.";
                    card.sourceName = "UK Tea & Infusions Association · Processing and blending";
                    card.sourceUrl = "https://www.tea.co.uk/tea-processing-and-blending";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "chamomile":
                    card.realWorldFact = "카모마일 꽃을 물에 우린 허브 인퓨전입니다. 인퓨전은 식물 재료를 물에 우린 음료를 뜻하며, 찻나무 잎으로 만든 차와 구별합니다.";
                    card.gameplayConnection = "쉬어 가는 인상을 게임 회복으로 표현했습니다.";
                    card.sourceName = "Twinings · Whole camomile buds";
                    card.sourceUrl = "https://twinings.co.uk/blogs/news/whole-camomile-buds";
                    card.additionalSources = "";
                    card.educationArtId = "card_chamomile";
                    card.flavorText = card.realWorldFact;
                    break;
                case "nettle_tea":
                    card.realWorldFact = "녹차의 떫은맛은 입안이 조이거나 마르는 듯한 감각을 말합니다. 마신 뒤 남는 감각을 여운으로 표현하며 독성을 뜻하지 않습니다.";
                    card.gameplayConnection = "남는 감각을 게임 지속 피해로 표현했습니다.";
                    card.sourceName = "Ippodo Tea · Tea bitterness and astringency";
                    card.sourceUrl = "https://ippodotea.com/blogs/frequently-asked-questions/help-my-tea-is-bitter";
                    card.additionalSources = "";
                    card.educationArtId = "card_green_tea";
                    card.flavorText = card.realWorldFact;
                    break;
                case "aged_puerh":
                    card.realWorldFact = "보이숙차는 수분과 열 조건에서 미생물 발효를 촉진하는 악퇴 가공을 거친 보이차입니다. 생차와 구별하며, 오래될수록 무조건 품질이 좋아지는 것은 아닙니다.";
                    card.gameplayConnection = "가공으로 형성된 풍미을 공격력으로 표현했습니다.";
                    card.sourceName = "TeaVivre · Raw and ripe Pu-erh processing";
                    card.sourceUrl = "https://www.teavivre.com/info/can-raw-pu-erh-tea-transform-into-ripe-pu-erh-through-aging.html";
                    card.additionalSources = "";
                    card.educationArtId = "card_puerh";
                    card.flavorText = card.realWorldFact;
                    break;
                case "golden_ratio":
                    card.realWorldFact = "찻잎량·물·온도·시간이 맛에 영향을 줍니다. 모든 차에 통하는 하나의 황금비는 없습니다.";
                    card.gameplayConnection = "조절의 균형을 공격·공속으로 표현했습니다.";
                    card.sourceName = "UK Tea & Infusions Association · The perfect brew";
                    card.sourceUrl = "https://www.tea.co.uk/make-a-perfect-brew";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "ginseng_root":
                    card.realWorldFact = "남아프리카의 Aspalathus linearis 잎과 줄기로 만드는 허브 인퓨전입니다. 찻나무 Camellia sinensis로 만든 차와 구별합니다.";
                    card.gameplayConnection = "쉬어 가는 인상을 게임 회복으로 표현했습니다.";
                    card.sourceName = "SANBI · Aspalathus linearis";
                    card.sourceUrl = "https://pza.sanbi.org/aspalathus-linearis";
                    card.additionalSources = "";
                    card.educationArtId = "card_rooibos";
                    card.flavorText = card.realWorldFact;
                    break;
                case "nightshade_blend":
                    card.realWorldFact = "홍차는 찻나무 잎을 효소적으로 산화해 만드는 차입니다. 떫은맛과 잔향은 원료와 우림 조건에 따라 달라지며 홍차가 언제나 더 떫은 것은 아닙니다.";
                    card.gameplayConnection = "남는 감각을 게임 지속 피해로 표현했습니다.";
                    card.sourceName = "Oregon State University · Tea; Ippodo · Astringency";
                    card.sourceUrl = "https://lpi.oregonstate.edu/mic/food-beverages/tea";
                    card.additionalSources = "떫은맛과 우림: Ippodo Tea\nhttps://ippodotea.com/blogs/frequently-asked-questions/help-my-tea-is-bitter";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "piercing_arrowhead":
                    card.realWorldFact = "우린 액체와 찻잎을 분리하는 도구입니다.";
                    card.gameplayConnection = "차탕의 통과을 관통으로 표현했습니다.";
                    card.sourceName = "Ippodo Tea · Serving tea with a strainer";
                    card.sourceUrl = "https://global.ippodo-tea.co.jp/blogs/tea-recipe/preparing-different-teas-in-the-tea-bowl-with-serving-spout";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                case "hybrid_tea_martini":
                    card.realWorldFact = "차를 우린 베르무트 등으로 차 향을 더하는 마티니 변형이 있으며 레시피는 다양합니다. 티니 보우는 두 테마를 결합한 게임 속 창작 보조 활입니다.";
                    card.gameplayConnection = "두 테마의 결합을 보조 활로 표현했습니다.";
                    card.sourceName = "Diageo Bar Academy · Marteani";
                    card.sourceUrl = "https://www.diageobaracademy.com/en-zz/home/explore-all-recipes/marteani";
                    card.additionalSources = "";
                    card.educationArtId = "";
                    card.flavorText = card.realWorldFact;
                    break;
                default: throw new ArgumentException("Unknown educational card: " + card.id);
            }
        }
        public const string SymbolNotice = "효과 수치는 게임의 상징적 표현이며 섭취에 따른 건강 효능이나 술의 도수·우열을 뜻하지 않습니다.";
        public static string MaxEffect(CardData c) { var form = AttackFinalForms.ForCard(c.id); return form.HasValue ? "\n" + AttackFinalForms.Description(form.Value) : ""; }
        public static string Detail(CardData c) => "게임 효과\n" + c.description + "\n최대 중첩 " + c.maxStacks + MaxEffect(c) + "\n\n음료 지식\n" + c.realWorldFact + "\n\n게임 해석\n" + c.gameplayConnection + "\n\n" + SymbolNotice + "\n\n출처: " + c.sourceName + "\n" + c.sourceUrl + (string.IsNullOrEmpty(c.additionalSources) ? "" : "\n\n" + c.additionalSources);
    }
}
