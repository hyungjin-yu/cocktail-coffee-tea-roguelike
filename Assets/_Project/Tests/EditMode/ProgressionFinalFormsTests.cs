using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CCT.Tests
{
    public class ProgressionFinalFormsTests
    {
        readonly List<Object> created=new List<Object>();
        List<PermanentSkillData> Nodes(){var list=DefaultPermanentSkills.CreateRuntime();created.AddRange(list);return list;}
        List<CardData> Cards(){var list=DefaultCards.CreateRuntime();list.AddRange(DefaultTeaCards.CreateRuntime());created.AddRange(list);return list;}
        [TearDown]public void Cleanup(){foreach(var item in created)Object.DestroyImmediate(item);created.Clear();}
        [TestCase(0,0,1f,1f)] [TestCase(0,10,1.12f,1.04f)] [TestCase(18,15,1.36f,1.132f)]
        [TestCase(-1,-1,1f,1f)] [TestCase(int.MaxValue,int.MaxValue,1.4f,1.18f)]
        public void Challenge_IndependentFormulaAndCaps(int p,int c,float hp,float damage)
        {
            var run=new RunChallenge(p);run.SetCoffeeCount(c);
            Assert.AreEqual(hp,run.HpMultiplier,.00001f);Assert.AreEqual(damage,run.DamageMultiplier,.00001f);
            Assert.AreEqual((long)Math.Max(0,p)+Math.Max(0,c),run.Stage);
        }
        [Test]public void Challenge_ActualChangeNotifiesOnce_AndNewRunHasNoCoffee()
        {
            var run=new RunChallenge(3);int calls=0;float from=0,to=0;
            run.HpMultiplierChanged+=(a,b)=>{calls++;from=a;to=b;};
            run.SetCoffeeCount(1);run.SetCoffeeCount(1);
            Assert.AreEqual(1,calls);Assert.AreEqual(1.03f,from,.00001);Assert.AreEqual(1.042f,to,.00001);
            Assert.AreEqual(0,new RunChallenge(3).CoffeeCount);Assert.AreEqual(1.054f,run.NextCoffeeHp,.00001);
        }
        [TestCase(WeaponKind.Rapier)] [TestCase(WeaponKind.Sword)] [TestCase(WeaponKind.Gun)] [TestCase(WeaponKind.Bow)]
        public void AllOwned_OnlySelectedWeapon18Apply_Once(WeaponKind weapon)
        {
            var nodes=Nodes();var tree=new PermanentSkillTree(nodes,nodes.Select(n=>n.id),5);
            var stats=new PlayerStats();tree.ApplyAll(stats,weapon);tree.ApplyAll(stats,weapon);
            Assert.AreEqual(18,tree.AppliedCount(weapon));Assert.AreEqual(129,tree.SpentPoints);
            Assert.AreEqual(150,stats.MaxHp);Assert.AreEqual(.4f,stats.beanGainPercent,.00001);
            Assert.AreEqual(weapon==WeaponKind.Sword?.35f:.20f,stats.damagePercent,.00001);
            Assert.AreEqual(weapon==WeaponKind.Bow?.30f:.20f,stats.attackSpeedPercent,.00001);
            Assert.AreEqual(weapon==WeaponKind.Gun||weapon==WeaponKind.Rapier?1:0,stats.extraThrusts);
            Assert.AreEqual(weapon==WeaponKind.Bow?1:0,stats.pierceCount);
        }
        [TestCase("roast_profile",1)] [TestCase("master_service",3)] [TestCase("master_blend",5)]
        public void Purchase_RequiresClearBoundaryAndEveryParent(string id,int clears)
        {
            var nodes=Nodes();var n=nodes.Single(x=>x.id==id);
            var owned=nodes.Where(x=>x.id!=id).Select(x=>x.id).ToList();
            Assert.IsFalse(new PermanentSkillTree(nodes,owned,clears-1).TryUnlock(n,999));
            var tree=new PermanentSkillTree(nodes,owned,clears);Assert.IsTrue(tree.TryUnlock(n,999));Assert.IsFalse(tree.TryUnlock(n,999));
            foreach(var parent in n.Prerequisites)
                Assert.IsFalse(new PermanentSkillTree(nodes,owned.Where(x=>x!=parent.id),clears).TryUnlock(n,999),parent.id);
        }
        [Test]public void InvalidGraph_ExplicitErrorAndNoPurchase_CostsAreNotFree()
        {
            var nodes=Nodes();var root=nodes.Single(n=>n.id=="dark_roast_blend");root.prerequisite=nodes.Single(n=>n.id=="perfect_crema");
            var tree=new PermanentSkillTree(nodes,null,5);Assert.IsTrue(tree.HasDataErrors);Assert.IsFalse(tree.TryUnlock(root,999));
            root.prerequisite=null;
            var legacy=new PermanentSkillTree(nodes,new[]{"perfect_crema","unrecognised_id"});
            Assert.AreEqual(0,legacy.SpentPoints);Assert.IsFalse(legacy.TryUnlock(root,3));Assert.IsTrue(legacy.TryUnlock(root,9));Assert.AreEqual(9,legacy.SpentPoints);
        }
        [Test]public void FinalForm_OnlyMaximumCard_OneUnlockForTwoIds()
        {
            var forms=new AttackFinalForms();int calls=0;forms.Unlocked+=_=>calls++;
            var cards=Cards();var first=cards.Single(c=>c.id=="rifled_barrel");var second=cards.Single(c=>c.id=="piercing_arrowhead");
            forms.OnCardTaken(first,first.maxStacks-1);Assert.IsFalse(forms.Has(AttackFinalForm.Pierce));
            forms.OnCardTaken(first,first.maxStacks);forms.OnCardTaken(first,first.maxStacks);forms.OnCardTaken(second,second.maxStacks);
            Assert.IsTrue(forms.Has(AttackFinalForm.Pierce));Assert.AreEqual(1,calls);Assert.IsFalse(new AttackFinalForms().Has(AttackFinalForm.Pierce));
        }
        [Test]public void RangedPriority_BoundariesOnlyEligible_NoDuplicates_AndMeasuredIncrease()
        {
            var pool=Cards();int priority=0,normalShape=0,earlyShape=0;
            for(int seed=0;seed<1000;seed++)
            {
                var early=new CardSystem(pool,new System.Random(seed)).Draw(CardCategory.Cocktail,true,3,179.99f,5);
                var normal=new CardSystem(pool,new System.Random(seed)).Draw(CardCategory.Cocktail,true,3,180,5);
                priority+=early[0].earlyRangedPriority?1:0;
                earlyShape+=early.Count(o=>AttackFinalForms.ForCard(o.card.id)==AttackFinalForm.Pierce||AttackFinalForms.ForCard(o.card.id)==AttackFinalForm.Spread);
                normalShape+=normal.Count(o=>AttackFinalForms.ForCard(o.card.id)==AttackFinalForm.Pierce||AttackFinalForms.ForCard(o.card.id)==AttackFinalForm.Spread);
                Assert.AreEqual(3,early.Select(o=>o.card.id).Distinct().Count());
                Assert.IsTrue(early.All(o=>o.card.weaponRestriction!=WeaponRestriction.MeleeOnly));
                Assert.IsFalse(normal.Any(o=>o.earlyRangedPriority));
                Assert.IsFalse(new CardSystem(pool,new System.Random(seed)).Draw(CardCategory.Cocktail,true,3,179.99f,6).Any(o=>o.earlyRangedPriority));
                Assert.IsFalse(new CardSystem(pool,new System.Random(seed)).Draw(CardCategory.Cocktail,false,3,0,0).Any(o=>o.earlyRangedPriority));
            }
            Assert.That(priority,Is.InRange(570,630));Assert.Greater(earlyShape,normalShape+350);
            var exhausted=new CardSystem(pool,new System.Random(1));var stats=new PlayerStats();
            foreach(var card in pool.Where(c=>AttackFinalForms.ForCard(c.id)==AttackFinalForm.Pierce||AttackFinalForms.ForCard(c.id)==AttackFinalForm.Spread))
                for(int i=0;i<card.maxStacks;i++)exhausted.Take(card,stats);
            Assert.IsFalse(exhausted.Draw(CardCategory.Cocktail,true,3,0,0).Any(o=>o.earlyRangedPriority));
        }
    }
}
