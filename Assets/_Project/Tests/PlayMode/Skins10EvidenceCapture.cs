using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 79 최종 증거(창 없음, CCT_EVIDENCE_DIR 있을 때만): 실제 경로(판매 코인 1000 구매 → SkinCommerce 장착 → GameBootstrap 런 시작의
    /// SkinSpriteSwapper.AttachIfEquipped)로 신규 10종을 각 직업 무기 2종 전투에 장착하고, 렌더 시점(LateUpdate 10000) 몸체 프레임이
    /// 모두 스킨판인지(기본 복장 프레임 0), 좌우 반전, 검 외형 오버레이(휘두르기 중만), 능력치·판정 불변을 검사한다.
    /// 강제 경로(보고 표기): 체력 크게, 검 휘두르기·피격·쓰러짐 동작 직접 재생, 좌우 이동 입력 대체. 상점은 RenderTexture로 1080·720·836×705 캡처.
    /// </summary>
    public class Skins10EvidenceCapture
    {
        class MemPersister : ICommercePersister
        {
            public string Json;
            public CommerceState Load() { var s = string.IsNullOrEmpty(Json) ? new CommerceState() : JsonUtility.FromJson<CommerceState>(Json); s.Normalize(); return s; }
            public bool Save(CommerceState state) { Json = JsonUtility.ToJson(state); return true; }
        }

        [DefaultExecutionOrder(10000)]
        class RenderProbe : MonoBehaviour
        {
            public SpriteRenderer body;
            public WeaponSkinApplier applier;
            public readonly List<(Sprite sprite, float sign, bool overlay, string overlayName)> frames = new List<(Sprite, float, bool, string)>();
            void LateUpdate()
            {
                if (applier == null) applier = GetComponent<WeaponSkinApplier>();
                var o = applier != null ? applier.SwingOverlay : null;
                frames.Add((body.sprite, Mathf.Sign(body.transform.lossyScale.x), o != null && o.enabled, o != null && o.sprite != null ? o.sprite.name : null));
            }
        }

        static readonly string[] NewIds =
        {
            SkinCatalog.BartenderRail, SkinCatalog.BartenderTropic, SkinCatalog.BartenderNoir, SkinCatalog.BartenderAlchemist, SkinCatalog.BartenderStage,
            SkinCatalog.TeaMountain, SkinCatalog.TeaCeremony, SkinCatalog.TeaApiary, SkinCatalog.TeaGreenhouse, SkinCatalog.TeaAstronomer,
        };
        static readonly (int w, int h)[] ShopSizes = { (1920, 1080), (1280, 720), (960, 720), (836, 705) };

        string dir, tempDir;
        readonly StringBuilder log = new StringBuilder();
        MemPersister persister;
        SkinCommerce commerce;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            tempDir = Path.Combine(Application.temporaryCachePath, $"cct_skins10_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            PermanentProgress.SetPathForTests(Path.Combine(tempDir, "save.json"));
            DrinkLab.SetPathForTests(Path.Combine(tempDir, "drinks.json"));
            InputSettings.Scheme = MovementScheme.Keyboard;
            persister = new MemPersister();
            commerce = new SkinCommerce(CommerceConfig.Disabled(), new UnavailableSkinStore(), null, persister, () => 0d);
            SkinCommerce.SetSharedForTests(commerce);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!string.IsNullOrEmpty(dir)) File.AppendAllText(Path.Combine(dir, "skins10-evidence-log.txt"), log.ToString(), new UTF8Encoding(false));
            log.Clear();
            Time.timeScale = 1f;
            foreach (var w in WeaponSkinCatalog.Weapons) WeaponSkinStore.Equip(w, null);
            SkinCommerce.SetSharedForTests(null);
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            yield return null;
            try { Directory.Delete(tempDir, true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[Skins10] " + s); }

        IEnumerator Shot(Camera cam, IList<Canvas> canvases, int w, int h, string name) => SkinEvidenceCapture.CaptureTo(dir, Line, cam, canvases, w, h, name);

        /// <summary>실제 코인 구매 + 장착(아트 미완성이면 BuyCosmetic이 거부 → 실패로 드러난다).</summary>
        void BuyAll()
        {
            DrinkLab.Data.coins = 1000 * NewIds.Length;
            foreach (var id in NewIds)
            {
                Assert.IsTrue(SkinArt.Exists(SkinCatalog.Find(id)), $"{id} 아트 미완성: {string.Join(",", SkinArt.MissingFrames(SkinCatalog.Find(id)))}");
                Assert.IsTrue(DrinkLab.BuyCosmetic(id, commerce.IsCashOwned, out var e), id + " " + e);
            }
            Assert.AreEqual(0, DrinkLab.Coins, "10종 × 1000코인");
        }

        [UnityTest]
        public IEnumerator Capture_Shop_TenSkins_AllSizes()
        {
            if (EventSystem.current == null) new GameObject("EvidenceEventSystem", typeof(EventSystem));
            var canvas = UiFactory.CreateCanvas(null, "Skins10Shop", 100);
            DrinkLab.Data.coins = 2500;
            var view = SkinShopView.Create(canvas.transform, () => { }, () => commerce);
            view.Show();
            yield return null;
            foreach (var (w, h) in ShopSizes)
                foreach (var id in new[] { SkinCatalog.BartenderRail, SkinCatalog.TeaAstronomer })
                {
                    EventSystem.current.SetSelectedGameObject(view.RowButton(id).gameObject);
                    yield return null; yield return null;
                    yield return Shot(null, new[] { canvas }, w, h, $"shop_{id}_{w}x{h}");
                    var overlaps = DetailOverlaps(view);
                    Line($"layout {w}x{h} {id}: overlaps=[{string.Join(",", overlaps)}]");
                    Assert.IsEmpty(overlaps, $"상세 칸 글자·버튼 겹침 {w}x{h}");
                    Line($"shop {w}x{h} {id}: selected={view.SelectedId} preview={(view.PreviewSprite(id) != null)} artNote={view.ArtNoteVisible(id)} " +
                         $"buy={view.BuyButton(id).interactable} label='{view.BuyLabel(id)}' state='{view.StateText(id)}' row='{view.RowStatusText(id)}' scrollY={view.ListContent.anchoredPosition.y:0}");
                }
            foreach (var id in NewIds)
            {
                view.RowButton(id).onClick.Invoke();
                yield return Shot(null, new[] { canvas }, 1920, 1080, $"shop_detail_{id}_1920x1080");
                Assert.IsNotNull(view.PreviewSprite(id), id + " 미리보기(완성 아트)");
                Assert.IsFalse(view.ArtNoteVisible(id), id);
                Line($"detail {id}: preview={view.PreviewSprite(id).name} diff='{view.DifferenceText(id).Replace("\n", " / ")}'");
            }
            // 구매·장착 상태 표시
            view.BuyButton(SkinCatalog.TeaGreenhouse).onClick.Invoke();
            view.BuyButton(SkinCatalog.TeaGreenhouse).onClick.Invoke();
            view.EquipButton(SkinCatalog.TeaGreenhouse).onClick.Invoke();
            yield return Shot(null, new[] { canvas }, 1280, 720, "shop_bought_equipped_tea_greenhouse_1280x720");
            Line($"buy+dup+equip greenhouse: coins={DrinkLab.Coins} owned={commerce.IsOwned(SkinCatalog.TeaGreenhouse)} equipped={commerce.IsEquipped(SkinCatalog.TeaGreenhouse)} state='{view.StateText(SkinCatalog.TeaGreenhouse)}'");
            Assert.AreEqual(1500, DrinkLab.Coins);
            UnityEngine.Object.Destroy(canvas.gameObject);

            // 판매 코인 목록(음료 연구소 주문 탭) — 12 스킨 + 맵 카드, 아래로 스크롤.
            var lab = DrinkLabView.Show(() => { });
            lab.SetTab(DrinkLabView.Tab.Orders);
            yield return null;
            var labCanvas = lab.GetComponent<Canvas>();
            foreach (var sr in lab.GetComponentsInChildren<ScrollRect>()) sr.verticalNormalizedPosition = 0.35f;
            yield return Shot(null, new[] { labCanvas }, 1920, 1080, "drinklab_coin_cosmetics_1920x1080");
            int cards = lab.GetComponentsInChildren<RectTransform>(true).Count(r => r.name.StartsWith("Cosmetic_"));
            Line($"drinklab coin list cards={cards}");
            Assert.AreEqual(13, cards, "스킨 12 + 맵 1");
            lab.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Combat_TenSkins_RealEquip_NoBaseFrames_FacingOverlayStats()
        {
            BuyAll();
            // 기준 능력치(스킨 없음) — 직업·무기마다.
            var baseline = new Dictionary<(Job, WeaponKind), string>();
            var combos = new[] { (Job.Bartender, WeaponKind.Rapier), (Job.Bartender, WeaponKind.Gun), (Job.TeaSommelier, WeaponKind.Sword), (Job.TeaSommelier, WeaponKind.Bow) };
            foreach (var (job, weapon) in combos)
            {
                commerce.Unequip(job);
                var g = GameBootstrap.Build(job, weapon);
                yield return null;
                Assert.IsNull(g.Player.Visual.Body.GetComponent<SkinSpriteSwapper>(), "미장착 = 기본");
                baseline[(job, weapon)] = Stats(g);
                UnityEngine.Object.Destroy(g.gameObject);
                yield return null;
            }

            int failures = 0;
            foreach (var id in NewIds)
            {
                var def = SkinCatalog.Find(id);
                Assert.IsTrue(commerce.Equip(id), id);
                var map = SkinArt.Sprites(def);
                var skinSprites = new HashSet<Sprite>(map.Values);
                var required = SkinArt.RequiredFrameNames(def.job);
                var seen = new HashSet<string>();
                int run = 0;
                foreach (var (job, weapon) in combos.Where(c => c.Item1 == def.job))
                {
                    bool firstRun = run++ == 0; // 증거 컷 최소화: 왼쪽(836×705)은 스킨당 1장, 쓰러짐은 직업 첫 스킨만
                    WeaponSkinStore.Equip(WeaponKind.Sword, weapon == WeaponKind.Sword ? WeaponSkinCatalog.ForWeapon(WeaponKind.Sword).id : null);
                    var game = GameBootstrap.Build(job, weapon);
                    yield return null;
                    var tutorial = UnityEngine.Object.FindFirstObjectByType<FirstRunTutorialView>();
                    if (tutorial != null && tutorial.IsOpen) tutorial.Dismiss();
                    var visual = game.Player.Visual;
                    var swapper = visual.Body.GetComponent<SkinSpriteSwapper>();
                    Assert.IsNotNull(swapper, $"{id}: 실제 장착 경로로 스킨 적용");
                    Assert.AreEqual(id, swapper.ProductId);
                    string stats = Stats(game);
                    Assert.AreEqual(baseline[(job, weapon)], stats, $"{id} {weapon}: 능력치·판정 불변");
                    game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
                    game.Player.Heal(1f);
                    var probe = visual.gameObject.AddComponent<RenderProbe>();
                    probe.body = visual.Body;
                    var canvas = game.CardView.GetComponentInParent<Canvas>();
                    var cam = Camera.main;

                    float start = Time.realtimeSinceStartup, end = start + 5f;
                    bool shotR = false, shotL = false, swingShot = false;
                    while (Time.realtimeSinceStartup < end)
                    {
                        if (game.CardView.IsOpen) game.CardView.Choose(0);
                        if (game.SkillView.IsOpen && !game.SkillView.IsReadOnlyOpen) game.SkillView.Continue();
                        if (game.HybridView.IsOpen) game.HybridView.Unlock();
                        float t = Time.realtimeSinceStartup - start;
                        game.Player.AutoInputOverride = new Vector2(t < 2.5f ? 1f : -1f, Mathf.Sin(t * 2f) * 0.3f);
                        if (weapon == WeaponKind.Sword && (int)(t * 2f) % 2 == 0 && !visual.SwordSwingActive) visual.PlaySwordSwing(); // QA 강제 휘두르기
                        yield return null;
                        var bs = visual.Body.sprite;
                        if (!swingShot && visual.SwordSwingActive && bs != null && bs.name.EndsWith("_05", StringComparison.Ordinal))
                        { swingShot = true; yield return Shot(cam, new[] { canvas }, 1280, 720, $"combat_{id}_{weapon}_swing_1280x720"); }
                        if (!shotR && t > 1.6f) { shotR = true; yield return Shot(cam, new[] { canvas }, 1280, 720, $"combat_{id}_{weapon}_right_1280x720"); }
                        if (!shotL && firstRun && t > 4.2f) { shotL = true; yield return Shot(cam, new[] { canvas }, 836, 705, $"combat_{id}_{weapon}_left_836x705"); }
                    }
                    game.Player.AutoInputOverride = Vector2.zero;
                    visual.PlayHurt(); // QA 강제 피격 동작
                    yield return new WaitForSeconds(0.25f);
                    visual.PlayDefeat(); // QA 강제 쓰러짐 동작
                    yield return new WaitForSeconds(0.6f);
                    if (firstRun && (id == SkinCatalog.BartenderRail || id == SkinCatalog.TeaMountain))
                        yield return Shot(cam, new[] { canvas }, 1280, 720, $"combat_{id}_{weapon}_defeat_1280x720");

                    int leaks = 0, swingFrames = 0, overlayOn = 0, overlayOff = 0, overlayMismatch = 0;
                    var leakNames = new HashSet<string>();
                    var signs = new HashSet<float>();
                    foreach (var f in probe.frames)
                    {
                        if (f.sprite == null) continue;
                        signs.Add(f.sign);
                        if (skinSprites.Contains(f.sprite)) seen.Add(f.sprite.name);
                        else { leaks++; leakNames.Add(f.sprite.name); }
                        bool swing = f.sprite.name.StartsWith(WeaponSkinApplier.SwingPrefix, StringComparison.Ordinal);
                        if (swing) { swingFrames++; if (f.overlay) { overlayOn++; if (f.overlayName != f.sprite.name) overlayMismatch++; } }
                        else if (f.overlay) overlayOff++;
                    }
                    bool expectOverlay = weapon == WeaponKind.Sword;
                    Line($"combat {id} {job}/{weapon}: frames={probe.frames.Count} baseCostumeFrames={leaks} [{string.Join(",", leakNames)}] facing={string.Join("/", signs.OrderBy(s => s))} " +
                         $"swingFrames={swingFrames} overlayOn={overlayOn} overlayOutsideSwing={overlayOff} overlayNameMismatch={overlayMismatch} kills={game.Kills} stats={stats}");
                    if (leaks > 0 || overlayOff > 0 || overlayMismatch > 0 || signs.Count < 2 || (expectOverlay && (swingFrames == 0 || overlayOn != swingFrames)) || (!expectOverlay && overlayOn > 0))
                        failures++;
                    UnityEngine.Object.Destroy(game.gameObject);
                    yield return null;
                }
                var groups = seen.Select(n => System.Text.RegularExpressions.Regex.Replace(n, @"_right_\d+$", "")).Distinct().OrderBy(n => n);
                Line($"coverage {id}: seen {seen.Count(required.Contains)}/{required.Count} groups=[{string.Join(",", groups)}] missing=[{string.Join(",", required.Where(r => !seen.Contains(r)))}]");
            }
            Assert.AreEqual(0, failures, "기본 복장 프레임·오버레이·반전 위반이 있는 전투 실행 수(로그 참고)");
        }

        /// <summary>상세 칸 요소(이름·직업·차이·능력치 동일·상태 문구·구매/장착 버튼) 사각형이 서로 겹치는 쌍. 글자는 실제 줄 높이(preferredHeight)로 잰다.</summary>
        static List<string> DetailOverlaps(SkinShopView view)
        {
            var detail = view.transform.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "SkinDetail");
            var names = new[] { "Name", "Job", "Difference", "StatsSame", "State", "BuyButton", "EquipButton" };
            var rects = new List<(string n, Rect r)>();
            foreach (var n in names)
            {
                var rt = detail.Cast<Transform>().Select(t => t as RectTransform).First(t => t.name == n);
                var r = new Rect(rt.offsetMin + new Vector2(detail.rect.width * rt.anchorMin.x, detail.rect.height * rt.anchorMin.y), rt.rect.size);
                var txt = rt.GetComponent<Text>();
                if (txt != null)
                {
                    float ph = txt.cachedTextGeneratorForLayout.GetPreferredHeight(txt.text, txt.GetGenerationSettings(new Vector2(rt.rect.width, 0f))) / txt.pixelsPerUnit;
                    float grow = Mathf.Max(0f, ph - r.height) / 2f; // 가운데 정렬 글자는 위아래로 넘친다
                    r = new Rect(r.x, r.y - grow, r.width, r.height + grow * 2f);
                }
                rects.Add((n, r));
            }
            var hits = new List<string>();
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                    if (rects[i].r.Overlaps(rects[j].r)) hits.Add(rects[i].n + "/" + rects[j].n);
            return hits;
        }

        static string Stats(GameManager g) =>
            $"dmg={g.Stats.Damage:0.###} range={g.Stats.Range:0.###} interval={g.Stats.AttackInterval:0.###} hp={g.Player.MaxHp:0.#} " +
            $"radius={g.Player.GetComponent<CircleCollider2D>().radius:0.###} scale={g.Player.Visual.transform.localScale.y:0.###}";
    }
}
