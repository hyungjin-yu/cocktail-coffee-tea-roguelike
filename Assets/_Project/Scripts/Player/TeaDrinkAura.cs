using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 Beverage Master 항목 16: 차 소믈리에가 실제로 음료 버프를 적용 중일 때만 발밑·주변에 보이는 얇은 "찻잔 잔물결 + 김" 오라.
    /// 모티프는 작은 찻잔의 물결 두 겹(발밑 납작한 연옥빛 고리가 천천히 퍼져 사라짐)과 옆으로 피어오르는 가는 김 세 가닥.
    /// 바텐더 혼합 러시(주황 큰 원판)·기존 음료색 발밑 원·적 공격/둔화 장판(붉은·보라 면)과 색(옥/백)·크기(얇은 고리)로 구분한다.
    /// 상태 근거: GameManager.DrinkBuff(실제 적용된 버프 — 입장 음용·마스터 제조 후 마시기 교체 모두 이 값이 곧 현재 상태). 음료가 없으면 만들지도 켜지지도 않는다.
    /// 교체(DrinkBuff 참조가 바뀜)하면 잔물결을 처음부터 다시 퍼뜨려 "바뀜"만 보이게 한다(누적 없음 — 렌더러는 고정 풀). 환불 봉인(BonusesSuppressed) 중엔 회색으로 옅어진다.
    /// 저이펙트: 움직임·김 없이 옅은 고리 하나만 고정. 표시 전용 — 효과 수치·판정·충돌 없음. 새 아트 없음(고리 텍스처는 코드 생성, 김은 기존 원 도형).
    /// 플레이어 자식 오브젝트라 런 종료·재시작(GameManager 재생성)과 함께 사라지고, 코루틴·정적 상태·외부 스폰이 없어 누수 경로가 없다.
    /// </summary>
    public class TeaDrinkAura : MonoBehaviour
    {
        public const int Ripples = 2, Wisps = 3;
        public const float RippleSeconds = 2.6f, WispSeconds = 1.8f, FadeSeconds = 0.3f;
        public const float RippleMinRadius = 0.32f, RippleMaxRadius = 0.95f, GroundSquash = 0.42f;
        public const float RingAlpha = 0.26f, WispAlpha = 0.3f, LowRingAlpha = 0.2f;
        public static readonly Color JadeTint = new Color(0.72f, 0.96f, 0.84f);
        static readonly Color SuppressedTint = new Color(0.6f, 0.6f, 0.6f);

        PlayerController player;
        GameManager game;
        Transform root;
        readonly SpriteRenderer[] ripples = new SpriteRenderer[Ripples];
        readonly SpriteRenderer[] wisps = new SpriteRenderer[Wisps];
        DrinkBuff lastBuff;
        float fade, clock;

        static Sprite ringSprite;

        /// <summary>지금 오라(고리·김)가 켜져 있는지(페이드 아웃 중 포함) — 테스트·QA.</summary>
        public bool Showing => root != null && root.gameObject.activeSelf;
        /// <summary>전체 불투명도 0~1(켜짐 1, 꺼짐 0).</summary>
        public float Fade => fade;
        /// <summary>저이펙트 단순 마커 모드로 그리는 중인지.</summary>
        public bool SimpleMarker { get; private set; }
        /// <summary>보이는 김 가닥 수(저이펙트면 0).</summary>
        public int VisibleWisps { get { int n = 0; foreach (var w in wisps) if (w != null && w.enabled) n++; return n; } }
        /// <summary>보이는 고리 수(풀 상한 Ripples).</summary>
        public int VisibleRipples { get { int n = 0; foreach (var r in ripples) if (r != null && r.enabled) n++; return n; } }
        /// <summary>렌더러 풀 크기(항상 Ripples+Wisps 고정 — 스폰 누수 없음).</summary>
        public int PoolSize => Ripples + Wisps;

        /// <summary>오라를 켜야 하는 실제 상태: 차 소믈리에 + 적용된 음료 버프 + 진행 중 + 생존.</summary>
        public static bool ShouldShow(Job job, bool hasDrinkBuff, bool playing, bool alive) =>
            job == Job.TeaSommelier && hasDrinkBuff && playing && alive;

        /// <summary>잔물결 한 겹의 진행도(0~1) → 반지름. 위상은 0~1 순환.</summary>
        public static float RippleRadius(float phase) => Mathf.Lerp(RippleMinRadius, RippleMaxRadius, Mathf.Clamp01(phase));
        /// <summary>잔물결 불투명도: 빠르게 나타나 끝으로 갈수록 사라진다.</summary>
        public static float RippleAlphaAt(float phase)
        {
            phase = Mathf.Clamp01(phase);
            return RingAlpha * Mathf.Clamp01(phase / 0.12f) * (1f - phase);
        }

        public static TeaDrinkAura Attach(PlayerController player, GameManager game)
        {
            if (player == null) return null;
            var go = new GameObject("TeaDrinkAura");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, -PlayerController.Radius, 0f); // 발밑(기존 음료 표식과 같은 높이)
            var a = go.AddComponent<TeaDrinkAura>();
            a.player = player; a.game = game; a.root = go.transform;
            a.Build();
            go.SetActive(false);
            return a;
        }

        void Build()
        {
            for (int i = 0; i < Ripples; i++)
            {
                var g = new GameObject("Ripple" + i);
                g.transform.SetParent(root, false);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = RingSprite();
                sr.sortingOrder = SortOrders.Shadow; // 바닥 위험 장판(GroundZone)·적보다 아래
                sr.enabled = false;
                ripples[i] = sr;
            }
            for (int i = 0; i < Wisps; i++)
            {
                var g = new GameObject("Wisp" + i);
                g.transform.SetParent(root, false);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.sortingOrder = SortOrders.AirVfx;
                sr.enabled = false;
                wisps[i] = sr;
            }
        }

        /// <summary>지름 1유닛의 얇은 고리(바깥 가장자리만 밝고 안쪽은 투명). 코드 생성·1회 캐시 — 편집용 아트 아님.</summary>
        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f, thick = 3.2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (r - thick - 1f)) / thick);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return ringSprite;
        }

        void LateUpdate()
        {
            if (player == null || game == null) return;
            bool want = ShouldShow(game.Job, game.DrinkBuff != null, game.State == GameState.Playing, !player.IsDead);
            Tick(Time.deltaTime, want);
        }

        /// <summary>한 프레임 갱신. dt = 게임 시간(정지 중 0). want = ShouldShow 결과.</summary>
        public void Tick(float dt, bool want)
        {
            var buff = game != null ? game.DrinkBuff : null;
            if (want && lastBuff != null && buff != lastBuff) clock = 0f; // 교체: 잔물결 다시 시작
            lastBuff = want ? buff : null;

            // 꺼질 때는 런 종료(timeScale 0)에서도 사라지도록 실시간 기준을 함께 쓴다.
            float fdt = want ? dt : Mathf.Max(dt, Time.unscaledDeltaTime);
            fade = Mathf.MoveTowards(fade, want ? 1f : 0f, fdt / FadeSeconds);
            if (!want && fade <= 0f)
            {
                clock = 0f;
                SetAllEnabled(false);
                if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
                return;
            }
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

            bool low = HitFeedback.LowEffects;
            SimpleMarker = low;
            bool sup = player != null && player.Stats != null && player.Stats.BonusesSuppressed;
            Color tint = sup ? SuppressedTint : JadeTint;
            if (!low) clock += dt;

            if (low)
            {
                // 단순 마커: 옅은 고리 하나 고정.
                for (int i = 0; i < Ripples; i++)
                {
                    var r = ripples[i];
                    r.enabled = i == 0;
                    if (i != 0) continue;
                    PlaceRing(r, 0.62f);
                    r.color = new Color(tint.r, tint.g, tint.b, LowRingAlpha * fade * (sup ? 0.5f : 1f));
                }
                foreach (var w in wisps) w.enabled = false;
                return;
            }

            for (int i = 0; i < Ripples; i++)
            {
                float phase = Mathf.Repeat(clock / RippleSeconds + i / (float)Ripples, 1f);
                var r = ripples[i];
                r.enabled = true;
                PlaceRing(r, RippleRadius(phase));
                r.color = new Color(tint.r, tint.g, tint.b, RippleAlphaAt(phase) * fade * (sup ? 0.5f : 1f));
            }
            for (int i = 0; i < Wisps; i++)
            {
                float phase = Mathf.Repeat(clock / WispSeconds + i / (float)Wisps, 1f);
                float side = i == 0 ? -0.42f : i == 1 ? 0.38f : 0.02f; // 몸 옆 두 가닥 + 가운데 뒤 한 가닥
                var w = wisps[i];
                w.enabled = true;
                float sway = Mathf.Sin((clock * 1.7f) + i * 2.1f) * 0.07f;
                w.transform.localPosition = new Vector3(side + sway, 0.12f + phase * 0.75f, 0f);
                float s = Mathf.Lerp(0.09f, 0.2f, phase);
                w.transform.localScale = new Vector3(s * 0.8f, s * 1.25f, 1f); // 위로 길쭉한 가는 김
                float a = WispAlpha * Mathf.Sin(Mathf.Clamp01(phase) * Mathf.PI) * fade * (sup ? 0.5f : 1f);
                w.color = new Color(0.94f, 1f, 0.97f, a);
            }
        }

        static void PlaceRing(SpriteRenderer r, float radius)
        {
            float d = radius * 2f;
            r.transform.localScale = new Vector3(d, d * GroundSquash, 1f); // 바닥에 눕힌 타원(벨트스크롤 원근)
        }

        void SetAllEnabled(bool on)
        {
            foreach (var r in ripples) if (r != null) r.enabled = on;
            foreach (var w in wisps) if (w != null) w.enabled = on;
        }
    }
}
