using UnityEngine;

namespace CCT
{
    /// <summary>올리브 바머가 자폭 후 남기는 화염(알콜) 장판. 범위 안에 있으면 지속 피해.
    /// 실제 피해는 PlayerZoneDamage를 거친다 — 겹친 장판끼리는 가장 높은 DPS 하나만 적용된다(06:23).</summary>
    public class BurstZone : MonoBehaviour
    {
        PlayerController player;
        float radius;
        float damagePerSecond;
        float remaining;
        float tickTimer;

        public static void Spawn(Vector2 position, float radius, float damagePerSecond, float seconds,
            PlayerController player, Transform parent)
        {
            var go = new GameObject("BurstZone");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = new Color(1f, 0.5f, 0.15f, 0.35f);
            sr.sortingOrder = 1;

            var zone = go.AddComponent<BurstZone>();
            zone.player = player;
            zone.radius = radius;
            zone.damagePerSecond = damagePerSecond;
            zone.remaining = seconds;
            zone.tickTimer = Balance.ZoneTickInterval; // 첫 틱은 생성 0.4초 후 (06:23)
        }

        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (player == null || player.IsDead) return;

            tickTimer -= Time.deltaTime;
            if (tickTimer > 0f) return;
            tickTimer = Balance.ZoneTickInterval;

            if (Vector2.Distance(transform.position, player.Position) <= radius)
                PlayerZoneDamage.RequestTick(damagePerSecond);
        }
    }
}
