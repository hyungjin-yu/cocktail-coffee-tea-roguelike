using UnityEngine;

namespace CCT
{
    /// <summary>슈가 큐브 바머가 자폭 후 남기는 끈적한 설탕 장판. 범위 안에 있으면 이동속도 둔화 (피해 대신 제어기, BurstZone과 차별화).</summary>
    public class SlowZone : MonoBehaviour
    {
        PlayerController player;
        float radius;
        float multiplier;
        float remaining;

        public static void Spawn(Vector2 position, float radius, float multiplier, float seconds,
            PlayerController player, Transform parent)
        {
            var go = new GameObject("SlowZone");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = new Color(0.9f, 0.85f, 0.55f, 0.4f);
            sr.sortingOrder = 1;

            var zone = go.AddComponent<SlowZone>();
            zone.player = player;
            zone.radius = radius;
            zone.multiplier = multiplier;
            zone.remaining = seconds;
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

            if (Vector2.Distance(transform.position, player.Position) <= radius)
                player.Stats.ApplySlow(0.25f, multiplier); // 매 프레임 짧게 갱신 → 범위 벗어나면 자연히 풀림
        }
    }
}
