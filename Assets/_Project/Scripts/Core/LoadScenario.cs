#if CCT_QA_LOAD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace CCT
{
    /// <summary>
    /// 10 §3 고정 부하 QA 시나리오(검증 빌드 심볼 CCT_QA_LOAD에서만 컴파일, -cct-loadtest &lt;폴더&gt; + -cct-data-dir 필수).
    /// 실제 타이틀→선택→바텐더 총 런을 시작한 뒤 일반/소환 적 100 → 최종 보스 + 소환 보충으로 전체 160, 적 탄 100,
    /// 플레이어 탄 80, 장판 10, 픽업 200을 매 프레임 보충해 유지한다. 1080p·효과 보통·VSync off(60fps 제한)에서
    /// 준비 30초 뒤 180초 동안 프레임 시간·개체 수·프로세스/관리 메모리를 기록하고 performance.csv와 요약을 남긴다.
    /// 강제 조건(보고 시 표기): 플레이어 최대 HP 1e7(피해 처리는 그대로 실행), 최종 보스 HP ×500(부하 유지용),
    /// 보상 창은 즉시 첫 선택지로 닫음(그 프레임은 Playing 아님으로 따로 표시), 보충 스폰. 정상 플레이·밸런스 증거가 아니다.
    /// -cct-loadtest-restarts N: 측정 뒤 포기→결과 재시작을 N회 반복하며 초기화 2.5초 뒤 managed heap을 기록한다.
    /// </summary>
    public class LoadScenario : MonoBehaviour
    {
        public const int BaseEnemies = 100;
        public const int TargetEnemies = 160;
        public const int TargetEnemyProjectiles = 100;
        public const int TargetPlayerProjectiles = 80;
        public const int TargetZones = 10;
        public const int TargetPickups = 200;
        const float BossHpScale = 500f;
        const int MaxEnemySpawnsPerFrame = 20;
        const long MemoryLimitBytes = 1536L * 1024 * 1024;

        enum Phase { Starting, Warmup, Measure, Restarts, Done }

        string outDir;
        float warmupSeconds = 30f, measureSeconds = 180f;
        int restarts;
        Phase phase = Phase.Starting;
        float phaseStart;
        int errors, exceptions, rewardsResolved;
        readonly StringBuilder log = new StringBuilder();
        float started;
        GameManager game;
        PlayerController player;
        Enemy boss;
        float moveTimer;
        int zoneKind;
        float telegraphTimer;
        bool bossLost;

        // 측정 프레임 기록(사전 할당 — 측정 중 할당을 만들지 않는다)
        const int MaxFrames = 40000;
        readonly float[] fTime = new float[MaxFrames], fDt = new float[MaxFrames];
        readonly int[] fEnemies = new int[MaxFrames], fEProj = new int[MaxFrames], fPProj = new int[MaxFrames],
            fZones = new int[MaxFrames], fPickups = new int[MaxFrames];
        readonly bool[] fPlaying = new bool[MaxFrames];
        readonly long[] fWorking = new long[MaxFrames], fPrivate = new long[MaxFrames], fMono = new long[MaxFrames];
        int frames;
        long lastWorking, lastPrivate, lastMono, peakWorking, peakPrivate;
        float memTimer;

        public void Init(string dir, string warmup, string measure, string restartCount)
        {
            Application.runInBackground = true; // 다른 창에 가려져도 측정이 멈추지 않게(검증 빌드만)
            outDir = dir;
            Directory.CreateDirectory(outDir);
            if (float.TryParse(warmup, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w >= 0f) warmupSeconds = w;
            if (float.TryParse(measure, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) && m > 0f) measureSeconds = m;
            if (int.TryParse(restartCount, out var r) && r > 0) restarts = r;
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions++;
            else if (type == LogType.Error || type == LogType.Assert) errors++;
            else return;
            Line($"[{type}] {condition}");
        }

        void Line(string text) => log.AppendLine($"{Time.realtimeSinceStartup - started,8:0.00}s {text}");

        void Fail(string text)
        {
            errors++;
            Line("FAIL " + text);
        }

        IEnumerator Run()
        {
            Line($"loadtest start, version {Application.version}, data={LaunchOptions.DataDir}, warmup={warmupSeconds}s measure={measureSeconds}s restarts={restarts}");
            TitleView title = null;
            for (float t = 0f; t < 30f && (title == null || !title.IsOpen); t += 0.25f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                title = FindFirstObjectByType<TitleView>();
            }
            if (title == null || !title.IsOpen) { Fail("title not open"); yield return Finish(); yield break; }

            // 10 §3 조건: 효과 보통, VSync off·60fps 제한, 1920×1080. 설정은 격리 저장(-cct-data-dir)에만 쓴다.
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            SettingsService.SetVSync(true);
            SettingsService.SetVSync(false);
            if (Screen.width != 1920 || Screen.height != 1080)
            {
                Line($"resolution {Screen.width}x{Screen.height} → SetResolution 1920x1080 FullScreenWindow");
                Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
                yield return new WaitForSecondsRealtime(2f);
            }
            Line(EnvLine());

            title.RequestStart();
            yield return new WaitForSecondsRealtime(1.2f);
            if (!StartGunRun()) { Fail("character select not found"); yield return Finish(); yield break; }
            yield return new WaitForSecondsRealtime(0.8f);
            var tutorial = FindFirstObjectByType<FirstRunTutorialView>();
            if (tutorial != null && tutorial.IsOpen) tutorial.Dismiss();
            yield return new WaitForSecondsRealtime(0.5f);

            game = GameManager.Instance;
            if (game == null) { Fail("run did not start"); yield return Finish(); yield break; }
            player = game.Player;
            // QA only: maximum real card effects, actual original weapon hits and uncapped burst spawning.
            foreach (var id in new[] { "rifled_barrel", "piercing_arrowhead", "champagne_spray", "tea_leaf_scatter", "molotov", "blazer", "nettle_tea", "nightshade_blend" })
            {
                var card = CardDatabase.LoadAll().First(c => c.id == id);
                for (int n = 0; n < card.maxStacks; n++) game.Cards.Take(card, game.Stats);
                player.FinalForms.OnCardTaken(card, game.Cards.GetStacks(card));
            }
            game.Stats.Apply(new CardEffect(CardEffectType.ExtraThrust, 1)); // max coffee node effect, eight original shots
            // 2026-10-12: 히든 24종 전부 강제(효과 훅 전부 활성) — 최대 해금(N24 저장)·160적과 함께 부하 측정. 강제 경로로 보고.
            foreach (var h in HiddenSkillDatabase.All) player.Hidden.ForceForTests(h.skill);
            Line($"hidden forced: {player.Hidden.Count}/24, spawn N={game.Challenge.PermanentUnlockTotal} cap={game.Challenge.RegularAliveCap}");
            Line($"maximum final forms: pierce={player.FinalForms.Has(AttackFinalForm.Pierce)} spread={player.FinalForms.Has(AttackFinalForm.Spread)} flame={player.FinalForms.Has(AttackFinalForm.Flame)} poison={player.FinalForms.Has(AttackFinalForm.Poison)}; real weapon, no burst suppression");
            // [강제] 피해 처리·피격 효과는 그대로 두고 사망만 막는다(측정 구간이 사망으로 끊기지 않게).
            game.Stats.baseMaxHp = 1e7f;
            player.Heal(1f);
            boss = Enemy.SpawnAt(player.Position + new Vector2(6f, 0.4f), EnemyDef.DifficultCustomer, BossHpScale, player, game.transform);
            EnemyRoster.AttachBehavior(boss, game, player);
            game.ActiveBoss = boss; // BossDirector 일정 보스와 겹치지 않게(동시 보스 1)
            Line($"run started job={game.Job} weapon={game.Weapon} bossHp={boss.MaxHp:0}");

            phase = Phase.Warmup;
            phaseStart = Time.realtimeSinceStartup;
            while (phase == Phase.Warmup || phase == Phase.Measure) yield return null;

            WriteResults();
            if (restarts > 0) yield return RestartLoop();
            yield return Finish();
        }

        bool StartGunRun()
        {
            var select = FindFirstObjectByType<CharacterSelectView>();
            if (select == null) return false;
            select.BartenderButton.onClick.Invoke();
            select.WeaponButtonB.onClick.Invoke(); // 바텐더 B = 총
            select.StartButton.onClick.Invoke();
            return true;
        }

        void Update()
        {
            if (phase != Phase.Warmup && phase != Phase.Measure) return;
            if (game == null || game.State == GameState.GameOver)
            {
                Fail($"run ended during {phase} (reason={game?.EndReason})");
                phase = Phase.Done;
                return;
            }
            if (!bossLost && (boss == null || boss.IsDead)) { bossLost = true; Fail("final boss died — load scenario invalid"); }

            ResolveRewards();
            Move();
            if (game.State == GameState.Playing && !game.IsPaused) TopUp(Time.realtimeSinceStartup - phaseStart);
            SampleMemory();

            float elapsed = Time.realtimeSinceStartup - phaseStart;
            if (phase == Phase.Warmup)
            {
                if (elapsed >= warmupSeconds)
                {
                    phase = Phase.Measure;
                    phaseStart = Time.realtimeSinceStartup;
                    Line($"measure start: {Counts()}");
                }
                return;
            }

            // 측정: 이번 프레임의 실제 경과(unscaled) — 첫 프레임은 준비 구간의 마지막 프레임 시간이라 버린다.
            if (elapsed > 0f && frames < MaxFrames)
            {
                int i = frames++;
                fTime[i] = elapsed;
                fDt[i] = Time.unscaledDeltaTime;
                fEnemies[i] = Enemy.Active.Count;
                fEProj[i] = EnemyProjectile.ActiveCount;
                fPProj[i] = PlayerProjectile.ActiveCount;
                fZones[i] = BurstZone.ActiveCount + SlowZone.ActiveCount;
                fPickups[i] = IngredientPickup.ActiveCount;
                fPlaying[i] = game.State == GameState.Playing && !game.IsPaused;
                fWorking[i] = lastWorking;
                fPrivate[i] = lastPrivate;
                fMono[i] = lastMono;
            }
            if (elapsed >= measureSeconds)
            {
                Line($"measure end: {Counts()}");
                phase = Phase.Done;
            }
        }

        void SampleMemory()
        {
            memTimer -= Time.unscaledDeltaTime;
            if (memTimer > 0f && lastWorking > 0) return;
            memTimer = 1f;
            ReadProcessMemory(out lastWorking, out lastPrivate);
            lastMono = GC.GetTotalMemory(false);
            peakWorking = Math.Max(peakWorking, lastWorking);
            peakPrivate = Math.Max(peakPrivate, lastPrivate);
        }

        // Mono 플레이어의 Process.WorkingSet64/PrivateMemorySize64는 Windows에서 0을 돌려준다(스모크 실측) — Win32 API로 직접 읽는다.
        [StructLayout(LayoutKind.Sequential)]
        struct ProcessMemoryCounters
        {
            public uint cb, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
        }

        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
        static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);

        static void ReadProcessMemory(out long workingSet, out long privateBytes)
        {
            workingSet = privateBytes = 0;
            try
            {
                if (GetProcessMemoryInfo(GetCurrentProcess(), out var c, (uint)Marshal.SizeOf<ProcessMemoryCounters>()))
                {
                    workingSet = (long)c.WorkingSetSize.ToUInt64();
                    privateBytes = (long)c.PrivateUsage.ToUInt64();
                }
            }
            catch (Exception) { /* 측정 불가는 0으로 남고 요약에서 FAIL로 드러난다 */ }
        }

        string Counts() => $"enemies={Enemy.Active.Count} enemyProj={EnemyProjectile.ActiveCount} playerProj={PlayerProjectile.ActiveCount} " +
                           $"zones={BurstZone.ActiveCount + SlowZone.ActiveCount} pickups={IngredientPickup.ActiveCount} kills={game?.Kills} runTime={game?.RunTime:0.0}";

        /// <summary>보상 창이 열리면 즉시 첫 선택지로 닫는다(timeScale 0 프레임을 오래 두지 않음). 그 프레임은 fPlaying=false로 남는다.</summary>
        void ResolveRewards()
        {
            if (game.CardView.IsOpen) { game.CardView.Choose(0); rewardsResolved++; }
            else if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen) { game.SkillView.Continue(); rewardsResolved++; }
            else if (game.HybridView.IsOpen) { game.HybridView.Unlock(); rewardsResolved++; }
        }

        /// <summary>입력 대행으로 좌우 왕복 이동(카메라 스크롤·정렬 부하 포함). 픽업 반경 밖 보충 위치와 겹치지 않게 폭은 작게.</summary>
        void Move()
        {
            moveTimer += Time.deltaTime;
            float phaseT = moveTimer % 4f;
            player.AutoInputOverride = phaseT < 2f ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
        }

        void TopUp(float elapsed)
        {
            var parent = game.transform;
            Vector2 p = player.Position;
            // 적: 준비 첫 10초는 일반/소환 100, 그 뒤는 최종 소환 보충으로 전체 160(보스 포함).
            int enemyTarget = phase == Phase.Warmup && elapsed < 10f ? BaseEnemies : TargetEnemies;
            float hpScale = Balance.EnemyHpScale(game.RunTime);
            for (int n = 0; n < MaxEnemySpawnsPerFrame && Enemy.Active.Count < enemyTarget; n++)
            {
                var def = EnemyRoster.PickCommon(600f);
                var pos = new Vector2(p.x + Side() * UnityEngine.Random.Range(3f, 11f),
                    UnityEngine.Random.Range(Balance.DepthMin, Balance.DepthMax));
                var e = Enemy.SpawnAt(pos, def, hpScale, player, parent);
                EnemyRoster.AttachBehavior(e, game, player);
            }

            if (Enemy.Active.Count > 0)
                while (EnemyProjectile.ActiveCount < TargetEnemyProjectiles)
                {
                    var src = Enemy.Active[UnityEngine.Random.Range(0, Enemy.Active.Count)];
                    Vector2 from = src.transform.position;
                    var dir = (p - from).sqrMagnitude > 0.01f ? p - from : Vector2.left;
                    dir = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-35f, 35f)) * dir;
                    EnemyProjectile.Spawn(from, dir, 4.5f, 5f, player, parent, EnemyProjectile.ArtPrefix + "glass_shard");
                }

            while (PlayerProjectile.ActiveCount < TargetPlayerProjectiles)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.35f);
                PlayerProjectile.Spawn(p + dir.normalized * 0.4f, dir, 14f, 10f, 0, parent, "bullet");
            }

            while (BurstZone.ActiveCount + SlowZone.ActiveCount < TargetZones)
            {
                var pos = new Vector2(p.x + UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(Balance.DepthMin, Balance.DepthMax));
                // 43 v2 장판 3종(올리브·커피·설탕)을 섞어 실제 그림 부하로
                int kind = zoneKind++ % 3;
                if (kind == 2) SlowZone.Spawn(pos, 1.2f, 0.6f, 8f, player, parent);
                else BurstZone.Spawn(pos, 1.2f, 5f, 8f, player, parent, kind == 0 ? BurstZone.OliveFireArt : BurstZone.CoffeePoolArt);
            }
            telegraphTimer -= Time.deltaTime;
            if (telegraphTimer <= 0f) // 43 v2 예고 링(0.8초)을 0.25초마다 — 동시 약 3개 유지
            {
                telegraphTimer = 0.25f;
                var pos = new Vector2(p.x + UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(Balance.DepthMin, Balance.DepthMax));
                ZoneTelegraphV2.Spawn(pos, 1.3f, 0.8f, new Color(1f, .62f, .3f), parent);
            }

            while (IngredientPickup.ActiveCount < TargetPickups)
            {
                var pos = new Vector2(p.x + Side() * UnityEngine.Random.Range(5f, 10f),
                    UnityEngine.Random.Range(Balance.DepthMin, Balance.DepthMax));
                var type = UnityEngine.Random.value < 0.5f ? IngredientType.Cocktail : IngredientType.Tea;
                IngredientPickup.Spawn(game, player, pos, type, 1, parent);
            }
        }

        static float Side() => UnityEngine.Random.value < 0.5f ? -1f : 1f;

        static string EnvLine() =>
            $"env os=\"{SystemInfo.operatingSystem}\" cpu=\"{SystemInfo.processorType}\" cores={SystemInfo.processorCount} " +
            $"ramMB={SystemInfo.systemMemorySize} gpu=\"{SystemInfo.graphicsDeviceName}\" api={SystemInfo.graphicsDeviceType} vramMB={SystemInfo.graphicsMemorySize} " +
            $"screen={Screen.width}x{Screen.height} mode={Screen.fullScreenMode} monitor={Screen.currentResolution.width}x{Screen.currentResolution.height}@{Screen.currentResolution.refreshRateRatio.value:0.##} " +
            $"vSyncCount={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
            $"effects={SettingsService.Current.effectsLevel} debugBuild={Debug.isDebugBuild}";

        void WriteResults()
        {
            var csv = new StringBuilder("t_s,frame_ms,playing,enemies,enemy_proj,player_proj,zones,pickups,working_set_mb,private_mb,managed_mb\n");
            for (int i = 0; i < frames; i++)
                csv.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.0000},{1:0.000},{2},{3},{4},{5},{6},{7},{8:0.0},{9:0.0},{10:0.0}\n",
                    fTime[i], fDt[i] * 1000f, fPlaying[i] ? 1 : 0, fEnemies[i], fEProj[i], fPProj[i], fZones[i], fPickups[i],
                    fWorking[i] / 1048576.0, fPrivate[i] / 1048576.0, fMono[i] / 1048576.0));
            File.WriteAllText(Path.Combine(outDir, "performance.csv"), csv.ToString());

            var s = new StringBuilder();
            s.AppendLine("# G35 고정 부하 측정 요약 (LoadScenario, CCT_QA_LOAD 검증 빌드)");
            s.AppendLine(EnvLine());
            if (frames == 0) { s.AppendLine("frames=0 — 측정 없음"); File.WriteAllText(Path.Combine(outDir, "load-summary.txt"), s.ToString()); return; }
            Summarize(s, "all", Enumerable.Range(0, frames).ToArray());
            var playing = Enumerable.Range(0, frames).Where(i => fPlaying[i]).ToArray();
            Summarize(s, "playing-only", playing);

            float measured = fTime[frames - 1];
            int stalls = Enumerable.Range(0, frames).Count(i => fDt[i] >= 0.1f);
            double avgFps = frames / Math.Max(0.001, measured);
            double p95 = Percentile(Enumerable.Range(0, frames).Select(i => (double)fDt[i] * 1000.0).ToArray(), 0.95);
            int eMin = Enumerable.Range(0, frames).Min(i => fEnemies[i]);
            double[] Window(Func<int, double> f, float from, float to) => Enumerable.Range(0, frames).Where(i => fTime[i] >= from && fTime[i] < to).Select(f).ToArray();
            double wsFirst = Window(i => fWorking[i], 0f, 30f).DefaultIfEmpty(0).Average() / 1048576.0;
            double wsLast = Window(i => fWorking[i], measured - 30f, measured + 1f).DefaultIfEmpty(0).Average() / 1048576.0;
            double monoFirst = Window(i => fMono[i], 0f, 30f).DefaultIfEmpty(0).Average() / 1048576.0;
            double monoLast = Window(i => fMono[i], measured - 30f, measured + 1f).DefaultIfEmpty(0).Average() / 1048576.0;
            s.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "load mins: enemies={0} enemyProj={1} playerProj={2} zones={3} pickups={4} | means: enemies={5:0.0} enemyProj={6:0.0} playerProj={7:0.0} zones={8:0.0} pickups={9:0.0}",
                eMin, Enumerable.Range(0, frames).Min(i => fEProj[i]), Enumerable.Range(0, frames).Min(i => fPProj[i]),
                Enumerable.Range(0, frames).Min(i => fZones[i]), Enumerable.Range(0, frames).Min(i => fPickups[i]),
                Enumerable.Range(0, frames).Average(i => fEnemies[i]), Enumerable.Range(0, frames).Average(i => fEProj[i]),
                Enumerable.Range(0, frames).Average(i => fPProj[i]), Enumerable.Range(0, frames).Average(i => fZones[i]),
                Enumerable.Range(0, frames).Average(i => fPickups[i])));
            s.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "memory: peakWorkingSetMB={0:0.0} peakPrivateMB={1:0.0} workingSet first30s={2:0.0} last30s={3:0.0} | managed first30s={4:0.0} last30s={5:0.0}",
                peakWorking / 1048576.0, peakPrivate / 1048576.0, wsFirst, wsLast, monoFirst, monoLast));
            s.AppendLine($"rewardsResolved={rewardsResolved} kills={game?.Kills} errors={errors} exceptions={exceptions}");
            bool fpsOk = avgFps >= 58.0, p95Ok = p95 <= 25.0, stallOk = stalls <= 1, memOk = peakWorking > 0 && peakWorking <= MemoryLimitBytes && peakPrivate <= MemoryLimitBytes;
            bool errOk = errors == 0 && exceptions == 0;
            s.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "criteria(all frames): avgFps={0:0.00} {1} | p95={2:0.00}ms {3} | stalls>=100ms={4} {5} | mem≤1.5GiB {6} | errors0 {7}",
                avgFps, fpsOk ? "PASS" : "FAIL", p95, p95Ok ? "PASS" : "FAIL", stalls, stallOk ? "PASS" : "FAIL",
                memOk ? "PASS" : "FAIL", errOk ? "PASS" : "FAIL"));
            s.AppendLine($"RESULT {(fpsOk && p95Ok && stallOk && memOk && errOk ? "PASS" : "FAIL")}");
            File.WriteAllText(Path.Combine(outDir, "load-summary.txt"), s.ToString());
            Line("results written: frames=" + frames);
        }

        void Summarize(StringBuilder s, string label, int[] idx)
        {
            if (idx.Length == 0) { s.AppendLine($"[{label}] frames=0"); return; }
            var ms = idx.Select(i => (double)fDt[i] * 1000.0).ToArray();
            double total = ms.Sum() / 1000.0;
            s.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[{0}] frames={1} seconds={2:0.00} avgFps={3:0.00} meanMs={4:0.000} p50={5:0.000} p95={6:0.000} p99={7:0.000} maxMs={8:0.000} over25ms={9} over50ms={10} over100ms={11}",
                label, idx.Length, total, idx.Length / Math.Max(0.001, total), ms.Average(), Percentile(ms, 0.5), Percentile(ms, 0.95),
                Percentile(ms, 0.99), ms.Max(), ms.Count(v => v > 25.0), ms.Count(v => v > 50.0), ms.Count(v => v >= 100.0)));
        }

        static double Percentile(double[] values, double q)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            int k = Mathf.Clamp(Mathf.CeilToInt((float)(q * sorted.Length)) - 1, 0, sorted.Length - 1);
            return sorted[k];
        }

        /// <summary>10 §3 재시작 10회: 포기→결과 "다시 하기"(Restart) 경로, 새 런 초기화 2.5초 뒤 GC 후 managed heap 기록.</summary>
        IEnumerator RestartLoop()
        {
            phase = Phase.Restarts;
            var heaps = new long[restarts];
            var sb = new StringBuilder("restart,managed_after_gc_mb,mono_used_mb,working_set_mb,root_objects,enemies\n");
            for (int i = 0; i < restarts; i++)
            {
                var g = GameManager.Instance;
                if (g == null) { Fail($"restart {i}: no game"); break; }
                if (g.Player != null) g.Player.AutoInputOverride = null;
                g.OpenPause();
                g.GiveUp();
                yield return new WaitForSecondsRealtime(0.8f);
                g.Restart();
                yield return new WaitForSecondsRealtime(1.2f);
                if (!StartGunRun()) { Fail($"restart {i}: select missing"); break; }
                yield return new WaitForSecondsRealtime(2.5f);
                if (GameManager.Instance == null || GameManager.Instance == g) { Fail($"restart {i}: new run not started"); break; }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                yield return null;
                heaps[i] = GC.GetTotalMemory(true);
                ReadProcessMemory(out var ws, out _);
                int roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount;
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.000},{2:0.000},{3:0.0},{4},{5}\n", i + 1, heaps[i] / 1048576.0,
                    Profiler.GetMonoUsedSizeLong() / 1048576.0, ws / 1048576.0, roots, Enemy.Active.Count));
            }
            File.WriteAllText(Path.Combine(outDir, "restart-heap.csv"), sb.ToString());
            if (restarts >= 6 && heaps[restarts - 1] > 0)
            {
                double first = heaps.Take(3).Average(), last = heaps.Skip(restarts - 3).Average();
                double ratio = last / Math.Max(1.0, first);
                Line(string.Format(CultureInfo.InvariantCulture, "restart heap first3={0:0.000}MB last3={1:0.000}MB ratio={2:0.000} {3}",
                    first / 1048576.0, last / 1048576.0, ratio, ratio < 1.10 ? "PASS" : "FAIL(≥10%)"));
            }
        }

        IEnumerator Finish()
        {
            phase = Phase.Done;
            Line($"loadtest done errors={errors} exceptions={exceptions}");
            File.WriteAllText(Path.Combine(outDir, "load-log.txt"), log.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(errors == 0 && exceptions == 0 ? 0 : 1);
        }
    }
}
#endif
