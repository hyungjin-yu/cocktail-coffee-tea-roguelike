using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 72 §7 연구 노트북(손 없음): 닫힌 노트북 등장 0.25초 → 덮개 열기 0.35초 → 화면 점등 0.15초(ready 아트 프레임 50ms) → 화면 안 실제 UI.
    /// 닫기는 덮개 닫기 0.35초. 연출 중에는 화면 안 버튼이 꺼져 있어 중복 입력·이중 거래가 없고, 클릭/Enter/Space로 끝 상태로 건너뛴다.
    /// 모든 클립은 공통 배율·실제 경첩(hinge) 피벗으로 맞춘다(캔버스 크기가 다른 클립을 같은 사각형에 늘리지 않는다).
    /// 화면 안: 기존 강화(24종, 기존 화면 열기) / 숙련 트리(3가지 × 4단계, 해금과 장착은 별도) / 직원 채용(칵테일·차 한 화면).
    /// </summary>
    public class ResearchLaptopView : MonoBehaviour
    {
        public enum Tab { Upgrades, Mastery, Staff }
        public enum Phase { Opening, Open, Closing, Closed }

        public const float FrameSeconds = 0.05f, ArtScale = 4f;
        static readonly Vector2 Hinge = new Vector2(0f, -322f); // 화면 가운데 기준 경첩 위치(노트북 전체가 1080 안)
        const float HingeX = 192f, HingeY = 204f; // 아트 좌상단 원점 px

        Canvas canvas;
        RectTransform root, screen;
        Image laptop;
        CanvasGroup screenGroup;
        Action onClosed, onOpenPermanent;
        Coroutine anim;
        Job job = Job.Bartender;
        bool skip;
        Tab tab = Tab.Mastery;
        string branch = DrinkResearch.Service;
        string selectedTrait = "service_1", selectedStaff = "staff_cocktail_aroma";
        RectTransform body;
        Text rpText, message;
        readonly Dictionary<Tab, Button> tabButtons = new Dictionary<Tab, Button>();

        public Phase State { get; private set; } = Phase.Opening;
        public Tab CurrentTab => tab;
        public string MessageText => message != null ? message.text : "";
        public bool ScreenInteractable => screenGroup != null && screenGroup.interactable;
        public static ResearchLaptopView Current { get; private set; }

        public static ResearchLaptopView Show(Action onClosed, Action onOpenPermanent = null, Job job = Job.Bartender)
        {
            var canvas = UiFactory.CreateCanvas(null, "ResearchLaptop", 150);
            // 좁은 창(가로/세로 1.18 이상): 높이 맞춤 — 화면 안 UI(1024 폭)는 다 보이고 글자는 16:9와 같은 크기(노트북 몸체 양옆만 조금 잘림).
            float aspect = Screen.width / Mathf.Max(1f, (float)Screen.height);
            if (aspect >= DrinkLabView.MinNarrowAspect)
            {
                var adaptive = canvas.GetComponent<AdaptiveCanvasScaler>();
                if (adaptive != null) { adaptive.enabled = false; Destroy(adaptive); }
                var sc = canvas.GetComponent<CanvasScaler>();
                if (sc != null) sc.matchWidthOrHeight = 1f;
            }
            var v = canvas.gameObject.AddComponent<ResearchLaptopView>();
            v.canvas = canvas;
            v.onClosed = onClosed;
            v.onOpenPermanent = onOpenPermanent;
            v.job = job;
            v.Build();
            Current = v;
            return v;
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void Build()
        {
            root = UiFactory.Stretch("Root", canvas.transform);
            // 입력 차단층(보이지 않음)과 배경 그림을 분리: 뒤 메뉴는 눌리지 않고, 화면은 직업별 작업 공간(바/찻집) — 검은 빈 배경·검은 번쩍임 없음.
            var bg = UiFactory.AddImage(root, new Color(0f, 0f, 0f, 0f));
            bg.raycastTarget = true;
            BuildRoom();
            var skipArea = root.gameObject.AddComponent<Button>(); // 연출 중 아무 곳이나 누르면 끝 상태로
            skipArea.transition = Selectable.Transition.None;
            skipArea.onClick.AddListener(() => { if (State == Phase.Opening || State == Phase.Closing) skip = true; });
            var lr = UiFactory.Place("Laptop", root, new Vector2(0.5f, 0.5f), Hinge, Vector2.one);
            laptop = lr.gameObject.AddComponent<Image>();
            laptop.raycastTarget = false;
            // 화면 안 실제 UI: laptop_open contentRect (64,36,256,144) → 경첩 기준 위치. 열린 뒤에만 보이고 눌린다.
            screen = UiFactory.Place("Screen", root, new Vector2(0.5f, 0.5f), Hinge + new Vector2((64f - HingeX) * ArtScale, (HingeY - 36f) * ArtScale), new Vector2(256f * ArtScale, 144f * ArtScale));
            screen.pivot = new Vector2(0f, 1f);
            screenGroup = screen.gameObject.AddComponent<CanvasGroup>();
            SetScreen(false);
            BuildScreen();
            anim = StartCoroutine(OpenRoutine());
        }

        /// <summary>
        /// 노트북이 놓인 작업 공간: 바텐더 = 바 벽+호두 상판, 티 소믈리에 = 찻집 벽+삼나무 상판(제조 전용 배경 아트 재사용, 정수 배율·늘림 없음).
        /// 상판 윗면 = 노트북 아트의 책상 접점(tableContact y=252)이라 노트북이 상판 위에 놓인다. 등장·열기·닫기 내내 그대로.
        /// </summary>
        void BuildRoom()
        {
            bool tea = job == Job.TeaSommelier;
            var wall = DrinkArt.Sprite(tea ? "dl_craft_tea_wall" : "dl_craft_bar_wall");
            var table = DrinkArt.Sprite(tea ? "dl_craft_tea_table" : "dl_craft_bar_table");
            float canvasW = 1080f * Screen.width / Mathf.Max(1f, Screen.height);
            int k = Mathf.Max(5, Mathf.CeilToInt(Mathf.Max(1920f, canvasW) / 400f));
            float tableTop = Hinge.y - (TableContactY - HingeY) * ArtScale;
            if (wall != null)
            {
                var wr = UiFactory.Place("RoomWall", root, new Vector2(0.5f, 0.5f), new Vector2(0f, tableTop), new Vector2(wall.rect.width * k, wall.rect.height * k));
                wr.pivot = new Vector2(0.5f, 0f);
                var wi = wr.gameObject.AddComponent<Image>(); wi.sprite = wall; wi.raycastTarget = false;
            }
            else UiFactory.AddImage(UiFactory.Stretch("RoomFill", root), tea ? new Color(0.36f, 0.27f, 0.2f) : new Color(0.2f, 0.12f, 0.09f)).raycastTarget = false;
            if (table != null)
            {
                var tr = UiFactory.Place("RoomTable", root, new Vector2(0.5f, 0.5f), new Vector2(0f, tableTop), new Vector2(table.rect.width * k, 1080f));
                tr.pivot = new Vector2(0.5f, 1f);
                // 상판 띠(정수 배율) 아래는 같은 나무색 앞면으로 화면 끝까지(빈 검은 띠 없음)
                var front = UiFactory.AddImage(tr, tea ? new Color(0.33f, 0.23f, 0.15f) : new Color(0.16f, 0.1f, 0.07f));
                front.raycastTarget = false;
                var top = UiFactory.Place("Top", tr, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(table.rect.width * k, table.rect.height * k));
                top.pivot = new Vector2(0.5f, 1f);
                var ti = top.gameObject.AddComponent<Image>(); ti.sprite = table; ti.raycastTarget = false;
            }
        }

        const float TableContactY = 252f;

        void SetScreen(bool on)
        {
            screenGroup.alpha = on ? 1f : 0f;
            screenGroup.interactable = on;
            screenGroup.blocksRaycasts = on;
        }

        /// <summary>클립 프레임: 아트 캔버스 크기·경첩 피벗을 프레임마다 맞춘다(384×256 / 384×288).</summary>
        void ShowFrame(string id)
        {
            var sp = DrinkPaperUi.Sprite(id);
            laptop.sprite = sp;
            laptop.enabled = sp != null;
            if (sp == null) return;
            var rt = laptop.rectTransform;
            float w = sp.rect.width, h = sp.rect.height;
            rt.pivot = new Vector2(HingeX / w, 1f - HingeY / h);
            rt.sizeDelta = new Vector2(w * ArtScale, h * ArtScale);
            rt.anchoredPosition = Hinge;
        }

        IEnumerator Play(string clip, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                if (skip) yield break;
                ShowFrame($"{clip}_{i:00}");
                float t = 0f;
                while (t < FrameSeconds && !skip) { t += Time.unscaledDeltaTime; yield return null; }
            }
        }

        IEnumerator OpenRoutine()
        {
            State = Phase.Opening;
            skip = false;
            if (!skip) yield return Play("laptop_arrive", 5);   // 0.25초
            if (!skip) yield return Play("laptop_opening", 7);  // 0.35초
            if (!skip) yield return Play("laptop_light", 3);    // 0.15초
            ShowFrame("laptop_open");
            skip = false;
            State = Phase.Open;
            Refresh();
            SetScreen(true);
            anim = null;
        }

        /// <summary>닫기(0.35초, 연출 중 입력 차단). 이미 닫는 중이면 무시.</summary>
        public void Close()
        {
            if (State == Phase.Closing || State == Phase.Closed) return;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(CloseRoutine(onClosed));
        }

        IEnumerator CloseRoutine(Action after)
        {
            SetScreen(false);
            State = Phase.Closing;
            skip = false;
            yield return Play("laptop_closing", 7);
            State = Phase.Closed;
            Destroy(canvas.gameObject);
            after?.Invoke();
        }

        /// <summary>테스트·건너뛰기: 연출을 끝 상태로.</summary>
        public void SkipAnimation() { if (State == Phase.Opening || State == Phase.Closing) skip = true; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if ((State == Phase.Opening || State == Phase.Closing) && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) skip = true;
            else if (State == Phase.Open && kb.escapeKey.wasPressedThisFrame) Close();
        }

        // ───── 화면 안 UI(1024×576) ─────

        const float W = 256f * ArtScale, H = 144f * ArtScale;
        static readonly Color ScreenBg = new Color(0.1f, 0.13f, 0.17f, 1f), Line = new Color(0.42f, 0.55f, 0.66f, 0.6f);
        static readonly Color TextMain = new Color(0.92f, 0.95f, 1f), TextMuted = new Color(0.66f, 0.74f, 0.82f), Good = new Color(0.55f, 0.9f, 0.6f), Bad = new Color(1f, 0.6f, 0.55f), Accent = new Color(1f, 0.85f, 0.55f);

        void BuildScreen()
        {
            UiFactory.AddImage(screen, ScreenBg).raycastTarget = true;
            float x = 12f;
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var tt = t;
                tabButtons[t] = DrinkPaperUi.Button(screen, "LaptopTab" + t, new Vector2(0f, 1f), new Vector2(x, -10f), new Vector2(196f, 64f), TabName(t), DrinkPaperUi.Body, () => SetTab(tt));
                x += 206f;
            }
            rpText = Label(screen, "Rp", new Vector2(640f, -10f), new Vector2(232f, 64f), "", DrinkPaperUi.Body, Accent, TextAnchor.MiddleRight);
            DrinkPaperUi.Button(screen, "LaptopClose", new Vector2(0f, 1f), new Vector2(W - 140f, -10f), new Vector2(128f, 64f), "닫기", DrinkPaperUi.Body, Close);
            UiFactory.AddImage(UiFactory.Place("TopRule", screen, new Vector2(0f, 1f), new Vector2(12f, -80f), new Vector2(W - 24f, 2f)), Line).raycastTarget = false;
            message = Label(screen, "Message", new Vector2(12f, -H + 34f), new Vector2(W - 24f, 30f), "", DrinkPaperUi.Small, Accent, TextAnchor.MiddleLeft);
        }

        static string TabName(Tab t) => t switch { Tab.Upgrades => "기존 강화", Tab.Mastery => "숙련 트리", _ => "직원 채용" };

        public void SetTab(Tab t)
        {
            if (State != Phase.Open) return;
            tab = t;
            message.text = "";
            Refresh();
        }

        static Text Label(RectTransform parent, string name, Vector2 pos, Vector2 size, string text, int fontSize, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            size.y = Mathf.Max(size.y, fontSize * 1.6f); // 한 줄 높이보다 작은 상자는 잘림(Truncate)으로 글이 통째로 사라진다
            var t = UiFactory.AddText(UiFactory.Place(name, parent, new Vector2(0f, 1f), pos, size), text, fontSize, anchor, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        void Refresh()
        {
            var r = DrinkLab.Research;
            rpText.text = $"연구 포인트 {r.researchPoints}";
            foreach (var kv in tabButtons) DrinkPaperUi.SetSelected(kv.Value, kv.Key == tab);
            if (body != null) { body.gameObject.SetActive(false); Destroy(body.gameObject); }
            body = UiFactory.Place("Body", screen, new Vector2(0f, 1f), new Vector2(0f, -84f), new Vector2(W, H - 84f - 40f));
            body.pivot = new Vector2(0f, 1f);
            switch (tab)
            {
                case Tab.Upgrades: BuildUpgrades(); break;
                case Tab.Mastery: BuildMastery(r); break;
                default: BuildStaff(r); break;
            }
        }

        void Act(Func<string> action)
        {
            if (State != Phase.Open) return;
            string error = action();
            if (error != null) message.text = error;
            Refresh();
            if (error == null) message.text = "저장했습니다.";
        }

        // ── 기존 강화 ──
        void BuildUpgrades()
        {
            int sp = PermanentProgress.Current != null ? PermanentProgress.Current.specialtyPoints : 0;
            Label(body, "UpTitle", new Vector2(12f, -8f), new Vector2(W - 24f, 44f), "기존 영구 강화 24종 (스페셜티)", DrinkPaperUi.Heading, TextMain);
            Label(body, "UpBody", new Vector2(12f, -60f), new Vector2(W - 24f, 200f),
                $"보유 스페셜티 {sp}\n강화 보스에게서 얻는 스페셜티로 해금하며, 출발할 때 항상 적용됩니다.\n숙련 트리·직원 연구와는 별개입니다(연구 포인트로 사지 않고, 서로 합산하지 않습니다).",
                DrinkPaperUi.Body, TextMuted);
            DrinkPaperUi.Button(body, "OpenPermanent", new Vector2(0f, 1f), new Vector2(12f, -280f), new Vector2(420f, 66f), "영구 강화 화면 열기", DrinkPaperUi.Body, () =>
            {
                if (State != Phase.Open) return;
                var open = onOpenPermanent;
                if (anim != null) StopCoroutine(anim);
                anim = StartCoroutine(CloseRoutine(open ?? onClosed));
            });
        }

        // ── 숙련 트리 ──
        void BuildMastery(ResearchState r)
        {
            float x = 12f;
            foreach (var b in new[] { DrinkResearch.Service, DrinkResearch.Stability, DrinkResearch.Finesse })
            {
                var bb = b;
                var btn = DrinkPaperUi.Button(body, "Branch_" + b, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(170f, 64f), DrinkResearch.BranchName(b), DrinkPaperUi.Body, () => { branch = bb; selectedTrait = DrinkResearch.TraitAt(bb, 1).id; Refresh(); });
                DrinkPaperUi.SetSelected(btn, b == branch);
                x += 180f;
            }
            // 노드 4개(조직도처럼 위→아래 연결). 잠김/해금 가능/해금됨/장착됨은 테두리 아트와 글로 구분(색만으로 구분하지 않음).
            const float nodeW = 420f, nodeH = 74f, gap = 18f, top = -76f;
            for (int tier = 1; tier <= 4; tier++)
            {
                var t = DrinkResearch.TraitAt(branch, tier);
                var state = DrinkResearch.StateOf(r, t);
                float y = top - (tier - 1) * (nodeH + gap);
                if (tier > 1)
                {
                    bool linked = r.unlockedTraits.Contains(DrinkResearch.TraitAt(branch, tier - 1).id);
                    UiFactory.AddImage(UiFactory.Place("Link" + tier, body, new Vector2(0f, 1f), new Vector2(12f + nodeW * 0.5f - 3f, y + gap), new Vector2(6f, gap)), linked ? Accent : Line).raycastTarget = false;
                }
                var tid = t.id;
                var node = DrinkPaperUi.Button(body, "Node_" + t.id, new Vector2(0f, 1f), new Vector2(12f, y), new Vector2(nodeW, nodeH),
                    $"{tier}. {t.name} · {StateShort(state)}", DrinkPaperUi.Body, () => { selectedTrait = tid; Refresh(); }, NodeArt(state));
                // 노드 아트(어두운 판, 9slice 아트 1배: 테두리 좌우 16·위 22·아래 16) 안쪽 칸에 한 줄 밝은 글자. 상태는 테두리 아트와 글로 함께 구분.
                var img = node.targetGraphic as Image;
                if (img != null) img.pixelsPerUnitMultiplier = 1f;
                var nl = node.GetComponentInChildren<Text>();
                var lr = nl.rectTransform;
                lr.offsetMin = new Vector2(20f, 14f); lr.offsetMax = new Vector2(-20f, -20f);
                nl.color = state == DrinkResearch.NodeState.Locked ? TextMuted : TextMain;
                nl.alignment = TextAnchor.MiddleLeft;
                nl.resizeTextMinSize = DrinkPaperUi.Small;
                if (t.id == selectedTrait) { nl.fontStyle = FontStyle.Bold; nl.color = Accent; }
            }
            // 상세
            var sel = DrinkResearch.Trait(selectedTrait) ?? DrinkResearch.TraitAt(branch, 1);
            var st = DrinkResearch.StateOf(r, sel);
            const float dx = 452f, dw = W - dx - 12f;
            Label(body, "DetailName", new Vector2(dx, -76f), new Vector2(dw, 40f), $"{DrinkResearch.BranchName(sel.branch)} {sel.tier}단계 · {sel.name}", DrinkPaperUi.Heading, TextMain);
            Label(body, "DetailEffect", new Vector2(dx, -128f), new Vector2(dw, 92f), sel.effect, DrinkPaperUi.Body, TextMain);
            string pre = sel.Prerequisite != null ? $"선행: {DrinkResearch.Trait(sel.Prerequisite).name}" : "선행 없음";
            Label(body, "DetailCost", new Vector2(dx, -222f), new Vector2(dw, 70f), $"해금 {sel.cost} 연구 포인트 · {pre}\n{StateWord(st, sel)}", DrinkPaperUi.Small, st == DrinkResearch.NodeState.Locked ? Bad : TextMuted);
            var unlock = DrinkPaperUi.Button(body, "Unlock", new Vector2(0f, 1f), new Vector2(dx, -300f), new Vector2(270f, 66f), $"해금 ({sel.cost} RP)", DrinkPaperUi.Body,
                () => Act(() => DrinkLab.UnlockTrait(sel.id, out var e) ? null : e), DrinkResearch.CanUnlock(r, sel.id, out _) ? "button_available" : "button_locked");
            unlock.interactable = st == DrinkResearch.NodeState.Available;
            bool equipped = st == DrinkResearch.NodeState.Equipped;
            var equip = DrinkPaperUi.Button(body, "Equip", new Vector2(0f, 1f), new Vector2(dx + 280f, -300f), new Vector2(270f, 66f), equipped ? "장착 해제" : "장착", DrinkPaperUi.Body,
                () => Act(() => equipped ? (DrinkLab.UnequipTrait(sel.id, out var e) ? null : e) : (DrinkLab.EquipTrait(sel.id, out var e2) ? null : e2)), equipped ? "button_equipped" : "button");
            equip.interactable = st == DrinkResearch.NodeState.Owned || equipped;
            var names = new List<string>();
            foreach (var id in r.equippedTraits) { var t = DrinkResearch.Trait(id); if (t != null) names.Add(t.name); }
            Label(body, "Slots", new Vector2(dx, -376f), new Vector2(dw, 76f), $"장착 슬롯 {names.Count}/{DrinkResearch.Slots}: {(names.Count > 0 ? string.Join(" · ", names) : "비어 있음")}\n출발할 때 장착한 특성만 그 판에 적용", DrinkPaperUi.Small, Accent);
        }

        static string NodeArt(DrinkResearch.NodeState s) => s switch
        {
            DrinkResearch.NodeState.Locked => "tree_node_locked",
            DrinkResearch.NodeState.Available => "tree_node_available",
            DrinkResearch.NodeState.Owned => "tree_node_owned",
            _ => "tree_node_equipped",
        };

        static string StateShort(DrinkResearch.NodeState s) => s switch
        {
            DrinkResearch.NodeState.Locked => "잠김",
            DrinkResearch.NodeState.Available => "해금 가능",
            DrinkResearch.NodeState.Owned => "해금됨",
            _ => "장착됨",
        };

        static string StateWord(DrinkResearch.NodeState s, MasteryTrait t) => s switch
        {
            DrinkResearch.NodeState.Locked => "잠김",
            DrinkResearch.NodeState.Available => $"해금 가능 {t.cost}RP",
            DrinkResearch.NodeState.Owned => "해금됨",
            _ => "장착됨",
        };

        // ── 직원 채용(칵테일·차 한 화면) ──
        void BuildStaff(ResearchState r)
        {
            const float cardScale = 1.75f, cw = 160f * cardScale, ch = 112f * cardScale;
            Label(body, "ColCocktail", new Vector2(12f, 0f), new Vector2(cw, 40f), "칵테일 연구", DrinkPaperUi.Heading, Accent);
            Label(body, "ColTea", new Vector2(24f + cw, 0f), new Vector2(cw, 40f), "차 연구", DrinkPaperUi.Heading, Accent);
            int ci = 0, ti = 0;
            foreach (var s in DrinkResearch.Staff)
            {
                bool tea = s.area == "tea";
                int row = tea ? ti++ : ci++;
                var pos = new Vector2(tea ? 24f + cw : 12f, -50f - row * (ch + 6f));
                var sid = s.id;
                var state = StaffState(r, s);
                var card = UiFactory.Place("Staff_" + s.id, body, new Vector2(0f, 1f), pos, new Vector2(cw, ch));
                var img = DrinkPaperUi.Uniform(card, StaffArt(state), cardScale);
                img.raycastTarget = true;
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                UiFactory.AddFocusSfx(btn);
                btn.onClick.AddListener(() => { selectedStaff = sid; Refresh(); });
                var portrait = UiFactory.Place("Portrait", card, new Vector2(0f, 1f), new Vector2(18f * cardScale, -27f * cardScale), Vector2.zero);
                DrinkPaperUi.Uniform(portrait, s.portrait, cardScale);
                var text = UiFactory.Place("Text", card, new Vector2(0f, 1f), new Vector2(62f * cardScale, -24f * cardScale), new Vector2(72f * cardScale + 14f, 64f * cardScale));
                var t = UiFactory.AddText(text, $"{s.name}\n<size={DrinkPaperUi.Small}>{s.research}\n{StaffWord(state, s)}</size>", DrinkPaperUi.Body, TextAnchor.UpperLeft, state == StaffUi.Locked ? TextMuted : TextMain);
                t.supportRichText = true; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                if (s.id == selectedStaff) { t.fontStyle = FontStyle.Bold; t.color = Accent; }
            }
            // 상세: 미리보기는 무료(열람만으로 해금·소모 없음), 채용은 버튼으로만.
            var sel = DrinkResearch.StaffById(selectedStaff) ?? DrinkResearch.Staff[0];
            var st = StaffState(r, sel);
            float dx = 36f + 2f * cw, dw = W - dx - 12f;
            Label(body, "StaffName", new Vector2(dx, 0f), new Vector2(dw, 40f), $"{(sel.area == "tea" ? "차" : "칵테일")} · {sel.name}", DrinkPaperUi.Heading, TextMain);
            string cond = sel.craftsRequired > 0 ? $"조건: {(sel.area == "tea" ? "차" : "칵테일")} 제조 {sel.craftsRequired}회 (현재 {DrinkResearch.CraftsOf(r, sel.area)}회)" : "조건 없음";
            Label(body, "StaffBody", new Vector2(dx, -50f), new Vector2(dw, 224f), $"보유 연구: {sel.research}\n{sel.effect}\n{cond}\n채용 {sel.cost} RP · 한 번 채용하면 영구 확보(월급·퇴사 없음)", DrinkPaperUi.Body, TextMain);
            bool hired = r.hiredStaff.Contains(sel.id), active = r.activeSpecialty == sel.id;
            var hire = DrinkPaperUi.Button(body, "Hire", new Vector2(0f, 1f), new Vector2(dx, -282f), new Vector2(dw, 66f), hired ? "채용함" : $"채용 ({sel.cost} RP)", DrinkPaperUi.Body,
                () => Act(() => DrinkLab.HireStaff(sel.id, out var e) ? null : e), hired ? "button_owned" : DrinkResearch.CanHire(r, sel.id, out _) ? "button_available" : "button_locked");
            hire.interactable = !hired && DrinkResearch.CanHire(r, sel.id, out _);
            var spec = DrinkPaperUi.Button(body, "Specialty", new Vector2(0f, 1f), new Vector2(dx, -356f), new Vector2(dw, 66f), active ? "제조 전문 해제" : "제조 전문으로 활성", DrinkPaperUi.Body,
                () => Act(() => DrinkLab.SetSpecialty(active ? "" : sel.id, out var e) ? null : e), active ? "button_equipped" : "button");
            spec.interactable = hired;
            var cur = DrinkResearch.StaffById(r.activeSpecialty);
            Label(body, "ActiveSpecialty", new Vector2(dx, -420f), new Vector2(dw, 38f), $"활성 전문: {(cur != null ? cur.research : "없음")} · 한 번에 하나", DrinkPaperUi.Small, Accent);
        }

        enum StaffUi { Locked, Available, Owned, Active }

        static StaffUi StaffState(ResearchState r, ResearchStaff s) =>
            r.activeSpecialty == s.id ? StaffUi.Active : r.hiredStaff.Contains(s.id) ? StaffUi.Owned
            : DrinkResearch.CraftsOf(r, s.area) >= s.craftsRequired ? StaffUi.Available : StaffUi.Locked;

        static string StaffArt(StaffUi s) => s switch
        {
            StaffUi.Locked => "staff_card_locked",
            StaffUi.Available => "staff_card_available",
            StaffUi.Owned => "staff_card_owned",
            _ => "staff_card_equipped",
        };

        static string StaffWord(StaffUi s, ResearchStaff st) => s switch
        {
            StaffUi.Locked => $"조건 미달 · {st.cost}RP",
            StaffUi.Available => $"채용 가능 · {st.cost}RP",
            StaffUi.Owned => "채용함",
            _ => "전문 활성",
        };
    }
}
