using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>커피 트리(08:88~91). 새 포인트가 생기면 한 번 자동으로 열리고, K로 언제든(포인트 0이어도)
    /// 연다. 열려 있는 동안 노드를 눌러 1포인트씩 여러 개를 배분하고, "계속하기"로 닫으면 남은 포인트는
    /// 보존된다. 포인트를 다 쓰거나 더 찍을 노드가 없어지면 저절로 닫힌다.
    /// 2026-09-28: Pause의 "커피 트리" 버튼에서 조회 전용(ShowReadOnly)으로도 연다 — 같은 상단 잔여
    /// 포인트·노드 상태를 보여주되 포인트를 쓰지 않는다.</summary>
    public class SkillTreeView : MonoBehaviour
    {
        class NodeSlot
        {
            public SkillData data;
            public Image background;
            public Button button;
            public Text key;
            public Text name;
            public Text desc;
            public Text status;
        }

        readonly List<NodeSlot> slots = new List<NodeSlot>();
        GameObject root;
        GameObject closeButtonGo;
        Text pointsText;
        Text treeTitle;
        Job CurrentJob => GetComponentInParent<GameManager>()?.Job ?? GameManager.Instance?.Job ?? Job.Bartender;
        SkillTree tree;
        Func<int> pointsSource;
        Func<SkillData, bool> trySpend;
        Action onClosed;
        bool readOnly;
        /// <summary>08:39 — 직전 보상 화면을 닫은 키/클릭/Submit(같은 호출에서 연쇄로 열림, 07:46)이
        /// 포커스된 노드를 바로 해금하지 않도록 연 프레임과 눌림 해제 전까지 사용자 입력 경로를 잠근다.
        /// 테스트용 공개 Choose/Continue는 이 게이트를 거치지 않는다.</summary>
        readonly InputReleaseGate inputGate = new InputReleaseGate();

        // 14 "카페 메뉴판" 색. 밝은 바탕(가능/포인트부족)엔 진한 커피 글자, 어두운 바탕(해금/선행필요)엔 크림 글자.
        static readonly Color Cream = Hex(0xF8F1DD);
        static readonly Color Kraft = Hex(0xEAD4A5);
        static readonly Color Wood = Hex(0x714732);
        static readonly Color DarkCoffee = Hex(0x4A3A38);
        static readonly Color Bean = Hex(0x5C3928);
        static readonly Color CoffeeAccent = Hex(0xB98247);
        static readonly Color TeaGreen = Hex(0x6E8F4E);
        static readonly Color Danger = Hex(0xD9695A);

        static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        static readonly Key[] HotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>Pause 위 조회 전용으로 열려 있는지(PauseView/Esc 처리용).</summary>
        public bool IsReadOnlyOpen => IsOpen && readOnly;
        /// <summary>테스트용 — 상단 잔여 포인트 문구.</summary>
        public string PointsLabel => pointsText != null ? pointsText.text : "";
        /// <summary>테스트용 — 08:39 입력 잠금 상태.</summary>
        public bool InputLocked => inputGate.Locked;

        public static SkillTreeView Create(Transform canvas)
        {
            var rootRt = UiFactory.Stretch("SkillTree", canvas);
            UiFactory.AddImage(rootRt, new Color(0.19f, 0.13f, 0.11f, 0.9f));
            UiFactory.AddCafeBackdrop(rootRt);
            CenterBand.Attach(rootRt); // 2026-10-12 세로가 긴 화면: 3열 트리 세로 가운데

            var view = rootRt.gameObject.AddComponent<SkillTreeView>();
            view.root = rootRt.gameObject;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -48f),
                new Vector2(1400f, 50f));
            view.treeTitle = UiFactory.AddText(title, "커피 연구", 48, TextAnchor.MiddleCenter, Cream);

            var pointsRt = UiFactory.Place("Points", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -104f),
                new Vector2(1740f, 36f));
            view.pointsText = UiFactory.AddText(pointsRt, "", 24, TextAnchor.MiddleCenter, CoffeeAccent);

            var closeRt = UiFactory.Place("CloseButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 68f),
                new Vector2(320f, 64f));
            view.closeButtonGo = closeRt.gameObject;
            var closeBg = UiFactory.AddImage(closeRt, Wood);
            closeBg.sprite = SpriteFactory.Square; // 공용 ui_button 자동 입힘 제외(14 메뉴판 팔레트)
            UiFactory.SkinButton(closeBg);
            var closeButton = closeRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(closeButton);
            closeButton.targetGraphic = closeBg;
            UiFactory.ApplyHoverColors(closeButton);
            closeButton.onClick.AddListener(() =>
            {
                if (!view.inputGate.Allows(Time.frameCount, InputReleaseGate.AnyConfirmHeld())) return;
                AudioService.PlaySfx("ui_cancel");
                view.Continue();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", closeRt), UiFactory.InputLabel("게임 재개", "Esc"), 28, TextAnchor.MiddleCenter, Cream);

            view.root.SetActive(false);
            return view;
        }

        /// <summary>07:122 — 3갈래 × 5단계(15노드). 1080 기준 세로 배치(중심 좌표):
        /// 제목 442..492 · 잔여포인트 404..436 · 갈래 헤더 362..398 · 노드 1단 355..215 … 5단 −245..−385
        /// · 계속하기 −408..−472. 서로 겹치지 않고 08:47 안전 여백48(±492) 안에 든다(이전 배치는 5단 노드와
        /// 계속하기가 겹쳤음).
        /// 본문은 08:47 최소 24px(1080 기준).</summary>
        const float TierSpacing = 150f;
        const float Tier1Y = 285f;
        const float HeaderY = 380f;
        static readonly Vector2 NodeSize = new Vector2(580f, 140f);

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
                    new Vector2(ColumnX(branch), HeaderY), new Vector2(420f, 36f));
                UiFactory.AddText(header, SkillData.BranchLabel(branch), 32, TextAnchor.MiddleCenter, Kraft);
                UiFactory.AddIcon(header, BranchIconId(branch), new Vector2(0f, 0.5f), new Vector2(60f, 0f), 36f); // 09 skill_*
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                var data = ordered[i];
                float y = Tier1Y - (data.tier - 1) * TierSpacing;
                var rt = UiFactory.Place($"Node_{data.id}", root.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(ColumnX(data.branch), y), NodeSize);

                var slot = new NodeSlot { data = data, background = UiFactory.AddImage(rt, Wood) };
                slot.background.sprite = SpriteFactory.Square; // 공용 ui_button 자동 입힘 제외(크림 노드 위 커피색 글자 대비 유지)
                UiFactory.SkinCard(slot.background);
                slot.button = rt.gameObject.AddComponent<Button>();
                UiFactory.AddFocusSfx(slot.button);
                slot.button.targetGraphic = slot.background;
                UiFactory.ApplyHoverColors(slot.button);
                // QA 2026-10-05: 기본 disabled 틴트(회색·알파 0.5)가 포인트 부족/선행 필요 노드를 흐리게 비쳐 대비가 무너졌다.
                // 상태는 바탕색+사유 문구(08:50)로 이미 구분하므로 비활성 노드도 불투명 원색 그대로 둔다.
                var nodeColors = slot.button.colors;
                nodeColors.disabledColor = Color.white;
                slot.button.colors = nodeColors;
                var captured = data;
                slot.button.onClick.AddListener(() => ChooseFromInput(captured));

                // 단축키(1~6)는 화면에 15개가 다 안 들어가서 그 앞쪽 슬롯만 실제로 동작한다(Update 참고)
                // — 안 맞는 번호를 표시해 헷갈리게 하지 않도록, 해당하는 슬롯에만 라벨을 단다.
                if (i < HotKeys.Length)
                {
                    slot.key = UiFactory.AddText(UiFactory.Place("Key", rt, new Vector2(0f, 1f),
                        new Vector2(16f, -12f), new Vector2(48f, 36f)), MobilePlatform.IsTouchUI ? "" : $"[{i + 1}]", 24, TextAnchor.MiddleLeft, Cream);
                }
                // 이름 위 / 효과 가운데(최대 2줄) / 상태·비용 아래. 단축키 표시가 왼쪽 위 모서리라 이름 칸을
                // 좌우로 줄여 겹치지 않게 한다.
                slot.name = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -12f),
                    new Vector2(400f, 36f)), JobStudyTheme.Name(data,CurrentJob), 24, TextAnchor.MiddleCenter, Cream);
                slot.desc = UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f),
                    new Vector2(556f, 36f)), JobStudyTheme.Text(data.description,CurrentJob), 24, TextAnchor.MiddleCenter, Cream);
                slot.status = UiFactory.AddText(UiFactory.Place("Status", rt, new Vector2(0.5f, 0f),
                    new Vector2(0f, 12f), new Vector2(556f, 36f)), "", 24, TextAnchor.MiddleCenter, Cream);

                slots.Add(slot);
            }
        }

        /// <summary>09 skill_attack/skill_survival/skill_ingredient — 커피·영구 강화 갈래 공용 아이콘.</summary>
        public static string BranchIconId(SkillBranch branch) =>
            branch == SkillBranch.Attack ? "skill_attack" : branch == SkillBranch.Survival ? "skill_survival" : "skill_ingredient";

        static float ColumnX(SkillBranch branch) => ((int)branch - 1) * 600f;

        /// <summary>배분 모드로 연다. points는 현재 미사용 포인트, spend는 노드 1개 해금 시도(성공 시 true),
        /// closed는 이 화면이 닫힌 뒤(계속하기/포인트 소진/찍을 노드 없음) 한 번 호출된다.</summary>
        public void Show(SkillTree skillTree, Func<int> points, Func<SkillData, bool> spend, Action closed)
        {
            Open(skillTree, points, spend, closed, false);
        }

        /// <summary>08:91 "포인트가0이어도 조회 가능" — Pause의 "커피 트리" 버튼에서 쓴다. 잔여 포인트와
        /// 노드 상태는 배분 모드와 똑같이 보여주지만 노드를 눌러도 포인트를 쓰지 않는다.</summary>
        public void ShowReadOnly(SkillTree skillTree, Func<int> points, Action closed)
        {
            Open(skillTree, points, null, closed, true);
            root.transform.SetAsLastSibling(); // Pause 위에서 열릴 수 있어 항상 맨 위로
        }

        void Open(SkillTree skillTree, Func<int> points, Func<SkillData, bool> spend, Action closed, bool isReadOnly)
        {
            tree = skillTree;
            pointsSource = points;
            trySpend = spend;
            onClosed = closed;
            readOnly = isReadOnly;
            inputGate.Arm(Time.frameCount);
            if (slots.Count == 0) BuildSlots();

            Refresh();
            root.SetActive(true);

            if (EventSystem.current != null)
            {
                var first = readOnly ? null : slots.Find(s => s.button.interactable);
                EventSystem.current.SetSelectedGameObject(first != null
                    ? first.background.gameObject
                    : closeButtonGo);
            }
        }

        int Points => pointsSource != null ? pointsSource() : 0;

        void Refresh()
        {
            int points = Points;
            bool allUnlocked = slots.TrueForAll(s => tree.IsUnlocked(s.data));
            pointsText.text = $"남은 커피 포인트 {points} · " + (allUnlocked ? "연구 모두 해금" : "카드를 선택해 연구를 해금하세요");

            treeTitle.text=JobStudyTheme.TreeTitle(CurrentJob);
            pointsText.text=JobStudyTheme.Text(pointsText.text,CurrentJob);
            foreach (var slot in slots)
            {
                var state = tree.StateOf(slot.data, points);
                Color fill, text, statusColor;
                switch (state)
                {
                    case CoffeeNodeState.Unlocked:
                        fill = TeaGreen; text = Cream; statusColor = Cream;
                        slot.status.text = "해금됨";
                        break;
                    case CoffeeNodeState.Available:
                        fill = Cream; text = DarkCoffee; statusColor = Bean;
                        slot.status.text = $"해금 가능 · 비용 {SkillTree.NodeCost}";
                        break;
                    case CoffeeNodeState.NotEnoughPoints:
                        fill = Kraft; text = DarkCoffee; statusColor = Danger;
                        slot.status.text = $"포인트 부족 · 비용 {SkillTree.NodeCost}";
                        break;
                    default:
                        fill = Wood; text = Cream; statusColor = Kraft;
                        slot.status.text = "선행 스킬 필요";
                        break;
                }
                UiFactory.SetChoice(slot.background, state == CoffeeNodeState.Unlocked, fill);
                if (UiFactory.IsCafe(slot.background, "ui_cafe_card"))
                {
                    text = Cream;
                    statusColor = state == CoffeeNodeState.NotEnoughPoints ? Danger : Kraft;
                }
                slot.name.color = text;
                slot.desc.color = text;
                slot.status.color = statusColor;
                if (slot.key != null) slot.key.color = text;
                slot.button.interactable = !readOnly && state == CoffeeNodeState.Available;
            }
        }

        void Update()
        {
            // 08:23 "키 관통 없음" — Pause가 이 화면 위에 겹쳐 열려 있으면(오버레이 스택) 밑에 깔린
            // 이 화면의 단축키(1~6)가 새어 들어오면 안 된다.
            if (!IsOpen || readOnly || (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;

            // 매 프레임 눌림 상태를 봐야 "뗐다가 다시 누름"을 구분한다(클릭/Submit 경로도 이 판정을 공유).
            inputGate.Poll(Time.frameCount, InputReleaseGate.AnyConfirmHeld());
            var kb = Keyboard.current;
            if (kb == null || inputGate.Locked) return;
            for (int i = 0; i < HotKeys.Length && i < slots.Count; i++)
            {
                if (kb[HotKeys[i]].wasPressedThisFrame)
                {
                    Choose(slots[i].data);
                    return;
                }
            }
        }

        /// <summary>버튼 클릭/Submit 경로 — 08:39 입력 잠금을 거친다.</summary>
        void ChooseFromInput(SkillData node)
        {
            if (!inputGate.Allows(Time.frameCount, InputReleaseGate.AnyConfirmHeld())) return;
            Choose(node);
        }

        /// <summary>노드 1개에 1포인트를 쓴다(테스트용 직접 API — 입력 잠금은 ChooseFromInput에서).
        /// 해금 불가(잠김·이미 해금·포인트 없음)거나 조회 전용이거나 Pause가 위에 열려 있으면 무시한다
        /// (08:23 "키 관통 없음"). 배분 후 포인트가 0이 되거나 더 찍을 노드가 없으면 닫는다(남은 포인트는
        /// 그대로 보존).</summary>
        public void Choose(SkillData node)
        {
            if (!IsOpen || readOnly || trySpend == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            if (tree.StateOf(node, Points) != CoffeeNodeState.Available) return;
            if (!trySpend(node)) return;

            if (Points <= 0 || !tree.AnyAvailable())
            {
                Close();
                return;
            }
            Refresh();
            var selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>()
                : null;
            if (EventSystem.current != null && (selected == null || !selected.IsInteractable()))
            {
                var next = slots.Find(s => s.button.interactable);
                EventSystem.current.SetSelectedGameObject(next != null ? next.background.gameObject : closeButtonGo);
            }
        }

        /// <summary>"계속하기" — 남은 포인트를 보존한 채 닫는다(08:91). 테스트 공개 진입점이기도 하다.</summary>
        public void Continue()
        {
            if (!IsOpen) return;
            if (!readOnly && GameManager.Instance != null && GameManager.Instance.IsPaused) return;
            Close();
        }

        /// <summary>Pause 위 조회 전용 창만 닫는다(PauseView의 Esc 처리용).</summary>
        public void CloseReadOnly()
        {
            if (!IsReadOnlyOpen) return;
            Close();
        }

        void Close()
        {
            root.SetActive(false);
            trySpend = null;
            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }
    }
}
