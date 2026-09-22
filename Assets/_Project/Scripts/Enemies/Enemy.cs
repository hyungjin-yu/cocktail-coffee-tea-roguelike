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
        };

        /// <summary>디자인 문서엔 없는 자체 추가 잡몹 (덩치 큰 돌진형, 다양성용).</summary>
        public static readonly EnemyDef Brute = new EnemyDef
        {
            name = "레몬 브루트",
            color = new Color(0.95f, 0.6f, 0.25f),
            radius = 0.6f,
            hp = 70f,
            speed = 1.8f,
            contactDamage = 12f,
            ingredientAmount = 3,
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
            projectileSpriteResourcePath = "Icons/Enemies/ice_shard",
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
            archetype = EnemyArchetype.EliteBarrage,
            keepDistance = 3.5f,
            projectileSpeed = 5f,
            projectileDamage = 7f,
            attackInterval = 3.2f,
            barrageCount = 8,
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
            projectileSpriteResourcePath = "Icons/Enemies/water_drop",
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
            archetype = EnemyArchetype.EliteBarrage,
            keepDistance = 3.5f,
            projectileSpeed = 5f,
            projectileDamage = 7f,
            attackInterval = 3.2f,
            barrageCount = 8,
        };

        /// <summary>중간보스 A: 돌진 패턴. 처치하면 커피 원두를 쏟아낸다 (5개 × 4 = 원두 20).
        /// 전용 아트는 아직 없음 (쉐이커 골렘은 별개의 일반 몹으로 분리됨) — 캡슐 placeholder.</summary>
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
        };

        /// <summary>최종보스. 클리어 시 런 종료.</summary>
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
        };
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
        bool dead;

        public EnemyDef Def => def;
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsDead => dead;

        /// <summary>true면 TakeDamage가 전부 무시된다 (드립 서버 스펙터의 "투명화" 패턴 등).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>실제로 적용된 피해량과 함께 발생 (보스 패턴이 "받은 피해"에 반응할 때 사용, 예: 압력 게이지 딜레이스).</summary>
        public event System.Action<float> Damaged;

        /// <summary>피격 플래시가 끝나면 돌아갈 색. 보스 텔레그래프 표시에도 쓴다.</summary>
        public Color BaseColor { get; set; }

        /// <summary>평상시(피격/텔레그래프 없을 때) 색. 실제 아이콘이면 흰색, placeholder면 def.color.</summary>
        public Color NormalColor { get; private set; }

        public static Enemy SpawnAt(Vector2 position, EnemyDef def, float hpScale, PlayerController player,
            Transform parent)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var enemy = go.AddComponent<Enemy>();
            enemy.Init(def, hpScale, player);
            return enemy;
        }

        void Init(EnemyDef def, float hpScale, PlayerController player)
        {
            this.def = def;
            this.player = player;
            MaxHp = def.hp * hpScale;
            Hp = MaxHp;

            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            // 보스가 다른 몹에 밀려 튕기지 않도록 무겁게
            if (def.IsBoss) rb.mass = 50f;

            var icon = string.IsNullOrEmpty(def.spriteResourcePath) ? null : Resources.Load<Sprite>(def.spriteResourcePath);

            // 스프라이트를 별도 자식에 둬서, 실제 아이콘의 캔버스 여백(그림이 1024px 안에 절반 정도만
            // 차 있음) 때문에 체감 크기가 작아지는 걸 시각적으로만 보정한다.
            // 루트(=이 오브젝트)는 스케일 1을 유지해 콜라이더·전투 판정은 def.radius 그대로 간다.
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(transform, false);
            float visualScale = icon != null ? Balance.EnemyIconVisualScale : 1f;
            visualGo.transform.localScale = Vector3.one * def.radius * 2f * visualScale;

            sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sprite = icon != null ? icon : SpriteFactory.Capsule;
            // 실제 아이콘은 원본 색 그대로(흰색 틴트), 없으면 placeholder를 def.color로 물들인다.
            // 피격 플래시 후 이 색으로 돌아가므로 BaseColor 도 함께 맞춘다.
            NormalColor = icon != null ? Color.white : def.color;
            BaseColor = NormalColor;
            sr.color = BaseColor;
            sr.sortingOrder = 2; // 플레이어와 같은 순서 → 카메라의 깊이 정렬(Y)로 앞뒤가 갈린다

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = def.radius; // 루트 스케일이 1이므로 반지름을 직접 지정
        }

        /// <summary>보스 패턴 컴포넌트가 자신을 등록한다.</summary>
        public void SetDriver(IEnemyDriver driver) => this.driver = driver;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Update()
        {
            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                if (flash > 0f) return;
            }
            sr.color = BaseColor;
        }

        void FixedUpdate()
        {
            if (dead || player == null || player.IsDead)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 toPlayer = player.Position - rb.position;
            if (driver != null && driver.TryGetVelocity(rb.position, player.Position, out var driven))
                rb.linearVelocity = driven;
            else
                rb.linearVelocity = toPlayer.normalized * def.speed;

            float touch = def.radius + PlayerController.Radius + 0.15f;
            if (toPlayer.sqrMagnitude <= touch * touch)
            {
                player.TakeDamage(def.contactDamage);
                if (def.explodesOnContact) Explode();
            }
        }

        /// <summary>Suicide 아키타입 전용: 닿으면 한 방 터뜨리고(위 TakeDamage 로 이미 처리) 자폭, 장판을 남긴다.</summary>
        void Explode()
        {
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
                    BurstZone.Spawn(transform.position, def.burstZoneRadius, def.burstZoneDamagePerSecond,
                        def.burstZoneSeconds, player, transform.parent);
                }
            }
            Die();
        }

        public void TakeDamage(float amount)
        {
            if (dead || Invulnerable) return;

            Hp -= amount;
            flash = 0.06f;
            sr.color = Color.white;
            Damaged?.Invoke(amount);
            if (Hp <= 0f) Die();
        }

        void Die()
        {
            dead = true;
            GameManager.Instance?.OnEnemyKilled(transform.position, def);
            Destroy(gameObject);
        }

        /// <summary>origin 기준 maxDistance 안에서 가장 가까운 몹. 없으면 null.</summary>
        public static Enemy FindNearest(Vector2 origin, float maxDistance)
        {
            Enemy best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Active.Count; i++)
            {
                var e = Active[i];
                if (e == null || e.dead) continue;
                float sqr = ((Vector2)e.transform.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }
    }
}
