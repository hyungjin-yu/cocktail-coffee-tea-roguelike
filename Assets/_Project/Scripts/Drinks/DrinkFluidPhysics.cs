using System;
using System.Collections.Generic;

namespace CCT
{
    // 71 음료 제조 물리 축약 모델(순수 System C#, UnityEngine 비의존).
    // 단위: 부피 mL(=cm³), 질량 g, 밀도 g/mL. 높이 h01은 잔 내부 바닥 0 ~ 림 1로 정규화.
    // 단면 좌표 x도 같은 등방 정규 단위(cm / 잔 내부 높이 cm)이며 x=0이 잔 축이다.
    // 이 파일은 정지 평형(얼음 부력·액면)과 1차원 부피 셀 혼합만 계산한다. CFD·열역학·융해·에탄올/물 체적 수축은 계산하지 않는다.

    /// <summary>한 액체 성분의 양과 물성. 같은 id는 같은 물성이어야 한다(농도가 다르면 id를 분리).</summary>
    public struct FluidPart
    {
        public string id;
        public double volumeMl;
        /// <summary>g/mL.</summary>
        public double density;
        /// <summary>0~1 게임 점성(DrinkIngredientDef.viscosity와 같은 척도). 혼합·침강 속도만 늦춘다.</summary>
        public double viscosity;
        /// <summary>혼화성 그룹. 0 = 수성/알코올(현재 8종+물 모두). 다른 그룹끼리는 확산으로 섞이지 않고 밀도로 분리된다.</summary>
        public int miscibilityGroup;

        public FluidPart(string id, double volumeMl, double density, double viscosity = 0, int miscibilityGroup = 0)
        {
            this.id = id; this.volumeMl = volumeMl; this.density = density; this.viscosity = viscosity; this.miscibilityGroup = miscibilityGroup;
        }

        public double MassG => volumeMl * density;
    }

    /// <summary>정지 상태의 한 층. bottom01/top01은 SolveIce가 얼음 점유 공간을 제외해 채운다.</summary>
    public sealed class FluidLayer
    {
        public FluidPart[] parts = Array.Empty<FluidPart>();
        public double Volume, Mass, Density;
        public double bottom01, top01;

        public static FluidLayer From(IEnumerable<FluidPart> parts)
        {
            var list = new List<FluidPart>();
            double v = 0, m = 0;
            foreach (var p in parts)
            {
                FluidValidate.Part(p);
                if (p.volumeMl <= 0) continue;
                list.Add(p); v += p.volumeMl; m += p.MassG;
            }
            return new FluidLayer { parts = list.ToArray(), Volume = v, Mass = m, Density = v > 0 ? m / v : 0 };
        }

        public FluidLayer Scaled(double factor)
        {
            var ps = new FluidPart[parts.Length];
            for (int i = 0; i < ps.Length; i++) { ps[i] = parts[i]; ps[i].volumeMl *= factor; }
            return new FluidLayer { parts = ps, Volume = Volume * factor, Mass = Mass * factor, Density = Density, bottom01 = bottom01, top01 = top01 };
        }
    }

    public struct FluidPoint
    {
        public double X, Y;
        public FluidPoint(double x, double y) { X = x; Y = y; }
        public override string ToString() => $"({X:0.####}, {Y:0.####})";
    }

    /// <summary>
    /// 잔 내부의 단조 누적 용량 C(h). 구간별 단면적은 상수(계단형 회전체)라 C(h)는 구간 선형이며 보정표와 정확히 일치한다.
    /// FromRowWidths는 회전 대칭 가정(단면적 ∝ 폭²)이고 픽셀 종횡비로 실제 cm 척도를 유도한다. 2D 마스크가 정확한 3D 용량을 보증하지 않는다.
    /// </summary>
    public sealed class FluidVesselProfile
    {
        readonly double[] h;    // 구간 경계 h01 (h[0]=0, h[n]=1)
        readonly double[] cum;  // 경계에서의 누적 mL
        public double CapacityMl { get; }
        /// <summary>내부 높이(cm). 얼음 크기와 잔 크기를 같은 척도로 맞춘다.</summary>
        public double HeightCm { get; }
        public int SegmentCount => h.Length - 1;

        FluidVesselProfile(double[] h, double[] cum, double heightCm)
        {
            this.h = h; this.cum = cum; HeightCm = heightCm; CapacityMl = cum[cum.Length - 1];
        }

        /// <summary>마스크 행별 내부 폭(px, 아래→위)과 보정 용량. 회전 대칭 근사.</summary>
        public static FluidVesselProfile FromRowWidths(double[] innerWidthsPx, double capacityMl)
        {
            if (innerWidthsPx == null || innerWidthsPx.Length == 0) throw new ArgumentException("widths required");
            FluidValidate.Positive(capacityMl, nameof(capacityMl));
            int n = innerWidthsPx.Length;
            double sumSq = 0;
            foreach (var w in innerWidthsPx) { FluidValidate.NonNegative(w, "width"); sumSq += w * w; }
            if (sumSq <= 0) throw new ArgumentException("all widths are zero");
            // capacity = Σ π (w s / 2)² s  →  s(cm/px) = cbrt(4C / (π Σw²))
            double s = Math.Pow(4 * capacityMl / (Math.PI * sumSq), 1.0 / 3.0);
            var hh = new double[n + 1];
            var cc = new double[n + 1];
            for (int i = 0; i < n; i++)
            {
                hh[i + 1] = (i + 1) / (double)n;
                cc[i + 1] = cc[i] + capacityMl * innerWidthsPx[i] * innerWidthsPx[i] / sumSq;
            }
            hh[n] = 1; cc[n] = capacityMl;
            return new FluidVesselProfile(hh, cc, n * s);
        }

        /// <summary>보정한 단면별 용량표. heights01은 0에서 1까지 증가, cumulativeMl은 0에서 단조 비감소.</summary>
        public static FluidVesselProfile FromCapacityTable(double[] heights01, double[] cumulativeMl, double heightCm)
        {
            if (heights01 == null || cumulativeMl == null || heights01.Length != cumulativeMl.Length || heights01.Length < 2)
                throw new ArgumentException("table needs >=2 matching points");
            FluidValidate.Positive(heightCm, nameof(heightCm));
            int n = heights01.Length;
            if (heights01[0] != 0 || heights01[n - 1] != 1) throw new ArgumentException("heights must span 0..1");
            if (cumulativeMl[0] != 0) throw new ArgumentException("cumulative must start at 0");
            for (int i = 0; i < n; i++)
            {
                FluidValidate.Finite(heights01[i], "height"); FluidValidate.NonNegative(cumulativeMl[i], "cumulative");
                if (i > 0 && heights01[i] <= heights01[i - 1]) throw new ArgumentException("heights must strictly increase");
                if (i > 0 && cumulativeMl[i] < cumulativeMl[i - 1]) throw new ArgumentException("capacity must be monotone");
            }
            if (cumulativeMl[n - 1] <= 0) throw new ArgumentException("capacity must be positive");
            return new FluidVesselProfile((double[])heights01.Clone(), (double[])cumulativeMl.Clone(), heightCm);
        }

        /// <summary>일정 단면 원통(시험·참조용).</summary>
        public static FluidVesselProfile Cylinder(double capacityMl, double heightCm) =>
            FromCapacityTable(new[] { 0.0, 1.0 }, new[] { 0.0, capacityMl }, heightCm);

