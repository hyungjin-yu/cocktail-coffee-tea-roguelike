using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>Direct-hit effects attached to one enemy. Secondary damage never attaches new effects.</summary>
    public sealed class EnemyHitStatus : MonoBehaviour
    {
        sealed class Dot
        {
            public float dps;
            public double remaining, tick = 1d;
            public bool canSpread;
            public SpriteRenderer marker;
            public SpriteAnimator anim;
        }

        // 42(2026-10-12): 화상=불꽃·잔불(왼쪽 위), 중독=독방울·기포(오른쪽 위) PixelLab 루프 클립. 없으면 기존 색 원.
        public const string BurnArt = "ArtExtra/status_burn";
        public const string PoisonArt = "ArtExtra/status_poison";
        public const float MarkerWorldSize = 0.62f; // 64px 클립 한 변의 월드 크기(내용은 가운데 절반 정도)
        static readonly System.Collections.Generic.Dictionary<string, ArtClip> clips = new System.Collections.Generic.Dictionary<string, ArtClip>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetClips() => clips.Clear();

        public static ArtClip MarkerClip(bool isFlame)
        {
            string path = isFlame ? BurnArt : PoisonArt;
            if (clips.TryGetValue(path, out var cached) && cached != null && cached.frames.Length > 0 && cached.frames[0] != null) return cached;
            var all = Resources.LoadAll<Sprite>(path);
            ArtClip clip = null;
            if (all != null && all.Length > 0)
            {
                System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
                clip = new ArtClip { name = isFlame ? "status_burn" : "status_poison", fps = 12f, loop = true, frames = all };
            }
            clips[path] = clip;
            return clip;
        }

        public bool FlameMarkerVisible => flame.marker != null && flame.marker.enabled;
        public bool PoisonMarkerVisible => poison.marker != null && poison.marker.enabled;
        public string FlameMarkerSprite => flame.marker != null && flame.marker.sprite != null ? flame.marker.sprite.name : "";
        public string PoisonMarkerSprite => poison.marker != null && poison.marker.sprite != null ? poison.marker.sprite.name : "";
        Enemy enemy;
        PlayerController owner;
        readonly Dot flame = new Dot(), poison = new Dot();
        bool tickingPoison;
        public float FlameDps => flame.dps;
        public float PoisonDps => poison.dps;
        public float FlameRemaining => (float)flame.remaining;
        public float PoisonRemaining => (float)poison.remaining;

        public static void AttachDirect(Enemy enemy, PlayerController owner)
        {
            if (enemy == null || enemy.IsDead || enemy.Invulnerable || owner == null || owner.IsDead || enemy.RunPlayer != owner) return;
            var stats = owner.Stats;
            if ((stats.flameLevel <= 0 || stats.FlameLocked) && (stats.poisonLevel <= 0 || stats.PoisonLocked)) return;
            var status = enemy.GetComponent<EnemyHitStatus>() ?? enemy.gameObject.AddComponent<EnemyHitStatus>();
            // A status belongs to its original run, not a subsequently created GameManager singleton.
            if (status.owner != null && status.owner != owner) return;
            status.enemy = enemy;
            status.owner = owner;
            float steep = owner.Hidden.Has(HiddenSkill.LongSteep) ? 1f : 0f; // 41 오래 우린 향 +1초
            if (stats.flameLevel > 0 && !stats.FlameLocked)
                status.Refresh(status.flame, (6f + 4f * stats.flameLevel) * (1f + stats.DamageBonus) * .25f *
                    (owner.FinalForms.Has(AttackFinalForm.Flame) ? 1.5f : 1f),
                    (owner.FinalForms.Has(AttackFinalForm.Flame) ? 4f : 3f) + steep, false, true);
            if (stats.poisonLevel > 0 && !stats.PoisonLocked)
                status.Refresh(status.poison, (4f + 3f * stats.poisonLevel) * (1f + stats.DamageBonus) * .25f,
                    3f + steep, owner.FinalForms.Has(AttackFinalForm.Poison), false);
        }

        void Refresh(Dot dot, float dps, float duration, bool spread, bool isFlame)
        {
            bool expired = dot.remaining <= 0f;
            if (expired || dps > dot.dps)
            {
                dot.dps = dps;
                dot.canSpread = spread;
            }
            else if (dps == dot.dps && spread) dot.canSpread = true;
            dot.remaining = System.Math.Max(dot.remaining, duration);
            if (expired) dot.tick = 1f;
            if (dot.marker == null)
            {
                var marker = new GameObject(isFlame ? "FlameHitMarker" : "PoisonHitMarker");
                marker.transform.SetParent(transform, false);
                dot.marker = marker.AddComponent<SpriteRenderer>();
                var clip = MarkerClip(isFlame);
                float r = enemy.Def.radius;
                if (clip != null)
                {
                    // 몸 윗부분 바깥쪽(왼쪽 화상/오른쪽 중독)에 작게 — 윤곽 중심·피격 섬광·예고를 가리지 않게 정렬은 섬광 아래.
                    marker.transform.localPosition = new Vector3((isFlame ? -1f : 1f) * r * .75f, r * .95f, 0f);
                    float s = MarkerWorldSize * Mathf.Clamp(r / .5f, .8f, 1.6f) / (clip.frames[0].rect.width / clip.frames[0].pixelsPerUnit);
                    marker.transform.localScale = new Vector3(s, s, 1f);
                    dot.marker.sprite = clip.frames[0];
                    dot.marker.color = Color.white;
                    dot.anim = marker.AddComponent<SpriteAnimator>();
                    dot.anim.Bind(dot.marker);
                    dot.anim.Play(clip, true);
                }
                else
                {
                    marker.transform.localPosition = new Vector3(isFlame ? -.16f : .16f, r * .65f, 0f);
                    marker.transform.localScale = new Vector3(.16f, .22f, 1f);
                    dot.marker.sprite = SpriteFactory.Circle;
                    dot.marker.color = isFlame ? new Color(1f, .55f, .16f) : new Color(.46f, .85f, .35f);
                }
                dot.marker.sortingOrder = SortOrders.AirVfx - 1;
            }
            else if (expired && dot.anim != null) dot.anim.Play(dot.anim.Current, true); // 새로 걸림: 처음부터, 갱신: 끊김 없이 이어서
            dot.marker.enabled = true;
        }

        void Update()
        {
            if (owner == null || owner.IsDead || enemy == null || enemy.IsDead) return;
            // 41 불꽃 쇼: 화상 중인 일반 적 기본 이동 -15%(보스 제외, 상태가 끝나면 원래대로).
            enemy.StatusSpeedMultiplier = flame.remaining > 0f && !enemy.Def.IsBoss && owner.Hidden.Has(HiddenSkill.FireShow) ? .85f : 1f;
            Tick(flame, owner.Stats.FlameLocked, Time.deltaTime);
            if (!enemy.IsDead) Tick(poison, owner.Stats.PoisonLocked, Time.deltaTime);
        }

        void Tick(Dot dot, bool locked, float dt)
        {
            // 봉인 중: 표식은 남되 동작 정지 + 반투명(남은 시간 보존과 같은 의미). Pause는 게임 시간 정지로 함께 멈춘다.
            if (dot.marker != null && dot.marker.enabled)
            {
                if (dot.anim != null) dot.anim.enabled = !locked;
                if (dot.anim != null) dot.marker.color = locked ? new Color(1f, 1f, 1f, .45f) : Color.white;
            }
            if (locked || dot.remaining <= 0f || dt <= 0f) return;
            double elapsed = System.Math.Min(dt, dot.remaining);
            dot.remaining = System.Math.Max(0d, dot.remaining - elapsed);
            dot.tick -= elapsed;
            while (dot.tick <= .00000001d && !enemy.IsDead)
            {
                dot.tick += 1f;
                tickingPoison = dot == poison;
                // 41 겹치는 여운: 화상·중독이 함께 걸린 동안 지속 피해 +25%(이 틱 피해만, 다른 효과 미발동).
                bool both = flame.remaining > 0f && poison.remaining > 0f && owner.Hidden.Has(HiddenSkill.BitterBlend);
                enemy.TakeDamage(dot.dps * (both ? 1.25f : 1f));
                tickingPoison = false;
            }
            if (dot.remaining <= 0f && dot.marker != null) dot.marker.enabled = false;
        }

        public void OnEnemyDeath()
        {
            if (owner == null || owner.IsDead || owner.Stats.PoisonLocked || !poison.canSpread ||
                (poison.remaining <= 0f && !tickingPoison) || poison.dps <= 0f) return;
            poison.canSpread = false; // Before any recipient can die: exactly one nonrecursive propagation.
            var candidates = new List<Enemy>();
            foreach (var target in Enemy.Active)
                if (target != null && target != enemy && !target.IsDead && !target.Invulnerable &&
                    target.RunPlayer == owner && ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude <= 4f)
                    candidates.Add(target);
            candidates.Sort((a, b) => ((Vector2)a.transform.position - (Vector2)transform.position).sqrMagnitude.CompareTo(
                ((Vector2)b.transform.position - (Vector2)transform.position).sqrMagnitude));
            for (int i = 0; i < Mathf.Min(3, candidates.Count); i++)
            {
                var target = candidates[i];
                var status = target.GetComponent<EnemyHitStatus>() ?? target.gameObject.AddComponent<EnemyHitStatus>();
                if (status.owner != null && status.owner != owner) continue;
                status.owner = owner;
                status.enemy = target;
                status.Refresh(status.poison, poison.dps * .5f, 3f, false, false);
            }
        }
    }
}
