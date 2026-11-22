using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-22 플레이 피드백 B(15): 능력치마다 고유 색·아이콘 하나. 레벨업 카드와 영구 강화가 같은 표를 쓴다.
    /// 효과 한 줄 = 아이콘 + 능력치 이름(색) + 짧은 수치. 아이콘은 임시 아트(Resources/StatIcons, Tools/Art/make_stat_icons.py — 색 동일).
    /// 색은 어두운 카드 바탕(#2A2030 부근)에서 읽히게 밝은 쪽으로 고르고, 서로 색상(hue)이 겹치지 않게 나눴다. 색만으로 구분하지 않도록 아이콘·이름을 함께 쓴다.
    /// </summary>
    public static class StatStyle
    {
        public struct Entry
        {
            public string key, label;
            public Color color;
        }

        static readonly Dictionary<CardEffectType, Entry> table = new Dictionary<CardEffectType, Entry>
        {
            { CardEffectType.DamagePercent, E("damage", "공격력", 255, 107, 90) },
            { CardEffectType.AttackSpeedPercent, E("attack_speed", "공격 속도", 255, 210, 74) },
            { CardEffectType.RangePercent, E("range", "사거리", 90, 210, 255) },
            { CardEffectType.MoveSpeedPercent, E("move_speed", "이동 속도", 107, 224, 122) },
            { CardEffectType.MaxHpFlat, E("max_hp", "최대 체력", 255, 127, 168) },
            { CardEffectType.RegenBonus, E("regen", "분당 회복", 127, 255, 208) },
            { CardEffectType.DamageReduction, E("damage_reduction", "받는 피해 감소", 157, 184, 255) },
            { CardEffectType.FlameLevel, E("flame", "적중 화염", 255, 154, 60) },
            { CardEffectType.PoisonLevel, E("poison", "적중 여운", 197, 140, 255) },
            { CardEffectType.PierceCount, E("pierce", "관통", 208, 216, 232) },
            { CardEffectType.ExtraThrust, E("multi_hit", "연속 타격", 255, 176, 122) },
            { CardEffectType.DropChancePercent, E("drop", "추가 드랍", 245, 196, 106) },
            { CardEffectType.GaugeNeedPercent, E("gauge", "게이지 필요량", 236, 226, 196) },
            { CardEffectType.BeanGainPercent, E("bean", "원두 획득", 217, 160, 102) },
            { CardEffectType.PickupRadiusFlat, E("pickup", "줍기 반경", 200, 240, 106) },
        };

        static Entry E(string key, string label, int r, int g, int b) =>
            new Entry { key = key, label = label, color = new Color(r / 255f, g / 255f, b / 255f) };

        /// <summary>히든 조합·직업 연계 표시 색(능력치 색과 겹치지 않는 금색·청록).</summary>
        public static readonly Color HiddenColor = new Color(1f, 0.84f, 0.32f);
        public static readonly Color JobColorBartender = new Color(1f, 0.6f, 0.32f);
        public static readonly Color JobColorTea = new Color(0.55f, 0.9f, 0.55f);
        public static Color JobColor(Job job) => job == Job.TeaSommelier ? JobColorTea : JobColorBartender;

        public static IEnumerable<CardEffectType> Types => table.Keys;
        public static bool Has(CardEffectType t) => table.ContainsKey(t);
        public static Entry Get(CardEffectType t) => table.TryGetValue(t, out var e) ? e : new Entry { key = "", label = t.ToString(), color = Color.white };
        public static Color ColorOf(CardEffectType t) => Get(t).color;

        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        /// <summary>능력치 아이콘(임시 아트). 없으면 null.</summary>
        public static Sprite Icon(CardEffectType t)
        {
            var key = Get(t).key;
            if (string.IsNullOrEmpty(key)) return null;
            if (icons.TryGetValue(key, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("StatIcons/stat_" + key);
            icons[key] = s;
            return s;
        }

        /// <summary>능력치 이름(무기에 따라 다르게 읽히는 것만 바꾼다: 원거리 전용 추가 타격 = 다중 발사).</summary>
        public static string Label(CardEffectType t, WeaponRestriction restriction = WeaponRestriction.Any)
        {
            if (t == CardEffectType.ExtraThrust && restriction == WeaponRestriction.RangedOnly) return "다중 발사";
            return Get(t).label;
        }

        /// <summary>짧은 수치(정수, 부호 포함). 비율은 %, 받는 피해 감소·영구 추가 드랍은 %p, 회복은 분당, 줍기 반경은 cm.</summary>
        public static string Value(CardEffect e, bool percentPoint = false)
        {
            float v = e.value;
            string sign = v < 0f ? "-" : "+";
            float a = Mathf.Abs(v);
            switch (e.type)
            {
                case CardEffectType.DamagePercent:
                case CardEffectType.AttackSpeedPercent:
                case CardEffectType.RangePercent:
                case CardEffectType.MoveSpeedPercent:
                case CardEffectType.GaugeNeedPercent:
                case CardEffectType.BeanGainPercent:
                    return sign + IntNum.Percent(a) + "%";
                case CardEffectType.DropChancePercent:
                    return sign + IntNum.Percent(a) + (percentPoint ? "%p" : "%");
                case CardEffectType.DamageReduction:
                    return sign + IntNum.Percent(a) + "%p";
                case CardEffectType.RegenBonus:
                    return sign + IntNum.PerMinute(a);
                case CardEffectType.PickupRadiusFlat:
                    return sign + IntNum.Round(a * 100.0) + "cm";
                case CardEffectType.FlameLevel:
                case CardEffectType.PoisonLevel:
                    return "Lv " + sign + IntNum.Round(a);
                case CardEffectType.ExtraThrust:
                    return sign + IntNum.Round(a) + "회";
                default:
                    return sign + IntNum.Round(a);
            }
        }

        /// <summary>효과 한 줄 글자(테스트·접근성용): "공격력 +15%".</summary>
        public static string LineText(CardEffect e, WeaponRestriction restriction = WeaponRestriction.Any, bool percentPoint = false) =>
            Label(e.type, restriction) + " " + Value(e, percentPoint);

        /// <summary>효과 한 줄을 그린다: [아이콘] 이름(능력치 색) 수치(밝게·굵게). 부모 안 왼쪽 정렬 가로 줄. 반환 = 줄 루트.</summary>
        public static RectTransform BuildLine(RectTransform parent, string name, CardEffect e, WeaponRestriction restriction,
            float width, float height, int fontSize, bool percentPoint = false, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var row = UiFactory.Place(name, parent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, height));
            row.pivot = new Vector2(0.5f, 1f);
            var color = ColorOf(e.type);
            float icon = Mathf.Round(height * 0.86f);
            string label = Label(e.type, restriction), value = Value(e, percentPoint);
            var t = UiFactory.AddText(UiFactory.Place("Text", row, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width - icon - 8f, height)),
                $"{label} <b><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(color, Color.white, 0.55f))}>{value}</color></b>", fontSize, TextAnchor.MiddleLeft, color);
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = t.rectTransform;
            var img = UiFactory.Place("Icon", row, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(icon, icon)).gameObject.AddComponent<Image>();
            img.sprite = Icon(e.type); img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = img.sprite != null;
            // 가운데 정렬: 아이콘 + 간격 + 글자 폭을 한 묶음으로 가운데에 둔다(글자 폭은 렌더 기준 preferredWidth).
            float textW = Mathf.Min(t.preferredWidth, width - icon - 8f);
            float total = icon + 8f + textW;
            float left = align == TextAnchor.MiddleLeft ? 0f : (width - total) * 0.5f;
            ((RectTransform)img.transform).pivot = new Vector2(0f, 0.5f);
            ((RectTransform)img.transform).anchoredPosition = new Vector2(left, 0f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(left + icon + 8f, 0f);
            trt.sizeDelta = new Vector2(textW + 4f, height);
            return row;
        }
    }
}
