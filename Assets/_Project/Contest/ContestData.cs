using System.Collections.Generic;

namespace CCT.Contest
{
    /// <summary>공모전판(음료 마스터: 공공조달 체험편)의 정적 데이터. 기관·행사·업체·수치는 모두 창작 값이며
    /// 실제 정부 사업·입찰·업체가 아니다. 모든 플레이 수치는 정수.</summary>
    public enum Product { GreenTea, Chamomile, BlackTea, Coffee }
    public enum Temp { T70, T80, T95 }
    public enum Vessel { TeaCup, Mug, CoffeeCup }
    public enum PackDefect { None, Damaged, NoOriginLabel, WrongProduct }

    public static class ContestText
    {
        public static string Name(Product p) => p switch
        {
            Product.GreenTea => "녹차",
            Product.Chamomile => "캐모마일 차",
            Product.BlackTea => "홍차",
            _ => "핸드드립 커피",
        };

        public static string Name(Temp t) => t switch { Temp.T70 => "70℃", Temp.T80 => "80℃", _ => "95℃" };
        public static string Name(Vessel v) => v switch { Vessel.TeaCup => "찻잔", Vessel.Mug => "머그", _ => "커피잔" };

        public static string Name(PackDefect d) => d switch
        {
            PackDefect.Damaged => "포장 파손",
            PackDefect.NoOriginLabel => "원산지 표시 없음",
            PackDefect.WrongProduct => "다른 품목 오배송",
            _ => "이상 없음",
        };
    }

    public sealed class SupplyOffer
    {
        public string Supplier;   // 가상의 공급업체명
        public Product Product;
        public int UnitsPerPack;  // 1단위 = 1잔 분량
        public int PricePerPack;  // 게임 코인
        public bool OriginLabeled;
        public bool ContainsCaffeine;
        public string Note;
    }

    /// <summary>입고 시 정상 포장 사이에 섞여 들어오는 결함 포장(추가 배송분, 대금 없음). Position은 정상 포장 몇 개 뒤에 오는지.</summary>
    public struct DefectSpec
    {
        public int Position;
        public PackDefect Defect;
        public Product WrongProduct;
    }

    public sealed class Mission
    {
        public string Id;
        public string Agency;     // 가상의 수요기관
        public string EventName;  // 가상의 행사
        public string Purpose;
        public Product Product;
        public int Quantity;
        public Temp Temp;
        public Vessel Vessel;
        public bool RequireOriginLabel;
        public bool RequireCaffeineFree;
        public int DeadlineMinutes;
        public int Budget;
        public SupplyOffer[] Offers;
        public DefectSpec[] Defects;
        public string[] Lessons;

        /// <summary>요청서의 확인 항목. 플레이어가 모두 확인해야 공급안 비교로 넘어간다.</summary>
        public List<string> Conditions()
        {
            var list = new List<string>
            {
                "품명: " + ContestText.Name(Product),
                "수량: " + Quantity + "잔",
                "규격(온도): " + ContestText.Name(Temp),
                "규격(용기): " + ContestText.Name(Vessel),
            };
            if (RequireOriginLabel) list.Add("규격(표시): 원산지 표시 필수");
            if (RequireCaffeineFree) list.Add("규격(성분): 카페인 없음");
            list.Add("납기: 작업 시작 후 " + DeadlineMinutes + "분 이내");
            list.Add("예산: " + Budget + "코인 이내");
            return list;
        }
    }

    public static class ContestMissions
    {
        public const int BrewMinutes = 10;
        public const int OrderMinutes = 10;
        public const int ReceiveMinutes = 5;
        public const int ResendMinutes = 5;
        public const int InspectMinutes = 5;
        public const int MaxCupsPerBatch = 4;

        static Mission[] all;

        public static Mission[] All => all ??= Build();

        public static Mission Get(int index) => All[index];

