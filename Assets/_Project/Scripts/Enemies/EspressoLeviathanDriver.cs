using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 강화 보스(3번째 중간보스): 에스프레소 머신 리바이어던 ([[02_몹 디자인 - 커피 & 중간보스]] 참고).
    /// 그라인더 골렘의 "직선 위협"(증기 분사)과 드립 서버 스펙터의 "장판"(드립 폭우) 패턴을 압축 계승하고,
    /// 신규 패턴 "압력 게이지 폭발"을 더한다. 압력 게이지는 시간이 지나면 자동으로 차고, 플레이어가
    /// 보스에게 주는 피해로 늦출 수 있다(딜레이스). HP 60%/30% 이하에서 축적 속도가 빨라진다.
    /// </summary>
    public class EspressoLeviathanDriver : MonoBehaviour
    {
        const float FirstAttackDelay = 3f;
        const float AttackCooldown = 6.5f;

        const float SteamTelegraphSeconds = 0.8f;
        const float SteamWidth = 1.4f;
        const float SteamDamage = 24f;
        const float SteamRange = 12f;

        const int DripRainZoneCount = 5;
        const float DripRainRadius = 1.4f;
        const float DripRainDamagePerSecond = 9f;
        const float DripRainZoneSeconds = 2.5f;
        const float DripRainSpread = 4f;

        const float PressureMax = 100f;
        const float PressureFillPerSecond = 6f;
        const float PressureFillPerSecondPhase60 = 9f;
        const float PressureFillPerSecondPhase30 = 13f;
        /// <summary>플레이어가 보스에게 준 피해 1당 압력 게이지가 이만큼 줄어든다 (딜레이스 체크).</summary>
        const float PressureReliefPerDamage = 0.15f;
        const float PressureExplosionRadius = 3.5f;
        const float PressureExplosionDamage = 20f;

        Enemy enemy;
        GameManager game;
        float attackTimer = FirstAttackDelay;
        int patternIndex;
        float pressure;

        public void Init(Enemy enemy, GameManager game)
        {
            this.enemy = enemy;
            this.game = game;
            enemy.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (enemy != null) enemy.Damaged -= OnDamaged;
        }

        void OnDamaged(float amount) => pressure = Mathf.Max(0f, pressure - amount * PressureReliefPerDamage);

        /// <summary>테스트용 — 현재 압력 게이지(0~PressureMax).</summary>
        public float Pressure => pressure;

        void Update()
        {
            if (enemy == null || enemy.IsDead || game == null || game.State != GameState.Playing) return;
            if (!enemy.HasArrived) return; // 06:24 — 등장 안내 1초 뒤부터 "첫 지연" 시작

            TickPressure();
            TickAttackPattern();
        }

        float FillRate
        {
            get
            {
                float hpFrac = enemy.MaxHp > 0f ? enemy.Hp / enemy.MaxHp : 0f;
                if (hpFrac <= 0.3f) return PressureFillPerSecondPhase30;
                if (hpFrac <= 0.6f) return PressureFillPerSecondPhase60;
                return PressureFillPerSecond;
            }
        }

        void TickPressure()
        {
            pressure += FillRate * Time.deltaTime;
            if (pressure < PressureMax) return;

            pressure = 0f;
            Explode();
        }

        void Explode()
        {
            game.Hud.ShowBanner("압력 게이지 폭발!", "밀려남 + 광역 피해!", 2f);
            FadeVfx.Spawn(SpriteFactory.Circle, enemy.transform.position, 0f,
                Vector2.one * 0.6f, Vector2.one * (PressureExplosionRadius * 2.2f),
                new Color(0.9f, 0.6f, 0.2f, 0.6f), 0.4f, 5);

            if (game.Player == null || game.Player.IsDead) return;

            float dist = Vector2.Distance(enemy.transform.position, game.Player.Position);
            if (dist > PressureExplosionRadius) return;

            game.Player.TakeDamage(PressureExplosionDamage);

            Vector2 away = game.Player.Position - (Vector2)enemy.transform.position;
            Vector2 dir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
            game.Player.ApplyKnockback(dir * Balance.PressureExplosionKnockbackSpeed, Balance.PressureKnockbackDuration);
        }

        void TickAttackPattern()
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            attackTimer = AttackCooldown;
            if (patternIndex++ % 2 == 0) StartCoroutine(SteamBeam());
            else DripRain();
        }

        IEnumerator SteamBeam()
        {
            if (game.Player == null || game.Player.IsDead) yield break;

            Vector2 origin = enemy.transform.position;
            Vector2 dir = game.Player.Position.x >= origin.x ? Vector2.right : Vector2.left;
            Vector2 mid = origin + dir * (SteamRange * 0.5f);
            float angle = dir.x >= 0f ? 0f : 180f;

            FadeVfx.Spawn(SpriteFactory.Square, mid, angle,
                new Vector2(SteamRange, 0.15f), new Vector2(SteamRange, SteamWidth),
                new Color(1f, 0.3f, 0.2f, 0.55f), SteamTelegraphSeconds, 4);
            game.Hud.ShowBanner("증기 분사", "조준선 방향을 피하세요!", SteamTelegraphSeconds);

            yield return new WaitForSeconds(SteamTelegraphSeconds);

            if (enemy == null || enemy.IsDead || game.Player == null || game.Player.IsDead) yield break;

            // 조준선은 텔레그래프가 시작될 때 고정한 방향으로 발사 (재조준 없음 — 옆으로 피하면 안전)
            Vector2 endPoint = origin + dir * SteamRange;
            if (DistancePointToSegment(game.Player.Position, origin, endPoint) <= SteamWidth * 0.5f)
                game.Player.TakeDamage(SteamDamage);

            FadeVfx.Spawn(SpriteFactory.Square, mid, angle,
                new Vector2(SteamRange, SteamWidth), new Vector2(SteamRange, SteamWidth * 0.3f),
                new Color(1f, 0.85f, 0.6f, 0.7f), 0.25f, 4);
        }

        void DripRain()
        {
            game.Hud.ShowBanner("드립 폭우", "안전지대를 찾아 움직이세요!", 2f);
            if (game.Player == null) return;

            for (int i = 0; i < DripRainZoneCount; i++)
            {
                Vector2 pos = game.Player.Position + Random.insideUnitCircle * DripRainSpread;
                pos.y = Mathf.Clamp(pos.y, Balance.DepthMin, Balance.DepthMax);
                BurstZone.Spawn(pos, DripRainRadius, DripRainDamagePerSecond, DripRainZoneSeconds,
                    game.Player, enemy.transform.parent);
            }
        }

        static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            Vector2 closest = a + ab * t;
            return Vector2.Distance(p, closest);
        }
    }
}
