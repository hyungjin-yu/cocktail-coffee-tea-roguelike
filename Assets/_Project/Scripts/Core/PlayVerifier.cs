#if CCT_PLAY_VERIFIER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 정상 플레이 검증 전용 입력 대행(10 §2 "정상 완주 자동화는 입력만 조작"). 검증 빌드(스크립팅 심볼 CCT_PLAY_VERIFIER)에만
    /// 컴파일되고, 실행 인자 -cct-playverify &lt;폴더&gt; + -cct-data-dir &lt;폴더&gt;가 둘 다 있을 때만 켜진다.
    /// 하는 일은 두 가지뿐이다:
    /// 1) 이동: 키보드와 같은 8방향 디지털 벡터(길이 0 또는 1)를 PlayerController.AutoInputOverride에 넣는다. 실제 이동은
    ///    PlayerController.FixedUpdate의 정상 속도·레인 제한·넉백 경로를 그대로 탄다(위치를 직접 옮기지 않는다).
    /// 2) 화면 선택: 타이틀/직업/무기/첫 안내/카드/커피 트리/하이브리드/결과/일시정지 화면의 실제 버튼 onClick 또는
    ///    버튼과 같은 공개 진입점(CardSelectView.Choose 등)을 사람이 누르는 간격(실제 0.35초 이상)으로 호출한다.
    /// HP·적 HP/위치/스폰·재료·영구 강화·난수/시드·런 시간/timeScale·무적·보스 시각·보상 수치는 읽기만 하고 쓰지 않는다.
    /// 영구 강화는 -cct-playverify-buy &lt;id&gt;를 줄 때만, 앞 런에서 실제로 번 스페셜티로 실제 상점 버튼을 눌러 산다.
    /// -cct-playverify-die-after-enhanced는 강화 보스 처치 뒤 회피를 끄고 적·장판 쪽으로 걷는 입력만 바꾼다. 텔레메트리(이동·선택·보스 등장 시각·피해 출처·결과·저장 내용·오류)를 출력 폴더에 남긴다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PlayVerifier : MonoBehaviour
    {
        // ── 설정 ──
        string outDir;
        Job job;
        WeaponKind weapon;
        int maxRuns;
        bool doAbandonCheck;
        // 선택 모드(기본 꺼짐): 실제 구매 경로로 영구 강화 1개 구매 / 강화 보스 처치 뒤 무모한 입력으로 사망 경로 확인.
        string buyNodeId;
        bool buyPending;
        bool dieAfterEnhanced;
        bool reckless;
        bool recklessFromStart;
        bool skipCoffee; // 커피 노드만 미선택(카드는 정상 최선 선택) — 강화 보스 처치 후 실제 사망 재현의 중간 강도
        bool weakBuild; // 2026-10-09: 강화 보스 처치 뒤 실제 사망 재현용 — 가장 낮은 점수 카드, 커피 노드 미선택(입력 선택만)

        // ── 출력 ──
        StreamWriter eventLog;
        StreamWriter telemetry;
        int errors, exceptions;
        float startedReal;

        // ── 런 상태 ──
        int runIndex;
        GameManager boundGame;
        Enemy lastBoss;
        string lastBossName;
        BossKind lastBossKind;
        float playingReal;      // Playing(일시정지·카운트다운·선택창 제외) 동안 흐른 실제 시간
        float lastHp = -1f;
        float lastTelemetryReal;
        float lastDecisionReal;
        Vector2 currentInput;
        Vector2 lastPos;
        float distanceMoved;
        int inputFramesMoving, inputFramesIdle, directionChanges;
        readonly Dictionary<string, float> damageBySource = new Dictionary<string, float>();
        readonly List<string> runSummaries = new List<string>();
        readonly List<string> choices = new List<string>();
        readonly List<string> bossEvents = new List<string>();
        readonly Queue<string> recentHits = new Queue<string>();
        bool anyClear;

        // 읽기 전용 리플렉션(지각용) — 값을 쓰지 않는다.
        static readonly FieldInfo BurstRadius = typeof(BurstZone).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo BurstDps = typeof(BurstZone).GetField("damagePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo SlowRadius = typeof(SlowZone).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo ProjVelocity = typeof(EnemyProjectile).GetField("velocity", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo VfxStart = typeof(FadeVfx).GetField("startScale", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo VfxEnd = typeof(FadeVfx).GetField("endScale", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo VfxLife = typeof(FadeVfx).GetField("life", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo VfxElapsed = typeof(FadeVfx).GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo CardOffers = typeof(CardSelectView).GetField("offers", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo SkillTreeField = typeof(SkillTreeView).GetField("tree", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        bool forcedJob;

        public void Init(string dir, string weaponArg, string runsArg, bool abandonCheck, string buyNode = null, bool dieAfterEnhancedKill = false,
            bool recklessFromRunStart = false)
        {
            skipCoffee = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-cct-playverify-skip-coffee");
            weakBuild = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-cct-playverify-weak-build");
            recklessFromStart = recklessFromRunStart; // 2026-10-09: 10 §2 "실패 1회" 정상 입력 경로 — 처음부터 회피 없이 적 쪽으로 걷는다(입력만 변경)
            buyNodeId = string.IsNullOrEmpty(buyNode) ? null : buyNode;
            buyPending = buyNodeId != null;
            dieAfterEnhanced = dieAfterEnhancedKill;
            outDir = dir;
            Directory.CreateDirectory(outDir);
            weapon = ParseWeapon(weaponArg);
            job = weapon == WeaponKind.Rapier || weapon == WeaponKind.Gun ? Job.Bartender : Job.TeaSommelier;
            // 48 QA 강제 비교: 같은 시드·같은 직업(맵·몹 구성)에서 무기만 바꾼다. 정상 플레이 근거와 구분해 보고.
            var argv = System.Environment.GetCommandLineArgs();
            int si = System.Array.IndexOf(argv, "-cct-playverify-seed");
            if (si >= 0 && si + 1 < argv.Length && int.TryParse(argv[si + 1], out var seed)) RunRandom.QaSeedOverride = seed;
            RapierWeapon.QaDisableSwordStagger = System.Array.Exists(argv, a => a == "-cct-playverify-nostagger");
            int ji = System.Array.IndexOf(argv, "-cct-playverify-forcejob");
            if (ji >= 0 && ji + 1 < argv.Length) { forcedJob = true; job = argv[ji + 1] == "tea" ? Job.TeaSommelier : Job.Bartender; }
            maxRuns = int.TryParse(runsArg, out var n) ? Mathf.Clamp(n, 1, 3) : 3;
            doAbandonCheck = abandonCheck;
            startedReal = Time.realtimeSinceStartup;
            eventLog = new StreamWriter(Path.Combine(outDir, "events.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            telemetry = new StreamWriter(Path.Combine(outDir, "telemetry.csv"), false, new UTF8Encoding(false)) { AutoFlush = false };
            telemetry.WriteLine("run,real,playingReal,runTime,state,hp,maxHp,x,y,inX,inY,kills,enemies,enemyShots,zones,cocktailLv,teaLv,beans,coffeePts,coffeeNodes,hybrid,boss,bossHp,fps,permanentCount,challengeCoffeeCount,enemyHpMultiplier,enemyDamageMultiplier,unlockTotal,spawnMultiplier,regularCap,regularSpawns,waveSpawns");
            Application.logMessageReceived += OnLog;
            Event($"playverify start version={Application.version} job={job} weapon={weapon} maxRuns={maxRuns} abandonCheck={doAbandonCheck} buy={buyNodeId ?? "-"} dieAfterEnhanced={dieAfterEnhanced} weakBuild={weakBuild} skipCoffee={skipCoffee} " +
                  $"data={LaunchOptions.DataDir} save={PermanentProgress.ActiveSavePath} unity={Application.unityVersion}");
            Event("contract: input-only — 8-direction digital move vector via AutoInputOverride + normal UI buttons; no HP/enemy/spawn/material/RNG/time/invulnerability/boss/reward writes" +
                  (buyNodeId != null ? $"; permanent purchase only via real shop button Node_{buyNodeId} with earned specialty" : "; no permanent purchases") +
                  (dieAfterEnhanced ? "; after enhanced boss kill: reckless input (walk into enemies/zones, no dodging, first card offered)" : ""));
            SnapshotSave("boot");
            StartCoroutine(Flow());
        }

        static WeaponKind ParseWeapon(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "gun": return WeaponKind.Gun;
                case "sword": return WeaponKind.Sword;
                case "bow": return WeaponKind.Bow;
                default: return WeaponKind.Rapier;
            }
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            telemetry?.Flush();
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions++;
            else if (type == LogType.Error || type == LogType.Assert) errors++;
            else return;
            Event($"[{type}] {condition} | {FirstLine(stackTrace)}");
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i);
        }

        float Real => Time.realtimeSinceStartup - startedReal;

        void Event(string text)
        {
            var g = GameManager.Instance;
            string rt = g != null ? g.RunTime.ToString("0.00", Inv) : "-";
            eventLog?.WriteLine($"{Real.ToString("0.00", Inv),8}s run={runIndex} runTime={rt} {text}");
        }

        // ── 화면 흐름(실제 버튼) ──────────────────────────────────────────────

        static IEnumerator Human(float seconds = 0.4f)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        static bool ClickNamed(Component root, string name)
        {
            if (root == null) return false;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == name && b.gameObject.activeInHierarchy && b.interactable)
                {
                    b.onClick.Invoke();
                    return true;
                }
            return false;
        }

        IEnumerator Flow()
        {
            yield return Human(2f);
            var title = FindFirstObjectByType<TitleView>();
            Event($"title open={(title != null && title.IsOpen)}");
            if (title == null || !ClickNamed(title, "StartButton")) { Event("FAIL title start button"); yield return Finish(); yield break; }

            for (runIndex = 1; runIndex <= maxRuns; runIndex++)
            {
                yield return Human(1f);
                yield return SelectAndStart();
                if (GameManager.Instance == null) { Event("FAIL run did not start"); break; }
                yield return PlayRun();
                bool cleared = boundGame != null && boundGame.Cleared;
                yield return Human(2.5f);
                var result = FindFirstObjectByType<ResultView>();
                Event($"result open={(result != null && result.IsOpen)} title=\"{result?.TitleText}\" stats=\"{result?.StatsText?.Replace("\n", " / ")}\"");
                SnapshotSave($"after run {runIndex}");
                if (cleared) anyClear = true;
                bool continueForBuy = buyPending && runIndex < maxRuns;
                bool last = (cleared && !continueForBuy) || runIndex == maxRuns;
                if (!last)
                {
                    if (!ClickNamed(result, "RestartButton")) { Event("FAIL restart button"); break; }
                    Event("clicked result RestartButton (다시 시작)");
                }
                else
                {
                    if (!ClickNamed(result, "TitleButton")) { Event("FAIL title button"); break; }
                    Event("clicked result TitleButton");
                    break;
                }
            }

            yield return Human(2f);
            var t2 = FindFirstObjectByType<TitleView>();
            Event($"title after runs open={(t2 != null && t2.IsOpen)} footer=\"{t2?.SaveNoticeText}\"");

            if (doAbandonCheck && t2 != null && ClickNamed(t2, "StartButton"))
            {
                runIndex = 99;
                yield return Human(1f);
                yield return SelectAndStart();
                var g = GameManager.Instance;
                if (g != null)
                {
                    BindGame(g);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (Time.realtimeSinceStartup < until && g != null && g.State != GameState.GameOver)
                    {
                        HandleRewardScreens(g);
                        yield return null;
                    }
                    if (g != null && g.State != GameState.GameOver)
                    {
                        currentInput = Vector2.zero;
                        g.Player.AutoInputOverride = Vector2.zero;
                        g.OpenPause(); // Esc와 같은 진입점(GameManager.Update의 Esc 처리)
                        Event("abandon check: opened pause (Esc path)");
                        yield return Human(0.6f);
                        if (ClickNamed(g.PauseView, "GiveUpButton")) Event("clicked pause GiveUpButton (런 포기)");
                        yield return Human(0.6f);
                        Transform confirm = null;
                        foreach (var t in g.GetComponentsInChildren<Transform>(true))
                            if (t.name == "EndRunConfirm") { confirm = t; break; }
                        if (confirm != null && ClickNamed(confirm, "Confirm")) Event("clicked confirm (포기)");
                        else Event("FAIL abandon confirm button not found");
                        yield return Human(1.5f);
                        var r = FindFirstObjectByType<ResultView>();
                        Event($"abandon result open={(r != null && r.IsOpen)} title=\"{r?.TitleText}\" reason={g.EndReason}");
                        SnapshotSave("after abandon");
                        ClickNamed(r, "TitleButton");
                        yield return Human(1.5f);
                    }
                }
            }

            yield return Finish();
        }

        IEnumerator SelectAndStart()
        {
            var select = FindFirstObjectByType<CharacterSelectView>();
            float wait = Time.realtimeSinceStartup + 5f;
            while (select == null && Time.realtimeSinceStartup < wait) { yield return null; select = FindFirstObjectByType<CharacterSelectView>(); }
            if (select == null) { Event("FAIL no CharacterSelectView"); yield break; }
            if (buyPending && runIndex > 1) yield return BuyPermanent(select);
            if (forcedJob)
            {
                Destroy(select.gameObject);
                yield return null;
                GameBootstrap.Build(job, weapon);
                Event($"QA forced start job={job} weapon={weapon} seed={GameManager.Instance?.Seed}");
                yield return Human(0.8f);
                var forcedTutorial = FindFirstObjectByType<FirstRunTutorialView>();
                if (forcedTutorial != null && forcedTutorial.IsOpen) ClickNamed(forcedTutorial, "DismissButton");
                yield break;
            }
            (job == Job.Bartender ? select.BartenderButton : select.TeaSommelierButton).onClick.Invoke();
            Event($"clicked job {job}");
            yield return Human();
            bool isA = weapon == WeaponKind.Rapier || weapon == WeaponKind.Sword;
            (isA ? select.WeaponButtonA : select.WeaponButtonB).onClick.Invoke();
            Event($"clicked weapon {(isA ? "A" : "B")} selected={select.SelectedWeapon}");
            yield return Human();
            select.StartButton.onClick.Invoke();
            Event("clicked 영업 시작");
            yield return Human(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen)
            {
                ClickNamed(tutorial, "DismissButton");
                Event("first-run tutorial shown → clicked 시작");
                yield return Human(0.5f);
            }
        }

        /// <summary>직업 선택 화면의 "영구 강화" 버튼 → 노드 버튼(Node_id) → 뒤로. 실제로 번 스페셜티로만 산다.</summary>
        IEnumerator BuyPermanent(CharacterSelectView select)
        {
            buyPending = false;
            SnapshotSave("before buy");
            if (select.PermanentShopButton == null || !select.PermanentShopButton.interactable) { Event("FAIL permanent shop button"); yield break; }
            select.PermanentShopButton.onClick.Invoke();
            Event("clicked 영구 강화");
            yield return Human(0.8f);
            var shop = FindFirstObjectByType<PermanentSkillView>();
            if (shop == null || !shop.IsOpen) { Event("FAIL permanent shop not open"); yield break; }
            bool clicked = ClickNamed(shop, "Node_" + buyNodeId);
            Event($"clicked shop Node_{buyNodeId} clicked={clicked} result={shop.LastPurchaseResult} notice=\"{shop.NoticeText}\"");
            SnapshotSave("after buy");
            yield return Human(0.6f);
            if (ClickNamed(shop, "BackButton")) Event("clicked shop BackButton");
            yield return Human(0.6f);
        }

        void BindGame(GameManager g)
        {
            boundGame = g;
            lastBoss = null;
            lastBossName = null;
            reckless = recklessFromStart;
            if (reckless) Event("reckless mode ON from run start (failure-run check): walk into nearest enemy/zone, no dodging, first card offered");
            playingReal = 0f;
            lastHp = g.Player.Hp;
            lastPos = g.Player.Position;
            distanceMoved = 0f;
            inputFramesMoving = inputFramesIdle = directionChanges = 0;
            damageBySource.Clear();
            recentHits.Clear();
            Event($"run bound job={g.Job} weapon={g.Weapon} seed={g.Seed} runId={g.RunId} hp={g.Player.Hp:0}/{g.Player.MaxHp:0} pos=({g.Player.Position.x:0.00},{g.Player.Position.y:0.00}) " +
                  $"perm=[{string.Join(",", PermanentProgress.Current.unlockedPermanentSkillIds)}]");
        }

        IEnumerator PlayRun()
        {
            var g = GameManager.Instance;
            BindGame(g);
            while (g != null && g.State != GameState.GameOver)
            {
                HandleRewardScreens(g);
                yield return null;
            }
            if (g == null) yield break;
            var p = g.Player;
            string summary = $"run {runIndex} {g.Job}/{g.Weapon} end reason={g.EndReason} cleared={g.Cleared} runTime={g.RunTime:0.0} playingReal={playingReal:0.0} " +
                             $"kills={g.Kills} hp={p.Hp:0.0}/{p.MaxHp:0.0} cocktailLv={g.CocktailGauge.Level} teaLv={g.TeaGauge.Level} coffeeNodes={g.Skills.UnlockedCount} " +
                             $"hybrid={g.HybridUnlocked} seed={g.Seed} dist={distanceMoved:0} moveFrames={inputFramesMoving} idleFrames={inputFramesIdle} dirChanges={directionChanges} " +
                             $"damage[{FormatDamage()}]";
            Event("END " + summary);
            if (!g.Cleared)
            {
                Event("last hits before end: " + string.Join(" ; ", recentHits));
            }
            runSummaries.Add(summary);
            telemetry.Flush();
        }

        string FormatDamage()
        {
            var parts = new List<string>();
            foreach (var kv in damageBySource) parts.Add($"{kv.Key}:{kv.Value:0}");
            parts.Sort();
            return string.Join(",", parts);
        }

        float uiSince = -1f;
        const float HumanUiDelay = 0.45f;

        /// <summary>선택창은 사람이 읽고 누르는 간격(실제 0.45초)을 둔 뒤 실제 선택 경로를 호출한다.</summary>
        void HandleRewardScreens(GameManager g)
        {
            bool selecting = !g.IsPaused && (g.State == GameState.LevelUp || g.State == GameState.SkillSelect || g.State == GameState.HybridOffer);
            if (!selecting) { uiSince = -1f; return; }
            float now = Time.realtimeSinceStartup;
            if (uiSince < 0f) { uiSince = now; return; }
            if (now - uiSince < HumanUiDelay) return;
            uiSince = now; // 다음 화면/다음 노드도 같은 간격 뒤에
            if (g.State == GameState.LevelUp && g.CardView.IsOpen) ChooseCard(g);
            else if (g.State == GameState.SkillSelect && g.SkillView.IsOpen) ChooseCoffee(g);
            else if (g.State == GameState.HybridOffer && g.HybridView.IsOpen)
            {
                g.HybridView.Unlock(); // "해금" 버튼 onClick과 같은 진입점
                Log($"hybrid unlock clicked → hybrid={g.HybridUnlocked}");
            }
        }

        void Log(string choice)
        {
            choices.Add($"run{runIndex}@{GameManager.Instance?.RunTime:0.0}: {choice}");
            Event(choice);
        }

        void ChooseCard(GameManager g)
        {
            var offers = CardOffers.GetValue(g.CardView) as List<CardOffer>;
            if (offers == null || offers.Count == 0) return;
            int best = 0;
            float bestScore = float.MinValue;
            var desc = new StringBuilder();
            for (int i = 0; i < offers.Count; i++)
            {
                float s = ScoreCard(g, offers[i].card);
                desc.Append($"[{i + 1}] {offers[i].card.id}({offers[i].card.rarity},{offers[i].currentStacks}) s={s:0.0} ");
                if (s > bestScore) { bestScore = s; best = i; }
            }
            if (weakBuild)
            {
                float worst = float.MaxValue;
                for (int i = 0; i < offers.Count; i++) { float s = ScoreCard(g, offers[i].card); if (s < worst) { worst = s; best = i; } }
            }
            if (reckless) best = 0; // 무모 모드: 생존 판단 없이 첫 카드
            var chosen = offers[best].card.id;
            g.CardView.Choose(best); // 카드 클릭/숫자키와 같은 진입점
            Log($"card offer {desc}→ chose {chosen}");
        }

        float ScoreCard(GameManager g, CardData c)
        {
            bool ranged = g.Weapon.IsRanged();
            var p = g.Player;
            float hpFrac = p.Hp / Mathf.Max(1f, p.MaxHp);
            float survival = hpFrac < 0.5f ? 1.6f : 1f;
            float s = 0f;
            if (c.effects == null) return 0f;
            foreach (var e in c.effects)
            {
                switch (e.type)
                {
                    case CardEffectType.DamagePercent: s += e.value * 100f; break;
                    case CardEffectType.AttackSpeedPercent: s += e.value * 95f; break;
                    case CardEffectType.ExtraThrust: s += 34f * e.value; break;
                    case CardEffectType.PierceCount: s += 22f * e.value; break;
                    case CardEffectType.FlameLevel: s += 20f * e.value; break;
                    case CardEffectType.PoisonLevel: s += 20f * e.value; break;
                    case CardEffectType.MaxHpFlat: s += e.value * 0.75f * survival; break;
                    case CardEffectType.RegenBonus: s += e.value * 11f * survival; break;
                    case CardEffectType.MoveSpeedPercent: s += e.value * 90f; break;
                    case CardEffectType.RangePercent: s += e.value * (ranged ? 45f : 75f); break;
                    case CardEffectType.GaugeNeedPercent: s += -e.value * 70f; break;
                    case CardEffectType.DropChancePercent: s += e.value * 40f; break;
                    case CardEffectType.DamageReduction: s += e.value * 100f * survival; break;
                    case CardEffectType.PickupRadiusFlat: s += e.value * 4f; break;
                }
            }
            return s;
        }

        static readonly string[] CoffeePriority =
        {
            "espresso_shot", "caffeine_rush", "ristretto", "doppio_body", "mellow_finish", "balanced_extraction",
            "insulated_cup", "full_body_roast", "steady_hand", "perfect_extraction",
            "cold_brew", "fine_grind", "wide_tray", "measured_pour", "zero_waste",
        };

        void ChooseCoffee(GameManager g)
        {
            var tree = SkillTreeField.GetValue(g.SkillView) as SkillTree;
            if (tree == null || weakBuild || skipCoffee) { g.SkillView.Continue(); Log($"coffee tree 계속하기 (weakBuild={weakBuild} skipCoffee={skipCoffee}) pointsLeft={g.CoffeePoints}"); return; }
            foreach (var id in CoffeePriority)
            {
                SkillData node = null;
                foreach (var n in tree.Nodes) if (n.id == id) { node = n; break; }
                if (node == null) continue;
                if (tree.StateOf(node, g.CoffeePoints) != CoffeeNodeState.Available) continue;
                g.SkillView.Choose(node); // 노드 클릭과 같은 진입점(입력 1회, 1포인트)
                Log($"coffee node {id} → unlocked={tree.IsUnlocked(node)} pointsLeft={g.CoffeePoints}");
                return; // 다음 노드는 다음 사람 간격 뒤에
            }
            g.SkillView.Continue(); // "계속하기"
            Log($"coffee tree 계속하기 pointsLeft={g.CoffeePoints}");
        }

        // ── 전투 중 이동(8방향 디지털 입력) ─────────────────────────────────────

        static readonly Vector2[] Directions = BuildDirections();

        static Vector2[] BuildDirections()
        {
            var list = new List<Vector2> { Vector2.zero };
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                // 키보드 두 키 동시 입력은 (±1,±1)을 길이 1로 줄인 값 — PlayerController.ReadKeyboardInput과 같다.
                list.Add(Vector2.ClampMagnitude(new Vector2(Mathf.Round(Mathf.Cos(a)), Mathf.Round(Mathf.Sin(a))), 1f));
            }
            return list.ToArray();
        }

        struct Zone { public Vector2 c; public float r; public float w; }
        struct Band { public Vector2 mid; public Vector2 axis; public float halfLen; public float halfWidth; }
        struct Shot { public Vector2 p; public Vector2 v; }

        readonly List<Zone> zones = new List<Zone>();
        readonly List<Band> bands = new List<Band>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Vector2> pickups = new List<Vector2>();
        readonly Dictionary<Enemy, Vector2> foeVelocity = new Dictionary<Enemy, Vector2>();

        void Update()
        {
            var g = GameManager.Instance;
            if (g == null || g != boundGame || g.Player == null) return;
            var p = g.Player;

            bool active = g.State == GameState.Playing && !g.IsPaused && !g.IsResuming;
            if (active) playingReal += Time.unscaledDeltaTime;

            TrackBoss(g);
            TrackDamage(g, p);

            if (active && !p.IsDead)
            {
                if (Time.realtimeSinceStartup - lastDecisionReal >= 1f / 30f)
                {
                    lastDecisionReal = Time.realtimeSinceStartup;
                    var next = Decide(g, p);
                    if (next != currentInput) directionChanges++;
                    currentInput = next;
                }
                p.AutoInputOverride = currentInput;
                if (currentInput.sqrMagnitude > 0f) inputFramesMoving++; else inputFramesIdle++;
                distanceMoved += Vector2.Distance(lastPos, p.Position);
            }
            else
            {
                p.AutoInputOverride = Vector2.zero; // 선택창/일시정지 중 키를 누르고 있지 않음
            }
            lastPos = p.Position;

            if (Time.realtimeSinceStartup - lastTelemetryReal >= 1f)
            {
                lastTelemetryReal = Time.realtimeSinceStartup;
                WriteTelemetry(g, p);
            }
        }

        void TrackBoss(GameManager g)
        {
            var boss = g.ActiveBoss;
            if (boss != null && boss != lastBoss)
            {
                lastBoss = boss;
                lastBossName = boss.Def.name;
                lastBossKind = boss.Def.boss;
                string e = $"BOSS SPAWN {boss.Def.name} runTime={g.RunTime:0.00} playingReal={playingReal:0.00} hp={boss.Hp:0}/{boss.MaxHp:0} playerHp={g.Player.Hp:0}/{g.Player.MaxHp:0}";
                bossEvents.Add($"run{runIndex}: {e}");
                Event(e);
            }
            else if (boss == null && !ReferenceEquals(lastBoss, null)) // 파괴된 보스는 Unity == null이라 참조로 비교
            {
                string e = $"BOSS DOWN {lastBossName} runTime={g.RunTime:0.00} playingReal={playingReal:0.00} playerHp={g.Player.Hp:0}/{g.Player.MaxHp:0}";
                bossEvents.Add($"run{runIndex}: {e}");
                Event(e);
                lastBoss = null;
                if (dieAfterEnhanced && lastBossKind == BossKind.Enhanced && !reckless)
                {
                    reckless = true;
                    Event("reckless mode ON (after enhanced boss kill): walk into nearest boss/enemy/zone, no dodging, first card offered");
                }
            }
        }

        void TrackDamage(GameManager g, PlayerController p)
        {
            float hp = p.Hp;
            if (lastHp >= 0f && hp < lastHp - 0.01f)
            {
                float dmg = lastHp - hp;
                string src = AttributeDamage(p);
                damageBySource.TryGetValue(src, out var acc);
                damageBySource[src] = acc + dmg;
                string hit = $"{g.RunTime:0.0}s -{dmg:0.0} {src} hp={hp:0}";
                recentHits.Enqueue(hit);
                while (recentHits.Count > 12) recentHits.Dequeue();
                if (dmg >= 8f) Event($"hit {hit} pos=({p.Position.x:0.0},{p.Position.y:0.0})");
            }
            lastHp = hp;
        }

        string AttributeDamage(PlayerController p)
        {
            Vector2 pos = p.Position;
            Enemy near = null;
            float best = float.MaxValue;
            foreach (var e in Enemy.Active)
            {
                if (e == null || e.IsDead) continue;
                float d = Vector2.Distance(pos, e.transform.position) - e.Def.radius;
                if (d < best) { best = d; near = e; }
            }
            foreach (var pr in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                if (Vector2.Distance(pr.transform.position, pos) < 0.9f) return "projectile";
            if (near != null && best < 0.9f) return near.Def.name + (near.Def.IsBoss ? "(boss)" : "");
            foreach (var z in FindObjectsByType<BurstZone>(FindObjectsSortMode.None))
                if (Vector2.Distance(z.transform.position, pos) <= (float)BurstRadius.GetValue(z) + 0.05f) return "zone";
            if (near != null && near.Def.IsBoss && best < 4.5f) return near.Def.name + "(boss-pattern)";
            return near != null ? $"other(near {near.Def.name})" : "other";
        }

        void WriteTelemetry(GameManager g, PlayerController p)
        {
            var boss = g.ActiveBoss;
            var spawner = g.GetComponentInChildren<EnemySpawner>();
            int shotsCount = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length;
            int zoneCount = FindObjectsByType<BurstZone>(FindObjectsSortMode.None).Length;
            telemetry.WriteLine(string.Join(",",
                runIndex.ToString(Inv), Real.ToString("0.0", Inv), playingReal.ToString("0.0", Inv), g.RunTime.ToString("0.0", Inv),
                g.IsPaused ? "Paused" : g.IsResuming ? "Resuming" : g.State.ToString(),
                p.Hp.ToString("0.0", Inv), p.MaxHp.ToString("0", Inv), p.Position.x.ToString("0.00", Inv), p.Position.y.ToString("0.00", Inv),
                currentInput.x.ToString("0.00", Inv), currentInput.y.ToString("0.00", Inv), g.Kills.ToString(Inv), Enemy.Active.Count.ToString(Inv),
                shotsCount.ToString(Inv), zoneCount.ToString(Inv), g.CocktailGauge.Level.ToString(Inv), g.TeaGauge.Level.ToString(Inv),
                g.BeanGauge.EffectiveTotal.ToString(Inv), g.CoffeePoints.ToString(Inv), g.Skills.UnlockedCount.ToString(Inv), g.HybridUnlocked ? "1" : "0",
                boss != null ? boss.Def.ArtId : "", boss != null ? boss.Hp.ToString("0", Inv) : "",
                (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("0", Inv),
                g.Challenge.PermanentCount.ToString(Inv), g.Challenge.CoffeeCount.ToString(Inv),
                g.Challenge.HpMultiplier.ToString("0.000", Inv), g.Challenge.DamageMultiplier.ToString("0.000", Inv),
                g.Challenge.PermanentUnlockTotal.ToString(Inv), g.Challenge.SpawnMultiplier.ToString("0.00", Inv), g.Challenge.RegularAliveCap.ToString(Inv),
                (spawner != null ? spawner.RegularSpawns : -1).ToString(Inv), (spawner != null ? spawner.WaveSpawns : -1).ToString(Inv)));
            if ((int)Real % 15 == 0) telemetry.Flush();
        }

        void Perceive(PlayerController p)
        {
            zones.Clear();
            bands.Clear();
            shots.Clear();
            pickups.Clear();
            foeVelocity.Clear();
            foreach (var e in Enemy.Active)
            {
                if (e == null || e.IsDead) continue;
                var rb = e.GetComponent<Rigidbody2D>();
                foeVelocity[e] = rb != null ? rb.linearVelocity : Vector2.zero; // 읽기 전용(실제 추격 속도·돌진 포함)
            }
            foreach (var z in FindObjectsByType<BurstZone>(FindObjectsSortMode.None))
                zones.Add(new Zone { c = z.transform.position, r = (float)BurstRadius.GetValue(z), w = 40f });
            foreach (var z in FindObjectsByType<SlowZone>(FindObjectsSortMode.None))
                zones.Add(new Zone { c = z.transform.position, r = (float)SlowRadius.GetValue(z), w = 12f });
            foreach (var pr in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                shots.Add(new Shot { p = pr.transform.position, v = (Vector2)ProjVelocity.GetValue(pr) });
            foreach (var pk in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
                pickups.Add(pk.transform.position);
            // 적 예고(FadeVfx 원/사각, 정렬 4 이상). 플레이어 자신의 화염/독/찌르기 효과는 정렬 1이라 제외된다.
            foreach (var f in FindObjectsByType<FadeVfx>(FindObjectsSortMode.None))
            {
                var sr = f.Renderer;
                if (sr == null || sr.sortingOrder < 4) continue;
                var s0 = (Vector3)VfxStart.GetValue(f);
                var s1 = (Vector3)VfxEnd.GetValue(f);
                float life = (float)VfxLife.GetValue(f);
                float elapsed = (float)VfxElapsed.GetValue(f);
                if (sr.sprite == SpriteFactory.Circle)
                {
                    float r = Mathf.Max(s0.x, s1.x) * 0.5f;
                    if (r < 0.5f || life < 0.3f) continue; // 짧은 피격/폭발 잔상은 예고가 아니다
                    zones.Add(new Zone { c = f.transform.position, r = r, w = 70f + 30f * Mathf.Clamp01(elapsed / life) });
                }
                else if (sr.sprite == SpriteFactory.Square)
                {
                    if (life < 0.3f) continue;
                    var axis = (Vector2)(f.transform.rotation * Vector3.right);
                    bands.Add(new Band
                    {
                        mid = f.transform.position, axis = axis.normalized,
                        halfLen = Mathf.Max(s0.x, s1.x) * 0.5f, halfWidth = Mathf.Max(0.35f, Mathf.Max(s0.y, s1.y) * 0.5f),
                    });
                }
            }
        }

        Vector2 Decide(GameManager g, PlayerController p)
        {
            Perceive(p);
            Vector2 pos = p.Position;
            if (reckless) return RecklessDirection(g, pos);
            float speed = p.Stats.MoveSpeed;
            bool ranged = g.Weapon.IsRanged();
            float acquire = ranged ? p.Stats.Range + 3.6f : p.Stats.Range + 0.6f;

            // 공격 대상: 보스(도착·타격 가능) 우선, 없으면 가장 가까운 적.
            Enemy target = null;
            float targetDist = float.MaxValue;
            var boss = g.ActiveBoss;
            if (!ranged)
            {
                // v3 근접: 사람처럼 멀리서 쏘는 적(투척·탄막)과 소환형을 먼저 쫓아가 정리하고, 보스는 가까우면 우선한다.
                // (p1/p2 근접 런에서 가까운 추적형만 노리다 원거리 적 탄이 쌓여 사망 — 입력 전략 문제)
                float bestScore = float.MaxValue;
                foreach (var e in Enemy.Active)
                {
                    if (e == null || e.IsDead || e.Invulnerable) continue;
                    if (e.Def.IsBoss && !e.HasArrived) continue;
                    float d = Vector2.Distance(pos, e.transform.position);
                    if (d > 12f) continue;
                    var a = e.Def.archetype;
                    float bonus = e.Def.IsBoss ? 2.5f : a == EnemyArchetype.Summoner ? 4f
                        : a == EnemyArchetype.Ranged || a == EnemyArchetype.EliteBarrage ? 3f : 0f;
                    float score = d - bonus;
                    if (score < bestScore) { bestScore = score; target = e; targetDist = d; }
                }
            }
            else if (boss != null && !boss.IsDead && boss.HasArrived && !boss.Invulnerable)
            {
                target = boss;
                targetDist = Vector2.Distance(pos, boss.transform.position);
            }
            if (target == null)
            {
                foreach (var e in Enemy.Active)
                {
                    if (e == null || e.IsDead || e.Invulnerable) continue;
                    float d = Vector2.Distance(pos, e.transform.position);
                    if (d < targetDist) { targetDist = d; target = e; }
                }
            }

            Vector2 nearestPickup = Vector2.zero;
            float pickupDist = float.MaxValue;
            foreach (var pk in pickups)
            {
                float d = Vector2.Distance(pos, pk);
                if (d < pickupDist) { pickupDist = d; nearestPickup = pk; }
            }

            float hpFrac = p.Hp / Mathf.Max(1f, p.MaxHp);
            float caution = hpFrac < 0.35f ? 1.8f : hpFrac < 0.6f ? 1.3f : 1f;

            int bestIndex = 0;
            float bestCost = float.MaxValue;
            for (int i = 0; i < Directions.Length; i++)
            {
                var dir = Directions[i];
                float cost = 0f;
                foreach (float horizon in new[] { 0.25f, 0.6f })
                {
                    Vector2 q = pos + dir * speed * horizon;
                    q.y = Mathf.Clamp(q.y, Balance.DepthMin, Balance.DepthMax);
                    cost += CostAt(q, pos, dir, speed, horizon, caution, !ranged) * (horizon < 0.3f ? 1.4f : 1f);
                }
                Vector2 qEnd = pos + dir * speed * 0.6f;
                qEnd.y = Mathf.Clamp(qEnd.y, Balance.DepthMin, Balance.DepthMax);

                // 공격 위치 보상
                if (target != null)
                {
                    foeVelocity.TryGetValue(target, out var tv);
                    Vector2 tPred = (Vector2)target.transform.position + tv * 0.6f; // 도망가는 투척형도 따라잡도록 이동 예측
                    float dEnd = Vector2.Distance(qEnd, tPred);
                    float contact = target.Def.radius + 0.55f;
                    float want = ranged ? Mathf.Min(acquire - 0.7f, Mathf.Max(contact + 2.2f, 3.8f)) : Mathf.Min(acquire - 0.35f, contact + 0.9f);
                    if (dEnd <= acquire - 0.2f) cost -= (ranged ? 6f : 10f) / caution;
                    // 레이피어 하이브리드: 해금 뒤 보조 활(사거리+3.6, 07 §4)도 맞힐 수 있는 거리는 작은 보상(체력이 낮을 때 거리를 두기 쉬움)
                    else if (!ranged && g.HybridUnlocked && dEnd <= p.Stats.Range + 3.4f) cost -= 4f;
                    cost += Mathf.Abs(dEnd - want) * (ranged ? 0.9f : 1.3f) / caution;
                }
                // 재료 줍기
                if (pickupDist < 14f)
                {
                    float dEnd = Vector2.Distance(qEnd, nearestPickup);
                    cost += (dEnd - pickupDist) * 0.9f;
                }
                // 레인 가장자리에 몰리지 않기(넉백·포위 대비)
                float edge = Mathf.Min(qEnd.y - Balance.DepthMin, Balance.DepthMax - qEnd.y);
                if (edge < 1.0f) cost += (1.0f - edge) * 3f;
                if (dir == Vector2.zero) cost += 0.6f;      // 정지보다 계속 움직이기 선호
                if (dir == currentInput) cost -= 0.8f;       // 사람처럼 방향을 자주 바꾸지 않기

                if (cost < bestCost) { bestCost = cost; bestIndex = i; }
            }
            return Directions[bestIndex];
        }

        /// <summary>강화 보스 처치 뒤 사망 경로 확인용: 가장 가까운 보스/장판/적 쪽으로 걸어간다(회피 없음). 입력만 바꾼다.</summary>
        Vector2 RecklessDirection(GameManager g, Vector2 pos)
        {
            Vector2 goal = pos;
            float best = float.MaxValue;
            var boss = g.ActiveBoss;
            if (boss != null && !boss.IsDead && boss.HasArrived) { goal = boss.transform.position; best = 0f; }
            else
            {
                foreach (var z in zones)
                {
                    float d = Vector2.Distance(pos, z.c) - 3f; // 장판을 조금 더 선호
                    if (d < best) { best = d; goal = z.c; }
                }
                foreach (var e in Enemy.Active)
                {
                    if (e == null || e.IsDead) continue;
                    float d = Vector2.Distance(pos, e.transform.position) - (e.Def.explodesOnContact ? 2f : 0f);
                    if (d < best) { best = d; goal = e.transform.position; }
                }
            }
            Vector2 want = goal - pos;
            if (want.sqrMagnitude < 0.04f) return Vector2.zero;
            int bi = 1;
            float bd = float.MinValue;
            for (int i = 1; i < Directions.Length; i++)
            {
                float dot = Vector2.Dot(Directions[i], want.normalized);
                if (dot > bd) { bd = dot; bi = i; }
            }
            return Directions[bi];
        }

        float CostAt(Vector2 q, Vector2 from, Vector2 dir, float speed, float horizon, float caution, bool melee)
        {
            float cost = 0f;
            foreach (var e in Enemy.Active)
            {
                if (e == null || e.IsDead) continue;
                var def = e.Def;
                Vector2 ep = e.transform.position;
                float dist0 = Vector2.Distance(q, ep);
                if (dist0 > 9f) continue;
                // v3: 실제 강체 속도(돌진·후퇴 포함)로 예측하고, 거의 멈춰 있으면 플레이어 쪽 추격(정격 속도)으로 근사한다.
                foeVelocity.TryGetValue(e, out var ev);
                Vector2 epPred;
                if (ev.sqrMagnitude > 0.04f) epPred = ep + ev * horizon;
                else
                {
                    float step = def.speed * horizon * (def.IsBoss ? 1f : 0.9f);
                    epPred = ep + (q - ep).normalized * Mathf.Min(step, Mathf.Max(0f, dist0 - 0.5f));
                }
                float dist = Vector2.Distance(q, epPred);
                float contact = def.radius + 0.4f + 0.15f;
                // 근접은 획득 반경(사거리+0.6)이 짧아 보스·일반 적 여유 거리를 그 안으로 줄인다(v2, p1 근접 런이 보스에 닿지 못함).
                float margin = def.IsBoss ? (melee ? (caution > 1.5f ? 1.4f : 0.7f) : 2.4f)
                    : def.explodesOnContact ? 1.6f : melee ? 0.6f : 1.0f;
                float dmgW = Mathf.Max(0.6f, def.contactDamage / 10f) * (def.explodesOnContact ? 1.8f : 1f);
                if (!e.HasArrived) dmgW *= 0.3f;
                if (dist < contact + 0.15f) cost += 60f * dmgW * caution;
                else if (dist < contact + margin)
                {
                    float k = 1f - (dist - contact - 0.15f) / margin;
                    cost += 9f * k * k * dmgW * caution;
                }
                if (dist < 4f) cost += 0.12f; // 붐비는 쪽 회피(약하게)
            }
            foreach (var s in shots)
            {
                if (s.v.sqrMagnitude < 0.01f) continue;
                for (float t = 0.1f; t <= horizon + 0.01f; t += 0.15f)
                {
                    Vector2 me = from + dir * speed * Mathf.Min(t, horizon);
                    me.y = Mathf.Clamp(me.y, Balance.DepthMin, Balance.DepthMax);
                    Vector2 sp = s.p + s.v * t;
                    float d = Vector2.Distance(me, sp);
                    if (d < 0.22f + 0.4f + 0.2f) { cost += 45f * caution; break; }
                    if (d < 1.2f) cost += 3f * (1.2f - d);
                }
            }
            foreach (var z in zones)
            {
                float d = Vector2.Distance(q, z.c);
                if (d < z.r + 0.25f) cost += z.w * caution;
                else if (d < z.r + 0.9f) cost += z.w * 0.15f * (1f - (d - z.r - 0.25f) / 0.65f);
            }
            foreach (var b in bands)
            {
                Vector2 rel = q - b.mid;
                float along = Vector2.Dot(rel, b.axis);
                float perp = Mathf.Abs(rel.x * b.axis.y - rel.y * b.axis.x);
                if (Mathf.Abs(along) <= b.halfLen + 0.5f && perp < b.halfWidth + 0.55f) cost += 90f * caution;
            }
            return cost;
        }

        // ── 저장 확인과 종료 ─────────────────────────────────────────────────

        void SnapshotSave(string label)
        {
            try
            {
                var path = PermanentProgress.ActiveSavePath;
                bool exists = File.Exists(path);
                var s = PermanentProgress.Current;
                Event($"SAVE[{label}] path={path} exists={exists} bytes={(exists ? new FileInfo(path).Length : 0)} " +
                      $"totalRuns={s.totalRuns} cleared={s.clearedRuns} failed={s.failedRuns} abandoned={s.abandonedRuns} specialty={s.specialtyPoints} " +
                      $"perm={s.unlockedPermanentSkillIds.Count} cards={s.discoveredCardIds.Count} skills={s.discoveredSkillIds.Count} varieties={s.discoveredMaterialVarietyIds.Count} " +
                      $"cocktail={s.cocktailIngredientsCollected} tea={s.teaLeavesCollected} beans={s.coffeeBeansCollected} bestClear={s.bestClearSeconds:0.0} " +
                      $"activeRun={(s.activeRun == null ? "null" : s.activeRun.runId)} lastCommitted={s.lastCommittedRunId} saveFailed={PermanentProgress.LastSaveFailed}");
                if (exists) File.Copy(path, Path.Combine(outDir, $"save-{label.Replace(' ', '_')}.json"), true);
            }
            catch (Exception e)
            {
                Event($"SAVE[{label}] snapshot error {e.Message}");
            }
        }

        IEnumerator Finish()
        {
            SnapshotSave("final");
            var sb = new StringBuilder();
            sb.AppendLine($"job={job} weapon={weapon} runs={runSummaries.Count} anyClear={anyClear} errors={errors} exceptions={exceptions} real={Real:0}s");
            foreach (var s in runSummaries) sb.AppendLine(s);
            sb.AppendLine("bosses:");
            foreach (var s in bossEvents) sb.AppendLine("  " + s);
            sb.AppendLine("choices:");
            foreach (var s in choices) sb.AppendLine("  " + s);
            File.WriteAllText(Path.Combine(outDir, "summary.txt"), sb.ToString(), new UTF8Encoding(false));
            Event($"done anyClear={anyClear} errors={errors} exceptions={exceptions}");
            telemetry.Flush();
            yield return Human(0.5f);
            Application.Quit();
        }
    }
}
#endif
