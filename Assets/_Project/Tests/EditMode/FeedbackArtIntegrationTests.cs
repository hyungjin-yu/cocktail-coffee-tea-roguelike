using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-16 Codex 최종 아트 36개(ArtSource/feedback-art-2026-11-16/manifest.ready.json) Unity 반입 확인:
    /// 실제 Resources 경로·크기, 잔/마스크 같은 캔버스, 잔 앵커(액체 창 = 마스크 불투명 영역), 완성 음료가 임시 잔을 더 쓰지 않음,
    /// 산지 원두 28종 재료 정의 iconId 연결(포장 임시 그림 아님), 거품·잠금 아트 존재. 화면은 QA 투어(feedback2·feedback1)로 따로.
    /// </summary>
    public class FeedbackArtIntegrationTests
    {
        static readonly (string id, int w, int h, string res)[] Assets =
        {
            ("dl_coffee_latte_glass", 36, 36, "DrinkLab/dl_coffee_latte_glass"), ("dl_coffee_latte_glass_mask", 36, 36, "DrinkLab/dl_coffee_latte_glass_mask"),
            ("dl_coffee_flatwhite_cup", 32, 26, "DrinkLab/dl_coffee_flatwhite_cup"), ("dl_coffee_flatwhite_cup_mask", 32, 26, "DrinkLab/dl_coffee_flatwhite_cup_mask"),
            ("dl_coffee_cappuccino_cup", 32, 26, "DrinkLab/dl_coffee_cappuccino_cup"), ("dl_coffee_cappuccino_cup_mask", 32, 26, "DrinkLab/dl_coffee_cappuccino_cup_mask"),
            ("dl_foam_layer", 32, 8, "DrinkLab/dl_foam_layer"), ("ui_lock_badge", 16, 16, "ProgressionUI/ui_lock_badge"),
        };

        static readonly (string ingredient, string icon)[] Origins =
        {
            ("ethiopia_yirgacheffe_kochere", "dl_icon_ethiopia_yirgacheffe_kochere"), ("colombia_huila_vergel", "dl_icon_colombia_huila_vergel"),
            ("brazil_mantiqueira_alves_lima", "dl_icon_brazil_mantiqueira_alves_lima"), ("panama_esmeralda_geisha", "dl_icon_panama_esmeralda_geisha"),
            ("colombia_huila_san_pedro_geisha", "dl_icon_colombia_huila_san_pedro_geisha"), ("colombia_la_esperanza_bourbon", "dl_icon_colombia_la_esperanza_bourbon"),
            ("coffee_ethiopia_guji", "dl_icon_coffee_ethiopia_guji"), ("coffee_ethiopia_sidama", "dl_icon_coffee_ethiopia_sidama"),
            ("coffee_kenya_nyeri", "dl_icon_coffee_kenya_nyeri"), ("coffee_rwanda", "dl_icon_coffee_rwanda"), ("coffee_burundi", "dl_icon_coffee_burundi"),
            ("coffee_uganda", "dl_icon_coffee_uganda"), ("coffee_indonesia_gayo", "dl_icon_coffee_indonesia_gayo"), ("coffee_indonesia_java", "dl_icon_coffee_indonesia_java"),
            ("coffee_vietnam_dalat", "dl_icon_coffee_vietnam_dalat"), ("coffee_india_malabar", "dl_icon_coffee_india_malabar"),
            ("coffee_costa_rica_tarrazu", "dl_icon_coffee_costa_rica_tarrazu"), ("coffee_guatemala_antigua", "dl_icon_coffee_guatemala_antigua"),
            ("coffee_honduras_marcala", "dl_icon_coffee_honduras_marcala"), ("coffee_peru_chanchamayo", "dl_icon_coffee_peru_chanchamayo"),
            ("coffee_mexico_chiapas", "dl_icon_coffee_mexico_chiapas"), ("coffee_jamaica_blue_mountain", "dl_icon_coffee_jamaica_blue_mountain"),
            ("coffee_brazil_cerrado", "dl_icon_coffee_brazil_cerrado"), ("coffee_colombia_narino", "dl_icon_coffee_colombia_narino"),
            ("coffee_el_salvador", "dl_icon_coffee_el_salvador"), ("coffee_nicaragua", "dl_icon_coffee_nicaragua"), ("coffee_tanzania", "dl_icon_coffee_tanzania"),
            ("coffee_bolivia", "dl_icon_coffee_bolivia"),
        };

        [Test]
        public void AllThirtySixAssets_LoadFromResources_WithManifestSize()
        {
            Assert.AreEqual(36, Assets.Length + Origins.Length, "manifest ready 36개");
            foreach (var a in Assets)
            {
                var s = Resources.Load<Sprite>(a.res);
                Assert.IsNotNull(s, a.id + " 반입");
                Assert.AreEqual(new Vector2(a.w, a.h), s.rect.size, a.id + " 크기");
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, a.id + " Point");
            }
            foreach (var o in Origins)
            {
                var s = Resources.Load<Sprite>("DrinkLab/" + o.icon);
                Assert.IsNotNull(s, o.icon);
                Assert.AreEqual(new Vector2(32, 32), s.rect.size, o.icon);
            }
        }

        [Test]
        public void Cups_MaskSameCanvas_AnchorLiquidMatchesMaskOpaqueBox_NoTempArt()
        {
            foreach (var (style, id) in new[] { (CoffeeStyle.Latte, "dl_coffee_latte_glass"), (CoffeeStyle.FlatWhite, "dl_coffee_flatwhite_cup"), (CoffeeStyle.Cappuccino, "dl_coffee_cappuccino_cup") })
            {
                Assert.AreEqual(id, CoffeeStyles.ResolvedCupArt(style, CoffeeBrewMethod.EspressoMachine), style + " 최종 잔");
                Assert.IsFalse(CoffeeStyles.UsesTempArt(style, CoffeeBrewMethod.EspressoMachine), style + " '잔 그림 임시' 문구 해제 조건");
                var cup = DrinkArt.Sprite(id); var mask = DrinkArt.Mask(id);
                Assert.IsNotNull(mask, id + " 마스크");
                Assert.AreEqual(cup.rect.size, mask.rect.size, id + " 잔·마스크 같은 캔버스");
                var lr = DrinkArt.LiquidRect(id);
                Assert.Less(lr.width, 1f, id + " 앵커 액체 창(기본값 아님)");
                Assert.Greater(lr.height, 0.2f, id);
                var g = DrinkArt.Anchor(id, "grip"); var m = DrinkArt.Anchor(id, "mouth");
                Assert.IsTrue(g.x >= 0f && g.x <= 1f && m.y > lr.yMin, id + " 손잡이·입구 앵커");
            }
            Assert.IsNotNull(DrinkArt.Sprite("dl_foam_layer"), "거품 층 아트");
        }

        [Test]
        public void Origins_IngredientIconIdsPointToFinalArt_NotPackagingPlaceholder()
        {
            foreach (var o in Origins)
            {
                var def = DrinkIngredients.Get(o.ingredient);
                Assert.IsNotNull(def, o.ingredient + " 재료 정의");
                Assert.AreEqual(o.icon, def.iconId, o.ingredient + " iconId");
                var sp = DrinkArt.SpriteOrPlaceholder(def.iconId, out bool placeholder);
                Assert.IsFalse(placeholder, o.ingredient + " 실제 아이콘");
                Assert.AreSame(DrinkArt.Sprite(o.icon), sp, o.ingredient + " 포장 그림이 아니라 산지 아이콘");
            }
        }

        [Test]
        public void ArrowTail_HasDarkOutlineForContrast_DisplayOnly()
        {
            Assert.Less(PlayerProjectile.TailOutline.r + PlayerProjectile.TailOutline.g + PlayerProjectile.TailOutline.b, 0.6f, "어두운 테두리");
        }
    }
}
