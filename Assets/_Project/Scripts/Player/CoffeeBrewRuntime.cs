using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 81 커피 추출법별 전투 리듬(런 입장 때 마신 커피가 있을 때만, GameManager가 플레이어에 한 번 붙인다). 기존 커피 사거리 버프는 그대로 두고
    /// 추출법마다 다른 운용 결정을 더한다 — 에스프레소: 멈춰 서서 압력을 모아 짧은 샷 집중, 핸드 드립: 뜸/붓기 고정 주기에 맞춘 이동·거리 운용,
    /// 모카포트: 직접 타격으로 증기압을 쌓아 가득 차면 주변 증기 폭발. 수치는 모두 임시 게임 밸런스이며 실제 카페인 효능이 아니다.
    /// 타이머는 게임 시간(일시정지·카드 창의 timeScale 0에서 멈춤). 난수를 쓰지 않아 같은 입력이면 같은 결과다.
    /// 증기 폭발은 직접 타격 훅을 부르지 않아 히든·숙련 효과나 증기압을 재귀 생성하지 않는다.
    /// </summary>
    public sealed class CoffeeBrewRuntime : MonoBehaviour
    {
        // 에스프레소 머신: 집중(샷)
        public const float EspressoChargeSeconds = 1.0f, EspressoShotSeconds = 3.0f, EspressoRechargeSeconds = 4.0f, EspressoShotAttackSpeed = 0.30f;
        // 핸드 드립: 리듬(뜸 → 붓기 반복)
        public const float PourBloomSeconds = 2.0f, PourPourSeconds = 4.0f, PourBloomMoveSpeed = 0.15f, PourPourRange = 0.20f;
        // 모카포트: 숙련(증기압 게이지)
        public const int MokaHeatMax = 10;
        public const float MokaHeatDecaySeconds = 1.5f, MokaBurstRadius = 2.6f, MokaBurstDamageScale = 1.2f, MokaStaggerSeconds = 0.35f, MokaStaggerCooldown = 1.0f;

        PlayerController player;
        float charge, shotTimer, rechargeTimer, cycleTime, heatIdle;

        public CoffeeBrewMethod Method { get; private set; }
        /// <summary>에스프레소 압력 0~1(멈춰 선 시간 / 충전 시간).</summary>
        public float Charge => charge;
        public bool ShotActive => shotTimer > 0f;
        public float ShotRemaining => shotTimer;
        public float RechargeRemaining => rechargeTimer;
        public int ShotsPulled { get; private set; }
        public bool Blooming => cycleTime < PourBloomSeconds;
        /// <summary>현재 뜸/붓기 단계의 남은 시간.</summary>
        public float PhaseRemaining => Blooming ? PourBloomSeconds - cycleTime : PourBloomSeconds + PourPourSeconds - cycleTime;
        public int Heat { get; private set; }
        public int Bursts { get; private set; }
        public int LastBurstHits { get; private set; }

        public static CoffeeBrewRuntime Attach(PlayerController player, CoffeeBrewMethod method)
        {
            if (player == null || !CoffeeBrewing.IsDefined(method)) return null;
            // 같은 플레이어에 두 번 붙이지 않는다(재호출되어도 효과 하나).
            var rt = player.GetComponent<CoffeeBrewRuntime>();
            if (rt == null) rt = player.gameObject.AddComponent<CoffeeBrewRuntime>();
            rt.player = player;
            if (rt.Method != method) rt.ResetState();
            rt.Method = method;
            rt.ApplyStats();
            return rt;
        }

        /// <summary>2026-11-11 런 중 음료 교체: 커피 추출 리듬을 떼고 그 효과 슬롯을 0으로.</summary>
        public static void Detach(PlayerController player)
        {
            if (player == null) return;
            var rt = player.GetComponent<CoffeeBrewRuntime>();
            var s = player.Stats;
            if (s != null) { s.coffeeAttackSpeed = 0f; s.coffeeMoveSpeed = 0f; s.coffeeRangePercent = 0f; }
            if (rt != null) { rt.enabled = false; Destroy(rt); }
            player.Coffee = null;
        }

        void ResetState()
        {
            charge = shotTimer = rechargeTimer = cycleTime = heatIdle = 0f;
            Heat = 0;
        }

        /// <summary>시험 전용: 프레임 진행으로 흐른 상태를 지우고 Step으로만 진행하게 한다.</summary>
        public void ResetStateForTests()
        {
            enabled = false;
            ResetState();
            ApplyStats();
        }

        void Update()
        {
            if (player == null || player.IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Step(dt, player.IsMovingInput);
        }

        /// <summary>게임 시간 dt만큼 진행. moving = 이번 프레임 이동 입력(에스프레소 압력 판정).</summary>
        public void Step(float dt, bool moving)
        {
            if (dt <= 0f) return;
            switch (Method)
            {
                case CoffeeBrewMethod.EspressoMachine:
                    if (shotTimer > 0f)
                    {
                        shotTimer -= dt;
                        if (shotTimer <= 0f) { shotTimer = 0f; rechargeTimer = EspressoRechargeSeconds; }
                    }
                    else if (rechargeTimer > 0f) rechargeTimer = Mathf.Max(0f, rechargeTimer - dt);
                    else
                    {
                        charge = moving ? 0f : charge + dt / EspressoChargeSeconds;
                        if (charge >= 1f)
                        {
                            charge = 0f;
                            shotTimer = EspressoShotSeconds;
                            ShotsPulled++;
                            Pulse(new Color(1f, 0.78f, 0.45f, 0.8f), 0.5f, 1.6f);
                            // 지속 상태: 출처 · 효과 · 남은 시간(게임 시간으로 줄어듦). 샷을 뽑는 순간 잔이 떠오르고 증기음.
                            Notice("espresso_shot", $"공격 속도 +{IntNum.Percent(EspressoShotAttackSpeed)}%", DrinkNoticeKind.Timed,
                                EspressoShotSeconds, new Color(1f, 0.82f, 0.48f), 2f);
                            CupFx(new Color(1f, 0.78f, 0.45f), "steam");
                        }
                    }
                    break;
                case CoffeeBrewMethod.PourOver:
                    bool wasBlooming = Blooming;
                    cycleTime = (cycleTime + dt) % (PourBloomSeconds + PourPourSeconds);
                    if (wasBlooming != Blooming)
                    {
                        Pulse(Blooming ? new Color(0.75f, 0.9f, 1f, 0.6f) : new Color(1f, 0.85f, 0.6f, 0.6f), 0.4f, 1.2f);
                        // 뜸/붓기는 2·4초마다 바뀐다 — 알림은 단계별로 PourNoticeCooldown에 한 번(바뀌는 순간의 고리·HUD 칩 줄은 매번).
                        if (Blooming)
                            Notice("pour_bloom", $"뜸 · 이동 속도 +{IntNum.Percent(PourBloomMoveSpeed)}%", DrinkNoticeKind.Timed,
                                PourBloomSeconds, new Color(0.72f, 0.88f, 1f), 1.4f, PourNoticeCooldown);
                        else
                            Notice("pour_pour", $"붓기 · 사거리 +{IntNum.Percent(PourPourRange)}%", DrinkNoticeKind.Timed,
                                PourPourSeconds, new Color(1f, 0.82f, 0.48f), 1.4f, PourNoticeCooldown);
                    }
                    break;
                case CoffeeBrewMethod.MokaPot:
                    if (Heat > 0)
                    {
                        heatIdle += dt;
                        while (heatIdle >= MokaHeatDecaySeconds && Heat > 0) { Heat--; heatIdle -= MokaHeatDecaySeconds; }
                    }
                    else heatIdle = 0f;
                    break;
            }
            ApplyStats();
        }

        /// <summary>추출법별 런 중 보정. 기존 % 보너스처럼 환불 봉인(BonusesSuppressed)을 받는다.</summary>
        void ApplyStats()
        {
            var s = player != null ? player.Stats : null;
            if (s == null) return;
            s.coffeeAttackSpeed = Method == CoffeeBrewMethod.EspressoMachine && shotTimer > 0f ? EspressoShotAttackSpeed : 0f;
            s.coffeeMoveSpeed = Method == CoffeeBrewMethod.PourOver && Blooming ? PourBloomMoveSpeed : 0f;
            s.coffeeRangePercent = Method == CoffeeBrewMethod.PourOver && !Blooming ? PourPourRange : 0f;
        }

        /// <summary>직접 타격 직후(HiddenSkillRuntime.AfterDirectHit). 모카포트만 증기압을 쌓는다.</summary>
        public void OnDirectHit(Enemy enemy)
        {
            if (Method != CoffeeBrewMethod.MokaPot || player == null || player.IsDead) return;
            Heat++;
            heatIdle = 0f;
            if (Heat >= MokaHeatMax) Burst();
        }

        /// <summary>증기 폭발: 플레이어 주변 반경 안의 적에게 기본 피해 × 배율, 일반 적은 짧게 경직. 증기압 0으로.</summary>
        void Burst()
        {
            Heat = 0;
            Bursts++;
            Vector2 center = player.Position;
            float damage = (player.Stats != null ? player.Stats.Damage : 0f) * MokaBurstDamageScale;
            float r2 = MokaBurstRadius * MokaBurstRadius;
            var targets = Enemy.Active.ToArray(); // 처치로 목록이 바뀌어도 안전
            int hits = 0;
            foreach (var e in targets)
            {
                if (e == null || e.IsDead || e.Invulnerable) continue;
                if (((Vector2)e.transform.position - center).sqrMagnitude > r2) continue;
                e.TryStagger(MokaStaggerSeconds, MokaStaggerCooldown);
                e.TakeDirectHit(damage, HitKind.Heavy); // 2026-11-10 광역 직접 공격: 대상마다 상성 판정
                hits++;
            }
            LastBurstHits = hits;
            Pulse(new Color(1f, 1f, 1f, 0.55f), 0.6f, MokaBurstRadius * 2f);
            // 순간 발동: 출처 · 효과(정수 배율) · 적중 수. 폭발 한 번에 한 번만 짧게 흔든다(설정 흔들림 % 적용, 0이면 없음).
            Notice("moka_burst", $"증기 폭발 · 기본 피해 {IntNum.Multiplier(MokaBurstDamageScale)} · {hits}명 적중", DrinkNoticeKind.Instant,
                0f, new Color(0.9f, 0.94f, 1f), 1.8f);
            CupFx(new Color(0.92f, 0.95f, 1f), "steam");
            CameraRig.Shake(0.08f, 0.12f);
        }

        /// <summary>핸드 드립 단계 알림의 단계별 최소 간격(초, 게임 시간).</summary>
        public const float PourNoticeCooldown = 20f;

        /// <summary>출처(추출법 이름)를 붙인 음료 알림. HUD가 같은 key를 합치고 차례로 보여 준다.</summary>
        void Notice(string key, string effect, DrinkNoticeKind kind, float stateSeconds, Color accent, float seconds, float cooldown = 0f)
        {
            player?.GetComponentInParent<GameManager>()?.DrinkHud?.ShowDrinkNotice(key, CoffeeBrewing.ButtonName(Method), effect, kind,
                stateSeconds, accent, seconds, cooldown);
        }

        /// <summary>발동 순간 머리 위로 마신 커피 잔이 짧게 떠오른다(입력·판정 무관, 기존 잔 아트·효과음 재사용).</summary>
        void CupFx(Color accent, string sfx)
        {
            var game = player != null ? player.GetComponentInParent<GameManager>() : null;
            var recipe = game != null ? DrinkLab.DrinkInRun(game.RunId)?.recipe : null;
            if (recipe != null) DrinkUseFx.Play(player, recipe, accent, sfx, 0.8f);
        }

        void Pulse(Color color, float from, float to)
        {
            if (player == null) return;
            var fx = FadeVfx.Spawn(SpriteFactory.Circle, player.Position, 0f, Vector2.one * from, Vector2.one * to, color, 0.35f, SortOrders.AirVfx);
            fx.transform.SetParent(player.transform.parent, true); // 런 종료/재시작 시 잔상도 함께 정리
        }

        /// <summary>런 HUD 한 줄: 추출법 이름 · 현재 단계와 남은 시간 · 다음 결정.</summary>
        public string StatusLine()
        {
            string name = CoffeeBrewing.ButtonName(Method);
            switch (Method)
            {
                case CoffeeBrewMethod.EspressoMachine:
                    // 남은 시간은 올림(정수 초) — 0초 표시 = 실제 종료.
                    if (shotTimer > 0f) return $"{name} · 샷 {IntNum.RemainingSeconds(shotTimer)}초 · 공격 속도 +{IntNum.Percent(EspressoShotAttackSpeed)}%";
                    if (rechargeTimer > 0f) return $"{name} · 다음 샷 준비 {IntNum.RemainingSeconds(rechargeTimer)}초";
                    return $"{name} · 멈춰 서면 압력 {Mathf.FloorToInt(charge * 10f) * 10}% → 샷";
                case CoffeeBrewMethod.PourOver:
                    return Blooming
                        ? $"{name} · 뜸 {IntNum.RemainingSeconds(PhaseRemaining)}초 · 이동 속도 +{IntNum.Percent(PourBloomMoveSpeed)}%"
                        : $"{name} · 붓기 {IntNum.RemainingSeconds(PhaseRemaining)}초 · 사거리 +{IntNum.Percent(PourPourRange)}%";
                case CoffeeBrewMethod.MokaPot:
                    return $"{name} · 증기압 {Heat}/{MokaHeatMax} · 차면 증기 폭발";
                default:
                    return "";
            }
        }
    }
}
