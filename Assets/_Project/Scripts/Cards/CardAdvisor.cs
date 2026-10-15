using System;
using System.Collections.Generic;

namespace CCT
{
    /// <summary>49(2026-10-13) 카드 추천 판정에 쓰는 이번 런 상태. 게임은 GameManager.BuildCardAdviceContext로, 테스트는 직접 채운다.</summary>
    public sealed class CardAdviceContext
    {
        public Job job;
        public WeaponKind weapon;
        /// <summary>카드 중첩·커피 노드·하이브리드·최종 형태(히든 판정과 같은 인터페이스).</summary>
        public IHiddenRunState run;
        /// <summary>이번 런에 이미 해금한 히든.</summary>
        public Func<HiddenSkill, bool> hiddenOwned = _ => false;
        /// <summary>도감에서 발견한 히든 id — 미발견 히든은 이름을 숨긴다.</summary>
        public Func<string, bool> hiddenDiscovered = _ => false;
        public float hp = 100f, maxHp = 100f;
        /// <summary>이번 런 보유 칵테일 고유 종류 수(혼합 러시 기준), 칵테일/차 총 중첩(하이브리드 기준).</summary>
        public int cocktailKinds, cocktailStacks, teaStacks;
        public int deepSteepLevel;
        public bool hybridUnlocked, hybridPending;
        public float damageReduction;
        public int extraThrusts, pierceCount;
    }

    public struct CardAdvice
    {
        public int score;
        /// <summary>순위 = 가장 강한 근거 ×100 + 둘째 근거(동점 가르기).</summary>
        public int rank;
        /// <summary>짧은 한국어 이유(최대 2개, " · "로 연결). 근거가 없으면 빈 문자열.</summary>
        public string reason;
        public bool recommended;
    }

    /// <summary>
    /// 49 카드 추천: 제시된 카드마다 실제 런 상태로 점수와 이유를 계산한다. 결정적(난수 없음)이며 추첨·선택·성능을 바꾸지 않는다.
    /// 점수: 히든 완성 100 / 최종 형태 완성 70 / 티니 보우 조건 완성 65 / 혼합 러시 발동 60 / 생존(체력 낮음) 55·30 /
    /// 이 카드만 더 모으면 히든(남은 장수 ≤2: 45, 3: 30, 그 이상 20) / 깊은 우림 상승 40 / 최종 형태 2장 이내 20 / 하이브리드 진행 18 /
    /// 혼합 러시 진행 15 / 무기 전용 효과 12. 참고 정보(단독 추천 안 함, 15 미만): 다른 요소가 남은 히든 재료 10, 최종 형태 3장 이상 10.
    /// 순위는 가장 강한 근거 ×100 + 둘째 근거로 동점을 가른다.
    /// </summary>
    public static class CardAdvisor
    {
        public const int MinRecommendScore = 15;
        public const int MixingRushEvery = 3;
        public const float LowHpFraction = 0.35f, MidHpFraction = 0.6f;

        /// <summary>혼합 러시(46): 새 칵테일 종류가 3/6/9…번째가 되는 순간 발동. GameManager와 같은 규칙.</summary>
        public static bool MixingRushTriggers(int kindsBefore, int kindsAfter) =>
            kindsAfter > kindsBefore && kindsAfter % MixingRushEvery == 0;

        public static List<CardAdvice> Evaluate(IList<CardOffer> offers, CardAdviceContext ctx)
        {
            var result = new List<CardAdvice>();
            if (offers == null) return result;
            int best = 0;
            foreach (var offer in offers)
            {
                var a = ctx == null || offer.card == null ? new CardAdvice { reason = "" } : Advise(offer.card, ctx);
                best = Math.Max(best, a.score);
                result.Add(a);
            }
            // 동점이면 둘째 근거로 가른다(결정적). 그래도 같으면 같은 수준으로 함께 표시.
            int bestRank = 0;
            foreach (var a in result) if (a.score == best) bestRank = Math.Max(bestRank, a.rank);
            if (best >= MinRecommendScore)
                for (int i = 0; i < result.Count; i++)
                    if (result[i].score == best && result[i].rank == bestRank) { var a = result[i]; a.recommended = true; result[i] = a; }
            // 제시된 카드가 모두 같은 수준이면 고를 근거가 없으므로 표식은 생략하고 이유만 보여 준다.
            if (result.Count > 1 && result.TrueForAll(a => a.recommended))
                for (int i = 0; i < result.Count; i++) { var a = result[i]; a.recommended = false; result[i] = a; }
            return result;
        }

        struct Reason { public int score; public string text; }

