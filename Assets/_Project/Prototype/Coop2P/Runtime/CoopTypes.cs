using UnityEngine;

namespace CCT.Prototype.Coop2P
{
    // 2026-11-17 로컬 2인 협동 최소 실험판 — 런 메모리 전용(영구 저장·상점·보상 없음).
    public enum CoopRole { Brew, Fight }
    public enum Ingredient { Lime = 0, Bean = 1, Leaf = 2 }
    public enum CoopDrink { LeafTonic = 0, RushShot = 1, CitrusBurst = 2 }
    public enum CoopOutcome { Running, Success, Failed }
    public enum CupPlace { Station, Counter, Hand, Consumed }

    public enum CoopAction
    {
        None,
        RejectedDowned,
        Placed,
        Given,
        HoldingNoTarget,
        RejectedCounterFull,
        TakenFromStation,
        TakenFromCounter,
        BrewStarted,
        BrewCancelled,
        RejectedBusy,
        RejectedInsufficient,
        RejectedNotRunning,
    }

    public enum CoopEventKind
    {
        Hit, Kill, IngredientGained, IngredientOverflow, BrewStart, BrewCancel, BrewDone, CupPlaced, CupTaken, CupGiven,
        Drank, Rejected, Downed, Revived, RoleChanged, StageUp, Outcome, Shockwave, EnemySpawn,
    }

    public struct CoopEvent
    {
        public CoopEventKind Kind;
        public int Player;      // -1 = 없음
        public Vector2 Pos;
        public Vector2 From;
        public int A;           // 재료/음료/단계 등 종류 인덱스
        public CoopAction Action;
    }

    public struct PlayerInput
    {
        public Vector2 Move;
        public bool Interact;   // 이번 프레임 눌림(엣지)
        public bool Drink;      // 이번 프레임 눌림(엣지)
    }

    public sealed class CoopRecipe
    {
        public CoopDrink Drink;
        public string Name;
        public string Effect;
        public int Lime, Bean, Leaf;
        public float BrewSeconds;

        public int Cost(Ingredient i) => i == Ingredient.Lime ? Lime : i == Ingredient.Bean ? Bean : Leaf;
    }

    /// <summary>실험판 수치(보고서 표와 1:1). 원래 게임 난이도/저장 시스템은 쓰지 않는다.</summary>
    public static class CoopTuning
    {
        public const float RunSeconds = 300f;

        // 2026-11-18 증축 맵: 홀은 11-17 실험판과 같은 크기(18.4x5.1), 바 카운터 뒤(위)로 주방을 증축해 월드를 넓힌다.
        // 배경 승인 아트는 원본 배율(PPU64, 1u=64아트px) 그대로 — 11-17 바닥 타일과 같은 체감 크기, 늘림 없음.
        public const float ArtScale = 1f;
        public const float ArenaMinX = -9.2f, ArenaMaxX = 9.2f, ArenaMinY = -4.4f;
        /// <summary>사람 발 위치 위 끝(주방 통로 안쪽, 작업대 앞).</summary>
        public const float ArenaMaxY = 4.5f;
        /// <summary>월드 위 끝(주방 뒷벽 꼭대기). 카메라가 이 위로는 보여 주지 않는다.</summary>
        public const float MapTopY = 7.7f;
        /// <summary>월드 아래 끝(홀 바닥 아래 테두리).</summary>
        public const float MapBottomY = -4.5f;
        // 바 카운터 띠(경계): 카운터 앞(y≤0.7)은 홀 바닥, 카운터 뒤(y≥1.7)는 주방 바닥. 가운데 문 폭 2.4.
        public const float CounterMinY = 0.7f, CounterMaxY = 1.7f, DoorHalfWidth = 1.2f;
        /// <summary>표시·판정용 경계 중앙(문 통로 가운데).</summary>
        public const float BoundaryY = 1.2f;
        /// <summary>역할 전환: 주방 바닥(카운터 뒤)에 들어서면 제조, 홀 바닥(카운터 앞)에 들어서면 전투. 문 통로 안에서는 직전 역할 유지.</summary>
        public const float RoleBrewY = CounterMaxY, RoleFightY = CounterMinY;
        /// <summary>적이 갈 수 있는 위 끝(중심): 홀 바닥 안.</summary>
        public const float EnemyMaxY = CounterMinY - 0.3f;
        public static readonly Vector2 PassCounterPos = new Vector2(3.2f, 1.2f);
        public static readonly Vector2 PassStandKitchen = new Vector2(3.2f, 2.1f), PassStandHall = new Vector2(3.2f, 0.3f);
        public const int PassCounterCapacity = 2;

