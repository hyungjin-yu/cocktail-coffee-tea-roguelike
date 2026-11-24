using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>바닥 위험 두 종류. 숫자는 저장하지 않는다(런 안에서만 쓰는 상태) — 새 세이브/도감 ID 없음.</summary>
    public enum FloorHazardKind { Water = 0, Stain = 1 }

    /// <summary>
    /// 2026-11-24 Beverage Master #22(주인 지시 + 같은 날 정정): 정규 모드의 디버프 전용 몹(쓴맛 안개·빈 잔 도둑) 자동 등장을
    /// 바닥 환경 위험 2종으로 대체한다. 순서: 예고(경계 링이 차오름) → 생성 순간 플레이어 위치와 무관하게 해당 디버프 즉시 적용
    /// → 플레이어가 구역 안에 CleanSeconds 연속 체류 → 청소 완료 = 그 구역과(같은 계열 다른 구역이 없으면) 디버프가 함께 제거.
    /// 구역을 나가면 연속 체류 카운터 초기화. 구역은 자연 소멸하지 않는다(시간 경과·이탈로 디버프가 풀리지 않음 — 해제는 청소만).
    /// 물기 = 미끄러짐(이동 관성), 얼룩 = 스트레스(공격 속도 감소). 같은 계열은 구역이 여러 개여도 효과가 겹치지 않는다(곳 수만 표시).
    /// 아래 수치는 모두 **검수용 가안**이다(주인 검수 후 조정). 체류 시간은 CleanSeconds 한 곳에서만 정한다.
    /// </summary>
    public static class FloorHazardRules
    {
        /// <summary>청소에 필요한 연속 체류 시간(초). 주인 미정 — 검수용 잠정 3초. 이 상수 한 곳에서 조정한다.</summary>
        public const float CleanSeconds = 3f;

        // ── 등장 일정(가안): 기존 디버프 몹과 같은 120초 시작·30초 간격으로 런 리듬을 유지하고, 300초부터 20초 간격으로 조금 잦아진다.
        public const float FirstSeconds = 120f, IntervalSeconds = 30f, LateFromSeconds = 300f, LateIntervalSeconds = 20f;
        /// <summary>예고 시간(초): 경계 링이 이 시간 동안 안쪽에서 차오른 뒤 구역이 생기고 디버프가 걸린다.</summary>
        public const float WarnSeconds = 1.5f;
        /// <summary>폭주 방지 상한: 화면의 구역(예고 포함) 총 3곳, 계열당 2곳. 상한이면 그 차례를 미루고 밀린 차례는 1개만 남긴다.</summary>
        public const int MaxActive = 3, MaxPerKind = 2;
        /// <summary>연속 생성 유예: 생성·청소 직후 이 시간 동안은 새 구역을 만들지 않는다(청소하자마자 바로 다시 생기는 체감 방지).</summary>
        public const float MinGapSeconds = 8f;

        // ── 배치(가안)
        public const float WaterRadius = 1.1f, StainRadius = 1.0f;
        /// <summary>플레이어 몸 중심에서 구역 가장자리까지 최소 거리 — 발밑·바로 옆 무경고 생성 금지.</summary>
        public const float MinPlayerEdgeDistance = 2.2f;
        /// <summary>플레이어 중심에서 구역 중심까지 최대 거리 — 화면 안·가까운 곳(예고를 보고 청소하러 갈 수 있게).</summary>
        public const float MaxPlayerCenterDistance = 7f;
        /// <summary>런 시작 지점(플레이어 첫 위치) 둘레 이 반경 안에는 구역 가장자리가 들어오지 않는다.</summary>
        public const float StartPointClearance = 3f;
        /// <summary>구역끼리 가장자리 최소 간격 — 겹치지 않아 각자 따로 청소한다(통로 봉쇄 방지).</summary>
        public const float ZoneGap = 0.8f;
        /// <summary>화면 가장자리 여유(월드) — HUD 패널·터치 조이스틱 쪽 끝에 붙지 않게.</summary>
        public const float ViewEdgeMargin = 1.6f;
        public const int PlacementTries = 16;

        // ── 디버프(가안)
        /// <summary>미끄러짐: 입력 속도로 맞춰지는 데 걸리는 시간(초). 멈추면 이 시간 동안 현재 방향으로 감속하며 미끄러진다
        /// (이동 5에서 약 0.75 유닛). 서 있으면(속도 0·입력 0) 아무 힘도 받지 않는다.</summary>
        public const float SlipResponseSeconds = 0.3f;
        /// <summary>스트레스: 공격 속도 배율(0.85 = -15%). 다른 공속 감소와 곱으로 겹치되 얼룩끼리는 겹치지 않는다.</summary>
        public const float StressAttackMultiplier = 0.85f;

        public static bool ModeHasHazards(RunMode mode) => mode == RunMode.Standard;

        public static float Radius(FloorHazardKind k) => k == FloorHazardKind.Water ? WaterRadius : StainRadius;
        public static string ZoneName(FloorHazardKind k) => k == FloorHazardKind.Water ? "물기" : "얼룩";
        public static string DebuffName(FloorHazardKind k) => k == FloorHazardKind.Water ? "미끄러짐" : "스트레스";
        /// <summary>예고·청소 링 색(물기 = 하늘색, 얼룩 = 자주색).</summary>
        public static Color Tint(FloorHazardKind k) => k == FloorHazardKind.Water ? new Color(0.45f, 0.85f, 1f) : new Color(0.85f, 0.45f, 0.95f);
        /// <summary>기존 장판 아트(바닥 웅덩이) 재사용: 물기 = 차 웅덩이(색 틴트로 물빛), 얼룩 = 커피 웅덩이.</summary>
        public static string ArtId(FloorHazardKind k) => k == FloorHazardKind.Water ? "zone_tea_pool_v2" : "zone_coffee_pool_v2";
        public static Color PoolTint(FloorHazardKind k) => k == FloorHazardKind.Water ? new Color(0.7f, 0.95f, 1f) : Color.white;

        /// <summary>runTime까지 예정된 생성 차례 수. 120초 = 1, 150초 = 2 … 300초부터 20초 간격.</summary>
        public static int DueCount(float runTime)
        {
            if (runTime < FirstSeconds) return 0;
            if (runTime < LateFromSeconds) return 1 + Mathf.FloorToInt((runTime - FirstSeconds) / IntervalSeconds);
            int early = Mathf.CeilToInt((LateFromSeconds - FirstSeconds) / IntervalSeconds); // 120~299.9초의 6회
            return early + 1 + Mathf.FloorToInt((runTime - LateFromSeconds) / LateIntervalSeconds);
        }

        /// <summary>n번째(0부터) 차례의 기본 종류: 짝수 물기, 홀수 얼룩.</summary>
        public static FloorHazardKind KindFor(int index) => index % 2 == 0 ? FloorHazardKind.Water : FloorHazardKind.Stain;

        /// <summary>미끄러짐 이동 한 단계: 현재 속도를 목표 속도로 SlipResponseSeconds에 걸쳐 맞춘다.
        /// 서 있으면 0(강제 가속 없음), 결과 크기는 max(이동 속도, 현재 속도)를 넘지 않는다.</summary>
        public static Vector2 SlipStep(Vector2 current, Vector2 target, float moveSpeed, float dt)
        {
            if (target.sqrMagnitude < 1e-6f && current.sqrMagnitude < 1e-6f) return Vector2.zero;
            float maxDelta = Mathf.Max(0.01f, moveSpeed) / SlipResponseSeconds * dt;
            var next = Vector2.MoveTowards(current, target, maxDelta);
            float cap = Mathf.Max(moveSpeed, current.magnitude);
            return next.sqrMagnitude > cap * cap ? next.normalized * cap : next;
        }

        /// <summary>배치 규칙 검사: 레인·전투 구역·화면 안, 플레이어·시작 지점·다른 구역과 거리.</summary>
        public static bool IsValidPlacement(Vector2 c, float r, Vector2 player, Vector2 start, float viewMinX, float viewMaxX, IReadOnlyList<FloorHazardZone> existing)
        {
            if (c.y - r < Balance.DepthMin || c.y + r > Balance.DepthMax) return false;
            if (!BattleArena.Contains(c.x, r)) return false;
            if (c.x - r < viewMinX + ViewEdgeMargin || c.x + r > viewMaxX - ViewEdgeMargin) return false;
            float dp = (c - player).magnitude;
            if (dp - r < MinPlayerEdgeDistance || dp > MaxPlayerCenterDistance) return false;
            if ((c - start).magnitude - r < StartPointClearance) return false;
            if (existing != null)
                foreach (var z in existing)
                    if (z != null && !z.Cleaned && (c - z.Center).magnitude < r + z.Radius + ZoneGap) return false;
            return true;
        }

        /// <summary>플레이어 둘레(MinPlayerEdgeDistance+r ~ MaxPlayerCenterDistance) 후보를 PlacementTries번 뽑아 첫 유효 위치.
        /// rng는 런 시드 난수와 분리된 자체 난수(기존 몹 추첨 순서 불변).</summary>
        public static bool TryPlace(FloorHazardKind kind, Vector2 player, Vector2 start, float viewMinX, float viewMaxX,
            IReadOnlyList<FloorHazardZone> existing, System.Random rng, out Vector2 pos)
        {
            float r = Radius(kind);
            float minD = MinPlayerEdgeDistance + r + 0.1f;
            for (int i = 0; i < PlacementTries; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2.0;
                float d = Mathf.Lerp(minD, MaxPlayerCenterDistance, (float)rng.NextDouble());
                var c = player + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * d;
                if (IsValidPlacement(c, r, player, start, viewMinX, viewMaxX, existing)) { pos = c; return true; }
            }
            pos = default;
            return false;
        }
    }

    /// <summary>바닥 위험 구역 하나의 상태(판정 = 중심·반경 원, 그림 경계와 같다).</summary>
    public sealed class FloorHazardZone
    {
        public FloorHazardKind Kind;
        public Vector2 Center;
        public float Radius;
        /// <summary>남은 예고 시간. 0이 되는 순간 구역이 생기고 디버프가 걸린다.</summary>
        public float WarnLeft;
        /// <summary>연속 체류 시간(구역을 나가면 0).</summary>
        public float CleanProgress;
        public bool Cleaned;

        public bool Warning => !Cleaned && WarnLeft > 0f;
        public bool Active => !Cleaned && WarnLeft <= 0f;
        public float Clean01 => Mathf.Clamp01(CleanProgress / FloorHazardRules.CleanSeconds);
        public bool Contains(Vector2 p) => (p - Center).sqrMagnitude <= Radius * Radius;
    }

    /// <summary>
    /// 바닥 위험의 순수 상태(시각·씬 없음 — EditMode 시험이 이 클래스로 순서를 검증한다).
    /// 디버프는 "계열별로 활성 구역이 하나라도 있으면 켜짐"으로 파생된다: 생성 즉시 켜지고, 그 계열의 마지막 구역이 청소되는 순간 꺼진다.
    /// </summary>
    public sealed class FloorHazardField
    {
        readonly List<FloorHazardZone> zones = new List<FloorHazardZone>();
        public IReadOnlyList<FloorHazardZone> Zones => zones;
        public event Action<FloorHazardZone> Materialized, CleanedZone;
        public int Created { get; private set; }
        public int CleanedCount { get; private set; }

        public FloorHazardZone Add(FloorHazardKind kind, Vector2 center, float warnSeconds = FloorHazardRules.WarnSeconds)
        {
            var z = new FloorHazardZone { Kind = kind, Center = center, Radius = FloorHazardRules.Radius(kind), WarnLeft = Mathf.Max(0f, warnSeconds) };
            zones.Add(z);
            Created++;
            if (z.WarnLeft <= 0f) Materialized?.Invoke(z);
            return z;
        }

        /// <summary>예고 진행 → 생성, 활성 구역의 연속 체류 → 청소. dt = 게임 시간(일시정지·선택 창 중에는 호출하지 않거나 0).</summary>
        public void Tick(float dt, Vector2 player, bool playerCanClean)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.Cleaned) continue;
                if (z.Warning)
                {
                    z.WarnLeft -= dt;
                    if (z.WarnLeft <= 0f) { z.WarnLeft = 0f; Materialized?.Invoke(z); }
                    continue; // 생성된 틱에는 체류를 세지 않는다(예고 중 체류는 청소가 아님)
                }
                if (playerCanClean && z.Contains(player))
                {
                    z.CleanProgress += dt;
                    if (z.CleanProgress >= FloorHazardRules.CleanSeconds) { z.Cleaned = true; CleanedCount++; CleanedZone?.Invoke(z); }
                }
                else z.CleanProgress = 0f; // 나가면 연속 체류 초기화
            }
            zones.RemoveAll(z => z.Cleaned);
        }

        public int Count(FloorHazardKind k) { int n = 0; foreach (var z in zones) if (!z.Cleaned && z.Kind == k) n++; return n; }
        public int ActiveCount(FloorHazardKind k) { int n = 0; foreach (var z in zones) if (z.Active && z.Kind == k) n++; return n; }
        public int Total { get { int n = 0; foreach (var z in zones) if (!z.Cleaned) n++; return n; } }
        public bool SlipActive => ActiveCount(FloorHazardKind.Water) > 0;
        public bool StressActive => ActiveCount(FloorHazardKind.Stain) > 0;
        public bool AnyDebuff => SlipActive || StressActive;

        /// <summary>지금 청소 중인(체류 진행이 가장 큰) 구역. 없으면 null.</summary>
        public FloorHazardZone Cleaning
        {
            get
            {
                FloorHazardZone best = null;
                foreach (var z in zones) if (z.Active && z.CleanProgress > 0f && (best == null || z.CleanProgress > best.CleanProgress)) best = z;
                return best;
            }
        }

        /// <summary>상한 안에서 다음 종류: 기본 종류가 계열 상한이면 다른 종류, 총 상한이면 null.</summary>
        public FloorHazardKind? NextKind(int index)
        {
            if (Total >= FloorHazardRules.MaxActive) return null;
            var k = FloorHazardRules.KindFor(index);
            if (Count(k) < FloorHazardRules.MaxPerKind) return k;
            var other = k == FloorHazardKind.Water ? FloorHazardKind.Stain : FloorHazardKind.Water;
            return Count(other) < FloorHazardRules.MaxPerKind ? other : (FloorHazardKind?)null;
        }

        /// <summary>런 종료·리트라이·씬 전환: 모든 구역 제거(디버프도 파생되어 함께 꺼진다).</summary>
        public void Clear() => zones.Clear();

        /// <summary>HUD 상태 줄 조각. 예: "미끄러짐(물기 2곳) · 스트레스(얼룩 1곳) · 청소 1.2/3초". 없으면 "".</summary>
        public string StatusText()
        {
            int w = ActiveCount(FloorHazardKind.Water), s = ActiveCount(FloorHazardKind.Stain);
            return StatusText(w, s, Cleaning);
        }

        public static string StatusText(int water, int stain, FloorHazardZone cleaning)
        {
            var parts = new List<string>(3);
            if (water > 0) parts.Add($"{FloorHazardRules.DebuffName(FloorHazardKind.Water)}(물기 {water}곳)");
            if (stain > 0) parts.Add($"{FloorHazardRules.DebuffName(FloorHazardKind.Stain)}(얼룩 {stain}곳)");
            if (parts.Count == 0) return "";
            if (cleaning != null)
                parts.Add($"{FloorHazardRules.ZoneName(cleaning.Kind)} 청소 {cleaning.CleanProgress:0.0}/{FloorHazardRules.CleanSeconds:0}초");
            else parts.Add($"구역 안 {FloorHazardRules.CleanSeconds:0}초 머물러 청소");
            return string.Join(" · ", parts);
        }
    }
}
