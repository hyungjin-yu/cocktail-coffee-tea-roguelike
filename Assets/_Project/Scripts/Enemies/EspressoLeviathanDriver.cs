using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 강화 보스(3번째 중간보스): 에스프레소 머신 리바이어던 ([[06_전투와 런 규칙 v1.0]] 강화 절 참고).
    /// 그라인더 골렘의 "직선 위협"(증기 분사)과 드립 서버 스펙터의 "장판"(드립 폭우) 패턴을 압축 계승하고,
    /// 신규 패턴 "압력 게이지 폭발"을 더한다. 압력 게이지는 시간이 지나면 자동으로 차고, 플레이어가
    /// 보스에게 주는 피해로 늦출 수 있다(딜레이스). HP 60%/30% 이하에서 축적 속도가 빨라진다.
    /// 06:130 — 압력이 100에 도달하면 진행 중이던 증기/폭우 예고를 취소하고 압력 시전으로 전환한다.
    /// </summary>
    public class EspressoLeviathanDriver : MonoBehaviour, IEnemyDriver
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
        const float DripRainTelegraphSeconds = 0.6f;

        const float PressureMax = 100f;
        const float PressureFillPerSecond = 6f;
        const float PressureFillPerSecondPhase60 = 9f;
        const float PressureFillPerSecondPhase30 = 13f;
        /// <summary>플레이어가 보스에게 준 피해 1당 압력 게이지가 이만큼 줄어든다 (딜레이스 체크).</summary>
        const float PressureReliefPerDamage = 0.15f;
        const float PressureTelegraphSeconds = 0.6f;
        const float PressureExplosionRadius = 3.5f;
        const float PressureExplosionDamage = 20f;

        Enemy enemy;
        GameManager game;
        float attackTimer = FirstAttackDelay;
        int patternIndex;
        float pressure;
        /// <summary>증기/폭우 예고~시전 중이거나 압력 폭발 예고 중이면 true — 그동안 제자리에 멈춘다(06:127/128 "시전 중 정지").</summary>
        bool frozen;
        /// <summary>압력 폭발 예고가 시작된 뒤로는 true — 그동안 압력이 고정되어 피해로도 줄지 않는다(06:130).</summary>
        bool pressureLocked;
        Coroutine currentPattern;
        /// <summary>진행 중인 증기/폭우 패턴의 예고 표시. 압력 시전으로 패턴이 취소되면 함께 지운다(06:130 "기존 미발동 공격 경고 취소").</summary>
        readonly System.Collections.Generic.List<FadeVfx> pendingWarnings = new System.Collections.Generic.List<FadeVfx>();

        void Warn(FadeVfx v) { if (v != null) pendingWarnings.Add(v); }

        readonly System.Collections.Generic.List<ZoneTelegraphV2> pendingV2 = new System.Collections.Generic.List<ZoneTelegraphV2>();

        void ClearPendingWarnings()
        {
            foreach (var v in pendingWarnings) if (v != null) Destroy(v.gameObject);
            pendingWarnings.Clear();
            foreach (var v in pendingV2) if (v != null) Destroy(v.gameObject);
            pendingV2.Clear();
        }

        public void Init(Enemy enemy, GameManager game)
        {
            this.enemy = enemy;
            this.game = game;
            enemy.Damaged += OnDamaged;
            enemy.SetDriver(this);
        }

        void OnDestroy()
        {
            if (enemy != null) enemy.Damaged -= OnDamaged;
        }

        public bool TryGetVelocity(Vector2 selfPosition, Vector2 playerPosition, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            return frozen; // frozen이 아니면 false를 돌려줘서 Enemy의 기본 추적을 그대로 쓴다
        }

        void OnDamaged(float amount)
        {
            if (pressureLocked) return; // 06:130 — 예고 중 압력은 고정되어 피해로 취소되지 않음
            pressure = Mathf.Max(0f, pressure - amount * PressureReliefPerDamage);
        }

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
            if (pressureLocked) return;
            pressure += FillRate * Time.deltaTime;
            if (pressure < PressureMax) return;

            // 06:130 — 기존 미발동 공격 경고 취소, 압력 시전으로 전환
            if (currentPattern != null)
            {
                StopCoroutine(currentPattern);
                currentPattern = null;
                ClearPendingWarnings(); // 취소된 패턴의 예고가 화면에 남아 계속 경고하지 않게
            }
            currentPattern = StartCoroutine(PressureExplosionRoutine());
        }

        IEnumerator PressureExplosionRoutine()
        {
            pressureLocked = true;
            frozen = true;

            game.Hud.ShowBanner("압력 게이지 폭발 예고!", "곧 넓은 범위에 피해 + 넉백!", PressureTelegraphSeconds);
            enemy.PlayAction("pressure", PressureTelegraphSeconds);
            FadeVfx.Spawn(SpriteFactory.Circle, enemy.transform.position, 0f,
                Vector2.one * 0.4f, Vector2.one * (PressureExplosionRadius * 2f),
                new Color(0.95f, 0.7f, 0.25f, 0.5f), PressureTelegraphSeconds, 5);
            ArtVfx.GroundRing("warning_ring", enemy.transform.position, PressureExplosionRadius, PressureTelegraphSeconds, Color.white);

            yield return new WaitForSeconds(PressureTelegraphSeconds);

            if (enemy != null && !enemy.IsDead) Explode();

            pressure = 0f;
            pressureLocked = false;
            frozen = false;
            currentPattern = null;
            attackTimer = AttackCooldown; // 06:130 — 이후 대기 6.5초 후 중단됐던 패턴 순서부터 재개(기존 활성 장판은 남는다)
        }

        void Explode()
        {
            AudioService.PlaySfx("explosion", enemy.transform.position);
            game.Hud.ShowBanner("압력 게이지 폭발!", "밀려남 + 광역 피해!", 2f);
            CameraRig.Shake(0.35f, 0.3f); // 08:122
            if (ArtVfx.GroundRing("pressure_wave", enemy.transform.position, PressureExplosionRadius, 0.4f, Color.white, true,
                    SortOrders.AirVfx, "pressure") == null)
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
            if (pressureLocked) return; // 06:131 — 압력 경고와 다른 단발 예고를 동시에 시작하지 않는다
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            attackTimer = AttackCooldown;
            currentPattern = patternIndex++ % 2 == 0 ? StartCoroutine(SteamBeam()) : StartCoroutine(DripRainRoutine());
        }

        IEnumerator SteamBeam()
        {
            frozen = true;
            if (game.Player == null || game.Player.IsDead) { frozen = false; currentPattern = null; yield break; }

            Vector2 origin = enemy.transform.position;
            Vector2 dir = game.Player.Position.x >= origin.x ? Vector2.right : Vector2.left;
            Vector2 mid = origin + dir * (SteamRange * 0.5f);
            float angle = dir.x >= 0f ? 0f : 180f;

            pendingWarnings.Clear();
            Warn(FadeVfx.Spawn(SpriteFactory.Square, mid, angle,
                new Vector2(SteamRange, 0.15f), new Vector2(SteamRange, SteamWidth),
                new Color(1f, 0.3f, 0.2f, 0.35f), SteamTelegraphSeconds, SortOrders.GroundZone));
            // 증기 예고 띠는 바닥 층(캐릭터 아래) — 불투명 경고선이 플레이어 몸을 가리던 문제(2026-10-08 아트 검수 P1). 폭·사거리·시간·피해 불변.
            Warn(ArtVfx.Band("warning_line", mid, angle, SteamRange, SteamWidth, SteamTelegraphSeconds, Color.white, false, SortOrders.GroundZone));
            AudioService.PlaySfx("steam", mid);
            enemy.PlayAction("steam", SteamTelegraphSeconds);
            game.Hud.ShowBanner("증기 분사", "조준선 방향을 피하세요!", SteamTelegraphSeconds);

            yield return new WaitForSeconds(SteamTelegraphSeconds);

            if (enemy == null || enemy.IsDead || game.Player == null || game.Player.IsDead)
            {
                frozen = false;
                currentPattern = null;
                yield break;
            }

            // 조준선은 텔레그래프가 시작될 때 고정한 방향으로 발사 (재조준 없음 — 옆으로 피하면 안전)
            Vector2 endPoint = origin + dir * SteamRange;
            if (DistancePointToSegment(game.Player.Position, origin, endPoint) <= SteamWidth * 0.5f)
                game.Player.TakeDamage(SteamDamage);

            // 04 §8 steam_jet "연결 타일" — 사거리를 따라 여러 조각을 이어 붙인다(판정은 선분 거리 그대로).
            if (ArtLibrary.Get("steam_jet") != null)
            {
                int segments = Mathf.Max(1, Mathf.CeilToInt(SteamRange / SteamWidth));
                float seg = SteamRange / segments;
                for (int i = 0; i < segments; i++)
                    ArtVfx.Band("steam_jet", origin + dir * (seg * (i + 0.5f)), angle, seg, SteamWidth, 0.33f, Color.white);
            }
            else
                FadeVfx.Spawn(SpriteFactory.Square, mid, angle,
                    new Vector2(SteamRange, SteamWidth), new Vector2(SteamRange, SteamWidth * 0.3f),
                    new Color(1f, 0.85f, 0.6f, 0.7f), 0.25f, 4);

            frozen = false;
            currentPattern = null;
        }

        IEnumerator DripRainRoutine()
        {
            frozen = true;
            game.Hud.ShowBanner("드립 폭우 예고", "곧 안전지대를 찾아 움직이세요!", DripRainTelegraphSeconds);
            enemy.PlayAction("rain", DripRainTelegraphSeconds);

            // 06:128 — 시작 시 위치를 고정하고 0.6초 예고 후 동시에 생성.
            pendingWarnings.Clear();
            var positions = new Vector2[DripRainZoneCount];
            bool havePlayer = game.Player != null;
            for (int i = 0; i < DripRainZoneCount; i++)
            {
                Vector2 pos = havePlayer
                    ? game.Player.Position + Random.insideUnitCircle * DripRainSpread
                    : (Vector2)enemy.transform.position;
                pos.y = Mathf.Clamp(pos.y, Balance.DepthMin, Balance.DepthMax);
                positions[i] = pos;
                var v2 = ZoneTelegraphV2.Spawn(pos, DripRainRadius, DripRainTelegraphSeconds, new Color(1f, .62f, .3f), enemy.transform.parent);
                if (v2 != null) pendingV2.Add(v2); // 43 v2 예고(압력 시전 취소 시 함께 제거)
                else
                {
                    Warn(FadeVfx.Spawn(SpriteFactory.Circle, pos, 0f,
                        Vector2.one * 0.3f, Vector2.one * (DripRainRadius * 2f),
                        new Color(0.85f, 0.5f, 0.2f, 0.35f), DripRainTelegraphSeconds));
                    Warn(ArtVfx.GroundRing("warning_ring", pos, DripRainRadius, DripRainTelegraphSeconds, Color.white));
                }
            }

            yield return new WaitForSeconds(DripRainTelegraphSeconds);

            if (enemy != null && !enemy.IsDead && game.Player != null)
            {
                foreach (var pos in positions)
                    BurstZone.Spawn(pos, DripRainRadius, DripRainDamagePerSecond, DripRainZoneSeconds,
                        game.Player, enemy.transform.parent, (enemy.Def==EnemyDef.TeaKilnDragon ? BurstZone.TeaPoolArt : BurstZone.CoffeePoolArt));
            }

            frozen = false; // 생성 시 패턴 종료 (장판 수명은 독립)
            currentPattern = null;
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
