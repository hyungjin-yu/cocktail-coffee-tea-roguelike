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
            EnemyDef.TeabagImp, EnemyDef.KettleCannon, EnemyDef.SugarCubeBomber,
            EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian, EnemyDef.EspressoBug, EnemyDef.MilkFoamer,
            EnemyDef.GrinderGolem, EnemyDef.DripServerSpecter, EnemyDef.EspressoLeviathan, EnemyDef.DifficultCustomer,
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
                Assert.AreEqual(def.radius * 2f * def.iconVisualScale, sr.transform.localScale.x, 0.001f,
                    "실제 아이콘은 def.iconVisualScale 배율이 적용돼야 한다");
                Assert.AreEqual(def.radius, enemy.GetComponent<CircleCollider2D>().radius, 0.001f,
                    "콜라이더 반지름은 시각적 배율과 무관하게 def.radius 그대로여야 한다");

                Object.DestroyImmediate(enemy.gameObject);
            }

            Object.DestroyImmediate(parent.gameObject);
        }

        [Test]
        public void NewBossAndEliteArt_DoesNotGetTheOldRosterCanvasScale()
        {
            // 실제 버그: 보스/신규 아트는 칵테일·차 로스터 원화보다 캔버스를 훨씬 꽉 채우는데
            // 옛 2배 보정을 그대로 물려받아서 화면을 반쯤 채울 정도로 거대하게 렌더링됐다
            // ("이게 맞나 싶은데" 사용자 리포트, 2026-09-23). 신규 아트는 배율 보정 없이(1배) 써야 한다.
            foreach (var def in new[]
                     {
                         EnemyDef.GrinderGolem, EnemyDef.DripServerSpecter, EnemyDef.EspressoLeviathan,
                         EnemyDef.DifficultCustomer, EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian,
                         EnemyDef.EspressoBug, EnemyDef.MilkFoamer,
                     })
            {
                Assert.AreEqual(1f, def.iconVisualScale, $"{def.name} 은 캔버스를 거의 꽉 채우는 아트라 추가 배율이 필요 없다");
            }
        }

        [Test]
        public void SpawnAt_DefWithoutArt_UsesCapsulePlaceholder()
        {
            // 브루트는 디자인 문서에 없는 자체 추가 잡몹이라 전용 아트가 없다.
            AssertPlaceholder(EnemyDef.Brute);
        }

        [Test]
        public void DifficultCustomer_SwitchesToEnragedArt_BelowHalfHp_AndBackAbove()
        {
            var parent = new GameObject("Parent").transform;
            var enemy = Enemy.SpawnAt(Vector2.zero, EnemyDef.DifficultCustomer, 1f, null, parent);
            var sr = enemy.GetComponentInChildren<SpriteRenderer>();

            var normal = Resources.Load<Sprite>(EnemyDef.DifficultCustomer.spriteResourcePath);
            var enraged = Resources.Load<Sprite>(EnemyDef.DifficultCustomer.enragedSpriteResourcePath);
            Assert.AreEqual(normal, sr.sprite, "초기에는 1페 아트여야 한다");

            enemy.SetEnragedVisual(true);
            Assert.AreEqual(enraged, sr.sprite, "Enraged 진입 시 2페 아트로 바뀌어야 한다");

            enemy.SetEnragedVisual(false);
            Assert.AreEqual(normal, sr.sprite, "Enraged 해제 시 1페 아트로 돌아와야 한다");

            Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(parent.gameObject);
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
