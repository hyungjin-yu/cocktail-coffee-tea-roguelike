using System.Collections.Generic;
using System.Linq;

namespace CCT.Contest
{
    public enum Stage { Request, Supply, Receive, Brew, Inspect, Done, Failed }

    public sealed class Pack
    {
        public int Serial;
        public Product Product;
        public int Units;
        public PackDefect Defect;
        public bool Ordered; // 대금을 낸 주문분인지(결함 포장은 추가 배송분)
    }

    public sealed class Lot
    {
        public int Serial;
        public Product Product;
        public int Units;
        public PackDefect Defect;
    }

    public sealed class Cup
    {
        public int Id;
        public Product Product;
        public Temp Temp;
        public Vessel Vessel;
        public PackDefect SourceDefect;
        public int Batch;
    }

    public sealed class OfferVerdict
    {
        public bool Ok;
        public int Units;
        public int Cost;
        public readonly List<string> Reasons = new List<string>();
    }

    public sealed class InspectionReport
    {
        public bool Pass;
        public int Conforming;
        public int Nonconforming;
        public int Surplus;
        public bool Late;
        public int TrayVersion;
        public readonly List<string> Issues = new List<string>();
        public readonly List<string> Fixes = new List<string>();
        public readonly Dictionary<int, string> CupFailures = new Dictionary<int, string>();
    }

    public sealed class DeliveryReceipt
    {
        public string MissionId;
        public int Accepted;
        public int Surplus;
        public int Minutes;
        public int Spent;
        public int Reworks;
        public int Stars;
    }

    /// <summary>임무 한 번의 진행 상태. 순수 로직(Unity 의존 없음) — 결정적이며 단위 테스트 대상.
    /// 요청 확인 → 공급안 선택 → 입고 확인 → 제조 → 검수(불일치 보완) → 납품.</summary>
    public sealed class ContestSession
    {
        public readonly Mission Mission;
        public Stage Stage { get; private set; } = Stage.Request;
        public int Clock { get; private set; }
        public int Spent { get; private set; }
        public int Remaining => Mission.Budget - Spent;
        public int Reworks { get; private set; }
        public int RejectedOffers { get; private set; }
        public int Inspections { get; private set; }
        public string FailReason { get; private set; }
        public SupplyOffer Chosen { get; private set; }
        public InspectionReport LastReport { get; private set; }
        public DeliveryReceipt Receipt { get; private set; }

        readonly bool[] checkedConditions;
        readonly List<string> conditions;
        readonly Queue<Pack> inbound = new Queue<Pack>();
        readonly List<Lot> stock = new List<Lot>();
        readonly List<Cup> tray = new List<Cup>();
        readonly List<string> log = new List<string>();
        int trayVersion;
        int nextSerial = 1;
        int nextCup = 1;
        int batch;

        public ContestSession(Mission mission)
        {
            Mission = mission;
            conditions = mission.Conditions();
            checkedConditions = new bool[conditions.Count];
            Log("요청 접수", mission.Agency + " · " + mission.EventName + " — " + ContestText.Name(mission.Product) + " " + mission.Quantity + "잔");
        }

        public IReadOnlyList<string> Conditions => conditions;
        public bool IsChecked(int i) => checkedConditions[i];
        public int CheckedCount => checkedConditions.Count(c => c);
        public IReadOnlyList<string> History => log;
        public IReadOnlyList<Cup> Tray => tray;
        public IReadOnlyList<Lot> Stock => stock;
        public int InboundCount => inbound.Count;
        public Pack FrontPack => inbound.Count > 0 ? inbound.Peek() : null;
        public int TrayVersion => trayVersion;

        public int UnitsOf(Product p) => stock.Where(l => l.Product == p).Sum(l => l.Units);
        public IEnumerable<Product> StockProducts => stock.Where(l => l.Units > 0).Select(l => l.Product).Distinct();

        void Log(string stage, string text) => log.Add("[" + Clock.ToString("00") + "분] " + stage + ": " + text);

        void Advance(int minutes) => Clock += minutes;

        // ---------- 1. 요청 확인 ----------
        public bool CheckCondition(int index)
        {
            if (Stage != Stage.Request || index < 0 || index >= checkedConditions.Length) return false;
            checkedConditions[index] = true;
            return true;
        }

        public bool CanFinishRequest => Stage == Stage.Request && checkedConditions.All(c => c);

        public bool FinishRequest()
        {
            if (!CanFinishRequest) return false;
            Log("요청 확인", "품명·수량·규격·납기·예산 " + conditions.Count + "개 항목 확인");
            Stage = Stage.Supply;
            return true;
        }

