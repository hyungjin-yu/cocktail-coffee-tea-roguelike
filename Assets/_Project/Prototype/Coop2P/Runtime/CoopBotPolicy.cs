using UnityEngine;
using T = CCT.Prototype.Coop2P.CoopTuning;

namespace CCT.Prototype.Coop2P
{
    /// <summary>
    /// 검증 전용 두 사람 자동 플레이 정책(정상 규칙만 사용: 이동·상호작용·마시기 키 입력에 해당하는 값만 낸다).
    /// 체력·재고·스폰·시간을 바꾸지 않는다. 실빌드 검증기는 이 출력을 실제 키 이벤트로 바꿔 넣는다.
    /// 기본: 둘 다 홀에서 손님을 피하며 자동 공격. 재료가 모이면 한 사람만 주방에 들어가 상황에 맞는 음료를 만들어 들고 나온다.
    /// </summary>
    public sealed class CoopBotPolicy
    {
        enum Job { Fight, Brew }

        readonly Job[] job = { Job.Fight, Job.Fight };
        readonly CoopDrink[] target = new CoopDrink[2];
        readonly float[] pressCooldown = new float[2];

        public int BrewTrips { get; private set; }

        public void Decide(CoopSim s, float dt, out PlayerInput p1, out PlayerInput p2)
        {
            p1 = One(s, 0, dt);
            p2 = One(s, 1, dt);
        }

        PlayerInput One(CoopSim s, int i, float dt)
        {
            var input = new PlayerInput();
            var me = s.Players[i];
            var mate = s.Players[1 - i];
            pressCooldown[i] = Mathf.Max(0f, pressCooldown[i] - dt);
            if (me.Downed || s.Outcome != CoopOutcome.Running || s.Paused) { job[i] = Job.Fight; return input; }

            // 1) 들고 있는 잔
            if (me.Held != null)
            {
                job[i] = Job.Fight;
                var d = me.Held.Drink;
                bool inHall = me.Role == CoopRole.Fight;
                if (d == CoopDrink.RushShot && inHall) input.Drink = Press(i);
                else if (d == CoopDrink.LeafTonic)
                {
                    if (me.Hp <= 70f) input.Drink = Press(i);
                    else if (!mate.Downed && mate.Hp <= 60f && mate.Held == null)
                    {
                        if (s.PredictInteract(i) == CoopAction.Given) input.Interact = Press(i);
                        else { input.Move = Toward(me.Pos, mate.Pos); return input; }
                    }
                }
                else if (d == CoopDrink.CitrusBurst && inHall)
                {
                    if (Near(s, me.Pos, 3f) >= 3 || (me.Hp <= 45f && Near(s, me.Pos, 2f) >= 1)) input.Drink = Press(i);
                }
                if (!inHall) { input.Move = Toward(me.Pos, HallAnchor(i)); return input; }
                input.Move = Kite(s, me, HallAnchor(i));
                return input;
            }

            // 2) 동료 구조
            if (mate.Downed && Near(s, mate.Pos, 2.2f) <= 2 && me.Hp > 35f)
            {
                job[i] = Job.Fight;
                input.Move = Vector2.Distance(me.Pos, mate.Pos) > T.ReviveRange * 0.6f ? Toward(me.Pos, mate.Pos) : Kite(s, me, mate.Pos) * 0.4f;
                return input;
            }

            // 3) 제조 담당 결정(한 번에 한 사람)
            // 마지막 단계 대비: 4단계 후반엔 각자 버스트 한 잔씩 들어 두고, 5단계에는 주방 왕복을 멈춘다.
            bool prep = s.Elapsed >= 200f;
            bool tripsAllowed = s.Elapsed < 245f;
            if (tripsAllowed && job[i] == Job.Fight && job[1 - i] == Job.Fight && (mate.Held == null || prep) && !AnyStationBusy(s))
            {
                bool doorClear = Near(s, DoorPoint, 2.6f) <= 1 && Near(s, me.Pos, 2.0f) <= 2;
                if (doorClear && !mate.Downed && me.Hp >= 45f && TryPick(s, out var drink) && me.Hp >= mate.Hp - 0.01f && !(me.Hp == mate.Hp && i == 1))
                {
                    job[i] = Job.Brew;
                    target[i] = drink;
                    BrewTrips++;
                }
            }

            if (job[i] == Job.Brew)
            {
                var st = s.Stations[(int)target[i]];
                var spot = st.Pos + new Vector2(0f, -0.75f);
                if (st.Ready != null && Vector2.Distance(me.Pos, st.Pos) <= T.InteractRange)
                {
                    if (s.PredictInteract(i) == CoopAction.TakenFromStation) input.Interact = Press(i);
                    return input;
                }
                if (st.Brewing && st.Owner == i)
                {
                    if (Near(s, me.Pos, 1.4f) > 0) { input.Move = KiteKitchen(s, me, spot); return input; }
                    if (Vector2.Distance(me.Pos, spot) > 0.3f) input.Move = Toward(me.Pos, spot);
                    return input; // 곁에서 기다림(제조 진행)
                }
                if (st.Brewing || st.Ready != null || !s.CanAfford(T.Recipe(target[i])))
                {
                    if (st.Ready == null) { job[i] = Job.Fight; }
                    else { input.Move = Toward(me.Pos, spot); return input; }
                }
                else if (Vector2.Distance(me.Pos, spot) < 0.3f)
                {
                    if (s.PredictInteract(i) == CoopAction.BrewStarted) input.Interact = Press(i);
                    return input;
                }
                else { input.Move = Toward(me.Pos, spot); return input; }
            }

            // 4) 전투(홀)
            if (me.Role == CoopRole.Brew) { input.Move = Toward(me.Pos, HallAnchor(i)); return input; }
            input.Move = Kite(s, me, HallAnchor(i));
            return input;
        }

