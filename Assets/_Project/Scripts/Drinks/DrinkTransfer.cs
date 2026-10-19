using System;
using System.Collections.Generic;

namespace CCT
{
    /// <summary>
    /// 73 단계1 제조 장부의 그릇 하나: 액체 기둥(FluidColumn), 건조 찻잎(g, 종류별), 우러난 추출 고형분(g)과 우림 정도.
    /// 장부는 화면 표시용 가상 배합 예약분이다 — 창고 재고는 제조 확정 때 한 번만 차감하고 여기서는 건드리지 않는다.
    /// </summary>
    public sealed class DrinkVesselState
    {
        public readonly string Name;
        public FluidColumn Liquid = new FluidColumn();
        public readonly Dictionary<string, double> LeavesG = new Dictionary<string, double>();
        /// <summary>찻잎에서 녹아 나온 고형분(g). 액체와 함께 이동하고 거른 잎에는 남지 않는다.</summary>
        public double DissolvedG;
        /// <summary>물 성분의 우림 정도 0~1(1 = 기준 시간 동안 다 우린 차). 색 표시와 추출 장부에 쓴다.</summary>
        public double TeaStrength;

        public DrinkVesselState(string name) { Name = name; }

        public double LiquidMl => Liquid.TotalVolume;
        public double LeafG { get { double g = 0; foreach (var v in LeavesG.Values) g += v; return g; } }
        public double TotalMassG => Liquid.TotalMass + LeafG + DissolvedG;
        public double WaterMl => Liquid.ComponentVolume(DrinkTransferLedger.WaterId);
        public bool IsEmpty => Liquid.IsEmpty && LeafG <= 1e-9;

        public void Clear()
        {
            Liquid = new FluidColumn();
            LeavesG.Clear();
            DissolvedG = 0;
            TeaStrength = 0;
        }

        public double Leaf(string id) => id != null && LeavesG.TryGetValue(id, out var g) ? g : 0;

        public void AddLeaf(string id, double g)
        {
            if (g <= 0) return;
            LeavesG[id] = Leaf(id) + g;
        }
    }

    /// <summary>공중(줄기·떨어지는 잎)에 있는 한 덩어리. 도착 시간이 지나면 받는 그릇에 들어간다.</summary>
    public sealed class DrinkPacket
    {
        public string To;
        public FluidPart[] Parts = Array.Empty<FluidPart>();
        public string LeafId;
        public double LeafG, DissolvedG, TeaStrength, Gentleness;
        public double RemainingSeconds;
        public double VolumeMl { get { double v = 0; foreach (var p in Parts) v += p.volumeMl; return v; } }
        public double MassG { get { double m = LeafG + DissolvedG; foreach (var p in Parts) m += p.MassG; return m; } }
    }

    /// <summary>
    /// 73 단계1: 병/통 → 지거/다하 → 셰이커·믹싱 글라스·다관 → 완성 잔 전체에서 같은 성분의 양·질량을 보존하는 장부.
    /// 이동은 원래 그릇에서 정확한 양을 빼서(Withdraw) 공중 덩어리로 들고 있다가 낙하 시간이 지나면 받는 그릇에 붓는다(Pour).
    /// 그래서 어느 프레임이든 Σ(그릇) + Σ(공중) = 처음 양이다. 차는 찻잎 g(고체)·물 mL(액체)·추출 고형분 g을 따로 장부에 두고,
    /// 우린 차를 따르면 액체와 추출분만 이동하고 거른 잎은 다관에 남는다.
    /// </summary>
    public sealed class DrinkTransferLedger
    {
        public const string WaterId = "water";
        /// <summary>찻잎 건조 질량 중 우러날 수 있는 최대 비율. 근거 실측이 아닌 게임 축약값이다(재우림·온도·시간 효과 없음).
        /// 추출분 질량은 장부에서 따로 세며 FluidColumn의 밀도·부피에는 더하지 않는다(찻물 밀도는 물 1.0 그대로인 근사).</summary>
        public const double ExtractableFraction = 0.3;
        /// <summary>우림 시간 상수(초, 실제 수 분을 화면 2초 안팎으로 압축한 게임값).</summary>
        public const double SteepTau = 0.7;

