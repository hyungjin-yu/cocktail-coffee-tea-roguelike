using System;
using UnityEngine;

namespace CCT
{
    /// <summary>플레이어 스탯. 기본값 + 카드로 쌓인 보너스를 합산해 최종값을 낸다.</summary>
    public class PlayerStats
    {
        /// <summary>06:23 — 연속타/동시발사 정수 합계 상한. 카드/노드 출처 수 기준 안전판이며 정상 플레이에서
        /// 이 값에 못 미치게 임의로 잘라내는 용도가 아니다.</summary>
        public const int MaxExtraThrusts = 7;
        public const int MaxPierceCount = 6;

        public float baseMoveSpeed = 5f;
        public float baseMaxHp = 100f;
        public float baseDamage = 12f;
        public float baseAttackInterval = 0.7f;
        public float baseRange = 2.2f;
        public float basePickupRadius = 2.5f;

        public float damagePercent;
        public float attackSpeedPercent;
        public float rangePercent;
        public float moveSpeedPercent;
        public float maxHpFlat;
        public float dropChancePercent;
        public float gaugeNeedPercent;
        public int extraThrusts;
        public int flameLevel;
        /// <summary>차 스킬 — 초당 회복량 보너스.</summary>
        public float regenBonus;
        /// <summary>차 스킬 — 독 오라 레벨.</summary>
        public int poisonLevel;
        /// <summary>원거리 전용 — 발사체가 뚫고 지나갈 수 있는 추가 대상 수.</summary>
        public int pierceCount;
        /// <summary>영구 강화(스페셜티) 전용 — 커피 원두 획득량 % 보너스. GameManager.BeanGainMultiplier가 읽는다.</summary>
        public float beanGainPercent;
        /// <summary>받은 피해 감소율 (0~0.25로 clamp). 단발/장판 피해 모두에 적용, 0.8초 무적과는 별개.</summary>
        public float damageReduction;
        /// <summary>줍기 반경 가산치 (커피 노드 wide_tray/zero_waste 등).</summary>
        public float pickupRadiusFlat;

        public event Action Changed;

        // 41 히든 스킬 런 중 보정(HiddenSkillRuntime이 매 프레임 설정). 공속/이동은 다른 % 보너스처럼 환불 봉인을 받는다.
        public float hiddenAttackSpeed;
        public float hiddenMoveSpeed;
        /// <summary>72 §4 장착 숙련 특성(런 시작 스냅샷, MasteryRuntime이 매 프레임 갱신). 기존 이동·공격 속도·피해 감소 상한에 합산.</summary>
        public float masteryMoveSpeed, masteryAttackSpeed, masteryDamageReduction;
        public float hiddenRegenMultiplier = 1f;
        public float jobDamagePercent, jobAttackSpeedPercent, jobRegenBonus;
        /// <summary>받는 둔화 약화 비율(0.4면 둔화량의 40%를 덜 받음).</summary>
        public float slowResist;
        public float suppressDurationScale = 1f;
        /// <summary>69 준비 음료(런 입장 시 1회 적용): 기존 치명타 규칙(직접 타격 ×2)의 발동 확률 가산, 재사용 시간 감소 비율.</summary>
        public float drinkCritChance;
        public float drinkCooldownReduction;
        public float CooldownScale => 1f - Mathf.Clamp(drinkCooldownReduction, 0f, DrinkRules.Cap(DrinkStat.CooldownReduction));

        float suppressedRemaining;
        float slowRemaining;
        float slowMultiplier = 1f;
        float flameLockRemaining;
        float poisonLockRemaining;
        float hybridLockRemaining;

        /// <summary>진상 손님 "환불해주세요!" 등으로 공격/이동 % 보너스가 무효화된 상태.</summary>
        public bool BonusesSuppressed => suppressedRemaining > 0f;
        public float SuppressedRemaining => suppressedRemaining;

        /// <summary>공격력·공속·사거리·이동속도 % 보너스를 duration 초 동안 무효화 (체력·드랍 관련은 유지).</summary>
        public void SuppressBonuses(float duration)
        {
            suppressedRemaining = Mathf.Max(suppressedRemaining, duration * suppressDurationScale);
        }

        /// <summary>슈가 큐브 바머 등이 거는 이동속도 둔화. 더 강하거나(낮은 배율) 더 긴 효과로만 갱신된다.</summary>
        public bool IsSlowed => slowRemaining > 0f;

        public void ApplySlow(float duration, float multiplier)
        {
            slowRemaining = Mathf.Max(slowRemaining, duration);
            multiplier = 1f - (1f - multiplier) * (1f - Mathf.Clamp01(slowResist));
            slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
        }

        /// <summary>진상 손님 "주문한 거 아닌데?" — 화염/독 주기 스킬을 duration초 동안 잠근다.
        /// 스킬 자체(Lv)는 그대로고, FlameBurst/PoisonAura가 잠긴 동안 자기 타이머를 멈춰서
        /// 남은 시간을 보존한다(06:20 "타이머도 멈췄다가 남은 시간부터 재개").</summary>
        public bool FlameLocked => flameLockRemaining > 0f;
        public bool PoisonLocked => poisonLockRemaining > 0f;
        /// <summary>07:119 — "주문 부정은 보조 활만 정지". 화염/독과 같은 잠금 패턴.</summary>
        public bool HybridLocked => hybridLockRemaining > 0f;
        public void LockFlame(float duration) => flameLockRemaining = Mathf.Max(flameLockRemaining, duration);
        public void LockPoison(float duration) => poisonLockRemaining = Mathf.Max(poisonLockRemaining, duration);
        public void LockHybrid(float duration) => hybridLockRemaining = Mathf.Max(hybridLockRemaining, duration);

