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
            if (game.Mode == RunMode.Endless) { TickEndless(game.RunTimePrecise); return; } // 2026-11-10 별도 모드: 기존 트리클·14초 웨이브 대신

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
            var def = game.PickStandardEnemy(t, out var reward); // 47 직업 테마(2026-11-10 계열 표 선택 — 기본은 기존 그대로)
            var pos = BeltScroll.ArenaEdgePosition(cam, side, Balance.SpawnEdgeMargin, def.radius, out _); // 57 유한 구역 안 화면 밖
            var enemy = Enemy.SpawnAt(pos, def, Balance.EnemyHpScale(t), player, transform);
            if (reward != def) enemy.RewardDef = reward;
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
                    var def = game.PickStandardEnemy(t, out var reward);
                    var pos = BeltScroll.ArenaEdgePosition(cam, s == 0 ? -1f : 1f, Balance.SpawnEdgeMargin, def.radius, out float used);
                    var enemy = Enemy.SpawnAt(pos, def, hpScale, player, transform);
                    if (reward != def) enemy.RewardDef = reward;
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

        // ───── 2026-11-10 끝없는 재료 모으기(개정: 웨이브·대기 상한 없음, long 부채 장부) ─────
        /// <summary>현재 웨이브(시작 전 0). 웨이브 길이·등장 수·배율은 EndlessRules.</summary>
        public long EndlessWave { get; private set; }
        /// <summary>이번 웨이브 구간에 지금까지 풀어 놓은 수(구간 분산 배치 — floor(총수 × 경과/30초)).</summary>
        public long EndlessReleased { get; private set; }
        /// <summary>생성 대기 = 압축된 long 부채 장부. 동시 개체 상한·프레임 예산 때문에 아직 못 나온 적의 수이며 버리지 않는다.
        /// long 한계(9.2×10^18)에서만 포화하고 그때 EndlessPendingSaturated가 켜진다(명시).</summary>
        public long EndlessPending { get; private set; }
        public bool EndlessPendingSaturated { get; private set; }
        /// <summary>이 모드에서 지금까지 예정된 총 등장 수(풀어 놓은 양의 누계, long 포화). = 생성 + 대기.</summary>
        public long EndlessScheduledTotal { get; private set; }
        /// <summary>이 모드에서 실제로 생성한 적 수(전체·계열별).</summary>
        public long EndlessSpawned { get; private set; }
        readonly long[] endlessByFamily = new long[4];
        public long EndlessSpawnedOf(MonsterFamily f) => endlessByFamily[(int)f];
        /// <summary>웨이브 시작 알림(웨이브 번호). HUD 안내용 읽기 전용 통지.</summary>
        public event System.Action<long> EndlessWaveStarted;
        /// <summary>테스트용: 웨이브 시작 배너를 끈다(초장시간 시험의 문자열 생성 비용 제거). 기본 꺼짐 아님.</summary>
        public bool EndlessBannersEnabled { get; set; } = true;

        /// <summary>테스트용 — 실제 끝없는 모드 틱을 한 번 실행(Time.deltaTime 무관, 주어진 생존 시간 기준).</summary>
        public void TickEndlessForTests(double runTime) => TickEndless(runTime);

        void TickEndless(double t)
        {
            long target = EndlessRules.WaveAt(t);
            if (EndlessWave == 0) StartEndlessWave(1, true);
            if (EndlessWave < target)
            {
                // 지난 웨이브의 남은 몫 + 건너뛴 웨이브 전부를 닫힌 식으로 한 번에 대기 장부로 옮긴다(큰 dt·장시간에도 소실·반복 없음).
                AddPending(EndlessRules.WaveCount(EndlessWave) - EndlessReleased);
                AddPending(EndlessRules.SumWaveCounts(EndlessWave + 1, target - 1));
                StartEndlessWave(target, true);
            }

            long count = EndlessRules.WaveCount(EndlessWave);
            double into = System.Math.Max(0d, t - (EndlessWave - 1) * (double)EndlessRules.WaveSeconds);
            long due = System.Math.Min(count, EndlessRules.ToLongSaturated(count * System.Math.Min(1d, into / EndlessRules.WaveSeconds)));
            if (due > EndlessReleased) { AddPending(due - EndlessReleased); EndlessReleased = due; }

            for (int i = 0; i < EndlessRules.SpawnsPerFrame && EndlessPending > 0 && UnderCap; i++)
            {
                SpawnEndlessOne(t);
                if (!EndlessPendingSaturated) EndlessPending--; // 포화 상태에서는 장부 값 자체를 신뢰할 수 없으므로 감소하지 않고 포화 유지
            }
        }

        void AddPending(long n)
        {
            if (n <= 0) return;
            if (EndlessPending > EndlessRules.SaturatedLong - n) { EndlessPending = EndlessRules.SaturatedLong; EndlessPendingSaturated = true; }
            else EndlessPending += n;
            EndlessScheduledTotal = EndlessScheduledTotal > EndlessRules.SaturatedLong - n ? EndlessRules.SaturatedLong : EndlessScheduledTotal + n;
        }

        void StartEndlessWave(long wave, bool announce)
        {
            EndlessWave = wave;
            EndlessReleased = 0;
            if (announce && EndlessBannersEnabled)
            {
                var focus = EndlessRules.FocusFor(wave);
                string sub = $"집중 {MonsterFamilies.Mark(focus)} {MonsterFamilies.Name(focus)} 계열 {EndlessRules.FocusWeightPercent}% · 체력 {EndlessRules.Show(EndlessRules.HpPercentInt(wave))}% · 공격 {EndlessRules.Show(EndlessRules.AttackPercentInt(wave))}%";
                if (EndlessRules.IsFocusPreviewWave(wave))
                {
                    var next = EndlessRules.FocusFor(wave + 1);
                    sub = $"다음 웨이브부터 집중 {MonsterFamilies.Mark(next)} {MonsterFamilies.Name(next)} · " + sub;
                }
                if (wave == 1 && DrinkAffinity.GuideLine(player != null ? player.DrunkDrink : null) is string guide) sub += " · 상성 " + guide;
                game.Hud?.ShowBanner($"웨이브 {EndlessRules.Show(wave)}", sub, wave == 1 ? 3f : 1.8f);
            }
            EndlessWaveStarted?.Invoke(wave);
        }

        void SpawnEndlessOne(double t)
        {
            var rng = game.SpawnRng;
            float side = rng.Value() < 0.5f ? -1f : 1f;
            var def = EnemyRoster.PickEndless(EndlessWave, (float)System.Math.Min(t, float.MaxValue), rng, game.Job, out var family);
            var pos = BeltScroll.ArenaEdgePosition(cam, side, Balance.SpawnEdgeMargin, def.radius, out _);
            var enemy = Enemy.SpawnAt(pos, def, EndlessRules.HpScale(EndlessWave), player, transform, EndlessRules.AttackScale(EndlessWave));
            EnemyRoster.AttachBehavior(enemy, game, player);
            if (EndlessSpawned < EndlessRules.SaturatedLong) EndlessSpawned++;
            if (endlessByFamily[(int)family] < EndlessRules.SaturatedLong) endlessByFamily[(int)family]++;
            if (def.archetype == EnemyArchetype.EliteBarrage) EliteSpawned?.Invoke(def, pos);
        }
    }
}
