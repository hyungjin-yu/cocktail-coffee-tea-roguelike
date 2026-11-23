using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-23 실창 720p 결함(왼쪽 HUD가 쓴맛 안개 몸체·파동 테두리를 가림) 후속: 겹친 패널만 반투명으로 낮추고
    /// 몹 위치·예고 경계·판정 반경은 그대로인지, 겹침이 끝나면 복원되는지, 파동 직후 상태 줄에 이동 둔화가 글로 보이는지.</summary>
    public class HudHazardOcclusionPlayTests
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
            yield return null;
            Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        IEnumerator Run(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(100000f);
        }

        /// <summary>패널 화면 중심 아래의 월드 지점(z=0).</summary>
        static Vector2 WorldUnder(RectTransform panel)
        {
            var c = new Vector3[4]; panel.GetWorldCorners(c);
            var screen = (c[0] + c[2]) * 0.5f; // Overlay 캔버스: 월드 코너 = 화면 픽셀
            var w = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.main.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        IEnumerator Hold(Enemy e, Vector2 pos, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { e.transform.position = pos; yield return null; }
        }

        [UnityTest]
        public IEnumerator BitterMist_UnderLeftPanel_FadesOnlyOverlappedPanels_ThenRestores_JudgementUnchanged()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var occ = game.Hud.HazardOcclusion;
            Assert.IsNotNull(occ, "HUD 가림 처리 부착");
            Assert.GreaterOrEqual(occ.PanelCount, 4, "왼쪽 패널 감시");
            var job = game.Hud.LeftPanels[1];
            var hidden = game.Hud.LeftPanels[0];

            game.SetRunTimeForTests(121f);
            var mist = game.Spawner.SpawnDebuffMob(game.RunTime);
            Assert.AreSame(EnemyDef.BitterMist, mist.Def);
            mist.Invulnerable = true;
            var driver = mist.GetComponent<BitterMistDriver>();
            Assert.IsTrue(driver is IHudHazard, "쓴맛 안개는 HUD 위험 영역을 내놓는다");

            // 처음: 겹침 없음(몹을 화면 오른쪽 아래로)
            var cam = Camera.main;
            var farRight = (Vector2)cam.ViewportToWorldPoint(new Vector3(0.8f, 0.3f, -cam.transform.position.z));
            yield return Hold(mist, farRight, 0.3f);
            Assert.IsFalse(occ.IsOccluded(job), "멀리 있으면 가리지 않음");
            Assert.AreEqual(1f, occ.AlphaOf(job), 0.001f, "평소 불투명");

            // 직업 패널 아래로 이동 → 직업 패널이 낮아짐
            var under = WorldUnder(job);
            yield return Hold(mist, under, 0.4f);
            Assert.IsTrue(occ.IsOccluded(job), "직업 패널 아래 몸체 → 가림 판정");
            Assert.AreEqual(HudHazardOcclusion.OccludedAlpha, occ.AlphaOf(job), 0.02f, "겹친 패널 반투명");
            Assert.AreEqual(under, (Vector2)mist.transform.position, "HUD 처리는 몹 위치를 바꾸지 않는다");

            // 예고 중: 바닥 테두리 경계까지 위험 영역에 들어간다(몸체보다 넓음)
            Assert.IsTrue(driver.TryGetHudHazardBounds(out var bodyOnly));
            float until = Time.realtimeSinceStartup + 8f;
            while (!driver.Telegraphing && Time.realtimeSinceStartup < until) { mist.transform.position = under; yield return null; }
            Assert.IsTrue(driver.Telegraphing, "예고 시작");
            yield return Hold(mist, under, 0.2f);
            Assert.IsTrue(driver.TryGetHudHazardBounds(out var withRing));
            Assert.Greater(withRing.size.x, bodyOnly.size.x + 1f, "예고 중에는 파동 테두리 폭까지");
            Assert.AreEqual(BitterMistDriver.PulseRadius * 2f, withRing.size.x, 0.35f, "위험 영역 폭 = 판정 지름(그림 테두리 기준)");

            // 판정 위치·반경 불변: 반경 안/밖 판정이 HUD와 무관
            var p = game.Player;
            mist.transform.position = p.Position + new Vector2(BitterMistDriver.PulseRadius + 0.6f, 0f);
            driver.Pulse();
            Assert.IsFalse(p.Stats.IsSlowed, "반경 밖 무사(판정 불변)");
            mist.transform.position = p.Position + new Vector2(BitterMistDriver.PulseRadius - 0.3f, 0f);
            driver.Pulse();
            Assert.IsTrue(p.Stats.IsSlowed, "반경 안 둔화(판정 불변)");

            // 다시 멀리 → 복원
            yield return Hold(mist, farRight, 0.4f);
            Assert.IsFalse(occ.IsOccluded(job));
            Assert.AreEqual(1f, occ.AlphaOf(job), 0.001f, "겹침이 끝나면 원래 불투명도");
            Assert.AreEqual(1f, occ.AlphaOf(hidden), 0.001f);

            // 몹이 죽으면 위험 영역 없음
            mist.Invulnerable = false;
            mist.TakeDamage(100000f);
            yield return null;
            Assert.IsFalse(driver != null && driver.TryGetHudHazardBounds(out _), "죽은 몹은 가림 판정 없음");
        }

        [UnityTest]
        public IEnumerator BitterMist_PulseHit_StatusLineShowsMoveSlow()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow);
            game.SetRunTimeForTests(121f);
            var mist = game.Spawner.SpawnDebuffMob(game.RunTime);
            mist.Invulnerable = true;
            var driver = mist.GetComponent<BitterMistDriver>();
            var p = game.Player;
            mist.transform.position = p.Position + new Vector2(1f, 0f);
            driver.Pulse();
            yield return new WaitForSeconds(0.2f);
            StringAssert.Contains("이동 속도 -", game.Hud.StatusLineText, "파동 직후 이동 둔화 글(실제 감소율)");
            StringAssert.Contains("(3초)", game.Hud.StatusLineText, "남은 시간은 괄호 안 초");
            StringAssert.Contains("공격 속도 -25% (3초)", game.Hud.StatusLineText, "공격 속도 감소 글 유지(전체 명칭)");
            yield return new WaitForSeconds(BitterMistDriver.SlowSeconds + 0.3f);
            Assert.IsFalse(p.Stats.IsSlowed);
            StringAssert.DoesNotContain("이동 속도", game.Hud.StatusLineText, "끝나면 사라짐");
        }
    }
}