        int SegmentAt(double h01)
        {
            if (h01 <= 0) return 0;
            if (h01 >= 1) return h.Length - 2;
            int lo = 0, hi = h.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (h[mid] <= h01) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>단면적(mL per unit h01).</summary>
        public double AreaAt(double h01)
        {
            int k = SegmentAt(h01);
            return (cum[k + 1] - cum[k]) / (h[k + 1] - h[k]);
        }

        public double VolumeBelow(double h01)
        {
            if (double.IsNaN(h01)) throw new ArgumentException("h01 NaN");
            if (h01 <= 0) return 0;
            if (h01 >= 1) return CapacityMl;
            int k = SegmentAt(h01);
            return cum[k] + (cum[k + 1] - cum[k]) * (h01 - h[k]) / (h[k + 1] - h[k]);
        }

        /// <summary>C(h)=v의 해. 단면 0 구간에서는 가장 낮은 해. 용량 초과는 1.</summary>
        public double HeightForVolume(double volumeMl)
        {
            FluidValidate.Finite(volumeMl, nameof(volumeMl));
            if (volumeMl <= 0) return 0;
            if (volumeMl >= CapacityMl) return 1;
            for (int k = 0; k < h.Length - 1; k++)
            {
                if (volumeMl <= cum[k + 1] && cum[k + 1] > cum[k])
                    return h[k] + (h[k + 1] - h[k]) * (volumeMl - cum[k]) / (cum[k + 1] - cum[k]);
            }
            return 1;
        }

        /// <summary>등방 정규 단위 반폭(회전 대칭 가정: A = π r²).</summary>
        public double HalfWidthAt(double h01) => RadiusFromArea(AreaAt(h01));

        internal double RadiusFromArea(double area01) =>
            Math.Sqrt(Math.Max(0, area01) / (Math.PI * HeightCm * HeightCm * HeightCm));

        internal double SegmentLow(int k) => h[k];
        internal double CumulativeAt(int boundary) => cum[boundary];
        internal double SegmentHigh(int k) => h[k + 1];
        internal double SegmentRadius(int k) => RadiusFromArea((cum[k + 1] - cum[k]) / (h[k + 1] - h[k]));

        /// <summary>정규 길이를 px로(행 수 = 내부 높이 px).</summary>
        public static double ToPixels(double length01, int heightPx) => length01 * heightPx;
    }

    public enum FluidIceSupport { Floating = 0, Floor = 1, IceBelow = 2 }

    public sealed class FluidIceCube
    {
        public double Bottom01, Top01, Centre01, CentreX, Side01;
        public double VolumeMl, MassG, SubmergedMl;
        /// <summary>해 층별 잠긴 부피(mL). Layers와 같은 순서.</summary>
        public double[] VolumeInLayer = Array.Empty<double>();
        public FluidIceSupport Support;
        /// <summary>잔 폭이 정육면체를 담기에 충분한 위치를 찾았는지.</summary>
        public bool Fits = true;
        public double SidePx(int heightPx) => Side01 * heightPx;
    }

    public sealed class FluidIceSolution
    {
        public bool HasLiquid;
        public double Surface01;
        /// <summary>잔에 남은 액체 부피(넘친 양 제외). 입력 액체 = LiquidVolume + OverflowMl.</summary>
        public double LiquidVolume;
        /// <summary>수면 아래 얼음 부피 = 배제 부피.</summary>
        public double SubmergedIceVolume;
        /// <summary>LiquidVolume + SubmergedIceVolume = C(Surface01).</summary>
        public double OccupiedBelowSurface;
        public double IceAboveSurfaceVolume;
        /// <summary>수면 위 기하 여유 = Capacity − C(Surface01).</summary>
        public double HeadspaceMl;
        /// <summary>림 아래 실제 빈 공간 = Capacity − 액체 − 림 아래 얼음 전체.</summary>
        public double FreeVolumeMl;
        public double OverflowMl;
        public double LiquidMass;
        public FluidLayer[] Layers = Array.Empty<FluidLayer>();
        public FluidIceCube[] Cubes = Array.Empty<FluidIceCube>();
        public bool Converged;
        public int Iterations;
    }

    public sealed class FluidTiltSolution
    {
        /// <summary>층 경계(아래→위 누적)의 세계 높이 평면 값 c: 국소점 (x,y)가 −x·sinθ + y·cosθ ≤ c이면 액체.</summary>
        public double[] Levels = Array.Empty<double>();
        public double SpillMl;
        /// <summary>액체가 입구(+x 쪽 림 최저점)에 닿음 = 따를 수 있음.</summary>
        public bool LipReached;
        public double LipLevel;
        public FluidPoint Lip;
        /// <summary>국소 단면 좌표의 최상단 수면 선분(액체가 없으면 빈 배열).</summary>
        public FluidPoint[] SurfaceLine = Array.Empty<FluidPoint>();
        /// <summary>층별 국소 단면 다각형(아래→위). 렌더러가 국소→세계 회전을 적용한다.</summary>
        public FluidPoint[][] LayerPolygons = Array.Empty<FluidPoint[]>();
        public double[] RetainedCumulativeMl = Array.Empty<double>();
    }

    /// <summary>
    /// 1차원 부피 좌표 혼합 상태(아래→위 동일 부피 셀). 성분 부피를 정확히 옮겨 질량·성분 보존.
    /// 과정: 붓기 유입(위에 추가 + 붓기 세기에 따른 동반 혼합), 밀도 역전 전복(무거운 셀이 시간에 걸쳐 가라앉으며 경계 혼합),
    /// 혼화 액체의 느린 확산, 비혼화 그룹 분리, 젓기/셰이킹의 전역 균일화. 계수는 게임 축약값이다.
    /// </summary>
    public sealed class FluidColumn
    {
        sealed class Comp { public string id; public double density, resist; public int group; }

        public const int DefaultCells = 64;
        /// <summary>밀도 역전 전복 속도(셀 교환/s, Δρ/ρ=0.1·점성 0 기준).</summary>
        public const double OverturnRate = 25.0;
        /// <summary>전복 덩어리가 기둥 전체 깊이를 지날 때 주변과 섞이는 총 비율(경계 동반 혼합). 셀 하나 교환당 값은 셀 수와 점성 저항으로 나눈다(꿀처럼 끈적한 덩어리는 덜 섞인 채 가라앉는다).</summary>
        public const double EntrainPerDepth = 0.4;
        /// <summary>정지 상태 혼화 확산(셀 쌍 교환/s). 실제 분자 확산보다 빠른 게임값(취급 진동·대류 포함).</summary>
        public const double RestDiffusion = 0.02;
        public const double StirPairDiffusion = 20.0;
        public const double StirGlobalRate = 1.2;
        public const double SeparationRate = 4.0;
        public const double EntrainFactor = 1.5;
        const double MaxSubstep = 0.02;
        const int MaxSubsteps = 50000;

        readonly List<Comp> comps = new List<Comp>();
        readonly int cellCount;
        double[][] cells; // [cell][comp] 성분 부피
        double cellVolume;
        double stirStrength;
        readonly double[] overturnBudget;

        public FluidColumn(int cells = DefaultCells)
        {
            if (cells < 2 || cells > 4096) throw new ArgumentOutOfRangeException(nameof(cells));
            cellCount = cells;
            overturnBudget = new double[cells];
            this.cells = NewCells(0);
        }

