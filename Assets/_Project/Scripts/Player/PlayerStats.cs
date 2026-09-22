using System;
using UnityEngine;

namespace CCT
{
    /// <summary>플레이어 스탯. 기본값 + 카드로 쌓인 보너스를 합산해 최종값을 낸다.</summary>
    public class PlayerStats
    {
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

        public event Action Changed;

        float suppressedRemaining;
        float slowRemaining;
        float slowMultiplier = 1f;

        /// <summary>진상 손님 "환불해주세요!" 등으로 공격/이동 % 보너스가 무효화된 상태.</summary>
        public bool BonusesSuppressed => suppressedRemaining > 0f;
        public float SuppressedRemaining => suppressedRemaining;

        /// <summary>공격력·공속·사거리·이동속도 % 보너스를 duration 초 동안 무효화 (체력·드랍 관련은 유지).</summary>
        public void SuppressBonuses(float duration)
        {
            suppressedRemaining = Mathf.Max(suppressedRemaining, duration);
        }

        /// <summary>슈가 큐브 바머 등이 거는 이동속도 둔화. 더 강하거나(낮은 배율) 더 긴 효과로만 갱신된다.</summary>
        public bool IsSlowed => slowRemaining > 0f;

        public void ApplySlow(float duration, float multiplier)
        {
            slowRemaining = Mathf.Max(slowRemaining, duration);
            slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
        }

        public void Tick(float deltaTime)
        {
            if (suppressedRemaining > 0f)
                suppressedRemaining = Mathf.Max(0f, suppressedRemaining - deltaTime);

            if (slowRemaining > 0f)
            {
                slowRemaining = Mathf.Max(0f, slowRemaining - deltaTime);
                if (slowRemaining <= 0f) slowMultiplier = 1f;
            }
        }

        float Live(float percent) => BonusesSuppressed ? 0f : percent;

        public float DamageBonus => Live(damagePercent);
        public float MoveSpeed => baseMoveSpeed * (1f + Live(moveSpeedPercent)) * (IsSlowed ? slowMultiplier : 1f);
        public float MaxHp => baseMaxHp + maxHpFlat;
        public float Damage => baseDamage * (1f + DamageBonus);
        public float AttackInterval => baseAttackInterval / (1f + Live(attackSpeedPercent));
        public float Range => baseRange * (1f + Live(rangePercent));
        public float PickupRadius => basePickupRadius;
        public float ExtraDropChance => Mathf.Clamp01(dropChancePercent);

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
                case CardEffectType.ExtraThrust: extraThrusts += Mathf.RoundToInt(effect.value); break;
                case CardEffectType.FlameLevel: flameLevel += Mathf.RoundToInt(effect.value); break;
                case CardEffectType.RegenBonus: regenBonus += effect.value; break;
                case CardEffectType.PoisonLevel: poisonLevel += Mathf.RoundToInt(effect.value); break;
                case CardEffectType.PierceCount: pierceCount += Mathf.RoundToInt(effect.value); break;
                case CardEffectType.BeanGainPercent: beanGainPercent += effect.value; break;
            }
            Changed?.Invoke();
        }
    }
}
