using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text;

/// 78 old(77 방식, 14765b3: 상한 없음·일반 표 희귀 없음) vs new(78: 일반 판당 60묶음 상한·일반/정예 희귀 후보) 같은 시드 반복 추첨.
/// 순수 계산(DrinkSupply.Roll 직접 호출) — 게임/세이브 미사용, 런 중 소비 없음(순획득 = 최종 − 시작).
static class Sim {
  static readonly string[] Src = { "Normal", "Elite", "MidBoss", "FinalBoss" };
  delegate List<(string id, int amount)> RollFn(int src, string runId, string awardId, Func<string, int> held);
  static List<(string, int)> NewRoll(int s, string r, string a, Func<string, int> h) => CCT.DrinkSupply.Roll((CCT.DrinkSupply.Source)s, r, a, h);
  static List<(string, int)> OldRoll(int s, string r, string a, Func<string, int> h) => CCTOld.DrinkSupply.Roll((CCTOld.DrinkSupply.Source)s, r, a, h);

  static readonly string[] Ids = CCT.DrinkSupply.Table.Select(r => r.id).ToArray();
  static int BaseUse(string id) => CCT.DrinkSupply.Get(id).baseUse;
  static bool Rare(string id) => CCT.DrinkSupply.Get(id).rare;
  static bool Leaf(string id) => id == "black_tea" || id == "green_tea"; // DrinkIngredients IngredientForm.Leaf(0.1g), 나머지 액체(0.1mL)
  static Dictionary<string, int> FromUses(Func<string, double> u) => Ids.ToDictionary(id => id, id => (int)Math.Round(u(id) * BaseUse(id)));

  /// 한 판의 보상 순서: 일반 25킬마다(cap 묶음까지 — GameManager와 같이 cap 뒤에는 일반 보상 없음), 정예·중간은 처치 구간에 고르게, 최종은 마지막.
  static List<(int src, string awardId)> Timeline(int kills, int elites, int mids, int finals, int normalCap) {
    var ev = new List<(double t, int order, int src, string awardId)>();
    int nb = Math.Min(normalCap, kills / CCT.DrinkSupply.NormalKillsPerBundle);
    for (int i = 1; i <= nb; i++) ev.Add((i * 25.0, 0, 0, "normal" + i));
    for (int j = 1; j <= elites; j++) ev.Add((j * kills / (double)(elites + 1), 1, 1, "elite" + j));
    for (int k = 1; k <= mids; k++) ev.Add((k * kills / (double)(mids + 1), 2, 2, $"boss{k}_Mid"));
    for (int f = 1; f <= finals; f++) ev.Add((kills + f, 3, 3, $"boss{mids + f}_Final"));
    return ev.OrderBy(e => e.t).ThenBy(e => e.order).Select(e => (e.src, e.awardId)).ToList();
  }

  sealed class Agg {
    public int runs; public long[] draws = new long[4], ledger = new long[4];
    public Dictionary<string, long>[] gain = Enumerable.Range(0, 4).Select(_ => Ids.ToDictionary(i => i, i => 0L)).ToArray();
    public List<double> cvNormal = new List<double>(), cvAll = new List<double>(), minNormalUses = new List<double>(), minRareUses = new List<double>(), rareGain = new List<double>(), shortGain = new List<double>();
  }

  static double Cv(IEnumerable<double> xs) { var v = xs.ToArray(); double m = v.Average(); if (m <= 0) return 0; return Math.Sqrt(v.Select(x => (x - m) * (x - m)).Average()) / m; }
  static double Pct(List<double> v, double p) { var s = v.OrderBy(x => x).ToList(); return s[(int)Math.Min(s.Count - 1, Math.Floor(p * s.Count))]; }

