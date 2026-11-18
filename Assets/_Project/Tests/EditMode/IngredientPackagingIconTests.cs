using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 재료 목록·도감 아이콘(DrinkArt.SpriteOrPlaceholder(iconId))은 원형 공통 대체 그림을 쓰지 않는다:
    /// 실제 아이콘 아트 → 실제 포장 아트(bottleId) → 형태 일치 포장 실루엣(액체 = 라벨 병, 찻잎·원두 = 틴) 순.
    /// 실루엣은 아트가 도착하기 전 임시 절차 렌더(placeholder=true)이며 고유 스프라이트가 아니다.
    /// </summary>
    public class IngredientPackagingIconTests
    {
        static float AlphaAt(Sprite s, float u, float v)
        {
            var t = s.texture;
            return t.GetPixel(Mathf.Clamp((int)(u * t.width), 0, t.width - 1), Mathf.Clamp((int)(v * t.height), 0, t.height - 1)).a;
        }

        [Test]
        public void EveryIngredientIcon_IsRealArtOrFormMatchedPackaging_NeverRound()
        {
            int real = 0, packaged = 0;
            var seen = new HashSet<Sprite>();
            foreach (var def in DrinkIngredients.All)
            {
                var s = DrinkArt.SpriteOrPlaceholder(def.iconId, out bool placeholder);
                Assert.IsNotNull(s, def.id);
                Assert.IsTrue(seen.Add(s), $"{def.id}: 재료마다 따로 그린 아이콘(다른 재료와 같은 스프라이트 공유 없음)");
                if (!placeholder)
                {
                    real++;
                    StringAssert.DoesNotStartWith("placeholder_", s.name, def.id);
                    continue;
                }
                packaged++;
                string expected = "placeholder_" + DrinkArt.PackagingId(def);
                Assert.AreEqual(expected, s.name, $"{def.id}: 형태에 맞는 포장 실루엣");
                StringAssert.StartsWith(def.form == IngredientForm.Leaf ? "placeholder_dl_tin_" : "placeholder_dl_bottle_", s.name, def.id);
                // 원형(중심 반지름 0.45) 대체 그림이면 아래 모서리가 비어 있다 — 병·틴 실루엣은 바닥이 채워져 있다.
                Assert.Greater(AlphaAt(s, 0.15f, 0.02f), 0.5f, $"{def.id}: 바닥 모서리가 채워진 포장 형태(원형 아님)");
                Assert.Greater(AlphaAt(s, 0.5f, 0.4f), 0.5f, $"{def.id}: 라벨 영역");
            }
            Assert.GreaterOrEqual(real, 20, "기존 실제 아이콘 아트는 그대로 우선 사용");
            Assert.AreEqual(DrinkIngredients.All.Count, real + packaged);
        }

        [Test]
        public void RealIconArt_StillWinsOverPackagingFallback()
        {
            var gin = DrinkIngredients.Get(DrinkIngredients.Gin);
            var s = DrinkArt.SpriteOrPlaceholder(gin.iconId, out bool placeholder);
            Assert.IsFalse(placeholder);
            Assert.AreSame(DrinkArt.Sprite(gin.iconId), s);
        }

        [Test]
        public void MissingTinArt_DrawsTinNotCircle()
        {
            // 틴 아트가 없는 차(기존에는 원형 실루엣)
            var def = DrinkIngredients.Get("assam_tea");
            Assume.That(def != null && !DrinkArt.Has(def.bottleId));
            var s = DrinkArt.SpriteOrPlaceholder(def.bottleId, out bool placeholder);
            Assert.IsTrue(placeholder);
            Assert.Greater(AlphaAt(s, 0.15f, 0.02f), 0.5f);
            Assert.Greater(AlphaAt(s, 0.5f, 0.86f), 0.5f, "뚜껑 띠");
        }
    }
}
