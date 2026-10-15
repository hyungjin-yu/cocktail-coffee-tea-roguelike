using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>Resources/Icons 에 실제 아이콘이 들어오면 색상 원 대신 그 아이콘을 그대로(흰색) 쓰는지 확인.</summary>
    public class IngredientPickupVisualTests
    {
        [Test]
        public void Spawn_UsesRealIcon_ForEachIngredientType()
        {
            var parent = new GameObject("Parent").transform;

            AssertUsesIcon(IngredientType.Cocktail, "Icons/cocktail_ingredient", parent);
            AssertUsesIcon(IngredientType.Tea, "Icons/tea", parent);
            AssertUsesIcon(IngredientType.Coffee, "Icons/coffee_bean", parent);

            Object.DestroyImmediate(parent.gameObject);
        }

        static void AssertUsesIcon(IngredientType type, string resourcePath, Transform parent)
        {
            var expected = IngredientPickup.DropArt(type) ?? Resources.Load<Sprite>(resourcePath); // 57 새 드롭 아트 우선
            Assert.IsNotNull(expected, $"{resourcePath} 아이콘이 Resources 에 있어야 한다");

            IngredientPickup.Spawn(null, null, Vector2.zero, type, 1, parent);
            var sr = parent.GetComponentInChildren<SpriteRenderer>();

            Assert.AreEqual(expected, sr.sprite, $"{type} 는 {resourcePath} 아이콘을 써야 한다");
            Assert.AreEqual(Color.white, sr.color, "실제 아이콘은 색을 덧씌우면 안 된다");
            Assert.AreEqual(0.26f * Balance.IngredientIconVisualScale * Balance.PickupReadabilityScale, sr.transform.localScale.x, 0.001f,
                "실제 아이콘은 캔버스 여백 보정 배율이 적용돼야 한다");

            Object.DestroyImmediate(sr.gameObject);
        }
    }
}
