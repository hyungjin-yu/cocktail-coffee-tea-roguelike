using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-22 주인 결정(피드백 13): 바텐더·차 공용 디버프 몹의 등장 일정. 기존 몹 추첨표(EnemyRoster)·시드 난수는 건드리지 않는
    /// 별도 고정 일정이다: 정규 모드에서 FirstSeconds(120초)부터 IntervalSeconds(30초)마다 한 마리, 쓴맛 안개 → 빈 잔 도둑 번갈아,
    /// 동시에 MaxAlive(2)마리까지, 보스전 중에는 쉰다. 끝없는(마스터 도전) 모드는 이번 범위 밖(등장 없음).
    /// </summary>
    public static class DebuffMobSchedule
    {
        public const float FirstSeconds = 120f, IntervalSeconds = 30f;
        public const int MaxAlive = 2;

        /// <summary>runTime까지 예정된 등장 횟수(0부터). 120초 = 1번째, 150초 = 2번째 …</summary>
        public static int DueCount(float runTime) => runTime < FirstSeconds ? 0 : 1 + Mathf.FloorToInt((runTime - FirstSeconds) / IntervalSeconds);
        /// <summary>n번째(0부터) 등장 몹: 짝수 = 쓴맛 안개, 홀수 = 빈 잔 도둑.</summary>
        public static EnemyDef DefFor(int index) => index % 2 == 0 ? EnemyDef.BitterMist : EnemyDef.GlassThief;
        public static bool IsDebuffMob(EnemyDef d) => d == EnemyDef.BitterMist || d == EnemyDef.GlassThief;

        public static int AliveCount()
        {
            int n = 0;
            foreach (var e in Enemy.Active) if (e != null && !e.IsDead && IsDebuffMob(e.Def)) n++;
            return n;
        }

        /// <summary>정의에 맞는 행동 드라이버를 붙인다(생성 직후 한 번).</summary>
        public static void AttachDriver(Enemy e, GameManager game)
        {
            if (e == null) return;
            if (e.Def == EnemyDef.BitterMist) e.gameObject.AddComponent<BitterMistDriver>().Init(e, game);
            else if (e.Def == EnemyDef.GlassThief) e.gameObject.AddComponent<GlassThiefDriver>().Init(e, game);
        }
    }

    /// <summary>쓴맛 안개: 플레이어와 KeepDistance만큼 거리를 두고 천천히 맴돈다. PulseEvery마다 TelegraphSeconds 동안 발밑 보라 원이
    /// 반경 PulseRadius까지 차오르며(예고) 끝나는 순간 파동 — 안에 있으면 이동 둔화·공격 속도 감소. 저이펙트에서도 예고 원은 그린다(피할 정보).</summary>
    public class BitterMistDriver : MonoBehaviour, IEnemyDriver
    {
        public const float KeepDistance = 3.4f, PulseEvery = 4.5f, TelegraphSeconds = 1f, PulseRadius = 2.4f;
        public const float SlowSeconds = 3f, SlowMultiplier = 0.7f, AttackSlowSeconds = 3f, AttackSlowMultiplier = 0.75f;
        Enemy enemy; GameManager game;
        SpriteRenderer warn, edge;
        float timer = 2f, telegraph = -1f, flash;
        public bool Telegraphing => telegraph >= 0f;
        public int Pulses { get; private set; }

        public void Init(Enemy e, GameManager g)
        {
            enemy = e; game = g;
            e.SetDriver(this);
            warn = Ring("PulseWarn", 0.18f);
            edge = Ring("PulseEdge", 0.55f);
        }

        SpriteRenderer Ring(string name, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -enemy.Def.radius, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle; sr.sortingOrder = SortOrders.GroundZone;
            sr.color = new Color(0.65f, 0.4f, 1f, alpha); sr.enabled = false;
            return sr;
        }

        void SetRing(SpriteRenderer sr, float radius)
        {
            float size = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
            float d = radius * 2f / size;
            sr.transform.localScale = new Vector3(d, d * 0.5f, 1f); // 바닥 원근 타원(판정은 원형 거리)
        }

        public bool TryGetVelocity(Vector2 self, Vector2 player, out Vector2 velocity)
        {
            var to = player - self;
            float dist = to.magnitude;
            var dir = dist > 0.001f ? to / dist : Vector2.right;
            float approach = Mathf.Clamp((dist - KeepDistance) * 0.8f, -1f, 1f); // 가까우면 물러나고 멀면 다가간다
            var side = new Vector2(-dir.y, dir.x) * 0.45f; // 천천히 맴돌기
            velocity = (dir * approach + side) * enemy.Def.speed * (telegraph >= 0f ? 0.3f : 1f);
            return true;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || game == null || game.State != GameState.Playing || game.Player == null) { if (warn != null) warn.enabled = edge.enabled = false; return; }
            float dt = Time.deltaTime;
            if (flash > 0f) { flash -= dt; edge.color = new Color(0.8f, 0.6f, 1f, 0.6f * Mathf.Clamp01(flash / 0.25f)); if (flash <= 0f) edge.enabled = false; }
            if (telegraph >= 0f)
            {
                telegraph += dt;
                float k = Mathf.Clamp01(telegraph / TelegraphSeconds);
                warn.enabled = edge.enabled = true;
                SetRing(warn, PulseRadius * k); SetRing(edge, PulseRadius);
                warn.color = new Color(0.65f, 0.4f, 1f, 0.12f + 0.2f * k);
                edge.color = new Color(0.8f, 0.6f, 1f, 0.35f + 0.25f * Mathf.PingPong(telegraph * 6f, 1f));
                if (k >= 1f) Pulse();
                return;
            }
            timer -= dt;
            if (timer <= 0f) { telegraph = 0f; enemy.PlayAction("action"); }
        }

        /// <summary>파동: 반경 안이면 디버프. 시험에서 직접 부를 수 있다.</summary>
        public void Pulse()
        {
            telegraph = -1f; timer = PulseEvery; Pulses++;
            warn.enabled = false; flash = 0.25f; SetRing(edge, PulseRadius);
            var p = game.Player;
            if (p == null || p.IsDead) return;
            if (((Vector2)transform.position - p.Position).sqrMagnitude <= PulseRadius * PulseRadius)
            {
                p.Stats.ApplySlow(SlowSeconds, SlowMultiplier);
                p.Stats.ApplyAttackSlow(AttackSlowSeconds, AttackSlowMultiplier);
            }
        }
    }

    /// <summary>빈 잔 도둑: 기본 추적으로 빠르게 다가오고, WarnDistance 안에 들어오면 몸이 파랗게 깜빡여 예고한다.
    /// 닿으면(Enemy 접촉 경로가 회복 차단·공격 속도 감소를 건다) FleeSeconds 동안 반대쪽으로 달아난다.</summary>
    public class GlassThiefDriver : MonoBehaviour, IEnemyDriver
    {
        public const float WarnDistance = 2.8f, FleeSeconds = 1.6f;
        Enemy enemy; GameManager game;
        float flee;
        public bool Fleeing => flee > 0f;
        public bool Warning { get; private set; }

        SpriteRenderer marker;
        public void Init(Enemy e, GameManager g)
        {
            enemy = e; game = g;
            e.SetDriver(this);
            e.ContactLanded += _ => flee = FleeSeconds;
            // 디버프 몹 표식(임시): 머리 위 작은 하늘색 원 — 일반 몹과 한눈에 구분
            var go = new GameObject("DebuffMarker");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, e.Def.radius * 2.6f, 0f);
            marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = SpriteFactory.Circle; marker.sortingOrder = SortOrders.AirVfx;
            marker.color = new Color(0.45f, 0.8f, 1f, 0.95f);
            float size = marker.sprite != null ? marker.sprite.bounds.size.x : 1f;
            go.transform.localScale = Vector3.one * (0.28f / size);
        }

        public bool TryGetVelocity(Vector2 self, Vector2 player, out Vector2 velocity)
        {
            if (flee <= 0f) { velocity = Vector2.zero; return false; }
            var away = self - player;
            velocity = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right) * enemy.Def.speed * 1.1f;
            return true;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || game == null || game.Player == null) return;
            if (flee > 0f) flee -= Time.deltaTime;
            float d = ((Vector2)transform.position - game.Player.Position).magnitude;
            Warning = flee <= 0f && d <= WarnDistance;
            var basis = enemy.Def.iconTint;
            enemy.BaseColor = Warning ? Color.Lerp(basis, new Color(0.35f, 0.65f, 1f), HitFeedback.LowEffects ? 0.6f : 0.35f + 0.35f * Mathf.PingPong(Time.time * 8f, 1f)) : basis;
        }
    }
}
