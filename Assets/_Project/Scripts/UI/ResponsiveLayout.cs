using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-10-12 실기기(갤럭시 폴드7 펼침 1664×1499, 1.11:1) 피드백: 기준 1920×1080·가로세로 절반 맞춤이면
    /// 정사각형에 가까운 화면에서 캔버스 폭이 1517로 줄어 3열 트리·HUD 윗줄이 넘치고, 위에 붙은 화면은 아래가 빈다.
    /// 세로가 긴 화면은 폭 1920을, 가로가 긴 화면은 높이 1080을 확보한다. 16:9는 기존 배율을 유지한다.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class AdaptiveCanvasScaler : MonoBehaviour
    {
        public const float ReferenceAspect = 1920f / 1080f;
        CanvasScaler scaler;

        public static float MatchFor(float width, float height)
        {
            if (height <= 0f) return 0.5f;
            float aspect = width / height;
            if (aspect < ReferenceAspect - 0.01f) return 0f;
            if (aspect > ReferenceAspect + 0.01f) return 1f;
            return 0.5f;
        }

        void Awake() { scaler = GetComponent<CanvasScaler>(); Apply(); }
        void Update() => Apply();
        void Apply()
        {
            if (scaler == null) return;
            float m = MatchFor(Screen.width, Screen.height);
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, m)) scaler.matchWidthOrHeight = m;
        }
    }

    /// <summary>
    /// 전체 화면 메뉴(직업 선택·조작 안내·카드 선택·트리)의 내용을 캔버스가 1080보다 높을 때 세로 가운데 1080 띠에 둔다.
    /// 루트 자체를 가운데 띠로 줄이고(자식 경로·배치는 그대로), 배경색·카페 배경은 띠 밖까지 늘려 화면 전체를 덮는다.
    /// 캔버스 높이 ≤ 1080(16:9 데스크톱·가로 폰)이면 아무것도 바꾸지 않는다.
    /// </summary>
    public class CenterBand : MonoBehaviour
    {
        public const float BandHeight = 1080f;
        RectTransform rt, parent, fill, backdrop;
        float lastExtra = -1f;
        Vector2 backdropMin, backdropMax;
        Image rootImage; Color rootColor;

        public static CenterBand Attach(RectTransform root)
        {
            var existing = root.GetComponent<CenterBand>();
            if (existing != null) return existing;
            var band = root.gameObject.AddComponent<CenterBand>();
            band.Init();
            return band;
        }

        void Init()
        {
            rt = (RectTransform)transform;
            parent = rt.parent as RectTransform;
            var img = GetComponent<Image>();
            if (img != null)
            {
                // 띠 밖도 같은 색으로 덮고 입력도 막는 채움(맨 뒤 자식). 루트 이미지는 투명하게(띠 안 이중 어둡게 방지, 입력 차단은 유지).
                fill = UiFactory.Stretch("BandFill", rt);
                fill.SetAsFirstSibling();
                var f = fill.gameObject.AddComponent<Image>();
                f.color = img.color; f.sprite = img.sprite; f.type = img.type;
                rootImage = img; rootColor = img.color;
                fill.gameObject.SetActive(false);
            }
            backdrop = rt.Find("CafeBackdrop") as RectTransform;
            if (backdrop != null) { backdropMin = backdrop.offsetMin; backdropMax = backdrop.offsetMax; }
            Apply();
        }

        void LateUpdate() => Apply();

        public float Extra => parent == null ? 0f : Mathf.Max(0f, parent.rect.height - BandHeight);

        void Apply()
        {
            if (rt == null) return;
            float extra = Extra;
            if (Mathf.Approximately(extra, lastExtra)) return;
            lastExtra = extra;
            float half = extra * 0.5f;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, half); rt.offsetMax = new Vector2(0f, -half);
            if (fill != null)
            {
                // 띠가 필요할 때만 채움을 켜고 루트 이미지를 투명하게(입력 차단은 유지). 16:9 이하는 원래 그대로.
                fill.gameObject.SetActive(extra > 0f);
                fill.offsetMin = new Vector2(0f, -half); fill.offsetMax = new Vector2(0f, half);
                rootImage.color = extra > 0f ? new Color(rootColor.r, rootColor.g, rootColor.b, 0f) : rootColor;
            }
            if (backdrop != null)
            {
                backdrop.offsetMin = backdropMin - new Vector2(0f, half);
                backdrop.offsetMax = backdropMax + new Vector2(0f, half);
            }
        }
    }
}
