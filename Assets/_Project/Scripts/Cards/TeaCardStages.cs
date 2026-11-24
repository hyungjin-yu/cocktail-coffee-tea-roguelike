using System.Collections.Generic;

namespace CCT
{
    public enum TeaStageKind { Hidden, Core, Utility }

    /// <summary>한 단계 효과 한 줄: 같은 카드를 stack회째 골랐을 때 한 번 더해지는 능력치 1개.</summary>
    public struct TeaCardStage
    {
        public int stack;
        public TeaStageKind kind;
        public CardEffect effect;
    }

    /// <summary>
    /// #19(2026-11-24) 티 소믈리에 카드 획득 단계(사용자 확정 규칙).
    /// - 대상: 티 소믈리에 런 + 차 카드(category Tea)만. 바텐더는 차 카드를 받아도 단계 효과 없음(기존 그대로).
    /// - 1회 = 기존 기본 효과. 2회 = 숨은 효과 1줄, 3회 = 핵심 효과 1줄, 상한이 4·5인 카드는 4회·5회에 유틸 효과 1줄씩.
    ///   매 선택의 기본 효과 누적(CardSystem.Take)은 그대로 두고 그 위에 단계 줄을 한 번씩 더한다(2/3배 조정·상한 변경 없음).
    /// - 단계 줄은 모두 기존 능력치(PlayerStats.Apply)라 실제 계산이 기존 카드 효과와 같은 경로·같은 상한을 쓴다
    ///   (받는 피해 감소 합계 25%, 게이지 필요량 하한 50%, 이동 속도 상한 10). 무기 전용 능력치(연속 타격·관통·화염·여운 Lv)는 쓰지 않아
    ///   최종 형태·히든 조건(카드 중첩 수로만 판정)과 분리된다 — 단계 줄은 중첩 수를 바꾸지 않는다.
    /// - 원두 획득(BeanGainPercent)은 런 시작 때 게이지에 고정돼 런 중 가산이 반영되지 않으므로 쓰지 않는다.
    /// - 저장 없음: 단계는 그 런의 카드 중첩 수에서 바로 정해진다(재시작하면 중첩과 함께 0).
    /// </summary>
    public static class TeaCardStages
    {
        static TeaCardStage S(int stack, TeaStageKind kind, CardEffectType type, float value) =>
            new TeaCardStage { stack = stack, kind = kind, effect = new CardEffect(type, value) };

        const TeaStageKind H = TeaStageKind.Hidden, C = TeaStageKind.Core, U = TeaStageKind.Utility;
        const CardEffectType Dmg = CardEffectType.DamagePercent, Spd = CardEffectType.AttackSpeedPercent, Rng = CardEffectType.RangePercent,
            Move = CardEffectType.MoveSpeedPercent, Hp = CardEffectType.MaxHpFlat, Gauge = CardEffectType.GaugeNeedPercent,
            Regen = CardEffectType.RegenBonus, Guard = CardEffectType.DamageReduction, Pick = CardEffectType.PickupRadiusFlat;

        /// <summary>카드 id → 단계 줄(2..상한). 상한은 카드 데이터(maxStacks) 그대로 — TeaCardStageTests가 18종 모두 맞는지 확인한다.</summary>
        static readonly Dictionary<string, TeaCardStage[]> table = new Dictionary<string, TeaCardStage[]>
        {
            // 상한 5 (일반 7 · 희귀 2)
            ["green_tea"] = new[] { S(2, H, Spd, 0.08f), S(3, C, Dmg, 0.10f), S(4, U, Pick, 0.3f), S(5, U, Gauge, -0.04f) },
            ["dried_leaf"] = new[] { S(2, H, Pick, 0.4f), S(3, C, Spd, 0.08f), S(4, U, Move, 0.04f), S(5, U, Gauge, -0.04f) },
            ["honey"] = new[] { S(2, H, Hp, 10f), S(3, C, Guard, 0.04f), S(4, U, Pick, 0.3f), S(5, U, Regen, 0.5f) },
            ["long_spout"] = new[] { S(2, H, Dmg, 0.06f), S(3, C, Spd, 0.10f), S(4, U, Pick, 0.3f), S(5, U, Move, 0.04f) },
            ["tea_sandals"] = new[] { S(2, H, Guard, 0.03f), S(3, C, Spd, 0.10f), S(4, U, Pick, 0.4f), S(5, U, Hp, 15f) },
            ["tea_whisk"] = new[] { S(2, H, Move, 0.04f), S(3, C, Dmg, 0.10f), S(4, U, Gauge, -0.03f), S(5, U, Pick, 0.3f) },
            ["warm_cup"] = new[] { S(2, H, Regen, 0.3f), S(3, C, Guard, 0.05f), S(4, U, Hp, 10f), S(5, U, Pick, 0.3f) },
            ["chamomile"] = new[] { S(2, H, Hp, 15f), S(3, C, Guard, 0.04f), S(4, U, Move, 0.04f), S(5, U, Pick, 0.4f) },
            ["nettle_tea"] = new[] { S(2, H, Dmg, 0.06f), S(3, C, Spd, 0.10f), S(4, U, Pick, 0.3f), S(5, U, Regen, 0.3f) },
            // 상한 4 (희귀 2)
            ["brew_rhythm"] = new[] { S(2, H, Move, 0.05f), S(3, C, Dmg, 0.12f), S(4, U, Pick, 0.3f) },
            ["oolong"] = new[] { S(2, H, Hp, 15f), S(3, C, Spd, 0.12f), S(4, U, Gauge, -0.03f) },
            // 상한 3 (희귀 2 · 영웅 5)
            ["second_infusion"] = new[] { S(2, H, Dmg, 0.08f), S(3, C, Guard, 0.05f) },
            ["tea_leaf_scatter"] = new[] { S(2, H, Dmg, 0.08f), S(3, C, Rng, 0.12f) },
            ["aged_puerh"] = new[] { S(2, H, Hp, 20f), S(3, C, Spd, 0.15f) },
            ["ginseng_root"] = new[] { S(2, H, Guard, 0.04f), S(3, C, Hp, 30f) },
            ["golden_ratio"] = new[] { S(2, H, Move, 0.06f), S(3, C, Dmg, 0.12f) },
            ["nightshade_blend"] = new[] { S(2, H, Spd, 0.08f), S(3, C, Dmg, 0.12f) },
            ["piercing_arrowhead"] = new[] { S(2, H, Dmg, 0.08f), S(3, C, Rng, 0.12f) },
        };

