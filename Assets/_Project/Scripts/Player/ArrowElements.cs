using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>2026-11-24 #21 속성 화살 종류. 아트 폴더 이름(burn/freeze/shock/poison)과 같은 순서·이름.</summary>
    public enum ArrowElementKind { None, Burn, Freeze, Shock, Poison }

    /// <summary>
    /// 2026-11-24 #21 기본 활 전용 속성 화살 규칙(카드 문구가 이 상수에서 만들어진다 — 설명과 실제 규칙이 어긋나지 않게).
    /// 적용 범위: 기본 활(시작 무기)의 **원래 화살**만. 총·보조 활(티니 보우)·근접·파편(관통 최종 형태)·옆 화살(확산 최종 형태)에는 붙지 않는다.
    /// 여러 속성을 가지면 화살 한 발마다 화상→빙결→감전→중독 고정 순서로 번갈아 나간다(없는 속성은 건너뜀, PlayerStats.ConsumeArrowElement).
    /// 한 화살에 전부 실리지 않으므로 다중 발사·관통과 곱해져 폭주하지 않는다.
    /// 같은 발사 묶음(다중 발사 한 번)의 같은 속성은 같은 적에게 한 번만 발동한다. 서로 다른 속성은 각자 상한 안에서 따로.
    /// </summary>
    public static class ArrowElementRules
    {
        /// <summary>카드 최대 중첩 = 최대 레벨.</summary>
        public const int MaxLevel = 3;

        // 화상: 짧고 빠른 지속 피해. 다시 맞히면 시간만 갱신(더 센 쪽 유지), 겹치지 않는다.
        public const float BurnSeconds = 2f;
        public const int BurnTicksPerSecond = 4;
        public static float BurnDpsFraction(int level) => level <= 0 ? 0f : 0.2f + 0.1f * level; // 공격력의 30/40/50% (초당)

        // 빙결: 일반 적은 기존 경직(이동·몸통 공격 정지)으로 잠시 멈춤 + 같은 적 재빙결 대기. 보스는 기존 경직 면역 그대로 → 이 화살 피해 증가로 대체.
        public const float FreezeSeconds = 1f;
        public static float FreezeCooldown(int level) => 5f - Mathf.Clamp(level, 1, MaxLevel); // 얼기 시작부터 4/3/2초
        public static float FreezeBossBonus(int level) => 0.1f * Mathf.Clamp(level, 0, MaxLevel); // +10/20/30%

        // 감전: 처음 맞힌 적에서 가까운 다른 적으로 제한된 연쇄. 연쇄 피해는 보조 피해(상태·히든·상성 없음, 다시 튀지 않음).
        public const float ShockRange = 2.5f;
        public const float ShockDamageFraction = 0.4f;
        /// <summary>같은 적이 짧은 시간에 여러 화살의 연쇄를 연달아 받지 않게(다중 발사 폭주 방지). 내부 값.</summary>
        public const float ShockRecipientLockout = 0.25f;
        public const float ShockMarkSeconds = 0.4f;
        public static int ShockJumps(int level) => Mathf.Clamp(level, 0, MaxLevel); // 1/2/3마리

        // 중독: 더 오래, 맞힐수록 쌓이는 지속 피해. 상한 5중첩.
        public const float PoisonSeconds = 6f;
        public const int PoisonMaxStacks = 5;
        public static float PoisonStackDpsFraction(int level) => level <= 0 ? 0f : 0.03f + 0.02f * level; // 중첩당 공격력의 5/7/9% (초당)

        public static int Level(PlayerStats s, ArrowElementKind kind)
        {
            if (s == null) return 0;
            switch (kind)
            {
                case ArrowElementKind.Burn: return s.arrowBurnLevel;
                case ArrowElementKind.Freeze: return s.arrowFreezeLevel;
                case ArrowElementKind.Shock: return s.arrowShockLevel;
                case ArrowElementKind.Poison: return s.arrowPoisonLevel;
                default: return 0;
            }
        }

        public static int OwnedCount(PlayerStats s)
        {
            int n = 0;
            if (s != null) foreach (var k in PlayerStats.ArrowCycle) if (Level(s, k) > 0) n++;
            return n;
        }

        public static string Name(ArrowElementKind k) => k switch
        {
            ArrowElementKind.Burn => "화상", ArrowElementKind.Freeze => "빙결", ArrowElementKind.Shock => "감전", ArrowElementKind.Poison => "중독", _ => "",
        };

        /// <summary>아트 폴더 접두(Resources/ArtExtra/arrow_element/&lt;id&gt;_&lt;clip&gt;).</summary>
        public static string ArtId(ArrowElementKind k) => k switch
        {
            ArrowElementKind.Burn => "burn", ArrowElementKind.Freeze => "freeze", ArrowElementKind.Shock => "shock", ArrowElementKind.Poison => "poison", _ => null,
        };

        /// <summary>레벨업 카드 효과 칸(2줄)용 짧은 글.</summary>
        public static string ShortEffect(ArrowElementKind k) => k switch
        {
            ArrowElementKind.Burn => $"{IntNum.Duration(BurnSeconds)} 화상 · 초당 {BurnTicksPerSecond}번 피해",
            ArrowElementKind.Freeze => $"일반 적 {IntNum.Duration(FreezeSeconds)} 얼림 · 보스는 피해 증가",
            ArrowElementKind.Shock => "근처 적 Lv마리에게 전기 연쇄",
            ArrowElementKind.Poison => $"{IntNum.Duration(PoisonSeconds)} 중독 · 최대 {PoisonMaxStacks}중첩",
            _ => "",
        };

        public const string RotationNote = "여러 속성을 고르면 화상→빙결→감전→중독 순서로 한 발씩 번갈아 나가며(없는 속성은 건너뜀) 다음 화살은 머리 위에 보입니다. 보조 활과 총에는 붙지 않습니다.";

        /// <summary>도감·상세용 쉬운 문장(발동 조건 → 행동 → 수치). 수치는 위 상수에서 만든다.</summary>
        public static string Description(ArrowElementKind k)
        {
            switch (k)
            {
                case ArrowElementKind.Burn:
                    return $"활 화살이 맞힌 적을 {IntNum.Duration(BurnSeconds)} 동안 태웁니다. 초당 {BurnTicksPerSecond}번 나눠 피해를 주며 1초 합계는 공격력의 {IntNum.Percent(BurnDpsFraction(1))}%(Lv마다 +{IntNum.Percent(0.1f)}%p)입니다. 다시 맞히면 {IntNum.Duration(BurnSeconds)}로 갱신되고 겹치지 않습니다.\n" + RotationNote;
                case ArrowElementKind.Freeze:
                    return $"활 화살이 맞힌 일반 적은 {IntNum.Duration(FreezeSeconds)} 동안 얼어 걷지 못하고 몸으로 부딪혀도 피해가 없습니다. 같은 적은 얼기 시작하고 {IntNum.Duration(FreezeCooldown(1))}가 지나야 다시 얼며 Lv마다 1초 줄어듭니다. 보스는 멈추지 않는 대신 이 화살 피해 +{IntNum.Percent(FreezeBossBonus(1))}%(Lv마다 +{IntNum.Percent(0.1f)}%p).\n" + RotationNote;
                case ArrowElementKind.Shock:
                    return $"활 화살이 처음 맞힌 적에서 {IntNum.Centimeters(ShockRange)}cm 안의 가장 가까운 다른 적에게 전기가 이어집니다. Lv마다 1마리씩 최대 {MaxLevel}마리, 각각 화살 피해의 {IntNum.Percent(ShockDamageFraction)}%. 관통한 뒤에는 다시 튀지 않습니다.\n" + RotationNote;
                case ArrowElementKind.Poison:
                    return $"활 화살이 맞히면 {IntNum.Duration(PoisonSeconds)} 동안 중독됩니다. 맞힐 때마다 1중첩씩 최대 {PoisonMaxStacks}중첩까지 쌓이고 시간이 다시 {IntNum.Duration(PoisonSeconds)}가 됩니다. 1초마다 중첩당 공격력의 {IntNum.Percent(PoisonStackDpsFraction(1))}%(Lv마다 +{IntNum.Percent(0.02f)}%p) 피해.\n" + RotationNote;
                default: return "";
            }
        }

        /// <summary>직접 적중 피해 보정(빙결 화살이 보스를 맞힐 때만). 다른 경우 그대로.</summary>
        public static float ModifyDirect(ArrowElementKind k, Enemy enemy, PlayerController owner, float damage)
        {
            if (k != ArrowElementKind.Freeze || enemy == null || enemy.Def == null || !enemy.Def.IsBoss || owner == null) return damage;
            return damage * (1f + FreezeBossBonus(Level(owner.Stats, k)));
        }

        public static bool LowEffects => SettingsService.Current != null && SettingsService.Current.effectsLevel == SettingsData.EffectsLow;
    }

    /// <summary>
    /// 2026-11-24 #21 속성 화살 아트(Resources/ArtExtra/arrow_element, ArtSource/elemental-arrows-2026-11-24/manifest.json).
    /// 클립 = &lt;id&gt;_arrow(3프레임 루프 85/95/105ms)·_arrow_low(정지)·_hit(4프레임 원샷 240ms)·_active(4프레임 루프 650ms)·_mark(정지)·
    /// shock_chain(4프레임 원샷 210ms, 시작 피벗 (8,24), 앵커 간 112px)·shock_chain_low(70ms). 없으면 빈 배열 → 코드 대체 표시.
    /// </summary>
    public static class ArrowElementArt
    {
        public const string Folder = "ArtExtra/arrow_element/";
        public static readonly float[] ArrowMs = { 85f, 95f, 105f };
        public static readonly float[] HitMs = { 40f, 55f, 70f, 75f };
        public static readonly float[] ActiveMs = { 150f, 175f, 150f, 175f };
        public static readonly float[] ChainMs = { 45f, 45f, 55f, 65f };
        public const float ChainLowMs = 70f;
        /// <summary>저이펙트 명중 = 정지 표식을 짧게.</summary>
        public const float LowHitMarkSeconds = 0.15f;
        /// <summary>감전 연쇄 그림의 두 앵커(8→120px) 사이 월드 길이(128 PPU).</summary>
        public const float ChainSpanWorld = 112f / 128f;

        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cache.Clear();

        public static Sprite[] Frames(ArrowElementKind k, string clip)
        {
            var id = ArrowElementRules.ArtId(k);
            if (id == null) return new Sprite[0];
            string path = Folder + id + "_" + clip;
            if (cache.TryGetValue(path, out var f) && (f.Length == 0 || f[0] != null)) return f;
            var all = Resources.LoadAll<Sprite>(path);
            if (all == null) all = new Sprite[0];
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            cache[path] = all;
            return all;
        }

        public static Color FallbackColor(ArrowElementKind k) => k switch
        {
            ArrowElementKind.Burn => new Color(1f, 0.55f, 0.16f),
            ArrowElementKind.Freeze => new Color(0.6f, 0.9f, 1f),
            ArrowElementKind.Shock => new Color(1f, 0.92f, 0.3f),
            ArrowElementKind.Poison => new Color(0.55f, 0.85f, 0.3f),
            _ => Color.white,
        };

        /// <summary>명중 한 번: 보통 = hit 원샷, 저이펙트 = mark 정지 표식 짧게. 아트가 없으면 아무것도 하지 않는다(피격 섬광은 기존 연출).</summary>
        public static void PlayHit(ArrowElementKind k, Vector2 at)
        {
            bool low = ArrowElementRules.LowEffects;
            var frames = Frames(k, low ? "mark" : "hit");
            if (frames.Length == 0) return;
            var go = new GameObject("ArrowElementHit");
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = SortOrders.AirVfx;
            var anim = go.AddComponent<ArrowElementAnim>();
            if (low) anim.Hold(frames[0], LowHitMarkSeconds);
            else anim.Play(frames, HitMs, false, true);
        }

        /// <summary>감전 연쇄 한 줄(from → to). 보통 = chain, 저이펙트 = chain_low. 아트가 없으면 얇은 노란 선(저이펙트에서도 최소 표시).</summary>
        public static void PlayChain(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            bool low = ArrowElementRules.LowEffects;
            var frames = Frames(ArrowElementKind.Shock, low ? "chain_low" : "chain");
            var go = new GameObject("ArrowShockChain");
            go.transform.position = from;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortOrders.AirVfx;
            var anim = go.AddComponent<ArrowElementAnim>();
            if (frames.Length > 0)
            {
                go.transform.localScale = new Vector3(len / ChainSpanWorld, 1f, 1f);
                sr.sprite = frames[0];
                if (low) anim.Play(frames, new[] { ChainLowMs }, false, true);
                else anim.Play(frames, ChainMs, false, true);
                return;
            }
            // 대체: 시작점에서 끝점까지 얇은 선(Square는 중앙 피벗 → 중간점에 둔다).
            go.transform.position = from + d * 0.5f;
            go.transform.localScale = new Vector3(len, 0.05f, 1f);
            sr.sprite = SpriteFactory.Square;
            sr.color = FallbackColor(ArrowElementKind.Shock);
            anim.Hold(sr.sprite, 0.12f);
        }
    }

    /// <summary>프레임별 시간(ms)으로 재생하는 작은 애니메이터(게임 시간 — 일시정지 중 멈춤). 원샷은 끝나면 오브젝트 제거.</summary>
    public class ArrowElementAnim : MonoBehaviour
    {
        Sprite[] frames;
        float[] ms;
        bool loop, destroyAtEnd;
        float t, holdSeconds = -1f;
        SpriteRenderer sr;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        public void Play(Sprite[] f, float[] frameMs, bool looping, bool destroyWhenDone)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            frames = f; ms = frameMs; loop = looping; destroyAtEnd = destroyWhenDone; t = 0f; holdSeconds = -1f;
            if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        /// <summary>한 장을 seconds 동안 정지 표시 후 제거.</summary>
        public void Hold(Sprite s, float seconds)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            frames = null; holdSeconds = seconds; t = 0f;
            if (sr != null) sr.sprite = s;
        }

        float Ms(int i) => ms == null || ms.Length == 0 ? 100f : ms[Mathf.Min(i, ms.Length - 1)];

        void Update()
        {
            t += Time.deltaTime;
            if (holdSeconds >= 0f)
            {
                if (t >= holdSeconds) Destroy(gameObject);
                return;
            }
            if (frames == null || frames.Length == 0 || sr == null) return;
            float total = 0f;
            for (int i = 0; i < frames.Length; i++) total += Ms(i);
            total /= 1000f;
            float time = t;
            if (time >= total)
            {
                if (!loop) { if (destroyAtEnd) Destroy(gameObject); else sr.sprite = frames[frames.Length - 1]; return; }
                time %= Mathf.Max(0.001f, total);
            }
            float acc = 0f;
            for (int i = 0; i < frames.Length; i++)
            {
                acc += Ms(i) / 1000f;
                if (time < acc) { sr.sprite = frames[i]; return; }
            }
        }
    }

    /// <summary>
    /// 2026-11-24 #21 적 한 마리에 붙는 속성 화살 상태(화상·빙결 표식·감전 표식·중독 중첩). 카드 화염/여운(EnemyHitStatus)과 별개 — 서로 덮지 않는다.
    /// 적이 죽으면 오브젝트와 함께 사라진다(적은 풀 재사용 없이 Destroy). 주인 플레이어가 죽거나 사라지면 멈춘다. 게임 시간 정지 중 멈춘다.
    /// </summary>
    public sealed class ArrowElementStatus : MonoBehaviour
    {
        Enemy enemy;
        PlayerController owner;

        float burnDps;
        double burnRemaining, burnTick;
        int poisonStacks;
        float poisonStackDps;
        double poisonRemaining, poisonTick;
        float freezeRemaining;
        float shockMarkRemaining;
        float shockLockUntil = float.MinValue;
        readonly Dictionary<ArrowElementKind, int> lastVolley = new Dictionary<ArrowElementKind, int>();

        readonly Dictionary<ArrowElementKind, SpriteRenderer> markers = new Dictionary<ArrowElementKind, SpriteRenderer>();
        readonly Dictionary<ArrowElementKind, ArrowElementAnim> anims = new Dictionary<ArrowElementKind, ArrowElementAnim>();
        readonly Dictionary<ArrowElementKind, bool> markerLow = new Dictionary<ArrowElementKind, bool>();

        // 검수용 읽기 전용.
        public float BurnDps => burnDps;
        public float BurnRemaining => (float)burnRemaining;
        public int PoisonStacks => poisonRemaining > 0d ? poisonStacks : 0;
        public float PoisonStackDps => poisonStackDps;
        public float PoisonRemaining => (float)poisonRemaining;
        public float FreezeRemaining => freezeRemaining;
        public bool MarkerVisible(ArrowElementKind k) => markers.TryGetValue(k, out var m) && m != null && m.enabled;

        static ArrowElementStatus For(Enemy enemy, PlayerController owner)
        {
            var s = enemy.GetComponent<ArrowElementStatus>() ?? enemy.gameObject.AddComponent<ArrowElementStatus>();
            if (s.owner != null && s.owner != owner) return null; // 다른 런의 상태는 건드리지 않는다
            s.enemy = enemy;
            s.owner = owner;
            return s;
        }

        static bool Valid(Enemy enemy, PlayerController owner) =>
            enemy != null && !enemy.IsDead && !enemy.Invulnerable && owner != null && !owner.IsDead && enemy.RunPlayer == owner;

        /// <summary>
        /// 원래 화살의 직접 적중 직후(피해 적용 뒤) 호출. firstHit = 이 화살의 첫 대상(감전 연쇄는 첫 대상에서만).
        /// directDamage = 이 화살이 준 기본 피해(상성 추가 피해 제외) — 감전 연쇄 피해의 기준.
        /// </summary>
        /// <param name="volley">발사 묶음 번호(0 = 묶음 없음). 같은 묶음·같은 속성은 같은 적에게 한 번만.</param>
        public static void OnArrowHit(Enemy enemy, PlayerController owner, ArrowElementKind kind, bool firstHit, float directDamage, Vector2 at,
            int volley = 0)
        {
            if (kind == ArrowElementKind.None || owner == null) return;
            int level = ArrowElementRules.Level(owner.Stats, kind);
            if (level <= 0) return;
            if (volley != 0 && enemy != null)
            {
                var prev = enemy.GetComponent<ArrowElementStatus>();
                if (prev != null && prev.lastVolley.TryGetValue(kind, out int v) && v == volley) return; // 같은 발사의 같은 속성 중복 발동 금지
                if (Valid(enemy, owner)) { var s0 = For(enemy, owner); if (s0 != null) s0.lastVolley[kind] = volley; }
            }
            ArrowElementArt.PlayHit(kind, at); // 죽은 적에게도 명중 연출은 남긴다(막타)
            if (kind == ArrowElementKind.Shock)
            {
                if (firstHit) Chain(enemy, owner, directDamage * ArrowElementRules.ShockDamageFraction, ArrowElementRules.ShockJumps(level));
                return;
            }
            if (!Valid(enemy, owner)) return;
            var s = For(enemy, owner);
            if (s == null) return;
            float damage = owner.Stats.Damage;
            switch (kind)
            {
                case ArrowElementKind.Burn:
                {
                    bool expired = s.burnRemaining <= 0d;
                    float dps = damage * ArrowElementRules.BurnDpsFraction(level);
                    if (expired || dps > s.burnDps) s.burnDps = dps;
                    s.burnRemaining = ArrowElementRules.BurnSeconds;
                    if (expired) s.burnTick = 1d / ArrowElementRules.BurnTicksPerSecond;
                    break;
                }
                case ArrowElementKind.Poison:
                {
                    bool expired = s.poisonRemaining <= 0d;
                    if (expired) { s.poisonStacks = 0; s.poisonStackDps = 0f; s.poisonTick = 1d; }
                    s.poisonStacks = Mathf.Min(ArrowElementRules.PoisonMaxStacks, s.poisonStacks + 1);
                    s.poisonStackDps = Mathf.Max(s.poisonStackDps, damage * ArrowElementRules.PoisonStackDpsFraction(level));
                    s.poisonRemaining = ArrowElementRules.PoisonSeconds;
                    break;
                }
                case ArrowElementKind.Freeze:
                    // 보스: 기존 경직 면역(TryStagger false) 그대로 — 대신 ModifyDirect가 이 화살 피해를 올렸다.
                    if (enemy.TryStagger(ArrowElementRules.FreezeSeconds, ArrowElementRules.FreezeCooldown(level)))
                        s.freezeRemaining = ArrowElementRules.FreezeSeconds;
                    break;
            }
            s.RefreshMarkers();
        }

        /// <summary>감전 연쇄: 마지막으로 맞은 적에서 가장 가까운(범위 안, 아직 안 맞은, 잠금 아닌) 적으로 jumps번. 보조 피해·재귀 없음.</summary>
        static void Chain(Enemy first, PlayerController owner, float damage, int jumps)
        {
            if (first == null || jumps <= 0 || damage <= 0f) return;
            var visited = new HashSet<Enemy> { first };
            Vector2 from = first.transform.position;
            if (Valid(first, owner)) { var fs = For(first, owner); if (fs != null) { fs.shockMarkRemaining = ArrowElementRules.ShockMarkSeconds; fs.RefreshMarkers(); } }
            float r2 = ArrowElementRules.ShockRange * ArrowElementRules.ShockRange;
            for (int j = 0; j < jumps; j++)
            {
                Enemy best = null;
                float bestD = float.MaxValue;
                foreach (var e in Enemy.Active)
                {
                    if (e == null || visited.Contains(e) || !Valid(e, owner)) continue;
                    var st = e.GetComponent<ArrowElementStatus>();
                    if (st != null && Time.time < st.shockLockUntil) continue;
                    float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                    if (d <= r2 && d < bestD) { bestD = d; best = e; }
                }
                if (best == null) break;
                visited.Add(best);
                Vector2 to = best.transform.position;
                ArrowElementArt.PlayChain(from, to);
                var s = For(best, owner);
                if (s != null)
                {
                    s.shockLockUntil = Time.time + ArrowElementRules.ShockRecipientLockout;
                    s.shockMarkRemaining = ArrowElementRules.ShockMarkSeconds;
                    s.RefreshMarkers();
                }
                best.TakeDamage(damage, HitKind.Effect, to);
                from = to;
            }
        }

        void Update()
        {
            if (owner == null || owner.IsDead || enemy == null || enemy.IsDead) { HideAll(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (freezeRemaining > 0f) freezeRemaining = Mathf.Max(0f, freezeRemaining - dt);
            if (shockMarkRemaining > 0f) shockMarkRemaining = Mathf.Max(0f, shockMarkRemaining - dt);

            if (burnRemaining > 0d)
            {
                double step = 1d / ArrowElementRules.BurnTicksPerSecond;
                double elapsed = System.Math.Min(dt, burnRemaining);
                burnRemaining = System.Math.Max(0d, burnRemaining - elapsed);
                burnTick -= elapsed;
                while (burnTick <= 1e-8 && !enemy.IsDead)
                {
                    burnTick += step;
                    enemy.TakeDamage(burnDps * (float)step, HitKind.Dot);
                }
                if (burnRemaining <= 0d) burnDps = 0f;
            }
            if (!enemy.IsDead && poisonRemaining > 0d)
            {
                double elapsed = System.Math.Min(dt, poisonRemaining);
                poisonRemaining = System.Math.Max(0d, poisonRemaining - elapsed);
                poisonTick -= elapsed;
                while (poisonTick <= 1e-8 && !enemy.IsDead)
                {
                    poisonTick += 1d;
                    enemy.TakeDamage(poisonStackDps * poisonStacks, HitKind.Dot);
                }
                if (poisonRemaining <= 0d) { poisonStacks = 0; poisonStackDps = 0f; }
            }
            if (!enemy.IsDead) RefreshMarkers();
        }

        bool Active(ArrowElementKind k) => k switch
        {
            ArrowElementKind.Burn => burnRemaining > 0d,
            ArrowElementKind.Freeze => freezeRemaining > 0f,
            ArrowElementKind.Shock => shockMarkRemaining > 0f,
            ArrowElementKind.Poison => poisonRemaining > 0d && poisonStacks > 0,
            _ => false,
        };

        static readonly ArrowElementKind[] MarkerOrder = { ArrowElementKind.Burn, ArrowElementKind.Freeze, ArrowElementKind.Shock, ArrowElementKind.Poison };

        /// <summary>활성 표식: 머리 위 한 줄(카드 화염/여운 표식 — 몸 위 좌우 — 과 겹치지 않는 높이), 활성 수에 맞춰 가운데 정렬.
        /// 보통 = active 루프, 저이펙트 = mark 정지(상태는 항상 보인다). 중독은 중첩만큼 조금씩 커진다.</summary>
        void RefreshMarkers()
        {
            if (enemy == null) return;
            bool low = ArrowElementRules.LowEffects;
            int count = 0;
            foreach (var k in MarkerOrder) if (Active(k)) count++;
            float r = enemy.Def != null ? enemy.Def.radius : 0.5f;
            const float Spacing = 0.36f;
            float x = -(count - 1) * Spacing * 0.5f;
            foreach (var k in MarkerOrder)
            {
                bool on = Active(k);
                markers.TryGetValue(k, out var m);
                if (!on) { if (m != null) m.enabled = false; continue; }
                if (m == null)
                {
                    var go = new GameObject("ArrowElementMark_" + ArrowElementRules.ArtId(k));
                    go.transform.SetParent(transform, false);
                    m = go.AddComponent<SpriteRenderer>();
                    m.sortingOrder = SortOrders.AirVfx - 1;
                    markers[k] = m;
                    anims[k] = go.AddComponent<ArrowElementAnim>();
                    markerLow[k] = !low; // 아래에서 처음 설정되게
                }
                if (!m.enabled || markerLow[k] != low)
                {
                    var frames = ArrowElementArt.Frames(k, low ? "mark" : "active");
                    if (frames.Length == 0) frames = ArrowElementArt.Frames(k, "mark");
                    if (frames.Length > 0)
                    {
                        m.color = Color.white;
                        if (low || frames.Length == 1) { anims[k].enabled = false; m.sprite = frames[0]; }
                        else { anims[k].enabled = true; anims[k].Play(frames, ArrowElementArt.ActiveMs, true, false); }
                    }
                    else
                    {
                        anims[k].enabled = false;
                        m.sprite = SpriteFactory.Circle;
                        m.color = ArrowElementArt.FallbackColor(k);
                    }
                    markerLow[k] = low;
                }
                m.enabled = true;
                bool art = m.sprite != SpriteFactory.Circle;
                float baseScale = art ? 0.8f : 0.22f; // 64px 아트 = 0.5 월드 → 0.4, 대체 원 = 0.22
                float grow = k == ArrowElementKind.Poison ? 1f + 0.06f * (poisonStacks - 1) : 1f;
                m.transform.localPosition = new Vector3(x, r * 1.25f + 0.15f, 0f);
                m.transform.localScale = Vector3.one * baseScale * grow;
                x += Spacing;
            }
        }

        void HideAll()
        {
            foreach (var m in markers.Values) if (m != null) m.enabled = false;
        }
    }
}

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #21 머리 위 다음 화살 미리보기(기본 활 + 속성 1개 이상일 때만). 왼쪽부터 실제로 쏘는(spawn) 순서 —
    /// PlayerStats.PeekArrowElement(i)는 ConsumeArrowElement와 같은 계산이라 아이콘 i번 = 다음 발사의 i번째 원래 화살.
    /// 다음 발사 묶음(1 + 다중 발사)은 크고 진하게, 그 뒤 몇 발은 작고 흐리게(묶음 사이 간격). 착탄 순서가 아니라 발사 순서다(거리에 따라 맞는 순서는 다를 수 있음).
    /// 그림은 정지 화살 아트(&lt;속성&gt;_arrow_low, 위를 향하게) — 저이펙트에서도 같은 정지 그림. 아이콘 수 상한으로 HUD 쪽으로 넓어지지 않게.
    /// </summary>
    public class ArrowQueuePreview : MonoBehaviour
    {
        public const int MaxNextVolley = PlayerStats.MaxExtraThrusts + 1;
        public const int FollowingShown = 3;
        public const float IconLength = 0.5f, IconSpacing = 0.21f, GroupGap = 0.2f, FollowingScale = 0.75f, FollowingAlpha = 0.45f;

        PlayerController player;
        PlayerStats stats;
        Transform row;
        readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
        readonly List<ArrowElementKind> shown = new List<ArrowElementKind>();
        int shownVolley = -1;

        /// <summary>검수용: 지금 보이는 아이콘 속성(왼쪽 = 다음에 쏘는 화살)과 다음 묶음 크기.</summary>
        public IReadOnlyList<ArrowElementKind> Shown => shown;
        public int NextVolleyShown => shownVolley;
        public bool Visible => row != null && row.gameObject.activeSelf;

        public static ArrowQueuePreview Attach(PlayerController player, PlayerStats stats)
        {
            if (player == null) return null;
            var p = player.GetComponent<ArrowQueuePreview>() ?? player.gameObject.AddComponent<ArrowQueuePreview>();
            p.player = player;
            p.stats = stats;
            return p;
        }

        float HeadTop()
        {
            var art = player != null && player.Visual != null ? player.Visual.BodyArt : null;
            return art != null ? -PlayerController.Radius + art.targetBodyHeightWorld : PlayerController.Radius;
        }

        void LateUpdate()
        {
            bool on = player != null && !player.IsDead && stats != null && stats.HasArrowElement;
            if (row == null)
            {
                if (!on) return;
                row = new GameObject("ArrowQueuePreview").transform;
                row.SetParent(transform, false);
            }
            row.gameObject.SetActive(on);
            if (!on) { shown.Clear(); shownVolley = -1; return; }
            row.localPosition = new Vector3(0f, HeadTop() + 0.4f, 0f);

            int volley = Mathf.Clamp(1 + stats.extraThrusts, 1, MaxNextVolley);
            int total = volley + FollowingShown;
            bool same = shownVolley == volley && shown.Count == total;
            for (int i = 0; same && i < total; i++) if (shown[i] != stats.PeekArrowElement(i)) same = false;
            if (same) return;

            shown.Clear();
            for (int i = 0; i < total; i++) shown.Add(stats.PeekArrowElement(i));
            shownVolley = volley;
            while (icons.Count < total)
            {
                var go = new GameObject("NextArrow" + icons.Count);
                go.transform.SetParent(row, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortOrders.AirVfx;
                icons.Add(sr);
            }
            float width = (total - 1) * IconSpacing + GroupGap;
            float x = -width * 0.5f;
            for (int i = 0; i < icons.Count; i++)
            {
                var sr = icons[i];
                if (i >= total) { sr.enabled = false; continue; }
                bool next = i < volley;
                if (i == volley) x += GroupGap;
                var frames = ArrowElementArt.Frames(shown[i], "arrow_low");
                float s;
                if (frames.Length > 0)
                {
                    sr.sprite = frames[0];
                    float len = Mathf.Max(0.01f, 98f / 128f); // 화살 가로 스팬 98px(128 PPU)
                    s = IconLength / len;
                    sr.color = new Color(1f, 1f, 1f, next ? 1f : FollowingAlpha);
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 위를 향하게
                }
                else
                {
                    sr.sprite = SpriteFactory.Circle;
                    s = 0.1f;
                    var c = ArrowElementArt.FallbackColor(shown[i]); c.a = next ? 1f : FollowingAlpha;
                    sr.color = c;
                    sr.transform.localRotation = Quaternion.identity;
                }
                sr.transform.localScale = Vector3.one * s * (next ? 1f : FollowingScale);
                sr.transform.localPosition = new Vector3(x, next ? 0f : -0.03f, 0f);
                sr.enabled = true;
                x += IconSpacing;
            }
        }
    }
}
