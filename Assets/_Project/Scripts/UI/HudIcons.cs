using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-12 텍스트 중심 HUD 가시성 개선 공용 부품. 기존 승인 스프라이트만 재사용한다(새 아트·원본 수정 없음):
    /// 음료 = DrinkLab 잔/도구 그림, 몬스터 계열 = 그 계열 대표 적 몸체 첫 프레임, 직업 = 직업 몸체 첫 프레임, 무기 = 무기 아이콘, 재료 = 재료 아이콘.
    /// 색만으로 구분하지 않도록 항상 짧은 글자 라벨([칵]/[커]/[차]/[중] 표식·이름·수치)을 함께 둔다.
    /// </summary>
    public static class HudIcons
    {
        static Sprite Res(string path) => string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);

        public static Sprite Drink(DrinkKind? kind, CoffeeBrewMethod method = 0) => kind switch
        {
            DrinkKind.Cocktail => Res("DrinkLab/dl_glass_cocktail"),
            DrinkKind.Tea => Res("DrinkLab/dl_teacup"),
            DrinkKind.Coffee => method == CoffeeBrewMethod.EspressoMachine ? Res("DrinkLab/dl_coffee_espresso")
                : method == CoffeeBrewMethod.PourOver ? Res("DrinkLab/dl_coffee_dripper")
                : method == CoffeeBrewMethod.MokaPot ? Res("DrinkLab/dl_coffee_moka") : Res("DrinkLab/dl_coffee_cup"),
            _ => null,
        };

        /// <summary>계열 대표 적(칵테일 = 라임웨지, 커피 = 에스프레소 벅, 차 = 티백 임프). 중립은 그림 없음(글자 [중]).</summary>
        public static EnemyDef FamilyRepresentative(MonsterFamily f) => f switch
        {
            MonsterFamily.Cocktail => EnemyDef.Imp,
            MonsterFamily.Coffee => EnemyDef.EspressoBug,
            MonsterFamily.Tea => EnemyDef.TeabagImp,
            _ => null,
        };

        /// <summary>계열 대표 적 그림(상성 대상 표시).</summary>
        public static Sprite Family(MonsterFamily f) => MasterWavePlan.IconOf(FamilyRepresentative(f));

        /// <summary>2026-11-12 사용자 지시: 계열 이름 대신 서로 모양이 다른 잔 그림(칵테일잔·커피잔·찻잔). 중립은 그림 없음 → 호출자가 '중립' 글자.</summary>
        public static Sprite FamilyCup(MonsterFamily f) => f switch
        {
            MonsterFamily.Cocktail => Res("DrinkLab/dl_glass_cocktail"),
            MonsterFamily.Coffee => Res("DrinkLab/dl_coffee_cup"),
            MonsterFamily.Tea => Res("DrinkLab/dl_teacup"),
            _ => null,
        };

        public static Sprite Job(Job job) => ArtLibrary.Get(JobPreset.For(job).bodyArtId)?.Clip("idle")?.frames[0];

        public static Sprite Weapon(WeaponKind w) => Res(WeaponPreset.For(w)?.iconResourcePath);

        public static Sprite Ingredient(IngredientType t) => t == IngredientType.Tea ? SpriteFactory.TeaIcon
            : t == IngredientType.Coffee ? Res("Icons/coffee_bean") : SpriteFactory.CocktailIngredientIcon;

        /// <summary>그림 칸(비율 유지, 클릭 통과). sprite가 없으면 숨긴다.</summary>
        public static Image Icon(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size, Sprite sprite)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            Set(img, sprite);
            return img;
        }

        public static void Set(Image img, Sprite sprite)
        {
            if (img == null) return;
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        public static Text Label(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string text, int font, Color color, TextAnchor align = TextAnchor.MiddleLeft, int minFont = 14)
        {
            var t = UiFactory.AddText(UiFactory.Place(name, parent, anchor, pos, size), text, font, align, color);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = minFont; t.resizeTextMaxSize = font;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>가로 진행 막대(배경 + 채움). Set으로 0~1.</summary>
        public sealed class Bar
        {
            public RectTransform root;
            public Image fill;
            public float Value { get; private set; }
            public void SetValue(float v)
            {
                Value = Mathf.Clamp01(v);
                if (fill != null) fill.rectTransform.anchorMax = new Vector2(Value, 1f);
            }
        }

        public static Bar ProgressBar(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            UiFactory.AddImage(rt, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = false;
            var frt = UiFactory.Stretch("Fill", rt);
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
            var fill = UiFactory.AddImage(frt, fillColor);
            fill.raycastTarget = false;
            var bar = new Bar { root = rt, fill = fill };
            bar.SetValue(0f);
            return bar;
        }

        /// <summary>단계 칸(예: 새 칵테일 2/3종) — 채운 칸은 밝게 + 안에 ✓ 대신 숫자 없이 테두리 굵기 차이. 진행 수치 글자는 호출자가 함께 쓴다.</summary>
        public static Image[] Pips(RectTransform parent, string name, Vector2 anchor, Vector2 pos, int count, float size, float gap)
        {
            var arr = new Image[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = UiFactory.AddImage(UiFactory.Place(name + i, parent, anchor, pos + new Vector2(i * (size + gap), 0f), new Vector2(size, size)), Color.white);
                arr[i].raycastTarget = false;
            }
            return arr;
        }

        public static void SetPips(Image[] pips, int filled, Color on, Color off)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++) if (pips[i] != null) pips[i].color = i < filled ? on : off;
        }
    }
}