        static readonly TeaCardStage[] none = new TeaCardStage[0];

        public static IEnumerable<string> CardIds => table.Keys;

        /// <summary>이 직업·카드에 단계 효과가 붙는지(티 소믈리에 + 차 카드 + 표에 있음).</summary>
        public static bool Applies(Job job, CardData card) =>
            job == Job.TeaSommelier && card != null && card.category == CardCategory.Tea && table.ContainsKey(card.id);

        /// <summary>카드의 단계 줄 전체(직업 무관 — 도감 표시용). 없으면 빈 배열.</summary>
        public static IReadOnlyList<TeaCardStage> For(string cardId) =>
            cardId != null && table.TryGetValue(cardId, out var s) ? s : none;

        /// <summary>정확히 stack회째에 더해지는 단계 줄(없으면 false — 1회째·표 밖·상한 밖).</summary>
        public static bool TryGet(string cardId, int stack, out TeaCardStage stage)
        {
            foreach (var s in For(cardId)) if (s.stack == stack) { stage = s; return true; }
            stage = default;
            return false;
        }

        /// <summary>CardSystem.Take로 중첩이 newStacks가 된 직후 부른다. 티 소믈리에 + 차 카드에서 그 단계 줄이 있으면 한 번 적용.
        /// 같은 중첩에서 두 번 부르지 않게 호출자는 실제로 중첩이 늘었을 때만 부른다. 적용했으면 true.</summary>
        public static bool ApplyOnTake(Job job, CardData card, int newStacks, PlayerStats stats)
        {
            if (stats == null || !Applies(job, card) || newStacks > card.maxStacks) return false;
            if (!TryGet(card.id, newStacks, out var stage)) return false;
            stats.Apply(stage.effect);
            return true;
        }

        public static string KindLabel(TeaStageKind k) => k == TeaStageKind.Hidden ? "숨은 효과" : k == TeaStageKind.Core ? "핵심 효과" : "유틸 효과";

        /// <summary>"2회 숨은 효과 · 공격 속도 +8%" — 카드 창·도감 공용 한 줄.</summary>
        public static string Line(TeaCardStage s) => $"{s.stack}회 {KindLabel(s.kind)} · {StatStyle.LineText(s.effect)}";

        /// <summary>카드 창: 지금 중첩 currentStacks에서 이 카드를 고르면 새로 더해지는 단계 줄(없으면 "").</summary>
        public static string NextLine(Job job, CardData card, int currentStacks)
        {
            if (!Applies(job, card) || currentStacks + 1 > card.maxStacks) return "";
            return TryGet(card.id, currentStacks + 1, out var s) ? Line(s) : "";
        }

        /// <summary>도감(발견한 차 카드 상세만): 티 소믈리에 단계 효과 전체.</summary>
        public static string CodexText(CardData card)
        {
            if (card == null || card.category != CardCategory.Tea) return "";
            var stages = For(card.id);
            if (stages.Count == 0) return "";
            var sb = new System.Text.StringBuilder("티 소믈리에 획득 단계 (같은 카드를 여러 번 고를 때 · 기본 효과는 매번 그대로 누적)\n1회 기본 효과");
            foreach (var s in stages) sb.Append('\n').Append(Line(s));
            return sb.ToString();
        }
    }
}
