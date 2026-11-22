using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-22 플레이 피드백 D(6): 혼합 러시(바텐더 직업 효과)가 켜져 있는 동안 플레이어 발밑에 주황 오라.
    /// 바닥 원판(GroundZone 순서 — 몸·몹 아래) + 바깥 고리가 천천히 숨쉰다. 2026-11-22 주인 결정으로 러시에 지속시간이 없어져,
    /// 꺼지는 순간(같은 카드 선택)엔 0.45초 동안 크게 퍼지며 사라져 "꺼짐"이 보이게 한다.
    /// 저이펙트: 숨쉬기·퍼짐 없이 켜짐/꺼짐만. 표시 전용(GameManager.MixingRushActive만 읽음).
    /// 원판은 절차 생성 원(임시 그림 — 승인 아트 없음).
    /// </summary>
    public class MixingRushAura : MonoBehaviour
    {
        GameManager game;
        SpriteRenderer disc, ring;
        public const float Radius = 1.15f;
        public static readonly Color AuraColor = new Color(1f, 0.6f, 0.28f);

        public bool Showing => disc != null && disc.enabled;
        public float Alpha => disc != null ? disc.color.a : 0f;

        public static MixingRushAura Attach(PlayerController player, GameManager game)
        {
            var go = new GameObject("MixingRushAura");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var a = go.AddComponent<MixingRushAura>();
            a.game = game;
            a.disc = Make(go.transform, "Disc", Radius * 2f, SortOrders.GroundZone);
            a.ring = Make(go.transform, "Ring", Radius * 2.3f, SortOrders.GroundZone);
            a.Apply(0f);
            return a;
        }

        static SpriteRenderer Make(Transform parent, string name, float diameter, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.sortingOrder = order;
            float size = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
            go.transform.localScale = new Vector3(diameter / size, diameter / size * 0.45f, 1f); // 바닥에 눕힌 타원(벨트스크롤 원근)
            return sr;
        }

        void LateUpdate()
        {
            Apply(game != null && game.Job == Job.Bartender && game.MixingRushActive ? 1f : 0f);
        }

        bool wasOn;
        float offT = -1f;
        public const float OffSeconds = 0.45f;
        /// <summary>꺼짐 연출 진행 중인지(테스트·QA).</summary>
        public bool FadingOut => offT >= 0f;

        void Apply(float onValue)
        {
            bool on = onValue > 0f;
            bool low = HitFeedback.LowEffects;
            if (wasOn && !on && !low) offT = 0f;
            wasOn = on;
            if (!on && offT >= 0f)
            {
                offT += Time.deltaTime;
                float k = Mathf.Clamp01(offT / OffSeconds);
                disc.enabled = ring.enabled = true;
                disc.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, 0.3f * (1f - k));
                ring.color = new Color(1f, 0.82f, 0.45f, 0.5f * (1f - k));
                ring.transform.localScale = BaseScale(ring) * (1f + 0.8f * k);
                if (k >= 1f) offT = -1f;
                return;
            }
            disc.enabled = ring.enabled = on;
            if (!on) return;
            float t = Time.time;
            float breathe = low ? 0f : 0.5f + 0.5f * Mathf.Sin(t * 5f);
            disc.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, 0.22f + 0.08f * breathe);
            ring.color = new Color(1f, 0.82f, 0.45f, 0.18f + 0.22f * breathe);
            ring.transform.localScale = BaseScale(ring) * (1f + 0.06f * breathe);
        }

        Vector3 baseRing = Vector3.zero;
        Vector3 BaseScale(SpriteRenderer r)
        {
            if (baseRing == Vector3.zero)
            {
                float size = r.sprite != null ? r.sprite.bounds.size.x : 1f;
                float d = Radius * 2.3f / size;
                baseRing = new Vector3(d, d * 0.45f, 1f);
            }
            return baseRing;
        }
    }
}
