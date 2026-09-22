using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>런 시간에 맞춰 중간보스 → 최종보스를 등장시킨다. 보스는 한 번에 한 마리만.</summary>
    public class BossDirector : MonoBehaviour
    {
        public class Spec
        {
            public float time;
            public EnemyDef def;
            public float hpScale = 1f;
        }

        /// <summary>가안 스케줄. 기획서의 1런 15~20분보다 짧게 잡은 MVP 검증용 (8분).</summary>
        public static List<Spec> DefaultSchedule() => new List<Spec>
        {
            new Spec { time = 150f, def = EnemyDef.GrinderGolem, hpScale = 1f },
            new Spec { time = 300f, def = EnemyDef.GrinderGolem, hpScale = 1.8f },
            new Spec { time = 480f, def = EnemyDef.DifficultCustomer, hpScale = 1f },
        };

        GameManager game;
        List<Spec> schedule;
        int next;

        public void Init(GameManager game, List<Spec> schedule = null)
        {
            this.game = game;
            this.schedule = schedule ?? DefaultSchedule();
        }

        void Update()
        {
            if (game == null || game.State != GameState.Playing || game.Player.IsDead) return;
            if (next >= schedule.Count || game.ActiveBoss != null) return;

            if (game.RunTime >= schedule[next].time)
                SpawnNow(schedule[next++]);
        }

        public Enemy SpawnNow(Spec spec)
        {
            // 벨트스크롤 시점: 보스도 화면 좌/우 바깥에서 걸어들어온다
            float side = Random.value < 0.5f ? -1f : 1f;
            var pos = BeltScroll.EdgePosition(Camera.main, side, Balance.SpawnEdgeMargin);
            var boss = Enemy.SpawnAt(pos, spec.def, spec.hpScale, game.Player, game.transform);

            if (spec.def.boss == BossKind.Final)
            {
                boss.gameObject.AddComponent<CustomerBossDriver>().Init(boss, game);
                game.Hud.ShowBanner("진상 손님 등장!", "최종보스를 처치하면 클리어", 3f);
            }
            else
            {
                boss.gameObject.AddComponent<ChargerDriver>().Init(boss);
                game.Hud.ShowBanner($"중간보스 출현: {spec.def.name}", "붉게 깜빡이면 돌진 직전! 옆으로 피하세요", 3f);
            }

            game.ActiveBoss = boss;
            return boss;
        }
    }
}
