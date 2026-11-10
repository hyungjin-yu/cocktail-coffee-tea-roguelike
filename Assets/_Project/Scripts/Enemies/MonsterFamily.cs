using System.Collections.Generic;

namespace CCT
{
    /// <summary>2026-11-10 몬스터 계열(음료 순환 상성 대상). 이름 문자열·dropType·플레이어 직업으로 추측하지 않고 아래 명시 표로만 정한다.</summary>
    public enum MonsterFamily { Neutral = 0, Cocktail = 1, Coffee = 2, Tea = 3 }

    /// <summary>
    /// 기존 적 전체의 명시 계열 표(테마 기준). 표에 없는 정의(테스트용 임시 정의 등)는 중립.
    /// 테마가 확실하지 않은 적(손님 보스 2종)은 중립으로 남긴다.
    /// 주의: 찻집 변형(TeaVariant)은 원본 커피 보스/몹을 복제하지만 외형·이름이 차 테마이므로 차 계열이다(드롭 재료와 무관).
    /// </summary>
    public static class MonsterFamilies
    {
        public struct Row
        {
            public EnemyDef def;
            public MonsterFamily family;
            public string reason;
        }

        static List<Row> rows;
        static Dictionary<EnemyDef, MonsterFamily> map;

        public static IReadOnlyList<Row> Table { get { Build(); return rows; } }

        static void Build()
        {
            if (map != null) return;
            rows = new List<Row>
            {
                // 칵테일 트랙(바)
                R(EnemyDef.Imp, MonsterFamily.Cocktail, "라임 웨지 — 칵테일 가니시"),
                R(EnemyDef.Brute, MonsterFamily.Cocktail, "레몬 브루트 — 칵테일 시트러스"),
                R(EnemyDef.ShakerGolem, MonsterFamily.Cocktail, "쉐이커 — 칵테일 도구"),
                R(EnemyDef.OliveBomber, MonsterFamily.Cocktail, "올리브 — 마티니 가니시"),
                R(EnemyDef.SwirlConjuror, MonsterFamily.Cocktail, "스월 스틱 — 칵테일 도구"),
                R(EnemyDef.MartiniSpirit, MonsterFamily.Cocktail, "마티니 — 칵테일(정예)"),
                // 차 트랙(바의 차 몹)
                R(EnemyDef.TeabagImp, MonsterFamily.Tea, "티백"),
                R(EnemyDef.KettleCannon, MonsterFamily.Tea, "주전자"),
                R(EnemyDef.SugarCubeBomber, MonsterFamily.Tea, "각설탕 — 티 테이블"),
                R(EnemyDef.LeafWarden, MonsterFamily.Tea, "찻잎"),
                R(EnemyDef.TeaCeremonyGuardian, MonsterFamily.Tea, "다도 — 차(정예)"),
                // 찻집 전용 변형
                R(EnemyDef.SpringLeafSprite, MonsterFamily.Tea, "새싹 찻잎"),
                R(EnemyDef.BambooScoopArcher, MonsterFamily.Tea, "차시(대나무 숟가락)"),
                R(EnemyDef.TeaLeafScholar, MonsterFamily.Tea, "차 연구 정령 — 원두를 드롭하지만 외형·이름은 차"),
                // 커피 트랙
                R(EnemyDef.EspressoBug, MonsterFamily.Coffee, "에스프레소"),
                R(EnemyDef.MilkFoamer, MonsterFamily.Coffee, "우유 거품 — 커피"),
                // 보스
                R(EnemyDef.GrinderGolem, MonsterFamily.Coffee, "원두 그라인더"),
                R(EnemyDef.DripServerSpecter, MonsterFamily.Coffee, "드립 서버"),
                R(EnemyDef.EspressoLeviathan, MonsterFamily.Coffee, "에스프레소 머신"),
                R(EnemyDef.GaiwanSentinel, MonsterFamily.Tea, "개완(차 그릇)"),
                R(EnemyDef.SteamKettleElder, MonsterFamily.Tea, "찻주전자"),
                R(EnemyDef.TeaKilnDragon, MonsterFamily.Tea, "차 화로"),
                R(EnemyDef.DifficultCustomer, MonsterFamily.Neutral, "손님 — 음료 테마 아님"),
                R(EnemyDef.TeaCustomer, MonsterFamily.Neutral, "손님 — 음료 테마 아님(진상 손님과 같은 분류)"),
            };
            map = new Dictionary<EnemyDef, MonsterFamily>();
            foreach (var r in rows) map[r.def] = r.family;
        }

