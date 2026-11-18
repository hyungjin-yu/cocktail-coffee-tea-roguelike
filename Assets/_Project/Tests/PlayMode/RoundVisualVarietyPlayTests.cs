using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-09 "라운드 내내 그래픽 변화가 없어 살짝 무료": 실제 진행(보스 수·후반 시각·웨이브·정예·보스 등장/처치)에 따라
    /// 기존 배경 자산의 구성(후광·김·벽/막 깊이)과 화면 가장자리 진입 표시가 바뀌는지, 상한·재사용·효과 낮음·일시정지·재시작 복구.
    /// </summary>
    public class RoundVisualVarietyPlayTests
    {
        GameManager game;
        readonly List<string> folders = new List<string>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            NewSave();
            SettingsService.SetForTests(Path.Combine(folders[0], "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var f in folders) try { Directory.Delete(f, true); } catch { }
            folders.Clear();
        }

        void NewSave()
        {
            var f = Path.Combine(Application.temporaryCachePath, "round-variety-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(f);
            folders.Add(f);
            PermanentProgress.SetPathForTests(Path.Combine(f, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(f, "drinks.json"));
        }

        IEnumerator StartRun(Job job = Job.Bartender)
        {
            if (game != null) { UnityEngine.Object.Destroy(game.gameObject); game = null; yield return null; }
            NewSave();
            game = GameBootstrap.Build(job, job == Job.TeaSommelier ? WeaponKind.Bow : WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            game.Director.enabled = false;
            foreach (var w in game.Player.GetComponentsInChildren<RapierWeapon>()) w.enabled = false;
            foreach (var w in game.Player.GetComponentsInChildren<PlayerProjectileWeapon>()) w.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            yield return null;
        }

        static bool Darker(Color a, Color b) => a.r < b.r - 1e-4f || a.g < b.g - 1e-4f || a.b < b.b - 1e-4f;

        [UnityTest]
        public IEnumerator Stages_FollowRealProgress_CompositionDiffers_BossTransitionAndRelief()
        {
            yield return StartRun();
            var amb = game.Ambience;
            Assert.IsNotNull(amb);
            Assert.AreSame(amb, RunAmbience.Attach(amb.transform, game), "같은 배경에 중복 연출기 없음");
            Assume.That(amb.LampCount, Is.GreaterThan(0), "직업 맵 아트가 있어야 하는 시험");
            Assert.AreEqual(amb.LampCount, amb.HaloCount, "램프마다 후광 하나(런 시작 때 한 번 생성)");
            int objectsBefore = amb.transform.GetComponentsInChildren<Transform>(true).Length;

            yield return null;
            Assert.AreEqual(RunStage.Opening, amb.Stage);
            Assert.IsFalse(amb.HaloVisible(0), "시작: 후광 없음(깨끗한 기본 장면)");
            Assert.AreEqual(amb.CounterCount, amb.CounterGlowCount, "카운터마다 조명 하나");
            if (amb.CounterGlowCount > 0) Assert.IsFalse(amb.CounterGlowVisible(0), "시작: 카운터 조명 없음");
            Assert.AreEqual(0f, amb.VignetteAlpha, "시작: 화면 끝 어둠 없음");
            for (int i = 0; i < amb.WallCount; i++) Assert.AreEqual(amb.WallBaseColor(i), amb.WallColor(i));

            // 첫 보스 실제 등장(BossDirector.SpawnNow) → 전환 + 가장자리 표시 + 보스전 구성.
            var boss = game.Director.SpawnNow(game.Director.NextSpec);
            yield return null;
            Assert.IsTrue(amb.BossTransitionActive, "보스 등장 전환(램프 깜빡 → 붉게)");
            Assert.AreEqual(1, amb.ActiveCues, "실제 등장 쪽 진입 표시 하나");
            Assert.AreEqual(RunPhaseState.BossFight, amb.State);
            Assert.IsTrue(amb.HaloVisible(0));
            Color bossHalo = amb.HaloColor(0);
            Assert.Greater(bossHalo.r - bossHalo.g, 0.4f, "보스전 후광은 붉다");
            Assert.AreEqual(0, amb.LiveWisps, "보스 중엔 배경 김 없음");
            Assert.AreEqual(1f, Time.timeScale, "전환은 timeScale을 바꾸지 않는다");
            Assert.AreEqual(0f, CameraRig.CurrentShakeAmplitude, "전환은 카메라를 흔들지 않는다");

            // 보스 퇴장(ActiveBoss 해제) → 0.4초 밝힘 → 중반 구성(따뜻한 후광, 벽 원래 색). 처치 보상 창(timeScale 0)은 이 시험 범위 밖이라
            // 보스 오브젝트를 직접 치우고 ActiveBoss를 비운다 — RunAmbience가 보는 실제 신호는 ActiveBoss뿐이다.
            UnityEngine.Object.Destroy(boss.gameObject);
            game.ActiveBoss = null;
            yield return null;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(RunStage.Midservice, amb.Stage, "보스 1회 등장 후 중반");
            Assert.IsTrue(amb.HaloVisible(0), "중반: 따뜻한 후광");
            Color mid = amb.HaloColor(0);
            Assert.Less(Mathf.Abs(mid.r - 1f), 0.01f);
            Assert.Greater(mid.g, 0.7f, "중반 후광은 붉지 않다");
            for (int i = 0; i < amb.WallCount; i++) Assert.AreEqual(amb.WallBaseColor(i), amb.WallColor(i), "중반 벽은 원래 색");
            if (amb.CounterGlowCount > 0) Assert.IsTrue(amb.CounterGlowVisible(0), "중반: 카운터 조명");
            float midShade = amb.VignetteAlpha;
            Assert.Greater(midShade, 0f, "중반: 화면 끝 옅은 어둠");

            // 후반(실제 후반 스폰 배가 시각) → 벽 어둡게 + 막 +0.04 + 호박색 후광.
            game.SetRunTimeForTests(Balance.LateSpawnStartSeconds + 1f);
            yield return null;
            Assert.AreEqual(RunStage.LateNight, amb.Stage);
            if (amb.WallCount > 0) Assert.IsTrue(Darker(amb.WallColor(0), amb.WallBaseColor(0)), "후반 벽이 깊어진다");
            if (amb.HasVeil) Assert.AreEqual(amb.VeilBaseColor.a + RunAmbience.LateVeilAlpha, amb.VeilColor.a, 1e-4f);
            Assert.Less(amb.HaloColor(0).g, mid.g, "후반 후광은 더 짙은 호박색");
            Assert.Greater(amb.VignetteAlpha, midShade, "후반: 화면 끝 어둠이 더 진함");
            Assert.LessOrEqual(amb.VignetteAlpha, 0.26f + 1e-4f, "가장자리 어둠 상한(가운데 전투 구역은 건드리지 않음)");
            Assert.AreEqual(objectsBefore, amb.transform.GetComponentsInChildren<Transform>(true).Length - amb.CueObjects * 3 - amb.LiveWisps,
                "단계 전환은 오브젝트를 새로 만들지 않는다(진입 표시 풀·김 제외)");
        }

        [UnityTest]
        public IEnumerator Wisps_OnlyMidLate_CappedAndOffOnLowEffects()
        {
            yield return StartRun();
            var amb = game.Ambience;
            Assume.That(amb.CounterCount, Is.GreaterThan(0));
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(0, amb.WispsSpawned, "시작 단계엔 김 없음");
            game.SetRunTimeForTests(Balance.LateSpawnStartSeconds + 1f);
            float t0 = Time.realtimeSinceStartup;
            while (amb.WispsSpawned < 3 && Time.realtimeSinceStartup - t0 < 12f) yield return null;
            Assert.Greater(amb.WispsSpawned, 0, "후반: 카운터 김");
            Assert.LessOrEqual(amb.LiveWisps, RunAmbience.MaxWisps, "동시 상한");
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            int n = amb.WispsSpawned;
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(n, amb.WispsSpawned, "효과 낮음: 김 없음");
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        [UnityTest]
        public IEnumerator WaveAndEliteCues_UseRealSpawnerEvents_PooledAndPauseFrozen()
        {
            yield return StartRun();
            var amb = game.Ambience;
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            yield return null; // 구독
            spawner.SpawnWaveForTests(); // 실제 웨이브 경로(규칙·수 불변)
            Assert.Greater(spawner.WaveSpawns, 0);
            Assert.Greater(amb.ActiveCues, 0, "실제 웨이브 → 가장자리 표시");
            Assert.AreEqual(spawner.LastWaveSides == "양쪽" ? 4 : 2, amb.ActiveCues, $"실제로 나온 쪽({spawner.LastWaveSides})만, 쪽마다 둘");
            for (int i = 0; i < 10; i++) amb.OnWaveForTests(true, true, 20);
            Assert.LessOrEqual(amb.CueObjects, RunAmbience.CuePool, "표시 오브젝트 풀 상한");

            int shown = amb.CuesShown;
            amb.OnEliteForTests(EnemyDef.Imp, new Vector2(20f, -2f));
            amb.OnEliteForTests(EnemyDef.Imp, new Vector2(20f, -2f));
            Assert.AreEqual(shown + 1, amb.CuesShown, "정예 표시 2초 쿨다운");

            game.OpenPause();
            int active = amb.ActiveCues;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(active, amb.ActiveCues, "일시정지 중 표시 시간 정지");
            game.ClosePause();
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(0, amb.ActiveCues, "짧게 사라진다");

            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            amb.OnWaveForTests(true, false, 30);
            Assert.AreEqual(1, amb.ActiveCues, "효과 낮음: 쪽마다 하나");
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        [UnityTest]
        public IEnumerator Restart_RestoresOpeningScene_NoSharedMaterialChange_TeaMapToo()
        {
            yield return StartRun();
            var amb = game.Ambience;
            game.SetRunTimeForTests(Balance.LateSpawnStartSeconds + 1f);
            yield return null;
            Assert.AreEqual(RunStage.LateNight, amb.Stage);
            var lampMat = amb.LampCount > 0 ? amb.GetComponentsInChildren<SpriteRenderer>().First(r => r.gameObject.name == "Lamp").sharedMaterial : null;
            if (lampMat != null) StringAssert.DoesNotContain("(Instance)", lampMat.name, "공유 머티리얼 복제/변경 없음(색은 SpriteRenderer.color)");

            yield return StartRun(Job.TeaSommelier); // 재시작(다른 맵)
            var tea = game.Ambience;
            Assert.AreNotSame(amb, tea);
            yield return null;
            Assert.AreEqual(RunStage.Opening, tea.Stage, "새 런은 시작 구성");
            for (int i = 0; i < tea.LampCount; i++) Assert.AreEqual(tea.LampBaseColor(i), tea.LampColor(i));
            if (tea.HasVeil) Assert.AreEqual(tea.VeilBaseColor, tea.VeilColor);
            Assert.AreEqual(tea.LampCount, tea.HaloCount);
        }

        [Test]
        public void StageFor_UsesOnlyRealCounters()
        {
            Assert.AreEqual(RunStage.Opening, RunAmbience.StageFor(null));
        }
    }

    /// <summary>시작·중반·후반·보스를 같은 카메라 위치에서 비교 캡처(지정 실행 전용, 창·포커스 없이 오프스크린).</summary>
    public class RoundVisualVarietyEvidence
    {
        [UnityTest, Explicit("QA 증거 프레임 — 지정 실행 전용")]
        public IEnumerator CaptureSameViewStages()
        {
            string outDir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Delivery/Verification/2026-11-09/round-visual-variety/frames");
            Directory.CreateDirectory(outDir);
            var folder = Path.Combine(Application.temporaryCachePath, "round-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            var notes = new List<string>();
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                var game = GameBootstrap.Build(job, job == Job.TeaSommelier ? WeaponKind.Bow : WeaponKind.Rapier);
                yield return null;
                foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
                game.Director.enabled = false;
                foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
                var amb = game.Ambience;
                notes.Add($"{job}: lamps={amb.LampCount} counters={amb.CounterCount} walls={amb.WallCount} veil={amb.HasVeil} map={CafeBackground.ActiveMapPrefix}");
                yield return new WaitForSeconds(0.5f);
                yield return Shot(outDir, $"{job}_1_opening", notes, game);
                game.SetRunTimeForTests(200f);
                var b1 = game.Director.SpawnNow(game.Director.NextSpec);
                yield return new WaitForSeconds(0.25f);
                yield return Shot(outDir, $"{job}_4a_boss_transition", notes, game);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(outDir, $"{job}_4b_boss_fight", notes, game);
                UnityEngine.Object.Destroy(b1.gameObject); game.ActiveBoss = null; // 보상 창 없이 보스 퇴장 신호만
                yield return null;
                yield return new WaitForSeconds(4f); // 중반 김 한 번 이상
                yield return Shot(outDir, $"{job}_2_midservice", notes, game);
                game.SetRunTimeForTests(Balance.LateSpawnStartSeconds + 5f);
                yield return new WaitForSeconds(3f);
                yield return Shot(outDir, $"{job}_3_latenight", notes, game);
                game.GetComponentInChildren<EnemySpawner>().SpawnWaveForTests();
                yield return new WaitForSeconds(0.15f);
                yield return Shot(outDir, $"{job}_5_wave_edge_cues", notes, game);
                UnityEngine.Object.Destroy(game.gameObject);
                yield return null;
            }
            File.WriteAllLines(Path.Combine(outDir, "frames-notes.txt"), notes);
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        static IEnumerator Shot(string outDir, string name, List<string> notes, GameManager game)
        {
            const int W = 1280, H = 720;
            var cam = Camera.main;
            // 같은 시점 비교: 카메라를 매번 같은 x(0)에 둔다(CameraRig가 다음 프레임에 다시 따라가기 전 렌더).
            var rt = new RenderTexture(W, H, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var oldOrders = canvases.Select(c => c.sortingOrder).ToList();
            // 실제 게임의 오버레이처럼 HUD가 월드 스프라이트 위에 그려지게 정렬 순서를 잠시 올린다(캡처 전용).
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 1000; }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            var p = cam.transform.position;
            cam.transform.position = new Vector3(0f, p.y, p.z);
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            for (int ci = 0; ci < canvases.Count; ci++) { canvases[ci].renderMode = RenderMode.ScreenSpaceOverlay; canvases[ci].worldCamera = null; canvases[ci].sortingOrder = oldOrders[ci]; }
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            var amb = game.Ambience;
            notes.Add($"{name}.png | stage={amb.Stage} state={amb.State} halo={(amb.HaloCount > 0 ? amb.HaloVisible(0) + " " + amb.HaloColor(0) : "-")} wisps={amb.LiveWisps} cues={amb.ActiveCues} badge='{game.Hud.PhaseBadge?.Text}' strip={game.Hud.EffectsStrip?.VisibleTiles}");
        }
    }
}
