using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CCT
{
    /// <summary>
    /// 이동(WASD/방향키/패드)과 체력. 공격은 무기 컴포넌트가 자동으로 처리한다.
    /// 벨트스크롤(던파식) 시점: 좌우는 BattleArena 구역(양 끝 벽) 안, 위아래는 Balance.DepthMin~DepthMax 레인 폭 안에서만 움직인다.
    /// </summary>
    // 04 §9: 시각(반전·확대·애니메이션)은 Visual 자식(PlayerVisual)만 담당하고 이 루트는 스케일 1·판정만 가진다.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public const float Radius = 0.4f;

        PlayerStats stats;
        Rigidbody2D rb;
        PlayerVisual visual;
        Vector2 input;
        float invulnerable;
        float lastMaxHp;
        Vector2 knockbackVelocity;
        float knockbackRemaining;

        public event Action Died;

        /// <summary>자동 캡처 투어(-cct-autotour) 전용 이동 입력. null이면 실제 키보드/마우스.</summary>
        public Vector2? AutoInputOverride { get; set; }

        public float Hp { get; private set; }
        public float MaxHp => stats.MaxHp;
        public bool IsDead { get; private set; }
        public Vector2 Position => transform.position;
        public float PickupRadius => stats.PickupRadius;
        public PlayerStats Stats => stats;
        public RunChallenge Challenge { get; private set; }
        public AttackFinalForms FinalForms { get; private set; } = new AttackFinalForms();
        /// <summary>41 이번 런 히든 스킬(런마다 새 플레이어 = 재시작 초기화).</summary>
        public RunHiddenSkills Hidden { get; private set; } = new RunHiddenSkills();
        public HiddenSkillRuntime HiddenRuntime { get; private set; }
        /// <summary>72 §4 장착 숙련 효과(런 시작 때 GameManager가 붙인다, 없으면 null).</summary>
        public MasteryRuntime Mastery { get; set; }
        /// <summary>이번 프레임 이동 입력이 있는지(히든 정지/이동 조건).</summary>
        public bool IsMovingInput => input.sqrMagnitude > 0.0001f;

        /// <summary>좌(-1)/우(+1) 바라보는 방향. 스프라이트 반전과 공격 방향에 쓰인다.</summary>
        public float Facing { get; private set; } = 1f;

        /// <summary>시각 담당 자식(몸체 아트/캡슐, 무기, 그림자).</summary>
        public PlayerVisual Visual => visual;

        /// <param name="bodyArtId">직업 몸체 아트 ID(bartender/tea_sommelier). 목록에 없으면 캡슐 플레이스홀더.</param>
        public void Init(PlayerStats stats, Color color, string bodyArtId = null, RunChallenge challenge = null)
        {
            this.stats = stats;
            Challenge = challenge;
            if (HiddenRuntime == null) HiddenRuntime = HiddenSkillRuntime.Attach(this);
            rb = GetComponent<Rigidbody2D>();

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            transform.localScale = Vector3.one; // 물리 루트는 스케일 1 — 판정 반지름을 직접 지정
            visual = PlayerVisual.Create(transform, Radius, color, bodyArtId);

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = Radius;

            Hp = stats.MaxHp;
            lastMaxHp = stats.MaxHp;
            stats.Changed += OnStatsChanged;
        }

        void OnDestroy()
        {
            if (stats != null) stats.Changed -= OnStatsChanged;
        }

        void OnStatsChanged()
        {
            // 최대 체력이 늘어난 만큼 현재 체력도 함께 회복
            float delta = stats.MaxHp - lastMaxHp;
            lastMaxHp = stats.MaxHp;
            if (delta > 0f) Hp = Mathf.Min(Hp + delta, stats.MaxHp);
        }

        void Update()
        {
            if (IsDead) return;

            input = AutoInputOverride ?? ReadMoveInput();
            if (Mathf.Abs(input.x) > 0.01f) Facing = Mathf.Sign(input.x);

            Hp = Mathf.Min(stats.MaxHp, Hp + stats.RegenPerSecond * Time.deltaTime);
            bool blink = false;
            if (invulnerable > 0f)
            {
                invulnerable -= Time.deltaTime;
                blink = invulnerable > 0f && Mathf.FloorToInt(Time.time * 20f) % 2 == 0;
            }

            if (visual != null) visual.Tick(Facing, input.sqrMagnitude > 0.0001f, blink);
        }

        /// <summary>이번 프레임 모든 장판의 Update가 끝난 뒤, 가장 높은 DPS 후보 하나만 실제로 적용한다
        /// (06:23 — 중첩 피해 장판은 가장 높은 DPS 하나만).</summary>
        void LateUpdate()
        {
            if (IsDead) return;
            float dps = PlayerZoneDamage.ConsumeFrame();
            if (dps > 0f) TakeZoneDamage(dps * Balance.ZoneTickInterval, PlayerZoneDamage.LastConsumedSource);
        }

        void FixedUpdate()
        {
            if (IsDead) return;

            Vector2 velocity = input * stats.MoveSpeed;
            if (knockbackRemaining > 0f)
            {
                knockbackRemaining -= Time.fixedDeltaTime;
                velocity += knockbackVelocity;
            }

            Vector2 next = rb.position + velocity * Time.fixedDeltaTime;
            next.y = Mathf.Clamp(next.y, Balance.DepthMin, Balance.DepthMax);
            next.x = BattleArena.ClampX(next.x, Radius); // 57 P0-1 유한 전투 구역(양 끝 벽)
            rb.MovePosition(next);
        }

        const float MouseDeadzone = 0.15f;

        Vector2 ReadMoveInput()
        {
            // 2026-10-12 Android: 터치 UI는 가상 조이스틱이 우선, 놓았을 때는 연결된 키보드/게임패드(마우스 추적 방식은 쓰지 않음).
            if (MobilePlatform.IsTouchUI)
                return TouchControlsView.Held ? TouchControlsView.Move : ReadKeyboardInput();
            return InputSettings.Scheme == MovementScheme.Mouse ? ReadMouseInput() : ReadKeyboardInput();
        }

        static Vector2 ReadKeyboardInput()
        {
            var v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            }
            var pad = Gamepad.current;
            if (pad != null) v += pad.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>마우스 커서가 있는 쪽으로 계속 이동 (가상 조이스틱 방식). 커서가 아주 가까우면 정지.</summary>
        Vector2 ReadMouseInput()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return Vector2.zero;

            float camDistance = Mathf.Abs(cam.transform.position.z); // 게임 평면(z=0)까지 거리
            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, camDistance));

            Vector2 toMouse = (Vector2)world - Position;
            return toMouse.magnitude < MouseDeadzone ? Vector2.zero : toMouse.normalized;
        }

        /// <summary>최대 체력의 fraction 만큼 회복.</summary>
        public void Heal(float fraction)
        {
            if (IsDead) return;
            Hp = Mathf.Min(stats.MaxHp, Hp + stats.MaxHp * fraction);
        }

        /// <summary>고정량 회복(히든 처치/줍기 회복).</summary>
        public void HealFlat(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Hp = Mathf.Min(stats.MaxHp, Hp + amount);
        }

        /// <summary>단발 무적 시간을 최소 seconds로 늘린다(히든 삼색 원두 보스 등장).</summary>
        public void GrantInvulnerability(float seconds)
        {
            if (!IsDead) invulnerable = Mathf.Max(invulnerable, seconds);
        }

        /// <summary>적 몸 접촉 피해. 히든 여운 정원(중독 적 -15%)·쟁반 방패(반격)를 거친 뒤 일반 단발 피해와 같다.</summary>
        public void TakeContactDamage(Enemy source, float amount)
        {
            if (IsDead || invulnerable > 0f) return;
            if (HiddenRuntime != null) amount = HiddenRuntime.ModifyContactDamage(source, amount);
            float before = Hp;
            TakeDamage(amount, DamageSource.Contact(source != null ? source.Def : null));
            if (Hp < before) HiddenRuntime?.OnContactHit(source);
        }

        /// <summary>월드 X 좌표 쪽을 바라보도록 즉시 방향을 돌린다 (공격 시작 시 대상 쪽으로 스냅).</summary>
        public void SetFacing(float towardWorldX)
        {
            if (!Mathf.Approximately(towardWorldX, Position.x))
                Facing = towardWorldX > Position.x ? 1f : -1f;
        }

        /// <summary>일정 시간 동안 velocity 만큼 이동 입력에 더해서 밀려난다 (보스 패턴의 넉백 등).</summary>
        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            knockbackVelocity = velocity;
            knockbackRemaining = duration;
        }

        /// <summary>접촉·발사체·보스 단발 피해. 0.8초 무적을 걸고, 그 무적에 막힌다 (06:22).</summary>
        public void TakeDamage(float amount) => TakeDamage(amount, default);

        /// <summary>67: 출처를 함께 받는 단발 피해. 판정·피해량·무적은 출처 없는 호출과 같다.</summary>
        public void TakeDamage(float amount, DamageSource source)
        {
            if (IsDead || invulnerable > 0f) return;
            if (HiddenRuntime != null && HiddenRuntime.TryBlockHit())
            {
                invulnerable = Balance.PlayerInvulnerableSeconds; // 보호막도 일반 피격처럼 0.8초 무적
                return;
            }
            ApplyDamage(amount, source);
            if (!IsDead) invulnerable = Balance.PlayerInvulnerableSeconds;
        }

        /// <summary>장판(DOT) 틱 피해. 단발 무적을 걸지도, 그 무적에 막히지도 않는다 (06:22 — "단발 무적을
        /// 만들거나 그 무적에 막히지 않는다"). 피해 감소율은 단발과 동일하게 적용된다.</summary>
        public void TakeZoneDamage(float amount) => TakeZoneDamage(amount, default);

        public void TakeZoneDamage(float amount, DamageSource source)
        {
            if (IsDead) return;
            ApplyDamage(amount, source);
        }

        /// <summary>67: 마지막으로 HP를 실제로 줄인 피해의 출처(무적·차단·0·음수·비유한 피해는 덮어쓰지 않는다).</summary>
        public DamageSource? LastDamageSource { get; private set; }
        /// <summary>67: HP를 0으로 만든 바로 그 피해의 출처. 사망 전에는 null.</summary>
        public DamageSource? LethalDamageSource { get; private set; }

        void ApplyDamage(float amount, DamageSource source)
        {
            float hpBefore = Hp;
            amount *= Challenge?.DamageMultiplier ?? 1f;
            // 09:84 "플레이어가 무적이라 피해0이면 hit_player 재생하지 않는다" — 무적 중엔 애초에
            // TakeDamage에서 걸러져 여기까지 안 온다. amount>0 방어는 남겨둔다.
            if (amount > 0f) AudioService.PlaySfx("hit_player", transform.position);
            float taken = stats.DamageTakenMultiplier;
            if (HiddenRuntime != null) taken *= HiddenRuntime.ExtraTakenMultiplier(taken);
            Hp = Mathf.Max(0f, Hp - amount * taken);
            // 67 원인 기록: 유한한 양수 피해가 실제로 HP를 줄였을 때만(무적·차단·0·음수·NaN·무한대는 이전 기록을 덮지 않는다).
            float effective = amount * taken;
            bool attributable = Hp < hpBefore && amount > 0f && !float.IsInfinity(amount) && effective > 0f && !float.IsInfinity(effective);
            if (attributable) LastDamageSource = source;
            if (amount > 0f) HiddenRuntime?.OnDamaged();
            if (amount > 0f) Mastery?.OnDamaged();
            if (amount > 0f && Hp > 0f) visual?.PlayHurt(); // 04 §6 hurt 0.08초(있을 때만)
            if (amount > 0f) CameraRig.Shake(0.12f, 0.15f); // 08:122 피격 흔들림(설정 0이면 없음)
            if (Hp <= 0f)
            {
                // 기록할 수 없는 피해로 쓰러지면 이전 공격자 대신 확인 불가(기본값 = "전투 피해")로 남긴다.
                LethalDamageSource = attributable ? source : default(DamageSource);
                IsDead = true;
                visual?.PlayDefeat();
                Died?.Invoke();
            }
        }
    }
}
