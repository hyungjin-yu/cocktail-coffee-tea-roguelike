using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    public class EnemyBombTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard; // 로컬 PlayerPrefs 값과 무관하게 결정론적으로
            Assert.IsNotNull(GameBootstrap.Build());
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
        public IEnumerator OliveBomber_ExplodesOnContact_DealsDamageOnce_AndSelfDestructs()
        {
            var game = GameManager.Instance;
            float hpBefore = game.Player.Hp;

            var pos = game.Player.Position + new Vector2(0.1f, 0f); // 접촉 거리 안에서 스폰
            var bomber = Enemy.SpawnAt(pos, EnemyDef.OliveBomber, 1f, game.Player, game.transform);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.AreEqual(hpBefore - EnemyDef.OliveBomber.contactDamage, game.Player.Hp, 0.5f,
                "폭발 데미지 한 번만 들어가야 한다 (반복 접촉 데미지 아님)");
            Assert.IsFalse(Enemy.Active.Contains(bomber), "터지면 즉시 자폭해서 사라져야 한다");
            Assert.IsNotNull(Object.FindFirstObjectByType<BurstZone>(), "자폭 자리에 화염 장판이 남아야 한다");
        }

        [UnityTest]
        public IEnumerator BurstZone_DamagesPlayerOverTime_ThenExpires()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 500f)); // 테스트 도중 죽지 않게
            float hpBefore = game.Player.Hp;

            BurstZone.Spawn(game.Player.Position, 1.5f, 10f, 1.2f, game.Player, game.transform);

            yield return new WaitForSeconds(1.4f); // 장판 지속시간(1.2초)을 넘기기 충분히

            Assert.Less(game.Player.Hp, hpBefore, "장판 범위 안에 있으면 지속 피해를 입어야 한다");
            Assert.IsNull(Object.FindFirstObjectByType<BurstZone>(), "지속시간이 끝나면 장판이 사라져야 한다");
        }

        [UnityTest]
        public IEnumerator SugarCubeBomber_ExplodesOnContact_LeavesSlowZone_NotBurstZone()
        {
            var game = GameManager.Instance;
            float hpBefore = game.Player.Hp;

            var pos = game.Player.Position + new Vector2(0.1f, 0f);
            var bomber = Enemy.SpawnAt(pos, EnemyDef.SugarCubeBomber, 1f, game.Player, game.transform);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.AreEqual(hpBefore - EnemyDef.SugarCubeBomber.contactDamage, game.Player.Hp, 0.5f);
            Assert.IsFalse(Enemy.Active.Contains(bomber));
            Assert.IsNotNull(Object.FindFirstObjectByType<SlowZone>(), "슈가 큐브 바머는 피해 장판 대신 둔화 장판을 남겨야 한다");
            Assert.IsNull(Object.FindFirstObjectByType<BurstZone>(), "슈가 큐브 바머는 화염 장판을 남기면 안 된다");
        }

        [UnityTest]
        public IEnumerator SlowZone_SlowsPlayer_ThenExpires()
        {
            var game = GameManager.Instance;
            float normalSpeed = game.Stats.MoveSpeed;

            SlowZone.Spawn(game.Player.Position, 1.5f, 0.5f, 1.2f, game.Player, game.transform);
            yield return null;

            Assert.IsTrue(game.Stats.IsSlowed);
            Assert.Less(game.Stats.MoveSpeed, normalSpeed);

            yield return new WaitForSeconds(1.5f); // 장판 지속시간(1.2초) + 둔화 자체 유예(0.25초)를 넘기기 충분히

            Assert.IsFalse(game.Stats.IsSlowed);
            Assert.AreEqual(normalSpeed, game.Stats.MoveSpeed, 0.01f);
            Assert.IsNull(Object.FindFirstObjectByType<SlowZone>());
        }
    }
}
