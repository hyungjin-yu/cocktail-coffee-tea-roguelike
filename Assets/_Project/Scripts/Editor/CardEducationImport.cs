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
        /// <summary>2026-11-12 UI 아이콘 묶음(아트 담당 매니페스트). status=ready·group=card 항목만 카드 그림으로 인정한다.</summary>
        public const string UiIconsSource="ArtSource/ui-icons-2026-11-12";
        [Serializable] class UiIconItem { public string id, group, artId, status, sha256; }
        [Serializable] class UiIconManifest { public UiIconItem[] items; }
        public static string[] ReadyUiIconCardArtIds()
        {
            string path=Path.Combine(UiIconsSource,"manifest.json");
            if(!File.Exists(path))return new string[0];
            var m=JsonUtility.FromJson<UiIconManifest>(File.ReadAllText(path));
            return (m?.items??new UiIconItem[0]).Where(i=>i.status=="ready" && i.group=="card" && !string.IsNullOrEmpty(i.artId)).Select(i=>i.artId).Distinct().ToArray();
        }
        /// <summary>원래 16종(2026-10-16) + 카드 에셋에 실제 연결된 새 묶음 그림 수(각각 매니페스트 ready여야 함 — ValidateAll이 검사).</summary>
        public static int ExpectedMappedArtIds()
        {
            var ready=ReadyUiIconCardArtIds();
            return 16 + Resources.LoadAll<CardData>("Cards").Select(c=>c.educationArtId).Where(id=>!string.IsNullOrEmpty(id) && !File.Exists("ArtSource/card-education-2026-10-16/"+id+".png")).Distinct().Count();
        }
        public static void ValidateAll()
        {
            var cards=Resources.LoadAll<CardData>("Cards");
            if(cards.Length!=35)throw new Exception("Card education requires all 35 serialized cards");
            var ids=cards.Select(c=>c.educationArtId).Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
            var readyNew=ReadyUiIconCardArtIds();
            int original=ids.Count(id=>File.Exists("ArtSource/card-education-2026-10-16/"+id+".png"));
            if(original!=16)throw new Exception($"Card education requires the 16 original mapped art IDs, found {original}");
            foreach(var id in ids)
                if(!File.Exists("ArtSource/card-education-2026-10-16/"+id+".png") && !readyNew.Contains(id))
                    throw new Exception("Card art is not an original final sprite nor a ready ui-icons manifest item: "+id);
            foreach(var id in ids)
            {
                bool fromUiIcons=readyNew.Contains(id);
                string source=fromUiIcons ? UiIconsSource+"/"+id : "ArtSource/card-education-2026-10-16/"+id;
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
            Debug.Log($"[CCT] Card education validated: 35 cards, {ids.Length} mapped final sprites ({ids.Length - original} from {UiIconsSource} ready)");
        }
    }
}
