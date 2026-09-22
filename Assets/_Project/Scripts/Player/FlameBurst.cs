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
        }

        float Interval => Mathf.Max(1.2f, 3.2f - 0.25f * stats.flameLevel);
        float Radius => 2.0f + 0.3f * stats.flameLevel;
        float Damage => (6f + 4f * stats.flameLevel) * (1f + stats.DamageBonus);

        void Update()
        {
            if (player == null || player.IsDead || stats.flameLevel <= 0) return;

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
                new Color(1f, 0.5f, 0.15f, 0.55f), 0.35f, 1);
        }
    }
}
