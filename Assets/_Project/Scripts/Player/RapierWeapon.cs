using System.Collections;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 바텐더 시작 무기. 바라보는 좌/우 방향으로 가장 가까운 몹을 자동 횡베기.
    /// 판정은 항상 VFX와 같은 모양·크기를 쓴다 — 예전엔 판정(레인 전체를 덮는 직사각형)과
    /// VFX(발밑 얇은 선)가 서로 달라서 "VFX 밖에서도 맞는다"는 버그 리포트가 있었음(2026-09-25).
    /// 이후 사용자 요청으로 찌르기 대신 반원 슬래시로 재설계 — 판정도 같은 반원을 쓴다.
    /// </summary>
    public class RapierWeapon : MonoBehaviour
    {
        const float ChainDelay = 0.09f;
        const float AcquireMargin = 0.6f;

        PlayerStats stats;
        PlayerController player;
        float cooldown;
        bool attacking;
        string swingSfxId = "swing_rapier";
        bool finalChainHit;
        bool thrustStyle;
        int swingIndex;

        public void Init(PlayerStats stats, PlayerController player, WeaponKind weapon = WeaponKind.Rapier)
        {
            this.stats = stats;
            this.player = player;
            // 09: swing_rapier/swing_sword — 레이피어·검이 메커닉은 같아도 소리는 다르다.
            swingSfxId = weapon == WeaponKind.Sword ? "swing_sword" : "swing_rapier";
            thrustStyle = weapon != WeaponKind.Sword;
        }

        void Update()
        {
            if (player == null || player.IsDead || Time.timeScale <= 0f) return;

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
                if (i == 0) AudioService.PlaySfx(swingSfxId, player.Position); // 09: "레이피어/검 첫타"만
                finalChainHit = i == thrusts - 1 && player.FinalForms.Has(AttackFinalForm.Chain);
                Slash(player.Facing);
                finalChainHit = false;
                if (i < thrusts - 1) yield return new WaitForSeconds(ChainDelay);
            }
            attacking = false;
        }

        /// <summary>바라보는 방향 반원(180°) 안, 사거리 이내의 적을 전부 벤다. 등 뒤(반대쪽 반원)는
        /// 맞지 않는다 — 내적 부호로 앞/뒤를 가른다(각도 계산 없이 반원 절반평면과 동치).</summary>
        void Slash(float facing)
        {
            float range = stats.Range * (finalChainHit ? 1.25f : 1f);
            Vector2 origin = player.Position;
            Vector2 forwardDir = new Vector2(facing, 0f);

            var hits = Physics2D.OverlapCircleAll(origin, range);
            float damage = stats.Damage * (finalChainHit ? 1.5f : 1f);
            var hitEnemies = new System.Collections.Generic.HashSet<Enemy>();
            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<Enemy>(out var enemy) || enemy.Invulnerable) continue;
                if (Vector2.Dot((Vector2)enemy.transform.position - origin, forwardDir) < 0f) continue;
                if (enemy.IsDead || !hitEnemies.Add(enemy) || enemy.RunPlayer != player) continue;
                EnemyHitStatus.AttachDirect(enemy, player);
                var hidden = player.HiddenRuntime;
                enemy.TakeDamage(hidden != null ? hidden.ModifyDirectDamage(enemy, damage) : damage);
                hidden?.AfterDirectHit(enemy);
            }

            float angle = facing > 0f ? 0f : 180f;
            // 검(티 소믈리에)은 PixelLab 휘두르기 몸체 클립, 그 외는 04 §6 melee(찌르기 자세, 12fps 1회). 판정은 위에서 이미 끝났다.
            bool swing = !thrustStyle && player.Visual != null && player.Visual.PlaySwordSwing();
            if (!swing) player.Visual?.PlayAction("melee");
            if (swing) StartCoroutine(SlashVisualAtImpact(forwardDir, range, angle));
            else SpawnSlashVisual(forwardDir, range, angle);
        }

        /// <summary>2026-10-09 사용자 지시(06/10의 플레이어 전방 범위 시각표시 대체): 파란 반원/바닥 범위 채움을 없애고, 타격 순간 공격
        /// 방향으로 짧게 휘두르는 얇은 반월 베기만 보인다. 판정 모양·사거리·피해는 위 Slash 그대로다(시험 유지).
        /// 검은 크림색 반월 베기, 레이피어는 앞으로 길고 얇게 눌린 찌르기 궤적으로 찌르기/베기 차이를 남긴다.
        /// 잔상은 플레이어 아래에 붙어 런과 함께 정리되고, 일시정지 중에는 게임 시간으로 멈춘다.</summary>
        void SpawnSlashVisual(Vector2 forwardDir, float range, float angle)
        {
            var weapon = player.Visual != null ? player.Visual.Weapon : null;
            Vector2 hand = weapon != null && weapon.transform.parent != null ? (Vector2)weapon.transform.parent.position : player.Position;
            bool alternate = (swingIndex++ & 1) == 1; // 연속타는 위→아래/아래→위로 번갈아 휘두른다
            if (thrustStyle)
            {
                // 레이피어 찌르기: 예전 melee_streak는 크게 늘어나 두꺼운 회색 막대로 보였다(2026-10-09 실제 720 녹화) — 대신 앞으로 길고
                // 얇게 눌린 반월(찌르기 궤적)을 뻗은 팔 높이(melee 02 손 anchor ≈ 대기 손 +0.4)에 둔다.
                Attach(FadeVfx.Spawn(SpriteFactory.Crescent, hand + forwardDir * (range * 0.5f) + Vector2.up * 0.4f, angle,
                    new Vector2(range * 0.7f, range * 0.28f * (alternate ? -1f : 1f)), new Vector2(range * 0.8f, range * 0.3f * (alternate ? -1f : 1f)),
                    new Color(1f, 0.93f, 0.88f, 0.9f), 0.13f, SortOrders.AirVfx));
            }
            else
            {
                Attach(FadeVfx.Spawn(SpriteFactory.Crescent, hand + forwardDir * (range * 0.3f) + Vector2.up * 0.15f, angle,
                    new Vector2(range * 0.5f, range * 0.95f * (alternate ? -1f : 1f)), new Vector2(range * 0.56f, range * 1.05f * (alternate ? -1f : 1f)),
                    new Color(1f, 0.95f, 0.84f, 0.95f), 0.16f, SortOrders.AirVfx));
            }
        }

        /// <summary>휘두르기 클립의 대각선 베기 프레임에 반월을 맞춘다(게임 시간 — 일시정지 중엔 멈추고, 사망/종료면 생략).</summary>
        IEnumerator SlashVisualAtImpact(Vector2 forwardDir, float range, float angle)
        {
            yield return new WaitForSeconds(PlayerVisual.SwordSwingImpactDelay);
            if (player == null || player.IsDead) yield break;
            SpawnSlashVisual(forwardDir, range, angle);
        }

        void Attach(FadeVfx vfx)
        {
            if (vfx != null && player != null) vfx.transform.SetParent(player.transform, true);
        }
    }
}
