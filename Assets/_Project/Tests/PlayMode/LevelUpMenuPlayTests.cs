using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>2026-11-22 레벨업 메뉴판 건네기 실제 경로: 카드별 장식·최고 등급 공통 연출 한 번·두 직업·특수 경로·저이펙트·입력 해제/잠금·재오픈 잔상.
    /// 입력은 가상 키보드(실제 PC 입력·포커스 미사용).</summary>
    public class LevelUpMenuPlayTests
    {
        GameManager game;
        Keyboard kb;
        UnityEngine.InputSystem.InputSettings.BackgroundBehavior savedBg;
        UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode savedEditor;
        bool savedSuppress;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            savedSuppress = CardEducationGuide.SuppressAutomaticForTests;
            CardEducationGuide.SuppressAutomaticForTests = true;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (kb != null)
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(kb); kb = null;
                var s = UnityEngine.InputSystem.InputSystem.settings; s.backgroundBehavior = savedBg; s.editorInputBehaviorInPlayMode = savedEditor;
            }
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            CardEducationGuide.SuppressAutomaticForTests = savedSuppress;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        IEnumerator Run(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
        }

        void VirtualKeyboard()
        {
            var s = UnityEngine.InputSystem.InputSystem.settings;
            savedBg = s.backgroundBehavior; savedEditor = s.editorInputBehaviorInPlayMode;
            s.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            s.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            kb = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>("LevelUpMenuKeyboard");
            kb.MakeCurrent();
        }

        static CardData Pick(CardRarity rarity, CardCategory category)
        {
            var c = Resources.LoadAll<CardData>("Cards").First(x => x.rarity == rarity && x.category == category);
            Assert.IsNotNull(c);
            return c;
        }

        static List<CardOffer> Mixed(CardCategory category = CardCategory.Cocktail) => new List<CardOffer>
        {
            new CardOffer { card = Pick(CardRarity.Common, category) },
            new CardOffer { card = Pick(CardRarity.Rare, category) },
            new CardOffer { card = Pick(CardRarity.Epic, category) },
        };

        static string DecoName(Sprite s) => s != null ? s.name : "";

        static void Queue(Keyboard k, params Key[] keys) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(k, new KeyboardState(keys));

        [UnityTest]
        public IEnumerator Mixed_EachCardOwnDecoration_SingleClipAtHighestRarity()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            Assert.IsTrue(view.HandoffPlaying);
            Assert.AreEqual(2, view.HandoffTier, "일반·희귀·영웅 → 영웅 동작 한 번");
            Assert.AreEqual("card_common", DecoName(view.SlotDecoration(0)));
            Assert.AreEqual("card_rare", DecoName(view.SlotDecoration(1)));
            Assert.AreEqual("card_epic", DecoName(view.SlotDecoration(2)));
            Assert.AreSame(LevelUpMenuArt.Frames(Job.Bartender, 2)[0], view.HandoffSprite, "바텐더 영웅 첫 프레임");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0f, view.CardAlpha(i), 1e-4f);
                Assert.IsFalse(view.SlotInteractive(i));
                Assert.IsTrue(view.EntranceDone(i), "기존 채우기 등장과 중복하지 않음");
            }
            Assert.IsTrue(view.InputLocked);
            view.Choose(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Handoff_Settles_AtManifestTime_NoExtraDelay_InputGateRearmed()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            float start = Time.realtimeSinceStartup;
            while (view.HandoffPlaying && Time.realtimeSinceStartup - start < 3f) yield return null;
            float took = Time.realtimeSinceStartup - start;
            Assert.IsFalse(view.HandoffPlaying);
            Assert.GreaterOrEqual(took, LevelUpMenuArt.SelectMs(2) / 1000f - 0.02f, "영웅은 390 ms 이전에 끝나지 않는다");
            Assert.Less(took, LevelUpMenuArt.SelectMs(2) / 1000f + 0.25f, "프레임이나 카드에 별도 대기를 더하지 않는다");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(1f, view.CardAlpha(i), 1e-4f);
                Assert.IsTrue(view.SlotInteractive(i));
                Assert.AreEqual((i - 1) * 480f, view.SlotPosition(i).x, 0.01f, "최종 좌표");
            }
            Assert.IsTrue(view.InputLocked, "정착 프레임에는 게이트가 새로 무장된 상태");
            yield return null; yield return null;
            Assert.IsFalse(view.InputLocked, "눌린 키가 없으면 다음 프레임 해제");
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator TeaSommelier_UsesTeaClip_SingleCardCentered()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow);
            var view = game.CardView;
            view.Show(new List<CardOffer> { new CardOffer { card = Pick(CardRarity.Common, CardCategory.Tea) } }, _ => { });
            Assert.AreEqual(0, view.HandoffTier);
            Assert.AreSame(LevelUpMenuArt.Frames(Job.TeaSommelier, 0)[0], view.HandoffSprite);
            Assert.AreNotSame(LevelUpMenuArt.Frames(Job.Bartender, 0)[0], view.HandoffSprite, "직업별 외형");
            Assert.AreEqual(0f, view.SlotPosition(0).x, 0.01f, "1장은 가운데 정렬");
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.IsFalse(view.HandoffPlaying);
            Assert.AreEqual(0f, view.SlotPosition(0).x, 0.01f);
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator TwoCards_AreCentered()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed().Take(2).ToList(), _ => { });
            Assert.AreEqual(1, view.HandoffTier, "일반·희귀 → 희귀 동작");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(-240f, view.SlotPosition(0).x, 0.01f);
            Assert.AreEqual(240f, view.SlotPosition(1).x, 0.01f);
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator EmptyOffers_NoPresentation_NoException()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(new List<CardOffer>(), _ => { });
            Assert.IsFalse(view.HandoffPlaying);
            view.Choose(0); // 범위 밖: 무시
            Assert.IsTrue(view.IsOpen);
            view.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
        }

        [UnityTest]
        public IEnumerator HeldKeyAtOpen_DoesNotSelect_AfterSettle_UntilReleasedAndPressedAgain()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            VirtualKeyboard();
            var view = game.CardView;
            CardOffer? chosen = null;
            Queue(kb, Key.Digit1); // 직전 화면을 닫은 키가 계속 눌려 있음
            yield return null;
            view.Show(Mixed(), o => chosen = o);
            yield return new WaitForSecondsRealtime(0.7f); // 전달·정착 완료 후에도 계속 누름
            Assert.IsFalse(view.HandoffPlaying);
            Assert.IsNull(chosen, "눌린 채 정착해도 자동 선택 없음");
            Assert.IsTrue(view.IsOpen);
            Assert.IsTrue(view.InputLocked, "뗄 때까지 잠금");
            Queue(kb);
            yield return null; yield return null; yield return null;
            Assert.IsFalse(view.InputLocked);
            Assert.IsNull(chosen);
            Queue(kb, Key.Digit2);
            yield return null; yield return null;
            Assert.IsTrue(chosen.HasValue, "새로 누른 키는 선택");
        }

        [UnityTest]
        public IEnumerator KeyPressedDuringHandoff_IsNotQueued_AndButtonClickBlocked()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            VirtualKeyboard();
            var view = game.CardView;
            CardOffer? chosen = null;
            view.Show(Mixed(), o => chosen = o);
            yield return null;
            Assert.IsTrue(view.HandoffPlaying);
            Queue(kb, Key.Digit3);
            yield return null;
            Queue(kb);
            yield return null;
            view.transform.Find("Card1").GetComponent<Button>().onClick.Invoke(); // 전달 중 버튼
            Assert.IsNull(chosen, "전달 중 입력은 선택하지 않는다");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return null; yield return null;
            Assert.IsNull(chosen, "쌓아 둔 입력이 정착 뒤 확정되지 않는다");
            Assert.IsTrue(view.IsOpen);
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator RerollAndReopen_ReplaceCurrent_NoDuplicate_NoResidual()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(view.HandoffFrame, -1);
            view.Show(new List<CardOffer> { new CardOffer { card = Pick(CardRarity.Common, CardCategory.Cocktail) } }, _ => { }); // 재추첨
            Assert.AreEqual(0, view.HandoffTier, "새 후보 기준 등급 클립으로 교체");
            Assert.AreEqual(0, view.HandoffFrame, "처음부터");
            Assert.AreEqual(1, view.GetComponentsInChildren<Transform>(true).Count(t => t.name == "MenuHandoff"), "재생 이미지 하나");
            view.Choose(0); // 전달 중 닫기
            Assert.IsFalse(view.HandoffPlaying);
            var img = view.transform.Find("MenuHandoff");
            Assert.IsFalse(img.gameObject.activeSelf, "닫으면 메뉴판 이미지 없음");
            Assert.AreEqual(Color.white, img.GetComponent<Image>().color);
            yield return null;
            view.Show(Mixed(), _ => { }); // 재오픈: 지난 재생의 잠금·알파·오프셋이 남지 않고 새로 시작
            Assert.AreEqual(2, view.HandoffTier); Assert.AreEqual(0, view.HandoffFrame);
            yield return new WaitForSecondsRealtime(0.7f);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(1f, view.CardAlpha(i), 1e-4f);
                Assert.IsTrue(view.SlotInteractive(i));
                Assert.AreEqual((i - 1) * 480f, view.SlotPosition(i).x, 0.01f);
            }
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator LowEffects_NoHandoff_SettledImmediately_DecorationsStillShown()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            Assert.IsFalse(view.HandoffPlaying, "전달·페이드·광원 없음");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(1f, view.CardAlpha(i));
                Assert.IsTrue(view.SlotInteractive(i));
                Assert.IsTrue(view.EntranceDone(i));
                Assert.IsNotNull(view.SlotDecoration(i));
            }
            Assert.IsTrue(view.InputLocked, "기존 입력 해제 조건은 유지");
            yield return null; yield return null;
            Assert.IsFalse(view.InputLocked);
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator SceneTeardown_DuringHandoff_NoErrors()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow);
            var view = game.CardView;
            view.Show(new List<CardOffer> { new CardOffer { card = Pick(CardRarity.Epic, CardCategory.Tea) } }, _ => { });
            yield return new WaitForSecondsRealtime(0.1f);
            Object.Destroy(game.gameObject); game = null;
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator HybridUnlock_SpecialClip_Settles_ThenUnlocks_LowEffectsImmediate()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.HybridView;
            bool unlocked = false;
            view.Show(() => unlocked = true);
            Assert.IsTrue(view.HandoffPlaying); Assert.AreEqual(3, view.HandoffTier);
            Assert.AreSame(LevelUpMenuArt.Frames(Job.Bartender, 3)[0], view.HandoffSprite);
            Assert.AreEqual("hybrid_special", DecoName(view.Decoration));
            Assert.AreEqual(0f, view.ContentAlpha, 1e-4f); Assert.IsFalse(view.ContentInteractive);
            float start = Time.realtimeSinceStartup;
            while (view.HandoffPlaying && Time.realtimeSinceStartup - start < 3f) yield return null;
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, 0.43f);
            Assert.AreEqual(1f, view.ContentAlpha, 1e-4f); Assert.IsTrue(view.ContentInteractive);
            Assert.IsTrue(view.InputLocked);
            yield return null; yield return null;
            Assert.IsFalse(view.InputLocked);
            view.Unlock();
            Assert.IsTrue(unlocked); Assert.IsFalse(view.IsOpen); Assert.IsFalse(view.HandoffPlaying);

            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            view.Show(() => { });
            Assert.IsFalse(view.HandoffPlaying); Assert.AreEqual(1f, view.ContentAlpha); Assert.IsTrue(view.ContentInteractive);
            Assert.IsNotNull(view.Decoration);
            view.Unlock();
        }

        [UnityTest]
        public IEnumerator HybridUnlock_RealRewardPath_BartenderRapier_SpecialClip_PoolHasNoSpecial()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(1f);
            game.AddIngredient(IngredientType.Cocktail, 5000);
            game.AddIngredient(IngredientType.Tea, 5000);
            for (int i = 0; i < 80 && game.State != GameState.HybridOffer; i++)
            {
                if (game.State == GameState.LevelUp)
                {
                    Assert.IsFalse(game.CardView.Offers.Any(o => o.card.rarity == CardRarity.Special), "일반 추첨 풀에는 특수 카드가 없다");
                    game.CardView.Choose(0);
                }
                else if (game.State == GameState.SkillSelect) game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
                yield return null;
            }
            Assert.AreEqual(GameState.HybridOffer, game.State);
            Assert.AreEqual(3, game.HybridView.HandoffTier);
            game.HybridView.Unlock();
        }
    }
}
