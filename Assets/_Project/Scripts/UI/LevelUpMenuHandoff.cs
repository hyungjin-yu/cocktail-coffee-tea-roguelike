using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-22 레벨업 메뉴판 건네기(원본 ArtSource/v1/levelup-menu-2026-11-22/manifest.json). 2026-11-23 항목 8 후속: 공통 한 번 대신
    /// **카드마다 자기 등급의 동작**을 그 슬롯 자리에서 바텐더·차 소믈리에 실제 직업 외형으로 재생하고, 카드마다 자기 등급의 내부 장식을 쓴다.
    /// 프레임 표(ms)는 원본 그대로이며 재생 속도는 설정(기본 0.5배 = 약 2배 길이)으로 늘이거나 줄인다.
    /// 시간은 모두 Time.unscaledDeltaTime(시간 정지 중 재생), 표시 전용 — 추첨·효과·저장과 무관, 무음.
    /// </summary>
    public static class LevelUpMenuArt
    {
        /// <summary>등급(0 일반 · 1 희귀 · 2 영웅 · 3 특수)별 프레임 시간(ms). 두 직업이 같은 표를 쓴다. 매니페스트와 EditMode 시험이 대조한다.</summary>
        static readonly int[][] FrameMs =
        {
            new[] { 55, 65, 70 },
            new[] { 55, 70, 65, 60 },
            new[] { 55, 65, 70, 65, 65 },
            new[] { 55, 65, 65, 65, 65, 65 },
        };
        static readonly string[] TierNames = { "common", "rare", "epic", "special" };

        public const float RevealSeconds = 0.070f;
        public const float StartOffsetFraction = 0.88f, StartScale = 0.94f;
        /// <summary>128×96 스프라이트를 1920×1080 기준 3배(384×288)로 표시. 판 가운데 선의 앵커는 (64,32) → 정규화 피벗 (0.5, 1/3).</summary>
        public const int CanvasWidthPx = 128, CanvasHeightPx = 96, DisplayScale = 3;
        public static readonly Vector2 Pivot = new Vector2(0.5f, 1f / 3f);
        /// <summary>카드 묶음 중앙(1920×1080 기준 화면 중앙 Y −30), 하이브리드 패널 중앙(Y +105)에 앵커를 둔다.</summary>
        public const float CardGroupAnchorY = -30f, HybridAnchorY = 105f;

        public static int Tier(CardRarity r) => Mathf.Clamp((int)r, 0, 3);

        /// <summary>설정의 재생 속도 배율(기본 0.5). 1 = 원본 프레임 시간, 0.5 = 두 배 길이. 저장 값은 25~200%로 정리된다.</summary>
        public static float PlaybackSpeed =>
            Mathf.Clamp(SettingsService.Current.levelUpAnimPercent, SettingsService.LevelUpAnimMinPercent, SettingsService.LevelUpAnimMaxPercent) / 100f;
        /// <summary>실제(벽시계) 선택 가능 시점(초) = 원본 선택 시점 ÷ 재생 속도.</summary>
        public static float RealSelectSeconds(int tier, float speed) => SelectMs(tier) / 1000f / Mathf.Max(0.01f, speed);
        public static int FrameCount(int tier) => FrameMs[Mathf.Clamp(tier, 0, 3)].Length;
        public static int FrameMsAt(int tier, int frame) => FrameMs[Mathf.Clamp(tier, 0, 3)][frame];

        /// <summary>전달 종료(ms) = 프레임 시간 합. 선택 가능 시점(ms)은 여기에 페이드 70 ms를 더한 값.</summary>
        public static int HandoffMs(int tier)
        {
            int sum = 0;
            foreach (int ms in FrameMs[Mathf.Clamp(tier, 0, 3)]) sum += ms;
            return sum;
        }
        public static int SelectMs(int tier) => HandoffMs(tier) + Mathf.RoundToInt(RevealSeconds * 1000f);

        /// <summary>표시 후보의 최고 등급(없으면 -1) — 카드별 재생 중 가장 늦게 정착하는 슬롯(입력이 열리는 시점)의 기준.</summary>
        public static int MaxTier(System.Collections.Generic.IReadOnlyList<CardOffer> offers)
        {
            int max = -1;
            if (offers != null) foreach (var o in offers) if (o.card != null) max = Mathf.Max(max, Tier(o.card.rarity));
            return max;
        }

        /// <summary>순수 시간표: t초 시점의 프레임·카드 노출 비율(0~1)·완료 여부. 전달 중에는 reveal 0, 전달 종료 뒤 70 ms 동안 0→1.</summary>
        public static void Evaluate(int tier, float t, out int frame, out float reveal, out bool done)
        {
            var ms = FrameMs[Mathf.Clamp(tier, 0, 3)];
            float acc = 0f; frame = ms.Length - 1;
            for (int i = 0; i < ms.Length; i++) { acc += ms[i] / 1000f; if (t < acc) { frame = i; break; } }
            float handoff = HandoffMs(tier) / 1000f;
            reveal = t < handoff ? 0f : Mathf.Clamp01((t - handoff) / RevealSeconds);
            done = t >= handoff + RevealSeconds - 1e-4f;
        }

        public static bool HasClip(Job job) => job == Job.Bartender || job == Job.TeaSommelier;
        static string JobFolder(Job job) => job == Job.TeaSommelier ? "tea_sommelier" : "bartender";

        public static Sprite[] Frames(Job job, int tier)
        {
            if (!HasClip(job)) return null;
            tier = Mathf.Clamp(tier, 0, 3);
            var list = new Sprite[FrameCount(tier)];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = Resources.Load<Sprite>($"LevelUpMenu/{JobFolder(job)}/{TierNames[tier]}/{i:00}");
                if (list[i] == null) return null; // 한 장이라도 없으면 연출 없이 기존 경로
            }
            return list;
        }

        public static Sprite CardDecoration(int tier) => Resources.Load<Sprite>($"LevelUpMenu/card_{TierNames[Mathf.Clamp(tier, 0, 3)]}");
        public static Sprite HybridDecoration() => Resources.Load<Sprite>("LevelUpMenu/hybrid_special");
    }

    /// <summary>한 번의 건네기 재생 상태. 같은 인스턴스를 다시 Begin하면 이전 재생을 지우고 새로 시작한다(중복 실행 없음).
    /// t는 원본 시간표(클립 시간) 기준 — 벽시계 delta × 재생 속도를 누적한다.</summary>
    public sealed class MenuHandoffPlayer
    {
        Image image;
        Sprite[] frames;
        int tier;
        float t;
        int startFrame;
        float speed = 1f;

        public bool Active { get; private set; }
        public int Tier => tier;
        /// <summary>이번 재생의 속도 배율(Begin 때 고정 — 재생 중 설정을 바꿔도 다음 재생부터).</summary>
        public float Speed => speed;
        public Sprite CurrentSprite => Active && image != null ? image.sprite : null;
        public int Frame { get; private set; }
        /// <summary>카드 그룹 노출 비율. 전달 중 0 → 종료 뒤 70 ms 동안 1.</summary>
        public float Reveal { get; private set; }

        public void Begin(Image target, Sprite[] clip, int clipTier) => Begin(target, clip, clipTier, LevelUpMenuArt.PlaybackSpeed);

        public void Begin(Image target, Sprite[] clip, int clipTier, float playbackSpeed)
        {
            Stop();
            image = target; frames = clip; tier = clipTier; t = 0f; startFrame = Time.frameCount;
            speed = Mathf.Clamp(playbackSpeed, 0.25f, 2f);
            Active = image != null && clip != null && clip.Length > 0;
            if (!Active) return;
            Apply();
            image.gameObject.SetActive(true);
        }

        /// <summary>끝나면 true(이번 호출에서 완료). 완료·정지 시 이미지는 꺼진다.</summary>
        public bool Tick(float unscaledDelta)
        {
            if (!Active) return false;
            // 시작한 프레임의 delta는 시작 이전 시간을 포함하므로 세지 않는다(카드 창이 뜨는 프레임의 끊김이 전달을 잘라먹지 않게).
            if (Time.frameCount != startFrame) t += Mathf.Min(unscaledDelta, 0.1f) * speed; // 프레임 정지 뒤 한 번에 건너뛰지 않게 한 틱 상한(벽시계 기준)
            Apply();
            LevelUpMenuArt.Evaluate(tier, t, out _, out _, out bool done);
            if (!done) return false;
            Stop();
            Reveal = 1f;
            return true;
        }

        void Apply()
        {
            LevelUpMenuArt.Evaluate(tier, t, out int frame, out float reveal, out _);
            Frame = frame; Reveal = reveal;
            image.sprite = frames[frame];
            image.color = new Color(1f, 1f, 1f, 1f - reveal); // 마지막 포즈를 고정하고 70 ms 페이드 아웃
        }

        public void Stop()
        {
            if (image != null) { image.gameObject.SetActive(false); image.color = Color.white; }
            Active = false; Reveal = 0f; Frame = 0; t = 0f; image = null; frames = null;
        }

        /// <summary>메뉴판 이미지(384×288, 피벗 (0.5,1/3))를 만들고 앵커를 anchorY에 둔다. 클릭을 막지 않는다.</summary>
        public static Image CreateImage(RectTransform parent, string name, float anchorY)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0f, anchorY),
                new Vector2(LevelUpMenuArt.CanvasWidthPx * LevelUpMenuArt.DisplayScale, LevelUpMenuArt.CanvasHeightPx * LevelUpMenuArt.DisplayScale));
            rt.pivot = LevelUpMenuArt.Pivot;
            rt.anchoredPosition = new Vector2(0f, anchorY);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true; img.raycastTarget = false;
            rt.gameObject.SetActive(false);
            return img;
        }
    }
}
