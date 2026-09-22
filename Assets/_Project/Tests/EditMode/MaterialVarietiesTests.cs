using System.Linq;
using NUnit.Framework;

namespace CCT.Tests
{
    public class MaterialVarietiesTests
    {
        [Test]
        public void EachCategory_HasSixVarieties()
        {
            Assert.AreEqual(6, MaterialVarieties.Cocktail.Count);
            Assert.AreEqual(6, MaterialVarieties.Tea.Count);
            Assert.AreEqual(6, MaterialVarieties.Coffee.Count);
        }

        [Test]
        public void For_ReturnsMatchingCategory()
        {
            Assert.AreSame(MaterialVarieties.Cocktail, MaterialVarieties.For(IngredientType.Cocktail));
            Assert.AreSame(MaterialVarieties.Tea, MaterialVarieties.For(IngredientType.Tea));
            Assert.AreSame(MaterialVarieties.Coffee, MaterialVarieties.For(IngredientType.Coffee));
        }

        [Test]
        public void AllVarieties_HaveUniqueIds_AndNoEmptyFields()
        {
            var all = MaterialVarieties.Cocktail
                .Concat(MaterialVarieties.Tea)
                .Concat(MaterialVarieties.Coffee)
                .ToList();

            var ids = all.Select(v => v.id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "품종 id가 서로 겹치면 안 된다");

            foreach (var variety in all)
            {
                Assert.IsFalse(string.IsNullOrEmpty(variety.id));
                Assert.IsFalse(string.IsNullOrEmpty(variety.displayName));
                Assert.IsFalse(string.IsNullOrEmpty(variety.flavorText));
            }
        }
    }
}
