using System;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 재료 게이지. 재료 종류별로 인스턴스를 하나씩 쓴다 (기획서 13장).
    /// 임계치는 레벨이 오를수록 증가 (가안: 10 → 15 → 22 → 33 …).
    /// </summary>
    public class IngredientGauge
    {
        readonly float growth;
        readonly Func<float> needMultiplier;
        float baseThreshold;

        public IngredientType Type { get; }
        public int Level { get; private set; } = 1;
        public int Current { get; private set; }

        /// <summary>레벨업 1회당 한 번씩 호출된다.</summary>
        public event Action<IngredientGauge> LeveledUp;

        public IngredientGauge(IngredientType type, float startThreshold = 10f, float growth = 1.5f,
            Func<float> needMultiplier = null)
        {
            Type = type;
            baseThreshold = startThreshold;
            this.growth = growth;
            this.needMultiplier = needMultiplier;
        }

        public int Threshold =>
            Mathf.Max(3, Mathf.RoundToInt(baseThreshold * (needMultiplier != null ? needMultiplier() : 1f)));

        public float Fill01 => Mathf.Clamp01((float)Current / Threshold);

        /// <returns>이번 호출로 발생한 레벨업 횟수</returns>
        public int Add(int amount)
        {
            if (amount <= 0) return 0;

            Current += amount;
            int levelUps = 0;
            while (Current >= Threshold)
            {
                Current -= Threshold;
                baseThreshold = Mathf.Floor(baseThreshold * growth);
                Level++;
                levelUps++;
                LeveledUp?.Invoke(this);
            }
            return levelUps;
        }
    }
}
