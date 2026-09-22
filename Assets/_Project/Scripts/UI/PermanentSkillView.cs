using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 스페셜티(영구 재화)를 직접 찍는 영구 강화 화면. 런과 런 사이(직업 선택 화면에서 여는 버튼)에만
    /// 열 수 있고, 여기서 해금한 노드는 세이브 파일에 영구히 남는다 (PermanentProgress).
    /// </summary>
    public class PermanentSkillView : MonoBehaviour
    {
        class NodeSlot
        {
            public PermanentSkillData data;
            public Image background;
            public Text status;
        }

        readonly List<NodeSlot> slots = new List<NodeSlot>();
        GameObject root;
        Text pointsLabel;
        PermanentSkillTree tree;
        List<PermanentSkillData> nodes;
        Action onClose;

        static readonly Color LockedColor = new Color(0.14f, 0.12f, 0.12f);
        static readonly Color AffordableColor = new Color(0.42f, 0.28f, 0.18f);
        static readonly Color UnaffordableColor = new Color(0.22f, 0.18f, 0.15f);
        static readonly Color UnlockedColor = new Color(0.2f, 0.4f, 0.24f);

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트용 — Show() 이후에만 채워진다.</summary>
        public IReadOnlyList<PermanentSkillData> Nodes => nodes;

        public static PermanentSkillView Create(Transform canvas, Action onClose)
        {
            var rootRt = UiFactory.Stretch("PermanentSkillShop", canvas);
            UiFactory.AddImage(rootRt, new Color(0.05f, 0.04f, 0.07f, 0.97f));

            var view = rootRt.gameObject.AddComponent<PermanentSkillView>();
            view.root = rootRt.gameObject;
            view.onClose = onClose;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                new Vector2(1400f, 70f));
            UiFactory.AddText(title, "영구 강화", 52, TextAnchor.MiddleCenter, Color.white);

            var pointsRt = UiFactory.Place("Points", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -128f),
                new Vector2(900f, 50f));
            view.pointsLabel = UiFactory.AddText(pointsRt, "", 34, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.65f, 1f));

            var backRt = UiFactory.Place("BackButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                new Vector2(360f, 90f));
            var backBg = UiFactory.AddImage(backRt, new Color(0.5f, 0.2f, 0.55f));
            var backButton = backRt.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backBg;
            backButton.onClick.AddListener(view.Close);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 38, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        void BuildSlots()
        {
            nodes = PermanentSkillDatabase.LoadAll();
            var ordered = new List<PermanentSkillData>(nodes);
            ordered.Sort((a, b) => a.branch != b.branch
                ? a.branch.CompareTo(b.branch)
                : a.tier.CompareTo(b.tier));

            foreach (SkillBranch branch in Enum.GetValues(typeof(SkillBranch)))
            {
                var header = UiFactory.Place($"Header_{branch}", root.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(ColumnX(branch), 220f), new Vector2(400f, 50f));
                UiFactory.AddText(header, SkillData.BranchLabel(branch), 38, TextAnchor.MiddleCenter,
                    new Color(0.9f, 0.7f, 0.5f));
            }

            foreach (var data in ordered)
            {
                float y = 60f - (data.tier - 1) * 280f;
                var rt = UiFactory.Place($"Node_{data.id}", root.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(ColumnX(data.branch), y), new Vector2(400f, 250f));

                var slot = new NodeSlot { data = data, background = UiFactory.AddImage(rt, LockedColor) };
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = slot.background;
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                var captured = data;
                button.onClick.AddListener(() => TryBuy(captured));

                var icon = string.IsNullOrEmpty(data.iconResourcePath)
                    ? null : Resources.Load<Sprite>(data.iconResourcePath);
                if (icon != null)
                {
                    var iconRt = UiFactory.Place("Icon", rt, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                        new Vector2(56f, 56f));
                    var iconImg = iconRt.gameObject.AddComponent<Image>();
                    iconImg.sprite = icon;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                }

                UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -78f),
                    new Vector2(370f, 36f)), data.displayName, 32, TextAnchor.MiddleCenter, Color.white);
                UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -122f),
                    new Vector2(370f, 66f)), data.description, 26, TextAnchor.MiddleCenter,
                    new Color(0.92f, 0.92f, 0.92f));
                slot.status = UiFactory.AddText(UiFactory.Place("Status", rt, new Vector2(0.5f, 0f),
                    new Vector2(0f, 22f), new Vector2(370f, 34f)), "", 24, TextAnchor.MiddleCenter, Color.white);

                slots.Add(slot);
            }
        }

        static float ColumnX(SkillBranch branch) => ((int)branch - 1) * 460f;

        public void Show()
        {
            if (slots.Count == 0) BuildSlots();
            Refresh();
            root.SetActive(true);
        }

        void Refresh()
        {
            var save = PermanentProgress.Current;
            tree = new PermanentSkillTree(nodes, save.unlockedPermanentSkillIds);
            int available = save.specialtyPoints - tree.SpentPoints;
            pointsLabel.text = $"사용 가능한 스페셜티: {available}   (누적 획득 {save.specialtyPoints})";

            foreach (var slot in slots)
            {
                if (tree.IsUnlocked(slot.data))
                {
                    slot.background.color = UnlockedColor;
                    slot.status.text = "해금됨";
                    slot.status.color = new Color(0.6f, 1f, 0.7f);
                }
                else if (!tree.IsAvailable(slot.data))
                {
                    slot.background.color = LockedColor;
                    slot.status.text = "선행 강화 필요";
                    slot.status.color = new Color(1f, 1f, 1f, 0.4f);
                }
                else if (available >= slot.data.cost)
                {
                    slot.background.color = AffordableColor;
                    slot.status.text = $"비용 {slot.data.cost} · 클릭해서 해금";
                    slot.status.color = new Color(1f, 0.85f, 0.5f);
                }
                else
                {
                    slot.background.color = UnaffordableColor;
                    slot.status.text = $"비용 {slot.data.cost} (부족)";
                    slot.status.color = new Color(1f, 0.5f, 0.5f);
                }
            }
        }

        /// <summary>노드를 구매 시도한다. 버튼 클릭과 테스트 양쪽에서 쓴다 (CardSelectView.Choose 등과 같은 패턴).</summary>
        public void TryBuy(PermanentSkillData node)
        {
            var save = PermanentProgress.Current;
            int available = save.specialtyPoints - tree.SpentPoints;
            if (!tree.TryUnlock(node, available)) return;

            PermanentProgress.UnlockPermanentSkill(node.id);
            Refresh();
        }

        void Close()
        {
            root.SetActive(false);
            onClose?.Invoke();
        }
    }
}