        public int CellCount => cellCount;
        public double TotalVolume => cellVolume * cellCount;
        public bool IsEmpty => cellVolume <= 0;

        public double TotalMass
        {
            get { double m = 0; for (int c = 0; c < comps.Count; c++) m += ComponentVolumeAt(c) * comps[c].density; return m; }
        }

        public double ComponentVolume(string id)
        {
            int c = comps.FindIndex(x => x.id == id);
            return c < 0 ? 0 : ComponentVolumeAt(c);
        }

        double ComponentVolumeAt(int c) { double v = 0; for (int i = 0; i < cellCount; i++) v += cells[i][c]; return v; }

        public double CellDensity(int i)
        {
            double v = 0, m = 0;
            for (int c = 0; c < comps.Count; c++) { v += cells[i][c]; m += cells[i][c] * comps[c].density; }
            return v > 0 ? m / v : 0;
        }

        public FluidPart[] CellParts(int i)
        {
            var list = new List<FluidPart>();
            for (int c = 0; c < comps.Count; c++) if (cells[i][c] > 0) list.Add(MakePart(c, cells[i][c]));
            return list.ToArray();
        }

        public FluidColumn Clone()
        {
            var o = new FluidColumn(cellCount);
            foreach (var c in comps) o.comps.Add(new Comp { id = c.id, density = c.density, resist = c.resist, group = c.group });
            o.cells = new double[cellCount][];
            for (int i = 0; i < cellCount; i++) o.cells[i] = (double[])cells[i].Clone();
            o.cellVolume = cellVolume;
            Array.Copy(overturnBudget, o.overturnBudget, cellCount);
            return o;
        }

        public void Pour(FluidPart part, double gentleness01) => Pour(new[] { part }, gentleness01);

        /// <summary>새 액체를 위에 넣는다. gentleness01=1은 바스푼 레이어링, 0은 높은 낙차의 빠른 붓기(위쪽 동반 혼합 최대).</summary>
        public void Pour(IEnumerable<FluidPart> parts, double gentleness01)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            FluidValidate.Unit(gentleness01, nameof(gentleness01));
            var incoming = new List<(int c, double v)>();
            double vin = 0;
            foreach (var p in parts)
            {
                FluidValidate.Part(p);
                if (p.volumeMl <= 0) continue;
                incoming.Add((Register(p), p.volumeMl));
                vin += p.volumeMl;
            }
            if (vin <= 0) return;
            double vold = TotalVolume;
            // 쌓기: 기존 셀 + 위에 새 블록, 동일 부피 셀로 보존 재표본.
            var segs = new List<(double vol, double[] comp)>();
            for (int i = 0; i < cellCount && cellVolume > 0; i++) segs.Add((cellVolume, (double[])cells[i].Clone()));
            var block = new double[comps.Count];
            foreach (var (c, v) in incoming) block[c] += v;
            segs.Add((vin, block));
            Rebin(segs, vold + vin);
            // 붓기 줄기의 동반 혼합: 위쪽 (유입 + 동반) 부피 영역을 (1−gentleness)만큼 섞는다.
            double phi = 1 - gentleness01;
            if (phi > 0 && vold > 0)
            {
                double region = vin + Math.Min(vold, vin * EntrainFactor * phi);
                int k = Math.Max(1, Math.Min(cellCount, (int)Math.Ceiling(region / cellVolume - 1e-9)));
                BlendRange(cellCount - k, cellCount, phi);
            }
        }

        /// <summary>정확히 volumeMl를 위(따르기)나 아래에서 뺀다. 총량 초과는 거부(몰래 줄이지 않음).</summary>
        public FluidPart[] Withdraw(double volumeMl, bool fromTop = true)
        {
            FluidValidate.NonNegative(volumeMl, nameof(volumeMl));
            double total = TotalVolume;
            if (volumeMl > total * (1 + 1e-12) + 1e-9) throw new ArgumentOutOfRangeException(nameof(volumeMl), $"withdraw {volumeMl} > {total}");
            volumeMl = Math.Min(volumeMl, total);
            var taken = new double[comps.Count];
            if (volumeMl <= 0) return Array.Empty<FluidPart>();
            var segs = new List<(double vol, double[] comp)>();
            double remain = volumeMl;
            for (int n = 0; n < cellCount; n++)
            {
                int i = fromTop ? cellCount - 1 - n : n;
                double take = Math.Min(remain, cellVolume);
                double keepFrac = cellVolume > 0 ? 1 - take / cellVolume : 0;
                var keep = new double[comps.Count];
                for (int c = 0; c < comps.Count; c++) { double t = cells[i][c] * (1 - keepFrac); taken[c] += t; keep[c] = cells[i][c] - t; }
                remain -= take;
                if (cellVolume - take > 0) segs.Add((cellVolume - take, keep));
            }
            if (fromTop) segs.Reverse(); // 위→아래로 모였으므로 아래→위로
            Rebin(segs, total - volumeMl);
            var result = new List<FluidPart>();
            for (int c = 0; c < comps.Count; c++) if (taken[c] > 0) result.Add(MakePart(c, taken[c]));
            return result.ToArray();
        }

        /// <summary>정지 경과. 밀도 역전 침강·확산·비혼화 분리. 유한 반복(하위 단계 상한).</summary>
        public void Step(double dtSeconds)
        {
            FluidValidate.NonNegative(dtSeconds, nameof(dtSeconds));
            if (dtSeconds <= 0 || cellVolume <= 0) return;
            int n = (int)Math.Min(MaxSubsteps, Math.Ceiling(dtSeconds / MaxSubstep));
            double dt = dtSeconds / n;
            var dens = new double[cellCount];
            var resist = new double[cellCount];
            for (int s = 0; s < n; s++)
            {
                for (int parity = 0; parity < 2; parity++)
                {
                    for (int i = 0; i < cellCount; i++) { dens[i] = CellDensity(i); resist[i] = CellResist(i); }
                    for (int i = (s + parity) & 1; i + 1 < cellCount; i += 2)
                    {
                        int j = i + 1; // j가 위
                        double r = 0.5 * (resist[i] + resist[j]);
                        double rhoBar = 0.5 * (dens[i] + dens[j]);
                        // 밀도 역전 전복: 셀 덩어리(parcel)가 √(Δρ/ρ)/점성에 비례하는 속도(셀/s)로 자리를 바꾼다.
                        // 부분 교환은 확산처럼 기울기만 만들므로, 위치별 예산이 1이 되면 통째로 교환하고 EntrainPerDepth에 비례해 경계 혼합한다.
                        if (dens[j] > dens[i] * (1 + 1e-6) && rhoBar > 0)
                        {
                            double d = (dens[j] - dens[i]) / rhoBar;
                            overturnBudget[i] += OverturnRate * Math.Sqrt(d / 0.1) * dt / r;
                            if (overturnBudget[i] >= 1)
                            {
                                overturnBudget[i] = Math.Min(1, overturnBudget[i] - 1);
                                Exchange(i, j, 1 - Math.Min(0.5, EntrainPerDepth / (cellCount * r)));
                                dens[i] = CellDensity(i); dens[j] = CellDensity(j);
                            }
                        }
                        else overturnBudget[i] = 0;
                        double m = Miscibility(i, j);
                        double diff = (RestDiffusion + StirPairDiffusion * stirStrength) * m * dt / r;
                        if (diff > 0) Exchange(i, j, Math.Min(0.25, diff));
                        Separate(i, j, dt / r);
                    }
                }
                if (stirStrength > 0) BlendRange(0, cellCount, 1 - Math.Exp(-StirGlobalRate * stirStrength * dt));
            }
        }

