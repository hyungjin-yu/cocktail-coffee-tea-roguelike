using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 벨트스크롤(던파식) 스폰: 화면 좌우 바깥(57: 유한 전투 구역 안)에서 몹이 걸어들어온다.
    /// 평소엔 트리클 스폰, 주기적으로 양쪽에서 동시에 웨이브가 몰려와 긴장감을 준다.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        PlayerController player;
        GameManager game;
        Camera cam;
        float timer;
        float waveTimer = Balance.WaveIntervalSeconds;

        public void Init(GameManager game, PlayerController player)
        {
            this.game = game;
            this.player = player;
            cam = Camera.main;
        }

        void Update()
        {
            if (player == null || player.IsDead || game.State != GameState.Playing) return;

            float t = game.RunTime;
            if (game.Mode == RunMode.Endless) { TickEndless(t); return; } // 2026-11-10 별도 모드: 기존 트리클·14초 웨이브 대신

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = RegularInterval(t, game.ActiveBoss != null, SpawnMultiplier);
                SpawnOne(t);
            }

            // 보스전 중에는 웨이브를 쉬어서 난이도가 과하게 겹치지 않게 한다
            if (game.ActiveBoss == null)
            {
                waveTimer -= Time.deltaTime;
                if (waveTimer <= 0f)
                {
                    waveTimer = Balance.WaveIntervalSeconds;
                    SpawnWave(t);
                }
            }
        }

        /// <summary>시험/계측용 — 일반 스폰 주기로 실제 생성한 적 수(웨이브·소환형의 소환은 제외). 읽기 전용.</summary>
        public int RegularSpawns { get; private set; }
        /// <summary>시험/계측용 — 웨이브로 실제 생성한 적 수.</summary>
        public int WaveSpawns { get; private set; }

        float SpawnMultiplier => game.Challenge != null ? game.Challenge.SpawnMultiplier : 1f;
        /// <summary>영구 해금 상한에 시간 경과 증가를 더하되 보스·소환 포함 절대 160 이하.</summary>
        public int RegularCap => Balance.TimeAliveCap(game.RunTime,
            game.Challenge != null ? game.Challenge.RegularAliveCap : Balance.MaxAliveEnemies);
        /// <summary>보스가 없을 땐 다음 예정 보스 1자리를 남겨(159) 보스가 등장해도 전체가 160을 넘지 않게 한다.
        /// N0(상한 100)에서는 영향 없음.</summary>
        bool UnderCap
        {
            get
            {
                int alive = Enemy.Active.Count;
                int absolute = Balance.MaxAliveEnemiesAbsolute - (game.ActiveBoss == null ? 1 : 0);
                return alive < RegularCap && alive < absolute;
            }
        }

        /// <summary>기존 시간 간격(보스전 ×2)을 영구 배율과 추가 시간 배율로 나눈다.</summary>
        public static float RegularInterval(float runTime, bool bossActive, float spawnMultiplier)
        {
            float interval = Mathf.Max(Balance.SpawnIntervalMin,
                Balance.SpawnIntervalStart - runTime * Balance.SpawnIntervalDecayPerSecond);
            if (bossActive) interval *= 2f; // 보스전 중에는 잡몹 스폰을 절반으로
            return interval / (Mathf.Max(1f, spawnMultiplier) * Balance.TimeSpawnMultiplier(runTime));
        }

        /// <summary>테스트용 — 실제 일반 스폰/웨이브 경로를 한 번 실행.</summary>
        public void SpawnOneForTests() => SpawnOne(game.RunTime);
        public void SpawnWaveForTests() => SpawnWave(game.RunTime);

        void SpawnOne(float t)
        {
            if (!UnderCap) return;
            var rng = game.SpawnRng; // 06:98 — 스폰은 시드에서 파생된 독립 스트림을 쓴다
            float side = rng.Value() < 0.5f ? -1f : 1f;
            var def = EnemyRoster.PickCommon(t, rng, game.Job); // 47 직업 테마
            var pos = BeltScroll.ArenaEdgePosition(cam, side, Balance.SpawnEdgeMargin, def.radius, out _); // 57 유한 구역 안 화면 밖
            var enemy = Enemy.SpawnAt(pos, def, Balance.EnemyHpScale(t), player, transform);
            EnemyRoster.AttachBehavior(enemy, game, player);
            RegularSpawns++;
            if (def.archetype == EnemyArchetype.EliteBarrage) EliteSpawned?.Invoke(def, pos);
        }

        /// <summary>2026-11-09 장면 변주 알림(읽기 전용 통지 — 스폰 규칙·수·난수 소비 불변):
        /// 실제로 웨이브가 나온 쪽(왼쪽/오른쪽)과 마리 수, 실제로 나온 정예와 그 위치.</summary>
        public event System.Action<bool, bool, int> WaveSpawned;
        public event System.Action<EnemyDef, Vector2> EliteSpawned;

        /// <summary>14초마다 양쪽 웨이브. 한쪽 4마리에서 3분마다 한 마리씩 늘며 영구 해금 수는 웨이브 수에 중복 적용하지 않는다.
        /// 57 P0-1: 벽에 붙어 한쪽 화면 밖이 구역 밖이면 그쪽 몫은 반대쪽 화면 밖에서 나오고, 안내 문구도 실제 쪽(왼쪽/오른쪽/양쪽)으로 말한다.</summary>
        void SpawnWave(float t)
        {
            float hpScale = Balance.EnemyHpScale(t);
            var rng = game.SpawnRng;
            bool fromLeft = false, fromRight = false;
            int spawned = 0;

            for (int i = 0; i < Balance.TimeWaveCountPerSide(t); i++)
            {
                for (int s = 0; s < 2; s++)
                {
                    if (!UnderCap) break;
                    var def = EnemyRoster.PickCommon(t, rng, game.Job);
                    var pos = BeltScroll.ArenaEdgePosition(cam, s == 0 ? -1f : 1f, Balance.SpawnEdgeMargin, def.radius, out float used);
                    var enemy = Enemy.SpawnAt(pos, def, hpScale, player, transform);
                    EnemyRoster.AttachBehavior(enemy, game, player);
                    WaveSpawns++;
                    spawned++;
                    if (used < 0f) fromLeft = true; else fromRight = true;
                    if (def.archetype == EnemyArchetype.EliteBarrage) EliteSpawned?.Invoke(def, pos);
                }
                if (!UnderCap) break;
            }
            LastWaveSides = BattleArena.SideLabel(fromLeft, fromRight);
            if (fromLeft || fromRight) game.Hud.ShowBanner("몹 웨이브!", $"{LastWaveSides}에서 몰려온다", 1.6f);
            if (spawned > 0) WaveSpawned?.Invoke(fromLeft, fromRight, spawned);
        }

        /// <summary>시험/보고용 — 마지막 웨이브가 실제로 나온 쪽(왼쪽/오른쪽/양쪽).</summary>
        public string LastWaveSides { get; private set; }

        // ───── 2026-11-10 끝없는 재료 모으기 ─────
        /// <summary>현재 웨이브(시작 전 0). 웨이브 길이·등장 수·배율은 EndlessRules.</summary>
        public int EndlessWave { get; private set; }
        /// <summary>이번 웨이브 구간에 지금까지 풀어 놓은 수(구간 분산 배치 — floor(총수 × 경과/30초)).</summary>
        public int EndlessReleased { get; private set; }
        /// <summary>생성 대기(압축 정수 묶음). 동시 개체 상한·프레임 예산 때문에 못 나온 적은 사라지지 않고 이월된다(상한 MaxPending).</summary>
        public int EndlessPending { get; private set; }
        /// <summary>이 모드에서 실제로 생성한 적 수(전체·계열별).</summary>
        public int EndlessSpawned { get; private set; }
        readonly int[] endlessByFamily = new int[4];
        public int EndlessSpawnedOf(MonsterFamily f) => endlessByFamily[(int)f];
        /// <summary>웨이브 시작 알림(웨이브 번호). HUD 안내용 읽기 전용 통지.</summary>
        public event System.Action<int> EndlessWaveStarted;

        /// <summary>테스트용 — 실제 끝없는 모드 틱을 한 번 실행(Time.deltaTime 무관, 주어진 생존 시간 기준).</summary>
        public void TickEndlessForTests(float runTime) => TickEndless(runTime);

        void TickEndless(float t)
        {
            int target = EndlessRules.WaveAt(t);
            if (EndlessWave == 0) StartEndlessWave(1);
            // 한 프레임에 여러 웨이브를 건너뛰어도(매우 큰 dt) 앞 웨이브 잔여분을 대기로 이월한다. 반복 상한으로 멈춤 방지.
            for (int guard = 0; EndlessWave < target && guard < 64; guard++)
            {
                AddPending(EndlessRules.WaveCount(EndlessWave) - EndlessReleased);
                StartEndlessWave(EndlessWave + 1);
            }
            if (EndlessWave < target) StartEndlessWave(target);

            int count = EndlessRules.WaveCount(EndlessWave);
            double into = System.Math.Max(0d, t - (EndlessWave - 1) * (double)EndlessRules.WaveSeconds);
            int due = (int)System.Math.Min(count, System.Math.Floor(count * into / EndlessRules.WaveSeconds));
            if (due > EndlessReleased) { AddPending(due - EndlessReleased); EndlessReleased = due; }

            for (int i = 0; i < EndlessRules.SpawnsPerFrame && EndlessPending > 0 && UnderCap; i++)
            {
                SpawnEndlessOne(t);
                EndlessPending--;
            }
        }

        void AddPending(int n)
        {
            if (n <= 0) return;
            EndlessPending = (int)System.Math.Min(EndlessRules.MaxPending, (long)EndlessPending + n);
        }

        void StartEndlessWave(int wave)
        {
            EndlessWave = wave;
            EndlessReleased = 0;
            var focus = EndlessRules.FocusFor(wave);
            string sub = $"집중 {MonsterFamilies.Name(focus)} 계열 {EndlessRules.FocusWeightPercent}% · 체력 {EndlessRules.HpPercent(wave)}% · 공격 {EndlessRules.AttackPercent(wave)}%";
            if (EndlessRules.IsFocusPreviewWave(wave))
                sub = $"다음 웨이브부터 집중 계열: {MonsterFamilies.Name(EndlessRules.FocusFor(wave + 1))} · " + sub;
            if (wave == 1 && DrinkAffinity.GuideLine(player != null ? player.DrunkDrink : null) is string guide) sub += " · 상성 " + guide;
            game.Hud?.ShowBanner($"웨이브 {wave}", sub, wave == 1 ? 3f : 1.8f);
            EndlessWaveStarted?.Invoke(wave);
        }

        void SpawnEndlessOne(float t)
        {
            var rng = game.SpawnRng;
            float side = rng.Value() < 0.5f ? -1f : 1f;
            var def = EnemyRoster.PickEndless(EndlessWave, t, rng, game.Job, out var family);
            var pos = BeltScroll.ArenaEdgePosition(cam, side, Balance.SpawnEdgeMargin, def.radius, out _);
            var enemy = Enemy.SpawnAt(pos, def, EndlessRules.HpScale(EndlessWave), player, transform, EndlessRules.AttackScale(EndlessWave));
            EnemyRoster.AttachBehavior(enemy, game, player);
            EndlessSpawned++;
            endlessByFamily[(int)family]++;
            if (def.archetype == EnemyArchetype.EliteBarrage) EliteSpawned?.Invoke(def, pos);
        }
    }
}
