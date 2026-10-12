using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>08장 "일시정지" (G24) — 전투 완전 정지, 카드/스킬 선택 화면 위에 겹쳐 열리는 오버레이
    /// 스택, 그 아래 화면으로의 키 입력 관통 차단, 런 포기/타이틀로 종료 흐름, Playing 복귀 시 1초
    /// "계속" 카운트다운(08:102)을 검증한다.</summary>
    public class PauseTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            Assert.IsNotNull(GameBootstrap.Build());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenPause_DuringPlaying_FreezesTime_AndClosePause_Resumes()
        {
            var game = GameManager.Instance;

            game.OpenPause();
            Assert.IsTrue(game.IsPaused);
            Assert.AreEqual(0f, Time.timeScale, "전투가 완전히 멈춰야 한다");

            game.ClosePause();
            Assert.IsFalse(game.IsPaused);
            Assert.AreEqual(GameState.Playing, game.State);

            // 08:102 — Playing으로 돌아갈 땐 곧장 풀리지 않고 1초 "계속" 카운트다운 동안 계속 멈춰있는다.
            Assert.IsTrue(game.IsResuming);
            Assert.AreEqual(0f, Time.timeScale, "카운트다운 동안엔 이동·피해가 계속 정지돼야 한다");
            Assert.IsTrue(game.PauseView.IsResumeCountdownVisible);

            yield return new WaitForSecondsRealtime(1.1f);

            Assert.IsFalse(game.IsResuming);
            Assert.AreEqual(1f, Time.timeScale, "카운트다운이 끝나면 다시 흘러야 한다");
            Assert.IsFalse(game.PauseView.IsResumeCountdownVisible);
        }

        [UnityTest]
        public IEnumerator ClosePause_ReturningToCardSelect_SkipsCountdown()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, 999);
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);

            game.OpenPause();
            game.ClosePause();

            Assert.IsFalse(game.IsResuming, "08:102 — 선택 화면으로 돌아갈 땐 카운트다운이 없어야 한다");
            Assert.IsFalse(game.PauseView.IsResumeCountdownVisible);
            Assert.AreEqual(0f, Time.timeScale, "카드 선택 화면 자체의 정지 상태는 그대로 유지돼야 한다");
        }

        [UnityTest]
        public IEnumerator ReopeningPauseDuringCountdown_CancelsCountdown_AndReopensPause()
        {
            var game = GameManager.Instance;
            game.OpenPause();
            game.ClosePause();
            Assert.IsTrue(game.IsResuming);

            game.OpenPause();

            Assert.IsFalse(game.IsResuming, "다시 Pause를 열면 카운트다운은 취소돼야 한다");
            Assert.IsTrue(game.IsPaused);
            Assert.IsFalse(game.PauseView.IsResumeCountdownVisible);
            Assert.AreEqual(0f, Time.timeScale);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenPause_OverCardSelect_KeepsCardScreenOpen_AndRestoresItOnClose()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, 999); // 강제로 레벨업 → 카드 선택 화면 오픈
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);
            Assert.IsTrue(game.CardView.IsOpen);

            game.OpenPause();

            Assert.IsTrue(game.CardView.IsOpen, "08:16/102 — 선택 화면 위에 겹쳐 열려야지 그 화면을 닫으면 안 된다");
            Assert.AreEqual(0f, Time.timeScale);

            game.ClosePause();

            Assert.IsTrue(game.CardView.IsOpen, "닫으면 원래 있던 선택 화면 그대로 복원돼야 한다");
            Assert.AreEqual(0f, Time.timeScale, "선택 화면 자체가 요구하는 정지 상태는 그대로 유지돼야 한다");
            Assert.AreEqual(GameState.LevelUp, game.State);
        }

        [UnityTest]
        public IEnumerator Paused_CardSelectHotkey_DoesNotLeakThrough()
        {
            var game = GameManager.Instance;
            game.AddIngredient(IngredientType.Cocktail, 999);
            yield return null;
            int ownedBefore = game.Cards.CountOwned(CardCategory.Cocktail);

            game.OpenPause();
            game.CardView.Choose(0); // Pause 아래로는 "선택 자체"도 막혀야 한다(키/클릭 모두)
            yield return null;

            Assert.IsTrue(game.CardView.IsOpen, "Pause 중엔 선택이 접수되면 안 된다");
            Assert.AreEqual(ownedBefore, game.Cards.CountOwned(CardCategory.Cocktail));
            Assert.AreEqual(GameState.LevelUp, game.State);
        }

        [UnityTest]
        public IEnumerator GiveUp_EndsRunAsFailure_WithGiveUpReason()
        {
            var game = GameManager.Instance;
            game.OpenPause();

            game.GiveUp();

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(game.Cleared);
            Assert.AreEqual("포기", game.EndReason);
            Assert.IsFalse(game.IsPaused, "런이 끝났으니 Pause 오버레이도 같이 닫혀야 한다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkillTreeButton_DisabledOverCardSelect_WithReason_AndEnabledWhilePlaying()
        {
            var game = GameManager.Instance;

            game.OpenPause();
            Assert.IsTrue(game.PauseView.SkillTreeButton.interactable, "08:101 — Playing 중엔 커피 트리 이용 가능");
            Assert.AreEqual("", game.PauseView.SkillTreeReason);
            game.ClosePause();
            // 2026-10-06: Playing 복귀는 08:102 1초 "계속" 카운트다운을 거치고, 그동안 보상 처리도 멈춘다
            // (GameManager.LateUpdate). 이 테스트는 카운트다운 도입 전 작성돼 바로 다음 프레임 LevelUp을 기대했다.
            Assert.IsTrue(game.IsResuming, "Playing으로 돌아갈 때는 1초 카운트다운");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (game.IsResuming && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(game.IsResuming, "카운트다운은 실제 시간 1초 뒤 끝나야 한다");
            Assert.AreEqual(GameState.Playing, game.State);

            game.AddIngredient(IngredientType.Cocktail, 999); // 카드 선택 화면 강제 오픈
            yield return null;
            Assert.AreEqual(GameState.LevelUp, game.State);

            game.OpenPause();
            Assert.IsFalse(game.PauseView.SkillTreeButton.interactable, "08:101 — 서로 다른 보상창 중첩 금지");
            Assert.AreNotEqual("", game.PauseView.SkillTreeReason, "비활성은 사유문구도 같이 보여야 한다(08:50)");
        }

        [UnityTest]
        public IEnumerator OpenSkillTreeFromPause_KeepsPauseOpenUnderneath_AndClosesOnlyTheTree()
        {
            var game = GameManager.Instance;
            game.OpenPause();

            game.SkillView.ShowReadOnly(game.Skills, () => game.CoffeePoints, () => { });
            Assert.IsTrue(game.SkillView.IsOpen);
            Assert.IsTrue(game.PauseView.IsOpen, "커피 트리도 Pause 위 오버레이라 Pause 자체는 유지돼야 한다");
            Assert.IsTrue(game.IsPaused);

            game.PauseView.CloseSkillTree();
            Assert.IsFalse(game.SkillView.IsOpen);
            Assert.IsTrue(game.PauseView.IsOpen);
            Assert.IsTrue(game.IsPaused);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TutorialButton_OpensReplay_KeepsPauseOpen_AndEscClosesOnlyTheTutorial()
        {
            var game = GameManager.Instance;
            game.OpenPause();

            game.PauseView.TutorialView.Show("닫기", () => { });
            Assert.IsTrue(game.PauseView.IsTutorialOpen);
            Assert.IsTrue(game.PauseView.IsOpen, "조작 안내도 Pause 위 오버레이라 Pause는 유지돼야 한다");
            Assert.IsTrue(game.IsPaused);

            game.PauseView.CloseTutorial();
            Assert.IsFalse(game.PauseView.IsTutorialOpen);
            Assert.IsTrue(game.PauseView.IsOpen);
            Assert.IsTrue(game.IsPaused);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuitToTitle_SavesMaterials_DestroysGame_AndShowsTitle()
        {
            var game = GameManager.Instance;
            game.OpenPause();

            game.QuitToTitle();
            yield return null;

            Assert.IsNull(GameManager.Instance, "타이틀로 종료하면 이번 런의 GameManager는 사라져야 한다");
            var title = Object.FindFirstObjectByType<TitleView>();
            Assert.IsNotNull(title, "타이틀 화면으로 이어져야 한다(08:100 '저장 후 Title')");
            Object.DestroyImmediate(title.gameObject);
        }
    }
}
