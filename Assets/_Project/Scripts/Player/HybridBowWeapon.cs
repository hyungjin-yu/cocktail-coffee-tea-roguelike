using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 07:118 "티니 보우" — 레이피어 시작(칵테일3+차3 획득)으로 해금하는 하이브리드 보조 무기.
    /// 레이피어는 그대로 두고 이 컴포넌트를 Player에 추가로 붙인다. 자체 사거리/피해/간격 공식을
    /// 쓰며, extraThrust(동시발사)와 원거리 관통 카드의 영향을 받지 않는다(항상 1발/관통1).
    /// </summary>
    public class HybridBowWeapon : MonoBehaviour
    {
        const float FirstDelay = 0.2f;
        const float BaseInterval = 1.4f;
        const float MinInterval = 0.35f;
        const float AcquireRangeBonus = 3.6f;
        const float DamageMultiplier = 0.6f;
        const float ProjectileSpeed = 9f;
        const int Pierce = 1;

        PlayerStats stats;
        PlayerController player;
        float cooldown = FirstDelay;

        public void Init(PlayerStats stats, PlayerController player)
        {
            this.stats = stats;
            this.player = player;
            player.Visual?.AttachSecondaryWeaponArt("weapon_tea_bow"); // 09 장착 보조 활 그림
        }

        void Update()
        {
            if (player == null || player.IsDead || Time.timeScale <= 0f) return;

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;
            // 07:119 "주문 부정은 보조 활만 정지" — 화염/독과 같은 패턴으로 이 컴포넌트만 멈춘다.
            if (stats.HybridLocked) return;

            // 07:119 "현재 유효한 목표가 없으면 대기, 대상 생기면 발사" — cooldown은 계속 0 이하로 머물고,
            // 대상이 생기는 순간 바로 발사된다(RapierWeapon/PlayerProjectileWeapon과 같은 패턴).
            float range = stats.Range + AcquireRangeBonus;
            var target = Enemy.FindNearest(player.Position, range);
            if (target == null) return;

            cooldown = Mathf.Max(MinInterval, BaseInterval * stats.CooldownScale / (1f + stats.AttackSpeedBonus)); // 69 음료 재사용 시간 감소
            Fire(target);
        }

        void Fire(Enemy target)
        {
            player.Visual?.PlayHybridDraw(); // 2026-10-09: 등에 멘 활을 앞 손으로 — 아래 발사 지점도 잡은 활의 화살 anchor
            // 장착 보조 활(weapon_tea_bow)의 arrow anchor에서 발사(시각 정렬). 없으면 몸 중심.
            Vector2 origin = player.Visual != null && player.Visual.TryGetSecondaryLaunchWorld(out var launch)
                ? launch : player.Position;
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            float damage = stats.Damage * DamageMultiplier;
            AudioService.PlaySfx("shot_hybrid", origin);
            PlayerProjectile.Spawn(origin, dir, ProjectileSpeed, damage, Pierce, transform.parent, "tea_arrow");
            if (player.Hidden.Has(HiddenSkill.TiniMaster)) // 41 티니 보우 마스터: 같은 피해의 화살 +1(8도 벌어짐)
                PlayerProjectile.Spawn(origin, (Vector2)(Quaternion.Euler(0f, 0f, 8f) * dir), ProjectileSpeed, damage, Pierce,
                    transform.parent, "tea_arrow");
        }
    }
}