        readonly Dictionary<string, DrinkVesselState> vessels = new Dictionary<string, DrinkVesselState>();
        readonly List<DrinkPacket> inFlight = new List<DrinkPacket>();

        public IReadOnlyList<DrinkPacket> InFlight => inFlight;

        public DrinkVesselState this[string name] => Get(name);

        public DrinkVesselState Get(string name)
        {
            if (!vessels.TryGetValue(name, out var v)) { v = new DrinkVesselState(name); vessels[name] = v; }
            return v;
        }

        public IEnumerable<DrinkVesselState> Vessels => vessels.Values;

        /// <summary>그 그릇으로 향하는 공중 덩어리가 있는지.</summary>
        public bool HasInFlight(string to = null)
        {
            foreach (var p in inFlight) if (to == null || p.To == to) return true;
            return false;
        }

        public double InFlightMl(string to = null)
        {
            double v = 0;
            foreach (var p in inFlight) if (to == null || p.To == to) v += p.VolumeMl;
            return v;
        }

        public double InFlightLeafG(string to = null)
        {
            double g = 0;
            foreach (var p in inFlight) if (to == null || p.To == to) g += p.LeafG;
            return g;
        }

        /// <summary>
        /// 액체 이동: from의 위쪽(입구 쪽)에서 정확히 ml를 빼 공중 덩어리로 만든다. 추출 고형분은 물 비율만큼 함께 간다.
        /// 남은 양보다 많이 요구하면 남은 양만 옮긴다(반환값 = 실제 옮긴 mL).
        /// </summary>
        public double Emit(string from, string to, double ml, double fallSeconds, double gentleness01)
        {
            var src = Get(from);
            ml = Math.Min(ml, src.LiquidMl);
            if (ml <= 1e-12) return 0;
            double waterBefore = src.WaterMl;
            var parts = src.Liquid.Withdraw(ml, fromTop: true);
            double waterTaken = 0;
            foreach (var p in parts) if (p.id == WaterId) waterTaken += p.volumeMl;
            double dissolved = 0;
            if (src.DissolvedG > 0)
            {
                // 추출분은 물에 녹아 있다: 물이 빠진 비율만큼(물이 없으면 액체 비율만큼) 이동.
                double frac = waterBefore > 1e-12 ? waterTaken / waterBefore : ml / (ml + src.LiquidMl);
                dissolved = src.DissolvedG * Math.Min(1, Math.Max(0, frac));
                src.DissolvedG -= dissolved;
                if (src.Liquid.IsEmpty) { dissolved += src.DissolvedG; src.DissolvedG = 0; }
            }
            inFlight.Add(new DrinkPacket { To = to, Parts = parts, DissolvedG = dissolved, TeaStrength = src.TeaStrength, Gentleness = Clamp01(gentleness01), RemainingSeconds = Math.Max(0, fallSeconds) });
            if (src.Liquid.IsEmpty) src.TeaStrength = 0;
            return ml;
        }

        /// <summary>찻잎 이동(입자): from의 그 찻잎 g만큼(남은 양 한도).</summary>
        public double EmitLeaves(string from, string to, string leafId, double g, double fallSeconds)
        {
            var src = Get(from);
            g = Math.Min(g, src.Leaf(leafId));
            if (g <= 1e-12) return 0;
            src.LeavesG[leafId] = src.Leaf(leafId) - g;
            if (src.LeavesG[leafId] <= 1e-12) src.LeavesG.Remove(leafId);
            inFlight.Add(new DrinkPacket { To = to, LeafId = leafId, LeafG = g, RemainingSeconds = Math.Max(0, fallSeconds) });
            return g;
        }

        /// <summary>시간 경과: 도착한 덩어리를 받는 그릇에 붓는다.</summary>
        public void Advance(double dtSeconds)
        {
            for (int i = 0; i < inFlight.Count; i++)
            {
                var p = inFlight[i];
                p.RemainingSeconds -= dtSeconds;
                if (p.RemainingSeconds > 0) continue;
                Deliver(p);
                inFlight.RemoveAt(i--);
            }
        }

