using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-23 무기 선택 화면 "등장 몬스터" 표 QA 증거(수동 지정 실행 전용 — [Explicit]). 창·마우스·키보드·저장 데이터를 건드리지 않고
    /// 배치 모드에서 오프스크린 렌더 텍스처로 720p/1080p PNG를 남긴다. 출력: CCT_EVIDENCE_DIR.
    /// </summary>
    public class WeaponStepEvidenceCapture
    {
        string outDir;
        Camera cam;
        string saveDir;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            outDir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Delivery/Verification/weapon-step-evidence");
            Directory.CreateDirectory(outDir);
            saveDir = Path.Combine(Application.temporaryCachePath, "weapon-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDir);
            PermanentProgress.SetPathForTests(Path.Combine(saveDir, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(saveDir, "drinks.json"));
            SettingsService.SetForTests(Path.Combine(saveDir, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            var go = new GameObject("EvidenceCam");
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            if (cam != null) UnityEngine.Object.Destroy(cam.gameObject);
            RunModeSelection.Current = RunMode.Standard;
            RunModeSelection.StandardTable = SpawnTable.JobTheme;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(saveDir, true); } catch { }
            yield return null;
        }

        IEnumerator Shot(string name, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var oldOrders = canvases.Select(c => c.sortingOrder).ToList();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 1000; }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            for (int i = 0; i < canvases.Count; i++) { if (canvases[i] == null) continue; canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = null; canvases[i].sortingOrder = oldOrders[i]; }
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }

        [UnityTest, Explicit("QA 증거 프레임 — 지정 실행 전용")]
        public IEnumerator CaptureWeaponStep()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            var jobs = new[] { Job.Bartender, Job.TeaSommelier };
            var res = new[] { (1280, 720), (1920, 1080) };
            foreach (var job in jobs)
            {
                (job == Job.Bartender ? view.BartenderButton : view.TeaSommelierButton).onClick.Invoke();
                yield return null;
                foreach (var (w, h) in res)
                {
                    string tag = (job == Job.Bartender ? "bar" : "tea") + "_" + h + "p";
                    RunModeSelection.StandardTable = SpawnTable.JobTheme;
                    view.SetMode(RunMode.Standard);
                    yield return Shot(tag + "_standard_jobdefault", w, h);
                    view.ToggleSpawnTable();
                    yield return Shot(tag + "_standard_mixed", w, h);
                    if (view.SpawnTableButton != null && view.SpawnTableButton.gameObject.activeInHierarchy)
                    {
                        EventSystem.current?.SetSelectedGameObject(view.SpawnTableButton.gameObject);
                        yield return Shot(tag + "_standard_mixed_focus", w, h);
                        EventSystem.current?.SetSelectedGameObject(null);
                    }
                    view.SetMode(RunMode.Endless);
                    yield return Shot(tag + "_master", w, h);
                    view.SetMode(RunMode.Standard);
                    RunModeSelection.StandardTable = SpawnTable.JobTheme;
                }
                view.TitleBackButton?.onClick.Invoke();
                yield return null;
                if (view.WeaponBackButton != null) view.WeaponBackButton.onClick.Invoke();
                yield return null;
            }
        }
    }
}
