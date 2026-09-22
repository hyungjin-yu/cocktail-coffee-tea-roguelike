using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        public IEnumerator ClickingJob_ThenWeapon_InvokesCallback_AndDestroysScreen()
        {
            (Job job, WeaponKind weapon)? chosen = null;
            var view = CharacterSelectView.Show((j, w) => chosen = (j, w));
            yield return null;

            Assert.IsNotNull(view.TeaSommelierButton);
            Assert.IsNull(view.WeaponButtonA, "무기 선택은 직업을 고른 뒤에야 나와야 한다");
            view.TeaSommelierButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(view.WeaponButtonA);
            Assert.IsNotNull(view.WeaponButtonB);
            view.WeaponButtonB.onClick.Invoke(); // 티 소믈리에의 두 번째 무기 = 활

            Assert.IsTrue(chosen.HasValue);
            Assert.AreEqual(Job.TeaSommelier, chosen.Value.job);
            Assert.AreEqual(WeaponKind.Bow, chosen.Value.weapon);

            yield return null;
            Assert.IsTrue(view == null, "선택 후 화면(Canvas)이 파괴되어야 한다");
        }

        [UnityTest]
        public IEnumerator WeaponCard_RendersItsIcon()
        {
            var view = CharacterSelectView.Show((j, w) => { });
            yield return null;
            view.TeaSommelierButton.onClick.Invoke();
            yield return null;

            var expected = Resources.Load<Sprite>(WeaponPreset.Sword.iconResourcePath);
            Assert.IsNotNull(expected);

            var iconTransform = view.WeaponButtonA.transform.Find("Icon");
            Assert.IsNotNull(iconTransform, "무기 카드에 Icon 자식이 있어야 한다");

            var image = iconTransform.GetComponent<UnityEngine.UI.Image>();
            Assert.AreEqual(expected, image.sprite);
        }

        [UnityTest]
        public IEnumerator Build_WithChosenJobAndWeapon_AppliesBoth()
        {
            var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Bow);
            yield return null;

            Assert.AreEqual(Job.TeaSommelier, game.Job);
            Assert.AreEqual(WeaponKind.Bow, game.Weapon);
            Assert.IsNotNull(game.Player.GetComponent<PlayerProjectileWeapon>(),
                "총/활은 PlayerProjectileWeapon 을 붙여야 한다");
            Assert.IsNull(game.Player.GetComponent<RapierWeapon>());
        }

        [UnityTest]
        public IEnumerator Build_WithMeleeWeapon_AttachesRapierWeapon()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            Assert.IsNotNull(game.Player.GetComponent<RapierWeapon>());
            Assert.IsNull(game.Player.GetComponent<PlayerProjectileWeapon>());
        }

        [UnityTest]
        public IEnumerator RangedWeapon_NeverOffersMeleeOnlyCards()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Gun);
            yield return null;

            for (int i = 0; i < 30; i++)
            {
                var offers = game.Cards.Draw(CardCategory.Cocktail, game.Weapon.IsRanged());
                foreach (var offer in offers)
                    Assert.AreNotEqual(WeaponRestriction.MeleeOnly, offer.card.weaponRestriction,
                        $"원거리인데 근접 전용 카드({offer.card.id})가 뽑혔다");
            }
        }

        [UnityTest]
        public IEnumerator MeleeWeapon_NeverOffersRangedOnlyCards()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;

            for (int i = 0; i < 30; i++)
            {
                var offers = game.Cards.Draw(CardCategory.Cocktail, game.Weapon.IsRanged());
                foreach (var offer in offers)
                    Assert.AreNotEqual(WeaponRestriction.RangedOnly, offer.card.weaponRestriction,
                        $"근접인데 원거리 전용 카드({offer.card.id})가 뽑혔다");
            }
        }
    }
}
