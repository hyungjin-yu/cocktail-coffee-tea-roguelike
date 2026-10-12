using UnityEngine;

namespace CCT
{
    /// <summary>기획서 5장: 시작 시 직업 1개 + 무기 1개 선택. 재료는 직업과 무관하게 항상 둘 다 드랍(임시 결정).</summary>
    public enum Job { Bartender, TeaSommelier }

    /// <summary>
    /// 무기 종류. 레이피어/검은 근접, 총/활은 원거리. 활은 기본 관통 1로 시작한다.
    /// 실제 동작은 PlayerProjectileWeapon(총/활) 또는 RapierWeapon(레이피어/검)이 담당한다.
    /// </summary>
    public enum WeaponKind { Rapier, Gun, Sword, Bow }

    public static class WeaponKindExtensions
    {
        /// <summary>총/활 = 원거리, 레이피어/검 = 근접. 카드 필터링에 쓴다.</summary>
        public static bool IsRanged(this WeaponKind kind) => kind == WeaponKind.Gun || kind == WeaponKind.Bow;
    }

    public class WeaponPreset
    {
        public WeaponKind kind;
        public string name;
        public string description;
        /// <summary>Resources 아이콘 경로. CharacterSelectView 무기 선택 카드가 쓴다.</summary>
        public string iconResourcePath;
        /// <summary>04 §8 장착 시각 길이 / 플레이어 높이(레이피어0.8, 검0.65, 총0.4, 활0.7). 사거리·판정과 무관.</summary>
        public float heldLengthRatio = 0.6f;

        public static WeaponPreset For(WeaponKind kind) =>
            kind == WeaponKind.Rapier ? Rapier : kind == WeaponKind.Gun ? Gun : kind == WeaponKind.Sword ? Sword : Bow;

        public static readonly WeaponPreset Rapier = new WeaponPreset
        {
            kind = WeaponKind.Rapier, name = "레이피어",
            description = "전방 레인을 연속 공격.\n칵테일3+차3 획득 시 티니 보우 해금.", // 08:63
            iconResourcePath = "Icons/Weapons/rapier", heldLengthRatio = 0.8f,
        };
        public static readonly WeaponPreset Gun = new WeaponPreset
        {
            kind = WeaponKind.Gun, name = "총", description = "자동 조준 사격.\n다중 발사·관통 카드로 강화.",
            iconResourcePath = "Icons/Weapons/gun", heldLengthRatio = 0.4f,
        };
        public static readonly WeaponPreset Sword = new WeaponPreset
        {
            kind = WeaponKind.Sword, name = "검", description = "전방 레인을 연속 공격.\n근접 강화 카드를 사용.",
            iconResourcePath = "Icons/Weapons/sword", heldLengthRatio = 0.65f,
        };
        public static readonly WeaponPreset Bow = new WeaponPreset
        {
            kind = WeaponKind.Bow, name = "활", description = "자동 조준 사격. 기본 관통 1.\n다중 발사·관통 카드로 강화.",
            iconResourcePath = "Icons/Weapons/bow", heldLengthRatio = 0.7f,
        };
    }

    /// <summary>직업별 프리셋. 직업당 무기 선택지 2개(기획서 5장 표).</summary>
    public class JobPreset
    {
        public Job job;
        public string name;
        public string description;
        public Color playerColor;
        public WeaponPreset[] weapons;
        /// <summary>04 §5 몸체 아트 ID(ArtLibrary). 아트가 없으면 캡슐 플레이스홀더.</summary>
        public string bodyArtId;

        public static readonly JobPreset Bartender = new JobPreset
        {
            job = Job.Bartender,
            name = "바텐더",
            description = "혼합 러시\n새 칵테일 3종마다 12초\n피해 +20% · 공속 +15%\n다양한 조합에 유리",
            playerColor = new Color(0.35f, 0.85f, 0.95f),
            weapons = new[] { WeaponPreset.Rapier, WeaponPreset.Gun },
            bodyArtId = "bartender",
        };

        public static readonly JobPreset TeaSommelier = new JobPreset
        {
            job = Job.TeaSommelier,
            name = "티 소믈리에",
            description = "깊은 우림\n같은 차의 추가 중첩당\n피해 +2% · 초당 회복 +0.1\n추가 중첩 최대 6 · 집중 투자",
            playerColor = new Color(0.55f, 0.85f, 0.5f),
            weapons = new[] { WeaponPreset.Sword, WeaponPreset.Bow },
            bodyArtId = "tea_sommelier",
        };

        public static JobPreset For(Job job) => job == Job.TeaSommelier ? TeaSommelier : Bartender;
    }
}