        public static CardAdvice Advise(CardData card, CardAdviceContext ctx)
        {
            var reasons = new List<Reason>();
            int cur = ctx.run != null ? ctx.run.Stacks(card.id) : 0;
            int next = Math.Min(card.maxStacks, cur + 1);
            // 49 검수: 최대 중첩, 무기 제한 불일치, 이 무기에 적용되지 않거나 상한에 닿은 효과가 있는 카드는 후보 전체를 추천에서 뺀다.
            bool canStack = cur < card.maxStacks && WeaponApplies(card, ctx);
            if (canStack)
            {
                AddHidden(card, ctx, cur, reasons);
                AddFinalForm(card, cur, reasons);
                AddHybrid(card, ctx, reasons);
                AddJobTrait(card, ctx, cur, next, reasons);
                AddSurvival(card, ctx, reasons);
                AddWeapon(card, ctx, reasons);
            }
            reasons.Sort((a, b) => b.score.CompareTo(a.score));
            var advice = new CardAdvice { reason = "" };
            if (reasons.Count > 0)
            {
                advice.score = reasons[0].score;
                advice.rank = reasons[0].score * 100 + (reasons.Count > 1 ? reasons[1].score : 0);
                advice.reason = reasons.Count > 1 && reasons[1].text != reasons[0].text
                    ? reasons[0].text + " · " + reasons[1].text : reasons[0].text;
            }
            return advice;
        }

        static void AddHidden(CardData card, CardAdviceContext ctx, int cur, List<Reason> reasons)
        {
            if (ctx.run == null) return;
            var withOne = new ProbeState(ctx.run, card.id, cur + 1, false);
            var maxedThis = new ProbeState(ctx.run, card.id, card.maxStacks, false);
            var othersMaxCur = new ProbeState(ctx.run, card.id, cur, true);
            var othersMaxFull = new ProbeState(ctx.run, card.id, card.maxStacks, true);
            Reason? bestHidden = null;
            foreach (var def in HiddenSkillDatabase.All)
            {
                if (!def.AvailableFor(ctx.job) || ctx.hiddenOwned(def.skill) || def.MetFor(ctx.run)) continue;
                string name = ctx.hiddenDiscovered(def.id) ? $"히든 '{def.name}'" : "숨은 조합";
                Reason r;
                if (def.MetFor(withOne)) r = new Reason { score = 100, text = name + " 조건 완성" };
                else if (def.MetFor(maxedThis))
                {
                    // 다른 요소는 이미 충족 — 이 카드가 몇 장 더 필요한지 실제 조건으로 센다.
                    int need = card.maxStacks - cur;
                    for (int n = cur + 2; n <= card.maxStacks; n++)
                        if (def.MetFor(new ProbeState(ctx.run, card.id, n, false))) { need = n - cur; break; }
                    r = new Reason { score = need <= 2 ? 45 : need == 3 ? 30 : 20, text = $"{name}까지 이 카드 {need}장" };
                }
                // 이 카드 중첩이 실제로 조건에 쓰이는지: 다른 요소를 모두 채워도 지금 중첩으론 부족하고, 이 카드를 더 모으면 충족.
                // 다른 요소가 남아 있으므로 참고 정보로만 표시(단독으로는 추천하지 않음).
                else if (!def.MetFor(othersMaxCur) && def.MetFor(othersMaxFull)) r = new Reason { score = 10, text = name + " 재료" };
                else continue;
                if (bestHidden == null || r.score > bestHidden.Value.score) bestHidden = r;
            }
            if (bestHidden.HasValue) reasons.Add(bestHidden.Value);
        }

        static void AddFinalForm(CardData card, int cur, List<Reason> reasons)
        {
            var form = AttackFinalForms.ForCard(card.id);
            if (!form.HasValue) return;
            int left = card.maxStacks - (cur + 1);
            if (left == 0) reasons.Add(new Reason { score = 70, text = "최대 중첩: 최종 형태 해금" });
            else reasons.Add(new Reason { score = left <= 2 ? 20 : 10, text = $"최종 형태까지 {left}장" });
        }

        static void AddHybrid(CardData card, CardAdviceContext ctx, List<Reason> reasons)
        {
            if (ctx.job != Job.Bartender || ctx.weapon != WeaponKind.Rapier || ctx.hybridUnlocked || ctx.hybridPending) return;
            int c = ctx.cocktailStacks, t = ctx.teaStacks;
            if (card.category == CardCategory.Cocktail) { if (c >= 3) return; c++; }
            else if (card.category == CardCategory.Tea) { if (t >= 3) return; t++; }
            else return;
            if (c >= 3 && t >= 3) reasons.Add(new Reason { score = 65, text = "티니 보우 해금 조건 완성" });
            else reasons.Add(new Reason { score = 18, text = $"티니 보우 조건 칵테일 {Math.Min(3, c)}/3 · 차 {Math.Min(3, t)}/3" });
        }

