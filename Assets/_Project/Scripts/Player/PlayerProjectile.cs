using UnityEngine;

namespace CCT
{
    /// <summary>총/활(원거리 무기)이 쏘는 단순 직선 발사체. 처음 맞힌 몹에게 피해를 주고 사라진다.</summary>
    public class PlayerProjectile : MonoBehaviour
    {
        const float Lifetime = 2f;
        const float HitRadius = 0.18f;

        Vector2 velocity;
        float damage;
        float life;

        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage, Transform parent)
        {
            var go = new GameObject("PlayerProjectile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (HitRadius * 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = new Color(1f, 0.9f, 0.5f, 0.95f);
            sr.sortingOrder = 3;

            var p = go.AddComponent<PlayerProjectile>();
            p.velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.zero;
            p.damage = damage;
            p.life = Lifetime;
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

            var hits = Physics2D.OverlapCircleAll(transform.position, HitRadius);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<Enemy>(out var enemy))
                {
                    enemy.TakeDamage(damage);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
