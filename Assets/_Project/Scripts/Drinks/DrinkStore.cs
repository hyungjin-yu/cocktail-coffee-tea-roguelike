using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CCT
{
    [Serializable]
    public class DrinkAmount
    {
        public string id;
        public int amount;
        public DrinkAmount() { }
        public DrinkAmount(string id, int amount) { this.id = id; this.amount = amount; }
    }

    /// <summary>준비 음료(MVP 한 잔/한 슬롯). state: ""(없음) / "Ready"(제조 완료·미출발) / "InRun"(이번 런에 마심).</summary>
    [Serializable]
    public class PreparedDrink
    {
        public const string Ready = "Ready", InRun = "InRun";
        public string state = "";
        public string drinkId = "";
        public DrinkRecipe recipe = new DrinkRecipe();
        public string preparedUtc = "";
        public string runId = "";
        /// <summary>마신 런의 유효 처치 수(5 이상이면 locked).</summary>
        public int kills;
        /// <summary>5킬 도달 — 이 런에 귀속되어 종료 시 소멸.</summary>
        public bool locked;
        /// <summary>몇 번 보존 포기로 돌아왔는지(표시용).</summary>
        public int preservedCount;
        /// <summary>72 §5 제조 확정 때 활성이던 제조 전문(직원 ID, 없으면 ""). 이후 전문을 바꿔도 이 음료의 버프는 그대로.</summary>
        public string specialtyId = "";
        /// <summary>2026-11-10 출처 장부: 제조 때 소모한 재료 중 "끝없는 재료 모으기" 출처 양(재료별). 1 단위라도 있으면 판매 불가(마시는 것은 가능).
        /// 기존(출처 정보 없는) 준비 음료는 빈 목록 → 기존처럼 판매 가능.</summary>
        public List<DrinkAmount> endlessAmounts = new List<DrinkAmount>();

        public bool Exists => state == Ready || state == InRun;
        public bool HasEndlessSource
        {
            get
            {
                if (endlessAmounts == null) return false;
                foreach (var a in endlessAmounts) if (a != null && a.amount > 0) return true;
                return false;
            }
        }
        public int EndlessAmountOf(string id)
        {
            long sum = 0;
            if (endlessAmounts != null) foreach (var a in endlessAmounts) if (a != null && a.id == id) sum += Math.Max(0, a.amount);
            return (int)Math.Min(int.MaxValue, sum);
        }
    }

    /// <summary>보스 처치 시 확정된 음료 재료(런 임시 보상). 종료 정산 전까지 pending에 남는다(중간 보스 후 사망도 보존).</summary>
    [Serializable]
    public class DrinkRunAward
    {
        public string runId;
        public string awardId;
        public string label;
        public List<DrinkAmount> items = new List<DrinkAmount>();
        /// <summary>2026-11-10 끝없는 재료 모으기 런의 보상이면 true — 정산(정상·비정상 종료 복구 모두) 때 출처 장부에도 같은 양을 기록한다.</summary>
        public bool endless;
    }

    /// <summary>마지막 종료 정산 결과(결과 화면 표시용).</summary>
    [Serializable]
    public class DrinkSettlement
    {
        public string runId = "";
        /// <summary>"Settled"(창고 반영) / "PreservedAbandon"(보존 포기, 재료 미정산) / "Recovered"(비정상 종료 복구).</summary>
        public string kind = "";
        /// <summary>"None" / "Consumed" / "Preserved".</summary>
        public string drink = "None";
        public List<DrinkAmount> granted = new List<DrinkAmount>();
        public List<DrinkAmount> discarded = new List<DrinkAmount>();
        public string utc = "";
        /// <summary>72 §3 이번 정산으로 받은 연구 포인트(보존 포기·복구는 0).</summary>
        public int researchPoints;
    }

    /// <summary>drinks.json 스키마(v2 = v1 + 72 연구 상태). save.json v2와 독립(기존 빌드·안드로이드 세이브 호환을 깨지 않는다).
    /// v1 파일은 그대로 읽어 새 필드 기본값으로 채운다. v2 파일을 v1 빌드가 읽으면 미래 버전 보호(쓰기 없음).</summary>
    [Serializable]
    public class DrinkSaveData
    {
        public int schemaVersion = DrinkStore.CurrentVersion;
        public int saveRevision;
        public bool starterGranted;
        /// <summary>커피 확장 저장 마이그레이션용 1회 지급 플래그.</summary>
        public bool coffeeStarterGranted;
        public List<DrinkAmount> stock = new List<DrinkAmount>();
        /// <summary>2026-11-10 v4 출처 장부: stock 가운데 "끝없는 재료 모으기"에서 얻은 양(재료별, 항상 0 ≤ endless ≤ stock).
        /// 판매 코인이 스킨 재화와 같은 재화라서(DrinkLab.Sell → coins → BuyCosmetic) 무한 모드 재료로 만든 음료의 판매를 막는 데 쓴다.
        /// 기존 v3 이하 파일은 빈 목록 → 기존 재고는 전부 일반 출처.</summary>
        public List<DrinkAmount> endlessStock = new List<DrinkAmount>();
        /// <summary>한 번이라도 보유한 재료(도감 잠금 해제).</summary>
        public List<string> seenIds = new List<string>();
        public List<DrinkRecipe> recipes = new List<DrinkRecipe>();
        public PreparedDrink prepared = new PreparedDrink();
        public List<DrinkRunAward> pending = new List<DrinkRunAward>();
        /// <summary>정산을 끝낸 runId(최근 200개). 같은 runId 정산은 다시 하지 않는다.</summary>
        public List<string> settledRunIds = new List<string>();
        public DrinkSettlement lastSettlement = new DrinkSettlement();
        /// <summary>격리 테스트 저장에서만 쓰는 개발용 지급 횟수.</summary>
        public int devGrants;
        /// <summary>72 연구 포인트·숙련 해금/장착·직원·제조 누적(기존 24종 영구 강화·스페셜티와 별개).</summary>
        public ResearchState research = new ResearchState();
        /// <summary>72 §6 판매 코인(외형 구매 전용), 주문 3칸, 판매한 음료 ID(최근 200), 코인으로 산 외형, 장착 맵 외형("" = 직업 기본 맵).</summary>
        public int coins;
        public int orderSerial;
        public List<DrinkOrder> orders = new List<DrinkOrder>();
        public List<string> soldDrinkIds = new List<string>();
        public List<string> coinOwned = new List<string>();
        public string equippedMap = "";
        /// <summary>2026-11-06 정수 전용 마이그레이션(v2 이하 → v3)을 적용한 시각(UTC, 없으면 ""). 감사용 기록이며 재적용 판단은 schemaVersion으로 한다.</summary>
        public string integerMigrationUtc = "";
        /// <summary>정수 마이그레이션 직전 원본 파일 버전(0 = 적용 안 함/처음부터 v3).</summary>
        public int integerMigrationFromVersion;

        public int Owned(string id)
        {
            foreach (var s in stock) if (s != null && s.id == id) return Math.Max(0, s.amount);
            return 0;
        }

        /// <summary>재고 증감. long으로 더해 0~StockMax로 자른다(넘침으로 재고가 음수/소실되지 않게).
        /// 2026-11-06 정수 전용: 증감량은 1 단위(10의 배수)로 반올림한다 — 재고는 항상 정수 mL/g.</summary>
        public void Add(string id, int amount)
        {
            amount = DrinkRules.Whole(amount);
            if (amount == 0 || DrinkIngredients.Get(id) == null) return;
            foreach (var s in stock)
            {
                if (s == null || s.id != id) continue;
                s.amount = (int)Math.Max(0L, Math.Min(DrinkRules.StockMax, (long)s.amount + amount));
                if (s.amount > 0 && !seenIds.Contains(id)) seenIds.Add(id);
                return;
            }
            if (amount < 0) return;
            stock.Add(new DrinkAmount(id, Math.Min(DrinkRules.StockMax, amount)));
            if (!seenIds.Contains(id)) seenIds.Add(id);
        }

        /// <summary>재고 중 끝없는 모드 출처 양.</summary>
        public int EndlessOwned(string id)
        {
            if (endlessStock == null) return 0;
            foreach (var s in endlessStock) if (s != null && s.id == id) return Math.Max(0, Math.Min(s.amount, Owned(id)));
            return 0;
        }

        /// <summary>재고 중 일반 출처(판매 음료에 쓸 수 있는) 양.</summary>
        public int NormalOwned(string id) => Math.Max(0, Owned(id) - EndlessOwned(id));

        /// <summary>출처를 지정해 재고 추가. 끝없는 출처는 StockMax 클램프 뒤 실제로 늘어난 양만 장부에 기록한다(넘친 양은 어느 쪽에도 없음).</summary>
        public void AddFrom(string id, int amount, bool endless)
        {
            int before = Owned(id);
            Add(id, amount);
            if (!endless) return;
            int gained = Owned(id) - before;
            if (gained > 0) SetEndless(id, (long)EndlessOwned(id) + gained);
        }

        /// <summary>제조 소모: 재료별로 일반 출처를 먼저 쓰고 부족분만 끝없는 출처를 쓴다. 반환 = 이번에 쓴 끝없는 출처 양.</summary>
        public int ConsumeNormalFirst(string id, int amount)
        {
            amount = DrinkRules.Whole(amount);
            if (amount <= 0) return 0;
            int endlessBefore = EndlessOwned(id);
            int endlessUsed = Math.Max(0, Math.Min(endlessBefore, amount - NormalOwned(id)));
            Add(id, -amount);
            SetEndless(id, (long)endlessBefore - endlessUsed);
            return endlessUsed;
        }

        /// <summary>제조 전 예고용: 이 배합을 지금 만들면 끝없는 출처 재료가 섞이는지(일반 출처 우선 소모 기준).</summary>
        public bool WouldUseEndless(DrinkRecipe recipe)
        {
            if (recipe == null || recipe.steps == null) return false;
            var need = new Dictionary<string, long>();
            foreach (var st in recipe.steps)
            {
                if (st == null || st.amount <= 0) continue;
                need.TryGetValue(st.id, out long v);
                need[st.id] = v + DrinkRules.Whole(st.amount);
            }
            foreach (var kv in need) if (kv.Value > NormalOwned(kv.Key) && EndlessOwned(kv.Key) > 0) return true;
            return false;
        }

        void SetEndless(string id, long value)
        {
            endlessStock ??= new List<DrinkAmount>();
            int clamped = (int)Math.Max(0L, Math.Min(Owned(id), value));
            for (int i = 0; i < endlessStock.Count; i++)
            {
                var s = endlessStock[i];
                if (s == null || s.id != id) continue;
                if (clamped == 0) endlessStock.RemoveAt(i); else s.amount = clamped;
                return;
            }
            if (clamped > 0) endlessStock.Add(new DrinkAmount(id, clamped));
        }

        /// <summary>불변식 유지: 장부는 재고를 넘지 않는다. 재고가 장부 밖 경로로 줄면 끝없는 출처부터 줄인 것으로 본다(판매 가능량을 늘리지 않는 보수적 선택).</summary>
        public void ClampEndlessToStock()
        {
            endlessStock ??= new List<DrinkAmount>();
            var merged = new Dictionary<string, long>();
            var order = new List<string>();
            foreach (var e in endlessStock)
            {
                if (e == null || DrinkIngredients.Get(e.id) == null) continue;
                if (!merged.ContainsKey(e.id)) { merged[e.id] = 0; order.Add(e.id); }
                merged[e.id] += Math.Max(0, e.amount);
            }
            endlessStock = new List<DrinkAmount>();
            foreach (var id in order)
            {
                int v = (int)Math.Min(Owned(id), merged[id]);
                if (v > 0) endlessStock.Add(new DrinkAmount(id, v));
            }
        }

        public DrinkSaveData DeepClone() => JsonUtility.FromJson<DrinkSaveData>(JsonUtility.ToJson(this));
    }

    /// <summary>drinks.json 읽기/쓰기 — SaveSystem/RunHistoryStore와 같은 원칙: .tmp 완전 쓰기 → 재읽기 검증 → 정상 원본만 .bak → 교체.
    /// 손상 파일은 .corrupt 사본 보존 후 정상 .bak을 쓴다. 미래 버전 파일은 쓰지 않는다.
    /// v3(2026-11-06 정수 전용): v1·v2 파일의 0.1 단위 소수 양(재고·레시피·준비 음료·대기 보상·주문 최소량)을 1 단위로 반올림한다.
    /// 마이그레이션은 원본 파일 내용만으로 계산하는 순수 함수(지급이 아님)라 저장 실패 후 다시 읽어도 결과가 같고 반복 이득이 없다.
    /// 처음 적용할 때 원본 파일을 "{path}.pre-integer-v{n}.bak"으로 한 번 복사해 둔다(이미 있으면 덮어쓰지 않는다).</summary>
    public static class DrinkStore
    {
        public const int CurrentVersion = 4;
        /// <summary>2026-11-10 v4: 끝없는 모드 출처 장부(endlessStock·준비 음료 endlessAmounts·대기 보상 endless) 추가. 값 변환 없음(추가 필드만).
        /// v3 이하 빌드가 v4 파일을 다시 써서 장부를 잃지 않도록 버전을 올려 기존 미래 버전 보호(쓰기 금지)를 받게 한다.</summary>
        public const int EndlessLedgerVersion = 4;
        /// <summary>이 버전부터 모든 양이 1 단위(10의 배수) 정수다.</summary>
        public const int IntegerUnitsVersion = 3;
        public enum FileState { Missing, Valid, Invalid, Future }
        public enum LoadSource { Fresh, Original, Backup, FreshAfterCorrupt, FutureProtected }
        public enum SaveStage { WriteTmp, AfterTmpWritten, BeforeReplace }

        public static Action<SaveStage> FaultHookForTests;

        public sealed class LoadResult
        {
            public DrinkSaveData Data;
            public LoadSource Source;
            public int SourceVersion;
            public readonly List<string> CorruptPreservedPaths = new List<string>();
            public bool FutureProtected => Source == LoadSource.FutureProtected;
            /// <summary>이번 읽기에서 v3 이전 파일을 정수 단위로 바꿨다(호출자가 바로 v3로 저장해야 한다).</summary>
            public bool IntegerMigrated;
            /// <summary>정수 마이그레이션 전 원본 백업 경로(없으면 null).</summary>
            public string IntegerBackupPath;
            /// <summary>v4 출처 장부 이전 파일 원본 백업 경로(없으면 null). 값은 바뀌지 않는다(빈 장부 = 전부 일반 출처).</summary>
            public string LedgerBackupPath;
        }

        struct ReadResult { public FileState State; public DrinkSaveData Data; public int Version; }

        public static LoadResult Load(string path)
        {
            var result = new LoadResult();
            var original = Read(path);
            if (original.State == FileState.Valid)
            {
                result.Data = original.Data;
                result.Source = Read(path + ".bak").State == FileState.Future ? LoadSource.FutureProtected : LoadSource.Original;
                result.SourceVersion = original.Version;
                if (result.Source == LoadSource.Original) { NoteIntegerMigration(path, path, original.Version, result); NoteLedgerMigration(path, path, original.Version, result); }
                return result;
            }
            if (original.State == FileState.Future)
            {
                result.Data = new DrinkSaveData();
                result.Source = LoadSource.FutureProtected;
                result.SourceVersion = original.Version;
                return result;
            }
            if (original.State == FileState.Invalid) PreserveCorrupt(path, result);
            var backup = Read(path + ".bak");
            if (backup.State == FileState.Valid)
            {
                result.Data = backup.Data;
                result.Source = LoadSource.Backup;
                result.SourceVersion = backup.Version;
                NoteIntegerMigration(path, path + ".bak", backup.Version, result);
                NoteLedgerMigration(path, path + ".bak", backup.Version, result);
            }
            else if (backup.State == FileState.Future)
            {
                result.Data = new DrinkSaveData();
                result.Source = LoadSource.FutureProtected;
                result.SourceVersion = backup.Version;
            }
            else
            {
                if (backup.State == FileState.Invalid) PreserveCorrupt(path + ".bak", result);
                bool corrupt = original.State == FileState.Invalid || backup.State == FileState.Invalid;
                result.Data = new DrinkSaveData();
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
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (!JsonSyntax.TryGetRootMembers(json, out var members)) return new ReadResult { State = FileState.Invalid };
                if (!members.TryGetValue("schemaVersion", out var raw) || !JsonSyntax.TryParseInt(raw, out int version))
                    return new ReadResult { State = FileState.Invalid };
                if (version > CurrentVersion) return new ReadResult { State = FileState.Future, Version = version };
                if (version < 1) return new ReadResult { State = FileState.Invalid };
                var data = JsonUtility.FromJson<DrinkSaveData>(json);
                if (data == null) return new ReadResult { State = FileState.Invalid };
                // 정규화(준비 음료 검증 포함) 전에 정수 단위로 바꾼다 — 옛 0.5g 찻잎 같은 값이 새 최소량 검증에 걸려 사라지지 않게.
                if (version < IntegerUnitsVersion) MigrateToWholeUnits(data, version);
                Normalize(data);
                return new ReadResult { State = FileState.Valid, Data = data, Version = version };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Drinks] 읽기 실패 {path}: {e.GetType().Name}");
                return new ReadResult { State = FileState.Invalid };
            }
        }

        /// <summary>
        /// v3 정수 마이그레이션(순수 함수, 원본 내용에서만 계산). 반올림은 중간값을 0에서 먼 쪽(IntNum.Round).
        /// 재고·정산 기록은 반올림(0 가능), 레시피·준비 음료·대기 보상·주문 최소량의 양수 값은 최소 1 단위(배합이 0으로 사라져 무효가 되지 않게).
        /// 이미 정수인 값은 바뀌지 않으므로 여러 번 적용해도 결과가 같다(멱등).
        /// </summary>
        public static void MigrateToWholeUnits(DrinkSaveData d, int fromVersion)
        {
            if (d == null) return;
            int Keep(int v) => v > 0 ? Math.Max(DrinkRules.TenthsPerUnit, DrinkRules.Whole(v)) : v;
            void Amounts(List<DrinkAmount> list, bool keepPositive)
            {
                if (list == null) return;
                foreach (var a in list) if (a != null) a.amount = keepPositive ? Keep(a.amount) : Math.Max(0, DrinkRules.Whole(a.amount));
            }
            void Recipe(DrinkRecipe r)
            {
                if (r == null) return;
                r.water = Math.Max(0, DrinkRules.Whole(r.water));
                if (r.steps != null) foreach (var s in r.steps) if (s != null) s.amount = Keep(s.amount);
            }
            Amounts(d.stock, false);
            if (d.recipes != null) foreach (var r in d.recipes) Recipe(r);
            if (d.prepared != null) Recipe(d.prepared.recipe);
            if (d.pending != null) foreach (var a in d.pending) if (a != null) Amounts(a.items, true);
            if (d.lastSettlement != null) { Amounts(d.lastSettlement.granted, false); Amounts(d.lastSettlement.discarded, false); }
            if (d.orders != null)
                foreach (var o in d.orders)
                    if (o != null) { o.minA = Keep(o.minA); o.minB = Keep(o.minB); }
            if (fromVersion < IntegerUnitsVersion)
            {
                d.integerMigrationFromVersion = fromVersion;
                d.integerMigrationUtc = DateTime.UtcNow.ToString("o");
            }
        }

        /// <summary>v3 이전 파일을 읽었으면 원본을 한 번만 백업하고 결과에 표시한다. 백업 실패는 마이그레이션을 막지 않는다(원본은 다음 저장 때 .bak으로도 남는다).</summary>
        static void NoteIntegerMigration(string path, string sourcePath, int version, LoadResult result)
        {
            if (version >= IntegerUnitsVersion) return;
            result.IntegerMigrated = true;
            string target = path + ".pre-integer-v" + version + ".bak";
            try
            {
                if (!File.Exists(target)) File.Copy(sourcePath, target);
                result.IntegerBackupPath = target;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Drinks] 정수 마이그레이션 백업 실패 " + target + ": " + e.GetType().Name);
            }
        }

        /// <summary>v4 이전 파일을 처음 읽을 때 원본을 "{path}.pre-ledger-v{n}.bak"으로 한 번만 복사(이미 있으면 덮어쓰지 않음). 실패해도 진행.</summary>
        static void NoteLedgerMigration(string path, string sourcePath, int version, LoadResult result)
        {
            if (version >= EndlessLedgerVersion) return;
            string target = path + ".pre-ledger-v" + version + ".bak";
            try
            {
                if (!File.Exists(target)) File.Copy(sourcePath, target);
                result.LedgerBackupPath = target;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Drinks] 출처 장부 마이그레이션 백업 실패 " + target + ": " + e.GetType().Name);
            }
        }

        /// <summary>손상된 개별 값 보정: 음수 재고 0, 알 수 없는 재고 id 제거, null 목록 복구. 레시피는 지우지 않는다(목록에서 검증 표시).</summary>
        public static void Normalize(DrinkSaveData d)
        {
            d.stock ??= new List<DrinkAmount>();
            d.seenIds ??= new List<string>();
            d.recipes ??= new List<DrinkRecipe>();
            d.pending ??= new List<DrinkRunAward>();
            d.settledRunIds ??= new List<string>();
            d.prepared ??= new PreparedDrink();
            d.prepared.recipe ??= new DrinkRecipe();
            d.lastSettlement ??= new DrinkSettlement();
            // 재고: 알 수 없는 id 제거, 같은 id는 합쳐 한 줄로(long 합 → 0~StockMax).
            var merged = new Dictionary<string, long>();
            var order = new List<string>();
            foreach (var s in d.stock)
            {
                if (s == null || DrinkIngredients.Get(s.id) == null) continue;
                if (!merged.ContainsKey(s.id)) { merged[s.id] = 0; order.Add(s.id); }
                merged[s.id] += Math.Max(0, s.amount);
            }
            d.stock = new List<DrinkAmount>();
            foreach (var id in order) d.stock.Add(new DrinkAmount(id, (int)Math.Min(DrinkRules.StockMax, merged[id])));
            d.seenIds.RemoveAll(id => DrinkIngredients.Get(id) == null);
            d.recipes.RemoveAll(r => r == null);
            foreach (var r in d.recipes) { r.steps ??= new List<DrinkStep>(); r.steps.RemoveAll(s => s == null); r.name ??= ""; }
            if (d.recipes.Count > DrinkRules.RecipeLimit) d.recipes.RemoveRange(DrinkRules.RecipeLimit, d.recipes.Count - DrinkRules.RecipeLimit);
            d.prepared.recipe.steps ??= new List<DrinkStep>();
            d.prepared.recipe.steps.RemoveAll(s => s == null);
            d.prepared.runId ??= "";
            // 손상된 준비 음료(규칙 위반 배합)는 버프를 주지 않도록 비운다 — 재료 환불도 하지 않는다(복제 방지).
            if (!d.prepared.Exists || !DrinkValidator.Check(d.prepared.recipe).Ok) d.prepared = new PreparedDrink();
            d.pending.RemoveAll(a => a == null || string.IsNullOrEmpty(a.runId) || string.IsNullOrEmpty(a.awardId));
            foreach (var a in d.pending)
            {
                a.items ??= new List<DrinkAmount>();
                a.items.RemoveAll(i => i == null || DrinkIngredients.Get(i.id) == null || i.amount <= 0 || i.amount > DrinkRules.StepMax * 10);
            }
            d.settledRunIds.RemoveAll(string.IsNullOrEmpty);
            d.lastSettlement.granted ??= new List<DrinkAmount>();
            d.lastSettlement.discarded ??= new List<DrinkAmount>();
            d.prepared.specialtyId ??= "";
            // 2026-11-10 출처 장부 정규화: 장부 ≤ 재고, 준비 음료의 끝없는 출처 양 ≤ 그 재료의 배합 양(손상 값이 판매 가능량을 늘리지 않게).
            d.ClampEndlessToStock();
            d.prepared.endlessAmounts ??= new List<DrinkAmount>();
            d.prepared.endlessAmounts.RemoveAll(a => a == null || DrinkIngredients.Get(a.id) == null || a.amount <= 0);
            foreach (var a in d.prepared.endlessAmounts)
            {
                long used = 0;
                foreach (var st in d.prepared.recipe.steps) if (st.id == a.id) used += Math.Max(0, st.amount);
                a.amount = (int)Math.Min(a.amount, used);
            }
            d.prepared.endlessAmounts.RemoveAll(a => a.amount <= 0);
            d.research ??= new ResearchState();
            DrinkResearch.Sanitize(d.research);
            d.orders ??= new List<DrinkOrder>();
            d.orders.RemoveAll(o => o == null);
            if (d.orders.Count > DrinkSales.Slots) d.orders.RemoveRange(DrinkSales.Slots, d.orders.Count - DrinkSales.Slots);
            foreach (var o in d.orders)
            {
                // 손상·알 수 없는 재료 주문은 취소 칸(다음 정산 때 새 주문)으로 — 판매 판정에 쓰지 않는다.
                // 커피 주문은 두 번째 재료 대신 추출 도구로 판정한다(ingB "", minB 0이 정상). 예전 검사는 다시 켤 때마다 열린 커피 주문을 취소했다(2026-11-02).
                bool coffee = o.kind == (int)DrinkKind.Coffee;
                bool broken = coffee
                    ? !DrinkIngredients.IsCoffeeBean(o.ingA) || !Enum.IsDefined(typeof(CoffeeBrewMethod), o.method) || o.minA <= 0
                    : DrinkIngredients.Get(o.ingA) == null || DrinkIngredients.Get(o.ingB) == null || o.ingA == o.ingB || o.minA <= 0 || o.minB <= 0;
                if (broken) o.state = "Cancelled";
                o.colorFamily ??= "";
            }
            d.soldDrinkIds ??= new List<string>();
            d.coinOwned ??= new List<string>();
            d.equippedMap ??= "";
            if (d.coins < 0) d.coins = 0;
            if (d.equippedMap != "" && !d.coinOwned.Contains(d.equippedMap)) d.equippedMap = "";
        }

        public static string Serialize(DrinkSaveData d) => JsonUtility.ToJson(d, true);

        public static bool Save(string path, DrinkSaveData data)
        {
            var tmp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                data.schemaVersion = CurrentVersion;
                var json = Serialize(data);
                FaultHookForTests?.Invoke(SaveStage.WriteTmp);
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                FaultHookForTests?.Invoke(SaveStage.AfterTmpWritten);
                var check = Read(tmp);
                if (check.State != FileState.Valid || check.Data.saveRevision != data.saveRevision) throw new IOException("drinks.tmp 검증 실패");
                var current = Read(path);
                if (Read(path + ".bak").State == FileState.Future) throw new IOException("미래 버전 drinks.json.bak 보존 — 쓰지 않는다");
                FaultHookForTests?.Invoke(SaveStage.BeforeReplace);
                switch (current.State)
                {
                    case FileState.Missing: File.Move(tmp, path); break;
                    case FileState.Valid: File.Replace(tmp, path, path + ".bak", true); break;
                    case FileState.Invalid:
                        var preserve = new LoadResult();
                        PreserveCorrupt(path, preserve);
                        if (preserve.CorruptPreservedPaths.Count == 0) throw new IOException("손상 파일 보존 실패");
                        File.Replace(tmp, path, null, true);
                        break;
                    default: throw new IOException("미래 버전 drinks.json은 덮어쓰지 않는다");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Drinks] 저장 실패 {path}: {e.GetType().Name}: {e.Message}");
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
                Debug.LogWarning($"[Drinks] 손상 파일 보존 실패 {path}: {e.GetType().Name}");
            }
        }
    }
}
