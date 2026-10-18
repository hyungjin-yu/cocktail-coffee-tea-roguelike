using System;
using System.Collections.Generic;

namespace CCT
{
    public enum AttackFinalForm { Pierce, Spread, Chain, Flame, Poison }
    public sealed class AttackFinalForms
    {
        readonly HashSet<AttackFinalForm> unlocked = new HashSet<AttackFinalForm>();
        public event Action<AttackFinalForm> Unlocked;
        public bool Has(AttackFinalForm form) => unlocked.Contains(form);
        public int Count => unlocked.Count;
        public static AttackFinalForm? ForCard(string id) => id switch
        {
            "rifled_barrel" or "piercing_arrowhead" => AttackFinalForm.Pierce,
            "champagne_spray" or "tea_leaf_scatter" => AttackFinalForm.Spread,
            "flambe" or "second_infusion" => AttackFinalForm.Chain,
            "molotov" or "blazer" => AttackFinalForm.Flame,
            "nettle_tea" or "nightshade_blend" => AttackFinalForm.Poison,
            _ => null
        };
        public void OnCardTaken(CardData card, int stacks)
        {
            var form = ForCard(card.id);
            if (form.HasValue && stacks >= card.maxStacks && unlocked.Add(form.Value))
                Unlocked?.Invoke(form.Value);
        }
        public static string Description(AttackFinalForm form) => form switch
        {
            AttackFinalForm.Pierce => "원형 파열: 주탄 적중마다 8방향, 피해 20% · 비재귀",
            AttackFinalForm.Spread => "교차 확산: 바깥 보조 2발, 피해 60% · 파열 없음",
            AttackFinalForm.Chain => "마무리 일격: 마지막 타격 범위 125% · 피해 150%",
            AttackFinalForm.Flame => "집중 연소: 직접 적중 화염 4초 · 초당 피해 150%",
            _ => "여운 확산: 사망 시 반경 2 최대 3적 · 여운 피해 50% · 재확산 없음"
        };
    }
}
