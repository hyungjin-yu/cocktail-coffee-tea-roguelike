using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>42 화상/중독 부착 표식, 43 몬스터 장판 v2(PixelLab) — 그림만 바뀌고 규칙은 그대로인지.</summary>
    public class StatusZoneVfxTests
    {
        GameObject root;
        PlayerController player;
        class Stationary : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1f;
            root = new GameObject("StatusZoneVfx");
            var go = new GameObject("Player"); go.transform.SetParent(root.transform);
            go.transform.position = new Vector2(0f, Balance.LaneCenterY);
            player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white, null, new RunChallenge(0));
            player.enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup() { Time.timeScale = 1f; Object.Destroy(root); yield return null; }

        Enemy Target(float x)
        {
            var e = Enemy.SpawnAt(new Vector2(x, Balance.LaneCenterY), EnemyDef.Brute, 10f, player, root.transform);
            e.SetDriver(new Stationary()); e.GetComponent<Rigidbody2D>().simulated = false;
            return e;
        }

        [UnityTest]
        public IEnumerator BurnAndPoison_DistinctAnimatedMarkers_LeftRight_SealFreezes_ExpiryHides()
        {
            Assert.IsNotNull(EnemyHitStatus.MarkerClip(true), "PixelLab 화상 클립 반입");
            Assert.IsNotNull(EnemyHitStatus.MarkerClip(false), "PixelLab 중독 클립 반입");
            Assert.GreaterOrEqual(EnemyHitStatus.MarkerClip(true).frames.Length, 8);
            player.Stats.flameLevel = 1; player.Stats.poisonLevel = 1;
            var e = Target(1.5f);
            EnemyHitStatus.AttachDirect(e, player);
            var s = e.GetComponent<EnemyHitStatus>();
            Assert.IsTrue(s.FlameMarkerVisible && s.PoisonMarkerVisible, "동시 상태 둘 다 표시");
            StringAssert.StartsWith("status_burn", s.FlameMarkerSprite);
            StringAssert.StartsWith("status_poison", s.PoisonMarkerSprite);
            var flame = e.transform.Find("FlameHitMarker"); var poison = e.transform.Find("PoisonHitMarker");
            Assert.Less(flame.localPosition.x, 0f); Assert.Greater(poison.localPosition.x, 0f);
            string before = s.FlameMarkerSprite;
            yield return new WaitForSeconds(0.25f);
            Assert.AreNotEqual(before, s.FlameMarkerSprite, "불꽃은 움직인다(루프 재생)");

            player.Stats.LockFlame(2f);
            yield return null;
            string frozen = s.FlameMarkerSprite;
            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(frozen, s.FlameMarkerSprite, "봉인 중 정지");
            Assert.Less(flame.GetComponent<SpriteRenderer>().color.a, 0.6f, "봉인 중 반투명");

            yield return new WaitForSeconds(3.2f);
            Assert.IsFalse(s.PoisonMarkerVisible, "지속 끝나면 사라짐");
        }

        [UnityTest]
        public IEnumerator ZoneV2_VisualBoundaryEqualsHitRadius_FadesInAndOut_RulesUnchanged()
        {
            foreach (var art in new[] { BurstZone.CoffeePoolArt, BurstZone.OliveFireArt, "sugar_pool" })
            {
                string v2 = BurstZone.V2For(art);
                Assert.IsTrue(ZoneVisualV2.Available(v2), v2 + " 반입");
                foreach (float r in new[] { 1.1f, 1.3f, 1.4f })
                {
                    var half = ZoneVisualV2.VisualHalfExtents(v2, r);
                    Assert.AreEqual(r, half.x, 0.001f, $"{v2} 가로 경계 = 판정 반경 {r}");
                    Assert.AreEqual(r, half.y, 0.001f, $"{v2} 세로 경계 = 판정 반경 {r}");
                }
            }
            BurstZone.Spawn(player.Position + new Vector2(5f, 0f), 1.3f, 9f, 2f, player, root.transform, BurstZone.CoffeePoolArt);
            var zone = Object.FindFirstObjectByType<BurstZone>();
            var v = zone.GetComponent<ZoneVisualV2>();
            Assert.IsNotNull(v, "v2 활성 그림");
            Assert.IsFalse(zone.GetComponent<SpriteRenderer>().enabled, "v1 그림 숨김");
            Assert.AreEqual(Vector3.one, zone.transform.localScale, "판정 오브젝트 배율 1(판정은 radius 값)");
            Assert.Less(v.AlphaForTests, 0.5f, "막 생겨날 때 나타남 연출");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0.9f, v.AlphaForTests, 0.01f, "활성 중 약간 투명(캐릭터 가림 방지)");
            yield return new WaitForSeconds(1.35f);
            Assert.Less(v.AlphaForTests, 0.6f, "마지막 0.35초 소멸 연출");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(zone == null, "원래 지속 시간 2초에 사라짐");
        }

        [UnityTest]
        public IEnumerator Telegraph_RingMatchesRadius_FillsToEdgeAtActivation_ThenGone()
        {
            Assert.IsTrue(ZoneVisualV2.Available(ZoneTelegraphV2.RingId), "PixelLab 예고 링 반입");
            var half = ZoneVisualV2.VisualHalfExtents(ZoneTelegraphV2.RingId, 1.3f);
            Assert.AreEqual(1.3f, half.x, 0.001f); Assert.AreEqual(1.3f, half.y, 0.001f);
            var t = ZoneTelegraphV2.Spawn(new Vector2(3f, Balance.LaneCenterY), 1.3f, 0.6f, Color.white, root.transform);
            Assert.IsNotNull(t);
            var inner = t.transform.Find("Progress");
            Assert.IsNotNull(inner, "차오름 링");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0.5f, t.ProgressForTests, 0.12f, "예고 절반");
            yield return new WaitForSeconds(0.25f);
            Assert.Greater(t.ProgressForTests, 0.85f);
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(t == null, "예고 시간(0.6초)에 정확히 사라짐");
        }

        [UnityTest]
        public IEnumerator SlowZone_UsesSugarV2_AndStillSlows()
        {
            SlowZone.Spawn(player.Position, 1.2f, 0.5f, 1f, player, root.transform);
            var z = Object.FindFirstObjectByType<SlowZone>();
            Assert.IsNotNull(z.GetComponent<ZoneVisualV2>(), "설탕 v2 그림");
            yield return null; yield return null;
            Assert.IsTrue(player.Stats.IsSlowed, "둔화 규칙 그대로");
        }
    }
}
