using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 벨트스크롤(던파식) 스폰: 화면 좌우 바깥에서 몹이 걸어들어온다.
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
            var pos = BeltScroll.EdgePosition(cam, side, Balance.SpawnEdgeMargin);
            var enemy = Enemy.SpawnAt(pos, EnemyRoster.PickCommon(t, rng, game.Job), Balance.EnemyHpScale(t), player, transform); // 47 직업 테마
            EnemyRoster.AttachBehavior(enemy, game, player);
            RegularSpawns++;
        }

        /// <summary>14초마다 양쪽 웨이브. 한쪽 4마리에서 3분마다 한 마리씩 늘며 영구 해금 수는 웨이브 수에 중복 적용하지 않는다.</summary>
        void SpawnWave(float t)
        {
            game.Hud.ShowBanner("몹 웨이브!", "양쪽에서 몰려온다", 1.6f);
            float hpScale = Balance.EnemyHpScale(t);
            var rng = game.SpawnRng;

            for (int i = 0; i < Balance.TimeWaveCountPerSide(t); i++)
            {
                if (!UnderCap) break;
                var left = Enemy.SpawnAt(BeltScroll.EdgePosition(cam, -1f, Balance.SpawnEdgeMargin),
                    EnemyRoster.PickCommon(t, rng, game.Job), hpScale, player, transform);
                EnemyRoster.AttachBehavior(left, game, player);
                WaveSpawns++;

                if (!UnderCap) break;
                var right = Enemy.SpawnAt(BeltScroll.EdgePosition(cam, 1f, Balance.SpawnEdgeMargin),
                    EnemyRoster.PickCommon(t, rng, game.Job), hpScale, player, transform);
                EnemyRoster.AttachBehavior(right, game, player);
                WaveSpawns++;
            }
        }
    }
}
