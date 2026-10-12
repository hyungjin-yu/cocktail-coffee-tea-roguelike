using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    // User change 35 replaces the removed automatic burst with direct-hit status.
    public class FlameBurstTests
    {
        GameObject root;
        PlayerController player;
        PlayerStats stats;
        Enemy Target(float x = 2f)
        {
            var e = Enemy.SpawnAt(new Vector2(x, Balance.LaneCenterY), EnemyDef.Brute, 10f, player, root.transform);
            e.enabled = false; e.GetComponent<Rigidbody2D>().simulated = false;
            return e;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;root=new GameObject("DirectStatus35");
            var go=new GameObject("Player");go.transform.SetParent(root.transform);
            go.transform.position=new Vector2(0,Balance.LaneCenterY);
            player=go.AddComponent<PlayerController>();stats=new PlayerStats { flameLevel=1 };
            player.Init(stats,Color.white);player.enabled=false;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator WithoutDirectHit_NoAutomaticDamageOrFloorVfx()
        {
            var e=Target();float before=e.Hp;yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(before,e.Hp);Assert.IsNull(e.GetComponent<EnemyHitStatus>());
            Assert.AreEqual(0,Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator DirectHit_OnlyTarget_ThreeExactTicks_RefreshNotStack()
        {
            var hit=Target();var other=Target(3f);float before=hit.Hp,untouched=other.Hp;
            EnemyHitStatus.AttachDirect(hit,player);EnemyHitStatus.AttachDirect(hit,player);
            var s=hit.GetComponent<EnemyHitStatus>();Assert.AreEqual(2.5f,s.FlameDps,.0001f);
            yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(before-7.5f,hit.Hp,.001f);Assert.AreEqual(untouched,other.Hp);
            Assert.AreEqual(0,s.FlameRemaining);Assert.IsNotNull(hit.transform.Find("FlameHitMarker"));
        }
        [UnityTest] public IEnumerator LockAndPause_PreserveRemaining_RejectNewAttachment()
        {
            var hit=Target();var other=Target(3f);EnemyHitStatus.AttachDirect(hit,player);
            var s=hit.GetComponent<EnemyHitStatus>();stats.LockFlame(2);
            float before=hit.Hp,remaining=s.FlameRemaining;EnemyHitStatus.AttachDirect(other,player);
            yield return new WaitForSeconds(.3f);Assert.AreEqual(before,hit.Hp);Assert.AreEqual(remaining,s.FlameRemaining);
            Assert.IsNull(other.GetComponent<EnemyHitStatus>());
            stats.Tick(2);Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(remaining,s.FlameRemaining);Time.timeScale=1;
            yield return new WaitForSeconds(1.1f);Assert.AreEqual(before-2.5f,hit.Hp,.001f);
        }
        [UnityTest] public IEnumerator MaximumCard_ConcentratedFlame_FourTicksAt150Percent()
        {
            var card=ScriptableObject.CreateInstance<CardData>();card.id="molotov";card.maxStacks=3;
            player.FinalForms.OnCardTaken(card,3);Object.Destroy(card);
            var e=Target();float before=e.Hp;EnemyHitStatus.AttachDirect(e,player);
            Assert.AreEqual(3.75f,e.GetComponent<EnemyHitStatus>().FlameDps,.001f);
            yield return new WaitForSeconds(4.2f);Assert.AreEqual(before-15f,e.Hp,.001f);
        }
    }
}
