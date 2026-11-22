using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>2026-11-22 레벨업 메뉴판 아트(Resources/LevelUpMenu, 원본 ArtSource/v1/levelup-menu-2026-11-22/ready 복사본):
    /// 인계 규칙 — Single 스프라이트, Point, 밉맵 없음, 무압축, sRGB, 128 PPU, 정수 배율 표시. 건너뛴 편집은 없다.</summary>
    public class LevelUpMenuImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace((char)92, (char)47).Contains("/Resources/LevelUpMenu/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 128f;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(settings);
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
        }
    }
}
