using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-24 #25 시그니처 전투 VFX — ArtSource/signature-drinks-2026-11-24 아트(SignatureArt)를 재생한다. 표시 전용(판정·피해·거리 판정 없음).
    /// 전투 시간(스케일 시간)을 따른다. 저이펙트(HitFeedback.LowEffects)는 manifest의 _low clip으로 교체(일반과 함께 재생하지 않음).
    /// 아트가 임포트되지 않았으면(Resources에 프레임 없음) 기존 절차형 원으로 대체한다 — 범위 표시는 항상 남는다.
    /// 합성 순서: 솔트 림·질주 바람선·잔상 = 그림자 층(캐릭터·적·공격 예고 아래), 얼음 배지 = 공중 VFX 층, 서리 = HUD 캔버스 아래.
    /// </summary>
    public static class SignatureVfx
    {
        public const int GroundOrder = SortOrders.Shadow;
        public const int BadgeOrder = SortOrders.AirVfx;
        /// <summary>서리 캔버스 정렬(HUD 캔버스 0보다 아래).</summary>
        public const int EdgeCanvasOrder = -1;
        public const float SaltRimDiameterWorld = 8f;
        public const float WindFootLift = 0.2f;

        /// <summary>마지막으로 재생한 효과 종류(검수용).</summary>
        public static string LastPlayed { get; private set; } = "";
        public static int BadgesShown { get; private set; }

        // ───── 솔트 림: 발동 순간 플레이어 좌표에 고정, 지름 8월드(반경 4 판정과 같음) ─────
        public static void SaltRim(Vector2 at, Color accent)
        {
            bool low = HitFeedback.LowEffects;
            string clip = SignatureArt.Resolve("salt_ring", low);
            if (Sequence.Play(clip, at, GroundOrder) != null) { LastPlayed = clip; return; }
            Ring(at, SignatureRuntime.SaltRimRadius, accent);
        }

        /// <summary>대체(아트 없음) 범위 고리.</summary>
        public static void Ring(Vector2 at, float radius, Color accent)
        {
            bool low = HitFeedback.LowEffects;
            float d = Mathf.Max(0.5f, radius * 2f);
            var c = accent; c.a = low ? 0.35f : 0.6f;
            FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * d * 0.25f, Vector2.one * d, c, 0.45f, GroundOrder);
            LastPlayed = low ? "ring_low" : "ring";
        }

        // ───── 사이드카 질주: 5초 동안 바람선 반복 + 75ms 잔상(이동 중에만), 저이펙트는 발밑 정적 갈매기표 ─────
        public static SidecarTrail Sidecar(PlayerController player, float seconds)
        {
            if (player == null) return null;
            var go = new GameObject("SignatureSidecar");
            var t = go.AddComponent<SidecarTrail>();
            t.Init(player, seconds, HitFeedback.LowEffects);
            LastPlayed = HitFeedback.LowEffects ? "sidecar_low" : "sidecar_wind";
            return t;
        }

        // ───── 화이트 아웃: 가장자리 서리(가운데 투명, 720p 56px 띠) — 전체 화면 틴트·큰 원 없음 ─────
        public static void WhiteOutEdge()
        {
            bool low = HitFeedback.LowEffects;
            string clip = SignatureArt.Resolve("whiteout_edge", low);
            var frames = SignatureArt.Load(clip);
            if (frames == null) { LastPlayed = "edge_missing"; return; }
            var go = new GameObject("SignatureFrost");
            var canvas = UiFactory.CreateCanvas(go.transform, "Canvas", EdgeCanvasOrder);
            var img = UiFactory.AddImage(UiFactory.Stretch("Frost", canvas.transform), Color.white);
            img.raycastTarget = false;
            img.type = Image.Type.Sliced;
            img.fillCenter = false; // 가운데 시야 보존(원본도 투명)
            img.sprite = frames[0];
            go.AddComponent<EdgeFrost>().Init(canvas, img, SignatureArt.Get(clip), frames);
            LastPlayed = clip;
        }

        /// <summary>화이트 아웃 얼음 배지: **경직 적용이 실제로 성공한** 살아 있는 일반·정예에게만(호출부 SignatureRuntime이 ApplySignatureHold == true일 때만 부른다).
        /// 보스·플레이어에는 붙이지 않는다(여기서도 한 번 더 거른다). 적 사망·경직 종료·seconds 경과 중 먼저 오는 때 지운다.</summary>
        public static IceBadge Badge(Enemy enemy, float seconds)
        {
            if (enemy == null || enemy.IsDead || enemy.Def == null || enemy.Def.IsBoss || !enemy.Staggered) return null;
            var clip = SignatureArt.Resolve("whiteout_hold", HitFeedback.LowEffects);
            var frames = SignatureArt.Load(clip);
            if (frames == null) return null;
            var go = new GameObject("SignatureIceBadge");
            var b = go.AddComponent<IceBadge>();
            b.Init(enemy, frames[0], seconds);
            BadgesShown++;
            return b;
        }

        // ───── 구성 요소 ─────

        /// <summary>월드 위치 고정 1회 재생(노출 시간 표, 스케일 시간). 끝나면 사라진다.</summary>
        public sealed class Sequence : MonoBehaviour
        {
            SpriteRenderer sr; SignatureArt.Clip clip; Sprite[] frames; float ms;
            public static Sequence Play(string clipName, Vector2 at, int order)
            {
                var frames = SignatureArt.Load(clipName);
                if (frames == null) return null;
                var go = new GameObject("SignatureSeq_" + clipName);
                go.transform.position = at;
                var s = go.AddComponent<Sequence>();
                s.sr = go.AddComponent<SpriteRenderer>();
                s.sr.sortingOrder = order;
                s.frames = frames; s.clip = SignatureArt.Get(clipName);
                s.sr.sprite = frames[0];
                return s;
            }
            void Update()
            {
                ms += Time.deltaTime * 1000f;
                if (ms >= clip.TotalMs) { Destroy(gameObject); return; }
                sr.sprite = frames[SignatureArt.FrameAt(clip, ms)];
            }
        }

        public sealed class SidecarTrail : MonoBehaviour
        {
            PlayerController player; bool low; float remaining, ms, ghostTimer;
            SpriteRenderer wind, mark;
            Sprite[] windFrames; SignatureArt.Clip windClip;
            Vector2 lastPos, dir = Vector2.right;
            readonly List<(SpriteRenderer sr, float age)> ghosts = new List<(SpriteRenderer, float)>();
            public bool WindVisible => wind != null && wind.enabled;
            public int GhostCount => ghosts.Count;

            public void Init(PlayerController p, float seconds, bool lowEffects)
            {
                player = p; low = lowEffects; remaining = seconds; lastPos = p.Position;
                dir = new Vector2(p.Facing >= 0f ? 1f : -1f, 0f);
                if (low)
                {
                    var f = SignatureArt.Load("sidecar_low");
                    mark = NewRenderer("Mark", f != null ? f[0] : SpriteFactory.Circle);
                    if (f == null) { mark.color = new Color(1f, 0.8f, 0.4f, 0.35f); mark.transform.localScale = Vector3.one * 0.5f; }
                }
                else
                {
                    windFrames = SignatureArt.Load("sidecar_wind");
                    windClip = SignatureArt.Get("sidecar_wind");
                    if (windFrames != null) { wind = NewRenderer("Wind", windFrames[0]); wind.enabled = false; }
                }
                Place();
            }

            SpriteRenderer NewRenderer(string n, Sprite s)
            {
                var go = new GameObject(n);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = s; r.sortingOrder = GroundOrder;
                return r;
            }

            Vector3 Foot => player != null ? (Vector3)player.Position : transform.position;

            void Place()
            {
                if (player == null) return;
                transform.position = Foot;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (wind != null) { wind.transform.position = Foot + Vector3.up * WindFootLift; wind.transform.rotation = Quaternion.Euler(0f, 0f, ang); }
                if (mark != null) mark.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            }

            void Update()
            {
                float dt = Time.deltaTime;
                remaining -= dt;
                if (player == null || player.IsDead || remaining <= 0f) { Destroy(gameObject); return; }
                Vector2 pos = player.Position;
                Vector2 delta = pos - lastPos;
                bool moving = delta.sqrMagnitude > 1e-6f;
                if (moving)
                {
                    var nd = delta.normalized;
                    // 저이펙트는 이동 방향이 바뀔 때만(8방향 기준) 갱신 — 계속 흔들리지 않게.
                    if (!low || Vector2.Angle(nd, dir) > 22.5f) dir = nd;
                }
                lastPos = pos;
                Place();
                if (wind != null)
                {
                    wind.enabled = moving;
                    if (moving) { ms += dt * 1000f; wind.sprite = windFrames[SignatureArt.FrameAt(windClip, ms, true)]; }
                }
                if (!low)
                {
                    ghostTimer -= dt;
                    if (moving && ghostTimer <= 0f) { Snapshot(); ghostTimer = SignatureArt.GhostSpacing; }
                }
                for (int i = ghosts.Count - 1; i >= 0; i--)
                {
                    var (sr, age) = ghosts[i];
                    age += dt;
                    if (sr == null || age >= SignatureArt.GhostLife) { if (sr != null) Destroy(sr.gameObject); ghosts.RemoveAt(i); continue; }
                    var c = sr.color; c.a = SignatureArt.GhostOpacity[0] * (1f - age / SignatureArt.GhostLife); sr.color = c;
                    ghosts[i] = (sr, age);
                }
            }

            void Snapshot()
            {
                var body = player.Visual != null ? player.Visual.Body : null;
                if (body == null || body.sprite == null) return;
                while (ghosts.Count >= SignatureArt.GhostOpacity.Length) { if (ghosts[0].sr != null) Destroy(ghosts[0].sr.gameObject); ghosts.RemoveAt(0); }
                var go = new GameObject("Ghost");
                go.transform.SetParent(transform, true);
                go.transform.position = body.transform.position;
                go.transform.rotation = body.transform.rotation;
                go.transform.localScale = body.transform.lossyScale;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = body.sprite; r.flipX = body.flipX; r.flipY = body.flipY;
                r.sortingOrder = GroundOrder;
                r.color = new Color(1f, 1f, 1f, SignatureArt.GhostOpacity[0]);
                ghosts.Add((r, 0f));
            }

            /// <summary>버프 종료·런 종료 때 바람선·잔상을 모두 지운다.</summary>
            public void Stop() { if (this != null) Destroy(gameObject); }
        }

        public sealed class EdgeFrost : MonoBehaviour
        {
            Canvas canvas; Image img; SignatureArt.Clip clip; Sprite[] frames; float ms;
            public void Init(Canvas c, Image i, SignatureArt.Clip cl, Sprite[] f) { canvas = c; img = i; clip = cl; frames = f; Fit(); }
            void Fit()
            {
                // 원본 테두리 14px(9분할) → 화면 높이 720 기준 56px. 캔버스 단위 = 화면 px / scaleFactor.
                float sf = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                float wantUnits = SignatureArt.EdgeRef720Px * Screen.height / 720f / sf;
                img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, 14f / Mathf.Max(1f, wantUnits));
            }
            void Update()
            {
                ms += Time.deltaTime * 1000f;
                if (ms >= clip.TotalMs) { Destroy(gameObject); return; }
                img.sprite = frames[SignatureArt.FrameAt(clip, ms)];
                Fit();
            }
        }

        public sealed class IceBadge : MonoBehaviour
        {
            Enemy enemy; SpriteRenderer sr; float remaining;
            public Enemy Target => enemy;
            public void Init(Enemy e, Sprite s, float seconds)
            {
                enemy = e; remaining = seconds;
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = s; sr.sortingOrder = BadgeOrder;
                LateUpdate();
            }
            void LateUpdate()
            {
                remaining -= Time.deltaTime;
                if (enemy == null || enemy.IsDead || !enemy.Staggered || remaining <= 0f) { Destroy(gameObject); return; }
                var cam = Camera.main;
                float size = SignatureArt.WorldPer720Px(cam, SignatureArt.BadgeRef720Px);
                float spriteW = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;
                transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, spriteW));
                var b = enemy.VisualBounds;
                var p = new Vector3(b.center.x, b.max.y + size * 0.1f, 0f); // 하단 중앙 앵커 → 적 실루엣 바로 위
                if (cam != null && cam.orthographic)
                {
                    float top = cam.transform.position.y + cam.orthographicSize - size;
                    float half = cam.orthographicSize * cam.aspect;
                    bool onScreen = Mathf.Abs(b.center.x - cam.transform.position.x) <= half && b.min.y <= top + size;
                    if (!onScreen) { sr.enabled = false; return; }
                    if (p.y > top) p.y = Mathf.Max(b.center.y, top); // 화면 위 끝이면 짧은 대체 위치(몸 쪽으로 내림)
                }
                sr.enabled = true;
                transform.position = p;
            }
        }
    }
}
