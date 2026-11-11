using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    public enum BossKind { None, Mid, Enhanced, Final }

    /// <summary>
    /// 몹 행동 아키타입 ([[01_몹 디자인 - 칵테일 & 차]] 0장 공통 설계 원칙).
    /// Rush 는 기본 추적이라 별도 드라이버가 필요 없고, 나머지는 EnemyRoster.AttachBehavior 가
    /// 스폰 시점에 맞는 드라이버 컴포넌트를 붙여준다.
    /// </summary>
    public enum EnemyArchetype { Rush, Ranged, Suicide, Summoner, EliteBarrage }

    /// <summary>몹 정의 (가안 수치). 수치와 아키타입은 [[01_몹 디자인 - 칵테일 & 차]] 문서를 따른다.</summary>
    public class EnemyDef
    {
        public string name;
        public Color color;
        public float radius;
        public float hp;
        public float speed;
        public float contactDamage;
        public IngredientType dropType = IngredientType.Cocktail;
        /// <summary>떨어뜨리는 픽업 개수.</summary>
        public int dropCount = 1;
        /// <summary>픽업 1개가 주는 재료량.</summary>
        public int ingredientAmount;
        public BossKind boss = BossKind.None;

        /// <summary>Resources 경로 (예: "Icons/Enemies/lime_wedge"). 없으면 Capsule placeholder + color.</summary>
        public string spriteResourcePath;
        /// <summary>진상 손님 전용: HP 50% 미만("Enraged") 진입 시 바꿔 낄 2페 아트. 없으면 항상 기본 아트 유지.</summary>
        public string enragedSpriteResourcePath;
        /// <summary>아이콘이 있을 때만 적용되는 시각 배율(콜라이더는 영향 없음, def.radius 그대로).
        /// 이미지마다 캔버스 안에서 캐릭터가 차지하는 비율이 달라서(칵테일/차 로스터 원화는 여백이
        /// 있어 2배를 줘야 체감 크기가 맞고, 보스/엘리트 신규 아트는 캔버스를 거의 꽉 채워서 그대로
        /// 1배가 맞다) — 알파 채널을 읽어 자동으로 계산해봤지만(연기/오라 효과가 캔버스 가장자리까지
        /// 닿는 이미지가 많아) 신뢰할 수 없어서 이미지를 직접 보고 수동으로 값을 정한다.</summary>
        public float iconVisualScale = 1f;

        public EnemyArchetype archetype = EnemyArchetype.Rush;

        /// <summary>true 면 플레이어에 닿는 즉시 한 방 터지고 자폭한다 (Suicide 아키타입).</summary>
        public bool explodesOnContact;
        /// <summary>자폭 후 남기는 장판 (0 이면 장판 없음).</summary>
        public float burstZoneRadius;
        public float burstZoneDamagePerSecond;
        public float burstZoneSeconds;
        /// <summary>true면 장판이 피해(BurstZone) 대신 둔화(SlowZone)를 준다 (슈가 큐브 바머 전용 — 차 트랙 차별화).</summary>
        public bool burstZoneSlows;
        public float burstSlowMultiplier = 0.5f;

        // Ranged / EliteBarrage 공용 (RangedAttackDriver)
        /// <summary>이 거리를 유지하며(너무 가까우면 물러남, 너무 멀면 접근) 원거리 공격.</summary>
        public float keepDistance;
        public float projectileSpeed = 6f;
        public float projectileDamage;
        public float attackInterval = 2f;
        /// <summary>발사체 아이콘 Resources 경로. 없으면 색상 원(SpriteFactory.Circle) placeholder.</summary>
        public string projectileSpriteResourcePath;
        /// <summary>1 이면 플레이어를 조준한 단발, 그 이상이면 원형으로 흩뿌리는 탄막.</summary>
        public int barrageCount = 1;

        // Summoner 전용 (SummonerDriver)
        public EnemyDef summonDef;
        public float summonInterval = 6f;

        public bool IsBoss => boss != BossKind.None;

        /// <summary>2026-11-11 마스터 도전: 이 정의만의 추가 시각 배율(작은 진상 손님 등). 콜라이더(radius)와 별개. 기본 1 = 기존 그대로.</summary>
        public float visualScaleMultiplier = 1f;
        /// <summary>2026-11-11 원본 공유 정의를 바꾸지 않는 별도 사본(마스터 도전 전용 정의 생성용).</summary>
        public EnemyDef CloneDef() => (EnemyDef)MemberwiseClone();

        /// <summary>04 §5 아트 ID(ArtLibrary 몸체 조회용). 비워 두면 spriteResourcePath 파일명(_bgfix 제거)에서 유도.</summary>
        public string artId;

        public string ArtId
        {
            get
            {
                if (!string.IsNullOrEmpty(artId)) return artId;
                if (string.IsNullOrEmpty(spriteResourcePath)) return null;
                var file = spriteResourcePath.Substring(spriteResourcePath.LastIndexOf('/') + 1);
                return file.EndsWith("_bgfix") ? file.Substring(0, file.Length - "_bgfix".Length) : file;
            }
        }

        /// <summary>돌진형(Rush) 기본 잡몹. MVP 필수.</summary>
        public static readonly EnemyDef Imp = new EnemyDef
        {
            name = "라임웨지",
            color = new Color(0.55f, 0.85f, 0.25f),
            radius = 0.35f,
            hp = 20f,
            speed = 2.6f,
            contactDamage = 6f,
            ingredientAmount = 1,
            spriteResourcePath = "Icons/Enemies/lime_wedge",
            iconVisualScale = 2f,
        };

        /// <summary>덩치 큰 돌진형(칵테일 트랙 전용, 다양성용). "디자인 문서에 없어서 아트가 없는 게
        /// 정상"이라던 예전 주석은 틀렸다 — 06 로스터 표에 lemon_brute로 정식 등재돼 있고, 04 아트
        /// 제작 기준도 이 몹을 필수 아트로 명시한다(2026-09-25 Orca 감사로 정정). 아트는 아직 Codex가
        /// 만들지 않아 placeholder 유지 — "필요 없음"이 아니라 "아직 없음".</summary>
        public static readonly EnemyDef Brute = new EnemyDef
        {
            name = "레몬 브루트",
            color = new Color(0.95f, 0.6f, 0.25f),
            radius = 0.6f,
            hp = 70f,
            speed = 1.8f,
            contactDamage = 12f,
            ingredientAmount = 3,
            artId = "lemon_brute",
        };

        /// <summary>투척형(Ranged). 거리를 유지하며 얼음 조각을 던진다. Imp 와 함께 MVP 필수 2종.</summary>
        public static readonly EnemyDef ShakerGolem = new EnemyDef
        {
            name = "쉐이커 골렘",
            color = new Color(0.62f, 0.64f, 0.68f),
            radius = 0.45f,
            hp = 55f,
            speed = 2.0f,
            contactDamage = 5f,
            ingredientAmount = 2,
            spriteResourcePath = "Icons/Enemies/shaker_golem",
            archetype = EnemyArchetype.Ranged,
            keepDistance = 4.5f,
            projectileSpeed = 6f,
            projectileDamage = 10f,
            attackInterval = 2.0f,
            // 04 §8 ice_shard(ArtSource ready 1차 반입). 미반입이면 EnemyProjectile이 기존 Icons/Enemies/ice_shard로 대체.
            projectileSpriteResourcePath = EnemyProjectile.ArtPrefix + "ice_shard",
            iconVisualScale = 2f,
        };

        /// <summary>폭발형(Suicide). 접근해 자폭 → 화염(알콜) 장판을 잠시 남긴다.</summary>
        public static readonly EnemyDef OliveBomber = new EnemyDef
        {
            name = "올리브 바머",
            color = new Color(0.42f, 0.5f, 0.2f),
            radius = 0.32f,
            hp = 35f,
            speed = 3.0f,
            contactDamage = 18f,
            ingredientAmount = 2,
            explodesOnContact = true,
            burstZoneRadius = 1.6f,
            burstZoneDamagePerSecond = 10f,
            burstZoneSeconds = 2.5f,
            spriteResourcePath = "Icons/Enemies/olive_bomber",
            archetype = EnemyArchetype.Suicide,
            iconVisualScale = 2f,
        };

        /// <summary>소환형(Summoner). 제자리에서 라임웨지를 주기적으로 소환, 직접 공격은 거의 없음.</summary>
        public static readonly EnemyDef SwirlConjuror = new EnemyDef
        {
            name = "스월스틱 컨저러",
            color = new Color(0.85f, 0.25f, 0.35f),
            radius = 0.4f,
            hp = 40f,
            speed = 0f,
            contactDamage = 4f,
            ingredientAmount = 3,
            spriteResourcePath = "Icons/Enemies/swirl_stick_conjuror",
            archetype = EnemyArchetype.Summoner,
            summonDef = Imp,
            summonInterval = 6f,
            iconVisualScale = 2f,
        };

        /// <summary>[엘리트] 탄막형(EliteBarrage). 원형 유리 파편 탄막. 체력이 높고 웨이브 후반에 드물게 등장.</summary>
        public static readonly EnemyDef MartiniSpirit = new EnemyDef
        {
            name = "마티니 스피릿",
            color = new Color(0.35f, 0.55f, 0.95f),
            radius = 0.42f,
            hp = 150f,
            speed = 2.0f,
            contactDamage = 8f,
            ingredientAmount = 4,
            spriteResourcePath = "Icons/Enemies/martini_spirit",
            // 04 §8 glass_shard(마티니 탄). 아트가 없으면 색 원 placeholder.
            projectileSpriteResourcePath = EnemyProjectile.ArtPrefix + "glass_shard",
            archetype = EnemyArchetype.EliteBarrage,
            keepDistance = 3.5f,
            projectileSpeed = 5f,
            projectileDamage = 7f,
            attackInterval = 3.2f,
            barrageCount = 8,
            iconVisualScale = 2f,
        };

        // ── 차 트랙 (Tea) — 칵테일 트랙과 동일 아키타입을 재사용, 대칭 구조 ([[01_몹 디자인 - 칵테일 & 차]] 3장) ──

        /// <summary>돌진형(Rush). 라임웨지의 차 트랙 대칭.</summary>
        public static readonly EnemyDef TeabagImp = new EnemyDef
        {
            name = "티백 임프",
            color = new Color(0.85f, 0.7f, 0.45f),
            radius = 0.35f,
            hp = 20f,
            speed = 2.6f,
            contactDamage = 6f,
            ingredientAmount = 1,
            dropType = IngredientType.Tea,
            spriteResourcePath = "Icons/Enemies/teabag_imp",
            iconVisualScale = 2f,
        };

        /// <summary>투척형(Ranged). 쉐이커 골렘의 차 트랙 대칭 — 뜨거운 물방울 투척.</summary>
        public static readonly EnemyDef KettleCannon = new EnemyDef
        {
            name = "케틀 캐논",
            color = new Color(0.55f, 0.35f, 0.2f),
            radius = 0.45f,
            hp = 55f,
            speed = 2.0f,
            contactDamage = 5f,
            ingredientAmount = 2,
            dropType = IngredientType.Tea,
            spriteResourcePath = "Icons/Enemies/kettle_cannon",
            archetype = EnemyArchetype.Ranged,
            keepDistance = 4.5f,
            projectileSpeed = 6f,
            projectileDamage = 10f,
            attackInterval = 2.0f,
            projectileSpriteResourcePath = EnemyProjectile.ArtPrefix + "water_drop", // 04 §8 water_drop(없으면 기존 아이콘)
            iconVisualScale = 2f,
        };

        /// <summary>폭발형(Suicide). 올리브 바머의 차 트랙 대칭 — 피해 대신 둔화 장판(끈적한 설탕)으로 차별화.</summary>
        public static readonly EnemyDef SugarCubeBomber = new EnemyDef
        {
            name = "슈가 큐브 바머",
            color = new Color(0.92f, 0.87f, 0.72f),
            radius = 0.32f,
            hp = 35f,
            speed = 3.0f,
            contactDamage = 8f,
            ingredientAmount = 2,
            dropType = IngredientType.Tea,
            spriteResourcePath = "Icons/Enemies/sugar_cube_bomber",
            explodesOnContact = true,
            burstZoneRadius = 1.8f,
            burstZoneSeconds = 3f,
            burstZoneSlows = true,
            burstSlowMultiplier = 0.5f,
            archetype = EnemyArchetype.Suicide,
            iconVisualScale = 2f,
        };

        /// <summary>소환형(Summoner). 스월스틱 컨저러의 차 트랙 대칭 — 제자리에서 티백 임프를 주기 소환.</summary>
        public static readonly EnemyDef LeafWarden = new EnemyDef
        {
            name = "리프 워든",
            color = new Color(0.35f, 0.6f, 0.3f),
            radius = 0.4f,
            hp = 40f,
            speed = 0f,
            contactDamage = 4f,
            ingredientAmount = 3,
            dropType = IngredientType.Tea,
            archetype = EnemyArchetype.Summoner,
            summonDef = TeabagImp,
            summonInterval = 6f,
            spriteResourcePath = "Icons/Enemies/leaf_warden",
        };

        /// <summary>[엘리트] 탄막형(EliteBarrage). 마티니 스피릿의 차 트랙 대칭 — 뜨거운 물줄기 탄막.</summary>
        public static readonly EnemyDef TeaCeremonyGuardian = new EnemyDef
        {
            name = "티 세리머니 가디언",
            color = new Color(0.6f, 0.42f, 0.25f),
            radius = 0.42f,
            hp = 150f,
            speed = 2.0f,
            contactDamage = 8f,
            ingredientAmount = 4,
            dropType = IngredientType.Tea,
            projectileSpriteResourcePath = EnemyProjectile.ArtPrefix + "water_drop", // 04 §8 케틀/차 엘리트 탄 공유
            archetype = EnemyArchetype.EliteBarrage,
            keepDistance = 3.5f,
            projectileSpeed = 5f,
            projectileDamage = 7f,
            attackInterval = 3.2f,
            barrageCount = 8,
            spriteResourcePath = "Icons/Enemies/tea_ceremony_guardian",
        };

        // ── 커피 트랙 일반 몹 (완충용, 소량) — [[02_몹 디자인 - 커피 & 중간보스]] 1장.
        // 중간보스만으로는 원두 게이지가 뭉텅이로만 차서, 후반 웨이브에 아주 낮은 비율로만 섞는다.
        // EnemyRoster.PickCommon 의 CoffeeTrackUnlockTime 이후에만 등장.

        /// <summary>돌진형(Rush). 라임웨지/티백 임프와 체력은 같지만 더 빠르다 — "커피는 빠르다".</summary>
        public static readonly EnemyDef EspressoBug = new EnemyDef
        {
            name = "에스프레소 벅",
            color = new Color(0.32f, 0.2f, 0.12f),
            radius = 0.3f,
            hp = 20f,
            speed = 3.6f,
            contactDamage = 6f,
            ingredientAmount = 1,
            dropType = IngredientType.Coffee,
            spriteResourcePath = "Icons/Enemies/espresso_bug",
        };

        /// <summary>투척형(Ranged). 우유 거품 투척 — 시야 방해 연출은 이번 범위 밖(피해만 준다).</summary>
        public static readonly EnemyDef MilkFoamer = new EnemyDef
        {
            name = "밀크 포머",
            color = new Color(0.92f, 0.88f, 0.8f),
            radius = 0.35f,
            hp = 35f,
            speed = 1.8f,
            contactDamage = 4f,
            ingredientAmount = 2,
            dropType = IngredientType.Coffee,
            // _bgfix: 원본 PNG가 가짜 알파(체크무늬가 픽셀로 그려짐, 실제 투명 0%)라 배경을 직접
            // 제거한 파생본. 임시 조치이며 Codex가 실제 투명 배경으로 재납품하면 교체한다
            // (2026-09-25 Orca 감사, Delivery/orchestration-art-gap-review.md).
            spriteResourcePath = "Icons/Enemies/milk_foamer_bgfix",
            archetype = EnemyArchetype.Ranged,
            keepDistance = 4.5f,
            projectileSpeed = 6f,
            projectileDamage = 8f,
            attackInterval = 2.2f,
        };

        /// <summary>중간보스 A: 돌진 패턴. 처치하면 커피 원두를 쏟아낸다 (5개 × 4 = 원두 20).</summary>
        public static readonly EnemyDef GrinderGolem = new EnemyDef
        {
            name = "그라인더 골렘",
            color = new Color(0.55f, 0.36f, 0.24f),
            radius = 1.1f,
            hp = 500f,
            speed = 1.5f,
            contactDamage = 16f,
            dropType = IngredientType.Coffee,
            dropCount = 5,
            ingredientAmount = 4,
            boss = BossKind.Mid,
            // _bgfix: 가짜 알파 배경 제거 파생본 (위 milk_foamer 주석 참고).
            spriteResourcePath = "Icons/Enemies/grinder_golem_bgfix",
        };

        /// <summary>중간보스 B: 드립 서버 스펙터. 둥둥 떠다니며 장판(드립 웨이브)과 투명화로 회피를 요구한다
        /// ([[02_몹 디자인 - 커피 & 중간보스]] 상대 HP 55, 그라인더 골렘의 500/40 비율로 환산).</summary>
        public static readonly EnemyDef DripServerSpecter = new EnemyDef
        {
            name = "드립 서버 스펙터",
            color = new Color(0.45f, 0.32f, 0.4f),
            radius = 1.0f,
            hp = 700f,
            speed = 1.6f,
            contactDamage = 14f,
            dropType = IngredientType.Coffee,
            dropCount = 5,
            ingredientAmount = 4,
            boss = BossKind.Mid,
            // _bgfix: 가짜 알파 배경 제거 파생본 (위 milk_foamer 주석 참고).
            spriteResourcePath = "Icons/Enemies/drip_server_specter_bgfix",
        };

        /// <summary>강화 보스(3번째 중간보스): 에스프레소 머신 리바이어던.
        /// 그라인더 골렘 + 드립 서버 스펙터 패턴을 압축 계승하고 신규 "압력 게이지 폭발" 패턴을 더한다.
        /// 처치 시 커피 원두(대량) + 스페셜티(영구 재화)를 드랍한다.</summary>
        public static readonly EnemyDef EspressoLeviathan = new EnemyDef
        {
            name = "에스프레소 머신 리바이어던",
            color = new Color(0.3f, 0.18f, 0.12f),
            radius = 1.5f,
            hp = 1250f,
            speed = 1.3f,
            contactDamage = 22f,
            dropType = IngredientType.Coffee,
            dropCount = 8,
            ingredientAmount = 5,
            boss = BossKind.Enhanced,
            // _bgfix: 가짜 알파 배경 제거 파생본 (위 milk_foamer 주석 참고).
            spriteResourcePath = "Icons/Enemies/espresso_leviathan_bgfix",
        };

        /// <summary>최종보스. 클리어 시 런 종료. HP 50% 미만("Enraged") 진입 시 2페 아트(rage_overlay)로 바뀐다
        /// (CustomerBossDriver.Enraged 기준 — 소환/환불 쿨타임이 줄어드는 시점과 동일).</summary>
        public static readonly EnemyDef DifficultCustomer = new EnemyDef
        {
            name = "진상 손님",
            color = new Color(0.85f, 0.2f, 0.25f),
            radius = 1.3f,
            hp = 2500f,
            speed = 1.4f,
            contactDamage = 20f,
            dropCount = 0,
            boss = BossKind.Final,
            // _bgfix: 가짜 알파 배경 제거 파생본 (위 milk_foamer 주석 참고).
            spriteResourcePath = "Icons/Enemies/difficult_customer_bgfix",
            enragedSpriteResourcePath = "Icons/Enemies/difficult_customer_enraged_bgfix",
        };

        static EnemyDef TeaVariant(EnemyDef source, string name, string art)
        {
            var copy=(EnemyDef)source.MemberwiseClone();
            copy.name=name; copy.artId=art; copy.spriteResourcePath=null;
            copy.color=new Color(.58f,.76f,.42f);
            return copy;
        }
        public static readonly EnemyDef GaiwanSentinel=TeaVariant(GrinderGolem,"개완 수호자","gaiwan_sentinel");
        public static readonly EnemyDef SteamKettleElder=TeaVariant(DripServerSpecter,"김 서린 주전자 장로","steam_kettle_elder");
        public static readonly EnemyDef TeaKilnDragon=TeaVariant(EspressoLeviathan,"차 화로 용","tea_kiln_dragon");
        public static readonly EnemyDef SpringLeafSprite=TeaVariant(TeabagImp,"새싹 찻잎 정령","spring_leaf_sprite");
        public static readonly EnemyDef BambooScoopArcher=TeaVariant(KettleCannon,"차시 궁수","bamboo_scoop_archer");
        public static readonly EnemyDef TeaLeafScholar=TeaVariant(EspressoBug,"차 연구 정령","leaf_warden");
        public static readonly EnemyDef TeaCustomer=TeaVariant(DifficultCustomer,"까다로운 차 손님",DifficultCustomer.ArtId);
    }

    /// <summary>2026-11-10 하나의 직접 공격 피해 결과(기본/추가/합계, 정수). 표시 숫자는 계산된 공격 피해(막타의 실제 HP 감소와 구분).</summary>
    public struct DamageResult
    {
        public int baseDamage, bonusDamage, total;
        /// <summary>2026-11-12 런 음료 특성 추가 피해(에스프레소 강적). total = base + bonus(상성) + drinkBonus.</summary>
        public int drinkBonus;
        public bool Affinity => bonusDamage > 0;
    }

    /// <summary>몹의 기본 추적 이동을 대신할 수 있는 행동 (보스 패턴용).</summary>
    public interface IEnemyDriver
    {
        /// <summary>true 를 돌려주면 velocity 가 기본 추적 대신 쓰인다.</summary>
        bool TryGetVelocity(Vector2 selfPosition, Vector2 playerPosition, out Vector2 velocity);
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour
    {
        /// <summary>살아있는 몹 목록. 자동 조준용 최근접 탐색에 쓴다.</summary>
        public static readonly List<Enemy> Active = new List<Enemy>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        EnemyDef def;
        PlayerController player;
        Rigidbody2D rb;
        SpriteRenderer sr;
        IEnemyDriver driver;
        float flash;
        Color flashColor = Color.white;
        float dotTintReady;
        Transform visualTf;
        Vector3 visualBasePos;
        Vector2 recoilDir;
        float recoilDistance, recoilTimer;
        bool dead;
        Sprite normalIcon;
        Sprite enragedIcon;
        bool isEnragedVisual;
        bool hasEnteredView;
        float arrivalGrace;
        ArtAsset bodyArt;
        SpriteAnimator animator;
        float hurtTimer;
        float actionTimer;
        float faceLockTimer;
        float faceLockDir = 1f;
        Vector2 lastPosition;

        /// <summary>보스 등장 1초 안내가 끝나 실제로 행동/피해를 주고받을 수 있는 상태인지.
        /// 보스가 아니면 항상 true. 드라이버는 이게 true가 되기 전까지 자기 타이머를 멈춰야 한다.</summary>
        public bool HasArrived => arrivalGrace <= 0f;

        public EnemyDef Def => def;
        public PlayerController RunPlayer => player;
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        /// <summary>반올림 전 최대 체력(도전 배율 재조정용). MaxHp = 이 값의 정수 반올림.</summary>
        float rawMaxHp;
        RunChallenge challenge;
        public bool IsDead => dead;

        /// <summary>true면 TakeDamage가 전부 무시된다 (드립 서버 스펙터의 "투명화" 패턴 등).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>실제로 적용된 피해량과 함께 발생 (보스 패턴이 "받은 피해"에 반응할 때 사용, 예: 압력 게이지 딜레이스).</summary>
        public event System.Action<float> Damaged;
        /// <summary>41 히든 불꽃 쇼: 화상 중인 일반 적의 기본 추적 이동 배율(EnemyHitStatus가 설정). 패턴/돌진 속도는 그대로.</summary>
        public float StatusSpeedMultiplier { get; set; } = 1f;
        /// <summary>2026-11-12 차 2번째 우림 적중 둔화(StatusSpeedMultiplier와 별개 곱 — 불꽃 둔화 등 기존 상태를 덮지 않음). 다시 맞히면 시간만 갱신, 배율은 더 강한 쪽 하나(중첩 없음).</summary>
        float drinkSlowUntil = float.MinValue, drinkSlowFactor = 1f;
        public bool DrinkSlowed => Time.time < drinkSlowUntil;
        public float DrinkSlowMultiplier => DrinkSlowed ? drinkSlowFactor : 1f;
        public float DrinkSlowRemaining => DrinkSlowed ? drinkSlowUntil - Time.time : 0f;
        public void ApplyDrinkSlow(float factor, float seconds)
        {
            if (dead || def == null || def.IsBoss || seconds <= 0f) return;
            factor = Mathf.Clamp(factor, 0.05f, 1f);
            drinkSlowFactor = DrinkSlowed ? Mathf.Min(drinkSlowFactor, factor) : factor;
            drinkSlowUntil = Time.time + seconds;
        }

        // 48(2026-10-12) 검 타격 경직: 일반 적만, 짧게(Balance.SwordStaggerSeconds), 같은 적은 쿨다운 동안 다시 경직되지 않는다(무한 경직 방지).
        float staggerRemaining;
        float staggerReadyAt = float.MinValue;
        public bool Staggered => staggerRemaining > 0f;
        /// <returns>실제로 경직이 걸렸으면 true(보스·쿨다운 중·사망은 false).</returns>
        public bool TryStagger(float seconds, float cooldown)
        {
            if (dead || def.IsBoss || Time.time < staggerReadyAt) return false;
            staggerRemaining = seconds;
            staggerReadyAt = Time.time + cooldown;
            return true;
        }

        /// <summary>피격 플래시가 끝나면 돌아갈 색. 보스 텔레그래프 표시에도 쓴다.</summary>
        public Color BaseColor { get; set; }

        /// <summary>평상시(피격/텔레그래프 없을 때) 색. 실제 아이콘이면 흰색, placeholder면 def.color.</summary>
        public Color NormalColor { get; private set; }

        public static Enemy SpawnAt(Vector2 position, EnemyDef def, float hpScale, PlayerController player,
            Transform parent) => SpawnAt(position, def, hpScale, player, parent, 1f);

        /// <summary>2026-11-10: attackScale은 끝없는 재료 모으기 웨이브 공격 배율(정규 모드는 항상 1 — 기존 값 그대로).
        /// 원본 EnemyDef(공유 정적 정의)는 바꾸지 않고 이 개체에만 보관해 접촉·투사체·자폭 장판·소환체에 적용한다.</summary>
        public static Enemy SpawnAt(Vector2 position, EnemyDef def, float hpScale, PlayerController player,
            Transform parent, float attackScale)
        {
            return SpawnAt(position, def, hpScale, player, parent, attackScale, false, false);
        }

        /// <summary>2026-11-11 마스터 도전 보상 몬스터용: harmless = 접촉·자폭·장판·투사체·소환 등 플레이어 피해 경로 전부 차단(피해 0 호출도 하지 않음),
        /// ignoreChallengeHp = 영구 해금 도전 체력 배율을 곱하지 않고 이후 배율 변경에도 다시 늘리지 않는다(절대 웨이브1 기준 고정 HP).</summary>
        public static Enemy SpawnAt(Vector2 position, EnemyDef def, float hpScale, PlayerController player,
            Transform parent, float attackScale, bool harmless, bool ignoreChallengeHp)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var enemy = go.AddComponent<Enemy>();
            enemy.AttackScale = float.IsNaN(attackScale) || attackScale <= 0f ? 1f : attackScale;
            enemy.HpScale = hpScale;
            enemy.Harmless = harmless;
            enemy.IgnoreChallengeHp = ignoreChallengeHp;
            enemy.Init(def, hpScale, player);
            return enemy;
        }

        /// <summary>2026-11-11 true면 이 개체는 플레이어에게 어떤 피해·피격 이벤트도 만들지 않는다(보상 몬스터).</summary>
        public bool Harmless { get; private set; }
        public bool IgnoreChallengeHp { get; private set; }
        /// <summary>마스터 도전 웨이브 소속 표식(0 = 소속 없음). 처치 시 Killed 통지로 웨이브 처리량을 센다.</summary>
        public long MasterWaveTag { get; set; }
        /// <summary>2026-11-11 이 개체가 처치(사망)됐을 때 한 번(소속 웨이브 집계용 읽기 전용 통지).</summary>
        public event System.Action<Enemy> Killed;

        /// <summary>이 개체의 공격 배율(1 = 기존). 생성 때 한 번 정해지고 이후 웨이브가 바뀌어도 그대로(신규 적만 새 수치).</summary>
        public float AttackScale { get; private set; } = 1f;
        /// <summary>생성 때 받은 체력 배율(소환체 상속용).</summary>
        public float HpScale { get; private set; } = 1f;
        /// <summary>2026-11-10 처치 보상 출처 정의(null = 자기 정의). 진상 보스 영업의 "세 계열 고르게" 표에서 바뀐 몬스터도
        /// 드롭·보급·히든 처치 효과는 원래 직업 테마 추첨 결과를 따르게 한다(보상 보존).</summary>
        public EnemyDef RewardDef { get; set; }
        /// <summary>명시 계열 표(MonsterFamilies)의 계열.</summary>
        public MonsterFamily Family => MonsterFamilies.Of(def);

        /// <summary>공격 배율 적용 정수 피해. 배율 1이면 원래 값을 그대로 돌려준다(정규 모드 회귀 없음). 양수면 최소 1.</summary>
        public float ScaledAttack(float baseDamage)
        {
            if (AttackScale == 1f || baseDamage <= 0f) return baseDamage;
            return Mathf.Max(1, IntNum.Round(baseDamage * (double)AttackScale));
        }

        void Init(EnemyDef def, float hpScale, PlayerController player)
        {
            this.def = def;
            this.player = player;
            challenge = player != null && !IgnoreChallengeHp ? player.Challenge : null;
            // 2026-11-06 정수 전용: 최대 체력은 정수(중간값은 0에서 먼 쪽, 최소 1). 피해도 정수라 HP는 항상 정수다.
            rawMaxHp = def.hp * hpScale * (challenge?.HpMultiplier ?? 1f);
            MaxHp = Mathf.Max(1, IntNum.Round(rawMaxHp));
            Hp = MaxHp;
            if (challenge != null) challenge.HpMultiplierChanged += RescaleChallengeHp;

            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // 보스가 다른 몹에 밀려 튕기지 않도록 무겁게
            if (def.IsBoss) rb.mass = 50f;
            if (def.IsBoss)
            {
                arrivalGrace = Balance.BossArrivalGraceSeconds;
                Invulnerable = true; // 안내 1초 동안은 무적 + 접촉 피해 없음(FixedUpdate에서 HasArrived로 게이트)
            }

            var icon = string.IsNullOrEmpty(def.spriteResourcePath) ? null : Resources.Load<Sprite>(def.spriteResourcePath);
            // 04 §9 새 몸체 아트(Codex ready)가 있으면 그것을 우선 — 발 피벗, H/(bodyHeightPx/128) 균일 배율,
            // 기존 EnemyIconVisualScale을 곱하지 않는다. 없으면 기존 아이콘 경로 그대로(단계적 전환).
            var art = ArtLibrary.Get(def.ArtId);
            bodyArt = art != null && art.IsBody && art.Clip("idle") != null ? art : null;

            // 스프라이트를 별도 자식에 둬서, 실제 아이콘의 캔버스 여백(이미지마다 캐릭터가 차지하는
            // 비율이 다 다름 — def.iconVisualScale) 때문에 체감 크기가 달라지는 걸 시각적으로만
            // 보정한다. 루트(=이 오브젝트)는 스케일 1을 유지해 콜라이더·전투 판정은 def.radius 그대로 간다.
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(transform, false);
            sr = visualGo.AddComponent<SpriteRenderer>();
            if (bodyArt != null)
            {
                visualGo.transform.localPosition = new Vector3(0f, -def.radius, 0f);
                visualGo.transform.localScale = Vector3.one * bodyArt.BodyVisualScale * Mathf.Max(0.05f, def.visualScaleMultiplier);
                animator = visualGo.AddComponent<SpriteAnimator>();
                animator.Bind(sr);
                animator.Play(bodyArt.Clip("idle"));
                icon = sr.sprite;
            }
            else
            {
                float visualScale = icon != null ? def.iconVisualScale : 1f;
                visualGo.transform.localScale = Vector3.one * def.radius * 2f * visualScale; // 아이콘 경로는 radius가 이미 크기를 정한다(축소 정의는 radius로 반영)
                sr.sprite = icon != null ? icon : SpriteFactory.Capsule;
            }
            visualTf = visualGo.transform;
            visualBasePos = visualTf.localPosition;
            normalIcon = sr.sprite;
            enragedIcon = string.IsNullOrEmpty(def.enragedSpriteResourcePath)
                ? null : Resources.Load<Sprite>(def.enragedSpriteResourcePath);
            // 실제 아이콘은 원본 색 그대로(흰색 틴트), 없으면 placeholder를 def.color로 물들인다.
            // 피격 플래시 후 이 색으로 돌아가므로 BaseColor 도 함께 맞춘다.
            NormalColor = icon != null ? Color.white : def.color;
            BaseColor = NormalColor;
            sr.color = BaseColor;
            sr.sortingOrder = 2; // 플레이어와 같은 순서 → 카메라의 깊이 정렬(Y)로 앞뒤가 갈린다

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = def.radius; // 루트 스케일이 1이므로 반지름을 직접 지정

            // 04 §8 발밑 그림자(Visual 다음에 붙여 GetComponentInChildren이 몸체를 먼저 찾게 한다).
            GroundShadow.Attach(transform, new Vector2(0f, -def.radius), def.radius * 2.4f);
            if (def.IsBoss)
            {
                // 09 boss_arrival — 등장 안내 1초 동안 발밑에 재생(판정 없음).
                ArtVfx.Spawn("boss_arrival", (Vector2)transform.position + new Vector2(0f, -def.radius), 0f,
                    def.radius * 4f, Balance.BossArrivalGraceSeconds, Color.white, SortOrders.GroundZone);
            }
            if (def.ArtId == "leaf_warden") LeafMoteOrbit.Attach(this);
            lastPosition = transform.position;
        }

        /// <summary>04 §6 행동 클립(action/charge_prepare/drip/steam/refund/...)을 재생한다. 게임 이벤트가 클립을 부를 뿐
        /// 판정 시간은 바꾸지 않는다. 반복 클립은 holdSeconds 동안 유지. 클립(또는 새 몸체 아트)이 없으면 false.</summary>
        public bool PlayAction(string clipName, float holdSeconds = 0f)
        {
            var clip = bodyArt?.Clip(clipName);
            if (clip == null || animator == null || dead) return false;
            animator.Play(clip, true);
            actionTimer = clip.loop ? Mathf.Max(holdSeconds, 0.1f) : Mathf.Max(clip.Duration, holdSeconds);
            return true;
        }

        /// <summary>행동 클립 anchor(manifest anchorsByFrame: muzzle/emitter/summon/reviewOrigin, 좌하단 원점 px)의 월드 위치.
        /// frame &lt; 0이면 지금 표시 중인 프레임(그 클립이 재생 중이 아니면 0). Visual 자식의 발 피벗·배율·좌우 반전을 그대로
        /// 따르므로 루트 판정·콜라이더와 무관한 순수 발사 지점 정렬이다. 몸체 아트나 해당 anchor가 없으면 false + 루트 위치.</summary>
        public bool TryGetActionAnchorWorld(string clipName, string anchorName, out Vector2 world, int frame = -1)
        {
            world = transform.position;
            var clip = bodyArt?.Clip(clipName);
            if (clip == null || animator == null) return false;
            if (frame < 0) frame = animator.Current == clip ? animator.FrameIndex : 0;
            if (!bodyArt.TryGetAnchor(clipName, frame, anchorName, out var px)) return false;
            world = animator.transform.TransformPoint(bodyArt.AnchorToLocal(px));
            return true;
        }

        /// <summary>행동 시작 시 목표 쪽을 seconds 동안 바라본다(Visual 좌우만 — 이동·판정 불변). 뒤로 물러나며 쏠 때
        /// 총구/손 anchor가 등 뒤에 붙는 것을 막는다. 몸체 아트가 없으면 아무 일도 하지 않는다.</summary>
        public void FaceTowards(float worldX, float seconds)
        {
            if (animator == null || dead) return;
            faceLockDir = worldX < transform.position.x ? -1f : 1f;
            faceLockTimer = Mathf.Max(faceLockTimer, seconds);
            ApplyFacing(faceLockDir);
        }

        void ApplyFacing(float dirX)
        {
            var visual = animator.transform;
            var scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (dirX < 0f ? -1f : 1f);
            visual.localScale = scale;
        }

        /// <summary>새 몸체 아트를 쓰는 중인지(테스트/보고용).</summary>
        public bool UsesBodyArt => bodyArt != null;
        public SpriteAnimator Animator => animator;

        /// <summary>보스 패턴 컴포넌트가 자신을 등록한다.</summary>
        public void SetDriver(IEnemyDriver driver) => this.driver = driver;

        /// <summary>진상 손님의 HP 50% 미만("Enraged") 2페 아트 전환. enragedSpriteResourcePath가 없으면 아무 효과 없음.
        /// 매 프레임 호출해도 안전 — 이미 같은 상태면 무시한다.</summary>
        public void SetEnragedVisual(bool enraged)
        {
            if (enraged == isEnragedVisual) return;
            if (animator != null)
            {
                // 04 §8: 새 몸체는 같은 클립 + rage_overlay(진상 손님 피벗 기준) 오버레이로 분노를 표시한다.
                isEnragedVisual = enraged;
                var overlay = animator.transform.Find("RageOverlay");
                if (overlay == null && enraged)
                {
                    var sprite = ArtLibrary.Sprite("rage_overlay");
                    if (sprite == null) return;
                    var go = new GameObject("RageOverlay");
                    go.transform.SetParent(animator.transform, false);
                    var osr = go.AddComponent<SpriteRenderer>();
                    osr.sprite = sprite;
                    osr.sortingOrder = sr.sortingOrder;
                    overlay = go.transform;
                }
                if (overlay != null) overlay.gameObject.SetActive(enraged);
                return;
            }
            if (enragedIcon == null) return;
            isEnragedVisual = enraged;
            sr.sprite = enraged ? enragedIcon : normalIcon;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void OnDestroy()
        {
            if (challenge != null) challenge.HpMultiplierChanged -= RescaleChallengeHp;
        }

        void RescaleChallengeHp(float previous, float current)
        {
            if (dead || Hp <= 0f || previous <= 0f) return;
            // 정수 전용: 반올림 전 원값을 배율로 조정한 뒤 한 번만 반올림한다(새로 나온 적과 같은 최대 체력, 누적 오차 없음).
            float oldMax = MaxHp;
            rawMaxHp *= current / previous;
            MaxHp = Mathf.Max(1, IntNum.Round(rawMaxHp));
            Hp = Mathf.Clamp(IntNum.Round(Hp * MaxHp / oldMax), 1, (int)MaxHp); // 살아 있는 적은 비율 조정으로 죽지 않는다
        }

        void Update()
        {
            ClampToCameraView();

            if (arrivalGrace > 0f)
            {
                arrivalGrace -= Time.deltaTime;
                if (arrivalGrace <= 0f) Invulnerable = false;
            }

            TickBodyAnimation();
            TickRecoil();

            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                if (flash > 0f) { sr.color = flashColor; return; }
            }
            sr.color = BaseColor;
        }

        /// <summary>새 몸체 아트: 이동/대기 클립과 좌우 반전(Visual만), hurt 0.08초 표시(04 §6).</summary>
        void TickBodyAnimation()
        {
            if (animator == null || dead) return;
            Vector2 pos = transform.position;
            Vector2 delta = rb.linearVelocity;
            lastPosition = pos;
            float dirX = Mathf.Abs(delta.x) > 0.05f ? Mathf.Sign(delta.x)
                : player != null ? Mathf.Sign(player.Position.x - pos.x) : 1f;
            if (player != null && (def.archetype == EnemyArchetype.Ranged || def.archetype == EnemyArchetype.EliteBarrage))
                dirX = Mathf.Abs(player.Position.x - pos.x) > 0.1f
                    ? Mathf.Sign(player.Position.x - pos.x) : Mathf.Sign(animator.transform.localScale.x);
            if (faceLockTimer > 0f)
            {
                faceLockTimer -= Time.deltaTime;
                dirX = faceLockDir;
            }
            ApplyFacing(dirX);

            if (hurtTimer > 0f)
            {
                hurtTimer -= Time.deltaTime;
                return;
            }
            if (actionTimer > 0f)
            {
                actionTimer -= Time.deltaTime;
                return;
            }
            bool moving = delta.sqrMagnitude > 0.0025f;
            var clip = moving ? bodyArt.Clip("move") ?? bodyArt.Clip("idle") : bodyArt.Clip("idle");
            animator.Play(clip);
        }

        /// <summary>한 번 화면 안에 들어온 뒤로는 카메라 밖으로 못 나가게 X축만 붙잡는다.
        /// 스폰 직후(아직 화면에 한 번도 안 들어온 상태)는 SpawnEdgeMargin 진입 연출을 그대로 살려둔다.</summary>
        void ClampToCameraView()
        {
            if (dead) return;
            var cam = Camera.main;
            if (cam == null) return;

            float halfWidth = cam.orthographicSize * cam.aspect - Balance.EnemyViewClampMargin - def.radius;
            if (halfWidth <= 0f) return;

            float camX = cam.transform.position.x;
            float x = rb.position.x;

            if (!hasEnteredView)
            {
                if (Mathf.Abs(x - camX) > halfWidth) return; // 아직 진입 연출 중 — 그대로 둔다
                hasEnteredView = true;
            }

            float clampedX = Mathf.Clamp(x, camX - halfWidth, camX + halfWidth);
            if (!Mathf.Approximately(clampedX, x)) rb.position = new Vector2(clampedX, rb.position.y);
        }

        void FixedUpdate()
        {
            if (dead || player == null || player.IsDead)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            // 적 루트 y를 레인으로 제한한다 (06:14). 직전 스텝에서 벗어났으면 여기서 되돌린다 —
            // 돌진 같은 임의 방향 이동도 레인 밖으로 더 나아가지 못하게 막는다(06:117 그라인더 돌진).
            float clampedY = Mathf.Clamp(rb.position.y, Balance.DepthMin, Balance.DepthMax);
            // 57 P0-1: X도 유한 전투 구역 안으로 — 돌진·순간이동·넉백이 벽을 넘지 못하고, 벽에 반쯤 박혀 끼지 않게 반지름만큼 안쪽.
            float clampedArenaX = BattleArena.ClampX(rb.position.x, def.radius);
            if (!Mathf.Approximately(clampedY, rb.position.y) || !Mathf.Approximately(clampedArenaX, rb.position.x))
                rb.position = new Vector2(clampedArenaX, clampedY);

            Vector2 toPlayer = player.Position - rb.position;
            if (staggerRemaining > 0f) staggerRemaining -= Time.fixedDeltaTime;
            if (driver != null && driver.TryGetVelocity(rb.position, player.Position, out var driven))
                rb.linearVelocity = driven; // 돌진·보스 패턴 이동은 경직이 끊지 않는다
            else
                rb.linearVelocity = Staggered ? Vector2.zero : toPlayer.normalized * def.speed * StatusSpeedMultiplier * DrinkSlowMultiplier;

            if (Harmless) return; // 2026-11-11 보상 몬스터: 접촉·자폭 경로 자체를 건너뛴다(0 피해 호출·피격 무적·보호막 소모도 없음)
            if (!HasArrived) return; // 보스 등장 안내 1초 동안은 접촉 피해 없음 (06:24)
            if (Staggered) return; // 48 검 경직: 짧은 동안 접촉 피해·자폭 없음

            float touch = def.radius + PlayerController.Radius + 0.15f;
            if (toPlayer.sqrMagnitude <= touch * touch)
            {
                player.TakeContactDamage(this, ScaledAttack(def.contactDamage));
                if (def.explodesOnContact) Explode();
            }
        }

        /// <summary>Suicide 아키타입 전용: 닿으면 한 방 터뜨리고(위 TakeDamage 로 이미 처리) 자폭, 장판을 남긴다.</summary>
        void Explode()
        {
            AudioService.PlaySfx("explosion", transform.position);
            CameraRig.Shake(0.2f, 0.2f); // 08:122
            var vfxColor = def.burstZoneSlows ? new Color(0.95f, 0.9f, 0.7f, 0.65f) : new Color(1f, 0.45f, 0.15f, 0.65f);
            FadeVfx.Spawn(SpriteFactory.Circle, transform.position, 0f,
                Vector2.one * (def.radius * 1.2f), Vector2.one * (def.radius * 5f), vfxColor, 0.25f, 3);

            if (def.burstZoneSeconds > 0f)
            {
                if (def.burstZoneSlows)
                {
                    SlowZone.Spawn(transform.position, def.burstZoneRadius, def.burstSlowMultiplier,
                        def.burstZoneSeconds, player, transform.parent);
                }
                else
                {
                    BurstZone.Spawn(transform.position, def.burstZoneRadius, ScaledAttack(def.burstZoneDamagePerSecond),
                        def.burstZoneSeconds, player, transform.parent, BurstZone.OliveFireArt, DamageSource.Zone(def, "자폭 장판"));
                }
            }
            Die();
        }

        public void TakeDamage(float amount) => TakeDamage(amount, HitKind.Normal, null);

        /// <summary>피해 적용. kind는 연출 구분만 바꾼다(피해량·판정 불변). hitPoint는 불꽃 위치(없으면 몸 중앙 위).
        /// 2026-11-09: 무적이면 아무것도 하지 않고, 실제 피해가 0 이하면 섬광·소리·불꽃·숫자를 만들지 않는다.</summary>
        public void TakeDamage(float amount, HitKind kind, Vector2? hitPoint = null)
        {
            if (dead || Invulnerable) return;

            // 2026-11-06 정수 전용 전투 경계: 양수 피해는 정수로(중간값은 0에서 먼 쪽, 최소 1). 피해 숫자·HP 감소·Damaged 이벤트가 같은 값을 쓴다.
            if (amount > 0f) amount = IntNum.Damage(amount);
            Apply(amount, 0, kind, hitPoint);
        }

        /// <summary>2026-11-10 플레이어의 유효 직접 공격(근접·원래 투사체·광역 직접 공격 — 대상마다 따로 호출).
        /// 기본 정수 피해 B에 음료 순환 상성 추가 피해 A를 더해 하나의 피해 결과 T = B + A로 한 번만 적용한다
        /// (TakeDamage를 두 번 부르지 않음 → 사망·처치·드롭·히든 이벤트 1회). 지속 피해·반사·파편/추가 효과는 이 경로를 쓰지 않는다.
        /// 무적·0 피해면 아무것도 하지 않고(숫자·연출 없음) 기본 결과를 돌려준다.</summary>
        public DamageResult TakeDirectHit(float amount, HitKind kind, Vector2? hitPoint = null)
        {
            if (dead || Invulnerable) return default;
            int b = amount > 0f ? IntNum.Damage(amount) : 0;
            if (b <= 0) { Apply(amount > 0f ? 0f : amount, 0, kind, hitPoint); return default; }
            int a = DrinkAffinity.Bonus(b, player != null ? player.AffinityDrink : null, Family);
            // 2026-11-12 런 음료 특성(에스프레소 강적 +20%): 상성 A는 위처럼 보너스 전 B로 계산 — 서로 증폭하지 않는다.
            int d = RunDrinkTrait.DirectBonus(player, this, b);
            long t = (long)b + a + d;
            var result = new DamageResult { baseDamage = b, bonusDamage = a, drinkBonus = d, total = t > int.MaxValue ? int.MaxValue : (int)t };
            // 2026-11-11 상성 추가 피해가 실제로 확정된 적중에만 인수 아트 1회(상한·간격 예산 — 넘으면 건너뜀, 판정 불변).
            if (a > 0 && player != null && player.AffinityDrink is DrinkKind ak)
                MasterVfx.PlayAffinity(ak, hitPoint ?? (Vector2)transform.position + new Vector2(0f, def.radius * 0.3f));
            Apply(result.total, a, kind, hitPoint);
            // 둔화·보조 적중(보조 적중은 TakeDamage(Effect) — 이 직접 적중 경로를 다시 부르지 않는다).
            RunDrinkTrait.AfterDirectHit(player, this, b, hitPoint ?? (Vector2)transform.position);
#if CCT_PLAY_VERIFIER
            QaDirectHit?.Invoke(this, result); // QA 자동 검증 빌드 전용 읽기 관찰(판정·값 불변). 검수/출시 빌드에는 컴파일되지 않는다.
#endif
            return result;
        }
#if CCT_PLAY_VERIFIER
        public static System.Action<Enemy, DamageResult> QaDirectHit;
#endif

        void Apply(float amount, int bonus, HitKind kind, Vector2? hitPoint)
        {
            Hp -= amount;
            if (amount > 0f) PlayHitFeedback(kind, hitPoint);
            Damaged?.Invoke(amount);
            DamageNumbers.Show(this, amount, kind, bonus); // 08:80 피해 숫자(설정에서 끄면 숫자만 숨김). 상성이면 "합계 (추가 A)"
            if (Hp <= 0f) Die();
        }

        /// <summary>판정과 무관한 피격 연출: 종류별 색조·몸체 반동·불꽃(예산 제한)·소리(AudioService 중복/동시 제한).</summary>
        void PlayHitFeedback(HitKind kind, Vector2? hitPoint)
        {
            if (kind == HitKind.Dot)
            {
                // 지속 피해: 소리·불꽃 없이 짧은 색조만, 적마다 쿨다운(직접 타격 섬광을 덮지 않음).
                if (Time.time < dotTintReady || flash > 0f) return;
                dotTintReady = Time.time + HitFeedback.DotTintCooldown;
                SetFlash(kind);
                return;
            }
            SetFlash(kind);
            string sfx = HitFeedback.Sound(kind);
            if (sfx != null) AudioService.PlaySfx(sfx, transform.position);

            Vector2 center = (Vector2)transform.position + new Vector2(0f, def.radius * 0.3f);
            Vector2 at = hitPoint ?? center;
            float recoil = HitFeedback.Recoil(kind) * (def.IsBoss ? 0.5f : 1f);
            if (recoil > 0f && visualTf != null)
            {
                Vector2 from = hitPoint ?? (player != null ? player.Position : at - Vector2.right);
                Vector2 dir = (Vector2)transform.position - from;
                recoilDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
                recoilDistance = recoil;
                recoilTimer = HitFeedback.RecoilSeconds;
            }

            // 04 §8 hit_spark(피격 위치, 16fps 4장). 판정과 무관한 시각 효과. 다수 적·관통이 한꺼번에 맞아도 예산만큼만.
            if (!HitFeedback.TryReserveSpark()) return;
            float scale = HitFeedback.SparkScale(kind);
            ArtVfx.Spawn("hit_spark", at, Random.Range(0f, 360f), (0.5f + def.radius * 0.5f) * scale, 0.25f,
                HitFeedback.SparkColor(kind), SortOrders.AirVfx, 1.1f, false);
            if ((kind == HitKind.Critical || kind == HitKind.Heavy) && !HitFeedback.LowEffects)
            {
                // 치명/강타만 짧은 테두리 고리(기존 원 스프라이트) — 일반 타격과 한눈에 구분.
                var ring = FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * 0.25f, Vector2.one * (0.7f + def.radius * 0.6f),
                    kind == HitKind.Critical ? new Color(1f, 0.85f, 0.3f, 0.55f) : new Color(1f, 0.66f, 0.4f, 0.5f), 0.16f, SortOrders.AirVfx);
                if (transform.parent != null) ring.transform.SetParent(transform.parent, true);
            }
        }

        void SetFlash(HitKind kind)
        {
            flashColor = HitFeedback.Tint(kind) * BaseColor;
            flashColor.a = BaseColor.a;
            flash = HitFeedback.TintSeconds(kind);
            sr.color = flashColor;
            var hurt = kind == HitKind.Dot ? null : bodyArt?.Clip("hurt");
            // 행동 클립(예고·공격: charge_prepare/refund/steam/action 등) 중에는 hurt로 덮지 않는다 — 덮으면 hurt 끝 프레임이
            // 남은 행동 시간 내내 멈춰 보여 공격 예고가 사라진다(2026-10-08 r14 보스 녹화에서 확인). 피격은 색조·숫자·불꽃으로 계속 표시된다.
            if (hurt != null && animator != null && actionTimer <= 0f)
            {
                animator.Play(hurt, true);
                hurtTimer = 0.08f;
            }
        }

        /// <summary>피격 반동: Visual 자식의 위치만 잠깐 밀었다가 원위치(게임 시간 — 일시정지 중 정지).</summary>
        void TickRecoil()
        {
            if (visualTf == null || Time.deltaTime <= 0f) return; // 일시정지(timeScale 0): 반동 위치를 그대로 둔다
            if (recoilTimer > 0f)
            {
                recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
                float k = recoilTimer / HitFeedback.RecoilSeconds; // 1 → 0
                visualTf.localPosition = visualBasePos + (Vector3)(recoilDir * recoilDistance * k);
            }
            else if (visualTf.localPosition != visualBasePos) visualTf.localPosition = visualBasePos;
        }

        /// <summary>테스트/검수용: 현재 피격 색조가 남아 있는지, 반동 오프셋.</summary>
        public bool HitTintActive => flash > 0f;
        public Color CurrentBodyColor => sr != null ? sr.color : Color.clear;
        public Vector3 VisualOffset => visualTf != null ? visualTf.localPosition - visualBasePos : Vector3.zero;

        void Die()
        {
            dead = true;
            GetComponent<EnemyHitStatus>()?.OnEnemyDeath();
            // 04 §6 defeat: 판정 없는 별도 시각 오브젝트에서 재생(보상·판정 제거 시점은 그대로).
            var defeat = bodyArt?.Clip("defeat");
            if (defeat != null && animator != null)
                DefeatVisual.Spawn(defeat, animator.transform.position, animator.transform.localScale, transform.parent);
            // 09:20 "enemy_defeat: 일반 적 사망" — 보스는 대상 밖(보스 처치는 clear/보상 흐름이 대신함).
            if (!def.IsBoss) AudioService.PlaySfx("enemy_defeat", transform.position);
            player?.GetComponentInParent<GameManager>()?.OnEnemyKilled(transform.position, RewardDef ?? def);
            var killed = Killed; Killed = null; killed?.Invoke(this); // 2026-11-11 한 번만
            Destroy(gameObject);
        }

        /// <summary>origin 기준 maxDistance 안에서 가장 가까운 "타격 가능한"(무적 제외) 몹. 없으면 null.
        /// 동일 거리면 먼저 스폰한 적이 우선한다 (06:18 — Active는 스폰 순서로 추가됨, `&lt;`로 비교해
        /// 나중에 검사된 적이 동률로 덮어쓰지 않게 한다).</summary>
        public static Enemy FindNearest(Vector2 origin, float maxDistance)
        {
            Enemy best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Active.Count; i++)
            {
                var e = Active[i];
                if (e == null || e.dead || e.Invulnerable) continue;
                float sqr = ((Vector2)e.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }
    }
}
