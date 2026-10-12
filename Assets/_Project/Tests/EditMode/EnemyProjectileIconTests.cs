using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>발사체 아이콘(def.projectileSpriteResourcePath)이 있으면 그걸 쓰고, 없으면 색상 원
    /// placeholder를 쓰는지 확인.</summary>
    public class EnemyProjectileIconTests
    {
        [Test]
        public void Spawn_WithIconPath_UsesRealIcon()
        {
            var expected = Resources.Load<Sprite>("Icons/Enemies/ice_shard");
            Assert.IsNotNull(expected, "Icons/Enemies/ice_shard 아이콘이 Resources 에 있어야 한다");

            EnemyProjectile.Spawn(Vector2.zero, Vector2.right, 1f, 1f, null, null, "Icons/Enemies/ice_shard");
            var sr = Object.FindFirstObjectByType<EnemyProjectile>().GetComponent<SpriteRenderer>();

            Assert.AreEqual(expected, sr.sprite);
            Assert.AreEqual(Color.white, sr.color, "실제 아이콘은 색을 덧씌우면 안 된다");

            Object.DestroyImmediate(sr.gameObject);
        }

        [Test]
        public void Spawn_WithoutIconPath_UsesCirclePlaceholder()
        {
            EnemyProjectile.Spawn(Vector2.zero, Vector2.right, 1f, 1f, null, null);
            var sr = Object.FindFirstObjectByType<EnemyProjectile>().GetComponent<SpriteRenderer>();

            Assert.AreEqual(SpriteFactory.Circle, sr.sprite);
            Assert.AreNotEqual(Color.white, sr.color, "placeholder는 색을 덧씌운다");

            Object.DestroyImmediate(sr.gameObject);
        }

        [Test]
        public void ShakerGolem_And_KettleCannon_HaveMatchingTrackProjectileIcons()
        {
            // 2026-10-04 1차 통합: 04 §8 v1 아트(art:ice_shard / art:water_drop)를 우선 쓰고, 미반입 시 같은 이름의
            // 기존 Icons/Enemies 아이콘으로 대체한다 — 경로 문자열 대신 실제로 쓸 스프라이트가 있는지를 검사.
            Assert.AreEqual(EnemyProjectile.ArtPrefix + "ice_shard", EnemyDef.ShakerGolem.projectileSpriteResourcePath);
            Assert.AreEqual(EnemyProjectile.ArtPrefix + "water_drop", EnemyDef.KettleCannon.projectileSpriteResourcePath);
            Assert.IsNotNull(ArtLibrary.Sprite("ice_shard") ?? Resources.Load<Sprite>("Icons/Enemies/ice_shard"));
            Assert.IsNotNull(ArtLibrary.Sprite("water_drop") ?? Resources.Load<Sprite>("Icons/Enemies/water_drop"));
        }
    }
}
