using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 총/활 시작 무기. 레이피어(근접 찌르기)와 달리 원거리에서 가장 가까운 몹을 조준 사격한다.
    /// 근접보다 넉넉한 사거리로 "안전 거리에서 싸운다"는 느낌을 낸다. 연속 공격(찌르기+1회 카드)은
    /// 적용하지 않는다 — 근접의 "콤보", 원거리의 "단발 정밀 사격"으로 무기군을 차별화.
    /// </summary>
    public class PlayerProjectileWeapon : MonoBehaviour
    {
        const float AcquireMargin = 0.6f;
        const float RangedBonusRange = 3f;
        const float ProjectileSpeed = 9f;

        PlayerStats stats;
        PlayerController player;
        float cooldown;

        public void Init(PlayerStats stats, PlayerController player)
        {
            this.stats = stats;
            this.player = player;
        }

        float AcquireRange => stats.Range + RangedBonusRange + AcquireMargin;

        void Update()
        {
            if (player == null || player.IsDead) return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            var target = Enemy.FindNearest(player.Position, AcquireRange);
            if (target == null) return;

            cooldown = stats.AttackInterval;
            Fire(target);
        }

        void Fire(Enemy target)
        {
            Vector2 origin = player.Position;
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            player.SetFacing(target.transform.position.x);

            PlayerProjectile.Spawn(origin, dir, ProjectileSpeed, stats.Damage, transform.parent);
        }
    }
}
