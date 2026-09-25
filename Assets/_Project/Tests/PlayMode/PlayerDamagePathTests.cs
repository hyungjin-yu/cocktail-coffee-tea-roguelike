using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>06:22 — 단발 피해(TakeDamage)는 0.8초 무적을 걸고 그 무적에 막히지만, 장판 틱 피해
    /// (TakeZoneDamage)는 그 무적을 만들지도, 막히지도 않아야 한다. 격리 테스트(GameBootstrap 없이).</summary>
    public class PlayerDamagePathTests
    {
        GameObject playerGo;
        PlayerController player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            playerGo = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(SpriteRenderer));
            player = playerGo.AddComponent<PlayerController>();
            player.Init(new PlayerStats(), Color.white);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TakeDamage_Twice_QuicklyInSuccession_SecondHitIsBlockedByInvulnerability()
        {
            float hpBefore = player.Hp;
            player.TakeDamage(10f);
            player.TakeDamage(10f); // 같은 프레임, 0.8초 무적 안 — 막혀야 함

            Assert.AreEqual(hpBefore - 10f, player.Hp, 0.01f);
            yield break;
        }

        [UnityTest]
        public IEnumerator TakeZoneDamage_DuringSingleHitInvulnerability_StillApplies()
        {
            float hpBefore = player.Hp;
            player.TakeDamage(10f); // 0.8초 단발 무적 시작
            player.TakeZoneDamage(4f); // 그 무적 창 안이어도 장판 피해는 들어가야 한다

            Assert.AreEqual(hpBefore - 14f, player.Hp, 0.01f,
                "장판 피해는 단발 무적에 막히면 안 된다 (06:22)");
            yield break;
        }

        [UnityTest]
        public IEnumerator TakeZoneDamage_DoesNotSet_SingleHitInvulnerability()
        {
            float hpBefore = player.Hp;
            player.TakeZoneDamage(4f);
            player.TakeDamage(10f); // 장판 피해가 무적을 걸지 않았으므로 이 단발 피해는 들어가야 한다

            Assert.AreEqual(hpBefore - 14f, player.Hp, 0.01f,
                "장판 피해가 단발 무적을 걸면 안 된다 (06:22)");
            yield break;
        }

        [UnityTest]
        public IEnumerator TakeDamage_AppliesDamageReduction()
        {
            player.Stats.damageReduction = 0.2f;
            float hpBefore = player.Hp;

            player.TakeDamage(10f);

            Assert.AreEqual(hpBefore - 8f, player.Hp, 0.01f, "07:19 — 받은 피해 × (1 - 0.2)");
            yield break;
        }
    }
}
