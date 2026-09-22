using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>강화 보스 처치 → 스페셜티(영구 재화) 지급 → 영구 저장, 그리고 다음 런에 적용되는 보너스.
    /// 실제 세이브 파일은 절대 건드리지 않도록 매 테스트마다 임시 경로로 리다이렉트한다.</summary>
    public class EnhancedBossFlowTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_enhanced_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);

            InputSettings.Scheme = MovementScheme.Keyboard;
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
            // 실제 세이브 경로가 아니라 스위트 공용 임시 경로로 되돌린다 (TestSaveIsolation 참고).
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnhancedBoss_Kill_HealsPlayer_GrantsSpecialty_DropsBeans_AndDoesNotEndRun()
        {
            var game = GameManager.Instance;
            game.Player.TakeDamage(50f); // 회복이 실제로 일어나는지 보이도록 미리 깎아둔다
            float hpBeforeKill = game.Player.Hp;

            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(4f, 0f), EnemyDef.EspressoLeviathan, 1f,
                game.Player, game.transform);
            game.ActiveBoss = boss;

            boss.TakeDamage(999999f);
            yield return null;

            Assert.IsNull(game.ActiveBoss, "처치하면 보스 상태가 해제되어야 한다");
            Assert.Greater(game.Player.Hp, hpBeforeKill, "강화 보스도 중간보스처럼 처치 시 회복을 줘야 한다");
            Assert.AreEqual(GameState.Playing, game.State, "강화 보스 처치로 런이 끝나면 안 된다");

            var beans = Object.FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None);
            Assert.AreEqual(EnemyDef.EspressoLeviathan.dropCount, beans.Length, "원두 드랍 개수는 def.dropCount와 같아야 한다");

            Assert.AreEqual(Balance.SpecialtyDropAmount, PermanentProgress.Current.specialtyPoints,
                "처치 시 스페셜티를 즉시 지급해야 한다");
            Assert.AreEqual(Balance.SpecialtyDropAmount, SaveSystem.Load(tempPath).specialtyPoints,
                "런이 끝나기 전에도 파일에 즉시 저장되어야 한다 (죽어도 유지되도록)");
        }

        [UnityTest]
        public IEnumerator PermanentSpecialty_FromPreviousRuns_BoostsNextRunDamageAndBeanGain()
        {
            // 이 GameManager는 스페셜티 0인 상태로 이미 만들어졌으니, 재시작해서 새로 반영되는지 확인한다.
            PermanentProgress.AddSpecialty(10);

            GameManager.Instance.Restart();
            yield return null;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            float expectedDamagePercent = SpecialtyBonus.DamagePercent(10);
            Assert.AreEqual(expectedDamagePercent, game.Stats.damagePercent, 0.0001f,
                "영구 스페셜티 보너스는 런 시작 시 바로 적용돼야 한다");

            float expectedMultiplier = SpecialtyBonus.BeanGainMultiplier(10);
            Assert.AreEqual(expectedMultiplier, game.BeanGainMultiplier, 0.0001f);

            game.AddIngredient(IngredientType.Coffee, 4);
            Assert.AreEqual(Mathf.RoundToInt(4 * expectedMultiplier), game.BeanGauge.Current,
                "원두 획득량에도 배율이 적용돼야 한다");
        }
    }
}
