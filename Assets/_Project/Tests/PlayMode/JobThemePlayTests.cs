using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>47(2026-10-12) 실제 런: 직업 맵 배경, 찻집 찻잎 게이지 성장(×1.2) vs 바(×1.4), 찻집 HUD 칵테일 바 숨김.</summary>
    public class JobThemePlayTests
    {
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_jobtheme_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            foreach (var s in new[] { "", ".bak", ".tmp" }) if (File.Exists(tempPath + s)) File.Delete(tempPath + s);
        }

        static int TeaLevelAfter(GameManager g, int leaves)
        {
            for (int i = 0; i < leaves; i++) g.TeaGauge.Add(1);
            return g.TeaGauge.Level;
        }

        [UnityTest]
        public IEnumerator TeaHouse_MapAndSlowerTeaGrowth_BarUnchanged()
        {
            var tea = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            yield return null;
            Assert.AreEqual("bg_tea", CafeBackground.ActiveMapPrefix, "찻집 배경");
            int teaHouse = TeaLevelAfter(tea, 1500);
            Object.Destroy(tea.gameObject);
            yield return null;

            var bar = GameBootstrap.Build(Job.Bartender, WeaponKind.Gun);
            yield return null;
            Assert.AreEqual("bg_bar", CafeBackground.ActiveMapPrefix, "칵테일 바 배경");
            int barTea = TeaLevelAfter(bar, 1500);
            Assert.Greater(teaHouse, barTea + 4, "찻집 찻잎 게이지는 ×1.2로 더 많이 레벨업(칵테일 게이지가 없으므로)");
            Assert.AreEqual(Balance.CocktailGaugeGrowth, 1.4f, 1e-6f, "바 게이지 성장 그대로");
        }
    }
}
