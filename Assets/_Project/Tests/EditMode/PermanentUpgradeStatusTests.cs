using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>#17 영구 강화: 강화 가능 / 강화됨(다음 단계 있음) / 완료(최대 단계) / 잠김(실제 이유)을 기호·문구로 구분하고,
    /// 카드·상세·출발 전 버튼이 같은 판정을 쓰며 구매 직후 갱신된다. 구매 규칙·비용·저장은 그대로(PermanentSkillTree/TryPurchase 사용).</summary>
    public class PermanentUpgradeStatusTests : SaveV2Fixture
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyUi()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        PermanentSkillView OpenShop()
        {
            var canvas = new GameObject("ShopCanvas", typeof(RectTransform));
            created.Add(canvas);
            var shop = PermanentSkillView.Create(canvas.transform, null);
            shop.Show();
            created.AddRange(shop.Nodes.Where(n => n != null && !UnityEditor.AssetDatabase.Contains(n)));
            return shop;
        }

        static string Status(PermanentSkillView shop, string id) => shop.transform.Find("Node_" + id + "/Chip/Status").GetComponent<Text>().text;
        static string Cost(PermanentSkillView shop, string id) => shop.transform.Find("Node_" + id + "/Cost").GetComponent<Text>().text;

        [Test]
        public void ThreeStates_BuyableOwnedWithNext_Locked_AndImmediateRefreshAfterPurchase()
        {
            PermanentProgress.AddSpecialty(3);
            var shop = OpenShop();
            Assert.AreEqual("▶ 강화 가능", Status(shop, "dark_roast_blend"));
            Assert.AreEqual("선행 강화 필요", Status(shop, "perfect_crema"));
            Assert.AreEqual((3, 3), (PermanentSkillView.Summary().available, PermanentSkillView.Summary().buyable), "선행 없는 1단계 3개(공격·생존·재료)");

            shop.TryBuy(shop.Nodes.Single(n => n.id == "dark_roast_blend"));
            Assert.AreEqual(PermanentPurchaseResult.Purchased, shop.LastPurchaseResult);
            Assert.AreEqual("✓ 강화됨", Status(shop, "dark_roast_blend"), "구매 직후 같은 화면에서 갱신");
            StringAssert.Contains("현재 1단계 · 다음 비용 6", Cost(shop, "dark_roast_blend"));
            Assert.AreEqual("스페셜티 6 부족", Status(shop, "perfect_crema"));
        }

        [Test]
        public void OwnedNode_DetailShowsCurrentAndNextStepCost_AndMaxStepIsComplete()
        {
            PermanentProgress.AddSpecialty(200);
            PermanentProgress.Current.clearedRuns = 5;
            var shop = OpenShop();
            var roast = shop.Nodes.Single(n => n.id == "dark_roast_blend");
            shop.TryBuy(roast);
            shop.transform.Find("Node_dark_roast_blend").GetComponent<Button>().onClick.Invoke(); // 이미 산 카드 선택 → 상세
            Assert.AreEqual("dark_roast_blend", shop.DetailNode.id);
            StringAssert.Contains("현재 1단계 (비용 3) → 다음 2단계 완벽한 크레마 (비용 6)", shop.DetailText);
            StringAssert.Contains("지금 강화 가능", shop.DetailText);
            StringAssert.Contains("✓ 강화됨", shop.DetailText);

            // 마지막 단계(마스터 카드): 선행 전부 필요 — 순서대로 구매해 최대 단계 표시를 확인한다.
            foreach (var id in new[] { "perfect_crema", "precision_tools", "roast_profile", "master_service", "sturdy_tumbler", "veteran_instinct", "padded_apron", "restorative_break", "balanced_tray",
                                       "bean_cupper", "skilled_roasting", "tidy_counter", "careful_sourcing", "origin_roastery" })
            {
                shop.TryBuy(shop.Nodes.Single(n => n.id == id));
            }
            shop.TryBuy(shop.Nodes.Single(n => n.id == "master_blend"));
            Assert.AreEqual("✓ 완료 (최대 단계)", Status(shop, "master_blend"));
            StringAssert.Contains("적용 중", Cost(shop, "master_blend"));
        }
    }
}
