using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-17 Codex 추가 아트(화살 전용 꼬리·히든 모티프·아메리카노 액면창 잔·산지 원두통) 연결 계약.
    /// 아트가 오기 전에는 기존 대체 표현, 오면 같은 ID로 자동 사용 — 판정·피해·속성·용량은 바꾸지 않는다.
    /// </summary>
    public class FeedbackArtContractTests
    {
        [Test]
        public void ArrowTail_IdsPerElement_SocketAtFletchEnd()
        {
            Assert.AreEqual("vfx_arrow_fire_tail", PlayerProjectile.TailArtId(PlayerProjectile.ArrowElement.Flame));
            Assert.AreEqual("vfx_arrow_bitter_tail", PlayerProjectile.TailArtId(PlayerProjectile.ArrowElement.Bitter));
            Assert.AreEqual("vfx_arrow_dual_tail", PlayerProjectile.TailArtId(PlayerProjectile.ArrowElement.Flame | PlayerProjectile.ArrowElement.Bitter));
            Assert.IsNull(PlayerProjectile.TailArtId(PlayerProjectile.ArrowElement.None));
            foreach (var id in new[] { "arrow", "tea_arrow" })
            {
                var art = ArtLibrary.Get(id);
                Assert.IsNotNull(art, id);
                var socket = PlayerProjectile.FletchSocketLocal(art);
                Assert.AreEqual(-0.359f, socket.x, 0.005f, id + " 깃 끝 = 불투명 영역 왼쪽(128px 프레임 x=18)");
                Assert.AreEqual(0f, socket.y, 0.02f, id + " 세로 가운데");
            }
        }

        [Test]
        public void HiddenDedicatedArtIds_FollowReviewTable_FallbackToApprovedArt()
        {
            Assert.AreEqual("hidden_rhythm_shaker", ActiveEffects.DedicatedHiddenArt("shake_rhythm"));
            Assert.AreEqual("hidden_weekend_rush_steps", ActiveEffects.DedicatedHiddenArt("weekend_rush"));
            Assert.AreEqual("hidden_iced_cup", ActiveEffects.DedicatedHiddenArt("iced_cup"));
            if (Resources.Load<Sprite>("Icons/Hidden/hidden_iced_cup") == null)
                Assert.IsNotNull(ActiveEffects.HiddenIcon("iced_cup"), "전용 아트 전에는 기존 승인 아트");
        }

        [Test]
        public void Americano_UsesWindowMugWhenDelivered_ElseExistingMugMarkedTemporary()
        {
            Assert.AreEqual("dl_coffee_americano_mug", CoffeeStyles.CupArt(CoffeeStyle.Americano, CoffeeBrewMethod.EspressoMachine));
            bool delivered = DrinkArt.Sprite("dl_coffee_americano_mug") != null;
            Assert.AreEqual(delivered ? "dl_coffee_americano_mug" : "dl_coffee_mug", CoffeeStyles.ResolvedCupArt(CoffeeStyle.Americano, CoffeeBrewMethod.EspressoMachine));
            Assert.AreEqual(!delivered, CoffeeStyles.UsesTempArt(CoffeeStyle.Americano, CoffeeBrewMethod.EspressoMachine), "임시 표시는 실제 액면창 잔이 없을 때만");
            Assert.AreEqual(380f, CoffeeStyles.CupCapacityMl(CoffeeStyle.Americano, CoffeeBrewMethod.EspressoMachine), "용량 불변");
        }

        /// <summary>2026-11-17 회귀: 아메리카노 뜨거운 물이 잔 표시(편집 중 잔·완성 잔)에 들어간다 — 액면창 머그에서 1/10만 찬 것처럼 보이던 결함.</summary>
        [Test]
        public void Americano_HotWaterIsInCupDisplay_TotalMatchesStyleVolume()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.EspressoMachine);
            r.steps.Add(new DrinkStep(DrinkIngredients.CoffeeBean, 190));
            r.coffeeStyle = (int)CoffeeStyle.Americano;
            r.hotWater = 1800;
            var add = DrinkPhysicsSim.CupAdditions(r);
            Assert.AreEqual(180.0, add.TotalVolume, 0.05, "편집 중 잔 = 뜨거운 물 180mL");
            var fin = DrinkPhysicsSim.Final(r);
            Assert.AreEqual(CoffeeStyles.TotalVolumeMl(r), (float)fin.TotalVolume, 0.5f, "완성 잔 부피 = 스타일 총 부피(추출 + 물)");
            Assert.LessOrEqual(fin.TotalVolume / DrinkRules.ServeCapacityMl(r), DrinkRules.ServeFillMax + 1e-3, "채움 상한 82% 이하");
            r.hotWater = 0;
            Assert.AreEqual(0.0, DrinkPhysicsSim.CupAdditions(r).TotalVolume, 0.05, "물 없으면 빈 잔");
        }

        [Test]
        public void Americano_DeliveredWindowMug_MaskSameCanvas_LiquidAnchorFromHandoff()
        {
            var cup = DrinkArt.Sprite("dl_coffee_americano_mug");
            Assert.IsNotNull(cup, "11-17 ready 반입");
            var mask = DrinkArt.Mask("dl_coffee_americano_mug");
            Assert.IsNotNull(mask);
            Assert.AreEqual(new Vector2(36, 36), cup.rect.size);
            Assert.AreEqual(cup.rect.size, mask.rect.size, "잔·마스크 같은 캔버스");
            var lr = DrinkArt.LiquidRect("dl_coffee_americano_mug");
            Assert.AreEqual(0.0833f, lr.xMin, 1e-3f); Assert.AreEqual(0.0556f, lr.yMin, 1e-3f);
            Assert.AreEqual(0.75f, lr.xMax, 1e-3f); Assert.AreEqual(0.8889f, lr.yMax, 1e-3f);
            Assert.IsFalse(CoffeeStyles.UsesTempArt(CoffeeStyle.Americano, CoffeeBrewMethod.EspressoMachine), "임시 문구 해제");
            Assert.IsNotNull(DrinkArt.Sprite("dl_coffee_mug"), "기존 머그 보존(핸드 드립 잔)");
        }
    }
}
