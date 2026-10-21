using System;
using System.Collections.Generic;
using System.Linq;

namespace CCT
{
    /// <summary>
    /// 72 §2 제조 재료 수급(최초 기본값, 플레이 검증으로 조정): 묶음 수량은 재료별 고정, 종류만 획득처별 가중치 표에서 뽑는다.
    /// 77 상대 부족 보정: 실제 후보의 보유 회분 중 양수인 값의 중앙값 m을 기준으로 c = clamp((m+3)/(e+3), 0.5, 2.0).
    /// 30종 목표 확장(2026-10-21): 신규 재료가 절반 이상 0개여도 기준값이 0으로 무너지지 않도록 양수 보유량만 기준 중앙값에 포함한다.
    /// 여러 묶음은 앞 추첨 결과를 다음 추첨의 보유량에 더한다. 난수는 runId·보상 ID·추첨 순번의 해시(전투 난수와 독립, 저장 재시도로 재추첨 없음).
    /// 78 총량·희귀 조정(2026-10-20): 일반 25킬당 1묶음은 판당 최대 60묶음, 일반·정예 표에도 희귀 후보(보장은 보스만). 중간/최종 표·추첨 수·77 보정 유지.
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

        /// <summary>표 순서 = 추첨 누적 순서(결정적). 재료 확장 10종(2026-10-21): 신규 10종을 끝에 붙이고 획득처별 합 100을 다시 나눴다.
        /// 계열 몫(일반): 칵테일 전용 32 · 차 29 · 커피 23 · 공용 16. 희귀(보스 보장 후보)는 여전히 블루 큐라소·그레나딘 두 종뿐.
        /// 신규 재료는 보유 0회분에서 시작하므로 77 상대 보정(최대 ×2)이 따라잡을 때까지 자동으로 우대한다. 근거·수치는 재료 확장 10종 문서.</summary>
        public static Row[] Table =
        {
            new Row { id = "gin", bundle = 150, baseUse = 300, normal = 6, elite = 5, midBoss = 4, finalBoss = 3 },
            new Row { id = "rum", bundle = 150, baseUse = 300, normal = 6, elite = 5, midBoss = 4, finalBoss = 3 },
            new Row { id = "black_tea", bundle = 10, baseUse = 20, normal = 6, elite = 5, midBoss = 4, finalBoss = 3 },
            new Row { id = "green_tea", bundle = 10, baseUse = 20, normal = 6, elite = 5, midBoss = 4, finalBoss = 3 },
            new Row { id = "coffee_bean", bundle = 100, baseUse = 160, normal = 5, elite = 5, midBoss = 5, finalBoss = 4 },
            new Row { id = "steamed_milk", bundle = 150, baseUse = 150, normal = 6, elite = 5, midBoss = 4, finalBoss = 4 },
            new Row { id = "lemon_juice", bundle = 100, baseUse = 150, normal = 7, elite = 6, midBoss = 7, finalBoss = 6 },
            new Row { id = "honey", bundle = 50, baseUse = 100, normal = 7, elite = 6, midBoss = 7, finalBoss = 6 },
            new Row { id = "blue_curacao", bundle = 50, baseUse = 100, normal = 4, elite = 8, midBoss = 8, finalBoss = 12, rare = true },
            new Row { id = "grenadine", bundle = 50, baseUse = 100, normal = 4, elite = 8, midBoss = 8, finalBoss = 12, rare = true },
            // 재료 확장 신규(희귀 아님 — 보스 희귀 보장 후보를 바꾸지 않는다). 묶음은 기준 사용량의 1/2(찻잎 일부 2/5~2/3).
            new Row { id = "absinthe", bundle = 50, baseUse = 100, normal = 2, elite = 3, midBoss = 3, finalBoss = 3 },
            new Row { id = "creme_cassis", bundle = 50, baseUse = 100, normal = 3, elite = 4, midBoss = 4, finalBoss = 4 },
            new Row { id = "limoncello", bundle = 100, baseUse = 200, normal = 4, elite = 3, midBoss = 4, finalBoss = 3 },
            new Row { id = "white_tea", bundle = 10, baseUse = 25, normal = 3, elite = 4, midBoss = 4, finalBoss = 4 },
            new Row { id = "oolong_tea", bundle = 10, baseUse = 20, normal = 4, elite = 3, midBoss = 3, finalBoss = 3 },
            new Row { id = "jasmine_tea", bundle = 10, baseUse = 20, normal = 4, elite = 3, midBoss = 3, finalBoss = 3 },
            new Row { id = "chamomile_flower", bundle = 10, baseUse = 15, normal = 4, elite = 3, midBoss = 3, finalBoss = 3 },
            new Row { id = "ethiopia_yirgacheffe_kochere", bundle = 80, baseUse = 160, normal = 3, elite = 4, midBoss = 4, finalBoss = 4 },
            new Row { id = "colombia_huila_vergel", bundle = 80, baseUse = 160, normal = 3, elite = 3, midBoss = 4, finalBoss = 4 },
            new Row { id = "brazil_mantiqueira_alves_lima", bundle = 80, baseUse = 160, normal = 4, elite = 3, midBoss = 4, finalBoss = 4 },
            new Row { id = "lime_juice", bundle = 100, baseUse = 150, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "simple_syrup", bundle = 100, baseUse = 150, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "dry_vermouth", bundle = 100, baseUse = 100, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "darjeeling_tea", bundle = 10, baseUse = 20, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "anxi_tieguanyin", bundle = 10, baseUse = 20, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "pu_er_tea", bundle = 10, baseUse = 20, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "panama_esmeralda_geisha", bundle = 80, baseUse = 160, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "colombia_huila_san_pedro_geisha", bundle = 80, baseUse = 160, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
            new Row { id = "colombia_la_esperanza_bourbon", bundle = 80, baseUse = 160, normal = 1, elite = 1, midBoss = 1, finalBoss = 1 },
        };

        static DrinkSupply()
        {
            // 새 카탈로그 항목은 최소 1점씩 모든 획득원에 분배한다. 낮은 재고 보정은 품목 수가 절반을 넘어도 작동한다.
            var added = new List<Row>();
            foreach (var def in DrinkIngredients.All)
            {
                if (Array.Exists(Table, r => r.id == def.id)) continue;
                bool coffee = def.IsCoffeeBean;
                bool leaf = def.form == IngredientForm.Leaf;
                int baseUse = coffee ? 160 : leaf ? 20 : 100;
                added.Add(new Row { id = def.id, bundle = coffee ? 80 : leaf ? 10 : 50, baseUse = baseUse,
                    normal = 1, elite = 1, midBoss = 1, finalBoss = 1 });
            }
            if (added.Count > 0) Table = Array.ConvertAll(Table, r => r).Concat(added).ToArray();
        }

        public const int NormalKillsPerBundle = 25;
        /// <summary>78: 판당 일반 보상 최대 묶음(1500일반킬). 정예·보스는 별도이며 포함하지 않는다. 다음 런 초기화.</summary>
        public const int NormalBundleCap = 60;
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
            var stockedUses = uses.FindAll(x => x > 0.0);
            double m = Median(stockedUses);
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
