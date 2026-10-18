#if CCT_QA_TOUR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CCT
{
    /// <summary>Explicit QA player only: isolated save, actual runtime UI capture, no production inclusion.</summary>
    public class CardEducationCapture : MonoBehaviour
    {
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-cct-education-capture");
            if(i<0 || i+1>=args.Length || Array.IndexOf(args,"-cct-data-dir")<0)return;
            var go=new GameObject("CardEducationCapture");DontDestroyOnLoad(go);
            var v=go.AddComponent<CardEducationCapture>();v.output=args[i+1];v.StartCoroutine(v.Run());
        }
        IEnumerator Shot(string label)
        {
            yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,label+".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator Run()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(2);
            var args=Environment.GetCommandLineArgs();
            int w=Array.IndexOf(args,"-cct-capture-width"),h=Array.IndexOf(args,"-cct-capture-height");
            if(w>=0 && h>=0) { Screen.SetResolution(int.Parse(args[w+1]),int.Parse(args[h+1]),FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(2); }
            foreach(var title in FindObjectsByType<TitleView>(FindObjectsSortMode.None))Destroy(title.gameObject);
            yield return null;
            var game=GameBootstrap.Build(Job.Bartender,WeaponKind.Rapier);
            yield return null;
            foreach(var spawner in game.GetComponentsInChildren<EnemySpawner>())spawner.enabled=false;
            if(game.Director!=null)game.Director.enabled=false;
            var cards=CardDatabase.LoadAll().ToDictionary(c=>c.id);
            game.SetComboTarget(HiddenSkill.BitterBlend);
            game.AddIngredient(IngredientType.Cocktail,game.CocktailGauge.Threshold);
            yield return null;yield return null;
            if(game.CardView.IsTutorialOpen)
            {
                for(int step=0;step<4;step++){yield return Shot("tutorial-automatic-"+step);game.CardView.Tutorial.Next();}
                if(!game.CardView.IsOpen || Time.timeScale!=0)throw new Exception("Tutorial resumed or selected");
            }
            var groups=new[]{new[]{"dry_gin","shot_glass","dry_martini"},new[]{"ginseng_root","nettle_tea","aged_puerh"},new[]{"blazer","mixing_tempo","nightshade_blend"}};
            bool chosen=false;
            for(int g=0;g<groups.Length;g++)
            {
                game.CardView.Show(groups[g].Select(id=>new CardOffer{card=cards[id],currentStacks=0}).ToList(),_=>chosen=true);
                if(g<2)
                {
                    game.CardView.OpenTutorial();
                    for(int step=0;step<4;step++){yield return Shot("tutorial-"+g+"-"+step);game.CardView.Tutorial.Next();}
                }
                yield return Shot("select-"+g);
                game.CardView.OpenLearning(0);yield return Shot("learning-"+g);
                game.CardView.Choose(0);
                if(chosen)throw new Exception("Learning modal selected a card");
                game.RequestEscape();
                if(!game.CardView.IsOpen || game.CardView.IsLearningOpen || Time.timeScale!=0)throw new Exception("Learning close leaked pause or card state");
            }
            // Capture every authored card in the actual same select UI, checking text mesh height.
            int clipped=0;
            foreach(var c in cards.Values)
            {
                game.CardView.Show(new List<CardOffer>{new CardOffer{card=c}},_=>{});Canvas.ForceUpdateCanvases();
                foreach(var name in new[]{"Desc","RealWorldFact","GameConnection","Name"})
                {
                    var t=game.CardView.transform.Find("Card1/"+name).GetComponent<Text>();
                    if(t.preferredHeight>t.rectTransform.rect.height+1){clipped++;Debug.LogError("Education text overflow: "+c.id+"/"+name+" "+t.preferredHeight+">"+t.rectTransform.rect.height);}
                }
                game.CardView.OpenTutorial();
                for(int step=0;step<4;step++)
                {
                    Canvas.ForceUpdateCanvases();var text=game.CardView.Tutorial.transform.Find("Panel/Body").GetComponent<Text>();
                    if(text.preferredHeight>text.rectTransform.rect.height+1){clipped++;Debug.LogError("Tutorial overflow: "+c.id+"/"+step);}
                    game.CardView.Tutorial.Next();
                }
            }
            game.CardView.gameObject.SetActive(false);
            var canvas=UiFactory.CreateCanvas(null,"EducationCodexQA",500);
            var codex=CardCodexView.Create(canvas.transform,()=>{});codex.Show();
            yield return Shot("codex-undiscovered");
            codex.OpenDetailFor("nettle_tea");yield return Shot("codex-undiscovered-detail");codex.HideDetail();
            foreach(var c in cards.Values)PermanentProgress.DiscoverCard(c.id);
            codex.Show();yield return Shot("codex-discovered");
            codex.OpenDetailFor("aged_puerh");yield return Shot("codex-puerh-detail");
            var scroll=codex.transform.Find("Detail/DetailScroll").GetComponent<ScrollRect>();scroll.verticalNormalizedPosition=0;
            yield return Shot("codex-puerh-source");
            File.WriteAllText(Path.Combine(output,"capture-result.json"),JsonUtility.ToJson(new Result{width=Screen.width,height=Screen.height,touch=MobilePlatform.IsTouchUI,overflowCount=clipped,modalSelectedCard=chosen,cardCount=cards.Count},true));
            Application.Quit(clipped==0?0:1);
        }
        [Serializable] class Result { public int width,height,overflowCount,cardCount;public bool touch,modalSelectedCard; }
    }
}
#endif
