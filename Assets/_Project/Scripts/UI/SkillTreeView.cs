using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>원두 게이지가 찰 때마다 열리는 커피 스킬트리. 해금할 노드를 플레이어가 직접 고른다.</summary>
    public class SkillTreeView : MonoBehaviour
    {
        class NodeSlot
        {
            public SkillData data;
            public Image background;
            public Text status;
        }

        readonly List<NodeSlot> slots = new List<NodeSlot>();
        GameObject root;
        SkillTree tree;
        Action<SkillData> onChosen;

        static readonly Color LockedColor = new Color(0.14f, 0.12f, 0.12f);
        static readonly Color AvailableColor = new Color(0.42f, 0.28f, 0.18f);
        static readonly Color UnlockedColor = new Color(0.2f, 0.4f, 0.24f);

        public bool IsOpen => root != null && root.activeSelf;

        public static SkillTreeView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("SkillTree", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.78f));

            var view = rootRt.gameObject.AddComponent<SkillTreeView>();
            view.root = rootRt.gameObject;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -90f),
                new Vector2(1400f, 80f));
            UiFactory.AddText(title, "원두 획득!  해금할 스킬을 선택하세요", 52, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        void BuildSlots()
        {
            var ordered = new List<SkillData>(tree.Nodes);
            ordered.Sort((a, b) => a.branch != b.branch
                ? a.branch.CompareTo(b.branch)
                : a.tier.CompareTo(b.tier));

            // 갈래별 헤더
            foreach (SkillBranch branch in Enum.GetValues(typeof(SkillBranch)))
            {
                var header = UiFactory.Place($"Header_{branch}", root.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(ColumnX(branch), 250f), new Vector2(400f, 50f));
                UiFactory.AddText(header, SkillData.BranchLabel(branch), 40, TextAnchor.MiddleCenter,
                    new Color(0.9f, 0.7f, 0.5f));
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                var data = ordered[i];
                float y = 90f - (data.tier - 1) * 260f;
                var rt = UiFactory.Place($"Node_{data.id}", root.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(ColumnX(data.branch), y), new Vector2(400f, 220f));

                var slot = new NodeSlot { data = data, background = UiFactory.AddImage(rt, LockedColor) };
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = slot.background;
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                var captured = data;
                button.onClick.AddListener(() => Choose(captured));

                UiFactory.AddText(UiFactory.Place("Key", rt, new Vector2(0f, 1f), new Vector2(14f, -8f),
                    new Vector2(70f, 36f)), $"[{i + 1}]", 26, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.5f));
                UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                    new Vector2(370f, 50f)), data.displayName, 38, TextAnchor.MiddleCenter, Color.white);
                UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f),
                    new Vector2(370f, 80f)), data.description, 30, TextAnchor.MiddleCenter,
                    new Color(0.92f, 0.92f, 0.92f));
                slot.status = UiFactory.AddText(UiFactory.Place("Status", rt, new Vector2(0.5f, 0f),
                    new Vector2(0f, 28f), new Vector2(370f, 40f)), "", 28, TextAnchor.MiddleCenter, Color.white);

                slots.Add(slot);
            }
        }

        static float ColumnX(SkillBranch branch) => ((int)branch - 1) * 460f;

        public void Show(SkillTree skillTree, Action<SkillData> callback)
        {
            tree = skillTree;
            onChosen = callback;
            if (slots.Count == 0) BuildSlots();

            foreach (var slot in slots)
            {
                if (tree.IsUnlocked(slot.data))
                {
                    slot.background.color = UnlockedColor;
                    slot.status.text = "해금됨";
                    slot.status.color = new Color(0.6f, 1f, 0.7f);
                }
                else if (tree.IsAvailable(slot.data))
                {
                    slot.background.color = AvailableColor;
                    slot.status.text = "선택 가능";
                    slot.status.color = new Color(1f, 0.85f, 0.5f);
                }
                else
                {
                    slot.background.color = LockedColor;
                    slot.status.text = "선행 스킬 필요";
                    slot.status.color = new Color(1f, 1f, 1f, 0.4f);
                }
            }

            root.SetActive(true);
        }

        void Update()
        {
            if (!IsOpen) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (int i = 0; i < keys.Length && i < slots.Count; i++)
            {
                if (kb[keys[i]].wasPressedThisFrame)
                {
                    Choose(slots[i].data);
                    return;
                }
            }
        }

        /// <summary>해금 가능한 노드만 받아들인다. 잠긴/이미 해금된 노드는 무시.</summary>
        public void Choose(SkillData node)
        {
            if (!IsOpen || onChosen == null || !tree.IsAvailable(node)) return;

            var callback = onChosen;
            onChosen = null;
            root.SetActive(false);
            callback(node);
        }
    }
}
