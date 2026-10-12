using UnityEngine;

namespace CCT
{
    /// <summary>슈가 큐브 바머가 자폭 후 남기는 끈적한 설탕 장판. 범위 안에 있으면 이동속도 둔화 (피해 대신 제어기, BurstZone과 차별화).</summary>
    public class SlowZone : MonoBehaviour
    {
        /// <summary>살아 있는 개수(10 §3 고정 부하 시나리오 계측용). 활성/비활성 시점에만 갱신한다.</summary>
        public static int ActiveCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveCount() => ActiveCount = 0;

        void OnEnable() => ActiveCount++;
        void OnDisable() => ActiveCount--;

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
            var sr = go.AddComponent<SpriteRenderer>();
            BurstZone.ApplyZoneVisual(go, sr, "sugar_pool", radius, new Color(0.9f, 0.85f, 0.55f, 0.4f), seconds); // 04 §8 sugar_pool → 43 v2

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
