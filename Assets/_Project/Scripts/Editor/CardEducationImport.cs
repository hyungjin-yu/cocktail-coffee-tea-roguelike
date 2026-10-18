using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace CCT.Editor
{
    public static class CardEducationImport
    {
        public static void ImportReady()
        {
            const string source="ArtSource/card-education-2026-10-16";
            const string target="Assets/_Project/Resources/CardEducation";
            Directory.CreateDirectory(target);
            var cards=DefaultCards.CreateRuntime().Concat(DefaultTeaCards.CreateRuntime()).Concat(DefaultHybridCards.CreateRuntime()).ToList();
            foreach(var id in cards.Select(c=>c.educationArtId).Where(id=>!string.IsNullOrEmpty(id)).Distinct())
            {
                string from=Path.Combine(source,id+".png"),to=target+"/"+id+".png";
                if(!File.Exists(from) || !File.Exists(Path.Combine(source,id+".aseprite")))continue;
                File.Copy(from,to,true);
                AssetDatabase.ImportAsset(to,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(to);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.spritePixelsPerUnit=64;importer.SaveAndReimport();
            }
            foreach(var c in cards)UnityEngine.Object.DestroyImmediate(c);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        public static void Batch() { try { ImportReady(); EditorApplication.Exit(0); } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); } }
        public static void QaBatch()
        {
            try { ImportReady(); ValidateAll(); BuildRelease.Windows64ReleaseQaTourBatch(); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void ValidateAll()
        {
            var cards=Resources.LoadAll<CardData>("Cards");
            if(cards.Length!=35)throw new Exception("Card education requires all 35 serialized cards");
            var ids=cards.Select(c=>c.educationArtId).Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
            if(ids.Length!=16)throw new Exception("Card education requires 16 distinct mapped art IDs");
            foreach(var id in ids)
            {
                string source="ArtSource/card-education-2026-10-16/"+id;
                string target="Assets/_Project/Resources/CardEducation/"+id+".png";
                if(!File.Exists(source+".aseprite") || !File.Exists(source+".png") || !File.Exists(target))throw new Exception("Missing final card art: "+id);
                if(!File.ReadAllBytes(source+".png").SequenceEqual(File.ReadAllBytes(target)))throw new Exception("Stale imported card art: "+id);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                var importer=(TextureImporter)AssetImporter.GetAtPath(target);
                if(texture==null || texture.width!=64 || texture.height!=64 || importer.filterMode!=FilterMode.Point || importer.mipmapEnabled || importer.textureType!=TextureImporterType.Sprite)
                    throw new Exception("Card art settings mismatch: "+id);
            }
            foreach(var c in cards)
                if(string.IsNullOrEmpty(c.realWorldFact) || string.IsNullOrEmpty(c.gameplayConnection) || string.IsNullOrEmpty(c.sourceUrl))throw new Exception("Incomplete education: "+c.id);
            Debug.Log("[CCT] Card education validated: 35 cards, 16 mapped final sprites");
        }
    }
}
