using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>실제 몹 아이콘(Resources)이 있으면 흰색으로 그대로, 없으면 Capsule+색상 placeholder를 쓰는지 확인.</summary>
    public class EnemySpriteTests
    {
        static readonly EnemyDef[] DefsWithArt =
        {
            EnemyDef.Imp, EnemyDef.MartiniSpirit, EnemyDef.OliveBomber, EnemyDef.SwirlConjuror, EnemyDef.ShakerGolem,
        };

        [Test]
        public void SpawnAt_UsesRealIcon_ForDefsThatHaveOne()
        {
            var parent = new GameObject("Parent").transform;

            foreach (var def in DefsWithArt)
            {
                var expected = Resources.Load<Sprite>(def.spriteResourcePath);
                Assert.IsNotNull(expected, $"{def.spriteResourcePath} 리소스가 있어야 한다");

                var enemy = Enemy.SpawnAt(Vector2.zero, def, 1f, null, parent);
                var sr = enemy.GetComponentInChildren<SpriteRenderer>();

                Assert.AreEqual(expected, sr.sprite, $"{def.name} 는 자기 아이콘을 써야 한다");
                Assert.AreEqual(Color.white, sr.color);
                Assert.AreEqual(Color.white, enemy.NormalColor);
                Assert.AreEqual(def.radius * 2f * Balance.EnemyIconVisualScale, sr.transform.localScale.x, 0.001f,
                    "실제 아이콘은 캔버스 여백 보정 배율이 적용돼야 한다");
                Assert.AreEqual(def.radius, enemy.GetComponent<CircleCollider2D>().radius, 0.001f,
                    "콜라이더 반지름은 시각적 배율과 무관하게 def.radius 그대로여야 한다");

                Object.DestroyImmediate(enemy.gameObject);
            }

            Object.DestroyImmediate(parent.gameObject);
        }

        [Test]
        public void SpawnAt_DefWithoutArt_UsesCapsulePlaceholder()
        {
            AssertPlaceholder(EnemyDef.Brute);
            // 쉐이커 골렘 아트가 일반 몹으로 분리되면서, 중간보스(그라인더 골렘)는 전용 아트가 없어졌다
            // ([[02_몹 디자인 - 커피 & 중간보스]] 기준 재정렬, 2026-09-22).
            AssertPlaceholder(EnemyDef.GrinderGolem);
        }

        static void AssertPlaceholder(EnemyDef def)
        {
            var parent = new GameObject("Parent").transform;
            var enemy = Enemy.SpawnAt(Vector2.zero, def, 1f, null, parent);
            var sr = enemy.GetComponentInChildren<SpriteRenderer>();

            Assert.AreEqual(SpriteFactory.Capsule, sr.sprite, $"{def.name} 는 아직 전용 아트가 없어야 한다");
            Assert.AreEqual(def.color, sr.color);
            Assert.AreEqual(def.color, enemy.NormalColor);
            Assert.AreEqual(def.radius * 2f, sr.transform.localScale.x, 0.001f,
                "placeholder는 여백 보정 배율이 필요 없다");

            Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(parent.gameObject);
        }
    }
}
