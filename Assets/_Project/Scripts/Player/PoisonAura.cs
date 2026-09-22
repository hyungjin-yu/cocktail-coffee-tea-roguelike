using UnityEngine;

namespace CCT
{
    /// <summary>차 스킬(독). poisonLevel 이 0보다 커지면 주기적으로 주변에 독 피해를 준다. FlameBurst와 같은 구조, 다른 컬러.</summary>
    public class PoisonAura : MonoBehaviour
    {
        PlayerStats stats;
        PlayerController player;
        float timer;

        public void Init(PlayerStats stats, PlayerController player)
        {
            this.stats = stats;
            this.player = player;
        }

        float Interval => Mathf.Max(1.0f, 2.6f - 0.2f * stats.poisonLevel);
        float Radius => 1.6f + 0.25f * stats.poisonLevel;
        float Damage => (4f + 3f * stats.poisonLevel) * (1f + stats.DamageBonus);

        void Update()
        {
            if (player == null || player.IsDead || stats.poisonLevel <= 0) return;

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

            FadeVfx.Spawn(SpriteFactory.Circle, player.Position, 0f,
                Vector2.one * Radius * 0.6f, Vector2.one * Radius * 2f,
                new Color(0.55f, 0.25f, 0.85f, 0.5f), 0.35f, 1);
        }
    }
}
