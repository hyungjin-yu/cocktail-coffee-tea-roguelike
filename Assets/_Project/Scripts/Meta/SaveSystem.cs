using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 영구 데이터(SaveData)를 save.json으로 읽고 쓴다 — 08 §5/§6.
    /// 쓰기: 직렬화 → .tmp 완전 쓰기/닫기 → .tmp 재읽기 검증 → 현재 원본이 "정상"일 때만 .bak으로 보존 →
    /// 원본 교체. 읽기: 정상 원본 → 정상 .bak → 새 데이터. .tmp는 자동 적용하지 않는다. 손상 파일은 시간
    /// 접미사 .corrupt로 복사 보존. 미래 schemaVersion(&gt;2)은 원본·.bak을 절대 바꾸지 않는다(guest 세션).
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>현재 세이브 스키마 버전(08:135).</summary>
        public const int CurrentVersion = 2;

        public const string NoticeBackupRestored = "저장 파일을 읽지 못해 백업(.bak)에서 복구했습니다.";
        public const string NoticeCorruptFresh = "저장 파일을 읽지 못해 새 진행으로 시작합니다. 기존 파일은 보관했습니다.";
        public const string NoticeFutureVersion = "새 버전 저장입니다. 기존 파일을 보존하고 임시 플레이합니다.";
        public const string NoticeNegativeClamped = "일부 누적치가 음수로 손상돼 0으로 되돌렸습니다.";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>미래 버전 원본을 보존하는 동안 쓰는 임시 플레이 파일(08:172). 원본과 같은 폴더.</summary>
        public static string GuestPathFor(string path)
        {
            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? "guest_session.json" : Path.Combine(dir, "guest_session.json");
        }

        /// <summary>가장 최근 Load가 사용자에게 알려야 할 문제(백업 복구/손상/미래 버전/음수 보정). 없으면 null.</summary>
        public static string LastLoadIssue { get; private set; }

        /// <summary>가장 최근 Load의 상세 결과.</summary>
        public static LoadResult LastLoad { get; private set; }

        /// <summary>가장 최근 Save 호출이 실패했는지.</summary>
        public static bool LastSaveFailed { get; private set; }

        /// <summary>테스트 전용 오류 주입 — 지정 단계에서 예외를 던지면 그 단계에서 프로세스가 죽거나 디스크가
        /// 실패한 것처럼 동작한다. 실제 게임에서는 항상 null.</summary>
        public static Action<SaveStage> FaultHookForTests;

        public enum SaveStage { WriteTmp, AfterTmpWritten, BeforeReplace }

        public enum FileState { Missing, Valid, Invalid, Future }

        public enum LoadSource { Fresh, Original, Backup, FreshAfterCorrupt, FutureProtected }

        public sealed class LoadResult
        {
            public SaveData Data;
            public LoadSource Source;
            /// <summary>원본(또는 .bak)이 schemaVersion&gt;2라 쓰기를 막아야 한다. 사용자가 임시 플레이를
            /// 고르면 GuestPath에만 쓴다.</summary>
            public bool FutureProtected;
            public string GuestPath;
            /// <summary>원본 파일의 스키마 버전(없으면 0, v1의 "version" 포함). 새 데이터면 CurrentVersion.</summary>
            public int SourceVersion;
            public readonly List<string> CorruptPreservedPaths = new List<string>();
            public readonly List<string> Diagnostics = new List<string>();
            public string Notice;
        }

        struct ReadResult
        {
            public FileState State;
            public SaveData Data;
            public int Version;
        }

        /// <summary>기존 호출부 호환용. 파일이 없거나 손상돼도 예외를 던지지 않는다.</summary>
        public static SaveData Load(string path = null) => LoadDetailed(path).Data;

        public static LoadResult LoadDetailed(string path = null)
        {
            path = string.IsNullOrEmpty(path) ? DefaultPath : path;
            var result = new LoadResult { GuestPath = GuestPathFor(path) };
            var notices = new List<string>();

            var original = Read(path);
            ReadResult chosen = default;
            int futureVersion = 0;
            switch (original.State)
            {
                case FileState.Valid:
                    chosen = original;
                    result.Source = LoadSource.Original;
                    break;
                case FileState.Future:
                    result.FutureProtected = true;
                    futureVersion = original.Version;
                    break;
                default:
                    if (original.State == FileState.Invalid) PreserveCorrupt(path, result);
                    var backup = Read(path + ".bak");
                    if (backup.State == FileState.Valid)
                    {
                        chosen = backup;
                        result.Source = LoadSource.Backup;
                        notices.Add(NoticeBackupRestored);
                    }
                    else if (backup.State == FileState.Future)
                    {
                        // 미래 버전 .bak을 다음 저장이 덮어쓰지 않게 원본과 똑같이 보호한다.
                        result.FutureProtected = true;
                        futureVersion = backup.Version;
                    }
                    else
                    {
                        if (backup.State == FileState.Invalid) PreserveCorrupt(path + ".bak", result);
                        bool anyCorrupt = original.State == FileState.Invalid || backup.State == FileState.Invalid;
                        result.Source = anyCorrupt ? LoadSource.FreshAfterCorrupt : LoadSource.Fresh;
                        if (anyCorrupt) notices.Add(NoticeCorruptFresh);
                    }
                    break;
            }

            if (result.FutureProtected)
            {
                result.Source = LoadSource.FutureProtected;
                result.Data = NewData();
                result.SourceVersion = futureVersion;
                notices.Add(NoticeFutureVersion);
                result.Diagnostics.Add($"save: schemaVersion>{CurrentVersion} 파일 보존, 쓰기 보류(guest={result.GuestPath})");
            }
            else if (chosen.Data != null)
            {
                result.Data = chosen.Data;
                result.SourceVersion = chosen.Version;
                if (Normalize(result.Data, chosen.Version, result.Diagnostics)) notices.Add(NoticeNegativeClamped);
            }
            else
            {
                result.Data = NewData();
                result.SourceVersion = CurrentVersion;
            }

            result.Notice = notices.Count > 0 ? string.Join(" ", notices) : null;
            LastLoad = result;
            LastLoadIssue = result.Notice;
            return result;
        }

        static SaveData NewData()
        {
            var data = new SaveData();
            Normalize(data, CurrentVersion, null);
            return data;
        }

        /// <summary>파일 하나를 분류한다. 텍스트가 "완전한" JSON 객체가 아니면(잘림·빈 파일·쓰레기) Invalid.</summary>
        static ReadResult Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return new ReadResult { State = FileState.Missing };
                var json = File.ReadAllText(path, Encoding.UTF8);
                return Parse(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 읽기 실패 {path}: {e.GetType().Name}");
                return new ReadResult { State = FileState.Invalid };
            }
        }

        static ReadResult Parse(string json)
        {
            if (!JsonSyntax.TryGetRootMembers(json, out var members)) return new ReadResult { State = FileState.Invalid };
            try
            {
                // 버전은 루트 단계 키에서만 읽는다(중첩 객체 안의 같은 이름 키로 오판하지 않음). 타입이 정수가 아니면 손상.
                int version;
                if (members.TryGetValue("schemaVersion", out var rawSchema))
                {
                    if (!JsonSyntax.TryParseInt(rawSchema, out version)) return new ReadResult { State = FileState.Invalid };
                }
                else if (members.TryGetValue("version", out var rawLegacy))
                {
                    if (!JsonSyntax.TryParseInt(rawLegacy, out version)) return new ReadResult { State = FileState.Invalid };
                }
                else
                {
                    version = 0;
                }
                if (version > CurrentVersion) return new ReadResult { State = FileState.Future, Version = version };
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return new ReadResult { State = FileState.Invalid };
                return new ReadResult { State = FileState.Valid, Data = data, Version = Math.Max(0, version) };
            }
            catch
            {
                return new ReadResult { State = FileState.Invalid };
            }
        }

        /// <summary>손상 파일을 "{원본}.{UTC 시각}.corrupt"로 복사해 둔다(원본은 그대로 — 다음 정상 저장이
        /// 교체하되 .bak으로는 옮기지 않는다).</summary>
        static void PreserveCorrupt(string path, LoadResult result)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string target = $"{path}.{stamp}.corrupt";
                for (int i = 1; File.Exists(target); i++) target = $"{path}.{stamp}-{i}.corrupt";
                File.Copy(path, target);
                result.CorruptPreservedPaths.Add(target);
                result.Diagnostics.Add($"save: 손상 파일 보존 {target}");
            }
            catch (Exception e)
            {
                result.Diagnostics.Add($"save: 손상 파일 보존 실패 {path}: {e.GetType().Name}");
            }
        }

        static bool HasCorruptCopy(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                var bytes = File.ReadAllBytes(path);
                foreach (var candidate in Directory.GetFiles(dir, Path.GetFileName(path) + ".*.corrupt"))
                {
                    var other = File.ReadAllBytes(candidate);
                    if (other.Length != bytes.Length) continue;
                    bool same = true;
                    for (int k = 0; k < bytes.Length && same; k++) same = bytes[k] == other[k];
                    if (same) return true;
                }
            }
            catch
            {
                // 확인 실패 시 사본을 하나 더 남기는 쪽을 택한다.
            }
            return false;
        }

        /// <summary>08:165~171 v0(버전 없음)/v1 이전 + v2 정리. 목록 null→빈 목록, 빈 문자열 제거, 중복은 한 번만
        /// (순서 유지), 알 수 없는 id는 보존. 음수 카운터는 0(보정 사실을 true로 돌려 알린다). 옛 파일에는 런 통계가
        /// 없으므로 기본값 그대로 — 플레이 기록을 추정해 만들지 않는다.</summary>
        static bool Normalize(SaveData data, int sourceVersion, List<string> diagnostics)
        {
            data.unlockedPermanentSkillIds = CleanIds(data.unlockedPermanentSkillIds, "unlockedPermanentSkillIds", diagnostics);
            data.discoveredCardIds = CleanIds(data.discoveredCardIds, "discoveredCardIds", diagnostics);
            data.discoveredSkillIds = CleanIds(data.discoveredSkillIds, "discoveredSkillIds", diagnostics);
            data.discoveredMaterialVarietyIds = CleanIds(data.discoveredMaterialVarietyIds, "discoveredMaterialVarietyIds", diagnostics);

            if (sourceVersion < CurrentVersion)
            {
                // v0/v1 파일에 이 필드들은 존재하지 않았다. 혹시 같은 이름의 쓰레기 값이 있어도 통계를 지어내지 않는다.
                data.saveRevision = 0;
                data.totalRuns = data.clearedRuns = data.failedRuns = data.abandonedRuns = 0;
                data.bestClearSeconds = 0f;
                data.activeRun = null;
                data.lastCommittedRunId = "";
                diagnostics?.Add($"save: v{sourceVersion} → v{CurrentVersion} 이전");
            }

            bool negative = false;
            data.specialtyPoints = ClampCounter(data.specialtyPoints, ref negative);
            data.cocktailIngredientsCollected = ClampCounter(data.cocktailIngredientsCollected, ref negative);
            data.teaLeavesCollected = ClampCounter(data.teaLeavesCollected, ref negative);
            data.coffeeBeansCollected = ClampCounter(data.coffeeBeansCollected, ref negative);
            data.totalRuns = ClampCounter(data.totalRuns, ref negative);
            data.clearedRuns = ClampCounter(data.clearedRuns, ref negative);
            data.failedRuns = ClampCounter(data.failedRuns, ref negative);
            data.abandonedRuns = ClampCounter(data.abandonedRuns, ref negative);
            data.saveRevision = ClampCounter(data.saveRevision, ref negative);
            if (float.IsNaN(data.bestClearSeconds) || float.IsInfinity(data.bestClearSeconds) || data.bestClearSeconds < 0f)
            {
                data.bestClearSeconds = 0f;
                negative = true;
            }
            data.lastCommittedRunId ??= "";

            // JsonUtility는 null 객체를 기본 인스턴스로 읽는다 — runId가 없으면 "activeRun 없음".
            if (data.activeRun != null && string.IsNullOrEmpty(data.activeRun.runId)) data.activeRun = null;
            if (data.activeRun != null)
            {
                bool runNegative = false;
                data.activeRun.cocktail = ClampCounter(data.activeRun.cocktail, ref runNegative);
                data.activeRun.tea = ClampCounter(data.activeRun.tea, ref runNegative);
                data.activeRun.coffee = ClampCounter(data.activeRun.coffee, ref runNegative);
                data.activeRun.specialtyAwarded = ClampCounter(data.activeRun.specialtyAwarded, ref runNegative);
                data.activeRun.startedUtc ??= "";
                float cp = data.activeRun.checkpointGameSeconds;
                if (float.IsNaN(cp) || float.IsInfinity(cp) || cp < 0f)
                {
                    data.activeRun.checkpointGameSeconds = 0f;
                    runNegative = true;
                }
                negative |= runNegative;
            }

            data.schemaVersion = CurrentVersion;
            if (negative) diagnostics?.Add("save: 음수 카운터를 0으로 보정");
            return negative;
        }

        static int ClampCounter(int value, ref bool clamped)
        {
            if (value >= 0) return value;
            clamped = true;
            return 0;
        }

        static List<string> CleanIds(List<string> ids, string field, List<string> diagnostics)
        {
            var clean = new List<string>();
            if (ids == null) return clean;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int dropped = 0;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) { dropped++; continue; }
                clean.Add(id);
            }
            if (dropped > 0) diagnostics?.Add($"save: {field} 빈 값/중복 {dropped}개 정리");
            return clean;
        }

        /// <summary>JSON 직렬화. activeRun이 없으면 08:144대로 JSON null을 쓴다(JsonUtility는 null 객체를
        /// 기본 인스턴스로 쓰므로 그 부분만 치환).</summary>
        public static string Serialize(SaveData data)
        {
            data.unlockedPermanentSkillIds ??= new List<string>();
            data.discoveredCardIds ??= new List<string>();
            data.discoveredSkillIds ??= new List<string>();
            data.discoveredMaterialVarietyIds ??= new List<string>();
            data.lastCommittedRunId ??= "";
            var json = JsonUtility.ToJson(data);
            if (data.activeRun == null)
                json = json.Replace("\"activeRun\":" + JsonUtility.ToJson(new ActiveRunRecord()), "\"activeRun\":null");
            return json;
        }

        /// <summary>원자적 저장. 데이터 변경 계약: schemaVersion이 2 이하이면 2로 맞추고, 성공 시에만 saveRevision을
        /// 1 올린 채 true. 실패하면 saveRevision을 원래 값으로 되돌리고 그 밖의 메모리 진행은 의도적으로 유지(dirty
        /// 재시도 대상), 원본/.bak은 이전 정상 상태 그대로 false. schemaVersion&gt;2 데이터는 아무것도 바꾸지 않고 거부.
        /// 원본이 손상돼 있으면 .bak으로 옮기지 않고(정상 .bak 보호) .corrupt 사본을 확보한 뒤에만 교체한다. 원본이
        /// 미래 버전이면 절대 쓰지 않고, 미래 버전 .bak도 덮어쓰지 않는다.</summary>
        public static bool Save(SaveData data, string path = null)
        {
            path = string.IsNullOrEmpty(path) ? DefaultPath : path;
            var tmp = path + ".tmp";
            if (data == null || data.schemaVersion > CurrentVersion)
            {
                // 미래 버전 데이터를 v2로 낮춰 쓰지 않는다 — 데이터도 바꾸지 않고 거부.
                LastSaveFailed = true;
                Debug.LogWarning($"[Save] 저장 거부 {path}: schemaVersion {data?.schemaVersion} > {CurrentVersion}");
                return false;
            }
            int previousRevision = data.saveRevision;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                data.schemaVersion = CurrentVersion;
                data.saveRevision = previousRevision + 1;
                var json = Serialize(data);

                FaultHookForTests?.Invoke(SaveStage.WriteTmp);
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                           FileOptions.WriteThrough))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                FaultHookForTests?.Invoke(SaveStage.AfterTmpWritten);

                var check = Read(tmp);
                if (check.State != FileState.Valid || check.Data.saveRevision != data.saveRevision)
                    throw new IOException("save.tmp 검증 실패");

                var current = Read(path);
                var backupState = Read(path + ".bak").State;
                FaultHookForTests?.Invoke(SaveStage.BeforeReplace);
                switch (current.State)
                {
                    case FileState.Missing:
                        File.Move(tmp, path); // .bak은 그대로(원본이 없을 때도 정상 .bak 유지)
                        break;
                    case FileState.Valid:
                        // 미래 버전 .bak은 정상 원본으로도 덮어쓰지 않는다 — 그 경우만 백업 없이 교체.
                        File.Replace(tmp, path, backupState == FileState.Future ? null : path + ".bak", true);
                        break;
                    case FileState.Invalid:
                        // 손상 원본으로 정상 .bak을 덮지 않는다. 로드를 거치지 않은 경로일 수도 있으니 교체
                        // 전에 같은 내용의 .corrupt 사본이 없으면 하나 남긴다.
                        if (!HasCorruptCopy(path))
                        {
                            var preserve = new LoadResult();
                            PreserveCorrupt(path, preserve);
                            if (preserve.CorruptPreservedPaths.Count == 0)
                                throw new IOException("손상 원본 보존 실패 — 원본을 교체하지 않는다");
                        }
                        File.Replace(tmp, path, null, true);
                        break;
                    default:
                        throw new IOException("미래 버전 save.json은 덮어쓰지 않는다");
                }

                LastSaveFailed = false;
                return true;
            }
            catch (Exception e)
            {
                data.saveRevision = previousRevision;
                LastSaveFailed = true;
                Debug.LogWarning($"[Save] 저장 실패 {path}: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>JSON 텍스트가 완전한 단일 객체인지 검사하는 최소 구문 검사기(값의 의미는 보지 않는다).
    /// JsonUtility가 잘린 텍스트를 부분적으로 받아들이는 문제를 막는다.</summary>
    public static class JsonSyntax
    {
        public static bool IsCompleteObject(string json) => TryGetRootMembers(json, out _);

        /// <summary>완전한 단일 객체이면 true와 함께 "루트 단계" 멤버의 키 → 원시 값 텍스트를 돌려준다(중첩 객체 안의
        /// 같은 이름 키는 포함하지 않는다). 같은 키가 루트에 두 번 나오면 모호하므로 false.</summary>
        public static bool TryGetRootMembers(string json, out Dictionary<string, string> members)
        {
            members = null;
            if (string.IsNullOrEmpty(json)) return false;
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') return false;
            i++;
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == '}')
            {
                i++;
            }
            else
            {
                while (true)
                {
                    SkipWs(json, ref i);
                    int keyStart = i;
                    if (i >= json.Length || json[i] != '"' || !Str(json, ref i)) return false;
                    string key = json.Substring(keyStart + 1, i - keyStart - 2);
                    SkipWs(json, ref i);
                    if (i >= json.Length || json[i] != ':') return false;
                    i++;
                    SkipWs(json, ref i);
                    int valueStart = i;
                    if (!Value(json, ref i, 1)) return false;
                    if (found.ContainsKey(key)) return false;
                    found[key] = json.Substring(valueStart, i - valueStart);
                    SkipWs(json, ref i);
                    if (i >= json.Length) return false;
                    if (json[i] == ',') { i++; continue; }
                    if (json[i] == '}') { i++; break; }
                    return false;
                }
            }
            SkipWs(json, ref i);
            if (i != json.Length) return false;
            members = found;
            return true;
        }

        /// <summary>원시 값 텍스트가 정수 리터럴이면 true(소수/지수/문자열/불리언은 false).</summary>
        public static bool TryParseInt(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(raw)) return false;
            for (int k = 0; k < raw.Length; k++)
                if (!(char.IsDigit(raw[k]) || (k == 0 && raw[k] == '-'))) return false;
            return int.TryParse(raw, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '﻿')) i++;
        }

        static bool Value(string s, ref int i, int depth)
        {
            if (depth > 64) return false;
            SkipWs(s, ref i);
            if (i >= s.Length) return false;
            char c = s[i];
            if (c == '{') return Obj(s, ref i, depth + 1);
            if (c == '[') return Arr(s, ref i, depth + 1);
            if (c == '"') return Str(s, ref i);
            if (c == '-' || (c >= '0' && c <= '9')) return Num(s, ref i);
            return Lit(s, ref i, "true") || Lit(s, ref i, "false") || Lit(s, ref i, "null");
        }

        static bool Obj(string s, ref int i, int depth)
        {
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return true; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"' || !Str(s, ref i)) return false;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') return false;
                i++;
                if (!Value(s, ref i, depth)) return false;
                SkipWs(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return true; }
                return false;
            }
        }

        static bool Arr(string s, ref int i, int depth)
        {
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return true; }
            while (true)
            {
                if (!Value(s, ref i, depth)) return false;
                SkipWs(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return true; }
                return false;
            }
        }

        static bool Str(string s, ref int i)
        {
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return true;
                if (c < ' ') return false;
                if (c == '\\')
                {
                    if (i >= s.Length) return false;
                    char e = s[i++];
                    if (e == 'u')
                    {
                        for (int k = 0; k < 4; k++, i++)
                            if (i >= s.Length || !Uri.IsHexDigit(s[i])) return false;
                    }
                    else if ("\"\\/bfnrt".IndexOf(e) < 0) return false;
                }
            }
            return false;
        }

        static bool Num(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            int digits = 0;
            while (i < s.Length && char.IsDigit(s[i])) { i++; digits++; }
            if (digits == 0) return false;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                int frac = 0;
                while (i < s.Length && char.IsDigit(s[i])) { i++; frac++; }
                if (frac == 0) return false;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                int exp = 0;
                while (i < s.Length && char.IsDigit(s[i])) { i++; exp++; }
                if (exp == 0) return false;
            }
            return i > start;
        }

        static bool Lit(string s, ref int i, string lit)
        {
            if (string.CompareOrdinal(s, i, lit, 0, lit.Length) != 0) return false;
            i += lit.Length;
            return true;
        }
    }
}
