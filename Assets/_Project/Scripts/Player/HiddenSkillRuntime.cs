using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>41 히든 스킬의 런 중 효과. 플레이어(런 소속)에 붙어 런과 함께 사라진다.
    /// 보조 효과(반격·처형·치명타)는 다른 히든/상태/최종 형태를 다시 발동하지 않는다 — 직접 타격 훅은 무기의 원래 타격에서만 불린다.</summary>
    public sealed class HiddenSkillRuntime : MonoBehaviour
    {
        public const float BarrierInterval = 20f;
        public const float MagnetInterval = 12f;
        public const float StillSeconds = 0.5f;
        public const float SpeedBurstSeconds = 2f;
        public const float ComboWindow = 1f;
        public const int ComboMax = 10;
        public const float KillHealPerSecondCap = 4f;
        public const float PickupHealPerSecondCap = 3f;
        public const float ThornsCooldown = 0.5f;
        public const float MaxTotalDamageReduction = 0.35f;

        PlayerController player;
        RunHiddenSkills Hidden => player.Hidden;

        float barrierTimer = BarrierInterval;
        float magnetTimer = MagnetInterval;
        float stillTime;
        float speedBurst;
        float comboTimer;
        float healWindow, killHealUsed, pickupHealUsed;
        readonly Dictionary<Enemy, float> thornsReady = new Dictionary<Enemy, float>();

        public bool BarrierReady { get; private set; }
        public int Combo { get; private set; }
        public bool Standing => stillTime >= StillSeconds;
        public string LastActivation { get; private set; } = "";
        public float LastActivationRemaining { get; private set; }
        public int ActivationSerial { get; private set; }
        float feedbackReady;

        public string StateLabel(HiddenSkill skill)
        {
            switch (skill)
            {
                case HiddenSkill.IcedCup: return BarrierReady ? "보호막 준비 완료 · 다음 단발 피해 차단" : $"보호막 재충전 {Mathf.CeilToInt(barrierTimer)}초";
                case HiddenSkill.SodaPump: return $"재료 끌어당기기 {Mathf.CeilToInt(magnetTimer)}초 후";
                case HiddenSkill.ShakeRhythm: return $"콤보 {Combo}/{ComboMax} · 직접 피해 +{Combo*2}%";
                case HiddenSkill.BitterWake: return player.Hp <= player.MaxHp*.35f ? "발동 중 · 공격 속도 +25%" : "대기 · 체력 35% 이하에서 발동";
                case HiddenSkill.HerbGarden: return Standing ? "발동 중 · 회복량 2배" : "대기 · 0.5초 정지 필요";
                case HiddenSkill.WeekendRush: return speedBurst > 0f ? $"발동 중 · 이동 +15% · {speedBurst:0.0}초" : "대기 · 일반 적 처치 시 발동";
                case HiddenSkill.ThickMug: return $"적용 중 · 직접 피해 +{ThickMugBonus(player.MaxHp)*100f:0}%";
                case HiddenSkill.BarTempo: return Standing ? "정지 보너스 · 받는 피해 -10%" : player.IsMovingInput ? "이동 보너스 · 피해 +10%" : "정지 보너스 준비 중";
                default: return HiddenSkillDatabase.Get(skill).effect;
            }
        }

        void Notify(HiddenSkill skill, Vector2 position, Color color)
        {
            // 많은 탄이 한 프레임에 맞아도 텍스트/VFX를 무한 생성하지 않는다.
            if (Time.time < feedbackReady) return;
            feedbackReady = Time.time + .35f;
            LastActivation = HiddenSkillDatabase.Get(skill).name + " · " + StateLabel(skill);
            LastActivationRemaining = 3f;
            ActivationSerial++;
            var fx = FadeVfx.Spawn(SpriteFactory.Crescent, position + Vector2.up*.4f,
                35f, new Vector2(.4f,.4f), new Vector2(.65f,.65f), color, .25f, SortOrders.AirVfx);
            fx.transform.SetParent(player.transform.parent, true); // 런 종료/재시작 시 잔상도 함께 정리
        }
        public void SetBarrierReadyForTests() => BarrierReady = true;

        public static HiddenSkillRuntime Attach(PlayerController player)
        {
            var rt = player.gameObject.AddComponent<HiddenSkillRuntime>();
            rt.player = player;
            return rt;
        }

        void Update()
        {
            if (player == null || player.IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            LastActivationRemaining = Mathf.Max(0f, LastActivationRemaining - dt);
            var stats = player.Stats;

            stillTime = player.IsMovingInput ? 0f : stillTime + dt;
            healWindow += dt;
            if (healWindow >= 1f) { healWindow = 0f; killHealUsed = 0f; pickupHealUsed = 0f; }

            if (Hidden.Has(HiddenSkill.IcedCup) && !BarrierReady)
            {
                barrierTimer -= dt;
                if (barrierTimer <= 0f) { BarrierReady = true; barrierTimer = BarrierInterval; Notify(HiddenSkill.IcedCup, player.Position, Color.cyan); }
            }
            if (Hidden.Has(HiddenSkill.SodaPump))
            {
                magnetTimer -= dt;
                if (magnetTimer <= 0f)
                {
                    magnetTimer = MagnetInterval;
                    foreach (var pickup in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
                        if (pickup != null && pickup.RunPlayer == player) pickup.ForceAttract();
                    Notify(HiddenSkill.SodaPump, player.Position, Color.cyan);
                }
            }
            if (comboTimer > 0f) { comboTimer -= dt; if (comboTimer <= 0f) Combo = 0; }
            if (speedBurst > 0f) speedBurst -= dt;

            stats.hiddenAttackSpeed = Hidden.Has(HiddenSkill.BitterWake) && player.Hp <= player.MaxHp * 0.35f ? 0.25f : 0f;
            stats.hiddenMoveSpeed = Hidden.Has(HiddenSkill.WeekendRush) && speedBurst > 0f ? 0.15f : 0f;
            stats.hiddenRegenMultiplier = Hidden.Has(HiddenSkill.HerbGarden) && Standing ? 2f : 1f;
            bool calm = Hidden.Has(HiddenSkill.CalmService);
            stats.slowResist = calm ? 0.4f : 0f;
            stats.suppressDurationScale = calm ? 0.6f : 1f;
        }

        // ── 직접 타격(무기 원래 타격만) ─────────────────────

        /// <summary>직접 타격 피해에 히든 보정을 곱한다. 조건부 증가는 더하고, 치명타는 마지막에 곱한다.</summary>
        public float ModifyDirectDamage(Enemy enemy, float damage)
        {
            if (enemy == null || Hidden.Count == 0) return damage;
            float bonus = 0f;
            float dist = Vector2.Distance(player.Position, enemy.transform.position);
            if (Hidden.Has(HiddenSkill.FirstSip) && enemy.Hp >= enemy.MaxHp - 0.001f) bonus += 0.40f;
            if (Hidden.Has(HiddenSkill.LongReach) && dist > player.Stats.Range * 0.7f) bonus += 0.20f;
            if (Hidden.Has(HiddenSkill.PointBlank) && dist <= 2.5f) bonus += 0.20f;
            if (Hidden.Has(HiddenSkill.VipService) && enemy.Def.IsBoss) bonus += 0.15f;
            if (Hidden.Has(HiddenSkill.ThickMug)) bonus += ThickMugBonus(player.MaxHp);
            if (Hidden.Has(HiddenSkill.ShakeRhythm)) bonus += 0.02f * Combo;
            if (Hidden.Has(HiddenSkill.BarTempo) && !Standing && player.IsMovingInput) bonus += 0.10f;
            float result = damage * (1f + bonus);
            if (Hidden.Has(HiddenSkill.PerfectCup) && Random.value < 0.10f)
            {
                result *= 2f;
                Notify(HiddenSkill.PerfectCup, enemy.transform.position, Color.yellow);
            }
            else if (bonus > 0f)
            {
                HiddenSkill effect = Hidden.Has(HiddenSkill.FirstSip) && enemy.Hp >= enemy.MaxHp-.001f ? HiddenSkill.FirstSip
                    : Hidden.Has(HiddenSkill.PointBlank) && dist <= 2.5f ? HiddenSkill.PointBlank
                    : Hidden.Has(HiddenSkill.VipService) && enemy.Def.IsBoss ? HiddenSkill.VipService
                    : Hidden.Has(HiddenSkill.LongReach) && dist > player.Stats.Range*.7f ? HiddenSkill.LongReach
                    : Hidden.Has(HiddenSkill.ThickMug) && ThickMugBonus(player.MaxHp)>0f ? HiddenSkill.ThickMug
                    : Hidden.Has(HiddenSkill.ShakeRhythm) && Combo>0 ? HiddenSkill.ShakeRhythm : HiddenSkill.BarTempo;
                Notify(effect, enemy.transform.position, new Color(1f,.8f,.35f));
            }
            return result;
        }

        public static float ThickMugBonus(float maxHp) => Mathf.Clamp(Mathf.Floor(Mathf.Max(0f, maxHp - 100f) / 20f) * 0.01f, 0f, 0.20f);

        /// <summary>직접 타격 직후: 콤보 누적, 이중 마무리 처형(일반 적, 다른 효과 미발동).</summary>
        public void AfterDirectHit(Enemy enemy)
        {
            if (Hidden.Has(HiddenSkill.ShakeRhythm))
            {
                Combo = Mathf.Min(ComboMax, Combo + 1);
                comboTimer = ComboWindow;
            }
            if (Hidden.Has(HiddenSkill.DoubleFinish) && enemy != null && !enemy.IsDead && !enemy.Def.IsBoss &&
                enemy.Hp < enemy.MaxHp * 0.12f)
            {
                Notify(HiddenSkill.DoubleFinish, enemy.transform.position, Color.yellow);
                enemy.TakeDamage(enemy.Hp);
            }
        }

        // ── 처치/줍기/보스 ─────────────────────

        public void OnEnemyKilled(EnemyDef def)
        {
            if (def == null || def.IsBoss) return;
            if (Hidden.Has(HiddenSkill.HotToddy))
            {
                float before = player.Hp;
                killHealUsed += HealFlat(1f, KillHealPerSecondCap - killHealUsed);
                if (player.Hp > before) Notify(HiddenSkill.HotToddy, player.Position, Color.green);
            }
            if (Hidden.Has(HiddenSkill.WeekendRush)) { speedBurst = SpeedBurstSeconds; Notify(HiddenSkill.WeekendRush, player.Position, Color.cyan); }
        }

        public void OnPickup()
        {
            if (Hidden.Has(HiddenSkill.BeanCrate))
            {
                float before = player.Hp;
                pickupHealUsed += HealFlat(0.5f, PickupHealPerSecondCap - pickupHealUsed);
                if (player.Hp > before) Notify(HiddenSkill.BeanCrate, player.Position, Color.green);
            }
        }

        public void OnBossSpawned()
        {
            if (!Hidden.Has(HiddenSkill.TriRoast) || player.IsDead) return;
            player.GrantInvulnerability(2f);
            player.Heal(0.15f);
            Notify(HiddenSkill.TriRoast, player.Position, Color.cyan);
        }

        float HealFlat(float amount, float budget)
        {
            float give = Mathf.Min(amount, Mathf.Max(0f, budget));
            if (give <= 0f) return 0f;
            player.HealFlat(give);
            return give;
        }

        // ── 피격 ─────────────────────

        /// <summary>단발 피격 직전: 얼음 찻잔 보호막이 있으면 소비하고 피해를 막는다(true).</summary>
        public bool TryBlockHit()
        {
            if (!Hidden.Has(HiddenSkill.IcedCup) || !BarrierReady) return false;
            BarrierReady = false;
            barrierTimer = BarrierInterval;
            Notify(HiddenSkill.IcedCup, player.Position, Color.cyan);
            return true;
        }

        /// <summary>피해가 실제로 들어간 뒤: 셰이킹 리듬 콤보 초기화.</summary>
        public void OnDamaged() => Combo = 0;

        /// <summary>추가 받는 피해 배율(바 템포 정지 시 -10%). 기존 감소와 합쳐 총 35% 상한.</summary>
        public float ExtraTakenMultiplier(float existing)
        {
            float extra = Hidden.Has(HiddenSkill.BarTempo) && Standing ? 0.9f : 1f;
            return Mathf.Max(1f - MaxTotalDamageReduction, existing * extra) / Mathf.Max(0.0001f, existing);
        }

        /// <summary>접촉 피해 보정(독초 정원) — 중독된 적의 접촉 -15%.</summary>
        public float ModifyContactDamage(Enemy source, float amount)
        {
            if (source == null || !Hidden.Has(HiddenSkill.PoisonGarden)) return amount;
            var status = source.GetComponent<EnemyHitStatus>();
            return status != null && status.PoisonRemaining > 0f ? amount * 0.85f : amount;
        }

        /// <summary>쟁반 방패: 접촉 피해가 실제로 들어갔을 때 그 적에게 기본 피해 100% 반격(적마다 0.5초, 상태·히든 미발동).</summary>
        public void OnContactHit(Enemy source)
        {
            if (source == null || source.IsDead || source.Invulnerable || !Hidden.Has(HiddenSkill.TrayGuard)) return;
            float now = Time.time;
            if (thornsReady.TryGetValue(source, out var ready) && now < ready) return;
            thornsReady[source] = now + ThornsCooldown;
            if (thornsReady.Count > 64) thornsReady.Clear(); // 죽은 적 키 누적 방지(상한만 지키면 됨)
            source.TakeDamage(player.Stats.Damage);
            Notify(HiddenSkill.TrayGuard, source.transform.position, Color.yellow);
        }
    }
}
