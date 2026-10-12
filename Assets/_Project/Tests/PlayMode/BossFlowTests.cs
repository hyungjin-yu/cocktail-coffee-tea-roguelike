using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>중간보스 → 원두 → 스킬 해금, 최종보스 패턴과 클리어를 실제로 돌려본다.</summary>
    public class BossFlowTests
    {
        static BossDirector.Spec Mid() =>
            new BossDirector.Spec { def = EnemyDef.GrinderGolem, hpScale = 1f };

        static BossDirector.Spec Final() =>
            new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f };

        /// <summary>관찰 루프 중 레벨업/스킬 화면이 뜨면 시간이 멈추므로 자동으로 닫는다.</summary>
        static void DismissPopups(GameManager game)
        {
            if (game.State == GameState.LevelUp)
                game.CardView.Choose(0);
            else if (game.State == GameState.SkillSelect)
                game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard; // 로컬 PlayerPrefs 값과 무관하게 결정론적으로
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MidBoss_Spawns_ShowsBossState_AndDropsBeans()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(Mid());
            yield return null;

            Assert.AreSame(boss, game.ActiveBoss);
            Assert.IsNotNull(boss.GetComponent<ChargerDriver>());
            Assert.AreEqual(BossKind.Mid, boss.Def.boss);

            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f); // 06:24 등장 안내 동안은 무적
            boss.TakeDamage(999999f);
            yield return null;

            Assert.IsNull(game.ActiveBoss, "처치하면 보스 상태가 해제되어야 한다");

            var beans = Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(5, beans.Length, "원두 픽업 5개");
            Assert.AreEqual(GameState.Playing, game.State, "중간보스 처치로 런이 끝나면 안 된다");
        }

        [UnityTest]
        public IEnumerator BeanGauge_Full_OpensSkillTree_AndUnlockAppliesEffect()
        {
            var game = GameManager.Instance;
            float damageBefore = game.Stats.Damage;

            game.AddIngredient(IngredientType.Coffee, 10);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.SkillSelect, game.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(game.SkillView.IsOpen);
            Assert.AreEqual(0,game.Challenge.CoffeeCount,"Opening/preview must not increase challenge.");
            StringAssert.Contains("다음 해금",game.SkillView.PointsLabel);

            var locked = game.Skills.Nodes.First(n => n.id == "ristretto");
            game.SkillView.Choose(locked);
            Assert.AreEqual(0,game.Challenge.CoffeeCount,"A denied prerequisite does not increase challenge.");
            Assert.IsTrue(game.SkillView.IsOpen, "선행 노드가 없는 노드는 선택되면 안 된다");

            var root = game.Skills.Nodes.First(n => n.id == "espresso_shot");
            game.SkillView.Choose(root);
            yield return null;

            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(game.Skills.IsUnlocked(root));
            Assert.AreEqual(1,game.Challenge.CoffeeCount);
            game.SkillView.Choose(root);
            Assert.AreEqual(1,game.Challenge.CoffeeCount,"A duplicate/closed choice never increases challenge.");
            Assert.Greater(game.Stats.Damage, damageBefore);
        }

        [UnityTest]
        public IEnumerator AllFifteenCoffeeNodes_HeaderShowsCurrentChallengeNotNextUnlock()
        {
            // 2026-10-09 실제720 검수: 15노드 전부 해금 뒤에도 "다음 해금 → 137.2%"처럼 불가능한 예측을 보였다.
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Coffee, 200); // 새 런 기준 15포인트 이상
            yield return null;
            yield return null;
            for (int pass = 0; pass < 6; pass++)
                foreach (var node in game.Skills.Nodes.ToList())
                    if (game.SkillView.IsOpen) game.SkillView.Choose(node);
            Assert.AreEqual(15, game.Skills.UnlockedCount);
            if (!game.SkillView.IsOpen) game.OpenCoffeeTree();
            yield return null;
            StringAssert.Contains("모두 해금", game.SkillView.PointsLabel);
            StringAssert.DoesNotContain("다음 해금", game.SkillView.PointsLabel);
            StringAssert.Contains($"{game.Challenge.HpMultiplier * 100f:0.#}%", game.SkillView.PointsLabel);
        }

        [UnityTest]
        public IEnumerator BossBeans20_GiveTwoPoints_AllocatedInOneOpening()
        {
            var game = GameManager.Instance;

            // 07:36 — 중간보스 1마리 분량 원두 20 = 포인트 2개(임계치 10 고정, 캡핑 없음).
            game.AddIngredient(IngredientType.Coffee, 20);
            Assert.AreEqual(3, game.BeanGauge.Level);
            Assert.AreEqual(2, game.CoffeePoints);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.SkillSelect, game.State, "새 포인트가 생기면 트리가 1회 열린다");
            game.SkillView.Choose(game.Skills.Nodes.First(n => n.id == "espresso_shot"));
            Assert.IsTrue(game.SkillView.IsOpen, "포인트가 남아 있으면 같은 창에서 계속 배분한다");
            Assert.AreEqual(1, game.CoffeePoints);
            game.SkillView.Choose(game.Skills.Nodes.First(n => n.id == "ristretto"));
            Assert.IsFalse(game.SkillView.IsOpen, "포인트를 다 쓰면 닫힌다");
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(2, game.Skills.UnlockedCount);
            yield return null;
            yield return null;
            Assert.AreEqual(GameState.Playing, game.State, "새 포인트 없이 다시 열리면 안 된다");
        }

        [UnityTest]
        public IEnumerator Continue_KeepsPoints_NoAutoReopen_UntilNewPointOrK()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Coffee, 20);
            yield return null;
            yield return null;
            Assert.AreEqual(GameState.SkillSelect, game.State);

            game.SkillView.Continue();
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(2, game.CoffeePoints, "계속하기는 남은 포인트를 보존한다");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(GameState.Playing, game.State, "08:49 — 매 프레임 자동 재열림 금지");

            game.OpenCoffeeTree(); // K와 같은 진입점
            Assert.AreEqual(GameState.SkillSelect, game.State);
            Assert.AreEqual(0f, Time.timeScale);
            game.SkillView.Continue();

            game.AddIngredient(IngredientType.Coffee, 10); // 새 포인트 → 다시 1회 자동
            yield return null;
            yield return null;
            Assert.AreEqual(GameState.SkillSelect, game.State);
            Assert.AreEqual(3, game.CoffeePoints);
        }

        [UnityTest]
        public IEnumerator K_OpensWithZeroPoints_ButNotDuringPauseOrCountdown()
        {
            var game = GameManager.Instance;
            Assert.AreEqual(0, game.CoffeePoints);

            game.OpenPause();
            game.OpenCoffeeTree();
            Assert.AreEqual(GameState.Playing, game.State, "Pause 중에는 K로 열리지 않는다");
            game.ClosePause();
            Assert.IsTrue(game.IsResuming);
            game.OpenCoffeeTree();
            Assert.AreEqual(GameState.Playing, game.State, "1초 계속 카운트다운 중에는 열리지 않는다");
            yield return new WaitForSecondsRealtime(1.2f);

            game.OpenCoffeeTree();
            Assert.AreEqual(GameState.SkillSelect, game.State, "포인트 0이어도 조회용으로 열린다");
            Assert.AreEqual(0f, Time.timeScale);
            StringAssert.Contains("남은 커피 포인트 0", game.SkillView.PointsLabel);
            game.SkillView.Choose(game.Skills.Nodes.First(n => n.id == "espresso_shot"));
            Assert.AreEqual(0, game.Skills.UnlockedCount, "포인트 없이 해금되면 안 된다");
            game.SkillView.Continue();
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator CardThenTree_ChainWithoutPlayingFrame()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            game.AddIngredient(IngredientType.Coffee, 10);
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);
            float runTime = game.RunTime;

            game.CardView.Choose(0);
            Assert.AreEqual(GameState.SkillSelect, game.State, "07:46 — 선택 직후 같은 호출에서 다음 보상으로");
            Assert.AreEqual(0f, Time.timeScale, "보상 사이에 시간이 풀리면 안 된다");
            yield return null;
            Assert.AreEqual(runTime, game.RunTime, "보상 사이에 전투 시간이 흐르면 안 된다");
        }

        [UnityTest]
        public IEnumerator LevelUpAndSkillPoint_Together_AreHandledOneAfterAnother()
        {
            var game = GameManager.Instance;

            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            game.AddIngredient(IngredientType.Coffee, 10);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.LevelUp, game.State, "카드 선택이 먼저");
            game.CardView.Choose(0);
            yield return null;
            yield return null;

            Assert.AreEqual(GameState.SkillSelect, game.State, "이어서 스킬 선택");
            game.SkillView.Choose(game.Skills.Nodes.First(n => n.id == "cold_brew"));
            yield return null;

            Assert.AreEqual(GameState.Playing, game.State);
        }

        [UnityTest]
        public IEnumerator Ingredients_RouteToTheirOwnGauge()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Coffee, 4);
            game.AddIngredient(IngredientType.Cocktail, 2);
            yield return null;

            Assert.AreEqual(4, game.BeanGauge.Current);
            Assert.AreEqual(2, game.CocktailGauge.Current);
        }

        [UnityTest]
        public IEnumerator ChargerBoss_EventuallyDashes()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(Mid());
            var rb = boss.GetComponent<Rigidbody2D>();

            // 플레이어를 죽지 않게 묶어두고(무적 대용으로 체력 대폭 증가) 돌진 속도가 나오는지 관찰
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));

            float maxSpeed = 0f;
            float deadline = Time.time + 8f;
            while (Time.time < deadline)
            {
                DismissPopups(game);
                maxSpeed = Mathf.Max(maxSpeed, rb.linearVelocity.magnitude);
                yield return null;
            }

            Assert.Greater(maxSpeed, 7f, "텔레그래프 이후 돌진 속도(9)가 나와야 한다");
        }

        [UnityTest]
        public IEnumerator FinalBoss_Refund_SuppressesBonuses_ThenSummonsMinions()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Stats.Apply(new CardEffect(CardEffectType.DamagePercent, 1f));

            var boss = game.Director.SpawnNow(Final());
            Assert.IsNotNull(boss.GetComponent<CustomerBossDriver>());
            // 06:139 — 환불은 "범위 안" 플레이어에게만 적용되므로, 보스가 화면 밖에서 걸어 들어오는
            // 시간에 기대지 않고 확실히 사거리 안에 있도록 플레이어를 보스 근처로 붙여준다.
            game.Player.transform.position = boss.transform.position + new Vector3(1f, 0f, 0f);

            // 첫 패턴(등장 안내 1초 + 3초 후)은 "환불해주세요!"
            float deadline = Time.time + 7f;
            while (!game.Stats.BonusesSuppressed && Time.time < deadline)
            {
                DismissPopups(game);
                yield return null;
            }
            Assert.IsTrue(game.Stats.BonusesSuppressed, "환불 패턴이 강화 효과를 무효화해야 한다");
            Assert.AreEqual(game.Stats.baseDamage, game.Stats.Damage, 0.0001f);

            // 두 번째 패턴(7초 뒤)은 "사장 나와!" — 몹 수가 급증해야 한다
            int before = Enemy.Active.Count;
            int peak = before;
            deadline = Time.time + 9f;
            while (Time.time < deadline)
            {
                DismissPopups(game);
                peak = Mathf.Max(peak, Enemy.Active.Count);
                yield return null;
            }
            Assert.GreaterOrEqual(peak - before, 8, "소환으로 잡몹이 크게 늘어야 한다");
        }

        [UnityTest]
        public IEnumerator FinalBoss_Kill_ClearsRun()
        {
            var game = GameManager.Instance;
            var boss = game.Director.SpawnNow(Final());
            yield return null;

            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f); // 06:24 등장 안내 동안은 무적
            boss.TakeDamage(999999f);
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsTrue(game.Cleared);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(0, Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None).Length);

            game.Restart(); // 직업 선택 화면을 띄움
            yield return null;
            GameBootstrap.Build(Job.Bartender); // 테스트에서는 화면 클릭 대신 직접 진행
            yield return null;
            Assert.IsFalse(GameManager.Instance.Cleared);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
        }

        [UnityTest]
        public IEnumerator Schedule_SpawnsMidBossByRunTime()
        {
            var game = GameManager.Instance;
            var schedule = new System.Collections.Generic.List<BossDirector.Spec>
            {
                new BossDirector.Spec { time = 1f, def = EnemyDef.GrinderGolem },
            };
            game.Director.Init(game, schedule);

            float deadline = Time.time + 4f;
            while (game.ActiveBoss == null && Time.time < deadline)
            {
                DismissPopups(game);
                yield return null;
            }

            Assert.IsNotNull(game.ActiveBoss, "런 시간 1초 이후 스케줄된 보스가 나와야 한다");
        }
    }
}
