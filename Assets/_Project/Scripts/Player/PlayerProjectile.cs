using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 총/활(원거리 무기)이 쏘는 단순 직선 발사체. 기본은 처음 맞힌 몹에게 피해를 주고 사라지지만,
    /// pierceCount 만큼 더 뚫고 지나가며 같은 몹은 두 번 때리지 않는다.
    /// </summary>
    public class PlayerProjectile : MonoBehaviour
    {
        /// <summary>살아 있는 개수(10 §3 고정 부하 시나리오 계측용). 활성/비활성 시점에만 갱신한다.</summary>
        public static int ActiveCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveCount() => ActiveCount = 0;

        void OnEnable() => ActiveCount++;
        void OnDisable() => ActiveCount--;

        const float Lifetime = 2f;
        const float HitRadius = 0.18f;

        readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();
        Vector2 velocity;
        float damage;
        float life;
        int pierceRemaining;
        PlayerController owner;
        bool original;
        float remainingDistance = float.PositiveInfinity;
        string projectileArtId;
        // 총 전용 유도(GunHoming). 0단계는 직진. 표적은 진행 방향 원뿔 안의 살아 있는 적, 이미 맞힌 적·무적은 제외.
        int homingLevel;
        float homingBudgetDeg, homingScan;
        Enemy homingTarget;
        /// <summary>#23 발사 묶음 번호(0 = 세지 않음). PlayerProjectileWeapon.Fire가 SpawnVolleyId로 넘긴 값을 생성 때 복사한다.</summary>
        int volleyId;
        /// <summary>#23 무기가 한 번의 발사 동안만 설정하는 발사 묶음 번호(그 외 생성 — 원형 파열 파편·보조 활 등 — 은 0).</summary>
        public static int SpawnVolleyId;
        public int VolleyId => volleyId;
        /// <summary>2026-11-24 #21 속성 화살(기본 활의 원래 화살만). 감전 연쇄는 첫 대상에서만.</summary>
        ArrowElementKind element;
        bool elementFirstHitDone;
        int volley; // #21 속성 화살 묶음(#23 volleyId와 별개)
        public ArrowElementKind Element => element;

        /// <param name="artId">04 §8 탄 아트(bullet/arrow/tea_arrow). 없거나 미반입이면 노란 원 placeholder.</param>
        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage, int pierceCount,
            Transform parent, string artId = null, PlayerController owner = null, bool original = false,
            Enemy excluded = null, float lifetime = Lifetime, float maxDistance = float.PositiveInfinity, int homingLevel = 0,
            ArrowElementKind element = ArrowElementKind.None, int volley = 0)
        {
            var go = new GameObject("PlayerProjectile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var art = ArtLibrary.Get(artId);
            var sprite = art?.FirstSprite;
            var sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                // 시각 길이 ≈ 판정 지름 × 3(가늘고 긴 탄), 오른쪽이 진행 방향. 판정(HitRadius)은 그대로.
                go.transform.localScale = Vector3.one * (HitRadius * 2f * 3f / Mathf.Max(0.01f, art.ContentWorldSize.x));
                if (direction.sqrMagnitude > 0.0001f)
                    go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                sr.sprite = sprite;
                sr.color = Color.white;
            }
            else
            {
                go.transform.localScale = Vector3.one * (HitRadius * 2f);
                sr.sprite = SpriteFactory.Circle;
                sr.color = new Color(1f, 0.9f, 0.5f, 0.95f);
            }
            go.transform.localScale *= 0.5f; // 사용자 피드백: 모든 탄 그림을 절반으로. HitRadius/피해/속도는 유지.
            sr.sortingOrder = SortOrders.Projectile;

            var p = go.AddComponent<PlayerProjectile>();
            p.velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.zero;
            p.damage = damage;
            p.life = lifetime;
            p.pierceRemaining = Mathf.Max(0, pierceCount);
            p.owner = owner;
            p.original = original;
            p.projectileArtId = artId;
            p.remainingDistance = maxDistance;
            p.homingLevel = GunHoming.Clamp(homingLevel);
            p.homingBudgetDeg = GunHoming.BudgetDeg(p.homingLevel);
            p.volleyId = SpawnVolleyId;
            if (excluded != null) p.alreadyHit.Add(excluded);
            // 속성은 원래 화살 + 주인 있음 + 화살 그림일 때만(보조 활 tea_arrow는 주인 없이 쏘므로 해당 없음).
            p.element = original && owner != null && IsArrowArt(artId) ? element : ArrowElementKind.None;
            p.volley = volley;
            if (p.element != ArrowElementKind.None) ApplyElementArrowArt(go, sr, p.element);
            else if (original && owner != null && IsArrowArt(artId)) ApplyArrowElement(go, sr, ArrowElementOf(owner.Stats), art);
        }

        /// <summary>2026-11-24 #21 속성 화살 비행 그림: 기본 화살과 같은 128 캔버스·가로 스팬(98px)·중앙 피벗·+X라 배율·회전을 그대로 두고 그림만 바꾼다.
        /// 보통 = &lt;속성&gt;_arrow 3프레임 루프, 저이펙트 = &lt;속성&gt;_arrow_low 정지. 아트가 없으면 속성 색 틴트(판정·피해 무관).</summary>
        static void ApplyElementArrowArt(GameObject go, SpriteRenderer sr, ArrowElementKind e)
        {
            bool low = ArrowElementRules.LowEffects;
            var frames = ArrowElementArt.Frames(e, low ? "arrow_low" : "arrow");
            if (frames.Length == 0) frames = ArrowElementArt.Frames(e, "arrow_low");
            if (frames.Length == 0) { sr.color = ArrowElementArt.FallbackColor(e); return; }
            sr.sprite = frames[0];
            sr.color = Color.white;
            if (!low && frames.Length > 1) go.AddComponent<ArrowElementAnim>().Play(frames, ArrowElementArt.ArrowMs, true, false);
        }

        // ───── 2026-11-16 피드백 19: 화살의 기존 적중 속성 표현(표시 전용) ─────
        // 새 화살 종류·새 피해가 아니다. 원래 탄이 직접 적중하면 EnemyHitStatus.AttachDirect가 붙이는 화염/여운과 **같은 조건**(Lv>0, 잠금 아님)일 때
        // 화살 머리 색과 짧은 꼬리 점으로 보여 준다. 피해·확률·습득(카드)·판정 반지름·속도·관통은 그대로. 파편(비원본)은 상태를 붙이지 않으므로 표시도 없다.
        [Flags] public enum ArrowElement { None = 0, Flame = 1, Bitter = 2 }

        public static bool IsArrowArt(string artId) => !string.IsNullOrEmpty(artId) && artId.Contains("arrow");

        public static ArrowElement ArrowElementOf(PlayerStats stats)
        {
            if (stats == null) return ArrowElement.None;
            var e = ArrowElement.None;
            if (stats.flameLevel > 0 && !stats.FlameLocked) e |= ArrowElement.Flame;
            if (stats.poisonLevel > 0 && !stats.PoisonLocked) e |= ArrowElement.Bitter;
            return e;
        }

        public static readonly Color FlameTint = new Color(1f, 0.62f, 0.42f), BitterTint = new Color(0.66f, 0.86f, 0.46f);
        static readonly Color FlameTail = new Color(1f, 0.45f, 0.15f, 0.9f), BitterTail = new Color(0.5f, 0.62f, 0.3f, 0.9f);
        /// <summary>꼬리 테두리(Codex 검토표 외곽 #201D29).</summary>
        public static readonly Color TailOutline = new Color(0.125f, 0.114f, 0.161f, 0.9f);

        /// <summary>머리 = 우선 속성 색(화염 우선), 꼬리 점 = 둘 다면 반대 속성 색(두 색으로 동시 보유 표시). 저이펙트면 꼬리 없이 머리 색만.</summary>
        // ── 2026-11-17 전용 꼬리 아트 연결(Codex 납품 시 자동): Resources/ArtExtra/vfx_arrow_tail/<id>_00..02.png, 32×16·PPU128·피벗 = 앞쪽 소켓.
        public const string TailFolder = "ArtExtra/vfx_arrow_tail/";
        public const float TailFps = 8f;
        static readonly Dictionary<string, Sprite[]> tailFrames = new Dictionary<string, Sprite[]>();

        /// <summary>속성 → 전용 꼬리 아트 ID(화염만 / 여운만 / 둘 다).</summary>
        public static string TailArtId(ArrowElement e) =>
            (e & ArrowElement.Flame) != 0 && (e & ArrowElement.Bitter) != 0 ? "vfx_arrow_dual_tail"
            : (e & ArrowElement.Flame) != 0 ? "vfx_arrow_fire_tail" : (e & ArrowElement.Bitter) != 0 ? "vfx_arrow_bitter_tail" : null;

        /// <summary>전용 꼬리 프레임(_00, _01 …). 없으면 빈 배열 → 원형 점 대체.</summary>
        public static Sprite[] TailFrames(string id)
        {
            if (string.IsNullOrEmpty(id)) return new Sprite[0];
            if (tailFrames.TryGetValue(id, out var f)) return f;
            var list = new List<Sprite>();
            for (int i = 0; i < 8; i++) { var s = Resources.Load<Sprite>($"{TailFolder}{id}_{i:00}"); if (s == null) break; list.Add(s); }
            f = list.ToArray();
            tailFrames[id] = f;
            return f;
        }

        /// <summary>화살 그림의 깃 끝(불투명 영역 왼쪽 가운데)을 화살 로컬 좌표로. 꼬리 아트의 소켓 피벗을 여기에 붙인다.</summary>
        public static Vector2 FletchSocketLocal(ArtAsset art)
        {
            if (art == null || art.frameSizePx.x <= 0f) return new Vector2(-0.36f, 0f);
            var c = art.contentRectNormalized;
            return new Vector2((c.xMin - art.pivotNormalized.x) * art.frameSizePx.x / art.pixelsPerUnit,
                (c.center.y - art.pivotNormalized.y) * art.frameSizePx.y / art.pixelsPerUnit);
        }

        static void ApplyArrowElement(GameObject go, SpriteRenderer sr, ArrowElement e, ArtAsset arrowArt)
        {
            if (e == ArrowElement.None || sr == null) return;
            bool flame = (e & ArrowElement.Flame) != 0, bitter = (e & ArrowElement.Bitter) != 0;
            sr.color = flame ? FlameTint : BitterTint;
            if (SettingsService.Current.effectsLevel == SettingsData.EffectsLow) return;
            var frames = TailFrames(TailArtId(e));
            if (frames.Length > 0)
            {
                // 전용 아트: 화살 로컬 공간(같은 PPU 128·같은 배율)에 소켓 피벗을 깃 끝에 맞춰 붙이고 8fps 반복. 판정·피해 무관.
                var t = new GameObject("ElementTail");
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = FletchSocketLocal(arrowArt);
                var r = t.AddComponent<SpriteRenderer>();
                r.sprite = frames[0];
                r.sortingOrder = sr.sortingOrder - 1;
                if (frames.Length > 1) t.AddComponent<ArrowTailAnim>().frames = frames;
                return;
            }
            var tail = new GameObject("ElementTail");
            tail.transform.SetParent(go.transform, false);
            float k = 1f / Mathf.Max(0.01f, go.transform.localScale.x); // 부모(화살) 배율을 풀어 월드 크기로 지정
            tail.transform.localPosition = new Vector3(-0.32f * k, 0f, 0f); // 화살 뒤 0.32(월드) — 화살 길이 ≈0.54의 꼬리 쪽
            tail.transform.localScale = Vector3.one * (0.12f * k);
            var tr = tail.AddComponent<SpriteRenderer>();
            tr.sprite = SpriteFactory.Circle;
            tr.color = bitter && flame ? BitterTail : flame ? FlameTail : BitterTail;
            tr.sortingOrder = sr.sortingOrder - 1;
            // 2026-11-16 후속: 찻집 녹색 바닥에서 녹색 꼬리 대비가 낮았다(followup feedback4 12_arrow_bitter) → 꼬리 뒤 어두운 테두리 원(표시 전용, 판정·속성 불변).
            var ring = new GameObject("ElementTailOutline");
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = tail.transform.localPosition;
            ring.transform.localScale = Vector3.one * (0.17f * k);
            var rr = ring.AddComponent<SpriteRenderer>();
            rr.sprite = SpriteFactory.Circle;
            rr.color = TailOutline;
            rr.sortingOrder = sr.sortingOrder - 2;
        }

        bool ValidHomingTarget(Enemy e, Vector2 pos) =>
            e != null && !e.IsDead && !e.Invulnerable && !alreadyHit.Contains(e) && (owner == null || e.RunPlayer == owner) &&
            ((Vector2)e.transform.position - pos).sqrMagnitude <= GunHoming.DetectRadius * GunHoming.DetectRadius * GunHoming.LoseRadiusScale * GunHoming.LoseRadiusScale;

        /// <summary>표적이 없으면 0.1초마다 원뿔 안 가장 작은 각도의 적을 잡고, 있으면 초당 선회각 상한 안에서만 방향을 튼다(순간 꺾임·무한 추적 없음).</summary>
        void SteerHoming(Vector2 pos, float dt)
        {
            if (velocity.sqrMagnitude < 0.0001f || homingBudgetDeg <= 0f) return;
            if (!ValidHomingTarget(homingTarget, pos)) homingTarget = null;
            if (homingTarget == null)
            {
                homingScan -= dt;
                if (homingScan > 0f) return;
                homingScan = GunHoming.ScanInterval;
                float best = float.MaxValue;
                for (int i = 0; i < Enemy.Active.Count; i++)
                {
                    var e = Enemy.Active[i];
                    if (!ValidHomingTarget(e, pos)) continue;
                    Vector2 to = (Vector2)e.transform.position - pos;
                    if (!GunHoming.InCone(velocity, to, homingLevel)) continue;
                    float a = Vector2.Angle(velocity, to);
                    if (a < best) { best = a; homingTarget = e; }
                }
                if (homingTarget == null) return;
            }
            float speed = velocity.magnitude;
            Vector2 dir = GunHoming.Steer(velocity / speed, (Vector2)homingTarget.transform.position - pos, homingLevel, dt, ref homingBudgetDeg);
            velocity = dir * speed;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            if (owner != null && owner.IsDead) { Destroy(gameObject); return; }
            if (Time.timeScale <= 0f) return;
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // 프레임 간 선분 스윕으로 고속 이동 시 통과 누락을 막는다 (06:20). 경로상 가까운
            // 충돌부터 순서대로 처리하고, 무적 적은 피해·관통 횟수를 소비하지 않고 통과한다(06:23).
            Vector2 prev = transform.position;
            if (homingLevel > 0) SteerHoming(prev, Time.deltaTime);
            Vector2 delta = velocity * Time.deltaTime;
            if (delta.magnitude > remainingDistance) delta = delta.normalized * remainingDistance;
            Vector2 next = prev + delta;
            float dist = delta.magnitude;

            var hits = dist > 0.0001f
                ? Physics2D.CircleCastAll(prev, HitRadius, delta.normalized, dist)
                : Physics2D.CircleCastAll(prev, HitRadius, Vector2.right, 0f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (!hit.collider.TryGetComponent<Enemy>(out var enemy)) continue;
                if (enemy.IsDead || enemy.Invulnerable || alreadyHit.Contains(enemy) ||
                    (owner != null && enemy.RunPlayer != owner)) continue;

                if (original) EnemyHitStatus.AttachDirect(enemy, owner);
                var hidden = original && owner != null ? owner.HiddenRuntime : null; // 41 히든: 원래 탄의 직접 타격만
                float dealt = hidden != null ? hidden.ModifyDirectDamage(enemy, damage) : damage;
                dealt = ArrowElementRules.ModifyDirect(element, enemy, owner, dealt); // #21 빙결 화살: 보스만 이 화살 피해 증가
                // 2026-11-09 피격 연출: 치명타/일반 구분, 불꽃은 실제 접촉 지점. 파편(비원본)은 보조 효과로 가볍게.
                float reach = enemy.Def.radius + 0.6f; // 시작부터 겹친 캐스트 등 접점이 몸에서 멀면 몸 중앙 위로 대체
                Vector2? at = ((Vector2)hit.point - (Vector2)enemy.transform.position).sqrMagnitude <= reach * reach ? hit.point : (Vector2?)null;
                // 2026-11-10 원래 탄의 직접 타격만 상성(단일 피해 결과). 파편(비원본)은 추가 효과라 상성 재귀 없음.
                if (original) enemy.TakeDirectHit(dealt, HiddenSkillRuntime.DirectKind(hidden, false), at);
                else enemy.TakeDamage(dealt, HitKind.Effect, at);
                hidden?.AfterDirectHit(enemy);
                if (element != ArrowElementKind.None)
                {
                    // #21 피해 적용 뒤(막타면 상태 없이 명중 연출·감전 연쇄만). 감전은 이 화살의 첫 대상에서만 — 관통으로 연쇄가 늘지 않게.
                    ArrowElementStatus.OnArrowHit(enemy, owner, element, !elementFirstHitDone, dealt,
                        at ?? (Vector2)enemy.transform.position + new Vector2(0f, enemy.Def.radius * 0.3f), volley);
                    elementFirstHitDone = true;
                }
                alreadyHit.Add(enemy);
                if (volleyId != 0 && owner != null) owner.HiddenRuntime?.OnGunVolleyHit(volleyId, hit.point); // #23 같은 발사는 1회만 센다
                if (original && owner != null && owner.FinalForms.Has(AttackFinalForm.Pierce))
                {
                    Vector2 impact = hit.point;
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4f;
                        Spawn(impact, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 5f, damage * .2f, 0,
                            transform.parent, projectileArtId, owner, false, enemy, .6f, 3f);
                    }
                }

                if (pierceRemaining <= 0)
                {
                    Destroy(gameObject);
                    return;
                }
                pierceRemaining--;
            }

            transform.position = next;
            remainingDistance -= dist;
            if (remainingDistance <= 0f) Destroy(gameObject);
        }
    }
    /// <summary>2026-11-17 화살 전용 꼬리 프레임 반복(표시 전용, 8fps, 일시정지 중 멈춤).</summary>
    public class ArrowTailAnim : MonoBehaviour
    {
        public Sprite[] frames;
        SpriteRenderer r;
        float t;
        void Awake() => r = GetComponent<SpriteRenderer>();
        void Update()
        {
            if (frames == null || frames.Length == 0 || r == null) return;
            t += Time.deltaTime;
            r.sprite = frames[Mathf.FloorToInt(t * PlayerProjectile.TailFps) % frames.Length];
        }
    }
}
