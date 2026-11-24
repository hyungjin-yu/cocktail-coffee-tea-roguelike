using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 정식 레시피 시그니처 — 런 중 1회 사용(플레이어에 붙어 런과 함께 사라진다).
    /// 충전: 출발 잔이 정식 레시피면 런 시작 때, 마스터 런 잔이면 마실 때(아직 이번 판에 쓰지 않았을 때만 — 쓰기 전 교체는 덮어쓰기, 쌓이지 않음).
    /// 사용: Q 키 또는 HUD 시그니처 칸 클릭. **먼저** DrinkLab.CommitSignatureUse로 디스크에 사용을 확정(출발 잔은 같은 사건에서 locked)한 뒤에만 효과를 낸다 —
    /// 저장 실패면 효과 없음(다시 눌러 재시도). 같은 판 두 번째 사용은 디스크 기록으로 거부되어 재시작·복구·런 잔 교체로 다시 쓸 수 없다.
    /// 피해는 TakeDamage(보조 경로) — 직접 적중 훅·히든·상성·치명타를 다시 부르지 않는다. 보스 한 번 피해는 최대 체력의 12% 이하.
    /// </summary>
    public sealed class SignatureRuntime : MonoBehaviour
    {
        public const float BossDamageCapFraction = 0.12f;
        public const float SaltRimRadius = 4f, SaltRimDamage = 2.5f, SaltRimHold = 2f;
        public const float SidecarSeconds = 5f, SidecarMoveSpeed = 0.4f;
        public const float WhiteOutDamage = 1.5f, WhiteOutHold = 4f;
        public const float ScreenMargin = 0.02f;

        GameManager game;
        PlayerController player;
        float sidecarRemaining;
        SignatureVfx.SidecarTrail sidecarFx;
        /// <summary>마지막 화이트 아웃에서 경직(=얼음 배지)이 실제로 걸린 적 수(검수용).</summary>
        public int LastHeld { get; private set; }

        /// <summary>지금 충전된(아직 쓰지 않은) 시그니처. 없으면 null.</summary>
        public SignatureDef Charged { get; private set; }
        /// <summary>충전 출처가 출발 잔인가(사용 시 출발 잔 귀속 처리).</summary>
        public bool ChargedFromPrepared { get; private set; }
        /// <summary>이번 판에 사용한 시그니처(없으면 null).</summary>
        public SignatureDef UsedDef { get; private set; }
        public bool Used => UsedDef != null;
        public float ActiveRemaining => Mathf.Max(0f, sidecarRemaining);
        /// <summary>마지막 사용 시도 결과 문구(HUD 알림·검수용).</summary>
        public string LastMessage { get; private set; } = "";
        public int UseSerial { get; private set; }
        /// <summary>마지막 사용에서 효과를 받은 적 수(검수용).</summary>
        public int LastAffected { get; private set; }

        public static SignatureRuntime Attach(PlayerController player, GameManager game)
        {
            var rt = player.gameObject.AddComponent<SignatureRuntime>();
            rt.player = player;
            rt.game = game;
            return rt;
        }

        /// <summary>충전(교체). 이번 판에 이미 썼으면(메모리·디스크 기록) 무시한다 — 런 잔 교체로 두 번째 사용을 만들 수 없다.</summary>
        public bool Charge(SignatureDef def, bool fromPrepared)
        {
            if (def == null || Used || (game != null && DrinkLab.SignatureUsedIn(game.RunId))) return false;
            Charged = def;
            ChargedFromPrepared = fromPrepared;
            return true;
        }

        /// <summary>지금 사용 입력을 받을 수 있는 상태(전투 진행 중·일시정지/보상창 아님).</summary>
        public bool CanUseNow => Charged != null && !Used && game != null && player != null && !player.IsDead
            && game.State == GameState.Playing && !game.IsPaused && !game.MasterFreezeActive && !game.MasterBrewOpen;

        /// <summary>Q / HUD 클릭. 성공하면 true(효과 발동). 실패 사유는 LastMessage.</summary>
        public bool TryUse()
        {
            if (Charged == null) { LastMessage = Used ? $"시그니처 · {KoreanParticle.EunNeun(UsedDef.effectName)} 이번 판에 이미 사용했습니다." : "이번 판에 쓸 시그니처가 없습니다."; return false; }
            if (!CanUseNow) { LastMessage = "지금은 시그니처를 쓸 수 없습니다(전투 중에만)."; return false; }
            var def = Charged;
            if (!DrinkLab.CommitSignatureUse(game.RunId, def.id, ChargedFromPrepared, out var error))
            {
                LastMessage = error;
                return false; // 효과 없음 — 충전은 그대로(저장 실패면 다시 눌러 재시도)
            }
            Charged = null;
            UsedDef = def;
            UseSerial++;
            LastHeld = 0;
            LastAffected = Execute(def);
            LastMessage = $"시그니처 · {def.effectName} 사용함";
            return true;
        }

        int Execute(SignatureDef def)
        {
            var at = player.Position;
            switch (def.effect)
            {
                case SignatureEffect.SaltRim:
                {
                    int n = 0;
                    float dmg = game.Stats.Damage * SaltRimDamage;
                    float r2 = SaltRimRadius * SaltRimRadius;
                    foreach (var e in Enemy.Active.ToArray())
                    {
                        if (e == null || e.IsDead || ((Vector2)e.transform.position - at).sqrMagnitude > r2) continue;
                        Hit(e, dmg); e.ApplySignatureHold(SaltRimHold); n++;
                    }
                    SignatureVfx.SaltRim(at, def.accent); // 발동 순간 좌표 고정, 지름 8월드 = 반경 4 판정
                    return n;
                }
                case SignatureEffect.Sidecar:
                    player.GrantInvulnerability(SidecarSeconds);
                    sidecarRemaining = SidecarSeconds;
                    game.Stats.signatureMoveSpeed = SidecarMoveSpeed;
                    sidecarFx?.Stop();
                    sidecarFx = SignatureVfx.Sidecar(player, SidecarSeconds); // 5초 바람선·잔상(저이펙트: 발밑 정적 표식)
                    return 0;
                case SignatureEffect.WhiteOut:
                {
                    int n = 0;
                    float dmg = game.Stats.Damage * WhiteOutDamage;
                    var cam = Camera.main;
                    foreach (var e in Enemy.Active.ToArray())
                    {
                        if (e == null || e.IsDead || !OnScreen(cam, e.transform.position)) continue;
                        Hit(e, dmg);
                        // 얼음 배지는 경직이 실제로 걸린(살아 있는 일반·정예) 대상에게만 — 보스·사망·플레이어는 ApplySignatureHold가 false.
                        if (e.ApplySignatureHold(WhiteOutHold)) { SignatureVfx.Badge(e, WhiteOutHold); LastHeld++; }
                        n++;
                    }
                    SignatureVfx.WhiteOutEdge(); // 가장자리 서리만(가운데 시야 보존, 전체 화면 틴트·큰 원 없음)
                    return n;
                }
            }
            return 0;
        }

        /// <summary>보조 경로 피해(직접 적중 훅 없음). 보스는 한 번에 최대 체력 12% 이하.</summary>
        public static float CappedDamage(Enemy e, float amount)
        {
            if (e == null || amount <= 0f) return 0f;
            return e.Def != null && e.Def.IsBoss ? Mathf.Min(amount, e.MaxHp * BossDamageCapFraction) : amount;
        }

        static void Hit(Enemy e, float amount)
        {
            float d = CappedDamage(e, amount);
            if (d > 0f) e.TakeDamage(d, HitKind.Heavy, null);
        }

        static bool OnScreen(Camera cam, Vector3 world)
        {
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(world);
            return v.x >= -ScreenMargin && v.x <= 1f + ScreenMargin && v.y >= -ScreenMargin && v.y <= 1f + ScreenMargin;
        }

        void Update()
        {
            if (sidecarRemaining <= 0f) return;
            sidecarRemaining -= Time.deltaTime;
            if (sidecarRemaining <= 0f)
            {
                sidecarRemaining = 0f;
                if (game != null && game.Stats != null) game.Stats.signatureMoveSpeed = 0f;
                sidecarFx?.Stop(); sidecarFx = null; // 버프 종료 = 바람선·잔상 모두 제거
            }
        }

        void OnDestroy()
        {
            if (game != null && game.Stats != null) game.Stats.signatureMoveSpeed = 0f;
            if (sidecarFx != null) Destroy(sidecarFx.gameObject);
        }
    }
}
