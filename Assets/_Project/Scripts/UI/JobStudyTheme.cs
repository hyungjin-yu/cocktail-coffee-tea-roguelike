using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    // Stable skill IDs preserve saves. Tea runs present the shared research effects as tea study.
    public static class JobStudyTheme
    {
        static readonly Dictionary<string,string> teaNames = new Dictionary<string,string>
        {
            {"espresso_shot","첫물 우림"}, {"ristretto","농축 우림"},
            {"caffeine_rush","차 향의 활력"}, {"balanced_extraction","균형 우림"},
            {"perfect_extraction","완전 우림"}, {"doppio_body","겹겹의 향"},
            {"insulated_cup","보온 찻잔"}, {"mellow_finish","부드러운 여운"},
            {"wide_tray","다구 쟁반"}, {"full_body_roast","깊은 숙성"},
            {"fine_grind","찻잎 고르기"}, {"cold_brew","냉침"},
            {"steady_hand","안정된 찻손"}, {"measured_pour","정량 따르기"},
            {"zero_waste","찻물 남김없이"}
        };
        static Dictionary<string,string> teaWords;
        public static string TreeTitle(Job job) => job == Job.TeaSommelier ? "우림 연구" : "커피 연구";
        public static string Name(SkillData node, Job job) => job == Job.TeaSommelier && teaNames.TryGetValue(node.id,out var name) ? name : node.displayName;
        public static string Text(string text, Job job)
        {
            if(job!=Job.TeaSommelier || string.IsNullOrEmpty(text)) return text;
            if(teaWords==null)
            {
                teaWords=new Dictionary<string,string>();
                foreach(var node in SkillDatabase.LoadAll())
                    if(teaNames.TryGetValue(node.id,out var name)) teaWords[node.displayName]=name;
            }
            foreach(var pair in teaWords) text=text.Replace(pair.Key,pair.Value);
            return text.Replace("커피 포인트", "우림 포인트").Replace("커피", "우림 연구").Replace("원두", "우림 지식");
        }
        public static string HiddenName(HiddenSkillDef def, Job job) => job!=Job.TeaSommelier ? def.name : def.skill switch
        {
            HiddenSkill.BeanCrate => "찻잎 꾸러미",
            HiddenSkill.TriRoast => "세 갈래 다도",
            HiddenSkill.ShakeRhythm => "공부차 우림",
            _ => Text(def.name,job)
        };
    }
}
