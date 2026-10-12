using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CCT.Editor
{
    /// <summary>
    /// 1차 통합 아트 반입(기획 20). Tools/Art/firstpass-sources.json의 소스에서:
    /// - status "ready" 에셋 → Assets/_Project/Art/v1/&lt;id&gt;/ (04 §9, 출시 BuildRelease 검사 대상)
    /// - 사용자가 명시 허용한 미완성 소스(provisional) → Assets/_Project/Art/firstpass/&lt;id&gt;/ + firstpass-acceptance.json
    ///   (출시 검사 대상 아님 — 1차 프리뷰 빌드만 예외로 보고한 뒤 사용)
    /// manifest.json과 manifest가 참조하는 프레임 PNG만 바이트 그대로 복사한다(원본 수정·재작성 없음, SHA-256 기록).
    /// 이어서 두 루트를 읽어 런타임 목록 Resources/ArtV1/ArtCatalog.asset을 굽고 Delivery/logs/art-integration-firstpass.json을 쓴다.
    /// </summary>
    public static class FirstPassArtIntegrator
    {
        public const string ConfigPath = "Tools/Art/firstpass-sources.json";
        public const string ReadyRoot = "Assets/_Project/Art/v1";
        public const string FirstPassRoot = "Assets/_Project/Art/firstpass";
        public const string AcceptanceFileName = "firstpass-acceptance.json";
        public const string CatalogAssetPath = "Assets/_Project/Resources/ArtV1/ArtCatalog.asset";
        public const string ReportPath = "Delivery/logs/art-integration-firstpass.json";

        public sealed class CopyRecord
        {
            public string AssetId;
            public string Mode; // ready | provisional | skipped
            public string Source;
            public string Destination;
            public string Status;
            public int Files;
            public string Note;
            public readonly List<string> Sha256 = new List<string>();
        }

        [MenuItem("CCT/Art/Integrate First Pass Art")]
        public static void IntegrateMenu() => Integrate();

        /// <summary>-executeMethod CCT.Editor.FirstPassArtIntegrator.IntegrateBatch</summary>
        public static void IntegrateBatch()
        {
            try
            {
                var records = Integrate();
                Debug.Log($"[FirstPassArt] 완료: {records.Count(r => r.Mode == "ready")} ready, " +
                          $"{records.Count(r => r.Mode == "provisional")} provisional, {records.Count(r => r.Mode == "skipped")} skipped");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirstPassArt] 실패: {e}");
                EditorApplication.Exit(1);
            }
        }

        public static List<CopyRecord> Integrate()
        {
            var config = (Dictionary<string, object>)ArtV1Json.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8));
            var rejects = Strings(config, "rejectPathFragments");
            var records = new List<CopyRecord>();

            // 1) ready: 뒤쪽 루트가 우선 — 같은 ID면 덮어쓴다.
            var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var root in Strings(config, "readyRoots"))
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var manifestPath = Path.Combine(dir, "manifest.json");
                    if (!File.Exists(manifestPath)) continue;
                    var m = ArtV1Manifest.Parse(File.ReadAllText(manifestPath, Encoding.UTF8), out _);
                    string id = m?.AssetId ?? Path.GetFileName(dir);
                    if (id == "player_fire" || id == "player_poison")
                    {
                        records.Add(new CopyRecord { AssetId=id, Mode="skipped", Source=dir, Note="35 retired player floor effect; source history preserved" });
                        continue;
                    }
                    if (rejects.Any(r => dir.Replace('\\', '/').Contains(r)))
                    {
                        records.Add(new CopyRecord { AssetId = id, Mode = "skipped", Source = dir, Note = "rejectPathFragments" });
                        continue;
                    }
                    if (m == null || !m.IsReady)
                    {
                        records.Add(new CopyRecord { AssetId = id, Mode = "skipped", Source = dir, Status = m?.Status, Note = "status != ready" });
                        continue;
                    }
                    chosen[id] = dir;
                }
            }

            // Explicit review candidates may temporarily replace an older ready asset in previews.
            // They remain provisional, so the strict release gate still rejects them.
            var reviewOverrides = ReviewOverrideIds(config);
            foreach (var id in reviewOverrides) chosen.Remove(id);

            foreach (var kv in chosen)
                records.Add(CopyAsset(kv.Key, kv.Value, Path.Combine(ReadyRoot, kv.Key), "ready", null));

            // 2) provisional: 같은 ID의 ready 소스가 생기면 그쪽을 쓰고 provisional 사본은 건너뛴다.
            if (config.TryGetValue("provisional", out var provObj) && provObj is List<object> provList)
            {
                foreach (var o in provList.OfType<Dictionary<string, object>>())
                {
                    string id = o.TryGetValue("assetId", out var v) ? v as string : null;
                    string src = o.TryGetValue("source", out var s) ? s as string : null;
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(src)) continue;
                    if (rejects.Any(r => src.Replace('\\', '/').Contains(r)))
                        throw new InvalidOperationException($"provisional 소스가 금지 경로다: {src}");
                    if (chosen.ContainsKey(id))
                    {
                        records.Add(new CopyRecord { AssetId = id, Mode = "skipped", Source = src, Note = "ready 소스가 있어 provisional 미사용" });
                        continue;
                    }
                    if (!Directory.Exists(src))
                    {
                        records.Add(new CopyRecord { AssetId = id, Mode = "skipped", Source = src, Note = "소스 폴더 없음" });
                        continue;
                    }
                    var clips = Strings(o, "clips");
                    string reason = o.TryGetValue("reason", out var rs) ? rs as string : "";
                    var rec = CopyAsset(id, src, Path.Combine(FirstPassRoot, id), "provisional", clips);
                    var acceptance = "{\n  \"assetId\": " + Quote(id) + ",\n  \"source\": " + Quote(src.Replace('\\', '/')) +
                                     ",\n  \"clips\": [" + string.Join(", ", clips.Select(Quote)) + "],\n  \"reason\": " + Quote(reason) +
                                     ",\n  \"integratedUtc\": " + Quote(DateTime.UtcNow.ToString("o")) + "\n}\n";
                    File.WriteAllText(Path.Combine(FirstPassRoot, id, AcceptanceFileName), acceptance, new UTF8Encoding(false));
                    records.Add(rec);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildCatalog();
            WriteReport(records);
            return records;
        }

        static CopyRecord CopyAsset(string id, string sourceDir, string destDir, string mode, List<string> onlyClips)
        {
            var rec = new CopyRecord { AssetId = id, Mode = mode, Source = sourceDir.Replace('\\', '/'), Destination = destDir.Replace('\\', '/') };
            var manifestPath = Path.Combine(sourceDir, "manifest.json");
            var manifestText = File.ReadAllText(manifestPath, Encoding.UTF8);
            var m = ArtV1Manifest.Parse(manifestText, out var err);
            if (m == null) throw new InvalidOperationException($"{id} manifest 파싱 실패: {err}");
            rec.Status = m.Status;

            var files = new List<string>();
            foreach (var c in m.Clips)
                if (onlyClips == null || onlyClips.Count == 0 || onlyClips.Contains(c.Name))
                    files.AddRange(c.Files);
            if (files.Count == 0) files.AddRange(m.Files);
            files = files.Distinct().ToList();

            Directory.CreateDirectory(destDir);
            CopyIdentical(manifestPath, Path.Combine(destDir, "manifest.json"), rec);
            foreach (var rel in files)
            {
                var src = Path.Combine(sourceDir, rel);
                if (!File.Exists(src)) throw new FileNotFoundException($"{id} 프레임 누락: {src}");
                var dst = Path.Combine(destDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                CopyIdentical(src, dst, rec);
            }
            rec.Files = files.Count;
            return rec;
        }

        static void CopyIdentical(string src, string dst, CopyRecord rec)
        {
            var bytes = File.ReadAllBytes(src);
            bool same = File.Exists(dst) && File.ReadAllBytes(dst).SequenceEqual(bytes);
            if (!same) File.WriteAllBytes(dst, bytes);
            using (var sha = SHA256.Create())
                rec.Sha256.Add(Path.GetFileName(dst) + " " + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
        }

        // ── 런타임 목록 ─────────────────────────────────────────────

        public static ArtCatalog BuildCatalog()
        {
            var assets = new List<ArtAsset>();
            var config = (Dictionary<string, object>)ArtV1Json.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8));
            var reviewOverrides = ReviewOverrideIds(config);
            foreach (var root in new[] { ReadyRoot, FirstPassRoot })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
                {
                    if (Path.GetFileName(dir) == "player_fire" || Path.GetFileName(dir) == "player_poison") continue;
                    var a = BuildAsset(dir.Replace('\\', '/'), root == FirstPassRoot);
                    if (a != null && root == ReadyRoot && reviewOverrides.Contains(a.assetId)) continue;
                    if (a != null) assets.Add(a);
                }
            }

            // ready가 있으면 같은 ID의 provisional은 목록에서 뺀다.
            var readyIds = new HashSet<string>(assets.Where(a => !a.firstPassProvisional).Select(a => a.assetId));
            assets = assets.Where(a => !a.firstPassProvisional || !readyIds.Contains(a.assetId)).ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogAssetPath));
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogAssetPath);
            bool create = catalog == null;
            if (create) catalog = ScriptableObject.CreateInstance<ArtCatalog>();
            catalog.assets = assets.ToArray();
            catalog.generatedUtc = DateTime.UtcNow.ToString("o");
            if (create) AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static HashSet<string> ReviewOverrideIds(Dictionary<string, object> config)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (config.TryGetValue("provisional", out var value) && value is List<object> entries)
                foreach (var entry in entries.OfType<Dictionary<string, object>>())
                    if (entry.TryGetValue("overrideReadyForReview", out var flag) && flag is bool enabled && enabled &&
                        entry.TryGetValue("assetId", out var id) && id is string assetId)
                        ids.Add(assetId);
            return ids;
        }

        static ArtAsset BuildAsset(string dir, bool provisional)
        {
            var manifestPath = dir + "/manifest.json";
            if (!File.Exists(manifestPath)) return null;
            var text = File.ReadAllText(manifestPath, Encoding.UTF8);
            var m = ArtV1Manifest.Parse(text, out _);
            if (m == null) return null;
            if (!provisional && !m.IsReady) return null; // Art/v1에는 ready만 — 혹시 남아 있어도 런타임에 쓰지 않는다
            if (provisional && !File.Exists(dir + "/" + AcceptanceFileName)) return null;
            var raw = (Dictionary<string, object>)ArtV1Json.Parse(text);

            var kind = m.ResolveKind();
            var a = new ArtAsset
            {
                assetId = m.AssetId ?? Path.GetFileName(dir),
                kind = kind.ToString().ToLowerInvariant(),
                status = m.Status,
                firstPassProvisional = provisional,
                sourcePath = dir,
                pixelsPerUnit = m.PixelsPerUnit ?? ArtV1Spec.PixelsPerUnit,
                frameSizePx = m.FrameSizePx != null && m.FrameSizePx.Length == 2 ? new Vector2(m.FrameSizePx[0], m.FrameSizePx[1]) : Vector2.zero,
                deviations = m.Deviations.ToArray(),
            };
            a.pivotNormalized = kind == ArtV1Kind.Body
                ? new Vector2(ArtV1Spec.BodyPivotX, ArtV1Spec.BodyPivotY)
                : m.PivotNormalized != null ? new Vector2(m.PivotNormalized[0], m.PivotNormalized[1]) : new Vector2(0.5f, 0.5f);
            if (raw.TryGetValue("runtimeOpacity", out var op) && op is double opd) a.runtimeOpacity = (float)opd;
            if (raw.TryGetValue("targetVisualLengthRelativeToPlayer", out var tl) && tl is double tld) a.targetVisualLengthRelativeToPlayer = (float)tld;
            if (raw.TryGetValue("runtimeTintVariants", out var tv) && tv is Dictionary<string, object> tints)
            {
                a.tints = tints.Select(kv => new ArtTint
                {
                    name = kv.Key,
                    color = ColorUtility.TryParseHtmlString(kv.Value as string ?? "", out var c) ? c : Color.white,
                }).ToArray();
            }

            HashSet<string> allowedClips = null;
            if (provisional)
            {
                var acc = (Dictionary<string, object>)ArtV1Json.Parse(File.ReadAllText(dir + "/" + AcceptanceFileName, Encoding.UTF8));
                allowedClips = new HashSet<string>(Strings(acc, "clips"));
            }

            var clips = new List<ArtClip>();
            var anchors = new List<ArtAnchor>();
            foreach (var c in m.Clips)
            {
                if (allowedClips != null && allowedClips.Count > 0 && !allowedClips.Contains(c.Name)) continue;
                var sprites = new List<Sprite>();
                for (int i = 0; i < c.Files.Count; i++)
                {
                    var path = dir + "/" + c.Files[i];
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) throw new InvalidOperationException($"스프라이트 임포트 실패: {path}");
                    sprites.Add(sprite);
                    // anchorsByFrame 키는 manifest의 files 경로(frames/...) 기준. 파일명만 적은 manifest도 같은 프레임으로 받는다.
                    if (m.AnchorsByFrame.TryGetValue(c.Files[i], out var byName) ||
                        m.AnchorsByFrame.TryGetValue(Path.GetFileName(c.Files[i]), out byName))
                        foreach (var kv in byName)
                            if (kv.Value != null && kv.Value.Length == 2)
                                anchors.Add(new ArtAnchor { clip = c.Name, frame = i, name = kv.Key, px = new Vector2(kv.Value[0], kv.Value[1]) });
                }
                clips.Add(new ArtClip { name = c.Name, fps = c.Fps ?? 0f, loop = c.Loop ?? false, frames = sprites.ToArray() });
            }
            a.clips = clips.ToArray();
            a.anchors = anchors.ToArray();

            var firstFile = m.Clips.SelectMany(c => c.Files).FirstOrDefault() ?? m.Files.FirstOrDefault();
            // 몸체는 idle_00을 기준으로 측정(04 §7 bodyHeightPx).
            var idle = m.Clips.FirstOrDefault(c => c.Name == "idle");
            if (idle != null && idle.Files.Count > 0) firstFile = idle.Files[0];
            if (firstFile != null && File.Exists(dir + "/" + firstFile))
            {
                var png = ArtV1Png.ReadFile(dir + "/" + firstFile);
                if (png.Rgba != null)
                {
                    a.contentRectNormalized = AlphaRect(png);
                    if (kind == ArtV1Kind.Body)
                    {
                        if (ArtV1Spec.TryGetBody(a.assetId, out var spec))
                            a.targetBodyHeightWorld = m.TargetBodyHeightWorld ?? spec.TargetHeightWorld;
                        else
                            a.targetBodyHeightWorld = m.TargetBodyHeightWorld ?? 0f;
                        if (m.BodyHeightPx.HasValue && m.BodyHeightPx.Value > 0f)
                        {
                            a.bodyHeightPx = m.BodyHeightPx.Value;
                        }
                        else
                        {
                            int foot = spec != null ? spec.FootLinePx : Mathf.RoundToInt(a.frameSizePx.y * ArtV1Spec.BodyPivotY);
                            float top = a.contentRectNormalized.yMax * png.Height;
                            a.bodyHeightPx = Mathf.Max(1f, top - foot);
                            a.bodyHeightEstimated = true;
                        }
                    }
                }
            }
            return a;
        }

        /// <summary>알파 &gt; 0 픽셀의 경계(정규화, 좌하단 원점).</summary>
        static Rect AlphaRect(ArtV1Png png)
        {
            int minX = png.Width, minY = png.Height, maxX = -1, maxY = -1; // y는 위에서부터
            for (int y = 0; y < png.Height; y++)
                for (int x = 0; x < png.Width; x++)
                    if (png.AlphaAt(x, y) > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return new Rect(0f, 0f, 1f, 1f);
            float w = png.Width, h = png.Height;
            float yMinBottom = (h - 1 - maxY) / h;
            return new Rect(minX / w, yMinBottom, (maxX - minX + 1) / w, (maxY - minY + 1) / h);
        }

        static void WriteReport(List<CopyRecord> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            var sb = new StringBuilder();
            sb.Append("{\n  \"generatedUtc\": ").Append(Quote(DateTime.UtcNow.ToString("o"))).Append(",\n  \"records\": [\n");
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                sb.Append("    {\"assetId\": ").Append(Quote(r.AssetId))
                  .Append(", \"mode\": ").Append(Quote(r.Mode))
                  .Append(", \"status\": ").Append(Quote(r.Status ?? ""))
                  .Append(", \"files\": ").Append(r.Files.ToString(CultureInfo.InvariantCulture))
                  .Append(", \"source\": ").Append(Quote(r.Source ?? ""))
                  .Append(", \"destination\": ").Append(Quote(r.Destination ?? ""))
                  .Append(", \"note\": ").Append(Quote(r.Note ?? ""))
                  .Append(", \"sha256\": [").Append(string.Join(", ", r.Sha256.Select(Quote))).Append("]}")
                  .Append(i < records.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }

        static List<string> Strings(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var v) && v is List<object> list ? list.OfType<string>().ToList() : new List<string>();

        static string Quote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}
