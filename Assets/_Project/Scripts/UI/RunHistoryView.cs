using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 67(2026-10-16) 한 판의 빌드 상세 — 결과 화면 "이번 판 빌드"와 타이틀 "최근 기록"이 같은 모달을 쓴다.
    /// 위: 종료 상태·시간·직업/무기·종료 사유(사망 원인). 아래 스크롤: 선택 카드와 최종 중첩, 실제로 완성한 조합, 연구 해금.
    /// 화면을 덮는 차단판이 뒤 버튼 클릭을 막고, 닫기 버튼은 탐색을 끊어 방향키가 뒤 메뉴로 빠지지 않는다.
    /// Esc/모바일 뒤로는 소유 화면(결과·최근 기록)이나 MobileBackRouter가 "Close"를 눌러 가장 위 모달부터 닫는다.
    /// </summary>
    public class RunDetailView : MonoBehaviour
    {
        public const float PanelWidth = 1600f;
        public const float PanelHeight = 940f;
        const float HeaderHeight = 300f; // 제목·직업·종료 사유(2줄)·통계 줄 아래에서 스크롤 시작
        const float CloseReserved = 120f;
        const float ScrollWidth = 1520f;
        const int BodyFont = 26;
        const int SectionFont = 30;
        const float CardColumnWidth = 744f;
        const float CardColumnStep = 760f;

        static readonly Color Gold = new Color(1f, 0.84f, 0.45f);
        static readonly Color Muted = new Color(0.80f, 0.74f, 0.68f);
        static readonly Color Cream = new Color(0.95f, 0.92f, 0.86f);

        Text titleText, identityText, endText, statsText;
        RectTransform content;
        ScrollRect scroll;
        Button closeButton;
        GameObject returnFocus;
        Action onClosed;
        RunRecord shown;

        public bool IsOpen => gameObject.activeSelf;
        public RunRecord Shown => shown;
        public Button CloseButton => closeButton;
        public ScrollRect Scroll => scroll;
        public RectTransform Content => content;
        /// <summary>테스트/QA용 — 화면에 그려진 글(머리·본문) 전부.</summary>
        public string AllText
        {
            get
            {
                var parts = new List<string>();
                foreach (var t in GetComponentsInChildren<Text>(false)) if (!string.IsNullOrEmpty(t.text)) parts.Add(t.text);
                return string.Join("\n", parts);
            }
        }

        public static RunDetailView Create(Transform parent, Action onClosed)
        {
            var root = UiFactory.Stretch("RunDetail", parent);
            // 2026-10-09 실제 720: 반투명 모달은 선형 색공간 합성에서 뒤 글자가 비쳤다 — 차단판·패널 모두 불투명에 가깝게.
            UiFactory.AddImage(root, new Color(0.02f, 0.015f, 0.03f, 0.985f)); // 뒤 버튼 클릭 차단
            var v = root.gameObject.AddComponent<RunDetailView>();
            v.onClosed = onClosed;

            var panel = UiFactory.Place("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            UiFactory.SkinPanel(UiFactory.AddImage(panel, new Color(0.1f, 0.08f, 0.12f, 1f)));

            v.titleText = UiFactory.AddText(UiFactory.Place("Title", panel, new Vector2(0.5f, 1f), new Vector2(0f, -26f),
                new Vector2(ScrollWidth, 64f)), "", 44, TextAnchor.MiddleCenter, Gold);
            v.identityText = Line(panel, "Identity", -94f, 44f, 30, Cream);
            v.endText = Line(panel, "EndDetail", -140f, UiFactory.TruncatedBoxHeight(28, 2), 28, new Color(1f, 0.72f, 0.72f));
            v.endText.verticalOverflow = VerticalWrapMode.Truncate;
            v.statsText = Line(panel, "Stats", -140f - UiFactory.TruncatedBoxHeight(28, 2), 40f, BodyFont, Muted);

            var area = UiFactory.Place("Scroll", panel, new Vector2(0.5f, 1f), new Vector2(0f, -HeaderHeight),
                new Vector2(ScrollWidth, PanelHeight - HeaderHeight - CloseReserved));
            UiFactory.AddImage(area, new Color(0f, 0f, 0f, 0.18f));
            var viewport = UiFactory.Stretch("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            var contentGo = new GameObject("Content", typeof(RectTransform));
            v.content = (RectTransform)contentGo.transform;
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
            UiFactory.AddText(UiFactory.Stretch("Label", closeRt), "닫기", 32, TextAnchor.MiddleCenter, Color.white);

            UiFactory.AddText(UiFactory.Place("ScrollHint", panel, new Vector2(1f, 0f), new Vector2(-40f, 46f), new Vector2(560f, 40f)),
                MobilePlatform.IsTouchUI ? "목록을 위아래로 밀어 보기" : "휠 · 위아래 키 · 오른쪽 스틱으로 보기",
                24, TextAnchor.MiddleRight, new Color(0.7f, 0.8f, 0.85f));

            root.gameObject.SetActive(false);
            return v;
        }

        static Job RunJob(RunRecord r) => RunRecordText.TryParseName(r.job, out Job job) ? job : Job.Bartender;

        static Text Line(RectTransform panel, string name, float y, float height, int size, Color color) =>
            UiFactory.AddText(UiFactory.Place(name, panel, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ScrollWidth, height)),
                "", size, TextAnchor.MiddleCenter, color);

        public void Show(RunRecord record, GameObject focusAfterClose = null)
        {
            if (record == null) return;
            shown = record;
            returnFocus = focusAfterClose != null ? focusAfterClose : EventSystem.current?.currentSelectedGameObject;
            titleText.text = $"{RunRecordText.EndTitle(record.EndKind)}  ·  {RunRecordText.TimeLabel(record)} {RunRecordText.Time(record.runSeconds)}";
            titleText.color = record.EndKind == RunEndKind.Cleared ? Gold : new Color(1f, 0.6f, 0.65f);
            identityText.text = $"{RunRecordText.JobName(record)}  ·  시작 무기 {RunRecordText.WeaponName(record)}  ·  처치 {record.kills}";
            endText.text = RunRecordText.EndDetail(record);
            var when = RunRecordText.EndedLocal(record);
            var job = RunJob(record);
            string levels = job == Job.TeaSommelier ? $"차 Lv {record.teaLevel}" : $"칵테일 Lv {record.cocktailLevel}  ·  차 Lv {record.teaLevel}";
            statsText.text = $"{levels}  ·  {JobStudyTheme.TreeTitle(job)} {record.researchUnlocked}  ·  스페셜티 +{record.specialtyEarned}" +
                             (when.Length > 0 ? $"  ·  {when}" : "");
            BuildBody(record);
            gameObject.SetActive(true);
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

        void BuildBody(RunRecord record)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            float y = -12f;
            int totalStacks = 0;
            foreach (var c in record.cards) totalStacks += c.stacks;
            y = Section($"선택 카드  ·  {record.cards.Count}종 · 총 중첩 {totalStacks}", y);
            if (record.cards.Count == 0) y = Note("이번 판에는 고른 카드가 없습니다.", y);
            float rowHeight = UiFactory.TruncatedBoxHeight(28) + UiFactory.TruncatedBoxHeight(24) + 12f;
            for (int i = 0; i < record.cards.Count; i++)
            {
                int col = i % 2;
                float rowY = y - (i / 2) * (rowHeight + 8f);
                CardRow(record.cards[i], new Vector2((col - 0.5f) * CardColumnStep, rowY), rowHeight);
            }
            if (record.cards.Count > 0) y -= ((record.cards.Count + 1) / 2) * (rowHeight + 8f);

            y -= 14f;
            y = Section($"완성한 조합  ·  {record.combos.Count}", y);
            if (record.combos.Count == 0) y = Note("이번 판에 완성한 히든·하이브리드·최종 형태가 없습니다.", y);
            foreach (var combo in record.combos)
                y = Note($"{RunRecordText.ComboKindLabel(combo.kind)}  ·  {RunRecordText.ComboName(combo, record)}", y, Cream);

            y -= 14f;
            y = Section($"{JobStudyTheme.TreeTitle(RunJob(record))} 해금  ·  {record.research.Count}", y);
            if (record.research.Count == 0) y = Note("해금한 연구가 없습니다.", y);
            else
            {
                var names = new List<string>();
                foreach (var r in record.research) names.Add(RunRecordText.ComboName(r, record));
                y = Paragraph(string.Join("  ·  ", names), y);
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, -y + 16f);
        }

        float Section(string label, float y)
        {
            float h = UiFactory.TruncatedBoxHeight(SectionFont);
            var t = UiFactory.AddText(UiFactory.Place("Section", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ScrollWidth - 40f, h)),
                label, SectionFont, TextAnchor.MiddleLeft, Gold);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return y - h - 6f;
        }

        float Note(string text, float y, Color? color = null)
        {
            float h = UiFactory.TruncatedBoxHeight(BodyFont);
            var t = UiFactory.AddText(UiFactory.Place("Line", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ScrollWidth - 60f, h)),
                text, BodyFont, TextAnchor.MiddleLeft, color ?? Muted);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return y - h - 4f;
        }

        float Paragraph(string text, float y)
        {
            var rt = UiFactory.Place("Paragraph", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(ScrollWidth - 60f, 40f));
            var t = UiFactory.AddText(rt, text, BodyFont, TextAnchor.UpperLeft, Cream);
            float h = Mathf.Max(UiFactory.TruncatedBoxHeight(BodyFont), t.preferredHeight + 8f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            return y - h - 4f;
        }

        void CardRow(RunCardEntry entry, Vector2 pos, float height)
        {
            var row = UiFactory.Place("Card_" + entry.id, content, new Vector2(0.5f, 1f), pos, new Vector2(CardColumnWidth, height));
            UiFactory.AddImage(row, new Color(1f, 1f, 1f, 0.05f));
            var card = RunRecordText.CurrentCard(entry.id);
            Sprite icon = null;
            if (card != null) icon = CardEducationIcons.For(card);
            else if (System.Enum.TryParse(entry.category, out CardCategory category)) icon = CardSelectView.CategoryIcon(category);
            float textLeft = 14f;
            if (icon != null)
            {
                var iconRt = UiFactory.Place("Icon", row, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(height - 12f, height - 12f));
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                textLeft = height + 4f;
            }
            float nameH = UiFactory.TruncatedBoxHeight(28);
            var name = UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 1f), new Vector2(textLeft, -4f),
                new Vector2(CardColumnWidth - textLeft - 170f, nameH)), RunRecordText.CardName(entry), 28, TextAnchor.MiddleLeft, Cream);
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            var cat = UiFactory.AddText(UiFactory.Place("Category", row, new Vector2(0f, 1f), new Vector2(textLeft, -4f - nameH),
                new Vector2(CardColumnWidth - textLeft - 170f, UiFactory.TruncatedBoxHeight(24))), RunRecordText.CategoryLabel(entry.category), 24,
                TextAnchor.MiddleLeft, Muted);
            cat.verticalOverflow = VerticalWrapMode.Truncate;
            bool max = entry.maxStacks > 0 && entry.stacks >= entry.maxStacks;
            UiFactory.AddText(UiFactory.Place("Stacks", row, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(160f, height - 8f)),
                entry.maxStacks > 0 ? $"×{entry.stacks} / {entry.maxStacks}" : $"×{entry.stacks}", 30, TextAnchor.MiddleRight,
                max ? Gold : Color.white);
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            float direction = kb != null && (kb.downArrowKey.isPressed || kb.pageDownKey.isPressed) ? -1f
                : kb != null && (kb.upArrowKey.isPressed || kb.pageUpKey.isPressed) ? 1f : 0f;
            if (pad != null) direction += pad.rightStick.ReadValue().y;
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow > 0f && direction != 0f)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + direction * 600f * Time.unscaledDeltaTime / overflow);
        }
    }

    /// <summary>
    /// 67 타이틀 "최근 기록": 최근 20판 최신순 목록 → 판별 상세 → 뒤로. 행은 종료 상태·직업·무기·시간과 짧은 둘째 줄(날짜·원인/카드 수).
    /// 데스크톱 Esc는 상세 → 목록 순으로 하나씩 닫는다. 터치 UI의 뒤로는 MobileBackRouter가 "Close" → "BackButton" 순으로 누른다(같은 입력을 두 번 쓰지 않게 여기서는 처리하지 않음).
    /// </summary>
    public class RunHistoryListView : MonoBehaviour
    {
        const float ListWidth = 1500f;
        const float RowStep = 136f;
        const float RowHeight = 124f;
        public const string EmptyText = "아직 기록된 판이 없습니다.\n한 판을 마치면 최근 20판까지 여기에서 다시 볼 수 있습니다.";

        GameObject root;
        RectTransform content;
        ScrollRect scroll;
        Text countText, noticeText, emptyText;
        Button backButton;
        RunDetailView detail;
        Action onClose;
        readonly List<Button> rows = new List<Button>();
        int closedFrame = -1;

        public bool IsOpen => root != null && root.activeSelf;
        public RunDetailView Detail => detail;
        public int RowCount => rows.Count;
        public Button Row(int i) => rows[i];
        public Button BackButton => backButton;
        public bool EmptyVisible => emptyText != null && emptyText.gameObject.activeSelf;
        public string CountText => countText != null ? countText.text : "";
        public ScrollRect Scroll => scroll;
        public RectTransform Content => content;

        public static RunHistoryListView Create(Transform canvas, Action onClose)
        {
            var rootRt = UiFactory.Stretch("RunHistory", canvas);
            UiFactory.AddImage(rootRt, new Color(0.05f, 0.04f, 0.07f, 0.97f));
            UiFactory.AddCafeBackdrop(rootRt);
            var v = rootRt.gameObject.AddComponent<RunHistoryListView>();
            v.root = rootRt.gameObject;
            v.onClose = onClose;

            UiFactory.AddText(UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(1400f, 72f)),
                "최근 기록", 46, TextAnchor.MiddleCenter, Color.white);
            v.countText = UiFactory.AddText(UiFactory.Place("Count", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1400f, 40f)),
                "", 26, TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 1f));
            v.noticeText = UiFactory.AddText(UiFactory.Place("Notice", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(1500f, 38f)),
                "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.62f));

            // 목록: 제목 아래 ~ 뒤로 버튼 위. 1080 기준 높이 760(6행 남짓), 나머지는 스크롤.
            var area = UiFactory.Place("ScrollArea", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(ListWidth + 40f, 740f));
            var viewport = UiFactory.Stretch("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            var contentGo = new GameObject("Content", typeof(RectTransform));
            v.content = (RectTransform)contentGo.transform;
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
            v.scroll.scrollSensitivity = 40f;
            area.gameObject.AddComponent<ScrollToSelected>().Init(v.scroll, v.content, viewport);

            v.emptyText = UiFactory.AddText(UiFactory.Place("Empty", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 160f)),
                EmptyText, 32, TextAnchor.MiddleCenter, new Color(0.85f, 0.82f, 0.78f));

            var backRt = UiFactory.Place("BackButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(340f, 72f));
            var backBg = UiFactory.AddImage(backRt, new Color(0.5f, 0.2f, 0.55f));
            v.backButton = backRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(v.backButton);
            v.backButton.targetGraphic = backBg;
            UiFactory.ApplyHoverColors(v.backButton);
            v.backButton.onClick.AddListener(v.Close);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 30, TextAnchor.MiddleCenter, Color.white);

            // 상세는 마지막 자식 — 그리기·MobileBackRouter 탐색 순서에서 목록의 "뒤로"보다 위.
            v.detail = RunDetailView.Create(rootRt, null);

            v.root.SetActive(false);
            return v;
        }

        public void Show()
        {
            Rebuild();
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            EventSystem.current?.SetSelectedGameObject(rows.Count > 0 ? rows[0].gameObject : backButton.gameObject);
        }

        void Rebuild()
        {
            foreach (var b in rows) { b.gameObject.SetActive(false); Destroy(b.gameObject); }
            rows.Clear();
            var records = RunHistory.Records;
            countText.text = records.Count > 0 ? $"최근 {records.Count}판  ·  최신순  ·  최대 {RunHistoryStore.MaxRecords}판 보관" : "";
            noticeText.text = RunHistory.Notice ?? "";
            emptyText.gameObject.SetActive(records.Count == 0);
            for (int i = 0; i < records.Count; i++) rows.Add(BuildRow(records[i], i));
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(100f, records.Count * RowStep + 8f));

            // 패드/방향키: 행끼리 위아래, 마지막 행 → 뒤로. 뒤로에서 위 → 마지막 행.
            for (int i = 0; i < rows.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? rows[i - 1] : null;
                nav.selectOnDown = i + 1 < rows.Count ? rows[i + 1] : backButton;
                rows[i].navigation = nav;
            }
            backButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = rows.Count > 0 ? rows[rows.Count - 1] : null,
            };
        }

        Button BuildRow(RunRecord record, int index)
        {
            var rt = UiFactory.Place($"RunRow_{index}", content, new Vector2(0.5f, 1f), new Vector2(0f, -8f - index * RowStep),
                new Vector2(ListWidth, RowHeight));
            var bg = UiFactory.AddImage(rt, new Color(1f, 1f, 1f, 0.06f));
            bg.sprite = SpriteFactory.Square;
            UiFactory.SkinCard(bg);
            if (UiFactory.IsCafe(bg, "ui_cafe_card")) bg.color = Color.white;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            UiFactory.AddFocusSfx(button);
            var captured = record;
            button.onClick.AddListener(() => OpenDetail(captured, rt.gameObject));

            // 카페 카드 판은 어두운 나무색 — 도감 행과 같이 밝은 크림 글자.
            var ink = new Color(0.97f, 0.93f, 0.85f);
            var sub = new Color(0.82f, 0.76f, 0.68f);
            float line1 = UiFactory.TruncatedBoxHeight(30);
            var top = UiFactory.AddText(UiFactory.Place("Line1", rt, new Vector2(0f, 1f), new Vector2(36f, -8f), new Vector2(ListWidth - 72f, line1)),
                $"{index + 1}.  {RunRecordText.ListLine(record)}", 30, TextAnchor.MiddleLeft, ink);
            top.verticalOverflow = VerticalWrapMode.Truncate;
            string when = RunRecordText.EndedLocal(record);
            string second = record.IsDeath ? RunRecordText.EndDetail(record)
                : $"카드 {record.cards.Count}종 · 조합 {record.combos.Count} · 처치 {record.kills}";
            var bottom = UiFactory.AddText(UiFactory.Place("Line2", rt, new Vector2(0f, 1f), new Vector2(36f, -8f - line1), new Vector2(ListWidth - 72f, UiFactory.TruncatedBoxHeight(24))),
                (when.Length > 0 ? when + "  ·  " : "") + second, 24, TextAnchor.MiddleLeft, sub);
            bottom.verticalOverflow = VerticalWrapMode.Truncate;
            bottom.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        public void OpenDetail(RunRecord record, GameObject returnTo) => detail.Show(record, returnTo);

        public void Close()
        {
            if (!IsOpen) return;
            if (detail.IsOpen) { detail.Close(); return; } // 가장 위 상세부터
            root.SetActive(false);
            closedFrame = Time.frameCount;
            onClose?.Invoke();
        }

        /// <summary>데스크톱 Esc 한 번 = 가장 위 하나만 닫는다(터치 UI는 MobileBackRouter 담당).</summary>
        public bool HandleEscape()
        {
            if (!IsOpen || Time.frameCount == closedFrame) return false;
            if (detail.IsOpen) detail.Close(); else Close();
            return true;
        }

        void Update()
        {
            if (!IsOpen || MobilePlatform.IsTouchUI) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HandleEscape();
        }
    }
}
