using System;
using System.Collections.Generic;

namespace CCT
{
    public enum HiddenReqKind { Card, Coffee, Hybrid, FinalFormCount, FinalForm }

    /// <summary>57(2026-10-15) P1 히든 조건 한 요소. HiddenSkillDatabase의 실제 판정(met/teaMet)과 1:1로 맞춘 표시용 구조 —
    /// HiddenRequirementTests가 "모두 채우면 해금, 하나라도 빠지면 미해금"을 직업별로 검증해 안내가 실제 규칙과 어긋나지 않게 한다.</summary>
    public struct HiddenRequirement
    {
        public HiddenReqKind kind;
        public string id;
        public int count;
        public AttackFinalForm form;
        public string label;
    }

    public struct HiddenReqProgress
    {
        public HiddenRequirement req;
        public int current;
        public bool met;

        /// <summary>"드라이 진 1/2" · "커피 콜드브루 ✓"처럼 짧은 진행 문구(찻집이면 연구 이름 치환은 호출자가).</summary>
        public string Text => req.kind == HiddenReqKind.Card || req.kind == HiddenReqKind.FinalFormCount
            ? $"{req.label} {Math.Min(current, req.count)}/{req.count}{(met ? " ✓" : "")}"
            : $"{req.label}{(met ? " ✓" : " ✗")}";
    }

    /// <summary>이번 런에서 그 히든을 노릴 수 있는지(직업·무기·카드 풀 기준). 노릴 수 없으면 사유.</summary>
    public struct HiddenReachability
    {
        public bool reachable;
        public string reason;
    }

    public static class HiddenRequirements
    {
        static HiddenRequirement C(string id, int n, string label) => new HiddenRequirement { kind = HiddenReqKind.Card, id = id, count = n, label = label };
        static HiddenRequirement K(string id, string label) => new HiddenRequirement { kind = HiddenReqKind.Coffee, id = id, count = 1, label = label };
        static HiddenRequirement Hy(string label) => new HiddenRequirement { kind = HiddenReqKind.Hybrid, count = 1, label = label };
        static HiddenRequirement FfN(int n, string label) => new HiddenRequirement { kind = HiddenReqKind.FinalFormCount, count = n, label = label };
        static HiddenRequirement Ff(AttackFinalForm f, string label) => new HiddenRequirement { kind = HiddenReqKind.FinalForm, form = f, count = 1, label = label };

        static readonly Dictionary<HiddenSkill, HiddenRequirement[]> bartender = new Dictionary<HiddenSkill, HiddenRequirement[]>
        {
            { HiddenSkill.FirstSip, new[] { C("dry_gin", 2, "드라이 진"), C("green_tea", 2, "녹차") } },
            { HiddenSkill.HotToddy, new[] { C("molotov", 1, "플레임드 제스트"), C("chamomile", 1, "카모마일 인퓨전") } },
            { HiddenSkill.BitterBlend, new[] { C("molotov", 1, "플레임드 제스트"), C("nettle_tea", 1, "녹차의 떫은 여운") } },
            { HiddenSkill.IcedCup, new[] { C("ice_cube", 3, "얼음과 희석"), C("warm_cup", 3, "찻잔 예열") } },
            { HiddenSkill.WeekendRush, new[] { C("bar_shoes", 2, "빌드"), C("tea_sandals", 2, "출탕") } },
            { HiddenSkill.ShakeRhythm, new[] { C("shot_glass", 3, "지거"), C("tea_whisk", 3, "차선") } },
            { HiddenSkill.SodaPump, new[] { K("cold_brew", "커피 콜드브루"), C("syrup", 2, "프리배치") } },
            { HiddenSkill.CalmService, new[] { K("doppio_body", "커피 도피오 체력"), C("chamomile", 2, "카모마일 인퓨전") } },
            { HiddenSkill.LongReach, new[] { C("long_stirrer", 2, "바 스푼"), C("long_spout", 2, "긴 주둥이 주전자") } },
            { HiddenSkill.DoubleFinish, new[] { C("flambe", 2, "더블 스트레인"), C("second_infusion", 2, "재우림") } },
            { HiddenSkill.PointBlank, new[] { C("champagne_spray", 1, "스플릿 베이스"), C("tea_leaf_scatter", 1, "블렌딩") } },
            { HiddenSkill.BitterWake, new[] { K("espresso_shot", "커피 에스프레소 샷"), C("oolong", 1, "우롱차") } },
            { HiddenSkill.VipService, new[] { C("scotch_rocks", 1, "올드패션드"), C("aged_puerh", 1, "보이숙차"), K("full_body_roast", "커피 풀바디 로스트") } },
            { HiddenSkill.HerbGarden, new[] { C("chamomile", 2, "카모마일 인퓨전"), C("ginseng_root", 1, "루이보스 인퓨전"), K("mellow_finish", "커피 부드러운 여운") } },
            { HiddenSkill.PoisonGarden, new[] { C("nettle_tea", 2, "녹차의 떫은 여운"), C("nightshade_blend", 1, "홍차의 떫은 여운"), K("ristretto", "커피 리스트레토") } },
            { HiddenSkill.FireShow, new[] { C("molotov", 2, "플레임드 제스트"), C("blazer", 1, "블루 블레이저"), K("espresso_shot", "커피 에스프레소 샷") } },
            { HiddenSkill.BeanCrate, new[] { C("syrup", 2, "프리배치"), C("dried_leaf", 2, "찻잎 건조"), K("fine_grind", "커피 곱게 갈기") } },
            { HiddenSkill.ThickMug, new[] { C("ice_cube", 2, "얼음과 희석"), C("warm_cup", 2, "찻잔 예열"), K("insulated_cup", "커피 보온 컵") } },
            { HiddenSkill.TrayGuard, new[] { C("ice_cube", 1, "얼음과 희석"), C("warm_cup", 1, "찻잔 예열"), K("wide_tray", "커피 넓은 트레이") } },
            { HiddenSkill.LongSteep, new[] { C("molotov", 1, "플레임드 제스트"), C("nettle_tea", 1, "녹차의 떫은 여운"), K("balanced_extraction", "커피 균형 추출") } },
            { HiddenSkill.PerfectCup, new[] { K("perfect_extraction", "커피 완전 추출"), FfN(1, "최종 형태") } },
            { HiddenSkill.TiniMaster, new[] { Hy("티니 보우"), Ff(AttackFinalForm.Chain, "마무리 일격 최종 형태") } },
            { HiddenSkill.TriRoast, new[] { K("balanced_extraction", "커피 균형 추출"), K("mellow_finish", "커피 부드러운 여운"), K("wide_tray", "커피 넓은 트레이") } },
            { HiddenSkill.BarTempo, new[] { C("mixing_tempo", 4, "셰이킹"), C("brew_rhythm", 4, "공부차 우림"), K("ristretto", "커피 리스트레토") } },
        };

