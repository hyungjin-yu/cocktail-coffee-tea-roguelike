using System.Collections.Generic;
using UnityEngine;
using T = CCT.Prototype.Coop2P.CoopTuning;

namespace CCT.Prototype.Coop2P
{
    public sealed class CoopPlayer
    {
        public int Index;
        public Vector2 Pos;
        public float Hp = T.PlayerMaxHp;
        public bool Downed;
        public float DownedTime;
        public float ReviveProgress;
        public CoopCup Held;
        public float AttackTimer;
        public float RushTime;
        public float InvulnTime;
        public float HealPool;
        public Vector2 Facing = Vector2.right;
        public bool Moving;
        public float HurtFlash;
        public CoopRole LastRole;
        /// <summary>현재 역할. 주방 바닥(카운터 뒤)에 들어서면 제조, 홀 바닥(카운터 앞)에 들어서면 전투, 문 통로 안에서는 유지.</summary>
        public CoopRole RoleState;

        public CoopRole Role => RoleState;

        /// <summary>위치로 역할을 맞춘다(문 통로는 가운데 기준). 시작·재시작·시험 배치용.</summary>
        public void SnapRole()
        {
            RoleState = Pos.y > T.BoundaryY ? CoopRole.Brew : CoopRole.Fight;
            LastRole = RoleState;
        }

        public void UpdateRole()
        {
            if (Pos.y >= T.RoleBrewY) RoleState = CoopRole.Brew;
            else if (Pos.y <= T.RoleFightY) RoleState = CoopRole.Fight;
        }
    }

    public sealed class CoopCup
    {
        public int Id;
        public CoopDrink Drink;
        public int Holder = -1;
        public CupPlace Place;
    }

    public sealed class CoopStation
    {
        public int Index;
        public CoopDrink Drink;
        public Vector2 Pos;
        public bool Brewing;
        public int Owner = -1;
        public float Progress;
        public CoopCup Ready;
        public bool Attended;

        public float Progress01 => Brewing ? Mathf.Clamp01(Progress / T.Recipe(Drink).BrewSeconds) : Ready != null ? 1f : 0f;
    }

    public sealed class CoopEnemy
    {
        public int Id;
        public Vector2 Pos;
        public float Hp, MaxHp;
        public float Stun;
        public Vector2 KnockVel;
        public float HitFlash;
        public bool Dead;
        public Vector2 Facing = Vector2.left;
        public Vector2 Wander;
        public float WanderTimer;
    }

    /// <summary>
    /// 로컬 2인 협동 실험판 규칙 전체(입력 → 이동/역할/제조/전달/음용/전투/부활/결과). Unity 화면과 분리된 순수 C#이라
    /// EditMode에서 그대로 검사한다. 시간은 Tick의 dt로만 흐르므로 Paused면 모든 타이머(제조·효과·적·남은 시간)가 함께 멈춘다.
    /// </summary>
    public sealed class CoopSim
    {
        public readonly int[] Stock = new int[3];
        public readonly CoopPlayer[] Players = { new CoopPlayer { Index = 0 }, new CoopPlayer { Index = 1 } };
        public readonly CoopStation[] Stations = new CoopStation[3];
        public readonly CoopCup[] Counter = new CoopCup[T.PassCounterCapacity];
        public readonly List<CoopEnemy> Enemies = new List<CoopEnemy>();
        public readonly List<CoopEvent> Events = new List<CoopEvent>();
        /// <summary>사람별 자동 공격 횟수(체감 비교·검증 기록용).</summary>
        public readonly int[] Hits = new int[2];
        /// <summary>사람별 실제 회복량 누계(최대 체력 초과분 제외).</summary>
        public readonly float[] Healed = new float[2];

