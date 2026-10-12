using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 08:80 피해 숫자 — 같은 적의 연속 적중은 0.12초 안이면 한 숫자로 합산, 동시에 최대 30개, 0.6초 동안 떠오르며 사라진다.
    /// 설정 damageNumbers=false면 만들지 않는다(숫자만 숨김, 판정·피격 효과는 그대로). 판정과 무관한 월드 텍스트.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        public const int MaxConcurrent = 30;
        public const float Lifetime = 0.6f;
        const float MergeWindow = 0.12f;

        static readonly List<DamageNumbers> active = new List<DamageNumbers>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>테스트 전용 — EditMode에서도 숫자를 만든다(기본은 플레이 중에만).</summary>
        public static bool EnableInEditModeForTests;

        public static int ActiveCount
        {
            get
            {
                active.RemoveAll(n => n == null);
                return active.Count;
            }
        }

        Enemy owner;
        TextMesh text;
        float amount;
        float age;
        Vector3 start;

        public float Amount => amount;
        public string Text => text != null ? text.text : "";

        public static DamageNumbers Show(Enemy enemy, float damage)
        {
            if (enemy == null || damage <= 0f || !SettingsService.Current.damageNumbers) return null;
            if (!Application.isPlaying && !EnableInEditModeForTests) return null; // EditMode 테스트의 피해 호출이 오브젝트를 남기지 않게
            active.RemoveAll(n => n == null);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var n = active[i];
                if (n.owner == enemy && n.age < MergeWindow)
                {
                    n.amount += damage;
                    n.Refresh();
                    return n;
                }
            }
            while (active.Count >= MaxConcurrent)
            {
                var oldest = active[0];
                active.RemoveAt(0);
                if (oldest != null) DestroyAny(oldest.gameObject);
            }

            var go = new GameObject("DamageNumber");
            var num = go.AddComponent<DamageNumbers>();
            num.owner = enemy;
            num.amount = damage;
            num.start = enemy.transform.position + new Vector3(Random.Range(-0.2f, 0.2f), enemy.Def.radius + 0.4f, 0f);
            go.transform.position = num.start;
            num.text = go.AddComponent<TextMesh>();
            var font = UiFactory.Font;
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null)
            {
                num.text.font = font;
                renderer.sharedMaterial = font.material;
            }
            num.text.fontSize = 48;
            num.text.characterSize = 0.06f;
            num.text.anchor = TextAnchor.MiddleCenter;
            num.text.alignment = TextAlignment.Center;
            num.text.color = new Color(1f, 0.92f, 0.6f);
            renderer.sortingOrder = SortOrders.AirVfx + 1;
            num.Refresh();
            active.Add(num);
            return num;
        }

        static void DestroyAny(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        void Refresh() => text.text = Mathf.RoundToInt(amount).ToString();

        void Update()
        {
            age += Time.deltaTime;
            float k = age / Lifetime;
            if (k >= 1f)
            {
                active.Remove(this);
                Destroy(gameObject);
                return;
            }
            transform.position = start + new Vector3(0f, 0.6f * k, 0f);
            var c = text.color;
            c.a = 1f - k * k;
            text.color = c;
        }

        void OnDestroy() => active.Remove(this);
    }
}
