using System;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 7: 첫 판을 끝낸 뒤 로비(직업·무기 선택)에서 연구 노트북으로 다음 행동을 잇는 안내.
    /// 결과·획득 내역은 건너뛰지 않는다(로비에 돌아왔을 때만 표시). 구매를 강제하지 않고 건너뛰기·다시 보기를 제공한다.
    ///
    /// 저장(새 규칙): "첫 판 종료" = 기존 save.json clearedRuns + failedRuns + abandonedRuns ≥ 1(새 필드 없음). "안내 완료" = settings.json
    /// notebookGuideVersion(새 필드, 없으면 0 = 미완료 → 기존 저장 그대로 읽힘). 두 상태는 독립: 첫 판 종료(판 수) × 안내 완료(설정) — 숙련 해금 여부로 안내를 막지 않는다.
    /// </summary>
    public static class NotebookGuide
    {
        public const int Version = 1;
        public const string LobbyLine = "이번 플레이로 얻은 재료와 연구를 확인해 보세요";
        public const string LobbySub = "연구 노트북에서 연구 포인트로 숙련을 해금하고 장착합니다";

        /// <summary>시험 전용: 자동 표시를 끈다.</summary>
        public static bool SuppressForTests;

        public static bool Needed =>
            !SuppressForTests
            && SettingsService.Current.notebookGuideVersion < Version
            && FirstRunEnded;

        /// <summary>"첫 판 종료" 상태(save.json 정산 판 수에서 계산 — 안내 완료 플래그와 독립). 숙련 해금 여부와 무관하게 안내 대상이 된다
        /// (기존에 숙련을 해금한 저장도 안내를 한 번 받고 건너뛸 수 있다 — 영구 누락 없음).</summary>
        public static bool FirstRunEnded => PermanentProgress.Current != null && EndedRuns(PermanentProgress.Current) >= 1;
        /// <summary>"안내 완료" 상태(settings.json notebookGuideVersion — 첫 판 종료 상태와 독립, 다시 보기로만 되돌림).</summary>
        public static bool GuideDone => SettingsService.Current.notebookGuideVersion >= Version;

        /// <summary>끝난 판 수(클리어·실패·포기 정산). totalRuns는 출발 때 늘어 "첫 판 종료"로 쓰지 않는다.</summary>
        public static int EndedRuns(SaveData s) => s == null ? 0 : s.clearedRuns + s.failedRuns + s.abandonedRuns;

        public static void Complete() => SettingsService.CompleteNotebookGuide(Version);

        /// <summary>노트북 안 단계 문구(2 = 숙련 위치, 3 = 보유 포인트·가능한 성장). 포인트가 부족하면 설명까지만.</summary>
        public static string StepText(int step)
        {
            var r = DrinkLab.Research;
            if (step == 2)
                return "숙련 탭: 서비스·안정·기교 갈래의 숙련을 연구 포인트로 해금하고, 해금한 숙련을 장착하면 다음 판부터 적용됩니다.";
            int cheapest = int.MaxValue;
            foreach (var t in DrinkResearch.Traits) if (t.tier == 1) cheapest = Math.Min(cheapest, t.cost);
            if (cheapest == int.MaxValue) cheapest = 0;
            return r.researchPoints >= cheapest
                ? $"보유 연구 포인트 {r.researchPoints} — 1단계 숙련(비용 {cheapest})을 해금할 수 있습니다. 원하는 갈래를 골라 노드를 누르세요."
                : $"보유 연구 포인트 {r.researchPoints} — 1단계 숙련은 {cheapest}이 필요합니다. 판을 끝낼 때마다 정산으로 모입니다.";
        }

        /// <summary>안내 말풍선 판(어두운 바탕 + 글 + 버튼 2개). 버튼 이름: Primary, Skip.</summary>
        public static RectTransform Bubble(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string line, string sub,
            string primaryLabel, Action primary, string skipLabel, Action skip)
        {
            var rt = UiFactory.Place(name, parent, anchor, pos, size);
            var bg = UiFactory.AddImage(rt, new Color(0.08f, 0.06f, 0.05f, 0.96f));
            UiFactory.SkinPanel(bg);
            // 2026-11-16 720p 실화면(f2 40_lobby_notebook_guide): 판 스킨 가운데가 반투명이라 뒤 직업 카드 글이 비쳐 보였고, 글 칸이 좁아
            // 제목이 두 줄로 넘쳐 본문과 겹침 → 테두리 안쪽 불투명 판 + 버튼 폭 축소로 글 칸 확대, 제목·본문 각각 한 줄 최적 크기.
            var solid = UiFactory.Stretch("Solid", rt);
            solid.offsetMin = new Vector2(6f, 6f); solid.offsetMax = new Vector2(-6f, -6f);
            UiFactory.AddImage(solid, new Color(0.08f, 0.06f, 0.05f, 1f)).raycastTarget = false;
            solid.SetAsFirstSibling();
            float btnW = 200f;
            float textW = size.x - 2f * btnW - 72f;
            var t = UiFactory.AddText(UiFactory.Place("Line", rt, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(textW, 44f)), line, 28, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.6f));
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 20; t.resizeTextMaxSize = 28;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            var s = UiFactory.AddText(UiFactory.Place("Sub", rt, new Vector2(0f, 1f), new Vector2(24f, -62f), new Vector2(textW, size.y - 74f)), sub ?? "", 22, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.84f));
            s.resizeTextForBestFit = true; s.resizeTextMinSize = 18; s.resizeTextMaxSize = 22;
            s.verticalOverflow = VerticalWrapMode.Truncate;
            s.raycastTarget = false;
            Btn(rt, "Primary", new Vector2(-(btnW + 32f), 0f), btnW, primaryLabel, new Color(0.62f, 0.44f, 0.16f), primary);
            if (skip != null) Btn(rt, "Skip", new Vector2(-16f, 0f), btnW, skipLabel, new Color(0.3f, 0.3f, 0.34f), skip);
            return rt;
        }

        static void Btn(RectTransform parent, string name, Vector2 pos, float w, string label, Color c, Action onClick)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(1f, 0.5f), pos, new Vector2(w, 64f));
            var bg = UiFactory.AddImage(rt, c);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            UiFactory.ApplyHoverColors(b);
            UiFactory.AddFocusSfx(b);
            b.onClick.AddListener(() => onClick?.Invoke());
            var t = UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 26, TextAnchor.MiddleCenter, Color.white);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 22; t.resizeTextMaxSize = 26;
        }
    }

    /// <summary>노트북 버튼 둘레의 완만한 맥동 테두리(저이펙트 설정이면 고정 테두리). 입력은 막지 않는다.</summary>
    public class GuidePulse : MonoBehaviour
    {
        Image frame;
        public static GuidePulse Attach(RectTransform target)
        {
            var rt = UiFactory.Place("GuidePulse", target, new Vector2(0.5f, 0.5f), Vector2.zero, target.sizeDelta + new Vector2(16f, 16f));
            var img = UiFactory.AddImage(rt, new Color(1f, 0.8f, 0.35f, 0.9f));
            img.raycastTarget = false;
            UiFactory.SkinPanel(img);
            rt.SetAsFirstSibling();
            var p = rt.gameObject.AddComponent<GuidePulse>();
            p.frame = img;
            return p;
        }

        void Update()
        {
            if (frame == null) return;
            bool calm = SettingsService.Current.effectsLevel == SettingsData.EffectsLow; // 저이펙트: 깜박이지 않고 고정
            float a = calm ? 0.85f : 0.55f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            var c = frame.color; c.a = a; frame.color = c;
        }
    }
}
