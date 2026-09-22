using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>Enemy.Damaged 이벤트와 Invulnerable 플래그 (강화 보스 압력 게이지, 드립 서버 스펙터 투명화가 의존).</summary>
    public class EnemyDamageEventsTests
    {
        [Test]
        public void TakeDamage_FiresDamagedEvent_WithAppliedAmount()
        {
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Brute, 1f, null, null);
            float received = -1f;
            enemy.Damaged += amount => received = amount;

            enemy.TakeDamage(12f);

            Assert.AreEqual(12f, received, 0.0001f);
            Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void Invulnerable_BlocksDamage_AndSuppressesDamagedEvent()
        {
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Brute, 1f, null, null);
            float hpBefore = enemy.Hp;
            bool fired = false;
            enemy.Damaged += _ => fired = true;

            enemy.Invulnerable = true;
            enemy.TakeDamage(999f);

            Assert.AreEqual(hpBefore, enemy.Hp, "무적 상태에서는 피해를 받으면 안 된다");
            Assert.IsFalse(fired, "무적 상태에서는 Damaged 이벤트도 발생하면 안 된다");
            Assert.IsFalse(enemy.IsDead);

            Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void Invulnerable_Cleared_AllowsDamageAgain()
        {
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.Brute, 1f, null, null);
            enemy.Invulnerable = true;
            enemy.TakeDamage(999f);
            enemy.Invulnerable = false;

            float hpBefore = enemy.Hp;
            enemy.TakeDamage(10f);

            Assert.AreEqual(hpBefore - 10f, enemy.Hp, 0.0001f);
            Object.DestroyImmediate(enemy.gameObject);
        }
    }
}
