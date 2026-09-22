using UnityEngine;

namespace CCT
{
    /// <summary>기획서 5장: 시작 시 직업 1개 + 무기 1개 선택. 재료는 직업과 무관하게 항상 둘 다 드랍(임시 결정).</summary>
    public enum Job { Bartender, TeaSommelier }

    /// <summary>
    /// 무기 종류. 레이피어/검, 총/활은 각각 메커닉이 같다(근접 vs 원거리로만 갈림) — 시각/이름만 다른 치장.
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

        public static readonly WeaponPreset Rapier = new WeaponPreset
        { kind = WeaponKind.Rapier, name = "레이피어", description = "근접 찌르기. 연속 공격에 강하다." };
        public static readonly WeaponPreset Gun = new WeaponPreset
        { kind = WeaponKind.Gun, name = "총", description = "원거리 사격. 더 먼 거리에서 안전하게 싸운다." };
        public static readonly WeaponPreset Sword = new WeaponPreset
        { kind = WeaponKind.Sword, name = "검", description = "근접 베기. 연속 공격에 강하다." };
        public static readonly WeaponPreset Bow = new WeaponPreset
        { kind = WeaponKind.Bow, name = "활", description = "원거리 사격. 더 먼 거리에서 안전하게 싸운다." };
    }

    /// <summary>직업별 프리셋. 직업당 무기 선택지 2개(기획서 5장 표).</summary>
    public class JobPreset
    {
        public Job job;
        public string name;
        public string description;
        public Color playerColor;
        public WeaponPreset[] weapons;

        public static readonly JobPreset Bartender = new JobPreset
        {
            job = Job.Bartender,
            name = "바텐더",
            description = "칵테일 재료로 성장하는 클래스.\n화염 폭발 스킬 계열과 잘 맞는다.",
            playerColor = new Color(0.35f, 0.85f, 0.95f),
            weapons = new[] { WeaponPreset.Rapier, WeaponPreset.Gun },
        };

        public static readonly JobPreset TeaSommelier = new JobPreset
        {
            job = Job.TeaSommelier,
            name = "티 소믈리에",
            description = "차 재료로 성장하는 클래스.\n회복·독 스킬 계열과 잘 맞는다.",
            playerColor = new Color(0.55f, 0.85f, 0.5f),
            weapons = new[] { WeaponPreset.Sword, WeaponPreset.Bow },
        };

        public static JobPreset For(Job job) => job == Job.TeaSommelier ? TeaSommelier : Bartender;
    }
}
