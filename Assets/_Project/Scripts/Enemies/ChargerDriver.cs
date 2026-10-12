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
            if (enemy == null || enemy.IsDead || !enemy.HasArrived) return; // 06:24 — 등장 안내 1초 뒤부터 "첫 지연" 시작

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
                    // 06:116 최종판 변경 — 돌진 방향은 "예고 시작 시" 고정한다. 예고 끝에 재조준하지 않는다.
                    dashDirection = (lastPlayer - lastSelf).normalized;
                    ShowChargePath();
                    enemy.PlayAction("charge_prepare", TelegraphSeconds); // 04 §6 그라인더 예고 0.8초
                    break;
                case Phase.Telegraph:
                    enemy.BaseColor = enemy.NormalColor;
                    phase = Phase.Dash;
                    timer = DashSeconds;
                    enemy.PlayAction("charge", DashSeconds); // 04 §6 돌진 0.6초(반복 2장)
                    break;
                default:
                    phase = Phase.Chase;
                    timer = ChaseSeconds;
                    break;
            }
        }

        /// <summary>예고 동안 실제 돌진 경로를 표시한다 (06:37 "예고 동안 실제 돌진 경로 표시").</summary>
        void ShowChargePath()
        {
            float length = DashSpeed * DashSeconds;
            Vector2 mid = lastSelf + dashDirection * (length * 0.5f);
            float angle = Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg;
            FadeVfx.Spawn(SpriteFactory.Square, mid, angle,
                new Vector2(length, 0.08f), new Vector2(length, 0.3f),
                new Color(1f, 0.2f, 0.15f, 0.55f), TelegraphSeconds, SortOrders.GroundZone);
            // 예고 띠는 바닥 층(캐릭터 아래)에 그린다 — 불투명 경고선이 플레이어·보스 몸을 덮지 않게(2026-10-08 아트 검수 P1). 범위·시간 불변.
            ArtVfx.Band("warning_line", mid, angle, length, 0.5f, TelegraphSeconds, Color.white, false, SortOrders.GroundZone); // 04 §8 돌진 예고
        }
    }
}
