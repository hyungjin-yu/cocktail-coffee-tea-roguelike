using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 런 시간에 따른 일반 몹 등장 테이블 (가안). 트리클 스폰·웨이브·"사장 나와!" 소환이 전부 공유한다.
    /// [[01_몹 디자인 - 칵테일 & 차]] 기준: MVP 필수는 라임웨지(돌진)+쉐이커 골렘(투척) 2종,
    /// 폭발/소환/엘리트는 그보다 늦게, 드물게 섞이도록 페이싱을 잡는다.
    /// </summary>
    public static class EnemyRoster
    {
        public static EnemyDef PickCommon(float runTime)
        {
            const float wImp = 1f;
            float wShaker = runTime >= 15f ? 0.4f : 0f;
            float wBrute = runTime >= 45f ? Mathf.Min(0.55f, 0.15f + (runTime - 45f) * 0.0015f) : 0f;
            float wBomber = runTime >= 60f ? 0.3f : 0f;
            float wConjuror = runTime >= 90f ? 0.15f : 0f;
            // 엘리트: 웨이브 후반에 드물게 단독 등장 컨셉이라 등장 시점도 늦고 가중치도 낮게
            float wMartini = runTime >= 120f ? 0.08f : 0f;

            float roll = Random.value * (wImp + wShaker + wBrute + wBomber + wConjuror + wMartini);
            if ((roll -= wMartini) < 0f) return EnemyDef.MartiniSpirit;
            if ((roll -= wConjuror) < 0f) return EnemyDef.SwirlConjuror;
            if ((roll -= wBomber) < 0f) return EnemyDef.OliveBomber;
            if ((roll -= wBrute) < 0f) return EnemyDef.Brute;
            if ((roll -= wShaker) < 0f) return EnemyDef.ShakerGolem;
            return EnemyDef.Imp;
        }

        /// <summary>아키타입에 맞는 행동 컴포넌트를 붙인다. Rush 는 기본 추적 그대로라 붙일 게 없다.</summary>
        public static void AttachBehavior(Enemy enemy, GameManager game, PlayerController player)
        {
            switch (enemy.Def.archetype)
            {
                case EnemyArchetype.Ranged:
                case EnemyArchetype.EliteBarrage:
                    enemy.gameObject.AddComponent<RangedAttackDriver>().Init(enemy, player, game);
                    break;
                case EnemyArchetype.Summoner:
                    enemy.gameObject.AddComponent<SummonerDriver>().Init(enemy, player, game);
                    break;
            }
        }
    }
}
