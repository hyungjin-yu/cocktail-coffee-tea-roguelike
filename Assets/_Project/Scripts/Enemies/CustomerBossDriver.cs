using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 최종보스 진상 손님 — 06:133 "4패턴 완성": 환불→소환→별점→주문 부정 고정 순서에
    /// 2026-11-24(#24) 5번째 "영수증 저격"(조준 원거리)을 더해 환불→소환→별점→주문 부정→저격으로 반복.
    /// HP 30% 이하 최종 단계에서는 일반 패턴 2개마다 복합 패턴 "폐점 클레임 러시"(틈 있는 영수증 벽 → 저격 → 반대쪽 틈 벽 → 회복)를 끼운다.
    /// 패턴 시작 시 분노(HP&lt;50%) 여부를 고정해 실행 중간에 피해/쿨타임이 바뀌지 않는다.
    /// 모든 시전(예고 포함) 중에는 제자리에 멈추고, 패턴이 끝나면 다시 추적을 재개한다. 한 번에 한 패턴만 돈다.
    /// 바텐더(진상 손님)·찻집(까다로운 차 손님) 공통 — 같은 몸 아트·같은 수치.
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

        // ── #24 영수증 저격(조준 원거리) — 가안 수치. 준비(조준 추적) → 고정(락) → 발사 → 회복 ──
        /// <summary>조준선·탄 사거리(월드). 16:9 화면 폭(약 21.3)보다 길어 화면 어디서든 닿는다.</summary>
        public const float SnipeRange = 26f;
        public const float SnipeFirstTrackSeconds = 1.2f;    // 최초 노출: 길게 보여 준다
        public const float SnipeTrackSeconds = 1.0f;
        public const float SnipeEnragedTrackSeconds = 0.75f;
        public const float SnipeFollowupTrackSeconds = 0.5f; // 분노 2연사 두 번째·러시 안 저격
        public const float SnipeLockSeconds = 0.55f;
        public const float SnipeEnragedLockSeconds = 0.45f;
        const float SnipeFollowupGapSeconds = 0.35f;  // 연사 사이 이동 여유
        public const float SnipeRecoverSeconds = 0.8f; // 발사 뒤 정지(반격 여유)
        const float SnipeTrackThickness = 0.12f;
        const int SnipeShotCount = 3;                  // 같은 선 위 3발(피격 무적 0.8초라 실제로는 1회 피해)
        const float SnipeShotSpacing = 0.08f;
        public const float SnipeProjectileSpeed = 13f;
        const float SnipeDamage = 18f;
        const float SnipeEnragedDamage = 20f;
        /// <summary>탄 한 줄의 실제 위험 반폭 = 탄 판정 반지름 + 플레이어 반지름. 예고 띠 두께 = 2×이 값.</summary>
        public const float DangerHalfWidth = EnemyProjectile.BaseHitRadius + PlayerController.Radius;

        // ── #24 최종 단계 "폐점 클레임 러시"(HP 30% 이하) — 가안 수치 ──
        public const float FinalPhaseFraction = 0.3f;
        /// <summary>최종 단계에서 러시 사이에 끼는 일반 패턴 수.</summary>
        const int NormalPatternsBetweenRush = 2;
        public const int WallLaneCount = 8;
        public const float WallLaneSpacing = 1f;
        public const float WallFirstLaneY = Balance.DepthMin + 0.5f; // -5.5 … 1.5
        public const int WallGapLanes = 2;                           // 연속 2줄 비움 → 통로 폭 1.76(플레이어 중심 기준)
        public const float WallGapMinOffset = 1.2f;  // 통로 중심이 플레이어에서 이만큼 이상 떨어져야(제자리 안전 방지)
        public const float WallGapMaxOffset = 3.2f;  // … 이만큼 이하(예고 1초 안에 둔화 상태로도 도달)
        public const float WallTelegraphSeconds = 1.0f;
        public const float WallProjectileSpeed = 7f;
        const float WallDamage = 10f;
        const float RushMoveWindowSeconds = 1.2f;
        const float RushSnipeWindowSeconds = 1.0f;
        public const float RushRecoverSeconds = 2.0f;
        const float RushAfterCooldown = 2.5f; // 회복 2초 + 이만큼 다음 패턴 없음
        /// <summary>보스 패턴이 새 탄을 더 내지 않는 적 탄 수 상한(10 §3 부하 시나리오 100발 + 여유).</summary>
        public const int MaxEnemyProjectilesForPattern = 140;

        /// <summary>예고 아트 ID(Codex 인계, Docs/FinalBossPatterns-2026-11-24/telegraph-art-spec.md). 없으면 warning_line 대체.</summary>
        public const string AimTrackArt = FeedbackArt.FinalAimTrack, AimLockArt = FeedbackArt.FinalAimLock, QueueWallArt = FeedbackArt.FinalQueueWall;
        public const string RefundRingArt = FeedbackArt.FinalRefundRing, StarFanArt = FeedbackArt.FinalStarFan;

        Enemy enemy;
        GameManager game;
        float timer = FirstDelay;
        /// <summary>패턴 코루틴 실행 중 — 다음 패턴은 끝난 뒤 시작한다(러시가 길어도 겹치지 않게).</summary>
        bool busy;
        bool finalPhaseEntered;
        bool rushDue;
        int normalSinceRush;
        bool snipeIntroShown;
        /// <summary>진행 중인 예고 그림(보스 사망·파괴 시 즉시 정리).</summary>
        readonly List<FadeVfx> liveWarnings = new List<FadeVfx>();

        // 테스트·검수용 읽기 전용 기록
        public int SnipeVolleys { get; private set; }
        public Vector2 LastSnipeOrigin { get; private set; }
        public Vector2 LastSnipeDirection { get; private set; }
        public int RushCount { get; private set; }
        public float LastGapCenterY { get; private set; }
        public bool InFinalPhase => finalPhaseEntered;
        public bool Busy => busy;
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

        void OnDestroy() => ClearWarnings();

        void ClearWarnings()
        {
            foreach (var v in liveWarnings) if (v != null) Destroy(v.gameObject);
            liveWarnings.Clear();
        }

        FadeVfx Keep(FadeVfx v) { if (v != null) liveWarnings.Add(v); return v; }

        bool Alive => enemy != null && !enemy.IsDead && game != null && game.Player != null && !game.Player.IsDead;

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

            if (!finalPhaseEntered && enemy.Hp <= enemy.MaxHp * FinalPhaseFraction)
            {
                finalPhaseEntered = true;
                rushDue = true;
                // 진행 중인 패턴의 예고 문구를 덮지 않도록 실행 중이 아닐 때만 즉시 안내(아니면 러시 시작 배너가 안내한다).
                if (!busy) game.Hud.ShowBanner("최종 단계: 폐점 직전!", "진상 손님이 마지막 클레임을 준비합니다", 2f);
                if (!HitFeedback.LowEffects) CameraRig.Shake(0.2f, 0.25f);
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (busy) return; // 이전 패턴(러시 등)이 끝나야 다음 패턴

            // 06:134 — 패턴 시작 시 분노 여부를 고정. 쿨타임과 이번 패턴의 세기 모두 이 값을 쓴다.
            bool enraged = Enraged;
            timer = enraged ? EnragedCooldown : Cooldown;

            if (finalPhaseEntered && rushDue)
            {
                rushDue = false;
                normalSinceRush = 0;
                Run(ClosingRush());
                return;
            }

            switch (patternIndex++ % 5)
            {
                case 0: Run(Refund()); break;
                case 1: Run(Summon(enraged)); break;
                case 2: Run(StarRating(enraged)); break;
                case 3: Run(SpellBlock(enraged)); break;
                default: Run(Snipe(enraged)); break;
            }
            if (finalPhaseEntered && ++normalSinceRush >= NormalPatternsBetweenRush) rushDue = true;
        }

        void Run(IEnumerator routine) => StartCoroutine(RunGuarded(routine));

        IEnumerator RunGuarded(IEnumerator routine)
        {
            busy = true;
            yield return StartCoroutine(routine);
            busy = false;
        }

        IEnumerator Refund()
        {
            frozen = true;
            game.Hud.ShowBanner("\"환불해주세요!\"", "빨간 원 밖으로! 안에 있으면 강화 효과 무효", RefundTelegraphSeconds);
            enemy.PlayAction("refund", RefundTelegraphSeconds);
            // #24 예고 아트: 판정 원(r 4) = 그림 테두리 바깥 가장자리. 없으면 기존 커지는 원 + warning_ring.
            if (Keep(LowFx(ArtVfx.GroundRing(RefundRingArt, enemy.transform.position, RefundRadius, RefundTelegraphSeconds, Color.white), RefundRingArt)) == null)
            {
                FadeVfx.Spawn(SpriteFactory.Circle, enemy.transform.position, 0f,
                    Vector2.one * 0.4f, Vector2.one * (RefundRadius * 2f),
                    new Color(1f, 0.25f, 0.25f, 0.4f), RefundTelegraphSeconds, 5);
                ArtVfx.GroundRing("warning_ring", enemy.transform.position, RefundRadius, RefundTelegraphSeconds, Color.white);
            }

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
                // 2026-11-24 사용자 지시: 회피 성공 문구("환불 회피!") 제거 — 원 밖이면 아무 표시 없이 강화 유지.
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
                float x = BattleArena.ClampX(enemy.transform.position.x + side * (enemy.Def.radius + 2f + 1f), 1.2f); // 57 구역 안 예고
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
                    float x = BattleArena.ClampX(enemy.transform.position.x + side * (enemy.Def.radius + 2f + Random.Range(0f, 2f)), 1.2f);
                    var pick = game.PickStandardEnemy(game.RunTime, out var reward); // 2026-11-10 계열 표 선택(기본 = 기존 PickCommon 그대로)
                    var summoned = Enemy.SpawnAt(new Vector2(x, depth), pick, hpScale, game.Player, enemy.transform.parent);
                    if (reward != pick) summoned.RewardDef = reward;
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
            // 09 review_star 예고 — review 클립이 예고 대부분 머무는 마지막 프레임의 reviewOrigin(휴대폰 화면). 없으면 머리 위.
            var review = enemy.UsesBodyArt ? ArtLibrary.Get(enemy.Def.ArtId)?.Clip("review") : null;
            if (review == null || !enemy.TryGetActionAnchorWorld("review", ReviewOriginAnchor, out var starAt,
                    review.frames.Length - 1))
                starAt = origin + new Vector2(0f, enemy.Def.radius + 0.6f);
            ArtVfx.Spawn("review_star", starAt, 0f, 0.9f, StarTelegraphSeconds, Color.white, SortOrders.AirVfx, 1f, false);
            // #24 예고 아트: 휴대폰(발사점)에서 뻗는 탄줄 5개(-30~30°, 길이 15, 폭 1.24). 없으면 기존 노란 원.
            if (StarFan(starAt, baseDir, StarTelegraphSeconds) == null)
                FadeVfx.Spawn(SpriteFactory.Circle, origin, 0f,
                    Vector2.one * 0.4f, Vector2.one * 6f, new Color(1f, 0.85f, 0.2f, 0.35f), StarTelegraphSeconds, 5);

            yield return new WaitForSeconds(StarTelegraphSeconds);

            if (enemy != null && !enemy.IsDead && game.Player != null)
            {
                float damage = enemy.ScaledAttack(enraged ? StarEnragedDamage : StarDamage);
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
                        () => game.Player.Stats.ApplySlow(StarSlowSeconds, StarSlowMultiplier),
                        DamageSource.BossSkill(enemy.Def, "별점 1점"), EnemyProjectile.Tier.Boss); // 2026-11-15 피드백 20 보스 탄 테두리·꼬리
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
            bool tea = game.Job == Job.TeaSommelier; // 2026-10-12 찻집: 찻물 장판·문구
            game.Hud.ShowBanner("주문 부정!", tea ? "대신 발밑에 뜨거운 찻물이 고입니다!" : "대신 발밑에 커피 장판이 생깁니다!", CoffeeZoneTelegraphSeconds);
            if (ZoneTelegraphV2.Spawn(pos, CoffeeZoneRadius, CoffeeZoneTelegraphSeconds, new Color(1f, .62f, .3f), enemy.transform.parent) == null)
            {
                FadeVfx.Spawn(SpriteFactory.Circle, pos, 0f,
                    Vector2.one * 0.3f, Vector2.one * (CoffeeZoneRadius * 2f),
                    new Color(0.5f, 0.3f, 0.15f, 0.4f), CoffeeZoneTelegraphSeconds);
                ArtVfx.GroundRing("warning_ring", pos, CoffeeZoneRadius, CoffeeZoneTelegraphSeconds, Color.white);
            }

            yield return new WaitForSeconds(CoffeeZoneTelegraphSeconds);

            if (game.Player != null)
                BurstZone.Spawn(pos, CoffeeZoneRadius, enemy != null ? enemy.ScaledAttack(CoffeeZoneDamagePerSecond) : CoffeeZoneDamagePerSecond, CoffeeZoneSeconds,
                    game.Player, enemy.transform.parent, tea ? BurstZone.TeaPoolArt : BurstZone.CoffeePoolArt,
                    DamageSource.Zone(enemy != null ? enemy.Def : null, tea ? "뜨거운 찻물 장판" : "커피 장판"));

            frozen = false;
        }
    
        // ───────────────────────── #24 영수증 저격 ─────────────────────────

        /// <summary>휴대폰 화면(review 마지막 프레임 reviewOrigin) 월드 위치. 아트·앵커가 없으면 머리 위.</summary>
        Vector2 PhoneOrigin()
        {
            Vector2 origin = enemy.transform.position;
            var review = enemy.UsesBodyArt ? ArtLibrary.Get(enemy.Def.ArtId)?.Clip("review") : null;
            if (review != null && enemy.TryGetActionAnchorWorld("review", ReviewOriginAnchor, out var at, review.frames.Length - 1))
                return at;
            return origin + new Vector2(0f, enemy.Def.radius + 0.6f);
        }

        /// <summary>띠 예고: 지정 아트 → warning_line → 단색 사각형 순으로 대체. 알파 상자 = 길이×두께(=판정 경계).</summary>
        FadeVfx BandWarning(string artId, Vector2 mid, float angle, float length, float thickness, float life, Color fill)
            => BandWarning(artId, mid, angle, length, thickness, life, fill, out _);

        FadeVfx BandWarning(string artId, Vector2 mid, float angle, float length, float thickness, float life, Color fill, out FadeVfx exact)
        {
            exact = null;
            // #24 예고 아트(알파 상자 = 판정 경계, 채움 포함)가 있으면 그것만. 저이펙트는 0번 프레임 고정.
            var art = Keep(LowFx(ArtVfx.Band(artId, mid, angle, length, thickness, life, Color.white, false, SortOrders.GroundZone), artId));
            if (art != null) return art;
            // 대체: 정확한 판정 경계를 옅은 단색 채움으로 깔고 warning_line을 같은 상자에 얹는다(04 §8).
            exact = Keep(FadeVfx.Spawn(SpriteFactory.Square, mid, angle, new Vector2(length, thickness), new Vector2(length, thickness),
                fill, life, SortOrders.GroundZone));
            return Keep(ArtVfx.Band("warning_line", mid, angle, length, thickness, life, Color.white, false, SortOrders.GroundZone));
        }

        /// <summary>저이펙트: 예고 동안 0번 프레임 고정(경계 정보는 그대로, 애니메이션만 생략).</summary>
        static FadeVfx LowFx(FadeVfx v, string artId)
        {
            if (v == null || !HitFeedback.LowEffects) return v;
            var clip = ArtLibrary.Get(artId)?.Main;
            if (clip != null && clip.frames.Length > 0)
                v.SetClip(new ArtClip { name = clip.name, fps = 0f, loop = false, frames = new[] { clip.frames[0] } }, false);
            return v;
        }

        /// <summary>별점 부채 예고: 스프라이트 피벗 = 발사점. 알파 상자 = 발사점 뒤 0.62 ~ 앞 15.62, 좌우 ±(15·sin30°+0.62).</summary>
        FadeVfx StarFan(Vector2 at, Vector2 dir, float life)
        {
            var art = ArtLibrary.Get(StarFanArt);
            var clip = art?.Main;
            if (clip == null || clip.frames.Length == 0) return null;
            float range = StarProjectileSpeed * EnemyProjectile.LifetimeSeconds;
            var size = art.ContentWorldSize;
            var scale = new Vector2((range + DangerHalfWidth * 2f) / Mathf.Max(0.01f, size.x),
                2f * (range * Mathf.Sin(StarFanDegrees * 0.5f * Mathf.Deg2Rad) + DangerHalfWidth) / Mathf.Max(0.01f, size.y));
            var v = FadeVfx.Spawn(clip.frames[0], at, Angle(dir), scale, scale, Color.white, life, SortOrders.GroundZone);
            v.SetClip(clip, false);
            return Keep(LowFx(v, StarFanArt));
        }

        static void Place(FadeVfx v, Vector2 mid, float angle)
        {
            if (v == null) return;
            v.transform.position = mid;
            v.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// "영수증 저격" — 멀리 있어도 닿는 조준 원거리. 휴대폰을 들고 가는 조준선이 플레이어를 따라가다(준비),
        /// 굵은 띠로 방향을 고정하고(락, 띠 두께 = 실제 위험 폭) 그 선을 따라 빠른 별점 탄을 쏜다. 고정 뒤 옆으로 비키면 안전.
        /// 최초 노출은 조준을 길게·설명 배너, 분노(HP&lt;50%)는 짧은 조준 + 연속 2회.
        /// </summary>
        IEnumerator Snipe(bool enraged)
        {
            frozen = true;
            if (!Alive) { frozen = false; yield break; }

            bool first = !snipeIntroShown;
            snipeIntroShown = true;
            float track = first ? SnipeFirstTrackSeconds : enraged ? SnipeEnragedTrackSeconds : SnipeTrackSeconds;
            float lockS = enraged ? SnipeEnragedLockSeconds : SnipeLockSeconds;
            int volleys = enraged ? 2 : 1;
            if (first)
                game.Hud.ShowBanner("\"리뷰 올리러 갑니다!\"", "휴대폰 조준선이 굵게 고정되면 옆으로 비키세요 · 멀리서도 닿습니다", track + lockS);
            else
                game.Hud.ShowBanner(enraged ? "\"영수증 저격 2연발!\"" : "\"영수증 저격!\"", "굵은 선이 고정되면 옆으로!", track + lockS);

            // 몸 클립(review)은 연사 전체 동안 한 번만 유지 — 2연사도 하나의 패턴으로 읽힌다.
            float shots = (SnipeShotCount - 1) * SnipeShotSpacing;
            float hold = track + lockS + shots + (volleys > 1 ? SnipeFollowupGapSeconds + SnipeFollowupTrackSeconds + lockS + shots : 0f);
            if (enemy.PlayAction("review", hold)) enemy.FaceTowards(game.Player.Position.x, hold);

            for (int i = 0; i < volleys; i++)
            {
                yield return AimAndFire(i == 0 ? track : SnipeFollowupTrackSeconds, lockS, enraged ? SnipeEnragedDamage : SnipeDamage, false);
                if (!Alive) break;
                if (i < volleys - 1) yield return new WaitForSeconds(SnipeFollowupGapSeconds);
            }

            if (Alive) yield return new WaitForSeconds(SnipeRecoverSeconds); // 회복: 정지(근접 반격 여유)
            frozen = false;
        }

        IEnumerator AimAndFire(float trackSeconds, float lockSeconds, float baseDamage, bool playClip = true)
        {
            if (!Alive) yield break;
            if (playClip && enemy.PlayAction("review", trackSeconds + lockSeconds))
                enemy.FaceTowards(game.Player.Position.x, trackSeconds + lockSeconds); // 앵커(휴대폰)는 바라보는 쪽 기준
            else if (!playClip)
                enemy.FaceTowards(game.Player.Position.x, trackSeconds + lockSeconds);
            Vector2 origin = PhoneOrigin();
            Vector2 dir = AimDir(origin);
            float angle = Angle(dir);

            // 준비: 가는 조준선이 플레이어를 따라간다(재조준 구간).
            var line = BandWarning(AimTrackArt, origin + dir * (SnipeRange * 0.5f), angle, SnipeRange, SnipeTrackThickness,
                trackSeconds, new Color(1f, 0.35f, 0.25f, 0.45f), out var fillLine);
            float t = 0f;
            while (t < trackSeconds)
            {
                if (!Alive) { ClearWarnings(); yield break; }
                dir = AimDir(origin);
                angle = Angle(dir);
                Vector2 mid = origin + dir * (SnipeRange * 0.5f);
                Place(line, mid, angle);
                Place(fillLine, mid, angle);
                t += Time.deltaTime; // 일시정지·보상창(timeScale 0) 중에는 멈춘다
                yield return null;
            }
            if (line != null) Destroy(line.gameObject);
            if (fillLine != null) Destroy(fillLine.gameObject);

            // 고정: 방향을 여기서 확정(이후 재조준 없음). 띠 두께 = 2×(탄 판정 + 플레이어 반지름).
            LastSnipeOrigin = origin;
            LastSnipeDirection = dir;
            BandWarning(AimLockArt, origin + dir * (SnipeRange * 0.5f), angle, SnipeRange, DangerHalfWidth * 2f,
                lockSeconds, new Color(1f, 0.2f, 0.15f, 0.38f));
            AudioService.PlaySfx("spell", origin);
            yield return new WaitForSeconds(lockSeconds);
            if (!Alive) { ClearWarnings(); yield break; }

            // 발사: 고정한 원점·방향 그대로 빠른 별점 탄. 상한을 넘으면 더 내지 않는다.
            float damage = enemy.ScaledAttack(baseDamage);
            AudioService.PlaySfx("shot_gun", origin);
            SnipeVolleys++;
            for (int i = 0; i < SnipeShotCount; i++)
            {
                if (EnemyProjectile.ActiveCount < MaxEnemyProjectilesForPattern)
                    EnemyProjectile.Spawn(origin, dir, SnipeProjectileSpeed, damage, game.Player, enemy.transform.parent,
                        EnemyProjectile.ArtPrefix + "review_star", null, DamageSource.BossSkill(enemy.Def, "영수증 저격"),
                        EnemyProjectile.Tier.Boss);
                if (i < SnipeShotCount - 1) yield return new WaitForSeconds(SnipeShotSpacing);
                if (!Alive) yield break;
            }
        }

        Vector2 AimDir(Vector2 origin)
        {
            Vector2 d = game.Player.Position - origin;
            return d.sqrMagnitude > 0.0001f ? d.normalized : (game.Player.Position.x >= origin.x ? Vector2.right : Vector2.left);
        }

        static float Angle(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        // ───────────────────────── #24 폐점 클레임 러시(최종 단계) ─────────────────────────

        public static float LaneY(int lane) => WallFirstLaneY + lane * WallLaneSpacing;
        /// <summary>틈(연속 WallGapLanes줄)이 lane gapStart부터일 때 통로 중심 y.</summary>
        public static float GapCenterY(int gapStart) => LaneY(gapStart) + (WallGapLanes - 1) * WallLaneSpacing * 0.5f;

        /// <summary>
        /// 틈 위치 선택(결정적): 통로 중심이 플레이어에서 [WallGapMinOffset, WallGapMaxOffset] 떨어진 후보 중 preferSide 쪽(+1 위, -1 아래),
        /// 0이면 레인 중심 쪽. 그 쪽에 없으면 반대쪽, 그래도 없으면 거리 2에 가장 가까운 후보. 제자리 안전(통로 안에 이미 서 있음)은 피한다.
        /// </summary>
        public static int ChooseGapStart(float playerY, int preferSide)
        {
            if (preferSide == 0) preferSide = Balance.LaneCenterY >= playerY ? 1 : -1;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int pass = 0; pass < 3 && best < 0; pass++)
            {
                for (int g = 0; g <= WallLaneCount - WallGapLanes; g++)
                {
                    float off = GapCenterY(g) - playerY;
                    float dist = Mathf.Abs(off);
                    if (pass < 2)
                    {
                        int side = pass == 0 ? preferSide : -preferSide;
                        if (Mathf.Sign(off) != side || dist < WallGapMinOffset || dist > WallGapMaxOffset) continue;
                    }
                    float score = Mathf.Abs(dist - 2f);
                    if (score < bestScore) { bestScore = score; best = g; }
                }
            }
            return best;
        }

        /// <summary>
        /// "폐점 클레임 러시" — HP 30% 이하 최종 단계 복합 패턴.
        /// ① 영수증 벽(연속 2줄 틈) → 이동 여유 1.2초 → ② 짧은 조준 저격 1회 → 여유 1.0초 → ③ 틈이 반대쪽인 벽 → ④ 회복 2초(정지, 반격).
        /// 벽은 좌우로 퍼지므로 지나간 뒤 반대쪽으로 건너가면 된다. 각 공격 사이에 이동·반격 시간을 일부러 남긴다.
        /// </summary>
        IEnumerator ClosingRush()
        {
            frozen = true;
            if (!Alive) { frozen = false; yield break; }
            RushCount++;
            bool first = RushCount == 1;
            game.Hud.ShowBanner("\"줄 서세요! 전부 환불이에요!\"",
                first ? "영수증 벽이 좌우로 퍼집니다 — 비어 있는 줄로 이동하세요" : "빈 줄 → 조준선 → 반대쪽 빈 줄",
                WallTelegraphSeconds + 0.6f);
            if (!HitFeedback.LowEffects) CameraRig.Shake(0.15f, 0.2f);

            enemy.PlayAction("summon", WallTelegraphSeconds);
            int firstSide = 0;
            yield return QueueWall(0, s => firstSide = s);
            if (!Alive) { frozen = false; yield break; }
            yield return new WaitForSeconds(RushMoveWindowSeconds);
            if (!Alive) { frozen = false; yield break; }

            yield return AimAndFire(SnipeFollowupTrackSeconds, SnipeEnragedLockSeconds, SnipeEnragedDamage);
            if (!Alive) { frozen = false; yield break; }
            yield return new WaitForSeconds(RushSnipeWindowSeconds);
            if (!Alive) { frozen = false; yield break; }

            enemy.PlayAction("refund", WallTelegraphSeconds);
            game.Hud.ShowBanner("\"다시 줄 서요!\"", "이번 빈 줄은 반대쪽", WallTelegraphSeconds);
            yield return QueueWall(-firstSide, null);
            if (!Alive) { frozen = false; yield break; }

            // 회복: 숨 고르기(정지) — 단계가 읽히도록 짧은 안내.
            game.Hud.ShowBanner("진상 손님이 숨을 고릅니다", "지금 반격하세요", RushRecoverSeconds);
            yield return new WaitForSeconds(RushRecoverSeconds);
            timer = Mathf.Max(timer, RushAfterCooldown);
            frozen = false;
        }

        /// <summary>틈 있는 영수증 벽 하나: 예고(묶음별 띠) → 좌우로 별점 탄 발사. onSide = 플레이어 기준 틈 방향(+1 위/-1 아래).</summary>
        IEnumerator QueueWall(int preferSide, System.Action<int> onSide)
        {
            float py = game.Player.Position.y;
            int gap = ChooseGapStart(py, preferSide);
            LastGapCenterY = GapCenterY(gap);
            onSide?.Invoke(LastGapCenterY >= py ? 1 : -1);
            float bx = enemy.transform.position.x;
            float reach = WallProjectileSpeed * EnemyProjectile.LifetimeSeconds;

            // 예고: 틈으로 나뉜 연속 줄 묶음마다 좌·우 띠. 두께 = 묶음 줄 간격 + 2×위험 반폭 → 띠 가장자리 = 실제 위험 경계.
            for (int block = 0; block < 2; block++)
            {
                int a = block == 0 ? 0 : gap + WallGapLanes;
                int b = block == 0 ? gap - 1 : WallLaneCount - 1;
                if (b < a) continue;
                float yMid = (LaneY(a) + LaneY(b)) * 0.5f;
                float thick = (LaneY(b) - LaneY(a)) + DangerHalfWidth * 2f;
                for (int side = -1; side <= 1; side += 2)
                    BandWarning(QueueWallArt, new Vector2(bx + side * reach * 0.5f, yMid), side > 0 ? 0f : 180f, reach, thick,
                        WallTelegraphSeconds, new Color(1f, 0.3f, 0.2f, 0.3f));
            }
            AudioService.PlaySfx("steam", new Vector2(bx, Balance.LaneCenterY));

            yield return new WaitForSeconds(WallTelegraphSeconds);
            if (!Alive) { ClearWarnings(); yield break; }
            ClearWarnings();

            float damage = enemy.ScaledAttack(WallDamage);
            AudioService.PlaySfx("shot_hybrid", new Vector2(bx, Balance.LaneCenterY));
            for (int lane = 0; lane < WallLaneCount; lane++)
            {
                if (lane >= gap && lane < gap + WallGapLanes) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (EnemyProjectile.ActiveCount >= MaxEnemyProjectilesForPattern) yield break; // 상한: 더 내지 않음
                    EnemyProjectile.Spawn(new Vector2(bx, LaneY(lane)), new Vector2(side, 0f), WallProjectileSpeed, damage,
                        game.Player, enemy.transform.parent, EnemyProjectile.ArtPrefix + "review_star", null,
                        DamageSource.BossSkill(enemy.Def, "영수증 벽"), EnemyProjectile.Tier.Boss);
                }
            }
        }
    }
}
