using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 투척형(Ranged)·탄막형(EliteBarrage) 공용 드라이버.
    /// keepDistance 를 유지하며(가까우면 물러나고 멀면 접근) 주기적으로 발사체를 쏜다.
    /// barrageCount==1 이면 조준 단발, 그 이상이면 원형으로 흩뿌리는 탄막.
    /// </summary>
    public class RangedAttackDriver : MonoBehaviour, IEnemyDriver
    {
        Enemy enemy;
        PlayerController player;
        GameManager game;
        float timer;

        public void Init(Enemy enemy, PlayerController player, GameManager game)
        {
            this.enemy = enemy;
            this.player = player;
            this.game = game;
            enemy.SetDriver(this);
            timer = enemy.Def.attackInterval * 0.5f; // 스폰하자마자 쏘지 않도록 약간의 텀
        }

        public bool TryGetVelocity(Vector2 selfPosition, Vector2 playerPosition, out Vector2 velocity)
        {
            var def = enemy.Def;
            Vector2 toPlayer = playerPosition - selfPosition;
            float dist = toPlayer.magnitude;

            if (dist > def.keepDistance * 1.15f) velocity = toPlayer.normalized * def.speed;
            else if (dist < def.keepDistance * 0.85f) velocity = -toPlayer.normalized * def.speed;
            else velocity = Vector2.zero;
            return true;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead || player == null || player.IsDead) return;
            if (game != null && game.State != GameState.Playing) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = enemy.Def.attackInterval;

            // 04 §6 T/E 투척 action(발사 시점은 로직 그대로). 쏘는 동안 플레이어 쪽을 보게 해 총구/손이 등 뒤에 오지 않게 한다.
            if (enemy.PlayAction("action")) enemy.FaceTowards(player.Position.x, enemy.Animator.Current.Duration);
            Fire();
        }

        void Fire()
        {
            var def = enemy.Def;
            Vector2 origin = EmitterOrigin(enemy);
            Vector2 toPlayer = player.Position - origin;
            Vector2 baseDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.right;

            int count = Mathf.Max(1, def.barrageCount);
            var parent = enemy.transform.parent;
            if (count == 1)
            {
                EnemyProjectile.Spawn(origin, baseDir, def.projectileSpeed, def.projectileDamage, player, parent,
                    def.projectileSpriteResourcePath);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = 360f / count * i;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
                EnemyProjectile.Spawn(origin, dir, def.projectileSpeed, def.projectileDamage, player, parent,
                    def.projectileSpriteResourcePath);
            }
        }

        /// <summary>몸체 action 프레임의 muzzle(T: 주전자·셰이커·다도 가디언) 또는 emitter(E/T: 마티니·밀크 포머) anchor
        /// 월드 위치. 없으면(아이콘 몹·anchor 없는 프레임) 기존대로 몸 중심.</summary>
        public static Vector2 EmitterOrigin(Enemy enemy)
        {
            if (enemy.TryGetActionAnchorWorld("action", "muzzle", out var world)) return world;
            enemy.TryGetActionAnchorWorld("action", "emitter", out world);
            return world;
        }
    }
}
