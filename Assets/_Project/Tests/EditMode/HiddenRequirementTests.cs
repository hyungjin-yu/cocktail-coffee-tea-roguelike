using System.Collections.Generic;
using NUnit.Framework;

namespace CCT.Tests
{
    /// <summary>57 P1: 목표 조합 안내(HiddenRequirements)가 실제 히든 판정(HiddenSkillDef.MetFor)과 직업별로 정확히 같은지.
    /// 모든 요소를 채우면 해금, 어느 한 요소라도 하나 모자라면 미해금이어야 한다 — 안내 문구가 추측한 규칙이 되지 않게.</summary>
    public class HiddenRequirementTests
    {
        sealed class Fake : IHiddenRunState
        {
            public readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
            public readonly HashSet<string> coffee = new HashSet<string>();
            public bool hybrid;
            public int finalForms;
            public readonly HashSet<AttackFinalForm> forms = new HashSet<AttackFinalForm>();
            public Job job;
            public int Stacks(string id) => stacks.TryGetValue(id, out var n) ? n : 0;
            public bool Coffee(string id) => coffee.Contains(id);
            public bool Hybrid => hybrid;
            public int FinalFormCount => finalForms;
            public bool HasFinalForm(AttackFinalForm f) => forms.Contains(f);
            public Job Job => job;
        }

        static Fake Satisfy(IReadOnlyList<HiddenRequirement> reqs, Job job, int skip)
        {
            var f = new Fake { job = job };
            for (int i = 0; i < reqs.Count; i++)
            {
                var r = reqs[i];
                bool shortOne = i == skip;
                switch (r.kind)
                {
                    case HiddenReqKind.Card:
                        f.stacks[r.id] = System.Math.Max(f.Stacks(r.id), r.count - (shortOne ? 1 : 0));
                        break;
                    case HiddenReqKind.Coffee: if (!shortOne) f.coffee.Add(r.id); break;
                    case HiddenReqKind.Hybrid: f.hybrid = !shortOne; break;
                    case HiddenReqKind.FinalFormCount: f.finalForms = shortOne ? r.count - 1 : r.count; break;
                    case HiddenReqKind.FinalForm:
                        if (!shortOne) { f.forms.Add(r.form); f.finalForms = System.Math.Max(f.finalForms, 1); }
                        break;
                }
            }
            // 같은 카드가 두 요소에 쓰이는 경우가 없으므로 skip 요소의 카드 수가 다른 요소로 다시 채워지지 않는다.
            if (skip >= 0 && reqs[skip].kind == HiddenReqKind.Card) f.stacks[reqs[skip].id] = reqs[skip].count - 1;
            return f;
        }

        [Test]
        public void EveryRequirementTable_MatchesRealConditions_ForBothJobs()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
                foreach (var def in HiddenSkillDatabase.All)
                {
                    var reqs = HiddenRequirements.For(def.skill, job);
                    if (!def.AvailableFor(job))
                    {
                        Assert.AreEqual(0, reqs.Count, $"{job} {def.id}: 해금 불가 히든은 조건 표시가 없어야 한다");
                        continue;
                    }
                    Assert.Greater(reqs.Count, 0, $"{job} {def.id}: 조건 표 누락");
                    Assert.IsTrue(def.MetFor(Satisfy(reqs, job, -1)), $"{job} {def.id}: 표의 요소를 모두 채우면 실제로 해금돼야 한다");
                    for (int i = 0; i < reqs.Count; i++)
                        Assert.IsFalse(def.MetFor(Satisfy(reqs, job, i)), $"{job} {def.id}: '{reqs[i].label}'가 모자라면 해금되면 안 된다");
                    var empty = new Fake { job = job };
                    Assert.IsFalse(def.MetFor(empty));
                    Assert.AreEqual(0, HiddenRequirements.MetCount(HiddenRequirements.Progress(def.skill, job, empty)));
                }
        }

        [Test]
        public void Labels_AppearInConditionText()
        {
            foreach (var def in HiddenSkillDatabase.All)
                foreach (var r in HiddenRequirements.For(def.skill, Job.Bartender))
                    if (r.kind == HiddenReqKind.Card || r.kind == HiddenReqKind.Coffee)
                        StringAssert.Contains(r.kind == HiddenReqKind.Coffee ? r.label.Replace("커피 ", "") : r.label, def.condition, def.id);
        }

        [Test]
        public void CardAdvances_ReportsStepAndCompletion()
        {
            var def = HiddenSkillDatabase.Get(HiddenSkill.FirstSip);
            var s = new Fake { job = Job.Bartender };
            s.stacks["dry_gin"] = 1;
            s.stacks["green_tea"] = 2;
            Assert.IsTrue(HiddenRequirements.CardAdvances(def, Job.Bartender, s, "dry_gin", out var before, out bool completes));
            Assert.AreEqual(1, before.current);
            Assert.IsTrue(completes, "드라이 진 2번째 = 첫 잔의 향 완성");
            Assert.IsFalse(HiddenRequirements.CardAdvances(def, Job.Bartender, s, "green_tea", out _, out _), "이미 충족한 요소는 진행 아님");
            Assert.IsFalse(HiddenRequirements.CardAdvances(def, Job.Bartender, s, "molotov", out _, out _), "무관한 카드");
        }

        [Test]
        public void TargetProgressLine_ShowsEveryConditionWithCounts()
        {
            var def = HiddenSkillDatabase.Get(HiddenSkill.FirstSip);
            var s = new Fake { job = Job.Bartender };
            s.stacks["green_tea"] = 2;
            string line = HiddenRequirements.ProgressLine(def, Job.Bartender, s);
            StringAssert.Contains("드라이 진 0/2", line);
            StringAssert.Contains("녹차 잎 2/2 ✓", line);
            Assert.AreEqual("???", HiddenRequirements.DisplayName(def, Job.Bartender, false), "미발견이면 이름만 숨김");
        }

        [Test]
        public void Reachability_BlocksWeaponOnlyCardsAndTiniBow()
        {
            System.Func<string, bool> meleeOnlyBlocked = id => id != "champagne_spray" && id != "tea_leaf_scatter";
            var pb = HiddenRequirements.Reachability(HiddenSkillDatabase.Get(HiddenSkill.PointBlank), Job.Bartender, WeaponKind.Sword, meleeOnlyBlocked);
            Assert.IsFalse(pb.reachable);
            var tini = HiddenRequirements.Reachability(HiddenSkillDatabase.Get(HiddenSkill.TiniMaster), Job.Bartender, WeaponKind.Gun, _ => true);
            Assert.IsFalse(tini.reachable);
            Assert.IsTrue(HiddenRequirements.Reachability(HiddenSkillDatabase.Get(HiddenSkill.TiniMaster), Job.Bartender, WeaponKind.Rapier, _ => true).reachable);
            Assert.IsFalse(HiddenRequirements.Reachability(HiddenSkillDatabase.Get(HiddenSkill.HotToddy), Job.TeaSommelier, WeaponKind.Rapier, _ => true).reachable);
        }
    }
}