        /// <summary>공중 덩어리를 모두 즉시 도착시킨다(명시적 조작·건너뛰기). 양은 그대로 보존된다.</summary>
        public void Flush(string to = null)
        {
            for (int i = 0; i < inFlight.Count; i++)
            {
                if (to != null && inFlight[i].To != to) continue;
                Deliver(inFlight[i]);
                inFlight.RemoveAt(i--);
            }
        }

        void Deliver(DrinkPacket p)
        {
            var dst = Get(p.To);
            if (p.LeafG > 0) dst.AddLeaf(p.LeafId, p.LeafG);
            if (p.Parts.Length > 0)
            {
                double waterIn = 0;
                foreach (var part in p.Parts) if (part.id == WaterId) waterIn += part.volumeMl;
                double waterOld = dst.WaterMl;
                if (waterIn + waterOld > 1e-12) dst.TeaStrength = (dst.TeaStrength * waterOld + p.TeaStrength * waterIn) / (waterOld + waterIn);
                dst.Liquid.Pour(p.Parts, p.Gentleness);
            }
            dst.DissolvedG += p.DissolvedG;
        }

        /// <summary>
        /// 우림 한 단계: 그릇의 찻잎에서 물로 추출 고형분이 옮겨 간다(지수 접근). 잎 질량이 줄고 추출분이 늘어 합은 같다.
        /// TeaStrength = 추출분 / (처음 잎 질량 × 최대 추출 비율).
        /// </summary>
        public void Steep(string vesselName, double dtSeconds)
        {
            var v = Get(vesselName);
            double leaves = v.LeafG;
            if (dtSeconds <= 0 || leaves <= 1e-12 || v.WaterMl <= 1e-12) return;
            double original = leaves + v.DissolvedG; // 이 그릇의 추출분은 모두 이 잎에서 나왔다(우림 전 다관에는 차가 없다)
            double target = original * ExtractableFraction;
            double gain = Math.Max(0, target - v.DissolvedG) * (1 - Math.Exp(-dtSeconds / SteepTau));
            gain = Math.Min(gain, leaves);
            if (gain <= 0) return;
            // 종류별 비례 감소
            var keys = new List<string>(v.LeavesG.Keys);
            foreach (var k in keys) v.LeavesG[k] -= gain * (v.LeavesG[k] / leaves);
            v.DissolvedG += gain;
            v.TeaStrength = Clamp01(v.DissolvedG / target);
        }

        /// <summary>한 성분의 전체 양: 모든 그릇 + 공중(mL). 보존 검증용.</summary>
        public double ComponentTotalMl(string id)
        {
            double v = 0;
            foreach (var s in vessels.Values) v += s.Liquid.ComponentVolume(id);
            foreach (var p in inFlight) foreach (var part in p.Parts) if (part.id == id) v += part.volumeMl;
            return v;
        }

        /// <summary>찻잎 종류의 건조 잎 + 그 잎에서 녹아 나온 분량은 따로 묻지 않고 전체 고형분으로 본다.</summary>
        public double SolidsTotalG()
        {
            double g = 0;
            foreach (var s in vessels.Values) g += s.LeafG + s.DissolvedG;
            foreach (var p in inFlight) g += p.LeafG + p.DissolvedG;
            return g;
        }

        public double TotalMassG()
        {
            double m = 0;
            foreach (var s in vessels.Values) m += s.TotalMassG;
            foreach (var p in inFlight) m += p.MassG;
            return m;
        }

        public double TotalLiquidMl()
        {
            double v = 0;
            foreach (var s in vessels.Values) v += s.LiquidMl;
            v += InFlightMl();
            return v;
        }

        /// <summary>낙하 시간(초): 낙차 px와 화면 중력(px/s²). 0 이하 낙차는 0.</summary>
        public static double FallSeconds(double dropPx, double gravityPx) => dropPx <= 0 || gravityPx <= 0 ? 0 : Math.Sqrt(2 * dropPx / gravityPx);

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }
}
