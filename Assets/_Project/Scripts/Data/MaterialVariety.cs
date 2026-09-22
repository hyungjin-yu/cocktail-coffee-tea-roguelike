using System.Collections.Generic;

namespace CCT
{
    /// <summary>
    /// 재료(칵테일 재료/찻잎/커피 원두) 품종 하나. 아이콘은 재료 종류 공용 아이콘을 그대로 쓰고
    /// 이름/풍미만 다르다 — 드랍된 재료를 주울 때마다 그 종류 안에서 품종이 무작위로 하나 골라지고,
    /// 처음 나온 품종은 도감에 영구히 기록된다 (PermanentProgress.discoveredMaterialVarietyIds).
    /// </summary>
    public class MaterialVariety
    {
        public string id;
        public string displayName;
        public string flavorText;
    }

    public static class MaterialVarieties
    {
        public static readonly List<MaterialVariety> Cocktail = new List<MaterialVariety>
        {
            Make("gin", "진", "노간주나무 향이 진한 증류주. 클래식 칵테일의 근본."),
            Make("rum", "럼", "사탕수수로 만든 달콤쌉싸름한 증류주."),
            Make("vodka", "보드카", "무색무취에 가까워 어디에든 잘 섞이는 증류주."),
            Make("tequila", "데킬라", "용설란(아가베)으로 만드는 멕시코의 증류주."),
            Make("vermouth", "베르무트", "허브·향신료를 더한 가향 와인. 마티니의 숨은 주역."),
            Make("triple_sec", "트리플 섹", "오렌지 껍질로 향을 낸 달콤한 리큐르."),
        };

        public static readonly List<MaterialVariety> Tea = new List<MaterialVariety>
        {
            Make("green", "녹차", "발효하지 않은 찻잎. 산뜻하고 풋풋한 첫 맛."),
            Make("black", "홍차", "완전히 발효시켜 진하고 깊은 향이 나는 찻잎."),
            Make("oolong", "우롱차", "녹차와 홍차 사이, 반쯤 발효시킨 차."),
            Make("white", "백차", "가장 여리게 덖은, 은은하고 섬세한 찻잎."),
            Make("puerh", "보이차", "오래 묵혀 숙성시키는 흑차. 깊고 구수한 향."),
            Make("jasmine", "자스민차", "재스민 꽃향을 입힌 향긋한 찻잎."),
        };

        public static readonly List<MaterialVariety> Coffee = new List<MaterialVariety>
        {
            Make("ethiopia", "에티오피아 원두", "커피의 원산지. 꽃향과 과일향이 화사하다."),
            Make("colombia", "콜롬비아 원두", "균형 잡힌 산미와 단맛으로 무난하게 인기 있는 원두."),
            Make("brazil", "브라질 원두", "고소하고 묵직한 바디감의 대중적인 원두."),
            Make("guatemala", "과테말라 원두", "초콜릿 같은 단맛과 스모키한 향이 특징."),
            Make("kenya", "케냐 원두", "선명한 산미와 베리류 향이 도드라지는 원두."),
            Make("yirgacheffe", "예가체프 원두", "에티오피아산 중에서도 손꼽히는 향미의 원두."),
        };

        static MaterialVariety Make(string id, string displayName, string flavorText) =>
            new MaterialVariety { id = id, displayName = displayName, flavorText = flavorText };

        public static List<MaterialVariety> For(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Tea: return Tea;
                case IngredientType.Coffee: return Coffee;
                default: return Cocktail;
            }
        }
    }
}
