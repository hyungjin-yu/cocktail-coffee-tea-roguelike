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
        static (Sprite sprite, Color color, bool isRealIcon) VisualFor(IngredientType type)
        {
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
            if(type==IngredientType.Coffee && game!=null && game.Job==Job.TeaSommelier)
            {
                var study=ArtLibrary.Sprite("ui_codex");
                if(study!=null) { sprite=study; color=Color.white; isRealIcon=true; }
            }
            // 실제 아이콘은 캔버스 여백(그림이 절반 정도만 차 있음) 때문에 작아 보여서 시각적으로 더 키운다.
            float visualScale = isRealIcon ? Balance.IngredientIconVisualScale : 1f;
            go.transform.localScale = Vector3.one * (0.22f + 0.04f * amount) * visualScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 1;

            var pickup = go.AddComponent<IngredientPickup>();
            pickup.game = game;
            pickup.player = player;
            pickup.type = type;
            pickup.amount = amount;
            pickup.pulling = forceAttraction;
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
