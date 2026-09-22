using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 런 시간에 따른 일반 몹 등장 테이블 (가안). 트리클 스폰·웨이브·"사장 나와!" 소환이 전부 공유한다.
    /// 칵테일/차 두 트랙을 50:50으로 섞는다 ([[00_기획서 v0.1]] 6장: "재료는 직업과 무관하게 항상 두 종류 다 드랍").
    /// 각 트랙 안에서는 [[01_몹 디자인 - 칵테일 & 차]] 기준: 돌진+투척 2종이 MVP 필수, 폭발/소환/엘리트는
    /// 그보다 늦고 드물게 섞이도록 페이싱을 잡는다. 두 트랙이 완전히 대칭이라 가중치 로직도 동일하게 공유한다.
    /// </summary>
    public static class EnemyRoster
    {
        public static EnemyDef PickCommon(float runTime)
        {
            bool tea = Random.value < 0.5f;
            return tea ? PickTrack(runTime, TeaTrack) : PickTrack(runTime, CocktailTrack);
        }

        /// <summary>트랙별 아키타입 슬롯 (레몬 브루트는 디자인 문서에 없는 자체 추가라 칵테일에만 있음).</summary>
        class Track
        {
            public EnemyDef rush;
            public EnemyDef ranged;
            public EnemyDef extra; // 칵테일 트랙만: 레몬 브루트
            public EnemyDef suicide;
            public EnemyDef summoner;
            public EnemyDef elite;
        }

        static readonly Track CocktailTrack = new Track
        {
            rush = EnemyDef.Imp,
            ranged = EnemyDef.ShakerGolem,
            extra = EnemyDef.Brute,
            suicide = EnemyDef.OliveBomber,
            summoner = EnemyDef.SwirlConjuror,
            elite = EnemyDef.MartiniSpirit,
        };

        static readonly Track TeaTrack = new Track
        {
            rush = EnemyDef.TeabagImp,
            ranged = EnemyDef.KettleCannon,
            extra = null,
            suicide = EnemyDef.SugarCubeBomber,
            summoner = EnemyDef.LeafWarden,
            elite = EnemyDef.TeaCeremonyGuardian,
        };

        static EnemyDef PickTrack(float runTime, Track track)
        {
            const float wRush = 1f;
            float wRanged = runTime >= 15f ? 0.4f : 0f;
            float wExtra = track.extra != null && runTime >= 45f
                ? Mathf.Min(0.55f, 0.15f + (runTime - 45f) * 0.0015f) : 0f;
            float wSuicide = runTime >= 60f ? 0.3f : 0f;
            float wSummoner = runTime >= 90f ? 0.15f : 0f;
            // 엘리트: 웨이브 후반에 드물게 단독 등장 컨셉이라 등장 시점도 늦고 가중치도 낮게
            float wElite = runTime >= 120f ? 0.08f : 0f;

            float roll = Random.value * (wRush + wRanged + wExtra + wSuicide + wSummoner + wElite);
            if ((roll -= wElite) < 0f) return track.elite;
            if ((roll -= wSummoner) < 0f) return track.summoner;
            if ((roll -= wSuicide) < 0f) return track.suicide;
            if ((roll -= wExtra) < 0f) return track.extra;
            if ((roll -= wRanged) < 0f) return track.ranged;
            return track.rush;
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
