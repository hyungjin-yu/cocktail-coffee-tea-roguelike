using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>투척형/탄막형(RangedAttackDriver)과 소환형(SummonerDriver)이 실제로 동작하는지 확인.</summary>
    public class EnemyBehaviorTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
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
        public IEnumerator ShakerGolem_KeepsDistance_AndItsProjectileDamagesPlayer()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 500f)); // 테스트 도중 죽지 않게
            float hpBefore = game.Player.Hp;

            var pos = game.Player.Position + new Vector2(EnemyDef.ShakerGolem.keepDistance, 0f);
            var golem = Enemy.SpawnAt(pos, EnemyDef.ShakerGolem, 1f, game.Player, game.transform);
            EnemyRoster.AttachBehavior(golem, game, game.Player);

            // 공격 간격(2초) + 발사체가 keepDistance 를 건너오는 시간만큼 여유 있게 대기
            yield return new WaitForSeconds(EnemyDef.ShakerGolem.attackInterval + 2f);

            Assert.Less(game.Player.Hp, hpBefore, "투척형의 발사체가 플레이어에게 맞아야 한다");
        }

        [UnityTest]
        public IEnumerator MartiniSpirit_FiresBarrage_SpawningMultipleProjectiles()
        {
            var game = GameManager.Instance;

            var pos = game.Player.Position + new Vector2(EnemyDef.MartiniSpirit.keepDistance, 0f);
            var elite = Enemy.SpawnAt(pos, EnemyDef.MartiniSpirit, 1f, game.Player, game.transform);
            EnemyRoster.AttachBehavior(elite, game, game.Player);

            // 초탄 발사 텀(RangedAttackDriver 의 attackInterval*0.5 = 1.6초)을 넘기기 충분히 대기
            yield return new WaitForSeconds(EnemyDef.MartiniSpirit.attackInterval * 0.5f + 0.3f);

            var projectiles = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(projectiles.Length, EnemyDef.MartiniSpirit.barrageCount,
                "탄막형은 한 번에 barrageCount 만큼 발사체를 흩뿌려야 한다");
        }

        [UnityTest]
        public IEnumerator SwirlConjuror_StaysStill_AndPeriodicallySummonsImp()
        {
            var game = GameManager.Instance;
            var spawnPos = game.Player.Position + new Vector2(6f, 0f);
            var conjuror = Enemy.SpawnAt(spawnPos, EnemyDef.SwirlConjuror, 1f, game.Player, game.transform);
            EnemyRoster.AttachBehavior(conjuror, game, game.Player);

            int impCountBefore = CountImps();

            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector2.Distance(conjuror.transform.position, spawnPos), 0.1f,
                "소환형은 제자리에 머물러야 한다 (speed=0)");

            // 첫 소환까지 텀(summonInterval*0.5=3초)을 넘기기 충분히 대기
            yield return new WaitForSeconds(3f);

            Assert.Greater(CountImps(), impCountBefore, "주기적으로 라임웨지를 소환해야 한다");
        }

        [UnityTest]
        public IEnumerator EnemyProjectile_HitsPlayer_ThenDestroysItself()
        {
            var game = GameManager.Instance;
            float hpBefore = game.Player.Hp;

            var pos = game.Player.Position + new Vector2(2f, 0f);
            EnemyProjectile.Spawn(pos, Vector2.left, 6f, 12f, game.Player, game.transform);

            yield return new WaitForSeconds(0.6f); // 2유닛 / 6유닛초 = 약 0.33초면 명중

            Assert.Less(game.Player.Hp, hpBefore, "발사체에 맞으면 피해를 입어야 한다");
            Assert.IsNull(Object.FindFirstObjectByType<EnemyProjectile>(), "명중하면 곧바로 사라져야 한다");
        }

        [UnityTest]
        public IEnumerator EnemyProjectile_Miss_ExpiresAfterLifetime()
        {
            var game = GameManager.Instance;
            // 플레이어와 무관한 방향으로 쏴서 빗나가게 한다
            var pos = game.Player.Position + new Vector2(0f, 3f);
            EnemyProjectile.Spawn(pos, Vector2.up, 6f, 12f, game.Player, game.transform);

            yield return new WaitForSeconds(3.2f); // Lifetime(3초) 초과

            Assert.IsNull(Object.FindFirstObjectByType<EnemyProjectile>(), "수명이 다하면 스스로 사라져야 한다");
        }

        static int CountImps()
        {
            int count = 0;
            foreach (var e in Enemy.Active)
                if (e != null && e.Def == EnemyDef.Imp) count++;
            return count;
        }
    }
}
