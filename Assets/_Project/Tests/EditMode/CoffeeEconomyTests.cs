using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>07 §2 "커피 변경"(2026-10-04 적용)과 보상 대기열 순서를 숫자 그대로 대조한다(G16/G17/G18).
    /// 원두 10개당 1포인트·임계치 고정·포인트 전부 보존, 누적 원량 기준 보너스(픽업별 반올림 차익 없음),
    /// 요구 감소 직후 재평가, 칵테일→차→커피 트리 순서.</summary>
    public class CoffeeEconomyTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SkillTree DefaultTree()
        {
            var nodes = DefaultSkills.CreateRuntime();
            created.AddRange(nodes);
            return new SkillTree(nodes);
        }

        static SkillData Find(SkillTree tree, string id) => tree.Nodes.Single(n => n.id == id);

        // ---- 고정 임계치 10 ----

        [Test]
        public void Threshold_IsFixedTen_AndNeverGrows()
        {
            var g = new CoffeeBeanGauge();
            Assert.AreEqual(10, g.Threshold);
            Assert.AreEqual(1, g.Level);
            for (int i = 1; i <= 12; i++)
            {
                Assert.AreEqual(1, g.AddRaw(10), $"{i}번째 10개");
                Assert.AreEqual(10, g.Threshold, "레벨이 올라도 임계치는 그대로");
                Assert.AreEqual(0, g.Current);
                Assert.AreEqual(1 + i, g.Level, "Level = 1 + 누적 발생 포인트");
            }
        }

        [Test]
        public void Boundary_NineThenOne_GivesExactlyOnePoint()
        {
            var g = new CoffeeBeanGauge();
            Assert.AreEqual(0, g.AddRaw(9));
            Assert.AreEqual(9, g.Current, "잔량은 0..9");
            Assert.AreEqual(1, g.AddRaw(1));
            Assert.AreEqual(0, g.Current);
            Assert.AreEqual(1, g.PointsEarned);
        }

        [Test]
        public void SingleLargePickup_GivesAllPoints_NoCap()
        {
            var g = new CoffeeBeanGauge();
            int fired = 0, firedPoints = 0;
            g.PointsGained += n => { fired++; firedPoints += n; };
            Assert.AreEqual(4, g.AddRaw(40), "강화 보스 원두40 → 4포인트(옛 1개 캡핑 없음)");
            Assert.AreEqual(1, fired, "한 번의 지급은 이벤트 1회");
            Assert.AreEqual(4, firedPoints);
            Assert.AreEqual(5, g.Level);
        }

        [Test]
        public void ZeroOrNegative_IsIgnored()
        {
            var g = new CoffeeBeanGauge(0.2f);
            Assert.AreEqual(0, g.AddRaw(0));
            Assert.AreEqual(0, g.AddRaw(-5));
            Assert.AreEqual(0, g.RawTotal);
            Assert.AreEqual(0, g.EffectiveTotal);
        }

        // ---- 런 표준 분량: 20+20+40 ----

        [Test]
        public void BossSequence_20_20_40_Base_GivesEightPoints()
        {
            var g = new CoffeeBeanGauge();
            Assert.AreEqual(2, g.AddRaw(20), "보스A");
            Assert.AreEqual(2, g.AddRaw(20), "보스B");
            Assert.AreEqual(4, g.AddRaw(40), "강화 보스");
            Assert.AreEqual(8, g.PointsEarned);
            Assert.AreEqual(0, g.Current);
            Assert.AreEqual(80, g.EffectiveTotal);
        }

        [Test]
        public void BossSequence_WithPermanentPlus20_GivesNinePoints_Remainder6()
        {
            var g = new CoffeeBeanGauge(0.20f);
            g.AddRaw(20);
            g.AddRaw(20);
            g.AddRaw(40);
            Assert.AreEqual(96, g.EffectiveTotal, "E=floor(80×1.2)=96");
            Assert.AreEqual(9, g.PointsEarned);
            Assert.AreEqual(6, g.Current);
            Assert.AreEqual(10, g.Level);
        }

        [Test]
        public void PermanentBeanCupper_DataValue_ProducesNinePoints()
        {
            // 실제 영구 노드 데이터(bean_cupper)를 스탯에 적용해 나온 값으로 계산 — 상수만 맞추는 테스트가 아님.
            var stats = new PlayerStats();
            var cupper = PermanentSkillDatabase.LoadAll().Single(n => n.id == "bean_cupper");
            foreach (var e in cupper.effects) stats.Apply(e);
            var g = new CoffeeBeanGauge(stats.beanGainPercent);
            g.AddRaw(80);
            Assert.AreEqual(9, g.PointsEarned);
            Assert.AreEqual(6, g.Current);
        }

        // ---- 분할 불변성(픽업별 반올림 차익 금지) ----

        static (int points, int current, int effective) Run(float bonus, IEnumerable<int> pickups)
        {
            var g = new CoffeeBeanGauge(bonus);
            foreach (var p in pickups) g.AddRaw(p);
            return (g.PointsEarned, g.Current, g.EffectiveTotal);
        }

        [Test]
        public void BonusPartitionInvariance_OneByOne_EqualsLump()
        {
            var lump = Run(0.2f, new[] { 80 });
            var ones = Run(0.2f, Enumerable.Repeat(1, 80));
            var fours = Run(0.2f, Enumerable.Repeat(4, 20));
            var bossPickups = Run(0.2f, Enumerable.Repeat(4, 5).Concat(Enumerable.Repeat(4, 5)).Concat(Enumerable.Repeat(5, 8)));
            Assert.AreEqual(lump, ones, "1개씩 80번");
            Assert.AreEqual(lump, fours, "4개씩 20번(픽업별 RoundToInt였다면 5×20=100)");
            Assert.AreEqual(lump, bossPickups, "실제 보스 픽업 단위 5×4,5×4,8×5");
        }

        [Test]
        public void BonusPartitionInvariance_AllSplitsUpTo40_MatchFloorOfCumulative()
        {
            // 원량 1..40을 여러 분할로 넣어도 E는 항상 floor(R×1.2)이고 포인트/잔량이 같다.
            var rng = new System.Random(1234);
            for (int total = 1; total <= 40; total++)
            {
                int expectedE = (int)System.Math.Floor(total * 1.2 + 1e-9);
                for (int trial = 0; trial < 20; trial++)
                {
                    var parts = new List<int>();
                    int left = total;
                    while (left > 0)
                    {
                        int p = rng.Next(1, left + 1);
                        parts.Add(p);
                        left -= p;
                    }
                    var r = Run(0.2f, parts);
                    Assert.AreEqual(expectedE, r.effective, $"R={total} split={string.Join("+", parts)}");
                    Assert.AreEqual(expectedE / 10, r.points);
                    Assert.AreEqual(expectedE % 10, r.current);
                }
            }
        }

        [Test]
        public void SmallPickups_NoRoundingArbitrage()
        {
            // 원두1을 하나씩: 옛 RoundToInt(1×1.2)=1이라 손해, RoundToInt(2×1.2)=2… 대신 누적 기준만 허용.
            var g = new CoffeeBeanGauge(0.2f);
            int[] expectedE = { 1, 2, 3, 4, 6 }; // floor(1.2),floor(2.4),floor(3.6),floor(4.8),floor(6.0)
            for (int i = 0; i < expectedE.Length; i++)
            {
                g.AddRaw(1);
                Assert.AreEqual(expectedE[i], g.EffectiveTotal, $"R={i + 1}");
            }
        }

        // ---- 포인트 보존 ----

        [Test]
        public void Points_AreAllRetained_InRewards_UntilSpent()
        {
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f);
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(cocktail, tea, beans);

            beans.AddRaw(20);
            beans.AddRaw(20);
            beans.AddRaw(40);
            Assert.AreEqual(8, rewards.UnspentPoints, "캡핑 없이 8포인트 전부 적립");
        }

        [Test]
        public void FullyUnlockedTree_KeepsPoints_AndDoesNotAutoOpen()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f, () => stats.GaugeNeedMultiplier);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f, () => stats.GaugeNeedMultiplier);
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(cocktail, tea, beans);

            beans.AddRaw(150);
            Assert.AreEqual(15, rewards.UnspentPoints);
            foreach (var n in tree.Nodes.OrderBy(n => n.tier))
                Assert.IsTrue(rewards.TryUnlockNode(tree, n, stats), n.id);
            Assert.AreEqual(0, rewards.UnspentPoints);
            Assert.IsFalse(tree.AnyAvailable());
            rewards.TakeNext(tree.AnyAvailable(), out _); // 첫 오픈 예약 소모

            beans.AddRaw(30); // 15개 전부 해금된 뒤 생긴 포인트
            Assert.AreEqual(3, rewards.UnspentPoints, "07:146 — 포인트를 보존·표시한다");
            Assert.AreEqual(RewardKind.None, rewards.TakeNext(tree.AnyAvailable(), out _), "자동창은 열지 않는다");
            Assert.AreEqual(RewardKind.None, rewards.TakeNext(tree.AnyAvailable(), out _), "무한 팝업 없음");
            Assert.AreEqual(3, rewards.UnspentPoints, "버리지 않는다");
        }

        [Test]
        public void TryUnlockNode_RequiresPointAndPrerequisite_AndCostsExactlyOne()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            var cocktail = new IngredientGauge(IngredientType.Cocktail);
            var tea = new IngredientGauge(IngredientType.Tea);
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(cocktail, tea, beans);

            Assert.IsFalse(rewards.TryUnlockNode(tree, Find(tree, "espresso_shot"), stats), "포인트 0");
            beans.AddRaw(20);
            Assert.IsFalse(rewards.TryUnlockNode(tree, Find(tree, "ristretto"), stats), "선행 없음");
            Assert.AreEqual(2, rewards.UnspentPoints, "실패는 포인트를 쓰지 않는다");
            Assert.IsTrue(rewards.TryUnlockNode(tree, Find(tree, "espresso_shot"), stats));
            Assert.IsFalse(rewards.TryUnlockNode(tree, Find(tree, "espresso_shot"), stats), "중복 해금 없음(연속 클릭)");
            Assert.AreEqual(1, rewards.UnspentPoints);
            Assert.AreEqual(0.20f, stats.damagePercent, 0.0001f, "효과 1회만 적용");
        }

        [Test]
        public void NodeStates_MatchSpec()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            Assert.AreEqual(CoffeeNodeState.NotEnoughPoints, tree.StateOf(Find(tree, "espresso_shot"), 0));
            Assert.AreEqual(CoffeeNodeState.Available, tree.StateOf(Find(tree, "espresso_shot"), 1));
            Assert.AreEqual(CoffeeNodeState.NeedsPrerequisite, tree.StateOf(Find(tree, "ristretto"), 5));
            tree.Unlock(Find(tree, "espresso_shot"), stats);
            Assert.AreEqual(CoffeeNodeState.Unlocked, tree.StateOf(Find(tree, "espresso_shot"), 0));
            Assert.AreEqual(CoffeeNodeState.NotEnoughPoints, tree.StateOf(Find(tree, "ristretto"), 0));
        }

        // ---- 자동 열림 1회 / K ----

        [Test]
        public void TreeAutoOpen_OncePerNewPoints_NotEveryFrame()
        {
            var tree = DefaultTree();
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(new IngredientGauge(IngredientType.Cocktail),
                new IngredientGauge(IngredientType.Tea), beans);

            Assert.AreEqual(RewardKind.None, rewards.TakeNext(true, out _));
            beans.AddRaw(20);
            Assert.AreEqual(RewardKind.CoffeeTree, rewards.TakeNext(true, out _), "새 포인트 → 1회");
            // "계속하기"로 닫은 뒤 매 프레임 다시 묻는다
            for (int frame = 0; frame < 10; frame++)
                Assert.AreEqual(RewardKind.None, rewards.TakeNext(true, out _), $"frame {frame}");
            Assert.AreEqual(2, rewards.UnspentPoints);

            beans.AddRaw(10);
            Assert.AreEqual(RewardKind.CoffeeTree, rewards.TakeNext(true, out _), "새 포인트가 생기면 다시 1회");

            beans.AddRaw(10);
            rewards.DisarmTreeAutoOpen(); // K로 직접 연 경우 예약 소모
            Assert.AreEqual(RewardKind.None, rewards.TakeNext(true, out _));
            Assert.AreEqual(4, rewards.UnspentPoints);
        }

        // ---- 재평가와 보상 순서 ----

        [Test]
        public void SameTick_CocktailThenTea_ThenCoffeeTree_FIFO()
        {
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f);
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(cocktail, tea, beans);

            beans.AddRaw(10);
            tea.Add(8);             // 같은 틱에 차가 먼저 들어와도
            cocktail.Add(8 + 11);   // 칵테일 2회
            // 07:43 같은 틱은 칵테일 → 차 순, 커피 포인트는 적립만 하고 트리는 카드 대기열 뒤.
            var order = new List<string>();
            while (true)
            {
                var kind = rewards.TakeNext(true, out var cat);
                if (kind == RewardKind.None) break;
                order.Add(kind == RewardKind.Card ? cat.ToString() : kind.ToString());
            }
            CollectionAssert.AreEqual(new[] { "Cocktail", "Cocktail", "Tea", "CoffeeTree" }, order);
        }

        [Test]
        public void EarlierTickRewards_StayAheadOfLaterTick()
        {
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f);
            var rewards = new RunRewards(cocktail, tea, new CoffeeBeanGauge());

            tea.Add(8);
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(true, out var a)); // 틱1: 차 보상 화면 열림
            cocktail.Add(8);                                                     // 선택 중엔 재료가 안 들어오지만
            tea.Add(11);                                                         // 재평가 등으로 생긴 다음 틱
            Assert.AreEqual(CardCategory.Tea, a);
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(true, out var b));
            Assert.AreEqual(CardCategory.Cocktail, b);
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(true, out var c));
            Assert.AreEqual(CardCategory.Tea, c);
        }

        [Test]
        public void HybridOffer_ComesBeforeQueuedCards_AndTree()
        {
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f);
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(cocktail, tea, beans);
            tea.Add(8);
            beans.AddRaw(10);
            rewards.RequestHybrid();

            Assert.AreEqual(RewardKind.Hybrid, rewards.TakeNext(true, out _));
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(true, out var c));
            Assert.AreEqual(CardCategory.Tea, c);
            Assert.AreEqual(RewardKind.CoffeeTree, rewards.TakeNext(true, out _));
            Assert.AreEqual(RewardKind.None, rewards.TakeNext(true, out _));
        }

        [Test]
        public void RequirementReduction_ReevaluatesImmediately_CocktailBeforeTea()
        {
            var stats = new PlayerStats();
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f, () => stats.GaugeNeedMultiplier);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f, () => stats.GaugeNeedMultiplier);
            var rewards = new RunRewards(cocktail, tea, new CoffeeBeanGauge());

            // 둘 다 7/8 — 아직 보상 없음
            tea.Add(7);
            cocktail.Add(7);
            Assert.AreEqual(0, rewards.PendingCardCount);

            // 리큐르(−8%) 적용: 8×0.92=7.36 → 7. 재료 추가 없이 즉시 재평가해야 한다.
            stats.Apply(new CardEffect(CardEffectType.GaugeNeedPercent, -0.08f));
            Assert.AreEqual(7, cocktail.Threshold);
            Assert.AreEqual(0, rewards.PendingCardCount, "재평가 전엔 그대로(옛 버그: Add가 와야만 판정)");
            rewards.ReevaluateGauges();

            Assert.AreEqual(2, rewards.PendingCardCount);
            Assert.AreEqual(0, cocktail.Current, "사용한 요구량(7) 차감, 잔량 보존");
            Assert.AreEqual(2, cocktail.Level);
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(true, out var first));
            Assert.AreEqual(CardCategory.Cocktail, first, "재평가 순서는 칵테일 → 차");
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(true, out var second));
            Assert.AreEqual(CardCategory.Tea, second);
        }

        [Test]
        public void ColdBrewNode_ReevaluatesGaugesThroughTryUnlockNode()
        {
            var tree = DefaultTree();
            var stats = new PlayerStats();
            var cocktail = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f, () => stats.GaugeNeedMultiplier);
            var tea = new IngredientGauge(IngredientType.Tea, 8f, 1.4f, () => stats.GaugeNeedMultiplier);
            var beans = new CoffeeBeanGauge();
            var rewards = new RunRewards(cocktail, tea, beans);

            cocktail.Add(7); // 콜드브루(−10%): 8×0.9=7.2 → 7
            beans.AddRaw(10);
            Assert.AreEqual(RewardKind.CoffeeTree, rewards.TakeNext(tree.AnyAvailable(), out _));
            Assert.IsTrue(rewards.TryUnlockNode(tree, Find(tree, "cold_brew"), stats));

            Assert.AreEqual(1, rewards.PendingCardCount, "노드 해금 직후 칵테일 보상이 예약된다");
            Assert.AreEqual(RewardKind.Card, rewards.TakeNext(tree.AnyAvailable(), out var cat));
            Assert.AreEqual(CardCategory.Cocktail, cat);
            Assert.AreEqual(RewardKind.None, rewards.TakeNext(tree.AnyAvailable(), out _),
                "트리는 이미 열렸으므로 새 포인트 없이 다시 열리지 않는다");
        }

        [Test]
        public void Reevaluate_WithoutReduction_DoesNothing()
        {
            var g = new IngredientGauge(IngredientType.Cocktail, 8f, 1.4f);
            g.Add(7);
            Assert.AreEqual(0, g.Reevaluate());
            Assert.AreEqual(7, g.Current);
            Assert.AreEqual(1, g.Level);
        }
    }
}