        /// <summary>젓기(≈0.6)/셰이킹(1.0) 강도로 seconds 동안 섞는다. 혼합 후 혼화 성분은 다시 분리되지 않는다.</summary>
        public void Mix(double strength01, double seconds)
        {
            FluidValidate.Unit(strength01, nameof(strength01));
            stirStrength = strength01;
            try { Step(seconds); } finally { stirStrength = 0; }
        }

        /// <summary>
        /// 인접 셀 조성이 비슷하면 한 층으로 묶는다(L1 조성 비율 차 &lt; tolerance).
        /// 침강 중 1차원 셀은 무거운/가벼운 셀이 번갈아 놓이는데(실제로는 옆으로 나란한 손가락 흐름), 표시·부력용으로
        /// 밀도가 뒤집힌 이웃 쌍만 fingerSmoothingPasses번 질량 보존 평균한다. 안정된 층 경계는 날카롭게 남는다. 내부 상태는 바꾸지 않는다.
        /// </summary>
        public FluidLayer[] GetLayers(double compositionTolerance = 0.04, int fingerSmoothingPasses = 3)
        {
            FluidValidate.NonNegative(compositionTolerance, nameof(compositionTolerance));
            if (fingerSmoothingPasses < 0 || fingerSmoothingPasses > 64) throw new ArgumentOutOfRangeException(nameof(fingerSmoothingPasses));
            var layers = new List<FluidLayer>();
            if (cellVolume <= 0) return layers.ToArray();
            var saved = cells;
            cells = new double[cellCount][];
            for (int i = 0; i < cellCount; i++) cells[i] = (double[])saved[i].Clone();
            try
            {
                for (int pass = 0; pass < fingerSmoothingPasses; pass++)
                    for (int parity = 0; parity < 2; parity++)
                        for (int i = parity; i + 1 < cellCount; i += 2)
                            if (CellDensity(i + 1) > CellDensity(i) * (1 + 1e-6)) Exchange(i, i + 1, 0.5);
                double[] acc = null; double accV = 0;
                for (int i = 0; i < cellCount; i++)
                {
                    if (acc != null && FractionDistance(acc, accV, cells[i], cellVolume) >= compositionTolerance)
                    {
                        layers.Add(LayerFrom(acc)); acc = null;
                    }
                    if (acc == null) { acc = new double[comps.Count]; accV = 0; }
                    for (int c = 0; c < comps.Count; c++) acc[c] += cells[i][c];
                    accV += cellVolume;
                }
                if (acc != null) layers.Add(LayerFrom(acc));
            }
            finally { cells = saved; }
            return layers.ToArray();
        }

        FluidLayer LayerFrom(double[] acc)
        {
            var ps = new List<FluidPart>();
            for (int c = 0; c < comps.Count; c++) if (acc[c] > 0) ps.Add(MakePart(c, acc[c]));
            return FluidLayer.From(ps);
        }

        static double FractionDistance(double[] a, double va, double[] b, double vb)
        {
            double d = 0;
            for (int c = 0; c < a.Length; c++) d += Math.Abs(a[c] / va - b[c] / vb);
            return d;
        }

        FluidPart MakePart(int c, double v) => new FluidPart(comps[c].id, v, comps[c].density, (comps[c].resist - 1) / 9.0, comps[c].group);

        int Register(FluidPart p)
        {
            for (int c = 0; c < comps.Count; c++)
            {
                if (comps[c].id != p.id) continue;
                if (Math.Abs(comps[c].density - p.density) > 1e-9 || comps[c].group != p.miscibilityGroup)
                    throw new ArgumentException($"component '{p.id}' re-registered with different properties; use a distinct id per concentration");
                return c;
            }
            comps.Add(new Comp { id = p.id, density = p.density, resist = 1 + 9 * p.viscosity, group = p.miscibilityGroup });
            for (int i = 0; i < cellCount; i++) Array.Resize(ref cells[i], comps.Count);
            return comps.Count - 1;
        }

        double[][] NewCells(int compCount)
        {
            var a = new double[cellCount][];
            for (int i = 0; i < cellCount; i++) a[i] = new double[compCount];
            return a;
        }

        // 구간 상수 조성 분포를 동일 부피 셀로 정확히 재분배(성분 부피 보존).
        void Rebin(List<(double vol, double[] comp)> segs, double total)
        {
            var next = NewCells(comps.Count);
            if (total <= 1e-15) { cells = next; cellVolume = 0; return; }
            double cv = total / cellCount;
            int cell = 0; double room = cv;
            foreach (var (vol, comp) in segs)
            {
                if (vol <= 0) continue;
                double left = vol;
                while (left > 1e-15)
                {
                    double put = cell == cellCount - 1 ? left : Math.Min(left, room);
                    double frac = put / vol;
                    for (int c = 0; c < comp.Length; c++) next[cell][c] += comp[c] * frac;
                    left -= put; room -= put;
                    if (room <= 1e-15 && cell < cellCount - 1) { cell++; room = cv; }
                }
            }
            cells = next; cellVolume = cv;
            Array.Clear(overturnBudget, 0, cellCount);
        }

        void BlendRange(int from, int to, double f)
        {
            if (to - from < 2 || f <= 0) return;
            var mean = new double[comps.Count];
            for (int i = from; i < to; i++) for (int c = 0; c < comps.Count; c++) mean[c] += cells[i][c];
            for (int c = 0; c < comps.Count; c++) mean[c] /= (to - from);
            for (int i = from; i < to; i++) for (int c = 0; c < comps.Count; c++) cells[i][c] += f * (mean[c] - cells[i][c]);
        }

        void Exchange(int i, int j, double f)
        {
            var a = cells[i]; var b = cells[j];
            for (int c = 0; c < a.Length; c++) { double t = f * (b[c] - a[c]); a[c] += t; b[c] -= t; }
        }

        double CellResist(int i)
        {
            double v = 0, r = 0;
            for (int c = 0; c < comps.Count; c++) { v += cells[i][c]; r += cells[i][c] * comps[c].resist; }
            return v > 0 ? r / v : 1;
        }

        // 그룹 분율 겹침(Bhattacharyya). 같은 그룹 1, 완전 다른 그룹 0.
        double Miscibility(int i, int j)
        {
            bool single = true; int g0 = comps.Count > 0 ? comps[0].group : 0;
            foreach (var c in comps) if (c.group != g0) { single = false; break; }
            if (single) return 1;
            var gi = GroupFractions(i); var gj = GroupFractions(j);
            double s = 0;
            foreach (var kv in gi) if (gj.TryGetValue(kv.Key, out var fj)) s += Math.Sqrt(kv.Value * fj);
            return Math.Min(1, s);
        }

