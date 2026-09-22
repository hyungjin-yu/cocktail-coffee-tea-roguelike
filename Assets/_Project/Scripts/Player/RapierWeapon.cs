using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 바텐더 시작 무기. 바라보는 좌/우 방향으로 가장 가까운 몹을 자동 찌르기.
    /// 벨트스크롤 시점이라 판정 높이는 레인(깊이) 폭 전체를 넉넉히 덮는다.
    /// </summary>
    public class RapierWeapon : MonoBehaviour
    {
        const float ChainDelay = 0.09f;
        const float AcquireMargin = 0.6f;
        const float HeightPadding = 0.6f;

        PlayerStats stats;
        PlayerController player;
        float cooldown;
        bool attacking;

        public void Init(PlayerStats stats, PlayerController player)
        {
            this.stats = stats;
            this.player = player;
        }

        void Update()
        {
            if (player == null || player.IsDead) return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0f || attacking) return;

            var target = Enemy.FindNearest(player.Position, stats.Range + AcquireMargin);
            if (target == null) return;

            cooldown = stats.AttackInterval;
            StartCoroutine(AttackRoutine());
        }

        IEnumerator AttackRoutine()
        {
            attacking = true;
            int thrusts = 1 + stats.extraThrusts;
            for (int i = 0; i < thrusts; i++)
            {
                var target = Enemy.FindNearest(player.Position, stats.Range + AcquireMargin);
                if (target == null) break;

                player.SetFacing(target.transform.position.x);
                Thrust(player.Facing);
                if (i < thrusts - 1) yield return new WaitForSeconds(ChainDelay);
            }
            attacking = false;
        }

        void Thrust(float facing)
        {
            float range = stats.Range;
            float bandHeight = Balance.DepthMax - Balance.DepthMin + HeightPadding;
            float bandCenterY = (Balance.DepthMin + Balance.DepthMax) * 0.5f;
            float forwardX = player.Position.x + facing * (range * 0.5f + PlayerController.Radius * 0.5f);

            var hits = Physics2D.OverlapBoxAll(new Vector2(forwardX, bandCenterY), new Vector2(range, bandHeight), 0f);
            float damage = stats.Damage;
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<Enemy>(out var enemy))
                    enemy.TakeDamage(damage);
            }

            float angle = facing > 0f ? 0f : 180f;
            Vector2 vfxCenter = new Vector2(forwardX, player.Position.y);
            FadeVfx.Spawn(SpriteFactory.Square, vfxCenter, angle,
                new Vector2(range, 0.22f), new Vector2(range, 0.04f),
                new Color(1f, 1f, 1f, 0.9f), 0.12f);
        }
    }
}
