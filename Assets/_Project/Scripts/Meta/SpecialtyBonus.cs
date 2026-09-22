using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 스페셜티(영구 재화) 포인트를 실제 보너스 수치로 바꾸는 순수 함수 모음.
    /// 기획서 11장의 경고("영구 강화가 무제한이면 후반 난이도가 무너짐")에 따라 전부 상한을 둔다.
    /// </summary>
    public static class SpecialtyBonus
    {
        /// <summary>영구 공격력 % 보너스. 포인트당 1.5%, 상한 30%.</summary>
        public static float DamagePercent(int points) =>
            Mathf.Min(Balance.SpecialtyDamagePercentCap, Mathf.Max(0, points) * Balance.SpecialtyDamagePercentPerPoint);

        /// <summary>커피 원두 획득량 배율. 포인트당 +3%, 상한 +50% (배율 1.5).</summary>
        public static float BeanGainMultiplier(int points) =>
            1f + Mathf.Min(Balance.SpecialtyBeanBonusCap, Mathf.Max(0, points) * Balance.SpecialtyBeanBonusPerPoint);
    }
}