        static Mission[] Build() => new[]
        {
            new Mission
            {
                Id = "m1_library",
                Agency = "햇살구 작은도서관(가상)",
                EventName = "가을 독서 모임",
                Purpose = "독서 모임 참가자에게 따뜻한 녹차를 제공합니다.",
                Product = Product.GreenTea, Quantity = 4, Temp = Temp.T80, Vessel = Vessel.TeaCup,
                DeadlineMinutes = 80, Budget = 12,
                Offers = new[]
                {
                    new SupplyOffer { Supplier = "초록잎상회", Product = Product.GreenTea, UnitsPerPack = 2, PricePerPack = 3, OriginLabeled = true, Note = "소포장" },
                    new SupplyOffer { Supplier = "새벽차방", Product = Product.BlackTea, UnitsPerPack = 4, PricePerPack = 2, OriginLabeled = true, ContainsCaffeine = true, Note = "가장 저렴" },
                    new SupplyOffer { Supplier = "들판농원", Product = Product.GreenTea, UnitsPerPack = 5, PricePerPack = 8, OriginLabeled = true, Note = "대용량" },
                },
                Defects = new[] { new DefectSpec { Position = 1, Defect = PackDefect.Damaged } },
                Lessons = new[]
                {
                    "요청서의 품명·수량·규격·납기·예산을 먼저 확인했습니다.",
                    "가장 저렴한 공급안이라도 요청 품명과 다르면 고를 수 없었습니다.",
                    "검수에서 조건과 다른 물품은 인수되지 않고 보완이 필요했습니다.",
                },
            },
            new Mission
            {
                Id = "m2_center",
                Agency = "물빛동 주민센터(가상)",
                EventName = "어르신 건강 강좌",
                Purpose = "강좌 참가자에게 카페인 없는 따뜻한 차를 제공합니다.",
                Product = Product.Chamomile, Quantity = 6, Temp = Temp.T95, Vessel = Vessel.Mug,
                RequireCaffeineFree = true,
                DeadlineMinutes = 70, Budget = 14,
                Offers = new[]
                {
                    new SupplyOffer { Supplier = "들꽃허브", Product = Product.Chamomile, UnitsPerPack = 3, PricePerPack = 5, OriginLabeled = true, Note = "단일 원료" },
                    new SupplyOffer { Supplier = "노을블렌드", Product = Product.Chamomile, UnitsPerPack = 6, PricePerPack = 4, OriginLabeled = true, ContainsCaffeine = true, Note = "홍차 혼합 블렌드" },
                    new SupplyOffer { Supplier = "바람농장", Product = Product.Chamomile, UnitsPerPack = 2, PricePerPack = 3, OriginLabeled = true, Note = "소포장" },
                },
                Defects = new[] { new DefectSpec { Position = 1, Defect = PackDefect.WrongProduct, WrongProduct = Product.BlackTea } },
                Lessons = new[]
                {
                    "성분 조건(카페인 없음)도 규격의 일부로 확인했습니다.",
                    "필요 수량을 채우려면 포장 단위와 개수를 함께 계산해야 했습니다.",
                    "입고 단계에서 오배송 물품을 반품해 재료 이력을 지켰습니다.",
                },
            },
            new Mission
            {
                Id = "m3_volunteer",
                Agency = "바람시 자원봉사센터(가상)",
                EventName = "시민 자원봉사자 감사 행사",
                Purpose = "행사 참석자에게 원산지가 표시된 원두로 내린 커피를 제공합니다.",
                Product = Product.Coffee, Quantity = 8, Temp = Temp.T95, Vessel = Vessel.CoffeeCup,
                RequireOriginLabel = true,
                DeadlineMinutes = 60, Budget = 16,
                Offers = new[]
                {
                    new SupplyOffer { Supplier = "언덕로스터", Product = Product.Coffee, UnitsPerPack = 4, PricePerPack = 6, OriginLabeled = true, ContainsCaffeine = true, Note = "원산지 표시" },
                    new SupplyOffer { Supplier = "항구상사", Product = Product.Coffee, UnitsPerPack = 5, PricePerPack = 3, OriginLabeled = false, ContainsCaffeine = true, Note = "가장 저렴" },
                    new SupplyOffer { Supplier = "솔밭커피", Product = Product.Coffee, UnitsPerPack = 10, PricePerPack = 18, OriginLabeled = true, ContainsCaffeine = true, Note = "대용량" },
                },
                Defects = new[]
                {
                    new DefectSpec { Position = 0, Defect = PackDefect.NoOriginLabel },
                    new DefectSpec { Position = 1, Defect = PackDefect.Damaged },
                },
                Lessons = new[]
                {
                    "표시 조건(원산지)을 충족하지 못한 공급안은 가격과 무관하게 제외했습니다.",
                    "예산을 넘는 공급안은 규격을 충족해도 고를 수 없었습니다.",
                    "납기 안에 제조·검수·보완을 마치도록 작업 순서를 계획했습니다.",
                },
            },
        };
    }
}
