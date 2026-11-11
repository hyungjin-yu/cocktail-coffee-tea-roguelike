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
            // 2026-11-11 발 미끄럼 보정: 화면 밖에서 오지 않고, 설치 위치에서 실측 보폭 속도×걷기 시간만큼 떨어진 곳에서 투명하게 시작.
            Assert.AreEqual(0f, s.NpcAlpha, 1e-4f, "시작 프레임은 투명(가장자리 갑툭튀 없음)");
            Assert.AreEqual(MasterArt.WalkSpeedPerMs(s.NpcScale) * MasterArt.WalkInEndMs, s.WalkDistance, 1e-4f, "걷기 거리 = 보폭 속도 × 걷기 시간");
            Assert.AreEqual(s.RiggerStand.x - s.WalkDistance, s.RiggerPosition.x, 1e-4f);
            Assert.AreEqual(s.PorterStand.x + s.WalkDistance, s.PorterPosition.x, 1e-4f);
            Assert.AreEqual("npc_rigger_walk_in", s.RiggerClip);
            Assert.IsTrue(s.PorterFlipped, "porter 접근은 왼쪽을 본다");
            Assert.AreEqual(-1, s.BannerFrame, "현수막 시작 전 없음(운반 천만)");
            Assert.IsFalse(s.LabelVisible);
            s.Step(Sec(MasterArt.NpcFadeMs, d));
            Assert.AreEqual(1f, s.NpcAlpha, 1e-4f, "전환 후 불투명");
            // 이동 속도 = 걷기 그림 보폭 속도(계약 시각 기준): 100ms 동안 이동 거리.
            float x0 = s.RiggerPosition.x; s.Step(Sec(MasterArt.NpcFadeMs + 100f, d));
            Assert.AreEqual(MasterArt.WalkSpeedPerMs(s.NpcScale) * 100f, s.RiggerPosition.x - x0, 1e-3f, "걸음과 이동 속도 일치(발 미끄럼 최소)");

            s.Step(Sec(MasterArt.WalkInEndMs, d));
            Assert.AreEqual("npc_rigger_install", s.RiggerClip);
            Assert.AreEqual(s.RiggerStand, s.RiggerPosition, "설치 중 발 고정(미끄럼 없음)");
            Assert.AreEqual(-1, s.BannerFrame);

            s.Step(Sec(MasterArt.BannerStartMs + 450f, d));
            Assert.AreEqual(3, s.BannerFrame, "현수막 시작+450ms → 프레임 03");
            Assert.IsFalse(s.LabelVisible, "펼침 완료 전 글자 없음");
            // 펼치는 중(프레임 03): 손에서 현재 천 끝까지 줄로 연결(손-천 분리 없음), 천 끝은 최종 핀보다 안쪽.
            Assert.IsTrue(s.CordVisible, "펼치는 중 줄 표시");
            Vector2 expHandL = s.RiggerPosition + MasterArt.NpcAnchorLocal(MasterArt.RiggerGripPx, s.RiggerFlipped) * s.NpcScale;
            Assert.Less(Vector2.Distance(s.CordHandLeft, expHandL), 0.001f, "줄 시작 = rigger 손");
            Vector2 expEndL = s.BannerTopWorld + MasterArt.BannerLocal(new Vector2(MasterArt.BannerPinsPx[3].x, MasterArt.BannerPinsPx[3].z)) * FairBannerShow.BannerScale;
            Assert.Less(Vector2.Distance(s.CordEndLeft, expEndL), 0.001f, "줄 끝 = 프레임 03 천 왼쪽 끝");
            Assert.Greater(s.CordEndLeft.x, s.CordHandLeft.x, "천 끝은 손보다 안쪽(펼치는 중)");
            Assert.Less(s.CordEndRight.x, s.CordHandRight.x);

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

            s.Step(Sec(MasterArt.BannerStartMs + 875f, d) + 0.001f);
            Assert.AreEqual(7, s.BannerFrame);
            Assert.IsTrue(s.LabelVisible, "07 프레임부터 '박람회' 글자");
            Assert.IsFalse(s.CordVisible, "최종 핀 도달 후 줄 없음");

            s.Step(Sec(MasterArt.InstallEndMs + 100f, d));
            Assert.AreEqual(1f, s.NpcAlpha, 1e-4f, "퇴장 초반 불투명");
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

        /// <summary>2026-11-11 사용자 결함: 현수막이 카메라를 따라다니며 바 카운터 뒤에 깔리고 글자가 카운터 위에 겹쳤다.
        /// 현수막·글자는 무대 벽의 카운터 사이 빈 칸에 월드 고정 — 카운터·램프 스프라이트와 겹치지 않고, 플레이어·카메라가 움직여도 월드 좌표 불변.</summary>
        [UnityTest]
        public IEnumerator BannerShow_WorldFixedInCounterGap_NoPropOverlap_CameraMoveInvariant([Values(Job.Bartender, Job.TeaSommelier)] Job job)
        {
            if (GameManager.Instance != null) { UnityEngine.Object.Destroy(GameManager.Instance.gameObject); yield return null; }
            RunModeSelection.Current = RunMode.Endless;
            var g = GameBootstrap.Build(job, job == Job.Bartender ? WeaponKind.Rapier : WeaponKind.Sword);
            yield return null;
            g.Master.BannersEnabled = false;
            foreach (var sp in g.GetComponentsInChildren<EnemySpawner>()) sp.enabled = false;
            g.Master.enabled = false;
            var s = FairBannerShow.Create(g, 3.5f);
            s.Settle();
            s.SetLabel("박람회 1/3");
            yield return null;

            var banner = s.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.name == "Banner");
            var label = s.GetComponentInChildren<TextMesh>(true).GetComponent<MeshRenderer>();
            Assert.IsNull(s.GetComponentInParent<Camera>(), "카메라 자식 아님(화면 고정 아님)");
            Assert.IsFalse(s.transform.IsChildOf(g.Player.transform), "플레이어 자식 아님");
            Bounds bb = banner.bounds, lb = label.bounds;
            Assert.IsTrue(bb.min.x <= lb.min.x && lb.max.x <= bb.max.x && bb.min.y <= lb.min.y && lb.max.y <= bb.max.y, $"글자는 천 안({lb} ⊂ {bb})");
            var props = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Counter" || r.name == "Lamp").ToList();
            Assert.Greater(props.Count, 4, "실제 무대 카운터·램프 존재");
            foreach (var p in props)
            {
                bool hitB = bb.min.x < p.bounds.max.x && p.bounds.min.x < bb.max.x && bb.min.y < p.bounds.max.y && p.bounds.min.y < bb.max.y;
                bool hitL = lb.min.x < p.bounds.max.x && p.bounds.min.x < lb.max.x && lb.min.y < p.bounds.max.y && p.bounds.min.y < lb.max.y;
                Assert.IsFalse(hitB, $"{job}: 현수막 {bb} ↔ {p.name} {p.bounds} 겹침 없음");
                Assert.IsFalse(hitL, $"{job}: 글자 ↔ {p.name} 겹침 없음");
            }
            Assert.GreaterOrEqual(bb.min.y, CafeBackground.WallBottomWorldY - 0.01f, "바닥선 위(벽면)");
            Assert.Greater(label.sortingOrder, banner.sortingOrder, "글자가 천 위");

            Vector3 before = banner.transform.position, labelBefore = label.transform.position;
            var cam = Camera.main;
            g.Player.transform.position += new Vector3(6f, 0f, 0f);
            for (int i = 0; i < 20; i++) yield return null; // 카메라가 플레이어를 따라감
            cam.transform.position += new Vector3(3f, 0f, 0f);
            yield return null;
            Assert.AreEqual(before, banner.transform.position, "플레이어·카메라 이동 후 현수막 월드 좌표 불변");
            Assert.AreEqual(labelBefore, label.transform.position, "글자 월드 좌표 불변");
            Assert.AreEqual((Vector2)before, s.InstalledTopWorld);
            s.Finish();
        }

        [UnityTest]
        public IEnumerator BannerShow_ScheduleScalesWithPrepSeconds_3And4()
        {
            foreach (float d in new[] { 3f, 4f })
            {
                var s = FairBannerShow.Create(game, d);
                s.Step(d * 0.5f);
                Assert.AreEqual(1750f, s.ContractTime, 0.5f, $"{d}초 중간 = 계약 1750ms"); // 설치 1000–2500 안
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
            var bug = Enemy.SpawnAt(new Vector2(14f, 0f), EnemyDef.EspressoBug, 50f, game.Player, game.transform); // 플레이어 실제 자동 공격 사거리 밖(0.7초 안에 닿아 추가 상성 효과가 생기면 반납 단언이 흔들림)
            var tea = Enemy.SpawnAt(new Vector2(-14f, 0f), EnemyDef.TeabagImp, 50f, game.Player, game.transform);
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

        /// <summary>2026-11-11 가독성 국소 개선: 상성 월드 배율(원두 1.5·기타 1.25), 원두만 크림 외곽선 4개(재사용 시 꺼짐), 상한 불변.
        /// 품질 효과 그림 168(슬롯 112 유지) — 보이는 내용이 제조 창 안·첫 잔 줄 위에 머문다.</summary>
        [UnityTest]
        public IEnumerator Vfx_ReadabilityScaleOutline_ResetOnReuse_UiArtInsidePanel()
        {
            foreach (var sp in game.GetComponentsInChildren<EnemySpawner>()) sp.enabled = false;
            game.Master.enabled = false;
            foreach (var e in Enemy.Active.ToArray()) if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            MasterVfx.ReleaseAll();
            game.Player.DrunkDrink = DrinkKind.Coffee; // 커피 → 차 계열
            var tea = Enemy.SpawnAt(new Vector2(14f, 0f), EnemyDef.TeabagImp, 50f, game.Player, game.transform);
            yield return new WaitForSeconds(0.1f);
            var r = tea.TakeDirectHit(10f, HitKind.Normal);
            Assert.AreEqual(10, r.baseDamage);
            Assert.AreEqual(2, r.bonusDamage, "A = max(1, round(10×20%))");
            Assert.AreEqual(12, r.total);
            Assert.AreEqual("coffee_bean_steam", MasterVfx.LastClip);
            var fx = UnityEngine.Object.FindObjectsByType<MasterVfx>(FindObjectsSortMode.None).First(v => v.Playing);
            Assert.AreEqual(MasterVfx.AffinityScaleCoffee, fx.transform.localScale.x, 1e-4f);
            Assert.AreEqual(4, fx.OutlineCount, "원두만 외곽선");
            Assert.Greater(fx.GetComponent<SpriteRenderer>().sortingOrder, SortOrders.AirVfx, "같은 적중점 피격 불꽃(AirVfx) 위");
            foreach (var o in fx.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.gameObject != fx.gameObject))
            {
                Assert.AreSame(fx.GetComponent<SpriteRenderer>().sprite, o.sprite, "외곽선 = 같은 프레임");
                Assert.Less(o.sortingOrder, fx.GetComponent<SpriteRenderer>().sortingOrder);
            }
            fx.Release();
            Assert.AreEqual(0, fx.OutlineCount, "반납 시 외곽선 끔");
            var foam = MasterVfx.PlayWorld(MasterArt.AffinityClip(DrinkKind.Cocktail, false), Vector2.zero, MasterVfx.AffinityScale(DrinkKind.Cocktail), false);
            Assert.AreSame(fx, foam, "풀 재사용");
            Assert.AreEqual(0, foam.OutlineCount, "거품은 외곽선 없음");
            Assert.AreEqual(MasterVfx.AffinityScaleOther, foam.transform.localScale.x, 1e-4f);
            foam.Release();
            for (int i = 0; i < MasterVfx.MaxWorldActive + 3; i++) MasterVfx.PlayWorld("coffee_bean_steam", Vector2.zero, MasterVfx.AffinityScaleCoffee, false);
            Assert.AreEqual(MasterVfx.MaxWorldActive, MasterVfx.ActiveWorld, "동시 상한 불변");
            MasterVfx.ReleaseAll();

            Assert.IsTrue(game.OpenMasterBrew());
            yield return null;
            DrinkQualityFx.Play(game, 95, DrinkQualityFx.Moment.Crafted, game.MasterHud.FxSlot);
            var ui = game.MasterHud.FxSlot.GetComponentsInChildren<MasterVfx>(true).First(x => x.Playing);
            var urt = (RectTransform)ui.transform;
            Assert.AreEqual(MasterChallengeHud.FxArtPx, urt.sizeDelta.x, 1e-3f);
            Assert.AreEqual(Vector2.zero, urt.anchoredPosition);
            // 보이는 내용(승인 PNG 알파 경계, 128 캔버스 중 최대 x24–112·y21–103)을 패널 좌표로 환산.
            var panel = (RectTransform)game.MasterHud.FxSlot.parent;
            Canvas.ForceUpdateCanvases();
            var c = new Vector3[4];
            urt.GetWorldCorners(c); // 0=좌하 1=좌상 2=우상
            Vector2 tl = panel.InverseTransformPoint(c[1]), br = panel.InverseTransformPoint(c[3]);
            float sx = (br.x - tl.x) / 128f, sy = (tl.y - br.y) / 128f;
            float contentTop = tl.y - 21f * sy, contentBottom = tl.y - 103f * sy, contentRight = tl.x + 112f * sx;
            float panelTop = panel.rect.yMax, panelRight = panel.rect.xMax;
            Assert.LessOrEqual(contentTop, panelTop, "내용이 패널 위로 넘지 않음");
            Assert.LessOrEqual(contentRight, panelRight, "내용이 패널 오른쪽으로 넘지 않음");
            Assert.GreaterOrEqual(contentBottom, panelTop - 118f, "첫 잔 줄(−118) 위");
            game.CloseMasterBrew();
            yield return null;
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
            Camera.main.aspect = 16f / 9f; // 캡처 RT(16:9)와 같은 비율로 배치 계산(배치 모드 화면 비율 영향 제거)
            game.Hud.ShowBanner("", "NPC가 현수막을 거는 중 · 전투 정지", 60f); // 실제 설치 시작 때 Director가 띄우는 형태
            var show = FairBannerShow.Create(game, d);
            foreach (float ms in new[] { 400f, 1000f, 1500f, 1900f, 2250f, 3100f } /* 2026-11-11 일정: 접근0–1000·설치1000–2500·현수막1250–2250·퇴장2500–3500 */)
            {
                show.Step(Sec(ms, d));
                string n = $"rigger={show.RiggerClip} porter={show.PorterClip} bannerFrame={show.BannerFrame} label={show.LabelVisible} riggerPos={show.RiggerPosition} porterPos={show.PorterPosition}";
                yield return Shot(outDir, $"banner_{ms:0000}ms_720", notes, n, 1280, 720);
                yield return Shot(outDir, $"banner_{ms:0000}ms_1080", notes, n, 1920, 1080);
            }
            // 1b) 동작 검수용 연속 프레임(계약 0~3500ms, 100ms 간격, 720p) — 손/천/발 이동을 프레임 단위로 본다.
            Directory.CreateDirectory(Path.Combine(outDir, "seq"));
            for (int ms = 0; ms <= 3500; ms += 100)
            {
                show.Step(Sec(ms, d));
                string n = $"t={ms} rigger={show.RiggerClip} porter={show.PorterClip} bannerFrame={show.BannerFrame} alpha={show.NpcAlpha:0.00} riggerPos={show.RiggerPosition.x:0.000},{show.RiggerPosition.y:0.000} porterPos={show.PorterPosition.x:0.000},{show.PorterPosition.y:0.000} bannerTop={show.BannerTopWorld.x:0.000},{show.BannerTopWorld.y:0.000} scale={show.NpcScale:0.0000} rFlip={show.RiggerFlipped} pFlip={show.PorterFlipped}";
                yield return Shot(Path.Combine(outDir, "seq"), $"seq_{ms:0000}", notes, n, 1280, 720);
            }
            show.Settle();
            show.SetLabel("박람회 1/3");
            game.Hud.ShowBanner("", "박람회 시작! 진상 손님 12명 막기", 5f); // 실제 Director가 띄우는 형태(제목 없음, 부제만)
            yield return Shot(outDir, "banner_settled_hud_720", notes, $"settled label={show.LabelText} + HUD subtitle only", 1280, 720);
            yield return Shot(outDir, "banner_settled_hud_1080", notes, $"settled label={show.LabelText} + HUD subtitle only", 1920, 1080);
            // 월드 고정 확인: 플레이어를 옆으로 옮겨 카메라가 따라간 뒤에도 현수막은 벽의 같은 칸에 남는다.
            Time.timeScale = 1f;
            var bannerPos = show.BannerTopWorld;
            game.Player.transform.position += new Vector3(5f, 0f, 0f);
            for (int i = 0; i < 30; i++) yield return null;
            Time.timeScale = 0f;
            yield return Shot(outDir, "banner_after_player_move_720", notes, $"player moved +5 → bannerTop {bannerPos} → {show.BannerTopWorld} cam={Camera.main.transform.position.x:0.00}", 1280, 720);
            yield return Shot(outDir, "banner_after_player_move_1080", notes, $"player moved +5 → bannerTop {show.BannerTopWorld}", 1920, 1080);
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

            MasterVfx.ReleaseAll(); // 앞 시험이 남긴 정지 효과 정리(시험 순서 독립)
            // 3) 상성 VFX: 실제 적 옆 적중점에 배치(적중 경로 판정은 AffinityVfx_* 시험이 따로 검증). full/low 각 3종.
            var bug = Enemy.SpawnAt((Vector2)game.Player.Position + new Vector2(2.5f, 0f), EnemyDef.EspressoBug, 50f, game.Player, game.transform);
            yield return null;
            Time.timeScale = 0f;
            Vector2 at = (Vector2)bug.transform.position + new Vector2(0f, 0.3f);
            foreach (bool low in new[] { false, true })
                foreach (var kind in new[] { DrinkKind.Cocktail, DrinkKind.Coffee, DrinkKind.Tea })
                {
                    string clip = MasterArt.AffinityClip(kind, low);
                    var fx = MasterVfx.PlayWorld(clip, at, MasterVfx.AffinityScale(kind), false); // 실제 PlayAffinity와 같은 배율
                    Assert.IsNotNull(fx, clip);
                    fx.Step(low ? 83f + 1f : 83f * 2 + 1f);
                    yield return Shot(outDir, $"affinity_{clip}_720", notes, $"clip={fx.ClipName} frame={fx.CurrentFrame} at={at}", 1280, 720);
                    fx.Release();
                }
            Time.timeScale = 1f;
            Camera.main.ResetAspect();
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
