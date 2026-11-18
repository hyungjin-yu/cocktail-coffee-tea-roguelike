using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 21: 현재 HP / 최대 HP가 20% "미만"이면 HP 바 테두리 맥동 + "! 위험" 칩 + 화면 가장자리 약한 붉은 띠.
    /// 정확히 20%는 정상. 회복·최대 HP 증가로 20% 이상이 되면 같은 프레임에 정상화. 사망(0)·새 런은 꺼짐.
    /// 저이펙트 설정: 맥동 없이 고정 표시(가장자리 띠도 고정의 옅은 값). 화면 흔들림·점멸·반복 경고음 없음(전용 경고음 아트가 없어 소리는 넣지 않음).
    /// 가장자리 띠는 화면 끝 28(1080 기준)만 덮고 입력을 막지 않아 적 공격 예고·조작을 가리지 않는다.
    /// </summary>
    public class LowHpWarning : MonoBehaviour
    {
        public const float Threshold = 0.2f;
        public const float EdgeWidth = 28f;
        Image outline;
        GameObject chip;
        Image[] edges;
        float t;
        public bool Active { get; private set; }
        public bool ChipVisible => chip != null && chip.activeSelf;
        public float EdgeAlpha => edges != null && edges.Length > 0 ? edges[0].color.a : 0f;

        /// <summary>경계 판정: hp/max &lt; 20%(정확히 20%는 정상), hp ≤ 0(사망)·max ≤ 0은 표시 안 함.</summary>
        public static bool IsLow(float hp, float max) => max > 0f && hp > 0f && hp * 5f < max; // hp/max < 0.2 — 부동소수 경계(정확히 20%)를 곱셈으로 판정

        public static LowHpWarning Attach(RectTransform hpBarRoot, RectTransform hudRoot)
        {
            var go = new GameObject("LowHpWarning", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(hudRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling(); // 가장자리 띠는 HUD 글·버튼보다 뒤
            var w = go.AddComponent<LowHpWarning>();

            var o = UiFactory.Place("LowHpOutline", hpBarRoot.parent, hpBarRoot.anchorMin, hpBarRoot.anchoredPosition - new Vector2(6f, -6f) * 1f, hpBarRoot.sizeDelta + new Vector2(12f, 12f));
            o.pivot = hpBarRoot.pivot;
            o.anchoredPosition = hpBarRoot.anchoredPosition + new Vector2(-6f, 6f);
            o.SetSiblingIndex(hpBarRoot.GetSiblingIndex()); // 바 뒤
            w.outline = UiFactory.AddImage(o, new Color(1f, 0.25f, 0.2f, 0f));
            w.outline.raycastTarget = false;

            // HP 바 안쪽 오른쪽 끝(가운데 "HP n / m" 글과 겹치지 않는 자리). 바 오른쪽 바깥은 가운데 게이지 판과 부딪혔다(2026-11-15 720p 캡처).
            var c = UiFactory.Place("LowHpChip", hpBarRoot, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(96f, hpBarRoot.sizeDelta.y - 4f));
            UiFactory.AddImage(c, new Color(0.55f, 0.06f, 0.06f, 0.92f)).raycastTarget = false;
            var label = UiFactory.AddText(UiFactory.Stretch("Label", c), "! 위험", 24, TextAnchor.MiddleCenter, Color.white);
            label.raycastTarget = false;
            w.chip = c.gameObject;
            w.chip.SetActive(false);

            w.edges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var e = UiFactory.Stretch("Edge" + i, rt);
                if (i == 0) { e.anchorMin = new Vector2(0f, 0f); e.anchorMax = new Vector2(0f, 1f); e.sizeDelta = new Vector2(EdgeWidth, 0f); e.pivot = new Vector2(0f, 0.5f); e.anchoredPosition = Vector2.zero; }
                if (i == 1) { e.anchorMin = new Vector2(1f, 0f); e.anchorMax = new Vector2(1f, 1f); e.sizeDelta = new Vector2(EdgeWidth, 0f); e.pivot = new Vector2(1f, 0.5f); e.anchoredPosition = Vector2.zero; }
                if (i == 2) { e.anchorMin = new Vector2(0f, 0f); e.anchorMax = new Vector2(1f, 0f); e.sizeDelta = new Vector2(0f, EdgeWidth); e.pivot = new Vector2(0.5f, 0f); e.anchoredPosition = Vector2.zero; }
                if (i == 3) { e.anchorMin = new Vector2(0f, 1f); e.anchorMax = new Vector2(1f, 1f); e.sizeDelta = new Vector2(0f, EdgeWidth); e.pivot = new Vector2(0.5f, 1f); e.anchoredPosition = Vector2.zero; }
                w.edges[i] = UiFactory.AddImage(e, new Color(0.85f, 0.05f, 0.05f, 0f));
                w.edges[i].raycastTarget = false;
            }
            return w;
        }

        /// <summary>HUD 갱신마다 호출. 일시정지·결과 화면에서는 hidden=true로 가장자리 띠를 끈다(칩·테두리는 HP 상태 그대로).</summary>
        public void Tick(float hp, float max, bool hidden)
        {
            bool low = IsLow(hp, max);
            Active = low;
            bool calm = SettingsService.Current.effectsLevel == SettingsData.EffectsLow;
            if (low) t += Time.unscaledDeltaTime; else t = 0f;
            float pulse = calm ? 0.6f : 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 4.2f)); // 약 0.67Hz 완만한 맥동
            if (chip.activeSelf != low) chip.SetActive(low);
            var oc = outline.color; oc.a = low ? pulse : 0f; outline.color = oc;
            float edgeA = !low || hidden ? 0f : calm ? 0.10f : 0.06f + 0.10f * (0.5f + 0.5f * Mathf.Sin(t * 4.2f));
            foreach (var e in edges) { var c = e.color; c.a = edgeA; e.color = c; }
        }
    }
}
