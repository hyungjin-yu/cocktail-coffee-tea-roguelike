using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-24 Beverage Master #24 — 최종 보스 신규 패턴: "영수증 저격"(조준 원거리)과 최종 단계 "폐점 클레임 러시".
    /// 바텐더·찻집 최종 보스는 같은 드라이버라 두 직업 모두 돌린다.
    /// </summary>
    public class FinalBossAimAndRushTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        static GameManager Quiet(Job job)
        {
            var game = GameBootstrap.Build(job, job == Job.TeaSommelier ? WeaponKind.Bow : WeaponKind.Gun);
            foreach (var mb in game.Player.GetComponents<MonoBehaviour>())
                if (mb is PlayerProjectileWeapon || mb is RapierWeapon) Object.Destroy(mb);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            return game;
        }

        static EnemyDef FinalDef(Job job) => job == Job.TeaSommelier ? EnemyDef.TeaCustomer : EnemyDef.DifficultCustomer;

        static IEnumerator SpawnBoss(GameManager game, Job job, Vector2 offset, float hpFraction, bool autoPatterns,
            System.Action<Enemy, CustomerBossDriver> got)
        {
            game.Player.transform.position = new Vector3(0f, Balance.LaneCenterY, 0f);
            var boss = Enemy.SpawnAt(game.Player.Position + offset, FinalDef(job), 1f, game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<CustomerBossDriver>();
            driver.Init(boss, game);
            driver.enabled = autoPatterns;
            game.ActiveBoss = boss;
            if (hpFraction < 1f) { boss.Invulnerable = false; boss.TakeDamage(boss.Hp - boss.MaxHp * hpFraction); } // 등장 무적 중 피해가 무시되어 HP가 그대로였다(통합 검수 발견)
            got(boss, driver);
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);
        }

        static void Invoke(CustomerBossDriver driver, string method, params object[] args)
        {
            var routine = (IEnumerator)typeof(CustomerBossDriver).GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(driver, args);
            driver.StartCoroutine(routine);
        }

        [UnityTest]
        public IEnumerator Snipe_TracksThenLocks_SidestepAvoids_FromFar([Values(Job.Bartender, Job.TeaSommelier)] Job job)
        {
            var game = Quiet(job);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, job, new Vector2(9f, 0f), 1f, false, (b, d) => { boss = b; driver = d; });

            Invoke(driver, "Snipe", false);
            // 조준 추적 중 플레이어가 움직이면 선이 따라온다(최초 노출 1.2초).
            yield return new WaitForSeconds(0.5f);
            game.Player.transform.position += new Vector3(0f, 1f, 0f);
            yield return new WaitForSeconds(CustomerBossDriver.SnipeFirstTrackSeconds - 0.5f + 0.1f); // 고정 직후
            Vector2 toPlayer = (game.Player.Position - driver.LastSnipeOrigin).normalized;
            Assert.Greater(Vector2.Dot(toPlayer, driver.LastSnipeDirection), 0.995f, "고정 방향 = 고정 시점 플레이어 쪽(추적 반영)");
            Assert.AreEqual(0, driver.SnipeVolleys, "고정 중에는 아직 발사 전");

            // 고정 뒤 옆으로 위험 반폭(0.62)보다 조금 더 비키면 안전.
            Vector2 d = driver.LastSnipeDirection;
            Vector2 perp = new Vector2(-d.y, d.x);
            if (game.Player.Position.y + perp.y * 1.2f > Balance.DepthMax - 0.3f) perp = -perp;
            game.Player.transform.position += (Vector3)(perp * 1.2f);
            float hp = game.Player.Hp;
            yield return new WaitForSeconds(CustomerBossDriver.SnipeLockSeconds + 1.2f);
            Assert.AreEqual(1, driver.SnipeVolleys, "발사 1회");
            Assert.AreEqual(hp, game.Player.Hp, 0.01f, "고정 뒤 옆으로 비키면 피해 없음(재조준 없음)");
        }

        [UnityTest]
        public IEnumerator Snipe_StandingOnLockedLine_IsHit_AndRangeCoversScreen()
        {
            var game = Quiet(Job.Bartender);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, Job.Bartender, new Vector2(16f, 0f), 1f, false, (b, d) => { boss = b; driver = d; });
            float hp = game.Player.Hp;
            Invoke(driver, "Snipe", false);
            yield return new WaitForSeconds(CustomerBossDriver.SnipeFirstTrackSeconds + CustomerBossDriver.SnipeLockSeconds + 1.6f);
            Assert.Less(game.Player.Hp, hp, "16 거리(화면 폭 밖 근처)에서도 제자리면 맞는다");
        }

        [UnityTest]
        public IEnumerator Snipe_Enraged_FiresTwoVolleys_WithGap()
        {
            var game = Quiet(Job.Bartender);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, Job.Bartender, new Vector2(8f, 0f), 0.49f, false, (b, d) => { boss = b; driver = d; });
            Invoke(driver, "Snipe", true);
            yield return new WaitForSeconds(4f);
            Assert.AreEqual(2, driver.SnipeVolleys, "분노(후반 강화): 연속 2회");
        }

        [UnityTest]
        public IEnumerator Rush_WallLeavesGap_StandingInGapIsSafe_InLaneIsHit([Values(Job.Bartender, Job.TeaSommelier)] Job job)
        {
            var game = Quiet(job);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, job, new Vector2(6f, 0f), 0.29f, false, (b, d) => { boss = b; driver = d; });
            int before = EnemyProjectile.ActiveCount;
            Invoke(driver, "ClosingRush");
            yield return null;
            float gapY = driver.LastGapCenterY;
            Assert.Greater(Mathf.Abs(gapY - game.Player.Position.y), 0.88f, "제자리는 통로가 아니다(이동 요구)");

            // 예고 중 통로 중심으로 이동 → 벽 통과 후 무피해.
            game.Player.transform.position = new Vector3(game.Player.Position.x, gapY, 0f);
            float hp = game.Player.Hp;
            yield return new WaitForSeconds(CustomerBossDriver.WallTelegraphSeconds + 0.05f);
            var shots = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
            Assert.AreEqual((CustomerBossDriver.WallLaneCount - CustomerBossDriver.WallGapLanes) * 2, EnemyProjectile.ActiveCount - before, "벽 탄 = (8-2)줄 × 좌우");
            Assert.IsTrue(shots.All(s => Mathf.Abs(s.transform.position.y - gapY) >= CustomerBossDriver.WallLaneSpacing * 1.5f - 0.01f), "통로 두 줄에는 탄이 없다");
            yield return new WaitForSeconds(1.0f); // 6 거리 / 속도 7 → 통과
            Assert.AreEqual(hp, game.Player.Hp, 0.01f, "통로 안은 안전");
        }

        [UnityTest]
        public IEnumerator Rush_StayingInLane_IsHit()
        {
            var game = Quiet(Job.Bartender);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, Job.Bartender, new Vector2(6f, 0f), 0.29f, false, (b, d) => { boss = b; driver = d; });
            float hp = game.Player.Hp;
            Invoke(driver, "ClosingRush");
            yield return new WaitForSeconds(CustomerBossDriver.WallTelegraphSeconds + 1.1f);
            Assert.Less(game.Player.Hp, hp, "벽 줄 위에 서 있으면 맞는다");
        }

        [UnityTest]
        public IEnumerator FinalPhase_At30Percent_RushFirst_ThenNoOverlapUntilRecovered()
        {
            var game = Quiet(Job.Bartender);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, Job.Bartender, new Vector2(7f, 0f), 0.29f, true, (b, d) => { boss = b; driver = d; });
            float t0 = Time.time;
            while (driver.RushCount == 0 && Time.time - t0 < 5f)
            {
                if (game.Player.Hp < game.Player.MaxHp * 0.5f) game.Player.Heal(1f);
                yield return null;
            }
            Assert.IsTrue(driver.InFinalPhase, $"HP 30% 이하 → 최종 단계 (state={game.State} arrived={boss.HasArrived} hp={boss.Hp}/{boss.MaxHp} enabled={driver.enabled} dead={boss.IsDead} ts={Time.timeScale})");
            Assert.AreEqual(1, driver.RushCount, "최종 단계 첫 패턴은 러시");
            float started = Time.time;
            // 러시는 벽1 1.0 + 여유 1.2 + 저격 ~1.2 + 여유 1.0 + 벽2 1.0 + 회복 2.0 ≈ 7.4초 — 그동안 다음 패턴이 시작되지 않는다.
            yield return new WaitForSeconds(6.5f);
            Assert.IsTrue(driver.Busy, "러시 진행 중(회복 포함)");
            int volleys = driver.SnipeVolleys;
            Assert.AreEqual(1, volleys, "러시 안 저격 1회");
            while (driver.Busy && Time.time - started < 12f) yield return null;
            Assert.IsFalse(driver.Busy, "러시가 끝나면 다음 패턴 대기");
            Assert.Less(Time.time - started, 9f, "러시 총 길이 상한");
        }

        [UnityTest]
        public IEnumerator BossDeath_MidAim_ClearsTelegraphs_NoShot()
        {
            var game = Quiet(Job.Bartender);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, Job.Bartender, new Vector2(8f, 0f), 1f, false, (b, d) => { boss = b; driver = d; });
            int before = EnemyProjectile.ActiveCount;
            Invoke(driver, "Snipe", false);
            yield return new WaitForSeconds(0.4f);
            boss.TakeDamage(999999f); // 최종 보스 사망 = 런 종료(timeScale 0) — 실시간으로 기다린다
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(0, driver == null ? 0 : driver.SnipeVolleys, "사망 뒤 발사 없음");
            Assert.AreEqual(before, EnemyProjectile.ActiveCount, "탄 생성 없음");
        }

        [UnityTest]
        public IEnumerator Pause_FreezesAimProgress()
        {
            var game = Quiet(Job.Bartender);
            Enemy boss = null; CustomerBossDriver driver = null;
            yield return SpawnBoss(game, Job.Bartender, new Vector2(8f, 0f), 1f, false, (b, d) => { boss = b; driver = d; });
            Invoke(driver, "Snipe", false);
            yield return new WaitForSeconds(0.3f);
            Time.timeScale = 0f; // 일시정지·보상창과 같은 조건
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(0, driver.SnipeVolleys, "정지 중에는 조준·발사가 진행되지 않는다");
            Time.timeScale = 1f;
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(1, driver.SnipeVolleys, "재개 후 정상 발사");
        }
    }
}
