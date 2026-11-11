using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-11 마스터 도전 인수 아트(품질 3+3 / 상성 3+3 clip)의 1회 재생기. 월드(SpriteRenderer) 또는 UI(Image) 대상.
    /// 아트 계약: 성공 거래·확정 적중마다 정확히 1회, loop 없음, 끝나면 renderer 끄고 sprite 해제 후 pool 반납.
    /// 재사용 때 frame0·elapsed0·white·scale·rotation0·flip false로 초기화. 동시 개체 상한을 넘으면 새 재생을 건너뛴다(쌓지 않음).
    /// </summary>
    public sealed class MasterVfx : MonoBehaviour
    {
        /// <summary>월드 상성 효과 동시 상한(다수 적·DOT 과밀 예산)과 같은 프레임 연속 생성 최소 간격(초, 전투 시간).</summary>
        public const int MaxWorldActive = 6;
        public const float AffinityMinInterval = 0.06f;
        /// <summary>UI 품질 효과 동시 상한(제조 창 연타 시 겹침 방지 — 새 것으로 교체).</summary>
        public const int MaxUiActive = 1;

        static readonly List<MasterVfx> worldPool = new List<MasterVfx>(), uiPool = new List<MasterVfx>();
        static readonly List<MasterVfx> active = new List<MasterVfx>();
        static Transform worldRoot;
        static float nextAffinityAt;

        public static int Played { get; private set; }
        public static int Skipped { get; private set; }
        public static string LastClip { get; private set; }
        public static int ActiveWorld { get { int n = 0; foreach (var v in active) if (v != null && v.sr != null) n++; return n; } }
        public static int ActiveUi { get { int n = 0; foreach (var v in active) if (v != null && v.img != null) n++; return n; } }
        public static int PooledCount => worldPool.Count + uiPool.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            worldPool.Clear(); uiPool.Clear(); active.Clear(); worldRoot = null;
            nextAffinityAt = 0f; Played = 0; Skipped = 0; LastClip = null;
        }

        SpriteRenderer sr;
        Image img;
        MasterArt.Clip clip;
        Sprite[] frames;
        float elapsedMs;
        bool unscaled;
        int shown = -1;
        public bool Playing { get; private set; }

        static void Purge()
        {
            worldPool.RemoveAll(v => v == null);
            uiPool.RemoveAll(v => v == null);
            active.RemoveAll(v => v == null);
        }

        /// <summary>재생 중인 효과를 모두 즉시 반납(런 종료·재시작). 풀은 런 사이에 남으므로, 정지 상태로 끝난 런의 효과가 다음 런의 동시 상한을 차지하지 않게 한다.</summary>
        /// <summary>2026-11-12 UI 효과만 즉시 반납(제조 창 닫기). 창이 닫히면 효과 개체가 비활성이라 Update가 멈춰, 다시 열 때 정지 프레임이 남지 않게 한다.</summary>
        public static void ReleaseUi()
        {
            Purge();
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].img != null) active[i].Release();
        }

        public static void ReleaseAll()
        {
            Purge();
            for (int i = active.Count - 1; i >= 0; i--) active[i].Release();
            active.Clear();
            nextAffinityAt = 0f;
        }

        /// <summary>2026-11-11 가독성 국소 개선(승인 원본 PNG 무수정): 월드 상성 효과 표시 배율. 720p(1월드≈60px)에서 배율 1이면 원두 내용이 ≈24×30px로 작아
        /// 원두(가장 작은 내용, 갈색)는 1.5, 거품·찻잎은 1.25. 동시 상한·간격 예산은 그대로.</summary>
        public const float AffinityScaleCoffee = 1.5f, AffinityScaleOther = 1.25f;
        public static float AffinityScale(DrinkKind kind) => kind == DrinkKind.Coffee ? AffinityScaleCoffee : AffinityScaleOther;
        /// <summary>갈색 적 위 갈색 원두 대비 보강: 같은 프레임을 크림색으로 상하좌우 2원본px 밀어 뒤에 그린 외곽선(원두 clip만, 4 renderer, 풀 개체에 붙어 재사용).</summary>
        public static readonly Color OutlineColor = new Color(1f, 0.93f, 0.78f, 0.9f);
        public const float OutlineSourcePx = 2f;
        public static bool UsesOutline(string clipName) => clipName != null && clipName.StartsWith("coffee_bean");

        /// <summary>상성 추가 피해가 실제로 확정된 직접 적중 1회에 호출. 저이펙트면 low clip으로 교체.</summary>
        public static bool PlayAffinity(DrinkKind kind, Vector2 at)
        {
            if (Time.time < nextAffinityAt) { Skipped++; return false; }
            var v = PlayWorld(MasterArt.AffinityClip(kind, HitFeedback.LowEffects), at, AffinityScale(kind), false);
            if (v != null) nextAffinityAt = Time.time + AffinityMinInterval;
            return v != null;
        }

        public static MasterVfx PlayWorld(string clipName, Vector2 at, float scale, bool useUnscaled)
        {
            Purge();
            var def = MasterArt.Get(clipName);
            var spr = MasterArt.Load(clipName);
            if (def == null || spr == null) { Skipped++; return null; }
            if (ActiveWorld >= MaxWorldActive) { Skipped++; return null; }
            MasterVfx v = null;
            if (worldPool.Count > 0) { v = worldPool[worldPool.Count - 1]; worldPool.RemoveAt(worldPool.Count - 1); }
            if (v == null)
            {
                if (worldRoot == null) worldRoot = new GameObject("MasterVfxPool").transform;
                var go = new GameObject("MasterVfx");
                go.transform.SetParent(worldRoot, false);
                v = go.AddComponent<MasterVfx>();
                v.sr = go.AddComponent<SpriteRenderer>();
                v.sr.sortingOrder = SortOrders.AirVfx + 1; // 2026-11-11 같은 적중점·같은 순서(AirVfx)의 피격 불꽃에 가려지던 문제 → 불꽃 위(피해 숫자와 같은 단)
            }
            v.transform.position = new Vector3(at.x, at.y, 0f);
            v.Begin(def, spr, scale, useUnscaled);
            return v;
        }

        /// <summary>UI 슬롯(제조 창 등)에 1회 재생. 이미 재생 중이면 그것을 즉시 반납하고 새 것으로 교체(겹침·누적 없음).</summary>
        public static MasterVfx PlayUi(string clipName, RectTransform slot, float sizePx)
        {
            Purge();
            if (slot == null) { Skipped++; return null; }
            var def = MasterArt.Get(clipName);
            var spr = MasterArt.Load(clipName);
            if (def == null || spr == null) { Skipped++; return null; }
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].img != null) active[i].Release();
            MasterVfx v = null;
            if (uiPool.Count > 0) { v = uiPool[uiPool.Count - 1]; uiPool.RemoveAt(uiPool.Count - 1); }
            if (v == null)
            {
                var go = new GameObject("MasterVfxUi", typeof(RectTransform));
                v = go.AddComponent<MasterVfx>();
                v.img = go.AddComponent<Image>();
                v.img.raycastTarget = false;
                v.img.preserveAspect = true;
            }
            var rt = (RectTransform)v.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(sizePx, sizePx);
            rt.SetAsLastSibling();
            v.Begin(def, spr, 1f, true);
            return v;
        }

        SpriteRenderer[] outline;
        public bool OutlineOn => outline != null && outline[0].enabled;
        public int OutlineCount { get { int n = 0; if (outline != null) foreach (var o in outline) if (o.enabled) n++; return n; } }

        static Material outlineMat;
        /// <summary>외곽선 재질: 텍스처 알파만 쓰고 색은 renderer 색인 단색 실루엣(GUI/Text 셰이더 — 월드 피해 숫자가 이미 쓰는 글꼴 재질과 같은 셰이더라 빌드에 포함).
        /// 기본 Sprite 재질은 색을 곱해 원두가 더 어두운 갈색 테두리가 되므로 쓰지 않는다. 셰이더를 못 찾으면 외곽선 없이 본 효과만.</summary>
        static Material OutlineMaterial()
        {
            if (outlineMat != null) return outlineMat;
            var font = UiFactory.Font;
            var sh = font != null && font.material != null ? font.material.shader : Shader.Find("GUI/Text Shader");
            if (sh == null) return null;
            outlineMat = new Material(sh) { name = "MasterVfxOutline", hideFlags = HideFlags.DontSave };
            return outlineMat;
        }

        void SetOutline(bool on)
        {
            if (sr == null) return;
            if (on && outline == null && OutlineMaterial() == null) on = false;
            if (on && outline == null)
            {
                outline = new SpriteRenderer[4];
                float d = OutlineSourcePx / 128f; // 원본 PPU 128 — 부모 배율을 따라 커진다
                var offs = new[] { new Vector2(d, 0f), new Vector2(-d, 0f), new Vector2(0f, d), new Vector2(0f, -d) };
                for (int i = 0; i < 4; i++)
                {
                    var go = new GameObject("Outline" + i);
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = offs[i];
                    outline[i] = go.AddComponent<SpriteRenderer>();
                    outline[i].sharedMaterial = OutlineMaterial();
                    outline[i].sortingOrder = SortOrders.AirVfx; // 본 효과(AirVfx+1) 바로 뒤, 개체(Actor) 앞
                }
            }
            if (outline == null) return;
            foreach (var o in outline) { o.enabled = on; o.color = OutlineColor; o.flipX = false; o.flipY = false; if (!on) o.sprite = null; }
        }

        void Begin(MasterArt.Clip def, Sprite[] spr, float scale, bool useUnscaled)
        {
            clip = def; frames = spr; elapsedMs = 0f; unscaled = useUnscaled; shown = -1;
            transform.localScale = Vector3.one * scale;
            transform.localRotation = Quaternion.identity;
            SetOutline(sr != null && UsesOutline(def.name));
            if (sr != null) { sr.color = Color.white; sr.flipX = false; sr.flipY = false; sr.enabled = true; }
            if (img != null) { img.color = Color.white; img.enabled = true; }
            gameObject.SetActive(true);
            Playing = true;
            ShowFrame(0);
            active.Add(this);
            Played++;
            LastClip = def.name;
        }

        void ShowFrame(int i)
        {
            if (i == shown) return;
            shown = i;
            if (sr != null) sr.sprite = frames[i];
            if (img != null) img.sprite = frames[i];
            if (outline != null && outline[0].enabled) foreach (var o in outline) o.sprite = frames[i];
        }

        void Update()
        {
            if (!Playing) return;
            Step((unscaled ? Time.unscaledDeltaTime : Time.deltaTime) * 1000f);
        }

        /// <summary>시험/본 루프 공용 진행(ms). 전체 길이를 지나면 반납(마지막 프레임은 완전 투명이라 잔상 없음).</summary>
        public void Step(float dtMs)
        {
            if (!Playing) return;
            elapsedMs += Mathf.Max(0f, dtMs);
            if (elapsedMs >= clip.TotalMs) { Release(); return; }
            ShowFrame(MasterArt.FrameAt(clip, elapsedMs));
        }

        public void Release()
        {
            if (!Playing) return;
            Playing = false;
            active.Remove(this);
            if (sr != null) { sr.sprite = null; sr.enabled = false; }
            if (img != null) { img.sprite = null; img.enabled = false; }
            SetOutline(false);
            shown = -1;
            gameObject.SetActive(false);
            if (img != null) uiPool.Add(this); else worldPool.Add(this);
        }

        public int CurrentFrame => shown;
        public string ClipName => clip?.name;
    }
}
