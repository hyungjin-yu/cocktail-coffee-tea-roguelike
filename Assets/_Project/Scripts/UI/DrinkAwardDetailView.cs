using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// hudfix 2026-10-19 결과 화면 "획득 내역": 이번 판 음료 재료(같은 재료끼리 합친 전체 합계 — 빠지는 재료 없음)·연구 포인트·준비 음료를
    /// 스크롤 창으로 보여 준다. 표시 전용 — 정산 기록·창고·RP는 바꾸지 않는다. 67 "이번 판 빌드"(RunDetailView)와 같은 불투명 모달 형식.
    /// </summary>
    public class DrinkAwardDetailView : MonoBehaviour
    {
        public const float PanelWidth = 1300f;
        public const float PanelHeight = 860f;
        public const int BodyFont = 28; // 720에서 18.7px(27 이상)
        const int SectionFont = 32;
        const float HeaderHeight = 150f;
        const float CloseReserved = 120f;
        const float ScrollWidth = 1220f;
        /// <summary>좁은 창(폭 맞춤으로 캔버스가 0.435배까지 줄어듦)에서도 본문 글자가 이 화면 픽셀이 되도록 창만 키운다(창 여백 24px 안).</summary>
        public const float TargetBodyPx = 18f;
        const float ScreenMargin = 24f;

        static readonly Color Gold = new Color(1f, 0.84f, 0.45f);
        static readonly Color Muted = new Color(0.80f, 0.74f, 0.68f);
        static readonly Color Cream = new Color(0.95f, 0.92f, 0.86f);

        Text titleText, statusText;
        RectTransform panel, area, content;
        float scrollWidth = ScrollWidth;
        ScrollRect scroll;
        Button closeButton;
        GameObject returnFocus;
        Action onClosed;

        public bool IsOpen => gameObject.activeSelf;
        public Button CloseButton => closeButton;
        public ScrollRect Scroll => scroll;
        public RectTransform Content => content;
        public RectTransform Panel => panel;
        public string AllText
        {
            get
            {
                var parts = new List<string>();
                foreach (var t in GetComponentsInChildren<Text>(false)) if (!string.IsNullOrEmpty(t.text)) parts.Add(t.text);
                return string.Join("\n", parts);
            }
        }

        public static DrinkAwardDetailView Create(Transform parent, Action onClosed)
        {
            var root = UiFactory.Stretch("DrinkAwardDetail", parent);
            UiFactory.AddImage(root, new Color(0.02f, 0.015f, 0.03f, 0.985f)); // 뒤 버튼 클릭 차단
            var v = root.gameObject.AddComponent<DrinkAwardDetailView>();
            v.onClosed = onClosed;

            var panel = UiFactory.Place("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            v.panel = panel;
            UiFactory.SkinPanel(UiFactory.AddImage(panel, new Color(0.1f, 0.08f, 0.12f, 1f)));
            v.titleText = UiFactory.AddText(UiFactory.Place("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(ScrollWidth, 64f)),
                "이번 판 음료 획득 내역", 44, TextAnchor.MiddleCenter, Gold);
            v.statusText = UiFactory.AddText(UiFactory.Place("Status", panel, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(ScrollWidth, 48f)),
                "", BodyFont, TextAnchor.MiddleCenter, Cream);
            v.statusText.verticalOverflow = VerticalWrapMode.Truncate;

            var area = v.area = UiFactory.Place("Scroll", panel, new Vector2(0.5f, 1f), new Vector2(0f, -HeaderHeight),
                new Vector2(ScrollWidth, PanelHeight - HeaderHeight - CloseReserved));
            UiFactory.AddImage(area, new Color(0f, 0f, 0f, 0.18f));
            var viewport = UiFactory.Stretch("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            v.content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            v.content.SetParent(viewport, false);
            v.content.anchorMin = new Vector2(0f, 1f);
            v.content.anchorMax = new Vector2(1f, 1f);
            v.content.pivot = new Vector2(0.5f, 1f);
            v.content.anchoredPosition = Vector2.zero;
            v.content.sizeDelta = new Vector2(0f, 100f);
            v.scroll = area.gameObject.AddComponent<ScrollRect>();
            v.scroll.content = v.content;
            v.scroll.viewport = viewport;
            v.scroll.horizontal = false;
            v.scroll.movementType = ScrollRect.MovementType.Clamped;
            v.scroll.scrollSensitivity = 45f;

            var closeRt = UiFactory.Place("Close", panel, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(360f, 80f));
            var closeBg = UiFactory.AddImage(closeRt, new Color(0.3f, 0.32f, 0.36f));
            v.closeButton = closeRt.gameObject.AddComponent<Button>();
            v.closeButton.targetGraphic = closeBg;
            UiFactory.ApplyHoverColors(v.closeButton);
            UiFactory.AddFocusSfx(v.closeButton);
            v.closeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            v.closeButton.onClick.AddListener(v.Close);
            UiFactory.AddText(UiFactory.Stretch("Label", closeRt), MobilePlatform.IsTouchUI ? "닫기" : "닫기 · Esc", 32, TextAnchor.MiddleCenter, Color.white);

            root.gameObject.SetActive(false);
            return v;
        }

        public void Show(DrinkSettlement s, GameObject focusAfterClose = null)
        {
            if (s == null) return;
            returnFocus = focusAfterClose != null ? focusAfterClose : EventSystem.current?.currentSelectedGameObject;
            statusText.text = ResultView.DrinkStatusHead(s);
            gameObject.SetActive(true);
            Fit();
            BuildBody(s);
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            if (returnFocus != null && returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnFocus);
            onClosed?.Invoke();
        }

        /// <summary>실제 캔버스 배율로 창 배율·폭을 정한다: 본문이 TargetBodyPx가 될 때까지 키우되 창 높이 안, 폭은 화면 안에 들어가게 줄인다.</summary>
        void Fit()
        {
            var canvas = GetComponentInParent<Canvas>();
            float cs = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            if (cs <= 0f) cs = 1f;
            float availW = Mathf.Max(100f, Screen.width - ScreenMargin * 2f), availH = Mathf.Max(100f, Screen.height - ScreenMargin * 2f);
            float k = Mathf.Max(1f, TargetBodyPx / (BodyFont * cs));
            k = Mathf.Max(1f, Mathf.Min(k, availH / (PanelHeight * cs)));
            float w = Mathf.Min(PanelWidth, availW / (cs * k));
            panel.localScale = new Vector3(k, k, 1f);
            panel.sizeDelta = new Vector2(w, PanelHeight);
            scrollWidth = w - (PanelWidth - ScrollWidth);
            foreach (var t in new[] { titleText.rectTransform, statusText.rectTransform, area })
                t.sizeDelta = new Vector2(scrollWidth, t.sizeDelta.y);
        }

        void BuildBody(DrinkSettlement s)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            float y = -12f;
            bool abandon = s.kind == "PreservedAbandon";
            var items = abandon ? s.discarded : s.granted;
            var totals = DrinkLab.Totals(items);
            y = Section(abandon ? $"정산하지 않은 음료 재료(보존 포기)  ·  {totals.Count}종" : $"음료 재료  ·  {totals.Count}종", y);
            if (totals.Count == 0) y = Row("이번 판 재료 보상 없음", "", y, Muted);
            else
            {
                // items는 보상별로 같은 재료 추첨을 이미 합친 정산 기록 줄(RollAward) — 실제 추첨 묶음 수가 아니다.
                y = Row($"획득 기록 {items.Count}건을 재료별로 합친 양", "", y, Muted);
                foreach (var def in DrinkIngredients.All)
                    if (totals.TryGetValue(def.id, out var v))
                        y = Row(def.name, DrinkRules.Format((int)Math.Min(v, int.MaxValue), def.form), y, Cream, "Material_" + def.id);
            }
            y -= 14f;
            y = Section("연구 포인트", y);
            y = s.researchPoints > 0
                ? Row($"+{s.researchPoints}", $"현재 {DrinkLab.Research.researchPoints} · 연구 노트북에서 사용", y, Cream)
                : Row(abandon ? "보존 포기 — 연구 포인트 없음" : "이번 판 연구 포인트 없음", "", y, Muted);
            y -= 14f;
            y = Section("준비 음료", y);
            string drink = s.drink == "Preserved" ? "5마리 처치 전 포기라 보존했습니다(다음 출발 때 그대로 마심)"
                : s.drink == "Consumed" ? "이번 판에 사용되어 사라졌습니다" : "이번 판에 준비 음료 없음";
            y = Row(drink, "", y, s.drink == "None" ? Muted : Cream);
            content.sizeDelta = new Vector2(content.sizeDelta.x, -y + 16f);
        }

        float Section(string label, float y)
        {
            float h = UiFactory.TruncatedBoxHeight(SectionFont);
            var t = UiFactory.AddText(UiFactory.Place("Section", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(scrollWidth - 40f, h)),
                label, SectionFont, TextAnchor.MiddleLeft, Gold);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return y - h - 6f;
        }

        /// <summary>왼쪽 이름 · 오른쪽 값 한 줄(넘치면 줄바꿈해 높이를 늘린다 — 글자를 줄이거나 자르지 않는다).</summary>
        float Row(string left, string right, float y, Color color, string name = "Line")
        {
            float rowW = scrollWidth - 60f, nameW = right.Length > 0 ? Mathf.Min(560f, rowW * 0.45f) : rowW;
            var rt = UiFactory.Place(name, content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(rowW, 40f));
            var l = UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0f, 1f), Vector2.zero, new Vector2(nameW, 40f)),
                left, BodyFont, TextAnchor.UpperLeft, color);
            float h = Mathf.Max(UiFactory.TruncatedBoxHeight(BodyFont), l.preferredHeight + 6f);
            if (right.Length > 0)
            {
                var r = UiFactory.AddText(UiFactory.Place("Value", rt, new Vector2(1f, 1f), Vector2.zero, new Vector2(rowW - nameW - 20f, 40f)),
                    right, BodyFont, TextAnchor.UpperRight, color);
                h = Mathf.Max(h, r.preferredHeight + 6f);
                r.rectTransform.sizeDelta = new Vector2(r.rectTransform.sizeDelta.x, h);
            }
            l.rectTransform.sizeDelta = new Vector2(l.rectTransform.sizeDelta.x, h);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            return y - h - 4f;
        }
    }
}
