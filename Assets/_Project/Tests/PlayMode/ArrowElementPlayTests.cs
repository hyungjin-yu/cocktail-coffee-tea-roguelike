using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-24 #21 속성 화살 실제 적중 경로(기본 활 무기·발사체)와 적 사망·보스·저이펙트·보조 활 범위.</summary>
    public class ArrowElementPlayTests
    {
        GameObject root;
        PlayerController player;
        PlayerStats stats;
        class Stationary : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        Enemy Target(float x, float y = 0f, EnemyDef def = null, float scale = 10f)
        {
            var e = Enemy.SpawnAt(new Vector2(x, Balance.LaneCenterY + y), def ?? EnemyDef.Brute, scale, player, root.transform);
            e.SetDriver(new Stationary());
            Physics2D.SyncTransforms();
            return e;
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1; root = new GameObject("ArrowElementPlay"); var go = new GameObject("Player"); go.transform.SetParent(root.transform);
            go.transform.position = new Vector2(0, Balance.LaneCenterY); player = go.AddComponent<PlayerController>();
            stats = new PlayerStats(); player.Init(stats, Color.white, null, new RunChallenge(0)); player.enabled = false;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1; SettingsService.SetEffectsLevel(SettingsData.EffectsNormal); Object.Destroy(root); yield return null;
        }

        void Shoot(ArrowElementKind k, int pierce = 0, float damage = 12f) =>
            PlayerProjectile.Spawn(player.Position, Vector2.right, 9f, damage, pierce, root.transform, "arrow", player, true, element: k);

        [UnityTest] public IEnumerator ActualBowWeapon_CarriesElement_GunDoesNot()
        {
            stats.arrowBurnLevel = 1;
            foreach (var kind in new[] { WeaponKind.Gun, WeaponKind.Bow })
            {
                var enemy = Target(1.5f);
                var weapon = player.gameObject.AddComponent<PlayerProjectileWeapon>(); weapon.Init(stats, player, kind);
                yield return new WaitForSeconds(.3f);
                var st = enemy.GetComponent<ArrowElementStatus>();
                if (kind == WeaponKind.Bow) { Assert.IsNotNull(st, "활"); Assert.Greater(st.BurnRemaining, 0f); }
                else Assert.IsNull(st, "총은 같은 무기 컴포넌트지만 속성 없음");
                Object.Destroy(weapon); Object.Destroy(enemy.gameObject); yield return null;
                foreach (var shot in root.GetComponentsInChildren<PlayerProjectile>()) Object.Destroy(shot.gameObject);
                yield return null;
            }
        }

        [UnityTest] public IEnumerator ActualMultiShot_SpawnOrderMatchesHeadPreview_ConsumesOnlyOnFire()
        {
            stats.arrowBurnLevel = 1; stats.arrowShockLevel = 1; stats.arrowPoisonLevel = 1; stats.extraThrusts = 3; // 4발 묶음
            var weapon = player.gameObject.AddComponent<PlayerProjectileWeapon>(); weapon.Init(stats, player, WeaponKind.Bow);
            yield return null; yield return null; // 대상 없음: 발사도 소비도 없음
            var preview = player.GetComponent<ArrowQueuePreview>();
            Assert.IsNotNull(preview); Assert.IsTrue(preview.Visible, "속성 있으면 머리 위 표시");
            Assert.AreEqual(4, preview.NextVolleyShown);
            var expected = preview.Shown.Take(4).ToList();
            CollectionAssert.AreEqual(new[] { ArrowElementKind.Burn, ArrowElementKind.Shock, ArrowElementKind.Poison, ArrowElementKind.Burn }, expected,
                "대상 없이 기다리는 동안 소비 없음 · 고정 순서(빙결 건너뜀)");
            var e = Target(3f, 0f, EnemyDef.Brute, 100f);
            yield return null; yield return null;
            var shots = root.GetComponentsInChildren<PlayerProjectile>().Where(p => p.Element != ArrowElementKind.None).Take(4).Select(p => p.Element).ToList();
            CollectionAssert.AreEqual(expected, shots, "실제 spawn 순서 = 미리보기 왼쪽부터");
            Assert.AreEqual(ArrowElementKind.Shock, preview.Shown[0], "다음 묶음 첫 화살로 갱신");
            Object.Destroy(weapon);
        }

        [UnityTest] public IEnumerator SameVolleySameElement_AppliesOncePerEnemy_DifferentElementsSeparate()
        {
            stats.arrowPoisonLevel = 1; stats.arrowBurnLevel = 1; var e = Target(1f, 0f, EnemyDef.Brute, 100f);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Poison, true, 12f, e.transform.position, 7);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Poison, true, 12f, e.transform.position, 7);
            var st = e.GetComponent<ArrowElementStatus>();
            Assert.AreEqual(1, st.PoisonStacks, "같은 발사·같은 속성 중복 없음");
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Burn, true, 12f, e.transform.position, 7);
            Assert.Greater(st.BurnRemaining, 0f, "다른 속성은 따로");
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Poison, true, 12f, e.transform.position, 8);
            Assert.AreEqual(2, st.PoisonStacks, "다음 발사는 쌓임");
            yield return null;
        }

        [UnityTest] public IEnumerator Preview_HiddenWithoutElements()
        {
            var weapon = player.gameObject.AddComponent<PlayerProjectileWeapon>(); weapon.Init(stats, player, WeaponKind.Bow);
            yield return null;
            var preview = player.GetComponent<ArrowQueuePreview>();
            Assert.IsTrue(preview == null || !preview.Visible, "속성 미획득 = 기본 화살, 표시 없음");
            Object.Destroy(weapon);
        }

        [UnityTest] public IEnumerator HybridTeaArrow_AndNonOriginal_NeverCarryElement()
        {
            stats.arrowBurnLevel = 1;
            var e = Target(1f);
            PlayerProjectile.Spawn(player.Position, Vector2.right, 9, 12, 0, root.transform, "tea_arrow", null, false, element: ArrowElementKind.Burn); // 보조 활 경로(주인 없음)
            PlayerProjectile.Spawn(player.Position, Vector2.right, 9, 12, 0, root.transform, "arrow", player, false, element: ArrowElementKind.Burn); // 파편/옆 화살
            yield return new WaitForSeconds(.2f);
            Assert.Less(e.Hp, e.MaxHp);
            Assert.IsNull(e.GetComponent<ArrowElementStatus>());
        }

        [UnityTest] public IEnumerator Burn_ShortFastDot_ExpiresAfterTwoSeconds()
        {
            stats.arrowBurnLevel = 1; var e = Target(1f);
            Shoot(ArrowElementKind.Burn); yield return new WaitForSeconds(.2f);
            var st = e.GetComponent<ArrowElementStatus>(); Assert.IsNotNull(st);
            float afterHit = e.Hp;
            Assert.AreEqual(stats.Damage * .3f, st.BurnDps, .01f);
            Assert.IsTrue(st.MarkerVisible(ArrowElementKind.Burn));
            yield return new WaitForSeconds(1f);
            Assert.Less(e.Hp, afterHit, "초당 여러 번 피해");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0f, st.BurnRemaining); Assert.IsFalse(st.MarkerVisible(ArrowElementKind.Burn));
            float done = e.Hp; yield return new WaitForSeconds(.5f); Assert.AreEqual(done, e.Hp, "끝나면 피해 없음");
        }

        [UnityTest] public IEnumerator Poison_StacksToCap_RefreshesSixSeconds()
        {
            stats.arrowPoisonLevel = 2; var e = Target(1f, 0f, EnemyDef.Brute, 100f);
            for (int i = 0; i < 8; i++) ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Poison, true, 12f, e.transform.position);
            var st = e.GetComponent<ArrowElementStatus>();
            Assert.AreEqual(ArrowElementRules.PoisonMaxStacks, st.PoisonStacks, "상한 5");
            Assert.AreEqual(6f, st.PoisonRemaining, .01f);
            Assert.AreEqual(stats.Damage * .07f, st.PoisonStackDps, .01f);
            float hp = e.Hp; yield return new WaitForSeconds(1.1f);
            Assert.AreEqual(hp - IntNum.Damage(st.PoisonStackDps * 5), e.Hp, 1.01f, "1초마다 중첩 × 중첩당 피해");
        }

        [UnityTest] public IEnumerator Freeze_NormalStops_ThenCooldown_BossNotStoppedButBonus()
        {
            stats.arrowFreezeLevel = 1; var e = Target(1f);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Freeze, true, 12f, e.transform.position);
            Assert.IsTrue(e.Staggered, "일반 적 정지");
            Assert.IsTrue(e.GetComponent<ArrowElementStatus>().MarkerVisible(ArrowElementKind.Freeze));
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(e.Staggered);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Freeze, true, 12f, e.transform.position);
            Assert.IsFalse(e.Staggered, "4초 대기 중 다시 얼지 않음(무한 정지 방지)");

            var boss = Target(3f, 0f, EnemyDef.GrinderGolem, 1f);
            ArrowElementStatus.OnArrowHit(boss, player, ArrowElementKind.Freeze, true, 12f, boss.transform.position);
            Assert.IsFalse(boss.Staggered, "보스 기존 경직 면역");
            Assert.AreEqual(13.2f, ArrowElementRules.ModifyDirect(ArrowElementKind.Freeze, boss, player, 12f), .001f, "보스 +10%");
            Assert.AreEqual(12f, ArrowElementRules.ModifyDirect(ArrowElementKind.Freeze, e, player, 12f), "일반 적은 피해 그대로");
            Assert.AreEqual(12f, ArrowElementRules.ModifyDirect(ArrowElementKind.Burn, boss, player, 12f));
        }

        [UnityTest] public IEnumerator Shock_ChainsLimitedNearbyOnly_FirstHitOnly_NoRecursion()
        {
            stats.arrowShockLevel = 2;
            var a = Target(1f); var b = Target(1.8f, 1f); var c = Target(1.8f, -1f); var far = Target(8f, 1f);
            float hb = b.Hp, hc = c.Hp, hf = far.Hp;
            Shoot(ArrowElementKind.Shock, 0, 10f);
            yield return new WaitForSeconds(.25f);
            Assert.Less(a.Hp, a.MaxHp, "첫 대상 직접 피해");
            Assert.AreEqual(hb - IntNum.Damage(10f * .4f), b.Hp, .01f, "연쇄 40%");
            Assert.AreEqual(hc - IntNum.Damage(10f * .4f), c.Hp, .01f, "Lv2 = 2마리");
            Assert.AreEqual(hf, far.Hp, "250cm 밖은 안 닿음");
            var bs = b.GetComponent<ArrowElementStatus>();
            Assert.IsTrue(bs == null || (bs.BurnRemaining == 0f && bs.PoisonStacks == 0 && !b.Staggered), "연쇄는 다른 상태를 붙이지 않음");
        }

        [UnityTest] public IEnumerator Shock_PierceDoesNotMultiplyChains()
        {
            stats.arrowShockLevel = 1;
            var a = Target(1f); var b = Target(2.4f); var side = Target(1.2f, 1.4f); var side2 = Target(2.6f, -1.4f);
            float s1 = side.Hp, s2 = side2.Hp;
            Shoot(ArrowElementKind.Shock, 3, 10f);
            yield return new WaitForSeconds(.4f);
            Assert.Less(b.Hp, b.MaxHp, "관통 두 번째 대상 직접 피해");
            int chained = (side.Hp < s1 ? 1 : 0) + (side2.Hp < s2 ? 1 : 0);
            Assert.LessOrEqual(chained, 1, "연쇄는 첫 대상에서 Lv(1)마리만");
        }

        [UnityTest] public IEnumerator EnemyDeath_DuringDots_NoErrors_StatusGoesWithEnemy()
        {
            stats.arrowBurnLevel = 3; stats.arrowPoisonLevel = 3; var e = Target(1f, 0f, EnemyDef.Brute, 1f);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Burn, true, 12f, e.transform.position);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Poison, true, 12f, e.transform.position);
            e.TakeDamage(e.Hp);
            Assert.IsTrue(e.IsDead);
            yield return new WaitForSeconds(.5f);
            Assert.IsTrue(e == null, "적과 함께 제거(풀 재사용 없음)");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator RunEnd_OwnerDead_StopsTicking()
        {
            stats.arrowBurnLevel = 1; var e = Target(1f, 0f, EnemyDef.Brute, 100f);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Burn, true, 12f, e.transform.position);
            Object.Destroy(player.gameObject); yield return null;
            float hp = e.Hp; yield return new WaitForSeconds(.6f);
            Assert.AreEqual(hp, e.Hp, "주인이 사라지면 지속 피해 정지");
        }

        [UnityTest] public IEnumerator LowEffects_KeepsMinimalStatusMarkerAndChain()
        {
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            stats.arrowPoisonLevel = 1; stats.arrowShockLevel = 1;
            var e = Target(1f); var n = Target(1f, 1f);
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Poison, true, 12f, e.transform.position);
            Assert.IsTrue(e.GetComponent<ArrowElementStatus>().MarkerVisible(ArrowElementKind.Poison), "저이펙트에서도 상태 표식");
            ArrowElementStatus.OnArrowHit(e, player, ArrowElementKind.Shock, true, 12f, e.transform.position);
            Assert.IsTrue(Object.FindObjectsByType<ArrowElementAnim>(FindObjectsSortMode.None).Any(x => x.name == "ArrowShockChain"), "저이펙트 연쇄 최소 표시");
            Assert.Less(n.Hp, n.MaxHp);
            yield return null;
        }
    }
}
