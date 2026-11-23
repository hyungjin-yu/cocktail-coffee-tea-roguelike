using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-23 실창 720p 결함(27_bitter_mist_partly_under_left_hud) 재현·개선 증거(창 없음, 가상 렌더). CCT_EVIDENCE_DIR가 있을 때만 실행.
    /// 쓴맛 안개를 왼쪽 HUD 패널(직업·목표 조합·발동 상자) 아래에 두고 예고·파동 중 1280x720 / 1920x1080을 같은 장면에서
    /// before(가림 처리 끔 = 5c2d866 동작) / after(가림 처리) 두 장으로 찍는다. 파동 직후 상태 줄(이동 둔화) 장면도 함께.
    /// </summary>
    public class HudHazardEvidenceCapture
    {
        string dir; readonly StringBuilder log = new StringBuilder();
        GameManager game; Canvas canvas; bool setUp;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            setUp = true;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!setUp) yield break;
            File.AppendAllText(Path.Combine(dir, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[HudHazardEvidence] " + s); }

        void KeepActivity()
        {
            var rt = game.Player.HiddenRuntime;
            var t = rt.GetType();
            t.GetProperty("LastActivation").GetSetMethod(true).Invoke(rt, new object[] { "근거리 산탄 ×4\n250cm 안 적에게 직접 타격 피해 +20%" });
            t.GetProperty("LastActivationRemaining").GetSetMethod(true).Invoke(rt, new object[] { 3f });
        }

        IEnumerator Shot(string name, int w, int h, bool occlusion)
        {
            var occ = game.Hud.HazardOcclusion;
            occ.enabled = occlusion;
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, w, h, $"{name}_{(occlusion ? "after" : "before")}_{w}x{h}", () =>
            {
                if (occlusion) occ.Evaluate(Camera.main, true);
                else foreach (var p in game.Hud.LeftPanels) { var g = p.GetComponent<CanvasGroup>(); if (g != null) g.alpha = 1f; }
                var states = new StringBuilder();
                foreach (var p in game.Hud.LeftPanels) states.Append($" {p.name}={(p.gameObject.activeInHierarchy ? occ.AlphaOf(p).ToString("0.00") : "off")}");
                Line($"{name} {w}x{h} occlusion={occlusion} hazards={occ.HazardCount}{states}");
            });
            occ.enabled = true;
        }

        IEnumerator Both(string name)
        {
            foreach (var (w, h) in new[] { (1280, 720), (1920, 1080) })
            {
                yield return Shot(name, w, h, false);
                yield return Shot(name, w, h, true);
            }
        }

        IEnumerator Scenario(string tag, Job job, WeaponKind weapon)
        {
            RunModeSelection.Current = RunMode.Standard;
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            canvas = game.Hud.GetComponentInParent<Canvas>();
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(100000f);
            game.SetRunTimeForTests(247f);
            var mist = game.Spawner.SpawnDebuffMob(game.RunTime);
            mist.Invulnerable = true;
            var md = mist.GetComponent<BitterMistDriver>();
            var cam = Camera.main;
            // 실창 27번 장면과 같은 자리: 화면 왼쪽 30%, 위에서 40%(목표 조합·발동 상자 오른쪽 끝 아래).
            Vector2 Spot() => cam.ViewportToWorldPoint(new Vector3(0.30f, 0.58f, -cam.transform.position.z));
            float until = Time.realtimeSinceStartup + 8f;
            while (!md.Telegraphing && Time.realtimeSinceStartup < until) { mist.transform.position = Spot(); KeepActivity(); yield return null; }
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.55f) { mist.transform.position = Spot(); KeepActivity(); yield return null; }
            Line($"{tag}: telegraph={md.Telegraphing} formal={md.UsesFormalArt} mist={(Vector2)mist.transform.position} player={game.Player.Position} pulseRadius={BitterMistDriver.PulseRadius}");
            yield return Both($"{tag}_mist_under_left_hud_telegraph");

            // 파동 직후 상태 줄(이동 둔화 · 공속 감소). 플레이어 옆에서 실제 Pulse 판정.
            var p = game.Player;
            mist.transform.position = p.Position + new Vector2(1f, 0f);
            md.Pulse();
            yield return new WaitForSecondsRealtime(0.25f);
            Line($"{tag}: slowed={p.Stats.IsSlowed} slowLeft={p.Stats.SlowRemaining:0.00} atkSlow={p.Stats.AttackSlowed} status='{game.Hud.StatusLineText}'");
            foreach (var (w, h) in new[] { (1280, 720), (1920, 1080) })
                yield return SkinEvidenceCapture.CaptureTo(dir, Line, cam, new[] { canvas }, w, h, $"{tag}_pulse_hit_status_{w}x{h}", () => game.Hud.HazardOcclusion.Evaluate(cam, true));
        }

        [UnityTest] public IEnumerator Tea_Bow() { yield return Scenario("tea", Job.TeaSommelier, WeaponKind.Bow); }
        [UnityTest] public IEnumerator Bartender_Gun() { yield return Scenario("bartender", Job.Bartender, WeaponKind.Gun); }
    }
}
