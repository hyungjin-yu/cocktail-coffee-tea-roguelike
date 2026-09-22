using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    public enum GameState { Playing, LevelUp, SkillSelect, GameOver }

    /// <summary>
    /// 한 판(런)의 루트. 몹 처치 → 재료 드랍 → 게이지 → 레벨업 → 카드 선택 루프,
    /// 보스 → 원두 → 스킬 해금 루프, 런 종료(사망/클리어)를 관리한다.
    /// 재시작은 이 오브젝트를 통째로 파괴하고 직업 선택 화면부터 다시 띄운다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        CardSystem cards;
        CardSelectView cardView;
        SkillTreeView skillView;
        ResultView resultView;
        /// <summary>재료 게이지가 찰 때마다 그 게이지의 카드 카테고리를 여기 쌓고, 한 번에 하나씩 처리한다.</summary>
        readonly Queue<CardCategory> pendingCardDraws = new Queue<CardCategory>();
        int pendingSkillPoints;
        /// <summary>이번 런에서 모은 재료량 (도감 표시용 누적치는 런 종료 시 한 번에 반영).</summary>
        int cocktailCollectedThisRun;
        int teaCollectedThisRun;
        int beansCollectedThisRun;

        public GameState State { get; private set; } = GameState.Playing;
        public Job Job { get; private set; }
        public WeaponKind Weapon { get; private set; }
        public PlayerStats Stats { get; private set; }
        public PlayerController Player { get; private set; }
        public IngredientGauge CocktailGauge { get; private set; }
        public IngredientGauge TeaGauge { get; private set; }
        /// <summary>
        /// 원두 게이지. 한 번 찰 때마다 스킬 포인트 1.
        /// 한 번의 드랍으로 게이지가 여러 단계 넘어가도(예: 보스 한 방에 원두가 몰려 들어옴)
        /// 스킬 선택 화면은 한 번만 뜨도록 pendingSkillPoints 를 1로 캡핑한다.
        /// </summary>
        public IngredientGauge BeanGauge { get; private set; }
        public SkillTree Skills { get; private set; }
        /// <summary>스페셜티로 직접 찍는 영구 강화 트리. 런 시작 시 세이브에서 읽어 스탯에 적용한다.</summary>
        public PermanentSkillTree PermanentSkills { get; private set; }
        public BossDirector Director { get; private set; }
        public HudView Hud { get; private set; }
        public CardSystem Cards => cards;
        public CardSelectView CardView => cardView;
        public SkillTreeView SkillView => skillView;
        public float RunTime { get; private set; }
        public int Kills { get; private set; }
        public bool Cleared { get; private set; }

        /// <summary>스페셜티(영구 재화)로 얻은 커피 원두 획득 배율. 1 = 보너스 없음.</summary>
        public float BeanGainMultiplier { get; private set; } = 1f;

        /// <summary>현재 살아있는 보스. 없으면 null.</summary>
        public Enemy ActiveBoss { get; set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        public void Initialize(Job job, WeaponKind weapon)
        {
            Time.timeScale = 1f;
            Job = job;
            Weapon = weapon;
            var preset = JobPreset.For(job);

            Stats = new PlayerStats();
            cards = new CardSystem(CardDatabase.LoadAll());
            Skills = new SkillTree(SkillDatabase.LoadAll());

            // 스페셜티로 미리 찍어둔 영구 강화는 런 시작 시 한 번 적용 — 런 종료해도 사라지지 않는 유일한 진행치.
            var save = PermanentProgress.Current;
            PermanentSkills = new PermanentSkillTree(PermanentSkillDatabase.LoadAll(), save.unlockedPermanentSkillIds);
            PermanentSkills.ApplyAll(Stats);
            BeanGainMultiplier = 1f + Stats.beanGainPercent;

            CocktailGauge = new IngredientGauge(IngredientType.Cocktail, Balance.CocktailGaugeStart,
                Balance.CocktailGaugeGrowth, () => Stats.GaugeNeedMultiplier);
            CocktailGauge.LeveledUp += _ => pendingCardDraws.Enqueue(CardCategory.Cocktail);
            TeaGauge = new IngredientGauge(IngredientType.Tea, Balance.CocktailGaugeStart,
                Balance.CocktailGaugeGrowth, () => Stats.GaugeNeedMultiplier);
            TeaGauge.LeveledUp += _ => pendingCardDraws.Enqueue(CardCategory.Tea);
            BeanGauge = new IngredientGauge(IngredientType.Coffee, Balance.BeanGaugeStart, Balance.BeanGaugeGrowth);
            BeanGauge.LeveledUp += _ => pendingSkillPoints = Mathf.Min(1, pendingSkillPoints + 1);

            var camera = SetupCamera();
            var background = CreateBackground();
            Player = CreatePlayer(preset, weapon);
            gameObject.AddComponent<CameraRig>().Init(camera, Player.transform, background);

            var spawner = new GameObject("Spawner");
            spawner.transform.SetParent(transform, false);
            spawner.AddComponent<EnemySpawner>().Init(this, Player);

            var director = new GameObject("BossDirector");
            director.transform.SetParent(transform, false);
            Director = director.AddComponent<BossDirector>();
            Director.Init(this);

            var canvas = UiFactory.CreateCanvas(transform, "UI", 0);
            Hud = HudView.Create(canvas.transform, this);
            cardView = CardSelectView.Create(canvas.transform);
            skillView = SkillTreeView.Create(canvas.transform);
            resultView = ResultView.Create(canvas.transform, this);

            Player.Died += OnPlayerDied;
        }

        Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                // 재시작 때 Camera.main 이 파괴 예정 오브젝트를 잡지 않도록 루트 밖에 둔다.
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            // 벨트스크롤 깊이감: 화면 아래쪽(월드 Y 작은 쪽)에 있을수록 앞에 그려지게
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, -1f, 0f);
            // 플레이 영역(레인)이 화면 아래쪽에 오도록 카메라를 레인 중심보다 위에 고정
            cam.transform.position = new Vector3(cam.transform.position.x, Balance.CameraFixedY, cam.transform.position.z);
            return cam;
        }

        Transform CreateBackground()
        {
            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, Balance.LaneCenterY, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Tile;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(60f, 40f);
            sr.sortingOrder = -10;
            return go.transform;
        }

        PlayerController CreatePlayer(JobPreset preset, WeaponKind weapon)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, Balance.LaneCenterY, 0f);
            var player = go.AddComponent<PlayerController>();
            player.Init(Stats, preset.playerColor);

            // 레이피어/검(근접), 총/활(원거리)은 각 쌍끼리 메커닉이 같음 — 치장만 다른 임시 처리.
            if (weapon == WeaponKind.Gun || weapon == WeaponKind.Bow)
                go.AddComponent<PlayerProjectileWeapon>().Init(Stats, player);
            else
                go.AddComponent<RapierWeapon>().Init(Stats, player);

            go.AddComponent<FlameBurst>().Init(Stats, player);
            go.AddComponent<PoisonAura>().Init(Stats, player);
            return player;
        }

        void Update()
        {
            if (State != GameState.Playing) return;

            RunTime += Time.deltaTime;
            Stats.Tick(Time.deltaTime);

            if (pendingCardDraws.Count > 0) BeginLevelUp();
            else if (pendingSkillPoints > 0) BeginSkillSelect();
        }

        public void AddIngredient(IngredientType type, int amount)
        {
            if (State == GameState.GameOver) return;

            switch (type)
            {
                case IngredientType.Cocktail:
                    CocktailGauge.Add(amount);
                    cocktailCollectedThisRun += amount;
                    break;
                case IngredientType.Tea:
                    TeaGauge.Add(amount);
                    teaCollectedThisRun += amount;
                    break;
                case IngredientType.Coffee:
                    int beanAmount = Mathf.RoundToInt(amount * BeanGainMultiplier);
                    BeanGauge.Add(beanAmount);
                    beansCollectedThisRun += beanAmount;
                    break;
            }
        }

        public void OnEnemyKilled(Vector2 position, EnemyDef def)
        {
            Kills++;

            for (int i = 0; i < def.dropCount; i++)
            {
                var jitter = def.dropCount > 1 ? Random.insideUnitCircle * def.radius : Vector2.zero;
                IngredientPickup.Spawn(this, Player, position + jitter, def.dropType, def.ingredientAmount,
                    transform);
            }

            if (!def.IsBoss && def.dropCount > 0 && Random.value < Stats.ExtraDropChance)
            {
                var jitter = Random.insideUnitCircle * 0.4f;
                IngredientPickup.Spawn(this, Player, position + jitter, def.dropType, def.ingredientAmount,
                    transform);
            }

            if (def.IsBoss)
            {
                ActiveBoss = null;
                if (def.boss == BossKind.Mid || def.boss == BossKind.Enhanced)
                    Player.Heal(Balance.MidBossHealFraction);
                if (def.boss == BossKind.Enhanced) GrantSpecialty();
                if (def.boss == BossKind.Final) Clear();
            }
        }

        /// <summary>강화 보스 처치 시 즉시 영구 저장 — 이후 플레이어가 죽어도 이번에 얻은 스페셜티는 남는다.</summary>
        void GrantSpecialty()
        {
            PermanentProgress.AddSpecialty(Balance.SpecialtyDropAmount);
            Hud.ShowBanner("스페셜티 획득!", $"+{Balance.SpecialtyDropAmount} (영구 재화, 다음 런부터 적용)", 3f);
        }

        void BeginLevelUp()
        {
            var category = pendingCardDraws.Dequeue();
            var offers = cards.Draw(category, Weapon.IsRanged());
            if (offers.Count == 0) return; // 이 카테고리 카드가 모두 소진됨 — 다음 대기열은 다음 프레임에 처리

            State = GameState.LevelUp;
            Time.timeScale = 0f;
            cardView.Show(offers, ChooseCard);
        }

        void ChooseCard(CardOffer offer)
        {
            cards.Take(offer.card, Stats);
            PermanentProgress.DiscoverCard(offer.card.id);
            Player.Heal(Balance.LevelUpHealFraction);
            State = GameState.Playing;
            Time.timeScale = 1f;
        }

        void BeginSkillSelect()
        {
            pendingSkillPoints--;
            if (!Skills.AnyAvailable())
            {
                pendingSkillPoints = 0; // 전부 해금됨 — 남은 포인트는 버린다
                return;
            }

            State = GameState.SkillSelect;
            Time.timeScale = 0f;
            skillView.Show(Skills, ChooseSkill);
        }

        void ChooseSkill(SkillData node)
        {
            Skills.Unlock(node, Stats);
            State = GameState.Playing;
            Time.timeScale = 1f;
        }

        void OnPlayerDied()
        {
            if (State == GameState.GameOver) return;
            EndRun(false);
        }

        void Clear()
        {
            if (State == GameState.GameOver) return;
            EndRun(true);
        }

        void EndRun(bool cleared)
        {
            Cleared = cleared;
            State = GameState.GameOver;
            Time.timeScale = 0f;
            PermanentProgress.AddMaterialsCollected(cocktailCollectedThisRun, teaCollectedThisRun, beansCollectedThisRun);
            resultView.Show(cleared, RunTime, Kills, CocktailGauge.Level, TeaGauge.Level, Skills.UnlockedCount,
                PermanentProgress.Current.specialtyPoints);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            GameBootstrap.RestartToSelect();
        }
    }
}
