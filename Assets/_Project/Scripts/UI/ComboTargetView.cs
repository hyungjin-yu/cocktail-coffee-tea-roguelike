using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 57(2026-10-15) P1 "목표 조합 지정": 이번 런 직업에서 노릴 수 있는 히든 스킬을 골라 목표로 정한다. 행마다 단계·실제 조건 진행도
    /// (HiddenRequirements, 실제 판정과 같은 표)를 보인다. 미발견 히든은 이름만 ???이고 필요한 조건·현재 수는 모두 공개한다(57 결정).
    /// 무기·직업 때문에 노릴 수 없는 히든은 사유와 함께 고를 수 없게 표시한다. 카드 창은 목표에 필요한 카드를 따로 표시한다.
    /// HUD [T]/버튼(Playing, 시간 정지)과 카드 창의 "목표 바꾸기"(이미 정지) 양쪽에서 같은 뷰를 연다.
    /// </summary>
    public class ComboTargetView : MonoBehaviour
    {
        GameManager game;
        GameObject root;
        RectTransform content;
        Text header;
        Button clearButton, closeButton;
        Action onClosed;
        readonly List<(HiddenSkill skill, Button button)> rows = new List<(HiddenSkill, Button)>();

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>테스트용 — 지금 목록에 있는 히든과 고를 수 있는지.</summary>
        public IReadOnlyList<(HiddenSkill skill, Button button)> Rows => rows;

        public static ComboTargetView Create(Transform canvas, GameManager game)
        {
            var rootRt = UiFactory.Stretch("ComboTarget", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.78f));
            CenterBand.Attach(rootRt);
            var view = rootRt.gameObject.AddComponent<ComboTargetView>();
            view.game = game;
            view.root = rootRt.gameObject;

            var panel = UiFactory.Place("Panel", rootRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 900f));
            UiFactory.SkinPanel(UiFactory.AddImage(panel, new Color(0.12f, 0.09f, 0.12f, 1f)));
            UiFactory.AddText(UiFactory.Place("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1100f, 56f)),
                "목표 조합 정하기", 40, TextAnchor.MiddleCenter, Color.white);
            view.header = UiFactory.AddText(UiFactory.Place("Header", panel, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1100f, 64f)),
                "", 24, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.85f));

            var scrollRt = UiFactory.Place("Scroll", panel, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1120f, 630f));
            UiFactory.AddImage(scrollRt, Color.clear).raycastTarget = true;
            scrollRt.gameObject.AddComponent<RectMask2D>();
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var c = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            c.SetParent(scrollRt, false);
            c.anchorMin = new Vector2(0f, 1f);
            c.anchorMax = new Vector2(1f, 1f);
            c.pivot = new Vector2(0.5f, 1f);
            view.content = c;
            scroll.viewport = scrollRt;
            scroll.content = c;

            view.clearButton = view.FooterButton(panel, "Clear", new Vector2(-200f, 28f), "목표 해제", () => view.Pick(null));
            view.closeButton = view.FooterButton(panel, "Close", new Vector2(200f, 28f),
                MobilePlatform.IsTouchUI ? "닫기" : "닫기 · Esc", view.Close);
            view.root.SetActive(false);
            return view;
        }

        Button FooterButton(RectTransform panel, string name, Vector2 pos, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UiFactory.Place(name, panel, new Vector2(0.5f, 0f), pos, new Vector2(340f, 64f));
            var bg = UiFactory.AddImage(rt, new Color(0.35f, 0.22f, 0.18f));
            UiFactory.SkinButton(bg);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            UiFactory.ApplyHoverColors(b);
            UiFactory.AddFocusSfx(b);
            b.onClick.AddListener(onClick);
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 28, TextAnchor.MiddleCenter, Color.white);
            return b;
        }

        public void Show(Action onClosed)
        {
            this.onClosed = onClosed;
            Rebuild();
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            var first = rows.Find(r => r.button.interactable).button;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : closeButton.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            AudioService.PlaySfx("ui_cancel");
            root.SetActive(false);
            var cb = onClosed;
            onClosed = null;
            cb?.Invoke();
        }

        /// <summary>목표를 정하고(또는 null로 해제) 닫는다. 이미 해금했거나 노릴 수 없는 히든은 무시.</summary>
        public void Pick(HiddenSkill? skill)
        {
            if (skill.HasValue && !game.CanTarget(skill.Value)) return;
            AudioService.PlaySfx("ui_confirm");
            game.SetComboTarget(skill);
            root.SetActive(false);
            var cb = onClosed;
            onClosed = null;
            cb?.Invoke();
        }

        void Rebuild()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            rows.Clear();
            var job = game.Job;
            var state = game.HiddenState;
            var target = game.ComboTarget;
            header.text = JobStudyTheme.Text("히든 스킬은 카드·커피 조합 조건을 채우면 자동 해금됩니다. 목표를 정하면 카드 창에서\n" +
                "그 조건을 올리는 카드가 표시됩니다. 추천과 다른 카드를 골라도 됩니다.", job);
            int row = 0;
            foreach (var def in HiddenSkillDatabase.All)
            {
                if (!def.AvailableFor(job)) continue;
                bool owned = game.Player != null && game.Player.Hidden.Has(def.skill);
                bool discovered = game.HiddenDiscovered(def.id);
                var reach = game.Reachability(def);
                var progress = HiddenRequirements.Progress(def.skill, job, state);
                int met = HiddenRequirements.MetCount(progress);

                var rt = UiFactory.Place(def.id, content, new Vector2(0.5f, 1f), new Vector2(0f, -row * 98f), new Vector2(1090f, 90f));
                bool isTarget = target.HasValue && target.Value == def.skill;
                var bg = UiFactory.AddImage(rt, isTarget ? new Color(0.36f, 0.27f, 0.12f, 1f) : new Color(0.19f, 0.14f, 0.17f, 1f));
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                UiFactory.ApplyHoverColors(b);
                UiFactory.AddFocusSfx(b);
                var skill = def.skill;
                b.onClick.AddListener(() => Pick(skill));
                b.interactable = !owned && reach.reachable;

                string name = HiddenRequirements.DisplayName(def, job, discovered);
                string status = owned ? "해금됨" : !reach.reachable ? "이번 런 불가 · " + reach.reason
                    : isTarget ? "◎ 현재 목표" : $"조건 {met}/{progress.Count}";
                var titleColor = owned ? new Color(0.6f, 0.85f, 0.6f) : !reach.reachable ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.85f, 0.4f);
                UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0f, 1f), new Vector2(18f, -6f), new Vector2(1050f, 36f)),
                    $"{name} · {(def.tier == HiddenTier.Hard ? HiddenSkillDatabase.TierLabel(def.tier) : $"{progress.Count}요소")}   {status}", 26, TextAnchor.MiddleLeft, titleColor);
                UiFactory.AddText(UiFactory.Place("Progress", rt, new Vector2(0f, 1f), new Vector2(18f, -46f), new Vector2(1050f, 36f)),
                    HiddenRequirements.ProgressLine(def, job, state),
                    22, TextAnchor.MiddleLeft, reach.reachable ? Color.white : new Color(0.7f, 0.7f, 0.7f));
                rows.Add((def.skill, b));
                row++;
            }
            content.sizeDelta = new Vector2(0f, Mathf.Max(100f, row * 98f));
            content.anchoredPosition = Vector2.zero;
            clearButton.interactable = target.HasValue;
        }
    }
}
