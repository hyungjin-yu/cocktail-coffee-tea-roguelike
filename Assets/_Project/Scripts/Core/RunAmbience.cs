using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>런 장면 구성 단계 — 실제 진행(BossDirector가 등장시킨 보스 수·후반 스폰 배가 시각)에서만 정한다.</summary>
    public enum RunStage
    {
        /// <summary>첫 중간보스 전: 깨끗한 기본 장면(조명 후광·김 없음).</summary>
        Opening,
        /// <summary>첫 보스 등장 이후: 램프 따뜻한 후광 + 가끔 카운터 김(영업 중).</summary>
        Midservice,
        /// <summary>강화 보스 등장 이후 또는 후반 스폰 배가(Balance.LateSpawnStartSeconds) 이후: 벽·막이 깊어지고 호박색 후광 + 잦은 김(마감 러시).</summary>
        LateNight,
    }

    /// <summary>
    /// 2026-11-09 런 장면 변주(유일한 담당 — 다른 곳에서 배경 색을 따로 바꾸지 않는다). 새 아트 없이 이 런의 배경 인스턴스만 바꾼다:
    /// 기존 Lamp·Counter·Wall·ReadabilityVeil 렌더러의 instance color, 런 시작 때 한 번 만든 램프 후광(원 도형, 램프 수만큼),
    /// 기존 steam_jet 클립의 카운터 김(동시 상한), 화면 가장자리 진입 표시(풀 재사용).
    /// 단계 구성(시작/중반/후반)은 RunStage, 보스 상태(접근/보스전)는 RunPhaseBadge.Compute(실제 BossDirector·ActiveBoss)로 정한다.
    /// - 보스 접근(실제 등장 15초 전): 램프 붉은 맥동(효과 낮음이면 고정).
    /// - 보스 등장 순간: 램프·후광이 0.6초 어두워졌다 붉게 켜지는 전환 + 실제 등장 쪽 화면 가장자리에 붉은 진입 표시. 김은 멈춘다(보스 가독성).
    /// - 보스전: 붉은 후광·램프 고정, 바닥 막 알파 +0.06 이하(바닥 위험 장판 GroundZone·적 경고는 배경보다 위에 그려진다).
    /// - 보스 처치: 0.4초 따뜻한 밝힘 뒤 현재 단계 구성으로 복귀.
    /// - 웨이브(EnemySpawner.WaveSpawned): 실제로 나온 쪽 가장자리에 진입 표시(마리 수가 많으면 셋, 아니면 둘). 정예(EliteSpawned): 그 높이에 주황 표시 하나(2초 쿨다운).
    /// 모든 시간은 게임 시간(일시정지 중 정지), 카메라 흔들림 없음. 배경과 함께 파괴되므로 재시작 때 새로 시작한다.
    /// </summary>
    public class RunAmbience : MonoBehaviour
    {
        public const float VeilAlphaBoost = 0.06f, LateVeilAlpha = 0.04f, LateWallScale = 0.88f;
        public const float BossFlickerSeconds = 0.6f, ReliefSeconds = 0.4f;
        public const int MaxWisps = 4, CuePool = 8;
        public const float EliteCueCooldown = 2f, CueLife = 0.9f;
        static readonly Color LampWarn = new Color(1f, 0.62f, 0.5f);
        static readonly Color VeilBoss = new Color(0.32f, 0.06f, 0.06f);
        static readonly Color HaloWarm = new Color(1f, 0.78f, 0.45f, 0.16f);
        static readonly Color HaloLate = new Color(1f, 0.62f, 0.3f, 0.22f);
        static readonly Color HaloBoss = new Color(1f, 0.32f, 0.26f, 0.22f);

        GameManager game;
        Transform root;
        readonly List<SpriteRenderer> lamps = new List<SpriteRenderer>();
        readonly List<Color> lampBase = new List<Color>();
        readonly List<SpriteRenderer> halos = new List<SpriteRenderer>();
        // 2026-11-09 r2(실제 렌더 비교 후 보강): 카메라에 실제로 보이는 벽 띠의 카운터 뒤 조명 웅덩이 + 화면 좌우 끝 바닥 어둠(전투 중앙은 비움).
        readonly List<SpriteRenderer> counterGlows = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> vignette = new List<SpriteRenderer>();
        public const int VignetteSteps = 3;
        public const float VignetteStepWidth = 0.9f;
        readonly List<Transform> counters = new List<Transform>();
        readonly List<SpriteRenderer> walls = new List<SpriteRenderer>();
        readonly List<Color> wallBase = new List<Color>();
        SpriteRenderer veil;
        Color veilBase;
        readonly List<FadeVfx> wisps = new List<FadeVfx>();
        readonly List<EdgeCue> cues = new List<EdgeCue>();
        EnemySpawner spawner;
        Enemy lastBoss;
        bool bossWasActive;
        float flicker, relief, wispTimer, eliteReady;
        int wispCursor;
        bool dirty = true;

        public RunPhaseState State { get; private set; }
        public RunStage Stage { get; private set; }
        public int LampCount => lamps.Count;
        public int HaloCount => halos.Count;
        public int CounterCount => counters.Count;
        public bool HasVeil => veil != null;
        public Color LampColor(int i) => lamps[i] != null ? lamps[i].color : Color.clear;
        public Color VeilColor => veil != null ? veil.color : Color.clear;
        public Color LampBaseColor(int i) => lampBase[i];
        public Color VeilBaseColor => veilBase;
        public Color WallColor(int i) => walls[i].color;
        public Color WallBaseColor(int i) => wallBase[i];
        public int WallCount => walls.Count;
        public bool HaloVisible(int i) => halos[i].enabled;
        public Color HaloColor(int i) => halos[i].color;
        public int LiveWisps { get { wisps.RemoveAll(w => w == null); return wisps.Count; } }
        public int WispsSpawned { get; private set; }
        public int ActiveCues { get { int n = 0; foreach (var c in cues) if (c.Active) n++; return n; } }
        public int CuesShown { get; private set; }
        public int CueObjects => cues.Count;
        public bool BossTransitionActive => flicker > 0f;
        public int CounterGlowCount => counterGlows.Count;
        public bool CounterGlowVisible(int i) => counterGlows[i].enabled;
        public Color CounterGlowColor(int i) => counterGlows[i].color;
        /// <summary>화면 끝 바닥 어둠의 가장 바깥 칸 알파(0이면 꺼짐).</summary>
        public float VignetteAlpha => vignette.Count > 0 && vignette[0].enabled ? vignette[0].color.a : 0f;
        public int VignetteCount => vignette.Count;

        public static RunAmbience Attach(Transform background, GameManager game)
        {
            if (background == null) return null;
            var existing = background.GetComponent<RunAmbience>();
            if (existing != null) return existing; // 같은 배경에 두 번 붙이지 않는다(중복 연출 방지)
            var a = background.gameObject.AddComponent<RunAmbience>();
            a.game = game;
            a.root = background;
            foreach (var sr in background.GetComponentsInChildren<SpriteRenderer>(true))
            {
                switch (sr.gameObject.name)
                {
                    case "Lamp": a.lamps.Add(sr); a.lampBase.Add(sr.color); break;
                    case "Counter": a.counters.Add(sr.transform); break;
                    case "Wall": a.walls.Add(sr); a.wallBase.Add(sr.color); break;
                    case "ReadabilityVeil": a.veil = sr; a.veilBase = sr.color; break;
                }
            }
            // 램프마다 후광 하나(런 시작 때 한 번만, 이후 켜고 끄기만). 램프 뒤(z 더 멀리), 벽 영역 안(레인 위로 내려오지 않는 크기).
            foreach (var lamp in a.lamps)
            {
                var go = new GameObject("LampHalo");
                go.transform.SetParent(background, false);
                go.transform.position = lamp.transform.position + new Vector3(0f, 0f, 0.01f);
                go.transform.localScale = Vector3.one * 2.1f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.sortingOrder = SortOrders.BackgroundProp;
                sr.enabled = false;
                a.halos.Add(sr);
            }
            // 카운터 뒤 조명 웅덩이(카운터마다 하나, 런 시작 때 한 번). 벽 띠 안 — 카운터 바로 뒤, 레인으로 내려오지 않는다.
            foreach (var c in a.counters)
            {
                var csr = c.GetComponent<SpriteRenderer>();
                float w = csr != null && csr.sprite != null ? csr.bounds.size.x : 3f;
                float h = csr != null && csr.sprite != null ? csr.bounds.size.y : 1.5f;
                var go = new GameObject("CounterGlow");
                go.transform.SetParent(background, false);
                go.transform.position = c.position + new Vector3(0f, h * 0.35f, 0.01f);
                go.transform.localScale = new Vector3(w * 1.25f, h * 1.3f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.sortingOrder = SortOrders.BackgroundProp;
                sr.enabled = false;
                a.counterGlows.Add(sr);
            }
            // 화면 좌우 끝 바닥 어둠(좌우 각 3칸, 바깥일수록 진함). 배경 정렬이라 장판·적·탄보다 아래, 화면 가운데 전투 구역은 건드리지 않는다.
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < VignetteSteps; i++)
                {
                    var go = new GameObject("EdgeShade");
                    go.transform.SetParent(background, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteFactory.Square;
                    sr.sortingOrder = SortOrders.Background;
                    sr.enabled = false;
                    a.vignette.Add(sr);
                }
            return a;
        }

        void LateUpdate()
        {
            // 화면 끝 어둠은 카메라를 따라간다(배경 고정 좌표계 안에서 x만, 바닥 높이 전체).
            if (vignette.Count == 0 || !vignette[0].enabled) return;
            var cam = Camera.main;
            if (cam == null) return;
            float half = cam.orthographicSize * cam.aspect;
            float camX = cam.transform.position.x;
            float floorTop = walls.Count > 0 ? walls[0].bounds.min.y : Balance.DepthMax + 0.6f;
            float bottom = cam.transform.position.y - cam.orthographicSize - 0.5f;
            float h = Mathf.Max(1f, floorTop - bottom);
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < VignetteSteps; i++)
                {
                    var sr = vignette[side * VignetteSteps + i];
                    float sign = side == 0 ? -1f : 1f;
                    float x = camX + sign * (half - VignetteStepWidth * (i + 0.5f));
                    sr.transform.position = new Vector3(x, bottom + h * 0.5f, transform.position.z - 0.012f);
                    sr.transform.localScale = new Vector3(VignetteStepWidth, h, 1f);
                }
        }

        void Update()
        {
            if (game == null) return;
            if (spawner == null)
            {
                spawner = game.GetComponentInChildren<EnemySpawner>();
                if (spawner != null) { spawner.WaveSpawned += OnWave; spawner.EliteSpawned += OnElite; }
            }
            float dt = Time.deltaTime;
            bool playing = game.State == GameState.Playing;
            var state = playing ? RunPhaseBadge.Compute(game).state : RunPhaseState.Calm;
            var stage = StageFor(game);
            var boss = game.ActiveBoss != null && !game.ActiveBoss.IsDead ? game.ActiveBoss : null;
            // 파괴된 보스는 Unity에서 null과 같으므로 "보스가 있었는지"는 따로 기억한다.
            if (boss != null && (!bossWasActive || !ReferenceEquals(boss, lastBoss))) { OnBossArrived(boss); lastBoss = boss; bossWasActive = true; }
            else if (boss == null && bossWasActive) { relief = ReliefSeconds; bossWasActive = false; lastBoss = null; dirty = true; }
            if (state != State || stage != Stage) dirty = true;
            Stage = stage;
            if (dt > 0f)
            {
                if (flicker > 0f) { flicker = Mathf.Max(0f, flicker - dt); dirty = true; }
                if (relief > 0f) { relief = Mathf.Max(0f, relief - dt); dirty = true; }
                if (state == RunPhaseState.BossIncoming && !HitFeedback.LowEffects) dirty = true; // 맥동
                TickWisps(dt, state);
                foreach (var c in cues) c.Tick(dt);
            }
            if (dirty) Apply(state, Time.time);
        }

        /// <summary>실제 진행에서 단계: 등장한 보스 수(SpawnedCount) ≥ 3 또는 후반 스폰 배가 시각 이후 = 후반, ≥ 1 = 중반, 그 외 시작.</summary>
        public static RunStage StageFor(GameManager game)
        {
            if (game == null) return RunStage.Opening;
            int spawned = game.Director != null ? Mathf.Max(game.Director.SpawnedCount, game.Director.BossesSpawned) : 0;
            if (spawned >= 3 || game.RunTime >= Balance.LateSpawnStartSeconds) return RunStage.LateNight;
            if (spawned >= 1) return RunStage.Midservice;
            return RunStage.Opening;
        }

        /// <summary>단계 강제(테스트·검수 캡처용). 다음 Update에서 실제 단계로 다시 맞춰진다.</summary>
        public void ApplyStageForTests(RunStage stage, RunPhaseState state)
        {
            Stage = stage;
            Apply(state, Time.time);
        }

        /// <summary>상태·단계별 색과 후광 적용(테스트 직접 호출 가능). t = 게임 시간(맥동 위상).</summary>
        public void Apply(RunPhaseState state, float t)
        {
            dirty = false;
            State = state;
            float k;
            switch (state)
            {
                case RunPhaseState.BossIncoming:
                    k = HitFeedback.LowEffects ? 0.5f : 0.35f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f));
                    break;
                case RunPhaseState.BossFight: k = 0.6f; break;
                default: k = 0f; break;
            }
            // 보스 등장 전환: 0.6초 동안 램프·후광이 어두워졌다(깜빡 2회) 붉게. 효과 낮음이면 깜빡임 없이 바로 붉게.
            float dim = 1f;
            if (flicker > 0f && !HitFeedback.LowEffects)
            {
                float p = 1f - flicker / BossFlickerSeconds; // 0 → 1
                dim = p < 0.5f ? Mathf.Lerp(1f, 0.35f, Mathf.PingPong(p * 4f, 1f)) : Mathf.Lerp(0.35f, 1f, (p - 0.5f) * 2f);
            }
            float warm = relief > 0f ? relief / ReliefSeconds : 0f;
            for (int i = 0; i < lamps.Count; i++)
            {
                if (lamps[i] == null) continue;
                var c = Color.Lerp(lampBase[i], lampBase[i] * LampWarn, k);
                c = Color.Lerp(c, Color.Lerp(c, new Color(1f, 0.95f, 0.8f, c.a), 0.5f), warm);
                c.r *= dim; c.g *= dim; c.b *= dim;
                lamps[i].color = c;
            }

            Color halo; bool haloOn;
            if (state == RunPhaseState.BossFight) { halo = HaloBoss; haloOn = true; }
            else if (Stage == RunStage.LateNight) { halo = HaloLate; haloOn = true; }
            else if (Stage == RunStage.Midservice) { halo = HaloWarm; haloOn = true; }
            else { halo = HaloWarm; haloOn = false; }
            if (state == RunPhaseState.BossIncoming) { halo = Color.Lerp(halo, HaloBoss, k); haloOn = true; }
            if (warm > 0f) { halo = Color.Lerp(halo, new Color(1f, 0.9f, 0.6f, 0.26f), warm); haloOn = true; }
            halo.a *= dim;
            foreach (var h in halos) { h.enabled = haloOn; h.color = halo; }
            // 카운터 조명: 시작 없음 → 중반 따뜻함 → 후반 짙은 호박색 → 보스 붉음(후광과 같은 규칙, 조금 더 옅게).
            var glow = halo; glow.a *= 0.85f;
            foreach (var g in counterGlows) { g.enabled = haloOn; g.color = glow; }
            // 화면 끝 바닥 어둠: 시작 없음, 중반 옅게, 후반 진하게, 보스전은 붉은 기. 바깥 칸일수록 진함(최대 알파 0.26).
            float va = state == RunPhaseState.BossFight ? 0.24f : Stage == RunStage.LateNight ? 0.26f : Stage == RunStage.Midservice ? 0.12f : 0f;
            Color vc = state == RunPhaseState.BossFight ? new Color(0.22f, 0.02f, 0.03f) : new Color(0.03f, 0.02f, 0.06f);
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < VignetteSteps; i++)
                {
                    var sr = vignette[side * VignetteSteps + i];
                    float a = va * (1f - i / (float)VignetteSteps) * dim;
                    sr.enabled = a > 0.001f;
                    sr.color = new Color(vc.r, vc.g, vc.b, a);
                }

            bool late = Stage == RunStage.LateNight;
            for (int i = 0; i < walls.Count; i++)
            {
                var b = wallBase[i];
                walls[i].color = late ? new Color(b.r * LateWallScale, b.g * LateWallScale, b.b * LateWallScale, b.a) : b;
            }
            if (veil != null)
            {
                if (state == RunPhaseState.BossFight)
                {
                    var c = Color.Lerp(veilBase, VeilBoss, 0.35f);
                    c.a = veilBase.a + VeilAlphaBoost;
                    veil.color = c;
                }
                else if (late)
                {
                    var c = veilBase;
                    c.a = veilBase.a + LateVeilAlpha;
                    veil.color = c;
                }
                else veil.color = veilBase;
            }
        }

        void OnBossArrived(Enemy boss)
        {
            flicker = BossFlickerSeconds;
            dirty = true;
            foreach (var w in wisps) if (w != null) Destroy(w.gameObject); // 보스 등장: 배경 김을 걷어 시선을 보스로
            wisps.Clear();
            var cam = Camera.main;
            float camX = cam != null ? cam.transform.position.x : 0f;
            float side = boss.transform.position.x < camX ? -1f : 1f; // 실제 등장 위치 쪽
            ShowCue(side, boss.transform.position.y, new Color(1f, 0.35f, 0.3f, 0.85f), 1.35f, 1.5f);
        }

        void OnWave(bool left, bool right, int count)
        {
            // 실제로 나온 쪽만. 마리 수(실제 생성 수)가 많으면 셋, 아니면 둘. 효과 낮음이면 하나.
            int n = HitFeedback.LowEffects ? 1 : count >= 12 ? 3 : 2;
            for (int s = 0; s < 2; s++)
            {
                if ((s == 0 && !left) || (s == 1 && !right)) continue;
                for (int i = 0; i < n; i++)
                {
                    float y = Mathf.Lerp(Balance.DepthMin + 1f, Balance.DepthMax - 1f, n == 1 ? 0.5f : i / (float)(n - 1));
                    ShowCue(s == 0 ? -1f : 1f, y, new Color(1f, 0.92f, 0.7f, 0.7f), 1f, CueLife);
                }
            }
        }

        void OnElite(EnemyDef def, Vector2 pos)
        {
            if (Time.time < eliteReady) return;
            eliteReady = Time.time + EliteCueCooldown;
            var cam = Camera.main;
            float camX = cam != null ? cam.transform.position.x : 0f;
            ShowCue(pos.x < camX ? -1f : 1f, pos.y, new Color(1f, 0.6f, 0.25f, 0.85f), 1.25f, 1.2f);
        }

        /// <summary>시험용: 실제 이벤트 처리 경로를 직접 호출.</summary>
        public void OnWaveForTests(bool left, bool right, int count) => OnWave(left, right, count);
        public void OnEliteForTests(EnemyDef def, Vector2 pos) => OnElite(def, pos);

        /// <summary>화면 가장자리(실제 등장 쪽) 안쪽을 가리키는 "&lt;/&gt;" 표시. 풀(CuePool)에서 재사용 — 모자라면 가장 오래된 것을 다시 쓴다.</summary>
        void ShowCue(float side, float y, Color color, float scale, float life)
        {
            var cam = Camera.main;
            float half = cam != null ? cam.orthographicSize * cam.aspect : 10f;
            float camX = cam != null ? cam.transform.position.x : 0f;
            float x = camX + side * (half - 0.9f);
            EdgeCue cue = null;
            foreach (var c in cues) if (!c.Active) { cue = c; break; }
            if (cue == null && cues.Count < CuePool) { cue = new EdgeCue(transform); cues.Add(cue); }
            if (cue == null) { cue = cues[0]; foreach (var c in cues) if (c.Remaining < cue.Remaining) cue = c; }
            cue.Show(new Vector3(x, Mathf.Clamp(y, Balance.DepthMin, Balance.DepthMax), 0f), side, color, scale, life);
            CuesShown++;
        }

        void TickWisps(float dt, RunPhaseState state)
        {
            wisps.RemoveAll(w => w == null);
            if (HitFeedback.LowEffects || counters.Count == 0 || state == RunPhaseState.BossFight || Stage == RunStage.Opening) return;
            float interval = Stage == RunStage.LateNight ? 2f : 3.2f;
            int cap = Stage == RunStage.LateNight ? MaxWisps : MaxWisps / 2;
            wispTimer -= dt;
            if (wispTimer > 0f || wisps.Count >= cap) return;
            wispTimer = interval;
            // 화면 안 카운터 중에서 차례로(같은 자리 반복 방지). 화면 밖이면 만들지 않는다.
            var cam = Camera.main;
            float half = cam != null ? cam.orthographicSize * cam.aspect : 10f;
            float camX = cam != null ? cam.transform.position.x : 0f;
            for (int tries = 0; tries < counters.Count; tries++)
            {
                var c = counters[wispCursor++ % counters.Count];
                if (c == null || Mathf.Abs(c.position.x - camX) > half - 1f) continue;
                var sr = c.GetComponent<SpriteRenderer>();
                float top = sr != null && sr.sprite != null ? sr.bounds.max.y : c.position.y + 0.6f;
                var fx = ArtVfx.Band("steam_jet", new Vector2(c.position.x + (wispCursor % 2 == 0 ? -0.6f : 0.6f), top + 0.6f), 90f, 1.2f, 0.45f, 1.6f,
                    new Color(1f, 1f, 1f, 0.32f), true, SortOrders.BackgroundProp);
                if (fx == null) return;
                fx.transform.SetParent(root, true);
                wisps.Add(fx);
                WispsSpawned++;
                return;
            }
        }

        void OnDestroy()
        {
            if (spawner != null) { spawner.WaveSpawned -= OnWave; spawner.EliteSpawned -= OnElite; }
        }

        /// <summary>가장자리 표시 하나: 막대 두 개로 만든 화살촉(기존 사각 도형). 재사용.</summary>
        sealed class EdgeCue
        {
            readonly Transform tf;
            readonly SpriteRenderer a, b;
            float life, remaining;
            Color color;
            public bool Active => remaining > 0f;
            public float Remaining => remaining;

            public EdgeCue(Transform parent)
            {
                tf = new GameObject("EdgeCue").transform;
                tf.SetParent(parent, false);
                a = Bar(tf, 40f);
                b = Bar(tf, -40f);
                tf.gameObject.SetActive(false);
            }

            static SpriteRenderer Bar(Transform parent, float angle)
            {
                var go = new GameObject("Bar");
                go.transform.SetParent(parent, false);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                go.transform.localScale = new Vector3(0.62f, 0.13f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Square;
                sr.sortingOrder = SortOrders.AirVfx;
                return sr;
            }

            public void Show(Vector3 worldPos, float side, Color c, float scale, float lifeSeconds)
            {
                tf.position = worldPos;
                // side<0(왼쪽에서 들어옴): 오른쪽(안쪽)을 가리키는 ">" — 막대 끝이 안쪽에서 만나게 놓는다.
                float dir = side < 0f ? 1f : -1f;
                a.transform.localPosition = new Vector3(-dir * 0.22f, 0.2f, 0f);
                b.transform.localPosition = new Vector3(-dir * 0.22f, -0.2f, 0f);
                a.transform.localRotation = Quaternion.Euler(0f, 0f, -40f * dir);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, 40f * dir);
                tf.localScale = Vector3.one * scale;
                color = c;
                life = remaining = Mathf.Max(0.1f, lifeSeconds);
                tf.gameObject.SetActive(true);
                Tick(0f);
            }

            public void Tick(float dt)
            {
                if (remaining <= 0f) return;
                remaining = Mathf.Max(0f, remaining - dt);
                float k = remaining / life;
                var c = color;
                c.a *= Mathf.Clamp01(k * 1.6f); // 끝 쪽에서 사라짐
                a.color = b.color = c;
                if (remaining <= 0f) tf.gameObject.SetActive(false);
            }
        }
    }
}