        Dictionary<int, double> GroupFractions(int i)
        {
            var d = new Dictionary<int, double>(); double v = 0;
            for (int c = 0; c < comps.Count; c++) { v += cells[i][c]; d.TryGetValue(comps[c].group, out var x); d[comps[c].group] = x + cells[i][c]; }
            if (v > 0) foreach (var k in new List<int>(d.Keys)) d[k] /= v;
            return d;
        }

        // 비혼화 그룹 분리: 아래 셀의 가벼운 그룹과 위 셀의 무거운 그룹을 같은 부피만큼 맞바꾼다(셀 부피·성분 보존).
        void Separate(int lower, int upper, double dtOverResist)
        {
            int g0 = comps.Count > 0 ? comps[0].group : 0; bool multi = false;
            foreach (var c in comps) if (c.group != g0) { multi = true; break; }
            if (!multi) return;
            var groupDensity = new Dictionary<int, (double m, double v)>();
            for (int c = 0; c < comps.Count; c++)
            {
                double v = cells[lower][c] + cells[upper][c];
                groupDensity.TryGetValue(comps[c].group, out var gd);
                groupDensity[comps[c].group] = (gd.m + v * comps[c].density, gd.v + v);
            }
            int light = int.MinValue, heavy = int.MinValue; double lightRho = double.MaxValue, heavyRho = double.MinValue;
            foreach (var kv in groupDensity)
            {
                if (kv.Value.v <= 0) continue;
                double rho = kv.Value.m / kv.Value.v;
                if (rho < lightRho) { lightRho = rho; light = kv.Key; }
                if (rho > heavyRho) { heavyRho = rho; heavy = kv.Key; }
            }
            if (light == heavy || light == int.MinValue) return;
            double lightBelow = 0, heavyAbove = 0;
            for (int c = 0; c < comps.Count; c++)
            {
                if (comps[c].group == light) lightBelow += cells[lower][c];
                if (comps[c].group == heavy) heavyAbove += cells[upper][c];
            }
            double move = Math.Min(lightBelow, heavyAbove) * Math.Min(0.5, SeparationRate * dtOverResist);
            if (move <= 0) return;
            for (int c = 0; c < comps.Count; c++)
            {
                if (comps[c].group == light && lightBelow > 0) { double t = cells[lower][c] / lightBelow * move; cells[lower][c] -= t; cells[upper][c] += t; }
                else if (comps[c].group == heavy && heavyAbove > 0) { double t = cells[upper][c] / heavyAbove * move; cells[upper][c] -= t; cells[lower][c] += t; }
            }
        }
    }

    /// <summary>71 정지 평형 계산: 혼합 밀도, 얼음 부력/배제 부피/접촉, 기울어진 잔의 중력 수평 액면.</summary>
    public static class DrinkFluidPhysics
    {
        public const double IceDensity = 0.917;
        /// <summary>정육면체 얼음이 한 높이대에서 차지할 수 있는 단면 비율(원형 잔 내 사각 배치 근사).</summary>
        public const double IcePacking = 0.55;
        const int MaxOuterIterations = 200;
        const int BisectionIterations = 52;

        /// <summary>부피 가산 근사의 혼합 밀도 Σm/ΣV. 실제 에탄올/물 체적 수축은 무시한다.</summary>
        public static double MixDensity(IEnumerable<FluidPart> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            double v = 0, m = 0;
            foreach (var p in parts) { FluidValidate.Part(p); v += p.volumeMl; m += p.MassG; }
            return v > 0 ? m / v : 0;
        }

        /// <summary>층을 거치는 원천→받는 용기 이동. 반환값은 실제로 옮긴 성분(공중 줄기 상태가 필요하면 호출자가 이 배열을 들고 있다가 Pour).</summary>
        public static FluidPart[] Transfer(FluidColumn source, FluidColumn destination, double volumeMl, double gentleness01)
        {
            if (source == null || destination == null) throw new ArgumentNullException(source == null ? nameof(source) : nameof(destination));
            var moved = source.Withdraw(volumeMl, true);
            destination.Pour(moved, gentleness01);
            return moved;
        }

        sealed class Cube { public int column, tier; public double z, floor, side, footprint; public bool fits = true; }

