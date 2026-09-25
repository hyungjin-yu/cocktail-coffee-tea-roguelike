using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>06:24 — 보스 등장 1초 "안내" 동안은 무적이고 접촉 피해도 없어야 한다. 일반 몹은 영향 없음.</summary>
    public class BossArrivalGraceTests
    {
        [UnityTest]
        public IEnumerator Boss_IsInvulnerable_DuringArrivalGrace_ThenBecomesVulnerable()
        {
            var parent = new GameObject("Parent").transform;
            var boss = Enemy.SpawnAt(Vector2.zero, EnemyDef.GrinderGolem, 1f, null, parent);

            Assert.IsTrue(boss.Invulnerable, "등장 직후에는 무적 상태여야 한다");
            Assert.IsFalse(boss.HasArrived);

            float hpBefore = boss.Hp;
            boss.TakeDamage(50f);
            Assert.AreEqual(hpBefore, boss.Hp, "안내 중에는 피해가 들어가면 안 된다");

            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);

            Assert.IsTrue(boss.HasArrived);
            Assert.IsFalse(boss.Invulnerable, "안내가 끝나면 무적이 풀려야 한다");
            boss.TakeDamage(50f);
            Assert.Less(boss.Hp, hpBefore, "안내가 끝난 뒤에는 피해가 들어가야 한다");

            Object.Destroy(parent.gameObject);
        }

        [UnityTest]
        public IEnumerator CommonEnemy_IsNotInvulnerable_AndHasArrivedImmediately()
        {
            var parent = new GameObject("Parent").transform;
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Imp, 1f, null, parent);
            yield return null;

            Assert.IsTrue(enemy.HasArrived, "보스가 아니면 등장 안내가 없어야 한다");
            Assert.IsFalse(enemy.Invulnerable);

            Object.Destroy(parent.gameObject);
        }
    }
}
