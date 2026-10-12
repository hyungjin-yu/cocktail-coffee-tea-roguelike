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

        /// <summary>
        /// 가안 스케줄. 기획서 10장의 중간보스 A → B → 강화 보스 → 진상 손님 순서를 그대로 따른다.
        /// 보스가 2종 늘어난 만큼 기존(8분)보다 1런이 길어졌다 — 밸런스는 플레이 테스트 후 조정.
        /// </summary>
        public static List<Spec> DefaultSchedule() => new List<Spec>
        {
            new Spec { time = 150f, def = EnemyDef.GrinderGolem, hpScale = 1f },
            new Spec { time = 320f, def = EnemyDef.DripServerSpecter, hpScale = 1f },
            new Spec { time = 500f, def = EnemyDef.EspressoLeviathan, hpScale = 1f },
            new Spec { time = 680f, def = EnemyDef.DifficultCustomer, hpScale = 1f },
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
            // 벨트스크롤 시점: 보스도 화면 좌/우 바깥에서 걸어들어온다. 06:98 — 보스 위치는 시드에서
            // 파생된 독립 스트림을 쓴다.
            float side = game.BossPositionRng.Value() < 0.5f ? -1f : 1f;
            var pos = BeltScroll.EdgePosition(Camera.main, side, Balance.SpawnEdgeMargin);
            var boss = Enemy.SpawnAt(pos, spec.def, spec.hpScale, game.Player, game.transform);

            if (spec.def.boss == BossKind.Final)
            {
                boss.gameObject.AddComponent<CustomerBossDriver>().Init(boss, game);
                game.Hud.ShowBanner("진상 손님 등장!", "최종보스를 처치하면 클리어", 3f);
            }
            else if (spec.def == EnemyDef.EspressoLeviathan)
            {
                boss.gameObject.AddComponent<EspressoLeviathanDriver>().Init(boss, game);
                game.Hud.ShowBanner("강화 보스 출현: 에스프레소 머신 리바이어던", "처치하면 스페셜티(영구 재화) 획득!", 3f);
            }
            else if (spec.def == EnemyDef.DripServerSpecter)
            {
                boss.gameObject.AddComponent<DripSpecterDriver>().Init(boss, game);
                game.Hud.ShowBanner($"중간보스 출현: {spec.def.name}", "장판을 피해 안전지대를 찾으세요", 3f);
            }
            else
            {
                boss.gameObject.AddComponent<ChargerDriver>().Init(boss);
                game.Hud.ShowBanner($"중간보스 출현: {spec.def.name}", "붉게 깜빡이면 돌진 직전! 옆으로 피하세요", 3f);
            }

            game.ActiveBoss = boss;
            game.Player?.HiddenRuntime?.OnBossSpawned(); // 41 삼색 원두
            AudioService.PlaySfx("boss_warning"); // 09: "보스 입장/위험" — 중앙 고정(worldPos 없음)
            AudioService.SetMusicState(AudioService.MusicState.Boss); // 09:135 "보스 생존은 boss"
            return boss;
        }
    }
}
