using System;
using System.Collections.Generic;

namespace CCT
{
    /// <summary>41(2026-10-12) 히든 스킬 24종. 순서·id는 저장(도감 발견 `hidden_*`)과 묶이므로 바꾸지 않는다.</summary>
    public enum HiddenSkill
    {
        FirstSip, HotToddy, BitterBlend, IcedCup, WeekendRush, ShakeRhythm, SodaPump, CalmService,
        LongReach, DoubleFinish, PointBlank, BitterWake,
        VipService, HerbGarden, PoisonGarden, FireShow, BeanCrate, ThickMug, TrayGuard, LongSteep,
        PerfectCup, TiniMaster, TriRoast, BarTempo,
    }

    public enum HiddenTier { Two, Three, Hard }

    /// <summary>조건 판정에 쓰는 이번 런 상태(카드 중첩·커피 노드·하이브리드·최종 형태). 테스트에서 가짜로 대체한다.</summary>
    public interface IHiddenRunState
    {
        int Stacks(string cardId);
        bool Coffee(string nodeId);
        bool Hybrid { get; }
        int FinalFormCount { get; }
        bool HasFinalForm(AttackFinalForm form);
        /// <summary>47(2026-10-12): 이번 런 직업 — 찻집(티 소믈리에) 런은 칵테일 카드가 나오지 않으므로 차 전용 조건을 쓴다.</summary>
        Job Job { get; }
    }

    public sealed class HiddenSkillDef
    {
        public HiddenSkill skill;
        public string id, name, condition, hint, effect;
        public HiddenTier tier;
        public Func<IHiddenRunState, bool> met;
        /// <summary>47: 티 소믈리에 런 조건(차·커피·최종 형태만). null이면 바텐더 전용(술·불꽃·바 도구 개념).</summary>
        public string teaCondition, teaHint;
        public Func<IHiddenRunState, bool> teaMet;

        public bool AvailableFor(Job job) => job == Job.Bartender || teaMet != null;
        public string ConditionFor(Job job) => job == Job.Bartender ? condition : JobStudyTheme.Text(teaCondition ?? "바텐더 전용",job);
        public string HintFor(Job job) => job == Job.Bartender ? hint : JobStudyTheme.Text(teaHint ?? "바텐더 전용",job);
        public bool MetFor(IHiddenRunState s) => s != null && (s.Job == Job.Bartender ? met(s) : teaMet != null && teaMet(s));
    }

    public static class HiddenSkillDatabase
    {
        public const string IdPrefix = "hidden_";
        static HiddenSkillDef D(HiddenSkill s, string id, string name, HiddenTier tier, string condition, string hint,
            string effect, Func<IHiddenRunState, bool> met) =>
            new HiddenSkillDef { skill = s, id = IdPrefix + id, name = name, tier = tier, condition = condition, hint = hint, effect = effect, met = met };

