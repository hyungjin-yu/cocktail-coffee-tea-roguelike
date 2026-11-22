using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-22 플레이 피드백 D(6): 혼합 러시(바텐더 직업 효과)가 켜져 있는 동안 플레이어 발밑에 주황 오라.
    /// 바닥 원판(GroundZone 순서 — 몸·몹 아래) + 바깥 고리가 천천히 숨쉰다. 2026-11-22 주인 결정으로 러시에 지속시간이 없어져,
    /// 꺼지는 순간(같은 카드 선택)엔 0.45초 동안 크게 퍼지며 사라져 "꺼짐"이 보이게 한다.
    /// 저이펙트: 숨쉬기·퍼짐 없이 켜짐/꺼짐만. 표시 전용(GameManager.MixingRushActive만 읽음).
    /// 정식 아트(2026-11-22 v1-formal-pixels mixing_rush_aura): 켜짐 active 6장·8fps 반복, 꺼짐 off 4장·12fps 단발(0.333초)을 기존
    /// 꺼짐 구간 OffSeconds(0.45초)에 맞춰 샘플링. 저이펙트 = active 첫 컷 고정 + 꺼짐 생략. 아트가 없으면 기존 절차 원으로 떨어진다.
    /// </summary>
    public class MixingRushAura : MonoBehaviour
    {
        GameManager game;
        SpriteRenderer disc, ring;
        ArtClip activeClip, offClip; // 정식 아트(256x128 센터 피벗) — 있으면 disc 한 장만 쓴다
        public const float Radius = 1.15f;
        /// <summary>정식 아트를 쓰는 중인지(테스트·QA).</summary>
        public bool UsesFormalArt => activeClip != null;
        public static readonly Color AuraColor = new Color(1f, 0.6f, 0.28f);

        public bool Showing => disc != null && disc.enabled;
        public float Alpha => disc != null && (activeClip == null || disc.enabled) ? disc.color.a : 0f;

        public static MixingRushAura Attach(PlayerController player, GameManager game)
        {
            var go = new GameObject("MixingRushAura");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var a = go.AddComponent<MixingRushAura>();
            a.game = game;
            var art = ArtLibrary.Get(FeedbackArt.MixingRushAura);
            a.activeClip = art?.Clip("active"); a.offClip = art?.Clip("off");
            if (a.activeClip != null && a.offClip != null)
            {
                var fgo = new GameObject("Aura");
                fgo.transform.SetParent(go.transform, false);
                // 알파 영역 가로(약 200px)를 기존 바깥 고리 지름(Radius*2.3)에 맞춘 균일 배율 — 아트 자체가 바닥 타원이라 Y 압축 없음.
                float s = Radius * 2.3f / (FeedbackArt.AuraContentPx / art.pixelsPerUnit);
                fgo.transform.localScale = new Vector3(s, s, 1f);
                a.disc = fgo.AddComponent<SpriteRenderer>();
                a.disc.sortingOrder = SortOrders.GroundZone;
                a.disc.sprite = a.activeClip.frames[0];
                a.Apply(0f);
                return a;
            }
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
            if (activeClip != null) { ApplyFormal(on, low); return; }
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

        float activeT;
        void ApplyFormal(bool on, bool low)
        {
            disc.color = Color.white; // 텍스처 자체가 반투명 — 코드 틴트·알파 곡선을 겹쳐 쓰지 않는다
            if (!on && offT >= 0f)
            {
                offT += Time.deltaTime;
                float k = Mathf.Clamp01(offT / OffSeconds);
                disc.enabled = true;
                disc.sprite = offClip.frames[Mathf.Min(Mathf.FloorToInt(k * offClip.frames.Length), offClip.frames.Length - 1)];
                if (k >= 1f) { offT = -1f; disc.enabled = false; }
                return;
            }
            disc.enabled = on;
            if (!on) { activeT = 0f; return; }
            activeT += Time.deltaTime;
            disc.sprite = low ? activeClip.frames[0] : activeClip.FrameAt(activeT);
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
