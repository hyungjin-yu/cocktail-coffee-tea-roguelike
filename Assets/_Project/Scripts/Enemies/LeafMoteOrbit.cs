using UnityEngine;

namespace CCT
{
    /// <summary>04 §8 leaf_mote — 리프 워든 주위를 떠도는 찻잎 장식(판정 없음). 아트가 없으면 붙지 않는다.</summary>
    public class LeafMoteOrbit : MonoBehaviour
    {
        const int Count = 3;
        Transform[] motes;
        float time;
        float radius;

        public static LeafMoteOrbit Attach(Enemy enemy)
        {
            var art = ArtLibrary.Get("leaf_mote");
            if (art?.FirstSprite == null) return null;
            var orbit = enemy.gameObject.AddComponent<LeafMoteOrbit>();
            orbit.radius = enemy.Def.radius * 1.6f;
            orbit.motes = new Transform[Count];
            float scale = 0.35f / Mathf.Max(0.01f, art.ContentWorldSize.x);
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("LeafMote");
                go.transform.SetParent(enemy.transform, false);
                go.transform.localScale = Vector3.one * scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art.FirstSprite;
                sr.sortingOrder = SortOrders.AirVfx;
                orbit.motes[i] = go.transform;
            }
            orbit.Place();
            return orbit;
        }

        void Update()
        {
            time += Time.deltaTime;
            Place();
        }

        void Place()
        {
            for (int i = 0; i < motes.Length; i++)
            {
                if (motes[i] == null) continue;
                float a = time * 1.4f + i * Mathf.PI * 2f / motes.Length;
                motes[i].localPosition = new Vector3(Mathf.Cos(a) * radius, 0.3f + Mathf.Sin(a * 2f) * 0.15f + Mathf.Sin(a) * radius * 0.35f, 0f);
                motes[i].localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
        }
    }
}
