using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    public static class SkinArtImport
    {
        [Serializable] class Source { public string source; public string target; public string skin; }
        [Serializable] class Sources { public Source[] entries; }

        public static void ImportBatch()
        {
            AssetDatabase.Refresh();
            var json=File.ReadAllText("ArtSource/mobile-skins-2026-10-12/sources.json");
            var sources=JsonUtility.FromJson<Sources>("{\"entries\":"+json+"}");
            foreach(var entry in sources.entries)
            {
                var source=Relative(entry.source); var target=Relative(entry.target);
                var original=(TextureImporter)AssetImporter.GetAtPath(source);
                var variant=(TextureImporter)AssetImporter.GetAtPath(target);
                if(original==null || variant==null) throw new InvalidOperationException("Missing skin texture importer: "+target);
                var settings=new TextureImporterSettings(); original.ReadTextureSettings(settings);
                variant.SetTextureSettings(settings); variant.spritePixelsPerUnit=original.spritePixelsPerUnit;
                variant.textureCompression=TextureImporterCompression.Uncompressed;
                variant.filterMode=FilterMode.Point; variant.mipmapEnabled=false; variant.alphaIsTransparency=true;
                variant.SaveAndReimport();
                var oldSprite=AssetDatabase.LoadAssetAtPath<Sprite>(source);
                var newSprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);
                if(oldSprite==null || newSprite==null || oldSprite.pivot!=newSprite.pivot || oldSprite.rect!=newSprite.rect)
                    throw new InvalidOperationException("Skin changes sprite geometry: "+target);
            }
            AssetDatabase.SaveAssets(); Debug.Log("Cosmetic outfit frames imported with original geometry");
        }

        static string Relative(string path)
        {
            var root=Directory.GetCurrentDirectory().Replace('\\','/').TrimEnd('/')+"/";
            var full=Path.GetFullPath(path).Replace('\\','/');
            if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Skin source outside project");
            return full.Substring(root.Length);
        }
    }
}
