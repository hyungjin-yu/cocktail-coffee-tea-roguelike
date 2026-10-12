using System;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 07 §2 "커피 변경" — 원두 게이지. 칵테일/차(IngredientGauge)와 달리 임계치가 자라지 않는다:
    /// 원두 10개마다 1포인트, Level=1+누적 발생 포인트, 잔량 0..9. 발생 포인트는 전부 보존한다(캡핑 없음).
    /// 원두 획득 보너스는 런 누적 원두 원량 R에 대해 E=floor(R×(1+보너스))를 계산하고, 이번 지급은
    /// 새 E−이전 E다 — 픽업마다 반올림하지 않으므로 같은 총량을 어떻게 나눠 주워도 결과가 같다.
    /// </summary>
    public class CoffeeBeanGauge
    {
        /// <summary>보너스를 1/10000 단위 정수로 바꿔 E를 정수 연산으로 구한다(float 곱셈+floor가
        /// 80×1.2=95.99… 처럼 경계에서 밀리는 일을 원천 차단).</summary>
        const long BonusScale = 10000;

        readonly long bonusBasisPoints;

        public IngredientType Type => IngredientType.Coffee;
        /// <summary>런 누적 원두 원량 R (보너스 전, 실제로 주운 양).</summary>
        public int RawTotal { get; private set; }
        /// <summary>E=floor(R×(1+보너스)) — 실제 게이지에 들어간 누적량(도감 집계도 이 값).</summary>
        public int EffectiveTotal { get; private set; }
        /// <summary>이번 런에서 발생한 포인트 총수(사용 여부 무관).</summary>
        public int PointsEarned { get; private set; }
        public int Level => 1 + PointsEarned;
        public int Current { get; private set; }
        public int Threshold => Balance.BeanPointThreshold;
        public float Fill01 => Mathf.Clamp01((float)Current / Threshold);

        /// <summary>포인트가 1개 이상 발생할 때마다 1회 호출된다. 인자는 이번에 발생한 포인트 수.</summary>
        public event Action<int> PointsGained;

        public CoffeeBeanGauge(float beanGainPercent = 0f)
        {
            bonusBasisPoints = (long)Math.Round(beanGainPercent * BonusScale);
        }

        /// <param name="raw">주운 원두 원량(보너스 전).</param>
        /// <returns>이번 호출로 발생한 포인트 수.</returns>
        public int AddRaw(int raw)
        {
            if (raw <= 0) return 0;

            RawTotal += raw;
            int newEffective = (int)((long)RawTotal * (BonusScale + bonusBasisPoints) / BonusScale);
            int delta = newEffective - EffectiveTotal;
            EffectiveTotal = newEffective;

            Current += delta;
            int points = Current / Threshold;
            Current -= points * Threshold;
            if (points > 0)
            {
                PointsEarned += points;
                PointsGained?.Invoke(points);
            }
            return points;
        }
    }
}
