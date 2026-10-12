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
        public void CoffeeTrack_NeverAppears_BeforeUnlockTime()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 12000; i++) seen.Add(EnemyRoster.PickCommon(125f));

            Assert.IsFalse(seen.Contains(EnemyDef.EspressoBug), "150초 전에는 에스프레소 벅이 나오면 안 된다");
            Assert.IsFalse(seen.Contains(EnemyDef.MilkFoamer), "150초 전에는 밀크 포머가 나오면 안 된다");
        }

        [Test]
        public void CoffeeTrack_AppearsAfterUnlockTime_AlongsideCocktailAndTea()
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 4000; i++) seen.Add(EnemyRoster.PickCommon(150f));

            Assert.IsTrue(seen.Contains(EnemyDef.EspressoBug));
            Assert.IsTrue(seen.Contains(EnemyDef.MilkFoamer));
            Assert.IsTrue(seen.Contains(EnemyDef.Imp), "커피가 섞여도 칵테일 트랙은 계속 나와야 한다");
            Assert.IsTrue(seen.Contains(EnemyDef.TeabagImp), "커피가 섞여도 차 트랙은 계속 나와야 한다");
        }

        /// <summary>첫 호출들은 정해 둔 값을, 그 뒤(트랙 안 아키타입 추첨)는 0.5를 돌려준다.</summary>
        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<float> values;
            public ScriptedRandom(params float[] v) => values = new Queue<float>(v);
            public float Value() => values.Count > 0 ? values.Dequeue() : 0.5f;
        }

        static IngredientType TrackOf(float runTime, float roll) =>
            EnemyRoster.PickCommon(runTime, new ScriptedRandom(roll)).dropType;

        [Test]
        public void CoffeeShare_IsTwoPercent_CocktailAndTea49Each_AfterUnlock()
        {
            // 2026-10-06 기획 변경(02/07/27): 150초 이후 칵테일 0.49 / 차 0.49 / 커피 0.02.
            Assert.AreEqual(0.49f, EnemyRoster.CocktailShareAfterCoffee, 1e-6f);
            Assert.AreEqual(0.49f, EnemyRoster.TeaShareAfterCoffee, 1e-6f);
            Assert.AreEqual(0.02f, EnemyRoster.CoffeeShareAfterCoffee, 1e-5f);
            Assert.AreEqual(150f, EnemyRoster.CoffeeTrackUnlockTime);
        }

        [Test]
        public void TrackPartition_Boundaries_AtAndAfterUnlock()
        {
            foreach (float t in new[] { 150f, 300f, 680f, 1199f })
            {
                Assert.AreEqual(IngredientType.Cocktail, TrackOf(t, 0f), $"t={t} roll 0");
                Assert.AreEqual(IngredientType.Cocktail, TrackOf(t, 0.4899f), $"t={t} roll .4899");
                Assert.AreEqual(IngredientType.Tea, TrackOf(t, 0.4901f), $"t={t} roll .4901");
                Assert.AreEqual(IngredientType.Tea, TrackOf(t, 0.9799f), $"t={t} roll .9799");
                Assert.AreEqual(IngredientType.Coffee, TrackOf(t, 0.9801f), $"t={t} roll .9801");
                Assert.AreEqual(IngredientType.Coffee, TrackOf(t, 0.99999f), $"t={t} roll .99999");
            }
        }

        [Test]
        public void TrackPartition_BeforeUnlock_StaysFiftyFifty_NoCoffee()
        {
            // 06 §4: 150초 전에는 칵테일/차 50:50(첫 roll<0.5면 차).
            Assert.AreEqual(IngredientType.Tea, TrackOf(149.99f, 0.4999f));
            Assert.AreEqual(IngredientType.Cocktail, TrackOf(149.99f, 0.5001f));
            Assert.AreEqual(IngredientType.Cocktail, TrackOf(149.99f, 0.99999f), "150초 전에는 높은 roll도 커피가 아니다");
        }

        [Test]
        public void SeededSample_MatchesShares_AfterUnlock()
        {
            var rng = new SeededRandomSource(20261006);
            int cocktail = 0, tea = 0, coffee = 0;
            const int n = 40000;
            for (int i = 0; i < n; i++)
            {
                switch (EnemyRoster.PickCommon(300f, rng).dropType)
                {
                    case IngredientType.Cocktail: cocktail++; break;
                    case IngredientType.Tea: tea++; break;
                    case IngredientType.Coffee: coffee++; break;
                }
            }
            Assert.AreEqual(n, cocktail + tea + coffee);
            Assert.AreEqual(0.02, coffee / (double)n, 0.004, "커피 비중 2%");
            Assert.AreEqual(0.49, cocktail / (double)n, 0.012, "칵테일 비중 49%");
            Assert.AreEqual(0.49, tea / (double)n, 0.012, "차 비중 49%");
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
