using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>06:98 — 런의 기본 시드에서 파생된 카드/스폰/품종/보스 위치 스트림은 서로 독립적이고,
    /// 같은 시드면 같은 순서로 같은 값을 내야 한다.</summary>
    public class RunRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence_ForEachStream()
        {
            var a = new RunRandom(12345);
            var b = new RunRandom(12345);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.Spawns.Value(), b.Spawns.Value(), 0.00001f);
                Assert.AreEqual(a.Varieties.Value(), b.Varieties.Value(), 0.00001f);
                Assert.AreEqual(a.BossPositions.Value(), b.BossPositions.Value(), 0.00001f);
                Assert.AreEqual(a.Cards.Next(), b.Cards.Next());
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new RunRandom(1);
            var b = new RunRandom(2);

            bool anyDifferent = false;
            for (int i = 0; i < 10; i++)
            {
                if (!UnityEngine.Mathf.Approximately(a.Spawns.Value(), b.Spawns.Value())) anyDifferent = true;
            }
            Assert.IsTrue(anyDifferent, "다른 시드는 사실상 항상 다른 값을 내야 한다");
        }

        [Test]
        public void StreamsAreIndependent_ConsumingOneDoesNotAffectAnother()
        {
            // 시드가 같으면, Spawns를 아무리 많이 소비해도 Varieties의 다음 값은 그대로여야 한다.
            var untouched = new RunRandom(999);
            float expectedFirstVariety = untouched.Varieties.Value();

            var heavilyUsed = new RunRandom(999);
            for (int i = 0; i < 50; i++) heavilyUsed.Spawns.Value();
            float actualFirstVariety = heavilyUsed.Varieties.Value();

            Assert.AreEqual(expectedFirstVariety, actualFirstVariety, 0.00001f,
                "06:98 — 스트림은 서로 독립적이어야 한다(한쪽을 소비해도 다른 쪽 순서에 영향 없음)");
        }

        [Test]
        public void RangeInt_StaysWithinBounds()
        {
            var rng = new SeededRandomSource(42);
            for (int i = 0; i < 200; i++)
            {
                int v = rng.RangeInt(6);
                Assert.GreaterOrEqual(v, 0);
                Assert.Less(v, 6);
            }
        }
    }
}
