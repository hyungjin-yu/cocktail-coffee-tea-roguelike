using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 재료 도감 탭(2026-11-15 플레이 피드백 2·3·6). 이전: 재료마다 큰 종이 카드(96 그림 + 본문 11줄)를 3열로 늘어놓아 한 화면에 3~6장,
    /// 그림이 글에 묻혔다. 지금: 위 = 검색(이름·맛·향) + 종류/발견 필터 + 초기화 + 결과 수, 아래 = 그림 칸 격자(그림 → 이름 → 보유/미발견),
    /// 칸을 누르면 큰 그림과 기존 본문(맛·향·색·사용·게임 효과·획득처·문화·산지)을 보이는 상세. 미발견은 실루엣 + "미발견" 배지 + 획득처 안내.
    /// 표시 전용 — 재고·발견·저장은 바꾸지 않는다.
    /// </summary>
    public partial class DrinkLabView
    {
        public enum CodexKind { All, Cocktail, Tea, Coffee }
        public enum CodexSeen { All, Seen, Unseen }

        // 탭을 다시 열어도(SetTab이 본문을 새로 만든다) 검색·필터는 유지 — 화면을 떠나면(창 닫기) 새 DrinkLabView라 초기화.
        string codexQuery = "";
        CodexKind codexKind = CodexKind.All;
        CodexSeen codexSeen = CodexSeen.All;
        RectTransform codexGrid, codexDetail;
        ScrollRect codexScroll;
        InputField codexSearch;
        Text codexCount;
        readonly Dictionary<CodexKind, Button> codexKindButtons = new Dictionary<CodexKind, Button>();
        readonly Dictionary<CodexSeen, Button> codexSeenButtons = new Dictionary<CodexSeen, Button>();
        string codexReturnId;

        public const float CodexCellMinWidth = 216f, CodexCellHeight = 236f, CodexCellIcon = 128f, CodexDetailIcon = 192f;
        public const string CodexLockedLabel = "미발견";
        public const float SignatureBookButtonW = 250f;
        public const string CodexLockedHint = "플레이 중 획득하면 등록됩니다";
        public const string CodexEmptyText = "조건에 맞는 재료가 없습니다 — 검색어를 지우거나 필터를 초기화하세요.";

        // ── 시험·QA용 읽기 ──
        public IReadOnlyList<string> CodexVisibleIds { get; private set; } = Array.Empty<string>();
        public string CodexCountText => codexCount != null ? codexCount.text : "";
        public string CodexQuery => codexQuery;
        public CodexKind CodexKindFilter => codexKind;
        public CodexSeen CodexSeenFilter => codexSeen;
        public bool CodexDetailOpen => codexDetail != null && codexDetail.gameObject.activeSelf;
        public string CodexDetailId { get; private set; }
        public RectTransform CodexGridContent => codexGrid;
        public ScrollRect CodexScroll => codexScroll;

        /// <summary>검색어 적용(입력 칸과 같은 경로).</summary>
        public void SetCodexQuery(string q)
        {
            if (codexSearch != null && codexSearch.text != (q ?? "")) { codexSearch.text = q ?? ""; return; } // onValueChanged → 아래로
            codexQuery = q ?? "";
            RebuildCodexGrid(true);
        }
        public void SetCodexKind(CodexKind k) { codexKind = k; RefreshCodexChips(); RebuildCodexGrid(true); }
        public void SetCodexSeen(CodexSeen s) { codexSeen = s; RefreshCodexChips(); RebuildCodexGrid(true); }
        public void ResetCodexFilters()
        {
            codexKind = CodexKind.All; codexSeen = CodexSeen.All;
            if (codexSearch != null) codexSearch.SetTextWithoutNotify("");
            codexQuery = "";
            RefreshCodexChips();
            RebuildCodexGrid(true);
        }

        /// <summary>검색·필터 판정(이름 + 맛·향 설명, 대소문자 무시). 이름이 공개된 재료만 있으므로 이름 검색이 정체를 새로 드러내지 않는다.</summary>
        public static bool CodexMatches(DrinkIngredientDef def, string query, CodexKind kind, CodexSeen seenFilter, bool seen)
        {
            if (def == null) return false;
            switch (kind)
            {
                case CodexKind.Cocktail: if (!def.UsableIn(DrinkKind.Cocktail)) return false; break;
                case CodexKind.Tea: if (!def.UsableIn(DrinkKind.Tea)) return false; break;
                case CodexKind.Coffee: if (!def.UsableIn(DrinkKind.Coffee)) return false; break;
            }
            if (seenFilter == CodexSeen.Seen && !seen) return false;
            if (seenFilter == CodexSeen.Unseen && seen) return false;
            var q = (query ?? "").Trim();
            if (q.Length == 0) return true;
            return (def.name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                || (def.taste ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void OpenCodexFor(DrinkIngredientDef def)
        {
            SetTab(Tab.Codex);
            if (def != null) OpenCodexDetail(def);
        }

        void BuildCodex(DrinkIngredientDef focus)
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            codexKindButtons.Clear(); codexSeenButtons.Clear();
            float listW = layoutW - 2f * FrameSafeInset; // 제조 탭과 같은 테두리 안전선(좌우 44)
            const float rowH = 64f, rowGap = 10f, top = 10f;

            // ── 1줄: 검색 + 지우기 + 결과 수 + 초기화 ──
            var bar = UiFactory.Place("CodexToolbar", body, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(listW, rowH * 2f + rowGap));
            codexSearch = MakeInput(bar, "Search", new Vector2(0f, 1f), Vector2.zero, new Vector2(Mathf.Min(560f, listW * 0.42f), rowH), "재료 이름·맛 검색", DrinkPaperUi.Small);
            codexSearch.lineType = InputField.LineType.SingleLine;
            codexSearch.characterLimit = 24;
            codexSearch.SetTextWithoutNotify(codexQuery);
            codexSearch.onValueChanged.AddListener(v => { codexQuery = v ?? ""; RebuildCodexGrid(true); });
            float sx = Mathf.Min(560f, listW * 0.42f) + 10f;
            DrinkPaperUi.Button(bar, "ClearSearch", new Vector2(0f, 1f), new Vector2(sx, 0f), new Vector2(150f, rowH), "지우기", DrinkPaperUi.Small,
                () => SetCodexQuery(""));
            DrinkPaperUi.Button(bar, "ResetFilters", new Vector2(1f, 1f), Vector2.zero, new Vector2(190f, rowH), "필터 초기화", DrinkPaperUi.Small, ResetCodexFilters);
            // #25 정식 레시피 도감 버튼(발견 수만 — 미발견 정보 없음). 필터 초기화 왼쪽, 결과 수는 그만큼 왼쪽으로.
            DrinkPaperUi.Button(bar, "SignatureBook", new Vector2(1f, 1f), new Vector2(-200f, 0f), new Vector2(SignatureBookButtonW, rowH), SignatureBookButtonLabel(), DrinkPaperUi.Small, OpenSignatureBook);
            codexCount = UiFactory.AddText(UiFactory.Place("ResultCount", bar, new Vector2(1f, 1f), new Vector2(-210f - SignatureBookButtonW, 0f), new Vector2(Mathf.Max(160f, listW - sx - 160f - 200f - 30f - SignatureBookButtonW), rowH)),
                "", DrinkPaperUi.Small, TextAnchor.MiddleRight, new Color(1f, 0.92f, 0.78f));
            codexCount.raycastTarget = false;

            // ── 2줄: 종류 · 발견 필터(선택 칩은 색 + 테두리 표식 + 글자 "✓"로 구분) ──
            float cx = 0f, chipW = Mathf.Min(150f, (listW - 40f) / 7.6f);
            UiFactory.AddText(UiFactory.Place("KindLabel", bar, new Vector2(0f, 1f), new Vector2(cx, -(rowH + rowGap)), new Vector2(76f, rowH)), "종류", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(0.9f, 0.86f, 0.8f));
            cx += 80f;
            foreach (var (k, label) in new[] { (CodexKind.All, "전체"), (CodexKind.Cocktail, "칵테일"), (CodexKind.Tea, "차"), (CodexKind.Coffee, "커피") })
            {
                var kk = k;
                codexKindButtons[k] = DrinkPaperUi.Button(bar, "Kind_" + k, new Vector2(0f, 1f), new Vector2(cx, -(rowH + rowGap)), new Vector2(chipW, rowH), label, DrinkPaperUi.Small, () => SetCodexKind(kk));
                cx += chipW + 8f;
            }
            cx += 24f;
            UiFactory.AddText(UiFactory.Place("SeenLabel", bar, new Vector2(0f, 1f), new Vector2(cx, -(rowH + rowGap)), new Vector2(76f, rowH)), "상태", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(0.9f, 0.86f, 0.8f));
            cx += 80f;
            foreach (var (s, label) in new[] { (CodexSeen.All, "전체"), (CodexSeen.Seen, "발견"), (CodexSeen.Unseen, CodexLockedLabel) })
            {
                var ss = s;
                codexSeenButtons[s] = DrinkPaperUi.Button(bar, "Seen_" + s, new Vector2(0f, 1f), new Vector2(cx, -(rowH + rowGap)), new Vector2(chipW, rowH), label, DrinkPaperUi.Small, () => SetCodexSeen(ss));
                cx += chipW + 8f;
            }

            // ── 격자(스크롤) ──
            float gridTop = top + rowH * 2f + rowGap + 12f;
            var listRt = UiFactory.Place("CodexList", body, new Vector2(0.5f, 1f), new Vector2(0f, -gridTop), new Vector2(listW, 884f + top - gridTop)); // 아래 끝은 이전과 같은 1036(테두리 안쪽)
            codexGrid = MakeScroll(listRt);
            codexScroll = listRt.GetComponent<ScrollRect>();

            // ── 상세(목록 위 덮개) ──
            codexDetail = UiFactory.Place("CodexDetail", body, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(listW, 884f));
            var shield = codexDetail.gameObject.AddComponent<Image>();
            shield.color = new Color(0.03f, 0.02f, 0.03f, 0.9f); // 뒤 격자 클릭 차단
            codexDetail.gameObject.SetActive(false);

            RefreshCodexChips();
            RebuildCodexGrid(true);
            if (focus != null) OpenCodexDetail(focus);
        }

        void RefreshCodexChips()
        {
            foreach (var kv in codexKindButtons) MarkChip(kv.Value, kv.Key == codexKind);
            foreach (var kv in codexSeenButtons) MarkChip(kv.Value, kv.Key == codexSeen);
        }

        static void MarkChip(Button b, bool on)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = on ? new Color(1f, 0.8f, 0.5f) : new Color(0.78f, 0.76f, 0.74f);
            var t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                var label = t.text.TrimStart('✓', ' ');
                t.text = on ? "✓ " + label : label;
                t.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        void RebuildCodexGrid(bool resetScroll)
        {
            if (codexGrid == null) return;
            for (int i = codexGrid.childCount - 1; i >= 0; i--)
            {
                var child = codexGrid.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            float listW = ((RectTransform)codexGrid.parent).rect.width;
            if (listW <= 1f) listW = layoutW - 2f * FrameSafeInset;
            const float gap = 12f;
            int cols = Mathf.Max(2, Mathf.FloorToInt((listW - 30f + gap) / (CodexCellMinWidth + gap)));
            float cellW = (listW - 30f - (cols + 1) * gap) / cols;

            var all = DrinkIngredients.All;
            var shown = all.Where(d => CodexMatches(d, codexQuery, codexKind, codexSeen, DrinkLab.Seen(d.id))).ToList();
            // 검색어가 있으면 이름이 맞는 재료를 먼저(맛·향 설명만 맞는 재료는 뒤) — 같은 묶음 안은 기존 도감 순서 그대로.
            var q = (codexQuery ?? "").Trim();
            if (q.Length > 0) shown = shown.OrderBy(d => (d.name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1).ToList();
            CodexVisibleIds = shown.Select(d => d.id).ToList();
            int seenCount = all.Count(d => DrinkLab.Seen(d.id));
            if (codexCount != null) codexCount.text = $"결과 {shown.Count} / {all.Count}종 · 발견 {seenCount}";

            float y = gap;
            for (int i = 0; i < shown.Count; i++)
            {
                int col = i % cols, row = i / cols;
                BuildCodexCell(shown[i], new Vector2(gap + col * (cellW + gap), -(gap + row * (CodexCellHeight + gap))), new Vector2(cellW, CodexCellHeight));
            }
            if (shown.Count > 0) y = gap + ((shown.Count + cols - 1) / cols) * (CodexCellHeight + gap);
            else
            {
                var empty = UiFactory.Place("CodexEmpty", codexGrid, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(listW - 80f, 90f));
                UiFactory.AddText(empty, CodexEmptyText, DrinkPaperUi.Body, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.78f));
                DrinkPaperUi.Button(codexGrid, "CodexEmptyReset", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(260f, 66f), "필터 초기화", DrinkPaperUi.Small, ResetCodexFilters);
                y = 230f;
            }
            // 기존 하단 안내(물·얼음 보조 자원, 78 공급 규칙)는 목록 끝에 그대로.
            var aux = UiFactory.Place("Aux", codexGrid, new Vector2(0f, 1f), new Vector2(gap, -y), new Vector2(listW - 40f, 90f));
            UiFactory.AddText(aux, "물·기본 얼음은 무료 제조 보조 자원입니다(배합에 기록). 물은 색을 희석하고 버프 기여가 없습니다.", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 1f));
            y += 100f;
            var supply = UiFactory.Place("SupplyRule", codexGrid, new Vector2(0f, 1f), new Vector2(gap, -y), new Vector2(listW - 40f, 150f));
            UiFactory.AddText(supply, DrinkLab.SupplyRuleText(), DrinkPaperUi.Small, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));
            y += 160f;
            codexGrid.sizeDelta = new Vector2(0f, y + 20f);
            if (resetScroll)
            {
                codexGrid.anchoredPosition = Vector2.zero; // 필터·검색이 바뀌면 맨 위부터(선택·스크롤 어긋남 방지)
                if (codexScroll != null) codexScroll.StopMovement();
            }
        }

        void BuildCodexCell(DrinkIngredientDef def, Vector2 pos, Vector2 size)
        {
            bool seen = DrinkLab.Seen(def.id);
            int owned = DrinkLab.Owned(def.id);
            var cell = UiFactory.Place("Cell_" + def.id, codexGrid, new Vector2(0f, 1f), pos, size);
            var bg = DrinkPaperUi.Sliced(cell, "ingredient_card", 2f, seen ? Color.white : new Color(0.72f, 0.7f, 0.68f));
            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            UiFactory.AddFocusSfx(button);
            var captured = def;
            button.onClick.AddListener(() => OpenCodexDetail(captured));

            // 그림이 주인공: 칸 위쪽 128(720p ≈85px, 이전 96 = 64px). 미발견 = 같은 그림의 어두운 실루엣 + 배지(색만으로 구분하지 않음).
            var img = UiFactory.AddImage(UiFactory.Place("Icon", cell, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(CodexCellIcon, CodexCellIcon)), Color.white);
            img.sprite = DrinkArt.SpriteOrPlaceholder(def.iconId, out _);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = seen ? Color.white : new Color(0.05f, 0.04f, 0.04f, 0.7f);
            var name = UiFactory.AddText(UiFactory.Place("Name", cell, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(size.x - 24f, 40f)),
                def.name, DrinkPaperUi.Small, TextAnchor.MiddleCenter, seen ? DrinkPaperUi.Ink : DrinkPaperUi.InkMuted);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = DrinkPaperUi.Small;
            name.raycastTarget = false;
            string status = owned > 0 ? $"보유 {DrinkRules.Format(owned, def.form)}" : seen ? "보유 0" : CodexLockedLabel;
            var st = UiFactory.AddText(UiFactory.Place("Status", cell, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(size.x - 24f, 34f)),
                status, DrinkPaperUi.Small, TextAnchor.MiddleCenter, owned > 0 ? DrinkPaperUi.InkGood : seen ? DrinkPaperUi.InkMuted : DrinkPaperUi.InkBad);
            st.raycastTarget = false;
            if (!seen)
            {
                var badge = UiFactory.Place("LockBadge", cell, new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(120f, 36f));
                UiFactory.AddImage(badge, new Color(0.15f, 0.11f, 0.1f, 0.92f)).raycastTarget = false;
                var label = UiFactory.Stretch("Label", badge);
                UiFactory.AddText(label, CodexLockedLabel, 22, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.6f)).raycastTarget = false;
                // 2026-11-16 최종 아트: 자물쇠(16px × 2) + "미발견".
                if (UiFactory.AddLockIcon(badge, new Vector2(4f, 0f), 32f) != null) label.offsetMin = new Vector2(36f, label.offsetMin.y);
            }
        }

        public void CloseCodexDetail()
        {
            if (codexDetail == null || !codexDetail.gameObject.activeSelf) return;
            codexDetail.gameObject.SetActive(false);
            CodexDetailId = null;
            SignatureBookOpen = false; // #25
            // 닫으면 연 칸으로 포커스·스크롤 위치 그대로(격자를 다시 만들지 않음).
            var cell = codexGrid != null && codexReturnId != null ? codexGrid.Find("Cell_" + codexReturnId) : null;
            if (cell != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cell.gameObject);
        }

        /// <summary>재료 상세(격자 칸 클릭·획득처 보기와 같은 경로). 필터에 걸려 격자에 없는 재료도 열 수 있다.</summary>
        public void OpenCodexDetail(DrinkIngredientDef def)
        {
            if (def == null || codexDetail == null) return;
            for (int i = codexDetail.childCount - 1; i >= 0; i--)
            {
                var child = codexDetail.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            codexReturnId = def.id;
            CodexDetailId = def.id;
            codexDetail.gameObject.SetActive(true);
            codexDetail.SetAsLastSibling();
            float listW = codexDetail.sizeDelta.x;
            float W = Mathf.Min(listW - 40f, 1500f), H = 864f;
            bool seen = DrinkLab.Seen(def.id);
            int owned = DrinkLab.Owned(def.id);
            bool leaf = def.form == IngredientForm.Leaf;
            var card = UiFactory.Place("Codex_" + def.id, codexDetail, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W, H));
            DrinkPaperUi.Sliced(card, "ingredient_card", 3f, seen ? Color.white : new Color(0.85f, 0.83f, 0.8f));

            // 왼쪽 열: 큰 그림(192 = 720p 128px, 원본 32px의 정수 4배) + 실제 색 잔
            var icon = UiFactory.AddImage(UiFactory.Place("Icon", card, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(CodexDetailIcon, CodexDetailIcon)), Color.white);
            icon.sprite = DrinkArt.SpriteOrPlaceholder(def.iconId, out _);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (!seen) icon.color = new Color(0.05f, 0.04f, 0.04f, 0.7f);
            bool bean = def.IsCoffeeBean;
            bool coffeeOnly = def.use == DrinkUse.Coffee;
            var probe = bean ? DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.PourOver)
                : DrinkRecipe.NewDraft(coffeeOnly ? DrinkKind.Coffee : leaf ? DrinkKind.Tea : DrinkKind.Cocktail);
            if (coffeeOnly && !bean) probe.water = 0; // 재료 자체의 색(커피 없이)
            probe.SetAmount(def.id, bean ? 150 : leaf ? 20 : 300);
            var glassRoot = UiFactory.Place("ColorGlass", card, new Vector2(0f, 1f), new Vector2(40f + (CodexDetailIcon - 110f) * 0.5f, -(40f + CodexDetailIcon + 30f)), new Vector2(110f, 120f));
            var glass = DrinkVesselView.Create(glassRoot, leaf ? "dl_teacup" : "dl_glass_cocktail", new Vector2(0f, -60f), 60f, 2);
            if (bean) glass.SetLiquid(CoffeeBrewing.BrewColor(probe), 40f, def.turbidity);
            else if (leaf) glass.SetLiquid(DrinkPhysicsSim.TeaColor(probe), 40f, def.turbidity);
            else glass.SetLiquid(DrinkCalculator.IngredientLook(def), 40f, def.turbidity, def.density);

            // 오른쪽 열: 이름 → 상태 → (스크롤) 본문 · 문화·산지
            float rx = 40f + CodexDetailIcon + 40f, rw = W - rx - 40f;
            var nameText = UiFactory.AddText(UiFactory.Place("Name", card, new Vector2(0f, 1f), new Vector2(rx, -36f), new Vector2(rw, 52f)), def.name,
                DrinkPaperUi.Heading + 6, TextAnchor.MiddleLeft, seen ? DrinkPaperUi.Ink : DrinkPaperUi.InkMuted);
            nameText.resizeTextForBestFit = true; nameText.resizeTextMinSize = DrinkPaperUi.Small; nameText.resizeTextMaxSize = DrinkPaperUi.Heading + 6;
            string state = owned > 0 ? $"보유 {DrinkRules.Format(owned, def.form)}" : seen ? "보유 0 · 발견함" : $"{CodexLockedLabel} · {CodexLockedHint}";
            UiFactory.AddText(UiFactory.Place("Status", card, new Vector2(0f, 1f), new Vector2(rx, -92f), new Vector2(rw, 40f)), state,
                DrinkPaperUi.Body, TextAnchor.MiddleLeft, owned > 0 ? DrinkPaperUi.InkGood : seen ? DrinkPaperUi.InkMuted : DrinkPaperUi.InkBad);

            string colorWords = bean ? "<b>핸드 드립</b>: " + CoffeeLookWords(probe) : (leaf ? "<b>우린 차</b>: " : "<b>실제 색</b>: ") + LookWords(DrinkCalculator.Look(probe));
            var ingredientEffect = DrinkCalculator.EffectFor(probe.Kind, def);
            string text =
                $"<b>맛·향</b>: {def.taste}\n" +
                $"{colorWords}\n" +
                $"<b>사용</b>: {def.UseLabel} · 단위 {def.Unit}\n" +
                $"<b>게임 효과</b>: {DrinkRules.StatName(ingredientEffect)} (출발 잔 상한 {DrinkBuff.Pct(ingredientEffect, DrinkRules.DepartureCap(ingredientEffect))} · 런 잔 {DrinkBuff.Pct(ingredientEffect, DrinkRules.Cap(ingredientEffect))})\n" +
                $"<b>착색</b> {(def.tint > 2f ? "강함" : def.tint > 0.5f ? "보통" : "약함")} · <b>탁도</b> {(def.turbidity > 0.3f ? "높음" : def.turbidity > 0.05f ? "약간" : "맑음")} · <b>밀도</b> {IntNum.Round(def.density * 1000f)}g/L\n" +
                $"<b>획득처</b>: {DrinkLab.SourceText(def.id)}" + (seen ? "" : "\n아직 보유한 적이 없어 제조에 쓸 수 없습니다.");
            float scrollTop = 144f, closeH = 66f, scrollH = H - scrollTop - closeH - 60f;
            var area = UiFactory.Place("DetailScroll", card, new Vector2(0f, 1f), new Vector2(rx, -scrollTop), new Vector2(rw, scrollH));
            var content = MakeScroll(area, true);
            var t = UiFactory.AddText(UiFactory.Place("Body", content, new Vector2(0f, 1f), Vector2.zero, new Vector2(rw - 24f, 40f)), text, DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
            float bodyH = Mathf.Ceil(TextHeightAt(t, rw - 24f)) + 4f;
            t.rectTransform.sizeDelta = new Vector2(rw - 24f, bodyH);
            float contentH = bodyH;
            if (!string.IsNullOrEmpty(def.culture))
            {
                // 문화·산지 사실은 맛·향/게임 정보와 다른 칸·색으로 구분한다(출처는 재료 확장 10종 문서).
                var ct = UiFactory.AddText(UiFactory.Place("Culture", content, new Vector2(0f, 1f), new Vector2(0f, -(bodyH + 12f)), new Vector2(rw - 24f, 40f)),
                    "<b>문화·산지</b>: " + def.culture, DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.InkMuted);
                ct.supportRichText = true;
                ct.lineSpacing = 1.05f;
                float cultureH = Mathf.Ceil(TextHeightAt(ct, rw - 24f)) + 4f;
                ct.rectTransform.sizeDelta = new Vector2(rw - 24f, cultureH);
                contentH = bodyH + 12f + cultureH;
            }
            content.sizeDelta = new Vector2(0f, contentH + 8f);

            var close = DrinkPaperUi.Button(card, "CloseDetail", new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, closeH), "목록으로", DrinkPaperUi.Small, CloseCodexDetail);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
    }
}
