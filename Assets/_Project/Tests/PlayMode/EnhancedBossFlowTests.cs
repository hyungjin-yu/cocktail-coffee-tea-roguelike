using System.Collections;
using System.IO;
using System.Linq;
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

            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f); // 06:24 등장 안내 동안은 무적
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
        public IEnumerator PermanentSkill_UnlockedInPreviousRun_AppliesAtNextRunStart()
        {
            // 강화 보스 1마리 분량(3)을 모아서 1단계 노드 두 개를 직접 찍는다 (원두 감별사엔 3만 있으면 충분).
            PermanentProgress.AddSpecialty(3);
            var cupper = PermanentSkillDatabase.LoadAll().Single(n => n.id == "bean_cupper");
            var tree = new PermanentSkillTree(PermanentSkillDatabase.LoadAll(),
                PermanentProgress.Current.unlockedPermanentSkillIds);
            Assert.IsTrue(tree.TryUnlock(cupper, 3), "포인트가 충분하면 1단계 노드를 찍을 수 있어야 한다");
            PermanentProgress.UnlockPermanentSkill(cupper.id);

            // 이 GameManager는 아직 반영 전 상태로 만들어졌으니, 재시작해서 새로 반영되는지 확인한다.
            GameManager.Instance.Restart();
            yield return null;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            Assert.IsTrue(game.PermanentSkills.IsUnlocked(cupper), "이전 런에서 찍은 노드는 다음 런에도 해금 상태여야 한다");
            Assert.AreEqual(0.20f, game.Stats.beanGainPercent, 0.0001f, "원두 감별사(+20%) 효과가 적용돼야 한다");
            Assert.AreEqual(1.20f, game.BeanGainMultiplier, 0.0001f);

            game.AddIngredient(IngredientType.Coffee, 4);
            // 07:34 — 픽업별 반올림이 아니라 누적 원량 기준 floor(4×1.2)=4. 다음 원두 1개로 R=5 → E=6.
            Assert.AreEqual(4, game.BeanGauge.Current, "원두 획득량에도 배율이 적용돼야 한다(누적 floor)");
            game.AddIngredient(IngredientType.Coffee, 1);
            Assert.AreEqual(6, game.BeanGauge.Current, "누적 R=5 → E=floor(6.0)=6 — 픽업별 반올림 차익 없음");
        }

        [UnityTest]
        public IEnumerator PermanentSkill_CannotBeUnlocked_WithoutEnoughPoints_OrPrerequisite()
        {
            var cupper = PermanentSkillDatabase.LoadAll().Single(n => n.id == "bean_cupper");
            var roasting = PermanentSkillDatabase.LoadAll().Single(n => n.id == "skilled_roasting"); // cupper의 2단계

            var tree = new PermanentSkillTree(PermanentSkillDatabase.LoadAll(),
                PermanentProgress.Current.unlockedPermanentSkillIds);

            Assert.IsFalse(tree.TryUnlock(cupper, 2), "비용(3)보다 적은 포인트로는 찍을 수 없어야 한다");
            Assert.IsFalse(tree.TryUnlock(roasting, 999), "선행 노드 없이는 포인트가 충분해도 찍을 수 없어야 한다");

            Assert.IsTrue(tree.TryUnlock(cupper, 3));
            Assert.IsTrue(tree.TryUnlock(roasting, 6), "선행 노드를 찍은 뒤에는 2단계도 찍을 수 있어야 한다");
            yield return null;
        }
    }
}
