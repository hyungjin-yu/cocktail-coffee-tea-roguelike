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
        /// <summary>커피 트랙(에스프레소 벅/밀크 포머)이 섞이기 시작하는 시점. 그라인더 골렘(중간보스 A) 등장
        /// 시점(2:30)과 맞춰서, "중간보스 A 처치 이후"라는 기획 의도를 런타임 타이머로 근사한다
        /// ([[02_몹 디자인 - 커피 & 중간보스]] 1장 — 원두 게이지가 중간보스 킬에만 의존하지 않게 하는 완충재).</summary>
        const float CoffeeTrackUnlockTime = 150f;

        /// <summary>rng를 생략하면 기존 UnityEngine.Random(전역)을 그대로 쓴다 — 기존 호출부/테스트는
        /// 아무것도 안 바꿔도 된다. 실제 런에서는 GameManager가 시드에서 파생한 스트림(06:98)을 넘긴다.</summary>
        public static EnemyDef PickCommon(float runTime, IRandomSource rng = null)
        {
            rng ??= UnityRandomSource.Instance;

            if (runTime >= CoffeeTrackUnlockTime)
            {
                // 커피 합류 후 비율(가안): 칵테일 40 / 차 40 / 커피 20
                float roll = rng.Value();
                if (roll < 0.4f) return PickTrack(runTime, CocktailTrack, rng);
                if (roll < 0.8f) return PickTrack(runTime, TeaTrack, rng);
                return PickCoffee(rng);
            }

            bool tea = rng.Value() < 0.5f;
            return tea ? PickTrack(runTime, TeaTrack, rng) : PickTrack(runTime, CocktailTrack, rng);
        }

        /// <summary>커피 트랙은 칵테일/차처럼 시간에 따라 늘어나는 여러 아키타입이 아니라, 소량 완충용
        /// 돌진형/투척형 2종만 고정 비율로 섞는다 (에스프레소 벅이 더 흔함).</summary>
        static EnemyDef PickCoffee(IRandomSource rng) =>
            rng.Value() < 0.65f ? EnemyDef.EspressoBug : EnemyDef.MilkFoamer;

        /// <summary>트랙별 아키타입 슬롯 (레몬 브루트는 06 로스터 표에 칵테일 트랙 전용으로만 있고
        /// 차 트랙 대칭이 없음 — "문서에 없는 자체 추가"라던 예전 설명은 틀렸다, 04/06에 정식 등재됨).</summary>
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

        static EnemyDef PickTrack(float runTime, Track track, IRandomSource rng)
        {
            const float wRush = 1f;
            float wRanged = runTime >= 15f ? 0.4f : 0f;
            float wExtra = track.extra != null && runTime >= 45f
                ? Mathf.Min(0.55f, 0.15f + (runTime - 45f) * 0.0015f) : 0f;
            float wSuicide = runTime >= 60f ? 0.3f : 0f;
            float wSummoner = runTime >= 90f ? 0.15f : 0f;
            // 엘리트: 웨이브 후반에 드물게 단독 등장 컨셉이라 등장 시점도 늦고 가중치도 낮게
            float wElite = runTime >= 120f ? 0.08f : 0f;

            float roll = rng.Value() * (wRush + wRanged + wExtra + wSuicide + wSummoner + wElite);
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
