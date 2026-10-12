using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 실제 GameManager(Initialize 전체 구성)로 "한 프레임"을 흉내 내는 EditMode 통합 테스트.
    /// 픽업 Update가 하는 일(AddIngredient)·K 입력(RequestCoffeeTree)·사망 이벤트를 같은 프레임 안에서 일으킨 뒤,
    /// LateUpdate가 매 프레임 부르는 것과 같은 ResolveEndOfFrame을 호출해 07:43(같은 틱 칵테일→차),
    /// 06:148/07:47(종료 우선, 미선택 보상 폐기), 07:46(보상 사이 Playing 없음), K가 보상 예약을 소모하지 않음을
    /// 검증한다. Unity 엔진의 실제 Update/LateUpdate 호출 순서 자체는 여기서 돌리지 않는다 — 그건
    /// DefaultExecutionOrder 속성 검사와 (미실행) PlayMode 테스트의 몫이다. 저장은 TestSaveIsolation 임시 경로.
    /// </summary>
    public class GameManagerFrameOrderTests
    {
        GameObject root;
        GameObject createdCamera;
        GameManager game;

        [SetUp]
        public void SetUp()
        {
            bool hadCamera = Camera.main != null;
            root = new GameObject("Game");
            game = root.AddComponent<GameManager>();
            game.Initialize(Job.Bartender, WeaponKind.Rapier);
            if (!hadCamera && Camera.main != null) createdCamera = Camera.main.gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (createdCamera != null) Object.DestroyImmediate(createdCamera);
            Time.timeScale = 1f;
        }

        [Test]
        public void SameFrame_TeaPickupBeforeCocktailPickup_OpensCocktailFirst_ThenTeaWithoutPlayingGap()
        {
            game.AddIngredient(IngredientType.Tea, game.TeaGauge.Threshold);          // 차 픽업 Update가 먼저
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold); // 칵테일 픽업 Update가 나중
            Assert.AreEqual(GameState.Playing, game.State, "프레임 도중에는 보상을 열지 않는다");

            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.LevelUp, game.State);
            Assert.AreEqual(CardCategory.Cocktail, game.LastOpenedCardCategory, "07:43 같은 틱은 칵테일 먼저");
            Assert.AreEqual(0f, Time.timeScale);

            game.CardView.Choose(0);
            Assert.AreEqual(GameState.LevelUp, game.State, "07:46 선택 직후 같은 호출에서 다음 보상");
            Assert.AreEqual(CardCategory.Tea, game.LastOpenedCardCategory);
            Assert.AreEqual(0f, Time.timeScale, "보상 사이에 시간이 풀리지 않는다");

            game.CardView.Choose(0);
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void SameFrame_Death_BeatsQueuedReward_AndRewardIsDiscarded()
        {
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual("사망", game.EndReason);
            Assert.IsFalse(game.CardView.IsOpen, "죽은 뒤 선택 화면 없음(06:148, 07:47)");
            game.ResolveEndOfFrame();
            Assert.IsFalse(game.CardView.IsOpen, "다음 프레임에도 열리지 않는다");
        }

        [Test]
        public void SameFrame_Timeout_BeatsQueuedReward()
        {
            game.AddIngredient(IngredientType.Tea, game.TeaGauge.Threshold);
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            game.ResolveEndOfFrame();

            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual("시간 초과", game.EndReason);
            Assert.IsFalse(game.CardView.IsOpen);
        }

        [Test]
        public void KSameFrameAsCardAndNewPoint_DoesNotConsumeAutoTree_CardThenTree()
        {
            game.AddIngredient(IngredientType.Coffee, 10); // 새 포인트 → 자동 트리 1회 예약
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            game.RequestCoffeeTree();                      // 같은 프레임 K
            Assert.AreEqual(GameState.Playing, game.State, "K는 프레임 끝까지 열지 않는다");

            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.LevelUp, game.State, "대기 카드가 K보다 먼저");
            Assert.IsTrue(game.Rewards.TreeAutoOpenArmed, "K가 자동 트리 예약을 소모하지 않았다");

            game.CardView.Choose(0);
            Assert.AreEqual(GameState.SkillSelect, game.State, "카드 다음 같은 호출에서 트리");
            Assert.AreEqual(1, game.CoffeePoints);

            game.SkillView.Continue();
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1, game.CoffeePoints, "계속하기는 포인트 보존");
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.Playing, game.State, "버려진 K 요청이나 자동 예약으로 다시 열리지 않는다");
        }

        [Test]
        public void KAlone_OpensTreeAtEndOfFrame_EvenWithZeroPoints()
        {
            Assert.AreEqual(0, game.CoffeePoints);
            game.RequestCoffeeTree();
            Assert.AreEqual(GameState.Playing, game.State);
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.SkillSelect, game.State);
            Assert.AreEqual(0f, Time.timeScale);
            game.SkillView.Continue();
            Assert.AreEqual(GameState.Playing, game.State);
        }

        [Test]
        public void KSameFrameAsDeath_DoesNotOpenTree()
        {
            game.RequestCoffeeTree();
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.IsFalse(game.SkillView.IsOpen);
        }

        [Test]
        public void KDuringRewardScreen_IsIgnored()
        {
            game.AddIngredient(IngredientType.Cocktail, game.CocktailGauge.Threshold);
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.LevelUp, game.State);
            game.RequestCoffeeTree();
            game.CardView.Choose(0);
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.Playing, game.State, "보상창 중 K는 받지 않는다(08:31 모달)");
        }

        [Test]
        public void GameManager_RunsAfterPickupsAndPlayerLateUpdate()
        {
            // 엔진의 호출 순서 보장은 속성으로만 정해진다 — 픽업/플레이어(기본 0)보다 뒤여야 한다.
            int Order(System.Type t) =>
                t.GetCustomAttributes(typeof(DefaultExecutionOrder), false).Cast<DefaultExecutionOrder>()
                    .Select(a => a.order).DefaultIfEmpty(0).First();
            Assert.Greater(Order(typeof(GameManager)), Order(typeof(IngredientPickup)));
            Assert.Greater(Order(typeof(GameManager)), Order(typeof(PlayerController)));
            Assert.IsNotNull(typeof(GameManager).GetMethod("LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic), "프레임 끝 처리는 LateUpdate에서");
        }

        [Test]
        public void OpenPause_AfterGameOver_IsIgnored_AndNewRunStartsWithoutMusicDuck()
        {
            game.MarkPlayerDiedForTests();
            game.ResolveEndOfFrame();
            Assert.AreEqual(GameState.GameOver, game.State);
            game.OpenPause();
            Assert.IsFalse(game.IsPaused, "결과 화면에서는 일시정지가 열리지 않는다");

            AudioService.SetMusicDucked(true); // 직전 런에서 남은 덕킹
            var next = new GameObject("Game2").AddComponent<GameManager>();
            try
            {
                next.Initialize(Job.Bartender, WeaponKind.Gun);
                Assert.IsFalse(AudioBus.MusicDucked, "새 런은 덕킹 없이 시작한다(09:139)");
            }
            finally { Object.DestroyImmediate(next.gameObject); }
        }
    }
}
