using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-10-12 Android 터치 실제 런: 조이스틱 포인터 이벤트로 이동, 연구·히든 버튼, 백그라운드 전환 일시정지·저장,
    /// 메뉴 뒤로 가기, 데스크톱에서는 터치 UI 없음.</summary>
    public class MobileTouchPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.Destroy(v.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) Object.Destroy(v.gameObject);
            Time.timeScale = 1f;
            yield return null;
            MobilePlatform.ForceForTests(null);
            SafeAreaFitter.OverrideForTests = null;
        }

        static GameManager BuildTouch()
        {
            MobilePlatform.ForceForTests(true);
            var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            game.GetComponentInChildren<EnemySpawner>().enabled = false;
            game.Director.enabled = false;
            return game;
        }

        static PointerEventData Pointer(Vector2 screen, int id = 7) =>
            new PointerEventData(EventSystem.current) { position = screen, pointerId = id };

        static Vector2 ScreenOf(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return (c[0] + c[2]) * 0.5f; // Screen Space Overlay
        }

        [UnityTest]
        public IEnumerator Desktop_HasNoTouchControls_SchemeToggleVisible()
        {
            MobilePlatform.ForceForTests(false);
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Gun);
            yield return null;
            Assert.IsNull(game.Hud.TouchControls);
            Assert.IsNull(game.Hud.GetComponent<SafeAreaFitter>());
            Assert.IsTrue(game.Hud.transform.Find("MovementScheme").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Joystick_DragMovesPlayer_ReleaseStops()
        {
            var game = BuildTouch();
            yield return null;
            var touch = game.Hud.TouchControls;
            Assert.IsNotNull(touch, "터치 UI");
            Assert.IsFalse(game.Hud.transform.Find("MovementScheme").gameObject.activeSelf, "키보드/마우스 전환 숨김");
            var zone = touch.transform.Find("JoystickZone").GetComponent<JoystickZone>();
            var start = ScreenOf((RectTransform)zone.transform) - new Vector2(0f, 40f);
            float scale = touch.GetComponentInParent<Canvas>().scaleFactor;

            zone.OnPointerDown(Pointer(start));
            zone.OnDrag(Pointer(start + new Vector2(TouchControlsView.JoystickRadius * scale * 2f, 0f)));
            Assert.IsTrue(TouchControlsView.Held);
            Assert.AreEqual(1f, TouchControlsView.Move.x, 1e-3f, "오른쪽 끝까지");
            Vector2 before = game.Player.Position;
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(game.Player.Position.x, before.x + 0.3f, "조이스틱으로 실제 이동");

            zone.OnDrag(Pointer(start + new Vector2(0f, 50f), id: 99)); // 다른 손가락은 무시
            Assert.AreEqual(1f, TouchControlsView.Move.x, 1e-3f);
            zone.OnPointerUp(Pointer(start));
            Assert.IsFalse(TouchControlsView.Held);
            Assert.AreEqual(Vector2.zero, TouchControlsView.Move);
            yield return null;
            Vector2 stopped = game.Player.Position;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(stopped.x, game.Player.Position.x, 0.05f, "놓으면 정지");
        }

        [UnityTest]
        public IEnumerator StudyAndHiddenButtons_UseSameGameEntryPoints()
        {
            var game = BuildTouch();
            yield return null;
            var touch = game.Hud.TouchControls;
            StringAssert.Contains("우림 연구", touch.StudyButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
            touch.StudyButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.AreEqual(GameState.SkillSelect, game.State, "연구 트리 열림(K와 같은 경로)");
            game.SkillView.Continue();
            yield return null;
            Assert.AreEqual(GameState.Playing, game.State);

            game.AddIngredient(IngredientType.Tea, 1000); // 카드 보상 창
            yield return null; yield return null;
            Assert.AreNotEqual(GameState.Playing, game.State);
            Assert.IsFalse(touch.Visible, "보상 창 뒤로 조이스틱·버튼이 비치지 않음");
            for (int i = 0; i < 40 && game.State != GameState.Playing; i++)
            {
                if (game.State == GameState.LevelUp) game.CardView.Choose(0);
                else if (game.State == GameState.SkillSelect) game.SkillView.Continue();
                else if (game.State == GameState.HybridOffer) game.HybridView.Unlock();
                yield return null;
            }
            yield return null;
            Assert.IsTrue(touch.Visible, "전투로 돌아오면 다시 보임");

            touch.HiddenButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(game.Hud.HiddenDetailsOpen, "히든 효과 창(H와 같은 경로)");
            game.RequestEscape(); // 안드로이드 뒤로 = Esc
            Assert.IsFalse(game.Hud.HiddenDetailsOpen);
        }

        [UnityTest]
        public IEnumerator Backgrounded_OpensPause_ReleasesStick_SavesWithoutAbandoning()
        {
            var game = BuildTouch();
            yield return null;
            var zone = game.Hud.TouchControls.transform.Find("JoystickZone").GetComponent<JoystickZone>();
            zone.OnPointerDown(Pointer(ScreenOf((RectTransform)zone.transform)));
            game.HandleAppBackgrounded();
            yield return null;
            Assert.IsTrue(game.IsPaused, "돌아오면 일시정지 상태");
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(TouchControlsView.Held, "백그라운드 전환 시 조이스틱 해제");
            Assert.AreEqual(GameState.Playing, game.State, "런은 계속(포기 집계 아님)");
            Assert.IsNotNull(PermanentProgress.Current.activeRun, "진행 중 런 기록 유지");
            game.HandleAppBackgrounded(); // 이미 일시정지면 그대로
            Assert.IsTrue(game.IsPaused);
        }

        [UnityTest]
        public IEnumerator SafeArea_InsetsHud_OnTouch()
        {
            SafeAreaFitter.OverrideForTests = new Rect(Screen.width * 0.05f, 0f, Screen.width * 0.95f, Screen.height * 0.96f);
            var game = BuildTouch();
            yield return null;
            var hud = (RectTransform)game.Hud.transform;
            Assert.AreEqual(0.05f, hud.anchorMin.x, 1e-3f, "노치 쪽 여백");
            Assert.AreEqual(0.96f, hud.anchorMax.y, 1e-3f);
        }

        [UnityTest]
        public IEnumerator BackRouter_InRun_DefersToGame_MenusUseCloseButtons()
        {
            var game = BuildTouch();
            yield return null;
            Assert.IsNull(MobileBackRouter.HandleBack(), "런 중에는 GameManager가 Esc 처리");
            Object.Destroy(game.gameObject);
            yield return null;

            TitleView.Show();
            yield return null;
            var title = Object.FindFirstObjectByType<TitleView>();
            title.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "CardCodexButton").onClick.Invoke();
            yield return null;
            var codex = Object.FindObjectsByType<CardCodexView>(FindObjectsSortMode.None).First(v => v.IsOpen);
            var handled = MobileBackRouter.HandleBack();
            Assert.IsNotNull(handled, "도감 뒤로 버튼");
            yield return null;
            Assert.IsFalse(codex.IsOpen, "뒤로 가기로 도감 닫힘");
            Assert.IsTrue(title.IsOpen, "타이틀로 복귀");
        }
    }
}
