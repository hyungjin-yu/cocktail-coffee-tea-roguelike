using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #22 바닥 위험(물기·얼룩)의 런 안 실행부. EnemySpawner 자식 오브젝트에 붙어 런과 함께 사라진다(리트라이·타이틀 = Destroy).
    /// - 일정: EnemySpawner.Update가 Playing·정규 모드·보스전 아님일 때만 TickSchedule을 부른다(스포너를 끈 시험에선 생성 없음).
    /// - 진행: 자체 Update가 Playing이고 일시정지·카운트다운이 아닐 때만 예고/체류를 진행(선택 창 timeScale 0이면 dt 0).
    /// - 런 종료(GameOver): 모든 구역·디버프·오라를 즉시 정리.
    /// - 그림: 예고 = 기존 장판 예고 링(ZoneTelegraphV2, 판정 경계와 같은 테두리가 예고 시간 동안 차오름),
    ///   구역 = 기존 웅덩이 장판(ZoneVisualV2, 불투명 영역 = 판정 지름) + 같은 테두리의 청소 링(안쪽이 체류 진행만큼 차오름).
    ///   저이펙트: 웅덩이 애니메이션은 유지(정보), 청소 링 깜빡임 없음. 활성 오브젝트 = 구역당 2~3 렌더러, 상한 3곳.
    /// </summary>
    public sealed class FloorHazardSystem : MonoBehaviour
    {
        sealed class View
        {
            public FloorHazardZone zone;
            public GameObject root;
            public ZoneTelegraphV2 telegraph;
            public SpriteRenderer fallbackWarn;
            public ZoneVisualV2 pool;
            public SpriteRenderer fallbackPool;
            public ZoneTelegraphV2 cleanRing;
            public HazardHud hud;
        }

        /// <summary>HUD 가림 판정용 — 구역(예고 포함) 테두리 경계.</summary>
        sealed class HazardHud : IHudHazard
        {
            public FloorHazardZone zone;
            public bool TryGetHudHazardBounds(out Bounds b)
            {
                b = default;
                if (zone == null || zone.Cleaned) return false;
                b = new Bounds(zone.Center, new Vector3(zone.Radius * 2f, zone.Radius * 2f, 0.1f));
                return true;
            }
        }

        GameManager game;
        PlayerController player;
        Camera cam;
        Vector2 startPoint;
        readonly System.Random rng = new System.Random(0x22F1); // 런 시드 난수와 분리(기존 몹 추첨 불변)
        readonly List<View> views = new List<View>();
        float gapLeft;
        PlayerFloorDebuffAura aura;

        public FloorHazardField Field { get; } = new FloorHazardField();
        /// <summary>일정상 처리한 차례 수(생성 + 상한으로 건너뛴 밀린 차례).</summary>
        public int ScheduledIndex { get; private set; }
        public int Spawned => Field.Created;
        public PlayerFloorDebuffAura Aura => aura;
        public string StatusText => Field.StatusText();

        public static FloorHazardSystem Create(Transform parent, GameManager game, PlayerController player, Camera cam)
        {
            var go = new GameObject("FloorHazards");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<FloorHazardSystem>();
            s.game = game; s.player = player; s.cam = cam;
            s.startPoint = player != null ? player.Position : new Vector2(0f, Balance.LaneCenterY);
            s.Field.Materialized += s.OnMaterialized;
            s.Field.CleanedZone += s.OnCleaned;
            if (player != null) s.aura = PlayerFloorDebuffAura.Attach(player);
            return s;
        }

        /// <summary>정규 모드 일정. 밀린 차례는 하나만 남기고(폭주 방지) 상한·유예·배치 실패면 다음 틱에 다시 시도.</summary>
        public void TickSchedule(float runTime)
        {
            if (game == null || player == null || player.IsDead) return;
            if (!FloorHazardRules.ModeHasHazards(game.Mode)) return;
            if (game.ActiveBoss != null) return; // 보스전 중 쉼(이미 있는 구역·디버프는 유지 — 청소 가능)
            int due = FloorHazardRules.DueCount(runTime);
            if (ScheduledIndex >= due) return;
            if (due - ScheduledIndex > 1) ScheduledIndex = due - 1;
            if (gapLeft > 0f) return;
            var kind = Field.NextKind(ScheduledIndex);
            if (kind == null) return;
            GetView(out float minX, out float maxX);
            if (!FloorHazardRules.TryPlace(kind.Value, player.Position, startPoint, minX, maxX, Field.Zones, rng, out var pos)) return;
            SpawnAt(kind.Value, pos);
            ScheduledIndex++;
        }

        void GetView(out float minX, out float maxX)
        {
            var c = cam != null ? cam : Camera.main;
            if (c == null) { minX = BattleArena.MinX; maxX = BattleArena.MaxX; return; }
            float half = c.orthographicSize * c.aspect;
            minX = c.transform.position.x - half; maxX = c.transform.position.x + half;
        }

        /// <summary>시험·QA도 같은 경로(예고 → 생성 → 즉시 디버프).</summary>
        public FloorHazardZone SpawnAt(FloorHazardKind kind, Vector2 center, float warnSeconds = FloorHazardRules.WarnSeconds)
        {
            gapLeft = FloorHazardRules.MinGapSeconds;
            var v = new View { root = new GameObject("FloorHazard_" + kind) };
            v.root.transform.SetParent(transform, false);
            v.root.transform.position = center;
            views.Add(v);
            // 예고를 먼저 만든다(Add가 warnSeconds 0이면 곧바로 Materialized를 부른다).
            if (warnSeconds > 0f)
            {
                v.telegraph = ZoneTelegraphV2.Spawn(center, FloorHazardRules.Radius(kind), warnSeconds, FloorHazardRules.Tint(kind), v.root.transform);
                if (v.telegraph == null) v.fallbackWarn = Disc(v.root.transform, "Warn", FloorHazardRules.Radius(kind), FloorHazardRules.Tint(kind) * new Color(1f, 1f, 1f, 0.35f), SortOrders.GroundZone + 1);
            }
            v.zone = Field.Add(kind, center, warnSeconds);
            v.hud = new HazardHud { zone = v.zone };
            HudHazardOcclusion.Register(v.hud);
            return v.zone;
        }

        static SpriteRenderer Disc(Transform parent, string name, float radius, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle; sr.sortingOrder = order; sr.color = color;
            float size = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
            go.transform.localScale = Vector3.one * (radius * 2f / size); // 판정 원과 같은 지름
            return sr;
        }

        void OnMaterialized(FloorHazardZone z)
        {
            var v = views.Find(x => x.zone == z);
            if (v == null) { v = views.Find(x => x.zone == null); if (v != null) v.zone = z; } // Add 안에서 즉시 생성된 경우
            if (v == null) return;
            if (v.telegraph != null) { Destroy(v.telegraph.gameObject); v.telegraph = null; }
            if (v.fallbackWarn != null) { Destroy(v.fallbackWarn.gameObject); v.fallbackWarn = null; }
            var poolGo = new GameObject("Pool");
            poolGo.transform.SetParent(v.root.transform, false);
            if (ZoneVisualV2.AttachActive(poolGo, FloorHazardRules.ArtId(z.Kind), z.Radius, float.MaxValue, SortOrders.GroundZone))
            {
                v.pool = poolGo.GetComponent<ZoneVisualV2>();
                v.pool.Tint = FloorHazardRules.PoolTint(z.Kind);
            }
            else v.fallbackPool = Disc(poolGo.transform, "PoolFallback", z.Radius, FloorHazardRules.Tint(z.Kind) * new Color(1f, 1f, 1f, 0.45f), SortOrders.GroundZone);
            v.cleanRing = ZoneTelegraphV2.SpawnManual(z.Center, z.Radius, FloorHazardRules.Tint(z.Kind), v.root.transform);
            ApplyDebuffs(); // 생성 순간 플레이어 위치와 무관하게 즉시
        }

        void OnCleaned(FloorHazardZone z)
        {
            var v = views.Find(x => x.zone == z);
            if (v != null) RemoveView(v);
            gapLeft = Mathf.Max(gapLeft, FloorHazardRules.MinGapSeconds);
        }

        void RemoveView(View v)
        {
            HudHazardOcclusion.Unregister(v.hud);
            if (v.root != null) Destroy(v.root);
            views.Remove(v);
        }

        void ApplyDebuffs()
        {
            if (player == null) return;
            player.Stats.SetFloorHazards(Field.SlipActive, Field.StressActive);
            if (aura != null) aura.SetActive(Field.AnyDebuff);
        }

        /// <summary>런 종료·씬 정리: 구역·디버프·오라 모두 제거.</summary>
        public void ClearAll()
        {
            Field.Clear();
            for (int i = views.Count - 1; i >= 0; i--) RemoveView(views[i]);
            ApplyDebuffs();
        }

        void Update()
        {
            if (game == null) return;
            if (game.State == GameState.GameOver) { if (Field.Total > 0 || (player != null && (player.Stats.FloorSlip || player.Stats.FloorStress))) ClearAll(); return; }
            if (game.IsPaused || game.State != GameState.Playing) return;
            float dt = Time.deltaTime;
            if (gapLeft > 0f) gapLeft -= dt;
            bool canClean = player != null && !player.IsDead;
            Field.Tick(dt, player != null ? player.Position : Vector2.zero, canClean);
            ApplyDebuffs();
            foreach (var v in views)
                if (v.cleanRing != null && v.zone != null && v.zone.Active)
                {
                    v.cleanRing.Progress = v.zone.Clean01;
                    if (v.pool != null) v.pool.AlphaScale = Mathf.Lerp(1f, 0.45f, v.zone.Clean01); // 닦일수록 옅어짐(경계·크기는 그대로)
                }
        }

        void OnDestroy()
        {
            foreach (var v in views) HudHazardOcclusion.Unregister(v.hud);
            views.Clear();
            Field.Clear();
            if (player != null) { player.Stats.SetFloorHazards(false, false); if (aura != null) aura.SetActive(false); }
        }

        // ── 시험·QA
        public int ViewCount => views.Count;
        public float CleanRingProgress(FloorHazardZone z) { var v = views.Find(x => x.zone == z); return v != null && v.cleanRing != null ? v.cleanRing.Progress : -1f; }
        public bool HasPool(FloorHazardZone z) { var v = views.Find(x => x.zone == z); return v != null && (v.pool != null || v.fallbackPool != null); }
        public bool HasWarning(FloorHazardZone z) { var v = views.Find(x => x.zone == z); return v != null && (v.telegraph != null || v.fallbackWarn != null); }
        public void SetStartPointForTests(Vector2 p) => startPoint = p;
        public void ResetGapForTests() => gapLeft = 0f;
    }

    /// <summary>
    /// 2026-11-24 #22 추가 지시: 바닥 디버프(미끄러짐·스트레스 중 하나라도)가 걸린 동안 캐릭터 실루엣 바깥에 얇은 먹빛·짙은 보라 외곽.
    /// 몸체 스프라이트를 같은 정렬 그룹 안 맨 뒤(-3: 보조 무기 -1보다 뒤)에 4방향으로 조금씩 밀어 그려 테두리만 비친다 — 몸·무기를 덮지 않고,
    /// 바닥 예고(GroundZone)보다 위 레이어지만 실루엣 밖 두께가 약 0.05 유닛이라 적 예고를 가리지 않는다.
    /// 디버프가 몇 개여도 오라는 하나(겹침 없음). 마지막 디버프가 청소로 풀리는 틱에 즉시 꺼진다.
    /// 일반 효과: 천천히 숨쉬는 알파, 저이펙트: 고정 알파. 차 소믈리에의 밝은 잔물결 음료 오라(발밑 원판)와 위치·색·형태가 다르다.
    /// </summary>
    public sealed class PlayerFloorDebuffAura : MonoBehaviour
    {
        public const float OutlineWorld = 0.05f, FixedAlpha = 0.6f;
        public static readonly Color Ink = new Color(0.16f, 0.05f, 0.26f, 1f);
        static readonly Vector2[] dirs = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        PlayerVisual visual;
        readonly SpriteRenderer[] layers = new SpriteRenderer[4];
        bool active;

        public bool Visible => active && layers[0] != null && layers[0].enabled;
        public float CurrentAlpha => layers[0] != null ? layers[0].color.a : 0f;

        public static PlayerFloorDebuffAura Attach(PlayerController player)
        {
            var v = player.Visual;
            if (v == null) return null;
            var a = v.GetComponent<PlayerFloorDebuffAura>() ?? v.gameObject.AddComponent<PlayerFloorDebuffAura>();
            a.visual = v;
            a.Build();
            return a;
        }

        void Build()
        {
            if (layers[0] != null) return;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("FloorDebuffOutline" + i);
                go.transform.SetParent(visual.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -3; // 정렬 그룹(Actor) 안: 보조 무기(-1)·몸(0)·무기(1) 뒤
                sr.enabled = false;
                layers[i] = sr;
            }
        }

        public void SetActive(bool on)
        {
            if (active == on) return;
            active = on;
            if (!on) foreach (var l in layers) if (l != null) l.enabled = false; // 즉시 사라짐
            else Refresh();
        }

        void LateUpdate() { if (active) Refresh(); }

        void Refresh()
        {
            var body = visual != null ? visual.Body : null;
            if (body == null) return;
            var ls = visual.transform.lossyScale;
            float sx = Mathf.Max(1e-4f, Mathf.Abs(ls.x)), sy = Mathf.Max(1e-4f, Mathf.Abs(ls.y));
            float a = HitFeedback.LowEffects ? FixedAlpha : 0.45f + 0.25f * Mathf.PingPong(Time.time * 1.2f, 1f);
            for (int i = 0; i < 4; i++)
            {
                var l = layers[i];
                if (l == null) continue;
                l.sprite = body.sprite;
                l.enabled = body.enabled && body.sprite != null;
                l.color = new Color(Ink.r, Ink.g, Ink.b, a);
                l.transform.localPosition = new Vector3(dirs[i].x * OutlineWorld / sx, dirs[i].y * OutlineWorld / sy, 0f);
            }
        }
    }
}
