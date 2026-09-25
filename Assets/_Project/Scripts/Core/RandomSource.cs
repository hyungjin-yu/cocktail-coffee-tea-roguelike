using UnityEngine;

namespace CCT
{
    /// <summary>0~1 사이 실수 난수 하나를 주는 최소 인터페이스. 06:98 — 카드/스폰/품종/보스 위치는
    /// 시드에서 파생된 독립 스트림을 쓰고, VFX 같은 순수 시각 요소는 이 인터페이스를 거치지 않고
    /// 기존 UnityEngine.Random(전역)을 그대로 써서 게임 난수 소비량에 영향을 주지 않는다.</summary>
    public interface IRandomSource
    {
        /// <summary>[0,1) 균등분포.</summary>
        float Value();
    }

    /// <summary>기존 UnityEngine.Random(전역) 어댑터. 시드를 명시적으로 주입하지 않는 곳(기존
    /// 테스트 등)의 기본값 — 동작이 이전과 완전히 동일하다.</summary>
    public sealed class UnityRandomSource : IRandomSource
    {
        public static readonly UnityRandomSource Instance = new UnityRandomSource();
        public float Value() => Random.value;
    }

    /// <summary>System.Random 기반 독립 스트림. 같은 시드로 만들면 항상 같은 순서로 같은 값을 낸다.</summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        readonly System.Random rng;
        public SeededRandomSource(int seed) => rng = new System.Random(seed);
        public float Value() => (float)rng.NextDouble();
    }

    public static class RandomSourceExtensions
    {
        /// <summary>[0, maxExclusive) 정수. maxExclusive&lt;=0이면 0.</summary>
        public static int RangeInt(this IRandomSource rng, int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(rng.Value() * maxExclusive), 0, maxExclusive - 1);
        }
    }

    /// <summary>
    /// 런 하나의 기본 시드와, 거기서 파생된 카드/스폰/품종/보스 위치용 독립 난수 스트림 (06:98).
    /// 같은 시드로 만들면 (카드 뽑기처럼) 입력 순서가 같은 한 같은 결과를 낸다. 물리 시뮬레이션까지
    /// 완전 동일한 리플레이를 보장하지는 않는다 — 스펙이 요구하는 범위도 딱 그만큼이다.
    /// </summary>
    public class RunRandom
    {
        public readonly int Seed;
        /// <summary>CardSystem이 System.Random을 직접 받으므로 그대로 넘긴다.</summary>
        public readonly System.Random Cards;
        public readonly IRandomSource Spawns;
        public readonly IRandomSource Varieties;
        public readonly IRandomSource BossPositions;

        public RunRandom(int seed)
        {
            Seed = seed;
            Cards = new System.Random(Derive(seed, 1));
            Spawns = new SeededRandomSource(Derive(seed, 2));
            Varieties = new SeededRandomSource(Derive(seed, 3));
            BossPositions = new SeededRandomSource(Derive(seed, 4));
        }

        static int Derive(int seed, int salt) => unchecked(seed * 397 ^ salt);

        /// <summary>런 시작마다 새 32비트 시드를 만든다. 전역 UnityEngine.Random 상태와는 무관하다.</summary>
        public static int GenerateSeed() =>
            unchecked((int)System.DateTime.UtcNow.Ticks ^ System.Guid.NewGuid().GetHashCode());
    }
}
