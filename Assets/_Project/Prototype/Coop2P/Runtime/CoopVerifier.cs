using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using T = CCT.Prototype.Coop2P.CoopTuning;

namespace CCT.Prototype.Coop2P
{
    /// <summary>
    /// 실제 빌드 자동 입력 검증(-cct-coop2p -cct-coop2p-verify &lt;폴더&gt;에서만 생성). 정상 시작 상태(재고 0·적 정상 스폰·무적 없음)에서
    /// 가상 키보드 장치에 두 사람의 키(WASD/E/Q, 방향키/오른쪽 Ctrl/오른쪽 Shift, Esc, R)를 실제 InputSystem 이벤트로 넣는다.
    /// 재고 지급·체력 조작 같은 fixture는 쓰지 않는다. 사람 2인 직접 플레이를 대신하지 않는다.
    /// </summary>
    public sealed class CoopVerifier : MonoBehaviour
    {
        CoopGame game;
        string dir;
        Keyboard kb;
        readonly HashSet<Key> tap = new HashSet<Key>();
        readonly Key[][] moveKeys = new Key[2][];
        readonly Vector2?[] goal = new Vector2?[2];
        readonly Vector2?[] fightAnchor = new Vector2?[2];
        readonly HashSet<Key> holdExtra = new HashSet<Key>();
        readonly List<string> checks = new List<string>();
        readonly StringBuilder log = new StringBuilder();
        int bothMovingFrames;
        float startReal;
        int pass, fail;

        CoopSim Sim => game.Sim;

        public void Init(CoopGame g, string outDir)
        {
            game = g;
            dir = outDir;
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            kb = InputSystem.AddDevice<Keyboard>("CoopVerifyKeyboard");
            game.KeyboardOverride = kb;
            game.VerifyMode = true;
            moveKeys[0] = new[] { CoopInput.P1.Up, CoopInput.P1.Down, CoopInput.P1.Left, CoopInput.P1.Right };
            moveKeys[1] = new[] { CoopInput.P2.Up, CoopInput.P2.Down, CoopInput.P2.Left, CoopInput.P2.Right };
            startReal = Time.realtimeSinceStartup;
            survive = CoopBoot.ArgValue("-cct-coop2p-verify-mode") == "survive";
            StartCoroutine(survive ? RunSurvive() : Run());
        }

        // ───────── 키 주입 ─────────

        bool survive, botActive;
        float maxEnemyY = float.MinValue, kitchenDamage;
        int kitchenHits, maxEnemies, visFrames, bothVisFrames;
        float visMaxSpan, notVisMaxSpan;
        readonly bool[] prevBrew = new bool[2];
        readonly float[] prevHp = { T.PlayerMaxHp, T.PlayerMaxHp };
        readonly int[] prevHits = new int[2];
        readonly CoopBotPolicy bot = new CoopBotPolicy();

