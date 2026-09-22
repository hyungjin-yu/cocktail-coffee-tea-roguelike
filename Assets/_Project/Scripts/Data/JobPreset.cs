using UnityEngine;

namespace CCT
{
    /// <summary>기획서 5장: 시작 시 직업 1개 + 무기 1개 선택. 재료는 직업과 무관하게 항상 둘 다 드랍(임시 결정).</summary>
    public enum Job { Bartender, TeaSommelier }

    /// <summary>
    /// 직업별 프리셋. 지금은 총/활이 없어서 직업당 무기 선택지가 1개뿐이라(레이피어/검) 무기 선택 단계는 생략하고
    /// 직업 선택에 합쳐뒀다. 검은 아직 레이피어와 메커닉이 동일함(치장만 다름) — 별도 무기가 추가되면 갈라낼 것.
    /// </summary>
    public class JobPreset
    {
        public Job job;
        public string name;
        public string weaponName;
        public string description;
        public Color playerColor;

        public static readonly JobPreset Bartender = new JobPreset
        {
            job = Job.Bartender,
            name = "바텐더",
            weaponName = "레이피어",
            description = "칵테일 재료로 성장하는 클래스.\n화염 폭발 스킬 계열과 잘 맞는다.",
            playerColor = new Color(0.35f, 0.85f, 0.95f),
        };

        public static readonly JobPreset TeaSommelier = new JobPreset
        {
            job = Job.TeaSommelier,
            name = "티 소믈리에",
            weaponName = "검",
            description = "차 재료로 성장하는 클래스.\n회복·독 스킬 계열과 잘 맞는다.",
            playerColor = new Color(0.55f, 0.85f, 0.5f),
        };

        public static JobPreset For(Job job) => job == Job.TeaSommelier ? TeaSommelier : Bartender;
    }
}
