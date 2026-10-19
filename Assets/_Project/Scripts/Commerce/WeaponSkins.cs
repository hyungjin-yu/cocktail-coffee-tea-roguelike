using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>49(2026-10-13) 무기 외형 스킨 1종. 무료 선택 — 결제·상품·소유권과 무관하고 능력치·판정·발사에 관여하지 않는다.</summary>
    public sealed class WeaponSkinDefinition
    {
        public string id;
        public WeaponKind weapon;
        public string displayName;
        /// <summary>기본 외형과 실제로 달라지는 색·재질 한 줄.</summary>
        public string difference;
        public string Folder => WeaponSkinCatalog.ResourceRoot + id + "/";
        /// <summary>손에 드는 무기(기본 무기와 같은 256 캔버스·pivot·grip).</summary>
        public string HeldResource => Folder + id + "_held";
        /// <summary>검 전용: 휘두르기 몸체 프레임(검이 그림에 포함). jade=비취 티 소믈리에 몸체 스킨용.</summary>
        public string SwingFolder(bool jade) => Folder + (jade ? "swing_jade" : "swing_base");
        /// <summary>79 검 전용: 무기-only 휘두르기 프레임(03~07, Aseprite에서 승인 마스크로 export). 신규 몸체 스킨 위에 겹친다.</summary>
        public string SwingOverlayFolder => Folder + "swing_overlay";
    }

    public static class WeaponSkinCatalog
    {
        public const string ResourceRoot = "WeaponSkins/";

        public static readonly WeaponSkinDefinition[] All =
        {
            // 이름: ArtSource/skins-aseprite-pixellab-2026-10-13 manifest displayMetadata. 차이 문구는 임포트한 최종 PNG를 확대해 실제 색을 확인해 맞췄다.
            new WeaponSkinDefinition { id = "weaponskin_rapier_01", weapon = WeaponKind.Rapier, displayName = "자수정 레이피어",
                difference = "분홍 날 → 연보라 자수정\n초록 테두리 → 연한 금빛, 손잡이 → 보라" },
            new WeaponSkinDefinition { id = "weaponskin_gun_01", weapon = WeaponKind.Gun, displayName = "구리빛 총",
                difference = "은빛·청록 총신 → 구리와 황동\n보라 장식 포인트" },
            new WeaponSkinDefinition { id = "weaponskin_sword_01", weapon = WeaponKind.Sword, displayName = "푸른 강철 검",
                difference = "분홍 날 → 푸른 강철\n초록 코등이·테두리 → 금빛" },
            new WeaponSkinDefinition { id = "weaponskin_bow_01", weapon = WeaponKind.Bow, displayName = "달빛 활",
                difference = "붉은 활대·흰 시위 → 남색·푸른빛\n양 끝 → 크림 금빛" },
        };

        public static readonly WeaponKind[] Weapons = { WeaponKind.Rapier, WeaponKind.Gun, WeaponKind.Sword, WeaponKind.Bow };

        public static WeaponSkinDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        public static WeaponSkinDefinition ForWeapon(WeaponKind weapon)
        {
            foreach (var d in All) if (d.weapon == weapon) return d;
            return null;
        }

        public static string WeaponLabel(WeaponKind w) => CardAdvisor.WeaponName(w);

        /// <summary>기본 무기가 실제로 손에 드는 스프라이트(활은 v2).</summary>
        public static Sprite BaseHeldSprite(WeaponKind weapon)
        {
            var id = "weapon_" + weapon.ToString().ToLowerInvariant();
            var art = ArtLibrary.Get(id);
            var v1 = art != null ? art.FirstSprite : null;
            return PlayerVisual.HeldWeaponSprite(id, v1) ?? Resources.Load<Sprite>(WeaponPreset.For(weapon).iconResourcePath);
        }

        /// <summary>스킨 무기 스프라이트. 파일이 없거나 기본 무기와 캔버스·pivot이 다르면(손 위치가 어긋남) null.</summary>
        public static Sprite HeldSprite(WeaponSkinDefinition def)
        {
            if (def == null) return null;
            var s = Resources.Load<Sprite>(def.HeldResource);
            if (s == null) return null;
            var b = BaseHeldSprite(def.weapon);
            if (b != null && (b.rect.size != s.rect.size || (b.pivot - s.pivot).sqrMagnitude > 0.01f ||
                Mathf.Abs(b.pixelsPerUnit - s.pixelsPerUnit) > 0.01f)) return null;
            return s;
        }

        public static bool ArtReady(WeaponSkinDefinition def) => HeldSprite(def) != null;
    }

    /// <summary>
    /// 2026-10-13 무기 스킨 기하 계약(새 스킨을 계속 추가해도 스킨별 보정 없이 같은 배치가 되도록):
    /// - 스킨 PNG는 기본 무기 PNG와 캔버스·pivot·PPU가 같고(런타임 HeldSprite가 확인) 알파 실루엣이 같아야 한다(이 클래스, 임포트 시 강제).
    /// - 그러면 기본 무기 manifest의 grip/muzzle/string anchor와 몸체 프레임별 hand anchor로 정한 배치·방향·발사 위치를 그대로 공유한다.
    /// - 실루엣이 다른 스킨은 임포트에서 실패한다(지금은 스킨별 기하 선언을 지원하지 않음 — 다른 모양은 새 무기 아트로 manifest부터 만든다).
    /// </summary>
    public static class WeaponSkinGeometry
    {
        public static Texture2D LoadPng(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!File.Exists(path) || !t.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(t); return null; }
            return t;
        }

        /// <summary>알파&gt;0 영역의 경계(좌하단 원점 px, 끝 미포함). 비어 있으면 크기 0.</summary>
        public static RectInt AlphaBounds(Texture2D t)
        {
            var px = t.GetPixels32();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                    if (px[y * t.width + x].a > 0) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
            return maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>같은 크기이고 알파&gt;0 마스크가 픽셀 단위로 같으면 true. 다르면 detail에 첫 차이.</summary>
        public static bool SameSilhouette(Texture2D a, Texture2D b, out string detail)
        {
            detail = null;
            if (a == null || b == null) { detail = "missing texture"; return false; }
            if (a.width != b.width || a.height != b.height) { detail = $"size {a.width}x{a.height} vs {b.width}x{b.height}"; return false; }
            var pa = a.GetPixels32(); var pb = b.GetPixels32();
            for (int i = 0; i < pa.Length; i++)
                if ((pa[i].a > 0) != (pb[i].a > 0)) { detail = $"alpha differs at ({i % a.width},{i / a.width})"; return false; }
            return true;
        }
    }

    [Serializable]
    public class WeaponSkinChoice
    {
        public string weapon;
        public string skinId;
    }

    /// <summary>weapon_skins.json — 무기별 장착 외형(없으면 기본). commerce.json(몸체 스킨 소유·장착)과 분리된 파일.</summary>
    [Serializable]
    public class WeaponSkinState
    {
        public int schemaVersion = 1;
        public List<WeaponSkinChoice> equipped = new List<WeaponSkinChoice>();

        /// <summary>알 수 없는 id·다른 무기 id는 기본(null)으로 본다.</summary>
        public string EquippedFor(WeaponKind weapon)
        {
            if (equipped == null) return null;
            foreach (var c in equipped)
            {
                if (c == null || c.weapon != weapon.ToString()) continue;
                var def = WeaponSkinCatalog.Find(c.skinId);
                return def != null && def.weapon == weapon ? def.id : null;
            }
            return null;
        }

        public void SetEquipped(WeaponKind weapon, string skinIdOrNull)
        {
            if (equipped == null) equipped = new List<WeaponSkinChoice>();
            equipped.RemoveAll(c => c == null || c.weapon == weapon.ToString());
            var def = WeaponSkinCatalog.Find(skinIdOrNull);
            if (def != null && def.weapon == weapon) equipped.Add(new WeaponSkinChoice { weapon = weapon.ToString(), skinId = def.id });
        }

        public void Normalize()
        {
            if (equipped == null) equipped = new List<WeaponSkinChoice>();
            equipped.RemoveAll(c => c == null || !Enum.TryParse<WeaponKind>(c.weapon, out var w) ||
                WeaponSkinCatalog.Find(c.skinId) == null || WeaponSkinCatalog.Find(c.skinId).weapon != w);
        }
    }

    /// <summary>무기 외형 선택 저장. 경로는 PermanentProgress 저장 폴더(-cct-data-dir·테스트 격리를 따른다). 원자적 저장(.tmp→검증→Replace).</summary>
    public static class WeaponSkinStore
    {
        public const string FileName = "weapon_skins.json";
        static WeaponSkinState current;
        static string currentPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { current = null; currentPath = null; }

        public static string DefaultPath()
        {
            var dir = Path.GetDirectoryName(PermanentProgress.ActiveSavePath);
            return string.IsNullOrEmpty(dir) ? FileName : Path.Combine(dir, FileName);
        }

        /// <summary>현재 저장 폴더의 상태(폴더가 바뀌면 다시 읽는다).</summary>
        public static WeaponSkinState Current
        {
            get
            {
                var path = DefaultPath();
                if (current == null || currentPath != path) { current = Load(path); currentPath = path; }
                return current;
            }
        }

        public static string EquippedFor(WeaponKind weapon) => Current.EquippedFor(weapon);

        /// <summary>장착 변경 후 즉시 저장. 저장 실패 시 false(메모리 상태는 바뀐 채 — 다음 저장에서 재시도).</summary>
        public static bool Equip(WeaponKind weapon, string skinIdOrNull)
        {
            var state = Current;
            state.SetEquipped(weapon, skinIdOrNull);
            return Save(currentPath, state);
        }

        public static WeaponSkinState Load(string path)
        {
            var state = TryRead(path) ?? TryRead(path + ".bak") ?? new WeaponSkinState();
            state.Normalize();
            return state;
        }

        static WeaponSkinState TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (!JsonSyntax.IsCompleteObject(json)) return null;
                return JsonUtility.FromJson<WeaponSkinState>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WeaponSkin] {FileName} 읽기 실패: {e.GetType().Name}");
                return null;
            }
        }

        public static bool Save(string path, WeaponSkinState state)
        {
            if (state == null || string.IsNullOrEmpty(path)) return false;
            var tmp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(state, true));
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (TryRead(tmp) == null) throw new IOException("weapon_skins.tmp 검증 실패");
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", true);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WeaponSkin] {FileName} 저장 실패: {e.GetType().Name}");
                return false;
            }
        }
    }

    /// <summary>
    /// 런 시작 시 장착 무기 외형을 실제 손 무기에 적용. 스프라이트만 바꾸며 위치·회전·크기·판정·발사 anchor는 기본 무기 그대로
    /// (같은 캔버스·pivot이 아니면 적용하지 않음). 검은 휘두르기 몸체 프레임도 같은 이름의 스킨 프레임으로 바꾼다.
    /// </summary>
    [DefaultExecutionOrder(5001)] // SkinSpriteSwapper(5000, 몸체 스킨) 다음 — 비취 몸체의 검 프레임도 덮는다.
    public class WeaponSkinApplier : MonoBehaviour
    {
        public const string SwingPrefix = "tea_sommelier_sword_swing_";
        SpriteRenderer body;
        Dictionary<string, Sprite> baseSwing, jadeSwing, overlaySwing;
        /// <summary>79: 신규 몸체 스킨 + 검 외형일 때 휘두르기 동안만 몸 위에 그리는 무기-only 렌더러(몸체 프레임은 스킨 그대로).</summary>
        SpriteRenderer overlay;
        public SpriteRenderer SwingOverlay => overlay;
        HashSet<Sprite> own;
        SkinSpriteSwapper bodySkin;

        public string SkinId { get; private set; }

        public static WeaponSkinApplier Apply(PlayerVisual visual, WeaponKind weapon) =>
            Apply(visual, weapon, WeaponSkinCatalog.Find(SafeEquipped(weapon)));

        static string SafeEquipped(WeaponKind weapon)
        {
            try { return WeaponSkinStore.EquippedFor(weapon); }
            catch (Exception e) { Debug.LogWarning($"[WeaponSkin] 선택 읽기 실패 — 기본 외형: {e.GetType().Name}"); return null; }
        }

        public static WeaponSkinApplier Apply(PlayerVisual visual, WeaponKind weapon, WeaponSkinDefinition def)
        {
            if (visual == null || def == null || def.weapon != weapon || visual.Weapon == null) return null;
            var held = WeaponSkinCatalog.HeldSprite(def);
            if (held == null) return null; // 아트 없음/기하 불일치 — 기본 외형
            var current = visual.Weapon.sprite;
            if (current != null && (current.rect.size != held.rect.size || (current.pivot - held.pivot).sqrMagnitude > 0.01f)) return null;
            visual.Weapon.sprite = held;
            var applier = visual.gameObject.AddComponent<WeaponSkinApplier>();
            applier.SkinId = def.id;
            applier.body = visual.Body;
            applier.bodySkin = visual.Body != null ? visual.Body.GetComponent<SkinSpriteSwapper>() : null;
            if (weapon == WeaponKind.Sword)
            {
                applier.baseSwing = Load(def.SwingFolder(false));
                applier.jadeSwing = Load(def.SwingFolder(true));
                applier.own = new HashSet<Sprite>(applier.baseSwing.Values);
                applier.own.UnionWith(applier.jadeSwing.Values);
                applier.overlaySwing = Load(def.SwingOverlayFolder);
            }
            return applier;
        }

        static Dictionary<string, Sprite> Load(string folder)
        {
            var map = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var s in Resources.LoadAll<Sprite>(folder))
                if (s != null && s.name.StartsWith(SwingPrefix, StringComparison.Ordinal)) map[s.name] = s;
            return map;
        }

        void LateUpdate()
        {
            ApplySwing();
            UpdateOverlay();
        }

        /// <summary>79: 신규 몸체 스킨(비취 아님)일 때 현재 몸체가 검 휘두르기 프레임이면 같은 이름의 무기-only 프레임을 몸 위에 표시,
        /// 아니면(대기·이동·피격·쓰러짐·다른 무기) 숨긴다. 몸체와 같은 Transform 자식 → 좌우 반전·위치·스케일을 그대로 따른다.
        /// 오버레이 아트가 없으면 아무것도 그리지 않는다(신규 스킨 자체 프레임의 기본 검이 보임 — 보고된 한계).</summary>
        public bool UpdateOverlay()
        {
            if (body == null) return false;
            if (bodySkin == null) bodySkin = body.GetComponent<SkinSpriteSwapper>();
            var s = body.sprite;
            bool newBodySkin = bodySkin != null && bodySkin.ProductId != SkinCatalog.TeaJade;
            Sprite frame = null;
            if (newBodySkin && body.enabled && overlaySwing != null && s != null && s.name.StartsWith(SwingPrefix, StringComparison.Ordinal))
                overlaySwing.TryGetValue(s.name, out frame);
            if (frame != null && (frame.rect.size != s.rect.size || (frame.pivot - s.pivot).sqrMagnitude > 0.01f)) frame = null; // 기하 불일치면 그리지 않음
            if (frame == null)
            {
                if (overlay != null) overlay.enabled = false;
                return false;
            }
            if (overlay == null)
            {
                var go = new GameObject("SwordSwingWeaponOverlay");
                go.transform.SetParent(body.transform, false);
                overlay = go.AddComponent<SpriteRenderer>();
                overlay.sortingOrder = body.sortingOrder + 2; // 같은 SortingGroup 안에서 몸·손 무기(1) 앞
            }
            overlay.sprite = frame;
            overlay.color = body.color; // 피격 깜빡임·투명도 동일
            overlay.flipX = body.flipX;
            overlay.enabled = true;
            return true;
        }

        /// <summary>현재 몸체가 검 휘두르기 프레임이면 스킨 프레임으로. 바꿨으면 true.</summary>
        public bool ApplySwing()
        {
            if (body == null || own == null) return false;
            var s = body.sprite;
            if (s == null || own.Contains(s) || !s.name.StartsWith(SwingPrefix, StringComparison.Ordinal)) return false;
            if (bodySkin == null) bodySkin = body.GetComponent<SkinSpriteSwapper>(); // 몸체 스킨이 나중에 붙은 경우(QA 강제 장착 등)
            // 79: 신규 몸체 스킨은 자기 휘두르기 프레임(몸체+검 그림)을 가진다 — 기본색 검 휘두르기로 덮으면 기본 복장이 번쩍인다.
            if (bodySkin != null && bodySkin.ProductId != SkinCatalog.TeaJade) return false;
            bool jade = bodySkin != null && bodySkin.ProductId == SkinCatalog.TeaJade;
            var map = jade ? jadeSwing : baseSwing;
            if (map == null || !map.TryGetValue(s.name, out var skin) || skin == null) return false;
            if (skin.rect.size != s.rect.size || (skin.pivot - s.pivot).sqrMagnitude > 0.01f) return false;
            body.sprite = skin;
            return true;
        }
    }
}
