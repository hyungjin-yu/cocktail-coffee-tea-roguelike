using System.Collections.Generic;
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
        public const float CoffeeTrackUnlockTime = 150f;

        /// <summary>커피 합류(150초) 이후 트랙 비율. 칵테일+차+커피 = 1.</summary>
        public const float CocktailShareAfterCoffee = 0.49f;
        public const float TeaShareAfterCoffee = 0.49f;
        public const float CoffeeShareAfterCoffee = 1f - CocktailShareAfterCoffee - TeaShareAfterCoffee;

        /// <summary>rng를 생략하면 기존 UnityEngine.Random(전역)을 그대로 쓴다 — 기존 호출부/테스트는
        /// 아무것도 안 바꿔도 된다. 실제 런에서는 GameManager가 시드에서 파생한 스트림(06:98)을 넘긴다.</summary>
        public static EnemyDef PickCommon(float runTime, IRandomSource rng = null) => PickCommon(runTime, rng, null);

        /// <summary>47(2026-10-12) 직업 테마: 칵테일+차 몫 안에서 직업 주 테마가 65%(JobPreset.MainThemeShare), 커피 2%·트랙 내부
        /// 시간표·난수 소비 횟수(트랙 1회 + 아키타입 1회)는 그대로. job이 null이면 기존 50:50(49/49/2)과 완전히 같다.</summary>
        public static EnemyDef PickCommon(float runTime, IRandomSource rng, Job? job)
        {
            rng ??= UnityRandomSource.Instance;
            float cocktailOfPair = CocktailShareOfPair(job);
            var teaRoster=job==Job.TeaSommelier ? TeaHouseTrack : TeaTrack;

            if (runTime >= CoffeeTrackUnlockTime)
            {
                // 커피 합류 후 비율: 칵테일 49 / 차 49 / 커피 2 (2026-10-06 기획 변경, 02/07/27).
                // 이전 20%에서는 일반 몹 원두가 런당 278~385개라 커피 15노드가 드립 직후 전부 해금됐다
                // (Delivery/Reports/coffee-economy-analysis-2026-10-06.md). 보스 원두 80=8포인트는 그대로다.
                float pair = CocktailShareAfterCoffee + TeaShareAfterCoffee;
                float cocktail = job == null ? CocktailShareAfterCoffee : pair * cocktailOfPair;
                float roll = rng.Value();
                if (roll < cocktail) return PickTrack(runTime, CocktailTrack, rng);
                if (roll < pair) return PickTrack(runTime, teaRoster, rng);
                var coffee=PickCoffee(rng);
                return job==Job.TeaSommelier ? EnemyDef.TeaLeafScholar : coffee;
            }

            // 기존: roll < 0.5면 차. 직업 테마는 차 몫(1 - 칵테일 몫) 미만이면 차.
            bool tea = rng.Value() < 1f - cocktailOfPair;
            return tea ? PickTrack(runTime, teaRoster, rng) : PickTrack(runTime, CocktailTrack, rng);
        }

        /// <summary>칵테일+차 중 칵테일 몫: 직업 없음 0.5, 바텐더 0.65(차 몹 35% 허용), 티 소믈리에 0 —
        /// 2026-10-12 사용자 지시 "차에는 칵테일 몹 나오지 않게": 찻집 런의 일반·웨이브·소환(사장 나와!) 모두 칵테일 트랙 제외.</summary>
        public static float CocktailShareOfPair(Job? job) => job == null ? 0.5f
            : job == Job.Bartender ? JobPreset.MainThemeShare : 0f;

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
            public EnemyDef secondRanged;
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

        static readonly Track TeaHouseTrack=new Track
        {
            rush=EnemyDef.SpringLeafSprite, ranged=EnemyDef.BambooScoopArcher,
            extra=EnemyDef.TeabagImp, secondRanged=EnemyDef.KettleCannon,
            suicide=EnemyDef.SugarCubeBomber, summoner=EnemyDef.LeafWarden,
            elite=EnemyDef.TeaCeremonyGuardian
        };

        static EnemyDef PickTrack(float runTime, Track track, IRandomSource rng)
        {
            const float wRush = 1f;
            float wRanged = runTime >= 15f ? 0.4f : 0f;
            float wSecond=track.secondRanged!=null ? wRanged*.5f : 0f;
            if(wSecond>0f) wRanged-=wSecond;
            float wExtra = track.extra != null && runTime >= 45f
                ? Mathf.Min(0.55f, 0.15f + (runTime - 45f) * 0.0015f) : 0f;
            float wSuicide = runTime >= 60f ? 0.3f : 0f;
            float wSummoner = runTime >= 90f ? 0.15f : 0f;
            // 엘리트: 웨이브 후반에 드물게 단독 등장 컨셉이라 등장 시점도 늦고 가중치도 낮게
            float wElite = runTime >= 120f ? 0.08f : 0f;

            float roll = rng.Value() * (wRush + wRanged + wSecond + wExtra + wSuicide + wSummoner + wElite);
            if ((roll -= wElite) < 0f) return track.elite;
            if ((roll -= wSummoner) < 0f) return track.summoner;
            if ((roll -= wSuicide) < 0f) return track.suicide;
            if ((roll -= wExtra) < 0f) return track.extra;
            if ((roll -= wSecond) < 0f) return track.secondRanged;
            if ((roll -= wRanged) < 0f) return track.ranged;
            return track.rush;
        }

        // ───── 2026-11-10 계열 스폰 테이블(기획/모드별 스폰과 장면 변화.md) ─────

        /// <summary>정규 모드 표(기존 PickCommon 규칙을 그대로 계열로 읽은 값, 정수 % — 표시·검증용, 추첨 경로·난수 소비 불변).
        /// 바텐더 150초 전: 칵테일 65/차 35. 이후: 칵테일 64/차 34/커피 2(0.98×0.65=63.7 반올림). 찻집: 차 100(커피 몫은 차 연구 정령 = 차 계열).
        /// 직업 없음(테스트 경로): 50/50, 150초 후 49/49/2.</summary>
        public static void StandardFamilyPercents(Job? job, float runTime, out int cocktail, out int tea, out int coffee)
        {
            float c = CocktailShareOfPair(job);
            if (runTime < CoffeeTrackUnlockTime) { cocktail = IntNum.Round(c * 100.0); tea = 100 - cocktail; coffee = 0; return; }
            float pair = CocktailShareAfterCoffee + TeaShareAfterCoffee;
            float cf = job == null ? CocktailShareAfterCoffee : pair * c;
            int coffeePct = IntNum.Round(CoffeeShareAfterCoffee * 100.0);
            cocktail = IntNum.Round(cf * 100.0);
            if (job == Job.TeaSommelier) { cocktail = 0; coffee = 0; tea = 100; return; }
            coffee = coffeePct;
            tea = 100 - cocktail - coffee;
        }

        /// <summary>끝없는 재료 모으기의 계열 후보(사용자·코디네이터 승인 2026-11-10: 직업과 무관하게 세 계열의 실제 몬스터가 등장해 모든 상성이 가능).
        /// 정규 모드의 "찻집에는 칵테일 몹 없음" 규칙은 정규 표에만 그대로 남는다(이 모드에서만 별도 표를 명시 선택).
        /// 차 계열은 찻집(티 소믈리에)이면 찻집 트랙, 아니면 바의 차 트랙. 커피 계열은 기존 커피 몹 2종.</summary>
        public static IReadOnlyList<EnemyDef> EndlessCandidates(MonsterFamily family, Job? job)
        {
            var list = new List<EnemyDef>();
            switch (family)
            {
                case MonsterFamily.Cocktail: AddTrack(list, CocktailTrack); break;
                case MonsterFamily.Tea: AddTrack(list, job == Job.TeaSommelier ? TeaHouseTrack : TeaTrack); break;
                case MonsterFamily.Coffee: list.Add(EnemyDef.EspressoBug); list.Add(EnemyDef.MilkFoamer); break;
            }
            return list;
        }

        static void AddTrack(List<EnemyDef> list, Track t)
        {
            foreach (var d in new[] { t.rush, t.ranged, t.extra, t.secondRanged, t.suicide, t.summoner, t.elite })
                if (d != null && !list.Contains(d)) list.Add(d);
        }

        /// <summary>2026-11-10 진상 보스 영업 계열 표 선택. JobTheme이면 기존 PickCommon 그대로(rewardDef = 같은 정의).
        /// AllFamilies면: ① 기존 PickCommon을 같은 스폰 난수로 그대로 추첨(난수 소비·시간 조건·보상 출처 불변) →
        /// ② 그 아키타입 자리(돌진·원거리·대형·제2원거리·자폭·소환·정예)를 가진 계열 후보 중 하나를 별도 난수(remapRng)로 균등 선택해
        /// 같은 자리의 실제 기존 몬스터로 바꾼다. 커피 계열은 돌진(에스프레소 벅)·원거리(밀크 포머) 자리만 있어 다른 자리는 칵테일·차 중에서 고른다.
        /// rewardDef(드롭 종류·수량·보급·히든 처치 효과)는 ①의 직업 테마 정의 — 보상·재료 경제는 기존 표와 같다. 능력치는 바뀐 몬스터의 기존 값.</summary>
        public static EnemyDef PickStandard(float runTime, IRandomSource rng, IRandomSource remapRng, Job? job, SpawnTable table, out EnemyDef rewardDef)
        {
            var theme = PickCommon(runTime, rng, job);
            rewardDef = theme;
            if (table != SpawnTable.AllFamilies) return theme;
            var options = SlotCandidates(SlotOf(theme, job), job);
            if (options.Count == 0) return theme;
            remapRng ??= UnityRandomSource.Instance;
            int i = Mathf.Clamp(Mathf.FloorToInt(remapRng.Value() * options.Count), 0, options.Count - 1);
            return options[i];
        }

        public enum Slot { Rush, Ranged, Extra, SecondRanged, Suicide, Summoner, Elite, Unknown }

        public static Slot SlotOf(EnemyDef d, Job? job)
        {
            if (d == null) return Slot.Unknown;
            if (d == EnemyDef.EspressoBug || d == EnemyDef.TeaLeafScholar) return Slot.Rush;
            if (d == EnemyDef.MilkFoamer) return Slot.Ranged;
            foreach (var t in new[] { CocktailTrack, TeaTrack, TeaHouseTrack })
            {
                if (d == t.rush) return Slot.Rush;
                if (d == t.ranged) return Slot.Ranged;
                if (d == t.extra) return Slot.Extra;
                if (d == t.secondRanged) return Slot.SecondRanged;
                if (d == t.suicide) return Slot.Suicide;
                if (d == t.summoner) return Slot.Summoner;
                if (d == t.elite) return Slot.Elite;
            }
            return Slot.Unknown;
        }

        /// <summary>자리별 세 계열 후보(칵테일 트랙 · 직업에 맞는 차 트랙 · 커피). 없는 자리는 빠진다. 대형·제2원거리 자리는 같은 역할의 원래 자리로 묶는다.</summary>
        public static List<EnemyDef> SlotCandidates(Slot slot, Job? job)
        {
            var tea = job == Job.TeaSommelier ? TeaHouseTrack : TeaTrack;
            var list = new List<EnemyDef>();
            void Add(EnemyDef d) { if (d != null && !list.Contains(d)) list.Add(d); }
            switch (slot)
            {
                case Slot.Rush: Add(CocktailTrack.rush); Add(tea.rush); Add(EnemyDef.EspressoBug); break;
                case Slot.Ranged: case Slot.SecondRanged: Add(CocktailTrack.ranged); Add(tea.ranged); Add(EnemyDef.MilkFoamer); break;
                case Slot.Extra: Add(CocktailTrack.extra); Add(tea.extra ?? tea.rush); Add(EnemyDef.EspressoBug); break;
                case Slot.Suicide: Add(CocktailTrack.suicide); Add(tea.suicide); break;
                case Slot.Summoner: Add(CocktailTrack.summoner); Add(tea.summoner); break;
                case Slot.Elite: Add(CocktailTrack.elite); Add(tea.elite); break;
            }
            return list;
        }

        /// <summary>표 검증: 계열 가중치 합 100, 각 계열 후보가 있고, 후보가 전부 그 계열로 명시 분류됐는지. 문제 목록(빈 목록 = 정상).</summary>
        public static List<string> ValidateEndlessTable(Job? job, long wave)
        {
            var errors = new List<string>();
            int sum = 0;
            foreach (var f in EndlessFamilies)
            {
                sum += EndlessRules.WeightPercent(wave, f);
                var c = EndlessCandidates(f, job);
                if (c.Count == 0) errors.Add($"{MonsterFamilies.Name(f)} 계열 후보 없음(job={job})");
                foreach (var d in c)
                    if (MonsterFamilies.Of(d) != f) errors.Add($"{d.name}: 표 계열 {MonsterFamilies.Name(MonsterFamilies.Of(d))} ≠ {MonsterFamilies.Name(f)}");
            }
            if (sum != 100) errors.Add($"가중치 합 {sum} ≠ 100");
            return errors;
        }

        public static readonly MonsterFamily[] EndlessFamilies = { MonsterFamily.Coffee, MonsterFamily.Tea, MonsterFamily.Cocktail };

        /// <summary>끝없는 모드 1마리 추첨: 계열 1회(정수 % 가중치) + 계열 안 아키타입 1회. 시드 스트림만 사용(재현 가능).
        /// 후보가 없는 계열은 조용히 다른 테마로 바꾸지 않고 예외로 알린다(표 검증이 막아야 하는 상태).</summary>
        public static EnemyDef PickEndless(long wave, float runTime, IRandomSource rng, Job? job, out MonsterFamily family)
        {
            rng ??= UnityRandomSource.Instance;
            int roll = Mathf.Clamp(Mathf.FloorToInt(rng.Value() * 100f), 0, 99);
            family = EndlessFamilies[EndlessFamilies.Length - 1];
            int acc = 0;
            foreach (var f in EndlessFamilies)
            {
                acc += EndlessRules.WeightPercent(wave, f);
                if (roll < acc) { family = f; break; }
            }
            switch (family)
            {
                case MonsterFamily.Cocktail: return PickTrack(runTime, CocktailTrack, rng);
                case MonsterFamily.Tea: return PickTrack(runTime, job == Job.TeaSommelier ? TeaHouseTrack : TeaTrack, rng);
                case MonsterFamily.Coffee: return PickCoffee(rng);
            }
            throw new System.InvalidOperationException($"끝없는 모드 계열 표 오류: {family}");
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
