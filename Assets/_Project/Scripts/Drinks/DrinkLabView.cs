using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69 음료 제조 화면(출발 전, 직업/무기 선택 화면에서 연다). 탭: 제조 / 재료 도감 / 레시피.
    /// 제조: 왼쪽 보유 재료(종류 필터·색·이름·보유량), 가운데 제조대(병 드래그·지거/다하·그릇), 아래 정밀 입력(0.1 단위 직접 입력·±버튼·키보드),
    /// 오른쪽 현재 배합(정확한 양·한도)·기법·얼음/물·완성 색·버프 미리보기·소모량/부족분 → 레시피 저장 / 제조 확정.
    /// 미리보기·취소·레시피 저장은 무료, 재료 차감은 제조 확정에서만(DrinkLab.Craft 트랜잭션).
    /// 73 단계2: 재료는 종이 카드, 오른쪽 배합은 영수증(실시간 계량·옮기는 중·확정 줄 구분), 레시피는 찢긴 레시피 카드 + 보관함,
    /// 저장 성공 뒤에만 노트에서 찢어 보관하는 모션(DrinkPaperUi). 글자는 실제 UI, 본문 기준 27(720p 18px).
    /// </summary>
    public class DrinkLabView : MonoBehaviour
    {
        public enum Tab { Craft, Codex, Recipes, Orders }

        Canvas canvas;
        RectTransform root, body;
        Action onClose;
        Tab tab = Tab.Craft;
        DrinkKind kind = DrinkKind.Cocktail;
        DrinkRecipe draft;
        int editingRecipeIndex = -1;
        DrinkIngredientDef selected;
        DrinkStageView stage;
        Text notice, amountError, preparedText, footerStatus;
        string mixLine = "", buffLine = "", costLine = "";
        RectTransform mixContent;
        bool costExpanded;
        InputField amountField, nameField, importField;
        RectTransform listContent;
        Button craftButton, saveButton;
        readonly Dictionary<DrinkTechnique, Button> techButtons = new Dictionary<DrinkTechnique, Button>();
        readonly Dictionary<Tab, Button> tabButtons = new Dictionary<Tab, Button>();
        bool skipMotion;
        DrinkRecipe importPreview;
        string lastMessage = "", fullNotice = "";
        // 좁은 창(가로/세로 1.18~1.7, 예: 836×705): 높이 맞춤으로 글자 크기를 16:9와 같게 두고, 영수증은 접는 서랍, 도감 2열, 레시피 가져오기는 목록 아래.
        public const float NarrowAspect = 1.7f, MinNarrowAspect = 1.18f;
        CanvasScaler scaler;
        bool narrow, receiptOpen;
        float layoutW = 1920f;
        RectTransform mixPanel;
        Button receiptToggle;
        public bool Narrow => narrow;
        public bool ReceiptOpen => receiptOpen;

        // ───── 테스트 훅 ─────
        public DrinkRecipe Draft => draft;
        public DrinkStageView Stage => stage;
        public Tab CurrentTab => tab;
        public DrinkKind Kind => kind;
        public string NoticeText => fullNotice;
        public string BuffText => buffLine;
        public string CostText => costLine;
        public string MixText => mixLine;
        public string FooterStatusText => footerStatus != null ? footerStatus.text : "";
        public string AmountErrorText => amountError != null ? amountError.text : "";
        public string PreparedText => preparedText != null ? preparedText.text : "";
        public bool CraftInteractable => craftButton != null && craftButton.interactable;
        public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;
        public bool SkipMotion { get => skipMotion; set => skipMotion = value; }
        public string LastMessage => lastMessage;
        public InputField AmountField => amountField;
        public InputField ImportField => importField;
        public DrinkRecipe ImportPreview => importPreview;
        public int ListRowCount => listContent != null ? listContent.childCount : 0;

        public static DrinkLabView Show(Action onClose)
        {
            var canvas = UiFactory.CreateCanvas(null, "DrinkLab", 140);
            var view = canvas.gameObject.AddComponent<DrinkLabView>();
            view.canvas = canvas;
            view.onClose = onClose;
            view.draft = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            var adaptive = canvas.GetComponent<AdaptiveCanvasScaler>();
            if (adaptive != null) { adaptive.enabled = false; Destroy(adaptive); } // 이 화면은 좁은 창 배치를 직접 고른다
            view.scaler = canvas.GetComponent<CanvasScaler>();
            view.narrow = view.ApplyScale();
            view.Build();
            return view;
        }

        public void Close()
        {
            stage?.StopPour();
            var cb = onClose;
            Destroy(canvas.gameObject);
            cb?.Invoke();
        }

        void Build()
        {
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(root);
            float titleW = narrow ? 300f : 420f, tabX = narrow ? 335f : 470f, tabW = narrow ? 170f : 210f, tabStep = narrow ? 178f : 220f;
            UiFactory.SkinHeader(UiFactory.AddText(UiFactory.Place("Title", root, new Vector2(0f, 1f), new Vector2(narrow ? 20f : 30f, -14f), new Vector2(titleW, 70f)), "음료 제조", narrow ? 38 : 44, TextAnchor.MiddleCenter, Color.white));
            float x = tabX;
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var tt = t;
                tabButtons[t] = MakeButton(root, "Tab" + t, new Vector2(0f, 1f), new Vector2(x, -16f), new Vector2(tabW, 66f), TabName(t), DrinkPaperUi.Body, new Color(0.25f, 0.2f, 0.3f), () => SetTab(tt));
                x += tabStep;
            }
            MakeButton(root, "Close", new Vector2(1f, 1f), new Vector2(narrow ? -20f : -30f, -16f), new Vector2(narrow ? 190f : 200f, 66f), MobilePlatform.IsTouchUI ? "닫기" : "닫기 (Esc)", DrinkPaperUi.Body, new Color(0.3f, 0.26f, 0.3f), Close);
            // 버전 표시는 위 탭 줄의 빈 자리(탭과 닫기 사이): 아래쪽 목록·입력·영수증·도감 카드와 겹치지 않는다. 좁은 창은 자리가 없어 생략(타이틀 화면에 표시).
            if (!narrow)
                UiFactory.AddText(UiFactory.Place("Mvp", root, new Vector2(1f, 1f), new Vector2(-250f, -32f), new Vector2(310f, 34f)), DrinkLab.MvpLabel, DrinkPaperUi.Small - 4, TextAnchor.MiddleRight, new Color(0.7f, 0.7f, 0.75f));
            // 안내 한 줄: 탭 줄(-16..-82) 아래, 본문(-118) 위, 화면 폭에 맞춤. 넘치면 말줄임(두 줄로 늘어 탭·본문을 덮지 않음).
            var noticeRt = UiFactory.Place("Notice", root, new Vector2(0f, 1f), new Vector2(0f, -86f), new Vector2(0f, 32f));
            noticeRt.anchorMin = new Vector2(0f, 1f); noticeRt.anchorMax = new Vector2(1f, 1f); noticeRt.pivot = new Vector2(0.5f, 1f);
            noticeRt.offsetMin = new Vector2(30f, -118f); noticeRt.offsetMax = new Vector2(-30f, -86f);
            notice = UiFactory.AddText(noticeRt, "", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.55f));
            notice.horizontalOverflow = HorizontalWrapMode.Overflow;
            notice.verticalOverflow = VerticalWrapMode.Truncate;
            SetTab(tab);
        }

        static string TabName(Tab t) => t switch { Tab.Craft => "제조", Tab.Codex => "재료 도감", Tab.Orders => "주문·판매", _ => "레시피" };

        public void SetTab(Tab t)
        {
            if (drawer != null) drawerOpen = drawer.State != RecipeDrawerView.Phase.Closed && drawer.State != RecipeDrawerView.Phase.Closing;
            drawer = null;
            stage?.StopPour();
            tab = t;
            foreach (var kv in tabButtons) kv.Value.GetComponent<Image>().color = kv.Key == t ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.25f, 0.2f, 0.3f);
            if (body != null) Destroy(body.gameObject);
            body = UiFactory.Stretch("Body", root);
            body.offsetMax = new Vector2(0f, -118f);
            stage = null;
            mixPanel = null; receiptToggle = null;
            switch (t)
            {
                case Tab.Craft: BuildCraft(); break;
                case Tab.Codex: BuildCodex(null); break;
                case Tab.Orders: BuildOrders(); break;
                default: BuildRecipes(); break;
            }
            RefreshNotice();
        }

        void RefreshNotice()
        {
            var n = DrinkLab.Notice;
            fullNotice = string.IsNullOrEmpty(lastMessage) ? (n ?? "") : lastMessage + (n != null ? "  ·  " + n : "");
            notice.text = OneLine(notice, fullNotice);
        }

        void Message(string text)
        {
            lastMessage = text ?? "";
            RefreshNotice();
        }

        /// <summary>글 상자 폭을 넘으면 끝을 말줄임으로 자른 한 줄(전체 문장은 lastMessage·테스트 훅에 그대로).</summary>
        static string OneLine(Text t, string text)
        {
            if (string.IsNullOrEmpty(text) || t == null) return text ?? "";
            text = text.Replace('\n', ' ');
            float w = t.rectTransform.rect.width;
            if (w <= 0f) return text;
            var gen = t.cachedTextGeneratorForLayout;
            var settings = t.GetGenerationSettings(new Vector2(w, t.rectTransform.rect.height));
            if (gen.GetPreferredWidth(text, settings) / t.pixelsPerUnit <= w) return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (gen.GetPreferredWidth(text.Substring(0, mid) + "…", settings) / t.pixelsPerUnit <= w) lo = mid; else hi = mid - 1;
            }
            return text.Substring(0, lo) + "…";
        }

        /// <summary>창 비율 → 배치 방식. 좁은 창은 높이 맞춤(캔버스 높이 1080, 폭 1080×비율), 그 밖은 기존 적응 맞춤. 좁은 창이면 true.</summary>
        bool ApplyScale()
        {
            float w = Screen.width, h = Mathf.Max(1f, Screen.height), aspect = w / h;
            bool n = aspect < NarrowAspect && aspect >= MinNarrowAspect;
            float m = n ? 1f : AdaptiveCanvasScaler.MatchFor(w, h);
            if (scaler != null && !Mathf.Approximately(scaler.matchWidthOrHeight, m)) scaler.matchWidthOrHeight = m;
            layoutW = n ? 1080f * aspect : 1920f;
            return n;
        }

        /// <summary>좁은 창 영수증 서랍: 닫히면 오른쪽 화면 밖(버튼은 그대로 살아 있음), 열리면 제조대 오른쪽 위에 펼친다.</summary>
        public void SetReceiptOpen(bool open)
        {
            receiptOpen = open;
            if (mixPanel == null) return;
            mixPanel.anchoredPosition = new Vector2(narrow && !open ? 660f : -24f, -6f);
            if (receiptToggle != null)
                receiptToggle.GetComponentInChildren<Text>().text = open ? "◂ 영수증 접기" : "영수증 · 제조 확정 ▸";
        }

        /// <summary>창 크기가 바뀌어 배치 방식이 달라지면 화면 전체를 다시 만든다(붓기·모션 중에는 끝난 뒤).</summary>
        void RebuildIfLayoutChanged()
        {
            bool n = ApplyScale();
            if (n == narrow) return;
            if (stage != null && (stage.Pouring || stage.Transferring || stage.SequencePlaying)) return;
            narrow = n;
            receiptOpen = false;
            tabButtons.Clear();
            if (root != null) Destroy(root.gameObject);
            body = null; stage = null; mixPanel = null; receiptToggle = null;
            Build();
        }

        // ═════ 제조 탭 ═════

        void BuildCraft()
        {
            // 종류 전환(칵테일/차/커피, 재료 목록 폭 410 안에 3칸). 전환 시 현재 배합은 비운다 — 부적합 재료를 자동 소비/혼합하지 않는다.
            MakeButton(body, "KindCocktail", new Vector2(0f, 1f), new Vector2(30f, -6f), new Vector2(130f, 66f), "칵테일", DrinkPaperUi.Body,
                kind == DrinkKind.Cocktail ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Cocktail));
            MakeButton(body, "KindTea", new Vector2(0f, 1f), new Vector2(170f, -6f), new Vector2(130f, 66f), "차", DrinkPaperUi.Body,
                kind == DrinkKind.Tea ? new Color(0.36f, 0.5f, 0.2f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Tea));
            MakeButton(body, "KindCoffee", new Vector2(0f, 1f), new Vector2(310f, -6f), new Vector2(130f, 66f), "커피", DrinkPaperUi.Body,
                kind == DrinkKind.Coffee ? new Color(0.42f, 0.26f, 0.14f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Coffee));

            // 왼쪽 재료 목록
            var listRt = UiFactory.Place("IngredientList", body, new Vector2(0f, 1f), new Vector2(30f, -80f), new Vector2(410f, narrow ? 770f : 836f));
            listContent = MakeScroll(listRt);
            BuildIngredientRows();

            // 가운데 제조대
            var stageRt = UiFactory.Place("Stage", body, new Vector2(0f, 1f), new Vector2(460f, -6f), new Vector2(800f, 690f));
            stage = DrinkStageView.Create(stageRt, draft, RemainingAllowed, OnDraftChanged);
            stage.SetOwnedLookup(d => DrinkLab.Owned(d.id));
            stage.MeasureChanged += () => liveDirty = true;

            // 정밀 입력
            var ctl = UiFactory.Place("AmountControls", body, new Vector2(0f, 1f), new Vector2(460f, -706f), new Vector2(800f, 240f));
            UiFactory.AddImage(ctl, new Color(0.1f, 0.08f, 0.12f, 0.92f));
            UiFactory.AddText(UiFactory.Place("Label", ctl, new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(500f, 40f)), "정확한 양 (0.1 단위 입력)", DrinkPaperUi.Body, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            amountField = MakeInput(ctl, "Amount", new Vector2(0f, 1f), new Vector2(16f, -48f), new Vector2(220f, 66f), "0.0", 30);
            amountField.onEndEdit.AddListener(t => ApplyTypedAmount(t));
            MakeButton(ctl, "Apply", new Vector2(0f, 1f), new Vector2(246f, -48f), new Vector2(120f, 66f), "적용", DrinkPaperUi.Small, new Color(0.4f, 0.3f, 0.2f), () => ApplyTypedAmount(amountField.text));
            MakeButton(ctl, "Remove", new Vector2(0f, 1f), new Vector2(376f, -48f), new Vector2(120f, 66f), "빼기", DrinkPaperUi.Small, new Color(0.45f, 0.2f, 0.2f), () => { if (selected != null) { draft.Remove(selected.id); OnDraftChanged(); } });
            int[] steps = { -100, -10, -1, 1, 10, 100 };
            for (int i = 0; i < steps.Length; i++)
            {
                int s = steps[i];
                string label = (s > 0 ? "+" : "−") + DrinkRules.Format(Math.Abs(s));
                MakeButton(ctl, "Nudge" + s, new Vector2(0f, 1f), new Vector2(16f + i * 128f, -122f), new Vector2(118f, 66f), label, DrinkPaperUi.Small, new Color(0.22f, 0.22f, 0.3f), () => Nudge(s));
            }
            amountError = UiFactory.AddText(UiFactory.Place("Error", ctl, new Vector2(0f, 1f), new Vector2(510f, -50f), new Vector2(280f, 60f)), "", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.5f));
            UiFactory.AddText(UiFactory.Place("Hint", ctl, new Vector2(0f, 1f), new Vector2(16f, -194f), new Vector2(770f, 40f)),
                MobilePlatform.IsTouchUI ? "병을 누른 채 끌어 붓기 · 손을 떼면 멈춤" : "병을 끌어 붓기 · 놓으면 멈춤 · ↑↓ 0.1, Shift 1.0", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.9f));

            // 오른쪽 영수증: 위 준비 음료 / 가운데 스크롤(배합 → 제조 방법 → 예상 효과 → 재료 소모) / 아래 고정 버튼(제조 확정은 항상 같은 자리).
            // 영수증 아트 3배(테두리 L/R 36·위/아래 60, 안쪽 글자 영역 = contentRect 왼쪽 16·위 20 아트 픽셀).
            var mix = UiFactory.Place("MixPanel", body, new Vector2(1f, 1f), new Vector2(-24f, -6f), new Vector2(630f, 950f));
            mixPanel = mix;
            DrinkPaperUi.Sliced(mix, "receipt", 3f).raycastTarget = true;
            // 종이 안쪽 안전 여백: 좌우 48, 위 52, 아래 66(영수증 찢긴 끝에 버튼이 걸치지 않게). 스크롤 내용과 고정 아래쪽 사이 22 간격.
            const float padX = 48f, padTop = 52f, padBottom = 66f, gap = 14f, innerW = 630f - 2f * padX, panelH = 950f;
            const float buttonsY = padBottom, buttonsH = 66f;
            const float nameY = buttonsY + buttonsH + gap, nameH = 60f;
            const float craftY = nameY + nameH + gap, craftH = 70f;
            const float statusY = craftY + craftH + 8f, statusH = 40f;
            const float ruleY = statusY + statusH + 10f;
            float scrollTop = padTop + 80f, scrollH = panelH - scrollTop - (ruleY + 22f);
            preparedText = UiFactory.AddText(UiFactory.Place("Prepared", mix, new Vector2(0f, 1f), new Vector2(padX, -padTop), new Vector2(innerW, 76f)), "", DrinkPaperUi.Small, TextAnchor.MiddleLeft, DrinkPaperUi.InkGood);
            var scrollRt = UiFactory.Place("MixScroll", mix, new Vector2(0f, 1f), new Vector2(padX - 8f, -scrollTop), new Vector2(innerW + 16f, scrollH));
            mixContent = MakeScroll(scrollRt, transparent: true);
            lastTopology = null; leftTexts.Clear(); rightTexts.Clear(); // 새 패널 — 다음 갱신은 반드시 다시 만든다
            UiFactory.AddImage(UiFactory.Place("FooterRule", mix, new Vector2(0f, 0f), new Vector2(padX, ruleY), new Vector2(innerW, 3f)), new Color(0.42f, 0.33f, 0.25f, 0.5f)).raycastTarget = false;
            footerStatus = UiFactory.AddText(UiFactory.Place("FooterStatus", mix, new Vector2(0f, 0f), new Vector2(padX, statusY), new Vector2(innerW, statusH)), "", DrinkPaperUi.Small, TextAnchor.MiddleLeft, DrinkPaperUi.Ink);
            footerStatus.supportRichText = true;
            craftButton = DrinkPaperUi.Button(mix, "Craft", new Vector2(0f, 0f), new Vector2(padX, craftY), new Vector2(innerW, craftH), "제조 확정", DrinkPaperUi.Heading, ConfirmCraft, tint: new Color(1f, 0.82f, 0.6f));
            nameField = MakeInput(mix, "RecipeName", new Vector2(0f, 0f), new Vector2(padX, nameY), new Vector2(innerW, nameH), "레시피 이름", DrinkPaperUi.Small);
            nameField.characterLimit = DrinkRules.RecipeNameMax;
            float bw = (innerW - 2f * gap) / 3f;
            saveButton = DrinkPaperUi.Button(mix, "SaveRecipe", new Vector2(0f, 0f), new Vector2(padX, buttonsY), new Vector2(bw, buttonsH), "레시피 저장", DrinkPaperUi.Small, SaveDraftAsRecipe);
            DrinkPaperUi.Button(mix, "Clear", new Vector2(0f, 0f), new Vector2(padX + bw + gap, buttonsY), new Vector2(bw, buttonsH), "비우기", DrinkPaperUi.Small, () => { if (Busy("비우기")) return; draft = DrinkRecipe.NewDraft(kind, CoffeeMethodOrDefault()); editingRecipeIndex = -1; Rebind(); });
            DrinkPaperUi.Button(mix, "SkipMotion", new Vector2(0f, 0f), new Vector2(padX + 2f * (bw + gap), buttonsY), new Vector2(bw, buttonsH), "", DrinkPaperUi.Small, () => { skipMotion = !skipMotion; RefreshMix(); });
            if (DrinkLab.DevToolsEnabled && DrinkLab.IsIsolatedSave)
                MakeButton(body, "DevGrant", new Vector2(0f, 0f), new Vector2(30f, 4f), new Vector2(260f, 44f), "테스트 재료 지급(격리 저장)", 16, new Color(0.4f, 0.15f, 0.4f), () => { DrinkLab.DevGrant(); BuildIngredientRows(); RefreshMix(); });

            if (narrow)
            {
                // 좁은 창: 영수증은 오른쪽 서랍(제조대 위에 펼침). 여닫기 버튼은 재료 목록 아래 고정 자리.
                receiptToggle = MakeButton(body, "ReceiptToggle", new Vector2(0f, 1f), new Vector2(30f, -862f), new Vector2(410f, 70f), "", DrinkPaperUi.Body, new Color(0.55f, 0.36f, 0.16f), () => SetReceiptOpen(!receiptOpen));
                mix.SetAsLastSibling();
                SetReceiptOpen(receiptOpen);
            }
            stage.RefreshDraft();
            if (selected != null && !selected.UsableIn(kind)) selected = null;
            SelectIngredient(selected ?? FirstUsable());
            RefreshMix();
        }

        DrinkIngredientDef FirstUsable()
        {
            foreach (var d in DrinkIngredients.All) if (d.UsableIn(kind) && DrinkLab.Owned(d.id) > 0) return d;
            return null;
        }

        void BuildIngredientRows()
        {
            if (listContent == null) return;
            foreach (Transform c in listContent) Destroy(c.gameObject);
            float y = 0f;
            foreach (var def in DrinkIngredients.All)
            {
                if (!def.UsableIn(kind)) continue; // 종류 필터
                var d = def;
                int owned = DrinkLab.Owned(def.id);
                bool locked = owned <= 0;
                // 재료 종이 카드(9slice 3배): 선택한 카드는 오른쪽으로 살짝 나오고 따뜻한 종이색, 없는 재료는 회색 종이.
                bool on = selected == def;
                var row = UiFactory.Place("Row_" + def.id, listContent, new Vector2(0.5f, 1f), new Vector2(on ? 8f : 0f, -y), new Vector2(386f, 150f));
                var bg = DrinkPaperUi.Sliced(row, "ingredient_card", 2f, locked ? new Color(0.7f, 0.68f, 0.66f) : on ? new Color(1f, 0.86f, 0.62f) : Color.white);
                bg.raycastTarget = true;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                UiFactory.AddFocusSfx(btn);
                btn.onClick.AddListener(() => { if (DrinkLab.Owned(d.id) > 0) SelectIngredient(d); else OpenCodexFor(d); });
                var icon = DrinkArt.SpriteOrPlaceholder(def.iconId, out _);
                var iconRt = UiFactory.Place("Icon", row, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(64f, 64f));
                var iconImg = iconRt.gameObject.AddComponent<Image>(); iconImg.sprite = icon; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                if (locked) iconImg.color = new Color(0.45f, 0.45f, 0.45f, 1f);
                UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 1f), new Vector2(104f, -30f), new Vector2(250f, 38f)), def.name, DrinkPaperUi.Body, TextAnchor.MiddleLeft, locked ? DrinkPaperUi.InkMuted : DrinkPaperUi.Ink);
                UiFactory.AddText(UiFactory.Place("Owned", row, new Vector2(0f, 1f), new Vector2(104f, -68f), new Vector2(260f, 70f)),
                    locked ? "보유 없음\n눌러서 획득처 보기" : $"보유 {DrinkRules.Format(owned, def.form)}\n{DrinkRules.StatName(def.effect)}", DrinkPaperUi.Small - 2, TextAnchor.UpperLeft, locked ? DrinkPaperUi.InkBad : DrinkPaperUi.InkMuted);
                y += 156f;
            }
            listContent.sizeDelta = new Vector2(0f, y);
        }

        DrinkIngredientDef queuedSelect;

        void SelectIngredient(DrinkIngredientDef def)
        {
            if (stage != null && (stage.Transferring || stage.Pouring) && def != selected)
            {
                // 옮겨 붓기가 끝나 도구가 제자리로 돌아온 뒤 바꾼다(도구 순간이동 없음).
                queuedSelect = def;
                Message("옮겨 붓기가 끝나면 " + (def != null ? def.name : "재료") + "(으)로 바꿉니다.");
                return;
            }
            if (def != null && (!def.UsableIn(kind) || DrinkLab.Owned(def.id) <= 0)) return; // 없는 재료는 제조에 쓸 수 없다
            selected = def;
            stage?.Select(def);
            if (amountField != null) amountField.SetTextWithoutNotify(def != null ? DrinkRules.Format(draft.AmountOf(def.id)) : "");
            if (amountError != null) amountError.text = "";
            BuildIngredientRows();
        }

        /// <summary>재고·종류 한도를 고려한 "지금 이 재료를 배합에 더 넣을 수 있는 양"(0.1 단위).</summary>
        public int RemainingAllowed(DrinkIngredientDef def)
        {
            if (def == null || !def.UsableIn(kind)) return 0;
            int inDraft = draft.AmountOf(def.id);
            int stock = DrinkLab.Owned(def.id) - inDraft;
            int limit;
            if (def.form == IngredientForm.Leaf) limit = (kind == DrinkKind.Coffee ? CoffeeBrewing.DoseMaxTenths(draft.coffeeMethod) : DrinkRules.TeaLeafMax) - draft.LeafTotal();
            else if (kind == DrinkKind.Tea) limit = DrinkRules.TeaAdditiveMax - draft.LiquidTotal();
            else if (kind == DrinkKind.Coffee)
            {
                // 스팀 우유: 첨가 상한과 잔 여유(추출량 + 첨가 ≤ 잔의 82%) 중 작은 쪽.
                int cupRoom = Mathf.FloorToInt(DrinkRules.ServeCapacityMl(draft) * DrinkRules.ServeFillMax * 10f);
                limit = Math.Min(DrinkRules.CoffeeAdditiveMax - draft.LiquidTotal(), cupRoom - draft.water - draft.LiquidTotal());
            }
            else limit = DrinkRules.CocktailLiquidMax - draft.LiquidTotal() - draft.water;
            return Math.Max(0, Math.Min(stock, limit));
        }

        /// <summary>직접 입력·붙여넣기 → 검증 후 절대량 설정. 실패하면 배합을 바꾸지 않고 이유를 보인다.</summary>
        public bool ApplyTypedAmount(string text)
        {
            if (selected == null) { amountError.text = "재료를 먼저 고르세요."; return false; }
            if (!DrinkRules.TryParseAmount(text, out int tenths, out string error))
            {
                amountError.text = error;
                amountField.SetTextWithoutNotify(DrinkRules.Format(draft.AmountOf(selected.id)));
                return false;
            }
            return SetSelectedAmount(tenths);
        }

        public bool SetSelectedAmount(int tenths)
        {
            if (selected == null) return false;
            if (Busy("양 변경")) { amountField.SetTextWithoutNotify(DrinkRules.Format(draft.AmountOf(selected.id))); return false; }
            int current = draft.AmountOf(selected.id);
            int max = current + RemainingAllowed(selected);
            int owned = DrinkLab.Owned(selected.id);
            if (tenths > max)
            {
                amountError.text = tenths > owned ? $"보유량({DrinkRules.Format(owned, selected.form)})을 넘습니다." : "이 음료의 최대 양을 넘습니다.";
                amountField.SetTextWithoutNotify(DrinkRules.Format(current));
                return false;
            }
            amountError.text = "";
            draft.SetAmount(selected.id, tenths);
            OnDraftChanged();
            return true;
        }

        public void Nudge(int delta)
        {
            if (selected == null) return;
            int target = Math.Max(0, draft.AmountOf(selected.id) + delta);
            int max = draft.AmountOf(selected.id) + RemainingAllowed(selected);
            SetSelectedAmount(Math.Min(target, max));
        }

        bool liveDirty, wasTransferring;
        float liveTimer;

        /// <summary>표시용 예상 배합: 확정 배합 + 지금 지거/다하에 담긴 양(옮겨 붓기 전). 옮겨 부은 뒤에는 InMeasure=0이라 중복 없음.</summary>
        public DrinkRecipe Projected()
        {
            if (stage == null || stage.InMeasure <= 0 || stage.CurrentId == null) return draft;
            var r = draft.Clone();
            r.SetAmount(stage.CurrentId, r.AmountOf(stage.CurrentId) + stage.InMeasure);
            return r;
        }

        void Update()
        {
            RebuildIfLayoutChanged();
            liveTimer -= Time.unscaledDeltaTime;
            // 영수증의 '옮기는 중 %' 줄은 옮기는 동안 갱신하고, 끝나는 순간 확정 줄(✓)로 바꾼다.
            bool transferring = stage != null && stage.Transferring;
            if (transferring || wasTransferring != transferring) liveDirty = true;
            wasTransferring = transferring;
            if (queuedSelect != null && stage != null && !stage.Transferring && !stage.Pouring) { var q = queuedSelect; queuedSelect = null; SelectIngredient(q); }
            if (liveDirty && liveTimer <= 0f)
            {
                // 붓는 동안 오른쪽 배합·효과·소모와 입력 칸을 초당 약 8회 갱신(계량 중인 양 포함).
                liveDirty = false;
                liveTimer = 0.12f;
                RefreshMix();
                if (amountField != null && selected != null && stage != null && stage.CurrentId == selected.id)
                    amountField.SetTextWithoutNotify(DrinkRules.Format(Projected().AmountOf(selected.id)));
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { if (stage != null && stage.SequencePlaying) stage.Skip(); else Close(); return; }
            if (tab != Tab.Craft || selected == null) return;
            bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                          EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
            if (typing) return;
            int step = kb.shiftKey.isPressed ? 10 : 1;
            if (kb.upArrowKey.wasPressedThisFrame || kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Nudge(step);
            else if (kb.downArrowKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Nudge(-step);
        }

        void SetKind(DrinkKind k)
        {
            if (k == kind) return;
            if (Busy("종류 변경")) return;
            kind = k;
            draft = DrinkRecipe.NewDraft(k); // 부적합 재료를 옮기거나 섞지 않는다
            editingRecipeIndex = -1;
            selected = null;
            Message(k == DrinkKind.Coffee ? $"커피 배합으로 바꿨습니다(이전 배합은 비웠습니다) — 추출 도구: {CoffeeBrewing.DisplayName(draft.coffeeMethod)}."
                : k == DrinkKind.Tea ? "차 배합으로 바꿨습니다(칵테일 배합은 비웠습니다)." : "칵테일 배합으로 바꿨습니다(차 배합은 비웠습니다).");
            SetTab(Tab.Craft);
        }

        /// <summary>지금 커피 배합의 추출 도구(커피가 아니거나 알 수 없으면 에스프레소 머신).</summary>
        CoffeeBrewMethod CoffeeMethodOrDefault() =>
            draft != null && draft.Kind == DrinkKind.Coffee && CoffeeBrewing.IsDefined(draft.coffeeMethod) ? draft.coffeeMethod : CoffeeBrewMethod.EspressoMachine;

        /// <summary>커피 추출 도구 고르기: 물(추출량)은 그 도구의 기본값으로, 원두는 그대로. 제조대 가운데 도구가 바뀐다.</summary>
        public void SetCoffeeMethod(CoffeeBrewMethod m)
        {
            if (kind != DrinkKind.Coffee || !CoffeeBrewing.IsDefined(m) || Busy("추출 도구 변경")) return;
            if (draft.coffeeMethod == m) return;
            draft.SetCoffeeMethod(m);
            stage?.Select(selected); // 도구 안내 문구·스쿱 위치를 새 도구 기준으로
            OnDraftChanged();
            Message($"추출 도구: {CoffeeBrewing.DisplayName(m)} — {CoffeeBrewing.ShortDescription(m)}");
        }

        void SetTechnique(DrinkTechnique t)
        {
            if (!DrinkRules.AllowedTechnique(kind, t) || Busy("기법 변경")) return;
            draft.technique = (int)t;
            OnDraftChanged();
        }

        /// <summary>배합 객체를 바꾼 뒤(비우기/레시피 불러오기) 제조대를 다시 묶는다.</summary>
        void Rebind() => SetTab(Tab.Craft);

        /// <summary>테스트·QA 캡처: Draft를 직접 바꾼 뒤 화면 갱신.</summary>
        public void RefreshForTests() => OnDraftChanged();

        void OnDraftChanged()
        {
            stage?.RefreshDraft();
            if (amountField != null && selected != null) amountField.SetTextWithoutNotify(DrinkRules.Format(draft.AmountOf(selected.id)));
            RefreshMix();
        }

        static readonly Color Gold = DrinkPaperUi.Ink;
        static readonly Color Muted = DrinkPaperUi.InkMuted;
        const float RowW = 520f;

        // ───── 오른쪽 패널: 행 명세 → (구조가 같으면) 글자만 갱신 / (구조가 바뀌면) 다시 만들기 ─────
        enum RowKind { Header, Line, Techs, Toggle }
        sealed class RowSpec
        {
            public RowKind kind;
            public string name, left, right;
            public Color color = DrinkPaperUi.Ink;
            public float h = 42f;
            public int size = DrinkPaperUi.Body;
            public Action minus, plus, remove;
        }

        readonly Dictionary<string, Text> leftTexts = new Dictionary<string, Text>();
        readonly Dictionary<string, Text> rightTexts = new Dictionary<string, Text>();
        string lastTopology;
        public int PanelRebuilds { get; private set; }

        List<RowSpec> BuildSpecs(DrinkRecipe shown, int pending, out string topology)
        {
            var specs = new List<RowSpec>();
            var topo = new System.Text.StringBuilder();
            void Add(RowSpec s) { specs.Add(s); topo.Append(s.kind).Append(':').Append(s.name).Append('|'); }
            string total = kind == DrinkKind.Cocktail
                ? $"합계 {DrinkRules.Format(shown.LiquidTotal() + shown.water)} / {DrinkRules.Format(DrinkRules.CocktailLiquidMax)}mL"
                : kind == DrinkKind.Coffee
                ? $"원두 {DrinkRules.Format(shown.LeafTotal())} / {DrinkRules.Format(CoffeeBrewing.DoseMaxTenths(shown.coffeeMethod))}g"
                : $"찻잎 {DrinkRules.Format(shown.LeafTotal())} / {DrinkRules.Format(DrinkRules.TeaLeafMax)}g";
            Add(new RowSpec { kind = RowKind.Header, name = "H_mix", left = "배합", right = total });
            int idx = 1;
            string moving = stage != null && stage.Transferring ? stage.TransferId : null;
            foreach (var s in draft.steps)
            {
                var def = DrinkIngredients.Get(s.id);
                if (def == null) continue;
                string id = s.id;
                bool inTransfer = moving == id;
                // 확정 줄: 배합에 들어간 양(✓). 지거에서 그릇으로 옮기는 중이면 그 표시(양은 이미 정확히 확정).
                Add(new RowSpec
                {
                    // 옮기는 중 표시는 둘째 줄(긴 재료 이름에서도 잘리지 않게). 이름이 달라 시작·끝에만 다시 배치한다.
                    kind = RowKind.Line, name = "Mix_" + id + (inTransfer ? "_moving" : ""),
                    left = $"{idx++}. {def.name}" + (inTransfer ? $"\n<color=#1f548f>옮기는 중 {Mathf.RoundToInt(stage.TransferProgress * 100f)}%</color>" : " ✓"),
                    right = DrinkRules.Format(s.amount, def.form),
                    remove = () => { if (Busy("빼기")) return; this.draft.Remove(id); OnDraftChanged(); },
                });
            }
            if (pending > 0 && stage != null && stage.CurrentId != null)
            {
                // 계량 중 줄(아직 배합 확정 전): 같은 프레임 장부 — 지거(다하)에 담긴 양 + 병에서 떨어지는 중인 양 = 계량량
                var def = DrinkIngredients.Get(stage.CurrentId);
                var m = stage.Ledger[DrinkStageView.LedgerMeasure];
                bool leaf = def.form == IngredientForm.Leaf;
                double inTool = leaf ? m.LeafG : m.LiquidMl;
                double air = leaf ? stage.Ledger.InFlightLeafG(DrinkStageView.LedgerMeasure) : stage.Ledger.InFlightMl(DrinkStageView.LedgerMeasure);
                Add(new RowSpec
                {
                    kind = RowKind.Line, name = "Measure", color = DrinkPaperUi.InkLive, h = 76f, size = DrinkPaperUi.Small,
                    left = $"계량 중 {def.name} +{DrinkRules.Format(pending, def.form)}\n{(leaf ? "다하" : "지거")} 안 {DrinkRules.Format(Mathf.RoundToInt((float)(inTool * 10.0)), def.form)} · 떨어지는 중 {DrinkRules.Format(Mathf.RoundToInt((float)(air * 10.0)), def.form)}",
                });
            }
            if (shown.steps.Count == 0) Add(new RowSpec { kind = RowKind.Line, name = "MixEmpty", left = kind == DrinkKind.Coffee ? "원두를 골라 통을 끌어 스쿱에 덜으세요" : "재료를 골라 병을 끌어 부으세요", color = Muted });
            if (kind == DrinkKind.Cocktail)
            {
                Add(new RowSpec { kind = RowKind.Line, name = "Water", left = "물 (무료)", right = DrinkRules.Format(shown.water) + "mL", color = Muted,
                    minus = () => { this.draft.water = Mathf.Max(0, this.draft.water - 50); OnDraftChanged(); },
                    plus = () => { this.draft.water = Mathf.Min(Math.Min(DrinkRules.CocktailWaterMax, DrinkRules.CocktailLiquidMax - this.draft.LiquidTotal()), this.draft.water + 50); OnDraftChanged(); } });
                Add(new RowSpec { kind = RowKind.Line, name = "Ice", left = "얼음 (무료)", right = $"{shown.ice}개", color = Muted,
                    minus = () => { this.draft.ice = Mathf.Max(0, this.draft.ice - 1); OnDraftChanged(); },
                    plus = () => { this.draft.ice = Mathf.Min(DrinkRules.IceMax, this.draft.ice + 1); OnDraftChanged(); } });
            }
            else if (kind == DrinkKind.Coffee)
            {
                var m = shown.coffeeMethod;
                int step = CoffeeBrewing.WaterStepTenths(m);
                Add(new RowSpec { kind = RowKind.Line, name = "Water", left = "추출량 (물, 무료)", right = DrinkRules.Format(shown.water) + "mL", color = Muted,
                    minus = () => { if (Busy("추출량 변경")) return; this.draft.water = Mathf.Max(CoffeeBrewing.WaterMinTenths(this.draft.coffeeMethod), this.draft.water - step); OnDraftChanged(); },
                    plus = () => { if (Busy("추출량 변경")) return; this.draft.water = Mathf.Min(CoffeeBrewing.WaterMaxTenths(this.draft.coffeeMethod), this.draft.water + step); OnDraftChanged(); } });
                int dose = shown.LeafTotal();
                string balance = dose > 0 && shown.water > 0
                    ? $"{Mathf.RoundToInt(CoffeeBrewing.Strength01(m, dose, shown.water) * 100f)}% · 1:{shown.water / (float)dose:0.#} (목표 1:{CoffeeBrewing.ExtractionRatio(m):0.#})"
                    : $"목표 1:{CoffeeBrewing.ExtractionRatio(m):0.#}";
                Add(new RowSpec { kind = RowKind.Line, name = "CoffeeBalance", left = "추출 균형", right = balance, color = Muted, size = DrinkPaperUi.Small, h = 48f });
            }
            else Add(new RowSpec { kind = RowKind.Line, name = "Water", left = "물 (무료, 고정)", right = "150.0mL", color = Muted });
            Add(new RowSpec { kind = RowKind.Header, name = "H_tech", left = kind == DrinkKind.Coffee ? "추출 도구" : "제조 방법" });
            Add(new RowSpec { kind = RowKind.Techs, name = "Techs_" + kind + "_" + shown.technique + "_" + (int)shown.coffeeMethod });
            Add(new RowSpec { kind = RowKind.Line, name = "TechDesc", left = kind == DrinkKind.Coffee ? CoffeeBrewing.ShortDescription(shown.coffeeMethod) : DrinkRules.TechniqueShort(shown.Technique), color = Muted, h = 66f, size = DrinkPaperUi.Small });
            Add(new RowSpec { kind = RowKind.Header, name = "H_buff", left = "예상 효과", right = "이번 판 동안" });
            var buff = DrinkCalculator.Buff(shown);
            var buffParts = new List<string>();
            foreach (var s in buff.Active())
            {
                Add(new RowSpec { kind = RowKind.Line, name = "Buff_" + s, left = DrinkRules.StatName(s), right = DrinkBuff.Pct(s, buff[s]), color = DrinkPaperUi.InkGood });
                buffParts.Add($"{DrinkRules.StatName(s)} {DrinkBuff.Pct(s, buff[s])}");
            }
            if (buffParts.Count == 0) Add(new RowSpec { kind = RowKind.Line, name = "BuffNone", left = "효과 없음", color = Muted });
            buffLine = buffParts.Count == 0 ? "효과 없음" : string.Join("\n", buffParts);
            if (kind == DrinkKind.Coffee)
            {
                if (shown.LeafTotal() > 0 && shown.water > 0) Add(new RowSpec { kind = RowKind.Line, name = "LookDesc", left = "완성 모습: " + CoffeeLookWords(shown), color = Muted, h = 40f, size = DrinkPaperUi.Small });
            }
            else
            {
                var look = DrinkCalculator.Look(shown);
                if (look.totalVolume > 0f) Add(new RowSpec { kind = RowKind.Line, name = "LookDesc", left = "완성 모습: " + LookWords(look), color = Muted, h = 40f, size = DrinkPaperUi.Small });
            }
            Add(new RowSpec { kind = RowKind.Toggle, name = "CostToggle", left = (costExpanded ? "▾ " : "▸ ") + "재료 소모와 보유량" });
            var shortages = DrinkValidator.Shortages(shown, DrinkLab.Owned);
            var costParts = new List<string>();
            foreach (var s in shown.steps)
            {
                var def = DrinkIngredients.Get(s.id);
                if (def == null) continue;
                int owned = DrinkLab.Owned(s.id);
                bool lack = shortages.ContainsKey(s.id);
                string right = $"{DrinkRules.Format(s.amount, def.form)} / 보유 {DrinkRules.Format(owned, def.form)}";
                costParts.Add(def.name + " " + right + (lack ? " 부족" : ""));
                if (costExpanded || lack)
                    Add(new RowSpec { kind = RowKind.Line, name = "Cost_" + s.id + (lack ? "_short" : ""), h = 70f, size = DrinkPaperUi.Small,
                        left = (lack ? $"<color=#a8241a>{def.name} — {DrinkRules.Format(shortages[s.id], def.form)} 부족</color>" : def.name) + $"\n필요 {DrinkRules.Format(s.amount, def.form)} · 보유 {DrinkRules.Format(owned, def.form)}" });
                if (lack && costExpanded) Add(new RowSpec { kind = RowKind.Line, name = "CostSrc_" + s.id, left = "획득처: " + DrinkLab.SourceText(def.id), color = Muted, h = 98f, size = DrinkPaperUi.Small - 2 });
            }
            costLine = costParts.Count == 0 ? "—" : string.Join("\n", costParts);
            mixLine = string.Join(", ", costParts);
            topology = kind + "/" + topo;
            return specs;
        }

        void Rebuild(List<RowSpec> specs)
        {
            var scroll = mixContent.GetComponentInParent<ScrollRect>();
            float keepY = lastTopology != null ? mixContent.anchoredPosition.y : 0f; // 위에서부터 내려간 픽셀(처음엔 맨 위)
            foreach (Transform c in mixContent) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            leftTexts.Clear(); rightTexts.Clear(); techButtons.Clear();
            float y = 6f;
            foreach (var s in specs)
            {
                switch (s.kind)
                {
                    case RowKind.Header:
                        if (y > 10f) y += 10f;
                        leftTexts[s.name] = UiFactory.AddText(UiFactory.Place(s.name, mixContent, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(230f, 48f)), s.left, DrinkPaperUi.Heading, TextAnchor.MiddleLeft, Gold);
                        if (s.right != null)
                            rightTexts[s.name] = UiFactory.AddText(UiFactory.Place(s.name + "_Right", mixContent, new Vector2(0f, 1f), new Vector2(238f, -y), new Vector2(RowW - 230f, 48f)), s.right, DrinkPaperUi.Small, TextAnchor.MiddleRight, Muted);
                        // 영수증 점선 구분
                        UiFactory.AddImage(UiFactory.Place(s.name + "_Rule", mixContent, new Vector2(0f, 1f), new Vector2(8f, -y - 50f), new Vector2(RowW, 2f)), new Color(0.42f, 0.33f, 0.25f, 0.35f)).raycastTarget = false;
                        y += 56f;
                        break;
                    case RowKind.Line:
                    {
                        if (s.remove != null || s.minus != null || s.plus != null) s.h = Mathf.Max(s.h, 66f); // 누르기 영역 720p 44px
                        // 여러 줄 글자(소모·계량 중 줄)는 줄 수만큼 높이를 잡아 둘째 줄이 잘리지 않는다(한글 글꼴 줄 간격 ≈ 1.5배).
                        int lines = 1;
                        foreach (char ch in s.left ?? "") if (ch == '\n') lines++;
                        s.h = Mathf.Max(s.h, lines * s.size * 1.5f + 12f);
                        var row = UiFactory.Place(s.name, mixContent, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(RowW, s.h));
                        var l = UiFactory.AddText(UiFactory.Place("L", row, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(RowW * (s.right != null ? 0.62f : 0.98f), s.h)), s.left, s.size, TextAnchor.MiddleLeft, s.color);
                        l.supportRichText = true;
                        l.verticalOverflow = VerticalWrapMode.Truncate;
                        l.horizontalOverflow = HorizontalWrapMode.Wrap;
                        leftTexts[s.name] = l;
                        // 78 획득처 줄(상한·희귀 후보 문구로 길어짐): 잘리지(Truncate) 않게 글 높이만큼 행을 늘린다.
                        if (s.name.StartsWith("CostSrc_", System.StringComparison.Ordinal) && l.preferredHeight + 8f > s.h)
                        {
                            s.h = Mathf.Ceil(l.preferredHeight + 8f);
                            row.sizeDelta = new Vector2(RowW, s.h);
                            l.rectTransform.sizeDelta = new Vector2(l.rectTransform.sizeDelta.x, s.h);
                        }
                        // 값 칸은 행 끝 버튼(× 또는 −/+) 폭만큼 안쪽에서 끝난다(겹침 없음).
                        float inset = s.remove != null ? 70f : s.minus != null || s.plus != null ? 144f : 0f;
                        if (s.right != null) rightTexts[s.name] = UiFactory.AddText(UiFactory.Place("R", row, new Vector2(1f, 0.5f), new Vector2(-8f - inset, 0f), new Vector2(RowW * 0.36f - inset * 0.5f, s.h)), s.right, s.size, TextAnchor.MiddleRight, s.color == DrinkPaperUi.InkLive ? DrinkPaperUi.InkLive : DrinkPaperUi.Ink);
                        if (s.remove != null) SmallButton(row, "Remove", -2f, "×", s.remove);
                        if (s.minus != null) SmallButton(row, s.name + "Minus", -74f, "−", s.minus);
                        if (s.plus != null) SmallButton(row, s.name + "Plus", -2f, "+", s.plus);
                        y += s.h + 2f;
                        break;
                    }
                    case RowKind.Techs:
                    {
                        if (kind == DrinkKind.Coffee)
                        {
                            // 추출 도구 3종(한 줄 3칸). 고른 도구 = 선택 표식 아트. 버튼 이름 "Method" + 도구.
                            var methods = CoffeeBrewing.All;
                            float mw = (RowW - (methods.Length - 1) * 8f) / methods.Length;
                            for (int i = 0; i < methods.Length; i++)
                            {
                                var mm = methods[i];
                                bool on = mm == draft.coffeeMethod;
                                DrinkPaperUi.Button(mixContent, "Method" + mm, new Vector2(0f, 1f), new Vector2(8f + i * (mw + 8f), -y), new Vector2(mw, 66f), CoffeeBrewing.DisplayName(mm), DrinkPaperUi.Small,
                                    () => SetCoffeeMethod(mm), on ? "button_equipped" : "button", on ? new Color(1f, 0.85f, 0.6f) : (Color?)null);
                            }
                            y += 76f;
                            break;
                        }
                        var techs = kind == DrinkKind.Tea ? new[] { DrinkTechnique.Steep } : new[] { DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer };
                        // 2열 버튼(720p에서 글자 18px 이상·누르기 44px 이상). 고른 기법 = 선택 표식 아트.
                        int cols = Mathf.Min(2, techs.Length);
                        float bw = (RowW - (cols - 1) * 8f) / cols;
                        for (int i = 0; i < techs.Length; i++)
                        {
                            var t = techs[i];
                            bool on = (int)t == draft.technique;
                            techButtons[t] = DrinkPaperUi.Button(mixContent, "Tech" + t, new Vector2(0f, 1f), new Vector2(8f + (i % cols) * (bw + 8f), -y - (i / cols) * 72f), new Vector2(bw, 66f), DrinkRules.TechniqueName(t), DrinkPaperUi.Body,
                                () => SetTechnique(t), on ? "button_equipped" : "button", on ? new Color(1f, 0.85f, 0.6f) : (Color?)null);
                        }
                        y += ((techs.Length + cols - 1) / cols) * 72f + 4f;
                        break;
                    }
                    case RowKind.Toggle:
                    {
                        y += 10f;
                        var b = DrinkPaperUi.Button(mixContent, s.name, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(RowW, 66f), s.left, DrinkPaperUi.Small, () => { costExpanded = !costExpanded; RefreshMix(); });
                        var label = b.GetComponentInChildren<Text>();
                        label.alignment = TextAnchor.MiddleLeft;
                        leftTexts[s.name] = label;
                        y += 72f;
                        break;
                    }
                }
            }
            mixContent.sizeDelta = new Vector2(0f, y + 28f); // 마지막 줄 아래 여백(끝까지 스크롤하면 마지막 줄이 가리지 않음)
            if (scroll != null)
            {
                scroll.StopMovement();
                float viewH = ((RectTransform)scroll.transform).rect.height;
                mixContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(keepY, 0f, Mathf.Max(0f, mixContent.sizeDelta.y - viewH)));
            }
            PanelRebuilds++;
        }

        Button SmallButton(Transform parent, string name, float x, string label, Action onClick) =>
            DrinkPaperUi.Button(parent, name, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(66f, 66f), label, DrinkPaperUi.Body, onClick); // 720p 44px

        void RefreshMix()
        {
            if (mixContent == null) return;
            var prepared = DrinkLab.Prepared;
            preparedText.text = prepared != null
                ? $"준비된 음료: {prepared.recipe.name}\n이 음료로 출발합니다(새로 만들 수 없음)"
                : "준비된 음료 없음\n만들지 않고 출발해도 됩니다";
            var shown = Projected(); // 표시는 계량 중인 양까지 포함한 예상값, 조작은 this.draft에
            int pending = stage != null ? stage.InMeasure : 0;
            var specs = BuildSpecs(shown, pending, out var topology);
            if (topology != lastTopology) { Rebuild(specs); lastTopology = topology; }
            else
                foreach (var s in specs)
                {
                    if (s.left != null && leftTexts.TryGetValue(s.name, out var l) && l.text != s.left) l.text = s.left;
                    if (s.right != null && rightTexts.TryGetValue(s.name, out var r) && r.text != s.right) r.text = s.right;
                }

            var shortages = DrinkValidator.Shortages(shown, DrinkLab.Owned);
            var check = DrinkValidator.Check(shown);
            bool busy = stage != null && (stage.SequencePlaying || stage.Pouring || stage.Transferring);
            craftButton.interactable = prepared == null && check.Ok && shortages.Count == 0 && !busy && DrinkValidator.Check(draft).Ok;
            saveButton.interactable = check.Ok && !busy;
            footerStatus.text = prepared != null ? "<color=#1f6b2e>준비된 음료가 있어 새로 만들 수 없습니다</color>"
                : busy ? "붓는 중… 다 옮겨 부은 뒤 제조할 수 있습니다"
                : shown.steps.Count == 0 ? "재료를 넣으면 제조할 수 있습니다"
                : !check.Ok ? $"<color=#a8241a>{check.First}</color>"
                : shortages.Count > 0 ? $"<color=#a8241a>재료 {shortages.Count}종 부족 — 소모 목록에서 획득처 확인</color>"
                : $"재료 {shown.steps.Count}종을 사용합니다";
            var skip = body.Find("MixPanel/SkipMotion");
            if (skip != null) skip.GetComponentInChildren<Text>().text = skipMotion ? "모션 생략" : "모션 보기";
        }

        /// <summary>붓기·옮겨 붓기·제조 모션 중에는 배합을 바꾸는 조작을 막는다(계량 중인 양이 확정되고 도구가 돌아온 뒤 가능).</summary>
        bool Busy(string what)
        {
            if (stage == null || !(stage.Pouring || stage.Transferring || stage.SequencePlaying)) return false;
            Message($"붓는 중에는 {what}할 수 없습니다 — 옮겨 붓기가 끝난 뒤 다시 시도하세요.");
            return true;
        }

        /// <summary>사용자용 완성 모습 설명(내부 색 코드 없이).</summary>
        static string LookWords(DrinkLook look)
        {
            var c = look.color;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            string hue = max - min < 0.08f ? (c.a < 0.45f ? "무색" : "옅은 색") :
                c.r >= c.g && c.r >= c.b ? (c.g > c.b + 0.15f ? "주황·호박색" : c.b > c.g + 0.1f ? "보랏빛 붉은색" : "붉은색") :
                c.b >= c.r && c.b >= c.g ? (c.r > c.g + 0.05f ? "보랏빛" : "푸른색") : "초록빛";
            string clarity = look.turbidity > 0.25f ? "뿌옇게 탁함" : c.a < 0.5f ? "맑고 투명" : "선명";
            return look.boundaries > 0 ? $"{hue} · {look.layers.Count}개 층" : $"{hue} · {clarity}";
        }

        /// <summary>커피 완성 모습(농도·도구).</summary>
        static string CoffeeLookWords(DrinkRecipe r)
        {
            float conc = r.water > 0 ? r.LeafTotal() / (float)r.water : 0f;
            string tone = conc >= 0.3f ? "짙은 흑갈색" : conc >= 0.1f ? "진한 갈색" : conc >= 0.05f ? "맑은 호박빛 갈색" : "연한 갈색";
            string top = r.coffeeMethod switch
            {
                CoffeeBrewMethod.EspressoMachine => "크레마",
                CoffeeBrewMethod.MokaPot => "묵직한 바디",
                _ => "깨끗한 뒷맛",
            };
            return $"{tone} · {top}";
        }

        public void ConfirmCraft()
        {
            if (Busy("제조")) return;
            if (string.IsNullOrWhiteSpace(draft.name) && nameField != null) draft.name = nameField.text;
            var result = DrinkLab.Craft(draft);
            switch (result.Status)
            {
                case DrinkLab.CraftStatus.Crafted:
                    Message($"제조 완료 — {DrinkLab.Prepared.recipe.name}: {result.Buff.Describe()}");
                    var made = DrinkLab.Prepared.recipe.Clone();
                    BuildIngredientRows();
                    craftButton.interactable = false;
                    stage.PlayCraft(made, skipMotion, () => { RefreshMix(); });
                    break;
                case DrinkLab.CraftStatus.Shortage:
                    Message("재료가 부족합니다 — 부족한 재료는 도감에서 획득처를 확인하세요.");
                    break;
                default:
                    Message(result.Message);
                    break;
            }
            RefreshMix();
        }

        void SaveDraftAsRecipe()
        {
            if (Busy("레시피 저장")) return;
            var r = draft.Clone();
            r.name = nameField != null ? nameField.text : "";
            if (DrinkLab.SaveRecipe(r, editingRecipeIndex, out var error))
            {
                Message(editingRecipeIndex >= 0 ? "레시피를 수정했습니다." : "레시피를 저장해 레시피 서랍에 꽂았습니다(재료 소모 없음).");
                // 저장 트랜잭션이 성공한 뒤에만: 노트에서 한 장 찢어 보관함에 넣는 장식 모션(데이터 변화 없음, 클릭하면 끝 상태로)
                string shown = string.IsNullOrWhiteSpace(r.name) ? DrinkLab.Recipes[DrinkLab.Recipes.Count - 1].name : r.name;
                if (editingRecipeIndex >= 0 && editingRecipeIndex < DrinkLab.Recipes.Count) shown = DrinkLab.Recipes[editingRecipeIndex].name;
                RecipeTearOverlay.Play(root, shown, tabButtons.TryGetValue(Tab.Recipes, out var rb) ? (RectTransform)rb.transform : null); // 찢은 종이 → 레시피 서랍
            }
            else Message(error);
        }

        // ═════ 도감 탭 ═════

        void OpenCodexFor(DrinkIngredientDef def)
        {
            SetTab(Tab.Codex);
            BuildCodex(def);
        }

        void BuildCodex(DrinkIngredientDef focus)
        {
            // 73 단계2: 재료 종이 카드 3열(본문 기준 27 = 720p 18px). 실제 색은 색 사각형 대신 작은 실제 잔(같은 액체 렌더러)과 글로 보인다.
            foreach (Transform c in body) Destroy(c.gameObject);
            float listW = layoutW - 60f;
            var listRt = UiFactory.Place("CodexList", body, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(listW, 940f));
            var content = MakeScroll(listRt);
            int cols = listW >= 1800f ? 3 : 2; // 좁은 창은 2열(카드 폭을 줄여 글자를 줄이지 않는다)
            const float cardH = 660f, gap = 12f; // 본문 최대 11줄 + 머리(78: 획득처에 판당 상한·희귀 후보 문구가 늘어 610→660)
            float cardW = (listW - 30f - (cols + 1) * gap) / cols;
            float y = 0f, focusY = -1f;
            int col = 0;
            foreach (var def in DrinkIngredients.All)
            {
                bool seen = DrinkLab.Seen(def.id);
                int owned = DrinkLab.Owned(def.id);
                bool leaf = def.form == IngredientForm.Leaf;
                if (focus == def) focusY = y;
                var card = UiFactory.Place("Codex_" + def.id, content, new Vector2(0f, 1f), new Vector2(gap + col * (cardW + gap), -y), new Vector2(cardW, cardH));
                DrinkPaperUi.Sliced(card, "ingredient_card", 3f, focus == def ? new Color(1f, 0.86f, 0.62f) : seen ? Color.white : new Color(0.75f, 0.73f, 0.7f));
                var icon = UiFactory.Place("Icon", card, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(96f, 96f));
                var img = icon.gameObject.AddComponent<Image>(); img.sprite = DrinkArt.SpriteOrPlaceholder(def.iconId, out _); img.preserveAspect = true; img.raycastTarget = false;
                if (!seen) img.color = new Color(0.4f, 0.4f, 0.4f);
                UiFactory.AddText(UiFactory.Place("Name", card, new Vector2(0f, 1f), new Vector2(150f, -40f), new Vector2(300f, 44f)), def.name, DrinkPaperUi.Heading, TextAnchor.MiddleLeft, seen ? DrinkPaperUi.Ink : DrinkPaperUi.InkMuted);
                UiFactory.AddText(UiFactory.Place("Status", card, new Vector2(0f, 1f), new Vector2(150f, -88f), new Vector2(300f, 40f)),
                    owned > 0 ? $"보유 {DrinkRules.Format(owned, def.form)}" : seen ? "보유 0" : "아직 없음(잠김)", DrinkPaperUi.Small, TextAnchor.MiddleLeft, owned > 0 ? DrinkPaperUi.InkGood : DrinkPaperUi.InkBad);
                // 실제 색: 액체는 작은 잔에 그 재료만, 찻잎은 우린 차를 찻잔에(같은 물리·색 렌더러, 색 사각형 없음)
                bool bean = def.id == DrinkIngredients.CoffeeBean;
                bool coffeeOnly = def.use == DrinkUse.Coffee;
                var probe = bean ? DrinkRecipe.NewDraft(DrinkKind.Coffee, CoffeeBrewMethod.PourOver)
                    : DrinkRecipe.NewDraft(coffeeOnly ? DrinkKind.Coffee : leaf ? DrinkKind.Tea : DrinkKind.Cocktail);
                if (coffeeOnly && !bean) probe.water = 0; // 재료 자체의 색(커피 없이)
                probe.SetAmount(def.id, bean ? 150 : leaf ? 20 : 300);
                var glassRoot = UiFactory.Place("ColorGlass", card, new Vector2(1f, 1f), new Vector2(-44f, -36f), new Vector2(110f, 120f));
                glassRoot.pivot = new Vector2(1f, 1f);
                var glass = DrinkVesselView.Create(glassRoot, leaf ? "dl_teacup" : "dl_glass_cocktail", new Vector2(0f, -60f), 60f, 2);
                if (bean) glass.SetLiquid(CoffeeBrewing.BrewColor(probe), 40f, def.turbidity);
                else if (leaf) glass.SetLiquid(DrinkPhysicsSim.TeaColor(probe), 40f, def.turbidity);
                else glass.SetLiquid(DrinkCalculator.IngredientLook(def), 40f, def.turbidity, def.density);
                string colorWords = bean ? "핸드 드립: " + CoffeeLookWords(probe) : (leaf ? "우린 차: " : "실제 색: ") + LookWords(DrinkCalculator.Look(probe));
                string text =
                    $"{def.taste}\n" +
                    $"{colorWords}\n" +
                    $"사용: {def.UseLabel} · 단위 {def.Unit}\n" +
                    $"게임 효과: {DrinkRules.StatName(def.effect)} (상한 {DrinkBuff.Pct(def.effect, DrinkRules.Cap(def.effect))})\n" +
                    $"착색 {(def.tint > 2f ? "강함" : def.tint > 0.5f ? "보통" : "약함")} · 탁도 {(def.turbidity > 0.3f ? "높음" : def.turbidity > 0.05f ? "약간" : "맑음")} · 밀도 {def.density:0.00}\n" +
                    $"획득처: {DrinkLab.SourceText(def.id)}" + (seen ? "" : "\n아직 보유한 적이 없어 제조에 쓸 수 없습니다.");
                var t = UiFactory.AddText(UiFactory.Place("Body", card, new Vector2(0f, 1f), new Vector2(40f, -164f), new Vector2(cardW - 80f, cardH - 210f)), text, DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
                t.lineSpacing = 1.05f;
                if (++col == cols) { col = 0; y += cardH + gap; }
            }
            if (col != 0) y += cardH + gap;
            var aux = UiFactory.Place("Aux", content, new Vector2(0f, 1f), new Vector2(gap, -y), new Vector2(listW - 40f, 90f));
            UiFactory.AddText(aux, "물·기본 얼음은 무료 제조 보조 자원입니다(배합에 기록). 물은 색을 희석하고 버프 기여가 없습니다.", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 1f));
            y += 100f;
            // 78 공급 규칙(일반 상한·출처 구분·희귀): 같은 안내 칸 형식으로 한 줄 더(새 화면 없음). 좁은 폭에서도 잘리지 않게 높이 여유.
            var supply = UiFactory.Place("SupplyRule", content, new Vector2(0f, 1f), new Vector2(gap, -y), new Vector2(listW - 40f, 150f));
            UiFactory.AddText(supply, DrinkLab.SupplyRuleText(), DrinkPaperUi.Small, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));
            y += 60f;
            content.sizeDelta = new Vector2(0f, y + 100f);
            if (focusY > 0f) content.anchoredPosition = new Vector2(0f, focusY); // 획득처 보기로 연 재료가 보이게
        }

        // ═════ 72 §6 주문·판매 탭 ═════

        void BuildOrders()
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            float listW = layoutW - 60f;
            var listRt = UiFactory.Place("OrdersList", body, new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(listW, 940f));
            var content = MakeScroll(listRt);
            bool twoCols = listW >= 1800f;
            float colW = twoCols ? (listW - 60f) / 2f : listW - 30f;
            float leftX = 10f, rightX = twoCols ? colW + 40f : 10f;
            float y = 0f;
            var prepared = DrinkLab.Prepared;
            // 준비 음료(완성 음료 1잔): 출발용 사용 또는 주문 판매(중복 불가) 또는 명시 폐기
            var pr = UiFactory.Place("PreparedCard", content, new Vector2(0f, 1f), new Vector2(leftX, -y), new Vector2(colW, 186f));
            DrinkPaperUi.Sliced(pr, "torn_recipe", 3f);
            string pl = prepared == null ? "준비 음료 없음 — 제조 탭에서 만든 음료를 팔 수 있습니다."
                : prepared.state == PreparedDrink.InRun ? $"{prepared.recipe.name} — 이번 판에 사용 중(판매 불가)"
                : $"{prepared.recipe.name} · {DrinkRules.KindName(prepared.recipe.Kind)} · {DrinkRules.TechniqueName(prepared.recipe.Technique)} · {DrinkSales.FamilyOf(prepared.recipe)}\n{RecipeLine(prepared.recipe)}";
            var pt = UiFactory.AddText(UiFactory.Place("PreparedInfo", pr, new Vector2(0f, 1f), new Vector2(36f, -40f), new Vector2(colW - 330f, 120f)), pl, DrinkPaperUi.Small, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
            pt.verticalOverflow = VerticalWrapMode.Truncate;
            var discard = DrinkPaperUi.Button(pr, "DiscardPrepared", new Vector2(1f, 0.5f), new Vector2(-30f, -6f), new Vector2(260f, 66f), "준비 음료 버리기", DrinkPaperUi.Small,
                () => { if (DrinkLab.DiscardPrepared(out var e)) Message("준비 음료를 버렸습니다(재료 환불 없음)."); else Message(e); SetTab(Tab.Orders); }, tint: new Color(1f, 0.8f, 0.75f));
            discard.interactable = prepared != null && prepared.state == PreparedDrink.Ready;
            y += 196f;
            var orders = DrinkLab.Orders;
            for (int i = 0; i < orders.Count; i++)
            {
                int index = i;
                var o = orders[i];
                var row = UiFactory.Place("Order_" + i, content, new Vector2(0f, 1f), new Vector2(leftX, -y), new Vector2(colW, 236f));
                DrinkPaperUi.Sliced(row, "receipt", 2f);
                string text;
                int price = 0;
                if (!o.IsOpen) text = $"주문 {i + 1} · 취소됨\n다음 판이 끝나면 새 주문이 들어옵니다.";
                else
                {
                    var r = prepared != null && prepared.state == PreparedDrink.Ready ? prepared.recipe : null;
                    price = DrinkSales.Price(o, r);
                    string Mark(bool ok) => r == null ? "·" : ok ? "✓" : "✗";
                    var a = DrinkIngredients.Get(o.ingA); var b = DrinkIngredients.Get(o.ingB);
                    text = $"주문 {i + 1} · {DrinkRules.KindName((DrinkKind)o.kind)}\n" +
                           $"{Mark(r != null && (int)r.Kind == o.kind && r.AmountOf(o.ingA) >= o.minA)} {a?.name} {DrinkRules.Format(o.minA, a != null ? a.form : IngredientForm.Liquid)} 이상   " +
                           $"{Mark(r != null && (int)r.Kind == o.kind && r.AmountOf(o.ingB) >= o.minB)} {b?.name} {DrinkRules.Format(o.minB, b != null ? b.form : IngredientForm.Liquid)} 이상\n" +
                           $"보너스: {Mark(DrinkSales.TechniqueMatches(o, r))} {DrinkRules.TechniqueName((DrinkTechnique)o.technique)} +{DrinkSales.TechniqueBonus} · {Mark(DrinkSales.ColorMatches(o, r))} {o.colorFamily} +{DrinkSales.ColorBonus}";
                }
                var t = UiFactory.AddText(UiFactory.Place("Info", row, new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(colW - 340f, 170f)), text, DrinkPaperUi.Small, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
                t.verticalOverflow = VerticalWrapMode.Truncate;
                var sell = DrinkPaperUi.Button(row, "Sell", new Vector2(1f, 1f), new Vector2(-36f, -40f), new Vector2(260f, 66f), price > 0 ? $"판매 ({price}코인)" : "판매", DrinkPaperUi.Small,
                    () => { if (DrinkLab.Sell(index, out var got, out var e)) Message($"판매했습니다: +{got}코인"); else Message(e); SetTab(Tab.Orders); }, price > 0 ? "button_available" : "button", price > 0 ? new Color(1f, 0.85f, 0.6f) : (Color?)null);
                sell.interactable = price > 0;
                var cancel = DrinkPaperUi.Button(row, "CancelOrder", new Vector2(1f, 1f), new Vector2(-36f, -116f), new Vector2(260f, 66f), "주문 취소", DrinkPaperUi.Small,
                    () => { if (DrinkLab.CancelOrder(index, out var e)) Message("주문을 취소했습니다(다음 판 종료 후 새 주문)."); else Message(e); SetTab(Tab.Orders); });
                cancel.interactable = o.IsOpen;
                y += 246f;
            }
            float leftEnd = y;
            // 외형 상점(판매 코인 전용, 기존 준비된 외형만)
            float ry = twoCols ? 0f : y + 10f;
            UiFactory.AddText(UiFactory.Place("CoinTitle", content, new Vector2(0f, 1f), new Vector2(rightX, -ry), new Vector2(colW, 50f)),
                $"판매 코인 {DrinkLab.Coins} — 외형 구매 전용(연구 포인트·스페셜티·재료와 바꾸지 않음)", DrinkPaperUi.Body, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            ry += 58f;
            foreach (var c in DrinkSales.All)
            {
                var cc = c;
                bool cash = c.kind == "skin" && SafeCashOwned(c.id);
                bool owned = cash || DrinkLab.CoinOwns(c.id);
                bool equipped = c.kind == "map" ? DrinkLab.EquippedMap == c.id : SafeSkinEquipped(c.id);
                bool onSale = DrinkSales.OnSale(c);
                var card = UiFactory.Place("Cosmetic_" + c.id, content, new Vector2(0f, 1f), new Vector2(rightX, -ry), new Vector2(colW, 196f));
                DrinkPaperUi.Sliced(card, "ingredient_card", 2f, owned ? new Color(1f, 0.95f, 0.85f) : Color.white);
                // 79: 스킨은 몸체 미리보기(아트 준비 전이면 비움). 12종+맵이 이어져도 같은 스크롤 목록 안에서 읽힌다.
                float tx = 36f;
                if (c.kind == "skin")
                {
                    var def = SkinCatalog.Find(c.id);
                    var img = UiFactory.AddImage(UiFactory.Place("Preview", card, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(120f, 170f)), Color.white);
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    Sprite sp = null;
                    try { sp = def != null && onSale ? SkinShopView.BodyPreview(SkinArt.PreviewSprite(def)) : null; } catch (Exception) { }
                    img.sprite = sp;
                    img.enabled = sp != null;
                    tx = 156f;
                }
                UiFactory.AddText(UiFactory.Place("Name", card, new Vector2(0f, 1f), new Vector2(tx, -30f), new Vector2(colW - 304f - tx, 40f)), c.name, DrinkPaperUi.Body, TextAnchor.MiddleLeft, DrinkPaperUi.Ink);
                string status = owned ? (cash ? "보유(결제 구매)" : "보유(코인 구매)") : !onSale ? "아트 준비 중 · 아직 판매 안 함" : $"{c.price}코인";
                var d = UiFactory.AddText(UiFactory.Place("Desc", card, new Vector2(0f, 1f), new Vector2(tx, -74f), new Vector2(colW - 304f - tx, 100f)),
                    $"{c.description}\n{status}", DrinkPaperUi.Small, TextAnchor.UpperLeft, DrinkPaperUi.InkMuted);
                d.verticalOverflow = VerticalWrapMode.Truncate;
                if (!owned)
                {
                    var buy = DrinkPaperUi.Button(card, "Buy", new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(260f, 66f), onSale ? $"구매 ({c.price}코인)" : "준비 중", DrinkPaperUi.Small,
                        () => { if (DrinkLab.BuyCosmetic(cc.id, SafeCashOwned, out var e)) Message($"{cc.name}을(를) 샀습니다."); else Message(e); SetTab(Tab.Orders); }, onSale && DrinkLab.Coins >= c.price ? "button_available" : "button_locked");
                    buy.interactable = onSale && DrinkLab.Coins >= c.price;
                }
                else
                {
                    DrinkPaperUi.Button(card, "EquipCosmetic", new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(260f, 66f), equipped ? "장착 해제" : "장착", DrinkPaperUi.Small,
                        () => { Message(ToggleCosmetic(cc, equipped)); SetTab(Tab.Orders); }, equipped ? "button_equipped" : "button_owned");
                }
                ry += 206f;
            }
            content.sizeDelta = new Vector2(0f, Mathf.Max(leftEnd, ry) + 20f);
        }

        static bool SafeCashOwned(string id) { try { return SkinCommerce.Shared.IsCashOwned(id); } catch (Exception) { return false; } }
        static bool SafeSkinEquipped(string id) { try { return SkinCommerce.Shared.IsEquipped(id); } catch (Exception) { return false; } }

        static string ToggleCosmetic(CoinCosmetic c, bool equipped)
        {
            if (c.kind == "map") return DrinkLab.EquipMap(equipped ? "" : c.id, out var e) ? (equipped ? "직업 기본 맵으로 되돌렸습니다." : $"{c.name} 배경을 장착했습니다(배경만 바뀜).") : e;
            try
            {
                var def = SkinCatalog.Find(c.id);
                bool ok = equipped ? SkinCommerce.Shared.Unequip(def.job) : SkinCommerce.Shared.Equip(c.id);
                return ok ? (equipped ? "외형을 해제했습니다." : $"{c.name} 외형을 장착했습니다.") : "장착 정보를 저장하지 못했습니다.";
            }
            catch (Exception) { return "외형 장착을 처리하지 못했습니다."; }
        }

        // ═════ 레시피 탭 ═════

        bool drawerOpen, importOpen;
        RecipeDrawerView drawer;
        RectTransform importPanel;
        public RecipeDrawerView Drawer => drawer;

        void BuildRecipes()
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            // 넓은 창: 왼쪽 = 레시피 서랍(정수 2배), 오른쪽 = 기본 예시 2장 + 공유 코드 가져오기.
            // 좁은 창: 위 = 예시 불러오기 2 + 가져오기 열기 버튼, 아래 = 서랍. 가져오기는 버튼으로 여는 판(서랍 위).
            var presets = DrinkLab.ExamplePresets;
            float leftW = narrow ? layoutW - 60f : 1100f;
            float top = 10f;
            if (narrow)
            {
                float bw = (leftW - 24f) / 3f;
                for (int i = 0; i < presets.Count && i < 2; i++)
                {
                    int index = i;
                    DrinkPaperUi.Button(body, "Example_" + i, new Vector2(0f, 1f), new Vector2(30f + i * (bw + 12f), -top), new Vector2(bw, 66f), $"{presets[i].name} 불러오기", DrinkPaperUi.Small, () => LoadPreset(index));
                }
                DrinkPaperUi.Button(body, "ImportToggle", new Vector2(0f, 1f), new Vector2(30f + 2f * (bw + 12f), -top), new Vector2(bw, 66f), importOpen ? "가져오기 닫기" : "공유 코드 가져오기", DrinkPaperUi.Small,
                    () => { importOpen = !importOpen; SetTab(Tab.Recipes); });
                top += 76f;
            }
            var area = UiFactory.Place("RecipeDrawer", body, new Vector2(0f, 1f), new Vector2(30f, -top), new Vector2(leftW, 930f - top));
            drawer = RecipeDrawerView.Create(area, () => DrinkLab.Recipes, BuildRecipeSheet, drawerOpen, Message);
            if (!narrow)
            {
                float rx = 30f, ry = 6f;
                UiFactory.AddText(UiFactory.Place("Count", body, new Vector2(1f, 1f), new Vector2(-rx, -ry), new Vector2(700f, 40f)), $"서랍 속 레시피 {DrinkLab.Recipes.Count} / {DrinkRules.RecipeLimit}  ·  기본 예시(불러오기만, 재료 소모 없음)", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
                ry += 46f;
                for (int i = 0; i < presets.Count; i++)
                {
                    int index = i;
                    var r = presets[i];
                    var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
                    var row = UiFactory.Place("Example_" + i, body, new Vector2(1f, 1f), new Vector2(-rx, -ry), new Vector2(700f, 116f));
                    DrinkPaperUi.Sliced(row, "torn_recipe", 2f, new Color(0.9f, 0.96f, 0.86f));
                    var info = UiFactory.AddText(UiFactory.Place("Info", row, new Vector2(0f, 1f), new Vector2(28f, -16f), new Vector2(430f, 88f)),
                        $"{r.name}\n{RecipeLine(r)}" + (shortages.Count > 0 ? "  <color=#a8241a>재료 부족</color>" : ""), DrinkPaperUi.Small, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
                    info.supportRichText = true;
                    info.verticalOverflow = VerticalWrapMode.Truncate;
                    DrinkPaperUi.Button(row, "UseExample", new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(200f, 66f), "불러오기", DrinkPaperUi.Small, () => LoadPreset(index));
                    ry += 124f;
                }
            }

            // 가져오기
            var imp = narrow ? UiFactory.Place("Import", body, new Vector2(0f, 0f), new Vector2(30f, 16f), new Vector2(layoutW - 60f, 410f))
                             : UiFactory.Place("Import", body, new Vector2(1f, 1f), new Vector2(-30f, -306f), new Vector2(700f, 600f));
            importPanel = imp;
            UiFactory.AddImage(imp, new Color(0.1f, 0.08f, 0.12f, 0.92f));
            UiFactory.AddText(UiFactory.Place("Title", imp, new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(660f, 48f)), "공유 코드 가져오기", DrinkPaperUi.Heading, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            UiFactory.AddText(UiFactory.Place("Help", imp, new Vector2(0f, 1f), new Vector2(16f, -54f), new Vector2(660f, 64f)), "코드(CCT1-…)를 붙여 넣고 확인 → 미리보기 후 저장. 창고·버프는 바뀌지 않고, 제조 때 내 재료를 씁니다.", DrinkPaperUi.Small - 2, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.9f));
            importField = MakeInput(imp, "Code", new Vector2(0f, 1f), new Vector2(16f, -124f), new Vector2(668f, 116f), "CCT1-…", DrinkPaperUi.Small - 2);
            importField.lineType = InputField.LineType.MultiLineNewline;
            importField.characterLimit = DrinkShareCode.MaxPasteLength;
            DrinkPaperUi.Button(imp, "Paste", new Vector2(0f, 1f), new Vector2(16f, -250f), new Vector2(210f, 66f), "붙여넣기", DrinkPaperUi.Small, () => importField.text = GUIUtility.systemCopyBuffer ?? "");
            DrinkPaperUi.Button(imp, "Check", new Vector2(0f, 1f), new Vector2(234f, -250f), new Vector2(210f, 66f), "코드 확인", DrinkPaperUi.Small, () => PreviewImport(importField.text));
            DrinkPaperUi.Button(imp, "SaveImport", new Vector2(0f, 1f), new Vector2(452f, -250f), new Vector2(232f, 66f), "레시피로 저장", DrinkPaperUi.Small, () => SaveImport(), tint: new Color(1f, 0.85f, 0.6f));
            var prev = UiFactory.AddText(narrow ? UiFactory.Place("Preview", imp, new Vector2(0f, 1f), new Vector2(716f, -54f), new Vector2(layoutW - 60f - 740f, 340f))
                                                : UiFactory.Place("Preview", imp, new Vector2(0f, 1f), new Vector2(16f, -326f), new Vector2(668f, 262f)), "", DrinkPaperUi.Small - 2, TextAnchor.UpperLeft, Color.white);
            prev.supportRichText = true;
            prev.name = "ImportPreviewText";
            previewText = prev;
            RenderImportPreview(null);
            if (narrow && !importOpen) imp.gameObject.SetActive(false); // 좁은 창: 버튼으로 여는 판(필드·미리보기는 그대로 살아 있음)
        }

        /// <summary>서랍에서 꺼내 펼친 레시피 종이의 실제 UI(글·버튼은 종이와 함께 움직인다). 저장 실패는 종이를 그대로 두고 안내만.</summary>
        bool BuildRecipeSheet(int index, RectTransform content)
        {
            var list = DrinkLab.Recipes;
            if (index < 0 || index >= list.Count) return false;
            var r = list[index];
            var check = DrinkValidator.Check(r);
            var buff = DrinkCalculator.Buff(r); // 저장값이 아니라 현재 밸런스로 재계산
            var look = DrinkCalculator.Look(r);
            var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
            var titleRt = UiFactory.Stretch("SheetTitle", content);
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.offsetMin = new Vector2(0f, -54f); titleRt.offsetMax = Vector2.zero;
            var title = UiFactory.AddText(titleRt, r.name, DrinkPaperUi.Heading, TextAnchor.MiddleLeft, DrinkPaperUi.Ink);
            title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
            // 본문은 종이 안전 영역 안의 세로 스크롤(긴 이름·재료 8종·긴 버프도 18px 그대로 끝까지 읽힘, 종이 밖으로 넘치지 않음)
            var bodyArea = UiFactory.Stretch("SheetBody", content);
            bodyArea.offsetMin = new Vector2(0f, 160f); bodyArea.offsetMax = new Vector2(0f, -58f);
            var bodyRt = MakeScroll(bodyArea, transparent: true);
            Canvas.ForceUpdateCanvases();
            float infoW = Mathf.Max(100f, bodyArea.rect.width - 24f);
            var irt = UiFactory.Place("Info", bodyRt, new Vector2(0f, 1f), Vector2.zero, new Vector2(infoW, 10f)); // 스크롤 내용 안의 글 상자(폭 고정)
            irt.pivot = new Vector2(0f, 1f);
            var info = UiFactory.AddText(irt,
                $"{DrinkRules.KindName(r.Kind)} · {DrinkRules.TechniqueName(r.Technique)} · {(look.totalVolume > 0 ? LookWords(look) : "")}\n{RecipeLine(r)}\n버프(현재 밸런스): {buff.Describe()}" +
                (!check.Ok ? $"\n<color=#a8241a>사용 불가: {check.First}</color>" : shortages.Count > 0 ? $"\n<color=#a8241a>재료 부족: {ShortageLine(shortages)}</color>" : ""),
                DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
            info.supportRichText = true; info.horizontalOverflow = HorizontalWrapMode.Wrap; info.verticalOverflow = VerticalWrapMode.Overflow;
            float infoH = info.cachedTextGeneratorForLayout.GetPreferredHeight(info.text, info.GetGenerationSettings(new Vector2(infoW, 0f))) / info.pixelsPerUnit + 8f;
            irt.sizeDelta = new Vector2(infoW, infoH);
            bodyRt.sizeDelta = new Vector2(0f, infoH);
            // 아래 두 줄 버튼(3 × 2): 제조·수정·공유 / 복제·삭제·다시 넣기
            var row1 = new (string name, string label, Action act)[]
            {
                ("Use", "불러와 제조", () => LoadRecipe(index, false)),
                ("Edit", "수정", () => LoadRecipe(index, true)),
                ("Copy", "공유 코드 복사", () => CopyCode(index)),
            };
            var row2 = new (string name, string label, Action act)[]
            {
                ("Dup", "복제", () => { if (DrinkLab.DuplicateRecipe(index, out var e)) { Message("복제했습니다(서랍에 한 장 더 꽂음)."); SetTab(Tab.Recipes); } else Message(e); }),
                ("Delete", "삭제", () => { if (DrinkLab.DeleteRecipe(index, out var e)) { Message("서랍에서 뺐습니다."); SetTab(Tab.Recipes); } else Message(e); }),
                ("PutBack", "다시 넣기", () => drawer?.ReturnSheet()),
            };
            for (int rr = 0; rr < 2; rr++)
            {
                var rowDefs = rr == 0 ? row1 : row2;
                for (int i = 0; i < rowDefs.Length; i++)
                {
                    var d = rowDefs[i];
                    var b = DrinkPaperUi.Button(content, d.name, new Vector2(i / 3f, 0f), Vector2.zero, new Vector2(10f, 66f), d.label, DrinkPaperUi.Small, d.act,
                        tint: d.name == "Use" ? new Color(1f, 0.85f, 0.6f) : d.name == "Delete" ? new Color(1f, 0.75f, 0.7f) : (Color?)null);
                    var brt = (RectTransform)b.transform;
                    brt.anchorMin = new Vector2(i / 3f, 0f); brt.anchorMax = new Vector2((i + 1) / 3f, 0f); brt.pivot = new Vector2(0.5f, 0f);
                    brt.offsetMin = new Vector2(6f, rr == 0 ? 82f : 6f); brt.offsetMax = new Vector2(-6f, rr == 0 ? 148f : 72f);
                }
            }
            return true;
        }


        Text previewText;
        public string ImportPreviewText => previewText != null ? previewText.text : "";

        static string RecipeLine(DrinkRecipe r)
        {
            var parts = new List<string>();
            foreach (var s in r.steps)
            {
                var d = DrinkIngredients.Get(s?.id);
                parts.Add(d != null ? $"{d.name} {DrinkRules.Format(s.amount, d.form)}" : "알 수 없는 재료");
            }
            if (r.Kind == DrinkKind.Cocktail) { if (r.water > 0) parts.Add($"물 {DrinkRules.Format(r.water)}mL"); if (r.ice > 0) parts.Add($"얼음 {r.ice}"); }
            if (r.Kind == DrinkKind.Coffee) parts.Add($"{CoffeeBrewing.DisplayName(r.coffeeMethod)} {DrinkRules.Format(r.water)}mL 추출");
            return string.Join(" → ", parts);
        }

        static string ShortageLine(Dictionary<string, int> s)
        {
            var parts = new List<string>();
            foreach (var kv in s) { var d = DrinkIngredients.Get(kv.Key); if (d != null) parts.Add($"{d.name} {DrinkRules.Format(kv.Value, d.form)} (획득처: {DrinkLab.SourceText(d.id)})"); }
            return string.Join(", ", parts);
        }

        public void LoadPreset(int index)
        {
            if (Busy("불러오기")) return;
            var list = DrinkLab.ExamplePresets;
            if (index < 0 || index >= list.Count) return;
            var r = list[index].Clone();
            kind = r.Kind;
            draft = r;
            editingRecipeIndex = -1;
            selected = null;
            SetTab(Tab.Craft);
            if (nameField != null) nameField.SetTextWithoutNotify("");
            Message("예시 배합을 불러왔습니다 — 양·기법을 고친 뒤 제조 확정하거나 레시피로 저장하세요.");
        }

        public void LoadRecipe(int index, bool edit)
        {
            if (Busy("불러오기")) return;
            var list = DrinkLab.Recipes;
            if (index < 0 || index >= list.Count) return;
            var r = list[index].Clone();
            kind = r.Kind == DrinkKind.Tea || r.Kind == DrinkKind.Coffee ? r.Kind : DrinkKind.Cocktail;
            draft = r;
            editingRecipeIndex = edit ? index : -1;
            selected = null;
            SetTab(Tab.Craft);
            if (nameField != null) nameField.SetTextWithoutNotify(r.name);
            var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
            Message(shortages.Count > 0 ? "재료가 부족합니다(자동 대체·차감 없음): " + ShortageLine(shortages) : edit ? "레시피 수정 중 — 저장하면 덮어씁니다." : "레시피를 불러왔습니다. 확인 후 제조 확정을 누르세요.");
        }

        public string CopyCode(int index)
        {
            var list = DrinkLab.Recipes;
            if (index < 0 || index >= list.Count) return null;
            var code = DrinkShareCode.Encode(list[index]);
            GUIUtility.systemCopyBuffer = code;
            Message($"\"{list[index].name}\" 공유 코드를 클립보드에 복사했습니다(붙여넣기로 전달).");
            return code;
        }

        public bool PreviewImport(string code)
        {
            importPreview = DrinkShareCode.Decode(code, out var error);
            RenderImportPreview(error);
            return importPreview != null;
        }

        void RenderImportPreview(string error)
        {
            if (previewText == null) return;
            if (importPreview == null) { previewText.text = error != null ? $"<color=#ff8a80>가져올 수 없습니다: {error}</color>" : "코드를 확인하면 여기에서 배합·색·버프를 미리 봅니다."; return; }
            var r = importPreview;
            var look = DrinkCalculator.Look(r);
            var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
            previewText.text = $"{r.name} · {DrinkRules.KindName(r.Kind)} · {DrinkRules.TechniqueName(r.Technique)}\n{RecipeLine(r)}\n" +
                               $"완성 모습: {LookWords(look)}\n" +
                               $"버프(현재 밸런스로 계산): {DrinkCalculator.Buff(r).Describe()}\n" +
                               (shortages.Count > 0 ? $"<color=#ff8a80>내 재료 부족: {ShortageLine(shortages)}</color>" : "내 재료로 바로 제조할 수 있습니다.");
        }

        public bool SaveImport()
        {
            if (importPreview == null) { Message("먼저 코드를 확인하세요."); return false; }
            bool ok = DrinkLab.SaveRecipe(importPreview, -1, out var error);
            Message(ok ? "가져온 레시피를 저장했습니다(창고·버프 변화 없음)." : error);
            if (ok) { importPreview = null; SetTab(Tab.Recipes); }
            return ok;
        }

        // ═════ 공용 UI ═════

        static Button MakeButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, int fontSize, Color color, Action onClick)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            var bg = UiFactory.AddImage(rt, color);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            UiFactory.AddFocusSfx(b);
            b.onClick.AddListener(() => onClick());
            var t = UiFactory.AddText(UiFactory.Stretch("Label", rt), label, fontSize, TextAnchor.MiddleCenter, Color.white);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return b;
        }

        static InputField MakeInput(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string placeholder, int fontSize)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            var bg = UiFactory.AddImage(rt, new Color(0.95f, 0.93f, 0.88f, 1f));
            var textRt = UiFactory.Stretch("Text", rt);
            textRt.offsetMin = new Vector2(10f, 4f); textRt.offsetMax = new Vector2(-10f, -4f);
            var text = UiFactory.AddText(textRt, "", fontSize, TextAnchor.MiddleLeft, new Color(0.1f, 0.08f, 0.06f));
            text.color = new Color(0.1f, 0.08f, 0.06f);
            text.supportRichText = false;
            var phRt = UiFactory.Stretch("Placeholder", rt);
            phRt.offsetMin = new Vector2(10f, 4f); phRt.offsetMax = new Vector2(-10f, -4f);
            var ph = UiFactory.AddText(phRt, placeholder, fontSize, TextAnchor.MiddleLeft, new Color(0.45f, 0.42f, 0.4f));
            ph.color = new Color(0.45f, 0.42f, 0.4f);
            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = text;
            input.placeholder = ph;
            return input;
        }

        static RectTransform MakeScroll(RectTransform area, bool transparent = false)
        {
            UiFactory.AddImage(area, new Color(0f, 0f, 0f, transparent ? 0f : 0.25f));
            area.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(area, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 100f);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            // 세로 스크롤바(내용이 넘칠 때만 보임): 아래 내용이 더 있음을 알리고 끌어서 이동
            var barRt = UiFactory.Place("Scrollbar", area, new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(12f, 0f));
            barRt.anchorMin = new Vector2(1f, 0f); barRt.anchorMax = new Vector2(1f, 1f); barRt.pivot = new Vector2(1f, 0.5f);
            barRt.sizeDelta = new Vector2(12f, -8f);
            UiFactory.AddImage(barRt, new Color(0f, 0f, 0f, transparent ? 0.12f : 0.3f));
            var handleArea = UiFactory.Stretch("Sliding", barRt);
            var handle = UiFactory.Stretch("Handle", handleArea);
            var handleImg = UiFactory.AddImage(handle, transparent ? new Color(0.42f, 0.33f, 0.25f, 0.75f) : new Color(1f, 0.86f, 0.6f, 0.7f));
            var bar = barRt.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImg;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
    }
}
