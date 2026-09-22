using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// Resources/Icons 아래 텍스처는 항상 스프라이트로 임포트되게 강제한다.
    /// 수동으로 Inspector에서 Texture Type을 바꿀 필요 없이 파일만 넣으면 바로 쓸 수 있게 하기 위함.
    /// PPU는 원본 가로 픽셀 수로 맞춰서(정사각형 아이콘 전제) 1장 = 1유닛 기준이 항상 유지되게 한다
    /// — 512px 재료 아이콘과 1024px 몹 아이콘이 섞여 있어도 코드의 uniform scale 로직이 그대로 통한다.
    /// </summary>
    public class IconImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Icons/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            if (width > 0) importer.spritePixelsPerUnit = width;
            // 1024px 원본을 인게임에서 20~30px 수준으로 작게 그리므로(캐릭터 스케일), 밉맵이 없으면
            // GPU가 풀해상도 텍스처를 그대로 다운샘플링하면서 체커보드 같은 앨리어싱/모아레 무늬가 생긴다.
            // 밉맵을 켜서 미리 필터링된 축소본을 쓰게 한다.
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true; // 밉 생성 시 투명 영역 색이 경계로 번지는 것 방지
            // Compressed(BC 계열)는 알파 경계 아티팩트 위험이 있어 무압축으로 고정. 아이콘 몇 장 수준이라 메모리 부담 무시할만함.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
