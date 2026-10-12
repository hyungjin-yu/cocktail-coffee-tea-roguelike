using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 최종보스 진상 손님 — 06:133 "4패턴 완성": 환불→소환→별점→주문 부정 고정 순서로 반복.
    /// 패턴 시작 시 분노(HP&lt;50%) 여부를 고정해 실행 중간에 피해/쿨타임이 바뀌지 않는다.
    /// 모든 시전(예고 포함) 중에는 제자리에 멈추고, 패턴이 끝나면 다시 추적을 재개한다.
    /// </summary>
    public class CustomerBossDriver : MonoBehaviour, IEnemyDriver
    {
        const float FirstDelay = 3f;
        const float Cooldown = 7f;
        const float EnragedCooldown = 5f;

        const float RefundTelegraphSeconds = 0.8f;
        const float RefundRadius = 4f;
        const float RefundSeconds = 5f;

        const float SummonTelegraphSeconds = 1f;
        const int SummonCount = 10;
        const int EnragedSummonCount = 16;
        const int MaxAliveForSummon = 160;

        const float StarTelegraphSeconds = 0.8f;
        const int StarShotCount = 5;
        const float StarFanDegrees = 60f;
        const float StarProjectileSpeed = 5f;
        const float StarDamage = 12f;
        const float StarEnragedDamage = 16f;
        const float StarSlowMultiplier = 0.75f;
        const float StarSlowSeconds = 2f;
        /// <summary>difficult_customer manifest review 프레임별 별점 발사 지점(휴대폰 화면).</summary>
        public const string ReviewOriginAnchor = "reviewOrigin";

        const float SpellTelegraphSeconds = 0.8f;
        const float SpellLockSeconds = 5f;
        const float SpellLockEnragedSeconds = 6f;
        const float CoffeeZoneTelegraphSeconds = 0.8f;
        const float CoffeeZoneRadius = 1.2f;
        const float CoffeeZoneDamagePerSecond = 6f;
        const float CoffeeZoneSeconds = 2f;

        Enemy enemy;
        GameManager game;
        float timer = FirstDelay;
        int patternIndex;
        /// <summary>화염→독→하이브리드 순환 커서(06:142, 2026-09-25 하이브리드 배선 완료).</summary>
        int lockCycle = -1;
        /// <summary>시전(예고 포함) 중에는 제자리에 멈춘다 (06:134 "모든 시전 예고 중 정지").</summary>
        bool frozen;

        bool Enraged => enemy.Hp < enemy.MaxHp * 0.5f;

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

            enemy.SetEnragedVisual(Enraged);

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            // 06:134 — 패턴 시작 시 분노 여부를 고정. 쿨타임과 이번 패턴의 세기 모두 이 값을 쓴다.
            bool enraged = Enraged;
            timer = enraged ? EnragedCooldown : Cooldown;

            switch (patternIndex++ % 4)
            {
                case 0: StartCoroutine(Refund()); break;
                case 1: StartCoroutine(Summon(enraged)); break;
                case 2: StartCoroutine(StarRating(enraged)); break;
                default: StartCoroutine(SpellBlock(enraged)); break;
            }
        }

        IEnumerator Refund()
        {
            frozen = true;
            game.Hud.ShowBanner("\"환불해주세요!\"", "곧 강화 효과가 무효화됩니다!", RefundTelegraphSeconds);
            enemy.PlayAction("refund", RefundTelegraphSeconds);
            FadeVfx.Spawn(SpriteFactory.Circle, enemy.transform.position, 0f,
                Vector2.one * 0.4f, Vector2.one * (RefundRadius * 2f),
                new Color(1f, 0.25f, 0.25f, 0.4f), RefundTelegraphSeconds, 5);
            ArtVfx.GroundRing("warning_ring", enemy.transform.position, RefundRadius, RefundTelegraphSeconds, Color.white);

            yield return new WaitForSeconds(RefundTelegraphSeconds);

            if (enemy != null && !enemy.IsDead && game.Player != null && !game.Player.IsDead)
            {
                float dist = Vector2.Distance(enemy.transform.position, game.Player.Position);
                if (dist <= RefundRadius) // 06:139 — 범위 안 플레이어에게만
                {
                    game.Hud.ShowBanner("\"환불해주세요!\"", $"공격·이동 강화 효과 {RefundSeconds:0}초간 무효!", 2.5f);
                    game.Stats.SuppressBonuses(RefundSeconds);
                    if (ArtVfx.GroundRing("pressure_wave", game.Player.Position, 1.2f, 0.5f, Color.white, true,
                            SortOrders.AirVfx, "refund") == null)
                        FadeVfx.Spawn(SpriteFactory.Circle, game.Player.Position, 0f,
                            Vector2.one * 0.5f, Vector2.one * 6f, new Color(1f, 0.2f, 0.25f, 0.6f), 0.5f, 5);
                }
            }

            frozen = false;
        }

        IEnumerator Summon(bool enraged)
        {
            frozen = true;
            game.Hud.ShowBanner("\"사장 나와!\"", "잡몹이 몰려온다!", SummonTelegraphSeconds);
            enemy.PlayAction("summon", SummonTelegraphSeconds);

            // 06:140 — 좌/우 소환 지점을 실제 자리별로 표시
            int count = enraged ? EnragedSummonCount : SummonCount;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = enemy.transform.position.x + side * (enemy.Def.radius + 2f + 1f);
                FadeVfx.Spawn(SpriteFactory.Circle, new Vector2(x, Balance.LaneCenterY), 0f,
                    Vector2.one * 0.3f, Vector2.one * (Balance.DepthHalfWidth * 2f),
                    new Color(0.9f, 0.2f, 0.9f, 0.3f), SummonTelegraphSeconds, 4);
                ArtVfx.GroundRing("summon_ring", new Vector2(x, Balance.LaneCenterY), 1.2f, SummonTelegraphSeconds, Color.white);
            }

            yield return new WaitForSeconds(SummonTelegraphSeconds);

            if (enemy != null && !enemy.IsDead)
            {
                float hpScale = Balance.EnemyHpScale(game.RunTime);
                for (int i = 0; i < count; i++)
                {
                    if (Enemy.Active.Count >= MaxAliveForSummon) break;

                    float side = (i % 2 == 0) ? -1f : 1f;
                    float depth = Random.Range(Balance.DepthMin, Balance.DepthMax);
                    float x = enemy.transform.position.x + side * (enemy.Def.radius + 2f + Random.Range(0f, 2f));
                    var summoned = Enemy.SpawnAt(new Vector2(x, depth), EnemyRoster.PickCommon(game.RunTime, game.SpawnRng, game.Job), hpScale,
                        game.Player, enemy.transform.parent);
                    EnemyRoster.AttachBehavior(summoned, game, game.Player);
                }
            }

            frozen = false;
        }

        IEnumerator StarRating(bool enraged)
        {
            frozen = true;
            if (game.Player == null || game.Player.IsDead) { frozen = false; yield break; }

            Vector2 origin = enemy.transform.position;
            // 06:141 — 플레이어 방향으로 60도 부채를 고정 표시 (예고 시작 시 방향 고정, 재조준 없음)
            Vector2 baseDir = ((Vector2)game.Player.Position - origin).normalized;
            game.Hud.ShowBanner("\"별점 1점!\"", "부채꼴 방향을 피하세요!", StarTelegraphSeconds);
            if (enemy.PlayAction("review", StarTelegraphSeconds))
                enemy.FaceTowards(game.Player.Position.x, StarTelegraphSeconds); // 고정된 부채 방향 쪽을 바라본 채 휴대폰을 든다
            FadeVfx.Spawn(SpriteFactory.Circle, origin, 0f,
                Vector2.one * 0.4f, Vector2.one * 6f, new Color(1f, 0.85f, 0.2f, 0.35f), StarTelegraphSeconds, 5);
            // 09 review_star 예고 — review 클립이 예고 대부분 머무는 마지막 프레임의 reviewOrigin(휴대폰 화면). 없으면 머리 위.
            var review = enemy.UsesBodyArt ? ArtLibrary.Get(enemy.Def.ArtId)?.Clip("review") : null;
            if (review == null || !enemy.TryGetActionAnchorWorld("review", ReviewOriginAnchor, out var starAt,
                    review.frames.Length - 1))
                starAt = origin + new Vector2(0f, enemy.Def.radius + 0.6f);
            ArtVfx.Spawn("review_star", starAt, 0f, 0.9f, StarTelegraphSeconds, Color.white, SortOrders.AirVfx, 1f, false);

            yield return new WaitForSeconds(StarTelegraphSeconds);

            if (enemy != null && !enemy.IsDead && game.Player != null)
            {
                float damage = enraged ? StarEnragedDamage : StarDamage;
                float step = StarFanDegrees / (StarShotCount - 1);
                float startAngle = -StarFanDegrees * 0.5f;
                // 별점 탄은 지금 보이는 review 프레임의 reviewOrigin(휴대폰)에서 나간다. 부채 방향(baseDir)·시점은 예고 그대로.
                Vector2 shotOrigin = enemy.TryGetActionAnchorWorld("review", ReviewOriginAnchor, out var phone)
                    ? phone : origin;
                for (int i = 0; i < StarShotCount; i++)
                {
                    var dir = Quaternion.Euler(0f, 0f, startAngle + step * i) * baseDir;
                    EnemyProjectile.Spawn(shotOrigin, dir, StarProjectileSpeed, damage, game.Player,
                        enemy.transform.parent, EnemyProjectile.ArtPrefix + "review_star",
                        () => game.Player.Stats.ApplySlow(StarSlowSeconds, StarSlowMultiplier));
                }
            }

            frozen = false;
        }

        IEnumerator SpellBlock(bool enraged)
        {
            frozen = true;
            game.Hud.ShowBanner("\"주문한 거 아닌데?\"", "스킬이 잠깐 봉인됩니다...", SpellTelegraphSeconds);
            enemy.PlayAction("reject", SpellTelegraphSeconds);

            yield return new WaitForSeconds(SpellTelegraphSeconds);

            if (enemy == null || enemy.IsDead || game.Player == null || game.Player.IsDead)
            {
                frozen = false;
                yield break;
            }

            float lockSeconds = enraged ? SpellLockEnragedSeconds : SpellLockSeconds;
            var stats = game.Player.Stats;
            bool locked = false;
            // 06:142 — 화염→독→하이브리드 순환으로 "보유한" 스킬 중 하나만 잠근다.
            for (int tries = 0; tries < 3 && !locked; tries++)
            {
                lockCycle = (lockCycle + 1) % 3;
                if (lockCycle == 0 && stats.flameLevel > 0) { stats.LockFlame(lockSeconds); locked = true; }
                else if (lockCycle == 1 && stats.poisonLevel > 0) { stats.LockPoison(lockSeconds); locked = true; }
                else if (lockCycle == 2 && game.HybridUnlocked) { stats.LockHybrid(lockSeconds); locked = true; }
            }

            if (locked)
            {
                AudioService.PlaySfx("spell", enemy.transform.position); // 09: "spell: 화염/독/봉인" — 봉인
                ArtVfx.Spawn("seal_burst", game.Player.Position + new Vector2(0f, 0.8f), 0f, 1.2f, 0.34f, Color.white); // 09 seal_burst
                game.Hud.ShowBanner("주문 부정!", $"주기 스킬이 {lockSeconds:0}초간 봉인됐습니다", 2f);
                frozen = false;
                yield break;
            }

            // 06:142 — 보유한 주기 스킬이 하나도 없으면 대체: 플레이어 중심 커피 장판
            Vector2 pos = game.Player.Position;
            game.Hud.ShowBanner("주문 부정!", "대신 발밑에 커피 장판이 생깁니다!", CoffeeZoneTelegraphSeconds);
            if (ZoneTelegraphV2.Spawn(pos, CoffeeZoneRadius, CoffeeZoneTelegraphSeconds, new Color(1f, .62f, .3f), enemy.transform.parent) == null)
            {
                FadeVfx.Spawn(SpriteFactory.Circle, pos, 0f,
                    Vector2.one * 0.3f, Vector2.one * (CoffeeZoneRadius * 2f),
                    new Color(0.5f, 0.3f, 0.15f, 0.4f), CoffeeZoneTelegraphSeconds);
                ArtVfx.GroundRing("warning_ring", pos, CoffeeZoneRadius, CoffeeZoneTelegraphSeconds, Color.white);
            }

            yield return new WaitForSeconds(CoffeeZoneTelegraphSeconds);

            if (game.Player != null)
                BurstZone.Spawn(pos, CoffeeZoneRadius, CoffeeZoneDamagePerSecond, CoffeeZoneSeconds,
                    game.Player, enemy.transform.parent, BurstZone.CoffeePoolArt);

            frozen = false;
        }
    }
}
