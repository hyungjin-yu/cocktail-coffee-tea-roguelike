using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 24(+14 획득 그림): 목표 음료 재료 칸(런 HUD 왼쪽 열, 목표 조합 칸 아래).
    /// 목표 = drinks.json 사용자 저장의 goalRecipe 사본(레시피 서랍·예시의 "재료 모으기"로 지정 — 전역 예시 정의는 바꾸지 않음, 활성 1개).
    /// 칸마다 재료 그림 + "창고 + 이번 판 / 필요": 창고 = 지금 제조에 쓸 수 있는 실제 재고, 이번 판 = 아직 정산 전(판이 끝나면 창고로)인 획득량.
    /// 보스 재료 보상이 기록되면(DrinkLab.AwardRecorded — 실제 지급과 같은 사건, 1회) 재료 그림이 화면 가운데에서 해당 칸으로 날아가고
    /// 수치가 갱신된다. 모두 채워지는 순간 한 번 강조. 표시 전용 — 드롭 확률·지급량·저장은 바꾸지 않는다.
    /// </summary>
    public class DrinkGoalHud : MonoBehaviour
    {
        public const int MaxChips = 4;
        // 2026-11-16: 720p 실화면(feedback2 22_hud_goal_after)에서 값 "30/60mL"이 칸 폭 38에 줄바꿈되어 9px대 글자 → 칸 폭을 실제 칸 수로 나누고
        // 값(가진 양/필요)과 단위·이번 판 몫을 두 줄로 분리, 그림 44.
        public const float Width = 580f, Height = 72f, ChipIcon = 44f, TitleWidth = 150f, ChipMaxWidth = 150f;
        GameManager game;
        string runIdWithoutGame;
        string RunId => game != null ? game.RunId : runIdWithoutGame;
        RectTransform root;
        Text title;
        readonly List<(string id, Image icon, Text value, Text sub, RectTransform rt)> chips = new List<(string, Image, Text, Text, RectTransform)>();
        string shownSignature;
        bool wasReady;
        float flash;
        Image back;
        readonly List<Flight> flights = new List<Flight>();

        class Flight { public RectTransform rt; public Vector2 from, to; public float t; public string id; }

        public bool Visible => root != null && root.gameObject.activeSelf;
        public string TitleText => title != null ? title.text : "";
        public string ChipText(int i) => i >= 0 && i < chips.Count ? chips[i].value.text + " " + chips[i].sub.text : "";
        public int ChipCount => chips.Count;
        public int FlightsInAir => flights.Count;
        public bool ReadyFlashing => flash > 0f;

        public static DrinkGoalHud Create(RectTransform parent, GameManager game, Vector2 topLeft) => Create(parent, game, topLeft, null);

        /// <summary>runIdWithoutGame = GameManager 없이(편집 모드 시험) 이번 판 id로 쓸 값. 실제 런은 game.RunId.</summary>
        public static DrinkGoalHud Create(RectTransform parent, GameManager game, Vector2 topLeft, string runIdWithoutGame)
        {
            var rt = UiFactory.Place("DrinkGoal", parent, new Vector2(0f, 1f), topLeft, new Vector2(Width, Height));
            var v = rt.gameObject.AddComponent<DrinkGoalHud>();
            v.game = game;
            v.runIdWithoutGame = runIdWithoutGame;
            v.root = rt;
            v.back = UiFactory.AddImage(rt, new Color(0.06f, 0.05f, 0.07f, 0.82f));
            v.back.raycastTarget = false;
            v.title = UiFactory.AddText(UiFactory.Place("Title", rt, new Vector2(0f, 1f), new Vector2(8f, -2f), new Vector2(TitleWidth, Height - 4f)), "", 20, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.6f));
            v.title.resizeTextForBestFit = true; v.title.resizeTextMinSize = 16; v.title.resizeTextMaxSize = 20;
            v.title.raycastTarget = false;
            DrinkLab.AwardRecorded += v.OnAward;
            v.Rebuild();
            return v;
        }

        void OnDestroy() => DrinkLab.AwardRecorded -= OnAward;

        static DrinkRecipe Goal => DrinkLab.GoalRecipe;

        /// <summary>재료별 (창고, 이번 판 미정산, 필요). 같은 재료가 여러 줄이면 합친다.</summary>
        public static List<(string id, int stock, int run, int need)> Progress(DrinkRecipe goal, string runId)
        {
            var list = new List<(string, int, int, int)>();
            if (goal == null || goal.steps == null) return list;
            var need = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var s in goal.steps)
            {
                if (s == null || s.amount <= 0 || DrinkIngredients.Get(s.id) == null) continue;
                if (!need.ContainsKey(s.id)) { need[s.id] = 0; order.Add(s.id); }
                need[s.id] += s.amount;
            }
            foreach (var id in order)
            {
                long run = 0;
                if (!string.IsNullOrEmpty(runId))
                    foreach (var a in DrinkLab.PendingFor(runId)) foreach (var i in a.items) if (i != null && i.id == id) run += i.amount;
                list.Add((id, DrinkLab.Owned(id), (int)Math.Min(int.MaxValue, run), need[id]));
            }
            return list;
        }

        public static bool IsReady(List<(string id, int stock, int run, int need)> p)
        {
            if (p.Count == 0) return false;
            foreach (var x in p) if ((long)x.stock + x.run < x.need) return false;
            return true;
        }

        string Signature()
        {
            var g = Goal;
            if (g == null) return "";
            var sb = new System.Text.StringBuilder(g.name);
            foreach (var s in g.steps) if (s != null) sb.Append('|').Append(s.id).Append(':').Append(s.amount);
            return sb.ToString();
        }

        void Rebuild()
        {
            foreach (var c in chips) if (c.rt != null) UiFactory.SafeDestroy(c.rt.gameObject);
            chips.Clear();
            shownSignature = Signature();
            var g = Goal;
            root.gameObject.SetActive(g != null);
            if (g == null) return;
            var p = Progress(g, RunId);
            int n = Mathf.Clamp(p.Count, 1, MaxChips);
            float x = TitleWidth + 16f, w = Mathf.Min(ChipMaxWidth, (Width - x - 8f) / n);
            for (int i = 0; i < p.Count && i < MaxChips; i++)
            {
                var def = DrinkIngredients.Get(p[i].id);
                var c = UiFactory.Place("Chip_" + p[i].id, root, new Vector2(0f, 0.5f), new Vector2(x + i * w, 0f), new Vector2(w - 6f, Height - 8f));
                var art = DrinkArt.SpriteOrPlaceholder(def.iconId, out bool placeholder);
                var icon = UiFactory.AddImage(UiFactory.Place("Icon", c, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(ChipIcon, ChipIcon)), Color.white);
                icon.sprite = art; icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = art != null;
                float tw = w - ChipIcon - 10f, rowH = (Height - 8f) * 0.5f;
                var val = UiFactory.AddText(UiFactory.Place("Value", c, new Vector2(0f, 1f), new Vector2(ChipIcon + 4f, 0f), new Vector2(tw, rowH + 4f)), "", 22, TextAnchor.LowerLeft, Color.white);
                val.horizontalOverflow = HorizontalWrapMode.Wrap; val.verticalOverflow = VerticalWrapMode.Truncate;
                val.resizeTextForBestFit = true; val.resizeTextMinSize = 16; val.resizeTextMaxSize = 22; val.raycastTarget = false;
                var sub = UiFactory.AddText(UiFactory.Place("Sub", c, new Vector2(0f, 1f), new Vector2(ChipIcon + 4f, -rowH - 4f), new Vector2(tw, rowH - 4f)), "", 16, TextAnchor.UpperLeft, new Color(0.82f, 0.78f, 0.7f));
                sub.horizontalOverflow = HorizontalWrapMode.Wrap; sub.verticalOverflow = VerticalWrapMode.Truncate;
                sub.resizeTextForBestFit = true; sub.resizeTextMinSize = 14; sub.resizeTextMaxSize = 16; sub.raycastTarget = false;
                chips.Add((p[i].id, icon, val, sub, c));
            }
            Refresh(false);
        }

        void Refresh(bool allowFlash)
        {
            var g = Goal;
            if (g == null) return;
            var p = Progress(g, RunId);
            bool ready = IsReady(p);
            int extra = Mathf.Max(0, p.Count - MaxChips);
            title.text = (ready ? "목표 음료 ✓ 재료 충분\n" : "목표 음료\n") + g.name + (extra > 0 ? $" (+{extra}종)" : "");
            for (int i = 0; i < chips.Count && i < p.Count; i++)
            {
                var x = p[i];
                var def = DrinkIngredients.Get(x.id);
                bool ok = (long)x.stock + x.run >= x.need;
                // 첫 줄 "가진 양(창고+이번 판)/필요", 둘째 줄 "단위 · 이번 판 +n" — 이번 판 몫은 판이 끝나 정산되면 창고로 들어간다.
                long have = (long)x.stock + x.run;
                chips[i].value.text = $"{DrinkRules.Format((int)Math.Min(int.MaxValue, have))}/{DrinkRules.Format(x.need)}";
                string unit = def.Unit;
                chips[i].sub.text = x.run > 0 ? $"{unit} · 판+{DrinkRules.Format(x.run)}" : unit;
                chips[i].value.color = ok ? new Color(0.65f, 1f, 0.7f) : Color.white;
                chips[i].sub.color = x.run > 0 ? new Color(0.65f, 1f, 0.7f) : new Color(0.82f, 0.78f, 0.7f);
            }
            if (allowFlash && ready && !wasReady) flash = 1.2f; // 제작 가능해지는 순간 한 번
            wasReady = ready;
        }

        void OnAward(string runId, IList<DrinkAmount> items)
        {
            if (this == null) { DrinkLab.AwardRecorded -= OnAward; return; } // 파괴된 HUD(편집 모드 등)는 구독 해제
            if (string.IsNullOrEmpty(RunId) || runId != RunId || Goal == null || root == null || !root.gameObject.activeInHierarchy) { Refresh(true); return; }
            bool calm = SettingsService.Current.effectsLevel == SettingsData.EffectsLow;
            foreach (var it in items)
            {
                int idx = chips.FindIndex(c => c.id == it.id);
                if (idx < 0 || calm) continue; // 저이펙트: 비행 생략(수치는 아래에서 바로 갱신 — 실제 지급량과 무관)
                var f = UiFactory.Place("Fly_" + it.id, root, new Vector2(0f, 1f), Vector2.zero, new Vector2(ChipIcon, ChipIcon));
                var img = UiFactory.AddImage(f, Color.white);
                img.sprite = chips[idx].icon.sprite; img.preserveAspect = true; img.raycastTarget = false;
                Vector2 to = LocalOf(chips[idx].icon.rectTransform);
                Vector2 from = to + new Vector2(520f, -260f); // 화면 가운데(플레이어 쪽)에서 출발
                if (flights.Count > 6) { UiFactory.SafeDestroy(f.gameObject); continue; } // 동시 획득 폭주 방지(수치는 그대로 집계)
                flights.Add(new Flight { rt = f, from = from, to = to, t = 0f, id = it.id });
            }
            Refresh(true);
        }

        Vector2 LocalOf(RectTransform target)
        {
            var world = target.TransformPoint(target.rect.center);
            Vector2 local = root.InverseTransformPoint(world);
            // root 피벗(0,1) 기준 → Place의 앵커(0,1) 좌표
            return local + new Vector2(-ChipIcon * 0.5f, ChipIcon * 0.5f);
        }

        void Update()
        {
            if (shownSignature != Signature()) Rebuild();
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                f.t += Time.unscaledDeltaTime / 0.6f;
                float e = 1f - (1f - Mathf.Clamp01(f.t)) * (1f - Mathf.Clamp01(f.t));
                f.rt.anchoredPosition = Vector2.Lerp(f.from, f.to, e) + new Vector2(0f, Mathf.Sin(e * Mathf.PI) * 60f);
                if (f.t >= 1f) { UiFactory.SafeDestroy(f.rt.gameObject); flights.RemoveAt(i); }
            }
            if (flash > 0f)
            {
                flash -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(flash / 1.2f);
                back.color = Color.Lerp(new Color(0.06f, 0.05f, 0.07f, 0.82f), new Color(0.55f, 0.42f, 0.12f, 0.95f), Mathf.Sin(k * Mathf.PI));
            }
        }
    }
}
