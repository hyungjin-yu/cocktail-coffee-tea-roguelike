using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 최종보스 진상 손님 패턴 (기획서 10장 중 2개, 번갈아 사용):
    /// "환불해주세요!" — 플레이어의 공격/이동 강화 효과를 잠시 무효화
    /// "사장 나와!" — 잡몹 대량 소환
    /// 체력 50% 미만이면 쿨타임이 줄고 소환 수가 늘어난다.
    /// </summary>
    public class CustomerBossDriver : MonoBehaviour
    {
        const float FirstDelay = 3f;
        const float Cooldown = 7f;
        const float EnragedCooldown = 5f;
        const float RefundSeconds = 5f;
        const int SummonCount = 10;
        const int EnragedSummonCount = 16;
        const int MaxAliveForSummon = 160;

        Enemy enemy;
        GameManager game;
        float timer = FirstDelay;
        int patternIndex;

        bool Enraged => enemy.Hp < enemy.MaxHp * 0.5f;

        public void Init(Enemy enemy, GameManager game)
        {
            this.enemy = enemy;
            this.game = game;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || game == null || game.State != GameState.Playing) return;
            if (!enemy.HasArrived) return; // 06:24 — 등장 안내 1초 뒤부터 "첫 지연" 시작

            enemy.SetEnragedVisual(Enraged);

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = Enraged ? EnragedCooldown : Cooldown;
            if (patternIndex++ % 2 == 0) Refund();
            else Summon();
        }

        void Refund()
        {
            game.Hud.ShowBanner("\"환불해주세요!\"", $"공격·이동 강화 효과 {RefundSeconds:0}초간 무효!", 2.5f);
            game.Stats.SuppressBonuses(RefundSeconds);
            FadeVfx.Spawn(SpriteFactory.Circle, game.Player.Position, 0f,
                Vector2.one * 0.5f, Vector2.one * 6f, new Color(1f, 0.2f, 0.25f, 0.6f), 0.5f, 5);
        }

        void Summon()
        {
            game.Hud.ShowBanner("\"사장 나와!\"", "잡몹이 몰려온다!", 2.5f);

            int count = Enraged ? EnragedSummonCount : SummonCount;
            float hpScale = Balance.EnemyHpScale(game.RunTime);
            for (int i = 0; i < count; i++)
            {
                if (Enemy.Active.Count >= MaxAliveForSummon) break;

                // 벨트스크롤 시점: 보스 좌/우로 번갈아, 레인(깊이) 폭 안 랜덤 위치에 소환
                float side = (i % 2 == 0) ? -1f : 1f;
                float depth = Random.Range(Balance.DepthMin, Balance.DepthMax);
                float x = enemy.transform.position.x + side * (enemy.Def.radius + 2f + Random.Range(0f, 2f));
                var summoned = Enemy.SpawnAt(new Vector2(x, depth), EnemyRoster.PickCommon(game.RunTime), hpScale,
                    game.Player, game.transform);
                EnemyRoster.AttachBehavior(summoned, game, game.Player);
            }
        }
    }
}
