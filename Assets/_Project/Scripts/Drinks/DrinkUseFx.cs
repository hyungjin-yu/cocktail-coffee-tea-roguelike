using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-09 음료 사용/발동 순간의 짧은 월드 표시: 준비 음료의 기존 잔 스프라이트(DrinkArt)가 플레이어 머리 위로 0.55초 떠올랐다 사라지고,
    /// 발밑에 음료 색 고리가 퍼진다. 새 아트 없음 · 판정/입력 무관(자동 공격·이동을 막지 않음) · 게임 시간(일시정지 시 정지).
    /// 같은 순간 여러 번 불려도 MinInterval 안에서는 하나만 만든다.
    /// </summary>
    public static class DrinkUseFx
    {
        public const float Life = 0.55f;
        public const float MinInterval = 0.4f;
        static float readyAt = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => readyAt = -999f;

        /// <summary>테스트/검수용: 마지막으로 만든 잔 표시.</summary>
        public static FadeVfx LastCup { get; private set; }

        public static void ResetForTests() { readyAt = -999f; LastCup = null; }

        /// <summary>recipe의 잔을 머리 위로 띄운다. accent가 없으면 음료 색. sfx는 기존 효과음 ID(null이면 무음).</summary>
        public static void Play(PlayerController player, DrinkRecipe recipe, Color? accent = null, string sfx = "pickup", float scale = 1f)
        {
            if (player == null || recipe == null) return;
            if (Time.time < readyAt) return;
            readyAt = Time.time + MinInterval;
            var look = DrinkCalculator.Look(recipe);
            Color liquid = accent ?? new Color(look.color.r, look.color.g, look.color.b, 1f);
            Vector2 head = player.Position + Vector2.up * 1.15f;
            var parent = player.transform;

            var sprite = DrinkArt.SpriteOrPlaceholder(DrinkStageView.ServeFor(recipe), out _);
            if (sprite != null)
            {
                float h = Mathf.Max(0.01f, sprite.bounds.size.y);
                float s = 0.6f * scale / h; // 잔 높이 ≈ 0.6 월드
                var cup = FadeVfx.Spawn(sprite, head, 0f, Vector2.one * s * 0.75f, Vector2.one * s, Color.white, Life, SortOrders.AirVfx + 1);
                cup.transform.SetParent(parent, true); // 플레이어를 따라간다(0.55초 뒤 사라짐)
                cup.gameObject.AddComponent<RiseFx>().speed = 0.9f;
                LastCup = cup;
            }
            var ring = FadeVfx.Spawn(SpriteFactory.Circle, player.Position + Vector2.down * PlayerController.Radius, 0f, Vector2.one * 0.4f, new Vector2(1.6f, 0.6f) * scale,
                new Color(liquid.r, liquid.g, liquid.b, 0.45f), 0.35f, SortOrders.Shadow); // 발밑 높이 — 캐릭터 몸을 덮지 않는다(2026-11-09 렌더 검수)
            ring.transform.SetParent(player.transform.parent, true);
            if (!string.IsNullOrEmpty(sfx)) AudioService.PlaySfx(sfx, player.Position);
        }

        /// <summary>위로 천천히 떠오르게만 하는 보조 컴포넌트(FadeVfx는 위치를 바꾸지 않음).</summary>
        sealed class RiseFx : MonoBehaviour
        {
            public float speed;
            void Update() => transform.localPosition += Vector3.up * speed * Time.deltaTime;
        }
    }
}
