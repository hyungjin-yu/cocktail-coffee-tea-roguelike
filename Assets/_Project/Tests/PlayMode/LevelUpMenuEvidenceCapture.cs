using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-22 레벨업 메뉴판 시각 증거(창 없음): 환경 변수 CCT_EVIDENCE_DIR이 있을 때만 실행. 카메라를 RenderTexture로 렌더해 PNG 저장 — 보이는 창·OS 입력·포커스 사용 없음.
    /// 전달 시점은 실시간 대기·프레임 조건으로 잡는다(렌더 캡처가 프레임당 시간을 늘려 인접 프레임은 정확히 재현되지 않는다).
    /// 후보는 실제 카드 자산에서 등급별로 고른 강제 제시(Show 직접 호출), 하이브리드는 확정 해금 창 Show 직접 호출.
    /// </summary>
    public class LevelUpMenuEvidenceCapture
    {
        string dir;
        readonly StringBuilder log = new StringBuilder();
        GameManager game;
        Canvas canvas;
        bool savedSuppress;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            Directory.CreateDirectory(dir);
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            savedSuppress = CardEducationGuide.SuppressAutomaticForTests;
            CardEducationGuide.SuppressAutomaticForTests = true;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
    
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;
            if (!string.IsNullOrEmpty(dir)) File.AppendAllText(Path.Combine(dir, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false));
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            CardEducationGuide.SuppressAutomaticForTests = savedSuppress;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[LevelUpEvidence] " + s); }

        IEnumerator Start(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            canvas = game.CardView.GetComponentInParent<Canvas>();
        }

        static CardData Pick(CardRarity rarity, CardCategory category, int skip = 0) =>
            Resources.LoadAll<CardData>("Cards").Where(x => x.rarity == rarity && x.category == category).OrderBy(x => x.id).Skip(skip).First();

        static List<CardOffer> Offers(CardCategory cat, params CardRarity[] rarities)
        {
            var list = new List<CardOffer>(); var used = new Dictionary<CardRarity, int>();
            foreach (var r in rarities)
            {
                used.TryGetValue(r, out int n); used[r] = n + 1;
                list.Add(new CardOffer { card = Pick(r, cat, n), currentStacks = 0 });
            }
            return list;
        }

        IEnumerator Snap(string name, bool both = true)
        {
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1920, 1080, name + "_1920x1080");
            if (both) yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1280, 720, name + "_1280x720");
        }

        IEnumerator WaitFrame(CardSelectView v, int frame, int guard = 120)
        {
            float until = Time.realtimeSinceStartup + 3f;
            while (v.HandoffPlaying && v.HandoffFrame < frame && Time.realtimeSinceStartup < until) yield return null;
        }

        IEnumerator Settle(Func<bool> playing)
        {
            float until = Time.realtimeSinceStartup + 3f;
            while (playing() && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null; yield return null;
        }

        IEnumerator Opening(string tag, CardSelectView view, List<CardOffer> offers, bool inFlight)
        {
            view.Show(offers, _ => { });
            Line($"{tag}: tier={view.HandoffTier} playing={view.HandoffPlaying} offers={string.Join(",", offers.Select(o => o.card.rarity))}");
            if (inFlight && view.HandoffPlaying)
            {
                yield return WaitFrame(view, 1);
                Line($"{tag}: frame {view.HandoffFrame} reveal {view.HandoffReveal:0.00}");
                yield return Snap($"{tag}_flight_f{view.HandoffFrame}", false);
                yield return WaitFrame(view, LevelUpMenuArt.FrameCount(view.HandoffTier) - 1);
                Line($"{tag}: last pose frame {view.HandoffFrame} reveal {view.HandoffReveal:0.00}");
                yield return Snap($"{tag}_lastpose");
                float ru = Time.realtimeSinceStartup + 1f;
                while (view.HandoffPlaying && view.HandoffReveal < 0.4f && Time.realtimeSinceStartup < ru) yield return null;
                if (view.HandoffPlaying) { Line($"{tag}: reveal {view.HandoffReveal:0.00}"); yield return Snap($"{tag}_reveal", false); }
            }
            yield return Settle(() => view.HandoffPlaying);
            Line($"{tag}: settled playing={view.HandoffPlaying} alpha={view.CardAlpha(0):0.00} interactive={view.SlotInteractive(0)}");
            yield return Snap($"{tag}_settled");
            view.Choose(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bartender_MixedRarities_FullSequence()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            yield return Opening("bartender_mixed_common_rare_epic", game.CardView, Offers(CardCategory.Cocktail, CardRarity.Common, CardRarity.Rare, CardRarity.Epic), true);
        }

        [UnityTest]
        public IEnumerator Bartender_RepeatedOpenings_CommonOnly_RareMax_EpicMix()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            yield return Opening("repeat1_common_only", game.CardView, Offers(CardCategory.Cocktail, CardRarity.Common, CardRarity.Common, CardRarity.Common), true);
            yield return Opening("repeat2_common_rare", game.CardView, Offers(CardCategory.Cocktail, CardRarity.Common, CardRarity.Rare, CardRarity.Common), true);
            yield return Opening("repeat3_epic_rare_epic", game.CardView, Offers(CardCategory.Cocktail, CardRarity.Epic, CardRarity.Rare, CardRarity.Epic), true);
            yield return Opening("repeat4_two_cards_rare_common", game.CardView, Offers(CardCategory.Cocktail, CardRarity.Rare, CardRarity.Common), false);
        }

        [UnityTest]
        public IEnumerator TeaSommelier_MixedAndCommon()
        {
            yield return Start(Job.TeaSommelier, WeaponKind.Bow);
            yield return Opening("tea_mixed_common_rare_epic", game.CardView, Offers(CardCategory.Tea, CardRarity.Common, CardRarity.Rare, CardRarity.Epic), true);
            yield return Opening("tea_common_only", game.CardView, Offers(CardCategory.Tea, CardRarity.Common, CardRarity.Common), true);
        }

        [UnityTest]
        public IEnumerator Special_HybridUnlock_Bartender_AndLowEffects()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            var view = game.HybridView;
            view.Show(() => { });
            Line($"hybrid: tier={view.HandoffTier} playing={view.HandoffPlaying}");
            yield return new WaitForSecondsRealtime(0.12f); // 프레임 수가 아니라 실시간 기준(렌더 캡처가 프레임당 시간을 늘린다)
            yield return Snap("hybrid_flight", false);
            float until = Time.realtimeSinceStartup + 3f;
            while (view.HandoffPlaying && view.ContentAlpha < 0.3f && Time.realtimeSinceStartup < until) yield return null;
            yield return Snap("hybrid_reveal", false);
            while (view.HandoffPlaying && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null; yield return null;
            Line($"hybrid: settled playing={view.HandoffPlaying} alpha={view.ContentAlpha:0.00}");
            yield return Snap("hybrid_settled");
            view.Unlock();
            yield return null;
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            view.Show(() => { });
            Line($"hybrid low: playing={view.HandoffPlaying} alpha={view.ContentAlpha:0.00}");
            yield return null;
            yield return Snap("hybrid_loweffects");
            view.Unlock();
        }

        [UnityTest]
        public IEnumerator LowEffects_Card_ImmediateSettled()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            var view = game.CardView;
            view.Show(Offers(CardCategory.Cocktail, CardRarity.Common, CardRarity.Rare, CardRarity.Epic), _ => { });
            Line($"low: playing={view.HandoffPlaying} alpha={view.CardAlpha(0):0.00}");
            yield return null; yield return null;
            yield return Snap("card_loweffects");
            view.Choose(0);
        }
    }
}
