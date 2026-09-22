using System.Collections.Generic;
using NUnit.Framework;

namespace CCT.Tests
{
    public class EnemyRosterTests
    {
        [Test]
        public void EarlyGame_OnlyImpAppears()
        {
            for (int i = 0; i < 200; i++)
                Assert.AreSame(EnemyDef.Imp, EnemyRoster.PickCommon(0f));
        }

        [Test]
        public void ShakerGolem_AppearsAfter15Seconds_ButNotLaterTypes()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 500; i++) seen.Add(EnemyRoster.PickCommon(20f));

            Assert.IsTrue(seen.Contains(EnemyDef.Imp));
            Assert.IsTrue(seen.Contains(EnemyDef.ShakerGolem));
            Assert.IsFalse(seen.Contains(EnemyDef.Brute), "45초 전에는 브루트가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.OliveBomber), "60초 전에는 바머가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.SwirlConjuror), "90초 전에는 컨저러가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.MartiniSpirit), "120초 전에는 마티니 스피릿이 나오면 안 된다");
        }

        [Test]
        public void Brute_AppearsAfter45Seconds()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 800; i++) seen.Add(EnemyRoster.PickCommon(50f));
            Assert.IsTrue(seen.Contains(EnemyDef.Brute));
            Assert.IsFalse(seen.Contains(EnemyDef.OliveBomber), "60초 전에는 바머가 나오면 안 된다");
        }

        [Test]
        public void AllSixTypes_CanAppear_AfterOneHundredTwentySeconds()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 6000; i++) seen.Add(EnemyRoster.PickCommon(125f));

            Assert.IsTrue(seen.Contains(EnemyDef.Imp));
            Assert.IsTrue(seen.Contains(EnemyDef.ShakerGolem));
            Assert.IsTrue(seen.Contains(EnemyDef.Brute));
            Assert.IsTrue(seen.Contains(EnemyDef.OliveBomber));
            Assert.IsTrue(seen.Contains(EnemyDef.SwirlConjuror));
            Assert.IsTrue(seen.Contains(EnemyDef.MartiniSpirit), "가중치가 낮아도 6000번이면 한 번은 나와야 한다");
        }

        [Test]
        public void AttachBehavior_GivesRangedAndSummonerDefsTheirDriver_ButNotRush()
        {
            var parent = new UnityEngine.GameObject("Parent").transform;

            var rush = Enemy.SpawnAt(UnityEngine.Vector2.zero, EnemyDef.Imp, 1f, null, parent);
            EnemyRoster.AttachBehavior(rush, null, null);
            Assert.IsNull(rush.GetComponent<RangedAttackDriver>());
            Assert.IsNull(rush.GetComponent<SummonerDriver>());

            var ranged = Enemy.SpawnAt(UnityEngine.Vector2.zero, EnemyDef.ShakerGolem, 1f, null, parent);
            EnemyRoster.AttachBehavior(ranged, null, null);
            Assert.IsNotNull(ranged.GetComponent<RangedAttackDriver>());

            var elite = Enemy.SpawnAt(UnityEngine.Vector2.zero, EnemyDef.MartiniSpirit, 1f, null, parent);
            EnemyRoster.AttachBehavior(elite, null, null);
            Assert.IsNotNull(elite.GetComponent<RangedAttackDriver>());

            var summoner = Enemy.SpawnAt(UnityEngine.Vector2.zero, EnemyDef.SwirlConjuror, 1f, null, parent);
            EnemyRoster.AttachBehavior(summoner, null, null);
            Assert.IsNotNull(summoner.GetComponent<SummonerDriver>());

            UnityEngine.Object.DestroyImmediate(parent.gameObject);
        }
    }
}