        static void AddJobTrait(CardData card, CardAdviceContext ctx, int cur, int next, List<Reason> reasons)
        {
            if (ctx.job == Job.Bartender && card.category == CardCategory.Cocktail)
            {
                if (cur > 0) return; // 이미 보유한 칵테일의 추가 중첩은 새 종류가 아니다.
                int after = ctx.cocktailKinds + 1;
                if (MixingRushTriggers(ctx.cocktailKinds, after))
                    reasons.Add(new Reason { score = 60, text = $"새 칵테일 {after}종째: 혼합 러시 발동" });
                else
                    reasons.Add(new Reason { score = 15, text = $"새 칵테일 종류 (러시 {after % MixingRushEvery}/{MixingRushEvery})" });
            }
            else if (ctx.job == Job.TeaSommelier && card.category == CardCategory.Tea && ctx.run != null)
            {
                // 깊은 우림: 가장 많이 중첩한 차의 (중첩-1), 최대 6. 이 카드 중첩이 최고치를 넘길 때만 실제로 오른다.
                int level = Math.Min(6, Math.Max(ctx.deepSteepLevel, next - 1));
                if (level > ctx.deepSteepLevel)
                    reasons.Add(new Reason { score = 40, text = $"깊은 우림 {ctx.deepSteepLevel}→{level} (피해 +{level * 2}%)" });
            }
        }

        static void AddSurvival(CardData card, CardAdviceContext ctx, List<Reason> reasons)
        {
            if (card.effects == null || ctx.maxHp <= 0f) return;
            // 레벨업 선택 직후 기본 회복(30%)을 반영한 실제 체력 비율.
            float after = Math.Min(ctx.maxHp, ctx.hp + ctx.maxHp * Balance.LevelUpHealFraction) / ctx.maxHp;
            if (after >= MidHpFraction) return;
            string what = null;
            foreach (var e in card.effects)
            {
                if (e.type == CardEffectType.RegenBonus && e.value > 0f) { what = $"초당 회복 +{e.value:0.#}"; break; }
                if (e.type == CardEffectType.MaxHpFlat && e.value > 0f) { what = $"최대 체력 +{e.value:0}"; break; }
                if (e.type == CardEffectType.DamageReduction && e.value > 0f && ctx.damageReduction < 0.25f - 1e-4f)
                { what = "받는 피해 감소"; break; }
            }
            if (what == null) return;
            int pct = (int)Math.Round(ctx.hp / ctx.maxHp * 100f);
            reasons.Add(new Reason { score = after < LowHpFraction ? 55 : 30, text = $"체력 {pct}%: {what}" });
        }

        /// <summary>무기 제한·효과가 현재 무기에 실제로 적용되는지(근접 연속타는 근접만, 관통은 원거리만, 각 상한 전).</summary>
        public static bool WeaponApplies(CardData card, CardAdviceContext ctx)
        {
            bool ranged = ctx.weapon.IsRanged();
            if (card.weaponRestriction == WeaponRestriction.MeleeOnly && ranged) return false;
            if (card.weaponRestriction == WeaponRestriction.RangedOnly && !ranged) return false;
            if (card.effects != null)
                foreach (var e in card.effects)
                {
                    if (e.type == CardEffectType.ExtraThrust && (ranged || ctx.extraThrusts >= PlayerStats.MaxExtraThrusts)) return false;
                    if (e.type == CardEffectType.PierceCount && (!ranged || ctx.pierceCount >= PlayerStats.MaxPierceCount)) return false;
                }
            return true;
        }

        static void AddWeapon(CardData card, CardAdviceContext ctx, List<Reason> reasons)
        {
            bool ranged = ctx.weapon.IsRanged();
            if (card.weaponRestriction == WeaponRestriction.MeleeOnly && !ranged)
                reasons.Add(new Reason { score = 12, text = $"{WeaponName(ctx.weapon)} 전용 강화" });
            else if (card.weaponRestriction == WeaponRestriction.RangedOnly && ranged)
                reasons.Add(new Reason { score = 12, text = $"{WeaponName(ctx.weapon)} 전용 강화" });
        }

        public static string WeaponName(WeaponKind w) => w switch
        {
            WeaponKind.Rapier => "레이피어",
            WeaponKind.Gun => "총",
            WeaponKind.Sword => "검",
            _ => "활",
        };

        /// <summary>한 카드 중첩만 바꾼 가상 상태. othersMax면 나머지 카드·커피·하이브리드·최종 형태를 모두 채운다(조건이 이 카드를 쓰는지 판정용).</summary>
        sealed class ProbeState : IHiddenRunState
        {
            readonly IHiddenRunState real;
            readonly string id;
            readonly int stacks;
            readonly bool othersMax;
            public ProbeState(IHiddenRunState real, string id, int stacks, bool othersMax)
            { this.real = real; this.id = id; this.stacks = stacks; this.othersMax = othersMax; }
            public int Stacks(string cardId) => cardId == id ? stacks : othersMax ? 99 : real.Stacks(cardId);
            public bool Coffee(string nodeId) => othersMax || real.Coffee(nodeId);
            public bool Hybrid => othersMax || real.Hybrid;
            public int FinalFormCount => othersMax ? 5 : real.FinalFormCount;
            public bool HasFinalForm(AttackFinalForm form) => othersMax || real.HasFinalForm(form);
            public Job Job => real.Job;
        }
    }
}
