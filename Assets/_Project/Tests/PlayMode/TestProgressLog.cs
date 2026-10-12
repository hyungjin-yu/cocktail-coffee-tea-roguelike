using System;
using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(CCT.Tests.TestProgressLog))]

namespace CCT.Tests
{
    /// <summary>
    /// 배치 PlayMode 실행 진단용 — 테스트 시작/종료를 한 줄씩 남긴다. Unity 로그는 결과 XML이 실행 끝에만
    /// 쓰이고 매 프레임 경고로 묻히기 쉬워서, 러너가 멈췄을 때 "마지막으로 시작한 테스트"를 알 수 없었다.
    /// 실행 인자 -cctTestProgress &lt;파일&gt;이 있으면 그 파일에 즉시 flush해서 쓴다(없으면 Debug.Log만).
    /// 게임 동작과 세이브에는 관여하지 않는다.
    /// </summary>
    public class TestProgressLog : ITestRunCallback
    {
        // 정적 상태 — 에디터 Test Runner에서 도메인 리로드 없이 여러 번 실행해도 이전 실행 값이 남지 않게
        // RunStarted에서 매번 초기화하고, 이 콜백이 만든 하트비트 오브젝트는 RunStarted/RunFinished에서 직접 파괴한다.
        static string path;
        static string currentTest;
        static GameObject heartbeat;

        static void Write(string line)
        {
            line = $"{DateTime.Now:HH:mm:ss.fff} {line} (t={Time.time:F1} ts={Time.timeScale})";
            Debug.Log("[TestProgress] " + line);
            if (path == null) return;
            try { File.AppendAllText(path, line + Environment.NewLine); }
            catch (Exception) { /* 진단 실패가 테스트를 깨지 않게 */ }
        }

        public void RunStarted(ITest testsToRun)
        {
            DestroyHeartbeat(); // 이전 실행이 RunFinished 없이 끝났어도 하트비트가 둘이 되지 않게
            path = null;
            currentTest = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-cctTestProgress") path = args[i + 1];
            Write($"RUN START {testsToRun.TestCaseCount} cases");
            // 정지 감시는 진행 파일 인자와 무관하게 항상 켠다(에디터 Test Runner 실행도 무한 대기하지 않게).
            // 단, 이 콜백은 EditMode 실행에서도 불린다 — 2026-10-06 edit1: 에디트 모드의 DontDestroyOnLoad 예외로 EditMode
            // 러너 전체가 RunError(종료 코드 3, XML 없음)로 끝났다. 하트비트는 플레이 모드에서만 만든다.
            if (!Application.isPlaying) return;
            heartbeat = new GameObject("TestProgressHeartbeat") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(heartbeat);
            heartbeat.AddComponent<Heartbeat>();
        }

        static void DestroyHeartbeat()
        {
            if (heartbeat != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(heartbeat);
                else UnityEngine.Object.DestroyImmediate(heartbeat);
            }
            heartbeat = null;
        }

        /// <summary>실제 시간 5초마다 시간/정지/게임 상태를 남긴다 — 스케일 시간 대기(WaitForSeconds)가 멈췄는지 구분용.
        /// 한 테스트 안에서 timeScale=0이 StallSeconds(실제 시간) 넘게 이어지면 LogError로 그 테스트를 실패시키고
        /// timeScale을 1로 풀어 러너가 다음 테스트로 넘어가게 한다. UTF 기본 180초 타임아웃은 WaitForSeconds에
        /// 묶인 코루틴이 양보하지 않으면 검사되지 않아(2026-10-05 재현) 러너 전체가 무한 대기했다.</summary>
        class Heartbeat : MonoBehaviour
        {
            const float StallSeconds = 30f;
            float next;
            float frozenSince = -1f;
            string frozenTest;

            void LateUpdate()
            {
                if (currentTest == null || Time.timeScale > 0f) { frozenSince = -1f; return; }
                if (frozenSince < 0f || frozenTest != currentTest)
                {
                    frozenSince = Time.realtimeSinceStartup;
                    frozenTest = currentTest;
                    return;
                }
                if (Time.realtimeSinceStartup - frozenSince < StallSeconds) return;
                var gm = GameManager.Instance;
                Write($"STALL {currentTest} timeScale=0 for {StallSeconds}s state={(gm != null ? gm.State.ToString() : "noGM")} — failing and unfreezing");
                Debug.LogError($"[TestProgress] STALL: {currentTest} — timeScale가 실제 {StallSeconds}초 동안 0 " +
                               $"(state={(gm != null ? gm.State.ToString() : "noGM")}). 스케일 시간 대기가 끝날 수 없어 실패 처리하고 timeScale=1로 푼다.");
                Time.timeScale = 1f;
                frozenSince = -1f;
            }

            void Update()
            {
                if (Time.realtimeSinceStartup < next) return;
                next = Time.realtimeSinceStartup + 5f;
                var gm = GameManager.Instance;
                Write($"HB frame={Time.frameCount} dt={Time.deltaTime:F4} unscaledDt={Time.unscaledDeltaTime:F4} " +
                      $"state={(gm != null ? gm.State.ToString() : "noGM")} enemies={Enemy.Active.Count}");
            }
        }

        public void RunFinished(ITestResult testResults)
        {
            currentTest = null;
            Write($"RUN END pass={testResults.PassCount} fail={testResults.FailCount} skip={testResults.SkipCount}");
            DestroyHeartbeat();
            path = null; // 다음 실행(인자 없을 수 있음)이 이 파일에 이어 쓰지 않게
        }

        public void TestStarted(ITest test)
        {
            if (test.IsSuite) return;
            currentTest = test.FullName;
            Write("START " + test.FullName);
        }

        public void TestFinished(ITestResult result)
        {
            if (!result.Test.IsSuite) currentTest = null;
            if (!result.Test.IsSuite) Write($"END {result.ResultState} {result.Duration:F1}s {result.Test.FullName}");
        }
    }
}