        public const float PlayerRadius = 0.3f, PlayerSpeed = 3.6f, PlayerMaxHp = 100f, InteractRange = 1.25f;
        public static readonly Vector2 P1Start = new Vector2(-1.8f, 2.5f);
        public static readonly Vector2 P2Start = new Vector2(1.8f, 2.5f);

        /// <summary>적 진입점(홀 좌·우 벽 각 2곳). 그림의 입구 기둥 한 쌍 사이와 같다.</summary>
        public static readonly Vector2[] Entrances =
        {
            new Vector2(-9.2f, -1.0f), new Vector2(-9.2f, -3.2f),
            new Vector2(9.2f, -1.0f), new Vector2(9.2f, -3.2f),
        };

        public const float AttackInterval = 0.55f, AttackRange = 3.2f, AttackDamage = 14f;

        // 음료 효과 — 회복 / 공격 지원 / 위기 대응
        public const float TonicHeal = 50f, TonicSeconds = 2.5f;                              // 2.5초에 걸쳐 50 회복(초과분 소멸)
        public const float RushSeconds = 10f, RushInterval = 0.22f, RushRange = 4.2f, RushSpeedMul = 1.25f; // 재음용은 10초로 갱신(누적 없음)
        public const float BurstRadius = 4f, BurstDamage = 30f, BurstKnock = 18f, BurstStun = 2f, BurstInvuln = 1.5f;

        public const float ReviveRange = 1.3f, ReviveSeconds = 2f, AutoReviveSeconds = 12f, ReviveHp = 50f, AutoReviveHp = 35f, ReviveInvuln = 1.5f;

        public const float EnemyRadius = 0.3f, ContactDps = 10f;
        public const int ContactCap = 3;          // 한 사람에게 동시에 피해를 주는 적 최대 3
        public const int IngredientCap = 9;

        public const float StageSeconds = 60f;
        public static readonly float[] SpawnInterval = { 2.6f, 2.1f, 1.7f, 1.4f, 1.1f };
        public static readonly int[] MaxAlive = { 8, 12, 16, 20, 24 };
        public static readonly float[] EnemySpeed = { 1.2f, 1.35f, 1.5f, 1.65f, 1.8f };
        public static readonly float[] EnemyHp = { 30f, 34f, 38f, 44f, 50f };
        public static readonly int[] StageBurst = { 0, 3, 4, 5, 6 };

        /// <summary>처치 보장 지급 순서: 원두 → 찻잎 → 라임 → 반복(공유 재고).</summary>
        public static readonly Ingredient[] DropCycle = { Ingredient.Bean, Ingredient.Leaf, Ingredient.Lime };

        public static readonly CoopRecipe[] Recipes =
        {
            new CoopRecipe { Drink = CoopDrink.LeafTonic, Name = "찻잎 토닉", Effect = "회복 +50 (2.5초)", Leaf = 2, BrewSeconds = 3f },
            new CoopRecipe { Drink = CoopDrink.RushShot, Name = "러시 샷", Effect = "10초 연사·사거리·이속", Bean = 2, BrewSeconds = 3f },
            new CoopRecipe { Drink = CoopDrink.CitrusBurst, Name = "시트러스 버스트", Effect = "주변 충격파·기절 2초", Lime = 2, Bean = 1, BrewSeconds = 4.5f },
        };

        /// <summary>작업대 앞면 가운데(상호작용 기준점). 작업대 몸체는 이 점에서 위로 StationDepth, 좌우 StationHalfWidth.</summary>
        public static readonly Vector2[] StationPos =
        {
            new Vector2(-6.6f, 4.5f), // 찻잎 토닉
            new Vector2(-2.2f, 4.5f), // 러시 샷
            new Vector2(6.6f, 4.5f),  // 시트러스 버스트
        };
        public const float StationHalfWidth = 1.8f, StationDepth = 1.2f;
        public static Vector2 StationStand(CoopDrink d) => StationPos[(int)d] + new Vector2(0f, -0.45f);

        public static CoopRecipe Recipe(CoopDrink d) => Recipes[(int)d];
    }
}
