using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 69 런 중 표시: 준비 음료 칩(이름·버프·보존 상태 n/5킬)과 보스 음료 재료 확보 알림 + 전용 픽업 연출.
    /// 지급은 보스 사망 순간 DrinkLab에 이미 확정 기록된다 — 픽업 연출은 플레이어에게 날아가는 표시일 뿐 놓칠 수 없다.
    /// 2026-10-19 hudfix: 칩·알림은 전투 화면에서만 보인다(카드 선택·음료 지식 안내·일시정지·조작 안내·트리·혼합 해금·
    /// 목표 조합·결과 화면 위에 올라오지 않음). 칩은 오른쪽 위 기준 고정 폭, 이름·효과·상태 줄 수에 맞춰 높이가 늘어난다.
    /// </summary>
    public class DrinkRunHud : MonoBehaviour
    {
        public const float ChipWidth = 500f;
        public const float ChipRight = 30f;
        public const float ChipTop = 196f;
        public const float ChipMinHeight = 96f;
        const float TextLeft = 68f, Pad = 8f, Gap = 2f;
        /// <summary>폭 맞춤으로 캔버스가 크게 줄어드는 좁은 창(예: 836×705 → 0.435배)에서도 칩·알림 글자가 이 화면 픽셀보다 작아지지 않게 칩만 키운다.</summary>
        public const float MinGlyphPx = 12f;
        const int BodyFont = 20, ToastFont = 24;

        GameManager game;
        RectTransform chipRt, titleRt, buffRt, stateRt;
        GameObject chip;
        Text chipTitle, chipBuff, chipState;
        Image chipSwatch;
        GameObject toast;
        Text toastText;
        float toastRemaining;
        int shownKills = -1;
        bool shownLocked;
        string shownTitle, shownBuff;
        float laidOutScale = -1f;

        public string ChipStateText => chipState != null ? chipState.text : "";
        public bool ChipVisible => chip != null && chip.activeInHierarchy;
        public string ToastText => toast != null && (toast.activeSelf || toastRemaining > 0f) ? toastText.text : "";
        public bool ToastVisible => toast != null && toast.activeInHierarchy;
        public RectTransform ChipRect => chipRt;
        public RectTransform ToastRect => toast != null ? (RectTransform)toast.transform : null;
        public Text ChipTitle => chipTitle;
        public Text ChipBuff => chipBuff;
        public Text ChipState => chipState;

        /// <summary>전투가 실제로 진행 중인 화면인지. State만으로는 부족하다 — 카드 선택 위 음료 지식 안내(CardView.IsLearningOpen/
        /// IsTutorialOpen)·일시정지 조작 안내도 실제 열린 창으로 확인한다.</summary>
        public static bool CombatVisible(GameManager g)
        {
            if (g == null || g.State != GameState.Playing || g.IsPaused) return false;
            if (g.CardView != null && (g.CardView.IsOpen || g.CardView.IsLearningOpen)) return false;
            if (g.PauseView != null && (g.PauseView.IsOpen || g.PauseView.IsTutorialOpen)) return false;
            if (g.SkillView != null && g.SkillView.IsOpen) return false;
            if (g.HybridView != null && g.HybridView.IsOpen) return false;
            if (g.ComboView != null && g.ComboView.IsOpen) return false;
            return true;
        }

        public static DrinkRunHud Create(Transform canvas, GameManager game)
        {
            var root = UiFactory.Stretch("DrinkHud", canvas);
            if (MobilePlatform.IsTouchUI) root.gameObject.AddComponent<SafeAreaFitter>(); // HUD와 같은 안전 영역
            var view = root.gameObject.AddComponent<DrinkRunHud>();
            view.game = game;

            // Place는 피벗=앵커(1,1) — 오른쪽 위 모서리에서 왼쪽·아래로 펼쳐진다.
            var chipRt = UiFactory.Place("DrinkChip", root, new Vector2(1f, 1f), new Vector2(-ChipRight, -ChipTop), new Vector2(ChipWidth, ChipMinHeight));
            UiFactory.AddImage(chipRt, new Color(0.08f, 0.06f, 0.1f, 0.86f)).raycastTarget = false;
            view.chipSwatch = UiFactory.AddImage(UiFactory.Place("Swatch", chipRt, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 72f)), Color.clear);
            view.chipSwatch.raycastTarget = false;
            float w = ChipWidth - TextLeft - Pad * 2f;
            view.titleRt = UiFactory.Place("Title", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -Pad), new Vector2(w, 30f));
            view.chipTitle = UiFactory.AddText(view.titleRt, "", 22, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.55f));
            view.buffRt = UiFactory.Place("Buff", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -38f), new Vector2(w, 28f));
            view.chipBuff = UiFactory.AddText(view.buffRt, "", BodyFont, TextAnchor.UpperLeft, Color.white);
            view.stateRt = UiFactory.Place("State", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -66f), new Vector2(w, 26f));
            view.chipState = UiFactory.AddText(view.stateRt, "", BodyFont, TextAnchor.UpperLeft, new Color(0.75f, 0.9f, 1f));
            view.chipRt = chipRt;
            view.chip = chipRt.gameObject;

            var toastRt = UiFactory.Place("DrinkAwardToast", root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 70f));
            UiFactory.AddImage(toastRt, new Color(0.14f, 0.09f, 0.04f, 0.92f)).raycastTarget = false;
            view.toastText = UiFactory.AddText(UiFactory.Stretch("Text", toastRt), "", ToastFont, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            view.toast = toastRt.gameObject;
            view.toast.SetActive(false);
            view.Refresh(true);
            if (!string.IsNullOrEmpty(DrinkLab.LastBeginError))
            {
                // 69 입장 음용 기록 저장 실패: 버프 없이 시작했음을 알린다(음료는 Ready로 남아 있음).
                view.ShowToast(DrinkLab.LastBeginError, 6f);
            }
            return view;
        }

        public void RefreshForTests() => Refresh(false);

        void Update()
        {
            Refresh(false);
            // 결과 화면에서는 재료 알림을 거둔다(획득 내역은 결과 화면 정산 줄이 같은 내용을 보여 준다) — 결과 버튼을 가리지 않게.
            if (game != null && game.State == GameState.GameOver) toastRemaining = 0f;
            bool combat = CombatVisible(game);
            // 알림 시간은 전투 화면에서만 흐른다 — 카드 선택 등으로 가려졌다가 전투로 돌아오면 남은 시간만큼 다시 보인다.
            if (combat && toastRemaining > 0f) toastRemaining -= Time.unscaledDeltaTime;
            bool showToast = combat && toastRemaining > 0f;
            if (showToast) toast.transform.localScale = Vector3.one * ReadableScale(ToastFont, CanvasScale());
            if (toast.activeSelf != showToast) toast.SetActive(showToast);
        }

        void Refresh(bool force)
        {
            if (game == null) return;
            var drink = DrinkLab.DrinkInRun(game.RunId);
            bool has = drink != null && game.DrinkBuff != null;
            bool show = has && CombatVisible(game);
            if (chip.activeSelf != show) chip.SetActive(show);
            if (!has) return;
            bool locked = drink.locked || game.Kills >= DrinkLab.PreserveKillLimit;
            string title = $"준비 음료 · {drink.recipe.name}";
            if (!force && shownKills == game.Kills && shownLocked == locked && shownTitle == title)
            {
                // 창 크기가 바뀌면(캔버스 배율) 글자 줄바꿈이 달라질 수 있어 다시 잰다.
                if (show && !Mathf.Approximately(laidOutScale, CanvasScale())) Layout();
                return;
            }
            shownKills = game.Kills;
            shownLocked = locked;
            shownTitle = title;
            chipTitle.text = title;
            var look = DrinkCalculator.Look(drink.recipe);
            var c = look.color; c.a = Mathf.Max(0.55f, c.a);
            chipSwatch.color = c;
            chipState.text = locked ? "이번 판에 사용됨 — 판이 끝나면 사라집니다"
                : $"지금 포기하면 음료 보존 (처치 {Mathf.Min(game.Kills, DrinkLab.PreserveKillLimit)}/{DrinkLab.PreserveKillLimit})";
            shownBuff = game.DrinkBuff.Describe("\u0001");
            Layout();
        }

        /// <summary>폭은 고정, 줄 수만큼 높이를 늘린다. 효과는 한 효과("공격 속도 +3.6%")가 줄 중간에서 끊기지 않게 줄 단위로 묶는다.</summary>
        void Layout()
        {
            laidOutScale = CanvasScale();
            float k = ReadableScale(BodyFont, laidOutScale);
            chipRt.localScale = new Vector3(k, k, 1f);
            float w = titleRt.sizeDelta.x;
            chipBuff.text = PackLines(chipBuff, shownBuff.Split('\u0001'), " · ", w);
            float y = Pad;
            foreach (var (rt, text) in new[] { (titleRt, chipTitle), (buffRt, chipBuff), (stateRt, chipState) })
            {
                float h = Mathf.Ceil(text.preferredHeight) + 2f;
                rt.anchoredPosition = new Vector2(TextLeft, -y);
                rt.sizeDelta = new Vector2(w, h);
                y += h + Gap;
            }
            chipRt.sizeDelta = new Vector2(ChipWidth, Mathf.Max(ChipMinHeight, y - Gap + Pad));
        }

        /// <summary>글자 크기 × 캔버스 배율이 MinGlyphPx보다 작으면 그만큼 키운다(최대 1.6배 — 836×705에서 칩 폭 ≈ 화면 36%).</summary>
        public static float ReadableScale(int fontSize, float canvasScale) =>
            canvasScale <= 0f ? 1f : Mathf.Clamp(MinGlyphPx / (fontSize * canvasScale), 1f, 1.6f);

        float CanvasScale() => chipTitle.canvas != null ? chipTitle.canvas.scaleFactor : 1f;

        static string PackLines(Text t, string[] items, string sep, float width)
        {
            var settings = t.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float ppu = Mathf.Max(0.0001f, t.pixelsPerUnit);
            var gen = t.cachedTextGeneratorForLayout;
            var sb = new StringBuilder();
            string line = "";
            foreach (var item in items)
            {
                string candidate = line.Length == 0 ? item : line + sep + item;
                if (line.Length > 0 && gen.GetPreferredWidth(candidate, settings) / ppu > width)
                {
                    sb.Append(line).Append('\n');
                    line = item;
                }
                else line = candidate;
            }
            return sb.Append(line).ToString();
        }

        void ShowToast(string text, float seconds)
        {
            toastText.text = text;
            toastRemaining = seconds;
            toast.SetActive(CombatVisible(game));
        }

        /// <summary>보스 처치: 전용 픽업 연출 + 알림. 지급은 이미 확정.</summary>
        public void OnBossAward(Vector2 bossPosition, List<DrinkAmount> items)
        {
            ShowToast($"보스 음료 재료 확보 — {DrinkLab.Summarize(items)} (판이 끝나면 창고에 저장)", 4.5f);
            if (game != null && game.Player != null)
                DrinkMaterialPickup.Spawn(game.transform, bossPosition, game.Player.transform, game.Job);
        }

        /// <summary>72 §2 일반 처치 25회·정예 처치 묶음: 짧은 픽업 연출 + 짧은 알림(찾아다닐 필요 없음, 지급은 이미 확정).</summary>
        public void OnSupplyAward(Vector2 position, List<DrinkAmount> items)
        {
            ShowToast($"음료 재료 — {DrinkLab.Summarize(items)} (판이 끝나면 창고에 저장)", 2.5f);
            if (game != null && game.Player != null)
                DrinkMaterialPickup.Spawn(game.transform, position, game.Player.transform, game.Job);
        }
    }

    /// <summary>보스 음료 재료 전용 픽업 표시(월드). 플레이어에게 날아가 사라진다. 판정·지급 없음(이미 확정).</summary>
    public class DrinkMaterialPickup : MonoBehaviour
    {
        Transform target;
        Vector3 start;
        float t;
        const float Duration = 1.1f;

        public static DrinkMaterialPickup Spawn(Transform parent, Vector2 from, Transform target, Job job)
        {
            var go = new GameObject("DrinkMaterialPickup");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DrinkArt.Sprite("dl_boss_material_drop") ?? IngredientPickup.DropArt(job == Job.TeaSommelier ? IngredientType.Tea : IngredientType.Cocktail);
            sr.sortingOrder = 50;
            go.transform.localScale = Vector3.one * 1.6f;
            var p = go.AddComponent<DrinkMaterialPickup>();
            p.target = target;
            p.start = from;
            return p;
        }

        void Update()
        {
            // 결과 화면(timeScale 0)에서도 끝까지 날아간다.
            t += Time.unscaledDeltaTime / Duration;
            if (target == null || t >= 1f) { Destroy(gameObject); return; }
            float e = t * t * (3f - 2f * t);
            var arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.4f;
            transform.position = Vector3.Lerp(start, target.position, e) + arc;
            transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.6f, e);
        }
    }
}
