using System.IO;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// Assets/_Project/Art/v1 아래 PNG에만 04 §9 임포트 설정을 강제한다:
    /// Single Sprite, 128 PPU, Point, mipmap off, 무압축, 입력 알파 유지, FullRect(프레임 캔버스·피벗 보존).
    /// 피벗 결정은 ArtV1ImportRules가 한다 (몸체 0.5/0.125, VFX·UI는 매니페스트).
    /// 기존 Resources/Icons 경로는 IconImportSettings가 그대로 담당하고 여기서는 건드리지 않는다.
    /// 임포트 자체를 막을 수는 없으므로 needs_fix·매니페스트 누락은 콘솔 에러로 알린다.
    /// </summary>
    public class ArtV1ImportSettings : AssetPostprocessor
    {
        // 규칙을 바꾸면 올린다 — Unity가 해당 텍스처를 다시 임포트한다.
        public override uint GetVersion() => 2; // 2: Art/firstpass 루트 추가(기획 20)

        void OnPreprocessTexture()
        {
            if (!ArtV1ImportRules.IsArtV1Png(assetPath)) return;

            ArtV1Manifest manifest = null;
            string manifestError = null;
            string manifestPath = ArtV1ImportRules.ManifestPathFor(assetPath);
            if (manifestPath != null && File.Exists(manifestPath))
            {
                manifest = ArtV1Manifest.Parse(File.ReadAllText(manifestPath), out manifestError);
                // 매니페스트를 바꾸면 이 텍스처도 다시 임포트되게 의존성을 건다.
                context.DependsOnSourceAsset(manifestPath);
            }

            var plan = ArtV1ImportRules.Resolve(assetPath, manifest, manifestError);
            var importer = (TextureImporter)assetImporter;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = plan.PixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(plan.PivotX, plan.PivotY);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteBorder = plan.BorderPx != null
                ? new Vector4(plan.BorderPx[0], plan.BorderPx[1], plan.BorderPx[2], plan.BorderPx[3])
                : Vector4.zero;
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.sRGBTexture = true;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = true;
            settings.readable = false;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            foreach (var e in plan.Errors) Debug.LogError($"[Art v1 import] {assetPath}: {e}");
            foreach (var w in plan.Warnings) Debug.LogWarning($"[Art v1 import] {assetPath}: {w}");
        }
    }
}
