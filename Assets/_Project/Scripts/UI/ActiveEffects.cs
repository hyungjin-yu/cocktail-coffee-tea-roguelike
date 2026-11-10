using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>지금 적용 중인 효과 한 칸의 성격. 색만으로 구분하지 않도록 HUD는 종류마다 다른 모양(테두리·남은 시간 막대·칸 점)을 쓴다.</summary>
    public enum ActiveEffectKind
    {
        /// <summary>이번 판 내내(준비 음료 등) — 굵은 테두리.</summary>
        Run,
        /// <summary>남은 시간이 있는 상태 — 아래 남은 시간 막대 + "n초".</summary>
        Timed,
        /// <summary>쌓이는 수치(콤보·증기압) — "n/최대" + 칸 점.</summary>
        Stack,
        /// <summary>대기 중 준비 완료(보호막 등) — "준비".</summary>
        Ready,
        /// <summary>이번 판 효과가 잠시 무효(환불 봉인) — "무효 n초".</summary>
        Suppressed,
    }

    /// <summary>HUD 지속 칸 하나. 모든 숫자는 정수 문자열.</summary>
    public sealed class ActiveEffect
    {
        public string id;
        /// <summary>칸 위 짧은 이름(한글 4자 이하 권장).</summary>
        public string label;
        /// <summary>칸 아래 짧은 정수 요약("+30%", "3초", "7/10").</summary>
        public string value;
        /// <summary>상세 창 한 줄(출처 전체 이름 · 효과 · 상태).</summary>
        public string detail;
        public ActiveEffectKind kind;
        public float remaining, duration;
        public int stacks, maxStacks;
        /// <summary>칸 아이콘(기존 잔 아트/도형). null이면 도형 기본값.</summary>
        public Sprite icon;
        public Color accent = Color.white;
        /// <summary>정렬 우선(작을수록 앞): 시간 제한·무효 → 쌓임·준비 → 이번 판 내내.</summary>
        public int order;
    }

    /// <summary>
    /// 2026-11-09 추가 친구 피드백("라운드 안에서 내가 뭘 먹었고 무슨 버프인지 모른다"): 실제 런 상태만을 유일한 기준으로
    /// 지금 적용 중인 음료·커피 추출법·히든 스킬 상태를 모은다. 조건부 타격 보정(첫 모금 등)처럼 "지금" 켜져 있지 않은 것은
    /// 칸으로 만들지 않고 상세 창에만 둔다. 수치·규칙을 바꾸지 않는 읽기 전용 수집기다.
    /// </summary>
    public static class ActiveEffects
    {
        public static string ShortStat(DrinkStat s) => s switch
        {
            DrinkStat.Attack => "공격",
            DrinkStat.AttackSpeed => "공속",
            DrinkStat.CritChance => "치명",
            DrinkStat.MaxHp => "체력",
            DrinkStat.CooldownReduction => "쿨감",
            DrinkStat.MoveSpeed => "이속",
            DrinkStat.Range => "사거리",
            _ => s.ToString(),
        };

        public static List<ActiveEffect> Collect(GameManager game)
        {
            var list = new List<ActiveEffect>();
            if (game == null || game.Player == null) return list;
            var stats = game.Stats;
            bool suppressed = stats != null && stats.BonusesSuppressed;

            // 준비 음료: 입장 때 실제로 적용된 DrinkBuff(정수 %로 0보다 큰 효과만).
            var drink = game.DrinkBuff != null ? DrinkLab.DrinkInRun(game.RunId) : null;
            if (drink != null)
            {
                var shown = new List<DrinkStat>(game.DrinkBuff.Shown());
                string first = shown.Count > 0 ? ShortStat(shown[0]) + DrinkBuff.Pct(shown[0], game.DrinkBuff[shown[0]]) : "효과 없음";
                var look = DrinkCalculator.Look(drink.recipe);
                list.Add(new ActiveEffect
                {
                    id = "drink",
                    label = "음료",
                    value = suppressed ? $"무효 {IntNum.RemainingSeconds(stats.SuppressedRemaining)}초"
                        : shown.Count > 1 ? $"{first} 외{shown.Count - 1}" : first,
                    detail = $"준비 음료 · {drink.recipe.name} · {game.DrinkBuff.Describe()}" +
                        (DrinkAffinity.Describe(drink.recipe.Kind) is string aff ? " · " + aff : "") + (suppressed ? $" · 환불로 {IntNum.RemainingSeconds(stats.SuppressedRemaining)}초 무효" : " · 이번 판 내내"),
                    kind = suppressed ? ActiveEffectKind.Suppressed : ActiveEffectKind.Run,
                    remaining = suppressed ? stats.SuppressedRemaining : 0f,
                    icon = DrinkArt.SpriteOrPlaceholder(DrinkStageView.ServeFor(drink.recipe), out _),
                    accent = new Color(look.color.r, look.color.g, look.color.b, 1f),
                    order = suppressed ? 0 : 50,
                });
            }

            AddCoffee(list, game, suppressed, drink != null ? drink.recipe : null);
            AddHidden(list, game);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        static void AddCoffee(List<ActiveEffect> list, GameManager game, bool suppressed, DrinkRecipe recipe)
        {
            var c = game.Player.Coffee;
            if (c == null) return;
            string name = CoffeeBrewing.ButtonName(c.Method);
            var icon = recipe != null ? DrinkArt.SpriteOrPlaceholder(CoffeeBrewing.CupArt(c.Method), out _) : null;
            var e = new ActiveEffect { id = "coffee", icon = icon, accent = new Color(1f, 0.8f, 0.5f) };
            switch (c.Method)
            {
                case CoffeeBrewMethod.EspressoMachine:
                    if (c.ShotActive)
                    {
                        e.label = "샷"; e.kind = ActiveEffectKind.Timed; e.remaining = c.ShotRemaining; e.duration = CoffeeBrewRuntime.EspressoShotSeconds;
                        e.value = $"공속+{IntNum.Percent(CoffeeBrewRuntime.EspressoShotAttackSpeed)}%"; e.order = 10;
                        e.detail = $"{name} 샷 · 공격 속도 +{IntNum.Percent(CoffeeBrewRuntime.EspressoShotAttackSpeed)}% · {IntNum.RemainingSeconds(c.ShotRemaining)}초 남음";
                    }
                    else if (c.RechargeRemaining > 0f)
                    {
                        e.label = "샷 대기"; e.kind = ActiveEffectKind.Timed; e.remaining = c.RechargeRemaining; e.duration = CoffeeBrewRuntime.EspressoRechargeSeconds;
                        e.value = $"{IntNum.RemainingSeconds(c.RechargeRemaining)}초"; e.order = 40; e.accent = new Color(0.7f, 0.66f, 0.6f);
                        e.detail = $"{name} · 다음 샷 준비 {IntNum.RemainingSeconds(c.RechargeRemaining)}초 (지금은 보너스 없음)";
                    }
                    else
                    {
                        int pct = Mathf.FloorToInt(c.Charge * 10f) * 10;
                        e.label = "압력"; e.kind = ActiveEffectKind.Stack; e.stacks = pct / 10; e.maxStacks = 10;
                        e.value = $"{pct}%"; e.order = 30; e.accent = new Color(0.85f, 0.75f, 0.6f);
                        e.detail = $"{name} · 멈춰 서면 압력 {pct}% → 100%에서 샷(공격 속도 +{IntNum.Percent(CoffeeBrewRuntime.EspressoShotAttackSpeed)}%)";
                    }
                    break;
                case CoffeeBrewMethod.PourOver:
                    e.kind = ActiveEffectKind.Timed; e.remaining = c.PhaseRemaining; e.order = 10;
                    if (c.Blooming)
                    {
                        e.label = "뜸"; e.duration = CoffeeBrewRuntime.PourBloomSeconds; e.accent = new Color(0.72f, 0.88f, 1f);
                        e.value = $"이속+{IntNum.Percent(CoffeeBrewRuntime.PourBloomMoveSpeed)}%";
                        e.detail = $"{name} 뜸 · 이동 속도 +{IntNum.Percent(CoffeeBrewRuntime.PourBloomMoveSpeed)}% · {IntNum.RemainingSeconds(c.PhaseRemaining)}초 뒤 붓기";
                    }
                    else
                    {
                        e.label = "붓기"; e.duration = CoffeeBrewRuntime.PourPourSeconds;
                        e.value = $"사거리+{IntNum.Percent(CoffeeBrewRuntime.PourPourRange)}%";
                        e.detail = $"{name} 붓기 · 사거리 +{IntNum.Percent(CoffeeBrewRuntime.PourPourRange)}% · {IntNum.RemainingSeconds(c.PhaseRemaining)}초 뒤 뜸";
                    }
                    break;
                case CoffeeBrewMethod.MokaPot:
                    e.label = "증기압"; e.kind = ActiveEffectKind.Stack; e.stacks = c.Heat; e.maxStacks = CoffeeBrewRuntime.MokaHeatMax;
                    e.value = $"{c.Heat}/{CoffeeBrewRuntime.MokaHeatMax}"; e.order = c.Heat > 0 ? 20 : 45; e.accent = new Color(0.9f, 0.94f, 1f);
                    e.detail = $"{name} · 증기압 {c.Heat}/{CoffeeBrewRuntime.MokaHeatMax} · 직접 타격으로 차면 증기 폭발(기본 피해 {IntNum.Multiplier(CoffeeBrewRuntime.MokaBurstDamageScale)})";
                    break;
                default:
                    return;
            }
            if (suppressed && e.kind == ActiveEffectKind.Timed && e.label != "샷 대기")
            {
                // 커피 보정도 % 보너스라 환불 봉인 중에는 실제로 0이다(CoffeeBrewRuntime.ApplyStats → PlayerStats.Live).
                e.value = "무효"; e.kind = ActiveEffectKind.Suppressed; e.detail += " · 환불로 무효 중";
            }
            list.Add(e);
        }

        static void AddHidden(List<ActiveEffect> list, GameManager game)
        {
            var p = game.Player;
            var rt = p.HiddenRuntime;
            if (rt == null || p.Hidden == null) return;
            var job = game.Job;
            string Name(HiddenSkill s) => JobStudyTheme.HiddenName(HiddenSkillDatabase.Get(s), job);

            if (p.Hidden.Has(HiddenSkill.IcedCup))
                list.Add(rt.BarrierReady
                    ? new ActiveEffect { id = "iced_cup", label = "보호막", value = "준비", kind = ActiveEffectKind.Ready, order = 25,
                        accent = new Color(0.6f, 0.95f, 1f), detail = $"{Name(HiddenSkill.IcedCup)} · 다음 단발 피해 1회 차단" }
                    : new ActiveEffect { id = "iced_cup", label = "보호막", value = $"{IntNum.RemainingSeconds(rt.BarrierRemaining)}초",
                        kind = ActiveEffectKind.Timed, remaining = rt.BarrierRemaining, duration = HiddenSkillRuntime.BarrierInterval, order = 48,
                        accent = new Color(0.55f, 0.7f, 0.75f), detail = $"{Name(HiddenSkill.IcedCup)} · 보호막 재충전 {IntNum.RemainingSeconds(rt.BarrierRemaining)}초" });
            if (p.Hidden.Has(HiddenSkill.ShakeRhythm) && rt.Combo > 0)
                list.Add(new ActiveEffect { id = "shake_rhythm", label = "콤보", value = $"{rt.Combo}/{HiddenSkillRuntime.ComboMax}",
                    kind = ActiveEffectKind.Stack, stacks = rt.Combo, maxStacks = HiddenSkillRuntime.ComboMax, order = 20,
                    accent = new Color(1f, 0.8f, 0.4f), detail = $"{Name(HiddenSkill.ShakeRhythm)} · 콤보 {rt.Combo}/{HiddenSkillRuntime.ComboMax} · 직접 피해 +{rt.Combo * 2}%" });
            if (rt.BitterWakeActive)
                list.Add(new ActiveEffect { id = "bitter_wake", label = "각성", value = "공속+25%", kind = ActiveEffectKind.Run, order = 15,
                    accent = new Color(1f, 0.55f, 0.45f), detail = $"{Name(HiddenSkill.BitterWake)} · 체력 35% 이하 동안 공격 속도 +25%" });
            if (p.Hidden.Has(HiddenSkill.WeekendRush) && rt.SpeedBurstRemaining > 0f)
                list.Add(new ActiveEffect { id = "weekend_rush", label = "질주", value = "이속+15%", kind = ActiveEffectKind.Timed,
                    remaining = rt.SpeedBurstRemaining, duration = HiddenSkillRuntime.SpeedBurstSeconds, order = 10,
                    accent = new Color(0.6f, 1f, 0.75f), detail = $"{Name(HiddenSkill.WeekendRush)} · 이동 속도 +15% · {IntNum.RemainingSeconds(rt.SpeedBurstRemaining)}초 남음" });
            if (p.Hidden.Has(HiddenSkill.HerbGarden) && rt.Standing)
                list.Add(new ActiveEffect { id = "herb_garden", label = "회복", value = "×2", kind = ActiveEffectKind.Run, order = 22,
                    accent = new Color(0.6f, 0.95f, 0.6f), detail = $"{Name(HiddenSkill.HerbGarden)} · 멈춰 선 동안 회복량 2배" });
            if (p.Hidden.Has(HiddenSkill.BarTempo) && (rt.Standing || p.IsMovingInput))
                list.Add(rt.Standing
                    ? new ActiveEffect { id = "bar_tempo", label = "템포", value = "받피-10%", kind = ActiveEffectKind.Run, order = 24,
                        accent = new Color(0.75f, 0.8f, 1f), detail = $"{Name(HiddenSkill.BarTempo)} · 멈춰 선 동안 받는 피해 -10%" }
                    : new ActiveEffect { id = "bar_tempo", label = "템포", value = "피해+10%", kind = ActiveEffectKind.Run, order = 24,
                        accent = new Color(1f, 0.75f, 0.55f), detail = $"{Name(HiddenSkill.BarTempo)} · 이동 중 직접 피해 +10%" });
            if (p.Hidden.Has(HiddenSkill.ThickMug))
            {
                int pct = IntNum.Percent(HiddenSkillRuntime.ThickMugBonus(p.MaxHp));
                if (pct > 0)
                    list.Add(new ActiveEffect { id = "thick_mug", label = "머그", value = $"피해+{pct}%", kind = ActiveEffectKind.Run, order = 55,
                        accent = new Color(0.9f, 0.75f, 0.55f), detail = $"{Name(HiddenSkill.ThickMug)} · 최대 체력 기준 직접 피해 +{pct}%" });
            }
        }

        /// <summary>칸 수 상한: 앞에서 max-1개 + "+N" 묶음 칸(상세 창에서 모두 보기). max 이하면 그대로.</summary>
        public static int VisibleCount(int total, int max) => total <= max ? total : Mathf.Max(0, max - 1);
    }
}
