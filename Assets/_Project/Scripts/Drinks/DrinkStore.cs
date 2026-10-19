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

        public bool Exists => state == Ready || state == InRun;
    }

    /// <summary>보스 처치 시 확정된 음료 재료(런 임시 보상). 종료 정산 전까지 pending에 남는다(중간 보스 후 사망도 보존).</summary>
    [Serializable]
    public class DrinkRunAward
    {
        public string runId;
        public string awardId;
        public string label;
        public List<DrinkAmount> items = new List<DrinkAmount>();
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
        public List<DrinkAmount> stock = new List<DrinkAmount>();
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

        public int Owned(string id)
        {
            foreach (var s in stock) if (s != null && s.id == id) return Math.Max(0, s.amount);
            return 0;
        }

        /// <summary>재고 증감. long으로 더해 0~StockMax로 자른다(넘침으로 재고가 음수/소실되지 않게).</summary>
        public void Add(string id, int amount)
        {
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

        public DrinkSaveData DeepClone() => JsonUtility.FromJson<DrinkSaveData>(JsonUtility.ToJson(this));
    }

    /// <summary>drinks.json 읽기/쓰기 — SaveSystem/RunHistoryStore와 같은 원칙: .tmp 완전 쓰기 → 재읽기 검증 → 정상 원본만 .bak → 교체.
    /// 손상 파일은 .corrupt 사본 보존 후 정상 .bak을 쓴다. 미래 버전 파일은 쓰지 않는다.</summary>
    public static class DrinkStore
    {
        public const int CurrentVersion = 2;
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
                Normalize(data);
                return new ReadResult { State = FileState.Valid, Data = data, Version = version };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Drinks] 읽기 실패 {path}: {e.GetType().Name}");
                return new ReadResult { State = FileState.Invalid };
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
            d.research ??= new ResearchState();
            DrinkResearch.Sanitize(d.research);
            d.orders ??= new List<DrinkOrder>();
            d.orders.RemoveAll(o => o == null);
            if (d.orders.Count > DrinkSales.Slots) d.orders.RemoveRange(DrinkSales.Slots, d.orders.Count - DrinkSales.Slots);
            foreach (var o in d.orders)
            {
                // 손상·알 수 없는 재료 주문은 취소 칸(다음 정산 때 새 주문)으로 — 판매 판정에 쓰지 않는다.
                if (DrinkIngredients.Get(o.ingA) == null || DrinkIngredients.Get(o.ingB) == null || o.ingA == o.ingB || o.minA <= 0 || o.minB <= 0) o.state = "Cancelled";
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
