using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>57(2026-10-15) 사용자 피드백: 물방울 탄(art:water_drop)은 둥근 몸통(로컬 -Y)이 진행 방향을 향하고, 보이는 크기와 판정이
    /// 이전의 절반. 다른 탄(유리 조각 등)은 회전·크기·판정이 그대로.</summary>
    public class WaterDropProjectileTests
    {
        [TearDown]
        public void Clean()
        {
            foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(p.gameObject);
        }

        static EnemyProjectile Spawn(Vector2 dir, string path)
        {
            var before = new System.Collections.Generic.HashSet<EnemyProjectile>(Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None));
            EnemyProjectile.Spawn(Vector2.zero, dir, 1f, 1f, null, null, path);
            foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                if (!before.Contains(p)) return p; // 검색 순서는 보장되지 않으므로 새로 생긴 것만
            return null;
        }

        [Test]
        public void IceShard_IsHalfSize_WithHalfHitRadius_AndPreservesArtDirection()
        {
            var ice = Spawn(Vector2.right, EnemyProjectile.ArtPrefix + EnemyProjectile.IceShardId);
            Assert.AreEqual(0.11f, ice.HitRadiusValue, 1e-4f);
            var art = ArtLibrary.Get(EnemyProjectile.IceShardId);
            Assert.IsNotNull(art, "실제 얼음 탄 아트로 검증");
            float before = 0.22f * 2f * 2.2f / Mathf.Max(0.01f, art.ContentWorldSize.x) * 0.5f;
            Assert.AreEqual(before * 0.5f, ice.transform.localScale.x, 1e-4f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(ice.transform.eulerAngles.z, 0f), 0.1f);
        }

        [Test]
        public void WaterDrop_BulbFacesVelocity_InEightDirections()
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var p = Spawn(dir, EnemyProjectile.ArtPrefix + EnemyProjectile.WaterDropId);
                Vector2 bulb = p.transform.TransformDirection(Vector3.down);
                Assert.AreEqual(1f, Vector2.Dot(bulb.normalized, dir.normalized), 1e-3f, $"dir {i * 45}°: 둥근 몸통(로컬 -Y)이 진행 방향");
                Object.DestroyImmediate(p.gameObject);
            }
        }

        [Test]
        public void WaterDrop_IsHalfSize_WithHalfHitRadius_OthersUnchanged()
        {
            var water = Spawn(Vector2.right, EnemyProjectile.ArtPrefix + EnemyProjectile.WaterDropId);
            Assert.AreEqual(0.11f, water.HitRadiusValue, 1e-4f);
            var art = ArtLibrary.Get(EnemyProjectile.WaterDropId);
            if (art != null)
            {
                float before = 0.22f * 2f * 2.2f / Mathf.Max(0.01f, art.ContentWorldSize.x) * 0.5f;
                Assert.AreEqual(before * 0.5f, water.transform.localScale.x, 1e-4f, "기존 보이는 크기의 절반");
            }
            var glass = Spawn(Vector2.right, EnemyProjectile.ArtPrefix + "glass_shard");
            Assert.AreEqual(0.22f, glass.HitRadiusValue, 1e-4f, "다른 탄 판정 그대로");
            var glassArt = ArtLibrary.Get("glass_shard");
            if (glassArt != null)
            {
                Assert.AreEqual(0.22f * 2f * 2.2f / Mathf.Max(0.01f, glassArt.ContentWorldSize.x) * 0.5f, glass.transform.localScale.x, 1e-4f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(glass.transform.eulerAngles.z, 0f), 0.1f, "새 VFX 탄은 +X가 진행 방향(보정 없음)");
            }
        }
    }
}
