namespace CCT
{
    /// <summary>67(2026-10-16) 플레이 결과 기록 — 플레이어 피해 경로의 종류.</summary>
    public enum DamageKind { Unknown, Contact, Projectile, Zone, BossSkill }

    /// <summary>
    /// 67 사망 원인 기록용 피해 출처(값 복사). 발사체·장판은 생성 시점에 받은 값을 그대로 들고 있어
    /// 공격자가 먼저 죽거나 다른 적이 마지막에 닿아도 이전 공격자를 잘못 지목하지 않는다.
    /// 판정·피해량·무적에는 관여하지 않는다(기록 전용).
    /// </summary>
    public readonly struct DamageSource
    {
        /// <summary>출처를 확인할 수 없는 경로의 정직한 대체 표현.</summary>
        public const string UnknownLabel = "전투 피해";

        public readonly DamageKind kind;
        /// <summary>적/보스 이름(없으면 null).</summary>
        public readonly string attacker;
        /// <summary>공격 이름(없으면 null).</summary>
        public readonly string attack;

        public DamageSource(DamageKind kind, string attacker, string attack)
        {
            this.kind = kind;
            this.attacker = string.IsNullOrWhiteSpace(attacker) ? null : attacker;
            this.attack = string.IsNullOrWhiteSpace(attack) ? null : attack;
        }

        public bool IsKnown => attacker != null || attack != null;

        /// <summary>결과·기록에 보일 한국어 문구. 둘 다 없으면 "전투 피해".</summary>
        public string Label => attacker != null && attack != null ? $"{attacker} · {attack}"
            : attacker ?? attack ?? UnknownLabel;

        /// <summary>몸 접촉. 닿으면 터지는 자폭형은 그 접촉 피해가 곧 폭발이라 "자폭"으로 적는다.</summary>
        public static DamageSource Contact(EnemyDef def) =>
            new DamageSource(DamageKind.Contact, def?.name, def != null && def.explodesOnContact ? "자폭" : "몸통 접촉");
        public static DamageSource Projectile(EnemyDef def, string attack = "원거리 공격") =>
            new DamageSource(DamageKind.Projectile, def?.name, attack);
        public static DamageSource Zone(EnemyDef def, string attack) => new DamageSource(DamageKind.Zone, def?.name, attack);
        public static DamageSource BossSkill(EnemyDef def, string attack) => new DamageSource(DamageKind.BossSkill, def?.name, attack);
    }
}