        /// <summary>
        /// 직립 잔의 정지 평형. 층은 아래→위. 얼음은 정육면체(volume^(1/3) cm), 각 층에 잠긴 부피의 부력 합 Σρ_k V_k = m_ice로 평형,
        /// 바닥/좁은 벽/아래 얼음 접촉 제한. 수면: C(h) − 얼음 잠김(h) = V_liquid. 액체가 없으면 수면 없음, 얼음은 바닥.
        /// </summary>
        public static FluidIceSolution SolveIce(FluidVesselProfile vessel, IReadOnlyList<FluidLayer> layersBottomToTop, int cubeCount, double cubeVolumeMl, double iceDensity = IceDensity)
        {
            if (vessel == null) throw new ArgumentNullException(nameof(vessel));
            if (cubeCount < 0 || cubeCount > 64) throw new ArgumentOutOfRangeException(nameof(cubeCount));
            if (cubeCount > 0) FluidValidate.Positive(cubeVolumeMl, nameof(cubeVolumeMl));
            FluidValidate.Positive(iceDensity, nameof(iceDensity));
            var layers = new List<FluidLayer>();
            if (layersBottomToTop != null)
                foreach (var l in layersBottomToTop)
                {
                    if (l == null) throw new ArgumentException("null layer");
                    FluidValidate.NonNegative(l.Volume, "layer volume");
                    if (l.Volume <= 0) continue;
                    FluidValidate.Positive(l.Density, "layer density");
                    layers.Add(l);
                }
            double liquid = 0; foreach (var l in layers) liquid += l.Volume;

            double H = vessel.HeightCm;
            double sideCm = cubeCount > 0 ? Math.Pow(cubeVolumeMl, 1.0 / 3.0) : 0;
            double side01 = sideCm / H;
            double footprint = sideCm * sideCm * H; // mL per unit h01
            double cubeMass = cubeVolumeMl * iceDensity;

            // 열(column)/단(tier) 배치: 한 개가 들어가는 가장 낮은 높이대의 최소 단면이 허용하는 열 수(위가 넓은 잔에서는 보수적으로 더 쌓인다).
            bool fits = true;
            double floor1 = cubeCount > 0 ? TierFloor(vessel, side01, footprint, out fits) : 0;
            int columns = 1;
            if (cubeCount > 0)
            {
                double minArea = double.MaxValue;
                for (int p = 0; p <= 12; p++) minArea = Math.Min(minArea, vessel.AreaAt(Math.Min(1 - 1e-12, floor1 + side01 * p / 12)));
                columns = Math.Max(1, Math.Min(cubeCount, (int)Math.Floor(IcePacking * minArea / footprint)));
            }
            var cubes = new Cube[cubeCount];
            for (int i = 0; i < cubeCount; i++)
                cubes[i] = new Cube { column = i % columns, tier = i / columns, side = side01, footprint = footprint, floor = floor1, z = floor1, fits = fits };
            // 같은 열의 단 순서로 초기 적층.
            for (int i = 0; i < cubeCount; i++) if (cubes[i].tier > 0) cubes[i].z = Math.Max(cubes[i].z, cubes[i - columns].z + side01);

            var cum = new double[layers.Count];
            double run = 0;
            for (int k = 0; k < layers.Count; k++) { run += layers[k].Volume; cum[k] = run; }

            bool converged = cubeCount == 0 || liquid <= 0;
            int iter = 0;
            if (!converged)
            {
                for (iter = 1; iter <= MaxOuterIterations; iter++)
                {
                    double maxMove = 0;
                    for (int col = 0; col < columns; col++)
                    {
                        var idx = new List<int>();
                        for (int i = col; i < cubeCount; i += columns) idx.Add(i);
                        maxMove = Math.Max(maxMove, SolveColumn(vessel, layers, cum, cubes, idx, cubeMass));
                    }
                    if (maxMove < 1e-11) { converged = true; break; }
                }
                if (iter > MaxOuterIterations) iter = MaxOuterIterations;
            }

            // 최종 기하.
            var occ = Occupancy.Build(vessel, cubes);
            double retainedCap = occ.VolumeBelow(1);
            double overflow = Math.Max(0, liquid - retainedCap);
            var sol = new FluidIceSolution { Converged = converged, Iterations = iter, OverflowMl = overflow };
            // 넘친 양은 맨 위층부터 제거(성분 비율 유지).
            var kept = new List<FluidLayer>();
            double toRemove = overflow;
            for (int k = layers.Count - 1; k >= 0; k--)
            {
                var l = layers[k];
                if (toRemove > 0)
                {
                    double rm = Math.Min(toRemove, l.Volume); toRemove -= rm;
                    if (l.Volume - rm <= 1e-12) continue;
                    l = l.Scaled((l.Volume - rm) / l.Volume);
                }
                else l = l.Scaled(1);
                kept.Insert(0, l);
            }
            double below = 0, mass = 0;
            foreach (var l in kept)
            {
                l.bottom01 = occ.HeightFor(below);
                below += l.Volume; mass += l.Mass;
                l.top01 = occ.HeightFor(below);
            }
            sol.Layers = kept.ToArray();
            sol.LiquidVolume = below;
            sol.LiquidMass = mass;
            sol.HasLiquid = below > 0;
            sol.Surface01 = sol.HasLiquid ? occ.HeightFor(below) : 0;
            double surf = sol.Surface01;

            var outCubes = new FluidIceCube[cubeCount];
            double submerged = 0, above = 0, iceBelowRim = 0;
            for (int i = 0; i < cubeCount; i++)
            {
                var c = cubes[i];
                var oc = new FluidIceCube
                {
                    Bottom01 = c.z, Top01 = c.z + side01, Centre01 = c.z + side01 / 2, Side01 = side01,
                    VolumeMl = cubeVolumeMl, MassG = cubeMass, Fits = c.fits,
                    VolumeInLayer = new double[kept.Count],
                };
                double sub = 0;
                for (int k = 0; k < kept.Count; k++)
                {
                    double v = footprint * Overlap(c.z, c.z + side01, kept[k].bottom01, kept[k].top01);
                    oc.VolumeInLayer[k] = v; sub += v;
                }
                if (!sol.HasLiquid) sub = 0;
                oc.SubmergedMl = sub;
                submerged += sub;
                double inRim = footprint * Overlap(c.z, c.z + side01, 0, 1);
                iceBelowRim += inRim;
                above += cubeVolumeMl - sub;
                outCubes[i] = oc;
            }
            // 지지 판정과 가로 배치.
            for (int i = 0; i < cubeCount; i++)
            {
                var c = cubes[i];
                bool onIce = c.tier > 0 && Math.Abs(c.z - (cubes[i - columns].z + side01)) < 1e-9;
                if (onIce) outCubes[i].Support = FluidIceSupport.IceBelow;
                else if (Math.Abs(c.z - c.floor) < 1e-9) outCubes[i].Support = FluidIceSupport.Floor;
                else outCubes[i].Support = FluidIceSupport.Floating;
                int inTier = Math.Min(columns, cubeCount - c.tier * columns);
                double r = vessel.HalfWidthAt(Math.Min(1, c.z + side01 / 2));
                double pitch = side01 * 1.05;
                double x = (c.column - (inTier - 1) / 2.0) * pitch;
                double lim = Math.Max(0, r - side01 / 2);
                outCubes[i].CentreX = Math.Max(-lim, Math.Min(lim, x));
            }
            sol.Cubes = outCubes;
            sol.SubmergedIceVolume = submerged;
            sol.IceAboveSurfaceVolume = above;
            sol.OccupiedBelowSurface = sol.LiquidVolume + submerged;
            sol.HeadspaceMl = vessel.CapacityMl - (sol.HasLiquid ? vessel.VolumeBelow(surf) : 0);
            sol.FreeVolumeMl = Math.Max(0, vessel.CapacityMl - sol.LiquidVolume - iceBelowRim);
            return sol;
        }

        // 한 열을 아래→위로 PAVA(인접 위반 병합): 각 묶음은 붙은 정육면체 더미로 평형/바닥 접촉을 푼다.
        static double SolveColumn(FluidVesselProfile vessel, List<FluidLayer> layers, double[] cum, Cube[] cubes, List<int> idx, double cubeMass)
        {
            var groups = new List<List<int>>();
            var groupZ = new List<double>();
            double maxMove = 0;
            var old = new double[idx.Count];
            for (int n = 0; n < idx.Count; n++) old[n] = cubes[idx[n]].z;
            foreach (var i in idx)
            {
                var g = new List<int> { i };
                double z = SolveGroup(vessel, layers, cum, cubes, g, cubeMass);
                while (groups.Count > 0)
                {
                    var prev = groups[groups.Count - 1];
                    double prevTop = groupZ[groupZ.Count - 1] + prev.Count * cubes[i].side;
                    if (z >= prevTop - 1e-12) break;
                    prev.AddRange(g); g = prev;
                    groups.RemoveAt(groups.Count - 1); groupZ.RemoveAt(groupZ.Count - 1);
                    z = SolveGroup(vessel, layers, cum, cubes, g, cubeMass);
                }
                groups.Add(g); groupZ.Add(z);
                Place(cubes, g, z);
            }
            for (int n = 0; n < idx.Count; n++) maxMove = Math.Max(maxMove, Math.Abs(cubes[idx[n]].z - old[n]));
            return maxMove;
        }

        static void Place(Cube[] cubes, List<int> g, double z)
        {
            for (int j = 0; j < g.Count; j++) cubes[g[j]].z = z + j * cubes[g[j]].side;
        }

        static double SolveGroup(FluidVesselProfile vessel, List<FluidLayer> layers, double[] cum, Cube[] cubes, List<int> g, double cubeMass)
        {
            double s = cubes[g[0]].side;
            double lo = double.MinValue;
            for (int j = 0; j < g.Count; j++) lo = Math.Max(lo, cubes[g[j]].floor - j * s);
            double mass = cubeMass * g.Count;
            double F(double z) { Place(cubes, g, z); return Buoyant(vessel, layers, cum, cubes, g) - mass; }
            double flo = F(lo);
            if (flo <= 0) { Place(cubes, g, lo); return lo; }
            // 근은 림 아래: 정육면체가 림 위(z ≥ 1)면 부력 0. Illinois(수정 가위치법) — 구간을 항상 유지하므로 이분법처럼 안전하고 더 빨리 수렴.
            double hi = Math.Max(lo, 1.0), fhi = F(hi);
            if (fhi > 0) { Place(cubes, g, hi); return hi; }
            int side = 0;
            double z = lo;
            for (int it = 0; it < BisectionIterations && hi - lo > 1e-13; it++)
            {
                z = (lo * fhi - hi * flo) / (fhi - flo);
                if (!(z > lo && z < hi)) z = 0.5 * (lo + hi);
                double fz = F(z);
                if (Math.Abs(fz) <= 1e-12 * mass) { lo = hi = z; break; }
                if (fz > 0) { lo = z; flo = fz; if (side == 1) fhi *= 0.5; side = 1; }
                else { hi = z; fhi = fz; if (side == -1) flo *= 0.5; side = -1; }
            }
            z = 0.5 * (lo + hi);
            Place(cubes, g, z);
            return z;
        }

