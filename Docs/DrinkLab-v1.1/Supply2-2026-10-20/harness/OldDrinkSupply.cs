using System;
using System.Collections.Generic;

namespace CCTOld
{
    /// <summary>
    /// 72 §2 제조 재료 수급(최초 기본값, 플레이 검증으로 조정): 묶음 수량은 재료별 고정, 종류만 획득처별 가중치 표에서 뽑는다.
    /// 77 상대 부족 보정(2026-10-20, 72의 절대 보정 대체): 이번 추첨의 실제 후보마다 e = max(0, 창고 + 이번 런 미정산) / 기준 사용량 d,
    /// 후보 e의 중앙값 m(짝수면 가운데 두 값 평균), 배율 c = clamp((m+3)/(e+3), 0.5, 2.0). 후보 하나·모두 같은 회분이면 c=1 → 원래 가중치 비율.
    /// 여러 묶음은 앞 추첨 결과를 다음 추첨의 보유량에 더한다. 난수는 runId·보상 ID·추첨 순번의 해시(전투 난수와 독립, 저장 재시도로 재추첨 없음).
    /// 단위: 0.1mL / 0.1g 정수(DrinkRules와 같음). UnityEngine 비의존(순수 시험 가능).
    /// </summary>
    public static class DrinkSupply
    {
        public enum Source { Normal, Elite, MidBoss, FinalBoss }

        public sealed class Row
        {
            public string id;
            public int bundle, baseUse;
            public int normal, elite, midBoss, finalBoss;
            public bool rare;
            public int Weight(Source s) => s switch { Source.Normal => normal, Source.Elite => elite, Source.MidBoss => midBoss, _ => finalBoss };
        }

        /// <summary>표 순서 = 추첨 누적 순서(결정적).</summary>
        public static readonly Row[] Table =
        {
            new Row { id = "gin", bundle = 150, baseUse = 300, normal = 18, elite = 15, midBoss = 12, finalBoss = 10 },
            new Row { id = "rum", bundle = 150, baseUse = 300, normal = 18, elite = 15, midBoss = 12, finalBoss = 10 },
            new Row { id = "black_tea", bundle = 10, baseUse = 20, normal = 18, elite = 15, midBoss = 12, finalBoss = 10 },
            new Row { id = "green_tea", bundle = 10, baseUse = 20, normal = 18, elite = 15, midBoss = 12, finalBoss = 10 },
            new Row { id = "lemon_juice", bundle = 100, baseUse = 150, normal = 14, elite = 15, midBoss = 16, finalBoss = 15 },
            new Row { id = "honey", bundle = 50, baseUse = 100, normal = 14, elite = 15, midBoss = 16, finalBoss = 15 },
            new Row { id = "blue_curacao", bundle = 50, baseUse = 100, normal = 0, elite = 5, midBoss = 10, finalBoss = 15, rare = true },
            new Row { id = "grenadine", bundle = 50, baseUse = 100, normal = 0, elite = 5, midBoss = 10, finalBoss = 15, rare = true },
        };

        public const int NormalKillsPerBundle = 25;
        public const int MidBossDraws = 3, FinalBossDraws = 6;
        /// <summary>77: 중앙값 완충(회분)과 배율 범위.</summary>
        public const double CorrectionUses = 3.0, MinMultiplier = 0.5, MaxMultiplier = 2.0;

        public static Row Get(string id)
        {
            foreach (var r in Table) if (r.id == id) return r;
            return null;
        }

        public static int Draws(Source s) => s switch { Source.MidBoss => MidBossDraws, Source.FinalBoss => FinalBossDraws, _ => 1 };

        /// <summary>희귀 보장 추첨(0부터): 중간 보스 3번째, 최종 보스 5·6번째는 큐라소/그레나딘 후보만.</summary>
        public static bool RareOnly(Source s, int drawIndex) =>
            (s == Source.MidBoss && drawIndex == 2) || (s == Source.FinalBoss && drawIndex >= 4);

        /// <summary>77 상대 보정 배율: 후보 중앙값 m회분 대비 이 재료 e회분. clamp((m+3)/(e+3), 0.5, 2.0).</summary>
        public static double Multiplier(double usesHeld, double medianUses)
        {
            double c = (Math.Max(0, medianUses) + CorrectionUses) / (Math.Max(0, usesHeld) + CorrectionUses);
            return c < MinMultiplier ? MinMultiplier : c > MaxMultiplier ? MaxMultiplier : c;
        }

