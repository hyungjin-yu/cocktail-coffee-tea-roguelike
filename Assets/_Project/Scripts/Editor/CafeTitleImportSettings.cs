using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>2026-11-18 카페 입장형 첫 화면 아트(Resources/CafeTitle, 원본 ArtSource/cafe-title-2026-11-18 복사본): 픽셀 아트 UI 스프라이트 설정.
    /// 크기는 layout.json의 정규화 rect가 정하므로 PPU는 UI 기본(100) — 늘림 없이 같은 아트 밀도로 제작된 PNG를 그대로 쓴다.</summary>
    public class CafeTitleImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace(System.IO.Path.DirectorySeparatorChar, '/').Contains("/Resources/CafeTitle/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 100f;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(settings);
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            // 2026-11-20 r5: 실내 물체 레이어는 hover/선택 때 그 물체 픽셀만 은은히 밝힌 사본을 실행 중에 만든다(아트 재생성 없음) → CPU 읽기 허용(480x270 몇 장).
            ti.isReadable = assetPath.Replace(System.IO.Path.DirectorySeparatorChar, '/').Contains("/Resources/CafeTitle/ready/interior/");
        }
    }
}
