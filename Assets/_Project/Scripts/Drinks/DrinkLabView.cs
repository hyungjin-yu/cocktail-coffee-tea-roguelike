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
        public enum Tab { Craft, Codex, Recipes }

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
        string lastMessage = "";

        // ───── 테스트 훅 ─────
        public DrinkRecipe Draft => draft;
        public DrinkStageView Stage => stage;
        public Tab CurrentTab => tab;
        public DrinkKind Kind => kind;
        public string NoticeText => notice != null ? notice.text : "";
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
            UiFactory.SkinHeader(UiFactory.AddText(UiFactory.Place("Title", root, new Vector2(0f, 1f), new Vector2(30f, -14f), new Vector2(420f, 70f)), "음료 제조", 44, TextAnchor.MiddleCenter, Color.white));
            float x = 470f;
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var tt = t;
                tabButtons[t] = MakeButton(root, "Tab" + t, new Vector2(0f, 1f), new Vector2(x, -16f), new Vector2(230f, 66f), TabName(t), DrinkPaperUi.Body, new Color(0.25f, 0.2f, 0.3f), () => SetTab(tt));
                x += 245f;
            }
            MakeButton(root, "Close", new Vector2(1f, 1f), new Vector2(-30f, -16f), new Vector2(200f, 66f), MobilePlatform.IsTouchUI ? "닫기" : "닫기 (Esc)", DrinkPaperUi.Body, new Color(0.3f, 0.26f, 0.3f), Close);
            UiFactory.AddText(UiFactory.Place("Mvp", root, new Vector2(0f, 0f), new Vector2(30f, 4f), new Vector2(420f, 34f)), DrinkLab.MvpLabel, DrinkPaperUi.Small, TextAnchor.LowerLeft, new Color(0.7f, 0.7f, 0.75f));
            notice = UiFactory.AddText(UiFactory.Place("Notice", root, new Vector2(0.5f, 1f), new Vector2(150f, -86f), new Vector2(1480f, 30f)), "", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.55f));
            notice.horizontalOverflow = HorizontalWrapMode.Wrap;
            notice.verticalOverflow = VerticalWrapMode.Truncate;
            SetTab(Tab.Craft);
        }

        static string TabName(Tab t) => t switch { Tab.Craft => "제조", Tab.Codex => "재료 도감", _ => "레시피" };

        public void SetTab(Tab t)
        {
            stage?.StopPour();
            tab = t;
            foreach (var kv in tabButtons) kv.Value.GetComponent<Image>().color = kv.Key == t ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.25f, 0.2f, 0.3f);
            if (body != null) Destroy(body.gameObject);
            body = UiFactory.Stretch("Body", root);
            body.offsetMax = new Vector2(0f, -118f);
            stage = null;
            switch (t)
            {
                case Tab.Craft: BuildCraft(); break;
                case Tab.Codex: BuildCodex(null); break;
                default: BuildRecipes(); break;
            }
            RefreshNotice();
        }

        void RefreshNotice()
        {
            var n = DrinkLab.Notice;
            notice.text = string.IsNullOrEmpty(lastMessage) ? (n ?? "") : lastMessage + (n != null ? "  ·  " + n : "");
        }

        void Message(string text)
        {
            lastMessage = text ?? "";
            RefreshNotice();
        }

        // ═════ 제조 탭 ═════

        void BuildCraft()
        {
            // 종류 전환(칵테일/차). 전환 시 현재 배합은 비운다 — 부적합 재료를 자동 소비/혼합하지 않는다.
            MakeButton(body, "KindCocktail", new Vector2(0f, 1f), new Vector2(30f, -6f), new Vector2(200f, 66f), "칵테일", DrinkPaperUi.Body,
                kind == DrinkKind.Cocktail ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Cocktail));
            MakeButton(body, "KindTea", new Vector2(0f, 1f), new Vector2(240f, -6f), new Vector2(200f, 66f), "차", DrinkPaperUi.Body,
                kind == DrinkKind.Tea ? new Color(0.36f, 0.5f, 0.2f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Tea));

            // 왼쪽 재료 목록
            var listRt = UiFactory.Place("IngredientList", body, new Vector2(0f, 1f), new Vector2(30f, -80f), new Vector2(410f, 836f));
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
            DrinkPaperUi.Sliced(mix, "receipt", 3f).raycastTarget = true;
            // 종이 안쪽 안전 여백: 좌우 48, 위 52, 아래 66(영수증 찢긴 끝에 버튼이 걸치지 않게). 스크롤 내용과 고정 아래쪽 사이 22 간격.
            const float padX = 48f, padTop = 52f, padBottom = 66f, gap = 14f, innerW = 630f - 2f * padX, panelH = 950f;
            const float buttonsY = padBottom, buttonsH = 66f;
            const float nameY = buttonsY + buttonsH + gap, nameH = 60f;
            const float craftY = nameY + nameH + gap, craftH = 70f;
            const float statusY = craftY + craftH + 8f, statusH = 40f;
            const float ruleY = statusY + statusH + 10f;
            float scrollTop = padTop + 70f, scrollH = panelH - scrollTop - (ruleY + 22f);
            preparedText = UiFactory.AddText(UiFactory.Place("Prepared", mix, new Vector2(0f, 1f), new Vector2(padX, -padTop), new Vector2(innerW, 66f)), "", DrinkPaperUi.Small, TextAnchor.MiddleLeft, DrinkPaperUi.InkGood);
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
            DrinkPaperUi.Button(mix, "Clear", new Vector2(0f, 0f), new Vector2(padX + bw + gap, buttonsY), new Vector2(bw, buttonsH), "비우기", DrinkPaperUi.Small, () => { if (Busy("비우기")) return; draft = DrinkRecipe.NewDraft(kind); editingRecipeIndex = -1; Rebind(); });
            DrinkPaperUi.Button(mix, "SkipMotion", new Vector2(0f, 0f), new Vector2(padX + 2f * (bw + gap), buttonsY), new Vector2(bw, buttonsH), "", DrinkPaperUi.Small, () => { skipMotion = !skipMotion; RefreshMix(); });
            if (DrinkLab.DevToolsEnabled && DrinkLab.IsIsolatedSave)
                MakeButton(body, "DevGrant", new Vector2(0f, 0f), new Vector2(30f, 4f), new Vector2(260f, 44f), "테스트 재료 지급(격리 저장)", 16, new Color(0.4f, 0.15f, 0.4f), () => { DrinkLab.DevGrant(); BuildIngredientRows(); RefreshMix(); });

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
                UiFactory.AddText(UiFactory.Place("Owned", row, new Vector2(0f, 1f), new Vector2(104f, -68f), new Vector2(250f, 54f)),
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
            if (def.form == IngredientForm.Leaf) limit = DrinkRules.TeaLeafMax - draft.LeafTotal();
            else if (kind == DrinkKind.Tea) limit = DrinkRules.TeaAdditiveMax - draft.LiquidTotal();
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
            Message(k == DrinkKind.Tea ? "차 배합으로 바꿨습니다(칵테일 배합은 비웠습니다)." : "칵테일 배합으로 바꿨습니다(차 배합은 비웠습니다).");
            SetTab(Tab.Craft);
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
                    kind = RowKind.Line, name = "Mix_" + id,
                    left = $"{idx++}. {def.name}" + (inTransfer ? $" <color=#1f548f>(옮기는 중 {Mathf.RoundToInt(stage.TransferProgress * 100f)}%)</color>" : " ✓"),
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
            if (shown.steps.Count == 0) Add(new RowSpec { kind = RowKind.Line, name = "MixEmpty", left = "재료를 골라 병을 끌어 부으세요", color = Muted });
            if (kind == DrinkKind.Cocktail)
            {
                Add(new RowSpec { kind = RowKind.Line, name = "Water", left = "물 (무료)", right = DrinkRules.Format(shown.water) + "mL", color = Muted,
                    minus = () => { this.draft.water = Mathf.Max(0, this.draft.water - 50); OnDraftChanged(); },
                    plus = () => { this.draft.water = Mathf.Min(Math.Min(DrinkRules.CocktailWaterMax, DrinkRules.CocktailLiquidMax - this.draft.LiquidTotal()), this.draft.water + 50); OnDraftChanged(); } });
                Add(new RowSpec { kind = RowKind.Line, name = "Ice", left = "얼음 (무료)", right = $"{shown.ice}개", color = Muted,
                    minus = () => { this.draft.ice = Mathf.Max(0, this.draft.ice - 1); OnDraftChanged(); },
                    plus = () => { this.draft.ice = Mathf.Min(DrinkRules.IceMax, this.draft.ice + 1); OnDraftChanged(); } });
            }
            else Add(new RowSpec { kind = RowKind.Line, name = "Water", left = "물 (무료, 고정)", right = "150.0mL", color = Muted });
            Add(new RowSpec { kind = RowKind.Header, name = "H_tech", left = "제조 방법" });
            Add(new RowSpec { kind = RowKind.Techs, name = "Techs_" + kind + "_" + shown.technique });
            Add(new RowSpec { kind = RowKind.Line, name = "TechDesc", left = DrinkRules.TechniqueShort(shown.Technique), color = Muted, h = 66f, size = DrinkPaperUi.Small });
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
            var look = DrinkCalculator.Look(shown);
            if (look.totalVolume > 0f) Add(new RowSpec { kind = RowKind.Line, name = "LookDesc", left = "완성 모습: " + LookWords(look), color = Muted, h = 40f, size = DrinkPaperUi.Small });
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
                if (lack && costExpanded) Add(new RowSpec { kind = RowKind.Line, name = "CostSrc_" + s.id, left = "획득처: " + def.source, color = Muted, h = 66f, size = DrinkPaperUi.Small - 2 });
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
                        leftTexts[s.name] = UiFactory.AddText(UiFactory.Place(s.name, mixContent, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(230f, 42f)), s.left, DrinkPaperUi.Heading, TextAnchor.MiddleLeft, Gold);
                        if (s.right != null)
                            rightTexts[s.name] = UiFactory.AddText(UiFactory.Place(s.name + "_Right", mixContent, new Vector2(0f, 1f), new Vector2(238f, -y), new Vector2(RowW - 230f, 42f)), s.right, DrinkPaperUi.Small, TextAnchor.MiddleRight, Muted);
                        // 영수증 점선 구분
                        UiFactory.AddImage(UiFactory.Place(s.name + "_Rule", mixContent, new Vector2(0f, 1f), new Vector2(8f, -y - 44f), new Vector2(RowW, 2f)), new Color(0.42f, 0.33f, 0.25f, 0.35f)).raycastTarget = false;
                        y += 50f;
                        break;
                    case RowKind.Line:
                    {
                        if (s.remove != null || s.minus != null || s.plus != null) s.h = Mathf.Max(s.h, 66f); // 누르기 영역 720p 44px
                        var row = UiFactory.Place(s.name, mixContent, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(RowW, s.h));
                        var l = UiFactory.AddText(UiFactory.Place("L", row, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(RowW * (s.right != null ? 0.62f : 0.98f), s.h)), s.left, s.size, TextAnchor.MiddleLeft, s.color);
                        l.supportRichText = true;
                        l.verticalOverflow = VerticalWrapMode.Truncate;
                        l.horizontalOverflow = HorizontalWrapMode.Wrap;
                        leftTexts[s.name] = l;
                        // 값 칸은 행 끝 버튼(× 또는 −/+) 폭만큼 안쪽에서 끝난다(겹침 없음).
                        float inset = s.remove != null ? 64f : s.minus != null || s.plus != null ? 128f : 0f;
                        if (s.right != null) rightTexts[s.name] = UiFactory.AddText(UiFactory.Place("R", row, new Vector2(1f, 0.5f), new Vector2(-8f - inset, 0f), new Vector2(RowW * 0.36f - inset * 0.5f, s.h)), s.right, s.size, TextAnchor.MiddleRight, s.color == DrinkPaperUi.InkLive ? DrinkPaperUi.InkLive : DrinkPaperUi.Ink);
                        if (s.remove != null) SmallButton(row, "Remove", -2f, "×", s.remove);
                        if (s.minus != null) SmallButton(row, s.name + "Minus", -66f, "−", s.minus);
                        if (s.plus != null) SmallButton(row, s.name + "Plus", -2f, "+", s.plus);
                        y += s.h + 2f;
                        break;
                    }
                    case RowKind.Techs:
                    {
                        var techs = kind == DrinkKind.Tea ? new[] { DrinkTechnique.Steep } : new[] { DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer };
                        // 2열 버튼(720p에서 글자 18px 이상·누르기 44px 이상). 고른 기법 = 선택 표식 아트.
                        int cols = Mathf.Min(2, techs.Length);
                        float bw = (RowW - (cols - 1) * 8f) / cols;
                        for (int i = 0; i < techs.Length; i++)
                        {
                            var t = techs[i];
                            bool on = (int)t == draft.technique;
                            techButtons[t] = DrinkPaperUi.Button(mixContent, "Tech" + t, new Vector2(0f, 1f), new Vector2(8f + (i % cols) * (bw + 8f), -y - (i / cols) * 64f), new Vector2(bw, 58f), DrinkRules.TechniqueName(t), DrinkPaperUi.Body,
                                () => SetTechnique(t), on ? "button_equipped" : "button", on ? new Color(1f, 0.85f, 0.6f) : (Color?)null);
                        }
                        y += ((techs.Length + cols - 1) / cols) * 64f + 4f;
                        break;
                    }
                    case RowKind.Toggle:
                    {
                        y += 10f;
                        var b = DrinkPaperUi.Button(mixContent, s.name, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(RowW, 54f), s.left, DrinkPaperUi.Small, () => { costExpanded = !costExpanded; RefreshMix(); });
                        var label = b.GetComponentInChildren<Text>();
                        label.alignment = TextAnchor.MiddleLeft;
                        leftTexts[s.name] = label;
                        y += 60f;
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
            DrinkPaperUi.Button(parent, name, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(60f, 62f), label, DrinkPaperUi.Body, onClick);

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
                Message(editingRecipeIndex >= 0 ? "레시피를 수정했습니다." : "레시피를 저장했습니다(재료 소모 없음).");
                // 저장 트랜잭션이 성공한 뒤에만: 노트에서 한 장 찢어 보관함에 넣는 장식 모션(데이터 변화 없음, 클릭하면 끝 상태로)
                string shown = string.IsNullOrWhiteSpace(r.name) ? DrinkLab.Recipes[DrinkLab.Recipes.Count - 1].name : r.name;
                if (editingRecipeIndex >= 0 && editingRecipeIndex < DrinkLab.Recipes.Count) shown = DrinkLab.Recipes[editingRecipeIndex].name;
                RecipeTearOverlay.Play(root, shown);
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
            var listRt = UiFactory.Place("CodexList", body, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1860f, 940f));
            var content = MakeScroll(listRt);
            const float cardW = 606f, cardH = 640f, gap = 12f;
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
                var probe = DrinkRecipe.NewDraft(leaf ? DrinkKind.Tea : DrinkKind.Cocktail);
                probe.SetAmount(def.id, leaf ? 20 : 300);
                var glassRoot = UiFactory.Place("ColorGlass", card, new Vector2(1f, 1f), new Vector2(-44f, -36f), new Vector2(110f, 120f));
                glassRoot.pivot = new Vector2(1f, 1f);
                var glass = DrinkVesselView.Create(glassRoot, leaf ? "dl_teacup" : "dl_glass_cocktail", new Vector2(0f, -60f), 60f, 2);
                if (leaf) glass.SetLiquid(DrinkPhysicsSim.TeaColor(probe), 40f, def.turbidity);
                else glass.SetLiquid(DrinkCalculator.IngredientLook(def), 40f, def.turbidity, def.density);
                string colorWords = (leaf ? "우린 차: " : "실제 색: ") + LookWords(DrinkCalculator.Look(probe));
                string text =
                    $"{def.taste}\n" +
                    $"{colorWords}\n" +
                    $"사용: {def.UseLabel} · 단위 {def.Unit}\n" +
                    $"게임 효과: {DrinkRules.StatName(def.effect)} (상한 {DrinkBuff.Pct(def.effect, DrinkRules.Cap(def.effect))})\n" +
                    $"착색 {(def.tint > 2f ? "강함" : def.tint > 0.5f ? "보통" : "약함")} · 탁도 {(def.turbidity > 0.3f ? "높음" : def.turbidity > 0.05f ? "약간" : "맑음")} · 밀도 {def.density:0.00}\n" +
                    $"획득처: {def.source}" + (seen ? "" : "\n아직 보유한 적이 없어 제조에 쓸 수 없습니다.");
                var t = UiFactory.AddText(UiFactory.Place("Body", card, new Vector2(0f, 1f), new Vector2(40f, -164f), new Vector2(cardW - 80f, cardH - 210f)), text, DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
                t.lineSpacing = 1.05f;
                if (++col == 3) { col = 0; y += cardH + gap; }
            }
            if (col != 0) y += cardH + gap;
            var aux = UiFactory.Place("Aux", content, new Vector2(0f, 1f), new Vector2(gap, -y), new Vector2(1830f, 90f));
            UiFactory.AddText(aux, "물·기본 얼음은 무료 제조 보조 자원입니다(배합에 기록). 물은 색을 희석하고 버프 기여가 없습니다.", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 1f));
            content.sizeDelta = new Vector2(0f, y + 100f);
            if (focusY > 0f) content.anchoredPosition = new Vector2(0f, focusY); // 획득처 보기로 연 재료가 보이게
        }

        // ═════ 레시피 탭 ═════

        void BuildRecipes()
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var listRt = UiFactory.Place("RecipeList", body, new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(1100f, 930f));
            var content = MakeScroll(listRt);
            float y = 0f;
            // 처음에도 고를 수 있게: 내장 예시 배합(저장 목록과 별개, 불러오기만 — 제조·소모 없음).
            UiFactory.AddText(UiFactory.Place("ExamplesTitle", content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1060f, 40f)),
                "기본 예시 배합 (불러오기만으로는 재료를 쓰지 않습니다)", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            y += 46f;
            var presets = DrinkLab.ExamplePresets;
            for (int i = 0; i < presets.Count; i++)
            {
                int index = i;
                var r = presets[i];
                var look = DrinkCalculator.Look(r);
                var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
                var row = UiFactory.Place("Example_" + i, content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1080f, 196f));
                DrinkPaperUi.Sliced(row, "torn_recipe", 3f, new Color(0.9f, 0.96f, 0.86f));
                var info = UiFactory.AddText(UiFactory.Place("Info", row, new Vector2(0f, 1f), new Vector2(36f, -44f), new Vector2(780f, 140f)),
                    $"{r.name}  ·  {(r.Kind == DrinkKind.Tea ? "차" : "칵테일")} · {DrinkRules.TechniqueName(r.Technique)} · {LookWords(look)}\n{RecipeLine(r)}\n버프: {DrinkCalculator.Buff(r).Describe()}" +
                    (shortages.Count > 0 ? $"\n<color=#a8241a>재료 부족: {ShortageLine(shortages)}</color>" : ""), DrinkPaperUi.Small, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
                info.supportRichText = true;
                DrinkPaperUi.Button(row, "UseExample", new Vector2(1f, 0.5f), new Vector2(-30f, -8f), new Vector2(200f, 60f), "불러오기", DrinkPaperUi.Small, () => LoadPreset(index));
                y += 204f;
            }
            y += 10f;
            var recipes = DrinkLab.Recipes;
            UiFactory.AddText(UiFactory.Place("Count", body, new Vector2(1f, 1f), new Vector2(-30f, -6f), new Vector2(700f, 40f)), $"저장한 레시피 {recipes.Count} / {DrinkRules.RecipeLimit}", DrinkPaperUi.Small, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            for (int i = 0; i < recipes.Count; i++)
            {
                int index = i;
                var r = recipes[i];
                var check = DrinkValidator.Check(r);
                var buff = DrinkCalculator.Buff(r); // 저장값이 아니라 현재 밸런스로 재계산
                var look = DrinkCalculator.Look(r);
                var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
                // 보관한 레시피 = 찢긴 레시피 카드(9slice 3배). 색은 글로만(색상 사각형 없음).
                var row = UiFactory.Place("Recipe_" + i, content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1080f, 236f));
                DrinkPaperUi.Sliced(row, "torn_recipe", 3f);
                string info = $"{r.name}  ·  {(r.Kind == DrinkKind.Tea ? "차" : "칵테일")} · {DrinkRules.TechniqueName(r.Technique)} · {(look.totalVolume > 0 ? LookWords(look) : "")}\n{RecipeLine(r)}\n버프: {buff.Describe()}" +
                              (!check.Ok ? $"\n<color=#a8241a>사용 불가: {check.First}</color>" : shortages.Count > 0 ? $"\n<color=#a8241a>재료 부족: {ShortageLine(shortages)}</color>" : "");
                var t = UiFactory.AddText(UiFactory.Place("Info", row, new Vector2(0f, 1f), new Vector2(36f, -44f), new Vector2(600f, 180f)), info, DrinkPaperUi.Small, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
                t.supportRichText = true;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                DrinkPaperUi.Button(row, "Use", new Vector2(1f, 1f), new Vector2(-232f, -40f), new Vector2(200f, 56f), "불러와 제조", DrinkPaperUi.Small, () => LoadRecipe(index, false), tint: new Color(1f, 0.85f, 0.6f));
                DrinkPaperUi.Button(row, "Edit", new Vector2(1f, 1f), new Vector2(-24f, -40f), new Vector2(200f, 56f), "수정", DrinkPaperUi.Small, () => LoadRecipe(index, true));
                DrinkPaperUi.Button(row, "Copy", new Vector2(1f, 1f), new Vector2(-232f, -102f), new Vector2(200f, 56f), "공유 코드 복사", DrinkPaperUi.Small, () => CopyCode(index));
                DrinkPaperUi.Button(row, "Dup", new Vector2(1f, 1f), new Vector2(-24f, -102f), new Vector2(200f, 56f), "복제", DrinkPaperUi.Small, () => { if (DrinkLab.DuplicateRecipe(index, out var e)) Message("복제했습니다."); else Message(e); SetTab(Tab.Recipes); });
                DrinkPaperUi.Button(row, "Delete", new Vector2(1f, 1f), new Vector2(-24f, -164f), new Vector2(200f, 56f), "삭제", DrinkPaperUi.Small, () => { if (DrinkLab.DeleteRecipe(index, out var e)) Message("삭제했습니다."); else Message(e); SetTab(Tab.Recipes); }, tint: new Color(1f, 0.75f, 0.7f));
                y += 244f;
            }
            if (recipes.Count == 0)
                UiFactory.AddText(UiFactory.Place("Empty", content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1000f, 100f)), "저장한 레시피가 없습니다. 제조 탭에서 배합 후 \"레시피 저장\"을 누르세요(재료 소모 없음).", DrinkPaperUi.Small, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.9f));
            content.sizeDelta = new Vector2(0f, Mathf.Max(y + (recipes.Count == 0 ? 110f : 0f), 120f));

            // 가져오기
            var imp = UiFactory.Place("Import", body, new Vector2(1f, 1f), new Vector2(-30f, -50f), new Vector2(700f, 600f));
            UiFactory.AddImage(imp, new Color(0.1f, 0.08f, 0.12f, 0.92f));
            UiFactory.AddText(UiFactory.Place("Title", imp, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(660f, 40f)), "공유 코드 가져오기", DrinkPaperUi.Heading, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            UiFactory.AddText(UiFactory.Place("Help", imp, new Vector2(0f, 1f), new Vector2(16f, -54f), new Vector2(660f, 64f)), "코드(CCT1-…)를 붙여 넣고 확인 → 미리보기 후 저장. 창고·버프는 바뀌지 않고, 제조 때 내 재료를 씁니다.", DrinkPaperUi.Small - 2, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.9f));
            importField = MakeInput(imp, "Code", new Vector2(0f, 1f), new Vector2(16f, -124f), new Vector2(668f, 116f), "CCT1-…", DrinkPaperUi.Small - 2);
            importField.lineType = InputField.LineType.MultiLineNewline;
            importField.characterLimit = DrinkShareCode.MaxPasteLength;
            DrinkPaperUi.Button(imp, "Paste", new Vector2(0f, 1f), new Vector2(16f, -250f), new Vector2(210f, 58f), "붙여넣기", DrinkPaperUi.Small, () => importField.text = GUIUtility.systemCopyBuffer ?? "");
            DrinkPaperUi.Button(imp, "Check", new Vector2(0f, 1f), new Vector2(234f, -250f), new Vector2(210f, 58f), "코드 확인", DrinkPaperUi.Small, () => PreviewImport(importField.text));
            DrinkPaperUi.Button(imp, "SaveImport", new Vector2(0f, 1f), new Vector2(452f, -250f), new Vector2(232f, 58f), "레시피로 저장", DrinkPaperUi.Small, () => SaveImport(), tint: new Color(1f, 0.85f, 0.6f));
            var prev = UiFactory.AddText(UiFactory.Place("Preview", imp, new Vector2(0f, 1f), new Vector2(16f, -318f), new Vector2(668f, 270f)), "", DrinkPaperUi.Small - 2, TextAnchor.UpperLeft, Color.white);
            prev.supportRichText = true;
            prev.name = "ImportPreviewText";
            previewText = prev;
            RenderImportPreview(null);
            // 레시피 보관함(장식, 균일 3배): 찢은 레시피가 들어가는 곳
            var box = UiFactory.Place("RecipeStorage", body, new Vector2(1f, 0f), new Vector2(-140f, 30f), Vector2.zero);
            box.pivot = new Vector2(1f, 0f);
            DrinkPaperUi.Uniform(box, "recipe_storage", 3f);
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
            return string.Join(" → ", parts);
        }

        static string ShortageLine(Dictionary<string, int> s)
        {
            var parts = new List<string>();
            foreach (var kv in s) { var d = DrinkIngredients.Get(kv.Key); if (d != null) parts.Add($"{d.name} {DrinkRules.Format(kv.Value, d.form)} (획득처: {d.source})"); }
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
            kind = r.Kind == DrinkKind.Tea ? DrinkKind.Tea : DrinkKind.Cocktail;
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
            Message("공유 코드를 클립보드에 복사했습니다: " + code);
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
            previewText.text = $"{r.name} · {(r.Kind == DrinkKind.Tea ? "차" : "칵테일")} · {DrinkRules.TechniqueName(r.Technique)}\n{RecipeLine(r)}\n" +
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
