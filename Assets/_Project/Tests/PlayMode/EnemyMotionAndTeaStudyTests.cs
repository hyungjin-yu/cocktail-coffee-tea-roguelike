using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    public class EnemyMotionAndTeaStudyTests
    {
        [UnityTearDown] public IEnumerator Clean()
        {
            if(GameManager.Instance!=null) Object.Destroy(GameManager.Instance.gameObject);
            foreach(var enemy in Enemy.Active.ToArray()) if(enemy!=null) Object.Destroy(enemy.gameObject);
            Time.timeScale=1f; yield return null;
        }
        GameManager Build()
        {
            var game=GameBootstrap.Build(Job.TeaSommelier,WeaponKind.Sword);
            game.GetComponentInChildren<EnemySpawner>().enabled=false; game.Director.enabled=false;
            game.Player.AutoInputOverride=Vector2.zero; return game;
        }
        static void Tick(Enemy enemy) => typeof(Enemy).GetMethod("TickBodyAnimation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(enemy,null);
        [UnityTest] public IEnumerator MovingClipKeepsAdvancingWhenNoPhysicsPositionStepOccurs()
        {
            var game=Build(); yield return null;
            var enemy=Enemy.SpawnAt(game.Player.Position+Vector2.right*3,EnemyDef.TeabagImp,1,game.Player,game.transform);
            var body=enemy.GetComponent<Rigidbody2D>(); body.linearVelocity=Vector2.right;
            Tick(enemy); var clip=enemy.Animator.Current;
            Assert.AreEqual("move",clip.name);
            enemy.Animator.Advance(.12f); var frame=enemy.Animator.FrameIndex;
            Assert.Greater(frame,0);
            for(int i=0;i<4;i++) Tick(enemy);
            Assert.AreSame(clip,enemy.Animator.Current);
            Assert.AreEqual(frame,enemy.Animator.FrameIndex);
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate,body.interpolation);
        }
        [UnityTest] public IEnumerator RangedRetreatKeepsFacingTarget()
        {
            var game=Build(); yield return null;
            var enemy=Enemy.SpawnAt(game.Player.Position+Vector2.right*3,EnemyDef.KettleCannon,1,game.Player,game.transform);
            enemy.GetComponent<Rigidbody2D>().linearVelocity=Vector2.right;
            Tick(enemy);
            Assert.Less(enemy.Animator.transform.localScale.x,0);
        }
        [UnityTest] public IEnumerator TeaStudyAndHiddenHintsUseTeaThemeWithoutChangingSkillIds()
        {
            var game=Build(); yield return null;
            game.OpenCoffeeTree(); yield return null;
            StringAssert.Contains("우림 포인트",game.SkillView.PointsLabel);
            StringAssert.DoesNotContain("커피",game.Hud.CoffeePointsText);
            foreach(var def in HiddenSkillDatabase.All.Where(d=>d.AvailableFor(Job.TeaSommelier)))
                StringAssert.DoesNotContain("커피",def.ConditionFor(Job.TeaSommelier));
            Assert.AreEqual("첫물 우림",JobStudyTheme.Name(game.Skills.Nodes.First(n=>n.id=="espresso_shot"),Job.TeaSommelier));
        }
    }
}
