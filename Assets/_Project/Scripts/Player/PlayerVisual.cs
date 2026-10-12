using UnityEngine;
using UnityEngine.Rendering;

namespace CCT
{
    /// <summary>
    /// 04 §9 플레이어 시각 분리: 물리 루트(위치·반지름·판정)는 그대로 두고 이 Visual 자식만 반전·확대·애니메이션한다.
    /// - 직업 몸체 아트(ArtLibrary: bartender / tea_sommelier)가 있으면 발 피벗(0.5,0.125) 스프라이트를 루트 원 바닥에 맞추고
    ///   visualScale = H / (bodyHeightPx/128)로 균일 확대, idle/move 클립 재생.
    /// - 없으면 기존 캡슐 플레이스홀더(지름 Radius*2, 직업 색).
    /// - 장착 무기: 손 anchor(manifest anchorsByFrame "hand"/"grip")가 있으면 그 위치, 없으면 1차 임시 손 위치(보고 대상).
    /// - 발밑 ground_shadow(알파 0.22).
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        /// <summary>v5처럼 손 anchor가 없는 1차 소스용 임시 손 위치(256 캔버스 좌하단 원점 px). 출시 전 anchor로 교체해야 한다.</summary>
        public static readonly Vector2 ProvisionalHandPx = new Vector2(150f, 112f);
        /// <summary>2026-10-09 사용자 피드백 활 v2(PixelLab Edit image Pro + Aseprite, ArtSource/weapon-bow-v2-2026-10-09): 얇고 매끄러운 활대.
        /// v1과 같은 캔버스·pivot·grip(76,128)·arrow 위치로 맞춰 anchor는 v1 manifest를 그대로 쓴다. 없으면 v1.</summary>
        public const string BowV2Resource = "ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00";
        const float BowV2ContentLongestPx = 200f; // v2 알파 영역 세로(32..232)

        public static Sprite HeldWeaponSprite(string weaponArtId, Sprite v1)
        {
            if (weaponArtId != "weapon_bow") return v1;
            var v2 = Resources.Load<Sprite>(BowV2Resource);
            return v2 != null ? v2 : v1;
        }

        public static float HeldWeaponContentLongestWorld(ArtAsset w, string weaponArtId)
        {
            if (weaponArtId == "weapon_bow" && Resources.Load<Sprite>(BowV2Resource) != null) return BowV2ContentLongestPx / w.pixelsPerUnit;
            var content = w.ContentWorldSize;
            return Mathf.Max(content.x, content.y);
        }

