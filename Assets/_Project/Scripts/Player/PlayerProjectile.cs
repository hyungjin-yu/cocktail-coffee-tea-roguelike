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
        /// <summary>#23 발사 묶음 번호(0 = 세지 않음). PlayerProjectileWeapon.Fire가 SpawnVolleyId로 넘긴 값을 생성 때 복사한다.</summary>
        int volleyId;
        /// <summary>#23 무기가 한 번의 발사 동안만 설정하는 발사 묶음 번호(그 외 생성 — 원형 파열 파편·보조 활 등 — 은 0).</summary>
        public static int SpawnVolleyId;
        public int VolleyId => volleyId;

        /// <param name="artId">04 §8 탄 아트(bullet/arrow/tea_arrow). 없거나 미반입이면 노란 원 placeholder.</param>
        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage, int pierceCount,
            Transform parent, string artId = null, PlayerController owner = null, bool original = false,
            Enemy excluded = null, float lifetime = Lifetime, float maxDistance = float.PositiveInfinity)
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
            p.volleyId = SpawnVolleyId;
            if (excluded != null) p.alreadyHit.Add(excluded);
            if (original && owner != null && IsArrowArt(artId)) ApplyArrowElement(go, sr, ArrowElementOf(owner.Stats), art);
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
                // 2026-11-09 피격 연출: 치명타/일반 구분, 불꽃은 실제 접촉 지점. 파편(비원본)은 보조 효과로 가볍게.
                float reach = enemy.Def.radius + 0.6f; // 시작부터 겹친 캐스트 등 접점이 몸에서 멀면 몸 중앙 위로 대체
                Vector2? at = ((Vector2)hit.point - (Vector2)enemy.transform.position).sqrMagnitude <= reach * reach ? hit.point : (Vector2?)null;
                // 2026-11-10 원래 탄의 직접 타격만 상성(단일 피해 결과). 파편(비원본)은 추가 효과라 상성 재귀 없음.
                if (original) enemy.TakeDirectHit(dealt, HiddenSkillRuntime.DirectKind(hidden, false), at);
                else enemy.TakeDamage(dealt, HitKind.Effect, at);
                hidden?.AfterDirectHit(enemy);
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