        static Row R(EnemyDef def, MonsterFamily f, string reason) => new Row { def = def, family = f, reason = reason };

        public static MonsterFamily Of(EnemyDef def)
        {
            if (def == null) return MonsterFamily.Neutral;
            Build();
            return map.TryGetValue(def, out var f) ? f : MonsterFamily.Neutral;
        }

        public static bool IsClassified(EnemyDef def) { Build(); return def != null && map.ContainsKey(def); }

        public static string Name(MonsterFamily f) => f switch
        {
            MonsterFamily.Cocktail => "칵테일",
            MonsterFamily.Coffee => "커피",
            MonsterFamily.Tea => "차",
            _ => "중립",
        };

        /// <summary>색만으로 구분하지 않도록 계열마다 다른 글자 표식.</summary>
        public static string Mark(MonsterFamily f) => f switch
        {
            MonsterFamily.Cocktail => "[칵]",
            MonsterFamily.Coffee => "[커]",
            MonsterFamily.Tea => "[차]",
            _ => "[중]",
        };
    }

    /// <summary>
    /// 음료 순환 상성(기획/음료 순환 상성 추가 피해.md). 실제로 마셔서 이번 런에 적용 중인 음료만 기준.
    /// 칵테일 → 커피 몬스터, 커피 → 차 몬스터, 차 → 칵테일 몬스터. 불리 감소 없음, 중립은 0.
    /// B = 기존 계산이 끝난 유효 직접 공격의 정수 피해, A = max(1, round(B×Percent/100)), T = B + A.
    /// </summary>
    public static class DrinkAffinity
    {
        /// <summary>조정 가능한 명세 기본값(사용자 확정값 아님).</summary>
        public static int BonusPercent = DefaultBonusPercent;
        public const int DefaultBonusPercent = 20;

        public static MonsterFamily TargetOf(DrinkKind? drink) => drink switch
        {
            DrinkKind.Cocktail => MonsterFamily.Coffee,
            DrinkKind.Coffee => MonsterFamily.Tea,
            DrinkKind.Tea => MonsterFamily.Cocktail,
            _ => MonsterFamily.Neutral,
        };

        public static bool Matches(DrinkKind? drink, MonsterFamily target) =>
            target != MonsterFamily.Neutral && TargetOf(drink) == target;

        /// <summary>추가 피해 A(정수). 미일치·0 이하 피해·비율 0 이하면 0. 오버플로 없이 int 상한에서 멈춘다.</summary>
        public static int Bonus(int baseDamage, DrinkKind? drink, MonsterFamily target)
        {
            if (baseDamage <= 0 || BonusPercent <= 0 || !Matches(drink, target)) return 0;
            double a = System.Math.Round(baseDamage * (double)BonusPercent / 100d, System.MidpointRounding.AwayFromZero);
            if (a > int.MaxValue) return int.MaxValue;
            return System.Math.Max(1, (int)a);
        }

        /// <summary>출전 전/버프 상세 설명용: "커피 계열 추가 피해 +20%". 무음료면 null.</summary>
        public static string Describe(DrinkKind? drink)
        {
            var t = TargetOf(drink);
            return t == MonsterFamily.Neutral ? null : $"{MonsterFamilies.Name(t)} 계열 추가 피해 +{BonusPercent}%";
        }

        /// <summary>런 첫 안내: "음료 상성 · [커] 커피 계열 몬스터 추가 피해 +20%". 무음료면 null.</summary>
        public static string GuideLine(DrinkKind? drink)
        {
            var t = TargetOf(drink);
            return t == MonsterFamily.Neutral ? null : $"{MonsterFamilies.Mark(t)} {MonsterFamilies.Name(t)} 계열 몬스터 추가 피해 +{BonusPercent}%";
        }

        public const string CycleLine = "순환 상성: 칵테일 → 커피 몬스터 · 커피 → 차 몬스터 · 차 → 칵테일 몬스터";
    }
}
