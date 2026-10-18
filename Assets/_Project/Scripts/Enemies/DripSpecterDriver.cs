using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 중간보스 B: 드립 서버 스펙터 패턴 ([[06_전투와 런 규칙 v1.0]] B절 참고, 번갈아 사용).
    /// "드립 웨이브" — 플레이어 주변에 뜨거운 커피 장판을 순서대로 떨어뜨려 안전지대를 찾아 움직이게 만든다.
    /// 이동은 정상 추적을 유지한다. "투명화" — 잠시 무적 상태로 사라졌다가(이동 정지) 플레이어 근처에
    /// 재등장하며 소형 장판을 하나 남긴다.
    /// </summary>
    public class DripSpecterDriver : MonoBehaviour, IEnemyDriver
    {
        const float FirstDelay = 2.5f;
        const float Cooldown = 6f;

        const int DripWaveCount = 4;
        const float DripWaveZoneRadius = 1.3f;
        const float DripWaveDamagePerSecond = 9f;
        const float DripWaveZoneSeconds = 2f;
        const float DripWaveStagger = 0.4f;
        const float DripWaveSpread = 1.5f;
        const float DripWaveTelegraphSeconds = 0.6f;

        const float InvisibleSeconds = 1.2f;
        const float TeleportDistance = 3.5f;
        const float ReappearZoneRadius = 1.1f;
        const float ReappearDamagePerSecond = 8f;
        const float ReappearZoneSeconds = 1.8f;

        Enemy enemy;
        GameManager game;
        float timer = FirstDelay;
        int patternIndex;
        /// <summary>투명화 중에만 true — 그동안 제자리에 멈춘다(06:123 "투명화 중 정지"). 웨이브 중엔 정상 추적.</summary>
        bool frozen;

        public void Init(Enemy enemy, GameManager game)
        {
            this.enemy = enemy;
            this.game = game;
            enemy.SetDriver(this);
        }

        public bool TryGetVelocity(Vector2 selfPosition, Vector2 playerPosition, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            return frozen;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || game == null || game.State != GameState.Playing) return;
            if (!enemy.HasArrived) return; // 06:24 — 등장 안내 1초 뒤부터 "첫 지연" 시작

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = Cooldown;
            if (patternIndex++ % 2 == 0) StartCoroutine(DripWave());
            else StartCoroutine(Invisibility());
        }

        IEnumerator DripWave()
        {
            game.Hud.ShowBanner("드립 웨이브", "바닥에 뜨거운 커피가 순서대로 떨어진다!", 2f);
            enemy.PlayAction("drip");

            for (int i = 0; i < DripWaveCount; i++)
            {
                if (enemy == null || enemy.IsDead || game.Player == null || game.Player.IsDead) yield break;

                // 06:121 — 점은 생성 시(예고 시작 시) 고정, 0.6초 경고 후 장판 생성.
                Vector2 pos = game.Player.Position + Random.insideUnitCircle * DripWaveSpread;
                pos.y = Mathf.Clamp(pos.y, Balance.DepthMin, Balance.DepthMax);
                StartCoroutine(DripWaveZone(pos));

                if (i < DripWaveCount - 1) yield return new WaitForSeconds(DripWaveStagger);
            }
        }

        IEnumerator DripWaveZone(Vector2 pos)
        {
            // 43: v2 예고(판정 원 테두리 + 예고 시간에 맞춰 차오르는 링). 아트가 없으면 기존 채움 원 + warning_ring.
            if (ZoneTelegraphV2.Spawn(pos, DripWaveZoneRadius, DripWaveTelegraphSeconds, new Color(1f, .62f, .3f), enemy.transform.parent) == null)
            {
                FadeVfx.Spawn(SpriteFactory.Circle, pos, 0f,
                    Vector2.one * 0.3f, Vector2.one * (DripWaveZoneRadius * 2f),
                    new Color(0.85f, 0.5f, 0.2f, 0.35f), DripWaveTelegraphSeconds);
                ArtVfx.GroundRing("warning_ring", pos, DripWaveZoneRadius, DripWaveTelegraphSeconds, Color.white);
            }

            yield return new WaitForSeconds(DripWaveTelegraphSeconds);

            if (enemy == null || enemy.IsDead || game.Player == null) yield break;
            BurstZone.Spawn(pos, DripWaveZoneRadius, DripWaveDamagePerSecond, DripWaveZoneSeconds,
                game.Player, enemy.transform.parent, (enemy.Def==EnemyDef.SteamKettleElder ? BurstZone.TeaPoolArt : BurstZone.CoffeePoolArt),
                DamageSource.Zone(enemy.Def, "드립 웨이브"));
        }

        IEnumerator Invisibility()
        {
            if (enemy == null || enemy.IsDead) yield break;

            game.Hud.ShowBanner("투명화", "스펙터가 잠시 사라진다...", 1.5f);
            enemy.Invulnerable = true;
            frozen = true;
            enemy.PlayAction("vanish");
            var normal = enemy.NormalColor;
            enemy.BaseColor = new Color(normal.r, normal.g, normal.b, 0.25f);

            // 06:122 — 재등장 지점은 투명화 "시작"에 확정·표시한다(끝날 때 정하면 미리 피할 수 없음,
            // 2026-09-25 G11 감사에서 발견한 스펙 위반 수정). 표시 위치는 투명화 내내 고정.
            bool hasTarget = game.Player != null && !game.Player.IsDead;
            Vector2 target = Vector2.zero;
            if (hasTarget)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                target = game.Player.Position + dir * TeleportDistance;
                target.y = Mathf.Clamp(target.y, Balance.DepthMin, Balance.DepthMax);
                target.x = BattleArena.ClampX(target.x, enemy.Def.radius); // 57 P0-1: 예고 위치=실제 재등장(벽 안)
                if (ZoneTelegraphV2.Spawn(target, ReappearZoneRadius, InvisibleSeconds, new Color(.8f, .7f, 1f), enemy.transform.parent) == null)
                {
                    FadeVfx.Spawn(SpriteFactory.Circle, target, 0f,
                        Vector2.one * (ReappearZoneRadius * 2f), Vector2.one * (ReappearZoneRadius * 2f),
                        new Color(0.6f, 0.45f, 0.75f, 0.45f), InvisibleSeconds);
                    ArtVfx.GroundRing("warning_ring", target, ReappearZoneRadius, InvisibleSeconds, new Color(0.8f, 0.7f, 1f, 1f));
                }
            }

            yield return new WaitForSeconds(InvisibleSeconds);

            if (enemy == null || enemy.IsDead) yield break;

            enemy.BaseColor = normal;
            enemy.Invulnerable = false;
            frozen = false;

            if (!hasTarget || game.Player == null || game.Player.IsDead) yield break;

            enemy.transform.position = target;
            enemy.PlayAction("appear");
            BurstZone.Spawn(target, ReappearZoneRadius, ReappearDamagePerSecond, ReappearZoneSeconds,
                game.Player, enemy.transform.parent, (enemy.Def==EnemyDef.SteamKettleElder ? BurstZone.TeaPoolArt : BurstZone.CoffeePoolArt),
                DamageSource.Zone(enemy.Def, "재등장 장판"));
        }
    }
}
