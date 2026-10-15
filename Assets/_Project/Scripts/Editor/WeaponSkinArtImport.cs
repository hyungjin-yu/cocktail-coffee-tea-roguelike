using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// 49 무기 외형 PNG(Resources/WeaponSkins/**)를 원본 무기·휘두르기 프레임과 같은 임포트 설정(pivot·PPU·필터·압축 없음)으로 맞추고
    /// 스프라이트 기하가 원본과 같은지 확인한다. 아트 담당 인계 PNG를 Assets로 복사한 뒤 -executeMethod로 실행.
    /// </summary>
    public static class WeaponSkinArtImport
    {
        const string Root = "Assets/_Project/Resources/WeaponSkins/";

        static string HeldOriginal(WeaponKind w) => w == WeaponKind.Bow
            ? "Assets/_Project/Resources/ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00.png"
            : $"Assets/_Project/Art/v1/weapon_{w.ToString().ToLowerInvariant()}/frames/weapon_{w.ToString().ToLowerInvariant()}_effect_right_00.png";

        public static IEnumerable<(string target, string original)> Pairs()
        {
            foreach (var def in WeaponSkinCatalog.All)
            {
                yield return (Root + def.id + "/" + def.id + "_held.png", HeldOriginal(def.weapon));
                if (def.weapon != WeaponKind.Sword) continue;
                for (int i = 0; i < 8; i++)
                {
                    var name = $"tea_sommelier_sword_swing_right_{i:00}.png";
                    yield return (Root + def.id + "/swing_base/" + name, "Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/" + name);
                    yield return (Root + def.id + "/swing_jade/" + name, "Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/" + name);
                }
            }
        }

        public static void ImportBatch()
        {
            AssetDatabase.Refresh();
            int done = 0, missing = 0;
            foreach (var (target, original) in Pairs())
            {
                var variant = AssetImporter.GetAtPath(target) as TextureImporter;
                if (variant == null) { missing++; Debug.LogWarning("[WeaponSkinImport] 없음(기본 외형 유지): " + target); continue; }
                var source = AssetImporter.GetAtPath(original) as TextureImporter;
                if (source == null) throw new InvalidOperationException("원본 임포터 없음: " + original);
                var settings = new TextureImporterSettings();
                source.ReadTextureSettings(settings);
                variant.SetTextureSettings(settings);
                variant.spritePixelsPerUnit = source.spritePixelsPerUnit;
                variant.textureCompression = TextureImporterCompression.Uncompressed;
                variant.filterMode = FilterMode.Point;
                variant.mipmapEnabled = false;
                variant.alphaIsTransparency = true;
                variant.SaveAndReimport();
                var a = AssetDatabase.LoadAssetAtPath<Sprite>(original);
                var b = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                if (a == null || b == null || a.rect != b.rect || a.pivot != b.pivot || Math.Abs(a.pixelsPerUnit - b.pixelsPerUnit) > 0.01f)
                    throw new InvalidOperationException("무기 스킨 기하가 원본과 다름: " + target);
                // 2026-10-13 기하 계약: 알파 실루엣이 같아야 기본 무기의 grip/방향/발사 anchor를 공유할 수 있다.
                var ta = WeaponSkinGeometry.LoadPng(original); var tb = WeaponSkinGeometry.LoadPng(target);
                bool same = WeaponSkinGeometry.SameSilhouette(ta, tb, out var why);
                UnityEngine.Object.DestroyImmediate(ta); UnityEngine.Object.DestroyImmediate(tb);
                if (!same) throw new InvalidOperationException($"무기 스킨 실루엣이 원본과 다름({why}): {target} — 같은 실루엣으로 다시 만들거나 새 무기 manifest를 만든다");
                done++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponSkinImport] imported={done} missing={missing}");
        }
    }
}
