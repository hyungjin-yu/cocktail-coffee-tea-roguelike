using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>#18/#23(2026-11-24) 히든 대표 6종 전투 체감 개편 + 바텐더 총 발사 묶음 폭발.
    /// ForceForTests는 효과 검증용 강제 해금(조건·도감·저장 경로는 HiddenSkillPlayTests·HiddenRequirementTests가 그대로 본다).</summary>
    public class HiddenRewardsPlayTests
    {
        string tempPath;
        GameManager game;
        PlayerController player;

        class Stationary : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        IEnumerator Build(Job job, WeaponKind weapon)
        {
            if (game != null) { Object.Destroy(game.gameObject); yield return null; }
            game = GameBootstrap.Build(job, weapon);
            foreach (var mb in game.Player.GetComponents<MonoBehaviour>())
                if (mb is PlayerProjectileWeapon || mb is RapierWeapon) Object.Destroy(mb);
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            player = game.Player;
            yield return null;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_hidden18_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return Build(Job.Bartender, WeaponKind.Rapier);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            PlayerProjectile.SpawnVolleyId = 0;
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            foreach (var s in new[] { "", ".bak", ".tmp" }) if (File.Exists(tempPath + s)) File.Delete(tempPath + s);
        }

        Enemy Target(Vector2 offset, EnemyDef def = null)
        {
            var e = Enemy.SpawnAt(player.Position + offset, def ?? EnemyDef.Brute, 10f, player, game.transform);
            e.SetDriver(new Stationary());
            e.GetComponent<Rigidbody2D>().simulated = false;
            return e;
        }

        [UnityTest]
        public IEnumerator IcedCup_BlockFreezesNearbyNormalEnemies_NotBossNotFar_PerEnemyCooldown()
        {
            Assert.AreEqual(15f, HiddenSkillRuntime.BarrierInterval);
            player.Hidden.ForceForTests(HiddenSkill.IcedCup);
            player.HiddenRuntime.SetBarrierReadyForTests();
            var near = Target(new Vector2(1.5f, 0f));
            var far = Target(new Vector2(4f, 0f));
            var boss = Target(new Vector2(-1.5f, 0f), EnemyDef.GrinderGolem);
            float hp = player.Hp;
            player.TakeDamage(20f);
            Assert.AreEqual(hp, player.Hp, 1e-3f, "보호막 차단은 그대로");
            Assert.IsTrue(near.HiddenFrozen, "250cm 안 일반 적 정지");
            Assert.AreEqual(HiddenSkillRuntime.FrostFreezeSeconds, near.HiddenFreezeRemaining, 0.05f);
            Assert.IsFalse(far.HiddenFrozen, "범위 밖");
            Assert.IsFalse(boss.HiddenFrozen, "보스 제외");
            Assert.AreEqual(1, player.HiddenRuntime.FrostFrozenTotal);
            Assert.AreEqual(near.MaxHp, near.Hp, 1e-3f, "파동은 피해 없음");
            Assert.IsFalse(near.TryHiddenFreeze(1f, 5f), "같은 적 재사용 대기");
            yield return new WaitForSeconds(HiddenSkillRuntime.FrostFreezeSeconds + 0.1f);
            Assert.IsFalse(near.HiddenFrozen, "1.5초 뒤 풀림");
        }

        [UnityTest]
        public IEnumerator PerfectCup_EverySeventhDirectHitTriple_DrinkCritNotStacked()
        {
            player.Hidden.ForceForTests(HiddenSkill.PerfectCup);
            var rt = player.HiddenRuntime;
            var e = Target(new Vector2(1.5f, 0f));
            e.TakeDamage(1f); // 가득 찬 체력 보너스 등 다른 조건 배제
            for (int i = 1; i < HiddenSkillRuntime.PerfectEvery; i++)
            {
                Assert.AreEqual(10f, rt.ModifyDirectDamage(e, 10f), 1e-4f, $"{i}타 = 그대로");
                Assert.IsFalse(rt.LastHitCritical);
                Assert.AreEqual(i, rt.PerfectCount);
            }
            Assert.AreEqual(30f, rt.ModifyDirectDamage(e, 10f), 1e-4f, "7타 = ×3");
            Assert.IsTrue(rt.LastHitCritical);
            Assert.AreEqual(0, rt.PerfectCount, "주기 초기화");
            Assert.AreEqual(1, rt.PerfectTotal);

            player.Stats.drinkCritChance = 1f; // 음료 치명타 확정: 1~6타는 ×2, 7타는 ×3(×6 아님)
            for (int i = 1; i < HiddenSkillRuntime.PerfectEvery; i++) Assert.AreEqual(20f, rt.ModifyDirectDamage(e, 10f), 1e-4f);
            Assert.AreEqual(30f, rt.ModifyDirectDamage(e, 10f), 1e-4f, "완벽 추출 타격에는 음료 치명타를 곱하지 않음");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BitterWake_KeepsBase25_AwakensOnDrop_CooldownBlocksRepeat()
        {
            player.Hidden.ForceForTests(HiddenSkill.BitterWake);
            var rt = player.HiddenRuntime;
            yield return null;
            Assert.AreEqual(0f, player.Stats.hiddenAttackSpeed, 1e-6f, "35% 위 = 없음");
            player.TakeZoneDamage(player.MaxHp * 0.7f);
            Assert.LessOrEqual(player.Hp, player.MaxHp * HiddenSkillRuntime.BitterWakeThreshold);
            yield return null;
            Assert.AreEqual(1, rt.AwakenTotal, "떨어지는 순간 각성");
            Assert.Greater(rt.AwakenRemaining, 0f);
            Assert.AreEqual(HiddenSkillRuntime.BitterWakeAttackSpeed + HiddenSkillRuntime.AwakenAttackSpeed, player.Stats.hiddenAttackSpeed, 1e-6f, "+25% + 35%");
            float before = player.Hp;
            player.TakeDamage(5f);
            Assert.AreEqual(before, player.Hp, 1e-3f, "각성 1초 무적");
            yield return new WaitForSeconds(HiddenSkillRuntime.AwakenSeconds + 0.1f);
            Assert.AreEqual(HiddenSkillRuntime.BitterWakeAttackSpeed, player.Stats.hiddenAttackSpeed, 1e-6f, "각성 끝 = 기존 +25%만");
            player.HealFlat(player.MaxHp);
            yield return null;
            Assert.AreEqual(0f, player.Stats.hiddenAttackSpeed, 1e-6f);
            player.TakeZoneDamage(player.MaxHp * 0.7f);
            yield return null;
            Assert.AreEqual(1, rt.AwakenTotal, "45초 재사용 대기 중에는 다시 각성하지 않음");
            Assert.AreEqual(HiddenSkillRuntime.BitterWakeAttackSpeed, player.Stats.hiddenAttackSpeed, 1e-6f, "상시 +25%는 유지");
        }

        [UnityTest]
        public IEnumerator DoubleFinish_Threshold15_EchoHitsNearestOnce_CappedPerSecond()
        {
            player.Hidden.ForceForTests(HiddenSkill.DoubleFinish);
            var rt = player.HiddenRuntime;
            var a = Target(new Vector2(1f, 0f));
            var near = Target(new Vector2(2f, 0f));
            var farther = Target(new Vector2(3.5f, 0f));
            a.TakeDamage(a.MaxHp - Mathf.Floor(a.MaxHp * 0.14f));
            Assert.Less(a.Hp, a.MaxHp * HiddenSkillRuntime.ExecuteThreshold);
            Assert.GreaterOrEqual(a.Hp, a.MaxHp * 0.12f, "기존 12% 기준으로는 처형되지 않는 체력");
            float nearHp = near.Hp, farHp = farther.Hp;
            rt.AfterDirectHit(a);
            Assert.IsTrue(a.IsDead, "15% 미만 처형");
            Assert.AreEqual(nearHp - IntNum.Damage(player.Stats.Damage), near.Hp, 1e-3f, "가장 가까운 적 덤 베기 100%");
            Assert.AreEqual(farHp, farther.Hp, 1e-3f, "1체만");
            Assert.AreEqual(1, rt.EchoTotal);
            for (int i = 0; i < 6; i++)
            {
                var v = Target(new Vector2(1f, 0.1f * i));
                v.TakeDamage(v.MaxHp * 0.9f);
                rt.AfterDirectHit(v);
            }
            Assert.AreEqual(HiddenSkillRuntime.EchoPerSecondCap, rt.EchoTotal, "초당 상한");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThickMug_SmashWhenReady_10PercentMaxHp_Boss5_ThenRecharges()
        {
            player.Hidden.ForceForTests(HiddenSkill.ThickMug);
            var rt = player.HiddenRuntime;
            Assert.IsFalse(rt.MugReady, "처음엔 8초 충전");
            rt.SetMugReadyForTests();
            var e = Target(new Vector2(1f, 0f));
            float hp = e.Hp;
            rt.AfterDirectHit(e);
            Assert.AreEqual(hp - IntNum.Damage(player.MaxHp * HiddenSkillRuntime.MugHpFraction), e.Hp, 1e-3f);
            Assert.IsFalse(rt.MugReady);
            Assert.AreEqual(HiddenSkillRuntime.MugInterval, rt.MugRemaining, 0.05f);
            hp = e.Hp;
            rt.AfterDirectHit(e);
            Assert.AreEqual(hp, e.Hp, 1e-3f, "재충전 중에는 없음");
            rt.SetMugReadyForTests();
            var boss = Target(new Vector2(-1f, 0f), EnemyDef.GrinderGolem);
            boss.Invulnerable = false;
            hp = boss.Hp;
            rt.AfterDirectHit(boss);
            Assert.AreEqual(hp - IntNum.Damage(player.MaxHp * HiddenSkillRuntime.MugBossHpFraction), boss.Hp, 1e-3f, "보스 5%");
            Assert.AreEqual(2, rt.MugTotal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointBlank_Bow_ShovesAndShrapnel_NoVolleyBurst()
        {
            yield return Build(Job.TeaSommelier, WeaponKind.Bow);
            player.Hidden.ForceForTests(HiddenSkill.PointBlank);
            var rt = player.HiddenRuntime;
            Assert.IsFalse(rt.GunVolleyActive, "활에는 발사 묶음 폭발 없음");
            Assert.AreEqual(0, rt.NextGunVolley());
            Assert.IsNull(player.GetComponent<GunVolleyPips>(), "활은 머리 위 총알 표시 자체가 없음");
            var e = Target(new Vector2(1f, 0f));
            var side = Target(new Vector2(1.7f, 0.3f));
            float sideHp = side.Hp;
            float x0 = e.transform.position.x;
            rt.AfterDirectHit(e);
            Assert.AreEqual(x0 + HiddenSkillRuntime.ShoveDistance, e.transform.position.x, 0.05f, "플레이어 반대쪽으로 밀림");
            Assert.AreEqual(sideHp - IntNum.Damage(player.Stats.Damage * HiddenSkillRuntime.ShrapnelDamage), side.Hp, 1e-3f, "파편 40%");
            float x1 = e.transform.position.x;
            rt.AfterDirectHit(e);
            Assert.AreEqual(x1, e.transform.position.x, 1e-3f, "같은 적 0.6초 재사용 대기");
            var boss = Target(new Vector2(-1.5f, 0f), EnemyDef.GrinderGolem);
            float bx = boss.transform.position.x;
            rt.AfterDirectHit(boss);
            Assert.AreEqual(bx, boss.transform.position.x, 1e-3f, "보스는 밀리지 않음");
        }

        [UnityTest]
        public IEnumerator PointBlank_BartenderGun_VolleyCountsOncePerShot_ThirdHitBursts_PipsFollowState()
        {
            yield return Build(Job.Bartender, WeaponKind.Gun);
            var rt = player.HiddenRuntime;
            var pips = player.GetComponent<GunVolleyPips>();
            Assert.IsNotNull(pips, "바텐더·총은 표시 부착");
            pips.Refresh();
            Assert.IsFalse(pips.Visible, "히든 없으면 숨김");
            Assert.AreEqual(0, rt.NextGunVolley(), "히든 없으면 세지 않음");

            player.Hidden.ForceForTests(HiddenSkill.PointBlank);
            Assert.IsTrue(rt.GunVolleyActive);
            pips.Refresh();
            Assert.IsTrue(pips.Visible);
            Assert.AreEqual(0, pips.FilledShown);
            Assert.Greater(pips.HeightLocal, 0.8f, "머리 위");

            var target = Target(new Vector2(4f, 0f));
            var splash = Target(new Vector2(4.8f, 0.4f));
            var outside = Target(new Vector2(7f, 0f));
            int v1 = rt.NextGunVolley(), v2 = rt.NextGunVolley(), v3 = rt.NextGunVolley();
            Assert.That(v1 > 0 && v2 > v1 && v3 > v2);
            Vector2 at = target.transform.position;
            rt.OnGunVolleyHit(v1, at); rt.OnGunVolleyHit(v1, at); rt.OnGunVolleyHit(v1, at); // 다중 발사·관통: 같은 발사 = 1회
            Assert.AreEqual(1, rt.VolleyHits);
            pips.Refresh();
            Assert.AreEqual(1, pips.FilledShown, "1/3");
            rt.OnGunVolleyHit(v2, at);
            Assert.AreEqual(2, rt.VolleyHits, "2/3");
            float tHp = target.Hp, sHp = splash.Hp, oHp = outside.Hp;
            rt.OnGunVolleyHit(v3, at);
            Assert.AreEqual(0, rt.VolleyHits, "폭발 순간 내부 카운터 즉시 0");
            Assert.AreEqual(1, rt.VolleyBurstTotal);
            int burst = IntNum.Damage(player.Stats.Damage * HiddenSkillRuntime.VolleyBurstDamage);
            Assert.AreEqual(tHp - burst, target.Hp, 1e-3f, "3번째 적중 지점 폭발");
            Assert.AreEqual(sHp - burst, splash.Hp, 1e-3f, "180cm 안 주변");
            Assert.AreEqual(oHp, outside.Hp, 1e-3f, "범위 밖");
            rt.OnGunVolleyHit(v3, at);
            Assert.AreEqual(0, rt.VolleyHits, "이미 센 발사의 늦은 적중은 다시 세지 않음");
            pips.Refresh();
            if (!HitFeedback.LowEffects)
            {
                Assert.IsTrue(rt.VolleyFlashing);
                Assert.AreEqual(3, pips.FilledShown, "짧게 3개 찬 모습");
                yield return new WaitForSeconds(HiddenSkillRuntime.VolleyFlashSeconds + 0.1f);
                pips.Refresh();
            }
            Assert.AreEqual(0, pips.FilledShown, "표시도 초기화");

            player.TakeZoneDamage(99999f);
            yield return null;
            pips.Refresh();
            Assert.IsFalse(pips.Visible, "사망 시 숨김");
        }

        [UnityTest]
        public IEnumerator Projectile_CopiesVolleyIdOnlyWhileWeaponSetsIt()
        {
            PlayerProjectile.SpawnVolleyId = 7;
            PlayerProjectile.Spawn(player.Position, Vector2.right, 1f, 1f, 0, game.transform, "bullet", player, true);
            PlayerProjectile.SpawnVolleyId = 0;
            PlayerProjectile.Spawn(player.Position, Vector2.left, 1f, 1f, 0, game.transform, "bullet", player, false);
            var all = Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None);
            Assert.AreEqual(1, all.Count(p => p.VolleyId == 7));
            Assert.AreEqual(1, all.Count(p => p.VolleyId == 0), "무기 발사 밖(원형 파열 파편 등)은 0 = 세지 않음");
            yield return null;
        }
    }
}
