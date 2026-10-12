using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>직업 선택 화면의 "영구 강화" 버튼 → PermanentSkillView 구매 흐름.
    /// 실제 세이브 파일은 절대 건드리지 않도록 이 테스트만의 임시 경로로 리다이렉트한다.</summary>
    public class PermanentSkillViewTests
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
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_test_shop_{System.Guid.NewGuid():N}.json");
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
        public IEnumerator ShopButton_OpensShop_AndHidesJobStep()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;

            Assert.IsNotNull(view.PermanentShopButton);
            Assert.IsFalse(view.ShopView.IsOpen);

            view.PermanentShopButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(view.ShopView.IsOpen, "버튼을 누르면 영구 강화 화면이 열려야 한다");
        }

        [UnityTest]
        public IEnumerator BuyingNode_PersistsAcrossScreens_AndAppliesAtRunStart()
        {
            PermanentProgress.AddSpecialty(3);

            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.PermanentShopButton.onClick.Invoke();
            yield return null;

            var cupper = view.ShopView.Nodes.Single(n => n.id == "bean_cupper");
            view.ShopView.TryBuy(cupper);

            Assert.IsTrue(PermanentProgress.Current.unlockedPermanentSkillIds.Contains("bean_cupper"),
                "구매하면 즉시 영구 저장되어야 한다");

            // 화면을 나갔다 다시 들어와도(=다음 런에서도) 해금 상태가 유지되는지 확인
            Object.DestroyImmediate(view.gameObject);
            InputSettings.Scheme = MovementScheme.Keyboard;
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            Assert.IsTrue(game.PermanentSkills.IsUnlocked(cupper));
            Assert.AreEqual(0.20f, game.Stats.beanGainPercent, 0.0001f);
        }

        [UnityTest]
        public IEnumerator NodeCard_RendersItsIcon()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.PermanentShopButton.onClick.Invoke();
            yield return null;

            var cupper = view.ShopView.Nodes.Single(n => n.id == "bean_cupper");
            var expected = Resources.Load<Sprite>(cupper.iconResourcePath);
            Assert.IsNotNull(expected);

            var iconTransform = view.ShopView.transform.Find($"Node_{cupper.id}/Icon");
            Assert.IsNotNull(iconTransform, "노드 카드에 Icon 자식이 있어야 한다");

            var image = iconTransform.GetComponent<UnityEngine.UI.Image>();
            Assert.AreEqual(expected, image.sprite);
        }

        [UnityTest]
        public IEnumerator BuyingNode_WithoutEnoughPoints_DoesNothing()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.PermanentShopButton.onClick.Invoke();
            yield return null;

            var cupper = view.ShopView.Nodes.Single(n => n.id == "bean_cupper"); // cost 3, 지금 포인트 0
            view.ShopView.TryBuy(cupper);

            Assert.IsFalse(PermanentProgress.Current.unlockedPermanentSkillIds.Contains("bean_cupper"),
                "포인트가 부족하면 구매가 무시돼야 한다");
        }
    }
}
