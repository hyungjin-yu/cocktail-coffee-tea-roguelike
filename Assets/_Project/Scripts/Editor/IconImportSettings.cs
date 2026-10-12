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
            if (assetPath.Contains("/Resources/ArtExtra/tea_sommelier_sword_swing/"))
            {
                // 2026-10-09 사용자 요청 검 휘두르기(ArtSource/melee-swing-2026-10-09/manifest.json): 몸체 v1 아트와 같은 픽셀 설정,
                // 256 캔버스 안에서 캐릭터를 -44/+16px 옮겼으므로 발 위치 (84,48)을 pivot으로 쓴다.
                var swing = (TextureImporter)assetImporter;
                swing.textureType = TextureImporterType.Sprite;
                swing.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                swing.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(84f / 256f, 48f / 256f);
                settings.spritePixelsPerUnit = 128f;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                swing.SetTextureSettings(settings);
                swing.mipmapEnabled = false;
                swing.filterMode = FilterMode.Point;
                swing.wrapMode = TextureWrapMode.Clamp;
                swing.alphaIsTransparency = true;
                swing.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (assetPath.Contains("/Resources/ArtExtra/weapon_bow_v2/"))
            {
                // 2026-10-09 사용자 피드백 활 원본 최소 보정 v2(ArtSource/weapon-bow-v2-2026-10-09): v1 weapon_bow와 같은 캔버스·pivot·PPU.
                var bow = (TextureImporter)assetImporter;
                bow.textureType = TextureImporterType.Sprite;
                bow.spriteImportMode = SpriteImportMode.Single;
                var bs = new TextureImporterSettings();
                bow.ReadTextureSettings(bs);
                bs.spriteAlignment = (int)SpriteAlignment.Custom;
                bs.spritePivot = new Vector2(0.296875f, 0.5f);
                bs.spritePixelsPerUnit = 128f;
                bs.spriteMeshType = SpriteMeshType.FullRect;
                bow.SetTextureSettings(bs);
                bow.mipmapEnabled = false;
                bow.filterMode = FilterMode.Point;
                bow.wrapMode = TextureWrapMode.Clamp;
                bow.alphaIsTransparency = true;
                bow.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (assetPath.Contains("/Resources/ArtExtra/status_") || assetPath.Contains("/Resources/ArtExtra/zone_") ||
                assetPath.Contains("/Resources/ArtExtra/bg_")) // 47 직업 맵(벽·바닥 반복은 FullRect 필요)
            {
                // 2026-10-12 42/43: PixelLab(Aseprite 확장) 제작 상태 표식·몬스터 장판 v2 프레임(ArtSource/status-zone-vfx-2026-10-12).
                // 가운데 pivot, 128 PPU(64px 표식 = 0.5 유닛, 판정 맞춤은 코드에서 배율), 점 필터·밉맵 없음·무압축.
                var vfx = (TextureImporter)assetImporter;
                vfx.textureType = TextureImporterType.Sprite;
                vfx.spriteImportMode = SpriteImportMode.Single;
                var vs = new TextureImporterSettings();
                vfx.ReadTextureSettings(vs);
                vs.spriteAlignment = (int)SpriteAlignment.Center;
                // 47 직업 맵 bg_*: PixelLab 계정 최대 256px → 절반 해상도로 만들어 64 PPU(기존 카페 배경과 같은 월드 크기, 배경 픽셀 2배)
                vs.spritePixelsPerUnit = assetPath.Contains("/Resources/ArtExtra/bg_") ? 64f : 128f;
                vs.spriteMeshType = SpriteMeshType.FullRect;
                vfx.SetTextureSettings(vs);
                vfx.mipmapEnabled = false;
                vfx.filterMode = FilterMode.Point;
                vfx.wrapMode = TextureWrapMode.Clamp;
                vfx.alphaIsTransparency = true;
                vfx.isReadable = true; // ZoneVisualV2가 프레임 불투명 영역을 실측해 판정 원에 맞춘다(작은 텍스처)
                vfx.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (!assetPath.Contains("/Resources/Icons/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // 테스트가 알파 채널을 읽어서 "가짜 투명"(체크무늬가 픽셀로 그려진 완전 불투명 PNG)을
            // 잡아내는 데 필요 (2026-09-25, grinder_golem 등 6개 파일에서 실제로 겪은 문제).
            importer.isReadable = true;
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
