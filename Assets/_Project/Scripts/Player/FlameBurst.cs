using UnityEngine;

namespace CCT
{
    /// <summary>화염병 칵테일 스킬. flameLevel 이 0보다 커지면 주기적으로 주변에 화염 폭발.</summary>
    public class FlameBurst : MonoBehaviour
    {
        PlayerStats stats;
        PlayerController player;
        float timer;

        public void Init(PlayerStats stats, PlayerController player)
        {
            this.stats = stats;
            this.player = player;
            stats.Changed += OnStatsChanged;
        }

        void OnDestroy()
        {
            if (stats != null) stats.Changed -= OnStatsChanged;
        }

        /// <summary>레벨 추가는 남은 타이머를 새 간격 "이하로만" 줄인다 — 늘리지 않는다 (06:20).</summary>
        void OnStatsChanged() => timer = Mathf.Min(timer, Interval);

        float Interval => Mathf.Max(1.2f, 3.2f - 0.25f * stats.flameLevel);
        float Radius => 2.0f + 0.3f * stats.flameLevel;
        float Damage => (6f + 4f * stats.flameLevel) * (1f + stats.DamageBonus);

        void Update()
        {
            if (player == null || player.IsDead || stats.flameLevel <= 0) return;
            if (stats.FlameLocked) return; // 06:142 — 주문 부정으로 잠긴 동안은 타이머도 멈춘다

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Interval;

            var hits = Physics2D.OverlapCircleAll(player.Position, Radius);
            float damage = Damage;
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<Enemy>(out var enemy))
                    enemy.TakeDamage(damage);
            }

            // 중심에서 짧게 번쩍이는 코어 플래시 + 사방으로 튀는 불티 파티클을 겹쳐서
            // 단순 도형 하나보다 "타격감" 있게 보이도록 함.
            FadeVfx.Spawn(SpriteFactory.Circle, player.Position, 0f,
                Vector2.one * Radius * 0.5f, Vector2.one * Radius * 1.3f,
                new Color(1f, 0.55f, 0.2f, 0.6f), 0.22f, 1);

            var fireConfig = ParticleBurstConfig.Default;
            fireConfig.count = 22;
            fireConfig.minSpeed = 1.5f;
            fireConfig.maxSpeed = 1.2f + Radius * 1.6f;
            fireConfig.minLifetime = 0.3f;
            fireConfig.maxLifetime = 0.5f;
            fireConfig.minSize = 0.18f;
            fireConfig.maxSize = 0.34f;
            fireConfig.startColor = new Color(1f, 0.92f, 0.55f, 1f); // 밝은 노랑(불꽃 중심)
            fireConfig.endColor = new Color(0.95f, 0.25f, 0.05f, 0.6f); // 어두운 주황-빨강(꺼지는 불씨)
            fireConfig.gravityModifier = -0.35f; // 불꽃이라 살짝 위로 뜸
            fireConfig.shapeRadius = Radius * 0.15f;
            fireConfig.sortingOrder = 2;
            ParticleFx.SpawnBurst(player.Position, transform.parent, fireConfig);
        }
    }
}
