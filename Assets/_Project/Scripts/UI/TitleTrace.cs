using System;
using System.IO;
using UnityEngine;

namespace CCT
{
    /// <summary>2026-11-18 첫 화면 입력·상태 추적(기록 전용, 입력 대행 없음): <c>-cct-title-trace &lt;파일&gt;</c>일 때만 쓴다.
    /// 입장 상태 전환, 기본 선택, RequestStart 호출(호출 경로 스택 포함)을 프레임·시간과 함께 남긴다.</summary>
    public static class TitleTrace
    {
        static string path;
        static bool init;

        static void Init()
        {
            if (init) return;
            init = true;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cct-title-trace");
            if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
        }

        public static void Log(string msg, bool withStack = false)
        {
            Init();
            if (path == null) return;
            try
            {
                var line = $"[{Time.realtimeSinceStartup:0.000}s f{Time.frameCount}] {msg}";
                if (withStack) line += "\n    " + Environment.StackTrace.Replace("\n", "\n    ");
                File.AppendAllText(path, line + "\n");
            }
            catch { }
        }
    }
}
