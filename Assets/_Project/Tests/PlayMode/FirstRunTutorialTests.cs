using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>08:70~72 "첫 런 안내" (2026-09-28, Codex 오디오/아트와 무관). 직업/무기를 고른 뒤,
    /// 처음 한 번만 전투 시작 직전에 뜨는지/닫아야 런이 시작되는지를 확인한다.</summary>
    public class FirstRunTutorialTests
    {
        static void DestroyStrayScreens()
        {
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
            foreach (var v in Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            DestroyStrayScreens();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            DestroyStrayScreens();
            FirstRunTutorial.MarkSeen(); // 이후 다른 테스트 파일이 우연히 이 경로를 타도 안내에 안 걸리게
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstTime_ShowsTutorialBeforeBuild_DismissingItStartsTheRun_AndMarksSeen()
        {
            FirstRunTutorial.ResetForTests();
            GameBootstrap.RestartToSelect();
            yield return null;

            var select = Object.FindFirstObjectByType<CharacterSelectView>();
            Assert.IsNotNull(select);
            select.BartenderButton.onClick.Invoke();
            yield return null;
            select.WeaponButtonA.onClick.Invoke(); select.StartButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(GameManager.Instance, "안내를 닫기 전엔 런이 시작되면 안 된다");
            var tutorial = Object.FindFirstObjectByType<FirstRunTutorialView>();
            Assert.IsNotNull(tutorial, "08:70~72 — 전투 시작 직전에 안내가 떠야 한다");
            Assert.IsTrue(tutorial.IsOpen);

            tutorial.Dismiss();
            yield return null;

            Assert.IsNotNull(GameManager.Instance, "안내를 닫으면 런이 시작돼야 한다");
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Assert.IsTrue(FirstRunTutorial.Seen);
        }

        /// <summary>2026-10-06 네이티브 QA: 설정에서 마우스 이동을 고른 뒤에도 첫 안내가 "WASD/방향키 이동"이라고 했다.
        /// 08:30 마우스 모드는 커서 방향 이동이므로 안내 첫 줄이 고른 방식을 따라야 한다(나머지 3줄·레이아웃은 그대로).</summary>
        [UnityTest]
        public IEnumerator FirstTime_WithMouseScheme_GuideDescribesCursorMovement()
        {
            InputSettings.Scheme = MovementScheme.Mouse;
            FirstRunTutorial.ResetForTests();
            GameBootstrap.RestartToSelect();
            yield return null;

            var select = Object.FindFirstObjectByType<CharacterSelectView>();
            select.BartenderButton.onClick.Invoke();
            yield return null;
            select.WeaponButtonA.onClick.Invoke(); select.StartButton.onClick.Invoke();
            yield return null;

            var tutorial = Object.FindFirstObjectByType<FirstRunTutorialView>();
            Assert.IsNotNull(tutorial);
            StringAssert.StartsWith("마우스 커서 쪽으로 이동 · 공격은 자동", tutorial.BodyText);
            StringAssert.DoesNotContain("WASD", tutorial.BodyText);
            StringAssert.Contains("Esc 일시정지", tutorial.BodyText);

            // 같은 뷰를 키보드로 바꾼 뒤 다시 열면(설정의 "조작 안내 다시보기"와 같은 Show 경로) 키보드 문구로 돌아온다.
            InputSettings.Scheme = MovementScheme.Keyboard;
            tutorial.Show("닫기", null);
            StringAssert.StartsWith("WASD/방향키 이동 · 공격은 자동", tutorial.BodyText);
            Assert.AreEqual(FirstRunTutorialView.BodyFor(MovementScheme.Keyboard).Split('\n').Length,
                FirstRunTutorialView.BodyFor(MovementScheme.Mouse).Split('\n').Length, "08:70 최대 4줄 유지");
        }

        /// <summary>2026-10-06 r8 네이티브 QA: 설정 위에서 다시 연 안내 뒤로 설정 글자가 비쳤다. 안내 패널(제목·본문·버튼 전체)은
        /// 불투명 배경이어야 하고, 그 배경은 패널의 글자보다 먼저(뒤에) 그려져야 한다.</summary>
        [UnityTest]
        public IEnumerator Replay_PanelHasOpaqueBackground_BehindAllText()
        {
            var canvas = UiFactory.CreateCanvas(null, "TutorialPanelTestCanvas", 50);
            var tutorial = FirstRunTutorialView.Create(canvas.transform);
            tutorial.Show("닫기", null);
            yield return null;

            var panel = tutorial.transform.Find("Panel") as RectTransform;
            Assert.IsNotNull(panel, "Panel");
            var bg = panel.GetComponent<UnityEngine.UI.Image>();
            Assert.IsNotNull(bg, "패널 배경 이미지");
            Assert.AreEqual(1f, bg.color.a, 1e-4f, "패널 배경은 완전히 불투명");
            Assert.IsNull(bg.sprite, "불투명 배경은 투명 테두리가 있을 수 있는 스킨 스프라이트가 아니라 단색");
            foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                var r = text.rectTransform;
                // 글자 상자가 패널 안에 있다(패널 밖으로 나가 딤 위에 직접 그려지는 글자 없음).
                var corners = new Vector3[4];
                r.GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    var local = panel.InverseTransformPoint(c);
                    Assert.IsTrue(Mathf.Abs(local.x - panel.rect.center.x) <= panel.rect.width / 2f + 0.5f &&
                                  Mathf.Abs(local.y - panel.rect.center.y) <= panel.rect.height / 2f + 0.5f,
                        $"{text.name} 글자 상자가 불투명 패널 안에 있어야 한다");
                }
            }
            Object.Destroy(canvas.gameObject);
        }

        [UnityTest]
        public IEnumerator AlreadySeen_SkipsTutorial_AndStartsRunImmediately()
        {
            FirstRunTutorial.MarkSeen();
            GameBootstrap.RestartToSelect();
            yield return null;

            var select = Object.FindFirstObjectByType<CharacterSelectView>();
            select.BartenderButton.onClick.Invoke();
            yield return null;
            select.WeaponButtonA.onClick.Invoke(); select.StartButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(Object.FindFirstObjectByType<FirstRunTutorialView>(),
                "이미 봤으면 다시 자동으로 뜨면 안 된다");
            Assert.IsNotNull(GameManager.Instance);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
        }
    }
}
