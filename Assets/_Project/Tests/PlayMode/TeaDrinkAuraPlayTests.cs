using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>항목 16 차 소믈리에 음료 오라가 실제 DrinkBuff 상태(음료 없음·준비 음료·마스터 마시기 교체·종료)를 따르는지.</summary>
    public class TeaDrinkAuraPlayTests
    {
        GameManager game;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            RunModeSelection.Current = RunMode.Standard;
            yield return null;
            Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        [UnityTest]
        public IEnumerator NoDrink_NoAura_And_Bartender_HasNone()
        {
            game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Bow);
            yield return null; yield return null;
            Assert.IsNull(game.DrinkBuff);
            Assert.IsNotNull(game.TeaAura);
            Assert.IsFalse(game.TeaAura.Showing, "음료 미사용이면 오라 없음");
            Assert.AreEqual(0, game.TeaAura.VisibleRipples + game.TeaAura.VisibleWisps);
            Object.Destroy(game.gameObject); yield return null;
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            Assert.IsNull(game.TeaAura, "바텐더는 차 오라 없음");
        }

        [UnityTest]
        public IEnumerator MasterDrink_ShowsAura_LowEffectsSimple_RunEndHides()
        {
            RunModeSelection.Current = RunMode.Endless;
            game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Bow);
            yield return null;
            game.Master.BannersEnabled = false;
            var r = DrinkLab.ExamplePresets.First(p => p.name.Contains("에스프레소")).Clone();
            foreach (var s in r.steps) DrinkLab.Data.Add(s.id, s.amount);
            var res = game.CraftRunCup(r);
            Assert.IsTrue(res.Ok, res.Message);
            Assert.IsFalse(game.TeaAura.Showing, "제조만으로는 오라 없음(마시기 전)");
            Assert.IsTrue(game.DrinkRunCup(res.Cup, out var err), err);
            float waitUntil = Time.realtimeSinceStartup + 3f;
            while (game.TeaAura.Fade < 1f && Time.realtimeSinceStartup < waitUntil) yield return null;
            Assert.IsTrue(game.TeaAura.Showing, $"실제 음용 후 오라 (state={game.State} job={game.Job} buff={(game.DrinkBuff != null)} dead={game.Player.IsDead} fade={game.TeaAura.Fade} ts={Time.timeScale})");
            Assert.AreEqual(TeaDrinkAura.Ripples, game.TeaAura.VisibleRipples);
            Assert.AreEqual(TeaDrinkAura.Wisps, game.TeaAura.VisibleWisps);
            Assert.AreEqual(1f, game.TeaAura.Fade, 1e-3f);
            int children = game.TeaAura.transform.childCount;
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(game.TeaAura.SimpleMarker);
            Assert.AreEqual(0, game.TeaAura.VisibleWisps, "저이펙트: 김 없음");
            Assert.AreEqual(1, game.TeaAura.VisibleRipples, "저이펙트: 단순 고리 하나");
            Assert.AreEqual(children, game.TeaAura.transform.childCount, "자식 수 불변(스폰 누수 없음)");
            game.GiveUp();
            float endWait = Time.realtimeSinceStartup + 3f;
            while (game.TeaAura.Showing && Time.realtimeSinceStartup < endWait) yield return null;
            Assert.IsFalse(game.TeaAura.Showing, "런 종료 후 오라 사라짐");
        }
    }
}