        /// <summary>전체 진행 시간(제조·효과·적 이동 등). 일시정지 중에는 멈춤.</summary>
        public float Elapsed { get; private set; }
        /// <summary>전투 진행 시간: 홀에 서 있는 사람이 1명 이상일 때만 누적. 5분 목표·단계 난이도는 이것 기준.</summary>
        public float CombatElapsed { get; private set; }
        /// <summary>이번 틱에 홀(전투 역할)에 서 있는 사람이 있었는가.</summary>
        public bool HallActive { get; private set; }
        public CoopOutcome Outcome { get; private set; }
        public bool Paused;
        public int Kills { get; private set; }
        public int CupsBrewed { get; private set; }
        public int CupsDrunk { get; private set; }
        public int RoleSwaps { get; private set; }
        public int Stage { get; private set; }
        /// <summary>EditMode 규칙 검사 전용 스위치(정상 실행 경로는 항상 true). 화면 실행에는 노출하지 않는다.</summary>
        public bool SpawningEnabled = true;

        public float Remaining => Mathf.Max(0f, T.RunSeconds - CombatElapsed);

        readonly int seed;
        System.Random rng;
        int nextDrop, nextCupId, nextEnemyId, tickCount;
        float spawnTimer;

        public CoopSim(int seed = 1117)
        {
            this.seed = seed;
            Reset();
        }

        /// <summary>재시작: 실험판 상태만 초기화한다(저장 파일 없음).</summary>
        public void Reset()
        {
            rng = new System.Random(seed);
            for (int i = 0; i < Stock.Length; i++) Stock[i] = 0;
            Players[0] = new CoopPlayer { Index = 0, Pos = T.P1Start, Facing = Vector2.right };
            Players[1] = new CoopPlayer { Index = 1, Pos = T.P2Start, Facing = Vector2.left };
            foreach (var p in Players) p.SnapRole();
            for (int i = 0; i < Stations.Length; i++)
                Stations[i] = new CoopStation { Index = i, Drink = (CoopDrink)i, Pos = T.StationPos[i] };
            for (int i = 0; i < Counter.Length; i++) Counter[i] = null;
            Enemies.Clear();
            Events.Clear();
            Elapsed = 0f;
            CombatElapsed = 0f;
            HallActive = false;
            Outcome = CoopOutcome.Running;
            Paused = false;
            Kills = CupsBrewed = CupsDrunk = RoleSwaps = Stage = 0;
            Hits[0] = Hits[1] = 0;
            Healed[0] = Healed[1] = 0f;
            nextDrop = nextCupId = nextEnemyId = tickCount = 0;
            spawnTimer = 1.5f;
        }

        public void Tick(float dt, PlayerInput p1, PlayerInput p2)
        {
            if (Paused || Outcome != CoopOutcome.Running || dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);
            Elapsed += dt;
            // 홀에 서 있는 사람이 있을 때만 전투 시간·난이도가 진행된다(둘 다 주방/쓰러짐이면 멈춤, 다른 것은 계속).
            HallActive = IsHallActive();
            if (HallActive) CombatElapsed += dt;

            int stage = Mathf.Min(T.SpawnInterval.Length - 1, (int)(CombatElapsed / T.StageSeconds));
            if (stage != Stage)
            {
                Stage = stage;
                Emit(CoopEventKind.StageUp, -1, Vector2.zero, stage);
                if (SpawningEnabled)
                    for (int i = 0; i < T.StageBurst[stage]; i++) TrySpawn();
            }

            MovePlayer(Players[0], p1.Move, dt);
            MovePlayer(Players[1], p2.Move, dt);
            SeparatePlayers();
            foreach (var p in Players)
            {
                p.UpdateRole();
                var role = p.Role;
                if (role != p.LastRole)
                {
                    p.LastRole = role;
                    RoleSwaps++;
                    Emit(CoopEventKind.RoleChanged, p.Index, p.Pos, (int)role);
                }
            }

            // 같은 프레임 동시 입력은 매 틱 순서를 바꿔 처리(한쪽 고정 우선 없음). 각 처리 자체는 원자적이라 중복·음수 없음.
            int first = (tickCount++ & 1);
            for (int k = 0; k < 2; k++)
            {
                int i = (first + k) % 2;
                var input = i == 0 ? p1 : p2;
                if (input.Interact) Interact(i);
                if (input.Drink) Drink(i);
            }

            TickBrewing(dt);
            TickEffects(dt);
            TickAttacks(dt);
            TickEnemies(dt);
            TickDowned(dt);

            HallActive = IsHallActive();
            if (CombatElapsed >= T.RunSeconds) Finish(CoopOutcome.Success);
            else if (Players[0].Downed && Players[1].Downed) Finish(CoopOutcome.Failed);
        }

