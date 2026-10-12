using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>런에서 모은 재료가 런 종료 시 PermanentProgress에 누적되는지 (도감의 "총 N개 수집" 표시용).
    /// 실제 세이브 파일은 절대 건드리지 않도록 이 테스트만의 임시 경로로 리다이렉트한다.</summary>
    public class MaterialCollectionTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_materials_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath); // 세션 공용 경로로 복원
            if (File.Exists(tempPath)) File.Delete(tempPath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RunEnd_AddsThisRunsMaterials_ToPermanentTotals()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            game.AddIngredient(IngredientType.Cocktail, 4);
            game.AddIngredient(IngredientType.Tea, 6);
            game.AddIngredient(IngredientType.Coffee, 3);
            yield return null;

            // 2026-10-06: 이전 기대(런 종료 전 영구 누적 0)는 저장 v1 규칙. 08 §6(v2)은 체크포인트(첫 품종 발견·
            // 선택창·5초 주기 등)마다 "마지막 반영량과의 차이"만 전역 합계에 더하고 activeRun 반영량을 같이 기록한다.
            // 그래서 런 중에는 전역 합계 == 이번 런 반영량(새 세이브 기준) ≤ 실제 획득량이어야 한다.
            var run = PermanentProgress.Current.activeRun;
            Assert.IsNotNull(run, "런 중에는 activeRun이 기록돼 있어야 한다(08:146)");
            Assert.AreEqual(run.cocktail, PermanentProgress.Current.cocktailIngredientsCollected,
                "전역 칵테일 합계는 activeRun 반영량과 같아야 한다(차이만 더함)");
            Assert.AreEqual(run.tea, PermanentProgress.Current.teaLeavesCollected);
            Assert.AreEqual(run.coffee, PermanentProgress.Current.coffeeBeansCollected);
            Assert.LessOrEqual(PermanentProgress.Current.cocktailIngredientsCollected, 4, "실제 획득량보다 많이 반영되면 안 된다");
            Assert.LessOrEqual(PermanentProgress.Current.teaLeavesCollected, 6);
            Assert.LessOrEqual(PermanentProgress.Current.coffeeBeansCollected, 3);

            game.Player.TakeDamage(999999f); // 죽여서 런 종료
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            // 런 종료 시 남은 차이만 더해 정확히 이번 런 획득량 — 체크포인트 반영분을 두 번 더하지 않는다.
            Assert.AreEqual(4, PermanentProgress.Current.cocktailIngredientsCollected);
            Assert.AreEqual(6, PermanentProgress.Current.teaLeavesCollected);
            Assert.AreEqual(3, PermanentProgress.Current.coffeeBeansCollected);
            Assert.IsNull(PermanentProgress.Current.activeRun, "런 종료 후 activeRun=null(08:146)");
        }

        [UnityTest]
        public IEnumerator TwoRuns_AccumulateAcrossBoth()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            game.AddIngredient(IngredientType.Cocktail, 5);
            yield return null;
            game.Player.TakeDamage(999999f);
            yield return null;

            game.Restart();
            yield return null;
            var game2 = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            game2.AddIngredient(IngredientType.Cocktail, 2);
            yield return null;
            game2.Player.TakeDamage(999999f);
            yield return null;

            Assert.AreEqual(7, PermanentProgress.Current.cocktailIngredientsCollected);

            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
        }

        [UnityTest]
        public IEnumerator AddIngredient_DiscoversAMaterialVariety_FromTheRightCategory()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            Assert.AreEqual(0, PermanentProgress.Current.discoveredMaterialVarietyIds.Count);

            game.AddIngredient(IngredientType.Cocktail, 1);
            yield return null;

            Assert.AreEqual(1, PermanentProgress.Current.discoveredMaterialVarietyIds.Count,
                "재료를 주우면 품종이 하나 무작위로 골라져 도감에 기록되어야 한다");
            var discoveredId = PermanentProgress.Current.discoveredMaterialVarietyIds[0];
            Assert.IsTrue(MaterialVarieties.Cocktail.Exists(v => v.id == discoveredId),
                "칵테일 재료를 주웠으면 칵테일 품종 목록에서 골라져야 한다");
        }

        [UnityTest]
        public IEnumerator AddIngredient_EnoughPickups_EventuallyDiscoversAllVarietiesInCategory()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            // 품종이 6개뿐이라 충분히 많이 주우면(50회) 전부 다 나올 확률이 사실상 100%에 가까움
            for (int i = 0; i < 50; i++)
                game.AddIngredient(IngredientType.Cocktail, 1);
            yield return null;

            Assert.AreEqual(MaterialVarieties.Cocktail.Count, PermanentProgress.Current.discoveredMaterialVarietyIds.Count);
        }
    }
}
