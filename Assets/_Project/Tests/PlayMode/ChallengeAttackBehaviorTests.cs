using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    public class ChallengeAttackBehaviorTests
    {
        GameObject root;
        PlayerController player;
        PlayerStats stats;
        RunChallenge challenge;
        class Stationary : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a,Vector2 b,out Vector2 v){v=Vector2.zero;return true;} }
        Enemy Target(float x,float scale=10)
        {
            var e=Enemy.SpawnAt(new Vector2(x,Balance.LaneCenterY),EnemyDef.Brute,scale,player,root.transform);
            e.SetDriver(new Stationary());e.GetComponent<Rigidbody2D>().simulated=false;
            return e;
        }
        void Final(string id)
        {
            var c=ScriptableObject.CreateInstance<CardData>();c.id=id;c.maxStacks=3;
            player.FinalForms.OnCardTaken(c,3);Object.Destroy(c);
        }
        [UnitySetUp]public IEnumerator Setup()
        {
            Time.timeScale=1;root=new GameObject("ChallengeAttackBehavior");var go=new GameObject("Player");go.transform.SetParent(root.transform);
            go.transform.position=new Vector2(0,Balance.LaneCenterY);player=go.AddComponent<PlayerController>();
            stats=new PlayerStats();challenge=new RunChallenge(18);player.Init(stats,Color.white,null,challenge);player.enabled=false;
            yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Object.Destroy(root);yield return null;}
        [UnityTest]public IEnumerator ExistingAndNewEnemies_RescaleHpPercentWithoutDamageEvent_DeadStaysDead()
        {
            var enemy=Target(2,2);float sharedHp=EnemyDef.Brute.hp;
            Assert.AreEqual(sharedHp*2*1.18f,enemy.MaxHp,.0001f);
            enemy.TakeDamage(enemy.MaxHp*.6f);int damageEvents=0;enemy.Damaged+=_=>damageEvents++;
            var dead=Target(3);dead.TakeDamage(dead.Hp);
            challenge.SetCoffeeCount(15);
            Assert.AreEqual(sharedHp*2*1.36f,enemy.MaxHp,.0001f);Assert.AreEqual(.4f,enemy.Hp/enemy.MaxHp,.0001f);
            Assert.AreEqual(0,damageEvents);Assert.IsTrue(dead.IsDead);Assert.LessOrEqual(dead.Hp,0);
            var later=Target(4,2);Assert.AreEqual(enemy.MaxHp,later.MaxHp,.0001f);Assert.AreEqual(sharedHp,EnemyDef.Brute.hp);
            yield return null;
        }
        [UnityTest]public IEnumerator CurrentHitMultiplier_OnceBeforeDamageReduction_IFrameAndZoneRemainSeparate()
        {
            stats.damageReduction=.25f;float initial=player.Hp;
            player.TakeDamage(10);Assert.AreEqual(initial-10*1.072f*.75f,player.Hp,.0001f);
            player.TakeDamage(10);Assert.AreEqual(initial-10*1.072f*.75f,player.Hp,.0001f);
            challenge.SetCoffeeCount(15);float before=player.Hp;
            player.TakeZoneDamage(10);Assert.AreEqual(before-10*1.132f*.75f,player.Hp,.0001f);
            Assert.AreEqual(12,stats.Damage,"Enemy challenge never changes player outgoing attack.");yield return null;
        }
        [UnityTest]public IEnumerator ActualGunAndBowPrimaryAttach_StatusButSecondaryAndHybridDoNot()
        {
            stats.flameLevel=1;stats.poisonLevel=1;
            foreach(var kind in new[]{WeaponKind.Gun,WeaponKind.Bow})
            {
                var enemy=Target(1.5f);enemy.GetComponent<Rigidbody2D>().simulated=true;
                var weapon=player.gameObject.AddComponent<PlayerProjectileWeapon>();weapon.Init(stats,player,kind);
                yield return new WaitForSeconds(.3f);
                Assert.IsNotNull(enemy.GetComponent<EnemyHitStatus>(),kind.ToString());
                Object.Destroy(weapon);Object.Destroy(enemy.gameObject);yield return null;
                foreach(var shot in root.GetComponentsInChildren<PlayerProjectile>())Object.Destroy(shot.gameObject);
                yield return null;
            }
            var secondary=Target(1);secondary.GetComponent<Rigidbody2D>().simulated=true;
            PlayerProjectile.Spawn(player.Position,Vector2.right,9,12,0,root.transform,"tea_arrow",player,false);
            yield return new WaitForSeconds(.2f);
            Assert.Less(secondary.Hp,secondary.MaxHp);Assert.IsNull(secondary.GetComponent<EnemyHitStatus>());
        }
        [UnityTest]public IEnumerator ActualMeleePrimaryAttachesOnlyFrontEnemies()
        {
            stats.flameLevel=1;var front=Target(1);var behind=Target(-1);
            front.GetComponent<Rigidbody2D>().simulated=true;behind.GetComponent<Rigidbody2D>().simulated=true;
            var weapon=player.gameObject.AddComponent<RapierWeapon>();weapon.Init(stats,player,WeaponKind.Sword);weapon.enabled=false;
            typeof(RapierWeapon).GetMethod("Slash",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(weapon,new object[]{1f});
            Assert.IsNotNull(front.GetComponent<EnemyHitStatus>());Assert.IsNull(behind.GetComponent<EnemyHitStatus>());
            yield return null;
        }
        [UnityTest]public IEnumerator PierceFinal_ActualHitEightEqualDirections20Percent_NoOriginalTargetOrRecursion()
        {
            Final("rifled_barrel");var enemy=Target(1);enemy.GetComponent<Rigidbody2D>().simulated=true;
            Physics2D.SyncTransforms();
            PlayerProjectile.Spawn(player.Position,Vector2.right,9,10,0,root.transform,"bullet",player,true);
            yield return new WaitForSeconds(.16f);
            var ring=root.GetComponentsInChildren<PlayerProjectile>();Assert.AreEqual(8,ring.Length);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var velocities=ring.Select(p=>(Vector2)typeof(PlayerProjectile).GetField("velocity",flags).GetValue(p)).ToArray();
            Assert.AreEqual(8,velocities.Select(v=>Mathf.RoundToInt(Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg/45)).Distinct().Count());
            foreach(var p in ring)
            {
                Assert.AreEqual(2f,typeof(PlayerProjectile).GetField("damage",flags).GetValue(p));
                Assert.AreEqual(false,typeof(PlayerProjectile).GetField("original",flags).GetValue(p));
                Assert.AreEqual(0,typeof(PlayerProjectile).GetField("pierceRemaining",flags).GetValue(p));
            }
            Assert.AreEqual(enemy.MaxHp-10,enemy.Hp,.001f);yield return new WaitForSeconds(.65f);
            Assert.AreEqual(0,root.GetComponentsInChildren<PlayerProjectile>().Length);
            Assert.AreEqual(enemy.MaxHp-10,enemy.Hp,.001f);
        }
        [UnityTest]public IEnumerator SpreadFinal_MainEightPlusTwoSides_MainAllOriginal_SidesNeverOriginal()
        {
            Final("champagne_spray");stats.extraThrusts=7;var enemy=Target(4);
            var weapon=player.gameObject.AddComponent<PlayerProjectileWeapon>();weapon.Init(stats,player);weapon.enabled=false;
            typeof(PlayerProjectileWeapon).GetMethod("Fire",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(weapon,new object[]{enemy});
            var shots=root.GetComponentsInChildren<PlayerProjectile>();Assert.AreEqual(10,shots.Length);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Assert.AreEqual(8,shots.Count(p=>(bool)typeof(PlayerProjectile).GetField("original",flags).GetValue(p)));
            foreach(var p in shots.Where(p=>!(bool)typeof(PlayerProjectile).GetField("original",flags).GetValue(p)))
                Assert.AreEqual(7.2f,(float)typeof(PlayerProjectile).GetField("damage",flags).GetValue(p),.001f);
            yield return null;
        }
        [UnityTest]public IEnumerator ChainFinal_OnlyLastStrikeHas125PercentRange150PercentDamage()
        {
            Final("flambe");stats.extraThrusts=1;var inner=Target(1);
            // Outside even the ordinary query's collider edge, but inside the final query's edge.
            var outer=Target(stats.Range+EnemyDef.Brute.radius+.1f);
            inner.GetComponent<Rigidbody2D>().simulated=true;outer.GetComponent<Rigidbody2D>().simulated=true;
            Physics2D.SyncTransforms();float a=inner.Hp,b=outer.Hp;
            var weapon=player.gameObject.AddComponent<RapierWeapon>();weapon.Init(stats,player);weapon.enabled=false;
            var routine=(IEnumerator)typeof(RapierWeapon).GetMethod("AttackRoutine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(weapon,null);
            weapon.StartCoroutine(routine);Assert.AreEqual(a-12,inner.Hp,.001f);Assert.AreEqual(b,outer.Hp,.001f);
            yield return new WaitForSeconds(.12f);Assert.AreEqual(a-30,inner.Hp,.001f);Assert.AreEqual(b-18,outer.Hp,.001f);
        }
        [UnityTest]public IEnumerator FrozenCombat_ZeroCooldownCannotAttackOrAttach_ProjectileCannotHit()
        {
            stats.flameLevel=1;var e=Target(.5f);e.GetComponent<Rigidbody2D>().simulated=true;Physics2D.SyncTransforms();
            float before=e.Hp;Time.timeScale=0;
            var weapon=player.gameObject.AddComponent<PlayerProjectileWeapon>();weapon.Init(stats,player);
            PlayerProjectile.Spawn(e.transform.position,Vector2.right,9,12,0,root.transform,"bullet",player,true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(before,e.Hp);Assert.IsNull(e.GetComponent<EnemyHitStatus>());
            Assert.AreEqual(1,root.GetComponentsInChildren<PlayerProjectile>().Length,"No zero-cooldown automatic shot while frozen.");
            Time.timeScale=1;yield return new WaitForSeconds(.1f);Assert.Less(e.Hp,before);
        }
    }
}
