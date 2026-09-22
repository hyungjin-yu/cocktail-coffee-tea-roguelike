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
        const float Lifetime = 2f;
        const float HitRadius = 0.18f;

        readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();
        Vector2 velocity;
        float damage;
        float life;
        int pierceRemaining;

        public static void Spawn(Vector2 position, Vector2 direction, float speed, float damage, int pierceCount,
            Transform parent)
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
            p.pierceRemaining = Mathf.Max(0, pierceCount);
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
                if (!hit.TryGetComponent<Enemy>(out var enemy) || alreadyHit.Contains(enemy)) continue;

                enemy.TakeDamage(damage);
                alreadyHit.Add(enemy);

                if (pierceRemaining <= 0)
                {
                    Destroy(gameObject);
                    return;
                }
                pierceRemaining--;
            }
        }
    }
}
