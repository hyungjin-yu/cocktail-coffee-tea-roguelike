using UnityEngine;

namespace CCT
{
    /// <summary>투척형/탄막형 몹이 쏘는 단순 직선 발사체.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        /// <summary>살아 있는 개수(10 §3 고정 부하 시나리오 계측용). 활성/비활성 시점에만 갱신한다.</summary>
        public static int ActiveCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveCount() => ActiveCount = 0;

        void OnEnable() => ActiveCount++;
        void OnDisable() => ActiveCount--;

        /// <summary>spriteResourcePath가 이 접두어로 시작하면 ArtLibrary(04 §8 VFX) ID로 본다. 예: "art:glass_shard".</summary>
        public const string ArtPrefix = "art:";
        const float Lifetime = 3f;
        const float HitRadius = 0.22f;
        /// <summary>57(2026-10-15) 사용자 피드백: 물방울 탄은 지금 보이는 크기의 절반(추가 ×0.5)으로, 판정 반지름도 같은 비율(0.22→0.11).
        /// 다른 탄은 그대로. 피해·속도·수명 불변.</summary>
        public const string WaterDropId = "water_drop";
        public const float WaterDropScale = 0.5f;
        public const string IceShardId = "ice_shard";
        public const float IceShardScale = 0.5f;
        float hitRadius = HitRadius;
        /// <summary>테스트용 — 이 탄의 판정 반지름.</summary>
        public float HitRadiusValue => hitRadius;

        PlayerController player;
        Vector2 velocity;
        float damage;
        float life;
        System.Action onHit;
        /// <summary>67: 이 탄을 쏜 적/공격(사망 원인 기록 전용, 생성 시점 값).</summary>
        DamageSource source;