        // ---------- 2. 공급안 선택 ----------
        public OfferVerdict Evaluate(SupplyOffer offer, int packs)
        {
            var v = new OfferVerdict { Units = offer.UnitsPerPack * packs, Cost = offer.PricePerPack * packs };
            if (offer.Product != Mission.Product)
                v.Reasons.Add("품명 불일치: 요청은 " + ContestText.Name(Mission.Product) + ", 공급안은 " + ContestText.Name(offer.Product) + "입니다.");
            if (Mission.RequireCaffeineFree && offer.ContainsCaffeine)
                v.Reasons.Add("성분 조건 불충족: 요청은 카페인 없음, 이 공급안은 카페인이 들어 있습니다.");
            if (Mission.RequireOriginLabel && !offer.OriginLabeled)
                v.Reasons.Add("표시 조건 불충족: 원산지 표시가 없습니다.");
            int need = Mission.Quantity - ConformingCups() - (UnitsOf(Mission.Product) - DefectiveUnits());
            if (packs <= 0) v.Reasons.Add("주문 수량이 없습니다. 포장 개수를 정하세요.");
            else if (v.Units < need) v.Reasons.Add("수량 부족: 필요 " + need + "단위, 주문 " + v.Units + "단위입니다.");
            if (v.Cost > Remaining) v.Reasons.Add("예산 초과: 남은 예산 " + Remaining + "코인, 비용 " + v.Cost + "코인입니다.");
            v.Ok = v.Reasons.Count == 0;
            return v;
        }

        /// <summary>최초 주문(Supply 단계) 또는 추가 주문(Brew/Inspect 단계, 같은 공급안). 조건 미달이면 상태를 바꾸지 않는다.</summary>
        public OfferVerdict Order(SupplyOffer offer, int packs)
        {
            bool reorder = Stage == Stage.Brew || Stage == Stage.Inspect;
            if (!(Stage == Stage.Supply || reorder) || (reorder && offer != Chosen))
                return new OfferVerdict { Reasons = { "지금은 주문할 수 없습니다." } };
            var v = Evaluate(offer, packs);
            if (!v.Ok)
            {
                if (!reorder) RejectedOffers++;
                Log("공급안 반려", offer.Supplier + " " + packs + "봉 — " + string.Join(" / ", v.Reasons));
                return v;
            }
            Chosen = offer;
            Spent += v.Cost;
            Advance(ContestMissions.OrderMinutes);
            Log(reorder ? "추가 확보" : "공급안 선택", offer.Supplier + " " + ContestText.Name(offer.Product) + " " + packs + "봉(" + v.Units + "단위) · " + v.Cost + "코인");
            BuildInbound(offer, packs, reorder);
            Stage = Stage.Receive;
            return v;
        }

        void BuildInbound(SupplyOffer offer, int packs, bool reorder)
        {
            var defects = reorder ? new DefectSpec[0] : Mission.Defects;
            for (int i = 0; i <= packs; i++)
            {
                foreach (var d in defects)
                    if (d.Position == i)
                        inbound.Enqueue(new Pack
                        {
                            Serial = nextSerial++, Units = offer.UnitsPerPack, Defect = d.Defect, Ordered = false,
                            Product = d.Defect == PackDefect.WrongProduct ? d.WrongProduct : offer.Product,
                        });
                if (i < packs)
                    inbound.Enqueue(new Pack { Serial = nextSerial++, Units = offer.UnitsPerPack, Product = offer.Product, Ordered = true });
            }
        }

        // ---------- 3. 입고 확인(재료 확보) ----------
        public bool AcceptPack()
        {
            if (Stage != Stage.Receive || inbound.Count == 0) return false;
            var p = inbound.Dequeue();
            stock.Add(new Lot { Serial = p.Serial, Product = p.Product, Units = p.Units, Defect = p.Defect });
            Log("입고", "#" + p.Serial + " " + ContestText.Name(p.Product) + " " + p.Units + "단위 입고" + (p.Defect != PackDefect.None ? " (확인 누락: " + ContestText.Name(p.Defect) + ")" : ""));
            FinishReceiveIfEmpty();
            return true;
        }

