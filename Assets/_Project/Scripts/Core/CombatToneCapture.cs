using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-18 칵테일바 전투 배경 톤 전후 측정(명시 인자 + 격리 저장에서만): <c>-cct-data-dir &lt;격리&gt; -cct-tone-capture &lt;출력&gt;</c>.
    /// 바텐더(바 맵) 정상 런을 만들어 일정 시각에 화면을 캡처하고, 적·적 탄·재료 드롭의 화면 사각형을 JSON으로 남긴다(대비 계산용).
    /// 입력 대행·무적·스폰 조작 없음 — 시각 비교용 표본이며 사람 체감 판정이 아니다.
    /// </summary>
    public class CombatToneCapture : MonoBehaviour
    {
        string dir;
        readonly StringBuilder json = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cct-tone-capture");
            if (i < 0 || i + 1 >= args.Length || Array.IndexOf(args, "-cct-data-dir") < 0) return;
            var go = new GameObject("CombatToneCapture");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<CombatToneCapture>();
            c.dir = args[i + 1];
            c.StartCoroutine(c.Run());
        }

        IEnumerator Run()
        {
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            // 입력을 쓰지 않는 측정: 실제 입력 장치를 꺼 사용자의 다른 창 입력이 들어오지 않게
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices.ToArray()) UnityEngine.InputSystem.InputSystem.DisableDevice(d);
            QaBanner.Show("QA 자동 측정 중 · 사람 플레이 아님 · 칵테일바 전투 배경 밝기 측정(입장 흐름과 무관)");
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var t in FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(t.gameObject);
            yield return null;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            json.Append("{\n  \"screen\": \"").Append(Screen.width).Append('x').Append(Screen.height)
                .Append("\", \"map\": \"").Append(CafeBackground.ActiveMapPrefix).Append("\", \"warmBar\": \"").Append(CafeBackground.UsesWarmBar).Append("\", \"effects\": \"").Append(SettingsService.Current.effectsLevel)
                .Append("\",\n  \"tone\": {\"floor\": \"").Append(CafeBackground.ActiveTone.floor).Append("\", \"wall\": \"").Append(CafeBackground.ActiveTone.wall)
                .Append("\", \"veil\": \"").Append(CafeBackground.ActiveTone.veil).Append("\"},\n  \"shots\": [\n");
            bool first = true;
            float start = Time.realtimeSinceStartup;
            foreach (var at in new[] { 2.5f, 8f, 16f, 26f })
            {
                while (Time.realtimeSinceStartup - start < at) yield return null;
                if (game == null) break;
                yield return new WaitForEndOfFrame();
                string name = $"tone_t{at:00.0}_{Screen.width}x{Screen.height}";
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
                if (!first) json.Append(",\n");
                first = false;
                json.Append("    {\"file\": \"").Append(name).Append(".png\", \"paused\": ").Append(Time.timeScale == 0f ? "true" : "false").Append(", \"objects\": [");
                var cam = Camera.main;
                var items = new List<string>();
                void Add(string kind, Component c)
                {
                    foreach (var r in c.GetComponentsInChildren<SpriteRenderer>())
                    {
                        if (!r.enabled || r.sprite == null || r.color.a < 0.2f) continue;
                        var b = r.bounds;
                        var a = cam.WorldToScreenPoint(b.min); var z = cam.WorldToScreenPoint(b.max);
                        if (z.x < 0 || a.x > Screen.width || z.y < 0 || a.y > Screen.height) continue;
                        items.Add($"{{\"kind\": \"{kind}\", \"rect\": [{a.x:0},{a.y:0},{z.x:0},{z.y:0}]}}");
                        break; // 개체당 본체 하나
                    }
                }
                foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Add("enemy", e);
                foreach (var p in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Add("projectile", p);
                foreach (var d in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)) Add("drop", d);
                json.Append(string.Join(", ", items)).Append("]}");
                yield return null; yield return null;
            }
            json.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "tone.json"), json.ToString(), new UTF8Encoding(false));
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
