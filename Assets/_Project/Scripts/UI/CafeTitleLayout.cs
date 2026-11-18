using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-18 카페 입장형 첫 화면의 배치 계약(layout.json). 좌표는 기준 1920x1080 장면의 정규화 좌표 — 좌상단 (0,0), 우하단 (1,1).
    /// rect = [x, y, w, h](x,y = 좌상단). 아트(ArtSource/cafe-title-2026-11-18, Codex 소유)가 이 파일과 PNG를 제공하고,
    /// 구현은 Resources/CafeTitle/로 복사된 것을 읽기만 한다. 최종 파일이 없으면 기존 승인 스프라이트로 만든 임시 배치(status=temp)를 쓴다.
    /// </summary>
    public sealed class CafeTitleLayout
    {
        public const string ResourceFolder = "CafeTitle";
        public const string FinalName = "layout";
        public const string TempName = "layout_temp";

        public sealed class Layer
        {
            public string Id, Png;
            public Color? Color;
            public Rect Rect;     // 정규화(좌상단 원점)
            public int Z;
            public bool Tile;
            public float TileScale = 1f;
        }

        public sealed class Feature
        {
            public string Id, Label, Desc, LabelSide = "below";
            public Rect HotspotRect;          // 다각형이면 그 외접 사각형
            public Vector2[] Polygon;          // null이면 사각형
            public Vector2 LabelAnchor;
        }

        /// <summary>입장 bridge: 같은 평면·가구·광원으로 그린 서로 다른 카메라 뷰(전체 장면 PNG). t = bridge 구간 안 정규화 시작 시각(0~1).</summary>
        public sealed class BridgeView
        {
            public string Png;
            public float T;
            public Vector2 Center = new Vector2(0.5f, 0.5f);
            public float Zoom = 1f;
        }

        public sealed class CameraKey
        {
            public float T, Zoom = 1f;
            public Vector2 Center = new Vector2(0.5f, 0.5f);
        }

        public sealed class Door
        {
            public string[] Frames = new string[0];
            public Rect Rect;
            public Vector2 Hinge;
            public string Swing = "inward-left";
            public float Fps = 12f;
            public Layer Glimpse;
            public Vector2 ThresholdA, ThresholdB;
            public Color? Color; // 프레임 그림이 없을 때(임시)의 문짝 색
        }

        public string Status = "temp";
        public string FitMode = "cover"; // cover(높이 맞춤·좌우 자름) | contain(전체 보임·여백)
        public Vector2Int ArtPixels = new Vector2Int(480, 270);
        public readonly List<Layer> ExteriorLayers = new List<Layer>();
        public readonly List<Layer> InteriorLayers = new List<Layer>();
        public readonly List<Feature> Features = new List<Feature>();
        public readonly List<CameraKey> ExteriorCamera = new List<CameraKey>();
        public readonly List<string> NavOrder = new List<string>();
        public Door EntranceDoor = new Door();
        public Rect SignRect;
        public Vector2 SignTitleAnchor = new Vector2(0.5f, 0.12f);
        public Vector2 ArrivalCenter = new Vector2(0.5f, 0.5f);
        public float ArrivalZoom = 1.2f;
        public string EntryDoorFeature = "quit";
        public readonly List<BridgeView> Bridge = new List<BridgeView>();
        /// <summary>아트가 정한 입장 시간(timing). 없으면(임시 배치) 구현 기본 구간.</summary>
        public bool HasTiming;
        public float TimingHold = 0.45f, TimingDoor = 0.8f, TimingApproach = 0.75f;
        public float BridgeSeconds = 1.2f;
        public string BridgeBlend = "crossfade"; // cut | crossfade
        public string SourceName;

        public bool IsFinalArt => Status == "ready";

        public Feature FindFeature(string id)
        {
            foreach (var f in Features) if (f.Id == id) return f;
            return null;
        }

        /// <summary>최종(layout) → 없으면 임시(layout_temp) 순서로 읽는다.</summary>
        public static CafeTitleLayout Load()
        {
            var text = Resources.Load<TextAsset>(ResourceFolder + "/" + FinalName);
            string name = FinalName;
            if (text == null) { text = Resources.Load<TextAsset>(ResourceFolder + "/" + TempName); name = TempName; }
            if (text == null) return null;
            var layout = Parse(text.text);
            if (layout != null) layout.SourceName = name;
            return layout;
        }

        /// <summary>png 경로: "res:<Resources 경로>"면 기존 리소스, 아니면 Resources/CafeTitle/ 아래(확장자 제외).</summary>
        public static Sprite LoadSprite(string png)
        {
            if (string.IsNullOrEmpty(png)) return null;
            string path = png.StartsWith("res:") ? png.Substring(4) : ResourceFolder + "/" + png;
            if (path.EndsWith(".png")) path = path.Substring(0, path.Length - 4);
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;
            var all = Resources.LoadAll<Sprite>(path);
            return all != null && all.Length > 0 ? all[0] : null;
        }

        // ───────── 파싱 ─────────

        public static CafeTitleLayout Parse(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return null;
            var l = new CafeTitleLayout();
            l.Status = Str(root, "status", "temp");
            l.FitMode = Str(root, "fit", "cover");
            if (root.TryGetValue("aspectPolicy", out var apO) && apO is Dictionary<string, object> apd) l.FitMode = Str(apd, "mode", l.FitMode); // Codex 초안 키
            if (root.TryGetValue("artPixels", out var ap) && ap is List<object> apl && apl.Count == 2)
                l.ArtPixels = new Vector2Int((int)Num(apl[0]), (int)Num(apl[1]));

            if (root.TryGetValue("exterior", out var exO) && exO is Dictionary<string, object> ex)
            {
                ReadLayers(ex, l.ExteriorLayers);
                if (ex.TryGetValue("sign", out var sO) && sO is Dictionary<string, object> sign)
                {
                    l.SignRect = RectOf(sign, "rect");
                    if (sign.ContainsKey("titleAnchor")) l.SignTitleAnchor = Vec(sign["titleAnchor"]);
                }
                if (ex.TryGetValue("door", out var dO) && dO is Dictionary<string, object> door)
                {
                    var d = l.EntranceDoor;
                    if (door.TryGetValue("frames", out var fr) && fr is List<object> frl)
                    {
                        d.Frames = new string[frl.Count];
                        for (int i = 0; i < frl.Count; i++) d.Frames[i] = frl[i] as string;
                    }
                    d.Rect = RectOf(door, "rect");
                    if (door.ContainsKey("hinge")) d.Hinge = Vec(door["hinge"]);
                    d.Swing = Str(door, "swing", d.Swing);
                    d.Fps = door.ContainsKey("fps") ? Num(door["fps"]) : d.Fps;
                    if (door.ContainsKey("color")) d.Color = ColorOf(door["color"] as string);
                    if (door.TryGetValue("glimpse", out var gO) && gO is Dictionary<string, object> g) d.Glimpse = LayerOf(g);
                    if (door.TryGetValue("threshold", out var tO) && tO is List<object> tl && tl.Count == 2)
                    { d.ThresholdA = Vec(tl[0]); d.ThresholdB = Vec(tl[1]); }
                }
                if (ex.TryGetValue("camera", out var cO) && cO is List<object> cl)
                    foreach (var k in cl)
                        if (k is Dictionary<string, object> kd)
                            l.ExteriorCamera.Add(new CameraKey { T = Num(kd["t"]), Center = Vec(kd["center"]), Zoom = kd.ContainsKey("zoom") ? Num(kd["zoom"]) : 1f });
            }

            if (root.TryGetValue("timing", out var tmO) && tmO is Dictionary<string, object> tm)
            {
                l.HasTiming = true;
                if (tm.ContainsKey("exteriorHoldSeconds")) l.TimingHold = Num(tm["exteriorHoldSeconds"]);
                if (tm.ContainsKey("doorOpenSeconds")) l.TimingDoor = Num(tm["doorOpenSeconds"]);
                l.TimingApproach = tm.ContainsKey("approachSeconds") ? Num(tm["approachSeconds"]) : 0f;
                if (tm.ContainsKey("bridgeSeconds")) l.BridgeSeconds = Num(tm["bridgeSeconds"]);
            }

            if (root.TryGetValue("bridge", out var bO) && bO is Dictionary<string, object> br)
            {
                if (br.ContainsKey("seconds")) l.BridgeSeconds = Num(br["seconds"]);
                l.BridgeBlend = Str(br, "blend", l.BridgeBlend);
                if (br.TryGetValue("views", out var vO) && vO is List<object> vl)
                    foreach (var o in vl)
                        if (o is Dictionary<string, object> vd)
                        {
                            var bv = new BridgeView { Png = Str(vd, "png", null), T = vd.ContainsKey("t") ? Num(vd["t"]) : 0f };
                            if (vd.TryGetValue("camera", out var cO2) && cO2 is Dictionary<string, object> cd)
                            {
                                if (cd.ContainsKey("center")) bv.Center = Vec(cd["center"]);
                                if (cd.ContainsKey("zoom")) bv.Zoom = Num(cd["zoom"]);
                            }
                            l.Bridge.Add(bv);
                        }
                l.Bridge.Sort((a, c) => a.T.CompareTo(c.T));
            }

            if (root.TryGetValue("interior", out var inO) && inO is Dictionary<string, object> inn)
            {
                ReadLayers(inn, l.InteriorLayers);
                if (inn.TryGetValue("arrival", out var aO) && aO is Dictionary<string, object> ar)
                {
                    if (ar.ContainsKey("from_center")) l.ArrivalCenter = Vec(ar["from_center"]);
                    if (ar.ContainsKey("from_zoom")) l.ArrivalZoom = Num(ar["from_zoom"]);
                }
                l.EntryDoorFeature = Str(inn, "entry_door_feature", l.EntryDoorFeature);
                if (inn.TryGetValue("features", out var fO) && fO is List<object> fl)
                    foreach (var o in fl)
                    {
                        if (!(o is Dictionary<string, object> fd)) continue;
                        var f = new Feature
                        {
                            Id = Str(fd, "id", ""), Label = Str(fd, "label", ""), Desc = Str(fd, "desc", ""),
                            LabelSide = Str(fd, "label_side", "below"),
                        };
                        if (fd.TryGetValue("hotspot", out var hO) && hO is Dictionary<string, object> h)
                        {
                            if (h.TryGetValue("polygon", out var pO) && pO is List<object> pl)
                            {
                                f.Polygon = new Vector2[pl.Count];
                                for (int i = 0; i < pl.Count; i++) f.Polygon[i] = Vec(pl[i]);
                                f.HotspotRect = Bounds(f.Polygon);
                            }
                            else f.HotspotRect = RectOf(h, "rect");
                        }
                        f.LabelAnchor = fd.ContainsKey("label_anchor") ? Vec(fd["label_anchor"]) : new Vector2(f.HotspotRect.center.x, f.HotspotRect.yMax);
                        l.Features.Add(f);
                    }
                if (inn.TryGetValue("nav_order", out var nO) && nO is List<object> nl)
                    foreach (var n in nl) if (n is string ns) l.NavOrder.Add(ns);
            }
            return l;
        }

        static void ReadLayers(Dictionary<string, object> d, List<Layer> into)
        {
            if (!d.TryGetValue("layers", out var lo) || !(lo is List<object> list)) return;
            foreach (var o in list) if (o is Dictionary<string, object> ld) into.Add(LayerOf(ld));
            into.Sort((a, b) => a.Z.CompareTo(b.Z));
        }

        static Layer LayerOf(Dictionary<string, object> ld) => new Layer
        {
            Id = Str(ld, "id", ""),
            Png = Str(ld, "png", null),
            Color = ld.ContainsKey("color") ? ColorOf(ld["color"] as string) : (Color?)null,
            Rect = RectOf(ld, "rect"),
            Z = ld.ContainsKey("z") ? (int)Num(ld["z"]) : 0,
            Tile = ld.TryGetValue("tile", out var t) && t is bool tb && tb,
            TileScale = ld.ContainsKey("tile_scale") ? Num(ld["tile_scale"]) : 1f,
        };

        static Rect Bounds(Vector2[] pts)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static string Str(Dictionary<string, object> d, string k, string def) => d.TryGetValue(k, out var v) && v is string s ? s : def;
        static float Num(object o) => o is double dd ? (float)dd : o is long ll ? ll : 0f;
        static Vector2 Vec(object o) => o is List<object> l && l.Count >= 2 ? new Vector2(Num(l[0]), Num(l[1])) : Vector2.zero;
        static Rect RectOf(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var o) && o is List<object> l && l.Count == 4 ? new Rect(Num(l[0]), Num(l[1]), Num(l[2]), Num(l[3])) : new Rect(0, 0, 1, 1);
        static Color? ColorOf(string hex) => hex != null && ColorUtility.TryParseHtmlString(hex, out var c) ? c : (Color?)null;

        /// <summary>최소 JSON 읽기(객체·배열·문자열·숫자·true/false/null). 숫자는 double.</summary>
        public static class MiniJson
        {
            public static object Parse(string s) { int i = 0; try { return Value(s, ref i); } catch { return null; } }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Ws(s, ref i);
                char c = s[i];
                if (c == '{') return Obj(s, ref i);
                if (c == '[') return Arr(s, ref i);
                if (c == '"') return StrLit(s, ref i);
                if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
                if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
                if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
                int st = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
            }

            static Dictionary<string, object> Obj(string s, ref int i)
            {
                var d = new Dictionary<string, object>();
                i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    var k = StrLit(s, ref i);
                    Ws(s, ref i); i++; // ':'
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d; // '}'
                }
            }

            static List<object> Arr(string s, ref int i)
            {
                var l = new List<object>();
                i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;
                }
            }

            static string StrLit(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        char e = s[i];
                        if (e == 'n') sb.Append('\n');
                        else if (e == 't') sb.Append('\t');
                        else if (e == 'u') { sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; }
                        else sb.Append(e);
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
        }
    }
}
