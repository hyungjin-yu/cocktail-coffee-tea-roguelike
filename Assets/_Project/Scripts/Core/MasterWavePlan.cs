using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-12 다음 웨이브 예고(기획 개발 기록/2026-11-12_다음웨이브예고_구현지시.md).
    /// 한 웨이브의 **확정 생성 계획**: 생성 순서대로의 적 정의 목록. 예고 화면과 실제 생성(MasterChallengeDirector.SpawnOne)이 같은 목록을 쓴다.
    /// 수집 웨이브는 (런 seed, 절대 웨이브, 반복 회차)에서 결정적으로 추첨하고 한 번 만든 계획은 캐시한다 — UI를 다시 열거나 음료를 바꿔도 재추첨하지 않는다.
    /// 박람회·보상은 고정 정의(작은 진상 손님·무해 보상 손님)라 같은 정의 × 목표 수.
    /// </summary>
    public sealed class MasterWavePlan
    {
        public long Wave { get; }
        public int Repeat { get; }
        public MasterSegment Segment { get; }
        /// <summary>생성 순서(Spawned 인덱스) 그대로의 정의.</summary>
        public IReadOnlyList<EnemyDef> Defs => defs;
        readonly List<EnemyDef> defs;
        /// <summary>계획을 만든 시점의 전투 시간(기존 PickEndless 트랙 가중치 입력 — 계획 이후에는 쓰지 않는다).</summary>
        public float PlannedAtRunTime { get; }

        MasterWavePlan(long wave, int repeat, MasterSegment seg, List<EnemyDef> defs, float runTime)
        {
            Wave = wave; Repeat = repeat; Segment = seg; this.defs = defs; PlannedAtRunTime = runTime;
        }

        public int Count => defs.Count;
        public EnemyDef DefAt(long index) => index >= 0 && index < defs.Count ? defs[(int)index] : (defs.Count > 0 ? defs[defs.Count - 1] : null);

        /// <summary>수집 계획 전용 seed(런 seed·웨이브·반복). 다른 난수 흐름(SpawnRng·카드)을 소비하지 않는다.</summary>
        public static int PlanSeed(int runSeed, long wave, int repeat) =>
            unchecked(runSeed * 397 ^ (int)(wave * 7919) ^ (repeat * 104729) ^ 0x5EED);

        /// <summary>계획 생성. 목표 수(MasterRules.TargetCount)만큼 정의를 정한다(포화 방어: 표시·생성 상한 10000).</summary>
        public static MasterWavePlan Build(long wave, int repeat, int runSeed, float runTime, Job job, EnemyDef fairDef, EnemyDef rewardDef)
        {
            var seg = MasterRules.SegmentOf(wave);
            long target = MasterRules.TargetCount(wave);
            int n = (int)System.Math.Min(target, MaxPlanned);
            var list = new List<EnemyDef>(n);
            if (seg == MasterSegment.Fair) for (int i = 0; i < n; i++) list.Add(fairDef);
            else if (seg == MasterSegment.Reward) for (int i = 0; i < n; i++) list.Add(rewardDef);
            else
            {
                var rng = new SeededRandomSource(PlanSeed(runSeed, wave, repeat));
                for (int i = 0; i < n; i++) list.Add(EnemyRoster.PickEndless(wave, runTime, rng, job, out _));
            }
            return new MasterWavePlan(wave, repeat, seg, list, runTime);
        }

        public const int MaxPlanned = 10000;

        // ───── 예고 분류(쉬운 말, 기존 정의 수치에서 유도) ─────

        public enum Kind { Swarm, Elite, Rush }

        /// <summary>강적 = 기본 체력 70 이상(레몬 브루트·마티니 스피릿·티 세리머니 가디언) 또는 정예 탄막. 돌진 = 속도 3 이상 또는 접촉 자폭. 나머지 무리.</summary>
        public const float EliteHp = 70f, RushSpeed = 3f;
        public static Kind KindOf(EnemyDef d)
        {
            if (d == null) return Kind.Swarm;
            if (d.hp >= EliteHp || d.archetype == EnemyArchetype.EliteBarrage) return Kind.Elite;
            if (d.speed >= RushSpeed || d.explodesOnContact || d.archetype == EnemyArchetype.Suicide) return Kind.Rush;
            return Kind.Swarm;
        }

        public static string KindName(Kind k) => k == Kind.Elite ? "강적" : k == Kind.Rush ? "돌진" : "무리";

        /// <summary>대표 행동 특성(쉬운 말). 기존 아키타입·필드만 읽는다.</summary>
        public static string TraitOf(EnemyDef d)
        {
            if (d == null) return "";
            var t = new List<string>();
            if (d.explodesOnContact || d.archetype == EnemyArchetype.Suicide) t.Add("닿으면 자폭");
            if (d.archetype == EnemyArchetype.Ranged || d.archetype == EnemyArchetype.EliteBarrage) t.Add("멀리서 공격");
            if (d.archetype == EnemyArchetype.Summoner) t.Add("제자리 소환");
            if (d.speed >= RushSpeed && !(d.explodesOnContact || d.archetype == EnemyArchetype.Suicide)) t.Add("빠르게 달려옴");
            if (t.Count == 0) t.Add(d.speed <= 0f ? "움직이지 않음" : d.speed < 2.2f ? "천천히 다가옴" : "다가옴");
            return string.Join(" · ", t);
        }

        public sealed class Group
        {
            public EnemyDef def;
            public int count;
            public Kind kind;
            public MonsterFamily family;
        }

        /// <summary>정의별 묶음(많은 순, 같으면 처음 등장 순).</summary>
        public List<Group> Groups()
        {
            var order = new List<EnemyDef>();
            var counts = new Dictionary<EnemyDef, int>();
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (!counts.ContainsKey(d)) { counts[d] = 0; order.Add(d); }
                counts[d]++;
            }
            return order.Select((d, i) => (d, i)).OrderByDescending(x => counts[x.d]).ThenBy(x => x.i)
                .Select(x => new Group { def = x.d, count = counts[x.d], kind = KindOf(x.d), family = MonsterFamilies.Of(x.d) }).ToList();
        }

        /// <summary>웨이브 분류 이름: 박람회 / 무해 보상 / (무리|강적|돌진)이 60% 이상이면 그 이름, 아니면 혼합.</summary>
        public string Label
        {
            get
            {
                if (Segment == MasterSegment.Fair) return "박람회";
                if (Segment == MasterSegment.Reward) return "무해 보상";
                if (defs.Count == 0) return "혼합";
                foreach (Kind k in new[] { Kind.Swarm, Kind.Elite, Kind.Rush })
                {
                    int c = defs.Count(d => KindOf(d) == k);
                    if (c * 100 >= defs.Count * DominantPercent) return KindName(k);
                }
                return "혼합";
            }
        }
        public const int DominantPercent = 60;

        public int CountOf(Kind k) => defs.Count(d => Segment == MasterSegment.Collect && KindOf(d) == k);

        /// <summary>가장 많은 몬스터 계열(중립 제외). 없으면 null.</summary>
        public MonsterFamily? MainFamily()
        {
            if (Segment != MasterSegment.Collect) return null;
            var g = defs.Where(d => d != null).GroupBy(d => MonsterFamilies.Of(d)).Where(x => x.Key != MonsterFamily.Neutral)
                .OrderByDescending(x => x.Count()).FirstOrDefault();
            return g?.Key;
        }

        /// <summary>그 계열에 상성 추가 피해를 주는 음료(이미 구현된 순환 상성만).</summary>
        public static DrinkKind? CounterDrink(MonsterFamily f)
        {
            foreach (DrinkKind k in new[] { DrinkKind.Cocktail, DrinkKind.Coffee, DrinkKind.Tea })
                if (DrinkAffinity.Matches(k, f)) return k;
            return null;
        }

        public static string DrinkName(DrinkKind k) => k == DrinkKind.Coffee ? "커피" : k == DrinkKind.Tea ? "차" : "칵테일";

        /// <summary>일반 전투 조언(적 특성 기준). 아직 없는 음료 공격 전환은 추천하지 않는다.</summary>
        public string Advice()
        {
            if (Segment == MasterSegment.Reward) return "공격하지 않는 보상 손님 — 마음껏 처치";
            if (Segment == MasterSegment.Fair) return "체력 높은 진상 손님 다수가 다가옴 — 한 대상 화력 집중과 거리 유지가 유리 · 상성 없음(중립)";
            var parts = new List<string>();
            int total = Mathf.Max(1, defs.Count);
            if (CountOf(Kind.Swarm) * 100 >= total * 40) parts.Add("수가 많음 — 넓은 범위·관통 공격이 유리");
            if (CountOf(Kind.Elite) > 0) parts.Add("체력 높은 적 — 한 대상 화력 집중");
            if (CountOf(Kind.Rush) * 100 >= total * 25) parts.Add("빠르게 붙는 적 — 거리 유지, 둔화·밀치기 효과가 있다면 유리");
            if (parts.Count == 0) parts.Add("여러 종류가 섞임 — 이동하며 처치");
            return string.Join(" · ", parts);
        }

        public string AffinityHint()
        {
            var f = MainFamily();
            if (f == null) return ""; // 박람회는 Advice에 '상성 없음(중립)'이 이미 있다
            var drink = CounterDrink(f.Value);
            int c = defs.Count(d => MonsterFamilies.Of(d) == f.Value);
            return drink == null ? "" : $"주요 계열 {MonsterFamilies.Mark(f.Value)} {MonsterFamilies.Name(f.Value)} {c}/{defs.Count} — {DrinkName(drink.Value)}을(를) 마시면 이 계열에 상성 추가 피해";
        }

        /// <summary>한 마리의 예상 최대 체력(정수): 정의 체력 × 웨이브 배율 × (1 − 약화) × 현재 도전 체력 배율. 보상 = 웨이브1 배율·도전 배율 제외(실제 생성과 같은 식).</summary>
        public static int ExpectedHp(EnemyDef d, MasterSegment seg, long wave, long reductionPercent, float challengeMultiplier)
        {
            if (d == null) return 0;
            if (seg == MasterSegment.Reward) return Mathf.Max(1, IntNum.Round(d.hp * EndlessRules.HpScale(1)));
            float scale = MasterChallengeDirector.Scale(EndlessRules.HpScale(wave), MasterRules.ReductionFactor(reductionPercent));
            return Mathf.Max(1, IntNum.Round(d.hp * scale * Mathf.Max(0f, challengeMultiplier)));
        }

        /// <summary>대표 적 아이콘(기존 몸체 아트 idle 첫 프레임 → 없으면 기존 아이콘).</summary>
        public static Sprite IconOf(EnemyDef d)
        {
            if (d == null) return null;
            var art = ArtLibrary.Get(d.ArtId);
            var clip = art != null && art.IsBody ? art.Clip("idle") : null;
            if (clip != null && clip.frames != null && clip.frames.Length > 0 && clip.frames[0] != null) return clip.frames[0];
            return string.IsNullOrEmpty(d.spriteResourcePath) ? null : Resources.Load<Sprite>(d.spriteResourcePath);
        }
    }
}
