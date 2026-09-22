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

            Summon();
        }

        void Summon()
        {
            var def = enemy.Def;
            if (def.summonDef == null || Enemy.Active.Count >= Balance.MaxAliveEnemies) return;

            float hpScale = game != null ? Balance.EnemyHpScale(game.RunTime) : 1f;
            var offset = Random.insideUnitCircle.normalized * (def.radius + 1f);
            Enemy.SpawnAt((Vector2)enemy.transform.position + offset, def.summonDef, hpScale, player,
                enemy.transform.parent);
        }
    }
}
