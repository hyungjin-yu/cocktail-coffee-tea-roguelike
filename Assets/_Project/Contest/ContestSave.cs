using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CCT.Contest
{
    [Serializable]
    public sealed class MissionRecord
    {
        public string id;
        public bool completed;
        public bool badgeGranted;
        public int bestStars;
        public int bestMinutes;
        public int plays;
    }

    [Serializable]
    public sealed class ContestSaveData
    {
        public int version = 1;
        public string product = "pps-contest-2026";
        public int badges;
        public List<MissionRecord> missions = new List<MissionRecord>();
    }

    /// <summary>공모전판 전용 저장. 본편 save.json/settings.json/PlayerPrefs를 읽거나 쓰지 않는다.
    /// 경로: Application.persistentDataPath(공모전 회사/제품명으로 분리)/pps_contest_save.json,
    /// 또는 실행 인자 -pps-data-dir &lt;폴더&gt;. 쓰기는 임시 파일 → 교체(.bak 보관), 읽기 실패 시 .bak → 새 데이터.</summary>
    public sealed class ContestSave
    {
        public const string FileName = "pps_contest_save.json";
        public readonly string Path;
        public ContestSaveData Data { get; private set; }
        public string LoadNote { get; private set; }
        readonly HashSet<DeliveryReceipt> committed = new HashSet<DeliveryReceipt>();

        public ContestSave(string path)
        {
            Path = path;
            Load();
        }

        public static string DefaultDirectory()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pps-data-dir");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            return Application.persistentDataPath;
        }

        public static ContestSave OpenDefault()
        {
            var dir = DefaultDirectory();
            Directory.CreateDirectory(dir);
            return new ContestSave(System.IO.Path.Combine(dir, FileName));
        }

        void Load()
        {
            if (TryRead(Path, out var d)) { Data = d; LoadNote = "ok"; return; }
            if (TryRead(Path + ".bak", out d)) { Data = d; LoadNote = "backup"; return; }
            Data = new ContestSaveData();
            LoadNote = File.Exists(Path) ? "reset-corrupt" : "new";
        }

        static bool TryRead(string path, out ContestSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                var d = JsonUtility.FromJson<ContestSaveData>(File.ReadAllText(path));
                if (d == null || d.version != 1 || d.product != "pps-contest-2026" || d.missions == null) return false;
                data = d;
                return true;
            }
            catch (Exception) { return false; }
        }

        public bool Write()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
                if (File.Exists(Path)) File.Replace(tmp, Path, Path + ".bak");
                else File.Move(tmp, Path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PPS] save failed: " + e.Message);
                return false;
            }
        }

        public MissionRecord Record(string id)
        {
            var r = Data.missions.Find(m => m.id == id);
            if (r == null) { r = new MissionRecord { id = id }; Data.missions.Add(r); }
            return r;
        }

        public bool IsCompleted(string id) => Data.missions.Exists(m => m.id == id && m.completed);

        public bool IsUnlocked(int index) => index == 0 || IsCompleted(ContestMissions.Get(index - 1).Id);

        /// <summary>납품 완료를 기록한다. 배지는 임무당 한 번만 지급(재실행·중복 호출에도 중복 지급 없음).</summary>
        public bool Commit(DeliveryReceipt receipt)
        {
            if (receipt == null || committed.Contains(receipt)) return false;
            committed.Add(receipt);
            var r = Record(receipt.MissionId);
            bool first = !r.badgeGranted;
            r.completed = true;
            r.plays++;
            if (receipt.Stars > r.bestStars) r.bestStars = receipt.Stars;
            if (r.bestMinutes == 0 || receipt.Minutes < r.bestMinutes) r.bestMinutes = receipt.Minutes;
            if (first) { r.badgeGranted = true; Data.badges++; }
            Write();
            return first;
        }
    }
}