        bool Press(int i)
        {
            if (pressCooldown[i] > 0f) return false;
            pressCooldown[i] = 0.35f;
            return true;
        }

        static bool AnyStationBusy(CoopSim s)
        {
            foreach (var st in s.Stations) if (st.Brewing || st.Ready != null) return true;
            return false;
        }

        static bool TryPick(CoopSim s, out CoopDrink drink)
        {
            float minHp = Mathf.Min(s.Players[0].Hp, s.Players[1].Hp);
            drink = CoopDrink.LeafTonic;
            if (s.Elapsed >= 200f) { drink = CoopDrink.CitrusBurst; return s.CanAfford(T.Recipe(CoopDrink.CitrusBurst)); }
            if (minHp <= 70f && s.CanAfford(T.Recipe(CoopDrink.LeafTonic))) { drink = CoopDrink.LeafTonic; return true; }
            if (s.Enemies.Count >= 9 && s.CanAfford(T.Recipe(CoopDrink.CitrusBurst))) { drink = CoopDrink.CitrusBurst; return true; }
            if (s.CanAfford(T.Recipe(CoopDrink.RushShot)) && s.Players[0].RushTime <= 0f && s.Players[1].RushTime <= 0f) { drink = CoopDrink.RushShot; return true; }
            return false;
        }

        static readonly Vector2 DoorPoint = new Vector2(0f, -0.1f);
        // 문에서 떨어진 기준점: 따라오는 손님 무리가 문을 막지 않게
        static Vector2 HallAnchor(int i) => new Vector2(i == 0 ? -4.8f : 4.8f, -2.0f);

        static Vector2 KiteKitchen(CoopSim s, CoopPlayer me, Vector2 spot)
        {
            var v = (spot - me.Pos) * 0.3f;
            foreach (var e in s.Enemies)
            {
                var d = me.Pos - e.Pos;
                float m = d.magnitude;
                if (m < 2f && m > 0.001f) v += d / m * (2f - m);
            }
            if (me.Pos.y < T.CounterMaxY + 0.6f) v.y += 1f;
            if (me.Pos.x < T.ArenaMinX + 1f) v.x += 1f;
            if (me.Pos.x > T.ArenaMaxX - 1f) v.x -= 1f;
            return v.sqrMagnitude < 0.04f ? Vector2.zero : v.normalized;
        }

        static int Near(CoopSim s, Vector2 p, float r)
        {
            int n = 0;
            foreach (var e in s.Enemies) if (Vector2.Distance(e.Pos, p) <= r) n++;
            return n;
        }

        /// <summary>문 틈을 경유하는 이동 방향.</summary>
        public static Vector2 Toward(Vector2 from, Vector2 to)
        {
            bool fk = from.y > T.BoundaryY, tk = to.y > T.BoundaryY;
            Vector2 goal = to;
            if (fk != tk) goal = Mathf.Abs(from.x) > 0.3f ? new Vector2(0f, fk ? T.CounterMaxY + 0.45f : T.CounterMinY - 0.45f) : new Vector2(0f, tk ? T.CounterMaxY + 0.45f : T.CounterMinY - 0.45f);
            // 문 띠 안에서는 문설주에 비비지 않도록 먼저 위/아래로 빠져나간다(8방향 키 입력 양자화 대비)
            bool inDoorBand = Mathf.Abs(from.x) < T.DoorHalfWidth + 0.1f && from.y > T.CounterMinY - 0.35f && from.y < T.CounterMaxY + 0.35f;
            if (inDoorBand && Mathf.Abs(goal.x - from.x) > 0.4f) goal = new Vector2(Mathf.Clamp(from.x, -0.5f, 0.5f), (fk ? T.CounterMaxY + 0.6f : T.CounterMinY - 0.6f));
            var d = goal - from;
            return d.sqrMagnitude < 0.0004f ? Vector2.zero : d.normalized;
        }

        /// <summary>8방향 후보 중 1.1칸 앞 위치가 손님에게서 멀고 벽·구석·문에서 떨어진 쪽을 고른다(키 입력과 같은 8방향).</summary>
        static Vector2 Kite(CoopSim s, CoopPlayer me, Vector2 anchor)
        {
            float best = Score(s, me.Pos, anchor) + 0.15f; // 제자리 유지에 약간 가산(떨림 방지)
            var bestDir = Vector2.zero;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var p = CoopSim.ResolveStatic(me.Pos + dir * 1.1f, T.PlayerRadius);
                if (p.y > T.CounterMinY) continue;
                float sc = Score(s, p, anchor);
                if (sc > best) { best = sc; bestDir = dir; }
            }
            return bestDir;
        }

        static float Score(CoopSim s, Vector2 p, Vector2 anchor)
        {
            float sc = 0f;
            foreach (var e in s.Enemies)
            {
                float d = Vector2.Distance(p, e.Pos);
                if (d < 4f) sc -= (e.Stun > 0f ? 0.3f : 1f) / Mathf.Max(0.25f, d * d);
            }
            float wx = Mathf.Min(p.x - T.ArenaMinX, T.ArenaMaxX - p.x);
            float wy = Mathf.Min(p.y - T.ArenaMinY, T.CounterMinY - p.y);
            if (wx < 2f) sc -= (2f - wx) * 0.5f;
            if (wy < 1.2f) sc -= (1.2f - wy) * 0.6f;
            float dd = Vector2.Distance(p, DoorPoint);
            if (dd < 2f) sc -= (2f - dd) * 0.3f;
            sc -= Vector2.Distance(p, anchor) * 0.04f;
            return sc;
        }
    }
}
