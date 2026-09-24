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
            // 2026-09-23: 보스/신규 아트가 옛 2배 보정을 물려받아 거대하게 렌더링돼 1배로 낮췄다.
            // 2026-09-25 Orca 감사로 드러남: 그 1배 조정의 "캔버스를 거의 꽉 채운다"는 근거 자체가
            // 틀렸었다 — 실제로는 보스 6종 PNG가 가짜 알파(완전 불투명, 체크무늬가 픽셀로 그려짐)라
            // 알파 측정이 왜곡된 것이었다. 실제 원인은 아래 테스트에서 수정(배경 제거 파생본 연결).
            // 배율 자체는 여전히 1배가 잠정값(정확한 값은 Codex가 04 규격으로 재납품하고 v1 파이프라인의
            // bodyHeightPx 공식을 쓸 때 나온다 — Delivery/orchestration-art-gap-review.md P2/#7).
            foreach (var def in new[]
                     {
                         EnemyDef.GrinderGolem, EnemyDef.DripServerSpecter, EnemyDef.EspressoLeviathan,
                         EnemyDef.DifficultCustomer, EnemyDef.LeafWarden, EnemyDef.TeaCeremonyGuardian,
                         EnemyDef.EspressoBug, EnemyDef.MilkFoamer,
                     })
            {
                Assert.AreEqual(1f, def.iconVisualScale, $"{def.name} 배율은 잠정 1배(정밀 조정은 v1 파이프라인에서)");
            }
        }

        [Test]
        public void AllWiredEnemyIcons_HaveRealTransparency_NotAnOpaqueBox()
        {
            // 실제로 겪은 버그: grinder_golem 등 6개 PNG가 알파 채널 없이(완전 불투명) 체크무늬
            // 배경을 픽셀로 그려서 "가짜 투명"이었다. 그래서 인게임에서 사각형 박스로 렌더링됐는데,
            // 기존 테스트는 배율 숫자만 검사해서 이 문제를 못 잡았다(2026-09-25 Orca 감사).
            foreach (var def in DefsWithArt)
            {
                var sprite = Resources.Load<Sprite>(def.spriteResourcePath);
                Assert.IsTrue(sprite.texture.isReadable, $"{def.spriteResourcePath} 는 알파 검사를 위해 읽기 가능해야 한다");

                var pixels = sprite.texture.GetPixels();
                bool hasTransparency = false;
                foreach (var p in pixels)
                {
                    if (p.a < 0.9f) { hasTransparency = true; break; }
                }
                Assert.IsTrue(hasTransparency,
                    $"{def.spriteResourcePath} 는 투명 픽셀이 하나도 없다 — 가짜 알파(불투명 배경 박스)일 가능성이 높다");
            }
        }

        [Test]
        public void SpawnAt_DefWithoutArt_UsesCapsulePlaceholder()
        {
            // 브루트는 04/06에 필수 아트로 등재돼 있지만(2026-09-25 Orca 감사로 확인) Codex가 아직
            // 만들지 않아서 placeholder다 — "아트 필요 없음"이 아니라 "아직 없음"이니 아트가 오면
            // 이 테스트도 함께 갱신해야 한다.
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
