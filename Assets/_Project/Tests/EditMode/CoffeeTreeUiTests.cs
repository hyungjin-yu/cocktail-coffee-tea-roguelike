using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>08:39 입력 잠금 판정과 커피 트리 화면 배치(08:47 안전 여백48·본문24px·겹침 없음)를
    /// 렌더링 없이 RectTransform 계산으로 검증한다. 실제 화면 캡처/육안 확인을 대신하지 않는다.</summary>
    public class CoffeeTreeUiTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        // ---- InputReleaseGate ----

        [Test]
        public void Gate_BlocksOpeningFrame_EvenIfNothingHeld()
        {
            var gate = new InputReleaseGate();
            gate.Arm(100);
            Assert.IsFalse(gate.Allows(100, false), "연 프레임의 Submit/클릭은 직전 화면 것이다");
            Assert.IsTrue(gate.Allows(101, false));
        }

        [Test]
        public void Gate_StaysLocked_WhileConfirmHeld_ThenOpensAfterRelease()
        {
            var gate = new InputReleaseGate();
            gate.Arm(10);
            for (int f = 11; f < 30; f++)
                Assert.IsFalse(gate.Allows(f, true), $"frame {f}: 눌린 채면 잠금 유지");
            gate.Poll(30, false); // 뗀 프레임
            Assert.IsFalse(gate.Locked);
            Assert.IsTrue(gate.Allows(31, true), "뗀 뒤 새로 누른 확정 입력은 허용");
        }

        [Test]
        public void Gate_NewPress_WithoutObservedRelease_IsBlocked()
        {
            // 눌림 해제를 한 번도 관찰하지 못했다면(계속 누르고 있었다면) 다음 프레임에도 허용하지 않는다.
            var gate = new InputReleaseGate();
            gate.Arm(5);
            Assert.IsFalse(gate.Allows(6, true));
            Assert.IsFalse(gate.Allows(7, true));
        }

        [Test]
        public void Gate_Rearm_LocksAgain()
        {
            var gate = new InputReleaseGate();
            gate.Arm(1);
            gate.Poll(2, false);
            Assert.IsFalse(gate.Locked);
            gate.Arm(3);
            Assert.IsTrue(gate.Locked, "다음 연쇄 화면이 열리면 다시 잠근다");
        }

        // ---- 커피 트리 배치 ----

        SkillTreeView CreateView(out RectTransform canvasRt, out SkillTree tree)
        {
            var canvasGo = new GameObject("TestCanvas", typeof(RectTransform));
            created.Add(canvasGo);
            canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.sizeDelta = new Vector2(1920f, 1080f);
            canvasRt.anchorMin = canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRt.position = Vector3.zero;

            var nodes = DefaultSkills.CreateRuntime();
            created.AddRange(nodes);
            var t = new SkillTree(nodes);
            tree = t;
            var stats = new PlayerStats();
            var view = SkillTreeView.Create(canvasRt);
            int points = 2;
            view.Show(t, () => points, n =>
            {
                if (!t.Unlock(n, stats)) return false;
                points--;
                return true;
            }, () => { });
            return view;
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [Test]
        public void Layout_TopLevelElements_DoNotOverlap_AndStayInSafeArea()
        {
            var view = CreateView(out var canvasRt, out _);
            var root = (RectTransform)view.transform;

            var elements = new List<RectTransform>();
            foreach (RectTransform child in root)
                if (child.name == "Title" || child.name == "Points" || child.name == "CloseButton" ||
                    child.name.StartsWith("Header_") || child.name.StartsWith("Node_"))
                    elements.Add(child);
            Assert.AreEqual(1 + 1 + 1 + 3 + 15, elements.Count);

            var safe = Rect.MinMaxRect(-960f + 48f, -540f + 48f, 960f - 48f, 540f - 48f);
            foreach (var e in elements)
            {
                var r = WorldRect(e);
                Assert.IsTrue(r.xMin >= safe.xMin - 0.01f && r.xMax <= safe.xMax + 0.01f &&
                              r.yMin >= safe.yMin - 0.01f && r.yMax <= safe.yMax + 0.01f,
                    $"{e.name} {r} 가 안전 여백48 밖");
            }
            for (int i = 0; i < elements.Count; i++)
                for (int j = i + 1; j < elements.Count; j++)
                {
                    var a = WorldRect(elements[i]);
                    var b = WorldRect(elements[j]);
                    bool overlap = a.xMin < b.xMax - 0.01f && b.xMin < a.xMax - 0.01f &&
                                   a.yMin < b.yMax - 0.01f && b.yMin < a.yMax - 0.01f;
                    Assert.IsFalse(overlap, $"{elements[i].name} 와 {elements[j].name} 겹침");
                }
        }

        [Test]
        public void Layout_NodeInnerTexts_DoNotOverlap_AndBodyIsAtLeast24px()
        {
            var view = CreateView(out _, out _);
            var root = (RectTransform)view.transform;

            foreach (var text in root.GetComponentsInChildren<Text>(true))
                Assert.GreaterOrEqual(text.fontSize, 24, $"{text.transform.parent.name}/{text.name} — 08:47 최소24px");

            foreach (RectTransform node in root)
            {
                if (!node.name.StartsWith("Node_")) continue;
                var parts = node.Cast<RectTransform>().Where(t => t.GetComponent<Text>() != null).ToList();
                var nodeRect = WorldRect(node);
                foreach (var p in parts)
                {
                    var r = WorldRect(p);
                    Assert.IsTrue(nodeRect.Contains(r.min) && nodeRect.Contains(r.max), $"{node.name}/{p.name} 노드 밖");
                }
                for (int i = 0; i < parts.Count; i++)
                    for (int j = i + 1; j < parts.Count; j++)
                    {
                        var a = WorldRect(parts[i]);
                        var b = WorldRect(parts[j]);
                        bool overlap = a.xMin < b.xMax - 0.01f && b.xMin < a.xMax - 0.01f &&
                                       a.yMin < b.yMax - 0.01f && b.yMin < a.yMax - 0.01f;
                        Assert.IsFalse(overlap, $"{node.name}: {parts[i].name} / {parts[j].name} 겹침");
                    }
            }
        }

        [Test]
        public void Show_DisplaysRemainingPoints_AndStatesUpdateAfterSpend()
        {
            var view = CreateView(out _, out var tree);
            StringAssert.Contains("남은 커피 포인트 2", view.PointsLabel);
            Assert.IsTrue(view.InputLocked, "연 직후에는 사용자 입력 경로가 잠겨 있다");

            view.Choose(tree.Nodes.Single(n => n.id == "espresso_shot"));
            Assert.IsTrue(view.IsOpen, "포인트가 남아 있으면 열린 채");
            StringAssert.Contains("남은 커피 포인트 1", view.PointsLabel);

            var status = view.transform.Find("Node_ristretto/Status").GetComponent<Text>();
            StringAssert.Contains("해금 가능", status.text, "선행이 풀린 노드는 즉시 가능 상태로");
            var unlocked = view.transform.Find("Node_espresso_shot/Status").GetComponent<Text>();
            Assert.AreEqual("해금됨", unlocked.text);
        }
    }
}