  static Agg Simulate(RollFn roll, Dictionary<string, int> start, List<(int src, string awardId)> tl, int seeds, string shortId, out List<List<(string, int)>> firstRunDraws) {
    var g = new Agg(); firstRunDraws = null;
    for (int s = 0; s < seeds; s++) {
      string runId = "sim78-" + s;
      var pending = Ids.ToDictionary(i => i, i => 0);
      var thisRun = new List<List<(string, int)>>();
      foreach (var (src, awardId) in tl) {
        var list = roll(src, runId, awardId, id => start[id] + pending[id]);
        thisRun.Add(list);
        g.draws[src] += list.Count;
        g.ledger[src] += list.Select(x => x.Item1).Distinct().Count(); // DrinkLab.RollAward가 한 보상 안 같은 재료를 한 기록으로 합침
        foreach (var (id, amt) in list) { pending[id] += amt; g.gain[src][id] += amt; }
      }
      if (s == 0) firstRunDraws = thisRun;
      var finalUses = Ids.ToDictionary(id => id, id => (start[id] + pending[id]) / (double)BaseUse(id));
      g.cvNormal.Add(Cv(Ids.Where(i => !Rare(i)).Select(i => finalUses[i])));
      g.cvAll.Add(Cv(Ids.Select(i => finalUses[i])));
      g.minNormalUses.Add(Ids.Where(i => !Rare(i)).Min(i => finalUses[i]));
      g.minRareUses.Add(Ids.Where(Rare).Min(i => finalUses[i]));
      g.rareGain.Add(Ids.Where(Rare).Sum(i => pending[i] / (double)BaseUse(i)));
      if (shortId != null) g.shortGain.Add(pending[shortId] / (double)BaseUse(shortId));
      g.runs++;
    }
    return g;
  }

  static double Gain(Agg g, string id, int seeds) => Enumerable.Range(0, 4).Sum(s => g.gain[s][id]) / (double)seeds;
  static double Phys(Agg g, bool leaf, int seeds, int src = -1) =>
    Ids.Where(id => Leaf(id) == leaf).Sum(id => (src < 0 ? Enumerable.Range(0, 4) : new[] { src }).Sum(s => g.gain[s][id])) / (double)seeds / 10.0;
  static int Worse(List<double> oldV, List<double> newV) { int n = 0; for (int i = 0; i < oldV.Count; i++) if (newV[i] < oldV[i] - 1e-9) n++; return n; }