        public void Tick(float deltaTime)
        {
            if (suppressedRemaining > 0f)
                suppressedRemaining = Mathf.Max(0f, suppressedRemaining - deltaTime);

            if (slowRemaining > 0f)
            {
                slowRemaining = Mathf.Max(0f, slowRemaining - deltaTime);
                if (slowRemaining <= 0f) slowMultiplier = 1f;
            }

            // 06:144 — 선택 UI에서 시간을 보내도(Time.timeScale=0) 남은 봉인 시간은 줄지 않는다.
            // Tick은 GameManager.Update에서 Playing 상태일 때만 호출되므로 이미 그 조건을 만족한다.
            if (flameLockRemaining > 0f) flameLockRemaining = Mathf.Max(0f, flameLockRemaining - deltaTime);
            if (poisonLockRemaining > 0f) poisonLockRemaining = Mathf.Max(0f, poisonLockRemaining - deltaTime);
            if (hybridLockRemaining > 0f) hybridLockRemaining = Mathf.Max(0f, hybridLockRemaining - deltaTime);
        }

        float Live(float percent) => BonusesSuppressed ? 0f : percent;

        public float DamageBonus => Live(damagePercent + jobDamagePercent);
        /// <summary>환불("환불해주세요!") 중엔 0 — 보조 활의 자체 간격 공식(07:118)도 이걸 통해 무효화를 받는다.</summary>
        public float AttackSpeedBonus => Live(attackSpeedPercent + hiddenAttackSpeed + jobAttackSpeedPercent + masteryAttackSpeed);
        public float MoveSpeed => Mathf.Min(10f, baseMoveSpeed * (1f + Live(moveSpeedPercent + hiddenMoveSpeed + masteryMoveSpeed))) * (IsSlowed ? slowMultiplier : 1f);
        public float MaxHp => baseMaxHp + maxHpFlat;
        public float Damage => baseDamage * (1f + DamageBonus);
        /// <summary>기본 공격 재사용 간격. 69 음료 재사용 시간 감소는 간격에 곱하고(공격 속도와 별개), 기존 최소 간격 0.12초 상한은 그대로.</summary>
        public float AttackInterval => Mathf.Max(0.12f, baseAttackInterval * CooldownScale / (1f + Live(attackSpeedPercent + hiddenAttackSpeed + jobAttackSpeedPercent + masteryAttackSpeed)));
        public float Range => baseRange * (1f + Live(rangePercent));
        public float PickupRadius => basePickupRadius + pickupRadiusFlat;
        public float ExtraDropChance => Mathf.Clamp01(dropChancePercent);
        /// <summary>받은 피해에 곱할 배율 (1 - clamp(damageReduction, 0, 0.25)).</summary>
        public float DamageTakenMultiplier => 1f - Mathf.Clamp(damageReduction + masteryDamageReduction, 0f, 0.25f);
        /// <summary>실제 초당 회복량. 음수로 떨어지지 않게 하한 0.</summary>
        public float RegenPerSecond => Mathf.Max(0f, Balance.PlayerRegenPerSecond + regenBonus + jobRegenBonus) * Mathf.Max(1f, hiddenRegenMultiplier);

        /// <summary>게이지 필요량 배율. 과도한 감소 방지를 위해 하한 0.5.</summary>
        public float GaugeNeedMultiplier => Mathf.Max(0.5f, 1f + gaugeNeedPercent);

        public void Apply(CardEffect effect)
        {
            switch (effect.type)
            {
                case CardEffectType.DamagePercent: damagePercent += effect.value; break;
                case CardEffectType.AttackSpeedPercent: attackSpeedPercent += effect.value; break;
                case CardEffectType.RangePercent: rangePercent += effect.value; break;
                case CardEffectType.MoveSpeedPercent: moveSpeedPercent += effect.value; break;
                case CardEffectType.MaxHpFlat: maxHpFlat += effect.value; break;
                case CardEffectType.DropChancePercent: dropChancePercent += effect.value; break;
                case CardEffectType.GaugeNeedPercent: gaugeNeedPercent += effect.value; break;
                case CardEffectType.ExtraThrust:
                    extraThrusts = Mathf.Min(MaxExtraThrusts, extraThrusts + Mathf.RoundToInt(effect.value));
                    break;
                case CardEffectType.FlameLevel: flameLevel += Mathf.RoundToInt(effect.value); break;
                case CardEffectType.RegenBonus: regenBonus += effect.value; break;
                case CardEffectType.PoisonLevel: poisonLevel += Mathf.RoundToInt(effect.value); break;
                case CardEffectType.PierceCount:
                    pierceCount = Mathf.Min(MaxPierceCount, pierceCount + Mathf.RoundToInt(effect.value));
                    break;
                case CardEffectType.BeanGainPercent: beanGainPercent += effect.value; break;
                case CardEffectType.DamageReduction: damageReduction += effect.value; break;
                case CardEffectType.PickupRadiusFlat: pickupRadiusFlat += effect.value; break;
            }
            Changed?.Invoke();
        }
    }
}
