using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace CCT.Tests
{
    public class CardEducationPlayTests
    {
        GameObject canvas;
        [UnityTearDown] public IEnumerator Cleanup() { if(canvas!=null)Object.Destroy(canvas);Time.timeScale=1;yield return null; }
        [UnityTest] public IEnumerator LearningDetailNeverChoosesOrChangesPauseAndMatchesCodex()
        {
            canvas=new GameObject("EducationTest",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var select=CardSelectView.Create(canvas.transform);
            var card=CardDatabase.LoadAll().First(c=>c.id=="nettle_tea");
            bool chosen=false;
            select.Show(new List<CardOffer>{new CardOffer{card=card}},_=>chosen=true);
            yield return null;
            float before=Time.timeScale;
            select.OpenLearning(0);yield return null;
            Assert.IsTrue(select.IsLearningOpen);Assert.IsFalse(chosen);
            StringAssert.Contains(card.realWorldFact,select.LearningBodyText);
            Assert.AreEqual(before,Time.timeScale);
            select.Choose(0);Assert.IsFalse(chosen,"Modal must block selection including hotkeys/direct choose");
            select.CloseLearning();yield return null;
            Assert.IsTrue(select.IsOpen);Assert.IsFalse(chosen);
            Assert.AreEqual(before,Time.timeScale);
            select.Choose(0);Assert.IsTrue(chosen);
            var save=PermanentProgress.Current.discoveredCardIds;
            bool had=save.Contains(card.id);if(!had)save.Add(card.id);
            var codex=CardCodexView.Create(canvas.transform,()=>{});codex.Show();
            Assert.IsTrue(codex.OpenDetailFor(card.id));yield return null;
            StringAssert.Contains(CardEducationData.Detail(card),codex.DetailBodyText);
            if(!had)save.Remove(card.id);
        }
        [UnityTest] public IEnumerator RecommendedBlueBlazerFooterFitsInsideFrame()
        {
            canvas=new GameObject("RecommendationLayout",typeof(Canvas),typeof(CanvasScaler));
            var select=CardSelectView.Create(canvas.transform);
            var card=CardDatabase.LoadAll().First(c=>c.id=="blazer");
            select.Show(new List<CardOffer>{new CardOffer{card=card}},_=>{},
                new CardAdviceContext{job=Job.Bartender,weapon=WeaponKind.Rapier,hybridUnlocked=true});
            yield return null;Canvas.ForceUpdateCanvases();
            var footer=select.transform.Find("Card1/Advice").GetComponent<Text>();
            StringAssert.Contains("최종 형태까지 2장",footer.text);
            StringAssert.Contains("러시 1/3",footer.text);
            var rect=footer.rectTransform;
            var frame=rect.parent.GetComponent<RectTransform>();
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            foreach(var corner in corners)
                Assert.IsTrue(frame.rect.Contains(frame.InverseTransformPoint(corner)),"Footer must stay inside card frame");
            var settings=footer.GetGenerationSettings(rect.rect.size);
            settings.resizeTextForBestFit=false;settings.fontSize=footer.resizeTextMinSize;
            Assert.LessOrEqual(new TextGenerator().GetPreferredHeight(footer.text,settings)/footer.pixelsPerUnit,
                rect.rect.height,"Full recommendation must fit, not just be truncated");
            var stacks=select.transform.Find("Card1/Stacks").GetComponent<RectTransform>();
            Assert.Less(rect.anchoredPosition.y+rect.rect.height,stacks.anchoredPosition.y,"Footer must leave room for stacks");
        }
        [UnityTest] public IEnumerator AllCardFactsFitTheirLayoutWithoutOverlappingDetailButton()
        {
            canvas=new GameObject("EducationLayout",typeof(Canvas),typeof(CanvasScaler));
            var select=CardSelectView.Create(canvas.transform);
            foreach(var card in CardDatabase.LoadAll())
            {
                select.Show(new List<CardOffer>{new CardOffer{card=card}},_=>{});
                Canvas.ForceUpdateCanvases();yield return null;
                foreach(var name in new[]{"RealWorldFact","GameConnection"})
                {
                    var t=select.transform.Find("Card1/"+name).GetComponent<Text>();
                    Assert.LessOrEqual(t.preferredHeight,t.rectTransform.rect.height+1,card.id+" "+name);
                }
            }
        }
    }
}
