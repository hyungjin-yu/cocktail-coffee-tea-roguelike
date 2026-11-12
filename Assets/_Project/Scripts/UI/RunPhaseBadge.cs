using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>런 진행 상태(실제 BossDirector·GameManager 상태에서만 계산).</summary>
    public enum RunPhaseState { Calm, BossIncoming, BossFight, Endless }

    /// <summary>배지 한 장의 내용. 테스트가 같은 계산을 확인할 수 있게 분리한다(정수만).</summary>
    public struct RunPhaseInfo
    {
        public RunPhaseState state;
        public int segment, segments, danger, secondsToBoss;
        public string bossName;
        public string text;
    }

    /// <summary>
    /// 2026-11-09 추가 피드백 범위 3: 런은 "시간 → 중간보스 → … → 최종보스"의 한 판 구조(BossDirector.DefaultSchedule)이고
    /// 몹 웨이브는 14초마다(배너가 이미 알림). 지금까지 구간·위험도·보스 접근은 보스 등장 순간 배너로만 보였다.
    /// 이 배지는 화면 아래 가운데(보스 체력바 바로 위)에 한 줄로 현재 구간 n/전체, 위험 단계(칸 5개 + 숫자), 다음 보스까지 남은 초를 보이고,
    /// 실제 등장 15초 전부터 "보스 접근 n초 · 이름"으로, 보스가 살아 있는 동안 "보스전 · 이름"으로 바뀐다(모양: 느낌표 칸 + 테두리).
    /// 스케줄·스폰·밸런스는 바꾸지 않는다(읽기 전용). 위험 단계는 Balance의 실제 시간 배율(스폰 배율 × 몹 체력 배율)에서 정수 단계로 만든다.
    /// </summary>
    public class RunPhaseBadge : MonoBehaviour
    {
        public const float IncomingWarnSeconds = 15f;
        public const float BadgeW = 560f, BadgeH = 36f, BadgeY = 92f;
        const int Font = 20;

        GameManager game;
        RectTransform rt;
        Image bg, mark;
        Image[] pips = new Image[5];
        Text label, glyph;
        float nextRefresh;
        CanvasGroup group;

        public RunPhaseInfo Last { get; private set; }
        public string Text => label != null ? label.text : "";
        public bool Visible => rt != null && rt.gameObject.activeInHierarchy && group.alpha > 0f;

        public static RunPhaseBadge Create(RectTransform hudRoot, GameManager game)
        {
            var rt = UiFactory.Place("RunPhase", hudRoot, new Vector2(0.5f, 0f), new Vector2(0f, BadgeY), new Vector2(BadgeW, BadgeH));
            var view = rt.gameObject.AddComponent<RunPhaseBadge>();
            view.game = game;
            view.rt = rt;
            view.group = rt.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            view.bg = UiFactory.AddImage(rt, new Color(0.06f, 0.05f, 0.08f, 0.82f));
            view.bg.raycastTarget = false;
            view.mark = UiFactory.AddImage(UiFactory.Place("Mark", rt, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(24f, 24f)), Color.white);
            view.mark.raycastTarget = false;
            view.glyph = UiFactory.AddText(UiFactory.Stretch("Glyph", view.mark.rectTransform), "!", 20, TextAnchor.MiddleCenter, new Color(0.15f, 0.05f, 0.05f));
            for (int i = 0; i < view.pips.Length; i++)
            {
                view.pips[i] = UiFactory.AddImage(UiFactory.Place("Danger" + i, rt, new Vector2(1f, 0.5f), new Vector2(-10f - (4 - i) * 14f, 0f), new Vector2(10f, 14f)), Color.white);
                view.pips[i].raycastTarget = false;
            }
            var labelRt = UiFactory.Place("Label", rt, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(BadgeW - 36f - 84f, BadgeH - 4f));
            view.label = UiFactory.AddText(labelRt, "", Font, TextAnchor.MiddleLeft, Color.white);
            view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.label.verticalOverflow = VerticalWrapMode.Truncate;
            view.label.resizeTextForBestFit = true; // 긴 보스 이름도 한 줄 안에서 글자만 줄인다
            view.label.resizeTextMinSize = 14;
            view.label.resizeTextMaxSize = Font;
            view.Refresh();
            return view;
        }

        // ───── 2026-11-12 마스터 도전 배지 그림판(사용자 HUD 가시성 요청): 주요 = 구간 그림·주기·웨이브·단계·처리 진행 막대 / 보조 = 난이도·약화·제출·칵테일 보너스 ─────
        public const float MasterW = 780f, MasterH = 74f;
        RectTransform masterRoot;
        Image segIcon, cupIcon, glassIcon;
        Text primary, progressText, secondary, submitText, cocktailText;
        HudIcons.Bar progressBar;
        public string MasterPrimaryText => primary != null ? primary.text : "";
        public string MasterProgressText => progressText != null ? progressText.text : "";
        public string MasterSecondaryText => (secondary != null ? secondary.text : "") + " | " + (submitText != null ? submitText.text : "") + " | " + (cocktailText != null ? cocktailText.text : "");
        public float MasterProgress => progressBar != null ? progressBar.Value : -1f;
        public bool SegmentIconShown => segIcon != null && segIcon.enabled;

        void BuildMaster()
        {
            masterRoot = UiFactory.Stretch("MasterLayout", rt);
            var top = new Vector2(0f, 1f);
            segIcon = HudIcons.Icon(masterRoot, "SegIcon", top, new Vector2(8f, -3f), 36f, null);
            primary = HudIcons.Label(masterRoot, "Primary", top, new Vector2(50f, -2f), new Vector2(400f, 38f), "", 24, Color.white, TextAnchor.MiddleLeft, 16);
            progressBar = HudIcons.ProgressBar(masterRoot, "Progress", top, new Vector2(456f, -12f), new Vector2(150f, 18f), new Color(1f, 0.78f, 0.38f, 0.95f));
            progressText = HudIcons.Label(masterRoot, "ProgressText", top, new Vector2(612f, -2f), new Vector2(92f, 38f), "", 22, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleLeft, 14);
            secondary = HudIcons.Label(masterRoot, "Secondary", top, new Vector2(50f, -42f), new Vector2(340f, 28f), "", 18, new Color(0.82f, 0.82f, 0.8f), TextAnchor.MiddleLeft, 13);
            cupIcon = HudIcons.Icon(masterRoot, "CupIcon", top, new Vector2(398f, -42f), 26f, Resources.Load<Sprite>("DrinkLab/dl_coffee_cup"));
            submitText = HudIcons.Label(masterRoot, "Submit", top, new Vector2(428f, -42f), new Vector2(110f, 28f), "", 18, new Color(0.82f, 0.82f, 0.8f), TextAnchor.MiddleLeft, 13);
            glassIcon = HudIcons.Icon(masterRoot, "GlassIcon", top, new Vector2(540f, -42f), 26f, Resources.Load<Sprite>("DrinkLab/dl_glass_cocktail"));
            cocktailText = HudIcons.Label(masterRoot, "Cocktail", top, new Vector2(570f, -42f), new Vector2(130f, 28f), "", 18, new Color(0.82f, 0.82f, 0.8f), TextAnchor.MiddleLeft, 13);
            label.enabled = false; // 원래 두 줄 문구(Text)는 시험·읽기용으로 계속 갱신
        }

        void RefreshMaster()
        {
            if (masterRoot == null || game == null || game.Master == null) return;
            var m = game.Master; var st = m.State; long wave = st.Wave;
            var seg = st.Segment;
            var segDef = seg == MasterSegment.Fair ? MasterDefs.FairCustomer(game.Job) : seg == MasterSegment.Reward ? MasterDefs.RewardMonster(game.Job) : null;
            HudIcons.Set(segIcon, segDef != null ? MasterWavePlan.IconOf(segDef) : HudIcons.Ingredient(game.Job == Job.TeaSommelier ? IngredientType.Tea : IngredientType.Cocktail));
            string segName = seg == MasterSegment.Fair ? $"박람회 {MasterRules.FairIndex(wave)}/{MasterRules.FairWaves}"
                : seg == MasterSegment.Reward ? $"보상 손님 {MasterRules.RewardIndex(wave)}/{MasterRules.RewardWaves}"
                : $"재료 모으기 {MasterRules.InCycle(wave)}/{MasterRules.CollectWaves}";
            primary.text = $"주기 {st.Cycle} · 웨이브 {EndlessRules.Show(wave)} · {segName}";
            switch (st.Phase)
            {
                case MasterPhase.Prep: progressBar.SetValue(0f); progressText.text = "준비 중"; break;
                case MasterPhase.AwaitChoice: progressBar.SetValue(1f); progressText.text = "선택"; break;
                case MasterPhase.ClearingForFair: progressBar.SetValue(1f); progressText.text = $"남은 {Enemy.Active.Count}"; break;
                case MasterPhase.Transition: progressBar.SetValue(1f); progressText.text = "설치 중"; break;
                default:
                    progressBar.SetValue(st.Target > 0 ? (float)((double)st.Cleared / st.Target) : 0f);
                    progressText.text = $"{st.Cleared}/{st.Target}";
                    break;
            }
            secondary.text = seg == MasterSegment.Reward ? "보상 손님: 공격 없음 · 체력 1웨이브"
                : $"적 체력 {EndlessRules.Show(EndlessRules.HpPercentInt(wave))}% · 공격 {EndlessRules.Show(EndlessRules.AttackPercentInt(wave))}% · 약화 {st.ActiveReduction}%";
            submitText.text = $"제출 {st.SubmittedThisCycle}잔";
            cocktailText.text = $"칵테일 {m.CocktailUniqueCount}종 +{m.CocktailAttackPercent}%";
        }

        /// <summary>실제 시간 배율 → 위험 단계 1~5. x = 스폰 배율 × 몹 체력 배율, 단계 = 1 + floor(log2 x).</summary>
        public static int DangerTier(float runTime)
        {
            float x = Balance.TimeSpawnMultiplier(runTime) * (1f + Mathf.Max(0f, runTime) / Balance.EnemyHpGrowthSeconds);
            return Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(1f, x), 2f)), 1, 5);
        }

        /// <summary>2026-11-10 끝없는 모드 배지(유한 구간·보스 예고 없음). 두 줄: 모드·웨이브·다음 웨이브 정수 초 / 집중 계열·체력·공격 배율·생성 대기.</summary>
        public static RunPhaseInfo ComputeEndless(GameManager game)
        {
            if (game.Master != null) return ComputeMaster(game);
            return ComputeLegacyEndless(game);
        }

        /// <summary>이전 30초 시간 웨이브 배지(스포너 수치 시험용 — 실제 런은 마스터 도전 배지).</summary>
        public static RunPhaseInfo ComputeLegacyEndless(GameManager game)
        {
            var info = new RunPhaseInfo { state = RunPhaseState.Endless };
            long wave = System.Math.Max(1L, game.Spawner != null ? game.Spawner.EndlessWave : 1L);
            long pending = game.Spawner != null ? game.Spawner.EndlessPending : 0;
            bool saturated = game.Spawner != null && game.Spawner.EndlessPendingSaturated;
            info.segment = (int)System.Math.Min(int.MaxValue, wave);
            info.secondsToBoss = EndlessRules.SecondsToNextWave(game.RunTimePrecise);
            double grow = EndlessRules.HpPercent(wave) / 100d * EndlessRules.WaveCount(wave) / EndlessRules.FirstWaveCount;
            info.danger = Mathf.Clamp(1 + (int)System.Math.Floor(System.Math.Log(System.Math.Max(1d, grow), 2d)), 1, 5);
            var focus = EndlessRules.FocusFor(wave);
            string next = EndlessRules.IsFocusPreviewWave(wave)
                ? $" · 다음 웨이브 집중 {MonsterFamilies.Mark(EndlessRules.FocusFor(wave + 1))} {MonsterFamilies.Name(EndlessRules.FocusFor(wave + 1))}" : "";
            info.text = $"{RunModeSelection.Name(RunMode.Endless)} · 웨이브 {EndlessRules.Show(wave)} · 다음 웨이브 {info.secondsToBoss}초{next}" + "\n" +
                        $"집중 {MonsterFamilies.Mark(focus)} {MonsterFamilies.Name(focus)} {EndlessRules.FocusWeightPercent}% · 체력 {EndlessRules.Show(EndlessRules.HpPercentInt(wave))}% · 공격 {EndlessRules.Show(EndlessRules.AttackPercentInt(wave))}% · 생성 대기 {(saturated ? "최대(포화)" : pending.ToString())}";
            return info;
        }

        /// <summary>2026-11-11 마스터 도전 배지(두 줄): 주기·구간·웨이브 처리 현황 / 약화율·제출·칵테일 공격 보너스. 시간 웨이브·보스 예고 없음.</summary>
        public static RunPhaseInfo ComputeMaster(GameManager game)
        {
            var info = new RunPhaseInfo { state = RunPhaseState.Endless };
            var m = game.Master; var st = m.State;
            long wave = st.Wave;
            info.segment = (int)System.Math.Min(int.MaxValue, wave);
            double grow = EndlessRules.HpPercent(wave) / 100d;
            info.danger = st.Segment == MasterSegment.Reward ? 1 : Mathf.Clamp(1 + (int)System.Math.Floor(System.Math.Log(System.Math.Max(1d, grow), 2d)), 1, 5);
            string seg = st.Segment == MasterSegment.Fair ? $"박람회 {MasterRules.FairIndex(wave)}/{MasterRules.FairWaves}"
                : st.Segment == MasterSegment.Reward ? $"보상 손님 {MasterRules.RewardIndex(wave)}/{MasterRules.RewardWaves}"
                : $"재료 모으기 {MasterRules.InCycle(wave)}/{MasterRules.CollectWaves}";
            string progress = st.Phase == MasterPhase.AwaitChoice ? "반복 또는 박람회 도전 선택"
                : st.Phase == MasterPhase.ClearingForFair ? $"남은 손님 {Enemy.Active.Count}명 처리 후 박람회"
                : st.Phase == MasterPhase.Transition ? "현수막 설치 중 · 전투 정지"
                : $"처리 {st.Cleared}/{st.Target}";
            info.text = $"{RunModeSelection.Name(RunMode.Endless)} · 주기 {st.Cycle} · 웨이브 {EndlessRules.Show(wave)} · {seg} · {progress}" + "\n" +
                        (st.Segment == MasterSegment.Reward ? "보상 손님은 공격하지 않음 · 체력 1웨이브 기준"
                            : $"체력 {EndlessRules.Show(EndlessRules.HpPercentInt(wave))}% · 공격 {EndlessRules.Show(EndlessRules.AttackPercentInt(wave))}% · 약화 {st.ActiveReduction}%") +
                        $" · 제출 {st.SubmittedThisCycle}잔 · 칵테일 {m.CocktailUniqueCount}종 공격 +{m.CocktailAttackPercent}%";
            return info;
        }

        public static RunPhaseInfo Compute(GameManager game)
        {
            var info = new RunPhaseInfo();
            if (game == null) return info;
            if (game.IsEndless) return ComputeEndless(game);
            float t = game.RunTime;
            var d = game.Director;
            info.segments = d != null ? Mathf.Max(1, d.ScheduleCount) : 1;
            info.segment = Mathf.Clamp((d != null ? Mathf.Max(d.SpawnedCount, d.BossesSpawned) : 0) + (game.ActiveBoss != null ? 0 : 1), 1, info.segments);
            info.danger = DangerTier(t);
            var boss = game.ActiveBoss;
            if (boss != null && !boss.IsDead)
            {
                info.state = RunPhaseState.BossFight;
                info.bossName = boss.Def.name;
                info.text = $"보스전 · {info.bossName} · 위험 {info.danger}";
                return info;
            }
            var next = d != null && d.enabled ? d.NextSpec : null;
            if (next != null)
            {
                info.secondsToBoss = IntNum.RemainingSeconds(next.time - t);
                info.bossName = next.def.name;
                if (next.time - t <= IncomingWarnSeconds)
                {
                    info.state = RunPhaseState.BossIncoming;
                    info.text = $"보스 접근 {info.secondsToBoss}초 · {info.bossName}";
                    return info;
                }
                info.text = $"구간 {info.segment}/{info.segments} · 다음 보스 {info.secondsToBoss / 60}:{info.secondsToBoss % 60:00} · 위험 {info.danger}";
                return info;
            }
            info.text = $"구간 {info.segment}/{info.segments} · 위험 {info.danger}";
            return info;
        }

        void Update()
        {
            bool combat = DrinkRunHud.CombatVisible(game);
            group.alpha = combat ? 1f : 0f; // 자기 오브젝트를 끄면 Update가 멈춘다 — 투명도로만 숨김
            if (!combat) return;
            if (Time.unscaledTime >= nextRefresh) Refresh();
            if (Last.state == RunPhaseState.BossIncoming && !HitFeedback.LowEffects)
            {
                // 접근 경고만 아주 약하게 깜빡(효과 낮음이면 고정). 게임 시간 기준이라 일시정지 중엔 멈춘다.
                float a = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
                mark.color = new Color(1f, 0.55f, 0.4f, a);
            }
        }

        public void RefreshForTests() => Refresh();

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.2f;
            if (masterRoot == null && game != null && game.Master != null) BuildMaster(); // HUD가 Director보다 먼저 만들어진다
            var info = Compute(game);
            Last = info;
            label.text = info.text ?? "";
            var canvas = rt.GetComponentInParent<Canvas>();
            float k = DrinkRunHud.ReadableScale(Font, canvas != null ? canvas.scaleFactor : 1f);
            rt.localScale = new Vector3(k, k, 1f);
            // 끝없는 모드는 두 줄(같은 자리, 높이만 늘림 — 이 모드에는 보스 체력바가 없다).
            rt.sizeDelta = masterRoot != null ? new Vector2(MasterW, MasterH) : info.state == RunPhaseState.Endless ? new Vector2(BadgeW + 200f, BadgeH * 1.8f) : new Vector2(BadgeW, BadgeH);
            RefreshMaster();
            label.rectTransform.sizeDelta = new Vector2(rt.sizeDelta.x - 36f - 84f, rt.sizeDelta.y - 4f);
            switch (info.state)
            {
                case RunPhaseState.BossFight:
                    bg.color = new Color(0.24f, 0.06f, 0.07f, 0.9f);
                    mark.enabled = true; mark.color = new Color(1f, 0.4f, 0.35f);
                    label.color = new Color(1f, 0.85f, 0.8f);
                    break;
                case RunPhaseState.BossIncoming:
                    bg.color = new Color(0.2f, 0.09f, 0.05f, 0.9f);
                    mark.enabled = true;
                    if (HitFeedback.LowEffects) mark.color = new Color(1f, 0.55f, 0.4f);
                    label.color = new Color(1f, 0.82f, 0.6f);
                    break;
                default:
                    bg.color = new Color(0.06f, 0.05f, 0.08f, 0.82f);
                    mark.enabled = false;
                    label.color = new Color(0.9f, 0.9f, 0.88f);
                    break;
            }
            glyph.enabled = mark.enabled; // 경고 모양(느낌표 칸)은 접근·보스전에서만
            for (int i = 0; i < pips.Length; i++)
                pips[i].color = i < info.danger ? (info.danger >= 4 ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.8f, 0.45f)) : new Color(1f, 1f, 1f, 0.18f);
        }
    }
}