        void LateUpdate()
        {
            var keys = new List<Key>(tap);
            keys.AddRange(holdExtra);
            if (botActive && !Sim.Paused && Sim.Outcome == CoopOutcome.Running)
            {
                // 정상 규칙 자동 플레이: 정책 출력 → 실제 키(8방향 이동 + 상호작용/마시기 탭)
                bot.Decide(Sim, Time.deltaTime, out var b1, out var b2);
                AddBotKeys(keys, b1, CoopInput.P1);
                AddBotKeys(keys, b2, CoopInput.P2);
            }
            else for (int i = 0; i < 2; i++) keys.AddRange(SteerKeys(i));
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys.ToArray()));
            tap.Clear();
            var p = Sim.Players;
            if (p[0].Moving && p[1].Moving) bothMovingFrames++;
            // 전 구간 감시: 적 최대 높이(주방 침범), 주방(제조 역할)에서 받은 피해, 주방에서 자동 공격
            foreach (var e in Sim.Enemies) maxEnemyY = Mathf.Max(maxEnemyY, e.Pos.y);
            for (int i = 0; i < 2; i++)
            {
                bool brewNow = p[i].Role == CoopRole.Brew;
                if (brewNow && prevBrew[i] && p[i].Hp < prevHp[i] - 0.001f && !p[i].Downed) kitchenDamage += prevHp[i] - p[i].Hp;
                if (brewNow && prevBrew[i] && Sim.Hits[i] > prevHits[i]) kitchenHits++;
                prevBrew[i] = brewNow; prevHp[i] = p[i].Hp; prevHits[i] = Sim.Hits[i];
            }
            maxEnemies = Mathf.Max(maxEnemies, Sim.Enemies.Count);
            // 공유 카메라 실측: 두 사람 몸(스프라이트 bounds)이 HUD 띠를 뺀 사용 영역 안에 함께 보이는 프레임 비율
            if (Sim.Outcome == CoopOutcome.Running && !Sim.Paused)
            {
                visFrames++;
                bool v0 = game.BodyFullyVisible(0, out _), v1 = game.BodyFullyVisible(1, out _);
                if (v0 && v1) bothVisFrames++;
                else { float span = Mathf.Abs(Sim.Players[0].Pos.y - Sim.Players[1].Pos.y); notVisMaxSpan = Mathf.Max(notVisMaxSpan, span); }
                if (v0 && v1) visMaxSpan = Mathf.Max(visMaxSpan, Mathf.Abs(Sim.Players[0].Pos.y - Sim.Players[1].Pos.y));
            }
        }

        List<Key> SteerKeys(int i)
        {
            var result = new List<Key>();
            if (Sim.Paused || Sim.Outcome != CoopOutcome.Running) return result;
            var p = Sim.Players[i];
            if (p.Downed) return result;
            Vector2? target = goal[i];
            if (target == null && fightAnchor[i] != null)
            {
                // 전투 담당 봇: 가장 가까운 손님이 너무 붙으면 물러서고, 아니면 기준점 근처를 지킨다(자동 공격은 게임 규칙).
                CoopEnemy near = null; float best = 1.3f;
                foreach (var e in Sim.Enemies) { float d = Vector2.Distance(e.Pos, p.Pos); if (d < best) { best = d; near = e; } }
                if (near != null) target = p.Pos + (p.Pos - near.Pos).normalized * 1.5f + (fightAnchor[i].Value - p.Pos) * 0.2f;
                else if (Vector2.Distance(p.Pos, fightAnchor[i].Value) > 0.6f) target = fightAnchor[i];
            }
            if (target == null) return result;
            var to = Route(p.Pos, target.Value);
            var d2 = to - p.Pos;
            if (goal[i] != null && Vector2.Distance(p.Pos, goal[i].Value) < 0.18f) return result;
            var k = moveKeys[i];
            if (d2.y > 0.12f) result.Add(k[0]);
            if (d2.y < -0.12f) result.Add(k[1]);
            if (d2.x < -0.12f) result.Add(k[2]);
            if (d2.x > 0.12f) result.Add(k[3]);
            return result;
        }

        static Vector2 Route(Vector2 from, Vector2 to)
        {
            bool fk = from.y > T.BoundaryY, tk = to.y > T.BoundaryY;
            if (fk == tk) return to;
            // 문 틈 가운데 접근점(자기 쪽)까지 간 뒤 반대쪽 접근점으로 통과
            const float kitchenSide = T.CounterMaxY + 0.45f, hallSide = T.CounterMinY - 0.45f;
            if (Mathf.Abs(from.x) > 0.3f) return new Vector2(0f, fk ? kitchenSide : hallSide);
            return new Vector2(0f, tk ? kitchenSide : hallSide);
        }

        void Tap(Key k) => tap.Add(k);

        void AddBotKeys(List<Key> keys, PlayerInput b, CoopKeyMap m)
        {
            if (b.Move.y > 0.38f) keys.Add(m.Up);
            if (b.Move.y < -0.38f) keys.Add(m.Down);
            if (b.Move.x < -0.38f) keys.Add(m.Left);
            if (b.Move.x > 0.38f) keys.Add(m.Right);
            if (b.Interact) keys.Add(m.Interact[0]);
            if (b.Drink) keys.Add(m.Drink[0]);
        }

        /// <summary>정상 시간 5분 생존 시도(테스트 무적·스폰 정지·상태 주입 없음). 성공/실패 결과 화면과 R 재시작을 기록한다.</summary>
        IEnumerator RunSurvive()
        {
            Log($"[survive] screen {Screen.width}x{Screen.height} args={string.Join(" ", System.Environment.GetCommandLineArgs())}");
            yield return Real(1.0f);
            Check("normal_start", Sim.Stock[0] + Sim.Stock[1] + Sim.Stock[2] == 0 && Sim.SpawningEnabled && Sim.Players[0].InvulnTime == 0f && Sim.Players[1].InvulnTime == 0f && Time.timeScale == 1f,
                $"stock={Stocks()} spawning={Sim.SpawningEnabled} timeScale={Time.timeScale} (fixture 없음)");
            float real0 = Time.realtimeSinceStartup, sim0 = Sim.Elapsed;
            botActive = true;
            int nextShot = 30, lastMinute = -1, downs = 0;
            float minHp = 100f;
            var shotsAt = new[] { 30, 120, 200, 245, 290 };
            int shotIdx = 0;
            while (Sim.Outcome == CoopOutcome.Running && Time.realtimeSinceStartup - real0 < 360f)
            {
                foreach (var p in Sim.Players) { minHp = Mathf.Min(minHp, p.Hp); }
                int minute = (int)(Sim.Elapsed / 60f);
                if (minute != lastMinute)
                {
                    lastMinute = minute;
                    Log($"[survive] t={Sim.Elapsed:0.0}s stage={Sim.Stage + 1} enemies={Sim.Enemies.Count} kills={Sim.Kills} brewed={Sim.CupsBrewed} drunk={Sim.CupsDrunk} swaps={Sim.RoleSwaps} hp={Sim.Players[0].Hp:0}/{Sim.Players[1].Hp:0} stock={Stocks()}");
                }
                if (shotIdx < shotsAt.Length && Sim.Elapsed >= shotsAt[shotIdx]) { yield return Shot($"S{shotIdx + 1:00}_t{shotsAt[shotIdx]:000}"); shotIdx++; }
                if (Sim.Players[0].Downed || Sim.Players[1].Downed) { if (nextShot >= 0) { downs++; nextShot = -1; } } else nextShot = 0;
                yield return null;
            }
            botActive = false;
            float realSpan = Time.realtimeSinceStartup - real0, simSpan = Sim.Elapsed - sim0;
            Log($"[survive] end outcome={Sim.Outcome} t={Sim.Elapsed:0.0}s real={realSpan:0.0}s kills={Sim.Kills} brewed={Sim.CupsBrewed} drunk={Sim.CupsDrunk} swaps={Sim.RoleSwaps} minHp={minHp:0} downEpisodes={downs} trips={bot.BrewTrips}");
            Check("normal_time_progress", Time.timeScale == 1f && simSpan <= realSpan + 0.5f && simSpan >= realSpan * 0.9f,
                $"sim {simSpan:0.0}s vs real {realSpan:0.0}s (일시정지·가속 없음)");
            Check("survive_5min_success", Sim.Outcome == CoopOutcome.Success && Sim.Elapsed >= T.RunSeconds,
                $"outcome={Sim.Outcome} t={Sim.Elapsed:0.0}s");
            yield return Real(0.8f);
            yield return Shot(Sim.Outcome == CoopOutcome.Success ? "S90_success" : "S90_result");
            Tap(Key.R);
            yield return Frames(3);
            bool reset = Sim.Outcome == CoopOutcome.Running && Sim.Elapsed < 0.2f && Sim.Stock[0] + Sim.Stock[1] + Sim.Stock[2] == 0 &&
                         Sim.Enemies.Count == 0 && Sim.LiveCupCount() == 0 && Sim.Kills == 0;
            Check("restart_after_result", reset, $"outcome={Sim.Outcome} t={Sim.Elapsed:0.00} stock={Stocks()} enemies={Sim.Enemies.Count}");
            yield return Real(1.0f);
            yield return Shot("S91_restart");
            Finish();
        }

        // ───────── 시나리오 ─────────

        IEnumerator Run()
        {
            Log($"screen {Screen.width}x{Screen.height} args={string.Join(" ", System.Environment.GetCommandLineArgs())}");
            yield return Real(1.5f);
            Check("normal_start", Sim.Stock[0] == 0 && Sim.Stock[1] == 0 && Sim.Stock[2] == 0 && Sim.Players[0].Hp == T.PlayerMaxHp && Sim.Players[1].Hp == T.PlayerMaxHp && Sim.SpawningEnabled,
                $"stock={Stocks()} hp={Sim.Players[0].Hp}/{Sim.Players[1].Hp} spawning={Sim.SpawningEnabled} (fixture 없음)");
            yield return Shot("01_start");

            // 1) 동시 이동: P1은 문을 지나 홀로, P2는 주방 러시 샷 작업대로
            var p1Start = Sim.Players[0].Pos; var p2Start = Sim.Players[1].Pos;
            goal[0] = new Vector2(0f, -2.2f);
            goal[1] = T.StationStand(CoopDrink.RushShot);
            yield return Until(() => Sim.Players[0].Role == CoopRole.Fight, 8f);
            Check("simultaneous_move", bothMovingFrames > 10 && Vector2.Distance(p1Start, Sim.Players[0].Pos) > 1f && Vector2.Distance(p2Start, Sim.Players[1].Pos) > 1f,
                $"bothMovingFrames={bothMovingFrames} p1 {p1Start}->{Sim.Players[0].Pos} p2 {p2Start}->{Sim.Players[1].Pos}");
            Check("p1_role_fight_on_cross", Sim.Players[0].Role == CoopRole.Fight && Sim.Players[1].Role == CoopRole.Brew, $"roles {Sim.Players[0].Role}/{Sim.Players[1].Role} swaps={Sim.RoleSwaps}");
            goal[0] = null; fightAnchor[0] = new Vector2(0f, -2.2f);

            // 2) 정상 처치로 원두 2 수급(재고 지급 없음)
            float baseHitsT0 = Sim.Elapsed; int baseHits0 = Sim.Hits[0];
            yield return Until(() => Sim.Stock[(int)Ingredient.Bean] >= 2, 120f);
            float baseRate = (Sim.Hits[0] - baseHits0) / Mathf.Max(0.1f, Sim.Elapsed - baseHitsT0);
            Check("kills_supply_bean", Sim.Stock[(int)Ingredient.Bean] >= 2, $"t={Sim.Elapsed:0.0}s kills={Sim.Kills} stock={Stocks()} p1 base hit rate={baseRate:0.00}/s");
            yield return Shot("02_supply");

            // 3) P2 제조(오른쪽 Ctrl) → 1회 차감
            int beanBefore = Sim.Stock[(int)Ingredient.Bean];
            goal[1] = null;
            Tap(Key.RightCtrl);
            yield return Frames(3);
            var st = Sim.Stations[(int)CoopDrink.RushShot];
            Check("p2_brew_start_deduct_once", st.Brewing && st.Owner == 1 && Sim.Stock[(int)Ingredient.Bean] == beanBefore - 2,
                $"brewing={st.Brewing} owner={st.Owner} bean {beanBefore}->{Sim.Stock[(int)Ingredient.Bean]}");
            yield return Real(1.4f);
            yield return Shot("03_brewing");
            yield return Until(() => st.Ready != null, 8f);
            Tap(Key.RightCtrl);
            yield return Frames(3);
            Check("p2_take_cup", Sim.Players[1].Held != null && Sim.Players[1].Held.Drink == CoopDrink.RushShot, $"held={Sim.Players[1].Held?.Drink}");

            // 4) P2 → 공용 카운터(주방 쪽)에 놓기
            goal[1] = T.PassStandKitchen;
            yield return Until(() => Vector2.Distance(Sim.Players[1].Pos, goal[1].Value) < 0.2f, 10f);
            Tap(Key.RightCtrl);
            yield return Frames(3);
            Check("p2_place_counter", Sim.Players[1].Held == null && Sim.Counter[0] != null, $"counter0={Sim.Counter[0]?.Drink} live={Sim.LiveCupCount()}");
            yield return Shot("04_counter");

            // 5) P1(홀 쪽)이 수령 → 음용(Q) → 러시 효과
            fightAnchor[0] = null;
            goal[0] = T.PassStandHall;
            yield return Until(() => Vector2.Distance(Sim.Players[0].Pos, goal[0].Value) < 0.2f || Sim.Players[0].Downed, 12f);
            yield return Until(() => !Sim.Players[0].Downed, 14f);
            Tap(Key.E);
            yield return Frames(3);
            Check("p1_receive_from_counter", Sim.Players[0].Held != null && Sim.Counter[0] == null && Sim.LiveCupCount() == 1,
                $"p1 held={Sim.Players[0].Held?.Drink} live={Sim.LiveCupCount()} p1 pos={Sim.Players[0].Pos}");
            Tap(Key.Q);
            yield return Frames(3);
            Check("p1_drink_rush", Sim.Players[0].RushTime > 9f && Sim.Players[0].Held == null && Sim.LiveCupCount() == 0 && Sim.Players[1].RushTime == 0f,
                $"p1 rush={Sim.Players[0].RushTime:0.00} p2 rush={Sim.Players[1].RushTime:0.00} (마신 사람만 적용)");
            goal[0] = null; fightAnchor[0] = new Vector2(1.5f, -2.2f);
            int rushHits0 = Sim.Hits[0]; float rushT0 = Sim.Elapsed;
            yield return Real(1.2f);
            yield return Shot("05_rush");
            yield return Real(3f);
            float rushRate = (Sim.Hits[0] - rushHits0) / Mathf.Max(0.1f, Sim.Elapsed - rushT0);
            Log($"p1 hit rate base={baseRate:0.00}/s rush={rushRate:0.00}/s (최대 {1f / T.AttackInterval:0.00} vs {1f / T.RushInterval:0.00})");
            checks.Add(Json("rush_attack_rate_observed", rushRate > baseRate ? "PASS" : "INFO", $"base={baseRate:0.00}/s rush={rushRate:0.00}/s"));

            // 6) 역할 교대: 문으로 엇갈려 지나가기(P1 → 주방, P2 → 홀)
            fightAnchor[0] = null;
            int swaps0 = Sim.RoleSwaps;
            goal[0] = new Vector2(-4.8f, 2.2f);
            goal[1] = new Vector2(2.0f, -2.2f);
            yield return Until(() => Sim.Players[0].Role == CoopRole.Brew && Sim.Players[1].Role == CoopRole.Fight, 14f);
            Check("role_swap_both", Sim.Players[0].Role == CoopRole.Brew && Sim.Players[1].Role == CoopRole.Fight && Sim.RoleSwaps >= swaps0 + 2,
                $"roles {Sim.Players[0].Role}/{Sim.Players[1].Role} swaps {swaps0}->{Sim.RoleSwaps}");
            yield return Real(0.3f);
            yield return Shot("06_swapped");

            // 6b) 두 사람 모두 주방: 전투 시간·단계 멈춤, 제조·손님 이동은 계속, 주방 침범·피격 없음
            goal[1] = T.PassStandKitchen;
            yield return Until(() => Sim.Players[1].Role == CoopRole.Brew && Vector2.Distance(Sim.Players[1].Pos, T.PassStandKitchen) < 0.25f, 12f);
            yield return Real(0.5f);
            float c0 = Sim.CombatElapsed, e0 = Sim.Elapsed; int stage0 = Sim.Stage;
            var enemyPos0 = new List<Vector2>(); foreach (var en in Sim.Enemies) enemyPos0.Add(en.Pos);
            float hpK0 = Sim.Players[0].Hp + Sim.Players[1].Hp;
            yield return Real(2.5f);
            yield return Shot("06b_both_kitchen");
            yield return Real(3.5f);
            int moved = 0; for (int k = 0; k < Mathf.Min(enemyPos0.Count, Sim.Enemies.Count); k++) if ((Sim.Enemies[k].Pos - enemyPos0[k]).sqrMagnitude > 0.01f) moved++;
            Check("both_kitchen_combat_clock_stops", !Sim.HallActive && Mathf.Abs(Sim.CombatElapsed - c0) < 0.001f && Sim.Elapsed - e0 > 5f && Sim.Stage == stage0,
                $"combat {c0:0.00}->{Sim.CombatElapsed:0.00} total {e0:0.0}->{Sim.Elapsed:0.0} stage={Sim.Stage + 1} hallActive={Sim.HallActive}");
            Check("both_kitchen_world_continues_safely", (enemyPos0.Count == 0 || moved > 0) && Sim.Enemies.Count <= T.MaxAlive[Sim.Stage] && Sim.Players[0].Hp + Sim.Players[1].Hp >= hpK0 - 0.001f,
                $"enemies {enemyPos0.Count}->{Sim.Enemies.Count} moved={moved} cap={T.MaxAlive[Sim.Stage]} hp {hpK0:0}->{Sim.Players[0].Hp + Sim.Players[1].Hp:0}");
            goal[1] = new Vector2(2.0f, -2.2f);
            yield return Until(() => Sim.Players[1].Role == CoopRole.Fight, 10f);
            yield return Real(0.6f);
            Check("combat_clock_resumes_with_hall_player", Sim.HallActive && Sim.CombatElapsed > c0, $"combat {c0:0.00}->{Sim.CombatElapsed:0.00}");

            // 6c) 가장 먼 위아래 배치(주방 통로 맨 위 ↔ 홀 맨 아래): 공유 카메라에 둘 다 몸 전체가 보이는지 실측, 안 보이면 가장자리 안내 표시 확인
            goal[0] = new Vector2(3.0f, T.ArenaMaxY - T.PlayerRadius);
            goal[1] = new Vector2(-8.6f, T.ArenaMinY + T.PlayerRadius);
            yield return Until(() => Vector2.Distance(Sim.Players[0].Pos, goal[0].Value) < 0.25f && Vector2.Distance(Sim.Players[1].Pos, goal[1].Value) < 0.25f, 12f);
            yield return Real(1.0f); // 카메라 따라가기 정착
            bool f0 = game.BodyFullyVisible(0, out var r0), f1 = game.BodyFullyVisible(1, out var r1);
            var u = game.UsableRect;
            checks.Add(Json("far_apart_both_visible", f0 && f1 ? "PASS" : "LIMIT",
                $"span={Mathf.Abs(Sim.Players[0].Pos.y - Sim.Players[1].Pos.y):0.00}u usable=({u.yMin:0}..{u.yMax:0}px) p1 body y {r0.yMin:0}..{r0.yMax:0} p2 body y {r1.yMin:0}..{r1.yMax:0} ppu={game.PixelsPerUnit:0.0}"));
            Log($"far apart: p1 visible={f0} p2 visible={f1} usable y {u.yMin:0}..{u.yMax:0} p1 {r0.yMin:0}..{r0.yMax:0} p2 {r1.yMin:0}..{r1.yMax:0}");
            yield return Shot("06c_far_apart");
            goal[0] = new Vector2(-4.8f, 2.2f);
            goal[1] = new Vector2(2.0f, -2.2f);
            yield return Until(() => Sim.Players[1].Role == CoopRole.Fight && Vector2.Distance(Sim.Players[1].Pos, goal[1].Value) < 0.3f, 10f);
            goal[1] = null; fightAnchor[1] = new Vector2(0f, -2.2f);

            // 7) P1이 찻잎 토닉 제조 → 직접 들고 홀로 나가 싸우다 마심(회복)
            goal[0] = T.StationStand(CoopDrink.LeafTonic);
            yield return Until(() => Sim.Stock[(int)Ingredient.Leaf] >= 2 && Vector2.Distance(Sim.Players[0].Pos, goal[0].Value) < 0.2f, 120f);
            Tap(Key.E);
            yield return Frames(3);
            var tonic = Sim.Stations[(int)CoopDrink.LeafTonic];
            Check("p1_brew_tonic", tonic.Brewing && tonic.Owner == 0, $"stock={Stocks()}");
            yield return Until(() => tonic.Ready != null, 8f);
            Tap(Key.E);
            yield return Frames(3);
            goal[0] = new Vector2(-2.5f, -2f);
            yield return Until(() => Sim.Players[0].Role == CoopRole.Fight, 10f);
            Check("brewer_carries_cup_out", Sim.Players[0].Role == CoopRole.Fight && Sim.Players[0].Held != null && Sim.Players[0].Held.Drink == CoopDrink.LeafTonic,
                $"p1 role={Sim.Players[0].Role} held={Sim.Players[0].Held?.Drink}");
            // 토닉은 들고 있다가 실제로 다쳤을 때(마지막 압박 구간) 마신다.
            goal[0] = null; fightAnchor[0] = new Vector2(-2.5f, -2f);
            yield return Shot("07_carry_tonic");

            // 8) P2가 다시 주방으로 들어가 시트러스 버스트 제조 → 들고 홀로 나감(손님이 몰릴 때 마심)
            fightAnchor[1] = null;
            goal[1] = T.StationStand(CoopDrink.CitrusBurst);
            yield return Until(() => Sim.Stock[0] >= 2 && Sim.Stock[1] >= 1 && Vector2.Distance(Sim.Players[1].Pos, goal[1].Value) < 0.2f, 150f);
            Tap(Key.RightCtrl);
            yield return Frames(3);
            var burst = Sim.Stations[(int)CoopDrink.CitrusBurst];
            Check("p2_brew_burst", burst.Brewing && burst.Owner == 1, $"stock={Stocks()}");
            yield return Until(() => burst.Ready != null, 10f);
            Tap(Key.RightCtrl);
            yield return Frames(3);
            goal[1] = new Vector2(1.5f, -2.5f);
            yield return Until(() => Sim.Players[1].Role == CoopRole.Fight, 12f);
            Check("p2_carries_burst_out", Sim.Players[1].Role == CoopRole.Fight && Sim.Players[1].Held != null && Sim.Players[1].Held.Drink == CoopDrink.CitrusBurst,
                $"p2 role={Sim.Players[1].Role} held={Sim.Players[1].Held?.Drink}");
            goal[1] = null; fightAnchor[1] = new Vector2(1.5f, -2.5f);
            yield return Real(1f);

            // 9) Esc 일시정지: 두 사람·손님·제조·효과·시간 정지(이동 키를 계속 눌러도 그대로)
            goal[0] = null; fightAnchor[0] = null; goal[1] = null; fightAnchor[1] = null;
            Tap(Key.Escape);
            yield return Frames(3);
            float el = Sim.Elapsed; var a0 = Sim.Players[0].Pos; var a1 = Sim.Players[1].Pos;
            var enemySnap = Sim.Enemies.Count > 0 ? Sim.Enemies[0].Pos : Vector2.zero; float rush0 = Sim.Players[0].RushTime;
            holdExtra.Add(Key.D); holdExtra.Add(Key.LeftArrow);
            yield return Real(1.6f);
            yield return Shot("09_pause");
            holdExtra.Clear();
            bool frozen = Sim.Paused && Mathf.Approximately(el, Sim.Elapsed) && a0 == Sim.Players[0].Pos && a1 == Sim.Players[1].Pos &&
                          (Sim.Enemies.Count == 0 || Sim.Enemies[0].Pos == enemySnap) && Mathf.Approximately(rush0, Sim.Players[0].RushTime);
            Check("pause_freezes_all", frozen, $"paused={Sim.Paused} elapsed {el:0.00}->{Sim.Elapsed:0.00} timeScale={Time.timeScale}");
            Tap(Key.Escape);
            yield return Real(0.5f);
            Check("pause_resume", !Sim.Paused && Sim.Elapsed > el && Time.timeScale == 1f, $"elapsed {el:0.00}->{Sim.Elapsed:0.00}");

            // 10) 마지막 압박: 두 사람이 서로 떨어져(구조 거리 밖) 홀 구석에서 물러서지 않음.
            //     다친 P1이 토닉(실제 회복량 측정), 손님이 몰린 P2가 버스트(기절 수 측정) → 이후 둘 다 쓰러지면 실패.
            goal[0] = new Vector2(-6.4f, -3.6f);
            goal[1] = new Vector2(-3.4f, -3.6f);
            bool tonicDone = false, burstDone = false, crowdShot = false, downShot = false;
            float endWait = Time.realtimeSinceStartup + 300f;
            while (Sim.Outcome == CoopOutcome.Running && Time.realtimeSinceStartup < endWait)
            {
                var a = Sim.Players[0];
                if (!crowdShot && Sim.Enemies.Count >= 10 && !Sim.Players[0].Downed && !Sim.Players[1].Downed) { crowdShot = true; yield return Shot("10a_crowd_both_hall"); continue; }
                if (!downShot && (Sim.Players[0].Downed ^ Sim.Players[1].Downed)) { downShot = true; yield return Real(0.3f); yield return Shot("10b_downed"); continue; }
                if (!tonicDone && a.Held != null && !a.Downed && a.Hp <= 85f)
                {
                    tonicDone = true;
                    float hp0 = a.Hp, healed0 = Sim.Healed[0];
                    Tap(Key.Q);
                    yield return Frames(3);
                    bool drank = a.Held == null && a.HealPool > 0f;
                    yield return Real(2.8f);
                    float healed = Sim.Healed[0] - healed0;
                    float room = Mathf.Min(T.TonicHeal, T.PlayerMaxHp - hp0);
                    Check("p1_drink_tonic_heal", drank && healed >= room - 1f, $"hp at drink {hp0:0.0}, room {room:0.0}, actual healed {healed:0.0} (under enemy contact), hp now {a.Hp:0.0}");
                    yield return Shot("07_tonic");
                    continue;
                }
                var b2 = Sim.Players[1];
                if (!burstDone && b2.Held != null && !b2.Downed && CountNear(b2.Pos, 3.0f) >= 2)
                {
                    burstDone = true;
                    int near = CountNear(b2.Pos, T.BurstRadius);
                    int kills0 = Sim.Kills;
                    Tap(Key.RightShift);
                    yield return Frames(2);
                    int stunned = 0; foreach (var e in Sim.Enemies) if (e.Stun > 1.5f) stunned++;
                    int killed = Sim.Kills - kills0; // 1단계 손님(체력 30)은 충격파 30 피해로 즉사 — 기절 대신 처치로 집계
                    Check("p2_drink_burst_stun", b2.Held == null && stunned + killed > 0 && b2.InvulnTime > 1f,
                        $"near(r4)={near} stunned={stunned} killed_in_2frames={killed} invuln={b2.InvulnTime:0.00} stage={Sim.Stage + 1}");
                    yield return Shot("08_burst");
                    continue;
                }
                yield return null;
            }
            if (!tonicDone) Check("p1_drink_tonic_heal", false, "P1 HP<=85 조건 미도달");
            if (!burstDone) Check("p2_drink_burst_stun", false, "P2 주변 손님 2마리 조건 미도달");
            Log($"outcome={Sim.Outcome} at t={Sim.Elapsed:0.0}s kills={Sim.Kills} brewed={Sim.CupsBrewed} drunk={Sim.CupsDrunk} swaps={Sim.RoleSwaps}");
            Check("outcome_reached", Sim.Outcome != CoopOutcome.Running, $"outcome={Sim.Outcome} t={Sim.Elapsed:0.0}");
            checks.Add(Json("outcome_kind", Sim.Outcome == CoopOutcome.Failed ? "PASS" : "INFO", Sim.Outcome.ToString()));
            yield return Real(0.6f);
            yield return Shot(Sim.Outcome == CoopOutcome.Failed ? "10_fail" : "10_result");

            // 11) R 다시 시작: 실험판 상태만 초기화
            goal[0] = goal[1] = null;
            Tap(Key.R);
            yield return Frames(3);
            bool reset = Sim.Outcome == CoopOutcome.Running && Sim.Elapsed < 0.2f && Sim.Stock[0] + Sim.Stock[1] + Sim.Stock[2] == 0 &&
                         Sim.Enemies.Count == 0 && Sim.LiveCupCount() == 0 && Sim.Kills == 0 && Sim.Players[0].Hp == T.PlayerMaxHp && !Sim.Players[1].Downed;
            Check("restart_resets", reset && Sim.CombatElapsed < 0.2f, $"outcome={Sim.Outcome} t={Sim.Elapsed:0.00} combat={Sim.CombatElapsed:0.00} stock={Stocks()} enemies={Sim.Enemies.Count}");
            int cams = FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            Check("single_camera_after_restart", cams == 1, $"cameras={cams}");
            yield return Real(1.0f);
            yield return Shot("11_restart");

            Finish();
        }

        void Finish()
        {
            Check("kitchen_intrusion_zero", maxEnemyY <= T.EnemyMaxY + 0.001f, $"max enemy y={maxEnemyY:0.000} limit={T.EnemyMaxY:0.000} (주방 바닥 시작 {T.CounterMaxY})");
            Check("kitchen_damage_and_attack_zero", kitchenDamage < 0.001f && kitchenHits == 0, $"kitchenDamage={kitchenDamage:0.00} kitchenHits={kitchenHits}");
            checks.Add(Json("enemy_peak", "INFO", $"max simultaneous enemies={maxEnemies}"));
            checks.Add(Json("shared_camera_both_visible", "INFO",
                $"both bodies fully in usable area {bothVisFrames}/{visFrames} frames ({(visFrames > 0 ? 100f * bothVisFrames / visFrames : 0f):0.0}%), max vertical gap while both visible {visMaxSpan:0.00}u, max gap when not {notVisMaxSpan:0.00}u"));
            checks.Add(Json("framing", "INFO", $"screen={Screen.width}x{Screen.height} pxPerUnit={game.PixelsPerUnit:0.0} bodyPx={game.PixelsPerUnit * 1.25f:0}"));
            Log($"done real={Time.realtimeSinceStartup - startReal:0.0}s pass={pass} fail={fail} frames={game.FramesRendered}");
            var sb = new StringBuilder();
            sb.Append("{\n  \"screen\": \"").Append(Screen.width).Append('x').Append(Screen.height).Append("\",\n");
            sb.Append("  \"pass\": ").Append(pass).Append(",\n  \"fail\": ").Append(fail).Append(",\n  \"checks\": [\n    ");
            sb.Append(string.Join(",\n    ", checks)).Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "results.json"), sb.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString(), new UTF8Encoding(false));
            Application.Quit(fail == 0 ? 0 : 3);
        }

        void Update()
        {
            // 전체 안전 시간 제한(멈춤 방지)
            if (Time.realtimeSinceStartup - startReal > 900f && enabled)
            {
                enabled = false;
                Check("verifier_timeout", false, "840s 초과");
                StopAllCoroutines();
                Finish();
            }
        }

        int CountNear(Vector2 pos, float r)
        {
            int n = 0;
            foreach (var e in Sim.Enemies) if (Vector2.Distance(e.Pos, pos) <= r) n++;
            return n;
        }

        string Stocks() => $"라임{Sim.Stock[0]}/원두{Sim.Stock[1]}/찻잎{Sim.Stock[2]}";

        void Check(string id, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            checks.Add(Json(id, ok ? "PASS" : "FAIL", detail));
            Log($"{(ok ? "PASS" : "FAIL")} {id}: {detail}");
        }

        static string Json(string id, string status, string detail) =>
            $"{{\"id\": \"{id}\", \"status\": \"{status}\", \"detail\": \"{detail.Replace("\\", "/").Replace("\"", "'")}\"}}";

        void Log(string line)
        {
            var s = $"[{Time.realtimeSinceStartup - startReal,7:0.0}s real | sim {(game != null && game.Sim != null ? game.Sim.Elapsed : 0f),6:0.0}s] {line}";
            log.AppendLine(s);
            Debug.Log("[CoopVerify] " + s);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(dir, $"{name}_{Screen.width}x{Screen.height}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("shot " + path);
            yield return null;
            yield return null;
        }

        static IEnumerator Real(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Until(System.Func<bool> cond, float timeoutReal)
        {
            float end = Time.realtimeSinceStartup + timeoutReal;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            if (!cond()) Log($"timeout {timeoutReal}s waiting (stock={Stocks()} p1={Sim.Players[0].Pos} {Sim.Players[0].Role} down={Sim.Players[0].Downed} p2={Sim.Players[1].Pos} {Sim.Players[1].Role} down={Sim.Players[1].Downed})");
        }
    }
}
