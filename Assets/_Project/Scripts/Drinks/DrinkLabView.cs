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
                tabButtons[t] = MakeButton(root, "Tab" + t, new Vector2(0f, 1f), new Vector2(x, -22f), new Vector2(230f, 60f), TabName(t), 26, new Color(0.25f, 0.2f, 0.3f), () => SetTab(tt));
                x += 245f;
            }
            MakeButton(root, "Close", new Vector2(1f, 1f), new Vector2(-30f, -22f), new Vector2(200f, 60f), "닫기 (Esc)", 26, new Color(0.3f, 0.26f, 0.3f), Close);
            UiFactory.AddText(UiFactory.Place("Mvp", root, new Vector2(1f, 0f), new Vector2(-24f, 6f), new Vector2(600f, 26f)), DrinkLab.MvpLabel, 16, TextAnchor.LowerRight, new Color(0.7f, 0.7f, 0.75f));
            notice = UiFactory.AddText(UiFactory.Place("Notice", root, new Vector2(0.5f, 1f), new Vector2(240f, -86f), new Vector2(1300f, 30f)), "", 20, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.55f));
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
            MakeButton(body, "KindCocktail", new Vector2(0f, 1f), new Vector2(30f, -6f), new Vector2(200f, 54f), "칵테일", 26,
                kind == DrinkKind.Cocktail ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Cocktail));
            MakeButton(body, "KindTea", new Vector2(0f, 1f), new Vector2(240f, -6f), new Vector2(200f, 54f), "차", 26,
                kind == DrinkKind.Tea ? new Color(0.36f, 0.5f, 0.2f) : new Color(0.25f, 0.2f, 0.3f), () => SetKind(DrinkKind.Tea));

            // 왼쪽 재료 목록
            var listRt = UiFactory.Place("IngredientList", body, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(410f, 880f));
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
            UiFactory.AddText(UiFactory.Place("Label", ctl, new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(500f, 36f)), "정확한 양 (0.1 단위 입력)", 22, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            amountField = MakeInput(ctl, "Amount", new Vector2(0f, 1f), new Vector2(16f, -50f), new Vector2(220f, 60f), "0.0", 30);
            amountField.onEndEdit.AddListener(t => ApplyTypedAmount(t));
            MakeButton(ctl, "Apply", new Vector2(0f, 1f), new Vector2(246f, -50f), new Vector2(120f, 60f), "적용", 24, new Color(0.4f, 0.3f, 0.2f), () => ApplyTypedAmount(amountField.text));
            MakeButton(ctl, "Remove", new Vector2(0f, 1f), new Vector2(376f, -50f), new Vector2(120f, 60f), "빼기", 24, new Color(0.45f, 0.2f, 0.2f), () => { if (selected != null) { draft.Remove(selected.id); OnDraftChanged(); } });
            int[] steps = { -100, -10, -1, 1, 10, 100 };
            for (int i = 0; i < steps.Length; i++)
            {
                int s = steps[i];
                string label = (s > 0 ? "+" : "−") + DrinkRules.Format(Math.Abs(s));
                MakeButton(ctl, "Nudge" + s, new Vector2(0f, 1f), new Vector2(16f + i * 128f, -122f), new Vector2(118f, 54f), label, 22, new Color(0.22f, 0.22f, 0.3f), () => Nudge(s));
            }
            amountError = UiFactory.AddText(UiFactory.Place("Error", ctl, new Vector2(0f, 1f), new Vector2(510f, -50f), new Vector2(280f, 60f)), "", 18, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.5f));
            UiFactory.AddText(UiFactory.Place("Hint", ctl, new Vector2(0f, 1f), new Vector2(16f, -182f), new Vector2(770f, 56f)),
                "병을 누른 채 오른쪽·아래로 끌면 기울어져 붓습니다(조금 끌면 아주 천천히). 놓으면 즉시 멈춤 · 키보드 ↑↓ 0.1, Shift 1.0", 18, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.85f));

            // 오른쪽 배합 패널: 위 준비 음료 한 줄 / 가운데 스크롤(배합 → 제조 방법 → 예상 효과 → 재료 소모) / 아래 고정 버튼.
            var mix = UiFactory.Place("MixPanel", body, new Vector2(1f, 1f), new Vector2(-24f, -6f), new Vector2(630f, 940f));
            UiFactory.AddImage(mix, new Color(0.1f, 0.08f, 0.12f, 0.92f));
            preparedText = UiFactory.AddText(UiFactory.Place("Prepared", mix, new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(596f, 48f)), "", 19, TextAnchor.MiddleLeft, new Color(0.7f, 1f, 0.75f));
            var scrollRt = UiFactory.Place("MixScroll", mix, new Vector2(0f, 1f), new Vector2(10f, -64f), new Vector2(610f, 690f));
            mixContent = MakeScroll(scrollRt);
            lastTopology = null; leftTexts.Clear(); rightTexts.Clear(); // 새 패널 — 다음 갱신은 반드시 다시 만든다
            UiFactory.AddImage(UiFactory.Place("FooterRule", mix, new Vector2(0f, 0f), new Vector2(16f, 182f), new Vector2(598f, 2f)), new Color(1f, 0.86f, 0.6f, 0.35f)).raycastTarget = false;
            footerStatus = UiFactory.AddText(UiFactory.Place("FooterStatus", mix, new Vector2(0f, 0f), new Vector2(18f, 146f), new Vector2(596f, 32f)), "", 19, TextAnchor.MiddleLeft, Color.white);
            footerStatus.supportRichText = true;
            craftButton = MakeButton(mix, "Craft", new Vector2(0f, 0f), new Vector2(16f, 80f), new Vector2(598f, 62f), "제조 확정", 28, new Color(0.6f, 0.36f, 0.12f), ConfirmCraft);
            nameField = MakeInput(mix, "RecipeName", new Vector2(0f, 0f), new Vector2(16f, 14f), new Vector2(248f, 56f), "레시피 이름", 20);
            nameField.characterLimit = DrinkRules.RecipeNameMax;
            saveButton = MakeButton(mix, "SaveRecipe", new Vector2(0f, 0f), new Vector2(272f, 14f), new Vector2(128f, 56f), "레시피 저장", 18, new Color(0.3f, 0.3f, 0.45f), SaveDraftAsRecipe);
            MakeButton(mix, "Clear", new Vector2(0f, 0f), new Vector2(408f, 14f), new Vector2(96f, 56f), "비우기", 18, new Color(0.35f, 0.25f, 0.25f), () => { if (Busy("비우기")) return; draft = DrinkRecipe.NewDraft(kind); editingRecipeIndex = -1; Rebind(); });
            MakeButton(mix, "SkipMotion", new Vector2(0f, 0f), new Vector2(512f, 14f), new Vector2(102f, 56f), "", 16, new Color(0.25f, 0.25f, 0.3f), () => { skipMotion = !skipMotion; RefreshMix(); });
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
                var row = UiFactory.Place("Row_" + def.id, listContent, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(390f, 96f));
                var bg = UiFactory.AddImage(row, selected == def ? new Color(0.42f, 0.3f, 0.16f) : new Color(0.18f, 0.15f, 0.22f));
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                UiFactory.AddFocusSfx(btn);
                btn.onClick.AddListener(() => { if (DrinkLab.Owned(d.id) > 0) SelectIngredient(d); else OpenCodexFor(d); });
                var icon = DrinkArt.SpriteOrPlaceholder(def.iconId, out _);
                var iconRt = UiFactory.Place("Icon", row, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(64f, 64f));
                var iconImg = iconRt.gameObject.AddComponent<Image>(); iconImg.sprite = icon; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                if (locked) iconImg.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                UiFactory.AddText(UiFactory.Place("Name", row, new Vector2(0f, 1f), new Vector2(84f, -6f), new Vector2(290f, 38f)), def.name, 24, TextAnchor.MiddleLeft, locked ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
                UiFactory.AddText(UiFactory.Place("Owned", row, new Vector2(0f, 1f), new Vector2(84f, -44f), new Vector2(270f, 44f)),
                    locked ? "보유 없음 · 눌러서 획득처 보기" : $"보유 {DrinkRules.Format(owned, def.form)} · {DrinkRules.StatName(def.effect)}", 17, TextAnchor.UpperLeft, locked ? new Color(1f, 0.6f, 0.5f) : new Color(0.8f, 0.85f, 0.9f));
                y += 104f;
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

        bool liveDirty;
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

        static readonly Color Gold = new Color(1f, 0.86f, 0.6f);
        static readonly Color Muted = new Color(0.78f, 0.78f, 0.84f);
        const float RowW = 590f;

        // ───── 오른쪽 패널: 행 명세 → (구조가 같으면) 글자만 갱신 / (구조가 바뀌면) 다시 만들기 ─────
        enum RowKind { Header, Line, Techs, Toggle }
        sealed class RowSpec
        {
            public RowKind kind;
            public string name, left, right;
            public Color color = Color.white;
            public float h = 34f;
            public int size = 20;
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
            foreach (var s in shown.steps)
            {
                var def = DrinkIngredients.Get(s.id);
                if (def == null) continue;
                string id = s.id;
                bool measuring = pending > 0 && stage != null && stage.CurrentId == id;
                Add(new RowSpec
                {
                    kind = RowKind.Line, name = "Mix_" + id,
                    left = $"{idx++}. {def.name}" + (measuring ? $" <color=#9fd8ff>(지거 안 {DrinkRules.Format(pending, def.form)} 포함)</color>" : ""),
                    right = DrinkRules.Format(s.amount, def.form),
                    remove = () => { if (Busy("빼기")) return; this.draft.Remove(id); OnDraftChanged(); },
                });
            }
            if (shown.steps.Count == 0) Add(new RowSpec { kind = RowKind.Line, name = "MixEmpty", left = "왼쪽에서 재료를 골라 병을 끌어 부으세요", color = Muted });
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
            Add(new RowSpec { kind = RowKind.Line, name = "TechDesc", left = DrinkRules.TechniqueShort(shown.Technique), color = Muted, h = 30f, size = 18 });
            Add(new RowSpec { kind = RowKind.Header, name = "H_buff", left = "예상 효과", right = "이번 판 동안" });
            var buff = DrinkCalculator.Buff(shown);
            var buffParts = new List<string>();
            foreach (var s in buff.Active())
            {
                Add(new RowSpec { kind = RowKind.Line, name = "Buff_" + s, left = DrinkRules.StatName(s), right = DrinkBuff.Pct(s, buff[s]), color = new Color(0.75f, 1f, 0.8f) });
                buffParts.Add($"{DrinkRules.StatName(s)} {DrinkBuff.Pct(s, buff[s])}");
            }
            if (buffParts.Count == 0) Add(new RowSpec { kind = RowKind.Line, name = "BuffNone", left = "효과 없음", color = Muted });
            buffLine = buffParts.Count == 0 ? "효과 없음" : string.Join("\n", buffParts);
            var look = DrinkCalculator.Look(shown);
            if (look.totalVolume > 0f) Add(new RowSpec { kind = RowKind.Line, name = "LookDesc", left = "완성 모습: " + LookWords(look), color = Muted, h = 30f, size = 18 });
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
                    Add(new RowSpec { kind = RowKind.Line, name = "Cost_" + s.id + (lack ? "_short" : ""), left = lack ? $"<color=#ff8a80>{def.name} — {DrinkRules.Format(shortages[s.id], def.form)} 부족</color>" : def.name, right = right, h = 32f, size = 18 });
                if (lack && costExpanded) Add(new RowSpec { kind = RowKind.Line, name = "CostSrc_" + s.id, left = "획득처: " + def.source, color = Muted, h = 28f, size = 16 });
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
                        leftTexts[s.name] = UiFactory.AddText(UiFactory.Place(s.name, mixContent, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(360f, 34f)), s.left, 22, TextAnchor.MiddleLeft, Gold);
                        if (s.right != null)
                            rightTexts[s.name] = UiFactory.AddText(UiFactory.Place(s.name + "_Right", mixContent, new Vector2(0f, 1f), new Vector2(370f, -y), new Vector2(220f, 34f)), s.right, 18, TextAnchor.MiddleRight, Muted);
                        y += 40f;
                        break;
                    case RowKind.Line:
                    {
                        var row = UiFactory.Place(s.name, mixContent, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(RowW, s.h));
                        var l = UiFactory.AddText(UiFactory.Place("L", row, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(RowW * (s.right != null ? 0.62f : 0.98f), s.h)), s.left, s.size, TextAnchor.MiddleLeft, s.color);
                        l.supportRichText = true;
                        l.verticalOverflow = VerticalWrapMode.Truncate;
                        leftTexts[s.name] = l;
                        // 값 칸은 행 끝 버튼(× 또는 −/+) 폭만큼 안쪽에서 끝난다(겹침 없음).
                        float inset = s.remove != null ? 52f : s.minus != null || s.plus != null ? 104f : 0f;
                        if (s.right != null) rightTexts[s.name] = UiFactory.AddText(UiFactory.Place("R", row, new Vector2(1f, 0.5f), new Vector2(-8f - inset, 0f), new Vector2(RowW * 0.36f - inset * 0.5f, s.h)), s.right, s.size, TextAnchor.MiddleRight, Color.white);
                        if (s.remove != null) SmallButton(row, "Remove", -2f, "×", s.remove);
                        if (s.minus != null) SmallButton(row, s.name + "Minus", -54f, "−", s.minus);
                        if (s.plus != null) SmallButton(row, s.name + "Plus", -2f, "+", s.plus);
                        y += s.h + 2f;
                        break;
                    }
                    case RowKind.Techs:
                    {
                        var techs = kind == DrinkKind.Tea ? new[] { DrinkTechnique.Steep } : new[] { DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer };
                        float bw = (RowW - (techs.Length - 1) * 6f) / techs.Length;
                        for (int i = 0; i < techs.Length; i++)
                        {
                            var t = techs[i];
                            bool on = (int)t == draft.technique;
                            techButtons[t] = MakeButton(mixContent, "Tech" + t, new Vector2(0f, 1f), new Vector2(8f + i * (bw + 6f), -y), new Vector2(bw, 48f), DrinkRules.TechniqueName(t), 21,
                                on ? new Color(0.55f, 0.36f, 0.16f) : new Color(0.24f, 0.21f, 0.3f), () => SetTechnique(t));
                        }
                        y += 54f;
                        break;
                    }
                    case RowKind.Toggle:
                    {
                        y += 10f;
                        var b = MakeButton(mixContent, s.name, new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(RowW, 38f), s.left, 19, new Color(0.18f, 0.16f, 0.22f), () => { costExpanded = !costExpanded; RefreshMix(); });
                        var label = b.GetComponentInChildren<Text>();
                        label.alignment = TextAnchor.MiddleLeft;
                        leftTexts[s.name] = label;
                        y += 44f;
                        break;
                    }
                }
            }
            mixContent.sizeDelta = new Vector2(0f, y + 12f);
            if (scroll != null)
            {
                scroll.StopMovement();
                float viewH = ((RectTransform)scroll.transform).rect.height;
                mixContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(keepY, 0f, Mathf.Max(0f, mixContent.sizeDelta.y - viewH)));
            }
            PanelRebuilds++;
        }

        Button SmallButton(Transform parent, string name, float x, string label, Action onClick) =>
            MakeButton(parent, name, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(44f, 30f), label, 18, new Color(0.25f, 0.24f, 0.32f), onClick);

        void RefreshMix()
        {
            if (mixContent == null) return;
            var prepared = DrinkLab.Prepared;
            preparedText.text = prepared != null
                ? $"준비된 음료: {prepared.recipe.name} — 이 음료로 출발합니다(새로 만들 수 없음)"
                : "준비된 음료 없음 · 만들지 않고 출발해도 됩니다";
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
            footerStatus.text = prepared != null ? "<color=#a8f0b0>준비된 음료가 있어 새로 만들 수 없습니다</color>"
                : busy ? "붓는 중… 다 옮겨 부은 뒤 제조할 수 있습니다"
                : shown.steps.Count == 0 ? "재료를 넣으면 제조할 수 있습니다"
                : !check.Ok ? $"<color=#ffb080>{check.First}</color>"
                : shortages.Count > 0 ? $"<color=#ff8a80>재료 {shortages.Count}종 부족 — 소모 목록에서 획득처 확인</color>"
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
                Message(editingRecipeIndex >= 0 ? "레시피를 수정했습니다." : "레시피를 저장했습니다(재료 소모 없음).");
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
            foreach (Transform c in body) Destroy(c.gameObject);
            var listRt = UiFactory.Place("CodexList", body, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1860f, 930f));
            var content = MakeScroll(listRt);
            float y = 0f;
            int col = 0;
            foreach (var def in DrinkIngredients.All)
            {
                bool seen = DrinkLab.Seen(def.id);
                int owned = DrinkLab.Owned(def.id);
                var card = UiFactory.Place("Codex_" + def.id, content, new Vector2(0f, 1f), new Vector2(10f + col * 460f, -y), new Vector2(450f, 440f));
                UiFactory.AddImage(card, focus == def ? new Color(0.42f, 0.3f, 0.16f) : new Color(0.15f, 0.12f, 0.19f));
                var icon = UiFactory.Place("Icon", card, new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(96f, 96f));
                var img = icon.gameObject.AddComponent<Image>(); img.sprite = DrinkArt.SpriteOrPlaceholder(def.iconId, out _); img.preserveAspect = true; img.raycastTarget = false;
                if (!seen) img.color = new Color(0.35f, 0.35f, 0.35f);
                UiFactory.AddText(UiFactory.Place("Name", card, new Vector2(0f, 1f), new Vector2(124f, -14f), new Vector2(310f, 40f)), def.name + (owned > 0 ? "" : seen ? " (보유 0)" : " (잠김)"), 28, TextAnchor.MiddleLeft, seen ? Color.white : new Color(0.7f, 0.7f, 0.7f));
                var swatch = UiFactory.Place("RealColor", card, new Vector2(0f, 1f), new Vector2(124f, -60f), new Vector2(310f, 30f));
                UiFactory.AddImage(swatch, def.form == IngredientForm.Leaf ? def.color : DrinkCalculator.IngredientLook(def)).raycastTarget = false;
                if (def.form == IngredientForm.Leaf)
                {
                    var brew = UiFactory.Place("BrewColor", card, new Vector2(0f, 1f), new Vector2(284f, -60f), new Vector2(150f, 30f));
                    UiFactory.AddImage(brew, def.brewColor).raycastTarget = false;
                }
                string body2 =
                    $"{def.taste}\n\n" +
                    $"사용: {def.UseLabel} · 단위: {def.Unit}\n" +
                    $"보유: {DrinkRules.Format(owned, def.form)}\n" +
                    $"게임 효과: {DrinkRules.StatName(def.effect)} (상한 {DrinkBuff.Pct(def.effect, DrinkRules.Cap(def.effect))})\n" +
                    $"성질: 착색 {(def.tint > 2f ? "강함" : def.tint > 0.5f ? "보통" : "약함")} · 탁도 {(def.turbidity > 0.3f ? "높음" : def.turbidity > 0.05f ? "약간" : "맑음")} · 밀도 {def.density:0.00}\n" +
                    $"획득처: {def.source}" + (seen ? "" : "\n아직 보유한 적이 없어 제조에 쓸 수 없습니다.");
                UiFactory.AddText(UiFactory.Place("Body", card, new Vector2(0f, 1f), new Vector2(14f, -120f), new Vector2(422f, 310f)), body2, 18, TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.92f));
                if (++col == 4) { col = 0; y += 450f; }
            }
            if (col != 0) y += 450f;
            var aux = UiFactory.Place("Aux", content, new Vector2(0f, 1f), new Vector2(10f, -y), new Vector2(1830f, 80f));
            UiFactory.AddText(aux, "물·기본 얼음: 제조 보조 자원으로 무료 제공(배합에 기록). 물은 색을 희석하고 버프 기여가 없습니다. 탄산·우유·과일 등은 후속 확장 예정입니다.", 20, TextAnchor.MiddleLeft, new Color(0.8f, 0.9f, 1f));
            content.sizeDelta = new Vector2(0f, y + 90f);
        }

        // ═════ 레시피 탭 ═════

        void BuildRecipes()
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var listRt = UiFactory.Place("RecipeList", body, new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(1100f, 930f));
            var content = MakeScroll(listRt);
            float y = 0f;
            // 처음에도 고를 수 있게: 내장 예시 배합(저장 목록과 별개, 불러오기만 — 제조·소모 없음).
            UiFactory.AddText(UiFactory.Place("ExamplesTitle", content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1060f, 36f)),
                "기본 예시 배합 (불러와서 그대로 또는 고쳐서 제조 · 선택만으로는 재료를 쓰지 않습니다)", 20, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            y += 42f;
            var presets = DrinkLab.ExamplePresets;
            for (int i = 0; i < presets.Count; i++)
            {
                int index = i;
                var r = presets[i];
                var look = DrinkCalculator.Look(r);
                var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
                var row = UiFactory.Place("Example_" + i, content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1080f, 120f));
                UiFactory.AddImage(row, new Color(0.13f, 0.16f, 0.14f));
                UiFactory.AddImage(UiFactory.Place("Swatch", row, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(50f, 90f)), look.color).raycastTarget = false;
                var info = UiFactory.AddText(UiFactory.Place("Info", row, new Vector2(0f, 1f), new Vector2(76f, -8f), new Vector2(760f, 106f)),
                    $"{r.name}  ·  {(r.Kind == DrinkKind.Tea ? "차" : "칵테일")} · {DrinkRules.TechniqueName(r.Technique)}\n{RecipeLine(r)}\n버프: {DrinkCalculator.Buff(r).Describe()}" +
                    (shortages.Count > 0 ? $"\n<color=#ff8a80>재료 부족: {ShortageLine(shortages)}</color>" : ""), 18, TextAnchor.UpperLeft, Color.white);
                info.supportRichText = true;
                MakeButton(row, "UseExample", new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(200f, 50f), "불러오기", 20, new Color(0.3f, 0.45f, 0.3f), () => LoadPreset(index));
                y += 128f;
            }
            y += 10f;
            var recipes = DrinkLab.Recipes;
            UiFactory.AddText(UiFactory.Place("Count", body, new Vector2(1f, 1f), new Vector2(-30f, -10f), new Vector2(700f, 30f)), $"저장한 레시피 {recipes.Count} / {DrinkRules.RecipeLimit}", 20, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            for (int i = 0; i < recipes.Count; i++)
            {
                int index = i;
                var r = recipes[i];
                var check = DrinkValidator.Check(r);
                var buff = DrinkCalculator.Buff(r); // 저장값이 아니라 현재 밸런스로 재계산
                var look = DrinkCalculator.Look(r);
                var shortages = DrinkValidator.Shortages(r, DrinkLab.Owned);
                var row = UiFactory.Place("Recipe_" + i, content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1080f, 150f));
                UiFactory.AddImage(row, new Color(0.15f, 0.12f, 0.19f));
                UiFactory.AddImage(UiFactory.Place("Swatch", row, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(50f, 110f)), look.totalVolume > 0 ? look.color : Color.clear).raycastTarget = false;
                string info = $"{r.name}  ·  {(r.Kind == DrinkKind.Tea ? "차" : "칵테일")} · {DrinkRules.TechniqueName(r.Technique)}\n{RecipeLine(r)}\n버프: {buff.Describe()}" +
                              (!check.Ok ? $"\n<color=#ff8a80>사용 불가: {check.First}</color>" : shortages.Count > 0 ? $"\n<color=#ff8a80>재료 부족: {ShortageLine(shortages)}</color>" : "");
                var t = UiFactory.AddText(UiFactory.Place("Info", row, new Vector2(0f, 1f), new Vector2(76f, -8f), new Vector2(640f, 136f)), info, 18, TextAnchor.UpperLeft, Color.white);
                t.supportRichText = true;
                MakeButton(row, "Use", new Vector2(1f, 1f), new Vector2(-218f, -10f), new Vector2(200f, 40f), "불러와 제조", 18, new Color(0.5f, 0.32f, 0.14f), () => LoadRecipe(index, false));
                MakeButton(row, "Edit", new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(200f, 40f), "수정", 18, new Color(0.3f, 0.3f, 0.45f), () => LoadRecipe(index, true));
                MakeButton(row, "Copy", new Vector2(1f, 1f), new Vector2(-218f, -56f), new Vector2(200f, 40f), "공유 코드 복사", 18, new Color(0.22f, 0.36f, 0.4f), () => CopyCode(index));
                MakeButton(row, "Dup", new Vector2(1f, 1f), new Vector2(-10f, -56f), new Vector2(200f, 40f), "복제", 18, new Color(0.25f, 0.25f, 0.3f), () => { if (DrinkLab.DuplicateRecipe(index, out var e)) Message("복제했습니다."); else Message(e); SetTab(Tab.Recipes); });
                MakeButton(row, "Delete", new Vector2(1f, 1f), new Vector2(-10f, -102f), new Vector2(200f, 40f), "삭제", 18, new Color(0.45f, 0.18f, 0.18f), () => { if (DrinkLab.DeleteRecipe(index, out var e)) Message("삭제했습니다."); else Message(e); SetTab(Tab.Recipes); });
                y += 160f;
            }
            if (recipes.Count == 0)
                UiFactory.AddText(UiFactory.Place("Empty", content, new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(1000f, 100f)), "저장한 레시피가 없습니다. 제조 탭에서 배합 후 \"레시피 저장\"을 누르세요(재료 소모 없음).", 22, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
            content.sizeDelta = new Vector2(0f, Mathf.Max(y + (recipes.Count == 0 ? 110f : 0f), 120f));

            // 가져오기
            var imp = UiFactory.Place("Import", body, new Vector2(1f, 1f), new Vector2(-30f, -50f), new Vector2(700f, 880f));
            UiFactory.AddImage(imp, new Color(0.1f, 0.08f, 0.12f, 0.92f));
            UiFactory.AddText(UiFactory.Place("Title", imp, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(660f, 36f)), "공유 코드 가져오기", 26, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            UiFactory.AddText(UiFactory.Place("Help", imp, new Vector2(0f, 1f), new Vector2(16f, -50f), new Vector2(660f, 60f)), "다른 사람이 복사한 코드(CCT1-…)를 붙여 넣고 확인하세요. 미리보기 후 저장하며 창고·버프는 바뀌지 않습니다. 제조할 때는 내 재료를 씁니다.", 17, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.85f));
            importField = MakeInput(imp, "Code", new Vector2(0f, 1f), new Vector2(16f, -120f), new Vector2(668f, 120f), "CCT1-…", 20);
            importField.lineType = InputField.LineType.MultiLineNewline;
            importField.characterLimit = DrinkShareCode.MaxPasteLength;
            MakeButton(imp, "Paste", new Vector2(0f, 1f), new Vector2(16f, -250f), new Vector2(200f, 50f), "클립보드 붙여넣기", 18, new Color(0.25f, 0.25f, 0.3f), () => importField.text = GUIUtility.systemCopyBuffer ?? "");
            MakeButton(imp, "Check", new Vector2(0f, 1f), new Vector2(226f, -250f), new Vector2(200f, 50f), "코드 확인", 20, new Color(0.3f, 0.3f, 0.45f), () => PreviewImport(importField.text));
            MakeButton(imp, "SaveImport", new Vector2(0f, 1f), new Vector2(436f, -250f), new Vector2(248f, 50f), "레시피로 저장", 20, new Color(0.5f, 0.32f, 0.14f), () => SaveImport());
            var prev = UiFactory.AddText(UiFactory.Place("Preview", imp, new Vector2(0f, 1f), new Vector2(16f, -316f), new Vector2(668f, 540f)), "", 19, TextAnchor.UpperLeft, Color.white);
            prev.supportRichText = true;
            prev.name = "ImportPreviewText";
            previewText = prev;
            RenderImportPreview(null);
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

        static RectTransform MakeScroll(RectTransform area)
        {
            UiFactory.AddImage(area, new Color(0f, 0f, 0f, 0.25f));
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
            return content;
        }
    }
}
