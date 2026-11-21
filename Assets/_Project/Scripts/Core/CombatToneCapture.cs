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
        string scene = "normal";
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
            // 2026-11-20 같은 빌드 전후 비교: -cct-tone-original = 보정 전 원본 bg_bar+기존 톤, -cct-tone-scene normal|crowd|boss|drop = 측정 장면 fixture
            CafeBackground.ForceOriginalBarForQa = Array.IndexOf(args, "-cct-tone-original") >= 0;
            if (Array.IndexOf(args, "-cct-tone-noboss-ground") >= 0) CafeBackground.BossGroundBoostForQa = false; // 보스 접지 보완 전후 비교
            int si = Array.IndexOf(args, "-cct-tone-scene");
            c.scene = si >= 0 && si + 1 < args.Length ? args[si + 1] : "normal";
            c.StartCoroutine(c.Run());
        }

        void Stage(GameManager game)
        {
            switch (scene)
            {
                case "crowd":
                    game.Spawner.SpawnWaveForTests(); game.Spawner.SpawnWaveForTests();
                    break;
                case "boss":
                    var spec = game.Director.NextSpec ?? BossDirector.DefaultSchedule(Job.Bartender)[0];
                    game.Director.SpawnNow(spec);
                    break;
                case "ranged":
                case "rangedcrowd":
                    // 적탄 표본: 실제 원거리 적(칵테일 트랙 ranged)을 양쪽에서 등장시켜 실제 RangedAttackDriver 발사 경로로 쏘게 한다(측정 전용 강제 등장)
                    for (int k = 0; k < 6; k++) game.Spawner.SpawnMaster(EnemyDef.ShakerGolem, 1f, 1f, false, false, k % 2 == 0 ? -1f : 1f);
                    if (scene == "rangedcrowd") { game.Spawner.SpawnWaveForTests(); game.Spawner.SpawnWaveForTests(); }
                    break;
                case "drop":
                    // 이미 화면에 들어온 적을 처치해 화면 안 드롭을 만들고, 새 웨이브로 적·드롭이 함께 보이게
                    foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                        if (e != null && !e.Def.IsBoss) e.TakeDamage(99999f);
                    game.Spawner.SpawnWaveForTests();
                    break;
            }
        }

        /// <summary>보스 실루엣 마스크(측정 전용): 같은 프레임 구도에서 보스 몸체만 흰색으로, 나머지는 검정으로 한 장 더 찍는다(가장자리 대비 계산용).</summary>
        IEnumerator BossMask(string name)
        {
            Enemy boss = null;
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) if (e != null && e.Def.IsBoss) boss = e;
            if (boss == null) yield break;
            var cam = Camera.main;
            float ts = Time.timeScale; Time.timeScale = 0f;
            var prevFlags = cam.clearFlags; var prevBg = cam.backgroundColor;
            var hidden = new List<Renderer>(); var recolor = new List<(SpriteRenderer, Color)>(); var canv = new List<Canvas>();
            SpriteRenderer body = null;
            foreach (var r in boss.GetComponentsInChildren<SpriteRenderer>()) if (r.enabled && r.sprite != null && r.gameObject.name != "Shadow") { body = r; break; }
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.enabled && r != body) { r.enabled = false; hidden.Add(r); }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.enabled) { c.enabled = false; canv.Add(c); }
            if (body != null) { recolor.Add((body, body.color)); body.color = Color.white; }
            var mat = body != null ? body.material : null; Shader prevShader = mat != null ? mat.shader : null;
            var flat = Shader.Find("GUI/Text Shader"); if (mat != null && flat != null) mat.shader = flat; // 스프라이트 알파를 흰색 단색으로
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + "_bossmask.png"));
            yield return null; yield return null;
            if (mat != null && prevShader != null) mat.shader = prevShader;
            foreach (var (r, c) in recolor) r.color = c;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            foreach (var c in canv) if (c != null) c.enabled = true;
            cam.clearFlags = prevFlags; cam.backgroundColor = prevBg; Time.timeScale = ts;
        }

        IEnumerator Run()
        {
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            // 입력을 쓰지 않는 측정: 실제 입력 장치를 꺼 사용자의 다른 창 입력이 들어오지 않게
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices.ToArray()) UnityEngine.InputSystem.InputSystem.DisableDevice(d);
            QuitGuard.AllowNextQuit(); // 격리 측정 창: OS 창 닫기·자동 종료가 미저장 확인창에 막히지 않게(사용자 저장 무관)
            QaBanner.Show($"QA 자동 측정 중 · 사람 플레이 아님 · 칵테일바 전투 배경 밝기 측정({(CafeBackground.ForceOriginalBarForQa ? "보정 전" : "보정 후")}·{scene} fixture·보스접지 {(CafeBackground.BossGroundBoostForQa ? "on" : "off")})");
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (var t in FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Destroy(t.gameObject);
            yield return null;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            json.Append("{\n  \"screen\": \"").Append(Screen.width).Append('x').Append(Screen.height)
                .Append("\", \"map\": \"").Append(CafeBackground.ActiveMapPrefix).Append("\", \"warmBar\": \"").Append(CafeBackground.UsesWarmBar).Append("\", \"scene\": \"").Append(scene).Append("\", \"forcedOriginal\": \"").Append(CafeBackground.ForceOriginalBarForQa).Append("\", \"effects\": \"").Append(SettingsService.Current.effectsLevel)
                .Append("\",\n  \"tone\": {\"floor\": \"").Append(CafeBackground.ActiveTone.floor).Append("\", \"wall\": \"").Append(CafeBackground.ActiveTone.wall)
                .Append("\", \"veil\": \"").Append(CafeBackground.ActiveTone.veil).Append("\"},\n  \"shots\": [\n");
            bool first = true;
            float start = Time.realtimeSinceStartup;
            // normal = 기존 시각 표본. 그 외 fixture는 2초에 장면을 만들고(군중: 웨이브 2회 / 보스: 다음 예정 보스 즉시 등장 / 드롭: 웨이브 후 화면 안 적 처치)
            // 레벨업 일시정지 전에 짧게 캡처한다. 판정·스폰 규칙 변경 아님(측정 전용 강제 장면).
            var times = scene == "normal" ? new[] { 2.5f, 8f, 16f, 26f } : scene == "boss" ? new[] { 2.0f, 5.5f, 7.5f }
                : scene.StartsWith("ranged") ? new[] { 2.0f, 4.2f, 4.9f, 5.6f, 6.3f } : new[] { 2.0f, 3.2f, 4.4f };
            bool staged = scene == "normal";
            foreach (var at in times)
            {
                while (Time.realtimeSinceStartup - start < at) yield return null;
                if (game == null) break;
                if (!staged) { staged = true; Stage(game); yield return null; }
                yield return new WaitForEndOfFrame();
                string name = $"tone_{scene}_t{at:00.0}_{Screen.width}x{Screen.height}";
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
                if (scene == "boss" && at == times[times.Length - 1]) yield return BossMask(name);
            }
            json.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "tone.json"), json.ToString(), new UTF8Encoding(false));
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
