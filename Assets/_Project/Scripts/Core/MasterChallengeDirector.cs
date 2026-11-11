using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>마스터 도전 전용 적 정의(원본 공유 EnemyDef·최종 보스와 분리된 사본). 직업별로 한 번만 만들어 재사용한다.</summary>
    public static class MasterDefs
    {
        static readonly Dictionary<Job, EnemyDef> fair = new Dictionary<Job, EnemyDef>();
        static readonly Dictionary<Job, EnemyDef> reward = new Dictionary<Job, EnemyDef>();

        /// <summary>작은 진상 손님: 원본 최종 보스(진상 손님 2500HP·접촉20·boss=Final)와 다른 일반 개체. 최종 보스 처치/클리어 플래그를 만들지 않는다(boss=None).
        /// 계열은 원본 손님과 같은 중립(MonsterFamilies 미등록 = 중립) — 음료 상성 추가 피해 대상 아님.</summary>
        public static EnemyDef FairCustomer(Job job)
        {
            if (fair.TryGetValue(job, out var d)) return d;
            var src = job == Job.TeaSommelier ? EnemyDef.TeaCustomer : EnemyDef.DifficultCustomer;
            d = src.CloneDef();
            d.name = job == Job.TeaSommelier ? "박람회 까다로운 손님" : "박람회 진상 손님";
            d.boss = BossKind.None;
            d.archetype = EnemyArchetype.Rush;
            d.radius = MasterRules.FairCustomerRadius;
            d.hp = MasterRules.FairCustomerHp;
            d.contactDamage = MasterRules.FairCustomerContact;
            d.speed = MasterRules.FairCustomerSpeed;
            d.dropCount = 1;
            d.dropType = job == Job.TeaSommelier ? IngredientType.Tea : IngredientType.Cocktail;
            d.ingredientAmount = EnemyDef.Imp.ingredientAmount;
            d.enragedSpriteResourcePath = null;
            d.summonDef = null;
            d.visualScaleMultiplier = MasterRules.FairCustomerVisualScale;
            fair[job] = d;
            return d;
        }

        /// <summary>보상 몬스터: 공격 수치 전부 0, 돌진(Rush)만 — 자폭·장판·투사체·소환 없음. 실제 피해 차단은 Enemy.Harmless가 한 번 더 보장한다.</summary>
        public static EnemyDef RewardMonster(Job job)
        {
            if (reward.TryGetValue(job, out var d)) return d;
            var src = job == Job.TeaSommelier ? EnemyDef.SpringLeafSprite : EnemyDef.Imp;
            d = src.CloneDef();
            d.name = "보상 " + src.name;
            d.archetype = EnemyArchetype.Rush;
            d.contactDamage = 0f;
            d.explodesOnContact = false;
            d.burstZoneRadius = 0f; d.burstZoneDamagePerSecond = 0f; d.burstZoneSeconds = 0f;
            d.projectileDamage = 0f; d.barrageCount = 1;
            d.summonDef = null;
            d.boss = BossKind.None;
            d.dropCount = 2;
            d.speed = Mathf.Min(src.speed, 1.6f);
            reward[job] = d;
            return d;
        }
    }

    /// <summary>
    /// 2026-11-11 마스터 도전 진행(RunMode.Endless 런에서만 붙는다). MasterChallengeState의 유한 웨이브 목표를 실제 생성·처치로 진행한다.
    /// - 생성: 목표량을 구간별 시간에 나눠 풀고(Time.deltaTime — 일시정지·카드·현수막 설치 중 0), 동시 개체 상한·프레임 예산 안에서만 낸다.
    ///   못 낸 몫은 '아직 생성 안 함'으로 남을 뿐 버리지 않고, 이벤트 후 한꺼번에 몰리지 않는다(풀린 양만큼만).
    /// - 완료: 웨이브 소속 개체가 모두 처치되면 다음 단계. 소속 개체가 처치 없이 사라지면(정리 등) 처리로 센다(진행 멈춤 방지).
    /// - 박람회: 도전 선택 → 남은 적 처리 대기 → NPC 현수막 설치(실제 시간 3.5초, GameManager 정지) → 박람회 1/3.
    /// </summary>
    public class MasterChallengeDirector : MonoBehaviour
    {
        GameManager game;
        public MasterChallengeState State { get; private set; }
        public float PrepSeconds { get; set; } = MasterRules.PrepSecondsDefault;
        readonly List<Enemy> tracked = new List<Enemy>();
        float released;
        float transitionElapsed;
        FairBannerShow show;
        readonly HashSet<string> cocktailKinds = new HashSet<string>();
        public int CocktailUniqueCount => cocktailKinds.Count;
        public bool HasCocktailKind(string key) => !string.IsNullOrEmpty(key) && cocktailKinds.Contains(key);
        public int PercentPerUnique { get; set; } = MasterRules.CocktailAttackPercentPerUniqueDefault;
        /// <summary>박람회 성공 보상으로 실제 기록한 주기(중복 방지 확인용).</summary>
        public readonly List<long> FairRewardsGranted = new List<long>();
        public event System.Action Changed;
        public void NotifyChanged() => Changed?.Invoke();
        public bool BannersEnabled { get; set; } = true;

        public static MasterChallengeDirector Attach(GameManager game)
        {
            var go = new GameObject("MasterChallenge");
            go.transform.SetParent(game.transform, false);
            var d = go.AddComponent<MasterChallengeDirector>();
            d.game = game;
            d.State = new MasterChallengeState();
            d.State.WaveStarted += d.OnWaveStarted;
            d.State.FairSucceeded += d.OnFairSucceeded;
            d.State.CycleCompleted += c => d.Changed?.Invoke();
            d.OnWaveStarted(1);
            return d;
        }

        /// <summary>박람회 성공 안내(보상 1웨이브 시작 배너에 합쳐 보인다 — 같은 호출 사슬에서 곧바로 덮이지 않게).</summary>
        string fairSuccessSub;
        /// <summary>마지막으로 띄운 HUD 배너(시험·검수용).</summary>
        public string LastBannerTitle { get; private set; }
        public string LastBannerSub { get; private set; }

        void Banner(string title, string sub, float seconds)
        {
            LastBannerTitle = title; LastBannerSub = sub;
            if (BannersEnabled && game != null && game.Hud != null) game.Hud.ShowBanner(title, sub, seconds);
        }

        void OnWaveStarted(long wave)
        {
            released = 0f;
            tracked.Clear();
            var seg = MasterRules.SegmentOf(wave);
            if (show != null && seg == MasterSegment.Reward) { show.Finish(); show = null; }
            if (seg == MasterSegment.Fair && show != null)
            {
                // 현수막이 걸린 동안 박람회 제목은 천 글자로, HUD에는 부제만(큰 HUD 제목 y≈190/1080이 천 아랫부분과 겹침 — 2026-11-11 오프스크린 검수).
                show.SetLabel($"박람회 {MasterRules.FairIndex(wave)}/{MasterRules.FairWaves}");
                Banner("", SubLine(wave), 2f);
            }
            else if (seg == MasterSegment.Reward && fairSuccessSub != null)
            {
                Banner("박람회 성공!", fairSuccessSub + " · " + SubLine(wave), 3.5f);
                fairSuccessSub = null;
            }
            else
            {
                string title = seg == MasterSegment.Fair ? $"박람회 {MasterRules.FairIndex(wave)}/{MasterRules.FairWaves}"
                    : seg == MasterSegment.Reward ? $"보상 손님 {MasterRules.RewardIndex(wave)}/{MasterRules.RewardWaves}"
                    : $"웨이브 {EndlessRules.Show(wave)} · 재료 모으기 {MasterRules.InCycle(wave)}/{MasterRules.CollectWaves}";
                Banner(title, SubLine(wave), 2f);
            }
            Changed?.Invoke();
        }

        string SubLine(long wave)
        {
            var seg = MasterRules.SegmentOf(wave);
            long target = MasterRules.TargetCount(wave);
            if (seg == MasterSegment.Reward) return $"공격하지 않는 보상 손님 {target}명 · 마음껏 처치하세요";
            long r = State.ActiveReduction;
            string red = r > 0 ? $" · 약화 {r}%" : "";
            if (seg == MasterSegment.Fair) return $"진상 손님 {target}명 막기 · 상성 없음(중립){red}";
            return $"손님 {target}명 처리{red}";
        }

        void OnFairSucceeded(long cycle)
        {
            // 성공 주기 ID당 1회(State가 보장) + DrinkLab의 runId·awardId 중복 거부로 저장/재시도/중복 콜백에도 한 번.
            var items = FairRewardItems();
            string awardId = $"master_fair_c{cycle}";
            bool recorded = game != null && DrinkLab.RecordBossAward(game.RunId, awardId, "박람회 성공 보상", items, true);
            if (recorded) FairRewardsGranted.Add(cycle);
            fairSuccessSub = recorded ? $"모든 재료 기본 묶음 {items.Count}종 지급(판매 불가 재료)" : "보상은 이미 기록되었습니다";
            Changed?.Invoke();
        }

        /// <summary>현재 플레이 가능한 모든 재료(카탈로그·수급 표)의 데이터 정의 기본 묶음 × 배수. 임의 대량 지급 없음.</summary>
        public static List<DrinkAmount> FairRewardItems()
        {
            var list = new List<DrinkAmount>();
            foreach (var row in DrinkSupply.Table)
            {
                if (row == null || DrinkIngredients.Get(row.id) == null || row.bundle <= 0) continue;
                list.Add(new DrinkAmount(row.id, row.bundle * MasterRules.FairRewardBundles));
            }
            return list;
        }

        void Update()
        {
            if (game == null || State == null || game.State == GameState.GameOver) return;
            if (State.Phase == MasterPhase.Transition) { TickTransition(Time.unscaledDeltaTime); return; }
            if (game.IsPaused || game.State != GameState.Playing) return;
            Tick(Time.deltaTime);
        }

        /// <summary>시험용/본 루프 공용 한 틱(전투 시간 dt).</summary>
        public void Tick(float dt)
        {
            if (State == null) return;
            PruneTracked();
            switch (State.Phase)
            {
                case MasterPhase.Wave: TickWave(dt); break;
                case MasterPhase.ClearingForFair:
                    if (Enemy.Active.Count == 0 && game.BeginMasterFreeze())
                    {
                        State.BeginTransition();
                        transitionElapsed = 0f;
                        show = FairBannerShow.Create(game, ClampedPrep);
                        Banner("", "NPC가 현수막을 거는 중 · 전투 정지", ClampedPrep); // 큰 제목 없이 부제만(천과 겹치지 않음)
                        Changed?.Invoke();
                    }
                    break;
            }
        }

        float ClampedPrep => MasterRules.ClampPrep(PrepSeconds);

        /// <summary>현수막 설치(실제 시간). 이 동안 GameManager는 정지(timeScale 0·IsPaused) — 생성·피해·전투 시간 모두 멈춘다.</summary>
        public void TickTransition(float unscaledDt)
        {
            if (State.Phase != MasterPhase.Transition) return;
            transitionElapsed += Mathf.Max(0f, unscaledDt);
            show?.Step(transitionElapsed);
            if (transitionElapsed < ClampedPrep) return;
            State.FinishTransition();
            game.EndMasterFreeze();
            show?.Settle();
            string startRed = State.ActiveReduction > 0 ? " · 약화 " + State.ActiveReduction + "%" : "";
            Banner("", "박람회 시작! 진상 손님 " + State.Target + "명 막기" + startRed, 2.5f); // 제목은 천 글자(박람회 1/3). 720p 좌측 패널에 가리지 않게 짧게
            Changed?.Invoke();
        }

        public float TransitionElapsed => transitionElapsed;

        void TickWave(float dt)
        {
            long target = State.Target;
            float rel = MasterRules.ReleaseSeconds(State.Segment);
            released = Mathf.Min(target, released + dt * target / Mathf.Max(0.1f, rel));
            if (State.Spawned == 0 && target > 0) released = Mathf.Max(released, 1f); // 첫 1마리는 바로
            int budget = MasterRules.SpawnsPerFrame;
            while (budget-- > 0 && State.Spawned < target && State.Spawned < (long)released)
            {
                var e = SpawnOne();
                if (e == null) break; // 동시 상한 — 다음 프레임에 다시(버리지 않음)
                e.MasterWaveTag = State.Wave;
                e.Killed += OnTrackedKilled;
                tracked.Add(e);
                State.NotifySpawned();
            }
        }

        void OnTrackedKilled(Enemy e)
        {
            if (!tracked.Remove(e)) return;
            State.NotifyCleared();
            Changed?.Invoke();
        }

        void PruneTracked()
        {
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                if (tracked[i] != null) continue;
                tracked.RemoveAt(i);
                State.NotifyCleared();
            }
        }

        Enemy SpawnOne()
        {
            var sp = game.Spawner;
            if (sp == null) return null;
            long wave = State.Wave;
            double factor = MasterRules.ReductionFactor(State.ActiveReduction);
            switch (State.Segment)
            {
                case MasterSegment.Reward:
                    // 절대 웨이브1 기준 HP(배율 1)·도전 체력 배율 제외·공격 0·무해.
                    return sp.SpawnMaster(MasterDefs.RewardMonster(game.Job), EndlessRules.HpScale(1), 1f, true, true);
                case MasterSegment.Fair:
                    return sp.SpawnMaster(MasterDefs.FairCustomer(game.Job), Scale(EndlessRules.HpScale(wave), factor), Scale(EndlessRules.AttackScale(wave), factor), false, false);
                default:
                    var def = EnemyRoster.PickEndless(wave, game.RunTime, game.SpawnRng, game.Job, out _);
                    return sp.SpawnMaster(def, Scale(EndlessRules.HpScale(wave), factor), Scale(EndlessRules.AttackScale(wave), factor), false, false);
            }
        }

        /// <summary>감소 계수를 한 번 곱한 배율. 0이 되면(R ≥ 100) 아주 작은 양수로 둬 Enemy가 최소 1로 정수화한다(0을 '배율 없음 = 1'로 오해하지 않게).</summary>
        public static float Scale(float baseScale, double factor)
        {
            double v = baseScale * factor;
            return v <= 0d ? 1e-9f : (float)v;
        }

        // ───── 플레이어 선택 ─────
        public bool ChooseRepeat()
        {
            if (!State.ChooseRepeat()) return false;
            return true;
        }

        /// <summary>박람회 도전 접수(중복 입력 무시). 감소율은 여기서 한 번 확정.</summary>
        public bool ChooseFair()
        {
            if (!State.ChooseFair()) return false;
            if (Enemy.Active.Count > 0) Banner("박람회 준비", $"남은 손님 {Enemy.Active.Count}명을 먼저 처리하세요", 2.5f);
            Changed?.Invoke();
            return true;
        }

        /// <summary>칵테일 성공 제조 1잔 기록. 새 종류일 때만 공격력 갱신(U × 증가율, 덮어쓰기).</summary>
        public bool RegisterCraft(RunCup cup)
        {
            if (cup == null || cup.Kind != DrinkKind.Cocktail || string.IsNullOrEmpty(cup.recipeKey)) return false;
            if (!cocktailKinds.Add(cup.recipeKey)) return false;
            if (game?.Stats != null) game.Stats.masterDamagePercent = MasterRules.CocktailAttackPercent(cocktailKinds.Count, PercentPerUnique) / 100f;
            Changed?.Invoke();
            return true;
        }

        public int CocktailAttackPercent => MasterRules.CocktailAttackPercent(cocktailKinds.Count, PercentPerUnique);

        public void OnRunEnded()
        {
            show?.Finish();
            show = null;
        }

        void OnDestroy() => OnRunEnded();
    }

    /// <summary>
    /// NPC 2명(운반 담당 porter·설치 담당 rigger)이 화면 위쪽 벽 영역에 '박람회' 현수막을 거는 연출. 실제 시간으로 진행(전투 정지 중).
    /// 2026-11-11 인수 아트(Resources/MasterChallenge, manifest 15 clip) 사용. 아트 계약 3500ms 일정(접근 0–800 / 설치 800–2300 / 퇴장 2300–3500,
    /// 현수막 1050–2050·마지막 프레임 고정, 글자는 펼침 완료 07 프레임부터)을 설정 길이(3~4초)에 비례해 적용한다.
    /// 설치 자세(노출 02/03)의 손 좌표가 현수막 최종 핀(43,20)/(1237,20)에 오도록 발 위치를 계산한다(flipX면 x=255−x).
    /// 아트를 못 읽으면 단색 천·기존 몸 아트 임시 대체(최종 아트 아님).
    /// </summary>
    public class FairBannerShow : MonoBehaviour
    {
        SpriteRenderer rigger, porter, banner;
        Sprite[] riggerIn, riggerInstall, riggerOut, porterIn, porterInstall, porterOut, bannerFrames;
        TextMesh label;
        float duration;
        float npcScale = 1f;
        Vector2 riggerStand, porterStand, riggerOff, porterOff;
        public bool UsingArt { get; private set; }

        /// <summary>현수막 상단 = 카메라 위 가장자리에서 이만큼 아래(월드). 2026-11-11 오프스크린 검수: 1.15·배율1은 HUD 레벨 막대(720p y≈20–80px) 아래에 깔림 →
        /// 1.45(레벨 막대 바로 아래)·배율 0.8(폭 8월드 = 화면 중앙 37.5%, 좌측 정보 패널·우측 음료 버튼 열과 겹치지 않음).</summary>
        public const float BannerTopFromCamTop = 1.45f;
        public const float BannerScale = 0.8f;
        /// <summary>계약 일정(ms, 3500 기준).</summary>
        public const float ContractMs = MasterArt.TotalMs;
        /// <summary>현수막 글자 표시 시작 = 배너 시작 + 7프레임(125ms×7).</summary>
        public const float LabelStartMs = MasterArt.BannerStartMs + 7 * 125f;

        public static FairBannerShow Create(GameManager game, float duration)
        {
            var cam = Camera.main;
            var root = new GameObject("FairBanner");
            if (game != null) root.transform.SetParent(game.transform, false);
            var s = root.AddComponent<FairBannerShow>();
            s.duration = Mathf.Max(0.1f, duration);
            Vector2 camPos = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 6f;
            // 시작/퇴장 위치는 최소 16:9 폭 기준 화면 밖(좁은 창·배치 화면 비율에서 화면 가장자리에 갑자기 나타나지 않게 — 2026-11-11 연속 프레임 검수).
            float halfW = halfH * (cam != null ? Mathf.Max(16f / 9f, cam.aspect) : 16f / 9f);
            Vector2 bannerTop = camPos + new Vector2(0f, halfH - BannerTopFromCamTop);
            s.Build(bannerTop, camPos.x, halfW);
            // 카메라는 가로로 플레이어를 따라간다(Y 고정) — 박람회 동안 현수막이 화면 위 벽 영역에 남도록 카메라에 붙인다(월드 위치 유지).
            if (cam != null) root.transform.SetParent(cam.transform, true);
            s.Step(0f);
            return s;
        }

        void Build(Vector2 bannerTop, float camX, float halfW)
        {
            riggerIn = MasterArt.Load("npc_rigger_walk_in"); riggerInstall = MasterArt.Load("npc_rigger_install"); riggerOut = MasterArt.Load("npc_rigger_walk_out");
            porterIn = MasterArt.Load("npc_porter_walk_in"); porterInstall = MasterArt.Load("npc_porter_install"); porterOut = MasterArt.Load("npc_porter_walk_out");
            bannerFrames = MasterArt.Load("banner_install");
            UsingArt = riggerIn != null && riggerInstall != null && riggerOut != null && porterIn != null && porterInstall != null && porterOut != null && bannerFrames != null;

            var body = ArtLibrary.Get("bartender");
            npcScale = body != null ? body.BodyVisualScale : 1f;

            banner = MakeRenderer("Banner", bannerTop, SortOrders.BackgroundProp);
            banner.transform.localScale = Vector3.one * BannerScale;
            if (!UsingArt)
            {
                banner.sprite = SpriteFactory.Square;
                banner.color = new Color(0.95f, 0.88f, 0.72f);
            }

            // 손(설치 노출 02/03 최고점)이 최종 핀에 닿도록 발 위치 = 핀 − 손 로컬 오프셋×배율.
            Vector2 pinL = bannerTop + MasterArt.BannerLocal(MasterArt.PinLeftPx) * BannerScale;
            Vector2 pinR = bannerTop + MasterArt.BannerLocal(MasterArt.PinRightPx) * BannerScale;
            riggerStand = pinL - MasterArt.NpcAnchorLocal(MasterArt.RiggerGripPx, false) * npcScale; // 왼쪽 끝: 오른쪽(중앙)을 보며 앞손으로 핀
            porterStand = pinR - MasterArt.NpcAnchorLocal(MasterArt.PorterGripPx, false) * npcScale; // 오른쪽 끝: 바깥을 보며 뒷손으로 핀(퇴장 방향 유지)
            // 2026-11-11 발 미끄럼 보정: 걷기 거리 = 실측 보폭 속도 × 걷기 시간(계약 시각 기준 — 3~4초 비례에도 걸음·이동이 같은 비율). 시작/끝은 투명도 전환.
            WalkDistance = MasterArt.WalkSpeedPerMs(npcScale) * MasterArt.WalkInEndMs;
            riggerOff = riggerStand - new Vector2(WalkDistance, 0f);
            porterOff = porterStand + new Vector2(WalkDistance, 0f);

            rigger = MakeRenderer("NpcRigger", riggerOff, SortOrders.BackgroundProp + 2);
            porter = MakeRenderer("NpcPorter", porterOff, SortOrders.BackgroundProp + 2);
            rigger.transform.localScale = porter.transform.localScale = Vector3.one * npcScale;
            cordL = MakeRenderer("CordLeft", Vector2.zero, SortOrders.BackgroundProp + 1);
            cordR = MakeRenderer("CordRight", Vector2.zero, SortOrders.BackgroundProp + 1);
            foreach (var c in new[] { cordL, cordR }) { c.sprite = SpriteFactory.Square; c.color = new Color(0.36f, 0.22f, 0.12f); c.enabled = false; }
            if (!UsingArt)
            {
                var a = ArtLibrary.Get("bartender");
                var t = ArtLibrary.Get("tea_sommelier");
                rigger.sprite = a?.Clip("idle")?.frames is Sprite[] fa && fa.Length > 0 ? fa[0] : SpriteFactory.Capsule;
                porter.sprite = t?.Clip("idle")?.frames is Sprite[] ft && ft.Length > 0 ? ft[0] : SpriteFactory.Capsule;
            }

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(transform, false);
            // 글자 안전 영역(중앙 80% 폭·60% 높이)의 중심 = 현수막 상단에서 높이 50%.
            textGo.transform.position = new Vector3(bannerTop.x, bannerTop.y - 192f / 128f * 0.5f * BannerScale, -0.01f);
            label = textGo.AddComponent<TextMesh>();
            label.text = "박람회";
            label.font = UiFactory.Font;
            label.fontSize = 96;
            label.characterSize = 0.075f * BannerScale;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.32f, 0.17f, 0.09f, 0f); // 크림 천 위 짙은 갈색 글자
            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null) { if (UiFactory.Font != null) mr.sharedMaterial = UiFactory.Font.material; mr.sortingOrder = SortOrders.BackgroundProp + 1; }
        }

        SpriteRenderer MakeRenderer(string name, Vector2 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>현재 계약 시각(ms, 0~3500) — 설정 길이에 비례.</summary>
        public float ContractTime { get; private set; }
        public int BannerFrame { get; private set; } = -1;
        public string RiggerClip { get; private set; }
        public string PorterClip { get; private set; }
        public bool LabelVisible => label != null && label.color.a > 0.5f;
        public string LabelText => label != null ? label.text : "";
        /// <summary>걸린 현수막 글자 교체(박람회 n/3). 글자 안전 영역(폭 80%) 안에 들도록 짧은 문구만.</summary>
        public void SetLabel(string text) { if (label != null && !string.IsNullOrEmpty(text)) label.text = text; }
        public Vector2 RiggerPosition => rigger != null ? (Vector2)rigger.transform.position : Vector2.zero;
        public Vector2 PorterPosition => porter != null ? (Vector2)porter.transform.position : Vector2.zero;
        public Vector2 RiggerStand => riggerStand;
        public Vector2 PorterStand => porterStand;
        public float NpcScale => npcScale;
        public float NpcAlpha { get; private set; } = 1f;
        public float WalkDistance { get; private set; }
        public Vector2 BannerTopWorld => banner != null ? (Vector2)banner.transform.position : Vector2.zero;
        public bool RiggerFlipped => rigger != null && rigger.flipX;
        public bool PorterFlipped => porter != null && porter.flipX;
        public bool BannerVisible => banner != null && banner.enabled && banner.sprite != null;
        public bool NpcsPresent => rigger != null || porter != null;

        /// <summary>실제 경과(초) → 계약 시각으로 진행. Director의 실제 시간 상태 타이머로만 호출(프레임 수 의존 없음).</summary>
        public void Step(float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / duration) * ContractMs;
            ContractTime = t;
            if (UsingArt) StepArt(t); else StepFallback(t);
            if (label != null)
            {
                var c = label.color;
                c.a = t >= LabelStartMs ? 1f : 0f;
                label.color = c;
            }
        }

        void StepArt(float t)
        {
            if (t < MasterArt.WalkInEndMs)
            {
                // 접근(walk_in 루프) — 일정 속도 직선 이동. rigger는 왼쪽에서(오른쪽 보기), porter는 오른쪽에서(flipX, 왼쪽 보기).
                float k = t / MasterArt.WalkInEndMs;
                SetNpc(rigger, Vector2.Lerp(riggerOff, riggerStand, k), riggerIn, "npc_rigger_walk_in", t, false);
                SetNpc(porter, Vector2.Lerp(porterOff, porterStand, k), porterIn, "npc_porter_walk_in", t, true);
            }
            else if (t < MasterArt.InstallEndMs)
            {
                float it = t - MasterArt.WalkInEndMs;
                SetNpc(rigger, riggerStand, riggerInstall, "npc_rigger_install", it, false);
                SetNpc(porter, porterStand, porterInstall, "npc_porter_install", it, false);
            }
            else
            {
                // 퇴장: rigger는 왼쪽으로(flipX), porter는 오른쪽으로(정방향, 천 없음).
                float ot = t - MasterArt.InstallEndMs;
                float k = Mathf.Clamp01(ot / (ContractMs - MasterArt.InstallEndMs));
                SetNpc(rigger, Vector2.Lerp(riggerStand, riggerOff, k), riggerOut, "npc_rigger_walk_out", ot, true);
                SetNpc(porter, Vector2.Lerp(porterStand, porterOff, k), porterOut, "npc_porter_walk_out", ot, false);
            }
            // 접근 시작·퇴장 끝 투명도 전환(걷기 속도를 보폭에 맞추느라 가까이서 나타나고 사라진다).
            NpcAlpha = t < MasterArt.WalkInEndMs ? Mathf.Clamp01(t / MasterArt.NpcFadeMs)
                : t >= MasterArt.InstallEndMs ? Mathf.Clamp01((ContractMs - t) / MasterArt.NpcFadeMs) : 1f;
            foreach (var sr in new[] { rigger, porter }) if (sr != null) { var c = sr.color; c.a = NpcAlpha; sr.color = c; }

            // 현수막: BannerStartMs 전에는 운반 천(porter 그림)만. 이후 8프레임 1회·마지막 고정.
            if (t < MasterArt.BannerStartMs) { banner.enabled = false; BannerFrame = -1; }
            else
            {
                int f = MasterArt.FrameAt(MasterArt.Get("banner_install"), t - MasterArt.BannerStartMs);
                banner.enabled = true;
                banner.sprite = bannerFrames[f];
                BannerFrame = f;
            }
            UpdateCords();
        }

        /// <summary>
        /// 2026-11-11 오프스크린 검수 보정: 펼침 00–04 동안 천 끝은 중앙에서 바깥으로 이동하는데 NPC는 발 고정·손은 최종 핀 위치라 손과 천이 떨어져 보였다.
        /// 발을 미끄러뜨리지 않고, 손(설치 최고점 앵커)에서 그 프레임의 천 끝 핀까지 얇은 줄을 그려 "줄을 당겨 펼친다"로 연결한다. 05부터 끝=핀이라 줄은 사라진다.
        /// 런타임 보조 선(아트 원본 아님) — 아트 검수 대상.
        /// </summary>
        void UpdateCords()
        {
            bool on = BannerFrame >= 0 && BannerFrame < 5 && rigger != null && porter != null;
            CordVisible = on;
            if (cordL == null || cordR == null) return;
            cordL.enabled = cordR.enabled = on;
            if (!on) return;
            Vector3 pins = MasterArt.BannerPinsPx[BannerFrame];
            Vector2 top = banner.transform.position;
            Vector2 endL = top + MasterArt.BannerLocal(new Vector2(pins.x, pins.z)) * BannerScale;
            Vector2 endR = top + MasterArt.BannerLocal(new Vector2(pins.y, pins.z)) * BannerScale;
            Vector2 handL = (Vector2)rigger.transform.position + MasterArt.NpcAnchorLocal(MasterArt.RiggerGripPx, rigger.flipX) * npcScale;
            Vector2 handR = (Vector2)porter.transform.position + MasterArt.NpcAnchorLocal(MasterArt.PorterGripPx, porter.flipX) * npcScale;
            PlaceCord(cordL, handL, endL);
            PlaceCord(cordR, handR, endR);
            CordEndLeft = endL; CordEndRight = endR; CordHandLeft = handL; CordHandRight = handR;
        }

        static void PlaceCord(SpriteRenderer cord, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            cord.transform.position = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            cord.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            cord.transform.localScale = new Vector3(Mathf.Max(0.01f, d.magnitude), CordThickness, 1f);
        }

        /// <summary>줄 두께(월드) = 원본 2px @PPU128 × 현수막 배율.</summary>
        public const float CordThickness = 2f / 128f * 1.6f;
        SpriteRenderer cordL, cordR;
        public bool CordVisible { get; private set; }
        public Vector2 CordEndLeft { get; private set; }
        public Vector2 CordEndRight { get; private set; }
        public Vector2 CordHandLeft { get; private set; }
        public Vector2 CordHandRight { get; private set; }

        void SetNpc(SpriteRenderer sr, Vector2 pos, Sprite[] frames, string clipName, float clipMs, bool flip)
        {
            if (sr == null) return;
            sr.transform.position = new Vector3(pos.x, pos.y, 0f);
            sr.flipX = flip;
            sr.sprite = frames[MasterArt.FrameAt(MasterArt.Get(clipName), clipMs)];
            if (sr == rigger) RiggerClip = clipName; else PorterClip = clipName;
        }

        void StepFallback(float t)
        {
            float walkIn = Mathf.Clamp01(t / MasterArt.WalkInEndMs);
            float unroll = Mathf.Clamp01((t - MasterArt.BannerStartMs) / (MasterArt.BannerEndMs - MasterArt.BannerStartMs));
            float walkOut = Mathf.Clamp01((t - MasterArt.InstallEndMs) / (ContractMs - MasterArt.InstallEndMs));
            if (rigger != null) rigger.transform.position = Vector2.Lerp(Vector2.Lerp(riggerOff, riggerStand, walkIn), riggerOff, walkOut);
            if (porter != null) porter.transform.position = Vector2.Lerp(Vector2.Lerp(porterOff, porterStand, walkIn), porterOff, walkOut);
            banner.enabled = unroll > 0f;
            banner.transform.localScale = new Vector3(Mathf.Max(0.01f, 10f * unroll), 1.5f, 1f);
            BannerFrame = unroll > 0f ? Mathf.Min(7, (int)(unroll * 8f)) : -1;
        }

        public bool Settled { get; private set; }

        /// <summary>설치 완료: 현수막(마지막 프레임)과 글자는 박람회 동안 남고 NPC는 퇴장(제거).</summary>
        public void Settle()
        {
            Step(duration);
            Settled = true;
            if (rigger != null) Destroy(rigger.gameObject);
            if (porter != null) Destroy(porter.gameObject);
            rigger = porter = null;
        }

        public void Finish() { if (this != null && gameObject != null) Destroy(gameObject); }
    }
}