  public static int Run(string[] args) {
    int seeds = args.Length > 0 ? int.Parse(args[0]) : 1000;
    string outPath = args.Length > 1 ? args[1] : "sim78.md";
    // 72 사용자 최종 잔고 원시 0.1 단위(진480mL 럼525mL 블루60mL 그레나딘85mL 레몬365mL 꿀195mL 홍차31g 녹차41g)
    var userUnits = new Dictionary<string, int> { { "gin", 4800 }, { "rum", 5250 }, { "blue_curacao", 600 }, { "grenadine", 850 }, { "lemon_juice", 3650 }, { "honey", 1950 }, { "black_tea", 310 }, { "green_tea", 410 } };
    var scenarios = new List<(string name, Dictionary<string, int> start, string shortId)> {
      ("빈 창고(전부 0)", FromUses(_ => 0), null),
      ("균등 풍부(전부 24회분)", FromUses(_ => 24), null),
      ("사용자 제보 잔고(72)", userUnits, "blue_curacao"),
      ("홍차만 0, 나머지 24회분", FromUses(id => id == "black_tea" ? 0 : 24), "black_tea"),
      ("큐라소만 0, 나머지 24회분", FromUses(id => id == "blue_curacao" ? 0 : 24), "blue_curacao"),
    };
    var profiles = new List<(string name, int kills, int elites, int mids, int finals)> {
      ("일반 500킬+정예20+중간3+최종1", 500, 20, 3, 1),
      ("일반 2000킬+정예20+중간3+최종1", 2000, 20, 3, 1),
      ("일반 5000킬+정예20+중간3+최종1", 5000, 20, 3, 1),
    };
    var sb = new StringBuilder();
    var summary = new StringBuilder();
    var csv = new StringBuilder("scenario,profile,method,source,ingredient,unit,mean_gain_raw_0.1,mean_gain_servings\n");
    sb.AppendLine($"# 78 수급 시뮬레이션 — old(77 방식, 14765b3) vs new(78: 일반 60묶음 상한·일반/정예 희귀 후보)");
    sb.AppendLine();
    sb.AppendLine($"- 같은 시드 runId `sim78-0..{seeds - 1}`, 시나리오×조건마다 old/new가 같은 runId·awardId(=같은 난수 u)를 쓴다. 다른 점은 확률표와 일반 상한뿐(new 일반 보상은 normal1..normal60에서 멈춤).");
    sb.AppendLine("- 합성 조건(사람의 실제 플레이·확정 경제 목표 아님). 런 중 소비 없음 → 순획득 = 최종 − 시작 창고. 실제 제조 가능한 잔 수와 다르다.");
    sb.AppendLine("- 단위 분리: 액체는 mL, 찻잎(홍차·녹차)은 g으로 따로 적고 더하지 않는다. '회분'은 재료별 수량/기준 사용량이며 '회분 합'은 환산값의 단순 합일 뿐 완성 음료 잔 수가 아니다.");
    sb.AppendLine("- 실제 추첨 수 = Roll이 돌려준 묶음 수. 원장 기록 수 = DrinkLab.RollAward가 한 보상 안 같은 재료를 합친 뒤의 항목 수(추첨 ≠ 기록 건수).");
    sb.AppendLine("- 편중 지표: 최종 회분 변동계수 CV(일반 6종/전체 8종, 판별 평균), 일반 6종 최소 회분, 희귀 2종 최소 회분(판별 평균·P5). '악화 판'은 같은 시드에서 new 값이 old보다 낮은 판 수.");
    sb.AppendLine();
    sb.AppendLine("## 사용자 제보 잔고 첫 추첨 확률(이 잔고 그대로, 추첨 전)");
    sb.AppendLine();
    sb.AppendLine("| 재료 | 회분 | 일반 old(77) | 일반 new(78) | 정예 old | 정예 new |");
    sb.AppendLine("|---|---:|---:|---:|---:|---:|");
    var pON = CCTOld.DrinkSupply.Probabilities(CCTOld.DrinkSupply.Source.Normal, false, id => userUnits[id]);
    var pNN = CCT.DrinkSupply.Probabilities(CCT.DrinkSupply.Source.Normal, false, id => userUnits[id]);
    var pOE = CCTOld.DrinkSupply.Probabilities(CCTOld.DrinkSupply.Source.Elite, false, id => userUnits[id]);
    var pNE = CCT.DrinkSupply.Probabilities(CCT.DrinkSupply.Source.Elite, false, id => userUnits[id]);
    Func<Dictionary<string, double>, string, string> P = (p, id) => p.TryGetValue(id, out var v) ? (v * 100).ToString("0.0000") + "%" : "후보 아님";
    foreach (var id in Ids) sb.AppendLine($"| {id} | {userUnits[id] / (double)BaseUse(id):0.###} | {P(pON, id)} | {P(pNN, id)} | {P(pOE, id)} | {P(pNE, id)} |");
    sb.AppendLine();
    summary.AppendLine("## 요약표(판별 평균, 1000시드)");
    summary.AppendLine();
    summary.AppendLine("| 시나리오 · 조건 | 추첨 old→new | 액체 mL old→new | 찻잎 g old→new | 회분 합 old→new | 희귀 순획득 회분 old→new | CV일반6 old→new | CV전체8 old→new | 일반 최소 회분 old→new | 희귀 최소 회분 old→new | 악화 판(일반최소/희귀최소) |");
    summary.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
    foreach (var sc in scenarios)
      foreach (var pf in profiles) {
        var tlOld = Timeline(pf.kills, pf.elites, pf.mids, pf.finals, int.MaxValue);
        var tlNew = Timeline(pf.kills, pf.elites, pf.mids, pf.finals, CCT.DrinkSupply.NormalBundleCap);
        var old = Simulate(OldRoll, sc.start, tlOld, seeds, sc.shortId, out var oldFirst);
        var neu = Simulate(NewRoll, sc.start, tlNew, seeds, sc.shortId, out var newFirst);
        sb.AppendLine($"## {sc.name} · {pf.name}");
        sb.AppendLine();
        sb.AppendLine("| 출처 | 실제 추첨/판 old | new | 원장 기록/판 old | new | 순획득 액체 mL old | new | 찻잎 g old | new | 희귀 회분 old | new |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        for (int s = 0; s < 4; s++) {
          double ro = Ids.Where(Rare).Sum(id => old.gain[s][id] / (double)seeds / BaseUse(id)), rn = Ids.Where(Rare).Sum(id => neu.gain[s][id] / (double)seeds / BaseUse(id));
          sb.AppendLine($"| {Src[s]} | {old.draws[s] / (double)seeds:0.##} | {neu.draws[s] / (double)seeds:0.##} | {old.ledger[s] / (double)seeds:0.##} | {neu.ledger[s] / (double)seeds:0.##} | {Phys(old, false, seeds, s):0.#} | {Phys(neu, false, seeds, s):0.#} | {Phys(old, true, seeds, s):0.#} | {Phys(neu, true, seeds, s):0.#} | {ro:0.##} | {rn:0.##} |");
        }
        long dO = old.draws.Sum() / seeds, dN = neu.draws.Sum() / seeds;
        sb.AppendLine($"| **합계** | {old.draws.Sum() / (double)seeds:0.##} | {neu.draws.Sum() / (double)seeds:0.##} | {old.ledger.Sum() / (double)seeds:0.##} | {neu.ledger.Sum() / (double)seeds:0.##} | {Phys(old, false, seeds):0.#} | {Phys(neu, false, seeds):0.#} | {Phys(old, true, seeds):0.#} | {Phys(neu, true, seeds):0.#} | {old.rareGain.Average():0.##} | {neu.rareGain.Average():0.##} |");
        sb.AppendLine();
        sb.AppendLine("| 재료 | 단위 | 시작 회분 | 순획득 old | new | 순획득 회분 old | new | 최종 회분 old | new |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var id in Ids) {
          double go = Gain(old, id, seeds), gn = Gain(neu, id, seeds);
          double st = sc.start[id] / (double)BaseUse(id);
          string unit = Leaf(id) ? "g" : "mL";
          sb.AppendLine($"| {id} | {unit} | {st:0.##} | {go / 10:0.#}{unit} | {gn / 10:0.#}{unit} | {go / BaseUse(id):0.##} | {gn / BaseUse(id):0.##} | {st + go / BaseUse(id):0.##} | {st + gn / BaseUse(id):0.##} |");
          for (int s = 0; s < 4; s++) {
            csv.AppendLine($"\"{sc.name}\",\"{pf.name}\",old77,{Src[s]},{id},{unit},{old.gain[s][id] / (double)seeds:0.###},{old.gain[s][id] / (double)seeds / BaseUse(id):0.####}");
            csv.AppendLine($"\"{sc.name}\",\"{pf.name}\",new78,{Src[s]},{id},{unit},{neu.gain[s][id] / (double)seeds:0.###},{neu.gain[s][id] / (double)seeds / BaseUse(id):0.####}");
          }
        }
        double servOld = Ids.Sum(id => Gain(old, id, seeds) / BaseUse(id)), servNew = Ids.Sum(id => Gain(neu, id, seeds) / BaseUse(id));
        sb.AppendLine($"| **회분 합(환산 합, 잔 수 아님)** | | | — | — | {servOld:0.##} | {servNew:0.##} | | |");
        sb.AppendLine();
        sb.AppendLine($"- CV 일반6종 old {old.cvNormal.Average():0.###} → new {neu.cvNormal.Average():0.###} · CV 전체8종 old {old.cvAll.Average():0.###} → new {neu.cvAll.Average():0.###}");
        sb.AppendLine($"- 일반 6종 최소 최종 회분 평균 old {old.minNormalUses.Average():0.##} → new {neu.minNormalUses.Average():0.##} (P5 {Pct(old.minNormalUses, 0.05):0.##} → {Pct(neu.minNormalUses, 0.05):0.##}) · 악화 판 {Worse(old.minNormalUses, neu.minNormalUses)}/{seeds}");
        sb.AppendLine($"- 희귀 2종 최소 최종 회분 평균 old {old.minRareUses.Average():0.##} → new {neu.minRareUses.Average():0.##} (P5 {Pct(old.minRareUses, 0.05):0.##} → {Pct(neu.minRareUses, 0.05):0.##}) · 악화 판 {Worse(old.minRareUses, neu.minRareUses)}/{seeds}");
        if (sc.shortId != null) sb.AppendLine($"- 부족 재료 `{sc.shortId}` 순획득 회분 평균 old {old.shortGain.Average():0.##} → new {neu.shortGain.Average():0.##} (P5 {Pct(old.shortGain, 0.05):0.##} → {Pct(neu.shortGain, 0.05):0.##}) · 악화 판 {Worse(old.shortGain, neu.shortGain)}/{seeds}");
        int same = 0, total = 0;
        for (int i = 0; i < Math.Min(oldFirst.Count, newFirst.Count); i++) for (int k = 0; k < Math.Min(oldFirst[i].Count, newFirst[i].Count); k++) { total++; if (oldFirst[i][k].Item1 == newFirst[i][k].Item1) same++; }
        sb.AppendLine($"- 시드 sim78-0 한 판에서 (시간순 공통 앞부분) 같은 순번 추첨 결과 일치 {same}/{total}");
        sb.AppendLine();
        summary.AppendLine($"| {sc.name} · {pf.kills}킬 | {dO}→{dN} | {Phys(old, false, seeds):0}→{Phys(neu, false, seeds):0} | {Phys(old, true, seeds):0.#}→{Phys(neu, true, seeds):0.#} | {servOld:0.#}→{servNew:0.#} | {old.rareGain.Average():0.##}→{neu.rareGain.Average():0.##} | {old.cvNormal.Average():0.###}→{neu.cvNormal.Average():0.###} | {old.cvAll.Average():0.###}→{neu.cvAll.Average():0.###} | {old.minNormalUses.Average():0.##}→{neu.minNormalUses.Average():0.##} | {old.minRareUses.Average():0.##}→{neu.minRareUses.Average():0.##} | {Worse(old.minNormalUses, neu.minNormalUses)}/{Worse(old.minRareUses, neu.minRareUses)} |");
        Console.WriteLine($"{sc.name} | {pf.name} | draws {dO}->{dN} | serv {servOld:0.0}->{servNew:0.0} | rare {old.rareGain.Average():0.00}->{neu.rareGain.Average():0.00} | CVn {old.cvNormal.Average():0.000}->{neu.cvNormal.Average():0.000} | CVa {old.cvAll.Average():0.000}->{neu.cvAll.Average():0.000}");
      }
    summary.AppendLine();
    File.WriteAllText(outPath, sb.ToString().Replace("## 사용자 제보 잔고 첫 추첨", summary.ToString() + "## 사용자 제보 잔고 첫 추첨"), new UTF8Encoding(false));
    File.WriteAllText(Path.ChangeExtension(outPath, ".csv"), csv.ToString(), new UTF8Encoding(true));
    return 0;
  }
}
