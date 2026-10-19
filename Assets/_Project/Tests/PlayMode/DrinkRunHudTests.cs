using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>hudfix 2026-10-19: 준비 음료 칩·재료 알림은 전투 화면에서만 보이고(카드 선택 위 음료 지식 안내·상세·일시정지·
    /// 조작 안내·결과 화면 위에 올라오지 않음) 전투로 돌아오면 바로 다시 보인다. 긴 이름+여러 효과도 칩 안·화면 안에 다 그려진다.</summary>
    public class DrinkRunHudTests
    {
        // 16자(이름 상한) 공백 없는 이름 + 칵테일 최대 효과 4종(공격력·공격 속도·치명타·최대 체력)
        const string LongName = "한밤의블루큐라소진레몬셰이킹특제";
        static readonly EnemyDef Mob = new EnemyDef { name = "테스트 몹", dropCount = 0, hp = 1f };
        string folder;
        GameManager game;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            folder = Path.Combine(Application.temporaryCachePath, "drink-hud-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            PermanentProgress.SetPathForTests(Path.Combine(folder, "save.json"));
            SettingsService.SetForTests(Path.Combine(folder, "settings.json"), new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            DrinkLab.SetPathForTests(Path.Combine(folder, "drinks.json"));
            CardEducationGuide.SuppressAutomaticForTests = false;
            // 다른 PlayMode 시험과 같이: 시험 장면에 남은 타이틀 화면(별도 캔버스)을 치운다 — 실제 런에서는 런 시작 때 사라진다.
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            Time.timeScale = 1f;
            yield return null;
            CardEducationGuide.SuppressAutomaticForTests = true;
            DrinkLab.SetPathForTests(null);
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath, new MemoryLegacySettingsStore(), new MemoryDisplayAdapter());
            try { Directory.Delete(folder, true); } catch { }
        }

        static DrinkRecipe LongMultiEffect()
        {
            var r = DrinkRecipe.NewDraft(DrinkKind.Cocktail);
            r.technique = (int)DrinkTechnique.Stir;
            r.name = LongName;
            r.steps.Add(new DrinkStep(DrinkIngredients.Gin, 200));
            r.steps.Add(new DrinkStep(DrinkIngredients.Rum, 150));
            r.steps.Add(new DrinkStep(DrinkIngredients.BlueCuracao, 100));
            r.steps.Add(new DrinkStep(DrinkIngredients.Grenadine, 100));
            return r;
        }

        IEnumerator StartWithDrink()
        {
            var craft = DrinkLab.Craft(LongMultiEffect());
            Assert.AreEqual(DrinkLab.CraftStatus.Crafted, craft.Status, craft.Message);
            Assert.AreEqual(DrinkLab.DepartStatus.Reserved, DrinkLab.ReserveForDeparture(out var e), e);
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var s in game.GetComponentsInChildren<EnemySpawner>()) s.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            yield return null;
            Assert.IsNotNull(game.DrinkBuff);
            Assert.GreaterOrEqual(game.DrinkBuff.Active().Count(), 4, "여러 효과 음료");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c); // Screen Space Overlay: 월드 = 화면 픽셀
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>실제 생성된 글자 사각형(화면 픽셀)의 경계. 줄바꿈·잘림 여부와 상관없이 그려지는 모든 글자.</summary>
        static Rect GlyphBounds(Text t)
        {
            var gen = t.cachedTextGenerator;
            Assert.Greater(gen.vertexCount, 0, t.name + " 글자 없음");
            float unitsPerPixel = 1f / t.pixelsPerUnit;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var v in gen.verts)
            {
                var w = t.rectTransform.TransformPoint(v.position * unitsPerPixel);
                minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        static void AssertInside(Rect inner, Rect outer, string what, float eps = 1.5f)
        {
            Assert.IsTrue(inner.xMin >= outer.xMin - eps && inner.xMax <= outer.xMax + eps && inner.yMin >= outer.yMin - eps && inner.yMax <= outer.yMax + eps,
                $"{what}: {inner} 가 {outer} 밖으로 나감");
        }

        static void AssertChipContained(DrinkRunHud hud)
        {
            Canvas.ForceUpdateCanvases();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var chip = ScreenRect(hud.ChipRect);
            AssertInside(chip, screen, "칩 사각형/화면");
            foreach (var t in new[] { hud.ChipTitle, hud.ChipBuff, hud.ChipState })
            {
                Assert.IsFalse(t.resizeTextForBestFit, "글자를 줄여서 맞추지 않는다");
                Assert.GreaterOrEqual(t.fontSize, 20, t.name + " 글자 크기");
                float px = t.fontSize * t.canvas.scaleFactor * hud.ChipRect.localScale.x;
                Assert.GreaterOrEqual(px + 0.01f, Mathf.Min(DrinkRunHud.MinGlyphPx, t.fontSize * t.canvas.scaleFactor * 1.6f), $"{t.name} 화면 글자 {px:0.0}px");
                Assert.LessOrEqual(t.preferredHeight, t.rectTransform.rect.height + 0.5f, t.name + " 필요한 높이 > 상자 높이");
                var g = GlyphBounds(t);
                AssertInside(g, ScreenRect(t.rectTransform), t.name + " 글자/글 상자", 3f); // 글꼴 왼쪽 여백(bearing) 몇 px 허용
                AssertInside(g, chip, t.name + " 글자/칩");
                AssertInside(g, screen, t.name + " 글자/화면");
            }
        }

        [Test]
        public void ReadableScale_NarrowWindowGrowsChip_DesktopUnchanged()
        {
            Assert.AreEqual(1f, DrinkRunHud.ReadableScale(20, 1f));            // 1920×1080
            Assert.AreEqual(1f, DrinkRunHud.ReadableScale(20, 720f / 1080f));  // 1280×720 → 13.3px
            Assert.AreEqual(12f / (20f * (836f / 1920f)), DrinkRunHud.ReadableScale(20, 836f / 1920f), 1e-4f); // 836×705 폭 맞춤
            Assert.AreEqual(1.6f, DrinkRunHud.ReadableScale(20, 0.2f));        // 상한
        }

        [UnityTest]
        public IEnumerator LongNameMultiEffect_AllGlyphsInsideChipAndScreen_EffectsNeverSplit()
        {
            yield return StartWithDrink();
            CardEducationGuide.SuppressAutomaticForTests = true;
            var hud = game.DrinkHud;
            yield return null;
            Assert.IsTrue(hud.ChipVisible);
            StringAssert.Contains(LongName, hud.ChipTitle.text);
            AssertChipContained(hud);
            Assert.Greater(hud.ChipRect.rect.height, DrinkRunHud.ChipMinHeight, "긴 이름+여러 효과면 칩이 높아진다");
            // 효과 하나("공격 속도 +3.6%")는 한 줄 안에 통째로 있다.
            var lines = hud.ChipBuff.text.Split('\n');
            foreach (var s in game.DrinkBuff.Active())
            {
                string item = $"{DrinkRules.StatName(s)} {DrinkBuff.Pct(s, game.DrinkBuff[s])}";
                Assert.IsTrue(lines.Any(l => l.Contains(item)), $"효과 '{item}'가 줄 사이에서 끊김: {hud.ChipBuff.text}");
            }
            Assert.AreEqual(lines.Length, hud.ChipBuff.cachedTextGenerator.lineCount, "묶은 줄이 상자 안에서 다시 줄바꿈되지 않음");
            // 5킬 뒤 상태 문구가 바뀌어도 칩 안.
            for (int i = 0; i < DrinkLab.PreserveKillLimit; i++) game.OnEnemyKilled(Vector2.zero, Mob);
            yield return null;
            StringAssert.Contains("사용됨", hud.ChipStateText);
            AssertChipContained(hud);
        }

        [UnityTest]
        public IEnumerator CardGuideLearningPauseResult_HideChipAndToast_CombatResumeRestoresImmediately()
        {
            FirstRunTutorial.MarkSeen();
            yield return StartWithDrink();
            var hud = game.DrinkHud;
            var buff = game.DrinkBuff.Describe();
            Assert.IsTrue(hud.ChipVisible, "전투 중 칩 표시");
            Assert.Less(hud.transform.GetSiblingIndex(), game.CardView.transform.parent == hud.transform.parent
                ? game.CardView.transform.GetSiblingIndex() : int.MaxValue, "칩 층은 카드 선택 창보다 뒤");

            // 실제 레벨업 → 처음 카드 제안에서 음료 지식 안내(1/4)가 자동으로 열린다.
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            yield return null; yield return null;
            var view = game.CardView;
            Assert.IsTrue(view.IsTutorialOpen, "음료 지식 안내 열림");
            Assert.AreEqual(GameState.LevelUp, game.State);
            Assert.IsFalse(hud.ChipVisible, "안내 위에 칩 없음");
            hud.OnSupplyAward(Vector2.zero, new System.Collections.Generic.List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 1) });
            yield return null;
            Assert.IsFalse(hud.ToastVisible, "안내 위에 재료 알림 없음");
            StringAssert.Contains("음료 재료", hud.ToastText, "알림은 보류(지급은 이미 확정)");

            view.Tutorial.Next(); view.Tutorial.Next(); view.Tutorial.Next(); view.Tutorial.OpenDetail();
            yield return null;
            Assert.IsTrue(view.IsLearningOpen);
            Assert.IsFalse(hud.ChipVisible, "카드 상세(학습) 위에 칩 없음");
            view.CloseLearning();
            yield return null;
            Assert.IsTrue(view.IsOpen);
            Assert.IsFalse(hud.ChipVisible, "카드 선택 위에 칩 없음");
            view.OpenTutorial();
            yield return null;
            Assert.IsFalse(hud.ChipVisible, "다시 연 안내 위에 칩 없음");
            view.CloseLearning();

            view.Choose(0);
            for (int i = 0; i < 120 && game.State != GameState.Playing; i++) yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            yield return null;
            Assert.IsTrue(hud.ChipVisible, "전투로 돌아오면 바로 다시 표시");
            Assert.IsTrue(hud.ToastVisible, "보류된 알림은 전투에서 남은 시간만큼 표시");
            AssertChipContained(hud);

            game.OpenPause();
            yield return null;
            Assert.IsFalse(hud.ChipVisible, "일시정지 위 칩 없음");
            Assert.IsFalse(hud.ToastVisible);
            game.PauseView.TutorialView.Show("닫기", () => { });
            yield return null;
            Assert.IsTrue(game.PauseView.IsTutorialOpen);
            Assert.IsFalse(hud.ChipVisible, "조작 안내 위 칩 없음");
            game.PauseView.CloseTutorial();
            game.ClosePause();
            yield return null;
            Assert.IsTrue(hud.ChipVisible, "일시정지 해제 즉시 표시");

            // 음료·버프·처치 의미는 그대로(숨김은 표시만).
            Assert.AreEqual(PreparedDrink.InRun, DrinkLab.Prepared.state);
            Assert.AreEqual(game.RunId, DrinkLab.Prepared.runId);
            Assert.AreEqual(buff, game.DrinkBuff.Describe());
            StringAssert.Contains($"0/{DrinkLab.PreserveKillLimit}", hud.ChipStateText);

            game.OpenPause();
            game.GiveUp();
            yield return null;
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(hud.ChipVisible, "결과 화면 위 칩 없음");
            Assert.IsFalse(hud.ToastVisible, "결과 화면 위 알림 없음");
            Assert.AreEqual(PreparedDrink.Ready, DrinkLab.Prepared.state, "0킬 포기 = 음료 보존");
        }

        static readonly EnemyDef FinalBoss = new EnemyDef { name = "테스트 최종 보스", dropCount = 0, hp = 10f, boss = BossKind.Final };
        static readonly EnemyDef Elite = new EnemyDef { name = "테스트 정예", dropCount = 0, hp = 10f, archetype = EnemyArchetype.EliteBarrage };

        static GameObject TopHit(Vector2 screen)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            Assert.IsNotNull(es, "EventSystem");
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = screen }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        [UnityTest]
        public IEnumerator LongClearResult_MaterialSummaryStaysInReservedBox_ButtonsReadableAndClickable()
        {
            CardEducationGuide.SuppressAutomaticForTests = true;
            yield return StartWithDrink();
            for (int i = 0; i < 5000; i++) game.OnEnemyKilled(Vector2.zero, Mob);
            for (int i = 0; i < 20; i++) game.OnEnemyKilled(Vector2.zero, Elite);
            int packets = 0;
            foreach (var a in DrinkLab.PendingFor(game.RunId)) packets += a.items.Count;
            Assert.AreEqual(DrinkSupply.NormalBundleCap + 20, packets, "78: 일반 상한 60묶음 + 정예 20(별도)");
            game.OnEnemyKilled(Vector2.zero, FinalBoss);
            var result = game.ResultScreen;
            for (int i = 0; i < 300 && !result.IsOpen; i++) yield return null;
            Assert.IsTrue(result.IsOpen, "결과 화면");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.GreaterOrEqual(game.DrinkSettlement.granted.Count, packets, "정산 기록(출처 단위) 유지");

            var text = result.DrinkStatus;
            var box = ScreenRect(text.rectTransform);
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            Assert.IsFalse(text.text.Contains("\n"), "한 줄 요약: " + text.text);
            Assert.GreaterOrEqual(text.fontSize, ResultView.DrinkSummaryFont);
            Assert.LessOrEqual(text.preferredWidth, text.rectTransform.rect.width + 0.5f, "요약 한 줄이 칸 안: " + text.text);
            Assert.AreEqual(1, text.cachedTextGenerator.lineCount);
            var glyphs = GlyphBounds(text);
            AssertInside(glyphs, box, "요약 글자/칸", 3f);
            AssertInside(glyphs, screen, "요약 글자/화면");
            var all = result.GetComponentsInChildren<RectTransform>(true);
            var stats = ScreenRect(all.First(t => t.name == "StatsPanel"));
            Assert.IsFalse(glyphs.Overlaps(stats), "통계 판과 겹침");
            var award = ScreenRect((RectTransform)result.DrinkAwardButton.transform);
            Assert.GreaterOrEqual(((RectTransform)result.DrinkAwardButton.transform).rect.height, 44f, "획득 내역 버튼 높이(논리 단위)");
            Assert.IsFalse(glyphs.Overlaps(award), "요약 글자와 획득 내역 버튼 겹침");
            AssertInside(award, screen, "획득 내역 버튼/화면");
            foreach (var name in new[] { "RestartButton", "PermanentShopButton", "TitleButton", "BuildDetailButton" })
            {
                var rt = all.First(t => t.name == name);
                var r = ScreenRect(rt);
                Assert.IsFalse(glyphs.Overlaps(r), $"{name}와 요약 글자 겹침");
                Assert.IsFalse(award.Overlaps(r), $"{name}와 획득 내역 버튼 겹침");
                var hit = TopHit(r.center);
                Assert.IsNotNull(hit, name);
                Assert.IsTrue(hit.transform.IsChildOf(rt), $"{name} 가운데를 누르면 {hit.name}이(가) 받음");
            }

            // 실제 포인터 클릭으로 획득 내역 열기 → 재료별 전체 합계(빠짐 없음)·읽을 수 있는 글자·스크롤, 뒤 버튼은 막힘
            var awardHit = TopHit(award.center);
            Assert.IsTrue(awardHit != null && awardHit.transform.IsChildOf(result.DrinkAwardButton.transform), "획득 내역 버튼 명중");
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(awardHit, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = award.center },
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return null;
            var detail = result.AwardDetail;
            Assert.IsTrue(detail.IsOpen, "획득 내역 열림");
            Canvas.ForceUpdateCanvases();
            // 좁은 창에서도 창은 화면 안(여백 24px), 본문은 실제 화면 18px(창 높이가 허락하는 만큼) 이상
            AssertInside(ScreenRect(detail.Panel), new Rect(0, 0, Screen.width, Screen.height), "획득 내역 창/화면");
            AssertInside(ScreenRect((RectTransform)detail.CloseButton.transform), new Rect(0, 0, Screen.width, Screen.height), "닫기 버튼/화면");
            float wantPx = Mathf.Min(DrinkAwardDetailView.TargetBodyPx, DrinkAwardDetailView.BodyFont * (Screen.height - 48f) / DrinkAwardDetailView.PanelHeight);
            foreach (var row in detail.Content.GetComponentsInChildren<Text>(false))
                Assert.GreaterOrEqual(row.fontSize * row.rectTransform.lossyScale.y + 0.3f, Mathf.Min(wantPx, row.fontSize * row.canvas.scaleFactor * 10f), $"{row.name} 실제 {row.fontSize * row.rectTransform.lossyScale.y:0.0}px");
            var totals = DrinkLab.Totals(game.DrinkSettlement.granted);
            var rows = detail.Content.GetComponentsInChildren<RectTransform>(true).Where(t => t.name.StartsWith("Material_")).ToList();
            Assert.AreEqual(totals.Count, rows.Count, "재료 종류마다 한 줄, 빠짐 없음");
            foreach (var kv in totals)
            {
                var def = DrinkIngredients.Get(kv.Key);
                StringAssert.Contains(DrinkRules.Format((int)kv.Value, def.form), detail.AllText, def.name + " 합계");
            }
            StringAssert.Contains($"+{game.DrinkSettlement.researchPoints}", detail.AllText);
            StringAssert.Contains("사용되어", detail.AllText);
            foreach (var t in detail.GetComponentsInChildren<Text>(false))
            {
                Assert.GreaterOrEqual(t.fontSize, 27, t.name + " 글자 크기(논리)");
                Assert.LessOrEqual(t.preferredHeight, t.rectTransform.rect.height + 1f, t.name + " 잘림: " + t.text);
            }
            var restart = ScreenRect(all.First(t => t.name == "RestartButton"));
            var blocked = TopHit(restart.center);
            Assert.IsTrue(blocked != null && blocked.transform.IsChildOf(detail.transform), "창이 열린 동안 뒤 버튼은 눌리지 않음");
            detail.Close();
            yield return null;
            Assert.IsFalse(detail.IsOpen);
            Assert.IsTrue(result.IsOpen);
            var again = TopHit(restart.center);
            Assert.IsTrue(again != null && again.transform.IsChildOf(all.First(t => t.name == "RestartButton")), "닫으면 다시 누를 수 있음");
        }
    }
}
