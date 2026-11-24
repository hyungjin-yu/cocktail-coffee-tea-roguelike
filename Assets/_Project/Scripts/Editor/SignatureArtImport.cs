using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// 2026-11-24 #25 시그니처 아트(ArtSource/signature-drinks-2026-11-24 → Resources/SignatureDrinks, 14 clip·57 frame) 임포트 규칙 재적용·검증.
    /// .meta는 manifest 값으로 미리 작성해 두었으므로 보통은 Validate만 돌리면 된다(Unity 배치에서 실행 — 이 작업 공간에서는 아직 실행하지 않음).
    /// Sprite, Point, Clamp, 무압축, mipmap 없음, NPOT 그대로, 최대 2048. PPU·피벗·테두리는 SpecFor.
    /// </summary>
    public static class SignatureArtImport
    {
        public const string Folder = "Assets/_Project/Resources/SignatureDrinks";

        public struct Spec { public float ppu; public Vector2 pivot; public Vector4 border; }

        public static Spec SpecFor(string clip)
        {
            if (clip.StartsWith("salt_ring")) return new Spec { ppu = 120f, pivot = new Vector2(0.5f, 0.5f) };
            if (clip == "sidecar_wind") return new Spec { ppu = 128f, pivot = new Vector2(0.9375f, 0.5f) };
            if (clip == "sidecar_low") return new Spec { ppu = 128f, pivot = new Vector2(0.5f, 0.5f) };
            if (clip.StartsWith("whiteout_hold")) return new Spec { ppu = 100f, pivot = new Vector2(0.5f, 0f) };
            if (clip.StartsWith("whiteout_edge")) return new Spec { ppu = 100f, pivot = new Vector2(0.5f, 0.5f), border = new Vector4(14, 14, 14, 14) };
            return new Spec { ppu = 100f, pivot = new Vector2(0.5f, 0.5f) };
        }

        static string ClipOf(string fileName) => fileName.Substring(0, fileName.LastIndexOf('_'));

        [MenuItem("CCT/Signature Art/Apply import rules")]
        public static void ImportBatch()
        {
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(Folder, "*.png"))
            {
                var path = file.Replace('\\', '/');
                var spec = SpecFor(ClipOf(Path.GetFileNameWithoutExtension(path)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = spec.ppu;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.spriteBorder = spec.border;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = spec.pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Validate();
            AssetDatabase.SaveAssets();
            Debug.Log("Signature art imported and validated");
        }

        [MenuItem("CCT/Signature Art/Validate")]
        public static void Validate()
        {
            int frames = 0;
            foreach (var clip in SignatureArt.All)
            {
                var sprites = SignatureArt.Load(clip.name);
                if (sprites == null || sprites.Length != clip.durationsMs.Length) throw new InvalidOperationException("Missing signature frames: " + clip.name);
                var spec = SpecFor(clip.name);
                foreach (var s in sprites)
                {
                    if (Mathf.Abs(s.pixelsPerUnit - spec.ppu) > 0.01f) throw new InvalidOperationException("PPU mismatch: " + s.name);
                    if (s.texture.width != clip.framePx.x || s.texture.height != clip.framePx.y) throw new InvalidOperationException("Frame size mismatch: " + s.name);
                    if (s.texture.filterMode != FilterMode.Point) throw new InvalidOperationException("Filter not point: " + s.name);
                    var pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    if ((pivot - spec.pivot).sqrMagnitude > 1e-6f) throw new InvalidOperationException("Pivot mismatch: " + s.name);
                    if ((s.border - spec.border).sqrMagnitude > 1e-6f) throw new InvalidOperationException("Border mismatch: " + s.name);
                    frames++;
                }
            }
            if (frames != SignatureArt.TotalFrames) throw new InvalidOperationException($"Expected {SignatureArt.TotalFrames} signature frames, got {frames}");
            // 솔트 림 표시 지름 = 판정 반경 × 2
            var ring = SignatureArt.Load("salt_ring")[0];
            if (Mathf.Abs(ring.bounds.size.x - SignatureRuntime.SaltRimRadius * 2f) > 0.01f) throw new InvalidOperationException("Salt rim diameter != 2 × radius");
        }
    }
}
