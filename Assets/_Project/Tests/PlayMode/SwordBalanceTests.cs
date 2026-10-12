using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>48(2026-10-12) 검 위험 대비 보상: 넓은 베기(×1.15)·피해(×1.25)·일반 적 0.25초 경직(쿨다운 0.8, 보스 면역). 레이피어는 그대로.</summary>
    public class SwordBalanceTests
    {
        GameObject root;
        PlayerController player;
        sealed class Still : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        IEnumerator Setup(WeaponKind weapon)
        {
            Time.timeScale = 1f;
            root = new GameObject("SwordBalance");
            var go = new GameObject("Player"); go.transform.SetParent(root.transform);
            go.transform.position = new Vector2(0f, Balance.LaneCenterY);
            player = go.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white, null, new RunChallenge(0));
            player.enabled = false;
            go.AddComponent<RapierWeapon>().Init(player.Stats, player, weapon);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup() { Time.timeScale = 1f; if (root != null) Object.Destroy(root); yield return null; }

        Enemy Spawn(float x, EnemyDef def = null)
        {
            var e = Enemy.SpawnAt(new Vector2(x, Balance.LaneCenterY), def ?? EnemyDef.Brute, 50f, player, root.transform);
            return e;
        }

        [UnityTest]
        public IEnumerator Sword_ReachesFurther_HitsHarder_ThanRapier()
        {
            yield return Setup(WeaponKind.Sword);
            float baseRange = player.Stats.Range;
            var far = Spawn(baseRange + EnemyDef.Brute.radius + 0.15f); // 판정은 콜라이더 겹침: 레이피어 2.2+r 밖, 검 2.53+r 안
            far.SetDriver(new Still());
            float hp = far.Hp;
            float t0 = Time.time;
            while (far.Hp >= hp && Time.time - t0 < 2f) yield return null;
            Assert.AreEqual(player.Stats.Damage * Balance.SwordDamageMultiplier, hp - far.Hp, 0.01f, "검 첫 베기 = 피해 ×1.25");
            Object.Destroy(root); yield return null;

            yield return Setup(WeaponKind.Rapier);
            var farR = Spawn(baseRange + EnemyDef.Brute.radius + 0.15f);
            farR.SetDriver(new Still());
            float hpR = farR.Hp;
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(hpR, farR.Hp, 0.01f, "레이피어는 기존 사거리(같은 위치 미적중)");
        }

        [UnityTest]
        public IEnumerator Sword_StaggersNormalEnemy_WithCooldown_NoContactWhileStaggered_BossImmune()
        {
            yield return Setup(WeaponKind.Sword);
            var e = Spawn(1f);
            float t0 = Time.time;
            while (!e.Staggered && Time.time - t0 < 2f) yield return null;
            Assert.IsTrue(e.Staggered, "검 타격 경직");
            Assert.IsFalse(e.TryStagger(1f, 1f), "쿨다운 중 재경직 없음(무한 경직 방지)");
            e.transform.position = player.Position; // 붙어 있어도
            float hp = player.Hp;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(hp, player.Hp, 0.01f, "경직 중 접촉 피해 없음");
            yield return new WaitForSeconds(Balance.SwordStaggerSeconds + 0.1f);
            Assert.IsFalse(e.Staggered, "0.25초 뒤 풀림");

            var boss = Spawn(-1f, EnemyDef.GrinderGolem);
            Assert.IsFalse(boss.TryStagger(1f, 0f), "보스 면역");
        }

        [UnityTest]
        public IEnumerator Rapier_NeverStaggers()
        {
            yield return Setup(WeaponKind.Rapier);
            var e = Spawn(1f);
            float hp = e.Hp, t0 = Time.time;
            while (e.Hp >= hp && Time.time - t0 < 2f) yield return null;
            Assert.Less(e.Hp, hp);
            Assert.IsFalse(e.Staggered);
        }
    }
}
