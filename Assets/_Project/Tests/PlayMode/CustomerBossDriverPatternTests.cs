using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>최종보스(진상 손님) 4패턴 중 신규 2개 — "별점 1점!"(§141) / "주문한 거 아닌데?"(§142).
    /// 첫 2패턴(환불/소환)은 BossFlowTests에서 이미 커버한다.</summary>
    public class CustomerBossDriverPatternTests
    {
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
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StarPattern_HitSlowsPlayer_AndBossFreezesDuringTelegraph()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));

            var boss = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f });
            game.Player.transform.position = boss.transform.position + new Vector3(3f, 0f, 0f);
            var rb = boss.GetComponent<Rigidbody2D>();

            float normalSpeed = game.Stats.MoveSpeed;
            bool wasFrozenAtSomePoint = false;
            bool slowApplied = false;

            float deadline = Time.time + 20f; // 등장 안내+첫 지연+환불+소환 두 패턴을 지나 별점까지
            while (Time.time < deadline && !slowApplied)
            {
                DismissPopups(game);
                if (boss == null || boss.IsDead) break;
                if (rb.linearVelocity == Vector2.zero) wasFrozenAtSomePoint = true;
                if (game.Stats.IsSlowed) slowApplied = true;
                yield return null;
            }

            Assert.IsTrue(wasFrozenAtSomePoint, "06:134 — 시전(예고 포함) 중에는 보스가 멈춰야 한다");
            Assert.IsTrue(slowApplied, "06:141 — 별점 탄에 맞으면 이동속도가 느려져야 한다");
            Assert.Less(game.Stats.MoveSpeed, normalSpeed);
        }

        [UnityTest]
        public IEnumerator SpellBlockPattern_LocksAnOwnedPeriodicSkill()
        {
            var game = GameManager.Instance;
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Stats.Apply(new CardEffect(CardEffectType.FlameLevel, 1f)); // 화염만 보유

            var boss = game.Director.SpawnNow(new BossDirector.Spec { def = EnemyDef.DifficultCustomer, hpScale = 1f });
            game.Player.transform.position = boss.transform.position + new Vector3(1f, 0f, 0f);

            // 환불(0) → 소환(1) → 별점(2) → 주문 부정(3) 순서로 네 번째 패턴까지 대기
            // 등장 안내(1)+첫 지연(3)+[환불 0.8+대기7]+[소환 1+대기7]+[별점 0.8+대기7]+주문 예고(0.8) 여유
            float deadline = Time.time + 30f;
            bool locked = false;
            while (Time.time < deadline)
            {
                DismissPopups(game);
                if (boss == null || boss.IsDead) break;
                if (game.Stats.FlameLocked) { locked = true; break; }
                yield return null;
            }

            Assert.IsTrue(locked, "06:142 — 보유한 주기 스킬(화염)이 주문 부정으로 잠겨야 한다");
        }
    }
}
