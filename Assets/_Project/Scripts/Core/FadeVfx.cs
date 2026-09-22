using UnityEngine;

namespace CCT
{
    /// <summary>짧게 커지거나 줄어들며 사라지는 단발성 스프라이트 이펙트 (찌르기, 화염 폭발 등).</summary>
    public class FadeVfx : MonoBehaviour
    {
        SpriteRenderer sr;
        Vector3 startScale;
        Vector3 endScale;
        Color color;
        float life;
        float elapsed;

        public static FadeVfx Spawn(Sprite sprite, Vector2 position, float angleDeg, Vector2 startScale,
            Vector2 endScale, Color color, float life, int sortingOrder = 4)
        {
            var go = new GameObject("Vfx");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);

            var vfx = go.AddComponent<FadeVfx>();
            vfx.sr = go.AddComponent<SpriteRenderer>();
            vfx.sr.sprite = sprite;
            vfx.sr.sortingOrder = sortingOrder;
            vfx.startScale = startScale;
            vfx.endScale = endScale;
            vfx.color = color;
            vfx.life = Mathf.Max(0.01f, life);
            vfx.Apply(0f);
            return vfx;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float k = elapsed / life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(k);
        }

        void Apply(float k)
        {
            transform.localScale = Vector3.Lerp(startScale, endScale, k);
            var c = color;
            c.a *= 1f - k;
            sr.color = c;
        }
    }
}
