using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>기획서 10장 순서(중간보스 A → B → 강화 보스 → 진상 손님)대로 스케줄이 짜였는지 확인.</summary>
    public class BossDirectorScheduleTests
    {
        [Test]
        public void DefaultSchedule_FollowsDesignOrder_AndTimesAreIncreasing()
        {
            var schedule = BossDirector.DefaultSchedule();

            Assert.AreEqual(4, schedule.Count);
            Assert.AreEqual(EnemyDef.GrinderGolem, schedule[0].def);
            Assert.AreEqual(EnemyDef.DripServerSpecter, schedule[1].def);
            Assert.AreEqual(EnemyDef.EspressoLeviathan, schedule[2].def);
            Assert.AreEqual(EnemyDef.DifficultCustomer, schedule[3].def);

            for (int i = 1; i < schedule.Count; i++)
                Assert.Greater(schedule[i].time, schedule[i - 1].time, $"index {i} 시간이 앞보다 늦어야 한다");
        }

        [Test]
        public void DefaultSchedule_BossKindsMatchDesign()
        {
            var schedule = BossDirector.DefaultSchedule();

            Assert.AreEqual(BossKind.Mid, schedule[0].def.boss);
            Assert.AreEqual(BossKind.Mid, schedule[1].def.boss);
            Assert.AreEqual(BossKind.Enhanced, schedule[2].def.boss);
            Assert.AreEqual(BossKind.Final, schedule[3].def.boss);
        }
    }
}
