using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 총 전용 탄환 유도. 사거리 카드(바 스푼/긴 주둥이 주전자)를 총으로 고르면 사거리 대신 이 단계가 오른다(카드 1장 = 1단계, 최대 5).
    /// 런타임(PlayerProjectile)과 설명 글(Describe)이 같은 상수를 읽는다. 유도 0단계 탄은 기존처럼 직진한다.
    /// 선회는 "초당 각도 × 프레임 시간"으로만 일어나 프레임 간격과 무관하게 같은 시간에 같은 각도를 돈다(순간 꺾임 없음).
    /// </summary>
    public static class GunHoming
    {
        public const int MaxLevel = 5;
        /// <summary>단계당 초당 최대 선회각(°/s). 1단계 60 … 5단계 300.</summary>
        public const float TurnDegPerSecondPerLevel = 60f;
        /// <summary>표적 탐지 반경(월드). 탄 위치 기준.</summary>
        public const float DetectRadius = 4f;
        /// <summary>표적을 놓는 반경 배수(탐지 반경 × 이 값 밖이면 포기).</summary>
        public const float LoseRadiusScale = 1.5f;
        const float ConeBaseDeg = 25f, ConePerLevelDeg = 10f;
        const float BudgetBaseDeg = 30f, BudgetPerLevelDeg = 30f;
        public const float ScanInterval = 0.1f;

        public static int Clamp(int level) => Mathf.Clamp(level, 0, MaxLevel);
        public static float TurnRate(int level) => Clamp(level) * TurnDegPerSecondPerLevel;
        /// <summary>탄 진행 방향 기준 좌우 반각. 이 안의 적만 새 표적이 된다.</summary>
        public static float ConeHalfDeg(int level) => level <= 0 ? 0f : ConeBaseDeg + ConePerLevelDeg * Clamp(level);
        /// <summary>한 발이 평생 돌 수 있는 총 각도(무한 추적·궤도 맴돌기 방지). 탄 수명(2초)도 따로 상한이다.</summary>
        public static float BudgetDeg(int level) => level <= 0 ? 0f : BudgetBaseDeg + BudgetPerLevelDeg * Clamp(level);

        /// <summary>한 프레임 선회. dir을 toTarget 쪽으로 최대 TurnRate·dt(남은 예산 이내)만 돌린다. budget은 쓴 만큼 줄어든다.</summary>
        public static Vector2 Steer(Vector2 dir, Vector2 toTarget, int level, float dt, ref float budgetDeg)
        {
            if (level <= 0 || dt <= 0f || budgetDeg <= 0f || toTarget.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-6f) return dir;
            float want = Vector2.SignedAngle(dir, toTarget);
            float step = Mathf.Min(Mathf.Abs(want), TurnRate(level) * dt, budgetDeg);
            budgetDeg -= step;
            return (Vector2)(Quaternion.Euler(0f, 0f, Mathf.Sign(want) * step) * dir);
        }

        /// <summary>새 표적 후보 적합성: 진행 방향 원뿔 안 + 탐지 반경 안.</summary>
        public static bool InCone(Vector2 dir, Vector2 toTarget, int level) =>
            level > 0 && toTarget.sqrMagnitude <= DetectRadius * DetectRadius && Mathf.Abs(Vector2.Angle(dir, toTarget)) <= ConeHalfDeg(level);

        /// <summary>카드 선택 창 문구(수치 없이 동작 중심). nextStack = 이 카드를 고른 뒤의 중첩(1부터).</summary>
        public static string Describe(int nextStack) =>
            nextStack <= 1 ? "탄환이 움직이는 적을 따라갑니다" : "탄환이 적을 더 빠르게 따라갑니다";
    }

    /// <summary>카드 효과를 무기에 맞게 보여 주는 읽기 전용 변환(데이터·ID·세이브는 그대로). 총이면 사거리 효과 → 탄환 유도 +1단계.</summary>
    public static class CardEffectsView
    {
        public static CardEffect[] For(CardEffect[] effects, bool gun)
        {
            if (effects == null || !gun) return effects;
            var r = new CardEffect[effects.Length];
            for (int i = 0; i < effects.Length; i++)
                r[i] = effects[i].type == CardEffectType.RangePercent ? new CardEffect(CardEffectType.BulletHoming, 1f) : effects[i];
            return r;
        }

        public static bool IsRangeCard(CardData card)
        {
            if (card == null || card.effects == null) return false;
            foreach (var e in card.effects) if (e.type == CardEffectType.RangePercent) return true;
            return false;
        }
    }
}
