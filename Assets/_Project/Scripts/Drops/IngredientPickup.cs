using UnityEngine;

namespace CCT
{
    /// <summary>몹이 떨어뜨리는 재료. 플레이어가 가까이 오면 끌려가서 게이지에 들어간다.</summary>
    public class IngredientPickup : MonoBehaviour
    {
        const float CollectDistance = 0.35f;

        GameManager game;
        PlayerController player;
        IngredientType type;
        int amount;
        float pullSpeed = 6f;
        bool pulling;

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
            IngredientType type, int amount, Transform parent)
        {
            var go = new GameObject("Ingredient");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var (sprite, color, isRealIcon) = VisualFor(type);
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
                game.AddIngredient(type, amount);
                Destroy(gameObject);
                return;
            }

            pullSpeed += 30f * Time.deltaTime;
            transform.position += (Vector3)(to / dist * Mathf.Min(pullSpeed * Time.deltaTime, dist));
        }
    }
}
