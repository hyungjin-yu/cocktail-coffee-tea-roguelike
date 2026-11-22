using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>41 히든 스킬 실제 런 효과. ForceForTests는 효과 검증용 강제 해금(조건 판정은 실제 카드 경로 시험에서 따로 본다).</summary>
    public class HiddenSkillPlayTests
    {
        string tempPath;
        GameManager game;
        PlayerController player;

        class Stationary : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a, Vector2 b, out Vector2 v) { v = Vector2.zero; return true; } }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_hidden_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
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
        public IEnumerator RealCardPath_UnlocksOnce_SavesDiscovery_BannerAndRestartResets()
        {
            var cards = CardDatabase.LoadAll();
            game.GrantCardForTests(cards.First(c => c.id == "molotov"));
            Assert.IsFalse(player.Hidden.Has(HiddenSkill.HotToddy));
            game.GrantCardForTests(cards.First(c => c.id == "chamomile"));
            Assert.IsTrue(player.Hidden.Has(HiddenSkill.HotToddy), "화염병1+카모마일1");
            Assert.IsTrue(player.Hidden.Has(HiddenSkill.BitterBlend) == false, "쐐기풀차 없음");
            Assert.IsTrue(PermanentProgress.Current.discoveredCardIds.Contains("hidden_hot_toddy"), "도감 발견 저장");
            Assert.AreEqual(0, game.EvaluateHidden().Count, "런당 1회");
            yield return null;

            Object.Destroy(game.gameObject);
            yield return null;
            var next = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            Assert.AreEqual(0, next.Player.Hidden.Count, "재시작 초기화");
            Assert.IsTrue(PermanentProgress.Current.discoveredCardIds.Contains("hidden_hot_toddy"), "발견은 영구");
        }

        [UnityTest]
        public IEnumerator Bartender_DiverseCocktailsTriggerRush_NoDuration_DuplicateTurnsItOff()
        {
            // 2026-11-22 주인 결정(피드백 7): 지속시간 없음 · 새 칵테일 3종이면 켜짐 · 이미 가진 카드를 고르면 꺼짐 · 켜진 동안 이동·공속 +40%(피해 보너스 없음).
            var choices=CardDatabase.LoadAll().Where(c=>c.category==CardCategory.Cocktail && c.weaponRestriction!=WeaponRestriction.RangedOnly).Take(6).ToArray();
            float baseMove=game.Stats.MoveSpeed;
            game.GrantCardForTests(choices[0]); game.GrantCardForTests(choices[1]);
            Assert.IsFalse(game.MixingRushActive);
            game.GrantCardForTests(choices[2]);
            Assert.IsTrue(game.MixingRushActive,"새 칵테일 3종째 켜짐");
            Assert.AreEqual(0f,game.Stats.jobDamagePercent,1e-6f,"피해 보너스 없음");
            Assert.AreEqual(GameManager.MixingRushSpeedBonus,game.Stats.jobAttackSpeedPercent,1e-6f);
            Assert.AreEqual(GameManager.MixingRushSpeedBonus,game.Stats.jobMoveSpeedPercent,1e-6f);
            Assert.Greater(game.Stats.MoveSpeed,baseMove,"이동 속도 실제 증가");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(game.MixingRushActive,"시간이 지나도 꺼지지 않음");
            game.GrantCardForTests(choices[0]);
            Assert.IsFalse(game.MixingRushActive,"중복 카드 = 꺼짐");
            Assert.AreEqual(0f,game.Stats.jobAttackSpeedPercent,1e-6f);
            Assert.AreEqual(0f,game.Stats.jobMoveSpeedPercent,1e-6f);
            Assert.AreEqual(1,game.MixingRushOffCount);
            game.GrantCardForTests(choices[3]); game.GrantCardForTests(choices[4]);
            Assert.IsFalse(game.MixingRushActive,"4·5종째는 아직");
            game.GrantCardForTests(choices[5]);
            Assert.IsTrue(game.MixingRushActive,"6종째 다시 켜짐");
        }

        [UnityTest]
        public IEnumerator TeaSommelier_RepeatedSameTeaAddsStableBonus_OtherTeaDoesNotFakeAnotherStack()
        {
            Object.Destroy(game.gameObject); yield return null;
            game=GameBootstrap.Build(Job.TeaSommelier,WeaponKind.Sword); player=game.Player;
            game.GetComponentInChildren<EnemySpawner>().enabled=false; game.Director.enabled=false;
            var tea=CardDatabase.LoadAll().First(c=>c.category==CardCategory.Tea && c.maxStacks>=3);
            var other=CardDatabase.LoadAll().First(c=>c.category==CardCategory.Tea && c.id!=tea.id);
            game.GrantCardForTests(tea); Assert.AreEqual(0,game.DeepSteepLevel);
            game.GrantCardForTests(tea); Assert.AreEqual(1,game.DeepSteepLevel);
            game.GrantCardForTests(other); Assert.AreEqual(1,game.DeepSteepLevel);
            game.GrantCardForTests(tea); Assert.AreEqual(2,game.DeepSteepLevel);
            Assert.AreEqual(.04f,game.Stats.jobDamagePercent,1e-6f);
            Assert.AreEqual(.2f,game.Stats.jobRegenBonus,1e-6f);
            Assert.AreEqual(0f,game.MixingRushRemaining);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenDetails_ShowOwnedEffectAndReadiness_EscapeResumes_ActualBlockReportsActivation()
        {
            player.Hidden.ForceForTests(HiddenSkill.IcedCup);
            player.HiddenRuntime.SetBarrierReadyForTests();
            game.OpenHiddenDetails();
            Assert.IsTrue(game.Hud.HiddenDetailsOpen);
            Assert.IsTrue(game.IsPaused);
            Assert.AreEqual(0f,Time.timeScale);
            string text=string.Join("\n",game.Hud.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t=>t.text));
            StringAssert.Contains(HiddenSkillDatabase.Get(HiddenSkill.IcedCup).name,text);
            StringAssert.Contains("보호막 준비 완료",text);
            game.RequestEscape();
            Assert.IsFalse(game.Hud.HiddenDetailsOpen);
            Assert.IsFalse(game.IsPaused);
            Assert.AreEqual(1f,Time.timeScale);
            Assert.IsTrue(player.HiddenRuntime.TryBlockHit());
            Assert.IsFalse(player.HiddenRuntime.BarrierReady);
            StringAssert.Contains("재충전",player.HiddenRuntime.StateLabel(HiddenSkill.IcedCup));
            StringAssert.Contains(HiddenSkillDatabase.Get(HiddenSkill.IcedCup).name,player.HiddenRuntime.LastActivation);
            Assert.Greater(player.HiddenRuntime.ActivationSerial,0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectHitBonuses_FirstSip_PointBlank_Vip_AndOnlyOnce()
        {
            var rt = player.HiddenRuntime;
            var e = Target(new Vector2(1.5f, 0f));
            Assert.AreEqual(10f, rt.ModifyDirectDamage(e, 10f), 1e-4f, "히든 없음 = 그대로");
            player.Hidden.ForceForTests(HiddenSkill.FirstSip);
            Assert.AreEqual(14f, rt.ModifyDirectDamage(e, 10f), 1e-4f, "가득 찬 체력 +40%");
            e.TakeDamage(1f);
            Assert.AreEqual(10f, rt.ModifyDirectDamage(e, 10f), 1e-4f, "깎인 적은 미적용");
            player.Hidden.ForceForTests(HiddenSkill.PointBlank);
            Assert.AreEqual(12f, rt.ModifyDirectDamage(e, 10f), 1e-4f, "2.5 안 +20%");
            var far = Target(new Vector2(4f, 0f)); far.TakeDamage(1f);
            Assert.AreEqual(10f, rt.ModifyDirectDamage(far, 10f), 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoubleFinish_ExecutesNormalOnly_BossUntouched()
        {
            player.Hidden.ForceForTests(HiddenSkill.DoubleFinish);
            var e = Target(new Vector2(1f, 0f));
            e.TakeDamage(e.MaxHp * 0.9f);
            player.HiddenRuntime.AfterDirectHit(e);
            Assert.IsTrue(e.IsDead, "12% 미만 일반 적 처치");
            var boss = Target(new Vector2(2f, 0f), EnemyDef.GrinderGolem);
            boss.TakeDamage(boss.MaxHp * 0.95f);
            player.HiddenRuntime.AfterDirectHit(boss);
            Assert.IsFalse(boss.IsDead, "보스 제외");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayGuard_ThornsOncePerHalfSecond_NoStatusAttach_PoisonGardenReducesContact()
        {
            player.Stats.flameLevel = 1;
            player.Hidden.ForceForTests(HiddenSkill.TrayGuard);
            var e = Target(new Vector2(0.5f, 0f));
            float hp = e.Hp;
            player.TakeContactDamage(e, 5f);
            Assert.AreEqual(hp - player.Stats.Damage, e.Hp, 1e-3f, "기본 피해 100% 반격");
            Assert.IsNull(e.GetComponent<EnemyHitStatus>(), "반격은 화상 부착 안 함(비재귀)");
            hp = e.Hp;
            player.HiddenRuntime.OnContactHit(e);
            Assert.AreEqual(hp, e.Hp, 1e-3f, "0.5초 쿨다운");

            player.Hidden.ForceForTests(HiddenSkill.PoisonGarden);
            player.Stats.poisonLevel = 1;
            EnemyHitStatus.AttachDirect(e, player);
            Assert.AreEqual(8.5f, player.HiddenRuntime.ModifyContactDamage(e, 10f), 1e-4f, "중독 적 접촉 -15%");
            yield return null;
        }

        [UnityTest]
        public IEnumerator KillHealCap_PickupHealCap_NoGroundZonesCreated()
        {
            int burst = BurstZone.ActiveCount, slow = SlowZone.ActiveCount;
            player.Hidden.ForceForTests(HiddenSkill.HotToddy);
            player.Hidden.ForceForTests(HiddenSkill.BeanCrate);
            player.TakeZoneDamage(50f);
            float before = player.Hp;
            for (int i = 0; i < 10; i++) player.HiddenRuntime.OnEnemyKilled(EnemyDef.Brute);
            Assert.AreEqual(before + 4f, player.Hp, 0.05f, "처치 회복 초당 4 상한");
            before = player.Hp;
            for (int i = 0; i < 10; i++) player.HiddenRuntime.OnPickup();
            Assert.AreEqual(before + 3f, player.Hp, 0.05f, "줍기 회복 초당 3 상한");
            Assert.AreEqual(burst, BurstZone.ActiveCount, "히든은 바닥 지대를 만들지 않는다(35)");
            Assert.AreEqual(slow, SlowZone.ActiveCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IcedCupBarrier_BlocksOneHit_ThenRecharges()
        {
            player.Hidden.ForceForTests(HiddenSkill.IcedCup);
            Assert.IsFalse(player.HiddenRuntime.BarrierReady);
            player.HiddenRuntime.SetBarrierReadyForTests();
            float hp = player.Hp;
            player.TakeDamage(20f);
            Assert.AreEqual(hp, player.Hp, 1e-3f, "보호막이 1회 막음");
            Assert.IsFalse(player.HiddenRuntime.BarrierReady);
            yield return new WaitForSeconds(Balance.PlayerInvulnerableSeconds + 0.05f);
            player.TakeDamage(20f);
            Assert.Less(player.Hp, hp, "두 번째 피격은 들어감");
        }

        [UnityTest]
        public IEnumerator LongSteep_PlusOneSecond_BitterBlendBothStatusesTick125()
        {
            player.Stats.flameLevel = 1; player.Stats.poisonLevel = 1;
            var a = Target(new Vector2(1f, 0f));
            EnemyHitStatus.AttachDirect(a, player);
            Assert.AreEqual(3f, a.GetComponent<EnemyHitStatus>().PoisonRemaining, 1e-4f);
            player.Hidden.ForceForTests(HiddenSkill.LongSteep);
            var b = Target(new Vector2(1.5f, 0f));
            EnemyHitStatus.AttachDirect(b, player);
            Assert.AreEqual(4f, b.GetComponent<EnemyHitStatus>().PoisonRemaining, 1e-4f, "+1초");
            Assert.AreEqual(4f, b.GetComponent<EnemyHitStatus>().FlameRemaining, 1e-4f);

            player.Hidden.ForceForTests(HiddenSkill.BitterBlend);
            float dmg = 0f; b.Damaged += x => dmg += x;
            float flameDps = b.GetComponent<EnemyHitStatus>().FlameDps, poisonDps = b.GetComponent<EnemyHitStatus>().PoisonDps;
            yield return new WaitForSeconds(1.05f);
            // 정수 전용: 화상·중독 틱이 각각 +25% 뒤 정수 피해(IntNum.Damage)로 들어간다.
            Assert.AreEqual(IntNum.Damage(flameDps * 1.25f) + IntNum.Damage(poisonDps * 1.25f), dmg, 0.01f, "둘 다 걸린 1초 틱 +25%");
        }
    }
}
