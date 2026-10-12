using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>강화 보스(에스프레소 머신 리바이어던, EspressoLeviathanDriver) — 압력 게이지 패턴 중심.</summary>
    public class EspressoLeviathanDriverTests
    {
        static void DismissPopups(GameManager game)
        {
            if (game.State == GameState.LevelUp)
                game.CardView.Choose(0);
            else if (game.State == GameState.SkillSelect)
                game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            var game = GameBootstrap.Build();
            Assert.IsNotNull(game);
            // 기본 무기(레이피어)가 가까이 스폰된 보스를 자동 공격하면 압력 게이지 딜레이스가 계속 발동돼서
            // 이 클래스의 시간 기반 압력 게이지 시나리오가 불안정해진다 — 여기선 보스 패턴만 격리해서 본다.
            var weapon = game.Player.GetComponent<RapierWeapon>();
            if (weapon != null) Object.Destroy(weapon);
            // 무기 없이 보스·일반 몹 사이에 서 있으면 ~8초 안에 죽는다(2026-10-05 PlayMode 재현: t≈9 GameOver).
            // GameOver는 timeScale=0이라 WaitForSeconds가 영원히 안 끝나고, UTF 180초 타임아웃도 코루틴이
            // 양보하지 않아 걸리지 않아 러너 전체가 멈췄다. 이 클래스는 보스 패턴만 보므로 사망을 막는다
            // (기존 폭발 테스트와 같은 방식, 단언은 그대로).
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pressure_FillsOverTime_AtBaseRate()
        {
            var game = GameManager.Instance;
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(4f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);

            // 06:24 — 등장 안내 1초 동안은 압력도 차지 않는다. 그 뒤로 1초치(기본 6/초)를 관찰한다.
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 1f);

            Assert.Greater(driver.Pressure, 4f, "압력 게이지는 시간이 지나면 저절로 차야 한다 (기본 6/초)");
            Assert.Less(driver.Pressure, 8f);
        }

        [UnityTest]
        public IEnumerator DamageToBoss_RelievesPressure()
        {
            var game = GameManager.Instance;
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(4f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);

            // 06:24 — 등장 안내(무적) 1초를 넘겨야 피해도 들어가고 압력도 쌓인다.
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 1f); // 압력이 어느 정도 쌓일 시간

            boss.TakeDamage(50f); // 딜레이스: 피해 1당 압력 -0.15
            Assert.AreEqual(0f, driver.Pressure, 0.01f, "50 피해(-7.5)는 1초치 압력(~6)보다 커서 0까지 떨어져야 한다");
        }

        [UnityTest]
        public IEnumerator PressureGauge_Explosion_DamagesAndKnocksBackPlayer()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); // 접촉/폭발 피해로 죽어서 대기가 멈추지 않게

            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(1f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);

            // 06:24 — 등장 안내(무적) 1초를 넘겨야 피해가 들어간다.
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);

            // HP 25%까지 미리 깎아서 압력 축적 속도를 30% 페이즈(13/초)로 끌어올려 테스트 시간을 줄인다.
            // (딜레이스 때문에 압력이 0으로 리셋되지만, 그 뒤로 100 차는 데 100/13 ≈ 7.7초 + 폭발 예고 0.6초)
            boss.TakeDamage(boss.MaxHp * 0.75f);

            float hpBefore = game.Player.Hp;
            Vector2 posBefore = game.Player.Position;

            float deadline = Time.time + 10f;
            while (Time.time < deadline)
            {
                DismissPopups(game);
                yield return null;
            }

            Assert.Less(game.Player.Hp, hpBefore, "압력 게이지가 차면 광역 피해를 줘야 한다");
            // 이동 입력이 전혀 없으므로, 위치가 눈에 띄게 바뀌었다면 넉백이 실제로 작동했다는 뜻
            Assert.Greater(Vector2.Distance(posBefore, game.Player.Position), 0.2f,
                "압력 게이지 폭발은 플레이어를 밀어내야 한다 (입력 없이는 다른 이동 수단이 없음)");
        }

        [UnityTest]
        public IEnumerator SteamBeam_FreezesMovement_DuringTelegraph()
        {
            var game = GameManager.Instance;
            // 플레이어를 멀리 둬서 얼려있지 않다면 계속 추적(속도>0)하게 만든다
            game.Player.transform.position = new Vector3(8f, game.Player.Position.y, 0f);

            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(-6f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);
            var rb = boss.GetComponent<Rigidbody2D>();

            // 등장 안내(1초) + 첫 지연(3초)을 넘겨 증기 예고(0.8초) 구간으로 진입
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 3f + 0.3f);

            // 2026-10-06 play4: 정확한 Vector2 비교가 간헐 실패("Expected (0.00, 0.00) But was (0.00, 0.00)"). Enemy.FixedUpdate가
            // 매 스텝 0으로 세팅한 뒤 물리 스텝이 웨이브 적과의 접촉을 풀며 아주 작은 잔여 속도를 남긴다. 멈춤 의도는 드라이버가
            // 내는 속도로 정확히 검사하고, 실제 강체 속도는 추적 속도보다 한참 작은지(접촉 잔여만 허용) 검사한다.
            Assert.IsTrue(driver.TryGetVelocity(rb.position, game.Player.Position, out var driven),
                "06:127 — 증기 분사 시전(예고 포함) 중에는 드라이버가 이동을 직접 멈춰야 한다");
            Assert.AreEqual(Vector2.zero, driven, "멈춤 중 드라이버 속도는 정확히 0");
            Assert.Less(rb.linearVelocity.magnitude, 0.05f,
                $"06:127 — 증기 분사 시전 중에는 제자리에 멈춰야 한다(추적 속도 {EnemyDef.EspressoLeviathan.speed}, 실제 {rb.linearVelocity.magnitude:F5})");
        }

        [UnityTest]
        public IEnumerator DripRain_DoesNotSpawnZones_BeforeTelegraphElapses()
        {
            var game = GameManager.Instance;
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(4f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);

            // 첫 패턴은 증기(패턴0), 두 번째가 드립 폭우(패턴1) — 쿨타임(6.5초) 뒤.
            // 등장 안내(1) + 첫 지연(3) + 쿨타임(6.5) 직후, 드립 폭우 예고(0.6초) "안"에 관찰한다.
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 3f + 6.5f + 0.2f);

            Assert.IsNull(Object.FindFirstObjectByType<BurstZone>(),
                "06:128 — 예고(0.6초)가 끝나기 전에는 장판이 생기면 안 된다");
        }
    }
}