        bool IsHallActive()
        {
            foreach (var p in Players) if (!p.Downed && p.Role == CoopRole.Fight) return true;
            return false;
        }

        void Finish(CoopOutcome outcome)
        {
            Outcome = outcome;
            Emit(CoopEventKind.Outcome, -1, Vector2.zero, (int)outcome);
        }

        // ───────── 상호작용 ─────────

        /// <summary>상호작용 키(P1 E / P2 오른쪽 Ctrl). 들고 있으면 놓기·건네기, 빈손이면 집기·제조 시작·취소.</summary>
        public CoopAction Interact(int i)
        {
            var p = Players[i];
            var r = InteractCore(p, true);
            if (r == CoopAction.RejectedInsufficient || r == CoopAction.RejectedBusy || r == CoopAction.RejectedCounterFull ||
                r == CoopAction.HoldingNoTarget || r == CoopAction.RejectedDowned)
                Emit(CoopEventKind.Rejected, i, p.Pos, -1, r);
            return r;
        }

        /// <summary>키를 눌렀을 때 일어날 일(화면 안내용, 상태 변경 없음).</summary>
        public CoopAction PredictInteract(int i) => InteractCore(Players[i], false);

        CoopAction InteractCore(CoopPlayer p, bool apply)
        {
            if (Outcome != CoopOutcome.Running) return CoopAction.RejectedNotRunning;
            if (p.Downed) return CoopAction.RejectedDowned;
            var station = NearestStation(p.Pos);
            bool nearCounter = Vector2.Distance(p.Pos, T.PassCounterPos) <= T.InteractRange;
            var mate = Players[1 - p.Index];

            if (p.Held != null)
            {
                if (nearCounter)
                {
                    int slot = System.Array.IndexOf(Counter, null);
                    if (slot < 0) return CoopAction.RejectedCounterFull;
                    if (!apply) return CoopAction.Placed;
                    var cup = p.Held;
                    p.Held = null;
                    cup.Holder = -1;
                    cup.Place = CupPlace.Counter;
                    Counter[slot] = cup;
                    Emit(CoopEventKind.CupPlaced, p.Index, T.PassCounterPos, (int)cup.Drink);
                    return CoopAction.Placed;
                }
                if (!mate.Downed && mate.Held == null && Vector2.Distance(p.Pos, mate.Pos) <= T.InteractRange)
                {
                    if (!apply) return CoopAction.Given;
                    var cup = p.Held;
                    p.Held = null;
                    mate.Held = cup;
                    cup.Holder = mate.Index;
                    Emit(CoopEventKind.CupGiven, p.Index, mate.Pos, (int)cup.Drink);
                    return CoopAction.Given;
                }
                return CoopAction.HoldingNoTarget;
            }

            if (station != null && station.Ready != null)
            {
                if (!apply) return CoopAction.TakenFromStation;
                var cup = station.Ready;
                station.Ready = null;
                Hold(p, cup);
                Emit(CoopEventKind.CupTaken, p.Index, station.Pos, (int)cup.Drink);
                return CoopAction.TakenFromStation;
            }
            if (nearCounter)
            {
                for (int s = 0; s < Counter.Length; s++)
                {
                    if (Counter[s] == null) continue;
                    if (!apply) return CoopAction.TakenFromCounter;
                    var cup = Counter[s];
                    Counter[s] = null;
                    Hold(p, cup);
                    Emit(CoopEventKind.CupTaken, p.Index, T.PassCounterPos, (int)cup.Drink);
                    return CoopAction.TakenFromCounter;
                }
            }
            if (station != null)
            {
                if (station.Brewing)
                {
                    if (station.Owner != p.Index) return CoopAction.RejectedBusy;
                    if (!apply) return CoopAction.BrewCancelled;
                    // 취소 정책: 확정 때 차감한 재료를 전부 환불(제조 미완성이므로 손실 없음).
                    var rc = T.Recipe(station.Drink);
                    Stock[0] += rc.Lime; Stock[1] += rc.Bean; Stock[2] += rc.Leaf;
                    station.Brewing = false;
                    station.Owner = -1;
                    station.Progress = 0f;
                    Emit(CoopEventKind.BrewCancel, p.Index, station.Pos, (int)station.Drink);
                    return CoopAction.BrewCancelled;
                }
                var recipe = T.Recipe(station.Drink);
                if (!CanAfford(recipe)) return CoopAction.RejectedInsufficient;
                if (!apply) return CoopAction.BrewStarted;
                Stock[0] -= recipe.Lime; Stock[1] -= recipe.Bean; Stock[2] -= recipe.Leaf; // 확정 시 1회 차감
                station.Brewing = true;
                station.Owner = p.Index;
                station.Progress = 0f;
                Emit(CoopEventKind.BrewStart, p.Index, station.Pos, (int)station.Drink);
                return CoopAction.BrewStarted;
            }
            return CoopAction.None;
        }

