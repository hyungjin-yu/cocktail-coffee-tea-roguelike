using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 전투 HUD 준비 음료 칩의 그릇(DrinkVesselView): 커피 추출법마다 맞는 잔 아트(에스프레소 데미타스·핸드 드립 머그·모카포트 컵)가
    /// 임시 그림 없이 그려지고, 음료 양만큼 액체가 차 있으며, 칩 사각형 안에 들어간다.
    /// </summary>
    public class DrinkRunHudVesselPlayTests
    {
        GameManager game;
        readonly List<string> folders = new List<string>();

        static readonly (CoffeeBrewMethod method, string cup)[] Cases =
        {
            (CoffeeBrewMethod.EspressoMachine, "dl_coffee_demitasse"),
            (CoffeeBrewMethod.PourOver, "dl_coffee_mug"),
            (CoffeeBrewMethod.MokaPot, "dl_coffee_cup"),
        };

        [UnitySetUp]
        public IEnumerator Setup()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return EndGame();
            Time.timeScale = 1f;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var f in folders) try { Directory.Delete(f, true); } catch { }
            folders.Clear();
        }

        IEnumerator EndGame()
        {
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
        }

        static DrinkRecipe Coffee(CoffeeBrewMethod m)
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, m);
            r.name = "시험 커피";
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, m == CoffeeBrewMethod.EspressoMachine ? 180 : 150));
            return r;
        }

        /// <summary>추출법마다 새 저장 폴더(원두 첫 지급 30g)에서 음료를 만들고 런을 시작한다. 스폰·보스는 끈다.</summary>
        IEnumerator StartRun(DrinkRecipe recipe)
        {
            yield return EndGame();
            var f = Path.Combine(Application.temporaryCachePath, "drink-hud-vessel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(f);
            folders.Add(f);
            PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
            SettingsService.SetForTests(Path.Combine(f, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
            var craft = DrinkLab.Craft(recipe);
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            yield return null;
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c); // Screen Space Overlay: 월드 = 화면 픽셀
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [UnityTest]
        public IEnumerator PreparedCoffee_ChipShowsMethodCupWithLiquid_InsideChip()
        {
            foreach (var (m, cup) in Cases)
            {
                yield return StartRun(Coffee(m));
                Assert.AreEqual(cup, CoffeeBrewing.CupArt(m), $"{m}: 추출법 잔 아트 대응");
                var drink = DrinkLab.DrinkInRun(game.RunId);
                Assert.IsNotNull(drink, $"{m}: 런에 준비 음료");
                Assert.AreEqual(m, drink.recipe.coffeeMethod);

                var hud = game.DrinkHud;
                hud.RefreshForTests();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(hud.ChipVisible, $"{m}: 전투 화면에 준비 음료 칩이 보인다");

                var vessels = hud.ChipRect.GetComponentsInChildren<DrinkVesselView>(true);
                Assert.AreEqual(1, vessels.Length, $"{m}: 칩 그릇은 하나");
                var v = vessels[0];
                Assert.IsTrue(v.gameObject.activeInHierarchy, $"{m}: 그릇이 활성");
                Assert.AreEqual(cup, v.AssetId, $"{m}: 추출법에 맞는 잔");
                Assert.IsFalse(v.Placeholder, $"{m}: 임시 그림이 아닌 실제 아트");
                Assert.IsNotNull(v.Glass, $"{m}: 잔 이미지");
                Assert.IsNotNull(v.Glass.sprite, $"{m}: 잔 스프라이트");
                Assert.AreSame(DrinkArt.Sprite(cup), v.Glass.sprite, $"{m}: 잔 스프라이트 = {cup} 아트");
                Assert.IsTrue(v.Glass.enabled && v.Glass.isActiveAndEnabled, $"{m}: 잔 이미지가 그려진다");
                Assert.Greater(v.Glass.color.a, 0.5f, $"{m}: 잔이 투명하지 않다");
                Assert.AreEqual(DrinkStageView.CapacityFor(cup), v.CapacityMl, 1e-4f, $"{m}: 잔 용량");

                float expectMl = DrinkCalculator.Look(drink.recipe).totalVolume;
                Assert.Greater(expectMl, 0f, $"{m}: 음료 양");
                Assert.Greater(v.VolumeMl, 0f, $"{m}: 액체 양 > 0");
                Assert.AreEqual(expectMl, v.VolumeMl, 1e-3f, $"{m}: 액체 양 = 음료 양");
                Assert.Greater(v.Level01, 0f, $"{m}: 수면이 바닥보다 위");
                Assert.Greater(v.LayerCount, 0, $"{m}: 액체 층");

                var chip = ScreenRect(hud.ChipRect);
                var glass = ScreenRect(v.Glass.rectTransform);
                Assert.Greater(glass.width, 1f, $"{m}: 잔 폭");
                Assert.Greater(glass.height, 1f, $"{m}: 잔 높이");
                const float eps = 0.5f;
                Assert.IsTrue(glass.xMin >= chip.xMin - eps && glass.xMax <= chip.xMax + eps && glass.yMin >= chip.yMin - eps && glass.yMax <= chip.yMax + eps,
                    $"{m}: 잔 {glass} 가 칩 {chip} 밖으로 나감");
            }
        }
    }
}
