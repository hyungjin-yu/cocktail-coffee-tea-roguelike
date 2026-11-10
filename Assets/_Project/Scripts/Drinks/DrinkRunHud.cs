using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>2026-11-09 음료 효과 알림 종류 — 이번 판 내내(상시) / 남은 시간이 있는 상태(지속) / 그 순간 한 번(발동).</summary>
    public enum DrinkNoticeKind { Run, Timed, Instant }

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
        const int BodyFont = 20, ToastFont = 24, ProcFont = 24;

        GameManager game;
        RectTransform chipRt, titleRt, buffRt, coffeeRt, stateRt;
        GameObject chip;
        Text chipTitle, chipBuff, chipCoffee, chipState;
        RectTransform chipVesselSlot;
        DrinkVesselView chipVessel;
        GameObject toast;
        Text toastText;
        float toastRemaining;
        GameObject procToast;
        Text procText;
        Image procBg;
        float procRemaining;
        // 2026-11-09 음료 알림 큐: 같은 출처·효과(key)가 반복되면 한 알림의 ×N으로 합치고, 다른 알림은 최소 표시 시간 뒤 차례로.
        public const float NoticeMinShow = 0.6f;
        public const int NoticeQueueMax = 3;
        sealed class Notice
        {
            public string key, source, effect;
            public DrinkNoticeKind kind;
            public float stateRemaining, seconds;
            public int count = 1;
            public Color accent;
        }
        Notice current;
        float currentShown;
        readonly List<Notice> queue = new List<Notice>();
        readonly Dictionary<string, float> keyCooldownUntil = new Dictionary<string, float>();
        bool appliedNoticeShown;
        int shownKills = -1;
        bool shownLocked;
        string shownTitle, shownBuff, shownCoffee = "";
        float laidOutScale = -1f;

        public string ChipStateText => chipState != null ? chipState.text : "";
        public bool ChipVisible => chip != null && chip.activeInHierarchy;
        public string ToastText => toast != null && (toast.activeSelf || toastRemaining > 0f) ? toastText.text : "";
        public bool ToastVisible => toast != null && toast.activeInHierarchy;
        public string ProcText => procText != null && (procToast.activeSelf || procRemaining > 0f) ? procText.text : "";
        public bool ProcVisible => procToast != null && procToast.activeInHierarchy;
        /// <summary>현재 알림의 합산 횟수(없으면 0)·대기 중 알림 수·종류(테스트/검수용).</summary>
        public int ProcCount => current != null && procRemaining > 0f ? current.count : 0;
        public int ProcQueued => queue.Count;
        public DrinkNoticeKind? ProcKind => current != null && procRemaining > 0f ? current.kind : (DrinkNoticeKind?)null;
        /// <summary>알림 출처로 쓰는 준비 음료 이름(없으면 "준비 음료").</summary>
        public string PreparedDrinkName => game != null ? DrinkLab.DrinkInRun(game.RunId)?.recipe?.name ?? "준비 음료" : "준비 음료";
        public RectTransform ChipRect => chipRt;
        public RectTransform ToastRect => toast != null ? (RectTransform)toast.transform : null;
        public Text ChipTitle => chipTitle;
        public Text ChipBuff => chipBuff;
        public Text ChipState => chipState;
        /// <summary>81 마신 커피의 추출법 상태 줄(커피가 아니면 빈 문자열·숨김).</summary>
        public Text ChipCoffee => chipCoffee;

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
            view.chipVesselSlot = UiFactory.Place("PreparedDrinkVessel", chipRt, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 72f));
            float w = ChipWidth - TextLeft - Pad * 2f;
            view.titleRt = UiFactory.Place("Title", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -Pad), new Vector2(w, 30f));
            view.chipTitle = UiFactory.AddText(view.titleRt, "", 22, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.55f));
            view.buffRt = UiFactory.Place("Buff", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -38f), new Vector2(w, 28f));
            view.chipBuff = UiFactory.AddText(view.buffRt, "", BodyFont, TextAnchor.UpperLeft, Color.white);
            view.coffeeRt = UiFactory.Place("Coffee", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -66f), new Vector2(w, 26f));
            view.chipCoffee = UiFactory.AddText(view.coffeeRt, "", BodyFont, TextAnchor.UpperLeft, new Color(1f, 0.78f, 0.5f));
            view.coffeeRt.gameObject.SetActive(false);
            view.stateRt = UiFactory.Place("State", chipRt, new Vector2(0f, 1f), new Vector2(TextLeft, -66f), new Vector2(w, 26f));
            view.chipState = UiFactory.AddText(view.stateRt, "", BodyFont, TextAnchor.UpperLeft, new Color(0.75f, 0.9f, 1f));
            view.chipRt = chipRt;
            view.chip = chipRt.gameObject;

            var toastRt = UiFactory.Place("DrinkAwardToast", root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 70f));
            UiFactory.AddImage(toastRt, new Color(0.14f, 0.09f, 0.04f, 0.92f)).raycastTarget = false;
            view.toastText = UiFactory.AddText(UiFactory.Stretch("Text", toastRt), "", ToastFont, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            view.toast = toastRt.gameObject;
            view.toast.SetActive(false);

            // 전투 중 발동한 음료 효과는 재료 보상 알림과 분리해 표시한다.
            var procRt = UiFactory.Place("DrinkEffectToast", root, new Vector2(0.5f, 0f), new Vector2(0f, 242f), new Vector2(900f, 58f));
            view.procBg = UiFactory.AddImage(procRt, new Color(0.12f, 0.08f, 0.04f, 0.94f));
            view.procBg.raycastTarget = false;
            view.procText = UiFactory.AddText(UiFactory.Stretch("Text", procRt), "", ProcFont, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.56f));
            view.procText.raycastTarget = false;
            view.procToast = procRt.gameObject;
            view.procToast.SetActive(false);
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
            if (game != null && game.State == GameState.GameOver)
            {
                toastRemaining = 0f;
                procRemaining = 0f;
                queue.Clear();
            }
            bool combat = CombatVisible(game);
            // 알림 시간은 전투 화면에서만 흐른다 — 카드 선택 등으로 가려졌다가 전투로 돌아오면 남은 시간만큼 다시 보인다.
            if (combat && toastRemaining > 0f) toastRemaining -= Time.unscaledDeltaTime;
            bool showToast = combat && toastRemaining > 0f;
            if (showToast) toast.transform.localScale = Vector3.one * ReadableScale(ToastFont, CanvasScale());
            if (toast.activeSelf != showToast) toast.SetActive(showToast);
            if (combat) TickNotice(Time.unscaledDeltaTime, Time.deltaTime);
            if (combat && !appliedNoticeShown) ShowAppliedNotice();
            bool showProc = combat && procRemaining > 0f;
            if (showProc) procToast.transform.localScale = Vector3.one * ReadableScale(ProcFont, CanvasScale());
            if (procToast.activeSelf != showProc) procToast.SetActive(showProc);
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
            // 81 커피 추출법 상태는 매 프레임 바뀔 수 있다(0.1초·10% 단위 문자열) — 바뀐 때만 다시 배치한다.
            var coffee = game.Player != null ? game.Player.Coffee : null;
            string coffeeLine = coffee != null ? coffee.StatusLine() : "";
            if (coffeeLine != shownCoffee)
            {
                shownCoffee = coffeeLine;
                chipCoffee.text = coffeeLine;
                if (!force && shownBuff != null) Layout();
            }
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
            if (chipVessel == null)
            {
                string vesselId = DrinkStageView.ServeFor(drink.recipe);
                var sprite = DrinkArt.SpriteOrPlaceholder(vesselId, out _);
                float width = sprite != null ? sprite.rect.width : 44f;
                float height = sprite != null ? sprite.rect.height : 72f;
                float fit = Mathf.Min(44f / (width * DrinkVesselView.PixelScale), 68f / (height * DrinkVesselView.PixelScale));
                chipVessel = DrinkVesselView.Create(chipVesselSlot, vesselId, Vector2.zero, DrinkStageView.CapacityFor(vesselId));
                chipVessel.Rect.localScale = Vector3.one * fit;
                chipVessel.Rect.anchoredPosition = new Vector2(0f, -height * DrinkVesselView.PixelScale * fit * 0.5f);
            }
            chipVessel.SetLiquid(look.color, look.totalVolume, look.turbidity);
            chipState.text = locked ? "이번 판에 사용됨 — 판이 끝나면 사라집니다"
                : $"지금 포기하면 음료 보존 (처치 {Mathf.Min(game.Kills, DrinkLab.PreserveKillLimit)}/{DrinkLab.PreserveKillLimit})";
            shownBuff = game.DrinkBuff.Describe("\u0001");
            Layout();
        }

        /// <summary>폭은 고정, 줄 수만큼 높이를 늘린다. 효과는 한 효과("공격 속도 +4%")가 줄 중간에서 끊기지 않게 줄 단위로 묶는다.</summary>
        void Layout()
        {
            laidOutScale = CanvasScale();
            float k = ReadableScale(BodyFont, laidOutScale);
            chipRt.localScale = new Vector3(k, k, 1f);
            float w = titleRt.sizeDelta.x;
            chipBuff.text = PackLines(chipBuff, shownBuff.Split('\u0001'), " · ", w);
            float y = Pad;
            coffeeRt.gameObject.SetActive(chipCoffee.text.Length > 0);
            foreach (var (rt, text) in new[] { (titleRt, chipTitle), (buffRt, chipBuff), (coffeeRt, chipCoffee), (stateRt, chipState) })
            {
                if (!rt.gameObject.activeSelf) continue;
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

        /// <summary>이전 API 호환: 한 줄 텍스트를 순간 발동 알림으로 보낸다(같은 텍스트끼리 합산).</summary>
        public void ShowDrinkEffect(string text, Color accent, float seconds = 1.5f)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            ShowDrinkNotice("text:" + text, null, text, DrinkNoticeKind.Instant, 0f, accent, seconds);
        }

        /// <summary>
        /// 2026-11-09 음료 효과 알림(출처 · 효과 · 정수 수치 · 남은 시간). 실제로 적용/발동한 순간에만 호출한다.
        /// - kind: Run "[상시]"(이번 판 내내) / Timed "[지속 n초]"(남은 시간이 게임 시간으로 줄어듦) / Instant "[발동]".
        /// - 같은 key가 보이는 중이면 새로 띄우지 않고 ×N으로 합산·시간만 연장(깜빡임 없음).
        /// - 다른 key는 지금 알림이 NoticeMinShow 이상 보인 뒤 차례로(최대 NoticeQueueMax, 넘치면 가장 오래된 대기 알림 버림).
        /// - cooldown초 안에 같은 key가 다시 오면 생략 — 주기적으로 바뀌는 상태(핸드 드립 뜸/붓기)의 도배 방지.
        /// </summary>
        public void ShowDrinkNotice(string key, string source, string effect, DrinkNoticeKind kind, float stateSeconds, Color accent,
            float seconds = 1.6f, float cooldown = 0f)
        {
            if (game == null || game.DrinkBuff == null || !CombatVisible(game) || string.IsNullOrWhiteSpace(effect)) return;
            key = string.IsNullOrEmpty(key) ? effect : key;
            if (current != null && procRemaining > 0f && current.key == key)
            {
                current.count++;
                current.effect = effect;
                current.stateRemaining = Mathf.Max(current.stateRemaining, stateSeconds);
                procRemaining = Mathf.Max(procRemaining, Mathf.Max(0.5f, seconds));
                Render();
                return;
            }
            var queued = queue.Find(q => q.key == key);
            if (queued != null)
            {
                queued.count++;
                queued.effect = effect;
                queued.stateRemaining = Mathf.Max(queued.stateRemaining, stateSeconds);
                return;
            }
            if (cooldown > 0f && keyCooldownUntil.TryGetValue(key, out var until) && Time.time < until) return;
            if (cooldown > 0f) keyCooldownUntil[key] = Time.time + cooldown;
            var n = new Notice { key = key, source = source, effect = effect, kind = kind, stateRemaining = stateSeconds,
                seconds = Mathf.Max(0.5f, seconds), accent = accent };
            if (current != null && procRemaining > 0f)
            {
                if (queue.Count >= NoticeQueueMax) queue.RemoveAt(0);
                queue.Add(n);
                return;
            }
            Begin(n);
        }

        void Begin(Notice n)
        {
            current = n;
            currentShown = 0f;
            procRemaining = n.seconds;
            Render();
            procToast.transform.localScale = Vector3.one * ReadableScale(ProcFont, CanvasScale());
            procToast.SetActive(CombatVisible(game));
        }

        void Advance()
        {
            if (queue.Count == 0) { procRemaining = 0f; return; }
            var next = queue[0];
            queue.RemoveAt(0);
            Begin(next);
        }

        /// <summary>표시 시간은 실시간(전투 화면에서만 흐름), 지속 상태의 남은 시간은 게임 시간(일시정지 시 정지).
        /// 대기 알림이 있으면 지금 알림은 최소 표시 시간만 채우고 넘긴다.</summary>
        void TickNotice(float unscaledDt, float gameDt)
        {
            if (current == null || procRemaining <= 0f) { if (queue.Count > 0) Advance(); return; }
            currentShown += unscaledDt;
            procRemaining -= unscaledDt;
            if (current.kind == DrinkNoticeKind.Timed && current.stateRemaining > 0f)
            {
                int before = IntNum.RemainingSeconds(current.stateRemaining);
                current.stateRemaining = Mathf.Max(0f, current.stateRemaining - gameDt);
                if (IntNum.RemainingSeconds(current.stateRemaining) != before) Render();
            }
            if (procRemaining <= 0f || (queue.Count > 0 && currentShown >= NoticeMinShow)) Advance();
        }

        /// <summary>테스트 전용: 전투 화면 한 프레임만큼 알림 시간을 흘린다.</summary>
        public void TickNoticeForTests(float unscaledDt, float gameDt) => TickNotice(unscaledDt, gameDt);

        void Render()
        {
            if (current == null) return;
            procText.text = Format(current.kind, current.source, current.effect, current.stateRemaining, current.count);
            procText.color = current.accent;
            procBg.color = current.kind == DrinkNoticeKind.Instant ? new Color(0.16f, 0.08f, 0.03f, 0.94f)
                : current.kind == DrinkNoticeKind.Timed ? new Color(0.05f, 0.09f, 0.14f, 0.94f) : new Color(0.07f, 0.1f, 0.05f, 0.94f);
        }

        /// <summary>"[발동] 출처 · 효과 ×N" / "[지속 3초] 출처 · 효과" / "[상시] 출처 · 효과". 숫자는 모두 정수.</summary>
        public static string Format(DrinkNoticeKind kind, string source, string effect, float stateRemaining, int count)
        {
            string tag = kind == DrinkNoticeKind.Run ? "[상시]"
                : kind == DrinkNoticeKind.Timed ? $"[지속 {IntNum.RemainingSeconds(stateRemaining)}초]" : "[발동]";
            string body = string.IsNullOrEmpty(source) ? effect : source + " · " + effect;
            return tag + " " + body + (count > 1 ? $" ×{count}" : "");
        }

        /// <summary>첫 전투 화면에서 한 번: 준비 음료가 실제로 적용됐음을 알리고 플레이어 머리 위로 잔이 짧게 떠오른다(입력 차단 없음).</summary>
        void ShowAppliedNotice()
        {
            appliedNoticeShown = true;
            if (game == null || game.DrinkBuff == null) return;
            var drink = DrinkLab.DrinkInRun(game.RunId);
            if (drink == null) return;
            var parts = game.DrinkBuff.Describe("\u0001").Split('\u0001');
            string effect = parts.Length <= 2 ? string.Join(" · ", parts) : $"{parts[0]} · {parts[1]} 외 {parts.Length - 2}개";
            ShowDrinkNotice("drink_applied", $"{drink.recipe.name} 적용", effect, DrinkNoticeKind.Run, 0f,
                new Color(0.82f, 1f, 0.7f), 2.4f);
            DrinkUseFx.Play(game.Player, drink.recipe);
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
