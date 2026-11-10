using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>2026-11-11 마스터 도전 아트(ArtSource/master-challenge-2026-11-11 manifest 15 clip·83 frame) 임포트.
    /// PPU 128, Point, Clamp, 무압축, mipmap 없음, 알파 투명. pivot: NPC (.5,.125) / 현수막 (.5,1) / 품질 VFX (.5,.5).</summary>
    public static class MasterChallengeArtImport
    {
        public const string Folder = "Assets/_Project/Resources/MasterChallenge";

        public static Vector2 PivotFor(string fileName) =>
            fileName.StartsWith("npc_") ? new Vector2(0.5f, 0.125f) : fileName.StartsWith("banner_") ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);

        public static void ImportBatch()
        {
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(Folder, "*.png"))
            {
                var path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = PivotFor(Path.GetFileName(path));
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Validate();
            AssetDatabase.SaveAssets();
            Debug.Log("Master challenge art imported and validated");
        }

        public static void Validate()
        {
            int frames = 0;
            foreach (var clip in MasterArt.AllClips)
            {
                var sprites = MasterArt.Load(clip.name);
                if (sprites == null || sprites.Length != clip.durationsMs.Length)
                    throw new InvalidOperationException("Missing master challenge frames: " + clip.name);
                foreach (var s in sprites)
                {
                    if (Mathf.Abs(s.pixelsPerUnit - 128f) > 0.01f) throw new InvalidOperationException("PPU mismatch: " + s.name);
                    var tex = s.texture;
                    if (tex.width != clip.canvas.x || tex.height != clip.canvas.y) throw new InvalidOperationException("Canvas mismatch: " + s.name);
                    if (tex.filterMode != FilterMode.Point) throw new InvalidOperationException("Filter not point: " + s.name);
                    var pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    if ((pivot - PivotFor(s.name)).sqrMagnitude > 1e-6f) throw new InvalidOperationException("Pivot mismatch: " + s.name);
                    frames++;
                }
            }
            if (frames != 83) throw new InvalidOperationException("Expected 83 master challenge frames, got " + frames);
        }
    }
}
