using System;

namespace CCT
{
    /// <summary>One run's applied progression; never modifies shared enemy definitions.</summary>
    public sealed class RunChallenge
    {
        public int PermanentCount { get; }
        public int CoffeeCount { get; private set; }
        public long Stage => (long)PermanentCount + CoffeeCount;
        public float HpMultiplier => HpFor(PermanentCount, CoffeeCount);
        public float DamageMultiplier => DamageFor(PermanentCount, CoffeeCount);
        public event Action<float, float> HpMultiplierChanged;

        /// <summary>40 — 무기 무관 유효 영구 해금 총수 N(일반 스폰량 전용, 체력/피해의 P와 별개). 런 시작 시 고정.</summary>
        public int PermanentUnlockTotal { get; }
        public float SpawnMultiplier => SpawnMultiplierFor(PermanentUnlockTotal);
        public int RegularAliveCap => RegularAliveCapFor(PermanentUnlockTotal);

        public RunChallenge(int permanentCount, int permanentUnlockTotal = 0)
        {
            PermanentCount = Math.Max(0, permanentCount);
            PermanentUnlockTotal = Math.Max(0, permanentUnlockTotal);
        }
        /// <summary>정수 %p로 계산해 부동소수 반올림 없이 1+min(.60,.03N)과 floor(100×배율)을 정확히 맞춘다.</summary>
        public static int SpawnBonusPercentFor(int unlockTotal) =>
            (int)Math.Min(Balance.SpawnBonusPercentMax, (long)Balance.SpawnBonusPercentPerUnlock * Math.Max(0, unlockTotal));
        public static float SpawnMultiplierFor(int unlockTotal) => 1f + SpawnBonusPercentFor(unlockTotal) / 100f;
        public static int RegularAliveCapFor(int unlockTotal) =>
            Math.Min(Balance.MaxAliveEnemiesAbsolute, Balance.MaxAliveEnemies * (100 + SpawnBonusPercentFor(unlockTotal)) / 100);
        public static float HpFor(int permanent, int coffee) =>
            1f + (float)Math.Min(.40, .010 * Math.Max(0, permanent) + .012 * Math.Max(0, coffee));
        public static float DamageFor(int permanent, int coffee) =>
            1f + (float)Math.Min(.18, .004 * Math.Max(0, permanent) + .004 * Math.Max(0, coffee));
        public float NextCoffeeHp => HpFor(PermanentCount, CoffeeCount == int.MaxValue ? CoffeeCount : CoffeeCount + 1);
        public float NextCoffeeDamage => DamageFor(PermanentCount, CoffeeCount == int.MaxValue ? CoffeeCount : CoffeeCount + 1);

        public void SetCoffeeCount(int successfulUnlocks)
        {
            int next = Math.Max(0, successfulUnlocks);
            if (next == CoffeeCount) return;
            float previous = HpMultiplier;
            CoffeeCount = next;
            float current = HpMultiplier;
            if (current != previous) HpMultiplierChanged?.Invoke(previous, current);
        }
    }
}
