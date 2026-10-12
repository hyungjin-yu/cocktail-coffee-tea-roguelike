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
            if (excluded != null) p.alreadyHit.Add(excluded);
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
                enemy.TakeDamage(hidden != null ? hidden.ModifyDirectDamage(enemy, damage) : damage);
                hidden?.AfterDirectHit(enemy);
                alreadyHit.Add(enemy);
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
}
