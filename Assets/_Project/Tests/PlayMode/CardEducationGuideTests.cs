using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace CCT.Tests
{
    public class CardEducationGuideTests
    {
        string folder; GameManager game;
        [UnitySetUp] public IEnumerator Setup()
        {
            folder=Path.Combine(Application.temporaryCachePath,"education-guide-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder,"save.json"));
            SettingsService.SetForTests(Path.Combine(folder,"settings.json"),new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            CardEducationGuide.SuppressAutomaticForTests=false;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(game!=null)UnityEngine.Object.Destroy(game.gameObject);
            Time.timeScale=1;yield return null;
            CardEducationGuide.SuppressAutomaticForTests=true;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath,new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            Directory.Delete(folder,true);
        }
        IEnumerator Offer()
        {
            game=GameBootstrap.Build(Job.Bartender,WeaponKind.Rapier);yield return null;
            foreach(var s in game.GetComponentsInChildren<EnemySpawner>())s.enabled=false;
            if(game.Director!=null)game.Director.enabled=false;
            game.AddIngredient(IngredientType.Cocktail,game.CocktailGauge.Threshold);yield return null;yield return null;
        }
        [UnityTest] public IEnumerator FirstActualOffer_UsesActualCardAndArt_BlocksChoice_PersistsThenReplays()
        {
            FirstRunTutorial.MarkSeen(); // Existing combat tutorial completion must not suppress new education.
            yield return Offer();var view=game.CardView;var guide=view.Tutorial;
            Assert.IsTrue(view.IsTutorialOpen);Assert.AreEqual(view.Offers[0].card.id,guide.CardId);
            Assert.AreSame(CardEducationIcons.For(view.Offers[0].card),guide.ShownSprite);
            float time=game.RunTime;var chosen=view.Offers[0].card;
            view.Choose(0);Assert.IsTrue(view.IsOpen);Assert.AreEqual(0,game.Player.Stats.flameLevel);
            Assert.AreEqual(GameState.LevelUp,game.State);Assert.AreEqual(0,Time.timeScale);
            game.OpenPause();guide.Close();guide.Next();view.Choose(0);
            Assert.IsTrue(view.IsTutorialOpen);Assert.AreEqual(0,guide.Step);
            game.ClosePause();Assert.AreEqual(0,Time.timeScale);
            guide.Next();guide.Previous();Assert.AreEqual(0,guide.Step);
            guide.Next();guide.Next();guide.Next();guide.OpenDetail();
            Assert.IsFalse(view.IsTutorialOpen);Assert.IsTrue(view.IsLearningOpen);
            StringAssert.Contains(chosen.realWorldFact,view.LearningBodyText);
            view.Choose(0);Assert.IsTrue(view.IsOpen);
            game.RequestEscape();Assert.IsFalse(view.IsLearningOpen);Assert.AreEqual(GameState.LevelUp,game.State);
            Assert.AreEqual(time,game.RunTime);Assert.AreEqual(0,Time.timeScale);
            Assert.AreEqual(CardEducationGuide.Version,SettingsService.Current.cardEducationGuideVersion);Assert.IsTrue(FirstRunTutorial.Seen);
            SettingsService.SetForTests(Path.Combine(folder,"settings.json"),new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            Assert.IsFalse(CardEducationGuide.NeedsIntroduction);Assert.IsTrue(FirstRunTutorial.Seen);
            view.Show(view.Offers.ToList(),_=>{});Assert.IsFalse(view.IsTutorialOpen);
            view.OpenTutorial();Assert.IsTrue(view.IsTutorialOpen);game.RequestEscape();Assert.IsFalse(view.IsTutorialOpen);
            Assert.AreEqual(0,Time.timeScale);
        }
        [UnityTest] public IEnumerator EveryTutorialStepFits_UsesOnlyOfferedSprite_AndPauseCannotDismissIt()
        {
            yield return Offer();game.CardView.CloseLearning();
            foreach(var card in CardDatabase.LoadAll())
            {
                game.CardView.Show(new System.Collections.Generic.List<CardOffer>{new CardOffer{card=card}},_=>Assert.Fail("Tutorial chose card"));
                game.CardView.OpenTutorial();var guide=game.CardView.Tutorial;
                Assert.AreEqual(card.id,guide.CardId);Assert.AreSame(CardEducationIcons.For(card),guide.ShownSprite);
                for(int i=0;i<4;i++)
                {
                    Canvas.ForceUpdateCanvases();var text=guide.transform.Find("Panel/Body").GetComponent<Text>();
                    foreach(var label in guide.GetComponentsInChildren<Text>())
                        StringAssert.DoesNotContain("실제",label.text,card.id+" guide step "+i);
                    Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+1,card.id+" guide step "+i);
                    game.CardView.Choose(0);Assert.IsTrue(game.CardView.IsOpen);
                    if(i<3)guide.Next();
                }
                guide.Close();
            }
            Assert.AreEqual(0,Time.timeScale);
        }
    }
}
