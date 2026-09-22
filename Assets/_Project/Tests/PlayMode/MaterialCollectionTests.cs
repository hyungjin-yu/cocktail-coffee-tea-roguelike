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

            Assert.AreEqual(0, PermanentProgress.Current.cocktailIngredientsCollected,
                "런이 끝나기 전에는 아직 영구 누적치에 반영되면 안 된다");

            game.Player.TakeDamage(999999f); // 죽여서 런 종료
            yield return null;

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(4, PermanentProgress.Current.cocktailIngredientsCollected);
            Assert.AreEqual(6, PermanentProgress.Current.teaLeavesCollected);
            Assert.AreEqual(3, PermanentProgress.Current.coffeeBeansCollected);
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
    }
}
