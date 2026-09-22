using UnityEngine;

namespace CCT
{
    public enum GameState { Playing, LevelUp, SkillSelect, GameOver }

    /// <summary>
    /// 한 판(런)의 루트. 몹 처치 → 재료 드랍 → 게이지 → 레벨업 → 카드 선택 루프,
    /// 보스 → 원두 → 스킬 해금 루프, 런 종료(사망/클리어)를 관리한다.
    /// 재시작은 이 오브젝트를 통째로 파괴하고 GameBootstrap 으로 새로 만든다.
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
        int pendingLevelUps;
        int pendingSkillPoints;

        public GameState State { get; private set; } = GameState.Playing;
        public PlayerStats Stats { get; private set; }
        public PlayerController Player { get; private set; }
        public IngredientGauge CocktailGauge { get; private set; }
        /// <summary>
        /// 원두 게이지. 한 번 찰 때마다 스킬 포인트 1.
        /// 한 번의 드랍으로 게이지가 여러 단계 넘어가도(예: 보스 한 방에 원두가 몰려 들어옴)
        /// 스킬 선택 화면은 한 번만 뜨도록 pendingSkillPoints 를 1로 캡핑한다.
        /// </summary>
        public IngredientGauge BeanGauge { get; private set; }
        public SkillTree Skills { get; private set; }
        public BossDirector Director { get; private set; }
        public HudView Hud { get; private set; }
        public CardSystem Cards => cards;
        public CardSelectView CardView => cardView;
        public SkillTreeView SkillView => skillView;
        public float RunTime { get; private set; }
        public int Kills { get; private set; }
        public bool Cleared { get; private set; }

        /// <summary>현재 살아있는 보스. 없으면 null.</summary>
        public Enemy ActiveBoss { get; set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        public void Initialize()
        {
            Time.timeScale = 1f;

            Stats = new PlayerStats();
            cards = new CardSystem(CardDatabase.LoadAll());
            Skills = new SkillTree(SkillDatabase.LoadAll());

            CocktailGauge = new IngredientGauge(IngredientType.Cocktail, Balance.CocktailGaugeStart,
                Balance.CocktailGaugeGrowth, () => Stats.GaugeNeedMultiplier);
            CocktailGauge.LeveledUp += _ => pendingLevelUps++;
            BeanGauge = new IngredientGauge(IngredientType.Coffee, Balance.BeanGaugeStart, Balance.BeanGaugeGrowth);
            BeanGauge.LeveledUp += _ => pendingSkillPoints = Mathf.Min(1, pendingSkillPoints + 1);

            var camera = SetupCamera();
            var background = CreateBackground();
            Player = CreatePlayer();
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

        PlayerController CreatePlayer()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, Balance.LaneCenterY, 0f);
            var player = go.AddComponent<PlayerController>();
            player.Init(Stats);
            go.AddComponent<RapierWeapon>().Init(Stats, player);
            go.AddComponent<FlameBurst>().Init(Stats, player);
            return player;
        }

        void Update()
        {
            if (State != GameState.Playing) return;

            RunTime += Time.deltaTime;
            Stats.Tick(Time.deltaTime);

            if (pendingLevelUps > 0) BeginLevelUp();
            else if (pendingSkillPoints > 0) BeginSkillSelect();
        }

        public void AddIngredient(IngredientType type, int amount)
        {
            if (State == GameState.GameOver) return;

            switch (type)
            {
                case IngredientType.Cocktail: CocktailGauge.Add(amount); break;
                case IngredientType.Coffee: BeanGauge.Add(amount); break;
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
                if (def.boss == BossKind.Mid) Player.Heal(Balance.MidBossHealFraction);
                if (def.boss == BossKind.Final) Clear();
            }
        }

        void BeginLevelUp()
        {
            pendingLevelUps--;
            var offers = cards.Draw(CardCategory.Cocktail);
            if (offers.Count == 0) return; // 뽑을 카드가 모두 소진됨

            State = GameState.LevelUp;
            Time.timeScale = 0f;
            cardView.Show(offers, ChooseCard);
        }

        void ChooseCard(CardOffer offer)
        {
            cards.Take(offer.card, Stats);
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
            resultView.Show(cleared, RunTime, Kills, CocktailGauge.Level, Skills.UnlockedCount);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            GameBootstrap.Build();
        }
    }
}