        public bool RejectPack()
        {
            if (Stage != Stage.Receive || inbound.Count == 0) return false;
            var p = inbound.Dequeue();
            if (p.Defect != PackDefect.None)
                Log("반품", "#" + p.Serial + " " + ContestText.Name(p.Defect) + " 포장 반품");
            else
            {
                // 정상 포장을 반품하면 공급자가 다시 보내 시간만 든다(주문분은 결국 도착).
                Advance(ContestMissions.ResendMinutes);
                var resend = new Pack { Serial = nextSerial++, Product = p.Product, Units = p.Units, Ordered = true };
                inbound.Enqueue(resend);
                Log("반품", "#" + p.Serial + " 정상 포장을 반품 → 재발송 #" + resend.Serial + " (+" + ContestMissions.ResendMinutes + "분)");
                CheckDeadline();
                if (Stage == Stage.Failed) return true;
            }
            FinishReceiveIfEmpty();
            return true;
        }

        void FinishReceiveIfEmpty()
        {
            if (inbound.Count > 0) return;
            Advance(ContestMissions.ReceiveMinutes);
            Log("입고 완료", ContestText.Name(Mission.Product) + " 재고 " + UnitsOf(Mission.Product) + "단위");
            Stage = Stage.Brew;
            CheckFeasible();
        }

        // ---------- 4. 제조 ----------
        public string BrewError(Product recipe, Temp temp, Vessel vessel, int cups)
        {
            if (Stage != Stage.Brew && Stage != Stage.Inspect) return "지금은 제조할 수 없습니다.";
            if (cups < 1 || cups > ContestMissions.MaxCupsPerBatch) return "한 번에 1~" + ContestMissions.MaxCupsPerBatch + "잔까지 만들 수 있습니다.";
            int have = UnitsOf(recipe);
            if (have < cups) return ContestText.Name(recipe) + " 재료가 부족합니다(보유 " + have + "단위).";
            return null;
        }

        public bool Brew(Product recipe, Temp temp, Vessel vessel, int cups)
        {
            if (BrewError(recipe, temp, vessel, cups) != null) return false;
            batch++;
            for (int i = 0; i < cups; i++)
            {
                var lot = stock.First(l => l.Product == recipe && l.Units > 0); // 먼저 입고된 재료부터 사용
                lot.Units--;
                tray.Add(new Cup { Id = nextCup++, Product = recipe, Temp = temp, Vessel = vessel, SourceDefect = lot.Defect, Batch = batch });
            }
            stock.RemoveAll(l => l.Units <= 0);
            trayVersion++;
            Advance(ContestMissions.BrewMinutes);
            Stage = Stage.Brew;
            Log("제조", "배치 " + batch + ": " + ContestText.Name(recipe) + " " + ContestText.Name(temp) + " " + ContestText.Name(vessel) + " " + cups + "잔");
            CheckDeadline();
            return true;
        }

        public string CupFailure(Cup c)
        {
            if (c.Product != Mission.Product) return "품명 불일치(" + ContestText.Name(c.Product) + ")";
            if (c.Temp != Mission.Temp) return "온도 불일치(" + ContestText.Name(c.Temp) + ")";
            if (c.Vessel != Mission.Vessel) return "용기 불일치(" + ContestText.Name(c.Vessel) + ")";
            if (c.SourceDefect != PackDefect.None) return "재료 이력 불일치(" + ContestText.Name(c.SourceDefect) + ")";
            return null;
        }

        int ConformingCups() => tray.Count(c => CupFailure(c) == null);

        // ---------- 5. 검사·검수 ----------
        public bool CanInspect => (Stage == Stage.Brew || Stage == Stage.Inspect) && tray.Count > 0;

