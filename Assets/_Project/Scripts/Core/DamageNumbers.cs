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
        /// <summary>2026-11-10 보조 문구 "상성!"은 도배되지 않게 전역 간격(게임 시간)을 둔다. 숫자 자체의 "(추가 A)"는 매번 표시.</summary>
        public const float AffinityLabelInterval = 1.5f;
        static float nextAffinityLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active.Clear(); nextAffinityLabel = 0f; }

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
        HitKind kind;
        float baseCharSize;
        float amount;
        int bonus;
        bool affinityLabel;
        float age;
        Vector3 start;

        public float Amount => amount;
        public HitKind Kind => kind;
        /// <summary>합계에 포함된 상성 추가 피해(정수, 없으면 0).</summary>
        public int Bonus => bonus;
        public string Text => text != null ? text.text : "";

        public static DamageNumbers Show(Enemy enemy, float damage) => Show(enemy, damage, HitKind.Normal);

        /// <summary>2026-11-09: 종류별 모양. 치명타는 크고 금색에 "!"(같은 적의 일반 숫자와 합치지 않음), 강타는 주황,
        /// 지속 피해는 작고 옅은 색(직접 타격 숫자와 따로 합산). 합산 규칙(0.12초)·동시 30개 상한은 그대로.</summary>
        public static DamageNumbers Show(Enemy enemy, float damage, HitKind kind) => Show(enemy, damage, kind, 0);

        /// <summary>bonus = damage(합계)에 이미 포함된 상성 추가 피해. "120 (추가 20)"처럼 합계에 병기하고 별도 숫자를 만들지 않는다.</summary>
        public static DamageNumbers Show(Enemy enemy, float damage, HitKind kind, int bonus)
        {
            if (enemy == null || damage <= 0f || !SettingsService.Current.damageNumbers) return null;
            if (!Application.isPlaying && !EnableInEditModeForTests) return null; // EditMode 테스트의 피해 호출이 오브젝트를 남기지 않게
            active.RemoveAll(n => n == null);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var n = active[i];
                if (n.owner == enemy && n.age < MergeWindow && MergeClass(n.kind) == MergeClass(kind))
                {
                    n.amount += damage;
                    n.bonus = (int)System.Math.Min(int.MaxValue, (long)n.bonus + System.Math.Max(0, bonus));
                    if (bonus > 0) n.TryAffinityLabel();
                    if (kind == HitKind.Critical || (kind == HitKind.Heavy && n.kind != HitKind.Critical)) n.SetKind(kind);
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
            num.bonus = System.Math.Max(0, bonus);
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
            num.text.anchor = TextAnchor.MiddleCenter;
            num.text.alignment = TextAlignment.Center;
            num.SetKind(kind);
            if (num.bonus > 0) num.TryAffinityLabel();
            if (kind == HitKind.Dot) num.start += new Vector3(0.25f, -0.2f, 0f); // 직접 타격 숫자와 겹치지 않게 옆·아래
            go.transform.position = num.start;
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

        // 피해는 TakeDamage 경계에서 이미 정수(중간값은 0에서 먼 쪽)
        void Refresh() => text.text = Format(amount, bonus, kind == HitKind.Critical, affinityLabel);

        /// <summary>표시 문자열(테스트 공용). 예: 120 (추가 20), 치명 "!" 유지, 보조 문구는 다음 줄.</summary>
        public static string Format(float total, int bonus, bool critical, bool affinityLabel)
        {
            string s = IntNum.Round(total) + (critical ? "!" : "");
            if (bonus > 0) s += $" (추가 {bonus})";
            if (affinityLabel && bonus > 0) s += "\n상성!";
            return s;
        }

        void TryAffinityLabel()
        {
            if (affinityLabel || Time.time < nextAffinityLabel) return;
            affinityLabel = true;
            nextAffinityLabel = Time.time + AffinityLabelInterval;
        }

        /// <summary>치명·강타·일반은 한 숫자로 합칠 수 있고(가장 강한 모양으로), 지속 피해는 따로 센다.</summary>
        static int MergeClass(HitKind k) => k == HitKind.Dot ? 1 : 0;

        void SetKind(HitKind k)
        {
            kind = k;
            switch (k)
            {
                case HitKind.Critical: baseCharSize = 0.085f; text.color = new Color(1f, 0.74f, 0.22f); break;
                case HitKind.Heavy: baseCharSize = 0.075f; text.color = new Color(1f, 0.62f, 0.38f); break;
                case HitKind.Dot: baseCharSize = 0.045f; text.color = new Color(0.86f, 0.8f, 0.7f); break;
                default: baseCharSize = 0.06f; text.color = new Color(1f, 0.92f, 0.6f); break;
            }
            text.characterSize = baseCharSize;
        }

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
            // 치명·강타 숫자는 처음 0.1초 동안 크게 튀었다가 제 크기로(위치 판정 없음).
            if (kind == HitKind.Critical || kind == HitKind.Heavy)
                text.characterSize = baseCharSize * (age < 0.1f ? Mathf.Lerp(1.45f, 1f, age / 0.1f) : 1f);
            var c = text.color;
            c.a = 1f - k * k;
            text.color = c;
        }

        void OnDestroy() => active.Remove(this);
    }
}