        public static readonly IReadOnlyList<HiddenSkillDef> All = new[]
        {
            D(HiddenSkill.FirstSip, "first_sip", "첫 잔의 향", HiddenTier.Two, "드라이 진 2 + 녹차 잎 2", "드라이 진",
                "체력이 가득 찬 적에게 직접 타격 피해 +40%", r => r.Stacks("dry_gin") >= 2 && r.Stacks("green_tea") >= 2),
            D(HiddenSkill.HotToddy, "hot_toddy", "핫 토디", HiddenTier.Two, "화염병 칵테일 1 + 카모마일 1", "화염병 칵테일",
                "일반 적 처치 시 체력 1 회복 (초당 최대 4)", r => r.Stacks("molotov") >= 1 && r.Stacks("chamomile") >= 1),
            D(HiddenSkill.BitterBlend, "bitter_blend", "독한 블렌딩", HiddenTier.Two, "화염병 칵테일 1 + 쐐기풀차 1", "쐐기풀차",
                "화상과 중독이 함께 걸린 적의 지속 피해 +25%", r => r.Stacks("molotov") >= 1 && r.Stacks("nettle_tea") >= 1),
            D(HiddenSkill.IcedCup, "iced_cup", "얼음 찻잔", HiddenTier.Two, "얼음 한 조각 3 + 따뜻한 찻잔 3", "얼음 한 조각",
                "20초마다 다음 피격 1회를 막는 보호막", r => r.Stacks("ice_cube") >= 3 && r.Stacks("warm_cup") >= 3),
            D(HiddenSkill.WeekendRush, "weekend_rush", "주말 러시", HiddenTier.Two, "바텐더 슈즈 2 + 다도용 신발 2", "바텐더 슈즈",
                "처치 시 2초간 이동 속도 +15%", r => r.Stacks("bar_shoes") >= 2 && r.Stacks("tea_sandals") >= 2),
            D(HiddenSkill.ShakeRhythm, "shake_rhythm", "셰이킹 리듬", HiddenTier.Two, "샷 글래스 3 + 차선 3", "차선",
                "1초 안 연속 직접 타격마다 피해 +2% (최대 +20%), 피격 시 초기화", r => r.Stacks("shot_glass") >= 3 && r.Stacks("tea_whisk") >= 3),
            D(HiddenSkill.SodaPump, "soda_pump", "탄산 펌프", HiddenTier.Two, "커피 콜드브루 + 시럽 한 방울 2", "커피 콜드브루",
                "12초마다 필드의 재료를 모두 끌어옴", r => r.Coffee("cold_brew") && r.Stacks("syrup") >= 2),
            D(HiddenSkill.CalmService, "calm_service", "침착한 응대", HiddenTier.Two, "커피 도피오 체력 + 카모마일 2", "커피 도피오 체력",
                "받는 둔화 40% 약화 · 보너스 봉인 시간 40% 단축", r => r.Coffee("doppio_body") && r.Stacks("chamomile") >= 2),
            D(HiddenSkill.LongReach, "long_reach", "롱 리치", HiddenTier.Two, "롱 스터러 2 + 긴 주둥이 주전자 2", "롱 스터러",
                "사거리 70% 바깥 적에게 직접 타격 피해 +20%", r => r.Stacks("long_stirrer") >= 2 && r.Stacks("long_spout") >= 2),
            D(HiddenSkill.DoubleFinish, "double_finish", "이중 마무리", HiddenTier.Two, "플람베 2 + 두 번째 우림 2 (근접)", "플람베",
                "직접 타격 후 체력 12% 미만 일반 적 즉시 처치", r => r.Stacks("flambe") >= 2 && r.Stacks("second_infusion") >= 2),
            D(HiddenSkill.PointBlank, "point_blank", "근거리 산탄", HiddenTier.Two, "샴페인 스프레이 1 + 찻잎 흩뿌리기 1 (원거리)", "샴페인 스프레이",
                "2.5 거리 안 적에게 직접 타격 피해 +20%", r => r.Stacks("champagne_spray") >= 1 && r.Stacks("tea_leaf_scatter") >= 1),
            D(HiddenSkill.BitterWake, "bitter_wake", "쓴맛 각성", HiddenTier.Two, "커피 에스프레소 샷 + 우롱차 1", "우롱차",
                "체력 35% 이하일 때 공격 속도 +25%", r => r.Coffee("espresso_shot") && r.Stacks("oolong") >= 1),

            D(HiddenSkill.VipService, "vip_service", "VIP 접객", HiddenTier.Three, "스카치 온 더 락 1 + 숙성 보이차 1 + 커피 풀바디 로스트", "숙성 보이차",
                "보스에게 직접 타격 피해 +15%", r => r.Stacks("scotch_rocks") >= 1 && r.Stacks("aged_puerh") >= 1 && r.Coffee("full_body_roast")),
            D(HiddenSkill.HerbGarden, "herb_garden", "약초 정원", HiddenTier.Three, "카모마일 2 + 산삼 뿌리 1 + 커피 부드러운 여운", "산삼 뿌리",
                "0.5초 이상 멈추면 초당 회복량 2배", r => r.Stacks("chamomile") >= 2 && r.Stacks("ginseng_root") >= 1 && r.Coffee("mellow_finish")),
            D(HiddenSkill.PoisonGarden, "poison_garden", "독초 정원", HiddenTier.Three, "쐐기풀차 2 + 가지속 블렌드 1 + 커피 리스트레토", "가지속 블렌드",
                "중독된 적의 접촉 피해 -15%", r => r.Stacks("nettle_tea") >= 2 && r.Stacks("nightshade_blend") >= 1 && r.Coffee("ristretto")),
            D(HiddenSkill.FireShow, "fire_show", "불꽃 쇼", HiddenTier.Three, "화염병 칵테일 2 + 블레이저 1 + 커피 에스프레소 샷", "블레이저",
                "화상 중인 일반 적 이동 속도 -15%", r => r.Stacks("molotov") >= 2 && r.Stacks("blazer") >= 1 && r.Coffee("espresso_shot")),
            D(HiddenSkill.BeanCrate, "bean_crate", "원두 상자", HiddenTier.Three, "시럽 한 방울 2 + 말린 찻잎 2 + 커피 곱게 갈기", "말린 찻잎",
                "재료를 주울 때마다 체력 0.5 회복 (초당 최대 3)", r => r.Stacks("syrup") >= 2 && r.Stacks("dried_leaf") >= 2 && r.Coffee("fine_grind")),
            D(HiddenSkill.ThickMug, "thick_mug", "두꺼운 머그", HiddenTier.Three, "얼음 한 조각 2 + 따뜻한 찻잔 2 + 커피 보온 컵", "커피 보온 컵",
                "최대 체력 100 초과분 20당 피해 +1% (최대 +20%)", r => r.Stacks("ice_cube") >= 2 && r.Stacks("warm_cup") >= 2 && r.Coffee("insulated_cup")),
            D(HiddenSkill.TrayGuard, "tray_guard", "쟁반 방패", HiddenTier.Three, "얼음 한 조각 1 + 따뜻한 찻잔 1 + 커피 넓은 트레이", "커피 넓은 트레이",
                "접촉 피격 시 그 적에게 기본 피해 100% 반격 (적마다 0.5초)", r => r.Stacks("ice_cube") >= 1 && r.Stacks("warm_cup") >= 1 && r.Coffee("wide_tray")),
            D(HiddenSkill.LongSteep, "long_steep", "오래 우린 향", HiddenTier.Three, "화염병 칵테일 1 + 쐐기풀차 1 + 커피 균형 추출", "커피 균형 추출",
                "화상·중독 지속 시간 +1초", r => r.Stacks("molotov") >= 1 && r.Stacks("nettle_tea") >= 1 && r.Coffee("balanced_extraction")),

            D(HiddenSkill.PerfectCup, "perfect_cup", "완벽한 한 잔", HiddenTier.Hard, "커피 완전 추출 + 최종 형태 1개", "커피 완전 추출",
                "직접 타격 10% 확률로 피해 2배", r => r.Coffee("perfect_extraction") && r.FinalFormCount >= 1),
            D(HiddenSkill.TiniMaster, "tini_master", "티니 보우 마스터", HiddenTier.Hard, "티니 보우 + 마무리 일격 최종 형태", "티니 보우",
                "티니 보우 발사마다 화살 +1발", r => r.Hybrid && r.HasFinalForm(AttackFinalForm.Chain)),
            D(HiddenSkill.TriRoast, "tri_roast", "삼색 원두", HiddenTier.Hard, "커피 균형 추출 + 부드러운 여운 + 넓은 트레이", "커피 세 갈래 3단",
                "보스 등장 시 2초 무적 + 체력 15% 회복",
                r => r.Coffee("balanced_extraction") && r.Coffee("mellow_finish") && r.Coffee("wide_tray")),
            D(HiddenSkill.BarTempo, "bar_tempo", "바 템포 마스터", HiddenTier.Hard, "믹싱 템포 4 + 우림 리듬 4 + 커피 리스트레토", "믹싱 템포",
                "이동 중 피해 +10%, 0.5초 이상 멈추면 받는 피해 -10%",
                r => r.Stacks("mixing_tempo") >= 4 && r.Stacks("brew_rhythm") >= 4 && r.Coffee("ristretto")),
        };

