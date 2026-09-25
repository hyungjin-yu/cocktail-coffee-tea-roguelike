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
            // (딜레이스 때문에 압력이 0으로 리셋되지만, 그 뒤로 100 차는 데 100/13 ≈ 7.7초)
            boss.TakeDamage(boss.MaxHp * 0.75f);

            float hpBefore = game.Player.Hp;
            Vector2 posBefore = game.Player.Position;

            float deadline = Time.time + 9f;
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
    }
}