        // 묶음이 받는 부력의 질량 등가 Σ_k ρ_k V_k(층 경계는 현재 모든 얼음 위치로 다시 계산).
        static double Buoyant(FluidVesselProfile vessel, List<FluidLayer> layers, double[] cum, Cube[] cubes, List<int> g)
        {
            var occ = Occupancy.Build(vessel, cubes);
            double b = 0, prevH = 0;
            for (int k = 0; k < layers.Count; k++)
            {
                double top = occ.HeightFor(cum[k]);
                foreach (var i in g) b += layers[k].Density * cubes[i].footprint * Overlap(cubes[i].z, cubes[i].z + cubes[i].side, prevH, top);
                prevH = top;
            }
            return b;
        }

        static double Overlap(double a0, double a1, double b0, double b1) => Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));

        // 정육면체 하나가 들어갈 가장 낮은 바닥 높이(대각선 ≤ 지름). 좁은 바닥/원뿔 잔에서는 벽에 걸린다.
        static double TierFloor(FluidVesselProfile vessel, double side01, double footprint, out bool fits)
        {
            const int Samples = 400, Probe = 12;
            double needRadius = side01 / Math.Sqrt(2);
            for (int k = 0; k <= Samples; k++)
            {
                double z = k / (double)Samples * Math.Max(0, 1 - side01);
                bool ok = true;
                for (int p = 0; p <= Probe && ok; p++)
                {
                    double a = vessel.AreaAt(Math.Min(1 - 1e-12, z + side01 * p / Probe));
                    ok = a >= footprint && vessel.RadiusFromArea(a) >= needRadius;
                }
                if (ok) { fits = true; return z; }
            }
            fits = false;
            return Math.Max(0, 1 - side01);
        }

        // 얼음 단면을 뺀 액체 가용 누적 부피 O(h) = C(h) − Σ 얼음 단면 × 겹친 높이. 잔 구간 경계와 얼음 위/아래 경계에서만 기울기가 바뀌는
        // 구간 선형 함수이므로 경계값 표를 만들고 이진 탐색 + 선형 보간으로 정확히 역산한다(일시적 겹침은 누적 최대로 단조화).
        sealed class Occupancy
        {
            double[] hs, vs; int n;

            public static Occupancy Build(FluidVesselProfile vessel, Cube[] cubes)
            {
                int segs = vessel.SegmentCount;
                var extra = new List<double>(cubes.Length * 2);
                foreach (var c in cubes)
                {
                    if (c.z > 0 && c.z < 1) extra.Add(c.z);
                    double t = c.z + c.side;
                    if (t > 0 && t < 1) extra.Add(t);
                }
                extra.Sort();
                var o = new Occupancy { hs = new double[segs + 1 + extra.Count], vs = new double[segs + 1 + extra.Count] };
                int e = 0;
                double last = -1, maxV = 0;
                for (int k = 0; k <= segs; k++)
                {
                    double hb = k < segs ? vessel.SegmentLow(k) : 1.0;
                    while (e < extra.Count && extra[e] < hb) o.Add(extra[e++], -1, vessel, cubes, ref last, ref maxV);
                    o.Add(hb, k, vessel, cubes, ref last, ref maxV);
                }
                return o;
            }

            void Add(double h, int vk, FluidVesselProfile vessel, Cube[] cubes, ref double last, ref double maxV)
            {
                if (h <= last) return;
                double v = vk >= 0 ? vessel.CumulativeAt(vk) : vessel.VolumeBelow(h);
                foreach (var c in cubes) v -= c.footprint * Overlap(c.z, c.z + c.side, 0, h);
                maxV = Math.Max(maxV, v);
                hs[n] = h; vs[n] = maxV; n++; last = h;
            }

            public double VolumeBelow(double h01)
            {
                if (h01 <= 0) return 0;
                if (h01 >= 1) return vs[n - 1];
                int lo = 0, hi = n - 1;
                while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (hs[mid] <= h01) lo = mid; else hi = mid; }
                return vs[lo] + (vs[hi] - vs[lo]) * (h01 - hs[lo]) / (hs[hi] - hs[lo]);
            }

            public double HeightFor(double v)
            {
                if (v <= 0) return 0;
                if (v >= vs[n - 1]) return 1;
                int lo = 0, hi = n - 1;
                while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (vs[mid] < v) lo = mid; else hi = mid; }
                // vs[lo] < v <= vs[hi]; 평평한(얼음으로 꽉 찬) 구간은 건너뛴다.
                return vs[hi] > vs[lo] ? hs[lo] + (hs[hi] - hs[lo]) * (v - vs[lo]) / (vs[hi] - vs[lo]) : hs[hi];
            }
        }

        /// <summary>
        /// 기울어진 직립 회전체 잔(얼음 없음). 액체는 세계 수평 평면 아래의 회전된 내부 영역이며 3D 부피(회전 대칭)를 보존한다.
        /// angleDeg 양수 = 림이 +x로 기울어 +x 쪽 림이 입구. 입구 높이를 넘는 부피는 SpillMl로 보고(호출자가 원천에서 빼고 받는 용기에 더함).
        /// </summary>
        public static FluidTiltSolution SolveTilt(FluidVesselProfile vessel, double[] cumulativeVolumesMl, double angleDeg)
        {
            if (vessel == null) throw new ArgumentNullException(nameof(vessel));
            if (cumulativeVolumesMl == null) throw new ArgumentNullException(nameof(cumulativeVolumesMl));
            FluidValidate.Finite(angleDeg, nameof(angleDeg));
            if (Math.Abs(angleDeg) > 179) throw new ArgumentOutOfRangeException(nameof(angleDeg));
            double prev = 0;
            foreach (var v in cumulativeVolumesMl)
            {
                FluidValidate.NonNegative(v, "cumulative volume");
                if (v < prev) throw new ArgumentException("cumulative volumes must be non-decreasing");
                prev = v;
            }
            double th = angleDeg * Math.PI / 180;
            double sn = Math.Sin(th), cs = Math.Cos(th);
            var tilt = new TiltGeometry(vessel, sn, cs);
            double rTop = vessel.SegmentRadius(vessel.SegmentCount - 1);
            double lipX = sn >= 0 ? rTop : -rTop;
            var sol = new FluidTiltSolution
            {
                Lip = new FluidPoint(lipX, 1),
                LipLevel = -lipX * sn + cs,
            };
            double lipLevel = Math.Min(sol.LipLevel, tilt.MaxLevel);
            double maxHold = tilt.Volume(lipLevel);
            double total = cumulativeVolumesMl.Length > 0 ? cumulativeVolumesMl[cumulativeVolumesMl.Length - 1] : 0;
            sol.SpillMl = Math.Max(0, total - maxHold);
            sol.LipReached = total > 0 && total >= maxHold * (1 - 1e-9) - 1e-9;
            sol.Levels = new double[cumulativeVolumesMl.Length];
            sol.RetainedCumulativeMl = new double[cumulativeVolumesMl.Length];
            sol.LayerPolygons = new FluidPoint[cumulativeVolumesMl.Length][];
            var section = tilt.SectionPolygon();
            double prevLevel = double.NegativeInfinity;
            for (int k = 0; k < cumulativeVolumesMl.Length; k++)
            {
                double v = Math.Min(cumulativeVolumesMl[k], maxHold);
                sol.RetainedCumulativeMl[k] = v;
                double level = v <= 0 ? tilt.MinLevel : (v >= maxHold ? lipLevel : tilt.LevelFor(v, lipLevel));
                sol.Levels[k] = level;
                var poly = ClipHalfPlane(section, sn, cs, level, true);
                if (!double.IsNegativeInfinity(prevLevel)) poly = ClipHalfPlane(poly, sn, cs, prevLevel, false);
                sol.LayerPolygons[k] = cumulativeVolumesMl[k] > (k > 0 ? cumulativeVolumesMl[k - 1] : 0) ? poly.ToArray() : Array.Empty<FluidPoint>();
                prevLevel = level;
            }
            if (total > 0 && sol.Levels.Length > 0)
            {
                double top = sol.Levels[sol.Levels.Length - 1];
                var below = ClipHalfPlane(section, sn, cs, top, true);
                double best0 = double.MaxValue, best1 = double.MinValue; FluidPoint p0 = default, p1 = default; int on = 0;
                foreach (var p in below)
                {
                    if (Math.Abs(-p.X * sn + p.Y * cs - top) > 1e-9) continue;
                    double t = p.X * cs + p.Y * sn; // 수면 방향 좌표
                    if (t < best0) { best0 = t; p0 = p; }
                    if (t > best1) { best1 = t; p1 = p; }
                    on++;
                }
                if (on >= 2) sol.SurfaceLine = new[] { p0, p1 };
            }
            return sol;
        }

        // 세계 높이 f(x,y) = −x·sinθ + y·cosθ. half-plane keepBelow: f ≤ c.
        static List<FluidPoint> ClipHalfPlane(List<FluidPoint> poly, double sn, double cs, double c, bool keepBelow)
        {
            var res = new List<FluidPoint>();
            int n = poly.Count;
            if (n == 0) return res;
            double Side(FluidPoint p) { double f = -p.X * sn + p.Y * cs - c; return keepBelow ? -f : f; }
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                double sa = Side(a), sb = Side(b);
                if (sa >= 0) res.Add(a);
                if ((sa >= 0) != (sb >= 0))
                {
                    double t = sa / (sa - sb);
                    res.Add(new FluidPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                }
            }
            return res;
        }

        sealed class TiltGeometry
        {
            readonly FluidVesselProfile v;
            readonly double sn, cs, abs, h3;
            public readonly double MinLevel, MaxLevel;
            static readonly double[] GaussX = { -0.7745966692414834, 0, 0.7745966692414834 };
            static readonly double[] GaussW = { 5.0 / 9, 8.0 / 9, 5.0 / 9 };
            const int SubTotal = 192;

            public TiltGeometry(FluidVesselProfile v, double sn, double cs)
            {
                this.v = v; this.sn = sn; this.cs = cs; abs = Math.Abs(sn); h3 = v.HeightCm * v.HeightCm * v.HeightCm;
                double mn = double.MaxValue, mx = double.MinValue;
                for (int k = 0; k < v.SegmentCount; k++)
                {
                    double r = v.SegmentRadius(k);
                    foreach (var y in new[] { v.SegmentLow(k), v.SegmentHigh(k) })
                    {
                        mn = Math.Min(mn, y * cs - r * abs);
                        mx = Math.Max(mx, y * cs + r * abs);
                    }
                }
                MinLevel = mn; MaxLevel = mx;
            }

            // 반지름 r 원판에서 세계 높이 ≤ c인 부분 면적(정규 단위²).
            double DiscArea(double r, double y, double c)
            {
                if (r <= 0) return 0;
                if (abs < 1e-12) return y * cs <= c ? Math.PI * r * r : 0;
                double d = (y * cs - c) / abs; // 필요한 '입구 방향' 좌표 하한
                if (d <= -r) return Math.PI * r * r;
                if (d >= r) return 0;
                return r * r * Math.Acos(d / r) - d * Math.Sqrt(r * r - d * d);
            }

            public double Volume(double c)
            {
                if (abs < 1e-12 && cs > 0) return v.VolumeBelow(c / cs);
                double sum = 0;
                int Sub = Math.Max(1, (SubTotal + v.SegmentCount - 1) / v.SegmentCount);
                for (int k = 0; k < v.SegmentCount; k++)
                {
                    double r = v.SegmentRadius(k), y0 = v.SegmentLow(k), y1 = v.SegmentHigh(k);
                    double step = (y1 - y0) / Sub;
                    for (int s = 0; s < Sub; s++)
                    {
                        double a = y0 + s * step;
                        for (int q = 0; q < 3; q++)
                        {
                            double y = a + step * (GaussX[q] + 1) / 2;
                            sum += GaussW[q] * step / 2 * DiscArea(r, y, c);
                        }
                    }
                }
                return sum * h3;
            }

            public double LevelFor(double volume, double maxLevel)
            {
                double lo = MinLevel, hi = maxLevel;
                for (int i = 0; i < 56; i++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (Volume(mid) < volume) lo = mid; else hi = mid;
                }
                return 0.5 * (lo + hi);
            }

            public List<FluidPoint> SectionPolygon()
            {
                var pts = new List<FluidPoint>();
                int n = v.SegmentCount;
                for (int k = 0; k < n; k++)
                {
                    double r = v.SegmentRadius(k);
                    pts.Add(new FluidPoint(r, v.SegmentLow(k)));
                    pts.Add(new FluidPoint(r, v.SegmentHigh(k)));
                }
                for (int k = n - 1; k >= 0; k--)
                {
                    double r = v.SegmentRadius(k);
                    pts.Add(new FluidPoint(-r, v.SegmentHigh(k)));
                    pts.Add(new FluidPoint(-r, v.SegmentLow(k)));
                }
                return pts;
            }
        }
    }

    static class FluidValidate
    {
        public static void Finite(double x, string name)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentException($"{name} must be finite", name);
        }

        public static void NonNegative(double x, string name)
        {
            Finite(x, name);
            if (x < 0) throw new ArgumentOutOfRangeException(name, $"{name} must be >= 0");
        }

        public static void Positive(double x, string name)
        {
            Finite(x, name);
            if (x <= 0) throw new ArgumentOutOfRangeException(name, $"{name} must be > 0");
        }

        public static void Unit(double x, string name)
        {
            Finite(x, name);
            if (x < 0 || x > 1) throw new ArgumentOutOfRangeException(name, $"{name} must be in [0,1]");
        }

        public static void Part(FluidPart p)
        {
            if (string.IsNullOrEmpty(p.id)) throw new ArgumentException("part id required");
            NonNegative(p.volumeMl, "volumeMl");
            Positive(p.density, "density");
            Unit(p.viscosity, "viscosity");
        }
    }
}
