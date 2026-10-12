using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>직업 선택 화면의 "카드 도감" 버튼 → CardCodexView, 그리고 실제 카드 선택 시 도감에
    /// 기록되는지. 실제 세이브 파일은 절대 건드리지 않도록 이 테스트만의 임시 경로로 리다이렉트한다.</summary>
    public class CardCodexViewTests
    {
        string tempPath;

        static void DestroyAllSelectScreens()
        {
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
            // AutoStart가 씬 시작 시 CharacterSelectView 대신 TitleView를 띄우므로(2026-09-28) 같이 치운다.
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyAllSelectScreens();
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_codexview_{System.Guid.NewGuid():N}.json");
            PermanentProgress.SetPathForTests(tempPath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            DestroyAllSelectScreens();
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath); // 세션 공용 경로로 복원
            if (File.Exists(tempPath)) File.Delete(tempPath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CodexButton_OpensCodex_AndHidesJobStep()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;

            Assert.IsNotNull(view.CardCodexButton);
            Assert.IsFalse(view.CodexView.IsOpen);

            view.CardCodexButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(view.CodexView.IsOpen, "버튼을 누르면 도감이 열려야 한다");
            Assert.Greater(view.CodexView.EntryCount, 0, "카드 데이터베이스의 모든 카드가 항목으로 나와야 한다");
            Assert.Greater(view.CodexView.SkillEntryCount, 0, "영구 강화 노드도 항목으로 나와야 한다");
        }

        [UnityTest]
        public IEnumerator Tabs_FilterOnlyTheMatchingCardColumn()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.CardCodexButton.onClick.Invoke();
            yield return null;
            var codex = view.CodexView;

            // 08:95 기본값 "전체" — 세 카드 열이 모두 보여야 한다.
            Assert.IsTrue(codex.IsCocktailColumnVisible);
            Assert.IsTrue(codex.IsTeaColumnVisible);
            Assert.IsTrue(codex.IsHybridColumnVisible);

            codex.SelectCocktailTab();
            Assert.IsTrue(codex.IsCocktailColumnVisible);
            Assert.IsFalse(codex.IsTeaColumnVisible);
            Assert.IsFalse(codex.IsHybridColumnVisible);

            codex.SelectHybridTab();
            Assert.IsFalse(codex.IsCocktailColumnVisible);
            Assert.IsFalse(codex.IsTeaColumnVisible);
            Assert.IsTrue(codex.IsHybridColumnVisible);

            codex.SelectAllTab();
            Assert.IsTrue(codex.IsCocktailColumnVisible);
            Assert.IsTrue(codex.IsTeaColumnVisible);
            Assert.IsTrue(codex.IsHybridColumnVisible);
        }

        [UnityTest]
        public IEnumerator HybridCard_ShowsNameAndUnlockCondition_EvenBeforeDiscovery()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.CardCodexButton.onClick.Invoke();
            yield return null;

            var (name, desc) = view.CodexView.GetCardText("hybrid_tea_martini");

            Assert.IsNotNull(name, "07:114 티니 보우 카드가 도감에 있어야 한다");
            Assert.IsTrue(name.Contains("티니 보우"), "07:167 — 하이브리드는 미발견이어도 '???' 대신 이름이 보여야 한다");
            Assert.IsTrue(desc.Contains("레이피어") && desc.Contains("3"),
                "07:167 — 해금 조건(레이피어: 칵테일3 + 차3)이 보여야 한다");
        }

        [UnityTest]
        public IEnumerator HybridCard_ShowsRealDescription_AfterDiscovery()
        {
            PermanentProgress.DiscoverCard("hybrid_tea_martini");
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.CardCodexButton.onClick.Invoke();
            yield return null;

            var (_, desc) = view.CodexView.GetCardText("hybrid_tea_martini");

            Assert.IsFalse(desc.Contains("해금 조건"), "발견 후엔 조건 문구 대신 실제 효과 설명이 보여야 한다");
        }

        [UnityTest]
        public IEnumerator CodexShows_UnlockedPermanentSkill_AndCollectedMaterials()
        {
            PermanentProgress.AddSpecialty(3);
            PermanentProgress.UnlockPermanentSkill("bean_cupper");
            PermanentProgress.AddMaterialsCollected(12, 7, 2);

            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.CardCodexButton.onClick.Invoke();
            yield return null;

            var materialsRt = view.CodexView.transform.Find("Materials");
            Assert.IsNotNull(materialsRt, "재료 줄이 있어야 한다");

            var cocktailText = materialsRt.Find("Material_칵테일 재료/Header").GetComponent<UnityEngine.UI.Text>();
            Assert.IsTrue(cocktailText.text.Contains("12"), "칵테일 재료 누적 수집량이 표시되어야 한다");

            var teaText = materialsRt.Find("Material_찻잎/Header").GetComponent<UnityEngine.UI.Text>();
            Assert.IsTrue(teaText.text.Contains("7"));

            var beanText = materialsRt.Find("Material_커피 원두/Header").GetComponent<UnityEngine.UI.Text>();
            Assert.IsTrue(beanText.text.Contains("2"));

            var skillRow = view.CodexView.transform.Find("ScrollArea/Viewport/Content/SkillRow_bean_cupper/Name")
                .GetComponent<UnityEngine.UI.Text>();
            Assert.AreEqual("원두 감별사", skillRow.text, "해금된 영구 강화 노드는 이름이 그대로 보여야 한다(비밀 아님)");
        }

        [UnityTest]
        public IEnumerator CodexShows_DiscoveredVariety_ByName_AndUndiscoveredAsQuestionMarks()
        {
            var known = MaterialVarieties.Cocktail[0];
            PermanentProgress.DiscoverMaterialVariety(known.id);

            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.CardCodexButton.onClick.Invoke();
            yield return null;

            var varietyText = view.CodexView.transform.Find("Materials/Material_칵테일 재료/Varieties")
                .GetComponent<UnityEngine.UI.Text>();

            Assert.IsTrue(varietyText.text.Contains(known.displayName), "발견한 품종은 이름이 보여야 한다");
            Assert.IsTrue(varietyText.text.Contains("???"), "아직 못 본 품종은 ???로 보여야 한다");

            var otherNames = MaterialVarieties.Cocktail.Where(v => v.id != known.id).Select(v => v.displayName);
            foreach (var name in otherNames)
                Assert.IsFalse(varietyText.text.Contains(name), $"아직 안 나온 품종({name})의 이름이 그대로 보이면 안 된다");
        }

        [UnityTest]
        public IEnumerator ChoosingACard_RecordsItInTheCodex_AndPersistsAcrossRestarts()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            yield return null;

            Assert.AreEqual(GameState.LevelUp, game.State);
            game.CardView.Choose(0);
            yield return null;

            Assert.AreEqual(1, PermanentProgress.Current.discoveredCardIds.Count,
                "카드를 선택하면 도감에 즉시 기록되어야 한다");

            // 재시작해도(=다음 런에서도) 도감 기록이 유지되는지 확인. Restart()는 GameBootstrap.RestartToSelect()를
            // 통해 스스로 CharacterSelectView를 띄우므로, 여기서 또 Show()를 불러 중복 생성하지 않는다.
            game.Restart();
            yield return null;
            var view = Object.FindFirstObjectByType<CharacterSelectView>();
            Assert.IsNotNull(view, "재시작하면 직업 선택 화면이 자동으로 떠야 한다");
            view.CardCodexButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, PermanentProgress.Current.discoveredCardIds.Count);
        }
    }
}
