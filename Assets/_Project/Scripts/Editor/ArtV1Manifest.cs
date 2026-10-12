using System;
using System.Collections.Generic;

namespace CCT.Editor
{
    public sealed class ArtV1ManifestClip
    {
        public string Name;
        public float? Fps;
        public bool? Loop;
        public readonly List<string> Files = new List<string>();
    }

    /// <summary>
    /// 04 §7의 ArtSource/v1/&lt;assetId&gt;/manifest.json을 읽은 결과. 형식이 틀린 필드는 null로 두고
    /// ParseErrors에 사유를 쌓는다 — 판정(에러/경고)은 ArtV1Validator가 한다.
    /// 04 필수 필드 외에 선택 필드: kind(body|vfx|ui|background), scope, bodyFrameTotal(09),
    /// files(11 인계 필드), checks(검수 결과), spriteBorderPx([좌,하,우,상], UI 9-slice).
    /// </summary>
    public sealed class ArtV1Manifest
    {
        public string SpecVersion;
        public string AssetId;
        public string Status;
        public string KindText;
        public string Scope;
        public int[] FrameSizePx;
        public float? PixelsPerUnit;
        public float[] PivotNormalized;
        public float? TargetBodyHeightWorld;
        public float? BodyHeightPx;
        public int? BodyFrameTotal;
        public int[] SpriteBorderPx;
        public readonly List<ArtV1ManifestClip> Clips = new List<ArtV1ManifestClip>();
        public readonly List<string> Files = new List<string>();
        public readonly List<string> SourceReferences = new List<string>();
        public readonly List<string> Deviations = new List<string>();
        /// <summary>프레임 파일명(또는 경로) → anchor 이름 → [x, y] 좌하단 원점 픽셀.</summary>
        public readonly Dictionary<string, Dictionary<string, float[]>> AnchorsByFrame =
            new Dictionary<string, Dictionary<string, float[]>>(StringComparer.Ordinal);
        public bool HasAnchorsByFrame;
        public bool HasClips;
        public bool HasChecks;
        public readonly List<string> ParseErrors = new List<string>();

        public bool IsReady => string.Equals(Status, "ready", StringComparison.Ordinal);

        /// <summary>kind 필드가 있으면 그것을, 없으면 asset ID로 04/09 표에서 추정한 종류.</summary>
        public ArtV1Kind ResolveKind()
        {
            var k = ArtV1Spec.ParseKind(KindText);
            return k != ArtV1Kind.Unknown ? k : ArtV1Spec.InferKind(AssetId);
        }

        public int TotalClipFrames
        {
            get
            {
                int n = 0;
                foreach (var c in Clips) n += c.Files.Count;
                return n;
            }
        }

        /// <summary>JSON 문법 오류면 null과 함께 error를 돌려준다. 문법은 맞고 필드 형식만 틀리면 ParseErrors에 기록.</summary>
        public static ArtV1Manifest Parse(string json, out string error)
        {
            if (!ArtV1Json.TryParse(json, out object root, out error)) return null;
            if (!(root is Dictionary<string, object> obj))
            {
                error = "manifest 최상위가 JSON 객체가 아니다";
                return null;
            }

            var m = new ArtV1Manifest();
            m.SpecVersion = ReadString(obj, "specVersion", m);
            m.AssetId = ReadString(obj, "assetId", m);
            m.Status = ReadString(obj, "status", m);
            m.KindText = ReadString(obj, "kind", m);
            m.Scope = ReadString(obj, "scope", m);
            m.FrameSizePx = ReadIntArray(obj, "frameSizePx", 2, m);
            m.PixelsPerUnit = ReadFloat(obj, "pixelsPerUnit", m);
            m.PivotNormalized = ReadFloatArray(obj, "pivotNormalized", 2, m);
            m.TargetBodyHeightWorld = ReadFloat(obj, "targetBodyHeightWorld", m);
            m.BodyHeightPx = ReadFloat(obj, "bodyHeightPx", m);
            var total = ReadFloat(obj, "bodyFrameTotal", m);
            if (total.HasValue) m.BodyFrameTotal = (int)total.Value;
            m.SpriteBorderPx = ReadIntArray(obj, "spriteBorderPx", 4, m);
            ReadStringList(obj, "files", m.Files, m);
            ReadStringList(obj, "sourceReferences", m.SourceReferences, m);
            ReadStringList(obj, "deviations", m.Deviations, m);
            m.HasChecks = obj.ContainsKey("checks") && obj["checks"] != null;

            if (obj.TryGetValue("clips", out object clipsValue) && clipsValue != null)
            {
                m.HasClips = true;
                if (clipsValue is Dictionary<string, object> clips)
                {
                    foreach (var kv in clips) m.Clips.Add(ReadClip(kv.Key, kv.Value, m));
                }
                else if (clipsValue is List<object> clipList)
                {
                    // [{ "name": "idle", ... }] 형태도 허용.
                    foreach (var item in clipList)
                    {
                        var co = item as Dictionary<string, object>;
                        string name = co != null && co.TryGetValue("name", out object n) ? n as string : null;
                        if (string.IsNullOrEmpty(name)) { m.ParseErrors.Add("clips 배열 항목에 name이 없다"); continue; }
                        m.Clips.Add(ReadClip(name, item, m));
                    }
                }
                else m.ParseErrors.Add("clips는 객체(이름→클립) 또는 배열이어야 한다");
            }

            if (obj.TryGetValue("anchorsByFrame", out object anchorsValue) && anchorsValue != null)
            {
                m.HasAnchorsByFrame = true;
                if (anchorsValue is Dictionary<string, object> frames)
                {
                    foreach (var frame in frames)
                    {
                        var anchors = new Dictionary<string, float[]>(StringComparer.Ordinal);
                        if (frame.Value is Dictionary<string, object> anchorObj)
                        {
                            foreach (var a in anchorObj)
                            {
                                var xy = ToFloatArray(a.Value, 2);
                                if (xy == null) m.ParseErrors.Add($"anchorsByFrame.{frame.Key}.{a.Key}는 [x, y] 숫자 배열이어야 한다");
                                else anchors[a.Key] = xy;
                            }
                        }
                        else m.ParseErrors.Add($"anchorsByFrame.{frame.Key}는 객체여야 한다");
                        m.AnchorsByFrame[frame.Key] = anchors;
                    }
                }
                else m.ParseErrors.Add("anchorsByFrame은 객체여야 한다");
            }

            return m;
        }

