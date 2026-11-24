using UnityEngine;

namespace CCT
{
    /// <summary>
    /// #23(2026-11-24) 바텐더 총 근거리 산탄 폭발 준비 표시: 캐릭터 머리 바로 위에 총알 3개를 가로로.
    /// - 기존 탄 아트("bullet")를 세워 쓴다(없으면 캡슐 도형). 빈 탄 = 어두운 실루엣만, 찬 탄 = 실루엣 + 밝은 탄 채움 — 색뿐 아니라 채움 유무로 구분.
    /// - 플레이어 루트의 자식이라 이동을 그대로 따라가고, 좌우 반전은 몸체(Visual)만 하므로 표시가 뒤집히지 않는다.
    /// - 높이는 처음 측정한 몸체 윗변 + 여백으로 고정(애니메이션 프레임마다 흔들리지 않게).
    /// - 히든이 켜져 있을 때(HiddenSkillRuntime.GunVolleyActive)만 보이고, 사망하면 숨긴다. 런이 끝나면 플레이어와 함께 파괴된다.
    /// - 폭발 순간에는 내부 카운터가 즉시 0이 되지만 표시는 VolleyFlashSeconds 동안 3/3(살짝 커짐)을 보여 준다. 저효과 설정이면 플래시 없이 상태만.
    /// 판정·피해와 무관한 표시 전용.
    /// </summary>
    public sealed class GunVolleyPips : MonoBehaviour
    {
        public const float PipLength = 0.22f, PipSpacing = 0.2f, HeadMargin = 0.22f, DefaultHeadTop = 1.2f;
        public static readonly Color EmptyColor = new Color(0.12f, 0.1f, 0.14f, 0.85f);
        public static readonly Color FillColor = new Color(1f, 0.86f, 0.42f, 1f);

        PlayerController player;
        Transform root;
        readonly SpriteRenderer[] shells = new SpriteRenderer[HiddenSkillRuntime.VolleyHitsPerBurst];
        readonly SpriteRenderer[] fills = new SpriteRenderer[HiddenSkillRuntime.VolleyHitsPerBurst];
        bool heightFixed;

        public bool Visible => root != null && root.gameObject.activeSelf;
        public int FilledShown { get; private set; }
        public float HeightLocal => root != null ? root.localPosition.y : 0f;

        public static GunVolleyPips Attach(PlayerController player)
        {
            if (player == null) return null;
            var p = player.GetComponent<GunVolleyPips>() ?? player.gameObject.AddComponent<GunVolleyPips>();
            p.player = player;
            p.Build();
            return p;
        }

        void Build()
        {
            if (root != null) return;
            root = new GameObject("GunVolleyPips").transform;
            root.SetParent(player.transform, false);
            root.localPosition = new Vector3(0f, DefaultHeadTop + HeadMargin, 0f);
            var art = ArtLibrary.Get("bullet");
            Sprite sprite = art != null ? art.FirstSprite : null;
            float longest = art != null ? Mathf.Max(0.01f, art.ContentWorldSize.x) : 1f;
            if (sprite == null) sprite = SpriteFactory.Capsule;
            float scale = PipLength / longest;
            for (int i = 0; i < shells.Length; i++)
            {
                var pip = new GameObject("Pip" + i).transform;
                pip.SetParent(root, false);
                pip.localPosition = new Vector3((i - (shells.Length - 1) * 0.5f) * PipSpacing, 0f, 0f);
                pip.localRotation = Quaternion.Euler(0f, 0f, 90f); // 탄 아트는 오른쪽이 진행 방향 → 세운다
                shells[i] = Part(pip, "Shell", sprite, scale * 1.18f, EmptyColor, SortOrders.AirVfx);
                fills[i] = Part(pip, "Fill", sprite, scale, FillColor, SortOrders.AirVfx + 1);
            }
            root.gameObject.SetActive(false);
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void LateUpdate() => Refresh();

        /// <summary>표시 갱신(시험에서 직접 부를 수 있다).</summary>
        public void Refresh()
        {
            if (player == null || root == null) return;
            var rt = player.HiddenRuntime;
            bool show = rt != null && !player.IsDead && rt.GunVolleyActive;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) { FilledShown = 0; return; }
            if (!heightFixed) FixHeight();
            int filled = Mathf.Clamp(rt.VolleyPipsShown, 0, fills.Length);
            FilledShown = filled;
            for (int i = 0; i < fills.Length; i++)
                if (fills[i].enabled != (i < filled)) fills[i].enabled = i < filled;
            float pulse = rt.VolleyFlashing ? 1.15f : 1f;
            if (!Mathf.Approximately(root.localScale.x, pulse)) root.localScale = Vector3.one * pulse;
        }

        /// <summary>몸체 스프라이트 윗변을 한 번 재서 높이를 고정한다(몸체가 아직 없으면 기본값으로 두고 다음에 다시 잰다).</summary>
        void FixHeight()
        {
            var body = player.Visual != null ? player.Visual.Body : null;
            if (body == null || body.sprite == null) return;
            float top = body.bounds.max.y - player.transform.position.y;
            if (top <= 0.1f) return;
            root.localPosition = new Vector3(0f, Mathf.Clamp(top, 0.8f, 2.4f) + HeadMargin, 0f);
            heightFixed = true;
        }
    }
}
