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

    /// <summary>쓴맛 안개: 플레이어와 KeepDistance만큼 거리를 두고 천천히 맴돈다. PulseEvery마다 TelegraphSeconds(정확히 1초) 동안 발밑
    /// 정식 파동 아트(bitter_mist_pulse charge 6장·6fps)가 재생되고 — 바깥 테두리가 처음부터 판정 경계(반경 PulseRadius)를 표시 —
    /// 끝나는 순간 파동(burst) — 안에 있으면 이동 둔화·공격 속도 감소. 저이펙트에서도 예고는 그린다(피할 정보).
    /// 아트가 없으면(누락) 기존 절차 원으로 떨어진다.</summary>
    public class BitterMistDriver : MonoBehaviour, IEnemyDriver, IHudHazard
    {
        public const float KeepDistance = 3.4f, PulseEvery = 4.5f, TelegraphSeconds = 1f, PulseRadius = 2.4f;
        public const float SlowSeconds = 3f, SlowMultiplier = 0.7f, AttackSlowSeconds = 3f, AttackSlowMultiplier = 0.75f;
        Enemy enemy; GameManager game;
        SpriteRenderer warn, edge;      // 아트 누락 시 fallback 절차 원
        SpriteRenderer fx; ArtClip chargeClip, burstClip; // 정식 파동 아트(256x128 센터 피벗)
        float timer = 2f, telegraph = -1f, flash;
        /// <summary>정식 파동 아트를 쓰는 중인지(테스트·QA).</summary>
        public bool UsesFormalArt => fx != null;
        public bool Telegraphing => telegraph >= 0f;
        public int Pulses { get; private set; }

        public void Init(Enemy e, GameManager g)
        {
            enemy = e; game = g;
            e.SetDriver(this);
            var art = ArtLibrary.Get(FeedbackArt.BitterMistPulse);
            chargeClip = art?.Clip("charge"); burstClip = art?.Clip("burst");
            if (chargeClip != null && burstClip != null)
            {
                var go = new GameObject("PulseFx");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, -enemy.Def.radius, 0f);
                // 텍스처 자체가 반투명 타원이라 Y 압축·코드 색 틴트는 겹쳐 쓰지 않는다. 바깥 테두리 가로 지름 = 판정 지름(반경 2.4의 두 배).
                float s = PulseRadius * 2f / (FeedbackArt.PulseOutlinePx / art.pixelsPerUnit);
                go.transform.localScale = new Vector3(s, s, 1f);
                fx = go.AddComponent<SpriteRenderer>();
                fx.sortingOrder = SortOrders.GroundZone; fx.color = Color.white; fx.enabled = false;
                return;
            }
            warn = Ring("PulseWarn", 0.18f);
            edge = Ring("PulseEdge", 0.55f);
        }

        void HideFx() { if (fx != null) fx.enabled = false; if (warn != null) warn.enabled = false; if (edge != null) edge.enabled = false; }

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
            if (enemy == null || enemy.IsDead || game == null || game.State != GameState.Playing || game.Player == null) { HideFx(); return; }
            float dt = Time.deltaTime;
            if (flash > 0f)
            {
                flash -= dt;
                if (fx != null)
                {
                    if (flash > 0f) { fx.sprite = burstClip.FrameAt(burstClip.Duration - flash); fx.enabled = true; }
                    else fx.enabled = false;
                }
                else { edge.color = new Color(0.8f, 0.6f, 1f, 0.6f * Mathf.Clamp01(flash / 0.25f)); if (flash <= 0f) edge.enabled = false; }
            }
            if (telegraph >= 0f)
            {
                telegraph += dt;
                float k = Mathf.Clamp01(telegraph / TelegraphSeconds);
                if (fx != null)
                {
                    fx.sprite = chargeClip.FrameAt(telegraph); fx.enabled = true;
                }
                else
                {
                    warn.enabled = edge.enabled = true;
                    SetRing(warn, PulseRadius * k); SetRing(edge, PulseRadius);
                    warn.color = new Color(0.65f, 0.4f, 1f, 0.12f + 0.2f * k);
                    edge.color = new Color(0.8f, 0.6f, 1f, 0.35f + 0.25f * Mathf.PingPong(telegraph * 6f, 1f));
                }
                if (k >= 1f) Pulse();
                return;
            }
            timer -= dt;
            // 몸체 action(6장·10fps = 0.6초)은 예고 시작에 재생하고 파동까지 마지막 컷을 유지한다(hold = 예고 1초).
            if (timer <= 0f) { telegraph = 0f; enemy.PlayAction("action", TelegraphSeconds); }
        }

        /// <summary>2026-11-23 HUD 가림 판정(표시 전용): 몸체 + 예고·파동이 보이는 동안은 바닥 테두리(= 판정 경계) 그림 경계.
        /// 위치·반경·판정은 바꾸지 않고, HUD가 이 영역을 덮을 때 HudHazardOcclusion이 겹친 패널만 반투명으로 낮춘다.</summary>
        public bool TryGetHudHazardBounds(out Bounds worldBounds)
        {
            worldBounds = default;
            if (enemy == null || enemy.IsDead) return false;
            worldBounds = enemy.VisualBounds;
            if (fx != null) { if (fx.enabled && fx.sprite != null) worldBounds.Encapsulate(fx.bounds); }
            else if (edge != null && edge.enabled) worldBounds.Encapsulate(edge.bounds);
            return true;
        }

        /// <summary>파동: 반경 안이면 디버프. 시험에서 직접 부를 수 있다.</summary>
        public void Pulse()
        {
            telegraph = -1f; timer = PulseEvery; Pulses++;
            if (fx != null)
            {
                flash = burstClip.Duration; fx.sprite = burstClip.frames[0]; fx.enabled = true;
            }
            else { warn.enabled = false; flash = 0.25f; SetRing(edge, PulseRadius); }
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
    /// 닿으면(Enemy 접촉 경로가 회복 차단·공격 속도 감소를 건다) FleeSeconds 동안 반대쪽으로 달아난다.
    /// 시각: 접촉 순간 action(낚아채기 4장·14fps)을 재생하고 이어 flee 클립을 도주 시간 동안 반복한다(방향은 실제 도주 속도를 따른다).
    /// 클립은 표시 전용 — 접촉·도주 타이머는 시각 클립과 독립이다.</summary>
    public class GlassThiefDriver : MonoBehaviour, IEnemyDriver
    {
        public const float WarnDistance = 2.8f, FleeSeconds = 1.6f;
        Enemy enemy; GameManager game;
        float flee;
        public bool Fleeing => flee > 0f;
        public bool Warning { get; private set; }

        public const float SnatchSeconds = 4f / 14f;
        float snatchLeft; bool fleeClipPlayed;
        public void Init(Enemy e, GameManager g)
        {
            enemy = e; game = g;
            e.SetDriver(this);
            e.ContactLanded += _ =>
            {
                flee = FleeSeconds; fleeClipPlayed = false;
                snatchLeft = e.PlayAction("action") ? SnatchSeconds : 0f;
            };
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
            if (flee > 0f)
            {
                flee -= Time.deltaTime;
                if (snatchLeft > 0f) snatchLeft -= Time.deltaTime;
                else if (!fleeClipPlayed) { fleeClipPlayed = true; enemy.PlayAction("flee", Mathf.Max(0.1f, flee)); }
            }
            float d = ((Vector2)transform.position - game.Player.Position).magnitude;
            Warning = flee <= 0f && d <= WarnDistance;
            var basis = enemy.Def.iconTint; // 정식 아트: 흰색(원본 색 그대로)
            enemy.BaseColor = Warning ? Color.Lerp(basis, new Color(0.35f, 0.65f, 1f), HitFeedback.LowEffects ? 0.6f : 0.35f + 0.35f * Mathf.PingPong(Time.time * 8f, 1f)) : basis;
        }
    }
}
