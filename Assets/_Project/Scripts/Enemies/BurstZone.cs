using UnityEngine;

namespace CCT
{
    /// <summary>올리브 바머가 자폭 후 남기는 화염(알콜) 장판. 범위 안에 있으면 지속 피해.
    /// 실제 피해는 PlayerZoneDamage를 거친다 — 겹친 장판끼리는 가장 높은 DPS 하나만 적용된다(06:23).</summary>
    public class BurstZone : MonoBehaviour
    {
        /// <summary>살아 있는 개수(10 §3 고정 부하 시나리오 계측용). 활성/비활성 시점에만 갱신한다.</summary>
        public static int ActiveCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveCount() => ActiveCount = 0;

        void OnEnable() => ActiveCount++;
        void OnDisable() => ActiveCount--;

        PlayerController player;
        float radius;
        float damagePerSecond;
        float remaining;
        float tickTimer;
        /// <summary>67: 이 장판을 만든 적/공격(사망 원인 기록 전용).</summary>
        DamageSource source;

        /// <summary>04 §8 장판 아트: 올리브 바머 화염은 olive_fire, 커피 보스 장판은 coffee_pool.</summary>
        public const string OliveFireArt = "olive_fire";
        public const string CoffeePoolArt = "coffee_pool";
        public const string TeaPoolArt = "tea_pool";

        public static void Spawn(Vector2 position, float radius, float damagePerSecond, float seconds,
            PlayerController player, Transform parent, string artId = OliveFireArt, DamageSource source = default)
        {
            var go = new GameObject("BurstZone");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            ApplyZoneVisual(go, sr, artId, radius, new Color(1f, 0.5f, 0.15f, 0.35f), seconds);

            var zone = go.AddComponent<BurstZone>();
            zone.player = player;
            zone.radius = radius;
            zone.damagePerSecond = damagePerSecond;
            zone.remaining = seconds;
            zone.source = source;
            zone.tickTimer = Balance.ZoneTickInterval; // 첫 틱은 생성 0.4초 후 (06:23)
        }

        /// <summary>장판 그림: 아트가 있으면 판정 원이 타원 안에 들어가는 배율로 반복 재생, 없으면 기존 색 원.</summary>
        /// <summary>43: v1 장판 아트 id → PixelLab v2(판정 원 경계 일치·나타남/소멸 연출). 없으면 v1.</summary>
        public static string V2For(string artId) => artId == OliveFireArt ? "zone_olive_fire_v2" :
            artId == CoffeePoolArt ? "zone_coffee_pool_v2" : artId == TeaPoolArt ? "zone_tea_pool_v2" : artId == "sugar_pool" ? "zone_sugar_pool_v2" : null;

        public static void ApplyZoneVisual(GameObject go, SpriteRenderer sr, string artId, float radius, Color fallback,
            float lifetime = float.MaxValue)
        {
            sr.sortingOrder = SortOrders.GroundZone;
            var v2 = V2For(artId);
            if (v2 != null && ZoneVisualV2.AttachActive(go, v2, radius, lifetime, SortOrders.GroundZone))
            {
                go.transform.localScale = Vector3.one; // 판정 반경은 컴포넌트 값, 그림 배율은 자식에서
                sr.enabled = false;
                return;
            }
            var art = ArtLibrary.Get(artId);
            if (art?.Main == null)
            {
                go.transform.localScale = Vector3.one * (radius * 2f);
                sr.sprite = SpriteFactory.Circle;
                sr.color = fallback;
                return;
            }
            go.transform.localScale = Vector3.one * ArtVfx.ScaleForGroundRadius(art, radius);
            sr.sprite = art.Main.frames[0];
            sr.color = Color.white;
            if (art.Main.frames.Length > 1)
            {
                var anim = go.AddComponent<SpriteAnimator>();
                anim.Bind(sr);
                anim.Play(art.Main);
            }
        }

        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (player == null || player.IsDead) return;

            tickTimer -= Time.deltaTime;
            if (tickTimer > 0f) return;
            tickTimer = Balance.ZoneTickInterval;

            if (Vector2.Distance(transform.position, player.Position) <= radius)
                PlayerZoneDamage.RequestTick(damagePerSecond, source);
        }
    }
}
