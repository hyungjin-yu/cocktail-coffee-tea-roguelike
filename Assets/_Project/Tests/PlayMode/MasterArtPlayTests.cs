using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-11 마스터 도전 인수 아트 연결(실제 Resources 스프라이트) — 현수막 NPC 일정/손-핀 정렬, 품질·상성 VFX 1회 재생·풀 반납·재사용 초기화.
    /// 미적 PASS는 이 시험이 아니라 오프스크린 증거 프레임(Explicit) 육안 검수로 따로 판단한다.
    /// </summary>
    public class MasterArtPlayTests
    {
        GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            RunModeSelection.Current = RunMode.Endless;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
            game = GameManager.Instance;
            game.Master.BannersEnabled = false;
            game.Stats.baseMaxHp = 100000f;
            game.Player.Heal(1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            foreach (var s in UnityEngine.Object.FindObjectsByType<FairBannerShow>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(s.gameObject);
            if (GameManager.Instance != null) UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            RunModeSelection.Current = RunMode.Standard;
            yield return null;
        }

        static float Sec(float contractMs, float duration) => contractMs / FairBannerShow.ContractMs * duration;

        [UnityTest]
        public IEnumerator BannerShow_UsesAcceptedArt_ContractScheduleAndHandsOnFinalPins()
        {
            const float d = 3.5f;
            var s = FairBannerShow.Create(game, d);
            yield return null;
            Assert.IsTrue(s.UsingArt, "인수 아트 15 clip 로드(임시 대체 아님)");

            var cam = Camera.main;
            float halfW = cam.orthographicSize * cam.aspect;
            Assert.Less(s.RiggerPosition.x, cam.transform.position.x - halfW, "rigger는 화면 밖 왼쪽에서 시작");
            Assert.Greater(s.PorterPosition.x, cam.transform.position.x + halfW, "porter는 화면 밖 오른쪽에서 시작");
            Assert.AreEqual("npc_rigger_walk_in", s.RiggerClip);
            Assert.IsTrue(s.PorterFlipped, "porter 접근은 왼쪽을 본다");
            Assert.AreEqual(-1, s.BannerFrame, "1050ms 전 현수막 없음(운반 천만)");
            Assert.IsFalse(s.LabelVisible);

            s.Step(Sec(1000f, d));
            Assert.AreEqual("npc_rigger_install", s.RiggerClip);
            Assert.AreEqual(s.RiggerStand, s.RiggerPosition, "설치 중 발 고정(미끄럼 없음)");
            Assert.AreEqual(-1, s.BannerFrame);

            s.Step(Sec(1500f, d));
            Assert.AreEqual(3, s.BannerFrame, "1050+450ms → 프레임 03");
            Assert.IsFalse(s.LabelVisible, "펼침 완료 전 글자 없음");

            // 설치 최고점(노출 02/03)의 손 = 현수막 최종 핀(월드).
            Vector2 top = s.BannerTopWorld;
            Vector2 pinL = top + MasterArt.BannerLocal(MasterArt.PinLeftPx) * FairBannerShow.BannerScale;
            Vector2 pinR = top + MasterArt.BannerLocal(MasterArt.PinRightPx) * FairBannerShow.BannerScale;
            Vector2 handR = s.RiggerPosition + MasterArt.NpcAnchorLocal(MasterArt.RiggerGripPx, s.RiggerFlipped) * s.NpcScale;
            Vector2 handP = s.PorterPosition + MasterArt.NpcAnchorLocal(MasterArt.PorterGripPx, s.PorterFlipped) * s.NpcScale;
            Assert.Less(Vector2.Distance(handR, pinL), 0.01f, $"rigger 손 {handR} ↔ 왼쪽 핀 {pinL}");
            Assert.Less(Vector2.Distance(handP, pinR), 0.01f, $"porter 손 {handP} ↔ 오른쪽 핀 {pinR}");
            Assert.Less(Mathf.Abs(s.RiggerStand.x - cam.transform.position.x) + 0.5f, halfW, "설치 위치는 화면 안");
            Assert.Less(Mathf.Abs(s.PorterStand.x - cam.transform.position.x) + 0.5f, halfW);
            Assert.Less(top.y, cam.transform.position.y + cam.orthographicSize, "현수막은 화면 안 위쪽");

            s.Step(Sec(1925f, d) + 0.001f);
            Assert.AreEqual(7, s.BannerFrame);
            Assert.IsTrue(s.LabelVisible, "07 프레임부터 '박람회' 글자");

            s.Step(Sec(2600f, d));
            Assert.AreEqual("npc_rigger_walk_out", s.RiggerClip);
            Assert.IsTrue(s.RiggerFlipped, "rigger 퇴장은 왼쪽을 본다");
            Assert.IsFalse(s.PorterFlipped, "porter 퇴장은 오른쪽(정방향)");
            Assert.AreEqual(7, s.BannerFrame, "마지막 프레임 고정");

            s.Settle();
            yield return null;
            Assert.IsTrue(s.Settled);
            Assert.IsFalse(s.NpcsPresent, "설치 완료 후 NPC 제거");
            Assert.IsTrue(s.BannerVisible);
            Assert.IsTrue(s.LabelVisible);
        }

        [UnityTest]
        public IEnumerator BannerShow_ScheduleScalesWithPrepSeconds_3And4()
        {
            foreach (float d in new[] { 3f, 4f })
            {
                var s = FairBannerShow.Create(game, d);
                s.Step(d * 0.5f);
                Assert.AreEqual(1750f, s.ContractTime, 0.5f, $"{d}초 중간 = 계약 1750ms");
                Assert.AreEqual("npc_porter_install", s.PorterClip);
                s.Step(d);
                Assert.AreEqual(7, s.BannerFrame);
                s.Finish();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator QualityVfx_OncePerTransaction_ReleasesAndResetsOnReuse_LowReplacesFull()
        {
            Assert.IsTrue(game.OpenMasterBrew());
            var slot = game.MasterHud.FxSlot;
            Assert.IsNotNull(slot);
            int played = MasterVfx.Played;
            DrinkQualityFx.Play(game, 90, DrinkQualityFx.Moment.Crafted, slot);
            Assert.AreEqual("quality_master", DrinkQualityFx.LastClip);
            Assert.AreEqual(played + 1, MasterVfx.Played, "거래 1건 = 1회");
            Assert.AreEqual(1, MasterVfx.ActiveUi);
            var v = slot.GetComponentsInChildren<MasterVfx>(true).First(x => x.Playing);
            v.Step(83f * 3 + 1f);
            Assert.AreEqual(3, v.CurrentFrame);
            // 연타: 이전 것은 즉시 반납되고 새 것 1개만(겹침 없음).
            DrinkQualityFx.Play(game, 70, DrinkQualityFx.Moment.Drunk, slot);
            Assert.AreEqual("quality_excellent", DrinkQualityFx.LastClip);
            Assert.AreEqual(1, MasterVfx.ActiveUi);
            var v2 = slot.GetComponentsInChildren<MasterVfx>(true).First(x => x.Playing);
            Assert.AreSame(v, v2, "풀 재사용");
            Assert.AreEqual(0, v2.CurrentFrame, "재사용 시 frame0");
            Assert.AreEqual(Vector3.one, v2.transform.localScale);
            v2.Step(MasterArt.Get("quality_excellent").TotalMs + 1f);
            Assert.IsFalse(v2.Playing, "끝나면 반납(루프 없음)");
            Assert.AreEqual(0, MasterVfx.ActiveUi);
            Assert.IsFalse(v2.gameObject.activeSelf);

            // 품질 없는 음료(-1)는 효과 없음.
            int p0 = MasterVfx.Played;
            DrinkQualityFx.Play(game, -1, DrinkQualityFx.Moment.Submitted, slot);
            Assert.AreEqual(p0, MasterVfx.Played);

            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            DrinkQualityFx.Play(game, 10, DrinkQualityFx.Moment.Submitted, slot);
            Assert.AreEqual("quality_normal_low", DrinkQualityFx.LastClip, "저이펙트는 full 대신 low");
            Assert.AreEqual(1, MasterVfx.ActiveUi);
            game.CloseMasterBrew();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AffinityVfx_OnlyOnConfirmedAffinityHit_ReleasesAndBudgetCaps()
        {
            foreach (var sp in game.GetComponentsInChildren<EnemySpawner>()) sp.enabled = false;
            game.Master.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            game.Player.DrunkDrink = DrinkKind.Cocktail; // 칵테일 → 커피 계열 상성
            var bug = Enemy.SpawnAt(new Vector2(3f, 0f), EnemyDef.EspressoBug, 50f, game.Player, game.transform);
            var tea = Enemy.SpawnAt(new Vector2(-3f, 0f), EnemyDef.TeabagImp, 50f, game.Player, game.transform);
            yield return new WaitForSeconds(0.1f);

            int p = MasterVfx.Played;
            Assert.AreEqual(0, tea.TakeDirectHit(10f, HitKind.Normal).bonusDamage);
            Assert.AreEqual(p, MasterVfx.Played, "상성 없는 적중은 효과 없음");
            tea.TakeDamage(10f, HitKind.Dot);
            Assert.AreEqual(p, MasterVfx.Played, "지속 피해는 효과 없음");

            var r = bug.TakeDirectHit(10f, HitKind.Normal);
            Assert.Greater(r.bonusDamage, 0);
            Assert.AreEqual(p + 1, MasterVfx.Played, "확정 상성 적중 1회");
            Assert.AreEqual("cocktail_foam", MasterVfx.LastClip);
            Assert.AreEqual(1, MasterVfx.ActiveWorld);

            // 같은 순간 연타는 간격 예산으로 건너뜀(쌓지 않음).
            bug.TakeDirectHit(10f, HitKind.Normal);
            Assert.AreEqual(p + 1, MasterVfx.Played);

            yield return new WaitForSeconds(0.7f); // 498ms 후 반납
            Assert.AreEqual(0, MasterVfx.ActiveWorld, "끝나면 반납");

            for (int i = 0; i < MasterVfx.MaxWorldActive + 4; i++)
                MasterVfx.PlayWorld("coffee_bean_steam", Vector2.zero, 1f, false);
            Assert.AreEqual(MasterVfx.MaxWorldActive, MasterVfx.ActiveWorld, "동시 상한");
        }

        /// <summary>오프스크린 증거 프레임(명시 실행 전용). 창·포커스·소리 없이 메인 카메라+HUD를 렌더 텍스처로 그려 PNG로 남긴다.
        /// 출력: 환경 변수 CCT_EVIDENCE_DIR(없으면 Delivery/Reports/steam-master-2026-11-11/frames).</summary>
        [UnityTest, Explicit("QA 증거 캡처 — 명시 지정 시만")]
        public IEnumerator MasterArt_OffscreenEvidenceFrames()
        {
            string outDir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("Delivery/Reports/steam-master-2026-11-11/frames");
            Directory.CreateDirectory(outDir);
            var notes = new List<string>();
            // 배치 실행의 자동 시작 타이틀 화면 캔버스가 런 화면을 덮지 않게 제거(캡처 전용, 기존 증거 시험과 같은 처리).
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var sp in game.GetComponentsInChildren<EnemySpawner>()) sp.enabled = false;
            game.Master.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            for (int i = 0; i < 10; i++) yield return null;

            // 1) 현수막 3500ms 계약 시점별(720p + 1080p). 전투 정지(timeScale 0) 상태 그대로.
            const float d = 3.5f;
            Time.timeScale = 0f;
            var show = FairBannerShow.Create(game, d);
            foreach (float ms in new[] { 400f, 1000f, 1300f, 1700f, 2050f, 2900f })
            {
                show.Step(Sec(ms, d));
                string n = $"rigger={show.RiggerClip} porter={show.PorterClip} bannerFrame={show.BannerFrame} label={show.LabelVisible} riggerPos={show.RiggerPosition} porterPos={show.PorterPosition}";
                yield return Shot(outDir, $"banner_{ms:0000}ms_720", notes, n, 1280, 720);
                yield return Shot(outDir, $"banner_{ms:0000}ms_1080", notes, n, 1920, 1080);
            }
            show.Settle();
            yield return Shot(outDir, "banner_settled_720", notes, "settled", 1280, 720);
            show.Finish();
            Time.timeScale = 1f;
            yield return null;

            // 2) 품질 VFX(제조 창 슬롯) full 3등급 + low 3등급, 정점 프레임.
            Assert.IsTrue(game.OpenMasterBrew());
            foreach (bool low in new[] { false, true })
            {
                SettingsService.SetEffectsLevel(low ? SettingsData.EffectsLow : SettingsData.EffectsNormal);
                foreach (var (q, name) in new[] { (40, "normal"), (70, "excellent"), (95, "master") })
                {
                    DrinkQualityFx.Play(game, q, low ? DrinkQualityFx.Moment.Crafted : DrinkQualityFx.Moment.Submitted, game.MasterHud.FxSlot);
                    var v = game.MasterHud.FxSlot.GetComponentsInChildren<MasterVfx>(true).First(x => x.Playing);
                    v.Step(low ? 83f + 1f : 83f * 3 + 1f);
                    string tag = $"quality_{name}{(low ? "_low" : "")}";
                    yield return Shot(outDir, tag + "_720", notes, $"clip={v.ClipName} frame={v.CurrentFrame} cup={game.MasterHud.SubmitCupShown}", 1280, 720);
                    if (!low && name == "master") yield return Shot(outDir, tag + "_1080", notes, $"clip={v.ClipName} frame={v.CurrentFrame}", 1920, 1080);
                }
            }
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            game.CloseMasterBrew();
            yield return null;

            // 3) 상성 VFX: 실제 적 옆 적중점에 배치(적중 경로 판정은 AffinityVfx_* 시험이 따로 검증). full/low 각 3종.
            var bug = Enemy.SpawnAt((Vector2)game.Player.Position + new Vector2(2.5f, 0f), EnemyDef.EspressoBug, 50f, game.Player, game.transform);
            yield return null;
            Time.timeScale = 0f;
            Vector2 at = (Vector2)bug.transform.position + new Vector2(0f, 0.3f);
            foreach (bool low in new[] { false, true })
                foreach (var kind in new[] { DrinkKind.Cocktail, DrinkKind.Coffee, DrinkKind.Tea })
                {
                    string clip = MasterArt.AffinityClip(kind, low);
                    var fx = MasterVfx.PlayWorld(clip, at, 1f, false);
                    Assert.IsNotNull(fx, clip);
                    fx.Step(low ? 83f + 1f : 83f * 2 + 1f);
                    yield return Shot(outDir, $"affinity_{clip}_720", notes, $"clip={fx.ClipName} frame={fx.CurrentFrame} at={at}", 1280, 720);
                    fx.Release();
                }
            Time.timeScale = 1f;
            File.WriteAllLines(Path.Combine(outDir, "frames-notes.txt"), notes);
        }

        static IEnumerator Shot(string outDir, string name, List<string> notes, string note, int w, int h)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(w, h, 24);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var oldOrders = canvases.Select(c => c.sortingOrder).ToList();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 1000; }
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            for (int ci = 0; ci < canvases.Count; ci++) { canvases[ci].renderMode = RenderMode.ScreenSpaceOverlay; canvases[ci].worldCamera = null; canvases[ci].sortingOrder = oldOrders[ci]; }
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            notes.Add($"{name}.png | {note} | timeScale={Time.timeScale}");
        }
    }
}