        /// <summary>2026-11-15 피드백 20: 쏜 몹 등급(일반·정예·보스) — 테두리 색·두께와 꼬리 길이만 다르다. 판정 반지름·피해·속도는 그대로.</summary>
        public enum Tier { Normal, Elite, Boss }
        public static Tier TierOf(EnemyDef def) => def == null ? Tier.Normal : def.IsBoss ? Tier.Boss : def.archetype == EnemyArchetype.EliteBarrage ? Tier.Elite : Tier.Normal;
        public static Color RimColor(Tier t) => t == Tier.Boss ? new Color(1f, 0.18f, 0.12f, 1f) : t == Tier.Elite ? new Color(1f, 0.62f, 0.15f, 0.95f) : new Color(0.03f, 0.01f, 0.02f, 0.9f);
        public static float RimScale(Tier t) => t == Tier.Boss ? 1.36f : t == Tier.Elite ? 1.3f : 1.22f;
        public static int TailCount(Tier t) => t == Tier.Boss ? 3 : t == Tier.Elite ? 2 : 0;
        public Tier ShotTier { get; private set; }
        public float HitRadiusForTests => hitRadius;
        Transform[] tail;

        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage,
            PlayerController player, Transform parent, string spriteResourcePath = null,
            System.Action onHit = null, DamageSource source = default, Tier tier = Tier.Normal)
        {
            var go = new GameObject("EnemyProjectile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            bool isArt = spriteResourcePath != null && spriteResourcePath.StartsWith(ArtPrefix);
            // 물방울(새 아트·옛 아이콘 모두): 뾰족한 끝이 로컬 +Y, 둥근 몸통이 로컬 -Y — 둥근 쪽이 진행 방향을 향해야 한다.
            bool waterDrop = spriteResourcePath != null && spriteResourcePath.EndsWith(WaterDropId);
            bool iceShard = spriteResourcePath != null && spriteResourcePath.EndsWith(IceShardId);
            float sizeMultiplier = waterDrop ? WaterDropScale : iceShard ? IceShardScale : 1f;
            var art = isArt ? ArtLibrary.Get(spriteResourcePath.Substring(ArtPrefix.Length)) : null;
            var icon = isArt ? art?.FirstSprite
                : string.IsNullOrEmpty(spriteResourcePath) ? null : Resources.Load<Sprite>(spriteResourcePath);
            // 새 아트가 아직 없으면 같은 이름의 기존 아이콘(Icons/Enemies/<id>)으로 단계적 대체.
            if (isArt && art == null)
                icon = Resources.Load<Sprite>("Icons/Enemies/" + spriteResourcePath.Substring(ArtPrefix.Length));
            // 실제 아이콘은 캔버스 여백 때문에 작아 보여서 시각적으로만 키운다 (히트 판정은 HitRadius 그대로).
            // 04 §8 새 VFX는 알파 영역 기준으로 판정 지름의 약 2.2배 길이.
            float visualScale = art != null ? HitRadius * 2f * 2.2f / Mathf.Max(0.01f, art.ContentWorldSize.x)
                : icon != null ? HitRadius * 2f * Balance.EnemyProjectileIconVisualScale : HitRadius * 2f;
            go.transform.localScale = Vector3.one * (visualScale * 0.5f * sizeMultiplier); // 물방울·얼음 탄만 현재 크기에서 추가 절반.

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = icon != null ? icon : SpriteFactory.Circle;
            sr.color = icon != null ? Color.white : new Color(0.65f, 0.88f, 1f, 0.95f);
            sr.sortingOrder = SortOrders.Projectile;
            if (art != null && art.Main != null && art.Main.frames.Length > 1)
            {
                var anim = go.AddComponent<SpriteAnimator>();
                anim.Bind(sr);
                anim.Play(art.Main);
            }

            // 57 P0-2: 작은 적 탄이 바닥 무늬에 묻혔다 — 같은 그림의 얇은 어두운 실루엣 외곽만 뒤(z+)에 깐다(색 원 후광은 사용자 반려).
            var outline = new GameObject("Outline");
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.01f / Mathf.Max(0.0001f, go.transform.localScale.x));
            outline.transform.localScale = Vector3.one * RimScale(tier);
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite = sr.sprite;
            osr.color = RimColor(tier); // 일반 = 기존 어두운 실루엣, 정예 = 주황 테두리, 보스 = 붉은 굵은 테두리
            osr.sortingOrder = SortOrders.Projectile;
            if (art != null && art.Main != null && art.Main.frames.Length > 1)
            {
                var oanim = outline.AddComponent<SpriteAnimator>();
                oanim.Bind(osr);
                oanim.Play(art.Main);
            }

            var p = go.AddComponent<EnemyProjectile>();
            p.player = player;
            p.velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.zero;
            p.damage = damage;
            p.hitRadius = HitRadius * sizeMultiplier;
            p.life = Lifetime;
            p.onHit = onHit;
            p.source = source;
            p.ShotTier = tier;
            // 등급 꼬리: 같은 그림의 옅고 작은 잔상(진행 반대쪽). 저이펙트면 생략(테두리 색만으로 구분). 판정 없음.
            int tails = HitFeedback.LowEffects ? 0 : TailCount(tier);
            if (tails > 0)
            {
                p.tail = new Transform[tails];
                for (int i = 0; i < tails; i++)
                {
                    var g = new GameObject("Tail" + i);
                    g.transform.SetParent(go.transform, false);
                    g.transform.localScale = Vector3.one * (0.8f - 0.15f * i);
                    var gsr = g.AddComponent<SpriteRenderer>();
                    gsr.sprite = sr.sprite;
                    var c = RimColor(tier); c.a = 0.45f - 0.12f * i;
                    gsr.color = c;
                    gsr.sortingOrder = SortOrders.Projectile - 1;
                    p.tail[i] = g.transform;
                }
            }

            // 물방울/얼음조각 아트는 위쪽이 뾰족하고 아래쪽(로컬 -Y)이 두꺼운 모양이라,
            // 두꺼운 쪽이 진행 방향(플레이어 쪽)을 향하도록 발사 방향으로 회전시킨다.
            if (p.velocity.sqrMagnitude > 0.0001f)
            {
                // 04 §8 새 VFX 탄은 오른쪽(+X)이 진행 방향 — +90° 보정은 기존 아이콘(아래가 앞)에만.
                float angle = Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg + (art != null && !waterDrop ? 0f : 90f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void Update()
        {
            if (Time.deltaTime <= 0f) return; // 2026-11-11 정지된 시간(일시정지·제조 창·박람회 현수막 설치)에는 이동·명중 판정 없음
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (tail != null && velocity.sqrMagnitude > 0.0001f)
            {
                Vector3 back = -(Vector3)velocity.normalized;
                float spacing = HitRadius * (ShotTier == Tier.Boss ? 1.6f : 1.2f);
                for (int i = 0; i < tail.Length; i++) if (tail[i] != null) tail[i].position = transform.position + back * spacing * (i + 1) + new Vector3(0f, 0f, 0.02f);
            }

            if (player == null || player.IsDead) return;

            float touch = hitRadius + PlayerController.Radius;
            if (((Vector2)transform.position - player.Position).sqrMagnitude <= touch * touch)
            {
                player.TakeDamage(damage, source);
                onHit?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
