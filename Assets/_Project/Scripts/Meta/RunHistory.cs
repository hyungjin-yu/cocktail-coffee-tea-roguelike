using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>67(2026-10-16) 한 판의 종료 종류. 저장은 문자열 이름으로 한다(정수 순서가 바뀌어도 옛 기록이 뒤섞이지 않게).</summary>
    public enum RunEndKind { Cleared, Died, TimeUp, Abandoned }

    /// <summary>선택 카드 하나와 런 종료 시점의 최종 중첩. 이름은 저장 당시 표시명(삭제·변경된 id의 대체 표시용).</summary>
    [Serializable]
    public class RunCardEntry
    {
        public string id;
        public string name;
        /// <summary>"Cocktail"/"Tea"/"Hybrid"(CardCategory 이름).</summary>
        public string category;
        public int stacks;
        public int maxStacks;
    }

    /// <summary>이번 런에 실제로 완성한 조합(히든·하이브리드·최종 형태) 또는 해금한 연구 노드.</summary>
    [Serializable]
    public class RunNamedEntry
    {
        /// <summary>"hidden" / "hybrid" / "finalForm" / "research".</summary>
        public string kind;
        public string id;
        public string name;
    }

    /// <summary>run-history.json의 한 판. 카드·조합은 종료 시점의 값 복사본이라 이후 런 진행·카드 변경과 무관하다.</summary>
    [Serializable]
    public class RunRecord
    {
        public string runId;
        public string endedUtc;
        /// <summary>RunEndKind 이름.</summary>
        public string endKind;
        public string job;
        public string jobName;
        public string weapon;
        public string weaponName;
        public float runSeconds;
        public int kills;
        public int cocktailLevel;
        public int teaLevel;
        public int researchUnlocked;
        public int specialtyEarned;
        public bool hybridUnlocked;
        /// <summary>사망일 때만: HP를 0으로 만든 피해의 한국어 문구(확인 불가면 "전투 피해"). 그 밖의 종료는 빈 문자열.</summary>
        public string deathCause = "";
        /// <summary>DamageKind 이름(사망일 때만).</summary>
        public string deathCauseKind = "";
        public string appVersion;
        public List<RunCardEntry> cards = new List<RunCardEntry>();
        public List<RunNamedEntry> combos = new List<RunNamedEntry>();
        public List<RunNamedEntry> research = new List<RunNamedEntry>();

        public RunEndKind EndKind => RunRecordText.TryParseName(endKind, out RunEndKind k) ? k : RunEndKind.Abandoned;
        public bool IsDeath => EndKind == RunEndKind.Died;
    }

    [Serializable]
    class RunHistoryFile
    {
        public int schemaVersion;
        public List<RunRecord> runs = new List<RunRecord>();
    }

    /// <summary>
    /// 67 run-history.json 읽기/쓰기(파일 하나 단위, 상태 없음). save.json v2·settings.json과 독립된 파일이며 SaveSystem과 같은 원칙:
    /// .tmp 완전 쓰기 → 재읽기 검증 → 정상 원본만 .bak 보존 → 교체. 손상 파일은 .corrupt 사본으로 보존하고 정상 .bak을 쓴다.
    /// schemaVersion이 현재보다 큰 파일은 절대 쓰지 않는다.
    /// </summary>
    public static class RunHistoryStore
    {
        public const int CurrentVersion = 1;
        public const int MaxRecords = 20;

        public enum FileState { Missing, Valid, Invalid, Future }
        public enum LoadSource { Fresh, Original, Backup, FreshAfterCorrupt, FutureProtected }
        public enum SaveStage { WriteTmp, AfterTmpWritten, BeforeReplace }

        /// <summary>테스트 전용 오류 주입(실제 게임에서는 항상 null).</summary>
        public static Action<SaveStage> FaultHookForTests;

        public sealed class LoadResult
        {
            public List<RunRecord> Records = new List<RunRecord>();
            public LoadSource Source;
            public int SourceVersion;
            public readonly List<string> CorruptPreservedPaths = new List<string>();
            public bool FutureProtected => Source == LoadSource.FutureProtected;
        }

        struct ReadResult
        {
            public FileState State;
            public List<RunRecord> Records;
            public int Version;
        }

        public static LoadResult Load(string path)
        {
            var result = new LoadResult();
            var original = Read(path);
            switch (original.State)
            {
                case FileState.Valid:
                    result.Records = original.Records;
                    result.Source = LoadSource.Original;
                    result.SourceVersion = original.Version;
                    // 미래 버전 .bak이 있으면 다음 저장의 백업 교체가 그것을 덮게 된다 — 두 파일 모두 보존(쓰기 보류).
                    if (Read(path + ".bak").State == FileState.Future) result.Source = LoadSource.FutureProtected;
                    return result;
                case FileState.Future:
                    result.Source = LoadSource.FutureProtected;
                    result.SourceVersion = original.Version;
                    return result;
            }
            if (original.State == FileState.Invalid) PreserveCorrupt(path, result);
            var backup = Read(path + ".bak");
            if (backup.State == FileState.Valid)
            {
                result.Records = backup.Records;
                result.Source = LoadSource.Backup;
                result.SourceVersion = backup.Version;
            }
            else if (backup.State == FileState.Future)
            {
                result.Source = LoadSource.FutureProtected; // 미래 .bak도 다음 저장이 덮지 않게 보호
                result.SourceVersion = backup.Version;
            }
            else
            {
                if (backup.State == FileState.Invalid) PreserveCorrupt(path + ".bak", result);
                bool corrupt = original.State == FileState.Invalid || backup.State == FileState.Invalid;
                result.Source = corrupt ? LoadSource.FreshAfterCorrupt : LoadSource.Fresh;
                result.SourceVersion = CurrentVersion;
            }
            return result;
        }

        static ReadResult Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return new ReadResult { State = FileState.Missing };
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunHistory] 읽기 실패 {path}: {e.GetType().Name}");
                return new ReadResult { State = FileState.Invalid };
            }
        }

        static ReadResult Parse(string json)
        {
            if (!JsonSyntax.TryGetRootMembers(json, out var members)) return new ReadResult { State = FileState.Invalid };
            if (!members.TryGetValue("schemaVersion", out var rawVersion) || !JsonSyntax.TryParseInt(rawVersion, out int version) || version < 1)
                return new ReadResult { State = FileState.Invalid };
            if (version > CurrentVersion) return new ReadResult { State = FileState.Future, Version = version };
            // 현재 버전은 runs가 반드시 배열이어야 한다(없음·null·다른 형식은 손상 — 정상 .bak보다 앞세우지 않는다).
            if (!members.TryGetValue("runs", out var rawRuns) || rawRuns.Length == 0 || rawRuns[0] != '[')
                return new ReadResult { State = FileState.Invalid };
            try
            {
                var file = JsonUtility.FromJson<RunHistoryFile>(json);
                if (file == null) return new ReadResult { State = FileState.Invalid };
                return new ReadResult { State = FileState.Valid, Records = Normalize(file.runs), Version = version };
            }
            catch
            {
                return new ReadResult { State = FileState.Invalid };
            }
        }

        /// <summary>빈 id·중복 runId 제거(앞의 = 최신 기록 유지), 목록 null → 빈 목록, 음수/비유한 수치 0, 20판 상한.</summary>
        public static List<RunRecord> Normalize(List<RunRecord> runs)
        {
            var clean = new List<RunRecord>();
            if (runs == null) return clean;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in runs)
            {
                if (r == null || string.IsNullOrEmpty(r.runId) || !seen.Add(r.runId)) continue;
                r.cards ??= new List<RunCardEntry>();
                r.combos ??= new List<RunNamedEntry>();
                r.research ??= new List<RunNamedEntry>();
                r.cards.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id));
                r.combos.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id));
                r.research.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id));
                foreach (var c in r.cards) { c.stacks = Math.Max(0, c.stacks); c.maxStacks = Math.Max(0, c.maxStacks); }
                if (float.IsNaN(r.runSeconds) || float.IsInfinity(r.runSeconds) || r.runSeconds < 0f) r.runSeconds = 0f;
                r.kills = Math.Max(0, r.kills);
                r.cocktailLevel = Math.Max(0, r.cocktailLevel);
                r.teaLevel = Math.Max(0, r.teaLevel);
                r.researchUnlocked = Math.Max(0, r.researchUnlocked);
                r.specialtyEarned = Math.Max(0, r.specialtyEarned);
                r.deathCause ??= "";
                r.deathCauseKind ??= "";
                clean.Add(r);
                if (clean.Count >= MaxRecords) break;
            }
            return clean;
        }

        public static string Serialize(List<RunRecord> records) =>
            JsonUtility.ToJson(new RunHistoryFile { schemaVersion = CurrentVersion, runs = records ?? new List<RunRecord>() });

        /// <summary>원자적 저장. 성공이면 true. 실패해도 원본/.bak은 이전 정상 상태 그대로다. 원본이나 .bak 중 하나라도 미래 버전이면
        /// 아무것도 쓰지 않고 false.</summary>
        public static bool Save(string path, List<RunRecord> records)
        {
            var tmp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = Serialize(records);
                FaultHookForTests?.Invoke(SaveStage.WriteTmp);
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                FaultHookForTests?.Invoke(SaveStage.AfterTmpWritten);
                var check = Read(tmp);
                if (check.State != FileState.Valid || check.Records.Count != Math.Min(records?.Count ?? 0, MaxRecords))
                    throw new IOException("run-history.tmp 검증 실패");

                var current = Read(path);
                var backupState = Read(path + ".bak").State;
                if (backupState == FileState.Future) throw new IOException("미래 버전 run-history.json.bak 보존 — 쓰지 않는다");
                FaultHookForTests?.Invoke(SaveStage.BeforeReplace);
                switch (current.State)
                {
                    case FileState.Missing:
                        File.Move(tmp, path);
                        break;
                    case FileState.Valid:
                        File.Replace(tmp, path, path + ".bak", true);
                        break;
                    case FileState.Invalid:
                        // 손상 원본으로 정상 .bak을 덮지 않는다 — 사본을 확보한 뒤에만 교체.
                        var preserve = new LoadResult();
                        PreserveCorrupt(path, preserve);
                        if (preserve.CorruptPreservedPaths.Count == 0) throw new IOException("손상 기록 파일 보존 실패");
                        File.Replace(tmp, path, null, true);
                        break;
                    default:
                        throw new IOException("미래 버전 run-history.json은 덮어쓰지 않는다");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunHistory] 저장 실패 {path}: {e.GetType().Name}: {e.Message}");
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 다음 저장이 덮어쓴다 */ }
                return false;
            }
        }

        static void PreserveCorrupt(string path, LoadResult result)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string target = $"{path}.{stamp}.corrupt";
                for (int i = 1; File.Exists(target); i++) target = $"{path}.{stamp}-{i}.corrupt";
                File.Copy(path, target);
                result.CorruptPreservedPaths.Add(target);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunHistory] 손상 파일 보존 실패 {path}: {e.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// 67 최근 20판 기록 서비스. 경로는 PermanentProgress.ActiveSavePath와 같은 폴더(-cct-data-dir·테스트 격리를 그대로 따른다):
    /// save.json → run-history.json, 그 밖의 파일명(테스트 임시 세이브·guest_session.json) → "{이름}.run-history.json".
    /// 그래서 미래 버전 보호 중 임시 플레이(게스트)는 원본 기록을 덮지 않는 별도 파일을 쓴다.
    /// 저장 실패/미래 버전 보호 중에도 기록은 메모리에 남아 결과·목록에서 읽을 수 있고, 실패는 실제 시간 5초마다 재시도한다.
    /// </summary>
    public static class RunHistory
    {
        public enum AddResult { Saved, SaveFailed, WriteBlocked, Duplicate, Invalid }

        public const string NoticeCorrupt = "최근 기록 파일을 읽지 못해 보관하고 새로 시작합니다.";
        public const string NoticeBackup = "최근 기록 파일을 읽지 못해 백업에서 복구했습니다.";
        public const string NoticeFuture = "새 버전 기록 파일을 보존합니다. 이번 실행의 기록은 저장되지 않습니다.";
        public const string NoticeSaveFailed = "최근 기록을 저장하지 못했습니다. 자동으로 다시 시도합니다.";

        static string loadedPath;
        static string overridePath;
        static List<RunRecord> records;
        static RunHistoryStore.LoadResult lastLoad;
        static bool dirty;
        static bool lastSaveFailed;
        static float retryTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            overridePath = null;
            ResetSession();
        }

        static void ResetSession()
        {
            loadedPath = null;
            records = null;
            lastLoad = null;
            dirty = false;
            lastSaveFailed = false;
            retryTimer = 0f;
        }

        /// <summary>세이브 경로에 대응하는 기록 파일 경로.</summary>
        public static string PathForSave(string savePath)
        {
            if (string.IsNullOrEmpty(savePath)) savePath = SaveSystem.DefaultPath;
            var dir = Path.GetDirectoryName(savePath);
            var stem = Path.GetFileNameWithoutExtension(savePath);
            var name = string.Equals(stem, "save", StringComparison.OrdinalIgnoreCase) ? "run-history.json" : stem + ".run-history.json";
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
        }

        public static string ActivePath => !string.IsNullOrEmpty(overridePath) ? overridePath : PathForSave(PermanentProgress.ActiveSavePath);

        static void EnsureLoaded()
        {
            var path = ActivePath;
            if (records != null && path == loadedPath) return;
            ResetSession();
            loadedPath = path;
            lastLoad = RunHistoryStore.Load(path);
            records = lastLoad.Records;
            Debug.Log($"[RunHistory] 경로: {path} (source={lastLoad.Source}, {records.Count}판)");
        }

        /// <summary>최신순 최근 기록(최대 20, 저장 실패·보호 중인 이번 실행 기록 포함). 읽기 전용으로 쓴다.</summary>
        public static IReadOnlyList<RunRecord> Records
        {
            get
            {
                EnsureLoaded();
                return records;
            }
        }

        public static RunRecord Find(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return null;
            foreach (var r in Records) if (r.runId == runId) return r;
            return null;
        }

        public static bool LastSaveFailed => lastSaveFailed;
        public static bool IsDirty => dirty;
        public static bool IsWriteBlocked
        {
            get
            {
                EnsureLoaded();
                return lastLoad.FutureProtected;
            }
        }

        /// <summary>로드·저장 관련 짧은 알림(없으면 null).</summary>
        public static string Notice
        {
            get
            {
                EnsureLoaded();
                if (lastLoad.FutureProtected) return NoticeFuture;
                if (lastSaveFailed) return NoticeSaveFailed;
                if (lastLoad.Source == RunHistoryStore.LoadSource.Backup) return NoticeBackup;
                if (lastLoad.Source == RunHistoryStore.LoadSource.FreshAfterCorrupt) return NoticeCorrupt;
                return null;
            }
        }

        /// <summary>종료한 한 판을 맨 앞(최신)에 넣고 즉시 저장한다. 같은 runId는 한 번만(Duplicate). 20판을 넘으면 가장 오래된 것부터 버린다.</summary>
        public static AddResult Add(RunRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.runId)) return AddResult.Invalid;
            EnsureLoaded();
            if (Find(record.runId) != null) return AddResult.Duplicate;
            records.Insert(0, record);
            if (records.Count > RunHistoryStore.MaxRecords) records.RemoveRange(RunHistoryStore.MaxRecords, records.Count - RunHistoryStore.MaxRecords);
            if (lastLoad.FutureProtected) return AddResult.WriteBlocked;
            dirty = true;
            return SaveNow() ? AddResult.Saved : AddResult.SaveFailed;
        }

        public static bool SaveNow()
        {
            EnsureLoaded();
            if (lastLoad.FutureProtected) return false;
            bool ok = RunHistoryStore.Save(loadedPath, records);
            dirty = !ok;
            lastSaveFailed = !ok;
            retryTimer = 0f;
            if (!ok) SaveRetryRunner.EnsureRunning();
            return ok;
        }

        /// <summary>실패 후 실제 시간 5초 간격 재시도(SaveRetryRunner가 부른다).</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (records == null || !dirty || lastLoad == null || lastLoad.FutureProtected)
            {
                retryTimer = 0f;
                return;
            }
            retryTimer += Mathf.Max(0f, unscaledDeltaTime);
            if (retryTimer >= PermanentProgress.RetryIntervalSeconds) SaveNow();
        }

        /// <summary>테스트 전용 — 기록 파일 경로를 직접 지정(null이면 세이브 경로 기준으로 되돌림). 세션 상태도 초기화.</summary>
        public static void SetPathForTests(string path)
        {
            overridePath = path;
            ResetSession();
        }

        /// <summary>테스트 전용 — 다음 접근에서 디스크를 다시 읽는다(새 실행 흉내).</summary>
        public static void ReloadForTests() => ResetSession();
    }

    /// <summary>67 기록의 한국어 표시. id는 보관만 하고 화면에는 현재 이름(없으면 저장 당시 이름)을 쓴다.</summary>
    public static class RunRecordText
    {
        public const string UnknownCard = "알 수 없는 카드";
        public const string UnknownCombo = "알 수 없는 조합";

        /// <summary>저장된 enum 이름만 받는다(숫자 문자열·정의되지 않은 값은 거부 — 삭제·변경된 값은 저장 당시 이름으로 표시).</summary>
        public static bool TryParseName<T>(string value, out T result) where T : struct, Enum
        {
            result = default;
            if (string.IsNullOrEmpty(value) || char.IsDigit(value[0]) || value[0] == '-') return false;
            return Enum.TryParse(value, false, out result) && Enum.IsDefined(typeof(T), result);
        }

        public static string EndTitle(RunEndKind kind) => kind switch
        {
            RunEndKind.Cleared => "클리어",
            RunEndKind.Died => "사망",
            RunEndKind.TimeUp => "시간 초과",
            _ => "포기",
        };

        public static string Time(float seconds)
        {
            int s = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        public static string TimeLabel(RunRecord r) => r.EndKind == RunEndKind.Cleared ? "클리어 시간" : "생존 시간";

        public static string JobName(RunRecord r) =>
            TryParseName(r.job, out Job job) ? JobPreset.For(job).name : string.IsNullOrEmpty(r.jobName) ? "알 수 없는 직업" : r.jobName;

        public static string WeaponName(RunRecord r) =>
            TryParseName(r.weapon, out WeaponKind w) ? WeaponPreset.For(w).name : string.IsNullOrEmpty(r.weaponName) ? "알 수 없는 무기" : r.weaponName;

        static Dictionary<string, CardData> cardsById;
        static Dictionary<string, CardData> Cards
        {
            get
            {
                if (cardsById != null) return cardsById;
                cardsById = new Dictionary<string, CardData>(StringComparer.Ordinal);
                try
                {
                    foreach (var c in CardDatabase.LoadAll())
                        if (c != null && !string.IsNullOrEmpty(c.id)) cardsById[c.id] = c;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RunHistory] 카드 이름 조회 생략: {e.GetType().Name}");
                }
                return cardsById;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cardsById = null;

        public static CardData CurrentCard(string id) => id != null && Cards.TryGetValue(id, out var c) ? c : null;

        public static string CardName(RunCardEntry e)
        {
            var card = CurrentCard(e.id);
            if (card != null && !string.IsNullOrEmpty(card.displayName)) return card.displayName;
            return string.IsNullOrEmpty(e.name) ? UnknownCard : e.name;
        }

        /// <param name="record">직업별 표시 이름(찻집 히든·연구 이름)을 고르기 위한 그 판의 기록.</param>
        public static string ComboName(RunNamedEntry e, RunRecord record = null)
        {
            string current = null;
            Job runJob = Job.Bartender;
            if (record != null && TryParseName(record.job, out Job parsed)) runJob = parsed;
            switch (e.kind)
            {
                case "hidden":
                    foreach (var def in HiddenSkillDatabase.All)
                        if (def.id == e.id) { current = JobStudyTheme.HiddenName(def, runJob); break; }
                    break;
                case "research":
                    try
                    {
                        foreach (var node in SkillDatabase.LoadAll())
                            if (node != null && node.id == e.id) { current = JobStudyTheme.Name(node, runJob); break; }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RunHistory] 연구 이름 조회 생략: {ex.GetType().Name}");
                    }
                    break;
                case "hybrid":
                    current = CurrentCard(e.id)?.displayName;
                    break;
                case "finalForm":
                    if (TryParseName(e.id, out AttackFinalForm form)) current = FinalFormName(form);
                    break;
            }
            if (!string.IsNullOrEmpty(current)) return current;
            return string.IsNullOrEmpty(e.name) ? UnknownCombo : e.name;
        }

        public static string ComboKindLabel(string kind) => kind switch
        {
            "hidden" => "히든",
            "hybrid" => "하이브리드",
            "finalForm" => "최종 형태",
            _ => "연구",
        };

        /// <summary>최종 형태 짧은 이름 — 해금 배너 설명("원형 파열: …")의 앞부분.</summary>
        public static string FinalFormName(AttackFinalForm form)
        {
            var text = AttackFinalForms.Description(form);
            int colon = text.IndexOf(':');
            return colon > 0 ? text.Substring(0, colon).Trim() : text;
        }

        public static string CategoryLabel(string category) => category switch
        {
            "Cocktail" => "칵테일",
            "Tea" => "차",
            "Hybrid" => "하이브리드",
            _ => "카드",
        };

        /// <summary>목록 한 줄: "사망 · 바텐더 · 레이피어 · 12:34".</summary>
        public static string ListLine(RunRecord r) =>
            $"{EndTitle(r.EndKind)}  ·  {JobName(r)}  ·  {WeaponName(r)}  ·  {Time(r.runSeconds)}";

        public static string EndedLocal(RunRecord r)
        {
            if (DateTime.TryParse(r.endedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
                return utc.ToLocalTime().ToString("MM/dd HH:mm");
            return "";
        }

        /// <summary>종료 사유 줄. 사망만 원인을 붙인다(그 밖의 종료에 마지막 적 이름을 붙이지 않는다).</summary>
        public static string EndDetail(RunRecord r) => r.EndKind switch
        {
            RunEndKind.Died => "사망 원인  " + (string.IsNullOrEmpty(r.deathCause) ? DamageSource.UnknownLabel : r.deathCause),
            RunEndKind.Cleared => "최종 보스 처치로 영업 완료",
            RunEndKind.TimeUp => "영업 시간(20분) 종료",
            _ => "플레이어가 판을 중단",
        };
    }
}
