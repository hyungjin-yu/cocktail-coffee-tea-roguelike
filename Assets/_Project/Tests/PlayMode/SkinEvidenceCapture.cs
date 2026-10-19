using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 49 시각 증거(창 없음): 환경 변수 CCT_EVIDENCE_DIR이 있을 때만 실행. 카메라를 RenderTexture로 렌더해 PNG로 저장한다
    /// — 보이는 창·OS 입력·포커스 사용 없음. 스킨 상점(캐릭터 비교/무기 탭), 실제 런 카드 추천 화면, 네 무기 전투(일반 스폰·실제 공격).
    /// 강제 경로(보고 표기): 결제 꺼진 상점, 무기 외형 장착 버튼 onClick 직접 호출, AddIngredient로 카드 보상 유도, 관찰 중 최대 체력 보강,
    /// 비취 몸체 스킨 QA 강제 장착(SkinSpriteSwapper.Attach — 소유권 우회, 화면 확인용).
    /// </summary>
    public class SkinEvidenceCapture
    {
        class NullPersister : ICommercePersister
        {
            public CommerceState Load() { var s = new CommerceState(); s.Normalize(); return s; }
            public bool Save(CommerceState state) => true;
        }

        string dir;
        readonly StringBuilder log = new StringBuilder();
        string tempPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_evidence_{Guid.NewGuid():N}", "save.json");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false));
            Time.timeScale = 1f;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            yield return null;
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[Evidence] " + s); }

        /// <summary>카메라(없으면 임시)와 캔버스들을 w×h RenderTexture에 렌더해 PNG 저장. 캔버스는 화면 크기 1920×1080 기준과 같은 배율.</summary>
        IEnumerator Capture(Camera cam, IList<Canvas> canvases, int w, int h, string name) => CaptureTo(dir, Line, cam, canvases, w, h, name);

        /// <summary>79 증거(Skins10EvidenceCapture)도 같은 창 없는 렌더 경로를 쓴다.</summary>
        internal static IEnumerator CaptureTo(string dir, Action<string> Line, Camera cam, IList<Canvas> canvases, int w, int h, string name)
        {
            yield return null;
            // 배치 모드는 WaitForEndOfFrame이 없다 — 렌더 직전에 LateUpdate의 스킨 교체를 같은 순서로 한 번 더 적용한다.
            foreach (var s in UnityEngine.Object.FindObjectsByType<SkinSpriteSwapper>(FindObjectsSortMode.None)) s.Apply();
            foreach (var a in UnityEngine.Object.FindObjectsByType<WeaponSkinApplier>(FindObjectsSortMode.None)) a.ApplySwing();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            bool temp = cam == null;
            if (temp)
            {
                cam = new GameObject("EvidenceCam").AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            var restore = new List<(Canvas c, RenderMode mode, Camera cam, int order, float plane, CanvasScaler.ScaleMode scaleMode, float factor, bool adaptive)>();
            foreach (var c in canvases)
            {
                if (c == null) continue;
                var scaler = c.GetComponent<CanvasScaler>();
                var adaptive = c.GetComponent<AdaptiveCanvasScaler>();
                restore.Add((c, c.renderMode, c.worldCamera, c.sortingOrder, c.planeDistance, scaler != null ? scaler.uiScaleMode : default,
                    scaler != null ? scaler.scaleFactor : 1f, adaptive != null && adaptive.enabled));
                if (adaptive != null) adaptive.enabled = false;
                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = w / 1920f; }
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                c.sortingOrder = 30000 + c.sortingOrder;
            }
            Canvas.ForceUpdateCanvases();
            ReapplyCenterBands(canvases); // 세로 띠 배치(CenterBand, LateUpdate)를 새 16:9 캔버스 크기로 즉시 다시 계산
            foreach (var c in canvases) if (c != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            var path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Line($"shot {name}.png {w}x{h}");
            UnityEngine.Object.Destroy(tex);
            cam.targetTexture = prevTarget;
            foreach (var r in restore)
            {
                r.c.renderMode = r.mode; r.c.worldCamera = r.cam; r.c.sortingOrder = r.order; r.c.planeDistance = r.plane;
                var scaler = r.c.GetComponent<CanvasScaler>();
                if (scaler != null) { scaler.uiScaleMode = r.scaleMode; scaler.scaleFactor = r.factor; }
                var adaptive = r.c.GetComponent<AdaptiveCanvasScaler>();
                if (adaptive != null) adaptive.enabled = r.adaptive;
            }
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            if (temp) UnityEngine.Object.Destroy(cam.gameObject);
        }

        static void ReapplyCenterBands(IList<Canvas> canvases)
        {
            var apply = typeof(CenterBand).GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var last = typeof(CenterBand).GetField("lastExtra", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var c in canvases)
            {
                if (c == null) continue;
                foreach (var band in c.GetComponentsInChildren<CenterBand>(true))
                {
                    last?.SetValue(band, -1f);
                    apply?.Invoke(band, null);
                }
            }
        }

        static readonly (int w, int h)[] Sizes = { (1280, 720), (1920, 1080) };

        /// <summary>2026-10-13 개인정보처리방침 버튼(제작 정보·설정)과 제목 표시 — 720·1080 렌더 증거.</summary>
        [UnityTest]
        public IEnumerator Capture_PrivacyPolicyButtons_CreditsAndSettings()
        {
            foreach (var (w, h) in Sizes)
            {
                var canvas = UiFactory.CreateCanvas(null, "EvidencePrivacy", 100);
                var credits = CreditsView.Create(canvas.transform, () => { });
                credits.Show();
                yield return null;
                yield return Capture(null, new[] { canvas }, w, h, $"privacy_credits_{w}x{h}");
                credits.Hide();
                var settings = SettingsView.Create(canvas.transform);
                settings.Show();
                yield return null;
                yield return Capture(null, new[] { canvas }, w, h, $"privacy_settings_{w}x{h}");
                UnityEngine.Object.Destroy(canvas.gameObject);
                yield return null;
            }
            var title = TitleView.Show();
            yield return null;
            yield return Capture(null, new[] { title.GetComponentInParent<Canvas>() }, 1920, 1080, "title_1920x1080");
            UnityEngine.Object.Destroy(title.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_SkinShop_CharacterAndWeaponTabs()
        {
            foreach (var (w, h) in Sizes)
            {
                var canvas = UiFactory.CreateCanvas(null, "EvidenceShop", 100);
                var commerce = new SkinCommerce(CommerceConfig.Disabled(), new UnavailableSkinStore(), null, new NullPersister(), () => 0d);
                var view = SkinShopView.Create(canvas.transform, () => { }, () => commerce);
                foreach (var weapon in WeaponSkinCatalog.Weapons) WeaponSkinStore.Equip(weapon, null);
                view.Show();
                yield return null;
                yield return Capture(null, new[] { canvas }, w, h, $"shop_character_{w}x{h}");
                foreach (var def in SkinCatalog.All)
                    Line($"body {def.productId}: base={view.BasePreviewSprite(def.productId)?.name} skin={view.PreviewSprite(def.productId)?.name} " +
                         $"artNote={view.ArtNoteVisible(def.productId)} state={view.StateText(def.productId)} diff={view.DifferenceText(def.productId)?.Replace("\n", " / ")}");
                view.ShowTab(true);
                yield return null;
                yield return Capture(null, new[] { canvas }, w, h, $"shop_weapons_default_{w}x{h}");
                foreach (var weapon in WeaponSkinCatalog.Weapons) view.WeaponEquipButton(weapon).onClick.Invoke();
                yield return null;
                yield return Capture(null, new[] { canvas }, w, h, $"shop_weapons_equipped_{w}x{h}");
                foreach (var weapon in WeaponSkinCatalog.Weapons)
                    Line($"weapon {weapon}: base={view.WeaponBasePreview(weapon)?.name} skin={view.WeaponSkinPreview(weapon)?.name} " +
                         $"state={view.WeaponStateText(weapon)} saved={WeaponSkinStore.Load(WeaponSkinStore.DefaultPath()).EquippedFor(weapon)}");
                Line("weapon_skins.json: " + File.ReadAllText(WeaponSkinStore.DefaultPath()).Replace("\n", " "));
                UnityEngine.Object.Destroy(canvas.gameObject);
                yield return null;
            }
        }

        static readonly (Job job, WeaponKind weapon)[] Combos =
        {
            (Job.Bartender, WeaponKind.Rapier), (Job.Bartender, WeaponKind.Gun),
            (Job.TeaSommelier, WeaponKind.Sword), (Job.TeaSommelier, WeaponKind.Bow),
        };

        /// <summary>2026-10-13 활 모션 수정 증거: 기본/비취 몸체 × 기본/달빛 활, 좌우 각각 대기·이동 2컷·발사·복귀·피격을
        /// 플레이어 확대 카메라로 렌더한다(원본 전신 클립 + 프레임별 hand anchor). 비취는 QA 강제 장착(소유권 우회).</summary>
        [UnityTest]
        public IEnumerator Capture_BowMotion_BodyAndBowSkinCombos()
        {
            foreach (bool jade in new[] { false, true })
            foreach (bool moonBow in new[] { false, true })
            {
                WeaponSkinStore.Equip(WeaponKind.Bow, moonBow ? "weaponskin_bow_01" : null);
                var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Bow);
                yield return null;
                var tutorial = UnityEngine.Object.FindFirstObjectByType<FirstRunTutorialView>();
                if (tutorial != null && tutorial.IsOpen) tutorial.Dismiss();
                game.GetComponentInChildren<EnemySpawner>().enabled = false;
                if (game.Director != null) game.Director.enabled = false;
                foreach (var mb in game.Player.GetComponents<MonoBehaviour>()) if (mb is PlayerProjectileWeapon) mb.enabled = false;
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
                game.Player.Heal(1f);
                // 격리: 남은 몹·적 탄을 지워 피격이 자세 캡처에 섞이지 않게 한다.
                foreach (var e in new List<Enemy>(Enemy.Active)) if (e != null) UnityEngine.Object.Destroy(e.gameObject);
                foreach (var p in UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(p.gameObject);
                var visual = game.Player.Visual;
                if (jade) SkinSpriteSwapper.Attach(visual.Body, SkinCatalog.Find(SkinCatalog.TeaJade));
                var cam = new GameObject("BowEvidenceCam").AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 1.5f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.27f, 0.27f, 0.31f);
                string tag = $"bow_{(jade ? "jade" : "base")}_{(moonBow ? "moonbow" : "basebow")}";
                IEnumerator Shot(string pose)
                {
                    var p = game.Player.Position;
                    cam.transform.position = new Vector3(p.x, p.y + 0.9f, -10f);
                    yield return Capture(cam, new Canvas[0], 480, 480, $"{tag}_{pose}");
                    Line($"{tag}_{pose}: body={visual.Body.sprite?.name} weapon={visual.Weapon.sprite?.name} provisional={visual.WeaponUsesProvisionalAnchor} " +
                         $"weaponRotZ={visual.Weapon.transform.parent.localEulerAngles.z:0.#} scaleX={visual.transform.localScale.x:0.###}");
                }
                foreach (float facing in new[] { 1f, -1f })
                {
                    string side = facing > 0f ? "R" : "L";
                    game.Player.SetFacing(game.Player.Position.x + facing);
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot($"{side}_1idle");
                    game.Player.AutoInputOverride = new Vector2(facing, 0f);
                    yield return new WaitForSeconds(0.25f);
                    yield return Shot($"{side}_2move_a");
                    yield return new WaitForSeconds(0.17f);
                    yield return Shot($"{side}_3move_b");
                    game.Player.AutoInputOverride = null;
                    yield return new WaitForSeconds(0.3f);
                    visual.PlayAction("ranged");
                    yield return new WaitForSeconds(0.09f);
                    yield return Shot($"{side}_4aim");
                    yield return new WaitForSeconds(0.17f);
                    yield return Shot($"{side}_5return03"); // ranged 03(손을 내리는 복귀 프레임) 근처
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot($"{side}_5idle_after");
                    visual.PlayHurt();
                    yield return Shot($"{side}_6hurt");
                    yield return new WaitForSeconds(0.3f);
                }
                UnityEngine.Object.Destroy(cam.gameObject);
                UnityEngine.Object.Destroy(game.gameObject);
                yield return null;
            }
            WeaponSkinStore.Equip(WeaponKind.Bow, null);
        }

        [UnityTest]
        public IEnumerator Capture_FourWeaponsCombat_AndCardAdvice()
        {
            foreach (var weapon in WeaponSkinCatalog.Weapons) WeaponSkinStore.Equip(weapon, WeaponSkinCatalog.ForWeapon(weapon).id);
            int sizeIndex = 0;
            foreach (var (job, weapon) in Combos)
            {
                var (w, h) = Sizes[sizeIndex++ % Sizes.Length];
                var game = GameBootstrap.Build(job, weapon);
                yield return null;
                var tutorial = UnityEngine.Object.FindFirstObjectByType<FirstRunTutorialView>();
                if (tutorial != null && tutorial.IsOpen) tutorial.Dismiss();
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
                game.Player.Heal(1f);
                var def = WeaponSkinCatalog.ForWeapon(weapon);
                var skin = WeaponSkinCatalog.HeldSprite(def);
                var canvas = game.CardView.GetComponentInParent<Canvas>();
                var cam = Camera.main;
                // 일반 스폰·실제 무기 공격으로 6초 전투(좌우 이동).
                var swingSet = new HashSet<Sprite>(Resources.LoadAll<Sprite>(def.SwingFolder(false)));
                swingSet.UnionWith(Resources.LoadAll<Sprite>(def.SwingFolder(true)));
                var probe = LateSpriteProbe.Attach(game.Player.Visual); // 배치 모드: 렌더 시점 스프라이트를 LateUpdate 탐침으로 기록
                float end = Time.realtimeSinceStartup + 6f; bool shot = false;
                while (Time.realtimeSinceStartup < end)
                {
                    if (game.CardView.IsOpen) game.CardView.Choose(0);
                    if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen) game.SkillView.Continue();
                    if (game.HybridView.IsOpen) game.HybridView.Unlock();
                    game.Player.AutoInputOverride = new Vector2(Mathf.Sin(Time.time * 1.3f), 0f);
                    yield return null;
                    var bs = game.Player.Visual.Body.sprite;
                    if (!shot && weapon == WeaponKind.Sword && game.Player.Visual.SwordSwingActive && bs != null &&
                        bs.name.EndsWith("_05", StringComparison.Ordinal))
                    { shot = true; yield return Capture(cam, new[] { canvas }, w, h, $"combat_{job}_{weapon}_swing_{w}x{h}"); }
                }
                game.Player.AutoInputOverride = null;
                int frames = probe.weaponFrames.Count;
                int skinFrames = skin != null ? probe.weaponFrames.Count(s => s == skin) : 0;
                var swings = probe.bodyFrames.Where(s => s != null && s.name.StartsWith(WeaponSkinApplier.SwingPrefix, StringComparison.Ordinal)).ToList();
                int swing = swings.Count, swingSkin = swings.Count(swingSet.Contains);
                yield return Capture(cam, new[] { canvas }, w, h, $"combat_{job}_{weapon}_{w}x{h}");
                Line($"combat {job}/{weapon}: skin={def.displayName} ready={skin != null} frames={frames} weaponSkinFrames={skinFrames} " +
                     $"weaponSprite={game.Player.Visual.Weapon.sprite?.name} swingFrames={swing} swingSkinFrames={swingSkin} kills={game.Kills} " +
                     $"dmg={game.Stats.Damage:0.###} range={game.Stats.Range:0.###} interval={game.Stats.AttackInterval:0.###}");

                // 카드 추천: 실제 보유 상태에서 보상 창을 연다(AddIngredient 강제 유도). 같은 상태 재표시 안정성도 기록.
                var cat = job == Job.TeaSommelier ? IngredientType.Tea : IngredientType.Cocktail;
                var gauge = cat == IngredientType.Tea ? game.TeaGauge : game.CocktailGauge;
                for (int k = 0; k < 4; k++)
                {
                    game.AddIngredient(cat, Mathf.Max(1, gauge.Threshold - gauge.Current));
                    for (int i = 0; i < 10 && !game.CardView.IsOpen; i++)
                    {
                        if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen) game.SkillView.Continue();
                        if (game.HybridView.IsOpen) game.HybridView.Unlock();
                        yield return null;
                    }
                    if (!game.CardView.IsOpen) { Line($"advice {job}/{weapon} pick{k}: card view not open"); continue; }
                    var parts = new List<string>();
                    for (int i = 0; i < game.CardView.Advice.Count; i++)
                        parts.Add($"[{i + 1}{(game.CardView.BadgeVisible(i) ? "*추천" : "")}] {game.CardView.AdviceText(i)}");
                    Line($"advice {job}/{weapon} pick{k}: hp={game.Player.Hp:0}/{game.Player.MaxHp:0} {string.Join(" | ", parts)}");
                    if (k == 3) yield return Capture(cam, new[] { canvas }, w, h, $"card_advice_{job}_{weapon}_{w}x{h}");
                    int pick = Enumerable.Range(0, game.CardView.Advice.Count).FirstOrDefault(i => game.CardView.BadgeVisible(i));
                    game.CardView.Choose(pick);
                    yield return null;
                }
                UnityEngine.Object.Destroy(game.gameObject);
                yield return null;
            }

            // 비취 몸체 + 푸른 강철 검: 몸체 스킨과 무기 외형의 독립 조합(비취는 유료 소유 없이 QA 강제 장착).
            var g2 = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            yield return null;
            var tut = UnityEngine.Object.FindFirstObjectByType<FirstRunTutorialView>();
            if (tut != null && tut.IsOpen) tut.Dismiss();
            g2.GetComponentInChildren<EnemySpawner>().enabled = false;
            SkinSpriteSwapper.Attach(g2.Player.Visual.Body, SkinCatalog.Find(SkinCatalog.TeaJade));
            yield return new WaitForSeconds(0.3f);
            g2.Player.Visual.PlaySwordSwing();
            yield return new WaitForSeconds(0.07f);
            yield return Capture(Camera.main, new[] { g2.CardView.GetComponentInParent<Canvas>() }, 1920, 1080, "combat_jade_body_blue_steel_sword_swing_1920x1080");
            Line($"jade+sword swing body={g2.Player.Visual.Body.sprite?.name} weapon={g2.Player.Visual.Weapon.sprite?.name}");
            UnityEngine.Object.Destroy(g2.gameObject);
            yield return null;
            foreach (var weapon in WeaponSkinCatalog.Weapons) WeaponSkinStore.Equip(weapon, null);
        }
    }
}
