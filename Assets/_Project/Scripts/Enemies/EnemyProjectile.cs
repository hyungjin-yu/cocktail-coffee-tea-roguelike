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

        PlayerController player;
        Vector2 velocity;
        float damage;
        float life;
        System.Action onHit;

        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage,
            PlayerController player, Transform parent, string spriteResourcePath = null,
            System.Action onHit = null)
        {
            var go = new GameObject("EnemyProjectile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            bool isArt = spriteResourcePath != null && spriteResourcePath.StartsWith(ArtPrefix);
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
            go.transform.localScale = Vector3.one * (visualScale * 0.5f); // 모든 적/보스 탄의 시각 크기만 절반.

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

            var p = go.AddComponent<EnemyProjectile>();
            p.player = player;
            p.velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.zero;
            p.damage = damage;
            p.life = Lifetime;
            p.onHit = onHit;

            // 물방울/얼음조각 아트는 위쪽이 뾰족하고 아래쪽(로컬 -Y)이 두꺼운 모양이라,
            // 두꺼운 쪽이 진행 방향(플레이어 쪽)을 향하도록 발사 방향으로 회전시킨다.
            if (p.velocity.sqrMagnitude > 0.0001f)
            {
                // 04 §8 새 VFX 탄은 오른쪽(+X)이 진행 방향 — +90° 보정은 기존 아이콘(아래가 앞)에만.
                float angle = Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg + (art != null ? 0f : 90f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(velocity * Time.deltaTime);

            if (player == null || player.IsDead) return;

            float touch = HitRadius + PlayerController.Radius;
            if (((Vector2)transform.position - player.Position).sqrMagnitude <= touch * touch)
            {
                player.TakeDamage(damage);
                onHit?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