        /// <summary>중앙값(짝수 개면 가운데 두 값의 평균). 빈 목록은 0.</summary>
        public static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var v = new List<double>(values);
            v.Sort();
            int k = v.Count / 2;
            return v.Count % 2 == 1 ? v[k] : (v[k - 1] + v[k]) / 2.0;
        }

        /// <summary>보유 회분 e(음수 보유는 0).</summary>
        public static double Uses(Row r, int held) => Math.Max(0, held) / (double)r.baseUse;

        /// <summary>이번 추첨의 후보별 최종 확률(합 1). held: 창고+미정산(0.1 단위).
        /// 후보 = 이 획득처 가중치 &gt; 0 (희귀 보장 추첨이면 희귀만) — 중앙값도 이 후보들로만 구한다.</summary>
        public static Dictionary<string, double> Probabilities(Source s, bool rareOnly, Func<string, int> held)
        {
            var rows = new List<Row>();
            var uses = new List<double>();
            foreach (var r in Table)
            {
                if (r.Weight(s) <= 0 || (rareOnly && !r.rare)) continue;
                rows.Add(r);
                uses.Add(Uses(r, held(r.id)));
            }
            double m = Median(uses);
            var w = new double[rows.Count];
            double sum = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                w[i] = rows[i].Weight(s) * (rows.Count == 1 ? 1.0 : Multiplier(uses[i], m));
                sum += w[i];
            }
            var p = new Dictionary<string, double>();
            for (int i = 0; i < rows.Count; i++) p[rows[i].id] = sum > 0 ? w[i] / sum : 0;
            return p;
        }

        /// <summary>u∈[0,1)로 한 종류를 고른다(표 순서 누적).</summary>
        public static string Pick(Source s, bool rareOnly, Func<string, int> held, double u)
        {
            var p = Probabilities(s, rareOnly, held);
            double acc = 0; string last = null;
            foreach (var r in Table)
            {
                if (!p.TryGetValue(r.id, out double pr) || pr <= 0) continue;
                acc += pr; last = r.id;
                if (u < acc) return r.id;
            }
            return last;
        }

        /// <summary>한 보상(같은 awardId)의 모든 묶음. held는 창고+이번 런 미정산(이 보상 이전) — 앞 묶음은 다음 추첨에 더해진다.</summary>
        public static List<(string id, int amount)> Roll(Source s, string runId, string awardId, Func<string, int> held)
        {
            var got = new Dictionary<string, int>();
            var list = new List<(string, int)>();
            int n = Draws(s);
            for (int i = 0; i < n; i++)
            {
                double u = Unit(runId, awardId, i);
                string id = Pick(s, RareOnly(s, i), x => held(x) + (got.TryGetValue(x, out int g) ? g : 0), u);
                if (id == null) continue;
                int amount = Get(id).bundle;
                got[id] = (got.TryGetValue(id, out int a) ? a : 0) + amount;
                list.Add((id, amount));
            }
            return list;
        }

        /// <summary>결정적 [0,1) 난수: FNV-1a 64 → splitmix 마무리. 같은 runId·awardId·순번은 항상 같은 값.</summary>
        public static double Unit(string runId, string awardId, int index)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                void Mix(string str) { if (str != null) foreach (char c in str) { h ^= c; h *= 1099511628211UL; } h ^= 0xFF; h *= 1099511628211UL; }
                Mix(runId); Mix(awardId); Mix(index.ToString());
                h += 0x9E3779B97F4A7C15UL;
                h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
                h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return (h >> 11) * (1.0 / 9007199254740992.0);
            }
        }

        // ───── 72 §3 연구 포인트(RP) ─────
        public const int KillsPerRp = 100, KillRpCap = 5, MidBossRp = 2, FinalBossRp = 4, FirstGrantRp = 10;

        /// <summary>런 종료 정산 RP: 유효 처치(일반+정예) 100회당 1(최대 5) + 중간 보스(강화 포함)당 2 + 최종 보스 4. 이월 없음.</summary>
        public static int RunRp(int effectiveKills, int midBossKills, int finalBossKills) =>
            Math.Min(KillRpCap, Math.Max(0, effectiveKills) / KillsPerRp) + MidBossRp * Math.Max(0, midBossKills) + FinalBossRp * Math.Max(0, finalBossKills);
    }
}
