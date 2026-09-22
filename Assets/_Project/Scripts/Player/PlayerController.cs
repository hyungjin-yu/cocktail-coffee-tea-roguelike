using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CCT
{
    /// <summary>
    /// 이동(WASD/방향키/패드)과 체력. 공격은 무기 컴포넌트가 자동으로 처리한다.
    /// 벨트스크롤(던파식) 시점: 좌우는 자유, 위아래는 Balance.DepthMin~DepthMax 레인 폭 안에서만 움직인다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class PlayerController : MonoBehaviour
    {
        public const float Radius = 0.4f;

        PlayerStats stats;
        Rigidbody2D rb;
        SpriteRenderer sr;
        Vector2 input;
        float invulnerable;
        float lastMaxHp;
        Color baseColor;

        public event Action Died;

        public float Hp { get; private set; }
        public float MaxHp => stats.MaxHp;
        public bool IsDead { get; private set; }
        public Vector2 Position => transform.position;
        public float PickupRadius => stats.PickupRadius;
        public PlayerStats Stats => stats;

        /// <summary>좌(-1)/우(+1) 바라보는 방향. 스프라이트 반전과 공격 방향에 쓰인다.</summary>
        public float Facing { get; private set; } = 1f;

        public void Init(PlayerStats stats, Color color)
        {
            this.stats = stats;
            baseColor = color;
            rb = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            sr.sprite = SpriteFactory.Capsule;
            sr.color = baseColor;
            sr.sortingOrder = 2; // 몹과 같은 순서 → 카메라의 깊이 정렬(Y)로 앞뒤가 갈린다
            transform.localScale = Vector3.one * Radius * 2f;

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.5f; // 스케일 적용 후 월드 반지름 = Radius

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

            input = ReadMoveInput();
            if (Mathf.Abs(input.x) > 0.01f) Facing = Mathf.Sign(input.x);

            Hp = Mathf.Min(stats.MaxHp, Hp + (Balance.PlayerRegenPerSecond + stats.regenBonus) * Time.deltaTime);
            if (invulnerable > 0f)
            {
                invulnerable -= Time.deltaTime;
                bool blink = Mathf.FloorToInt(Time.time * 20f) % 2 == 0;
                sr.color = blink ? new Color(1f, 0.4f, 0.4f) : baseColor;
                if (invulnerable <= 0f) sr.color = baseColor;
            }

            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Facing;
            transform.localScale = scale;
        }

        void FixedUpdate()
        {
            if (IsDead) return;

            Vector2 next = rb.position + input * stats.MoveSpeed * Time.fixedDeltaTime;
            next.y = Mathf.Clamp(next.y, Balance.DepthMin, Balance.DepthMax);
            rb.MovePosition(next);
        }

        const float MouseDeadzone = 0.15f;

        Vector2 ReadMoveInput() =>
            InputSettings.Scheme == MovementScheme.Mouse ? ReadMouseInput() : ReadKeyboardInput();

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

        /// <summary>월드 X 좌표 쪽을 바라보도록 즉시 방향을 돌린다 (공격 시작 시 대상 쪽으로 스냅).</summary>
        public void SetFacing(float towardWorldX)
        {
            if (!Mathf.Approximately(towardWorldX, Position.x))
                Facing = towardWorldX > Position.x ? 1f : -1f;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || invulnerable > 0f) return;

            Hp = Mathf.Max(0f, Hp - amount);
            invulnerable = Balance.PlayerInvulnerableSeconds;
            if (Hp <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }
    }
}
