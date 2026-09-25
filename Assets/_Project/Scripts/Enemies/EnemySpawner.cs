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
                timer = Mathf.Max(Balance.SpawnIntervalMin,
                    Balance.SpawnIntervalStart - t * Balance.SpawnIntervalDecayPerSecond);
                if (game.ActiveBoss != null) timer *= 2f; // 보스전 중에는 잡몹 스폰을 절반으로
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

        void SpawnOne(float t)
        {
            if (Enemy.Active.Count >= Balance.MaxAliveEnemies) return;
            var rng = game.SpawnRng; // 06:98 — 스폰은 시드에서 파생된 독립 스트림을 쓴다
            float side = rng.Value() < 0.5f ? -1f : 1f;
            var pos = BeltScroll.EdgePosition(cam, side, Balance.SpawnEdgeMargin);
            var enemy = Enemy.SpawnAt(pos, EnemyRoster.PickCommon(t, rng), Balance.EnemyHpScale(t), player, transform);
            EnemyRoster.AttachBehavior(enemy, game, player);
        }

        /// <summary>양쪽에서 동시에 몰려오는 웨이브 (기획서의 "몰려오는 몹" 긴장감).</summary>
        void SpawnWave(float t)
        {
            game.Hud.ShowBanner("몹 웨이브!", "양쪽에서 몰려온다", 1.6f);
            float hpScale = Balance.EnemyHpScale(t);
            var rng = game.SpawnRng;

            for (int i = 0; i < Balance.WaveCountPerSide; i++)
            {
                if (Enemy.Active.Count >= Balance.MaxAliveEnemies) break;
                var left = Enemy.SpawnAt(BeltScroll.EdgePosition(cam, -1f, Balance.SpawnEdgeMargin),
                    EnemyRoster.PickCommon(t, rng), hpScale, player, transform);
                EnemyRoster.AttachBehavior(left, game, player);

                if (Enemy.Active.Count >= Balance.MaxAliveEnemies) break;
                var right = Enemy.SpawnAt(BeltScroll.EdgePosition(cam, 1f, Balance.SpawnEdgeMargin),
                    EnemyRoster.PickCommon(t, rng), hpScale, player, transform);
                EnemyRoster.AttachBehavior(right, game, player);
            }
        }
    }
}
