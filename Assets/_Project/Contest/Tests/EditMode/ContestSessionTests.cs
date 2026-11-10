using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CCT.Contest.Tests
{
    public class ContestSessionTests
    {
        static ContestSession Ready(int mission)
        {
            var s = new ContestSession(ContestMissions.Get(mission));
            for (int i = 0; i < s.Conditions.Count; i++) s.CheckCondition(i);
            Assert.IsTrue(s.FinishRequest());
            return s;
        }

        static int ValidOffer(ContestSession s, out int packs)
        {
            for (int i = 0; i < 3; i++)
            {
                var o = s.Mission.Offers[i];
                packs = (s.Mission.Quantity + o.UnitsPerPack - 1) / o.UnitsPerPack;
                if (s.Evaluate(o, packs).Ok) return i;
            }
            packs = 0;
            return -1;
        }

        static void ReceiveCorrectly(ContestSession s)
        {
            while (s.Stage == Stage.Receive)
            {
                if (s.FrontPack.Defect != PackDefect.None) s.RejectPack(); else s.AcceptPack();
            }
        }

        static void BrewAllCorrect(ContestSession s)
        {
            var m = s.Mission;
            while (s.Tray.Count < m.Quantity)
                Assert.IsTrue(s.Brew(m.Product, m.Temp, m.Vessel, System.Math.Min(4, m.Quantity - s.Tray.Count)));
        }

        [Test]
        public void MissionData_IsIntegerDeterministic_AndEachHasValidAndInvalidOffers()
        {
            Assert.AreEqual(3, ContestMissions.All.Length);
            for (int i = 0; i < 3; i++)
            {
                var s = Ready(i);
                Assert.GreaterOrEqual(ValidOffer(s, out _), 0, "mission " + i + " needs a valid offer");
                int invalid = s.Mission.Offers.Count(o => !s.Evaluate(o, (s.Mission.Quantity + o.UnitsPerPack - 1) / o.UnitsPerPack).Ok);
                Assert.GreaterOrEqual(invalid, 1, "mission " + i + " must teach an invalid choice");
                if (i != 1) // 임무 1·3: 가장 싼 공급안이 조건 미달(가격만으로 고르지 않음). 임무 2: 가장 싼 공급안이 조건 충족(가격이 기준은 아님).
                {
                    var cheapest = s.Mission.Offers.OrderBy(o => o.PricePerPack).First();
                    Assert.IsFalse(s.Evaluate(cheapest, (s.Mission.Quantity + cheapest.UnitsPerPack - 1) / cheapest.UnitsPerPack).Ok,
                        "mission " + i + ": cheapest offer must fail a condition");
                }
            }
        }

        [Test]
        public void Request_CannotProceedUntilEveryConditionChecked()
        {
            var s = new ContestSession(ContestMissions.Get(0));
            Assert.IsFalse(s.FinishRequest());
            for (int i = 0; i < s.Conditions.Count - 1; i++) s.CheckCondition(i);
            Assert.IsFalse(s.FinishRequest());
            Assert.AreEqual(Stage.Request, s.Stage);
            s.CheckCondition(s.Conditions.Count - 1);
            Assert.IsTrue(s.FinishRequest());
            Assert.AreEqual(Stage.Supply, s.Stage);
            Assert.IsFalse(s.CheckCondition(0), "no request edits after confirmation");
        }

        [Test]
        public void Supply_RejectsWrongProductShortQuantityAndOverBudget_WithoutStateChange()
        {
            var s = Ready(0);
            var v = s.Order(s.Mission.Offers[1], 1); // 홍차
            Assert.IsFalse(v.Ok);
            Assert.IsTrue(v.Reasons.Any(r => r.Contains("품명 불일치")));
            v = s.Order(s.Mission.Offers[0], 1); // 2단위 < 4
            Assert.IsTrue(v.Reasons.Any(r => r.Contains("수량 부족")));
            v = s.Order(s.Mission.Offers[2], 2); // 16코인 > 12
            Assert.IsTrue(v.Reasons.Any(r => r.Contains("예산 초과")));
            Assert.AreEqual(Stage.Supply, s.Stage);
            Assert.AreEqual(0, s.Spent);
            Assert.AreEqual(0, s.Clock);
            Assert.AreEqual(3, s.RejectedOffers);
        }

        [Test]
        public void Supply_CaffeineAndOriginConditionsEnforced()
        {
            var m2 = Ready(1);
            Assert.IsTrue(m2.Evaluate(m2.Mission.Offers[1], 1).Reasons.Any(r => r.Contains("카페인")));
            var m3 = Ready(2);
            Assert.IsTrue(m3.Evaluate(m3.Mission.Offers[1], 2).Reasons.Any(r => r.Contains("원산지")));
            Assert.IsTrue(m3.Evaluate(m3.Mission.Offers[2], 1).Reasons.Any(r => r.Contains("예산 초과")));
        }

        [Test]
        public void Supply_ValidOrderSpendsBudgetAndBuildsInboundWithDefects()
        {
            var s = Ready(0);
            var v = s.Order(s.Mission.Offers[0], 2);
            Assert.IsTrue(v.Ok);
            Assert.AreEqual(6, s.Spent);
            Assert.AreEqual(6, s.Remaining);
            Assert.AreEqual(ContestMissions.OrderMinutes, s.Clock);
            Assert.AreEqual(Stage.Receive, s.Stage);
            Assert.AreEqual(3, s.InboundCount); // 정상 2 + 파손 1
        }

        [Test]
        public void Receive_RejectingNormalPackResendsAndCostsTime_AcceptingDefectStoresIt()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[0], 2);
            Assert.AreEqual(PackDefect.None, s.FrontPack.Defect);
            int clock = s.Clock;
            s.RejectPack();
            Assert.AreEqual(clock + ContestMissions.ResendMinutes, s.Clock);
            Assert.AreEqual(3, s.InboundCount);
            Assert.AreEqual(PackDefect.Damaged, s.FrontPack.Defect);
            s.AcceptPack();
            Assert.AreEqual(1, s.Stock.Count(l => l.Defect == PackDefect.Damaged));
            s.AcceptPack(); s.AcceptPack();
            Assert.AreEqual(Stage.Brew, s.Stage);
            Assert.AreEqual(6, s.UnitsOf(Product.GreenTea));
        }

        [Test]
        public void Brew_ChecksUnitsAndBatchLimit_UsesOldestStockFirst()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[0], 2);
            s.AcceptPack(); s.AcceptPack(); s.AcceptPack(); // 정상, 파손, 정상
            Assert.IsNotNull(s.BrewError(Product.GreenTea, Temp.T80, Vessel.TeaCup, 5));
            Assert.IsNotNull(s.BrewError(Product.BlackTea, Temp.T80, Vessel.TeaCup, 1));
            Assert.IsTrue(s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 3));
            Assert.AreEqual(PackDefect.None, s.Tray[0].SourceDefect);
            Assert.AreEqual(PackDefect.None, s.Tray[1].SourceDefect);
            Assert.AreEqual(PackDefect.Damaged, s.Tray[2].SourceDefect);
            Assert.AreEqual(3, s.UnitsOf(Product.GreenTea));
        }

        [Test]
        public void Inspection_FlagsSpecificMismatches_BlocksDelivery_UntilFixedAndReinspected()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[2], 1); // 들판농원 5단위
            ReceiveCorrectly(s);
            s.Brew(Product.GreenTea, Temp.T95, Vessel.TeaCup, 2); // 온도 불일치
            s.Brew(Product.GreenTea, Temp.T80, Vessel.Mug, 1);    // 용기 불일치
            var r = s.Inspect();
            Assert.IsFalse(r.Pass);
            Assert.AreEqual(3, r.Nonconforming);
            Assert.IsTrue(r.Issues.Any(i => i.Contains("온도 불일치") && i.Contains("2잔")));
            Assert.IsTrue(r.Issues.Any(i => i.Contains("용기 불일치")));
            Assert.IsTrue(r.Issues.Any(i => i.Contains("수량 부족")));
            Assert.IsTrue(r.Fixes.Any(f => f.Contains("추가 확보")), "only 2 units left for 4 cups → re-supply guidance");
            Assert.IsNull(s.Deliver());
            Assert.AreEqual(3, s.ExcludeNonconforming());
            Assert.AreEqual(1, s.Reworks);
            Assert.IsFalse(s.CanDeliver);
            // 남은 2단위 + 추가 확보 1봉(5단위, 8코인 > 남은 4코인) 불가 → 실패 판정
            Assert.AreEqual(Stage.Failed, s.Stage);
            StringAssert.Contains("예산", s.FailReason);
        }

        [Test]
        public void Reorder_SameOfferRestoresShortage_ThenPass()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[0], 2); // 6코인
            ReceiveCorrectly(s);
            s.Brew(Product.GreenTea, Temp.T95, Vessel.TeaCup, 2);
            s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 2);
            s.Inspect();
            s.ExcludeNonconforming();
            Assert.AreEqual(Stage.Brew, s.Stage);
            Assert.IsFalse(s.Order(s.Mission.Offers[2], 1).Ok, "reorder must use the contracted offer");
            Assert.IsTrue(s.Order(s.Chosen, 1).Ok);
            Assert.AreEqual(1, s.InboundCount, "no defects on reorder");
            ReceiveCorrectly(s);
            s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 2);
            var r = s.Inspect();
            Assert.IsTrue(r.Pass, string.Join(",", r.Issues));
        }

        [Test]
        public void DefectiveIngredient_DetectedAtInspection_ExclusionReturnsRemainingDefectStock()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[0], 2);
            s.AcceptPack(); s.AcceptPack(); s.AcceptPack();
            s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 4);
            var r = s.Inspect();
            Assert.IsFalse(r.Pass);
            Assert.IsTrue(r.Issues.Any(i => i.Contains("재료 이력 불일치")));
            s.ExcludeNonconforming();
            s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 2);
            Assert.IsTrue(s.Inspect().Pass);
            Assert.LessOrEqual(s.Clock, s.Mission.DeadlineMinutes);
        }

        [Test]
        public void Delivery_RequiresCurrentInspection_AndIsIdempotent()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[2], 1); // 5단위
            ReceiveCorrectly(s);
            BrewAllCorrect(s);
            Assert.IsTrue(s.Inspect().Pass);
            s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 1); // 검수 후 변경
            Assert.IsFalse(s.CanDeliver);
            Assert.IsNull(s.Deliver());
            var r = s.Inspect();
            Assert.IsTrue(r.Pass);
            Assert.AreEqual(1, r.Surplus);
            var receipt = s.Deliver();
            Assert.IsNotNull(receipt);
            Assert.AreEqual(4, receipt.Accepted);
            Assert.AreEqual(1, receipt.Surplus);
            Assert.IsNull(s.Deliver(), "second delivery must be ignored");
            Assert.AreEqual(Stage.Done, s.Stage);
            Assert.IsFalse(s.Brew(Product.GreenTea, Temp.T80, Vessel.TeaCup, 1));
            Assert.AreEqual(1, s.History.Count(h => h.Contains("인수:")));
        }

        [Test]
        public void Deadline_ExceededByRepeatedReturns_FailsMission()
        {
            var s = Ready(0);
            s.Order(s.Mission.Offers[0], 2);
            int guard = 0;
            while (s.Stage == Stage.Receive && guard++ < 50) s.RejectPack();
            Assert.AreEqual(Stage.Failed, s.Stage);
            StringAssert.Contains("납기", s.FailReason);
            Assert.Greater(s.Clock, s.Mission.DeadlineMinutes);
        }

        [Test]
        public void OptimalPlay_AllMissionsCompleteWithinDeadlineAndBudget_ThreeStars()
        {
            for (int i = 0; i < 3; i++)
            {
                var s = Ready(i);
                int offer = ValidOffer(s, out int packs);
                Assert.IsTrue(s.Order(s.Mission.Offers[offer], packs).Ok);
                ReceiveCorrectly(s);
                BrewAllCorrect(s);
                var r = s.Inspect();
                Assert.IsTrue(r.Pass, "mission " + i + ": " + string.Join(",", r.Issues));
                var rc = s.Deliver();
                Assert.AreEqual(3, rc.Stars);
                Assert.LessOrEqual(rc.Minutes, s.Mission.DeadlineMinutes);
                Assert.LessOrEqual(rc.Spent, s.Mission.Budget);
            }
        }

        [Test]
        public void Restart_CreatesCleanStateAndDoesNotMutateMissionData()
        {
            var a = Ready(1);
            a.Order(a.Mission.Offers[0], 2);
            a.AcceptPack();
            var b = new ContestSession(ContestMissions.Get(1));
            Assert.AreEqual(Stage.Request, b.Stage);
            Assert.AreEqual(0, b.Clock);
            Assert.AreEqual(0, b.Spent);
            Assert.AreEqual(0, b.Stock.Count);
            Assert.AreEqual(0, b.Tray.Count);
            Assert.AreEqual(3, ContestMissions.Get(1).Offers[0].UnitsPerPack);
            Assert.AreEqual(14, ContestMissions.Get(1).Budget);
        }

        [Test]
        public void WrongProductPack_InMission2_IsNotUsedForChamomileButFailsIfBrewed()
        {
            var s = Ready(1);
            s.Order(s.Mission.Offers[0], 2);
            while (s.Stage == Stage.Receive) s.AcceptPack();
            Assert.AreEqual(3, s.UnitsOf(Product.BlackTea));
            Assert.AreEqual(6, s.UnitsOf(Product.Chamomile));
            s.Brew(Product.BlackTea, Temp.T95, Vessel.Mug, 1);
            var r = s.Inspect();
            Assert.IsTrue(r.Issues.Any(i => i.Contains("품명 불일치")));
        }
    }

    public class ContestSaveTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "pps-contest-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        static DeliveryReceipt Receipt(string id, int stars = 3, int minutes = 30) =>
            new DeliveryReceipt { MissionId = id, Accepted = 4, Minutes = minutes, Spent = 6, Stars = stars };

        [Test]
        public void Commit_GrantsBadgeOncePerMission_AndPersists()
        {
            var path = Path.Combine(dir, ContestSave.FileName);
            var save = new ContestSave(path);
            Assert.AreEqual("new", save.LoadNote);
            var r1 = Receipt("m1_library");
            Assert.IsTrue(save.Commit(r1));
            Assert.IsFalse(save.Commit(r1), "same receipt twice");
            Assert.IsFalse(save.Commit(Receipt("m1_library", 2, 25)), "replay never re-grants");
            Assert.AreEqual(1, save.Data.badges);
            var reload = new ContestSave(path);
            Assert.AreEqual("ok", reload.LoadNote);
            Assert.AreEqual(1, reload.Data.badges);
            var rec = reload.Record("m1_library");
            Assert.AreEqual(3, rec.bestStars);
            Assert.AreEqual(25, rec.bestMinutes);
            Assert.AreEqual(2, rec.plays);
            Assert.IsTrue(reload.IsUnlocked(1));
            Assert.IsFalse(reload.IsUnlocked(2));
        }

        [Test]
        public void CorruptMain_FallsBackToBackup_ThenToNew()
        {
            var path = Path.Combine(dir, ContestSave.FileName);
            var save = new ContestSave(path);
            save.Commit(Receipt("m1_library"));
            save.Commit(Receipt("m2_center")); // 두 번째 쓰기에서 .bak 생성(1개 배지 상태)
            Assert.IsTrue(File.Exists(path + ".bak"));
            File.WriteAllText(path, "{ broken");
            var fromBak = new ContestSave(path);
            Assert.AreEqual("backup", fromBak.LoadNote);
            Assert.AreEqual(1, fromBak.Data.badges);
            File.WriteAllText(path + ".bak", "garbage");
            var fresh = new ContestSave(path);
            Assert.AreEqual("reset-corrupt", fresh.LoadNote);
            Assert.AreEqual(0, fresh.Data.badges);
        }

        [Test]
        public void ForeignJson_IsNotAcceptedAsContestSave()
        {
            var path = Path.Combine(dir, ContestSave.FileName);
            File.WriteAllText(path, "{\"version\":1,\"product\":\"steam\",\"badges\":9,\"missions\":[]}");
            var s = new ContestSave(path);
            Assert.AreEqual(0, s.Data.badges);
        }

        [Test]
        public void FirstMissionUnlocked_OthersLockedOnNewSave()
        {
            var s = new ContestSave(Path.Combine(dir, ContestSave.FileName));
            Assert.IsTrue(s.IsUnlocked(0));
            Assert.IsFalse(s.IsUnlocked(1));
            Assert.IsFalse(s.IsUnlocked(2));
        }
    }
}
