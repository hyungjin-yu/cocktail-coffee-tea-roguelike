using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-11-12 UI 아이콘(ready 매니페스트 묶음) 실제 화면 연결: 연구 트리 노드 그림(바텐더 = 공용, 티 소믈리에 = 차 변형),
    /// 그림이 단축키·이름·효과·상태 글과 겹치지 않고 노드 안, 그림 없는 노드는 기존 배치 그대로.</summary>
    public class UiIconWiringPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        static Rect R(Transform t)
        {
            var c = new Vector3[4]; ((RectTransform)t).GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator ResearchNode_ShowsOwnArt_PerJob_WithoutOverlappingText(
            [Values(Job.Bartender, Job.TeaSommelier)] Job job)
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            var game = GameBootstrap.Build(job, job == Job.Bartender ? WeaponKind.Rapier : WeaponKind.Bow);
            yield return null;
            game.OpenCoffeeTree();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var view = game.SkillView.transform;
            var node = Find(view, "Node_espresso_shot");
            Assert.IsNotNull(node);
            var art = Find(node, "NodeArt");
            Assert.IsNotNull(art, "ready 그림이 있는 노드는 그림 칸");
            var img = art.GetComponent<Image>();
            Assert.IsTrue(img.enabled && img.sprite != null);
            Assert.AreEqual(job == Job.TeaSommelier ? "research_espresso_shot_tea" : "research_espresso_shot", img.sprite.name);
            var ar = R(art);
            Assert.IsTrue(R(node).Contains(ar.min) && R(node).Contains(ar.max), "그림은 노드 안");
            foreach (var n in new[] { "Key", "Name", "Desc", "Status" })
            {
                var t = Find(node, n);
                if (t == null) continue;
                var text = t.GetComponent<Text>();
                if (text != null && string.IsNullOrEmpty(text.text)) continue;
                Assert.IsFalse(ar.Overlaps(R(t)), $"그림이 {n}와 겹침");
            }
            // 모든 노드: 효과 글이 한 줄(상태 줄과 겹침 없음)
            foreach (var data in SkillDatabase.LoadAll())
            {
                var d = Find(Find(view, "Node_" + data.id), "Desc");
                var dt = d.GetComponent<Text>();
                Assert.LessOrEqual(dt.cachedTextGenerator.lineCount, 1, data.id + " 효과 글 한 줄");
                var st = Find(Find(view, "Node_" + data.id), "Status");
                Assert.IsFalse(R(d).Overlaps(R(st)), data.id + " 효과 칸이 상태 칸과 겹침");
            }
            foreach (var data in SkillDatabase.LoadAll())
                if (ResearchIcons.Research(data.id, job) == null)
                    Assert.IsNull(Find(Find(view, "Node_" + data.id), "NodeArt"), data.id + ": 그림이 아직 없는 노드는 기존 글 상자 그대로");
        }
    }
}
