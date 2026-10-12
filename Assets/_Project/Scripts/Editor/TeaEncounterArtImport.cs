using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    public static class TeaEncounterArtImport
    {
        public static void ImportBatch()
        {
            AssetDatabase.Refresh();
            foreach(var id in TeaEncounterArt.Ids)
            {
                var folder="Assets/_Project/Resources/ArtExtra/"+id;
                var info=JsonUtility.FromJson<TeaEncounterArt.Info>(File.ReadAllText(folder+"/runtime.json"));
                foreach(var file in Directory.GetFiles(folder,"*.png"))
                {
                    var path=file.Replace('\\','/');
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=128;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
                    var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,info.footPixelY/128);
                    settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                }
            }
            foreach(var file in Directory.GetFiles("Assets/_Project/Resources/ArtExtra/zone_tea_pool_v2","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=128; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
                var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Center; settings.spritePivot=new Vector2(.5f,.5f);
                settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            Validate(); AssetDatabase.SaveAssets(); Debug.Log("Tea encounter art imported and validated");
        }
        public static void Validate()
        {
            foreach(var id in TeaEncounterArt.Ids)
                if(TeaEncounterArt.Load(id)==null) throw new InvalidOperationException("Missing tea encounter body clips: "+id);
            if(Resources.LoadAll<Sprite>("ArtExtra/zone_tea_pool_v2").Length!=4)
                throw new InvalidOperationException("Missing tea infusion zone frames");
        }
    }
}
