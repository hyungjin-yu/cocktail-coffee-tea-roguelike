using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// 79 신규 코인 스킨 10종 아트 import(루트 승인된 ready 매니페스트에서 만든 스테이징 목록만). 아트 원본(ArtSource)은 읽기만 한다.
    /// 스테이징 JSON: {"entries":[{"skin":"skin_tea_apiary","name":"tea_sommelier_idle_right_00","source":"C:/.../x.png","sha256":"..."}]}
    /// - 대상: Assets/_Project/Resources/CosmeticSkins/&lt;skin&gt;/&lt;name&gt;.png (런타임은 같은 이름의 원본 프레임을 교체).
    /// - 원본 해시 일치, 각 스킨이 실사용 필수 프레임(SkinArt.RequiredFrameNames)을 정확히 덮는지, 원본 몸체 프레임의 import 설정
    ///   (PPU·피벗·필터)을 복사한 뒤 rect/pivot이 원본과 같은지 확인. 하나라도 어긋나면 그 스킨 폴더를 지우고 실패(부분 import 금지).
    /// 실행: -executeMethod CCT.Editor.CoinSkinArtImport.ImportBatch -cct-skin79-staging &lt;json&gt;
    /// </summary>
    public static class CoinSkinArtImport
    {
        [Serializable] class Entry { public string skin; public string name; public string source; public string sha256; }
        [Serializable] class Staging { public Entry[] entries; }

        const string TargetRoot = "Assets/_Project/Resources/CosmeticSkins/";

        public static void ImportBatch()
        {
            string staging = Arg("-cct-skin79-staging");
            if (string.IsNullOrEmpty(staging) || !File.Exists(staging)) throw new InvalidOperationException("staging json missing: " + staging);
            var list = JsonUtility.FromJson<Staging>(File.ReadAllText(staging));
            var bySkin = new Dictionary<string, List<Entry>>();
            foreach (var e in list.entries)
            {
                var def = SkinCatalog.Find(e.skin);
                if (def == null || def.paid) throw new InvalidOperationException("not a 79 coin skin: " + e.skin);
                if (!bySkin.TryGetValue(e.skin, out var l)) bySkin[e.skin] = l = new List<Entry>();
                l.Add(e);
            }
            foreach (var kv in bySkin) ImportSkin(SkinCatalog.Find(kv.Key), kv.Value);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CoinSkinArtImport] imported {bySkin.Count} skins");
        }

        static void ImportSkin(SkinDefinition def, List<Entry> entries)
        {
            var required = SkinArt.RequiredFrameNames(def.job);
            var names = new HashSet<string>();
            foreach (var e in entries) if (!names.Add(e.name)) throw new InvalidOperationException($"{def.productId}: duplicate frame {e.name}");
            foreach (var n in required) if (!names.Contains(n)) throw new InvalidOperationException($"{def.productId}: missing required frame {n}");
            foreach (var n in names) if (!required.Contains(n)) throw new InvalidOperationException($"{def.productId}: unexpected frame {n}");

            var baseByName = BaseFramePaths(def.job);
            string folder = TargetRoot + def.productId;
            if (Directory.Exists(folder)) throw new InvalidOperationException($"{def.productId}: target folder already exists (refuse to overwrite): {folder}");
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var e in entries)
                {
                    if (!string.Equals(Sha256(e.source), e.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"{def.productId}/{e.name}: source hash mismatch");
                    File.Copy(e.source, $"{folder}/{e.name}.png");
                }
                AssetDatabase.Refresh();
                foreach (var e in entries)
                {
                    string target = $"{folder}/{e.name}.png";
                    string original = baseByName[e.name];
                    var oi = (TextureImporter)AssetImporter.GetAtPath(original);
                    var vi = (TextureImporter)AssetImporter.GetAtPath(target);
                    if (oi == null || vi == null) throw new InvalidOperationException("importer missing: " + target);
                    var settings = new TextureImporterSettings();
                    oi.ReadTextureSettings(settings);
                    vi.SetTextureSettings(settings);
                    vi.spritePixelsPerUnit = oi.spritePixelsPerUnit;
                    vi.textureCompression = TextureImporterCompression.Uncompressed;
                    vi.filterMode = FilterMode.Point;
                    vi.mipmapEnabled = false;
                    vi.alphaIsTransparency = true;
                    vi.SaveAndReimport();
                    var os = AssetDatabase.LoadAssetAtPath<Sprite>(original);
                    var ns = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                    if (os == null || ns == null || os.rect.size != ns.rect.size || (os.pivot - ns.pivot).sqrMagnitude > 0.01f || !Mathf.Approximately(os.pixelsPerUnit, ns.pixelsPerUnit))
                        throw new InvalidOperationException($"{def.productId}/{e.name}: geometry differs from original frame");
                }
            }
            catch
            {
                AssetDatabase.DeleteAsset(folder); // 부분 import 금지 — 이 스킨은 기본 아트로 남는다
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                AssetDatabase.Refresh();
                throw;
            }
        }

        /// <summary>필수 프레임 이름 → 원본 몸체 PNG 에셋 경로.</summary>
        static Dictionary<string, string> BaseFramePaths(Job job)
        {
            var map = new Dictionary<string, string>();
            var art = ArtLibrary.Get(JobPreset.For(job).bodyArtId);
            foreach (var clip in art.clips)
                foreach (var f in clip.frames)
                    if (f != null) map[f.name] = AssetDatabase.GetAssetPath(f);
            if (job == Job.TeaSommelier)
                foreach (var f in PlayerVisual.SwordSwingFrames()) map[f.name] = AssetDatabase.GetAssetPath(f);
            return map;
        }

        static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
