using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 73 사용자: 저장 레시피 = 서랍 속 종이 서류(손·몸 없음, 서랍과 종이만 움직인다). Codex RecipeDrawer-2026-10-19(ready, root 수락) 기하 그대로:
    /// 480×320 공통 피벗 body/front/closed, 정수 배율. 층 = body → 꽂힌 종이(실제 글자, 상단 176행까지만 보이게 잘림) → front → 꺼낸 종이(overlay).
    /// 열기: 같은 front 그림을 위로 51 → 0 이동(손잡이 크기·피벗 튐 없음), 잘림 경계 = 176 − 51×(1−진행). 종이 = 기존 찢긴 레시피(torn_recipe) 재사용,
    /// 꺼내기 경로 selected → pull_mid → clear_rim(앞판 위로 완전히 나옴) → unfolded(overlay). 되돌리기는 역순, 종이 아래끝이 176 위일 때 다시 앞판 뒤로.
    /// 30장은 5장씩 칸(구분 탭 = divider 아트)으로 나눈다. 모션 중 다른 선택은 무시하고 같은 입력으로 끝 상태로 건너뛴다.
    /// </summary>
    public class RecipeDrawerView : MonoBehaviour
    {
        public enum Phase { Closed, Opening, Open, Pulling, Sheet, Returning, Closing }
        public const int PerPage = 5;
        public const float OpenSeconds = 0.45f, PullSeconds = 0.45f, ReturnSeconds = 0.4f;

        [Serializable] class Slot { public int id; public float[] position; public float[] size; }
        [Serializable] class PathPose { public string phase; public float[] position; public float[] size; public string layer; public bool textVisible; public float[] textSafeRect; }
        [Serializable] class Layout { public float[] canvas; public Slot[] paperSlots; public PathPose[] paperPath; }

        RectTransform area, rig, stackClip, overlay, front, pager, frontHitbox;
        public RectTransform FrontHitbox => frontHitbox;
        Image closedImg, bodyImg, frontImg;
        Text caption, emptyLabel;
        Func<IReadOnlyList<DrinkRecipe>> recipes;
        Func<int, RectTransform, bool> buildSheet;
        Action<string> message;
        Layout layout;
        float k = 2f, open01;
        readonly List<RectTransform> papers = new List<RectTransform>();
        RectTransform pulled;
        Coroutine motion;
        bool skip;

        public Phase State { get; private set; } = Phase.Closed;
        public int Page { get; private set; }
        public int Selected { get; private set; } = -1;
        public int PageCount => Mathf.Max(1, (recipes().Count + PerPage - 1) / PerPage);
        public RectTransform PulledSheet => pulled;
        public float Scale => k;

        public static RecipeDrawerView Create(RectTransform area, Func<IReadOnlyList<DrinkRecipe>> recipes, Func<int, RectTransform, bool> buildSheet, bool startOpen, Action<string> message = null)
        {
            var v = area.gameObject.AddComponent<RecipeDrawerView>();
            v.area = area; v.recipes = recipes; v.buildSheet = buildSheet; v.message = message;
            v.Build(startOpen);
            return v;
        }

        void Build(bool startOpen)
        {
            var ta = Resources.Load<TextAsset>(DrinkPaperUi.Folder + "recipe_drawer_layout");
            layout = ta != null ? JsonUtility.FromJson<Layout>(ta.text) : null;
            if (layout == null || layout.paperSlots == null || layout.paperPath == null) { Debug.LogWarning("[RecipeDrawer] layout 없음"); return; }
            var size = area.rect.size;
            const float pagerH = 70f, controlsH = 76f;
            k = Mathf.Max(1f, Mathf.Floor(Mathf.Min(size.x / 480f, (size.y - pagerH - controlsH) / 320f)));
            pager = UiFactory.Place("Pager", area, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size.x, pagerH));
            rig = UiFactory.Place("DrawerRig", area, new Vector2(0.5f, 1f), new Vector2(0f, -pagerH), new Vector2(480f * k, 320f * k));
            rig.pivot = new Vector2(0.5f, 1f);
            closedImg = Art("Closed", rig, "dl_recipe_drawer_closed");
            bodyImg = Art("Body", rig, "dl_recipe_drawer_body");
            stackClip = At("Stack", rig, 0f, 0f, 480f, 176f);
            stackClip.gameObject.AddComponent<RectMask2D>();
            emptyLabel = UiFactory.AddText(At("Empty", rig, 96f, 100f, 288f, 70f), "서랍이 비어 있습니다 — 제조 탭에서 레시피를 저장하면 여기에 꽂힙니다.", DrinkPaperUi.Small, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.8f));
            emptyLabel.horizontalOverflow = HorizontalWrapMode.Wrap; emptyLabel.raycastTarget = false;
            frontImg = Art("Front", rig, "dl_recipe_drawer_front");
            front = frontImg.rectTransform;
            // 앞판 누르기 = 열기/닫기(모션 중이면 끝 상태로). 480×320 전체 그림은 입력을 받지 않고(위쪽 투명 칸의 종이 클릭을 막지 않게),
            // 실제 앞판 사각형(frontOcclusionRect 22,176,435,86)만 누름 영역 — front와 같은 부모라 열고 닫힐 때 앞판을 따라 51 움직인다.
            frontHitbox = At("FrontHitbox", front, 22f, 176f, 435f, 86f);
            var hit = frontHitbox.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var fb = frontHitbox.gameObject.AddComponent<Button>();
            fb.targetGraphic = hit;
            fb.transition = Selectable.Transition.None;
            fb.onClick.AddListener(ToggleDrawer);
            overlay = UiFactory.Stretch("Overlay", rig);
            var controls = UiFactory.Place("Controls", area, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(size.x, 66f));
            caption = UiFactory.AddText(UiFactory.Place("Caption", controls, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(Mathf.Max(240f, size.x - 610f), 66f)), "", DrinkPaperUi.Small - 2, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            DrinkPaperUi.Button(controls, "PrevPaper", new Vector2(1f, 0.5f), new Vector2(-480f, 0f), new Vector2(110f, 66f), "◀", DrinkPaperUi.Body, () => Step(-1));
            DrinkPaperUi.Button(controls, "NextPaper", new Vector2(1f, 0.5f), new Vector2(-360f, 0f), new Vector2(110f, 66f), "▶", DrinkPaperUi.Body, () => Step(1));
            DrinkPaperUi.Button(controls, "PullPaper", new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(350f, 66f), "꺼내 펼치기", DrinkPaperUi.Body, () => { if (Selected >= 0) Pull(Selected); });
            open01 = startOpen ? 1f : 0f;
            State = startOpen ? Phase.Open : Phase.Closed;
            RebuildPapers();
            ApplyOpen();
        }

        Image Art(string name, RectTransform parent, string id)
        {
            var rt = UiFactory.Stretch(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrinkPaperUi.Sprite(id);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>아트 좌상단 px 사각형 → rig 안 위치(정수 배율).</summary>
        RectTransform At(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0f, 1f), new Vector2(x * k, -y * k), new Vector2(w * k, h * k));
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        void Pose(RectTransform rt, float[] pos, float[] size)
        {
            rt.anchoredPosition = new Vector2(pos[0] * k, -pos[1] * k);
            rt.sizeDelta = new Vector2(size[0] * k, size[1] * k);
        }

        PathPose PathAt(string phase) { foreach (var p in layout.paperPath) if (p.phase == phase) return p; return null; }

        static float[] Lerp(float[] a, float[] b, float t) => new[] { Mathf.Lerp(a[0], b[0], t), Mathf.Lerp(a[1], b[1], t) };

        /// <summary>열림 정도 반영: 닫힌 그림 ↔ (body + 같은 front를 위로 51 이동), 종이 잘림 경계 176 − 51×(1−진행).</summary>
        void ApplyOpen()
        {
            if (layout == null) return;
            bool shut = open01 <= 0.001f;
            closedImg.enabled = shut;
            bodyImg.enabled = !shut;
            frontImg.enabled = !shut;
            front.anchoredPosition = new Vector2(0f, 51f * k * (1f - open01));
            stackClip.sizeDelta = new Vector2(480f * k, Mathf.Max(0f, (176f - 51f * (1f - open01)) * k));
            stackClip.gameObject.SetActive(!shut);
            emptyLabel.gameObject.SetActive(!shut && recipes().Count == 0);
            RefreshCaption();
        }

        void RefreshCaption()
        {
            int n = recipes().Count;
            var list = recipes();
            string sel = Selected >= 0 && Selected < n ? $"고른 종이: {list[Selected].name}" : n > 0 ? "종이를 눌러 고르기(◀ ▶)" : "";
            caption.text = State == Phase.Closed ? $"앞판을 눌러 서랍 열기 · {n}/{DrinkRules.RecipeLimit}장" : $"{sel}  ·  칸 {Page + 1}/{PageCount}";
        }

        /// <summary>현재 칸(5장)의 종이를 슬롯에 꽂는다(아래 깊이일수록 앞). 꽂힌 종이 글자는 숨기고, 고른 종이만 살짝 올라와 제목이 보인다.</summary>
        public void RebuildPapers()
        {
            foreach (var p in papers) if (p != null) Destroy(p.gameObject);
            papers.Clear();
            BuildPager();
            var list = recipes();
            Page = Mathf.Clamp(Page, 0, PageCount - 1);
            for (int s = 0; s < PerPage; s++)
            {
                int index = Page * PerPage + s;
                if (index >= list.Count) break;
                var slot = layout.paperSlots[Mathf.Min(s, layout.paperSlots.Length - 1)];
                var paper = MakePaper("Paper_" + index, stackClip, index, list[index]);
                Pose(paper, slot.position, slot.size);
                papers.Add(paper);
            }
            UpdateSelectionPose();
            ApplyOpen();
        }

        RectTransform MakePaper(string name, Transform parent, int index, DrinkRecipe r)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrinkPaperUi.Sprite("torn_recipe");
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => OnPaperClicked(index));
            // 종이 위 실제 글자(textSafeRect = 종이 원본 10,20,152,96 비율) — 종이와 함께 움직이고 같은 잘림을 받는다
            var t = UiFactory.Stretch("Title", rt);
            t.anchorMin = new Vector2(10f / 172f, 1f - 116f / 128f); t.anchorMax = new Vector2(162f / 172f, 1f - 20f / 128f);
            t.offsetMin = t.offsetMax = Vector2.zero;
            var txt = UiFactory.AddText(t, $"{r.name}\n<size={DrinkPaperUi.Small}>{DrinkRules.KindName(r.Kind)} · {DrinkRules.TechniqueName(r.Technique)}</size>", DrinkPaperUi.Body, TextAnchor.UpperLeft, DrinkPaperUi.Ink);
            txt.supportRichText = true; txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap; txt.verticalOverflow = VerticalWrapMode.Truncate;
            t.gameObject.SetActive(false);
            return rt;
        }

        void BuildPager()
        {
            foreach (Transform c in pager) Destroy(c.gameObject);
            int pages = PageCount;
            if (recipes().Count <= PerPage) return;
            float w = 128f, gap = 8f, x0 = -(pages * w + (pages - 1) * gap) * 0.5f;
            for (int i = 0; i < pages; i++)
            {
                int page = i;
                var rt = UiFactory.Place("Divider_" + i, pager, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (w + gap) + w * 0.5f, 0f), new Vector2(w, 64f));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = DrinkPaperUi.Sprite("dl_recipe_divider");
                img.color = i == Page ? new Color(1f, 0.85f, 0.55f) : Color.white;
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => { if (motion != null || State != Phase.Open) return; Page = page; Selected = -1; RebuildPapers(); AudioService.PlaySfx("ui_focus"); });
                int a = i * PerPage + 1, z = Mathf.Min(recipes().Count, (i + 1) * PerPage);
                var label = UiFactory.AddText(UiFactory.Stretch("Label", rt), $"{a}–{z}", DrinkPaperUi.Small, TextAnchor.MiddleCenter, DrinkPaperUi.Ink);
                label.raycastTarget = false;
            }
        }

        void OnPaperClicked(int index)
        {
            if (motion != null) { skip = true; return; }
            if (State != Phase.Open) return;
            if (Selected == index) { Pull(index); return; }
            Selected = index;
            UpdateSelectionPose();
            AudioService.PlaySfx("ui_focus");
            RefreshCaption();
        }

        void Step(int d)
        {
            if (motion != null || State != Phase.Open) return;
            int n = recipes().Count;
            if (n == 0) return;
            int s = Selected < 0 ? Page * PerPage : Mathf.Clamp(Selected + d, 0, n - 1);
            int page = s / PerPage;
            if (page != Page) { Page = page; Selected = s; RebuildPapers(); }
            else { Selected = s; UpdateSelectionPose(); }
            AudioService.PlaySfx("ui_focus");
            RefreshCaption();
        }

        void UpdateSelectionPose()
        {
            var sel = PathAt("selected");
            for (int s = 0; s < papers.Count; s++)
            {
                int index = Page * PerPage + s;
                var slot = layout.paperSlots[Mathf.Min(s, layout.paperSlots.Length - 1)];
                bool on = index == Selected;
                // 고른 종이: 같은 칸에서 8px 올라오고(selected 자세) 맨 앞 + 제목이 보인다
                var pos = on ? new[] { slot.position[0], slot.position[1] - (PathAt("stored").position[1] - sel.position[1]) } : slot.position;
                Pose(papers[s], pos, slot.size);
                papers[s].Find("Title").gameObject.SetActive(on);
                if (on) papers[s].SetAsLastSibling();
                else papers[s].SetSiblingIndex(Mathf.Min(s, papers[s].parent.childCount - 1));
            }
        }

        public void ToggleDrawer()
        {
            if (motion != null) { skip = true; return; }
            if (State == Phase.Closed) motion = StartCoroutine(OpenRoutine(true));
            else if (State == Phase.Open) motion = StartCoroutine(OpenRoutine(false));
            else if (State == Phase.Sheet) motion = StartCoroutine(ReturnRoutine(true));
        }

        public void SkipMotion() { if (motion != null) skip = true; }

        IEnumerator Animate(float seconds, Action<float> apply)
        {
            float t = 0f;
            while (t < seconds && !skip) { t += Time.unscaledDeltaTime; apply(Mathf.Clamp01(t / seconds)); yield return null; }
            apply(1f);
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        IEnumerator OpenRoutine(bool open)
        {
            State = open ? Phase.Opening : Phase.Closing; skip = false;
            AudioService.PlaySfx(open ? "ui_confirm" : "ui_cancel");
            float from = open01, to = open ? 1f : 0f;
            yield return Animate(OpenSeconds, t => { open01 = Mathf.Lerp(from, to, Ease(t)); ApplyOpen(); });
            State = open ? Phase.Open : Phase.Closed;
            if (!open) Selected = -1;
            UpdateSelectionPose();
            ApplyOpen();
            motion = null; skip = false;
        }

        public void Pull(int index)
        {
            if (motion != null || State != Phase.Open) return;
            int s = index - Page * PerPage;
            if (s < 0 || s >= papers.Count) return;
            Selected = index;
            motion = StartCoroutine(PullRoutine(s));
        }

        IEnumerator PullRoutine(int s)
        {
            State = Phase.Pulling; skip = false;
            AudioService.PlaySfx("ui_focus");
            var p = papers[s];
            p.SetAsLastSibling();
            p.Find("Title").gameObject.SetActive(true);
            var a = PathAt("selected"); var b = PathAt("pull_mid"); var c = PathAt("clear_rim"); var d = PathAt("unfolded");
            var start = new[] { p.anchoredPosition.x / k, -p.anchoredPosition.y / k };
            var startSize = new[] { p.sizeDelta.x / k, p.sizeDelta.y / k };
            yield return Animate(PullSeconds * 0.25f, t => Pose(p, Lerp(start, b.position, Ease(t)), Lerp(startSize, b.size, Ease(t))));
            yield return Animate(PullSeconds * 0.25f, t => Pose(p, Lerp(b.position, c.position, Ease(t)), Lerp(b.size, c.size, Ease(t))));
            // 앞판 위로 완전히 나온 뒤에만 overlay(가림 없음)로 옮긴다
            p.SetParent(overlay, false);
            Pose(p, c.position, c.size);
            yield return Animate(PullSeconds * 0.5f, t => Pose(p, Lerp(c.position, d.position, Ease(t)), Lerp(c.size, d.size, Ease(t))));
            // 펼친 종이 안쪽 안전 영역(unfolded textSafeRect)에 실제 UI
            p.Find("Title").gameObject.SetActive(false);
            var content = UiFactory.Stretch("SheetContent", p);
            float px = d.position[0], py = d.position[1];
            var safe = d.textSafeRect; // 서랍 좌표
            content.anchorMin = new Vector2((safe[0] - px) / d.size[0], 1f - (safe[1] - py + safe[3]) / d.size[1]);
            content.anchorMax = new Vector2((safe[0] - px + safe[2]) / d.size[0], 1f - (safe[1] - py) / d.size[1]);
            content.offsetMin = content.offsetMax = Vector2.zero;
            buildSheet?.Invoke(Selected, content);
            pulled = p;
            State = Phase.Sheet; motion = null; skip = false;
            RefreshCaption();
        }

        /// <summary>펼친 종이를 원래 칸으로 되돌려 꽂기(다시 넣기 버튼). closeAfter면 이어서 서랍을 닫는다.</summary>
        public void ReturnSheet() { if (motion == null && State == Phase.Sheet) motion = StartCoroutine(ReturnRoutine(false)); }

        IEnumerator ReturnRoutine(bool closeAfter)
        {
            State = Phase.Returning; skip = false;
            AudioService.PlaySfx("ui_cancel");
            var p = pulled;
            int s = Selected - Page * PerPage;
            if (p != null)
            {
                var content = p.Find("SheetContent");
                if (content != null) Destroy(content.gameObject);
                p.Find("Title").gameObject.SetActive(true);
                var c = PathAt("clear_rim"); var b = PathAt("pull_mid"); var d = PathAt("unfolded");
                var slot = layout.paperSlots[Mathf.Clamp(s, 0, layout.paperSlots.Length - 1)];
                var sel = new[] { slot.position[0], slot.position[1] - (PathAt("stored").position[1] - PathAt("selected").position[1]) };
                yield return Animate(ReturnSeconds * 0.5f, t => Pose(p, Lerp(d.position, c.position, Ease(t)), Lerp(d.size, c.size, Ease(t))));
                // 종이 아래끝이 앞판 윗선(176) 위 → 다시 앞판 뒤(꽂힌 층)로
                p.SetParent(stackClip, false);
                Pose(p, c.position, c.size);
                yield return Animate(ReturnSeconds * 0.25f, t => Pose(p, Lerp(c.position, b.position, Ease(t)), Lerp(c.size, b.size, Ease(t))));
                yield return Animate(ReturnSeconds * 0.25f, t => Pose(p, Lerp(b.position, sel, Ease(t)), Lerp(b.size, slot.size, Ease(t))));
            }
            pulled = null;
            State = Phase.Open;
            UpdateSelectionPose();
            if (closeAfter) yield return OpenRoutine(false);
            motion = null; skip = false;
            RefreshCaption();
        }

        /// <summary>목록이 바뀐 뒤(삭제·복제): 펼친 종이를 거두고 다시 꽂는다(서랍은 연 채로).</summary>
        public void ResetAfterChange()
        {
            if (motion != null) { StopCoroutine(motion); motion = null; }
            if (pulled != null) Destroy(pulled.gameObject);
            pulled = null; Selected = -1;
            if (State != Phase.Closed) { State = Phase.Open; open01 = 1f; }
            RebuildPapers();
        }
    }
}
