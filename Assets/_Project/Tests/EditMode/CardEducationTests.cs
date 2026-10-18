using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace CCT.Tests
{
    public class CardEducationTests
    {
        [Test] public void SerializedCardsAndDefaultsHaveIdenticalEducationAndMechanics()
        {
            var defaults=DefaultCards.CreateRuntime().Concat(DefaultTeaCards.CreateRuntime()).Concat(DefaultHybridCards.CreateRuntime()).ToList();
            var assets=Resources.LoadAll<CardData>("Cards").ToDictionary(c=>c.id);
            Assert.AreEqual(35,assets.Count);Assert.AreEqual(35,defaults.Count);
            foreach(var d in defaults)
            {
                Assert.IsTrue(assets.ContainsKey(d.id));var a=assets[d.id];
                Assert.AreEqual(d.displayName,a.displayName,d.id);
                Assert.AreEqual(d.description,a.description,d.id);
                Assert.AreEqual(d.flavorText,a.flavorText,d.id);
                Assert.AreEqual(d.category,a.category);Assert.AreEqual(d.rarity,a.rarity);
                Assert.AreEqual(d.maxStacks,a.maxStacks);Assert.AreEqual(d.weaponRestriction,a.weaponRestriction);
                CollectionAssert.AreEqual(d.effects,a.effects,d.id);
                Assert.AreEqual(d.realWorldFact,a.realWorldFact,d.id);
                Assert.AreEqual(d.gameplayConnection,a.gameplayConnection,d.id);
                Assert.AreEqual(d.additionalSources,a.additionalSources,d.id);
                Assert.AreEqual(d.sourceName,a.sourceName,d.id);Assert.AreEqual(d.sourceUrl,a.sourceUrl,d.id);
                Assert.AreEqual(d.educationArtId,a.educationArtId,d.id);
                Assert.IsNotEmpty(a.realWorldFact);Assert.IsNotEmpty(a.gameplayConnection);Assert.IsNotEmpty(a.sourceName);
                Assert.IsTrue(Uri.TryCreate(a.sourceUrl,UriKind.Absolute,out var uri) && uri.Scheme=="https",d.id);
                StringAssert.Contains("음료 지식",CardEducationData.Detail(a));
                StringAssert.DoesNotContain("실제",CardEducationData.Detail(a));
                StringAssert.Contains("게임 해석",CardEducationData.Detail(a));
                StringAssert.Contains(CardEducationData.SymbolNotice,CardEducationData.Detail(a));
                Assert.LessOrEqual(CardEducationSummary.Fact(a).Length,65,d.id+" concise fact");
                Assert.LessOrEqual(CardEducationSummary.Connection(a).Length,32,d.id+" concise connection");
                UnityEngine.Object.DestroyImmediate(d);
            }
        }
        [Test] public void BotanicalAndOxidationDistinctionsAreExplicit()
        {
            var c=CardDatabase.LoadAll().ToDictionary(x=>x.id);
            StringAssert.Contains("효소적 산화",c["green_tea"].realWorldFact);
            StringAssert.Contains("미생물 발효와 다르",c["oolong"].realWorldFact);
            StringAssert.Contains("악퇴",c["aged_puerh"].realWorldFact);
            StringAssert.Contains("무조건",c["aged_puerh"].realWorldFact);
            StringAssert.Contains("카모마일 꽃",c["chamomile"].realWorldFact);
            StringAssert.Contains("찻나무 잎",c["chamomile"].realWorldFact);
            StringAssert.Contains("Aspalathus linearis",c["ginseng_root"].realWorldFact);
            foreach(var id in new[]{"nettle_tea","nightshade_blend"})
            { StringAssert.Contains("떫은 여운",c[id].displayName);StringAssert.Contains("여운 피해",c[id].description);Assert.AreEqual(CardEffectType.PoisonLevel,c[id].effects[0].type); }
            foreach(var card in c.Values)
                foreach(var bad in new[]{"쐐기풀차","가지속 블렌드","산삼 뿌리","반쯤 발효","화염병 칵테일","독하게, 화끈하게"})
                    StringAssert.DoesNotContain(bad,card.displayName+card.description+card.flavorText);
            Assert.AreEqual(10,(int)CardEffectType.PoisonLevel);
        }
        [Test] public void HiddenCardRequirementLabelsResolveToCurrentCardNames()
        {
            var cards=CardDatabase.LoadAll().ToDictionary(c=>c.id);
            foreach(var def in HiddenSkillDatabase.All)
            {
                var text=def.condition+def.hint+def.teaCondition+def.teaHint;
                foreach(var old in new[]{"쐐기풀차","가지속 블렌드","산삼 뿌리","샷 글래스","화염병 칵테일","플람베","숙성 보이차"})StringAssert.DoesNotContain(old,text);
            }
            Assert.AreEqual("지거",cards["shot_glass"].displayName);
            Assert.AreEqual("티 마티니 · 티니 보우",cards["hybrid_tea_martini"].displayName);
        }
        [Test] public void AllSixteenFinalIconsAreImportedAndUsedWithHonestFallbacks()
        {
            CCT.Editor.CardEducationImport.ValidateAll();
            var cards=CardDatabase.LoadAll();
            Assert.AreEqual(16,cards.Select(c=>c.educationArtId).Where(id=>!string.IsNullOrEmpty(id)).Distinct().Count());
            foreach(var c in cards)
            {
                var sprite=CardEducationIcons.For(c);
                if(!string.IsNullOrEmpty(c.educationArtId))
                {
                    Assert.AreSame(Resources.Load<Sprite>("CardEducation/"+c.educationArtId),sprite,c.id);
                    Assert.AreEqual(64,sprite.texture.width);Assert.AreEqual(64,sprite.texture.height);
                    Assert.AreEqual(FilterMode.Point,sprite.texture.filterMode);
                }
                else Assert.AreSame(CardSelectView.CategoryIcon(c.category),sprite,c.id+" category fallback");
            }
        }
    }
}
