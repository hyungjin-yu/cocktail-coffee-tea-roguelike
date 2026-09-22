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
            Assert.AreEqual("Icons/Enemies/ice_shard", EnemyDef.ShakerGolem.projectileSpriteResourcePath);
            Assert.AreEqual("Icons/Enemies/water_drop", EnemyDef.KettleCannon.projectileSpriteResourcePath);
            Assert.IsNotNull(Resources.Load<Sprite>(EnemyDef.ShakerGolem.projectileSpriteResourcePath));
            Assert.IsNotNull(Resources.Load<Sprite>(EnemyDef.KettleCannon.projectileSpriteResourcePath));
        }
    }
}
