using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CCT.Editor;

namespace CCT.Tests.ArtV1
{
    /// <summary>
    /// 테스트용 ArtSource/v1/&lt;assetId&gt; 폴더를 임시 디렉터리에 만든다.
    /// 기본값은 04 계약을 모두 지키는 ready 에셋이고, 각 테스트가 한 가지씩만 깨뜨린다.
    /// </summary>
    public static class ArtV1TestAssets
    {
        public const int NormalBodyHeightPx = 120;
        public const int BossBodyHeightPx = 300;

        public static Dictionary<string, object> BodyManifest(string assetId)
        {
            ArtV1Spec.TryGetBody(assetId, out var spec);
            var clips = new Dictionary<string, object>();
            var anchors = new Dictionary<string, object>();
            foreach (var c in spec.Clips)
            {
                var files = new List<object>();
                for (int k = 0; k < c.Frames; k++)
                {
                    string file = $"frames/{assetId}_{c.Name}_right_{k:00}.png";
                    files.Add(file);
                    if (spec.Set == "A")
                        anchors[Path.GetFileName(file)] = new Dictionary<string, object> { { "hand", new List<object> { 150.0, 90.0 } } };
                }
                clips[c.Name] = new Dictionary<string, object>
                {
                    { "fps", (double)(c.Fps > 0 ? c.Fps : 8f) },
                    { "loop", c.Loop ?? false },
                    { "files", files },
                };
            }

            return new Dictionary<string, object>
            {
                { "specVersion", "1.0" },
                { "assetId", assetId },
                { "status", "ready" },
                { "sourceReferences", new List<object> { "Assets/_Project/Resources/Icons/Enemies/teabag_imp.png" } },
                { "frameSizePx", new List<object> { (double)spec.FrameSizePx, (double)spec.FrameSizePx } },
                { "pixelsPerUnit", 128.0 },
                { "pivotNormalized", new List<object> { 0.5, 0.125 } },
                { "targetBodyHeightWorld", (double)spec.TargetHeightWorld },
                { "bodyHeightPx", (double)(spec.IsBoss ? BossBodyHeightPx : NormalBodyHeightPx) },
                { "clips", clips },
                { "anchorsByFrame", anchors },
                { "deviations", new List<object>() },
                { "checks", new Dictionary<string, object> { { "alpha", "pass" } } },
            };
        }

        /// <summary>manifest와 모든 프레임 PNG(정상 알파 몸체)를 쓴다.</summary>
        public static string WriteBody(string root, string assetId, Dictionary<string, object> manifest = null,
            Func<string, ArtV1TestImage> frameOverride = null)
        {
            ArtV1Spec.TryGetBody(assetId, out var spec);
            manifest = manifest ?? BodyManifest(assetId);
            string dir = Path.Combine(root, assetId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), ToJson(manifest));

            int height = spec.IsBoss ? BossBodyHeightPx : NormalBodyHeightPx;
            foreach (var file in AllFrameFiles(manifest))
            {
                var img = frameOverride?.Invoke(file) ?? ArtV1TestImage.Body(spec.FrameSizePx, spec.FootLinePx, height);
                ArtV1TestPng.Write(Path.Combine(dir, file), ArtV1TestPng.EncodeRgba(img));
            }
            return dir;
        }

        public static Dictionary<string, object> FlatManifest(string assetId, List<object> pivot)
        {
            ArtV1Spec.TryGetFlat(assetId, out var spec);
            var files = new List<object>();
            for (int k = 0; k < spec.Frames; k++) files.Add($"frames/{assetId}_play_right_{k:00}.png");
            var m = new Dictionary<string, object>
            {
                { "specVersion", "1.0" },
                { "assetId", assetId },
                { "status", "ready" },
                { "kind", spec.Kind.ToString().ToLowerInvariant() },
                { "frameSizePx", new List<object> { (double)spec.Width, (double)spec.Height } },
                { "pixelsPerUnit", 128.0 },
                { "clips", new Dictionary<string, object> { { "play", new Dictionary<string, object> { { "fps", 16.0 }, { "loop", false }, { "files", files } } } } },
                { "checks", new List<object> { "alpha" } },
            };
            if (pivot != null) m["pivotNormalized"] = pivot;
            return m;
        }

        public static string WriteFlat(string root, string assetId, Dictionary<string, object> manifest)
        {
            ArtV1Spec.TryGetFlat(assetId, out var spec);
            string dir = Path.Combine(root, assetId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), ToJson(manifest));
            foreach (var file in AllFrameFiles(manifest))
            {
                var img = new ArtV1TestImage(spec.Width, spec.Height).Fill(spec.Width / 4, spec.Height / 4, spec.Width / 2, spec.Height / 2, 250, 220, 120, 255);
                ArtV1TestPng.Write(Path.Combine(dir, file), ArtV1TestPng.EncodeRgba(img));
            }
            return dir;
        }

        public static IEnumerable<string> AllFrameFiles(Dictionary<string, object> manifest)
        {
            var clips = (Dictionary<string, object>)manifest["clips"];
            return clips.Values.Cast<Dictionary<string, object>>()
                .SelectMany(c => ((List<object>)c["files"]).Cast<string>())
                .Distinct();
        }

        public static Dictionary<string, object> Clip(Dictionary<string, object> manifest, string name) =>
            (Dictionary<string, object>)((Dictionary<string, object>)manifest["clips"])[name];

        public static List<object> ClipFiles(Dictionary<string, object> manifest, string name) =>
            (List<object>)Clip(manifest, name)["files"];

        public static string ToJson(object value)
        {
            var sb = new StringBuilder();
            WriteJson(sb, value);
            return sb.ToString();
        }

        static void WriteJson(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> o:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in o)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteJson(sb, kv.Key);
                        sb.Append(':');
                        WriteJson(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case List<object> list:
                    sb.Append('[');
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        WriteJson(sb, list[k]);
                    }
                    sb.Append(']');
                    break;
                default: throw new ArgumentException($"JSON으로 쓸 수 없는 형식 {v.GetType()}");
            }
        }
    }
}
