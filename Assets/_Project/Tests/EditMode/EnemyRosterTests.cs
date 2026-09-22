using System.Collections.Generic;
using NUnit.Framework;

namespace CCT.Tests
{
    public class EnemyRosterTests
    {
        [Test]
        public void EarlyGame_OnlyRushTypesAppear()
        {
            // 칵테일/차 50:50 이라 초반엔 각 트랙의 돌진형(라임웨지/티백 임프)만 나와야 한다
            for (int i = 0; i < 200; i++)
            {
                var def = EnemyRoster.PickCommon(0f);
                Assert.IsTrue(def == EnemyDef.Imp || def == EnemyDef.TeabagImp,
                    $"기대: 라임웨지 또는 티백 임프, 실제: {def.name}");
            }
        }

        [Test]
        public void RangedTypes_AppearAfter15Seconds_ButNotLaterTypes()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 1000; i++) seen.Add(EnemyRoster.PickCommon(20f));

            Assert.IsTrue(seen.Contains(EnemyDef.Imp));
            Assert.IsTrue(seen.Contains(EnemyDef.TeabagImp));
            Assert.IsTrue(seen.Contains(EnemyDef.ShakerGolem));
            Assert.IsTrue(seen.Contains(EnemyDef.KettleCannon));
            Assert.IsFalse(seen.Contains(EnemyDef.Brute), "45초 전에는 브루트가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.OliveBomber), "60초 전에는 바머가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.SugarCubeBomber), "60초 전에는 슈가 큐브 바머가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.SwirlConjuror), "90초 전에는 컨저러가 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.MartiniSpirit), "120초 전에는 마티니 스피릿이 나오면 안 된다");
        }

        [Test]
        public void Brute_AppearsAfter45Seconds_ButHasNoTeaEquivalent()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 1600; i++) seen.Add(EnemyRoster.PickCommon(50f));
            Assert.IsTrue(seen.Contains(EnemyDef.Brute));
            Assert.IsFalse(seen.Contains(EnemyDef.OliveBomber), "60초 전에는 바머가 나오면 안 된다");
        }

        [Test]
        public void AllTypes_CanAppear_AfterOneHundredTwentySeconds()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 12000; i++) seen.Add(EnemyRoster.PickCommon(125f));

            Assert.IsTrue(seen.Contains(EnemyDef.Imp));
            Assert.IsTrue(seen.Contains(EnemyDef.ShakerGolem));
            Assert.IsTrue(seen.Contains(EnemyDef.Brute));
            Assert.IsTrue(seen.Contains(EnemyDef.OliveBomber));
            Assert.IsTrue(seen.Contains(EnemyDef.SwirlConjuror));
            Assert.IsTrue(seen.Contains(EnemyDef.MartiniSpirit), "가중치가 낮아도 이 정도 시행이면 한 번은 나와야 한다");

            Assert.IsTrue(seen.Contains(EnemyDef.TeabagImp));
            Assert.IsTrue(seen.Contains(EnemyDef.KettleCannon));
            Assert.IsTrue(seen.Contains(EnemyDef.SugarCubeBomber));
            Assert.IsTrue(seen.Contains(EnemyDef.LeafWarden));
            Assert.IsTrue(seen.Contains(EnemyDef.TeaCeremonyGuardian), "가중치가 낮아도 이 정도 시행이면 한 번은 나와야 한다");
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
