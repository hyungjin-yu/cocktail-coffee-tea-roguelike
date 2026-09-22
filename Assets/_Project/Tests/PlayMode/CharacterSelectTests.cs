using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    public class CharacterSelectTests
    {
        static void DestroyAllSelectScreens()
        {
            foreach (var v in Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // AutoStart 가 세션 시작 시 띄운 선택 화면이 남아있으면(같은 이름의 버튼과 겹쳐서
            // GameObject.Find 가 엉뚱한 걸 집을 수 있음) 먼저 치운다.
            DestroyAllSelectScreens();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            DestroyAllSelectScreens();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Show_ClickingTeaSommelier_InvokesCallback_AndDestroysScreen()
        {
            Job? chosen = null;
            var view = CharacterSelectView.Show(j => chosen = j);
            yield return null;

            Assert.IsNotNull(view.TeaSommelierButton);
            view.TeaSommelierButton.onClick.Invoke();

            Assert.AreEqual(Job.TeaSommelier, chosen);

            yield return null;
            Assert.IsTrue(view == null, "선택 후 화면(Canvas)이 파괴되어야 한다");
        }

        [UnityTest]
        public IEnumerator Build_WithChosenJob_AppliesJobPreset()
        {
            var game = GameBootstrap.Build(Job.TeaSommelier);
            yield return null;

            Assert.AreEqual(Job.TeaSommelier, game.Job);
        }
    }
}
