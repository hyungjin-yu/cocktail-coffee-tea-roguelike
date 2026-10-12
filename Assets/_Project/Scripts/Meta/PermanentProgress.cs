using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CCT
{
    /// <summary>08:142 런 종료 결과. 사망/시간 초과는 Failed, 런 포기/타이틀로 종료/강제종료 복구는 Abandoned.</summary>
    public enum RunOutcome { Cleared, Failed, Abandoned }

    /// <summary>영구 구매 트랜잭션 결과(08:177).</summary>
    public enum PermanentPurchaseResult { Purchased, Denied, Locked, SaveFailed }

    /// <summary>
    /// SaveData를 세션 동안 캐시하고 08 §6의 저장 시점·런 수명주기·실패 재시도를 관리하는 진입점.
    /// - 런: BeginRun(totalRuns+1, activeRun 기록, 즉시 저장) → SetRunProgress(메모리 누적) → 저장 때마다
    ///   "마지막 반영량과의 차이"만 전역 합계에 더하고 activeRun 반영량을 같은 스냅샷에 기록 → CommitRunEnd(결과
    ///   하나만 +1, lastCommittedRunId, activeRun=null; 같은 runId 재호출은 무시).
    /// - 즉시 저장: 런 시작/첫 발견/스페셜티/영구 구매/런 종료. Playing 중 재료 변화는 게임시간 5초마다.
    /// - 실패: 메모리 진행 유지 + dirty, 실제 시간 5초 간격(Tick) 또는 RetrySave 버튼으로 재시도.
    /// - 미래 버전 원본: 쓰기 보류, AcceptGuestSession 후 guest_session.json에만 쓴다.
    /// </summary>
    public static class PermanentProgress
    {
        public const float CheckpointIntervalSeconds = 5f;
        public const float RetryIntervalSeconds = 5f;
        /// <summary>07:150 스페셜티는 강화 보스당 3, 강화 보스는 런당 1회(06 보스 순서) — 런당 지급 상한.</summary>
        public const int MaxSpecialtyPerRun = Balance.SpecialtyDropAmount;

        public const string NoticeRunInterrupted = "이전 런은 중단되었습니다. 마지막 저장까지의 영구 진행이 유지됩니다.";
        public const string NoticeSaveFailed = "저장하지 못했습니다. 다시 시도";

        enum WriteMode { Normal, FutureBlocked, Guest }

        static SaveData cached;
        static string overridePath;
        static SaveSystem.LoadResult lastLoad;
        static WriteMode mode;
        static string recoveryNotice;
        static bool dirty;
        static bool lastSaveFailed;
        static float retryTimer;
        static int runCocktail, runTea, runCoffee;
        static float runGameSeconds, lastRunSaveGameSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            overridePath = null;
            ResetSession();
        }

        static void ResetSession()
        {
            cached = null;
            lastLoad = null;
            mode = WriteMode.Normal;
            recoveryNotice = null;
            dirty = false;
            lastSaveFailed = false;
            retryTimer = 0f;
            runCocktail = runTea = runCoffee = 0;
            runGameSeconds = lastRunSaveGameSeconds = 0f;
        }

        static string PrimaryPath => string.IsNullOrEmpty(overridePath) ? SaveSystem.DefaultPath : overridePath;

        public static SaveData Current
        {
            get
            {
                EnsureLoaded();
                return cached;
            }
        }

        static void EnsureLoaded()
        {
            if (cached != null) return;
            lastLoad = SaveSystem.LoadDetailed(PrimaryPath);
            cached = lastLoad.Data;
            mode = lastLoad.FutureProtected ? WriteMode.FutureBlocked : WriteMode.Normal;
            Debug.Log($"[Save] save.json 경로: {PrimaryPath} (source={lastLoad.Source}, v{lastLoad.SourceVersion})");
            foreach (var line in lastLoad.Diagnostics) Debug.Log($"[Save] {line}");
            LogUnknownPermanentIds();
            RecoverInterruptedRun();
        }

        /// <summary>08:169 알 수 없는 영구 노드 id는 보존하되 진단로그에 기록한다.</summary>
        static void LogUnknownPermanentIds()
        {
            if (cached.unlockedPermanentSkillIds.Count == 0) return;
            try
            {
                var known = new HashSet<string>(PermanentSkillDatabase.LoadAll().Select(n => n.id));
                foreach (var id in cached.unlockedPermanentSkillIds)
                    if (!known.Contains(id)) Debug.Log($"[Save] 알 수 없는 영구 노드 id 보존(효과/비용 제외): {id}");
            }
            catch (Exception e)
            {
                Debug.Log($"[Save] 영구 노드 id 진단 생략: {e.GetType().Name}");
            }
        }

        /// <summary>08:157 강제종료 뒤 activeRun이 남아 있으면 abandonedRuns+1, activeRun=null로 1회 저장.
        /// 체크포인트된 진행은 그대로 두고 미저장분은 복원하지 않는다. 이 처리는 메모리 캐시에서 1회만 일어나며,
        /// 저장이 실패하면 dirty로 남아 재시도된다(같은 세션에서 다시 세지 않는다).</summary>
        static void RecoverInterruptedRun()
        {
            var run = cached.activeRun;
            if (run == null) return;
            cached.activeRun = null;
            if (!run.resultCommitted && run.runId != cached.lastCommittedRunId)
            {
                cached.abandonedRuns++;
                recoveryNotice = NoticeRunInterrupted;
            }
            SaveNow();
        }

        // ── 상태 조회 ─────────────────────────────────────────────

        /// <summary>사용자에게 보여줄 로드 관련 알림(백업 복구/손상/미래 버전/음수 보정/중단된 런). 없으면 null.</summary>
        public static string LastLoadIssue
        {
            get
            {
                EnsureLoaded();
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(lastLoad.Notice)) parts.Add(lastLoad.Notice);
                if (!string.IsNullOrEmpty(recoveryNotice)) parts.Add(recoveryNotice);
                return parts.Count > 0 ? string.Join(" ", parts) : null;
            }
        }

        public static SaveSystem.LoadResult LastLoad
        {
            get
            {
                EnsureLoaded();
                return lastLoad;
            }
        }

        /// <summary>가장 최근 저장 시도가 실패했는지(08:175 HUD/메뉴 알림).</summary>
        public static bool LastSaveFailed => lastSaveFailed;

        /// <summary>메모리에 아직 디스크에 반영되지 않은 진행이 있는지.</summary>
        public static bool IsDirty => dirty || HasPendingRunDelta;

        /// <summary>미래 버전 원본을 보존 중이고 사용자가 아직 임시 플레이를 고르지 않아 쓰기를 보류 중.</summary>
        public static bool IsFutureSaveProtected
        {
            get
            {
                EnsureLoaded();
                return mode == WriteMode.FutureBlocked;
            }
        }

        public static bool IsGuestSession => mode == WriteMode.Guest;

        public static string ActiveSavePath => mode == WriteMode.Guest ? SaveSystem.GuestPathFor(PrimaryPath) : PrimaryPath;

        /// <summary>진행 중 런 id. 없으면 null.</summary>
        public static string ActiveRunId => Current.activeRun?.runId;

        // ── 저장 ─────────────────────────────────────────────

        /// <summary>즉시 저장. 진행 중 런이 있으면 미반영 재료 차이를 같은 스냅샷에 먼저 반영한다.</summary>
        public static bool SaveNow()
        {
            EnsureLoaded();
            FlushRunDeltas();
            if (cached.activeRun != null) lastRunSaveGameSeconds = runGameSeconds;
            if (mode == WriteMode.FutureBlocked)
            {
                dirty = true; // 메모리 진행은 유지, 사용자가 임시 플레이를 고를 때까지 어떤 파일도 쓰지 않는다
                return false;
            }

            bool ok = SaveSystem.Save(cached, ActiveSavePath);
            dirty = !ok;
            lastSaveFailed = !ok;
            retryTimer = 0f;
            if (!ok) SaveRetryRunner.EnsureRunning();
            return ok;
        }

        /// <summary>08:155 UI 선택/일시정지/포기/정상 종료 — 바뀐 것이 있을 때만 즉시 저장.</summary>
        public static bool SaveIfDirty() => IsDirty ? SaveNow() : true;

        /// <summary>08:174~178 "저장하지 못했습니다 — 다시 시도" 버튼.</summary>
        public static bool RetrySave() => SaveNow();

        /// <summary>08:176 실패 시 실제 시간 5초 간격 자동 재시도. SaveRetryRunner(플레이 중)나 테스트가 호출한다.</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (cached == null || !dirty || mode == WriteMode.FutureBlocked)
            {
                retryTimer = 0f;
                return;
            }
            retryTimer += Mathf.Max(0f, unscaledDeltaTime);
            if (retryTimer >= RetryIntervalSeconds) SaveNow();
        }

        /// <summary>08:172 "새 버전 저장입니다…" 확인 버튼 — 이후 쓰기는 guest_session.json에만 한다.</summary>
        public static bool AcceptGuestSession()
        {
            EnsureLoaded();
            if (mode != WriteMode.FutureBlocked) return mode == WriteMode.Guest;
            mode = WriteMode.Guest;
            return SaveNow();
        }

        // ── 런 수명주기 ─────────────────────────────────────────────

        /// <summary>런 시작: totalRuns+1, 새 activeRun, 즉시 저장. 같은 세션에 종료되지 않은 이전 런 기록이
        /// 남아 있으면(종료 경로 없이 파괴) 그 런을 포기로 1회 집계한다.</summary>
        public static string BeginRun(int seed)
        {
            EnsureLoaded();
            if (cached.activeRun != null)
            {
                FlushRunDeltas();
                if (cached.activeRun.runId != cached.lastCommittedRunId) cached.abandonedRuns++;
                cached.activeRun = null;
            }

            runCocktail = runTea = runCoffee = 0;
            runGameSeconds = lastRunSaveGameSeconds = 0f;
            cached.totalRuns++;
            cached.activeRun = new ActiveRunRecord
            {
                runId = Guid.NewGuid().ToString("N"),
                seed = seed,
                startedUtc = DateTime.UtcNow.ToString("o"),
            };
            var runId = cached.activeRun.runId;
            SaveNow();
            return runId;
        }

        /// <summary>런 메모리 누적량을 알린다(저장은 하지 않음). 줄어드는 값은 무시한다.</summary>
        public static void SetRunProgress(string runId, int cocktail, int tea, int coffee, float gameSeconds)
        {
            if (!IsActiveRun(runId)) return;
            runCocktail = Math.Max(runCocktail, cocktail);
            runTea = Math.Max(runTea, tea);
            runCoffee = Math.Max(runCoffee, coffee);
            runGameSeconds = Mathf.Max(runGameSeconds, gameSeconds);
        }

        /// <summary>마지막 저장(체크포인트) 이후 아직 전역 합계에 반영되지 않은 재료가 있는지.</summary>
        public static bool HasPendingRunDelta
        {
            get
            {
                var run = cached?.activeRun;
                return run != null && (runCocktail > run.cocktail || runTea > run.tea || runCoffee > run.coffee);
            }
        }

        /// <summary>08:155 Playing 중 변경이 있으면 게임시간 5초마다 체크포인트. 저장했으면 true.</summary>
        public static bool CheckpointIfDue(string runId)
        {
            if (!IsActiveRun(runId) || !HasPendingRunDelta) return false;
            if (runGameSeconds - lastRunSaveGameSeconds < CheckpointIntervalSeconds) return false;
            SaveNow();
            return true;
        }

        /// <summary>강화 보스 스페셜티(07:150) — 즉시 저장. 같은 런의 중복 처치 이벤트로 상한을 넘겨 지급하지 않는다.
        /// 런 종료 집계는 스페셜티를 다시 더하지 않는다.</summary>
        public static bool AwardRunSpecialty(string runId, int amount)
        {
            if (amount <= 0 || !IsActiveRun(runId)) return false;
            var run = cached.activeRun;
            if (run.specialtyAwarded + amount > MaxSpecialtyPerRun) return false;
            run.specialtyAwarded += amount;
            cached.specialtyPoints += amount;
            SaveNow();
            return true;
        }

        /// <summary>런 종료 집계(08:142~145). 결과 하나만 +1, 클리어면 최단 시간 갱신, 남은 재료 차이 반영,
        /// lastCommittedRunId 기록, activeRun=null, 즉시 저장. 같은 runId는 한 번만 — 두 번째 호출은 false.
        /// 저장이 실패해도 메모리 집계는 확정되고 dirty로 재시도된다(다시 세지 않는다).</summary>
        public static bool CommitRunEnd(string runId, RunOutcome outcome, float runSeconds)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(runId) || runId == cached.lastCommittedRunId) return false;
            if (!IsActiveRun(runId)) return false;

            runGameSeconds = Mathf.Max(runGameSeconds, runSeconds);
            FlushRunDeltas();
            switch (outcome)
            {
                case RunOutcome.Cleared:
                    cached.clearedRuns++;
                    if (runSeconds > 0f && (cached.bestClearSeconds <= 0f || runSeconds < cached.bestClearSeconds))
                        cached.bestClearSeconds = runSeconds;
                    break;
                case RunOutcome.Failed:
                    cached.failedRuns++;
                    break;
                default:
                    cached.abandonedRuns++;
                    break;
            }
            cached.lastCommittedRunId = runId;
            cached.activeRun = null;
            runCocktail = runTea = runCoffee = 0;
            SaveNow();
            return true;
        }

        static bool IsActiveRun(string runId)
        {
            var run = Current.activeRun;
            return run != null && !string.IsNullOrEmpty(runId) && run.runId == runId;
        }

        /// <summary>08:152 런 메모리 누적량과 activeRun 반영량의 차이만 전역 합계에 더하고, 반영량을 갱신한다.</summary>
        static void FlushRunDeltas()
        {
            var run = cached?.activeRun;
            if (run == null) return;
            int dc = Math.Max(0, runCocktail - run.cocktail);
            int dt = Math.Max(0, runTea - run.tea);
            int db = Math.Max(0, runCoffee - run.coffee);
            cached.cocktailIngredientsCollected += dc;
            cached.teaLeavesCollected += dt;
            cached.coffeeBeansCollected += db;
            run.cocktail += dc;
            run.tea += dt;
            run.coffee += db;
            run.checkpointGameSeconds = Mathf.Max(run.checkpointGameSeconds, runGameSeconds);
        }

        // ── 영구 구매 ─────────────────────────────────────────────

        /// <summary>08:177 트랜잭션 구매: 검증 → 메모리에 추가 → 저장 성공 시 확정. 저장 실패면 추가를 되돌린다
        /// (비용은 목록에서 계산되므로 차감도 함께 롤백). 음수 잔액이면 잠금(08:171), 런 중엔 구매 없음(07:150).</summary>
        public static PermanentPurchaseResult TryPurchasePermanentSkill(PermanentSkillData node,
            IReadOnlyList<PermanentSkillData> nodes)
        {
            EnsureLoaded();
            if (node == null || nodes == null || IsRunInProgress) return PermanentPurchaseResult.Denied;

            var tree = new PermanentSkillTree(nodes, cached.unlockedPermanentSkillIds, cached.clearedRuns);
            int balance = cached.specialtyPoints - tree.SpentPoints;
            if (balance < 0) return PermanentPurchaseResult.Locked;
            if (!tree.TryUnlock(node, balance)) return PermanentPurchaseResult.Denied;

            bool wasDirty = dirty;
            cached.unlockedPermanentSkillIds.Add(node.id);
            if (SaveNow()) return PermanentPurchaseResult.Purchased;

            cached.unlockedPermanentSkillIds.RemoveAt(cached.unlockedPermanentSkillIds.LastIndexOf(node.id));
            dirty = wasDirty; // 롤백된 메모리는 구매 전과 같다 — 구매 때문에 생긴 미저장분은 없다
            return PermanentPurchaseResult.SaveFailed;
        }

        /// <summary>살아 있는 런(결과 화면 전)이 있으면 true. 영구 구매는 런 밖에서만(07:150). activeRun 기록만으로
        /// 판단하지 않는다 — 강제종료 판정용 기록이 남아 있어도 구매 화면 자체는 런 밖이다.</summary>
        static bool IsRunInProgress => GameManager.Instance != null && GameManager.Instance.State != GameState.GameOver;

        /// <summary>비용 판단 없이 영구 노드 해금을 기록한다(테스트/도구용). 저장 실패면 되돌리고 false.</summary>
        public static bool UnlockPermanentSkill(string id)
        {
            if (string.IsNullOrEmpty(id) || Current.unlockedPermanentSkillIds.Contains(id)) return false;
            bool wasDirty = dirty;
            cached.unlockedPermanentSkillIds.Add(id);
            if (SaveNow()) return true;
            cached.unlockedPermanentSkillIds.Remove(id);
            dirty = wasDirty;
            return false;
        }

        // ── 발견/누적 ─────────────────────────────────────────────

        /// <summary>런 밖에서 직접 스페셜티를 더한다(도구/테스트). 런 중 보스 지급은 AwardRunSpecialty.</summary>
        public static void AddSpecialty(int amount)
        {
            if (amount <= 0) return;
            Current.specialtyPoints += amount;
            SaveNow();
        }

        /// <summary>카드를 처음 획득한 순간 도감에 기록하고 즉시 저장한다.</summary>
        public static void DiscoverCard(string id) => Discover(Current.discoveredCardIds, id);

        /// <summary>08:140/154 커피 노드를 처음 해금한 순간 기록하고 즉시 저장한다.</summary>
        public static void DiscoverSkill(string id) => Discover(Current.discoveredSkillIds, id);

        /// <summary>재료 품종을 처음 발견한 순간 기록하고 즉시 저장한다.</summary>
        public static void DiscoverMaterialVariety(string id) => Discover(Current.discoveredMaterialVarietyIds, id);

        static void Discover(List<string> list, string id)
        {
            if (string.IsNullOrEmpty(id) || list.Contains(id)) return;
            list.Add(id);
            SaveNow();
        }

        /// <summary>런 밖에서 재료 누적량을 직접 더한다(도구/테스트). 런 중에는 SetRunProgress + 체크포인트를 쓴다.</summary>
        public static void AddMaterialsCollected(int cocktail, int tea, int coffee)
        {
            if (cocktail <= 0 && tea <= 0 && coffee <= 0) return;
            if (cocktail > 0) Current.cocktailIngredientsCollected += cocktail;
            if (tea > 0) Current.teaLeavesCollected += tea;
            if (coffee > 0) Current.coffeeBeansCollected += coffee;
            SaveNow();
        }

        /// <summary>테스트 전용 — 실제 세이브 파일 대신 임시 경로를 쓰게 한다(세션 상태도 초기화 = 새 실행).
        /// null을 주면 원래 경로로 되돌린다.</summary>
        public static void SetPathForTests(string path)
        {
            overridePath = path;
            ResetSession();
        }
    }
}
