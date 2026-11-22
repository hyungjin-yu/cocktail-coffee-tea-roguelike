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
    /// <summary>2026-11-22 레벨업 메뉴판 건네기 실제 경로 / 2026-11-23 항목 8 후속: 카드별 장식·**카드별 자기 등급 전달**·두 직업·특수 경로·저이펙트·
    /// 입력 해제/잠금·재오픈 잔상·재생 속도 설정·창 안 포커스 묶음과 닫힘 복원. 입력은 가상 키보드(실제 PC 입력·포커스 미사용).
    /// 원본 시간표 검사는 재생 속도 100%로 고정하고(Setup), 기본 0.5배는 따로 검사한다.</summary>
    public class LevelUpMenuPlayTests
    {
        GameManager game;
        Keyboard kb;
        UnityEngine.InputSystem.InputSettings.BackgroundBehavior savedBg;
        UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode savedEditor;
        bool savedSuppress;
        int savedSpeed;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            savedSuppress = CardEducationGuide.SuppressAutomaticForTests;
            CardEducationGuide.SuppressAutomaticForTests = true;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            savedSpeed = SettingsService.Current.levelUpAnimPercent;
            SettingsService.SetLevelUpAnimPercent(100); // 원본 시간표(1배) 기준 시험 — 기본 0.5배는 DefaultSpeed_* 시험
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (kb != null || mouse != null || touch != null)
            {
                if (kb != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(kb);
                if (mouse != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                if (touch != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(touch);
                kb = null; mouse = null; touch = null;
                var s =UnityEngine.InputSystem.InputSystem.settings; s.backgroundBehavior = savedBg; s.editorInputBehaviorInPlayMode = savedEditor;
            }
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            CardEducationGuide.SuppressAutomaticForTests = savedSuppress;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            SettingsService.SetLevelUpAnimPercent(savedSpeed);
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
        public IEnumerator Mixed_EachCardOwnDecoration_AndOwnRarityHandoffInItsSlot()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            Assert.IsTrue(view.HandoffPlaying);
            Assert.AreEqual(2, view.HandoffTier, "가장 늦게 정착하는 카드 = 영웅");
            Assert.AreEqual("card_common", DecoName(view.SlotDecoration(0)));
            Assert.AreEqual("card_rare", DecoName(view.SlotDecoration(1)));
            Assert.AreEqual("card_epic", DecoName(view.SlotDecoration(2)));
            Assert.AreSame(LevelUpMenuArt.Frames(Job.Bartender, 2)[0], view.HandoffSprite, "바텐더 영웅 첫 프레임");
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(view.SlotHandoffPlaying(i), $"카드 {i} 자기 전달");
                Assert.AreEqual(i, view.SlotHandoffTier(i), "일반·희귀·영웅 카드가 각자 자기 등급 동작");
                Assert.AreSame(LevelUpMenuArt.Frames(Job.Bartender, i)[0], view.SlotHandoffSprite(i), "바텐더 외형 · 카드 등급 첫 프레임");
                Assert.AreEqual((i - 1) * 480f * LevelUpMenuArt.StartOffsetFraction, view.SlotHandoffAnchor(i).x, 0.01f, "판 앵커 = 그 카드의 시작 좌표");
                Assert.AreEqual(view.SlotPosition(i).x, view.SlotHandoffAnchor(i).x, 0.01f, "메뉴판이 그 슬롯으로 이어짐");
                Assert.AreEqual(LevelUpMenuArt.CardGroupAnchorY, view.SlotHandoffAnchor(i).y, 0.01f);
            }
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
        public IEnumerator Mixed_EachCardSettlesAtOwnTime_InputOpensOnlyAfterLast()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            float start = Time.realtimeSinceStartup;
            // 일반(260 ms) 정착 뒤 · 영웅(390 ms) 정착 전
            while (view.SlotHandoffPlaying(0) && Time.realtimeSinceStartup - start < 3f) yield return null;
            Assert.IsTrue(view.SlotHandoffPlaying(2), "일반 카드가 먼저 끝나도 영웅 카드는 아직 전달 중");
            Assert.AreEqual(1f, view.CardAlpha(0), 1e-4f, "일반 카드는 자기 시점에 정착");
            Assert.AreEqual(0f, view.SlotPosition(0).x + 480f, 0.01f, "일반 카드 최종 좌표");
            Assert.AreEqual(0f, view.CardAlpha(2), 1e-4f, "영웅 카드는 아직 감춤");
            Assert.IsFalse(view.SlotInteractive(0), "모든 카드가 정착할 때까지 입력은 닫힘");
            Assert.IsTrue(view.HandoffPlaying);
            while (view.HandoffPlaying && Time.realtimeSinceStartup - start < 3f) yield return null;
            for (int i = 0; i < 3; i++) Assert.IsTrue(view.SlotInteractive(i));
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator DefaultSpeed_Half_DoublesDuration_SettingAppliesToNextOpen()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            SettingsService.SetLevelUpAnimPercent(SettingsService.LevelUpAnimDefaultPercent);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            Assert.AreEqual(0.5f, view.SlotHandoffSpeed(0), 1e-6f, "기본 0.5배");
            float start = Time.realtimeSinceStartup;
            while (view.HandoffPlaying && Time.realtimeSinceStartup - start < 4f) yield return null;
            float took = Time.realtimeSinceStartup - start;
            Assert.GreaterOrEqual(took, LevelUpMenuArt.RealSelectSeconds(2, 0.5f) - 0.03f, "영웅 390 ms × 2 = 780 ms 이전에 끝나지 않는다");
            Assert.Less(took, LevelUpMenuArt.RealSelectSeconds(2, 0.5f) + 0.3f, "별도 대기 없음");
            view.Choose(0);
            yield return null;

            SettingsService.SetLevelUpAnimPercent(200);
            view.Show(Mixed(), _ => { });
            Assert.AreEqual(2f, view.SlotHandoffSpeed(2), 1e-6f, "설정 변경은 다음 창부터");
            start = Time.realtimeSinceStartup;
            while (view.HandoffPlaying && Time.realtimeSinceStartup - start < 3f) yield return null;
            Assert.Less(Time.realtimeSinceStartup - start, LevelUpMenuArt.RealSelectSeconds(2, 2f) + 0.25f);
            view.Choose(0);

            SettingsService.SetLevelUpAnimPercent(SettingsService.LevelUpAnimDefaultPercent);
            var hybrid = game.HybridView;
            hybrid.Show(() => { });
            start = Time.realtimeSinceStartup;
            while (hybrid.HandoffPlaying && Time.realtimeSinceStartup - start < 4f) yield return null;
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, LevelUpMenuArt.RealSelectSeconds(3, 0.5f) - 0.03f, "특수 해금도 같은 속도 설정");
            hybrid.Unlock();
        }

        [UnityTest]
        public IEnumerator LowEffects_IgnoresSpeed_StillImmediate()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow);
            SettingsService.SetLevelUpAnimPercent(25);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            var view = game.CardView;
            view.Show(Mixed(CardCategory.Tea), _ => { });
            Assert.IsFalse(view.HandoffPlaying, "저이펙트는 속도와 무관하게 즉시");
            for (int i = 0; i < 3; i++) { Assert.IsFalse(view.SlotHandoffPlaying(i)); Assert.AreEqual(1f, view.CardAlpha(i)); Assert.IsTrue(view.SlotInteractive(i)); }
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator KeyboardFocus_StaysInsideCardWindow_RestoredOnClose_Standard() { yield return FocusScope(RunMode.Standard); }

        [UnityTest]
        public IEnumerator KeyboardFocus_StaysInsideCardWindow_RestoredOnClose_Master() { yield return FocusScope(RunMode.Endless); }

        IEnumerator FocusScope(RunMode mode)
        {
            var savedMode = RunModeSelection.Current;
            RunModeSelection.Current = mode;
            try
            {
                yield return Run(Job.Bartender, WeaponKind.Rapier);
                Assert.AreEqual(mode == RunMode.Endless, game.IsEndless);
                var view = game.CardView;
                // 덮개 아래 화면의 버튼(예: 마스터 도전 준비 패널의 '전투 시작')에 포커스가 있던 상태에서 연다.
                var under = new GameObject("UnderButton", typeof(RectTransform), typeof(Image), typeof(Button));
                under.transform.SetParent(view.transform.parent, false);
                under.transform.SetSiblingIndex(0);
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(under);
                view.Show(Mixed(), _ => { });
                var inside = new HashSet<Selectable>(view.GetComponentsInChildren<Selectable>(true));
                for (int i = 0; i < 3; i++)
                {
                    foreach (var sel in new Selectable[] { view.SlotButton(i), view.LearnButton(i) })
                    {
                        Assert.AreEqual(Navigation.Mode.Explicit, sel.navigation.mode, sel.name + " 명시 이동");
                        foreach (var to in new[] { sel.navigation.selectOnLeft, sel.navigation.selectOnRight, sel.navigation.selectOnUp, sel.navigation.selectOnDown })
                            if (to != null) Assert.IsTrue(inside.Contains(to), $"{sel.name} → {to.name}: 카드 창 밖(덮개 아래)으로 포커스가 나가면 안 된다");
                    }
                }
                Assert.AreSame(view.SlotButton(1), view.SlotButton(0).navigation.selectOnRight);
                Assert.IsNull(view.SlotButton(0).navigation.selectOnLeft, "가장자리는 멈춤(창 밖으로 감기지 않음)");
                Assert.AreSame(view.ChangeTargetButton, view.SlotButton(1).navigation.selectOnUp, "목표 바꾸기는 정상 런에서 보임 → 위 이동 대상");
                Assert.AreSame(view.LearnButton(2), view.SlotButton(2).navigation.selectOnDown);
                foreach (var to in new[] { view.ChangeTargetButton.navigation.selectOnDown, view.ChangeTargetButton.navigation.selectOnUp, view.LearningHelpButton.navigation.selectOnDown })
                    if (to != null) Assert.IsTrue(inside.Contains(to));
                Assert.IsFalse(inside.Contains(under.GetComponent<Button>()));
                view.Choose(0); // 다음 화면 없음 → 열기 전 포커스로 복원
                Assert.IsFalse(view.IsOpen);
                Assert.AreSame(under, UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "닫히면 열기 전 포커스 복원");
                yield return null;

                // 2장: 셋째 카드·지식 버튼은 이동 대상에서 빠진다
                view.Show(Mixed().Take(2).ToList(), _ => { });
                Assert.AreEqual(Navigation.Mode.None, view.SlotButton(2).navigation.mode);
                Assert.IsNull(view.SlotButton(1).navigation.selectOnRight);
                view.Choose(1);
            }
            finally { RunModeSelection.Current = savedMode; }
        }

        [UnityTest]
        public IEnumerator TeaSommelier_UsesTeaClip_SingleCardCentered()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow);
            var view = game.CardView;
            view.Show(new List<CardOffer> { new CardOffer { card = Pick(CardRarity.Common, CardCategory.Tea) } }, _ => { });
            Assert.AreEqual(0, view.HandoffTier);
            Assert.AreSame(LevelUpMenuArt.Frames(Job.TeaSommelier, 0)[0], view.HandoffSprite);
            Assert.AreEqual(0f, view.SlotHandoffAnchor(0).x, 0.01f, "1장 = 가운데에서 전달");
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
            Assert.AreEqual(1, view.HandoffTier, "일반·희귀 → 가장 늦게 정착하는 카드 = 희귀");
            Assert.AreEqual(0, view.SlotHandoffTier(0)); Assert.AreEqual(1, view.SlotHandoffTier(1));
            Assert.AreEqual(-240f * LevelUpMenuArt.StartOffsetFraction, view.SlotHandoffAnchor(0).x, 0.01f);
            Assert.AreEqual(240f * LevelUpMenuArt.StartOffsetFraction, view.SlotHandoffAnchor(1).x, 0.01f);
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
            var imgs = view.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("MenuHandoff")).ToList();
            Assert.AreEqual(3, imgs.Count, "슬롯마다 재생 이미지 하나(재추첨해도 늘지 않음)");
            Assert.AreEqual(1, imgs.Count(t => t.gameObject.activeSelf), "1장으로 재추첨 → 재생 이미지 1개만");
            Assert.IsFalse(view.SlotHandoffPlaying(1)); Assert.IsFalse(view.SlotHandoffPlaying(2));
            view.Choose(0); // 전달 중 닫기
            Assert.IsFalse(view.HandoffPlaying);
            foreach (var img in imgs)
            {
                Assert.IsFalse(img.gameObject.activeSelf, "닫으면 메뉴판 이미지 없음");
                Assert.AreEqual(Color.white, img.GetComponent<Image>().color);
            }
            yield return null;
            view.Show(Mixed(), _ => { }); // 재오픈: 지난 재생의 잠금·알파·오프셋이 남지 않고 새로 시작
            Assert.AreEqual(2, view.HandoffTier); Assert.AreEqual(0, view.HandoffFrame);
            for (int i = 0; i < 3; i++) { Assert.AreEqual(i, view.SlotHandoffTier(i)); Assert.AreEqual(0, view.SlotHandoffFrame(i)); }
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

        // ───── 2차 검수: 선택 틴트·마우스·터치·정상 런 레벨업 입력과 재진입 ─────

        static Vector2 Center(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return (c[0] + c[2]) * 0.5f;
        }

        Mouse mouse;
        Touchscreen touch;

        void AllowVirtualDevices()
        {
            var st = UnityEngine.InputSystem.InputSystem.settings;
            if (kb == null && mouse == null && touch == null) { savedBg = st.backgroundBehavior; savedEditor = st.editorInputBehaviorInPlayMode; }
            st.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            st.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        }

        [UnityTest]
        public IEnumerator FocusTint_ShowsOnSelectedCard_AboveDecorationBelowText()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            view.Show(Mixed(), _ => { });
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(CardSelectView.FocusTintAlpha, view.FocusAlpha(0), 0.01f, "선택된(첫) 카드: 장식 위 틴트");
            Assert.AreEqual(0f, view.FocusAlpha(1), 1e-4f, "다른 카드는 틴트 없음");
            Assert.Less(CardSelectView.FocusTintAlpha, 0.25f, "글자 가독성 유지(은은한 막)");
            var tint = view.transform.Find("Card1/FocusTint");
            Assert.Less(tint.GetSiblingIndex(), view.transform.Find("Card1/Name").GetSiblingIndex(), "틴트는 글·아이콘 아래");
            Assert.Greater(tint.GetSiblingIndex(), view.transform.Find("Card1/MenuDeco").GetSiblingIndex(), "장식 위");
            Assert.IsFalse(tint.GetComponent<Image>().raycastTarget);
            view.Choose(0);
        }

        [UnityTest]
        public IEnumerator Mouse_Hover_Tints_Click_Selects_NotDuringHandoff()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            AllowVirtualDevices();
            mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>("LevelUpMenuMouse");
            mouse.MakeCurrent();
            var view = game.CardView;
            CardOffer? chosen = null;
            view.Show(Mixed(), o => chosen = o);
            yield return null;
            Vector2 p2 = Center((RectTransform)view.transform.Find("Card2"));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = p2 }.WithButton(MouseButton.Left, true));
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = p2 });
            yield return null; yield return null;
            Assert.IsNull(chosen, "전달 중 클릭은 선택하지 않는다");
            yield return new WaitForSecondsRealtime(0.6f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = p2 });
            yield return null; yield return null; yield return null;
            Assert.Greater(view.FocusAlpha(1), 0.05f, "마우스를 올리면 그 카드에 틴트");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = p2 }.WithButton(MouseButton.Left, true));
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new MouseState { position = p2 });
            yield return null; yield return null;
            Assert.IsTrue(chosen.HasValue, "정착 뒤 클릭은 그 카드 선택");
            Assert.AreEqual(Pick(CardRarity.Rare, CardCategory.Cocktail).id, chosen.Value.card.id);
        }

        [UnityTest]
        public IEnumerator Touch_Tap_Selects_AfterSettle_NotDuringHandoff()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            AllowVirtualDevices();
            touch = UnityEngine.InputSystem.InputSystem.AddDevice<Touchscreen>("LevelUpMenuTouch");
            var view = game.CardView;
            CardOffer? chosen = null;
            view.Show(Mixed(), o => chosen = o);
            yield return null;
            Vector2 p3 = Center((RectTransform)view.transform.Find("Card3"));
            void Touch(int id, bool down) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(touch, new TouchState
            {
                touchId = id, phase = down ? UnityEngine.InputSystem.TouchPhase.Began : UnityEngine.InputSystem.TouchPhase.Ended,
                position = p3, pressure = down ? 1f : 0f,
            });
            Touch(1, true); yield return null; yield return null; Touch(1, false); yield return null; yield return null;
            Assert.IsNull(chosen, "전달 중 탭은 선택하지 않는다");
            yield return new WaitForSecondsRealtime(0.6f);
            Touch(2, true); yield return null; yield return null; Touch(2, false); yield return null; yield return null;
            Assert.IsTrue(chosen.HasValue, "정착 뒤 탭 선택");
            Assert.AreEqual(Pick(CardRarity.Epic, CardCategory.Cocktail).id, chosen.Value.card.id);
        }

        [UnityTest]
        public IEnumerator RealRun_LevelUp_KeyChoose_ReentryHoldsWhileKeyStillPressed()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(1f);
            VirtualKeyboard();
            game.AddIngredient(IngredientType.Cocktail, 5000);
            int opened = 0, handoffs = 0, heldReentries = 0;
            for (int guard = 0; guard < 900 && opened < 3; guard++)
            {
                if (game.State == GameState.LevelUp && game.CardView.IsOpen)
                {
                    opened++;
                    if (game.CardView.HandoffPlaying) handoffs++;
                    float until = Time.realtimeSinceStartup + 3f;
                    while ((game.CardView.HandoffPlaying || game.CardView.InputLocked) && Time.realtimeSinceStartup < until) yield return null;
                    Assert.IsTrue(game.CardView.IsOpen, "정착까지 자동 선택 없음");
                    var firstOffers = game.CardView.Offers.Select(o => o.card.id).ToList();
                    Queue(kb, Key.Digit1); // 1키 선택, 이후 계속 누른 상태
                    yield return null; yield return null;
                    if (game.State == GameState.LevelUp && game.CardView.IsOpen)
                    {
                        // 같은 호출에서 다음 보상이 열림 — 눌린 키로 자동 선택되면 안 된다
                        for (int i = 0; i < 25; i++) yield return null;
                        Assert.IsTrue(game.State == GameState.LevelUp && game.CardView.IsOpen, "눌린 키로 재진입 창이 자동 선택됨");
                        heldReentries++;
                    }
                    Queue(kb);
                    yield return null; yield return null;
                    continue;
                }
                if (game.State == GameState.SkillSelect) game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
                yield return null;
            }
            Assert.GreaterOrEqual(opened, 1, "정상 런에서 레벨업이 열려야 한다");
            Assert.GreaterOrEqual(handoffs, 1, "정상 런 레벨업도 건네기로 시작");
            Debug.Log($"[LevelUpMenu] realrun opened={opened} handoffs={handoffs} heldReentries={heldReentries}");
        }
    }
}
