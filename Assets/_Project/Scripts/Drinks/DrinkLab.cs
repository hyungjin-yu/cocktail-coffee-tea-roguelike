using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 69 음료 제조 서비스(상태 보유). 경로: PermanentProgress.ActiveSavePath와 같은 폴더(-cct-data-dir·테스트 격리를 그대로 따름) —
    /// save.json → drinks.json, 그 밖의 세이브 파일명 → "{이름}.drinks.json".
    ///
    /// 트랜잭션: 모든 변경은 사본에 적용 → 저장. 플레이어 행동(제조·레시피)은 저장 실패 시 사본을 버린다(재고 차감 없음).
    /// 게임 사건(보스 보상·런 시작·종료 정산)은 메모리에 확정하고 실제 시간 5초마다 재시도한다 — runId/awardId 기준이라 재시도가 중복을 만들지 않는다.
    /// </summary>
    public static class DrinkLab
    {
        public const int PreserveKillLimit = 5;
        /// <summary>별도 Windows 실행본 식별(스토어 1.0.x와 구분). Application.version/안드로이드 버전은 바꾸지 않는다.</summary>
        public const string MvpLabel = "v1.1 MVP 음료 제조 체험판";

        public const string NoticeCorrupt = "음료 창고 파일을 읽지 못해 보관하고 새로 시작합니다.";
        public const string NoticeBackup = "음료 창고 파일을 읽지 못해 백업에서 복구했습니다.";
        public const string NoticeFuture = "새 버전 음료 창고 파일을 보존합니다. 이번 실행의 제조는 저장되지 않습니다.";
        public const string NoticeSaveFailed = "음료 창고를 저장하지 못했습니다. 자동으로 다시 시도합니다.";
        public const string NoticeRecovered = "지난 판이 비정상 종료되어 준비 음료를 사용한 것으로 처리했습니다.";
        public const string NoticeStarter = "첫 제조용 재료 세트를 창고에 넣었습니다.";
        public const string NoticeFirstRp = "연구 포인트 10을 받았습니다(노트북 연구).";

        static string overridePath;
        static string loadedPath;
        static DrinkSaveData data;
        static DrinkStore.LoadResult lastLoad;
        static bool dirty;
        static bool lastSaveFailed;
        static float retryTimer;
        static string sessionNotice;
        /// <summary>현재 진행 중인(이 프로세스에서 살아 있는) 런. 부팅 복구가 살아 있는 런을 비정상 종료로 오판하지 않게 한다.</summary>
        static string liveRunId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            overridePath = null;
            DevToolsEnabled = false;
            ResetSession();
        }

        static void ResetSession()
        {
            loadedPath = null;
            data = null;
            lastLoad = null;
            dirty = false;
            lastSaveFailed = false;
            retryTimer = 0f;
            sessionNotice = null;
            liveRunId = null;
            departTicket = null;
            SkipDrinkNextRun = false;
        }

        public static string PathForSave(string savePath)
        {
            if (string.IsNullOrEmpty(savePath)) savePath = SaveSystem.DefaultPath;
            var dir = Path.GetDirectoryName(savePath);
            var stem = Path.GetFileNameWithoutExtension(savePath);
            var name = string.Equals(stem, "save", StringComparison.OrdinalIgnoreCase) ? "drinks.json" : stem + ".drinks.json";
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
        }

        public static string ActivePath => !string.IsNullOrEmpty(overridePath) ? overridePath : PathForSave(PermanentProgress.ActiveSavePath);

        /// <summary>격리 저장(-cct-data-dir 또는 테스트 경로)일 때만 true. 개발용 반복 지급은 여기서만 허용.</summary>
        public static bool IsIsolatedSave => !string.IsNullOrEmpty(overridePath) || !string.IsNullOrEmpty(LaunchOptions.DataDir);
        /// <summary>-cct-drinklab-dev(격리 저장 필수)로 켜지는 테스트 재료 지급 버튼.</summary>
        public static bool DevToolsEnabled { get; set; }

        public static DrinkSaveData Data { get { EnsureLoaded(); return data; } }
        public static bool LastSaveFailed => lastSaveFailed;
        public static bool IsDirty => dirty;
        public static bool WriteBlocked { get { EnsureLoaded(); return lastLoad.FutureProtected; } }
        public static DrinkStore.LoadSource LoadSource { get { EnsureLoaded(); return lastLoad.Source; } }

        /// <summary>사용자에게 알릴 문제/안내(복구·손상·미래 버전·저장 실패·첫 세트). 없으면 null.</summary>
        public static string Notice
        {
            get
            {
                EnsureLoaded();
                if (lastSaveFailed) return NoticeSaveFailed;
                return sessionNotice;
            }
        }

        static void EnsureLoaded()
        {
            var path = ActivePath;
            if (data != null && path == loadedPath) return;
            var keepLive = liveRunId;
            ResetSession();
            liveRunId = keepLive;
            loadedPath = path;
            lastLoad = DrinkStore.Load(path);
            data = lastLoad.Data;
            var notices = new List<string>();
            switch (lastLoad.Source)
            {
                case DrinkStore.LoadSource.Backup: notices.Add(NoticeBackup); break;
                case DrinkStore.LoadSource.FreshAfterCorrupt: notices.Add(NoticeCorrupt); break;
                case DrinkStore.LoadSource.FutureProtected: notices.Add(NoticeFuture); break;
            }
            // 기존 저장 마이그레이션: drinks.json이 없던 저장(v1.0 이하)·새 저장 모두 첫 제조용 세트를 한 번 지급한다.
            if (!lastLoad.FutureProtected && !data.starterGranted)
            {
                foreach (var item in StarterKit) data.Add(item.id, item.amount);
                data.starterGranted = true;
                data.coffeeStarterGranted = true;
                notices.Add(NoticeStarter);
                dirty = true;
                SaveNow();
            }
            // 커피 확장 마이그레이션: 기존 창고에도 원두 30g을 한 번 지급한다. 신규 저장은 첫 세트 플래그로 중복 방지.
            if (!lastLoad.FutureProtected && !data.coffeeStarterGranted)
            {
                data.Add(DrinkIngredients.CoffeeBean, 300);
                data.Add(DrinkIngredients.SteamedMilk, 150);
                data.coffeeStarterGranted = true;
                notices.Add("커피 제조용 원두를 창고에 넣었습니다.");
                dirty = true;
                SaveNow();
            }
            // 72 §3 연구 포인트 첫 지급(새 저장·기존 저장 모두 1회, 플래그로 반복·초기화 우회 차단).
            if (!lastLoad.FutureProtected && DrinkResearch.GrantFirst(data.research))
            {
                notices.Add(NoticeFirstRp);
                dirty = true;
                SaveNow();
            }
            if (!lastLoad.FutureProtected && EnsureOrders(data)) { dirty = true; SaveNow(); }
            sessionNotice = notices.Count > 0 ? string.Join(" ", notices) : null;
        }

        /// <summary>첫 제조용 세트(새/이전 저장에 1회). 칵테일 2잔·차 2잔 정도.</summary>
        public static readonly DrinkAmount[] StarterKit =
        {
            new DrinkAmount(DrinkIngredients.Gin, 600), new DrinkAmount(DrinkIngredients.Rum, 450),
            new DrinkAmount(DrinkIngredients.BlueCuracao, 150), new DrinkAmount(DrinkIngredients.Grenadine, 150),
            new DrinkAmount(DrinkIngredients.LemonJuice, 300), new DrinkAmount(DrinkIngredients.Honey, 200),
            new DrinkAmount(DrinkIngredients.BlackTea, 60), new DrinkAmount(DrinkIngredients.GreenTea, 60),
            new DrinkAmount(DrinkIngredients.SenchaTea, 20), new DrinkAmount(DrinkIngredients.HojichaTea, 20),
            new DrinkAmount(DrinkIngredients.CoffeeBean, 300), new DrinkAmount(DrinkIngredients.SteamedMilk, 150),
        };

        public static int Owned(string id) => Data.Owned(id);
        public static bool Seen(string id) => Data.seenIds.Contains(id) || Data.Owned(id) > 0;
        public static PreparedDrink Prepared => Data.prepared.Exists ? Data.prepared : null;
        public static IReadOnlyList<DrinkRecipe> Recipes => Data.recipes;

        // ───── 저장 ─────

        public static bool SaveNow()
        {
            EnsureLoaded();
            if (lastLoad.FutureProtected) return false;
            data.saveRevision++;
            bool ok = DrinkStore.Save(loadedPath, data);
            if (!ok) data.saveRevision--;
            dirty = !ok;
            lastSaveFailed = !ok;
            retryTimer = 0f;
            if (!ok) SaveRetryRunner.EnsureRunning();
            return ok;
        }

        public static void Tick(float unscaledDeltaTime)
        {
            if (data == null || !dirty || lastLoad == null || lastLoad.FutureProtected) { retryTimer = 0f; return; }
            retryTimer += Mathf.Max(0f, unscaledDeltaTime);
            if (retryTimer >= PermanentProgress.RetryIntervalSeconds) SaveNow();
        }

        public static bool RetrySave() => !dirty || SaveNow();

        /// <summary>플레이어 행동 트랜잭션: 사본에 적용 → 저장 성공 시에만 반영. 실패하면 원래 상태 그대로(재고 미차감).</summary>
        static bool Transact(Func<DrinkSaveData, bool> mutate, out string error)
        {
            EnsureLoaded();
            error = null;
            if (lastLoad.FutureProtected) { error = "새 버전 음료 창고를 보호 중이라 저장할 수 없습니다."; return false; }
            var draft = data.DeepClone();
            if (!mutate(draft)) return false;
            draft.saveRevision = data.saveRevision + 1;
            if (!DrinkStore.Save(loadedPath, draft))
            {
                lastSaveFailed = true;
                error = "저장에 실패해 적용하지 않았습니다. 재료는 그대로입니다.";
                return false;
            }
            data = draft;
            dirty = false;
            lastSaveFailed = false;
            return true;
        }

        /// <summary>게임 사건: 메모리에 확정하고 저장(실패하면 재시도 예약). 중복 방지는 호출부의 runId/awardId 검사.</summary>
        static bool CommitEvent(Action<DrinkSaveData> mutate)
        {
            EnsureLoaded();
            mutate(data);
            if (lastLoad.FutureProtected) return false;
            dirty = true;
            return SaveNow();
        }

        // ───── 제조 ─────

        public enum CraftStatus { Crafted, Invalid, Shortage, AlreadyPrepared, SaveFailed }

        public sealed class CraftResult
        {
            public CraftStatus Status;
            public string Message;
            public DrinkBuff Buff;
            public Dictionary<string, int> Shortages;
        }

        /// <summary>제조 확정 — 여기서만 재료를 차감하고, 준비 음료와 같은 저장 트랜잭션으로 기록한다.</summary>
        public static CraftResult Craft(DrinkRecipe recipe)
        {
            EnsureLoaded();
            var check = DrinkValidator.Check(recipe);
            if (!check.Ok) return new CraftResult { Status = CraftStatus.Invalid, Message = check.First };
            if (data.prepared.Exists)
                return new CraftResult { Status = CraftStatus.AlreadyPrepared, Message = "이미 준비된 음료가 있습니다. 출발에 쓰거나 주문에 팔거나 버린 뒤 만드세요." };
            var shortages = DrinkValidator.Shortages(recipe, data.Owned);
            if (shortages.Count > 0)
                return new CraftResult { Status = CraftStatus.Shortage, Message = "재료가 부족합니다.", Shortages = shortages };
            var copy = recipe.Clone();
            copy.name = DrinkValidator.SanitizeName(copy.name, DefaultName(copy));
            string specialty = ActiveSpecialtyFor(copy);
            bool ok = Transact(d =>
            {
                foreach (var s in copy.steps) d.Add(s.id, -s.amount);
                d.prepared = new PreparedDrink
                {
                    state = PreparedDrink.Ready, drinkId = Guid.NewGuid().ToString("N"), recipe = copy,
                    preparedUtc = DateTime.UtcNow.ToString("o"), specialtyId = specialty,
                };
                // 72 §5 직원 채용 조건(제조 누적)은 같은 거래로 센다.
                if (copy.Kind == DrinkKind.Tea) d.research.teaCrafts++; else if (copy.Kind == DrinkKind.Coffee) d.research.coffeeCrafts++; else d.research.cocktailCrafts++;
                return true;
            }, out var error);
            if (!ok) return new CraftResult { Status = CraftStatus.SaveFailed, Message = error };
            return new CraftResult { Status = CraftStatus.Crafted, Message = "음료를 준비했습니다.", Buff = DrinkCalculator.Buff(copy, specialty) };
        }

        public static string DefaultName(DrinkRecipe r)
        {
            if (r == null) return "이름 없는 음료";
            var first = r.steps.Count > 0 ? DrinkIngredients.Get(r.steps[0].id) : null;
            string tech = DrinkRules.TechniqueName(r.Technique);
            return first != null ? $"{first.name} {tech}" : r.Kind == DrinkKind.Tea ? "우린 차" : "칵테일";
        }

        // ───── 기본 예시 배합(저장하지 않는 내장 초안, 선택만으로 제조·소모 없음) ─────
        public static IReadOnlyList<DrinkRecipe> ExamplePresets => examples ??= BuildExamples();
        static List<DrinkRecipe> examples;

        static List<DrinkRecipe> BuildExamples()
        {
            var list = new List<DrinkRecipe>();
            // Actual named recipes and preparation ratios; source URLs and game scaling are documented in Docs/DrinkLab-v1.1.
            // Game simplifications: hojicha 2g is a light game dose (Ippodo standard is 10g/240mL), and the coffee water slot holds
            // the reference volume (espresso 36 = beverage out, moka 130mL = Bialetti 3-cup figure), not measured input water.
            var negroni = DrinkRecipe.NewDraft(DrinkKind.Cocktail); negroni.name = "IBA 네그로니"; negroni.technique = (int)DrinkTechnique.Build; negroni.ice = 2;
            negroni.steps.Add(new DrinkStep(DrinkIngredients.Gin, 300)); negroni.steps.Add(new DrinkStep(DrinkIngredients.Campari, 300)); negroni.steps.Add(new DrinkStep(DrinkIngredients.SweetVermouth, 300)); list.Add(negroni);
            var sencha = DrinkRecipe.NewDraft(DrinkKind.Tea); sencha.name = "센차 1인용 티백";
            sencha.steps.Add(new DrinkStep(DrinkIngredients.SenchaTea, 20)); list.Add(sencha);
            var martini = DrinkRecipe.NewDraft(DrinkKind.Cocktail); martini.name = "IBA 드라이 마티니"; martini.technique = (int)DrinkTechnique.Stir; martini.ice = 2;
            martini.steps.Add(new DrinkStep(DrinkIngredients.Gin, 600)); martini.steps.Add(new DrinkStep(DrinkIngredients.DryVermouth, 100)); list.Add(martini);
            var hojicha = DrinkRecipe.NewDraft(DrinkKind.Tea); hojicha.name = "호지차 가벼운 우림";
            hojicha.steps.Add(new DrinkStep(DrinkIngredients.HojichaTea, 20)); list.Add(hojicha);
            var pourOver = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.PourOver); pourOver.name = "SCA 비율 핸드 드립"; pourOver.water = 3000;
            pourOver.SetAmount(DrinkIngredients.CoffeeBean, 165); list.Add(pourOver);
            var espresso = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.EspressoMachine); espresso.name = "더블 에스프레소 1:2";
            espresso.SetAmount(DrinkIngredients.CoffeeBean, 180); list.Add(espresso);
            var moka = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.MokaPot); moka.name = "모카포트 3컵"; moka.water = 1300;
            moka.SetAmount(DrinkIngredients.CoffeeBean, 160); list.Add(moka);
            return list;
        }

        // ───── 레시피 ─────

        /// <summary>레시피 저장(무료). index&lt;0이면 새로 추가(한도 30), 아니면 그 자리 수정.</summary>
        public static bool SaveRecipe(DrinkRecipe recipe, int index, out string error)
        {
            EnsureLoaded();
            var check = DrinkValidator.Check(recipe);
            if (!check.Ok) { error = check.First; return false; }
            var copy = recipe.Clone();
            copy.schemaVersion = DrinkRecipe.CurrentSchema;
            copy.name = DrinkValidator.SanitizeName(copy.name, DefaultName(copy));
            if (index < 0 && data.recipes.Count >= DrinkRules.RecipeLimit) { error = $"레시피는 최대 {DrinkRules.RecipeLimit}개까지 저장할 수 있습니다."; return false; }
            if (index >= data.recipes.Count) { error = "수정할 레시피가 없습니다."; return false; }
            return Transact(d =>
            {
                if (index < 0) d.recipes.Add(copy); else d.recipes[index] = copy;
                return true;
            }, out error);
        }

        public static bool DeleteRecipe(int index, out string error)
        {
            EnsureLoaded();
            if (index < 0 || index >= data.recipes.Count) { error = "삭제할 레시피가 없습니다."; return false; }
            return Transact(d => { d.recipes.RemoveAt(index); return true; }, out error);
        }

        /// <summary>복제해 수정용 사본 만들기(목록에 추가).</summary>
        public static bool DuplicateRecipe(int index, out string error)
        {
            EnsureLoaded();
            if (index < 0 || index >= data.recipes.Count) { error = "복제할 레시피가 없습니다."; return false; }
            var copy = data.recipes[index].Clone();
            copy.name = DrinkValidator.SanitizeName(copy.name + " 사본", copy.name);
            return SaveRecipe(copy, -1, out error);
        }

        // ───── 런 수명주기 ─────

        /// <summary>마지막 BeginRun에서 준비 음료를 마시지 못한 이유(저장 실패·보호). 마셨거나 음료가 없으면 null.</summary>
        public static string LastBeginError { get; private set; }

        /// <summary>런 시작 직후(GameManager.Initialize). 비정상 종료로 남은 이전 런 음료를 먼저 정리하고, 준비 음료가 있으면
        /// "이번 런에 마심(InRun)"을 디스크에 먼저 확정한 뒤에만 버프를 돌려준다(입장 1회). 저장에 실패하면 음료는 Ready 그대로
        /// 남고 버프 없이 시작한다 — 강제 종료·재시작으로 무료 버프/복제가 생기지 않는다. 음료가 없으면 null.</summary>
        public static DrinkBuff BeginRun(string runId)
        {
            EnsureLoaded();
            LastBeginError = null;
            bool skip = SkipDrinkNextRun;
            SkipDrinkNextRun = false;
            string ticket = departTicket;
            departTicket = null;
            liveRunId = null; // 새 런이 시작되면 이전 런은 더 이상 살아 있지 않다(정산 없이 사라졌다면 아래에서 소비 처리)
            RecoverAbandoned(ticket ?? runId);
            liveRunId = runId;
            var p = data.prepared;
            if (p.state == PreparedDrink.InRun && p.runId == runId) return DrinkCalculator.Buff(p.recipe, p.specialtyId);
            if (ticket != null && p.state == PreparedDrink.InRun && p.runId == ticket)
            {
                // 출발 전에 디스크에 확정한 음용(출발 표) → 실제 runId로 바꾼다. 실패해도 디스크는 InRun(표)이라 강제 종료 시 소비로 복구된다.
                CommitEvent(d => d.prepared.runId = runId);
                return DrinkCalculator.Buff(data.prepared.recipe, data.prepared.specialtyId);
            }
            if (skip) return null; // 사용자가 "음료 없이 출발"을 고름 — Ready 그대로 보존
            if (p.state != PreparedDrink.Ready) return null;
            bool ok = Transact(d =>
            {
                d.prepared.state = PreparedDrink.InRun;
                d.prepared.runId = runId;
                d.prepared.kills = 0;
                d.prepared.locked = false;
                return true;
            }, out var error);
            if (!ok)
            {
                LastBeginError = "음료 창고 저장에 실패해 준비 음료를 마시지 않고 출발했습니다. 음료는 그대로 남아 있습니다.";
                Debug.LogWarning($"[Drinks] 입장 음용 기록 실패 — 버프 없이 시작: {error}");
                return null;
            }
            return DrinkCalculator.Buff(data.prepared.recipe, data.prepared.specialtyId);
        }

        // ───── 출발(선택 화면 → 런) ─────
        static string departTicket;
        /// <summary>사용자가 저장 실패 안내에서 "음료 없이 출발"을 골랐다(다음 BeginRun 한 번만 Ready 음료를 쓰지 않는다).</summary>
        public static bool SkipDrinkNextRun { get; private set; }

        public enum DepartStatus { NoDrink, Reserved, SaveFailed }

        /// <summary>
        /// 출발 버튼: 준비 음료(Ready)가 있으면 런을 만들기 전에 "이번 출발에 마심(InRun, 출발 표)"을 디스크에 먼저 확정한다.
        /// 저장에 실패하면 SaveFailed — 호출부는 런을 시작하지 않고 다시 시도 / 음료 없이 출발 / 취소를 묻는다(자동으로 버프 없이 시작하지 않음).
        /// </summary>
        public static DepartStatus ReserveForDeparture(out string error)
        {
            EnsureLoaded();
            error = null;
            SkipDrinkNextRun = false;
            if (data.prepared.state != PreparedDrink.Ready) return DepartStatus.NoDrink;
            string ticket = "depart-" + Guid.NewGuid().ToString("N");
            bool ok = Transact(d =>
            {
                d.prepared.state = PreparedDrink.InRun;
                d.prepared.runId = ticket;
                d.prepared.kills = 0;
                d.prepared.locked = false;
                return true;
            }, out error);
            if (!ok) return DepartStatus.SaveFailed;
            departTicket = ticket;
            liveRunId = ticket;
            return DepartStatus.Reserved;
        }

        /// <summary>저장 실패 안내에서 사용자가 명시적으로 고른 "음료 없이 출발". 준비 음료는 Ready로 남는다.</summary>
        public static void DepartWithoutDrink() => SkipDrinkNextRun = true;

        /// <summary>이번 런에 마신 음료(없으면 null).</summary>
        public static PreparedDrink DrinkInRun(string runId)
        {
            var p = Data.prepared;
            return p.state == PreparedDrink.InRun && p.runId == runId ? p : null;
        }

        /// <summary>유효 처치마다. 0~4킬 동안 처치 수를 기록하고 5킬째에 이 런에 귀속(locked)시킨다.</summary>
        public static void OnKill(string runId, int kills)
        {
            var p = DrinkInRun(runId);
            if (p == null || p.locked) return;
            int k = Math.Min(kills, PreserveKillLimit);
            if (k <= p.kills) return;
            CommitEvent(d =>
            {
                d.prepared.kills = k;
                if (k >= PreserveKillLimit) d.prepared.locked = true;
            });
        }

        public static bool IsSettled(string runId) => !string.IsNullOrEmpty(runId) && Data.settledRunIds.Contains(runId);

        /// <summary>보스 처치 시 확정 지급(런 임시 보상). 같은 runId+awardId는 한 번만. 즉시 저장(실패 시 재시도).</summary>
        public static bool RecordBossAward(string runId, string awardId, string label, IList<DrinkAmount> items)
        {
            if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(awardId) || items == null) return false;
            EnsureLoaded();
            if (IsSettled(runId)) return false;
            foreach (var a in data.pending) if (a.runId == runId && a.awardId == awardId) return false;
            var award = new DrinkRunAward { runId = runId, awardId = awardId, label = label };
            foreach (var i in items) if (i != null && i.amount > 0 && DrinkIngredients.Get(i.id) != null) award.items.Add(new DrinkAmount(i.id, i.amount));
            CommitEvent(d => d.pending.Add(award));
            return true;
        }

        public static List<DrinkRunAward> PendingFor(string runId)
        {
            var list = new List<DrinkRunAward>();
            foreach (var a in Data.pending) if (a.runId == runId) list.Add(a);
            return list;
        }

        /// <summary>
        /// 런 종료 정산(클리어·사망·시간 종료·포기·타이틀로 종료·정상 앱 종료). 같은 runId는 한 번만 — 두 번째 호출은 첫 결과를 돌려준다.
        /// 명시적 포기(explicitAbandon) + 이번 런에 마신 음료 + 처치 0~4(미귀속)면 음료를 Ready로 되돌리고 이 런의 제조 재료 보상은 정산하지 않는다.
        /// 그 밖에는 마신 음료를 소모하고 pending 보상을 창고에 넣는다. 저장 실패해도 메모리 확정 + 재시도(중복 없음).
        /// </summary>
        public static DrinkSettlement EndRun(string runId, RunEndKind kind, int kills, bool explicitAbandon) =>
            EndRun(runId, kind, kills, explicitAbandon, 0, 0, 0);

        /// <summary>72 §3: 보존 포기가 아니면 같은 저장 사건으로 연구 포인트도 정산(runId 1회). rpKills = 유효 일반+정예 처치(보스 제외).</summary>
        public static DrinkSettlement EndRun(string runId, RunEndKind kind, int kills, bool explicitAbandon, int rpKills, int midBossKills, int finalBossKills)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(runId)) return null;
            if (IsSettled(runId)) return data.lastSettlement != null && data.lastSettlement.runId == runId ? data.lastSettlement : null;
            var drink = DrinkInRun(runId);
            bool preserve = explicitAbandon && kind == RunEndKind.Abandoned && drink != null && !drink.locked && kills < PreserveKillLimit;
            var settlement = new DrinkSettlement { runId = runId, utc = DateTime.UtcNow.ToString("o") };
            CommitEvent(d =>
            {
                if (preserve)
                {
                    d.prepared.state = PreparedDrink.Ready;
                    d.prepared.runId = "";
                    d.prepared.kills = 0;
                    d.prepared.preservedCount++;
                    settlement.kind = "PreservedAbandon";
                    settlement.drink = "Preserved";
                }
                else
                {
                    if (drink != null) { d.prepared = new PreparedDrink(); settlement.drink = "Consumed"; }
                    settlement.kind = "Settled";
                }
                for (int i = d.pending.Count - 1; i >= 0; i--)
                {
                    var a = d.pending[i];
                    if (a.runId != runId) continue;
                    foreach (var item in a.items)
                    {
                        if (preserve) settlement.discarded.Add(new DrinkAmount(item.id, item.amount));
                        else { d.Add(item.id, item.amount); settlement.granted.Add(new DrinkAmount(item.id, item.amount)); }
                    }
                    d.pending.RemoveAt(i);
                }
                if (!preserve) settlement.researchPoints = DrinkResearch.SettleRun(d.research, runId, rpKills, midBossKills, finalBossKills);
                if (!preserve) RefillCancelledOrders(d);
                MarkSettled(d, runId);
                d.lastSettlement = settlement;
            });
            if (liveRunId == runId) liveRunId = null;
            return settlement;
        }

        static void MarkSettled(DrinkSaveData d, string runId)
        {
            if (!d.settledRunIds.Contains(runId)) d.settledRunIds.Add(runId);
            if (d.settledRunIds.Count > 200) d.settledRunIds.RemoveRange(0, d.settledRunIds.Count - 200);
        }

        /// <summary>
        /// 부팅/새 런 시작 시: 살아 있지 않은 런에 묶인 음료(InRun)는 강제 종료로 보고 소모(포기 보존 아님), 그 런의 pending 보상은
        /// 사망과 같이 창고에 정산한다. 기존 게임에는 런 이어하기가 없으므로 복구 대신 소비 처리한다.
        /// </summary>
        public static bool RecoverAbandoned(string exceptRunId = null)
        {
            EnsureLoaded();
            var stale = new HashSet<string>();
            if (data.prepared.state == PreparedDrink.InRun && data.prepared.runId != exceptRunId && data.prepared.runId != liveRunId)
                stale.Add(data.prepared.runId ?? "");
            foreach (var a in data.pending)
                if (a.runId != exceptRunId && a.runId != liveRunId) stale.Add(a.runId ?? "");
            if (stale.Count == 0) return false;
            bool drinkRecovered = data.prepared.state == PreparedDrink.InRun && stale.Contains(data.prepared.runId ?? "");
            CommitEvent(d =>
            {
                foreach (var runId in stale)
                {
                    var settlement = new DrinkSettlement { runId = runId, kind = "Recovered", utc = DateTime.UtcNow.ToString("o") };
                    if (d.prepared.state == PreparedDrink.InRun && (d.prepared.runId ?? "") == runId)
                    {
                        d.prepared = new PreparedDrink();
                        settlement.drink = "Consumed";
                    }
                    for (int i = d.pending.Count - 1; i >= 0; i--)
                    {
                        var a = d.pending[i];
                        if ((a.runId ?? "") != runId) continue;
                        if (!d.settledRunIds.Contains(runId))
                            foreach (var item in a.items) { d.Add(item.id, item.amount); settlement.granted.Add(new DrinkAmount(item.id, item.amount)); }
                        d.pending.RemoveAt(i);
                    }
                    MarkSettled(d, runId);
                    d.lastSettlement = settlement;
                }
            });
            if (drinkRecovered) sessionNotice = string.IsNullOrEmpty(sessionNotice) ? NoticeRecovered : sessionNotice + " " + NoticeRecovered;
            return true;
        }

        /// <summary>격리 테스트 저장 전용 반복 지급. 실제 사용자 저장에서는 항상 거부.</summary>
        public static bool DevGrant()
        {
            if (!IsIsolatedSave) return false;
            CommitEvent(d =>
            {
                foreach (var item in StarterKit) d.Add(item.id, item.amount);
                d.devGrants++;
            });
            return true;
        }

        // ───── 72 §2 재료 수급(고정 묶음 + 보유량 보정) ─────

        /// <summary>추첨 보유량 = 창고 + 이번 런 미정산 획득(배합에 올려만 둔 재료는 창고에 그대로 있으므로 포함, 이미 만든 음료는 제외).</summary>
        public static int HeldForSupply(string runId, string id)
        {
            long held = Owned(id);
            foreach (var a in Data.pending)
                if (a.runId == runId) foreach (var i in a.items) if (i.id == id) held += i.amount;
            return (int)Math.Min(int.MaxValue, held);
        }

        /// <summary>한 보상의 묶음들(결정적: runId·awardId). 정산이 끝난 런이면 빈 목록.</summary>
        public static List<DrinkAmount> RollAward(DrinkSupply.Source source, string runId, string awardId)
        {
            var list = new List<DrinkAmount>();
            if (string.IsNullOrEmpty(runId) || IsSettled(runId)) return list;
            foreach (var (id, amount) in DrinkSupply.Roll(source, runId, awardId, x => HeldForSupply(runId, x)))
            {
                var same = list.Find(a => a.id == id);
                if (same != null) same.amount += amount; else list.Add(new DrinkAmount(id, amount));
            }
            return list;
        }

        public static DrinkSupply.Source SourceFor(BossKind boss) =>
            boss == BossKind.Final ? DrinkSupply.Source.FinalBoss : DrinkSupply.Source.MidBoss;

        /// <summary>획득처 설명(도감): 72/78 표의 가중치가 있는 곳. 일반은 판당 상한(78)까지 함께 적는다.</summary>
        public static string SourceText(string id)
        {
            var r = DrinkSupply.Get(id);
            if (r == null) return "";
            var at = new List<string>();
            if (r.normal > 0) at.Add($"일반 {DrinkSupply.NormalKillsPerBundle}킬마다(판당 {DrinkSupply.NormalBundleCap}묶음까지)");
            if (r.elite > 0) at.Add("정예");
            if (r.midBoss > 0) at.Add("중간 보스");
            if (r.finalBoss > 0) at.Add("최종 보스");
            string unit = DrinkIngredients.Get(id)?.form == IngredientForm.Leaf ? "g" : "mL";
            return $"{string.Join("·", at)} (1묶음 {DrinkRules.Format(r.bundle)}{unit}{(r.rare ? ", 보스 희귀 보상 후보(두 종 중 추첨)·일반/정예는 가끔" : "")}) · 적게 가진 재료일수록 잘 나옴";
        }

        /// <summary>78 공급 규칙 한 줄 도움말(도감 하단 기존 안내 칸).</summary>
        public static string SupplyRuleText() =>
            $"재료 보상: 일반 처치 {DrinkSupply.NormalKillsPerBundle}회마다 1묶음, 판당 최대 {DrinkSupply.NormalBundleCap}묶음({DrinkSupply.NormalKillsPerBundle * DrinkSupply.NormalBundleCap}킬). " +
            $"정예는 처치마다 1묶음, 중간 보스 {DrinkSupply.MidBossDraws}묶음·최종 보스 {DrinkSupply.FinalBossDraws}묶음(상한과 별도). 중간 보스 3번째·최종 보스 5·6번째 묶음은 희귀 두 종 중 하나입니다. " +
            "희귀(블루 큐라소·그레나딘)는 일반·정예에서도 가끔 나오고, 적게 가진 재료일수록 잘 나옵니다.";

        public static string Describe(IEnumerable<DrinkAmount> items)
        {
            var parts = new List<string>();
            if (items != null)
                foreach (var i in items)
                {
                    var def = DrinkIngredients.Get(i.id);
                    if (def != null) parts.Add($"{def.name} {DrinkRules.Format(i.amount, def.form)}");
                }
            if (parts.Count == 0) return "없음";
            string text = string.Join(", ", parts);
            // 늘어난 재료 보상도 결과 요약 패널 밖으로 밀어내지 않도록 표시 문자열만 짧게 제한한다.
            return text.Length > 380 ? text.Substring(0, 377) + "…" : text;
        }

        /// <summary>표시 전용 재료별 합계(0.1 단위, long으로 더함). 지급·창고·정산 기록은 바꾸지 않는다. 모르는 재료·0 이하는 뺀다.</summary>
        public static Dictionary<string, long> Totals(IEnumerable<DrinkAmount> items)
        {
            var totals = new Dictionary<string, long>();
            if (items == null) return totals;
            foreach (var i in items)
            {
                if (i == null || i.amount <= 0 || DrinkIngredients.Get(i.id) == null) continue;
                totals.TryGetValue(i.id, out var t);
                totals[i.id] = checked(t + i.amount);
            }
            return totals;
        }

        /// <summary>hudfix 2026-10-19 표시용 요약: 같은 재료를 합쳐 재료표 순서로 모두(최대 재료 종류 수 8) — 빠지는 재료 없음.
        /// 긴 판(묶음 수백 개)도 재료 종류 수만큼만 나온다.</summary>
        public static string Summarize(IEnumerable<DrinkAmount> items, string separator = " · ")
        {
            var totals = Totals(items);
            var parts = new List<string>();
            foreach (var def in DrinkIngredients.All)
                if (totals.TryGetValue(def.id, out var v))
                    parts.Add($"{def.name} {DrinkRules.Format((int)Math.Min(v, int.MaxValue), def.form)}");
            return parts.Count == 0 ? "없음" : string.Join(separator, parts);
        }

        // ───── 72 연구(노트북): 모두 플레이어 행동 트랜잭션 — 저장 성공 시에만 반영, 실패는 상태 그대로 ─────

        public static ResearchState Research => Data.research;

        public static bool UnlockTrait(string id, out string error) => ResearchAction(r => DrinkResearch.Unlock(r, id, out var e) ? null : e, out error);
        public static bool EquipTrait(string id, out string error) => ResearchAction(r => DrinkResearch.Equip(r, id, out var e) ? null : e, out error);
        public static bool UnequipTrait(string id, out string error) => ResearchAction(r => DrinkResearch.Unequip(r, id) ? null : "장착하지 않은 특성입니다.", out error);
        public static bool HireStaff(string id, out string error) => ResearchAction(r => DrinkResearch.Hire(r, id, out var e) ? null : e, out error);
        public static bool SetSpecialty(string id, out string error) => ResearchAction(r => DrinkResearch.SetSpecialty(r, id, out var e) ? null : e, out error);

        static bool ResearchAction(Func<ResearchState, string> act, out string error)
        {
            string why = null;
            bool ok = Transact(d => { why = act(d.research); return why == null; }, out error);
            if (!ok && error == null) error = why;
            return ok;
        }

        /// <summary>이 배합에 적용되는 활성 제조 전문(종류·기법이 맞을 때만), 아니면 "".</summary>
        public static string ActiveSpecialtyFor(DrinkRecipe r)
        {
            var st = DrinkResearch.StaffById(Data.research.activeSpecialty);
            return st != null && DrinkCalculator.SpecialtyApplies(st, r) ? st.id : "";
        }

        /// <summary>출발 때 장착된 숙련 특성(런 시작 스냅샷용 사본, 알려진 ID만).</summary>
        public static List<string> EquippedTraitsSnapshot()
        {
            var list = new List<string>();
            foreach (var id in Data.research.equippedTraits) if (DrinkResearch.Trait(id) != null) list.Add(id);
            return list;
        }

        // ───── 72 §6 판매·주문·외형(모두 플레이어 행동 트랜잭션) ─────

        public static int Coins => Data.coins;
        public static IReadOnlyList<DrinkOrder> Orders => Data.orders;

        static bool EnsureOrders(DrinkSaveData d)
        {
            bool changed = false;
            while (d.orders.Count < DrinkSales.Slots)
            {
                int i = d.orders.Count;
                int serial = d.orderSerial++;
                d.orders.Add(DrinkSales.Generate(serial, x => d.seenIds.Contains(x) || d.Owned(x) > 0, basic: i < 3 && serial <= 3));
                changed = true;
            }
            return changed;
        }

        static void RefillCancelledOrders(DrinkSaveData d)
        {
            for (int i = 0; i < d.orders.Count; i++)
                if (!d.orders[i].IsOpen) d.orders[i] = DrinkSales.Generate(d.orderSerial++, x => d.seenIds.Contains(x) || d.Owned(x) > 0);
            EnsureOrders(d);
        }

        /// <summary>준비 음료(Ready, 아직 출발 전)를 주문에 판매: 음료 소비 + 코인을 한 거래로 저장, 그 칸은 바로 다음 주문. 출발용 사용과 중복 불가.</summary>
        public static bool Sell(int orderIndex, out int coins, out string error)
        {
            coins = 0;
            EnsureLoaded();
            var p = data.prepared;
            if (p.state != PreparedDrink.Ready) { error = p.state == PreparedDrink.InRun ? "이번 판에 사용 중인 음료는 팔 수 없습니다." : "팔 음료가 없습니다. 먼저 제조하세요."; return false; }
            if (orderIndex < 0 || orderIndex >= data.orders.Count || !data.orders[orderIndex].IsOpen) { error = "열린 주문이 아닙니다."; return false; }
            var order = data.orders[orderIndex];
            int price = DrinkSales.Price(order, p.recipe);
            if (price <= 0) { error = "주문 조건(음료 계열·지정 재료 최소량)을 채우지 못했습니다."; return false; }
            string drinkId = p.drinkId;
            bool ok = Transact(d =>
            {
                if (d.prepared.state != PreparedDrink.Ready || d.prepared.drinkId != drinkId || d.soldDrinkIds.Contains(drinkId)) return false;
                d.prepared = new PreparedDrink();
                d.coins = (int)Math.Min(int.MaxValue, (long)d.coins + price);
                d.soldDrinkIds.Add(drinkId);
                if (d.soldDrinkIds.Count > 200) d.soldDrinkIds.RemoveRange(0, d.soldDrinkIds.Count - 200);
                d.orders[orderIndex] = DrinkSales.Generate(d.orderSerial++, x => d.seenIds.Contains(x) || d.Owned(x) > 0);
                return true;
            }, out error);
            if (ok) coins = price;
            else if (error == null) error = "이미 판매했거나 바뀐 음료입니다.";
            return ok;
        }

        /// <summary>주문 취소(무료): 그 칸은 다음 런 종료 정산 때 새 주문(0~4킬 보존 포기로는 갱신 안 됨).</summary>
        public static bool CancelOrder(int orderIndex, out string error)
        {
            EnsureLoaded();
            if (orderIndex < 0 || orderIndex >= data.orders.Count || !data.orders[orderIndex].IsOpen) { error = "열린 주문이 아닙니다."; return false; }
            return Transact(d => { d.orders[orderIndex].state = "Cancelled"; return true; }, out error);
        }

        /// <summary>준비 음료 취소 시 사용된 재료를 모두 창고로 돌려보낸다. 이번 판에 사용 중인 음료는 취소할 수 없다.</summary>
        public static bool CancelPreparedAndRefund(out string error)
        {
            EnsureLoaded();
            if (data.prepared.state != PreparedDrink.Ready) { error = data.prepared.state == PreparedDrink.InRun ? "이번 판에 사용 중인 음료는 취소할 수 없습니다." : "회수할 준비 음료가 없습니다."; return false; }
            var recipe = data.prepared.recipe != null ? data.prepared.recipe.Clone() : null;
            if (recipe == null) { error = "준비 음료 정보를 읽을 수 없습니다."; return false; }
            return Transact(d =>
            {
                if (recipe.steps != null) foreach (var step in recipe.steps) if (step != null && step.amount > 0) d.Add(step.id, step.amount);
                d.prepared = new PreparedDrink();
                return true;
            }, out error);
        }
        /// <summary>준비 음료 명시 폐기(다음 제조 확정을 위해). 재료 환불 없음. 이번 판에 사용 중이면 불가.</summary>
        public static bool DiscardPrepared(out string error)
        {
            EnsureLoaded();
            if (data.prepared.state != PreparedDrink.Ready) { error = "버릴 준비 음료가 없습니다."; return false; }
            return Transact(d => { d.prepared = new PreparedDrink(); return true; }, out error);
        }

        /// <summary>코인 소유(현금 구매 소유는 commerce.json — 게임 소유 판정에서 합친다).</summary>
        public static bool CoinOwns(string id)
        {
            try { return !string.IsNullOrEmpty(id) && Data.coinOwned.Contains(id); }
            catch (Exception) { return false; }
        }

        /// <summary>코인 외형 구매(이미 소유(현금/코인)면 거부, 코인 부족 거부). 판매 코인은 외형 전용.</summary>
        public static bool BuyCosmetic(string id, Func<string, bool> cashOwned, out string error)
        {
            EnsureLoaded();
            var c = DrinkSales.Cosmetic(id);
            if (c == null) { error = "알 수 없는 외형입니다."; return false; }
            if (!DrinkSales.OnSale(c)) { error = "아트 준비 중인 외형이라 아직 판매하지 않습니다."; return false; }
            if (data.coinOwned.Contains(id) || (cashOwned != null && cashOwned(id))) { error = "이미 보유한 외형입니다."; return false; }
            if (data.coins < c.price) { error = $"코인이 {c.price - data.coins} 부족합니다."; return false; }
            return Transact(d =>
            {
                if (d.coinOwned.Contains(id) || d.coins < c.price) return false;
                d.coins -= c.price;
                d.coinOwned.Add(id);
                return true;
            }, out error);
        }

        /// <summary>맵 외형 장착(코인 소유만, "" = 직업 기본 맵). 배경만 바뀐다.</summary>
        public static bool EquipMap(string id, out string error)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && !data.coinOwned.Contains(id)) { error = "보유하지 않은 맵 외형입니다."; return false; }
            return Transact(d => { d.equippedMap = id ?? ""; return true; }, out error);
        }

        public static string EquippedMap { get { try { return Data.equippedMap; } catch (Exception) { return ""; } } }

        // ───── 테스트 ─────

        public static void SetPathForTests(string path)
        {
            overridePath = path;
            ResetSession();
        }

        /// <summary>테스트 전용: 출발 표가 메모리에 남아 있는지(InRun 표 → 실제 runId 전환 검증).</summary>
        public static string DepartTicketForTests => departTicket;

        /// <summary>새 실행 흉내(디스크 재읽기). 살아 있는 런 표시도 지운다(프로세스 종료).</summary>
        public static void ReloadForTests() => ResetSession();
    }
}