        static readonly Dictionary<HiddenSkill, HiddenRequirement[]> tea = new Dictionary<HiddenSkill, HiddenRequirement[]>
        {
            { HiddenSkill.FirstSip, new[] { C("green_tea", 4, "녹차") } },
            { HiddenSkill.IcedCup, new[] { C("warm_cup", 5, "찻잔 예열") } },
            { HiddenSkill.WeekendRush, new[] { C("tea_sandals", 4, "출탕") } },
            { HiddenSkill.CalmService, new[] { K("doppio_body", "커피 도피오 체력"), C("chamomile", 2, "카모마일 인퓨전") } },
            { HiddenSkill.LongReach, new[] { C("long_spout", 4, "긴 주둥이 주전자") } },
            { HiddenSkill.DoubleFinish, new[] { C("second_infusion", 3, "재우림") } },
            { HiddenSkill.PointBlank, new[] { C("tea_leaf_scatter", 3, "블렌딩") } },
            { HiddenSkill.BitterWake, new[] { K("espresso_shot", "커피 에스프레소 샷"), C("oolong", 1, "우롱차") } },
            { HiddenSkill.VipService, new[] { C("aged_puerh", 2, "보이숙차"), K("full_body_roast", "커피 풀바디 로스트") } },
            { HiddenSkill.HerbGarden, new[] { C("chamomile", 2, "카모마일 인퓨전"), C("ginseng_root", 1, "루이보스 인퓨전"), K("mellow_finish", "커피 부드러운 여운") } },
            { HiddenSkill.PoisonGarden, new[] { C("nettle_tea", 2, "녹차의 떫은 여운"), C("nightshade_blend", 1, "홍차의 떫은 여운"), K("ristretto", "커피 리스트레토") } },
            { HiddenSkill.BeanCrate, new[] { C("dried_leaf", 4, "찻잎 건조"), K("fine_grind", "커피 곱게 갈기") } },
            { HiddenSkill.ThickMug, new[] { C("warm_cup", 4, "찻잔 예열"), K("insulated_cup", "커피 보온 컵") } },
            { HiddenSkill.TrayGuard, new[] { C("warm_cup", 2, "찻잔 예열"), K("wide_tray", "커피 넓은 트레이") } },
            { HiddenSkill.LongSteep, new[] { C("nettle_tea", 2, "녹차의 떫은 여운"), K("balanced_extraction", "커피 균형 추출") } },
            { HiddenSkill.PerfectCup, new[] { K("perfect_extraction", "커피 완전 추출"), FfN(1, "최종 형태") } },
            { HiddenSkill.TriRoast, new[] { K("balanced_extraction", "커피 균형 추출"), K("mellow_finish", "커피 부드러운 여운"), K("wide_tray", "커피 넓은 트레이") } },
        };

        static readonly HiddenRequirement[] none = new HiddenRequirement[0];

        /// <summary>직업별 실제 조건 요소. 그 직업에서 해금할 수 없는 히든(찻집의 바텐더 전용 7종)은 빈 목록.</summary>
        public static IReadOnlyList<HiddenRequirement> For(HiddenSkill skill, Job job)
        {
            var table = job == Job.Bartender ? bartender : tea;
            return table.TryGetValue(skill, out var reqs) ? reqs : none;
        }

