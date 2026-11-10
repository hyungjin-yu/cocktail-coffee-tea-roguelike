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

        void OnWaveStarted(long wave)
        {
            released = 0f;
            tracked.Clear();
            if (show != null && MasterRules.SegmentOf(wave) == MasterSegment.Reward) { show.Finish(); show = null; }
            if (BannersEnabled && game != null && game.Hud != null)
            {
                var seg = MasterRules.SegmentOf(wave);
                string title = seg == MasterSegment.Fair ? $"박람회 {MasterRules.FairIndex(wave)}/{MasterRules.FairWaves}"
                    : seg == MasterSegment.Reward ? $"보상 손님 {MasterRules.RewardIndex(wave)}/{MasterRules.RewardWaves}"
                    : $"웨이브 {EndlessRules.Show(wave)} · 재료 모으기 {MasterRules.InCycle(wave)}/{MasterRules.CollectWaves}";
                game.Hud.ShowBanner(title, SubLine(wave), 2f);
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
            if (BannersEnabled && game?.Hud != null)
                game.Hud.ShowBanner("박람회 성공!", recorded ? $"모든 재료 기본 묶음 {items.Count}종 지급(판매 불가 재료) · 이제 보상 손님" : "보상은 이미 기록되었습니다", 3f);
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
            if (BannersEnabled) game.Hud?.ShowBanner("박람회 시작!", $"현수막 설치 완료 · 박람회 1/{MasterRules.FairWaves} · 약화 {State.ActiveReduction}%", 2f);
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
            if (BannersEnabled && Enemy.Active.Count > 0) game.Hud?.ShowBanner("박람회 준비", $"남은 손님 {Enemy.Active.Count}명을 먼저 처리하세요", 2.5f);
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
    /// NPC 2명이 맵 위쪽(벽 영역, 전투 레인 위)에 '박람회' 현수막을 거는 연출. 실제 시간으로 진행(전투 정지 중).
    /// **임시 소재**: 기존 바텐더·티 소믈리에 몸 아트와 단색 천 + 월드 글자. 아트 담당 NPC·현수막 납품·검수 후 교체 예정 — 최종 아트 아님.
    /// </summary>
    public class FairBannerShow : MonoBehaviour
    {
        Transform npcL, npcR, cloth;
        TextMesh label;
        float duration;
        const float BannerWorldY = 4.1f, NpcWorldY = 2.6f, BannerWidth = 10f, BannerHeight = 1.3f;

        public static FairBannerShow Create(GameManager game, float duration)
        {
            var cam = Camera.main;
            var root = new GameObject("FairBanner");
            if (cam != null) { root.transform.SetParent(cam.transform, false); root.transform.localPosition = new Vector3(0f, 0f, -cam.transform.position.z); }
            else root.transform.SetParent(game.transform, false);
            var s = root.AddComponent<FairBannerShow>();
            s.duration = Mathf.Max(0.1f, duration);
            float camY = cam != null ? cam.transform.position.y : 0f;
            s.cloth = MakeSprite("Cloth", root.transform, SpriteFactory.Square, new Color(0.62f, 0.12f, 0.16f), new Vector2(0f, BannerWorldY - camY), SortOrders.BackgroundProp);
            s.cloth.localScale = new Vector3(0.01f, BannerHeight, 1f);
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, BannerWorldY - camY, -0.01f);
            s.label = textGo.AddComponent<TextMesh>();
            s.label.text = "박람회";
            s.label.font = UiFactory.Font;
            s.label.fontSize = 96;
            s.label.characterSize = 0.08f;
            s.label.anchor = TextAnchor.MiddleCenter;
            s.label.color = new Color(1f, 0.93f, 0.75f, 0f);
            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null) { if (UiFactory.Font != null) mr.sharedMaterial = UiFactory.Font.material; mr.sortingOrder = SortOrders.BackgroundProp + 1; }
            s.npcL = MakeNpc("NpcLeft", root.transform, "bartender", new Vector2(-12f, NpcWorldY - camY), false);
            s.npcR = MakeNpc("NpcRight", root.transform, "tea_sommelier", new Vector2(12f, NpcWorldY - camY), true);
            return s;
        }

        static Transform MakeSprite(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go.transform;
        }

        static Transform MakeNpc(string name, Transform parent, string artId, Vector2 pos, bool flip)
        {
            var art = ArtLibrary.Get(artId);
            var sprite = art?.Clip("idle")?.frames != null && art.Clip("idle").frames.Length > 0 ? art.Clip("idle").frames[0] : SpriteFactory.Capsule;
            var t = MakeSprite(name, parent, sprite, art != null ? Color.white : new Color(0.9f, 0.8f, 0.6f), pos, SortOrders.BackgroundProp + 2);
            t.localScale = Vector3.one * (art != null ? art.BodyVisualScale : 0.8f);
            var sr = t.GetComponent<SpriteRenderer>();
            sr.flipX = flip;
            return t;
        }

        /// <summary>아트 계약 타이밍(기본 3.5초 = 접근 0.8 + 설치 1.5 + 퇴장 1.2, 설정 길이에 비례). 프레임 수가 아닌 Director의 실제 시간 상태 타이머로만 진행한다.</summary>
        public const float ApproachShare = 0.8f / 3.5f, InstallEndShare = 2.3f / 3.5f;

        public void Step(float elapsed)
        {
            float k = Mathf.Clamp01(elapsed / duration);
            float walkIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / ApproachShare));
            float unroll = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - ApproachShare) / (InstallEndShare - ApproachShare)));
            float walkOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - InstallEndShare) / (1f - InstallEndShare)));
            float edge = BannerWidth * 0.5f + 0.4f;
            if (npcL != null) npcL.localPosition = new Vector3(Mathf.Lerp(Mathf.Lerp(-12f, -edge, walkIn), -12f, walkOut), npcL.localPosition.y, 0f);
            if (npcR != null) npcR.localPosition = new Vector3(Mathf.Lerp(Mathf.Lerp(12f, edge, walkIn), 12f, walkOut), npcR.localPosition.y, 0f);
            if (npcL != null) npcL.GetComponent<SpriteRenderer>().flipX = walkOut > 0f; // 퇴장 때 돌아선다
            if (npcR != null) npcR.GetComponent<SpriteRenderer>().flipX = walkOut <= 0f;
            if (cloth != null) cloth.localScale = new Vector3(Mathf.Max(0.01f, BannerWidth * unroll), BannerHeight, 1f);
            if (label != null) { var c = label.color; c.a = Mathf.Clamp01((k - InstallEndShare) / 0.08f); label.color = c; }
        }

        public bool Settled { get; private set; }

        /// <summary>설치 완료: 현수막은 박람회 동안 남고 NPC는 퇴장(제거).</summary>
        public void Settle()
        {
            Step(duration);
            Settled = true;
            if (npcL != null) Destroy(npcL.gameObject);
            if (npcR != null) Destroy(npcR.gameObject);
        }

        public void Finish() { if (this != null && gameObject != null) Destroy(gameObject); }
    }
}
