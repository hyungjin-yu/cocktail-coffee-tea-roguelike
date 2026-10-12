using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-10-12 찻집 보스(개완 수호자·김 서린 주전자 장로·차 화로 용·까다로운 차 손님): 실제 BossDirector 경로의
    /// 드라이버·아트·보상, 그리고 차시 궁수의 실제 물리 이동 중 move 클립 재생·조준 방향.</summary>
    public class TeaEncounterBossTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_tea_enc_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            game.GetComponentInChildren<EnemySpawner>().enabled = false;
            game.Director.enabled = false;
            game.Player.AutoInputOverride = Vector2.zero;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            foreach (var s in new[] { "", ".bak", ".tmp" }) if (File.Exists(tempPath + s)) File.Delete(tempPath + s);
        }

        static BossDirector.Spec TeaSpec(int i) => BossDirector.DefaultSchedule(Job.TeaSommelier)[i];

        IEnumerator Kill(Enemy boss)
        {
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);
            boss.TakeDamage(999999f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeaBosses_UseMatchingDrivers_AndNewBodyArt()
        {
            var game = GameManager.Instance;
            var expect = new[] { typeof(ChargerDriver), typeof(DripSpecterDriver), typeof(EspressoLeviathanDriver), typeof(CustomerBossDriver) };
            var clips = new[] { new[] { "charge_prepare", "charge" }, new[] { "drip", "vanish", "appear" },
                new[] { "steam", "rain", "pressure" }, new[] { "refund", "summon", "review", "reject" } };
            for (int i = 0; i < 4; i++)
            {
                var spec = TeaSpec(i);
                var boss = game.Director.SpawnNow(spec);
                yield return null;
                Assert.AreSame(boss, game.ActiveBoss, spec.def.name);
                Assert.AreSame(spec.def, boss.Def);
                Assert.IsNotNull(boss.GetComponent(expect[i]), $"{spec.def.name} → {expect[i].Name}");
                Assert.AreEqual(1, boss.GetComponents<MonoBehaviour>().Count(m => m is IEnemyDriver), "드라이버 1개");
                Assert.IsTrue(boss.UsesBodyArt, spec.def.name + " 몸체 아트");
                var art = ArtLibrary.Get(spec.def.ArtId);
                foreach (var c in clips[i]) Assert.IsNotNull(art.Clip(c), $"{spec.def.ArtId} 클립 {c}");
                Object.Destroy(boss.gameObject);
                game.ActiveBoss = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator GaiwanSentinel_Kill_DropsBeans_HealsAndContinues()
        {
            var game = GameManager.Instance;
            game.Player.TakeDamage(50f);
            float hp = game.Player.Hp;
            var boss = game.Director.SpawnNow(TeaSpec(0));
            yield return Kill(boss);
            Assert.IsNull(game.ActiveBoss);
            Assert.Greater(game.Player.Hp, hp, "중간보스 처치 회복");
            Assert.AreEqual(EnemyDef.GaiwanSentinel.dropCount, Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(0, PermanentProgress.Current.specialtyPoints, "중간보스는 스페셜티 없음");
        }

        [UnityTest]
        public IEnumerator SteamKettleElder_Kill_IsMidBossReward_NoSpecialty()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(TeaSpec(1));
            yield return Kill(boss);
            Assert.IsNull(game.ActiveBoss);
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(EnemyDef.SteamKettleElder.dropCount, Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(0, PermanentProgress.Current.specialtyPoints);
        }

        [UnityTest]
        public IEnumerator TeaKilnDragon_Kill_GrantsSpecialtyOnce_Saved()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(TeaSpec(2));
            yield return Kill(boss);
            Assert.IsNull(game.ActiveBoss);
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(Balance.SpecialtyDropAmount, PermanentProgress.Current.specialtyPoints, "강화 보스 스페셜티");
            Assert.AreEqual(Balance.SpecialtyDropAmount, SaveSystem.Load(tempPath).specialtyPoints, "즉시 저장");
        }

        [UnityTest]
        public IEnumerator TeaCustomer_Kill_ClearsRun()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(TeaSpec(3));
            yield return Kill(boss);
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsTrue(game.Cleared);
        }

        [UnityTest]
        public IEnumerator TeaBossZones_UseTeaPoolArt()
        {
            Assert.AreEqual("zone_tea_pool_v2", BurstZone.V2For(BurstZone.TeaPoolArt));
            Assert.IsNotNull(Resources.LoadAll<Sprite>("ArtExtra/zone_tea_pool_v2").FirstOrDefault(), "찻물 장판 v2 스프라이트");
            yield return null;
        }

        static IEnumerator RunPattern(Enemy boss, string method, params object[] args)
        {
            var driver = boss.GetComponent<CustomerBossDriver>();
            driver.enabled = false; // 자동 패턴 순환 없이 해당 패턴만
            var routine = (IEnumerator)typeof(CustomerBossDriver).GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(driver, args);
            driver.StartCoroutine(routine);
            yield return null;
        }

        static int ZoneCount(string key) => Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Count(sr => sr.sprite != null && sr.enabled && sr.sprite.name.Contains(key));

        [UnityTest]
        public IEnumerator TeaCustomer_SpellBlockFallback_UsesTeaPool()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(TeaSpec(3));
            boss.transform.position = game.Player.Position + new Vector2(3f, 0f);
            yield return RunPattern(boss, "SpellBlock", false);
            yield return new WaitForSeconds(2.0f); // 거부 0.8초 + 장판 예고 0.8초
            Assert.Greater(ZoneCount("tea_pool"), 0, "주기 스킬 없을 때 대체 장판은 찻물");
            Assert.AreEqual(0, ZoneCount("coffee_pool"), "찻집에서 커피 장판 없음");
        }

        [UnityTest]
        public IEnumerator Refund_Inside_ShowsHudTimer_Outside_NoSuppression()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(TeaSpec(3));
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(1f); // 추적 접촉으로 죽지 않게
            boss.transform.position = game.Player.Position + new Vector2(2f, 0f);
            yield return RunPattern(boss, "Refund");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(game.Stats.BonusesSuppressed, "원 안: 강화 무효");
            yield return null;
            Assert.IsTrue(game.Hud.SuppressedStatusVisible, "무효 상태 바탕 표시");
            StringAssert.Contains("초", game.Hud.SuppressedText, "남은 시간 표시");

            yield return new WaitForSeconds(5.2f);
            Assert.IsFalse(game.Stats.BonusesSuppressed);
            yield return null;
            Assert.IsFalse(game.Hud.SuppressedStatusVisible, "끝나면 바탕 숨김");

            boss.transform.position = game.Player.Position + new Vector2(8f, 0f);
            yield return RunPattern(boss, "Refund");
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(game.Stats.BonusesSuppressed, "원 밖: 무효 없음(수치 무변경)");
        }

        [UnityTest]
        public IEnumerator BambooScoopArcher_RealMovement_PlaysMoveClip_FacesPlayer_ThenIdles()
        {
            var game = GameManager.Instance;
            var archer = Enemy.SpawnAt(game.Player.Position + Vector2.right * 1.2f, EnemyDef.BambooScoopArcher, 1f, game.Player, game.transform);
            EnemyRoster.AttachBehavior(archer, game, game.Player); // 스포너와 같은 경로(RangedAttackDriver)
            Assert.IsTrue(archer.UsesBodyArt, "차시 궁수 PixelLab 몸체");
            float startDist = Vector2.Distance(archer.transform.position, game.Player.Position);
            int moveFrames = 0, facingWrong = 0, frameAdvances = 0, lastFrame = -1;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                yield return null;
                var cur = archer.Animator.Current;
                if (cur != null && cur.name == "move")
                {
                    moveFrames++;
                    if (lastFrame >= 0 && archer.Animator.FrameIndex != lastFrame) frameAdvances++;
                    lastFrame = archer.Animator.FrameIndex;
                }
                float toPlayer = game.Player.Position.x - archer.transform.position.x;
                if (Mathf.Abs(toPlayer) > 0.1f && Mathf.Sign(archer.Animator.transform.localScale.x) != Mathf.Sign(toPlayer)) facingWrong++;
            }
            float dist = Vector2.Distance(archer.transform.position, game.Player.Position);
            Assert.Greater(dist, startDist + 0.5f, "가까우면 실제로 물러남");
            Assert.Greater(moveFrames, 10, "물러나는 동안 move 클립");
            Assert.Greater(frameAdvances, 2, "move 프레임이 실제로 진행");
            Assert.AreEqual(0, facingWrong, "물러나도 플레이어를 바라봄");

            // 유지 거리 안에서 정지하면 idle로 안정(이동/정지 클립 반복 전환 없음).
            float settleEnd = Time.time + 6f;
            while (Time.time < settleEnd && archer.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude > 0.0025f) yield return null;
            int switches = 0; string last = archer.Animator.Current?.name;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                yield return null;
                var n = archer.Animator.Current?.name;
                if (n != last && (n == "move" || n == "idle") && (last == "move" || last == "idle")) switches++;
                last = n;
            }
            Assert.LessOrEqual(switches, 1, "정지 시 move/idle 깜박임 없음");
        }
    }
}