        public static HiddenReqProgress Evaluate(HiddenRequirement req, IHiddenRunState s)
        {
            int cur;
            switch (req.kind)
            {
                case HiddenReqKind.Card: cur = s != null ? s.Stacks(req.id) : 0; break;
                case HiddenReqKind.Coffee: cur = s != null && s.Coffee(req.id) ? 1 : 0; break;
                case HiddenReqKind.Hybrid: cur = s != null && s.Hybrid ? 1 : 0; break;
                case HiddenReqKind.FinalFormCount: cur = s != null ? s.FinalFormCount : 0; break;
                default: cur = s != null && s.HasFinalForm(req.form) ? 1 : 0; break;
            }
            return new HiddenReqProgress { req = req, current = cur, met = cur >= req.count };
        }

        public static List<HiddenReqProgress> Progress(HiddenSkill skill, Job job, IHiddenRunState s)
        {
            var list = new List<HiddenReqProgress>();
            foreach (var r in For(skill, job)) list.Add(Evaluate(r, s));
            return list;
        }

        public static int MetCount(List<HiddenReqProgress> progress)
        {
            int n = 0;
            foreach (var p in progress) if (p.met) n++;
            return n;
        }

        /// <summary>표시 이름: 도감에서 발견했으면 이름, 아니면 ???(41 결정: 미발견 = ??? + 단계 + 힌트).</summary>
        public static string DisplayName(HiddenSkillDef def, Job job, bool discovered) =>
            discovered ? JobStudyTheme.HiddenName(def, job) : "???";

        /// <summary>목표 조합 화면·카드 창·HUD에 쓰는 조건 진행 문구. 57(2026-10-15) 결정: 목표로 고르는 화면에서는 미발견 히든이라도
        /// 이름·효과만 ???로 두고 필요한 카드·커피 조건과 현재 수는 모두 공개한다(41의 "힌트 1요소만" 규칙을 목표 조합 UI에서만 대체 —
        /// 첫 플레이 피드백 "추천에 의존해 내가 만든 느낌이 약하다"를 풀려면 부족 조건을 알아야 계획할 수 있다). 도감 표시는 그대로.
        /// 찻집이면 커피→우림 연구 이름으로 바꾼다.</summary>
        public static string ProgressLine(HiddenSkillDef def, Job job, IHiddenRunState s, string separator = " · ")
        {
            var parts = new List<string>();
            foreach (var p in Progress(def.skill, job, s)) parts.Add(JobStudyTheme.Text(p.Text, job));
            return string.Join(separator, parts);
        }

        /// <summary>이번 런에서 노릴 수 있는지. cardObtainable: 이 직업·무기로 카드 창에 나올 수 있는 카드 id인지(GameManager 제공).</summary>
        public static HiddenReachability Reachability(HiddenSkillDef def, Job job, WeaponKind weapon, Func<string, bool> cardObtainable)
        {
            if (!def.AvailableFor(job)) return new HiddenReachability { reason = "바텐더 전용" };
            foreach (var r in For(def.skill, job))
            {
                if (r.kind == HiddenReqKind.Card && cardObtainable != null && !cardObtainable(r.id))
                    return new HiddenReachability { reason = $"{r.label}: {(weapon.IsRanged() ? "근접" : "원거리")} 무기 전용" };
                if (r.kind == HiddenReqKind.Hybrid && (job != Job.Bartender || weapon != WeaponKind.Rapier))
                    return new HiddenReachability { reason = "티니 보우: 레이피어 시작 전용" };
            }
            return new HiddenReachability { reachable = true, reason = "" };
        }

        /// <summary>이 카드를 1장 더 고르면 목표 히든의 어떤 조건이 오르는지. 오르지 않으면 false.
        /// completes: 이 1장으로 목표 조건이 모두 채워지는지(실제 판정 MetFor로 확인).</summary>
        public static bool CardAdvances(HiddenSkillDef def, Job job, IHiddenRunState s, string cardId,
            out HiddenReqProgress before, out bool completes)
        {
            before = default;
            completes = false;
            if (s == null || string.IsNullOrEmpty(cardId)) return false;
            foreach (var r in For(def.skill, job))
            {
                if (r.kind != HiddenReqKind.Card || r.id != cardId) continue;
                var p = Evaluate(r, s);
                if (p.met) return false;
                before = p;
                completes = def.MetFor(new PlusOneState(s, cardId));
                return true;
            }
            return false;
        }

        sealed class PlusOneState : IHiddenRunState
        {
            readonly IHiddenRunState real;
            readonly string id;
            public PlusOneState(IHiddenRunState real, string id) { this.real = real; this.id = id; }
            public int Stacks(string cardId) => real.Stacks(cardId) + (cardId == id ? 1 : 0);
            public bool Coffee(string nodeId) => real.Coffee(nodeId);
            public bool Hybrid => real.Hybrid;
            public int FinalFormCount => real.FinalFormCount;
            public bool HasFinalForm(AttackFinalForm form) => real.HasFinalForm(form);
            public Job Job => real.Job;
        }
    }
}
