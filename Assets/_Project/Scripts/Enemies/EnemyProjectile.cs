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
            PlayerController player, Transform parent, string spriteResourcePath = null)
        {
            var go = new GameObject("EnemyProjectile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var icon = string.IsNullOrEmpty(spriteResourcePath) ? null : Resources.Load<Sprite>(spriteResourcePath);
            // 실제 아이콘은 캔버스 여백 때문에 작아 보여서 시각적으로만 키운다 (히트 판정은 HitRadius 그대로).
            float visualScale = icon != null ? Balance.EnemyProjectileIconVisualScale : 1f;
            go.transform.localScale = Vector3.one * (HitRadius * 2f * visualScale);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = icon != null ? icon : SpriteFactory.Circle;
            sr.color = icon != null ? Color.white : new Color(0.65f, 0.88f, 1f, 0.95f);
            sr.sortingOrder = 2;

            var p = go.AddComponent<EnemyProjectile>();
            p.player = player;
            p.velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.zero;
            p.damage = damage;
            p.life = Lifetime;

            // 물방울/얼음조각 아트는 위쪽이 뾰족하고 아래쪽(로컬 -Y)이 두꺼운 모양이라,
            // 두꺼운 쪽이 진행 방향(플레이어 쪽)을 향하도록 발사 방향으로 회전시킨다.
            if (p.velocity.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg + 90f;
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
                Destroy(gameObject);
            }
        }
    }
}
