using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>2026-11-12 마스터 도전 런 제조 잔의 보조 전투 특성(기획 개발 기록/2026-11-12_커피차_전투연결_후속지시.md, 검토판 기본값).</summary>
    public enum RunDrinkTraitKind { None = 0, EspressoFocus = 1, DripSpread = 2, MokaSteam = 3, TeaFirst = 4, TeaSecond = 5, TeaThird = 6 }

    /// <summary>
    /// 런 중 실제로 **마신** 런 제조 잔 하나의 보조 전투 특성. 다른 잔을 마시면 통째로 교체(누적 없음), 새 런은 None.
    /// 직접 적중 경로(Enemy.TakeDirectHit — 근접·원래 투사체·광역 직접 공격)에서만 발동한다. 보조 적중은 TakeDamage(Effect)라
    /// 직접 적중 훅·상성·히든·숙련·다른 보조 적중을 다시 부르지 않는다(재귀 없음). 수치는 검토판 기본값이며 실제 음료 효능이 아니다.
    /// </summary>
    public static class RunDrinkTrait
    {
        // 에스프레소 머신: 강적 단일 화력
        public const int EspressoElitePercent = 20;
        // 핸드 드립: 주변 보조 적중
        public const float DripRadius = 1.6f, DripCoef = 0.35f, DripInterval = 0.2f;
        public const int DripTargets = 2;
        // 차 1번째: 공격 속도
        public const float TeaFirstAttackSpeed = 0.15f;
        // 차 2번째: 적중 둔화(보스 제외, 갱신만)
        public const float TeaSlowFactor = 0.6f, TeaSlowSeconds = 1.5f;
        // 차 3번째: 범위 보조 적중
        public const float TeaThirdRadius = 1.3f, TeaThirdCoef = 0.25f, TeaThirdInterval = 0.25f;
        public const int TeaThirdTargets = 3;
        public const int MaxSteep = 3;

        public static RunDrinkTraitKind For(RunCup cup)
        {
            if (cup == null || cup.recipe == null) return RunDrinkTraitKind.None;
            if (cup.Kind == DrinkKind.Coffee)
                return cup.recipe.coffeeMethod switch
                {
                    CoffeeBrewMethod.EspressoMachine => RunDrinkTraitKind.EspressoFocus,
                    CoffeeBrewMethod.PourOver => RunDrinkTraitKind.DripSpread,
                    CoffeeBrewMethod.MokaPot => RunDrinkTraitKind.MokaSteam,
                    _ => RunDrinkTraitKind.None,
                };
            if (cup.Kind == DrinkKind.Tea)
                return cup.Steep >= 3 ? RunDrinkTraitKind.TeaThird : cup.Steep == 2 ? RunDrinkTraitKind.TeaSecond : RunDrinkTraitKind.TeaFirst;
            return RunDrinkTraitKind.None;
        }

        public static string Name(RunDrinkTraitKind k) => k switch
        {
            RunDrinkTraitKind.EspressoFocus => "에스프레소 — 강적 집중",
            RunDrinkTraitKind.DripSpread => "핸드 드립 — 주변 보조 적중",
            RunDrinkTraitKind.MokaSteam => "모카포트 — 기존 증기압 폭발",
            RunDrinkTraitKind.TeaFirst => "차 1번째 우림 — 공격 속도",
            RunDrinkTraitKind.TeaSecond => "차 2번째 우림 — 적중 둔화",
            RunDrinkTraitKind.TeaThird => "차 3번째 우림 — 범위 보조 적중",
            _ => "보조 전투 특성 없음",
        };

        /// <summary>잔 줄에 쓰는 짧은 이름.</summary>
        public static string Short(RunDrinkTraitKind k) => k switch
        {
            RunDrinkTraitKind.EspressoFocus => $"강적 피해 +{EspressoElitePercent}%",
            RunDrinkTraitKind.DripSpread => "주변 보조 적중",
            RunDrinkTraitKind.MokaSteam => "증기압 폭발(기존)",
            RunDrinkTraitKind.TeaFirst => $"공격 속도 +{TeaFirstAttackSpeed * 100f:0}%",
            RunDrinkTraitKind.TeaSecond => "적중 둔화",
            RunDrinkTraitKind.TeaThird => "범위 보조 적중",
            _ => "특성 없음",
        };

        /// <summary>실제 적용 수치·제한을 숨기지 않는 설명(제조 창·준비 패널).</summary>
        public static string Describe(RunDrinkTraitKind k) => k switch
        {
            RunDrinkTraitKind.EspressoFocus => $"직접 적중 대상이 강적(기본 체력 {MasterWavePlan.EliteHp:0} 이상·정예·보스·박람회 진상 손님)이면 피해 +{EspressoElitePercent}% · 일반 적은 변화 없음",
            RunDrinkTraitKind.DripSpread => $"직접 적중 시 주변 {DripRadius:0.#}칸 안 다른 적 최대 {DripTargets}명에게 피해 {DripCoef * 100f:0}% 보조 적중({DripInterval:0.##}초마다 1회, 상성 없음)",
            RunDrinkTraitKind.MokaSteam => "기존 증기압 폭발 유지(직접 타격으로 증기압 → 주변 폭발) · 신규 특성 없음",
            RunDrinkTraitKind.TeaFirst => $"공격 속도 +{TeaFirstAttackSpeed * 100f:0}%",
            RunDrinkTraitKind.TeaSecond => $"직접 적중 대상 이동 속도 ×{TeaSlowFactor:0.#} {TeaSlowSeconds:0.#}초(보스 제외 · 다시 맞히면 시간만 갱신, 중첩 없음)",
            RunDrinkTraitKind.TeaThird => $"직접 적중 시 주변 {TeaThirdRadius:0.#}칸 안 다른 적 최대 {TeaThirdTargets}명에게 피해 {TeaThirdCoef * 100f:0}% 보조 적중({TeaThirdInterval:0.##}초마다 1회, 상성 없음)",
            _ => "",
        };

        /// <summary>강적 판정(에스프레소): 기본 체력 ≥ 70·정예 탄막·보스·박람회 진상 손님.</summary>
        public static bool IsElite(EnemyDef d) =>
            d != null && (d.IsBoss || MasterWavePlan.KindOf(d) == MasterWavePlan.Kind.Elite || MasterDefs.IsFairCustomer(d));

        /// <summary>직접 적중 기본 피해 b0에 더할 정수(에스프레소 강적). 상성은 호출 측에서 b0 기준으로 따로 계산한다(증폭 중복 없음).</summary>
        public static int DirectBonus(PlayerController p, Enemy target, int b0)
        {
            if (p == null || target == null || b0 <= 0 || p.Stats == null || p.Stats.BonusesSuppressed) return 0;
            if (p.DrinkTrait != RunDrinkTraitKind.EspressoFocus || !IsElite(target.Def)) return 0;
            return Mathf.Max(1, IntNum.Round(b0 * EspressoElitePercent / 100.0));
        }

        /// <summary>보조 적중·둔화 기록(시험·검수).</summary>
        public static int SplashHits { get; private set; }
        public static int SlowsApplied { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { SplashHits = 0; SlowsApplied = 0; }

        static readonly List<Enemy> near = new List<Enemy>();

        /// <summary>직접 적중 처리 뒤(대상이 죽었어도) 둔화·보조 적중. 보조 적중은 TakeDamage(Effect) — 직접 적중이 아니라 이 함수가 다시 불리지 않는다.</summary>
        public static void AfterDirectHit(PlayerController p, Enemy target, int b, Vector2 at)
        {
            if (p == null || target == null || b <= 0 || p.Stats == null || p.Stats.BonusesSuppressed) return;
            switch (p.DrinkTrait)
            {
                case RunDrinkTraitKind.TeaSecond:
                    if (!target.IsDead && target.Def != null && !target.Def.IsBoss) { target.ApplyDrinkSlow(TeaSlowFactor, TeaSlowSeconds); SlowsApplied++; }
                    break;
                case RunDrinkTraitKind.DripSpread:
                    Splash(p, target, b, at, DripRadius, DripCoef, DripTargets, DripInterval);
                    break;
                case RunDrinkTraitKind.TeaThird:
                    Splash(p, target, b, at, TeaThirdRadius, TeaThirdCoef, TeaThirdTargets, TeaThirdInterval);
                    break;
            }
        }

        static void Splash(PlayerController p, Enemy target, int b, Vector2 at, float radius, float coef, int maxTargets, float interval)
        {
            if (Time.time < p.DrinkSplashReadyAt) return;
            Vector2 c = target != null ? (Vector2)target.transform.position : at;
            near.Clear();
            foreach (var e in Enemy.Active)
            {
                if (e == null || e == target || e.IsDead || e.Invulnerable) continue;
                if (((Vector2)e.transform.position - c).sqrMagnitude <= radius * radius) near.Add(e);
            }
            if (near.Count == 0) return;
            near.Sort((x, y) => ((Vector2)x.transform.position - c).sqrMagnitude.CompareTo(((Vector2)y.transform.position - c).sqrMagnitude));
            int dmg = Mathf.Max(1, IntNum.Round(b * coef));
            p.DrinkSplashReadyAt = Time.time + interval;
            for (int i = 0; i < near.Count && i < maxTargets; i++)
            {
                near[i].TakeDamage(dmg, HitKind.Effect, (Vector2)near[i].transform.position);
                SplashHits++;
            }
        }
    }
}
