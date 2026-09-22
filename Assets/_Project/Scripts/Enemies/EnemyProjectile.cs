using UnityEngine;

namespace CCT
{
    /// <summary>투척형/탄막형 몹이 쏘는 단순 직선 발사체.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        const float Lifetime = 3f;
        const float HitRadius = 0.22f;

        PlayerController player;
        Vector2 velocity;
        float damage;
        float life;

        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage,
            PlayerController player, Transform parent)
        {
            var go = new GameObject("EnemyProjectile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (HitRadius * 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = new Color(0.65f, 0.88f, 1f, 0.95f);
            sr.sortingOrder = 2;

            var p = go.AddComponent<EnemyProjectile>();
            p.player = player;
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

            if (player == null || player.IsDead) return;

            float touch = HitRadius + PlayerController.Radius;
            if (((Vector2)transform.position - player.Position).sqrMagnitude <= touch * touch)
            {
                player.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