        /// <summary>주무기 활의 표시 길이/플레이어 높이(04 §8 원래 0.7 → 2026-10-09 사용자 피드백으로 0.5).</summary>
        public const float HeldBowLengthRatio = 0.5f;
        ArtClip bowIdle, bowMove, bowRanged;
        bool UsesBowReady => bodyArtId == "tea_sommelier" && WeaponArtId == "weapon_bow";
        static readonly int[] BowMoveBob = { 0, 2, -2, 0, 2, -2 };
        ArtClip BowClip(string kind)
        {
            if(!UsesBowReady) return art?.Clip(kind);
            var cached=kind=="idle" ? bowIdle : kind=="move" ? bowMove : bowRanged;
            if(cached!=null) return cached;
            var all=Resources.LoadAll<Sprite>("ArtExtra/tea_sommelier_bow_ready");
            var selected=new System.Collections.Generic.List<Sprite>();
            foreach(var frame in all) if(frame.name.StartsWith("bow_"+kind+"_right_",System.StringComparison.Ordinal)) selected.Add(frame);
            selected.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));
            if(selected.Count==0) return art?.Clip(kind);
            var original=art.Clip(kind);
            cached=new ArtClip {name="bow_"+kind,frames=selected.ToArray(),fps=original!=null ? original.fps : 10f,loop=kind!="ranged"};
            if(kind=="idle") bowIdle=cached; else if(kind=="move") bowMove=cached; else bowRanged=cached;
            return cached;
        }

        SpriteRenderer body;
        SpriteAnimator animator;
        ArtAsset art;
        Transform weaponPivot;
        SpriteRenderer weapon;
        Color baseColor = Color.white;
        float playerRadius;

        public ArtAsset BodyArt => art;
        public SpriteRenderer Body => body;
        public SpriteRenderer Weapon => weapon;
        public SpriteRenderer Shadow { get; private set; }
        public bool UsesBodyArt => art != null;
        public bool WeaponUsesProvisionalAnchor { get; private set; }

        public static PlayerVisual Create(Transform root, float radius, Color placeholderColor, string bodyArtId)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(root, false);
            var v = go.AddComponent<PlayerVisual>();
            v.owner = root.GetComponent<PlayerController>();
            v.Build(radius, placeholderColor, bodyArtId);
            return v;
        }

        void Build(float radius, Color placeholderColor, string bodyArtId)
        {
            playerRadius = radius;
            this.bodyArtId = bodyArtId;
            art = ArtLibrary.Get(bodyArtId);
            if (art != null && art.Clip("idle") == null) art = null;

            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = SortOrders.Actor;

            body = gameObject.AddComponent<SpriteRenderer>();
            body.sortingOrder = 0;
            if (art != null)
            {
                transform.localPosition = new Vector3(0f, -radius, 0f); // 발 피벗을 판정 원 바닥에
                transform.localScale = Vector3.one * art.BodyVisualScale;
                animator = gameObject.AddComponent<SpriteAnimator>();
                animator.Bind(body);
                animator.Play(art.Clip("idle"));
                baseColor = Color.white;
            }
            else
            {
                body.sprite = SpriteFactory.Capsule;
                transform.localScale = Vector3.one * radius * 2f;
                baseColor = placeholderColor;
            }
            body.color = baseColor;

            // 그림자는 정렬 그룹 밖(루트 자식)에 둔다 — 항상 장판·개체보다 아래(04 §9).
            Shadow = GroundShadow.Attach(transform.parent, new Vector2(0f, -radius), radius * 2.4f);
        }

        /// <summary>04 §8 장착용 무기 아트(weapon_&lt;id&gt;, 오른쪽 방향, grip anchor)를 손에 든다. 길이는 manifest
        /// targetVisualLengthRelativeToPlayer(없으면 lengthRatio). 없으면 false — 호출부가 기존 아이콘 경로로.</summary>
        public bool AttachWeaponArt(string weaponArtId, float lengthRatio)
        {
            var w = ArtLibrary.Get(weaponArtId);
            var sprite = w?.FirstSprite;
            if (sprite == null) return false;
            if (weaponPivot != null) Destroy(weaponPivot.gameObject);
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);
            var spriteGo = new GameObject("Weapon");
            spriteGo.transform.SetParent(weaponPivot, false);
            weapon = spriteGo.AddComponent<SpriteRenderer>();
            weapon.sprite = HeldWeaponSprite(weaponArtId, sprite);
            weapon.sortingOrder = 1;

            float ratio = w.targetVisualLengthRelativeToPlayer > 0f ? w.targetVisualLengthRelativeToPlayer : lengthRatio;
            // 2026-10-09 사용자 시각 피드백: 손에 든 주무기 활이 몸통을 덮을 만큼 컸다(0.7) — 표시 길이만 줄인다(판정·사거리 무관, 원본 PNG 보존).
            if (weaponArtId == "weapon_bow") ratio = Mathf.Min(ratio, HeldBowLengthRatio);
            float playerHeight = art != null ? art.targetBodyHeightWorld : playerRadius * 2f;
            float parentScale = Mathf.Max(0.0001f, Mathf.Abs(transform.localScale.x));
            // 시각 길이 축 = 알파 영역의 긴 변(레이피어·검·총은 가로, 오른쪽으로 열린 활은 세로).
            float s = playerHeight * ratio / Mathf.Max(0.01f, HeldWeaponContentLongestWorld(w, weaponArtId)) / parentScale;
            // 기존 활 원본은 활대가 왼쪽, 시위가 오른쪽이다. 장착 시 활대가 전방을 향하도록 보정한다.
            Vector3 weaponScale = HeldWeaponScale(weaponArtId, s);
            spriteGo.transform.localScale = weaponScale;
            // grip anchor를 손 위치(WeaponPivot 원점)에 맞춘다.
            Vector2 gripLocal = w.TryGetAnchor(w.Main.name, 0, "grip", out var gpx) ? w.AnchorToLocal(gpx) : Vector2.zero;
            spriteGo.transform.localPosition = -Vector2.Scale(gripLocal, (Vector2)weaponScale);
            WeaponArtId = weaponArtId;
            if(UsesBowReady) animator.Play(BowClip("idle"),true);
            UpdateWeaponAnchor();
            return true;
        }

        /// <summary>07:117 하이브리드 보조 활(09 weapon_tea_bow, grip anchor) — 주무기와 별도로 몸 뒤쪽에 든다.
        /// 길이는 manifest targetVisualLengthRelativeToPlayer. 아트가 없으면 false.</summary>
        public bool AttachSecondaryWeaponArt(string weaponArtId)
        {
            var w = ArtLibrary.Get(weaponArtId);
            var sprite = w?.FirstSprite;
            if (sprite == null) return false;
            var existing = transform.Find("SecondaryPivot");
            if (existing != null) Destroy(existing.gameObject);
            var pivot = new GameObject("SecondaryPivot").transform;
            pivot.SetParent(transform, false);
            var go = new GameObject("SecondaryWeapon");
            go.transform.SetParent(pivot, false);
            SecondaryWeapon = go.AddComponent<SpriteRenderer>();
            SecondaryWeapon.sprite = sprite;
            SecondaryWeapon.sortingOrder = -1; // 몸 뒤
            float playerHeight = art != null ? art.targetBodyHeightWorld : playerRadius * 2f;
            float ratio = w.targetVisualLengthRelativeToPlayer > 0f ? w.targetVisualLengthRelativeToPlayer : 0.7f;
            ratio = Mathf.Min(ratio, HeldBowLengthRatio); // 2026-10-09: 주무기 활과 같은 0.5(몸통을 덮지 않게)
            float parentScale = Mathf.Max(0.0001f, Mathf.Abs(transform.localScale.x));
            float s = playerHeight * ratio / Mathf.Max(0.01f, w.ContentWorldSize.y) / parentScale; // 활은 세로 길이 기준
            Vector3 weaponScale = HeldWeaponScale(weaponArtId, s);
            go.transform.localScale = weaponScale;
            Vector2 gripLocal = w.TryGetAnchor(w.Main.name, 0, "grip", out var gpx) ? w.AnchorToLocal(gpx) : Vector2.zero;
            go.transform.localPosition = -Vector2.Scale(gripLocal, (Vector2)weaponScale);
            secondaryPivot = pivot;
            secondaryArtId = weaponArtId;
            UpdateSecondaryAnchor();
            return true;
        }

        public SpriteRenderer SecondaryWeapon { get; private set; }
        static Vector3 HeldWeaponScale(string id, float scale) =>
            new Vector3(id == "weapon_bow" || id == "weapon_tea_bow" ? -scale : scale, scale, scale);
        public bool SecondaryUsesProvisionalAnchor { get; private set; }
        Transform secondaryPivot;
        string secondaryArtId;

        /// <summary>2026-10-09 사용자 피드백(활이 몸 뒤/다리 쪽에 걸려 손에 잡히지 않음): 예전에는 뒤쪽 손(offHand) anchor에 몸 뒤로
        /// 들어 대기·이동 중 활이 엉덩이~발밑에 매달려 보였다. 이제 평소에는 어깨 뒤에 비스듬히 메고(몸 뒤, 고정 위치),
        /// 실제 보조 활을 쏠 때만 앞 손(hand) anchor로 옮겨 몸체 ranged(당기기) 클립과 함께 잡고 쏜다. 그동안 주무기는 숨긴다.</summary>
        public static readonly Vector2 BackSlingPx = new Vector2(110f, 132f);
        public const float BackSlingAngle = 28f;
        float hybridDrawTimer;
        public bool HybridDrawActive => hybridDrawTimer > 0f;

        void UpdateSecondaryAnchor()
        {
            if (secondaryPivot == null) return;
            if (art == null)
            {
                secondaryPivot.localPosition = new Vector3(-0.3f, 0f, 0f);
                SecondaryUsesProvisionalAnchor = true;
                return;
            }
            if (HybridDrawActive)
            {
                var clip = animator != null ? animator.Current : null;
                int frame = animator != null ? animator.FrameIndex : 0;
                Vector2 px = ProvisionalHandPx;
                bool found = clip != null && (art.TryGetAnchor(clip.name, frame, "hand", out px) || art.TryGetAnchor(clip.name, frame, "grip", out px));
                if (!found) px = ProvisionalHandPx;
                SecondaryUsesProvisionalAnchor = !found;
                secondaryPivot.localPosition = art.AnchorToLocal(px);
                secondaryPivot.localRotation = Quaternion.identity;
                if (SecondaryWeapon != null) SecondaryWeapon.sortingOrder = 1; // 몸 앞 손에 잡는다
                return;
            }
            SecondaryUsesProvisionalAnchor = false; // 등에 멘 고정 위치(설계값)
            secondaryPivot.localPosition = art.AnchorToLocal(BackSlingPx);
            secondaryPivot.localRotation = Quaternion.Euler(0f, 0f, BackSlingAngle);
            if (SecondaryWeapon != null) SecondaryWeapon.sortingOrder = -1; // 몸 뒤
        }

        /// <summary>보조 활 발사 순간: 몸체 ranged 클립을 재생하고 활을 앞 손으로 옮긴다(판정/발사 간격과 무관한 시각 전용).</summary>
        public void PlayHybridDraw()
        {
            if (SecondaryWeapon == null || defeated) return;
            PlayAction("ranged");
            hybridDrawTimer = Mathf.Max(0.3f, actionTimer);
            if (weapon != null) weapon.enabled = false;
            SyncToCurrentFrame();
        }

        void EndHybridDraw()
        {
            if (hybridDrawTimer <= 0f) return;
            hybridDrawTimer = 0f;
            if (weapon != null && !swordSwingActive) weapon.enabled = true;
        }

        /// <summary>보조 활 아트의 arrow(없으면 muzzle) anchor 월드 위치 — 하이브리드 화살 발사 지점. 없으면 false.</summary>
        public bool TryGetSecondaryLaunchWorld(out Vector2 world)
        {
            world = default;
            var w = ArtLibrary.Get(secondaryArtId);
            if (w == null || SecondaryWeapon == null) return false;
            if (!w.TryGetAnchor(w.Main.name, 0, "arrow", out var px) && !w.TryGetAnchor(w.Main.name, 0, "muzzle", out px)) return false;
            world = SecondaryWeapon.transform.TransformPoint(w.AnchorToLocal(px));
            return true;
        }

        /// <summary>장착 무기 아트의 muzzle anchor 월드 위치(총구/화살 시작). facing은 이번 프레임 바라볼 방향 —
        /// 반전이 아직 Tick에 반영되지 않았을 수 있어 직접 맞춘다. 없으면 false.</summary>
        public bool TryGetMuzzleWorld(float facing, out Vector2 world)
        {
            world = default;
            var w = ArtLibrary.Get(WeaponArtId);
            if (w == null || weapon == null || !w.TryGetAnchor(w.Main.name, 0, "muzzle", out var px)) return false;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (facing < 0f ? -1f : 1f);
            transform.localScale = s;
            world = weapon.transform.TransformPoint(w.AnchorToLocal(px));
            return true;
        }

        /// <summary>장착 무기로 쓰인 04 §8 아트 ID(없으면 기존 아이콘 사용).</summary>
        public string WeaponArtId { get; private set; }

        /// <summary>무기 아이콘을 손에 든다. lengthRatio = 무기 시각 길이 / 플레이어 높이(04 §8: 레이피어0.8 검0.65 총0.4 활0.7).</summary>
        public void AttachWeapon(Sprite sprite, float lengthRatio, bool isBow)
        {
            if (sprite == null) return;
            if (weaponPivot != null) Destroy(weaponPivot.gameObject);
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);

            var spriteGo = new GameObject("Weapon");
            spriteGo.transform.SetParent(weaponPivot, false);
            weapon = spriteGo.AddComponent<SpriteRenderer>();
            weapon.sprite = sprite;
            weapon.sortingOrder = 1; // 그룹 안에서 몸 앞

            // 기존 아이콘은 45° 대각(손잡이 좌하단, 끝 우상단) — 끝이 오른쪽을 향하게 돌린다. 활은 시위가 세로가 되게.
            float angle = isBow ? 45f : -45f;
            spriteGo.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            float playerHeight = art != null ? art.targetBodyHeightWorld : playerRadius * 2f;
            float spriteDiagonal = sprite.bounds.size.magnitude * 0.88f; // 아이콘 여백 제외 대략
            float worldLength = playerHeight * lengthRatio;
            float parentScale = Mathf.Max(0.0001f, transform.localScale.x);
            float s = worldLength / Mathf.Max(0.01f, spriteDiagonal) / parentScale;
            spriteGo.transform.localScale = Vector3.one * s;

            // 손잡이(아이콘 좌하단 10%)를 손 위치에 둔다. 활은 가운데를 잡는다.
            Vector2 gripLocal = isBow ? Vector2.zero : new Vector2(-0.4f, -0.4f) * sprite.bounds.size.x;
            Vector2 rotated = Quaternion.Euler(0f, 0f, angle) * gripLocal;
            spriteGo.transform.localPosition = -rotated * s;
            UpdateWeaponAnchor();
        }

        void UpdateWeaponAnchor()
        {
            if (weaponPivot == null || swordSwingActive) return; // 휘두르기 프레임에는 검이 몸체 그림에 들어 있다
            if (art == null)
            {
                weaponPivot.localPosition = new Vector3(0.3f, 0f, 0f);
                WeaponUsesProvisionalAnchor = true;
                return;
            }
            Vector2 px = ProvisionalHandPx;
            var clip = animator != null ? animator.Current : null;
            int frame = animator != null ? animator.FrameIndex : 0;
            bool found = clip != null && (art.TryGetAnchor(clip.name, frame, "hand", out px) ||
                                          art.TryGetAnchor(clip.name, frame, "grip", out px));
            if(UsesBowReady && clip!=null && clip.name.StartsWith("bow_",System.StringComparison.Ordinal))
            {
                px=clip.name=="bow_move" ? new Vector2(198f,153f-BowMoveBob[Mathf.Clamp(frame,0,5)])
                    : clip.name=="bow_ranged" && frame==2 ? new Vector2(201f,154f) : new Vector2(198f,153f);
                found=true;
            }
            if (!found) px = ProvisionalHandPx;
            WeaponUsesProvisionalAnchor = !found;
            weaponPivot.localPosition = art.AnchorToLocal(px);
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, HeldWeaponAngle(WeaponArtId, clip != null ? clip.name : null, frame));
        }

        /// <summary>2026-10-09 사용자 피드백(무기·캐릭터 연결이 부자연스러움): 예전에는 모든 무기가 모든 클립에서 수평 고정이라 대기/이동 중
        /// 칼날·총신이 엉덩이에서 옆으로 뻗고, 피격·복귀 프레임에서도 수평이었다. grip을 손 anchor에 고정한 채(위치는 그대로) grip 기준으로만
        /// 무기·클립·프레임별 각도를 준다(+는 앞쪽 끝이 위). 좌우 반전은 부모 배율로 그대로 따라간다. 판정/발사 간격은 무관.</summary>
        public static float HeldWeaponAngle(string weaponArtId, string clip, int frame)
        {
            switch (weaponArtId)
            {
                case "weapon_rapier":
                    return clip == "melee" ? Pick(frame, -12f, 12f, 0f, -15f) : clip == "hurt" ? -35f : clip == "defeat" ? -45f : -22f;
                case "weapon_sword":
                    return clip == "melee" ? Pick(frame, -15f, 15f, 0f, -20f) : clip == "hurt" ? -40f : clip == "defeat" ? -45f : -30f;
                case "weapon_gun":
                    return clip == "ranged" ? Pick(frame, 0f, 0f, 0f, -12f) : clip == "hurt" ? -20f : clip == "defeat" ? -30f : -10f;
                case "weapon_bow":
                    if(clip!=null && clip.StartsWith("bow_",System.StringComparison.Ordinal)) return 0f;
                    return clip == "ranged" ? Pick(frame, 0f, 0f, 0f, -15f) : clip == "hurt" ? -20f : clip == "defeat" ? -30f : -12f;
                default:
                    return 0f;
            }
        }

        static float Pick(int frame, params float[] values) => values[Mathf.Clamp(frame, 0, values.Length - 1)];

        float actionTimer;
        bool defeated;
        string bodyArtId;
        bool swordSwingActive;
        static ArtClip swordSwingClip;

        /// <summary>2026-10-09 사용자 요청 검 휘두르기(PixelLab/Aseprite, ArtSource/melee-swing-2026-10-09): 실제 타격마다
        /// 휘두르기 정점(03)부터 복귀(07)까지 30fps로 재생한다. 05(대각선 베기) 타격 프레임은 재생 시작 0.067초 뒤.</summary>
        public const int SwordSwingStartFrame = 3, SwordSwingImpactFrame = 5;
        public const float SwordSwingFps = 30f;
        public static float SwordSwingImpactDelay => (SwordSwingImpactFrame - SwordSwingStartFrame) / SwordSwingFps;
        public bool SwordSwingActive => swordSwingActive;

        static ArtClip SwordSwingClip()
        {
            if (swordSwingClip != null && swordSwingClip.frames.Length > 0 && swordSwingClip.frames[0] != null) return swordSwingClip;
            var all = Resources.LoadAll<Sprite>("ArtExtra/tea_sommelier_sword_swing");
            if (all == null || all.Length < 8) return null;
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            var frames = new Sprite[all.Length - SwordSwingStartFrame];
            System.Array.Copy(all, SwordSwingStartFrame, frames, 0, frames.Length);
            swordSwingClip = new ArtClip { name = "sword_swing", fps = SwordSwingFps, loop = false, frames = frames };
            return swordSwingClip;
        }

        /// <summary>티 소믈리에 검 전용 휘두르기 몸체 클립(검 포함 그림). 다른 몸체/무기나 아트가 없으면 false — 호출부가 기존 melee로.
        /// 판정·공속은 바꾸지 않는다(시각 전용). 재생 동안 별도 손 무기 렌더러를 숨긴다.</summary>
        public bool PlaySwordSwing()
        {
            if (art == null || animator == null || defeated || bodyArtId != "tea_sommelier" || WeaponArtId != "weapon_sword") return false;
            var clip = SwordSwingClip();
            if (clip == null) return false;
            animator.Play(clip, true);
            actionTimer = clip.Duration;
            swordSwingActive = true;
            if (weapon != null) weapon.enabled = false;
            return true;
        }

        void EndSwordSwing()
        {
            if (!swordSwingActive) return;
            swordSwingActive = false;
            if (weapon != null) weapon.enabled = true;
        }

        /// <summary>04 §6 A세트 melee/ranged/hurt 클립(1회). 무기 발사 이벤트가 부르며 판정 시간은 바꾸지 않는다. 클립이 없으면 false.</summary>
        public bool PlayAction(string clipName, float holdSeconds = 0f)
        {
            var clip = UsesBowReady && clipName=="ranged" ? BowClip("ranged") : art?.Clip(clipName);
            if (clip == null || animator == null || defeated) return false;
            EndSwordSwing();
            EndHybridDraw();
            animator.Play(clip, true);
            actionTimer = Mathf.Max(clip.Duration, holdSeconds);
            return true;
        }

        public void PlayHurt() => PlayAction("hurt", 0.08f);

        /// <summary>04 §6 defeat(1회, 마지막 프레임 유지). 클립이 없으면 false.</summary>
        public bool PlayDefeat()
        {
            var clip = art?.Clip("defeat");
            if (clip == null || animator == null) return false;
            EndSwordSwing();
            EndHybridDraw();
            defeated = true;
            // 사망은 항상 런 종료(timeScale 0)로 이어진다 — 실시간으로 재생해야 결과 화면 뒤에서 defeat가 첫 프레임에 멈추지 않는다
            // (2026-10-08 r14 모션 녹화: defeat_00에 고정된 것을 확인).
            animator.UseUnscaledTime = true;
            animator.Play(clip, true);
            return true;
        }

        PlayerController owner;

        /// <summary>모든 Update(몸체 SpriteAnimator 프레임 진행, 무기의 PlayAction/SetFacing) 뒤에 좌우 반전과 손 anchor를 다시 맞춘다.
        /// Tick만으로는 같은 프레임에 바뀐 몸체 프레임/방향을 무기가 한 프레임 늦게 따라갔다(2026-10-08 r14 모션 녹화 CSV).</summary>
        void LateUpdate() => SyncToCurrentFrame();

        public void SyncToCurrentFrame()
        {
            if (owner != null && !defeated)
            {
                var s = transform.localScale;
                s.x = Mathf.Abs(s.x) * (owner.Facing < 0f ? -1f : 1f);
                transform.localScale = s;
            }
            UpdateWeaponAnchor();
            UpdateSecondaryAnchor();
        }

        /// <summary>매 프레임 PlayerController가 호출: 방향 반전(Visual만), 이동/대기 클립, 피격 점멸 색.</summary>
        public void Tick(float facing, bool moving, bool blinkHurt)
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (facing < 0f ? -1f : 1f);
            transform.localScale = s;

            if (actionTimer > 0f) actionTimer -= Time.deltaTime;
            if (hybridDrawTimer > 0f)
            {
                hybridDrawTimer -= Time.deltaTime;
                if (hybridDrawTimer <= 0f) { hybridDrawTimer = 0.0001f; EndHybridDraw(); }
            }
            if (animator != null && !defeated && actionTimer <= 0f)
            {
                EndSwordSwing();
                var next = moving ? BowClip("move") ?? BowClip("idle") : BowClip("idle");
                animator.Play(next);
            }
            body.color = blinkHurt ? new Color(1f, 0.4f, 0.4f) : baseColor;
            UpdateWeaponAnchor();
            UpdateSecondaryAnchor();
        }
    }
}
