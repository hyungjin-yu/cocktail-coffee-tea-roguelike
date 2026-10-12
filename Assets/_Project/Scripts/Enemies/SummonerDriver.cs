using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 소환형(Summoner) 전용. def.speed=0 이라 기본 추적 로직만으로도 제자리에 머무르므로
    /// 이동을 따로 손댈 필요 없이, 주기적으로 def.summonDef 를 주변에 소환하는 타이머만 담당한다.
    /// </summary>
    public class SummonerDriver : MonoBehaviour
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
            timer = enemy.Def.summonInterval * 0.5f;
        }

        void Update()
        {
            if (enemy == null || enemy.IsDead) return;
            if (game != null && game.State != GameState.Playing) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = enemy.Def.summonInterval;

            if (enemy.PlayAction("action") && player != null) // 04 §6 S 소환 action
                enemy.FaceTowards(player.Position.x, enemy.Animator.Current.Duration);
            Summon();
        }

        void Summon()
        {
            var def = enemy.Def;
            if (def.summonDef == null || Enemy.Active.Count >= Balance.MaxAliveEnemies) return;

            float hpScale = game != null ? Balance.EnemyHpScale(game.RunTime) : 1f;
            var offset = Random.insideUnitCircle.normalized * (def.radius + 1f);
            var pos = (Vector2)enemy.transform.position + offset;
            pos.y = Mathf.Clamp(pos.y, Balance.DepthMin, Balance.DepthMax); // 06:22 — y를 레인으로 제한
            Enemy.SpawnAt(pos, def.summonDef, hpScale, player, enemy.transform.parent);
            // 04 §8 summon_ring — 칵테일 청색/차 녹색 tint(소환 몹의 재료 종류).
            string tint = def.dropType == IngredientType.Tea ? "tea" : "cocktail";
            ArtVfx.GroundRing("summon_ring", pos, 0.6f, 0.5f, Color.white, true, SortOrders.GroundZone, tint);
            // 소환 시전 지점: 몸체 action 프레임의 summon anchor(지팡이 끝·잎)에 같은 tint의 작은 고리를 띄운다.
            // 소환 몹 위치·소환 시점은 그대로(시각 정렬만). anchor가 없으면 생략.
            if (enemy.TryGetActionAnchorWorld("action", "summon", out var castPoint))
                CastFlash(castPoint, tint);
        }

        static void CastFlash(Vector2 castPoint, string tintVariant)
        {
            var art = ArtLibrary.Get("summon_ring");
            var tint = Color.white;
            if (art != null && art.TryGetTint(tintVariant, out var variant)) tint *= variant;
            ArtVfx.Spawn("summon_ring", castPoint, 0f, 0.45f, 0.35f, tint, SortOrders.AirVfx, 1.6f);
        }
    }
}
