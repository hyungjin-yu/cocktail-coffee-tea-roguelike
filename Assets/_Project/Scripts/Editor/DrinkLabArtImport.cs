using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>69 음료 제조 아트: Resources/DrinkLab 텍스처는 픽셀 스프라이트(Point, 무압축, 밉맵 없음, PPU 32, 아래 가운데 피벗).</summary>
    public class DrinkLabImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/DrinkLab/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            s.spritePixelsPerUnit = 32f;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.isReadable = assetPath.EndsWith("_mask.png", StringComparison.OrdinalIgnoreCase);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }

    /// <summary>
    /// Codex 납품 ArtSource/drink-lab-2026-10-19/manifest.json에서 status=ready이고 sha256이 맞는 PNG만 Resources/DrinkLab으로 복사한다.
    /// 앵커(픽셀, 왼쪽 위 원점)는 Resources/DrinkLab/anchors.json(0~1, 왼쪽 아래 원점)으로 변환. ArtSource는 읽기만 한다.
    /// 결과 표(assetId, 상태, 사유)를 Delivery/DrinkLab-v1.1/art-import.json에 남긴다.
    /// </summary>
    public static class DrinkLabArtImport
    {
        public const string Source = "ArtSource/v1/BeverageCraft-v1.1";
        const string Dest = "Assets/_Project/Resources/DrinkLab";

        [Serializable] class Manifest { public string specVersion; public List<Entry> assets = new List<Entry>(); public List<Excluded> excludedAssets = new List<Excluded>(); }
        /// <summary>사용자 결정으로 제외된 원본(손·얼굴·마시기 동작 등). 활성 Resources에서 지운다(ArtSource 보관본은 건드리지 않음).</summary>
        [Serializable] class Excluded { public string assetId; public string status; public string reason; }
        [Serializable] class Entry
        {
            public string assetId; public string png; public string sha256; public string status; public int[] size;
            /// <summary>없으면 허용(true). false면 임포트하지 않고 기존 활성 사본도 지운다.</summary>
            public bool integrationAllowed = true;
            public List<FrameEntry> frames = new List<FrameEntry>();
            public MaskEntry mask;
            public List<string> animatedMasks = new List<string>();
            public Anchors anchors;
        }
        /// <summary>Codex 마스크: 흰색·이진 알파, alphaBounds [x0,y0,x1(제외),y1(제외)], emptyRow(빈 수위 행, 포함)·fullRow(가득 행), 왼쪽 위 원점.</summary>
        [Serializable] class MaskEntry { public string png; public string sha256; public int[] alphaBounds; public int emptyRow = -1; public int fullRow = -1; }
        [Serializable] class FrameEntry { public string png; public string sha256; }
        [Serializable] class Anchors { public int[] pourLip; public int[] mouth; public int[] grip; public int[] origin; }
        [Serializable] class Report { public string source; public string time; public List<Row> rows = new List<Row>(); }
        [Serializable] class Row { public string assetId; public string state; public string reason; public int files; }

        [MenuItem("CCT/Drink Lab/Import ready art")]
        public static void ImportReady()
        {
            var report = new Report { source = Source, time = DateTime.Now.ToString("o") };
            var manifestPath = Path.Combine(Source, "manifest.json");
            Directory.CreateDirectory(Dest);
            var anchorTable = new DrinkAnchorTable();
            if (!File.Exists(manifestPath))
            {
                report.rows.Add(new Row { assetId = "*", state = "missing", reason = "manifest.json 없음" });
                WriteReport(report);
                return;
            }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            var excluded = new HashSet<string>();
            foreach (var x in manifest.excludedAssets ?? new List<Excluded>())
                if (x != null && !string.IsNullOrEmpty(x.assetId)) excluded.Add(x.assetId);
            foreach (var e in manifest.assets ?? new List<Entry>())
                if (e != null && !e.integrationAllowed && !string.IsNullOrEmpty(e.assetId)) excluded.Add(e.assetId);
            foreach (var id in excluded)
            {
                int removed = RemoveActive(id);
                report.rows.Add(new Row { assetId = id, state = "excluded", reason = "사용자 결정으로 제외(integrationAllowed=false) — 활성 사본 삭제", files = removed });
            }
            foreach (var e in manifest.assets ?? new List<Entry>())
            {
                if (e == null || string.IsNullOrEmpty(e.assetId) || !e.assetId.StartsWith("dl_") || excluded.Contains(e.assetId)) continue;
                if (!string.Equals(e.status, "ready", StringComparison.OrdinalIgnoreCase))
                {
                    report.rows.Add(new Row { assetId = e.assetId, state = e.status ?? "planned", reason = "ready 아님" });
                    continue;
                }
                int copied = 0;
                string fail = null;
                if (e.frames != null && e.frames.Count > 1)
                {
                    for (int i = 0; i < e.frames.Count && fail == null; i++)
                        fail = Copy(e.frames[i].png, e.frames[i].sha256, $"{e.assetId}_{i:00}.png", ref copied);
                }
                else if (e.frames != null && e.frames.Count == 1) fail = Copy(e.frames[0].png, e.frames[0].sha256, e.assetId + ".png", ref copied);
                else fail = Copy(e.png, e.sha256, e.assetId + ".png", ref copied);
                if (fail == null && e.mask != null && !string.IsNullOrEmpty(e.mask.png)) fail = Copy(e.mask.png, e.mask.sha256, e.assetId + "_mask.png", ref copied);
                if (e.animatedMasks != null)
                    for (int i = 0; i < e.animatedMasks.Count && fail == null; i++)
                        fail = Copy(e.animatedMasks[i], null, $"{e.assetId}_mask_{i:00}.png", ref copied);
                if (fail == null && e.size != null && e.size.Length == 2)
                    anchorTable.entries.Add(Convert(e.assetId, e.anchors, e.mask, e.size[0], e.size[1]));
                report.rows.Add(new Row { assetId = e.assetId, state = fail == null ? "integrated" : "rejected", reason = fail ?? "", files = copied });
            }
            File.WriteAllText(Path.Combine(Dest, "anchors.json"), JsonUtility.ToJson(anchorTable, true));
            AssetDatabase.Refresh();
            WriteReport(report);
        }

        public static void Batch()
        {
            try { ImportReady(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static DrinkAnchorEntry Convert(string id, Anchors a, MaskEntry m, int w, int h)
        {
            float[] P(int[] v) => v != null && v.Length == 2 ? new[] { (v[0] + 0.5f) / w, 1f - (v[1] + 0.5f) / h } : null;
            float[] liquid = null;
            if (m != null && m.alphaBounds != null && m.alphaBounds.Length == 4)
            {
                int top = m.fullRow >= 0 ? m.fullRow : m.alphaBounds[1];
                int bottom = m.emptyRow >= 0 ? m.emptyRow + 1 : m.alphaBounds[3];
                liquid = new[] { m.alphaBounds[0] / (float)w, 1f - bottom / (float)h, m.alphaBounds[2] / (float)w, 1f - top / (float)h };
            }
            return new DrinkAnchorEntry
            {
                assetId = id, pourLip = P(a?.pourLip ?? a?.origin), mouth = P(a?.mouth), grip = P(a?.grip), liquid = liquid,
                width = w, height = h,
            };
        }

        static string Copy(string src, string sha, string destName, ref int copied)
        {
            if (string.IsNullOrEmpty(src)) return "png 경로 없음";
            if (!Path.IsPathRooted(src)) src = Path.Combine(Source, src);
            if (!File.Exists(src)) return "파일 없음: " + src;
            var bytes = File.ReadAllBytes(src);
            if (!string.IsNullOrEmpty(sha) && !string.Equals(Hash(bytes), sha, StringComparison.OrdinalIgnoreCase)) return "sha256 불일치: " + Path.GetFileName(src);
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != (byte)'P') return "PNG 아님";
            var dest = Path.Combine(Dest, destName);
            if (!File.Exists(dest) || Hash(File.ReadAllBytes(dest)) != Hash(bytes)) File.WriteAllBytes(dest, bytes);
            copied++;
            return null;
        }

        /// <summary>Resources/DrinkLab에서 그 ID의 활성 사본(단일·프레임·마스크와 .meta)을 지운다. 다른 ID 접두어와 섞이지 않게 정확히 맞춘다.</summary>
        static int RemoveActive(string id)
        {
            if (!Directory.Exists(Dest)) return 0;
            int n = 0;
            foreach (var f in Directory.GetFiles(Dest))
            {
                var name = Path.GetFileName(f);
                var stem = name.EndsWith(".meta") ? name.Substring(0, name.Length - 5) : name;
                if (!stem.EndsWith(".png")) continue;
                stem = stem.Substring(0, stem.Length - 4);
                bool match = stem == id || stem == id + "_mask" ||
                             System.Text.RegularExpressions.Regex.IsMatch(stem, "^" + System.Text.RegularExpressions.Regex.Escape(id) + @"(_mask)?_\d\d$");
                if (!match) continue;
                File.Delete(f);
                n++;
            }
            return n;
        }

        static string Hash(byte[] b)
        {
            using var s = SHA256.Create();
            return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLowerInvariant();
        }

        static void WriteReport(Report r)
        {
            Directory.CreateDirectory("Delivery/DrinkLab-v1.1");
            File.WriteAllText("Delivery/DrinkLab-v1.1/art-import.json", JsonUtility.ToJson(r, true));
            Debug.Log($"[DrinkLabArtImport] {r.rows.Count} rows → Delivery/DrinkLab-v1.1/art-import.json");
        }
    }
}
