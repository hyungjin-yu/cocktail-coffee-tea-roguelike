using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>41 히든 스킬의 런 중 효과. 플레이어(런 소속)에 붙어 런과 함께 사라진다.
    /// 보조 효과(반격·처형·치명타·파동·파편·덤 베기·머그 강타)는 다른 히든/상태/최종 형태를 다시 발동하지 않는다 — 직접 타격 훅은 무기의 원래 타격에서만 불린다.
    /// #18(2026-11-24): 대표 6종(얼음 찻잔·완벽한 한 잔·쓴맛 각성·이중 마무리·근거리 산탄·두꺼운 머그)에 눈에 보이는 규칙 효과를 더했다. 해금 조건·id·도감은 그대로.</summary>
    public sealed class HiddenSkillRuntime : MonoBehaviour
    {
        /// <summary>#18 얼음 찻잔 재충전 20→15초(파동 추가와 함께 체감 주기 단축).</summary>
        public const float BarrierInterval = 15f;
        public const float MagnetInterval = 12f;
        public const float StillSeconds = 0.5f;
        public const float SpeedBurstSeconds = 2f;
        public const float ComboWindow = 1f;
        public const int ComboMax = 10;
        public const float KillHealPerSecondCap = 4f;
        public const float PickupHealPerSecondCap = 3f;
        public const float ThornsCooldown = 0.5f;
        public const float MaxTotalDamageReduction = 0.35f;

        // ── #18(2026-11-24) 대표 6종 전투 체감 개편 ──
        /// <summary>얼음 찻잔 파동: 막는 순간 반경 안 일반 적 정지(피해 없음, 같은 적 재사용 대기, 처리 상한). 보스 제외.</summary>
        public const float FrostRadius = 2.5f, FrostFreezeSeconds = 1.5f, FrostFreezeCooldown = 5f;
        public const int FrostMaxTargets = 12;
        /// <summary>완벽한 한 잔: 직접 타격 N회째마다 확정 배율(확률 없음).</summary>
        public const int PerfectEvery = 7;
        public const float PerfectMultiplier = 3f;
        /// <summary>쓴맛 각성: 기존 35% 이하 상시 공속 +25%는 그대로, 35% 아래로 떨어지는 순간 각성(추가 공속·짧은 무적·재사용 대기).</summary>
        public const float BitterWakeThreshold = 0.35f, BitterWakeAttackSpeed = 0.25f;
        public const float AwakenAttackSpeed = 0.35f, AwakenSeconds = 4f, AwakenCooldown = 45f, AwakenInvulnerable = 1f;
        /// <summary>이중 마무리: 처형 기준 12→15%, 처형 시 가장 가까운 적 1체에 덤 베기(기본 피해 배율, 초당 상한, 연쇄 없음).</summary>
        public const float ExecuteThreshold = 0.15f, EchoRange = 3f, EchoDamage = 1f;
        public const int EchoPerSecondCap = 4;
        /// <summary>근거리 산탄: 2.5 안 직접 타격 +20%는 그대로, 일반 적 밀어내기 + 주변 파편(연쇄 없음, 초당 상한).</summary>
        public const float PointBlankRange = 2.5f, PointBlankBonus = 0.20f, ShoveDistance = 1.2f, ShoveCooldown = 0.6f;
        public const float ShrapnelRadius = 1.2f, ShrapnelDamage = 0.4f;
        public const int ShrapnelTargets = 2, ShrapnelPerSecondCap = 12;
        /// <summary>두꺼운 머그: 기존 최대 체력 비례 보너스는 그대로, N초마다 다음 직접 타격에 최대 체력 비례 추가 피해.</summary>
        public const float MugInterval = 8f, MugHpFraction = 0.10f, MugBossHpFraction = 0.05f, MugStaggerSeconds = 0.4f, MugStaggerCooldown = 2f;
        /// <summary>#23 근거리 산탄(바텐더·총 전용): 적중한 발사 VolleyHitsPerBurst회마다 그 3번째 적중 지점 폭발.
        /// 한 번의 발사(다중 발사·관통·교차 확산 보조탄 포함)는 적중을 1회만 센다. 빗나간 발사는 세지 않는다. 폭발 피해는 TakeDamage(Effect)라 치명타·히든·상태를 다시 부르지 않는다.</summary>
        public const int VolleyHitsPerBurst = 3;
        public const float VolleyBurstRadius = 1.8f, VolleyBurstDamage = 1.2f, VolleyFlashSeconds = 0.35f;
        public const int VolleyBurstMaxTargets = 16;

        PlayerController player;
        RunHiddenSkills Hidden => player.Hidden;

        float barrierTimer = BarrierInterval;
        float magnetTimer = MagnetInterval;
        float stillTime;
        float speedBurst;
        float comboTimer;
        float healWindow, killHealUsed, pickupHealUsed;
        readonly Dictionary<Enemy, float> thornsReady = new Dictionary<Enemy, float>();
        int perfectCount;
        float awakenRemaining, awakenCooldown;
        bool wasLowHp;
        float mugTimer = MugInterval;
        int echoUsed, shrapnelUsed;
        readonly List<Enemy> nearBuffer = new List<Enemy>();
        // #23 바텐더 총 발사 묶음: 발사마다 새 번호, 최근에 센 번호 8개(같은 발사의 늦은 적중 중복 방지).
        int volleySerial, volleyHits;
        readonly int[] countedVolleys = new int[8];
        int countedCursor;
        float volleyFlash;
        GameManager game;
        GameManager Game => game != null ? game : (game = GetComponentInParent<GameManager>());

        public bool BarrierReady { get; private set; }
        public int Combo { get; private set; }
        public bool Standing => stillTime >= StillSeconds;
        /// <summary>2026-11-09 HUD 지속 표시용(읽기 전용): 얼음 찻잔 재충전 남은 초, 주말 러시 남은 초, 콤보 남은 창.</summary>
        public float BarrierRemaining => BarrierReady ? 0f : barrierTimer;
        public float SpeedBurstRemaining => Mathf.Max(0f, speedBurst);
        /// <summary>2026-11-16 피드백 25: 탄산 펌프 다음 재료 끌어당기기까지 남은 초(읽기 전용, HUD 충전 칸).</summary>
        public float MagnetRemaining => Mathf.Max(0f, magnetTimer);
        public bool BitterWakeActive => player != null && Hidden.Has(HiddenSkill.BitterWake) && player.Hp <= player.MaxHp * BitterWakeThreshold;
        /// <summary>#18 쓴맛 각성: 각성 남은 초 / 다음 각성까지 재사용 대기 남은 초(읽기 전용, HUD).</summary>
        public float AwakenRemaining => Mathf.Max(0f, awakenRemaining);
        public float AwakenCooldownRemaining => Mathf.Max(0f, awakenCooldown);
        /// <summary>#18 완벽한 한 잔: 이번 주기에 센 직접 타격 수(0..PerfectEvery-1). 다음 타격이 PerfectEvery번째면 완벽 추출.</summary>
        public int PerfectCount => perfectCount;
        /// <summary>#18 두꺼운 머그: 강타 준비 여부 / 재충전 남은 초.</summary>
        public bool MugReady => mugTimer <= 0f;
        public float MugRemaining => Mathf.Max(0f, mugTimer);
        /// <summary>#18 검수용 누적 발동 수(읽기 전용, 판정에 쓰지 않음).</summary>
        public int FrostFrozenTotal { get; private set; }
        public int PerfectTotal { get; private set; }
        public int AwakenTotal { get; private set; }
        public int EchoTotal { get; private set; }
        public int ShrapnelTotal { get; private set; }
        public int MugTotal { get; private set; }
        public int VolleyBurstTotal { get; private set; }
        /// <summary>#23 바텐더·총 + 근거리 산탄일 때만 발사 묶음 폭발이 켜진다(활·찻집·레이피어에는 퍼지지 않음).</summary>
        public bool GunVolleyActive => player != null && Hidden.Has(HiddenSkill.PointBlank) && Game != null &&
            Game.Job == Job.Bartender && Game.Weapon == WeaponKind.Gun;
        /// <summary>이번 판 무기가 총인지(근거리 산탄 무기별 분기). GameManager가 없으면(단독 시험) 총이 아닌 것으로 본다.</summary>
        bool PointBlankGun => Game != null && Game.Weapon == WeaponKind.Gun;
        /// <summary>내부 적중 발사 수(0..2). 폭발 순간 즉시 0으로 돌아간다.</summary>
        public int VolleyHits => volleyHits;
        /// <summary>머리 위 표시용: 폭발 직후 짧게 3/3을 보여 주는 동안 true(내부 카운터와 별개, 저효과 설정이면 0초).</summary>
        public bool VolleyFlashing => volleyFlash > 0f;
        public int VolleyPipsShown => VolleyFlashing ? VolleyHitsPerBurst : volleyHits;
        /// <summary>무기가 발사 직전에 부른다. 폭발이 켜져 있으면 새 발사 번호(1 이상), 아니면 0(세지 않음).</summary>
        public int NextGunVolley() => GunVolleyActive ? ++volleySerial : 0;

        /// <summary>발사 번호 volleyId의 탄이 실제로 적을 맞혔을 때(직접·보조탄 모두). 같은 번호는 한 번만 센다.</summary>
        public void OnGunVolleyHit(int volleyId, Vector2 at)
        {
            if (volleyId <= 0 || !GunVolleyActive || player.IsDead) return;
            for (int i = 0; i < countedVolleys.Length; i++) if (countedVolleys[i] == volleyId) return;
            countedVolleys[countedCursor] = volleyId;
            countedCursor = (countedCursor + 1) % countedVolleys.Length;
            volleyHits++;
            if (volleyHits < VolleyHitsPerBurst) return;
            volleyHits = 0; // 내부 카운터는 즉시 초기화
            volleyFlash = HitFeedback.LowEffects ? 0f : VolleyFlashSeconds;
            VolleyBurst(at);
        }

        /// <summary>#23 폭발: VolleyBurstRadius 안 적(보스 포함, 최대 VolleyBurstMaxTargets)에게 기본 피해 VolleyBurstDamage배. 치명타 판정 없음, 연쇄 없음.</summary>
        void VolleyBurst(Vector2 at)
        {
            VolleyBurstTotal++;
            CollectNear(at, VolleyBurstRadius, null, true, nearBuffer, VolleyBurstMaxTargets);
            float dmg = player.Stats != null ? player.Stats.Damage * VolleyBurstDamage : 0f;
            for (int i = 0; i < nearBuffer.Count; i++) nearBuffer[i].TakeDamage(dmg, HitKind.Effect);
            Notify(HiddenSkill.PointBlank, at, new Color(1f, .6f, .25f));
            Ring(at, 0.5f, VolleyBurstRadius * 2f, new Color(1f, 0.55f, 0.2f, 0.6f), 0.3f);
            AudioService.PlaySfx("explosion", at);
            CameraRig.Shake(0.1f, 0.12f);
        }
        public string LastActivation { get; private set; } = "";
        public float LastActivationRemaining { get; private set; }
        public int ActivationSerial { get; private set; }
        /// <summary>직전 ModifyDirectDamage가 치명타(음료 ×2 또는 완벽 추출 ×3)였는지 — 무기가 피격 연출 종류(HitKind.Critical)를 고를 때만 쓴다.</summary>
        public bool LastHitCritical { get; private set; }

        /// <summary>직접 타격 연출 종류: 치명타 > 강한 공격 > 일반.</summary>
        public static HitKind DirectKind(HiddenSkillRuntime hidden, bool heavy) =>
            hidden != null && hidden.LastHitCritical ? HitKind.Critical : heavy ? HitKind.Heavy : HitKind.Normal;
        float feedbackReady;
        HiddenSkill lastSkill;
        int activationCount;

        public string StateLabel(HiddenSkill skill)
        {
            switch (skill)
            {
                case HiddenSkill.IcedCup: return BarrierReady ? $"보호막 준비 완료 · 막으면 {IntNum.Distance(FrostRadius)} 안 일반 적 {IntNum.Duration(FrostFreezeSeconds)} 정지" : $"보호막 재충전 {IntNum.RemainingSeconds(barrierTimer)}초";
                case HiddenSkill.SodaPump: return $"재료 끌어당기기 {IntNum.RemainingSeconds(magnetTimer)}초 후";
                case HiddenSkill.ShakeRhythm: return $"콤보 {Combo}/{ComboMax} · 직접 피해 +{Combo*2}%";
                case HiddenSkill.BitterWake:
                    if (awakenRemaining > 0f) return $"각성 {IntNum.RemainingSeconds(awakenRemaining)}초 · 공격 속도 +{IntNum.Percent(CurrentBitterWakeSpeed())}%";
                    if (player.Hp <= player.MaxHp * BitterWakeThreshold) return $"발동 중 · 공격 속도 +{IntNum.Percent(BitterWakeAttackSpeed)}%";
                    return awakenCooldown > 0f ? $"대기 · 다음 각성까지 {IntNum.RemainingSeconds(awakenCooldown)}초" : "대기 · 체력 35% 아래로 떨어지면 각성";
                case HiddenSkill.HerbGarden: return Standing ? "발동 중 · 회복량 2배" : $"대기 · {IntNum.Duration(StillSeconds)} 정지 필요";
                case HiddenSkill.WeekendRush: return speedBurst > 0f ? $"발동 중 · 이동 +15% · {IntNum.RemainingSeconds(speedBurst)}초" : "대기 · 일반 적 처치 시 발동";
                case HiddenSkill.ThickMug: return (MugReady ? "머그 강타 준비" : $"머그 강타 {IntNum.RemainingSeconds(mugTimer)}초 후") + $" · 직접 피해 +{IntNum.Percent(ThickMugBonus(player.MaxHp))}%";
                case HiddenSkill.BarTempo: return Standing ? "정지 보너스 · 받는 피해 -10%" : player.IsMovingInput ? "이동 보너스 · 피해 +10%" : "정지 보너스 준비 중";
                case HiddenSkill.PerfectCup: return $"완벽 추출 {perfectCount}/{PerfectEvery} · {PerfectEvery}번째 직접 타격 피해 ×{IntNum.Round(PerfectMultiplier)}";
                case HiddenSkill.DoubleFinish: return $"체력 {IntNum.Percent(ExecuteThreshold)}% 미만 일반 적 처치 · 덤 베기 {IntNum.Distance(EchoRange)} 안 1체";
                case HiddenSkill.PointBlank: return PointBlankGun
                    ? $"폭발 준비 {VolleyPipsShown}/{VolleyHitsPerBurst} · {IntNum.Distance(PointBlankRange)} 안 직접 피해 +{IntNum.Percent(PointBlankBonus)}%"
                    : $"{IntNum.Distance(PointBlankRange)} 안 직접 피해 +{IntNum.Percent(PointBlankBonus)}% · 일반 적 밀어내기 · 파편 {ShrapnelTargets}체";
                default: return HiddenSkillDatabase.Get(skill).effect;
            }
        }

        void Notify(HiddenSkill skill, Vector2 position, Color color)
        {
            // 2026-11-09: 표시 중인 같은 효과가 다시 발동하면 버리지 않고 ×N으로 합친다(텍스트 1줄 유지, VFX는 아래 쿨다운).
            string name = JobStudyTheme.HiddenName(HiddenSkillDatabase.Get(skill),GetComponentInParent<GameManager>()?.Job ?? Job.Bartender);
            if (LastActivationRemaining > 0f && lastSkill == skill) activationCount++;
            else activationCount = 1;
            lastSkill = skill;
            if (activationCount > 1)
            {
                LastActivation = name + $" ×{activationCount}\n" + StateLabel(skill);
                LastActivationRemaining = 3f;
            }
            // 많은 탄이 한 프레임에 맞아도 텍스트/VFX를 무한 생성하지 않는다.
            if (Time.time < feedbackReady) return;
            feedbackReady = Time.time + .35f;
            LastActivation = name + (activationCount > 1 ? $" ×{activationCount}" : "") + "\n" + StateLabel(skill);
            LastActivationRemaining = 3f;
            ActivationSerial++;
            var fx = FadeVfx.Spawn(SpriteFactory.Crescent, position + Vector2.up*.4f,
                35f, new Vector2(.4f,.4f), new Vector2(.65f,.65f), color, .25f, SortOrders.AirVfx);
            fx.transform.SetParent(player.transform.parent, true); // 런 종료/재시작 시 잔상도 함께 정리
        }
        public void SetBarrierReadyForTests() => BarrierReady = true;
        /// <summary>테스트/QA: 각성 재사용 대기만 초기화(조건 판정·수치는 그대로).</summary>
        public void ResetAwakenForTests() { awakenCooldown = 0f; awakenRemaining = 0f; wasLowHp = false; }
        public void SetMugReadyForTests() => mugTimer = 0f;

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
            if (healWindow >= 1f) { healWindow = 0f; killHealUsed = 0f; pickupHealUsed = 0f; echoUsed = 0; shrapnelUsed = 0; }

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
            if (awakenRemaining > 0f) awakenRemaining -= dt;
            if (awakenCooldown > 0f) awakenCooldown -= dt;
            if (Hidden.Has(HiddenSkill.BitterWake))
            {
                // #18: 35% 위→아래로 넘어가는 순간만 각성(상시 +25%는 그대로). 재사용 대기 중이면 새로 걸지 않는다.
                bool low = player.Hp <= player.MaxHp * BitterWakeThreshold;
                if (low && !wasLowHp && awakenCooldown <= 0f) StartAwakening();
                wasLowHp = low;
            }
            if (Hidden.Has(HiddenSkill.ThickMug) && mugTimer > 0f) mugTimer -= dt;
            if (volleyFlash > 0f) volleyFlash -= dt;

            stats.hiddenAttackSpeed = CurrentBitterWakeSpeed();
            stats.hiddenMoveSpeed = Hidden.Has(HiddenSkill.WeekendRush) && speedBurst > 0f ? 0.15f : 0f;
            stats.hiddenRegenMultiplier = Hidden.Has(HiddenSkill.HerbGarden) && Standing ? 2f : 1f;
            bool calm = Hidden.Has(HiddenSkill.CalmService);
            stats.slowResist = calm ? 0.4f : 0f;
            stats.suppressDurationScale = calm ? 0.6f : 1f;
        }

        /// <summary>쓴맛 각성 공속: 35% 이하 상시 +25% + 각성 중 +35%(합 최대 +60%). 기존 공격 간격 하한(0.12초)은 PlayerStats가 그대로 지킨다.</summary>
        float CurrentBitterWakeSpeed()
        {
            if (player == null || !Hidden.Has(HiddenSkill.BitterWake)) return 0f;
            float v = player.Hp <= player.MaxHp * BitterWakeThreshold ? BitterWakeAttackSpeed : 0f;
            if (awakenRemaining > 0f) v += AwakenAttackSpeed;
            return v;
        }

        void StartAwakening()
        {
            awakenRemaining = AwakenSeconds;
            awakenCooldown = AwakenCooldown;
            AwakenTotal++;
            player.GrantInvulnerability(AwakenInvulnerable);
            Notify(HiddenSkill.BitterWake, player.Position, new Color(1f, .35f, .3f));
            Ring(player.Position, 0.6f, 3.2f, new Color(0.85f, 0.18f, 0.15f, 0.55f), 0.45f);
            CameraRig.Shake(0.12f, 0.15f);
        }

        /// <summary>표시 전용 원형 파동(판정 없음). 런 루트에 붙여 런 종료 시 함께 정리.</summary>
        void Ring(Vector2 at, float fromDiameter, float toDiameter, Color color, float life)
        {
            var fx = FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * fromDiameter, Vector2.one * toDiameter, color, life, SortOrders.AirVfx);
            if (player != null && player.transform.parent != null) fx.transform.SetParent(player.transform.parent, true);
        }

        /// <summary>런 소속의 살아 있는 적 중 at에서 radius 안(except 제외, 무적 제외). includeBoss=false면 보스 제외. 최대 max개.</summary>
        void CollectNear(Vector2 at, float radius, Enemy except, bool includeBoss, List<Enemy> into, int max)
        {
            into.Clear();
            float r2 = radius * radius;
            foreach (var e in Enemy.Active)
            {
                if (into.Count >= max) break;
                if (e == null || e == except || e.IsDead || e.Invulnerable || e.RunPlayer != player || e.Def == null) continue;
                if (!includeBoss && e.Def.IsBoss) continue;
                if (((Vector2)e.transform.position - at).sqrMagnitude <= r2) into.Add(e);
            }
        }

        // ── 직접 타격(무기 원래 타격만) ─────────────────────

        /// <summary>직접 타격 피해에 히든 보정을 곱한다. 조건부 증가는 더하고, 치명타(음료 ×2)·완벽 추출(×3)은 마지막에 곱한다.</summary>
        public float ModifyDirectDamage(Enemy enemy, float damage)
        {
            LastHitCritical = false;
            if (enemy == null) return damage;
            float drinkCrit = player.Stats != null ? player.Stats.drinkCritChance : 0f;
            float dist = Vector2.Distance(player.Position, enemy.transform.position);
            // 72 §4 장착 숙련: 같은 합산 보너스 자리·같은 ×2 치명타 판정(새 전투 시스템 없음)
            var mastery = player.Mastery;
            float masteryBonus = mastery != null ? mastery.DirectBonus(enemy, dist) : 0f;
            drinkCrit += mastery != null ? mastery.CritBonus : 0f;
            if (Hidden.Count == 0)
            {
                // 69 준비 음료 치명타 확률: 기존 치명타 규칙(×2)만 사용, 새 전투 시스템 없음.
                float plain = damage * (1f + masteryBonus);
                if (drinkCrit > 0f && Random.value < drinkCrit)
                {
                    LastHitCritical = true;
                    NotifyDrinkCrit();
                    return plain * 2f;
                }
                return plain;
            }
            float bonus = masteryBonus;
            if (Hidden.Has(HiddenSkill.FirstSip) && enemy.Hp >= enemy.MaxHp - 0.001f) bonus += 0.40f;
            if (Hidden.Has(HiddenSkill.LongReach) && dist > player.Stats.Range * 0.7f) bonus += 0.20f;
            if (Hidden.Has(HiddenSkill.PointBlank) && dist <= PointBlankRange) bonus += PointBlankBonus;
            if (Hidden.Has(HiddenSkill.VipService) && enemy.Def.IsBoss) bonus += 0.15f;
            if (Hidden.Has(HiddenSkill.ThickMug)) bonus += ThickMugBonus(player.MaxHp);
            if (Hidden.Has(HiddenSkill.ShakeRhythm)) bonus += 0.02f * Combo;
            if (Hidden.Has(HiddenSkill.BarTempo) && !Standing && player.IsMovingInput) bonus += 0.10f;
            float result = damage * (1f + bonus);
            // #18 완벽한 한 잔: 확률(10% ×2) 대신 직접 타격 PerfectEvery번째마다 확정 ×3.
            // 완벽 추출 타격에는 음료 치명타(×2)를 따로 굴리지 않는다(곱해서 ×6이 되지 않음).
            bool perfect = false;
            if (Hidden.Has(HiddenSkill.PerfectCup))
            {
                perfectCount++;
                if (perfectCount >= PerfectEvery) { perfectCount = 0; perfect = true; }
            }
            if (perfect)
            {
                result *= PerfectMultiplier;
                LastHitCritical = true;
                PerfectTotal++;
                Notify(HiddenSkill.PerfectCup, enemy.transform.position, Color.yellow);
                Ring(enemy.transform.position, 0.4f, 1.6f, new Color(1f, 0.85f, 0.3f, 0.7f), 0.3f);
            }
            else if (drinkCrit > 0f && Random.value < drinkCrit)
            {
                result *= 2f;
                LastHitCritical = true;
                NotifyDrinkCrit();
            }
            else if (bonus > 0f)
            {
                HiddenSkill effect = Hidden.Has(HiddenSkill.FirstSip) && enemy.Hp >= enemy.MaxHp-.001f ? HiddenSkill.FirstSip
                    : Hidden.Has(HiddenSkill.PointBlank) && dist <= PointBlankRange ? HiddenSkill.PointBlank
                    : Hidden.Has(HiddenSkill.VipService) && enemy.Def.IsBoss ? HiddenSkill.VipService
                    : Hidden.Has(HiddenSkill.LongReach) && dist > player.Stats.Range*.7f ? HiddenSkill.LongReach
                    : Hidden.Has(HiddenSkill.ThickMug) && ThickMugBonus(player.MaxHp)>0f ? HiddenSkill.ThickMug
                    : Hidden.Has(HiddenSkill.ShakeRhythm) && Combo>0 ? HiddenSkill.ShakeRhythm : HiddenSkill.BarTempo;
                Notify(effect, enemy.transform.position, new Color(1f,.8f,.35f));
            }
            return result;
        }

        void NotifyDrinkCrit()
        {
            if (player == null || player.Stats == null || player.Stats.drinkCritChance <= 0f) return;
            // 2026-11-09: 출처(준비 음료 이름)·효과를 짧게, 연속 치명타는 한 알림의 ×N으로 합친다(DrinkRunHud).
            var hud = player.GetComponentInParent<GameManager>()?.DrinkHud;
            hud?.ShowDrinkNotice("drink_crit", hud.PreparedDrinkName, "치명타 · 피해 ×2", DrinkNoticeKind.Instant, 0f,
                new Color(1f, 0.82f, 0.32f));
        }

        public static float ThickMugBonus(float maxHp) => Mathf.Clamp(Mathf.Floor(Mathf.Max(0f, maxHp - 100f) / 20f) * 0.01f, 0f, 0.20f);

        /// <summary>직접 타격 직후: 콤보 누적, 이중 마무리 처형·덤 베기, 근거리 산탄 밀어내기·파편, 두꺼운 머그 강타.
        /// 여기서 만든 피해는 모두 TakeDamage(직접 타격 아님)라 이 훅·처형·히든을 다시 부르지 않는다.</summary>
        public void AfterDirectHit(Enemy enemy)
        {
            player.Coffee?.OnDirectHit(enemy); // 81 모카포트 증기압(직접 타격만)
            if (Hidden.Has(HiddenSkill.ShakeRhythm))
            {
                Combo = Mathf.Min(ComboMax, Combo + 1);
                comboTimer = ComboWindow;
            }
            if (enemy == null || enemy.Def == null) return;
            Vector2 at = enemy.transform.position;
            float dist = Vector2.Distance(player.Position, at);
            if (Hidden.Has(HiddenSkill.DoubleFinish) && !enemy.IsDead && !enemy.Def.IsBoss &&
                enemy.Hp < enemy.MaxHp * ExecuteThreshold)
            {
                Notify(HiddenSkill.DoubleFinish, at, Color.yellow);
                enemy.TakeDamage(enemy.Hp, HitKind.Heavy);
                EchoSlash(enemy, at);
            }
            // #18 근거리 산탄(총 이외 — 활 등): 밀어내기(일반 적만, 같은 적 ShoveCooldown)와 파편(주변 ShrapnelTargets체, 초당 상한).
            // 총은 #23 발사 묶음 폭발(OnGunVolleyHit)로 대신한다 — 두 효과를 겹치지 않는다.
            if (Hidden.Has(HiddenSkill.PointBlank) && dist <= PointBlankRange && !PointBlankGun)
            {
                if (!enemy.IsDead)
                {
                    Vector2 dir = at - player.Position;
                    dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : new Vector2(player.Facing, 0f);
                    enemy.TryShove(dir * ShoveDistance, ShoveCooldown);
                }
                if (shrapnelUsed < ShrapnelPerSecondCap && player.Stats != null)
                {
                    CollectNear(at, ShrapnelRadius, enemy, true, nearBuffer, ShrapnelTargets);
                    int hits = 0;
                    foreach (var e in nearBuffer)
                    {
                        if (shrapnelUsed >= ShrapnelPerSecondCap) break;
                        shrapnelUsed++; ShrapnelTotal++; hits++;
                        e.TakeDamage(player.Stats.Damage * ShrapnelDamage, HitKind.Effect);
                    }
                    if (hits > 0 && !HitFeedback.LowEffects) Ring(at, 0.3f, ShrapnelRadius * 2f, new Color(1f, 0.7f, 0.35f, 0.5f), 0.2f);
                }
            }
            // #18 두꺼운 머그 강타: 준비됐으면 이 직접 타격 대상에 최대 체력 비례 추가 피해(보스는 절반 비율) + 일반 적 짧은 경직.
            if (Hidden.Has(HiddenSkill.ThickMug) && MugReady && !enemy.IsDead)
            {
                mugTimer = MugInterval;
                MugTotal++;
                float extra = player.MaxHp * (enemy.Def.IsBoss ? MugBossHpFraction : MugHpFraction);
                enemy.TakeDamage(extra, HitKind.Heavy);
                if (!enemy.IsDead) enemy.TryStagger(MugStaggerSeconds, MugStaggerCooldown);
                Notify(HiddenSkill.ThickMug, at, new Color(0.95f, 0.75f, 0.5f));
                Ring(at, 0.5f, 2.2f, new Color(0.9f, 0.7f, 0.45f, 0.6f), 0.3f);
                CameraRig.Shake(0.08f, 0.1f);
            }
        }

        /// <summary>#18 이중 마무리 덤 베기: 처형한 자리에서 EchoRange 안 가장 가까운 적 1체에 기본 피해 EchoDamage배(보스 포함). 처형·다른 덤 베기를 부르지 않는다.</summary>
        void EchoSlash(Enemy executed, Vector2 at)
        {
            if (echoUsed >= EchoPerSecondCap || player.Stats == null) return;
            CollectNear(at, EchoRange, executed, true, nearBuffer, 64);
            Enemy best = null; float bestD = float.MaxValue;
            foreach (var e in nearBuffer)
            {
                float d = ((Vector2)e.transform.position - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null) return;
            echoUsed++; EchoTotal++;
            Vector2 to = best.transform.position;
            float ang = Mathf.Atan2(to.y - at.y, to.x - at.x) * Mathf.Rad2Deg;
            var fx = FadeVfx.Spawn(SpriteFactory.Crescent, to, ang, new Vector2(.5f, .5f), new Vector2(.9f, .9f), new Color(1f, .95f, .6f), .25f, SortOrders.AirVfx);
            if (player.transform.parent != null) fx.transform.SetParent(player.transform.parent, true);
            best.TakeDamage(player.Stats.Damage * EchoDamage, HitKind.Heavy);
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

        /// <summary>단발 피격 직전: 얼음 찻잔 보호막이 있으면 소비하고 피해를 막는다(true). #18: 막는 순간 얼음 파동.</summary>
        public bool TryBlockHit()
        {
            if (!Hidden.Has(HiddenSkill.IcedCup) || !BarrierReady) return false;
            BarrierReady = false;
            barrierTimer = BarrierInterval;
            Notify(HiddenSkill.IcedCup, player.Position, Color.cyan);
            FrostWave();
            return true;
        }

        /// <summary>#18 얼음 찻잔 파동: FrostRadius 안 일반 적을 FrostFreezeSeconds 정지(이동·접촉 피해 없음, 같은 적 FrostFreezeCooldown, 최대 FrostMaxTargets). 피해 없음, 보스 제외.</summary>
        void FrostWave()
        {
            Vector2 at = player.Position;
            CollectNear(at, FrostRadius, null, false, nearBuffer, FrostMaxTargets);
            foreach (var e in nearBuffer)
            {
                if (!e.TryHiddenFreeze(FrostFreezeSeconds, FrostFreezeCooldown)) continue;
                FrostFrozenTotal++;
                if (!HitFeedback.LowEffects)
                    Ring(e.transform.position, e.Def.radius * 1.6f, e.Def.radius * 2.4f, new Color(0.6f, 0.95f, 1f, 0.6f), FrostFreezeSeconds);
            }
            Ring(at, 0.6f, FrostRadius * 2f, new Color(0.55f, 0.9f, 1f, 0.55f), 0.35f);
            CameraRig.Shake(0.1f, 0.12f);
        }

        /// <summary>피해가 실제로 들어간 뒤: 셰이킹 리듬 콤보 초기화.</summary>
        public void OnDamaged() => Combo = 0;

        /// <summary>추가 받는 피해 배율(바 템포 정지 시 -10%). 기존 감소와 합쳐 총 35% 상한.</summary>
        public float ExtraTakenMultiplier(float existing)
        {
            float extra = Hidden.Has(HiddenSkill.BarTempo) && Standing ? 0.9f : 1f;
            return Mathf.Max(1f - MaxTotalDamageReduction, existing * extra) / Mathf.Max(0.0001f, existing);
        }

        /// <summary>접촉 피해 보정(여운 정원) — 여운 피해를 받는 적의 접촉 -15%.</summary>
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
            thornsReady[source] = now + ThornsCooldown * player.Stats.CooldownScale; // 69 음료 재사용 시간 감소
            if (thornsReady.Count > 64) thornsReady.Clear(); // 죽은 적 키 누적 방지(상한만 지키면 됨)
            source.TakeDamage(player.Stats.Damage, HitKind.Effect);
            Notify(HiddenSkill.TrayGuard, source.transform.position, Color.yellow);
        }
    }
}
