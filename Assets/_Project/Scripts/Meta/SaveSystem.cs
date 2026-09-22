using System.IO;
using UnityEngine;

namespace CCT
{
    /// <summary>영구 데이터(SaveData)를 JSON 파일로 읽고 쓴다 (기획서 13장: "영구 데이터 = JSON 저장").</summary>
    public static class SaveSystem
    {
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>파일이 없거나 손상돼도 실패하지 않고 빈 SaveData를 돌려준다.</summary>
        public static SaveData Load(string path = null)
        {
            path = string.IsNullOrEmpty(path) ? DefaultPath : path;
            try
            {
                if (!File.Exists(path)) return new SaveData();
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                return data ?? new SaveData();
            }
            catch
            {
                return new SaveData();
            }
        }

        /// <summary>저장 실패(권한/디스크 문제 등)는 조용히 무시한다 — 세이브 오류로 게임 진행이 막히면 안 됨.</summary>
        public static void Save(SaveData data, string path = null)
        {
            path = string.IsNullOrEmpty(path) ? DefaultPath : path;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(data));
            }
            catch
            {
                // 저장 실패는 무시 — 다음 저장 시점에 다시 시도된다.
            }
        }
    }
}