        static ArtV1ManifestClip ReadClip(string name, object value, ArtV1Manifest m)
        {
            var clip = new ArtV1ManifestClip { Name = name };
            if (!(value is Dictionary<string, object> o))
            {
                m.ParseErrors.Add($"clips.{name}는 객체여야 한다");
                return clip;
            }
            if (o.TryGetValue("fps", out object fps))
            {
                if (fps is double f) clip.Fps = (float)f;
                else m.ParseErrors.Add($"clips.{name}.fps는 숫자여야 한다");
            }
            if (o.TryGetValue("loop", out object loop))
            {
                if (loop is bool b) clip.Loop = b;
                else m.ParseErrors.Add($"clips.{name}.loop는 true/false여야 한다");
            }
            if (o.TryGetValue("files", out object files) && files is List<object> list)
            {
                foreach (var f in list)
                {
                    if (f is string s) clip.Files.Add(s);
                    else m.ParseErrors.Add($"clips.{name}.files 항목은 문자열이어야 한다");
                }
            }
            else m.ParseErrors.Add($"clips.{name}.files 배열이 없다");
            return clip;
        }

        static string ReadString(Dictionary<string, object> o, string key, ArtV1Manifest m)
        {
            if (!o.TryGetValue(key, out object v) || v == null) return null;
            if (v is string s) return s;
            m.ParseErrors.Add($"{key}는 문자열이어야 한다");
            return null;
        }

        static float? ReadFloat(Dictionary<string, object> o, string key, ArtV1Manifest m)
        {
            if (!o.TryGetValue(key, out object v) || v == null) return null;
            if (v is double d) return (float)d;
            m.ParseErrors.Add($"{key}는 숫자여야 한다");
            return null;
        }

        static float[] ReadFloatArray(Dictionary<string, object> o, string key, int length, ArtV1Manifest m)
        {
            if (!o.TryGetValue(key, out object v) || v == null) return null;
            var arr = ToFloatArray(v, length);
            if (arr == null) m.ParseErrors.Add($"{key}는 숫자 {length}개 배열이어야 한다");
            return arr;
        }

        static int[] ReadIntArray(Dictionary<string, object> o, string key, int length, ArtV1Manifest m)
        {
            var f = ReadFloatArray(o, key, length, m);
            if (f == null) return null;
            var result = new int[length];
            for (int k = 0; k < length; k++)
            {
                if (Math.Abs(f[k] - Math.Round(f[k])) > 1e-4) { m.ParseErrors.Add($"{key}는 정수 배열이어야 한다"); return null; }
                result[k] = (int)Math.Round(f[k]);
            }
            return result;
        }

        static float[] ToFloatArray(object v, int length)
        {
            if (!(v is List<object> list) || list.Count != length) return null;
            var result = new float[length];
            for (int k = 0; k < length; k++)
            {
                if (!(list[k] is double d)) return null;
                result[k] = (float)d;
            }
            return result;
        }

        static void ReadStringList(Dictionary<string, object> o, string key, List<string> target, ArtV1Manifest m)
        {
            if (!o.TryGetValue(key, out object v) || v == null) return;
            if (!(v is List<object> list)) { m.ParseErrors.Add($"{key}는 배열이어야 한다"); return; }
            foreach (var item in list)
            {
                if (item is string s) target.Add(s);
                // deviations/checks 등은 객체일 수도 있어서 문자열이 아니면 요약 문자열로 보관한다.
                else if (item is Dictionary<string, object> obj) target.Add(obj.TryGetValue("id", out object id) && id is string ids ? ids : "{객체}");
                else if (item != null) target.Add(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
