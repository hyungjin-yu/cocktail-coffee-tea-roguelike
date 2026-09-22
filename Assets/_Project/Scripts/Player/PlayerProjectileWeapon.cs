using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 총/활 시작 무기. 레이피어(근접 찌르기)와 달리 원거리에서 가장 가까운 몹을 조준 사격한다.
    /// 근접보다 넉넉한 사거리로 "안전 거리에서 싸운다"는 느낌을 낸다.
    /// 연속 공격 카드(플람베/두 번째 우림 등, stats.extraThrusts)는 근접에서는 같은 자리를 여러 번
    /// 찌르지만, 원거리에서는 부채꼴로 동시에 여러 발을 쏜다 — 한 발씩만 나가면 근접의 박스 히트(여러 마리
    /// 동시 타격)보다 몹을 처리하는 속도가 크게 밀려서 몹이 순식간에 쌓이는 문제가 있었음 (2026-09-22 피드백).
    /// </summary>
    public class PlayerProjectileWeapon : MonoBehaviour
    {
        const float AcquireMargin = 0.6f;
        const float RangedBonusRange = 3f;
        const float ProjectileSpeed = 9f;
        /// <summary>발사 수 1발 늘 때마다 넓어지는 부채꼴 각도, 상한 있음.</summary>
        const float SpreadDegreesPerShot = 10f;
        const float MaxSpreadDegrees = 60f;

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
            Vector2 baseDir = ((Vector2)target.transform.position - origin).normalized;
            player.SetFacing(target.transform.position.x);

            int count = 1 + stats.extraThrusts;
            float damage = stats.Damage;
            if (count <= 1)
            {
                PlayerProjectile.Spawn(origin, baseDir, ProjectileSpeed, damage, transform.parent);
                return;
            }

            // 부채꼴로 동시에 흩뿌린다 — 뭉쳐있는 몹을 한 번에 여러 마리 맞힐 수 있게
            float spread = Mathf.Min(MaxSpreadDegrees, SpreadDegreesPerShot * (count - 1));
            float step = spread / (count - 1);
            float startAngle = -spread * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var dir = Quaternion.Euler(0f, 0f, startAngle + step * i) * baseDir;
                PlayerProjectile.Spawn(origin, dir, ProjectileSpeed, damage, transform.parent);
            }
        }
    }
}
