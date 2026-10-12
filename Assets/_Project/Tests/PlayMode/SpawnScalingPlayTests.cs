using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>40(2026-10-12): 실제 런에서 영구 해금 총수 N이 런 시작 시 고정되고, 일반 스폰/웨이브 상한과 보스 포함 160을 지키는지.</summary>
    public class SpawnScalingPlayTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_spawnscale_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(tempPath + suffix)) File.Delete(tempPath + suffix);
        }

        static void UnlockAll(int count)
        {
            foreach (var n in PermanentSkillDatabase.LoadAll().Take(count)) PermanentProgress.UnlockPermanentSkill(n.id);
        }

        [UnityTest]
        public IEnumerator ActualRegularSpawns_IncreaseAtTenAndFifteenMinutes_WithSameUnlocks()
        {
            var game = Quiet(true);
            yield return null;
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            int before = spawner.RegularSpawns;
            yield return new WaitForSeconds(4f);
            int early = spawner.RegularSpawns - before;
            game.SetRunTimeForTests(600f);
            yield return new WaitForSeconds(1.3f);
            before = spawner.RegularSpawns;
            yield return new WaitForSeconds(4f);
            int ten = spawner.RegularSpawns - before;
            game.SetRunTimeForTests(900f);
            yield return new WaitForSeconds(.4f);
            before = spawner.RegularSpawns;
            yield return new WaitForSeconds(4f);
            int fifteen = spawner.RegularSpawns - before;
            Assert.Greater(early, 0);
            Assert.Greater(ten, early * 2, "같은 영구 해금 수: 10분의 실제 생성량 증가");
            Assert.Greater(fifteen, ten, "10분 이후에도 실제 생성량 증가");
            Assert.LessOrEqual(Enemy.Active.Count, 160);
            Debug.Log($"[TimeSpawn] same unlocks, 4s real spawns: start={early}, 10m={ten}, 15m={fifteen}");
        }

        static GameManager Quiet(bool keepSpawner = false)
        {
            var game = GameBootstrap.Build();
            foreach (var mb in game.Player.GetComponents<MonoBehaviour>())
                if (mb is PlayerProjectileWeapon || mb is RapierWeapon) Object.Destroy(mb);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = keepSpawner;
            if (game.Director != null) game.Director.enabled = false;
            return game;
        }

        [UnityTest]
        public IEnumerator UnlockTotal_FixedAtRunStart_NextRunPicksUpNewUnlock()
        {
            var game = Quiet();
            yield return null;
            Assert.AreEqual(0, game.Challenge.PermanentUnlockTotal);
            Assert.AreEqual(100, game.Challenge.RegularAliveCap, "보유0 = 기존 100");

            var first = PermanentSkillDatabase.LoadAll()[0];
            Assert.AreNotEqual(PermanentPurchaseResult.Purchased,
                PermanentProgress.TryPurchasePermanentSkill(first, PermanentSkillDatabase.LoadAll()), "런 중 구매 불가");
            Assert.AreEqual(0, game.Challenge.PermanentUnlockTotal);

            Object.Destroy(game.gameObject);
            yield return null;
            UnlockAll(24);
            game = Quiet();
            yield return null;
            Assert.AreEqual(24, game.Challenge.PermanentUnlockTotal, "다음 런에 반영");
            Assert.AreEqual(160, game.Challenge.RegularAliveCap);
            Assert.AreEqual(RunChallenge.HpFor(game.PermanentSkills.AppliedCount(game.Weapon), 0), game.Challenge.HpMultiplier, 0f,
                "33 체력 배율은 P 기준 그대로");
        }

        [UnityTest]
        public IEnumerator N0_RegularAndWaveStopAt100()
        {
            var game = Quiet();
            yield return null;
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            for (int i = 0; i < 300; i++) spawner.SpawnOneForTests();
            Assert.AreEqual(100, Enemy.Active.Count);
            spawner.SpawnWaveForTests();
            Assert.AreEqual(100, Enemy.Active.Count);
        }

        [UnityTest]
        public IEnumerator N24_RegularReserveBossSlot_BossOverlapNeverExceeds160()
        {
            UnlockAll(24);
            var game = Quiet();
            yield return null;
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            int waveBefore = spawner.WaveSpawns;
            spawner.SpawnWaveForTests();
            Assert.AreEqual(Balance.WaveCountPerSide * 2, spawner.WaveSpawns - waveBefore, "웨이브 한 번의 마리 수는 N과 무관");

            for (int i = 0; i < 400; i++) spawner.SpawnOneForTests();
            Assert.AreEqual(Balance.MaxAliveEnemiesAbsolute - 1, Enemy.Active.Count, "보스 1자리 예약");
            spawner.SpawnWaveForTests();
            Assert.AreEqual(Balance.MaxAliveEnemiesAbsolute - 1, Enemy.Active.Count);

            game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f });
            Assert.AreEqual(Balance.MaxAliveEnemiesAbsolute, Enemy.Active.Count, "보스 등장 시각/등장은 막지 않는다");
            for (int i = 0; i < 50; i++) spawner.SpawnOneForTests();
            Assert.AreEqual(Balance.MaxAliveEnemiesAbsolute, Enemy.Active.Count, "보스 포함 전체 160 유지");
        }

        [UnityTest]
        public IEnumerator MeasuredRegularSpawns_N24_About60PercentMoreThanN0()
        {
            var game = Quiet(true);
            yield return null;
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            int s0 = spawner.RegularSpawns; float t0 = Time.time;
            while (Time.time - t0 < 9f) yield return null;
            int n0 = spawner.RegularSpawns - s0;

            Object.Destroy(game.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            yield return null;
            UnlockAll(24);
            game = Quiet(true);
            yield return null;
            spawner = game.GetComponentInChildren<EnemySpawner>();
            s0 = spawner.RegularSpawns; t0 = Time.time;
            while (Time.time - t0 < 9f) yield return null;
            int n24 = spawner.RegularSpawns - s0;

            Debug.Log($"[SpawnScaling] 9s regular spawns N0={n0} N24={n24} ratio={(float)n24 / Mathf.Max(1, n0):0.00}");
            Assert.GreaterOrEqual(n0, 6);
            Assert.That((float)n24 / n0, Is.InRange(1.35f, 1.85f));
        }
    }
}
