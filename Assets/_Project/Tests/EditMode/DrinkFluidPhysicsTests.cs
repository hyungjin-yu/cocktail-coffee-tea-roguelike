using System;
using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    // 71 물리 축약 모델 시험. 기준값은 ArtProduction/beverage-v1.1/physics-reference-cases.json의 해석해(합성 액체)를 그대로 옮겼다.
    public class DrinkFluidPhysicsTests
    {
        const double Cube = 10.0, Ice = 0.917, Cap = 200.0;
        // 기준 사례의 "일정 단면 직립 잔": 200 mL, 내부 높이 10 cm(단면 20 cm², 10 mL 얼음 한 변 2.154 cm가 충분히 들어감).
        static FluidVesselProfile RefGlass() => FluidVesselProfile.Cylinder(Cap, 10.0);
        static FluidLayer[] Uniform(double v, double rho) => new[] { FluidLayer.From(new[] { new FluidPart("liq", v, rho) }) };

        // ---- 해석 기준 사례 ----

        [TestCase(0, 1.0, 0.0, 100.0, 0.5)]
        [TestCase(1, 1.0, 9.17, 109.17, 0.54585)]
        [TestCase(2, 1.0, 18.34, 118.34, 0.5917)]
        [TestCase(1, 1.2, 7.641666666666667, 107.64166666666667, 0.5382083333333333)]
        [TestCase(1, 0.8, 10.0, 110.0, 0.55)]
        public void ReferenceUniformCases(int ice, double rho, double displaced, double occupied, double height)
        {
            var s = DrinkFluidPhysics.SolveIce(RefGlass(), Uniform(100, rho), ice, Cube, Ice);
            Assert.That(s.Converged, Is.True);
            Assert.That(s.SubmergedIceVolume, Is.EqualTo(displaced).Within(1e-6));
            Assert.That(s.OccupiedBelowSurface, Is.EqualTo(occupied).Within(1e-6));
            Assert.That(s.Surface01, Is.EqualTo(height).Within(1e-8));
            Assert.That(s.LiquidVolume, Is.EqualTo(100.0).Within(1e-9));
            Assert.That(s.HeadspaceMl, Is.EqualTo(Cap - occupied).Within(1e-6));
            foreach (var c in s.Cubes)
            {
                if (rho * Cube > Ice * Cube)
                {
                    Assert.That(c.Support, Is.EqualTo(FluidIceSupport.Floating));
                    Assert.That(c.SubmergedMl / Cube, Is.EqualTo(Ice / rho).Within(1e-7));
                    Assert.That(c.Top01, Is.GreaterThan(s.Surface01), "floating cube pokes above surface");
                }
                else
                {
                    Assert.That(c.Support, Is.EqualTo(FluidIceSupport.Floor), "denser-than-liquid ice must sink, not float");
                    Assert.That(c.Top01, Is.LessThan(s.Surface01));
                }
            }
        }

        [Test]
        public void ReferenceLayerInterfaceBuoyancy()
        {
            var layers = new[]
            {
                FluidLayer.From(new[] { new FluidPart("dense", 50, 1.2) }),
                FluidLayer.From(new[] { new FluidPart("light", 50, 0.8) }),
            };
            var s = DrinkFluidPhysics.SolveIce(RefGlass(), layers, 1, Cube, Ice);
            var c = s.Cubes[0];
            Assert.That(c.Support, Is.EqualTo(FluidIceSupport.Floating));
            Assert.That(c.VolumeInLayer[0], Is.EqualTo(2.925).Within(1e-6));
            Assert.That(c.VolumeInLayer[1], Is.EqualTo(7.075).Within(1e-6));
            Assert.That(1.2 * c.VolumeInLayer[0] + 0.8 * c.VolumeInLayer[1], Is.EqualTo(9.17).Within(1e-6));
            Assert.That(s.LiquidVolume, Is.EqualTo(100).Within(1e-9));
            Assert.That(s.OccupiedBelowSurface, Is.EqualTo(110).Within(1e-6));
            Assert.That(s.Surface01, Is.EqualTo(0.55).Within(1e-8));
            Assert.That(c.Bottom01, Is.LessThan(s.Layers[0].top01));
            Assert.That(c.Top01, Is.GreaterThan(s.Layers[1].bottom01));
            Assert.That(s.Layers[0].Volume + s.Layers[1].Volume, Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void ReferenceEmptyVesselCubeOnFloor()
        {
            var s = DrinkFluidPhysics.SolveIce(RefGlass(), Array.Empty<FluidLayer>(), 1, Cube, Ice);
            Assert.That(s.HasLiquid, Is.False);
            Assert.That(s.Surface01, Is.EqualTo(0));
            Assert.That(s.SubmergedIceVolume, Is.EqualTo(0));
            Assert.That(s.LiquidVolume, Is.EqualTo(0));
            Assert.That(s.Cubes[0].Support, Is.EqualTo(FluidIceSupport.Floor));
            Assert.That(s.Cubes[0].Bottom01, Is.EqualTo(0).Within(1e-12));
            Assert.That(s.FreeVolumeMl, Is.EqualTo(Cap - Cube).Within(1e-9));
        }

        [Test]
        public void ReferencePourConservation()
        {
            var src = new FluidColumn(); src.Pour(new FluidPart("gin", 45.0, 0.95), 1);
            var dst = new FluidColumn(); dst.Pour(new FluidPart("lemon", 30.0, 1.03), 1);
            var moved = DrinkFluidPhysics.Transfer(src, dst, 12.3, 0.5);
            Assert.That(moved.Sum(p => p.volumeMl), Is.EqualTo(12.3).Within(1e-9));
            Assert.That(src.TotalVolume, Is.EqualTo(32.7).Within(1e-9));
            Assert.That(dst.TotalVolume, Is.EqualTo(42.3).Within(1e-9));
            Assert.That(src.TotalVolume + dst.TotalVolume, Is.EqualTo(75.0).Within(1e-9));
            Assert.That(src.TotalMass + dst.TotalMass, Is.EqualTo(45 * 0.95 + 30 * 1.03).Within(1e-9));
        }

        // ---- 잔 형태·얼음 ----

        static FluidVesselProfile Tapered()
        {
            // 아래 좁고 위 넓은 하이볼/텀블러형 마스크 폭(px), 120 행.
            var w = Enumerable.Range(0, 120).Select(i => 40.0 + 30.0 * i / 119).ToArray();
            return FluidVesselProfile.FromRowWidths(w, 150);
        }

        [Test]
        public void ProfileIsMonotoneAndInvertible()
        {
            var v = Tapered();
            Assert.That(v.CapacityMl, Is.EqualTo(150).Within(1e-9));
            Assert.That(v.VolumeBelow(1), Is.EqualTo(150).Within(1e-9));
            double prev = -1;
            for (int i = 0; i <= 200; i++)
            {
                double h = i / 200.0, c = v.VolumeBelow(h);
                Assert.That(c, Is.GreaterThanOrEqualTo(prev));
                Assert.That(v.HeightForVolume(c), Is.EqualTo(h).Within(1e-9));
                prev = c;
            }
            // 위가 넓으면 같은 부피 비율이라도 높이 비율은 다르다(용량/높이 비례 금지).
            Assert.That(v.HeightForVolume(75), Is.GreaterThan(0.5));
            // 회전 대칭 척도: 원통 마스크는 π r² H = C.
            var cyl = FluidVesselProfile.FromRowWidths(Enumerable.Repeat(60.0, 100).ToArray(), 200);
            double r = cyl.HalfWidthAt(0.5) * cyl.HeightCm;
            Assert.That(Math.PI * r * r * cyl.HeightCm, Is.EqualTo(200).Within(1e-9));
            Assert.That(cyl.HalfWidthAt(0.5) * 100, Is.EqualTo(30).Within(1e-9), "half width in px matches mask");
        }

        [Test]
        public void IceRaisesSurfaceAndConservesLiquid()
        {
            var v = Tapered();
            var layers = new[]
            {
                FluidLayer.From(new[] { new FluidPart("grenadine", 15, 1.30, 0.7) }),
                FluidLayer.From(new[] { new FluidPart("gin", 45, 0.95, 0.1), new FluidPart("water", 30, 1.0) }),
            };
            double prevSurface = -1;
            for (int n = 0; n <= 4; n++)
            {
                var s = DrinkFluidPhysics.SolveIce(v, layers, n, 8, Ice);
                Assert.That(s.Converged, Is.True, $"ice {n}");
                Assert.That(s.LiquidVolume, Is.EqualTo(90).Within(1e-9));
                Assert.That(s.Layers.Sum(l => l.Volume), Is.EqualTo(90).Within(1e-9));
                Assert.That(s.Layers.Sum(l => l.Mass), Is.EqualTo(15 * 1.3 + 45 * 0.95 + 30).Within(1e-9));
                Assert.That(s.OccupiedBelowSurface, Is.EqualTo(v.VolumeBelow(s.Surface01)).Within(1e-6));
                Assert.That(s.HeadspaceMl, Is.EqualTo(150 - s.OccupiedBelowSurface).Within(1e-6));
                Assert.That(s.Surface01, Is.GreaterThan(prevSurface), "adding ice raises the surface");
                Assert.That(s.SubmergedIceVolume, Is.LessThanOrEqualTo(n * 8 + 1e-9));
                Assert.That(s.SubmergedIceVolume + s.IceAboveSurfaceVolume, Is.EqualTo(n * 8).Within(1e-9));
                for (int i = 0; i < s.Cubes.Length; i++)
                    for (int j = i + 1; j < s.Cubes.Length; j++)
                    {
                        var a = s.Cubes[i]; var b = s.Cubes[j];
                        bool sameSpot = Math.Abs(a.CentreX - b.CentreX) < a.Side01 * 0.99;
                        if (sameSpot) Assert.That(Math.Max(a.Bottom01, b.Bottom01), Is.GreaterThanOrEqualTo(Math.Min(a.Top01, b.Top01) - 1e-9), "stacked cubes do not interpenetrate");
                    }
                prevSurface = s.Surface01;
            }
        }

        [Test]
        public void LowLiquidCubeRestsOnFloorPartiallySubmerged()
        {
            var s = DrinkFluidPhysics.SolveIce(RefGlass(), Uniform(5, 1.0), 1, Cube, Ice);
            var c = s.Cubes[0];
            Assert.That(c.Support, Is.EqualTo(FluidIceSupport.Floor));
            Assert.That(s.Surface01, Is.LessThan(c.Top01));
            Assert.That(c.SubmergedMl, Is.GreaterThan(0).And.LessThan(Ice * Cube));
            // 바닥 접촉: 액체 5 mL가 (단면 − 얼음) 위로 오른 만큼만 잠긴다.
            double area = 20.0, foot = Math.Pow(Cube, 2.0 / 3.0);
            Assert.That(s.Surface01 * 10, Is.EqualTo(5 / (area - foot)).Within(1e-9));
        }

        [Test]
        public void OverflowIsReportedNotHidden()
        {
            var s = DrinkFluidPhysics.SolveIce(RefGlass(), Uniform(195, 1.0), 2, Cube, Ice);
            Assert.That(s.OverflowMl, Is.GreaterThan(0));
            Assert.That(s.LiquidVolume + s.OverflowMl, Is.EqualTo(195).Within(1e-9));
            Assert.That(s.Surface01, Is.EqualTo(1).Within(1e-9));
            Assert.That(s.HeadspaceMl, Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void IceBuoyancyUsesLocalLayerDensityNotFinalMix()
        {
            var v = FluidVesselProfile.Cylinder(200, 10);
            var layered = new[]
            {
                FluidLayer.From(new[] { new FluidPart("grenadine", 40, 1.30) }),
                FluidLayer.From(new[] { new FluidPart("gin", 100, 0.95) }),
            };
            var mixed = new[] { FluidLayer.From(layered.SelectMany(l => l.parts)) };
            var a = DrinkFluidPhysics.SolveIce(v, layered, 1, Cube, Ice);
            var b = DrinkFluidPhysics.SolveIce(v, mixed, 1, Cube, Ice);
            Assert.That(a.Cubes[0].SubmergedMl, Is.EqualTo(9.17 / 0.95).Within(1e-6), "floats in local gin layer");
            Assert.That(b.Cubes[0].SubmergedMl, Is.EqualTo(9.17 / mixed[0].Density).Within(1e-6));
            Assert.That(a.Cubes[0].SubmergedMl, Is.GreaterThan(b.Cubes[0].SubmergedMl + 0.5));
        }

        [Test]
        public void LightSpiritSinksIceDenseSyrupFloatsHigher()
        {
            var v = FluidVesselProfile.Cylinder(200, 10);
            var light = DrinkFluidPhysics.SolveIce(v, Uniform(120, 0.90), 1, Cube, Ice); // 합성 고도수 증류주 근사
            var syrup = DrinkFluidPhysics.SolveIce(v, Uniform(120, 1.30), 1, Cube, Ice);
            Assert.That(light.Cubes[0].Support, Is.EqualTo(FluidIceSupport.Floor));
            Assert.That(syrup.Cubes[0].Support, Is.EqualTo(FluidIceSupport.Floating));
            Assert.That(syrup.Cubes[0].SubmergedMl / Cube, Is.EqualTo(0.917 / 1.30).Within(1e-7));
        }

        [Test]
        public void FourCubesInNarrowGlassStackAndStayConsistent()
        {
            var w = Enumerable.Range(0, 100).Select(i => 34.0 + 6.0 * i / 99).ToArray(); // 좁은 잔: 한 단에 1열
            var v = FluidVesselProfile.FromRowWidths(w, 150);
            var s = DrinkFluidPhysics.SolveIce(v, Uniform(30, 1.0), 4, 12, Ice);
            Assert.That(s.Converged, Is.True);
            var sorted = s.Cubes.OrderBy(c => c.Bottom01).ToArray();
            for (int i = 1; i < sorted.Length; i++)
                if (Math.Abs(sorted[i].CentreX - sorted[i - 1].CentreX) < sorted[i].Side01 * 0.99)
                    Assert.That(sorted[i].Bottom01, Is.GreaterThanOrEqualTo(sorted[i - 1].Top01 - 1e-9));
            Assert.That(s.LiquidVolume, Is.EqualTo(30).Within(1e-9));
            Assert.That(s.SubmergedIceVolume, Is.LessThanOrEqualTo(4 * 12 * Ice / 1.0 + 1e-6), "submerged never exceeds what buoyancy needs plus floor-supported");
            Assert.That(s.Cubes.All(c => c.Fits), Is.True);
        }

        [Test]
        public void ConicalGlassCubeRestsOnWallsNotAtApex()
        {
            var w = Enumerable.Range(0, 100).Select(i => 2.0 + 98.0 * i / 99).ToArray(); // V형 칵테일 잔
            var v = FluidVesselProfile.FromRowWidths(w, 150);
            var s = DrinkFluidPhysics.SolveIce(v, Array.Empty<FluidLayer>(), 1, 10, Ice);
            Assert.That(s.Cubes[0].Support, Is.EqualTo(FluidIceSupport.Floor));
            Assert.That(s.Cubes[0].Bottom01, Is.GreaterThan(0.05), "cube wedged where cone is wide enough");
            Assert.That(v.HalfWidthAt(s.Cubes[0].Bottom01), Is.GreaterThanOrEqualTo(s.Cubes[0].Side01 / Math.Sqrt(2) - 1e-9));
        }

        // ---- 기울기: 중력 수평 액면 ----

        [TestCase(0.0)]
        [TestCase(30.0)]
        [TestCase(60.0)]
        [TestCase(-45.0)]
        public void TiltedCylinderMatchesAnalyticAxisHeight(double deg)
        {
            var v = FluidVesselProfile.Cylinder(200, 10);
            var t = DrinkFluidPhysics.SolveTilt(v, new[] { 50.0, 100.0 }, deg);
            double cs = Math.Cos(deg * Math.PI / 180);
            // 평면이 양쪽 벽을 지나면 원통 아래 부피 = 축 위치 높이 × 단면.
            Assert.That(t.Levels[1] / cs, Is.EqualTo(0.5).Within(1e-6));
            // 25% 경계는 평면이 바닥을 지나지 않을 때만 같은 해석해가 성립한다(60°에서는 바닥과 교차).
            if (0.25 - v.HalfWidthAt(0.5) * Math.Tan(Math.Abs(deg) * Math.PI / 180) >= 0)
                Assert.That(t.Levels[0] / cs, Is.EqualTo(0.25).Within(1e-6));
            Assert.That(t.SpillMl, Is.EqualTo(0));
            AssertHorizontal(t, deg);
            double r = v.HalfWidthAt(0.5);
            foreach (var poly in t.LayerPolygons)
                foreach (var p in poly)
                {
                    Assert.That(Math.Abs(p.X), Is.LessThanOrEqualTo(r + 1e-9), "liquid stays inside rotated vessel");
                    Assert.That(p.Y, Is.InRange(-1e-9, 1 + 1e-9));
                }
        }

        static void AssertHorizontal(FluidTiltSolution t, double deg)
        {
            Assert.That(t.SurfaceLine.Length, Is.EqualTo(2));
            double sn = Math.Sin(deg * Math.PI / 180), cs = Math.Cos(deg * Math.PI / 180);
            // 국소→세계 회전 뒤 두 끝점의 세계 높이가 같다.
            double y0 = -t.SurfaceLine[0].X * sn + t.SurfaceLine[0].Y * cs;
            double y1 = -t.SurfaceLine[1].X * sn + t.SurfaceLine[1].Y * cs;
            Assert.That(y0, Is.EqualTo(y1).Within(1e-9));
        }

        [Test]
        public void TiltSpillsAtLipAnalytic()
        {
            var v = FluidVesselProfile.Cylinder(200, 10);
            double r = v.HalfWidthAt(0.5);
            double th = 60 * Math.PI / 180;
            double hold = 200 * (1 - r * Math.Tan(th)); // 축 높이 = 1 − r tanθ 에서 입구 도달
            var t = DrinkFluidPhysics.SolveTilt(v, new[] { 150.0 }, 60);
            Assert.That(t.LipReached, Is.True);
            Assert.That(t.SpillMl, Is.EqualTo(150 - hold).Within(1e-4));
            Assert.That(t.RetainedCumulativeMl[0] + t.SpillMl, Is.EqualTo(150).Within(1e-9));
            var upright = DrinkFluidPhysics.SolveTilt(v, new[] { 150.0 }, 0);
            Assert.That(upright.LipReached, Is.False, "an upright vessel below the rim does not pour");
            Assert.That(upright.SpillMl, Is.EqualTo(0));
        }

        [TestCase(0.0)]
        [TestCase(30.0)]
        [TestCase(60.0)]
        public void TiltedTaperedVolumeMatchesIndependentGridIntegration(double deg)
        {
            var v = Tapered();
            var t = DrinkFluidPhysics.SolveTilt(v, new[] { 60.0 }, deg);
            Assert.That(t.SpillMl, Is.EqualTo(0));
            double sn = Math.Sin(deg * Math.PI / 180), cs = Math.Cos(deg * Math.PI / 180), c = t.Levels[0];
            // 독립 확인: 3D 격자 점 세기(축대칭 반경 = HalfWidthAt).
            const int N = 90;
            double rMax = v.HalfWidthAt(0.999), cell = 2 * rMax / N, dy = 1.0 / N;
            long inside = 0;
            for (int iy = 0; iy < N; iy++)
            {
                double y = (iy + 0.5) * dy, rr = v.HalfWidthAt(y);
                for (int ix = 0; ix < N; ix++)
                {
                    double x = -rMax + (ix + 0.5) * cell;
                    for (int iz = 0; iz < N; iz++)
                    {
                        double z = -rMax + (iz + 0.5) * cell;
                        if (x * x + z * z <= rr * rr && -x * sn + y * cs <= c) inside++;
                    }
                }
            }
            double vol = inside * cell * cell * dy * Math.Pow(v.HeightCm, 3);
            Assert.That(vol, Is.EqualTo(60).Within(60 * 0.02));
            if (deg == 0) Assert.That(c, Is.EqualTo(v.HeightForVolume(60)).Within(1e-9));
            else AssertHorizontal(t, deg);
        }

        // ---- 혼합 ----

        static double Frac(FluidColumn col, int cell, string id)
        {
            var ps = col.CellParts(cell);
            double tot = ps.Sum(p => p.volumeMl);
            return tot > 0 ? ps.Where(p => p.id == id).Sum(p => p.volumeMl) / tot : 0;
        }

        static double CentreOfVolume(FluidColumn col, string id)
        {
            double s = 0, v = 0;
            for (int i = 0; i < col.CellCount; i++) { double f = Frac(col, i, id); s += f * (i + 0.5); v += f; }
            return s / v / col.CellCount;
        }

        [Test]
        public void DenseOnTopSinksOverTimeWithBoundaryMixing()
        {
            var col = new FluidColumn();
            col.Pour(new FluidPart("gin", 60, 0.95, 0.1), 1);
            col.Pour(new FluidPart("grenadine", 20, 1.30, 0.0), 1); // 가만히 위에 부어도 무거우면 불안정
            double m0 = col.TotalMass;
            Assert.That(col.CellDensity(col.CellCount - 1), Is.GreaterThan(col.CellDensity(0)), "initially inverted");
            double c0 = CentreOfVolume(col, "grenadine");
            col.Step(0.3);
            double c1 = CentreOfVolume(col, "grenadine");
            Assert.That(c1, Is.LessThan(c0), "starts sinking");
            Assert.That(c1, Is.GreaterThan(0.2), "not teleported to the bottom in 0.3 s");
            col.Step(8);
            double c2 = CentreOfVolume(col, "grenadine");
            Assert.That(c2, Is.LessThan(0.5));
            Assert.That(col.CellDensity(0), Is.GreaterThan(col.CellDensity(col.CellCount - 1)), "stable after sinking");
            int mixedCells = Enumerable.Range(0, col.CellCount).Count(i => Frac(col, i, "grenadine") is > 0.05 and < 0.95);
            Assert.That(mixedCells, Is.GreaterThan(4), "sinking entrains a mixed boundary, not a perfect band");
            Assert.That(col.ComponentVolume("grenadine"), Is.EqualTo(20).Within(1e-9));
            Assert.That(col.ComponentVolume("gin"), Is.EqualTo(60).Within(1e-9));
            Assert.That(col.TotalMass, Is.EqualTo(m0).Within(1e-9));
        }

        [Test]
        public void GentleLightOnDenseMakesTemporaryLayerThatBlurs()
        {
            var col = new FluidColumn();
            col.Pour(new FluidPart("grenadine", 20, 1.30, 0.7), 1);
            col.Pour(new FluidPart("gin", 40, 0.95, 0.1), 1);
            col.Step(5);
            Assert.That(Frac(col, col.CellCount - 1, "gin"), Is.GreaterThan(0.95));
            Assert.That(Frac(col, 0, "grenadine"), Is.GreaterThan(0.95));
            Assert.That(col.GetLayers().Length, Is.GreaterThanOrEqualTo(2));
            int sharp = Enumerable.Range(0, col.CellCount).Count(i => Frac(col, i, "gin") is > 0.05 and < 0.95);
            col.Step(600);
            int blurred = Enumerable.Range(0, col.CellCount).Count(i => Frac(col, i, "gin") is > 0.05 and < 0.95);
            Assert.That(blurred, Is.GreaterThan(sharp), "miscible boundary diffuses over time (not permanent)");
            Assert.That(col.ComponentVolume("gin"), Is.EqualTo(40).Within(1e-9));
        }

        [Test]
        public void FastPourMixesMoreThanGentlePour()
        {
            double MixedCount(double gentle)
            {
                var c = new FluidColumn();
                c.Pour(new FluidPart("grenadine", 20, 1.30), 1);
                c.Pour(new FluidPart("gin", 40, 0.95), gentle);
                return Enumerable.Range(0, c.CellCount).Count(i => Frac(c, i, "gin") is > 0.05 and < 0.95);
            }
            Assert.That(MixedCount(0.0), Is.GreaterThan(MixedCount(1.0)));
        }

        [Test]
        public void ShakeHomogenizesAndDoesNotResort()
        {
            var col = new FluidColumn();
            col.Pour(new FluidPart("grenadine", 15, 1.30, 0.7), 1);
            col.Pour(new FluidPart("blue_curacao", 15, 1.08, 0.35), 1);
            col.Pour(new FluidPart("gin", 45, 0.95, 0.1), 1);
            double m0 = col.TotalMass;
            col.Mix(1.0, 6);
            double expect = 15.0 / 75;
            for (int i = 0; i < col.CellCount; i++) Assert.That(Frac(col, i, "grenadine"), Is.EqualTo(expect).Within(0.01));
            col.Step(120);
            for (int i = 0; i < col.CellCount; i++) Assert.That(Frac(col, i, "grenadine"), Is.EqualTo(expect).Within(0.01), "no auto re-separation");
            Assert.That(col.GetLayers().Length, Is.EqualTo(1));
            Assert.That(col.TotalMass, Is.EqualTo(m0).Within(1e-9));
            Assert.That(DrinkFluidPhysics.MixDensity(col.GetLayers()[0].parts), Is.EqualTo(m0 / 75).Within(1e-12));
        }

        [Test]
        public void ViscositySlowsSinking()
        {
            double CentreAfter(double visc)
            {
                var c = new FluidColumn();
                c.Pour(new FluidPart("water", 60, 1.0), 1);
                c.Pour(new FluidPart("syrup", 20, 1.40, visc), 1);
                c.Step(0.6);
                return CentreOfVolume(c, "syrup");
            }
            Assert.That(CentreAfter(1.0), Is.GreaterThan(CentreAfter(0.0) + 0.05));
        }

        [Test]
        public void SinkingEndsStablyStratifiedAndViscousParcelEntrainsLess()
        {
            FluidColumn Run(double visc)
            {
                var c = new FluidColumn();
                c.Pour(new FluidPart("gin", 60, 0.95, 0.1), 1);
                c.Pour(new FluidPart("syrup", 15, 1.40, visc), 1);
                c.Step(40);
                return c;
            }
            var thin = Run(0.0); var thick = Run(1.0);
            foreach (var col in new[] { thin, thick })
            {
                var layers = col.GetLayers();
                for (int k = 1; k < layers.Length; k++)
                    Assert.That(layers[k].Density, Is.LessThanOrEqualTo(layers[k - 1].Density + 1e-3), "no inverted layer left after settling");
                Assert.That(col.ComponentVolume("syrup"), Is.EqualTo(15).Within(1e-9));
            }
            Assert.That(thick.CellDensity(0), Is.GreaterThan(thin.CellDensity(0)), "viscous blob arrives less diluted");
            Assert.That(thin.CellDensity(0), Is.LessThan(1.40 - 0.02), "thin syrup entrains gin on the way down");
        }

        [Test]
        public void ImmiscibleSeparatesEvenAfterStirring()
        {
            var col = new FluidColumn();
            col.Pour(new FluidPart("oil", 20, 0.90, 0, 1), 1); // 합성 비혼화 기름(현재 재료에는 없음)
            col.Pour(new FluidPart("water", 40, 1.0, 0, 0), 1);
            col.Mix(1, 3);
            col.Step(60);
            Assert.That(Frac(col, col.CellCount - 1, "oil"), Is.GreaterThan(0.9));
            Assert.That(Frac(col, 0, "water"), Is.GreaterThan(0.9));
            Assert.That(col.ComponentVolume("oil"), Is.EqualTo(20).Within(1e-9));
        }

        [Test]
        public void IncrementalStreamingPourConservesAndFeedsIceSolver()
        {
            var col = new FluidColumn();
            for (int f = 0; f < 60; f++) { col.Pour(new FluidPart("gin", 0.75, 0.95, 0.1), 0.6); col.Step(1 / 60.0); }
            for (int f = 0; f < 30; f++) { col.Pour(new FluidPart("grenadine", 0.5, 1.30, 0.7), 0.6); col.Step(1 / 60.0); }
            Assert.That(col.TotalVolume, Is.EqualTo(60).Within(1e-9));
            var s = DrinkFluidPhysics.SolveIce(Tapered(), col.GetLayers(), 2, 8, Ice);
            Assert.That(s.LiquidVolume, Is.EqualTo(60).Within(1e-9));
            Assert.That(s.Layers.Sum(l => l.parts.Where(p => p.id == "grenadine").Sum(p => p.volumeMl)), Is.EqualTo(15).Within(1e-9));
            var bottomUp = col.Withdraw(10, fromTop: false);
            Assert.That(bottomUp.Sum(p => p.volumeMl), Is.EqualTo(10).Within(1e-9));
            Assert.That(col.TotalVolume, Is.EqualTo(50).Within(1e-9));
        }

        // ---- 입력 검증 ----

        [Test]
        public void RejectsNonFiniteAndInvalidInputs()
        {
            var col = new FluidColumn();
            Assert.Throws<ArgumentException>(() => col.Pour(new FluidPart("x", double.NaN, 1), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => col.Pour(new FluidPart("x", 1, -1), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => col.Pour(new FluidPart("x", 1, 1), 1.5));
            Assert.Throws<ArgumentException>(() => col.Step(double.PositiveInfinity));
            col.Pour(new FluidPart("x", 5, 1), 1);
            Assert.Throws<ArgumentException>(() => col.Pour(new FluidPart("x", 1, 1.2), 1), "same id with other density");
            Assert.Throws<ArgumentOutOfRangeException>(() => col.Withdraw(6));
            Assert.That(col.TotalVolume, Is.EqualTo(5).Within(1e-12));
            Assert.Throws<ArgumentException>(() => FluidVesselProfile.FromCapacityTable(new[] { 0.0, 0.5, 1.0 }, new[] { 0.0, 50, 40 }, 10));
            Assert.Throws<ArgumentException>(() => FluidVesselProfile.FromRowWidths(new[] { 0.0, 0.0 }, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrinkFluidPhysics.SolveIce(RefGlass(), null, -1, 10));
            Assert.Throws<ArgumentException>(() => DrinkFluidPhysics.SolveTilt(RefGlass(), new[] { 10.0 }, double.NaN));
            Assert.Throws<ArgumentException>(() => DrinkFluidPhysics.SolveTilt(RefGlass(), new[] { 10.0, 5.0 }, 10));
        }
    }
}
