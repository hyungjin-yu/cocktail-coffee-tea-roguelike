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
    /// 2026-11-23 정식 디버프 몹(쓴맛 안개·빈 잔 도둑)·바닥 예고·HUD 상태 아이콘 시각 증거(창 없음, 가상 렌더). CCT_EVIDENCE_DIR가 있을 때만 실행.
    /// 표준(Standard)·마스터 도전(Endless) × 720p/1080p/4:3(1440x1080) + 저이펙트(예고·몸체).
    /// </summary>
    public class FeedbackArtEvidenceCapture
    {
        string dir; readonly StringBuilder log = new StringBuilder();
        GameManager game; Canvas canvas; bool setUp; RunMode savedMode; bool savedSuppress;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            savedMode = RunModeSelection.Current;
            savedSuppress = CardEducationGuide.SuppressAutomaticForTests;
            CardEducationGuide.SuppressAutomaticForTests = true;
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
            RunModeSelection.Current = savedMode;
            CardEducationGuide.SuppressAutomaticForTests = savedSuppress;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[FeedbackArtEvidence] " + s); }

        IEnumerator Snap(string name, bool all = true)
        {
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1920, 1080, name + "_1920x1080");
            if (!all) yield break;
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1280, 720, name + "_1280x720");
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1440, 1080, name + "_1440x1080_4x3");
        }

        IEnumerator Scenario(string tag, RunMode mode, Job job, WeaponKind weapon, bool low)
        {
            RunModeSelection.Current = mode;
            SettingsService.SetEffectsLevel(low ? SettingsData.EffectsLow : SettingsData.EffectsNormal);
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            canvas = game.Hud.GetComponentInParent<Canvas>();
            game.SetRunTimeForTests(121f);
            var p = game.Player;
            var mist = game.Spawner.SpawnDebuffMob(game.RunTime);
            var thief = game.Spawner.SpawnDebuffMob(game.RunTime);
            Line($"{tag}: mode={game.Mode} mist={mist.Def.name} thief={thief.Def.name} low={low}");
            mist.Invulnerable = true; thief.Invulnerable = true;
            mist.transform.position = p.Position + new Vector2(2.2f, 0.6f);
            thief.transform.position = p.Position + new Vector2(-1.6f, -0.5f);
            var md = mist.GetComponent<BitterMistDriver>();
            yield return null; yield return null;
            Line($"{tag}: formalArt={md.UsesFormalArt}");
            // 예고 중(1초 충전) — 파동 직전
            float until = Time.realtimeSinceStartup + 8f;
            while (!md.Telegraphing && Time.realtimeSinceStartup < until) { mist.transform.position = p.Position + new Vector2(2.2f, 0.6f); yield return null; }
            yield return new WaitForSecondsRealtime(0.5f);
            Line($"{tag}: telegraph={md.Telegraphing}");
            yield return Snap($"{tag}_telegraph");
            // 파동 + HUD 상태(이동 둔화·공속 감소·회복 차단)
            mist.transform.position = p.Position + new Vector2(1f, 0f);
            md.Pulse();
            p.Stats.BlockHeal(5f);
            yield return new WaitForSecondsRealtime(0.25f);
            Line($"{tag}: slowed={p.Stats.IsSlowed} atkSlow={p.Stats.AttackSlowed} healBlock={p.Stats.HealBlocked} status='{game.Hud.StatusLineText}'");
            yield return Snap($"{tag}_pulse_status");
            if (game.Mode == RunMode.Endless) Line($"{tag}: masterHud={(game.MasterHud != null)} brewInteractable={game.CanOpenMasterBrew}");
        }

        [UnityTest] public IEnumerator Standard_Bartender() { yield return Scenario("std_bartender", RunMode.Standard, Job.Bartender, WeaponKind.Rapier, false); }
        [UnityTest] public IEnumerator Standard_Tea() { yield return Scenario("std_tea", RunMode.Standard, Job.TeaSommelier, WeaponKind.Bow, false); }
        [UnityTest] public IEnumerator Master_Bartender() { yield return Scenario("master_bartender", RunMode.Endless, Job.Bartender, WeaponKind.Rapier, false); }
        [UnityTest] public IEnumerator Master_Tea() { yield return Scenario("master_tea", RunMode.Endless, Job.TeaSommelier, WeaponKind.Bow, false); }
        [UnityTest] public IEnumerator LowEffects_Standard_Bartender() { yield return Scenario("low_std_bartender", RunMode.Standard, Job.Bartender, WeaponKind.Rapier, true); }
    }
}
