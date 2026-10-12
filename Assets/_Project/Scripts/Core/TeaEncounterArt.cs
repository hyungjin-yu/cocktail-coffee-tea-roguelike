using System;
using System.Linq;
using UnityEngine;

namespace CCT
{
    public static class TeaEncounterArt
    {
        public static readonly string[] Ids={"gaiwan_sentinel","steam_kettle_elder","tea_kiln_dragon","spring_leaf_sprite","bamboo_scoop_archer"};
        [Serializable] public class ClipInfo { public string name; public int frames; public float fps; public bool loop; }
        [Serializable] public class Info { public string assetId; public float bodyHeightPx,targetBodyHeightWorld,footPixelY; public ClipInfo[] clips; }
        public static ArtAsset Load(string id)
        {
            if(!Ids.Contains(id)) return null;
            var text=Resources.Load<TextAsset>("ArtExtra/"+id+"/runtime");
            if(text==null) return null;
            var data=JsonUtility.FromJson<Info>(text.text);
            var sprites=Resources.LoadAll<Sprite>("ArtExtra/"+id);
            var clips=data.clips.Select(c=>new ArtClip{name=c.name,fps=c.fps,loop=c.loop,
                frames=sprites.Where(s=>s.name.LastIndexOf('_')>0 && s.name.Substring(0,s.name.LastIndexOf('_'))==id+"_"+c.name).OrderBy(s=>s.name).ToArray()}).ToArray();
            if(clips.Any(c=>c.frames.Length!=data.clips.First(i=>i.name==c.name).frames)) return null;
            var anchors=clips.SelectMany(c=>Enumerable.Range(0,c.frames.Length).SelectMany(i=>new[]{
                new ArtAnchor{clip=c.name,frame=i,name="feet",px=new Vector2(64,data.footPixelY)},
                new ArtAnchor{clip=c.name,frame=i,name="muzzle",px=new Vector2(104,62)}})).ToArray();
            return new ArtAsset{assetId=id,kind="body",status="ready",sourcePath="ArtSource/tea-encounters-2026-10-12/"+id,
                pixelsPerUnit=128,frameSizePx=new Vector2(128,128),pivotNormalized=new Vector2(.5f,data.footPixelY/128),
                bodyHeightPx=data.bodyHeightPx,targetBodyHeightWorld=data.targetBodyHeightWorld,
                contentRectNormalized=new Rect(0,data.footPixelY/128,1,data.bodyHeightPx/128),clips=clips,anchors=anchors};
        }
    }
}
