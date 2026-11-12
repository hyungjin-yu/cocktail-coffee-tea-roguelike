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
        static readonly System.Collections.Generic.Dictionary<MonsterFamily, Sprite> familyCrop = new System.Collections.Generic.Dictionary<MonsterFamily, Sprite>();

        /// <summary>계열 대표 적 그림. 2026-11-12 720p 실측: 몸체 프레임은 투명 여백이 커서 42px 칸에도 적이 ≈20px로 보였다 →
        /// 첫 프레임 알파 영역(ArtAsset.contentRectNormalized, 빌드 시 계산된 값)으로 잘라 칸을 채운다. 원본 텍스처·PNG는 그대로(같은 텍스처의 부분 사각형).</summary>
        public static Sprite Family(MonsterFamily f)
        {
            if (familyCrop.TryGetValue(f, out var cached) && cached != null) return cached;
            var def = FamilyRepresentative(f);
            var src = MasterWavePlan.IconOf(def);
            var s = Cropped(src, def != null ? ArtLibrary.Get(def.ArtId) : null);
            if (s != null) familyCrop[f] = s;
            return s;
        }

        /// <summary>알파 영역으로 자른 같은 텍스처의 스프라이트(아틀라스 묶음·여백 없음이면 원본 그대로).</summary>
        public static Sprite Cropped(Sprite src, ArtAsset art)
        {
            if (src == null || art == null || src.texture == null || src.packed) return src;
            var cn = art.contentRectNormalized;
            if (cn.width >= 0.95f && cn.height >= 0.95f) return src;
            var r = src.rect;
            float x0 = Mathf.Floor(r.x + cn.xMin * r.width), y0 = Mathf.Floor(r.y + cn.yMin * r.height);
            float x1 = Mathf.Ceil(r.x + cn.xMax * r.width), y1 = Mathf.Ceil(r.y + cn.yMax * r.height);
            var crop = Rect.MinMaxRect(Mathf.Max(r.xMin, x0 - 1f), Mathf.Max(r.yMin, y0 - 1f), Mathf.Min(r.xMax, x1 + 1f), Mathf.Min(r.yMax, y1 + 1f));
            if (crop.width < 2f || crop.height < 2f) return src;
            var sp = Sprite.Create(src.texture, crop, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sp.name = src.name + "_hudcrop";
            return sp;
        }

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