        static void Hold(CoopPlayer p, CoopCup cup)
        {
            p.Held = cup;
            cup.Holder = p.Index;
            cup.Place = CupPlace.Hand;
        }

        public bool CanAfford(CoopRecipe r) => Stock[0] >= r.Lime && Stock[1] >= r.Bean && Stock[2] >= r.Leaf;

        /// <summary>음용 키(P1 Q / P2 오른쪽 Shift). 들고 있는 잔을 마신 사람에게만 효과가 적용된다.</summary>
        public bool Drink(int i)
        {
            var p = Players[i];
            if (Outcome != CoopOutcome.Running || p.Downed || p.Held == null) return false;
            var cup = p.Held;
            p.Held = null;
            cup.Holder = -1;
            cup.Place = CupPlace.Consumed;
            CupsDrunk++;
            switch (cup.Drink)
            {
                case CoopDrink.LeafTonic:
                    p.HealPool = Mathf.Min(T.TonicHeal, p.HealPool + T.TonicHeal); // 대기 회복량 상한 50
                    break;
                case CoopDrink.RushShot:
                    p.RushTime = T.RushSeconds; // 갱신(누적 없음)
                    break;
                case CoopDrink.CitrusBurst:
                    p.InvulnTime = Mathf.Max(p.InvulnTime, T.BurstInvuln);
                    Shockwave(p);
                    break;
            }
            Emit(CoopEventKind.Drank, i, p.Pos, (int)cup.Drink);
            return true;
        }

        void Shockwave(CoopPlayer p)
        {
            int hit = 0;
            foreach (var e in Enemies)
            {
                if (e.Dead) continue;
                var d = e.Pos - p.Pos;
                if (d.magnitude > T.BurstRadius) continue;
                hit++;
                e.Stun = T.BurstStun;
                e.KnockVel = (d.sqrMagnitude < 0.0001f ? Vector2.down : d.normalized) * T.BurstKnock;
                Damage(e, T.BurstDamage, p.Index);
            }
            Emit(CoopEventKind.Shockwave, p.Index, p.Pos, hit);
        }

