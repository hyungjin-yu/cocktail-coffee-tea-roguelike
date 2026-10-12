using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>10 G26 "재시작 10회"와 10 §2 "강화 보스 처치 후 사망". 실제 결과 화면 버튼(다시 시작)과 직업 선택 버튼
    /// 경로로 재시작한다. 피해 주입(TakeDamage)·보스 강제 소환은 테스트 고정 장치이며 정상 플레이 증거가 아니다.</summary>
    public class RunLifecycleStressTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_lifecycle_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            Assert.AreEqual(1, CountAll<AudioListener>(), "Boot must have one listener before a run starts");
            Assert.IsTrue(AudioService.Instance.GetComponent<AudioListener>().enabled);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            foreach (var v in Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            yield return null;
        }

        static bool ClickNamed(Component root, string name)
        {
            if (root == null) return false;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == name && b.gameObject.activeInHierarchy && b.interactable)
                {
                    b.onClick.Invoke();
                    return true;
                }
            return false;
        }

        static readonly (Job job, WeaponKind weapon)[] Combos =
        {
            (Job.Bartender, WeaponKind.Rapier), (Job.Bartender, WeaponKind.Gun),
            (Job.TeaSommelier, WeaponKind.Sword), (Job.TeaSommelier, WeaponKind.Bow),
        };

        /// <summary>결과 화면 "다시 시작" → 직업/무기 → "영업 시작"(→ 첫 안내가 뜨면 "시작"). 실제 버튼만 누른다.</summary>
        static IEnumerator RestartThroughButtons(Job job, WeaponKind weapon)
        {
            // 사망 종료는 쓰러지는 동작 뒤에 결과 패널이 열린다(GameManager.DeathResultDelaySeconds) — 사람처럼 열릴 때까지 기다린다.
            float deadline = Time.realtimeSinceStartup + GameManager.DeathResultDelaySeconds + 1f;
            while (GameManager.Instance != null && GameManager.Instance.ResultPending && Time.realtimeSinceStartup < deadline) yield return null;
            var result = Object.FindFirstObjectByType<ResultView>();
            Assert.IsTrue(result != null && result.IsOpen, "결과 화면이 열려 있어야 한다");
            Assert.IsTrue(ClickNamed(result, "RestartButton"), "결과 화면의 다시 시작 버튼");
            yield return null;
            yield return null;

            var select = Object.FindFirstObjectByType<CharacterSelectView>();
            Assert.IsNotNull(select, "다시 시작은 직업 선택 화면을 띄운다");
            (job == Job.Bartender ? select.BartenderButton : select.TeaSommelierButton).onClick.Invoke();
            yield return null;
            bool isA = weapon == WeaponKind.Rapier || weapon == WeaponKind.Sword;
            (isA ? select.WeaponButtonA : select.WeaponButtonB).onClick.Invoke();
            yield return null;
            select.StartButton.onClick.Invoke();
            yield return null;
            yield return null;
            var tutorial = Object.FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen)
            {
                ClickNamed(tutorial, "DismissButton");
                yield return null;
            }
            yield return null;
        }

        static int CountAll<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator BossDrops_FarOutsidePickupRadius_AreAttractedAndCollectedOnce()
        {
            var game = GameManager.Instance;
            var position = game.Player.Position + Vector2.right * 25f;
            Assert.Greater(25f, game.Player.PickupRadius);
            // Spawn real reward pickups through the boss-death path, without proximity collection.
            game.OnEnemyKilled(position, EnemyDef.GrinderGolem);
            game.OnEnemyKilled(position, EnemyDef.DripServerSpecter);
            game.OnEnemyKilled(position, EnemyDef.EspressoLeviathan);
            var drops = Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(0, game.BeanGauge.RawTotal, "Reward stays a physical pickup, not an immediate credit");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (game.BeanGauge.RawTotal < 80 && Time.realtimeSinceStartup < deadline)
            {
                // Collection legitimately opens the coffee tree and freezes physics until dismissed.
                if (game.State == GameState.SkillSelect) ClickNamed(game.SkillView, "CloseButton");
                yield return null;
            }
            Assert.AreEqual(80, game.BeanGauge.RawTotal, "20+20+40 boss beans must reach the player from outside pickup radius");
            yield return null; // Destroy is committed at the end of the collection frame.
            foreach (var drop in drops) Assert.IsTrue(drop == null, "Collected boss pickups are removed");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(80, game.BeanGauge.RawTotal, "No duplicate collection");
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator Restart10Times_ThroughResultButtons_NoLeakedRoots_Statics_Audio_OrHeapGrowth()
        {
            var heapAtInit = new List<long>();
            var runIds = new HashSet<string>();
            int expectedRuns = 0;

            for (int i = 0; i < 10; i++)
            {
                var game = GameManager.Instance;
                Assert.IsNotNull(game, $"재시작 {i}: GameManager가 있어야 한다");
                expectedRuns++;

                // ── 초기화 상태 검사(06/07/08 초기값, 중복 게임 루트·static 목록·오디오 누수 없음) ──
                Assert.AreEqual(GameState.Playing, game.State, $"재시작 {i}: Playing");
                Assert.AreEqual(1f, Time.timeScale, $"재시작 {i}: timeScale 복구");
                Assert.Less(game.RunTime, 0.5f, $"재시작 {i}: 런 시간 초기화");
                Assert.AreEqual(0, game.Kills, $"재시작 {i}: 처치 0");
                Assert.AreEqual(1, game.CocktailGauge.Level, $"재시작 {i}: 칵테일 Lv1");
                Assert.AreEqual(1, game.TeaGauge.Level, $"재시작 {i}: 차 Lv1");
                Assert.AreEqual(0, game.BeanGauge.RawTotal, $"재시작 {i}: 원두 0");
                Assert.AreEqual(0, game.Skills.UnlockedCount, $"재시작 {i}: 커피 노드 0");
                Assert.IsFalse(game.HybridUnlocked, $"재시작 {i}: 하이브리드 없음");
                Assert.AreEqual(100f, game.Player.MaxHp, 0.001f, $"재시작 {i}: 최대 HP 100(영구 강화 없음)");
                Assert.Less(Vector2.Distance(new Vector2(0f, -2f), game.Player.Position), 0.1f, $"재시작 {i}: 시작 위치 (0,-2)");
                Assert.IsNull(game.ActiveBoss, $"재시작 {i}: 보스 없음");
                Assert.IsTrue(runIds.Add(game.RunId), $"재시작 {i}: 새 runId");

                Assert.AreEqual(1, CountAll<GameManager>(), $"재시작 {i}: GameManager 1개");
                Assert.AreEqual(1, CountAll<PlayerController>(), $"재시작 {i}: 플레이어 1명");
                Assert.AreEqual(1, CountAll<AudioService>(), $"재시작 {i}: AudioService 1개");
                Assert.AreEqual(1, CountAll<AudioListener>(), $"재시작 {i}: AudioListener 1개");
                Assert.IsTrue(AudioService.Instance.GetComponent<AudioListener>().enabled);
                Assert.LessOrEqual(CountAll<Camera>(), 1, $"재시작 {i}: 카메라 중복 없음");
                Assert.AreEqual(1, CountAll<EventSystem>(), $"재시작 {i}: EventSystem 1개");
                Assert.AreEqual(0, CountAll<CharacterSelectView>(), $"재시작 {i}: 직업 선택 화면이 남지 않음");
                Assert.AreEqual(0, CountAll<IngredientPickup>(), $"재시작 {i}: 이전 런 픽업이 남지 않음");
                Assert.AreEqual(0, CountAll<EnemyProjectile>(), $"재시작 {i}: 이전 런 적 탄이 남지 않음");
                Assert.AreEqual(0, CountAll<BurstZone>(), $"재시작 {i}: 이전 런 장판이 남지 않음");
                foreach (var e in Enemy.Active)
                {
                    Assert.IsTrue(e != null, $"재시작 {i}: Enemy.Active에 파괴된 적이 남지 않음");
                    Assert.IsTrue(e.transform.IsChildOf(game.transform), $"재시작 {i}: Enemy.Active는 현재 런의 적만 가진다");
                }
                Assert.LessOrEqual(Enemy.Active.Count, 2, $"재시작 {i}: 초기 적 수(이전 런 적 누적 없음)");
                Assert.AreEqual(expectedRuns, PermanentProgress.Current.totalRuns, $"재시작 {i}: totalRuns 1회씩");

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
                heapAtInit.Add(System.GC.GetTotalMemory(true));

                // ── 짧게 실제로 플레이(스폰·처치·드랍·오디오) 후 사망/포기 번갈아 종료 ──
                float until = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < until && game.State == GameState.Playing) yield return null;
                if (game.State == GameState.Playing)
                {
                    if (i % 2 == 0) game.Player.TakeDamage(99999f);
                    else { game.OpenPause(); yield return null; game.GiveUp(); }
                }
                // 레벨업 카드가 먼저 열렸으면 사망 경로를 위해 카드를 고른다(이 테스트는 종료 경로만 본다).
                for (int guard = 0; guard < 30 && game.State != GameState.GameOver; guard++)
                {
                    if (game.State == GameState.LevelUp && game.CardView.IsOpen) game.CardView.Choose(0);
                    else if (game.State == GameState.Playing) game.Player.TakeDamage(99999f);
                    yield return null;
                }
                yield return null;
                Assert.AreEqual(GameState.GameOver, game.State, $"재시작 {i}: 종료");

                var next = Combos[(i + 1) % Combos.Length];
                yield return RestartThroughButtons(next.job, next.weapon);
                Assert.AreNotSame(game, GameManager.Instance, $"재시작 {i}: 새 GameManager");
                Assert.IsTrue(game == null, $"재시작 {i}: 이전 GameManager 파괴");
                Assert.AreEqual(next.weapon, GameManager.Instance.Weapon, $"재시작 {i}: 고른 무기로 시작");
            }

            var save = SaveSystem.Load(tempPath);
            Assert.AreEqual(11, save.totalRuns, "10회 재시작 + 처음 1런 = 11런 시작");
            Assert.AreEqual(5, save.failedRuns, "짝수 회차 사망 5회");
            Assert.AreEqual(5, save.abandonedRuns, "홀수 회차 포기 5회");
            Assert.AreEqual(0, save.clearedRuns);
            Assert.IsNotNull(save.activeRun, "11번째 런은 진행 중");

            double first3 = (heapAtInit[0] + heapAtInit[1] + heapAtInit[2]) / 3.0;
            double last3 = (heapAtInit[7] + heapAtInit[8] + heapAtInit[9]) / 3.0;
            Debug.Log($"[RunLifecycleStress] managed heap at init (bytes): {string.Join(",", heapAtInit)} first3={first3:0} last3={last3:0} ratio={last3 / first3:0.000}");
            Assert.LessOrEqual(last3, first3 * 1.10, "10 §3: 마지막 3회 managed heap 평균이 첫 3회보다 10% 이상 크면 안 된다");
        }

        [UnityTest]
        public IEnumerator EnhancedBossKill_ThenDeath_CommitsFailedOnce_KeepsSpecialtyOnce_NoRewardAfterDeath()
        {
            var game = GameManager.Instance;
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(4f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            game.ActiveBoss = boss;
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);
            boss.TakeDamage(999999f);
            yield return null;
            Assert.IsNull(game.ActiveBoss);
            Assert.AreEqual(Balance.SpecialtyDropAmount, SaveSystem.Load(tempPath).specialtyPoints, "처치 즉시 스페셜티 저장");

            // 사망 직전 같은 프레임에 재료 보상이 생겨도 사망이 우선(06 §6) — 원두 픽업을 그대로 둔 채 사망.
            game.Player.TakeDamage(99999f);
            yield return null;
            yield return null;
            Assert.IsTrue(game.ResultPending, "사망 직후에는 쓰러지는 동작을 보이는 중(결과 패널 대기)");
            Assert.AreEqual(1, SaveSystem.Load(tempPath).failedRuns, "사망 집계·저장은 결과 패널을 기다리지 않고 즉시 1회");
            yield return new WaitForSecondsRealtime(GameManager.DeathResultDelaySeconds + 0.2f);

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(game.Cleared);
            Assert.IsFalse(game.CardView.IsOpen, "죽은 뒤 카드 선택 없음");
            Assert.IsFalse(game.SkillView.IsOpen, "죽은 뒤 커피 트리 없음");
            var result = Object.FindFirstObjectByType<ResultView>();
            Assert.IsTrue(result != null && result.IsOpen, "사망 결과 화면");
            StringAssert.Contains("+" + Balance.SpecialtyDropAmount, result.StatsText, "결과에 이번 스페셜티 +3 표시");

            var afterDeath = SaveSystem.Load(tempPath);
            Assert.AreEqual(1, afterDeath.totalRuns);
            Assert.AreEqual(1, afterDeath.failedRuns, "사망 1회 집계");
            Assert.AreEqual(0, afterDeath.clearedRuns);
            Assert.AreEqual(0, afterDeath.abandonedRuns);
            Assert.AreEqual(Balance.SpecialtyDropAmount, afterDeath.specialtyPoints, "스페셜티는 한 번만(사망으로 잃거나 두 번 받지 않음)");
            Assert.IsNull(afterDeath.activeRun, "종료 후 activeRun 없음");

            yield return RestartThroughButtons(Job.Bartender, WeaponKind.Gun);
            var after = SaveSystem.Load(tempPath);
            Assert.AreEqual(2, after.totalRuns, "다시 시작은 새 런 1회");
            Assert.AreEqual(1, after.failedRuns, "다시 시작이 사망 런을 포기로 다시 세지 않음");
            Assert.AreEqual(0, after.abandonedRuns);
            Assert.AreEqual(Balance.SpecialtyDropAmount, after.specialtyPoints, "재시작 뒤에도 스페셜티 3 유지");
            Assert.AreEqual(Balance.SpecialtyDropAmount, PermanentProgress.Current.specialtyPoints);
        }
    }
}
