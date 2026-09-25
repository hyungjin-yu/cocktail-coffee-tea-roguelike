using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>Enemy.FindNearest — 06:18 "살아 있고 타격 가능한 적만, 동률이면 먼저 스폰한 적 우선".
    /// PlayMode인 이유: Enemy.Active는 OnEnable에서 채워지는데, 이건 프레임이 한 번 돌아야
    /// 확실히 반영된다(순수 EditMode [Test]는 프레임을 안 돌려서 Active가 비어 있을 수 있다).</summary>
    public class EnemyFindNearestTests
    {
        Transform parent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            parent = new GameObject("Parent").transform;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(parent.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipsInvulnerableEnemies()
        {
            var near = Enemy.SpawnAt(new Vector2(1f, 0f), EnemyDef.Imp, 1f, null, parent);
            near.Invulnerable = true;
            var far = Enemy.SpawnAt(new Vector2(3f, 0f), EnemyDef.Imp, 1f, null, parent);
            yield return null;

            var found = Enemy.FindNearest(Vector2.zero, 10f);

            Assert.AreSame(far, found, "무적 상태인 더 가까운 적은 건너뛰고 타격 가능한 적을 찾아야 한다");
        }

        [UnityTest]
        public IEnumerator ReturnsNull_WhenOnlyInvulnerableEnemiesInRange()
        {
            var e = Enemy.SpawnAt(new Vector2(1f, 0f), EnemyDef.Imp, 1f, null, parent);
            e.Invulnerable = true;
            yield return null;

            Assert.IsNull(Enemy.FindNearest(Vector2.zero, 10f));
        }

        [UnityTest]
        public IEnumerator TiedDistance_PrefersEarlierSpawnedEnemy()
        {
            var first = Enemy.SpawnAt(new Vector2(2f, 0f), EnemyDef.Imp, 1f, null, parent);
            var second = Enemy.SpawnAt(new Vector2(-2f, 0f), EnemyDef.Imp, 1f, null, parent); // 정확히 같은 거리
            yield return null;

            var found = Enemy.FindNearest(Vector2.zero, 10f);

            Assert.AreSame(first, found, "06:18 — 동일 거리면 먼저 스폰한 적이 우선해야 한다");
        }
    }
}