        public InspectionReport Inspect()
        {
            if (!CanInspect) return null;
            Advance(ContestMissions.InspectMinutes);
            Inspections++;
            var r = new InspectionReport { TrayVersion = trayVersion, Late = Clock > Mission.DeadlineMinutes };
            foreach (var c in tray)
            {
                var f = CupFailure(c);
                if (f == null) r.Conforming++;
                else { r.Nonconforming++; r.CupFailures[c.Id] = f; }
            }
            foreach (var g in r.CupFailures.Values.GroupBy(v => v))
                r.Issues.Add(g.Key + " " + g.Count() + "잔");
            if (r.Nonconforming > 0)
                r.Fixes.Add("불일치 " + r.Nonconforming + "잔을 납품 대상에서 제외(폐기)하세요.");
            int shortBy = Mission.Quantity - r.Conforming;
            if (shortBy > 0)
            {
                r.Issues.Add("수량 부족: 요청 " + Mission.Quantity + "잔 중 적합 " + r.Conforming + "잔");
                int units = UnitsOf(Mission.Product) - DefectiveUnits();
                r.Fixes.Add(units >= shortBy
                    ? "요청 조건(" + ContestText.Name(Mission.Temp) + "·" + ContestText.Name(Mission.Vessel) + ")으로 " + shortBy + "잔을 더 제조하세요."
                    : "재료가 " + (shortBy - units) + "단위 부족합니다. 같은 공급안으로 추가 확보하세요(남은 예산 " + Remaining + "코인).");
            }
            r.Surplus = r.Conforming > Mission.Quantity ? r.Conforming - Mission.Quantity : 0;
            if (r.Late) r.Issues.Add("납기 초과: " + Clock + "분 / 납기 " + Mission.DeadlineMinutes + "분");
            r.Pass = !r.Late && r.Nonconforming == 0 && r.Conforming >= Mission.Quantity;
            LastReport = r;
            Stage = Stage.Inspect;
            Log("검수 " + Inspections + "차", r.Pass ? "적합 " + r.Conforming + "잔 — 납품 승인" + (r.Surplus > 0 ? " (초과 " + r.Surplus + "잔 제외)" : "") : "불합격 — " + string.Join(", ", r.Issues));
            if (r.Late) Fail("납기를 넘겨 이번 요청을 마칠 수 없습니다.");
            return r;
        }

        int DefectiveUnits() => stock.Where(l => l.Product == Mission.Product && l.Defect != PackDefect.None).Sum(l => l.Units);

        /// <summary>검수에서 확인된 불일치 물품을 제외하고, 같은 결함의 남은 재료도 반품한다(보완 조치).</summary>
        public int ExcludeNonconforming()
        {
            if (Stage != Stage.Inspect || LastReport == null || LastReport.TrayVersion != trayVersion || LastReport.Nonconforming == 0) return 0;
            var defects = tray.Where(c => CupFailure(c) != null && c.SourceDefect != PackDefect.None).Select(c => c.SourceDefect).Distinct().ToList();
            int removed = tray.RemoveAll(c => CupFailure(c) != null);
            int returned = stock.Where(l => l.Defect != PackDefect.None && defects.Contains(l.Defect)).Sum(l => l.Units);
            stock.RemoveAll(l => l.Defect != PackDefect.None && defects.Contains(l.Defect));
            trayVersion++;
            Reworks++;
            Stage = Stage.Brew;
            Log("보완", "불일치 " + removed + "잔 제외" + (returned > 0 ? ", 결함 재료 " + returned + "단위 반품" : ""));
            CheckFeasible();
            return removed;
        }

        // ---------- 6. 납품 ----------
        public bool CanDeliver => Stage == Stage.Inspect && LastReport != null && LastReport.Pass && LastReport.TrayVersion == trayVersion && Receipt == null;

        public DeliveryReceipt Deliver()
        {
            if (!CanDeliver) return null;
            int stars = 3 - (Reworks > 0 ? 1 : 0) - (RejectedOffers > 1 ? 1 : 0);
            Receipt = new DeliveryReceipt
            {
                MissionId = Mission.Id, Accepted = Mission.Quantity, Surplus = LastReport.Surplus,
                Minutes = Clock, Spent = Spent, Reworks = Reworks, Stars = stars < 1 ? 1 : stars,
            };
            Log("인수", "적합 물품 " + Mission.Quantity + "잔 인수 완료" + (LastReport.Surplus > 0 ? " · 초과 " + LastReport.Surplus + "잔은 인수 대상 아님" : ""));
            Stage = Stage.Done;
            return Receipt;
        }

        // ---------- 실패 판정 ----------
        void CheckDeadline()
        {
            if (Clock > Mission.DeadlineMinutes) Fail("납기를 넘겨 이번 요청을 마칠 수 없습니다.");
        }

        void CheckFeasible()
        {
            CheckDeadline();
            if (Stage == Stage.Failed || Chosen == null) return;
            int need = Mission.Quantity - ConformingCups();
            int usable = UnitsOf(Mission.Product) - DefectiveUnits();
            if (need <= usable) return;
            int packsNeeded = (need - usable + Chosen.UnitsPerPack - 1) / Chosen.UnitsPerPack;
            if (packsNeeded * Chosen.PricePerPack > Remaining)
                Fail("남은 예산(" + Remaining + "코인)으로 부족한 재료 " + (need - usable) + "단위를 확보할 수 없습니다.");
        }

        void Fail(string reason)
        {
            if (Stage == Stage.Failed) return;
            FailReason = reason;
            Stage = Stage.Failed;
            Log("중단", reason);
        }
    }
}