        public static HiddenSkillDef Get(HiddenSkill skill) => All[(int)skill];

        /// <summary>47: 티 소믈리에(찻집) 조건 — 바텐더 조건의 칵테일 카드를 같은 역할의 차 카드로 옮기고 그 중첩을 합친다(최대 중첩 안).
        /// 술·불꽃·바 도구가 핵심인 7종(핫 토디·독한 블렌딩·셰이킹 리듬·탄산 펌프·불꽃 쇼·티니 보우 마스터·바 템포 마스터)은 바텐더 전용.</summary>
        static HiddenSkillDatabase()
        {
            void T(HiddenSkill s, string cond, string hint, Func<IHiddenRunState, bool> met)
            { var d = Get(s); d.teaCondition = cond; d.teaHint = hint; d.teaMet = met; }
            T(HiddenSkill.FirstSip, "녹차 잎 4", "녹차 잎", r => r.Stacks("green_tea") >= 4);
            T(HiddenSkill.IcedCup, "따뜻한 찻잔 5", "따뜻한 찻잔", r => r.Stacks("warm_cup") >= 5);
            T(HiddenSkill.WeekendRush, "다도용 신발 4", "다도용 신발", r => r.Stacks("tea_sandals") >= 4);
            T(HiddenSkill.CalmService, "커피 도피오 체력 + 카모마일 2", "커피 도피오 체력", r => r.Coffee("doppio_body") && r.Stacks("chamomile") >= 2);
            T(HiddenSkill.LongReach, "긴 주둥이 주전자 4", "긴 주둥이 주전자", r => r.Stacks("long_spout") >= 4);
            T(HiddenSkill.DoubleFinish, "두 번째 우림 3 (근접)", "두 번째 우림", r => r.Stacks("second_infusion") >= 3);
            T(HiddenSkill.PointBlank, "찻잎 흩뿌리기 3 (원거리)", "찻잎 흩뿌리기", r => r.Stacks("tea_leaf_scatter") >= 3);
            T(HiddenSkill.BitterWake, "커피 에스프레소 샷 + 우롱차 1", "우롱차", r => r.Coffee("espresso_shot") && r.Stacks("oolong") >= 1);
            T(HiddenSkill.VipService, "숙성 보이차 2 + 커피 풀바디 로스트", "숙성 보이차", r => r.Stacks("aged_puerh") >= 2 && r.Coffee("full_body_roast"));
            T(HiddenSkill.HerbGarden, "카모마일 2 + 산삼 뿌리 1 + 커피 부드러운 여운", "산삼 뿌리",
                r => r.Stacks("chamomile") >= 2 && r.Stacks("ginseng_root") >= 1 && r.Coffee("mellow_finish"));
            T(HiddenSkill.PoisonGarden, "쐐기풀차 2 + 가지속 블렌드 1 + 커피 리스트레토", "가지속 블렌드",
                r => r.Stacks("nettle_tea") >= 2 && r.Stacks("nightshade_blend") >= 1 && r.Coffee("ristretto"));
            T(HiddenSkill.BeanCrate, "말린 찻잎 4 + 커피 곱게 갈기", "말린 찻잎", r => r.Stacks("dried_leaf") >= 4 && r.Coffee("fine_grind"));
            T(HiddenSkill.ThickMug, "따뜻한 찻잔 4 + 커피 보온 컵", "커피 보온 컵", r => r.Stacks("warm_cup") >= 4 && r.Coffee("insulated_cup"));
            T(HiddenSkill.TrayGuard, "따뜻한 찻잔 2 + 커피 넓은 트레이", "커피 넓은 트레이", r => r.Stacks("warm_cup") >= 2 && r.Coffee("wide_tray"));
            T(HiddenSkill.LongSteep, "쐐기풀차 2 + 커피 균형 추출", "커피 균형 추출", r => r.Stacks("nettle_tea") >= 2 && r.Coffee("balanced_extraction"));
            T(HiddenSkill.PerfectCup, "커피 완전 추출 + 최종 형태 1개", "커피 완전 추출", r => r.Coffee("perfect_extraction") && r.FinalFormCount >= 1);
            T(HiddenSkill.TriRoast, "커피 균형 추출 + 부드러운 여운 + 넓은 트레이", "커피 세 갈래 3단",
                r => r.Coffee("balanced_extraction") && r.Coffee("mellow_finish") && r.Coffee("wide_tray"));
        }
        public static string TierLabel(HiddenTier tier) => tier == HiddenTier.Two ? "2요소" : tier == HiddenTier.Three ? "3요소" : "고난도";
    }

    /// <summary>한 런의 히든 해금 상태. 런마다 새로 만든다(재시작 시 초기화). 해금은 런당 1회.</summary>
    public sealed class RunHiddenSkills
    {
        readonly bool[] owned = new bool[HiddenSkillDatabase.All.Count];
        public int Count { get; private set; }
        public bool Has(HiddenSkill skill) => owned[(int)skill];

        /// <summary>조건을 새로 충족한 히든만 해금해 돌려준다(이미 가진 것은 다시 내지 않음).</summary>
        public List<HiddenSkillDef> Evaluate(IHiddenRunState state)
        {
            var unlocked = new List<HiddenSkillDef>();
            if (state == null) return unlocked;
            foreach (var def in HiddenSkillDatabase.All)
            {
                if (owned[(int)def.skill] || !def.MetFor(state)) continue; // 47 직업별 조건
                owned[(int)def.skill] = true;
                Count++;
                unlocked.Add(def);
            }
            return unlocked;
        }

        /// <summary>테스트/QA용 — 조건과 무관하게 해금(보고 시 강제 경로로 표기).</summary>
        public void ForceForTests(HiddenSkill skill)
        {
            if (owned[(int)skill]) return;
            owned[(int)skill] = true;
            Count++;
        }
    }
}
