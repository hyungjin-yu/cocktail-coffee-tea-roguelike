using UnityEngine;

namespace CCT
{
    /// <summary>몹이 떨어뜨리는 재료. 플레이어가 가까이 오면 끌려가서 게이지에 들어간다.</summary>
    public class IngredientPickup : MonoBehaviour
    {
        /// <summary>살아 있는 개수(10 §3 고정 부하 시나리오 계측용). 활성/비활성 시점에만 갱신한다.</summary>
        public static int ActiveCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveCount() => ActiveCount = 0;

        void OnEnable() => ActiveCount++;
        void OnDisable() => ActiveCount--;

        const float CollectDistance = 0.35f;

        GameManager game;
        PlayerController player;
        IngredientType type;
        int amount;
        float pullSpeed = 6f;
        bool pulling;
        /// <summary>41 히든: 이 재료가 속한 런의 플레이어 / 강제 끌어오기(탄산 펌프).</summary>
        public PlayerController RunPlayer => player;
        public void ForceAttract() => pulling = true;

        public static readonly Color CocktailColor = new Color(1f, 0.4f, 0.85f);
        public static readonly Color TeaColor = new Color(0.45f, 0.9f, 0.5f);
        public static readonly Color CoffeeColor = new Color(0.65f, 0.42f, 0.25f);

        static Color ColorOf(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Tea: return TeaColor;
                case IngredientType.Coffee: return CoffeeColor;
                default: return CocktailColor;
            }
        }

        /// <summary>실제 아이콘이 있으면 흰색(원본 색 그대로)으로, 없으면 색상 원 placeholder로 대체.</summary>
        /// <summary>57(2026-10-15) 새 드롭 스프라이트(Aseprite+PixelLab, Resources/ArtExtra/drop_*). 없으면 기존 아이콘.</summary>
        public static Sprite DropArt(IngredientType type) => CafeBackground.JobSprite("drop", type switch
        {
            IngredientType.Tea => "tea",
            IngredientType.Coffee => "coffee",
            _ => "cocktail",
        });

        static (Sprite sprite, Color color, bool isRealIcon) VisualFor(IngredientType type)
        {
            var drop = DropArt(type);
            if (drop != null) return (drop, Color.white, true);
            Sprite icon = type switch
            {
                IngredientType.Tea => SpriteFactory.TeaIcon,
                IngredientType.Coffee => SpriteFactory.CoffeeBeanIcon,
                _ => SpriteFactory.CocktailIngredientIcon,
            };
            return icon != null ? (icon, Color.white, true) : (SpriteFactory.Circle, ColorOf(type), false);
        }

        public static void Spawn(GameManager game, PlayerController player, Vector2 position,
            IngredientType type, int amount, Transform parent, bool forceAttraction = false)
        {
            var go = new GameObject("Ingredient");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var (sprite, color, isRealIcon) = VisualFor(type);
            // 찻집의 우림 지식 아이콘(ui_codex)은 새 원두 드롭 아트(drop_coffee)가 없을 때만 대체로 쓴다(57: 새 아트를 찻집에도 사용).
            if(type==IngredientType.Coffee && game!=null && game.Job==Job.TeaSommelier && DropArt(IngredientType.Coffee)==null)
            {
                var study=ArtLibrary.Sprite("ui_codex");
                if(study!=null) { sprite=study; color=Color.white; isRealIcon=true; }
            }
            // 실제 아이콘은 캔버스 여백(그림이 절반 정도만 차 있음) 때문에 작아 보여서 시각적으로 더 키운다.
            float visualScale = isRealIcon ? Balance.IngredientIconVisualScale : 1f;
            go.transform.localScale = Vector3.one * (0.22f + 0.04f * amount) * visualScale * Balance.PickupReadabilityScale;

            // 57 P0-2: 드롭이 장판·바닥 무늬에 묻혔다 — 액터 정렬층으로 올리고(장판 위), z를 카메라 쪽으로 당겨 같은 층 몸체보다 앞.
            // 뒤에 검은 외곽(같은 그림 실루엣)만 깐다(색 원 후광은 사용자 반려 — 쓰지 않음). 판정·끌림 거리는 그대로(시각만).
            var position3 = go.transform.position;
            go.transform.position = new Vector3(position3.x, position3.y, PickupZ);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = SortOrders.Actor;

            var pickup = go.AddComponent<IngredientPickup>();
            pickup.outline = Layer(go.transform, "Outline", sprite, new Color(0.02f, 0.01f, 0.03f, 0.95f), 1.22f, 0.015f);
            pickup.game = game;
            pickup.player = player;
            pickup.type = type;
            pickup.amount = amount;
            pickup.pulling = forceAttraction;
        }

        /// <summary>몸체(z=0)보다 카메라 쪽 — 같은 정렬층에서 앞에 그린다(직교 카메라 z 정렬).</summary>
        public const float PickupZ = -0.05f;
        SpriteRenderer outline;
        public SpriteRenderer Outline => outline;

        static SpriteRenderer Layer(Transform parent, string name, Sprite sprite, Color color, float scale, float behindZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, behindZ / Mathf.Max(0.0001f, parent.localScale.z));
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = SortOrders.Actor;
            return sr;
        }

        void Update()
        {
            if (player == null || player.IsDead) return;

            Vector2 to = player.Position - (Vector2)transform.position;
            float dist = to.magnitude;

            if (!pulling && dist <= player.PickupRadius) pulling = true;
            if (!pulling) return;

            if (dist <= CollectDistance)
            {
                AudioService.PlaySfx("pickup", transform.position);
                game.AddIngredient(type, amount);
                player.HiddenRuntime?.OnPickup(); // 41 원두 상자
                ArtVfx.Spawn("pickup_glint", transform.position, 0f, 0.6f, 0.33f, Color.white); // 09 pickup_glint
                Destroy(gameObject);
                return;
            }

            pullSpeed += 30f * Time.deltaTime;
            transform.position += (Vector3)(to / dist * Mathf.Min(pullSpeed * Time.deltaTime, dist));
        }
    }
}