        public CoopStation NearestStation(Vector2 pos)
        {
            CoopStation best = null;
            float bestD = T.InteractRange;
            foreach (var s in Stations)
            {
                float d = Vector2.Distance(pos, s.Pos);
                if (d <= bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // ───────── 제조·효과 ─────────

        void TickBrewing(float dt)
        {
            foreach (var s in Stations)
            {
                s.Attended = false;
                if (!s.Brewing) continue;
                // 작업대 곁에 서 있는 사람이 있어야 진행(자리를 비우면 진행 일시정지, 재료는 이미 차감 상태).
                foreach (var p in Players)
                    if (!p.Downed && Vector2.Distance(p.Pos, s.Pos) <= T.InteractRange) s.Attended = true;
                if (!s.Attended) continue;
                s.Progress += dt;
                if (s.Progress >= T.Recipe(s.Drink).BrewSeconds)
                {
                    s.Brewing = false;
                    s.Owner = -1;
                    s.Progress = 0f;
                    s.Ready = new CoopCup { Id = ++nextCupId, Drink = s.Drink, Place = CupPlace.Station };
                    CupsBrewed++;
                    Emit(CoopEventKind.BrewDone, -1, s.Pos, (int)s.Drink);
                }
            }
        }

        void TickEffects(float dt)
        {
            foreach (var p in Players)
            {
                p.HurtFlash = Mathf.Max(0f, p.HurtFlash - dt);
                p.InvulnTime = Mathf.Max(0f, p.InvulnTime - dt);
                p.RushTime = Mathf.Max(0f, p.RushTime - dt);
                if (p.Downed) { p.HealPool = 0f; continue; }
                if (p.HealPool > 0f)
                {
                    float heal = Mathf.Min(p.HealPool, T.TonicHeal / T.TonicSeconds * dt);
                    p.HealPool -= heal;
                    float before = p.Hp;
                    p.Hp = Mathf.Min(T.PlayerMaxHp, p.Hp + heal);
                    Healed[p.Index] += p.Hp - before;
                }
            }
        }

        // ───────── 전투 ─────────

        void TickAttacks(float dt)
        {
            foreach (var p in Players)
            {
                if (p.Downed || p.Role != CoopRole.Fight) { p.AttackTimer = Mathf.Max(0f, p.AttackTimer - dt); continue; }
                p.AttackTimer -= dt;
                if (p.AttackTimer > 0f) continue;
                bool rush = p.RushTime > 0f;
                float range = rush ? T.RushRange : T.AttackRange;
                CoopEnemy target = null;
                float best = range;
                foreach (var e in Enemies)
                {
                    if (e.Dead) continue;
                    float d = Vector2.Distance(p.Pos, e.Pos);
                    if (d <= best) { best = d; target = e; }
                }
                if (target == null) { p.AttackTimer = 0f; continue; }
                p.AttackTimer = rush ? T.RushInterval : T.AttackInterval;
                Hits[p.Index]++;
                p.Facing = (target.Pos - p.Pos).normalized;
                Emit(CoopEventKind.Hit, p.Index, target.Pos, rush ? 1 : 0, CoopAction.None, p.Pos);
                Damage(target, T.AttackDamage, p.Index);
            }
            Enemies.RemoveAll(e => e.Dead);
        }

        void Damage(CoopEnemy e, float amount, int by)
        {
            if (e.Dead) return;
            e.Hp -= amount;
            e.HitFlash = 0.12f;
            if (e.Hp > 0f) return;
            e.Dead = true;
            Kills++;
            Emit(CoopEventKind.Kill, by, e.Pos, 0);
            var ing = T.DropCycle[nextDrop++ % T.DropCycle.Length];
            if (Stock[(int)ing] < T.IngredientCap)
            {
                Stock[(int)ing]++;
                Emit(CoopEventKind.IngredientGained, by, e.Pos, (int)ing);
            }
            else Emit(CoopEventKind.IngredientOverflow, by, e.Pos, (int)ing);
        }

        void TickEnemies(float dt)
        {
            if (SpawningEnabled)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    TrySpawn();
                    spawnTimer = T.SpawnInterval[Stage];
                }
            }

            float speed = T.EnemySpeed[Stage];
            foreach (var e in Enemies)
            {
                e.HitFlash = Mathf.Max(0f, e.HitFlash - dt);
                if (e.KnockVel.sqrMagnitude > 0.0001f)
                {
                    e.Pos = ClampHall(e.Pos + e.KnockVel * dt); // 밀쳐내기도 홀 안에서만
                    e.KnockVel *= Mathf.Exp(-8f * dt);
                }
                if (e.Stun > 0f)
                {
                    e.Stun -= dt;
                }
                else
                {
                    var target = NearestStanding(e.Pos);
                    float moveSpeed = speed;
                    Vector2 goal;
                    if (target != null) goal = ClampHall(target.Pos);
                    else
                    {
                        // 홀에 서 있는 사람이 없으면 홀 안을 느리게 배회(주방 쪽으로 몰려가 문을 막지 않음)
                        e.WanderTimer -= dt;
                        if (e.WanderTimer <= 0f || (e.Wander - e.Pos).sqrMagnitude < 0.04f)
                        {
                            e.Wander = new Vector2(Mathf.Lerp(T.ArenaMinX + 1f, T.ArenaMaxX - 1f, (float)rng.NextDouble()),
                                Mathf.Lerp(T.ArenaMinY + 0.8f, T.EnemyMaxY - 1.2f, (float)rng.NextDouble()));
                            e.WanderTimer = 2.5f + 2f * (float)rng.NextDouble();
                        }
                        goal = e.Wander;
                        moveSpeed = speed * 0.45f;
                    }
                    {
                        var d = goal - e.Pos;
                        if (d.sqrMagnitude > 0.0001f)
                        {
                            var step = d.normalized * moveSpeed * dt;
                            if (step.sqrMagnitude > d.sqrMagnitude) step = d;
                            e.Pos += step;
                            e.Facing = d.normalized;
                        }
                    }
                }
                e.Pos = ClampHall(e.Pos);
            }

            // 적끼리 겹침 완화(상한 24 → 최대 276쌍).
            for (int a = 0; a < Enemies.Count; a++)
                for (int b = a + 1; b < Enemies.Count; b++)
                {
                    var d = Enemies[b].Pos - Enemies[a].Pos;
                    float min = T.EnemyRadius * 2f;
                    float m = d.magnitude;
                    if (m >= min) continue;
                    var n = m < 0.0001f ? new Vector2(1f, 0f) : d / m;
                    var push = n * (min - m) * 0.5f;
                    Enemies[a].Pos = ClampHall(Enemies[a].Pos - push);
                    Enemies[b].Pos = ClampHall(Enemies[b].Pos + push);
                }

            // 접촉 피해(사람당 동시에 최대 3마리, 기절 중인 적은 무해).
            foreach (var p in Players)
            {
                // 주방(제조 역할) 인원은 적의 공격 대상이 아니다.
                if (p.Downed || p.InvulnTime > 0f || p.Role != CoopRole.Fight) continue;
                int touching = 0;
                foreach (var e in Enemies)
                    if (e.Stun <= 0f && Vector2.Distance(e.Pos, p.Pos) <= T.EnemyRadius + T.PlayerRadius + 0.05f) touching++;
                if (touching == 0) continue;
                p.Hp -= T.ContactDps * Mathf.Min(touching, T.ContactCap) * dt;
                p.HurtFlash = 0.15f;
                if (p.Hp <= 0f)
                {
                    p.Hp = 0f;
                    p.Downed = true;
                    p.DownedTime = 0f;
                    p.ReviveProgress = 0f;
                    p.RushTime = 0f;
                    p.HealPool = 0f;
                    Emit(CoopEventKind.Downed, p.Index, p.Pos, 0);
                }
            }
        }

        CoopPlayer NearestStanding(Vector2 pos)
        {
            CoopPlayer best = null;
            float bestD = float.MaxValue;
            foreach (var p in Players)
            {
                if (p.Downed || p.Role != CoopRole.Fight) continue;
                float d = (p.Pos - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>적 위치를 홀 바닥 안(카운터 앞)으로 제한한다. 스폰·추격·밀쳐내기·분리 모두 이 함수를 거친다.</summary>
        public static Vector2 ClampHall(Vector2 p)
        {
            p.x = Mathf.Clamp(p.x, T.ArenaMinX + T.EnemyRadius, T.ArenaMaxX - T.EnemyRadius);
            p.y = Mathf.Clamp(p.y, T.ArenaMinY + T.EnemyRadius, T.EnemyMaxY);
            return p;
        }

        bool TrySpawn()
        {
            if (Enemies.Count >= T.MaxAlive[Stage]) return false;
            // 그림의 입구(홀 좌·우·아래) 중 사람과 떨어진 곳에서 들어온다.
            Vector2 pos = Vector2.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var ent = T.Entrances[rng.Next(T.Entrances.Length)];
                var inward = ent.x <= T.ArenaMinX + 0.01f ? Vector2.right : ent.x >= T.ArenaMaxX - 0.01f ? Vector2.left : Vector2.up;
                var along = new Vector2(inward.y, inward.x) * ((float)rng.NextDouble() - 0.5f) * 0.8f;
                pos = ClampHall(ent + inward * 0.35f + along);
                bool clear = true;
                foreach (var p in Players) if (Vector2.Distance(p.Pos, pos) < 2.2f) clear = false;
                if (clear) break;
            }
            float hp = T.EnemyHp[Stage];
            Enemies.Add(new CoopEnemy { Id = ++nextEnemyId, Pos = pos, Hp = hp, MaxHp = hp });
            Emit(CoopEventKind.EnemySpawn, -1, pos, 0);
            return true;
        }

        // ───────── 쓰러짐·구조 ─────────

        void TickDowned(float dt)
        {
            foreach (var p in Players)
            {
                if (!p.Downed) continue;
                p.DownedTime += dt;
                var mate = Players[1 - p.Index];
                if (!mate.Downed && Vector2.Distance(mate.Pos, p.Pos) <= T.ReviveRange) p.ReviveProgress += dt;
                else p.ReviveProgress = Mathf.Max(0f, p.ReviveProgress - dt);
                if (p.ReviveProgress >= T.ReviveSeconds) Revive(p, T.ReviveHp);
                else if (p.DownedTime >= T.AutoReviveSeconds) Revive(p, T.AutoReviveHp);
            }
        }

        void Revive(CoopPlayer p, float hp)
        {
            p.Downed = false;
            p.Hp = hp;
            p.InvulnTime = T.ReviveInvuln;
            p.DownedTime = 0f;
            p.ReviveProgress = 0f;
            Emit(CoopEventKind.Revived, p.Index, p.Pos, (int)hp);
        }

        // ───────── 이동·충돌 ─────────

        void MovePlayer(CoopPlayer p, Vector2 move, float dt)
        {
            if (move.sqrMagnitude > 1f) move.Normalize();
            p.Moving = !p.Downed && move.sqrMagnitude > 0.01f;
            if (!p.Moving) return;
            p.Facing = move.normalized;
            float speed = T.PlayerSpeed * (p.RushTime > 0f ? T.RushSpeedMul : 1f);
            var before = p.Pos;
            p.Pos = ResolveStatic(p.Pos + move * speed * dt, T.PlayerRadius);
            // 문 모서리 보조: 문 입구 근처에서 위/아래로 누르는데 문설주에 걸리면 문 가운데 쪽으로 미끄러뜨린다.
            float ax = Mathf.Abs(p.Pos.x);
            bool nearMouth = ax > T.DoorHalfWidth - T.PlayerRadius - 0.02f && ax < T.DoorHalfWidth + T.PlayerRadius + 0.6f &&
                             p.Pos.y > T.CounterMinY - T.PlayerRadius - 0.3f && p.Pos.y < T.CounterMaxY + T.PlayerRadius + 0.3f;
            if (nearMouth && Mathf.Abs(move.y) > 0.3f && Mathf.Abs(move.x) < 0.8f && (p.Pos - before).sqrMagnitude < (move * speed * dt).sqrMagnitude * 0.5f)
            {
                float over = ax - (T.DoorHalfWidth - T.PlayerRadius - 0.05f);
                if (over > 0f)
                {
                    float slide = Mathf.Min(over, speed * dt) * Mathf.Sign(p.Pos.x);
                    p.Pos = ResolveStatic(new Vector2(p.Pos.x - slide, p.Pos.y), T.PlayerRadius);
                }
            }
        }

        void SeparatePlayers()
        {
            var a = Players[0];
            var b = Players[1];
            var d = b.Pos - a.Pos;
            float min = T.PlayerRadius * 2f;
            float m = d.magnitude;
            if (m >= min) return;
            var n = m < 0.0001f ? Vector2.right : d / m;
            // 겹치면 서로 밀어내고 옆으로 비켜선다(문 틈 정면 충돌에서도 막히지 않게).
            var perp = new Vector2(-n.y, n.x);
            var push = (n + perp * 0.8f) * (min - m) * 0.5f;
            a.Pos = ResolveStatic(a.Pos - push, T.PlayerRadius);
            b.Pos = ResolveStatic(b.Pos + push, T.PlayerRadius);
        }

        /// <summary>경기장 경계·카운터 두 벽(문 틈 제외)·작업대에서 원을 밀어낸다.</summary>
        public static Vector2 ResolveStatic(Vector2 p, float r)
        {
            p.x = Mathf.Clamp(p.x, T.ArenaMinX + r, T.ArenaMaxX - r);
            p.y = Mathf.Clamp(p.y, T.ArenaMinY + r, T.ArenaMaxY - r);
            p = PushOutOfBox(p, r, new Vector2(T.ArenaMinX - 1f, T.CounterMinY), new Vector2(-T.DoorHalfWidth, T.CounterMaxY));
            p = PushOutOfBox(p, r, new Vector2(T.DoorHalfWidth, T.CounterMinY), new Vector2(T.ArenaMaxX + 1f, T.CounterMaxY));
            foreach (var s in T.StationPos)
                p = PushOutOfBox(p, r, new Vector2(s.x - T.StationHalfWidth, s.y), new Vector2(s.x + T.StationHalfWidth, s.y + T.StationDepth));
            return p;
        }

        static Vector2 PushOutOfBox(Vector2 p, float r, Vector2 min, Vector2 max)
        {
            var closest = new Vector2(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y));
            var d = p - closest;
            if (d.sqrMagnitude >= r * r) return p;
            if (d.sqrMagnitude > 0.000001f) return closest + d.normalized * r;
            // 중심이 상자 안: 가장 얕은 축으로 밀어낸다.
            float left = p.x - min.x, right = max.x - p.x, down = p.y - min.y, up = max.y - p.y;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
            if (m == down) return new Vector2(p.x, min.y - r);
            if (m == up) return new Vector2(p.x, max.y + r);
            if (m == left) return new Vector2(min.x - r, p.y);
            return new Vector2(max.x + r, p.y);
        }

        void Emit(CoopEventKind kind, int player, Vector2 pos, int a, CoopAction action = CoopAction.None, Vector2 from = default)
        {
            Events.Add(new CoopEvent { Kind = kind, Player = player, Pos = pos, A = a, Action = action, From = from });
        }

        /// <summary>현재 존재하는 잔 수(작업대 대기 + 카운터 + 손). 복제 방지 검사용.</summary>
        public int LiveCupCount()
        {
            int n = 0;
            foreach (var s in Stations) if (s.Ready != null) n++;
            foreach (var c in Counter) if (c != null) n++;
            foreach (var p in Players) if (p.Held != null) n++;
            return n;
        }
    }
}
