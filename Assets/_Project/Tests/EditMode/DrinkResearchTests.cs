using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>72 §3~§5 연구 포인트·숙련 트리·직원 채용 규칙(순수 상태).</summary>
    public class DrinkResearchTests
    {
        [Test]
        public void Tree_12Nodes_3Branches_CostsAndPrerequisites()
        {
            Assert.AreEqual(12, DrinkResearch.Traits.Length);
            foreach (var b in new[] { DrinkResearch.Service, DrinkResearch.Stability, DrinkResearch.Finesse })
                for (int tier = 1; tier <= 4; tier++)
                {
                    var t = DrinkResearch.TraitAt(b, tier);
                    Assert.IsNotNull(t);
                    Assert.AreEqual(new[] { 4, 6, 8, 10 }[tier - 1], t.cost);
                    Assert.AreEqual(tier == 1 ? null : DrinkResearch.TraitAt(b, tier - 1).id, t.Prerequisite);
                }
        }

        [Test]
        public void Unlock_NeedsPrerequisiteAndRp_UnlockAndEquipAreSeparate()
        {
            var s = new ResearchState();
            Assert.IsTrue(DrinkResearch.GrantFirst(s));
            Assert.IsFalse(DrinkResearch.GrantFirst(s), "첫 10 RP는 한 번만");
            Assert.AreEqual(10, s.researchPoints);
            Assert.IsFalse(DrinkResearch.Unlock(s, "service_2", out var why)); StringAssert.Contains("먼저", why);
            Assert.AreEqual(10, s.researchPoints, "실패는 상태 불변");
            Assert.IsFalse(DrinkResearch.Equip(s, "service_1", out _), "미해금 장착 불가");
            Assert.IsTrue(DrinkResearch.Unlock(s, "service_1", out _));
            Assert.AreEqual(6, s.researchPoints);
            Assert.AreEqual(DrinkResearch.NodeState.Owned, DrinkResearch.StateOf(s, DrinkResearch.Trait("service_1")), "해금 ≠ 장착");
            Assert.AreEqual(DrinkResearch.NodeState.Available, DrinkResearch.StateOf(s, DrinkResearch.Trait("service_2")));
            Assert.IsTrue(DrinkResearch.Unlock(s, "service_2", out _));
            Assert.AreEqual(0, s.researchPoints);
            Assert.IsFalse(DrinkResearch.Unlock(s, "service_2", out _), "중복 해금 없음(이중 클릭)");
            Assert.IsFalse(DrinkResearch.Unlock(s, "service_3", out why)); StringAssert.Contains("부족", why);
            Assert.IsTrue(DrinkResearch.Equip(s, "service_2", out _), "선행 미장착이어도 장착 가능");
            Assert.AreEqual(DrinkResearch.NodeState.Equipped, DrinkResearch.StateOf(s, DrinkResearch.Trait("service_2")));
        }

        [Test]
        public void Equip_ThreeSlots_SameBranchAllowed()
        {
            var s = new ResearchState { researchPoints = 100 };
            foreach (var id in new[] { "stability_1", "stability_2", "stability_3", "stability_4" }) Assert.IsTrue(DrinkResearch.Unlock(s, id, out _));
            Assert.IsTrue(DrinkResearch.Equip(s, "stability_1", out _));
            Assert.IsTrue(DrinkResearch.Equip(s, "stability_2", out _));
            Assert.IsTrue(DrinkResearch.Equip(s, "stability_3", out _));
            Assert.IsFalse(DrinkResearch.Equip(s, "stability_4", out var why)); StringAssert.Contains("슬롯", why);
            Assert.IsTrue(DrinkResearch.Unequip(s, "stability_1"));
            Assert.IsTrue(DrinkResearch.Equip(s, "stability_4", out _));
            Assert.AreEqual(3, s.equippedTraits.Count);
        }

        [Test]
        public void Staff_ConditionsCostSpecialty()
        {
            var s = new ResearchState { researchPoints = 30 };
            Assert.IsFalse(DrinkResearch.Hire(s, "staff_cocktail_blend", out var why), "칵테일 제조 3회 필요"); StringAssert.Contains("3회", why);
            Assert.IsTrue(DrinkResearch.Hire(s, "staff_cocktail_aroma", out _));
            Assert.AreEqual(24, s.researchPoints);
            Assert.IsFalse(DrinkResearch.Hire(s, "staff_cocktail_aroma", out _), "중복 채용 없음");
            s.cocktailCrafts = 3;
            Assert.IsTrue(DrinkResearch.Hire(s, "staff_cocktail_blend", out _));
            Assert.IsFalse(DrinkResearch.SetSpecialty(s, "staff_tea_aroma", out _), "채용 안 한 직원 연구는 활성 불가");
            Assert.IsTrue(DrinkResearch.SetSpecialty(s, "staff_cocktail_blend", out _));
            Assert.IsTrue(DrinkResearch.SetSpecialty(s, "staff_cocktail_aroma", out _), "무료 교체");
            Assert.AreEqual("staff_cocktail_aroma", s.activeSpecialty);
            Assert.IsTrue(DrinkResearch.SetSpecialty(s, null, out _));
            Assert.AreEqual("", s.activeSpecialty);
        }

        [Test]
        public void SettleRun_OncePerRunId()
        {
            var s = new ResearchState();
            Assert.AreEqual(5 + 2 + 4, DrinkResearch.SettleRun(s, "r1", 650, 1, 1));
            Assert.AreEqual(0, DrinkResearch.SettleRun(s, "r1", 650, 1, 1), "같은 런 재정산 없음");
            Assert.AreEqual(11, s.researchPoints);
        }

        [Test]
        public void Sanitize_CorruptOrUnknown_KeepsUnknownOwnership_DropsInvalidEquip()
        {
            var s = new ResearchState { researchPoints = -3, cocktailCrafts = -1 };
            s.unlockedTraits.AddRange(new[] { "service_1", "service_1", "future_trait", "" });
            s.equippedTraits.AddRange(new[] { "future_trait", "service_1", "service_2", "service_1" });
            s.hiredStaff.Add("future_staff");
            s.activeSpecialty = "future_staff";
            s.unlockedTraits.Add(null);
            DrinkResearch.Sanitize(s);
            CollectionAssert.AreEqual(new[] { "service_1", "future_trait" }, s.unlockedTraits, "알 수 없는 해금 보존, 중복·빈 값 제거");
            CollectionAssert.AreEqual(new[] { "service_1" }, s.equippedTraits);
            CollectionAssert.Contains(s.hiredStaff, "future_staff");
            Assert.AreEqual("", s.activeSpecialty, "알 수 없는 전문은 활성 안 함");
            Assert.AreEqual(0, s.researchPoints);
            Assert.AreEqual(0, s.cocktailCrafts);
        }
    }
}
