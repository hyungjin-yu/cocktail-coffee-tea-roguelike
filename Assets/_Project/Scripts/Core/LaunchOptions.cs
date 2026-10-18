using System;
using System.IO;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 실행 인자(빌드 검증용):
    /// - -cct-data-dir &lt;폴더&gt;: save.json/settings.json을 그 폴더에서만 읽고 쓴다(실제 사용자 진행 보존).
    ///   빈 폴더면 구형 PlayerPrefs 설정도 이전하지 않는다(메모리 구형 저장소).
    /// - -cct-autotour &lt;폴더&gt; [-cct-autotour-mode screens|motion|all|audio]: 화면·모션·오디오 자동 투어(AutoTour). -cct-data-dir 필수.
    ///   QA 투어 빌드(CCT_QA_TOUR)에서만 동작한다.
    /// 인자가 없으면 아무것도 바꾸지 않는다.
    /// </summary>
    public static class LaunchOptions
    {
        public static string DataDir { get; private set; }
        public static string AutoTourDir { get; private set; }

        public static void Apply() => Apply(Environment.GetCommandLineArgs());

        public static void Apply(string[] args)
        {
            DataDir = Value(args, "-cct-data-dir");
            // 69 테스트 재료 지급 버튼 — 격리 저장(-cct-data-dir)에서만 켠다(실제 사용자 저장에는 반복 지급 불가).
            DrinkLab.DevToolsEnabled = !string.IsNullOrEmpty(DataDir) && Array.Exists(args, a => a == "-cct-drinklab-dev");
            AutoTourDir = Value(args, "-cct-autotour");
            if (!string.IsNullOrEmpty(DataDir))
            {
                Directory.CreateDirectory(DataDir);
                PermanentProgress.SetPathForTests(Path.Combine(DataDir, "save.json"));
                SettingsService.SetForTests(Path.Combine(DataDir, "settings.json"), new MemoryLegacySettingsStore(), null);
                Debug.Log($"[Launch] data dir: {DataDir}");
            }
#if CCT_PLAY_VERIFIER
            // 정상 플레이 검증(입력 대행) — 검증 빌드 심볼에서만 컴파일되고, -cct-data-dir 없이는 켜지지 않는다.
            var playVerifyDir = Value(args, "-cct-playverify");
            if (!string.IsNullOrEmpty(playVerifyDir))
            {
                if (string.IsNullOrEmpty(DataDir) || !string.IsNullOrEmpty(AutoTourDir))
                {
                    Debug.LogError("[Launch] -cct-playverify는 -cct-data-dir가 필요하고 -cct-autotour와 함께 쓸 수 없다 — 실행하지 않는다");
                }
                else
                {
                    var pv = new GameObject("PlayVerifier");
                    UnityEngine.Object.DontDestroyOnLoad(pv);
                    pv.AddComponent<PlayVerifier>().Init(playVerifyDir, Value(args, "-cct-playverify-weapon"),
                        Value(args, "-cct-playverify-runs"), Array.Exists(args, a => a == "-cct-playverify-abandon"),
                        Value(args, "-cct-playverify-buy"), Array.Exists(args, a => a == "-cct-playverify-die-after-enhanced"),
                        Array.Exists(args, a => a == "-cct-playverify-reckless"));
                }
            }
#endif
#if CCT_QA_LOAD
            // 10 §3 고정 부하 QA 시나리오 — 검증 빌드 심볼에서만 컴파일, 격리 저장(-cct-data-dir) 없이는 켜지지 않는다.
            var loadDir = Value(args, "-cct-loadtest");
            if (!string.IsNullOrEmpty(loadDir))
            {
                if (string.IsNullOrEmpty(DataDir) || !string.IsNullOrEmpty(AutoTourDir))
                {
                    Debug.LogError("[Launch] -cct-loadtest는 -cct-data-dir가 필요하고 -cct-autotour와 함께 쓸 수 없다 — 실행하지 않는다");
                }
                else
                {
                    var lt = new GameObject("LoadScenario");
                    UnityEngine.Object.DontDestroyOnLoad(lt);
                    lt.AddComponent<LoadScenario>().Init(loadDir, Value(args, "-cct-loadtest-warmup"),
                        Value(args, "-cct-loadtest-seconds"), Value(args, "-cct-loadtest-restarts"));
                }
            }
#endif
            if (!string.IsNullOrEmpty(AutoTourDir))
            {
                if (string.IsNullOrEmpty(DataDir))
                {
                    Debug.LogError("[Launch] -cct-autotour는 실제 세이브 보호를 위해 -cct-data-dir가 필요하다 — 투어를 실행하지 않는다");
                    AutoTourDir = null;
                    return;
                }
#if CCT_QA_TOUR
                Directory.CreateDirectory(AutoTourDir);
                var go = new GameObject("AutoTour");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AutoTour>().Init(AutoTourDir, Value(args, "-cct-autotour-mode"));
#else
                // 자동 투어는 QA 검증 빌드(CCT_QA_TOUR)에만 들어간다 — 일반 프리뷰/출시 빌드에서는 인자를 무시한다.
                Debug.LogError("[Launch] -cct-autotour는 이 빌드에 포함되지 않았다(QA 투어 빌드 전용) — 실행하지 않는다");
                AutoTourDir = null;
#endif
            }
        }

        static string Value(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
