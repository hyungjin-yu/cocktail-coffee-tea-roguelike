using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 총/활 시작 무기. 레이피어(근접 베기)와 달리 원거리에서 가장 가까운 몹을 조준 사격한다.
    /// 근접보다 넉넉한 사거리로 "안전 거리에서 싸운다"는 느낌을 낸다.
    /// 연속 공격 카드(더블 스트레인/재우림 등, stats.extraThrusts)는 근접에서는 반원을 여러 번
    /// 베지만, 원거리에서는 부채꼴로 동시에 여러 발을 쏜다 — 한 발씩만 나가면 근접의 반원 히트(여러 마리
    /// 동시 타격)보다 몹을 처리하는 속도가 크게 밀려서 몹이 순식간에 쌓이는 문제가 있었음 (2026-09-22 피드백).
    /// 관통 카드(스트레이너 등, stats.pierceCount)는 발사체 하나가 진행 경로의 여러 몹을 연달아 때리게 한다.
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
        string shotSfxId = "shot_gun";
        /// <summary>04 §8: 총=bullet, 활=arrow.</summary>
        string projectileArtId = "bullet";

        public void Init(PlayerStats stats, PlayerController player, WeaponKind weapon = WeaponKind.Gun)
        {
            this.stats = stats;
            this.player = player;
            shotSfxId = weapon == WeaponKind.Bow ? "shot_bow" : "shot_gun";
            projectileArtId = weapon == WeaponKind.Bow ? "arrow" : "bullet";
        }

        float AcquireRange => stats.Range + RangedBonusRange + AcquireMargin;

        void Update()
        {
            if (player == null || player.IsDead || Time.timeScale <= 0f) return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            var target = Enemy.FindNearest(player.Position, AcquireRange);
            if (target == null) return;

            cooldown = stats.AttackInterval;
            Fire(target);
        }

        void Fire(Enemy target)
        {
            player.SetFacing(target.transform.position.x);
            player.Visual?.PlayAction("ranged"); // 04 §6 ranged(12fps 1회)
            // 장착 무기 아트의 muzzle anchor가 있으면 그 월드 위치에서 발사(시각 정렬, 좌우 반전 반영). 없으면 몸 중심.
            Vector2 origin = player.Position;
            if (player.Visual != null && player.Visual.TryGetMuzzleWorld(player.Facing, out var muzzle))
            {
                // 표적이 총구보다 앞에 있을 때만 총구에서 쏜다. 총 2배(2026-10-08) 뒤 총구가 몸 앞 약 1 world라
                // 0.5~0.7 거리 표적은 총구 뒤에 있어 탄이 뒤로 날아갔다 — 그때는 몸 중심에서 쏜다(피해·속도·사거리 불변).
                if ((target.transform.position.x - muzzle.x) * player.Facing > 0f) origin = muzzle;
            }
            Vector2 baseDir = ((Vector2)target.transform.position - origin).normalized;
            AudioService.PlaySfx(shotSfxId, origin);

            int count = 1 + stats.extraThrusts;
            float damage = stats.Damage;
            int pierce = stats.pierceCount;
            if (count <= 1)
            {
                PlayerProjectile.Spawn(origin, baseDir, ProjectileSpeed, damage, pierce, transform.parent, projectileArtId, player, true);
                FireFinalSides(origin, baseDir, 0f, damage);
                return;
            }

            // 부채꼴로 동시에 흩뿌린다 — 뭉쳐있는 몹을 한 번에 여러 마리 맞힐 수 있게
            float spread = Mathf.Min(MaxSpreadDegrees, SpreadDegreesPerShot * (count - 1));
            float step = spread / (count - 1);
            float startAngle = -spread * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var dir = Quaternion.Euler(0f, 0f, startAngle + step * i) * baseDir;
                PlayerProjectile.Spawn(origin, dir, ProjectileSpeed, damage, pierce, transform.parent, projectileArtId, player, true);
            }
            FireFinalSides(origin, baseDir, spread, damage);
        }

        void FireFinalSides(Vector2 origin, Vector2 direction, float spread, float damage)
        {
            if (!player.FinalForms.Has(AttackFinalForm.Spread)) return;
            float outside = spread * .5f + SpreadDegreesPerShot;
            foreach (float angle in new[] { -outside, outside })
                PlayerProjectile.Spawn(origin, Quaternion.Euler(0f, 0f, angle) * direction, ProjectileSpeed,
                    damage * .6f, stats.pierceCount, transform.parent, projectileArtId, player, false);
        }
    }
}
