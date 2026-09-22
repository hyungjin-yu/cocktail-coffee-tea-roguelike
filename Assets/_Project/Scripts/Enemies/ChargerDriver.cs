using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 중간보스 패턴: 추적 → 멈추고 붉게 경고(텔레그래프) → 그 시점 플레이어 방향으로 돌진.
    /// 경고 중 방향이 정해지므로 옆으로 빠지면 피할 수 있다.
    /// </summary>
    public class ChargerDriver : MonoBehaviour, IEnemyDriver
    {
        const float ChaseSeconds = 3.5f;
        const float TelegraphSeconds = 0.8f;
        const float DashSeconds = 0.6f;
        const float DashSpeed = 9f;

        enum Phase { Chase, Telegraph, Dash }

        Enemy enemy;
        Phase phase = Phase.Chase;
        float timer = ChaseSeconds;
        Vector2 dashDirection;
        Vector2 lastSelf;
        Vector2 lastPlayer;

        public void Init(Enemy enemy)
        {
            this.enemy = enemy;
            enemy.SetDriver(this);
        }

        public bool TryGetVelocity(Vector2 selfPosition, Vector2 playerPosition, out Vector2 velocity)
        {
            lastSelf = selfPosition;
            lastPlayer = playerPosition;

            switch (phase)
            {
                case Phase.Telegraph:
                    velocity = Vector2.zero;
                    return true;
                case Phase.Dash:
                    velocity = dashDirection * DashSpeed;
                    return true;
                default:
                    velocity = Vector2.zero;
                    return false;
            }
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead) return;

            timer -= Time.deltaTime;

            if (phase == Phase.Telegraph)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                enemy.BaseColor = Color.Lerp(enemy.NormalColor, new Color(1f, 0.15f, 0.1f), pulse);
            }

            if (timer > 0f) return;

            switch (phase)
            {
                case Phase.Chase:
                    phase = Phase.Telegraph;
                    timer = TelegraphSeconds;
                    break;
                case Phase.Telegraph:
                    dashDirection = (lastPlayer - lastSelf).normalized;
                    enemy.BaseColor = enemy.NormalColor;
                    phase = Phase.Dash;
                    timer = DashSeconds;
                    break;
                default:
                    phase = Phase.Chase;
                    timer = ChaseSeconds;
                    break;
            }
        }
    }
}
