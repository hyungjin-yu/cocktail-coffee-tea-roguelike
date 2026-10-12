using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    // 35: no aura. Enemy-attached direct-hit poison and nonrecursive death propagation.
    public class PoisonAuraTests
    {
        GameObject root;
        PlayerController player;
        PlayerStats stats;
        Enemy Target(float x)
        {
            var e=Enemy.SpawnAt(new Vector2(x,Balance.LaneCenterY),EnemyDef.Brute,10,player,root.transform);
            e.SetDriver(new Stationary());e.GetComponent<Rigidbody2D>().simulated=false;return e;
        }
        class Stationary : IEnemyDriver
        { public bool TryGetVelocity(Vector2 a,Vector2 b,out Vector2 velocity){velocity=Vector2.zero;return true;} }
        [UnitySetUp]public IEnumerator Setup()
        {
            Time.timeScale=1;root=new GameObject("PoisonDirect35");var go=new GameObject("Player");go.transform.SetParent(root.transform);
            player=go.AddComponent<PlayerController>();stats=new PlayerStats {poisonLevel=1};player.Init(stats,Color.white);player.enabled=false;
            yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Object.Destroy(root);yield return null;}
        [UnityTest]public IEnumerator NoHit_NoDamage_AndDirectHitThreeTicksOnly()
        {
            var a=Target(2);var b=Target(3);float before=a.Hp,other=b.Hp;
            yield return new WaitForSeconds(.2f);Assert.AreEqual(before,a.Hp);
            EnemyHitStatus.AttachDirect(a,player);yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(before-5.25f,a.Hp,.001f);Assert.AreEqual(other,b.Hp);
        }
        [UnityTest]public IEnumerator StrongerDamageSurvivesRefresh_AndLockedTimerStops()
        {
            var a=Target(2);stats.poisonLevel=3;EnemyHitStatus.AttachDirect(a,player);
            stats.poisonLevel=1;EnemyHitStatus.AttachDirect(a,player);
            var s=a.GetComponent<EnemyHitStatus>();Assert.AreEqual(3.25f,s.PoisonDps,.001f);
            stats.LockPoison(1);float before=a.Hp,remaining=s.PoisonRemaining;
            yield return new WaitForSeconds(.2f);Assert.AreEqual(before,a.Hp);Assert.AreEqual(remaining,s.PoisonRemaining);
        }
        [UnityTest]public IEnumerator FinalLastPoisonTickDeath_ThreeNearestRecipients_NoRecursivePropagation()
        {
            var card=ScriptableObject.CreateInstance<CardData>();card.id="nettle_tea";card.maxStacks=3;
            player.FinalForms.OnCardTaken(card,3);Object.Destroy(card);
            var a=Target(2);var b=Target(2.4f);var c=Target(2.8f);var d=Target(3.2f);var outside=Target(4.1f);
            a.TakeDamage(a.Hp-5.2f);EnemyHitStatus.AttachDirect(a,player);
            yield return new WaitForSeconds(3.2f);
            Assert.IsTrue(a==null||a.IsDead,"The third and final poison tick must kill and still propagate.");
            foreach(var e in new[]{b,c,d})Assert.AreEqual(.875f,e.GetComponent<EnemyHitStatus>().PoisonDps,.001f);
            Assert.IsNull(outside.GetComponent<EnemyHitStatus>());
            b.TakeDamage(b.Hp);yield return null;
            Assert.IsNull(outside.GetComponent<EnemyHitStatus>(),"Propagated poison cannot repropagate.");
        }
    }
}
