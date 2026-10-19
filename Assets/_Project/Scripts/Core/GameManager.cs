using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CCT
{
    public enum GameState { Playing, LevelUp, SkillSelect, HybridOffer, GameOver }

    /// <summary>
    /// 한 판(런)의 루트. 몹 처치 → 재료 드랍 → 게이지 → 레벨업 → 카드 선택 루프,
    /// 보스 → 원두 → 스킬 해금 루프, 런 종료(사망/클리어)를 관리한다.
    /// 재시작은 이 오브젝트를 통째로 파괴하고 직업 선택 화면부터 다시 띄운다.
    /// </summary>
    // 07:43/06:148 — 같은 프레임의 픽업(IngredientPickup.Update)·장판 피해(PlayerController.LateUpdate)가
    // 모두 끝난 뒤에 종료 판정과 보상 열기를 하도록 GameManager의 Update/LateUpdate를 다른 컴포넌트보다
    // 늦게 돌린다. (LateUpdate 자체는 모든 Update 뒤지만, 다른 LateUpdate와의 순서는 이 속성으로만 정해진다.)
    [DefaultExecutionOrder(1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        CardSystem cards;
        int completedCardChoices;
        CardSelectView cardView;
        SkillTreeView skillView;
        ResultView resultView;
        PauseView pauseView;
        HybridUnlockView hybridView;
        ComboTargetView comboView;
        /// <summary>07 §2 보상 대기열(카드 FIFO·하이브리드 예약·미사용 커피 포인트). 다음에 열 화면은
        /// 여기서 정하고, 이 클래스는 화면 전환만 맡는다.</summary>
        RunRewards rewards;
        /// <summary>이번 런에서 모은 재료량(런 메모리 누적). 전역 누적치에는 PermanentProgress가 저장 때마다
        /// "마지막 반영량과의 차이"만 더한다(08:152).</summary>
        int cocktailCollectedThisRun;
        int teaCollectedThisRun;
        int beansCollectedThisRun;
        /// <summary>08:106 결과 "이번 스페셜티" — 런 종료 집계가 activeRun을 지우기 전에 런 메모리에도 들고 있는다.</summary>
        int specialtyEarnedThisRun;
        /// <summary>08:102 "1초 '계속' 카운트다운" — Pause를 닫고 Playing으로 돌아가는 중이면 true, 그
        /// 동안엔 이동·피해·보상 처리가 전부 멈춘다(Update 참고).</summary>
        bool resuming;
        Coroutine resumeCoroutine;
        /// <summary>08:31 K 입력 — 즉시 열지 않고 프레임 끝(ResolveEndOfFrame)에서 종료 판정과 같은 프레임의
        /// 대기 보상을 먼저 처리한 뒤에만 연다(보상 예약을 K가 가로채거나 소모하지 않게).</summary>
        bool coffeeTreeRequested;
        /// <summary>Pause를 열기 직전 EventSystem 선택(닫을 때 복원).</summary>
        GameObject focusBeforePause;

        public GameState State { get; private set; } = GameState.Playing;
        /// <summary>08:16/98/102 — Playing뿐 아니라 LevelUp/SkillSelect 위에도 "겹쳐서" 열리는 오버레이.
        /// 그래서 State의 값이 아니라 별도 플래그다 — 닫으면 State가 가리키던 화면(전투든 선택창이든)
        /// 그대로 복원된다("오버레이 스택").</summary>
        public bool IsPaused { get; private set; }
        /// <summary>테스트용으로도 참조(PauseTests) — 1초 카운트다운 중인지.</summary>
        public bool IsResuming => resuming;
        /// <summary>07:112~120 "티니 보우". 레이피어 시작 + 이번 런 칵테일/차 카드 누적3씩 획득 시 해금,
        /// 이번 런에만 유지(새 GameManager로 넘어가면 자연히 초기화).</summary>
        public bool HybridUnlocked { get; private set; }
        public Job Job { get; private set; }
        int cocktailTypesSeen;
        float mixingRushRemaining;
        public float MixingRushRemaining => mixingRushRemaining;
        public int DeepSteepLevel => Mathf.Clamp(Cards.HighestStacks(CardCategory.Tea)-1,0,6);
        public string JobTraitStatus => Job==Job.Bartender
            ? mixingRushRemaining>0f ? $"혼합 러시 {mixingRushRemaining:0.0}초\n피해 +20% · 공속 +15%"
                : $"혼합 러시 · 새 칵테일 {cocktailTypesSeen%3}/3종\n새 3종마다 12초 강화"
            : $"깊은 우림 {DeepSteepLevel}/6 · 피해 +{DeepSteepLevel*2}%\n초당 추가 회복 +{DeepSteepLevel*.1f:0.0}";
        void RefreshJobTrait(bool cardTaken=false)
        {
            // 46/49: 새 칵테일 '종류' 3/6/9번째에 발동 — 같은 카드의 추가 중첩은 세지 않는다(2026-10-13 총 중첩 기준 버그 수정).
            int count=Cards.CountKinds(CardCategory.Cocktail);
            if(Job==Job.Bartender && cardTaken && CardAdvisor.MixingRushTriggers(cocktailTypesSeen,count))
            {
                mixingRushRemaining=12f;
                var flash=FadeVfx.Spawn(SpriteFactory.Crescent,Player.Position+Vector2.up*.5f,20f,
                    new Vector2(.5f,.5f),new Vector2(.8f,.8f),new Color(1f,.7f,.3f),.35f,SortOrders.AirVfx);
                flash.transform.SetParent(transform,true);
            }
            cocktailTypesSeen=count;
            Stats.jobDamagePercent=Job==Job.Bartender ? mixingRushRemaining>0f ? .20f : 0f : DeepSteepLevel*.02f;
            Stats.jobAttackSpeedPercent=Job==Job.Bartender && mixingRushRemaining>0f ? .15f : 0f;
            Stats.jobRegenBonus=Job==Job.TeaSommelier ? DeepSteepLevel*.1f : 0f;
        }
        public WeaponKind Weapon { get; private set; }
        public PlayerStats Stats { get; private set; }
        public PlayerController Player { get; private set; }
        public IngredientGauge CocktailGauge { get; private set; }
        public IngredientGauge TeaGauge { get; private set; }
        /// <summary>
        /// 07 §2 원두 게이지 — 10개마다 커피 포인트 1, 임계치 고정, 발생 포인트 전부 보존.
        /// 한 번에 여러 포인트가 생겨도 트리는 1회만 열리고 그 안에서 여러 개를 배분한다.
        /// </summary>
        public CoffeeBeanGauge BeanGauge { get; private set; }
        public RunRewards Rewards => rewards;
        /// <summary>미사용 커피 포인트(HUD/트리 상단 표시용).</summary>
        public int CoffeePoints => rewards.UnspentPoints;
        public SkillTree Skills { get; private set; }
        /// <summary>스페셜티로 직접 찍는 영구 강화 트리. 런 시작 시 세이브에서 읽어 스탯에 적용한다.</summary>
        public PermanentSkillTree PermanentSkills { get; private set; }
        public RunChallenge Challenge { get; private set; }
        public BossDirector Director { get; private set; }
        public HudView Hud { get; private set; }
        public CardSystem Cards => cards;
        public CardSelectView CardView => cardView;
        public SkillTreeView SkillView => skillView;
        public HybridUnlockView HybridView => hybridView;
        public PauseView PauseView => pauseView;
        public ResultView ResultScreen => resultView;
        public float RunTime { get; private set; }
        /// <summary>08:144 이번 런의 activeRun id. 종료 집계(PermanentProgress.CommitRunEnd)는 이 id로 한 번만.</summary>
        public string RunId { get; private set; }
        /// <summary>테스트 전용 — 1200초 제한시간(06:148)까지 실제로 기다리지 않고 근접시켜 검증한다.</summary>
        public void SetRunTimeForTests(float t) => RunTime = t;
        public int Kills { get; private set; }
        public bool Cleared { get; private set; }
        public string EndReason { get; private set; }
        /// <summary>06:148 — 같은 틱에 여러 종료 조건이 겹칠 수 있어, 즉시 EndRun하지 않고 플래그만
        /// 세운 뒤 LateUpdate에서 우선순위(사망 → 최종보스 처치 → 시간 초과)대로 한 번에 해소한다.</summary>
        bool playerDiedFlag;
        bool bossDefeatedFlag;
        /// <summary>이번 런의 기본 시드 (06:98). 카드/스폰/품종/보스 위치는 여기서 파생된 독립 스트림을 쓴다.</summary>
        public int Seed => rng.Seed;
        public IRandomSource SpawnRng => rng.Spawns;
        public IRandomSource BossPositionRng => rng.BossPositions;
        RunRandom rng;

        /// <summary>스페셜티(영구 재화)로 얻은 커피 원두 획득 배율. 1 = 보너스 없음.</summary>
        public float BeanGainMultiplier { get; private set; } = 1f;

        /// <summary>현재 살아있는 보스. 없으면 null.</summary>
        public Enemy ActiveBoss { get; set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            CancelPendingResult();
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        public void Initialize(Job job, WeaponKind weapon)
        {
            Time.timeScale = 1f;
            Job = job;
            Weapon = weapon;
            var preset = JobPreset.For(job);

            rng = new RunRandom(RunRandom.QaSeedOverride ?? RunRandom.GenerateSeed());
            Stats = new PlayerStats();
            // 활 초반 군중 처리: 기본 한 발이 두 대상까지 명중. 카드 관통은 기존 상한까지 누적.
            if (weapon == WeaponKind.Bow) Stats.pierceCount = 1;
            cards = new CardSystem(CardDatabase.LoadAll(), rng.Cards);
            Skills = new SkillTree(SkillDatabase.LoadAll());

            // 스페셜티로 미리 찍어둔 영구 강화는 런 시작 시 한 번 적용 — 런 종료해도 사라지지 않는 유일한 진행치.
            var save = PermanentProgress.Current;
            PermanentSkills = new PermanentSkillTree(PermanentSkillDatabase.LoadAll(), save.unlockedPermanentSkillIds, save.clearedRuns);
            PermanentSkills.ApplyAll(Stats, weapon);
            Challenge = new RunChallenge(PermanentSkills.AppliedCount(weapon), PermanentSkills.UnlockedCount);
            BeanGainMultiplier = 1f + Stats.beanGainPercent;
            // 08:142/154 런 시작: totalRuns+1, activeRun 기록, 즉시 저장.
            RunId = PermanentProgress.BeginRun(rng.Seed);
            // 69 준비 음료: 입장 시 한 번만 적용(카드 선택·일시정지·화면 전환에서 다시 적용하지 않음). 최대 체력이 바뀌므로 플레이어 생성 전.
            // 72 §4 숙련 특성: 출발 때 장착한 것만 런 시작 스냅샷(런 중 노트북 교체·저장 변경과 무관).
            MasteryTraits = DrinkLab.EquippedTraitsSnapshot();
            DrinkBuff = DrinkLab.BeginRun(RunId);
            if (DrinkBuff != null && MasteryTraits.Contains("finesse_4")) DrinkBuff = DrinkCalculator.ScalePositive(DrinkBuff, 1.05f); // 한 잔의 집중(항목 상한 유지)
            if (DrinkBuff != null) DrinkCalculator.ApplyTo(Stats, DrinkBuff);
            normalKillsSinceBundle = 0; normalBundles = 0; eliteBundles = 0; rpKills = 0; midBossKills = 0; finalBossKills = 0;

            CocktailGauge = new IngredientGauge(IngredientType.Cocktail, Balance.CocktailGaugeStart,
                Balance.CocktailGaugeGrowth, () => Stats.GaugeNeedMultiplier);
            TeaGauge = new IngredientGauge(IngredientType.Tea, Balance.CocktailGaugeStart,
                job == Job.TeaSommelier ? Balance.TeaHouseGaugeGrowth : Balance.CocktailGaugeGrowth, () => Stats.GaugeNeedMultiplier);
            BeanGauge = new CoffeeBeanGauge(Stats.beanGainPercent);
            rewards = new RunRewards(CocktailGauge, TeaGauge, BeanGauge);

            var camera = SetupCamera();
            var background = CreateBackground();
            Player = CreatePlayer(preset, weapon);
            Player.Mastery = MasteryRuntime.Attach(Player, MasteryTraits);
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
            // 69 준비 음료·보스 음료 재료 표시. 카드 선택·안내·일시정지·결과 창보다 먼저(뒤에) 만들어 어떤 창도 덮지 않게 한다(hudfix).
            DrinkHud = DrinkRunHud.Create(canvas.transform, this);
            Player.FinalForms.Unlocked += form => Hud.ShowBanner("최종 형태 해금", AttackFinalForms.Description(form), 2.5f);
            cardView = CardSelectView.Create(canvas.transform);
            skillView = SkillTreeView.Create(canvas.transform);
            resultView = ResultView.Create(canvas.transform, this);
            pauseView = PauseView.Create(canvas.transform, this);
            hybridView = HybridUnlockView.Create(canvas.transform);
            comboView = ComboTargetView.Create(canvas.transform, this); // 57 P1 목표 조합
            if (rememberedTargets.TryGetValue(job, out var remembered) && CanTarget(remembered)) ComboTarget = remembered;
            Guide = new RunGuide(this);

            Player.Died += OnPlayerDied;

            AudioService.SetMusicState(AudioService.MusicState.Run); // 09:135 "Playing 일반은 run"
            // 09:139 — 새 런은 항상 억제 해제 상태로 시작한다. 직전 런이 Pause/보상창이 열린 채로
            // GiveUp 등으로 끝났다면 억제 플래그가 true로 남아있을 수 있어(EndRun이 결과 화면 동안
            // 억제를 걸어두므로) 명시적으로 초기화한다 — 오버레이 상태 추적에만 기대지 않는다.
            AudioService.SetCombatSuppressed(false);
            AudioService.SetMusicDucked(false); // 직전 런의 Pause/보상창 덕킹(-8dB)을 새 런으로 가져오지 않는다
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

        /// <summary>04 §8 카페 배경(아트 있으면 cafe_floor/wall/counter/lamp, 없으면 기존 격자 타일).</summary>
        Transform CreateBackground() => CafeBackground.Create(transform, Job); // 47 직업 맵(바/찻집)

        PlayerController CreatePlayer(JobPreset preset, WeaponKind weapon)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, Balance.LaneCenterY, 0f);
            var player = go.AddComponent<PlayerController>();
            player.Init(Stats, preset.playerColor, preset.bodyArtId, Challenge);
            SkinSpriteSwapper.AttachIfEquipped(player.Visual, preset.job); // 치장 스킨(외형만, 능력치 무관). 없으면 기본 아트
            // 04 §8 장착 무기(기존 무기 아이콘 재사용, 손잡이 기준 회전). 몸체 손 anchor가 없으면 1차 임시 위치.
            var weaponPreset = WeaponPreset.For(weapon);
            if (!player.Visual.AttachWeaponArt("weapon_" + weaponPreset.kind.ToString().ToLowerInvariant(), weaponPreset.heldLengthRatio))
                player.Visual.AttachWeapon(Resources.Load<Sprite>(weaponPreset.iconResourcePath), weaponPreset.heldLengthRatio,
                    weapon == WeaponKind.Bow);
            WeaponSkinApplier.Apply(player.Visual, weapon); // 49 무료 무기 외형(스프라이트만, 판정·발사 무관). 없으면 기본 외형

            // 레이피어/검(근접), 총/활(원거리)은 각 쌍끼리 메커닉이 같음 — 치장(아이콘·SFX)만 다르다.
            if (weapon == WeaponKind.Gun || weapon == WeaponKind.Bow)
                go.AddComponent<PlayerProjectileWeapon>().Init(Stats, player, weapon);
            else
                go.AddComponent<RapierWeapon>().Init(Stats, player, weapon);

            return player;
        }

        void Update()
        {
            if (State == GameState.GameOver) return;

            // 08:16/33 — Playing뿐 아니라 카드/스킬 선택 화면 위에서도 Esc로 열고 닫을 수 있다.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                RequestEscape();
                return; // 한 번의 Esc는 최상위 화면에만 사용한다(커피 닫기 뒤 Pause 재개방 금지).
            }
            if (IsPaused) return;
            if (resuming) return; // 08:102 — 1초 "계속" 카운트다운 동안은 이동·피해·보상 처리 모두 정지

            // 57 P1: 카드 창 위에서도 T로 목표 조합을 바꿀 수 있다(이미 시간 정지).
            if (State == GameState.LevelUp && kb != null && kb.tKey.wasPressedThisFrame) { OpenComboTarget(); return; }
            if (State != GameState.Playing) return;

            if (kb != null && kb.hKey.wasPressedThisFrame) { OpenHiddenDetails(); return; }
            if (kb != null && kb.tKey.wasPressedThisFrame) { OpenComboTarget(); return; }
            Guide?.Tick(Time.deltaTime);

            // 08:31 "K: 커피 트리(포인트0이어도 조회), 시간정지" — Pause/카운트다운/다른 보상창 중에는
            // 위에서 이미 걸러진다. 실제로 여는 건 프레임 끝(ResolveEndOfFrame).
            if (kb != null && kb.kKey.wasPressedThisFrame) RequestCoffeeTree();

            RunTime += Time.deltaTime;
            mixingRushRemaining=Mathf.Max(0f,mixingRushRemaining-Time.deltaTime);
            RefreshJobTrait();
            Stats.Tick(Time.deltaTime);
            // 08:155 Playing 중 재료 변화가 있으면 게임시간 5초마다 체크포인트(activeRun 반영량과 같은 스냅샷).
            ReportRunProgress();
            PermanentProgress.CheckpointIfDue(RunId);
            // 보상 열기는 여기서 하지 않는다 — 이 Update가 일부 픽업 Update보다 먼저 돌면 같은 틱의 칵테일/차
            // 순서(07:43)와 종료 우선(06:148)이 깨진다. ResolveEndOfFrame 참고.
        }

        /// <summary>Esc 입력의 화면 우선순위. 키보드 Update와 회귀 시험이 같은 경로를 사용한다.</summary>
        public void RequestEscape()
        {
            if (State == GameState.GameOver) return;
            if (!IsPaused && CardView != null && CardView.IsLearningOpen) { CardView.CloseLearning(); return; }
            if (Hud.HiddenDetailsOpen) { CloseHiddenDetails(); return; }
            if (comboView != null && comboView.IsOpen) { comboView.Close(); return; }
                // 설정/커피 트리/조작 안내가 Pause 위에 열려 있으면 Esc는 그것만 먼저 닫는다(Pause는 유지).
                if (pauseView.IsSettingsOpen) pauseView.CloseSettings();
                else if (pauseView.IsSkillTreeOpen)
                {
                    pauseView.CloseSkillTree();
                    ClosePause();
                    CancelResumeCountdown();
                    Time.timeScale = State == GameState.Playing ? 1f : 0f;
                }
                else if (skillView.IsOpen && State == GameState.SkillSelect && !IsPaused)
                {
                    AudioService.PlaySfx("ui_cancel");
                    skillView.Continue();
                }
                else if (pauseView.IsTutorialOpen) pauseView.CloseTutorial();
                else if (IsPaused) ClosePause(); else OpenPause();
        }

        // ───── 57(2026-10-15) P1 목표 조합 ─────
        /// <summary>같은 실행 세션 안에서 직업별 마지막 목표를 기억한다(저장 파일은 바꾸지 않음).</summary>
        static readonly System.Collections.Generic.Dictionary<Job, HiddenSkill> rememberedTargets =
            new System.Collections.Generic.Dictionary<Job, HiddenSkill>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRememberedTargets() => rememberedTargets.Clear();

        /// <summary>플레이어가 고른 목표 히든(없으면 null). 카드 창·HUD가 진행도를 보여 준다.</summary>
        public HiddenSkill? ComboTarget { get; private set; }
        public bool ComboTargetOpen => comboView != null && comboView.IsOpen;
        public ComboTargetView ComboView => comboView;
        public RunGuide Guide { get; private set; }
        public IHiddenRunState HiddenState => new HiddenRunState(this);

        public bool HiddenDiscovered(string id) => PermanentProgress.Current != null && PermanentProgress.Current.discoveredCardIds != null &&
            PermanentProgress.Current.discoveredCardIds.Contains(id);

        /// <summary>이 직업·무기로 카드 창에 나올 수 있는 카드인지 — 찻집은 칵테일 몹이 없어 칵테일 카드 창이 열리지 않는다(47).</summary>
        public bool CardObtainable(string id)
        {
            var card = cards?.FindInPool(id);
            if (card == null) return false;
            if (Job == Job.TeaSommelier && card.category == CardCategory.Cocktail) return false;
            return CardSystem.IsWeaponCompatible(card, Weapon.IsRanged());
        }

        public HiddenReachability Reachability(HiddenSkillDef def) => HiddenRequirements.Reachability(def, Job, Weapon, CardObtainable);

        public bool CanTarget(HiddenSkill skill)
        {
            var def = HiddenSkillDatabase.Get(skill);
            return (Player == null || !Player.Hidden.Has(skill)) && Reachability(def).reachable;
        }

        public void SetComboTarget(HiddenSkill? skill)
        {
            if (skill.HasValue && !CanTarget(skill.Value)) return;
            ComboTarget = skill;
            if (skill.HasValue) rememberedTargets[Job] = skill.Value; else rememberedTargets.Remove(Job);
        }

        /// <summary>T/HUD 버튼/카드 창 버튼. Playing이면 시간을 멈추고, 카드 창이면 그 위에 겹친다(카드 단축키는 잠금).</summary>
        public void OpenComboTarget()
        {
            if (comboView == null || comboView.IsOpen || IsPaused || resuming) return;
            if (State != GameState.Playing && State != GameState.LevelUp) return;
            bool fromPlaying = State == GameState.Playing;
            IsPaused = true;
            Time.timeScale = 0f;
            if (fromPlaying) EnterFrozenAudio();
            var es = UnityEngine.EventSystems.EventSystem.current;
            var focus = es != null ? es.currentSelectedGameObject : null;
            comboView.Show(() =>
            {
                IsPaused = false;
                Time.timeScale = State == GameState.Playing ? 1f : 0f;
                if (fromPlaying) ExitFrozenAudioIfClear();
                if (cardView != null && cardView.IsOpen) cardView.RefreshTargetInfo();
                if (es != null && focus != null && focus.activeInHierarchy) es.SetSelectedGameObject(focus);
            });
        }

        public void OpenHiddenDetails()
        {
            if(State!=GameState.Playing || IsPaused || resuming) return;
            IsPaused=true; Time.timeScale=0f; EnterFrozenAudio();
            Hud.ShowHiddenDetails();
        }
        public void CloseHiddenDetails()
        {
            if(!Hud.HiddenDetailsOpen) return;
            Hud.HideHiddenDetails(); IsPaused=false;
            Time.timeScale=State==GameState.Playing ? 1f : 0f;
            ExitFrozenAudioIfClear();
        }

        /// <summary>07:44~49 — 대기 중인 보상을 우선순위대로 하나 연다(하이브리드 → 카드 → 새 포인트가 생긴
        /// 뒤의 커피 트리). 연 화면이 있으면 true. 후보 0장인 카드 대기열은 건너뛰고 다음을 본다.</summary>
        bool OpenNextReward()
        {
            while (true)
            {
                switch (rewards.TakeNext(Skills.AnyAvailable(), out var category))
                {
                    case RewardKind.Hybrid:
                        BeginHybridOffer();
                        return true;
                    case RewardKind.Card:
                        if (BeginLevelUp(category)) return true;
                        continue;
                    case RewardKind.CoffeeTree:
                        BeginSkillSelect();
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>07:46 "보상이 남으면 Playing을 한 프레임 끼워 넣지 않는다" — 선택을 끝낸 그 호출 안에서
        /// 다음 보상을 바로 연다(timeScale 0 유지). 남은 보상이 없을 때만 Playing으로 돌아간다.</summary>
        void FinishRewardChoice()
        {
            if (State == GameState.GameOver) return;
            PermanentProgress.SaveIfDirty(); // 08:155 UI 선택 시 즉시(발견 저장과 겹치면 바뀐 것이 없어 생략)
            if (OpenNextReward()) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            ExitFrozenAudioIfClear();
        }

        /// <summary>08:31 K 입력 경로. Playing 중(Pause·카운트다운·다른 보상창 아님)에만 받아 두고, 프레임 끝에서
        /// 종료 판정과 같은 프레임 보상을 처리한 뒤 남는 게 없을 때 연다.</summary>
        public void RequestCoffeeTree()
        {
            if (State != GameState.Playing || IsPaused || resuming) return;
            coffeeTreeRequested = true;
        }

        /// <summary>즉시 여는 테스트 공개 진입점(대기 보상 확인 없이). 실제 K 입력은 RequestCoffeeTree를 쓴다.
        /// 포인트가 0이어도 조회용으로 열리고, 자동 열림 예약은 소모한다(07:49).</summary>
        public void OpenCoffeeTree()
        {
            if (State != GameState.Playing || IsPaused || resuming) return;
            rewards.DisarmTreeAutoOpen();
            BeginSkillSelect();
        }

        /// <summary>08:98/102 — 지금 State가 Playing이든 LevelUp/SkillSelect든 그 위에 그대로 겹쳐 연다
        /// (아래 화면은 건드리지 않음 — "오버레이 스택"). 이미 얼어있는 상태(LevelUp/SkillSelect)면
        /// Time.timeScale은 그대로 0이고, Playing 중이었으면 여기서 처음 얼린다. Esc 처리(Update)와
        /// 테스트가 함께 쓰는 public 진입점 — 이 코드베이스는 실제 키 입력을 흉내내는 대신 이렇게 공개
        /// API를 직접 호출해 검증하는 관례를 따른다(CardSelectView.Choose 등과 동일 패턴).</summary>
        public void OpenPause()
        {
            if (IsPaused) return;
            // 결과 화면(GameOver)에서는 열지 않는다 — HUD 일시정지 버튼 직접 클릭 등으로 열리면 음악 덕킹이 다음 런까지 남았다(2026-10-08 오디오 탭 측정).
            if (State == GameState.GameOver) return;
            if (resuming) CancelResumeCountdown(); // 카운트다운 도중 다시 Esc를 누르면 Pause로 되돌린다
            IsPaused = true;
            Time.timeScale = 0f;
            EnterFrozenAudio();
            var es = UnityEngine.EventSystems.EventSystem.current;
            focusBeforePause = es != null ? es.currentSelectedGameObject : null;
            pauseView.Show();
            PermanentProgress.SaveIfDirty(); // 08:155 일시정지 시 즉시
        }

        /// <summary>08:102 — 원래 있던 화면(Playing 또는 선택창)으로 그대로 복원한다. 선택창이었으면
        /// 그 화면이 요구하는 정지 상태(timeScale=0)를 건드리지 않는다(카운트다운도 없이 즉시 복원).
        /// Playing으로 돌아갈 때만 1초 "계속" 카운트다운 동안 이동·피해가 멈춘 채로 기다렸다가 풀어준다.
        /// PauseView의 "계속" 버튼도 이걸 직접 호출한다(Esc뿐 아니라 마우스 클릭으로도 닫을 수 있어야
        /// 하므로 public).</summary>
        public void ClosePause()
        {
            if (!IsPaused) return;
            IsPaused = false;
            pauseView.Hide();
            // QA 2026-10-04: 일시정지 아래 선택창(카드 등)의 키보드 포커스를 되돌린다(방향키/Enter 경로 유지).
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(focusBeforePause != null && focusBeforePause.activeInHierarchy ? focusBeforePause : null);
            focusBeforePause = null;
            ExitFrozenAudioIfClear();
            if (State == GameState.Playing)
            {
                resuming = true;
                pauseView.ShowResumeCountdown();
                resumeCoroutine = StartCoroutine(ResumeAfterCountdown());
            }
        }

        /// <summary>08:102 "1초 '계속' 카운트다운" — 실제 초 단위로 기다려야 해서(Time.timeScale=0인 동안)
        /// WaitForSecondsRealtime을 쓴다.</summary>
        System.Collections.IEnumerator ResumeAfterCountdown()
        {
            yield return new WaitForSecondsRealtime(1f);
            resuming = false;
            resumeCoroutine = null;
            pauseView.HideResumeCountdown();
            Time.timeScale = 1f;
        }

        void CancelResumeCountdown()
        {
            if (resumeCoroutine != null) StopCoroutine(resumeCoroutine);
            resumeCoroutine = null;
            resuming = false;
            pauseView.HideResumeCountdown();
        }

        /// <summary>09:139 "Pause/보상창은 음악 계속 재생하되 Music-8dB, 전투SFX 중단" — "보상창"은 Pause뿐
        /// 아니라 카드/영구스킬/하이브리드 해금창도 포함한다(전부 Time.timeScale=0으로 얼리는 오버레이).
        /// SetCombatSuppressed(true)가 진입 시점에 이미 울리던 전투 SFX를 즉시 끊고, 그 뒤 새 전투 SFX
        /// 재생 시도도 명시적으로 막는다(Time.timeScale=0에 기대는 우연한 차단이 아님).</summary>
        void EnterFrozenAudio()
        {
            AudioService.SetMusicDucked(true);
            AudioService.SetCombatSuppressed(true);
        }

        /// <summary>오버레이 스택(IsPaused)과 State가 둘 다 정리됐을 때만 덕킹·억제를 해제한다 — 예를 들어
        /// LevelUp 위에 Pause가 열려 있다가 LevelUp만 먼저 끝나도(State→Playing) IsPaused가 아직 true면
        /// 계속 덕킹/억제 상태를 유지해야 한다.</summary>
        void ExitFrozenAudioIfClear()
        {
            if (!IsPaused && State == GameState.Playing)
            {
                AudioService.SetMusicDucked(false);
                AudioService.SetCombatSuppressed(false);
            }
        }

        /// <summary>08:99~100 "런 포기" — Pause에서만 호출된다(이미 timeScale=0이라 다른 종료 조건과
        /// 같은 틱에 겹칠 일이 없음 — LateUpdate의 우선순위 큐를 거칠 필요 없이 바로 끝낸다).</summary>
        public void GiveUp()
        {
            if (State == GameState.GameOver) return;
            ClosePause();
            EndRun(false, "포기");
        }

        /// <summary>06:148 — 시뮬레이션 틱 마지막 우선순위: 플레이어 HP0 → 최종보스 사망 →
        /// 런시간 제한 도달 → 다음 보상/선택. LateUpdate는 이 프레임의 모든 Update가 끝난 뒤
        /// 실행되고(DefaultExecutionOrder로 다른 LateUpdate보다도 뒤), 같은 프레임에 세워진
        /// playerDiedFlag/bossDefeatedFlag를 여기서 한 번에 해소하면 어떤 컴포넌트가 먼저 이벤트를 쐈는지와
        /// 무관하게 우선순위가 항상 지켜진다.</summary>
        void LateUpdate() => ResolveEndOfFrame();

        /// <summary>프레임 끝 처리(LateUpdate가 매 프레임 호출, EditMode 통합 테스트도 같은 메서드를 직접 부른다).
        /// 1) 종료 판정(사망 → 클리어 → 시간 초과). 종료되면 대기 보상·K 요청은 버린다(07:47).
        /// 2) Playing 중이면 이 프레임까지 쌓인 보상을 연다 — 모든 픽업 Update 뒤라 같은 틱 칵테일→차(07:43).
        /// 3) 연 보상이 없을 때만 K 요청으로 커피 트리를 연다(보상 예약을 소모하지 않게).</summary>
        public void ResolveEndOfFrame()
        {
            if (State != GameState.GameOver)
            {
                if (playerDiedFlag) EndRun(false, "사망");
                else if (bossDefeatedFlag) EndRun(true, "클리어");
                else if (RunTime >= Balance.RunTimeLimitSeconds) EndRun(false, "시간 초과");
            }
            playerDiedFlag = false;
            bossDefeatedFlag = false;

            bool treeRequested = coffeeTreeRequested;
            coffeeTreeRequested = false;
            if (State != GameState.Playing || IsPaused || resuming) return;
            if (OpenNextReward()) return;
            if (treeRequested) OpenCoffeeTree();
        }

        /// <summary>EditMode 통합 테스트 전용 — 실제 사망 이벤트(Player.Died)와 같은 플래그를 세운다.</summary>
        public void MarkPlayerDiedForTests() => OnPlayerDied();

        public void AddIngredient(IngredientType type, int amount)
        {
            if (State == GameState.GameOver) return;

            // 재료를 주울 때마다 그 종류 안에서 품종이 무작위로 하나 골라진다 — 도감의 "품종 수집" 대상.
            // 06:98 — 품종은 시드에서 파생된 독립 스트림을 쓴다.
            var varieties = MaterialVarieties.For(type);
            string varietyId = varieties.Count > 0 ? varieties[rng.Varieties.RangeInt(varieties.Count)].id : null;

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
                    // 07:34 — 픽업별 반올림 없이 누적 원량 기준 E=floor(R×(1+보너스))의 증가분만 넣는다.
                    int effectiveBefore = BeanGauge.EffectiveTotal;
                    BeanGauge.AddRaw(amount);
                    beansCollectedThisRun += BeanGauge.EffectiveTotal - effectiveBefore;
                    break;
            }

            Guide?.OnPickup(type); // 57 P1 첫 수집 안내
            // 첫 품종 발견은 즉시 저장 — 이 픽업의 재료량을 먼저 런 누적에 알려 같은 스냅샷에 포함한다.
            ReportRunProgress();
            if (varietyId != null) PermanentProgress.DiscoverMaterialVariety(varietyId);
        }

        void ReportRunProgress() => PermanentProgress.SetRunProgress(RunId, cocktailCollectedThisRun,
            teaCollectedThisRun, beansCollectedThisRun, RunTime);

        public void OnEnemyKilled(Vector2 position, EnemyDef def)
        {
            Kills++;
            DrinkLab.OnKill(RunId, Kills); // 69 0~4킬 보존 / 5킬째 이 런에 귀속
            if (!def.IsBoss) { rpKills++; GrantSupplyForKill(position, def); } // 72 §2·§3
            Player?.HiddenRuntime?.OnEnemyKilled(def); // 41 핫 토디/주말 러시

            for (int i = 0; i < def.dropCount; i++)
            {
                var jitter = def.dropCount > 1 ? Random.insideUnitCircle * def.radius : Vector2.zero;
                IngredientPickup.Spawn(this, Player, position + jitter, def.dropType, def.ingredientAmount,
                    transform, forceAttraction: def.IsBoss);
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
                GrantDrinkMaterials(position, def);
                // 최종보스면 Clear()가 곧 결과 화면 음악 전환을 대신 처리하니 여기서 run으로 안 되돌린다.
                if (def.boss != BossKind.Final) AudioService.SetMusicState(AudioService.MusicState.Run);
                if (def.boss == BossKind.Mid || def.boss == BossKind.Enhanced)
                    Player.Heal(Balance.MidBossHealFraction);
                if (def.boss == BossKind.Enhanced) GrantSpecialty();
                if (def.boss == BossKind.Final) Clear();
            }
        }

        // ───── 69 음료 제조 ─────
        /// <summary>이번 런 입장 때 마신 준비 음료의 버프(없으면 null). 런 동안 한 번만 적용된 값.</summary>
        public DrinkBuff DrinkBuff { get; private set; }
        public DrinkRunHud DrinkHud { get; private set; }
        /// <summary>종료 정산 결과(종료 전 null).</summary>
        public DrinkSettlement DrinkSettlement { get; private set; }
        int bossKillIndex;

        /// <summary>보스 사망 즉시 음료 재료를 확정 기록(런 임시 보상, 저장). 픽업 연출은 표시만 — 이동 중 놓쳐도 지급은 이미 확정.</summary>
        void GrantDrinkMaterials(Vector2 position, EnemyDef def)
        {
            bossKillIndex++;
            if (def.boss == BossKind.Final) finalBossKills++; else midBossKills++;
            Player?.Mastery?.OnBossKilled(bossKillIndex); // 72 안정3 쉬는 시간
            // 72 §2: 중간 보스(강화 포함) 3묶음(3번째 희귀 보장), 최종 보스 6묶음(5·6번째 희귀 보장). 보유량 보정, runId·보상 ID로 결정.
            string awardId = $"boss{bossKillIndex}_{def.boss}";
            var items = DrinkLab.RollAward(DrinkLab.SourceFor(def.boss), RunId, awardId);
            if (items.Count == 0) return;
            if (!DrinkLab.RecordBossAward(RunId, awardId, def.name, items)) return;
            DrinkHud?.OnBossAward(position, items);
        }

        // ───── 72 §2·§3 일반/정예 수급과 연구 포인트 집계 ─────
        /// <summary>출발 때 장착한 숙련 특성(런 동안 고정).</summary>
        public System.Collections.Generic.List<string> MasteryTraits { get; private set; } = new System.Collections.Generic.List<string>();
        int normalKillsSinceBundle, normalBundles, eliteBundles, rpKills, midBossKills, finalBossKills;
        public int ResearchKills => rpKills;
        public int MidBossKills => midBossKills;
        public int FinalBossKills => finalBossKills;

        /// <summary>일반 처치 25회마다 1묶음(일반 표), 정예(탄막형)는 처치마다 1묶음(정예 표). 정예는 일반 카운트에 넣지 않는다. 보상 ID로 1회만.</summary>
        void GrantSupplyForKill(Vector2 position, EnemyDef def)
        {
            if (def.archetype == EnemyArchetype.EliteBarrage)
            {
                eliteBundles++;
                RecordSupply(position, DrinkSupply.Source.Elite, $"elite{eliteBundles}", def.name);
                return;
            }
            if (++normalKillsSinceBundle < DrinkSupply.NormalKillsPerBundle) return;
            normalKillsSinceBundle = 0;
            normalBundles++;
            RecordSupply(position, DrinkSupply.Source.Normal, $"normal{normalBundles}", "처치 보상");
        }

        void RecordSupply(Vector2 position, DrinkSupply.Source source, string awardId, string label)
        {
            var items = DrinkLab.RollAward(source, RunId, awardId);
            if (items.Count == 0 || !DrinkLab.RecordBossAward(RunId, awardId, label, items)) return;
            DrinkHud?.OnSupplyAward(position, items);
        }

        /// <summary>69 종료 정산(같은 runId는 한 번만). explicitAbandon: Pause의 포기/타이틀로 종료만 true(정상 앱 종료·사망·시간 종료는 false).</summary>
        void SettleDrinks(RunEndKind kind, bool explicitAbandon)
        {
            if (DrinkSettlement != null) return;
            try { DrinkSettlement = DrinkLab.EndRun(RunId, kind, Kills, explicitAbandon, rpKills, midBossKills, finalBossKills); }
            catch (System.Exception e) { Debug.LogWarning($"[Drinks] 정산 실패: {e.GetType().Name}: {e.Message}"); }
        }

        /// <summary>강화 보스 처치 시 즉시 영구 저장 — 이후 플레이어가 죽어도 이번에 얻은 스페셜티는 남는다.</summary>
        void GrantSpecialty()
        {
            // 런당 상한·runId 확인 — 같은 보스 처치 이벤트가 중복돼도 두 번 지급하지 않는다. 즉시 저장.
            if (!PermanentProgress.AwardRunSpecialty(RunId, Balance.SpecialtyDropAmount)) return;
            specialtyEarnedThisRun += Balance.SpecialtyDropAmount;
            Hud.ShowBanner("스페셜티 획득!", $"+{Balance.SpecialtyDropAmount} (영구 재화, 다음 런부터 적용)", 3f);
        }

        /// <summary>테스트용 — 마지막으로 연 카드 보상의 카테고리(같은 틱 칵테일→차 순서 검증).</summary>
        public CardCategory? LastOpenedCardCategory { get; private set; }

        bool BeginLevelUp(CardCategory category)
        {
            var offers = cards.Draw(category, Weapon.IsRanged(), 3, RunTime, completedCardChoices);
            if (offers.Count == 0) return false; // 이 카테고리 카드가 모두 소진됨 — 다음 대기열로

            LastOpenedCardCategory = category;
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            EnterFrozenAudio();
            SaveOnSelectionOpen();
            AudioService.PlaySfx("levelup"); // 09: "카드 보상 발생" — 화면이 뜨는 시점
            // 09 levelup_glint: 시간정지 중에도 보이도록 unscaled 재생.
            if (Player != null)
                ArtVfx.Spawn("levelup_glint", Player.Position + new Vector2(0f, 1f), 0f, 1.1f, 0.6f, Color.white,
                    SortOrders.AirVfx, 1.1f, true, true);
            cardView.Show(offers, ChooseCard);
            return true;
        }

        void ChooseCard(CardOffer offer)
        {
            cards.Take(offer.card, Stats);
            RefreshJobTrait(true);
            completedCardChoices++;
            Player.FinalForms.OnCardTaken(offer.card, cards.GetStacks(offer.card));
            EvaluateHidden();
            PermanentProgress.DiscoverCard(offer.card.id);
            Player.Heal(Balance.LevelUpHealFraction);
            Guide?.OnCardChosen(); // 57 P1 다음 카드 창 안내

            // 07:115 — 이 선택으로 조건이 "새로" 충족됐는지 확인. 정비/커피 노드는 CardSystem에 안 쌓여서
            // CountOwned에 자동으로 안 잡힌다("정비/커피 노드는 세지 않는다"). 레이피어 시작 전용, 런당 1회.
            if (!HybridUnlocked && !rewards.HybridPending && Job == Job.Bartender && Weapon == WeaponKind.Rapier &&
                cards.CountOwned(CardCategory.Cocktail) >= 3 && cards.CountOwned(CardCategory.Tea) >= 3)
            {
                rewards.RequestHybrid();
            }

            // 07:31 — 리큐르/꿀 같은 요구 감소 카드로 잔량이 새 요구 이상이 되면 바로 보상을 예약한다.
            rewards.ReevaluateGauges();
            FinishRewardChoice();
        }

        /// <summary>41 히든 스킬: 카드 선택·하이브리드·커피 노드 해금 직후 이번 런 상태로 판정. 새로 충족한 것만 런당 1회 해금,
        /// 배너와 도감 발견(hidden_* id, 즉시 저장).</summary>
        public System.Collections.Generic.List<HiddenSkillDef> EvaluateHidden()
        {
            var unlocked = Player.Hidden.Evaluate(new HiddenRunState(this));
            foreach (var def in unlocked)
            {
                PermanentProgress.DiscoverCard(def.id);
                if (ComboTarget.HasValue && ComboTarget.Value == def.skill)
                {
                    Player?.Mastery?.OnSignatureCompleted(); // 72 서비스3 시그니처 준비
                    ComboTarget = null; rememberedTargets.Remove(Job); // 57 목표 달성
                }
            }
            if (unlocked.Count > 0)
            {
                string names = string.Join(" · ", unlocked.ConvertAll(d => d.name));
                Hud.ShowBanner("히든 스킬 발견: " + names, unlocked.Count == 1 ? unlocked[0].effect : $"{unlocked.Count}종 동시 해금", 2.5f);
                AudioService.PlaySfx("skill_unlock");
            }
            return unlocked;
        }

        /// <summary>테스트용 — 카드 선택 화면 없이 실제 선택과 같은 경로(중첩·최종 형태·히든 판정)로 카드를 받는다.</summary>
        public void GrantCardForTests(CardData card)
        {
            cards.Take(card, Stats);
            RefreshJobTrait(true);
            Player.FinalForms.OnCardTaken(card, cards.GetStacks(card));
            EvaluateHidden();
        }

        /// <summary>49 카드 추천 — 이번 런의 실제 직업·무기·보유 카드·체력·히든 상태(읽기 전용).</summary>
        public CardAdviceContext BuildCardAdviceContext()
        {
            return new CardAdviceContext
            {
                job = Job, weapon = Weapon, run = new HiddenRunState(this),
                hiddenOwned = s => Player != null && Player.Hidden.Has(s),
                hiddenDiscovered = id => PermanentProgress.Current != null && PermanentProgress.Current.discoveredCardIds != null &&
                    PermanentProgress.Current.discoveredCardIds.Contains(id),
                hp = Player != null ? Player.Hp : 0f, maxHp = Player != null ? Player.MaxHp : 0f,
                cocktailKinds = cards.CountKinds(CardCategory.Cocktail),
                cocktailStacks = cards.CountOwned(CardCategory.Cocktail), teaStacks = cards.CountOwned(CardCategory.Tea),
                deepSteepLevel = DeepSteepLevel, hybridUnlocked = HybridUnlocked, hybridPending = rewards.HybridPending,
                damageReduction = Stats.damageReduction, extraThrusts = Stats.extraThrusts, pierceCount = Stats.pierceCount,
            };
        }

        sealed class HiddenRunState : IHiddenRunState
        {
            readonly GameManager g;
            public HiddenRunState(GameManager g) => this.g = g;
            public int Stacks(string cardId) => g.cards.StacksOf(cardId);
            public bool Coffee(string nodeId)
            {
                foreach (var node in g.Skills.Nodes)
                    if (node != null && node.id == nodeId) return g.Skills.IsUnlocked(node);
                return false;
            }
            public bool Hybrid => g.HybridUnlocked;
            public int FinalFormCount => g.Player.FinalForms.Count;
            public bool HasFinalForm(AttackFinalForm form) => g.Player.FinalForms.Has(form);
            public Job Job => g.Job;
        }

        /// <summary>07:116 — 랜덤 추첨이 아닌 확정 해금 창을 연다.</summary>
        void BeginHybridOffer()
        {
            State = GameState.HybridOffer;
            Time.timeScale = 0f;
            EnterFrozenAudio();
            SaveOnSelectionOpen();
            hybridView.Show(ChooseHybrid);
        }

        /// <summary>07:117 — 레이피어는 유지, 보조 활을 추가(슬롯 소비·카드 제거·재료 소모 없음).
        /// 이번 런에만 유지되고, 도감 발견(카드 34+하이브리드1의 그 하나)은 영구.</summary>
        void ChooseHybrid()
        {
            HybridUnlocked = true;
            PermanentProgress.DiscoverCard("hybrid_tea_martini");
            Player.gameObject.AddComponent<HybridBowWeapon>().Init(Stats, Player);
            EvaluateHidden();
            AudioService.PlaySfx("hybrid_unlock");
            FinishRewardChoice();
        }

        /// <summary>08:88~91 커피 트리 — 한 번 열어 남은 포인트를 여러 개 배분한다. 포인트를 다 쓰면(또는
        /// 더 찍을 노드가 없으면) 닫히고, "계속하기"로 닫으면 남은 포인트는 보존된다.</summary>
        void BeginSkillSelect()
        {
            State = GameState.SkillSelect;
            Time.timeScale = 0f;
            EnterFrozenAudio();
            SaveOnSelectionOpen();
            skillView.Show(Skills, () => rewards.UnspentPoints, SpendCoffeePoint, FinishRewardChoice);
        }

        /// <summary>08:155 "UI 선택 시에도 즉시" — 카드/하이브리드/커피 트리 창이 열리는 순간 그때까지의 런 재료를
        /// 반영해 저장한다(바뀐 것이 없으면 생략). 선택 창에서 바로 앱이 끝나도 진입 시점 체크포인트가 남는다.</summary>
        void SaveOnSelectionOpen()
        {
            ReportRunProgress();
            PermanentProgress.SaveIfDirty();
        }

        bool SpendCoffeePoint(SkillData node)
        {
            if (!rewards.TryUnlockNode(Skills, node, Stats)) return false;
            Challenge.SetCoffeeCount(Skills.UnlockedCount);
            EvaluateHidden();
            PermanentProgress.DiscoverSkill(node.id); // 08:140/154 첫 커피 노드 발견 즉시 저장
            AudioService.PlaySfx("skill_unlock"); // 09: "커피/영구 해금" — 커피 쪽
            return true;
        }

        void OnPlayerDied()
        {
            if (State == GameState.GameOver) return;
            playerDiedFlag = true;
        }

        void Clear()
        {
            if (State == GameState.GameOver) return;
            bossDefeatedFlag = true;
        }

        void EndRun(bool cleared, string reason)
        {
            // "포기"는 Pause(Playing 복귀 예정)를 닫자마자 곧장 여기로 들어와서, 아직 안 끝난 1초 "계속"
            // 카운트다운이 살아있을 수 있다 — 그대로 두면 1초 뒤 GameOver인데 Time.timeScale이 다시
            // 1로 풀려버린다.
            CancelResumeCountdown();
            Cleared = cleared;
            EndReason = reason;
            State = GameState.GameOver;
            Time.timeScale = 0f;
            CommitRun(cleared ? RunOutcome.Cleared : reason == "포기" ? RunOutcome.Abandoned : RunOutcome.Failed);
            SettleDrinks(cleared ? RunEndKind.Cleared : reason == "사망" ? RunEndKind.Died
                : reason == "시간 초과" ? RunEndKind.TimeUp : RunEndKind.Abandoned, reason == "포기");
            // 67 최근 기록: 결과 패널 지연(사망 동작)과 무관하게 종료 즉시 같은 runId로 한 번만 남긴다.
            RecordRunHistory(cleared ? RunEndKind.Cleared : reason == "사망" ? RunEndKind.Died
                : reason == "시간 초과" ? RunEndKind.TimeUp : RunEndKind.Abandoned);
            AudioService.SetMusicDucked(false); // 포기 등으로 Pause 중 끝났을 수 있음 — 덕킹 남기지 않는다
            // 결과 화면도 09:139가 말하는 "얼린 오버레이" 중 하나다 — GameOver 이후 남아있을 수 있는
            // 적/발사체 등의 지연 이벤트(예: 코루틴에 물린 폭발)가 결과 화면 위로 전투음을 내지 않도록
            // 명시적으로 억제한다. clear/defeat는 AlwaysAllowedIds라 아래 결과 스팅어는 그대로 난다.
            AudioService.SetCombatSuppressed(true);
            AudioService.PlayResultStingerThenReturnToMenu(cleared); // 09:139 "0.5초 fadeout+clear/defeat,1.5초 후 menu"
            var summary = new ResultSummary
            {
                cleared = cleared, reason = reason, job = Job, weapon = Weapon, hybridUnlocked = HybridUnlocked,
                runSeconds = RunTime, kills = Kills, cocktailLevel = CocktailGauge.Level, teaLevel = TeaGauge.Level,
                coffeeSkillsUnlocked = Skills.UnlockedCount, cocktailCollected = cocktailCollectedThisRun,
                teaCollected = teaCollectedThisRun, beansCollected = beansCollectedThisRun,
                specialtyEarned = specialtyEarnedThisRun, seed = Seed, record = RunRecord,
            };
            // 사망: 저장·집계는 위에서 즉시 1회 확정하고, 결과 패널만 쓰러지는 동작(defeat, 실시간 재생)이 보인 뒤에 연다
            // (2026-10-08 아트 검수: 결과 패널이 사망 동작을 가림). 이 런 오브젝트가 사라지면(재시작/타이틀) 예약도 함께 사라진다.
            if (!cleared && reason == "사망" && Application.isPlaying) pendingResult = StartCoroutine(ShowResultAfterDeath(summary));
            else resultView.Show(summary);
        }

        /// <summary>사망 후 결과 패널을 열기까지의 실시간 지연(defeat 4장@10fps + 쓰러진 자세 유지).</summary>
        public const float DeathResultDelaySeconds = 1.2f;
        Coroutine pendingResult;
        /// <summary>사망 동작을 보여 주는 중이라 결과 패널이 아직 열리지 않았는지.</summary>
        public bool ResultPending => pendingResult != null;

        IEnumerator ShowResultAfterDeath(ResultSummary summary)
        {
            yield return new WaitForSecondsRealtime(DeathResultDelaySeconds);
            pendingResult = null;
            if (Instance == this && State == GameState.GameOver && resultView != null) resultView.Show(summary);
        }

        void CancelPendingResult()
        {
            if (pendingResult == null) return;
            StopCoroutine(pendingResult);
            pendingResult = null;
        }

        /// <summary>08:142~145 런 종료 집계. 남은 재료 차이 반영 + 결과 하나만 +1 + 즉시 저장. 같은 runId는 한 번만
        /// (EndRun 뒤의 QuitToTitle/Restart/GoToTitle/앱 종료가 다시 불러도 무시된다).</summary>
        void CommitRun(RunOutcome outcome)
        {
            ReportRunProgress();
            PermanentProgress.CommitRunEnd(RunId, outcome, RunTime);
        }

        // ───── 67(2026-10-16) 플레이 결과 기록 ─────
        /// <summary>이번 런을 기록한 값(종료 전에는 null). 저장 실패·보호 중이어도 메모리에서 읽을 수 있다.</summary>
        public RunRecord RunRecord { get; private set; }
        /// <summary>마지막 기록 저장 결과(종료 전에는 null).</summary>
        public RunHistory.AddResult? RunHistoryResult { get; private set; }

        /// <summary>종료 경로(결과·포기·타이틀로 종료·정상 앱 종료)가 여러 번 불러도 이 런은 한 번만 기록한다.</summary>
        void RecordRunHistory(RunEndKind kind)
        {
            if (RunRecord != null) return;
            try
            {
                RunRecord = BuildRunRecord(kind);
            }
            catch (System.Exception e)
            {
                // 기록 구성 실패가 종료·결과 화면을 막지 않게 한다(저장 실패를 성공으로 표시하지도 않는다).
                Debug.LogWarning($"[RunHistory] 기록 구성 실패: {e.GetType().Name}: {e.Message}");
                return;
            }
            RunHistoryResult = RunHistory.Add(RunRecord);
        }

        /// <summary>종료 시점의 값 복사 스냅샷. 카드 중첩은 CardSystem의 실제 보유분, 조합은 실제로 해금한 것만.</summary>
        public RunRecord BuildRunRecord(RunEndKind kind)
        {
            var record = new RunRecord
            {
                runId = string.IsNullOrEmpty(RunId) ? System.Guid.NewGuid().ToString("N") : RunId,
                endedUtc = System.DateTime.UtcNow.ToString("o"),
                endKind = kind.ToString(),
                job = Job.ToString(), jobName = JobPreset.For(Job).name,
                weapon = Weapon.ToString(), weaponName = WeaponPreset.For(Weapon).name,
                runSeconds = RunTime, kills = Kills,
                cocktailLevel = CocktailGauge != null ? CocktailGauge.Level : 0,
                teaLevel = TeaGauge != null ? TeaGauge.Level : 0,
                researchUnlocked = Skills != null ? Skills.UnlockedCount : 0,
                specialtyEarned = specialtyEarnedThisRun,
                hybridUnlocked = HybridUnlocked,
                appVersion = Application.version,
            };
            if (kind == RunEndKind.Died)
            {
                var source = Player != null ? Player.LethalDamageSource : null;
                record.deathCause = source.HasValue ? source.Value.Label : DamageSource.UnknownLabel;
                record.deathCauseKind = (source.HasValue ? source.Value.kind : DamageKind.Unknown).ToString();
            }

            if (cards != null)
            {
                var owned = cards.OwnedSnapshot();
                owned.Sort((a, b) =>
                {
                    int c = a.Key.category.CompareTo(b.Key.category);
                    if (c != 0) return c;
                    c = b.Value.CompareTo(a.Value);
                    return c != 0 ? c : string.CompareOrdinal(a.Key.displayName, b.Key.displayName);
                });
                foreach (var kv in owned)
                    record.cards.Add(new RunCardEntry
                    {
                        id = kv.Key.id, name = kv.Key.displayName, category = kv.Key.category.ToString(),
                        stacks = kv.Value, maxStacks = kv.Key.maxStacks,
                    });
            }

            if (HybridUnlocked)
            {
                var hybrid = cards?.FindInPool("hybrid_tea_martini");
                record.combos.Add(new RunNamedEntry
                {
                    kind = "hybrid", id = "hybrid_tea_martini",
                    name = hybrid != null && !string.IsNullOrEmpty(hybrid.displayName) ? hybrid.displayName : "티니 보우",
                });
            }
            if (Player != null)
            {
                foreach (var def in HiddenSkillDatabase.All)
                    if (Player.Hidden.Has(def.skill))
                        record.combos.Add(new RunNamedEntry { kind = "hidden", id = def.id, name = JobStudyTheme.HiddenName(def, Job) });
                foreach (AttackFinalForm form in System.Enum.GetValues(typeof(AttackFinalForm)))
                    if (Player.FinalForms.Has(form))
                        record.combos.Add(new RunNamedEntry { kind = "finalForm", id = form.ToString(), name = RunRecordText.FinalFormName(form) });
            }
            if (Skills != null)
                foreach (var node in Skills.Nodes)
                    if (node != null && Skills.IsUnlocked(node))
                        record.research.Add(new RunNamedEntry { kind = "research", id = node.id, name = JobStudyTheme.Name(node, Job) });
            return record;
        }

        /// <summary>08:155 정상 앱 종료 — 진행 중 런은 포기로 집계하고 즉시 저장(다음 실행에서 "중단된 런"으로 보이지 않게).</summary>
        /// <summary>08:178 종료 요청(QuitGuard) 직전: 진행 중 런을 포기로 1회 집계하고 저장을 시도한다(OnApplicationQuit과 같은 경로, 멱등).</summary>
        public void PrepareForQuit()
        {
            if (State != GameState.GameOver) RecordRunHistory(RunEndKind.Abandoned); // 67 정상 앱 종료도 포기로 1회 기록
            if (State != GameState.GameOver) CommitRun(RunOutcome.Abandoned);
            if (State != GameState.GameOver) SettleDrinks(RunEndKind.Abandoned, false); // 69 앱 종료는 명시적 포기가 아니다(음료 소모)
            PermanentProgress.SaveIfDirty();
            DrinkLab.RetrySave();
        }

        void OnApplicationQuit()
        {
            if (State != GameState.GameOver) RecordRunHistory(RunEndKind.Abandoned);
            if (State != GameState.GameOver) CommitRun(RunOutcome.Abandoned);
            if (State != GameState.GameOver) SettleDrinks(RunEndKind.Abandoned, false);
            PermanentProgress.SaveIfDirty();
            DrinkLab.RetrySave();
        }

        /// <summary>2026-10-12 Android: 홈/앱 전환·전화 등으로 백그라운드가 되면 OS가 예고 없이 프로세스를 끝낼 수 있다
        /// (OnApplicationQuit 미호출). 그래서 진행 스냅샷과 설정을 즉시 저장하고, 전투 중이면 일시정지 화면을 열어
        /// 돌아왔을 때 바로 피해를 받지 않게 한다. 런은 포기로 집계하지 않는다(강제 종료 뒤엔 기존 "중단된 런" 복구 경로).
        /// 데스크톱(창 최소화 등)은 기존 동작 그대로 두기 위해 모바일 OS에서만.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused && MobilePlatform.IsMobileOs) HandleAppBackgrounded();
        }

        /// <summary>시험·OnApplicationPause 공용.</summary>
        public void HandleAppBackgrounded()
        {
            if (State == GameState.Playing && !IsPaused) OpenPause(); // OpenPause가 SaveIfDirty도 한다
            if (State != GameState.GameOver) ReportRunProgress();
            PermanentProgress.SaveIfDirty();
            if (SettingsService.IsDirty) SettingsService.RetrySave();
        }

        public void Restart()
        {
            CancelPendingResult();
            if (State != GameState.GameOver) RecordRunHistory(RunEndKind.Abandoned); // 67 이미 기록한 런이면 무시
            CommitRun(RunOutcome.Abandoned); // 결과 화면에서만 호출 — 이미 집계된 런이면 무시
            SettleDrinks(RunEndKind.Abandoned, false); // 69 결과 화면 뒤라 이미 정산됨 — 아니면 소모로 정산
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            GameBootstrap.RestartToSelect();
        }

        /// <summary>08장 결과 화면의 "타이틀" 버튼.</summary>
        public void GoToTitle()
        {
            CancelPendingResult();
            if (State != GameState.GameOver) RecordRunHistory(RunEndKind.Abandoned);
            CommitRun(RunOutcome.Abandoned); // 결과 화면에서만 호출 — 이미 집계된 런이면 무시
            SettleDrinks(RunEndKind.Abandoned, false); // 69 결과 화면 뒤라 이미 정산됨 — 아니면 소모로 정산
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            GameBootstrap.ShowTitle();
        }

        /// <summary>08:99~100 Pause의 "타이틀로 종료" — "런 포기"와 같은 확인이지만 Result를 보여주지
        /// 않고 저장 후 곧장 Title로 간다. 08:154 "런 종료: 즉시" — 남은 재료 차이와 포기 집계를 같은 저장에 반영.</summary>
        public void QuitToTitle()
        {
            CancelPendingResult();
            if (State != GameState.GameOver) RecordRunHistory(RunEndKind.Abandoned); // 67 타이틀로 종료 = 포기로 1회 기록
            CommitRun(RunOutcome.Abandoned); // 08:100 "저장 후 Title" — 포기로 1회 집계(이미 종료된 런이면 무시)
            SettleDrinks(RunEndKind.Abandoned, true); // 69 Pause의 타이틀로 종료 = 명시적 포기(이미 정산된 런이면 무시)
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            GameBootstrap.ShowTitle();
        }
    }
}
