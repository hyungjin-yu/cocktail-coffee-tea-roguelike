using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-15 플레이 피드백 첫 묶음(2·3·6·8·9·10·17·18·22) 회귀 시험. 화면 판정(그림 판독·잘림)은 실제 빌드 캡처로 따로 확인한다.
    /// </summary>
    public class FeedbackBatch1Tests
    {
        readonly List<Object> created = new List<Object>();
        string tempSave;

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            if (tempSave != null)
            {
                PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
                try { File.Delete(tempSave); File.Delete(tempSave + ".bak"); } catch { }
                tempSave = null;
            }
            MobilePlatform.ForceForTests(false);
        }

        Transform Canvas()
        {
            var go = new GameObject("C", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(1920f, 1080f);
            created.Add(go);
            return go.transform;
        }

        static Rect WorldRect(Transform t)
        {
            var c = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        void FreshSave()
        {
            tempSave = Path.Combine(Application.temporaryCachePath, $"cct_fb1_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempSave);
        }

        // ── 8: 레벨업 카드 순번 제거, 1/2/3 키 안내는 화면 공통 한 줄 ──
        [Test]
        public void CardSelect_NoOrdinalOnCards_KeyHintBelowCards_TouchHidesHint()
        {
            MobilePlatform.ForceForTests(false);
            var view = CardSelectView.Create(Canvas());
            for (int i = 1; i <= 3; i++)
            {
                var slot = view.transform.Find("Card" + i);
                Assert.IsNull(slot.Find("Key"), "카드 왼쪽 위 순번 칸 없음");
                foreach (var t in slot.GetComponentsInChildren<Text>(true))
                    StringAssert.DoesNotContain($"[{i}]", t.text, "카드 안에 [n] 글 없음");
            }
            Assert.AreEqual(CardSelectView.KeyHintText, view.KeyHintLabel);
            var hint = view.transform.Find("KeyHint");
            foreach (var n in new[] { "Card1", "Card2", "Card3" })
                Assert.IsFalse(WorldRect(hint).Overlaps(WorldRect(view.transform.Find(n))), "안내 줄은 카드 밖");
            Assert.IsFalse(WorldRect(hint).Overlaps(WorldRect(view.transform.Find("Card2/TargetInfluence"))), "목표 진행 띠와 겹치지 않음");
            Assert.LessOrEqual(Mathf.Abs(((RectTransform)hint).anchoredPosition.y) + 18f, 540f - 24f, "화면 안(아래 여백)");

            MobilePlatform.ForceForTests(true);
            var touch = CardSelectView.Create(Canvas());
            Assert.AreEqual("", touch.KeyHintLabel, "터치 UI는 키 안내 없음");
        }

        // ── 9: 제목 → 게임 효과(크게) → 지식·해석(작게) ──
        [Test]
        public void CardSelect_EffectLargerThanKnowledge_AllEffectsFitTwoLines_NoAutoShrink()
        {
            var view = CardSelectView.Create(Canvas());
            var slot = view.transform.Find("Card1");
            var desc = slot.Find("Desc").GetComponent<Text>();
            var fact = slot.Find("RealWorldFact").GetComponent<Text>();
            var conn = slot.Find("GameConnection").GetComponent<Text>();
            Assert.Greater(desc.fontSize, fact.fontSize + 6, "효과 글이 지식 글보다 확실히 큼");
            Assert.AreEqual(fact.fontSize, conn.fontSize);
            Assert.GreaterOrEqual(fact.fontSize, 20, "보조 글도 720p 13px 이상");
            Assert.IsFalse(desc.resizeTextForBestFit, "긴 효과를 자동 축소하지 않는다");
            Assert.IsFalse(WorldRect(desc.transform).Overlaps(WorldRect(fact.transform)), "효과 칸과 지식 칸 비겹침");
            Assert.IsFalse(WorldRect(desc.transform).Overlaps(WorldRect(slot.Find("Name"))), "효과 칸과 이름판 비겹침");
            Assert.IsFalse(WorldRect(fact.transform).Overlaps(WorldRect(conn.transform)));
            Assert.IsFalse(WorldRect(conn.transform).Overlaps(WorldRect(slot.Find("Learn"))), "해석 칸과 지식 버튼 비겹침");
            // 모든 카드의 게임 효과가 32 크기 2줄 상자 안에 다 그려진다(1080·720 배율).
            var problems = new List<string>();
            foreach (var card in CardDatabase.LoadAll())
            {
                desc.text = CardEducationSummary.Effect(card);
                foreach (float scale in new[] { 1f, 2f / 3f })
                {
                    var s = desc.GetGenerationSettings(desc.rectTransform.rect.size);
                    s.scaleFactor = scale;
                    var g = new TextGenerator();
                    g.Populate(desc.text, s);
                    int shown = g.characterCountVisible;
                    s.verticalOverflow = VerticalWrapMode.Overflow;
                    g.Populate(desc.text, s);
                    if (shown < g.characterCountVisible) problems.Add($"{card.id} '{desc.text}' @{scale:0.00}");
                }
            }
            Assert.IsEmpty(problems, string.Join(" / ", problems));
        }

        // ── 10: 출처 분리 ──
        [Test]
        public void Sources_ParsedFromPrimaryAndAdditional_ShortHosts()
        {
            Assert.AreEqual("diageobaracademy.com", CardSourceList.ShortHost("https://www.diageobaracademy.com/en-zz/home/explore-all-categories/liqueurs-bitters-and-vermouth"));
            Assert.AreEqual("lpi.oregonstate.edu", CardSourceList.ShortHost("https://lpi.oregonstate.edu/mic/food-beverages/tea?x=1"));
            Assert.AreEqual("", CardSourceList.ShortHost("not a url"));
            int multi = 0;
            foreach (var card in CardDatabase.LoadAll())
            {
                var list = CardSourceList.For(card);
                Assert.GreaterOrEqual(list.Count, 1, card.id + " 출처 1개 이상");
                Assert.AreEqual(card.sourceUrl, list[0].url, card.id + " 주 출처 URL 보존");
                foreach (var e in list)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(e.title), card.id + " 출처 이름");
                    if (!string.IsNullOrEmpty(e.url)) StringAssert.StartsWith("http", e.url);
                    Assert.IsFalse(e.title.StartsWith("http"), card.id + " 이름 자리에 원문 URL을 쓰지 않음");
                }
                if (list.Count > 1) multi++;
                string body = CardEducationData.Body(card);
                StringAssert.DoesNotContain("http", body, card.id + " 본문에는 URL이 없다");
                StringAssert.StartsWith(body, CardEducationData.Detail(card), "본문 = 이전 상세의 출처 앞부분 그대로");
            }
            Assert.GreaterOrEqual(multi, 3, "추가 출처가 있는 카드(캄파리·베르무트·홍차)");
        }

        [Test]
        public void EducationDetail_SourcesBelowBody_SmallerFont_CopyButtonCopiesFullUrl()
        {
            var view = CardEducationView.Create(Canvas(), null);
            var card = CardDatabase.LoadAll().First(c => CardSourceList.For(c).Count >= 2);
            string copied = null;
            var prev = CardSourceList.Copier;
            CardSourceList.Copier = s => copied = s;
            try
            {
                view.Show(card);
                var src = view.SourcesRect;
                Assert.IsNotNull(src, "출처 영역");
                var body = view.BodyRect;
                Assert.LessOrEqual(WorldRect(src).yMax, WorldRect(body).yMin + 0.01f, "출처는 본문 아래(겹치지 않음)");
                Assert.Less(CardSourceList.TitleFont, body.GetComponent<Text>().fontSize, "출처는 본문보다 작은 보조 글꼴");
                Assert.GreaterOrEqual(CardSourceList.HostFont, 22, "읽을 수 있는 최소 크기(720p ≈15px)");
                Assert.AreEqual(CardSourceList.For(card).Count(e => !string.IsNullOrEmpty(e.url)), view.SourceButtons.Count);
                foreach (var t in src.GetComponentsInChildren<Text>(true))
                    StringAssert.DoesNotContain("https://", t.text, "긴 URL 원문은 화면에 늘어놓지 않음");
                view.SourceButtons[0].onClick.Invoke();
                Assert.AreEqual(card.sourceUrl, copied, "복사 = 원문 URL 전체");
                // 다시 열어도 출처 영역이 쌓이지 않는다.
                view.Show(card);
                Assert.AreEqual(1, view.transform.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Sources" && t.gameObject.activeSelf));
            }
            finally { CardSourceList.Copier = prev; }
        }

        // ── 2·3: 카드 도감 그림 칸·잠금 표현 ──
        [Test]
        public void CardCodex_ArtCellLarge_LockedShowsSilhouetteBadgeAndHint_DiscoveredShowsColour()
        {
            FreshSave();
            var codex = CardCodexView.Create(Canvas(), null);
            codex.Show();
            var content = codex.transform.Find("ScrollArea/Viewport/Content");
            var row = content.Find("Row_dry_gin");
            var icon = row.Find("ArtCell/EducationIcon").GetComponent<Image>();
            Assert.IsTrue(icon.enabled && icon.sprite != null, "미발견도 그림 칸은 보인다(실루엣)");
            Assert.GreaterOrEqual(((RectTransform)row.Find("ArtCell")).sizeDelta.y, 104f, "그림 칸 104 이상(720p ≈70px, 이전 25px)");
            Assert.Less(icon.color.r + icon.color.g + icon.color.b, 0.3f, "미발견 = 어두운 실루엣");
            Assert.IsTrue(row.Find("LockBadge").gameObject.activeSelf, "미발견 배지(색만으로 구분하지 않음)");
            Assert.IsNotNull(row.Find("LockBadge/LockIcon"), "2026-11-16 최종 아트: 미발견 칩에 자물쇠 그림(ui_lock_badge)");
            Assert.AreEqual("???", codex.GetCardText("dry_gin").name, "이름 비공개 유지");
            Assert.AreEqual(CardCodexView.LockedHint, codex.GetCardText("dry_gin").desc);
            Assert.IsTrue(codex.OpenDetailFor("dry_gin"), "미획득 칸도 안내 상세를 연다");
            StringAssert.Contains(CardCodexView.LockedHint, codex.DetailBodyText);
            StringAssert.DoesNotContain(CardDatabase.LoadAll().First(c => c.id == "dry_gin").displayName, codex.DetailBodyText + codex.DetailTitleText, "정체 비공개");
            codex.HideDetail();

            PermanentProgress.DiscoverCard("dry_gin");
            codex.Show();
            Assert.AreEqual(Color.white, icon.color, "획득 = 원색");
            Assert.IsFalse(row.Find("LockBadge").gameObject.activeSelf);
            StringAssert.Contains("드라이", codex.GetCardText("dry_gin").name);
            // 그림 칸은 글과 겹치지 않는다.
            foreach (var n in new[] { "Name", "Desc", "Flavor" })
                Assert.IsFalse(WorldRect(row.Find("ArtCell")).Overlaps(WorldRect(row.Find(n))), "그림 칸과 " + n + " 비겹침");
        }

        [Test]
        public void HiddenSkills_StayHiddenInCodex_NoAnswerLeak()
        {
            FreshSave();
            var codex = CardCodexView.Create(Canvas(), null);
            codex.Show();
            for (int i = 0; i < codex.HiddenRowsForTests; i++)
                StringAssert.StartsWith("???", codex.HiddenNameTextForTests(i), "히든은 발견 전 정답 비공개");
        }

        // ── 6: 재료 도감 검색·필터 판정 ──
        [Test]
        public void IngredientCodex_SearchByNameOrTaste_KindAndSeenFilters()
        {
            var gin = DrinkIngredients.All.First(d => d.use == DrinkUse.Cocktail || d.use == DrinkUse.Both);
            Assert.IsTrue(DrinkLabView.CodexMatches(gin, "", DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.All, false));
            Assert.IsTrue(DrinkLabView.CodexMatches(gin, gin.name.Substring(0, 1), DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.All, false), "이름 일부 검색");
            Assert.IsTrue(DrinkLabView.CodexMatches(gin, "  " + gin.name + " ", DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.All, false), "앞뒤 공백 무시");
            string tastePiece = gin.taste.Substring(0, Mathf.Min(3, gin.taste.Length));
            Assert.IsTrue(DrinkLabView.CodexMatches(gin, tastePiece, DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.All, true), "맛·향 설명 검색");
            Assert.IsFalse(DrinkLabView.CodexMatches(gin, "존재하지않는재료이름xyz", DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.All, true), "빈 결과");
            Assert.IsFalse(DrinkLabView.CodexMatches(gin, "", DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.Seen, false), "발견 필터: 미발견 제외");
            Assert.IsTrue(DrinkLabView.CodexMatches(gin, "", DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.Unseen, false));
            Assert.IsFalse(DrinkLabView.CodexMatches(gin, "", DrinkLabView.CodexKind.All, DrinkLabView.CodexSeen.Unseen, true));
            foreach (var d in DrinkIngredients.All)
            {
                Assert.AreEqual(d.UsableIn(DrinkKind.Tea), DrinkLabView.CodexMatches(d, "", DrinkLabView.CodexKind.Tea, DrinkLabView.CodexSeen.All, true), d.id + " 차 필터");
                Assert.AreEqual(d.UsableIn(DrinkKind.Coffee), DrinkLabView.CodexMatches(d, "", DrinkLabView.CodexKind.Coffee, DrinkLabView.CodexSeen.All, true), d.id + " 커피 필터");
                Assert.AreEqual(d.UsableIn(DrinkKind.Cocktail), DrinkLabView.CodexMatches(d, "", DrinkLabView.CodexKind.Cocktail, DrinkLabView.CodexSeen.All, true), d.id + " 칵테일 필터");
            }
            int coffee = DrinkIngredients.All.Count(d => DrinkLabView.CodexMatches(d, "", DrinkLabView.CodexKind.Coffee, DrinkLabView.CodexSeen.All, true));
            Assert.Greater(coffee, 0);
            Assert.Less(coffee, DrinkIngredients.All.Count);
        }

        // ── 17·18: 우림 연구 노드 순번 제거·그림 세로 가운데 ──
        [Test]
        public void StudyTree_NoOrdinalOnNodes_ArtVerticallyCentered_KeyHintOnlyWhenAllocating()
        {
            var canvas = Canvas();
            var view = SkillTreeView.Create(canvas);
            var skills = DefaultSkills.CreateRuntime();
            try
            {
                int points = 1;
                view.Show(new SkillTree(skills), () => points, n => false, () => { });
                Assert.AreEqual(SkillTreeView.KeyHintText, view.KeyHintLabel, "배분 모드: 화면 아래 단축키 안내");
                var nodes = view.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Node_")).ToList();
                Assert.AreEqual(15, nodes.Count);
                int withArt = 0;
                foreach (var b in nodes)
                {
                    Assert.IsNull(b.transform.Find("Key"), b.name + ": 왼쪽 위 순번 없음");
                    foreach (var t in b.GetComponentsInChildren<Text>(true))
                        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(t.text, @"^\[\d\]$"), b.name + " 순번 글");
                    var art = b.transform.Find("NodeArt") as RectTransform;
                    if (art == null) continue;
                    withArt++;
                    var nr = WorldRect(b.transform); var ar = WorldRect(art);
                    Assert.AreEqual(nr.center.y, ar.center.y, 0.6f, b.name + ": 그림 세로 가운데");
                    Assert.IsTrue(nr.Contains(ar.min) && nr.Contains(ar.max), b.name + ": 그림은 노드 안");
                    foreach (var n in new[] { "Name", "Desc", "Status" })
                        Assert.IsFalse(ar.Overlaps(WorldRect(b.transform.Find(n))), b.name + " 그림과 " + n + " 비겹침");
                }
                Assert.Greater(withArt, 0, "승인 그림이 있는 노드");
                var hint = view.transform.Find("KeyHint");
                Assert.IsFalse(WorldRect(hint).Overlaps(WorldRect(view.transform.Find("CloseButton"))), "안내는 재개 버튼과 겹치지 않음");
                foreach (var b in nodes) Assert.IsFalse(WorldRect(hint).Overlaps(WorldRect(b.transform)), "안내는 노드와 겹치지 않음 " + b.name);
                Assert.LessOrEqual(WorldRect(hint).xMax, 0.5f * 1274f + 0.01f, "가장 좁은 화면비(가로 1274)에서도 안");

                view.Continue();
                view.ShowReadOnly(new SkillTree(skills), () => 0, () => { });
                Assert.AreEqual("", view.KeyHintLabel, "조회 전용은 단축키가 동작하지 않으므로 안내 없음");
            }
            finally { foreach (var s in skills) if (s != null) Object.DestroyImmediate(s); }
        }

        // ── 22: 종료 화면 버튼 문구 ──
        [Test]
        public void Result_BuildButtonSaysGrowthAndCombos_DetailHeadingMatchesScope()
        {
            var result = ResultView.Create(Canvas(), null);
            var label = result.transform.Find("BuildDetailButton/Label").GetComponent<Text>();
            Assert.AreEqual("성장·조합 보기", label.text);
            Assert.AreEqual(ResultView.BuildDetailLabel, label.text);
            foreach (var t in result.GetComponentsInChildren<Text>(true)) StringAssert.DoesNotContain("이번 판 빌드", t.text);
            StringAssert.Contains("획득 카드와 완성 조합", RunDetailView.DetailHeading);
        }
    }
}
